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
    public sealed class InkPremiumPlayTests
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
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/iap-gold-20260924");
            File.Copy(path, "output/qa/iap-gold-20260924/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
