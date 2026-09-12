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
        [UnityTest, Timeout(240000)]
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
                Assert.That(wardrobe.EquipButton.interactable, Is.False);
                wardrobe.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
                for (int i = 0; i < 10; i++) PermanentGrowthProfile.SettleRun("skin-unlock-" + i, 10, 0, true);
                Assert.That(ScoreManager.Instance.TryCommitBestCandidate(100), Is.True);
                yield return Wait(.3f);
                Assert.That(wardrobe.EquipButton.interactable, Is.True);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                // 실제 서버 계정은 건드리지 않고 메모리 기록으로 착용 검수한다.
                var measured = SkinRunStats.Begin(true, InkSkinId.CatEars);
                measured.drawnLandings=100; measured.items=30; measured.inkDrops=1; measured.maxLivingPlayers=3;
                for(int i=0;i<100;i++) PermanentGrowthProfile.SettleRun("art-review-"+i,200,200,0,120,true,measured);
                PermanentGrowthProfile.SettleRun("art-review-peak",2000,2000,0,120,true,measured);
                PermanentGrowthProfile.SettleRun("art-review-hidden",777,777,0,60,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
                for(int i=0;i<6;i++) SkinSecrets.AcceptedNightChange(i%2==0);
                foreach(var id in new[]{InkSkinId.Ink,InkSkinId.Gat,InkSkinId.Ink,InkSkinId.SpiderMask,InkSkinId.Ink}) wardrobe.Select(id);
                int purchased=0;
                foreach(var node in PermanentGrowthCatalog.Nodes)
                    if(purchased<5 && PermanentGrowthProfile.TryPurchaseNode(node)) purchased++;
                for(int i=0;i<6;i++)
                {
                    wardrobe.Select((InkSkinId)(i*6));
                    yield return Wait(.3f);yield return Capture("review-page-"+i);
                }
                foreach(var id in new[]{InkSkinId.CloudDragon,InkSkinId.InkMecha,InkSkinId.ClockworkCat})
                {
                    wardrobe.Select(id);yield return Wait(.3f);yield return Capture("review-detail-"+id);
                    Assert.That(wardrobe.Paper.Find("Preview/OriginalInkBody").GetComponent<UnityEngine.UI.Image>().enabled,Is.True);
                }
                wardrobe.Select(InkSkinId.SpiderMask);
                yield return Wait(.3f);
                yield return Capture("wardrobe-unlocked");
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
                // 새 특별 의상은 UI뿐 아니라 실제 플레이/분신/밤 배경에서도 원먹을 유지해야 한다.
                foreach (bool night in new[] { false, true })
                foreach (var id in new[] { InkSkinId.CloudDragon, InkSkinId.InkMecha, InkSkinId.ClockworkCat, InkSkinId.OrigamiWings })
                {
                    typeof(LobbyNightState).GetMethod("Set", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .Invoke(null, new object[] { night ? 1f : 0f, night, 0f, 0f, 0f });
                    SceneManager.LoadScene("Main");
                    yield return Wait(.8f);
                    manager = GameManager.Instance;
                    Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True, id.ToString());
                    manager.StartGameFromMenu();
                    tickingDeadline = Time.realtimeSinceStartup + 10;
                    while (!manager.IsGameplayTicking && Time.realtimeSinceStartup < tickingDeadline) yield return null;
                    Assert.That(manager.IsGameplayTicking, Is.True);
                    debugControls = Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                    if (debugControls != null) debugControls.gameObject.SetActive(false);
                    yield return Wait(.7f);
                    Assert.That(manager.TryCreateInkClone(manager.HighestLivingPlayer), Is.True);
                    yield return Wait(.2f);
                    manager.GetLivingPlayersNonAlloc(players);
                    Assert.That(players.Count, Is.GreaterThanOrEqualTo(2));
                    // 분신 생성 직후 무적 깜박임의 숨김 프레임은 의상 누락이 아니다.
                    float visibleDeadline = Time.realtimeSinceStartup + 2f;
                    while (players.Exists(p => !p.GetComponent<SpriteRenderer>().enabled) && Time.realtimeSinceStartup < visibleDeadline)
                        yield return null;
                    yield return null;
                    foreach (var alive in players)
                    {
                        var skin = alive.GetComponent<InkSkinView>();
                        var body = alive.GetComponent<SpriteRenderer>();
                        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
                        Assert.That(body.enabled, Is.True);
                        Assert.That(skin.AccessoryRenderer.enabled, Is.True);
                        Assert.That(skin.AccessoryRenderer.sprite, Is.SameAs(InkSkinCatalog.Sprite(id)));
                        Assert.That(skin.AccessoryRenderer.sortingOrder, Is.EqualTo(body.sortingOrder + (InkSkinCatalog.BehindBody(id) ? -1 : 1)));
                    }
                    Assert.That(players[0].GetComponent<InkSkinView>().AccessoryRenderer.isVisible, Is.True);
                    yield return Capture("review-play-" + id + (night ? "-night" : "-day"));
                }
            }
            finally
            {
                LobbyNightState.Reset();
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
