using System.Reflection;
using System.IO;
using System.Linq;
using NUnit.Framework;
using MukJump.Core;
using MukJump.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameplayHudViewTests
{
    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void UpdraftIndicatorUsesOneAlignedArrowAndLabel(GameLanguage language)
    {
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        GameLocalization.SetLanguage(language);
        var host = new GameObject("UpdraftLayoutTest", typeof(RectTransform));
        try
        {
            var view = BuildWindIndicator(host.transform);
            var weather = host.AddComponent<WindWeatherController>();
            typeof(WindWeatherController).GetMethod("SetPhase", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(weather, new object[] { WindWeatherPhase.Updraft });
            typeof(WindIndicatorView).GetMethod("ApplyState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { weather });
            var icon = view.transform.Find("WindAlertSeal").GetComponent<Image>();
            var arrow = (RectTransform)view.transform.Find("DirectionArrow");
            var label = view.transform.Find("WindStateText").GetComponent<Text>();
            Assert.That(icon.enabled, Is.False, "같은 뜻의 바람 아이콘을 중복 표시하지 않습니다.");
            Assert.That(label.enabled, Is.True);
            Assert.That(arrow.anchoredPosition.y, Is.EqualTo(label.rectTransform.anchoredPosition.y));
            Assert.That(arrow.anchoredPosition.x + arrow.sizeDelta.x * .5f,
                Is.LessThan(label.rectTransform.anchoredPosition.x - label.rectTransform.sizeDelta.x * .5f));
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.sizeDelta.x));
            Assert.That(label.rectTransform.anchoredPosition.y, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(host);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }
    }

    [Test]
    public void RenderCenteredRecordAndPauseFromActualUi()
    {
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        GameLocalization.SetLanguage(GameLanguage.English);
        var scene = MukJumpSceneBuilder.BuildForTests();
        var pauseHost = new GameObject("RecordPausePreview");
        var cameraHost = new GameObject("RecordHudPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(pauseHost, scene);
        SceneManager.MoveGameObjectToScene(cameraHost, scene);
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D capture = null;
        try
        {
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayHudView>(true)).Single();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameplayHudView).GetMethod("ApplyPolishedRuntimeLayout", flags).Invoke(hud, null);
            var top = (RectTransform)hud.transform.Find("TopHudRoot");
            LobbyAdLayout.ClearTopInset();
            Rect layout = GameplayHudView.CalculateTopHudRect(new Rect(0, 0, 1080, 1920), 1080, 1920);
            top.anchoredPosition = new Vector2(layout.center.x, layout.yMax);
            top.localScale = Vector3.one;
            var height = top.Find("HeightDisplay/HeightText").GetComponent<Text>();
            var best = top.Find("BestText").GetComponent<Text>();
            InkLocalizedText.SetSource(height, "고도 204m");
            typeof(GameplayHudView).GetMethod("ApplyRecordScoreLayout", flags).Invoke(hud, new object[] { true });
            InkLocalizedText.SetSource(best, "204m");
            best.color = InkPalette.Red;
            var record = top.GetComponentInChildren<NewBestIndicatorView>(true);
            record.transform.Find("RecordSeal").GetComponent<Image>().sprite = InkUiTextureFactory.CreateBlobSprite();
            record.ApplyPolishedLayout();
            record.GetComponent<CanvasGroup>().alpha = 1;
            var pause = pauseHost.AddComponent<PauseMenuView>();
            typeof(PauseMenuView).GetMethod("BuildIfNeeded", flags).Invoke(pause, null);
            var pauseRect = (RectTransform)pauseHost.transform.Find("PauseMenuCanvas/PauseButton");
            var safe = new Rect(0, 0, 1080, 1920);
            pauseRect.anchoredPosition = GameplayHudView.CalculatePauseTouchRect(safe, 1080, 1920).center;
            ((RectTransform)pauseRect.Find("Visual")).anchoredPosition =
                GameplayHudView.CalculatePauseButtonPosition(safe, 1080, 1920) - pauseRect.anchoredPosition;
            pauseRect.Find("Visual").localScale = Vector3.one;
            var camera = cameraHost.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 180;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            foreach (var canvas in new[] { hud.GetComponent<Canvas>(), pauseHost.GetComponentInChildren<Canvas>(true) })
            {
                canvas.enabled = true;
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvas.transform;
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(1080, 360);
                canvas.worldCamera = camera;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            }
            target = new RenderTexture(1080, 360, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(1080, 360, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1080, 360), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("output/quality-polish/record-hud");
            File.WriteAllBytes("output/quality-polish/record-hud/new-and-pause.png", capture.EncodeToPNG());
            int scoreInk = 0;
            for (int y = 265; y < 310; y++)
                for (int x = 400; x < 670; x++)
                    if (capture.GetPixel(x, y).grayscale < .35f) scoreInk++;
            Assert.That(scoreInk, Is.GreaterThan(100), "NEW!가 떠도 가운데 고도 숫자가 잘리거나 사라지면 안 됩니다.");
            int scoreTopPixel = -1, recordBottomPixel = capture.height;
            for (int y = 265; y < 350; y++)
                for (int x = 440; x < 640; x++)
                {
                    Color pixel = capture.GetPixel(x, y);
                    bool isRecord = pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 1.2f;
                    if (isRecord) recordBottomPixel = Mathf.Min(recordBottomPixel, y);
                    else if (pixel.grayscale < .35f) scoreTopPixel = Mathf.Max(scoreTopPixel, y);
                }
            Assert.That(recordBottomPixel - scoreTopPixel, Is.GreaterThanOrEqualTo(4),
                "폰트의 투명 행간이 아니라 실제 고도 먹획과 NEW! 사이 여백을 검사합니다.");
            int red = 0;
            for (int y = 310; y < 350; y++)
                for (int x = 480; x < 600; x++)
                {
                    Color color = capture.GetPixel(x, y);
                    if (color.r > color.g * 1.8f && color.r > color.b * 1.5f) red++;
                }
            Assert.That(red, Is.GreaterThan(20), "가운데 점수 위, 같은 띠 안에 붉은 NEW!가 렌더되어야 합니다.");
        }
        finally
        {
            RenderTexture.active = previous;
            cameraHost.GetComponent<Camera>().targetTexture = null;
            if (capture != null) Object.DestroyImmediate(capture);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(pauseHost);
            MukJumpSceneBuilder.CloseTestScene(scene);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            InkLocalizedText.RefreshAll();
        }
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void CenteredRecordBadgeFitsNewLabelWithoutShrinking(GameLanguage language)
    {
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        var host = new GameObject("NewBadgeTest", typeof(RectTransform));
        try
        {
            GameLocalization.SetLanguage(language);
            var view = NewBestIndicatorView.CreateRuntime(host.transform);
            var root = (RectTransform)view.transform;
            var text = root.Find("RecordSeal/SealText").GetComponent<Text>();
            Assert.That(root.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(root.anchoredPosition, Is.EqualTo(new Vector2(0, NewBestIndicatorView.BadgeCenterOffsetY)));
            Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(NewBestIndicatorView.BadgeSize, NewBestIndicatorView.BadgeHeight)));
            Assert.That(root.Find("RecordSeal").GetComponent<Image>().enabled, Is.False);
            Assert.That(text.text, Is.EqualTo("NEW!"));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.fontSize, Is.EqualTo(NewBestIndicatorView.BadgeFontSize));
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            var generator = new TextGenerator();
            Assert.That(generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit,
                Is.LessThanOrEqualTo(text.rectTransform.rect.width));
            Assert.That(generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit,
                Is.LessThanOrEqualTo(text.rectTransform.rect.height));
            foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }
        finally
        {
            Object.DestroyImmediate(host);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            InkLocalizedText.RefreshAll();
        }
    }

    [Test]
    public void RecordStampIsOneShotAndReusesItsWash()
    {
        var host = new GameObject("RecordStampTest", typeof(RectTransform));
        try
        {
            var view = NewBestIndicatorView.CreateRuntime(host.transform);
            var stamp = view.transform.Find("RecordSeal");
            var wash = view.transform.Find("ImpactWash").GetComponent<Image>();
            int count = view.GetComponentsInChildren<Transform>(true).Length;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            for (int run = 0; run < 3; run++)
            {
                typeof(NewBestIndicatorView).GetMethod("ShowRecord", flags).Invoke(view, new object[] { true });
                typeof(NewBestIndicatorView).GetMethod("ApplyStampProgress", flags).Invoke(view, new object[] { 0.25f });
                Assert.That(wash.color.a, Is.GreaterThan(0));
                typeof(NewBestIndicatorView).GetMethod("ApplyStampProgress", flags).Invoke(view, new object[] { 1f });
                Assert.That(stamp.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Quaternion.Angle(stamp.localRotation, Quaternion.identity), Is.LessThan(0.01f));
                Assert.That(wash.color.a, Is.Zero);
                typeof(NewBestIndicatorView).GetMethod("HideRecord", flags).Invoke(view, null);
                Assert.That(wash.color.a, Is.Zero);
                Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(1080, 1920, 0, 0, 1080, 1920)]
    [TestCase(1179, 2556, 0, 102, 1179, 2310)]
    [TestCase(1536, 2048, 0, 40, 1536, 1980)]
    [TestCase(2048, 1536, 40, 20, 1968, 1480)]
    public void RecordBadgeAndPauseSlotKeepReadableGaps(int width, int height, int x, int y, int safeWidth, int safeHeight)
    {
        var safe = new Rect(x, y, safeWidth, safeHeight);
        Rect hud = GameplayHudView.CalculateTopHudRect(safe, width, height);
        float scale = GameplayHudView.CalculateTopHudScale(safe, width, height);
        float badgeCenter = hud.center.y + NewBestIndicatorView.BadgeCenterOffsetY * scale;
        float maxWashRadius = 24f * scale;
        Assert.That(badgeCenter + maxWashRadius,
            Is.LessThan(-MobileUiLayout.GetLogicalTopInset(safe, width, height)));
        // 글자 칸에는 투명 행간이 포함되므로 실제 획 사이 간격은 위 렌더 테스트에서 검사한다.
        var pause = GameplayHudView.CalculatePauseButtonPosition(safe, width, height);
        float faceLeft = pause.x - PauseMenuView.PauseVisualSize * scale * 0.5f;
        float bestRight = hud.xMin + hud.width * 0.77f + 90f * scale;
        Assert.That(faceLeft - bestRight, Is.GreaterThan(20f * scale));
        Assert.That(pause.y, Is.EqualTo(hud.center.y));
    }

    [TestCase(false, 900f, 148f)]
    [TestCase(true, 44f, 44f)]
    public void HudSurfaceHasFullHeightSquareEndsAndDoesNotCaptureTouches(
        bool seal, float width, float height)
    {
        var host = new GameObject("SurfaceTest", typeof(RectTransform));
        try
        {
            var root = host.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(width, height);
            var surface = InkHudSurface.Ensure(root, seal);
            Assert.That(InkHudSurface.Ensure(root, seal), Is.SameAs(surface));
            Assert.That(root.childCount, Is.EqualTo(seal ? 1 : 2));
            Assert.That(surface.raycastTarget, Is.False);
            Assert.That(surface.rectTransform.rect.height, Is.EqualTo(seal ? 44f : InkHudSurface.BandHeight));
            using (var mesh = new VertexHelper())
            {
                typeof(InkHudSurface).GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Invoke(surface, new object[] { mesh });
                int columns = seal ? 12 : 100;
                int rows = seal ? 12 : 8;
                var bottom = new UIVertex();
                var top = new UIVertex();
                foreach (int column in new[] { 0, columns / 2, columns })
                {
                    mesh.PopulateUIVertex(ref bottom, column);
                    mesh.PopulateUIVertex(ref top, rows * (columns + 1) + column);
                    Assert.That(top.position.y - bottom.position.y,
                        Is.GreaterThan(surface.rectTransform.rect.height - 6f),
                        "왼쪽/오른쪽 끝이 붓꼬리처럼 가늘어지면 안 됩니다.");
                    if (seal) Assert.That(bottom.color.a, Is.GreaterThan(140));
                    else Assert.That(bottom.color.a, Is.Zero, "한지 가장자리만 부드럽게 번집니다.");
                }
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void LegacyWindSealBecomesOneFixedSizeInkIconWithoutSquareOrLetter()
    {
        var host = new GameObject("WindTest", typeof(RectTransform));
        try
        {
            var view = host.AddComponent<WindIndicatorView>();
            var old = new GameObject("WindAlertSeal", typeof(RectTransform), typeof(Image));
            old.transform.SetParent(host.transform, false);
            var label = new GameObject("SealText", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(old.transform, false);
            var image = old.GetComponent<Image>();
            image.color = InkPalette.Red;
            image.rectTransform.localScale = Vector3.one * 0.94f;
            var surface = InkHudSurface.Ensure(image.rectTransform, true);
            typeof(WindIndicatorView).GetField("alertSeal",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, image);
            view.ApplyPolishedLayout();
            view.ApplyPolishedLayout();
            Assert.That(image.enabled, Is.False);
            Assert.That(image.sprite, Is.SameAs(Resources.Load<Sprite>(WindIndicatorView.WindIconResourcePath)));
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(old.GetComponentsInChildren<InkHudSurface>().Length, Is.EqualTo(1));
            Assert.That(surface.enabled, Is.False, "기존 빨간 사각형이 아이콘 뒤에 남으면 안 됩니다.");
            Assert.That(label.GetComponent<Text>().enabled, Is.False);
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(66f, 66f)));
            Assert.That(image.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.24f, 0.5f)));
            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(image.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(label.transform.parent, Is.SameAs(old.transform));
            foreach (bool alert in new[] { false, true, false })
            {
                typeof(WindIndicatorView).GetMethod("ConfigureStaticVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                typeof(WindIndicatorView).GetMethod("ApplyInkVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { 1f, alert });
                Assert.That(image.enabled, Is.False);
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(surface.enabled, Is.False);
                Assert.That(label.GetComponent<Text>().enabled, Is.False);
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void SceneBuilderCreatesWindIconWithoutLegacyDecorations()
    {
        var host = new GameObject("WindBuilderTest", typeof(RectTransform));
        try
        {
            var view = BuildWindIndicator(host.transform);
            var image = view.transform.Find("WindAlertSeal").GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.enabled, Is.False);
            Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(66, 66)));
            Assert.That(image.transform.childCount, Is.Zero, "풍 글자나 네모 배경은 생성하지 않습니다.");
            Assert.That(view.transform.Find("DirectionArrow").childCount, Is.EqualTo(4));
            Assert.That(view.transform.Find("WindStateText").GetComponent<Text>().text, Is.EqualTo("산들"));
            Assert.That(view.transform.Find("WindStateText").GetComponent<Text>().enabled, Is.True);
            foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void RuntimeFallbackCreatesOnlyOneWindIcon()
    {
        var host = new GameObject("WindRuntimeFallback", typeof(RectTransform));
        try
        {
            var view = host.AddComponent<WindIndicatorView>();
            for (int i = 0; i < 2; i++)
            {
                typeof(WindIndicatorView).GetMethod("EnsureRuntimeDecorations", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                view.ApplyPolishedLayout();
            }
            Assert.That(host.transform.childCount, Is.EqualTo(1));
            var icon = host.transform.GetChild(0).GetComponent<Image>();
            Assert.That(icon.enabled, Is.False);
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.transform.childCount, Is.Zero);
            Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(66, 66)));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void WindIconKeepsTransparentBackgroundAndSmallMobileImport()
    {
        string path = "Assets/Resources/" + WindIndicatorView.WindIconResourcePath + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
            Assert.That(texture.width, Is.EqualTo(texture.height));
            var pixels = texture.GetPixels32();
            // 원본의 1/255 수준 알파 반올림은 허용하되 배경판은 없어야 한다.
            Assert.That(pixels[0].a, Is.LessThanOrEqualTo(2));
            Assert.That(pixels[texture.width - 1].a, Is.LessThanOrEqualTo(2));
            Assert.That(pixels[pixels.Length - texture.width].a, Is.LessThanOrEqualTo(2));
            Assert.That(pixels[pixels.Length - 1].a, Is.LessThanOrEqualTo(2));
            int visible = 0, transparent = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.a > 128) visible++;
                if (pixel.a <= 2) transparent++;
            }
            Assert.That(visible, Is.GreaterThan(pixels.Length * 0.15f));
            Assert.That(transparent, Is.GreaterThan(pixels.Length * 0.35f));
        }
        finally { Object.DestroyImmediate(texture); }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void RenderWindIconAtHudScale(int scale)
    {
        var host = new GameObject("WindHudRender", typeof(RectTransform), typeof(Canvas));
        var cameraHost = new GameObject("WindHudRenderCamera", typeof(Camera));
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D capture = null;
        try
        {
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)host.transform).sizeDelta = new Vector2(280, 104);
            var view = BuildWindIndicator(host.transform);
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            foreach (Transform child in host.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 52;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            target = new RenderTexture(280 * scale, 104 * scale, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            const string folder = "output/quality-polish/wind-icon";
            Directory.CreateDirectory(folder);
            File.WriteAllBytes($"{folder}/wind-hud-{scale}x.png", capture.EncodeToPNG());
            int dark = 0;
            // 아이콘 사각형 안에 실제 먹색 픽셀이 렌더되어야 한다.
            for (int y = 19 * scale; y < 85 * scale; y++)
                for (int x = 50 * scale; x < 116 * scale; x++)
                    if (capture.GetPixel(x, y).grayscale < 0.45f) dark++;
            Assert.That(dark, Is.GreaterThan(100 * scale * scale));
        }
        finally
        {
            RenderTexture.active = previous;
            cameraHost.GetComponent<Camera>().targetTexture = null;
            if (capture != null) Object.DestroyImmediate(capture);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(host);
        }
    }

    static WindIndicatorView BuildWindIndicator(Transform parent) =>
        (WindIndicatorView)typeof(MukJumpSceneBuilder).GetMethod("CreateWindIndicator", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { parent, false });

    [TestCase(0, "0m")]
    [TestCase(9999, "9999m")]
    [TestCase(10000, "10km")]
    [TestCase(10500, "10.5km")]
    [TestCase(99999, "100km")]
    public void LargeHeightUsesCompactReadableUnit(int meters, string expected)
    {
        var method = typeof(GameplayHudView).GetMethod(
            "FormatHeight", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        Assert.AreEqual(expected, method.Invoke(null, new object[] { meters }));
    }

    [Test]
    public void EditModePreviewHidesGameplayCanvasLikeRuntimeLobby()
    {
        var host = new GameObject(
            "GameplayHudEditModePreview",
            typeof(Canvas),
            typeof(GameplayHudView));
        try
        {
            var canvas = host.GetComponent<Canvas>();
            var view = host.GetComponent<GameplayHudView>();
            typeof(GameplayHudView).GetField(
                    "canvas",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(view, canvas);
            canvas.enabled = true;

            typeof(GameplayHudView).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);

            Assert.IsFalse(canvas.enabled,
                "Play 전 Game View에서 인게임 HUD와 DEBUG가 보이면 런타임 로비와 달라집니다.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void DevelopmentDebugDrawerRemainsHidden()
    {
        var host = new GameObject(
            "GameplayHudNoDebug",
            typeof(Canvas),
            typeof(GameplayHudView));
        var controls = new GameObject(
            "ItemTestControls",
            typeof(RectTransform));
        var panel = new GameObject(
            "DebugPanel",
            typeof(RectTransform));
        try
        {
            controls.transform.SetParent(host.transform, false);
            panel.transform.SetParent(controls.transform, false);
            var view = host.GetComponent<GameplayHudView>();
            typeof(GameplayHudView).GetField(
                    "itemTestControls",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(view, controls.GetComponent<RectTransform>());
            typeof(GameplayHudView).GetField(
                    "debugPanel",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(view, panel.GetComponent<RectTransform>());

            controls.SetActive(true);
            panel.SetActive(true);
            typeof(GameplayHudView).GetMethod(
                    "SetDebugToolsAvailable",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, new object[] { true });

            Assert.IsTrue(controls.activeSelf, "요청된 개발용 테스트 창을 에디터에서 연다.");
            Assert.IsTrue(panel.activeSelf);
            typeof(GameplayHudView).GetMethod("SetDebugToolsAvailable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { false });
            Assert.IsFalse(controls.activeSelf, "배포에서는 개발 도구를 숨긴다.");
            Assert.IsFalse(panel.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
