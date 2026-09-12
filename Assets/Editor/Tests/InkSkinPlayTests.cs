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
    public sealed class InkSkinPlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator WardrobeAndEquippedPlayerRenderInRealMainWithoutServerWrites()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                yield return Wait(1.2f);
                var manager = GameManager.Instance;
                Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
                Assert.That(MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.True);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                Assert.That(lobby.WardrobeButton, Is.Not.Null);
                Assert.That(lobby.WardrobeButton.GetComponent<RectTransform>().position.y,
                    Is.GreaterThan(lobby.OptionsButton.GetComponent<RectTransform>().position.y));
                yield return Capture("lobby");
                var navigator = LobbyScreenNavigator.Instance;
                Assert.That(navigator.OpenWardrobe(), Is.True);
                yield return Wait(4f);
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>();
                Assert.That(wardrobe.IsOpen, Is.True);
                foreach (GameLanguage language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    wardrobe.Select(language == GameLanguage.Japanese ? InkSkinId.SpiderMask : InkSkinId.Gat);
                    yield return Wait(.3f);
                    yield return Capture("wardrobe-" + language);
                }
                wardrobe.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.SpiderMask));
                wardrobe.BackButton.onClick.Invoke();
                yield return Wait(4f);
                manager.StartGameFromMenu();
                float tickingDeadline = Time.realtimeSinceStartup + 10;
                while (!manager.IsGameplayTicking && Time.realtimeSinceStartup < tickingDeadline) yield return null;
                Assert.That(manager.IsGameplayTicking, Is.True);
                var debugControls = Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                if (debugControls != null) debugControls.gameObject.SetActive(false);
                yield return Wait(1.2f);
                Assert.That(manager.State, Is.EqualTo(GameState.Playing));
                Assert.That(RunGrowthController.Instance.SkinSnapshot, Is.EqualTo(InkSkinId.SpiderMask));
                var player = Object.FindAnyObjectByType<InkSkinView>();
                Assert.That(player.AccessoryRenderer.enabled, Is.True);
                Assert.That(player.AccessoryRenderer.isVisible, Is.True, "장식이 실제 게임 카메라에 보여야 합니다.");
                Assert.That(player.AccessoryRenderer.color.a, Is.GreaterThan(.9f));
                yield return Capture("spider-player");
                Assert.That(manager.TryCreateInkClone(manager.HighestLivingPlayer), Is.True);
                yield return Wait(.4f);
                var players = new System.Collections.Generic.List<PlayerController>();
                manager.GetLivingPlayersNonAlloc(players);
                Assert.That(players.Count, Is.GreaterThanOrEqualTo(2));
                foreach (var alive in players)
                {
                    var skin = alive.GetComponent<InkSkinView>();
                    Assert.That(skin, Is.Not.Null);
                    Assert.That(skin.AccessoryRenderer.enabled, Is.True);
                    Assert.That(skin.AccessoryRenderer.sprite.name, Is.EqualTo("skin_spider_mask_v1"));
                }
            }
            finally
            {
                Application.runInBackground = oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }

        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True, "합성 UI 캡처 파일이 생성되어야 합니다.");
            string destination = Path.GetFullPath("output/qa/ink-skins/" + name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, true);
            File.Delete(path);
        }
        static IEnumerator Wait(float duration)
        {
            float until = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
