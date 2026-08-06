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
        public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void ThreeRootsCanBePurchasedIndependently()
        {
            SeedV2(3);

            Assert.That(PermanentGrowthProfile.CanPurchaseNode("S00"), Is.True);
            Assert.That(PermanentGrowthProfile.CanPurchaseNode("J00"), Is.True);
            Assert.That(PermanentGrowthProfile.CanPurchaseNode("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("S00"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("J00"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
        }

        [Test]
        public void NormalNodeRequiresItsOwnPreviousFruitAndCannotBeBoughtTwice()
        {
            SeedV2(5);

            Assert.That(PermanentGrowthProfile.MeetsNodeRequirements("I-A2"), Is.False);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I-A2"), Is.False);
            Assert.That(PermanentGrowthProfile.GetNodeLockReason("I-A2"),
                Does.Contain("넓은 벼루"));

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I00"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I-A1"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I-A2"), Is.True);
            int balance = PermanentGrowthProfile.Currency;
            int saves = store.SaveCount;

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("I-A2"), Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(balance));
            Assert.That(store.SaveCount, Is.EqualTo(saves));
        }

        [Test]
        public void KeystoneRequiresItsThreeStepChainAndFourGeneralFruits()
        {
            SeedV2(13);
            Buy("S00", "S-A1", "S-A2");

            Assert.That(PermanentGrowthProfile.MeetsNodeRequirements("S-KA"), Is.False);
            Assert.That(PermanentGrowthProfile.GetNodeLockReason("S-KA"),
                Does.Contain("일반 열매 4개 필요"));

            Assert.That(PermanentGrowthProfile.TryPurchaseNode("S-A3"), Is.True);
            Assert.That(PermanentGrowthProfile.MeetsNodeRequirements("S-KA"), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchaseNode("S-KA"), Is.True);
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("S-KA"), Is.True);
        }

        [Test]
        public void EachBranchAppliesOnlyItsSelectedPath()
        {
            SeedV2(13);
            Buy("S00", "S-A1", "S-A2", "S-A3", "S-KA");

            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KA"));
            Assert.That(PermanentGrowthProfile.HasLastBreath, Is.False);
            Assert.That(PermanentGrowthProfile.MaxHealthBonus, Is.EqualTo(4));

            Buy("S-B1", "S-B2", "S-B3", "S-KB");
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KB"));

            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KA"), Is.False);
            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KB"), Is.True);
            Assert.That(PermanentGrowthProfile.HasLastBreath, Is.False);
            Assert.That(PermanentGrowthProfile.DamageGraceBonusSeconds,
                Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(PermanentGrowthProfile.HasPostHitShield, Is.True);
            Assert.That(PermanentGrowthProfile.MaxHealthBonus, Is.EqualTo(1));

            Assert.That(PermanentGrowthProfile.TryEquipKeystone("S-KA"), Is.True);
            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KA"), Is.True);
            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KB"), Is.False);
            Assert.That(PermanentGrowthProfile.MaxHealthBonus, Is.EqualTo(4));

            Assert.That(
                PermanentGrowthProfile.ClearActiveKeystone(
                    PermanentGrowthBranch.Survival),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KA"));
        }

        [Test]
        public void ResetPurchasedNodesRefundsFruitsAndKeepsJourney()
        {
            SeedV2(4);
            Buy("S00", "S-A1", "S-A2");
            long journeyBefore = PermanentGrowthProfile.CumulativeDistanceMeters;

            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);

            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(4));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(journeyBefore));
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Survival),
                Is.Empty);
        }

        [Test]
        public void KeystoneEquipRejectsLockedNormalAndWrongIdsWithoutSaving()
        {
            SeedV2(4, "I00");
            int saves = store.SaveCount;

            Assert.That(PermanentGrowthProfile.TryEquipKeystone("I-KA"), Is.False);
            Assert.That(PermanentGrowthProfile.TryEquipKeystone("I00"), Is.False);
            Assert.That(PermanentGrowthProfile.TryEquipKeystone("missing"), Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(saves));
        }

        [Test]
        public void RunSnapshotDoesNotChangeWhenLobbyLoadoutChangesLater()
        {
            SeedV2(13);
            Buy("S00", "S-A1", "S-A2", "S-A3", "S-KA");
            PermanentGrowthRunSnapshot snapshot =
                PermanentGrowthProfile.CreateRunSnapshot();

            Buy("S-B1", "S-B2", "S-B3", "S-KB");
            Assert.That(PermanentGrowthProfile.TryEquipKeystone("S-KB"), Is.True);

            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasStableHit, Is.False);
            Assert.That(snapshot.HasNode("S-KB"), Is.False);
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(4));
            Assert.That(snapshot.DamageGraceBonusSeconds,
                Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(snapshot.HasPostHitShield, Is.False);
            Assert.That(snapshot.GetActiveKeystoneId(PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KA"));
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().DamageGraceBonusSeconds,
                Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.CreateRunSnapshot().HasPostHitShield,
                Is.True);
        }

        [Test]
        public void FullV1SaveMigratesDirectlyIntoThirtyNineNodeEconomy()
        {
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":1," +
                "\"wallet\":0,\"spent\":957," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"legacy-full\",\"ranks\":[" +
                "{\"id\":\"permanent.ink_capacity\",\"level\":6}," +
                "{\"id\":\"permanent.ink_recovery\",\"level\":6}," +
                "{\"id\":\"permanent.platform_lifetime\",\"level\":6}," +
                "{\"id\":\"permanent.stroke_guard\",\"level\":1}," +
                "{\"id\":\"permanent.jump_charge\",\"level\":6}," +
                "{\"id\":\"permanent.jump_power\",\"level\":5}," +
                "{\"id\":\"permanent.drawn_platform_leap\",\"level\":1}," +
                "{\"id\":\"permanent.vitality\",\"level\":1}," +
                "{\"id\":\"permanent.damage_grace\",\"level\":3}," +
                "{\"id\":\"permanent.last_breath\",\"level\":1}," +
                "{\"id\":\"permanent.clone_spawn_grace\",\"level\":3}]}";
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(33));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(33));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(6));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(39));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(3750));
            Assert.That(
                PermanentGrowthCatalog.Nodes.Count(node =>
                    PermanentGrowthProfile.IsNodeUnlocked(node.Id) &&
                    node.Branch == PermanentGrowthBranch.Survival),
                Is.EqualTo(8));
            Assert.That(
                PermanentGrowthCatalog.Nodes.Count(node =>
                    PermanentGrowthProfile.IsNodeUnlocked(node.Id) &&
                    node.Branch == PermanentGrowthBranch.Leap),
                Is.EqualTo(12));
            Assert.That(
                PermanentGrowthCatalog.Nodes.Count(node =>
                    PermanentGrowthProfile.IsNodeUnlocked(node.Id) &&
                    node.Branch == PermanentGrowthBranch.InkHandling),
                Is.EqualTo(13));
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Leap),
                Is.EqualTo("J-KA"));
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.InkHandling),
                Is.EqualTo("I-KA"));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
            Assert.That(store.Json, Does.Contain("\"ranks\":[]"));
        }

        [Test]
        public void V2LeapKeystoneSavePreservesLoadoutWithoutRetiredNodeRefund()
        {
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":4,\"spent\":5," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"kept-run\"," +
                "\"settledRunIds\":[\"kept-run\"],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"J00\",\"J-B1\",\"J-B2\",\"J-B3\",\"J-KB\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"J-KB\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("J-B4"), Is.False);
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("J-B5"), Is.False);
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Leap),
                Is.EqualTo("J-KB"));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(4));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(9));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(300));
            Assert.That(store.Json, Does.Contain("\"lastSettledRunId\":\"kept-run\""));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
        }

        [Test]
        public void V3RetiredLeapNodesRefundEachUniqueIdAndPreserveKeystone()
        {
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":3," +
                "\"wallet\":2,\"spent\":12," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"v3-run\"," +
                "\"settledRunIds\":[\"v3-run\"],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"J00\",\"J-B1\",\"J-B2\",\"J-B3\"," +
                "\"J-A4\",\"J-A5\",\"J-B4\",\"J-B4\",\"J-B5\"," +
                "\"J-C4\",\"J-C5\",\"J-KB\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"J-KB\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8),
                "삭제된 A4/A5·B4/B5·C4/C5는 중복을 제외하고 각각 먹빛 하나만 돌려줘야 합니다.");
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(13));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(500));
            foreach (string retiredId in new[]
                     {
                         "J-A4", "J-A5", "J-B4", "J-B5", "J-C4", "J-C5",
                     })
                Assert.That(PermanentGrowthProfile.IsNodeUnlocked(retiredId),
                    Is.False, retiredId);
            Assert.That(
                PermanentGrowthProfile.GetActiveKeystoneId(
                    PermanentGrowthBranch.Leap),
                Is.EqualTo("J-KB"));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":7"));
        }

        [Test]
        public void LoadedV2SaveDropsUnknownAndDuplicateNodeIds()
        {
            SeedV2(50, "I00", "I00", "unknown", "S00");

            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(37));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(39));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(3750));
            Assert.That(PermanentGrowthProfile.IsNodeUnlocked("unknown"), Is.False);
        }

        void Buy(params string[] nodeIds)
        {
            foreach (string nodeId in nodeIds)
                Assert.That(PermanentGrowthProfile.TryPurchaseNode(nodeId),
                    Is.True, nodeId);
        }

        void SeedV2(int wallet, params string[] ownedNodeIds)
        {
            string owned = ownedNodeIds == null || ownedNodeIds.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", ownedNodeIds) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                $"\"wallet\":{wallet},\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                "\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }
    }
}
