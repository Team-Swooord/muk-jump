using System.Linq;
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
        public void FreshProfileStartsWithZeroCurrencyAndLevels()
        {
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            foreach (PermanentGrowthDefinition definition in PermanentGrowthCatalog.All)
                Assert.That(
                    PermanentGrowthProfile.GetLevel(definition.Type),
                    Is.Zero);
        }

        [Test]
        public void DebugResetClearsGrowthAndStartsSessionWith999Currency()
        {
            SeedFullWallet();
            PermanentGrowthNodeDefinition root =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.InkCapacity,
                    1);
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(root),
                Is.True);
            int changedCount = 0;
            PermanentGrowthProfile.Changed += () => changedCount++;

            PermanentGrowthProfile.DebugResetProgress();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.IsDebugCurrencyActive, Is.True);
            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
                Assert.That(
                    PermanentGrowthProfile.GetLevel(definition.Type),
                    Is.Zero,
                    definition.Id);
            Assert.That(store.Json, Does.Contain("\"wallet\":0"));
            Assert.That(store.Json, Does.Contain("\"ranks\":[]"));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void DebugRefillUsesSessionCurrencyAndPurchaseDeductsFromIt()
        {
            PermanentGrowthProfile.DebugRefillCurrency();
            PermanentGrowthNodeDefinition root =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.InkCapacity,
                    1);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(root),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.Currency,
                Is.EqualTo(999 - root.Cost));
            Assert.That(
                PermanentGrowthProfile.SpentCurrency,
                Is.EqualTo(root.Cost));

            PermanentGrowthProfile.DebugRefillCurrency();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));
        }

        [Test]
        public void ResettingCacheClearsDebugSessionCurrency()
        {
            PermanentGrowthProfile.DebugRefillCurrency();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(999));

            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.IsDebugCurrencyActive, Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(9, 0)]
        [TestCase(10, 4)]
        [TestCase(30, 7)]
        [TestCase(50, 9)]
        [TestCase(100, 12)]
        [TestCase(200, 17)]
        [TestCase(500, 29)]
        [TestCase(1000, 40)]
        public void BaseRewardFollowsDocumentedHeightCurve(int height, int expected)
        {
            Assert.That(
                RunRewardCalculator.Calculate(height, height, false),
                Is.EqualTo(expected));
        }

        [Test]
        public void SettlementPersistsAndSameRunCannotPayTwiceAfterCacheReload()
        {
            PermanentGrowthSettlement first = PermanentGrowthProfile.SettleRun(
                "run-001",
                100,
                0,
                true);
            int savedBalance = first.Balance;

            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Earned, Is.EqualTo(22),
                "100m 기본 12 + 첫 성장 6 + 신규 50m 구간 두 칸 4입니다.");
            Assert.That(savedBalance, Is.EqualTo(22));

            PermanentGrowthProfile.ResetCacheForTests();
            PermanentGrowthSettlement duplicate = PermanentGrowthProfile.SettleRun(
                "run-001",
                100,
                0,
                true);

            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.Earned, Is.Zero);
            Assert.That(duplicate.Balance, Is.EqualTo(savedBalance));
        }

        [Test]
        public void DebugRunPaysNothingAndDoesNotConsumeFirstGrowthReward()
        {
            PermanentGrowthSettlement debug = PermanentGrowthProfile.SettleRun(
                "debug-run",
                1000,
                0,
                false);
            PermanentGrowthSettlement firstReal = PermanentGrowthProfile.SettleRun(
                "real-run",
                0,
                0,
                true);

            Assert.That(debug.Earned, Is.Zero);
            Assert.That(debug.Balance, Is.Zero);
            Assert.That(firstReal.Earned, Is.EqualTo(
                RunRewardCalculator.TutorialReward));
        }

        [Test]
        public void PurchaseDeductsCostAndRaisesOnlySelectedPermanentLevel()
        {
            SeedFullWallet();
            int cost = PermanentGrowthProfile.GetNextCost(
                PermanentGrowthType.InkCapacity);
            int before = PermanentGrowthProfile.Currency;
            int changedCount = 0;
            PermanentGrowthProfile.Changed += () => changedCount++;

            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkCapacity),
                Is.True);

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(1));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.Zero);
            Assert.That(PermanentGrowthProfile.Currency,
                Is.EqualTo(before - cost));
            Assert.That(PermanentGrowthProfile.SpentCurrency,
                Is.EqualTo(cost));
            Assert.That(PermanentGrowthProfile.InkCapacityMultiplier,
                Is.EqualTo(1.015f).Within(0.0001f));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientAndMaxLevelPurchasesDoNotMutate()
        {
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkRecovery),
                Is.False);
            Assert.That(store.SaveCount, Is.Zero);

            SeedFullWallet();
            var definition = PermanentGrowthCatalog.Get(
                PermanentGrowthType.JumpCharge);
            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes.OrderBy(node => node.LayoutY))
            {
                Assert.That(
                    PermanentGrowthProfile.TryPurchaseNode(node),
                    Is.True,
                    node.Id);
            }

            int balance = PermanentGrowthProfile.Currency;
            int spent = PermanentGrowthProfile.SpentCurrency;
            int saveCount = store.SaveCount;
            Assert.That(
                PermanentGrowthProfile.TryPurchase(definition.Type),
                Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(balance));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(spent));
            Assert.That(store.SaveCount, Is.EqualTo(saveCount));
            Assert.That(PermanentGrowthProfile.JumpChargeMultiplier,
                Is.EqualTo(0.955f).Within(0.0001f));
        }

        [Test]
        public void CorruptAndOldBalanceSavesRecoverWithoutThrowing()
        {
            store.Json = "{broken json";
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);

            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":0," +
                "\"wallet\":5,\"spent\":12,\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"ranks\":[]}";
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(17));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            foreach (PermanentGrowthDefinition definition in PermanentGrowthCatalog.All)
                Assert.That(
                    PermanentGrowthProfile.GetLevel(definition.Type),
                    Is.Zero);
        }

        [Test]
        public void WalletAndSpentCanNeverExceedFiniteCatalogCost()
        {
            SeedFullWallet(PermanentGrowthCatalog.TotalCost * 10);

            Assert.That(
                PermanentGrowthProfile.Currency +
                PermanentGrowthProfile.SpentCurrency,
                Is.EqualTo(PermanentGrowthCatalog.TotalCost));
        }

        [Test]
        public void ResettingRunScrollGrowthDoesNotResetPermanentGrowth()
        {
            SeedFullWallet();
            Assert.That(
                PermanentGrowthProfile.TryPurchase(
                    PermanentGrowthType.InkCapacity),
                Is.True);
            int balance = PermanentGrowthProfile.Currency;
            int saves = store.SaveCount;

            var host = new GameObject("RunGrowthSeparationTest");
            try
            {
                var runGrowth = host.AddComponent<RunGrowthController>();
                typeof(RunGrowthController).GetMethod(
                        "ResetRun",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(runGrowth, null);

                Assert.That(runGrowth.InkCapacityLevel, Is.Zero);
                Assert.That(
                    PermanentGrowthProfile.GetLevel(
                        PermanentGrowthType.InkCapacity),
                    Is.EqualTo(1));
                Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(balance));
                Assert.That(store.SaveCount, Is.EqualTo(saves),
                    "한 판 두루마리 초기화가 영구 저장을 쓰면 안 됩니다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void GameOverResultUsesAlreadySettledPermanentRewardExactlyOnce()
        {
            var scoreHost = new GameObject("PermanentGrowthScoreTest");
            var managerHost = new GameObject("PermanentGrowthManagerTest");
            try
            {
                var score = scoreHost.AddComponent<ScoreManager>();
                typeof(ScoreManager).GetMethod(
                        "OnEnable",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(score, null);
                typeof(ScoreManager).GetProperty(
                        "RecordsAllowed",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    ?.SetValue(score, true);

                var manager = managerHost.AddComponent<GameManager>();
                typeof(GameManager).GetField(
                        "currentRunId",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(manager, "result-run");
                var settle = typeof(GameManager).GetMethod(
                    "SettleGameOverResult",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var first = (GameOverResult)settle?.Invoke(manager, null);
                var duplicate = (GameOverResult)settle?.Invoke(manager, null);

                Assert.That(first.EarnedGrowthCurrency,
                    Is.EqualTo(RunRewardCalculator.TutorialReward));
                Assert.That(first.GrowthCurrencyBalance,
                    Is.EqualTo(RunRewardCalculator.TutorialReward));
                Assert.That(first.RewardsAllowed, Is.True);
                Assert.That(duplicate.EarnedGrowthCurrency, Is.Zero);
                Assert.That(duplicate.GrowthCurrencyBalance,
                    Is.EqualTo(first.GrowthCurrencyBalance));
            }
            finally
            {
                Object.DestroyImmediate(managerHost);
                Object.DestroyImmediate(scoreHost);
            }
        }

        void SeedFullWallet(int requestedWallet = -1)
        {
            int wallet = requestedWallet < 0
                ? PermanentGrowthCatalog.TotalCost
                : requestedWallet;
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":1," +
                $"\"wallet\":{wallet},\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"ranks\":[]}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }
    }
}
