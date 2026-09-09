using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class RestPlatformEdgeTests
    {
        [Test]
        public void SharedTextureHasTransparentEndsAndPreservesInteriorBrushGrain()
        {
            Material material = FallbackInkStyle.SharedRestPlatformMaterial;
            Assert.That(FallbackInkStyle.SharedRestPlatformMaterial, Is.SameAs(material));
            Assert.That(material, Is.Not.SameAs(FallbackInkStyle.SharedTintableBrushMaterial));
            var source = (Texture2D)material.mainTexture;
            Assert.That(source.isReadable, Is.False, "공유 텍스처의 CPU 사본을 유지하지 않습니다.");
            Texture2D rest = null, original = null;
            try
            {
                rest = ReadTexture(source);
                original = ReadTexture(FallbackInkStyle.SharedTintableBrushMaterial.mainTexture);
                float interior = 0f;
                for (int y = 0; y < rest.height; y++)
                {
                    Assert.That(rest.GetPixel(0, y).a, Is.Zero);
                    Assert.That(rest.GetPixel(rest.width - 1, y).a, Is.Zero);
                    for (int x = 16; x <= 240; x += 16)
                    {
                        float expected = original.GetPixel(x, y).a;
                        float actual = rest.GetPixel(x * 2, y).a;
                        Assert.That(actual, Is.EqualTo(expected).Within(2f / 255f));
                        interior += actual;
                    }
                }
                Assert.That(interior, Is.GreaterThan(50f), "빈 텍스처로 절단면만 숨기면 안 됩니다.");
            }
            finally { Object.DestroyImmediate(rest); Object.DestroyImmediate(original); }
        }

        [Test]
        public void EndSealIsShortFeatheredAndNotAStraightVerticalCut()
        {
            var sample = typeof(FallbackInkStyle).GetMethod("RestEndCoverage",
                BindingFlags.Static | BindingFlags.NonPublic);
            float Coverage(float u, float v) => (float)sample.Invoke(null, new object[] { u, v });
            float earliest = 1f, latest = 0f;
            int partialSamples = 0;
            for (int row = 0; row <= 12; row++)
            {
                float v = row / 12f;
                Assert.That(Coverage(0, v), Is.Zero);
                Assert.That(Coverage(1, v), Is.Zero);
                Assert.That(Coverage(.05f, v), Is.EqualTo(1f));
                Assert.That(Coverage(.95f, v), Is.EqualTo(1f));
                float lastLeft = 0, lastRight = 0;
                for (int column = 0; column <= 50; column++)
                {
                    float u = column / 1000f;
                    float left = Coverage(u, v), right = Coverage(1f - u, v);
                    Assert.That(left, Is.GreaterThanOrEqualTo(lastLeft));
                    Assert.That(right, Is.GreaterThanOrEqualTo(lastRight));
                    if (left > 0 && left < 1) partialSamples++;
                    if (left >= .5f && lastLeft < .5f)
                    { earliest = Mathf.Min(earliest, u); latest = Mathf.Max(latest, u); }
                    lastLeft = left; lastRight = right;
                }
            }
            Assert.That(latest - earliest, Is.GreaterThan(.012f));
            Assert.That(partialSamples, Is.GreaterThan(80));
        }

        [Test]
        public void RenderActualRestPlatformBeforeAndAfterWithoutEnteringPlay()
        {
            Scene original = SceneManager.GetActiveScene();
            bool wasDirty = original.isDirty;
            // 미저장 Test Runner 씬을 저장하거나 닫지 않는 별도 렌더 씬이다.
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D capture = null;
            var previous = RenderTexture.active;
            try
            {
                for (int row = 0; row < 2; row++)
                {
                    float y = row == 0 ? .65f : -.65f;
                    var platform = PlatformCollider.SpawnMapRestPlatform(new List<Vector2>
                    { new(-2.4f, y), new(2.4f, y) });
                    SceneManager.MoveGameObjectToScene(platform.gameObject, scene);
                    foreach (var line in platform.GetComponentsInChildren<LineRenderer>())
                    {
                        line.gameObject.layer = 31;
                        // 위쪽은 변경 전의 공용 붓결, 아래쪽은 실제 수정된 쉼터 렌더러다.
                        if (row == 0) line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
                    }
                }
                var camera = new GameObject("RestEdgeCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.cameraType = CameraType.Preview;
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 1.4f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = InkPalette.Paper;
                target = new RenderTexture(1000, 280, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                int greenPixels = 0;
                foreach (Color32 pixel in capture.GetPixels32())
                    if (pixel.g > pixel.r + 2 && pixel.g > pixel.b + 2) greenPixels++;
                Assert.That(greenPixels, Is.GreaterThan(100), "실제 쉼터가 보이는 Unity 렌더인지 확인합니다.");
                string folder = "output/quality-polish/rest-platform-ends";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "before-after.png"), capture.EncodeToPNG());
                Assert.That(original.isDirty, Is.EqualTo(wasDirty));
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                SceneManager.SetActiveScene(original);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static Texture2D ReadTexture(Texture source)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            catch { if (copy != null) Object.DestroyImmediate(copy); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
