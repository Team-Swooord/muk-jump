#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

namespace MukJump.Core
{
    /// 로컬 플레이를 항상 먼저 열고, 설정된 네이티브 빌드에서 뒤끝 게스트 계정과
    /// 비동기 동기화를 붙인다. 네트워크 실패는 게임 진입을 막지 않는다.
    [DisallowMultipleComponent]
    public sealed partial class MukJumpAccountRuntime : MonoBehaviour
    {
        const string KindKey = "MukJump.Account.Kind";
        const string RevisionKey = "MukJump.Cloud.Revision";
        const string PendingSaveKey = "MukJump.Cloud.PendingSave";
        const string PendingOperationIdKey =
            "MukJump.Cloud.PendingOperationId";
        const string LocalGuestSnapshotKey =
            "MukJump.Account.LocalGuestSnapshot";
        const string PendingLocalGuestImportKey =
            "MukJump.Account.PendingLocalGuestImport";
        const string PendingLocalGuestImportOwnerKey =
            "MukJump.Account.PendingLocalGuestImportOwner";
        const string PendingLocalGuestImportRestoredOwnerKey =
            "MukJump.Account.PendingLocalGuestImportRestoredOwner";
        const string PendingProfileResolutionOwnerKey =
            "MukJump.Account.PendingProfileResolutionOwner";
        const string PendingAuthorizedTransitionKindKey =
            "MukJump.Account.PendingAuthorizedTransitionKind";
        const string PendingAuthorizedTransitionPreviousOwnerKey =
            "MukJump.Account.PendingAuthorizedTransitionPreviousOwner";
        const string PendingAuthorizedTransitionReplaceLocalKey =
            "MukJump.Account.PendingAuthorizedTransitionReplaceLocal";
        const string PendingAuthorizedTransitionRestoreGuestKey =
            "MukJump.Account.PendingAuthorizedTransitionRestoreGuest";
        const string PendingGuestUpgradeKindKey =
            "MukJump.Account.PendingGuestUpgradeKind";
        const string PendingLocalLogoutCleanupKey =
            "MukJump.Account.PendingLocalLogoutCleanup";
        const string PendingLocalLogoutRemoteConfirmedKey =
            "MukJump.Account.PendingLocalLogoutRemoteConfirmed";
        const string PendingLocalAccountDeletionCleanupKey =
            "MukJump.Account.PendingLocalAccountDeletionCleanup";
        const string PendingLocalAccountDeletionOwnerKey =
            "MukJump.Account.PendingLocalAccountDeletionOwner";
        const string PendingLocalAccountDeletionRemoteConfirmedKey =
            "MukJump.Account.PendingLocalAccountDeletionRemoteConfirmed";
        const string PendingLocalAccountDeletionFederationClearedKey =
            "MukJump.Account.PendingLocalAccountDeletionFederationCleared";
        const string PendingLocalAccountDeletionAppleRevokeRequiredKey =
            "MukJump.Account.PendingLocalAccountDeletionAppleRevokeRequired";
        // 구버전 1은 전송 여부를 구분하지 않았다. 재실행 시 취소 가능한 상태로
        // 낮추지 않고, 신규 전송 전 상태만 3으로 구분한다.
        const int AppleRevokeMayHaveBeenDispatched = 2;
        const int AppleRevokeNotDispatched = 3;
        const string PendingLeaderboardBestKey =
            "MukJump.Cloud.PendingLeaderboardBest";
        const string PendingLeaderboardOwnerKey =
            "MukJump.Cloud.PendingLeaderboardOwner";
        const string StoredAccountScopeKey =
            "MukJump.Account.LastAuthenticatedOwner";
        const string AutomaticAuthenticationSuppressedKey =
            "MukJump.Account.AutomaticAuthenticationSuppressed";
        const string LegacyGuestCredentialCleanupKey =
            "MukJump.Account.LegacyGuestCredentialCleanup";
        const string InvalidGuestCredentialCleanupKey =
            "MukJump.Account.InvalidGuestCredentialCleanup";
        const float SaveDebounceSeconds = 2f;
        const float InitialRetrySeconds = 5f;
        const float MaximumRetrySeconds = 60f;
        const float AccountRequestWatchdogSeconds = 20f;

        public static MukJumpAccountRuntime Instance { get; private set; }
        // 로그인 테스트 배포는 게스트 → Apple 연동만 제공한다. 기존 계정 데이터는 보존한다.
        public static bool GoogleSignInEnabled => false;

        public MukJumpAccountKind AccountKind { get; private set; } =
            MukJumpAccountKind.LocalGuest;
        public MukJumpAccountPhase Phase { get; private set; } =
            MukJumpAccountPhase.LocalReady;
        public bool IsOnlineAuthenticated { get; private set; }
        public string StatusMessage { get; private set; } =
            "게스트로 바로 플레이할 수 있습니다";
        public bool HasPendingAccountConflict =>
            Phase == MukJumpAccountPhase.NeedsAccountChoice;
        public bool HasPendingSyncConflict =>
            Phase == MukJumpAccountPhase.NeedsSyncChoice &&
            pendingServerSnapshot != null;
        public bool HasPendingAccountDeletionCleanup =>
            accountDeletionCleanupPending;
        public bool IsTemporaryBackendPaused => temporaryBackendPause;
        public bool HasVerifiedAppsInTossIdentity => false;
        public bool CanReturnToLocalGuestDuringAccountSync =>
            !accountDeletionCleanupPending && !federationRequestInFlight;
        public bool HasPendingAuthorizedTransition =>
            PlayerPrefs.HasKey(PendingAuthorizedTransitionKindKey);
        public bool BlocksGameplayForAccountSync
        {
            get
            {
                bool authorizedTransitionPending =
                    HasPendingAuthorizedTransition;
                bool guestUpgradePending =
                    PlayerPrefs.HasKey(PendingGuestUpgradeKindKey);
                bool localGuestImportPending =
                    PlayerPrefs.GetInt(PendingLocalGuestImportKey, 0) != 0;
                bool mayPlayDuringInitialConnection =
                    ShouldAllowLocalGameplayDuringBackendInitialization(
                        backendInitializationInFlight,
                        AccountKind,
                        backendInitializationStoredOwner,
                        authorizedTransitionPending,
                        guestUpgradePending,
                        localGuestImportPending,
                        profileResolutionPending,
                        localLogoutCleanupPending,
                        accountDeletionCleanupPending);
                return Phase == MukJumpAccountPhase.Connecting &&
                       !mayPlayDuringInitialConnection ||
                       HasPendingAccountConflict ||
                       authorizedTransitionPending ||
                       guestUpgradePending ||
                       localGuestImportPending ||
                       cloudLoadInFlight || resumeCloudLoadPending ||
                       HasPendingSyncConflict ||
                       profileResolutionPending ||
                       localLogoutCleanupPending ||
                       accountDeletionCleanupPending ||
                       backendProviderVerificationInFlight ||
                       guestLoginInFlight ||
                       federationRequestInFlight ||
                       providerResolutionBlocked;
            }
        }
        public IReadOnlyList<MukJumpLeaderboardEntry> LeaderboardEntries =>
            leaderboardEntries;
        public string LeaderboardStatus { get; private set; } =
            "최고 고도 순위를 불러오는 중";
        public bool LeaderboardLoading { get; private set; }
        // UUID 기반 게스트 ID 생성·보관은 SDK가 담당한다. 외부 표시에는
        // 게스트→소셜 전환에도 같은 사용자에게 남는 뒤끝 UID를 사용한다.
        public string PlayerId => ReadDisplayPlayerId();
        public string SupportCode => IsOnlineAuthenticated
            ? CurrentAccountScope()
            : string.Empty;

        public event Action StateChanged;

        MukJumpBackendSettings settings;
        string rowInDate = string.Empty;
        string pendingFederationToken = string.Empty;
        FederationType pendingFederationType;
        MukJumpAccountKind pendingFederationKind;
        bool saveInFlight;
        float saveDeadlineRealtime;
        bool cloudLoadInFlight;
        bool resumeCloudLoadPending;
        string cloudLoadAccountScope = string.Empty;
        float cloudLoadDeadlineRealtime;
        bool suppressDirty;
        bool dirty;
        bool replaceLocalFromServerOnNextLoad;
        bool restoreLocalGuestIfServerEmptyOnNextLoad;
        bool profileResolutionPending;
        bool localLogoutCleanupPending;
        bool backendProviderVerificationInFlight;
        float backendProviderVerificationDeadlineRealtime;
        bool providerResolutionBlocked;
        bool temporaryBackendPause;
        float temporaryBackendPauseUntilRealtime;
        bool localLogoutRemoteConfirmed;
        bool accountDeletionCleanupPending;
        bool accountDeletionRemoteConfirmed;
        bool accountDeletionFederationCleared;
        bool accountDeletionAppleRevokeRequired;
        bool accountDeletionRequestInFlight;
        bool syncWriteBlocked;
        bool leaderboardSaveInFlight;
        float leaderboardSaveDeadlineRealtime;
        float leaderboardLoadDeadlineRealtime;
        float saveAtRealtime;
        float leaderboardRetryAtRealtime;
        long revision;
        long localMutationVersion;
        long accountSessionGeneration;
        long tokenLoginGeneration;
        long federationRequestGeneration;
        bool federationRequestInFlight;
        float federationRequestDeadlineRealtime;
        long interactiveFederationGeneration;
        bool interactiveFederationInFlight;
        MukJumpAccountKind interactiveFederationKind;
        float interactiveFederationDeadlineRealtime;
        bool tokenLoginInFlight;
        float tokenLoginDeadlineRealtime;
        bool suppressAutomaticAuthentication;
        bool backendErrorHandlersInstalled;
        bool restoreBackendErrorHandlersOnEnable;
        long backendInitializationGeneration;
        bool backendInitializationInFlight;
        float backendInitializationDeadlineRealtime;
        string backendInitializationStoredOwner = string.Empty;
        long backendLogoutRequestGeneration;
        bool backendLogoutRequestInFlight;
        float backendLogoutRequestDeadlineRealtime;
        long localLogoutFinalizationGeneration;
        bool localLogoutFinalizationInFlight;
        float localLogoutFinalizationDeadlineRealtime;
        long accountDeletionRequestGeneration;
        bool accountDeletionFinalizationInFlight;
        float accountDeletionRequestDeadlineRealtime;
        bool guestLoginInFlight;
        float guestReconnectAtRealtime;
        float guestReconnectDelaySeconds = InitialRetrySeconds;
        float guestLoginDeadlineRealtime;
        long appleDeletionGeneration;
        bool appleDeletionInFlight;
        bool appleDeletionRevokeDispatched;
        long appleDeletionSessionGeneration;
        enum AppleDeletionStage { VerifyingOwner, Authenticating, Revoking }
        AppleDeletionStage appleDeletionStage;
        float appleDeletionDeadlineRealtime;
        string appleDeletionAccountScope = string.Empty;
        MukJumpCloudSnapshot pendingServerSnapshot;
        string pendingServerRowInDate = string.Empty;
        float retryDelaySeconds = InitialRetrySeconds;
        readonly List<MukJumpLeaderboardEntry> leaderboardEntries = new(10);
        bool leaderboardRequested;
        bool leaderboardRefreshQueued;

#if UNITY_EDITOR
        // 실제 SDK를 호출하지 않고 동기 예외·재진입을 검증하기 위한 편집기 전용
        // 경계다. 플레이어 빌드에는 포함되지 않는다.
        Func<MukJumpCloudSnapshot> captureNextSnapshotForTests;
        Action<Action<BackendReturnObject>> initializeBackendForTests;
        Action<Action<BackendReturnObject>> tokenLoginForTests;
        Action<Action<BackendReturnObject>> guestLoginForTests;
        Func<string> storedGuestIdForTests;
        Action<Action<BackendReturnObject>> getUserInfoForTests;
        Action<Action<BackendReturnObject>> changeFederationForTests;
        Action<Action<BackendReturnObject>> authorizeFederationForTests;
        Action<Action<BackendReturnObject>> logoutForTests;
        Action<Action<BackendReturnObject>> withdrawAccountForTests;
        Action clearGuestInfoForTests;
        Action pumpAppleAuthenticationForTests;
        Action<Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>
            getLeaderboardForTests;
        Action<int, Action<BackendReturnObject>> updateLeaderboardForTests;
        Action<Action<BackendReturnObject>> getMyDataForTests;
        Action<Action<BackendReturnObject>> insertGameDataForTests;
        Action<Action<BackendReturnObject>> updateGameDataForTests;
        Action keepDeviceConflictPersistenceForTests;
        Action recoveryMarkerPersistenceForTests;
        Func<string> currentAccountScopeForTests;
        Action<Action<string, string>, Action> appleDeletionCredentialForTests;
        Action<string, Action<BackendReturnObject>> revokeAppleTokenForTests;
#endif

#if UNITY_IOS && !UNITY_EDITOR
        IAppleAuthManager appleAuthManager;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null)
                return;
            var root = new GameObject("MukJumpAccountRuntime");
            DontDestroyOnLoad(root);
            root.AddComponent<MukJumpAccountRuntime>();
        }

        void OnEnable()
        {
            Instance = this;
            PermanentGrowthProfile.Changed -= MarkDirty;
            LobbySettingsProfile.Changed -= MarkDirty;
            PermanentGrowthProfile.Changed += MarkDirty;
            LobbySettingsProfile.Changed += MarkDirty;
            if (restoreBackendErrorHandlersOnEnable)
            {
                restoreBackendErrorHandlersOnEnable = false;
                InstallBackendErrorHandlers();
            }
        }

        void OnDisable()
        {
            CancelIdentityRequest();
            PermanentGrowthProfile.Changed -= MarkDirty;
            LobbySettingsProfile.Changed -= MarkDirty;
            restoreBackendErrorHandlersOnEnable |= backendErrorHandlersInstalled;
            UninstallBackendErrorHandlers();
            // SDK 콜백은 컴포넌트 비활성화/파괴 뒤에도 도착할 수 있다.
            // 미완료 저장·소유자·충돌 선택은 유지하고 요청 세대만 폐기한다.
            accountSessionGeneration++;
            resumeCloudLoadPending |= cloudLoadInFlight;
            cloudLoadInFlight = false;
            saveInFlight = false;
            leaderboardSaveInFlight = false;
            cloudLoadDeadlineRealtime = 0f;
            saveDeadlineRealtime = 0f;
            leaderboardSaveDeadlineRealtime = 0f;
            leaderboardLoadDeadlineRealtime = 0f;
            backendInitializationGeneration++;
            backendInitializationInFlight = false;
            tokenLoginGeneration++;
            tokenLoginInFlight = false;
            guestLoginInFlight = false;
            federationRequestGeneration++;
            federationRequestInFlight = false;
            interactiveFederationGeneration++;
            interactiveFederationInFlight = false;
            interactiveFederationKind = default;
            backendLogoutRequestGeneration++;
            backendLogoutRequestInFlight = false;
            localLogoutFinalizationGeneration++;
            localLogoutFinalizationInFlight = false;
            accountDeletionRequestGeneration++;
            accountDeletionRequestInFlight = false;
            appleDeletionGeneration++;
            appleDeletionInFlight = false;
            appleDeletionRevokeDispatched = false;
            backendProviderVerificationInFlight = false;
            LeaderboardLoading = false;
            leaderboardRequested = false;
            leaderboardRefreshQueued = false;
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (Application.isPlaying)
            {
                _ = MukJumpIdentityProfile.LocalUid;
                _ = MukJumpIdentityProfile.GuestNickname;
            }
            settings = MukJumpBackendSettings.Load();
            revision = Math.Max(0L, ParseLong(PlayerPrefs.GetString(
                RevisionKey,
                "0")));
            AccountKind = ReadStoredKind();
            backendInitializationStoredOwner = PlayerPrefs.GetString(
                StoredAccountScopeKey,
                string.Empty);
            suppressAutomaticAuthentication = PlayerPrefs.GetInt(
                AutomaticAuthenticationSuppressedKey,
                0) != 0;
            // 계정 전환 도중 앱이 종료됐거나 네트워크가 끊겨도 빈 임시
            // 프로필로 플레이해 서버 기록을 덮지 못하게 한다.
            profileResolutionPending = PlayerPrefs.HasKey(
                PendingProfileResolutionOwnerKey);
            localLogoutCleanupPending = PlayerPrefs.GetInt(
                PendingLocalLogoutCleanupKey,
                0) != 0;
            localLogoutRemoteConfirmed = PlayerPrefs.GetInt(
                PendingLocalLogoutRemoteConfirmedKey,
                0) != 0;
            accountDeletionCleanupPending = PlayerPrefs.GetInt(
                PendingLocalAccountDeletionCleanupKey,
                0) != 0;
            accountDeletionRemoteConfirmed = PlayerPrefs.GetInt(
                PendingLocalAccountDeletionRemoteConfirmedKey,
                0) != 0;
            accountDeletionFederationCleared = PlayerPrefs.GetInt(
                PendingLocalAccountDeletionFederationClearedKey,
                0) != 0;
            accountDeletionAppleRevokeRequired = PlayerPrefs.GetInt(
                PendingLocalAccountDeletionAppleRevokeRequiredKey,
                0) != 0;
            dirty = PlayerPrefs.GetInt(PendingSaveKey, 0) != 0;
            localMutationVersion = dirty ? 1L : 0L;
            MigrateLegacyGuestLogout();

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (AppleAuthManager.IsCurrentPlatformSupported)
                    appleAuthManager = new AppleAuthManager(
                        new PayloadDeserializer());
            }
            catch (Exception exception)
            {
                appleAuthManager = null;
                Debug.LogWarning(
                    "[MukJump] Apple 로그인 처리기를 초기화하지 못했습니다: " +
                    exception.Message);
            }
