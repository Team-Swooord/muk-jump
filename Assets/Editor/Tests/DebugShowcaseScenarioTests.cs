#if UNITY_EDITOR
using System.Collections.Generic;
using MukJump.Core;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class DebugShowcaseScenarioTests
    {
        [TearDown]
        public void TearDown()
        {
            DebugShowcaseScenarioProfile.Select(
                DebugShowcaseScenarioId.Normal);
        }

        [Test]
        public void FourScenariosUseDistinctMapsSwarmsAndGrowthBuilds()
        {
            IReadOnlyList<DebugShowcaseScenarioDefinition> definitions =
                DebugShowcaseScenarioProfile.Definitions;
            Assert.That(definitions.Count, Is.EqualTo(4));
            Assert.That(definitions[0].DesiredLivingPlayers, Is.EqualTo(10));

            var heights = new HashSet<int>();
            var builds = new HashSet<string>();
            for (int i = 0; i < definitions.Count; i++)
            {
                DebugShowcaseScenarioDefinition definition = definitions[i];
                Assert.That(heights.Add(definition.TargetHeight), Is.True);
                Assert.That(builds.Add(
                    $"{definition.SurvivalPath}/" +
                    $"{definition.LeapPath}/{definition.InkPath}"), Is.True);
                Assert.That(definition.CreateGrowthSnapshot().OwnedNodeCount,
                    Is.EqualTo(15));
            }
        }

        [Test]
        public void SelectionIsSessionOnlyAndCanReturnToNormalPlay()
        {
            Assert.That(DebugShowcaseScenarioProfile.Select(
                DebugShowcaseScenarioId.HaetaeDescent), Is.True);
            Assert.That(
                DebugShowcaseScenarioProfile.SelectedDefinition.SpawnHaetae,
                Is.True);

            Assert.That(DebugShowcaseScenarioProfile.Select(
                DebugShowcaseScenarioId.Normal), Is.True);
            Assert.That(DebugShowcaseScenarioProfile.SelectedDefinition, Is.Null);
        }
    }
}
#endif
