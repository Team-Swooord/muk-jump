using UnityEngine;
using UnityEngine.SceneManagement;

namespace MukJump.Core
{
    public enum GameState
    {
        Lobby,
        Playing,
        GameOver,
    }

    /// 게임 상태(로비/플레이/게임오버)와 시작·재도전 흐름을 관리한다.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Lobby;

        // 게임오버 직후 오터치로 바로 재시작되는 것을 막는 대기 시간
        [SerializeField] float restartDelay = 0.8f;

        float gameOverTime;
        BrushTransitionView transitionView;
        GameOverPopupView gameOverPopupView;
        bool transitionInProgress;

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다 (Awake는 재호출 안 됨)
        void OnEnable()
        {
            Instance = this;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            State = GameState.Lobby;
            transitionView = GetComponent<BrushTransitionView>();
            if (transitionView == null) transitionView = gameObject.AddComponent<BrushTransitionView>();
            gameOverPopupView = GetComponent<GameOverPopupView>();
            if (gameOverPopupView == null) gameOverPopupView = gameObject.AddComponent<GameOverPopupView>();
        }

        void Update()
        {
            if (State == GameState.Lobby)
                return;

            if (State != GameState.GameOver) return;
            if (Time.unscaledTime - gameOverTime < restartDelay) return;

            if (PointerInput.WasPressedThisFrame())
                Restart();
        }

        /// 플레이어가 화면 아래로 추락했을 때 PlayerController가 호출한다.
        public void OnPlayerFell()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            gameOverTime = Time.unscaledTime;
            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            int previousBest = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            bool reachedNewBest = height > previousBest;
            ScoreManager.Instance?.SaveBest();
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : previousBest;
            gameOverPopupView.Show(height, best, reachedNewBest);
        }

        /// 로비 시작선이 완성되면 캐릭터의 고정을 풀고 현재 위치에서 낙하를 시작한다.
        public void StartGameFromStroke()
        {
            if (State != GameState.Lobby || transitionInProgress) return;
            PointerInput.SuppressUntilRelease();
            BeginPlayingAfterCover();
        }

        void BeginPlayingAfterCover()
        {
            if (State != GameState.Lobby) return;

            var player = FindFirstObjectByType<Player.PlayerController>();
            State = GameState.Playing;
            player?.BeginFromLobby();
            if (player != null)
                ScoreManager.Instance?.ResetOrigin(player.transform.position.y);
            PointerInput.SuppressUntilRelease();
        }

        public void Restart()
        {
            if (transitionInProgress) return;
            transitionInProgress = true;
            PointerInput.SuppressUntilRelease();
            transitionView.Play(() =>
            {
                BrushTransitionView.RequestRevealAfterSceneLoad();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }
    }
}
