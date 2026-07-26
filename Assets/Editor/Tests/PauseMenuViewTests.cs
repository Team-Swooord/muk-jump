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

        var panel = overlay.Find("SafeAreaRoot/PauseScroll");
        Assert.IsNotNull(panel);
        var title = panel.Find("Title")?.GetComponent<Text>();
        var resume = panel.Find("ResumeButton")?.GetComponent<Button>();
        var lobby = panel.Find("LobbyButton")?.GetComponent<Button>();
        Assert.IsNotNull(title);
        Assert.IsNotNull(resume);
        Assert.IsNotNull(lobby);
        Assert.GreaterOrEqual(title.fontSize, 60);
        Assert.GreaterOrEqual(
            resume.transform.Find("Label").GetComponent<Text>().fontSize, 36);
        Assert.GreaterOrEqual(
            lobby.transform.Find("Label").GetComponent<Text>().fontSize, 36);
        Assert.IsTrue(resume.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(resume.targetGraphic.raycastTarget);
        Assert.IsTrue(lobby.GetComponent<Graphic>().raycastTarget);
        Assert.IsTrue(lobby.targetGraphic.raycastTarget);
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
}
