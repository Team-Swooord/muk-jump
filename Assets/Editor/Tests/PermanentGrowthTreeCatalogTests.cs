using System;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeCatalogTests
    {
        [Test]
        public void V9CatalogUsesExactlyFourPlainTracksAndThirtyTwoStages()
        {
            Assert.That(PermanentGrowthCatalog.Choices.Count, Is.EqualTo(4));
            Assert.That(PermanentGrowthCatalog.Nodes.Count, Is.EqualTo(32));
            Assert.That(PermanentGrowthCatalog.TotalCost, Is.EqualTo(244));
            Assert.That(
                PermanentGrowthCatalog.Choices.Select(choice => choice.Id),
                Is.EqualTo(new[] { "body", "ink", "brush", "jump" }));
        }

        [TestCase("body", 8, new[] { 1, 2, 3, 5, 7, 10, 14, 19 })]
        [TestCase("ink", 8, new[] { 1, 2, 3, 5, 7, 10, 14, 19 })]
        [TestCase("brush", 8, new[] { 1, 2, 3, 5, 7, 10, 14, 19 })]
        [TestCase("jump", 8, new[] { 1, 2, 3, 5, 7, 10, 14, 19 })]
        public void EveryTrackHasStableSequentialIdsParentsAndCosts(
            string prefix,
            int stageCount,
            int[] costs)
        {
            for (int rank = 1; rank <= stageCount; rank++)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode($"{prefix}.{rank}");
                Assert.That(node, Is.Not.Null, $"{prefix}.{rank}");
                Assert.That(node.Rank, Is.EqualTo(rank));
                Assert.That(node.Cost, Is.EqualTo(costs[rank - 1]));
                Assert.That(node.NodeKind,
                    Is.EqualTo(PermanentGrowthNodeKind.Stat));
                Assert.That(PermanentGrowthCatalog.GetPath(node),
                    Is.EqualTo(PermanentGrowthPath.None));
                if (rank == 1)
                    Assert.That(node.ParentIds, Is.Empty);
                else
                    Assert.That(node.ParentIds,
                        Is.EqualTo(new[] { $"{prefix}.{rank - 1}" }));
            }
        }

        [Test]
        public void ChoiceCopyIsPlainDistinctAndContainsNoLevelNotation()
        {
            string[] names =
            {
                "튼튼한 먹", "넉넉한 먹물", "알뜰한 붓", "높은 도약",
            };
            string[] summaries =
            {
                "부딪혀도 더 오래 버텨요.",
                "먹물을 넉넉히 담아 둘 수 있어요.",
                "그릴 때 먹물이 천천히 줄어요.",
                "더 높은 곳까지 뛰어요.",
            };

            Assert.That(
                PermanentGrowthCatalog.Choices.Select(choice => choice.DisplayName),
                Is.EqualTo(names));
            Assert.That(
                PermanentGrowthCatalog.Choices.Select(choice => choice.Summary),
                Is.EqualTo(summaries));
            Assert.That(
                PermanentGrowthCatalog.Choices.Select(choice => choice.IconKey)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(4));
            Assert.That(
                PermanentGrowthCatalog.Choices.All(choice =>
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        choice.Summary,
                        "[0-9%+→]")),
                Is.True);
        }

        [Test]
        public void TracksAffectOnlyHealthCapacityInkCostAndJumpHeight()
        {
            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.EffectId)
                    .Distinct(),
                Is.EquivalentTo(new[]
                {
                    PermanentGrowthType.Vitality,
                    PermanentGrowthType.InkCapacity,
                    PermanentGrowthType.InkBudgetEfficiency,
                    PermanentGrowthType.JumpHeight,
                }));
            Assert.That(PermanentGrowthCatalog.Nodes.Any(node => node.IsKeystone),
                Is.False);
        }

        [Test]
        public void MaximumTrackValuesMatchTheSimpleBalanceContract()
        {
            Assert.That(
                PermanentGrowthCatalog.Get(PermanentGrowthType.Vitality)
                    .GetDisplayValueAtLevel(8),
                Is.EqualTo(8f).Within(0.0001f));
            Assert.That(
                PermanentGrowthCatalog.Get(PermanentGrowthType.InkCapacity)
                    .GetDisplayValueAtLevel(8),
                Is.EqualTo(100f).Within(0.0001f));
            Assert.That(
                PermanentGrowthCatalog.Get(PermanentGrowthType.InkBudgetEfficiency)
                    .GetDisplayValueAtLevel(8),
                Is.EqualTo(24f).Within(0.0001f));
            Assert.That(
                PermanentGrowthCatalog.Get(PermanentGrowthType.JumpHeight)
                    .GetDisplayValueAtLevel(8),
                Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void RemovedTreeNodeIdsAreNotPurchasableInTheCurrentCatalog()
        {
            foreach (string retired in new[]
                     { "S00", "J00", "I00", "S-KA", "J-KB", "I-KC" })
                Assert.That(PermanentGrowthCatalog.GetNode(retired), Is.Null);
        }
    }
}
