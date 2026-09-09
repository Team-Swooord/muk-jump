using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.AI;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class SharedHanjiUiTests
    {
        [TestCase(96, 96)]
        [TestCase(344, 120)]
        [TestCase(220, 256)]
        [TestCase(520, 176)]
        [TestCase(900, 82)]
        public void SharedPaperKeepsOneFrameAndDoesNotChangeHitArea(int width, int height)
        {
            var go = new GameObject("PaperTest", typeof(RectTransform), typeof(Image));
            try
            {
                var paper = go.GetComponent<Image>();
                paper.rectTransform.sizeDelta = new Vector2(width, height);
                paper.raycastTarget = false;
                InkUiStyle.ConfigureHanjiSurface(paper);
                InkUiStyle.ConfigureHanjiSurface(paper);
                Assert.That(paper.sprite.texture, Is.SameAs(InkUiTextureFactory.CreateGrowthPaperRibbonSprite().texture));
                Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(paper.raycastTarget, Is.False);
                Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(new Vector2(width, height)));
                var frames = go.GetComponentsInChildren<GrowthRingFrameGraphic>();
                Assert.That(frames.Length, Is.EqualTo(1));
                Assert.That(frames[0].sprite, Is.SameAs(Resources.Load<Sprite>("MukJump/UI/PermanentGrowth/pg_selected_ring")));
                Assert.That(frames[0].raycastTarget, Is.False);
                Assert.That(frames[0].BrushScale, Is.InRange(.25f, 1f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HudUsesSharedPaperWithoutTheOldOpaqueBand()
        {
            var go = new GameObject("HudTest", typeof(RectTransform));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(900, 148);
                var legacy = InkHudSurface.Ensure(rect, false);
                InkHudSurface.Ensure(rect, false);
                Assert.That(legacy.enabled, Is.False);
                Assert.That(rect.Find("HudHanjiCard").GetComponent<Image>().sprite, Is.SameAs(InkUiStyle.ActionButtonSprite));
                Assert.That(rect.Find("HudHanjiCard").GetComponent<RectTransform>().rect.height, Is.EqualTo(InkHudSurface.BandHeight));
                Assert.That(go.GetComponentsInChildren<HanjiCardFrame>().Length, Is.EqualTo(1));
                foreach (Graphic graphic in go.GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WindIsLargerWithBrushArrowAndNoAmbientName()
        {
            var go = new GameObject("WindTest", typeof(RectTransform));
            try
            {
                WindIndicatorView wind = BuildWind(go.transform);
                wind.ApplyPolishedLayout();
                Assert.That(wind.transform.Find("WindAlertSeal").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(66, 66)));
                Assert.That(wind.transform.Find("DirectionArrow/BrushArrow").GetComponent<InkBrushIcon>(), Is.Not.Null);
                Assert.That(wind.transform.Find("DirectionArrow/Shaft").GetComponent<Graphic>().enabled, Is.False);
                Assert.That(wind.transform.Find("WindStateText").GetComponent<Text>().enabled, Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RenderSharedPaperAndGameplayHeader()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var owned = new List<Object>();
            var previous = RenderTexture.active;
            RenderTexture target = null;
            try
            {
                var host = new GameObject("HanjiUiPreview", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(host, scene);
                var font = InkPalette.UiFont;
                const string glyphs = "고도 7m최고221m한국어고객센터튜토리얼순위계정연동누적500m마다먹빛1개매판오른높이가합산돼요.PlayBestNEW!";
                foreach (int size in new[] { 28, 36, 40, 42, 64 })
                    font.RequestCharactersInTexture(glyphs, size, FontStyle.Bold);

                var hud = Rect("Hud", host.transform, new Vector2(0, 240), new Vector2(900, 148));
                InkHudSurface.Ensure(hud, false);
                BuildWind(hud);
                Label(hud, "고도 7m", new Vector2(0, 0), new Vector2(290, 82), 64, InkPalette.Ink);
                Label(hud, "최고", new Vector2(243, 22), new Vector2(180, 30), 28, InkPalette.TextMuted);
                Label(hud, "221m", new Vector2(243, -16), new Vector2(180, 46), 42, InkPalette.Ink);
                var pause = Rect("Pause", hud, new Vector2(396, 0), new Vector2(48, 48)).gameObject.AddComponent<InkBrushIcon>();
                pause.Configure(InkBrushIcon.Symbol.Pause);
                string[] menu = { "한국어", "고객센터", "튜토리얼", "순위" };
                for (int i = 0; i < menu.Length; i++)
                {
                    var panel = Paper("Menu", host.transform, new Vector2(i % 2 == 0 ? -183 : 183, 100 - i / 2 * 140), new Vector2(344, 120));
                    Label(panel, menu[i], Vector2.zero, new Vector2(280, 72), 42, InkPalette.Ink);
                }
                var account = Paper("Account", host.transform, new Vector2(0, -180), new Vector2(540, 120));
                Label(account, "계정 연동", Vector2.zero, new Vector2(450, 72), 42, InkPalette.Ink);
                var help = Paper("Help", host.transform, new Vector2(0, -360), new Vector2(520, 176));
                Label(help, "누적 500m마다 먹빛 1개", new Vector2(0, 30), new Vector2(464, 60), 40, InkPalette.Ink);
                Label(help, "매 판 오른 높이가 합산돼요.", new Vector2(0, -32), new Vector2(464, 64), 36, InkPalette.TextMuted);

                int order = 0;
                foreach (Graphic graphic in host.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.enabled || graphic.color.a <= 0) continue;
                    using var vertices = new VertexHelper();
                    graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                    if (vertices.currentVertCount == 0) continue;
                    var mesh = new Mesh(); owned.Add(mesh); vertices.FillMesh(mesh);
                    var material = new Material(FallbackInkStyle.SharedTintableBrushMaterial);
                    owned.Add(material); material.mainTexture = graphic.mainTexture;
                    var go = new GameObject("PreviewMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    go.GetComponent<MeshRenderer>().sortingOrder = order++;
                    go.transform.SetPositionAndRotation(graphic.transform.position, graphic.transform.rotation);
                    go.transform.localScale = graphic.transform.lossyScale;
                }
                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene; camera.cameraType = CameraType.Preview;
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 420;
                camera.transform.position = new Vector3(0, -60, -100);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = InkPalette.Paper2;
                target = new RenderTexture(1000, 840, 24); target.Create(); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                var capture = new Texture2D(1000, 840, TextureFormat.RGB24, false); owned.Add(capture);
                capture.ReadPixels(new UnityEngine.Rect(0, 0, 1000, 840), 0, 0); capture.Apply();
                Directory.CreateDirectory("output/quality-polish/shared-hanji");
                File.WriteAllBytes("output/quality-polish/shared-hanji/panels-and-hud.png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value);
            }
        }

        static WindIndicatorView BuildWind(Transform parent) => (WindIndicatorView)typeof(MukJumpSceneBuilder)
            .GetMethod("CreateWindIndicator", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { parent, false });

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static RectTransform Paper(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            InkUiStyle.ConfigureHanjiSurface(image); return rect;
        }
        static void Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var text = Rect("Label", parent, position, size).gameObject.AddComponent<Text>();
            text.font = InkPalette.UiFont; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.alignByGeometry = true;
            text.text = value; text.color = color; text.raycastTarget = false;
        }
    }
}
