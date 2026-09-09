using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

public class PauseMenuViewTests
{
    GameObject host;
    GameObject playerHost;
    GameObject cameraHost;
    GameObject musicHost;
    float originalTimeScale;
    float originalFixedDeltaTime;
    bool originalAudioPause;
    MemoryPendingGameOverSettlementStore pendingSettlementStore;

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        originalAudioPause = AudioListener.pause;
        PermanentGrowthProfile.UseStoreForTests(
            new MemoryPermanentGrowthStore());
        ScoreManager.UseStoreForTests(new MemoryScoreStore());
        pendingSettlementStore =
            new MemoryPendingGameOverSettlementStore();
        GameManager.UsePendingGameOverSettlementStoreForTests(
            pendingSettlementStore);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null)
            Object.DestroyImmediate(host);
        if (playerHost != null)
            Object.DestroyImmediate(playerHost);
        if (cameraHost != null)
            Object.DestroyImmediate(cameraHost);
        if (musicHost != null)
            Object.DestroyImmediate(musicHost);
        AudioListener.pause = originalAudioPause;
        if (!Mathf.Approximately(Time.timeScale, originalTimeScale))
            Time.timeScale = originalTimeScale;
        if (!Mathf.Approximately(Time.fixedDeltaTime, originalFixedDeltaTime))
            Time.fixedDeltaTime = originalFixedDeltaTime;
        PointerInput.ResetSuppressionForTests();
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        BackgroundMusicController.Instance?.SetFullScreenAdActive(false);
        MonetizationAds.ResetProvider();
        PermanentGrowthProfile.RestoreDefaultStoreForTests();
        ScoreManager.RestoreDefaultStoreForTests();
        GameManager.RestorePendingGameOverSettlementStoreForTests();
    }

    [Test]
    public void BuildsReadableBlockingControls()
    {
        host = new GameObject("PauseHost");
        var view = host.AddComponent<PauseMenuView>();

        Invoke(view, "BuildIfNeeded");

        var canvasRoot = host.transform.Find("PauseMenuCanvas");
        Assert.IsNotNull(canvasRoot);
        var canvas = canvasRoot.GetComponent<Canvas>();
        Assert.IsNotNull(canvas);
        Assert.AreEqual(1000, canvas.sortingOrder);

        var pauseButton = canvasRoot.Find("PauseButton")?.GetComponent<Button>();
        Assert.IsNotNull(pauseButton);
        var pauseButtonRect = pauseButton.transform as RectTransform;
        Assert.That(
            pauseButtonRect.sizeDelta.x,
            Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
        Assert.That(
            pauseButtonRect.sizeDelta.y,
            Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
        var pauseVisual = pauseButton.transform.Find("Visual") as RectTransform;
        Assert.IsNotNull(pauseVisual);
        Assert.That(pauseVisual.sizeDelta, Is.EqualTo(new Vector2(PauseMenuView.PauseVisualSize, PauseMenuView.PauseVisualSize)),
            "보이는 아이콘은 작게 유지하고 투명 터치 영역만 넓혀야 합니다.");
        Assert.IsTrue(pauseButton.GetComponent<Graphic>().raycastTarget);
        Assert.IsFalse(pauseButton.targetGraphic.raycastTarget);
        AssertPauseButtonIsIconOnly(pauseButton);

        var overlay = canvasRoot.Find("PauseOverlay");
        Assert.IsNotNull(overlay);
        var overlayGroup = overlay.GetComponent<CanvasGroup>();
        Assert.IsNotNull(overlayGroup);
        Assert.IsFalse(overlayGroup.blocksRaycasts);
        var pauseDim = overlay.Find("InkDim")?.GetComponent<Image>();
        Assert.IsNotNull(pauseDim);
        Assert.IsTrue(pauseDim.raycastTarget);
        Assert.That(pauseDim.color.a,
            Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));

        var panel = overlay.Find("SafeAreaRoot/PauseScroll") as RectTransform;
        Assert.IsNotNull(panel);
        var title = panel.Find("Title")?.GetComponent<Text>();
        var resume = panel.Find("ResumeButton")?.GetComponent<Button>();
        var lobby = panel.Find("LobbyButton")?.GetComponent<Button>();
        var resumeRect = resume?.transform as RectTransform;
        var lobbyRect = lobby?.transform as RectTransform;
        Assert.IsNotNull(title);
        Assert.IsNotNull(resume);
        Assert.IsNotNull(lobby);
        Assert.GreaterOrEqual(title.fontSize, 54);
        Assert.That(title.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(
            title.rectTransform.anchoredPosition.x,
            Is.Zero.Within(0.001f),
            "일시정지 제목은 두루마리의 수평 중앙에 있어야 합니다.");
        Assert.GreaterOrEqual(
            resume.transform.Find("Label").GetComponent<Text>().fontSize, 38);
        Assert.GreaterOrEqual(
            lobby.transform.Find("Label").GetComponent<Text>().fontSize, 32);
        Assert.IsTrue(resume.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(resume.targetGraphic.raycastTarget);
        Assert.IsTrue(lobby.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(lobby.targetGraphic.raycastTarget);
        Assert.IsTrue(
            InkUiStyle.UsesActionButtonSprite(
                resume.targetGraphic as Image));
        Assert.IsTrue(
            InkUiStyle.UsesActionButtonSprite(
                lobby.targetGraphic as Image));
        Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.Greater(title.rectTransform.anchoredPosition.y,
            resumeRect.anchoredPosition.y);
        Assert.Greater(resumeRect.anchoredPosition.y, lobbyRect.anchoredPosition.y);
        Assert.That(resumeRect.sizeDelta, Is.EqualTo(lobbyRect.sizeDelta));
        Assert.That(
            resume.GetComponent<CanvasGroup>().alpha,
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(
            lobby.GetComponent<CanvasGroup>().alpha,
            Is.EqualTo(1f).Within(0.001f),
            "보조 행동도 글자 자체는 선명하게 유지해야 합니다.");
        Assert.That(
            lobby.targetGraphic.color.a,
            Is.EqualTo(1f).Within(0.001f),
            "주·보조 역할은 면 투명도가 아니라 의미 테두리로 구분해야 합니다.");
        Assert.That(
            resume.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Primary));
        Assert.That(
            lobby.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Secondary));
        Assert.GreaterOrEqual(resumeRect.sizeDelta.y, 96f);
        Assert.IsNotNull(panel.GetComponent<HanjiScrollFrame>());
        Assert.IsNotNull(panel.Find("HanjiScrollArt/Paper").GetComponent<HanjiScrollPaperGraphic>());
        Assert.IsNull(panel.Find("ScrollBody/PaperCore"));
        Assert.IsNotNull(panel.Find("HanjiScrollArt/TopRoll"));
        Assert.IsNotNull(panel.Find("HanjiScrollArt/BottomRoll"));
        Assert.IsNull(panel.Find("PauseSeal"));
        Assert.IsNull(panel.Find("Subtitle"));
        Assert.IsNull(panel.Find("SessionHint"));
    }

    [Test]
    public void PauseButtonOccupiesFormerRecordSlotWithoutShrinkingItsHitArea()
    {
        host = new GameObject("PauseButtonSpacingHost");
        var view = host.AddComponent<PauseMenuView>();

        Invoke(view, "BuildIfNeeded");

        RectTransform pauseButtonRect = host.transform
            .Find("PauseMenuCanvas/PauseButton") as RectTransform;
        Assert.That(pauseButtonRect, Is.Not.Null);
        Assert.That(pauseButtonRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
        Assert.That(pauseButtonRect.anchoredPosition, Is.EqualTo(GameplayHudView.CalculatePauseTouchRect(
            MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height).center));
        Assert.That(pauseButtonRect.sizeDelta,
            Is.EqualTo(new Vector2(InkUiStyle.MinimumTapHeight, InkUiStyle.MinimumTapHeight)));
        Rect hud = GameplayHudView.CalculateTopHudRect(MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);
        var face = (RectTransform)pauseButtonRect.Find("Visual");
        Assert.That(pauseButtonRect.anchoredPosition.y + face.anchoredPosition.y, Is.EqualTo(hud.center.y).Within(.001f));
        Assert.That(pauseButtonRect.anchoredPosition.x, Is.GreaterThan(hud.center.x + hud.width * 0.4f));
    }

    [Test]
    public void RestoresIconOnlyPauseButtonWithoutRevivingLegacyPaperOrDuplicatingControls()
    {
        host = new GameObject("PauseIconRestoreHost");
        var view = host.AddComponent<PauseMenuView>();
        Invoke(view, "BuildIfNeeded");
        Transform canvasRoot = host.transform.Find("PauseMenuCanvas");
        var button = canvasRoot.Find("PauseButton").GetComponent<Button>();
        // 복원 검사용 구형 한지 프레임을 먼저 만든 뒤 추가 중복 생성을 검사한다.
        InkUiStyle.ConfigureHanjiSurface(button.transform.Find("Visual").GetComponent<Image>());
        int graphicCount = button.GetComponentsInChildren<Graphic>(true).Length;

        for (int restore = 0; restore < 3; restore++)
        {
            var face = button.transform.Find("Visual").GetComponent<Image>();
            face.enabled = true;
            InkUiStyle.ConfigureHanjiSurface(face);
            var paper = face.transform.Find("Paper").GetComponent<Image>();
            paper.enabled = true;
            paper.color = InkPalette.Paper;
            button.transition = Selectable.Transition.ColorTint;

            SetField(view, "rootCanvas", null);
            Invoke(view, "BuildIfNeeded");

            Assert.That(host.transform.Find("PauseMenuCanvas"), Is.SameAs(canvasRoot));
            Assert.That(GetField<Button>(view, "pauseButton"), Is.SameAs(button));
            Assert.That(button.GetComponentsInChildren<Graphic>(true).Length, Is.EqualTo(graphicCount));
            AssertPauseButtonIsIconOnly(button);
        }

        var feedback = button.GetComponent<InkUiPressFeedback>();
        feedback.OnPointerDown(null);
        Assert.That(GetField<float>(feedback, "targetScale"), Is.EqualTo(.97f));
        feedback.OnPointerUp(null);
        Assert.That(GetField<float>(feedback, "targetScale"), Is.EqualTo(1f));
    }

    static void AssertPauseButtonIsIconOnly(Button button)
    {
        var hitImage = button.GetComponent<Image>();
        Assert.That(hitImage.enabled, Is.True);
        Assert.That(hitImage.color.a, Is.Zero);
        Assert.That(hitImage.raycastTarget, Is.True);
        Assert.That(hitImage.rectTransform.sizeDelta,
            Is.EqualTo(new Vector2(InkUiStyle.MinimumTapHeight, InkUiStyle.MinimumTapHeight)));
        Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
        Assert.That(button.GetComponents<InkUiPressFeedback>().Length, Is.EqualTo(1));
        var face = button.transform.Find("Visual").GetComponent<Image>();
        Assert.That(face.sprite, Is.Null);
        Assert.That(face.enabled, Is.False);
        Assert.That(face.raycastTarget, Is.False);
        var paper = face.transform.Find("Paper").GetComponent<Image>();
        Assert.That(paper.enabled, Is.False);
        Assert.That(paper.raycastTarget, Is.False);
        int visibleGraphics = 0;
        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled || graphic.color.a <= 0f) continue;
            visibleGraphics++;
            Assert.That(graphic.name, Is.EqualTo("BrushPause"));
            Assert.That(graphic.color, Is.EqualTo(InkPalette.Ink));
            Assert.That(graphic.rectTransform.sizeDelta, Is.EqualTo(new Vector2(48f, 48f)));
            Assert.That(graphic.raycastTarget, Is.False);
        }
        Assert.That(visibleGraphics, Is.EqualTo(1), "두 먹획을 그리는 아이콘 하나만 보여야 합니다.");
    }

    [Test]
    public void BuildsPauseIconWithNoRenderedPanel()
    {
        host = new GameObject("PauseIconRenderHost");
        var view = host.AddComponent<PauseMenuView>();
        Invoke(view, "BuildIfNeeded");
        var canvas = host.GetComponentInChildren<Canvas>();
        canvas.GetComponent<CanvasScaler>().enabled = false;
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRect = (RectTransform)canvas.transform;
        canvasRect.position = Vector3.zero;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = Vector2.one * 120f;
        var buttonRect = (RectTransform)canvas.transform.Find("PauseButton");
        buttonRect.anchorMin = buttonRect.anchorMax = Vector2.one * .5f;
        buttonRect.anchoredPosition = Vector2.zero;
        ((RectTransform)buttonRect.Find("Visual")).anchoredPosition = Vector2.zero;
        buttonRect.Find("Visual").localScale = Vector3.one;
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 31;

        cameraHost = new GameObject("PauseIconRenderCamera", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 60f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = InkPalette.Paper;
        canvas.worldCamera = camera;
        var target = new RenderTexture(120, 120, 24, RenderTextureFormat.ARGB32);
        var capture = new Texture2D(120, 120, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0f, 0f, 120f, 120f), 0, 0);
            capture.Apply();
            System.IO.Directory.CreateDirectory("output/quality-polish/pause-icon");
            System.IO.File.WriteAllBytes("output/quality-polish/pause-icon/icon-only.png", capture.EncodeToPNG());

            Color background = capture.GetPixel(0, 0);
            int inkPixels = 0;
            for (int y = 0; y < 120; y++)
                for (int x = 0; x < 120; x++)
                {
                    Color pixel = capture.GetPixel(x, y);
                    if (x >= 36 && x <= 84 && y >= 36 && y <= 84)
                    {
                        if (pixel.grayscale < .3f) inkPixels++;
                        continue;
                    }
                    Assert.That(Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) +
                        Mathf.Abs(pixel.b - background.b), Is.LessThan(.02f),
                        $"아이콘 바깥 ({x}, {y})에 버튼 면이나 테두리가 남았습니다.");
                }
            Assert.That(inkPixels, Is.InRange(200, 650), "48px 영역 안에 두 갈필 먹획이 실제로 렌더되어야 합니다.");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(capture);
        }
    }

    [Test]
    public void BuildsSimpleReadableGameOverLayoutOnlyOnce()
    {
        host = new GameObject("GameOverHost");
        var view = host.AddComponent<GameOverPopupView>();

        Invoke(view, "BuildIfNeeded");
        Invoke(view, "BuildIfNeeded");

        Assert.AreEqual(1, CountDirectChildren(host.transform, "GameOverPopupCanvas"));
        var canvasRoot = host.transform.Find("GameOverPopupCanvas");
        Assert.IsNotNull(canvasRoot);
        var canvas = canvasRoot.GetComponent<Canvas>();
        Assert.IsNotNull(canvas);
        Assert.AreEqual(5000, canvas.sortingOrder);
        Assert.IsTrue(canvas.pixelPerfect);

        var gameOverDim = canvasRoot.Find("InkWash")?.GetComponent<Image>();
        Assert.IsNotNull(gameOverDim);
        Assert.IsTrue(gameOverDim.raycastTarget);
        Assert.That(gameOverDim.color.a,
            Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));

        var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
        Assert.IsNotNull(rootGroup);
        Assert.That(rootGroup.alpha, Is.Zero);
        Assert.IsFalse(rootGroup.blocksRaycasts);

        var panel = canvasRoot.Find("SafeAreaRoot/ScrollResultPopup")
            as RectTransform;
        Assert.IsNotNull(panel);
        Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.LessOrEqual(panel.sizeDelta.x, 840f);
        Assert.That(panel.sizeDelta.y, Is.EqualTo(1200f));

        var scrollBody = panel.Find("ScrollBody");
        var topRoll = panel.Find("TopRoll");
        var bottomRoll = panel.Find("BottomRoll");
        Assert.IsNotNull(scrollBody);
        Assert.IsNotNull(topRoll);
        Assert.IsNotNull(bottomRoll);
        var resultPaperCore = scrollBody.Find("ScrollPaper")?.GetComponent<HanjiScrollPaperGraphic>();
        Assert.IsNotNull(resultPaperCore);
        Assert.IsNotNull(resultPaperCore.mainTexture);
        Assert.IsNull(scrollBody.Find("PaperCore"));
        Assert.GreaterOrEqual(resultPaperCore.rectTransform.sizeDelta.x, 600f);
        Assert.Less(resultPaperCore.rectTransform.sizeDelta.x,
            (scrollBody as RectTransform).sizeDelta.x);
        Assert.IsNotNull(topRoll.Find("PaperRoll")?.GetComponent<Image>().sprite);
        Assert.IsNotNull(bottomRoll.Find("PaperRoll")?.GetComponent<Image>().sprite);

        var content = panel.Find("ResultContent");
        Assert.IsNotNull(content);
        var title = content.Find("Title")?.GetComponent<Text>();
        var currentValue = content.Find("CurrentResult/Value")?.GetComponent<Text>();
        var bestValue = content.Find("BestResult/Value")?.GetComponent<Text>();
        var bestCaption = content.Find("BestResult/Caption")?.GetComponent<Text>();
        var saveNotice = content.Find("SaveNotice")?.GetComponent<Text>();
        var currentCaption = content.Find("CurrentResult/Caption")?.GetComponent<Text>();
        var hint = content.Find("RetryBrush/TouchHint")?.GetComponent<Text>();
        Assert.IsNotNull(title);
        Assert.IsNotNull(currentValue);
        Assert.IsNotNull(bestValue);
        Assert.IsNotNull(bestCaption);
        Assert.IsNull(content.Find("PermanentGrowthReward"));
        Assert.IsNotNull(saveNotice);
        Assert.IsFalse(saveNotice.gameObject.activeSelf);
        Assert.IsNotNull(currentCaption);
        Assert.IsNotNull(hint);
        Assert.GreaterOrEqual(title.fontSize, 54);
        Assert.That(title.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(
            currentCaption.alignment,
            Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(
            currentValue.alignment,
            Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(
            bestCaption.alignment,
            Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(
            bestValue.alignment,
            Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.Greater(currentValue.fontSize, bestValue.fontSize * 2);
        Assert.Greater(bestValue.fontSize, currentCaption.fontSize);
        Assert.GreaterOrEqual(hint.fontSize, 32);
        Assert.GreaterOrEqual(saveNotice.fontSize, 48);
        Assert.IsFalse(saveNotice.resizeTextForBestFit);
        var currentResult = content.Find("CurrentResult") as RectTransform;
        var bestResult = content.Find("BestResult") as RectTransform;
        var retry = content.Find("RetryBrush") as RectTransform;
        var revive = content.Find("ReviveBrush") as RectTransform;
        Assert.That(revive, Is.Not.Null);
        view.SetReviveOffer(true);
        Assert.That(revive.gameObject.activeSelf, Is.True);
        Assert.Greater(title.rectTransform.anchoredPosition.y,
            currentResult.anchoredPosition.y);
        Assert.Greater(currentResult.anchoredPosition.y,
            bestResult.anchoredPosition.y);
        Assert.Greater(bestResult.anchoredPosition.y, revive.anchoredPosition.y);
        Assert.That(
            LeftEdge(title.rectTransform),
            Is.EqualTo(LeftEdge(currentCaption.rectTransform)).Within(0.001f));
        Assert.That(
            LeftEdge(title.rectTransform),
            Is.EqualTo(LeftEdge(currentValue.rectTransform)).Within(0.001f));
        Assert.That(
            LeftEdge(title.rectTransform),
            Is.EqualTo(LeftEdge(bestCaption.rectTransform)).Within(0.001f));
        Assert.That(
            RightEdge(title.rectTransform),
            Is.EqualTo(RightEdge(bestValue.rectTransform)).Within(0.001f));
        float bestBottom = bestResult.anchoredPosition.y -
                           bestResult.sizeDelta.y * 0.5f;
        float retryTop = retry.anchoredPosition.y +
                         retry.sizeDelta.y * 0.5f;
        Assert.GreaterOrEqual(
            bestBottom,
            Mathf.Max(
                retryTop,
                revive.anchoredPosition.y + revive.sizeDelta.y * 0.5f) + 24f,
            "최고 기록과 게임오버 선택 버튼 사이에 충분한 여백이 필요합니다.");
        Assert.Greater(bestResult.anchoredPosition.y,
            retry.anchoredPosition.y);
        Assert.GreaterOrEqual(retry.sizeDelta.x, 260f);
        Assert.That(retry.sizeDelta.y,
            Is.EqualTo(132f));
        Assert.GreaterOrEqual(revive.sizeDelta.x, 260f);
        Assert.That(revive.sizeDelta.y,
            Is.EqualTo(164f));
        Invoke(view, "ApplyRevealPose", 1f, false);
        RectTransform bottomPaper =
            panel.Find("BottomRoll/PaperRoll") as RectTransform;
        Assert.That(bottomPaper, Is.Not.Null);
        Bounds retryBounds = RectTransformUtility
            .CalculateRelativeRectTransformBounds(panel, retry);
        Bounds bottomPaperBounds = RectTransformUtility
            .CalculateRelativeRectTransformBounds(panel, bottomPaper);
        Bounds paperCoreBounds = RectTransformUtility
            .CalculateRelativeRectTransformBounds(
                panel,
                resultPaperCore.rectTransform);
        Bounds reviveBounds = RectTransformUtility
            .CalculateRelativeRectTransformBounds(panel, revive);
        Assert.That(
            retryBounds.min.y - bottomPaperBounds.max.y,
            Is.GreaterThanOrEqualTo(24f),
            "게임오버 버튼이 하단 두루마리 장식과 겹치면 안 됩니다.");
        Assert.That(reviveBounds.min.y - retryBounds.max.y,
            // 월드 좌표를 패널 좌표로 되돌리면 12px이 11.99997px로 반올림될 수 있다.
            Is.GreaterThanOrEqualTo(12f - 0.001f),
            "부활과 메인 버튼이 붙어 하나의 버튼처럼 보여서는 안 됩니다.");
        Assert.That(reviveBounds.min.x - paperCoreBounds.min.x,
            Is.GreaterThanOrEqualTo(24f));
        Assert.That(paperCoreBounds.max.x - reviveBounds.max.x,
            Is.GreaterThanOrEqualTo(24f));
        Assert.That(retryBounds.min.x - paperCoreBounds.min.x,
            Is.GreaterThanOrEqualTo(24f));
        Assert.That(paperCoreBounds.max.x - retryBounds.max.x,
            Is.GreaterThanOrEqualTo(24f));
        Assert.That(retryBounds.min.y - paperCoreBounds.min.y,
            Is.GreaterThanOrEqualTo(24f));
        Assert.That(retry.GetComponent<Button>(), Is.Not.Null);
        Assert.That(revive.GetComponent<Button>(), Is.Not.Null);
        Assert.That(retry.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(revive.GetComponent<Image>().raycastTarget, Is.True);
        Assert.IsTrue(
            InkUiStyle.UsesActionButtonSprite(
                retry.GetComponent<Image>()));
        Assert.IsTrue(
            InkUiStyle.UsesActionButtonSprite(
                revive.GetComponent<Image>()));
        Assert.IsNull(content.Find("CurrentResult")?.GetComponent<Image>());
        Assert.IsNull(content.Find("BestResult")?.GetComponent<Image>());
        Assert.IsNull(content.Find("ResultSeal"));
        Assert.IsNull(content.Find("Subtitle"));
        Assert.IsNull(content.Find("Footer"));
    }

    [Test]
    public void GameOverReviveChoiceUsesExplicitRewardAndLobbyActions()
    {
        host = new GameObject("GameOverRewardChoiceHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");

        int reviveRequests = 0;
        int lobbyRequests = 0;
        view.ConfigureActions(
            () => reviveRequests++,
            () => lobbyRequests++);
        view.SetReviveOffer(true);

        Transform content = host.transform.Find(
            "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent");
        Button revive = content.Find("ReviveBrush")?.GetComponent<Button>();
        Button lobby = content.Find("RetryBrush")?.GetComponent<Button>();
        Text reviveLabel = content.Find("ReviveBrush/Label")?.GetComponent<Text>();
        Text lobbyLabel = content.Find("RetryBrush/TouchHint")?.GetComponent<Text>();

        Assert.That(revive, Is.Not.Null);
        Assert.That(lobby, Is.Not.Null);
        Assert.That(reviveLabel.text, Is.EqualTo("광고 시청하고 부활하기"));
        Assert.That(lobbyLabel.text, Is.EqualTo("메인으로"));
        Assert.That(revive.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Primary));
        Assert.That(lobby.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Secondary));
        revive.onClick.Invoke();
        lobby.onClick.Invoke();
        Assert.That(reviveRequests, Is.EqualTo(1));
        Assert.That(lobbyRequests, Is.EqualTo(1));

        view.SetReviveRequestInFlight(true);
        Assert.That(revive.interactable, Is.False);
        Assert.That(lobby.interactable, Is.False);
        Assert.That(reviveLabel.text, Is.EqualTo("광고 여는 중..."));

        view.SetReviveRequestInFlight(false);
        Assert.That(reviveLabel.text, Is.EqualTo("광고 시청하고 부활하기"));
        view.SetReviveOffer(false, waitingForAvailability: true);
        Assert.That(revive.gameObject.activeSelf, Is.True);
        Assert.That(revive.interactable, Is.False);
        Assert.That(reviveLabel.text, Is.EqualTo("광고 준비 중..."));

        view.SetReviveOffer(false);
        Assert.That(revive.gameObject.activeSelf, Is.False);
        Assert.That(
            (lobby.transform as RectTransform).sizeDelta.x,
            Is.EqualTo(640f));
        Assert.That((lobby.transform as RectTransform).sizeDelta.y,
            Is.EqualTo(132f));
        Assert.That(lobby.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Primary));

        view.SetReviveOffer(true);
        view.ShowPendingAbandonConfirmation();
        Assert.That(revive.gameObject.activeSelf, Is.False,
            "기록 저장 포기 확인 중에는 부활 CTA를 함께 보여 주면 안 됩니다.");
        Assert.That(lobbyLabel.text,
            Is.EqualTo("기록·먹빛 포기\n한 번 더 눌러 확인"));
        Assert.That((lobby.transform as RectTransform).sizeDelta.x,
            Is.EqualTo(640f));
        Assert.That((lobby.transform as RectTransform).sizeDelta.y,
            Is.EqualTo(188f));
        Assert.That(lobby.GetComponent<InkActionButtonVisual>().Role,
            Is.EqualTo(ActionButtonRole.Primary));
        Assert.That(lobbyLabel.preferredHeight,
            Is.LessThanOrEqualTo(lobbyLabel.rectTransform.rect.height + 0.01f));
    }

    [Test]
    public void ImmediateSecondGameOverRestoresLobbyActionAfterSuccessfulRevive()
    {
        host = new GameObject("GameOverSecondDeathHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");

        int lobbyRequests = 0;
        view.ConfigureActions(null, () => lobbyRequests++);
        view.SetReviveOffer(true);

        Transform content = host.transform.Find(
            "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent");
        Button revive = content.Find("ReviveBrush")?.GetComponent<Button>();
        Button lobby = content.Find("RetryBrush")?.GetComponent<Button>();

        view.SetReviveRequestInFlight(true);
        Assert.That(lobby.interactable, Is.False);
        view.Hide();

        view.Show(74, 74, false);

        Assert.That(revive.gameObject.activeSelf, Is.False);
        Assert.That(lobby.gameObject.activeSelf, Is.True);
        Assert.That(lobby.interactable, Is.True,
            "광고 부활 직후 다시 전멸해도 메인 버튼 입력 잠금은 남으면 안 됩니다.");
        lobby.onClick.Invoke();
        Assert.That(lobbyRequests, Is.EqualTo(1));
    }

    [Test]
    public void RewardedReviveShowExceptionImmediatelyUnlocksGameOverInput()
    {
        var ads = new ScriptedAdProvider
        {
            ThrowOnShow = true,
        };
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("ThrowingReviveManager");

        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 부활 광고 열기에 실패했습니다: show failed");
        Invoke(manager, "HandleGameOverReviveRequested");

        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.False);
        Assert.That(GetField<int>(manager, "activeReviveRequestGeneration"),
            Is.Zero);
        Assert.That(AudioListener.pause, Is.False,
            "네이티브 Show 예외 뒤에도 게임오버 버튼과 오디오가 즉시 복구되어야 합니다.");
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.Not.True,
            "Show 예외 뒤에는 BGM의 광고 정지 사유도 즉시 해제해야 합니다.");
    }

    [Test]
    public void CompletedRunNotificationExceptionCannotEscapeSettlementFlow()
    {
        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 테스트 알림을 다음 기회로 미룹니다: publish failed");

        object result = InvokeStatic(
            typeof(GameManager),
            "InvokeReleaseNotificationSafely",
            "테스트",
            (System.Action)(() =>
                throw new System.InvalidOperationException("publish failed")));

        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void RewardedReviveReadinessExceptionCannotRelockCompletedRequest()
    {
        var ads = new ScriptedAdProvider();
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("ReadinessFailureReviveManager");

        Invoke(manager, "HandleGameOverReviveRequested");
        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.True);
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.True,
            "전체 화면 광고 대기 중에는 listener 예외 BGM도 별도로 멈춰야 합니다.");
        ads.ThrowOnReady = true;
        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 광고 준비 상태 확인에 실패했습니다: ready failed");
        ads.Complete(false);

        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.False);
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.Not.True);
    }

    [Test]
    public void RewardedReviveReadinessFailureIsPolledWithCooldown()
    {
        var ads = new ScriptedAdProvider
        {
            ThrowOnReady = true,
        };
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("ReadinessCooldownReviveManager");
        SetField(manager, "nextReviveAvailabilityPollTime", 0f);

        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 광고 준비 상태 확인에 실패했습니다: ready failed");
        Invoke(manager, "Update");
        int firstPollCount = ads.ReadyCalls;

        Invoke(manager, "Update");

        Assert.That(firstPollCount, Is.EqualTo(1));
        Assert.That(ads.ReadyCalls, Is.EqualTo(firstPollCount),
            "SDK 준비 확인 예외를 매 프레임 재호출해 로그와 프레임을 소모하면 안 됩니다.");
    }

    [Test]
    public void ApplicationLifecycleRequiresFocusForegroundAndPlatformVisibility()
    {
        host = new GameObject("ApplicationLifecycleStateTest");
        var lifecycle = host.AddComponent<MobileApplicationLifecycle>();

        MobileApplicationLifecycle.SetPlatformVisibility(false);
        Invoke(lifecycle, "OnEnable");
        Assert.That(MobileApplicationLifecycle.IsApplicationActive, Is.False,
            "숨겨진 호스트에서 뒤늦게 OnEnable되어도 활성 상태로 덮으면 안 됩니다.");

        SetField(lifecycle, "applicationPaused", true);
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        Assert.That(MobileApplicationLifecycle.IsApplicationActive, Is.False,
            "호스트가 보여도 Unity pause 상태이면 재개하면 안 됩니다.");

        SetField(lifecycle, "applicationPaused", false);
        SetField(lifecycle, "applicationFocused", false);
        Invoke(lifecycle, "RefreshApplicationState");
        Assert.That(MobileApplicationLifecycle.IsApplicationActive, Is.False,
            "플랫폼 가시성만으로 포커스 손실을 덮으면 안 됩니다.");

        SetField(lifecycle, "applicationFocused", true);
        Invoke(lifecycle, "RefreshApplicationState");
        Assert.That(MobileApplicationLifecycle.IsApplicationActive, Is.True);
    }

    [TestCase(false, true, true, true)]
    [TestCase(true, true, true, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, false, false)]
    public void ApplicationLifecycleComposesIndependentSignals(
        bool paused,
        bool focused,
        bool visible,
        bool expected)
    {
        Assert.That(MobileApplicationLifecycle.ResolveApplicationActive(
            paused,
            focused,
            visible), Is.EqualTo(expected));
    }

    [Test]
    public void RewardedReviveIgnoresLateDuplicateCallbackAndKeepsBackgroundReward()
    {
        var ads = new ScriptedAdProvider();
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("DuplicateReviveManager");

        MobileApplicationLifecycle.SetPlatformVisibility(false);
        Invoke(manager, "HandleGameOverReviveRequested");
        ads.Complete(true);
        ads.Complete(false);

        Assert.That(GetField<bool>(manager, "reviveCompletionPendingForeground"),
            Is.True);
        Assert.That(GetField<bool>(manager, "pendingReviveRewardEarned"),
            Is.True,
            "백그라운드의 뒤늦은 false가 이미 받은 보상을 덮으면 안 됩니다.");

        // 요청을 실패로 한 번 소비한 뒤의 늦은 true도 다음 사망에 영향을 주면 안 된다.
        int generation = GetField<int>(manager, "activeReviveRequestGeneration");
        Invoke(
            manager,
            "FinishGameOverReviveAdCompletedForRequest",
            generation,
            false);
        ads.Complete(true);
        Assert.That(GetField<bool>(manager, "reviveUsedThisRun"), Is.False);
        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.False);
    }

    [Test]
    public void RewardedReviveKeepsBackgroundRewardWhenDuplicateFailureArrivesAfterForeground()
    {
        var ads = new ScriptedAdProvider();
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("ForegroundBoundaryReviveManager");

        cameraHost = new GameObject("MainCamera");
        cameraHost.tag = "MainCamera";
        var reviveCamera = cameraHost.AddComponent<Camera>();
        reviveCamera.orthographic = true;
        reviveCamera.orthographicSize = 5f;

        playerHost = new GameObject("RewardRevivePlayer");
        playerHost.AddComponent<Rigidbody2D>();
        playerHost.AddComponent<CircleCollider2D>();
        var player = playerHost.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        SetProperty(player, "IsDead", true);
        SetProperty(player, "CurrentHealth", 0);
        manager.RegisterPlayer(player);

        MobileApplicationLifecycle.SetPlatformVisibility(false);
        Invoke(manager, "HandleGameOverReviveRequested");
        ads.Complete(true);
        Assert.That(GetField<bool>(manager, "pendingReviveRewardEarned"),
            Is.True);
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.True,
            "백그라운드 콜백만으로 BGM을 먼저 재개하면 안 됩니다.");

        // 전경 Update가 아직 실행되기 전 들어오는 중복 실패 콜백도
        // 백그라운드에서 이미 받은 보상을 취소하면 안 된다.
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        ads.Complete(false);

        Assert.That(player.IsDead, Is.False);
        Assert.That(player.HasShield, Is.True);
        Assert.That(GetField<bool>(manager, "reviveUsedThisRun"), Is.True);
        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.False);
        Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.Not.True);
    }

    [Test]
    public void RewardedReviveTimeoutUnlocksInputAndFullScreenAdAudio()
    {
        var ads = new ScriptedAdProvider();
        MonetizationAds.RegisterProvider(ads);
        var manager = CreateGameOverManager("TimedOutReviveManager");

        Invoke(manager, "HandleGameOverReviveRequested");
        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.True);
        SetField(manager, "reviveRequestDeadline", 0f);
        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 부활 광고 완료 응답이 없어 입력과 오디오를 복구합니다.");

        Invoke(manager, "Update");

        Assert.That(GetField<bool>(manager, "reviveRequestInFlight"), Is.False);
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(
            BackgroundMusicController.Instance?.IsFullScreenAdActive,
            Is.Not.True);
    }

    [Test]
    public void GameOverResultBindingFormatsHeightAndTogglesNewBestSeal()
    {
        host = new GameObject("GameOverHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");

        var content = host.transform.Find(
            "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent");
        Assert.IsNotNull(content);
        var currentValue = content.Find("CurrentResult/Value")?.GetComponent<Text>();
        var bestValue = content.Find("BestResult/Value")?.GetComponent<Text>();
        var newBestSeal = content.Find("NewBestSeal");

        Invoke(view, "BindResults", 12345, 23456, true);

        Assert.AreEqual("12.3 <size=72>km</size>", currentValue?.text);
        Assert.AreEqual("23.5 km", bestValue?.text);
        Assert.IsTrue(newBestSeal != null && newBestSeal.gameObject.activeSelf);

        Invoke(view, "BindResults", -10, 845, false);

        Assert.AreEqual("0 <size=72>m</size>", currentValue?.text);
        Assert.AreEqual("845 m", bestValue?.text);
        Assert.IsFalse(newBestSeal != null && newBestSeal.gameObject.activeSelf);
    }

    [Test]
    public void GameOverResultOmitsGrowthLedgerAndOnlyShowsActionableSaveNotice()
    {
        host = new GameObject("GameOverHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");

        Invoke(
            view,
            "BindResult",
            new GameOverResult(
                120,
                120,
                true,
                14,
                32,
                true,
                true,
                true,
                GameOverPersistenceState.Complete,
                275,
                250,
                300));
        Assert.That(view.SaveNoticeLabel, Is.Empty);
        Transform content = host.transform.Find(
            "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent");
        Assert.That(content.Find("PermanentGrowthReward"), Is.Null);
        Assert.That(content.Find("SaveNotice").gameObject.activeSelf, Is.False);

        Invoke(
            view,
            "BindResult",
            new GameOverResult(120, 120, false, 0, 32, false));
        Assert.That(view.SaveNoticeLabel, Is.Empty);
        Assert.That(content.Find("SaveNotice").gameObject.activeSelf, Is.False);

        Invoke(
            view,
            "BindResult",
            new GameOverResult(120, 120, false, 0, 32, true, false));
        Assert.That(
            view.SaveNoticeLabel,
            Is.EqualTo("성장 저장을 복구해 주세요"));
        Assert.That(view.TouchHintLabel, Is.EqualTo("로비에서 성장 복구"));
        Text rewardValue = content.Find("SaveNotice").GetComponent<Text>();
        Assert.That(rewardValue.gameObject.activeSelf, Is.True);
        Assert.That(rewardValue, Is.Not.Null);
        Assert.That(
            rewardValue.preferredWidth,
            Is.LessThanOrEqualTo(rewardValue.rectTransform.rect.width + 0.01f));
        Assert.That(
            rewardValue.preferredHeight,
            Is.LessThanOrEqualTo(rewardValue.rectTransform.rect.height + 0.01f));
    }

    [Test]
    public void RecoverySettlementFlowsFromManagerIntoGameOverWarning()
    {
        var recoveryStore = new MemoryPermanentGrowthStore
        {
            Json = "{broken json",
        };
        PermanentGrowthProfile.UseStoreForTests(recoveryStore);

        host = new GameObject("RecoverySettlementHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        var popup = host.AddComponent<GameOverPopupView>();
        Invoke(popup, "BuildIfNeeded");

        var result = (GameOverResult)Invoke(manager, "SettleGameOverResult");
        Assert.That(result.RewardsAllowed, Is.True);
        Assert.That(result.GrowthRewardSaved, Is.False);
        Invoke(popup, "BindResult", result);
        Assert.That(popup.SaveNoticeLabel,
            Is.EqualTo("성장 저장을 복구해 주세요"));
        Assert.That(result.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.GrowthRecoveryRequired));
    }

    [Test]
    public void GrowthSaveFailureStillPreservesBestButNotDistanceJourney()
    {
        var growthStore = new MemoryPermanentGrowthStore
        {
            ThrowOnPrimarySave = true,
        };
        PermanentGrowthProfile.UseStoreForTests(growthStore);
        var scoreStore = new MemoryScoreStore();
        ScoreManager.UseStoreForTests(scoreStore);

        host = new GameObject("AtomicGameOverHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 100);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "7123456789abcdef0123456789abcdef");
        Assert.That(
            (bool)Invoke(manager, "PersistPendingGameOverSettlement"),
            Is.True);

        var result = (GameOverResult)Invoke(manager, "SettleGameOverResult");

        Assert.That(result.GrowthRewardSaved, Is.False);
        Assert.That(score.Best, Is.EqualTo(100));
        Assert.That(scoreStore.Best, Is.EqualTo(100),
            "성장 복구가 필요해도 독립된 단조 최고 기록은 잃으면 안 됩니다.");
        Assert.That(result.RecordSaved, Is.True);
        Assert.That(result.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.GrowthRecoveryRequired));
        Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.Zero);
    }

    [Test]
    public void FirstDeathCommitsBestBeforeRewardedReviveChoice()
    {
        var scoreStore = new MemoryScoreStore { Best = 25 };
        ScoreManager.UseStoreForTests(scoreStore);

        host = new GameObject("PreReviveRecordCommitHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        score.ResetOrigin(0f);
        SetProperty(score, "Height", 74);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        bool committed = (bool)Invoke(
            manager,
            "CommitCurrentBestBeforeGameOver");

        Assert.That(committed, Is.True);
        Assert.That(scoreStore.Best, Is.EqualTo(74));
        Assert.That(score.Best, Is.EqualTo(74));
        Assert.That(score.RunBestToBeat, Is.EqualTo(25));
    }

    [Test]
    public void AmbiguousPreReviveWriteStaysPendingUntilVerifiedRewrite()
    {
        var scoreStore = new MemoryScoreStore
        {
            Best = 25,
            ThrowOnSave = true,
            ApplyBeforeThrow = true,
        };
        ScoreManager.UseStoreForTests(scoreStore);

        host = new GameObject("AmbiguousPreReviveCommitHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        score.ResetOrigin(0f);
        SetProperty(score, "Height", 74);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        Assert.That(
            (bool)Invoke(manager, "CommitCurrentBestBeforeGameOver"),
            Is.False);
        GameOverResult preview = (GameOverResult)Invoke(
            manager,
            "CreateUnsettledGameOverPreview");
        Assert.That(preview.RecordSaved, Is.False);
        Assert.That(preview.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.ScoreBaselinePending));
        SetField(manager, "latestGameOverResult", preview);

        Invoke(manager, "RetryUnsettledBestCommit");
        preview = GetField<GameOverResult>(manager, "latestGameOverResult");
        Assert.That(score.HasPendingBestSaveRetry, Is.True);
        Assert.That(preview.RecordSaved, Is.False,
            "flush 실패 뒤 메모리 readback만으로 완료 처리하면 안 됩니다.");
        Assert.That(preview.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.RecordWritePending));

        scoreStore.ThrowOnSave = false;
        Invoke(manager, "RetryUnsettledBestCommit");
        preview = GetField<GameOverResult>(manager, "latestGameOverResult");
        Assert.That(score.HasPendingBestSaveRetry, Is.False);
        Assert.That(preview.RecordSaved, Is.True);
        Assert.That(preview.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.Complete));
        Assert.That(scoreStore.Best, Is.EqualTo(74));
        Assert.That(score.RunBestToBeat, Is.EqualTo(25));
    }

    [Test]
    public void UnsettledGrowthGaugeCarriesPreviewWithoutAwardingAndThenCommitsOnce()
    {
        PermanentGrowthProfile.SettleRun("before-result", 380, 0, true);
        host = new GameObject("GrowthGaugePreviewHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 245);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        var preview = (GameOverResult)Invoke(manager, "CreateUnsettledGameOverPreview");
        Assert.That(preview.IsGrowthPreview, Is.True);
        Assert.That(preview.GrowthDistanceBeforeMeters, Is.EqualTo(380));
        Assert.That(preview.CumulativeGrowthDistanceMeters, Is.EqualTo(625));
        Assert.That(preview.PreviewGrowthCurrency, Is.EqualTo(1));
        Assert.That(preview.EarnedGrowthCurrency, Is.Zero);
        Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(4));
        Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(380));
        var settled = (GameOverResult)Invoke(manager, "SettleGameOverResult");
        Assert.That(settled.IsGrowthPreview, Is.False);
        Assert.That(settled.GrowthDistanceBeforeMeters, Is.EqualTo(380));
        Assert.That(settled.CumulativeGrowthDistanceMeters, Is.EqualTo(625));
        Assert.That(settled.EarnedGrowthCurrency, Is.EqualTo(1));
        Invoke(manager, "SettleGameOverResult");
        Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(5));
        Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(625));
    }

    [Test]
    public void ScoreSaveFailurePreservesGrowthDistanceAndReturnsRecordWritePending()
    {
        var growthStore = new MemoryPermanentGrowthStore();
        PermanentGrowthProfile.UseStoreForTests(growthStore);
        var scoreStore = new MemoryScoreStore
        {
            ThrowOnSave = true,
        };
        ScoreManager.UseStoreForTests(scoreStore);

        host = new GameObject("ScoreFailureGameOverHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 100);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "b123456789abcdef0123456789abcdef");
        Assert.That(
            (bool)Invoke(manager, "PersistPendingGameOverSettlement"),
            Is.True);

        var result = (GameOverResult)Invoke(manager, "SettleGameOverResult");

        Assert.That(result.GrowthRewardSaved, Is.True);
        Assert.That(result.EarnedGrowthCurrency, Is.EqualTo(1));
        Assert.That(result.CumulativeGrowthDistanceMeters, Is.EqualTo(100));
        Assert.That(result.RecordSaved, Is.False);
        Assert.That(result.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.RecordWritePending));
        Assert.That(score.Best, Is.Zero,
            "최고 기록 저장 실패는 메모리 Best도 이전 값으로 되돌려야 합니다.");
        Assert.That(pendingSettlementStore.Json, Is.Not.Empty,
            "최고 기록이 내구 저장되기 전에는 정산 복구본을 지우면 안 됩니다.");

        scoreStore.ThrowOnSave = false;
        PermanentGrowthProfile.ResetCacheForTests();
        PermanentGrowthSettlement next = PermanentGrowthProfile.SettleRun(
            "after-score-failure",
            0,
            0,
            0,
            0f,
            true);
        Assert.That(next.Accepted, Is.True);
        Assert.That(next.Earned, Is.Zero,
            "기록 저장 재시도와 무관한 0m 판이 누적 보상을 만들면 안 됩니다.");
        Assert.That(next.CumulativeDistanceMeters, Is.EqualTo(100));
    }

    [Test]
    public void GameOverPersistenceMessagesDistinguishRetryAndRecovery()
    {
        host = new GameObject("GameOverPersistenceMessageHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");

        Invoke(
            view,
            "BindResult",
            new GameOverResult(
                120,
                0,
                false,
                0,
                0,
                true,
                false,
                false,
                GameOverPersistenceState.ScoreBaselinePending));
        Assert.That(
            view.SaveNoticeLabel,
            Is.EqualTo("기록을 확인하지 못했어요"));
        Assert.That(view.TouchHintLabel, Is.EqualTo("이번 판 기록·먹빛 포기"));

        Invoke(
            view,
            "BindResult",
            new GameOverResult(
                120,
                0,
                false,
                2,
                2,
                true,
                true,
                false,
                GameOverPersistenceState.RecordWritePending));
        Assert.That(
            view.SaveNoticeLabel,
            Is.EqualTo("기록 저장을 다시 시도해요"));
        Assert.That(view.TouchHintLabel, Is.EqualTo("재시도 중단하고 로비로"));
    }

    [Test]
    public void RecordWriteRetryDoesNotSettleGrowthTwice()
    {
        var growthStore = new MemoryPermanentGrowthStore();
        PermanentGrowthProfile.UseStoreForTests(growthStore);
        var scoreStore = new MemoryScoreStore
        {
            ThrowOnSave = true,
        };
        ScoreManager.UseStoreForTests(scoreStore);

        host = new GameObject("RecordWriteRetryHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 100);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        var account = host.AddComponent<MukJumpAccountRuntime>();
        Invoke(account, "OnEnable");
        SetField(account, "dirty", false);

        var pending = (GameOverResult)Invoke(manager, "SettleGameOverResult");
        Assert.That(pending.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.RecordWritePending));
        int growthSaveCount = growthStore.SaveCount;
        SetField(manager, "latestGameOverResult", pending);
        // 성장 프로필 변경 자체도 계정 동기화를 예약할 수 있다. 여기서는
        // 기록 재시도 성공이 완료 판 알림을 별도로 발생시키는지만 격리한다.
        SetField(account, "dirty", false);

        scoreStore.ThrowOnSave = false;
        Invoke(manager, "RetryPendingGameOverPersistence");

        var completed = GetField<GameOverResult>(manager, "latestGameOverResult");
        Assert.That(completed.PersistenceState,
            Is.EqualTo(GameOverPersistenceState.Complete));
        Assert.That(scoreStore.Best, Is.EqualTo(100));
        Assert.That(completed.CumulativeGrowthDistanceMeters, Is.EqualTo(100));
        Assert.That(completed.PreviousGrowthRewardDistanceMeters, Is.EqualTo(50));
        Assert.That(completed.NextGrowthRewardDistanceMeters, Is.EqualTo(150));
        Assert.That(growthStore.SaveCount, Is.EqualTo(growthSaveCount),
            "기록 저장 재시도는 이미 확정한 성장 정산을 다시 호출하면 안 됩니다.");
        Assert.That(GetField<bool>(account, "dirty"), Is.True,
            "기록 저장 재시도 성공은 클라우드 동기화를 즉시 예약해야 합니다.");
    }

    [Test]
    public void GameOverRevealPoseKeepsTopFixedAndLowersTheRolledEdge()
    {
        host = new GameObject("GameOverHost");
        var view = host.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");
        Invoke(view, "BindResults", 120, 120, true);

        var canvasRoot = host.transform.Find("GameOverPopupCanvas");
        var panel = canvasRoot.Find("SafeAreaRoot/ScrollResultPopup");
        var body = panel.Find("ScrollBody") as RectTransform;
        var topRoll = panel.Find("TopRoll") as RectTransform;
        var bottomRoll = panel.Find("BottomRoll") as RectTransform;
        var content = panel.Find("ResultContent") as RectTransform;
        var newBestSeal = content.Find("NewBestSeal") as RectTransform;
        var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
        var contentGroup = content.GetComponent<CanvasGroup>();
        var newBestGroup = newBestSeal.GetComponent<CanvasGroup>();

        Invoke(view, "ApplyRevealPose", 0f, true);
        float panelLayoutScale =
            GetField<float>(view, "panelLayoutScale");
        var paper = body.Find("ScrollPaper").GetComponent<HanjiScrollPaperGraphic>();
        float closedFraction = paper.RevealedFraction;
        float closedBottomY = bottomRoll.anchoredPosition.y;
        Assert.That(panel.localScale.x, Is.EqualTo(panelLayoutScale));
        Assert.That(body.localScale, Is.EqualTo(Vector3.one));
        Assert.That(topRoll.anchoredPosition.y, Is.EqualTo(550f));
        Assert.That(closedBottomY, Is.GreaterThan(500f));

        Invoke(view, "ApplyRevealPose", 0.5f, true);
        Assert.Greater(paper.RevealedFraction, closedFraction);
        Assert.That(body.localScale, Is.EqualTo(Vector3.one));
        Assert.That(topRoll.anchoredPosition.y, Is.EqualTo(550f));
        Assert.Less(bottomRoll.anchoredPosition.y, closedBottomY);

        Invoke(view, "ApplyRevealPose", 1f, true);
        Assert.That(body.localScale.y, Is.EqualTo(1f).Within(0.001f));
        Assert.That(paper.RevealedFraction, Is.EqualTo(1f));
        Assert.Greater(topRoll.anchoredPosition.y, 250f);
        Assert.That(topRoll.anchoredPosition.y,
            Is.EqualTo(-bottomRoll.anchoredPosition.y).Within(0.01f));
        Assert.That(
            panel.localScale.x,
            Is.EqualTo(panelLayoutScale).Within(0.001f));
        Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(rootGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(contentGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(newBestGroup.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(newBestSeal.localScale.x, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void PauseOverlayVisibilityControlsRaycastBlocking()
    {
        host = new GameObject("PauseHost");
        var view = host.AddComponent<PauseMenuView>();
        Invoke(view, "BuildIfNeeded");

        var overlay = host.transform.Find("PauseMenuCanvas/PauseOverlay");
        var group = overlay?.GetComponent<CanvasGroup>();
        Assert.IsNotNull(group);

        Invoke(view, "SetOverlayVisible", true, false);

        Assert.That(group.alpha, Is.EqualTo(1f));
        Assert.IsTrue(group.interactable);
        Assert.IsTrue(group.blocksRaycasts);

        Invoke(view, "SetOverlayVisible", false, false);

        Assert.That(group.alpha, Is.Zero);
        Assert.IsFalse(group.interactable);
        Assert.IsFalse(group.blocksRaycasts);
    }

    [Test]
    public void PauseAndResumePreservePlayingStateAndRestoreTime()
    {
        // 에디터가 정지된 상태여도 재개할 플레이 시간은 테스트에서 명시한다.
        const float playingTimeScale = 0.75f;
        const float playingFixedDelta = 0.016f;
        Time.timeScale = playingTimeScale;
        Time.fixedDeltaTime = playingFixedDelta;
        AudioListener.pause = false;
        host = new GameObject("GameManagerHost");
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");

        Assert.IsTrue(manager.PauseGame());
        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.IsTrue(manager.IsPaused);
        Assert.IsFalse(manager.IsGameplayTicking);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);

        Assert.IsTrue(manager.ResumeGame());
        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.IsFalse(manager.IsPaused);
        Assert.IsTrue(manager.IsGameplayTicking);
        Assert.That(Time.timeScale, Is.EqualTo(playingTimeScale).Within(0.000001f));
        Assert.That(Time.fixedDeltaTime,
            Is.EqualTo(playingFixedDelta).Within(0.000001f));
        Assert.IsFalse(AudioListener.pause);
    }

    [TestCase("RefreshManagerState")]
    [TestCase("RefreshImmediate")]
    [TestCase("HandlePauseChanged")]
    public void PauseNavigationKeepsClosedScrollHiddenWhileLobbyTransitionOwnsScreen(string refresh)
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition);
        int transitionStarts = 0;
        SetField(transition, "startCoroutineForTests",
            (System.Action<System.Collections.IEnumerator>)(_ => transitionStarts++));
        Invoke(view, "HandleLobbyPressed");
        Assert.That(manager.IsTransitioning, Is.False, "첫 종료 클릭은 확인창만 엽니다.");
        Invoke(view, "HandleExitConfirmed");
        Assert.That(manager.IsTransitioning, Is.True);
        Assert.That(manager.PauseReason, Is.EqualTo(GameplayPauseReason.UserMenu),
            "로비가 화면을 덮을 때까지 물리는 일시정지 상태를 유지합니다.");
        for (int frame = 0; frame < 6; frame++)
        {
            if (refresh == "HandlePauseChanged") Invoke(view, refresh, true);
            else Invoke(view, refresh);
            Assert.That(GetField<bool>(view, "overlayVisible"), Is.False,
                "전환 중 UserMenu 상태를 새 일시정지 요청으로 해석하면 안 됩니다.");
            Assert.That(GetField<CanvasGroup>(view, "overlayGroup").alpha, Is.Zero);
            Assert.That(GetField<Button>(view, "pauseButton").gameObject.activeSelf, Is.False);
        }
        Invoke(view, "HandleLobbyPressed");
        Assert.That(transitionStarts, Is.EqualTo(1), "중복 클릭은 씬 전환을 다시 시작하지 않습니다.");
    }

    [Test]
    public void PauseNavigationFailedTransitionRestoresBothActionsAndCanPauseAgain()
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition);
        Invoke(view, "HandleLobbyPressed");
        Invoke(view, "HandleExitConfirmed");
        Assert.That(GetField<Button>(view, "resumeButton").interactable, Is.False);
        Assert.That(GetField<Button>(view, "lobbyButton").interactable, Is.False);
        // 화면 덮기 전에 전환이 취소되면 실제 실패 콜백이 게임의 전환 잠금을 해제한다.
        Invoke(transition, "OnDisable");
        Assert.That(manager.IsTransitioning, Is.False);
        Invoke(view, "RefreshManagerState");
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.True);
        Assert.That(GetField<Button>(view, "resumeButton").interactable, Is.True);
        Assert.That(GetField<Button>(view, "lobbyButton").interactable, Is.True);
        Assert.That(manager.IsPaused, Is.True);
        Invoke(view, "HandleResumePressed");
        Assert.That(manager.IsPaused, Is.False);
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.False);
        Invoke(view, "HandlePausePressed");
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.True);
    }

    [Test]
    public void PauseNavigationStaleCloseCannotReopenMenuOverAnotherTransition()
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out _);
        SetField(manager, "transitionInProgress", true);
        Invoke(view, "HandleLobbyPressed");
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.False,
            "다른 전환이 이미 시작됐다면 오래된 로비 액션의 실패 복구로 다시 열면 안 됩니다.");
        Assert.That(manager.IsPaused, Is.True);
    }

    [Test]
    public void PauseNavigationAlsoStaysHiddenWhenOnlyBrushTransitionIsPlaying()
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition);
        Invoke(view, "SetOverlayVisible", false, false);
        SetField(transition, "playing", true);
        Assert.That(GetField<bool>(manager, "transitionInProgress"), Is.False);
        Invoke(view, "RefreshManagerState");
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.False);
    }

    [TestCase(GameState.Lobby)]
    [TestCase(GameState.GameOver)]
    public void PauseNavigationResidualPauseReasonCannotOpenMenuOutsidePlaying(GameState state)
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out _);
        SetProperty(manager, "State", state);
        Invoke(view, "RefreshManagerState");
        Invoke(view, "HandlePauseChanged", true);
        Invoke(view, "RefreshImmediate");
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.False);
        Assert.That(GetField<Button>(view, "pauseButton").gameObject.activeSelf, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PauseExitCancelOrDimKeepsCurrentRunPaused(bool viaDim)
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out _);
        GetField<Button>(view, "lobbyButton").onClick.Invoke();
        Assert.That(GetField<bool>(view, "exitConfirmationOpen"), Is.True);
        Assert.That(manager.IsTransitioning, Is.False);
        Assert.That(GetField<Button>(view, "resumeButton").interactable, Is.False);
        Assert.That(GetField<Button>(view, "lobbyButton").interactable, Is.False);
        // 아래 일시정지판의 오래된 액션도 확인창을 건너뛰지 못한다.
        Invoke(view, "HandleResumePressed");
        Assert.That(manager.IsPaused, Is.True);
        GetField<Button>(view, viaDim ? "exitDimButton" : "cancelExitButton").onClick.Invoke();
        Assert.That(GetField<bool>(view, "exitConfirmationOpen"), Is.False);
        Assert.That(GetField<RectTransform>(view, "exitPromptRoot").gameObject.activeSelf, Is.False);
        Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        Assert.That(manager.PauseReason, Is.EqualTo(GameplayPauseReason.UserMenu));
        Assert.That(manager.IsTransitioning, Is.False);
        Assert.That(GetField<Button>(view, "resumeButton").interactable, Is.True);
        Assert.That(GetField<Button>(view, "lobbyButton").interactable, Is.True);
    }

    [Test]
    public void PauseExitConfirmationIsRequiredAndCannotRunTwice()
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition);
        int starts = 0;
        SetField(transition, "startCoroutineForTests", (System.Action<System.Collections.IEnumerator>)(_ => starts++));
        Invoke(view, "HandleExitConfirmed");
        Assert.That(starts, Is.Zero);
        GetField<Button>(view, "lobbyButton").onClick.Invoke();
        GetField<Button>(view, "lobbyButton").onClick.Invoke();
        Assert.That(starts, Is.Zero);
        GetField<Button>(view, "confirmExitButton").onClick.Invoke();
        GetField<Button>(view, "confirmExitButton").onClick.Invoke();
        Assert.That(starts, Is.EqualTo(1));
        Assert.That(manager.IsTransitioning, Is.True);
        Assert.That(GetField<RectTransform>(view, "exitPromptRoot").gameObject.activeSelf, Is.False);
        Assert.That(GetField<bool>(view, "overlayVisible"), Is.False);
    }

    [Test]
    public void PauseExitStaleConfirmationCannotLeaveAnotherScreen()
    {
        PauseMenuView view = CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition);
        int starts = 0;
        SetField(transition, "startCoroutineForTests", (System.Action<System.Collections.IEnumerator>)(_ => starts++));
        Invoke(view, "HandleLobbyPressed");
        Invoke(view, "SetOverlayVisible", false, false);
        Invoke(view, "HandleExitConfirmed");
        Assert.That(starts, Is.Zero);
        Assert.That(GetField<bool>(view, "exitConfirmationOpen"), Is.False);
        Assert.That(manager.IsPaused, Is.True);
    }

    [Test]
    public void PauseExitPopupHasLocalizedWarningAndBlockingPaper()
    {
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        try
        {
            PauseMenuView view = CreatePausedNavigationMenu(out _, out _);
            Invoke(view, "HandleLobbyPressed");
            var content = GetField<RectTransform>(view, "exitPanel");
            Assert.That(content.GetComponent<HanjiScrollFrame>(), Is.Not.Null);
            Assert.That(content.Find("PaperHitArea").GetComponent<Image>().raycastTarget, Is.True);
            var message = content.Find("Message").GetComponent<Text>();
            Assert.That(message.text, Is.EqualTo("지금 종료하면 이번 판 기록이 저장되지 않습니다."));
            GameLocalization.SetLanguage(GameLanguage.English);
            Assert.That(message.text, Is.EqualTo("If you leave now, this run's record will not be saved."));
            Assert.That(content.Find("Title").GetComponent<Text>().text, Is.EqualTo("Leave this run?"));
            Assert.That(GetField<Button>(view, "confirmExitButton").transform.Find("Label").GetComponent<Text>().text,
                Is.EqualTo("Leave"));
            Assert.That(InkUiStyle.UsesActionButtonSprite(GetField<Button>(view, "confirmExitButton").targetGraphic as Image), Is.True);
        }
        finally { LobbySettingsProfile.RestoreDefaultStoreForTests(); }
    }

    [TestCase(GameState.Playing, false, false, true)]
    [TestCase(GameState.Playing, true, false, false)]
    [TestCase(GameState.Playing, false, true, false)]
    [TestCase(GameState.Playing, true, true, false)]
    [TestCase(GameState.Lobby, false, false, false)]
    [TestCase(GameState.GameOver, false, false, false)]
    public void PauseExitGaugeVisibilityExcludesBlackTransitionsAndModals(GameState state, bool paused, bool transitioning, bool expected)
    {
        var method = typeof(PrototypeHud).GetMethod("ShouldRenderGameplayHud", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { state, paused, transitioning }), Is.EqualTo(expected));
    }

    PauseMenuView CreatePausedNavigationMenu(out GameManager manager, out BrushTransitionView transition)
    {
        host = new GameObject("PauseNavigationHost");
        manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");
        Assert.That(manager.PauseGame(), Is.True);
        transition = host.AddComponent<BrushTransitionView>();
        SetField(transition, "reducedMotionForTests", (System.Func<bool>)(() => true));
        // 실제 ReturnToLobby를 사용하되 씬을 교체하지 않고 전환 대기 상태를 검사한다.
        SetField(transition, "startCoroutineForTests",
            (System.Action<System.Collections.IEnumerator>)(_ => { }));
        SetField(manager, "transitionView", transition);
        var view = host.AddComponent<PauseMenuView>();
        Invoke(view, "BuildIfNeeded");
        Invoke(view, "BindManager");
        Invoke(view, "BindButtons");
        Invoke(view, "RefreshImmediate");
        return view;
    }

    [Test]
    public void ThrowingStateListenerCannotInterruptTransitionOrLaterListener()
    {
        host = new GameObject("ThrowingStateListenerHost");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        int laterNotifications = 0;
        manager.StateChanged += (_, _) =>
            throw new System.InvalidOperationException("state observer failed");
        manager.StateChanged += (previous, current) =>
        {
            if (previous == GameState.Lobby && current == GameState.Playing)
                laterNotifications++;
        };
        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 게임 상태 알림 구독자 예외를 격리했습니다: " +
            "state observer failed");

        Invoke(manager, "SetState", GameState.Playing);

        Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        Assert.That(laterNotifications, Is.EqualTo(1));
    }

    [Test]
    public void AbandoningRevivedRunDiscardsPendingSettlement()
    {
        pendingSettlementStore.Json = JsonUtility.ToJson(
            new PendingGameOverSettlementSnapshot
            {
                runId = "3123456789abcdef0123456789abcdef",
                scoreHeight = 74,
                swarmProgressHeight = 74,
                activeGameplaySeconds = 20f,
                eligible = true,
            });
        host = new GameObject("AbandonedRevivedRunHost");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        Assert.That(
            (bool)Invoke(manager, "TryAbandonPendingGameOverSettlement"),
            Is.True);
        Assert.That(pendingSettlementStore.Json, Is.Empty);
        Assert.That(GetField<bool>(manager, "gameOverResultSettled"), Is.True);
        Assert.That(
            GameManager.TryRecoverPendingGameOverSettlement(),
            Is.True);
        Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.Zero,
            "명시적으로 포기한 부활 판은 다음 실행에서 정산되면 안 됩니다.");
    }

    [Test]
    public void AbandoningRunFailsClosedWhenPendingDeleteFails()
    {
        pendingSettlementStore.Json = JsonUtility.ToJson(
            new PendingGameOverSettlementSnapshot
            {
                runId = "4123456789abcdef0123456789abcdef",
                scoreHeight = 74,
                swarmProgressHeight = 74,
                activeGameplaySeconds = 20f,
                eligible = true,
            });
        pendingSettlementStore.ThrowOnClear = true;
        host = new GameObject("AbandonedRunDeleteFailureHost");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "4123456789abcdef0123456789abcdef");

        Assert.That(
            (bool)Invoke(manager, "TryAbandonPendingGameOverSettlement"),
            Is.False);
        Assert.That(pendingSettlementStore.Json, Is.Not.Empty);
        Assert.That(GetField<bool>(manager, "gameOverResultSettled"), Is.False,
            "폐기 저장에 실패했으면 로비 전환을 허용한 것처럼 표시하면 안 됩니다.");
    }

    [Test]
    public void PendingSettlementWriteFailureDoesNotEscapeGameOverFlow()
    {
        pendingSettlementStore.ThrowOnSave = true;
        host = new GameObject("PendingSettlementWriteFailureHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 74);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "5123456789abcdef0123456789abcdef");

        Assert.That(
            (bool)Invoke(manager, "PersistPendingGameOverSettlement"),
            Is.False,
            "기기 저장소 예외는 값으로 격리되어 게임오버 팝업을 중단하면 안 됩니다.");
        Assert.That(pendingSettlementStore.Json, Is.Empty);
    }

    [Test]
    public void StaleRecoveryKeyCannotTurnMidRunQuitIntoSettlement()
    {
        const string staleJson =
            "{\"version\":1," +
            "\"runId\":\"8123456789abcdef0123456789abcdef\"," +
            "\"swarmProgressHeight\":40,\"scoreHeight\":40," +
            "\"previousBest\":0,\"activeGameplaySeconds\":12," +
            "\"eligible\":true}";
        pendingSettlementStore.Json = staleJson;
        pendingSettlementStore.ThrowOnClear = true;
        Assert.That(
            GameManager.TryRecoverPendingGameOverSettlement(),
            Is.True,
            "이미 내구 정산된 값은 stale key 삭제 실패만으로 플레이를 막지 않습니다.");

        host = new GameObject("StalePendingSettlementHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 99);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "9123456789abcdef0123456789abcdef");

        Invoke(manager, "OnApplicationPause", true);

        Assert.That(pendingSettlementStore.SaveCount, Is.Zero);
        Assert.That(pendingSettlementStore.Json, Is.EqualTo(staleJson),
            "현재 판이 실제 GameOver를 거치지 않았다면 오래된 key를 새 run으로 덮으면 안 됩니다.");
    }

    [Test]
    public void StaleRecoveredKeyDeleteFailureCannotBlockNewRunAbandon()
    {
        const string staleRunId = "a123456789abcdef0123456789abcdef";
        pendingSettlementStore.Json = JsonUtility.ToJson(
            new PendingGameOverSettlementSnapshot
            {
                runId = staleRunId,
                scoreHeight = 40,
                swarmProgressHeight = 40,
                activeGameplaySeconds = 12f,
                eligible = true,
            });
        pendingSettlementStore.ThrowOnClear = true;
        Assert.That(
            GameManager.TryRecoverPendingGameOverSettlement(),
            Is.True);
        Assert.That(pendingSettlementStore.Json, Is.Not.Empty);

        host = new GameObject("StaleAbandonHost");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "b123456789abcdef0123456789abcdef");

        Assert.That(
            (bool)Invoke(manager, "TryAbandonPendingGameOverSettlement"),
            Is.True,
            "내구 정산된 이전 run의 stale key는 새 판의 로비 복귀를 막으면 안 됩니다.");
        Assert.That(pendingSettlementStore.Json, Is.Not.Empty);
    }

    [Test]
    public void AmbiguousCurrentRunSnapshotStillFailsClosedOnDeleteFailure()
    {
        const string currentRunId = "c123456789abcdef0123456789abcdef";
        pendingSettlementStore.Json = JsonUtility.ToJson(
            new PendingGameOverSettlementSnapshot
            {
                runId = currentRunId,
                scoreHeight = 74,
                swarmProgressHeight = 74,
                activeGameplaySeconds = 20f,
                eligible = true,
            });
        pendingSettlementStore.ThrowOnClear = true;

        host = new GameObject("AmbiguousCurrentRunAbandonHost");
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(manager, "currentRunId", currentRunId);
        SetField(manager, "hasActivePendingGameOverSettlement", false);

        Assert.That(
            (bool)Invoke(manager, "TryAbandonPendingGameOverSettlement"),
            Is.False,
            "SetString 뒤 flush 예외로 active flag가 없더라도 현재 run 복구본은 보존해야 합니다.");
        Assert.That(pendingSettlementStore.Json, Is.Not.Empty);
    }

    [Test]
    public void RewardRevivePendingSettlementRefreshesOnApplicationPause()
    {
        host = new GameObject("ActivePendingSettlementHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 40);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "a123456789abcdef0123456789abcdef");

        Assert.That(
            (bool)Invoke(manager, "PersistPendingGameOverSettlement"),
            Is.True);
        Assert.That(pendingSettlementStore.SaveCount, Is.EqualTo(1));
        SetProperty(score, "Height", 74);

        Invoke(manager, "OnApplicationPause", true);

        Assert.That(pendingSettlementStore.SaveCount, Is.EqualTo(2));
        Assert.That(pendingSettlementStore.Json,
            Does.Contain("\"scoreHeight\":74"));
    }

    [Test]
    public void FailedFirstPendingWriteStillRetriesAfterRewardRevivePause()
    {
        host = new GameObject("FailedFirstPendingSettlementHost");
        var score = host.AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        Invoke(score, "Awake");
        SetProperty(score, "Height", 40);
        var manager = host.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        SetField(
            manager,
            "currentRunId",
            "d123456789abcdef0123456789abcdef");
        pendingSettlementStore.ThrowOnSave = true;

        LogAssert.Expect(
            LogType.Warning,
            "[MukJump] 게임오버 정산 복구본을 저장하지 못했습니다. " +
            "현재 결과 화면은 계속 진행합니다: " +
            "Injected pending settlement write failure");
        Assert.That(
            (bool)Invoke(manager, "PersistPendingGameOverSettlement"),
            Is.False);
        Assert.That(
            GetField<bool>(manager, "requiresPendingGameOverSettlementRefresh"),
            Is.True,
            "첫 쓰기 실패도 광고 부활 뒤 재저장할 정산 책임을 남겨야 합니다.");

        // 광고 부활은 같은 run ID를 유지한 채 Playing으로 돌아간다.
        SetProperty(manager, "State", GameState.Playing);
        SetProperty(score, "Height", 74);
        pendingSettlementStore.ThrowOnSave = false;

        Invoke(manager, "OnApplicationPause", true);

        Assert.That(pendingSettlementStore.SaveCount, Is.EqualTo(1));
        Assert.That(pendingSettlementStore.Json,
            Does.Contain("\"scoreHeight\":74"));
    }

    [Test]
    public void ApplicationBackgroundCannotResumeAnotherPauseOwner()
    {
        host = new GameObject("GameManagerHost");
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");

        Assert.IsTrue(manager.PauseGame());
        Assert.IsFalse(manager.PauseForApplicationBackground());
        Assert.IsFalse(manager.ResumeFromApplicationBackground());
        Assert.IsTrue(manager.IsPaused);
        Assert.AreEqual(GameplayPauseReason.UserMenu, manager.PauseReason);
        Assert.IsTrue(manager.ResumeGame());

        Assert.IsTrue(manager.PauseForApplicationBackground());
        Assert.AreEqual(
            GameplayPauseReason.ApplicationBackground,
            manager.PauseReason);
        Assert.IsFalse(manager.ResumeGame());
        Assert.IsTrue(manager.ResumeFromApplicationBackground());
        Assert.IsFalse(manager.IsPaused);
    }

    [TestCase(0, "먹이 아직 덜 말랐어요")]
    [TestCase(8, "발판보다 먼저 포기했어요")]
    [TestCase(12, "그래도 두 자릿수예요")]
    [TestCase(35, "제법 하찮게 올랐어요")]
    [TestCase(80, "먹방울치고 꽤 높았어요")]
    public void ResultTitleKeepsFailureLightweight(int height, string expected)
    {
        Assert.AreEqual(expected, GameOverPopupView.ResultTitleForHeight(height));
    }

    [Test]
    public void FirstRunTutorialPauseOwnsWorldUntilTutorialReleasesIt()
    {
        host = new GameObject("GameManagerHost");
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");

        Assert.IsTrue(manager.PauseForFirstRunTutorial());
        Assert.IsTrue(manager.IsPaused);
        Assert.AreEqual(
            GameplayPauseReason.FirstRunTutorial,
            manager.PauseReason);
        Assert.IsFalse(manager.IsGameplayTicking);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);
        Assert.IsFalse(manager.PauseGame(),
            "사용자 일시정지가 튜토리얼의 정지 소유권을 빼앗으면 안 됩니다.");
        Assert.IsFalse(manager.ResumeGame(),
            "사용자 메뉴 닫기가 튜토리얼을 몰래 재개하면 안 됩니다.");

        Assert.IsTrue(manager.ResumeFirstRunTutorial());
        Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(GameplayPauseReason.None, manager.PauseReason);
        Assert.IsTrue(manager.IsGameplayTicking);
        Assert.That(Time.timeScale,
            Is.EqualTo(originalTimeScale).Within(0.000001f));
        Assert.That(Time.fixedDeltaTime,
            Is.EqualTo(originalFixedDeltaTime).Within(0.000001f));
        Assert.AreEqual(originalAudioPause, AudioListener.pause);
    }

    [Test]
    public void PauseUpdatePreservesAutoJumpChargeState()
    {
        host = new GameObject("GameManagerHost");
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");

        playerHost = new GameObject("PlayerHost");
        playerHost.AddComponent<Rigidbody2D>();
        playerHost.AddComponent<PlayerController>();
        var autoJump = playerHost.AddComponent<AutoJump>();
        // EditMode에서는 일반 MonoBehaviour의 Awake가 자동 실행되지 않으므로 참조를 명시적으로 결합한다.
        Invoke(autoJump, "Awake");
        SetField(autoJump, "chargeTimer", 0.64f);
        SetField(autoJump, "chargeStarted", true);
        SetField(autoJump, "hasLaunched", true);
        SetField(autoJump, "wasRising", true);
        Assert.That(GetField<float>(autoJump, "chargeTimer"),
            Is.EqualTo(0.64f).Within(0.000001f));

        Assert.IsTrue(manager.PauseGame());
        Invoke(autoJump, "Update");

        Assert.That(GetField<float>(autoJump, "chargeTimer"),
            Is.EqualTo(0.64f).Within(0.000001f));
        Assert.IsTrue(GetField<bool>(autoJump, "chargeStarted"));
        Assert.IsTrue(GetField<bool>(autoJump, "hasLaunched"));
        Assert.IsTrue(GetField<bool>(autoJump, "wasRising"));

        Assert.IsTrue(manager.ResumeGame());
        Assert.That(GetField<float>(autoJump, "chargeTimer"),
            Is.EqualTo(0.64f).Within(0.000001f));
        Assert.IsTrue(GetField<bool>(autoJump, "chargeStarted"));
        Assert.IsTrue(GetField<bool>(autoJump, "hasLaunched"));
        Assert.IsTrue(GetField<bool>(autoJump, "wasRising"));
    }

    [Test]
    public void PausePreventsItemTelegraphStateFromStarting()
    {
        host = new GameObject("GameManagerHost");
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.Playing);
        Invoke(manager, "OnEnable");

        cameraHost = new GameObject("ItemCamera") { tag = "MainCamera" };
        cameraHost.transform.position = new Vector3(0f, 0f, -10f);
        var camera = cameraHost.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        playerHost = new GameObject("PausedItem");
        var item = playerHost.AddComponent<ItemPickup>();
        playerHost.transform.position = new Vector3(0f, 3f, 0f);
        Invoke(item, "Awake");
        item.Configure(ItemType.InkDrop, 0f);
        SetField(item, "worldCamera", camera);

        Assert.IsTrue(manager.PauseGame());
        Invoke(item, "Update");

        Assert.IsFalse(GetField<bool>(item, "telegraphed"));
        Assert.That(GetField<float>(item, "telegraphTime"), Is.Zero);
    }

    static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);
    }

    static object InvokeStatic(
        System.Type type,
        string methodName,
        params object[] arguments)
    {
        return type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(
            null,
            arguments);
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    static float LeftEdge(RectTransform rect)
    {
        return rect.anchoredPosition.x - rect.sizeDelta.x * 0.5f;
    }

    static float RightEdge(RectTransform rect)
    {
        return rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f;
    }

    static int CountDirectChildren(Transform parent, string childName)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName)
                count++;
        return count;
    }

    GameManager CreateGameOverManager(string objectName)
    {
        // 이 fixture는 GameManager.Awake를 실행하지 않는다. 광고가 제어하는
        // BGM 의존성만 설치하고 실제 클립 재생·영구 객체 생성은 하지 않는다.
        Assert.That(BackgroundMusicController.Instance == null, Is.True,
            "다른 테스트의 BGM이 남으면 독립적으로 광고 상태를 검증할 수 없습니다.");
        musicHost = new GameObject("RewardedAdMusicTest");
        var music = musicHost.AddComponent<BackgroundMusicController>();
        Invoke(music, "OnEnable");
        Assert.That(BackgroundMusicController.Instance, Is.SameAs(music));
        host = new GameObject(objectName);
        var manager = host.AddComponent<GameManager>();
        SetProperty(manager, "State", GameState.GameOver);
        SetField(manager, "gameOverResultSettled", false);
        SetField(manager, "reviveUsedThisRun", false);
        Invoke(manager, "OnEnable");
        return manager;
    }

    sealed class ScriptedAdProvider : IFullScreenAdProvider
    {
        System.Action<bool> completion;

        public bool ThrowOnShow { get; set; }
        public bool ThrowOnReady { get; set; }
        public int ReadyCalls { get; private set; }

        public bool IsReady(FullScreenAdPlacement placement)
        {
            ReadyCalls++;
            if (ThrowOnReady)
                throw new System.InvalidOperationException("ready failed");
            return true;
        }

        public void Preload(FullScreenAdPlacement placement) { }

        public void Show(
            FullScreenAdPlacement placement,
            System.Action<bool> onCompleted)
        {
            if (ThrowOnShow)
                throw new System.InvalidOperationException("show failed");
            completion = onCompleted;
        }

        public void Complete(bool rewardEarned)
        {
            completion?.Invoke(rewardEarned);
        }
    }
}
