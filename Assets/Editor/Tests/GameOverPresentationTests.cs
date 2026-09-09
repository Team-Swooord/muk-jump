using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MukJump.Core;

public class GameOverPresentationTests
{
    GameObject host;
    GameOverPopupView view;
    Transform panel;
    Transform content;

    [SetUp]
    public void SetUp()
    {
        // 사용자 기기의 현재 언어와 무관하게 이 한국어 표시 계약을 검증한다.
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        host = new GameObject("ResultPresentationTest");
        view = host.AddComponent<GameOverPopupView>();
        Invoke("BuildIfNeeded");
        panel = host.transform.Find("GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup");
        content = panel.Find("ResultContent");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        LobbySettingsProfile.RestoreDefaultStoreForTests();
    }

    [Test]
    public void PaperIsUprightContainedAndAllDecorationIsNonBlocking()
    {
        Invoke("ApplyRevealPose", 1f, false);
        var paper = panel.Find("ScrollBody/ScrollPaper").GetComponent<HanjiScrollPaperGraphic>();
        Assert.That(paper.mainTexture,
            Is.EqualTo(Resources.Load<Texture2D>("MukJump/UI/PermanentGrowth/pg_hanji_background")));
        Assert.That(panel.Find("ScrollBody/PaperCore"), Is.Null);
        Assert.That(panel.Find("ScrollBody/ScrollBodyOutline"), Is.Null);
        foreach (Graphic image in panel.Find("ScrollBody").GetComponentsInChildren<Graphic>())
        {
            Assert.That(image.rectTransform.localEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(image.raycastTarget, Is.False);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, image.rectTransform);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-384f));
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(384f));
        }
        foreach (string roll in new[] { "TopRoll", "BottomRoll" })
        {
            Assert.That(panel.Find(roll + "/LeftCap"), Is.Null);
            Assert.That(panel.Find(roll + "/RightCap"), Is.Null);
            var image = panel.Find(roll + "/PaperRoll").GetComponent<Image>();
            Assert.That(image.sprite.name, Is.EqualTo("scroll_roll_hanji_v2"));
            Assert.That(image.raycastTarget, Is.False);
        }
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(9)]
    [TestCase(19)]
    [TestCase(49)]
    [TestCase(132)]
    [TestCase(9999)]
    [TestCase(12345)]
    [TestCase(int.MaxValue)]
    public void RecordAndCaptionStayReadableWithoutClipping(int height)
    {
        Invoke("BindResults", height, height, true);
        AssertFits(TextAt("Title"), 1);
        AssertFits(TextAt("CurrentResult/Caption"), 1);
        AssertFits(TextAt("CurrentResult/Value"), 1);
        AssertFits(TextAt("BestResult/Value"), 1);
        AssertFits(TextAt("BestResult/Caption"), 1);
        AssertResultCenterline();
        Assert.That(TextAt("CurrentResult/Value").fontSize, Is.EqualTo(156));
        Assert.That(TextAt("BestResult/Value").fontSize, Is.EqualTo(64));
        AssertFits(TextAt("NewBestSeal/NewBest"), 1);
    }

    [TestCase(GameOverPersistenceState.Complete)]
    [TestCase(GameOverPersistenceState.ScoreBaselinePending)]
    [TestCase(GameOverPersistenceState.GrowthRecoveryRequired)]
    [TestCase(GameOverPersistenceState.RecordWritePending)]
    public void EverySaveStateFitsAndKeepsTheMainAction(GameOverPersistenceState state)
    {
        view.RefreshResult(new GameOverResult(132, 132, true, 14, 32, true,
            persistenceState: state, cumulativeGrowthDistanceMeters: 132,
            previousGrowthRewardDistanceMeters: 120, nextGrowthRewardDistanceMeters: 160));
        Text notice = TextAt("SaveNotice");
        Assert.That(notice.gameObject.activeSelf, Is.EqualTo(state != GameOverPersistenceState.Complete));
        if (notice.gameObject.activeSelf) AssertFits(notice, 1, FontStyle.Normal);
        AssertResultCenterline();
        AssertFits(TextAt("RetryBrush/TouchHint"), state == GameOverPersistenceState.Complete ? 1 : 2);
        Assert.That(content.Find("RetryBrush").GetComponent<Button>().interactable, Is.True);
        Assert.That(TextAt("RetryBrush/TouchHint").fontSize, Is.EqualTo(56));
    }

    [Test]
    public void AdStatesAndSecondDeathPreserveReadableUnlockedActions()
    {
        view.SetReviveOffer(true);
        Assert.That(TextAt("ReviveBrush/Label").text, Is.EqualTo("광고 시청하고 부활하기"));
        AssertFits(TextAt("ReviveBrush/Label"), 1);
        Assert.That(TextAt("ReviveBrush/Label").fontSize, Is.EqualTo(56));
        view.SetReviveRequestInFlight(true);
        AssertFits(TextAt("ReviveBrush/Label"), 1);
        view.SetReviveOffer(false, true);
        AssertFits(TextAt("ReviveBrush/Label"), 1);
        view.RefreshResult(new GameOverResult(182, 182, true, 1, 9, true));
        var lobby = content.Find("RetryBrush").GetComponent<Button>();
        Assert.That(lobby.interactable, Is.True);
        Assert.That(content.Find("ReviveBrush").gameObject.activeSelf, Is.False);
        view.ShowPendingAbandonConfirmation();
        AssertFits(TextAt("RetryBrush/TouchHint"), 2);
        Assert.That(TextAt("RetryBrush/TouchHint").fontSize, Is.EqualTo(56));
    }

    [TestCase(1080, 1920, 0, 0)]
    [TestCase(1179, 2556, 177, 102)]
    [TestCase(1440, 3200, 120, 100)]
    public void PortraitPanelFitsSafeArea(int width, int height, int top, int bottom)
    {
        var size = ((RectTransform)panel).sizeDelta;
        var safe = new Rect(0, bottom, width, height - top - bottom);
        float scale = MobileUiLayout.CalculateFitScale(size, safe, width, height, new Vector2(28, 32));
        float pixelsPerUnit = height / 1920f;
        Assert.That(size.x * scale * pixelsPerUnit, Is.LessThanOrEqualTo(safe.width));
        Assert.That(size.y * scale * pixelsPerUnit, Is.LessThanOrEqualTo(safe.height));
    }

    Text TextAt(string path) => content.Find(path).GetComponent<Text>();

    [Test]
    public void CenteredRecordsNoticeAndActionsKeepClearGaps()
    {
        RectTransform caption = TextAt("BestResult/Caption").rectTransform;
        RectTransform value = TextAt("BestResult/Value").rectTransform;
        Assert.That(caption.anchoredPosition.y - caption.rect.height * 0.5f,
            Is.GreaterThanOrEqualTo(value.anchoredPosition.y + value.rect.height * 0.5f + 8f));
        RectTransform best = (RectTransform)content.Find("BestResult");
        RectTransform notice = TextAt("SaveNotice").rectTransform;
        Assert.That(best.anchoredPosition.y - best.rect.height * 0.5f,
            Is.GreaterThanOrEqualTo(notice.anchoredPosition.y + notice.rect.height * 0.5f + 10f));
        view.SetReviveOffer(true);
        RectTransform revive = (RectTransform)content.Find("ReviveBrush");
        RectTransform main = (RectTransform)content.Find("RetryBrush");
        Assert.That(notice.anchoredPosition.y - notice.rect.height * 0.5f,
            Is.GreaterThanOrEqualTo(revive.anchoredPosition.y + revive.rect.height * 0.5f + 10f));
        Assert.That(revive.anchoredPosition.y - revive.rect.height * 0.5f,
            Is.GreaterThanOrEqualTo(main.anchoredPosition.y + main.rect.height * 0.5f + 12f));
        Assert.That(main.anchoredPosition.y - main.rect.height * 0.5f,
            Is.GreaterThanOrEqualTo(-510f));
        AssertResultCenterline();
    }

    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void LegacyResultsWithoutDistanceDataKeepActionsAndNoExtraLedger(bool reviveAvailable, bool settlementPending)
    {
        view.Show(new GameOverResult(132, 200, false, 2, 8, true),
            reviveAvailable, settlementPending);
        AssertNoLedgerOrNotice();
        Assert.That(content.Find("ReviveBrush").gameObject.activeSelf,
            Is.EqualTo(reviveAvailable || settlementPending));
        if (reviveAvailable || settlementPending)
        {
            view.SetReviveRequestInFlight(true);
            AssertNoLedgerOrNotice();
        }
        view.Hide();
        view.Show(new GameOverResult(182, 200, false, 3, 9, true));
        AssertNoLedgerOrNotice();
        Assert.That(content.Find("RetryBrush").GetComponent<Button>().interactable, Is.True);
    }

    [Test]
    public void SuccessfulSaveClearsEarlierRecoveryNotice()
    {
        view.RefreshResult(new GameOverResult(132, 132, false, 0, 8, true,
            growthRewardSaved: false));
        Assert.That(TextAt("SaveNotice").gameObject.activeSelf, Is.True);
        view.RefreshResult(new GameOverResult(132, 132, false, 2, 10, true));
        AssertNoLedgerOrNotice();
    }

    void AssertNoLedgerOrNotice()
    {
        Assert.That(content.Find("PermanentGrowthReward"), Is.Null);
        Assert.That(TextAt("SaveNotice").gameObject.activeSelf, Is.False);
        Assert.That(view.SaveNoticeLabel, Is.Empty);
        foreach (Text text in content.GetComponentsInChildren<Text>())
        {
            foreach (string removed in new[] { "정산 보류", "이번 도전", "누적", "보유", "광고를 보면" })
                Assert.That(text.text, Does.Not.Contain(removed));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GrowthGaugeCountsAcrossThresholdAndFinishesAtExactCarry(bool preview)
    {
        var result = GrowthResult(380, 625, 1, preview);
        Invoke("BindGrowthProgress", result, true);
        var fill = content.Find("GrowthProgress/Fill").GetComponent<Image>();
        Assert.That(fill.fillAmount, Is.EqualTo(.2f).Within(.001f));
        Assert.That(TextAt("GrowthProgress/Added").text, Is.EqualTo("+0m"));
        bool sawFull = false, sawPartial = false;
        for (int i = 0; i < 65; i++)
        {
            Invoke("AdvanceGrowthProgress", .05f);
            sawFull |= fill.fillAmount > .999f;
            sawPartial |= fill.fillAmount > .77f && fill.fillAmount < .99f;
        }
        Assert.That(sawFull && sawPartial, Is.True);
        Assert.That(fill.fillAmount, Is.EqualTo(125f / 150f).Within(.001f));
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("125 / 150m"));
        Assert.That(TextAt("GrowthProgress/Added").text, Is.EqualTo("+245m"));
        Assert.That(TextAt("GrowthProgress/Earned").text, Is.EqualTo(preview ? "예상 먹빛 +1" : "먹빛 +1"));
        Assert.That(TextAt("GrowthProgress/Earned").rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(content.Find("GrowthProgress/InkTip").GetComponent<Image>().enabled, Is.False);
    }

    [Test]
    public void RefreshAndConfirmationDoNotRestartTheSameProgressAnimation()
    {
        Invoke("BindGrowthProgress", GrowthResult(380, 625, 1, true), true);
        Invoke("AdvanceGrowthProgress", .2f);
        string displayed = TextAt("GrowthProgress/Added").text;
        Invoke("BindGrowthProgress", GrowthResult(380, 625, 1, true), false);
        Assert.That(TextAt("GrowthProgress/Added").text, Is.EqualTo(displayed));
        Invoke("FinishGrowthProgress");
        Invoke("BindGrowthProgress", GrowthResult(380, 625, 1, false), false);
        Assert.That(TextAt("GrowthProgress/Added").text, Is.EqualTo("+245m"));
        Assert.That(TextAt("GrowthProgress/Earned").text, Is.EqualTo("먹빛 +1"));
    }

    [Test]
    public void GrowthGaugeRespectsReducedMotionAndLargeRunsStayBounded()
    {
        Invoke("BindGrowthProgress", GrowthResult(0, 112149, 243, false), true);
        for (int i = 0; i < 80; i++) Invoke("AdvanceGrowthProgress", .05f);
        Assert.That(TextAt("GrowthProgress/Earned").text, Is.EqualTo("먹빛 +243"));
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("499 / 500m"));
        Assert.That(content.Find("GrowthProgress/InkTip").GetComponent<Image>().enabled, Is.False);
        LobbySettingsProfile.SetReducedMotionEnabled(true);
        Invoke("BindGrowthProgress", GrowthResult(100, 300, 2, false), true);
        Assert.That(TextAt("GrowthProgress/Added").text, Is.EqualTo("+200m"));
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("50 / 100m"));
        Assert.That(content.Find("GrowthProgress/InkTip").GetComponent<Image>().enabled, Is.False);
    }

    [Test]
    public void FinalMilestoneStaysFullAndSaveFailuresDoNotShowEarnedRewards()
    {
        var complete = new GameOverResult(200, 200, false, 1, 244, true,
            cumulativeGrowthDistanceMeters: 112250, previousGrowthRewardDistanceMeters: 112150,
            nextGrowthRewardDistanceMeters: 112150, growthDistanceJourneyComplete: true,
            growthDistanceBeforeMeters: 112050);
        Invoke("BindGrowthProgress", complete, true);
        Invoke("FinishGrowthProgress");
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("500 / 500m"));
        view.RefreshResult(new GameOverResult(200, 200, false, 0, 0, true, growthRewardSaved: false));
        Assert.That(content.Find("GrowthProgress").gameObject.activeSelf, Is.False);
        Assert.That(TextAt("SaveNotice").gameObject.activeSelf, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GrowthGaugeFitsBetweenRecordsAndActionsInBothLanguages(bool english)
    {
        GameLocalization.SetLanguage(english ? GameLanguage.English : GameLanguage.Korean);
        view.RefreshResult(GrowthResult(380, 625, 1, true));
        Invoke("FinishGrowthProgress");
        view.SetReviveOffer(true);
        foreach (string path in new[] { "GrowthProgress/Caption", "GrowthProgress/Added", "GrowthProgress/Earned" })
            AssertFits(TextAt(path), 1);
        AssertFits(TextAt("GrowthProgress/Progress"), 1, FontStyle.Normal);
        var growth = (RectTransform)content.Find("GrowthProgress");
        var best = (RectTransform)content.Find("BestResult");
        var revive = (RectTransform)content.Find("ReviveBrush");
        Assert.That(best.anchoredPosition.y - best.rect.height * .5f,
            Is.GreaterThanOrEqualTo(growth.anchoredPosition.y + growth.rect.height * .5f + 8f));
        Assert.That(growth.anchoredPosition.y - growth.rect.height * .5f,
            Is.GreaterThanOrEqualTo(revive.anchoredPosition.y + revive.rect.height * .5f + 8f));
        foreach (Graphic graphic in growth.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
    }

    [TestCase(50L, "0 / 100m")]
    [TestCase(350L, "0 / 150m")]
    [TestCase(1550L, "0 / 250m")]
    [TestCase(4550L, "0 / 350m")]
    [TestCase(10150L, "0 / 500m")]
    public void GrowthGaugeSwitchesToTheNextTierAtItsExactBoundary(long total, string expected)
    {
        Invoke("BindGrowthProgress", GrowthResult(0, total, RunRewardCalculator.GetRewardCountForDistance(total), false), true);
        Invoke("FinishGrowthProgress");
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo(expected));
        Assert.That(content.Find("GrowthProgress/Fill").GetComponent<Image>().fillAmount, Is.Zero);
    }

    [Test]
    public void GrowthGaugeKeepsMigratedAccountProgressWithoutConsultingLiveProfile()
    {
        var result = new GameOverResult(125, 125, false, 1, 8, true,
            cumulativeGrowthDistanceMeters: 400, previousGrowthRewardDistanceMeters: 400,
            nextGrowthRewardDistanceMeters: 550, growthDistanceBeforeMeters: 275,
            growthDistanceRewardOffsetMeters: 700);
        Invoke("BindGrowthProgress", result, true);
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("25 / 150m"));
        Invoke("FinishGrowthProgress");
        Assert.That(TextAt("GrowthProgress/Progress").text, Is.EqualTo("0 / 150m"));
        Assert.That(TextAt("GrowthProgress/Earned").text, Is.EqualTo("먹빛 +1"));
    }

    static GameOverResult GrowthResult(long before, long total, int earned, bool preview) => new(
        (int)(total - before), (int)(total - before), false, preview ? 0 : earned, 8, true,
        cumulativeGrowthDistanceMeters: total,
        previousGrowthRewardDistanceMeters: RunRewardCalculator.GetPreviousRewardDistance(RunRewardCalculator.GetRewardCountForDistance(total)),
        nextGrowthRewardDistanceMeters: RunRewardCalculator.GetNextRewardDistance(RunRewardCalculator.GetRewardCountForDistance(total)),
        growthDistanceBeforeMeters: before, isGrowthPreview: preview, previewGrowthCurrency: earned);

    void AssertResultCenterline()
    {
        foreach (string path in new[] { "Title", "CurrentResult/Caption", "CurrentResult/Value",
                     "BestResult/Caption", "BestResult/Value", "SaveNotice" })
        {
            Text text = TextAt(path);
            Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter), path);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, text.rectTransform);
            Assert.That(bounds.center.x, Is.Zero.Within(0.01f), path);
        }
    }

    [Test]
    public void OpenResultPaperAndRollRemainStillEvenWithLegacyFlutterArguments()
    {
        Invoke("ApplyRevealPose", 1f, false);
        int objects = host.GetComponentsInChildren<Transform>(true).Length;
        Vector3 contentPosition = content.localPosition;
        var top = (RectTransform)panel.Find("TopRoll");
        Vector2 topPosition = top.anchoredPosition;
        var bottom = (RectTransform)panel.Find("BottomRoll");
        Vector2 bottomPosition = bottom.anchoredPosition;
        Invoke("ApplyPaperPose", 1f, 1.7f, true);
        Assert.That(bottom.anchoredPosition, Is.EqualTo(bottomPosition));
        for (int i = 0; i < 120; i++)
        {
            Invoke("ApplyPaperPose", 1f, i * 30f, true);
            Assert.That(top.anchoredPosition, Is.EqualTo(topPosition));
            Assert.That(bottom.anchoredPosition, Is.EqualTo(bottomPosition));
            Assert.That(bottom.localEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(content.localPosition, Is.EqualTo(contentPosition));
            Assert.That(content.localScale, Is.EqualTo(Vector3.one));
        }
        Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        Invoke("ApplyPaperPose", 1f, 17f, false);
        Assert.That(bottom.anchoredPosition, Is.EqualTo(bottomPosition));
        Assert.That(bottom.localEulerAngles, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FoldedPaperBlocksHiddenButtonsUntilThePaperIsOpen()
    {
        var group = content.GetComponent<CanvasGroup>();
        Invoke("ApplyRevealPose", 0f, false);
        Assert.That(panel.Find("TopRoll").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        Assert.That(group.interactable, Is.False);
        Assert.That(group.blocksRaycasts, Is.False);
        Invoke("ApplyRevealPose", 1f, false);
        Assert.That(group.interactable, Is.True);
        Assert.That(group.blocksRaycasts, Is.True);
        Assert.That(group.alpha, Is.EqualTo(1f));
    }

    [TestCase(0f)]
    [TestCase(0.2f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void PaperMeshRevealsOriginalUvFromTopWithoutStretching(float opening)
    {
        var paper = panel.Find("ScrollBody/ScrollPaper").GetComponent<HanjiScrollPaperGraphic>();
        paper.SetPose(opening, 0f, 0f);
        using var vertices = new VertexHelper();
        typeof(HanjiScrollPaperGraphic).GetMethod("OnPopulateMesh",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Invoke(paper, new object[] { vertices });
        Assert.That(vertices.currentVertCount, Is.LessThanOrEqualTo(600));
        var top = new UIVertex();
        var bottom = new UIVertex();
        vertices.PopulateUIVertex(ref top, HanjiScrollPaperGraphic.Columns / 2);
        vertices.PopulateUIVertex(ref bottom,
            HanjiScrollPaperGraphic.Rows * (HanjiScrollPaperGraphic.Columns + 1) +
            HanjiScrollPaperGraphic.Columns / 2);
        Assert.That(top.position.y, Is.EqualTo(550f).Within(0.001f));
        Assert.That(top.uv0.y, Is.EqualTo(1f));
        Assert.That(bottom.uv0.y, Is.EqualTo(1f - paper.RevealedFraction).Within(0.0001f));
        Assert.That(top.position.y - bottom.position.y,
            Is.EqualTo(1100f * paper.RevealedFraction).Within(0.01f));
        Assert.That(paper.transform.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void CanvasRendererReceivesVisiblePaperMeshNotTheLegacyEmptyMesh()
    {
        var paper = panel.Find("ScrollBody/ScrollPaper").GetComponent<HanjiScrollPaperGraphic>();
        Assert.That(paper.canvasRenderer, Is.Not.Null, "커스텀 Graphic은 CanvasRenderer를 직접 요구해야 한다.");
        paper.SetPose(1f, 1.2f, 1f);
        paper.canvasRenderer.cull = false;
        paper.SetVerticesDirty();
        paper.Rebuild(CanvasUpdate.PreRender);
        Mesh rendered = paper.canvasRenderer.GetMesh();
        try
        {
            Assert.That(rendered, Is.Not.Null);
            Assert.That(rendered.vertexCount, Is.EqualTo(
                (HanjiScrollPaperGraphic.Columns + 1) * (HanjiScrollPaperGraphic.Rows + 1)));
            Assert.That(rendered.bounds.size.y, Is.GreaterThan(1090f));
            Assert.That(rendered.colors32[HanjiScrollPaperGraphic.Columns / 2].a, Is.EqualTo(255));
        }
        finally { Object.DestroyImmediate(rendered); }
    }

    [Test]
    public void EmphasizedResultTextUsesOpaqueBoldWithoutBlurredShadow()
    {
        view.SetReviveOffer(true);
        foreach (string path in new[] { "Title", "CurrentResult/Caption", "CurrentResult/Value",
                     "BestResult/Caption", "BestResult/Value", "ReviveBrush/Label", "RetryBrush/TouchHint",
                     "NewBestSeal/NewBest" })
        {
            Text text = TextAt(path);
            Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold), path);
            Assert.That(text.font, Is.EqualTo(InkPalette.UiFont), path);
            Assert.That(text.color.a, Is.EqualTo(1f), path);
            foreach (Shadow shadow in text.GetComponents<Shadow>())
                Assert.That(shadow.enabled, Is.False, path);
        }
    }

    static void AssertFits(Text text, int maxLines, FontStyle style = FontStyle.Bold)
    {
        Assert.That(text.resizeTextForBestFit, Is.False, text.name);
        Assert.That(text.fontStyle, Is.EqualTo(style), text.name);
        Assert.That(text.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
        var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
        using var generator = new TextGenerator();
        generator.Populate(text.text, settings);
        Assert.That(generator.lineCount, Is.LessThanOrEqualTo(maxLines), text.text);
        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f), text.text);
        if (maxLines == 1)
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 0.5f), text.text);
        // 세로 Truncate가 끝 글자를 숨기는 경우도 선호 크기와 별도로 검출한다.
        // uGUI는 리치 텍스트 태그를 포함한 원문 인덱스로 가시 문자 끝을 반환한다.
        Assert.That(generator.characterCountVisible, Is.EqualTo(text.text.Length), text.text);
    }

    void Invoke(string method, params object[] args) =>
        typeof(GameOverPopupView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, args);
}
