using System;
using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeCatalogTests
    {
        static readonly string[] SurvivalIds =
        {
            "S00", "S-A1", "S-A2", "S-A3", "S-KA",
            "S-B1", "S-B2", "S-B3", "S-KB",
            "S-C1", "S-C2", "S-C3", "S-KC",
        };

        static readonly string[] LeapIds =
        {
            "J00", "J-A1", "J-A2", "J-A3", "J-KA",
            "J-B1", "J-B2", "J-B3", "J-KB",
            "J-C1", "J-C2", "J-C3", "J-KC",
        };

        static readonly string[] InkIds =
        {
            "I00", "I-A1", "I-A2", "I-A3", "I-KA",
            "I-B1", "I-B2", "I-B3", "I-KB",
            "I-C1", "I-C2", "I-C3", "I-KC",
        };

        [Test]
        public void CatalogHasThirtyNineOneCostNodesAcrossThreeEqualBranches()
        {
            Assert.That(PermanentGrowthCatalog.Branches.Count, Is.EqualTo(3));
            Assert.That(PermanentGrowthCatalog.Nodes.Count, Is.EqualTo(39));
            Assert.That(PermanentGrowthCatalog.TotalCost, Is.EqualTo(39));

            AssertBranch(PermanentGrowthBranch.Survival, SurvivalIds);
            AssertBranch(PermanentGrowthBranch.Leap, LeapIds);
            AssertBranch(PermanentGrowthBranch.InkHandling, InkIds);
            Assert.That(
                PermanentGrowthCatalog.Nodes.All(node => node.Cost == 1),
                Is.True);
        }

        [Test]
        public void EveryNodeOwnsStableReadableAndUniquePresentationData()
        {
            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.Id).Distinct().Count(),
                Is.EqualTo(39));
            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.DisplayName).Distinct().Count(),
                Is.EqualTo(39),
                "각 열매는 선택 팝업에서 구분되는 고유 이름을 가져야 합니다.");
            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.IconKey).Distinct().Count(),
                Is.EqualTo(39),
                "39개 열매는 같은 트랙 아이콘을 반복하지 않고 stable icon key를 소유해야 합니다.");

            foreach (PermanentGrowthNodeDefinition node in PermanentGrowthCatalog.Nodes)
            {
                Assert.That(node.Id, Is.Not.Empty);
                Assert.That(node.DisplayName, Is.Not.Empty, node.Id);
                Assert.That(node.Description, Is.Not.Empty, node.Id);
                Assert.That(node.EffectSummary, Is.Not.Empty, node.Id);
                Assert.That(node.IconKey, Is.Not.Empty, node.Id);
                Assert.That(PermanentGrowthCatalog.GetNode(node.Id), Is.SameAs(node));
            }
        }

        [Test]
        public void EachBranchHasIndependentRootThreeChainsAndThreeKeystones()
        {
            foreach (PermanentGrowthBranch branch
                     in Enum.GetValues(typeof(PermanentGrowthBranch)))
            {
                PermanentGrowthNodeDefinition[] nodes = BranchNodes(branch);
                PermanentGrowthNodeDefinition root = nodes.Single(node =>
                    node.NodeKind == PermanentGrowthNodeKind.Root);
                PermanentGrowthNodeDefinition[] keystones = nodes
                    .Where(node => node.IsKeystone)
                    .ToArray();
                PermanentGrowthNodeDefinition[] rootChildren = nodes
                    .Where(node => node.ParentIds.Contains(root.Id))
                    .ToArray();

                Assert.That(root.ParentIds, Is.Empty, branch.ToString());
                Assert.That(rootChildren, Has.Length.EqualTo(3), branch.ToString());
                Assert.That(keystones, Has.Length.EqualTo(3), branch.ToString());
                Assert.That(nodes.Count(node => !node.IsKeystone),
                    Is.EqualTo(10));

                foreach (PermanentGrowthNodeDefinition keystone in keystones)
                {
                    StringAssert.EndsWith("결실", keystone.DisplayName,
                        $"{keystone.Id}는 화면에서 결실 단계임을 바로 읽을 수 있어야 합니다.");
                    Assert.That(keystone.ParentIds.Count, Is.EqualTo(1), keystone.Id);
                    Assert.That(keystone.RequiredOwnedCountInBranch,
                        Is.EqualTo(4), keystone.Id);
                    Assert.That(keystone.KeystoneGroup, Is.Not.Empty, keystone.Id);
                    Assert.That(
                        PermanentGrowthCatalog.GetNode(keystone.ParentIds[0]).Branch,
                        Is.EqualTo(branch),
                        keystone.Id);
                }
            }

            Assert.That(
                PermanentGrowthCatalog.Nodes.Count(node => node.ParentIds.Count == 0),
                Is.EqualTo(3),
                "v3는 공통 구매 줄기 없이 세 계보 뿌리를 독립적으로 시작합니다.");
        }

        [Test]
        public void GraphParentsExistStayInsideBranchAndContainNoCycle()
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PermanentGrowthNodeDefinition node in PermanentGrowthCatalog.Nodes)
            {
                foreach (string parentId in node.ParentIds)
                {
                    PermanentGrowthNodeDefinition parent =
                        PermanentGrowthCatalog.GetNode(parentId);
                    Assert.That(parent, Is.Not.Null, $"{node.Id} <- {parentId}");
                    Assert.That(parent.Branch, Is.EqualTo(node.Branch), node.Id);
                }
            }

            foreach (PermanentGrowthNodeDefinition node in PermanentGrowthCatalog.Nodes)
                Assert.That(VisitWithoutCycle(node.Id, states), Is.True, node.Id);
        }

        [Test]
        public void CoreBalanceValuesMatchGrowthTracksAndSurvivalCapstones()
        {
            AssertEffect("I00", PermanentGrowthType.InkBudgetEfficiency, 0.02f);
            for (int rank = 1; rank <= 3; rank++)
            {
                AssertEffect($"I-A{rank}", PermanentGrowthType.InkCapacity, 0.375f);
                AssertEffect($"I-B{rank}", PermanentGrowthType.InkBudgetEfficiency, 0.02f);
                AssertEffect($"I-C{rank}", PermanentGrowthType.InkRecovery, 0.10f);
            }
            AssertEffect("I-KA", PermanentGrowthType.InkCapacityDouble, 2f);
            AssertEffect("I-KB", PermanentGrowthType.GoldenBrushShield, 1f);
            AssertEffect("I-KC", PermanentGrowthType.InkRecovery, 0.10f);

            AssertEffect("S00", PermanentGrowthType.DamageGrace, 0.04f);
            for (int rank = 1; rank <= 3; rank++)
            {
                AssertEffect($"S-A{rank}", PermanentGrowthType.Vitality, 1f);
                AssertEffect($"S-B{rank}", PermanentGrowthType.DamageGrace, 0.04f);
                AssertEffect($"S-C{rank}", PermanentGrowthType.HitHorizontalStability, 0.06f);
            }
            AssertEffect("S-KA", PermanentGrowthType.CloneMaxHealth, 1f);
            AssertEffect("S-KB", PermanentGrowthType.LastBreath, 1f);
            AssertEffect("S-KC", PermanentGrowthType.InkCloneItemExtraCount, 1f);

            AssertEffect("J00", PermanentGrowthType.JumpPower, 0.01f);
            for (int rank = 1; rank <= 3; rank++)
            {
                AssertEffect($"J-A{rank}", PermanentGrowthType.JumpCharge, 0.015f);
                AssertEffect($"J-B{rank}", PermanentGrowthType.JumpPower, 0.01f);
                AssertEffect($"J-C{rank}", PermanentGrowthType.JumpHeight, 0.0625f / 4f);
            }
            AssertEffect("J-KA", PermanentGrowthType.InkDropEndShield, 1f);
            AssertEffect("J-KB", PermanentGrowthType.WallCling, 1.2f);
            AssertEffect("J-KC", PermanentGrowthType.DoubleJump, 0.40f);

            Assert.That(EffectTotal(PermanentGrowthType.InkCapacity),
                Is.EqualTo(1.125f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.InkCapacityDouble),
                Is.EqualTo(2f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.InkBudgetEfficiency),
                Is.EqualTo(0.08f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.InkRecovery),
                Is.EqualTo(0.40f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.Vitality),
                Is.EqualTo(3f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.DamageGrace),
                Is.EqualTo(0.16f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.LastBreath),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.CloneMaxHealth),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.GoldenBrushShield),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.InkDropEndShield),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.HitHorizontalStability),
                Is.EqualTo(0.18f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.InkCloneItemExtraCount),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.JumpCharge),
                Is.EqualTo(0.045f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.JumpPower),
                Is.EqualTo(0.04f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.JumpHeight),
                Is.EqualTo(0.046875f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.WallCling),
                Is.EqualTo(1.2f).Within(0.000001f));
            Assert.That(EffectTotal(PermanentGrowthType.DoubleJump),
                Is.EqualTo(0.40f).Within(0.000001f));

            Assert.That(PermanentGrowthCatalog.Nodes.Any(node =>
                node.DisplayName == "낮은 흔들림"), Is.False);
        }

        [Test]
        public void BranchesOccupyDistinctLeftCenterAndRightTreeRegions()
        {
            PermanentGrowthNodeDefinition[] survival =
                BranchNodes(PermanentGrowthBranch.Survival);
            PermanentGrowthNodeDefinition[] ink =
                BranchNodes(PermanentGrowthBranch.InkHandling);
            PermanentGrowthNodeDefinition[] leap =
                BranchNodes(PermanentGrowthBranch.Leap);

            Assert.That(survival.Max(node => node.LayoutX), Is.LessThanOrEqualTo(-500f));
            Assert.That(ink.Min(node => node.LayoutX), Is.GreaterThanOrEqualTo(-500f));
            Assert.That(ink.Max(node => node.LayoutX), Is.LessThanOrEqualTo(500f));
            Assert.That(leap.Min(node => node.LayoutX), Is.GreaterThanOrEqualTo(500f));
        }

        [Test]
        public void SurvivalCloneAndInkCapacityPathsRemainVisuallySeparate()
        {
            string[] survivalPath = { "S-C1", "S-C2", "S-C3", "S-KC" };
            string[] inkCapacityPath = { "I-A1", "I-A2", "I-A3", "I-KA" };

            for (int i = 0; i < survivalPath.Length; i++)
            {
                PermanentGrowthNodeDefinition survival =
                    PermanentGrowthCatalog.GetNode(survivalPath[i]);
                PermanentGrowthNodeDefinition ink =
                    PermanentGrowthCatalog.GetNode(inkCapacityPath[i]);

                Assert.That(survival.Branch,
                    Is.EqualTo(PermanentGrowthBranch.Survival));
                Assert.That(ink.Branch,
                    Is.EqualTo(PermanentGrowthBranch.InkHandling));
                Assert.That(ink.LayoutX - survival.LayoutX,
                    Is.GreaterThanOrEqualTo(280f),
                    $"{survival.Id}와 {ink.Id}의 열매·가지가 다시 붙으면 안 됩니다.");
            }
        }

        static void AssertBranch(
            PermanentGrowthBranch branch,
            IReadOnlyCollection<string> expectedIds)
        {
            PermanentGrowthNodeDefinition[] nodes = BranchNodes(branch);
            Assert.That(nodes, Has.Length.EqualTo(expectedIds.Count), branch.ToString());
            Assert.That(nodes.Select(node => node.Id),
                Is.EquivalentTo(expectedIds), branch.ToString());
        }

        static PermanentGrowthNodeDefinition[] BranchNodes(
            PermanentGrowthBranch branch) =>
            PermanentGrowthCatalog.Nodes
                .Where(node => node.Branch == branch)
                .ToArray();

        static void AssertEffect(
            string id,
            PermanentGrowthType effectId,
            float effectValue)
        {
            PermanentGrowthNodeDefinition node = PermanentGrowthCatalog.GetNode(id);
            Assert.That(node, Is.Not.Null, id);
            Assert.That(node.EffectId, Is.EqualTo(effectId), id);
            Assert.That(node.EffectValue,
                Is.EqualTo(effectValue).Within(0.000001f), id);
        }

        static float EffectTotal(PermanentGrowthType effectId) =>
            PermanentGrowthCatalog.Nodes
                .Where(node => node.EffectId == effectId)
                .Sum(node => node.EffectValue);

        static bool VisitWithoutCycle(
            string nodeId,
            IDictionary<string, int> states)
        {
            if (states.TryGetValue(nodeId, out int state))
                return state == 2;

            states[nodeId] = 1;
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            foreach (string parentId in node.ParentIds)
            {
                if (states.TryGetValue(parentId, out int parentState) &&
                    parentState == 1)
                    return false;
                if (!VisitWithoutCycle(parentId, states))
                    return false;
            }
            states[nodeId] = 2;
            return true;
        }
    }
}
