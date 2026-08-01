using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeCatalogTests
    {
        [Test]
        public void Catalog_정확히_세_계보와_계보별_최종패시브_하나를_가진다()
        {
            Assert.That(PermanentGrowthCatalog.Branches.Count, Is.EqualTo(3));
            Assert.That(
                PermanentGrowthCatalog.Branches
                    .Select(metadata => metadata.Branch)
                    .Distinct()
                    .Count(),
                Is.EqualTo(3));
            Assert.That(
                PermanentGrowthCatalog.Branches
                    .Select(metadata => metadata.DisplayOrder)
                    .Distinct()
                    .Count(),
                Is.EqualTo(3));

            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches)
            {
                PermanentGrowthDefinition[] definitions =
                    PermanentGrowthCatalog.All
                        .Where(definition =>
                            definition.Branch == branch.Branch)
                        .ToArray();
                Assert.That(definitions, Is.Not.Empty, branch.DisplayName);
                Assert.That(
                    definitions.Count(definition => definition.IsCapstone),
                    Is.EqualTo(1),
                    branch.DisplayName);
                Assert.That(
                    definitions
                        .Select(definition => definition.BranchOrder)
                        .Distinct()
                        .Count(),
                    Is.EqualTo(definitions.Length),
                    branch.DisplayName);
            }
        }

        [Test]
        public void Catalog_기존_네_ID와_효과_비용을_보존한다()
        {
            Assert.That(
                PermanentGrowthCatalog.All
                    .Take(4)
                    .Select(definition => definition.Type),
                Is.EqualTo(new[]
                {
                    PermanentGrowthType.InkCapacity,
                    PermanentGrowthType.InkRecovery,
                    PermanentGrowthType.PlatformLifetime,
                    PermanentGrowthType.JumpCharge,
                }));

            AssertLegacyDefinition(
                PermanentGrowthType.InkCapacity,
                "permanent.ink_capacity",
                0.015f,
                false,
                6, 10, 16, 24, 34, 46);
            AssertLegacyDefinition(
                PermanentGrowthType.InkRecovery,
                "permanent.ink_recovery",
                0.02f,
                false,
                6, 10, 16, 24, 34, 46);
            AssertLegacyDefinition(
                PermanentGrowthType.PlatformLifetime,
                "permanent.platform_lifetime",
                0.0125f,
                false,
                7, 11, 17, 25, 35, 47);
            AssertLegacyDefinition(
                PermanentGrowthType.JumpCharge,
                "permanent.jump_charge",
                0.0075f,
                true,
                7, 12, 18, 26, 36, 48);
        }

        [Test]
        public void Catalog_신규_랭크와_효과가_설계값과_일치한다()
        {
            AssertNode(
                PermanentGrowthType.Vitality,
                PermanentGrowthBranch.Survival,
                1,
                1f,
                PermanentGrowthValueKind.Flat,
                false);
            AssertNode(
                PermanentGrowthType.DamageGrace,
                PermanentGrowthBranch.Survival,
                3,
                0.08f,
                PermanentGrowthValueKind.Seconds,
                false);
            AssertNode(
                PermanentGrowthType.CloneSpawnGrace,
                PermanentGrowthBranch.Survival,
                3,
                0.15f,
                PermanentGrowthValueKind.Seconds,
                false);
            AssertNode(
                PermanentGrowthType.LastBreath,
                PermanentGrowthBranch.Survival,
                1,
                1f,
                PermanentGrowthValueKind.Flat,
                true);
            AssertNode(
                PermanentGrowthType.JumpPower,
                PermanentGrowthBranch.Leap,
                5,
                0.01f,
                PermanentGrowthValueKind.Percent,
                false);
            AssertNode(
                PermanentGrowthType.DrawnPlatformLeap,
                PermanentGrowthBranch.Leap,
                1,
                0.10f,
                PermanentGrowthValueKind.Percent,
                true);
            AssertNode(
                PermanentGrowthType.StrokeGuard,
                PermanentGrowthBranch.InkHandling,
                1,
                1f,
                PermanentGrowthValueKind.Flat,
                true);
        }

        [Test]
        public void Catalog_세_계보의_선행조건과_최종패시브_조건이_설계값과_일치한다()
        {
            AssertRequirements(PermanentGrowthType.Vitality);
            AssertRequirements(
                PermanentGrowthType.DamageGrace,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.Vitality,
                    1));
            AssertRequirements(
                PermanentGrowthType.LastBreath,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.Vitality,
                    1),
                new PermanentGrowthRequirement(
                    PermanentGrowthType.DamageGrace,
                    3),
                new PermanentGrowthRequirement(
                    PermanentGrowthType.CloneSpawnGrace,
                    3));
            AssertRequirements(
                PermanentGrowthType.CloneSpawnGrace,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.Vitality,
                    1));

            AssertRequirements(PermanentGrowthType.JumpCharge);
            AssertRequirements(
                PermanentGrowthType.JumpPower,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.JumpCharge,
                    3));
            AssertRequirements(
                PermanentGrowthType.DrawnPlatformLeap,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.JumpCharge,
                    6),
                new PermanentGrowthRequirement(
                    PermanentGrowthType.JumpPower,
                    5));

            AssertRequirements(PermanentGrowthType.InkCapacity);
            AssertRequirements(
                PermanentGrowthType.InkRecovery,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.InkCapacity,
                    2));
            AssertRequirements(
                PermanentGrowthType.PlatformLifetime,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.InkRecovery,
                    2));
            AssertRequirements(
                PermanentGrowthType.StrokeGuard,
                new PermanentGrowthRequirement(
                    PermanentGrowthType.InkCapacity,
                    6),
                new PermanentGrowthRequirement(
                    PermanentGrowthType.InkRecovery,
                    6),
                new PermanentGrowthRequirement(
                    PermanentGrowthType.PlatformLifetime,
                    6));
        }

        [Test]
        public void Catalog_세_계보가_좌중우로_분리되고_각각_시각적으로_갈라진다()
        {
            PermanentGrowthNodeDefinition[] survival =
                BranchNodes(PermanentGrowthBranch.Survival);
            PermanentGrowthNodeDefinition[] ink =
                BranchNodes(PermanentGrowthBranch.InkHandling);
            PermanentGrowthNodeDefinition[] leap =
                BranchNodes(PermanentGrowthBranch.Leap);

            Assert.That(
                survival.Max(node => node.LayoutX),
                Is.LessThanOrEqualTo(-500f),
                "생존 계보는 왼쪽 큰 가지를 사용해야 합니다.");
            Assert.That(
                ink.Min(node => node.LayoutX),
                Is.GreaterThanOrEqualTo(-480f),
                "먹 계보는 가운데 큰 가지를 벗어나면 안 됩니다.");
            Assert.That(
                ink.Max(node => node.LayoutX),
                Is.LessThanOrEqualTo(480f),
                "먹 계보는 가운데 큰 가지를 벗어나면 안 됩니다.");
            Assert.That(
                leap.Min(node => node.LayoutX),
                Is.GreaterThanOrEqualTo(500f),
                "도약 계보는 오른쪽 큰 가지를 사용해야 합니다.");

            AssertVisibleFork(PermanentGrowthBranch.Survival, 220f);
            AssertVisibleFork(PermanentGrowthBranch.InkHandling, 220f);
            AssertVisibleFork(PermanentGrowthBranch.Leap, 220f);
            AssertTouchAreasDoNotOverlap();
        }

        [Test]
        public void Catalog_선행조건은_유효하고_순환하지_않는다()
        {
            var states = new Dictionary<PermanentGrowthType, int>();
            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
            {
                Assert.That(
                    PermanentGrowthCatalog.TryGet(
                        definition.Id,
                        out PermanentGrowthDefinition byId),
                    Is.True,
                    definition.Id);
                Assert.That(byId, Is.SameAs(definition));

                for (int i = 0; i < definition.Requirements.Count; i++)
                {
                    PermanentGrowthRequirement requirement =
                        definition.Requirements[i];
                    PermanentGrowthDefinition required =
                        PermanentGrowthCatalog.Get(requirement.Type);
                    Assert.That(required, Is.Not.Null, definition.Id);
                    Assert.That(
                        requirement.Type,
                        Is.Not.EqualTo(definition.Type),
                        definition.Id);
                    Assert.That(
                        requirement.MinimumLevel,
                        Is.InRange(1, required.MaxLevel),
                        definition.Id);
                }
            }

            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
                Assert.That(
                    VisitWithoutCycle(definition.Type, states),
                    Is.True,
                    definition.Id);
        }

        static bool VisitWithoutCycle(
            PermanentGrowthType type,
            IDictionary<PermanentGrowthType, int> states)
        {
            if (states.TryGetValue(type, out int state))
                return state == 2;

            states[type] = 1;
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            for (int i = 0; i < definition.Requirements.Count; i++)
            {
                PermanentGrowthType required =
                    definition.Requirements[i].Type;
                if (states.TryGetValue(required, out int requiredState) &&
                    requiredState == 1)
                    return false;
                if (!VisitWithoutCycle(required, states))
                    return false;
            }
            states[type] = 2;
            return true;
        }

        static PermanentGrowthNodeDefinition[] BranchNodes(
            PermanentGrowthBranch branch)
        {
            return PermanentGrowthCatalog.Nodes
                .Where(node => node.Branch == branch)
                .ToArray();
        }

        static void AssertVisibleFork(
            PermanentGrowthBranch branch,
            float minimumHorizontalSeparation)
        {
            PermanentGrowthNodeDefinition[] branchNodes =
                BranchNodes(branch);
            bool hasVisibleFork = branchNodes.Any(parent =>
            {
                PermanentGrowthNodeDefinition[] children = branchNodes
                    .Where(child => child.ParentIds.Contains(parent.Id))
                    .ToArray();
                if (children.Length < 2)
                    return false;
                float minimumX = children.Min(child => child.LayoutX);
                float maximumX = children.Max(child => child.LayoutX);
                return maximumX - minimumX >= minimumHorizontalSeparation;
            });

            Assert.That(
                hasVisibleFork,
                Is.True,
                $"{branch} 계보의 분기가 일직선처럼 보이면 안 됩니다.");
        }

        static void AssertTouchAreasDoNotOverlap()
        {
            for (int leftIndex = 0;
                 leftIndex < PermanentGrowthCatalog.Nodes.Count;
                 leftIndex++)
            {
                PermanentGrowthNodeDefinition left =
                    PermanentGrowthCatalog.Nodes[leftIndex];
                float leftWidth = left.IsCapstone ? 240f : 188f;
                float leftHeight = left.IsCapstone ? 260f : 218f;
                for (int rightIndex = leftIndex + 1;
                     rightIndex < PermanentGrowthCatalog.Nodes.Count;
                     rightIndex++)
                {
                    PermanentGrowthNodeDefinition right =
                        PermanentGrowthCatalog.Nodes[rightIndex];
                    float rightWidth = right.IsCapstone ? 240f : 188f;
                    float rightHeight = right.IsCapstone ? 260f : 218f;
                    bool overlaps =
                        System.Math.Abs(left.LayoutX - right.LayoutX) <
                            (leftWidth + rightWidth) * 0.5f &&
                        System.Math.Abs(left.LayoutY - right.LayoutY) <
                            (leftHeight + rightHeight) * 0.5f;
                    Assert.That(
                        overlaps,
                        Is.False,
                        $"{left.Id}와 {right.Id}의 터치 영역이 겹칩니다.");
                }
            }
        }

        static void AssertLegacyDefinition(
            PermanentGrowthType type,
            string id,
            float effectPerLevel,
            bool reducesValue,
            params int[] costs)
        {
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Id, Is.EqualTo(id));
            Assert.That(
                definition.EffectPerLevel,
                Is.EqualTo(effectPerLevel).Within(0.000001f));
            Assert.That(definition.ReducesValue, Is.EqualTo(reducesValue));
            Assert.That(definition.MaxLevel, Is.EqualTo(costs.Length));
            for (int i = 0; i < costs.Length; i++)
                Assert.That(definition.GetCost(i), Is.EqualTo(costs[i]));
        }

        static void AssertNode(
            PermanentGrowthType type,
            PermanentGrowthBranch branch,
            int maxLevel,
            float effectPerLevel,
            PermanentGrowthValueKind valueKind,
            bool capstone)
        {
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Branch, Is.EqualTo(branch));
            Assert.That(definition.MaxLevel, Is.EqualTo(maxLevel));
            Assert.That(
                definition.EffectPerLevel,
                Is.EqualTo(effectPerLevel).Within(0.000001f));
            Assert.That(definition.ValueKind, Is.EqualTo(valueKind));
            Assert.That(definition.IsCapstone, Is.EqualTo(capstone));
        }

        static void AssertRequirements(
            PermanentGrowthType type,
            params PermanentGrowthRequirement[] requirements)
        {
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.Requirements.Count,
                Is.EqualTo(requirements.Length),
                definition.Id);

            for (int i = 0; i < requirements.Length; i++)
            {
                Assert.That(
                    definition.Requirements[i].Type,
                    Is.EqualTo(requirements[i].Type),
                    $"{definition.Id} requirement {i}");
                Assert.That(
                    definition.Requirements[i].MinimumLevel,
                    Is.EqualTo(requirements[i].MinimumLevel),
                    $"{definition.Id} requirement {i}");
            }
        }
    }
}
