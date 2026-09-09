using UnityEngine;

namespace MukJump.Core
{
    /// Suno로 제작한 배경음악을 씬 전환에도 끊기지 않게 반복 재생한다.
    /// 게임 상태에 따라 음량만 부드럽게 바꿔 효과음과 플레이 피드백을 방해하지 않는다.
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusicController : MonoBehaviour
    {
        const string MusicResourcePath = "MukJump/Audio/InkdropAscent";

        [SerializeField, Range(0f, 1f)] float lobbyVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] float playingVolume = 0.48f;
        [SerializeField, Range(0f, 1f)] float pausedVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] float gameOverVolume = 0.18f;
        [SerializeField, Min(0.01f)] float fadeSpeed = 0.45f;

        public static BackgroundMusicController Instance { get; private set; }
        public bool IsFullScreenAdActive => fullScreenAdActive;

        AudioSource source;
        bool applicationActive = true;
        bool fullScreenAdActive;

        void OnEnable()
        {
            // Play 중 스크립트 재컴파일로 static이 초기화돼도 기존 재생 객체를 복구한다.
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.clip = Resources.Load<AudioClip>(MusicResourcePath);
            source.volume = lobbyVolume * LobbySettingsProfile.BgmVolume;
            applicationActive = MobileApplicationLifecycle.IsApplicationActive;

            if (source.clip == null)
            {
                Debug.LogWarning($"[MukJump] 배경음악을 찾을 수 없습니다: Resources/{MusicResourcePath}");
                return;
            }

            if (applicationActive && !fullScreenAdActive)
                source.Play();
        }

        void Update()
        {
            if (Instance != this || source == null || source.clip == null ||
                !applicationActive || fullScreenAdActive)
                return;

            float stateVolume = GameManager.Instance == null
                ? lobbyVolume
                : GameManager.Instance.IsPaused
                    ? pausedVolume
                    : GameManager.Instance.State switch
                {
                    GameState.Playing => playingVolume,
                    GameState.GameOver => gameOverVolume,
                    _ => lobbyVolume,
                };
            float targetVolume = stateVolume * LobbySettingsProfile.BgmVolume;
            source.volume = Mathf.MoveTowards(source.volume, targetVolume,
                fadeSpeed * Time.unscaledDeltaTime);

            if (!source.isPlaying)
                source.Play();
        }

        public void SetApplicationActive(bool active)
        {
            applicationActive = active;
            ApplyPlaybackState();
        }

        /// 네이티브/호스트 전체 화면 광고는 Unity의 포커스·visibility 콜백을
        /// 항상 발생시키지 않는다. 메뉴 일시정지의 저음량 정책과 분리해 BGM만
        /// 확실히 멈추고, 광고와 앱 비활성 사유가 모두 풀렸을 때만 재개한다.
        public void SetFullScreenAdActive(bool active)
        {
            fullScreenAdActive = active;
            ApplyPlaybackState();
        }

        void ApplyPlaybackState()
        {
            if (source == null || source.clip == null)
                return;
            if (applicationActive && !fullScreenAdActive)
            {
                if (!source.isPlaying)
                {
                    source.UnPause();
                    // 앱이 비활성인 상태에서 최초 생성됐다면 UnPause할 재생
                    // 세션이 없으므로 이때만 처음부터 시작한다.
                    if (!source.isPlaying)
                        source.Play();
                }
            }
            else
            {
                source.Pause();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            lobbyVolume = Mathf.Clamp01(lobbyVolume);
            playingVolume = Mathf.Clamp01(playingVolume);
            pausedVolume = Mathf.Clamp01(pausedVolume);
            gameOverVolume = Mathf.Clamp01(gameOverVolume);
            fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        }
    }
}
