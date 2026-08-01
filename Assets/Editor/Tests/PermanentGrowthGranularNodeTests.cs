using System;
using System.Collections.Generic;
using System.Linq;
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
        MemoryPermanentGrowthStore store;
        readonly List<Object> cleanup = new();

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
        public void Catalog_기존트랙을_보존하고_정확히_39개_rank노드를_만든다()
        {
            Assert.That((int)PermanentGrowthType.CloneSpawnGrace, Is.EqualTo(10));
            Assert.That(PermanentGrowthCatalog.All.Count, Is.EqualTo(11));
            Assert.That(PermanentGrowthCatalog.Nodes.Count, Is.EqualTo(39));

            AssertTrack(
                PermanentGrowthType.InkCapacity,
                "permanent.ink_capacity",
                0.015f,
                6, 10, 16, 24, 34, 46);
            AssertTrack(
                PermanentGrowthType.InkRecovery,
                "permanent.ink_recovery",
                0.02f,
                6, 10, 16, 24, 34, 46);
            AssertTrack(
                PermanentGrowthType.PlatformLifetime,
                "permanent.platform_lifetime",
                0.0125f,
                7, 11, 17, 25, 35, 47);
            AssertTrack(
                PermanentGrowthType.JumpCharge,
                "permanent.jump_charge",
                0.0075f,
                7, 12, 18, 26, 36, 48);
            AssertTrack(
                PermanentGrowthType.Vitality,
                "permanent.vitality",
                1f,
                24);
            AssertTrack(
                PermanentGrowthType.DamageGrace,
                "permanent.damage_grace",
                0.08f,
                8, 16, 28);
            AssertTrack(
                PermanentGrowthType.LastBreath,
                "permanent.last_breath",
                1f,
                56);
            AssertTrack(
                PermanentGrowthType.JumpPower,
                "permanent.jump_power",
                0.01f,
                8, 13, 19, 27, 37);
            AssertTrack(
                PermanentGrowthType.DrawnPlatformLeap,
                "permanent.drawn_platform_leap",
                0.10f,
                52);
            AssertTrack(
                PermanentGrowthType.StrokeGuard,
                "permanent.stroke_guard",
                1f,
                56);
            AssertTrack(
                PermanentGrowthType.CloneSpawnGrace,
                "permanent.clone_spawn_grace",
                0.15f,
                8, 16, 28);

            foreach (PermanentGrowthDefinition track
                     in PermanentGrowthCatalog.All)
            {
                PermanentGrowthNodeDefinition[] trackNodes =
                    PermanentGrowthCatalog.Nodes
                        .Where(node => node.Type == track.Type)
                        .OrderBy(node => node.Rank)
                        .ToArray();
                Assert.That(
                    trackNodes.Length,
                    Is.EqualTo(track.MaxLevel),
                    track.Id);
                for (int rank = 1; rank <= track.MaxLevel; rank++)
                {
                    PermanentGrowthNodeDefinition node =
                        trackNodes[rank - 1];
                    Assert.That(node.Rank, Is.EqualTo(rank), track.Id);
                    Assert.That(
                        node.Id,
                        Is.EqualTo($"{track.Id}.rank.{rank}"),
                        track.Id);
                    Assert.That(
                        node.Cost,
                        Is.EqualTo(track.GetCost(rank - 1)),
                        node.Id);
                    Assert.That(
                        PermanentGrowthCatalog.GetNode(track.Type, rank),
                        Is.SameAs(node),
                        node.Id);
                }
            }
        }

        [Test]
        public void Graph_모든부모가_존재하고_비순환이며_세계보가_분기합류한다()
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var childCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes)
            {
                for (int i = 0; i < node.ParentIds.Count; i++)
                {
                    string parentId = node.ParentIds[i];
                    Assert.That(
                        PermanentGrowthCatalog.GetNode(parentId),
                        Is.Not.Null,
                        $"{node.Id} <- {parentId}");
                    childCounts[parentId] =
                        childCounts.TryGetValue(parentId, out int count)
                            ? count + 1
                            : 1;
                }
            }

            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes)
                Assert.That(VisitWithoutCycle(node.Id, states), Is.True, node.Id);

            foreach (PermanentGrowthBranch branch
                     in Enum.GetValues(typeof(PermanentGrowthBranch)))
            {
                bool branches = PermanentGrowthCatalog.Nodes
                    .Where(node => node.Branch == branch)
                    .Any(node =>
                        childCounts.TryGetValue(node.Id, out int count) &&
                        count > 1);
                bool merges = PermanentGrowthCatalog.Nodes
                    .Any(node =>
                        node.Branch == branch &&
                        node.ParentIds.Count > 1);
                Assert.That(branches, Is.True, $"{branch} 분기");
                Assert.That(merges, Is.True, $"{branch} 합류");
            }

            PermanentGrowthNodeDefinition lastBreath =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.LastBreath,
                    1);
            Assert.That(
                lastBreath.ParentIds,
                Is.EquivalentTo(new[]
                {
                    PermanentGrowthCatalog.GetNodeId(
                        PermanentGrowthType.Vitality,
                        1),
                    PermanentGrowthCatalog.GetNodeId(
                        PermanentGrowthType.DamageGrace,
                        3),
                    PermanentGrowthCatalog.GetNodeId(
                        PermanentGrowthType.CloneSpawnGrace,
                        3),
                }));
            Assert.That(
                PermanentGrowthCatalog.GetNode(
                        PermanentGrowthType.CloneSpawnGrace,
                        1)
                    .ParentIds,
                Is.EqualTo(new[]
                {
                    PermanentGrowthCatalog.GetNodeId(
                        PermanentGrowthType.Vitality,
                        1),
                }));
        }

        [Test]
        public void Profile_노드는_한번만_다음rank를_구매하고_선행노드를_지킨다()
        {
            Seed(PermanentGrowthCatalog.TotalCost, 0, string.Empty);
            PermanentGrowthNodeDefinition vitality =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.Vitality,
                    1);
            PermanentGrowthNodeDefinition cloneRank1 =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.CloneSpawnGrace,
                    1);
            PermanentGrowthNodeDefinition cloneRank3 =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.CloneSpawnGrace,
                    3);

            Assert.That(
                PermanentGrowthProfile.MeetsNodeRequirements(cloneRank1),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.CanPurchaseNode(cloneRank1),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(cloneRank1),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.GetNodeLockReason(cloneRank1),
                Does.Contain("먹심"));

            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(vitality),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(cloneRank1),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.CloneSpawnGrace),
                Is.EqualTo(1));

            int wallet = PermanentGrowthProfile.Currency;
            int spent = PermanentGrowthProfile.SpentCurrency;
            int saveCount = store.SaveCount;
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(cloneRank1),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.TryPurchaseNode(cloneRank3),
                Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(wallet));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(spent));
            Assert.That(store.SaveCount, Is.EqualTo(saveCount));
        }

        [Test]
        public void Profile_schema1_구저장효과와_구매노드를_회수하지_않는다()
        {
            const string ranks =
                "{\"id\":\"permanent.ink_capacity\",\"level\":2}," +
                "{\"id\":\"permanent.ink_recovery\",\"level\":1}," +
                "{\"id\":\"permanent.platform_lifetime\",\"level\":3}," +
                "{\"id\":\"permanent.jump_charge\",\"level\":4}," +
                "{\"id\":\"permanent.last_breath\",\"level\":1}";
            Seed(17, 176, ranks);

            Assert.That(
                PermanentGrowthProfile.InkCapacityMultiplier,
                Is.EqualTo(1.03f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.InkRecoveryMultiplier,
                Is.EqualTo(1.02f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.PlatformLifetimeMultiplier,
                Is.EqualTo(1.0375f).Within(0.0001f));
            Assert.That(
                PermanentGrowthProfile.JumpChargeMultiplier,
                Is.EqualTo(0.97f).Within(0.0001f));
            Assert.That(PermanentGrowthProfile.HasLastBreath, Is.True);
            Assert.That(
                PermanentGrowthProfile.IsNodeUnlocked(
                    PermanentGrowthType.InkCapacity,
                    1),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.IsNodeUnlocked(
                    PermanentGrowthType.InkCapacity,
                    2),
                Is.True);
            Assert.That(
                PermanentGrowthProfile.IsNodeUnlocked(
                    PermanentGrowthType.InkCapacity,
                    3),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.IsNodeUnlocked(
                    PermanentGrowthType.LastBreath,
                    1),
                Is.True,
                "새 분신 선행 조건을 구 저장의 구매 완료 노드에 소급하면 안 됩니다.");
        }

        [Test]
        public void CloneSpawnGrace_프로필효과가_실제분신생성무적에_더해진다()
        {
            Seed(
                0,
                52,
                "{\"id\":\"permanent.clone_spawn_grace\",\"level\":3}");
            Assert.That(
                PermanentGrowthProfile.CloneSpawnGraceBonusSeconds,
                Is.EqualTo(0.45f).Within(0.0001f));

            var playerObject = Track(
                new GameObject("PermanentCloneSpawnGraceTest"));
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
            Assert.That(
                invulnerableUntil - configuredAt,
                Is.EqualTo(1.65f).Within(0.03f));
        }

        [Test]
        public void View_39개노드와_모든부모간선을_큰드래그캔버스에_만든다()
        {
            Seed(PermanentGrowthCatalog.TotalCost, 0, string.Empty);
            var managerHost = Track(
                new GameObject("PermanentGranularViewManager"));
            managerHost.AddComponent<GameManager>();
            var viewHost = Track(
                new GameObject("PermanentGranularView"));
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(
                view.TreeCanvas.sizeDelta,
                Is.EqualTo(new Vector2(3400f, 3200f)));
            Assert.That(
                view.CreatedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.Nodes.Count));

            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                string childName = Sanitize(definition.Id);
                Transform node = view.TreeCanvas.Find(
                    $"GrowthNode_{childName}");
                Assert.That(node, Is.Not.Null, definition.Id);
                RectTransform touch = node.GetComponent<RectTransform>();
                Assert.That(touch.sizeDelta.x, Is.GreaterThanOrEqualTo(156f));
                Assert.That(touch.sizeDelta.y, Is.GreaterThanOrEqualTo(176f));
                Assert.That(node.Find("NodeName"), Is.Null);
                Assert.That(node.Find("NodeLevel"), Is.Null);
                Assert.That(node.Find("Fruit"), Is.Not.Null);

                for (int parentIndex = 0;
                     parentIndex < definition.ParentIds.Count;
                     parentIndex++)
                {
                    string parentName =
                        Sanitize(definition.ParentIds[parentIndex]);
                    Assert.That(
                        view.TreeCanvas.Find(
                            $"GrowthPath_{childName}_From_{parentName}"),
                        Is.Not.Null,
                        $"{definition.Id} line {parentIndex}");
                    Transform branchArt = view.TreeCanvas.Find(
                        $"TreeBranchArt_{childName}_From_{parentName}");
                    Assert.That(
                        branchArt,
                        Is.Not.Null,
                        $"{definition.Id} branch {parentIndex}");
                    string spriteName =
                        branchArt.GetComponent<Image>()?.sprite?.name;
                    Assert.That(
                        !string.IsNullOrEmpty(spriteName) &&
                        (spriteName.StartsWith(
                             "pg_branch_piece_",
                             StringComparison.Ordinal) ||
                         spriteName.StartsWith(
                             "pg_branch",
                             StringComparison.Ordinal)),
                        Is.True,
                        $"{definition.Id} branch sprite");
                }
            }
        }

        static bool VisitWithoutCycle(
            string nodeId,
            IDictionary<string, int> states)
        {
            if (states.TryGetValue(nodeId, out int state))
                return state == 2;

            states[nodeId] = 1;
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            for (int i = 0; i < node.ParentIds.Count; i++)
            {
                string parentId = node.ParentIds[i];
                if (states.TryGetValue(parentId, out int parentState) &&
                    parentState == 1)
                    return false;
                if (!VisitWithoutCycle(parentId, states))
                    return false;
            }
            states[nodeId] = 2;
            return true;
        }

        static void AssertTrack(
            PermanentGrowthType type,
            string id,
            float effectPerLevel,
            params int[] costs)
        {
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            Assert.That(definition, Is.Not.Null, type.ToString());
            Assert.That(definition.Id, Is.EqualTo(id));
            Assert.That(
                definition.EffectPerLevel,
                Is.EqualTo(effectPerLevel).Within(0.000001f));
            Assert.That(definition.MaxLevel, Is.EqualTo(costs.Length));
            for (int i = 0; i < costs.Length; i++)
                Assert.That(definition.GetCost(i), Is.EqualTo(costs[i]));
        }

        void Seed(int wallet, int spent, string rankObjects)
        {
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":1," +
                $"\"wallet\":{wallet},\"spent\":{spent}," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"granular-test\"," +
                $"\"ranks\":[{rankObjects}]}}";
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

        static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
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

        static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
