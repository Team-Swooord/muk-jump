using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkAchievementNoticeTests
    {
        [Test] public void TwoNoteCueIsShortDeterministicAndClickFree()
        {
            float[] samples = InkAchievementNoticeSound.BuildSamples();
            Assert.That(samples.Length, Is.EqualTo((int)(44100 * .48f)));
            Assert.That(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True);
            Assert.That(samples.Max(x => Math.Abs(x)), Is.EqualTo(.65f).Within(.00001));
            Assert.That(Math.Abs(samples[0]) + Math.Abs(samples[^1]), Is.LessThan(.00001));
            Assert.That(Math.Sqrt(samples.Average(x => (double)x * x)), Is.InRange(.08, .3));
            CollectionAssert.AreEqual(samples, InkAchievementNoticeSound.BuildSamples());
            Directory.CreateDirectory("output/qa/achievement-notice");
            using var file = new BinaryWriter(File.Create("output/qa/achievement-notice/two-note-preview.wav"));
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + samples.Length * 2);
            file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16);
            file.Write((short)1); file.Write((short)1); file.Write(InkAchievementNoticeSound.SampleRate);
            file.Write(InkAchievementNoticeSound.SampleRate * 2); file.Write((short)2); file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(samples.Length * 2);
            foreach (float sample in samples) file.Write((short)Math.Round(sample * InkAchievementNoticeSound.Volume * short.MaxValue));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LiveCompletionShowsBriefNonBlockingInkNoticeAndOneCue()
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
            SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                float deadline = Time.realtimeSinceStartup + 25;
                while ((GameManager.Instance == null || GameManager.Instance.IsTransitioning || StartupBrandSplash.IsBlockingInput)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                yield return Wait(.4f);
                var game = GameManager.Instance;
                game.StartGameFromMenu();
                deadline = Time.realtimeSinceStartup + 10;
                while (!game.IsGameplayTicking && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.IsGameplayTicking && !game.DebugInvincible && ScoreManager.Instance.RecordsAllowed, Is.True);
                // UI는 실제 속도로 검사하되, 입력 없는 시험 중 추락으로 판이 끝나지 않게 물리 시간만 늦춘다.
                Time.timeScale = .04f;
                typeof(GameplayHudView).GetMethod("SetDebugToolsAvailable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Object.FindAnyObjectByType<GameplayHudView>(), new object[] { false });
                GameLocalization.SetLanguage(GameLanguage.Korean);
                var toast = game.GetComponent<InkAchievementToast>();
                game.NotifySkinWallCling(); game.NotifySkinWallCling();
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().gameplayMask & 1, Is.EqualTo(1));
                yield return Wait(.12f);
                var root = toast.transform.Find("AchievementToastCanvas");
                Assert.That(root, Is.Not.Null, "게임 종료 전 실제 달성 이벤트에서 생성");
                var group = root.GetComponent<CanvasGroup>();
                var source = root.GetComponent<AudioSource>(); var clip = source.clip;
                Assert.That(source.isPlaying, Is.True); Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1));
                yield return Wait(.15f);
                var panel = (RectTransform)root.Find("SafeArea/InkBanner");
                var label = panel.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t => t.text == "벽을 타는 먹");
                var paper = panel.Find("Brush").GetComponent<UnityEngine.UI.Image>();
                Assert.That(paper.sprite, Is.SameAs(InkUiStyle.ActionButtonSprite), "기존 흰 한지 버튼 자산 재사용");
                Assert.That(paper.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(paper.color.a, Is.EqualTo(.9f).Within(.001f), "배경만 90% 불투명도");
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); yield return null; Canvas.ForceUpdateCanvases();
                    Assert.That(group.alpha, Is.GreaterThan(.95));
                    Assert.That(game.IsGameplayTicking, Is.True, "업적 알림은 게임을 정지하지 않는다");
                    Assert.That(group.blocksRaycasts || group.interactable, Is.False);
                    Assert.That(root.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
                    Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget), Is.True);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2));
                    Assert.That(label.color, Is.EqualTo(InkPalette.Ink), "글자는 반투명 처리 없이 먹색 유지");
                    Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Outline>().All(o => !o.enabled), Is.True);
                    var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                    Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(MobileUiLayout.CurrentSafeArea.xMin));
                    Assert.That(corners[2].x, Is.LessThanOrEqualTo(MobileUiLayout.CurrentSafeArea.xMax));
                    yield return Capture(language.ToString());
                }
                // 중복 알림은 큐에 쌓이지 않고, 무음·앱 복귀 후 소리를 뒤늦게 반복하지 않는다.
                toast.Enqueue(InkSkinCatalog.Count); toast.Enqueue(InkSkinCatalog.Count + 1);
                float elapsed = GetTime(toast);
                Assert.That(game.PauseGame(), Is.True); yield return Wait(.2f);
                Assert.That(group.alpha, Is.Zero); Assert.That(source.isPlaying, Is.False);
                Assert.That(GetTime(toast), Is.EqualTo(elapsed).Within(.051));
                Assert.That(game.ResumeGame(), Is.True); yield return Wait(.1f);
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1));
                LobbySettingsProfile.SetSfxVolume(0); LobbySettingsProfile.SetReducedMotionEnabled(true);
                SetTime(toast, InkAchievementToast.Duration); yield return Wait(.15f);
                yield return Wait(.3f);
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1)); Assert.That(source.isPlaying, Is.False);
                Assert.That(panel.anchoredPosition.x, Is.EqualTo(-24));
                LobbySettingsProfile.SetSfxVolume(1); yield return null;
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1));
                SetTime(toast, InkAchievementToast.Duration); yield return Wait(.15f);
                Assert.That(group.alpha, Is.Zero, "3초 후 확인 버튼 없이 사라진다: " + label.text + ", t=" + GetTime(toast) +
                    ", queued=" + ((System.Collections.ICollection)typeof(InkAchievementToast).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(toast)).Count);
                Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(1)); Assert.That(source.clip, Is.SameAs(clip));

                toast.Enqueue((int)InkSkinId.Gat); yield return Wait(.1f);
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(2)); Assert.That(source.isPlaying, Is.True);
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                Assert.That(group.alpha, Is.Zero); Assert.That(source.isPlaying, Is.False);
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.1f);
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(2));
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True); yield return Wait(.1f);
                Assert.That(group.alpha, Is.Zero); Assert.That(source.isPlaying, Is.False);
                Object.Destroy(toast);
                // EditMode에서 진입한 Play 검사는 yield 두 번이 실제 두 프레임을 보장하지 않는다.
                // OnDestroy 안에서 예약한 음원 해제까지 완료되는지 제한 시간 내에 확인한다.
                deadline = Time.realtimeSinceStartup + 1;
                while (clip != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(toast == null, Is.True);
                Assert.That(clip == null, Is.True, "생성 음원은 컴포넌트 수명에 맞춰 해제");
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static float GetTime(InkAchievementToast toast) => (float)typeof(InkAchievementToast).GetField("time", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(toast);
        static void SetTime(InkAchievementToast toast, float time) => typeof(InkAchievementToast).GetField("time", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(toast, time);
        static IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/achievement-notice");
            File.Copy(path, "output/qa/achievement-notice/" + name + ".png", true);
        }
    }
}
