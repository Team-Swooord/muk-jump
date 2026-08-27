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
        Action unsubscribeSafeArea;
        int safeAreaGeneration;

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
#if UNITY_WEBGL && !UNITY_EDITOR
            InitializeSafeArea(++safeAreaGeneration);
#endif
        }

        void OnDisable()
        {
            AITVisibilityHelper.OnVisibilityChanged -= HandleVisibilityChanged;
            safeAreaGeneration++;
            try
            {
                unsubscribeSafeArea?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] Apps in Toss Safe Area 구독 해제 실패: " +
                    exception.Message);
            }
            unsubscribeSafeArea = null;
            MobileUiLayout.ClearPlatformSafeAreaOverride();
        }

        static void HandleVisibilityChanged(bool visible)
        {
            MobileApplicationLifecycle.SetPlatformVisibility(visible);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async void InitializeSafeArea(int generation)
        {
            try
            {
                SafeAreaInsets initial =
                    await AIT.SafeAreaInsetsGet(timeoutMs: 5000);
                ApplySafeAreaInsets(initial, generation);

                Action unsubscribe = await AIT.SafeAreaInsetsSubscribe(
                    new SafeAreaInsetsSubscribe__0
                    {
                        OnEvent = insets =>
                            ApplySafeAreaInsets(insets, generation),
                    },
                    timeoutMs: 5000);
                if (generation != safeAreaGeneration || !isActiveAndEnabled)
                {
                    unsubscribe?.Invoke();
                    return;
                }
                unsubscribeSafeArea = unsubscribe;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] Apps in Toss Safe Area를 불러오지 못해 Unity 영역을 사용합니다: " +
                    exception.Message);
            }
        }

        void ApplySafeAreaInsets(
            SafeAreaInsets insets,
            int generation)
        {
            if (generation != safeAreaGeneration ||
                !isActiveAndEnabled ||
                insets == null ||
                !string.IsNullOrWhiteSpace(insets.error) ||
                Screen.width <= 0 ||
                Screen.height <= 0)
                return;

            float devicePixelRatio = Mathf.Clamp(
                (float)AIT.GetDevicePixelRatio(),
                0.5f,
                8f);
            Rect safeArea = MobileUiLayout.SafeAreaFromInsets(
                (float)insets.Top * devicePixelRatio,
                (float)insets.Bottom * devicePixelRatio,
                (float)insets.Left * devicePixelRatio,
                (float)insets.Right * devicePixelRatio,
                Screen.width,
                Screen.height);
            MobileUiLayout.SetPlatformSafeAreaOverride(safeArea);
        }
#endif
    }
}
