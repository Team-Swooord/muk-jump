using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class TimedLeaderboardPlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ClockAndLeaderboardDisplayWithNoNetworkWrites()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var oldLanguage = GameLocalization.Language;
            try
            {
                Assert.That(MukJumpAccountRuntime.ShouldEnableBackendForRuntime(RuntimePlatform.OSXEditor, true, true), Is.False);
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var account = MukJumpAccountRuntime.Instance;
                Assert.That(account.IsOnlineAuthenticated, Is.False);
                var entries = (List<MukJumpLeaderboardEntry>)typeof(MukJumpAccountRuntime)
                    .GetField("leaderboardEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(account);
                entries.Clear();
                for (int i = 0; i < 10; i++) entries.Add(new MukJumpLeaderboardEntry(i + 1, i < 3 ? 237 : 200 - i,
                    new[] { "먹방울친구", "InkJumper", "月の旅人" }[i % 3],
                    regionCode: new[] { "KR", "US", "JP" }[i % 3], equippedSkin: InkSkinId.Gat,
                    timeSeconds: i == 2 ? -1 : 55 + i * 30));
                var view = Object.FindAnyObjectByType<LobbyOptionsView>();
                view.OpenLeaderboard(); yield return Wait(.5f);
                var page = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
                for (int i = 1; i <= 10; i++)
                {
                    var height = page.Find("LeaderboardRow" + i).GetComponent<Text>();
                    var time = page.Find("LeaderboardTime" + i).GetComponent<Text>();
                    Assert.That(time.text, Is.EqualTo(BestRunRecord.FormatTime(entries[i - 1].TimeSeconds)));
                    var name = (RectTransform)page.Find("NameCell" + i);
                    Assert.That(name.anchoredPosition.x + name.rect.xMax,
                        Is.LessThan(time.rectTransform.anchoredPosition.x + time.rectTransform.rect.xMin));
                    Assert.That(time.rectTransform.anchoredPosition.x + time.rectTransform.rect.xMax,
                        Is.LessThan(height.rectTransform.anchoredPosition.x + height.rectTransform.rect.xMin));
                    Assert.That(time.rectTransform.anchoredPosition.y, Is.EqualTo(height.rectTransform.anchoredPosition.y));
                }
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); yield return Wait(.3f);
                    Assert.That(page.Find("TimeHeading").GetComponent<Text>().text,
                        Is.EqualTo(language == GameLanguage.Korean ? "시간" : language == GameLanguage.English ? "Time" : "時間"));
                    Assert.That(page.Find("HeightHeading").GetComponent<Text>().text,
                        Is.EqualTo(language == GameLanguage.Korean ? "고도" : language == GameLanguage.English ? "Height" : "高度"));
                    yield return Capture("leaderboard-" + language);
                }
                view.Close(); yield return Wait(.5f);
                var game = GameManager.Instance;
                float readyDeadline = Time.realtimeSinceStartup + 8;
                while ((game.IsTransitioning || StartupBrandSplash.IsBlockingInput ||
                    !LobbyScreenNavigator.Instance.CanStartGame) && Time.realtimeSinceStartup < readyDeadline) yield return null;
                game.StartGameFromMenu();
                readyDeadline = Time.realtimeSinceStartup + 8;
                while (!game.IsGameplayTicking && Time.realtimeSinceStartup < readyDeadline) yield return null;
                Assert.That(game.IsGameplayTicking, Is.True,
                    $"state={game.State}, transition={game.IsTransitioning}, pause={game.PauseReason}");
                game.ToggleDebugInvincible(); // 실제 계정 기록을 만들지 않는다.
                float before = game.ActiveGameplaySeconds;
                yield return Wait(.3f);
                Assert.That(game.ActiveGameplaySeconds, Is.GreaterThan(before + .15f));
                Assert.That(game.PauseGame(), Is.True);
                float paused = game.ActiveGameplaySeconds; yield return Wait(.3f);
                Assert.That(game.ActiveGameplaySeconds, Is.EqualTo(paused));
                Assert.That(game.ResumeGame(), Is.True); yield return Wait(.2f);
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                float background = game.ActiveGameplaySeconds; yield return Wait(.3f);
                Assert.That(game.ActiveGameplaySeconds, Is.EqualTo(background));
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.3f);
                Assert.That(game.ActiveGameplaySeconds, Is.GreaterThan(background));
                var debugControls = Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                if (debugControls != null) debugControls.gameObject.SetActive(false);
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); yield return Wait(.2f);
                    var top = (RectTransform)GameplayHudView.Instance.transform.Find("TopHudRoot");
                    var paper = (RectTransform)top.Find("HudHanjiCard");
                    foreach (string field in new[] { "ElapsedTime", "ElapsedCaption" })
                    {
                        var corners = new Vector3[4];
                        ((RectTransform)top.Find(field)).GetWorldCorners(corners);
                        foreach (var corner in corners)
                            Assert.That(paper.rect.Contains(paper.InverseTransformPoint(corner)), Is.True,
                                "실제 플레이 중 시간 표시가 한지 칸 밖으로 내려가면 안 됩니다: " + field);
                    }
                    yield return Capture("hud-" + language);
                }
                Assert.That(account.IsOnlineAuthenticated, Is.False);
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                GameLocalization.SetLanguage(oldLanguage); Application.runInBackground = oldBackground;
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/timed-leaderboard-20260914");
            File.Copy(path, "output/qa/timed-leaderboard-20260914/" + name + ".png", true);
            File.Delete(path);
        }
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
