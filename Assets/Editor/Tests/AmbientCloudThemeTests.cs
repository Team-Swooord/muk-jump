using System;
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
    public sealed class AmbientCloudThemeTests
    {
        GameObject root;
        Camera camera;
        AmbientCloudView view;
        SpriteRenderer[] layers;
        AmbientCloudTheme[] themes;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void Setup()
        {
            themes = MukJumpSceneBuilder.BuildAmbientThemes();
            root = new GameObject("AmbientThemeFixture");
            camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            camera.aspect = 9f / 16f;
            var host = new GameObject("Clouds");
            host.transform.SetParent(root.transform, false);
            layers = new SpriteRenderer[4];
            for (int i = 0; i < layers.Length; i++)
            {
                var child = new GameObject("Layer_" + i);
                child.transform.SetParent(host.transform, false);
                layers[i] = child.AddComponent<SpriteRenderer>();
                layers[i].sprite = themes[0].sprites[i % 3];
            }
            view = host.AddComponent<AmbientCloudView>();
            view.Configure(camera, layers, themes);
            for (int i = 0; i < 60; i++) Tick(0.05f, false);
        }

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [Test]
        public void EveryAtlasHasRealAlphaAndSlicesInsideTransparentGutters()
        {
            foreach (string path in MukJumpSceneBuilder.AmbientThemeAtlasPaths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(source, System.IO.File.ReadAllBytes(path)), Is.True);
                    Color32[] pixels = source.GetPixels32();
                    Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True, "실제 alpha가 필요합니다: " + path);
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                    Assert.That(sprites.Length, Is.EqualTo(3), path);
                    foreach (Sprite sprite in sprites)
                    {
                        foreach (int y in new[] { (int)sprite.rect.yMin, (int)sprite.rect.yMax - 1 })
                            for (int x = 0; x < source.width; x++)
                                Assert.That(pixels[y * source.width + x].a, Is.LessThanOrEqualTo(20),
                                    $"원화 꼬리가 슬라이스 경계에 닿습니다: {path} / {y}");
                    }
                }
                finally { Object.DestroyImmediate(source); }
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void EachMapSelectsItsOwnArtAndMotion(int stage)
        {
            Assert.That(themes.Length, Is.EqualTo(7));
            Assert.That(themes.Select(t => t.sprites[0].texture).Distinct().Count(), Is.EqualTo(7));
            view.SetBackground(themes[stage].background, true);
            Assert.That(view.CurrentMotion, Is.EqualTo((AmbientCloudMotion)stage));
            Assert.That(layers.All(r => r.sprite.texture == themes[stage].sprites[0].texture), Is.True);
            foreach (int width in new[] { 1080, 1179, 1440, 1920 })
            {
                int height = width == 1080 ? 1920 : width == 1179 ? 2556 : width == 1440 ? 3200 : 1080;
                camera.aspect = width / (float)height;
                camera.transform.position = new Vector3(0f, 1050f, -10f);
                for (int i = 0; i < 120; i++) Tick(0.05f);
                foreach (SpriteRenderer layer in layers)
                {
                    Assert.That(layer.bounds.size.y / 19.2f, Is.LessThanOrEqualTo(0.1601f));
                    Assert.That(layer.bounds.min.y, Is.GreaterThan(1050f - 19.2f * 0.15f));
                    Assert.That(float.IsNaN(layer.transform.position.x), Is.False);
                    Assert.That(layer.color.a, Is.InRange(0f, 0.43f));
                }
            }
        }

        [Test]
        public void RenderClearerCloudMotionAtActualSpeed()
        {
            MukJumpAmbientCloudPreview.CaptureAllMapsTo("output/ambient-clouds/clearer-motion");
            for (int map = 0; map < themes.Length; map++)
            {
                string folder = $"output/ambient-clouds/clearer-motion/map-{map:00}";
                Assert.That(System.IO.File.Exists(folder + "/frame-000.png"), Is.True);
                Assert.That(System.IO.File.Exists(folder + "/frame-059.png"), Is.True);
            }
        }

        [Test]
        public void AllSevenMapsMoveMoreClearlyWithoutChangingDirectionOrLayerCount()
        {
            float[] oldSpeeds = { 0.0017f, 0.0024f, 0.0020f, 0.0015f };
            float width = camera.orthographicSize * 2f * camera.aspect;
            foreach (var theme in themes)
            {
                view.SetBackground(theme.background, true);
                typeof(AmbientCloudView).GetField("flowSeconds", Private).SetValue(view, 0d);
                Tick(0f, false);
                Vector3[] positions = layers.Select(layer => layer.transform.localPosition).ToArray();
                float flow = AmbientCloudMotionProfile.For(theme.motion).Flow;
                for (int frame = 0; frame < 600; frame++) Tick(1f / 60f);
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    float actual = (layers[layer].transform.localPosition.x - positions[layer].x) / width;
                    float expected = oldSpeeds[layer] * flow * 10f * 1.8f;
                    Assert.That(actual, Is.EqualTo(expected).Within(0.00001f), theme.motion.ToString());
                    Assert.That(Mathf.Abs(actual), Is.InRange(0.012f, 0.05f));
                    Assert.That(Mathf.Sign(actual), Is.EqualTo(Mathf.Sign(flow)));
                }
                Assert.That(view.transform.childCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void SpriteMappingSurvivesReorderedMissingAndUnknownMaps()
        {
            var reordered = new[] { themes[0], themes[6], themes[2], themes[5] };
            view.Configure(camera, layers, reordered);
            view.SetBackground(themes[6].background, true);
            Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.RiverFlow));
            view.SetBackground(themes[1].background, true);
            Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.Clouds));
            view.SetBackground(null, true);
            Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.Clouds));
        }

        [Test]
        public void MissingSpritesFallbackAndPartialThemesNeverProduceNullLayers()
        {
            themes[1].sprites = new Sprite[] { null, themes[1].sprites[1], null };
            view.SetBackground(themes[1].background, true);
            Assert.That(layers.All(r => r.sprite == themes[1].sprites[1]), Is.True);
            themes[1].sprites = Array.Empty<Sprite>();
            view.SetBackground(themes[1].background, true);
            Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.Clouds));
            themes[0].sprites = null;
            view.SetBackground(null, true);
            Assert.That(layers.All(r => r.sprite != null), Is.True);
        }

        [Test]
        public void RapidThemeRequestsSwapOnlyAtZeroAlphaWithoutRestartingMotion()
        {
            Sprite original = layers[0].sprite;
            double initialClock = Clock();
            view.SetBackground(themes[1].background);
            for (int i = 0; i < 4; i++) Tick(0.05f, false);
            Assert.That(layers[0].sprite, Is.SameAs(original));
            float previousAlpha = layers[0].color.a;
            view.SetBackground(themes[2].background);
            view.SetBackground(themes[2].background);
            bool swappedAtZero = false;
            for (int i = 0; i < 40; i++)
            {
                Sprite before = layers[0].sprite;
                Tick(0.05f, false);
                if (layers[0].sprite != before)
                {
                    swappedAtZero = true;
                    Assert.That(layers.All(r => r.color.a == 0f), Is.True);
                    Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.RainMist));
                }
                Assert.That(layers[0].sprite.texture, Is.Not.SameAs(themes[1].sprites[0].texture));
            }
            Assert.That(swappedAtZero, Is.True);
            Assert.That(layers[0].color.a, Is.GreaterThan(previousAlpha));
            Assert.That(Clock(), Is.EqualTo(initialClock), "일시정지 중 테마만 교체하고 이동 시간은 고정한다.");
        }

        [Test]
        public void OneHundredThemeChangesKeepFourRenderersMaterialsAndHorizontalPosition()
        {
            Material[] materials = layers.Select(r => r.sharedMaterial).ToArray();
            Tick(0.05f);
            float[] positions = layers.Select(r => r.transform.localPosition.x).ToArray();
            double clock = Clock();
            for (int i = 0; i < 100; i++)
            {
                int stage = i % 7;
                view.SetBackground(themes[stage].background, true, i % 2 == 1);
                Assert.That(layers.Select(r => r.transform.localPosition.x).ToArray(), Is.EqualTo(positions));
                Assert.That(layers[0].flipX, Is.EqualTo(i % 2 == 1));
                Assert.That(layers.Select(r => r.sharedMaterial).ToArray(), Is.EqualTo(materials));
                Assert.That(view.transform.childCount, Is.EqualTo(4));
            }
            Assert.That(Clock(), Is.EqualTo(clock));
        }

        [TestCase(2)] [TestCase(5)]
        public void ReverseFlowWrapsOutsideTheViewport(int stage)
        {
            view.SetBackground(themes[stage].background, true);
            var flow = typeof(AmbientCloudView).GetField("flowSeconds", Private);
            float previous = layers[0].transform.localPosition.x;
            int wraps = 0;
            for (int i = 1; i < 15000; i++)
            {
                flow.SetValue(view, -(double)i);
                Tick(0f, false);
                float current = layers[0].transform.localPosition.x;
                if (current > previous)
                {
                    float cameraWidth = camera.aspect * 19.2f;
                    Assert.That(previous + AmbientCloudView.LayerWidth(0) * cameraWidth * 0.5f,
                        Is.LessThan(-cameraWidth * 0.5f));
                    Assert.That(layers[0].bounds.min.x, Is.GreaterThan(cameraWidth * 0.5f));
                    wraps++;
                }
                previous = current;
            }
            Assert.That(wraps, Is.GreaterThan(5));
        }

        [Test]
        public void ImmediateMapReplacementClearsStaleFadeDestinationBeforeDisable()
        {
            var mapObject = new GameObject("Map");
            mapObject.transform.SetParent(root.transform, false);
            var current = mapObject.AddComponent<SpriteRenderer>();
            var nextObject = new GameObject("Next");
            nextObject.transform.SetParent(mapObject.transform, false);
            var next = nextObject.AddComponent<SpriteRenderer>();
            var map = mapObject.AddComponent<MapBackgroundView>();
            var so = new SerializedObject(map);
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.FindProperty("currentRenderer").objectReferenceValue = current;
            so.FindProperty("nextRenderer").objectReferenceValue = next;
            so.FindProperty("ambientClouds").objectReferenceValue = view;
            var stages = so.FindProperty("stageSprites");
            stages.arraySize = 2;
            for (int i = 0; i < 2; i++) stages.GetArrayElementAtIndex(i).objectReferenceValue = themes[i].background;
            so.ApplyModifiedPropertiesWithoutUndo();
            map.SetStage(0, true);
            typeof(MapBackgroundView).GetField("transitionTargetStage", Private).SetValue(map, 1);
            view.SetBackground(themes[1].background);
            map.SetStage(0, true);
            typeof(MapBackgroundView).GetMethod("OnDisable", Private).Invoke(map, null);
            Assert.That(current.sprite, Is.SameAs(themes[0].background));
            Assert.That(view.CurrentMotion, Is.EqualTo(AmbientCloudMotion.Clouds));
        }

        void Tick(float deltaTime, bool animate = true) => typeof(AmbientCloudView)
            .GetMethod("Refresh", Private).Invoke(view, new object[] { deltaTime, animate, VfxQualityTier.High });
        double Clock() => (double)typeof(AmbientCloudView).GetField("elapsedSeconds", Private).GetValue(view);
    }
}
