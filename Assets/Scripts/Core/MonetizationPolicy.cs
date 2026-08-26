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
