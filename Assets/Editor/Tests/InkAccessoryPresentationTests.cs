using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.Player;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkAccessoryPresentationTests
    {
        [UnityTest, Timeout(150000)]
        public IEnumerator AnimatedAccessoriesFitAndPauseInRealScreens()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(SkinDebugSession.SetAllUnlocked(true),Is.True);
                Assert.That(InkSkinPreferences.TryEquip(InkSkinId.BlueWisps),Is.True);
                yield return Wait(.2f);
                var preview=Object.FindFirstObjectByType<LobbySkinPreview>();
                var lobbyArt=preview.transform.Find("Drift/Artwork/Accessory").GetComponent<InkAccessoryImage>();
                Assert.That(lobbyArt.HasMotion,Is.True);
                float clock=lobbyArt.MotionTime; yield return Wait(.3f); Assert.That(lobbyArt.MotionTime,Is.GreaterThan(clock));
                foreach(var vertex in lobbyArt.canvasRenderer.GetMesh().vertices)
                    Assert.That(preview.Area.rect.Contains(preview.Area.InverseTransformPoint(lobbyArt.transform.TransformPoint(vertex))),Is.True);
                yield return Capture("lobby-wisps");
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                clock=lobbyArt.MotionTime; yield return Wait(.25f); Assert.That(lobbyArt.MotionTime,Is.EqualTo(clock));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f); Assert.That(lobbyArt.HasMotion,Is.False);
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Navigation();
                var wardrobe=Object.FindFirstObjectByType<InkWardrobeView>();
                var stage=(RectTransform)wardrobe.Paper.Find("ShowroomStage");
                var art=stage.Find("Preview/Accessory").GetComponent<InkAccessoryImage>();
                var ids=new[]{InkSkinId.BlueWisps,InkSkinId.PaperLanterns,InkSkinId.MoonClouds,
                    InkSkinId.SpiritHalo,InkSkinId.CraneWings,InkSkinId.ButterflyWings,InkSkinId.OrigamiWings,InkSkinId.StormScarf};
                for(int i=0;i<ids.Length;i++)
                {
                    GameLocalization.SetLanguage(i==1 ? GameLanguage.English : i==2 ? GameLanguage.Japanese : GameLanguage.Korean);
                    wardrobe.Select(ids[i]); yield return Wait(1.1f);
                    Assert.That(art.HasMotion,Is.True,ids[i].ToString());
                    var before=art.canvasRenderer.GetMesh().vertices; yield return Wait(.4f);
                    var after=art.canvasRenderer.GetMesh().vertices;
                    Assert.That(before.Zip(after,(a,b)=>Vector3.Distance(a,b)).Max(),Is.GreaterThan(.01f));
                    foreach(var vertex in after)
                        Assert.That(stage.rect.Contains(stage.InverseTransformPoint(art.transform.TransformPoint(vertex))),Is.True,ids[i]+" clip");
                    Assert.That(wardrobe.CollectionContent.GetComponentsInChildren<InkAccessoryImage>(true).All(a=>!a.HasMotion),Is.True);
                    yield return Capture(ids[i]+"-a"); yield return Wait(.65f); yield return Capture(ids[i]+"-b");
                }
                SkinDebugSession.SetAllUnlocked(false); wardrobe.Select(InkSkinId.BlueWisps); yield return Wait(1);
                Assert.That(art.HasMotion,Is.False); Assert.That(art.color,Is.EqualTo(Color.black));
                wardrobe.Select(InkSkinId.OrigamiWings); yield return Wait(1); Assert.That(art.enabled,Is.False);
                SkinDebugSession.SetAllUnlocked(true); wardrobe.Select(InkSkinId.BlueWisps);
                // 전체 해금을 껐다 켜면 시험 착용도 지워진다. 선택과 장착은 별개다.
                Assert.That(InkSkinPreferences.TryEquip(InkSkinId.BlueWisps),Is.True);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                wardrobe.BackButton.onClick.Invoke(); yield return Navigation();
                var lobby=Object.FindFirstObjectByType<LobbyView>(); lobby.StartButton.onClick.Invoke();
                float until=Time.realtimeSinceStartup+20;
                while(GameManager.Instance.State==GameState.Lobby && Time.realtimeSinceStartup<until) yield return null;
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Playing)); yield return null;
                // Playing은 붓 전환 중간에 바뀐다. 가림막이 걷힌 실제 플레이 화면을 기다린다.
                until=Time.realtimeSinceStartup+20;
                while(GameManager.Instance.IsTransitioning && Time.realtimeSinceStartup<until) yield return null;
                Assert.That(GameManager.Instance.IsTransitioning,Is.False);
                var skin=Object.FindFirstObjectByType<InkSkinView>();
                var motion=skin.GetComponentInChildren<InkAccessoryMesh>();
                Assert.That(motion,Is.Not.Null); Assert.That(motion.Renderer.enabled,Is.True);
                Assert.That(skin.AccessoryRenderer.forceRenderingOff,Is.True);
                yield return Capture("world-wisps");
                Assert.That(GameManager.Instance.PauseGame(),Is.True,GameManager.Instance.PauseReason.ToString()); yield return null;
                var points=motion.Mesh.vertices; yield return Wait(.2f);
                Assert.That(motion.Mesh.vertices,Is.EqualTo(points));
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground=background; Time.timeScale=1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds) { float until=Time.realtimeSinceStartup+seconds; while(Time.realtimeSinceStartup<until) yield return null; }
        static IEnumerator Navigation()
        {
            float until=Time.realtimeSinceStartup+20;
            while(LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning,Is.False); yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path=JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(File.Exists(path),Is.True);
            Directory.CreateDirectory("output/qa/animated-accessories");
            File.Copy(path,"output/qa/animated-accessories/"+name+".png",true); File.Delete(path);
        }
    }
}
