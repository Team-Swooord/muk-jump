namespace MukJump.Core
{
    /// 네이티브·토스·에디터 배너가 같은 로비·성장·플레이 노출 규칙을 사용한다.
    public static class GoogleMobileAdsPresentation
    {
        public static bool ShouldShowTopBanner(
            GameManager manager,
            LobbyScreenNavigator navigator,
            LobbyOptionsView options)
        {
            if (manager == null || StartupBrandSplash.IsBlockingInput ||
                BackgroundMusicController.Instance != null &&
                BackgroundMusicController.Instance.IsFullScreenAdActive)
                return false;
            bool bannerSection = navigator == null || navigator.CanShowTopBanner;
            return MonetizationPolicy.ShouldShowTopBanner(manager.State, manager.IsPaused,
                manager.IsTransitioning || navigator != null && navigator.IsTransitioning,
                bannerSection, options != null && options.IsOpen);
        }
    }
}
