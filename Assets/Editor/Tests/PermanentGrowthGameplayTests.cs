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
        public void SurvivalGeneralFruitsIncreaseHealthAndDamageGrace()
        {
            SeedGrowth(
                new[] { "S00", "S-A1", "S-A2", "S-A3" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentSurvivalStats");

            Assert.That(player.MaxHealth, Is.EqualTo(4));
            Assert.That(player.CurrentHealth, Is.EqualTo(4));

            ExpireDamageGrace(player);
            float hitTime = Time.time;
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.CurrentHealth, Is.EqualTo(3));
            Assert.That(player.DamageStage, Is.EqualTo(1));
            float invulnerableUntil =
                GetField<float>(player, "damageInvulnerableUntil");
            Assert.That(invulnerableUntil - hitTime,
                Is.EqualTo(0.70f).Within(0.03f),
                "기본 0.55초와 S00/S-A1/S-A2의 0.15초가 합산되어야 합니다.");
        }

        [Test]
        public void HitStabilityGeneralFruitsReduceButNeverReverseKnockback()
        {
            SeedGrowth(new[] { "S-B1", "S-B2" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentHitStability");
            player.Body.linearVelocity = new Vector2(10f, -5f);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.Body.linearVelocity.x,
                Is.EqualTo(9f).Within(0.001f));
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(1.3f).Within(0.001f),
                "피격은 위로 과도하게 솟지 않되 장애물에서 빠질 최소 상승은 남겨야 합니다.");
        }

        [Test]
        public void LastBreathOnlyWorksForExactlyOneLivingPlayerAndResetsPerRun()
        {
            SeedGrowth(new[] { "S-KA" }, survivalKeystone: "S-KA");
            var manager = CreatePlayingManager(out var growth);
            var first = CreatePlayer("PermanentLastBreathA");
            var second = CreatePlayer("PermanentLastBreathB");
            manager.RegisterPlayer(first);
            manager.RegisterPlayer(second);

            Assert.That(growth.LastBreathAvailable, Is.True);
            Assert.That(growth.TrySurviveLethalObstacleHit(first), Is.False,
                "다른 분신이 살아 있으면 마지막 먹숨을 먼저 소비하면 안 됩니다.");

            SetProperty(first, "IsDead", true);
            Assert.That(manager.LivingPlayerCount, Is.EqualTo(1));
            Assert.That(growth.TrySurviveLethalObstacleHit(second), Is.True);
            Assert.That(growth.LastBreathAvailable, Is.False);
            Assert.That(growth.TrySurviveLethalObstacleHit(second), Is.False);

            Invoke(growth, "ResetRun");
            Assert.That(growth.LastBreathAvailable, Is.True);
        }

        [Test]
        public void AutoJumpComposesPowerAndSqrtHeightWithinBalanceCap()
        {
            SeedGrowth(
                new[]
                {
                    "J-B1", "J-B2", "J-B3",
                    "J-C1", "J-C2", "J-C3",
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
        public void SharedPermanentGuardIsConsumedBeforeHazardDestroysPlatform()
        {
            SeedGrowth(
                new[] { "I-KC" },
                inkKeystone: "I-KC");
            CreatePlayingManager(out var growth);
            PlatformCollider platform = SpawnPlatform("SharedPermanentStrokeGuard");

            Assert.That(platform.BreakFromHazard(), Is.True);
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.False);
            Assert.That(growth.TryUsePermanentStrokeGuard(), Is.False,
                "먹떼 공용 영구 비기는 첫 방어 직후 18초 재사용 대기여야 합니다.");

            Assert.That(platform.BreakFromHazard(), Is.True);
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.True,
                "같은 재사용 시간 안의 두 번째 낙묵석은 발판을 제거해야 합니다.");
        }

        [Test]
        public void StableHitKeystoneUsesSharedTwelveSecondCooldown()
        {
            SeedGrowth(
                new[] { "S-KB" },
                survivalKeystone: "S-KB");
            CreatePlayingManager(out var growth);

            Assert.That(growth.TryPreserveHitMotion(), Is.True);
            Assert.That(growth.TryPreserveHitMotion(), Is.False);
        }

        [Test]
        public void FifthRepresentativeJumpCreatesOneWaySafetyPlatform()
        {
            SeedGrowth(new[] { "J-KB" }, leapKeystone: "J-KB");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentSafetyJump");
            player.transform.position = Vector3.zero;
            manager.RegisterPlayer(player);
            var leadingPlayer = CreatePlayer("PermanentSafetyJumpLeader");
            leadingPlayer.transform.position = Vector3.up * 5f;
            manager.RegisterPlayer(leadingPlayer);

            Assert.That(
                growth.NotifyPrimaryAutomaticJump(
                    leadingPlayer,
                    new Vector2(1f, 10f)),
                Is.False,
                "카메라보다 앞선 한 마리가 먹떼 공용 카운터를 독점하면 안 됩니다.");

            for (int i = 0; i < 4; i++)
                Assert.That(
                    growth.NotifyPrimaryAutomaticJump(player, new Vector2(1f, 10f)),
                    Is.False);
            Assert.That(growth.SafetyJumpProgress, Is.EqualTo(4));
            Assert.That(
                growth.NotifyPrimaryAutomaticJump(player, new Vector2(1f, 10f)),
                Is.True);
            Assert.That(growth.SafetyJumpProgress, Is.Zero);

            PlatformCollider safety =
                GetField<PlatformCollider>(growth, "activeSafetyPlatform");
            Assert.That(safety, Is.Not.Null);
            Track(safety.gameObject);
            Assert.That(safety.IsGrowthSafetyPlatform, Is.True);
            Assert.That(safety.IsOneWayPlatform, Is.True);
            Assert.That(safety.IsTemporaryDrawnPlatform, Is.False);
            Assert.That(safety.GetComponent<EdgeCollider2D>().usedByEffector, Is.True);
            Assert.That(safety.GetComponent<PlatformEffector2D>().useOneWay, Is.True);
            Assert.That(safety.BreakFromHazard(), Is.False);

            Invoke(safety, "FadeVisual", 1f);
            LineRenderer outline = safety.transform
                .Find("BrushOutline")
                .GetComponent<LineRenderer>();
            Assert.That(outline.colorGradient.Evaluate(0.5f).a,
                Is.LessThan(0.01f),
                "안전 발판의 안쪽 획과 외곽선은 같은 진행도로 사라져야 합니다.");

            for (int i = 0; i < 5; i++)
                Assert.That(
                    growth.NotifyPrimaryAutomaticJump(
                        player,
                        new Vector2(1f, 10f)),
                    Is.False);
            Assert.That(
                GetField<PlatformCollider>(growth, "activeSafetyPlatform"),
                Is.SameAs(safety),
                "다음 5회를 채워도 기존 안전 발판의 6초 수명을 끊으면 안 됩니다.");
            Assert.That(growth.SafetyJumpProgress, Is.EqualTo(5));
        }

        [Test]
        public void DoubleJumpUsesFortyPercentOnceAndSharedCooldownBlocksRepeat()
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
            Assert.That(growth.TryReserveDoubleJump(player), Is.True);

            Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(true));
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(4f).Within(0.001f));
            Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(false));

            SetField(autoJump, "doubleJumpArmed", true);
            SetField(autoJump, "doubleJumpUsed", false);
            Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(false),
                "같은 먹떼는 12초 공용 재사용 시간을 공유해야 합니다.");
        }

        [Test]
        public void SpecialLaunchImmediatelyReleasesSharedDoubleJumpReservation()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentSpecialLaunchReservation");
            manager.RegisterPlayer(player);
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "doubleJumpArmed", true);
            Assert.That(growth.TryReserveDoubleJump(player), Is.True);

            player.LaunchToHeight(10f);

            Assert.That(
                GetField<PlayerController>(growth, "doubleJumpReservedPlayer"),
                Is.Null,
                "특수 상승이 일반 2단점프 공용 예약을 붙잡으면 안 됩니다.");
            Assert.That(GetField<bool>(autoJump, "doubleJumpArmed"), Is.False);
        }

        [Test]
        public void WallKeystoneClingsOnDescendingAutomaticFlightThenReleasesForJump()
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
                Is.EqualTo(true));
            Assert.That(player.IsWallClinging, Is.True);
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(player.Body.gravityScale, Is.Zero);

            player.ReleaseWallClingForAutomaticJump();
            Assert.That(player.IsWallClinging, Is.False);
            Assert.That(player.Body.gravityScale, Is.EqualTo(1f));
        }

        [Test]
        public void LowInkRecoveryCountsReserveAsUsableInk()
        {
            SeedGrowth(new[] { "I-KB" }, inkKeystone: "I-KB");
            CreatePlayingManager(out _);
            var host = Track(new GameObject("PermanentLowInkRecovery"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", 12f);
            SetField(stroke, "ink", 0f);
            SetField(stroke, "inkReserve", 12f);

            Invoke(stroke, "UpdateLowInkRecoveryState");
            Assert.That(GetField<bool>(stroke, "lowInkRecoveryActive"), Is.False,
                "여유 먹이 충분하면 기본 벼루가 비어도 저먹 회복을 켜면 안 됩니다.");

            SetField(stroke, "inkReserve", 0f);
            SetField(stroke, "ink", 2f);
            Invoke(stroke, "UpdateLowInkRecoveryState");
            Assert.That(GetField<bool>(stroke, "lowInkRecoveryActive"), Is.True);
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
