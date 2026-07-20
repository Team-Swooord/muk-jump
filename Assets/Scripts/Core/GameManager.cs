using System;
using UnityEngine;

namespace MukJump.Core
{
    /// <summary>
    /// 게임 전체 상태를 관리하는 싱글턴.
    /// 코어 루프: 관찰 -> 발판 스케치 -> 자동 점프 -> 상승/추락 -> 재도전
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Ready,      // 시작 대기 (타이틀/튜토리얼)
            Playing,    // 플레이 중 (자동 점프 + 드로잉 가능)
            Falling,    // 발판을 놓쳐 추락 중 (입력은 막되 물리는 계속)
            GameOver    // 추락 완료, 점수 확정, 재도전 UI 노출
        }

        [Header("References")]
        [SerializeField] private ScoreManager scoreManager;

        [Header("Fall Detection")]
        [Tooltip("카메라 하단 기준, 이 값만큼 벗어나면 낙사 처리")]
        [SerializeField] private float fallMargin = 2f;
        [SerializeField] private Camera gameCamera;

        public GameState State { get; private set; } = GameState.Ready;

        public event Action<GameState> OnStateChanged;
        public event Action OnGameOver;
        public event Action OnGameReset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void Start()
        {
            SetState(GameState.Ready);
        }

        public void StartGame()
        {
            if (State == GameState.Ready || State == GameState.GameOver)
            {
                scoreManager?.ResetScore();
                OnGameReset?.Invoke();
                SetState(GameState.Playing);
            }
        }

        /// <summary>
        /// 캐릭터가 발판에 착지하지 못하고 낙하를 시작했을 때 PlayerController가 호출.
        /// </summary>
        public void NotifyFallingStarted()
        {
            if (State == GameState.Playing)
            {
                SetState(GameState.Falling);
            }
        }

        /// <summary>
        /// 캐릭터의 현재 높이를 매 프레임 보고받아 점수/낙사 판정에 사용.
        /// PlayerController.Update 등에서 호출.
        /// </summary>
        public void ReportPlayerHeight(float worldY, float lowestVisibleY)
        {
            if (State != GameState.Playing && State != GameState.Falling) return;

            scoreManager?.ReportHeight(worldY);

            if (worldY < lowestVisibleY - fallMargin)
            {
                EndGame();
            }
        }

        public void EndGame()
        {
            if (State == GameState.GameOver) return;
            SetState(GameState.GameOver);
            OnGameOver?.Invoke();
        }

        public void RestartGame()
        {
            StartGame();
        }

        private void SetState(GameState next)
        {
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
