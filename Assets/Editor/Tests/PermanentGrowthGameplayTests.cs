using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    /// 네 가지 영구 성장만 실제 체력·먹·붓·자동 점프에 이어지는지 검증한다.
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
        public void BodyGrowthRaisesOnlyOriginalHealthToEleven()
        {
            PurchaseTrack("body", 8);
            CreatePlayingManager(out RunGrowthController growth);

            PlayerController original = CreatePlayer("BodyGrowthOriginal");
            PlayerController clone = CreatePlayer("BodyGrowthClone");
            clone.ConfigureAsClone(1f);

            Assert.That(growth.PermanentSnapshot.MaxHealthBonus, Is.EqualTo(8));
            Assert.That(original.MaxHealth, Is.EqualTo(11));
            Assert.That(original.CurrentHealth, Is.EqualTo(11));
            Assert.That(clone.MaxHealth, Is.EqualTo(1));
            Assert.That(clone.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void ElevenHealthFallRecoversTenTimesThenFinalFallKills()
        {
            PurchaseTrack("body", 8);
            CreatePlayingManager(out _);
            PlayerController player = CreatePlayer("BodyGrowthFall");
            var cameraObject = Track(new GameObject("BodyGrowthFallCamera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.orthographicSize = 5f;
            SetField(player, "cam", camera);
            SetField(player, "camHalfHeight", camera.orthographicSize);

            for (int expected = 10; expected >= 0; expected--)
            {
                player.Body.position = new Vector2(1.25f, -20f);
                SetField(player, "damageInvulnerableUntil", Time.time - 1f);
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
        public void InkGrowthDoublesStrokeCapacity()
        {
            PurchaseTrack("ink", 8);
            CreatePlayingManager(out RunGrowthController growth);
            var host = Track(new GameObject("InkGrowthStroke"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", StrokeCapture.DefaultInkCapacity);

            Assert.That(growth.PermanentSnapshot.InkCapacityMultiplier,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(stroke.BaseEffectiveInkCapacity,
                Is.EqualTo(StrokeCapture.DefaultInkCapacity * 2f)
                    .Within(0.0001f));
            Assert.That(stroke.InkRemaining01, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void BrushGrowthReducesPendingStrokeCostToSeventySixPercent()
        {
            PurchaseTrack("brush", 8);
            CreatePlayingManager(out RunGrowthController growth);
            var host = Track(new GameObject("BrushGrowthStroke"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "drawing", true);
            SetField(stroke, "strokeLength", 2.6f);

            Assert.That(growth.PermanentSnapshot.InkBudgetCostMultiplier,
                Is.EqualTo(0.76f).Within(0.0001f));
            Assert.That(stroke.PendingStrokeBudgetCost,
                Is.EqualTo(2.6f * 0.76f).Within(0.0001f));
            Assert.That(growth.PermanentSnapshot.InkCapacityMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void JumpGrowthChangesAutomaticJumpBySquareRootOfTenPercent()
        {
            PurchaseTrack("jump", 8);
            CreatePlayingManager(out RunGrowthController growth);
            PlayerController player = CreatePlayer("JumpGrowthPlayer");
            var autoJump = player.gameObject.AddComponent<AutoJump>();
            Invoke(autoJump, "Awake");
            SetField(autoJump, "baseJumpSpeed", 10f);
            SetField(autoJump, "jumpStrengthMultiplier", 1f);
            SetField(autoJump, "horizontalMomentumRetention", 0f);
            SetField(autoJump, "flatPlatformWanderSpeed", 0f);
            SetField(autoJump, "normalInfluence", 0f);
            SetProperty(player, "GroundNormal", Vector2.up);
            SetProperty(player, "CurrentPlatform", null);

            Invoke(autoJump, "Jump");

            float expected = 10f * Mathf.Sqrt(1.10f);
            Assert.That(growth.PermanentSnapshot.JumpHeightMultiplier,
                Is.EqualTo(1.10f).Within(0.0001f));
            Assert.That(player.Body.linearVelocity.y,
                Is.EqualTo(expected).Within(0.001f));
            Assert.That(growth.PermanentSnapshot.JumpPowerMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MaximumGrowthDoesNotRestoreRemovedPassiveMechanics()
        {
            PurchaseTrack("body", 8);
            PurchaseTrack("ink", 8);
            PurchaseTrack("brush", 8);
            PurchaseTrack("jump", 8);
            CreatePlayingManager(out RunGrowthController growth);
            PermanentGrowthRunSnapshot snapshot = growth.PermanentSnapshot;

            Assert.That(snapshot.OwnedNodeCount, Is.EqualTo(32));
            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasPostHitShield, Is.False);
            Assert.That(snapshot.HasGoldenBrushShield, Is.False);
            Assert.That(snapshot.HasInkDropEndShield, Is.False);
            Assert.That(snapshot.HasWallCling, Is.False);
            Assert.That(snapshot.HasDoubleJump, Is.False);
            Assert.That(snapshot.HasSafetyPlatform, Is.False);
            Assert.That(snapshot.InkCloneItemExtraCount, Is.Zero);
            Assert.That(snapshot.InkCloneMaxHealthBonus, Is.Zero);
            Assert.That(snapshot.InkRecoverySpeedMultiplier,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(growth.TryPreserveHitMotion(), Is.False);
        }

        [Test]
        public void RetiredInkReserveKeepsLegacySerializedEnumSlot()
        {
            Assert.That((int)MukJump.Items.ItemType.InkReserve, Is.EqualTo(4),
                "폐기 아이템 번호는 구 씬 직렬화 호환을 위해 다시 사용하면 안 됩니다.");
        }

        void PurchaseTrack(string prefix, int stageCount)
        {
            PermanentGrowthProfile.DebugRefillCurrency();
            for (int stage = 1; stage <= stageCount; stage++)
                Assert.That(
                    PermanentGrowthProfile.TryPurchaseNode($"{prefix}.{stage}"),
                    Is.True,
                    $"{prefix}.{stage} 구매가 실제 프로필 경로에서 성공해야 합니다.");
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
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
            return player;
        }

        T Track<T>(T value) where T : Object
        {
            cleanup.Add(value);
            return value;
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
            PlatformCollider.RuntimeInkCapacityMultiplier = 1f;
        }
    }
}
