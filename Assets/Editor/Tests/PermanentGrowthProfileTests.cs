using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthProfileTests
    {
        MemoryPermanentGrowthStore store;
        MemoryPendingGameOverSettlementStore pendingSettlementStore;
        MemoryScoreStore scoreStore;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            pendingSettlementStore =
                new MemoryPendingGameOverSettlementStore();
            GameManager.UsePendingGameOverSettlementStoreForTests(
                pendingSettlementStore);
            scoreStore = new MemoryScoreStore();
            ScoreManager.UseStoreForTests(scoreStore);
        }

        [TearDown]
        public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
        }

        [Test]
        public void FreshProfileStartsEmpty()
        {
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.NextDistanceRewardMeters,
                Is.EqualTo(50));
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().OwnedNodeCount,
                Is.Zero);
            Assert.That(store.SaveCount, Is.Zero,
                "빈 프로필 조회만으로 저장을 만들면 안 됩니다.");
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void GrowthSaveContractIsPreservedInStrippedPlayers()
        {
            var linker = new System.Xml.XmlDocument();
            linker.Load(System.IO.Path.Combine(
                Application.dataPath, "Scripts/Core/link.xml"));
            foreach (string nestedType in new[] { "SaveData", "SaveHeader", "RankRecord" })
                Assert.That(linker.SelectSingleNode(
                    "/linker/assembly[@fullname='Assembly-CSharp']/type" +
                    "[@fullname='MukJump.Core.PermanentGrowthProfile/" +
                    nestedType + "'][@preserve='all']"), Is.Not.Null,
                    "IL2CPP에서도 JsonUtility 저장 계약 전체가 남아야 합니다.");
        }

        [Test]
        public void Device183MeterPendingRunRecoversWithFullSaveContract()
        {
            const string runId = "0123456789abcdef0123456789abcdef";
            pendingSettlementStore.Json = JsonUtility.ToJson(
                new PendingGameOverSettlementSnapshot
                {
                    runId = runId,
                    swarmProgressHeight = 182,
                    scoreHeight = 183,
                    previousBest = 0,
                    activeGameplaySeconds = 47.597755f,
                    eligible = true,
                });

            Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
            Assert.That(store.Json, Does.Contain("\"tutorialRewardClaimed\":false"));
            Assert.That(store.Json, Does.Contain("\"rewardMilestoneWatermarkInitialized\":false"));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(scoreStore.Best, Is.EqualTo(183));
            Assert.That(pendingSettlementStore.Json, Is.Empty);

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(183));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2),
                "재시작 시 같은 판의 먹빛을 중복 지급하면 안 됩니다.");
        }

        [Test]
        public void DebugResetClearsNodesAndUsesSessionOnly999Currency()
        {
            SeedV2(39, "I00", "S00");
            int changedCount = 0;
            PermanentGrowthProfile.Changed += () => changedCount++;

            PermanentGrowthProfile.DebugResetProgress();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.IsDebugCurrencyActive, Is.True);
            Assert.That(store.Json, Does.Contain("\"wallet\":0"));
            Assert.That(store.Json, Does.Contain("\"ownedNodeIds\":[]"));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out _),
                Is.True);

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero,
                "999 먹빛은 저장 재화가 아니라 현재 개발 세션 전용이어야 합니다.");
            Assert.That(PermanentGrowthProfile.IsDebugCurrencyActive, Is.False);
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out _),
                Is.True);
        }

        [Test]
        public void DebugRefillDeductsOnePerPurchasedNodeAndCanRefillAgain()
        {
            PermanentGrowthProfile.DebugRefillCurrency();

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("brush.1"), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(998));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out _),
                Is.True);

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out _),
                Is.True);

            PermanentGrowthProfile.DebugRefillCurrency();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
        }

        [Test]
        public void DebugPurchaseAtRewardCapSpendsRealWalletBeforeSponsoring()
        {
            int lastIndex = PermanentGrowthCatalog.Nodes.Count - 1;
            var owned = new string[lastIndex];
            for (int i = 0; i < lastIndex; i++)
                owned[i] = PermanentGrowthCatalog.Nodes[i].Id;
            store.Json = CurrentSaveJson(1, owned);
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
            PermanentGrowthProfile.DebugRefillCurrency();

            string finalNodeId =
                PermanentGrowthCatalog.Nodes[lastIndex].Id;
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(finalNodeId),
                Is.True);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.Nodes.Count));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(RunRewardCalculator.MaxRewardCount));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out _),
                Is.True);

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.Nodes.Count));
        }

        [TestCase(0, 0)]
        [TestCase(1, 20)]
        [TestCase(5, 100)]
        [TestCase(6, 150)]
        [TestCase(13, 500)]
        [TestCase(14, 600)]
        [TestCase(26, 1800)]
        [TestCase(27, 1950)]
        [TestCase(39, 3750)]
        [TestCase(40, 3900)]
        [TestCase(244, 34500)]
        public void FrozenLegacyDistanceRewardThresholdsStayCompatible(
            int rewardCount,
            long expectedDistance)
        {
            Assert.That(
                RunRewardCalculator.GetLegacyThresholdForRewardCount(rewardCount),
                Is.EqualTo(expectedDistance));
        }

        [TestCase(0, 0)]
        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(99, 4)]
        [TestCase(100, 5)]
        [TestCase(149, 5)]
        [TestCase(150, 6)]
        [TestCase(500, 13)]
        [TestCase(1800, 26)]
        [TestCase(3749, 38)]
        [TestCase(3750, 39)]
        [TestCase(3900, 40)]
        [TestCase(34499, 243)]
        [TestCase(34500, 244)]
        [TestCase(100000, 244)]
        public void FrozenLegacyDistanceReturnsEveryCrossedRewardCount(
            long cumulativeDistance,
            int expectedRewardCount)
        {
            Assert.That(
                RunRewardCalculator.GetLegacyRewardCountForDistance(
                    cumulativeDistance),
                Is.EqualTo(expectedRewardCount));
        }

        [Test]
        public void DistanceJourneyExactlyFundsThePermanentTree()
        {
            Assert.That(RunRewardCalculator.MaxRewardCount,
                Is.EqualTo(PermanentGrowthCatalog.TotalCost));
            Assert.That(
                RunRewardCalculator.GetThresholdForRewardCount(
                    RunRewardCalculator.MaxRewardCount),
                Is.EqualTo(RunRewardCalculator.FinalRewardDistance));
        }

        [Test]
        public void DistanceBeyondFinalTierTracksButCannotExceedEconomyCap()
        {
            store.Json = CurrentSaveJson(243);
            PermanentGrowthProfile.ResetCacheForTests();

            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "finish-distance-journey",
                    0,
                    1000,
                    0,
                    0f,
                    true);

            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(1));
            Assert.That(settlement.Balance, Is.EqualTo(244));
            Assert.That(settlement.DistanceJourneyComplete, Is.True);
            Assert.That(settlement.CumulativeDistanceMeters, Is.EqualTo(112650));
        }

        [Test]
        public void LegacyProgressMigratesToEquivalentDistanceTier()
        {
            store.Json = LegacyV6SaveJson(3, "I00", "I-A1");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(100));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
        }

        [Test]
        public void SettlementPersistsAndSameRunCannotPayTwiceAfterReload()
        {
            PermanentGrowthSettlement first = PermanentGrowthProfile.SettleRun(
                "run-001",
                12,
                250,
                99,
                20f,
                true);

            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Earned, Is.EqualTo(3));
            Assert.That(first.Balance, Is.EqualTo(3));
            Assert.That(first.RunDistanceMeters, Is.EqualTo(250));
            Assert.That(first.CumulativeDistanceMeters, Is.EqualTo(250));
            Assert.That(first.PreviousRewardDistanceMeters, Is.EqualTo(250));
            Assert.That(first.NextRewardDistanceMeters, Is.EqualTo(350));

            PermanentGrowthProfile.ResetCacheForTests();
            PermanentGrowthSettlement duplicate = PermanentGrowthProfile.SettleRun(
                "run-001",
                1000,
                1000,
                0,
                999f,
                true);

            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.Earned, Is.Zero);
            Assert.That(duplicate.Balance, Is.EqualTo(3));
            Assert.That(duplicate.CumulativeDistanceMeters, Is.EqualTo(250));
        }

        [Test]
        public void ThrowingChangedSubscriberCannotAbortDurableSettlement()
        {
            int laterSubscriberCalls = 0;
            PermanentGrowthProfile.Changed += () =>
                throw new System.InvalidOperationException("listener failed");
            PermanentGrowthProfile.Changed += () => laterSubscriberCalls++;

            PermanentGrowthSettlement settlement = default;
            Assert.DoesNotThrow(() => settlement =
                PermanentGrowthProfile.SettleRun(
                    "subscriber-failure-run",
                    20,
                    20,
                    0,
                    1f,
                    true));

            Assert.That(settlement.Accepted, Is.True);
            Assert.That(laterSubscriberCalls, Is.EqualTo(1),
                "앞 구독자가 실패해도 뒤 UI/동기화 구독자는 갱신되어야 합니다.");
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(
                PermanentGrowthProfile.IsRunSettled("subscriber-failure-run"),
                Is.True,
                "구독자 예외 전에 확정된 정산은 재로드 뒤에도 유지되어야 합니다.");
        }

        [Test]
        public void PendingGameOverSettlementRecoversOnceAfterForcedQuit()
        {
            const string runId = "0123456789abcdef0123456789abcdef";
            var snapshot = new PendingGameOverSettlementSnapshot
            {
                runId = runId,
                swarmProgressHeight = 35,
                scoreHeight = 40,
                previousBest = 10,
                activeGameplaySeconds = 12f,
                eligible = true,
            };
            pendingSettlementStore.Json = JsonUtility.ToJson(snapshot);

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.True);
            Assert.That(PermanentGrowthProfile.IsRunSettled(runId), Is.True);
            Assert.That(
                PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(40));
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(scoreStore.Best, Is.EqualTo(40));
            Assert.That(pendingSettlementStore.Json, Is.Empty);

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(40),
                "복구 정산은 같은 run ID를 두 번 더하면 안 됩니다.");
        }

        [Test]
        public void PendingSettlementReadFailureIsPreservedForRetry()
        {
            pendingSettlementStore.Json = JsonUtility.ToJson(
                new PendingGameOverSettlementSnapshot
                {
                    runId = "1123456789abcdef0123456789abcdef",
                    scoreHeight = 40,
                    swarmProgressHeight = 35,
                    activeGameplaySeconds = 12f,
                    eligible = true,
                });
            pendingSettlementStore.ThrowOnLoad = true;

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.False);
            Assert.That(pendingSettlementStore.Json, Is.Not.Empty,
                "일시적인 읽기 실패를 손상 저장으로 오판해 지우면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.Zero);
        }

        [Test]
        public void PendingSettlementExistenceReadFailureBlocksRecoverySafely()
        {
            pendingSettlementStore.ThrowOnHas = true;

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.False);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.Zero);
        }

        [Test]
        public void PendingSettlementDeleteFailureCannotDuplicateAcceptedRun()
        {
            const string runId = "2123456789abcdef0123456789abcdef";
            pendingSettlementStore.Json = JsonUtility.ToJson(
                new PendingGameOverSettlementSnapshot
                {
                    runId = runId,
                    scoreHeight = 40,
                    swarmProgressHeight = 35,
                    activeGameplaySeconds = 12f,
                    eligible = true,
                });
            pendingSettlementStore.ThrowOnClear = true;

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.True,
                "정산된 run ID는 삭제 실패가 다음 화면을 막게 하면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(40));
            Assert.That(pendingSettlementStore.Json, Is.Not.Empty);

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.True);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(40),
                "남은 복구본을 다시 읽어도 같은 run ID는 중복 정산되면 안 됩니다.");
        }

        [Test]
        public void PendingRecoveryKeepsSnapshotUntilBestAndGrowthAreDurable()
        {
            const string runId = "6123456789abcdef0123456789abcdef";
            pendingSettlementStore.Json = JsonUtility.ToJson(
                new PendingGameOverSettlementSnapshot
                {
                    runId = runId,
                    scoreHeight = 74,
                    swarmProgressHeight = 74,
                    previousBest = 25,
                    activeGameplaySeconds = 20f,
                    eligible = true,
                });
            scoreStore.Best = 25;
            scoreStore.ThrowOnSave = true;

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.False);
            Assert.That(PermanentGrowthProfile.IsRunSettled(runId), Is.True,
                "기록 저장과 독립적으로 성장 정산은 먼저 내구 확정될 수 있습니다.");
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(74));
            Assert.That(scoreStore.Best, Is.EqualTo(25));
            Assert.That(pendingSettlementStore.Json, Is.Not.Empty,
                "최고 기록 저장이 실패한 동안 복구본을 지우면 안 됩니다.");

            // 프로세스 재시작을 모사해 메모리 후보를 버려도 복구본의 scoreHeight가
            // 독립적으로 최고 기록을 되살려야 한다.
            scoreStore.ThrowOnSave = false;
            ScoreManager.UseStoreForTests(scoreStore);
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(
                GameManager.TryRecoverPendingGameOverSettlement(),
                Is.True);
            Assert.That(scoreStore.Best, Is.EqualTo(74));
            Assert.That(pendingSettlementStore.Json, Is.Empty);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(74),
                "이미 정산된 run의 성장 거리는 재시도에서 중복되면 안 됩니다.");
        }

        [Test]
        public void SettlementHistoryRejectsOlderRunAfterAnotherRunSettles()
        {
            PermanentGrowthSettlement first = PermanentGrowthProfile.SettleRun(
                "run-A", 0, 500, 0, 0f, true);
            PermanentGrowthSettlement second = PermanentGrowthProfile.SettleRun(
                "run-B", 12, 500, 20, 20f, true);

            PermanentGrowthProfile.ResetCacheForTests();
            PermanentGrowthSettlement repeatedFirst =
                PermanentGrowthProfile.SettleRun(
                    "run-A", 1000, 1000, 0, 999f, true);

            Assert.That(first.Earned, Is.EqualTo(5));
            Assert.That(second.Earned, Is.EqualTo(3));
            Assert.That(repeatedFirst.Accepted, Is.False);
            Assert.That(repeatedFirst.Earned, Is.Zero);
            Assert.That(repeatedFirst.Balance, Is.EqualTo(8));
        }

        [Test]
        public void IneligibleRunPaysNothingAndDoesNotAdvanceDistance()
        {
            PermanentGrowthSettlement debug = PermanentGrowthProfile.SettleRun(
                "debug-run",
                1000,
                1000,
                0,
                999f,
                false);
            PermanentGrowthSettlement firstReal = PermanentGrowthProfile.SettleRun(
                "real-run",
                0,
                20,
                0,
                0f,
                true);

            Assert.That(debug.Accepted, Is.True);
            Assert.That(debug.Earned, Is.Zero);
            Assert.That(debug.CumulativeDistanceMeters, Is.Zero);
            Assert.That(firstReal.Earned, Is.Zero);
            Assert.That(firstReal.CumulativeDistanceMeters, Is.EqualTo(20));
        }

        [Test]
        public void PurchaseCostsExactlyOneAndOnlyUnlocksSelectedNode()
        {
            SeedV2(4);
            int changedCount = 0;
            PermanentGrowthProfile.Changed += () => changedCount++;

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("brush.1"), Is.True);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("brush.1"), Is.True);
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("body.1"), Is.False);
            Assert.That(PermanentGrowthProfile.InkCapacityMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(PermanentGrowthProfile.InkBudgetCostMultiplier,
                Is.EqualTo(0.97f).Within(0.0001f));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDuplicateAndInsufficientPurchasesDoNotMutateSave()
        {
            SeedV2(1);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("brush.1"), Is.True);
            int saveCount = store.SaveCount;

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("brush.1"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("brush.2"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("missing"), Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(saveCount));
        }

        [Test]
        public void CorruptSaveIsPreservedAndMutationsStayReadOnly()
        {
            const string corrupt = "{broken json";
            store.Json = corrupt;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.CorruptReadOnly));
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.False);
            Assert.That(
                PermanentGrowthProfile.SettleRun(
                    "blocked-run", 100, 100, 0, 20f, true).Accepted,
                Is.False);
            Assert.That(store.Json, Is.EqualTo(corrupt));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BackupSaveCount, Is.Zero);
            Assert.That(store.QuarantineSaveCount, Is.Zero,
                "사용자 복구 선택 전 원본을 자동 이동하거나 덮어쓰지 않습니다.");
        }

        [Test]
        public void FutureBalanceSaveIsPreservedWithoutDroppingUnknownNodes()
        {
            const string future =
                "{\"schemaVersion\":1,\"balanceVersion\":12," +
                "\"wallet\":9,\"ownedNodeIds\":[\"I-D1\"]," +
                "\"inkHandlingKeystoneId\":\"I-D1\"}";
            store.Json = future;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.FutureBalanceReadOnly));
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.False);
            Assert.That(store.Json, Is.EqualTo(future));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void HeaderOnlyCurrentSaveCannotOverwriteValidBackup()
        {
            const string truncated =
                "{\"schemaVersion\":1,\"balanceVersion\":8}";
            string backup = CurrentSaveJson(5, "I00");
            store.Json = truncated;
            store.BackupJson = backup;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.CorruptReadOnly));
            Assert.That(store.Json, Is.EqualTo(truncated));
            Assert.That(store.BackupJson, Is.EqualTo(backup));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void InvalidCurrentDistanceJourneyCannotOverwriteValidBackup()
        {
            string valid = CurrentSaveJson(4, "I00");
            string backup = CurrentSaveJson(2, "I00");
            string distanceField = $"\"cumulativeDistanceMeters\":{RunRewardCalculator.GetThresholdForRewardCount(5)}";
            string[] invalidPrimaries =
            {
                valid.Replace(distanceField + ",", ""),
                valid.Replace("\"claimedDistanceRewardCount\":5,", ""),
                valid.Replace(
                    "\"claimedDistanceRewardCount\":5",
                    "\"claimedDistanceRewardCount\":4"),
                valid.Replace(
                    distanceField,
                    "\"cumulativeDistanceMeters\":-1"),
                valid.Replace(
                    "\"claimedDistanceRewardCount\":5",
                    "\"claimedDistanceRewardCount\":40"),
                BuildSaveJson(
                    7,
                    -1,
                    0L,
                    0,
                    true,
                    "I00"),
            };

            for (int i = 0; i < invalidPrimaries.Length; i++)
            {
                Assert.That(invalidPrimaries[i], Is.Not.EqualTo(valid), "손상 fixture가 원문 그대로면 안 됩니다.");
                store = new MemoryPermanentGrowthStore
                {
                    Json = invalidPrimaries[i],
                    BackupJson = backup,
                };
                PermanentGrowthProfile.UseStoreForTests(store);

                Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
                Assert.That(store.Json, Is.EqualTo(invalidPrimaries[i]));
                Assert.That(store.BackupJson, Is.EqualTo(backup));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(store.BackupSaveCount, Is.Zero);
            }
        }

        [Test]
        public void VersionFiveInkTreeMigratesToNewBudgetSemanticsWithoutDataLoss()
        {
            store.Json = LegacyV6SaveJson(3, "I00", "I-A1")
                .Replace("\"balanceVersion\":6", "\"balanceVersion\":5");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(5));
        }

        [Test]
        public void VersionSixProgressMigratesToMatchingDistanceThreshold()
        {
            store.Json = LegacyV6SaveJson(5, "I00");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(6));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(6));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(150));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
        }

        [Test]
        public void VersionFiveInvalidOwnedGraphCannotOverwriteValidBackup()
        {
            string backup = CurrentSaveJson(5, "I00");
            string[] invalidPrimaries =
            {
                LegacyV6SaveJson(3, "I-A1")
                    .Replace("\"balanceVersion\":6", "\"balanceVersion\":5"),
                LegacyV6SaveJson(3, "I00", "I00")
                    .Replace("\"balanceVersion\":6", "\"balanceVersion\":5"),
            };

            for (int i = 0; i < invalidPrimaries.Length; i++)
            {
                store.Json = invalidPrimaries[i];
                store.BackupJson = backup;
                PermanentGrowthProfile.ResetCacheForTests();

                Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
                Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
                Assert.That(store.Json, Is.EqualTo(invalidPrimaries[i]));
                Assert.That(store.BackupJson, Is.EqualTo(backup));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(store.BackupSaveCount, Is.Zero);
            }
        }

        [Test]
        public void CurrentV7MissingDistanceJourneyCannotBeCanonicalized()
        {
            string truncated = CurrentSaveJson(4, "I00")
                .Replace("\"cumulativeDistanceMeters\":100,", "")
                .Replace("\"claimedDistanceRewardCount\":5,", "");
            string backup = CurrentSaveJson(2, "I00");
            store.Json = truncated;
            store.BackupJson = backup;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(store.Json, Is.EqualTo(truncated));
            Assert.That(store.BackupJson, Is.EqualTo(backup),
                "v7 필수 거리 payload가 잘린 primary가 정상 backup을 덮으면 안 됩니다.");
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void V2SaveMissingSettlementHistoryStaysReadOnly()
        {
            const string truncatedV2 =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":7,\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"old-run\",\"ranks\":[]," +
                "\"ownedNodeIds\":[]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            store.Json = truncatedV2;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.CorruptReadOnly));
            Assert.That(store.Json, Is.EqualTo(truncatedV2));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void UnsupportedSchemaSaveIsPreservedReadOnly()
        {
            const string futureSchema =
                "{\"schemaVersion\":2,\"balanceVersion\":1," +
                "\"wallet\":17}";
            store.Json = futureSchema;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.UnsupportedSchemaReadOnly));
            Assert.That(store.Json, Is.EqualTo(futureSchema));
            Assert.That(store.SaveCount, Is.Zero);
        }

        [Test]
        public void HealthyMutationMirrorsValidatedSaveIntoBackup()
        {
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "healthy-run", 0, 0, 0, 0f, true);

            Assert.That(settlement.Accepted, Is.True);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.BackupSaveCount, Is.EqualTo(1));
            Assert.That(store.BackupJson, Is.EqualTo(store.Json));
        }

        [Test]
        public void PrimarySaveFailureCannotAdvanceBackup()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            _ = PermanentGrowthProfile.Currency;
            failingStore.ThrowOnPrimarySave = true;

            PermanentGrowthSettlement failed =
                PermanentGrowthProfile.SettleRun(
                    "interrupted-run", 12, 20, 0, 20f, true);
            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero,
                "primary 실패 직후 메모리 지갑도 이전 값으로 돌아가야 합니다.");
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.Zero,
                "primary 실패 직후 누적 거리도 이전 값으로 돌아가야 합니다.");
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.Zero);
            Assert.That(failingStore.Json, Is.EqualTo(original));
            Assert.That(failingStore.BackupJson, Is.EqualTo(original));
            Assert.That(failingStore.BackupSaveCount, Is.Zero,
                "primary 확정 전에는 backup 세대를 전진시키면 안 됩니다.");
            Assert.That(failingStore.BackupSyncPending, Is.False,
                "primary가 반영되지 않았음이 확인되면 새 동기화 표식도 지워야 합니다.");
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);

            PermanentGrowthSettlement retry =
                PermanentGrowthProfile.SettleRun(
                    "interrupted-run", 12, 20, 0, 20f, true);
            Assert.That(retry.Accepted, Is.True,
                "실패한 runId가 메모리에 남아 재시도를 막으면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(20));
        }

        [Test]
        public void PrimaryExceptionAfterWriteCompletesBackupWithoutRollback()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
                ThrowOnPrimarySave = true,
                ApplyPrimaryBeforeThrow = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "applied-before-error", 12, 20, 0, 20f, true);

            Assert.That(settlement.Accepted, Is.True,
                "primary 값이 이미 반영됐다면 메모리를 되돌리면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(20));
            Assert.That(failingStore.BackupJson, Is.EqualTo(failingStore.Json));
            Assert.That(failingStore.BackupSyncPending, Is.False);
        }

        [Test]
        public void AppliedPrimaryWithReadFailureRestoresNewestPendingGeneration()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            _ = PermanentGrowthProfile.Currency;
            failingStore.ThrowOnPrimarySave = true;
            failingStore.ApplyPrimaryBeforeThrow = true;
            failingStore.ThrowOnPrimaryLoad = true;

            PermanentGrowthSettlement failed =
                PermanentGrowthProfile.SettleRun(
                    "applied-before-unreadable", 12, 500, 0, 20f, true);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.Not.EqualTo(original),
                "예외 전에 실제 primary에는 새 세대가 반영된 결함을 재현합니다.");

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnPrimaryLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            PermanentGrowthSettlement duplicate =
                PermanentGrowthProfile.SettleRun(
                    "applied-before-unreadable", 12, 500, 0, 20f, true);
            Assert.That(duplicate.Accepted, Is.False,
                "복구한 새 세대의 runId를 다시 지급하면 안 됩니다.");
        }

        [Test]
        public void PartialPrimaryWriteIsQuarantinedBeforeRecovery()
        {
            const string partial = "{\"schemaVersion\":1,\"wallet\":";
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            _ = PermanentGrowthProfile.Currency;
            failingStore.PrimaryJsonBeforeThrow = partial;
            failingStore.ThrowOnPrimarySave = true;

            PermanentGrowthSettlement failed =
                PermanentGrowthProfile.SettleRun(
                    "partial-primary", 12, 500, 0, 20f, true);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo(partial));

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(partial));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5),
                "동기화 목표 세대를 복구 후보로 보존해야 합니다.");
        }

        [Test]
        public void PrimaryAndBackupReadFailureStillRollsBackIntoRecovery()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
                ThrowOnPrimarySave = true,
                ThrowOnBackupLoad = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            PermanentGrowthSettlement failed =
                PermanentGrowthProfile.SettleRun(
                    "double-storage-failure", 12, 20, 0, 20f, true);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True,
                "검증된 primary 스냅샷을 메모리 복구 후보로 유지해야 합니다.");

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
        }

        [Test]
        public void PrimaryLoadFailureUsesBackupWithoutPoisoningProfileCache()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = CurrentSaveJson(9, "S00"),
                BackupJson = CurrentSaveJson(2, "I00"),
                ThrowOnPrimaryLoad = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().HasNode("ink.1"),
                Is.True,
                "첫 Load 예외 뒤에도 data가 null인 poisoned cache가 남으면 안 됩니다.");

            failingStore.ThrowOnPrimaryLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9),
                "일시적 Load 실패가 풀리면 최신 지원 primary를 우선 복구해야 합니다.");
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("body.1"), Is.True);
            Assert.That(failingStore.QuarantineJson,
                Is.EqualTo(CurrentSaveJson(9, "S00")));
        }

        [Test]
        public void MissingPrimaryAndBackupReadFailureCannotCreateFreshProfile()
        {
            string validBackup = CurrentSaveJson(3, "I00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = string.Empty,
                BackupJson = validBackup,
                ThrowOnBackupLoad = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.False);
            Assert.That(failingStore.Json, Is.Empty);
            Assert.That(failingStore.BackupJson, Is.EqualTo(validBackup));

            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
        }

        [Test]
        public void ResetQuarantinesInvalidBackupDiscoveredAfterReadRecovers()
        {
            const string futureBackup =
                "{\"schemaVersion\":1,\"balanceVersion\":99}";
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = CurrentSaveJson(0),
                BackupJson = futureBackup,
                ThrowOnPrimarySave = true,
                ThrowOnBackupLoad = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            PermanentGrowthSettlement failed =
                PermanentGrowthProfile.SettleRun(
                    "invalid-backup-recovery", 12, 20, 0, 20f, true);
            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(failingStore.BackupQuarantineJson,
                Is.EqualTo(futureBackup));
            Assert.That(failingStore.BackupJson,
                Does.Contain("\"balanceVersion\":11"));
        }

        [Test]
        public void MigrationWriteFailureKeepsOriginalAsRecoveryCandidate()
        {
            const string v2 =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":7,\"spent\":1," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"old-run\"," +
                "\"settledRunIds\":[\"old-run\"],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"I00\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"," +
                "\"unknownLegacyNote\":\"keep raw bytes\"}";
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = v2,
                ThrowOnPrimarySave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo(v2));
            Assert.That(failingStore.BackupSyncPending, Is.False,
                "원시 v2가 그대로 남았다면 새 세대 동기화 표식은 지워야 합니다.");

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnBackupLoad = true;
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8),
                "backup 확인 실패가 세션 데이터를 빈 프로필로 바꾸면 안 됩니다.");
            Assert.That(failingStore.Json, Is.EqualTo(v2));
            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(v2));
            Assert.That(failingStore.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
        }

        [Test]
        public void MigrationFailureResetQuarantinesPrimaryAndKeepsBackup()
        {
            const string v2 =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":7,\"spent\":1," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"old-run\"," +
                "\"settledRunIds\":[\"old-run\"],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"I00\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            string existingBackup = CurrentSaveJson(3, "S00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = v2,
                BackupJson = existingBackup,
                ThrowOnPrimarySave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(v2));
            Assert.That(failingStore.BackupJson, Is.EqualTo(v2),
                "이관 전에 보존한 원시 v2가 물리 복구본으로 남아야 합니다.");
        }

        [Test]
        public void BackupSaveFailureKeepsPrimaryAndResyncsOnReload()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
                ThrowOnBackupSave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "backup-failure-run", 12, 500, 0, 20f, true);

            Assert.That(settlement.Accepted, Is.True,
                "primary가 확정됐으면 backup 실패가 게임 정산을 중단하면 안 됩니다.");
            Assert.That(failingStore.Json, Is.Not.EqualTo(original));
            Assert.That(failingStore.BackupJson, Is.EqualTo(original));
            Assert.That(failingStore.BackupSyncPending, Is.True);

            failingStore.ThrowOnBackupSave = false;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            Assert.That(failingStore.BackupJson, Is.EqualTo(failingStore.Json));
            Assert.That(failingStore.BackupSyncPending, Is.False);
        }

        [Test]
        public void PendingTargetWinsOverStaleBackupAndPreservesDistanceTier()
        {
            string original = CurrentSaveJson(0);
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = original,
                BackupJson = original,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            PermanentGrowthSettlement first =
                PermanentGrowthProfile.SettleRun(
                    "distance-500", 0, 500, 0, 0f, true);
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Earned, Is.EqualTo(5));
            string target = failingStore.Json;
            int balanceAfterFirst = PermanentGrowthProfile.Currency;

            failingStore.Json = "{broken primary";
            failingStore.BackupJson = original;
            failingStore.BackupSyncTarget = target;
            failingStore.BackupSyncPending = true;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.Currency,
                Is.EqualTo(balanceAfterFirst));
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(500));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(5));

            PermanentGrowthSettlement next =
                PermanentGrowthProfile.SettleRun(
                    "distance-500", 0, 500, 500, 0f, true);
            Assert.That(next.Accepted, Is.False);
            Assert.That(next.Earned, Is.Zero);
            Assert.That(next.CumulativeDistanceMeters, Is.EqualTo(500));
            Assert.That(PermanentGrowthProfile.Currency,
                Is.EqualTo(balanceAfterFirst));
        }

        [Test]
        public void FuturePendingTargetIsPreservedReadOnly()
        {
            string primary = CurrentSaveJson(4, "I00");
            string backup = CurrentSaveJson(2, "I00");
            const string futureTarget =
                "{\"schemaVersion\":1,\"balanceVersion\":99," +
                "\"wallet\":9,\"ownedNodeIds\":[\"I-D1\"]}";
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = primary,
                BackupJson = backup,
                BackupSyncPending = true,
                BackupSyncTarget = futureTarget,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.FutureBalanceReadOnly));
            Assert.That(failingStore.Json, Is.EqualTo(primary));
            Assert.That(failingStore.BackupJson, Is.EqualTo(backup));
            Assert.That(failingStore.BackupSyncPending, Is.True);
            Assert.That(failingStore.BackupSyncTarget, Is.EqualTo(futureTarget));
        }

        [Test]
        public void ExplicitBackupRestoreQuarantinesRejectedPrimary()
        {
            const string corrupt = "{broken json";
            store.Json = corrupt;
            store.BackupJson = CurrentSaveJson(2, "I00");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(store.SaveCount, Is.Zero);

            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(store.QuarantineJson, Is.EqualTo(corrupt));
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.BackupJson, Is.EqualTo(store.Json));

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "after-restore", 12, 500, 0, 20f, true);
            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(3));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
        }

        [Test]
        public void RestoreSucceedsAfterPrimaryWhenBackupSyncFails()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = CurrentSaveJson(2, "I00"),
                ThrowOnBackupSave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(failingStore.BackupSyncPending, Is.True);

            failingStore.ThrowOnBackupSave = false;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(failingStore.BackupJson, Is.EqualTo(failingStore.Json));
            Assert.That(failingStore.BackupSyncPending, Is.False);
        }

        [Test]
        public void RecoveryResetSucceedsAfterPrimaryWhenBackupSyncFails()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = string.Empty,
                ThrowOnBackupSave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(failingStore.BackupSyncPending, Is.True);

            failingStore.ThrowOnBackupSave = false;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(failingStore.BackupJson, Is.EqualTo(failingStore.Json));
            Assert.That(failingStore.BackupSyncPending, Is.False);
        }

        [Test]
        public void InterruptedResetPreservesBackupAndCompletesOnReload()
        {
            string validBackup = CurrentSaveJson(2, "I00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = validBackup,
                BackupSyncPending = true,
                ThrowOnPrimarySave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));

            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.False);
            Assert.That(failingStore.Json, Is.EqualTo("{broken json"));
            Assert.That(failingStore.BackupJson, Is.EqualTo(validBackup));
            Assert.That(failingStore.BackupSyncPending, Is.False);
            Assert.That(failingStore.ResetPending, Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(failingStore.BackupJson, Is.EqualTo(validBackup));
            Assert.That(failingStore.BackupSyncPending, Is.False);
            Assert.That(failingStore.ResetPending, Is.False);
        }

        [Test]
        public void BackupRestoreCancelsPreviouslyInterruptedReset()
        {
            string validBackup = CurrentSaveJson(2, "I00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = validBackup,
                ThrowOnPrimarySave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.False);
            Assert.That(failingStore.ResetPending, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(failingStore.ResetPending, Is.False);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
        }

        [Test]
        public void RestoreIntentSurvivesResetMarkerClearFailure()
        {
            string validBackup = CurrentSaveJson(2, "I00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = validBackup,
                ThrowOnPrimarySave = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.False);
            Assert.That(failingStore.ResetPending, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnResetPendingClear = true;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.False);
            Assert.That(failingStore.ResetPending, Is.True);
            Assert.That(failingStore.BackupSyncPending, Is.True,
                "reset marker보다 복원 목표를 먼저 내구 기록해야 합니다.");
            Assert.That(failingStore.BackupSyncTarget, Is.Not.Empty);

            failingStore.ThrowOnResetPendingClear = false;
            failingStore.ThrowOnPendingLoad = true;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo("{broken json"),
                "복원 목표를 한 번 못 읽었다고 빈 초기화를 확정하면 안 됩니다.");
            Assert.That(failingStore.ResetPending, Is.True);

            failingStore.ThrowOnPendingLoad = false;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True,
                "다음 로드는 빈 초기화보다 사용자의 복원 의도를 우선해야 합니다.");
            Assert.That(failingStore.ResetPending, Is.False);
            Assert.That(failingStore.BackupSyncPending, Is.False);
        }

        [Test]
        public void InvalidRestoreTargetCannotBlankResetAndPhysicalBackupRemainsRestorable()
        {
            string validBackup = CurrentSaveJson(2, "I00");
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken primary",
                BackupJson = validBackup,
                ResetPending = true,
                BackupSyncPending = true,
                BackupSyncTarget = "{broken restore target",
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo("{broken primary"));
            Assert.That(failingStore.ResetPending, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
        }

        [Test]
        public void InvalidPendingTargetWithoutBackupCanBeExplicitlyReset()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken primary",
                BackupJson = string.Empty,
                ResetPending = true,
                BackupSyncPending = true,
                BackupSyncTarget = "{broken restore target",
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.False);
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True,
                "자동 초기화는 금지하되 사용자의 확인된 초기화는 막으면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(failingStore.BackupQuarantineJson,
                Is.EqualTo("{broken restore target"));
            Assert.That(failingStore.ResetPending, Is.False,
                "명시 초기화가 끝난 뒤 reset marker가 남으면 안 됩니다.");
        }

        [Test]
        public void MissingPrimaryFindsBackupAfterPendingReadRecovers()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = string.Empty,
                BackupJson = CurrentSaveJson(2, "I00"),
                ThrowOnPendingLoad = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            failingStore.ThrowOnPendingLoad = false;
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
        }

        [Test]
        public void NewSettlementCannotPassWhileResetMarkerClearFails()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = "{broken json",
                BackupJson = CurrentSaveJson(2, "I00"),
                ThrowOnResetPendingClear = true,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));

            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(failingStore.ResetPending, Is.True,
                "마지막 marker clear 실패를 주입한 상태입니다.");

            PermanentGrowthSettlement blocked =
                PermanentGrowthProfile.SettleRun(
                    "must-not-be-erased", 0, 0, 0, 0f, true);
            Assert.That(blocked.Accepted, Is.False,
                "reset intent를 내구 취소하지 못한 채 새 보상을 성공 처리하면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnResetPendingClear = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True,
                "marker 정리 실패 뒤에는 빈 rollback보다 보존 backup을 우선해야 합니다.");
            PermanentGrowthSettlement retry =
                PermanentGrowthProfile.SettleRun(
                    "must-not-be-erased", 0, 0, 0, 0f, true);
            Assert.That(retry.Accepted, Is.True);
            Assert.That(retry.Earned, Is.Zero,
                "0m 판은 복원한 기존 누적 거리에서 새 문턱을 만들지 않습니다.");
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
        }

        [Test]
        public void ResetMarkerReadFailurePrefersPreservedPhysicalBackup()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = CurrentSaveJson(0),
                BackupJson = CurrentSaveJson(2, "I00"),
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);

            failingStore.ResetPending = true;
            failingStore.ThrowOnResetPendingLoad = true;
            PermanentGrowthSettlement failed = PermanentGrowthProfile.SettleRun(
                "reset-marker-read-failure", 0, 0, 0, 0f, true);
            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnResetPendingLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
        }

        [Test]
        public void ResetMarkerReadFailureWithoutBackupRestoresValidatedPrimary()
        {
            const string v2 =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":7,\"spent\":1," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                "\"ranks\":[],\"ownedNodeIds\":[\"I00\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = v2,
                BackupJson = string.Empty,
                ThrowOnResetPendingLoadCall = 2,
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True,
                "물리 backup이 없어도 검증된 v2 원본을 복구 후보로 제공해야 합니다.");
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
        }

        [Test]
        public void ResetAndBackupReadFailureStillPrefersPhysicalBackupAfterRecovery()
        {
            var failingStore = new FailingPrimaryGrowthStore
            {
                Json = CurrentSaveJson(0),
                BackupJson = CurrentSaveJson(2, "I00"),
            };
            PermanentGrowthProfile.UseStoreForTests(failingStore);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);

            failingStore.ResetPending = true;
            failingStore.ThrowOnResetPendingLoad = true;
            failingStore.ThrowOnBackupLoad = true;
            PermanentGrowthSettlement failed = PermanentGrowthProfile.SettleRun(
                "reset-and-backup-read-failure", 0, 0, 0, 0f, true);
            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnResetPendingLoad = false;
            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
        }

        [Test]
        public void MissingPrimaryRestoreKeepsPreviousQuarantine()
        {
            const string firstCorrupt = "{first broken json";
            store.Json = firstCorrupt;
            store.BackupJson = CurrentSaveJson(2, "I00");
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(store.QuarantineJson, Is.EqualTo(firstCorrupt));

            store.Json = string.Empty;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);

            Assert.That(store.QuarantineJson, Is.EqualTo(firstCorrupt),
                "격리할 primary가 없으면 이전 손상 원본을 빈 값으로 지우면 안 됩니다.");
        }

        [Test]
        public void WhitespacePrimaryRestoreKeepsPreviousQuarantine()
        {
            const string firstCorrupt = "{first broken json";
            store.Json = firstCorrupt;
            store.BackupJson = CurrentSaveJson(2, "I00");
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(store.QuarantineJson, Is.EqualTo(firstCorrupt));

            store.Json = "   \t\n";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);

            Assert.That(store.QuarantineJson, Is.EqualTo(firstCorrupt),
                "공백뿐인 primary도 기존 격리 원본을 덮어쓰면 안 됩니다.");
        }

        [Test]
        public void ExistingCanonicalPrimaryBackfillsBackupWithoutRewritingPrimary()
        {
            string primary = CurrentSaveJson(4, "S00");
            store.Json = primary;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(4));
            Assert.That(store.Json, Is.EqualTo(primary));
            Assert.That(store.SaveCount, Is.Zero,
                "정상 primary 조회는 primary를 다시 쓰지 않습니다.");
            Assert.That(store.BackupJson, Is.EqualTo(primary));
            Assert.That(store.BackupSaveCount, Is.EqualTo(1));

            store.Json = "{later broken json";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(4));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("body.1"), Is.True);
        }

        [Test]
        public void MissingPrimaryWithInvalidBackupCannotStartWritableProfile()
        {
            const string futureBackup =
                "{\"schemaVersion\":1,\"balanceVersion\":12}";
            store.Json = string.Empty;
            store.BackupJson = futureBackup;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.False);
            Assert.That(store.BackupJson, Is.EqualTo(futureBackup));
            Assert.That(store.SaveCount, Is.Zero);

            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(store.BackupQuarantineJson, Is.EqualTo(futureBackup));
        }

        [Test]
        public void InvalidBackupCannotReplaceRejectedPrimary()
        {
            const string corrupt = "{broken json";
            store.Json = corrupt;
            store.BackupJson =
                "{\"schemaVersion\":1,\"balanceVersion\":99}";
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.False);
            Assert.That(store.Json, Is.EqualTo(corrupt));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.QuarantineSaveCount, Is.Zero);
        }

        [Test]
        public void RecoveryResetQuarantinesPrimaryAndKeepsExistingBackup()
        {
            const string corrupt = "{broken json";
            string backup = CurrentSaveJson(3, "S00");
            store.Json = corrupt;
            store.BackupJson = backup;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(store.QuarantineJson, Is.EqualTo(corrupt));
            Assert.That(store.BackupJson, Is.EqualTo(backup));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "after-reset", 0, 500, 0, 0f, true);
            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(5));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
        }

        [Test]
        public void LoadedWalletCannotExceedCatalogBudget()
        {

            SeedV2(39 * 10, "I00", "S00");
            Assert.That(
                PermanentGrowthProfile.Currency +
                PermanentGrowthProfile.SpentCurrency,
                Is.EqualTo(39));
        }

        [Test]
        public void ResettingRunRefreshesPermanentSnapshotWithoutChangingProfile()
        {
            store.Json = CurrentSaveJson(0, "ink.1");
            PermanentGrowthProfile.ResetCacheForTests();
            var host = new GameObject("RunGrowthSeparationTest");
            try
            {
                var runGrowth = host.AddComponent<RunGrowthController>();
                typeof(RunGrowthController).GetMethod(
                        "ResetRun",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(runGrowth, null);

                Assert.That(runGrowth.PermanentSnapshot.HasNode("ink.1"), Is.True);
                Assert.That(PermanentGrowthProfile.IsNodeUnlocked("ink.1"), Is.True);
                Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        void SeedV2(int wallet, params string[] ownedNodeIds)
        {
            string owned = ownedNodeIds == null
                ? string.Empty
                : string.Join("\",\"", ownedNodeIds);
            string ownedJson = string.IsNullOrEmpty(owned)
                ? "[]"
                : $"[\"{owned}\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                $"\"wallet\":{wallet},\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                $"\"ownedNodeIds\":{ownedJson}," +
                "\"ranks\":[],\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }

        static string CurrentSaveJson(int wallet, params string[] ownedNodeIds)
        {
            string[] currentIds = ownedNodeIds == null
                ? System.Array.Empty<string>()
                : new string[ownedNodeIds.Length];
            int spent = 0;
            for (int i = 0; i < currentIds.Length; i++)
            {
                currentIds[i] = MapCurrentNodeId(ownedNodeIds[i]);
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(currentIds[i]);
                if (node != null)
                    spent += node.Cost;
            }
            int grantedRewardCount = Mathf.Clamp(
                wallet + spent,
                0,
                RunRewardCalculator.MaxRewardCount);
            long cumulativeDistance =
                RunRewardCalculator.GetThresholdForRewardCount(
                    grantedRewardCount);
            return BuildSaveJson(
                11,
                wallet,
                cumulativeDistance,
                grantedRewardCount,
                true,
                currentIds);
        }

        static string MapCurrentNodeId(string nodeId) => nodeId switch
        {
            "I00" => "ink.1",
            "S00" => "body.1",
            "J00" => "jump.1",
            _ => nodeId,
        };

        static string LegacyV6SaveJson(
            int wallet,
            params string[] ownedNodeIds) =>
            BuildSaveJson(6, wallet, 0L, 0, false, ownedNodeIds);

        static string BuildSaveJson(
            int balanceVersion,
            int wallet,
            long cumulativeDistance,
            int claimedRewardCount,
            bool includeDistanceJourney,
            params string[] ownedNodeIds)
        {
            string owned = ownedNodeIds == null || ownedNodeIds.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", ownedNodeIds) + "\"]";
            string distanceJourney = includeDistanceJourney
                ? $"\"cumulativeDistanceMeters\":{cumulativeDistance}," +
                  $"\"claimedDistanceRewardCount\":{claimedRewardCount},"
                : string.Empty;
            int spent = ownedNodeIds?.Length ?? 0;
            if (balanceVersion >= 8 && ownedNodeIds != null)
            {
                spent = 0;
                for (int i = 0; i < ownedNodeIds.Length; i++)
                {
                    PermanentGrowthNodeDefinition node =
                        PermanentGrowthCatalog.GetNode(ownedNodeIds[i]);
                    if (node != null)
                        spent += node.Cost;
                }
            }
            return
                $"{{\"schemaVersion\":1,\"balanceVersion\":{balanceVersion}," +
                $"\"wallet\":{wallet},\"spent\":{spent}," +
                "\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":false," +
                "\"rewardedBestHeight\":0," +
                distanceJourney +
                (balanceVersion >= 10 ? "\"distanceRewardOffsetMeters\":0," : string.Empty) +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                "\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
        }

        sealed class FailingPrimaryGrowthStore : IPermanentGrowthRecoveryStore
        {
            public string Json { get; set; } = string.Empty;
            public string BackupJson { get; set; } = string.Empty;
            public bool ThrowOnPrimaryLoad { get; set; }
            public bool ThrowOnPrimarySave { get; set; }
            public bool ApplyPrimaryBeforeThrow { get; set; }
            public string PrimaryJsonBeforeThrow { get; set; }
            public bool ThrowOnBackupSave { get; set; }
            public bool ThrowOnBackupLoad { get; set; }
            public bool ThrowOnPendingClear { get; set; }
            public bool ThrowOnPendingLoad { get; set; }
            public bool ThrowOnResetPendingLoad { get; set; }
            public int ThrowOnResetPendingLoadCall { get; set; }
            public bool ThrowOnResetPendingClear { get; set; }
            int resetPendingLoadCount;
            public int BackupSaveCount { get; private set; }
            public bool BackupSyncPending { get; set; }
            public string BackupSyncTarget { get; set; } = string.Empty;
            public bool ResetPending { get; set; }
            public string QuarantineJson { get; private set; } = string.Empty;
            public string BackupQuarantineJson { get; private set; } = string.Empty;

            public string Load()
            {
                if (ThrowOnPrimaryLoad)
                    throw new System.InvalidOperationException(
                        "Injected primary read failure");
                return Json;
            }

            public void Save(string json)
            {
                if (ThrowOnPrimarySave)
                {
                    if (PrimaryJsonBeforeThrow != null)
                        Json = PrimaryJsonBeforeThrow;
                    else if (ApplyPrimaryBeforeThrow)
                        Json = json ?? string.Empty;
                    throw new System.InvalidOperationException(
                        "Injected primary write failure");
                }
                Json = json ?? string.Empty;
            }

            public string LoadBackup()
            {
                if (ThrowOnBackupLoad)
                    throw new System.InvalidOperationException(
                        "Injected backup read failure");
                return BackupJson;
            }

            public void SaveBackup(string json)
            {
                if (ThrowOnBackupSave)
                    throw new System.InvalidOperationException(
                        "Injected backup write failure");
                BackupJson = json ?? string.Empty;
                BackupSaveCount++;
            }

            public void SaveQuarantine(string json)
            {
                QuarantineJson = json ?? string.Empty;
            }

            public void SaveBackupQuarantine(string json)
            {
                BackupQuarantineJson = json ?? string.Empty;
            }

            public bool LoadBackupSyncPending()
            {
                if (ThrowOnPendingLoad)
                    throw new System.InvalidOperationException(
                        "Injected pending read failure");
                return BackupSyncPending;
            }

            public void SaveBackupSyncPending(bool pending)
            {
                if (!pending && ThrowOnPendingClear)
                    throw new System.InvalidOperationException(
                        "Injected pending clear failure");
                BackupSyncPending = pending;
            }

            public string LoadBackupSyncTarget()
            {
                if (ThrowOnPendingLoad)
                    throw new System.InvalidOperationException(
                        "Injected pending target read failure");
                return BackupSyncTarget;
            }

            public void SaveBackupSyncTarget(string json)
            {
                BackupSyncTarget = json ?? string.Empty;
            }

            public bool LoadResetPending()
            {
                resetPendingLoadCount++;
                if (ThrowOnResetPendingLoad ||
                    ThrowOnResetPendingLoadCall > 0 &&
                    resetPendingLoadCount == ThrowOnResetPendingLoadCall)
                    throw new System.InvalidOperationException(
                        "Injected reset pending read failure");
                return ResetPending;
            }

            public void SaveResetPending(bool pending)
            {
                if (!pending && ThrowOnResetPendingClear)
                    throw new System.InvalidOperationException(
                        "Injected reset pending clear failure");
                ResetPending = pending;
            }
        }
    }
}
