namespace MukJump.Core
{
    /// 네이티브 배너와 에디터 테스트 배너가 같은 로비 노출 규칙을 사용한다.
    public static class GoogleMobileAdsPresentation
    {
        public static bool ShouldShowLobbyBanner(
            GameManager manager,
            LobbyScreenNavigator navigator,
            LobbyOptionsView options)
        {
            if (manager == null || manager.State != GameState.Lobby)
                return false;
            if (options != null && options.IsOpen)
                return false;
            return navigator == null ||
                   !navigator.IsTransitioning &&
                   navigator.CurrentSection ==
                   LobbyScreenNavigator.LobbySection.Lobby;
        }
    }
}
