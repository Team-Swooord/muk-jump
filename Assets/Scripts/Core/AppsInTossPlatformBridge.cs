using System;
using AppsInToss;
using UnityEngine;

namespace MukJump.Core
{
    /// Apps in Toss WebView의 가시성 이벤트를 공통 모바일 생명주기로 전달한다.
    [DefaultExecutionOrder(-990)]
    [DisallowMultipleComponent]
    public sealed class AppsInTossPlatformBridge : MonoBehaviour
    {
        int safeAreaGeneration;
#if UNITY_WEBGL && !UNITY_EDITOR
        const float SafeAreaProbeIntervalSeconds = 2f;
        bool safeAreaRequestInFlight;
        bool safeAreaWarningShown;
        bool platformVisible = true;
        float nextSafeAreaProbeTime;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (FindAnyObjectByType<AppsInTossPlatformBridge>() == null)
                new GameObject(nameof(AppsInTossPlatformBridge))
                    .AddComponent<AppsInTossPlatformBridge>();
#endif
        }

        void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            AITVisibilityHelper.OnVisibilityChanged += HandleVisibilityChanged;
            HandleVisibilityChanged(AITVisibilityHelper.IsVisible);
        }

        void OnDisable()
        {
            AITVisibilityHelper.OnVisibilityChanged -= HandleVisibilityChanged;
            safeAreaGeneration++;
#if UNITY_WEBGL && !UNITY_EDITOR
            safeAreaRequestInFlight = false;
#endif
            MobileUiLayout.ClearPlatformSafeAreaOverride();
        }

        void HandleVisibilityChanged(bool visible)
        {
            MobileApplicationLifecycle.SetPlatformVisibility(visible);
#if UNITY_WEBGL && !UNITY_EDITOR
            // 숨김 직전에 시작한 Web API 응답이 복귀한 새 viewport에 적용되지
            // 않도록 가시성 전환도 별도 세대로 취급한다.
            safeAreaGeneration++;
            safeAreaRequestInFlight = false;
            platformVisible = visible;
            if (visible)
                nextSafeAreaProbeTime = 0f;
            else
                MobileUiLayout.ClearPlatformSafeAreaOverride();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void Update()
        {
            if (!platformVisible || safeAreaRequestInFlight ||
                Time.unscaledTime < nextSafeAreaProbeTime)
                return;

            RefreshSafeArea(safeAreaGeneration);
            nextSafeAreaProbeTime =
                Time.unscaledTime + SafeAreaProbeIntervalSeconds;
        }

        async void RefreshSafeArea(int generation)
        {
            safeAreaRequestInFlight = true;
            int requestedWidth = Screen.width;
            int requestedHeight = Screen.height;
            bool hasRequestedDpr =
                TryReadDevicePixelRatio(out float requestedDpr);
            try
            {
                if (requestedWidth <= 0 || requestedHeight <= 0 ||
                    !hasRequestedDpr)
                {
                    UseUnitySafeAreaFallback(generation);
                    return;
                }

                SafeAreaInsets initial =
                    await AIT.SafeAreaInsetsGet(timeoutMs: 5000);
                if (generation != safeAreaGeneration ||
                    !isActiveAndEnabled)
                    return;
                if (TryApplySafeAreaInsets(
                        initial,
                        generation,
                        requestedWidth,
                        requestedHeight,
                        requestedDpr))
                    safeAreaWarningShown = false;
                else
                    UseUnitySafeAreaFallback(generation);
            }
            catch (Exception)
            {
                UseUnitySafeAreaFallback(generation);
            }
            finally
            {
                if (generation == safeAreaGeneration && this != null)
                    safeAreaRequestInFlight = false;
            }
        }

        bool TryApplySafeAreaInsets(
            SafeAreaInsets insets,
            int generation,
            int requestedWidth,
            int requestedHeight,
            float requestedDpr)
        {
            if (generation != safeAreaGeneration ||
                !isActiveAndEnabled ||
                insets == null ||
                !string.IsNullOrWhiteSpace(insets.error) ||
                Screen.width != requestedWidth ||
                Screen.height != requestedHeight)
                return false;

            if (!TryReadDevicePixelRatio(out float currentDpr) ||
                !Mathf.Approximately(currentDpr, requestedDpr))
                return false;
            Rect safeArea = MobileUiLayout.SafeAreaFromInsets(
                (float)insets.Top * requestedDpr,
                (float)insets.Bottom * requestedDpr,
                (float)insets.Left * requestedDpr,
                (float)insets.Right * requestedDpr,
                requestedWidth,
                requestedHeight);
            MobileUiLayout.SetPlatformSafeAreaOverride(safeArea);
            return true;
        }

        void UseUnitySafeAreaFallback(int generation)
        {
            if (generation != safeAreaGeneration || !isActiveAndEnabled)
                return;

            MobileUiLayout.ClearPlatformSafeAreaOverride();
            if (safeAreaWarningShown)
                return;
            safeAreaWarningShown = true;
            Debug.LogWarning(
                "[MukJump] Apps in Toss Safe Area를 불러오지 못해 Unity 영역을 사용합니다.");
        }

        static bool TryReadDevicePixelRatio(out float devicePixelRatio)
        {
            try
            {
                devicePixelRatio = Mathf.Clamp(
                    (float)AIT.GetDevicePixelRatio(),
                    0.5f,
                    8f);
                return true;
            }
            catch (Exception)
            {
                devicePixelRatio = 1f;
                return false;
            }
        }
#endif
    }
}
