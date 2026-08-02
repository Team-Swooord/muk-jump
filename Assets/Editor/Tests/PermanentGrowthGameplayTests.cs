using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    /// v3 영구 성장 스냅샷이 실제 피해·자동 점프·발판 방어 규칙까지 이어지는지 검증한다.
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
        public void AutoJumpMultipliesRunAndPermanentPowerWithinV3Cap()
        {
            SeedGrowth(
                new[] { "J-B1", "J-B2", "J-B3", "J-KB" },
                leapKeystone: "J-KB");
            CreatePlayingManager(out var growth);
            SetProperty(growth, "JumpLevel", 1);

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
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(10f * 1.04f * 1.02f).Within(0.001f));

            PlatformCollider platform = SpawnPlatform("PermanentShortPlatform");
            SetProperty(player, "CurrentPlatform", platform);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(autoJump, "Jump");

            float expected = 10f * 1.04f * 1.02f * 1.03f;
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected).Within(0.001f),
                "장착 J-KB는 짧은 그린 발판 하한만 1.0으로 만들고 J-B3 3%를 곱해야 합니다.");
            Assert.That(expected / 10f, Is.LessThan(1.30f));
        }

        [Test]
        public void RunPlatformGuardIsConsumedBeforeSharedPermanentGuard()
        {
            SeedGrowth(
                new[] { "I-KC" },
                inkKeystone: "I-KC");
            CreatePlayingManager(out var growth);
            SetProperty(growth, "StrokeGuardLevel", 1);
            PlatformCollider platform = SpawnPlatform("LayeredStrokeGuard");

            Assert.That(platform.HasStrokeGuard, Is.True);
            Assert.That(platform.BreakFromHazard(), Is.True);
            Assert.That(platform.HasStrokeGuard, Is.False,
                "첫 낙묵석은 한 판 굳은 획을 먼저 소비해야 합니다.");
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.False);

            Assert.That(platform.BreakFromHazard(), Is.True);
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.False,
                "두 번째 낙묵석은 먹떼 공용 영구 비기를 소비하고 발판을 남겨야 합니다.");

            Assert.That(growth.TryUsePermanentStrokeGuard(), Is.False,
                "먹떼 공용 영구 비기는 18초 안에 다시 소비되면 안 됩니다.");
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
        public void ConsecutiveLandingKeystoneAddsTwentyPercentToExistingCharge()
        {
            SeedGrowth(new[] { "J-KA" }, leapKeystone: "J-KA");
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentLandingRhythm");
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "chargeTimer", 0.4f);
            SetField(autoJump, "consecutiveDrawnLandings", 2);
            SetField(autoJump, "consecutiveLandingReadyAt", Time.time - 1f);

            autoJump.NotifyLanding(true);

            Assert.That(
                GetField<float>(autoJump, "chargeTimer"),
                Is.EqualTo(0.6f).Within(0.001f),
                "이미 20% 넘게 충전됐어도 세 번째 착지는 진행도를 20%p 더해야 합니다.");
        }

        [Test]
        public void FallControlOnlyCapsAutomaticJumpFlight()
        {
            SeedGrowth(new[] { "J-C2" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentAutomaticFallControl");

            player.Body.linearVelocity = new Vector2(0f, -30f);
            Invoke(player, "ApplyPermanentAirControl");
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(-30f).Within(0.001f),
                "특수 상승과 일반 추락에는 자동 점프 전용 낙하 제어를 적용하면 안 됩니다.");

            player.BeginAutomaticJumpFlight();
            player.Body.linearVelocity = new Vector2(0f, -30f);
            Invoke(player, "ApplyPermanentAirControl");
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(-17.28f).Within(0.001f),
                "기본 자동 점프 낙하 상한 18에서 정확히 4%만 줄어야 합니다.");

            player.LaunchInkDrop(10f, false);
            Assert.That(player.IsAutomaticJumpInFlight, Is.False,
                "먹물방울·풍맥 상승은 자동 점프 특성 출처를 즉시 해제해야 합니다.");
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
            string owned = ownedNodeIds == null || ownedNodeIds.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", ownedNodeIds) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":0,\"spent\":0," +
                "\"tutorialRewardClaimed\":false," +
                "\"lastSettledRunId\":\"\",\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                $"\"survivalKeystoneId\":\"{survivalKeystone}\"," +
                $"\"leapKeystoneId\":\"{leapKeystone}\"," +
                $"\"inkHandlingKeystoneId\":\"{inkKeystone}\"}}";
            PermanentGrowthProfile.ResetCacheForTests();
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
