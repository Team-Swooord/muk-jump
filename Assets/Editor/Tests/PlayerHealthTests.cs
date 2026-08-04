using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class PlayerHealthTests
    {
        readonly List<Object> cleanup = new();

        [SetUp]
        public void SetUp()
        {
            var managerObject = Track(new GameObject("PlayerHealthManager"));
            var manager = managerObject.AddComponent<GameManager>();
            SetAutoProperty(manager, "State", GameState.Playing);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }

        [Test]
        public void ThreeUnprotectedHitsConsumeHealthThenKillWithoutPhysicsScaling()
        {
            var player = CreatePlayer("ThreeHitTarget");
            var collider = player.GetComponent<CircleCollider2D>();
            Vector3 rootScale = player.transform.localScale;
            float colliderRadius = collider.radius;
            Vector2 colliderOffset = collider.offset;

            Assert.That(player.MaxHealth, Is.EqualTo(3));
            Assert.That(player.CurrentHealth, Is.EqualTo(3));
            Assert.That((float)GetField(player, "damageHitGraceDuration"),
                Is.EqualTo(0.55f).Within(0.001f));

            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(2));
            Assert.That(player.DamageStage, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.False);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(1));
            Assert.That(player.DamageStage, Is.EqualTo(2));
            Assert.That(player.IsDead, Is.False);

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(player.IsDead, Is.True);

            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(colliderRadius));
            Assert.That(collider.offset, Is.EqualTo(colliderOffset));
        }

        [Test]
        public void ShieldIsConsumedBeforeHealthAndRuntimeCloneStartsFull()
        {
            var player = CreatePlayer("ShieldAndCloneTarget");
            player.GrantShield();

            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.HasShield, Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(3));

            ExpireDamageGrace(player);
            Assert.That(player.TakeHit(), Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(2));

            player.ConfigureAsClone(1f);

            Assert.That(player.IsRuntimeClone, Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(3));
            Assert.That(player.DamageStage, Is.Zero);
        }

        [Test]
        public void BoostAndGraceContactsAreIgnoredAndDoNotConsumeHealth()
        {
            var player = CreatePlayer("IgnoredContactTarget");
            player.LaunchInkDrop(1f, false);

            Assert.That(player.TakeHit(), Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(3));

            SetField(player, "IsInkDropBoosted", false);
            SetField(player, "damageInvulnerableUntil", Time.time + 10f);
            Assert.That(player.TakeHit(), Is.False);
            Assert.That(player.CurrentHealth, Is.EqualTo(3));
        }

        [Test]
        public void DirectFallStyleKillClearsHealthAndNotifiesHud()
        {
            var player = CreatePlayer("FallDeathTarget");
            int notifiedCurrent = -1;
            int notifiedMax = -1;
            player.HealthChanged += (current, maximum) =>
            {
                notifiedCurrent = current;
                notifiedMax = maximum;
            };

            player.Kill();

            Assert.That(player.IsDead, Is.True);
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(notifiedCurrent, Is.Zero);
            Assert.That(notifiedMax, Is.EqualTo(3));
        }

        [Test]
        public void HitInkPuffIsSingleReusableCloneExcludedVisual()
        {
            var player = CreatePlayer("HitInkPuffPlayer");
            var view = player.gameObject.AddComponent<ItemEffectView>();
            var renderer = player.GetComponent<SpriteRenderer>();
            renderer.sprite = CreateTestSprite();
            Vector3 rootScale = player.transform.localScale;
            var collider = player.GetComponent<CircleCollider2D>();
            float radius = collider.radius;
            Vector2 offset = collider.offset;

            view.PlayVitalityHit();
            view.PlayVitalityHit();

            Assert.That(CountDirectChildren(
                player.transform, "GrowthVitalityPuff"), Is.EqualTo(1));
            Transform puff = player.transform.Find("GrowthVitalityPuff");
            Assert.That(puff, Is.Not.Null);
            Assert.That(puff.GetComponent<SpriteRenderer>(), Is.Not.Null);

            var lifecycle = (IRuntimeCloneLifecycle)view;
            lifecycle.PrepareForRuntimeClone();
            Assert.That(puff.parent, Is.Null);
            var clone = Track(Object.Instantiate(player.gameObject));
            Assert.That(clone.transform.Find("GrowthVitalityPuff"), Is.Null,
                "고정 캐시 VFX가 먹분신 수만큼 복제되면 안 됩니다.");
            lifecycle.RestoreAfterRuntimeClone();

            Assert.That(puff.parent, Is.SameAs(player.transform));
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(radius));
            Assert.That(collider.offset, Is.EqualTo(offset));
        }

        [Test]
        public void CharacterAnimatorUsesMatchingDamagePoseWithoutScalingRoot()
        {
            var player = CreatePlayer("DamageAnimatorTarget");
            var animator = player.gameObject.AddComponent<CharacterAnimator>();
            var baseFrames = CreateFrames("base");
            var firstHitFrames = CreateFrames("hit01");
            var secondHitFrames = CreateFrames("hit02");
            SetBaseFrames(animator, baseFrames);
            SetField(animator, "damageStageOneFrames", firstHitFrames);
            SetField(animator, "damageStageTwoFrames", secondHitFrames);
            Invoke(animator, "Awake");

            Vector3 rootScale = player.transform.localScale;
            float colliderRadius = player.GetComponent<CircleCollider2D>().radius;
            var renderer = player.GetComponent<SpriteRenderer>();

            SetAutoProperty(player, "CurrentHealth", 2);
            Invoke(animator, "LateUpdate");
            Assert.That(renderer.sprite, Is.SameAs(firstHitFrames[4]),
                "정점 상태는 피격 1단계 시트의 같은 apex 프레임을 사용해야 합니다.");

            SetAutoProperty(player, "CurrentHealth", 1);
            Invoke(animator, "LateUpdate");
            Assert.That(renderer.sprite, Is.SameAs(secondHitFrames[4]),
                "정점 상태는 피격 2단계 시트의 같은 apex 프레임을 사용해야 합니다.");

            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(player.GetComponent<CircleCollider2D>().radius,
                Is.EqualTo(colliderRadius));
        }

        [Test]
        public void FourthHealthPointAddsAThirdVisibleGrowthStage()
        {
            var player = CreatePlayer("FourHealthDamageAnimatorTarget");
            SetField(player, "maxHealth", 4);
            SetAutoProperty(player, "CurrentHealth", 1);
            Assert.That(player.DamageStage, Is.EqualTo(3));

            var animator = player.gameObject.AddComponent<CharacterAnimator>();
            var baseFrames = CreateFrames("base-four-health");
            var firstHitFrames = CreateFrames("hit01-four-health");
            var secondHitFrames = CreateFrames("hit02-four-health");
            SetBaseFrames(animator, baseFrames);
            SetField(animator, "damageStageOneFrames", firstHitFrames);
            SetField(animator, "damageStageTwoFrames", secondHitFrames);
            Invoke(animator, "Awake");

            Vector3 rootScale = player.transform.localScale;
            float colliderRadius = player.GetComponent<CircleCollider2D>().radius;
            Invoke(animator, "LateUpdate");

            Sprite thirdStage = player.GetComponent<SpriteRenderer>().sprite;
            Assert.That(thirdStage, Is.Not.Null);
            Assert.That(thirdStage.pixelsPerUnit,
                Is.LessThan(secondHitFrames[4].pixelsPerUnit));
            Assert.That(thirdStage.bounds.size.x,
                Is.GreaterThan(secondHitFrames[4].bounds.size.x));
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(player.GetComponent<CircleCollider2D>().radius,
                Is.EqualTo(colliderRadius));
        }

        [Test]
        public void CharacterAnimatorOrdersShuffledFramesBySemanticStateNames()
        {
            var source = CreateFrames("semantic");
            string[] states =
            {
                "idle", "crouch", "launch", "rise",
                "apex", "fall", "dive", "land",
            };
            for (int i = 0; i < source.Length; i++)
                source[i].name = $"muk_hit_01_{states[i]}";
            var shuffled = new[]
            {
                source[6], source[1], source[4], source[0],
                source[7], source[3], source[5], source[2],
            };

            var ordered = (Sprite[])typeof(CharacterAnimator).GetMethod(
                    "OrderFramesByState",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { shuffled });

            Assert.That(ordered, Is.Not.Null);
            for (int i = 0; i < source.Length; i++)
                Assert.That(ordered[i], Is.SameAs(source[i]));
        }

        [TestCase(
            "Assets/Art/Character/Player/muk_spritesheet.png", 780f)]
        [TestCase(
            "Assets/Resources/MukJump/Player/muk_spritesheet_hit_01.png", 735f)]
        [TestCase(
            "Assets/Resources/MukJump/Player/muk_spritesheet_hit_02.png", 690f)]
        public void CharacterSheetsGrowByDamageStageAndKeepEightStates(
            string assetPath,
            float expectedPpu)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.IsNotNull(texture, assetPath);
            Assert.That(texture.width, Is.EqualTo(4096));
            Assert.That(texture.height, Is.EqualTo(2048));

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.That(importer.spriteImportMode,
                Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.spritePixelsPerUnit,
                Is.EqualTo(expectedPpu).Within(0.001f));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(4096));

            string[] expectedStates =
            {
                "idle", "crouch", "launch", "rise",
                "apex", "fall", "dive", "land",
            };
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int frameCount = 0;
            for (int state = 0; state < expectedStates.Length; state++)
            {
                Sprite matched = null;
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sprite &&
                        sprite.name == expectedStates[state])
                    {
                        matched = sprite;
                        break;
                    }
                }

                Assert.IsNotNull(matched,
                    $"{assetPath}: {expectedStates[state]}");
                Assert.That(matched.rect.width, Is.EqualTo(1024f));
                Assert.That(matched.rect.height, Is.EqualTo(1024f));
                frameCount++;
            }
            Assert.That(frameCount, Is.EqualTo(8));
        }

        [Test]
        public void DeathFramesKeepTheLiveVisualScaleCorrectionRatio()
        {
            System.Type builderType =
                typeof(MukJump.EditorTools.MukJumpSceneBuilder);
            var livePpu = builderType.GetField(
                "CharPpu",
                BindingFlags.Static | BindingFlags.NonPublic);
            var deathPpu = builderType.GetField(
                "DeathPpu",
                BindingFlags.Static | BindingFlags.NonPublic);
            var hitOnePpu = builderType.GetField(
                "CharHitOnePpu",
                BindingFlags.Static | BindingFlags.NonPublic);
            var hitTwoPpu = builderType.GetField(
                "CharHitTwoPpu",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(livePpu, Is.Not.Null);
            Assert.That(deathPpu, Is.Not.Null);
            Assert.That(hitOnePpu, Is.Not.Null);
            Assert.That(hitTwoPpu, Is.Not.Null);
            float live = (float)livePpu.GetRawConstantValue();
            float death = (float)deathPpu.GetRawConstantValue();
            float hitOne = (float)hitOnePpu.GetRawConstantValue();
            float hitTwo = (float)hitTwoPpu.GetRawConstantValue();
            Assert.That(live, Is.EqualTo(780f).Within(0.001f));
            Assert.That(hitOne, Is.EqualTo(735f).Within(0.001f));
            Assert.That(hitTwo, Is.EqualTo(690f).Within(0.001f));
            Assert.That(hitOne, Is.LessThan(live));
            Assert.That(hitTwo, Is.LessThan(hitOne));
            Assert.That(death, Is.EqualTo(live * 0.8f).Within(0.001f));
        }

        [Test]
        public void WorldHealthBillboardUsesHorizontalSpriteAboveVisualBounds()
        {
            MethodInfo positionMethod = typeof(PlayerHealthBillboard).GetMethod(
                "ResolveWorldPosition",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo visibilityMethod = typeof(PlayerHealthBillboard).GetMethod(
                "ShouldDisplay",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(positionMethod, Is.Not.Null);
            Assert.That(visibilityMethod, Is.Not.Null);

            var bounds = new Bounds(
                new Vector3(2f, 3f, 0f),
                new Vector3(1.4f, 1.8f, 0f));
            var position = (Vector3)positionMethod.Invoke(null, new object[]
            {
                bounds,
                new Vector3(2f, 3f, 0f),
                0.12f,
            });
            Assert.That(position.x, Is.EqualTo(bounds.center.x).Within(0.001f));
            Assert.That(position.y,
                Is.EqualTo(bounds.max.y + 0.12f).Within(0.001f));

            Assert.That(visibilityMethod.Invoke(null, new object[]
            {
                true, GameState.Playing, false, true,
            }), Is.True);
            Assert.That(visibilityMethod.Invoke(null, new object[]
            {
                true, GameState.Lobby, false, true,
            }), Is.False);
            Assert.That(visibilityMethod.Invoke(null, new object[]
            {
                true, GameState.Playing, true, true,
            }), Is.False);
        }

        [Test]
        public void OriginalAndCloneOwnIndependentSingleHealthRenderers()
        {
            PlayerController original = CreatePlayer("HealthBarOriginal");
            original.GetComponent<SpriteRenderer>().sprite = CreateTestSprite();
            var originalBillboard =
                original.gameObject.AddComponent<PlayerHealthBillboard>();
            Invoke(originalBillboard, "Awake");

            Assert.That(CountDirectChildren(
                original.transform, "PlayerHealthBillboard"), Is.EqualTo(1));

            GameObject cloneObject = Track(Object.Instantiate(original.gameObject));
            var clone = cloneObject.GetComponent<PlayerController>();
            clone.ConfigureAsClone(1f);
            var cloneBillboard = cloneObject.GetComponent<PlayerHealthBillboard>();
            Invoke(cloneBillboard, "Awake");

            Assert.That(cloneBillboard, Is.Not.Null);
            Assert.That(CountDirectChildren(
                clone.transform, "PlayerHealthBillboard"), Is.EqualTo(1));
            Assert.That(cloneBillboard.HealthRenderer,
                Is.Not.SameAs(originalBillboard.HealthRenderer));
            Assert.That(clone.CurrentHealth, Is.EqualTo(clone.MaxHealth));
        }

        PlayerController CreatePlayer(string objectName)
        {
            var go = Track(new GameObject(objectName));
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Rigidbody2D>().gravityScale = 1f;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            var player = go.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            SetField(player, "shieldHitGraceDuration", 0f);
            ExpireDamageGrace(player);
            return player;
        }

        Sprite[] CreateFrames(string prefix)
        {
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                var texture = Track(new Texture2D(4, 4));
                var sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f, 4f);
                sprite.name = $"{prefix}_{i:00}";
                cleanup.Add(sprite);
                frames[i] = sprite;
            }
            return frames;
        }

        Sprite CreateTestSprite()
        {
            var texture = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            texture.SetPixels(Enumerable.Repeat(Color.black, 16).ToArray());
            texture.Apply();
            return Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f));
        }

        static int CountDirectChildren(Transform root, string objectName)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == objectName)
                    count++;
            return count;
        }

        static void SetBaseFrames(CharacterAnimator animator, Sprite[] frames)
        {
            string[] names =
            {
                "idle", "crouch", "launch", "rise",
                "apex", "fall", "dive", "land",
            };
            for (int i = 0; i < names.Length; i++)
                SetField(animator, names[i], frames[i]);
        }

        static void ExpireDamageGrace(PlayerController player)
        {
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
        }

        T Track<T>(T value) where T : Object
        {
            cleanup.Add(value);
            return value;
        }

        static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var property = type.GetProperty(fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            property?.SetValue(target, value);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
