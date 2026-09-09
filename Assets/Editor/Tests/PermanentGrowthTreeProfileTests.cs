using System;
using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeProfileTests
    {
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown() =>
            PermanentGrowthProfile.RestoreDefaultStoreForTests();

        [Test]
        public void FourFirstStagesCanBePurchasedIndependently()
        {
            store.Json = V8Json(4, Array.Empty<string>());
            PermanentGrowthProfile.ResetCacheForTests();

            foreach (PermanentGrowthChoiceDefinition choice
                     in PermanentGrowthCatalog.Choices)
                Assert.That(PermanentGrowthProfile.TryPurchase(choice.Type),
                    Is.True, choice.DisplayName);

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(4));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(4));
        }

        [Test]
        public void LaterStageRequiresItsOwnPreviousStageAndCannotBeBoughtTwice()
        {
            store.Json = V8Json(6, Array.Empty<string>());
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.2"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.2"), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(3));
        }

        [Test]
        public void ResetRefundsActualVariableCostAndKeepsJourneyAndSettlementIds()
        {
            string[] owned = { "body.1", "ink.1", "ink.2", "ink.3" };
            int spent = Cost(owned);
            store.Json = V8Json(39 - spent, owned, "journey-run");
            PermanentGrowthProfile.ResetCacheForTests();
            long distance = PermanentGrowthProfile.CumulativeDistanceMeters;
            int claimed = PermanentGrowthProfile.ClaimedDistanceRewardCount;

            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(39));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(distance));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(claimed));
            Assert.That(store.Json, Does.Contain("journey-run"));
        }

        [Test]
        public void RunSnapshotDoesNotChangeAfterLaterLobbyPurchase()
        {
            store.Json = V8Json(6, Array.Empty<string>());
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.1"), Is.True);
            PermanentGrowthRunSnapshot first =
                PermanentGrowthProfile.CreateRunSnapshot();

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("body.2"), Is.True);
            PermanentGrowthRunSnapshot second =
                PermanentGrowthProfile.CreateRunSnapshot();

            Assert.That(first.MaxHealthBonus, Is.EqualTo(1));
            Assert.That(second.MaxHealthBonus, Is.EqualTo(2));
            Assert.That(first.MaxHealthBonus, Is.EqualTo(1),
                "진행 중 판의 스냅샷은 로비의 후속 성장으로 바뀌면 안 됩니다.");
        }

        [Test]
        public void AllOwnedV8StagesApplyTogetherWithoutEquippedPaths()
        {
            string[] owned = PermanentGrowthCatalog.Nodes
                .Where(node => node.Rank <= (node.EffectId == PermanentGrowthType.Vitality ? 3 : 4))
                .Select(node => node.Id).ToArray();
            store.Json = V8Json(0, owned);
            PermanentGrowthProfile.ResetCacheForTests();

            PermanentGrowthRunSnapshot snapshot =
                PermanentGrowthProfile.CreateRunSnapshot();
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(3));
            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(snapshot.InkBudgetCostMultiplier,
                Is.EqualTo(0.88f).Within(0.0001f));
            Assert.That(snapshot.JumpHeightMultiplier,
                Is.EqualTo(1.05f).Within(0.0001f));
            Assert.That(snapshot.GetActiveKeystoneId(
                PermanentGrowthBranch.Survival), Is.Empty);
            Assert.That(snapshot.GetActiveKeystoneId(
                PermanentGrowthBranch.Leap), Is.Empty);
            Assert.That(snapshot.GetActiveKeystoneId(
                PermanentGrowthBranch.InkHandling), Is.Empty);
        }

        [Test]
        public void ValidV7TreeIsRefundedIntoV8WithoutCarryingEquippedPaths()
        {
            const string rawV7 =
                "{\"schemaVersion\":1,\"balanceVersion\":7," +
                "\"wallet\":3,\"spent\":3," +
                "\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":true," +
                "\"rewardedBestHeight\":0," +
                "\"cumulativeDistanceMeters\":150," +
                "\"claimedDistanceRewardCount\":6," +
                "\"lastSettledRunId\":\"legacy-run\"," +
                "\"settledRunIds\":[\"legacy-run\"],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"S00\",\"J00\",\"I00\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            store.Json = rawV7;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(6));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(store.Json, Does.Contain("legacy-run"));
        }

        static string V8Json(
            int wallet,
            IReadOnlyList<string> owned,
            string settledRunId = "")
        {
            int spent = Cost(owned);
            int claimed = wallet + spent;
            long distance = RunRewardCalculator.GetLegacyThresholdForRewardCount(claimed);
            string ownedJson = owned.Count == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", owned) + "\"]";
            string settledJson = string.IsNullOrEmpty(settledRunId)
                ? "[]"
                : $"[\"{settledRunId}\"]";
            return
                "{\"schemaVersion\":1,\"balanceVersion\":8," +
                $"\"wallet\":{wallet},\"spent\":{spent}," +
                "\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":true," +
                "\"rewardedBestHeight\":0," +
                $"\"cumulativeDistanceMeters\":{distance}," +
                $"\"claimedDistanceRewardCount\":{claimed}," +
                $"\"lastSettledRunId\":\"{settledRunId}\"," +
                $"\"settledRunIds\":{settledJson},\"ranks\":[]," +
                $"\"ownedNodeIds\":{ownedJson}," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
        }

        static int Cost(IEnumerable<string> owned) =>
            owned.Sum(id => PermanentGrowthCatalog.GetNode(id)?.Cost ?? 0);
    }
}
