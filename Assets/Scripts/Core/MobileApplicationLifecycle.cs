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

        static void SetApplicationActive(bool active)
        {
            if (IsApplicationActive == active)
                return;

            IsApplicationActive = active;
            BackgroundMusicController.Instance?.SetApplicationActive(active);
            if (active)
                GameManager.Instance?.ResumeFromApplicationBackground();
            else
                GameManager.Instance?.PauseForApplicationBackground();
        }
    }
}
