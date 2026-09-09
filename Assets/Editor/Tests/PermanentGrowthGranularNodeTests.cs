using System;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthGranularNodeTests
    {
        [Test]
        public void LegacyEnumValuesRemainStableForSaveMigration()
        {
            Assert.That((int)PermanentGrowthType.InkCapacity, Is.EqualTo(0));
            Assert.That((int)PermanentGrowthType.InkRecovery, Is.EqualTo(1));
            Assert.That((int)PermanentGrowthType.PlatformLifetime, Is.EqualTo(2));
            Assert.That((int)PermanentGrowthType.JumpCharge, Is.EqualTo(3));
            Assert.That((int)PermanentGrowthType.CloneSpawnGrace, Is.EqualTo(10));
            Assert.That((int)PermanentGrowthType.FirstLandingPause, Is.EqualTo(30));
            Assert.That((int)PermanentGrowthType.JumpHeight, Is.EqualTo(31));
            Assert.That((int)PermanentGrowthType.WallCling, Is.EqualTo(34));
            Assert.That((int)PermanentGrowthType.InkBudgetEfficiency, Is.EqualTo(35));
            Assert.That((int)PermanentGrowthType.InkEvictionFade, Is.EqualTo(36));
            Assert.That((int)PermanentGrowthType.InkEvictionDelay, Is.EqualTo(37));
            Assert.That((int)PermanentGrowthType.InkCloneItemExtraCount, Is.EqualTo(38));
            Assert.That((int)PermanentGrowthType.PostHitShield, Is.EqualTo(39));
            Assert.That((int)PermanentGrowthType.InkCapacityDouble, Is.EqualTo(40));
            Assert.That((int)PermanentGrowthType.GoldenBrushShield, Is.EqualTo(41));
            Assert.That((int)PermanentGrowthType.InkDropEndShield, Is.EqualTo(42));
            Assert.That((int)PermanentGrowthType.CloneMaxHealth, Is.EqualTo(43));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void VitalityStagesOnlyIncreaseOriginalBodyHealth(
            int stageCount,
            int expectedBonus)
        {
            PermanentGrowthRunSnapshot snapshot = Snapshot("body", stageCount);
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(expectedBonus));
            Assert.That(snapshot.InkCloneMaxHealthBonus, Is.Zero);
            AssertNeutralExcept(snapshot, PermanentGrowthType.Vitality);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 1.125f)]
        [TestCase(2, 1.25f)]
        [TestCase(3, 1.375f)]
        [TestCase(4, 1.5f)]
        public void InkStagesOnlyIncreaseMaximumInk(
            int stageCount,
            float expectedMultiplier)
        {
            PermanentGrowthRunSnapshot snapshot = Snapshot("ink", stageCount);
            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(expectedMultiplier).Within(0.0001f));
            AssertNeutralExcept(snapshot, PermanentGrowthType.InkCapacity);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 0.97f)]
        [TestCase(2, 0.94f)]
        [TestCase(3, 0.91f)]
        [TestCase(4, 0.88f)]
        public void BrushStagesOnlyReduceInkCost(
            int stageCount,
            float expectedMultiplier)
        {
            PermanentGrowthRunSnapshot snapshot = Snapshot("brush", stageCount);
            Assert.That(snapshot.InkBudgetCostMultiplier,
                Is.EqualTo(expectedMultiplier).Within(0.0001f));
            AssertNeutralExcept(snapshot,
                PermanentGrowthType.InkBudgetEfficiency);
        }

        [TestCase(0, 1f)]
        [TestCase(1, 1.0125f)]
        [TestCase(2, 1.025f)]
        [TestCase(3, 1.0375f)]
        [TestCase(4, 1.05f)]
        public void JumpStagesOnlyIncreaseHeight(
            int stageCount,
            float expectedHeight)
        {
            PermanentGrowthRunSnapshot snapshot = Snapshot("jump", stageCount);
            Assert.That(snapshot.JumpHeightMultiplier,
                Is.EqualTo(expectedHeight).Within(0.0001f));
            Assert.That(snapshot.JumpVerticalSpeedMultiplier,
                Is.EqualTo(Mathf.Sqrt(expectedHeight)).Within(0.0001f));
            AssertNeutralExcept(snapshot, PermanentGrowthType.JumpHeight);
        }

        [Test]
        public void MaximumV8BuildDoesNotReactivateRemovedPassives()
        {
            var snapshot = new PermanentGrowthRunSnapshot(
                PermanentGrowthCatalog.Nodes.Select(node => node.Id), null);

            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasPostHitShield, Is.False);
            Assert.That(snapshot.HasGoldenBrushShield, Is.False);
            Assert.That(snapshot.HasInkDropEndShield, Is.False);
            Assert.That(snapshot.HasWallCling, Is.False);
            Assert.That(snapshot.HasDoubleJump, Is.False);
            Assert.That(snapshot.HasSafetyPlatform, Is.False);
            Assert.That(snapshot.InkCloneItemExtraCount, Is.Zero);
            Assert.That(snapshot.InkCloneMaxHealthBonus, Is.Zero);
            Assert.That(snapshot.InkRecoverySpeedMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
        }

        static PermanentGrowthRunSnapshot Snapshot(string prefix, int count) =>
            new(
                Enumerable.Range(1, count).Select(rank => $"{prefix}.{rank}"),
                null);

        static void AssertNeutralExcept(
            PermanentGrowthRunSnapshot snapshot,
            PermanentGrowthType allowed)
        {
            if (allowed != PermanentGrowthType.Vitality)
                Assert.That(snapshot.MaxHealthBonus, Is.Zero);
            if (allowed != PermanentGrowthType.InkCapacity)
                Assert.That(snapshot.InkCapacityMultiplier,
                    Is.EqualTo(1f).Within(0.0001f));
            if (allowed != PermanentGrowthType.InkBudgetEfficiency)
                Assert.That(snapshot.InkBudgetCostMultiplier,
                    Is.EqualTo(1f).Within(0.0001f));
            if (allowed != PermanentGrowthType.JumpHeight)
            {
                Assert.That(snapshot.JumpHeightMultiplier,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(snapshot.JumpVerticalSpeedMultiplier,
                    Is.EqualTo(1f).Within(0.0001f));
            }
        }
    }
}
