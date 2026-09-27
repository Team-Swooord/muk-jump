using System.Reflection;
using MukJump.Core;
using MukJump.EditorTests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// 빌드 43 입력에만 복사하는 Editor 회귀 검사. 실제 저장소와 결제는 사용하지 않는다.
public sealed class SettingsPremiumLayoutTests
{
    GameObject host;
    LobbyOptionsView view;
    Transform panel, page;
    GameLanguage previousLanguage;

    [SetUp] public void SetUp()
    {
        previousLanguage = GameLocalization.Language;
        MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
    }

    [TearDown] public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        InkPremiumAppearance.RestoreForTests();
        GameLocalization.SetLanguage(previousLanguage);
        LobbyAdLayout.ClearTopInset();
        MukJumpIdentityProfile.UseStoreForTests(null);
        LobbySettingsProfile.RestoreDefaultStoreForTests();
        PointerInput.ResetSuppressionForTests();
    }

    void Build(bool premium)
    {
        InkPremiumAppearance.UseForTests(premium, null);
        host = new GameObject("SettingsPremiumLayoutTests");
        view = host.AddComponent<LobbyOptionsView>();
        view.BuildForTests();
        panel = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
        page = panel.Find("OptionsPage");
        Call("SetVisible", true);
        Call("ShowOptionsPageImmediate");
    }

    void Call(string name, params object[] args) => typeof(LobbyOptionsView)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, args);

    static Rect Bounds(Transform t)
    {
        var r = (RectTransform)t;
        return new Rect(r.anchoredPosition + r.rect.min, r.rect.size);
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    [TestCase(GameLanguage.Japanese)]
    public void PremiumAccountStatusAndLegalRowsNeverOverlap(GameLanguage language)
    {
        GameLocalization.SetLanguage(language);
        Build(true);
        foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(640, 1136) })
        foreach (float banner in new[] { 0f, .12f })
        foreach (bool showStatus in new[] { false, true, false, true })
        {
            LobbyAdLayout.SetTopInsetFraction(banner);
            view.SetDisplayMetricsForTests(size.x, size.y, new Rect(0, 20, size.x, size.y - 40));
            Call("SetSettingsStatus", showStatus ? "언어를 저장하지 못했어요. 다시 시도해 주세요" : "");
            Canvas.ForceUpdateCanvases();
            string context = language + " " + size + " banner=" + banner + " status=" + showStatus;
            var premium = Bounds(page.Find("PremiumButton"));
            foreach (string other in new[] { "AccountButton", "TermsButton", "PrivacyButton", "CloseButton" })
                Assert.That(premium.Overlaps(Bounds(page.Find(other))), Is.False, context + " " + other);
            var status = page.Find("ConnectionStatus").GetComponent<Text>();
            Assert.That(status.gameObject.activeSelf, Is.EqualTo(showStatus));
            if (showStatus)
            {
                foreach (string other in new[] { "PremiumButton", "TermsButton", "PrivacyButton" })
                    Assert.That(Bounds(status.transform).Overlaps(Bounds(page.Find(other))), Is.False, context + " status/" + other);
            }
            foreach (string textName in new[] { "PremiumButton", "TermsButton", "PrivacyButton", "ConnectionStatus" })
            foreach (var text in page.Find(textName).GetComponentsInChildren<Text>())
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), context + " " + text.name);
        }
    }

    [Test] public void NonIosSettingsKeepExistingCompactLayoutAndNoPremium()
    {
        Build(false);
        Assert.That(page.Find("PremiumButton"), Is.Null);
        Assert.That(((RectTransform)panel).rect.height, Is.EqualTo(1220));
        Call("SetSettingsStatus", "상태 문구");
        Assert.That(((RectTransform)panel).rect.height, Is.EqualTo(1330));
        Call("SetSettingsStatus", "");
        Assert.That(((RectTransform)panel).rect.height, Is.EqualTo(1220));
    }

    [Test] public void PremiumButtonStillRequestsExistingShop()
    {
        Build(true);
        int requests = 0;
        System.Action listener = () => requests++;
        InkPremiumAppearance.ShopRequested += listener;
        try { page.Find("PremiumButton").GetComponent<Button>().onClick.Invoke(); }
        finally { InkPremiumAppearance.ShopRequested -= listener; }
        Assert.That(requests, Is.EqualTo(1));
    }
}
