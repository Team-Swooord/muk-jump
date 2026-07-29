using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class RoguelikeGrowthCatalogTests
    {
        static readonly Regex StableIdPattern =
            new("^[a-z0-9]+(?:[._][a-z0-9]+)*$", RegexOptions.CultureInvariant);

        [Test]
        public void CatalogContainsExactlyOneHundredUniqueStableIds()
        {
            IReadOnlyList<RoguelikeGrowthDefinition> all =
                RoguelikeGrowthCatalog.All;

            Assert.That(all, Has.Count.EqualTo(100));
            Assert.That(
                all.Select(definition => definition.Id).Distinct().Count(),
                Is.EqualTo(100));

            for (int i = 0; i < all.Count; i++)
            {
                Assert.That(
                    StableIdPattern.IsMatch(all[i].Id),
                    Is.True,
                    $"{all[i].Id}는 저장 데이터에 사용할 stable ID 형식이 아닙니다.");
                Assert.That(
                    RoguelikeGrowthCatalog.IndexOf(all[i].Id),
                    Is.EqualTo(i),
                    $"{all[i].Id}의 도감 정렬 인덱스가 안정적이지 않습니다.");
            }

            Assert.That(RoguelikeGrowthCatalog.IndexOf(null), Is.EqualTo(-1));
            Assert.That(RoguelikeGrowthCatalog.IndexOf("growth.unknown"), Is.EqualTo(-1));
        }

        [Test]
        public void CatalogContainsTwentyFiveFourNodeFamilies()
        {
            var families = RoguelikeGrowthCatalog.All
                .GroupBy(definition => definition.FamilyId)
                .ToArray();

            Assert.That(families, Has.Length.EqualTo(25));
            foreach (var family in families)
            {
                RoguelikeGrowthDefinition[] nodes = family.ToArray();
                Assert.That(nodes, Has.Length.EqualTo(4), family.Key);
                Assert.That(
                    nodes.Count(node => node.Tier == NodeTier.Root),
                    Is.EqualTo(1),
                    $"{family.Key}의 뿌리는 하나여야 합니다.");
                Assert.That(
                    nodes.Count(node => node.Tier == NodeTier.Branch),
                    Is.EqualTo(2),
                    $"{family.Key}의 가지는 둘이어야 합니다.");
                Assert.That(
                    nodes.Count(node => node.Tier == NodeTier.Completion),
                    Is.EqualTo(1),
                    $"{family.Key}의 완성은 하나여야 합니다.");
            }
        }

        [Test]
        public void BranchesAreMutuallyExclusiveAndCompletionAcceptsEitherBranch()
        {
            foreach (var family in RoguelikeGrowthCatalog.All
                         .GroupBy(definition => definition.FamilyId))
            {
                RoguelikeGrowthDefinition root =
                    family.Single(node => node.Tier == NodeTier.Root);
                RoguelikeGrowthDefinition[] branches = family
                    .Where(node => node.Tier == NodeTier.Branch)
                    .ToArray();
                RoguelikeGrowthDefinition completion =
                    family.Single(node => node.Tier == NodeTier.Completion);

                CollectionAssert.AreEquivalent(
                    new[] { root.Id },
                    branches[0].RequiredPrerequisiteIds,
                    branches[0].Id);
                CollectionAssert.AreEquivalent(
                    new[] { root.Id },
                    branches[1].RequiredPrerequisiteIds,
                    branches[1].Id);
                CollectionAssert.AreEquivalent(
                    new[] { branches[1].Id },
                    branches[0].ExclusionIds,
                    branches[0].Id);
                CollectionAssert.AreEquivalent(
                    new[] { branches[0].Id },
                    branches[1].ExclusionIds,
                    branches[1].Id);

                CollectionAssert.AreEquivalent(
                    new[] { root.Id },
                    completion.RequiredPrerequisiteIds,
                    completion.Id);
                CollectionAssert.AreEquivalent(
                    branches.Select(branch => branch.Id).ToArray(),
                    completion.AlternativePrerequisiteIds,
                    $"{completion.Id}는 두 가지 중 하나를 선행으로 받아야 합니다.");
                CollectionAssert.AreEquivalent(
                    new[] { root.Id, branches[0].Id, branches[1].Id },
                    completion.PrerequisiteIds,
                    $"{completion.Id}의 UI용 평탄화 선행 목록이 잘못됐습니다.");
            }
        }

        [Test]
        public void PrerequisitesAndExclusionsHaveNoDanglingReferences()
        {
            var ids = new HashSet<string>(
                RoguelikeGrowthCatalog.All.Select(definition => definition.Id),
                StringComparer.Ordinal);

            foreach (RoguelikeGrowthDefinition definition in
                     RoguelikeGrowthCatalog.All)
            {
                AssertReferencesExist(
                    definition,
                    definition.PrerequisiteIds,
                    "선행",
                    ids);
                AssertReferencesExist(
                    definition,
                    definition.ExclusionIds,
                    "상충",
                    ids);
            }
        }

        [Test]
        public void ExactlyEightRuntimeReadyRootsRoundTripThroughLegacyAdapter()
        {
            GrowthUpgradeType[] runtimeTypes =
                Enum.GetValues(typeof(GrowthUpgradeType))
                    .Cast<GrowthUpgradeType>()
                    .ToArray();
            IReadOnlyList<RoguelikeGrowthDefinition> ready =
                RoguelikeGrowthCatalog.RuntimeReady;

            Assert.That(ready, Has.Count.EqualTo(8));
            Assert.That(ready, Has.All.Matches<RoguelikeGrowthDefinition>(
                definition =>
                    definition.Status == ImplementationStatus.RuntimeReady &&
                    definition.Tier == NodeTier.Root &&
                    definition.RuntimeType.HasValue));
            Assert.That(
                RoguelikeGrowthCatalog.All.Count(
                    definition =>
                        definition.Status == ImplementationStatus.Planned),
                Is.EqualTo(92));

            foreach (GrowthUpgradeType runtimeType in runtimeTypes)
            {
                Assert.That(
                    RoguelikeGrowthCatalog.TryGetDefinition(
                        runtimeType,
                        out RoguelikeGrowthDefinition definition),
                    Is.True,
                    $"{runtimeType} 어댑터가 없습니다.");
                Assert.That(definition.RuntimeType, Is.EqualTo(runtimeType));
                Assert.That(
                    RoguelikeGrowthCatalog.TryGetRuntimeType(
                        definition.Id,
                        out GrowthUpgradeType roundTrip),
                    Is.True);
                Assert.That(roundTrip, Is.EqualTo(runtimeType));
                Assert.That(
                    RoguelikeGrowthCatalog.TryGet(
                        definition.Id,
                        out RoguelikeGrowthDefinition byId),
                    Is.True);
                Assert.That(byId, Is.SameAs(definition));
            }

            Assert.That(runtimeTypes, Has.Length.EqualTo(8));
        }

        [Test]
        public void EveryDefinitionHasUiAndBalanceMetadata()
        {
            foreach (RoguelikeGrowthDefinition definition in
                     RoguelikeGrowthCatalog.All)
            {
                Assert.That(definition.FamilyName, Is.Not.Empty, definition.Id);
                Assert.That(definition.Name, Is.Not.Empty, definition.Id);
                Assert.That(definition.Description, Is.Not.Empty, definition.Id);
                Assert.That(definition.Effect, Is.EqualTo(definition.Description));
                Assert.That(definition.Synergy, Is.Not.Empty, definition.Id);
                Assert.That(definition.UnlockHint, Is.Not.Empty, definition.Id);
                Assert.That(definition.MaxLevel, Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public void ValidationApiReportsAValidCatalog()
        {
            RoguelikeGrowthCatalogValidation validation =
                RoguelikeGrowthCatalog.Validate();

            Assert.That(
                validation.IsValid,
                Is.True,
                string.Join("\n", validation.Errors));
            Assert.That(validation.Errors, Is.Empty);
            Assert.That(
                RoguelikeGrowthCatalog.TryValidate(
                    out IReadOnlyList<string> errors),
                Is.True);
            Assert.That(errors, Is.Empty);
        }

        static void AssertReferencesExist(
            RoguelikeGrowthDefinition owner,
            IReadOnlyList<string> references,
            string label,
            HashSet<string> ids)
        {
            for (int i = 0; i < references.Count; i++)
            {
                Assert.That(references[i], Is.Not.EqualTo(owner.Id));
                Assert.That(
                    ids.Contains(references[i]),
                    Is.True,
                    $"{owner.Id}의 {label} 참조 {references[i]}가 없습니다.");
            }
        }
    }
}
