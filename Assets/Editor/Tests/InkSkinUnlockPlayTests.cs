using System.Collections;
using System.IO;
using System.Reflection;
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
    public sealed class InkSkinUnlockPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator FinalResultRevealsOnceThenReturnsToLobbyWithSafeMotionAndAccountCancellation()
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
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                yield return BeginRun();
                var manager=GameManager.Instance;
                EndRun(manager,100);
                yield return Wait(2);
                var view=manager.GetComponent<InkSkinUnlockView>();
                Assert.That(view.IsOpen,Is.False,"부활 선택이 남은 결과 화면에서는 아직 공개하지 않는다.");
                LobbyButton(manager).onClick.Invoke();
                float deadline=Time.realtimeSinceStartup+5;
                while(!view.IsOpen && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(view.IsOpen,Is.True); Assert.That(view.Count,Is.GreaterThanOrEqualTo(4));
                Assert.That(view.Current,Is.EqualTo(InkSkinId.Gat));
                yield return Wait(.3f); yield return Capture("unlock-ripple");
                yield return Wait(1.3f);
                int rewardCount=view.Count;
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); yield return Wait(.1f); Canvas.ForceUpdateCanvases();
                    foreach(var label in view.Paper.GetComponentsInChildren<UnityEngine.UI.Text>())
                    {
                        if(string.IsNullOrEmpty(label.text)) continue;
                        Assert.That(label.canvasRenderer.cull,Is.False,label.name);
                        Assert.That(label.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),label.name);
                    }
                    var body=view.Paper.Find("UnlockStage/Entrance/Preview/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(body.enabled && body.sprite!=null,Is.True,"빌더에 원래 먹 프레임이 직렬화되어야 한다.");
                    Assert.That(body.canvasRenderer.cull,Is.False);
                    yield return Capture("unlock-"+language);
                }
                view.Advance();
                var next=view.Current;
                view.Advance(); Assert.That(view.Current,Is.EqualTo(next),"같은 프레임 연타는 항목을 넘기지 않는다.");
                Assert.That(view.Count,Is.EqualTo(rewardCount));
                yield return Wait(.2f);
                view.Advance(); Assert.That(view.IsRevealed,Is.True,"진행 중 확인은 해당 연출만 마무리한다.");
                Assert.That(view.Current,Is.EqualTo(next));
                yield return Wait(.2f);
                view.AcknowledgeAll(); view.AcknowledgeAll();
                yield return Wait(3);
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat),Is.True);
                Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
                // 같은 기록으로 또 끝나도 기존 해금 결과는 반복하지 않는다.
                yield return BeginRun(); manager=GameManager.Instance; EndRun(manager,100);
                yield return Wait(2); LobbyButton(manager).onClick.Invoke(); yield return Wait(3);
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(GameManager.Instance.GetComponent<InkSkinUnlockView>().IsOpen,Is.False);
                // 로비에서 새로 발견한 비밀 의상도 다음 확정 종료에서 공개한다.
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat),Is.True);
                yield return BeginRun(); manager=GameManager.Instance; EndRun(manager,100);
                yield return Wait(2); LobbyButton(manager).onClick.Invoke();
                view=manager.GetComponent<InkSkinUnlockView>(); deadline=Time.realtimeSinceStartup+5;
                while(!view.IsOpen && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(view.IsOpen,Is.True);
                for(int i=0; i<view.Count && view.Current!=InkSkinId.NightBat; i++)
                { yield return Wait(1.4f); view.Advance(); }
                Assert.That(view.Current,Is.EqualTo(InkSkinId.NightBat));
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.06f);
                Assert.That(view.IsRevealed,Is.True);
                Assert.That(view.GetComponentInChildren<InkShowroomPresentation>().IsPlaying,Is.False);
                GameLocalization.SetLanguage(GameLanguage.Korean); yield return Capture("unlock-secret");
                // 이전 계정의 완료 콜백을 실행하지 않고 빈 결과에 갇히지 않게 화면만 로비로 돌아간다.
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
                view.AcknowledgeAll(); yield return Wait(3);
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat),Is.False);
                // 감소된 동작은 첫 프레임부터 이동/입자 없이 읽을 수 있어야 한다.
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat),Is.True);
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                Call(GameManager.Instance,"SetState",GameState.GameOver);
                view=GameManager.Instance.GetComponent<InkSkinUnlockView>();
                Assert.That(view.Show(1UL<<(int)InkSkinId.NightBat,PermanentGrowthProfile.SkinAccountToken,null),Is.True);
                Assert.That(view.IsRevealed,Is.True);
                Assert.That(view.GetComponentInChildren<InkShowroomPresentation>().ParticleCount,Is.Zero);
                view.Cancel();
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator BeginRun()
        {
            float deadline=Time.realtimeSinceStartup+8;
            while((GameManager.Instance.IsTransitioning || StartupBrandSplash.IsBlockingInput ||
                !LobbyScreenNavigator.Instance.CanStartGame) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(PermanentGrowthProfile.RequiresRecovery,Is.False,"복구가 필요한 기록으로 시작할 수 없다.");
            GameManager.Instance.StartGameFromMenu(); deadline=Time.realtimeSinceStartup+8;
            while(!GameManager.Instance.IsGameplayTicking && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(GameManager.Instance.IsGameplayTicking,Is.True,$"state={GameManager.Instance.State}, transition={GameManager.Instance.IsTransitioning}, pause={GameManager.Instance.PauseReason}, nav={LobbyScreenNavigator.Instance.CanStartGame}");
        }
        static void EndRun(GameManager manager,int height)
        {
            typeof(ScoreManager).GetProperty("Height").SetValue(ScoreManager.Instance,height);
            Call(manager,"CommitCurrentBestBeforeGameOver"); Call(manager,"EnterGameOver");
        }
        static UnityEngine.UI.Button LobbyButton(GameManager manager) => manager.transform.Find(
            "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent/RetryBrush").GetComponent<UnityEngine.UI.Button>();
        static object Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static IEnumerator Capture(string name)
        {
            string path=JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(File.Exists(path),Is.True);
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
