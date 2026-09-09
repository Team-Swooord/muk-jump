using System;

namespace MukJump.Core
{
    public enum FullScreenAdPlacement
    {
        PreRunShieldReward,
        GameOverReviveReward,
        PostRunInterstitial,
    }

    /// 네이티브 광고 SDK와 Apps in Toss 광고 API가 같은 게임 규칙을 사용하게 하는 경계다.
    /// 공급자가 준비되지 않은 빌드에서는 광고 UI를 노출하지 않는다.
    public interface IFullScreenAdProvider
    {
        bool IsReady(FullScreenAdPlacement placement);
        void Preload(FullScreenAdPlacement placement);
        void Show(FullScreenAdPlacement placement, Action<bool> onCompleted);
    }

    public static class MonetizationAds
    {
        sealed class UnavailableAdProvider : IFullScreenAdProvider
        {
            public bool IsReady(FullScreenAdPlacement placement) => false;
            public void Preload(FullScreenAdPlacement placement) { }

            public void Show(
                FullScreenAdPlacement placement,
                Action<bool> onCompleted)
            {
                onCompleted?.Invoke(false);
            }
        }

        static readonly IFullScreenAdProvider unavailable =
            new UnavailableAdProvider();
        static IFullScreenAdProvider provider = unavailable;

        public static IFullScreenAdProvider Provider => provider;
        public static bool HasProvider => !ReferenceEquals(provider, unavailable);

        /// 지원 플랫폼은 SDK 초기화·동의 화면이 늦어도 게임오버 선택지를 먼저
        /// 유지한다. 공급자 등록 순간만 보고 정산하면 첫 판의 부활 광고가 사라진다.
        public static bool RuntimeProviderExpected
        {
            get
            {
#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL
                return true;
#else
                return false;
#endif
            }
        }

        public static void RegisterProvider(IFullScreenAdProvider value)
        {
            provider = value ?? unavailable;
        }

        public static void ResetProvider()
        {
            provider = unavailable;
        }
    }

    /// 광고 노출 빈도와 보상 조건을 플랫폼 SDK에서 분리해 한 곳에서 고정한다.
    public static class MonetizationPolicy
    {
        public const int InterstitialRunInterval = 3;
        public const float MinimumRunDurationForInterstitial = 45f;
        public const float MinimumSecondsBetweenFullScreenAds = 120f;

        /// 로비·성장과 실제 플레이는 같은 상단 배너를 유지한다. 팝업·전환에는 겹치지 않는다.
        public static bool ShouldShowTopBanner(GameState state, bool isPaused,
            bool isTransitioning, bool isBannerSection, bool overlayOpen)
        {
            if (isPaused || isTransitioning || overlayOpen) return false;
            return state == GameState.Playing || state == GameState.Lobby && isBannerSection;
        }

        /// 첫 세션 첫 판에서만 사용자가 선택해 방어막 1개를 받는 보상형 광고다.
        public static bool CanOfferPreRunShield(
            int completedRunsInSession,
            bool rewardClaimedInSession,
            bool adReady)
        {
            return completedRunsInSession == 0 &&
                   !rewardClaimedInSession &&
                   adReady;
        }

        /// 전멸 뒤 판당 한 번만 원본 먹방울을 1 HP로 되살리는 보상형 광고다.
        public static bool CanOfferGameOverRevive(
            bool isGameOver,
            bool reviveUsedThisRun,
            bool adReady)
        {
            return isGameOver && !reviveUsedThisRun && adReady;
        }

        /// 보상 획득 이벤트는 되돌릴 수 없는 단조 상태다. 공급자의 닫힘 콜백이
        /// 늦거나 누락되어 timeout·dispose·failed가 뒤늦게 와도 획득한 보상을
        /// 실패로 바꾸지 않는다. 전면 광고만 정상 닫힘 여부를 그대로 사용한다.
        public static bool ResolveFullScreenCompletion(
            FullScreenAdPlacement placement,
            bool rewardEarned,
            bool nonRewardedCompleted)
        {
            return placement == FullScreenAdPlacement.PostRunInterstitial
                ? nonRewardedCompleted
                : rewardEarned;
        }

        /// 짧은 실패·보상형 광고 직후에는 전면 광고를 겹쳐 보여주지 않는다.
        public static bool ShouldShowPostRunInterstitial(
            int completedRunsInSession,
            float runDurationSeconds,
            float secondsSinceLastFullScreenAd,
            bool rewardedAdShownThisRun,
            bool adReady)
        {
            return adReady &&
                   !rewardedAdShownThisRun &&
                   completedRunsInSession >= InterstitialRunInterval &&
                   completedRunsInSession % InterstitialRunInterval == 0 &&
                   runDurationSeconds >= MinimumRunDurationForInterstitial &&
                   secondsSinceLastFullScreenAd >=
                   MinimumSecondsBetweenFullScreenAds;
        }
    }
}
