using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;

namespace MukJump.EditorTests
{
    public sealed class VfxQualityRuntimeTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
            VfxQualityRuntime.SetTier(
                VfxQualityTier.Medium,
                VfxQualityChangeReason.DebugOverride);
        }

        [TestCase(3000, 2048, 50, VfxQualityTier.Low)]
        [TestCase(8000, 400, 50, VfxQualityTier.Low)]
        [TestCase(8000, 2048, 40, VfxQualityTier.Low)]
        [TestCase(5000, 2048, 50, VfxQualityTier.Medium)]
        [TestCase(8000, 1200, 50, VfxQualityTier.Medium)]
        [TestCase(8000, 2048, 50, VfxQualityTier.High)]
        [TestCase(0, 0, 50, VfxQualityTier.High)]
        [TestCase(0, 0, 0, VfxQualityTier.High)]
        public void InitialRecommendation_알수없는_값을_저사양으로_오판하지_않는다(
            int memory,
            int graphicsMemory,
            int shaderLevel,
            VfxQualityTier expected)
        {
            Assert.That(
                VfxQualityRuntime.RecommendInitialTier(
                    memory,
                    graphicsMemory,
                    shaderLevel),
                Is.EqualTo(expected));
        }

        [Test]
        public void Profiles_핵심_실루엣은_유지하고_장식_예산만_단계적으로_늘린다()
        {
            VfxQualityProfile low =
                VfxQualityRuntime.GetProfile(VfxQualityTier.Low);
            VfxQualityProfile medium =
                VfxQualityRuntime.GetProfile(VfxQualityTier.Medium);
            VfxQualityProfile high =
                VfxQualityRuntime.GetProfile(VfxQualityTier.High);

            Assert.That(low.TransientLineLimit, Is.LessThan(medium.TransientLineLimit));
            Assert.That(medium.TransientLineLimit, Is.LessThan(high.TransientLineLimit));
            Assert.That(low.TransientSpriteLimit, Is.LessThan(medium.TransientSpriteLimit));
            Assert.That(medium.TransientSpriteLimit, Is.LessThan(high.TransientSpriteLimit));
            Assert.That(low.WeatherLineCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(low.CompositeConcurrentLimit, Is.EqualTo(1));
            Assert.That(high.CompositeConcurrentLimit, Is.EqualTo(3));
        }

        [TestCase(60, 47f)]
        [TestCase(45, 35f)]
        [TestCase(30, 26f)]
        [TestCase(-1, 47f)]
        public void Monitor_목표_프레임에_맞는_저성능_기준을_사용한다(
            int targetFrameRate,
            float expected)
        {
            Assert.That(
                VfxRuntimeMonitor.LowFpsThresholdForTarget(targetFrameRate),
                Is.EqualTo(expected));
        }

        [Test]
        public void Monitor_지속_저프레임과_간헐적_Hitch를_버리지_않고_한단계_낮춘다()
        {
            VfxQualityRuntime.SetTier(
                VfxQualityTier.High,
                VfxQualityChangeReason.DebugOverride);
            root = new GameObject("VfxRuntimeMonitorSamplingTests");
            var monitor = root.AddComponent<VfxRuntimeMonitor>();
            SetField(monitor, "warmupRemaining", 0f);
            var sample = typeof(VfxRuntimeMonitor).GetMethod(
                "ProcessFrameSample",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sample, Is.Not.Null);

            for (int i = 0; i < 235; i++)
                sample.Invoke(monitor, new object[] { 0.05f, true, 100f, 60 });
            sample.Invoke(monitor, new object[] { 0.3f, true, 100f, 60 });

            Assert.That(VfxQualityRuntime.Tier, Is.EqualTo(VfxQualityTier.Medium));
            Assert.That(monitor.MeasuredFps, Is.LessThan(47f));
        }

        [Test]
        public void Monitor_로비와_일시정지_프레임은_표본을_초기화한다()
        {
            root = new GameObject("VfxRuntimeMonitorStateTests");
            var monitor = root.AddComponent<VfxRuntimeMonitor>();
            SetField(monitor, "warmupRemaining", 0f);
            var sample = typeof(VfxRuntimeMonitor).GetMethod(
                "ProcessFrameSample",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sample, Is.Not.Null);
            for (int i = 0; i < 100; i++)
                sample.Invoke(monitor, new object[] { 0.05f, true, 20f, 60 });

            sample.Invoke(monitor, new object[] { 0.05f, false, 21f, 60 });

            Assert.That(GetField<float>(monitor, "sampledTime"), Is.Zero);
            Assert.That(GetField<int>(monitor, "sampledFrames"), Is.Zero);
            Assert.That(GetField<float>(monitor, "warmupRemaining"), Is.EqualTo(1f));
        }

        [Test]
        public void Monitor_디버그버튼은_Auto_Low_Medium_High_Auto로_순환한다()
        {
            root = new GameObject("VfxRuntimeMonitorCycleTests");
            var monitor = root.AddComponent<VfxRuntimeMonitor>();
            Assert.That(monitor.AutomaticQualityEnabled, Is.True);

            monitor.CycleQualityForDebug();
            Assert.That(monitor.AutomaticQualityEnabled, Is.False);
            Assert.That(VfxQualityRuntime.Tier, Is.EqualTo(VfxQualityTier.Low));
            monitor.CycleQualityForDebug();
            Assert.That(VfxQualityRuntime.Tier, Is.EqualTo(VfxQualityTier.Medium));
            monitor.CycleQualityForDebug();
            Assert.That(VfxQualityRuntime.Tier, Is.EqualTo(VfxQualityTier.High));
            monitor.CycleQualityForDebug();

            Assert.That(monitor.AutomaticQualityEnabled, Is.True);
        }

        [Test]
        public void FeedbackLinePool_장식과_중요_연출이_Critical_예약_슬롯을_쓰지_못한다()
        {
            VfxQualityRuntime.SetTier(
                VfxQualityTier.High,
                VfxQualityChangeReason.DebugOverride);
            var feedback = CreateFeedback();

            for (int i = 0; i < 6; i++)
                Assert.That(AcquireLine(feedback, VfxImportance.Normal), Is.Not.Null);
            Assert.That(AcquireLine(feedback, VfxImportance.Normal), Is.Null);

            Assert.That(AcquireLine(feedback, VfxImportance.Important), Is.Not.Null);
            Assert.That(AcquireLine(feedback, VfxImportance.Important), Is.Null);
            Assert.That(AcquireLine(feedback, VfxImportance.Critical), Is.Not.Null);
            Assert.That(AcquireLine(feedback, VfxImportance.Critical), Is.Null);
            Assert.That(feedback.ActiveLineVfxCount, Is.EqualTo(8));
        }

        [Test]
        public void FeedbackSpritePool_Important_둘과_Critical_넷을_예약한다()
        {
            VfxQualityRuntime.SetTier(
                VfxQualityTier.High,
                VfxQualityChangeReason.DebugOverride);
            var feedback = CreateFeedback();

            for (int i = 0; i < 10; i++)
                Assert.That(AcquireSprite(feedback, VfxImportance.Normal), Is.Not.Null);
            Assert.That(AcquireSprite(feedback, VfxImportance.Normal), Is.Null);
            for (int i = 0; i < 2; i++)
                Assert.That(AcquireSprite(feedback, VfxImportance.Important), Is.Not.Null);
            Assert.That(AcquireSprite(feedback, VfxImportance.Important), Is.Null);
            for (int i = 0; i < 4; i++)
                Assert.That(AcquireSprite(feedback, VfxImportance.Critical), Is.Not.Null);
            Assert.That(AcquireSprite(feedback, VfxImportance.Critical), Is.Null);
            Assert.That(feedback.ActiveSpriteVfxCount, Is.EqualTo(16));
        }

        [Test]
        public void FeedbackPrewarm_반복호출해도_현재품질_상한에서_계층이_늘지_않는다()
        {
            VfxQualityRuntime.SetTier(
                VfxQualityTier.High,
                VfxQualityChangeReason.DebugOverride);
            var feedback = CreateFeedback();
            var prewarm = typeof(GameFeedbackController).GetMethod(
                "PrewarmTransientPools",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(prewarm, Is.Not.Null);
            prewarm.Invoke(feedback, null);
            int firstCount = feedback.GetComponentsInChildren<TransientVfxElement>(true).Length;

            prewarm.Invoke(feedback, null);
            int secondCount = feedback.GetComponentsInChildren<TransientVfxElement>(true).Length;

            Assert.That(firstCount, Is.EqualTo(24));
            Assert.That(secondCount, Is.EqualTo(firstCount));
            Assert.That(feedback.GetComponentsInChildren<LineRenderer>(true).Length,
                Is.EqualTo(8));
            Assert.That(feedback.GetComponentsInChildren<SpriteRenderer>(true).Length,
                Is.EqualTo(16));
        }

        GameFeedbackController CreateFeedback()
        {
            root = new GameObject("VfxQualityRuntimeTests");
            return root.AddComponent<GameFeedbackController>();
        }

        static TransientVfxElement AcquireLine(
            GameFeedbackController feedback,
            VfxImportance importance)
        {
            return InvokeAcquire(
                feedback,
                "TryAcquireLineVfx",
                "TestLine",
                importance);
        }

        static TransientVfxElement AcquireSprite(
            GameFeedbackController feedback,
            VfxImportance importance)
        {
            return InvokeAcquire(
                feedback,
                "TryAcquireSpriteVfx",
                "TestSprite",
                importance);
        }

        static TransientVfxElement InvokeAcquire(
            GameFeedbackController feedback,
            string methodName,
            string objectName,
            VfxImportance importance)
        {
            var method = typeof(GameFeedbackController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(
                feedback,
                new object[] { objectName, importance }) as TransientVfxElement;
        }

        static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }
    }
}
