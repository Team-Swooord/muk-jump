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
        FirstRunTutorial,
        ApplicationBackground,
    }

    [Serializable]
    public sealed class PendingGameOverSettlementSnapshot
    {
        public int version = 1;
        public string runId = string.Empty;
        public int swarmProgressHeight;
        public int scoreHeight;
        public int previousBest;
        public float activeGameplaySeconds;
        public bool eligible;
    }

    /// 게임오버 정산 복구본 저장을 격리해 기기 저장소 오류가 게임 흐름까지
    /// 중단하지 않게 하고, 실패 경로를 자동 테스트할 수 있게 한다.
    public interface IPendingGameOverSettlementStore
    {
        bool HasSnapshot();
        string Load();
        void Save(string json);
        void Clear();
    }

    sealed class PlayerPrefsPendingGameOverSettlementStore :
        IPendingGameOverSettlementStore
    {
        public bool HasSnapshot() =>
            PlayerPrefs.HasKey(GameManager.PendingGameOverSettlementKey);

        public string Load() => PlayerPrefs.GetString(
            GameManager.PendingGameOverSettlementKey,
            string.Empty);

        public void Save(string json)
        {
            PlayerPrefs.SetString(
                GameManager.PendingGameOverSettlementKey,
                json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(GameManager.PendingGameOverSettlementKey);
            PlayerPrefs.Save();
        }
    }

#if UNITY_EDITOR
    public sealed class MemoryPendingGameOverSettlementStore :
        IPendingGameOverSettlementStore
    {
        public string Json { get; set; } = string.Empty;
        public bool ThrowOnHas { get; set; }
        public bool ThrowOnLoad { get; set; }
        public bool ThrowOnSave { get; set; }
        public bool ThrowOnClear { get; set; }
        public int SaveCount { get; private set; }
        public int ClearCount { get; private set; }

        public bool HasSnapshot()
        {
            if (ThrowOnHas)
                throw new InvalidOperationException(
                    "Injected pending settlement existence read failure");
            return !string.IsNullOrEmpty(Json);
        }

        public string Load()
        {
            if (ThrowOnLoad)
                throw new InvalidOperationException(
                    "Injected pending settlement read failure");
            return Json;
        }

        public void Save(string json)
        {
            if (ThrowOnSave)
                throw new InvalidOperationException(
                    "Injected pending settlement write failure");
            Json = json ?? string.Empty;
            SaveCount++;
        }

        public void Clear()
        {
            if (ThrowOnClear)
                throw new InvalidOperationException(
                    "Injected pending settlement delete failure");
            Json = string.Empty;
            ClearCount++;
        }
    }
#endif

    /// 게임 상태(로비/플레이/게임오버)와 시작·재도전 흐름을 관리한다.
    public class GameManager : MonoBehaviour
    {
        public const string PendingGameOverSettlementKey =
            "MukJump.GameOver.PendingSettlement.v1";
        /// 먹분신은 각각 물리·애니메이션을 가진 실제 목숨이다. 모바일에서 한 판이
        /// 무한히 무거워지지 않으면서도 화면을 먹떼로 채울 수 있는 안전 상한이다.
        public const int MaxLivingPlayers = 24;
        /// 원본과 새 분신의 보이는 외곽 사이에 남기는 짧은 월드 간격.
        public const float CloneSpawnHorizontalGap = 0.1f;
        /// 화면 경계와 새 분신 외곽 사이에 남기는 최소 월드 간격.
        public const float CloneSpawnScreenEdgePadding = 0.05f;
        public const float ClonePopHorizontalSpeed = 2.6f;
        public const float ClonePopVerticalSpeed = 4.8f;
        public const float ClonePopRisingBoost = 1.2f;
        public const float ClonePopMaximumVerticalSpeed = 18f;
        const float ReviveAvailabilityPollInterval = 0.5f;
        const float ReviveAdRequestTimeoutSeconds = 120f;

        public static GameManager Instance { get; private set; }
        static IPendingGameOverSettlementStore pendingSettlementStore =
            new PlayerPrefsPendingGameOverSettlementStore();

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
        /// 튜토리얼이 시작 지형·풍맥과 실제 드로잉 발판 착지를 구분해 관찰한다.
        public event Action<PlayerController, PlatformCollider> PlayerLanded;
        /// 일시정지는 Playing 상태를 유지해 풀과 세션 예약을 보존하고 별도 계약으로 알린다.
        public event Action<bool> PauseChanged;
        /// 디버그 순간이동 뒤 과거 고도의 스폰 예약을 한 프레임에 소진하지 않게 알린다.
        public event Action<int> WorldHeightTeleported;

        // 게임오버 직후 오터치로 바로 재시작되는 것을 막는 대기 시간

        float gameOverTime;
        float nextScoreSettlementRetryTime;
        float nextReviveAvailabilityPollTime;
        GameOverResult latestGameOverResult;
        bool pendingRestartConfirmationArmed;
        bool gameOverPersistenceAbandoned;
        bool hasActivePendingGameOverSettlement;
        // 첫 복구본 쓰기가 실패했더라도 광고 부활로 이어진 같은 판은
        // 백그라운드/종료 시점에 다시 저장해야 한다. 위 active 플래그는
        // 실제 저장 성공만 뜻하므로 정산 책임 여부를 별도로 기억한다.
        bool requiresPendingGameOverSettlementRefresh;
        BrushTransitionView transitionView;
        GameOverPopupView gameOverPopupView;
        bool transitionInProgress;
        float timeScaleBeforePause = 1f;
        float fixedDeltaBeforePause = 0.02f;
        float maxSwarmProgressHeight;
        float activeGameplaySeconds;
        int lastActiveTimeSampleFrame = -1;
        PlayerController lastDeadPlayer;
        bool reviveUsedThisRun;
        bool reviveRequestInFlight;
        bool reviveCompletionPendingForeground;
        bool pendingReviveRewardEarned;
        int reviveRequestGeneration;
        int activeReviveRequestGeneration;
        float reviveRequestDeadline;
        bool gameOverResultSettled;
        [SerializeField, HideInInspector] string currentRunId;
        readonly List<PlayerController> players = new();
        readonly List<PlayerController> swarmScratch =
            new(MaxLivingPlayers);
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

        /// 점수는 선두 기록을 유지하되, 난이도 진행은 먹떼의 하위 중앙값을 쓴다.
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

        /// 점수·선두 대상 시스템은 살아 있는 캐릭터 중 가장 높은 캐릭터를 기준으로 한다.
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
            gameOverPopupView ??= GetComponent<GameOverPopupView>();
            gameOverPopupView?.ConfigureActions(
                HandleGameOverReviveRequested,
                HandleGameOverLobbyRequested);
        }

        void Awake()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                EditorTestAdsRuntime.EnsureExists();
#endif
            Application.targetFrameRate = 60;
            State = GameState.Lobby;
            if (!TryRecoverPendingGameOverSettlement())
                Debug.LogWarning(
                    "[MukJump] 이전 판 성장 정산을 아직 저장하지 못했습니다. 다음 진입 때 다시 시도합니다.");
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
                FindAnyObjectByType<BackgroundMusicController>() == null)
            {
                var musicObject = new GameObject("BackgroundMusic");
                musicObject.AddComponent<BackgroundMusicController>();
            }
            transitionView = GetComponent<BrushTransitionView>();
            if (transitionView == null) transitionView = gameObject.AddComponent<BrushTransitionView>();
            gameOverPopupView = GetComponent<GameOverPopupView>();
            if (gameOverPopupView == null) gameOverPopupView = gameObject.AddComponent<GameOverPopupView>();
            gameOverPopupView.ConfigureActions(
                HandleGameOverReviveRequested,
                HandleGameOverLobbyRequested);
            if (GetComponent<PauseMenuView>() == null)
                gameObject.AddComponent<PauseMenuView>();
            if (GetComponent<RunGrowthController>() == null)
                gameObject.AddComponent<RunGrowthController>();
            if (DebugToolsAvailable &&
                GetComponent<DebugShowcaseScenarioController>() == null)
                gameObject.AddComponent<DebugShowcaseScenarioController>();
            if (GetComponent<PermanentGrowthView>() == null)
                gameObject.AddComponent<PermanentGrowthView>();
            if (GetComponent<LobbyOptionsView>() == null)
                gameObject.AddComponent<LobbyOptionsView>();
            if (GetComponent<FirstRunTutorialController>() == null)
                gameObject.AddComponent<FirstRunTutorialController>();
            if (GetComponent<LobbyScreenNavigator>() == null)
                gameObject.AddComponent<LobbyScreenNavigator>();
            if (GetComponent<InkUiFeedbackController>() == null)
                gameObject.AddComponent<InkUiFeedbackController>();
            var eventSystem =
                FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
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
            if (reviveRequestInFlight)
                SetReviveAdAudioActive(false);
            transitionInProgress = false;
            reviveRequestInFlight = false;
            reviveCompletionPendingForeground = false;
            pendingReviveRewardEarned = false;
            activeReviveRequestGeneration = 0;
            reviveRequestDeadline = 0f;
            gameOverPopupView?.ConfigureActions(null, null);
            if (Instance != this) return;
            RestorePausedWorld(false, preserveBackgroundPause: false);
            Instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !gameOverResultSettled &&
                requiresPendingGameOverSettlementRefresh)
                PersistPendingGameOverSettlement();
        }

        void OnApplicationQuit()
        {
            if (!gameOverResultSettled &&
                requiresPendingGameOverSettlementRefresh)
                PersistPendingGameOverSettlement();
        }

        void Update()
        {
            // 복귀 콜백이 화면 전환 도중 도착해 보류됐다면 전환 종료 뒤 재개한다.
            // 사용자 메뉴·튜토리얼이 소유한 일시정지는 여기서 건드리지 않는다.
            if (PauseReason == GameplayPauseReason.ApplicationBackground &&
                MobileApplicationLifecycle.IsApplicationActive && !IsTransitioning)
                ResumeFromApplicationBackground();

            if (State == GameState.Playing)
            {
                // 일시정지·화면 전환 시간을 제외한 실제 조작 가능 시간만
                // 영구 성장 보상 판정에 사용한다.
                if (IsGameplayTicking)
                {
                    SampleActiveGameplayTime();
                    if (DebugInvincible || ScoreManager.Instance != null && !ScoreManager.Instance.RecordsAllowed)
                        MukJumpAnalytics.ExcludeDebugRun();
                    else if (MukJumpAnalytics.IsCollecting)
                        MukJumpAnalytics.Progress(ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0, LivingPlayerCount);
                }
                return;
            }

            if (State == GameState.Lobby)
                return;

            if (State != GameState.GameOver) return;
            if (reviveCompletionPendingForeground &&
                MobileApplicationLifecycle.IsApplicationActive)
            {
                bool rewardEarned = pendingReviveRewardEarned;
                int requestGeneration = activeReviveRequestGeneration;
                reviveCompletionPendingForeground = false;
                pendingReviveRewardEarned = false;
                FinishGameOverReviveAdCompletedForRequest(
                    requestGeneration,
                    rewardEarned);
                if (State != GameState.GameOver)
                    return;
            }
            if (reviveRequestInFlight &&
                MobileApplicationLifecycle.IsApplicationActive &&
                Time.unscaledTime >= reviveRequestDeadline)
            {
                int requestGeneration = activeReviveRequestGeneration;
                Debug.LogWarning(
                    "[MukJump] 부활 광고 완료 응답이 없어 입력과 오디오를 복구합니다.");
                FinishGameOverReviveAdCompletedForRequest(
                    requestGeneration,
                    false);
            }
            bool persistenceRetryPending =
                latestGameOverResult.PersistenceState ==
                    GameOverPersistenceState.ScoreBaselinePending ||
                latestGameOverResult.PersistenceState ==
                    GameOverPersistenceState.RecordWritePending;
            if (!transitionInProgress &&
                !gameOverPersistenceAbandoned &&
                persistenceRetryPending &&
                Time.unscaledTime >= nextScoreSettlementRetryTime)
            {
                nextScoreSettlementRetryTime = Time.unscaledTime + 0.5f;
                if (gameOverResultSettled)
                    RetryPendingGameOverPersistence();
                else
                    RetryUnsettledBestCommit();
            }
            if (!gameOverResultSettled &&
                !reviveUsedThisRun &&
                !reviveRequestInFlight &&
                Time.unscaledTime >= nextReviveAvailabilityPollTime)
            {
                nextReviveAvailabilityPollTime =
                    Time.unscaledTime + ReviveAvailabilityPollInterval;
                bool ready = SafeIsAdReady(
                    MonetizationAds.Provider,
                    FullScreenAdPlacement.GameOverReviveReward);
                gameOverPopupView?.SetReviveOffer(
                    ready,
                    waitingForAvailability: true);
            }
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

        public void NotifyPlayerLanded(
            PlayerController player,
            PlatformCollider platform)
        {
            if (State != GameState.Playing || player == null || player.IsDead)
                return;
            NotifyListenersSafely(
                PlayerLanded,
                player,
                platform,
                "착지");
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

        /// 카메라 전용 먹떼 프레임을 반환한다. 본체 여부와 상관없이 현재 가장 높은
        /// 생존자를 대표로 삼아 선두 분신을 놓치지 않는다. 사망한 개체는 제외된다.
        public bool TryGetSwarmCameraFrame(
            out PlayerController representative,
            out float clusterY,
            out float upperGuardY)
        {
            GetLivingPlayersNonAlloc(swarmScratch);
            return ResolveSwarmCameraFrame(
                swarmScratch,
                out representative,
                out clusterY,
                out upperGuardY);
        }

        /// 정렬된 먹떼의 가장 높은 생존자를 카메라 기준으로 반환한다. 카메라는 Y축만
        /// 추적하므로 선두가 바뀌어도 좌우 흔들림 없이 현재 최고 진행만 이어받는다.
        public static bool ResolveSwarmCameraFrame(
            List<PlayerController> living,
            out PlayerController representative,
            out float clusterY,
            out float upperGuardY)
        {
            representative = null;
            clusterY = float.NegativeInfinity;
            upperGuardY = float.NegativeInfinity;
            if (living == null || living.Count == 0)
                return false;

            for (int i = living.Count - 1; i >= 0; i--)
                if (living[i] == null || living[i].IsDead)
                    living.RemoveAt(i);
            if (living.Count == 0)
                return false;

            living.Sort(ComparePlayerHeight);
            int highestIndex = living.Count - 1;
            representative = living[highestIndex];
            clusterY = representative.transform.position.y;
            upperGuardY = clusterY;
            return true;
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

        /// 운영체제 백그라운드로 이동했을 때만 소유하는 일시정지다.
        /// 사용자가 이미 메뉴나 튜토리얼로 멈춘 판은 앱 복귀가 임의로 재개하지 않는다.
        public bool PauseForApplicationBackground()
        {
            return BeginPause(GameplayPauseReason.ApplicationBackground);
        }

        public bool ResumeFromApplicationBackground()
        {
            if (PauseReason != GameplayPauseReason.ApplicationBackground ||
                IsTransitioning || !MobileApplicationLifecycle.IsApplicationActive)
                return false;
            PointerInput.SuppressUntilRelease();
            RestorePausedWorld(true);
            return true;
        }

        /// 첫 플레이 설명을 읽는 동안 자동 점프·스폰·날씨·기록 시간을 함께 멈춘다.
        /// 사용자 일시정지와 소유권을 분리해 어느 한쪽이 다른 팝업을 닫지 않게 한다.
        public bool PauseForFirstRunTutorial()
        {
            return BeginPause(GameplayPauseReason.FirstRunTutorial);
        }

        public bool ResumeFirstRunTutorial()
        {
            if (PauseReason != GameplayPauseReason.FirstRunTutorial)
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

            // 광고 부활 뒤에도 첫 사망의 crash-safe 복구본은 남아 있다.
            // 사용자가 명시적으로 판을 포기하고 로비로 가면 그 복구본을 먼저
            // 폐기해야 다음 실행에서 거리·먹빛이 뒤늦게 정산되지 않는다.
            if (!TryAbandonPendingGameOverSettlement())
                return false;
            transitionInProgress = true;
            PointerInput.SuppressUntilRelease();
            // 붓 전환음은 들리되 물리는 화면이 완전히 덮일 때까지 멈춘 상태를 유지한다.
            AudioListener.pause = false;
            void ReloadLobby()
            {
                MukJumpAnalytics.EndRun(currentRunId, ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0,
                    activeGameplaySeconds, abandoned: true, newBest: false);
                MukJumpAnalytics.Screen(AnalyticsScreen.Lobby);
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
            if (State != GameState.Playing)
                return false;

            // 사망은 FixedUpdate 물리 콜백에서 Update보다 먼저 올 수 있다. 모든
            // 사망자의 도달 높이를 즉시 기록하고, 마지막 개체라면 진행·시간도
            // 현재 프레임까지 한 번만 최종 샘플한다.
            if (player != null)
            {
                ScoreManager.Instance?.SampleWorldHeight(
                    player.transform.position.y);
                SampleSwarmProgressIncluding(player);
            }
            if (LivingPlayerCount > 0)
            {
                if (player != null &&
                    TryGetSwarmCameraFrame(
                        out _,
                        out _,
                        out float survivingUpperGuardY) &&
                    CameraFollow.ShouldReframeAfterDeath(
                        player.transform.position.y,
                        survivingUpperGuardY))
                {
                    Camera.main?.GetComponent<CameraFollow>()?
                        .RequestSurvivorReframe();
                }
                return false;
            }
            SampleActiveGameplayTime();

            lastDeadPlayer = player;
            // 광고 선택창에서 앱을 닫거나 버튼 입력이 실패해도 이 판의 새 기록은
            // 이미 저장돼 있어야 한다. 부활 후 더 높게 죽으면 같은 API가 다시 올린다.
            CommitCurrentBestBeforeGameOver();
            EnterGameOver();
            return true;
        }

        bool CommitCurrentBestBeforeGameOver()
        {
            ScoreManager score = ScoreManager.Instance;
            return score == null || !score.RecordsAllowed ||
                   score.TryCommitBestCandidate(score.Height);
        }

        void SampleSwarmProgressIncluding(PlayerController dyingPlayer)
        {
            GetLivingPlayersNonAlloc(swarmScratch);
            if (dyingPlayer != null && !swarmScratch.Contains(dyingPlayer))
                swarmScratch.Add(dyingPlayer);
            float worldY = ResolveSwarmAnchorY(swarmScratch, out _);
            if (float.IsNegativeInfinity(worldY))
                return;
            float progress = ScoreManager.Instance != null
                ? Mathf.Max(0f, ScoreManager.Instance.HeightAt(worldY))
                : Mathf.Max(0f, worldY);
            maxSwarmProgressHeight = Mathf.Max(maxSwarmProgressHeight, progress);
        }

        void SampleActiveGameplayTime()
        {
            if (lastActiveTimeSampleFrame == Time.frameCount)
                return;
            activeGameplaySeconds += Time.unscaledDeltaTime;
            lastActiveTimeSampleFrame = Time.frameCount;
        }

        void EnterGameOver()
        {
            if (State == GameState.GameOver) return;
            MukJumpAnalytics.GameOver(ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0, activeGameplaySeconds);
            SetState(GameState.GameOver);
            var feedback = GameFeedbackController.Instance;
            float revealDelay = feedback != null ? feedback.GameOverRevealDelay : 0.62f;
            feedback?.PlayGameOver();
            gameOverTime = float.PositiveInfinity;
            bool canWaitForRevive = !reviveUsedThisRun &&
                                    MonetizationAds.RuntimeProviderExpected;
            bool canOfferRevive = MonetizationPolicy.CanOfferGameOverRevive(
                true,
                reviveUsedThisRun,
                SafeIsAdReady(
                    MonetizationAds.Provider,
                    FullScreenAdPlacement.GameOverReviveReward));
            nextReviveAvailabilityPollTime =
                Time.unscaledTime + ReviveAvailabilityPollInterval;
            PersistPendingGameOverSettlement();
            gameOverResultSettled = !canWaitForRevive;
            latestGameOverResult = canWaitForRevive
                ? CreateUnsettledGameOverPreview()
                : SettleGameOverResult();
            pendingRestartConfirmationArmed = false;
            gameOverPersistenceAbandoned = false;
            nextScoreSettlementRetryTime = Time.unscaledTime + 0.5f;
            StartCoroutine(ShowGameOverAfterDeath(
                revealDelay,
                canOfferRevive,
                !gameOverResultSettled));
        }

        GameOverResult CreateUnsettledGameOverPreview()
        {
            ScoreManager score = ScoreManager.Instance;
            int height = score != null ? score.Height : 0;
            int previousBest = score != null ? score.RunBestToBeat : 0;
            int committedBest = score != null ? score.Best : 0;
            bool rewardsAllowed = score == null || score.RecordsAllowed;
            bool baselineConfirmed = score == null || score.HasConfirmedBest;
            long distanceBefore = PermanentGrowthProfile.CumulativeDistanceMeters;
            var growthPreview = PermanentGrowthProfile.PreviewRun(height, rewardsAllowed && baselineConfirmed);
            bool recordSaved = baselineConfirmed &&
                               (score == null ||
                                !score.HasPendingBestSaveRetry) &&
                               (score == null || !score.RecordsAllowed ||
                                committedBest >= height);
            return new GameOverResult(
                height,
                Mathf.Max(committedBest, height),
                rewardsAllowed && baselineConfirmed && height > previousBest,
                0,
                PermanentGrowthProfile.Currency,
                rewardsAllowed,
                growthRewardSaved: growthPreview.Accepted,
                recordSaved: recordSaved,
                persistenceState: !baselineConfirmed
                    ? GameOverPersistenceState.ScoreBaselinePending
                    : recordSaved
                        ? GameOverPersistenceState.Complete
                        : GameOverPersistenceState.RecordWritePending,
                cumulativeGrowthDistanceMeters: growthPreview.CumulativeDistanceMeters,
                previousGrowthRewardDistanceMeters: growthPreview.PreviousRewardDistanceMeters,
                nextGrowthRewardDistanceMeters: growthPreview.NextRewardDistanceMeters,
                growthDistanceJourneyComplete: growthPreview.DistanceJourneyComplete,
                growthDistanceBeforeMeters: distanceBefore,
                isGrowthPreview: true,
                previewGrowthCurrency: growthPreview.Earned,
                growthDistanceRewardOffsetMeters: growthPreview.DistanceRewardOffsetMeters);
        }

        /// 광고 선택을 기다리는 동안에는 먹빛 정산을 시작하지 않고 최고 기록만
        /// 재시도한다. 광고 부활로 판을 이어가면 정산은 다음 사망까지 보류된다.
        void RetryUnsettledBestCommit()
        {
            if (gameOverResultSettled)
                return;

            ScoreManager score = ScoreManager.Instance;
            if (score == null)
                return;

            bool ready = latestGameOverResult.PersistenceState ==
                    GameOverPersistenceState.ScoreBaselinePending
                ? score.TryEnsureBestLoaded()
                : score.TryCommitBestCandidate(latestGameOverResult.Height);
            if (!ready)
                return;

            latestGameOverResult = CreateUnsettledGameOverPreview();
            gameOverPopupView?.RefreshResult(latestGameOverResult);
        }

        void HandleGameOverReviveRequested()
        {
            if (State != GameState.GameOver ||
                gameOverResultSettled ||
                reviveUsedThisRun ||
                reviveRequestInFlight)
                return;

            IFullScreenAdProvider ads = MonetizationAds.Provider;
            if (!MonetizationPolicy.CanOfferGameOverRevive(
                    true,
                    reviveUsedThisRun,
                    SafeIsAdReady(
                        ads,
                        FullScreenAdPlacement.GameOverReviveReward)))
            {
                gameOverPopupView?.SetReviveOffer(
                    false,
                    waitingForAvailability: true);
                SafePreloadAd(
                    ads,
                    FullScreenAdPlacement.GameOverReviveReward);
                return;
            }

            int requestGeneration = ++reviveRequestGeneration;
            MukJumpAnalytics.Ad(AnalyticsAdStage.ShowRequested);
            activeReviveRequestGeneration = requestGeneration;
            reviveRequestInFlight = true;
            reviveRequestDeadline =
                Time.unscaledTime + ReviveAdRequestTimeoutSeconds;
            reviveCompletionPendingForeground = false;
            pendingReviveRewardEarned = false;
            gameOverPopupView?.SetReviveRequestInFlight(true);
            SetReviveAdAudioActive(true);
            try
            {
                ads.Show(
                    FullScreenAdPlacement.GameOverReviveReward,
                    rewardEarned =>
                        HandleGameOverReviveAdCompletedForRequest(
                            requestGeneration,
                            rewardEarned));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 부활 광고 열기에 실패했습니다: {exception.Message}");
                HandleGameOverReviveAdCompletedForRequest(
                    requestGeneration,
                    false);
            }
        }

        void HandleGameOverReviveAdCompletedForRequest(
            int requestGeneration,
            bool rewardEarned)
        {
            if (this == null ||
                !reviveRequestInFlight ||
                requestGeneration != activeReviveRequestGeneration)
                return;

            // 완료 콜백이 전경·백그라운드 경계에서 중복되어도 보상 true는 단조롭게
            // 유지한다. 전경 복귀 직후 Update가 소비하기 전에 false 콜백이 와도
            // 이미 획득한 보상이 취소되면 안 된다.
            pendingReviveRewardEarned |= rewardEarned;
            if (!MobileApplicationLifecycle.IsApplicationActive)
            {
                reviveCompletionPendingForeground = true;
                return;
            }

            FinishGameOverReviveAdCompletedForRequest(
                requestGeneration,
                pendingReviveRewardEarned);
        }

        void FinishGameOverReviveAdCompletedForRequest(
            int requestGeneration,
            bool rewardEarned)
        {
            if (!reviveRequestInFlight ||
                requestGeneration != activeReviveRequestGeneration)
                return;

            activeReviveRequestGeneration = 0;
            reviveRequestDeadline = 0f;
            reviveCompletionPendingForeground = false;
            pendingReviveRewardEarned = false;
            SetReviveAdAudioActive(false);
            reviveRequestInFlight = false;
            // 공급자의 IsReady가 실패해도 두 버튼의 입력 잠금은 먼저 해제한다.
            gameOverPopupView?.SetReviveRequestInFlight(false);
            if (State != GameState.GameOver || !rewardEarned)
            {
                bool ready = State == GameState.GameOver &&
                             SafeIsAdReady(
                                 MonetizationAds.Provider,
                                 FullScreenAdPlacement.GameOverReviveReward);
                gameOverPopupView?.SetReviveOffer(
                    ready,
                    waitingForAvailability: !gameOverResultSettled);
                return;
            }

            PlayerController reviveTarget = ResolveRewardRevivePlayer();
            if (reviveTarget == null)
            {
                gameOverPopupView?.SetReviveOffer(false);
                return;
            }

            // 광고 보상은 확정된 상태지만 물리 재개는 결과 두루마리가 감긴 뒤다.
            if (gameOverPopupView != null)
                gameOverPopupView.Close(() => CompleteRewardedRevive(reviveTarget));
            else
                CompleteRewardedRevive(reviveTarget);
        }

        void CompleteRewardedRevive(PlayerController reviveTarget)
        {
            if (this == null || !isActiveAndEnabled || State != GameState.GameOver)
                return;
            if (reviveTarget == null || !reviveTarget.ReviveFromRewardedAd())
            {
                gameOverPopupView?.Show(latestGameOverResult, false, false);
                return;
            }

            reviveUsedThisRun = true;
            MukJumpAnalytics.Revive();
            lastDeadPlayer = null;
            gameOverTime = float.PositiveInfinity;
            pendingRestartConfirmationArmed = false;
            gameOverPersistenceAbandoned = false;
            SetState(GameState.Playing);
            Camera.main?.GetComponent<CameraFollow>()?
                .RequestSurvivorReframe();
            PointerInput.SuppressUntilRelease();
        }

        static void SetReviveAdAudioActive(bool active)
        {
            AudioListener.pause = active;
            BackgroundMusicController.Instance?.SetFullScreenAdActive(active);
        }

        static bool SafeIsAdReady(
            IFullScreenAdProvider provider,
            FullScreenAdPlacement placement)
        {
            if (provider == null)
                return false;
            try
            {
                return provider.IsReady(placement);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 광고 준비 상태 확인에 실패했습니다: {exception.Message}");
                return false;
            }
        }

        static void SafePreloadAd(
            IFullScreenAdProvider provider,
            FullScreenAdPlacement placement)
        {
            if (provider == null)
                return;
            try
            {
                provider.Preload(placement);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 광고 미리 불러오기에 실패했습니다: {exception.Message}");
            }
        }

        PlayerController ResolveRewardRevivePlayer()
        {
            CleanupPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController candidate = players[i];
                if (candidate != null && candidate.IsDead &&
                    !candidate.IsRuntimeClone)
                    return candidate;
            }
            return lastDeadPlayer != null && lastDeadPlayer.IsDead
                ? lastDeadPlayer
                : null;
        }

        void HandleGameOverLobbyRequested()
        {
            if (State != GameState.GameOver ||
                transitionInProgress ||
                reviveRequestInFlight)
                return;

            if (!gameOverResultSettled)
            {
                latestGameOverResult = SettleGameOverResult();
                gameOverResultSettled = true;
                gameOverPopupView?.RefreshResult(latestGameOverResult);
            }

            bool recordPending =
                latestGameOverResult.PersistenceState ==
                    GameOverPersistenceState.ScoreBaselinePending ||
                latestGameOverResult.PersistenceState ==
                    GameOverPersistenceState.RecordWritePending;
            if (recordPending)
            {
                RetryPendingGameOverPersistence();
                recordPending =
                    latestGameOverResult.PersistenceState ==
                        GameOverPersistenceState.ScoreBaselinePending ||
                    latestGameOverResult.PersistenceState ==
                        GameOverPersistenceState.RecordWritePending;
                if (recordPending && !pendingRestartConfirmationArmed)
                {
                    pendingRestartConfirmationArmed = true;
                    gameOverPopupView?.ShowPendingAbandonConfirmation();
                    return;
                }
            }
            if (recordPending)
            {
                if (!TryAbandonPendingGameOverSettlement())
                    return;
                ScoreManager.Instance?.StopPendingBestSaveRetry();
            }
            if (gameOverPopupView != null)
                gameOverPopupView.Close(Restart);
            else
                Restart();
        }

        /// 결과 표시보다 먼저 최고 기록과 영구 성장 보상을 한 번에 확정한다.
        /// 저장된 run ID가 도메인 리로드 뒤의 중복 호출도 막는다.
        GameOverResult SettleGameOverResult()
        {
            ScoreManager score = ScoreManager.Instance;
            int height = score != null ? score.Height : 0;
            int swarmProgressHeight = Mathf.FloorToInt(SwarmProgressHeight);
            bool recordsAllowed = score == null || score.RecordsAllowed;
            bool scoreBaselineReady = score == null ||
                !score.RecordsAllowed ||
                score.TryEnsureBestLoaded();
            int previousBest = score != null ? score.RunBestToBeat : 0;
            bool reachedNewBest = recordsAllowed &&
                height > previousBest;
            if (string.IsNullOrEmpty(currentRunId))
                currentRunId = Guid.NewGuid().ToString("N");
            bool rewardsAllowed =
                score != null && score.RecordsAllowed;
            bool growthProfileHealthy = !PermanentGrowthProfile.RequiresRecovery;
            long distanceBefore = PermanentGrowthProfile.CumulativeDistanceMeters;
            PermanentGrowthSettlement settlement = rewardsAllowed && scoreBaselineReady
                ? PermanentGrowthProfile.SettleRun(
                    currentRunId,
                    swarmProgressHeight,
                    height,
                    previousBest,
                    activeGameplaySeconds,
                    true)
                : new PermanentGrowthSettlement(
                    0,
                    PermanentGrowthProfile.Currency,
                    growthProfileHealthy && scoreBaselineReady,
                    0,
                    PermanentGrowthProfile.CumulativeDistanceMeters,
                    PermanentGrowthProfile.PreviousDistanceRewardMeters,
                    PermanentGrowthProfile.NextDistanceRewardMeters,
                    PermanentGrowthProfile.IsDistanceJourneyComplete);
            bool growthSaved = !rewardsAllowed || settlement.Accepted ||
                PermanentGrowthProfile.IsRunSettled(currentRunId);
            // 최고 기록은 광고·성장 정산과 독립된 단조 데이터다. 성장 저장이
            // 복구 상태여도 현재 판의 새 기록만큼은 잃지 않는다.
            bool recordNeedsSave = score != null && score.RecordsAllowed &&
                (height > score.Best || score.HasPendingBestSaveRetry);
            bool recordSaved = scoreBaselineReady &&
                (!recordNeedsSave ||
                 score.TryCommitBestCandidate(height));
            if (growthSaved && recordSaved &&
                ClearPendingGameOverSettlement())
            {
                hasActivePendingGameOverSettlement = false;
                requiresPendingGameOverSettlementRefresh = false;
            }
            GameOverPersistenceState persistenceState = !scoreBaselineReady
                ? GameOverPersistenceState.ScoreBaselinePending
                : !recordSaved
                        ? GameOverPersistenceState.RecordWritePending
                    : !growthSaved
                        ? GameOverPersistenceState.GrowthRecoveryRequired
                        : GameOverPersistenceState.Complete;
            int best = score != null ? score.Best : previousBest;
            var result = new GameOverResult(
                height,
                best,
                reachedNewBest && recordSaved,
                settlement.Earned,
                settlement.Balance,
                rewardsAllowed,
                growthSaved,
                recordSaved,
                persistenceState,
                settlement.CumulativeDistanceMeters,
                settlement.PreviousRewardDistanceMeters,
                settlement.NextRewardDistanceMeters,
                settlement.DistanceJourneyComplete,
                distanceBefore,
                growthDistanceRewardOffsetMeters: settlement.DistanceRewardOffsetMeters);
            PublishCompletedRun(result);
            if (growthSaved && recordSaved)
                MukJumpAnalytics.EndRun(currentRunId, height, activeGameplaySeconds, abandoned: false, newBest: reachedNewBest);
            return result;
        }

        static void PublishCompletedRun(GameOverResult result)
        {
            InvokeReleaseNotificationSafely(
                "계정 동기화",
                () => MukJumpAccountRuntime.Instance?.NotifyRunSettled(result));
            InvokeReleaseNotificationSafely(
                "앱인토스 순위 제출",
                () => AppsInTossGameCenterRuntime.SubmitCompletedRun(result));
            InvokeReleaseNotificationSafely(
                "Game Center 순위 제출",
                () => AppleGameCenterRuntime.SubmitCompletedRun(result));
        }

        static bool InvokeReleaseNotificationSafely(
            string label,
            Action notification)
        {
            try
            {
                notification?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                // 외부 동기화/순위 제출은 정산 뒤의 부가 알림이다. 로컬 저장과
                // 결과창을 이미 확정한 흐름까지 실패시키면 재시작 버튼이 사라진다.
                Debug.LogWarning(
                    $"[MukJump] {label} 알림을 다음 기회로 미룹니다: " +
                    exception.Message);
                return false;
            }
        }

        void RetryPendingGameOverPersistence()
        {
            ScoreManager score = ScoreManager.Instance;
            if (score == null)
                return;

            if (latestGameOverResult.PersistenceState ==
                GameOverPersistenceState.ScoreBaselinePending)
            {
                if (!score.TryEnsureBestLoaded())
                    return;
                latestGameOverResult = SettleGameOverResult();
                if (latestGameOverResult.PersistenceState !=
                    GameOverPersistenceState.ScoreBaselinePending)
                    pendingRestartConfirmationArmed = false;
                gameOverPopupView?.RefreshResult(latestGameOverResult);
                return;
            }

            if (latestGameOverResult.PersistenceState !=
                    GameOverPersistenceState.RecordWritePending ||
                !score.TryCommitBestCandidate(latestGameOverResult.Height))
                return;

            GameOverResult previous = latestGameOverResult;
            latestGameOverResult = new GameOverResult(
                previous.Height,
                score.Best,
                previous.Height > previous.Best &&
                previous.Height >= score.Best,
                previous.EarnedGrowthCurrency,
                previous.GrowthCurrencyBalance,
                previous.RewardsAllowed,
                previous.GrowthRewardSaved,
                true,
                previous.GrowthRewardSaved
                    ? GameOverPersistenceState.Complete
                    : GameOverPersistenceState.GrowthRecoveryRequired,
                previous.CumulativeGrowthDistanceMeters,
                previous.PreviousGrowthRewardDistanceMeters,
                previous.NextGrowthRewardDistanceMeters,
                previous.GrowthDistanceJourneyComplete,
                previous.GrowthDistanceBeforeMeters,
                previous.IsGrowthPreview,
                previous.PreviewGrowthCurrency,
                previous.GrowthDistanceRewardOffsetMeters);
            pendingRestartConfirmationArmed = false;
            gameOverPopupView?.RefreshResult(latestGameOverResult);
            // 최초 정산은 기록 저장 실패 상태라 클라우드와 순위 제출이
            // 거절된다. 기록 재시도가 완료된 정확한 시점에 한 번 더 알린다.
            PublishCompletedRun(latestGameOverResult);
        }

        System.Collections.IEnumerator ShowGameOverAfterDeath(
            float delay,
            bool canOfferRevive,
            bool settlementPending)
        {
            yield return new WaitForSecondsRealtime(delay);
            gameOverPopupView.Show(
                latestGameOverResult,
                canOfferRevive,
                settlementPending);
            // 팝업이 나타나기 전 포인터 다운은 Canvas가 받지 못하므로, 표시된
            // 명시적 버튼은 즉시 동작시킨다. 무반응 시간창을 만들지 않는다.
            gameOverTime = Time.unscaledTime;
        }

        /// 먹분신 한 마리를 즉시 추가한다. 아이템의 묶음 생성은
        /// TryCreateInkClonesFromItem에서 성장 수치만큼 이 원자 동작을 반복한다.
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
            if (!TryFindCloneSpawnPosition(source, cloneIndex, out Vector3 spawnPosition))
                return false;

            // 구체적인 아이템·VFX 타입을 모른 채 복제 생명주기 계약만 호출한다.
            // 각 기능은 동기 Instantiate 동안 게임 상태가 아닌 캐시 자식을 스스로 분리한다.
            cloneHookBehaviours.Clear();
            cloneHooks.Clear();
            source.GetComponents(cloneHookBehaviours);
            for (int i = 0; i < cloneHookBehaviours.Count; i++)
                if (cloneHookBehaviours[i] is IRuntimeCloneLifecycle hook)
                    cloneHooks.Add(hook);

            GameObject cloneObject = null;
            Exception cloneFailure = null;
            int restoreHookCount = 0;
            try
            {
                for (int i = 0; i < cloneHooks.Count; i++)
                {
                    // Prepare가 중간에 예외를 내더라도 해당 훅이 일부 상태를
                    // 바꿨을 수 있으므로 현재 훅까지 복구를 시도한다.
                    restoreHookCount = i + 1;
                    cloneHooks[i].PrepareForRuntimeClone();
                }
                cloneObject = Instantiate(source.gameObject, spawnPosition,
                    source.transform.rotation);
            }
            catch (Exception exception)
            {
                cloneFailure = exception;
                Debug.LogException(exception, source);
            }
            finally
            {
                for (int i = restoreHookCount - 1; i >= 0; i--)
                {
                    try
                    {
                        cloneHooks[i].RestoreAfterRuntimeClone();
                    }
                    catch (Exception exception)
                    {
                        // 한 훅의 복구 실패가 앞서 준비된 다른 훅의 복구를
                        // 막지 않게 끝까지 정리한다.
                        cloneFailure ??= exception;
                        Debug.LogException(exception, source);
                    }
                }
                cloneHooks.Clear();
                cloneHookBehaviours.Clear();
            }
            if (cloneFailure != null || cloneObject == null)
            {
                if (cloneObject != null)
                    DiscardFailedClone(cloneObject);
                return false;
            }
            cloneObject.name = "Player (먹분신)";
            var clone = cloneObject.GetComponent<PlayerController>();
            if (clone == null)
            {
                DiscardFailedClone(cloneObject);
                return false;
            }

            var cloneBody = clone.GetComponent<Rigidbody2D>();
            clone.ConfigureAsClone(source.NormalGravityScale);
            if (sourceBody != null && cloneBody != null)
            {
                float outwardDirection = Mathf.Sign(
                    spawnPosition.x - source.transform.position.x);
                bool preserveSpecialRise = source.IsInkDropBoosted ||
                    (!source.IsAutomaticJumpInFlight &&
                     sourceBody.linearVelocity.y > 8f);
                cloneBody.linearVelocity = ResolveClonePopVelocity(
                    sourceBody.linearVelocity,
                    outwardDirection,
                    cloneIndex,
                    preserveSpecialRise);
            }

            RegisterPlayer(clone);
            clone.GetComponent<InkCloneArrivalView>()?.Play();
            GameFeedbackController.Instance?.PlayCloneArrival(clone.transform.position,
                cloneBody != null ? cloneBody.linearVelocity : Vector2.zero);
            return true;
        }

        void DiscardFailedClone(GameObject cloneObject)
        {
            // Instantiate의 OnEnable에서 이미 등록됐을 수 있다. 프레임 끝의
            // Destroy까지 실패한 분신이 생존자 수·카메라에 잡히지 않게 정리한다.
            var clone = cloneObject.GetComponent<PlayerController>();
            if (clone != null)
                UnregisterPlayer(clone);
            cloneObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(cloneObject);
            else
                DestroyImmediate(cloneObject);
        }

        /// 먹분신이 원본 옆에 딱딱하게 서지 않고 팝콘처럼 좌우로 퍼져 오르게 한다.
        /// 난수 대신 인덱스 변주를 사용해 촬영과 테스트에서 같은 궤적을 재현한다.
        public static Vector2 ResolveClonePopVelocity(
            Vector2 sourceVelocity,
            float outwardDirection,
            int cloneIndex,
            bool preserveSpecialRise = false)
        {
            float side = Mathf.Abs(outwardDirection) > 0.01f
                ? Mathf.Sign(outwardDirection)
                : (cloneIndex % 2 == 0 ? -1f : 1f);
            float horizontalVariation = (Mathf.Abs(cloneIndex) % 3) * 0.35f;
            float verticalVariation = (Mathf.Abs(cloneIndex) % 2) * 0.9f;
            float inheritedHorizontal = Mathf.Clamp(
                sourceVelocity.x * 0.45f,
                -2f,
                2f);
            float verticalSpeed = preserveSpecialRise
                ? sourceVelocity.y
                : Mathf.Clamp(
                    Mathf.Max(
                        ClonePopVerticalSpeed + verticalVariation,
                        Mathf.Max(0f, sourceVelocity.y) + ClonePopRisingBoost),
                    ClonePopVerticalSpeed,
                    ClonePopMaximumVerticalSpeed);
            return new Vector2(
                inheritedHorizontal + side *
                (ClonePopHorizontalSpeed + horizontalVariation),
                verticalSpeed);
        }

        /// 먹분신 아이템 한 개로 기본 한 마리와 성장 보너스만큼을 함께 만든다.
        /// 최대 먹떼 수에 가까우면 들어갈 수 있는 수만 만들고, 한 마리라도 만들어졌을 때만
        /// 아이템을 소비한다.
        public bool TryCreateInkClonesFromItem(PlayerController source)
        {
            if (!CanCreateInkClone || source == null || source.IsDead)
                return false;

            int extraCount = RunGrowthController.Instance != null
                ? RunGrowthController.Instance.PermanentSnapshot.InkCloneItemExtraCount
                : 0;
            int availableCount = Mathf.Max(0, MaxLivingPlayers - LivingPlayerCount);
            int spawnCount = ResolveInkCloneItemSpawnCount(
                extraCount,
                availableCount);
            int createdCount = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryCreateInkClone(source))
                    break;
                createdCount++;
            }
            return createdCount > 0;
        }

        public static int ResolveInkCloneItemSpawnCount(
            int growthExtraCount,
            int availableLivingSlots)
        {
            int requestedCount = 1 + Mathf.Clamp(growthExtraCount, 0, 1);
            return Mathf.Min(requestedCount, Mathf.Max(0, availableLivingSlots));
        }

        /// 먹물방울은 한 마리만 화면 밖으로 이탈하지 않도록 현재 먹떼 전체에 같은
        /// 상승 속도를 적용한다. 카메라는 상승 후 가장 높은 생존자를 이어서 추적한다.
        public bool LaunchSwarmInkDrop(PlayerController collector, float height)
            => LaunchSwarmInkDrop(collector, height, out _);

        public bool LaunchSwarmInkDrop(PlayerController collector, float height,
            out PlayerController feedbackPlayer)
        {
            feedbackPlayer = null;
            if (!IsGameplayTicking || collector == null || collector.IsDead)
                return false;

            GetLivingPlayersNonAlloc(swarmScratch);
            if (swarmScratch.Count == 0)
                return false;

            for (int i = 0; i < swarmScratch.Count; i++)
                swarmScratch[i].LaunchInkDrop(height, playCameraImpulse: false);

            TryGetSwarmCameraFrame(out var representative, out _, out _);
            var impulseSource = representative != null ? representative : collector;
            feedbackPlayer = impulseSource;
            Camera.main?.GetComponent<CameraFollow>()?.PlayJumpImpulse(
                impulseSource.transform,
                Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(25f, 50f, height)));
            return true;
        }

        /// 먹은 캐릭터의 스프라이트·Collider 외곽 바로 옆에 새 분신을 만든다. 화면 가운데에서는
        /// 좌우를 번갈아 쓰고, 한쪽에 치우친 캐릭터는 화면 안쪽을 우선해 생성한다.
        /// 대량 분산보다 획득한 순간 두 캐릭터가 한 쌍으로 읽히는 것이 우선이다.
        Vector3 FindCloneSpawnPosition(PlayerController source, int cloneIndex)
        {
            return TryFindCloneSpawnPosition(source, cloneIndex, out Vector3 position)
                ? position
                : source.transform.position;
        }

        bool TryFindCloneSpawnPosition(
            PlayerController source,
            int cloneIndex,
            out Vector3 position)
        {
            Vector3 sourcePosition = source.transform.position;
            position = sourcePosition;
            Physics2D.SyncTransforms();

            Collider2D sourceCollider = source.PrimaryCollider;
            float footprintWidth = 0.8f;
            float footprintMinOffset = -footprintWidth * 0.5f;
            float footprintMaxOffset = footprintWidth * 0.5f;
            bool hasFootprint = false;
            Bounds footprint = default;
            if (sourceCollider != null)
            {
                footprint = sourceCollider.bounds;
                hasFootprint = footprint.size.x > 0.01f;
            }

            var sourceRenderer = source.GetComponent<SpriteRenderer>();
            if (sourceRenderer != null && sourceRenderer.sprite != null)
            {
                Bounds visualBounds = sourceRenderer.bounds;
                if (visualBounds.size.x > 0.01f)
                {
                    if (hasFootprint)
                        footprint.Encapsulate(visualBounds);
                    else
                    {
                        footprint = visualBounds;
                        hasFootprint = true;
                    }
                }
            }

            if (hasFootprint)
            {
                footprintWidth = footprint.size.x;
                footprintMinOffset = footprint.min.x - sourcePosition.x;
                footprintMaxOffset = footprint.max.x - sourcePosition.x;
            }

            float adjacentDistance = footprintWidth + CloneSpawnHorizontalGap;
            var worldCamera = Camera.main;
            if (worldCamera == null)
            {
                float directionWithoutCamera = ResolveOppositeCloneSide(
                    sourcePosition.x, 0f, cloneIndex);
                int maxRingWithoutCamera = Mathf.Max(1, LivingPlayerCount + 1);
                for (int ring = 1; ring <= maxRingWithoutCamera; ring++)
                for (int side = 0; side < 2; side++)
                {
                    float sideDirection = side == 0
                        ? directionWithoutCamera
                        : -directionWithoutCamera;
                    float candidateX = sourcePosition.x +
                                       sideDirection * adjacentDistance * ring;
                    if (!IsCloneHorizontalSlotFree(
                            candidateX,
                            sourcePosition.y,
                            footprintWidth))
                        continue;
                    position = new Vector3(
                        candidateX,
                        sourcePosition.y,
                        sourcePosition.z);
                    return true;
                }
                return false;
            }

            float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float cameraCenterX = worldCamera.transform.position.x;
            float direction = ResolveOppositeCloneSide(
                sourcePosition.x, cameraCenterX, cloneIndex);
            float cameraLeft = cameraCenterX - halfWidth;
            float cameraRight = cameraCenterX + halfWidth;
            float minRootX = cameraLeft + CloneSpawnScreenEdgePadding -
                             footprintMinOffset;
            float maxRootX = cameraRight - CloneSpawnScreenEdgePadding -
                             footprintMaxOffset;

            float preferredX = sourcePosition.x;
            bool foundFreeSlot = false;
            int maxRing = Mathf.Max(1, LivingPlayerCount + 1);
            for (int ring = 1; ring <= maxRing && !foundFreeSlot; ring++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float sideDirection = side == 0 ? direction : -direction;
                    float unclampedX = sourcePosition.x +
                                       sideDirection * adjacentDistance * ring;
                    float candidateX = Mathf.Clamp(unclampedX, minRootX, maxRootX);
                    // 경계 clamp로 본체 쪽에 눌린 슬롯은 사용하지 않는다.
                    if (Mathf.Abs(candidateX - sourcePosition.x) + 0.001f <
                        adjacentDistance)
                        continue;
                    if (!IsCloneHorizontalSlotFree(
                            candidateX,
                            sourcePosition.y,
                            footprintWidth))
                        continue;
                    preferredX = candidateX;
                    foundFreeSlot = true;
                    break;
                }
            }

            if (!foundFreeSlot)
                return false;

            position = new Vector3(
                preferredX,
                sourcePosition.y,
                sourcePosition.z);
            return true;
        }

        bool IsCloneHorizontalSlotFree(
            float candidateX,
            float candidateY,
            float candidateWidth)
        {
            CleanupPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController other = players[i];
                if (other == null || other.IsDead)
                    continue;

                float otherWidth = ResolveVisibleFootprintWidth(
                    other,
                    candidateWidth);
                float verticalTolerance = Mathf.Max(candidateWidth, otherWidth);
                if (Mathf.Abs(other.transform.position.y - candidateY) >
                    verticalTolerance)
                    continue;

                float requiredDistance =
                    (candidateWidth + otherWidth) * 0.5f + CloneSpawnHorizontalGap;
                if (Mathf.Abs(other.transform.position.x - candidateX) + 0.001f <
                    requiredDistance)
                    return false;
            }
            return true;
        }

        static float ResolveVisibleFootprintWidth(
            PlayerController player,
            float fallbackWidth)
        {
            if (player == null)
                return fallbackWidth;

            bool hasBounds = false;
            Bounds bounds = default;
            Collider2D collider = player.PrimaryCollider;
            if (collider != null && collider.bounds.size.x > 0.01f)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }

            var renderer = player.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null &&
                renderer.bounds.size.x > 0.01f)
            {
                if (hasBounds)
                    bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }
            return hasBounds ? bounds.size.x : fallbackWidth;
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
            NotifyListenersSafely(
                WorldHeightTeleported,
                Mathf.Max(0, targetHeight),
                "고도 순간이동");
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
            var scenePlayers = FindObjectsByType<PlayerController>();
            for (int i = 0; i < scenePlayers.Length; i++)
                RegisterPlayer(scenePlayers[i]);
        }

        /// 로비의 명시적인 시작 버튼에서 호출하는 유일한 새 게임 진입점.
        /// 씬 빌더가 준비한 영구 시작 발판 위에서 물리를 풀어 첫 자동 점프를 준비한다.
        public void StartGameFromMenu()
        {
            TryStartGame(false);
        }

        internal void StartFirstRunFromStartup()
        {
            if (LobbySettingsProfile.NeedsGameplayTutorial) TryStartGame(true);
        }

        void TryStartGame(bool behindStartupBrand)
        {
            if (StartupBrandSplash.IsBlockingInput && !behindStartupBrand)
                return;
            var navigator = LobbyScreenNavigator.Instance;
            if (navigator == null)
                navigator = GetComponent<LobbyScreenNavigator>();
            MukJumpAccountRuntime accountRuntime =
                MukJumpAccountRuntime.Instance;
            if (accountRuntime != null &&
                accountRuntime.BlocksGameplayForAccountSync)
            {
                LobbyOptionsView options = GetComponent<LobbyOptionsView>();
                if (options == null)
                    options = FindAnyObjectByType<LobbyOptionsView>();
                options?.OpenAccountForRequiredSync();
                return;
            }
            if (State != GameState.Lobby ||
                IsTransitioning ||
                PermanentGrowthProfile.RequiresRecovery ||
                navigator != null && !navigator.CanStartGame)
                return;
            if (!TryRecoverPendingGameOverSettlement())
            {
                Debug.LogWarning(
                    "이전 판 성장 정산을 저장하지 못해 새 도전을 시작하지 않았습니다.");
                return;
            }
            ScoreManager score = ScoreManager.Instance;
            if (score != null && !score.TryPrepareRunBaseline())
            {
                Debug.LogWarning(
                    "최고 기록을 확인하지 못해 새 도전을 시작하지 않았습니다. 잠시 후 다시 시도해 주세요.");
                return;
            }
            GetComponent<FirstRunTutorialController>()?
                .PrepareForGameStart();
            PointerInput.SuppressUntilRelease();
            // 첫 설치 안내는 이미 제작사 화면 아래에서 준비된다. 일반 시작만
            // 성장 왕복·로비 복귀와 같은 먹붓→완전 암전→드러남을 거친다.
            if (behindStartupBrand || !Application.isPlaying || transitionView == null)
            {
                BeginPlayingAfterCover();
                return;
            }
            transitionInProgress = true;
            if (!transitionView.TryPlay(() =>
                {
                    BeginPlayingAfterCover();
                    transitionInProgress = false;
                }, HandleTransitionFailure))
                HandleTransitionFailure();
        }

        /// 이전 씬·테스트와의 호환을 위한 별칭. 로비 드로잉은 더 이상 이 경로를 호출하지 않는다.
        public void StartGameFromStroke() => StartGameFromMenu();

        bool PersistPendingGameOverSettlement()
        {
            ScoreManager score = ScoreManager.Instance;
            if (string.IsNullOrEmpty(currentRunId) || score == null)
                return false;

            // 저장소가 첫 시도에서 예외를 내도 이 판이 실제 게임오버를
            // 거쳤다는 사실은 남긴다. 광고 부활 뒤에는 State가 Playing이라
            // 상태값만으로 정상 플레이와 구분할 수 없다.
            requiresPendingGameOverSettlementRefresh = true;

            var snapshot = new PendingGameOverSettlementSnapshot
            {
                runId = currentRunId,
                swarmProgressHeight = Mathf.FloorToInt(SwarmProgressHeight),
                scoreHeight = Mathf.Max(0, score.Height),
                previousBest = Mathf.Max(0, score.RunBestToBeat),
                activeGameplaySeconds = Mathf.Max(0f, activeGameplaySeconds),
                eligible = score.RecordsAllowed,
            };
            try
            {
                pendingSettlementStore.Save(JsonUtility.ToJson(snapshot));
                hasActivePendingGameOverSettlement = true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 게임오버 정산 복구본을 저장하지 못했습니다. " +
                    "현재 결과 화면은 계속 진행합니다: " + exception.Message);
                return false;
            }
        }

        bool TryAbandonPendingGameOverSettlement()
        {
            if (!TryHasPendingGameOverSettlement(out bool hasPending))
                return false;
            if (hasPending)
            {
                bool belongsToCurrentRun = hasActivePendingGameOverSettlement;
                try
                {
                    PendingGameOverSettlementSnapshot snapshot =
                        JsonUtility.FromJson<PendingGameOverSettlementSnapshot>(
                            pendingSettlementStore.Load());
                    belongsToCurrentRun |=
                        IsValidPendingGameOverSettlement(snapshot) &&
                        !string.IsNullOrEmpty(currentRunId) &&
                        string.Equals(
                            snapshot.runId,
                            currentRunId,
                            StringComparison.Ordinal);
                }
                catch (Exception exception)
                {
                    // 현재 판의 write-after-apply 예외일 수 있으므로 읽을 수 없는
                    // 복구본은 안전하게 구분될 때까지 로비 전환을 막는다.
                    Debug.LogWarning(
                        "[MukJump] 포기할 게임오버 복구본을 확인하지 못했습니다: " +
                        exception.Message);
                    return false;
                }

                bool cleared = ClearPendingGameOverSettlement();
                if (!cleared && belongsToCurrentRun)
                    return false;
                // 이미 복구·정산된 다른 run 또는 손상 key는 멱등/무효다.
                // 삭제 실패만으로 새 판의 명시적 로비 복귀를 가로막지 않는다.
            }

            gameOverPersistenceAbandoned = true;
            gameOverResultSettled = true;
            pendingRestartConfirmationArmed = false;
            hasActivePendingGameOverSettlement = false;
            requiresPendingGameOverSettlementRefresh = false;
            return true;
        }

        static bool TryHasPendingGameOverSettlement(out bool hasPending)
        {
            try
            {
                hasPending = pendingSettlementStore.HasSnapshot();
                return true;
            }
            catch (Exception exception)
            {
                hasPending = false;
                Debug.LogWarning(
                    "[MukJump] 게임오버 정산 복구본 존재 여부를 확인하지 " +
                    "못했습니다: " + exception.Message);
                return false;
            }
        }

        static bool ClearPendingGameOverSettlement()
        {
            try
            {
                pendingSettlementStore.Clear();
                return true;
            }
            catch (Exception exception)
            {
                // 이미 정산된 run ID는 성장 프로필에서도 멱등 처리되므로, 삭제
                // 실패가 게임오버 UI나 다음 판 시작을 중단하게 만들지 않는다.
                Debug.LogWarning(
                    "[MukJump] 게임오버 정산 복구본을 삭제하지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryRecoverPendingGameOverSettlement()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Apps in Toss 사용자 소유권을 확인하기 전에 이전 브라우저 사용자의
            // 정산 복구본을 성장·최고 기록에 반영하지 않는다. 식별 완료 직후와
            // 시작 버튼 경계에서 다시 호출한다.
            if (!AppsInTossIdentityPolicy.HasVerifiedIdentity)
                return true;
#endif
            if (!TryHasPendingGameOverSettlement(out bool hasPending))
                return false;
            if (!hasPending)
                return true;

            string json;
            try
            {
                json = pendingSettlementStore.Load();
            }
            catch (Exception exception)
            {
                // 읽기 실패는 손상으로 단정해 삭제하지 않는다. 다음 실행에서
                // 다시 복구할 수 있도록 원본을 그대로 보존한다.
                Debug.LogWarning(
                    "[MukJump] 게임오버 정산 복구본을 읽지 못했습니다: " +
                    exception.Message);
                return false;
            }
            PendingGameOverSettlementSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<
                    PendingGameOverSettlementSnapshot>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 이전 판 정산 복구본을 읽지 못해 격리합니다: " +
                    exception.Message);
                ClearPendingGameOverSettlement();
                return true;
            }

            if (!IsValidPendingGameOverSettlement(snapshot))
            {
                Debug.LogWarning(
                    "[MukJump] 이전 판 정산 복구본이 손상되어 격리합니다.");
                ClearPendingGameOverSettlement();
                return true;
            }

            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    snapshot.runId,
                    snapshot.swarmProgressHeight,
                    snapshot.scoreHeight,
                    snapshot.previousBest,
                    snapshot.activeGameplaySeconds,
                    snapshot.eligible);
            bool growthSaved = !snapshot.eligible || settlement.Accepted ||
                PermanentGrowthProfile.IsRunSettled(snapshot.runId);
            if (!growthSaved)
                return false;

            bool recordSaved = !snapshot.eligible ||
                (ScoreManager.Instance != null
                    ? ScoreManager.Instance.TryMergeVerifiedBest(
                        snapshot.scoreHeight)
                    : ScoreManager.TryMergeVerifiedBestIntoStore(
                        snapshot.scoreHeight));
            if (!recordSaved)
                return false;

            ClearPendingGameOverSettlement();
            return true;
        }

