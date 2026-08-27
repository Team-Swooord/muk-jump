using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class MukJumpAccountTests
    {
        const string ValidGrowthJson =
            "{\"schemaVersion\":1,\"balanceVersion\":7," +
            "\"wallet\":0,\"spent\":0," +
            "\"tutorialRewardClaimed\":false," +
            "\"rewardMilestoneWatermarkInitialized\":true," +
            "\"rewardedBestHeight\":0," +
            "\"cumulativeDistanceMeters\":0," +
            "\"claimedDistanceRewardCount\":0," +
            "\"lastSettledRunId\":\"\"," +
            "\"settledRunIds\":[],\"ranks\":[]," +
            "\"ownedNodeIds\":[]," +
            "\"survivalKeystoneId\":\"\"," +
            "\"leapKeystoneId\":\"\"," +
            "\"inkHandlingKeystoneId\":\"\"}";

        GameObject host;
        MemoryPermanentGrowthStore growthStore;
        MukJumpAccountRuntime activeAccountRuntime;
        GameManager activeGameManager;

        [SetUp]
        public void SetUp()
        {
            growthStore = new MemoryPermanentGrowthStore
            {
                Json = ValidGrowthJson,
                BackupJson = ValidGrowthJson,
            };
            PermanentGrowthProfile.UseStoreForTests(growthStore);
        }

        [TearDown]
        public void TearDown()
        {
            if (activeGameManager != null)
            {
                InvokeLifecycle(activeGameManager, "OnDisable");
                activeGameManager = null;
            }
            if (activeAccountRuntime != null)
            {
                InvokeLifecycle(activeAccountRuntime, "OnDisable");
                activeAccountRuntime = null;
            }
            if (host != null)
                Object.DestroyImmediate(host);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [TestCase(12, 8, 12)]
        [TestCase(8, 12, 12)]
        [TestCase(-5, -1, 0)]
        public void BestHeightMergeNeverLowersOrCreatesNegativeValue(
            int local,
            int server,
            int expected)
        {
            Assert.That(
                MukJumpCloudMergePolicy.MergeBestHeight(local, server),
                Is.EqualTo(expected));
        }

        [Test]
        public void UnsupportedCloudSnapshotCannotReplaceHealthyGrowth()
        {
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out string before),
                Is.True);

            Assert.That(
                PermanentGrowthProfile.TryReplaceFromCloudJson(
                    "{\"schemaVersion\":999}"),
                Is.False);
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out string after),
                Is.True);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void SupportedServerGrowthIsSavedAsARecoverableGeneration()
        {
            Assert.That(
                PermanentGrowthProfile.TryReplaceFromCloudJson(
                    ValidGrowthJson),
                Is.True);
            Assert.That(growthStore.Json, Does.Contain("\"balanceVersion\":7"));
            Assert.That(growthStore.BackupJson, Is.EqualTo(growthStore.Json));
        }

        [Test]
        public void VerifiedBestMergePersistsOnlyTheHigherRecord()
        {
            var scoreStore = new MemoryScoreStore { Best = 15 };
            ScoreManager.UseStoreForTests(scoreStore);
            host = new GameObject("ScoreAccountMergeTest");
            var score = host.AddComponent<ScoreManager>();

            Assert.That(score.TryMergeVerifiedBest(9), Is.True);
            Assert.That(score.Best, Is.EqualTo(15));
            Assert.That(score.TryMergeVerifiedBest(21), Is.True);
            Assert.That(score.Best, Is.EqualTo(21));
            Assert.That(scoreStore.Best, Is.EqualTo(21));
        }

        [Test]
        public void SnapshotRejectsMissingGrowthPayload()
        {
            var snapshot = new MukJumpCloudSnapshot
            {
                schemaVersion = MukJumpCloudSnapshot.CurrentSchemaVersion,
                bestHeight = 20,
                revision = 1,
                growthJson = string.Empty,
            };

            Assert.That(snapshot.IsSupported, Is.False);
            Assert.That(
                MukJumpCloudMergePolicy.ShouldAcceptServerGrowth(snapshot),
                Is.False);
        }

        [Test]
        public void VerifiedBestCanMergeBeforeSceneScoreManagerExists()
        {
            var scoreStore = new MemoryScoreStore { Best = 14 };
            ScoreManager.UseStoreForTests(scoreStore);

            Assert.That(
                ScoreManager.TryMergeVerifiedBestIntoStore(31),
                Is.True);
            Assert.That(scoreStore.Best, Is.EqualTo(31));
            Assert.That(
                ScoreManager.TryMergeVerifiedBestIntoStore(8),
                Is.True);
            Assert.That(scoreStore.Best, Is.EqualTo(31));
        }

        [Test]
        public void AccountSwitchReplacesInsteadOfMergingPreviousUsersBest()
        {
            var scoreStore = new MemoryScoreStore { Best = 88 };
            ScoreManager.UseStoreForTests(scoreStore);

            Assert.That(
                ScoreManager.TryReplaceVerifiedBestForAccountSwitch(12),
                Is.True);
            Assert.That(scoreStore.Best, Is.EqualTo(12));
        }

        [Test]
        public void AccountDeletionClearsGrowthRecoveryCopiesAndBest()
        {
            growthStore.SaveQuarantine("old-primary");
            growthStore.SaveBackupQuarantine("old-backup");
            var scoreStore = new MemoryScoreStore { Best = 99 };
            ScoreManager.UseStoreForTests(scoreStore);

            Assert.That(
                PermanentGrowthProfile.TryClearForAccountDeletion(),
                Is.True);
            Assert.That(
                ScoreManager.TryClearForAccountDeletion(),
                Is.True);

            Assert.That(growthStore.Json, Does.Contain("\"wallet\":0"));
            Assert.That(growthStore.BackupJson, Is.EqualTo(growthStore.Json));
            Assert.That(growthStore.QuarantineJson, Is.Empty);
            Assert.That(growthStore.BackupQuarantineJson, Is.Empty);
            Assert.That(scoreStore.Best, Is.Zero);
        }

        [Test]
        public void AccountDeletionResetsSettingsAndRotatesLocalIdentity()
        {
            var settingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settingsStore);
            string previousUid = LobbySettingsProfile.PlayerUid;
            LobbySettingsProfile.SetBgmVolume(0.25f);
            LobbySettingsProfile.SetSfxVolume(0.4f);
            LobbySettingsProfile.SetHapticsEnabled(false);
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Assert.That(
                LobbySettingsProfile.TryMarkGameplayTutorialCompleted(),
                Is.True);

            Assert.That(
                LobbySettingsProfile.TryResetForAccountDeletion(),
                Is.True);
            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(1f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(1f));
            Assert.That(LobbySettingsProfile.HapticsEnabled, Is.True);
            Assert.That(LobbySettingsProfile.ReducedMotionEnabled, Is.False);
            Assert.That(LobbySettingsProfile.GameplayTutorialVersion, Is.Zero);
            Assert.That(LobbySettingsProfile.PlayerUid, Is.Not.EqualTo(previousUid));
        }

        [TestCase(5f, 10f)]
        [TestCase(10f, 20f)]
        [TestCase(40f, 60f)]
        [TestCase(60f, 60f)]
        public void CloudRetryUsesBoundedExponentialBackoff(
            float current,
            float expected)
        {
            Assert.That(
                MukJumpAccountRuntime.CalculateNextRetryDelay(current),
                Is.EqualTo(expected));
        }

        [TestCase(3L, 3L, false)]
        [TestCase(3L, 4L, true)]
        [TestCase(8L, 2L, true)]
        public void SaveCompletionKeepsChangesMadeWhileRequestWasInFlight(
            long captured,
            long current,
            bool expectedDirty)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldKeepDirtyAfterSave(
                    captured,
                    current),
                Is.EqualTo(expectedDirty));
        }

        [TestCase("500", true)]
        [TestCase("502", true)]
        [TestCase("503", true)]
        [TestCase("409", false)]
        [TestCase("403", false)]
        public void OnlyTransientFederationFailuresAreRetried(
            string statusCode,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsTransientStatusCode(statusCode),
                Is.EqualTo(expected));
        }

        [TestCase("400", "accessTokenError", "accessToken not exist", true)]
        [TestCase("400", "UndefinedParameterException", "undefined refresh_token", true)]
        [TestCase("410", "GoneResourceException", "expired refreshToken", true)]
        [TestCase("401", "BadUnauthorizedException", "bad refreshToken", true)]
        [TestCase("401", "BadUnauthorizedException", "maintenance", false)]
        [TestCase("403", "Forbidden", "too many requests", false)]
        [TestCase("502", "BadGateway", "temporary", false)]
        [TestCase("408", "Timeout", "timeout", false)]
        public void PendingLogoutOnlyCompletesForDefinitivelyUnavailableToken(
            string statusCode,
            string errorCode,
            string message,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsDefinitiveTokenUnavailable(
                    statusCode,
                    errorCode,
                    message),
                Is.EqualTo(expected));
        }

        [Test]
        public void PendingCloudOperationIdIsReusedUntilSaveCompletes()
        {
            const string operationId =
                "0123456789abcdef0123456789abcdef";

            Assert.That(
                MukJumpAccountRuntime.ResolvePendingOperationId(
                    "  " + operationId + "  "),
                Is.EqualTo(operationId));
            Assert.That(
                MukJumpAccountRuntime.ResolvePendingOperationId(string.Empty),
                Does.Match("^[0-9a-f]{32}$"));
        }

        [TestCase(RuntimePlatform.IPhonePlayer, true)]
        [TestCase(RuntimePlatform.Android, false)]
        [TestCase(RuntimePlatform.WebGLPlayer, false)]
        [TestCase(RuntimePlatform.OSXEditor, true)]
        public void AppleLoginIsOnlyOfferedForIosAndEditorVerification(
            RuntimePlatform platform,
            bool expected)
        {
            Assert.That(
                LobbyOptionsView.ShouldOfferAppleSignIn(platform),
                Is.EqualTo(expected));
        }

        [TestCase(false, MukJumpAccountKind.LocalGuest, false, true)]
        [TestCase(true, MukJumpAccountKind.BackendGuest, false, true)]
        [TestCase(true, MukJumpAccountKind.Google, false, false)]
        [TestCase(true, MukJumpAccountKind.Apple, false, false)]
        [TestCase(false, MukJumpAccountKind.LocalGuest, true, false)]
        public void SocialLoginOnlyStartsFromLocalOrBackendGuest(
            bool online,
            MukJumpAccountKind kind,
            bool busy,
            bool expected)
        {
            Assert.That(
                LobbyOptionsView.CanStartSocialLogin(online, kind, busy),
                Is.EqualTo(expected));
        }

        [TestCase(RuntimePlatform.IPhonePlayer, false, true, false, true)]
        [TestCase(RuntimePlatform.Android, false, true, false, true)]
        [TestCase(RuntimePlatform.IPhonePlayer, false, true, true, false)]
        [TestCase(RuntimePlatform.Android, false, true, true, false)]
        [TestCase(RuntimePlatform.WebGLPlayer, false, true, false, false)]
        [TestCase(RuntimePlatform.OSXEditor, true, true, false, false)]
        [TestCase(RuntimePlatform.IPhonePlayer, false, false, false, false)]
        public void BackendOnlyStartsInConfiguredNativeReleaseRuntime(
            RuntimePlatform platform,
            bool isEditor,
            bool hasSettings,
            bool isDevelopmentBuild,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldEnableBackendForRuntime(
                    platform,
                    isEditor,
                    hasSettings,
                    isDevelopmentBuild),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void FirstPendingCloudMutationFlushesCrashRecoveryMarker(
            bool wasAlreadyDirty,
            bool pendingKeyExists,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldFlushPendingSaveMarker(
                    wasAlreadyDirty,
                    pendingKeyExists),
                Is.EqualTo(expected));
        }

        [TestCase("account-a", "account-a", true)]
        [TestCase(" account-a ", "account-a", true)]
        [TestCase("account-a", "account-b", false)]
        [TestCase("", "account-a", false)]
        public void PendingLeaderboardRetryBelongsToOneBackendAccount(
            string storedOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsPendingLeaderboardOwnedBy(
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(4L, 4L, "account-a", "account-a", true, true)]
        [TestCase(4L, 5L, "account-a", "account-a", true, false)]
        [TestCase(4L, 4L, "account-a", "account-b", true, false)]
        [TestCase(4L, 4L, "account-a", "account-a", false, false)]
        public void AsyncCloudCallbackCannotCrossAccountSession(
            long capturedGeneration,
            long currentGeneration,
            string capturedOwner,
            string currentOwner,
            bool authenticated,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsSameAccountSession(
                    capturedGeneration,
                    currentGeneration,
                    capturedOwner,
                    currentOwner,
                    authenticated),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, false, false, false)]
        [TestCase(true, false, false, false, true)]
        [TestCase(false, true, false, false, true)]
        [TestCase(false, false, true, false, true)]
        [TestCase(false, false, false, true, true)]
        public void AccountSwitchWaitsForEveryScopedOperation(
            bool cloudLoad,
            bool cloudSave,
            bool leaderboardSave,
            bool leaderboardLoad,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.HasAccountOperationInFlight(
                    cloudLoad,
                    cloudSave,
                    leaderboardSave,
                    leaderboardLoad),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void LogoutWaitsForDirtyCloudProfile(
            bool dirty,
            bool operationInFlight,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldWaitBeforeLogout(
                    dirty,
                    operationInFlight),
                Is.EqualTo(expected));
        }

        [TestCase(true, false, true, false)]
        [TestCase(true, true, true, true)]
        [TestCase(false, false, true, false)]
        public void FatalSyncBlockDoesNotTrapLogout(
            bool dirty,
            bool operationInFlight,
            bool saveBlocked,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldWaitBeforeLogout(
                    dirty,
                    operationInFlight,
                    saveBlocked),
                Is.EqualTo(expected));
        }

        [TestCase(true, 4L, 5L, true)]
        [TestCase(true, 5L, 5L, false)]
        [TestCase(false, 4L, 5L, false)]
        public void DifferentServerGenerationRequiresExplicitChoice(
            bool hasPendingLocalProfile,
            long localRevision,
            long serverRevision,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldRequireSyncChoice(
                    hasPendingLocalProfile,
                    localRevision,
                    serverRevision),
                Is.EqualTo(expected));
        }

        [TestCase(false, MukJumpAccountKind.LocalGuest, true)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, false)]
        [TestCase(false, MukJumpAccountKind.BackendGuest, false)]
        public void OfflineLocalGuestIsBackedUpBeforeSocialAuthorization(
            bool online,
            MukJumpAccountKind kind,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldPreserveLocalGuestBeforeFederation(
                    online,
                    kind),
                Is.EqualTo(expected));
        }

        [TestCase(true, "account-a", "account-a", true)]
        [TestCase(true, " account-a ", "account-a", true)]
        [TestCase(true, "account-a", "account-b", false)]
        [TestCase(true, "", "account-a", false)]
        [TestCase(false, "account-a", "account-a", false)]
        public void PendingGuestImportIsScopedToAuthenticatedAccount(
            bool pending,
            string storedOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsPendingLocalGuestImportOwnedBy(
                    pending,
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase("account-a", "account-a", true)]
        [TestCase(" account-a ", "account-a", true)]
        [TestCase("account-a", "account-b", false)]
        [TestCase("", "account-a", false)]
        public void RestoredGuestImportMarkerBelongsToOneAccount(
            string restoredOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .IsPendingLocalGuestImportRestoredForOwner(
                        restoredOwner,
                        currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void GuestBackupIsRestoredOnlyOnceBeforeInsertSucceeds(
            bool pendingForAccount,
            bool alreadyRestored,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldRestorePendingLocalGuestImport(
                    pendingForAccount,
                    alreadyRestored),
                Is.EqualTo(expected));
        }

        [TestCase("account-a", "account-a", true)]
        [TestCase("account-a", "account-b", false)]
        [TestCase("", "account-a", false)]
        public void PendingProfileResolutionCannotCrossAccounts(
            string storedOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsPendingProfileResolutionOwnedBy(
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(true, "account-a", "account-a", true)]
        [TestCase(true, " account-a ", "account-a", true)]
        [TestCase(true, "account-a", "account-b", false)]
        [TestCase(false, "account-a", "account-a", false)]
        public void PendingAccountDeletionCannotCrossAccounts(
            bool pending,
            string storedOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.IsPendingAccountDeletionOwnedBy(
                    pending,
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, false)]
        [TestCase(true, true, false, true)]
        [TestCase(false, true, true, false)]
        public void ExplicitLogoutRecoveryCanBypassAutomaticLoginSuppression(
            bool automaticLoginSuppressed,
            bool explicitRecovery,
            bool loginInFlight,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.CanBeginTokenLogin(
                    automaticLoginSuppressed,
                    explicitRecovery,
                    loginInFlight),
                Is.EqualTo(expected));
        }

        [Test]
        public void PendingAccountProfileResolutionBlocksGameStart()
        {
            host = new GameObject("PendingAccountResolutionTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetField(
                    "profileResolutionPending",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, true);
            ActivateAccountRuntime(account);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            Assert.That(
                MukJumpAccountRuntime.Instance,
                Is.SameAs(account),
                "테스트 계정 런타임이 활성 싱글톤이어야 한다.");
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var growthObject = new GameObject("PendingAccountGrowthView");
            growthObject.transform.SetParent(host.transform, false);
            var growthView = growthObject.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(growthView.IsOpen, Is.False);
        }

        [TestCase("localLogoutCleanupPending")]
        [TestCase("accountDeletionCleanupPending")]
        public void PendingAccountCleanupBlocksGameStartAndGrowth(
            string pendingField)
        {
            host = new GameObject("PendingAccountCleanupTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetField(
                    pendingField,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, true);
            ActivateAccountRuntime(account);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            Assert.That(
                MukJumpAccountRuntime.Instance,
                Is.SameAs(account),
                "테스트 계정 런타임이 활성 싱글톤이어야 한다.");
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var growthObject = new GameObject("PendingCleanupGrowthView");
            growthObject.transform.SetParent(host.transform, false);
            var growthView = growthObject.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(growthView.IsOpen, Is.False);
        }

        void ActivateAccountRuntime(MukJumpAccountRuntime account)
        {
            activeAccountRuntime = account;
            // EditMode에서는 일반 MonoBehaviour의 OnEnable이 자동 호출되지
            // 않으므로, 실제 런타임과 같은 싱글톤 상태를 명시적으로 만든다.
            InvokeLifecycle(account, "OnEnable");
        }

        void ActivateGameManager(GameManager manager)
        {
            activeGameManager = manager;
            InvokeLifecycle(manager, "OnEnable");
        }

        static void InvokeLifecycle(
            MonoBehaviour component,
            string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                $"{component.GetType().Name}.{methodName}을 찾을 수 없다.");
            method.Invoke(component, null);
        }

        [Test]
        public void CloudGrowthRejectsUnfundedDistanceCurrency()
        {
            long firstRewardDistance =
                RunRewardCalculator.GetNextRewardDistance(0);
            string forged = ValidGrowthJson
                .Replace(
                    "\"cumulativeDistanceMeters\":0",
                    $"\"cumulativeDistanceMeters\":{firstRewardDistance}")
                .Replace(
                    "\"claimedDistanceRewardCount\":0",
                    "\"claimedDistanceRewardCount\":1");

            Assert.That(
                PermanentGrowthProfile.TryReplaceFromCloudJson(forged),
                Is.False);
        }
    }
}
