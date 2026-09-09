using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.AI;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class HudSoftEdgeTests
    {
        [TestCase(540f)]
        [TestCase(900f)]
        [TestCase(1100f)]
        public void BandHasShortTransparentBorderOpaqueCenterAndStableShape(float width)
        {
            var host = new GameObject("HudSoftEdgeTest", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(width, 148);
                var surface = InkHudSurface.Ensure(rect, false);
                using var first = new VertexHelper();
                using var second = new VertexHelper();
                Populate(surface, first); Populate(surface, second);
                Assert.That(first.currentVertCount, Is.EqualTo(909));
                for (int y = 0; y <= 8; y++)
                for (int x = 0; x <= 100; x++)
                {
                    var a = new UIVertex(); var b = new UIVertex();
                    first.PopulateUIVertex(ref a, y * 101 + x);
                    second.PopulateUIVertex(ref b, y * 101 + x);
                    Assert.That(a.position, Is.EqualTo(b.position), "재생성으로 무한 모션이 생기면 안 됩니다.");
                    Assert.That(a.color, Is.EqualTo(b.color));
                    Assert.That(a.position.x, Is.InRange(-width / 2, width / 2));
                    Assert.That(a.position.y, Is.InRange(-InkHudSurface.BandHeight / 2f, InkHudSurface.BandHeight / 2f));
                    if (x == 0 || x == 100 || y == 0 || y == 8)
                        Assert.That(a.color.a, Is.Zero);
                    else if (x >= 2 && x <= 98 && y >= 2 && y <= 6)
                        Assert.That(a.color.a, Is.GreaterThanOrEqualTo(184), "글자 뒤 농도는 유지합니다.");
                }
                // 정규 내부 열에서 상하 윤곽의 잦고 큰 단차가 재발하지 않도록 검사한다.
                for (int x = 3; x <= 97; x++)
                {
                    var a = new UIVertex(); var b = new UIVertex();
                    first.PopulateUIVertex(ref a, x - 1);
                    first.PopulateUIVertex(ref b, x);
                    Assert.That(Mathf.Abs(a.position.y - b.position.y), Is.LessThan(.45f));
                }
                var outer = new UIVertex(); var inner = new UIVertex();
                first.PopulateUIVertex(ref outer, 50);
                first.PopulateUIVertex(ref inner, 2 * 101 + 50);
                Assert.That(inner.position.y - outer.position.y, Is.InRange(1.8f, 2.5f));
                Assert.That(surface.raycastTarget, Is.False);
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(width, 148)));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void RenderBackgroundMeshComparisonWithFixedSampleLabels()
        {
            Scene original = SceneManager.GetActiveScene();
            bool wasDirty = original.isDirty;
            Scene scene = EditorSceneManager.NewPreviewScene();
            var owned = new List<Object>();
            RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                var font = Resources.Load<Font>("MukJump/Fonts/NanumBrushScript-Regular");
                Assert.That(font, Is.Not.Null);
                foreach (int size in new[] { 34, 46, 50, 60 })
                    font.RequestCharactersInTexture("풍→산들고도33m최고132m", size, FontStyle.Bold);
                for (int row = 0; row < 2; row++)
                {
                    float y = row == 0 ? 70 : -70;
                    var host = new GameObject("HudBandSource", typeof(RectTransform));
                    SceneManager.MoveGameObjectToScene(host, scene);
                    var rect = host.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(900, 148);
                    var surface = InkHudSurface.Ensure(rect, false);
                    using (var vertices = new VertexHelper())
                    {
                        if (row == 0) PopulateLegacy(vertices, surface.color);
                        else Populate(surface, vertices);
                        AddMesh(vertices, Texture2D.whiteTexture, new Vector3(0, y, 0));
                    }
                    var sealHost = new GameObject("FixedSealSource", typeof(RectTransform));
                    SceneManager.MoveGameObjectToScene(sealHost, scene);
                    var sealRect = sealHost.GetComponent<RectTransform>();
                    sealRect.sizeDelta = new Vector2(44, 44);
                    using (var vertices = new VertexHelper())
                    {
                        Populate(InkHudSurface.Ensure(sealRect, true), vertices);
                        AddMesh(vertices, Texture2D.whiteTexture, new Vector3(-401, y, -.1f));
                    }
                    AddText("풍", -401, y, 34, 44, InkPalette.TextLight);
                    AddText("→   산들", -253, y, 46, 230, InkPalette.TextDark);
                    AddText("고도 33m", 0, y, 60, 320, InkPalette.TextDark);
                    AddText("최고 132m", 275, y, 50, 250, InkPalette.TextDark);
                }
                var camera = new GameObject("HudMeshPreviewCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.cameraType = CameraType.Preview;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 140;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = InkPalette.Paper;
                target = new RenderTexture(1000, 280, 24, RenderTextureFormat.ARGB32);
                target.Create(); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                var capture = new Texture2D(1000, 280, TextureFormat.RGB24, false);
                owned.Add(capture);
                capture.ReadPixels(new Rect(0, 0, 1000, 280), 0, 0); capture.Apply();
                int ink = 0;
                foreach (Color32 pixel in capture.GetPixels32())
                    if (pixel.r < 110 && pixel.g < 110 && pixel.b < 110) ink++;
                Assert.That(ink, Is.GreaterThan(200), "고정된 예시 글자도 실제로 렌더되어야 합니다.");
                Directory.CreateDirectory("output/quality-polish/hud-soft-edges");
                File.WriteAllBytes("output/quality-polish/hud-soft-edges/before-after.png", capture.EncodeToPNG());
                Assert.That(original.isDirty, Is.EqualTo(wasDirty));

                void AddMesh(VertexHelper vertices, Texture texture, Vector3 position)
                {
                    var mesh = new Mesh(); owned.Add(mesh); vertices.FillMesh(mesh);
                    var material = new Material(FallbackInkStyle.SharedTintableBrushMaterial);
                    owned.Add(material); material.mainTexture = texture;
                    var go = new GameObject("HudPreviewMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    go.transform.position = position;
                }
                void AddText(string value, float x, float y, int size, float width, Color tint)
                {
                    var go = new GameObject("FixedSampleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var label = go.GetComponent<Text>();
                    label.rectTransform.sizeDelta = new Vector2(width, 82);
                    label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter; label.color = tint;
                    label.text = value; label.raycastTarget = false;
                    using var vertices = new VertexHelper();
                    typeof(Text).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                            null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(label, new object[] { vertices });
                    AddMesh(vertices, label.mainTexture, new Vector3(x, y, -.2f));
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            }
        }

        static void Populate(InkHudSurface surface, VertexHelper vertices) =>
            typeof(InkHudSurface).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                .Invoke(surface, new object[] { vertices });

        // 변경 전 HUD 배경의 고정 비교 기준. 런타임에는 포함되지 않는다.
        static void PopulateLegacy(VertexHelper mesh, Color color)
        {
            const int columns = 96, rows = 4;
            const float roughness = 1.7f;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns, v = y / (float)rows;
                float px = Mathf.Lerp(-450 + roughness, 450 - roughness, u);
                float py = Mathf.Lerp(-41 + roughness, 41 - roughness, v);
                if (x == 0 || x == columns) px += (Mathf.PerlinNoise(y * 1.37f, x + .41f) - .5f) * roughness;
                if (y == 0 || y == rows) py += (Mathf.PerlinNoise(x * 1.73f, y + .29f) - .5f) * roughness;
                Color tint = color;
                tint.a *= Mathf.Lerp(.93f, 1f, Mathf.PerlinNoise(x * 1.19f + .7f, y * 1.63f + .2f));
                mesh.AddVert(new Vector3(px, py), tint, new Vector2(u, v));
                if (x == 0 || y == 0) continue;
                int i = y * (columns + 1) + x;
                mesh.AddTriangle(i - columns - 2, i - 1, i);
                mesh.AddTriangle(i - columns - 2, i, i - columns - 1);
            }
        }
    }
}