#if UNITY_EDITOR
        public static void UsePendingGameOverSettlementStoreForTests(
            IPendingGameOverSettlementStore store)
        {
            pendingSettlementStore = store ??
                new PlayerPrefsPendingGameOverSettlementStore();
        }

        public static void RestorePendingGameOverSettlementStoreForTests()
        {
            pendingSettlementStore =
                new PlayerPrefsPendingGameOverSettlementStore();
        }
#endif

        public static bool IsValidPendingGameOverSettlement(
            PendingGameOverSettlementSnapshot snapshot)
        {
            return snapshot != null && snapshot.version == 1 &&
                   Guid.TryParseExact(snapshot.runId, "N", out _) &&
                   snapshot.swarmProgressHeight >= 0 &&
                   snapshot.scoreHeight >= 0 &&
                   snapshot.previousBest >= 0 &&
                   !float.IsNaN(snapshot.activeGameplaySeconds) &&
                   !float.IsInfinity(snapshot.activeGameplaySeconds) &&
                   snapshot.activeGameplaySeconds >= 0f;
        }

        void BeginPlayingAfterCover()
        {
            if (State != GameState.Lobby) return;
            LobbySettingsProfile.MarkGameplayStartedThisSession();

            // 연출 난수와 분리된 게임 규칙 스트림을 판 시작 직전에 함께 초기화한다.
            GameplayRandom.ResetSession();
            currentRunId = Guid.NewGuid().ToString("N");
            AppleGameCenterRuntime.BeginRun();
            activeGameplaySeconds = 0f;
            lastActiveTimeSampleFrame = -1;
            lastDeadPlayer = null;
            reviveUsedThisRun = false;
            reviveRequestInFlight = false;
            reviveCompletionPendingForeground = false;
            pendingReviveRewardEarned = false;
            activeReviveRequestGeneration = 0;
            reviveRequestDeadline = 0f;
            gameOverResultSettled = false;
            hasActivePendingGameOverSettlement = false;
            requiresPendingGameOverSettlementRefresh = false;
            // 새 도전에서만 진행 기준을 초기화한다. 광고 부활의
            // GameOver -> Playing 전환은 같은 run을 이어 가므로 보존해야 한다.
            maxSwarmProgressHeight = 0f;
            SafePreloadAd(
                MonetizationAds.Provider,
                FullScreenAdPlacement.GameOverReviveReward);
            SetState(GameState.Playing);
            var debugScenario = DebugToolsAvailable
                ? GetComponent<DebugShowcaseScenarioController>()
                : null;
            debugScenario?.PrepareSelectedRun(this);
            // 성장 스냅샷이 확정된 뒤 등록된 모든 개체의 체력을 새 최대치로 맞춘다.
            // 정상 로비에는 본체 한 마리뿐이지만 재컴파일·호환 씬의 잔존 분신도 빠뜨리지 않는다.
            GetLivingPlayersNonAlloc(swarmScratch);
            for (int i = 0; i < swarmScratch.Count; i++)
                swarmScratch[i]?.BeginFromLobby();
            var player = HighestLivingPlayer;
            if (player != null)
                ScoreManager.Instance?.ResetOrigin(player.transform.position.y);
            debugScenario?.BeginPreparedRun();
            MukJumpAnalytics.BeginRun(currentRunId,
                !DebugInvincible && ScoreManager.Instance != null && ScoreManager.Instance.RecordsAllowed,
                PermanentGrowthProfile.OwnedNodeCount);
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
            // 씬 정리의 OnDisable 취소는 UI를 다시 여는 복구 상황이 아니다.
            if (!isActiveAndEnabled) return;
            if (IsPaused)
                AudioListener.pause = true;
            // 닫힌 결과창 뒤 씬 전환이 실패해도 메인 버튼을 다시 사용할 수 있어야 한다.
            if (State == GameState.GameOver && gameOverPopupView != null && gameOverPopupView.isActiveAndEnabled)
                gameOverPopupView.Show(latestGameOverResult, false, false);
        }

        bool BeginPause(GameplayPauseReason reason)
        {
            if (reason == GameplayPauseReason.None ||
                State != GameState.Playing)
                return false;

            // 숨겨진 상태에서 시작 안내가 열리면 안내가 정지 소유권을 이어받는다.
            // 따라서 앱 복귀만으로 아직 열린 안내/메뉴 아래의 게임이 움직이지 않는다.
            if (IsPaused)
            {
                if (PauseReason != GameplayPauseReason.ApplicationBackground ||
                    reason == GameplayPauseReason.ApplicationBackground)
                    return false;
                PauseReason = reason;
                NotifyListenersSafely(PauseChanged, true, "일시정지");
                return true;
            }
            // 첫 안내는 덮개가 걷히기 전 Playing 알림 안에서 열린다. 인증이
            // 늦거나 탈퇴 후 재진입해도 전환 중 안내의 물리 정지는 반드시 허용한다.
            // 사용자 메뉴만 전환 입력 잠금을 따른다.
            if (IsTransitioning && reason == GameplayPauseReason.UserMenu)
                return false;

            PointerInput.SuppressUntilRelease();
            FindAnyObjectByType<StrokeCapture>()?.CancelActiveStroke();
            GameFeedbackController.Instance?.PrepareForPause();
            timeScaleBeforePause = Mathf.Max(0.01f, Time.timeScale);
            fixedDeltaBeforePause = Mathf.Max(0.001f, Time.fixedDeltaTime);
            PauseReason = reason;
            IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (reason == GameplayPauseReason.UserMenu) MukJumpAnalytics.Pause(true);
            NotifyListenersSafely(PauseChanged, true, "일시정지");
            return true;
        }

        void RestorePausedWorld(bool notify, bool preserveBackgroundPause = true)
        {
            bool wasPaused = IsPaused;
            // 늦게 도착한 계속하기/튜토리얼 완료 콜백은 백그라운드의 물리를 풀지 않는다.
            if (preserveBackgroundPause && State == GameState.Playing &&
                !MobileApplicationLifecycle.IsApplicationActive)
            {
                IsPaused = true;
                PauseReason = GameplayPauseReason.ApplicationBackground;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                return;
            }
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
            if (PauseReason == GameplayPauseReason.UserMenu) MukJumpAnalytics.Pause(false);
            IsPaused = false;
            PauseReason = GameplayPauseReason.None;
            timeScaleBeforePause = 1f;
            fixedDeltaBeforePause = 0.02f;
            if (notify && wasPaused)
                NotifyListenersSafely(PauseChanged, false, "일시정지");
        }

        void SetState(GameState nextState)
        {
            if (State == nextState) return;
            GameState previousState = State;
            State = nextState;
            // 로비에서 이미 앱이 가려졌다면 이전 visibility 콜백은 Playing에 전달되지
            // 않았다. 상태 진입 시 즉시 반영하고 그다음 튜토리얼 등에 알린다.
            if (nextState == GameState.Playing && !MobileApplicationLifecycle.IsApplicationActive)
                PauseForApplicationBackground();
            NotifyListenersSafely(
                StateChanged,
                previousState,
                nextState,
                "게임 상태");
        }

        void NotifyListenersSafely<T>(
            Action<T> listeners,
            T value,
            string signalName)
        {
            if (listeners == null)
                return;

            foreach (Action<T> listener in listeners.GetInvocationList())
            {
                try
                {
                    listener(value);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[MukJump] {signalName} 알림 구독자 예외를 격리했습니다: " +
                        exception.Message,
                        this);
                }
            }
        }

        void NotifyListenersSafely<TFirst, TSecond>(
            Action<TFirst, TSecond> listeners,
            TFirst first,
            TSecond second,
            string signalName)
        {
            if (listeners == null)
                return;

            foreach (Action<TFirst, TSecond> listener in
                     listeners.GetInvocationList())
            {
                try
                {
                    listener(first, second);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[MukJump] {signalName} 알림 구독자 예외를 격리했습니다: " +
                        exception.Message,
                        this);
                }
            }
        }
    }
}
