using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class AmbientCloudViewTests
    {
        GameObject root;
        Camera camera;
        AmbientCloudView view;
        SpriteRenderer[] layers;
        static readonly MethodInfo Refresh = typeof(AmbientCloudView).GetMethod(
            "Refresh", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AmbientCloudFixture");
            camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.aspect = 9f / 16f;
            var host = new GameObject("AmbientClouds");
            host.transform.SetParent(root.transform, false);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(
                MukJumpSceneBuilder.AmbientCloudAtlasPath).OfType<Sprite>().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(3));
            layers = new SpriteRenderer[AmbientCloudView.MaximumLayers];
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = new GameObject("Cloud_" + i);
                layer.transform.SetParent(host.transform, false);
                layers[i] = layer.AddComponent<SpriteRenderer>();
                layers[i].sprite = sprites[i % sprites.Length];
            }
            view = host.AddComponent<AmbientCloudView>();
            view.Configure(camera, layers);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [TestCase(true, true, false, false, true)]
        [TestCase(false, true, false, false, false)]
        [TestCase(true, false, false, false, false)]
        [TestCase(true, true, true, false, false)]
        [TestCase(true, true, false, true, false)]
        public void MotionRespectsEditorApplicationPauseAndAccessibility(
            bool playing, bool active, bool paused, bool reduced, bool expected)
        {
            Assert.That(AmbientCloudView.CanAnimate(playing, active, paused, reduced),
                Is.EqualTo(expected));
        }

        [Test]
        public void TenSecondsMovesOnlyCloudXByRoughlyThreeToFourPercent()
        {
            Vector3 cameraPosition = camera.transform.position;
            Vector3[] initial = layers.Select(layer => layer.transform.localPosition).ToArray();
            for (int frame = 0; frame < 600; frame++) Tick(1f / 60f);
            float width = camera.orthographicSize * 2f * camera.aspect;
            for (int i = 0; i < layers.Length; i++)
            {
                Vector3 moved = layers[i].transform.localPosition;
                Assert.That((moved.x - initial[i].x) / width,
                    Is.EqualTo(AmbientCloudView.LayerSpeed(i) * 10f).Within(0.00001f));
                Assert.That(AmbientCloudView.LayerSpeed(i) * 10f, Is.InRange(0.0269f, 0.044f));
                Assert.That(moved.y, Is.EqualTo(initial[i].y));
                Assert.That(moved.z, Is.EqualTo(initial[i].z));
            }
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
        }

        [Test]
        public void PausedMotionRetainsPositionAndResumeDeltaCannotJump()
        {
            Tick(0.05f);
            Vector3 before = layers[0].transform.localPosition;
            Tick(60f, false);
            Assert.That(layers[0].transform.localPosition, Is.EqualTo(before));
            Tick(600f);
            float expected = AmbientCloudView.LayerSpeed(0) * 0.05f *
                             camera.orthographicSize * 2f * camera.aspect;
            Assert.That(layers[0].transform.localPosition.x - before.x,
                Is.EqualTo(expected).Within(0.000002f));
            Tick(float.NaN);
            Assert.That(float.IsNaN(layers[0].transform.localPosition.x), Is.False);
        }

        [Test]
        public void WrapOccursOnlyBeyondBothScreenEdgesEvenAfterLongSessions()
        {
            for (int layer = 0; layer < layers.Length; layer++)
            {
                float halfWidth = AmbientCloudView.LayerWidth(layer) * 0.5f;
                float previous = AmbientCloudView.ViewportX(layer, 0d);
                int wraps = 0;
                for (int second = 1; second < 12000; second++)
                {
                    float current = AmbientCloudView.ViewportX(layer, second);
                    if (current < previous)
                    {
                        Assert.That(previous - halfWidth, Is.GreaterThan(1f));
                        Assert.That(current + halfWidth, Is.LessThan(0f));
                        wraps++;
                    }
                    previous = current;
                }
                Assert.That(wraps, Is.GreaterThan(5));
                Assert.That(AmbientCloudView.ViewportX(layer, 86400d * 365d),
                    Is.InRange(-halfWidth - 0.0251f, 1f + halfWidth + 0.0251f));
            }
        }

        [TestCase(VfxQualityTier.Low, 2)]
        [TestCase(VfxQualityTier.Medium, 3)]
        [TestCase(VfxQualityTier.High, 4)]
        public void QualityUsesFixedRenderersAndSharedTexture(VfxQualityTier tier, int count)
        {
            Sprite[] originalSprites = layers.Select(layer => layer.sprite).ToArray();
            Material[] originalMaterials = layers.Select(layer => layer.sharedMaterial).ToArray();
            for (int frame = 0; frame < 200; frame++) Tick(0.05f, true, tier);
            Assert.That(layers.Count(layer => layer.enabled), Is.EqualTo(count));
            Assert.That(view.transform.childCount, Is.EqualTo(4));
            for (int i = 0; i < layers.Length; i++)
            {
                Assert.That(layers[i].sprite, Is.SameAs(originalSprites[i]));
                Assert.That(layers[i].sharedMaterial, Is.SameAs(originalMaterials[i]));
                Assert.That(layers[i].sprite.texture, Is.SameAs(layers[0].sprite.texture));
                Assert.That(layers[i].sortingOrder, Is.EqualTo(-8));
            }
            Assert.That(view.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            Assert.That(view.GetComponentsInChildren<UnityEngine.UI.Graphic>().Length, Is.Zero);
        }

        [TestCase(1080, 1920)]
        [TestCase(1179, 2556)]
        [TestCase(1440, 3200)]
        [TestCase(1920, 1080)]
        public void ViewportResizeAndCameraTravelKeepCloudsBehindGameplay(int width, int height)
        {
            camera.aspect = width / (float)height;
            camera.orthographicSize = 11f;
            root.transform.position = new Vector3(2f, 1050f, -10f);
            Tick(0f, false, VfxQualityTier.High);
            for (int i = 0; i < layers.Length; i++)
            {
                Bounds bounds = layers[i].bounds;
                Assert.That(bounds.size.y / 22f, Is.LessThanOrEqualTo(0.1601f));
                Assert.That(bounds.min.y, Is.GreaterThan(1050f - 11f * 0.30f));
                Assert.That(layers[i].transform.localPosition.x / (22f * camera.aspect) + 0.5f,
                    Is.EqualTo(AmbientCloudView.ViewportX(i, 0d)).Within(0.00001f));
            }
        }

        [Test]
        public void DisableReenableDoesNotDuplicateOrResetDrift()
        {
            Tick(0.05f);
            Vector3 before = layers[0].transform.localPosition;
            typeof(AmbientCloudView).GetMethod("OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(layers.All(layer => !layer.enabled), Is.True);
            typeof(AmbientCloudView).GetMethod("OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(layers[0].transform.localPosition, Is.EqualTo(before));
            Assert.That(view.transform.childCount, Is.EqualTo(4));
        }

        [Test]
        public void BuilderCreatesOneSharedBackgroundCloudOwner()
        {
            var scene = MukJumpSceneBuilder.BuildForTests();
            try
            {
                var clouds = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<AmbientCloudView>(true)).ToArray();
                Assert.That(clouds.Length, Is.EqualTo(1));
                Assert.That(clouds[0].transform.parent.GetComponent<MapBackgroundView>(), Is.Not.Null);
                Assert.That(clouds[0].GetComponentInParent<Camera>(), Is.Not.Null);
                Assert.That(clouds[0].GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(4));
                Assert.That(clouds[0].GetComponentsInChildren<SpriteRenderer>(true)
                    .All(layer => layer.sprite != null), Is.True);
                var background = clouds[0].transform.parent.GetComponent<MapBackgroundView>();
                float[] positions = clouds[0].GetComponentsInChildren<SpriteRenderer>(true)
                    .Select(layer => layer.transform.localPosition.x).ToArray();
                for (int stage = 0; stage < background.BaseStageCount + background.EndlessStageCount; stage++)
                {
                    background.SetStage(stage, true, mirrorX: true);
                    Assert.That(clouds[0].GetComponentsInChildren<SpriteRenderer>(true)
                        .Select(layer => layer.transform.localPosition.x).ToArray(), Is.EqualTo(positions),
                        "고도별 배경 교체·반전은 구름의 누적 X 위치를 초기화하지 않아야 합니다.");
                }
            }
            finally { MukJumpSceneBuilder.CloseTestScene(scene); }
        }

        [Test]
        public void ImportedAtlasIsAlphaClampedAndNotCpuReadable()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(MukJumpSceneBuilder.AmbientCloudAtlasPath);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(2048));
            string source = File.ReadAllText("Assets/Scripts/Core/AmbientCloudView.cs");
            foreach (string forbidden in new[] { "Instantiate(", "Destroy(", "Resources.Load", ".material", "UnityEngine.Random" })
                Assert.That(source, Does.Not.Contain(forbidden));
        }

        void Tick(float dt, bool animate = true, VfxQualityTier tier = VfxQualityTier.High) =>
            Refresh.Invoke(view, new object[] { dt, animate, tier });
    }
}
