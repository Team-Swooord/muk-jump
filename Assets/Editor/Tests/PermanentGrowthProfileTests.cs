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
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().OwnedNodeCount,
                Is.Zero);
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

        [TestCase(0, 0f, false, 0)]
        [TestCase(11, 120f, false, 0)]
        [TestCase(12, 19.99f, false, 0)]
        [TestCase(12, 20f, false, 1)]
        [TestCase(0, 0f, true, 1)]
        public void BaseRewardUsesFirstRunOrTwelveMetersAndTwentyActiveSeconds(
            int swarmHeight,
            float activeSeconds,
            bool firstEligible,
            int expected)
        {
            Assert.That(
                RunRewardCalculator.Calculate(
                    swarmHeight,
                    0,
                    0,
                    activeSeconds,
                    firstEligible),
                Is.EqualTo(expected));
        }

        [TestCase(99, 0, 0)]
        [TestCase(100, 99, 1)]
        [TestCase(250, 99, 2)]
        [TestCase(1000, 0, 5)]
        [TestCase(1000, 750, 1)]
        [TestCase(1000, 1000, 0)]
        public void RewardAddsEveryNewBestMilestone(
            int scoreHeight,
            int previousBest,
            int expectedMilestones)
        {
            Assert.That(
                RunRewardCalculator.Calculate(
                    0,
                    scoreHeight,
                    previousBest,
                    0f,
                    false),
                Is.EqualTo(expectedMilestones));
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
            Assert.That(first.Earned, Is.EqualTo(3),
                "첫 정상 판 1개와 최초 100m·250m 이정표 2개가 합산되어야 합니다.");
            Assert.That(first.Balance, Is.EqualTo(3));

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
        }

        [Test]
        public void SettlementHistoryRejectsOlderRunAfterAnotherRunSettles()
        {
            PermanentGrowthSettlement first = PermanentGrowthProfile.SettleRun(
                "run-A", 0, 0, 0, 0f, true);
            PermanentGrowthSettlement second = PermanentGrowthProfile.SettleRun(
                "run-B", 12, 0, 0, 20f, true);

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
        public void IneligibleRunPaysNothingAndDoesNotConsumeFirstReward()
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
                0,
                0,
                0f,
                true);

            Assert.That(debug.Accepted, Is.True);
            Assert.That(debug.Earned, Is.Zero);
            Assert.That(firstReal.Earned, Is.EqualTo(1));
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
                Is.EqualTo(1.03f).Within(0.0001f));
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
        public void CorruptSaveRecoversAndWalletCannotExceedCatalogBudget()
        {
            store.Json = "{broken json";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);

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
                "\"lastSettledRunId\":\"\"," +
                $"\"ownedNodeIds\":{ownedJson}," +
                "\"ranks\":[],\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }
    }
}
