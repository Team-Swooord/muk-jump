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
        public void OverflowReleasesTheOldestStrokeBeforeNewerStrokes()
        {
            var spawned = new List<PlatformCollider>();
            for (int i = 0; i < 3; i++)
            {
                var platform = PlatformCollider.Spawn(new List<Vector2>
                {
                    new(i, 0f),
                    new(i + 1f, 0f),
                }, 5f);
                spawned.Add(platform);
                created.Add(platform.gameObject);
            }

            PlatformCollider.ReconcileActiveInkBudget(12f);

            Assert.That(spawned[0].RetainedInkCost,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(spawned[1].RetainedInkCost,
                Is.EqualTo(5f).Within(0.0001f));
            Assert.That(spawned[2].RetainedInkCost,
                Is.EqualTo(5f).Within(0.0001f));
            Assert.That(PlatformCollider.ActiveInkCost,
                Is.EqualTo(12f).Within(0.0001f));
            Assert.That(GetField<float>(spawned[0], "evictionTargetFraction"),
                Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void PartialEvictionTrimsColliderFromTheOldestVisibleEnd()
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
                points.Add(new Vector2(i * 0.25f, 0f));

            PlatformCollider platform = PlatformCollider.Spawn(points, 10f);
            created.Add(platform.gameObject);
            var edge = platform.GetComponent<EdgeCollider2D>();
            int originalPointCount = edge.pointCount;

            PlatformCollider.ReconcileActiveInkBudget(5f);
            Invoke(platform, "FadeVisual", 0.5f);
            Invoke(platform, "TrimCollider", 0.5f);

            Assert.That(edge.enabled, Is.True);
            Assert.That(edge.pointCount, Is.LessThan(originalPointCount));
            Assert.That(edge.points[0].x, Is.GreaterThan(0f),
                "오래된 획은 처음 그린 쪽부터 충돌 영역이 줄어야 합니다.");
            Assert.That(platform.Line.colorGradient.alphaKeys[0].alpha,
                Is.EqualTo(0f).Within(0.0001f),
                "콜라이더가 사라진 앞부분은 먹선도 함께 투명해야 합니다.");
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

        static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        static void Invoke(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, args);
        }
    }
}
