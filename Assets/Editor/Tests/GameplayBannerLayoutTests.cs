using System.IO;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class GameplayBannerLayoutTests
    {
        [SetUp] public void SetUp() => LobbyAdLayout.ClearTopInset();
        [TearDown] public void TearDown() => LobbyAdLayout.ClearTopInset();

        [TestCase(540, 960, 0, 0)]
        [TestCase(1179, 2556, 102, 177)]
        public void GrowthReservesTheActualBannerBelowTheNotch(int width, int height, int bottom, int top)
        {
            var safe = new Rect(0, bottom, width, height - bottom - top);
            LobbyAdLayout.ReserveDefaultTopInset();
            LobbyAdLayout.SetTopInsetFraction(104f / MobileUiLayout.ReferenceHeight);
            Rect content = PermanentGrowthView.CalculateContentSafeArea(safe, width, height);
            Assert.That(content.yMin, Is.EqualTo(safe.yMin));
            Assert.That(content.width, Is.EqualTo(safe.width));
            Assert.That(safe.yMax - content.yMax,
                Is.EqualTo(height * 104f / MobileUiLayout.ReferenceHeight).Within(.001f),
                "노치는 중복 차감하지 않고 배너의 실제 높이만 제외합니다.");
            LobbyAdLayout.MarkBannerHidden();
            Assert.That(PermanentGrowthView.CalculateContentSafeArea(safe, width, height), Is.EqualTo(content),
                "확인 팝업에서 배너를 숨겨도 뒤쪽 성장 화면은 움직이지 않습니다.");
        }

        [TestCase(540, 960, 0, 0, 540, 960)]
        [TestCase(1080, 1920, 0, 0, 1080, 1920)]
        [TestCase(1179, 2556, 0, 102, 1179, 2277)]
        [TestCase(1488, 2266, 0, 48, 1488, 2168)]
        [TestCase(1920, 1080, 80, 20, 1760, 1020)]
        public void HudPauseAndRecordBadgeStayBelowActualBanner(int width, int height,
            float x, float y, float safeWidth, float safeHeight)
        {
            Rect safe = new Rect(x, y, safeWidth, safeHeight);
            Rect original = GameplayHudView.CalculateTopHudRect(safe, width, height);
            LobbyAdLayout.ReserveDefaultTopInset();
            LobbyAdLayout.SetTopInsetFraction(.12f);
            Rect hud = GameplayHudView.CalculateTopHudRect(safe, width, height);
            float adHeight = MobileUiLayout.ReferenceHeight * .12f;
            Assert.That(original.yMax - hud.yMax, Is.EqualTo(adHeight).Within(.01f));
            Vector2 pause = GameplayHudView.CalculatePauseButtonPosition(safe, width, height);
            Assert.That(pause.y, Is.EqualTo(hud.center.y).Within(.01f));
            Assert.That(pause.x, Is.EqualTo(hud.xMin + hud.width * GameplayHudView.PauseSlotAnchorX).Within(.01f));
            float adBottom = -MobileUiLayout.GetLogicalTopInset(safe, width, height) - adHeight;
            Rect band = GameplayHudView.CalculateVisibleHudRect(safe, width, height);
            float gapPixels = (adBottom - band.yMax) * height / MobileUiLayout.ReferenceHeight;
            Assert.That(gapPixels, Is.EqualTo(10f).Within(.001f), "보이는 한지 띠가 실제 배너 바로 아래에 있어야 합니다.");
            float badgeTop = hud.center.y + (NewBestIndicatorView.BadgeCenterOffsetY + 24f)
                * GameplayHudView.CalculateTopHudScale(safe, width, height);
            Assert.That(badgeTop, Is.LessThan(adBottom), "찍기 회전·번짐까지 광고 아래에 남아야 합니다.");
            Rect touch = GameplayHudView.CalculatePauseTouchRect(safe, width, height);
            Assert.That(touch.yMax, Is.LessThanOrEqualTo(adBottom + .001f));
            Assert.That(touch.size, Is.EqualTo(Vector2.one * InkUiStyle.MinimumTapHeight));
            Assert.That(touch.Contains(pause), Is.True);
        }

        [Test]
        public void ActualBannerResizeReclaimsSpaceWithoutMovingLobbyOrLosingNoFillBaseline()
        {
            LobbyAdLayout.ReserveDefaultTopInset();
            Assert.That(LobbyAdLayout.GameplayTopInsetFraction, Is.EqualTo(.08f));
            foreach (float size in new[] { .04f, .12f, .03f })
            {
                LobbyAdLayout.SetTopInsetFraction(size);
                LobbyAdLayout.MarkBannerVisible();
                Assert.That(LobbyAdLayout.GameplayTopInsetFraction, Is.EqualTo(size));
                Assert.That(LobbyAdLayout.TopInsetFraction, Is.EqualTo(.08f));
            }
            LobbyAdLayout.MarkBannerHidden();
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                LobbyAdLayout.SetTopInsetFraction(invalid);
                Assert.That(LobbyAdLayout.GameplayTopInsetFraction, Is.EqualTo(.03f));
                Assert.That(LobbyAdLayout.TopInsetFraction, Is.EqualTo(.08f));
            }
            LobbyAdLayout.ClearTopInset();
            Assert.That(LobbyAdLayout.GameplayTopInsetFraction, Is.Zero);
        }

        [TestCase(540, 960)]
        [TestCase(1080, 1920)]
        [TestCase(1179, 2556)]
        public void EditorBannerUsesItsActualBottomAndReleasesExcessDrawingReservation(int width, int height)
        {
            var safe = new Rect(0, 0, width, height);
            LobbyAdLayout.ReserveDefaultTopInset();
            // 테스트 배너의 높이 96 + 위쪽 간격 8. 아래에 숨은 8 또는 예약 8%를 더하지 않는다.
            LobbyAdLayout.SetTopInsetFraction(104f / MobileUiLayout.ReferenceHeight);
            var band = GameplayHudView.CalculateVisibleHudRect(safe, width, height);
            float actualBannerBottom = height - 104f * height / MobileUiLayout.ReferenceHeight;
            float bandTop = height + band.yMax * height / MobileUiLayout.ReferenceHeight;
            Assert.That(actualBannerBottom - bandTop, Is.EqualTo(10f).Within(.001f));
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(width / 2f, actualBannerBottom + 1f), safe, width, height), Is.True);
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(width / 2f, actualBannerBottom - 1f), safe, width, height), Is.False);
        }

        [Test]
        public void BannerSlotIsNotADrawingSurfaceEvenBeforeDelayedAdLoads()
        {
            var safe = new Rect(0, 60, 1080, 1740);
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(540, 1770), safe, 1080, 1920), Is.False);
            LobbyAdLayout.ReserveDefaultTopInset();
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(540, 1770), safe, 1080, 1920), Is.True);
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(540, 1600), safe, 1080, 1920), Is.False);
            Assert.That(LobbyAdLayout.IsPointerInBannerSlot(new Vector2(-1, 1770), safe, 1080, 1920), Is.False);
        }

        [Test]
        public void AllPlatformsUseTheSameTopBannerPolicyAndKeepTheirTopPlacement()
        {
            string native = File.ReadAllText("Assets/Scripts/Core/GoogleMobileAdsRuntime.cs");
            string toss = File.ReadAllText("Assets/Scripts/Core/AppsInTossAdRuntime.cs");
            string editor = File.ReadAllText("Assets/Scripts/Core/EditorTestAdsRuntime.cs");
            foreach (string source in new[] { native, toss, editor })
                Assert.That(source, Does.Contain("ShouldShowTopBanner("));
            Assert.That(native, Does.Contain("AdPosition.Top"));
            Assert.That(toss, Does.Contain("AITBannerPosition.Top"));
            Assert.That(editor, Does.Contain("new Vector2(0.5f, 1f)"));
        }
    }
}
