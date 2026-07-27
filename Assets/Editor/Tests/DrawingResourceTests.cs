using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;

namespace MukJump.EditorTests
{
    public sealed class DrawingResourceTests
    {
        readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void PointerStepCannotSpendMoreInkThanAvailable()
        {
            var method = typeof(StrokeCapture).GetMethod(
                "LimitStepToAvailableInk",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            float limited = (float)method.Invoke(null, new object[] { 8f, 0.2f, 0.15f });

            Assert.That(limited, Is.EqualTo(0.35f).Within(0.000001f));
        }

        [Test]
        public void RapidStrokesScheduleEveryPlatformBeyondBudgetForFade()
        {
            var activeField = typeof(PlatformCollider).GetField(
                "active", BindingFlags.Static | BindingFlags.NonPublic);
            var ageField = typeof(PlatformCollider).GetField(
                "age", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeField, Is.Not.Null);
            Assert.That(ageField, Is.Not.Null);

            var active = (IList)activeField.GetValue(null);
            int initialCount = active.Count;
            var spawned = new List<PlatformCollider>();
            for (int i = 0; i < 6; i++)
            {
                var platform = PlatformCollider.Spawn(new List<Vector2>
                {
                    new(i, 0f),
                    new(i + 1f, 0f),
                });
                spawned.Add(platform);
                created.Add(platform.gameObject);
            }

            Assert.That(active.Count, Is.LessThanOrEqualTo(Mathf.Max(4, initialCount)));
            Assert.That((float)ageField.GetValue(spawned[0]), Is.GreaterThan(0f));
            Assert.That((float)ageField.GetValue(spawned[1]), Is.GreaterThan(0f));
        }

        [Test]
        public void ReplacingProceduralBrushDoesNotLeakOwnedTexture()
        {
            var reset = typeof(FallbackInkStyle).GetMethod(
                "ReleaseRuntimeAssets",
                BindingFlags.Static | BindingFlags.NonPublic);
            var textureField = typeof(FallbackInkStyle).GetField(
                "brushTexture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            Assert.That(textureField, Is.Not.Null);
            reset.Invoke(null, null);

            _ = FallbackInkStyle.SharedInkMaterial;
            var generated = (Texture2D)textureField.GetValue(null);
            var external = new Texture2D(8, 8);

            FallbackInkStyle.SetBrushTexture(external);

            Assert.That(generated == null, Is.True,
                "소유한 절차적 텍스처는 외부 텍스처로 교체할 때 해제해야 합니다.");
            Assert.AreSame(external, FallbackInkStyle.SharedInkMaterial.mainTexture);
            reset.Invoke(null, null);
            Assert.That(external != null, Is.True,
                "외부 에셋 텍스처의 소유권은 스타일러에 없습니다.");
            Object.DestroyImmediate(external);
        }

        [Test]
        public void UiMaskFactoryBatchesAndReleasesRuntimeAssets()
        {
            var factory = typeof(BrushTransitionView).Assembly.GetType(
                "MukJump.Core.InkUiTextureFactory");
            Assert.That(factory, Is.Not.Null);
            var createBlob = factory.GetMethod(
                "CreateBlobSprite", BindingFlags.Static | BindingFlags.Public);
            var release = factory.GetMethod(
                "ReleaseRuntimeAssets", BindingFlags.Static | BindingFlags.NonPublic);
            var blobField = factory.GetField(
                "blobSprite", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(createBlob, Is.Not.Null);
            Assert.That(release, Is.Not.Null);
            Assert.That(blobField, Is.Not.Null);

            var blob = (Sprite)createBlob.Invoke(null, null);
            Assert.That(blob.texture.width, Is.LessThanOrEqualTo(256));
            release.Invoke(null, null);

            Assert.That(blob == null, Is.True);
            Assert.That(blobField.GetValue(null), Is.Null);
        }
    }
}
