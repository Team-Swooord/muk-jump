using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class InkContactParticleTests
    {
        const string Art = "Assets/MukJump/VFX/InkDropJump/Textures/";
        GameObject root;
        InkContactParticles effect;
        VfxQualityTier previousTier;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            previousTier = VfxQualityRuntime.Tier;
            SetTier(VfxQualityTier.High);
            root = new GameObject("InkContactParticleTests");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_VFX_InkDropletAtlas_512.png");
            var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_VFX_InkSplash_512.png");
            var shader = Resources.Load<Shader>("MukJump/Shaders/InkContactParticle");
            Assert.That(atlas, Is.Not.Null);
            Assert.That(splash, Is.Not.Null);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            effect = new InkContactParticles(root.transform, atlas, splash, shader);
        }

        [TearDown]
        public void TearDown()
        {
            effect?.Dispose();
            Object.DestroyImmediate(root);
            SetTier(previousTier);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [TestCase(VfxQualityTier.Low, 9, 37)]
        [TestCase(VfxQualityTier.Medium, 13, 62)]
        [TestCase(VfxQualityTier.High, 18, 90)]
        public void Jump_HasThreeLayersAndBoundedTierCounts(VfxQualityTier tier, int count, int capacity)
        {
            SetTier(tier);
            effect.EmitJump(Vector3.zero, Vector2.up, false);
            Assert.That(effect.ActiveCount, Is.EqualTo(count));
            Assert.That(effect.Capacity, Is.EqualTo(capacity));
            var layers = root.GetComponentsInChildren<ParticleSystem>();
            Assert.That(layers.Length, Is.EqualTo(3));
            foreach (var layer in layers)
            {
                Assert.That(layer.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(layer.main.playOnAwake, Is.False);
                Assert.That(layer.main.loop, Is.False);
                Assert.That(layer.emission.enabled, Is.False);
                Assert.That(layer.collision.enabled, Is.False);
                Assert.That(layer.noise.enabled, Is.False);
                Assert.That(layer.lights.enabled, Is.False);
                Assert.That(layer.trails.enabled, Is.False);
                Assert.That(layer.isPlaying, Is.False, "자동 재생과 수동 시뮬레이션을 중복 실행하면 안 됩니다.");
            }
        }

        [Test]
        public void RepeatedBurst_ReusesThreeSystemsAndTwoMaterials_WithinHardLimit()
        {
            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>();
            Material atlasMaterial = renderers[0].sharedMaterial;
            Material washMaterial = renderers[2].sharedMaterial;
            Assert.That(renderers[1].sharedMaterial, Is.SameAs(atlasMaterial));
            for (int i = 0; i < 200; i++)
            {
                effect.EmitLanding(Vector3.zero, 1f);
                effect.EmitJump(Vector3.zero, Vector2.up, false);
                Assert.That(effect.ActiveCount, Is.LessThanOrEqualTo(90));
            }
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));
            Assert.That(renderers[0].sharedMaterial, Is.SameAs(atlasMaterial));
            Assert.That(renderers[2].sharedMaterial, Is.SameAs(washMaterial));
            for (int i = 0; i < 30; i++) effect.Advance(0.05f, true);
            Assert.That(effect.ActiveCount, Is.Zero);
            effect.EmitLanding(Vector3.zero, 1f);
            Assert.That(effect.ActiveCount, Is.EqualTo(25));
        }

        [Test]
        public void Pause_FreezesPositionsAndLifetime_ResumeDoesNotCatchUp()
        {
            effect.EmitJump(new Vector3(3f, 8f), Vector2.up, false);
            var drops = root.transform.Find("InkContactParticles/BallisticInk").GetComponent<ParticleSystem>();
            var before = new ParticleSystem.Particle[48];
            var after = new ParticleSystem.Particle[48];
            int count = drops.GetParticles(before);
            effect.Advance(12f, false);
            Assert.That(drops.GetParticles(after), Is.EqualTo(count));
            Assert.That(after[0].position, Is.EqualTo(before[0].position));
            Assert.That(after[0].remainingLifetime, Is.EqualTo(before[0].remainingLifetime));
            effect.Advance(12f, true);
            drops.GetParticles(after);
            Assert.That(before[0].remainingLifetime - after[0].remainingLifetime, Is.EqualTo(0.05f).Within(0.001f));
        }

        [Test]
        public void ReducedMotion_ClearsMovingLayersImmediately_AndEmitsNothing()
        {
            effect.EmitLanding(Vector3.zero, 1f);
            Assert.That(effect.MovingCount, Is.GreaterThan(0));
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Assert.That(effect.ActiveCount, Is.Zero, "Update 전에 설정을 바꿔도 즉시 비웁니다.");
            effect.Advance(0f, false);
            effect.EmitJump(Vector3.zero, Vector2.up, false);
            effect.EmitLanding(Vector3.zero, 1f);
            Assert.That(effect.ActiveCount, Is.Zero);
        }

        [Test]
        public void LowMemoryTier_ClearsOldBudget_AndKeepsSystems()
        {
            effect.EmitLanding(Vector3.zero, 1f);
            VfxQualityRuntime.SetTier(VfxQualityTier.Low, VfxQualityChangeReason.LowMemory);
            Assert.That(effect.ActiveCount, Is.Zero, "강등 이벤트 프레임에서 즉시 비웁니다.");
            effect.Advance(0f, false);
            Assert.That(effect.ActiveCount, Is.Zero);
            Assert.That(effect.Capacity, Is.EqualTo(37));
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));
        }

        [Test]
        public void DecorativeRandom_DoesNotChangeGameplayRandomSequence()
        {
            Random.State previous = Random.state;
            try
            {
                Random.InitState(91);
                float expected = Random.value;
                Random.InitState(91);
                effect.EmitJump(Vector3.zero, Vector2.right, true);
                effect.EmitLanding(Vector3.zero, 1f);
                Assert.That(Random.value, Is.EqualTo(expected));
            }
            finally { Random.state = previous; }
        }

        [Test]
        public void Swarm_24JumpRequests_EmitOneBurst_AndDisableClearsEverything()
        {
            var feedback = root.AddComponent<GameFeedbackController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameFeedbackController).GetField("contactParticles", flags).SetValue(feedback, effect);
            for (int i = 0; i < 24; i++) feedback.PlayDirectionalJump(Vector3.zero, Vector2.up);
            typeof(GameFeedbackController).GetMethod("LateUpdate", flags).Invoke(feedback, null);
            Assert.That(effect.ActiveCount, Is.EqualTo(18));
            typeof(GameFeedbackController).GetMethod("OnDisable", flags).Invoke(feedback, null);
            Assert.That(effect.ActiveCount, Is.Zero);
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.Zero);
        }

        static void SetTier(VfxQualityTier value) => VfxQualityRuntime.SetTier(value, VfxQualityChangeReason.DebugOverride);

        [TestCase(VfxQualityTier.Low, 5, 4)]
        [TestCase(VfxQualityTier.Medium, 7, 6)]
        [TestCase(VfxQualityTier.High, 10, 8)]
        public void Creation_StrokeAndCloneHaveDifferentBoundedShapes(VfxQualityTier tier, int strokeCount, int cloneCount)
        {
            SetTier(tier);
            effect.EmitStrokeSettle(Vector3.zero);
            Assert.That(effect.ActiveCount, Is.EqualTo(strokeCount));
            effect.Clear();
            effect.EmitCloneConvergence(Vector3.zero);
            Assert.That(effect.ActiveCount, Is.EqualTo(cloneCount));
            var motes = root.transform.Find("InkContactParticles/DryBrushMotes").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[36];
            int count = motes.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                Assert.That(Vector3.Dot(particles[i].position, particles[i].velocity), Is.LessThan(0f));
                Assert.That(particles[i].startLifetime, Is.InRange(0.18f, 0.24f));
            }
            Assert.That(motes.isPlaying, Is.False);
            for (int i = 0; i < 12; i++) effect.Advance(0.05f, true);
            Assert.That(effect.ActiveCount, Is.Zero);
        }

        [Test]
        public void Creation_RepeatedSwarmSharesContactBudget_AndReducedEmitsNothing()
        {
            var state = Random.state;
            float expected = Random.value;
            Random.state = state;
            for (int i = 0; i < 24; i++)
            {
                effect.EmitCloneConvergence(Vector3.zero);
                effect.EmitStrokeSettle(Vector3.zero);
                effect.EmitLanding(Vector3.zero, 1f);
            }
            Assert.That(effect.ActiveCount, Is.LessThanOrEqualTo(90));
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));
            Assert.That(Random.value, Is.EqualTo(expected));
            Random.state = state;
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            effect.EmitCloneConvergence(Vector3.zero);
            effect.EmitStrokeSettle(Vector3.zero);
            Assert.That(effect.ActiveCount, Is.Zero);
        }

        [Test]
        public void Creation_InvalidStrokeDoesNotEmitNewParticles()
        {
            var feedback = root.AddComponent<GameFeedbackController>();
            typeof(GameFeedbackController).GetField("contactParticles", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(feedback, effect);
            feedback.PlayStrokeResolved(Vector3.zero, false);
            Assert.That(effect.ActiveCount, Is.Zero);
            feedback.PlayStrokeResolved(Vector3.zero, true);
            Assert.That(effect.ActiveCount, Is.EqualTo(10));
        }

        [Test]
        public void Creation_ConvergenceCarriesFastAscentVelocityWithoutChangingIt()
        {
            Vector2 velocity = new Vector2(2.6f, 40f);
            effect.EmitCloneConvergence(Vector3.zero, velocity);
            var motes = root.transform.Find("InkContactParticles/DryBrushMotes").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[36];
            int count = motes.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                Vector2 expected = (Vector2)particles[i].position +
                    (Vector2)particles[i].velocity * particles[i].startLifetime;
                Assert.That(Vector2.Distance(expected, velocity * particles[i].startLifetime), Is.LessThan(0.001f));
            }
            Assert.That(velocity, Is.EqualTo(new Vector2(2.6f, 40f)));
        }
    }
}
