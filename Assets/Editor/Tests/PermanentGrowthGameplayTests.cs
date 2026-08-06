using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    /// 영구 성장 스냅샷이 실제 피해·자동 점프·발판 방어 규칙까지 이어지는지 검증한다.
    public sealed class PermanentGrowthGameplayTests
    {
        readonly List<Object> cleanup = new();
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            ClearActivePlatforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            ClearActivePlatforms();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void SurvivalVitalityPathRaisesHealthFromOneToFive()
        {
            SeedGrowth(
                new[] { "S-KA" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentSurvivalStats");

            Assert.That(player.MaxHealth, Is.EqualTo(5));
            Assert.That(player.CurrentHealth, Is.EqualTo(5));

            ExpireDamageGrace(player);
            float hitTime = Time.time;
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.CurrentHealth, Is.EqualTo(4));
            Assert.That(player.DamageStage, Is.EqualTo(1));
            float invulnerableUntil =
                GetField<float>(player, "damageInvulnerableUntil");
            Assert.That(invulnerableUntil - hitTime,
                Is.EqualTo(0.59f).Within(0.03f),
                "공용 생존 뿌리의 +0.04초만 더해져야 합니다.");

            for (int expected = 3; expected >= 0; expected--)
            {
                ExpireDamageGrace(player);
                Assert.That(player.TakeHit(), Is.True);
                Assert.That(player.CurrentHealth, Is.EqualTo(expected));
                Assert.That(player.DamageStage, Is.EqualTo(5 - expected));
                Assert.That(player.IsDead, Is.EqualTo(expected == 0));
            }
        }

        [Test]
        public void FiveHealthFallRecoversFourTimesThenFinalFallKills()
        {
            SeedGrowth(new[] { "S-KA" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentFallVitality");
            var cameraObject = Track(new GameObject("PermanentFallCamera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.orthographicSize = 5f;
            SetField(player, "cam", camera);
            SetField(player, "camHalfHeight", camera.orthographicSize);

            for (int expected = 4; expected >= 0; expected--)
            {
                player.Body.position = new Vector2(1.25f, -20f);
                Invoke(player, "HandleFallBelowView");

                Assert.That(player.CurrentHealth, Is.EqualTo(expected));
                Assert.That(player.IsDead, Is.EqualTo(expected == 0));
                if (expected > 0)
                {
                    Assert.That(player.Body.position.y,
                        Is.EqualTo(5.8f).Within(0.001f));
                    Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(0f));
                }
            }
        }

        [Test]
        public void DamageGraceLineDoesNotChangeDefaultKnockbackMotion()
        {
            SeedGrowth(new[] { "S-B1", "S-B2" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentHitStability");
            player.Body.linearVelocity = new Vector2(10f, -5f);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.Body.linearVelocity.x,
                Is.EqualTo(8.2f).Within(0.001f));
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(1.6f).Within(0.001f));
            float invulnerableUntil =
                GetField<float>(player, "damageInvulnerableUntil");
            Assert.That(invulnerableUntil - Time.time,
                Is.EqualTo(0.67f).Within(0.04f));
        }

        [Test]
        public void RemovedLastBreathNeverActivates()
        {
            SeedGrowth(new[] { "S-KA" }, survivalKeystone: "S-KA");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentNoLastBreath");
            manager.RegisterPlayer(player);

            Assert.That(growth.LastBreathAvailable, Is.False);
            Assert.That(growth.TrySurviveLethalObstacleHit(player), Is.False);
        }

        [Test]
        public void AutoJumpComposesPowerAndSqrtHeightWithinBalanceCap()
        {
            SeedGrowth(
                new[]
                {
                    "J-KB", "J-KC",
                });
            CreatePlayingManager(out _);

            var player = CreatePlayer("PermanentJumpPlayer");
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "baseJumpSpeed", 10f);
            SetField(autoJump, "jumpStrengthMultiplier", 1f);
            SetField(autoJump, "platformLengthRange", new Vector2(1f, 5f));
            SetField(autoJump, "powerMultiplierRange", new Vector2(0.85f, 1.3f));
            SetField(autoJump, "horizontalMomentumRetention", 0f);
            SetField(autoJump, "flatPlatformWanderSpeed", 0f);
            SetField(autoJump, "normalInfluence", 0f);
            SetProperty(player, "GroundNormal", Vector2.up);

            SetProperty(player, "CurrentPlatform", null);
            Invoke(autoJump, "Jump");
            float expected = 10f * 1.05f * Mathf.Sqrt(1.0625f);
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected).Within(0.001f));
            Assert.That(expected / 10f, Is.LessThan(1.30f));

            PlatformCollider platform = SpawnPlatform("PermanentShortPlatform");
            SetProperty(player, "CurrentPlatform", platform);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(autoJump, "Jump");

            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected * 0.85f).Within(0.001f),
                "도약 v4는 그린 발판에 별도 힘을 중복하지 않습니다.");
        }

        [Test]
        public void InkRecoveryLineSpeedsGaugeReturnWithoutBlockingHazards()
        {
            SeedGrowth(
                new[] { "I-KC" },
                inkKeystone: "I-KC");
            CreatePlayingManager(out var growth);
            PlatformCollider platform = SpawnPlatform("SharedPermanentStrokeGuard");

            Assert.That(growth.PermanentSnapshot.InkRecoverySpeedMultiplier,
                Is.EqualTo(1.40f).Within(0.0001f));
            Assert.That(platform.BreakFromHazard(), Is.True);
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.True,
                "먹 회복 성장은 게이지 반환만 빠르게 하며 낙묵석은 막지 않습니다.");
        }

        [Test]
        public void RemovedStableHitNeverActivates()
        {
            SeedGrowth(
                new[] { "S-KB" },
                survivalKeystone: "S-KB");
            CreatePlayingManager(out var growth);

            Assert.That(growth.TryPreserveHitMotion(), Is.False);
        }

        [Test]
        public void RemovedSafetyPlatformNeverSpawns()
        {
            SeedGrowth(new[] { "J-KB" }, leapKeystone: "J-KB");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentSafetyJump");
            player.transform.position = Vector3.zero;
            manager.RegisterPlayer(player);
            var leadingPlayer = CreatePlayer("PermanentSafetyJumpLeader");
            leadingPlayer.transform.position = Vector3.up * 5f;
            manager.RegisterPlayer(leadingPlayer);

            for (int i = 0; i < 10; i++)
                Assert.That(
                    growth.NotifyPrimaryAutomaticJump(player, new Vector2(1f, 10f)),
                    Is.False);
            Assert.That(growth.SafetyJumpProgress, Is.Zero);
        }

        [Test]
        public void RemovedDoubleJumpCannotReserveOrPerform()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentDoubleJump");
            manager.RegisterPlayer(player);
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            player.BeginAutomaticJumpFlight();
            SetField(autoJump, "primaryJumpVerticalSpeed", 10f);
            SetField(autoJump, "doubleJumpArmed", true);
            SetField(autoJump, "doubleJumpUsed", false);
            Assert.That(growth.TryReserveDoubleJump(player), Is.False);
            Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(false));
        }

        [Test]
        public void SpecialLaunchKeepsRemovedDoubleJumpUnarmed()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentSpecialLaunchReservation");
            manager.RegisterPlayer(player);
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "doubleJumpArmed", true);
            Assert.That(growth.TryReserveDoubleJump(player), Is.False);

            player.LaunchToHeight(10f);

            Assert.That(
                GetField<PlayerController>(growth, "doubleJumpReservedPlayer"),
                Is.Null,
                "특수 상승이 일반 2단점프 공용 예약을 붙잡으면 안 됩니다.");
            Assert.That(GetField<bool>(autoJump, "doubleJumpArmed"), Is.False);
        }

        [Test]
        public void RemovedWallClingNeverStarts()
        {
            SeedGrowth(new[] { "J-KA" }, leapKeystone: "J-KA");
            var manager = CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentWallCling");
            manager.RegisterPlayer(player);
            SetField(player, "wallClingMinimumDuration", 0f);
            player.BeginAutomaticJumpFlight();
            player.Body.linearVelocity = new Vector2(-2f, -2f);

            var wallObject = Track(new GameObject("TestLeftWall"));
            var wall = wallObject.AddComponent<ScreenSideWall>();
            wall.Initialize(null, true);
            Assert.That(
                Invoke(player, "TryBeginWallCling", wall, 1f),
                Is.EqualTo(false));
            Assert.That(player.IsWallClinging, Is.False);
            Assert.That(player.Body.gravityScale, Is.EqualTo(1f));
        }

        [Test]
        public void InkReserveStacksAsAdditionalRetainedCapacity()
        {
            var host = Track(new GameObject("PermanentInkReserve"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", StrokeCapture.DefaultInkCapacity);

            stroke.AddInkReserve(StrokeCapture.InkReserveItemRatio);
            stroke.AddInkReserve(StrokeCapture.InkReserveItemRatio);

            Assert.That(stroke.EffectiveInkCapacity,
                Is.EqualTo(4.8f).Within(0.0001f));
            Assert.That(stroke.InkCapacityBonusRatio,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(stroke.InkCapacityRatio,
                Is.EqualTo(1.5f).Within(0.0001f),
                "HUD 최대 먹자리 폭도 붓 여유 누적을 그대로 반영해야 합니다.");
        }

        [Test]
        public void InkReserveAddsOneQuarterOfBaseWithoutGrowthScaling()
        {
            SeedGrowth(new[] { "I-KA" });
            CreatePlayingManager(out _);
            var host = Track(new GameObject("PermanentFixedInkReserve"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", StrokeCapture.DefaultInkCapacity);

            Assert.That(stroke.BaseEffectiveInkCapacity,
                Is.EqualTo(8.0f).Within(0.0001f));

            stroke.AddInkReserve(StrokeCapture.InkReserveItemRatio);

            Assert.That(stroke.EffectiveInkCapacity,
                Is.EqualTo(8.8f).Within(0.0001f),
                "붓 여유는 성장된 총량의 25%가 아니라 기본 3.2m의 25%만 더해야 합니다.");
        }

        GameManager CreatePlayingManager(out RunGrowthController growth)
        {
            var host = Track(new GameObject("PermanentGrowthGameManager"));
            var manager = host.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            growth = host.GetComponent<RunGrowthController>();
            if (growth == null)
                growth = host.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.PermanentSnapshot, Is.Not.Null);
            return manager;
        }

        PlayerController CreatePlayer(string objectName)
        {
            var host = Track(new GameObject(objectName));
            host.AddComponent<SpriteRenderer>();
            host.AddComponent<Rigidbody2D>().gravityScale = 1f;
            host.AddComponent<CircleCollider2D>().radius = 0.4f;
            var player = host.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            ExpireDamageGrace(player);
            return player;
        }

        PlatformCollider SpawnPlatform(string objectName)
        {
            PlatformCollider platform = PlatformCollider.Spawn(new List<Vector2>
            {
                Vector2.zero,
                Vector2.right,
            });
            platform.gameObject.name = objectName;
            Track(platform.gameObject);
            return platform;
        }

        void SeedGrowth(
            string[] ownedNodeIds,
            string survivalKeystone = "",
            string leapKeystone = "",
            string inkKeystone = "")
        {
            string[] validOwned = ExpandWithRequiredParents(ownedNodeIds);
            string owned = validOwned.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", validOwned) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":0,\"spent\":0," +
                "\"tutorialRewardClaimed\":false," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                "\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                $"\"survivalKeystoneId\":\"{survivalKeystone}\"," +
                $"\"leapKeystoneId\":\"{leapKeystone}\"," +
                $"\"inkHandlingKeystoneId\":\"{inkKeystone}\"}}";
            PermanentGrowthProfile.ResetCacheForTests();
        }

        static string[] ExpandWithRequiredParents(IEnumerable<string> requestedIds)
        {
            var requested = new HashSet<string>(
                requestedIds ?? System.Array.Empty<string>(),
                System.StringComparer.Ordinal);
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

        static void ExpireDamageGrace(PlayerController player)
        {
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
            return method.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            return (T)field.GetValue(target);
        }

        static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"{target.GetType().Name}.{propertyName} 속성을 찾을 수 없습니다.");
            property.SetValue(target, value);
        }

        static void ClearActivePlatforms()
        {
            FieldInfo field = typeof(PlatformCollider).GetField(
                "active",
                BindingFlags.Static | BindingFlags.NonPublic);
            var platforms = field?.GetValue(null) as List<PlatformCollider>;
            platforms?.Clear();
        }
    }
}
