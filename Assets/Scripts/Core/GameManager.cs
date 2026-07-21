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

        // 재도전은 로비를 건너뛰고 바로 플레이로 (씬 리로드를 넘어 유지되도록 static)
        static bool skipLobbyOnce;

        float gameOverTime;

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다 (Awake는 재호출 안 됨)
        void OnEnable()
        {
            Instance = this;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            State = skipLobbyOnce ? GameState.Playing : GameState.Lobby;
            skipLobbyOnce = false;
        }

        void Update()
        {
            if (State == GameState.Lobby)
            {
                if (PointerInput.WasPressedThisFrame())
                    StartGame();
                return;
            }

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
            ScoreManager.Instance?.SaveBest();
        }

        /// 로비 → 플레이 전환: 씬 전환·로딩 없이 캐릭터가 그 자리에서 점프하며 게임이 시작된다
        void StartGame()
        {
            State = GameState.Playing;
            FindFirstObjectByType<Player.AutoJump>()?.JumpNow();
        }

        public void Restart()
        {
            skipLobbyOnce = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
