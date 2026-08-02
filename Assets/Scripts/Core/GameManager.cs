using System;
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

    /// Playing 상태를 유지한 채 시간을 멈춘 주체. 서로 다른 일시정지 UI가
    /// 상대의 닫기 입력으로 게임을 재개하지 않도록 소유권을 명시한다.
    public enum GameplayPauseReason
    {
        None,
        UserMenu,
        GrowthChoice,
    }

    /// 게임 상태(로비/플레이/게임오버)와 시작·재도전 흐름을 관리한다.
    public class GameManager : MonoBehaviour
    {
        /// 먹분신은 각각 물리·애니메이션을 가진 실제 목숨이다. 모바일에서 한 판이
        /// 무한히 무거워지지 않으면서도 화면을 먹떼로 채울 수 있는 안전 상한이다.
        public const int MaxLivingPlayers = 24;
        /// 새 분신의 중심이 화면 벽과 겹치지 않게 남기는 월드 여백.
        public const float CloneSpawnHorizontalMargin = 1.1f;
        /// 획득자가 화면 경계에 있어도 새 분신이 즉시 추락하거나 상단에서 잘리지
        /// 않게 하는 월드 여백.
        public const float CloneSpawnVerticalMargin = 1.15f;

        public static GameManager Instance { get; private set; }

        /// 치트성 검증 도구는 에디터와 Development Build에서만 사용할 수 있다.
        public static bool DebugToolsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public GameState State { get; private set; } = GameState.Lobby;
        public bool IsPaused { get; private set; }
        public GameplayPauseReason PauseReason { get; private set; } =
            GameplayPauseReason.None;
        public bool IsTransitioning =>
            transitionInProgress || (transitionView != null && transitionView.IsPlaying);
        /// 게임 규칙·스폰·물리가 한 틱 진행되어도 되는 공통 계약.
        public bool IsGameplayTicking =>
            State == GameState.Playing && !IsPaused && !IsTransitioning;
        public bool DebugInvincible { get; private set; }
        /// 스포너·연출이 GameManager 구현을 직접 폴링하지 않고 세션 경계에 반응하는 계약.
        public event Action<GameState, GameState> StateChanged;
        /// 일시정지는 Playing 상태를 유지해 풀과 세션 예약을 보존하고 별도 계약으로 알린다.
        public event Action<bool> PauseChanged;
        /// 디버그 순간이동 뒤 과거 고도의 스폰 예약을 한 프레임에 소진하지 않게 알린다.
        public event Action<int> WorldHeightTeleported;

        // 게임오버 직후 오터치로 바로 재시작되는 것을 막는 대기 시간
        [SerializeField] float restartDelay = 0.8f;

        float gameOverTime;
        BrushTransitionView transitionView;
        GameOverPopupView gameOverPopupView;
        bool transitionInProgress;
        float timeScaleBeforePause = 1f;
        float fixedDeltaBeforePause = 0.02f;
        float maxSwarmProgressHeight;
        float activeGameplaySeconds;
        [SerializeField, HideInInspector] string currentRunId;
        readonly List<PlayerController> players = new();
        readonly List<PlayerController> swarmScratch =
            new(MaxLivingPlayers);
        int cloneSpawnEnvironmentMask = int.MinValue;
        readonly List<MonoBehaviour> cloneHookBehaviours = new();
        readonly List<IRuntimeCloneLifecycle> cloneHooks = new();

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

        public bool CanCreateInkClone =>
            State == GameState.Playing && LivingPlayerCount < MaxLivingPlayers;

        /// 점수는 선두 기록을 유지하되, 카메라와 난이도 진행은 먹떼의 하위 중앙값을 쓴다.
        /// 소수의 먹물방울 부스트가 나머지 무리를 화면 아래로 밀거나 위험물을 조기 해금하지
        /// 않도록 두 기준을 의도적으로 분리한다.
        public float SwarmProgressHeight
        {
            get
            {
                if (!TryGetSwarmAnchor(out _, out float worldY))
                    return maxSwarmProgressHeight;
                float current = ScoreManager.Instance != null
                    ? Mathf.Max(0f, ScoreManager.Instance.HeightAt(worldY))
                    : Mathf.Max(0f, worldY);
                if (State == GameState.Playing)
                    maxSwarmProgressHeight = Mathf.Max(maxSwarmProgressHeight, current);
                return Mathf.Max(maxSwarmProgressHeight, current);
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
            EnsureLobbyWorldSetup();
            RefreshPlayerRegistry();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            State = GameState.Lobby;
            // 이전 버전의 Main 씬을 열어도 새 피드백·구간 시스템이 즉시 동작한다.
            EnsureLobbyWorldSetup();
            if (GetComponent<VfxRuntimeMonitor>() == null)
                gameObject.AddComponent<VfxRuntimeMonitor>();
            if (GetComponent<GameFeedbackController>() == null)
                gameObject.AddComponent<GameFeedbackController>();
            if (GetComponent<HeightZoneController>() == null)
                gameObject.AddComponent<HeightZoneController>();
            if (GetComponent<WindWeatherController>() == null)
                gameObject.AddComponent<WindWeatherController>();
            if (GetComponent<WindWeatherView>() == null)
                gameObject.AddComponent<WindWeatherView>();
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
            if (GetComponent<PauseMenuView>() == null)
                gameObject.AddComponent<PauseMenuView>();
            if (GetComponent<RunGrowthController>() == null)
                gameObject.AddComponent<RunGrowthController>();
            if (GetComponent<GrowthChoiceView>() == null)
                gameObject.AddComponent<GrowthChoiceView>();
            if (GetComponent<LobbyCollectionView>() == null)
                gameObject.AddComponent<LobbyCollectionView>();
            if (GetComponent<PermanentGrowthView>() == null)
                gameObject.AddComponent<PermanentGrowthView>();
            if (GetComponent<LobbyOptionsView>() == null)
                gameObject.AddComponent<LobbyOptionsView>();
            if (GetComponent<LobbyScreenNavigator>() == null)
                gameObject.AddComponent<LobbyScreenNavigator>();
            if (GetComponent<InkUiFeedbackController>() == null)
                gameObject.AddComponent<InkUiFeedbackController>();
            var eventSystem =
                FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null &&
                eventSystem.GetComponent<UiInputDeviceGuard>() == null)
            {
                eventSystem.gameObject.AddComponent<UiInputDeviceGuard>();
            }
            RefreshPlayerRegistry();
        }

        void EnsureLobbyWorldSetup()
        {
            if (GetComponent<LobbyWorldSetup>() == null)
                gameObject.AddComponent<LobbyWorldSetup>();
        }

        void OnDisable()
        {
            transitionInProgress = false;
            if (Instance != this) return;
            RestorePausedWorld(false);
            Instance = null;
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                // 일시정지·증강 선택·화면 전환 시간을 제외한 실제 조작 가능 시간만
                // 영구 성장 보상 판정에 사용한다.
                if (IsGameplayTicking)
                    activeGameplaySeconds += Time.unscaledDeltaTime;
                return;
            }

            if (State == GameState.Lobby)
                return;

            if (State != GameState.GameOver) return;
            if (Time.unscaledTime - gameOverTime < restartDelay) return;

            if (PointerInput.WasPressedThisFrame())
                Restart();
        }

        public void RegisterPlayer(PlayerController player)
        {
            if (player == null) return;
            if (players.Contains(player)) return;
            ConfigurePlayerCollisionLayer(player);
            players.Add(player);
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (player != null) players.Remove(player);
        }

        /// 바람·카메라 같은 읽기 전용 시스템이 매 프레임 FindObjects 배열을 만들지 않도록
        /// 현재 생존자 목록을 호출자가 재사용하는 버퍼에 채운다.
        public void GetLivingPlayersNonAlloc(List<PlayerController> results)
        {
            if (results == null) return;
            results.Clear();
            CleanupPlayers();
            for (int i = 0; i < players.Count; i++)
                if (!players[i].IsDead)
                    results.Add(players[i]);
        }

        /// 카메라·위험 스케줄이 공유하는 먹떼 진행 기준을 반환한다.
        public bool TryGetSwarmAnchor(
            out PlayerController representative,
            out float worldY)
        {
            GetLivingPlayersNonAlloc(swarmScratch);
            if (swarmScratch.Count == 0)
            {
                representative = null;
                worldY = float.NegativeInfinity;
                return false;
            }

            worldY = ResolveSwarmAnchorY(swarmScratch, out representative);
            return representative != null;
        }

        /// 낮은 순서로 정렬한 뒤 하위 중앙값을 선택한다. 두 마리라면 낮은 개체를,
        /// 24마리라면 12번째 개체를 따라 최소 절반의 무리가 카메라에 남도록 한다.
        public static float ResolveSwarmAnchorY(
            List<PlayerController> living,
            out PlayerController representative)
        {
            if (living == null || living.Count == 0)
            {
                representative = null;
                return float.NegativeInfinity;
            }

            living.Sort(ComparePlayerHeight);
            int anchorIndex = (living.Count - 1) / 2;
            representative = living[anchorIndex];
            return representative != null
                ? representative.transform.position.y
                : float.NegativeInfinity;
        }

        /// 디버그 창에서만 사용하는 무적 모드. 장애물과 화면 하단에서 죽지 않고 되튄다.
        public void ToggleDebugInvincible()
        {
            if (!DebugToolsAvailable) return;
            DebugInvincible = !DebugInvincible;
            if (DebugInvincible)
                ScoreManager.Instance?.InvalidateCurrentRunForRecords();
        }

        /// 기존 Playing 상태를 바꾸지 않고 물리 시간만 멈춰 활성 풀·분신·날씨를 보존한다.
        public bool PauseGame()
        {
            return BeginPause(GameplayPauseReason.UserMenu);
        }

        public bool ResumeGame()
        {
            if (PauseReason != GameplayPauseReason.UserMenu || IsTransitioning)
                return false;
            PointerInput.SuppressUntilRelease();
            RestorePausedWorld(true);
            return true;
        }

        /// 성장 두루마리는 메뉴 일시정지와 같은 시간 정지를 쓰되 별도 소유권을 가진다.
        /// 선택 UI만 이 계약으로 닫을 수 있어 일시정지판과 겹치거나 교차 해제되지 않는다.
        public bool BeginGrowthChoicePause()
        {
            return BeginPause(GameplayPauseReason.GrowthChoice);
        }

        public bool EndGrowthChoicePause()
        {
            if (PauseReason != GameplayPauseReason.GrowthChoice ||
                IsTransitioning)
                return false;
            PointerInput.SuppressUntilRelease();
            RestorePausedWorld(true);
            return true;
        }

        /// 일시정지 화면에서 현재 씬을 다시 불러 로비와 새 세션으로 안전하게 돌아간다.
        public bool ReturnToLobby()
        {
            if (State != GameState.Playing ||
                PauseReason != GameplayPauseReason.UserMenu ||
                IsTransitioning)
                return false;

            transitionInProgress = true;
            PointerInput.SuppressUntilRelease();
            // 붓 전환음은 들리되 물리는 화면이 완전히 덮일 때까지 멈춘 상태를 유지한다.
            AudioListener.pause = false;
            void ReloadLobby()
            {
                RestorePausedWorld(true);
                BrushTransitionView.RequestRevealAfterSceneLoad();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            if (transitionView != null)
                transitionView.Play(ReloadLobby, HandleTransitionFailure);
            else
                ReloadLobby();
            return true;
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

        void EnterGameOver()
        {
            if (State == GameState.GameOver) return;
            SetState(GameState.GameOver);
            var feedback = GameFeedbackController.Instance;
            float revealDelay = feedback != null ? feedback.GameOverRevealDelay : 0.62f;
            feedback?.PlayGameOver();
            gameOverTime = float.PositiveInfinity;
            GameOverResult result = SettleGameOverResult();
            StartCoroutine(ShowGameOverAfterDeath(revealDelay, result));
        }

        /// 결과 표시보다 먼저 최고 기록과 영구 성장 보상을 한 번에 확정한다.
        /// 저장된 run ID가 도메인 리로드 뒤의 중복 호출도 막는다.
        GameOverResult SettleGameOverResult()
        {
            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            int swarmProgressHeight = Mathf.FloorToInt(SwarmProgressHeight);
            int previousBest = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            bool recordsAllowed =
                ScoreManager.Instance == null || ScoreManager.Instance.RecordsAllowed;
            bool reachedNewBest = recordsAllowed &&
                ((ScoreManager.Instance != null && ScoreManager.Instance.IsNewBestThisRun) ||
                 height > previousBest);
            if (string.IsNullOrEmpty(currentRunId))
                currentRunId = Guid.NewGuid().ToString("N");
            bool rewardsAllowed =
                ScoreManager.Instance != null && ScoreManager.Instance.RecordsAllowed;
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    currentRunId,
                    swarmProgressHeight,
                    height,
                    previousBest,
                    activeGameplaySeconds,
                    rewardsAllowed);
            ScoreManager.Instance?.SaveBest();
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : previousBest;
            var result = new GameOverResult(
                height,
                best,
                reachedNewBest,
                settlement.Earned,
                settlement.Balance,
                rewardsAllowed);
            return result;
        }

        System.Collections.IEnumerator ShowGameOverAfterDeath(
            float delay,
            GameOverResult result)
        {
            yield return new WaitForSecondsRealtime(delay);
            gameOverPopupView.Show(result);
            // 팝업이 나타난 뒤 restartDelay 동안은 오터치 재시작을 막는다.
            gameOverTime = Time.unscaledTime;
        }

        /// 먹분신 아이템 하나당 한 마리를 즉시 추가한다.
        public bool TryCreateInkClone(PlayerController source)
        {
            if (!CanCreateInkClone || source == null || source.IsDead)
                return false;

            // 구형 Main 씬도 재생성 없이 같은 분신 연출 계약을 갖도록 첫 획득 때
            // 원본에 한 번만 보조 뷰를 추가한다. 이후 분신은 이 고정 렌더러를 복제·재사용한다.
            if (source.GetComponent<InkCloneArrivalView>() == null)
                source.gameObject.AddComponent<InkCloneArrivalView>();

            var sourceBody = source.GetComponent<Rigidbody2D>();
            int cloneIndex = Mathf.Max(1, LivingPlayerCount);
            Vector3 spawnPosition = FindCloneSpawnPosition(source, cloneIndex);
            float direction = spawnPosition.x < source.transform.position.x ? -1f : 1f;

            // 구체적인 아이템·VFX 타입을 모른 채 복제 생명주기 계약만 호출한다.
            // 각 기능은 동기 Instantiate 동안 게임 상태가 아닌 캐시 자식을 스스로 분리한다.
            cloneHookBehaviours.Clear();
            cloneHooks.Clear();
            source.GetComponents(cloneHookBehaviours);
            for (int i = 0; i < cloneHookBehaviours.Count; i++)
                if (cloneHookBehaviours[i] is IRuntimeCloneLifecycle hook)
                    cloneHooks.Add(hook);

            GameObject cloneObject;
            try
            {
                for (int i = 0; i < cloneHooks.Count; i++)
                    cloneHooks[i].PrepareForRuntimeClone();
                cloneObject = Instantiate(source.gameObject, spawnPosition,
                    source.transform.rotation);
            }
            finally
            {
                for (int i = cloneHooks.Count - 1; i >= 0; i--)
                    cloneHooks[i].RestoreAfterRuntimeClone();
                cloneHooks.Clear();
                cloneHookBehaviours.Clear();
            }
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

            RegisterPlayer(clone);
            RunGrowthController.Instance?.NotifyCloneCreated(source, clone);
            clone.GetComponent<InkCloneArrivalView>()?.Play();
            GameFeedbackController.Instance?.PlayCloneArrival(clone.transform.position);
            return true;
        }

        /// 먹물방울은 한 마리만 화면 밖으로 이탈하지 않도록 현재 먹떼 전체에 같은
        /// 상승 속도를 적용한다. 카메라는 기존 하위 중앙값을 유지해 남은 무리를 버리지 않는다.
        public bool LaunchSwarmInkDrop(PlayerController collector, float height)
        {
            if (!IsGameplayTicking || collector == null || collector.IsDead)
                return false;

            GetLivingPlayersNonAlloc(swarmScratch);
            if (swarmScratch.Count == 0)
                return false;

            for (int i = 0; i < swarmScratch.Count; i++)
                swarmScratch[i].LaunchInkDrop(height, playCameraImpulse: false);

            TryGetSwarmAnchor(out var representative, out _);
            var impulseSource = representative != null ? representative : collector;
            Camera.main?.GetComponent<CameraFollow>()?.PlayJumpImpulse(
                impulseSource.transform,
                Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(25f, 50f, height)));
            return true;
        }

        /// 먹은 캐릭터의 반대쪽 화면 절반 안에서만 후보를 만들고, 그 안에서 현재
        /// 생존자·발판·장애물과 가장 멀리 떨어진 지점을 골라 좌우 규칙과
        /// 24마리 분산을 함께 지킨다.
        Vector3 FindCloneSpawnPosition(PlayerController source, int cloneIndex)
        {
            Vector3 sourcePosition = source.transform.position;
            var worldCamera = Camera.main;
            if (worldCamera == null)
            {
                float fallbackDirection = ResolveOppositeCloneSide(
                    sourcePosition.x, 0f, cloneIndex);
                float fallbackX = Mathf.Abs(sourcePosition.x) > 0.2f
                    ? -sourcePosition.x
                    : fallbackDirection * 0.9f;
                return new Vector3(fallbackX, sourcePosition.y, sourcePosition.z);
            }

            const int CandidateCount = 13;
            const int VerticalCandidateCount = 5;
            const float SpawnClearanceRadius = 0.52f;
            float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float cameraCenterX = worldCamera.transform.position.x;
            float horizontalMargin = Mathf.Min(
                CloneSpawnHorizontalMargin,
                Mathf.Max(0f, halfWidth * 0.45f));
            float usableHalfWidth = Mathf.Max(
                0.1f,
                halfWidth - horizontalMargin);
            float left = cameraCenterX - usableHalfWidth;
            float right = cameraCenterX + usableHalfWidth;
            float halfHeight = worldCamera.orthographicSize;
            float verticalMargin = Mathf.Min(
                CloneSpawnVerticalMargin,
                Mathf.Max(0f, halfHeight * 0.45f));
            float safeBottom =
                worldCamera.transform.position.y - halfHeight + verticalMargin;
            float safeTop =
                worldCamera.transform.position.y + halfHeight - verticalMargin;
            float safeSourceY = Mathf.Clamp(
                sourcePosition.y,
                safeBottom,
                safeTop);
            float direction = ResolveOppositeCloneSide(
                sourcePosition.x, cameraCenterX, cloneIndex);
            float centerGap = Mathf.Min(0.25f, Mathf.Max(0f, (right - left) * 0.08f));
            float sideStart = cameraCenterX + direction * centerGap;
            float sideEnd = direction > 0f ? right : left;
            if ((direction > 0f && sideEnd <= sideStart) ||
                (direction < 0f && sideEnd >= sideStart))
            {
                sideStart = cameraCenterX;
                sideEnd = direction > 0f ? right : left;
            }

            Vector3 best = sourcePosition;
            float bestClearance = float.NegativeInfinity;
            CleanupPlayers();
            if (cloneSpawnEnvironmentMask == int.MinValue)
                cloneSpawnEnvironmentMask =
                    LayerMask.GetMask("Platform", "Obstacle");
            Physics2D.SyncTransforms();

            for (int candidateIndex = 0; candidateIndex < CandidateCount; candidateIndex++)
            {
                float order = (candidateIndex + cloneIndex * 5) % CandidateCount;
                float x = Mathf.Lerp(
                    sideStart, sideEnd, order / (CandidateCount - 1f));
                for (int verticalIndex = 0;
                     verticalIndex < VerticalCandidateCount;
                     verticalIndex++)
                {
                    int centeredIndex = (verticalIndex + 1) / 2;
                    float signedOffset = verticalIndex == 0
                        ? 0f
                        : centeredIndex * 0.58f *
                          (verticalIndex % 2 == 1 ? 1f : -1f);
                    float y = Mathf.Clamp(
                        safeSourceY + signedOffset,
                        safeBottom,
                        safeTop);
                    var candidate = new Vector3(x, y, sourcePosition.z);
                    if (cloneSpawnEnvironmentMask != 0 &&
                        Physics2D.OverlapCircle(
                            candidate,
                            SpawnClearanceRadius,
                            cloneSpawnEnvironmentMask) != null)
                        continue;

                    float nearestSqr = float.PositiveInfinity;
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i] == null || players[i].IsDead) continue;
                        nearestSqr = Mathf.Min(
                            nearestSqr,
                            (players[i].transform.position - candidate).sqrMagnitude);
                    }

                    // 거의 같은 여백이면 원본 가까이에 생겨 획득 연출이 자연스러운 후보를 택한다.
                    float score = nearestSqr -
                                  Mathf.Abs(x - sourcePosition.x) * 0.01f -
                                  Mathf.Abs(y - safeSourceY) * 0.004f;
                    if (score <= bestClearance) continue;
                    bestClearance = score;
                    best = candidate;
                }
            }

            // 화면 절반이 모두 막힌 극단적인 경우에도 카메라 밖이나 수직 경계에는
            // 생성하지 않는다. 생성 직후 유예가 겹친 판정을 한 차례 보호한다.
            if (float.IsNegativeInfinity(bestClearance))
            {
                best = new Vector3(
                    Mathf.Clamp(
                        cameraCenterX +
                        direction * Mathf.Min(0.9f, usableHalfWidth),
                        left,
                        right),
                    safeSourceY,
                    sourcePosition.z);
            }
            return best;
        }

        static float ResolveOppositeCloneSide(
            float sourceX,
            float cameraCenterX,
            int cloneIndex)
        {
            const float CenterEpsilon = 0.05f;
            float offset = sourceX - cameraCenterX;
            if (offset < -CenterEpsilon) return 1f;
            if (offset > CenterEpsilon) return -1f;
            return cloneIndex % 2 == 0 ? -1f : 1f;
        }

        /// 디버그 패널에서 고도별 맵과 스폰을 즉시 검증하기 위한 순간이동.
        public void DebugTeleportToHeight(int targetHeight)
        {
            if (!DebugToolsAvailable || State != GameState.Playing) return;
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
            // DEBUG 맵 왕복은 정상 플레이의 단조 진행 규칙보다 명시적 이동 요청이 우선한다.
            maxSwarmProgressHeight = Mathf.Max(0, targetHeight);
            Camera.main?.GetComponent<CameraFollow>()?.DebugSnapTo(primary != null
                ? primary.transform
                : null);
            RestPlatformSpawner.Instance?.DebugResetSchedule(targetHeight);
            WorldHeightTeleported?.Invoke(Mathf.Max(0, targetHeight));
        }

        static void ConfigurePlayerCollisionLayer(PlayerController player)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0) return;

            player.gameObject.layer = playerLayer;
            var colliders = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].gameObject.layer = playerLayer;

            // 분신마다 모든 기존 캐릭터와 IgnoreCollision 쌍을 추가하면 누적 O(n²)이
            // 된다. 전용 레이어 하나로 같은 캐릭터끼리의 충돌만 전역 차단한다.
            Physics2D.IgnoreLayerCollision(playerLayer, playerLayer, true);
        }

        static int ComparePlayerHeight(PlayerController left, PlayerController right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.transform.position.y.CompareTo(right.transform.position.y);
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

        /// 로비의 명시적인 시작 버튼에서 호출하는 유일한 새 게임 진입점.
        /// 씬 빌더가 준비한 영구 시작 발판 위에서 물리를 풀어 첫 자동 점프를 준비한다.
        public void StartGameFromMenu()
        {
            var navigator = LobbyScreenNavigator.Instance;
            if (navigator == null)
                navigator = GetComponent<LobbyScreenNavigator>();
            if (State != GameState.Lobby ||
                IsTransitioning ||
                navigator != null && !navigator.CanStartGame)
                return;
            PointerInput.SuppressUntilRelease();
            BeginPlayingAfterCover();
        }

        /// 이전 씬·테스트와의 호환을 위한 별칭. 로비 드로잉은 더 이상 이 경로를 호출하지 않는다.
        public void StartGameFromStroke() => StartGameFromMenu();

        void BeginPlayingAfterCover()
        {
            if (State != GameState.Lobby) return;

            // 연출 난수와 분리된 게임 규칙 스트림을 판 시작 직전에 함께 초기화한다.
            GameplayRandom.ResetSession();
            currentRunId = Guid.NewGuid().ToString("N");
            activeGameplaySeconds = 0f;
            var player = HighestLivingPlayer;
            SetState(GameState.Playing);
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
            transitionView.Play(
                () =>
                {
                    BrushTransitionView.RequestRevealAfterSceneLoad();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                },
                HandleTransitionFailure);
        }

        void HandleTransitionFailure()
        {
            transitionInProgress = false;
            if (IsPaused)
                AudioListener.pause = true;
        }

        bool BeginPause(GameplayPauseReason reason)
        {
            if (reason == GameplayPauseReason.None ||
                State != GameState.Playing ||
                IsPaused ||
                IsTransitioning)
                return false;

            PointerInput.SuppressUntilRelease();
            FindFirstObjectByType<StrokeCapture>()?.CancelActiveStroke();
            GameFeedbackController.Instance?.PrepareForPause();
            timeScaleBeforePause = Mathf.Max(0.01f, Time.timeScale);
            fixedDeltaBeforePause = Mathf.Max(0.001f, Time.fixedDeltaTime);
            PauseReason = reason;
            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            PauseChanged?.Invoke(true);
            return true;
        }

        void RestorePausedWorld(bool notify)
        {
            bool wasPaused = IsPaused;
            AudioListener.pause = false;
            if (!wasPaused)
            {
                PauseReason = GameplayPauseReason.None;
                return;
            }
            if (fixedDeltaBeforePause > 0f &&
                !Mathf.Approximately(Time.fixedDeltaTime, fixedDeltaBeforePause))
                Time.fixedDeltaTime = fixedDeltaBeforePause;
            Time.timeScale = Mathf.Max(0.01f, timeScaleBeforePause);
            IsPaused = false;
            PauseReason = GameplayPauseReason.None;
            timeScaleBeforePause = 1f;
            fixedDeltaBeforePause = 0.02f;
            if (notify && wasPaused)
                PauseChanged?.Invoke(false);
        }

        void SetState(GameState nextState)
        {
            if (State == nextState) return;
            GameState previousState = State;
            State = nextState;
            if (nextState == GameState.Playing)
                maxSwarmProgressHeight = 0f;
            StateChanged?.Invoke(previousState, nextState);
        }
    }
}
