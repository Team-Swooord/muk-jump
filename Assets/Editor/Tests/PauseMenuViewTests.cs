using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

public class PauseMenuViewTests
{
    GameObject host;
    GameObject playerHost;
    GameObject cameraHost;
    float originalTimeScale;
    float originalFixedDeltaTime;
    bool originalAudioPause;

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        originalAudioPause = AudioListener.pause;
    }

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = originalAudioPause;
        if (!Mathf.Approximately(Time.timeScale, originalTimeScale))
            Time.timeScale = originalTimeScale;
        if (!Mathf.Approximately(Time.fixedDeltaTime, originalFixedDeltaTime))
            Time.fixedDeltaTime = originalFixedDeltaTime;
        if (host != null)
            Object.DestroyImmediate(host);
        if (playerHost != null)
            Object.DestroyImmediate(playerHost);
        if (cameraHost != null)
            Object.DestroyImmediate(cameraHost);
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
        Assert.IsTrue(pauseButton.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(pauseButton.targetGraphic.raycastTarget);

        var overlay = canvasRoot.Find("PauseOverlay");
        Assert.IsNotNull(overlay);
        var overlayGroup = overlay.GetComponent<CanvasGroup>();
        Assert.IsNotNull(overlayGroup);
        Assert.IsFalse(overlayGroup.blocksRaycasts);

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
        Assert.GreaterOrEqual(
            resume.transform.Find("Label").GetComponent<Text>().fontSize, 38);
        Assert.GreaterOrEqual(
            lobby.transform.Find("Label").GetComponent<Text>().fontSize, 32);
        Assert.IsTrue(resume.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(resume.targetGraphic.raycastTarget);
        Assert.IsTrue(lobby.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(lobby.targetGraphic.raycastTarget);
        Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.Greater(title.rectTransform.anchoredPosition.y,
            resumeRect.anchoredPosition.y);
        Assert.Greater(resumeRect.anchoredPosition.y, lobbyRect.anchoredPosition.y);
        Assert.That(resumeRect.sizeDelta, Is.EqualTo(lobbyRect.sizeDelta));
        Assert.GreaterOrEqual(resumeRect.sizeDelta.y, 96f);
        Assert.IsNotNull(panel.Find("ScrollBody/HanjiPaper"));
        var pausePaperCore = panel.Find("ScrollBody/PaperCore")
            ?.GetComponent<Image>();
        Assert.IsNotNull(pausePaperCore);
        Assert.IsNull(pausePaperCore.sprite);
        Assert.GreaterOrEqual(pausePaperCore.rectTransform.sizeDelta.x, 600f);
        Assert.Less(pausePaperCore.rectTransform.sizeDelta.x,
            (panel.Find("ScrollBody") as RectTransform).sizeDelta.x);
        Assert.IsNotNull(panel.Find("TopRoll"));
        Assert.IsNotNull(panel.Find("BottomRoll"));
        Assert.IsNull(panel.Find("PauseSeal"));
        Assert.IsNull(panel.Find("Subtitle"));
        Assert.IsNull(panel.Find("SessionHint"));
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

        var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
        Assert.IsNotNull(rootGroup);
        Assert.That(rootGroup.alpha, Is.Zero);
        Assert.IsFalse(rootGroup.blocksRaycasts);

        var panel = canvasRoot.Find("SafeAreaRoot/ScrollResultPopup")
            as RectTransform;
        Assert.IsNotNull(panel);
        Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.LessOrEqual(panel.sizeDelta.x, 840f);
        Assert.LessOrEqual(panel.sizeDelta.y, 1000f);
        Assert.GreaterOrEqual(panel.sizeDelta.y, 820f);

        var scrollBody = panel.Find("ScrollBody");
        var topRoll = panel.Find("TopRoll");
        var bottomRoll = panel.Find("BottomRoll");
        Assert.IsNotNull(scrollBody);
        Assert.IsNotNull(topRoll);
        Assert.IsNotNull(bottomRoll);
        Assert.IsNotNull(scrollBody.Find("ScrollPaper")?.GetComponent<Image>().sprite);
        var resultPaperCore = scrollBody.Find("PaperCore")?.GetComponent<Image>();
        Assert.IsNotNull(resultPaperCore);
        Assert.IsNull(resultPaperCore.sprite);
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
        var currentCaption = content.Find("CurrentResult/Caption")?.GetComponent<Text>();
        var hint = content.Find("RetryBrush/TouchHint")?.GetComponent<Text>();
        Assert.IsNotNull(title);
        Assert.IsNotNull(currentValue);
        Assert.IsNotNull(bestValue);
        Assert.IsNotNull(currentCaption);
        Assert.IsNotNull(hint);
        Assert.GreaterOrEqual(title.fontSize, 54);
        Assert.Greater(currentValue.fontSize, bestValue.fontSize * 2);
        Assert.Greater(bestValue.fontSize, currentCaption.fontSize);
        Assert.GreaterOrEqual(hint.fontSize, 32);
        var currentResult = content.Find("CurrentResult") as RectTransform;
        var bestResult = content.Find("BestResult") as RectTransform;
        var retry = content.Find("RetryBrush") as RectTransform;
        Assert.Greater(title.rectTransform.anchoredPosition.y,
            currentResult.anchoredPosition.y);
        Assert.Greater(currentResult.anchoredPosition.y,
            bestResult.anchoredPosition.y);
        Assert.Greater(bestResult.anchoredPosition.y,
            retry.anchoredPosition.y);
        Assert.GreaterOrEqual(retry.sizeDelta.x, 560f);
        Assert.GreaterOrEqual(retry.sizeDelta.y, 96f);
        Assert.IsNull(content.Find("CurrentResult")?.GetComponent<Image>());
        Assert.IsNull(content.Find("BestResult")?.GetComponent<Image>());
        Assert.IsNull(content.Find("ResultSeal"));
        Assert.IsNull(content.Find("Subtitle"));
        Assert.IsNull(content.Find("Footer"));
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

        Assert.AreEqual("12.3 km", currentValue?.text);
        Assert.AreEqual("23.5 km", bestValue?.text);
        Assert.IsTrue(newBestSeal != null && newBestSeal.gameObject.activeSelf);

        Invoke(view, "BindResults", -10, 845, false);

        Assert.AreEqual("0 m", currentValue?.text);
        Assert.AreEqual("845 m", bestValue?.text);
        Assert.IsFalse(newBestSeal != null && newBestSeal.gameObject.activeSelf);
    }

    [Test]
    public void GameOverRevealPoseUnrollsSymmetricallyAndSettles()
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
        float closedPanelScale = panel.localScale.x;
        float closedScale = body.localScale.y;
        Assert.GreaterOrEqual(closedPanelScale, 0.94f);
        Assert.Less(closedPanelScale, 1f);
        Assert.Greater(closedScale, 0f);
        Assert.That(topRoll.anchoredPosition.y, Is.Zero);
        Assert.That(bottomRoll.anchoredPosition.y, Is.Zero);

        Invoke(view, "ApplyRevealPose", 0.5f, true);
        float middleScale = body.localScale.y;
        Assert.Greater(middleScale, closedScale);
        Assert.GreaterOrEqual(panel.localScale.x, closedPanelScale);
        Assert.Greater(topRoll.anchoredPosition.y, 0f);
        Assert.Less(bottomRoll.anchoredPosition.y, 0f);
        Assert.That(
            topRoll.anchoredPosition.y,
            Is.EqualTo(-bottomRoll.anchoredPosition.y).Within(0.01f));

        Invoke(view, "ApplyRevealPose", 1f, true);
        Assert.That(body.localScale.y, Is.EqualTo(1f).Within(0.001f));
        Assert.Greater(topRoll.anchoredPosition.y, 250f);
        Assert.That(topRoll.anchoredPosition.y,
            Is.EqualTo(-bottomRoll.anchoredPosition.y).Within(0.01f));
        Assert.That(panel.localScale.x, Is.EqualTo(1f).Within(0.001f));
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
        Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.000001f));
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

    static int CountDirectChildren(Transform parent, string childName)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName)
                count++;
        return count;
    }
}
