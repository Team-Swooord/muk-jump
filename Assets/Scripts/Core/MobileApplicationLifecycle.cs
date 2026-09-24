using UnityEngine;

namespace MukJump.Core
{
    /// iOS와 Android의 포커스·백그라운드 전환을 게임 일시정지와 오디오에 연결한다.
    /// 사용자 메뉴가 소유한 일시정지는 앱 복귀 시 임의로 풀지 않는다.
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class MobileApplicationLifecycle : MonoBehaviour
    {
        public static MobileApplicationLifecycle Instance { get; private set; }
        public static bool IsApplicationActive { get; private set; } = true;
        // 숨김 중 프레임이 없더라도 복귀한 UI가 이전 입력·연출을 이어가지 않도록 식별한다.
        public static uint InactiveGeneration { get; private set; }

        static bool platformVisible = true;
        bool applicationPaused;
        bool applicationFocused = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null ||
                FindAnyObjectByType<MobileApplicationLifecycle>() != null)
                return;

            new GameObject(nameof(MobileApplicationLifecycle))
                .AddComponent<MobileApplicationLifecycle>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            applicationPaused = false;
            applicationFocused = true;
            RefreshApplicationState();
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (Application.isEditor)
                return;

            applicationPaused = paused;
            RefreshApplicationState();
        }

        void OnApplicationFocus(bool focused)
        {
            if (Application.isEditor)
                return;

            applicationFocused = focused;
            RefreshApplicationState();
        }

        void RefreshApplicationState()
        {
            SetApplicationActive(ResolveApplicationActive(
                applicationPaused,
                applicationFocused,
                platformVisible));
        }

        /// WebGL 호스트처럼 Unity의 포커스 콜백과 별도인 플랫폼 가시성 이벤트를 받는다.
        public static void SetPlatformVisibility(bool visible)
        {
            platformVisible = visible;
            if (Instance != null)
                Instance.RefreshApplicationState();
            else
                SetApplicationActive(ResolveApplicationActive(
                    false,
                    true,
                    platformVisible));
        }

        public static bool ResolveApplicationActive(
            bool paused,
            bool focused,
            bool visible) =>
            !paused && focused && visible;

#if UNITY_WEBGL || UNITY_EDITOR
        // WebView 광고가 focus 복귀를 누락해도 실제 전경 입력은 명확한 복귀 근거다.
        // JS가 document.hidden=false인 trusted 입력에서만 호출한다. 광고 완료나 사용자 메뉴는 해제하지 않는다.
        [UnityEngine.Scripting.Preserve]
        public void OnWebForegroundInteraction(string visibility)
        {
            if (Instance != this || visibility != "visible") return;
            applicationPaused = false;
            applicationFocused = true;
            platformVisible = true;
            RefreshApplicationState();
            // 이미 dismissed를 받은 요청만 같은 사용자 활성화 안에서 마무리한다.
            GameManager.Instance?.CompletePendingForegroundAd();
        }
#endif

        static void SetApplicationActive(bool active)
        {
            if (IsApplicationActive == active)
                return;

            IsApplicationActive = active;
            if (!active) unchecked { InactiveGeneration++; }
            BackgroundMusicController.Instance?.SetApplicationActive(active);
            if (active)
                GameManager.Instance?.ResumeFromApplicationBackground();
            else
                GameManager.Instance?.PauseForApplicationBackground();
        }
    }
}
