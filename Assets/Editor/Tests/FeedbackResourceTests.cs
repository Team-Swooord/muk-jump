using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class FeedbackResourceTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void DamageSoundIsDistinctShortAndHasQuietTail()
        {
            root = new GameObject("DamageSoundTests");
            var feedback = root.AddComponent<GameFeedbackController>();
            Invoke(feedback, "EnsureInitialized");
            var clip = (AudioClip)GetField(feedback, "damageHitClip");
            Assert.That(clip, Is.Not.SameAs(GetField(feedback, "wallHitClip")));
            Assert.That(clip.length, Is.EqualTo(.16f).Within(.001f));
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            float peak = 0f, tailPeak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.That(float.IsFinite(samples[i]), Is.True);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                if (i >= samples.Length - 441) tailPeak = Mathf.Max(tailPeak, Mathf.Abs(samples[i]));
            }
            Assert.That(peak, Is.InRange(.25f, 1f));
            Assert.That(tailPeak, Is.LessThan(.01f));
        }

        [Test]
        public void RuntimeGeneratedAudioAndSpriteAreReleasedWhenDisabled()
        {
            root = new GameObject("FeedbackResourceTests");
            var feedback = root.AddComponent<GameFeedbackController>();
            Invoke(feedback, "EnsureInitialized");

            var clips = (IList)GetField(feedback, "ownedRuntimeClips");
            Assert.That(clips.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(GetField(feedback, "dotSprite"), Is.Not.Null);

            Invoke(feedback, "OnDisable");

            Assert.That(clips.Count, Is.Zero);
            Assert.That(GetField(feedback, "dotSprite"), Is.Null);
            Assert.That(GetField(feedback, "jumpClip"), Is.Null);
        }

        [Test]
        public void GoldenGaugeUsesTintWithoutRuntimeTextureAllocation()
        {
            MethodInfo tintMethod = typeof(PrototypeHud).GetMethod(
                "ResolveGaugeFillTint",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(tintMethod, Is.Not.Null);
            Assert.That(
                (Color)tintMethod.Invoke(null, new object[] { false }),
                Is.EqualTo(Color.white));
            Assert.That(
                (Color)tintMethod.Invoke(null, new object[] { true }),
                Is.EqualTo(InkPalette.Gold));
            Assert.That(
                typeof(PrototypeHud).GetMethod(
                    "CreateColoredSilhouette",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null,
                "황금 상태 표시 때문에 GPU readback과 Texture2D 복사를 만들면 안 됩니다.");
            Assert.That(
                typeof(PrototypeHud).GetField(
                    "goldenGaugeFill",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "HUD가 원본과 같은 크기의 황금 텍스처를 보유하면 안 됩니다.");
        }

        [Test]
        public void DeathPopIsShortWithFastAttackAndQuietTail()
        {
            float[] samples = DeathPopSamples();
            Assert.That(samples.Length, Is.EqualTo(7497), "사망음은 0.17초로 짧게 끝난다.");
            Assert.That(samples[0], Is.Zero);
            Assert.That(samples[samples.Length - 1], Is.Zero);
            double energy = 0, tailEnergy = 0, earlyEnergy = 0, sum = 0, peak = 0;
            int peakIndex = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double value = samples[i];
                Assert.That(float.IsNaN(samples[i]) || float.IsInfinity(samples[i]), Is.False);
                double squared = value * value;
                energy += squared;
                sum += value;
                if (i < 4410) earlyEnergy += squared;
                if (i >= 5292) tailEnergy += squared;
                if (System.Math.Abs(value) > peak)
                {
                    peak = System.Math.Abs(value);
                    peakIndex = i;
                }
            }
            Assert.That(peak, Is.InRange(0.5, 0.98), "파열감은 살리되 클리핑하지 않는다.");
            Assert.That(peakIndex, Is.LessThan(662), "첫 15ms 안에 터져야 한다.");
            Assert.That(earlyEnergy / energy, Is.GreaterThan(0.99));
            Assert.That(tailEnergy / energy, Is.LessThan(0.001));
            Assert.That(System.Math.Abs(sum / samples.Length), Is.LessThan(0.01));
        }

        [Test]
        public void DeathPopGenerationIsDeterministic()
        {
            CollectionAssert.AreEqual(DeathPopSamples(), DeathPopSamples(),
                "폴백이 매 실행마다 다른 전자음이 되지 않게 한다.");
        }

        [Test]
        public void ShippedDeathWavMatchesShortPopFallback()
        {
            float[] expected = DeathPopSamples();
            byte[] bytes = File.ReadAllBytes(
                "Assets/Resources/MukJump/Audio/SFX/SFX_Character_Death.wav");
            using var reader = new BinaryReader(new MemoryStream(bytes));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
            reader.ReadInt32();
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"));
            bool foundData = false, foundFormat = false;
            while (reader.BaseStream.Position + 8 <= bytes.Length)
            {
                string chunk = new string(reader.ReadChars(4));
                int size = reader.ReadInt32();
                long end = reader.BaseStream.Position + size;
                Assert.That(end, Is.LessThanOrEqualTo(bytes.Length));
                if (chunk == "fmt ")
                {
                    Assert.That(reader.ReadInt16(), Is.EqualTo(1)); // PCM
                    Assert.That(reader.ReadInt16(), Is.EqualTo(1)); // 모노
                    Assert.That(reader.ReadInt32(), Is.EqualTo(44100));
                    reader.ReadInt32();
                    reader.ReadInt16();
                    Assert.That(reader.ReadInt16(), Is.EqualTo(16));
                    foundFormat = true;
                }
                else if (chunk == "data")
                {
                    Assert.That(size, Is.EqualTo(expected.Length * 2));
                    for (int i = 0; i < expected.Length; i++)
                        Assert.That(reader.ReadInt16() / 32767f,
                            Is.EqualTo(expected[i]).Within(1f / 32767f), $"sample {i}");
                    foundData = true;
                }
                reader.BaseStream.Position = end + (size & 1);
            }
            Assert.That(foundFormat && foundData, Is.True);
        }

        static float[] DeathPopSamples()
        {
            return (float[])typeof(GameFeedbackController).GetMethod(
                "BuildDeathPopSamples", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

    }
}
