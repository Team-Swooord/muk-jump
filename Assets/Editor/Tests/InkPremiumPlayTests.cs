using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed partial class InkPremiumPlayTests
    {
        sealed class PreviewStore : IInkPremiumStore
        {
            public bool Owned;
            public bool IsOwned => Owned;
            public bool IsBusy => false;
            public bool CanPurchase => !Owned;
            public string LocalizedPrice => "₩4,900";
            public string StatusMessage => "";
            public void Purchase() { }
            public void Restore() { }
        }
        [UnityTest, Timeout(120000), Category("PremiumPlay")]
        public IEnumerator LobbyPremiumEntryFitsSmallScreensAndOpensExistingShopOnlyOnIos()
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
            var store = new PreviewStore(); InkPremiumAppearance.UseForTests(true, store);
            var oldLanguage = GameLocalization.Language;
            var window = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            int oldSize = (int)window.GetType().GetProperty("selectedSizeIndex", flags).GetValue(window);
            var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizeType).GetProperty("instance").GetValue(null);
            var sizeGroup = sizeType.GetProperty("currentGroup", flags).GetValue(sizes);
            int firstTemporary = (int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup, null);
            int added = 0;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var entry = Object.FindAnyObjectByType<InkPremiumLobbyEntry>();
                Assert.That(entry, Is.Not.Null);
                var group = entry.GetComponent<CanvasGroup>();
                var label = entry.GetComponentInChildren<Text>();
                foreach (var size in new[] { new Vector2Int(540, 960), new Vector2Int(320, 568), new Vector2Int(640, 960) })
                {
                    var kind = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var temporary = System.Activator.CreateInstance(typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                        System.Enum.Parse(kind, "FixedResolution"), size.x, size.y, "MukJump Premium QA TEMP");
                    sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { temporary });
                    window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { firstTemporary + added++, null });
                    window.Focus(); window.Repaint(); yield return Wait(.3f);
                    // EditMode 테스트 루프의 Screen은 Scene 뷰 크기일 수 있어 실제 플레이 프레임에서 읽는다.
                    Vector2Int runtimeSize = default; Rect runtimeSafe = default; bool configured = false;
                    lobby.StartCoroutine(ApplySafeAreaAtRuntime((actual, safe) =>
                    { runtimeSize = actual; runtimeSafe = safe; configured = true; }));
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!configured && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(configured, Is.True);
                    Assert.That(runtimeSize, Is.EqualTo(size), "Game view resolution");
                    yield return Wait(.2f); Canvas.ForceUpdateCanvases();
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    foreach (bool owned in new[] { false, true })
                    {
                        GameLocalization.SetLanguage(language); store.Owned = owned; yield return null;
                        Assert.That(group.alpha, Is.EqualTo(1)); Assert.That(group.blocksRaycasts, Is.True);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2));
                        Rect bounds = ScreenBounds((RectTransform)entry.transform), safe = runtimeSafe;
                        Assert.That(safe.Contains(bounds.min) && safe.Contains(bounds.max), Is.True, size + " safe area");
                        Assert.That(bounds.height, Is.GreaterThanOrEqualTo(44));
                        if (language == GameLanguage.Korean && !owned)
                            yield return Capture("layout-" + size.x, "output/qa/ios-iap-20260925/lobby-entry");
                        foreach (var button in new[] { lobby.StartButton, lobby.GrowthButton, lobby.WardrobeButton, lobby.OptionsButton, lobby.LeaderboardButton })
                            Assert.That(bounds.Overlaps(ScreenBounds((RectTransform)button.transform)), Is.False,
                                size + " " + button.name + " entry=" + bounds + " menu=" + ScreenBounds((RectTransform)button.transform) + " safe=" + safe);
                        if (size.x == 540 || size.x == 320 && language == GameLanguage.English)
                            yield return Capture("lobby-entry-" + size.x + "-" + language + "-" + owned, "output/qa/ios-iap-20260925/lobby-entry");
                        entry.Button.onClick.Invoke(); yield return null;
                        Assert.That(InkPremiumShopView.IsOpen, Is.True);
                        Object.FindAnyObjectByType<InkPremiumShopView>().Close(); yield return Wait(.3f);
                    }
                }
                InkPremiumAppearance.UseForTests(false, store); yield return null;
                Assert.That(group.alpha, Is.Zero); Assert.That(group.blocksRaycasts, Is.False);
                entry.Button.onClick.Invoke(); Assert.That(InkPremiumShopView.IsOpen, Is.False);
                InkPremiumAppearance.UseForTests(true, store);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Wait(1);
                entry.Button.onClick.Invoke(); Assert.That(InkPremiumShopView.IsOpen, Is.False);
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
        static IEnumerator ApplySafeAreaAtRuntime(System.Action<Vector2Int, Rect> configured)
        {
            yield return null;
            var size = new Vector2Int(Screen.width, Screen.height);
            MobileUiLayout.SetPlatformSafeAreaOverride(new Rect(8, 20, size.x - 16, size.y - 55));
            configured(size, MobileUiLayout.CurrentSafeArea);
        }
        static Rect ScreenBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        [UnityTest, Timeout(120000), Category("PremiumPlay")]
        public IEnumerator GoldAndPurchaseScreenRenderInThreeLanguagesWithoutRealPayment()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground; Application.runInBackground = true;
            float oldTimeScale = Time.timeScale;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var store = new PreviewStore(); InkPremiumAppearance.UseForTests(true, store);
            var previousLanguage = GameLocalization.Language;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                var view = Object.FindAnyObjectByType<InkWardrobeView>();
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!view.CanBrowse && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(view.CanBrowse, Is.True);
                view.SelectGold(); view.Presentation.Cancel(); yield return Wait(.4f);
                yield return Capture("gold-wardrobe-unowned");
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); InkPremiumAppearance.OpenShop(); yield return Wait(.4f);
                    Assert.That(InkPremiumShopView.IsOpen, Is.True);
                    var shop = Object.FindAnyObjectByType<InkPremiumShopView>();
                    foreach (var text in shop.transform.Find("PremiumShopCanvas").GetComponentsInChildren<Text>())
                        if (text.isActiveAndEnabled && text.rectTransform.rect.height > 0)
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name + " " + language);
                    yield return Capture("purchase-" + language);
                    shop.Close(); yield return Wait(.3f);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                store.Owned = true;
                Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
                view.Presentation.Cancel();
                LobbyScreenNavigator.Instance.ReturnToLobby();
                deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (!LobbyScreenNavigator.Instance.CanStartGame && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(LobbyScreenNavigator.Instance.CanStartGame, Is.True);
                yield return Wait(.1f);
                yield return Capture("gold-lobby-owned");
                Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
                GameManager.Instance.StartGameFromMenu();
                deadline = Time.realtimeSinceStartupAsDouble + 10;
                while ((GameManager.Instance.State != GameState.Playing || GameManager.Instance.IsTransitioning) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                yield return Wait(.2f);
                var player = Object.FindAnyObjectByType<PlayerController>();
                var body = player.GetComponent<SpriteRenderer>();
                Assert.That(GoldInkVisual.IsWorld(body.sharedMaterial), Is.True);
                Assert.That(RunGrowthController.Instance.SkinSnapshot, Is.EqualTo(InkSkinId.Ink));
                Assert.That(RunGrowthController.Instance.GoldSnapshot, Is.True);
                Time.timeScale = 0;
                Object.FindAnyObjectByType<GameFeedbackController>().PlayLanding(player.transform.position + Vector3.down * .5f, 10);
                yield return Wait(.1f);
                yield return Capture("gold-gameplay-landing");
                store.Owned = false; yield return Wait(.3f);
                Assert.That(GoldInkVisual.IsWorld(body.sharedMaterial), Is.False);
                Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
                yield return Capture("base-gameplay-revoked");
            }
            finally
            {
                InkPremiumAppearance.RestoreForTests();
                GameLocalization.SetLanguage(previousLanguage); Application.runInBackground = oldBackground; Time.timeScale = oldTimeScale;
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
        static IEnumerator Capture(string name, string folder = "output/qa/iap-gold-20260924")
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory(folder);
            File.Copy(path, folder + "/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
