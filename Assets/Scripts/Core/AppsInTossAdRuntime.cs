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
        public const string TestInterstitialId = "ait-ad-test-interstitial-id";
        public const string TestBannerId = "ait-ad-test-banner-id";
        const float DefaultBannerInsetFraction = 0.065f;
        const float BannerRetrySeconds = 20f;

        AppsInTossAdProvider provider;
        string rewardedId, interstitialId;
        string bannerId;
        bool bannerVisible;
        LobbyScreenNavigator lobbyNavigator;
        LobbyOptionsView lobbyOptions;
        float nextBannerRetryAt;
        bool runtimeActive;
        bool bannerLoading;
        bool bannerLoadFailed;
        float bannerLoadDeadline;

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
            rewardedId = ResolveRewardedId(
                settings,
                Debug.isDebugBuild);
            interstitialId = ResolveInterstitialId(
                settings,
                Debug.isDebugBuild);
            bannerId = ResolveBannerId(settings, Debug.isDebugBuild);

            // 현재 출시 흐름은 부활 보상형만 필수다. 전면형 ID가 비어 있어도
            // 배너와 보상형 광고는 각각 독립적으로 동작해야 한다.
            if (string.IsNullOrWhiteSpace(rewardedId) && string.IsNullOrWhiteSpace(bannerId) &&
                string.IsNullOrWhiteSpace(interstitialId))
            {
                LobbyAdLayout.ClearTopInset();
                return;
            }

            RefreshPremiumAdBoundary();
            AITBannerAd.OnAdEvent += HandleBannerEvent;
            AITBannerAd.OnError += HandleBannerError;
        }

        void Update()
        {
            RefreshPremiumAdBoundary();
            if (bannerLoading && Time.realtimeSinceStartup >= bannerLoadDeadline)
                ScheduleBannerRetry();
            if (provider != null)
                provider.DeferLoading = bannerLoading;
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
                // 일부 토스 버전은 배너와 보상형 동시 로드의 콜백을 누락한다.
                if (provider != null && provider.IsLoading)
                    return;
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
                bannerLoading = false;
                bannerVisible = false;
                LobbyAdLayout.MarkBannerHidden();
                TryHideBanner();
            }
        }

        void HandleBannerEvent(AITBannerAdEvent bannerEvent)
        {
            if (!runtimeActive || bannerEvent == null)
                return;
            if (InkPremiumAppearance.SuppressAds)
            {
                bannerLoading = bannerVisible = false;
                LobbyAdLayout.MarkBannerHidden();
                TryHideBanner();
                return;
            }
            if (bannerEvent.Kind == AITBannerAdEventKind.Rendered ||
                bannerEvent.Kind == AITBannerAdEventKind.Viewable)
                bannerLoading = false;
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

        void RefreshPremiumAdBoundary()
        {
            if (InkPremiumAppearance.SuppressAds)
            {
                if (provider != null)
                {
                    if (MonetizationAds.IsRegistered(provider)) MonetizationAds.ResetProvider();
                    provider.Dispose(); provider = null;
                }
                return;
            }
            if (provider == null && (!string.IsNullOrWhiteSpace(rewardedId) || !string.IsNullOrWhiteSpace(interstitialId)))
            {
                provider = new AppsInTossAdProvider(rewardedId, interstitialId);
                MonetizationAds.RegisterProvider(provider);
            }
        }

        void ScheduleBannerRetry()
        {
            bannerLoading = false;
            bannerLoadFailed = true;
            bannerVisible = false;
            LobbyAdLayout.MarkBannerHidden();
            nextBannerRetryAt =
                Time.realtimeSinceStartup + BannerRetrySeconds;
            TryHideBanner();
        }

        void OnDisable()
        {
            bannerLoading = false;
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
                if (MonetizationAds.IsRegistered(provider))
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
            if (InkPremiumAppearance.SuppressAds) return false;
            try
            {
                bannerLoadFailed = false;
                bannerLoading = true;
                bannerLoadDeadline = Time.realtimeSinceStartup + 75f;
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
                return !bannerLoadFailed;
            }
            catch (Exception exception)
            {
                bannerLoading = false;
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
            debugBuild ? TestInterstitialId : value?.InterstitialAdGroupId;

        public static string ResolveBannerId(
            AppsInTossAdSettings value,
            bool debugBuild) =>
            debugBuild ? TestBannerId : value?.BannerAdGroupId;
    }
}
