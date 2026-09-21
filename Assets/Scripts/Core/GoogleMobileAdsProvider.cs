#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace MukJump.Core
{
    /// Google 보상형·전면 광고를 게임 공통 광고 계약으로 변환한다.
    public sealed class GoogleMobileAdsProvider : IFullScreenAdProvider, IFullScreenAdLifecycle, IDisposable
    {
        const float RetryDelaySeconds = 15f;
        const float LoadTimeoutSeconds = 30f;
        const float ShowTimeoutSeconds = 90f;
        // Google의 1시간 만료 전에 새 광고를 준비한다.
        const double CacheLifetimeSeconds = 55 * 60;
        static readonly Action<bool> IgnoreCompletion = _ => { };

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
        double rewardedLoadDeadline;
        double interstitialLoadDeadline;
        double showDeadline;
        double rewardedExpiresAt;
        double interstitialExpiresAt;
        double nextCacheCheckTime;
        double lastTickTime;
        int rewardedFailures;
        int interstitialFailures;
        bool fullScreenOpened;
        long rewardedLoadGeneration;
        long interstitialLoadGeneration;
        long showGeneration;

        public bool HasPendingPresentation => !disposed && pendingCompletion != null;

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
            if (IsRewarded(placement))
            {
                if (rewardedAd == null)
                    return false;
                try
                {
                    if (Time.realtimeSinceStartupAsDouble < rewardedExpiresAt &&
                        rewardedAd.CanShowAd()) return true;
                    SafeDestroy(ref rewardedAd, "만료된 보상형 광고 폐기");
                    nextRewardedLoadTime = 0d;
                    return false;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "먹점프 보상형 광고 준비 확인 실패: " +
                        exception.Message);
                    SafeDestroy(ref rewardedAd, "보상형 광고 폐기");
                    ScheduleRewardedRetry();
                    return false;
                }
            }

            if (interstitialAd == null)
                return false;
            try
            {
                if (Time.realtimeSinceStartupAsDouble < interstitialExpiresAt &&
                    interstitialAd.CanShowAd()) return true;
                SafeDestroy(ref interstitialAd, "만료된 전면 광고 폐기");
                nextInterstitialLoadTime = 0d;
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 전면 광고 준비 확인 실패: " +
                    exception.Message);
                SafeDestroy(ref interstitialAd, "전면 광고 폐기");
                ScheduleInterstitialRetry();
                return false;
            }
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
            double now = Time.realtimeSinceStartupAsDouble;
            // 앱 정지 동안의 벽시계 시간을 표시 실패로 세지 않는다. 복귀 프레임에는
            // 메인 스레드로 지연된 SDK 콜백이 먼저 처리될 여유를 준다.
            if (pendingCompletion != null && !fullScreenOpened)
                showDeadline += Math.Max(0d, now - lastTickTime - .25d);
            lastTickTime = now;
            if (!MobileApplicationLifecycle.IsApplicationActive) return;
            if (pendingCompletion != null &&
                !fullScreenOpened && now >= showDeadline)
            {
                Debug.LogWarning(
                    "먹점프 광고 종료 콜백 대기 시간이 초과되었습니다.");
                CompleteShow(false);
            }
            // 열린 광고에는 시청 길이 제한을 두지 않는다. 닫힘/실패/보상은 SDK가 판정한다.
            if (pendingCompletion != null) return;
            if (now >= nextCacheCheckTime)
            {
                nextCacheCheckTime = now + 1d;
                if (rewardedAd != null) IsReady(FullScreenAdPlacement.GameOverReviveReward);
                if (interstitialAd != null) IsReady(FullScreenAdPlacement.PostRunInterstitial);
            }
            if (rewardedLoading && now >= rewardedLoadDeadline)
            {
                rewardedLoading = false;
                rewardedLoadGeneration++;
                rewardedLoadDeadline = 0d;
                ScheduleRewardedRetry();
                Debug.LogWarning(
                    "먹점프 보상형 광고 로드 시간이 초과되었습니다.");
            }
            if (interstitialLoading && now >= interstitialLoadDeadline)
            {
                interstitialLoading = false;
                interstitialLoadGeneration++;
                interstitialLoadDeadline = 0d;
                ScheduleInterstitialRetry();
                Debug.LogWarning(
                    "먹점프 전면 광고 로드 시간이 초과되었습니다.");
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
                InvokeCompletionSafely(onCompleted, false);
                Preload(placement);
                return;
            }

            showingPlacement = placement;
            pendingCompletion = onCompleted ?? IgnoreCompletion;
            fullScreenOpened = false;
            lastTickTime = Time.realtimeSinceStartupAsDouble;
            long generation = ++showGeneration;
            showDeadline = Time.realtimeSinceStartupAsDouble +
                           ShowTimeoutSeconds;
            if (IsRewarded(placement))
                ShowRewarded(generation);
            else
                ShowInterstitial(generation);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Action<bool> callback = pendingCompletion;
            bool completion = MonetizationPolicy.ResolveFullScreenCompletion(
                showingPlacement,
                rewardEarned,
                nonRewardedCompleted: false);
            pendingCompletion = null;
            showGeneration++;
            rewardedLoadGeneration++;
            interstitialLoadGeneration++;
            showDeadline = 0d;
            InvokeCompletionSafely(callback, completion);

            SafeDestroy(ref rewardedAd, "보상형 광고 폐기");
            SafeDestroy(ref showingRewardedAd, "표시 중 보상형 광고 폐기");
            SafeDestroy(ref interstitialAd, "전면 광고 폐기");
            SafeDestroy(ref showingInterstitialAd, "표시 중 전면 광고 폐기");
        }

        static bool IsRewarded(FullScreenAdPlacement placement)
        {
            return placement != FullScreenAdPlacement.PostRunInterstitial;
        }

        static double RetryDelay(ref int failures)
        {
            // 15 → 30 → 60 → 120초. 통신 실패 중 매 프레임/고정 짧은 간격 요청을 피한다.
            int step = Math.Min(failures, 3);
            failures = Math.Min(failures + 1, 4);
            return RetryDelaySeconds * (1 << step);
        }

        void ScheduleRewardedRetry() => nextRewardedLoadTime =
            Time.realtimeSinceStartupAsDouble + RetryDelay(ref rewardedFailures);

        void ScheduleInterstitialRetry() => nextInterstitialLoadTime =
            Time.realtimeSinceStartupAsDouble + RetryDelay(ref interstitialFailures);

        bool CanPreload => pendingCompletion == null &&
            MobileApplicationLifecycle.IsApplicationActive &&
            Application.internetReachability != NetworkReachability.NotReachable;

        void LoadRewardedIfNeeded()
        {
            if (disposed ||
                !CanPreload ||
                rewardedLoading ||
                rewardedAd != null ||
                string.IsNullOrWhiteSpace(rewardedAdUnitId) ||
                Time.realtimeSinceStartupAsDouble < nextRewardedLoadTime)
                return;

            rewardedLoading = true;
            MukJumpAnalytics.Ad(AnalyticsAdStage.LoadRequested);
            rewardedLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                   LoadTimeoutSeconds;
            long generation = ++rewardedLoadGeneration;
            try
            {
                RewardedAd.Load(
                    rewardedAdUnitId,
                    GoogleMobileAdsRequestFactory.CreateNonPersonalized(),
                    (ad, error) => HandleRewardedLoaded(
                        generation,
                        ad,
                        error));
            }
            catch (Exception exception)
            {
                if (generation != rewardedLoadGeneration)
                    return;
                rewardedLoading = false;
                rewardedLoadGeneration++;
                rewardedLoadDeadline = 0d;
                ScheduleRewardedRetry();
                Debug.LogWarning(
                    "먹점프 보상형 광고 로드 요청 실패: " +
                    exception.Message);
            }
        }

        void LoadInterstitialIfNeeded()
        {
            if (disposed ||
                !CanPreload ||
                interstitialLoading ||
                interstitialAd != null ||
                string.IsNullOrWhiteSpace(interstitialAdUnitId) ||
                Time.realtimeSinceStartupAsDouble < nextInterstitialLoadTime)
                return;

            interstitialLoading = true;
            interstitialLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                       LoadTimeoutSeconds;
            long generation = ++interstitialLoadGeneration;
            try
            {
                InterstitialAd.Load(
                    interstitialAdUnitId,
                    GoogleMobileAdsRequestFactory.CreateNonPersonalized(),
                    (ad, error) => HandleInterstitialLoaded(
                        generation,
                        ad,
                        error));
            }
            catch (Exception exception)
            {
                if (generation != interstitialLoadGeneration)
                    return;
                interstitialLoading = false;
                interstitialLoadGeneration++;
                interstitialLoadDeadline = 0d;
                ScheduleInterstitialRetry();
                Debug.LogWarning(
                    "먹점프 전면 광고 로드 요청 실패: " +
                    exception.Message);
            }
        }

        void HandleRewardedLoaded(
            long generation,
            RewardedAd ad,
            LoadAdError error)
        {
            if (disposed || generation != rewardedLoadGeneration ||
                !rewardedLoading)
            {
                SafeDestroy(ad, "늦은 보상형 광고 폐기");
                return;
            }

            rewardedLoading = false;
            rewardedLoadGeneration++;
            rewardedLoadDeadline = 0d;
            try
            {
                if (error != null || ad == null)
                {
                    MukJumpAnalytics.Ad(AnalyticsAdStage.LoadFailed);
                    ScheduleRewardedRetry();
                    Debug.LogWarning(
                        $"먹점프 보상형 광고 로드 실패: {error}");
                    SafeDestroy(ad, "실패한 보상형 광고 폐기");
                    return;
                }

                SafeDestroy(ref rewardedAd, "이전 보상형 광고 폐기");
                rewardedAd = ad;
                rewardedExpiresAt = Time.realtimeSinceStartupAsDouble + CacheLifetimeSeconds;
                rewardedFailures = 0;
                MukJumpAnalytics.Ad(AnalyticsAdStage.Loaded);
                nextRewardedLoadTime = 0d;
            }
            catch (Exception exception)
            {
                SafeDestroy(ad, "처리 실패한 보상형 광고 폐기");
                if (ReferenceEquals(rewardedAd, ad)) rewardedAd = null;
                ScheduleRewardedRetry();
                Debug.LogWarning(
                    "먹점프 보상형 광고 로드 결과 처리 실패: " +
                    exception.Message);
            }
        }

        void HandleInterstitialLoaded(
            long generation,
            InterstitialAd ad,
            LoadAdError error)
        {
            if (disposed || generation != interstitialLoadGeneration ||
                !interstitialLoading)
            {
                SafeDestroy(ad, "늦은 전면 광고 폐기");
                return;
            }

            interstitialLoading = false;
            interstitialLoadGeneration++;
            interstitialLoadDeadline = 0d;
            try
            {
                if (error != null || ad == null)
                {
                    ScheduleInterstitialRetry();
                    Debug.LogWarning(
                        $"먹점프 전면 광고 로드 실패: {error}");
                    SafeDestroy(ad, "실패한 전면 광고 폐기");
                    return;
                }

                SafeDestroy(ref interstitialAd, "이전 전면 광고 폐기");
                interstitialAd = ad;
                interstitialExpiresAt = Time.realtimeSinceStartupAsDouble + CacheLifetimeSeconds;
                interstitialFailures = 0;
                nextInterstitialLoadTime = 0d;
            }
            catch (Exception exception)
            {
                SafeDestroy(ad, "처리 실패한 전면 광고 폐기");
                if (ReferenceEquals(interstitialAd, ad)) interstitialAd = null;
                ScheduleInterstitialRetry();
                Debug.LogWarning(
                    "먹점프 전면 광고 로드 결과 처리 실패: " +
                    exception.Message);
            }
        }

        void ShowRewarded(long generation)
        {
            showingRewardedAd = rewardedAd;
            rewardedAd = null;
            rewardEarned = false;
            try
            {
                showingRewardedAd.OnAdFullScreenContentOpened += () =>
                {
                    if (generation == showGeneration && pendingCompletion != null)
                    {
                        fullScreenOpened = true;
                        MukJumpAnalytics.Ad(AnalyticsAdStage.Opened);
                    }
                };
                showingRewardedAd.OnAdClicked += () =>
                {
                    if (generation == showGeneration && pendingCompletion != null)
                        MukJumpAnalytics.Ad(AnalyticsAdStage.Clicked);
                };
                showingRewardedAd.OnAdFullScreenContentClosed +=
                    () => HandleRewardedClosed(generation);
                showingRewardedAd.OnAdFullScreenContentFailed +=
                    error => HandleRewardedFailed(generation, error);
                showingRewardedAd.Show(_ =>
                {
                    if (generation == showGeneration &&
                        pendingCompletion != null)
                    {
                        if (!rewardEarned) MukJumpAnalytics.Ad(AnalyticsAdStage.RewardEarned);
                        rewardEarned = true;
                    }
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 보상형 광고 표시 요청 실패: " +
                    exception.Message);
                CompleteShow(generation, false);
            }
        }

        void ShowInterstitial(long generation)
        {
            showingInterstitialAd = interstitialAd;
            interstitialAd = null;
            try
            {
                showingInterstitialAd.OnAdFullScreenContentOpened += () =>
                {
                    if (generation == showGeneration && pendingCompletion != null)
                        fullScreenOpened = true;
                };
                showingInterstitialAd.OnAdFullScreenContentClosed +=
                    () => HandleInterstitialClosed(generation);
                showingInterstitialAd.OnAdFullScreenContentFailed +=
                    error => HandleInterstitialFailed(generation, error);
                showingInterstitialAd.Show();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 전면 광고 표시 요청 실패: " +
                    exception.Message);
                CompleteShow(generation, false);
            }
        }

        void HandleRewardedClosed(long generation)
        {
            CompleteShow(generation, rewardEarned, AnalyticsAdStage.Closed);
        }

        void HandleRewardedFailed(long generation, AdError error)
        {
            Debug.LogWarning($"먹점프 보상형 광고 표시 실패: {error}");
            CompleteShow(generation, false);
        }

        void HandleInterstitialClosed(long generation)
        {
            CompleteShow(generation, true);
        }

        void HandleInterstitialFailed(long generation, AdError error)
        {
            Debug.LogWarning($"먹점프 전면 광고 표시 실패: {error}");
            CompleteShow(generation, false);
        }

        void CompleteShow(bool completed)
        {
            CompleteShow(showGeneration, completed);
        }

        void CompleteShow(long generation, bool completed, AnalyticsAdStage stage = AnalyticsAdStage.Failed)
        {
            if (generation != showGeneration || pendingCompletion == null)
                return;

            FullScreenAdPlacement completedPlacement = showingPlacement;
            if (IsRewarded(completedPlacement))
                MukJumpAnalytics.Ad(stage);
            completed = MonetizationPolicy.ResolveFullScreenCompletion(
                completedPlacement,
                rewardEarned,
                completed);
            // Destroy가 동기 닫힘 콜백을 보내도 같은 보상/요청을 두 번 끝내지 않는다.
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            fullScreenOpened = false;
            showGeneration++;
            showDeadline = 0d;
            if (IsRewarded(completedPlacement))
            {
                SafeDestroy(
                    ref showingRewardedAd,
                    "표시 완료 보상형 광고 폐기");
                nextRewardedLoadTime = 0d;
            }
            else
            {
                SafeDestroy(
                    ref showingInterstitialAd,
                    "표시 완료 전면 광고 폐기");
                nextInterstitialLoadTime = 0d;
            }

            try
            {
                InvokeCompletionSafely(callback, completed);
            }
            finally
            {
                Preload(completedPlacement);
            }
        }

        static void InvokeCompletionSafely(
            Action<bool> callback,
            bool completed)
        {
            if (callback == null)
                return;
            try
            {
                callback.Invoke(completed);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 광고 완료 처리 실패: " + exception.Message);
            }
        }

        static void SafeDestroy(ref RewardedAd ad, string context)
        {
            RewardedAd captured = ad;
            ad = null;
            SafeDestroy(captured, context);
        }

        static void SafeDestroy(RewardedAd ad, string context)
        {
            if (ad == null)
                return;
            try
            {
                ad.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"먹점프 {context} 실패: {exception.Message}");
            }
        }

        static void SafeDestroy(ref InterstitialAd ad, string context)
        {
            InterstitialAd captured = ad;
            ad = null;
            SafeDestroy(captured, context);
        }

        static void SafeDestroy(InterstitialAd ad, string context)
        {
            if (ad == null)
                return;
            try
            {
                ad.Destroy();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"먹점프 {context} 실패: {exception.Message}");
            }
        }
    }
}
#endif
