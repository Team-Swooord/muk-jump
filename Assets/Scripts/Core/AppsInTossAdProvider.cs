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
        static readonly Action<bool> IgnoreCompletion = _ => { };

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
        long rewardedLoadGeneration;
        long interstitialLoadGeneration;
        long showGeneration;

#if UNITY_EDITOR
        Func<string, Action<bool>, Action, Action> loadRequestForTests;
        Func<string, Action<string>, Action, Action> showRequestForTests;
#endif

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

            long generation;
            if (rewarded)
            {
                generation = ++rewardedLoadGeneration;
                rewardedLoading = true;
                rewardedLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                       LoadTimeoutSeconds;
                SafeInvokeDisposer(
                    ref rewardedLoadDisposer,
                    "이전 보상형 로드 구독 해제");
            }
            else
            {
                generation = ++interstitialLoadGeneration;
                interstitialLoading = true;
                interstitialLoadDeadline = Time.realtimeSinceStartupAsDouble +
                                           LoadTimeoutSeconds;
                SafeInvokeDisposer(
                    ref interstitialLoadDisposer,
                    "이전 전면 로드 구독 해제");
            }

            try
            {
                Action disposer = RequestLoad(
                    adGroupId,
                    loaded => HandleLoadEvent(rewarded, generation, loaded),
                    () => HandleLoadError(rewarded, generation));
                if (IsCurrentLoad(rewarded, generation))
                {
                    if (rewarded)
                        rewardedLoadDisposer = disposer;
                    else
                        interstitialLoadDisposer = disposer;
                }
                else
                {
                    SafeInvokeDisposer(disposer, "완료된 광고 로드 구독 해제");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    (rewarded
                        ? "먹점프 토스 보상형 광고 로드 요청 실패: "
                        : "먹점프 토스 전면 광고 로드 요청 실패: ") +
                    exception.Message);
                HandleLoadError(rewarded, generation);
            }
        }

        public void Show(
            FullScreenAdPlacement placement,
            Action<bool> onCompleted)
        {
            if (!IsReady(placement) || pendingCompletion != null)
            {
                InvokeCompletionSafely(onCompleted, false);
                Preload(placement);
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
            // null 콜백도 진행 중 표시가 필요하다. null 자체를 상태 sentinel로
            // 쓰면 SDK 종료 이벤트와 watchdog이 모두 무시되어 광고가 고착된다.
            pendingCompletion = onCompleted ?? IgnoreCompletion;
            showDeadline = Time.realtimeSinceStartupAsDouble +
                           ShowTimeoutSeconds;
            long generation = ++showGeneration;
            SafeInvokeDisposer(ref showDisposer, "이전 표시 구독 해제");
            try
            {
                Action disposer = RequestShow(
                    adGroupId,
                    eventType => HandleShowEvent(generation, eventType),
                    () => CompleteShow(generation, false));
                if (generation == showGeneration && pendingCompletion != null)
                    showDisposer = disposer;
                else
                    SafeInvokeDisposer(disposer, "완료된 표시 구독 해제");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 광고 표시 요청 실패: " + exception.Message);
                CompleteShow(generation, false);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Action<bool> callback = pendingCompletion;
            bool completion = MonetizationPolicy.ResolveFullScreenCompletion(
                showingPlacement,
                rewardEarned,
                nonRewardedCompleted: false);
            pendingCompletion = null;
            rewardedLoadGeneration++;
            interstitialLoadGeneration++;
            showGeneration++;
            SafeInvokeDisposer(ref rewardedLoadDisposer, "보상형 로드 구독 해제");
            SafeInvokeDisposer(ref interstitialLoadDisposer, "전면 로드 구독 해제");
            SafeInvokeDisposer(ref showDisposer, "표시 구독 해제");
            rewardedReady = false;
            interstitialReady = false;
            rewardedLoading = false;
            interstitialLoading = false;
            showDeadline = 0d;
            InvokeCompletionSafely(callback, completion);
        }

        static bool IsRewarded(FullScreenAdPlacement placement)
        {
            return placement != FullScreenAdPlacement.PostRunInterstitial;
        }

        bool IsCurrentLoad(bool rewarded, long generation)
        {
            return !disposed && (rewarded
                ? rewardedLoading && generation == rewardedLoadGeneration
                : interstitialLoading &&
                  generation == interstitialLoadGeneration);
        }

        Action RequestLoad(
            string adGroupId,
            Action<bool> onLoaded,
            Action onError)
        {
#if UNITY_EDITOR
            if (loadRequestForTests != null)
                return loadRequestForTests(adGroupId, onLoaded, onError);
#endif
            return AIT.GoogleAdMobLoadAppsInTossAdMob(
                result => onLoaded?.Invoke(result?.Type == "loaded"),
                new LoadAdMobOptions { AdGroupId = adGroupId },
                _ => onError?.Invoke());
        }

        Action RequestShow(
            string adGroupId,
            Action<string> onEvent,
            Action onError)
        {
#if UNITY_EDITOR
            if (showRequestForTests != null)
                return showRequestForTests(adGroupId, onEvent, onError);
#endif
            return AIT.GoogleAdMobShowAppsInTossAdMob(
                result => onEvent?.Invoke(result?.Type),
                new ShowAdMobOptions { AdGroupId = adGroupId },
                _ => onError?.Invoke());
        }

        static void SafeInvokeDisposer(ref Action disposer, string context)
        {
            Action captured = disposer;
            disposer = null;
            SafeInvokeDisposer(captured, context);
        }

        static void SafeInvokeDisposer(Action disposer, string context)
        {
            if (disposer == null)
                return;
            try
            {
                disposer.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"먹점프 토스 광고 {context} 실패: {exception.Message}");
            }
        }

        static void InvokeCompletionSafely(Action<bool> callback, bool result)
        {
            if (callback == null)
                return;
            try
            {
                callback.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "먹점프 토스 광고 완료 처리 실패: " + exception.Message);
            }
        }

        void HandleLoadEvent(bool rewarded, long generation, bool loaded)
        {
            if (!loaded || !IsCurrentLoad(rewarded, generation))
                return;
            if (rewarded)
            {
                rewardedLoadGeneration++;
                rewardedLoading = false;
                rewardedReady = true;
                rewardedLoadDeadline = 0d;
                nextRewardedLoadTime = 0d;
                SafeInvokeDisposer(
                    ref rewardedLoadDisposer,
                    "보상형 로드 구독 해제");
            }
            else
            {
                interstitialLoadGeneration++;
                interstitialLoading = false;
                interstitialReady = true;
                interstitialLoadDeadline = 0d;
                nextInterstitialLoadTime = 0d;
                SafeInvokeDisposer(
                    ref interstitialLoadDisposer,
                    "전면 로드 구독 해제");
            }
        }

        void HandleLoadError(bool rewarded, long generation)
        {
            if (!IsCurrentLoad(rewarded, generation))
                return;
            if (rewarded)
            {
                rewardedLoadGeneration++;
                rewardedLoading = false;
                rewardedReady = false;
                rewardedLoadDeadline = 0d;
                nextRewardedLoadTime = Time.realtimeSinceStartupAsDouble +
                                       RetryDelaySeconds;
                SafeInvokeDisposer(
                    ref rewardedLoadDisposer,
                    "실패한 보상형 로드 구독 해제");
            }
            else
            {
                interstitialLoadGeneration++;
                interstitialLoading = false;
                interstitialReady = false;
                interstitialLoadDeadline = 0d;
                nextInterstitialLoadTime = Time.realtimeSinceStartupAsDouble +
                                           RetryDelaySeconds;
                SafeInvokeDisposer(
                    ref interstitialLoadDisposer,
                    "실패한 전면 로드 구독 해제");
            }
        }

        void CancelTimedOutLoad(bool rewarded)
        {
            Debug.LogWarning(rewarded
                ? "먹점프 토스 보상형 광고 로드 시간이 초과되었습니다."
                : "먹점프 토스 전면 광고 로드 시간이 초과되었습니다.");
            long generation = rewarded
                ? rewardedLoadGeneration
                : interstitialLoadGeneration;
            HandleLoadError(rewarded, generation);
        }

        void HandleShowEvent(long generation, string eventType)
        {
            if (generation != showGeneration || pendingCompletion == null)
                return;
            switch (eventType)
            {
                case "userEarnedReward":
                    rewardEarned = true;
                    break;
                case "dismissed":
                    CompleteShow(
                        generation,
                        IsRewarded(showingPlacement) ? rewardEarned : true);
                    break;
                case "failedToShow":
                    CompleteShow(generation, false);
                    break;
            }
        }

        void CompleteShow(bool completed)
        {
            CompleteShow(showGeneration, completed);
        }

        void CompleteShow(long generation, bool completed)
        {
            if (generation != showGeneration || pendingCompletion == null)
                return;
            completed = MonetizationPolicy.ResolveFullScreenCompletion(
                showingPlacement,
                rewardEarned,
                completed);
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            showGeneration++;
            showDeadline = 0d;
            SafeInvokeDisposer(ref showDisposer, "표시 완료 구독 해제");
            try
            {
                InvokeCompletionSafely(callback, completed);
            }
            finally
            {
                Preload(showingPlacement);
            }
        }
    }
}
