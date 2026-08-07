using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
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
        public void SurvivalVitalityPathRaisesBodyToFourAndCloneToTwo()
        {
            SeedGrowth(
                new[] { "S-KA" });
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
                Is.EqualTo(0.59f).Within(0.03f),
                "공용 생존 뿌리의 +0.04초만 더해져야 합니다.");

            for (int expected = 2; expected >= 0; expected--)
            {
                ExpireDamageGrace(player);
                Assert.That(player.TakeHit(), Is.True);
                Assert.That(player.CurrentHealth, Is.EqualTo(expected));
                Assert.That(player.DamageStage, Is.EqualTo(4 - expected));
                Assert.That(player.IsDead, Is.EqualTo(expected == 0));
            }
        }

        [Test]
        public void FourHealthFallRecoversThreeTimesThenFinalFallKills()
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

            for (int expected = 3; expected >= 0; expected--)
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
        public void LastBreathRevivesOriginalOnce()
        {
            SeedGrowth(
                new[] { "S-KB" },
                survivalKeystone: "S-KB");
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentHitStability");
            player.Body.linearVelocity = new Vector2(10f, -5f);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.IsDead, Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(1));
            float invulnerableUntil =
                GetField<float>(player, "damageInvulnerableUntil");
            Assert.That(invulnerableUntil - Time.time,
                Is.EqualTo(0.8f).Within(0.04f));
            Assert.That(player.HasShield, Is.False);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.IsDead, Is.True,
                "숨 고르기 결실은 본체를 한 판에 정확히 한 번만 부활시켜야 합니다.");
        }

        [Test]
        public void FruitionItemShieldsAreIsolatedBySelectedPath()
        {
            var golden = new PermanentGrowthRunSnapshot(
                new[] { "I-KB", "J-KA" },
                new Dictionary<PermanentGrowthBranch, string>
                {
                    [PermanentGrowthBranch.InkHandling] = "I-KB",
                    [PermanentGrowthBranch.Leap] = "J-KC",
                });
            Assert.That(golden.HasGoldenBrushShield, Is.True);
            Assert.That(golden.HasInkDropEndShield, Is.False);
        }

        [Test]
        public void GoldenBrushFruitionGrantsCollectorShieldImmediately()
        {
            SeedGrowth(new[] { "I-KB" }, inkKeystone: "I-KB");
            var manager = CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentGoldenBrushShield");
            manager.RegisterPlayer(player);
            Track(new GameObject("PermanentGoldenBrushStroke"))
                .AddComponent<StrokeCapture>();

            Assert.That(ItemEffect.Apply(ItemType.GoldenBrush, player), Is.True);
            Assert.That(player.HasShield, Is.True);
        }

        [Test]
        public void InkDropFruitionGrantsCollectorShieldWhenRiseEnds()
        {
            SeedGrowth(new[] { "J-KA" }, leapKeystone: "J-KA");
            var manager = CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentInkDropEndShield");
            manager.RegisterPlayer(player);

            Assert.That(ItemEffect.Apply(ItemType.InkDrop, player), Is.True);
            Assert.That(player.HasShield, Is.False,
                "상승 중이 아니라 상승이 끝난 뒤 방어막을 받아야 합니다.");

            SetField(player, "inkDropHasRisen", true);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(player, "FixedUpdate");

            Assert.That(player.HasShield, Is.True);
            Assert.That(player.IsInkDropBoosted, Is.False);
        }

        [Test]
        public void LastBreathAlsoRevivesOriginalFromOneFall()
        {
            SeedGrowth(new[] { "S-KB" }, survivalKeystone: "S-KB");
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentLastBreathFall");

            Invoke(player, "HandleFallBelowView");
            Assert.That(player.IsDead, Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(1));

            Invoke(player, "HandleFallBelowView");
            Assert.That(player.IsDead, Is.True,
                "숨 고르기 결실은 추락에서도 한 판에 한 번만 부활해야 합니다.");
        }

        [Test]
        public void HitStabilityLineReducesHorizontalKnockbackToSixtyFourPercent()
        {
            SeedGrowth(new[] { "S-C3" });
            CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentHitHorizontalStability");
            player.GrantShield();
            player.Body.linearVelocity = new Vector2(10f, -5f);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);

            Assert.That(player.Body.linearVelocity.x,
                Is.EqualTo(6.4f).Within(0.001f));
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(player.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void LastBreathDoesNotActivateForRuntimeClone()
        {
            SeedGrowth(new[] { "S-KB" }, survivalKeystone: "S-KB");
            var manager = CreatePlayingManager(out var growth);
            var player = CreatePlayer("PermanentCloneNoLastBreath");
            player.ConfigureAsClone(1f);
            manager.RegisterPlayer(player);

            Assert.That(growth.LastBreathAvailable, Is.True);
            Assert.That(growth.TrySurviveLethalObstacleHit(player), Is.False);
            Assert.That(growth.LastBreathAvailable, Is.True);
        }

        [Test]
        public void AutoJumpUsesOnlySelectedPowerPathWithoutHiddenDrawnBonus()
        {
            SeedGrowth(
                new[] { "J-KB", "J-KC" },
                leapKeystone: "J-KB");
            CreatePlayingManager(out var growth);

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
            float expected = 10f * 1.04f;
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected).Within(0.001f));
            Assert.That(expected / 10f, Is.LessThan(1.30f));
            Assert.That(
                growth.PermanentSnapshot.JumpHeightMultiplier,
                Is.EqualTo(1f).Within(0.001f),
                "선택하지 않은 높은 먹발 갈래가 점프 높이에 섞이면 안 됩니다.");

            PlatformCollider platform = SpawnPlatform("PermanentShortPlatform");
            SetProperty(player, "CurrentPlatform", platform);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(autoJump, "Jump");

            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected * 0.85f).Within(0.001f),
                "돋는 먹발 결실은 벽 매달림만 제공하고 숨은 도약 보너스를 더하면 안 됩니다.");
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
        public void HighInkFootPerformsOneDoubleJumpOnEveryAutomaticJump()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentDoubleJump");
            manager.RegisterPlayer(player);
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "baseJumpSpeed", 10f);
            SetField(autoJump, "jumpStrengthMultiplier", 1f);
            SetField(autoJump, "normalInfluence", 0f);
            SetProperty(player, "GroundNormal", Vector2.up);

            Invoke(autoJump, "Jump");
            float primarySpeed = GetField<float>(
                autoJump,
                "primaryJumpVerticalSpeed");
            float triggerSpeed = primarySpeed * 0.15f;
            player.Body.linearVelocity = new Vector2(0f, triggerSpeed + 0.1f);
            Invoke(autoJump, "FixedUpdate");
            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(triggerSpeed + 0.1f).Within(0.001f),
                "첫 상승 속도가 15%보다 많이 남았을 때는 아직 2단점프하면 안 됩니다.");
            player.Body.linearVelocity = new Vector2(0f, triggerSpeed);
            Invoke(autoJump, "FixedUpdate");
            float doubleJumpSpeed = primarySpeed * 0.4f;
            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(doubleJumpSpeed).Within(0.001f));
            Invoke(autoJump, "FixedUpdate");
            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(doubleJumpSpeed).Within(0.001f),
                "같은 비행에서 2단점프가 두 번 발동하면 안 됩니다.");

            autoJump.NotifyLanding(false);
            SetField(player, "automaticJumpInFlight", false);
            player.Body.linearVelocity = Vector2.zero;
            Invoke(autoJump, "Jump");
            primarySpeed = GetField<float>(autoJump, "primaryJumpVerticalSpeed");
            player.Body.linearVelocity = new Vector2(0f, -0.2f);
            Invoke(autoJump, "FixedUpdate");
            Assert.That(
                player.Body.linearVelocity.y,
                Is.EqualTo(primarySpeed * 0.4f).Within(0.001f),
                "높은 먹발 결실은 12초 대기 없이 다음 일반 자동점프에도 발동해야 합니다.");
        }

        [Test]
        public void HighInkFootAppliesToEveryCloneWithoutRepresentativeReservation()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out _);
            var original = CreatePlayer("PermanentDoubleJumpOriginal");
            var clone = CreatePlayer("PermanentDoubleJumpClone");
            clone.ConfigureAsClone(1f);
            manager.RegisterPlayer(original);
            manager.RegisterPlayer(clone);

            foreach (PlayerController player in new[] { original, clone })
            {
                var autoJump = player.gameObject.AddComponent<AutoJump>();
                Invoke(autoJump, "Awake");
                SetField(autoJump, "baseJumpSpeed", 10f);
                SetField(autoJump, "jumpStrengthMultiplier", 1f);
                SetField(autoJump, "normalInfluence", 0f);
                SetProperty(player, "GroundNormal", Vector2.up);
                Invoke(autoJump, "Jump");
                Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(true));
            }
        }

        [Test]
        public void SpecialLaunchCancelsLocalDoubleJump()
        {
            SeedGrowth(new[] { "J-KC" }, leapKeystone: "J-KC");
            var manager = CreatePlayingManager(out _);
            var player = CreatePlayer("PermanentSpecialLaunchReservation");
            manager.RegisterPlayer(player);
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "baseJumpSpeed", 10f);
            SetField(autoJump, "jumpStrengthMultiplier", 1f);
            SetField(autoJump, "normalInfluence", 0f);
            SetProperty(player, "GroundNormal", Vector2.up);
            Invoke(autoJump, "Jump");
            Assert.That(GetField<bool>(autoJump, "doubleJumpArmed"), Is.True);

            player.LaunchToHeight(10f);

            Assert.That(GetField<bool>(autoJump, "doubleJumpArmed"), Is.False);
            Assert.That(Invoke(autoJump, "TryPerformDoubleJump"), Is.EqualTo(false));
        }

        [Test]
        public void SproutingInkFootClingsToWallWhileDescending()
        {
            SeedGrowth(new[] { "J-KB" }, leapKeystone: "J-KB");
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
            Assert.That(player.Body.gravityScale, Is.Zero);
        }

        [Test]
        public void RetiredInkReserveKeepsLegacySerializedEnumSlot()
        {
            Assert.That((int)MukJump.Items.ItemType.InkReserve, Is.EqualTo(4),
                "폐기 아이템 번호는 구 씬 직렬화 호환을 위해 다시 사용하면 안 됩니다.");
        }

        [Test]
        public void InkCapacityUsesOnlyPermanentGrowthWithoutRunItemBonus()
        {
            SeedGrowth(new[] { "I-KA" });
            CreatePlayingManager(out _);
            var host = Track(new GameObject("PermanentInkCapacity"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", StrokeCapture.DefaultInkCapacity);

            Assert.That(stroke.BaseEffectiveInkCapacity,
                Is.EqualTo(20.4f).Within(0.0001f));
            Assert.That(stroke.EffectiveInkCapacity,
                Is.EqualTo(20.4f).Within(0.0001f));
            Assert.That(stroke.EffectiveNaturalHoldDuration,
                Is.EqualTo(PlatformCollider.DefaultNaturalHoldDuration)
                    .Within(0.0001f));
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
