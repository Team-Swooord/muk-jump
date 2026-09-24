using System;
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
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class MonthlyLeaderboardPlayTests
    {
        [UnityEngine.TestTools.UnityTest, Timeout(120000)]
        public IEnumerator MonthlyBoardShowsPeriodPolicyAndResetInThreeLanguages()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var preferences = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(preferences); InkSkinPreferences.UseStoreForTests(preferences);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var oldLanguage = GameLocalization.Language;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var account = MukJumpAccountRuntime.Instance;
                Assert.That(account.IsOnlineAuthenticated, Is.False);
                Assert.That(account.UsesMonthlyLeaderboard, Is.True);
                var clock = (MonthlyServerClock)Field(account, "monthlyClock").GetValue(account);
                double now = Time.realtimeSinceStartupAsDouble;
                clock.Set(DateTimeOffset.Parse("2026-09-24T00:00:00Z"), now, now);
                Field(account, "monthlyTimeRetry").SetValue(account, double.MaxValue);
                yield return null;
                var entries = (List<MukJumpLeaderboardEntry>)Field(account, "leaderboardEntries").GetValue(account);
                for (int i = 0; i < 10; i++) entries.Add(new MukJumpLeaderboardEntry(i + 1, 237 - i,
                    new[] { "먹방울친구", "InkJumper", "月の旅人" }[i % 3], regionCode: "KR", equippedSkin: InkSkinId.Gat,
                    timeSeconds: 55 + i));
                typeof(MukJumpAccountRuntime).GetProperty("LeaderboardStatus").SetValue(account, "이번 달 최고 고도 TOP 10");
                var view = UnityEngine.Object.FindAnyObjectByType<LobbyOptionsView>();
                view.OpenLeaderboard(); yield return Wait(.5f);
                var page = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); yield return Wait(.3f);
                    var period = page.Find("MonthlyPeriod").GetComponent<Text>();
                    var notice = page.Find("MonthlyNotice").GetComponent<Text>();
                    Assert.That(period.text, Does.Contain("2026-09"));
                    Assert.That(period.text, Does.Contain(language == GameLanguage.Japanese ? "今月の殿堂" :
                        language == GameLanguage.English ? "Monthly Hall of Fame" : "이번 달 명예의 전당"));
                    Assert.That(notice.text, Does.Contain(GameLocalization.Translate("매월 1일 00시 초기화 (한국시간)")));
                    Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height));
                    Assert.That(page.Find("LeaderboardRow10").GetComponent<Text>().text, Is.EqualTo("228 m"));
                    yield return Capture("monthly-" + language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                now = Time.realtimeSinceStartupAsDouble;
                clock.Set(DateTimeOffset.Parse("2026-09-30T15:30:00Z"), now, now);
                yield return Wait(.3f);
                Assert.That(entries, Is.Empty);
                Assert.That(page.Find("MonthlyNotice").GetComponent<Text>().text, Does.Contain("정산 중"));
                Assert.That(page.Find("MonthlyPeriod").GetComponent<Text>().text, Does.Contain("2026-10"));
                yield return Capture("monthly-reset");
            }
            finally
            {
                GameLocalization.SetLanguage(oldLanguage);
                Application.runInBackground = oldBackground;
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        static IEnumerator Wait(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/monthly-20260924");
            File.Copy(path, "output/qa/monthly-20260924/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