#endif

            if (accountDeletionCleanupPending &&
                accountDeletionRemoteConfirmed)
            {
                SetState(
                    MukJumpAccountPhase.Deleting,
                    "중단된 기기 데이터 삭제를 마무리하고 있습니다");
                CompleteLocalAccountDeletion();
                return;
            }

            if (!CanUseBackend())
            {
                if (accountDeletionCleanupPending)
                {
                    SetState(
                        MukJumpAccountPhase.Error,
                        "네트워크 연결 후 계정 삭제 상태를 다시 확인해 주세요");
                    return;
                }
                if (ShouldBlockLegacySocialFallback(
                        false,
                        false,
                        ReadStoredKind(),
                        PlayerPrefs.GetString(
                            StoredAccountScopeKey,
                            string.Empty)))
                {
                    EnterProviderResolutionBlock(
                        "기존 소셜 계정의 기록 소유자를 먼저 확인해야 합니다. 서버 설정과 네트워크를 확인해 주세요");
                    return;
                }
                SetLocalReady(
                    "서버 설정 전에도 게스트로 모든 콘텐츠를 플레이할 수 있습니다");
                return;
            }

            BeginBackendInitialization();
        }

        void BeginBackendInitialization()
        {
            // 신규 로컬 게스트는 SDK 초기화가 오래 걸려도 먼저 플레이할 수
            // 있다. 콜백 이후의 토큰 로그인·계정 복구는 다시 기존 차단 규칙을
            // 적용해 서버 소유권이 확인되기 전에 데이터를 섞지 않는다.
            backendInitializationInFlight = true;
            backendInitializationDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            long capturedGeneration = ++backendInitializationGeneration;
            SetState(MukJumpAccountPhase.Connecting, "계정 연결 확인 중");
            try
            {
                RequestBackendInitialization(bro =>
                {
                    if (capturedGeneration != backendInitializationGeneration)
                        return;
                    try
                    {
                        HandleInitialized(bro);
                    }
                    catch (Exception exception)
                    {
                        HandleInitializationBoundaryFailure(
                            "서버 초기화 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != backendInitializationGeneration)
                    return;
                HandleInitializationBoundaryFailure(
                    "서버 초기화 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        void Update()
        {
            PollIdentityRequest();
            ContinueRequestedLogout();
            TryReconnectGuest();
            if (leaderboardAwaitingAuthentication && IsOnlineAuthenticated &&
                Phase != MukJumpAccountPhase.Connecting && Phase != MukJumpAccountPhase.Deleting &&
                settings != null && !string.IsNullOrWhiteSpace(settings.AllTimeRankUuid))
                RefreshLeaderboard();
            if (resumeCloudLoadPending && IsOnlineAuthenticated &&
                settings != null && !cloudLoadInFlight && !saveInFlight)
            {
                resumeCloudLoadPending = false;
                // 중단된 조회의 소유자를 SDK의 현재 계정으로 바꾸어 재시도하지 않는다.
                if (!string.IsNullOrWhiteSpace(cloudLoadAccountScope) &&
                    string.Equals(cloudLoadAccountScope, CurrentAccountScope(), StringComparison.Ordinal))
                    LoadCloudSnapshot();
                else
                    EnterProviderResolutionBlock("계정이 변경되었습니다. 계정 연결을 다시 확인해 주세요.");
            }
            try
            {
                PumpAppleAuthentication();
            }
            catch (Exception exception)
            {
                try
                {
                    HandleAppleAuthenticationPumpFailure(exception);
                }
                catch (Exception recoveryException)
                {
                    Debug.LogWarning(
                        "[MukJump] Apple 로그인 처리기 오류 복구에도 실패했습니다: " +
                        recoveryException.Message);
                }
            }
            finally
            {
                // 네이티브 Apple SDK가 반복 예외를 내더라도 저장·광고·탈퇴
                // watchdog이 같은 프레임에 반드시 진행되어 latch를 풀어야 한다.
                ProcessAccountRequestWatchdogs();
            }
            if (ShouldResumeTemporaryBackendWrites(
                    temporaryBackendPause,
                    IsOnlineAuthenticated,
                    Time.realtimeSinceStartup,
                    temporaryBackendPauseUntilRealtime,
                    Phase == MukJumpAccountPhase.Error &&
                    !localLogoutCleanupPending &&
                    !accountDeletionCleanupPending &&
                    !backendProviderVerificationInFlight &&
                    !tokenLoginInFlight &&
                    !profileResolutionPending &&
                    !providerResolutionBlocked))
            {
                temporaryBackendPause = false;
                syncWriteBlocked = false;
                SetState(
                    MukJumpAccountPhase.OnlineReady,
                    "서버 연결을 다시 확인하고 있습니다");
                LoadCloudSnapshot();
            }
            if (dirty && IsOnlineAuthenticated && !saveInFlight &&
                !syncWriteBlocked &&
                Phase != MukJumpAccountPhase.Connecting &&
                Phase != MukJumpAccountPhase.NeedsAccountChoice &&
                Phase != MukJumpAccountPhase.NeedsSyncChoice &&
                Phase != MukJumpAccountPhase.Deleting &&
                Time.realtimeSinceStartup >= saveAtRealtime)
                SaveNow();
            if (IsOnlineAuthenticated &&
                !string.IsNullOrWhiteSpace(rowInDate) &&
                !leaderboardSaveInFlight &&
                Phase != MukJumpAccountPhase.Connecting &&
                Phase != MukJumpAccountPhase.NeedsAccountChoice &&
                Phase != MukJumpAccountPhase.NeedsSyncChoice &&
                Phase != MukJumpAccountPhase.Deleting &&
                Time.realtimeSinceStartup >= leaderboardRetryAtRealtime)
            {
                int pendingBest = PlayerPrefs.GetInt(
                    PendingLeaderboardBestKey,
                    0);
                string accountScope = CurrentAccountScope();
                bool hasPendingBest = PlayerPrefs.HasKey(PendingLeaderboardBestKey);
                if (hasPendingBest &&
                    IsPendingLeaderboardOwnedBy(
                        PlayerPrefs.GetString(
                            PendingLeaderboardOwnerKey,
                            string.Empty),
                        accountScope))
                    SubmitBestHeight(pendingBest);
                else if (hasPendingBest &&
                         !string.IsNullOrWhiteSpace(accountScope))
                {
                    // 이전 계정에서 남은 재시도 값을 새 계정으로 제출하지 않는다.
                    ClearPendingLeaderboardSave();
                }
            }
        }

        void PumpAppleAuthentication()
        {
#if UNITY_EDITOR
            pumpAppleAuthenticationForTests?.Invoke();
#elif UNITY_IOS
            appleAuthManager?.Update();
#endif
        }

        void HandleAppleAuthenticationPumpFailure(Exception exception)
        {
            Debug.LogWarning(
                "[MukJump] Apple 로그인 처리기 오류: " + exception.Message);
#if UNITY_EDITOR
            pumpAppleAuthenticationForTests = null;
#elif UNITY_IOS
            appleAuthManager = null;
#endif
            if (appleDeletionInFlight)
            {
                AbortAppleDeletionVerification(
                    "Apple 계정 확인을 계속할 수 없어 삭제를 중단했습니다",
                    exception);
            }
            if (interactiveFederationInFlight &&
                interactiveFederationKind == MukJumpAccountKind.Apple)
            {
                interactiveFederationInFlight = false;
                interactiveFederationGeneration++;
                interactiveFederationKind = default;
                SetState(
                    MukJumpAccountPhase.Error,
                    "Apple 로그인을 계속할 수 없습니다. 잠시 후 다시 시도해 주세요");
            }
        }

        void ProcessAccountRequestWatchdogs()
        {
            float now = Time.realtimeSinceStartup;
            if (backendInitializationInFlight &&
                now >= backendInitializationDeadlineRealtime)
            {
                backendInitializationGeneration++;
                HandleInitializationBoundaryFailure(
                    "서버 초기화 응답 시간이 초과되었습니다",
                    new TimeoutException("응답 시간 초과"));
            }

            if (tokenLoginInFlight && now >= tokenLoginDeadlineRealtime)
            {
                tokenLoginGeneration++;
                tokenLoginInFlight = false;
                HandleTokenLoginBoundaryFailure(
                    "저장된 계정 로그인 응답 시간이 초과되었습니다",
                    new TimeoutException("응답 시간 초과"));
            }

            if (guestLoginInFlight && now >= guestLoginDeadlineRealtime)
            {
                tokenLoginGeneration++;
                guestLoginInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 게스트 로그인 응답 시간이 초과되었습니다.");
                SetAccountOfflinePreservingKind("오프라인 게스트로 플레이합니다");
            }

            if (backendProviderVerificationInFlight &&
                now >= backendProviderVerificationDeadlineRealtime)
            {
                tokenLoginGeneration++;
                backendProviderVerificationInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 로그인 제공자 확인 응답 시간이 초과되었습니다.");
                EnterProviderResolutionBlock(
                    "계정 종류를 확인하지 못했습니다. 같은 계정으로 다시 연결해 주세요");
            }

            if (federationRequestInFlight &&
                now >= federationRequestDeadlineRealtime)
            {
                federationRequestGeneration++;
                federationRequestInFlight = false;
                EnterAmbiguousFederationTransitionFailure(
                    "계정 전환 응답 시간이 초과되었습니다",
                    new TimeoutException("응답 시간 초과"));
            }

            if (interactiveFederationInFlight &&
                now >= interactiveFederationDeadlineRealtime)
            {
                interactiveFederationInFlight = false;
                interactiveFederationGeneration++;
                interactiveFederationKind = default;
                Debug.LogWarning(
                    "[MukJump] 모바일 계정 로그인 응답 시간이 초과되었습니다.");
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그인 응답이 없어 중단했습니다. 다시 시도해 주세요");
            }

            if (backendLogoutRequestInFlight &&
                now >= backendLogoutRequestDeadlineRealtime)
            {
                backendLogoutRequestInFlight = false;
                backendLogoutRequestGeneration++;
                IsOnlineAuthenticated = false;
                Debug.LogWarning(
                    "[MukJump] 로그아웃 응답 시간이 초과되어 서버 상태를 다시 확인합니다.");
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그아웃 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
            }

            if (localLogoutFinalizationInFlight &&
                now >= localLogoutFinalizationDeadlineRealtime)
            {
                long capturedGeneration = localLogoutFinalizationGeneration;
                Debug.LogWarning(
                    "[MukJump] 기기 로그아웃 응답 시간이 초과되어 로컬 정리를 계속합니다.");
                HandleGoogleFederationSignOut(
                    capturedGeneration,
                    false,
                    "응답 시간 초과");
            }

            if (accountDeletionRequestInFlight &&
                now >= accountDeletionRequestDeadlineRealtime)
            {
                accountDeletionRequestInFlight = false;
                accountDeletionRequestGeneration++;
                IsOnlineAuthenticated = false;
                Debug.LogWarning(
                    "[MukJump] 계정 삭제 응답 시간이 초과되어 서버 상태를 다시 확인합니다.");
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 삭제 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
            }

            if (appleDeletionInFlight &&
                now >= appleDeletionDeadlineRealtime)
            {
                AbortAppleDeletionVerification(
                    "Apple 계정 확인 응답이 없어 삭제를 중단했습니다. 다시 시도해 주세요",
                    new TimeoutException("응답 시간 초과"));
            }

            bool cloudLoadTimedOut = cloudLoadInFlight &&
                                     now >= cloudLoadDeadlineRealtime;
            bool saveTimedOut = saveInFlight &&
                                now >= saveDeadlineRealtime;
            bool leaderboardSaveTimedOut = leaderboardSaveInFlight &&
                                           now >= leaderboardSaveDeadlineRealtime;
            bool leaderboardLoadTimedOut = LeaderboardLoading &&
                                           now >= leaderboardLoadDeadlineRealtime;
            if (!cloudLoadTimedOut && !saveTimedOut &&
                !leaderboardSaveTimedOut && !leaderboardLoadTimedOut)
                return;

            // 한 요청의 timeout에서 세션 세대를 올려 같은 세대의 늦은 콜백을
            // 모두 폐기한다. 함께 진행 중이던 요청도 latch를 풀고 안전하게
            // 재시도해 부분 적용이나 영구 대기를 막는다.
            bool hadCloudOperation = cloudLoadInFlight || saveInFlight;
            bool hadLeaderboardSave = leaderboardSaveInFlight;
            bool hadLeaderboardLoad = LeaderboardLoading;
            // 통신 timeout은 계정 변경이 아니다. 저장 뒤 로그아웃 요청은
            // 같은 계정의 재시도 세대로 이어가되 실제 계정 전환 때는 계속 폐기한다.
            if (logoutAfterSaveRequested && logoutAfterSaveSession == accountSessionGeneration)
                logoutAfterSaveSession++;
            accountSessionGeneration++;
            CancelIdentityRequest();
            cloudLoadInFlight = false;
            saveInFlight = false;
            leaderboardSaveInFlight = false;
            LeaderboardLoading = false;

            if (hadCloudOperation)
            {
                Debug.LogWarning(
                    "[MukJump] 클라우드 저장 응답 시간이 초과되어 다시 시도합니다.");
                KeepSavePending(
                    "서버 저장은 연결 복구 후 다시 시도합니다");
            }
            if (hadLeaderboardSave)
            {
                Debug.LogWarning(
                    "[MukJump] 순위 등록 응답 시간이 초과되어 다시 시도합니다.");
                leaderboardRetryAtRealtime = now + InitialRetrySeconds;
                SetStatus("기록 순위 등록은 연결 복구 후 다시 시도합니다");
            }
            if (hadLeaderboardLoad)
            {
                Debug.LogWarning(
                    "[MukJump] 순위 조회 응답 시간이 초과되었습니다.");
                LeaderboardStatus =
                    "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요";
                NotifyStateChangedSafely();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && dirty)
                SaveNow();
        }

        void OnApplicationQuit()
        {
            if (dirty)
                PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.Save();
        }

        bool CanUseBackend()
        {
            return ShouldEnableBackendForRuntime(
                Application.platform,
                Application.isEditor,
                settings != null && settings.HasRequiredRuntimeValues,
                Debug.isDebugBuild);
        }

        public static bool ShouldEnableBackendForRuntime(
            RuntimePlatform platform,
            bool isEditor,
            bool hasRequiredRuntimeValues,
            bool isDevelopmentBuild = false)
        {
            // 실기기 Development Build도 출시와 같은 게스트·연동 경로를
            // 검증한다. 자동화 에디터 테스트와 토스 WebGL은 접속하지 않는다.
            if (isEditor || !hasRequiredRuntimeValues)
                return false;

            return platform == RuntimePlatform.IPhonePlayer ||
                   platform == RuntimePlatform.Android;
        }

        void TryReconnectGuest()
        {
            if (!ShouldRetryGuestConnection(
                    Phase, ReadStoredKind(), IsOnlineAuthenticated,
                    suppressAutomaticAuthentication,
                    BlocksGameplayForAccountSync || backendInitializationInFlight ||
                    tokenLoginInFlight || guestLoginInFlight ||
                    backendProviderVerificationInFlight || interactiveFederationInFlight,
                    GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby,
                    Application.internetReachability != NetworkReachability.NotReachable,
                    Time.realtimeSinceStartup, guestReconnectAtRealtime) || !CanUseBackend())
                return;
            guestReconnectAtRealtime = Time.realtimeSinceStartup + guestReconnectDelaySeconds;
            guestReconnectDelaySeconds = Mathf.Min(MaximumRetrySeconds, guestReconnectDelaySeconds * 2);
            if (!Backend.IsInitialized)
                BeginBackendInitialization();
            else
            {
                SetState(MukJumpAccountPhase.Connecting, "게스트 계정 연결 중");
                BeginBackendTokenLogin();
            }
        }

        public static bool ShouldRetryGuestConnection(
            MukJumpAccountPhase phase, MukJumpAccountKind storedKind,
            bool authenticated, bool authenticationSuppressed, bool recoveryOrRequestPending,
            bool outsideLobby, bool networkAvailable, float now, float retryAt) =>
            phase == MukJumpAccountPhase.LocalReady &&
            (storedKind == MukJumpAccountKind.LocalGuest || storedKind == MukJumpAccountKind.BackendGuest) &&
            !authenticated && !authenticationSuppressed && !recoveryOrRequestPending &&
            !outsideLobby && networkAvailable && now >= retryAt;

        /// 뒤끝 SDK의 최초 InitializeAsync만 기다리는 신규 로컬 게스트는
        /// 네트워크와 무관하게 먼저 플레이한다. 소유자 흔적이나 중단된 계정
        /// 전환·복구가 하나라도 있으면 기존처럼 반드시 확인을 끝낼 때까지 막는다.
        public static bool ShouldAllowLocalGameplayDuringBackendInitialization(
            bool initializationInFlight,
            MukJumpAccountKind accountKind,
            string storedAccountOwner,
            bool authorizedTransitionPending,
            bool guestUpgradePending,
            bool localGuestImportPending,
            bool profileResolutionPending,
            bool localLogoutCleanupPending,
            bool accountDeletionCleanupPending)
        {
            return initializationInFlight &&
                   accountKind == MukJumpAccountKind.LocalGuest &&
                   string.IsNullOrWhiteSpace(storedAccountOwner) &&
                   !authorizedTransitionPending &&
                   !guestUpgradePending &&
                   !localGuestImportPending &&
                   !profileResolutionPending &&
                   !localLogoutCleanupPending &&
                   !accountDeletionCleanupPending;
        }

        void HandleInitialized(BackendReturnObject bro)
        {
            backendInitializationInFlight = false;
            if (bro != null && bro.IsSuccess())
                InstallBackendErrorHandlers();
            if (accountDeletionCleanupPending)
            {
                if (accountDeletionRemoteConfirmed)
                    CompleteLocalAccountDeletion();
                else if (bro != null && bro.IsSuccess())
                    BeginBackendTokenLogin(explicitRecovery: true);
                else
                    SetState(
                        MukJumpAccountPhase.Error,
                        "계정 삭제 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }
            if (localLogoutCleanupPending &&
                localLogoutRemoteConfirmed)
            {
                SignOutFederationAndFinishLocalLogout();
                return;
            }
            bool pendingProviderRecovery =
                HasPendingProviderAuthenticationRecovery(
                    PlayerPrefs.HasKey(PendingAuthorizedTransitionKindKey),
                    PlayerPrefs.HasKey(PendingGuestUpgradeKindKey));
            if (suppressAutomaticAuthentication &&
                !pendingProviderRecovery)
            {
                SetLocalReady(string.Empty);
                return;
            }
            if (bro == null || !bro.IsSuccess())
            {
                if (ShouldBlockLegacySocialFallback(
                        true,
                        false,
                        ReadStoredKind(),
                        PlayerPrefs.GetString(
                            StoredAccountScopeKey,
                            string.Empty)))
                {
                    EnterProviderResolutionBlock(
                        "기존 소셜 계정의 기록 소유자를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                    return;
                }
                SetLocalReady("서버 연결 없이 게스트로 플레이합니다");
                return;
            }

            BeginBackendTokenLogin(
                explicitRecovery: pendingProviderRecovery);
        }

        void HandleInitializationBoundaryFailure(
            string context,
            Exception exception)
        {
            backendInitializationInFlight = false;
            Debug.LogWarning(
                "[MukJump] " + context + ": " + exception.Message);

            if (accountDeletionCleanupPending)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 삭제 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }
            if (localLogoutCleanupPending)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그아웃 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }

            bool pendingProviderRecovery =
                HasPendingProviderAuthenticationRecovery(
                    PlayerPrefs.HasKey(PendingAuthorizedTransitionKindKey),
                    PlayerPrefs.HasKey(PendingGuestUpgradeKindKey));
            if (pendingProviderRecovery || profileResolutionPending ||
                ShouldBlockLegacySocialFallback(
                    true,
                    false,
                    ReadStoredKind(),
                    PlayerPrefs.GetString(
                        StoredAccountScopeKey,
                        string.Empty)))
            {
                EnterProviderResolutionBlock(
                    "기존 계정의 기록 소유자를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }

            SetLocalReady("서버 연결 없이 게스트로 플레이합니다");
        }

        void InstallBackendErrorHandlers()
        {
            if (backendErrorHandlersInstalled)
                return;

            Backend.ErrorHandler.OnOtherDeviceLoginDetectedError =
                HandleOtherDeviceLoginDetected;
            Backend.ErrorHandler.OnMaintenanceError =
                HandleBackendMaintenance;
            Backend.ErrorHandler.OnTooManyRequestError =
                HandleBackendRequestLimit;
            Backend.ErrorHandler.OnTooManyRequestByLocalError =
                HandleBackendRequestLimit;
            Backend.ErrorHandler.OnDeviceBlockError =
                HandleBackendDeviceBlock;
            backendErrorHandlersInstalled = true;
        }

        void UninstallBackendErrorHandlers()
        {
            if (!backendErrorHandlersInstalled)
                return;

            Backend.ErrorHandler.OnOtherDeviceLoginDetectedError = null;
            Backend.ErrorHandler.OnMaintenanceError = null;
            Backend.ErrorHandler.OnTooManyRequestError = null;
            Backend.ErrorHandler.OnTooManyRequestByLocalError = null;
            Backend.ErrorHandler.OnDeviceBlockError = null;
            backendErrorHandlersInstalled = false;
        }

        void HandleOtherDeviceLoginDetected()
        {
            // 로그인 요청 자체가 반환한 인증 오류는 해당 콜백에서 판별한다.
            // 공통 핸들러가 먼저 요청 세대를 바꾸면 토큰→게스트 복구 응답을 잃는다.
            if (!IsOnlineAuthenticated && (tokenLoginInFlight || guestLoginInFlight))
                return;
            EnterBackendSessionError(
                "로그인 정보가 만료되었거나 다른 기기에서 변경되었습니다. 이 기기에서 다시 로그인해 주세요");
        }

        void HandleBackendMaintenance()
        {
            EnterBackendTemporaryError(
                "서버 점검 중이라 기록 동기화를 잠시 멈췄습니다. 게임은 로컬에서 계속할 수 있습니다");
        }

        void HandleBackendRequestLimit()
        {
            EnterBackendTemporaryError(
                "서버 요청이 잠시 제한되었습니다. 잠시 뒤 같은 계정으로 다시 연결해 주세요");
        }

        void HandleBackendDeviceBlock()
        {
            EnterBackendSessionError(
                "이 기기에서는 서버 계정을 사용할 수 없습니다. 고객센터에 문의해 주세요");
        }

        void EnterBackendSessionError(string message)
        {
            StoreCurrentAccountScope();
            InvalidateAccountScopedOperations(
                clearPendingLeaderboard: false,
                invalidateTokenLogin:
                    ShouldInvalidateTokenLoginForSessionError(
                        accountDeletionCleanupPending,
                        localLogoutCleanupPending));
            IsOnlineAuthenticated = false;
            rowInDate = string.Empty;
            syncWriteBlocked = true;
            if (dirty)
                PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.Save();
            SetState(MukJumpAccountPhase.Error, message);
        }

        void EnterBackendTemporaryError(string message)
        {
            bool keepAuthenticatedIdentity = IsOnlineAuthenticated;
            InvalidateAccountScopedOperations(
                clearPendingLeaderboard: false,
                invalidateTokenLogin: false);
            IsOnlineAuthenticated = keepAuthenticatedIdentity;
            syncWriteBlocked = true;
            temporaryBackendPause = keepAuthenticatedIdentity;
            temporaryBackendPauseUntilRealtime =
                Time.realtimeSinceStartup + retryDelaySeconds;
            retryDelaySeconds = CalculateNextRetryDelay(retryDelaySeconds);
            if (dirty)
                PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.Save();
            SetState(MukJumpAccountPhase.Error, message);
        }

        public static bool ShouldResumeTemporaryBackendWrites(
            bool temporaryPause,
            bool authenticated,
            float nowRealtime,
            float resumeAtRealtime,
            bool accountStateAllowsResume = true) =>
            temporaryPause && authenticated &&
            nowRealtime >= resumeAtRealtime &&
            accountStateAllowsResume;

        public static bool ShouldBlockLogoutDuringTemporaryBackendPause(
            bool temporaryPause,
            bool hasUnsavedChanges) => temporaryPause;

        public static bool ShouldInvalidateTokenLoginForSessionError(
            bool accountDeletionRecoveryPending,
            bool localLogoutRecoveryPending) =>
            !accountDeletionRecoveryPending && !localLogoutRecoveryPending;

        void MigrateLegacyGuestLogout()
        {
            // 정상 로그아웃을 끝낸 구버전 설치만 자동 게스트 정책으로 이관한다.
            bool pendingRecovery = false;
            foreach (string key in new[] { PendingLocalLogoutCleanupKey, PendingLocalAccountDeletionCleanupKey,
                PendingProfileResolutionOwnerKey, PendingAuthorizedTransitionKindKey, PendingGuestUpgradeKindKey,
                PendingLocalGuestImportKey, PendingLocalGuestImportOwnerKey })
                pendingRecovery |= PlayerPrefs.HasKey(key);
            if (!ShouldMigrateLegacyGuestLogout(suppressAutomaticAuthentication, ReadStoredKind(),
                PlayerPrefs.GetString(StoredAccountScopeKey, string.Empty), pendingRecovery)) return;
            PlayerPrefs.SetInt(LegacyGuestCredentialCleanupKey, 1);
            SetAutomaticAuthenticationSuppressed(false);
        }

        public static bool ShouldMigrateLegacyGuestLogout(bool suppressed, MukJumpAccountKind kind,
            string owner, bool pendingRecovery) => suppressed && kind == MukJumpAccountKind.LocalGuest &&
            string.IsNullOrWhiteSpace(owner) && !pendingRecovery;

        void BeginBackendTokenLogin(bool explicitRecovery = false)
        {
            // CompleteLocalLogout 도중 로컬 복원이 실패하면 자동 재인증은
            // 계속 막되, 사용자가 누른 '다시 시도'만은 남은 원격 세션을
            // 확인할 수 있어야 한다.
            if (!CanBeginTokenLogin(
                    suppressAutomaticAuthentication,
                    explicitRecovery,
                    tokenLoginInFlight))
                return;
            if (PlayerPrefs.GetInt(InvalidGuestCredentialCleanupKey, 0) != 0 &&
                CanRecoverInvalidGuestCredentials())
            {
                if (TryCompleteInvalidGuestCredentialCleanup())
                    BeginBackendGuestLogin();
                return;
            }
            if (PlayerPrefs.GetInt(LegacyGuestCredentialCleanupKey, 0) != 0)
            {
                try
                {
                    // 이전 연동 계정의 게스트 자격으로 되돌아가지 않는다.
                    Backend.BMember.DeleteGuestInfo();
                    PlayerPrefs.DeleteKey(LegacyGuestCredentialCleanupKey);
                    PlayerPrefs.Save();
                    BeginBackendGuestLogin();
                }
                catch (Exception)
                {
                    SetLocalReady("게스트 연결을 다시 시도합니다");
                }
                return;
            }
            tokenLoginInFlight = true;
            tokenLoginDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            long capturedGeneration = ++tokenLoginGeneration;
            try
            {
                RequestBackendTokenLogin(bro =>
                {
                    if (capturedGeneration != tokenLoginGeneration)
                        return;
                    tokenLoginInFlight = false;
                    try
                    {
                        HandleTokenLogin(bro);
                    }
                    catch (Exception exception)
                    {
                        HandleTokenLoginBoundaryFailure(
                            "저장된 계정 로그인 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != tokenLoginGeneration)
                    return;
                tokenLoginInFlight = false;
                HandleTokenLoginBoundaryFailure(
                    "저장된 계정 로그인 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        public static bool CanBeginTokenLogin(
            bool automaticAuthenticationSuppressed,
            bool explicitRecovery,
            bool loginInFlight) =>
            !loginInFlight &&
            (explicitRecovery || !automaticAuthenticationSuppressed);

        public static bool HasPendingProviderAuthenticationRecovery(
            bool authorizedTransitionPending,
            bool guestUpgradePending) =>
            authorizedTransitionPending || guestUpgradePending;

        public static bool ShouldBlockPendingProviderTransitionAfterTokenFailure(
            bool authorizedTransitionPending,
            bool guestUpgradePending) =>
            HasPendingProviderAuthenticationRecovery(
                authorizedTransitionPending,
                guestUpgradePending);

        public static bool ShouldStartGuestLoginAfterTokenFailure(
            bool profileResolutionPending,
            bool authorizedTransitionPending,
            bool guestUpgradePending,
            MukJumpAccountKind persistedAccountKind,
            bool tokenDefinitivelyUnavailable,
            bool hasStoredGuestCredentials = false) =>
            !profileResolutionPending && !authorizedTransitionPending &&
            !guestUpgradePending && tokenDefinitivelyUnavailable &&
            (persistedAccountKind == MukJumpAccountKind.LocalGuest ||
             (persistedAccountKind == MukJumpAccountKind.BackendGuest && hasStoredGuestCredentials));

        void InvalidateBackendTokenLogin()
        {
            tokenLoginGeneration++;
            tokenLoginInFlight = false;
            guestLoginInFlight = false;
            backendProviderVerificationInFlight = false;
        }

        void HandleTokenLogin(BackendReturnObject bro)
        {
            if (accountDeletionCleanupPending)
            {
                ResumePendingAccountDeletion(bro);
                return;
            }
            if (localLogoutCleanupPending)
            {
                ResumePendingLocalLogout(bro);
                return;
            }
            if (bro != null && bro.IsSuccess())
            {
                VerifyCurrentBackendProvider();
                return;
            }

            if (profileResolutionPending)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "전환 중인 계정을 확인하지 못했습니다. 다시 시도하거나 로컬 게스트로 돌아가 주세요");
                return;
            }

            bool authorizedTransitionPending = PlayerPrefs.HasKey(
                PendingAuthorizedTransitionKindKey);
            bool tokenDefinitivelyUnavailable =
                IsDefinitiveTokenUnavailable(
                    bro?.GetStatusCode(),
                    bro?.GetErrorCode(),
                    bro?.GetMessage());
            if (RequiresLegacySocialOwnershipVerification(
                    ReadStoredKind(),
                    PlayerPrefs.GetString(
                        StoredAccountScopeKey,
                        string.Empty)))
            {
                EnterProviderResolutionBlock(
                    "기존 소셜 계정의 기록 소유자를 먼저 확인해야 합니다. 같은 계정으로 다시 로그인해 주세요");
                return;
            }
            if (!ShouldStartGuestLoginAfterTokenFailure(
                    profileResolutionPending,
                    authorizedTransitionPending,
                    PlayerPrefs.HasKey(PendingGuestUpgradeKindKey),
                    ReadStoredKind(),
                    tokenDefinitivelyUnavailable,
                    ReadStoredKind() == MukJumpAccountKind.BackendGuest &&
                    tokenDefinitivelyUnavailable &&
                    !string.IsNullOrWhiteSpace(ReadStoredGuestId())))
            {
                if (profileResolutionPending ||
                    ShouldBlockPendingProviderTransitionAfterTokenFailure(
                        authorizedTransitionPending,
                        PlayerPrefs.HasKey(PendingGuestUpgradeKindKey)))
                    EnterProviderResolutionBlock(
                        "계정 전환 결과를 확인하지 못했습니다. 게스트를 새로 만들지 않고 다시 확인합니다");
                else
                    SetAccountOfflinePreservingKind(
                        tokenDefinitivelyUnavailable
                            ? "저장된 계정 로그인이 만료되었습니다. 같은 계정으로 다시 연결해 주세요"
                            : "서버 응답을 확인하지 못해 로컬 게스트로 플레이합니다");
                return;
            }

            BeginBackendGuestLogin();
        }

        void BeginBackendGuestLogin()
        {
            if (guestLoginInFlight) return;
            // 복구 중 이전 요청의 중복/지연 콜백이 새 게스트 요청을 완료시키지 않는다.
            long capturedGeneration = ++tokenLoginGeneration;
            guestLoginInFlight = true;
            guestLoginDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                bool hadStoredCredentials = !string.IsNullOrWhiteSpace(ReadStoredGuestId());
                RequestBackendGuestLogin(guestBro =>
                {
                    if (capturedGeneration != tokenLoginGeneration || !guestLoginInFlight)
                        return;
                    guestLoginInFlight = false;
                    try
                    {
                        if (guestBro != null && guestBro.IsSuccess())
                        {
                            // 토큰 만료 후 기존 게스트 재로그인도 소유자를 검증한다.
                            // SDK 자격 정보가 다른 계정이면 현재 성장을 절대 업로드하지 않는다.
                            CompleteVerifiedTokenLogin(MukJumpAccountKind.BackendGuest);
                        }
                        else if (hadStoredCredentials && CanRecoverInvalidGuestCredentials() &&
                                 IsInvalidGuestCredentialResponse(guestBro?.GetStatusCode(),
                                     guestBro?.GetErrorCode(), guestBro?.GetMessage()))
                        {
                            // 서버에서 사라졌거나 더 이상 게스트로 사용할 수 없는 자격만
                            // 정리한다. 점수/성장/튜토리얼/기기 이름은 절대 초기화하지 않는다.
                            if (TryCompleteInvalidGuestCredentialCleanup())
                                BeginBackendGuestLogin();
                        }
                        else
                            SetAccountOfflinePreservingKind("오프라인 게스트로 플레이합니다");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[MukJump] 게스트 로그인 결과를 처리하지 못했습니다: " +
                            exception.Message);
                        SetAccountOfflinePreservingKind("오프라인 게스트로 플레이합니다");
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != tokenLoginGeneration)
                    return;
                guestLoginInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 게스트 로그인 요청을 시작하지 못했습니다: " +
                    exception.Message);
                SetAccountOfflinePreservingKind("오프라인 게스트로 플레이합니다");
            }
        }

        string ReadStoredGuestId()
        {
#if UNITY_EDITOR
            if (storedGuestIdForTests != null) return storedGuestIdForTests();
            // 격리된 요청 테스트는 개발자 기기의 실제 SDK 자격을 읽지 않는다.
            if (guestLoginForTests != null) return string.Empty;
#endif
            return Backend.BMember.GetGuestID();
        }

        public static bool IsInvalidGuestCredentialResponse(string status, string error, string message) =>
            status?.Trim() == "401" &&
            string.Equals(error?.Trim(), "BadUnauthorizedException", StringComparison.OrdinalIgnoreCase) &&
            (message?.Trim().StartsWith("bad customId", StringComparison.OrdinalIgnoreCase) ?? false);

        bool CanRecoverInvalidGuestCredentials() =>
            (ReadStoredKind() == MukJumpAccountKind.LocalGuest || ReadStoredKind() == MukJumpAccountKind.BackendGuest) &&
            !IsOnlineAuthenticated && !suppressAutomaticAuthentication &&
            !profileResolutionPending && !HasPendingAuthorizedTransition &&
            !PlayerPrefs.HasKey(PendingGuestUpgradeKindKey) &&
            !PlayerPrefs.HasKey(PendingProfileResolutionOwnerKey) &&
            PlayerPrefs.GetInt(PendingLocalGuestImportKey, 0) == 0 &&
            !localLogoutCleanupPending && !accountDeletionCleanupPending &&
            !federationRequestInFlight && !interactiveFederationInFlight && !providerResolutionBlocked;

        bool TryCompleteInvalidGuestCredentialCleanup()
        {
            if (!CanRecoverInvalidGuestCredentials())
            {
                EnterProviderResolutionBlock("계정 전환 결과를 확인하지 못했습니다. 게스트를 새로 만들지 않고 다시 확인합니다");
                return false;
            }
            try
            {
                // 자격 삭제 또는 저장 중 종료되어도 새 로그인 전에 이 단계부터 재개한다.
                // 복구 표식을 디스크에 먼저 확정한 뒤 SDK 로컬 정보만 지운다.
                PlayerPrefs.SetInt(InvalidGuestCredentialCleanupKey, 1);
                FlushRecoveryMarkers();
                ClearBackendGuestInfo();
                if (!string.IsNullOrWhiteSpace(ReadStoredGuestId()))
                    throw new InvalidOperationException("게스트 로그인 정보가 기기에 남아 있습니다");
                InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
                rowInDate = string.Empty;
                revision = 0;
                replaceLocalFromServerOnNextLoad = false;
                restoreLocalGuestIfServerEmptyOnNextLoad = false;
                PlayerPrefs.DeleteKey(StoredAccountScopeKey);
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
                PlayerPrefs.DeleteKey(PendingLeaderboardOwnerKey);
                PlayerPrefs.DeleteKey(PendingLeaderboardBestKey);
                PlayerPrefs.SetString(RevisionKey, "0");
                PlayerPrefs.SetInt(KindKey, (int)MukJumpAccountKind.LocalGuest);
                PlayerPrefs.SetInt(PendingSaveKey, 1);
                dirty = true;
                localMutationVersion++;
                // 이전 서버 행/순위 식별자는 재사용하지 않는다. 새 서버 조회 후 현재
                // 기기의 최신 스냅샷을 저장하고, 완료한 판이 있을 때만 순위를 제출한다.
                PlayerPrefs.DeleteKey(InvalidGuestCredentialCleanupKey);
                FlushRecoveryMarkers();
                AccountKind = MukJumpAccountKind.LocalGuest;
                SetState(MukJumpAccountPhase.Connecting, "게스트 계정 연결 중");
                return true;
            }
            catch (Exception exception)
            {
                PlayerPrefs.SetInt(InvalidGuestCredentialCleanupKey, 1);
                Debug.LogWarning("[MukJump] 유효하지 않은 게스트 로그인 정보 정리를 다시 시도합니다: " + exception.Message);
                SetAccountOfflinePreservingKind("게스트 연결을 다시 시도합니다");
                return false;
            }
        }

        void HandleTokenLoginBoundaryFailure(
            string context,
            Exception exception)
        {
            tokenLoginInFlight = false;
            Debug.LogWarning(
                "[MukJump] " + context + ": " + exception.Message);

            if (accountDeletionCleanupPending)
            {
                IsOnlineAuthenticated = false;
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 삭제 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }
            if (localLogoutCleanupPending)
            {
                IsOnlineAuthenticated = false;
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그아웃 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }

            bool pendingProviderRecovery =
                HasPendingProviderAuthenticationRecovery(
                    PlayerPrefs.HasKey(PendingAuthorizedTransitionKindKey),
                    PlayerPrefs.HasKey(PendingGuestUpgradeKindKey));
            if (profileResolutionPending || pendingProviderRecovery ||
                RequiresLegacySocialOwnershipVerification(
                    ReadStoredKind(),
                    PlayerPrefs.GetString(
                        StoredAccountScopeKey,
                        string.Empty)))
            {
                EnterProviderResolutionBlock(
                    "계정 전환 결과를 확인하지 못했습니다. 같은 계정으로 다시 확인해 주세요");
                return;
            }

            SetAccountOfflinePreservingKind(
                "서버 응답을 확인하지 못해 로컬 게스트로 플레이합니다");
        }

        void VerifyCurrentBackendProvider()
        {
            long capturedGeneration = tokenLoginGeneration;
            backendProviderVerificationInFlight = true;
            backendProviderVerificationDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestBackendUserInfo(bro =>
                {
                    if (capturedGeneration != tokenLoginGeneration)
                        return;
                    backendProviderVerificationInFlight = false;

                    try
                    {
                        if (bro == null || !bro.IsSuccess())
                        {
                            EnterProviderResolutionBlock(
                                "계정 종류를 확인하지 못했습니다. 같은 계정으로 다시 연결해 주세요");
                            return;
                        }

                        JsonData root = bro.GetReturnValuetoJSON();
                        JsonData row = root != null && root.IsObject &&
                                       root.ContainsKey("row")
                            ? root["row"]
                            : null;
                        if (!TryResolveAccountKind(
                                ReadString(row, "subscriptionType"),
                                out MukJumpAccountKind verifiedKind))
                        {
                            EnterProviderResolutionBlock(
                                "지원하지 않는 계정 종류라 서버 동기화를 시작하지 않았습니다. 고객센터에 문의해 주세요");
                            return;
                        }

                        CompleteVerifiedTokenLogin(verifiedKind);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[MukJump] 로그인 제공자 확인 실패: " +
                            exception.Message);
                        EnterProviderResolutionBlock(
                            "계정 종류를 확인하지 못했습니다. 같은 계정으로 다시 연결해 주세요");
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != tokenLoginGeneration)
                    return;
                backendProviderVerificationInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 로그인 제공자 확인 요청을 시작하지 못했습니다: " +
                    exception.Message);
                EnterProviderResolutionBlock(
                    "계정 종류를 확인하지 못했습니다. 같은 계정으로 다시 연결해 주세요");
            }
        }

        void CompleteVerifiedTokenLogin(MukJumpAccountKind verifiedKind)
        {
            providerResolutionBlocked = false;
            if (PlayerPrefs.HasKey(PendingAuthorizedTransitionKindKey))
            {
                int rawPendingKind = PlayerPrefs.GetInt(
                    PendingAuthorizedTransitionKindKey,
                    -1);
                if (!Enum.IsDefined(
                        typeof(MukJumpAccountKind),
                        rawPendingKind) ||
                    (MukJumpAccountKind)rawPendingKind != verifiedKind)
                {
                    IsOnlineAuthenticated = false;
                    syncWriteBlocked = true;
                    providerResolutionBlocked = true;
                    SetState(
                        MukJumpAccountPhase.Error,
                        "전환하려던 계정과 서버 로그인 계정이 달라 기록 동기화를 중단했습니다");
                    return;
                }

                AccountKind = verifiedKind;
                if (TryResumePendingAuthorizedTransition())
                    return;
            }

            if (PlayerPrefs.HasKey(PendingGuestUpgradeKindKey))
            {
                int rawUpgradeKind = PlayerPrefs.GetInt(
                    PendingGuestUpgradeKindKey,
                    -1);
                bool validUpgradeKind = Enum.IsDefined(
                    typeof(MukJumpAccountKind),
                    rawUpgradeKind);
                var expectedKind = validUpgradeKind
                    ? (MukJumpAccountKind)rawUpgradeKind
                    : MukJumpAccountKind.LocalGuest;
                if (verifiedKind == expectedKind)
                {
                    CommitConfirmedSocialAuthentication(verifiedKind);
                    ClearPendingGuestUpgrade();
                    CompleteAuthentication("중단된 계정 연결을 복구했습니다");
                    MarkDirty();
                    return;
                }
                if (verifiedKind != MukJumpAccountKind.BackendGuest)
                {
                    IsOnlineAuthenticated = false;
                    syncWriteBlocked = true;
                    providerResolutionBlocked = true;
                    SetState(
                        MukJumpAccountPhase.Error,
                        "연결하려던 계정과 서버 로그인 계정이 달라 기록 동기화를 중단했습니다");
                    return;
                }

                AccountKind = MukJumpAccountKind.BackendGuest;
                CompleteAuthentication(
                    "중단된 계정 연결을 확인하려면 같은 로그인 버튼을 다시 눌러 주세요");
                return;
            }

            string storedOwner = PlayerPrefs.GetString(
                StoredAccountScopeKey,
                string.Empty);
            string currentOwner = CurrentAccountScope();
            if (ShouldBlockLegacyProviderMismatch(
                    ReadStoredKind(),
                    verifiedKind,
                    storedOwner))
            {
                EnterProviderResolutionBlock(
                    "기존 계정과 현재 로그인 방식이 달라 기록 동기화를 중단했습니다. 같은 계정으로 다시 로그인해 주세요");
                return;
            }
            if (ShouldBlockVerifiedTokenOwnerMismatch(
                    storedOwner,
                    currentOwner))
            {
                EnterProviderResolutionBlock(
                    "확인된 계정과 현재 로그인 계정이 달라 기록 동기화를 중단했습니다. 같은 계정으로 다시 로그인해 주세요");
                return;
            }

            AccountKind = verifiedKind;
            CompleteAuthentication("계정 연결 완료");
        }

        public static bool ShouldBlockVerifiedTokenOwnerMismatch(
            string storedOwner,
            string currentOwner)
        {
            string current = currentOwner?.Trim() ?? string.Empty;
            if (current.Length == 0)
                return true;
            string stored = storedOwner?.Trim() ?? string.Empty;
            return stored.Length > 0 &&
                   !string.Equals(
                       stored,
                       current,
                       StringComparison.Ordinal);
        }

        public static bool ShouldBlockLegacyProviderMismatch(
            MukJumpAccountKind persistedKind,
            MukJumpAccountKind verifiedKind,
            string storedOwner) =>
            string.IsNullOrWhiteSpace(storedOwner) &&
            ShouldClearAutomaticAuthenticationSuppression(persistedKind) &&
            persistedKind != verifiedKind;

        public static bool TryResolveAccountKind(
            string subscriptionType,
            out MukJumpAccountKind accountKind)
        {
            string normalized = subscriptionType?.Trim() ?? string.Empty;
            if (string.Equals(
                    normalized,
                    "customSignUp",
                    StringComparison.OrdinalIgnoreCase))
            {
                accountKind = MukJumpAccountKind.BackendGuest;
                return true;
            }
            if (string.Equals(
                    normalized,
                    "google",
                    StringComparison.OrdinalIgnoreCase))
            {
                accountKind = MukJumpAccountKind.Google;
                return true;
            }
            if (string.Equals(
                    normalized,
                    "apple",
                    StringComparison.OrdinalIgnoreCase))
            {
                accountKind = MukJumpAccountKind.Apple;
                return true;
            }

            accountKind = MukJumpAccountKind.LocalGuest;
            return false;
        }

        void CompleteAuthentication(string message)
        {
            MukJumpAnalytics.Account(AccountKind == MukJumpAccountKind.BackendGuest
                ? AnalyticsAccountAction.GuestLogin : AnalyticsAccountAction.Login, AnalyticsOutcome.Success);
            guestReconnectDelaySeconds = InitialRetrySeconds;
            IsOnlineAuthenticated = true;
            // 사용자가 직접 소셜 인증을 마친 경우에도 이전 게스트 복구 표식을
            // 남겨 두어 다음 재실행의 올바른 토큰 로그인을 가로막지 않는다.
            PlayerPrefs.DeleteKey(InvalidGuestCredentialCleanupKey);
            backendProviderVerificationInFlight = false;
            providerResolutionBlocked = false;
            temporaryBackendPause = false;
            StoreCurrentAccountScope();
            BeginAuthenticatedAccountSession();
            retryDelaySeconds = InitialRetrySeconds;
            StoreKind();
            if (ShouldClearAutomaticAuthenticationSuppression(AccountKind))
                SetAutomaticAuthenticationSuppressed(false);
            if (profileResolutionPending &&
                !IsPendingProfileResolutionOwnedBy(
                    PlayerPrefs.GetString(
                        PendingProfileResolutionOwnerKey,
                        string.Empty),
                    CurrentAccountScope()))
            {
                syncWriteBlocked = true;
                SetState(
                    MukJumpAccountPhase.Error,
                    "전환 중인 계정과 현재 로그인 계정이 달라 기록 동기화를 중단했습니다");
                return;
            }
            SetState(MukJumpAccountPhase.OnlineReady, message);
            RefreshAuthenticatedIdentity();
            LoadCloudSnapshot();
        }

        void CommitConfirmedSocialAuthentication(
            MukJumpAccountKind kind)
        {
            AccountKind = kind;
            suppressAutomaticAuthentication = false;
            PlayerPrefs.SetInt(KindKey, (int)kind);
            PlayerPrefs.DeleteKey(AutomaticAuthenticationSuppressedKey);
            string accountScope = CurrentAccountScope();
            if (!string.IsNullOrWhiteSpace(accountScope))
                PlayerPrefs.SetString(
                    StoredAccountScopeKey,
                    accountScope.Trim());
            PlayerPrefs.Save();
        }

        void BeginAuthenticatedAccountSession()
        {
            leaderboardSubmissionFailure = string.Empty;
            leaderboardRequested = false;
            leaderboardRefreshQueued = false;
            CancelIdentityRequest();
            resumeCloudLoadPending = false;
            cloudLoadAccountScope = string.Empty;
            accountSessionGeneration++;
            cloudLoadInFlight = false;
            saveInFlight = false;
            leaderboardSaveInFlight = false;
            LeaderboardLoading = false;
            ClearPendingSyncConflict();
            syncWriteBlocked = false;
            string accountScope = CurrentAccountScope();
            string pendingProfileOwner = PlayerPrefs.GetString(
                PendingProfileResolutionOwnerKey,
                string.Empty);
            bool pendingProfileOwnedByCurrentAccount =
                IsPendingProfileResolutionOwnedBy(
                    pendingProfileOwner,
                    accountScope);
            profileResolutionPending =
                !string.IsNullOrWhiteSpace(pendingProfileOwner);
            if (pendingProfileOwnedByCurrentAccount)
                replaceLocalFromServerOnNextLoad = true;
            restoreLocalGuestIfServerEmptyOnNextLoad =
                IsPendingLocalGuestImportOwnedBy(
                    PlayerPrefs.GetInt(PendingLocalGuestImportKey, 0) != 0,
                    PlayerPrefs.GetString(
                        PendingLocalGuestImportOwnerKey,
                        string.Empty),
                    accountScope);
            leaderboardRetryAtRealtime =
                Time.realtimeSinceStartup + InitialRetrySeconds;

            int pendingBest = PlayerPrefs.GetInt(
                PendingLeaderboardBestKey,
                0);
            if (pendingBest > 0 &&
                !string.IsNullOrWhiteSpace(accountScope) &&
                !IsPendingLeaderboardOwnedBy(
                    PlayerPrefs.GetString(
                        PendingLeaderboardOwnerKey,
                        string.Empty),
                    accountScope))
            {
                ClearPendingLeaderboardSave();
            }
        }

        void InvalidateAccountScopedOperations(
            bool clearPendingLeaderboard,
            bool invalidateTokenLogin = true)
        {
            leaderboardSubmissionFailure = string.Empty;
            leaderboardRequested = false;
            leaderboardRefreshQueued = false;
            CancelIdentityRequest();
            resumeCloudLoadPending = false;
            cloudLoadAccountScope = string.Empty;
            if (invalidateTokenLogin)
                InvalidateBackendTokenLogin();
            federationRequestGeneration++;
            federationRequestInFlight = false;
            backendLogoutRequestGeneration++;
            backendLogoutRequestInFlight = false;
            backendProviderVerificationInFlight = false;
            accountSessionGeneration++;
            cloudLoadInFlight = false;
            saveInFlight = false;
            leaderboardSaveInFlight = false;
            LeaderboardLoading = false;
            ClearPendingSyncConflict();
            syncWriteBlocked = false;
            leaderboardRetryAtRealtime = 0f;
            if (clearPendingLeaderboard)
                ClearPendingLeaderboardSave();
        }

        string CurrentAccountScope()
        {
#if UNITY_EDITOR
            if (currentAccountScopeForTests != null)
                return currentAccountScopeForTests()?.Trim() ?? string.Empty;
#endif
            return Backend.UserInDate?.Trim() ?? string.Empty;
        }

        string KnownAccountScope()
        {
            string current = CurrentAccountScope();
            return current.Length > 0
                ? current
                : PlayerPrefs.GetString(
                    StoredAccountScopeKey,
                    string.Empty).Trim();
        }

        void StoreCurrentAccountScope()
        {
            string accountScope = CurrentAccountScope();
            string storedScope = PlayerPrefs.GetString(
                StoredAccountScopeKey,
                string.Empty);
            if (!ShouldStoreVerifiedAccountScope(
                    storedScope,
                    accountScope))
                return;
            PlayerPrefs.SetString(StoredAccountScopeKey, accountScope);
            PlayerPrefs.Save();
        }

        public static bool ShouldStoreVerifiedAccountScope(
            string storedOwner,
            string currentOwner)
        {
            string current = currentOwner?.Trim() ?? string.Empty;
            if (current.Length == 0)
                return false;
            string stored = storedOwner?.Trim() ?? string.Empty;
            return stored.Length == 0 ||
                   string.Equals(
                       stored,
                       current,
                       StringComparison.Ordinal);
        }

        void ClearStoredAccountScope()
        {
            PlayerPrefs.DeleteKey(StoredAccountScopeKey);
            PlayerPrefs.Save();
        }

        bool IsCurrentAccountSession(
            long capturedGeneration,
            string capturedAccountScope)
        {
            return this != null && isActiveAndEnabled &&
                ReferenceEquals(Instance, this) && IsSameAccountSession(
                capturedGeneration,
                accountSessionGeneration,
                capturedAccountScope,
                CurrentAccountScope(),
                IsOnlineAuthenticated);
        }

        public static bool IsSameAccountSession(
            long capturedGeneration,
            long currentGeneration,
            string capturedAccountScope,
            string currentAccountScope,
            bool authenticated)
        {
            return authenticated &&
                   capturedGeneration == currentGeneration &&
                   string.Equals(
                       capturedAccountScope?.Trim() ?? string.Empty,
                       currentAccountScope?.Trim() ?? string.Empty,
                       StringComparison.Ordinal);
        }

        public static bool IsPendingLeaderboardOwnedBy(
            string storedOwner,
            string currentAccountScope)
        {
            string stored = storedOwner?.Trim() ?? string.Empty;
            string current = currentAccountScope?.Trim() ?? string.Empty;
            return stored.Length > 0 &&
                   string.Equals(stored, current, StringComparison.Ordinal);
        }

        public static bool IsPendingLocalGuestImportOwnedBy(
            bool pending,
            string storedOwner,
            string currentAccountScope)
        {
            if (!pending)
                return false;

            string stored = storedOwner?.Trim() ?? string.Empty;
            string current = currentAccountScope?.Trim() ?? string.Empty;
            return stored.Length > 0 &&
                   string.Equals(stored, current, StringComparison.Ordinal);
        }

        public static bool IsPendingLocalGuestImportRestoredForOwner(
            string restoredOwner,
            string currentAccountScope)
        {
            string restored = restoredOwner?.Trim() ?? string.Empty;
            string current = currentAccountScope?.Trim() ?? string.Empty;
            return restored.Length > 0 &&
                   string.Equals(restored, current, StringComparison.Ordinal);
        }

        public static bool ShouldRestorePendingLocalGuestImport(
            bool pendingForCurrentAccount,
            bool alreadyRestoredForCurrentAccount) =>
            pendingForCurrentAccount && !alreadyRestoredForCurrentAccount;

        public static bool IsPendingProfileResolutionOwnedBy(
            string storedOwner,
            string currentAccountScope)
        {
            string stored = storedOwner?.Trim() ?? string.Empty;
            string current = currentAccountScope?.Trim() ?? string.Empty;
            return stored.Length > 0 &&
                   string.Equals(stored, current, StringComparison.Ordinal);
        }

        void FlushRecoveryMarkers()
        {
#if UNITY_EDITOR
            recoveryMarkerPersistenceForTests?.Invoke();
#endif
            PlayerPrefs.Save();
        }

        static void RestoreIntPreference(
            string key,
            bool existed,
            int value)
        {
            if (existed)
                PlayerPrefs.SetInt(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }

        static void RestoreStringPreference(
            string key,
            bool existed,
            string value)
        {
            if (existed)
                PlayerPrefs.SetString(key, value ?? string.Empty);
            else
                PlayerPrefs.DeleteKey(key);
        }

        static void FlushRecoveryMarkerRollback(string context)
        {
            try
            {
                PlayerPrefs.Save();
            }
            catch (Exception rollbackException)
            {
                Debug.LogWarning(
                    "[MukJump] " + context + " 이전 상태 저장에도 실패했습니다: " +
                    rollbackException.Message);
            }
        }

        bool BeginPendingLocalGuestImport()
        {
            bool pendingExisted = false;
            int previousPending = 0;
            bool ownerExisted = false;
            string previousOwner = string.Empty;
            bool restoredOwnerExisted = false;
            string previousRestoredOwner = string.Empty;
            bool snapshotCaptured = false;
            try
            {
                pendingExisted = PlayerPrefs.HasKey(PendingLocalGuestImportKey);
                previousPending = PlayerPrefs.GetInt(
                    PendingLocalGuestImportKey,
                    0);
                ownerExisted = PlayerPrefs.HasKey(
                    PendingLocalGuestImportOwnerKey);
                previousOwner = PlayerPrefs.GetString(
                    PendingLocalGuestImportOwnerKey,
                    string.Empty);
                restoredOwnerExisted = PlayerPrefs.HasKey(
                    PendingLocalGuestImportRestoredOwnerKey);
                previousRestoredOwner = PlayerPrefs.GetString(
                    PendingLocalGuestImportRestoredOwnerKey,
                    string.Empty);
                snapshotCaptured = true;

                PlayerPrefs.SetInt(PendingLocalGuestImportKey, 1);
                PlayerPrefs.DeleteKey(PendingLocalGuestImportOwnerKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalGuestImportRestoredOwnerKey);
                FlushRecoveryMarkers();
                return PlayerPrefs.GetInt(PendingLocalGuestImportKey, 0) != 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 게스트 가져오기 복구 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingLocalGuestImportKey,
                        pendingExisted,
                        previousPending);
                    RestoreStringPreference(
                        PendingLocalGuestImportOwnerKey,
                        ownerExisted,
                        previousOwner);
                    RestoreStringPreference(
                        PendingLocalGuestImportRestoredOwnerKey,
                        restoredOwnerExisted,
                        previousRestoredOwner);
                    FlushRecoveryMarkerRollback("게스트 가져오기 표식");
                }
                return false;
            }
        }

        bool BeginPendingAuthorizedTransition(
            string previousAccountScope,
            MukJumpAccountKind kind,
            bool replaceLocalProfile,
            bool restoreLocalIfServerEmpty)
        {
            if (kind != MukJumpAccountKind.Google &&
                kind != MukJumpAccountKind.Apple)
                return false;

            bool kindExisted = false;
            int previousKind = 0;
            bool ownerExisted = false;
            string previousOwner = string.Empty;
            bool replaceExisted = false;
            int previousReplace = 0;
            bool restoreExisted = false;
            int previousRestore = 0;
            bool snapshotCaptured = false;
            try
            {
                kindExisted = PlayerPrefs.HasKey(
                    PendingAuthorizedTransitionKindKey);
                previousKind = PlayerPrefs.GetInt(
                    PendingAuthorizedTransitionKindKey,
                    0);
                ownerExisted = PlayerPrefs.HasKey(
                    PendingAuthorizedTransitionPreviousOwnerKey);
                previousOwner = PlayerPrefs.GetString(
                    PendingAuthorizedTransitionPreviousOwnerKey,
                    string.Empty);
                replaceExisted = PlayerPrefs.HasKey(
                    PendingAuthorizedTransitionReplaceLocalKey);
                previousReplace = PlayerPrefs.GetInt(
                    PendingAuthorizedTransitionReplaceLocalKey,
                    0);
                restoreExisted = PlayerPrefs.HasKey(
                    PendingAuthorizedTransitionRestoreGuestKey);
                previousRestore = PlayerPrefs.GetInt(
                    PendingAuthorizedTransitionRestoreGuestKey,
                    0);
                snapshotCaptured = true;

                PlayerPrefs.SetInt(
                    PendingAuthorizedTransitionKindKey,
                    (int)kind);
                PlayerPrefs.SetString(
                    PendingAuthorizedTransitionPreviousOwnerKey,
                    previousAccountScope?.Trim() ?? string.Empty);
                PlayerPrefs.SetInt(
                    PendingAuthorizedTransitionReplaceLocalKey,
                    replaceLocalProfile ? 1 : 0);
                PlayerPrefs.SetInt(
                    PendingAuthorizedTransitionRestoreGuestKey,
                    restoreLocalIfServerEmpty ? 1 : 0);
                FlushRecoveryMarkers();
                return PlayerPrefs.HasKey(
                           PendingAuthorizedTransitionKindKey) &&
                       PlayerPrefs.GetInt(
                           PendingAuthorizedTransitionKindKey,
                           -1) == (int)kind;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 계정 전환 복구 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingAuthorizedTransitionKindKey,
                        kindExisted,
                        previousKind);
                    RestoreStringPreference(
                        PendingAuthorizedTransitionPreviousOwnerKey,
                        ownerExisted,
                        previousOwner);
                    RestoreIntPreference(
                        PendingAuthorizedTransitionReplaceLocalKey,
                        replaceExisted,
                        previousReplace);
                    RestoreIntPreference(
                        PendingAuthorizedTransitionRestoreGuestKey,
                        restoreExisted,
                        previousRestore);
                    FlushRecoveryMarkerRollback("계정 전환 표식");
                }
                return false;
            }
        }

        bool BeginPendingGuestUpgrade(MukJumpAccountKind kind)
        {
            if (kind != MukJumpAccountKind.Google &&
                kind != MukJumpAccountKind.Apple)
                return false;
            bool existed = false;
            int previousValue = 0;
            bool snapshotCaptured = false;
            try
            {
                existed = PlayerPrefs.HasKey(PendingGuestUpgradeKindKey);
                previousValue = PlayerPrefs.GetInt(
                    PendingGuestUpgradeKindKey,
                    0);
                snapshotCaptured = true;
                PlayerPrefs.SetInt(PendingGuestUpgradeKindKey, (int)kind);
                FlushRecoveryMarkers();
                return PlayerPrefs.GetInt(PendingGuestUpgradeKindKey, -1) ==
                       (int)kind;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 게스트 계정 연결 복구 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingGuestUpgradeKindKey,
                        existed,
                        previousValue);
                    FlushRecoveryMarkerRollback("게스트 계정 연결 표식");
                }
                return false;
            }
        }

        void ClearPendingGuestUpgrade()
        {
            PlayerPrefs.DeleteKey(PendingGuestUpgradeKindKey);
            PlayerPrefs.Save();
        }

        bool TryResumePendingAuthorizedTransition()
        {
            if (!PlayerPrefs.HasKey(
                    PendingAuthorizedTransitionKindKey))
                return false;

            int rawKind = PlayerPrefs.GetInt(
                PendingAuthorizedTransitionKindKey,
                -1);
            if (!Enum.IsDefined(typeof(MukJumpAccountKind), rawKind))
            {
                ClearPendingAuthorizedTransition();
                return false;
            }

            var kind = (MukJumpAccountKind)rawKind;
            if (kind != MukJumpAccountKind.Google &&
                kind != MukJumpAccountKind.Apple)
            {
                ClearPendingAuthorizedTransition();
                return false;
            }

            string previousAccountScope = PlayerPrefs.GetString(
                PendingAuthorizedTransitionPreviousOwnerKey,
                string.Empty);
            bool replaceLocalProfile = PlayerPrefs.GetInt(
                PendingAuthorizedTransitionReplaceLocalKey,
                0) != 0;
            bool restoreLocalIfServerEmpty = PlayerPrefs.GetInt(
                PendingAuthorizedTransitionRestoreGuestKey,
                0) != 0;
            MukJumpAccountScopeRelation relation =
                ClassifyAuthorizedTransitionAccount(
                    previousAccountScope,
                    CurrentAccountScope());
            if (relation == MukJumpAccountScopeRelation.Unknown)
            {
                EnterProviderResolutionBlock(
                    "로그인 계정 식별을 아직 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
                return true;
            }
            if (relation == MukJumpAccountScopeRelation.Same)
            {
                ClearPendingAuthorizedTransition();
                if (restoreLocalIfServerEmpty)
                    ClearPendingLocalGuestImport();
                return false;
            }

            FinishAuthorizedAccountTransition(
                kind,
                replaceLocalProfile,
                restoreLocalIfServerEmpty,
                "중단된 계정 전환을 복구했습니다");
            return true;
        }

        public static bool DidAuthorizedTransitionChangeAccount(
            string previousAccountScope,
            string currentAccountScope) =>
            ClassifyAuthorizedTransitionAccount(
                previousAccountScope,
                currentAccountScope) ==
            MukJumpAccountScopeRelation.Changed;

        public static MukJumpAccountScopeRelation
            ClassifyAuthorizedTransitionAccount(
                string previousAccountScope,
                string currentAccountScope)
        {
            string current = currentAccountScope?.Trim() ?? string.Empty;
            if (current.Length == 0)
                return MukJumpAccountScopeRelation.Unknown;
            return string.Equals(
                previousAccountScope?.Trim() ?? string.Empty,
                current,
                StringComparison.Ordinal)
                ? MukJumpAccountScopeRelation.Same
                : MukJumpAccountScopeRelation.Changed;
        }

        public static bool ShouldReplaceLocalProfileAfterAuthorization(
            bool replacementRequested,
            string previousAccountScope,
            string currentAccountScope) =>
            replacementRequested &&
            DidAuthorizedTransitionChangeAccount(
                previousAccountScope,
                currentAccountScope);

        bool FinishAuthorizedAccountTransition(
            MukJumpAccountKind kind,
            bool replaceLocalProfile,
            bool restoreLocalIfServerEmpty,
            string message)
        {
            ClearPendingFederation();
            if (!PersistAuthorizedAccountTransition(
                    kind,
                    replaceLocalProfile,
                    restoreLocalIfServerEmpty))
            {
                EnterFatalSyncBlock(
                    "계정 전환 상태를 안전하게 저장하지 못했습니다. 로컬 기록은 보존됩니다");
                return false;
            }

            if (ShouldClearAutomaticAuthenticationSuppression(kind))
                CommitConfirmedSocialAuthentication(kind);
            if (replaceLocalProfile)
            {
                replaceLocalFromServerOnNextLoad = true;
                restoreLocalGuestIfServerEmptyOnNextLoad =
                    restoreLocalIfServerEmpty;
                revision = 0L;
                // 서버 응답 전에는 화면의 기록도 원본 저장도 비우지 않는다.
                // profileResolutionPending이 플레이·저장을 잠그며, 검증된
                // 서버 기록 적용 또는 빈 계정 확인 이후에만 교체한다.
                if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby)
                {
                    EnterFatalSyncBlock(
                        "플레이 중에는 계정 기록을 교체할 수 없습니다. 현재 도전을 마친 뒤 다시 시도해 주세요");
                    return false;
                }
            }
            ClearPendingAuthorizedTransition();
            ClearPendingGuestUpgrade();
            rowInDate = string.Empty;
            CompleteAuthentication(message);
            return true;
        }

        void ClearPendingAuthorizedTransition()
        {
            PlayerPrefs.DeleteKey(PendingAuthorizedTransitionKindKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionPreviousOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionReplaceLocalKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionRestoreGuestKey);
            PlayerPrefs.Save();
        }

        bool PersistAuthorizedAccountTransition(
            MukJumpAccountKind kind,
            bool replaceLocalProfile,
            bool restoreLocalIfServerEmpty)
        {
            string accountScope = CurrentAccountScope();
            if (string.IsNullOrWhiteSpace(accountScope))
                return false;
            string normalizedScope = accountScope.Trim();
            if (restoreLocalIfServerEmpty &&
                PlayerPrefs.GetInt(PendingLocalGuestImportKey, 0) == 0)
                return false;

            // 계정 종류·전환 잠금·게스트 가져오기 소유자를 모두 stage한
            // 뒤 한 번만 flush한다. 이 경계에서 종료돼도 provider 종류나
            // import owner 하나만 남는 반쪽 상태를 만들지 않는다.
            AccountKind = kind;
            PlayerPrefs.SetInt(KindKey, (int)kind);
            if (replaceLocalProfile)
            {
                profileResolutionPending = true;
                PlayerPrefs.SetString(
                    PendingProfileResolutionOwnerKey,
                    normalizedScope);
                PlayerPrefs.DeleteKey(RevisionKey);
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
            }
            if (restoreLocalIfServerEmpty)
            {
                PlayerPrefs.SetString(
                    PendingLocalGuestImportOwnerKey,
                    normalizedScope);
            }
            PlayerPrefs.Save();

            bool kindStored = PlayerPrefs.GetInt(
                KindKey,
                (int)MukJumpAccountKind.LocalGuest) == (int)kind;
            bool profileStored = !replaceLocalProfile ||
                IsPendingProfileResolutionOwnedBy(
                    PlayerPrefs.GetString(
                        PendingProfileResolutionOwnerKey,
                        string.Empty),
                    normalizedScope);
            bool importStored = !restoreLocalIfServerEmpty ||
                IsPendingLocalGuestImportOwnedBy(
                    PlayerPrefs.GetInt(
                        PendingLocalGuestImportKey,
                        0) != 0,
                    PlayerPrefs.GetString(
                        PendingLocalGuestImportOwnerKey,
                        string.Empty),
                    normalizedScope);
            return kindStored && profileStored && importStored;
        }

        bool MarkPendingLocalGuestImportRestored()
        {
            string accountScope = CurrentAccountScope();
            if (string.IsNullOrWhiteSpace(accountScope) ||
                !restoreLocalGuestIfServerEmptyOnNextLoad)
                return false;

            PlayerPrefs.SetString(
                PendingLocalGuestImportRestoredOwnerKey,
                accountScope.Trim());
            PlayerPrefs.Save();
            return IsPendingLocalGuestImportRestoredForOwner(
                PlayerPrefs.GetString(
                    PendingLocalGuestImportRestoredOwnerKey,
                    string.Empty),
                accountScope);
        }

        void ClearPendingProfileResolution()
        {
            profileResolutionPending = false;
            PlayerPrefs.DeleteKey(PendingProfileResolutionOwnerKey);
            PlayerPrefs.Save();
        }

        void EnterLocalLogoutCleanupBlock(string message)
        {
            localLogoutCleanupPending = true;
            PlayerPrefs.SetInt(PendingLocalLogoutCleanupKey, 1);
            PlayerPrefs.Save();
            SetState(MukJumpAccountPhase.Error, message);
        }

        bool BeginPendingLocalLogoutCleanup()
        {
            bool previousCleanupPending = localLogoutCleanupPending;
            bool previousRemoteConfirmed = localLogoutRemoteConfirmed;
            bool cleanupExisted = false;
            int previousCleanup = 0;
            bool remoteExisted = false;
            int previousRemote = 0;
            bool snapshotCaptured = false;
            try
            {
                cleanupExisted = PlayerPrefs.HasKey(
                    PendingLocalLogoutCleanupKey);
                previousCleanup = PlayerPrefs.GetInt(
                    PendingLocalLogoutCleanupKey,
                    0);
                remoteExisted = PlayerPrefs.HasKey(
                    PendingLocalLogoutRemoteConfirmedKey);
                previousRemote = PlayerPrefs.GetInt(
                    PendingLocalLogoutRemoteConfirmedKey,
                    0);
                snapshotCaptured = true;

                localLogoutCleanupPending = true;
                localLogoutRemoteConfirmed = false;
                PlayerPrefs.SetInt(PendingLocalLogoutCleanupKey, 1);
                PlayerPrefs.DeleteKey(
                    PendingLocalLogoutRemoteConfirmedKey);
                FlushRecoveryMarkers();
                return PlayerPrefs.GetInt(
                    PendingLocalLogoutCleanupKey,
                    0) != 0;
            }
            catch (Exception exception)
            {
                localLogoutCleanupPending = previousCleanupPending;
                localLogoutRemoteConfirmed = previousRemoteConfirmed;
                Debug.LogWarning(
                    "[MukJump] 로그아웃 복구 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingLocalLogoutCleanupKey,
                        cleanupExisted,
                        previousCleanup);
                    RestoreIntPreference(
                        PendingLocalLogoutRemoteConfirmedKey,
                        remoteExisted,
                        previousRemote);
                    FlushRecoveryMarkerRollback("로그아웃 표식");
                }
                return false;
            }
        }

        void MarkPendingLocalLogoutRemoteConfirmed()
        {
            localLogoutRemoteConfirmed = true;
            PlayerPrefs.SetInt(
                PendingLocalLogoutRemoteConfirmedKey,
                1);
            PlayerPrefs.Save();
        }

        void ClearPendingLocalLogoutCleanup()
        {
            localLogoutCleanupPending = false;
            localLogoutRemoteConfirmed = false;
            PlayerPrefs.DeleteKey(PendingLocalLogoutCleanupKey);
            PlayerPrefs.DeleteKey(
                PendingLocalLogoutRemoteConfirmedKey);
            PlayerPrefs.Save();
        }

        public static bool IsDefinitiveTokenUnavailable(
            string statusCode,
            string errorCode,
            string message)
        {
            string status = statusCode?.Trim() ?? string.Empty;
            string error = errorCode?.Trim() ?? string.Empty;
            string detail = message?.Trim() ?? string.Empty;
            if (status == "400" &&
                (string.Equals(
                     error,
                     "accessTokenError",
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     error,
                     "UndefinedParameterException",
                     StringComparison.OrdinalIgnoreCase)))
                return true;
            if (status == "410" &&
                error.IndexOf(
                    "GoneResourceException",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return status == "401" &&
                   string.Equals(
                       error,
                       "BadUnauthorizedException",
                       StringComparison.OrdinalIgnoreCase) &&
                   detail.IndexOf(
                       "refreshToken",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void ClearPendingLocalGuestImport()
        {
            restoreLocalGuestIfServerEmptyOnNextLoad = false;
            PlayerPrefs.DeleteKey(PendingLocalGuestImportKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingLocalGuestImportRestoredOwnerKey);
            PlayerPrefs.Save();
        }

        bool StorePendingLeaderboardBest(string accountScope, int bestHeight)
        {
            string normalizedScope = accountScope?.Trim() ?? string.Empty;
            if (normalizedScope.Length == 0)
                return false;

            try
            {
                bool sameOwner = IsPendingLeaderboardOwnedBy(
                    PlayerPrefs.GetString(
                        PendingLeaderboardOwnerKey,
                        string.Empty),
                    normalizedScope);
                int existingBest = sameOwner
                    ? PlayerPrefs.GetInt(PendingLeaderboardBestKey, 0)
                    : 0;
                PlayerPrefs.SetString(
                    PendingLeaderboardOwnerKey,
                    normalizedScope);
                PlayerPrefs.SetInt(
                    PendingLeaderboardBestKey,
                    Mathf.Max(Mathf.Max(0, bestHeight), existingBest));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 순위 재시도 기록을 저장하지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        void ClearPendingLeaderboardSave(bool saveImmediately = true)
        {
            try
            {
                PlayerPrefs.DeleteKey(PendingLeaderboardBestKey);
                PlayerPrefs.DeleteKey(PendingLeaderboardOwnerKey);
                if (saveImmediately)
                    PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 완료된 순위 재시도 기록을 지우지 못했습니다: " +
                    exception.Message);
            }
        }

        void LoadCloudSnapshot()
        {
            if (!IsOnlineAuthenticated || settings == null ||
                cloudLoadInFlight || saveInFlight)
                return;

            if (leaderboardSaveInFlight)
            {
                // 순위 갱신도 같은 게임 정보 행을 쓴다. 조회 예약을 버리지 말고
                // 현재 계정 소유자와 함께 보존한 뒤 쓰기 완료 후 다시 읽는다.
                cloudLoadAccountScope = CurrentAccountScope();
                resumeCloudLoadPending = true;
                return;
            }

            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            cloudLoadAccountScope = capturedAccountScope;
            cloudLoadInFlight = true;
            cloudLoadDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestMyGameData(
                    settings.PlayerTableName,
                    new Where(),
                    bro =>
                    {
                        if (!IsCurrentAccountSession(
                                capturedSessionGeneration,
                                capturedAccountScope))
                            return;
                        cloudLoadInFlight = false;
                        try
                        {
                            if (bro == null || !bro.IsSuccess())
                            {
                                KeepSavePending(
                                    "서버 저장은 연결 복구 후 다시 시도합니다");
                                return;
                            }

                            retryDelaySeconds = InitialRetrySeconds;

                            JsonData rows = bro.FlattenRows();
                            if (rows == null || rows.Count == 0)
                            {
                                string currentAccountScope =
                                    CurrentAccountScope();
                                bool guestImportAlreadyRestored =
                                    IsPendingLocalGuestImportRestoredForOwner(
                                        PlayerPrefs.GetString(
                                            PendingLocalGuestImportRestoredOwnerKey,
                                            string.Empty),
                                        currentAccountScope);
                                if (replaceLocalFromServerOnNextLoad &&
                                    !restoreLocalGuestIfServerEmptyOnNextLoad &&
                                    !TryClearAccountProgressForSwitch())
                                {
                                    EnterFatalSyncBlock(
                                        "계정 기록을 안전하게 교체하지 못했습니다. 로컬 기록은 보존됩니다");
                                    return;
                                }
                                if (ShouldRestorePendingLocalGuestImport(
                                        restoreLocalGuestIfServerEmptyOnNextLoad,
                                        guestImportAlreadyRestored))
                                {
                                    if (!RestoreSavedLocalGuestProfile())
                                    {
                                        EnterFatalSyncBlock(
                                            "게스트 기록을 안전하게 복원하지 못해 첫 서버 저장을 중단했습니다");
                                        return;
                                    }
                                    // Insert 실패 뒤 재조회할 때 예전 백업을 다시 덮으면
                                    // 첫 복원 이후 상태가 사라진다. 계정별 1회 복원 완료를
                                    // Insert 성공까지 별도로 보존한다.
                                    if (!MarkPendingLocalGuestImportRestored())
                                    {
                                        EnterFatalSyncBlock(
                                            "게스트 기록 복원 상태를 저장하지 못해 첫 서버 저장을 중단했습니다");
                                        return;
                                    }
                                }
                                InsertLocalSnapshot();
                                return;
                            }
                            if (rows.Count != 1)
                            {
                                EnterFatalSyncBlock(
                                    "서버 저장이 중복되어 안전하게 동기화할 수 없습니다. 고객센터에 문의해 주세요");
                                return;
                            }

                            JsonData row = rows[0];
                            rowInDate = ReadString(row, "inDate");
                            MukJumpCloudSnapshot server = ReadSnapshot(row);
                            if (!server.IsSupported ||
                                !PermanentGrowthProfile.IsSupportedCloudJson(
                                    server.growthJson) ||
                                string.IsNullOrWhiteSpace(rowInDate))
                            {
                                EnterFatalSyncBlock(
                                    "서버 저장 형식을 확인하지 못했습니다. 로컬 기록은 보존됩니다");
                                return;
                            }

                            if (restoreLocalGuestIfServerEmptyOnNextLoad)
                            {
                                if (PermanentGrowthProfile.HasCompletedRun)
                                {
                                    // 기기에 실제 판 기록이 있고 대상 서버에도 행이
                                    // 있으면 자동 덮어쓰기 대신 기존 기록 선택을 쓴다.
                                    BeginSyncConflict(server, rowInDate);
                                    return;
                                }
                                ClearPendingLocalGuestImport();
                            }

                            AcknowledgePreviouslyCommittedWrite(server);

                            bool keepPendingLocalProfile =
                                dirty && !replaceLocalFromServerOnNextLoad;
                            if (ShouldRequireSyncChoice(
                                    keepPendingLocalProfile,
                                    revision,
                                    server.revision))
                            {
                                BeginSyncConflict(server, rowInDate);
                                return;
                            }

                            TryApplyCloudSnapshot(
                                server,
                                keepPendingLocalProfile);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                "[MukJump] 서버 저장을 읽지 못했습니다: " +
                                exception.Message);
                            KeepSavePending(
                                "서버 기록을 다시 읽어 로컬 기록을 확인합니다");
                        }
                    });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                cloudLoadInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 서버 저장 조회를 시작하지 못했습니다: " +
                    exception.Message);
                KeepSavePending("서버 저장은 연결 복구 후 다시 시도합니다");
            }
        }

        public static bool ShouldRequireSyncChoice(
            bool hasPendingLocalProfile,
            long localRevision,
            long serverRevision) =>
            hasPendingLocalProfile && localRevision != serverRevision;

        // 저장 응답이 유실된 경우에도 같은 요청 ID와 바로 다음 revision이
        // 확인되면 내 저장이다. 다른 요청·다른 세대는 기존 충돌 선택을 유지한다.
        bool AcknowledgePreviouslyCommittedWrite(MukJumpCloudSnapshot server)
        {
            string pendingOperation = PlayerPrefs.GetString(PendingOperationIdKey, string.Empty);
            if (server == null || revision == long.MaxValue || server.revision != revision + 1 ||
                string.IsNullOrWhiteSpace(pendingOperation) ||
                !string.Equals(pendingOperation, server.lastOperationId, StringComparison.Ordinal))
                return false;
            // 최신 로컬 변경과 dirty는 보존한다. 서버 원본을 재적용하거나 완료 처리하지
            // 않고, 확인한 revision 이후 새 요청 ID로 다시 저장하도록 한다.
            PlayerPrefs.SetString(RevisionKey, server.revision.ToString());
            PlayerPrefs.DeleteKey(PendingOperationIdKey);
            PlayerPrefs.Save();
            revision = server.revision;
            return true;
        }

        void BeginSyncConflict(
            MukJumpCloudSnapshot server,
            string serverRowInDate)
        {
            syncWriteBlocked = false;
            pendingServerSnapshot = server;
            pendingServerRowInDate = serverRowInDate?.Trim() ?? string.Empty;
            SetState(
                MukJumpAccountPhase.NeedsSyncChoice,
                "다른 기기의 기록이 변경되었습니다. 사용할 기록을 선택해 주세요");
        }

        void ClearPendingSyncConflict()
        {
            pendingServerSnapshot = null;
            pendingServerRowInDate = string.Empty;
        }

        public void UseServerAfterSyncConflict()
        {
            if (!HasPendingSyncConflict)
                return;

            MukJumpCloudSnapshot server = pendingServerSnapshot;
            string previousRowInDate = rowInDate;
            rowInDate = pendingServerRowInDate;
            try
            {
                if (!TryApplyCloudSnapshot(
                        server,
                        keepPendingLocalProfile: false,
                        mergeLocalBestHeight: !replaceLocalFromServerOnNextLoad))
                    rowInDate = previousRowInDate;
            }
            catch (Exception exception)
            {
                rowInDate = previousRowInDate;
                Debug.LogWarning(
                    "[MukJump] 서버 기록 선택 적용 예외를 격리했습니다: " +
                    exception.Message);
                BeginSyncConflict(server, pendingServerRowInDate);
                SetStatus(
                    "서버 기록을 모두 적용하지 못했습니다. 같은 선택을 다시 시도해 주세요");
            }
        }

        public void KeepThisDeviceAfterSyncConflict()
        {
            if (!HasPendingSyncConflict)
                return;

            MukJumpCloudSnapshot server = pendingServerSnapshot;
            string serverRowInDate = pendingServerRowInDate;
            int localBest = ScoreManager.Instance != null
                ? ScoreManager.Instance.Best
                : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
            int mergedBest = MukJumpCloudMergePolicy.MergeBestHeight(
                localBest,
                server.bestHeight);
            bool bestApplied = ScoreManager.Instance != null
                ? ScoreManager.Instance.TryMergeVerifiedBest(mergedBest)
                : ScoreManager.TryMergeVerifiedBestIntoStore(mergedBest);
            if (!bestApplied)
            {
                SetStatus(
                    "최고 기록을 안전하게 보존하지 못했습니다. 기록 선택을 다시 시도해 주세요");
                return;
            }

            long previousRevision = revision;
            string previousRowInDate = rowInDate;
            bool previousDirty = dirty;
            long previousMutationVersion = localMutationVersion;
            float previousSaveAtRealtime = saveAtRealtime;
            revision = Math.Max(0L, server.revision);
            rowInDate = serverRowInDate;
            try
            {
                PersistDeviceConflictChoice();
                dirty = true;
                localMutationVersion++;
                saveAtRealtime =
                    Time.realtimeSinceStartup + SaveDebounceSeconds;
            }
            catch (Exception exception)
            {
                revision = previousRevision;
                rowInDate = previousRowInDate;
                dirty = previousDirty;
                localMutationVersion = previousMutationVersion;
                saveAtRealtime = previousSaveAtRealtime;
                bool bestRestored = mergedBest == localBest ||
                                    ScoreManager
                                        .TryReplaceVerifiedBestForAccountSwitch(
                                            localBest);
                TryRestoreDeviceConflictMarkers(previousRevision);
                BeginSyncConflict(server, serverRowInDate);
                Debug.LogWarning(
                    "[MukJump] 이 기기 기록 선택 상태를 저장하지 못했습니다: " +
                    exception.Message);
                SetStatus(bestRestored
                    ? "기록 선택 상태를 저장하지 못했습니다. 같은 선택을 다시 시도해 주세요"
                    : "최고 기록을 되돌리지 못해 시작을 막았습니다. 다시 시도해 주세요");
                return;
            }

            ClearPendingSyncConflict();
            SetState(
                MukJumpAccountPhase.OnlineReady,
                "이 기기의 기록을 선택했습니다. 서버에 저장하는 중입니다");
        }

        void PersistDeviceConflictChoice()
        {
#if UNITY_EDITOR
            if (keepDeviceConflictPersistenceForTests != null)
            {
                keepDeviceConflictPersistenceForTests();
                return;
            }
#endif
            PlayerPrefs.DeleteKey(PendingOperationIdKey);
            PlayerPrefs.SetString(RevisionKey, revision.ToString());
            PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.DeleteKey(PendingProfileResolutionOwnerKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportOwnerKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportRestoredOwnerKey);
            PlayerPrefs.Save();
            profileResolutionPending = false;
            replaceLocalFromServerOnNextLoad = false;
            restoreLocalGuestIfServerEmptyOnNextLoad = false;
        }

        void TryRestoreDeviceConflictMarkers(long previousRevision)
        {
            try
            {
                if (profileResolutionPending)
                    PlayerPrefs.SetString(PendingProfileResolutionOwnerKey, CurrentAccountScope());
                if (restoreLocalGuestIfServerEmptyOnNextLoad)
                {
                    PlayerPrefs.SetInt(PendingLocalGuestImportKey, 1);
                    PlayerPrefs.SetString(PendingLocalGuestImportOwnerKey, CurrentAccountScope());
                }
                PlayerPrefs.SetString(
                    RevisionKey,
                    Math.Max(0L, previousRevision).ToString());
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 기록 선택 실패 뒤 이전 revision 표식을 복원하지 못했습니다: " +
                    exception.Message);
            }
        }

        bool TryApplyCloudSnapshot(
            MukJumpCloudSnapshot server,
            bool keepPendingLocalProfile,
            bool mergeLocalBestHeight = false)
        {
            if (server == null || !server.IsSupported ||
                !PermanentGrowthProfile.IsSupportedCloudJson(
                    server.growthJson))
            {
                EnterFatalSyncBlock(
                    "서버 성장 기록을 검증하지 못해 로컬 기록을 유지합니다");
                return false;
            }

            // 서버 응답이 늦게 도착해도 진행 중인 판의 Height·성장 스냅샷을
            // 교체하지 않는다. 결과 정산 뒤 사용자가 로비에서 선택하게 보류한다.
            if (GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Lobby)
            {
                BeginSyncConflict(server, rowInDate);
                return false;
            }

            int localBest = ScoreManager.Instance != null
                ? ScoreManager.Instance.Best
                : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
            bool explicitAccountReplacement =
                replaceLocalFromServerOnNextLoad &&
                !keepPendingLocalProfile &&
                !mergeLocalBestHeight;
            // 평상시 같은 계정 동기화는 서버 값이 낮아도 로컬 기록을 내리지 않는다.
            // exact replace는 명시적 계정 전환에서만 허용한다.
            bool shouldMergeBest = !explicitAccountReplacement;
            int resolvedBest = shouldMergeBest
                ? MukJumpCloudMergePolicy.MergeBestHeight(
                    localBest,
                    server.bestHeight)
                : server.bestHeight;
            bool growthApplied = true;
            bool bestApplied = false;
            string rollbackGrowthJson = string.Empty;
            LobbyCloudSettingsSnapshot rollbackSettings =
                LobbySettingsProfile.CaptureCloudSettings();
            bool growthChanged = false;
            bool growthUpgradePending = false;
            bool bestChanged = false;
            bool settingsAttempted = false;
            string conflictRowInDate =
                !string.IsNullOrWhiteSpace(pendingServerRowInDate)
                    ? pendingServerRowInDate
                    : rowInDate;
            long rollbackRevision = revision;
            bool rollbackDirty = dirty;
            bool rollbackReplaceLocal = replaceLocalFromServerOnNextLoad;
            suppressDirty = true;
            try
            {
                if (!keepPendingLocalProfile)
                {
                    PermanentGrowthProfile.TryExportCloudJson(
                        out rollbackGrowthJson);
                    growthApplied = PermanentGrowthProfile
                        .TryReplaceFromCloudJson(server.growthJson);
                    growthChanged = growthApplied;
                    // 이관 중 Changed 알림은 suppressDirty로 막혀 있으므로,
                    // 새 성장 버전/추가 거리 보상은 별도로 서버 재저장을 예약한다.
                    growthUpgradePending = growthApplied &&
                        PermanentGrowthProfile.TryExportCloudJson(out string upgradedGrowth) &&
                        !string.Equals(server.growthJson, upgradedGrowth, StringComparison.Ordinal);
                }

                if (growthApplied)
                {
                    bestApplied = shouldMergeBest
                        ? (ScoreManager.Instance != null
                            ? ScoreManager.Instance.TryMergeVerifiedBest(
                                resolvedBest)
                            : ScoreManager.TryMergeVerifiedBestIntoStore(
                                resolvedBest))
                        : ScoreManager.TryReplaceVerifiedBestForAccountSwitch(
                            resolvedBest);
                    bestChanged = bestApplied;
                    if (!bestApplied && !keepPendingLocalProfile &&
                        !string.IsNullOrWhiteSpace(rollbackGrowthJson))
                        PermanentGrowthProfile.TryReplaceFromCloudJson(
                            rollbackGrowthJson);

                    if (bestApplied && !keepPendingLocalProfile)
                    {
                        settingsAttempted = true;
                        LobbySettingsProfile.ApplyCloudSettings(
                            server.bgmVolume,
                            server.sfxVolume,
                            server.tutorialVersion);
                    }
                }
            }
            catch (Exception exception)
            {
                bool rollbackComplete =
                    RollbackCloudSnapshotApplication(
                        localBest,
                        rollbackGrowthJson,
                        rollbackSettings,
                        growthChanged,
                        bestChanged,
                        settingsAttempted);
                Debug.LogWarning(
                    "[MukJump] 서버 기록 전체 적용에 실패해 로컬 상태로 " +
                    "되돌렸습니다: " + exception.Message);
                BeginSyncConflict(server, conflictRowInDate);
                SetStatus(rollbackComplete
                    ? "서버 기록을 모두 적용하지 못했습니다. 같은 선택을 다시 시도해 주세요"
                    : "서버 기록 적용을 되돌리지 못해 시작을 막았습니다. 다시 시도해 주세요");
                return false;
            }
            finally
            {
                suppressDirty = false;
            }
            if (!growthApplied)
            {
                BeginSyncConflict(server, conflictRowInDate);
                SetStatus(
                    "성장 기록을 안전하게 복원하지 못했습니다. 같은 선택을 다시 시도해 주세요");
                return false;
            }
            if (!bestApplied)
            {
                BeginSyncConflict(server, conflictRowInDate);
                SetStatus(
                    "최고 기록을 안전하게 복원하지 못했습니다. 같은 선택을 다시 시도해 주세요");
                return false;
            }

            try
            {
                revision = keepPendingLocalProfile
                    ? Math.Max(revision, server.revision)
                    : Math.Max(0L, server.revision);
                PersistRevision();
                dirty = keepPendingLocalProfile ||
                        growthUpgradePending ||
                        resolvedBest > server.bestHeight;
                if (!dirty)
                {
                    PlayerPrefs.DeleteKey(PendingSaveKey);
                    PlayerPrefs.DeleteKey(PendingOperationIdKey);
                    PlayerPrefs.Save();
                }
                replaceLocalFromServerOnNextLoad = false;
                if (profileResolutionPending)
                {
                    ClearPendingProfileResolution();
                    ClearPendingLocalGuestImport();
                }
            }
            catch (Exception exception)
            {
                suppressDirty = true;
                bool rollbackComplete =
                    RollbackCloudSnapshotApplication(
                        localBest,
                        rollbackGrowthJson,
                        rollbackSettings,
                        growthChanged,
                        bestChanged,
                        settingsAttempted);
                suppressDirty = false;
                revision = rollbackRevision;
                dirty = rollbackDirty;
                replaceLocalFromServerOnNextLoad = rollbackReplaceLocal;
                Debug.LogWarning(
                    "[MukJump] 서버 기록 적용 완료 표식을 저장하지 못해 " +
                    "로컬 상태로 되돌렸습니다: " + exception.Message);
                BeginSyncConflict(server, conflictRowInDate);
                SetStatus(rollbackComplete
                    ? "서버 기록 완료 상태를 저장하지 못했습니다. 같은 선택을 다시 시도해 주세요"
                    : "서버 기록 적용을 되돌리지 못해 시작을 막았습니다. 다시 시도해 주세요");
                return false;
            }
            ClearPendingSyncConflict();
            SetState(
                MukJumpAccountPhase.OnlineReady,
                dirty ? "기록을 확인해 서버에 저장하는 중" : "동기화 완료");
            if (dirty)
                MarkDirty();
            // 이전 제출 콜백이 앱 종료로 사라졌더라도 서버의
            // 현재 최고 기록으로 리더보드를 다시 보정한다.
            QueueVerifiedLeaderboardBest(resolvedBest);
            SubmitBestHeight(resolvedBest);
            return true;
        }

        static bool RollbackCloudSnapshotApplication(
            int previousBest,
            string previousGrowthJson,
            LobbyCloudSettingsSnapshot previousSettings,
            bool growthChanged,
            bool bestChanged,
            bool settingsAttempted)
        {
            bool complete = true;
            if (settingsAttempted)
                complete &= LobbySettingsProfile.TryRestoreCloudSettings(
                    previousSettings);
            if (bestChanged)
                complete &= ScoreManager
                    .TryReplaceVerifiedBestForAccountSwitch(previousBest);
            if (growthChanged)
            {
                if (string.IsNullOrWhiteSpace(previousGrowthJson))
                    complete = false;
                else
                    complete &= PermanentGrowthProfile
                        .TryReplaceFromCloudJson(previousGrowthJson);
            }
            return complete;
        }

        void InsertLocalSnapshot()
        {
            if (!TryCaptureNextSnapshot(
                    "로컬 성장 기록을 확인한 뒤 첫 서버 저장을 다시 시도합니다",
                    out MukJumpCloudSnapshot snapshot))
                return;
            long capturedMutationVersion = localMutationVersion;
            if (!snapshot.IsSupported)
            {
                KeepSavePending("로컬 성장 기록을 확인한 뒤 서버 저장을 다시 시도합니다");
                return;
            }

            saveInFlight = true;
            saveDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            try
            {
                RequestInsertGameData(
                    settings.PlayerTableName,
                    ToParam(snapshot),
                    bro =>
                    {
                        if (!IsCurrentAccountSession(
                                capturedSessionGeneration,
                                capturedAccountScope))
                            return;
                        saveInFlight = false;
                        try
                        {
                            if (bro != null && bro.IsSuccess())
                            {
                                rowInDate = bro.GetInDate();
                                if (string.IsNullOrWhiteSpace(rowInDate))
                                    throw new InvalidOperationException(
                                        "첫 서버 저장 식별자가 비어 있습니다.");
                                if (restoreLocalGuestIfServerEmptyOnNextLoad)
                                    ClearPendingLocalGuestImport();
                                if (profileResolutionPending)
                                    ClearPendingProfileResolution();
                                CompleteSuccessfulSave(
                                    snapshot,
                                    capturedMutationVersion);
                                try
                                {
                                    SubmitBestHeight(snapshot.bestHeight);
                                }
                                catch (Exception leaderboardException)
                                {
                                    // 클라우드 행 저장은 이미 성공했다. 순위 제출
                                    // 오류가 클라우드 dirty를 되살리면 안 된다.
                                    Debug.LogWarning(
                                        "[MukJump] 저장 후 순위 제출을 시작하지 못했습니다: " +
                                        leaderboardException.Message);
                                }
                            }
                            else
                                KeepSavePending("첫 서버 저장을 다시 시도합니다");
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                "[MukJump] 첫 서버 저장 결과를 마무리하지 못했습니다: " +
                                exception.Message);
                            KeepSavePending(
                                "첫 서버 저장 결과를 다시 확인합니다");
                            // Insert 성공 여부가 불명확할 때 같은 행을 다시 만들지
                            // 않고 서버를 재조회해 단일 행을 식별한다.
                            LoadCloudSnapshot();
                        }
                    });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                saveInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 첫 서버 저장 요청을 시작하지 못했습니다: " +
                    exception.Message);
                KeepSavePending("첫 서버 저장을 다시 시도합니다");
            }
        }

        long BeginInteractiveFederationLogin(
            string message,
            MukJumpAccountKind kind)
        {
            interactiveFederationInFlight = true;
            interactiveFederationKind = kind;
            long generation = ++interactiveFederationGeneration;
            interactiveFederationDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            SetState(MukJumpAccountPhase.Connecting, message);
            return generation;
        }

        void HandleInteractiveFederationToken(
            long capturedGeneration,
            bool success,
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            string failureMessage)
        {
            if (!interactiveFederationInFlight ||
                capturedGeneration != interactiveFederationGeneration)
                return;
            interactiveFederationInFlight = false;
            interactiveFederationKind = default;
            try
            {
                HandleFederationToken(
                    success,
                    token,
                    type,
                    kind,
                    failureMessage);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 모바일 계정 로그인 결과를 처리하지 못했습니다: " +
                    exception.Message);
                SetState(
                    MukJumpAccountPhase.Error,
                    failureMessage);
            }
        }

        void HandleInteractiveFederationFailure(
            long capturedGeneration,
            string message,
            Exception exception = null)
        {
            if (!interactiveFederationInFlight ||
                capturedGeneration != interactiveFederationGeneration)
                return;
            interactiveFederationInFlight = false;
            interactiveFederationKind = default;
            if (exception != null)
                Debug.LogWarning(
                    "[MukJump] 모바일 계정 로그인을 시작하지 못했습니다: " +
                    exception.Message);
            SetState(MukJumpAccountPhase.Error, message);
        }

        public void SignInWithGoogle()
        {
            if (!GoogleSignInEnabled)
                return;
            if (!CanStartInteractiveLogin())
                return;
            long capturedGeneration =
                BeginInteractiveFederationLogin(
                    "Google 로그인 중",
                    MukJumpAccountKind.Google);
#if UNITY_ANDROID && !UNITY_EDITOR
            string webClientId = settings.AndroidGoogleWebClientId;
            if (string.IsNullOrWhiteSpace(webClientId))
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Google 클라이언트 ID 설정이 필요합니다");
                return;
            }
            try
            {
                TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin(
                    webClientId,
                    true,
                    (success, message, token) =>
                        HandleInteractiveFederationToken(
                            capturedGeneration,
                            success,
                            token,
                            FederationType.Google,
                            MukJumpAccountKind.Google,
                            "Google 로그인에 실패했습니다"));
            }
            catch (Exception exception)
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Google 로그인에 실패했습니다",
                    exception);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                TheBackend.ToolKit.GoogleLogin.iOS.GoogleLogin(
                    (success, message, token) =>
                        HandleInteractiveFederationToken(
                            capturedGeneration,
                        success,
                        token,
                        FederationType.Google,
                        MukJumpAccountKind.Google,
                            "Google 로그인에 실패했습니다"));
            }
            catch (Exception exception)
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Google 로그인에 실패했습니다",
                    exception);
            }
