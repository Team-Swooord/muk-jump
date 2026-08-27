#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace MukJump.Core
{
    /// Google 보상형·전면 광고를 게임 공통 광고 계약으로 변환한다.
    public sealed class GoogleMobileAdsProvider : IFullScreenAdProvider, IDisposable
    {
        const float RetryDelaySeconds = 15f;
        const float ShowTimeoutSeconds = 90f;

        readonly string rewardedAdUnitId;
        readonly string interstitialAdUnitId;

        RewardedAd rewardedAd;
        RewardedAd showingRewardedAd;
        InterstitialAd interstitialAd;
        InterstitialAd showingInterstitialAd;
        Action<bool> pendingCompletion;
        FullScreenAdPlacement showingPlacement;
        bool rewardedLoading;
        bool interstitialLoading;
        bool rewardEarned;
        bool disposed;
        double nextRewardedLoadTime;
        double nextInterstitialLoadTime;
        double showDeadline;

        public GoogleMobileAdsProvider(
            string rewardedAdUnitId,
            string interstitialAdUnitId)
        {
            this.rewardedAdUnitId = rewardedAdUnitId?.Trim();
            this.interstitialAdUnitId = interstitialAdUnitId?.Trim();
        }

        public bool IsReady(FullScreenAdPlacement placement)
        {
            if (disposed || pendingCompletion != null) return false;
            return IsRewarded(placement)
                ? rewardedAd != null && rewardedAd.CanShowAd()
                : interstitialAd != null && interstitialAd.CanShowAd();
        }

        public void Preload(FullScreenAdPlacement placement)
        {
            if (disposed) return;
            if (IsRewarded(placement))
                LoadRewardedIfNeeded();
            else
                LoadInterstitialIfNeeded();
        }

        public void Tick()
        {
            if (disposed) return;
            if (pendingCompletion != null &&
                Time.realtimeSinceStartupAsDouble >= showDeadline)
            {
                Debug.LogWarning(
                    "먹점프 광고 종료 콜백 대기 시간이 초과되었습니다.");
                CompleteShow(false);
            }
            if (rewardedAd == null &&
                !rewardedLoading &&
                Time.realtimeSinceStartupAsDouble >= nextRewardedLoadTime)
                LoadRewardedIfNeeded();
            if (!string.IsNullOrWhiteSpace(interstitialAdUnitId) &&
                interstitialAd == null &&
                !interstitialLoading &&
                Time.realtimeSinceStartupAsDouble >= nextInterstitialLoadTime)
                LoadInterstitialIfNeeded();
        }

        public void Show(
            FullScreenAdPlacement placement,
            Action<bool> onCompleted)
        {
            if (!IsReady(placement))
            {
                onCompleted?.Invoke(false);
                Preload(placement);
                return;
            }

            showingPlacement = placement;
            pendingCompletion = onCompleted;
            showDeadline = Time.realtimeSinceStartupAsDouble +
                           ShowTimeoutSeconds;
            if (IsRewarded(placement))
                ShowRewarded();
            else
                ShowInterstitial();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            showDeadline = 0d;
            callback?.Invoke(false);

            rewardedAd?.Destroy();
            showingRewardedAd?.Destroy();
            interstitialAd?.Destroy();
            showingInterstitialAd?.Destroy();
            rewardedAd = null;
            showingRewardedAd = null;
            interstitialAd = null;
            showingInterstitialAd = null;
        }

        static bool IsRewarded(FullScreenAdPlacement placement)
        {
            return placement != FullScreenAdPlacement.PostRunInterstitial;
        }

        void LoadRewardedIfNeeded()
        {
            if (disposed ||
                rewardedLoading ||
                rewardedAd != null ||
                string.IsNullOrWhiteSpace(rewardedAdUnitId) ||
                Time.realtimeSinceStartupAsDouble < nextRewardedLoadTime)
                return;

            rewardedLoading = true;
            RewardedAd.Load(
                rewardedAdUnitId,
                GoogleMobileAdsRequestFactory.CreateNonPersonalized(),
                (ad, error) =>
                {
                    rewardedLoading = false;
                    if (disposed)
                    {
                        ad?.Destroy();
                        return;
                    }
                    if (error != null || ad == null)
                    {
                        nextRewardedLoadTime =
                            Time.realtimeSinceStartupAsDouble +
                            RetryDelaySeconds;
                        Debug.LogWarning(
                            $"먹점프 보상형 광고 로드 실패: {error}");
                        return;
                    }

                    rewardedAd?.Destroy();
                    rewardedAd = ad;
                    nextRewardedLoadTime = 0d;
                });
        }

        void LoadInterstitialIfNeeded()
        {
            if (disposed ||
                interstitialLoading ||
                interstitialAd != null ||
                string.IsNullOrWhiteSpace(interstitialAdUnitId) ||
                Time.realtimeSinceStartupAsDouble < nextInterstitialLoadTime)
                return;

            interstitialLoading = true;
            InterstitialAd.Load(
                interstitialAdUnitId,
                GoogleMobileAdsRequestFactory.CreateNonPersonalized(),
                (ad, error) =>
                {
                    interstitialLoading = false;
                    if (disposed)
                    {
                        ad?.Destroy();
                        return;
                    }
                    if (error != null || ad == null)
                    {
                        nextInterstitialLoadTime =
                            Time.realtimeSinceStartupAsDouble +
                            RetryDelaySeconds;
                        Debug.LogWarning(
                            $"먹점프 전면 광고 로드 실패: {error}");
                        return;
                    }

                    interstitialAd?.Destroy();
                    interstitialAd = ad;
                    nextInterstitialLoadTime = 0d;
                });
        }

        void ShowRewarded()
        {
            showingRewardedAd = rewardedAd;
            rewardedAd = null;
            rewardEarned = false;
            showingRewardedAd.OnAdFullScreenContentClosed +=
                HandleRewardedClosed;
            showingRewardedAd.OnAdFullScreenContentFailed +=
                HandleRewardedFailed;
            showingRewardedAd.Show(_ => rewardEarned = true);
        }

        void ShowInterstitial()
        {
            showingInterstitialAd = interstitialAd;
            interstitialAd = null;
            showingInterstitialAd.OnAdFullScreenContentClosed +=
                HandleInterstitialClosed;
            showingInterstitialAd.OnAdFullScreenContentFailed +=
                HandleInterstitialFailed;
            showingInterstitialAd.Show();
        }

        void HandleRewardedClosed()
        {
            CompleteShow(rewardEarned);
        }

        void HandleRewardedFailed(AdError error)
        {
            Debug.LogWarning($"먹점프 보상형 광고 표시 실패: {error}");
            CompleteShow(false);
        }

        void HandleInterstitialClosed()
        {
            CompleteShow(true);
        }

        void HandleInterstitialFailed(AdError error)
        {
            Debug.LogWarning($"먹점프 전면 광고 표시 실패: {error}");
            CompleteShow(false);
        }

        void CompleteShow(bool completed)
        {
            if (pendingCompletion == null) return;

            FullScreenAdPlacement completedPlacement = showingPlacement;
            if (IsRewarded(completedPlacement))
            {
                showingRewardedAd?.Destroy();
                showingRewardedAd = null;
                nextRewardedLoadTime = 0d;
            }
            else
            {
                showingInterstitialAd?.Destroy();
                showingInterstitialAd = null;
                nextInterstitialLoadTime = 0d;
            }

            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            showDeadline = 0d;
            callback?.Invoke(completed);
            Preload(completedPlacement);
        }
    }
}
#endif
