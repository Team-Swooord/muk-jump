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
    /// 로컬 플레이를 항상 먼저 열고, 설정된 출시 빌드에서만 뒤끝 계정과
    /// 비동기 동기화를 붙인다. 네트워크 실패는 게임 진입을 막지 않는다.
    [DisallowMultipleComponent]
    public sealed class MukJumpAccountRuntime : MonoBehaviour
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
        const string PendingLeaderboardBestKey =
            "MukJump.Cloud.PendingLeaderboardBest";
        const string PendingLeaderboardOwnerKey =
            "MukJump.Cloud.PendingLeaderboardOwner";
        const float SaveDebounceSeconds = 2f;
        const float InitialRetrySeconds = 5f;
        const float MaximumRetrySeconds = 60f;

        public static MukJumpAccountRuntime Instance { get; private set; }

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
        public bool BlocksGameplayForAccountSync =>
            profileResolutionPending ||
            localLogoutCleanupPending ||
            accountDeletionCleanupPending;
        public IReadOnlyList<MukJumpLeaderboardEntry> LeaderboardEntries =>
            leaderboardEntries;
        public string LeaderboardStatus { get; private set; } =
            "계정 연결 후 최고 고도 순위를 확인할 수 있습니다";
        public bool LeaderboardLoading { get; private set; }

        public event Action StateChanged;

        MukJumpBackendSettings settings;
        string rowInDate = string.Empty;
        string pendingFederationToken = string.Empty;
        FederationType pendingFederationType;
        MukJumpAccountKind pendingFederationKind;
        bool saveInFlight;
        bool cloudLoadInFlight;
        bool suppressDirty;
        bool dirty;
        bool replaceLocalFromServerOnNextLoad;
        bool restoreLocalGuestIfServerEmptyOnNextLoad;
        bool profileResolutionPending;
        bool localLogoutCleanupPending;
        bool localLogoutRemoteConfirmed;
        bool accountDeletionCleanupPending;
        bool accountDeletionRemoteConfirmed;
        bool accountDeletionRequestInFlight;
        bool syncWriteBlocked;
        bool leaderboardSaveInFlight;
        float saveAtRealtime;
        float leaderboardRetryAtRealtime;
        long revision;
        long localMutationVersion;
        long accountSessionGeneration;
        long tokenLoginGeneration;
        bool tokenLoginInFlight;
        bool suppressAutomaticAuthentication;
        long localLogoutFinalizationGeneration;
        bool localLogoutFinalizationInFlight;
        long accountDeletionRequestGeneration;
        MukJumpCloudSnapshot pendingServerSnapshot;
        string pendingServerRowInDate = string.Empty;
        float retryDelaySeconds = InitialRetrySeconds;
        readonly List<MukJumpLeaderboardEntry> leaderboardEntries = new(10);

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
            PermanentGrowthProfile.Changed += MarkDirty;
            LobbySettingsProfile.Changed += MarkDirty;
        }

        void OnDisable()
        {
            PermanentGrowthProfile.Changed -= MarkDirty;
            LobbySettingsProfile.Changed -= MarkDirty;
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            settings = MukJumpBackendSettings.Load();
            revision = Math.Max(0L, ParseLong(PlayerPrefs.GetString(
                RevisionKey,
                "0")));
            AccountKind = ReadStoredKind();
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
            dirty = PlayerPrefs.GetInt(PendingSaveKey, 0) != 0;
            localMutationVersion = dirty ? 1L : 0L;

#if UNITY_IOS && !UNITY_EDITOR
            if (AppleAuthManager.IsCurrentPlatformSupported)
                appleAuthManager = new AppleAuthManager(
                    new PayloadDeserializer());
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
                SetLocalReady(
                    "서버 설정 전에도 게스트로 모든 콘텐츠를 플레이할 수 있습니다");
                return;
            }

            SetState(MukJumpAccountPhase.Connecting, "계정 연결 확인 중");
            Backend.InitializeAsync(HandleInitialized);
        }

        void Update()
        {
#if UNITY_IOS && !UNITY_EDITOR
            appleAuthManager?.Update();
#endif
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
                if (pendingBest > 0 &&
                    IsPendingLeaderboardOwnedBy(
                        PlayerPrefs.GetString(
                            PendingLeaderboardOwnerKey,
                            string.Empty),
                        accountScope))
                    SubmitBestHeight(pendingBest);
                else if (pendingBest > 0 &&
                         !string.IsNullOrWhiteSpace(accountScope))
                {
                    // 이전 계정에서 남은 재시도 값을 새 계정으로 제출하지 않는다.
                    ClearPendingLeaderboardSave();
                }
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
            // 에디터 테스트나 WebGL 미니앱이 운영 뒤끝 프로젝트에 접속하면
            // 테스트 계정·저장 데이터가 실제 통계에 섞일 수 있다. 1.0의 뒤끝
            // 계정 기능은 네이티브 iOS·Android 비개발 출시 빌드에서만
            // 활성화해 QA 성장·테스트 계정이 운영 데이터에 섞이지 않게 한다.
            if (isEditor || isDevelopmentBuild ||
                !hasRequiredRuntimeValues)
                return false;

            return platform == RuntimePlatform.IPhonePlayer ||
                   platform == RuntimePlatform.Android;
        }

        void HandleInitialized(BackendReturnObject bro)
        {
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
            if (suppressAutomaticAuthentication)
                return;
            if (bro == null || !bro.IsSuccess())
            {
                SetLocalReady("서버 연결 없이 게스트로 플레이합니다");
                return;
            }

            BeginBackendTokenLogin();
        }

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
            tokenLoginInFlight = true;
            long capturedGeneration = ++tokenLoginGeneration;
            Backend.BMember.LoginWithTheBackendToken(bro =>
            {
                if (capturedGeneration != tokenLoginGeneration)
                    return;
                tokenLoginInFlight = false;
                HandleTokenLogin(bro);
            });
        }

        public static bool CanBeginTokenLogin(
            bool automaticAuthenticationSuppressed,
            bool explicitRecovery,
            bool loginInFlight) =>
            !loginInFlight &&
            (explicitRecovery || !automaticAuthenticationSuppressed);

        void InvalidateBackendTokenLogin()
        {
            tokenLoginGeneration++;
            tokenLoginInFlight = false;
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
                if (AccountKind == MukJumpAccountKind.LocalGuest)
                    AccountKind = MukJumpAccountKind.BackendGuest;
                CompleteAuthentication("계정 연결 완료");
                return;
            }

            if (profileResolutionPending)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "전환 중인 계정을 확인하지 못했습니다. 다시 시도하거나 로컬 게스트로 돌아가 주세요");
                return;
            }

            Backend.BMember.GuestLogin(guestBro =>
            {
                if (guestBro != null && guestBro.IsSuccess())
                {
                    AccountKind = MukJumpAccountKind.BackendGuest;
                    StoreKind();
                    CompleteAuthentication("게스트 계정 연결 완료");
                }
                else
                    SetLocalReady("오프라인 게스트로 플레이합니다");
            });
        }

        void CompleteAuthentication(string message)
        {
            IsOnlineAuthenticated = true;
            BeginAuthenticatedAccountSession();
            retryDelaySeconds = InitialRetrySeconds;
            StoreKind();
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
            LoadCloudSnapshot();
        }

        void BeginAuthenticatedAccountSession()
        {
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

        void InvalidateAccountScopedOperations(bool clearPendingLeaderboard)
        {
            InvalidateBackendTokenLogin();
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
            return Backend.UserInDate?.Trim() ?? string.Empty;
        }

        bool IsCurrentAccountSession(
            long capturedGeneration,
            string capturedAccountScope)
        {
            return IsSameAccountSession(
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

        void BeginPendingLocalGuestImport()
        {
            PlayerPrefs.SetInt(PendingLocalGuestImportKey, 1);
            PlayerPrefs.DeleteKey(PendingLocalGuestImportOwnerKey);
            PlayerPrefs.DeleteKey(
                PendingLocalGuestImportRestoredOwnerKey);
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
            localLogoutCleanupPending = true;
            localLogoutRemoteConfirmed = false;
            PlayerPrefs.SetInt(PendingLocalLogoutCleanupKey, 1);
            PlayerPrefs.DeleteKey(
                PendingLocalLogoutRemoteConfirmedKey);
            PlayerPrefs.Save();
            return PlayerPrefs.GetInt(
                PendingLocalLogoutCleanupKey,
                0) != 0;
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

        void StorePendingLeaderboardBest(string accountScope, int bestHeight)
        {
            string normalizedScope = accountScope?.Trim() ?? string.Empty;
            if (normalizedScope.Length == 0)
                return;

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
        }

        void ClearPendingLeaderboardSave(bool saveImmediately = true)
        {
            PlayerPrefs.DeleteKey(PendingLeaderboardBestKey);
            PlayerPrefs.DeleteKey(PendingLeaderboardOwnerKey);
            if (saveImmediately)
                PlayerPrefs.Save();
        }

        void LoadCloudSnapshot()
        {
            if (!IsOnlineAuthenticated || settings == null ||
                cloudLoadInFlight || saveInFlight)
                return;

            cloudLoadInFlight = true;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            Backend.GameData.GetMyData(
                settings.PlayerTableName,
                new Where(),
                bro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;
                    cloudLoadInFlight = false;
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
                        string currentAccountScope = CurrentAccountScope();
                        bool guestImportAlreadyRestored =
                            IsPendingLocalGuestImportRestoredForOwner(
                                PlayerPrefs.GetString(
                                    PendingLocalGuestImportRestoredOwnerKey,
                                    string.Empty),
                                currentAccountScope);
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

                    try
                    {
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
                            ClearPendingLocalGuestImport();

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
                            $"[MukJump] 서버 저장을 읽지 못했습니다: {exception.Message}");
                        EnterFatalSyncBlock(
                            "서버 기록을 읽지 못해 로컬 기록을 유지합니다");
                    }
                });
        }

        public static bool ShouldRequireSyncChoice(
            bool hasPendingLocalProfile,
            long localRevision,
            long serverRevision) =>
            hasPendingLocalProfile && localRevision != serverRevision;

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
            rowInDate = pendingServerRowInDate;
            ClearPendingSyncConflict();
            TryApplyCloudSnapshot(
                server,
                keepPendingLocalProfile: false,
                mergeLocalBestHeight: true);
        }

        public void KeepThisDeviceAfterSyncConflict()
        {
            if (!HasPendingSyncConflict)
                return;

            MukJumpCloudSnapshot server = pendingServerSnapshot;
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

            revision = Math.Max(0L, server.revision);
            rowInDate = pendingServerRowInDate;
            ClearPendingSyncConflict();
            PlayerPrefs.DeleteKey(PendingOperationIdKey);
            PersistRevision();
            SetState(
                MukJumpAccountPhase.OnlineReady,
                "이 기기의 기록을 선택했습니다. 서버에 저장하는 중입니다");
            MarkDirty();
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

            int localBest = ScoreManager.Instance != null
                ? ScoreManager.Instance.Best
                : PlayerPrefs.GetInt("MukJump.BestHeight", 0);
            bool shouldMergeBest =
                keepPendingLocalProfile || mergeLocalBestHeight;
            int resolvedBest = shouldMergeBest
                ? MukJumpCloudMergePolicy.MergeBestHeight(
                    localBest,
                    server.bestHeight)
                : server.bestHeight;
            bool growthApplied = true;
            bool bestApplied = false;
            string rollbackGrowthJson = string.Empty;
            suppressDirty = true;
            try
            {
                if (!keepPendingLocalProfile)
                {
                    PermanentGrowthProfile.TryExportCloudJson(
                        out rollbackGrowthJson);
                    growthApplied = PermanentGrowthProfile
                        .TryReplaceFromCloudJson(server.growthJson);
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
                    if (!bestApplied && !keepPendingLocalProfile &&
                        !string.IsNullOrWhiteSpace(rollbackGrowthJson))
                        PermanentGrowthProfile.TryReplaceFromCloudJson(
                            rollbackGrowthJson);

                    if (bestApplied && !keepPendingLocalProfile)
                        LobbySettingsProfile.ApplyCloudSettings(
                            server.bgmVolume,
                            server.sfxVolume,
                            server.tutorialVersion);
                }
            }
            finally
            {
                suppressDirty = false;
            }
            if (!growthApplied)
            {
                EnterFatalSyncBlock(
                    "성장 기록을 안전하게 복원하지 못해 로컬 기록을 유지합니다");
                return false;
            }
            if (!bestApplied)
            {
                EnterFatalSyncBlock(
                    "최고 기록을 안전하게 복원하지 못해 서버 저장을 멈췄습니다");
                return false;
            }

            revision = keepPendingLocalProfile
                ? Math.Max(revision, server.revision)
                : Math.Max(0L, server.revision);
            PersistRevision();
            dirty = keepPendingLocalProfile ||
                    resolvedBest > server.bestHeight;
            if (!dirty)
            {
                PlayerPrefs.DeleteKey(PendingSaveKey);
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
                PlayerPrefs.Save();
            }
            replaceLocalFromServerOnNextLoad = false;
            if (profileResolutionPending)
                ClearPendingProfileResolution();
            ClearPendingSyncConflict();
            SetState(
                MukJumpAccountPhase.OnlineReady,
                dirty ? "기록을 확인해 서버에 저장하는 중" : "동기화 완료");
            if (dirty)
                MarkDirty();
            // 이전 제출 콜백이 앱 종료로 사라졌더라도 서버의
            // 현재 최고 기록으로 리더보드를 다시 보정한다.
            SubmitBestHeight(resolvedBest);
            return true;
        }

        void InsertLocalSnapshot()
        {
            MukJumpCloudSnapshot snapshot = CaptureNextSnapshot();
            long capturedMutationVersion = localMutationVersion;
            if (!snapshot.IsSupported)
            {
                KeepSavePending("로컬 성장 기록을 확인한 뒤 서버 저장을 다시 시도합니다");
                return;
            }

            saveInFlight = true;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            Backend.GameData.Insert(
                settings.PlayerTableName,
                ToParam(snapshot),
                bro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;
                    saveInFlight = false;
                    if (bro != null && bro.IsSuccess())
                    {
                        rowInDate = bro.GetInDate();
                        if (restoreLocalGuestIfServerEmptyOnNextLoad)
                            ClearPendingLocalGuestImport();
                        if (profileResolutionPending)
                            ClearPendingProfileResolution();
                        CompleteSuccessfulSave(
                            snapshot,
                            capturedMutationVersion);
                        SubmitBestHeight(snapshot.bestHeight);
                    }
                    else
                        KeepSavePending("첫 서버 저장을 다시 시도합니다");
                });
        }

        public void SignInWithGoogle()
        {
            if (!CanStartInteractiveLogin())
                return;
            suppressAutomaticAuthentication = false;
            SetState(MukJumpAccountPhase.Connecting, "Google 로그인 중");
#if UNITY_ANDROID && !UNITY_EDITOR
            string webClientId = settings.AndroidGoogleWebClientId;
            if (string.IsNullOrWhiteSpace(webClientId))
            {
                SetState(MukJumpAccountPhase.Error, "Google 클라이언트 ID 설정이 필요합니다");
                return;
            }
            TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin(
                webClientId,
                true,
                (success, message, token) =>
                    HandleFederationToken(
                        success,
                        token,
                        FederationType.Google,
                        MukJumpAccountKind.Google,
                        "Google 로그인에 실패했습니다"));
#elif UNITY_IOS && !UNITY_EDITOR
            TheBackend.ToolKit.GoogleLogin.iOS.GoogleLogin(
                (success, message, token) =>
                    HandleFederationToken(
                        success,
                        token,
                        FederationType.Google,
                        MukJumpAccountKind.Google,
                        "Google 로그인에 실패했습니다"));
#else
            SetState(MukJumpAccountPhase.Error, "Google 로그인은 모바일 빌드에서 확인합니다");
#endif
        }

        public void SignInWithApple()
        {
            if (!CanStartInteractiveLogin())
                return;
            suppressAutomaticAuthentication = false;
            SetState(MukJumpAccountPhase.Connecting, "Apple 로그인 중");
#if UNITY_IOS && !UNITY_EDITOR
            if (appleAuthManager == null)
            {
                SetState(MukJumpAccountPhase.Error, "이 기기에서는 Apple 로그인을 사용할 수 없습니다");
                return;
            }
            appleAuthManager.LoginWithAppleId(
                new AppleAuthLoginArgs(LoginOptions.None),
                credential =>
                {
                    if (credential is not IAppleIDCredential appleCredential)
                    {
                        SetState(MukJumpAccountPhase.Error, "Apple 인증 정보를 확인하지 못했습니다");
                        return;
                    }
                    string identityToken = appleCredential.IdentityToken == null
                        ? string.Empty
                        : System.Text.Encoding.UTF8.GetString(
                            appleCredential.IdentityToken);
                    HandleFederationToken(
                        !string.IsNullOrWhiteSpace(identityToken),
                        identityToken,
                        FederationType.Apple,
                        MukJumpAccountKind.Apple,
                        "Apple 로그인에 실패했습니다");
                },
                error => SetState(
                    MukJumpAccountPhase.Error,
                    "Apple 로그인이 취소되었거나 실패했습니다"));
#elif UNITY_ANDROID && !UNITY_EDITOR
            string serviceId = settings.AndroidAppleServiceId;
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                SetState(MukJumpAccountPhase.Error, "Apple Service ID 설정이 필요합니다");
                return;
            }
            bool opened = TheBackend.ToolKit.AppleLogin.Android.AppleLogin(
                serviceId,
                out string errorMessage,
                true,
                token => HandleFederationToken(
                    !string.IsNullOrWhiteSpace(token),
                    token,
                    FederationType.Apple,
                    MukJumpAccountKind.Apple,
                    "Apple 로그인에 실패했습니다"));
            if (!opened)
                SetState(MukJumpAccountPhase.Error, "Apple 로그인 창을 열지 못했습니다");