#else
            HandleInteractiveFederationFailure(
                capturedGeneration,
                "Google 로그인은 모바일 빌드에서 확인합니다");
#endif
        }

        public void SignInWithApple()
        {
            if (!CanStartInteractiveLogin())
                return;
            MukJumpAnalytics.Account(AnalyticsAccountAction.AppleLink, AnalyticsOutcome.Requested);
            long capturedGeneration =
                BeginInteractiveFederationLogin(
                    "Apple 로그인 중",
                    MukJumpAccountKind.Apple);
#if UNITY_IOS && !UNITY_EDITOR
            if (appleAuthManager == null)
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "이 기기에서는 Apple 로그인을 사용할 수 없습니다");
                return;
            }
            try
            {
                appleAuthManager.LoginWithAppleId(
                    new AppleAuthLoginArgs(LoginOptions.None),
                    credential =>
                    {
                        try
                        {
                            if (credential is not IAppleIDCredential appleCredential)
                            {
                                HandleInteractiveFederationFailure(
                                    capturedGeneration,
                                    "Apple 인증 정보를 확인하지 못했습니다");
                                return;
                            }
                            string identityToken = appleCredential.IdentityToken == null
                                ? string.Empty
                                : System.Text.Encoding.UTF8.GetString(
                                    appleCredential.IdentityToken);
                            HandleInteractiveFederationToken(
                                capturedGeneration,
                                !string.IsNullOrWhiteSpace(identityToken),
                                identityToken,
                                FederationType.Apple,
                                MukJumpAccountKind.Apple,
                                "Apple 로그인에 실패했습니다");
                        }
                        catch (Exception exception)
                        {
                            HandleInteractiveFederationFailure(
                                capturedGeneration,
                                "Apple 로그인에 실패했습니다",
                                exception);
                        }
                    },
                    error => HandleInteractiveFederationFailure(
                        capturedGeneration,
                        "Apple 로그인이 취소되었거나 실패했습니다"));
            }
            catch (Exception exception)
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Apple 로그인에 실패했습니다",
                    exception);
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            string serviceId = settings.AndroidAppleServiceId;
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Apple Service ID 설정이 필요합니다");
                return;
            }
            try
            {
                bool opened = TheBackend.ToolKit.AppleLogin.Android.AppleLogin(
                    serviceId,
                    out string errorMessage,
                    true,
                    token => HandleInteractiveFederationToken(
                        capturedGeneration,
                        !string.IsNullOrWhiteSpace(token),
                        token,
                        FederationType.Apple,
                        MukJumpAccountKind.Apple,
                        "Apple 로그인에 실패했습니다"));
                if (!opened)
                    HandleInteractiveFederationFailure(
                        capturedGeneration,
                        "Apple 로그인 창을 열지 못했습니다");
            }
            catch (Exception exception)
            {
                HandleInteractiveFederationFailure(
                    capturedGeneration,
                    "Apple 로그인에 실패했습니다",
                    exception);
            }
