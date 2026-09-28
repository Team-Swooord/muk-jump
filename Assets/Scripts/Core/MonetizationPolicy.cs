using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    /// 씬 재시작을 넘어 유지하지만 앱을 새로 켜면 초기화되는 광고 빈도다.
    public sealed class PostRunAdSession
    {
        public static PostRunAdSession Current { get; private set; } = new();
        readonly HashSet<string> completed = new();
        readonly HashSet<string> rewarded = new();
        double lastFullScreenFinished;
        public int CompletedRuns => completed.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => Current = new PostRunAdSession();
        public void MarkRewarded(string runId, double now)
        {
            if (!string.IsNullOrEmpty(runId)) rewarded.Add(runId);
            MarkFullScreen(now);
        }
        public void MarkFullScreen(double now) => lastFullScreenFinished = Math.Max(lastFullScreenFinished, now);
        public bool ConsumeExit(string runId, double now, bool adsSuppressed, bool ready)
        {
            if (string.IsNullOrEmpty(runId) || !completed.Add(runId)) return false;
            // 미준비·구매자·부활 광고 이용 판은 건너뛰고 지연 광고를 남기지 않는다.
            bool eligible = !adsSuppressed && MonetizationPolicy.ShouldShowPostRunInterstitial(
                completed.Count, 0, (float)(now - lastFullScreenFinished), rewarded.Contains(runId), ready);
            if (eligible) MarkFullScreen(now);
            return eligible;
        }
    }

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

    /// 자체 표시 watchdog을 가진 공급자는 게임의 별도 제한 시간이 시청을 중단하지 않게 한다.
    public interface IFullScreenAdLifecycle
    {
        bool HasPendingPresentation { get; }
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

        // 구매자는 기존 보상 조건 안에서만 광고 시청을 생략한다.
        sealed class PremiumAdProvider : IFullScreenAdProvider
        {
            public bool IsReady(FullScreenAdPlacement placement) =>
                InkPremiumAppearance.IsOwned && placement != FullScreenAdPlacement.PostRunInterstitial;
            public void Preload(FullScreenAdPlacement placement) { }
            public void Show(FullScreenAdPlacement placement, Action<bool> onCompleted) =>
                onCompleted?.Invoke(IsReady(placement));
        }
        static readonly IFullScreenAdProvider premium = new PremiumAdProvider();
        static readonly IFullScreenAdProvider unavailable =
            new UnavailableAdProvider();
        static IFullScreenAdProvider provider = unavailable;

        public static IFullScreenAdProvider Provider => InkPremiumAppearance.IsOwned ? premium :
            InkPremiumAppearance.SuppressAds ? unavailable : provider;
        public static bool IsRegistered(IFullScreenAdProvider value) => ReferenceEquals(provider, value);
        public static bool HasProvider => InkPremiumAppearance.IsOwned ||
            !InkPremiumAppearance.SuppressAds && !ReferenceEquals(provider, unavailable);

        public static bool HasPendingPresentation =>
            !InkPremiumAppearance.SuppressAds && provider is IFullScreenAdLifecycle lifecycle && lifecycle.HasPendingPresentation;

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
        public const float MinimumRunDurationForInterstitial = 0f;
        public const float MinimumSecondsBetweenFullScreenAds = 120f;

        /// 로비·성장과 실제 플레이는 같은 상단 배너를 유지한다. 팝업·전환에는 겹치지 않는다.
        public static bool ShouldShowTopBanner(GameState state, bool isPaused,
            bool isTransitioning, bool isBannerSection, bool overlayOpen)
        {
            if (InkPremiumAppearance.SuppressAds || isPaused || isTransitioning || overlayOpen) return false;
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
            return !InkPremiumAppearance.SuppressAds && adReady &&
                   !rewardedAdShownThisRun &&
                   completedRunsInSession >= InterstitialRunInterval &&
                   completedRunsInSession % InterstitialRunInterval == 0 &&
                   runDurationSeconds >= MinimumRunDurationForInterstitial &&
                   secondsSinceLastFullScreenAd >=
                   MinimumSecondsBetweenFullScreenAds;
        }
    }
}
