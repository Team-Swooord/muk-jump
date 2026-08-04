using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class PlayerSafetyTests
    {
        GameObject playerObject;
        GameObject itemObject;
        HashSet<int> existingPlatformIds;

        [SetUp]
        public void SetUp()
        {
            existingPlatformIds = new HashSet<int>();
            foreach (Drawing.PlatformCollider platform in
                     Object.FindObjectsByType<Drawing.PlatformCollider>(
                         FindObjectsSortMode.None))
                existingPlatformIds.Add(platform.GetInstanceID());
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
            if (itemObject != null)
                Object.DestroyImmediate(itemObject);
            foreach (Drawing.PlatformCollider platform in
                     Object.FindObjectsByType<Drawing.PlatformCollider>(
                         FindObjectsSortMode.None))
                if (!existingPlatformIds.Contains(platform.GetInstanceID()))
                    Object.DestroyImmediate(platform.gameObject);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void ShieldConsumptionAlwaysStartsDamageGracePeriod()
        {
            var player = CreatePlayer();
            player.GrantShield();

            bool consumed = (bool)Invoke(player, "ConsumeShield");
            float invulnerableUntil = (float)GetField(player, "damageInvulnerableUntil");

            Assert.That(consumed, Is.True);
            Assert.That(player.HasShield, Is.False);
            Assert.That(invulnerableUntil, Is.GreaterThan(Time.time));
        }

        [Test]
        public void DuplicateShieldPickupRemainsUntilCurrentShieldIsSpent()
        {
            var player = CreatePlayer();
            var manager = playerObject.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            SetAutoProperty(manager, "State", GameState.Playing);

            Assert.That(ItemEffect.Apply(ItemType.InkShield, player), Is.True);
            Assert.That(player.HasShield, Is.True);

            itemObject = new GameObject("DuplicateShieldPickup");
            var pickup = itemObject.AddComponent<ItemPickup>();
            pickup.Configure(ItemType.InkShield, 0f);
            bool released = false;
            pickup.ReleaseRequested += _ => released = true;

            Invoke(pickup, "OnTriggerEnter2D",
                player.GetComponent<CircleCollider2D>());

            Assert.That(released, Is.False,
                "보유 중인 비중첩 방어막 픽업은 풀로 반환되면 안 됩니다.");
            Assert.That(itemObject.GetComponent<CircleCollider2D>().enabled, Is.True);
            Assert.That(player.HasShield, Is.True);

            Assert.That((bool)Invoke(player, "ConsumeShield"), Is.True);
            Invoke(pickup, "OnTriggerEnter2D",
                player.GetComponent<CircleCollider2D>());

            Assert.That(released, Is.True,
                "기존 방어막을 쓴 뒤에는 남은 픽업을 다시 획득할 수 있어야 합니다.");
            Assert.That(itemObject.GetComponent<CircleCollider2D>().enabled, Is.False);
            Assert.That(player.HasShield, Is.True);
        }

        [Test]
        public void DeathSequenceKeepsRendererForDeathFramesUntilInkSpreadEnds()
        {
            var player = CreatePlayer();
            var renderer = player.GetComponent<SpriteRenderer>();
            var sequence = (IEnumerator)Invoke(player, "DeathSequence", false);

            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(renderer.enabled, Is.True,
                "첫 yield 전에 본체를 숨기면 CharacterAnimator의 사망 프레임이 보이지 않습니다.");
        }

        [Test]
        public void RegisteredClonesUseOneNonCollidingPlayerLayer()
        {
            var player = CreatePlayer();
            var manager = playerObject.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            manager.RegisterPlayer(player);

            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(player.gameObject.layer, Is.EqualTo(playerLayer));
            Assert.That(Physics2D.GetIgnoreLayerCollision(playerLayer, playerLayer), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(
                playerLayer, LayerMask.NameToLayer("Platform")), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(
                playerLayer, LayerMask.NameToLayer("Obstacle")), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(
                playerLayer, LayerMask.NameToLayer("Item")), Is.False);
        }

        [Test]
        public void DeadPlayerCannotConsumePickupDuringSamePhysicsTick()
        {
            var player = CreatePlayer();
            var manager = playerObject.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            SetAutoProperty(manager, "State", GameState.Playing);
            SetAutoProperty(player, "IsDead", true);

            itemObject = new GameObject("DeadPlayerPickupRace");
            var pickup = itemObject.AddComponent<ItemPickup>();
            pickup.Configure(ItemType.InkShield, 0f);
            bool released = false;
            pickup.ReleaseRequested += _ => released = true;

            Invoke(pickup, "OnTriggerEnter2D",
                player.GetComponent<CircleCollider2D>());

            Assert.That(released, Is.False);
            Assert.That(itemObject.GetComponent<CircleCollider2D>().enabled, Is.True);
            Assert.That(ItemEffect.Apply(ItemType.InkShield, player), Is.False);
        }

        [Test]
        public void InvalidAnimationTimingValuesAreClamped()
        {
            CreatePlayer();
            var autoJump = playerObject.AddComponent<AutoJump>();
            SetField(autoJump, "jumpIntervalSeconds", -1f);
            Invoke(autoJump, "OnValidate");

            var animator = playerObject.AddComponent<CharacterAnimator>();
            SetField(animator, "deadFps", -12f);
            Invoke(animator, "OnValidate");

            Assert.That((float)GetField(autoJump, "jumpIntervalSeconds"),
                Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(autoJump.ChargeRatio, Is.InRange(0f, 1f));
            Assert.That((float)GetField(animator, "deadFps"), Is.EqualTo(0f));
        }

        [Test]
        public void NonFiniteAnimationTimingValuesFallBackToSafeDefaults()
        {
            CreatePlayer();
            var autoJump = playerObject.AddComponent<AutoJump>();
            SetField(autoJump, "jumpIntervalSeconds", float.NaN);
            Invoke(autoJump, "OnValidate");

            var animator = playerObject.AddComponent<CharacterAnimator>();
            SetField(animator, "deadFps", float.PositiveInfinity);
            Invoke(animator, "OnValidate");

            Assert.That((float)GetField(autoJump, "jumpIntervalSeconds"), Is.EqualTo(1f));
            Assert.That(autoJump.ChargeRatio, Is.InRange(0f, 1f));
            Assert.That((float)GetField(animator, "deadFps"), Is.EqualTo(12f));
        }

        PlayerController CreatePlayer()
        {
            playerObject = new GameObject("PlayerSafetyTests");
            playerObject.AddComponent<SpriteRenderer>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            var player = playerObject.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            return player;
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
