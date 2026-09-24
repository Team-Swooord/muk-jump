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
        [UnityTest, Timeout(180000), Category("SkinUnlockFlow")]
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
                for(int i=0;i<InkSkinAchievements.Goal(InkSkinId.CatEars);i++) manager.NotifySkinItemCollected(true);
                yield return Wait(.3f);
                var toast=manager.GetComponent<InkAchievementToast>();
                var toastCanvas=toast.transform.Find("AchievementToastCanvas");
                Assert.That(toastCanvas,Is.Not.Null,"현재 먹방울 해금 목표를 달성하면 알림을 만든다.");
                Assert.That(toastCanvas.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                foreach(var graphic in toastCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.That(graphic.raycastTarget,Is.False,graphic.name);
                yield return Capture("achievement-playing");
                EndRun(manager,InkSkinAchievements.Goal(InkSkinId.Gat));
                yield return Wait(2);
                Assert.That(manager.GetComponent<InkSkinUnlockView>(),Is.Null,"해금 공개는 Main의 팝업이 아니다.");
                LobbyButton(manager).onClick.Invoke();
                yield return WaitForUnlock();
                var view=Object.FindFirstObjectByType<InkSkinUnlockView>();
                Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("SkinUnlock"));
                Assert.That(GameManager.Instance==null,Is.True,"게임 월드는 공개 씬에 남아 있지 않는다.");
                Assert.That(view.Paper.GetComponent<HanjiScrollFrame>(),Is.Null,"성장처럼 전면 화면을 사용한다.");
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
                yield return WaitForMainLobby();
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat),Is.True);
                Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
                // 같은 기록으로 또 끝나도 기존 해금 결과는 반복하지 않는다.
                yield return BeginRun(); manager=GameManager.Instance; EndRun(manager,InkSkinAchievements.Goal(InkSkinId.Gat));
                yield return Wait(2); LobbyButton(manager).onClick.Invoke(); yield return WaitForMainLobby();
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(Object.FindFirstObjectByType<InkSkinUnlockView>(),Is.Null);
                // 로비에서 새로 발견한 비밀 의상도 다음 확정 종료에서 공개한다.
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat),Is.True);
                yield return BeginRun(); manager=GameManager.Instance; EndRun(manager,100);
                yield return Wait(2); LobbyButton(manager).onClick.Invoke();
                yield return WaitForUnlock(); view=Object.FindFirstObjectByType<InkSkinUnlockView>();
                Assert.That(view.IsOpen,Is.True);
                for(int i=0; i<view.Count && view.Current!=InkSkinId.NightBat; i++)
                { yield return Wait(1.4f); view.Advance(); }
                Assert.That(view.Current,Is.EqualTo(InkSkinId.NightBat));
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.06f);
                Assert.That(view.IsRevealed,Is.True);
                Assert.That(view.GetComponentInChildren<InkShowroomPresentation>().IsPlaying,Is.False);
                GameLocalization.SetLanguage(GameLanguage.Korean); yield return Capture("unlock-secret");
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                Assert.That(view.IsRevealed,Is.True);
                Assert.That(view.GetComponentInChildren<InkShowroomPresentation>().ParticleCount,Is.Zero);
                // 이전 계정의 완료 콜백을 실행하지 않고 빈 결과에 갇히지 않게 화면만 로비로 돌아간다.
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
                view.AcknowledgeAll(); yield return WaitForMainLobby();
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat),Is.False);
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
        [UnityTest, Timeout(120000), Category("SkinUnlockFlow")]
        public IEnumerator LobbyDiscoveryRevealsOnWardrobeEntryThenReturnsThereExactlyOnce()
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
            MobileApplicationLifecycle.SetPlatformVisibility(true); SkinDebugSession.Reset();
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.True);
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat), Is.True);
                var toast = GameManager.Instance.GetComponent<InkAchievementToast>();
                toast.EnqueueLobby((int)InkSkinId.NightBat); toast.EnqueueLobby((int)InkSkinId.NightBat);
                yield return Wait(.4f);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
                Assert.That(Object.FindFirstObjectByType<InkSkinUnlockView>(), Is.Null);
                yield return Capture("lobby-unlock-notice");
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                yield return WaitForUnlock();
                var view = Object.FindFirstObjectByType<InkSkinUnlockView>();
                Assert.That(view.Count, Is.EqualTo(1)); Assert.That(view.Current, Is.EqualTo(InkSkinId.NightBat));
                yield return Wait(.4f); yield return Capture("lobby-unlock-reveal");
                view.Paper.Find("Equip").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.NightBat));
                Assert.That(view.Paper.Find("Equip").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                yield return Capture("lobby-unlock-equipped");
                yield return Wait(.2f);
                view.AcknowledgeAll();
                float deadline = Time.realtimeSinceStartup + 15;
                while ((LobbyScreenNavigator.Instance == null || LobbyScreenNavigator.Instance.CurrentSection != LobbyScreenNavigator.LobbySection.Wardrobe ||
                    LobbyScreenNavigator.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
                Assert.That(LobbyScreenNavigator.Instance.CurrentSection, Is.EqualTo(LobbyScreenNavigator.LobbySection.Wardrobe));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat), Is.True);
                // 별도 해금 씬을 떠나도 실제 저장소를 다시 읽어 같은 의상을 복원한다.
                InkSkinPreferences.UseStoreForTests(settings);
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.NightBat));
                Assert.That(SkinUnlockSceneController.ConsumeWardrobeReturn(), Is.Null);
                yield return Capture("lobby-unlock-return-wardrobe");
                Assert.That(LobbyScreenNavigator.Instance.ReturnToLobby(), Is.True); yield return WaitForNavigation();
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return WaitForNavigation();
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
                Assert.That(Object.FindFirstObjectByType<InkSkinUnlockView>(), Is.Null);
                Assert.That(LobbyScreenNavigator.Instance.ReturnToLobby(), Is.True); yield return WaitForNavigation();
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.Astronaut), Is.True);
                GameManager.Instance.GetComponent<InkAchievementToast>().EnqueueLobby((int)InkSkinId.Astronaut);
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return WaitForNavigation();
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Main"));
                Assert.That(Object.FindFirstObjectByType<InkSkinUnlockView>(), Is.Null, "이전 계정 보상을 새 계정에 표시하지 않는다.");
            }
            finally
            {
                Application.runInBackground = background; SkinDebugSession.Reset();
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
        static IEnumerator WaitForNavigation()
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while ((LobbyScreenNavigator.Instance.IsTransitioning || GameManager.Instance.GetComponent<BrushTransitionView>().IsPlaying) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
            yield return null;
        }
        static IEnumerator WaitForMainLobby()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while ((GameManager.Instance == null || GameManager.Instance.State != GameState.Lobby || LobbyScreenNavigator.Instance == null ||
                LobbyScreenNavigator.Instance.IsTransitioning || GameManager.Instance.GetComponent<BrushTransitionView>().IsPlaying) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameManager.Instance, Is.Not.Null);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
        }
        static IEnumerator WaitForUnlock()
        {
            float deadline=Time.realtimeSinceStartup+9;
            while(Time.realtimeSinceStartup<deadline)
            {
                var view=Object.FindFirstObjectByType<InkSkinUnlockView>();
                if(view!=null && view.IsOpen) yield break;
                yield return null;
            }
            Assert.Fail("해금 씬으로 이동하지 못했습니다.");
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
