using System;
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
        bool runtimeActive;

#if UNITY_EDITOR
        Action<string> showBannerForTests;
        Action hideBannerForTests;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LobbyAdLayout.ReserveDefaultTopInset();
            if (FindAnyObjectByType<AppsInTossAdRuntime>() == null)
                new GameObject(nameof(AppsInTossAdRuntime))
                    .AddComponent<AppsInTossAdRuntime>();
#endif
        }

        void OnEnable()
        {
            runtimeActive = true;
            LobbyAdLayout.ReserveDefaultTopInset();
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
            {
                LobbyAdLayout.ClearTopInset();
                return;
            }

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

            bool shouldShowBanner = !string.IsNullOrWhiteSpace(bannerId) &&
                                    GoogleMobileAdsPresentation.ShouldShowTopBanner(
                                        GameManager.Instance, lobbyNavigator, lobbyOptions) &&
                                    Time.realtimeSinceStartup >=
                                    nextBannerRetryAt;
            if (shouldShowBanner == bannerVisible)
                return;

            if (shouldShowBanner)
            {
                // 다시 표시할 때 이미 받은 실제 크기를 기본 예약값으로 덮지 않는다.
                if (LobbyAdLayout.MeasuredTopInsetFraction <= 0f)
                    LobbyAdLayout.SetTopInsetFraction(DefaultBannerInsetFraction);
                if (TryShowBanner())
                {
                    bannerVisible = true;
                    LobbyAdLayout.MarkBannerVisible();
                }
                else
                {
                    ScheduleBannerRetry();
                }
            }
            else
            {
                bannerVisible = false;
                LobbyAdLayout.MarkBannerHidden();
                TryHideBanner();
            }
        }

        void HandleBannerEvent(AITBannerAdEvent bannerEvent)
        {
            if (!runtimeActive || bannerEvent == null)
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
            if (!runtimeActive)
                return;
            Debug.LogWarning($"먹점프 토스 배너 오류: {message}");
            ScheduleBannerRetry();
        }

        void ScheduleBannerRetry()
        {
            bannerVisible = false;
            LobbyAdLayout.MarkBannerHidden();
            nextBannerRetryAt =
                Time.realtimeSinceStartup + BannerRetrySeconds;
            TryHideBanner();
        }

        void OnDisable()
        {
            runtimeActive = false;
            bannerVisible = false;
            LobbyAdLayout.MarkBannerHidden();
            TryHideBanner();
            try
            {
                AITBannerAd.OnAdEvent -= HandleBannerEvent;
                AITBannerAd.OnError -= HandleBannerError;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 배너 이벤트 해제 실패: " +
                    exception.Message);
            }
            LobbyAdLayout.ClearTopInset();
            try
            {
                if (ReferenceEquals(MonetizationAds.Provider, provider))
                    MonetizationAds.ResetProvider();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 광고 공급자 해제 실패: " +
                    exception.Message);
            }
            try
            {
                provider?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 광고 객체 정리 실패: " +
                    exception.Message);
            }
            provider = null;
        }

        bool TryShowBanner()
        {
            try
            {
#if UNITY_EDITOR
                if (showBannerForTests != null)
                    showBannerForTests.Invoke(bannerId);
                else
#endif
                    AITBannerAd.Show(
                        bannerId,
                        AITBannerPosition.Top,
                        AITBannerTheme.Light,
                        AITBannerTone.Grey,
                        AITBannerVariant.Expanded);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 배너 표시 실패: " + exception.Message);
                return false;
            }
        }

        bool TryHideBanner()
        {
            try
            {
#if UNITY_EDITOR
                if (hideBannerForTests != null)
                    hideBannerForTests.Invoke();
                else
#endif
                    AITBannerAd.Hide();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 배너 숨김 실패: " + exception.Message);
                return false;
            }
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
