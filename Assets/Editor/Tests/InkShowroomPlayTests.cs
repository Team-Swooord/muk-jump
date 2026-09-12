using System.Collections;
using System.IO;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkShowroomPlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator HeadbandsSitOnTheForeheadInShowroomAndActualGameplay()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1f);
                var stats=SkinRunStats.Begin(true,InkSkinId.CatEars);
                stats.drawnLandings=10; stats.items=stats.inkDrops=1;
                PermanentGrowthProfile.SettleRun("headband-fit",100,100,0,60,true,stats);
                PermanentGrowthProfile.SettleRun("headband-fit-secret",100,100,0,60,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
                foreach(var id in new[]{InkSkinId.RedHeadband,InkSkinId.CatEars,InkSkinId.ClockworkCat})
                {
                    yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(.8f);
                    var manager=GameManager.Instance;
                    Assert.That(manager.State,Is.EqualTo(GameState.Lobby));
                    Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                    Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Wait(3f);
                    var view=Object.FindAnyObjectByType<InkWardrobeView>();
                    view.Select(id); yield return Wait(.6f); yield return Capture("headband-preview-"+id);
                    view.EquipButton.onClick.Invoke(); Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(id));
                    view.BackButton.onClick.Invoke(); yield return Wait(3f);
                    manager.StartGameFromMenu();
                    float deadline=Time.realtimeSinceStartup+8;
                    while(!manager.IsGameplayTicking && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(manager.IsGameplayTicking,Is.True);
                    var debugControls=Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                    if(debugControls!=null) debugControls.gameObject.SetActive(false);
                    yield return Wait(.5f);
                    Assert.That(RunGrowthController.Instance.SkinSnapshot,Is.EqualTo(id));
                    var body=manager.HighestLivingPlayer.GetComponent<SpriteRenderer>();
                    var accessory=body.GetComponent<MukJump.Player.InkSkinView>().AccessoryRenderer;
                    Assert.That(accessory.enabled && accessory.isVisible,Is.True);
                    Assert.That(accessory.sprite,Is.SameAs(InkSkinCatalog.Sprite(id)));
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(body.sprite),Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
                    yield return Capture("headband-player-"+id);
                }
            }
            finally
            {
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ShowroomRendersBrowsingEquipAndCloseInRealMainWithoutServerWrites()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1.2f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.True);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                Assert.That(lobby.WardrobeButton.transform.position.y, Is.GreaterThan(lobby.OptionsButton.transform.position.y));
                yield return Capture("lobby");
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                yield return Wait(3f);
                var view = Object.FindAnyObjectByType<InkWardrobeView>(); Assert.That(view.IsOpen, Is.True);
                var measured = SkinRunStats.Begin(true, InkSkinId.CatEars);
                for (int i=0;i<100;i++) PermanentGrowthProfile.SettleRun("showroom-capture-"+i,200,200,0,120,true,measured);
                PermanentGrowthProfile.SettleRun("showroom-peak",2000,2000,0,120,true,measured);
                Assert.That(ScoreManager.Instance.TryCommitBestCandidate(2000), Is.True);
                foreach (var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); view.Select(InkSkinId.Gat);
                    yield return Wait(.6f); yield return Capture("showroom-"+language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                foreach (var id in new[]{InkSkinId.InkMecha,InkSkinId.ClockworkCat,InkSkinId.CloudDragon})
                {
                    view.Select(id); yield return Wait(.6f); yield return Capture("showroom-"+id);
                    var body=view.Paper.Find("ShowroomStage/Preview/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(body.enabled, Is.True);
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
                }
                foreach(var tall in new[]{InkSkinId.PhoenixCrown,InkSkinId.InkGhost})
                {
                    Assert.That(InkSkinAchievements.IsUnlocked(tall), Is.True);
                    view.Select(tall); yield return Wait(.6f); yield return Capture("tall-rest-"+tall);
                    view.StepSelection(1); yield return Wait(.06f); yield return Capture("tall-change-"+tall);
                    yield return Wait(.6f);
                }
                view.Select(InkSkinId.Gat); yield return Wait(.6f);
                view.Paper.Find("ShowroomStage/NextCostume").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(view.Selected, Is.EqualTo(InkSkinId.SpiderMask));
                Assert.That(view.Presentation.IsPlaying, Is.True);
                yield return Wait(.06f); yield return Capture("arrow-change-start");
                yield return Wait(.07f); yield return Capture("arrow-change-settle");
                yield return Wait(.6f);
                Assert.That(view.Presentation.IsPlaying, Is.False);
                Assert.That(view.Paper.Find("ShowroomStage/Preview/OutgoingCostume").GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                yield return Capture("arrow-change-complete");
                view.Select(InkSkinId.Gat); yield return Wait(.6f);
                view.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
                Assert.That(view.Presentation.IsEquipping, Is.True);
                yield return Wait(.17f);
                Assert.That(view.Presentation.IsEquipping, Is.True);
                Canvas.ForceUpdateCanvases();
                var fxRoot = view.Paper.Find("ShowroomStage/ShowroomInkEffect");
                Assert.That(fxRoot.gameObject.activeInHierarchy, Is.True);
                foreach (var graphic in fxRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                {
                    Assert.That(graphic.canvasRenderer.cull, Is.False, graphic.name);
                    Assert.That(graphic.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.9f), graphic.name);
                    var mesh = graphic.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0), graphic.name);
                    Debug.Log("[ShowroomRender] " + graphic.name + " vertices=" + mesh.vertexCount + " alpha=" + graphic.color.a);
                }
                yield return Capture("equip-ripple");
                yield return Wait(.18f); yield return Capture("equip-hanji");
                yield return Wait(1.4f); Assert.That(view.Presentation.IsPlaying, Is.False);
                yield return Capture("equipped");
                view.Select(InkSkinId.SpiderMask); view.EquipButton.onClick.Invoke();
                view.BackButton.onClick.Invoke(); yield return Wait(3f);
                Assert.That(view.IsOpen, Is.False); Assert.That(view.Presentation.IsPlaying, Is.False);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Wait(3f);
                Assert.That(view.Selected, Is.EqualTo(InkSkinId.SpiderMask));
                Assert.That(view.Presentation.ParticleCount, Is.Zero);
            }
            finally
            {
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Capture(string name)
        {
            string path=JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(File.Exists(path), Is.True);
            string destination=Path.GetFullPath("output/qa/ink-showroom/"+name+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path,destination,true); File.Delete(path);
        }
        static IEnumerator Wait(float duration)
        {
            float deadline=Time.realtimeSinceStartup+duration;
            while(Time.realtimeSinceStartup<deadline) yield return null;
        }
    }
}
