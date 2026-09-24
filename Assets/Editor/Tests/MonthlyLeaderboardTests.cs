using System;
using System.Collections.Generic;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class MonthlyLeaderboardTests
    {
        const string Owner = "monthly-test-owner";
        static MonthlyRunRecord Run(int height = 100, int seconds = 50, string utc = "2026-09-24T00:00:00Z", string owner = Owner) =>
            new() { owner = owner, runId = "run-" + height + "-" + seconds, completedUtc = utc,
                period = MonthlyLeaderboard.Period(DateTimeOffset.Parse(utc)), height = height, seconds = seconds };
        [TearDown] public void Cleanup() { MonthlyLeaderboard.ClearOwner(Owner); MonthlyLeaderboard.ClearOwner("other"); }

        [TestCase("2026-09-30T14:59:59Z", "2026-09", false)]
        [TestCase("2026-09-30T15:00:00Z", "2026-10", true)]
        [TestCase("2026-09-30T15:59:59Z", "2026-10", true)]
        [TestCase("2026-09-30T16:00:00Z", "2026-10", false)]
        [TestCase("2026-12-31T15:00:00Z", "2027-01", true)]
        [TestCase("2028-02-29T15:00:00Z", "2028-03", true)]
        public void UsesKoreanCalendar(string utc, string month, bool counting)
        {
            var time = DateTimeOffset.Parse(utc);
            Assert.That(MonthlyLeaderboard.Period(time), Is.EqualTo(month));
            Assert.That(MonthlyLeaderboard.IsCounting(time), Is.EqualTo(counting));
        }
        [Test] public void ServerClockIgnoresDeviceCalendarAndRejectsStaleOrAmbiguousSamples()
        {
            var clock = new MonthlyServerClock();
            Assert.That(clock.TryNow(10, out _), Is.False);
            clock.Set(DateTimeOffset.Parse("2026-09-30T14:59:59Z"), 10, 12);
            Assert.That(clock.TryNow(12, out _), Is.False);
            Assert.That(clock.TryNow(15, out var utc), Is.True);
            Assert.That(MonthlyLeaderboard.Period(utc), Is.EqualTo("2026-10"));
            Assert.That(clock.TryNow(4000, out _), Is.False);
            Assert.That(clock.TryNow(0, out _), Is.False);
        }
        [Test] public void LocalRetryKeepsWholePairAcrossReloadAndAccounts()
        {
            Assert.That(MonthlyLeaderboard.StoreCompleted(Run(1000, 300)), Is.True);
            Assert.That(MonthlyLeaderboard.StoreCompleted(Run(100, 50, "2026-10-02T00:00:00Z")), Is.True);
            MonthlyLeaderboard.StoreCompleted(Run(9999, 1));
            Assert.That(MonthlyLeaderboard.ReadLocal(Owner).Record.Height, Is.EqualTo(100));
            MonthlyLeaderboard.StoreCompleted(Run(100, 30, "2026-10-02T00:00:00Z"));
            MonthlyLeaderboard.StoreCompleted(Run(101, 90, "2026-10-02T00:00:00Z"));
            Assert.That(MonthlyLeaderboard.ReadLocal(Owner).Record.Seconds, Is.EqualTo(90));
            Assert.That(MonthlyLeaderboard.ReadLocal("other"), Is.Null);
        }
        [Test] public void UnknownAndLegacyCompletionCannotCreateMonthlyRecord()
        {
            Assert.That(MonthlyLeaderboard.Decode("{\"bestHeight\":1000}"), Is.Null);
            Assert.That(MonthlyLeaderboard.StoreCompleted(null), Is.True);
            Assert.That(MonthlyLeaderboard.ReadLocal(Owner), Is.Null);
            var record = Run(); record.period = "2026-10";
            Assert.That(record.IsValid, Is.False);
        }
        [Test] public void OldMonthAppearanceIsNeverShownInCurrentMonth()
        {
            Assert.That(MonthlyLeaderboard.TryAppearance("2026-09|KR|1|7", "2026-10", out _), Is.False);
            Assert.That(MonthlyLeaderboard.TryAppearance("2026-10|KR|1|7", "2026-10", out var value), Is.True);
            Assert.That(value, Is.EqualTo("KR|1|7"));
        }
        [Test] public void PublishParamDoesNotOverwriteLifetimeOrMonthlySource()
        {
            var data = MukJumpAccountRuntime.CreateMonthlyLeaderboardParam(Run(), "2026-09", "KR|1|0");
            Assert.That(data.GetValue().ContainsKey("bestHeight"), Is.False);
            Assert.That(data.GetValue().ContainsKey("growthJson"), Is.False);
            Assert.That(data.GetValue().ContainsKey("revision"), Is.False);
            Assert.That(data.GetValue().ContainsKey(MonthlyLeaderboard.SourceColumn), Is.False);
            Assert.That(data.GetValue().ContainsKey(MonthlyLeaderboard.ScoreColumn), Is.True);
        }

        sealed class FakeServer
        {
            public MonthlyRunRecord Source;
            public int Revision, Publishes, Writes;
            public readonly List<long> Scores = new();
            public bool Active = true, FailPublish;
            public Action OnWrite, OnPublish;
            public bool? Result;
            public void Sync(MonthlyRunRecord local, string month = "2026-09") => MonthlyLeaderboardSync.Run(Owner, month, local, () => Active,
                done => done(new MonthlyLeaderboardSync.State { Record = Source, UpdatedAt = Revision.ToString() }),
                (state, best, done) => {
                    Writes++; OnWrite?.Invoke();
                    bool match = state.UpdatedAt == Revision.ToString();
                    if (match) { Source = best; Revision++; }
                    done(match);
                },
                (record, done) => { Publishes++; Scores.Add(record.Record.RankScore); OnPublish?.Invoke(); done(!FailPublish); },
                ok => Result = ok);
        }
        [Test] public void LoginWithoutCurrentMonthRunDoesNotPublish()
        {
            var server = new FakeServer { Source = Run(1000, 300, "2026-08-30T00:00:00Z") };
            server.Sync(null);
            Assert.That(server.Result, Is.True); Assert.That(server.Publishes, Is.Zero); Assert.That(server.Writes, Is.Zero);
        }
        [Test] public void LowerNewMonthRecordReplacesOnlyMonthlySource()
        {
            var server = new FakeServer { Source = Run(1000, 300, "2026-08-30T00:00:00Z") };
            server.Sync(Run());
            Assert.That(server.Result, Is.True); Assert.That(server.Source.height, Is.EqualTo(100));
        }
        [Test] public void ConcurrentHigherRecordWinsConditionalWrite()
        {
            var server = new FakeServer();
            server.OnWrite = () => { server.Source = Run(200, 150); server.Revision++; server.OnWrite = null; };
            server.Sync(Run());
            Assert.That(server.Result, Is.True); Assert.That(server.Source.height, Is.EqualTo(200));
            Assert.That(server.Scores[0], Is.EqualTo(server.Source.Record.RankScore));
        }
        [Test] public void ConcurrentFasterTieRepairsStalePublish()
        {
            var server = new FakeServer { Source = Run(100, 80) };
            server.OnPublish = () => { server.Source = Run(100, 30); server.Revision++; server.OnPublish = null; };
            server.Sync(Run(100, 60));
            Assert.That(server.Result, Is.True); Assert.That(server.Scores.Count, Is.EqualTo(2));
            Assert.That(server.Scores[1], Is.EqualTo(Run(100, 30).Record.RankScore));
        }
        [Test] public void FailedPublishPreservesSourceForRetry()
        {
            var server = new FakeServer { FailPublish = true }; server.Sync(Run());
            Assert.That(server.Result, Is.False); Assert.That(server.Source.height, Is.EqualTo(100));
            server.FailPublish = false; server.Sync(null);
            Assert.That(server.Result, Is.True); Assert.That(server.Writes, Is.EqualTo(1));
        }
        [Test] public void AccountOrMonthChangeRejectsLateReadAndWrite()
        {
            var server = new FakeServer(); server.OnWrite = () => server.Active = false; server.Sync(Run());
            Assert.That(server.Result, Is.False); Assert.That(server.Publishes, Is.Zero);
        }
        [Test] public void LastMonthPendingNeverEntersNewMonth()
        {
            var server = new FakeServer(); server.Sync(Run(), "2026-10");
            Assert.That(server.Publishes, Is.Zero); Assert.That(server.Writes, Is.Zero);
        }
        [Test] public void FutureServerMonthCannotBeOverwrittenByStaleClient()
        {
            var server = new FakeServer { Source = Run(100, 80, "2026-10-02T00:00:00Z") }; server.Sync(Run(9999, 1));
            Assert.That(server.Result, Is.False); Assert.That(server.Writes, Is.Zero);
        }
        [Test] public void RecoveredSettlementKeepsFrozenMonthAndTime()
        {
            var ticket = Run(); ticket.runId = Guid.NewGuid().ToString("N");
            var snapshot = new PendingGameOverSettlementSnapshot { runId = ticket.runId,
                scoreHeight = 100, swarmProgressHeight = 100, eligible = true, monthlyRecord = ticket };
            var pending = new MemoryPendingGameOverSettlementStore();
            string json = JsonUtility.ToJson(snapshot); pending.Save(json);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 1000, Seconds = 300 });
            GameManager.UsePendingGameOverSettlementStoreForTests(pending);
            try
            {
                Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
                Assert.That(MonthlyLeaderboard.ReadLocal(Owner).period, Is.EqualTo("2026-09"));
                Assert.That(MonthlyLeaderboard.ReadLocal(Owner).seconds, Is.EqualTo(50));
                Assert.That(ScoreManager.StoredRecord.Height, Is.EqualTo(1000));
                pending.Save(json);
                Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
                Assert.That(PermanentGrowthProfile.CompletedRunHistoryCount, Is.EqualTo(1));
                Assert.That(pending.Json, Is.Empty);
            }
            finally
            {
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            }
        }
    }
}
