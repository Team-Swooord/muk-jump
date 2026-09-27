using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTests;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class SettingsPremiumPlayTests
{
    sealed class Store : IInkPremiumStore
    {
        public bool IsOwned => false;
        public bool IsBusy => false;
        public bool CanPurchase => false;
        public string LocalizedPrice => "";
        public string StatusMessage => "";
        public void Purchase() => Assert.Fail("표시 검사에서 결제를 실행하면 안 됩니다.");
        public void Restore() => Assert.Fail("표시 검사에서 복원을 실행하면 안 됩니다.");
    }
    static IEnumerator Wait(float seconds)
    {
        double until = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
    }
    static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static IEnumerator Capture(string name)
    {
        object result = MukJumpAgentAudit.CaptureUi();
        string path = (string)result.GetType().GetProperty("path").GetValue(result);
        double until = Time.realtimeSinceStartupAsDouble + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(File.Exists(path), Is.True);
        Directory.CreateDirectory("../play-captures");
        File.Copy(path, "../play-captures/" + name + ".png", true);
    }
    static IEnumerator ConfigureRuntime(LobbyOptionsView options, Vector2Int size, System.Action<System.Exception> done)
    {
        yield return null;
        try
        {
            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(size));
            var safe = new Rect(0, 20, size.x, size.y - 40);
            MobileUiLayout.SetPlatformSafeAreaOverride(safe);
            LobbyAdLayout.SetTopInsetFraction(.12f);
            options.SetDisplayMetricsForTests(size.x, size.y, safe);
            done(null);
        }
        catch (System.Exception error) { done(error); }
    }
    static IEnumerator CheckRuntime(Transform page, System.Action<System.Exception> done)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            Canvas.ForceUpdateCanvases();
            var premium = ScreenBounds((RectTransform)page.Find("PremiumButton"));
            foreach (string name in new[] { "AccountButton", "TermsButton", "PrivacyButton", "CloseButton" })
                Assert.That(premium.Overlaps(ScreenBounds((RectTransform)page.Find(name))), Is.False, name);
            foreach (string name in new[] { "PremiumButton", "TermsButton", "PrivacyButton" })
            {
                var button = page.Find(name).GetComponent<Button>();
                var bounds = ScreenBounds((RectTransform)button.transform);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = bounds.center }, hits);
                Button top = null;
                foreach (var hit in hits) { top = hit.gameObject.GetComponentInParent<Button>(); if (top != null) break; }
                Assert.That(top, Is.EqualTo(button), name + " 개별 터치 대상 " + bounds + " screen=" + Screen.width + "x" + Screen.height);
            }
            foreach (var text in page.GetComponentsInChildren<Text>())
                if (text.name != "Nickname") Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name);
            done(null);
        }
        catch (System.Exception error) { done(error); }
    }
    [UnityTest, Timeout(180000)]
    public IEnumerator ThreeLanguagesBannerStatusAndIndividualHitTargets()
    {
        yield return new EnterPlayMode();
        bool oldBackground = Application.runInBackground; Application.runInBackground = true;
        var settings = new MemoryLobbySettingsStore();
        LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
        MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
        LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
        PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
        ScoreManager.UseStoreForTests(new MemoryScoreStore());
        GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        InkPremiumAppearance.UseForTests(true, new Store());
        var oldLanguage = GameLocalization.Language;
        var window = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        int oldSize = (int)window.GetType().GetProperty("selectedSizeIndex", flags).GetValue(window);
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetProperty("currentGroup", flags).GetValue(sizes);
        int start = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), added = 0;
        try
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
            Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
            var options = Object.FindAnyObjectByType<LobbyOptionsView>();
            foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844), new Vector2Int(640, 1136) })
            {
                var kind = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                var temp = System.Activator.CreateInstance(typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                    System.Enum.Parse(kind, "FixedResolution"), size.x, size.y, "MukJump Settings 44 TEMP");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { temp });
                window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { start + added++, null });
                window.Focus(); window.Repaint(); yield return Wait(.3f);
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); options.Open(); yield return Wait(.6f);
                    // 광고 SDK·실제 노출 없이 배너가 차지할 화면 영역을 예약한다.
                    bool configured = false; System.Exception runtimeError = null;
                    options.StartCoroutine(ConfigureRuntime(options, size, e => { configured = true; runtimeError = e; }));
                    while (!configured) yield return null;
                    if (runtimeError != null) throw runtimeError;
                    var page = options.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
                    foreach (bool showStatus in new[] { false, true })
                    {
                        typeof(LobbyOptionsView).GetMethod("SetSettingsStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(options, new object[] { showStatus ? "언어를 저장하지 못했어요. 다시 시도해 주세요" : "" });
                        yield return Wait(.2f);
                        bool checkedFrame = false; runtimeError = null;
                        options.StartCoroutine(CheckRuntime(page, e => { checkedFrame = true; runtimeError = e; }));
                        while (!checkedFrame) yield return null;
                        if (runtimeError != null) throw runtimeError;
                        yield return Capture("settings-" + size.x + "-" + language + "-status-" + showStatus);
                    }
                    options.Close(); yield return Wait(.3f);
                }
            }
        }
        finally
        {
            MobileUiLayout.ClearPlatformSafeAreaOverride(); LobbyAdLayout.ClearTopInset();
            window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { oldSize, null });
            for (int i = start + added - 1; i >= start; --i) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { i });
            InkPremiumAppearance.RestoreForTests(); GameLocalization.SetLanguage(oldLanguage);
            Application.runInBackground = oldBackground;
            MukJumpIdentityProfile.UseStoreForTests(null);
            LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
        }
        yield return new ExitPlayMode();
    }
}