#else
            HandleInteractiveFederationFailure(
                capturedGeneration,
                "Apple 로그인은 모바일 빌드에서 확인합니다");
#endif
        }

        // iOS의 ASAuthorizationAppleIDButton은 UnitySendMessage로 이
        // 진입점을 호출한다. 실제 인증과 계정 전환 로직은 기존 단일
        // SignInWithApple 경로만 사용해 두 버튼 구현이 어긋나지 않게 한다.
        public void HandleNativeAppleSignInButton(string unused)
        {
            SignInWithApple();
        }

        void HandleFederationToken(
            bool success,
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            string failureMessage)
        {
            if (!success || string.IsNullOrWhiteSpace(token))
            {
                SetState(MukJumpAccountPhase.Error, failureMessage);
                return;
            }

            if (IsOnlineAuthenticated &&
                AccountKind == MukJumpAccountKind.BackendGuest)
            {
                UpgradeGuestFederation(
                    token,
                    type,
                    kind,
                    failureMessage,
                    0);
                return;
            }

            bool preserveLocalGuest =
                ShouldPreserveLocalGuestBeforeFederation(
                    IsOnlineAuthenticated,
                    AccountKind);
            if (preserveLocalGuest && !SaveCurrentProfileAsLocalGuest())
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "게스트 기록을 안전하게 백업하지 못해 계정 연결을 중단했습니다");
                return;
            }
            if (preserveLocalGuest)
            {
                if (!BeginPendingLocalGuestImport())
                {
                    SetState(
                        MukJumpAccountPhase.Error,
                        "게스트 기록 가져오기 복구 상태를 저장하지 못해 계정 연결을 중단했습니다");
                    return;
                }
            }
            AuthorizeExistingFederation(
                token,
                type,
                kind,
                replaceLocalProfile: true,
                restoreLocalIfServerEmpty: preserveLocalGuest);
        }

        public static bool ShouldPreserveLocalGuestBeforeFederation(
            bool isOnlineAuthenticated,
            MukJumpAccountKind accountKind) =>
            !isOnlineAuthenticated &&
            (accountKind == MukJumpAccountKind.LocalGuest ||
             accountKind == MukJumpAccountKind.BackendGuest ||
             accountKind == MukJumpAccountKind.Apple ||
             accountKind == MukJumpAccountKind.Google);

        void UpgradeGuestFederation(
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            string failureMessage,
            int attempt)
        {
            if (!BeginPendingGuestUpgrade(kind))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 연결 복구 상태를 저장하지 못해 로그인을 중단했습니다");
                return;
            }
            long capturedGeneration = ++federationRequestGeneration;
            federationRequestInFlight = true;
            federationRequestDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestGuestFederationUpgrade(token, type, bro =>
                {
                    if (capturedGeneration != federationRequestGeneration)
                        return;
                    federationRequestInFlight = false;
                    try
                    {
                        if (bro != null && bro.IsSuccess())
                        {
                            CommitConfirmedSocialAuthentication(kind);
                            ClearPendingGuestUpgrade();
                            CompleteAuthentication("게스트 기록을 계정에 연결했습니다");
                            MarkDirty();
                            return;
                        }

                        string statusCode = bro?.GetStatusCode() ?? string.Empty;
                        if (attempt == 0 && IsTransientStatusCode(statusCode))
                        {
                            UpgradeGuestFederation(
                                token,
                                type,
                                kind,
                                failureMessage,
                                attempt + 1);
                            return;
                        }

                        if (statusCode == "409")
                        {
                            ClearPendingGuestUpgrade();
                            pendingFederationToken = token;
                            pendingFederationType = type;
                            pendingFederationKind = kind;
                            SetState(
                                MukJumpAccountPhase.NeedsAccountChoice,
                                "이미 사용 중인 계정입니다. 기존 계정으로 전환하면 현재 게스트 기록은 합쳐지지 않습니다");
                            return;
                        }

                        if (ShouldRecoverGuestUpgradeWithAuthorization(
                                statusCode,
                                PlayerPrefs.HasKey(PendingGuestUpgradeKindKey)))
                        {
                            // 서버가 전환을 끝냈지만 성공 콜백 전에 앱이 종료된 경우
                            // 같은 provider 토큰으로 현재 계정을 다시 확인한다.
                            AuthorizeExistingFederation(
                                token,
                                type,
                                kind,
                                replaceLocalProfile: false);
                            return;
                        }

                        ClearPendingGuestUpgrade();
                        string message = statusCode switch
                        {
                            "403" => "로그인 정보가 만료되었습니다. 다시 로그인해 주세요",
                            "412" => "게스트 계정에서만 계정을 연결할 수 있습니다",
                            _ => failureMessage,
                        };
                        SetState(MukJumpAccountPhase.Error, message);
                    }
                    catch (Exception exception)
                    {
                        EnterAmbiguousFederationTransitionFailure(
                            "계정 연결 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != federationRequestGeneration)
                    return;
                federationRequestInFlight = false;
                EnterAmbiguousFederationTransitionFailure(
                    "계정 연결 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        void EnterAmbiguousFederationTransitionFailure(
            string context,
            Exception exception)
        {
            federationRequestInFlight = false;
            Debug.LogWarning(
                "[MukJump] " + context + ": " + exception.Message);
            EnterProviderResolutionBlock(
                "계정 전환 결과를 확인하지 못했습니다. 같은 계정으로 다시 확인해 주세요");
        }

        public static bool IsTransientStatusCode(string statusCode) =>
            statusCode == "500" || statusCode == "502" ||
            statusCode == "503";

        public static bool ShouldRecoverGuestUpgradeWithAuthorization(
            string statusCode,
            bool guestUpgradePending) =>
            guestUpgradePending && statusCode == "412";

        public void UseExistingAccountAfterConflict()
        {
            if (!HasPendingAccountConflict ||
                string.IsNullOrWhiteSpace(pendingFederationToken))
                return;
            if (!SaveCurrentProfileAsLocalGuest())
            {
                SetStatus(
                    "현재 게스트 기록을 안전하게 백업하지 못했습니다. 다시 시도해 주세요");
                return;
            }
            AuthorizeExistingFederation(
                pendingFederationToken,
                pendingFederationType,
                pendingFederationKind,
                replaceLocalProfile: true);
        }

        public void KeepCurrentGuestAfterConflict()
        {
            ClearPendingFederation();
            ClearPendingGuestUpgrade();
            SetState(MukJumpAccountPhase.OnlineReady, "현재 게스트 기록을 유지합니다");
        }

        void AuthorizeExistingFederation(
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            bool replaceLocalProfile = true,
            bool restoreLocalIfServerEmpty = false)
        {
            string previousAccountScope = KnownAccountScope();
            if (!BeginPendingAuthorizedTransition(
                    previousAccountScope,
                    kind,
                    replaceLocalProfile,
                    restoreLocalIfServerEmpty))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 전환 복구 상태를 저장하지 못해 로그인을 중단했습니다");
                return;
            }
            // 인증 주체가 바뀌는 동안 이전 계정 콜백이 UI·저장 상태를
            // 되돌리지 못하게 먼저 세대를 끊는다. 보류 순위는 성공 시
            // 계정 초기화에서 지우고, 실패하면 기존 계정 소유자로 유지한다.
            InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
            SetState(MukJumpAccountPhase.Connecting, "기존 계정으로 전환 중");
            long capturedGeneration = ++federationRequestGeneration;
            federationRequestInFlight = true;
            federationRequestDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestFederationAuthorization(token, type, bro =>
                {
                    if (capturedGeneration != federationRequestGeneration)
                        return;
                    federationRequestInFlight = false;
                    try
                    {
                        if (bro != null && bro.IsSuccess())
                        {
                            MukJumpAccountScopeRelation relation =
                                ClassifyAuthorizedTransitionAccount(
                                    previousAccountScope,
                                    CurrentAccountScope());
                            if (relation == MukJumpAccountScopeRelation.Unknown)
                            {
                                EnterProviderResolutionBlock(
                                    "로그인 계정 식별을 아직 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
                                return;
                            }
                            bool replaceAfterAuthorization =
                                ShouldReplaceLocalProfileAfterAuthorization(
                                    replaceLocalProfile,
                                    previousAccountScope,
                                    CurrentAccountScope());
                            FinishAuthorizedAccountTransition(
                                kind,
                                replaceAfterAuthorization,
                                restoreLocalIfServerEmpty &&
                                replaceAfterAuthorization,
                                "기존 계정으로 전환했습니다");
                        }
                        else
                        {
                            ClearPendingAuthorizedTransition();
                            if (restoreLocalIfServerEmpty)
                                ClearPendingLocalGuestImport();
                            RestoreAuthenticatedSessionAfterFailedTransition(
                                previousAccountScope,
                                "기존 계정 로그인에 실패했습니다");
                        }
                    }
                    catch (Exception exception)
                    {
                        EnterAmbiguousFederationTransitionFailure(
                            "기존 계정 전환 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != federationRequestGeneration)
                    return;
                federationRequestInFlight = false;
                EnterAmbiguousFederationTransitionFailure(
                    "기존 계정 전환 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        bool logoutAfterSaveRequested;
        long logoutAfterSaveSession;

        void ContinueRequestedLogout()
        {
            if (!logoutAfterSaveRequested) return;
            if (!IsOnlineAuthenticated || accountSessionGeneration != logoutAfterSaveSession)
            { logoutAfterSaveRequested = false; return; }
            if (dirty || HasAccountOperationInFlight(cloudLoadInFlight, saveInFlight,
                    leaderboardSaveInFlight, LeaderboardLoading)) return;
            logoutAfterSaveRequested = false;
            Logout();
        }

        public void Logout()
        {
            if (!IsOnlineAuthenticated)
                return;
            if (ShouldBlockLogoutDuringTemporaryBackendPause(
                    temporaryBackendPause,
                    dirty))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 연결이 복구되어 변경한 기록을 저장한 뒤 로그아웃해 주세요");
                return;
            }
            if (localLogoutCleanupPending)
            {
                SetStatus("로그아웃과 기기 기록 분리를 마무리하고 있습니다");
                return;
            }
            bool operationInFlight = HasAccountOperationInFlight(
                cloudLoadInFlight,
                saveInFlight,
                leaderboardSaveInFlight,
                LeaderboardLoading);
            if (ShouldWaitBeforeLogout(
                    dirty,
                    operationInFlight && !BlocksGameplayForAccountSync,
                    syncWriteBlocked || BlocksGameplayForAccountSync,
                    !profileResolutionPending &&
                    (AccountKind == MukJumpAccountKind.Apple ||
                     AccountKind == MukJumpAccountKind.Google)))
            {
                logoutAfterSaveRequested = true;
                logoutAfterSaveSession = accountSessionGeneration;
                if (dirty && !operationInFlight)
                    SaveNow();
                SetState(
                    MukJumpAccountPhase.Error,
                    dirty
                        ? "기록을 저장한 뒤 로그아웃합니다"
                        : "기록 동기화 후 로그아웃합니다");
                return;
            }
            if (ShouldPreserveBackendGuestBeforeLogout(AccountKind) &&
                !SaveCurrentProfileAsLocalGuest())
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "게스트 기록을 안전하게 백업하지 못해 로그아웃을 중단했습니다");
                return;
            }
            if (!BeginPendingLocalLogoutCleanup())
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그아웃 복구 상태를 저장하지 못해 작업을 중단했습니다");
                return;
            }
            InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
            SetState(MukJumpAccountPhase.Connecting, "로그아웃 중");
            MukJumpAnalytics.Account(AnalyticsAccountAction.Logout, AnalyticsOutcome.Requested);
            BeginBackendLogoutRequest();
        }

        public static bool ShouldPreserveBackendGuestBeforeLogout(
            MukJumpAccountKind accountKind) =>
            accountKind == MukJumpAccountKind.BackendGuest;

        public void RetryPendingProfileResolution()
        {
            if (!BlocksGameplayForAccountSync)
                return;
            // 사용자가 계정 또는 기록을 고르기 전에는 일반 복구 경로로
            // 들어가지 않는다. 특히 409 직후에는 아직 게스트 세션이다.
            if (HasPendingAccountConflict || HasPendingSyncConflict)
                return;
            // 인증 응답이 오기 전의 게스트 UID로 전환 결과를 복구하지 않는다.
            // 요청 감시 시간이 끝난 뒤에만 중단된 전환을 다시 확인할 수 있다.
            if (federationRequestInFlight)
                return;
            if (accountDeletionCleanupPending)
            {
                if (accountDeletionRemoteConfirmed)
                    CompleteLocalAccountDeletion();
                else if (appleDeletionInFlight || accountDeletionRequestInFlight)
                    SetStatus("서버 계정 삭제 결과를 확인하고 있습니다");
                else if (IsOnlineAuthenticated)
                {
                    string accountScope = CurrentAccountScope();
                    if (!IsPendingAccountDeletionOwnedBy(accountScope))
                    {
                        EnterProviderResolutionBlock(
                            "삭제를 요청한 계정과 현재 계정이 달라 작업을 중단했습니다");
                        return;
                    }
                    if (accountDeletionAppleRevokeRequired)
                        ReauthenticateAppleAndDelete(accountScope);
                    else
                        WithdrawBackendAccount(accountScope);
                }
                else if (Backend.IsInitialized)
                {
                    SetState(
                        MukJumpAccountPhase.Connecting,
                        "중단된 계정 삭제 상태를 다시 확인하고 있습니다");
                    BeginBackendTokenLogin(explicitRecovery: true);
                }
                else
                    SetStatus(
                        "네트워크 연결 후 계정 삭제 상태를 다시 확인해 주세요");
                return;
            }
            if (localLogoutCleanupPending)
            {
                if (localLogoutRemoteConfirmed)
                    SignOutFederationAndFinishLocalLogout();
                else if (IsOnlineAuthenticated)
                    SetStatus("로그아웃과 기기 기록 분리를 마무리하고 있습니다");
                else if (Backend.IsInitialized)
                {
                    SetState(
                        MukJumpAccountPhase.Connecting,
                        "중단된 로그아웃 상태를 다시 확인하고 있습니다");
                    BeginBackendTokenLogin(explicitRecovery: true);
                }
                else
                    SetStatus(
                        "네트워크 연결 후 로그아웃 상태를 다시 확인해 주세요");
                return;
            }
            if (!IsOnlineAuthenticated)
            {
                if (Backend.IsInitialized)
                {
                    SetState(
                        MukJumpAccountPhase.Connecting,
                        "전환 중인 계정을 다시 확인하고 있습니다");
                    BeginBackendTokenLogin(explicitRecovery: true);
                }
                else
                    SetStatus(
                        "네트워크 연결 후 앱을 다시 열거나 로컬 게스트로 돌아가 주세요");
                return;
            }
            if (HasPendingAuthorizedTransition)
            {
                // 인증된 세션이라도 중단된 전환은 일반 클라우드 로드보다 먼저
                // Finish 경계를 다시 통과해야 복구 표식이 고아로 남지 않는다.
                if (TryResumePendingAuthorizedTransition())
                    return;
                if (!profileResolutionPending)
                {
                    CompleteAuthentication(
                        "중단된 계정 전환 상태를 확인했습니다");
                    return;
                }
            }
            if (!IsPendingProfileResolutionOwnedBy(
                    PlayerPrefs.GetString(
                        PendingProfileResolutionOwnerKey,
                        string.Empty),
                    CurrentAccountScope()))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "전환 대상과 현재 로그인 계정이 달라 다시 확인할 수 없습니다. 로컬 게스트로 돌아가 주세요");
                return;
            }
            if (HasAccountOperationInFlight(
                    cloudLoadInFlight,
                    saveInFlight,
                    leaderboardSaveInFlight,
                    LeaderboardLoading))
            {
                SetStatus("계정 기록을 확인하고 있습니다");
                return;
            }

            syncWriteBlocked = false;
            retryDelaySeconds = InitialRetrySeconds;
            SetState(
                MukJumpAccountPhase.OnlineReady,
                "계정 기록을 다시 확인하고 있습니다");
            LoadCloudSnapshot();
        }

        public void ReturnToLocalGuestDuringAccountSync()
        {
            if (!BlocksGameplayForAccountSync || federationRequestInFlight)
                return;
            if (accountDeletionCleanupPending)
            {
                RetryPendingProfileResolution();
                return;
            }
            if (localLogoutRemoteConfirmed)
            {
                SignOutFederationAndFinishLocalLogout();
                return;
            }
            if (IsOnlineAuthenticated)
            {
                Logout();
                return;
            }

            // token login 실패가 단순 오프라인/서버 장애였을 수 있으므로
            // 로컬 상태만 먼저 바꾸지 않는다. crash-recovery 표식을 남긴
            // 뒤 서버 로그아웃 또는 토큰 부재가 확정돼야 로컬로 돌아간다.
            if (!localLogoutCleanupPending &&
                !BeginPendingLocalLogoutCleanup())
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "로그아웃 복구 상태를 저장하지 못해 작업을 중단했습니다");
                return;
            }
            InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
            if (!Backend.IsInitialized)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "네트워크 연결 후 로그아웃 상태를 확인해야 로컬 게스트로 돌아갈 수 있습니다");
                return;
            }

            SetState(
                MukJumpAccountPhase.Connecting,
                "계정 연결을 해제하고 로컬 기록으로 돌아가고 있습니다");
            BeginBackendTokenLogin(explicitRecovery: true);
        }

        void ResumePendingLocalLogout(BackendReturnObject tokenLoginResult)
        {
            if (tokenLoginResult == null || !tokenLoginResult.IsSuccess())
            {
                if (localLogoutRemoteConfirmed ||
                    IsDefinitiveTokenUnavailable(
                        tokenLoginResult?.GetStatusCode(),
                        tokenLoginResult?.GetErrorCode(),
                        tokenLoginResult?.GetMessage()))
                {
                    MarkPendingLocalLogoutRemoteConfirmed();
                    SignOutFederationAndFinishLocalLogout();
                }
                else
                    SetState(
                        MukJumpAccountPhase.Error,
                        "로그아웃 상태를 확인하지 못했습니다. 네트워크 연결 후 다시 시도해 주세요");
                return;
            }

            IsOnlineAuthenticated = true;
            accountSessionGeneration++;
            SetState(
                MukJumpAccountPhase.Connecting,
                "중단된 로그아웃을 마무리하고 있습니다");
            BeginBackendLogoutRequest();
        }

        void BeginBackendLogoutRequest()
        {
            backendLogoutRequestInFlight = true;
            long capturedGeneration = ++backendLogoutRequestGeneration;
            backendLogoutRequestDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestBackendLogout(logoutBro =>
                {
                    if (!backendLogoutRequestInFlight ||
                        capturedGeneration != backendLogoutRequestGeneration)
                        return;
                    backendLogoutRequestInFlight = false;
                    try
                    {
                        if (logoutBro != null && logoutBro.IsSuccess())
                        {
                            MarkPendingLocalLogoutRemoteConfirmed();
                            SignOutFederationAndFinishLocalLogout();
                            return;
                        }

                        IsOnlineAuthenticated = false;
                        SetState(
                            MukJumpAccountPhase.Error,
                            "로그아웃 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
                    }
                    catch (Exception exception)
                    {
                        HandleBackendLogoutBoundaryFailure(
                            "로그아웃 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != backendLogoutRequestGeneration)
                    return;
                backendLogoutRequestInFlight = false;
                HandleBackendLogoutBoundaryFailure(
                    "로그아웃 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        void HandleBackendLogoutBoundaryFailure(
            string context,
            Exception exception)
        {
            backendLogoutRequestInFlight = false;
            Debug.LogWarning(
                "[MukJump] " + context + ": " + exception.Message);
            IsOnlineAuthenticated = false;
            SetState(
                MukJumpAccountPhase.Error,
                "로그아웃 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
        }

        void SignOutFederationAndFinishLocalLogout()
        {
            if (!localLogoutCleanupPending ||
                !localLogoutRemoteConfirmed)
                return;
            if (localLogoutFinalizationInFlight)
            {
                SetStatus("기기 로그인 정보를 정리하고 있습니다");
                return;
            }

            localLogoutFinalizationInFlight = true;
            long capturedGeneration =
                ++localLogoutFinalizationGeneration;
            localLogoutFinalizationDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            SetState(
                MukJumpAccountPhase.Connecting,
                "기기 로그인 정보를 정리하고 있습니다");
            if (AccountKind == MukJumpAccountKind.Google)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut(
                        true,
                        (success, message) => HandleGoogleFederationSignOut(
                            capturedGeneration,
                            success,
                            message));
                }
                catch (Exception exception)
                {
                    HandleGoogleFederationSignOut(
                        capturedGeneration,
                        false,
                        exception.Message);
                }
                return;
#elif UNITY_IOS && !UNITY_EDITOR
                try
                {
                    TheBackend.ToolKit.GoogleLogin.iOS.GoogleSignOut(
                        (success, message) => HandleGoogleFederationSignOut(
                            capturedGeneration,
                            success,
                            message));
                }
                catch (Exception exception)
                {
                    HandleGoogleFederationSignOut(
                        capturedGeneration,
                        false,
                        exception.Message);
                }
                return;
#endif
            }

            FinishFederationSignOut(capturedGeneration);
        }

        void HandleGoogleFederationSignOut(
            long capturedGeneration,
            bool success,
            string message)
        {
            try
            {
                if (!success)
                    Debug.LogWarning(
                        "[MukJump] Google 기기 로그아웃 실패: " +
                        (message ?? string.Empty));
                FinishFederationSignOut(capturedGeneration);
            }
            catch (Exception exception)
            {
                localLogoutFinalizationInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 기기 로그아웃 결과를 마무리하지 못했습니다: " +
                    exception.Message);
                SetState(
                    MukJumpAccountPhase.Error,
                    "기기 로그인 정보 정리를 완료하지 못했습니다. 다시 시도해 주세요");
            }
        }

        void FinishFederationSignOut(long capturedGeneration)
        {
            if (!localLogoutFinalizationInFlight ||
                capturedGeneration != localLogoutFinalizationGeneration)
                return;
            localLogoutFinalizationInFlight = false;
            CompleteLocalLogout();
        }

        void ClearBackendGuestInfo()
        {
#if UNITY_EDITOR
            if (clearGuestInfoForTests != null)
            {
                clearGuestInfoForTests();
                return;
            }
#endif
            Backend.BMember.DeleteGuestInfo();
        }

        void CompleteLocalLogout()
        {
            localLogoutFinalizationGeneration++;
            localLogoutFinalizationInFlight = false;
            InvalidateBackendTokenLogin();
            SetAutomaticAuthenticationSuppressed(true);
            if (AccountKind != MukJumpAccountKind.LocalGuest)
            {
                try
                {
                    // 서버의 익명 계정은 삭제하지 않고 이 기기의 자동 로그인
                    // 자격만 제거한다. 저장한 스냅샷은 곧 로컬 게스트로 복원한다.
                    ClearBackendGuestInfo();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 뒤끝 게스트 로그인 정보 삭제를 다시 시도합니다: " +
                        exception.Message);
                    EnterLocalLogoutCleanupBlock(
                        "게스트 로그인 정보 정리에 실패했습니다. 다시 시도해 주세요");
                    return;
                }
            }
            bool mustRestorePendingGuest = profileResolutionPending;
            bool hasValidLocalGuestBackup =
                HasValidSavedLocalGuestProfile();
            IsOnlineAuthenticated = false;
            rowInDate = string.Empty;
            revision = 0L;
            syncWriteBlocked = false;
            replaceLocalFromServerOnNextLoad = false;
            providerResolutionBlocked = false;
            backendProviderVerificationInFlight = false;
            temporaryBackendPause = false;
            ClearPendingSyncConflict();

            if (mustRestorePendingGuest && !hasValidLocalGuestBackup)
            {
                EnterLocalLogoutCleanupBlock(
                    "전환 전 게스트 기록을 검증하지 못했습니다. 앱을 종료하지 말고 고객센터에 문의해 주세요");
                return;
            }
            if (hasValidLocalGuestBackup &&
                !RestoreSavedLocalGuestProfile())
            {
                EnterLocalLogoutCleanupBlock(
                    "로컬 게스트 기록 복원에 실패했습니다. 다시 시도해 주세요");
                return;
            }
            if (!hasValidLocalGuestBackup &&
                !mustRestorePendingGuest &&
                !TryClearAccountProgressForSwitch())
            {
                EnterLocalLogoutCleanupBlock(
                    "온라인 계정 기록을 기기에서 안전하게 분리하지 못했습니다. 다시 시도해 주세요");
                return;
            }

            dirty = false;
            PlayerPrefs.DeleteKey(PendingSaveKey);
            PlayerPrefs.DeleteKey(PendingOperationIdKey);
            PlayerPrefs.DeleteKey(RevisionKey);
            ClearPendingGuestUpgrade();
            ClearPendingProfileResolution();
            ClearPendingLocalGuestImport();
            ClearPendingFederation();
            // 원격 로그아웃과 로컬 복원이 확인된 뒤에만 포기한 계정 전환을
            // 정리한다. 남겨 두면 새 게스트도 전환 대기 상태로 계속 잠긴다.
            ClearPendingAuthorizedTransition();
            AccountKind = MukJumpAccountKind.LocalGuest;
            PlayerPrefs.DeleteKey(InvalidGuestCredentialCleanupKey);
            StoreKind();
            ClearStoredAccountScope();
            SetAutomaticAuthenticationSuppressed(false);
            // 로컬 계정 종류까지 저장된 뒤에만 crash-recovery 표식을
            // 지운다. 두 Save 사이 종료돼도 다음 실행이 정리를 재개한다.
            ClearPendingLocalLogoutCleanup();
            SetLocalReady("로그아웃했습니다. 로컬 게스트로 계속 플레이합니다");
        }

        bool BeginPendingLocalAccountDeletion(
            string accountScope,
            bool appleRevokeRequired = false)
        {
            string normalizedScope = accountScope?.Trim() ?? string.Empty;
            if (normalizedScope.Length == 0 || accountDeletionCleanupPending ||
                PlayerPrefs.GetInt(PendingLocalAccountDeletionCleanupKey, 0) != 0)
                return false;

            bool previousCleanupPending = accountDeletionCleanupPending;
            bool previousRemoteConfirmed = accountDeletionRemoteConfirmed;
            bool previousFederationCleared =
                accountDeletionFederationCleared;
            bool previousAppleRevokeRequired =
                accountDeletionAppleRevokeRequired;
            bool cleanupExisted = false;
            int previousCleanup = 0;
            bool ownerExisted = false;
            string previousOwner = string.Empty;
            bool remoteExisted = false;
            int previousRemote = 0;
            bool federationExisted = false;
            int previousFederation = 0;
            bool appleRevokeExisted = false;
            int previousAppleRevoke = 0;
            bool snapshotCaptured = false;
            try
            {
                cleanupExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionCleanupKey);
                previousCleanup = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionCleanupKey,
                    0);
                ownerExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionOwnerKey);
                previousOwner = PlayerPrefs.GetString(
                    PendingLocalAccountDeletionOwnerKey,
                    string.Empty);
                remoteExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionRemoteConfirmedKey);
                previousRemote = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionRemoteConfirmedKey,
                    0);
                federationExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionFederationClearedKey);
                previousFederation = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionFederationClearedKey,
                    0);
                appleRevokeExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey);
                previousAppleRevoke = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey,
                    0);
                snapshotCaptured = true;

                accountDeletionCleanupPending = true;
                accountDeletionRemoteConfirmed = false;
                accountDeletionFederationCleared = false;
                accountDeletionAppleRevokeRequired =
                    appleRevokeRequired;
                PlayerPrefs.SetInt(
                    PendingLocalAccountDeletionCleanupKey,
                    1);
                PlayerPrefs.SetString(
                    PendingLocalAccountDeletionOwnerKey,
                    normalizedScope);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionRemoteConfirmedKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionFederationClearedKey);
                if (appleRevokeRequired)
                    PlayerPrefs.SetInt(
                        PendingLocalAccountDeletionAppleRevokeRequiredKey,
                        AppleRevokeNotDispatched);
                else
                    PlayerPrefs.DeleteKey(
                        PendingLocalAccountDeletionAppleRevokeRequiredKey);
                FlushRecoveryMarkers();
                return PlayerPrefs.GetInt(
                           PendingLocalAccountDeletionCleanupKey,
                           0) != 0 &&
                       string.Equals(
                           PlayerPrefs.GetString(
                               PendingLocalAccountDeletionOwnerKey,
                               string.Empty),
                           normalizedScope,
                           StringComparison.Ordinal) &&
                       PlayerPrefs.GetInt(
                           PendingLocalAccountDeletionAppleRevokeRequiredKey,
                           0) == (appleRevokeRequired ? AppleRevokeNotDispatched : 0);
            }
            catch (Exception exception)
            {
                accountDeletionCleanupPending = previousCleanupPending;
                accountDeletionRemoteConfirmed = previousRemoteConfirmed;
                accountDeletionFederationCleared = previousFederationCleared;
                accountDeletionAppleRevokeRequired =
                    previousAppleRevokeRequired;
                Debug.LogWarning(
                    "[MukJump] 계정 삭제 복구 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingLocalAccountDeletionCleanupKey,
                        cleanupExisted,
                        previousCleanup);
                    RestoreStringPreference(
                        PendingLocalAccountDeletionOwnerKey,
                        ownerExisted,
                        previousOwner);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionRemoteConfirmedKey,
                        remoteExisted,
                        previousRemote);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionFederationClearedKey,
                        federationExisted,
                        previousFederation);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionAppleRevokeRequiredKey,
                        appleRevokeExisted,
                        previousAppleRevoke);
                    FlushRecoveryMarkerRollback("계정 삭제 표식");
                }
                return false;
            }
        }

        bool IsPendingAccountDeletionOwnedBy(string accountScope)
        {
            return IsPendingAccountDeletionOwnedBy(
                accountDeletionCleanupPending,
                PlayerPrefs.GetString(
                    PendingLocalAccountDeletionOwnerKey,
                    string.Empty),
                accountScope);
        }

        bool CancelPendingLocalAccountDeletionBeforeRemoteMutation(
            string accountScope)
        {
            string normalizedScope = accountScope?.Trim() ?? string.Empty;
            if (!IsPendingAccountDeletionOwnedBy(normalizedScope) ||
                ShouldKeepAppleDeletionRecoveryMarker(
                    appleDeletionRevokeDispatched,
                    PlayerPrefs.GetInt(PendingLocalAccountDeletionAppleRevokeRequiredKey, 0)))
                return false;

            bool previousCleanupPending = accountDeletionCleanupPending;
            bool previousRemoteConfirmed = accountDeletionRemoteConfirmed;
            bool previousFederationCleared = accountDeletionFederationCleared;
            bool previousAppleRevokeRequired =
                accountDeletionAppleRevokeRequired;
            bool cleanupExisted = false;
            int previousCleanup = 0;
            bool ownerExisted = false;
            string previousOwner = string.Empty;
            bool remoteExisted = false;
            int previousRemote = 0;
            bool federationExisted = false;
            int previousFederation = 0;
            bool appleRevokeExisted = false;
            int previousAppleRevoke = 0;
            bool snapshotCaptured = false;
            try
            {
                cleanupExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionCleanupKey);
                previousCleanup = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionCleanupKey,
                    0);
                ownerExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionOwnerKey);
                previousOwner = PlayerPrefs.GetString(
                    PendingLocalAccountDeletionOwnerKey,
                    string.Empty);
                remoteExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionRemoteConfirmedKey);
                previousRemote = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionRemoteConfirmedKey,
                    0);
                federationExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionFederationClearedKey);
                previousFederation = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionFederationClearedKey,
                    0);
                appleRevokeExisted = PlayerPrefs.HasKey(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey);
                previousAppleRevoke = PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey,
                    0);
                snapshotCaptured = true;

                accountDeletionCleanupPending = false;
                accountDeletionRemoteConfirmed = false;
                accountDeletionFederationCleared = false;
                accountDeletionAppleRevokeRequired = false;
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionCleanupKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionOwnerKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionRemoteConfirmedKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionFederationClearedKey);
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey);
                FlushRecoveryMarkers();
                return !PlayerPrefs.HasKey(
                           PendingLocalAccountDeletionCleanupKey) &&
                       !PlayerPrefs.HasKey(
                           PendingLocalAccountDeletionOwnerKey) &&
                       !PlayerPrefs.HasKey(
                           PendingLocalAccountDeletionAppleRevokeRequiredKey);
            }
            catch (Exception exception)
            {
                accountDeletionCleanupPending = previousCleanupPending;
                accountDeletionRemoteConfirmed = previousRemoteConfirmed;
                accountDeletionFederationCleared = previousFederationCleared;
                accountDeletionAppleRevokeRequired =
                    previousAppleRevokeRequired;
                Debug.LogWarning(
                    "[MukJump] 계정 삭제 취소 표식을 저장하지 못했습니다: " +
                    exception.Message);
                if (snapshotCaptured)
                {
                    RestoreIntPreference(
                        PendingLocalAccountDeletionCleanupKey,
                        cleanupExisted,
                        previousCleanup);
                    RestoreStringPreference(
                        PendingLocalAccountDeletionOwnerKey,
                        ownerExisted,
                        previousOwner);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionRemoteConfirmedKey,
                        remoteExisted,
                        previousRemote);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionFederationClearedKey,
                        federationExisted,
                        previousFederation);
                    RestoreIntPreference(
                        PendingLocalAccountDeletionAppleRevokeRequiredKey,
                        appleRevokeExisted,
                        previousAppleRevoke);
                    FlushRecoveryMarkerRollback("계정 삭제 취소 표식");
                }
                return false;
            }
        }

        public static bool IsPendingAccountDeletionOwnedBy(
            bool pending,
            string storedOwner,
            string currentOwner)
        {
            string stored = storedOwner?.Trim() ?? string.Empty;
            string current = currentOwner?.Trim() ?? string.Empty;
            return pending && stored.Length > 0 &&
                   string.Equals(
                       stored,
                       current,
                       StringComparison.Ordinal);
        }

        void MarkPendingAccountDeletionRemoteConfirmed()
        {
            accountDeletionRemoteConfirmed = true;
            PlayerPrefs.SetInt(
                PendingLocalAccountDeletionRemoteConfirmedKey,
                1);
            PlayerPrefs.Save();
        }

        void ResumePendingAccountDeletion(BackendReturnObject tokenLoginResult)
        {
            if (accountDeletionRemoteConfirmed)
            {
                CompleteLocalAccountDeletion();
                return;
            }
            if (tokenLoginResult != null && tokenLoginResult.IsSuccess())
            {
                string accountScope = CurrentAccountScope();
                if (!IsPendingAccountDeletionOwnedBy(accountScope))
                {
                    IsOnlineAuthenticated = false;
                    SetState(
                        MukJumpAccountPhase.Error,
                        "삭제를 요청한 계정과 현재 계정이 달라 작업을 중단했습니다. 고객센터에 문의해 주세요");
                    return;
                }

                IsOnlineAuthenticated = true;
                if (accountDeletionAppleRevokeRequired)
                {
                    ReauthenticateAppleAndDelete(accountScope);
                    return;
                }
                WithdrawBackendAccount(accountScope);
                return;
            }

            IsOnlineAuthenticated = false;
            SetState(
                MukJumpAccountPhase.Error,
                "계정 삭제 완료를 자동으로 확인할 수 없습니다. 다시 확인해도 계속되면 cysbandcs@gmail.com으로 문의해 주세요");
        }

        /// UI에서 반드시 두 단계 확인을 마친 뒤 호출한다.
        public void DeleteAccountConfirmed()
        {
            if (!IsOnlineAuthenticated ||
                Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                Phase == MukJumpAccountPhase.Deleting)
                return;
            if (temporaryBackendPause)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 연결이 복구된 뒤 계정 삭제를 다시 시도해 주세요");
                return;
            }
            if (HasAccountOperationInFlight(
                    cloudLoadInFlight,
                    saveInFlight,
                    leaderboardSaveInFlight,
                    LeaderboardLoading))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "기록 동기화가 끝난 뒤 계정 삭제를 다시 눌러 주세요");
                return;
            }
            string accountScope = CurrentAccountScope();
            bool appleRevokeRequired = false;
