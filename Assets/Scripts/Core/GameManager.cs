using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MukJump.Player;
using MukJump.Drawing;

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
        public bool DebugInvincible { get; private set; }

        // 게임오버 직후 오터치로 바로 재시작되는 것을 막는 대기 시간
        [SerializeField] float restartDelay = 0.8f;

        float gameOverTime;
        BrushTransitionView transitionView;
        GameOverPopupView gameOverPopupView;
        bool transitionInProgress;
        readonly List<PlayerController> players = new();

        public int LivingPlayerCount
        {
            get
            {
                CleanupPlayers();
                int count = 0;
                for (int i = 0; i < players.Count; i++)
                    if (!players[i].IsDead) count++;
                return count;
            }
        }

        /// 카메라와 점수는 살아 있는 캐릭터 중 가장 높은 캐릭터를 기준으로 한다.
        public PlayerController HighestLivingPlayer
        {
            get
            {
                CleanupPlayers();
                PlayerController highest = null;
                for (int i = 0; i < players.Count; i++)
                {
                    var candidate = players[i];
                    if (candidate.IsDead) continue;
                    if (highest == null || candidate.transform.position.y > highest.transform.position.y)
                        highest = candidate;
                }
                return highest;
            }
        }

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다 (Awake는 재호출 안 됨)
        void OnEnable()
        {
            Instance = this;
            RefreshPlayerRegistry();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            State = GameState.Lobby;
            // 이전 버전의 Main 씬을 열어도 새 피드백·구간 시스템이 즉시 동작한다.
            if (GetComponent<GameFeedbackController>() == null)
                gameObject.AddComponent<GameFeedbackController>();
            if (GetComponent<HeightZoneController>() == null)
                gameObject.AddComponent<HeightZoneController>();
            if (GetComponent<RestPlatformSpawner>() == null)
                gameObject.AddComponent<RestPlatformSpawner>();
            if (BackgroundMusicController.Instance == null &&
                FindFirstObjectByType<BackgroundMusicController>() == null)
            {
                var musicObject = new GameObject("BackgroundMusic");
                musicObject.AddComponent<BackgroundMusicController>();
            }
            transitionView = GetComponent<BrushTransitionView>();
            if (transitionView == null) transitionView = gameObject.AddComponent<BrushTransitionView>();
            gameOverPopupView = GetComponent<GameOverPopupView>();
            if (gameOverPopupView == null) gameOverPopupView = gameObject.AddComponent<GameOverPopupView>();
            RefreshPlayerRegistry();
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

        public void RegisterPlayer(PlayerController player)
        {
            if (player != null && !players.Contains(player))
                players.Add(player);
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (player != null) players.Remove(player);
        }

        /// 디버그 창에서만 사용하는 무적 모드. 장애물과 화면 하단에서 죽지 않고 되튄다.
        public void ToggleDebugInvincible()
        {
            DebugInvincible = !DebugInvincible;
        }

        /// 한 캐릭터가 죽어도 다른 먹분신이 살아 있으면 게임을 계속한다.
        /// 마지막 캐릭터가 죽었을 때만 true를 반환하고 게임오버로 전환한다.
        public bool NotifyPlayerDied(PlayerController player)
        {
            RegisterPlayer(player);
            if (State != GameState.Playing || LivingPlayerCount > 0)
                return false;

            EnterGameOver();
            return true;
        }

        /// 기존 단일 플레이어 호출부와의 호환용 진입점.
        public void OnPlayerFell()
        {
            EnterGameOver();
        }

        void EnterGameOver()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            var feedback = GameFeedbackController.Instance;
            float revealDelay = feedback != null ? feedback.GameOverRevealDelay : 0.62f;
            feedback?.PlayGameOver();
            gameOverTime = float.PositiveInfinity;
            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            int previousBest = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            bool reachedNewBest = height > previousBest;
            ScoreManager.Instance?.SaveBest();
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : previousBest;
            StartCoroutine(ShowGameOverAfterDeath(revealDelay, height, best, reachedNewBest));
        }

        System.Collections.IEnumerator ShowGameOverAfterDeath(float delay, int height, int best,
            bool reachedNewBest)
        {
            yield return new WaitForSecondsRealtime(delay);
            gameOverPopupView.Show(height, best, reachedNewBest);
            // 팝업이 나타난 뒤 restartDelay 동안은 오터치 재시작을 막는다.
            gameOverTime = Time.unscaledTime;
        }

        /// 먹분신 아이템을 먹을 때마다 생존 캐릭터를 한 마리씩 추가한다.
        public bool TryCreateInkClone(PlayerController source)
        {
            if (State != GameState.Playing || source == null || source.IsDead)
                return false;

            var sourceBody = source.GetComponent<Rigidbody2D>();
            int cloneIndex = Mathf.Max(1, LivingPlayerCount);
            float direction = cloneIndex % 2 == 0 ? -1f : 1f;
            float offset = 0.7f + Mathf.Min(1.2f, cloneIndex * 0.16f);
            Vector3 spawnPosition = source.transform.position + Vector3.right * (direction * offset);
            if (Camera.main != null)
            {
                float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
                spawnPosition.x = Mathf.Clamp(spawnPosition.x, -halfWidth + 0.6f, halfWidth - 0.6f);
            }

            var cloneObject = Instantiate(source.gameObject, spawnPosition, source.transform.rotation);
            cloneObject.name = "Player (먹분신)";
            var clone = cloneObject.GetComponent<PlayerController>();
            if (clone == null)
            {
                Destroy(cloneObject);
                return false;
            }

            var cloneBody = clone.GetComponent<Rigidbody2D>();
            clone.ConfigureAsClone(source.NormalGravityScale);
            if (sourceBody != null && cloneBody != null)
                cloneBody.linearVelocity = sourceBody.linearVelocity +
                                           Vector2.right * (direction * 0.45f);

            CleanupPlayers();
            for (int i = 0; i < players.Count; i++)
                if (players[i] != clone)
                    IgnorePlayerCollision(players[i], clone);
            RegisterPlayer(clone);
            return true;
        }

        /// 디버그 패널에서 고도별 맵과 스폰을 즉시 검증하기 위한 순간이동.
        public void DebugTeleportToHeight(int targetHeight)
        {
            if (State != GameState.Playing) return;
            var primary = HighestLivingPlayer;
            if (primary == null) return;

            int currentHeight = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            float deltaY = Mathf.Max(0, targetHeight) - currentHeight;
            CleanupPlayers();
            for (int i = 0; i < players.Count; i++)
                if (!players[i].IsDead)
                    players[i].DebugTeleportBy(Vector2.up * deltaY);

            primary = HighestLivingPlayer;
            ScoreManager.Instance?.DebugSetHeight(targetHeight, primary != null ? primary.transform : null);
            Camera.main?.GetComponent<CameraFollow>()?.DebugSnapTo(primary != null
                ? primary.transform
                : null);
            RestPlatformSpawner.Instance?.DebugResetSchedule(targetHeight);
        }

        void IgnorePlayerCollision(PlayerController first, PlayerController second)
        {
            var firstColliders = first.GetComponentsInChildren<Collider2D>(true);
            var secondColliders = second.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < firstColliders.Length; i++)
            for (int j = 0; j < secondColliders.Length; j++)
                Physics2D.IgnoreCollision(firstColliders[i], secondColliders[j], true);
        }

        void CleanupPlayers()
        {
            for (int i = players.Count - 1; i >= 0; i--)
                if (players[i] == null) players.RemoveAt(i);
        }

        void RefreshPlayerRegistry()
        {
            var scenePlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < scenePlayers.Length; i++)
                RegisterPlayer(scenePlayers[i]);
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

            var player = HighestLivingPlayer;
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
