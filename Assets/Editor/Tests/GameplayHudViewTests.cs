using System.Reflection;
using NUnit.Framework;
using MukJump.Core;
using UnityEngine;

public class GameplayHudViewTests
{
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

            Assert.IsFalse(controls.activeSelf,
                "Editor/Development Build에서도 DEBUG 창을 사용자 화면에 노출하지 않습니다.");
            Assert.IsFalse(panel.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
