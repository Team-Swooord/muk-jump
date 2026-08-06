using System;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthGranularNodeTests
    {
        readonly List<Object> cleanup = new();
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
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

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
        }

        [Test]
        public void SnapshotComposesNineSingleStatTracksWithoutLegacyBonuses()
        {
            var snapshot = new PermanentGrowthRunSnapshot(
                new[]
                {
                    "I00", "I-A1", "I-A2", "I-A3", "I-KA",
                    "I-B1", "I-B2", "I-B3", "I-KB",
                    "I-C1", "I-C2", "I-C3", "I-KC",
                    "S00", "S-A1", "S-A2", "S-A3", "S-KA",
                    "S-B1", "S-B2", "S-B3", "S-KB",
                    "S-C1", "S-C2", "S-C3", "S-KC",
                    "J00", "J-A1", "J-A2", "J-A3", "J-KA",
                    "J-B1", "J-B2", "J-B3", "J-KB",
                    "J-C1", "J-C2", "J-C3", "J-KC",
                },
                null);

            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(2.50f).Within(0.0001f));
            Assert.That(snapshot.InkBudgetCostMultiplier,
                Is.EqualTo(0.90f).Within(0.0001f));
            Assert.That(snapshot.ShortStrokeBudgetCostMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(snapshot.InkRecoverySpeedMultiplier,
                Is.EqualTo(1.40f).Within(0.0001f));
            Assert.That(snapshot.InkEvictionFadeBonusSeconds,
                Is.Zero.Within(0.0001f));
            Assert.That(snapshot.InkEvictionDelaySeconds,
                Is.Zero.Within(0.0001f));
            Assert.That(snapshot.HasShortStrokeDiscount, Is.False);
            Assert.That(snapshot.DamageGraceBonusSeconds,
                Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(4));
            Assert.That(snapshot.InkCloneItemExtraCount, Is.EqualTo(4));
            Assert.That(snapshot.JumpChargeMultiplier,
                Is.EqualTo(0.94f).Within(0.0001f));
            Assert.That(snapshot.JumpPowerMultiplier,
                Is.EqualTo(1.05f).Within(0.0001f));
            Assert.That(snapshot.JumpHeightMultiplier,
                Is.EqualTo(1.0625f).Within(0.0001f));
            Assert.That(snapshot.JumpVerticalSpeedMultiplier,
                Is.EqualTo(Mathf.Sqrt(1.0625f)).Within(0.0001f));
            Assert.That(snapshot.DrawnPlatformLeapMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(snapshot.MinimumPlatformPowerMultiplier,
                Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(snapshot.MaximumFallSpeedMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(snapshot.WindInfluenceMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void SurvivalVitalityPathAddsExactlyOneHealthPerOwnedNode(
            int unlockedCount,
            int expectedBonus)
        {
            string[] path = { "S-A1", "S-A2", "S-A3", "S-KA" };
            var owned = new List<string>(unlockedCount);
            for (int i = 0; i < unlockedCount; i++)
                owned.Add(path[i]);

            var snapshot = new PermanentGrowthRunSnapshot(owned, null);

            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(expectedBonus));
            Assert.That(
                PlayerController.DefaultMaxHealth + snapshot.MaxHealthBonus,
                Is.EqualTo(unlockedCount + 1));
        }

        [TestCase(0, 1.0f, 3.20f)]
        [TestCase(1, 1.375f, 4.40f)]
        [TestCase(2, 1.75f, 5.60f)]
        [TestCase(3, 2.125f, 6.80f)]
        [TestCase(4, 2.5f, 8.00f)]
        public void InkCapacityPathGrowsOnePracticalStrokeToTwoPointFive(
            int unlockedCount,
            float expectedMultiplier,
            float expectedCapacity)
        {
            string[] path = { "I-A1", "I-A2", "I-A3", "I-KA" };
            var owned = new List<string>(unlockedCount);
            for (int i = 0; i < unlockedCount; i++)
                owned.Add(path[i]);
            Dictionary<PermanentGrowthBranch, string> active = null;
            if (unlockedCount >= path.Length)
            {
                active = new Dictionary<PermanentGrowthBranch, string>
                {
                    [PermanentGrowthBranch.InkHandling] = "I-KA",
                };
            }
            var snapshot = new PermanentGrowthRunSnapshot(owned, active);

            Assert.That(StrokeCapture.DefaultInkCapacity,
                Is.EqualTo(3.2f).Within(0.0001f));
            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(expectedMultiplier).Within(0.0001f));
            Assert.That(
                StrokeCapture.DefaultInkCapacity * snapshot.InkCapacityMultiplier,
                Is.EqualTo(expectedCapacity).Within(0.0001f));
        }

        [Test]
        public void InkRecoveryPathShortensFadeWithoutExtendingNaturalHold()
        {
            var regular = new PermanentGrowthRunSnapshot(
                new[] { "I00", "I-C1", "I-C2", "I-C3" },
                null);
            var keystone = new PermanentGrowthRunSnapshot(
                new[] { "I00", "I-C1", "I-C2", "I-C3", "I-KC" },
                new Dictionary<PermanentGrowthBranch, string>
                {
                    [PermanentGrowthBranch.InkHandling] = "I-KC",
                });

            Assert.That(regular.InkRecoverySpeedMultiplier,
                Is.EqualTo(1.30f).Within(0.0001f));
            Assert.That(keystone.InkRecoverySpeedMultiplier,
                Is.EqualTo(1.40f).Within(0.0001f));
            Assert.That(1.1f / regular.InkRecoverySpeedMultiplier,
                Is.EqualTo(0.8461538f).Within(0.0001f));
            Assert.That(1.1f / keystone.InkRecoverySpeedMultiplier,
                Is.EqualTo(0.7857143f).Within(0.0001f));
            Assert.That(PlatformCollider.DefaultNaturalHoldDuration,
                Is.EqualTo(3.4f).Within(0.0001f));
        }

        [Test]
        public void OwnedLineEndNodesApplyAsStatsWithoutSpecialPassives()
        {
            var owned = new[] { "S-KA", "S-KB", "J-KB", "I-KC" };
            var active = new Dictionary<PermanentGrowthBranch, string>
            {
                [PermanentGrowthBranch.Survival] = "S-KB",
                [PermanentGrowthBranch.Leap] = "J-KB",
                [PermanentGrowthBranch.InkHandling] = "I-KC",
            };
            var snapshot = new PermanentGrowthRunSnapshot(owned, active);

            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(1));
            Assert.That(snapshot.DamageGraceBonusSeconds,
                Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(snapshot.JumpPowerMultiplier,
                Is.EqualTo(1.01f).Within(0.0001f));
            Assert.That(snapshot.InkRecoverySpeedMultiplier,
                Is.EqualTo(1.10f).Within(0.0001f));
            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasStableHit, Is.False);
            Assert.That(snapshot.HasSafetyPlatform, Is.False);
            Assert.That(snapshot.GetActiveKeystoneId(PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KB"));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void CloneItemLineAddsOneExtraClonePerOwnedNode(
            int unlockedCount,
            int expectedExtraCount)
        {
            string[] path = { "S-C1", "S-C2", "S-C3", "S-KC" };
            var owned = new List<string>();
            for (int i = 0; i < unlockedCount; i++)
                owned.Add(path[i]);
            var snapshot = new PermanentGrowthRunSnapshot(owned, null);

            Assert.That(snapshot.InkCloneItemExtraCount,
                Is.EqualTo(expectedExtraCount));
            Assert.That(1 + snapshot.InkCloneItemExtraCount,
                Is.EqualTo(1 + unlockedCount));
        }

        [Test]
        public void ViewBuildsEveryFruitAndColorsTheThreeLeapPathsDifferently()
        {
            SeedV2();
            var managerHost = Track(new GameObject("PermanentV3ViewManager"));
            managerHost.AddComponent<GameManager>();
            var viewHost = Track(new GameObject("PermanentV3View"));
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(view.TreeCanvas, Is.Not.Null);
            Assert.That(view.TreeCanvas.sizeDelta.x, Is.GreaterThan(1080f));
            Assert.That(view.TreeCanvas.sizeDelta.y, Is.GreaterThan(1920f));
            Assert.That(view.CreatedNodeCount, Is.EqualTo(39));

            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                string childName = Sanitize(definition.Id);
                Transform node = view.TreeCanvas.Find($"GrowthNode_{childName}");
                Assert.That(node, Is.Not.Null, definition.Id);
                RectTransform touch = node.GetComponent<RectTransform>();
                Assert.That(touch.sizeDelta.x, Is.GreaterThanOrEqualTo(132f));
                Assert.That(touch.sizeDelta.y, Is.GreaterThanOrEqualTo(132f));
                Button button = node.GetComponent<Button>();
                Image hitSurface = node.GetComponent<Image>();
                Assert.That(button, Is.Not.Null, $"{definition.Id} Button");
                Assert.That(hitSurface, Is.Not.Null, $"{definition.Id} hit surface");
                Assert.That(hitSurface.raycastTarget, Is.True, definition.Id);
                float narrowScreenZoom =
                    PermanentGrowthView.CalculateTreeZoomForTests(1080f, 2400f);
                Assert.That(
                    touch.sizeDelta.x * narrowScreenZoom,
                    Is.GreaterThanOrEqualTo(96f),
                    $"{definition.Id} 20:9 유효 터치 폭");
                Assert.That(
                    touch.sizeDelta.y * narrowScreenZoom,
                    Is.GreaterThanOrEqualTo(96f),
                    $"{definition.Id} 20:9 유효 터치 높이");
                Assert.That(node.Find("Fruit"), Is.Not.Null, definition.Id);

                foreach (string parentId in definition.ParentIds)
                {
                    string parentName = Sanitize(parentId);
                    Assert.That(
                        view.TreeCanvas.Find(
                            $"GrowthPath_{childName}_From_{parentName}"),
                        Is.Not.Null,
                        $"{definition.Id} path from {parentId}");
                    Transform branchArt = view.TreeCanvas.Find(
                        $"TreeBranchArt_{childName}_From_{parentName}");
                    Assert.That(branchArt, Is.Not.Null,
                        $"{definition.Id} branch from {parentId}");
                    string spriteName = branchArt.GetComponent<Image>()?.sprite?.name;
                    Assert.That(
                        !string.IsNullOrEmpty(spriteName) &&
                        spriteName.StartsWith("pg_branch", StringComparison.Ordinal),
                        Is.True,
                        $"{definition.Id} branch sprite");
                }
            }

            Color rhythm = view.TreeCanvas
                .Find("GrowthPath_J_A1_From_J00")
                .GetComponent<Image>().color;
            Color power = view.TreeCanvas
                .Find("GrowthPath_J_B1_From_J00")
                .GetComponent<Image>().color;
            Color height = view.TreeCanvas
                .Find("GrowthPath_J_C1_From_J00")
                .GetComponent<Image>().color;
            Assert.That(Vector3.Distance(ToRgb(rhythm), ToRgb(power)),
                Is.GreaterThan(0.08f));
            Assert.That(Vector3.Distance(ToRgb(power), ToRgb(height)),
                Is.GreaterThan(0.08f));
            Assert.That(rhythm.a, Is.GreaterThanOrEqualTo(0.1f));
        }

        static Vector3 ToRgb(Color color) =>
            new(color.r, color.g, color.b);

        void SeedV2(params string[] ownedNodeIds)
        {
            string[] validOwned = ExpandWithRequiredParents(ownedNodeIds);
            string owned = validOwned.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", validOwned) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":0,\"spent\":0," +
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

        static string[] ExpandWithRequiredParents(IEnumerable<string> requestedIds)
        {
            var requested = new HashSet<string>(
                requestedIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var stack = new Stack<string>(requested);
            while (stack.Count > 0)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(stack.Pop());
                if (node == null)
                    continue;
                for (int i = 0; i < node.ParentIds.Count; i++)
                    if (requested.Add(node.ParentIds[i]))
                        stack.Push(node.ParentIds[i]);
            }

            var ordered = new List<string>(requested.Count);
            for (int i = 0; i < PermanentGrowthCatalog.Nodes.Count; i++)
            {
                string id = PermanentGrowthCatalog.Nodes[i].Id;
                if (requested.Contains(id))
                    ordered.Add(id);
            }
            return ordered.ToArray();
        }

        T Track<T>(T value) where T : Object
        {
            cleanup.Add(value);
            return value;
        }

        static string Sanitize(string id)
        {
            char[] characters = id.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsLetterOrDigit(characters[i]))
                    characters[i] = '_';
            return new string(characters);
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
