using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MukJump.Core
{
    public enum GameState
    {
        Playing,
        GameOver,
    }

    /// 게임 상태(플레이/게임오버)와 재도전 흐름을 관리한다.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Playing;

        // 게임오버 직후 오터치로 바로 재시작되는 것을 막는 대기 시간
        [SerializeField] float restartDelay = 0.8f;

        float gameOverTime;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
        }

        void Update()
        {
            if (State != GameState.GameOver) return;
            if (Time.unscaledTime - gameOverTime < restartDelay) return;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
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

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
