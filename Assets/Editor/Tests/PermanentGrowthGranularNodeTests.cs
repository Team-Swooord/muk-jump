using System;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
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
        }

        [Test]
        public void SnapshotComposesAllAlwaysOnV3Stats()
        {
            var snapshot = new PermanentGrowthRunSnapshot(
                new[]
                {
                    "I00", "I-A1", "I-B1", "I-B2", "I-C1", "I-C2",
                    "S00", "S-A1", "S-A2", "S-A3", "S-C1",
                    "J00", "J-A1", "J-A2", "J-B1", "J-B2", "J-B3",
                    "J-C2", "J-C3",
                },
                null);

            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(1.06f).Within(0.0001f));
            Assert.That(snapshot.InkRecoveryMultiplier,
                Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(snapshot.PlatformLifetimeMultiplier,
                Is.EqualTo(1.04f).Within(0.0001f));
            Assert.That(snapshot.DamageGraceBonusSeconds,
                Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(1));
            Assert.That(snapshot.CloneSpawnGraceBonusSeconds,
                Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(snapshot.JumpChargeMultiplier,
                Is.EqualTo(0.94f).Within(0.0001f));
            Assert.That(snapshot.JumpPowerMultiplier,
                Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(snapshot.DrawnPlatformLeapMultiplier,
                Is.EqualTo(1.03f).Within(0.0001f));
            Assert.That(snapshot.MinimumPlatformPowerMultiplier,
                Is.EqualTo(0.90f).Within(0.0001f));
            Assert.That(snapshot.MaximumFallSpeedMultiplier,
                Is.EqualTo(0.96f).Within(0.0001f));
            Assert.That(snapshot.WindInfluenceMultiplier,
                Is.EqualTo(0.90f).Within(0.0001f));
        }

        [Test]
        public void OwnedKeystoneOnlyAffectsRunWhenEquippedInItsBranchSlot()
        {
            var owned = new[] { "S-KA", "S-KB", "J-KB", "I-KC" };
            var active = new Dictionary<PermanentGrowthBranch, string>
            {
                [PermanentGrowthBranch.Survival] = "S-KB",
                [PermanentGrowthBranch.Leap] = "J-KB",
                [PermanentGrowthBranch.InkHandling] = "I-KC",
            };
            var snapshot = new PermanentGrowthRunSnapshot(owned, active);

            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasStableHit, Is.True);
            Assert.That(snapshot.HasShortPlatformKeystone, Is.True);
            Assert.That(snapshot.HasSharedStrokeGuard, Is.True);
            Assert.That(snapshot.GetActiveKeystoneId(PermanentGrowthBranch.Survival),
                Is.EqualTo("S-KB"));
        }

        [Test]
        public void CloneSpawnGraceAppliesToActualCloneProtectionWindow()
        {
            SeedV2("S-C1");
            Assert.That(PermanentGrowthProfile.CloneSpawnGraceBonusSeconds,
                Is.EqualTo(0.15f).Within(0.0001f));

            var playerObject = Track(new GameObject("PermanentCloneGraceTest"));
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 1f;
            playerObject.AddComponent<CircleCollider2D>();
            var player = playerObject.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            SetField(player, "cloneSpawnGraceDuration", 1.2f);

            float configuredAt = Time.time;
            player.ConfigureAsClone(1f);
            float invulnerableUntil =
                GetField<float>(player, "damageInvulnerableUntil");

            Assert.That(player.IsRuntimeClone, Is.True);
            Assert.That(invulnerableUntil - configuredAt,
                Is.EqualTo(1.35f).Within(0.03f));
        }

        [Test]
        public void ViewBuildsEveryV3FruitAndEveryDeclaredParentEdge()
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
        }

        void SeedV2(params string[] ownedNodeIds)
        {
            string owned = ownedNodeIds == null || ownedNodeIds.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", ownedNodeIds) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":0,\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
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