#else
            SetState(MukJumpAccountPhase.Error, "Apple 로그인은 모바일 빌드에서 확인합니다");
#endif
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
                BeginPendingLocalGuestImport();
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
            accountKind == MukJumpAccountKind.LocalGuest;

        void UpgradeGuestFederation(
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            string failureMessage,
            int attempt)
        {
            Backend.BMember.ChangeCustomToFederation(token, type, bro =>
            {
                if (bro != null && bro.IsSuccess())
                {
                    AccountKind = kind;
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
                    pendingFederationToken = token;
                    pendingFederationType = type;
                    pendingFederationKind = kind;
                    SetState(
                        MukJumpAccountPhase.NeedsAccountChoice,
                        "이미 사용 중인 계정입니다. 기존 계정으로 전환하면 현재 게스트 기록은 합쳐지지 않습니다");
                    return;
                }

                string message = statusCode switch
                {
                    "403" => "로그인 정보가 만료되었습니다. 다시 로그인해 주세요",
                    "412" => "게스트 계정에서만 계정을 연결할 수 있습니다",
                    _ => failureMessage,
                };
                SetState(MukJumpAccountPhase.Error, message);
            });
        }

        public static bool IsTransientStatusCode(string statusCode) =>
            statusCode == "500" || statusCode == "502" ||
            statusCode == "503";

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
            SetState(MukJumpAccountPhase.OnlineReady, "현재 게스트 기록을 유지합니다");
        }

        void AuthorizeExistingFederation(
            string token,
            FederationType type,
            MukJumpAccountKind kind,
            bool replaceLocalProfile = true,
            bool restoreLocalIfServerEmpty = false)
        {
            string previousAccountScope = CurrentAccountScope();
            // 인증 주체가 바뀌는 동안 이전 계정 콜백이 UI·저장 상태를
            // 되돌리지 못하게 먼저 세대를 끊는다. 보류 순위는 성공 시
            // 계정 초기화에서 지우고, 실패하면 기존 계정 소유자로 유지한다.
            InvalidateAccountScopedOperations(clearPendingLeaderboard: false);
            SetState(MukJumpAccountPhase.Connecting, "기존 계정으로 전환 중");
            Backend.BMember.AuthorizeFederation(token, type, bro =>
            {
                if (bro != null && bro.IsSuccess())
                {
                    ClearPendingFederation();
                    if (!PersistAuthorizedAccountTransition(
                            kind,
                            replaceLocalProfile,
                            restoreLocalIfServerEmpty))
                    {
                        EnterFatalSyncBlock(
                            "계정 전환 상태를 안전하게 저장하지 못했습니다. 로컬 기록은 보존됩니다");
                        return;
                    }
                    if (replaceLocalProfile)
                    {
                        replaceLocalFromServerOnNextLoad = true;
                        restoreLocalGuestIfServerEmptyOnNextLoad =
                            restoreLocalIfServerEmpty;
                        revision = 0L;
                        if (!TryClearAccountProgressForSwitch())
                            Debug.LogWarning(
                                "[MukJump] 계정 전환 중 기기 기록 초기화가 완전히 검증되지 않았습니다. 서버 기록을 다시 불러옵니다.");
                    }
                    rowInDate = string.Empty;
                    CompleteAuthentication("기존 계정으로 전환했습니다");
                }
                else
                {
                    if (restoreLocalIfServerEmpty)
                        ClearPendingLocalGuestImport();
                    RestoreAuthenticatedSessionAfterFailedTransition(
                        previousAccountScope,
                        "기존 계정 로그인에 실패했습니다");
                }
            });
        }

        public void Logout()
        {
            if (!IsOnlineAuthenticated)
                return;
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
                    syncWriteBlocked || BlocksGameplayForAccountSync))
            {
                if (dirty && !operationInFlight)
                    SaveNow();
                SetState(
                    MukJumpAccountPhase.Error,
                    dirty
                        ? "변경한 기록을 저장한 뒤 로그아웃을 다시 눌러 주세요"
                        : "기록 동기화가 끝난 뒤 로그아웃을 다시 눌러 주세요");
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
            Backend.BMember.Logout(bro =>
            {
                if (bro == null || !bro.IsSuccess())
                {
                    IsOnlineAuthenticated = false;
                    SetState(
                        MukJumpAccountPhase.Error,
                        "로그아웃 결과를 확인하지 못했습니다. 네트워크 연결 후 다시 확인해 주세요");
                    return;
                }

                MarkPendingLocalLogoutRemoteConfirmed();
                SignOutFederationAndFinishLocalLogout();
            });
        }

        public void RetryPendingProfileResolution()
        {
            if (!BlocksGameplayForAccountSync)
                return;
            if (accountDeletionCleanupPending)
            {
                if (accountDeletionRemoteConfirmed)
                    CompleteLocalAccountDeletion();
                else if (accountDeletionRequestInFlight)
                    SetStatus("서버 계정 삭제 결과를 확인하고 있습니다");
                else if (IsOnlineAuthenticated)
                    WithdrawBackendAccount(CurrentAccountScope());
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
            if (!BlocksGameplayForAccountSync)
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
            Backend.BMember.Logout(logoutBro =>
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
            });
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
            SetState(
                MukJumpAccountPhase.Connecting,
                "기기 로그인 정보를 정리하고 있습니다");
            if (AccountKind == MukJumpAccountKind.Google)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut(
                    true,
                    (success, message) =>
                    {
                        if (!success)
                            Debug.LogWarning(
                                "[MukJump] Google 기기 로그아웃 실패: " + message);
                        FinishFederationSignOut(capturedGeneration);
                    });
                return;
#elif UNITY_IOS && !UNITY_EDITOR
                TheBackend.ToolKit.GoogleLogin.iOS.GoogleSignOut(
                    (success, message) =>
                    {
                        if (!success)
                            Debug.LogWarning(
                                "[MukJump] Google 기기 로그아웃 실패: " + message);
                        FinishFederationSignOut(capturedGeneration);
                    });
                return;
#endif
            }

            FinishFederationSignOut(capturedGeneration);
        }

        void FinishFederationSignOut(long capturedGeneration)
        {
            if (!localLogoutFinalizationInFlight ||
                capturedGeneration != localLogoutFinalizationGeneration)
                return;
            localLogoutFinalizationInFlight = false;
            CompleteLocalLogout();
        }

        void CompleteLocalLogout()
        {
            localLogoutFinalizationGeneration++;
            localLogoutFinalizationInFlight = false;
            InvalidateBackendTokenLogin();
            suppressAutomaticAuthentication = true;
            bool mustRestorePendingGuest = profileResolutionPending;
            bool hasValidLocalGuestBackup =
                HasValidSavedLocalGuestProfile();
            IsOnlineAuthenticated = false;
            rowInDate = string.Empty;
            revision = 0L;
            syncWriteBlocked = false;
            replaceLocalFromServerOnNextLoad = false;
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
            ClearPendingProfileResolution();
            ClearPendingLocalGuestImport();
            AccountKind = MukJumpAccountKind.LocalGuest;
            StoreKind();
            // 로컬 계정 종류까지 저장된 뒤에만 crash-recovery 표식을
            // 지운다. 두 Save 사이 종료돼도 다음 실행이 정리를 재개한다.
            ClearPendingLocalLogoutCleanup();
            SetLocalReady("로그아웃했습니다. 로컬 게스트로 계속 플레이합니다");
        }

        bool BeginPendingLocalAccountDeletion(string accountScope)
        {
            string normalizedScope = accountScope?.Trim() ?? string.Empty;
            if (normalizedScope.Length == 0)
                return false;

            accountDeletionCleanupPending = true;
            accountDeletionRemoteConfirmed = false;
            PlayerPrefs.SetInt(
                PendingLocalAccountDeletionCleanupKey,
                1);
            PlayerPrefs.SetString(
                PendingLocalAccountDeletionOwnerKey,
                normalizedScope);
            PlayerPrefs.DeleteKey(
                PendingLocalAccountDeletionRemoteConfirmedKey);
            PlayerPrefs.Save();
            return PlayerPrefs.GetInt(
                       PendingLocalAccountDeletionCleanupKey,
                       0) != 0 &&
                   string.Equals(
                       PlayerPrefs.GetString(
                           PendingLocalAccountDeletionOwnerKey,
                           string.Empty),
                       normalizedScope,
                       StringComparison.Ordinal);
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

#if UNITY_IOS && !UNITY_EDITOR
        void ReauthenticateAppleAndDelete(string accountScope)
        {
            if (appleAuthManager == null)
            {
                RestoreAuthenticatedSessionAfterFailedTransition(
                    accountScope,
                    "Apple 계정 확인을 시작할 수 없습니다. 잠시 후 다시 시도해 주세요");
                return;
            }

            appleAuthManager.LoginWithAppleId(
                new AppleAuthLoginArgs(LoginOptions.None),
                credential =>
                {
                    if (credential is not IAppleIDCredential appleCredential ||
                        appleCredential.AuthorizationCode == null)
                    {
                        RestoreAuthenticatedSessionAfterFailedTransition(
                            accountScope,
                            "Apple 계정을 확인하지 못해 삭제를 중단했습니다");
                        return;
                    }

                    string authorizationCode =
                        System.Text.Encoding.UTF8.GetString(
                            appleCredential.AuthorizationCode);
                    if (string.IsNullOrWhiteSpace(authorizationCode))
                    {
                        RestoreAuthenticatedSessionAfterFailedTransition(
                            accountScope,
                            "Apple 인증 코드가 없어 삭제를 중단했습니다");
                        return;
                    }

                    Backend.BMember.RevokeAppleToken(
                        authorizationCode,
                        revokeBro =>
                        {
                            if (revokeBro == null || !revokeBro.IsSuccess())
                            {
                                RestoreAuthenticatedSessionAfterFailedTransition(
                                    accountScope,
                                    "Apple 연결 해제에 실패해 계정 삭제를 중단했습니다");
                                return;
                            }
                            WithdrawBackendAccount(accountScope);
                        });
                },
                error => RestoreAuthenticatedSessionAfterFailedTransition(
                    accountScope,
                    "Apple 계정 확인이 취소되어 삭제하지 않았습니다"));
        }
#endif

        void WithdrawBackendAccount(string accountScope)
        {
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
            SetState(MukJumpAccountPhase.Deleting, "계정을 삭제하는 중");
            Backend.BMember.WithdrawAccount(bro =>
            {
                if (!accountDeletionRequestInFlight ||
                    capturedGeneration != accountDeletionRequestGeneration)
                    return;
                accountDeletionRequestInFlight = false;
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
            });
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

        void CompleteLocalAccountDeletion()
        {
            if (!accountDeletionCleanupPending ||
                !accountDeletionRemoteConfirmed)
                return;

            accountDeletionRequestGeneration++;
            accountDeletionRequestInFlight = false;
            suppressAutomaticAuthentication = true;
            bool backendLocalAccountCleared = true;
            try
            {
                // WithdrawAccount 성공 시 서버 토큰은 이미 폐기된다. SDK가
                // 보관한 게스트 인증 정보도 지워 다음 실행의 자동 재가입을 막는다.
                Backend.BMember.DeleteGuestInfo();
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
            }
            finally
            {
                suppressDirty = false;
            }

            bool localCleared =
                backendLocalAccountCleared &&
                scoreCleared &&
                growthCleared &&
                settingsCleared;
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
            ClearPendingFederation();
            leaderboardEntries.Clear();
            LeaderboardLoading = false;
            LeaderboardStatus =
                "계정 연결 후 최고 고도 순위를 확인할 수 있습니다";
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
            PlayerPrefs.DeleteKey(PendingLocalLogoutCleanupKey);
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
            PlayerPrefs.DeleteKey(PendingLeaderboardBestKey);
            PlayerPrefs.DeleteKey(PendingLeaderboardOwnerKey);
            PlayerPrefs.Save();

            accountDeletionCleanupPending = false;
            accountDeletionRemoteConfirmed = false;
            SetLocalReady("계정과 연결된 서버·기기 데이터를 삭제했습니다");
        }

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
            InvalidateAccountScopedOperations(clearPendingLeaderboard: true);
            suppressDirty = true;
            try
            {
                bool scoreCleared = ScoreManager
                    .TryReplaceVerifiedBestForAccountSwitch(0);
                bool growthCleared =
                    PermanentGrowthProfile.TryClearForAccountDeletion();
                LobbySettingsProfile.ApplyCloudSettings(1f, 1f, 0);
                dirty = false;
                localMutationVersion++;
                PlayerPrefs.DeleteKey(PendingSaveKey);
                PlayerPrefs.DeleteKey(PendingOperationIdKey);
                PlayerPrefs.DeleteKey(RevisionKey);
                PlayerPrefs.Save();
                return scoreCleared && growthCleared;
            }
            finally
            {
                suppressDirty = false;
            }
        }

        bool RestoreSavedLocalGuestProfile()
        {
            string json = PlayerPrefs.GetString(
                LocalGuestSnapshotKey,
                string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            bool restored = false;
            try
            {
                MukJumpCloudSnapshot snapshot =
                    JsonUtility.FromJson<MukJumpCloudSnapshot>(json);
                if (snapshot == null || !snapshot.IsSupported ||
                    !PermanentGrowthProfile.IsSupportedCloudJson(
                        snapshot.growthJson))
                    return false;

                suppressDirty = true;
                PermanentGrowthProfile.TryExportCloudJson(
                    out string rollbackGrowthJson);
                bool growthRestored = PermanentGrowthProfile
                    .TryReplaceFromCloudJson(snapshot.growthJson);
                bool bestRestored = growthRestored && ScoreManager
                    .TryReplaceVerifiedBestForAccountSwitch(
                        snapshot.bestHeight);
                if (growthRestored && !bestRestored &&
                    !string.IsNullOrWhiteSpace(rollbackGrowthJson))
                    PermanentGrowthProfile.TryReplaceFromCloudJson(
                        rollbackGrowthJson);
                restored = growthRestored && bestRestored;
                if (restored)
                {
                    LobbySettingsProfile.ApplyCloudSettings(
                        snapshot.bgmVolume,
                        snapshot.sfxVolume,
                        snapshot.tutorialVersion);
                    dirty = true;
                    localMutationVersion++;
                    PlayerPrefs.SetInt(PendingSaveKey, 1);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MukJump] 로컬 게스트 기록을 복원하지 못했습니다: " +
                    exception.Message);
            }
            finally
            {
                suppressDirty = false;
                PlayerPrefs.Save();
            }
            return restored;
        }

        public void NotifyRunSettled(GameOverResult result)
        {
            if (result.PersistenceState != GameOverPersistenceState.Complete ||
                !result.RecordsAllowedForResult())
                return;
            MarkDirty();
        }

        public void SubmitBestHeight(int bestHeight)
        {
            if (!IsOnlineAuthenticated ||
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
            StorePendingLeaderboardBest(accountScope, normalizedBest);
            if (leaderboardSaveInFlight)
                return;

            leaderboardSaveInFlight = true;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = accountScope;
            var param = new Param();
            param.Add(settings.BestHeightColumn, normalizedBest);
            Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(
                settings.AllTimeRankUuid,
                settings.PlayerTableName,
                rowInDate,
                param,
                bro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;
                    leaderboardSaveInFlight = false;
                    if (bro == null || !bro.IsSuccess())
                    {
                        StorePendingLeaderboardBest(
                            capturedAccountScope,
                            normalizedBest);
                        leaderboardRetryAtRealtime =
                            Time.realtimeSinceStartup + InitialRetrySeconds;
                        SetStatus("기록 순위 등록은 연결 복구 후 다시 시도합니다");
                    }
                    else
                    {
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
                            ClearPendingLeaderboardSave(saveImmediately: false);
                        leaderboardRetryAtRealtime =
                            Time.realtimeSinceStartup + InitialRetrySeconds;
                        PlayerPrefs.Save();
                    }
                });
        }

        /// 개인정보가 되는 공개 닉네임 없이 순위와 고도만 보여 주는 1.0용
        /// 전체 TOP 10 조회다.
        public void RefreshLeaderboard()
        {
            if (LeaderboardLoading)
                return;
            if (Phase == MukJumpAccountPhase.Connecting ||
                Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                Phase == MukJumpAccountPhase.Deleting)
                return;
            if (!IsOnlineAuthenticated || settings == null ||
                string.IsNullOrWhiteSpace(settings.AllTimeRankUuid))
            {
                LeaderboardStatus =
                    "게스트 서버 연결과 리더보드 설정이 완료되면 표시됩니다";
                StateChanged?.Invoke();
                return;
            }

            LeaderboardLoading = true;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            LeaderboardStatus = "최고 고도 순위를 불러오는 중";
            StateChanged?.Invoke();
            Backend.Leaderboard.User.GetLeaderboard(
                settings.AllTimeRankUuid,
                10,
                bro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;
                    LeaderboardLoading = false;
                    if (bro == null || !bro.IsSuccess())
                    {
                        LeaderboardStatus =
                            "순위를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요";
                        StateChanged?.Invoke();
                        return;
                    }

                    leaderboardEntries.Clear();
                    List<BackEnd.Leaderboard.UserLeaderboardItem> items =
                        bro.GetUserLeaderboardList();
                    if (items != null)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            BackEnd.Leaderboard.UserLeaderboardItem item =
                                items[i];
                            int rank = int.TryParse(item.rank, out int parsedRank)
                                ? parsedRank
                                : i + 1;
                            int height = int.TryParse(
                                item.score,
                                out int parsedHeight)
                                ? parsedHeight
                                : 0;
                            leaderboardEntries.Add(
                                new MukJumpLeaderboardEntry(rank, height));
                        }
                    }

                    LeaderboardStatus = leaderboardEntries.Count == 0
                        ? "아직 등록된 최고 고도 기록이 없습니다"
                        : "전체 최고 고도 TOP 10";
                    StateChanged?.Invoke();
                });
        }

        void MarkDirty()
        {
            if (suppressDirty)
                return;
            bool pendingMarkerNeedsFlush = ShouldFlushPendingSaveMarker(
                dirty,
                PlayerPrefs.HasKey(PendingSaveKey));
            localMutationVersion++;
            dirty = true;
            saveAtRealtime = Time.realtimeSinceStartup + SaveDebounceSeconds;
            PlayerPrefs.SetInt(PendingSaveKey, 1);
            if (pendingMarkerNeedsFlush)
                PlayerPrefs.Save();
        }

        public static bool ShouldFlushPendingSaveMarker(
            bool wasAlreadyDirty,
            bool pendingKeyExists) =>
            !wasAlreadyDirty || !pendingKeyExists;

        public void SaveNow()
        {
            if (!dirty || !IsOnlineAuthenticated || saveInFlight ||
                syncWriteBlocked ||
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

            saveInFlight = true;
            MukJumpCloudSnapshot snapshot = CaptureNextSnapshot();
            long capturedMutationVersion = localMutationVersion;
            long capturedSessionGeneration = accountSessionGeneration;
            string capturedAccountScope = CurrentAccountScope();
            if (!snapshot.IsSupported)
            {
                saveInFlight = false;
                KeepSavePending("로컬 성장 기록을 확인한 뒤 서버 저장을 다시 시도합니다");
                return;
            }
            Backend.GameData.GetMyData(
                settings.PlayerTableName,
                new Where(),
                loadBro =>
                {
                    if (!IsCurrentAccountSession(
                            capturedSessionGeneration,
                            capturedAccountScope))
                        return;

                    if (loadBro == null || !loadBro.IsSuccess())
                    {
                        saveInFlight = false;
                        KeepSavePending("저장 전 서버 기록 확인을 다시 시도합니다");
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
                    string verifiedRowInDate = ReadString(row, "inDate");
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
                        capturedAccountScope);
                });
        }

        void UpdateVerifiedSnapshot(
            MukJumpCloudSnapshot snapshot,
            long capturedMutationVersion,
            long capturedSessionGeneration,
            string capturedAccountScope)
        {
            var revisionWhere = new Where();
            revisionWhere.Equal("inDate", rowInDate);
            revisionWhere.Equal("revision", revision);
            Backend.GameData.Update(
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
                    if (bro != null && bro.IsSuccess())
                    {
                        CompleteSuccessfulSave(
                            snapshot,
                            capturedMutationVersion);
                        SubmitBestHeight(snapshot.bestHeight);
                    }
                    else
                    {
                        string statusCode = bro?.GetStatusCode() ?? string.Empty;
                        if (statusCode == "404" || statusCode == "409")
                        {
                            // revision 조건이 더 이상 맞지 않으면 최신 행을 다시
                            // 읽어 사용자가 서버/기기 기록을 직접 고르게 한다.
                            LoadCloudSnapshot();
                        }
                        else
                            KeepSavePending("서버 저장을 다시 시도합니다");
                    }
                });
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
            SetState(
                MukJumpAccountPhase.OnlineReady,
                dirty ? "새 변경 사항을 이어서 저장하는 중" : "동기화 완료");
        }

        public static bool ShouldKeepDirtyAfterSave(
            long capturedMutationVersion,
            long currentMutationVersion) =>
            currentMutationVersion != capturedMutationVersion;

        void KeepSavePending(string message)
        {
            dirty = true;
            saveAtRealtime = Time.realtimeSinceStartup + retryDelaySeconds;
            retryDelaySeconds = CalculateNextRetryDelay(retryDelaySeconds);
            PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.Save();
            SetStatus(message);
        }

        void EnterFatalSyncBlock(string message)
        {
            syncWriteBlocked = true;
            dirty = true;
            PlayerPrefs.SetInt(PendingSaveKey, 1);
            PlayerPrefs.Save();
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
            if (!CanUseBackend() || !Backend.IsInitialized)
            {
                SetState(
                    MukJumpAccountPhase.Error,
                    "서버 연결을 확인할 수 없습니다. 네트워크를 확인한 뒤 다시 시도해 주세요");
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
            bool saveBlocked = false) =>
            operationInFlight || (dirty && !saveBlocked);

        void SetLocalReady(string message)
        {
            IsOnlineAuthenticated = false;
            AccountKind = MukJumpAccountKind.LocalGuest;
            SetState(MukJumpAccountPhase.LocalReady, message);
        }

        void SetState(MukJumpAccountPhase phase, string message)
        {
            Phase = phase;
            StatusMessage = message ?? string.Empty;
            StateChanged?.Invoke();
        }

        void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            StateChanged?.Invoke();
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
                ? Mathf.Clamp01(value)
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
using UnityEngine;

namespace MukJump.Core
{
    /// Apps in Toss에서는 토스의 게스트 식별과 게임센터를 사용한다.
    /// 모바일 전용 뒤끝 SDK를 WebGL 번들에서 완전히 제외하기 위한 경량 대체 구현이다.
    [DisallowMultipleComponent]
    public sealed class MukJumpAccountRuntime : MonoBehaviour
    {
        static readonly IReadOnlyList<MukJumpLeaderboardEntry> EmptyEntries =
            Array.Empty<MukJumpLeaderboardEntry>();

        public static MukJumpAccountRuntime Instance { get; private set; }
        public MukJumpAccountKind AccountKind => MukJumpAccountKind.LocalGuest;
        public MukJumpAccountPhase Phase => MukJumpAccountPhase.LocalReady;
        public bool IsOnlineAuthenticated => false;
        public string StatusMessage =>
            "Apps in Toss에서는 토스 계정으로 기록을 관리합니다";
        public bool HasPendingAccountConflict => false;
        public bool HasPendingSyncConflict => false;
        public bool HasPendingAccountDeletionCleanup => false;
        public bool BlocksGameplayForAccountSync => false;
        public IReadOnlyList<MukJumpLeaderboardEntry> LeaderboardEntries =>
            EmptyEntries;
        public string LeaderboardStatus =>
            "토스 게임센터에서 최고 고도 순위를 확인할 수 있습니다";
        public bool LeaderboardLoading => false;

        public event Action StateChanged;

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
            StateChanged?.Invoke();
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SignInWithGoogle() { }
        public void SignInWithApple() { }
        public void UseExistingAccountAfterConflict() { }
        public void KeepCurrentGuestAfterConflict() { }
        public void UseServerAfterSyncConflict() { }
        public void KeepThisDeviceAfterSyncConflict() { }
        public void Logout() { }
        public void RetryPendingProfileResolution() { }
        public void ReturnToLocalGuestDuringAccountSync() { }
        public void DeleteAccountConfirmed() { }
        public void NotifyRunSettled(GameOverResult result) { }
        public void SubmitBestHeight(int bestHeight) { }
        public void RefreshLeaderboard() =>
            AppsInTossGameCenterRuntime.OpenLeaderboard();
        public void SaveNow() { }
    }
}
#endif
