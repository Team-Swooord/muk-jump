using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MukJump.Core;

public class PauseMenuViewTests
{
    GameObject host;

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (host != null)
            Object.DestroyImmediate(host);
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
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        Assert.IsTrue(manager.PauseGame());
        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.IsTrue(manager.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);

        Assert.IsTrue(manager.ResumeGame());
        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.IsFalse(manager.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.AreEqual(0.02f, Time.fixedDeltaTime);
        Assert.IsFalse(AudioListener.pause);
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
}
