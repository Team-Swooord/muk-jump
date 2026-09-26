using System.Collections;
using System.IO;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkCosmeticPlayTests
    {
        [UnityTest, Timeout(150000)]
        public IEnumerator IndependentCosmeticsRenderInThreeLanguagesAndRealPlay()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            InkPremiumAppearance.UseForTests(false, null); SkinDebugSession.Reset();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation();
                var view = Object.FindFirstObjectByType<InkWardrobeView>();
                view.SelectSlot(InkCosmeticSlot.Clone); view.SelectEffect(InkCosmeticCatalog.Tadpole);
                yield return Wait(.5f); yield return Capture("locked-tadpole");
                var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
                stats.drawnLandings = 200; stats.items = 1000; stats.maxLivingPlayers = 12;
                Assert.That(PermanentGrowthProfile.SettleRun("cosmetic-play-fixture", 500, 500, 0, 60, true, stats).Accepted, Is.True);
                view.SelectNest(); view.EquipButton.onClick.Invoke();
                view.SelectSlot(InkCosmeticSlot.Clone); view.SelectEffect(InkCosmeticCatalog.Tadpole); view.EquipButton.onClick.Invoke();
                view.SelectSlot(InkCosmeticSlot.Landing); view.SelectEffect(InkCosmeticCatalog.BambooLeaf); view.EquipButton.onClick.Invoke();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    view.SelectSlot(InkCosmeticSlot.Skin); yield return Wait(.5f); yield return Capture("nest-" + language);
                    view.SelectSlot(InkCosmeticSlot.Landing); yield return Wait(.5f); yield return Capture("landing-" + language);
                    view.SelectSlot(InkCosmeticSlot.Clone); yield return Wait(.5f); yield return Capture("clone-" + language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                view.BackButton.onClick.Invoke(); yield return Navigation(); yield return Wait(.3f); yield return Capture("lobby-nest");
                Object.FindFirstObjectByType<LobbyView>().StartButton.onClick.Invoke();
                float until = Time.realtimeSinceStartup + 20;
                while ((GameManager.Instance.State == GameState.Lobby || GameManager.Instance.IsTransitioning) && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                Assert.That(GameManager.Instance.PauseGame(), Is.True);
                var original = Object.FindFirstObjectByType<PlayerController>();
                var clone = Object.Instantiate(original.gameObject); clone.GetComponent<PlayerController>().ConfigureAsClone(3f);
                clone.transform.position = original.transform.position + Vector3.right * 1.2f;
                Assert.That(clone.GetComponent<PlayerController>().IsRuntimeClone, Is.True);
                Assert.That(RunGrowthController.Instance.CloneSnapshot, Is.EqualTo(InkCosmeticCatalog.Tadpole));
                // 복제 직후에는 원본 장식이 남는다. 실제 LateUpdate 표시 갱신을 기다린다.
                var cloneView = clone.GetComponent<InkSkinView>();
                var tail = InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole);
                float appearanceDeadline = Time.realtimeSinceStartup + 2f;
                while (cloneView.AccessoryRenderer.sprite != tail && Time.realtimeSinceStartup < appearanceDeadline)
                    yield return null;
                Assert.That(original.GetComponent<InkSkinView>().AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
                Assert.That(clone.GetComponent<InkSkinView>().AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole)));
                Assert.That(RunGrowthController.Instance.LandingSnapshot, Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
                yield return Capture("world-nest-and-tadpole");
                Object.Destroy(clone);
            }
            finally
            {
                SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static IEnumerator Navigation()
        {
            float until = Time.realtimeSinceStartup + 20;
            while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False); yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/wardrobe-next-20260926/screens");
            File.Copy(path, "output/qa/wardrobe-next-20260926/screens/" + name + ".png", true);
        }
    }
}
