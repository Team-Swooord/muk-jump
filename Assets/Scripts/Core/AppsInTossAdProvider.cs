using System;
using AppsInToss;
using UnityEngine;

namespace MukJump.Core
{
    /// Apps in Toss AdMob의 load → show 이벤트 모델을 게임 공통 광고 경계로 변환한다.
    public sealed class AppsInTossAdProvider : IFullScreenAdProvider, IDisposable
    {
        const float LoadTimeoutSeconds = 30f;
        const float RetryDelaySeconds = 15f;
        const float ShowTimeoutSeconds = 90f;

        readonly string rewardedAdGroupId;
        readonly string interstitialAdGroupId;

        Action rewardedLoadDisposer;
        Action interstitialLoadDisposer;
        Action showDisposer;
        Action<bool> pendingCompletion;
        FullScreenAdPlacement showingPlacement;
        bool rewardedReady;
        bool interstitialReady;
        bool rewardEarned;
        bool rewardedLoading;
        bool interstitialLoading;
        bool disposed;
        double rewardedLoadDeadline;
        double interstitialLoadDeadline;
        double nextRewardedLoadTime;
        double nextInterstitialLoadTime;
        double showDeadline;

        public AppsInTossAdProvider(
            string rewardedAdGroupId,
            string interstitialAdGroupId)
        {
            this.rewardedAdGroupId = rewardedAdGroupId?.Trim();
            this.interstitialAdGroupId = interstitialAdGroupId?.Trim();
        }

        public bool IsReady(FullScreenAdPlacement placement)
        {
            if (disposed || pendingCompletion != null)
                return false;
            return IsRewarded(placement) ? rewardedReady : interstitialReady;
        }

        public void Tick()
        {
            if (disposed)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (pendingCompletion != null && now >= showDeadline)
            {
                Debug.LogWarning(
                    "먹점프 토스 광고 종료 콜백 대기 시간이 초과되었습니다.");
                CompleteShow(false);
            }

            if (rewardedLoading && now >= rewardedLoadDeadline)
                CancelTimedOutLoad(true);
            if (interstitialLoading && now >= interstitialLoadDeadline)
                CancelTimedOutLoad(false);

            if (!rewardedReady && !rewardedLoading &&
                now >= nextRewardedLoadTime)
                Preload(FullScreenAdPlacement.GameOverReviveReward);
            if (!string.IsNullOrWhiteSpace(interstitialAdGroupId) &&
                !interstitialReady && !interstitialLoading &&
                now >= nextInterstitialLoadTime)
                Preload(FullScreenAdPlacement.PostRunInterstitial);
        }

        public void Preload(FullScreenAdPlacement placement)
        {
            if (disposed)
                return;
            bool rewarded = IsRewarded(placement);
            string adGroupId = rewarded
                ? rewardedAdGroupId
                : interstitialAdGroupId;
            if (string.IsNullOrWhiteSpace(adGroupId) ||
                (rewarded
                    ? rewardedReady || rewardedLoading ||
                      Time.realtimeSinceStartupAsDouble < nextRewardedLoadTime
                    : interstitialReady || interstitialLoading ||
                      Time.realtimeSinceStartupAsDouble < nextInterstitialLoadTime))
                return;

            if (rewarded)
            {
                rewardedLoading = true;
                rewardedLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                       LoadTimeoutSeconds;
                rewardedLoadDisposer?.Invoke();
                rewardedLoadDisposer =
                    AIT.GoogleAdMobLoadAppsInTossAdMob(
                        result => HandleLoadEvent(true, result),
                        new LoadAdMobOptions { AdGroupId = adGroupId },
                        _ => HandleLoadError(true));
            }
            else
            {
                interstitialLoading = true;
                interstitialLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                           LoadTimeoutSeconds;
                interstitialLoadDisposer?.Invoke();
                interstitialLoadDisposer =
                    AIT.GoogleAdMobLoadAppsInTossAdMob(
                        result => HandleLoadEvent(false, result),
                        new LoadAdMobOptions { AdGroupId = adGroupId },
                        _ => HandleLoadError(false));
            }
        }

