using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthProfileTests
    {
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
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
                Is.EqualTo(20));
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().OwnedNodeCount,
                Is.Zero);
            Assert.That(store.SaveCount, Is.Zero,
                "빈 프로필 조회만으로 저장을 만들면 안 됩니다.");
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void DebugResetClearsNodesAndUsesSessionOnly999Currency()
        {
            SeedV2(PermanentGrowthCatalog.TotalCost, "I00", "S00");
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

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero,
                "999 먹빛은 저장 재화가 아니라 현재 개발 세션 전용이어야 합니다.");
            Assert.That(PermanentGrowthProfile.IsDebugCurrencyActive, Is.False);
        }

        [Test]
        public void DebugRefillDeductsOnePerPurchasedNodeAndCanRefillAgain()
        {
            PermanentGrowthProfile.DebugRefillCurrency();

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(998));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));

            PermanentGrowthProfile.DebugRefillCurrency();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
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
        public void DistanceRewardThresholdsMatchThirtyNineNodeJourney(
            int rewardCount,
            long expectedDistance)
        {
            Assert.That(
                RunRewardCalculator.GetThresholdForRewardCount(rewardCount),
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
        [TestCase(100000, 39)]
        public void DistanceReturnsEveryCrossedRewardCount(
            long cumulativeDistance,
            int expectedRewardCount)
        {
            Assert.That(
                RunRewardCalculator.GetRewardCountForDistance(
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
            store.Json = CurrentSaveJson(38);
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
            Assert.That(settlement.Balance, Is.EqualTo(39));
            Assert.That(settlement.DistanceJourneyComplete, Is.True);
            Assert.That(settlement.CumulativeDistanceMeters, Is.EqualTo(4600));
        }

        [Test]
        public void LegacyProgressMigratesToEquivalentDistanceTier()
        {
            store.Json = LegacyV6SaveJson(3, "I00", "I-A1");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(100));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
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
            Assert.That(first.Earned, Is.EqualTo(8));
            Assert.That(first.Balance, Is.EqualTo(8));
            Assert.That(first.RunDistanceMeters, Is.EqualTo(250));
            Assert.That(first.CumulativeDistanceMeters, Is.EqualTo(250));
            Assert.That(first.PreviousRewardDistanceMeters, Is.EqualTo(250));
            Assert.That(first.NextRewardDistanceMeters, Is.EqualTo(300));

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
            Assert.That(duplicate.Balance, Is.EqualTo(8));
            Assert.That(duplicate.CumulativeDistanceMeters, Is.EqualTo(250));
        }

        [Test]
        public void SettlementHistoryRejectsOlderRunAfterAnotherRunSettles()
        {
            PermanentGrowthSettlement first = PermanentGrowthProfile.SettleRun(
                "run-A", 0, 20, 0, 0f, true);
            PermanentGrowthSettlement second = PermanentGrowthProfile.SettleRun(
                "run-B", 12, 30, 20, 20f, true);

            PermanentGrowthProfile.ResetCacheForTests();
            PermanentGrowthSettlement repeatedFirst =
                PermanentGrowthProfile.SettleRun(
                    "run-A", 1000, 1000, 0, 999f, true);

            Assert.That(first.Earned, Is.EqualTo(1));
            Assert.That(second.Earned, Is.EqualTo(1));
            Assert.That(repeatedFirst.Accepted, Is.False);
            Assert.That(repeatedFirst.Earned, Is.Zero);
            Assert.That(repeatedFirst.Balance, Is.EqualTo(2));
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
            Assert.That(firstReal.Earned, Is.EqualTo(1));
            Assert.That(firstReal.CumulativeDistanceMeters, Is.EqualTo(20));
        }

        [Test]
        public void PurchaseCostsExactlyOneAndOnlyUnlocksSelectedNode()
        {
            SeedV2(4);
            int changedCount = 0;
            PermanentGrowthProfile.Changed += () => changedCount++;

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.True);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("S00"), Is.False);
            Assert.That(PermanentGrowthProfile.InkCapacityMultiplier,
                Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDuplicateAndInsufficientPurchasesDoNotMutateSave()
        {
            SeedV2(1);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.True);
            int saveCount = store.SaveCount;

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I-A1"), Is.False);
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
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.False);
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
                "{\"schemaVersion\":1,\"balanceVersion\":8," +
                "\"wallet\":9,\"ownedNodeIds\":[\"I-D1\"]," +
                "\"inkHandlingKeystoneId\":\"I-D1\"}";
            store.Json = future;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.LoadState,
                Is.EqualTo(PermanentGrowthLoadState.FutureBalanceReadOnly));
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.False);
            Assert.That(store.Json, Is.EqualTo(future));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BackupSaveCount, Is.Zero);
        }

        [Test]
        public void HeaderOnlyCurrentSaveCannotOverwriteValidBackup()
        {
            const string truncated =
                "{\"schemaVersion\":1,\"balanceVersion\":7}";
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
            string[] invalidPrimaries =
            {
                valid.Replace("\"cumulativeDistanceMeters\":100,", ""),
                valid.Replace("\"claimedDistanceRewardCount\":5,", ""),
                valid.Replace(
                    "\"claimedDistanceRewardCount\":5",
                    "\"claimedDistanceRewardCount\":4"),
                valid.Replace(
                    "\"cumulativeDistanceMeters\":100",
                    "\"cumulativeDistanceMeters\":-1"),
                valid.Replace(
                    "\"claimedDistanceRewardCount\":5",
                    "\"claimedDistanceRewardCount\":40"),
            };

            for (int i = 0; i < invalidPrimaries.Length; i++)
            {
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
            store.Json = CurrentSaveJson(3, "I00", "I-A1")
                .Replace("\"balanceVersion\":7", "\"balanceVersion\":5");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I-A1"), Is.True);
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(5));
        }

        [Test]
        public void VersionSixProgressMigratesToMatchingDistanceThreshold()
        {
            store.Json = LegacyV6SaveJson(5, "I00");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(6));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(150));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
        }

        [Test]
        public void VersionFiveInvalidOwnedGraphCannotOverwriteValidBackup()
        {
            string backup = CurrentSaveJson(5, "I00");
            string[] invalidPrimaries =
            {
                CurrentSaveJson(3, "I-A1")
                    .Replace("\"balanceVersion\":7", "\"balanceVersion\":5"),
                CurrentSaveJson(3, "I00", "I00")
                    .Replace("\"balanceVersion\":7", "\"balanceVersion\":5"),
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
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
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
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
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
                    "applied-before-unreadable", 12, 20, 0, 20f, true);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.Not.EqualTo(original),
                "예외 전에 실제 primary에는 새 세대가 반영된 결함을 재현합니다.");

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnPrimaryLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
            PermanentGrowthSettlement duplicate =
                PermanentGrowthProfile.SettleRun(
                    "applied-before-unreadable", 12, 20, 0, 20f, true);
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
                    "partial-primary", 12, 20, 0, 20f, true);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo(partial));

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(partial));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1),
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
                PermanentGrowthProfile.CreateRunSnapshot().HasNode("I00"),
                Is.True,
                "첫 Load 예외 뒤에도 data가 null인 poisoned cache가 남으면 안 됩니다.");

            failingStore.ThrowOnPrimaryLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9),
                "일시적 Load 실패가 풀리면 최신 지원 primary를 우선 복구해야 합니다.");
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("S00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.False);
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
                Does.Contain("\"balanceVersion\":7"));
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

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.CanRestoreBackup, Is.True);
            Assert.That(failingStore.Json, Is.EqualTo(v2));
            Assert.That(failingStore.BackupSyncPending, Is.False,
                "원시 v2가 그대로 남았다면 새 세대 동기화 표식은 지워야 합니다.");

            failingStore.ThrowOnPrimarySave = false;
            failingStore.ThrowOnBackupLoad = true;
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7),
                "backup 확인 실패가 세션 데이터를 빈 프로필로 바꾸면 안 됩니다.");
            Assert.That(failingStore.Json, Is.EqualTo(v2));
            failingStore.ThrowOnBackupLoad = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(v2));
            Assert.That(failingStore.Json, Does.Contain("\"balanceVersion\":7"));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);

            failingStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryResetAfterLoadFailure(), Is.True);
            Assert.That(failingStore.QuarantineJson, Is.EqualTo(v2));
            Assert.That(failingStore.BackupJson, Is.EqualTo(existingBackup));
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
                    "backup-failure-run", 12, 20, 0, 20f, true);

            Assert.That(settlement.Accepted, Is.True,
                "primary가 확정됐으면 backup 실패가 게임 정산을 중단하면 안 됩니다.");
            Assert.That(failingStore.Json, Is.Not.EqualTo(original));
            Assert.That(failingStore.BackupJson, Is.EqualTo(original));
            Assert.That(failingStore.BackupSyncPending, Is.True);

            failingStore.ThrowOnBackupSave = false;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
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
            Assert.That(first.Earned, Is.EqualTo(13));
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
                Is.EqualTo(13));

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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "after-restore", 12, 20, 0, 20f, true);
            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(1));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True,
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True,
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("S00"), Is.True);
        }

        [Test]
        public void MissingPrimaryWithInvalidBackupCannotStartWritableProfile()
        {
            const string futureBackup =
                "{\"schemaVersion\":1,\"balanceVersion\":9}";
            store.Json = string.Empty;
            store.BackupJson = futureBackup;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.False);
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
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "after-reset", 0, 20, 0, 0f, true);
            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(1));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
        }

        [Test]
        public void LoadedWalletCannotExceedCatalogBudget()
        {

            SeedV2(PermanentGrowthCatalog.TotalCost * 10, "I00", "S00");
            Assert.That(
                PermanentGrowthProfile.Currency +
                PermanentGrowthProfile.SpentCurrency,
                Is.EqualTo(PermanentGrowthCatalog.TotalCost));
        }

        [Test]
        public void ResettingRunRefreshesPermanentSnapshotWithoutChangingProfile()
        {
            SeedV2(0, "I00");
            var host = new GameObject("RunGrowthSeparationTest");
            try
            {
                var runGrowth = host.AddComponent<RunGrowthController>();
                typeof(RunGrowthController).GetMethod(
                        "ResetRun",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(runGrowth, null);

                Assert.That(runGrowth.PermanentSnapshot.HasNode("I00"), Is.True);
                Assert.That(PermanentGrowthProfile.IsNodeUnlocked("I00"), Is.True);
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
            int ownedCount = ownedNodeIds?.Length ?? 0;
            int grantedRewardCount = Mathf.Clamp(
                wallet + ownedCount,
                0,
                RunRewardCalculator.MaxRewardCount);
            long cumulativeDistance =
                RunRewardCalculator.GetThresholdForRewardCount(
                    grantedRewardCount);
            return BuildSaveJson(
                7,
                wallet,
                cumulativeDistance,
                grantedRewardCount,
                true,
                ownedNodeIds);
        }

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
            return
                $"{{\"schemaVersion\":1,\"balanceVersion\":{balanceVersion}," +
                $"\"wallet\":{wallet},\"spent\":{ownedNodeIds?.Length ?? 0}," +
                "\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":false," +
                "\"rewardedBestHeight\":0," +
                distanceJourney +
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
