using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class SkinDebugTests
    {
        MemoryPermanentGrowthStore growth;
        MemoryLobbySettingsStore settings;
        [SetUp] public void Setup()
        {
            growth = new MemoryPermanentGrowthStore(); settings = new MemoryLobbySettingsStore();
            PermanentGrowthProfile.UseStoreForTests(growth); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings); ScoreManager.UseStoreForTests(new MemoryScoreStore());
            SkinDebugSession.Reset();
        }
        [TearDown] public void Cleanup()
        {
            SkinDebugSession.Reset(); InkSkinPreferences.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests(); PointerInput.ResetSuppressionForTests();
        }
        [Test] public void AllOutfitsAreTemporaryAndCannotChangeCloudOrGameCenterProgress()
        {
            string before = JsonUtility.ToJson(PermanentGrowthProfile.AchievementSyncSnapshot());
            string saved = growth.Json;
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            for (int i = 1; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                Assert.That(SkinDebugSession.CanEquip(id), Is.True);
                Assert.That(SkinDebugSession.IsConcealed(id), Is.False);
                Assert.That(InkSkinPreferences.TryEquip(id), Is.True);
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(id));
                Assert.That(InkSkinAchievements.IsUnlocked(id), Is.False);
            }
            SkinSecrets.AcceptedNightChange(true);
            foreach (var id in new[]{InkSkinId.Ink, InkSkinId.Gat, InkSkinId.Ink, InkSkinId.SpiderMask, InkSkinId.Ink}) SkinSecrets.SelectedByUser(id);
            Assert.That(growth.Json, Is.EqualTo(saved));
            Assert.That(JsonUtility.ToJson(PermanentGrowthProfile.AchievementSyncSnapshot()), Is.EqualTo(before));
            Assert.That(settings.GetInt(InkSkinPreferences.StorageKey, 0), Is.Zero);
            SkinDebugSession.SetAllUnlocked(false);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(SkinDebugSession.CanEquip(InkSkinId.SpiderMask), Is.False);
            Assert.That(SkinDebugSession.IsConcealed(InkSkinId.NightBat), Is.True);
        }
        [Test] public void OwnerReplacementAndRuntimeResetDiscardOverride()
        {
            SkinDebugSession.SetAllUnlocked(true); InkSkinPreferences.TryEquip(InkSkinId.NightBat);
            SkinDebugSession.SetMenuOpen(true);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Assert.That(SkinDebugSession.Active || SkinDebugSession.MenuOpen, Is.False);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            SkinDebugSession.SetAllUnlocked(true); SkinDebugSession.Reset();
            Assert.That(SkinDebugSession.Active, Is.False);
        }
        [Test] public void PreviewDoesNotGrantAndRealRevealStillRequiresOwnership()
        {
            var host = new GameObject("DebugPreviewTest");
            try
            {
                var view = host.AddComponent<InkSkinUnlockView>();
                ulong mask = 1UL << (int)InkSkinId.NightBat;
                var owner = PermanentGrowthProfile.SkinAccountToken;
                Assert.That(view.Show(mask, owner, null), Is.False);
                Assert.That(view.ShowPreview(mask, owner, null), Is.True);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat), Is.False);
                view.Cancel(); Assert.That(view.Show(mask, owner, null), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test] public void DedicatedInternalBuildRequiresOneTimeDebugAndForbidsReset()
        {
            string[] debug = { SkinDebugSession.BuildDefine };
            foreach (MukJumpNativeBuildIntent intent in Enum.GetValues(typeof(MukJumpNativeBuildIntent)))
            {
                if (intent == MukJumpNativeBuildIntent.IosSkinDebug) continue;
                Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidateSkinDebugScope(intent, Array.Empty<string>(), debug));
            }
            Assert.DoesNotThrow(() => MukJumpNativeReleaseBuildGuard.ValidateSkinDebugScope(MukJumpNativeBuildIntent.IosSkinDebug, Array.Empty<string>(), debug));
            Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidateSkinDebugScope(MukJumpNativeBuildIntent.IosSkinDebug, debug, debug));
            Assert.Throws<BuildFailedException>(() => MukJumpNativeReleaseBuildGuard.ValidatePrereleaseResetScope(MukJumpNativeBuildIntent.IosSkinDebug, Array.Empty<string>(), new[]{PrereleasePlayerReset.BuildDefine}));
            Assert.That(MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(UnityEditor.BuildTarget.iOS, UnityEditor.BuildOptions.None,
                MukJumpNativeBuildIntent.IosSkinDebug, false, false, true, MukJumpStoreBuild.DefaultBundleIdentifier), Is.Empty);
        }
        [Test] public void ReleaseIncludesUnlockSceneButNeverArbitraryDebugScenes()
        {
            var scenes = new[]{MukJumpSplashSceneBuilder.ScenePath, MukJumpSplashSceneBuilder.MainScenePath, MukJumpSkinUnlockSceneBuilder.ScenePath};
            Assert.DoesNotThrow(() => MukJumpStoreBuild.ValidateReleaseScenes(scenes));
            Assert.Throws<BuildFailedException>(() => MukJumpStoreBuild.ValidateReleaseScenes(new[]{scenes[0], scenes[1]}));
            Assert.Throws<BuildFailedException>(() => MukJumpStoreBuild.ValidateReleaseScenes(new[]{scenes[0], scenes[1], "Assets/Scenes/Debug.unity"}));
            Assert.Throws<BuildFailedException>(() => MukJumpStoreBuild.ValidateReleaseScenes(new[]{scenes[0], scenes[1], scenes[2], "Assets/Scenes/Debug.unity"}));
        }
    }

    public sealed class SkinDebugPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator LobbyPreviewAndTestRunLeaveRealProgressUntouched()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            var settings = new MemoryLobbySettingsStore(); var growth = new MemoryPermanentGrowthStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(growth); ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted(); MobileApplicationLifecycle.SetPlatformVisibility(true);
            SkinDebugSession.Reset(); bool background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return WaitForLobby();
                var debug = Object.FindFirstObjectByType<InkSkinDebugView>(); Assert.That(debug, Is.Not.Null);
                debug.Toggle(); yield return null; Assert.That(debug.IsOpen, Is.True);
                Assert.That(LobbyScreenNavigator.Instance.CanStartGame, Is.False);
                foreach (var language in new[]{GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); yield return null; Canvas.ForceUpdateCanvases();
                    var title = debug.transform.Find("SkinDebugCanvas/SafeAreaRoot/TestPanel/Title").GetComponent<UnityEngine.UI.Text>();
                    Assert.That(title.text, Is.EqualTo(language == GameLanguage.Korean ? "의상 시험실" : language == GameLanguage.English ? "Outfit test room" : "衣装テスト"));
                    var close = debug.transform.Find("SkinDebugCanvas/SafeAreaRoot/TestPanel/Close/Label").GetComponent<UnityEngine.UI.Text>();
                    Assert.That(close.text, Is.EqualTo(language == GameLanguage.Korean ? "닫기" : language == GameLanguage.English ? "Close" : "閉じる"));
                    yield return Capture("skin-debug-menu-" + language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                string saved = growth.Json;
                debug.Preview(false); yield return Wait(4);
                var view = Object.FindFirstObjectByType<InkSkinUnlockView>();
                Assert.That(view != null && view.IsOpen, Is.True);
                yield return Capture("skin-debug-unlock");
                Assert.That(view.Current, Is.EqualTo(InkSkinId.Gat));
                view.AcknowledgeAll(); yield return WaitForLobby();
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(growth.Json, Is.EqualTo(saved));
                debug = Object.FindFirstObjectByType<InkSkinDebugView>(); debug.Toggle(); debug.Preview(true); yield return Wait(4);
                view = Object.FindFirstObjectByType<InkSkinUnlockView>();
                Assert.That(view.Current, Is.EqualTo(InkSkinId.NightBat));
                yield return Capture("skin-debug-secret"); view.AcknowledgeAll(); yield return WaitForLobby();
                debug = Object.FindFirstObjectByType<InkSkinDebugView>(); debug.Toggle(); debug.ToggleAll(); debug.Close();
                Assert.That(SkinDebugSession.Active, Is.True);
                Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.True);
                GameManager.Instance.StartGameFromMenu(); yield return Wait(4);
                var manager = GameManager.Instance;
                Assert.That(manager.IsGameplayTicking, Is.True);
                Assert.That(manager.GetComponent<RunGrowthController>().SkinSnapshot, Is.EqualTo(InkSkinId.SpiderMask));
                Assert.That(ScoreManager.Instance.RecordsAllowed, Is.False);
                Assert.That(debug.transform.Find("SkinDebugCanvas").GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                typeof(ScoreManager).GetProperty("Height").SetValue(ScoreManager.Instance, 9999);
                Call(manager, "CommitCurrentBestBeforeGameOver"); Call(manager, "EnterGameOver"); yield return Wait(2);
                manager.transform.Find("GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent/RetryBrush").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return WaitForLobby();
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(growth.Json, Is.EqualTo(saved)); Assert.That(ScoreManager.Instance.Best, Is.Zero);
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().unlockedMask, Is.Zero);
                SkinDebugSession.SetAllUnlocked(false); GameManager.Instance.StartGameFromMenu(); yield return Wait(4);
                Assert.That(ScoreManager.Instance.RecordsAllowed, Is.True, "일반 판의 기록은 다시 정상 허용한다.");
            }
            finally
            {
                SkinDebugSession.Reset(); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static object Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static IEnumerator WaitForLobby()
        {
            yield return null;
            float end = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < end)
            {
                var debug = Object.FindFirstObjectByType<InkSkinDebugView>();
                if (debug != null && debug.CanOpen) yield break;
                yield return null;
            }
            Assert.Fail("전환이 끝난 로비로 돌아오지 못했습니다.");
        }
        static IEnumerator Wait(float duration) { float end = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < end) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float end = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            string destination = Path.GetFullPath("output/qa/ink-showroom/" + name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination, true); File.Delete(path);
        }
    }
}