#if UNITY_IOS && !UNITY_EDITOR
            appleRevokeRequired =
                AccountKind == MukJumpAccountKind.Apple;
#endif
            if (!BeginPendingLocalAccountDeletion(
                    accountScope,
                    appleRevokeRequired))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "계정 삭제 복구 상태를 저장하지 못해 삭제를 시작하지 않았습니다");
                return;
            }
            InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
            SetState(MukJumpAccountPhase.Deleting, "계정을 삭제하는 중");
#if UNITY_IOS && !UNITY_EDITOR
            if (AccountKind == MukJumpAccountKind.Apple)
            {
                ReauthenticateAppleAndDelete(accountScope);
                return;
            }
#endif
            WithdrawBackendAccount(accountScope);
        }

        void ReauthenticateAppleAndDelete(string accountScope)
        {
            if (appleDeletionInFlight || accountDeletionRequestInFlight)
                return;
            if (!IsOnlineAuthenticated ||
                !IsPendingAccountDeletionOwnedBy(accountScope) ||
                !string.Equals(CurrentAccountScope(), accountScope?.Trim(), StringComparison.Ordinal))
            {
                EnterProviderResolutionBlock(
                    "삭제를 요청한 계정과 현재 계정이 달라 작업을 중단했습니다");
                return;
            }
            appleDeletionAccountScope = accountScope?.Trim() ?? string.Empty;
            appleDeletionRevokeDispatched = false;
            appleDeletionSessionGeneration = accountSessionGeneration;
            appleDeletionStage = AppleDeletionStage.VerifyingOwner;
            appleDeletionInFlight = true;
            long capturedGeneration = ++appleDeletionGeneration;
            appleDeletionDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestBackendUserInfo(bro =>
                {
                    if (!IsCurrentAppleDeletionRequest(capturedGeneration, accountScope) ||
                        appleDeletionStage != AppleDeletionStage.VerifyingOwner)
                        return;
                    try
                    {
                        JsonData root = bro != null && bro.IsSuccess()
                            ? bro.GetReturnValuetoJSON() : null;
                        JsonData row = root != null && root.IsObject && root.ContainsKey("row")
                            ? root["row"] : null;
                        string subject = ReadString(row, "federationId");
                        if (!IsVerifiedAppleDeletionOwner(
                                accountScope, ReadString(row, "inDate"),
                                ReadString(row, "subscriptionType"), subject))
                        {
                            BlockUnverifiedAppleDeletionOwner();
                            return;
                        }
                        appleDeletionStage = AppleDeletionStage.Authenticating;
                        appleDeletionDeadlineRealtime = Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
                        RequestAppleDeletionCredential(
                            (user, code) => HandleAppleDeletionCredential(
                                capturedGeneration, accountScope, subject, user, code),
                            () =>
                            {
                                if (IsCurrentAppleDeletionRequest(capturedGeneration, accountScope) &&
                                    appleDeletionStage == AppleDeletionStage.Authenticating)
                                    AbortAppleDeletionVerification("Apple 계정 확인이 취소되었습니다");
                            });
                    }
                    catch (Exception exception)
                    {
                        if (!IsCurrentAppleDeletionRequest(capturedGeneration, accountScope))
                            return;
                        if (appleDeletionStage == AppleDeletionStage.VerifyingOwner)
                            BlockUnverifiedAppleDeletionOwner(exception);
                        else if (appleDeletionStage == AppleDeletionStage.Authenticating)
                            AbortAppleDeletionVerification("Apple 계정 확인을 처리하지 못했습니다", exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (IsCurrentAppleDeletionRequest(
                        capturedGeneration,
                        accountScope) && appleDeletionStage == AppleDeletionStage.VerifyingOwner)
                    BlockUnverifiedAppleDeletionOwner(exception);
            }
        }

        void BlockUnverifiedAppleDeletionOwner(Exception exception = null)
        {
            // 서버 소유자를 확인하지 못했으므로 기존 세션을 인증된 상태로
            // 되돌리지 않는다. 전송 여부와 무관하게 삭제 의도를 보존한다.
            appleDeletionInFlight = false;
            appleDeletionGeneration++;
            if (exception != null)
                Debug.LogWarning("[MukJump] Apple 삭제 대상 서버 확인 실패: " + exception.Message);
            EnterProviderResolutionBlock("삭제할 Apple 계정을 서버에서 확인하지 못했습니다");
        }

        public static bool IsVerifiedAppleDeletionOwner(
            string expectedOwner, string serverOwner, string provider, string subject) =>
            !string.IsNullOrWhiteSpace(expectedOwner) &&
            string.Equals(expectedOwner, serverOwner, StringComparison.Ordinal) &&
            TryResolveAccountKind(provider, out MukJumpAccountKind kind) &&
            kind == MukJumpAccountKind.Apple && !string.IsNullOrWhiteSpace(subject);

        public static bool IsSameAppleDeletionSubject(string expectedSubject, string credentialSubject) =>
            !string.IsNullOrWhiteSpace(expectedSubject) &&
            string.Equals(expectedSubject, credentialSubject, StringComparison.Ordinal);

        void HandleAppleDeletionCredential(
            long generation, string owner, string expectedSubject, string credentialSubject, string code)
        {
            if (!IsCurrentAppleDeletionRequest(generation, owner) ||
                appleDeletionStage != AppleDeletionStage.Authenticating)
                return;
            if (!IsSameAppleDeletionSubject(expectedSubject, credentialSubject) ||
                string.IsNullOrWhiteSpace(code))
            {
                AbortAppleDeletionVerification("삭제할 계정과 Apple 인증 계정이 달라 삭제를 중단했습니다");
                return;
            }

            appleDeletionStage = AppleDeletionStage.Revoking;
            appleDeletionDeadlineRealtime = Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                // 저장된 전송 가능 표식은 성공 응답 전까지 단조적으로 유지한다.
                // 실패 응답도 Apple에서 아무 변화가 없었다는 증거는 아니다.
                appleDeletionRevokeDispatched = true;
                PlayerPrefs.SetInt(PendingLocalAccountDeletionAppleRevokeRequiredKey,
                    AppleRevokeMayHaveBeenDispatched);
                FlushRecoveryMarkers();
                if (PlayerPrefs.GetInt(PendingLocalAccountDeletionAppleRevokeRequiredKey, 0) !=
                    AppleRevokeMayHaveBeenDispatched)
                    throw new InvalidOperationException("Apple 연결 해제 복구 표식 확인 실패");
                if (!IsCurrentAppleDeletionRequest(generation, owner))
                    return;
                RequestAppleTokenRevoke(code, bro =>
                {
                    if (!IsCurrentAppleDeletionRequest(generation, owner) ||
                        appleDeletionStage != AppleDeletionStage.Revoking)
                        return;
                    try
                    {
                        if (bro == null || !bro.IsSuccess())
                        {
                            AbortAppleDeletionVerification("Apple 연결 해제 결과를 확인하지 못했습니다. 다시 확인해 주세요");
                            return;
                        }
                        if (!MarkAppleDeletionRevokeComplete(generation, owner))
                        {
                            AbortAppleDeletionVerification("Apple 연결 해제 상태를 저장하지 못해 삭제를 잠시 중단했습니다");
                            return;
                        }
                        if (!IsCurrentAppleDeletionRequest(generation, owner))
                            return;
                        appleDeletionInFlight = false;
                        appleDeletionRevokeDispatched = false;
                        appleDeletionGeneration++;
                        WithdrawBackendAccount(owner);
                    }
                    catch (Exception exception)
                    {
                        if (IsCurrentAppleDeletionRequest(generation, owner))
                            AbortAppleDeletionVerification("Apple 연결 해제 결과 처리에 실패했습니다", exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (IsCurrentAppleDeletionRequest(generation, owner))
                    AbortAppleDeletionVerification("Apple 연결 해제 요청에 실패했습니다", exception);
            }
        }

        void RequestAppleDeletionCredential(Action<string, string> success, Action cancelled)
        {
#if UNITY_EDITOR
            if (appleDeletionCredentialForTests != null)
            {
                appleDeletionCredentialForTests(success, cancelled);
                return;
            }
#elif UNITY_IOS
            if (appleAuthManager != null)
            {
                appleAuthManager.LoginWithAppleId(new AppleAuthLoginArgs(LoginOptions.None), credential =>
                {
                    var apple = credential as IAppleIDCredential;
                    success(apple?.User, apple?.AuthorizationCode == null ? null :
                        System.Text.Encoding.UTF8.GetString(apple.AuthorizationCode));
                }, error => cancelled());
                return;
            }
#endif
            throw new InvalidOperationException("Apple 계정 확인을 시작할 수 없습니다");
        }

        void RequestAppleTokenRevoke(string code, Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (revokeAppleTokenForTests != null)
            {
                revokeAppleTokenForTests(code, callback);
                return;
            }
#elif UNITY_IOS
            Backend.BMember.RevokeAppleToken(code, bro => callback(bro));
            return;
#endif
            throw new InvalidOperationException("Apple 연결 해제를 시작할 수 없습니다");
        }

        bool IsCurrentAppleDeletionRequest(
            long capturedGeneration,
            string expectedAccountScope)
        {
            return appleDeletionInFlight &&
                   capturedGeneration == appleDeletionGeneration &&
                   IsCurrentAccountSession(appleDeletionSessionGeneration, expectedAccountScope) &&
                   IsPendingAccountDeletionOwnedBy(expectedAccountScope) &&
                   string.Equals(
                       appleDeletionAccountScope,
                       expectedAccountScope?.Trim() ?? string.Empty,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       CurrentAccountScope(),
                       expectedAccountScope?.Trim() ?? string.Empty,
                       StringComparison.Ordinal);
        }

        void AbortAppleDeletionVerification(
            string message,
            Exception exception = null)
        {
            string accountScope = appleDeletionAccountScope;
            bool keepRecoveryMarker =
                ShouldKeepAppleDeletionRecoveryMarker(
                    appleDeletionRevokeDispatched,
                    PlayerPrefs.GetInt(PendingLocalAccountDeletionAppleRevokeRequiredKey, 0));
            appleDeletionInFlight = false;
            appleDeletionRevokeDispatched = false;
            appleDeletionGeneration++;
            if (exception != null)
                Debug.LogWarning(
                    "[MukJump] Apple 계정 삭제 확인 실패: " +
                    exception.Message);
            if (appleDeletionSessionGeneration != accountSessionGeneration ||
                !IsPendingAccountDeletionOwnedBy(accountScope) || !string.Equals(
                    CurrentAccountScope(),
                    accountScope,
                    StringComparison.Ordinal))
            {
                IsOnlineAuthenticated = false;
                EnterProviderResolutionBlock(
                    message + " 현재 계정 소유자를 다시 확인해 주세요");
                return;
            }

            if (keepRecoveryMarker)
            {
                SetState(MukJumpAccountPhase.Error, message);
                return;
            }

            if (!CancelPendingLocalAccountDeletionBeforeRemoteMutation(
                    accountScope))
            {
                IsOnlineAuthenticated = false;
                SetState(
                    MukJumpAccountPhase.Error,
                    message + " 삭제 복구 상태를 정리하지 못해 앱을 다시 실행해 주세요");
                return;
            }

            RestoreAuthenticatedSessionAfterFailedTransition(
                accountScope,
                message);
        }

        public static bool ShouldKeepAppleDeletionRecoveryMarker(
            bool revokeRequestDispatched,
            int persistedRevokeState) =>
            revokeRequestDispatched ||
            persistedRevokeState != 0 && persistedRevokeState != AppleRevokeNotDispatched;

        bool MarkAppleDeletionRevokeComplete(long generation, string owner)
        {
            if (!IsCurrentAppleDeletionRequest(generation, owner))
                return false;
            try
            {
                accountDeletionAppleRevokeRequired = false;
                PlayerPrefs.DeleteKey(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey);
                FlushRecoveryMarkers();
                if (PlayerPrefs.GetInt(
                    PendingLocalAccountDeletionAppleRevokeRequiredKey,
                    0) != 0)
                    throw new InvalidOperationException("Apple 연결 해제 완료 표식 확인 실패");
                return true;
            }
            catch (Exception exception)
            {
                accountDeletionAppleRevokeRequired = true;
                Debug.LogWarning(
                    "[MukJump] Apple 연결 해제 완료 표식을 저장하지 못했습니다: " +
                    exception.Message);
                try
                {
                    PlayerPrefs.SetInt(
                        PendingLocalAccountDeletionAppleRevokeRequiredKey,
                        AppleRevokeMayHaveBeenDispatched);
                    PlayerPrefs.Save();
                }
                catch (Exception rollbackException)
                {
                    Debug.LogWarning(
                        "[MukJump] Apple 연결 해제 복구 표식도 다시 저장하지 못했습니다: " +
                        rollbackException.Message);
                }
                return false;
            }
        }

        void WithdrawBackendAccount(string accountScope)
        {
            if (accountDeletionAppleRevokeRequired ||
                PlayerPrefs.GetInt(PendingLocalAccountDeletionAppleRevokeRequiredKey, 0) != 0)
            {
                SetStatus("Apple 연결 해제를 먼저 확인해야 계정을 삭제할 수 있습니다");
                return;
            }
            if (accountDeletionRequestInFlight)
            {
                SetStatus("서버 계정 삭제 결과를 확인하고 있습니다");
                return;
            }
            if (!accountDeletionCleanupPending)
            {
                if (!BeginPendingLocalAccountDeletion(accountScope))
                {
                    RestoreAuthenticatedSessionAfterFailedTransition(
                        accountScope,
                        "계정 삭제 복구 상태를 저장하지 못해 삭제를 시작하지 않았습니다");
                    return;
                }
            }
            else if (!IsPendingAccountDeletionOwnedBy(accountScope))
            {
                IsOnlineAuthenticated = false;
                SetState(
                    MukJumpAccountPhase.Error,
                    "삭제를 요청한 계정과 현재 계정이 달라 작업을 중단했습니다. 고객센터에 문의해 주세요");
                return;
            }

            accountDeletionRequestInFlight = true;
            long capturedGeneration = ++accountDeletionRequestGeneration;
            accountDeletionRequestDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            SetState(MukJumpAccountPhase.Deleting, "계정을 삭제하는 중");
            try
            {
                RequestBackendAccountWithdrawal(bro =>
                {
                    if (!accountDeletionRequestInFlight ||
                        capturedGeneration != accountDeletionRequestGeneration)
                        return;
                    accountDeletionRequestInFlight = false;
                    try
                    {
                        if (bro == null || !bro.IsSuccess())
                        {
                            // 서버가 요청을 처리한 직후 응답만 유실될 수도 있다.
                            // 삭제 의도 표식을 유지하고 다음 재시도에서 token 상태로
                            // 성공 여부를 판별한다.
                            IsOnlineAuthenticated = false;
                            SetState(
                                MukJumpAccountPhase.Error,
                                "계정 삭제 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
                            return;
                        }

                        MarkPendingAccountDeletionRemoteConfirmed();
                        CompleteLocalAccountDeletion();
                    }
                    catch (Exception exception)
                    {
                        HandleAccountWithdrawalBoundaryFailure(
                            "계정 삭제 결과를 처리하지 못했습니다",
                            exception);
                    }
                });
            }
            catch (Exception exception)
            {
                if (capturedGeneration != accountDeletionRequestGeneration)
                    return;
                accountDeletionRequestInFlight = false;
                HandleAccountWithdrawalBoundaryFailure(
                    "계정 삭제 요청을 시작하지 못했습니다",
                    exception);
            }
        }

        void HandleAccountWithdrawalBoundaryFailure(
            string context,
            Exception exception)
        {
            accountDeletionRequestInFlight = false;
            Debug.LogWarning(
                "[MukJump] " + context + ": " + exception.Message);
            IsOnlineAuthenticated = false;
            SetState(
                MukJumpAccountPhase.Error,
                "계정 삭제 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
        }

        void RestoreAuthenticatedSessionAfterFailedTransition(
            string expectedAccountScope,
            string message)
        {
            string currentScope = CurrentAccountScope();
            if (string.IsNullOrWhiteSpace(expectedAccountScope) ||
                !string.Equals(
                    expectedAccountScope.Trim(),
                    currentScope,
                    StringComparison.Ordinal))
            {
                IsOnlineAuthenticated = false;
                rowInDate = string.Empty;
                SetLocalReady(
                    message + " 계정 상태 확인을 위해 앱을 다시 실행해 주세요");
                return;
            }

            IsOnlineAuthenticated = true;
            BeginAuthenticatedAccountSession();
            SetState(MukJumpAccountPhase.Error, message);
        }

        public static bool RequiresFederationSignOutBeforeAccountDeletion(
            MukJumpAccountKind accountKind) =>
            accountKind == MukJumpAccountKind.Google;

        void CompleteLocalAccountDeletion()
        {
            if (!accountDeletionCleanupPending ||
                !accountDeletionRemoteConfirmed)
                return;
            if (accountDeletionFinalizationInFlight)
            {
                SetStatus("기기 로그인 정보를 정리하고 있습니다");
                return;
            }

            if (!accountDeletionFederationCleared &&
                RequiresFederationSignOutBeforeAccountDeletion(AccountKind))
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                BeginGoogleAccountDeletionSignOut();
                try
                {
                    TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut(
                        true,
                        LogGoogleAccountDeletionSignOutResult);
                }
                catch (Exception exception)
                {
                    LogGoogleAccountDeletionSignOutResult(
                        false,
                        exception.Message);
                }
                CompleteGoogleAccountDeletionSignOutAttempt();
                return;
#elif UNITY_IOS && !UNITY_EDITOR
                BeginGoogleAccountDeletionSignOut();
                try
                {
                    TheBackend.ToolKit.GoogleLogin.iOS.GoogleSignOut(
                        LogGoogleAccountDeletionSignOutResult);
                }
                catch (Exception exception)
                {
                    LogGoogleAccountDeletionSignOutResult(
                        false,
                        exception.Message);
                }
                CompleteGoogleAccountDeletionSignOutAttempt();
                return;
#endif
            }

            FinishLocalAccountDeletion();
        }

        void BeginGoogleAccountDeletionSignOut()
        {
            accountDeletionFinalizationInFlight = true;
            SetState(
                MukJumpAccountPhase.Deleting,
                "Google 기기 로그인 정보를 정리하고 있습니다");
        }

        static void LogGoogleAccountDeletionSignOutResult(
            bool success,
            string message)
        {
            if (success)
                return;

            // 서버 탈퇴는 이미 끝났다. Google SDK의 로컬 캐시 정리는
            // best-effort로 처리해 콜백 누락이나 오류로 사용자를 가두지 않는다.
            Debug.LogWarning(
                "[MukJump] 계정 삭제 후 Google 기기 로그아웃을 완료하지 못했습니다: " +
                (message ?? string.Empty));
        }

        void CompleteGoogleAccountDeletionSignOutAttempt()
        {
            accountDeletionFederationCleared = true;
            PlayerPrefs.SetInt(
                PendingLocalAccountDeletionFederationClearedKey,
                1);
            PlayerPrefs.Save();
            FinishLocalAccountDeletion();
        }

        void FinishLocalAccountDeletion()
        {
            if (!accountDeletionCleanupPending ||
                !accountDeletionRemoteConfirmed)
                return;

            accountDeletionFinalizationInFlight = false;
            accountDeletionRequestGeneration++;
            accountDeletionRequestInFlight = false;
            SetAutomaticAuthenticationSuppressed(true);
            bool backendLocalAccountCleared = true;
            try
            {
                // WithdrawAccount 성공 시 서버 토큰은 이미 폐기된다. SDK가
                // 보관한 게스트 인증 정보도 지워 다음 실행의 자동 재가입을 막는다.
                ClearBackendGuestInfo();
            }
            catch (Exception exception)
            {
                backendLocalAccountCleared = false;
                Debug.LogWarning(
                    "[MukJump] 뒤끝 기기 계정 정보 삭제를 다시 시도합니다: " +
                    exception.Message);
            }
            InvalidateAccountScopedOperations(clearPendingLeaderboard: true);
            suppressDirty = true;
            bool scoreCleared = false;
            bool growthCleared = false;
            bool settingsCleared = false;
            bool identityCleared = false;
            try
            {
                try
                {
                    scoreCleared =
                        ScoreManager.TryClearForAccountDeletion();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 최고 기록 삭제를 다시 시도합니다: " +
                        exception.Message);
                }
                try
                {
                    growthCleared =
                        PermanentGrowthProfile.TryClearForAccountDeletion();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 성장 기록 삭제를 다시 시도합니다: " +
                        exception.Message);
                }
                try
                {
                    settingsCleared =
                        LobbySettingsProfile.TryResetForAccountDeletion();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 설정 기록 삭제를 다시 시도합니다: " +
                        exception.Message);
                }
                identityCleared = MukJumpIdentityProfile.TryResetForAccountDeletion(
                    PlayerPrefs.GetString(PendingLocalAccountDeletionOwnerKey, string.Empty));
            }
            finally
            {
                suppressDirty = false;
            }

            bool localCleared =
                backendLocalAccountCleared &&
                scoreCleared &&
                growthCleared &&
                settingsCleared && identityCleared;
            if (!localCleared)
            {
                IsOnlineAuthenticated = false;
                rowInDate = string.Empty;
                revision = 0L;
                dirty = false;
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 계정은 삭제했습니다. 기기 데이터 삭제를 다시 시도해 주세요");
                return;
            }

            IsOnlineAuthenticated = false;
            rowInDate = string.Empty;
            revision = 0L;
            dirty = false;
            profileResolutionPending = false;
            localLogoutCleanupPending = false;
            localLogoutRemoteConfirmed = false;
            providerResolutionBlocked = false;
            backendProviderVerificationInFlight = false;
            temporaryBackendPause = false;
            ClearPendingFederation();
            leaderboardEntries.Clear();
            LeaderboardLoading = false;
            LeaderboardStatus =
                "최고 고도 순위를 불러오는 중";
            AccountKind = MukJumpAccountKind.LocalGuest;
            PlayerPrefs.DeleteKey(KindKey);
            PlayerPrefs.DeleteKey(RevisionKey);
            PlayerPrefs.DeleteKey(PendingSaveKey);
            PlayerPrefs.DeleteKey(PendingOperationIdKey);
            PlayerPrefs.DeleteKey(LocalGuestSnapshotKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportKey);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingLocalGuestImportRestoredOwnerKey);
            PlayerPrefs.DeleteKey(PendingProfileResolutionOwnerKey);
            PlayerPrefs.DeleteKey(PendingAuthorizedTransitionKindKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionPreviousOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionReplaceLocalKey);
            PlayerPrefs.DeleteKey(
                PendingAuthorizedTransitionRestoreGuestKey);
            PlayerPrefs.DeleteKey(PendingGuestUpgradeKindKey);
            PlayerPrefs.DeleteKey(PendingLocalLogoutCleanupKey);
            PlayerPrefs.DeleteKey(InvalidGuestCredentialCleanupKey);
            PlayerPrefs.DeleteKey(
                PendingLocalLogoutRemoteConfirmedKey);
            // 로컬 데이터 삭제가 모두 성공한 뒤 마지막 저장에서만 계정
            // 삭제 복구 표식을 제거한다. 중간 종료 시 다음 실행이 멱등하게
            // 기기 삭제를 다시 수행한다.
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionCleanupKey);
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionRemoteConfirmedKey);
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionFederationClearedKey);
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionAppleRevokeRequiredKey);
            PlayerPrefs.DeleteKey(PendingLeaderboardBestKey);
            PlayerPrefs.DeleteKey(PendingLeaderboardOwnerKey);
            PlayerPrefs.DeleteKey(StoredAccountScopeKey);
            suppressAutomaticAuthentication = false;
            PlayerPrefs.DeleteKey(AutomaticAuthenticationSuppressedKey);
            PlayerPrefs.Save();

            accountDeletionCleanupPending = false;
            accountDeletionRemoteConfirmed = false;
            accountDeletionFederationCleared = false;
            accountDeletionAppleRevokeRequired = false;
            SetLocalReady("계정과 연결된 서버·기기 데이터를 삭제했습니다");
            // 삭제 직후의 새 게스트 연결은 일반 오류 재시도 지연을 물려받지 않는다.
            // Splash가 보이는 동안 연결을 시작하되 오프라인 플레이는 계속 허용한다.
            guestReconnectDelaySeconds = InitialRetrySeconds;
            guestReconnectAtRealtime = Time.realtimeSinceStartup;
            if (!StartupBrandSplash.TryRestartAfterAccountDeletion())
                SetStatus("계정은 삭제했습니다. 첫 안내를 시작하려면 앱을 다시 실행해 주세요");
        }

        void SetAutomaticAuthenticationSuppressed(bool suppressed)
        {
            suppressAutomaticAuthentication = suppressed;
            if (suppressed)
                PlayerPrefs.SetInt(
                    AutomaticAuthenticationSuppressedKey,
                    1);
            else
                PlayerPrefs.DeleteKey(
                    AutomaticAuthenticationSuppressedKey);
            PlayerPrefs.Save();
        }

        public static bool ShouldClearAutomaticAuthenticationSuppression(
            MukJumpAccountKind accountKind) =>
            accountKind == MukJumpAccountKind.Google ||
            accountKind == MukJumpAccountKind.Apple;

        public static bool RequiresLegacySocialOwnershipVerification(
            MukJumpAccountKind accountKind,
            string storedAccountScope) =>
            ShouldClearAutomaticAuthenticationSuppression(accountKind) &&
            string.IsNullOrWhiteSpace(storedAccountScope);

        public static bool ShouldBlockLegacySocialFallback(
            bool backendAvailable,
            bool backendInitialized,
            MukJumpAccountKind accountKind,
            string storedAccountScope) =>
            (!backendAvailable || !backendInitialized) &&
            RequiresLegacySocialOwnershipVerification(
                accountKind,
                storedAccountScope);

        bool SaveCurrentProfileAsLocalGuest()
        {
            try
            {
                int best = ScoreManager.Instance != null
                    ? ScoreManager.Instance.Best
                    : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
                MukJumpCloudSnapshot snapshot = MukJumpCloudSnapshot.Capture(
                    best,
                    Math.Max(0L, revision),
                    string.Empty);
                if (!snapshot.IsSupported ||
                    !PermanentGrowthProfile.IsSupportedCloudJson(
                        snapshot.growthJson))
                    return false;

                PlayerPrefs.SetString(
                    LocalGuestSnapshotKey,
                    JsonUtility.ToJson(snapshot));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 로컬 게스트 기록을 백업하지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        bool HasValidSavedLocalGuestProfile()
        {
            string json = PlayerPrefs.GetString(
                LocalGuestSnapshotKey,
                string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                MukJumpCloudSnapshot snapshot =
                    JsonUtility.FromJson<MukJumpCloudSnapshot>(json);
                return snapshot != null &&
                       snapshot.IsSupported &&
                       PermanentGrowthProfile.IsSupportedCloudJson(
                           snapshot.growthJson);
            }
            catch (Exception)
            {
                return false;
            }
        }

        bool TryClearAccountProgressForSwitch()
        {
            // 늦게 도착한 로그인/로그아웃 콜백이 진행 중인 판의 기록과 성장을
            // 지우지 못하게 한다. 계정 범위 교체는 로비에서만 허용한다.
            if (GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Lobby)
                return false;

            int previousBest;
            string previousGrowthJson;
            LobbyCloudSettingsSnapshot previousSettings;
            bool previousDirty = dirty;
            long previousMutationVersion = localMutationVersion;
            bool pendingSaveExisted;
            int previousPendingSave;
            bool operationIdExisted;
            string previousOperationId;
            bool revisionExisted;
            string previousRevisionMarker;
            try
            {
                previousBest = ScoreManager.Instance != null
                    ? ScoreManager.Instance.Best
                    : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
                if (!PermanentGrowthProfile.TryExportCloudJson(
                        out previousGrowthJson) ||
                    string.IsNullOrWhiteSpace(previousGrowthJson))
                    return false;
                previousSettings =
                    LobbySettingsProfile.CaptureCloudSettings();
                pendingSaveExisted = PlayerPrefs.HasKey(PendingSaveKey);
                previousPendingSave = PlayerPrefs.GetInt(PendingSaveKey, 0);
                operationIdExisted =
                    PlayerPrefs.HasKey(PendingOperationIdKey);
                previousOperationId = PlayerPrefs.GetString(
                    PendingOperationIdKey,
                    string.Empty);
                revisionExisted = PlayerPrefs.HasKey(RevisionKey);
                previousRevisionMarker = PlayerPrefs.GetString(
                    RevisionKey,
                    string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 계정 전환 전 로컬 상태를 읽지 못했습니다: " +
                    exception.Message);
                return false;
            }
            InvalidateAccountScopedOperations(clearPendingLeaderboard: true);
            bool previousSuppressDirty = suppressDirty;
            bool scoreAttempted = false;
            bool growthAttempted = false;
            bool settingsAttempted = false;
            suppressDirty = true;
            try
            {
                scoreAttempted = true;
                if (!ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0))
                    throw new InvalidOperationException(
                        "계정 전환 전 최고 기록을 비우지 못했습니다.");
                growthAttempted = true;
                if (!PermanentGrowthProfile.TryClearForAccountDeletion())
                    throw new InvalidOperationException(
                        "계정 전환 전 성장 기록을 비우지 못했습니다.");
                settingsAttempted = true;
                LobbySettingsProfile.ApplyCloudSettings(1f, 1f, 0);
                PlayerPrefs.DeleteKey(PendingSaveKey);
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
                PlayerPrefs.DeleteKey(RevisionKey);
                PlayerPrefs.Save();
                dirty = false;
                localMutationVersion++;
                return true;
            }
            catch (Exception exception)
            {
                bool rollbackComplete = RollbackCloudSnapshotApplication(
                    previousBest,
                    previousGrowthJson,
                    previousSettings,
                    growthAttempted,
                    scoreAttempted,
                    settingsAttempted);
                dirty = previousDirty;
                localMutationVersion = previousMutationVersion;
                rollbackComplete &= TryRestoreAccountSwitchMarkers(
                    pendingSaveExisted,
                    previousPendingSave,
                    operationIdExisted,
                    previousOperationId,
                    revisionExisted,
                    previousRevisionMarker);
                Debug.LogWarning(
                    "[MukJump] 계정 전환용 로컬 초기화에 실패해 이전 상태로 " +
                    "되돌렸습니다: " + exception.Message +
                    (rollbackComplete
                        ? string.Empty
                        : " (이전 상태의 내구 저장은 완료하지 못했습니다)"));
                return false;
            }
            finally
            {
                suppressDirty = previousSuppressDirty;
            }
        }

        static bool TryRestoreAccountSwitchMarkers(
            bool pendingSaveExisted,
            int previousPendingSave,
            bool operationIdExisted,
            string previousOperationId,
            bool revisionExisted,
            string previousRevisionMarker)
        {
            try
            {
                if (pendingSaveExisted)
                    PlayerPrefs.SetInt(PendingSaveKey, previousPendingSave);
                else
                    PlayerPrefs.DeleteKey(PendingSaveKey);
                if (operationIdExisted)
                    PlayerPrefs.SetString(
                        PendingOperationIdKey,
                        previousOperationId ?? string.Empty);
                else
                    PlayerPrefs.DeleteKey(PendingOperationIdKey);
                if (revisionExisted)
                    PlayerPrefs.SetString(
                        RevisionKey,
                        previousRevisionMarker ?? string.Empty);
                else
                    PlayerPrefs.DeleteKey(RevisionKey);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 계정 전환 실패 뒤 로컬 표식을 복원하지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        bool RestoreSavedLocalGuestProfile()
        {
            bool previousSuppressDirty = suppressDirty;
            bool previousDirty = dirty;
            long previousMutationVersion = localMutationVersion;
            int previousBest = 0;
            string previousGrowthJson = string.Empty;
            LobbyCloudSettingsSnapshot previousSettings = default;
            bool rollbackStateCaptured = false;
            bool growthAttempted = false;
            bool bestAttempted = false;
            bool settingsAttempted = false;
            bool pendingMarkerCaptured = false;
            bool pendingMarkerExisted = false;
            int previousPendingMarker = 0;
            try
            {
                string json = PlayerPrefs.GetString(
                    LocalGuestSnapshotKey,
                    string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                    return false;
                MukJumpCloudSnapshot snapshot =
                    JsonUtility.FromJson<MukJumpCloudSnapshot>(json);
                if (snapshot == null || !snapshot.IsSupported ||
                    !PermanentGrowthProfile.IsSupportedCloudJson(
                        snapshot.growthJson))
                    return false;

                previousBest = ScoreManager.Instance != null
                    ? ScoreManager.Instance.Best
                    : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
                if (!PermanentGrowthProfile.TryExportCloudJson(
                        out previousGrowthJson) ||
                    string.IsNullOrWhiteSpace(previousGrowthJson))
                    throw new InvalidOperationException(
                        "현재 성장 기록을 rollback용으로 읽지 못했습니다.");
                previousSettings = LobbySettingsProfile.CaptureCloudSettings();
                pendingMarkerExisted = PlayerPrefs.HasKey(PendingSaveKey);
                previousPendingMarker = PlayerPrefs.GetInt(PendingSaveKey, 0);
                pendingMarkerCaptured = true;
                rollbackStateCaptured = true;
                suppressDirty = true;
                growthAttempted = true;
                if (!PermanentGrowthProfile.TryReplaceFromCloudJson(
                        snapshot.growthJson))
                    throw new InvalidOperationException(
                        "게스트 성장 기록을 적용하지 못했습니다.");
                bestAttempted = true;
                if (!ScoreManager.TryReplaceVerifiedBestForAccountSwitch(
                        snapshot.bestHeight))
                    throw new InvalidOperationException(
                        "게스트 최고 기록을 적용하지 못했습니다.");
                settingsAttempted = true;
                LobbySettingsProfile.ApplyCloudSettings(
                    snapshot.bgmVolume,
                    snapshot.sfxVolume,
                    snapshot.tutorialVersion);
                PlayerPrefs.SetInt(PendingSaveKey, 1);
                PlayerPrefs.Save();
                dirty = true;
                localMutationVersion++;
                return true;
            }
            catch (Exception exception)
            {
                bool rollbackComplete = !rollbackStateCaptured ||
                    RollbackCloudSnapshotApplication(
                        previousBest,
                        previousGrowthJson,
                        previousSettings,
                        growthAttempted,
                        bestAttempted,
                        settingsAttempted);
                dirty = previousDirty;
                localMutationVersion = previousMutationVersion;
                if (pendingMarkerCaptured)
                    rollbackComplete &= TryRestorePendingSaveMarker(
                        pendingMarkerExisted,
                        previousPendingMarker);
                Debug.LogWarning(
                    "[MukJump] 로컬 게스트 기록을 복원하지 못했습니다: " +
                    exception.Message +
                    (rollbackComplete
                        ? string.Empty
                        : " (이전 로컬 상태 복원도 완료하지 못했습니다)"));
                return false;
            }
            finally
            {
                suppressDirty = previousSuppressDirty;
            }
        }

        static bool TryRestorePendingSaveMarker(
            bool existed,
            int previousValue)
        {
            try
            {
                if (existed)
                    PlayerPrefs.SetInt(PendingSaveKey, previousValue);
                else
                    PlayerPrefs.DeleteKey(PendingSaveKey);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 이전 클라우드 저장 대기 표식을 복원하지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        public void NotifyRunSettled(GameOverResult result)
        {
            if (result.PersistenceState != GameOverPersistenceState.Complete ||
                !result.RecordsAllowedForResult())
                return;
            MarkDirty();
        }

        /// 결과 정산·광고 선택보다 먼저 로컬 최고 기록이 확정됐음을 알린다.
        /// 리더보드 제출은 완료된 판만 허용하되 클라우드 저장 예약은 즉시 남긴다.
        public void NotifyBestCommitted(int bestHeight)
        {
            if (bestHeight > 0)
                MarkDirty();
        }

        // 서버 행의 소유자와 프로필을 확인한 지점에서만 예약한다.
        // 전환 잠금이 남아 있어도 예약은 보존하고 실제 전송은 기존 잠금에 맡긴다.
        void QueueVerifiedLeaderboardBest(int bestHeight)
        {
            if (!IsOnlineAuthenticated || !PermanentGrowthProfile.HasCompletedRun ||
                string.IsNullOrWhiteSpace(rowInDate))
                return;
            string scope = CurrentAccountScope();
            if (!string.IsNullOrWhiteSpace(scope))
                StorePendingLeaderboardBest(scope, Mathf.Max(0, bestHeight));
        }

        string leaderboardSubmissionFailure = string.Empty;

        bool HasOwnedPendingLeaderboardBest =>
            PlayerPrefs.HasKey(PendingLeaderboardBestKey) &&
            IsPendingLeaderboardOwnedBy(
                PlayerPrefs.GetString(PendingLeaderboardOwnerKey, string.Empty),
                CurrentAccountScope());

        public void SubmitBestHeight(int bestHeight)
        {
            // 로그인·빈 저장 행 생성은 플레이 기록이 아니다. 0m라도 실제 정산한 판만 등록한다.
            if (!PermanentGrowthProfile.HasCompletedRun) return;
            if (!IsOnlineAuthenticated || syncWriteBlocked ||
                BlocksGameplayForAccountSync || temporaryBackendPause ||
                Phase == MukJumpAccountPhase.Connecting ||
                Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                Phase == MukJumpAccountPhase.Deleting ||
                string.IsNullOrWhiteSpace(rowInDate) ||
                settings == null ||
                !settings.HasRequiredRuntimeValues ||
                string.IsNullOrWhiteSpace(settings.AllTimeRankUuid))
                return;

            int normalizedBest = Mathf.Max(0, bestHeight);
            string accountScope = CurrentAccountScope();
            if (string.IsNullOrWhiteSpace(accountScope))
                return;
            // 요청을 보내기 전에 영속화해야 앱 종료나 계정 세대 전환으로
            // 콜백이 사라져도 다음 로그인에서 다시 제출할 수 있다.
            if (!StorePendingLeaderboardBest(accountScope, normalizedBest))
            {
                SetStatus("기록 순위 재시도 상태를 저장하지 못했습니다");
                return;
            }
            // 리더보드 API도 같은 게임 정보 행을 쓴다. 저장과 겹치지 않게 하고
            // 연속 요청 중 가장 큰 미제출 최고 기록만 보낸다.
            if (leaderboardSaveInFlight || dirty || saveInFlight || cloudLoadInFlight)
                return;
            normalizedBest = Mathf.Max(normalizedBest,
                PlayerPrefs.GetInt(PendingLeaderboardBestKey, 0));

            leaderboardSaveInFlight = true;
            leaderboardSaveDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = accountScope;
            try
            {
                RequestLeaderboardUpdate(
                    normalizedBest,
                    bro =>
                    {
                        if (!IsCurrentAccountSession(
                                capturedSessionGeneration,
                                capturedAccountScope))
                            return;
                        leaderboardSaveInFlight = false;
                        try
                        {
                            if (bro == null || !bro.IsSuccess())
                            {
                                // 응답 본문에는 개인정보가 들어갈 수 있으므로 HTTP 코드만 남긴다.
                                string failure = "기록은 저장됐지만 순위 등록을 재시도 중입니다 (" +
                                    (bro == null ? "응답 없음" : bro.GetStatusCode()) + ")";
                                if (leaderboardSubmissionFailure != failure)
                                    Debug.Log("[MukJump] " + failure);
                                leaderboardSubmissionFailure = failure;
                                StorePendingLeaderboardBest(
                                    capturedAccountScope,
                                    normalizedBest);
                                leaderboardRetryAtRealtime =
                                    Time.realtimeSinceStartup + InitialRetrySeconds;
                                SetStatus(failure);
                                LeaderboardStatus = failure;
                                NotifyStateChangedSafely();
                            }
                            else
                            {
                                leaderboardSubmissionFailure = string.Empty;
                                int pending = IsPendingLeaderboardOwnedBy(
                                        PlayerPrefs.GetString(
                                            PendingLeaderboardOwnerKey,
                                            string.Empty),
                                        capturedAccountScope)
                                    ? PlayerPrefs.GetInt(
                                        PendingLeaderboardBestKey,
                                        0)
                                    : 0;
                                if (pending <= normalizedBest)
                                    ClearPendingLeaderboardSave(
                                        saveImmediately: false);
                                leaderboardRetryAtRealtime =
                                    Time.realtimeSinceStartup + InitialRetrySeconds;
                                PlayerPrefs.Save();
                                if (leaderboardRequested)
                                    RefreshLeaderboard();
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                "[MukJump] 순위 제출 결과를 처리하지 못했습니다: " +
                                exception.Message);
                            StorePendingLeaderboardBest(
                                capturedAccountScope,
                                normalizedBest);
                            leaderboardRetryAtRealtime =
                                Time.realtimeSinceStartup + InitialRetrySeconds;
                            SetStatus(
                                "기록 순위 등록은 연결 복구 후 다시 시도합니다");
                        }
                    });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                leaderboardSaveInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 순위 제출 요청을 시작하지 못했습니다: " +
                    exception.Message);
                StorePendingLeaderboardBest(
                    capturedAccountScope,
                    normalizedBest);
                leaderboardRetryAtRealtime =
                    Time.realtimeSinceStartup + InitialRetrySeconds;
                SetStatus("기록 순위 등록은 연결 복구 후 다시 시도합니다");
            }
        }

        /// 실제 뒤끝에 등록된 순위·공개 닉네임·최고 고도의 전체 TOP 10 조회다.
        bool leaderboardAwaitingAuthentication;

        public void RefreshLeaderboard()
        {
            leaderboardRequested = true;
            if (LeaderboardLoading)
            {
                leaderboardRefreshQueued = true;
                return;
            }
            if (!IsOnlineAuthenticated || settings == null ||
                string.IsNullOrWhiteSpace(settings.AllTimeRankUuid) ||
                Phase == MukJumpAccountPhase.Connecting || Phase == MukJumpAccountPhase.Deleting)
            {
                leaderboardAwaitingAuthentication = !IsOnlineAuthenticated || Phase == MukJumpAccountPhase.Connecting;
                LeaderboardStatus = Application.internetReachability == NetworkReachability.NotReachable ||
                    settings == null || string.IsNullOrWhiteSpace(settings.AllTimeRankUuid) || suppressAutomaticAuthentication
                    ? "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요"
                    : "최고 고도 순위를 불러오는 중";
                if (!IsOnlineAuthenticated) TryReconnectGuest();
                NotifyStateChangedSafely();
                return;
            }

            leaderboardAwaitingAuthentication = false;
            LeaderboardLoading = true;
            leaderboardRefreshQueued = false;
            leaderboardLoadDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            LeaderboardStatus = "최고 고도 순위를 불러오는 중";
            NotifyStateChangedSafely();
            try
            {
                RequestLeaderboard(bro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;
                    LeaderboardLoading = false;
                    try
                    {
                        if (bro == null || !bro.IsSuccess())
                        {
                            LeaderboardStatus =
                                "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요";
                            NotifyStateChangedSafely();
                            return;
                        }

                        var loadedEntries =
                            new List<MukJumpLeaderboardEntry>(10);
                        List<BackEnd.Leaderboard.UserLeaderboardItem> items =
                            bro.GetUserLeaderboardList();
                        if (items != null)
                        {
                            for (int i = 0; i < items.Count; i++)
                            {
                                BackEnd.Leaderboard.UserLeaderboardItem item =
                                    items[i];
                                int rank = int.TryParse(
                                        item.rank,
                                        out int parsedRank)
                                    ? parsedRank
                                    : i + 1;
                                int height = int.TryParse(
                                        item.score,
                                        out int parsedHeight)
                                    ? parsedHeight
                                    : 0;
                                loadedEntries.Add(
                                    new MukJumpLeaderboardEntry(rank, height, item.nickname,
                                        regionCode: item.extraData));
                            }
                        }

                        leaderboardEntries.Clear();
                        leaderboardEntries.AddRange(loadedEntries);
                        LeaderboardStatus = HasOwnedPendingLeaderboardBest
                            ? (string.IsNullOrEmpty(leaderboardSubmissionFailure)
                                ? "저장된 최고 기록을 순위에 반영하는 중입니다"
                                : leaderboardSubmissionFailure)
                            : leaderboardEntries.Count == 0
                            ? "아직 등록된 최고 고도 기록이 없습니다"
                            : "전체 최고 고도 TOP 10";
                        NotifyStateChangedSafely();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[MukJump] 순위 조회 결과를 처리하지 못했습니다: " +
                            exception.Message);
                        LeaderboardStatus =
                            "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요";
                        NotifyStateChangedSafely();
                    }
                    finally
                    {
                        // 점수·닉네임 제출보다 먼저 시작한 조회가 늦게 끝났으면
                        // 현재 조회를 끝낸 뒤 한 번만 최신 목록으로 다시 읽는다.
                        if (leaderboardRefreshQueued)
                        {
                            leaderboardRefreshQueued = false;
                            RefreshLeaderboard();
                        }
                    }
                });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                LeaderboardLoading = false;
                Debug.LogWarning(
                    "[MukJump] 순위 조회 요청을 시작하지 못했습니다: " +
                    exception.Message);
                LeaderboardStatus =
                    "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요";
                NotifyStateChangedSafely();
            }
        }

        void MarkDirty()
        {
            if (suppressDirty)
                return;

            bool wasAlreadyDirty = dirty;
            localMutationVersion++;
            dirty = true;
            saveAtRealtime = Time.realtimeSinceStartup + SaveDebounceSeconds;
            try
            {
                bool pendingMarkerNeedsFlush = ShouldFlushPendingSaveMarker(
                    wasAlreadyDirty,
                    PlayerPrefs.HasKey(PendingSaveKey));
                PlayerPrefs.SetInt(PendingSaveKey, 1);
                if (pendingMarkerNeedsFlush)
                    PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                // 로컬 원본 저장은 이미 완료됐다. 클라우드 재시도 표식을
                // 남기지 못해도 정산 UI까지 중단하지 않고, 메모리 dirty 상태로
                // 다음 Update/일시정지 저장 기회를 유지한다.
                Debug.LogWarning(
                    "[MukJump] 클라우드 저장 대기 표식을 남기지 못했습니다: " +
                    exception.Message);
            }
        }

        public static bool ShouldFlushPendingSaveMarker(
            bool wasAlreadyDirty,
            bool pendingKeyExists) =>
            !wasAlreadyDirty || !pendingKeyExists;

        public void SaveNow()
        {
            // 조회 A 뒤 저장 B가 먼저 끝나면 A의 옛 스냅샷이 최신 진행을
            // 덮을 수 있다. 계정 읽기와 쓰기는 항상 하나씩 진행한다.
            if (!dirty || !IsOnlineAuthenticated || saveInFlight ||
                cloudLoadInFlight || resumeCloudLoadPending || leaderboardSaveInFlight ||
                syncWriteBlocked || temporaryBackendPause || BlocksGameplayForAccountSync ||
                settings == null ||
                Phase == MukJumpAccountPhase.Connecting ||
                Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                Phase == MukJumpAccountPhase.Deleting)
                return;
            if (string.IsNullOrWhiteSpace(rowInDate))
            {
                LoadCloudSnapshot();
                return;
            }

            if (!TryCaptureNextSnapshot(
                    "로컬 성장 기록을 확인한 뒤 서버 저장을 다시 시도합니다",
                    out MukJumpCloudSnapshot snapshot))
                return;
            long capturedMutationVersion = localMutationVersion;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            if (!snapshot.IsSupported)
            {
                KeepSavePending("로컬 성장 기록을 확인한 뒤 서버 저장을 다시 시도합니다");
                return;
            }
            saveInFlight = true;
            saveDeadlineRealtime =
                Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            try
            {
                RequestMyGameData(
                    settings.PlayerTableName,
                    new Where(),
                    loadBro =>
                    {
                        if (!IsCurrentAccountSession(
                                capturedSessionGeneration,
                                capturedAccountScope))
                            return;

                        try
                        {
                            if (loadBro == null || !loadBro.IsSuccess())
                            {
                                saveInFlight = false;
                                KeepSavePending(
                                    "저장 전 서버 기록 확인을 다시 시도합니다");
                                return;
                            }

                            JsonData rows = loadBro.FlattenRows();
                            if (rows == null || rows.Count == 0)
                            {
                                saveInFlight = false;
                                EnterFatalSyncBlock(
                                    "서버 저장을 찾지 못해 덮어쓰기를 중단했습니다. 앱을 다시 실행해 계정 상태를 확인해 주세요");
                                return;
                            }
                            if (rows.Count != 1)
                            {
                                saveInFlight = false;
                                EnterFatalSyncBlock(
                                    "서버 저장이 중복되어 덮어쓰기를 중단했습니다. 고객센터에 문의해 주세요");
                                return;
                            }

                            JsonData row = rows[0];
                            string verifiedRowInDate =
                                ReadString(row, "inDate");
                            MukJumpCloudSnapshot server = ReadSnapshot(row);
                            if (!server.IsSupported ||
                                !PermanentGrowthProfile.IsSupportedCloudJson(
                                    server.growthJson) ||
                                string.IsNullOrWhiteSpace(verifiedRowInDate))
                            {
                                saveInFlight = false;
                                EnterFatalSyncBlock(
                                    "서버 저장 대상을 검증하지 못해 덮어쓰기를 중단했습니다");
                                return;
                            }

                            if (string.Equals(rowInDate, verifiedRowInDate, StringComparison.Ordinal) &&
                                AcknowledgePreviouslyCommittedWrite(server))
                            {
                                saveInFlight = false;
                                KeepSavePending("이전 저장을 확인했습니다. 최신 기록을 이어서 저장합니다");
                                return;
                            }

                            if (!string.Equals(
                                    rowInDate,
                                    verifiedRowInDate,
                                    StringComparison.Ordinal) ||
                                server.revision != revision)
                            {
                                saveInFlight = false;
                                BeginSyncConflict(server, verifiedRowInDate);
                                return;
                            }

                            UpdateVerifiedSnapshot(
                                snapshot,
                                capturedMutationVersion,
                                capturedSessionGeneration,
                                capturedAccountScope,
                                server.lastOperationId);
                        }
                        catch (Exception exception)
                        {
                            saveInFlight = false;
                            Debug.LogWarning(
                                "[MukJump] 저장 전 서버 기록을 검증하지 못했습니다: " +
                                exception.Message);
                            KeepSavePending(
                                "저장 전 서버 기록 확인을 다시 시도합니다");
                        }
                    });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                saveInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 저장 전 서버 확인 요청을 시작하지 못했습니다: " +
                    exception.Message);
                KeepSavePending("저장 전 서버 기록 확인을 다시 시도합니다");
            }
        }

        void UpdateVerifiedSnapshot(
            MukJumpCloudSnapshot snapshot,
            long capturedMutationVersion,
            long capturedSessionGeneration,
            string capturedAccountScope,
            string expectedOperationId)
        {
            try
            {
                var revisionWhere = BuildCloudUpdateCondition(revision, expectedOperationId);
                RequestUpdateGameData(
                    settings.PlayerTableName,
                    revisionWhere,
                    ToParam(snapshot),
                    bro =>
                    {
                        if (!IsCurrentAccountSession(
                                capturedSessionGeneration,
                                capturedAccountScope))
                            return;
                        saveInFlight = false;
                        try
                        {
                            if (bro != null && bro.IsSuccess())
                            {
                                CompleteSuccessfulSave(
                                    snapshot,
                                    capturedMutationVersion);
                                try
                                {
                                    SubmitBestHeight(snapshot.bestHeight);
                                }
                                catch (Exception leaderboardException)
                                {
                                    Debug.LogWarning(
                                        "[MukJump] 저장 후 순위 제출을 시작하지 못했습니다: " +
                                        leaderboardException.Message);
                                }
                            }
                            else
                            {
                                string statusCode = bro?.GetStatusCode() ??
                                                    string.Empty;
                                if (statusCode == "404" || statusCode == "409")
                                {
                                    // revision 조건이 더 이상 맞지 않으면 최신 행을 다시
                                    // 읽어 사용자가 서버/기기 기록을 직접 고르게 한다.
                                    LoadCloudSnapshot();
                                }
                                else
                                {
                                    // 원문 응답에는 사용자 데이터가 포함될 수 있어 출력하지 않는다.
                                    // 상태/오류 식별자만 보존해 영구 오류와 일시 실패를 구분한다.
                                    string failure = FormatCloudSaveFailure(
                                        statusCode, bro?.GetErrorCode());
                                    Debug.LogWarning("[MukJump] " + failure);
                                    KeepSavePending(failure);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                "[MukJump] 서버 저장 결과를 마무리하지 못했습니다: " +
                                exception.Message);
                            KeepSavePending("서버 저장을 다시 시도합니다");
                        }
                    });
            }
            catch (Exception exception)
            {
                if (!IsCurrentAccountSession(
                        capturedSessionGeneration,
                        capturedAccountScope))
                    return;
                saveInFlight = false;
                Debug.LogWarning(
                    "[MukJump] 서버 저장 요청을 시작하지 못했습니다: " +
                    exception.Message);
                KeepSavePending("서버 저장을 다시 시도합니다");
            }
        }

        bool TryCaptureNextSnapshot(
            string failureMessage,
            out MukJumpCloudSnapshot snapshot)
        {
            try
            {
#if UNITY_EDITOR
                snapshot = captureNextSnapshotForTests != null
                    ? captureNextSnapshotForTests()
                    : CaptureNextSnapshot();
#else
                snapshot = CaptureNextSnapshot();
#endif
                return true;
            }
            catch (Exception exception)
            {
                snapshot = null;
                Debug.LogWarning(
                    "[MukJump] 클라우드 저장 스냅샷을 만들지 못했습니다: " +
                    exception.Message);
                KeepSavePending(failureMessage);
                return false;
            }
        }

        void RequestBackendInitialization(
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (initializeBackendForTests != null)
            {
                initializeBackendForTests(callback);
                return;
            }
#endif
            Backend.InitializeAsync(bro => callback(bro));
        }

        void RequestBackendTokenLogin(
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (tokenLoginForTests != null)
            {
                tokenLoginForTests(callback);
                return;
            }
#endif
            Backend.BMember.LoginWithTheBackendToken(bro => callback(bro));
        }

        void RequestBackendGuestLogin(
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (guestLoginForTests != null)
            {
                guestLoginForTests(callback);
                return;
            }
#endif
            Backend.BMember.GuestLogin(bro => callback(bro));
        }

        void RequestBackendUserInfo(
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (getUserInfoForTests != null)
            {
                getUserInfoForTests(callback);
                return;
            }
#endif
            Backend.BMember.GetUserInfo(bro => callback(bro));
        }

        void RequestGuestFederationUpgrade(
            string token,
            FederationType type,
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (changeFederationForTests != null)
            {
                changeFederationForTests(callback);
                return;
            }
#endif
            Backend.BMember.ChangeCustomToFederation(
                token,
                type,
                bro => callback(bro));
        }

        void RequestFederationAuthorization(
            string token,
            FederationType type,
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (authorizeFederationForTests != null)
            {
                authorizeFederationForTests(callback);
                return;
            }
#endif
            Backend.BMember.AuthorizeFederation(
                token,
                type,
                bro => callback(bro));
        }

        void RequestBackendLogout(Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (logoutForTests != null)
            {
                logoutForTests(callback);
                return;
            }
#endif
            Backend.BMember.Logout(bro => callback(bro));
        }

        void RequestBackendAccountWithdrawal(
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (withdrawAccountForTests != null)
            {
                withdrawAccountForTests(callback);
                return;
            }
#endif
            Backend.BMember.WithdrawAccount(bro => callback(bro));
        }

        void RequestLeaderboardUpdate(int bestHeight, Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (updateLeaderboardForTests != null)
            {
                updateLeaderboardForTests(bestHeight, callback);
                return;
            }
#endif
            var param = new Param();
            param.Add(settings.BestHeightColumn, bestHeight);
            param.Add(DeviceRegion.Column, DeviceRegion.Current);
            Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(
                settings.AllTimeRankUuid, settings.PlayerTableName, rowInDate, param,
                bro => callback(bro));
        }

        void RequestLeaderboard(
            Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>
                callback)
        {
#if UNITY_EDITOR
            if (getLeaderboardForTests != null)
            {
                getLeaderboardForTests(callback);
                return;
            }
#endif
            Backend.Leaderboard.User.GetLeaderboard(
                settings.AllTimeRankUuid,
                10,
                bro => callback(bro));
        }

        void RequestMyGameData(
            string tableName,
            Where where,
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (getMyDataForTests != null)
            {
                getMyDataForTests(callback);
                return;
            }
#endif
            Backend.GameData.GetMyData(tableName, where, bro => callback(bro));
        }

        void RequestInsertGameData(
            string tableName,
            Param param,
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (insertGameDataForTests != null)
            {
                insertGameDataForTests(callback);
                return;
            }
#endif
            Backend.GameData.Insert(tableName, param, bro => callback(bro));
        }

        void RequestUpdateGameData(
            string tableName,
            Where where,
            Param param,
            Action<BackendReturnObject> callback)
        {
#if UNITY_EDITOR
            if (updateGameDataForTests != null)
            {
                updateGameDataForTests(callback);
                return;
            }
#endif
            Backend.GameData.Update(
                tableName,
                where,
                param,
                bro => callback(bro));
        }

        MukJumpCloudSnapshot CaptureNextSnapshot()
        {
            int best = ScoreManager.Instance != null
                ? ScoreManager.Instance.Best
                : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
            string operationId = ResolvePendingOperationId(
                PlayerPrefs.GetString(PendingOperationIdKey, string.Empty));
            PlayerPrefs.SetString(PendingOperationIdKey, operationId);
            PlayerPrefs.Save();
            return MukJumpCloudSnapshot.Capture(
                best,
                revision + 1L,
                operationId);
        }

        public static string ResolvePendingOperationId(string existing)
        {
            string normalized = existing?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(normalized)
                ? Guid.NewGuid().ToString("N")
                : normalized;
        }

        Param ToParam(MukJumpCloudSnapshot snapshot)
        {
            var param = new Param();
            param.Add("schemaVersion", snapshot.schemaVersion);
            param.Add(DeviceRegion.Column, DeviceRegion.Current);
            param.Add(settings.BestHeightColumn, snapshot.bestHeight);
            param.Add("growthJson", snapshot.growthJson);
            param.Add("bgmVolume", snapshot.bgmVolume);
            param.Add("sfxVolume", snapshot.sfxVolume);
            param.Add("tutorialVersion", snapshot.tutorialVersion);
            param.Add("revision", snapshot.revision);
            param.Add("updatedAtUtc", snapshot.updatedAtUtc);
            param.Add("lastOperationId", snapshot.lastOperationId);
            return param;
        }

        MukJumpCloudSnapshot ReadSnapshot(JsonData row)
        {
            return new MukJumpCloudSnapshot
            {
                schemaVersion = ReadInt(row, "schemaVersion"),
                bestHeight = ReadInt(row, settings.BestHeightColumn),
                growthJson = ReadString(row, "growthJson"),
                bgmVolume = ReadFloat(row, "bgmVolume", 1f),
                sfxVolume = ReadFloat(row, "sfxVolume", 1f),
                tutorialVersion = ReadInt(row, "tutorialVersion"),
                revision = ReadLong(row, "revision"),
                updatedAtUtc = ReadString(row, "updatedAtUtc"),
                lastOperationId = ReadString(row, "lastOperationId"),
            };
        }

        void CompleteSuccessfulSave(
            MukJumpCloudSnapshot snapshot,
            long capturedMutationVersion)
        {
            revision = Math.Max(revision, snapshot.revision);
            dirty = ShouldKeepDirtyAfterSave(
                capturedMutationVersion,
                localMutationVersion);
            retryDelaySeconds = InitialRetrySeconds;
            try
            {
                PersistRevision();
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
                if (dirty)
                {
                    PlayerPrefs.SetInt(PendingSaveKey, 1);
                    saveAtRealtime =
                        Time.realtimeSinceStartup + SaveDebounceSeconds;
                }
                else
                {
                    PlayerPrefs.DeleteKey(PendingSaveKey);
                }
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                // 서버 반영은 이미 성공했으므로 revision은 되돌리지 않는다.
                // 로컬 완료 표식만 재시도해 다음 로드에서 서버와 재검증한다.
                Debug.LogWarning(
                    "[MukJump] 서버 저장 완료 상태를 기기에 남기지 못했습니다: " +
                    exception.Message);
                KeepSavePending("서버 저장 완료 상태를 다시 확인합니다");
                return;
            }
            SetState(
                MukJumpAccountPhase.OnlineReady,
                dirty ? "새 변경 사항을 이어서 저장하는 중" : "동기화 완료");
            QueueVerifiedLeaderboardBest(snapshot.bestHeight);
        }

        public static bool ShouldKeepDirtyAfterSave(
            long capturedMutationVersion,
            long currentMutationVersion) =>
            currentMutationVersion != capturedMutationVersion;

        public static Where BuildCloudUpdateCondition(long expectedRevision, string expectedOperationId)
        {
            // inDate는 서버 기본 키라 QueryFilter에 넣으면 실제 서버에서 400이 난다.
            // 소유자의 단일 행/inDate를 직전 조회에서 검증하고 비기본 키로 변경 세대를 검사한다.
            var where = new Where();
            where.Equal("revision", expectedRevision);
            if (!string.IsNullOrWhiteSpace(expectedOperationId))
                where.Equal("lastOperationId", expectedOperationId);
            return where;
        }

        public static string FormatCloudSaveFailure(string statusCode, string errorCode)
        {
            bool validStatus = statusCode != null && statusCode.Length == 3 &&
                int.TryParse(statusCode, out int status) && status >= 100 && status <= 599;
            string safeStatus = validStatus ? statusCode : "NO_RESPONSE";
            string safeError = string.Empty;
            if (!string.IsNullOrEmpty(errorCode) && errorCode.Length <= 64)
            {
                bool valid = true;
                foreach (char c in errorCode)
                    valid &= c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' ||
                             c >= '0' && c <= '9' || c == '_';
                if (valid) safeError = "/" + errorCode;
            }
            return "서버 저장을 다시 시도합니다 (" + safeStatus + safeError + ")";
        }

        void KeepSavePending(string message)
        {
            dirty = true;
            saveAtRealtime = Time.realtimeSinceStartup + retryDelaySeconds;
            retryDelaySeconds = CalculateNextRetryDelay(retryDelaySeconds);
            try
            {
                PlayerPrefs.SetInt(PendingSaveKey, 1);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                // 디스크 표식 실패가 메모리 latch 해제와 다음 재시도를 막으면
                // 로그아웃까지 영구 대기하므로 메모리 dirty 상태를 우선한다.
                Debug.LogWarning(
                    "[MukJump] 클라우드 저장 재시도 표식을 남기지 못했습니다: " +
                    exception.Message);
            }
            SetStatus(message);
        }

        void EnterFatalSyncBlock(string message)
        {
            syncWriteBlocked = true;
            dirty = true;
            try
            {
                PlayerPrefs.SetInt(PendingSaveKey, 1);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MukJump] 동기화 차단 복구 표식을 남기지 못했습니다: " +
                    exception.Message);
            }
            SetState(MukJumpAccountPhase.Error, message);
        }

        public static float CalculateNextRetryDelay(float currentSeconds)
        {
            float current = Mathf.Clamp(
                currentSeconds,
                InitialRetrySeconds,
                MaximumRetrySeconds);
            return Mathf.Min(MaximumRetrySeconds, current * 2f);
        }

        bool CanStartInteractiveLogin()
        {
            if (!CanUseBackend())
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 연결을 확인할 수 없습니다. 네트워크를 확인한 뒤 다시 시도해 주세요");
                return false;
            }
            if (!Backend.IsInitialized)
            {
                // 첫 연결이 실패한 상태에서 연동 버튼을 눌러도 Error에 갇히지
                // 않고 초기화→기존 토큰/신규 게스트 흐름을 다시 시작한다.
                if (!backendInitializationInFlight)
                    BeginBackendInitialization();
                return false;
            }
            if (temporaryBackendPause)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 연결을 복구한 뒤 계정 연결을 다시 시도해 주세요");
                return false;
            }
            if (Phase == MukJumpAccountPhase.Connecting ||
                Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                Phase == MukJumpAccountPhase.Deleting)
                return false;
            if (HasAccountOperationInFlight(
                    cloudLoadInFlight,
                    saveInFlight,
                    leaderboardSaveInFlight,
                    LeaderboardLoading))
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "기록 동기화가 끝난 뒤 계정 연결을 다시 눌러 주세요");
                return false;
            }
            return true;
        }

        public static bool HasAccountOperationInFlight(
            bool cloudLoad,
            bool cloudSave,
            bool leaderboardSave,
            bool leaderboardLoad) =>
            cloudLoad || cloudSave || leaderboardSave || leaderboardLoad;

        public static bool ShouldWaitBeforeLogout(
            bool dirty,
            bool operationInFlight,
            bool saveBlocked = false,
            bool preserveUnsyncedProfile = false) =>
            // 미해결 계정 전환은 게스트로 돌아갈 수 있지만, 이미 플레이하던
            // 연동 계정의 미저장 기록은 저장 오류가 있어도 로그아웃으로 지우지 않는다.
            operationInFlight || (dirty && (!saveBlocked || preserveUnsyncedProfile));

        void SetLocalReady(string message)
        {
            guestReconnectAtRealtime = Time.realtimeSinceStartup + guestReconnectDelaySeconds;
            IsOnlineAuthenticated = false;
            AccountKind = MukJumpAccountKind.LocalGuest;
            SetState(MukJumpAccountPhase.LocalReady, message);
        }

        void SetAccountOfflinePreservingKind(string message)
        {
            guestReconnectAtRealtime = Time.realtimeSinceStartup + guestReconnectDelaySeconds;
            IsOnlineAuthenticated = false;
            // 서버의 계정 종류 확인이 일시적으로 실패해도 기존 Apple·Google·
            // 뒤끝 게스트를 새 로컬 게스트로 재분류하지 않는다. 그래야 다음
            // 소셜 로그인에서 다른 계정으로 현재 기록을 잘못 가져가지 않는다.
            AccountKind = ReadStoredKind();
            SetState(MukJumpAccountPhase.LocalReady, message);
        }

        void EnterProviderResolutionBlock(string message)
        {
            IsOnlineAuthenticated = false;
            syncWriteBlocked = true;
            providerResolutionBlocked = true;
            AccountKind = ReadStoredKind();
            SetState(MukJumpAccountPhase.Error, message);
        }

        void SetState(MukJumpAccountPhase phase, string message)
        {
            if (Phase != phase) MukJumpAnalytics.AccountState(phase);
            Phase = phase;
            StatusMessage = message ?? string.Empty;
            NotifyStateChangedSafely();
        }

        void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            NotifyStateChangedSafely();
        }

        void NotifyStateChangedSafely()
        {
            Action listeners = StateChanged;
            if (listeners == null)
                return;

            foreach (Action listener in listeners.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    // 계정 UI는 상태의 관찰자일 뿐이다. 깨진 화면 구독자 하나가
                    // 인증·탈퇴·저장·순위 요청 자체를 중단하면 안 된다.
                    Debug.LogWarning(
                        "[MukJump] 계정 상태 알림 구독자 예외를 격리했습니다: " +
                        exception.Message,
                        this);
                }
            }
        }

        void StoreKind()
        {
            PlayerPrefs.SetInt(KindKey, (int)AccountKind);
            PlayerPrefs.Save();
        }

        MukJumpAccountKind ReadStoredKind()
        {
            int raw = PlayerPrefs.GetInt(
                KindKey,
                (int)MukJumpAccountKind.LocalGuest);
            return Enum.IsDefined(typeof(MukJumpAccountKind), raw)
                ? (MukJumpAccountKind)raw
                : MukJumpAccountKind.LocalGuest;
        }

        void PersistRevision()
        {
            PlayerPrefs.SetString(RevisionKey, revision.ToString());
            PlayerPrefs.Save();
        }

        void ClearPendingFederation()
        {
            pendingFederationToken = string.Empty;
            pendingFederationType = default;
            pendingFederationKind = default;
        }

        static int ReadInt(JsonData row, string key) =>
            int.TryParse(ReadString(row, key), out int value) ? value : 0;
        static long ReadLong(JsonData row, string key) =>
            long.TryParse(ReadString(row, key), out long value) ? value : 0L;
        static float ReadFloat(JsonData row, string key, float fallback) =>
            float.TryParse(
                ReadString(row, key),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value)
                ? LobbySettingsProfile.NormalizeVolume(value, fallback)
                : fallback;
        static string ReadString(JsonData row, string key) =>
            row != null && row.IsObject && row.ContainsKey(key) &&
            row[key] != null
                ? row[key].ToString()
                : string.Empty;
        static long ParseLong(string value) =>
            long.TryParse(value, out long parsed) ? parsed : 0L;
    }

    static class GameOverResultAccountExtensions
    {
        public static bool RecordsAllowedForResult(this GameOverResult result) =>
            result.RewardsAllowed && result.RecordSaved;
    }
}
#else
using System;
using System.Collections.Generic;
using AppsInToss;
using UnityEngine;

