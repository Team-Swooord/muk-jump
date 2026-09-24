using System;
using System.Reflection;
using BackEnd;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed partial class MukJumpAccountTests
    {
        const string MonthlyOwner = "monthly-runtime-fixture";
        const string September = "2026-09-24T00:00:00Z";
        MonthlyRunRecord monthlySource;
        int monthlyReads, monthlyWrites, monthlyPublishes, monthlyPublishStatus;
        Param publishedMonthly;

        MukJumpAccountRuntime MonthlyRuntime(string utc = September)
        {
            MonthlyLeaderboard.ClearOwner(MonthlyOwner);
            deletionStringPrefs["MukJump.Monthly.Record." + MonthlyOwner] = (false, "");
            monthlySource = null; monthlyReads = monthlyWrites = monthlyPublishes = 0;
            monthlyPublishStatus = 204;
            var account = CreateReadySaveRuntime(true);
            SetPrivateField(account, "dirty", false);
            SetPrivateField(account, "currentAccountScopeForTests", new Func<string>(() => MonthlyOwner));
            SetBackendRequestHook(account, "monthlyTimeForTests", done => done(BackendResult(200,
                new JObject { ["utcTime"] = utc }.ToString())));
            SetBackendRequestHook(account, "getMyDataForTests", done => { monthlyReads++; done(MonthlyRow()); });
            SetBackendRequestHook(account, "updateGameDataForTests", done =>
            { monthlyWrites++; monthlySource = MonthlyLeaderboard.ReadLocal(MonthlyOwner); done(BackendResult(204)); });
            SetPrivateField(account, "monthlyPublishForTests", new Action<Param, Action<BackendReturnObject>>((data, done) =>
            { monthlyPublishes++; publishedMonthly = data; done(BackendResult(monthlyPublishStatus)); }));
            SetPrivateField(account, "getLeaderboardForTests",
                new Action<Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    done => done(LeaderboardResult("{\"rows\":[],\"totalCount\":0}"))));
            SetMonthlyClock(account, utc);
            return account;
        }
        BackendReturnObject MonthlyRow() => BackendResult(200, new JObject {
            ["rows"] = new JArray(new JObject {
                ["inDate"] = new JObject { ["S"] = "row-1" },
                ["owner_inDate"] = new JObject { ["S"] = MonthlyOwner },
                ["updatedAt"] = new JObject { ["S"] = "revision-" + monthlyWrites },
                [MonthlyLeaderboard.SourceColumn] = new JObject { ["S"] = MonthlyLeaderboard.Encode(monthlySource) }
            }) }.ToString());
        static void SetMonthlyClock(MukJumpAccountRuntime account, string utc, double age = 0)
        {
            var clock = (MonthlyServerClock)typeof(MukJumpAccountRuntime).GetField("monthlyClock",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(account);
            double now = Time.realtimeSinceStartupAsDouble - age;
            clock.Set(DateTimeOffset.Parse(utc), now, now);
            InvokeLifecycle(account, "UpdateMonthlyPeriod");
        }
        static void CompleteMonthly(MukJumpAccountRuntime account, int height = 100, int seconds = 50)
        {
            var ticket = account.CaptureMonthlyRun(MonthlyOwner, "monthly-fixture-run", new BestRunRecord(height, seconds));
            Assert.That(ticket, Is.Not.Null);
            Assert.That(MonthlyLeaderboard.StoreCompleted(ticket), Is.True);
        }

        [Test] public void MonthlyLoginAndLegacyBestDoNotInventParticipation()
        {
            var account = MonthlyRuntime();
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 1000, Seconds = 100 });
            PlayerPrefs.SetString("MukJump.Cloud.PendingLeaderboardOwner", MonthlyOwner);
            PlayerPrefs.SetInt("MukJump.Cloud.PendingLeaderboardBest", 1000);
            account.SubmitBestHeight(1000);
            Assert.That(monthlyWrites, Is.Zero); Assert.That(monthlyPublishes, Is.Zero);
            Assert.That(MonthlyLeaderboard.ReadLocal(MonthlyOwner), Is.Null);
            CompleteMonthly(account);
            SetPrivateField(account, "monthlySyncRetry", 0d);
            account.SubmitBestHeight(1000);
            Assert.That(monthlyPublishes, Is.EqualTo(1));
            Assert.That(publishedMonthly.GetValue()[MonthlyLeaderboard.ScoreColumn], Is.EqualTo(new BestRunRecord(100, 50).RankScore));
            Assert.That(ScoreManager.StoredRecord.Height, Is.EqualTo(1000));
        }
        [Test] public void Monthly428BackoffSurvivesSaveAndReadAndRetriesStoredRecord()
        {
            var account = MonthlyRuntime(); CompleteMonthly(account);
            monthlyPublishStatus = 428; account.SubmitBestHeight(1000);
            account.SubmitBestHeight(1000); account.RefreshLeaderboard();
            Assert.That(monthlyPublishes, Is.EqualTo(1));
            Assert.That(account.LeaderboardStatus, Does.Contain("정산 중"));
            Assert.That(MonthlyLeaderboard.ReadLocal(MonthlyOwner).height, Is.EqualTo(100));
            monthlyPublishStatus = 204; SetPrivateField(account, "monthlySyncRetry", 0d);
            InvokeLifecycle(account, "PumpMonthlyLeaderboard");
            Assert.That(monthlyPublishes, Is.EqualTo(2));
            Assert.That(account.LeaderboardStatus, Does.Not.Contain("정산 중"));
        }
        [Test] public void MonthlyResetHourStoresRunAndResumesAtOneWithoutReassigningLastMonth()
        {
            var account = MonthlyRuntime("2026-09-30T14:59:00Z"); CompleteMonthly(account, 1000);
            SetMonthlyClock(account, "2026-09-30T15:30:00Z");
            account.SubmitBestHeight(1000); Assert.That(monthlyReads, Is.Zero);
            Assert.That(account.MonthlyPeriodLabel, Is.EqualTo("2026-10"));
            CompleteMonthly(account, 100, 20);
            SetMonthlyClock(account, "2026-09-30T16:00:00Z");
            account.SubmitBestHeight(1000);
            Assert.That(monthlySource.period, Is.EqualTo("2026-10"));
            Assert.That(monthlySource.height, Is.EqualTo(100));
        }
        [Test] public void MonthlyLongOnlineSessionRefreshesServerClockAndForegroundRetries()
        {
            var account = MonthlyRuntime();
            SetMonthlyClock(account, September, 3601);
            Assert.That(account.CaptureMonthlyRun(MonthlyOwner, "offline", new BestRunRecord(100)), Is.Null);
            int timeRequests = 0;
            SetBackendRequestHook(account, "monthlyTimeForTests", done => { timeRequests++; done(BackendResult(200,
                "{\"utcTime\":\"2026-09-24T02:00:00Z\"}")); });
            InvokeLifecycle(account, "PumpMonthlyLeaderboard");
            Assert.That(account.CaptureMonthlyRun(MonthlyOwner, "online", new BestRunRecord(100)).IsValid, Is.True);
            InvokeLifecycle(account, "PumpMonthlyLeaderboard"); Assert.That(timeRequests, Is.EqualTo(1));
            InvokeLifecycle(account, "OnApplicationPause", false);
            InvokeLifecycle(account, "PumpMonthlyLeaderboard"); Assert.That(timeRequests, Is.EqualTo(2));
        }
        [Test] public void MonthlyClockFailureCannotAbortOtherRuntimeWork()
        {
            var account = MonthlyRuntime();
            SetBackendRequestHook(account, "monthlyTimeForTests", _ => throw new InvalidOperationException("fixture"));
            Assert.DoesNotThrow(() => InvokeLifecycle(account, "PumpMonthlyLeaderboard"));
            Assert.That(ReadPrivateBool(account, "monthlyTimeInFlight"), Is.False);
        }
        [Test] public void MonthlyLateReadAfterAccountSwitchCannotWrite()
        {
            var account = MonthlyRuntime(); CompleteMonthly(account);
            Action<BackendReturnObject> late = null;
            SetBackendRequestHook(account, "getMyDataForTests", done => late = done);
            account.SubmitBestHeight(1000);
            SetPrivateField(account, "currentAccountScopeForTests", new Func<string>(() => "another-owner"));
            InvokeLifecycle(account, "BeginAuthenticatedAccountSession"); late(MonthlyRow());
            Assert.That(monthlyWrites, Is.Zero); Assert.That(monthlyPublishes, Is.Zero);
            Assert.That(account.CaptureMonthlyRun(MonthlyOwner, "old-run", new BestRunRecord(100)), Is.Null);
        }
        [Test] public void MonthlyTimeoutReleasesLockAndIgnoresLateResponse()
        {
            var account = MonthlyRuntime(); CompleteMonthly(account);
            Action<BackendReturnObject> late = null;
            SetBackendRequestHook(account, "getMyDataForTests", done => late = done);
            account.SubmitBestHeight(1000); SetPrivateField(account, "leaderboardSaveDeadlineRealtime", -1f);
            LogAssert.Expect(LogType.Warning, "[MukJump] 순위 등록 응답 시간이 초과되어 다시 시도합니다.");
            InvokeLifecycle(account, "ProcessAccountRequestWatchdogs"); late(MonthlyRow());
            Assert.That(ReadPrivateBool(account, "leaderboardSaveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "monthlySyncRequested"), Is.True);
            Assert.That(monthlyWrites, Is.Zero); Assert.That(monthlyPublishes, Is.Zero);
            Assert.That(MonthlyLeaderboard.ReadLocal(MonthlyOwner), Is.Not.Null);
        }
        [Test] public void MonthlyOldReadAndOldAppearanceNeverEnterNewMonthView()
        {
            var account = MonthlyRuntime();
            Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject> late = null;
            SetPrivateField(account, "getLeaderboardForTests", new Action<Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(done => late = done));
            account.RefreshLeaderboard();
            string json = RankedPlayerJson.Replace("\"index\":0", "\"extraData\":\"2026-09|JP|1|36\",\"index\":0");
            SetMonthlyClock(account, "2026-10-02T00:00:00Z"); late(LeaderboardResult(json));
            Assert.That(account.LeaderboardEntries, Is.Empty);
            account.RefreshLeaderboard(); late(LeaderboardResult(json));
            Assert.That(account.LeaderboardEntries, Is.Empty);
            account.RefreshLeaderboard(); late(LeaderboardResult(json.Replace("2026-09|", "2026-10|")));
            Assert.That(account.LeaderboardEntries.Count, Is.EqualTo(1));
        }
    }
}
