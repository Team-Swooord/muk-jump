using AppsInToss;
using UnityEngine;

namespace MukJump.Core
{
    /// 개발 WebGL은 로컬 Mock용 ID를 쓰고, 운영 WebGL은 Resources 설정값이 있을 때만 광고를 켠다.
    public sealed class AppsInTossAdRuntime : MonoBehaviour
    {
        const string SettingsResourcePath =
            "MukJump/Settings/AppsInTossAdSettings";
        public const string TestRewardedId = "ait-ad-test-rewarded-id";
        public const string TestBannerId = "ait-ad-test-banner-id";
        const float DefaultBannerInsetFraction = 0.065f;
        const float BannerRetrySeconds = 20f;

        AppsInTossAdProvider provider;
        string bannerId;
        bool bannerVisible;
        LobbyScreenNavigator lobbyNavigator;
        LobbyOptionsView lobbyOptions;
        float nextBannerRetryAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (FindAnyObjectByType<AppsInTossAdRuntime>() == null)
                new GameObject(nameof(AppsInTossAdRuntime))
                    .AddComponent<AppsInTossAdRuntime>();
#endif
        }

        void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            AppsInTossAdSettings settings =
                Resources.Load<AppsInTossAdSettings>(SettingsResourcePath);
            string rewardedId = ResolveRewardedId(
                settings,
                Debug.isDebugBuild);
            string interstitialId = ResolveInterstitialId(
                settings,
                Debug.isDebugBuild);
            bannerId = ResolveBannerId(settings, Debug.isDebugBuild);

            // 현재 출시 흐름은 부활 보상형만 필수다. 전면형 ID가 비어 있어도
            // 배너와 보상형 광고는 각각 독립적으로 동작해야 한다.
            if (string.IsNullOrWhiteSpace(rewardedId))
                return;

            provider = new AppsInTossAdProvider(rewardedId, interstitialId);
            MonetizationAds.RegisterProvider(provider);
            provider.Preload(FullScreenAdPlacement.GameOverReviveReward);
            AITBannerAd.OnAdEvent += HandleBannerEvent;
            AITBannerAd.OnError += HandleBannerError;
        }

        void Update()
        {
            provider?.Tick();
            if (lobbyNavigator == null)
                lobbyNavigator = LobbyScreenNavigator.Instance;
            if (lobbyOptions == null)
                lobbyOptions = FindAnyObjectByType<LobbyOptionsView>();

            bool mainLobbyScreen = lobbyNavigator == null ||
                                   !lobbyNavigator.IsTransitioning &&
                                   lobbyNavigator.CurrentSection ==
                                   LobbyScreenNavigator.LobbySection.Lobby;
            if (lobbyOptions != null && lobbyOptions.IsOpen)
                mainLobbyScreen = false;
            bool shouldShowBanner = !string.IsNullOrWhiteSpace(bannerId) &&
                                    GameManager.Instance != null &&
                                    GameManager.Instance.State == GameState.Lobby &&
                                    mainLobbyScreen &&
                                    Time.realtimeSinceStartup >=
                                    nextBannerRetryAt;
            if (shouldShowBanner == bannerVisible)
                return;

            bannerVisible = shouldShowBanner;
            if (bannerVisible)
            {
                LobbyAdLayout.SetTopInsetFraction(
                    DefaultBannerInsetFraction);
                AITBannerAd.Show(
                    bannerId,
                    AITBannerPosition.Top,
                    AITBannerTheme.Light,
                    AITBannerTone.Grey,
                    AITBannerVariant.Expanded);
            }
            else
            {
                AITBannerAd.Hide();
                LobbyAdLayout.ClearTopInset();
            }
        }

        void HandleBannerEvent(AITBannerAdEvent bannerEvent)
        {
            if (bannerEvent == null)
                return;
            if (bannerEvent.Kind == AITBannerAdEventKind.Resized &&
                bannerEvent.HeightFraction > 0f)
            {
                LobbyAdLayout.SetTopInsetFraction(
                    bannerEvent.HeightFraction);
                return;
            }
            if (bannerEvent.Kind == AITBannerAdEventKind.FailedToRender ||
                bannerEvent.Kind == AITBannerAdEventKind.NoFill ||
                bannerEvent.Kind ==
                AITBannerAdEventKind.InitializationFailed)
                ScheduleBannerRetry();
        }

        void HandleBannerError(string message)
        {
            Debug.LogWarning($"먹점프 토스 배너 오류: {message}");
            ScheduleBannerRetry();
        }

        void ScheduleBannerRetry()
        {
            AITBannerAd.Hide();
            bannerVisible = false;
            LobbyAdLayout.ClearTopInset();
            nextBannerRetryAt =
                Time.realtimeSinceStartup + BannerRetrySeconds;
        }

        void OnDisable()
        {
            if (bannerVisible)
                AITBannerAd.Hide();
            bannerVisible = false;
            AITBannerAd.OnAdEvent -= HandleBannerEvent;
            AITBannerAd.OnError -= HandleBannerError;
            LobbyAdLayout.ClearTopInset();
            if (ReferenceEquals(MonetizationAds.Provider, provider))
                MonetizationAds.ResetProvider();
            provider?.Dispose();
            provider = null;
        }

        public static string ResolveRewardedId(
            AppsInTossAdSettings value,
            bool debugBuild) =>
            debugBuild ? TestRewardedId : value?.RewardedAdGroupId;

        public static string ResolveInterstitialId(
            AppsInTossAdSettings value,
            bool debugBuild) =>
            debugBuild ? string.Empty : value?.InterstitialAdGroupId;

        public static string ResolveBannerId(
            AppsInTossAdSettings value,
            bool debugBuild) =>
            debugBuild ? TestBannerId : value?.BannerAdGroupId;
    }
}