namespace MukJump.Core
{
    /// Apps in Toss에서는 토스의 게스트 식별과 게임센터를 사용한다.
    /// 모바일 전용 뒤끝 SDK를 WebGL 번들에서 완전히 제외하기 위한 경량 대체 구현이다.
    [DisallowMultipleComponent]
    public sealed class MukJumpAccountRuntime : MonoBehaviour
    {
        const int IdentityApiTimeoutMilliseconds = 10000;
        const float InitialRetrySeconds = 5f;
        const float MaximumRetrySeconds = 60f;

        static readonly IReadOnlyList<MukJumpLeaderboardEntry> EmptyEntries =
            Array.Empty<MukJumpLeaderboardEntry>();

        public static MukJumpAccountRuntime Instance { get; private set; }
        public static bool GoogleSignInEnabled => false;
        public MukJumpAccountKind AccountKind => MukJumpAccountKind.LocalGuest;
        public MukJumpAccountPhase Phase { get; private set; } =
            MukJumpAccountPhase.Connecting;
        public bool IsOnlineAuthenticated => false;
        public string StatusMessage { get; private set; } =
            "토스 사용자 기록을 확인하고 있습니다";
        public string SupportCode => string.Empty;
        public string PlayerId => string.Empty;
        public void RefreshDisplayIdentity() { }
        public string BackendUid => string.Empty;
        public string Nickname => string.Empty;
        public string NicknameStatus => string.Empty;
        public bool IsNicknameBusy => false;
        public bool NeedsNicknameSetup => false;
        public bool CanChangeNickname => false;
        public void ChangeNickname(string value, Action<bool, string> completed) =>
            completed?.Invoke(false, "토스에서는 지원하지 않는 기능입니다");
        public bool HasPendingAccountConflict => false;
        public bool HasPendingSyncConflict => false;
        public bool HasPendingAccountDeletionCleanup => false;
        public bool HasPendingAuthorizedTransition => false;
        public bool IsTemporaryBackendPaused => false;
        public bool HasVerifiedAppsInTossIdentity =>
            AppsInTossIdentityPolicy.HasVerifiedIdentity;
        public bool CanReturnToLocalGuestDuringAccountSync => false;
        public bool BlocksGameplayForAccountSync =>
            Phase != MukJumpAccountPhase.OnlineReady ||
            !HasVerifiedAppsInTossIdentity;
        public IReadOnlyList<MukJumpLeaderboardEntry> LeaderboardEntries =>
            EmptyEntries;
        public string LeaderboardStatus => HasVerifiedAppsInTossIdentity
            ? "토스 게임센터에서 최고 고도 순위를 확인할 수 있습니다"
            : StatusMessage;
        public bool LeaderboardLoading => identityResolutionInFlight;

