using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class TimedLeaderboardTests
    {
        GameObject host;
        MemoryScoreStore store;
        ScoreManager score;
        [SetUp] public void SetUp()
        {
            store = new MemoryScoreStore();
            ScoreManager.UseStoreForTests(store);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            host = new GameObject("TimedRecordTest");
            score = host.AddComponent<ScoreManager>();
            typeof(ScoreManager).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(score, null);
            typeof(ScoreManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(score, null);
        }
        [TearDown] public void TearDown()
        {
            typeof(ScoreManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(score, null);
            Object.DestroyImmediate(host);
            ScoreManager.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }
        void Candidate(int height, int seconds)
        {
            score.ResetOrigin(0);
            score.SampleWorldHeight(height);
            typeof(ScoreManager).GetField("<HeightTimeSeconds>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(score, seconds);
        }
        [TestCase(101, 900, 100, 1, true)]
        [TestCase(100, 50, 100, 60, true)]
        [TestCase(100, 60, 100, 50, false)]
        [TestCase(100, 60, 100, 60, false)]
        [TestCase(100, 60, 100, -1, true)]
        [TestCase(100, -1, 100, 60, false)]
        [TestCase(99, 1, 100, 60, false)]
        public void HeightFirstThenFasterTime(int a, int ta, int b, int tb, bool better) =>
            Assert.That(new BestRunRecord(a, ta).IsBetterThan(new BestRunRecord(b, tb)), Is.EqualTo(better));

        [TestCase(1, 0)] [TestCase(237, 62)] [TestCase(237, -1)]
        [TestCase(int.MaxValue, 0)] [TestCase(int.MaxValue, BestRunRecord.MaximumTimedSeconds)]
        public void ServerScoreIsLosslessWithinSafeIntegerRange(int height, int seconds)
        {
            var record = new BestRunRecord(height, seconds);
            Assert.That(record.RankScore, Is.LessThanOrEqualTo(9007199254740991L));
            var decoded = BestRunRecord.FromRankScore((long)(double)record.RankScore);
            Assert.That(decoded.Height, Is.EqualTo(height));
            Assert.That(decoded.Seconds, Is.EqualTo(seconds));
            Assert.That(BestRunRecord.Decode(record.Encode()).RankScore, Is.EqualTo(record.RankScore));
        }
        [TestCase(-1, "--:--")] [TestCase(0, "00:00")] [TestCase(59, "00:59")]
        [TestCase(60, "01:00")] [TestCase(125, "02:05")] [TestCase(3601, "60:01")]
        public void DisplaysMinutesAndSeconds(int seconds, string expected) =>
            Assert.That(BestRunRecord.FormatTime(seconds), Is.EqualTo(expected));

        [Test] public void SameHeightFasterTimeCommitsAndNotifies()
        {
            Candidate(237, 80); Assert.That(score.TrySaveBest(), Is.True);
            int notifications = 0; score.BestCommitted += _ => notifications++;
            Candidate(237, 55); Assert.That(score.TrySaveBest(), Is.True);
            Assert.That(score.Best, Is.EqualTo(237)); Assert.That(score.BestTimeSeconds, Is.EqualTo(55));
            Assert.That(store.Seconds, Is.EqualTo(55)); Assert.That(notifications, Is.EqualTo(1));
            Candidate(237, 90); score.TrySaveBest();
            Assert.That(store.Seconds, Is.EqualTo(55)); Assert.That(notifications, Is.EqualTo(1));
        }
        [Test] public void HigherSlowerRunKeepsItsOwnTime()
        {
            Candidate(100, 10); score.TrySaveBest(); Candidate(200, 90); score.TrySaveBest();
            Assert.That(score.BestTimeSeconds, Is.EqualTo(90));
            Assert.That(score.TryMergeVerifiedBest(100, 5), Is.True);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(90));
        }
        [Test] public void FailedFlushRetriesTheWholePair()
        {
            Candidate(237, 80); score.TrySaveBest();
            Candidate(237, 50); store.ThrowOnSave = true; store.ApplyBeforeThrow = true;
            Assert.That(score.TrySaveBest(), Is.False);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(80));
            Assert.That(score.HasPendingBestSaveRetry, Is.True);
            store.ThrowOnSave = false;
            Assert.That(score.TryPrepareRunBaseline(), Is.True);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(50));
            Assert.That(score.HasPendingBestSaveRetry, Is.False);
        }
        [Test] public void DebugRunCannotReplaceTime()
        {
            Candidate(100, 90); score.TrySaveBest(); Candidate(100, 5); score.InvalidateCurrentRunForRecords();
            score.TrySaveBest(); Assert.That(store.Seconds, Is.EqualTo(90));
        }
        [Test] public void AccountReplacementAndDeletionDoNotLeakTime()
        {
            Candidate(237, 55); score.TrySaveBest();
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(237), Is.True);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(-1));
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(300, 80), Is.True);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(80));
            Assert.That(ScoreManager.TryClearForAccountDeletion(), Is.True);
            Assert.That(ScoreManager.StoredRecord.Seconds, Is.EqualTo(-1));
        }
        [Test] public void LegacyOrMismatchedCloudTimeIsUnknown()
        {
            var legacy = JsonUtility.FromJson<MukJumpCloudSnapshot>("{\"bestHeight\":237}");
            Assert.That(legacy.BestRecord.Seconds, Is.EqualTo(-1));
            var snapshot = new MukJumpCloudSnapshot { bestHeight = 237, bestRecordVersion = 1, bestTimeHeight = 100, bestTimeSeconds = 10 };
            Assert.That(snapshot.BestRecord.Seconds, Is.EqualTo(-1));
            snapshot.bestTimeHeight = 237;
            Assert.That(JsonUtility.FromJson<MukJumpCloudSnapshot>(JsonUtility.ToJson(snapshot)).BestRecord.Seconds, Is.EqualTo(10));
        }
        [Test] public void CapturedCloudSnapshotCarriesPairedTime()
        {
            Candidate(237, 55); score.TrySaveBest();
            var snapshot = MukJumpCloudSnapshot.Capture(237, 1, "test");
            Assert.That(snapshot.BestRecord.Seconds, Is.EqualTo(55));
            Assert.That(MukJumpCloudSnapshot.Capture(500, 1, "test").BestRecord.Seconds, Is.EqualTo(-1));
        }
        [Test] public void InterruptedSettlementKeepsPeakTimeNotDeathTime()
        {
            var snapshot = new PendingGameOverSettlementSnapshot
            { scoreHeight = 237, scoreTimeVersion = 1, scoreTimeSeconds = 55, activeGameplaySeconds = 90 };
            var restored = JsonUtility.FromJson<PendingGameOverSettlementSnapshot>(JsonUtility.ToJson(snapshot));
            Assert.That(restored.ScoreRecord.Seconds, Is.EqualTo(55));
            Assert.That(score.TryMergeVerifiedBest(restored.scoreHeight, restored.ScoreRecord.Seconds), Is.True);
            Assert.That(score.BestTimeSeconds, Is.EqualTo(55));
            var legacy = JsonUtility.FromJson<PendingGameOverSettlementSnapshot>("{\"scoreHeight\":300,\"activeGameplaySeconds\":90}");
            Assert.That(legacy.ScoreRecord.Seconds, Is.EqualTo(-1));
        }
        [TestCase(GameLanguage.Korean, "소요시간", "고도 / 시간")]
        [TestCase(GameLanguage.English, "Time", "Height / Time")]
        [TestCase(GameLanguage.Japanese, "経過時間", "高度 / 時間")]
        public void TimerLabelsAreLocalized(GameLanguage language, string label, string heading)
        {
            GameLocalization.SetLanguage(language);
            Assert.That(GameLocalization.Translate("소요시간"), Is.EqualTo(label));
            Assert.That(GameLocalization.Translate("고도 / 시간"), Is.EqualTo(heading));
        }
    }
}
