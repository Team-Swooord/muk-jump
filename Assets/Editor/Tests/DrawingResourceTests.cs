using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        public void RuntimeStrokeNaturallyExpiresThroughTheSharedFadePipeline()
        {
            var points = CreateDetailedStrokePoints();
            float activeBefore = PlatformCollider.ActiveInkCost;
            PlatformCollider platform = PlatformCollider.Spawn(
                points,
                3.2f,
                evictionFadeSeconds: 1.1f,
                evictionDelaySeconds: 0f,
                naturalHoldSeconds: PlatformCollider.DefaultNaturalHoldDuration);
            created.Add(platform.gameObject);
            var edge = platform.GetComponent<EdgeCollider2D>();
            int originalPointCount = edge.pointCount;

            Invoke(platform, "UpdateRuntimeDrawnPlatform", 3.39f, 3.39f);
            Assert.That(platform.RetainedInkCost,
                Is.EqualTo(3.2f).Within(0.0001f));
            Assert.That(GetField<float>(platform, "evictionTargetFraction"), Is.Zero);

            Invoke(platform, "UpdateRuntimeDrawnPlatform", 0.02f, 3.41f);
            Assert.That(platform.RetainedInkCost, Is.Zero,
                "자연 소멸을 시작한 획은 총 먹자리 ledger를 한 번만 반환해야 합니다.");
            Assert.That(PlatformCollider.ActiveInkCost,
                Is.EqualTo(activeBefore).Within(0.0001f));
            Assert.That(GetField<float>(platform, "evictionTargetFraction"), Is.EqualTo(1f));
            Assert.That(GetField<object>(platform, "removalCause").ToString(),
                Is.EqualTo("NaturalExpiry"));

            Invoke(platform, "UpdateRuntimeDrawnPlatform", 0.53f, 3.94f);
            Assert.That(GetField<float>(platform, "evictionVisualFraction"),
                Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(edge.pointCount, Is.LessThan(originalPointCount),
                "마르는 먹선은 보이는 시작점과 실제 충돌 영역이 함께 줄어야 합니다.");
        }

        [Test]
        public void NaturalExpiryNeverRestoresAPartiallyBudgetEvictedCollider()
        {
            PlatformCollider platform = PlatformCollider.Spawn(
                CreateDetailedStrokePoints(),
                4f,
                evictionFadeSeconds: 1.1f,
                evictionDelaySeconds: 0f,
                naturalHoldSeconds: PlatformCollider.DefaultNaturalHoldDuration);
            created.Add(platform.gameObject);
            var edge = platform.GetComponent<EdgeCollider2D>();

            Invoke(platform, "RequestBudgetEviction", 2f);
            float requestedAt = GetField<float>(platform, "evictionRequestedAt");
            Invoke(platform, "UpdateBudgetEviction", 0.55f, requestedAt + 0.55f);
            int partiallyTrimmedCount = edge.pointCount;
            Assert.That(GetField<float>(platform, "evictionVisualFraction"),
                Is.EqualTo(0.5f).Within(0.01f));

            SetField(platform, "naturalAge",
                PlatformCollider.DefaultNaturalHoldDuration - 0.01f);
            Invoke(platform, "UpdateRuntimeDrawnPlatform", 0.02f, requestedAt + 0.57f);

            Assert.That(GetField<float>(platform, "evictionTargetFraction"), Is.EqualTo(1f));
            Assert.That(edge.pointCount, Is.LessThanOrEqualTo(partiallyTrimmedCount),
                "시간 소멸과 FIFO 소멸이 겹쳐도 잘린 콜라이더를 되살리면 안 됩니다.");
            Assert.That(platform.RetainedInkCost, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletedNaturalExpiryReleasesRegistryAndLedgerExactlyOnce()
        {
            float inkBefore = PlatformCollider.ActiveInkCost;
            int countBefore = PlatformCollider.ActiveDrawnPlatformCount;
            PlatformCollider platform = PlatformCollider.Spawn(
                CreateDetailedStrokePoints(),
                2.4f,
                evictionFadeSeconds: 1.1f,
                evictionDelaySeconds: 0f,
                naturalHoldSeconds: PlatformCollider.DefaultNaturalHoldDuration);
            created.Add(platform.gameObject);

            Assert.That(PlatformCollider.ActiveDrawnPlatformCount, Is.EqualTo(countBefore + 1));
            Assert.That(PlatformCollider.ActiveInkCost,
                Is.EqualTo(inkBefore + 2.4f).Within(0.0001f));

            Invoke(platform, "UpdateRuntimeDrawnPlatform",
                PlatformCollider.DefaultNaturalHoldDuration,
                PlatformCollider.DefaultNaturalHoldDuration);
            Invoke(platform, "UpdateRuntimeDrawnPlatform", 1.1f,
                PlatformCollider.DefaultNaturalHoldDuration + 1.1f);

            Assert.That(GetField<float>(platform, "evictionVisualFraction"), Is.EqualTo(1f));
            Assert.That(GetField<bool>(platform, "removalRequested"), Is.True);
            Assert.That(platform.BreakFromHazard(), Is.False,
                "완료된 자연 소멸에 위험물 제거가 다시 진입하면 안 됩니다.");

            // Destroy는 프레임 끝에 실행됩니다. 실제 콜백 전후로 같은 정리 루틴이
            // 반복되어도 정적 ledger/registry가 정확히 한 번만 줄어드는지 고정합니다.
            Invoke(platform, "OnDestroy");
            Invoke(platform, "OnDestroy");
            Assert.That(PlatformCollider.ActiveDrawnPlatformCount, Is.EqualTo(countBefore));
            Assert.That(PlatformCollider.ActiveInkCost, Is.EqualTo(inkBefore).Within(0.0001f));

            yield return null;

            Assert.That(platform == null, Is.True);
            Assert.That(PlatformCollider.ActiveDrawnPlatformCount, Is.EqualTo(countBefore));
            Assert.That(PlatformCollider.ActiveInkCost, Is.EqualTo(inkBefore).Within(0.0001f));
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

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static List<Vector2> CreateDetailedStrokePoints()
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
                points.Add(new Vector2(i * 0.16f, 0f));
            return points;
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