        public void Show(
            FullScreenAdPlacement placement,
            Action<bool> onCompleted)
        {
            if (!IsReady(placement) || pendingCompletion != null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            bool rewarded = IsRewarded(placement);
            string adGroupId = rewarded
                ? rewardedAdGroupId
                : interstitialAdGroupId;
            if (rewarded)
                rewardedReady = false;
            else
                interstitialReady = false;

            showingPlacement = placement;
            rewardEarned = false;
            pendingCompletion = onCompleted;
            showDeadline = Time.realtimeSinceStartupAsDouble +
                           ShowTimeoutSeconds;
            showDisposer?.Invoke();
            showDisposer = AIT.GoogleAdMobShowAppsInTossAdMob(
                HandleShowEvent,
                new ShowAdMobOptions { AdGroupId = adGroupId },
                _ => CompleteShow(false));
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            rewardedLoadDisposer?.Invoke();
            interstitialLoadDisposer?.Invoke();
            showDisposer?.Invoke();
            rewardedLoadDisposer = null;
            interstitialLoadDisposer = null;
            showDisposer = null;
            rewardedReady = false;
            interstitialReady = false;
            rewardedLoading = false;
            interstitialLoading = false;
            showDeadline = 0d;
            callback?.Invoke(false);
        }

        static bool IsRewarded(FullScreenAdPlacement placement)
        {
            return placement != FullScreenAdPlacement.PostRunInterstitial;
        }

        void HandleLoadEvent(bool rewarded, LoadAdMobEvent result)
        {
            if (result?.Type != "loaded")
                return;
            if (rewarded)
            {
                rewardedLoading = false;
                rewardedReady = true;
                rewardedLoadDeadline = 0d;
                nextRewardedLoadTime = 0d;
            }
            else
            {
                interstitialLoading = false;
                interstitialReady = true;
                interstitialLoadDeadline = 0d;
                nextInterstitialLoadTime = 0d;
            }
        }

        void HandleLoadError(bool rewarded)
        {
            if (rewarded)
            {
                rewardedLoading = false;
                rewardedReady = false;
                rewardedLoadDeadline = 0d;
                nextRewardedLoadTime = Time.realtimeSinceStartupAsDouble +
                                       RetryDelaySeconds;
            }
            else
            {
                interstitialLoading = false;
                interstitialReady = false;
                interstitialLoadDeadline = 0d;
                nextInterstitialLoadTime = Time.realtimeSinceStartupAsDouble +
                                           RetryDelaySeconds;
            }
        }

        void CancelTimedOutLoad(bool rewarded)
        {
            Debug.LogWarning(rewarded
                ? "먹점프 토스 보상형 광고 로드 시간이 초과되었습니다."
                : "먹점프 토스 전면 광고 로드 시간이 초과되었습니다.");
            if (rewarded)
            {
                rewardedLoadDisposer?.Invoke();
                rewardedLoadDisposer = null;
            }
            else
            {
                interstitialLoadDisposer?.Invoke();
                interstitialLoadDisposer = null;
            }
            HandleLoadError(rewarded);
        }

        void HandleShowEvent(ShowAdMobEvent result)
        {
            switch (result?.Type)
            {
                case "userEarnedReward":
                    rewardEarned = true;
                    break;
                case "dismissed":
                    CompleteShow(
                        IsRewarded(showingPlacement) ? rewardEarned : true);
                    break;
                case "failedToShow":
                    CompleteShow(false);
                    break;
            }
        }

        void CompleteShow(bool completed)
        {
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            showDeadline = 0d;
            showDisposer?.Invoke();
            showDisposer = null;
            callback?.Invoke(completed);
            Preload(showingPlacement);
        }
    }
}
