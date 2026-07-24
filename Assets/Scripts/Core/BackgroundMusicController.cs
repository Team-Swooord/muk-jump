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
        [SerializeField, Range(0f, 1f)] float gameOverVolume = 0.18f;
        [SerializeField, Min(0.01f)] float fadeSpeed = 0.45f;

        public static BackgroundMusicController Instance { get; private set; }

        AudioSource source;

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
            source.volume = lobbyVolume;

            if (source.clip == null)
            {
                Debug.LogWarning($"[MukJump] 배경음악을 찾을 수 없습니다: Resources/{MusicResourcePath}");
                return;
            }

            source.Play();
        }

        void Update()
        {
            if (Instance != this || source == null || source.clip == null) return;

            float targetVolume = GameManager.Instance == null
                ? lobbyVolume
                : GameManager.Instance.State switch
                {
                    GameState.Playing => playingVolume,
                    GameState.GameOver => gameOverVolume,
                    _ => lobbyVolume,
                };
            source.volume = Mathf.MoveTowards(source.volume, targetVolume,
                fadeSpeed * Time.unscaledDeltaTime);

            if (!source.isPlaying)
                source.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            lobbyVolume = Mathf.Clamp01(lobbyVolume);
            playingVolume = Mathf.Clamp01(playingVolume);
            gameOverVolume = Mathf.Clamp01(gameOverVolume);
            fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        }
    }
}
