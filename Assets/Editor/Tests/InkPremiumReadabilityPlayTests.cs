using System.Collections;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed partial class InkPremiumPlayTests
    {
        [UnityTest, Timeout(120000), Category("PremiumReadability")]
        public IEnumerator PremiumReadabilityAndRestoreOptionsInThreeLanguages()
        {
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var store = new InkPremiumUiTests.Store(); InkPremiumAppearance.UseForTests(true, store);
            var oldLanguage = GameLocalization.Language;
            var window = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            int oldSize = (int)window.GetType().GetProperty("selectedSizeIndex", flags).GetValue(window);
            var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizeType).GetProperty("instance").GetValue(null);
            var sizeGroup = sizeType.GetProperty("currentGroup", flags).GetValue(sizes);
            int firstTemporary = (int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup, null);
            int added = 0;
            const string output = "output/qa/premium-copy-20260927";
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var shop = Object.FindAnyObjectByType<InkPremiumShopView>();
                var options = Object.FindAnyObjectByType<LobbyOptionsView>();
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844) })
                {
                    var kind = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var temporary = System.Activator.CreateInstance(typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                        System.Enum.Parse(kind, "FixedResolution"), size.x, size.y, "MukJump Premium Readability TEMP");
                    sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { temporary });
                    window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { firstTemporary + added++, null });
                    window.Focus(); window.Repaint(); yield return Wait(.3f);
                    bool configured = false; Vector2Int actualSize = default;
                    lobby.StartCoroutine(ApplySafeAreaAtRuntime((actual, safe) => { configured = true; actualSize = actual; }));
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!configured && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(configured, Is.True); Assert.That(actualSize, Is.EqualTo(size));
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        // 표시 테스트용 가격이며 실기기 스토어 상품 조회 결과가 아니다.
                        store.Price = language == GameLanguage.Korean ? "₩4,900" : language == GameLanguage.Japanese ? "¥500" : "$2.99";
                        GameLocalization.SetLanguage(language); InkPremiumAppearance.OpenShop(); yield return Wait(.5f);
                        var paper = shop.transform.Find("PremiumShopCanvas/SafeArea/PremiumPaper");
                        Assert.That(paper.Find("Restore"), Is.Null);
                        Assert.That(paper.Find("Close"), Is.Null);
                        InkPremiumUiTests.AssertConcisePresentation(paper, language);
                        foreach (var text in paper.GetComponentsInChildren<Text>())
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name + " " + language);
                        var purchase = paper.Find("Purchase").GetComponent<Button>();
                        Assert.That(ScreenBounds((RectTransform)purchase.transform).height, Is.GreaterThanOrEqualTo(44));
                        yield return Capture("shop-" + size.x + "-" + language, output);
                        if (size.x == 390 && language == GameLanguage.Korean)
                        {
                            yield return Wait(1.3f); yield return Capture("gold-shimmer-later", output);
                        }
                        store.Busy = size.x == 390;
                        bool checkedDismiss = false; System.Exception dismissError = null;
                        lobby.StartCoroutine(CheckShopBackdropAtRuntime(shop, error =>
                            { checkedDismiss = true; dismissError = error; }));
                        double dismissDeadline = Time.realtimeSinceStartupAsDouble + 5;
                        while (!checkedDismiss && Time.realtimeSinceStartupAsDouble < dismissDeadline) yield return null;
                        Assert.That(checkedDismiss, Is.True); Assert.That(dismissError, Is.Null, dismissError?.ToString());
                        yield return Wait(.4f);
                        Assert.That(InkPremiumShopView.IsOpen, Is.False);
                        Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby), "닫는 입력으로 게임을 시작하지 않는다");
                        Assert.That(store.Busy, Is.EqualTo(size.x == 390), "화면 닫기는 구매 처리 상태를 바꾸지 않는다");
                        store.Busy = false;
                        options.Open(); yield return Wait(.5f);
                        Assert.That(options.GetComponentsInChildren<Button>(true).Any(b => b.name.Contains("Close")), Is.False);
                        var restore = options.GetComponentsInChildren<Button>(true).Single(b => b.name == "RestorePurchasesButton");
                        Assert.That(restore.gameObject.activeInHierarchy, Is.True);
                        foreach (string name in new[] { "AccountButton", "TermsButton", "PrivacyButton" })
                        {
                            var other = restore.transform.parent.Find(name) as RectTransform;
                            Assert.That(ScreenBounds((RectTransform)restore.transform).Overlaps(ScreenBounds(other)), Is.False, name);
                        }
                        restore.onClick.Invoke(); yield return Wait(.3f);
                        Assert.That(restore.interactable, Is.False);
                        store.Busy = false; store.Message = "현재 App Store 계정의 구매 내역이 없어요";
                        yield return Wait(.3f);
                        var status = restore.transform.parent.Find("ConnectionStatus").GetComponent<Text>();
                        Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 2), language.ToString());
                        Assert.That(ScreenBounds((RectTransform)restore.transform).Overlaps(ScreenBounds(status.rectTransform)), Is.False);
                        yield return Capture("settings-" + size.x + "-" + language, output);
                        options.Close(); yield return Wait(.4f);
                    }
                }
                Assert.That(store.Purchases, Is.Zero); Assert.That(store.Owned, Is.False);
                Assert.That(store.Restores, Is.EqualTo(6));
                GameLocalization.SetLanguage(GameLanguage.Korean);
                store.Owned = true;
                Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
                yield return Wait(.3f);
                GameManager.Instance.StartGameFromMenu();
                double playingDeadline = Time.realtimeSinceStartupAsDouble + 12;
                while ((GameManager.Instance.State != GameState.Playing || GameManager.Instance.IsTransitioning) &&
                    Time.realtimeSinceStartupAsDouble < playingDeadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                yield return Wait(.2f);
                var body = Object.FindAnyObjectByType<PlayerController>().GetComponent<SpriteRenderer>();
                Assert.That(GoldInkVisual.IsWorld(body.sharedMaterial), Is.True);
                Assert.That(ShaderUtil.ShaderHasError(body.sharedMaterial.shader), Is.False);
                yield return Capture("gold-playing", output);
                store.Owned = false; yield return null;
                Assert.That(GoldInkVisual.IsWorld(body.sharedMaterial), Is.False);
            }
            finally
            {
                MobileUiLayout.ClearPlatformSafeAreaOverride();
                window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { oldSize, null });
                for (int index = firstTemporary + added - 1; index >= firstTemporary; index--)
                    sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup, new object[] { index });
                InkPremiumAppearance.RestoreForTests(); GameLocalization.SetLanguage(oldLanguage);
                Application.runInBackground = oldBackground;
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }

        static IEnumerator CheckShopBackdropAtRuntime(InkPremiumShopView shop, System.Action<System.Exception> completed)
        {
            yield return null;
            System.Exception failure = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                var canvas = shop.transform.Find("PremiumShopCanvas");
                var paper = (RectTransform)canvas.Find("SafeArea/PremiumPaper");
                var backdrop = canvas.Find("Blocker").GetComponent<EventTrigger>();
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, paper.position);
                Vector2 outside = new Vector2(2, 2);
                pointer.position = pointer.pressPosition = inside;
                backdrop.OnPointerClick(pointer);
                Assert.That(InkPremiumShopView.IsOpen, Is.True);
                pointer.position = outside;
                backdrop.OnPointerClick(pointer);
                Assert.That(InkPremiumShopView.IsOpen, Is.True, "안에서 밖으로 끌어도 유지한다");
                pointer.pressPosition = outside; pointer.dragging = true;
                backdrop.OnPointerClick(pointer);
                Assert.That(InkPremiumShopView.IsOpen, Is.True);
                pointer.dragging = false;
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                    Is.EqualTo(backdrop.gameObject), "바깥 탭은 뒤 로비가 아닌 장막에 도착한다");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(paper.GetComponent<HanjiScrollFrame>().IsClosing, Is.True);
                Assert.That(canvas.GetComponent<CanvasGroup>().blocksRaycasts, Is.True, "닫힘 애니메이션 중 입력도 차단한다");
            }
            catch (System.Exception error) { failure = error; }
            completed(failure);
        }
    }
}
