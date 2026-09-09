using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkVfxMotionTests
    {
        GameObject root;
        InkContactParticles particles;
        VfxQualityTier previousTier;

        [SetUp]
        public void SetUp()
        {
            previousTier = VfxQualityRuntime.Tier;
            VfxQualityRuntime.SetTier(VfxQualityTier.High, VfxQualityChangeReason.DebugOverride);
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            root = new GameObject("InkMotionTest");
            const string art = "Assets/MukJump/VFX/InkDropJump/Textures/";
            particles = new InkContactParticles(root.transform,
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkDropletAtlas_512.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkSplash_512.png"),
                Resources.Load<Shader>("MukJump/Shaders/InkContactParticle"));
        }

        [TearDown]
        public void TearDown()
        {
            particles?.Dispose();
            Object.DestroyImmediate(root);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            VfxQualityRuntime.SetTier(previousTier, VfxQualityChangeReason.DebugOverride);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void FragmentMotionMatchesAnalyticTrajectoryAcrossFrameRates(int fps)
        {
            Vector3 expectedPosition = Vector3.zero, expectedVelocity = new(3f, 4f);
            Vector3 position = expectedPosition, velocity = expectedVelocity;
            InkVfxMotion.Integrate(ref expectedPosition, ref expectedVelocity, Vector3.down * 4.5f, 2.2f, 1f);
            for (int i = 0; i < fps; i++)
                InkVfxMotion.Integrate(ref position, ref velocity, Vector3.down * 4.5f, 2.2f, 1f / fps);
            Assert.That(Vector3.Distance(position, expectedPosition), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(velocity, expectedVelocity), Is.LessThan(0.0001f));
        }

        [Test]
        public void TailHoldsImpactAndEndsSmoothlyWithoutBrightnessSpike()
        {
            Assert.That(InkVfxMotion.TailAlpha(0.1f), Is.EqualTo(1f));
            float previous = 1f;
            for (int i = 0; i <= 100; i++)
            {
                float value = InkVfxMotion.TailAlpha(i / 100f);
                Assert.That(value, Is.InRange(0f, previous));
                previous = value;
            }
            Assert.That(previous, Is.Zero);
        }

        [Test]
        public void WakeNeverCatchesUpAfterTeleportPauseOrLeaderSwap()
        {
            var sampler = new InkWakeSampler();
            Assert.That(sampler.Sample(1, Vector3.zero, 0.05f), Is.False);
            Assert.That(sampler.Sample(1, Vector3.up, 0.05f), Is.False);
            Assert.That(sampler.Sample(1, Vector3.up * 2, 0.05f), Is.True);
            Assert.That(sampler.Sample(1, Vector3.up * 52, 0.05f), Is.False);
            Assert.That(sampler.Sample(1, Vector3.up * 53, 8f), Is.False);
            Assert.That(sampler.Sample(2, Vector3.up * 54, 0.05f), Is.False);
            sampler.Reset();
            Assert.That(sampler.Sample(2, Vector3.up * 55, 0.05f), Is.False);
        }

        [Test]
        public void StationaryWakeAndZeroDeltaDoNotEmit()
        {
            var sampler = new InkWakeSampler();
            for (int i = 0; i < 240; i++)
                Assert.That(sampler.Sample(1, Vector3.zero, 0.05f), Is.False);
            Assert.That(sampler.Sample(1, Vector3.up, 0f), Is.False);
        }

        [TestCase(VfxQualityTier.Low, 37)]
        [TestCase(VfxQualityTier.Medium, 62)]
        [TestCase(VfxQualityTier.High, 90)]
        public void AirSlipLeavesCapacityForContactAndNeverAddsSystems(VfxQualityTier tier, int capacity)
        {
            VfxQualityRuntime.SetTier(tier, VfxQualityChangeReason.DebugOverride);
            for (int i = 0; i < 100; i++) particles.EmitAirSlip(Vector3.zero, Vector2.up * 9f);
            int before = particles.ActiveCount;
            Assert.That(before, Is.GreaterThan(0));
            particles.EmitLanding(Vector3.zero, 1f);
            Assert.That(particles.ActiveCount, Is.GreaterThan(before));
            Assert.That(particles.Capacity, Is.EqualTo(capacity));
            Assert.That(particles.ActiveCount, Is.LessThanOrEqualTo(capacity));
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void WallParticlesTravelAwayFromWallAndDecelerate(float direction)
        {
            particles.EmitImpact(Vector3.zero, Vector2.right * direction, 1f);
            var system = root.transform.Find("InkContactParticles/BallisticInk").GetComponent<ParticleSystem>();
            var snapshot = new ParticleSystem.Particle[48];
            int count = system.GetParticles(snapshot);
            Assert.That(count, Is.GreaterThan(0));
            foreach (var p in snapshot)
                if (p.startLifetime > 0f) Assert.That(p.velocity.x * direction, Is.GreaterThan(0f));
            float velocity = Mathf.Abs(snapshot[0].velocity.x);
            particles.Advance(0.05f, true);
            system.GetParticles(snapshot);
            Assert.That(Mathf.Abs(snapshot[0].velocity.x), Is.LessThan(velocity));
            Assert.That(system.isPlaying, Is.False);
        }

        [Test]
        public void NewDecorationsRespectReducedAndDoNotChangeGameplayRandom()
        {
            var state = Random.state;
            float expected = Random.value;
            Random.state = state;
            particles.EmitAirSlip(Vector3.zero, Vector2.up * 12);
            particles.EmitImpact(Vector3.zero, Vector2.left, 1f);
            Assert.That(Random.value, Is.EqualTo(expected));
            Random.state = state;
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            particles.EmitAirSlip(Vector3.zero, Vector2.up * 12);
            particles.EmitImpact(Vector3.zero, Vector2.left, 1f);
            Assert.That(particles.ActiveCount, Is.Zero);
        }

        [Test]
        public void FeedbackWallHookUsesExistingMaskedParticles()
        {
            var feedback = root.AddComponent<GameFeedbackController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameFeedbackController).GetField("contactParticles", flags).SetValue(feedback, particles);
            feedback.PlayWallHit(Vector3.zero, 1f);
            Assert.That(particles.ActiveCount, Is.GreaterThan(0));
            Assert.That(feedback.ActiveSpriteVfxCount, Is.Zero, "구형 공용 방울을 중복하지 않습니다.");
            typeof(GameFeedbackController).GetMethod("OnDisable", flags).Invoke(feedback, null);
        }
    }
}
