using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    /// 영구 성장 저장값이 실제 피해·자동 점프·드로잉 발판 규칙까지 이어지는지 검증한다.
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
        public void SurvivalStatsIncreaseHealthAndActualDamageGrace()
        {
            SeedGrowth(
                (PermanentGrowthType.Vitality, 1),
                (PermanentGrowthType.DamageGrace, 3));
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
            Assert.That(
                invulnerableUntil - hitTime,
                Is.EqualTo(0.79f).Within(0.03f),
                "기본 0.55초와 영구 성장 0.24초가 실제 피해 유예 시간에 더해져야 합니다.");
        }

        [Test]
        public void LastBreathIsSharedAcrossClonesIgnoresFallAndRechargesPerRun()
        {
            SeedGrowth((PermanentGrowthType.LastBreath, 1));
            var manager = CreatePlayingManager(out var growth);
            var fallingPlayer = CreatePlayer("PermanentFallTarget");
            var firstClone = CreatePlayer("PermanentLastBreathCloneA");
            var secondClone = CreatePlayer("PermanentLastBreathCloneB");
            manager.RegisterPlayer(fallingPlayer);
            manager.RegisterPlayer(firstClone);
            manager.RegisterPlayer(secondClone);

            Assert.That(growth.LastBreathAvailable, Is.True);
            fallingPlayer.Kill();
            Assert.That(growth.LastBreathAvailable, Is.True,
                "마지막 먹숨은 화면 아래 추락 사망에는 소모되면 안 됩니다.");

            firstClone.ConfigureAsClone(1f);
            secondClone.ConfigureAsClone(1f);
            SetProperty(firstClone, "CurrentHealth", 1);
            SetProperty(secondClone, "CurrentHealth", 1);
            ExpireDamageGrace(firstClone);
            ExpireDamageGrace(secondClone);

            Assert.That(firstClone.TakeHit(), Is.True);
            Assert.That(firstClone.IsDead, Is.False);
            Assert.That(firstClone.CurrentHealth, Is.EqualTo(1));
            Assert.That(growth.LastBreathAvailable, Is.False);

            Assert.That(secondClone.TakeHit(), Is.True);
            Assert.That(secondClone.IsDead, Is.True,
                "한 분신이 사용한 마지막 먹숨을 다른 분신이 다시 사용하면 안 됩니다.");

            Invoke(growth, "ResetRun");
            Assert.That(growth.LastBreathAvailable, Is.True,
                "새 판이 시작되면 영구 패시브의 공유 1회 사용권이 복구되어야 합니다.");
        }

        [Test]
        public void AutoJumpMultipliesRunPermanentAndDrawnPlatformPower()
        {
            SeedGrowth(
                (PermanentGrowthType.JumpPower, 5),
                (PermanentGrowthType.DrawnPlatformLeap, 1));

            var growthHost = Track(new GameObject("PermanentJumpGrowth"));
            var growth = growthHost.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            SetProperty(growth, "JumpLevel", 1);

            var player = CreatePlayer("PermanentJumpPlayer");
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "baseJumpSpeed", 10f);
            SetField(autoJump, "jumpStrengthMultiplier", 1f);
            SetField(autoJump, "powerMultiplierRange", Vector2.one);
            SetField(autoJump, "horizontalMomentumRetention", 0f);
            SetField(autoJump, "flatPlatformWanderSpeed", 0f);
            SetField(autoJump, "normalInfluence", 0f);
            SetProperty(player, "GroundNormal", Vector2.up);

            SetProperty(player, "CurrentPlatform", null);
            Invoke(autoJump, "Jump");
            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(10f * 1.04f * 1.05f).Within(0.001f),
                "한 판 점프 성장과 영구 점프 성장은 덧셈이 아니라 곱으로 함께 적용되어야 합니다.");

            var platform = PlatformCollider.Spawn(new List<Vector2>
            {
                Vector2.zero,
                Vector2.right,
            });
            Track(platform.gameObject);
            SetProperty(player, "CurrentPlatform", platform);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(autoJump, "Jump");

            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(10f * 1.04f * 1.05f * 1.10f).Within(0.001f),
                "먹결 도약 최종 패시브는 직접 그린 임시 발판에서만 추가로 곱해져야 합니다.");
        }

        [Test]
        public void NewDrawnPlatformGuardUsesPermanentOrRunUpgrade()
        {
            SeedGrowth((PermanentGrowthType.StrokeGuard, 1));
            var permanentGuard = SpawnPlatform("PermanentGuardPlatform");
            Assert.That(permanentGuard.HasStrokeGuard, Is.True);

            SeedGrowth();
            Assert.That(permanentGuard.HasStrokeGuard, Is.True,
                "이미 생성된 획의 1회 방어 상태는 프로필 재조회로 사라지면 안 됩니다.");
            var unguarded = SpawnPlatform("UnguardedPlatform");
            Assert.That(unguarded.HasStrokeGuard, Is.False);

            var growthHost = Track(new GameObject("RuntimeGuardGrowth"));
            var growth = growthHost.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            SetProperty(growth, "StrokeGuardLevel", 1);
            var runGuard = SpawnPlatform("RuntimeGuardPlatform");
            Assert.That(runGuard.HasStrokeGuard, Is.True,
                "한 판 두루마리 수호 먹결도 영구 성장과 독립적으로 새 획을 지켜야 합니다.");

            Assert.That(permanentGuard.BreakFromHazard(), Is.True);
            Assert.That(permanentGuard.HasStrokeGuard, Is.False);
            Assert.That(permanentGuard.IsTemporaryDrawnPlatform, Is.True,
                "첫 낙묵석은 방어만 소비하고 발판을 남겨야 합니다.");
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
            Assert.That(growth, Is.Not.Null);
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
            var platform = PlatformCollider.Spawn(new List<Vector2>
            {
                Vector2.zero,
                Vector2.right,
            });
            platform.gameObject.name = objectName;
            Track(platform.gameObject);
            return platform;
        }

        void SeedGrowth(
            params (PermanentGrowthType type, int level)[] ranks)
        {
            var builder = new StringBuilder(
                "{\"schemaVersion\":1,\"balanceVersion\":1," +
                "\"wallet\":0,\"spent\":0,\"tutorialRewardClaimed\":false," +
                "\"lastSettledRunId\":\"\",\"ranks\":[");
            for (int i = 0; i < ranks.Length; i++)
            {
                if (i > 0) builder.Append(',');
                var definition = PermanentGrowthCatalog.Get(ranks[i].type);
                Assert.That(definition, Is.Not.Null);
                builder.Append("{\"id\":\"")
                    .Append(definition.Id)
                    .Append("\",\"level\":")
                    .Append(ranks[i].level)
                    .Append('}');
            }
            builder.Append("]}");
            store.Json = builder.ToString();
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

        static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
            return method.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            return (T)field.GetValue(target);
        }

        static void SetProperty(
            object target,
            string propertyName,
            object value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"{target.GetType().Name}.{propertyName} 속성을 찾을 수 없습니다.");
            property.SetValue(target, value);
        }

        static void ClearActivePlatforms()
        {
            var field = typeof(PlatformCollider).GetField(
                "active",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            var platforms = field?.GetValue(null) as List<PlatformCollider>;
            platforms?.Clear();
        }
    }
}
