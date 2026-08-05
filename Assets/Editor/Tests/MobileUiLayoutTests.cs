using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class MobileUiLayoutTests
    {
        [Test]
        public void InvalidSafeAreaFallsBackToFullScreen()
        {
            Rect safe = MobileUiLayout.SanitizeSafeArea(
                Rect.zero,
                1080,
                2400);

            Assert.That(safe, Is.EqualTo(new Rect(0f, 0f, 1080f, 2400f)));
        }

        [Test]
        public void AndroidGestureInsetsProduceStableNormalizedAnchors()
        {
            var host = new GameObject("SafeAreaHost", typeof(RectTransform));
            try
            {
                RectTransform rect = host.GetComponent<RectTransform>();
                Rect safe = new Rect(0f, 96f, 1080f, 2208f);

                MobileUiLayout.ApplySafeArea(rect, safe, 1080, 2400);

                Assert.That(rect.anchorMin.x, Is.Zero.Within(0.0001f));
                Assert.That(rect.anchorMin.y, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(rect.anchorMax.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(rect.anchorMax.y, Is.EqualTo(0.96f).Within(0.0001f));
                Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrowIPhoneSafeAreaFitsOptionsWithoutBestFitText()
        {
            Rect safe = new Rect(0f, 102f, 1179f, 2361f);

            float scale = MobileUiLayout.CalculateFitScale(
                new Vector2(900f, 1510f),
                safe,
                1179,
                2556,
                new Vector2(20f, 20f));
            Vector2 logicalSafe = MobileUiLayout.GetLogicalSafeSize(
                safe,
                1179,
                2556);

            Assert.LessOrEqual(900f * scale + 40f, logicalSafe.x + 0.01f);
            Assert.LessOrEqual(1510f * scale + 40f, logicalSafe.y + 0.01f);
            Assert.That(scale, Is.GreaterThan(0.9f));
        }

        [Test]
        public void VisibleLobbyLogoFitsWithoutShrinkingNormalPortrait()
        {
            Rect safe = new Rect(0f, 0f, 1080f, 1920f);
            Vector2 logicalSafe = MobileUiLayout.GetLogicalSafeSize(
                safe,
                1080,
                1920);
            const float edgePadding = 24f;

            float scale = MobileUiLayout.CalculateVisibleContentFitScale(
                872.88f,
                30.88f,
                12f,
                1f,
                safe,
                1080,
                1920,
                edgePadding);

            float rightEdge = 12f + scale * (30.88f + 872.88f * 0.5f);
            Assert.LessOrEqual(
                rightEdge,
                logicalSafe.x * 0.5f - edgePadding + 0.01f);
            Assert.That(scale, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FoldableFitsAsymmetricLobbyRailAndZoneBanner()
        {
            Rect safe = new Rect(55f, 96f, 970f, 2208f);
            Vector2 logicalSafe = MobileUiLayout.GetLogicalSafeSize(
                safe,
                1080,
                2400);
            const float lobbyRailWidth = (89f + 610.273f * 0.5f) * 2f;

            float lobbyScale = MobileUiLayout.CalculateWidthFitScale(
                lobbyRailWidth,
                safe,
                1080,
                2400,
                24f);
            float bannerScale = MobileUiLayout.CalculateWidthFitScale(
                860f,
                safe,
                1080,
                2400,
                24f);
            float logoScale =
                MobileUiLayout.CalculateVisibleContentFitScale(
                    872.88f,
                    30.88f,
                    12f,
                    lobbyScale,
                    safe,
                    1080,
                    2400,
                    24f);
            float logoRightEdge = lobbyScale *
                (12f + logoScale * (30.88f + 872.88f * 0.5f));

            Assert.LessOrEqual(
                lobbyRailWidth * lobbyScale + 48f,
                logicalSafe.x + 0.01f);
            Assert.LessOrEqual(
                860f * bannerScale + 48f,
                logicalSafe.x + 0.01f);
            Assert.LessOrEqual(
                logoRightEdge,
                logicalSafe.x * 0.5f - 24f + 0.01f);
        }

        [Test]
        public void SideInsetFoldableFitsPauseAndResultPanels()
        {
            Rect safe = new Rect(55f, 96f, 970f, 2208f);
            Vector2 logicalSafe = MobileUiLayout.GetLogicalSafeSize(
                safe,
                1080,
                2400);
            float pauseScale = MobileUiLayout.CalculateFitScale(
                new Vector2(760f, 680f),
                safe,
                1080,
                2400,
                new Vector2(28f, 32f));
            float resultScale = MobileUiLayout.CalculateFitScale(
                new Vector2(800f, 900f),
                safe,
                1080,
                2400,
                new Vector2(28f, 32f));

            Assert.LessOrEqual(760f * pauseScale + 56f, logicalSafe.x + 0.01f);
            Assert.LessOrEqual(800f * resultScale + 56f, logicalSafe.x + 0.01f);
            Assert.LessOrEqual(900f * resultScale + 64f, logicalSafe.y + 0.01f);
        }

        [Test]
        public void AsymmetricCutoutMovesCenteredHudIntoSafeArea()
        {
            Rect safe = new Rect(80f, 0f, 1000f, 2400f);

            Vector2 offset = MobileUiLayout.GetLogicalSafeCenterOffset(
                safe,
                1080,
                2400);

            Assert.That(offset.x, Is.EqualTo(32f).Within(0.001f));
            Assert.That(offset.y, Is.Zero.Within(0.001f));
        }

        [Test]
        public void GuiSafeAreaUsesTopLeftOriginAndKeepsGestureInset()
        {
            Rect guiSafe = MobileUiLayout.ToGuiSafeArea(
                new Rect(0f, 102f, 1179f, 2361f),
                1179,
                2556);

            Assert.That(guiSafe.yMin, Is.EqualTo(93f).Within(0.001f));
            Assert.That(guiSafe.yMax, Is.EqualTo(2454f).Within(0.001f));
            Assert.That(2556f - guiSafe.yMax, Is.EqualTo(102f).Within(0.001f));
        }

        [Test]
        public void TabletKeepsDesignedPanelScale()
        {
            Rect full = new Rect(0f, 0f, 1536f, 2048f);

            float scale = MobileUiLayout.CalculateFitScale(
                new Vector2(800f, 900f),
                full,
                1536,
                2048,
                new Vector2(28f, 32f));

            Assert.That(scale, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
