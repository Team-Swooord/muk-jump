using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;

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
        public void UnconfirmedBestBlocksRunUntilRecoveredBaselineIsKnown()
        {
            var store = new MemoryScoreStore
            {
                Best = 100,
                ThrowOnLoad = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("RunBaselineRecoveryTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            var manager = root.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            manager.StartGameFromMenu();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby),
                "기존 최고 기록을 확인하지 못한 상태에서 0m 기준으로 판을 시작하면 안 됩니다.");
            store.ThrowOnLoad = false;
            Assert.That(score.TryEnsureBestLoaded(), Is.True);
            score.ResetOrigin(0f);
            score.SampleWorldHeight(50f);
            Assert.That(score.Best, Is.EqualTo(100));
            Assert.That(score.RunBestToBeat, Is.EqualTo(100));
            Assert.That(score.IsNewBestThisRun, Is.False);
        }

        [Test]
        public void PendingBestRewriteBlocksNextRunUntilVerified()
        {
            var store = new MemoryScoreStore
            {
                Best = 25,
                ThrowOnSave = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("PendingBestRunBaselineTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            score.SampleWorldHeight(74f);
            Assert.That(score.TrySaveBest(), Is.False);
            Assert.That(score.HasPendingBestSaveRetry, Is.True);
            var manager = root.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            var playerObject = new GameObject("PendingBestBaselinePlayer");
            playerObject.transform.SetParent(root.transform, false);
            playerObject.AddComponent<SpriteRenderer>();
            var body = playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            var player = playerObject.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            body.bodyType = RigidbodyType2D.Kinematic;
            manager.RegisterPlayer(player);

            manager.StartGameFromMenu();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(score.HasPendingBestSaveRetry, Is.True);
            store.ThrowOnSave = false;

            manager.StartGameFromMenu();

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(score.HasPendingBestSaveRetry, Is.False);
            Assert.That(score.Best, Is.EqualTo(74));
            Assert.That(score.RunBestToBeat, Is.EqualTo(74));
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
                Best = 25,
                ThrowOnSave = true,
                ApplyBeforeThrow = true,
            };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("AmbiguousScoreWriteTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            score.SampleWorldHeight(200f);

            Assert.That(score.TrySaveBest(), Is.False,
                "flush 예외 뒤 같은 프로세스 readback만으로 내구 저장을 확정하면 안 됩니다.");
            Assert.That(store.Best, Is.EqualTo(200));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25));
            Assert.That(score.IsNewBestThisRun, Is.True);

            store.ThrowOnSave = false;
            Assert.That(score.TrySaveBest(), Is.True);
            Assert.That(score.Best, Is.EqualTo(200));
            Assert.That(store.Best, Is.EqualTo(200));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25),
                "재시도 성공이 광고 부활로 이어질 현재 판의 기준 기록을 바꾸면 안 됩니다.");
            Assert.That(score.IsNewBestThisRun, Is.True);
        }

        [Test]
        public void LowerReadbackKeepsCandidateForTheNextVerifiedRetry()
        {
            var store = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("LowerReadbackScoreRetryTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            score.SampleWorldHeight(74f);
            store.ForcedLoadBest = 25;

            Assert.That(score.TrySaveBest(), Is.False);
            Assert.That(store.Best, Is.EqualTo(74));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25));

            store.ForcedLoadBest = null;
            Assert.That(score.TryCommitBestCandidate(0), Is.True,
                "호출 후보가 낮아도 확인되지 않은 74m 후보를 다시 저장해야 합니다.");
            Assert.That(store.SaveCount, Is.EqualTo(2));
            Assert.That(score.Best, Is.EqualTo(74));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25));
        }

        [Test]
        public void CommittingExplicitCandidatePreservesLiveRunStateAndNotifiesOnce()
        {
            var store = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("ExplicitScoreCommitTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            SetProperty(score, "Height", 74);
            int committed = -1;
            int notifications = 0;
            score.BestCommitted += value =>
            {
                committed = value;
                notifications++;
            };

            Assert.That(score.TryCommitBestCandidate(74), Is.True);

            Assert.That(store.Best, Is.EqualTo(74));
            Assert.That(score.Best, Is.EqualTo(74));
            Assert.That(score.Height, Is.EqualTo(74));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25),
                "광고 부활로 같은 판을 이어갈 때 판 시작 기록 기준은 바뀌면 안 됩니다.");
            Assert.That(committed, Is.EqualTo(74));
            Assert.That(notifications, Is.EqualTo(1));

            Assert.That(score.TryCommitBestCandidate(60), Is.True);
            Assert.That(notifications, Is.EqualTo(1),
                "낮은 후보를 다시 확인하는 동작은 UI·클라우드 갱신을 중복 발생시키면 안 됩니다.");
        }

        [Test]
        public void ThrowingBestCommittedListenerCannotInvalidateDurableSave()
        {
            var store = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("ThrowingBestCommittedListenerTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            int laterCommitted = -1;
            score.BestCommitted += _ =>
                throw new System.InvalidOperationException("best observer failed");
            score.BestCommitted += value => laterCommitted = value;
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 최고 기록 완료 알림 구독자 예외를 격리했습니다: " +
                "best observer failed");

            Assert.That(score.TryCommitBestCandidate(74), Is.True);

            Assert.That(store.Best, Is.EqualTo(74));
            Assert.That(score.Best, Is.EqualTo(74));
            Assert.That(score.HasConfirmedBest, Is.True);
            Assert.That(score.HasPendingBestSaveRetry, Is.False);
            Assert.That(laterCommitted, Is.EqualTo(74));
        }

        [Test]
        public void ThrowingNewBestListenerCannotInterruptSamplingOrLaterListener()
        {
            var store = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(store);
            root = new GameObject("ThrowingNewBestListenerTest");
            var score = root.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            score.ResetOrigin(0f);
            int laterHeight = -1;
            score.NewBestReached += (_, _) =>
                throw new System.InvalidOperationException("new best observer failed");
            score.NewBestReached += (height, _) => laterHeight = height;
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 신기록 알림 구독자 예외를 격리했습니다: " +
                "new best observer failed");

            score.SampleWorldHeight(74f);

            Assert.That(score.Height, Is.EqualTo(74));
            Assert.That(score.IsNewBestThisRun, Is.True);
            Assert.That(laterHeight, Is.EqualTo(74));
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

        [Test]
        public void WebGlTemplateCoversViewportWithoutBlackFallback()
        {
            const string sdkTemplate =
                "<html><head><!-- USER_HEAD_START -->\n" +
                "<!-- USER_HEAD_END --></head><body></body></html>";
            Assert.That(
                MukJumpAppsInTossSetup.TryApplyFullscreenTemplate(
                    sdkTemplate,
                    out string source,
                    out string error),
                Is.True,
                error);

            Assert.That(source, Does.Contain("id=\"mukjump-fullscreen-webgl\""));
            Assert.That(source, Does.Contain("width: 100vw;"));
            Assert.That(source, Does.Contain("height: 100dvh;"));
            Assert.That(source, Does.Contain("background: #EAE3D2 !important;"));
        }

        [Test]
        public void InGameLegalUrlsUseExactAnonymousBackendPages()
        {
            Assert.That(
                MukJumpLegalUrls.TermsOfService,
                Is.EqualTo(
                    "https://storage.thebackend.io/" +
                    "27f4347cc58b6eca8349b49f00b25a0a9f7c92836f10ec5f6385356867184326/" +
                    "terms.html"));
            Assert.That(
                MukJumpLegalUrls.PrivacyPolicy,
                Is.EqualTo(
                    "https://storage.thebackend.io/" +
                    "27f4347cc58b6eca8349b49f00b25a0a9f7c92836f10ec5f6385356867184326/" +
                    "privacy.html"));
            Assert.That(
                MukJumpLegalUrls.PrivacyPolicy,
                Does.Not.Contain("github.com"));
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
