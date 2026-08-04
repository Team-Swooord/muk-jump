using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class SubmissionSafetyTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
            ScoreManager.RestoreDefaultStoreForTests();
        }

        [Test]
        public void DebugHeightCannotOverwriteSavedBest()
        {
            root = new GameObject("SubmissionSafetyTests");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            int savedBest = score.Best;

            score.ResetOrigin(0f);
            score.DebugSetHeight(savedBest + 1000, null);
            score.SaveBest();

            Assert.That(score.RecordsAllowed, Is.False);
            Assert.That(score.Best, Is.EqualTo(savedBest));
            Assert.That(score.IsNewBestThisRun, Is.False);
        }

        [Test]
        public void TransientBestReadFailureCannotDowngradeExistingRecord()
        {
            var store = new MemoryScoreStore
            {
                Best = 100,
                ThrowOnLoad = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("ScoreReadRecoveryTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            SetProperty(score, "Height", 50);

            store.ThrowOnLoad = false;
            Assert.That(score.TrySaveBest(), Is.True);
            Assert.That(score.Best, Is.EqualTo(100));
            Assert.That(store.Best, Is.EqualTo(100));
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void PersistentBestReadFailureBlocksRecordWrite()
        {
            var store = new MemoryScoreStore
            {
                Best = 100,
                ThrowOnLoad = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("ScoreReadFailureTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            SetProperty(score, "Height", 150);

            Assert.That(score.TrySaveBest(), Is.False);
            Assert.That(store.Best, Is.EqualTo(100));
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void AmbiguousAppliedScoreWriteIsRetriedIdempotently()
        {
            var store = new MemoryScoreStore
            {
                Best = 100,
                ThrowOnSave = true,
                ApplyBeforeThrow = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("AmbiguousScoreWriteTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            SetProperty(score, "Height", 200);

            Assert.That(score.TrySaveBest(), Is.False,
                "flush 예외 뒤 같은 프로세스 readback만으로 내구 저장을 확정하면 안 됩니다.");
            Assert.That(store.Best, Is.EqualTo(200));

            store.ThrowOnSave = false;
            Assert.That(score.TrySaveBest(), Is.True);
            Assert.That(score.Best, Is.EqualTo(200));
            Assert.That(store.Best, Is.EqualTo(200));
        }

        [Test]
        public void PendingScoreRetryCannotDowngradeNewerPersistedBest()
        {
            var store = new MemoryScoreStore
            {
                Best = 100,
                ThrowOnSave = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("MonotonicScoreRetryTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            SetProperty(score, "Height", 150);

            Assert.That(score.TrySaveBest(), Is.False);
            store.Best = 200;
            store.ThrowOnSave = false;

            Assert.That(score.TrySaveBest(), Is.True);
            Assert.That(score.Best, Is.EqualTo(200));
            Assert.That(store.Best, Is.EqualTo(200));
        }

        [Test]
        public void AbandonedRecordCandidateCannotLeakIntoNextRun()
        {
            var store = new MemoryScoreStore
            {
                Best = 0,
                ThrowOnSave = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("AbandonedScoreRunOne");
            var firstScore = root.AddComponent<ScoreManager>();
            Invoke(firstScore, "OnEnable");
            Invoke(firstScore, "Awake");
            SetProperty(firstScore, "Height", 100);

            Assert.That(firstScore.TrySaveBest(), Is.False);
            firstScore.StopPendingBestSaveRetry();
            Object.DestroyImmediate(root);

            store.ThrowOnSave = false;
            root = new GameObject("AbandonedScoreRunTwo");
            var secondScore = root.AddComponent<ScoreManager>();
            Invoke(secondScore, "OnEnable");
            Invoke(secondScore, "Awake");
            SetProperty(secondScore, "Height", 10);

            Assert.That(secondScore.TrySaveBest(), Is.True);
            Assert.That(store.Best, Is.EqualTo(10),
                "포기한 이전 판 100m 후보가 다음 10m 판에 섞이면 안 됩니다.");
        }

        [Test]
        public void AppliedAmbiguousRecordRemainsMonotonicWhenRetryStops()
        {
            var store = new MemoryScoreStore
            {
                Best = 0,
                ThrowOnSave = true,
                ApplyBeforeThrow = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("AppliedAmbiguousScoreRunOne");
            var firstScore = root.AddComponent<ScoreManager>();
            Invoke(firstScore, "OnEnable");
            Invoke(firstScore, "Awake");
            SetProperty(firstScore, "Height", 200);

            Assert.That(firstScore.TrySaveBest(), Is.False);
            firstScore.StopPendingBestSaveRetry();
            Object.DestroyImmediate(root);

            store.ThrowOnSave = false;
            root = new GameObject("AppliedAmbiguousScoreRunTwo");
            var secondScore = root.AddComponent<ScoreManager>();
            Invoke(secondScore, "OnEnable");
            Invoke(secondScore, "Awake");

            Assert.That(secondScore.Best, Is.EqualTo(200),
                "flush 예외 전에 이미 반영된 단조 최고기록은 안전하게 하향 롤백할 수 없습니다.");
        }

        [Test]
        public void EnablingDebugInvincibilityTaintsCurrentRun()
        {
            root = new GameObject("SubmissionSafetyTests");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            var manager = root.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            manager.ToggleDebugInvincible();

            Assert.That(GameManager.DebugToolsAvailable, Is.True,
                "EditMode 검증에서는 개발 도구가 활성화되어야 합니다.");
            Assert.That(manager.DebugInvincible, Is.True);
            Assert.That(score.RecordsAllowed, Is.False);
        }

        [Test]
        public void MainSceneContainsNoMissingScriptReferences()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            string source = File.ReadAllText(scenePath);

            Assert.That(Regex.IsMatch(
                    source, @"m_Script:\s*\{\s*fileID:\s*0(?:\s*,|\s*\})"),
                Is.False,
                "Main 씬에 Missing Script(fileID 0)가 남아 있습니다.");

            MatchCollection references = Regex.Matches(
                source,
                @"m_Script:\s*\{\s*fileID:\s*\d+\s*,\s*guid:\s*([0-9a-f]{32})");
            Assert.That(references.Count, Is.GreaterThan(0));
            foreach (Match reference in references)
            {
                string guid = reference.Groups[1].Value;
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty,
                    $"Main 씬의 스크립트 GUID를 찾을 수 없습니다: {guid}");
            }
        }

        [Test]
        public void MainSceneContainsSerializedVfxFoundationFromBuilder()
        {
            const string scenePath = "Assets/Scenes/Main.unity";
            string source = File.ReadAllText(scenePath);
            string monitorGuid = AssetDatabase.AssetPathToGUID(
                "Assets/Scripts/Core/VfxRuntimeMonitor.cs");

            Assert.That(Regex.Matches(source, @"m_Name: VfxQualityButton\b").Count,
                Is.EqualTo(1));
            Assert.That(Regex.Matches(source, @"m_Name: VfxStatsText\b").Count,
                Is.EqualTo(1));
            Assert.That(Regex.Matches(source, @"m_Name: BrushDrawingAudio\b").Count,
                Is.EqualTo(1));
            Assert.That(Regex.Matches(source, @"m_Name: PriorityAccentAudio\b").Count,
                Is.EqualTo(1));
            Assert.That(Regex.Matches(
                    source,
                    $@"m_Script:\s*\{{[^}}]*guid:\s*{monitorGuid}[^}}]*\}}").Count,
                Is.EqualTo(1));
            Assert.That(source, Does.Contain("m_HDR: 0"));
            Assert.That(source, Does.Contain("m_AllowMSAA: 0"));
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
