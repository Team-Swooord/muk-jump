using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class AppsInTossAnalyticsTests
    {
        readonly List<AppsInTossAnalyticsRuntime.Event> events = new();

        [SetUp]
        public void SetUp()
        {
            events.Clear();
            MukJumpAnalytics.UseSinkForTests(e =>
            {
                var mapped = AppsInTossAnalyticsRuntime.Map(e);
                if (mapped != null) events.Add(mapped);
            });
        }

        [TearDown]
        public void TearDown() => MukJumpAnalytics.ResetForTests();

        int Count(string name) => events.Count(e => Equals(e.Parameters["log_name"], name));
        void Begin() => MukJumpAnalytics.BeginRun("private-run-never-export", true, 2);

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void OnlyReleaseWebPlayerCollects(bool webPlayer, bool development, bool expected)
            => Assert.That(AppsInTossAnalyticsRuntime.ShouldCollect(webPlayer, development), Is.EqualTo(expected));

        [Test]
        public void FinalSettlementIsOneConversionAcrossReviveAndRepeatedCalls()
        {
            Begin(); Begin();
            MukJumpAnalytics.GameOver(100, 30);
            Assert.That(Count("mukjump_run_completed"), Is.Zero);
            MukJumpAnalytics.Revive();
            MukJumpAnalytics.GameOver(200, 60);
            MukJumpAnalytics.EndRun("private-run-never-export", 200, 60, false, true);
            MukJumpAnalytics.EndRun("private-run-never-export", 200, 60, false, true);
            Assert.That(Count("mukjump_run_started"), Is.EqualTo(1));
            Assert.That(Count("mukjump_run_completed"), Is.EqualTo(1));
            var result = events.Single(e => Equals(e.Parameters["log_name"], "mukjump_run_completed"));
            Assert.That(result.Parameters["score"], Is.EqualTo(200L));
            Assert.That(result.IsScreen, Is.False);
        }

        [Test]
        public void AbandonDebugAndRestoredRecordsDoNotInventCompletion()
        {
            Begin();
            MukJumpAnalytics.EndRun("private-run-never-export", 200, 60, true, false);
            MukJumpAnalytics.BeginRun("debug", false, 0);
            MukJumpAnalytics.EndRun("debug", 999, 1, false, false);
            MukJumpAnalytics.EndRun("restored", 999, 1, false, false);
            Assert.That(Count("mukjump_run_completed"), Is.Zero);
        }

        [TestCase(false, true, 1)]
        [TestCase(false, false, 0)]
        [TestCase(true, true, 0)]
        public void TutorialCompletionRequiresActualCompletionAndSave(bool skipped, bool saved, int count)
        {
            MukJumpAnalytics.TutorialBegin(); MukJumpAnalytics.TutorialEnd(skipped, saved);
            Assert.That(Count("mukjump_tutorial_completed"), Is.EqualTo(count));
        }

        [Test]
        public void HighFrequencyAndSensitiveEventsAreNotForwarded()
        {
            Begin();
            for (int i = 0; i < 1000; i++) MukJumpAnalytics.Stroke(2);
            MukJumpAnalytics.Account(AnalyticsAccountAction.Login, AnalyticsOutcome.Success);
            MukJumpAnalytics.Ad(AnalyticsAdStage.Clicked);
            Assert.That(Count("mukjump_first_stroke"), Is.EqualTo(1));
            Assert.That(events, Has.Count.EqualTo(3)); // 시작, 플레이 화면, 첫 먹선
            Assert.That(string.Join(" ", events.SelectMany(e => e.Parameters.Values)), Does.Not.Contain("private-run"));
        }

        [Test]
        public void ScreenTokenIsValidatedAndFutureSensitiveFieldsAreDropped()
        {
            var mapped = AppsInTossAnalyticsRuntime.Map(Raw("screen_view", new()
                { ["screen_name"] = "lobby", ["user_id"] = "private", ["email"] = "private", ["log_name"] = "spoof" }));
            Assert.That(mapped.IsScreen, Is.True);
            Assert.That(mapped.Parameters.Keys, Is.EquivalentTo(new[] { "schema_version", "log_name", "screen_name" }));
            Assert.That(mapped.Parameters["log_name"], Is.EqualTo("mukjump_screen_view"));
            foreach (string screen in new[] { "private-username", "0", "999", "Lobby", "lobby,playing" })
                Assert.That(AppsInTossAnalyticsRuntime.Map(Raw("screen_view", new() { ["screen_name"] = screen })), Is.Null);
        }

        [Test]
        public void NonNumericAndOutOfRangeMetricsAreNeverExported()
        {
            var mapped = AppsInTossAnalyticsRuntime.Map(Raw("post_score", new()
                { ["score"] = "private", ["new_best"] = 5L, ["user_id"] = "private" }));
            Assert.That(mapped.Parameters.Keys, Is.EquivalentTo(new[] { "schema_version", "log_name" }));
        }

        [Test]
        public void SlowTransportIsBoundedAndStopDiscardsPendingEvents()
        {
            var gate = new TaskCompletionSource<bool>();
            int calls = 0;
            var queue = new AppsInTossAnalyticsRuntime.EventQueue(_ => { calls++; return gate.Task; });
            var source = Raw("first_stroke", new());
            for (int i = 0; i < 1000; i++) queue.Enqueue(source);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(queue.PendingCount, Is.EqualTo(AppsInTossAnalyticsRuntime.EventQueue.Capacity));
            queue.Stop(); gate.SetResult(true); queue.Enqueue(source);
            Assert.That(queue.PendingCount, Is.Zero);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void TransportFailureDoesNotRetryOrBlockLaterEvents()
        {
            int calls = 0;
            var queue = new AppsInTossAnalyticsRuntime.EventQueue(_ =>
                ++calls == 1 ? Task.FromException(new InvalidOperationException("not for logging")) : Task.CompletedTask);
            Assert.DoesNotThrow(() => { queue.Enqueue(Raw("first_stroke", new())); queue.Enqueue(Raw("level_start", new())); });
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(queue.PendingCount, Is.Zero);
        }

        static MukJumpAnalyticsEvent Raw(string name, Dictionary<string, object> parameters)
            => (MukJumpAnalyticsEvent)Activator.CreateInstance(typeof(MukJumpAnalyticsEvent),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { name, parameters }, null);
    }
}