        public event Action StateChanged;

        bool identityResolutionInFlight;
        int identityResolutionGeneration;
        float retryDelaySeconds = InitialRetrySeconds;
        float retryAtUnscaledTime = float.PositiveInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null)
                return;
            var root = new GameObject(nameof(MukJumpAccountRuntime));
            DontDestroyOnLoad(root);
            root.AddComponent<MukJumpAccountRuntime>();
        }

        void OnEnable()
        {
            Instance = this;
            AppsInTossIdentityPolicy.MarkIdentityUnverified();
            BeginIdentityResolution();
        }

        void OnDisable()
        {
            if (Instance == this)
            {
                identityResolutionGeneration++;
                identityResolutionInFlight = false;
                AppsInTossIdentityPolicy.MarkIdentityUnverified();
                Instance = null;
            }
        }

        void Update()
        {
            if (!identityResolutionInFlight &&
                Phase == MukJumpAccountPhase.Error &&
                Time.unscaledTime >= retryAtUnscaledTime)
                BeginIdentityResolution();
        }

        public void SignInWithGoogle() { }
        public void SignInWithApple() { }
        public void UseExistingAccountAfterConflict() { }
        public void KeepCurrentGuestAfterConflict() { }
        public void UseServerAfterSyncConflict() { }
        public void KeepThisDeviceAfterSyncConflict() { }
        public void Logout() { }
        public void RetryPendingProfileResolution()
        {
            if (!identityResolutionInFlight)
                BeginIdentityResolution();
        }
        public void ReturnToLocalGuestDuringAccountSync() { }
        public void DeleteAccountConfirmed() { }
        public void NotifyRunSettled(GameOverResult result) { }
        // 토스 게임센터 제출은 GameManager가 완료·보상·기록 저장을 모두
        // 검증한 GameOverResult만 전달한다. 단순 로컬 best 저장 알림은 제출하지 않는다.
        public void NotifyBestCommitted(int bestHeight) { }
        public void SubmitBestHeight(int bestHeight) { }
        public void RefreshLeaderboard()
        {
            if (Phase == MukJumpAccountPhase.OnlineReady)
                AppsInTossGameCenterRuntime.OpenLeaderboard();
        }

        async void BeginIdentityResolution()
        {
            if (!isActiveAndEnabled || identityResolutionInFlight)
                return;

            int generation = ++identityResolutionGeneration;
            identityResolutionInFlight = true;
            retryAtUnscaledTime = float.PositiveInfinity;
            SetState(
                MukJumpAccountPhase.Connecting,
                "토스 사용자 기록을 확인하고 있습니다");
            try
            {
                string rawUserKey = await AIT.GetUserKeyForGame(
                    IdentityApiTimeoutMilliseconds);
                if (!IsCurrentResolution(generation))
                    return;
                if (!AppsInTossIdentityPolicy.TryExtractUserHash(
                        rawUserKey,
                        out string userHash))
                {
                    FailIdentityResolution(
                        generation,
                        "토스 게임 사용자 정보를 확인하지 못했습니다. 다시 확인해 주세요");
                    return;
                }

                GameCenterGameProfileResponse profile =
                    await AIT.GetGameCenterGameProfile(
                        IdentityApiTimeoutMilliseconds);
                if (!IsCurrentResolution(generation))
                    return;
                if (profile == null ||
                    !AppsInTossIdentityPolicy.IsGameCenterProfileReady(
                        profile.StatusCode))
                {
                    FailIdentityResolution(
                        generation,
                        "토스 게임센터 프로필을 확인한 뒤 다시 시도해 주세요");
                    return;
                }

                if (!AppsInTossIdentityPolicy.TryBindVerifiedIdentity(
                        userHash,
                        out AppsInTossOwnerBindingDecision decision))
                {
                    bool ownerMismatch = decision ==
                        AppsInTossOwnerBindingDecision.RejectOwnerMismatch;
                    FailIdentityResolution(
                        generation,
                        ownerMismatch
                            ? "이 기기의 기록은 다른 토스 사용자에게 연결되어 있습니다. 기록이 섞이지 않도록 시작을 막았습니다"
                            : "토스 사용자 정보를 안전하게 저장하지 못했습니다. 다시 확인해 주세요",
                        scheduleAutomaticRetry: !ownerMismatch);
                    return;
                }
                if (!IsCurrentResolution(generation))
                {
                    AppsInTossIdentityPolicy.MarkIdentityUnverified();
                    return;
                }

                bool growthReady =
                    PermanentGrowthProfile
                        .TryReloadAfterAppsInTossIdentity();
                bool scoreReady =
                    ScoreManager.TryReloadAfterAppsInTossIdentity();
                bool settlementReady =
                    GameManager.TryRecoverPendingGameOverSettlement();
                if (!growthReady || !scoreReady || !settlementReady)
                {
                    FailIdentityResolution(
                        generation,
                        "토스 사용자 기록을 불러오지 못했습니다. 다시 확인해 주세요");
                    return;
                }

                identityResolutionInFlight = false;
                retryDelaySeconds = InitialRetrySeconds;
                SetState(
                    MukJumpAccountPhase.OnlineReady,
                    "토스 사용자 기록을 확인했습니다");
                AppsInTossGameCenterRuntime
                    .RetryPendingScoreForVerifiedUser();
            }
            catch (AITClientTimeoutException)
            {
                FailIdentityResolution(
                    generation,
                    "토스 사용자 확인 시간이 초과됐습니다. 네트워크를 확인하고 다시 시도해 주세요");
            }
            catch (AITException)
            {
                FailIdentityResolution(
                    generation,
                    "토스 사용자 정보를 확인하지 못했습니다. 다시 시도해 주세요");
            }
            catch (Exception)
            {
                FailIdentityResolution(
                    generation,
                    "토스 사용자 기록을 준비하지 못했습니다. 다시 시도해 주세요");
            }
        }

        bool IsCurrentResolution(int generation) =>
            this != null && isActiveAndEnabled && Instance == this &&
            identityResolutionGeneration == generation;

        void FailIdentityResolution(
            int generation,
            string message,
            bool scheduleAutomaticRetry = true)
        {
            if (!IsCurrentResolution(generation))
                return;
            identityResolutionInFlight = false;
            AppsInTossIdentityPolicy.MarkIdentityUnverified();
            SetState(MukJumpAccountPhase.Error, message);
            if (!scheduleAutomaticRetry)
            {
                retryAtUnscaledTime = float.PositiveInfinity;
                return;
            }
            retryAtUnscaledTime = Time.unscaledTime + retryDelaySeconds;
            retryDelaySeconds = Mathf.Min(
                MaximumRetrySeconds,
                retryDelaySeconds * 2f);
        }

        void SetState(MukJumpAccountPhase phase, string message)
        {
            Phase = phase;
            StatusMessage = message ?? string.Empty;
            NotifyStateChangedSafely();
        }

        void NotifyStateChangedSafely()
        {
            Action listeners = StateChanged;
            if (listeners == null)
                return;

            foreach (Action listener in listeners.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MukJump] 계정 상태 알림 구독자 예외를 격리했습니다: " +
                        exception.Message,
                        this);
                }
            }
        }
        public void SaveNow() { }
    }
}
#endif
