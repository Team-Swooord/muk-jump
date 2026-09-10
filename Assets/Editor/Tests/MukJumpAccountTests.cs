using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class MukJumpAccountTests
    {
        [Test]
        public void CurrentReleaseDoesNotOfferGoogleSignIn()
        {
            Assert.That(MukJumpAccountRuntime.GoogleSignInEnabled, Is.False);
        }

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
        readonly System.Collections.Generic.Dictionary<string, (bool existed, int value)> deletionPrefs = new();
        readonly System.Collections.Generic.Dictionary<string, (bool existed, string value)> deletionStringPrefs = new();
        const string DeletionOwnerKey = "MukJump.Account.PendingLocalAccountDeletionOwner";

        [SetUp]
        public void SetUp()
        {
            // 삭제 복구 테스트는 실제 개발자 계정 표식과 다른 테스트에 영향을 주지 않는다.
            deletionPrefs.Clear();
            foreach (string key in new[] {
                "MukJump.Account.PendingLocalAccountDeletionCleanup",
                "MukJump.Account.PendingLocalAccountDeletionRemoteConfirmed",
                "MukJump.Account.PendingLocalAccountDeletionFederationCleared",
                "MukJump.Cloud.PendingLeaderboardBest",
                "MukJump.Cloud.PendingSave",
                "MukJump.Account.PendingLocalLogoutCleanup",
                "MukJump.Account.PendingLocalLogoutRemoteConfirmed",
                "MukJump.Account.Kind",
                "MukJump.Account.AutomaticAuthenticationSuppressed",
                "MukJump.Account.LegacyGuestCredentialCleanup",
                AppleDeletionMarkerKey })
            {
                deletionPrefs.Add(key, (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0)));
                PlayerPrefs.DeleteKey(key);
            }
            deletionStringPrefs.Clear();
            foreach (string key in new[] { DeletionOwnerKey, "MukJump.Account.LastAuthenticatedOwner", "MukJump.Cloud.PendingLeaderboardOwner",
                "MukJump.Cloud.Revision", "MukJump.Cloud.PendingOperationId" })
            {
                deletionStringPrefs.Add(key, (PlayerPrefs.HasKey(key), PlayerPrefs.GetString(key, string.Empty)));
                PlayerPrefs.DeleteKey(key);
            }
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
            try
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
            finally
            {
                foreach (var preference in deletionPrefs)
                {
                    if (preference.Value.existed)
                        PlayerPrefs.SetInt(preference.Key, preference.Value.value);
                    else
                        PlayerPrefs.DeleteKey(preference.Key);
                }
                foreach (var preference in deletionStringPrefs)
                {
                    if (preference.Value.existed)
                        PlayerPrefs.SetString(preference.Key, preference.Value.value);
                    else
                        PlayerPrefs.DeleteKey(preference.Key);
                }
                PlayerPrefs.Save();
            }
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
            Assert.That(growthStore.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(growthStore.BackupJson, Is.EqualTo(growthStore.Json));
        }

        [Test]
        public void MigratedServerGrowthIsQueuedOnceForCloudSave()
        {
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 25 });
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            host = new GameObject("MigratedServerGrowthTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            string legacy = ExtendedV8GrowthJson();
            var server = new MukJumpCloudSnapshot
            {
                bestHeight = 25,
                growthJson = legacy,
                revision = 3,
            };
            SetPrivateField(account, "dirty", false);
            Assert.That(InvokePrivate<bool>(account, "TryApplyCloudSnapshot", server, false, false), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(40));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.EqualTo(40));
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(PlayerPrefs.GetInt("MukJump.Cloud.PendingSave", 0), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string canonical), Is.True);
            Assert.That(canonical, Does.Contain("\"balanceVersion\":11"));

            server.growthJson = canonical;
            SetPrivateField(account, "dirty", false);
            PlayerPrefs.DeleteKey("MukJump.Cloud.PendingSave");
            Assert.That(InvokePrivate<bool>(account, "TryApplyCloudSnapshot", server, false, false), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(40));
            Assert.That(ReadPrivateBool(account, "dirty"), Is.False);
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingSave"), Is.False);
        }

        static string ExtendedV8GrowthJson() => ValidGrowthJson
            .Replace("\"balanceVersion\":7", "\"balanceVersion\":8")
            .Replace("\"wallet\":0", "\"wallet\":39")
            .Replace("\"cumulativeDistanceMeters\":0", "\"cumulativeDistanceMeters\":3900")
            .Replace("\"claimedDistanceRewardCount\":0", "\"claimedDistanceRewardCount\":39");

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
            Assert.That(LobbySettingsProfile.GameplayTutorialVersion, Is.EqualTo(LobbySettingsProfile.CurrentGameplayTutorialVersion));
            Assert.That(LobbySettingsProfile.PlayerUid, Is.Not.EqualTo(previousUid));
        }

        [Test]
        public void ThrowingSettingsObserverCannotTurnDeletionResetIntoFailure()
        {
            var settingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settingsStore);
            _ = LobbySettingsProfile.PlayerUid;
            int laterObserverCalls = 0;
            LobbySettingsProfile.Changed += () =>
                throw new System.InvalidOperationException("settings observer failed");
            LobbySettingsProfile.Changed += () => laterObserverCalls++;

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 로비 설정 변경 알림 구독자 예외를 격리했습니다: " +
                "settings observer failed");
            bool reset = LobbySettingsProfile.TryResetForAccountDeletion();

            Assert.That(reset, Is.True,
                "설정 관찰자 오류가 영속화 성공을 탈퇴 정리 실패로 바꾸면 안 됩니다.");
            Assert.That(settingsStore.SaveCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(1f));
            Assert.That(LobbySettingsProfile.GameplayTutorialVersion, Is.Zero);
            Assert.That(laterObserverCalls, Is.EqualTo(1));
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

        [Test]
        public void SnapshotCaptureFailureNeverLeavesCloudSaveLatched()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetField(
                    "captureNextSnapshotForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Func<MukJumpCloudSnapshot>(() =>
                        throw new System.InvalidOperationException(
                            "snapshot capture failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 클라우드 저장 스냅샷을 만들지 못했습니다: " +
                "snapshot capture failed");
            Assert.DoesNotThrow(account.SaveNow);

            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
        }

        [Test]
        public void SynchronousSaveReadFailureReleasesLatchForRetryAndLogout()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            SetSaveSnapshotHook(account, CreateValidCloudSnapshot(2));
            typeof(MukJumpAccountRuntime).GetField(
                    "getMyDataForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<BackEnd.BackendReturnObject>>(
                        _ => throw new System.InvalidOperationException(
                            "get rows failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 저장 전 서버 확인 요청을 시작하지 못했습니다: " +
                "get rows failed");
            Assert.DoesNotThrow(account.SaveNow);

            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
        }

        [Test]
        public void SynchronousCloudLoadFailureReleasesGameplayBlock()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetField(
                    "dirty",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, false);
            typeof(MukJumpAccountRuntime).GetField(
                    "getMyDataForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<BackEnd.BackendReturnObject>>(
                        _ => throw new System.InvalidOperationException(
                            "load rows failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 서버 저장 조회를 시작하지 못했습니다: " +
                "load rows failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "LoadCloudSnapshot"));

            Assert.That(ReadPrivateBool(account, "cloudLoadInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
        }

        [Test]
        public void SynchronousInsertFailureReleasesSaveLatch()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            SetSaveSnapshotHook(account, CreateValidCloudSnapshot(1));
            typeof(MukJumpAccountRuntime).GetField(
                    "insertGameDataForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<BackEnd.BackendReturnObject>>(
                        _ => throw new System.InvalidOperationException(
                            "insert failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 첫 서버 저장 요청을 시작하지 못했습니다: " +
                "insert failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "InsertLocalSnapshot"));

            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
        }

        [Test]
        public void SynchronousUpdateFailureReleasesSaveLatch()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            var snapshot = CreateValidCloudSnapshot(2);
            typeof(MukJumpAccountRuntime).GetField(
                    "updateGameDataForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<BackEnd.BackendReturnObject>>(
                        _ => throw new System.InvalidOperationException(
                            "update failed")));
            typeof(MukJumpAccountRuntime).GetField(
                    "saveInFlight",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, true);

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 서버 저장 요청을 시작하지 못했습니다: " +
                "update failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "UpdateVerifiedSnapshot",
                snapshot,
                0L,
                0L,
                string.Empty));

            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
        }

        [Test]
        public void SynchronousBackendInitializationFailureReleasesConnectingState()
        {
            host = new GameObject("BackendInitializationFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetBackendRequestHook(
                account,
                "initializeBackendForTests",
                _ => throw new System.InvalidOperationException(
                    "initialize failed"));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 서버 초기화 요청을 시작하지 못했습니다: " +
                "initialize failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "BeginBackendInitialization"));

            Assert.That(
                ReadPrivateBool(account, "backendInitializationInFlight"),
                Is.False);
            Assert.That(account.Phase, Is.Not.EqualTo(
                MukJumpAccountPhase.Connecting));
        }

        [Test]
        public void SynchronousTokenLoginFailureReleasesLoginLatch()
        {
            host = new GameObject("TokenLoginFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetBackendRequestHook(
                account,
                "tokenLoginForTests",
                _ => throw new System.InvalidOperationException(
                    "token login failed"));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 저장된 계정 로그인 요청을 시작하지 못했습니다: " +
                "token login failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "BeginBackendTokenLogin",
                false));

            Assert.That(ReadPrivateBool(account, "tokenLoginInFlight"), Is.False);
            Assert.That(account.Phase, Is.Not.EqualTo(
                MukJumpAccountPhase.Connecting));
        }

        [Test]
        public void SynchronousGuestLoginFailureFallsBackToLocalReady()
        {
            host = new GameObject("GuestLoginFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetBackendRequestHook(
                account,
                "guestLoginForTests",
                _ => throw new System.InvalidOperationException(
                    "guest login failed"));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 게스트 로그인 요청을 시작하지 못했습니다: " +
                "guest login failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "BeginBackendGuestLogin"));

            Assert.That(account.Phase, Is.EqualTo(
                MukJumpAccountPhase.LocalReady));
            Assert.That(account.AccountKind, Is.EqualTo(
                MukJumpAccountKind.LocalGuest));
        }

        [Test]
        public void SynchronousProviderVerificationFailureReleasesGameplayLatch()
        {
            host = new GameObject("ProviderVerificationFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetBackendRequestHook(
                account,
                "getUserInfoForTests",
                _ => throw new System.InvalidOperationException(
                    "provider lookup failed"));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 로그인 제공자 확인 요청을 시작하지 못했습니다: " +
                "provider lookup failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "VerifyCurrentBackendProvider"));

            Assert.That(
                ReadPrivateBool(account, "backendProviderVerificationInFlight"),
                Is.False);
            Assert.That(account.Phase, Is.EqualTo(MukJumpAccountPhase.Error));
        }

        [Test]
        public void SynchronousLeaderboardLoadFailureReleasesLoadingLatch()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetField(
                    "getLeaderboardForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<
                        BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                        _ => throw new System.InvalidOperationException(
                            "leaderboard failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 순위 조회 요청을 시작하지 못했습니다: " +
                "leaderboard failed");
            Assert.DoesNotThrow(account.RefreshLeaderboard);

            Assert.That(account.LeaderboardLoading, Is.False);
            Assert.That(account.LeaderboardStatus, Does.Contain("다시 시도"));
        }

        [Test]
        public void MissingTokenLoginCallbackTimesOutAndLateCallbackIsIgnored()
        {
            host = new GameObject("TokenLoginTimeoutTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            System.Action<BackEnd.BackendReturnObject> lateCallback = null;
            SetBackendRequestHook(
                account,
                "tokenLoginForTests",
                callback => lateCallback = callback);

            InvokeLifecycle(account, "BeginBackendTokenLogin", false);
            SetPrivateField(account, "tokenLoginDeadlineRealtime", -1f);
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 저장된 계정 로그인 응답 시간이 초과되었습니다: " +
                "응답 시간 초과");
            InvokeLifecycle(account, "ProcessAccountRequestWatchdogs");

            Assert.That(ReadPrivateBool(account, "tokenLoginInFlight"), Is.False);
            MukJumpAccountPhase recoveredPhase = account.Phase;
            Assert.That(lateCallback, Is.Not.Null);
            Assert.DoesNotThrow(() => lateCallback(null));
            Assert.That(account.Phase, Is.EqualTo(recoveredPhase));
        }

        [Test]
        public void MissingCloudLoadCallbackReleasesLatchAndKeepsRetryDirty()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            SetPrivateField(account, "dirty", false);
            SetBackendRequestHook(
                account,
                "getMyDataForTests",
                _ => { });

            InvokeLifecycle(account, "LoadCloudSnapshot");
            SetPrivateField(account, "cloudLoadDeadlineRealtime", -1f);
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 클라우드 저장 응답 시간이 초과되어 다시 시도합니다.");
            InvokeLifecycle(account, "ProcessAccountRequestWatchdogs");

            Assert.That(ReadPrivateBool(account, "cloudLoadInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
        }

        MukJumpAccountRuntime CreateLeaderboardRuntime()
        {
            var account = CreateReadySaveRuntime();
            PermanentGrowthProfile.SettleRun("leaderboard-fixture-completed", 183, 0, true);
            SetPrivateField(account, "dirty", false);
            SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "rank-owner"));
            return account;
        }

        static BackEnd.Leaderboard.BackendUserLeaderboardReturnObject LeaderboardResult(string json)
        {
            // 네트워크는 호출하지 않고 공식 GetLeaderboard 응답 구조를 SDK 파서에 전달한다.
            var result = (BackEnd.Leaderboard.BackendUserLeaderboardReturnObject)
                System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                    typeof(BackEnd.Leaderboard.BackendUserLeaderboardReturnObject));
            typeof(BackEnd.BackendReturnObject).GetProperty("StatusCode").SetValue(result, 200);
            typeof(BackEnd.BackendReturnObject).GetProperty("ReturnValue").SetValue(result, json);
            return result;
        }

        const string RankedPlayerJson = "{\"rows\":[{\"gamerInDate\":\"rank-owner\"," +
            "\"nickname\":\"guest(1234567890)\",\"rank\":1,\"index\":0,\"score\":\"183\"}],\"totalCount\":1}";

        [Test]
        public void RealLeaderboardResponseReachesUiWithoutPreviewReplacement()
        {
            var account = CreateLeaderboardRuntime();
            SetPrivateField(account, "getLeaderboardForTests",
                new System.Action<System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    callback => callback(LeaderboardResult(RankedPlayerJson))));
            account.RefreshLeaderboard();

            Assert.That(account.LeaderboardEntries.Count, Is.EqualTo(1));
            Assert.That(account.LeaderboardEntries[0].DisplayName, Is.EqualTo("guest(1234567890)"));
            Assert.That(account.LeaderboardEntries[0].Height, Is.EqualTo(183));
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            var ranking = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            Assert.That(ranking.Find("Rank1").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("1"));
            Assert.That(ranking.Find("LeaderboardRow1").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("183 m"));
            Assert.That(ranking.Find("NameCell1/Name1").GetComponent<UnityEngine.UI.Text>().text, Does.StartWith("guest("));
            Assert.That(ranking.Find("LeaderboardRow2").GetComponent<UnityEngine.UI.Text>().text, Is.Empty);
        }

        [Test]
        public void EmptyLeaderboardRemovesOldRowsAndDisplaysEmptyState()
        {
            var account = CreateLeaderboardRuntime();
            string json = RankedPlayerJson;
            SetPrivateField(account, "getLeaderboardForTests",
                new System.Action<System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    callback => callback(LeaderboardResult(json))));
            account.RefreshLeaderboard();
            json = "{\"rows\":[],\"totalCount\":0}";
            account.RefreshLeaderboard();
            Assert.That(account.LeaderboardEntries, Is.Empty);
            Assert.That(account.LeaderboardStatus, Does.Contain("아직 등록된"));
        }

        [Test]
        public void LeaderboardSubmissionWaitsForSaveAndSendsLargestQueuedBest()
        {
            var account = CreateLeaderboardRuntime();
            int sent = -1;
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) =>
                { sent = height; callback(BackendResult(204)); }));
            SetPrivateField(account, "dirty", true);
            account.SubmitBestHeight(183);
            Assert.That(sent, Is.EqualTo(-1));
            Assert.That(PlayerPrefs.GetInt("MukJump.Cloud.PendingLeaderboardBest"), Is.EqualTo(183));

            SetPrivateField(account, "dirty", false);
            account.SubmitBestHeight(25);
            Assert.That(sent, Is.EqualTo(183));
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingLeaderboardBest"), Is.False);
        }

        [Test]
        public void LoginAloneDoesNotRegisterZeroButCompletedZeroMeterRunDoes()
        {
            var account = CreateLeaderboardRuntime();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            int calls = 0;
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) =>
                { calls++; callback(BackendResult(204)); }));
            account.SubmitBestHeight(0);
            account.SubmitBestHeight(50);
            Assert.That(calls, Is.Zero);
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingLeaderboardBest"), Is.False);
            PermanentGrowthProfile.SettleRun("first-completed-zero", 0, 0, true);
            account.SubmitBestHeight(0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase("cloudLoadInFlight")]
        [TestCase("backendProviderVerificationInFlight")]
        [TestCase("temporaryBackendPause")]
        public void VerifiedCloudSaveKeeps43MeterRankQueuedUntilTransitionUnlocks(string blockingField)
        {
            var account = CreateLeaderboardRuntime();
            int sent = -1;
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) =>
                { sent = height; callback(BackendResult(204)); }));
            SetPrivateField(account, blockingField, true);
            long savedMutation = (long)typeof(MukJumpAccountRuntime).GetField(
                "localMutationVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(account);
            InvokeLifecycle(account, "CompleteSuccessfulSave", new MukJumpCloudSnapshot
                { bestHeight = 43, revision = 1 }, savedMutation);
            account.SubmitBestHeight(43);
            Assert.That(sent, Is.EqualTo(-1));
            Assert.That(PlayerPrefs.GetInt("MukJump.Cloud.PendingLeaderboardBest", -1), Is.EqualTo(43));
            Assert.That(PlayerPrefs.GetString("MukJump.Cloud.PendingLeaderboardOwner"), Is.EqualTo("rank-owner"));
            SetPrivateField(account, blockingField, false);
            account.SubmitBestHeight(PlayerPrefs.GetInt("MukJump.Cloud.PendingLeaderboardBest"));
            Assert.That(sent, Is.EqualTo(43));
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingLeaderboardBest"), Is.False);
        }

        [Test]
        public void VerifiedEmptyCloudSaveDoesNotQueueUnplayedZeroMeterRank()
        {
            var account = CreateLeaderboardRuntime();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            InvokeLifecycle(account, "CompleteSuccessfulSave", new MukJumpCloudSnapshot
                { bestHeight = 0, revision = 1 }, 0L);
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingLeaderboardBest"), Is.False);
        }

        [Test]
        public void RankWriteFailureRemainsVisibleAfterSuccessfulEmptyRead()
        {
            var account = CreateLeaderboardRuntime();
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) =>
                    callback(BackendResult(403))));
            SetPrivateField(account, "getLeaderboardForTests",
                new System.Action<System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    callback => callback(LeaderboardResult("{\"rows\":[],\"totalCount\":0}"))));
            account.SubmitBestHeight(43);
            account.RefreshLeaderboard();
            Assert.That(account.LeaderboardStatus, Does.Contain("403"));
            Assert.That(account.LeaderboardStatus, Does.Contain("재시도"));
            Assert.That(account.LeaderboardStatus, Does.Not.Contain("아직 등록된"));
            Assert.That(PlayerPrefs.GetInt("MukJump.Cloud.PendingLeaderboardBest"), Is.EqualTo(43));
        }

        [Test]
        public void AuthenticatedGuestCanReadLeaderboardBeforePlayingWithoutSubmitting()
        {
            var account = CreateLeaderboardRuntime();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            typeof(MukJumpAccountRuntime).GetProperty("AccountKind").SetValue(account, MukJumpAccountKind.BackendGuest);
            SetPrivateField(account, "getLeaderboardForTests",
                new System.Action<System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    callback => callback(LeaderboardResult(RankedPlayerJson))));
            account.RefreshLeaderboard();
            Assert.That(account.LeaderboardEntries.Count, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.HasCompletedRun, Is.False);
            Assert.That(PlayerPrefs.HasKey("MukJump.Cloud.PendingLeaderboardBest"), Is.False);
        }

        [TestCase("saveInFlight")]
        [TestCase("cloudLoadInFlight")]
        [TestCase("syncWriteBlocked")]
        [TestCase("profileResolutionPending")]
        public void LeaderboardNeverWritesDuringUnresolvedProfileOperations(string blockingField)
        {
            var account = CreateLeaderboardRuntime();
            SetPrivateField(account, blockingField, true);
            int calls = 0;
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) => calls++));
            account.SubmitBestHeight(183);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void CloudSaveWaitsForLeaderboardWriteToSameRow()
        {
            var account = CreateLeaderboardRuntime();
            SetPrivateField(account, "dirty", true);
            SetPrivateField(account, "leaderboardSaveInFlight", true);
            int calls = 0;
            SetBackendRequestHook(account, "getMyDataForTests", _ => calls++);
            account.SaveNow();
            Assert.That(calls, Is.Zero);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
        }

        [Test]
        public void SuccessfulSubmissionReloadsAnAlreadyRequestedBoardAfterOldReadCompletes()
        {
            var account = CreateLeaderboardRuntime();
            var reads = new System.Collections.Generic.List<
                System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>();
            SetPrivateField(account, "getLeaderboardForTests",
                new System.Action<System.Action<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                    callback => reads.Add(callback)));
            SetPrivateField(account, "updateLeaderboardForTests",
                new System.Action<int, System.Action<BackEnd.BackendReturnObject>>((height, callback) =>
                    callback(BackendResult(204))));
            account.RefreshLeaderboard();
            account.SubmitBestHeight(183);
            Assert.That(reads.Count, Is.EqualTo(1), "현재 조회와 새 조회가 겹치지 않아야 합니다.");
            reads[0](LeaderboardResult("{\"rows\":[],\"totalCount\":0}"));
            Assert.That(reads.Count, Is.EqualTo(2));
            reads[1](LeaderboardResult(RankedPlayerJson));
            Assert.That(account.LeaderboardEntries.Count, Is.EqualTo(1));
            Assert.That(account.LeaderboardEntries[0].Height, Is.EqualTo(183));
            Assert.That(account.LeaderboardLoading, Is.False);
        }

        [Test]
        public void ServerNicknameChangeQueuesVerifiedSaveBeforeRankRefresh()
        {
            MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
            try
            {
                var account = CreateLeaderboardRuntime();
                typeof(MukJumpAccountRuntime).GetProperty(nameof(MukJumpAccountRuntime.AccountKind))
                    .SetValue(account, MukJumpAccountKind.Apple);
                SetPrivateField(account, "nicknameUpdateForTests",
                    new System.Action<string, System.Action<BackEnd.BackendReturnObject>>((name, callback) =>
                        callback(BackendResult(204))));
                bool completed = false;
                account.ChangeNickname("새먹방울", (success, _) => completed = success);
                Assert.That(completed, Is.True);
                Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
                Assert.That(PlayerPrefs.GetInt("MukJump.Cloud.PendingSave"), Is.EqualTo(1));
            }
            finally { MukJumpIdentityProfile.UseStoreForTests(null); }
        }

        [Test]
        public void MissingLeaderboardCallbackReleasesLoadingLatch()
        {
            MukJumpAccountRuntime account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetField(
                    "getLeaderboardForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Action<System.Action<
                        BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>>(
                        _ => { }));

            account.RefreshLeaderboard();
            SetPrivateField(account, "leaderboardLoadDeadlineRealtime", -1f);
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 순위 조회 응답 시간이 초과되었습니다.");
            InvokeLifecycle(account, "ProcessAccountRequestWatchdogs");

            Assert.That(account.LeaderboardLoading, Is.False);
            Assert.That(account.LeaderboardStatus, Does.Contain("다시 시도"));
        }

        [Test]
        public void RecoveryMarkerFailureStopsGuestUpgradeBeforeSdkRequest()
        {
            host = new GameObject("GuestUpgradeMarkerFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            int requestCount = 0;
            SetPrivateField(
                account,
                "recoveryMarkerPersistenceForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "marker flush failed")));
            SetBackendRequestHook(
                account,
                "changeFederationForTests",
                _ => requestCount++);

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 게스트 계정 연결 복구 표식을 저장하지 못했습니다: " +
                "marker flush failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "UpgradeGuestFederation",
                "token",
                BackEnd.FederationType.Google,
                MukJumpAccountKind.Google,
                "로그인 실패",
                0));

            Assert.That(requestCount, Is.Zero);
            Assert.That(ReadPrivateBool(account, "federationRequestInFlight"),
                Is.False);
            Assert.That(account.Phase, Is.EqualTo(MukJumpAccountPhase.Error));
        }

        [Test]
        public void RecoveryMarkerFailureStopsAuthorizationBeforeSdkRequest()
        {
            host = new GameObject("AuthorizationMarkerFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            int requestCount = 0;
            SetPrivateField(
                account,
                "recoveryMarkerPersistenceForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "marker flush failed")));
            SetBackendRequestHook(
                account,
                "authorizeFederationForTests",
                _ => requestCount++);

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 계정 전환 복구 표식을 저장하지 못했습니다: " +
                "marker flush failed");
            Assert.DoesNotThrow(() => InvokeLifecycle(
                account,
                "AuthorizeExistingFederation",
                "token",
                BackEnd.FederationType.Google,
                MukJumpAccountKind.Google,
                true,
                false));

            Assert.That(requestCount, Is.Zero);
            Assert.That(ReadPrivateBool(account, "federationRequestInFlight"),
                Is.False);
            Assert.That(account.Phase, Is.EqualTo(MukJumpAccountPhase.Error));
        }

        [Test]
        public void RecoveryMarkerFailureRollsBackLogoutIntent()
        {
            host = new GameObject("LogoutMarkerFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetPrivateField(
                account,
                "recoveryMarkerPersistenceForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "marker flush failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 로그아웃 복구 표식을 저장하지 못했습니다: " +
                "marker flush failed");
            bool began = InvokePrivate<bool>(
                account,
                "BeginPendingLocalLogoutCleanup");

            Assert.That(began, Is.False);
            Assert.That(ReadPrivateBool(account, "localLogoutCleanupPending"),
                Is.False);
        }

        [Test]
        public void RecoveryMarkerFailureRollsBackAccountDeletionIntent()
        {
            host = new GameObject("DeletionMarkerFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetPrivateField(
                account,
                "recoveryMarkerPersistenceForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "marker flush failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 계정 삭제 복구 표식을 저장하지 못했습니다: " +
                "marker flush failed");
            bool began = InvokePrivate<bool>(
                account,
                "BeginPendingLocalAccountDeletion",
                "owner-1",
                true);

            Assert.That(began, Is.False);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionCleanupPending"),
                Is.False);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionAppleRevokeRequired"),
                Is.False);
        }

        [Test]
        public void AccountDeletionCanBeCancelledBeforeRemoteMutation()
        {
            host = new GameObject("DeletionCancellationTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);

            Assert.That(InvokePrivate<bool>(
                account,
                "BeginPendingLocalAccountDeletion",
                "owner-1",
                true), Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);

            Assert.That(InvokePrivate<bool>(
                account,
                "CancelPendingLocalAccountDeletionBeforeRemoteMutation",
                "owner-1"), Is.True);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionCleanupPending"),
                Is.False);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionAppleRevokeRequired"),
                Is.False);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
        }

        [Test]
        public void AccountDeletionCancellationPersistenceFailureKeepsRecoveryIntent()
        {
            host = new GameObject("DeletionCancellationFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            Assert.That(InvokePrivate<bool>(
                account,
                "BeginPendingLocalAccountDeletion",
                "owner-1",
                true), Is.True);
            SetPrivateField(
                account,
                "recoveryMarkerPersistenceForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "cancel flush failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 계정 삭제 취소 표식을 저장하지 못했습니다: " +
                "cancel flush failed");
            Assert.That(InvokePrivate<bool>(
                account,
                "CancelPendingLocalAccountDeletionBeforeRemoteMutation",
                "owner-1"), Is.False);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionCleanupPending"),
                Is.True);
            Assert.That(
                ReadPrivateBool(account, "accountDeletionAppleRevokeRequired"),
                Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);

            SetPrivateField<System.Action>(
                account,
                "recoveryMarkerPersistenceForTests",
                null);
            Assert.That(InvokePrivate<bool>(
                account,
                "CancelPendingLocalAccountDeletionBeforeRemoteMutation",
                "owner-1"), Is.True);
        }

        [TestCase(false, 0, false)]
        [TestCase(false, 3, false)]
        [TestCase(false, 1, true)]
        [TestCase(false, 2, true)]
        [TestCase(false, 99, true)]
        [TestCase(true, 0, true)]
        [TestCase(true, 3, true)]
        [TestCase(true, 2, true)]
        public void AppleDeletionRecoveryMarkerSurvivesRestartAndLegacyAmbiguity(
            bool revokeRequestDispatched,
            int persistedState,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldKeepAppleDeletionRecoveryMarker(
                    revokeRequestDispatched,
                    persistedState),
                Is.EqualTo(expected));
        }

        [TestCase("owner-a", "owner-a", "apple", "apple-a", true)]
        [TestCase("owner-a", "owner-b", "apple", "apple-a", false)]
        [TestCase("owner-a", "owner-a", "google", "apple-a", false)]
        [TestCase("owner-a", "owner-a", "apple", "", false)]
        [TestCase("owner-a", "owner-a", "apple", null, false)]
        [TestCase("", "", "apple", "apple-a", false)]
        public void AppleDeletionRequiresVerifiedServerOwnerAndProvider(
            string expected, string server, string provider, string subject, bool valid)
        {
            Assert.That(MukJumpAccountRuntime.IsVerifiedAppleDeletionOwner(
                expected, server, provider, subject), Is.EqualTo(valid));
        }

        [TestCase("apple-a", "apple-a", true)]
        [TestCase("apple-a", "apple-b", false)]
        [TestCase("apple-a", "APPLE-A", false)]
        [TestCase("apple-a", "apple-a ", false)]
        [TestCase("", "", false)]
        [TestCase(null, null, false)]
        public void AppleDeletionRequiresExactNonemptyAppleSubject(
            string expected, string credential, bool valid)
        {
            Assert.That(MukJumpAccountRuntime.IsSameAppleDeletionSubject(
                expected, credential), Is.EqualTo(valid));
        }

        const string AppleDeletionMarkerKey =
            "MukJump.Account.PendingLocalAccountDeletionAppleRevokeRequired";

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(99)]
        public void AppleDeletionLegacyAndDispatchedIntentCannotBeCancelledOrReplaced(int state)
        {
            var account = CreateAppleDeletionAccount();
            PlayerPrefs.SetInt(AppleDeletionMarkerKey, state);
            // 요청별 latch가 초기화된 재실행 상황에서도 내구 상태가 우선한다.
            SetPrivateField(account, "appleDeletionRevokeDispatched", false);
            Assert.That(InvokePrivate<bool>(account,
                "CancelPendingLocalAccountDeletionBeforeRemoteMutation", "owner-a"), Is.False);
            Assert.That(InvokePrivate<bool>(account,
                "BeginPendingLocalAccountDeletion", "owner-b", true), Is.False);
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Cancel();
            Assert.That(callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(state));
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
        }

        [Test]
        public void AppleDeletionRetryWhileVerifyingOwnerDoesNotDispatchTwice()
        {
            var account = CreateAppleDeletionAccount();
            int calls = 0;
            SetBackendRequestHook(account, "getUserInfoForTests", callback => calls++);
            InvokeLifecycle(account, "ReauthenticateAppleAndDelete", "owner-a");
            account.RetryPendingProfileResolution();
            account.RetryPendingProfileResolution();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ReadPrivateBool(account, "appleDeletionInFlight"), Is.True);
        }

        [Test]
        public void AppleDeletionWrongStoredOwnerCannotStartAnyRemoteRequest()
        {
            var account = CreateAppleDeletionAccount();
            SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "owner-b"));
            int calls = 0;
            SetBackendRequestHook(account, "getUserInfoForTests", callback => calls++);
            SetBackendRequestHook(account, "withdrawAccountForTests", callback => calls++);
            account.RetryPendingProfileResolution();
            InvokeLifecycle(account, "ReauthenticateAppleAndDelete", "owner-b");
            Assert.That(calls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(3));
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
        }

        [Test]
        public void AppleDeletionDuplicateCallbacksAndRetriesWithdrawExactlyOnce()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Owner(AppleOwnerResult());
            Assert.That(callbacks.LoginCalls, Is.EqualTo(1));
            callbacks.Credential("apple-a", "test-code");
            callbacks.Cancel();
            callbacks.Credential("apple-a", "duplicate-code");
            account.RetryPendingProfileResolution();
            account.RetryPendingProfileResolution();
            Assert.That(callbacks.OwnerCalls, Is.EqualTo(1));
            Assert.That(callbacks.RevokeCalls, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
            callbacks.Revoke(BackendResult(200));
            callbacks.Revoke(BackendResult(200));
            Assert.That(callbacks.WithdrawCalls, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey, 0), Is.Zero);
        }

        [TestCase("owner-b", "apple", "apple-a")]
        [TestCase("owner-a", "google", "apple-a")]
        [TestCase("owner-a", "apple", "")]
        public void AppleDeletionUnverifiedServerOwnerPreservesIntentAndBlocksSession(
            string owner, string provider, string subject)
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult(owner, provider, subject));
            Assert.That(callbacks.LoginCalls + callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
            Assert.That(account.IsOnlineAuthenticated, Is.False);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(3));
        }

        [Test]
        public void AppleDeletionMalformedServerResponsePreservesIntentAndBlocksSession()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            // 현재 SDK는 잘못된 JSON을 null로 반환한다(예외를 던지지 않는다).
            callbacks.Owner(BackendResult(200, "{invalid-json"));
            Assert.That(callbacks.LoginCalls + callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
            Assert.That(account.IsOnlineAuthenticated, Is.False);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(3));
        }

        [Test]
        public void AppleDeletionSynchronousSuccessThenThrowDoesNotAbortNextStage()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            SetBackendRequestHook(account, "getUserInfoForTests", callback =>
            {
                callbacks.OwnerCalls++;
                callback(AppleOwnerResult());
                throw new System.InvalidOperationException("owner callback already delivered");
            });
            SetPrivateField(account, "appleDeletionCredentialForTests",
                new System.Action<System.Action<string, string>, System.Action>((success, cancel) =>
                {
                    callbacks.LoginCalls++;
                    success("apple-a", "code");
                    cancel();
                    throw new System.InvalidOperationException("credential callback already delivered");
                }));
            account.RetryPendingProfileResolution();
            Assert.That(ReadPrivateBool(account, "appleDeletionInFlight"), Is.True);
            Assert.That(callbacks.OwnerCalls, Is.EqualTo(1));
            Assert.That(callbacks.LoginCalls, Is.EqualTo(1));
            Assert.That(callbacks.RevokeCalls, Is.EqualTo(1));
            callbacks.Revoke(BackendResult(200));
            Assert.That(callbacks.WithdrawCalls, Is.EqualTo(1));
        }

        [TestCase("apple-b", "code")]
        [TestCase("", "code")]
        [TestCase("apple-a", "")]
        public void AppleDeletionMismatchedOrMissingCredentialCannotRevoke(string user, string code)
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Credential(user, code);
            Assert.That(callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.False,
                "신규 요청이 전송되기 전 취소만 허용한다.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AppleDeletionOldCredentialCannotMutateChangedOwnerOrSession(bool changeOwner)
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            if (changeOwner)
                SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "owner-b"));
            else
                SetPrivateField(account, "accountSessionGeneration", 1L);
            callbacks.Credential("apple-a", "code");
            Assert.That(callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(3));
        }

        [Test]
        public void AppleDeletionTimeoutThenCancelAfterDisableKeepsDispatchedIntent()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Credential("apple-a", "code");
            var oldRevoke = callbacks.Revoke;
            SetPrivateField(account, "appleDeletionDeadlineRealtime", -1f);
            LogAssert.Expect(LogType.Warning, "[MukJump] Apple 계정 삭제 확인 실패: 응답 시간 초과");
            InvokeLifecycle(account, "ProcessAccountRequestWatchdogs");
            InvokeLifecycle(account, "OnDisable");
            InvokeLifecycle(account, "OnEnable");
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Cancel();
            oldRevoke(BackendResult(200));
            Assert.That(callbacks.WithdrawCalls, Is.Zero);
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AppleDeletionOldRevokeSuccessCannotClearAnotherOwnerOrSession(int changedBoundary)
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Credential("apple-a", "code");
            if (changedBoundary == 0)
                SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "owner-b"));
            else if (changedBoundary == 1)
                PlayerPrefs.SetString(DeletionOwnerKey, "owner-b");
            else
                SetPrivateField(account, "accountSessionGeneration", 1L);
            callbacks.Revoke(BackendResult(200));
            Assert.That(callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
            Assert.That(ReadPrivateBool(account, "accountDeletionAppleRevokeRequired"), Is.True);
        }

        [TestCase(400)]
        [TestCase(408)]
        [TestCase(500)]
        public void AppleDeletionRemoteFailureIsNotProofThatNoRevokeOccurred(int status)
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Credential("apple-a", "code");
            callbacks.Revoke(BackendResult(status));
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Cancel();
            Assert.That(callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
        }

        [Test]
        public void AppleDeletionDispatchPersistenceFailureMakesNoRemoteCall()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            SetPrivateField(account, "recoveryMarkerPersistenceForTests", new System.Action(() =>
                throw new System.InvalidOperationException("dispatch save failed")));
            LogAssert.Expect(LogType.Warning, "[MukJump] Apple 계정 삭제 확인 실패: dispatch save failed");
            callbacks.Credential("apple-a", "code");
            Assert.That(callbacks.RevokeCalls + callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
        }

        [Test]
        public void AppleDeletionRevokeSuccessPersistenceFailureCannotWithdraw()
        {
            var account = CreateAppleDeletionAccount();
            var callbacks = CaptureAppleDeletionCallbacks(account);
            account.RetryPendingProfileResolution();
            callbacks.Owner(AppleOwnerResult());
            callbacks.Credential("apple-a", "code");
            SetPrivateField(account, "recoveryMarkerPersistenceForTests", new System.Action(() =>
                throw new System.InvalidOperationException("success save failed")));
            LogAssert.Expect(LogType.Warning,
                "[MukJump] Apple 연결 해제 완료 표식을 저장하지 못했습니다: success save failed");
            callbacks.Revoke(BackendResult(200));
            Assert.That(callbacks.WithdrawCalls, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(AppleDeletionMarkerKey), Is.EqualTo(2));
            Assert.That(account.HasPendingAccountDeletionCleanup, Is.True);
        }

        sealed class AppleDeletionCallbacks
        {
            public int OwnerCalls, LoginCalls, RevokeCalls, WithdrawCalls;
            public System.Action<BackEnd.BackendReturnObject> Owner, Revoke;
            public System.Action<string, string> Credential;
            public System.Action Cancel;
        }

        static AppleDeletionCallbacks CaptureAppleDeletionCallbacks(MukJumpAccountRuntime account)
        {
            var captured = new AppleDeletionCallbacks();
            SetBackendRequestHook(account, "getUserInfoForTests", callback =>
            {
                captured.OwnerCalls++;
                captured.Owner = callback;
            });
            SetPrivateField(account, "appleDeletionCredentialForTests",
                new System.Action<System.Action<string, string>, System.Action>((success, cancel) =>
                {
                    captured.LoginCalls++;
                    captured.Credential = success;
                    captured.Cancel = cancel;
                }));
            SetPrivateField(account, "revokeAppleTokenForTests",
                new System.Action<string, System.Action<BackEnd.BackendReturnObject>>((code, callback) =>
                {
                    captured.RevokeCalls++;
                    captured.Revoke = callback;
                }));
            SetBackendRequestHook(account, "withdrawAccountForTests", callback => captured.WithdrawCalls++);
            return captured;
        }

        static BackEnd.BackendReturnObject AppleOwnerResult(
            string owner = "owner-a", string provider = "apple", string subject = "apple-a") =>
            BackendResult(200, "{\"row\":{\"inDate\":\"" + owner +
                "\",\"subscriptionType\":\"" + provider + "\",\"federationId\":\"" + subject + "\"}}");

        static BackEnd.BackendReturnObject BackendResult(int status, string json = "{}")
        {
            // 현재 SDK의 공개 속성/private setter로만 테스트 응답을 구성한다.
            // 실제 SDK 파서와 IsSuccess를 사용하고 서버 호출은 전혀 하지 않는다.
            var result = new BackEnd.BackendReturnObject();
            typeof(BackEnd.BackendReturnObject).GetProperty("StatusCode").SetValue(result, status);
            typeof(BackEnd.BackendReturnObject).GetProperty("ReturnValue").SetValue(result, json);
            return result;
        }

        MukJumpAccountRuntime CreateAppleDeletionAccount()
        {
            host = new GameObject("AppleDeletionSafetyTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "owner-a"));
            typeof(MukJumpAccountRuntime).GetProperty(nameof(MukJumpAccountRuntime.IsOnlineAuthenticated))
                .SetValue(account, true);
            typeof(MukJumpAccountRuntime).GetProperty(nameof(MukJumpAccountRuntime.AccountKind))
                .SetValue(account, MukJumpAccountKind.Apple);
            Assert.That(InvokePrivate<bool>(account,
                "BeginPendingLocalAccountDeletion", "owner-a", true), Is.True);
            return account;
        }

        [Test]
        public void AppleSdkPumpFailureDoesNotSkipOtherRequestWatchdogs()
        {
            host = new GameObject("ApplePumpWatchdogTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            SetPrivateField(
                account,
                "pumpAppleAuthenticationForTests",
                new System.Action(() =>
                    throw new System.InvalidOperationException("pump failed")));
            SetPrivateField(account, "interactiveFederationInFlight", true);
            SetPrivateField(
                account,
                "interactiveFederationKind",
                MukJumpAccountKind.Apple);
            SetPrivateField(account, "saveInFlight", true);
            SetPrivateField(account, "saveDeadlineRealtime", -1f);

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] Apple 로그인 처리기 오류: pump failed");
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 클라우드 저장 응답 시간이 초과되어 다시 시도합니다.");
            InvokeLifecycle(account, "Update");

            Assert.That(
                ReadPrivateBool(account, "interactiveFederationInFlight"),
                Is.False);
            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False,
                "Apple SDK pump 예외가 다른 요청의 timeout 해제를 막으면 안 됩니다.");
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
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

        [TestCase(10f, 10.1f, false)]
        [TestCase(10f, 10.44f, false)]
        [TestCase(10f, 10.46f, true)]
        [TestCase(10f, 14f, true)]
        [TestCase(10f, 14.01f, false)]
        public void AccountDeletionNeedsADeliberateSecondTap(
            float armedAt,
            float now,
            bool expected)
        {
            Assert.That(
                LobbyOptionsView.IsDeleteConfirmationReady(armedAt, now),
                Is.EqualTo(expected));
        }

        [TestCase(10f, 9f, true)]
        [TestCase(10f, 14f, false)]
        [TestCase(10f, 14.01f, true)]
        public void AccountDeletionConfirmationExpiresSafely(
            float armedAt,
            float now,
            bool expected)
        {
            Assert.That(
                LobbyOptionsView.HasDeleteConfirmationExpired(armedAt, now),
                Is.EqualTo(expected));
        }

        [Test]
        public void AccountStateCallbackCancelsArmedDeletion()
        {
            host = new GameObject("AccountDeleteConfirmationStateTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetProperty(
                    nameof(MukJumpAccountRuntime.IsOnlineAuthenticated),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, true);
            typeof(MukJumpAccountRuntime).GetProperty(
                    nameof(MukJumpAccountRuntime.Phase),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, MukJumpAccountPhase.OnlineReady);
            ActivateAccountRuntime(account);

            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var options = host.AddComponent<LobbyOptionsView>();
            options.BuildForTests();
            typeof(LobbyOptionsView).GetMethod(
                    "ShowAccountPage",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(options, null);
            typeof(LobbyOptionsView).GetMethod(
                    "HandleDeleteAccount",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(options, null);

            var armedField = typeof(LobbyOptionsView).GetField(
                "deleteConfirmationArmed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armedField?.GetValue(options), Is.EqualTo(true));

            typeof(MukJumpAccountRuntime).GetMethod(
                    "SetStatus",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(account, new object[] { "백그라운드 저장 완료" });

            Assert.That(armedField?.GetValue(options), Is.EqualTo(false),
                "비동기 상태 콜백 뒤 이전 삭제 확인을 재사용하면 안 됩니다.");
        }

        [Test]
        public void ThrowingAccountStateObserverCannotBlockLaterObservers()
        {
            host = new GameObject("ThrowingAccountStateObserverTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            int laterObserverCalls = 0;
            account.StateChanged += () =>
                throw new System.InvalidOperationException("observer failed");
            account.StateChanged += () => laterObserverCalls++;

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 계정 상태 알림 구독자 예외를 격리했습니다: " +
                "observer failed");
            InvokeLifecycle(account, "SetStatus", "저장 완료");

            Assert.That(account.StatusMessage, Is.EqualTo("저장 완료"));
            Assert.That(laterObserverCalls, Is.EqualTo(1),
                "깨진 UI 구독자 뒤의 상태 관찰자도 호출되어야 합니다.");
        }

        [Test]
        public void LeavingApplicationCancelsArmedAccountDeletion()
        {
            host = new GameObject("AccountDeleteConfirmationFocusTest");
            var options = host.AddComponent<LobbyOptionsView>();
            var armedField = typeof(LobbyOptionsView).GetField(
                "deleteConfirmationArmed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var armedAtField = typeof(LobbyOptionsView).GetField(
                "deleteConfirmationArmedAt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armedField, Is.Not.Null);
            Assert.That(armedAtField, Is.Not.Null);

            armedField.SetValue(options, true);
            armedAtField.SetValue(options, 10f);
            InvokeLifecycle(options, "OnApplicationPause", true);
            Assert.That(armedField.GetValue(options), Is.EqualTo(false));

            armedField.SetValue(options, true);
            armedAtField.SetValue(options, 10f);
            InvokeLifecycle(options, "OnApplicationFocus", false);
            Assert.That(armedField.GetValue(options), Is.EqualTo(false));
        }

        [TestCase(RuntimePlatform.IPhonePlayer, false, true, false, true)]
        [TestCase(RuntimePlatform.Android, false, true, false, true)]
        [TestCase(RuntimePlatform.IPhonePlayer, false, true, true, true)]
        [TestCase(RuntimePlatform.Android, false, true, true, true)]
        [TestCase(RuntimePlatform.WebGLPlayer, false, true, false, false)]
        [TestCase(RuntimePlatform.OSXEditor, true, true, false, false)]
        [TestCase(RuntimePlatform.IPhonePlayer, false, false, false, false)]
        public void BackendStartsInConfiguredNativeReleaseAndDevelopmentRuntime(
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

        [TestCase(MukJumpAccountKind.LocalGuest, false, true)]
        [TestCase(MukJumpAccountKind.BackendGuest, false, false)]
        [TestCase(MukJumpAccountKind.BackendGuest, true, true)]
        [TestCase(MukJumpAccountKind.Google, true, false)]
        [TestCase(MukJumpAccountKind.Apple, true, false)]
        public void ExistingGuestTokenExpiryOnlyReusesStoredGuestCredentials(
            MukJumpAccountKind kind, bool credentials, bool expected)
        {
            Assert.That(MukJumpAccountRuntime.ShouldStartGuestLoginAfterTokenFailure(
                false, false, false, kind, true, credentials), Is.EqualTo(expected));
            Assert.That(MukJumpAccountRuntime.ShouldStartGuestLoginAfterTokenFailure(
                false, true, false, kind, true, credentials), Is.False);
            Assert.That(MukJumpAccountRuntime.ShouldStartGuestLoginAfterTokenFailure(
                false, false, true, kind, true, credentials), Is.False);
            Assert.That(MukJumpAccountRuntime.ShouldStartGuestLoginAfterTokenFailure(
                false, false, false, kind, false, credentials), Is.False);
        }

        [Test]
        public void AutomaticGuestReconnectRequiresIdleLobbyWithoutAccountRecovery()
        {
            foreach (var kind in new[] { MukJumpAccountKind.LocalGuest, MukJumpAccountKind.BackendGuest })
            {
                Assert.That(MukJumpAccountRuntime.ShouldRetryGuestConnection(
                    MukJumpAccountPhase.LocalReady, kind, false, false, false, false, true, 10, 10), Is.True);
                for (int block = 0; block < 7; block++)
                    Assert.That(MukJumpAccountRuntime.ShouldRetryGuestConnection(
                        block == 0 ? MukJumpAccountPhase.Connecting : MukJumpAccountPhase.LocalReady,
                        kind, block == 1, block == 2, block == 3, block == 4,
                        block != 5, block == 6 ? 9 : 10, 10), Is.False);
            }
            foreach (var kind in new[] { MukJumpAccountKind.Google, MukJumpAccountKind.Apple })
                Assert.That(MukJumpAccountRuntime.ShouldRetryGuestConnection(
                    MukJumpAccountPhase.LocalReady, kind, false, false, false, false, true, 10, 10), Is.False);
        }

        [Test]
        public void GuestLoginDoesNotDispatchTwiceWhileAwaitingServer()
        {
            host = new GameObject("SingleGuestLoginRequestTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            int requests = 0;
            SetBackendRequestHook(account, "guestLoginForTests", _ => requests++);
            InvokeLifecycle(account, "BeginBackendGuestLogin");
            InvokeLifecycle(account, "BeginBackendGuestLogin");
            Assert.That(requests, Is.EqualTo(1));
        }

        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, false, false, false, false, false, true)]
        [TestCase(false, MukJumpAccountKind.LocalGuest, "", false, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.BackendGuest, "", false, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.Google, "", false, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.Apple, "", false, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "owner-a", false, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", true, false, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, true, false, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, false, true, false, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, false, false, true, false, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, false, false, false, true, false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, false, false, false, false, true, false)]
        public void OnlyFreshLocalGuestMayPlayDuringBackendInitialization(
            bool initializationInFlight,
            MukJumpAccountKind accountKind,
            string storedOwner,
            bool authorizedTransitionPending,
            bool guestUpgradePending,
            bool localGuestImportPending,
            bool profileResolutionPending,
            bool localLogoutCleanupPending,
            bool accountDeletionCleanupPending,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldAllowLocalGameplayDuringBackendInitialization(
                        initializationInFlight,
                        accountKind,
                        storedOwner,
                        authorizedTransitionPending,
                        guestUpgradePending,
                        localGuestImportPending,
                        profileResolutionPending,
                        localLogoutCleanupPending,
                        accountDeletionCleanupPending),
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

        [Test]
        public void PersistedLegacyLogoutMigratesWithoutErasingCompletedGuestRun()
        {
            var account = CreateReadySaveRuntime();
            PermanentGrowthProfile.SettleRun("legacy-offline-43m", 43, 0, true);
            string before = growthStore.Json;
            PlayerPrefs.SetInt("MukJump.Account.Kind", (int)MukJumpAccountKind.LocalGuest);
            PlayerPrefs.SetInt("MukJump.Account.AutomaticAuthenticationSuppressed", 1);
            SetPrivateField(account, "suppressAutomaticAuthentication", true);
            InvokeLifecycle(account, "MigrateLegacyGuestLogout");
            Assert.That(ReadPrivateBool(account, "suppressAutomaticAuthentication"), Is.False);
            Assert.That(PlayerPrefs.HasKey("MukJump.Account.AutomaticAuthenticationSuppressed"), Is.False);
            Assert.That(PlayerPrefs.GetInt("MukJump.Account.LegacyGuestCredentialCleanup"), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.HasCompletedRun, Is.True);
            Assert.That(growthStore.Json, Is.EqualTo(before));
            InvokeLifecycle(account, "MigrateLegacyGuestLogout");
            Assert.That(growthStore.Json, Is.EqualTo(before));
        }

        [TestCase(true, MukJumpAccountKind.LocalGuest, "", false, true)]
        [TestCase(false, MukJumpAccountKind.LocalGuest, "", false, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "", true, false)]
        [TestCase(true, MukJumpAccountKind.LocalGuest, "old-owner", false, false)]
        [TestCase(true, MukJumpAccountKind.Apple, "", false, false)]
        [TestCase(true, MukJumpAccountKind.BackendGuest, "", false, false)]
        public void LegacyLogoutMigrationOnlyReconnectsFullyDetachedGuests(bool suppressed,
            MukJumpAccountKind kind, string owner, bool pendingRecovery, bool expected)
        {
            Assert.That(MukJumpAccountRuntime.ShouldMigrateLegacyGuestLogout(suppressed, kind,
                owner, pendingRecovery), Is.EqualTo(expected));
        }

        [TestCase(MukJumpAccountKind.BackendGuest, true)]
        [TestCase(MukJumpAccountKind.LocalGuest, false)]
        [TestCase(MukJumpAccountKind.Google, false)]
        [TestCase(MukJumpAccountKind.Apple, false)]
        public void BackendGuestLogoutPreservesCurrentProfileLocally(
            MukJumpAccountKind kind,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldPreserveBackendGuestBeforeLogout(kind),
                Is.EqualTo(expected));
        }

        [TestCase(MukJumpAccountKind.Google, true)]
        [TestCase(MukJumpAccountKind.LocalGuest, false)]
        [TestCase(MukJumpAccountKind.BackendGuest, false)]
        [TestCase(MukJumpAccountKind.Apple, false)]
        public void GoogleAccountDeletionClearsProviderSession(
            MukJumpAccountKind kind,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .RequiresFederationSignOutBeforeAccountDeletion(kind),
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

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void LinkedLocalProgressCannotBeDiscardedByBlockedLogout(bool dirty, bool expected)
        {
            Assert.That(MukJumpAccountRuntime.ShouldWaitBeforeLogout(
                dirty, false, saveBlocked: true, preserveUnsyncedProfile: true), Is.EqualTo(expected));
        }

        [TestCase(MukJumpAccountKind.Apple)]
        [TestCase(MukJumpAccountKind.Google)]
        public void LogoutKeepsUnsyncedLinkedAccountAndNeverCallsBackend(MukJumpAccountKind kind)
        {
            var account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetProperty(nameof(MukJumpAccountRuntime.AccountKind))
                .SetValue(account, kind);
            SetPrivateField(account, "syncWriteBlocked", true);
            SetPrivateField(account, "profileResolutionPending", false);
            int logoutCalls = 0;
            SetBackendRequestHook(account, "logoutForTests", _ => logoutCalls++);
            string before = growthStore.Json;

            account.Logout();

            Assert.That(logoutCalls, Is.Zero);
            Assert.That(account.IsOnlineAuthenticated, Is.True);
            Assert.That(account.AccountKind, Is.EqualTo(kind));
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(ReadPrivateBool(account, "localLogoutCleanupPending"), Is.False);
            Assert.That(growthStore.Json, Is.EqualTo(before));
            Assert.That(account.StatusMessage, Does.Contain("저장한 뒤 로그아웃"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void QueuedLogoutRunsOnceAfterSaveUnlessAccountSessionChanged(bool sessionChanged)
        {
            var account = CreateReadySaveRuntime();
            typeof(MukJumpAccountRuntime).GetProperty(nameof(MukJumpAccountRuntime.AccountKind))
                .SetValue(account, MukJumpAccountKind.Apple);
            SetPrivateField(account, "profileResolutionPending", false);
            SetPrivateField(account, "saveInFlight", true);
            int calls = 0;
            SetBackendRequestHook(account, "logoutForTests", _ => calls++);
            account.Logout();
            Assert.That(calls, Is.Zero);
            Assert.That(ReadPrivateBool(account, "logoutAfterSaveRequested"), Is.True);
            SetPrivateField(account, "dirty", false);
            SetPrivateField(account, "saveInFlight", false);
            if (sessionChanged)
                SetPrivateField(account, "logoutAfterSaveSession", -1L);
            InvokeLifecycle(account, "ContinueRequestedLogout");
            InvokeLifecycle(account, "ContinueRequestedLogout");
            Assert.That(calls, Is.EqualTo(sessionChanged ? 0 : 1));
            Assert.That(ReadPrivateBool(account, "logoutAfterSaveRequested"), Is.False);
        }

        [Test]
        public void InitialV7ServerSnapshotResolvesPendingAppleProfile()
        {
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            var account = CreateReadySaveRuntime();
            SetPrivateField(account, "profileResolutionPending", true);
            SetPrivateField(account, "replaceLocalFromServerOnNextLoad", true);

            bool applied = InvokePrivate<bool>(account, "TryApplyCloudSnapshot",
                new MukJumpCloudSnapshot
                {
                    bestHeight = 0,
                    growthJson = PermanentGrowthV8Tests.InitialV7ServerJson,
                    revision = 1,
                }, false, false);

            Assert.That(applied, Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.False);
            Assert.That(ReadPrivateBool(account, "syncWriteBlocked"), Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(growthStore.Json), Is.True);
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
        [TestCase(false, MukJumpAccountKind.BackendGuest, true)]
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

        [TestCase("guest-a", "social-b", true)]
        [TestCase(" guest-a ", "guest-a", false)]
        [TestCase("", "social-b", true)]
        [TestCase("guest-a", "", false)]
        public void InterruptedFederationRecoveryRequiresChangedAccount(
            string previousOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .DidAuthorizedTransitionChangeAccount(
                        previousOwner,
                        currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase("account-a", "", MukJumpAccountScopeRelation.Unknown)]
        [TestCase("account-a", "   ", MukJumpAccountScopeRelation.Unknown)]
        [TestCase("account-a", "account-a", MukJumpAccountScopeRelation.Same)]
        [TestCase("account-a", " account-b ", MukJumpAccountScopeRelation.Changed)]
        [TestCase("", "account-a", MukJumpAccountScopeRelation.Changed)]
        public void AuthorizedTransitionKeepsIdentityUnknownSeparate(
            string previousOwner,
            string currentOwner,
            MukJumpAccountScopeRelation expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ClassifyAuthorizedTransitionAccount(
                    previousOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(true, "account-a", "account-a", false)]
        [TestCase(true, "account-a", "account-b", true)]
        [TestCase(true, "", "account-b", true)]
        [TestCase(false, "account-a", "account-b", false)]
        public void ReauthenticationOnlyClearsLocalProgressForAnotherAccount(
            bool replacementRequested,
            string previousOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldReplaceLocalProfileAfterAuthorization(
                        replacementRequested,
                        previousOwner,
                        currentOwner),
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

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void PendingProviderTransitionBypassesLogoutSuppressionWithoutCreatingGuest(
            bool authorizedTransitionPending,
            bool guestUpgradePending,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.HasPendingProviderAuthenticationRecovery(
                    authorizedTransitionPending,
                    guestUpgradePending),
                Is.EqualTo(expected));
            Assert.That(
                MukJumpAccountRuntime.ShouldBlockPendingProviderTransitionAfterTokenFailure(
                    authorizedTransitionPending,
                    guestUpgradePending),
                Is.EqualTo(expected));
        }

        [TestCase(MukJumpAccountKind.LocalGuest, false)]
        [TestCase(MukJumpAccountKind.BackendGuest, false)]
        [TestCase(MukJumpAccountKind.Google, true)]
        [TestCase(MukJumpAccountKind.Apple, true)]
        public void LogoutSuppressionClearsOnlyAfterSocialAccountAuthentication(
            MukJumpAccountKind kind,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldClearAutomaticAuthenticationSuppression(kind),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        public void TemporaryBackendPauseProtectsUnsavedProgressBeforeLogout(
            bool temporaryPause,
            bool dirty,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldBlockLogoutDuringTemporaryBackendPause(
                        temporaryPause,
                        dirty),
                Is.EqualTo(expected));
        }

        [TestCase(true, true, 10f, 10f, true, true)]
        [TestCase(true, true, 10f, 10f, false, false)]
        [TestCase(true, true, 9f, 10f, true, false)]
        [TestCase(true, false, 10f, 10f, true, false)]
        public void TemporaryBackendResumeRequiresSafeAccountState(
            bool temporaryPause,
            bool authenticated,
            float nowRealtime,
            float resumeAtRealtime,
            bool accountStateAllowsResume,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldResumeTemporaryBackendWrites(
                    temporaryPause,
                    authenticated,
                    nowRealtime,
                    resumeAtRealtime,
                    accountStateAllowsResume),
                Is.EqualTo(expected));
        }

        [TestCase(MukJumpAccountKind.LocalGuest, "", false)]
        [TestCase(MukJumpAccountKind.BackendGuest, "", false)]
        [TestCase(MukJumpAccountKind.Google, "", true)]
        [TestCase(MukJumpAccountKind.Apple, "   ", true)]
        [TestCase(MukJumpAccountKind.Apple, "account-a", false)]
        public void LegacySocialProfileRequiresOwnerVerificationBeforeOfflinePlay(
            MukJumpAccountKind kind,
            string storedAccountScope,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.RequiresLegacySocialOwnershipVerification(
                    kind,
                    storedAccountScope),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, MukJumpAccountKind.Apple, "", true)]
        [TestCase(true, false, MukJumpAccountKind.Google, "", true)]
        [TestCase(true, true, MukJumpAccountKind.Apple, "", false)]
        [TestCase(false, false, MukJumpAccountKind.LocalGuest, "", false)]
        [TestCase(false, false, MukJumpAccountKind.Apple, "owner-a", false)]
        public void LegacySocialProfileBlocksBackendFallbackUntilOwnerIsVerified(
            bool backendAvailable,
            bool backendInitialized,
            MukJumpAccountKind kind,
            string storedAccountScope,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldBlockLegacySocialFallback(
                    backendAvailable,
                    backendInitialized,
                    kind,
                    storedAccountScope),
                Is.EqualTo(expected));
        }

        [TestCase("", "owner-a", false)]
        [TestCase("owner-a", "owner-a", false)]
        [TestCase(" owner-a ", "owner-a", false)]
        [TestCase("owner-a", "owner-b", true)]
        [TestCase("owner-a", "", true)]
        [TestCase("", "", true)]
        public void VerifiedTokenCannotReplaceConfirmedAccountOwner(
            string storedOwner,
            string currentOwner,
            bool expectedBlock)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldBlockVerifiedTokenOwnerMismatch(
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expectedBlock));
        }

        [TestCase(MukJumpAccountKind.Apple, MukJumpAccountKind.Apple, "", false)]
        [TestCase(MukJumpAccountKind.Google, MukJumpAccountKind.Google, "", false)]
        [TestCase(MukJumpAccountKind.Apple, MukJumpAccountKind.Google, "", true)]
        [TestCase(MukJumpAccountKind.Google, MukJumpAccountKind.Apple, " ", true)]
        [TestCase(MukJumpAccountKind.Apple, MukJumpAccountKind.Google, "owner-a", false)]
        [TestCase(MukJumpAccountKind.LocalGuest, MukJumpAccountKind.Google, "", false)]
        public void LegacySocialMigrationRequiresSameVerifiedProvider(
            MukJumpAccountKind persistedKind,
            MukJumpAccountKind verifiedKind,
            string storedOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldBlockLegacyProviderMismatch(
                    persistedKind,
                    verifiedKind,
                    storedOwner),
                Is.EqualTo(expected));
        }

        [TestCase("", "owner-a", true)]
        [TestCase("owner-a", "owner-a", true)]
        [TestCase(" owner-a ", "owner-a", true)]
        [TestCase("owner-a", "owner-b", false)]
        [TestCase("owner-a", "", false)]
        public void ConfirmedAccountScopeIsNeverOverwrittenByUnverifiedOwner(
            string storedOwner,
            string currentOwner,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldStoreVerifiedAccountScope(
                    storedOwner,
                    currentOwner),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void CleanupRecoveryKeepsItsTokenCallbackAlive(
            bool deletionPending,
            bool logoutPending,
            bool expectedInvalidate)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldInvalidateTokenLoginForSessionError(
                        deletionPending,
                        logoutPending),
                Is.EqualTo(expectedInvalidate));
        }

        [TestCase(true, true, 10f, 10f, true)]
        [TestCase(true, true, 9.9f, 10f, false)]
        [TestCase(true, false, 12f, 10f, false)]
        [TestCase(false, true, 12f, 10f, false)]
        public void TemporaryBackendPauseResumesOnlyForAuthenticatedSession(
            bool paused,
            bool authenticated,
            float now,
            float resumeAt,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldResumeTemporaryBackendWrites(
                    paused,
                    authenticated,
                    now,
                    resumeAt),
                Is.EqualTo(expected));
        }

        [TestCase(false, false, false, MukJumpAccountKind.LocalGuest, true, true)]
        [TestCase(true, false, false, MukJumpAccountKind.LocalGuest, true, false)]
        [TestCase(false, true, false, MukJumpAccountKind.LocalGuest, true, false)]
        [TestCase(false, false, true, MukJumpAccountKind.LocalGuest, true, false)]
        [TestCase(false, false, false, MukJumpAccountKind.BackendGuest, true, false)]
        [TestCase(false, false, false, MukJumpAccountKind.Google, true, false)]
        [TestCase(false, false, false, MukJumpAccountKind.Apple, true, false)]
        [TestCase(false, false, false, MukJumpAccountKind.LocalGuest, false, false)]
        public void GuestLoginOnlyStartsForFreshLocalAccountWithMissingToken(
            bool profileResolutionPending,
            bool authorizedTransitionPending,
            bool guestUpgradePending,
            MukJumpAccountKind persistedAccountKind,
            bool tokenDefinitivelyUnavailable,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime.ShouldStartGuestLoginAfterTokenFailure(
                    profileResolutionPending,
                    authorizedTransitionPending,
                    guestUpgradePending,
                    persistedAccountKind,
                    tokenDefinitivelyUnavailable),
                Is.EqualTo(expected));
        }

        [TestCase("customSignUp", MukJumpAccountKind.BackendGuest, true)]
        [TestCase(" GOOGLE ", MukJumpAccountKind.Google, true)]
        [TestCase("apple", MukJumpAccountKind.Apple, true)]
        [TestCase("", MukJumpAccountKind.LocalGuest, false)]
        [TestCase("unknown", MukJumpAccountKind.LocalGuest, false)]
        public void BackendSubscriptionTypeDeterminesAccountProvider(
            string subscriptionType,
            MukJumpAccountKind expectedKind,
            bool expectedSuccess)
        {
            bool success = MukJumpAccountRuntime.TryResolveAccountKind(
                subscriptionType,
                out MukJumpAccountKind kind);

            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(kind, Is.EqualTo(expectedKind));
        }

        [TestCase("412", true, true)]
        [TestCase("412", false, false)]
        [TestCase("409", true, false)]
        [TestCase("503", true, false)]
        public void InterruptedGuestUpgradeReauthorizesOnlyAfterGuestRejection(
            string statusCode,
            bool guestUpgradePending,
            bool expected)
        {
            Assert.That(
                MukJumpAccountRuntime
                    .ShouldRecoverGuestUpgradeWithAuthorization(
                        statusCode,
                        guestUpgradePending),
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

        [Test]
        public void PendingCloudLoadBlocksGameStartAndGrowth()
        {
            host = new GameObject("PendingCloudLoadTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetField(
                    "cloudLoadInFlight",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, true);
            ActivateAccountRuntime(account);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var growthObject = new GameObject("PendingCloudLoadGrowthView");
            growthObject.transform.SetParent(host.transform, false);
            var growthView = growthObject.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(growthView.IsOpen, Is.False);
        }

        [Test]
        public void PendingSyncConflictBlocksGameStartAndGrowth()
        {
            host = new GameObject("PendingSyncConflictTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetField(
                    "pendingServerSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, new MukJumpCloudSnapshot
                {
                    bestHeight = 25,
                    growthJson = ValidGrowthJson,
                    revision = 1,
                });
            typeof(MukJumpAccountRuntime).GetProperty(
                    "Phase",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, MukJumpAccountPhase.NeedsSyncChoice);
            ActivateAccountRuntime(account);
            Assert.That(account.HasPendingSyncConflict, Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var growthObject = new GameObject("PendingSyncConflictGrowthView");
            growthObject.transform.SetParent(host.transform, false);
            var growthView = growthObject.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(growthView.IsOpen, Is.False);
        }

        [TestCase(MukJumpAccountPhase.Connecting)]
        [TestCase(MukJumpAccountPhase.NeedsAccountChoice)]
        public void PendingAccountTransitionBlocksGameStartAndGrowth(
            MukJumpAccountPhase phase)
        {
            host = new GameObject("PendingAccountTransitionTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetProperty(
                    "Phase",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, phase);
            ActivateAccountRuntime(account);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            var growthObject = new GameObject("PendingTransitionGrowthView");
            growthObject.transform.SetParent(host.transform, false);
            var growthView = growthObject.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(growthView.IsOpen, Is.False);
        }

        [Test]
        public void LateCloudSnapshotCannotReplaceAnActiveRun()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            host = new GameObject("ActiveRunCloudSnapshotTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            score.ResetOrigin(0f);
            score.SampleWorldHeight(74f);
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            typeof(GameManager).GetProperty(
                    "State",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(manager, GameState.Playing);
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            MethodInfo apply = typeof(MukJumpAccountRuntime).GetMethod(
                "TryApplyCloudSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);

            bool applied = (bool)apply.Invoke(account, new object[]
            {
                new MukJumpCloudSnapshot
                {
                    bestHeight = 10,
                    growthJson = ValidGrowthJson,
                    revision = 1,
                },
                false,
                false,
            });

            Assert.That(applied, Is.False);
            Assert.That(score.Height, Is.EqualTo(74));
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25));
            Assert.That(scoreStore.Best, Is.EqualTo(25));
            Assert.That(account.HasPendingSyncConflict, Is.True);
        }

        [Test]
        public void ServerChoiceKeepsConflictAndRollsBackWhenSettingsSaveFails()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            var settingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settingsStore);
            LobbySettingsProfile.SetBgmVolume(0.25f);
            LobbySettingsProfile.SetSfxVolume(0.4f);
            Assert.That(
                LobbySettingsProfile.TryMarkGameplayTutorialCompleted(),
                Is.True);

            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string localGrowth),
                Is.True);

            host = new GameObject("ServerChoiceSettingsFailureTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            var server = new MukJumpCloudSnapshot
            {
                bestHeight = 80,
                growthJson = ExtendedV8GrowthJson(),
                bgmVolume = 0.9f,
                sfxVolume = 0.8f,
                tutorialVersion = 0,
                revision = 3,
            };
            MethodInfo beginConflict = typeof(MukJumpAccountRuntime)
                .GetMethod(
                    "BeginSyncConflict",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginConflict, Is.Not.Null);
            beginConflict.Invoke(account, new object[] { server, "row-1" });
            settingsStore.ThrowOnSave = true;

            Assert.DoesNotThrow(account.UseServerAfterSyncConflict);

            Assert.That(account.HasPendingSyncConflict, Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(scoreStore.Best, Is.EqualTo(25));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string growthAfterFailure),
                Is.True);
            Assert.That(growthAfterFailure, Is.EqualTo(localGrowth));
            Assert.That(ReadPrivateBool(account, "dirty"), Is.False);
            Assert.That(
                LobbySettingsProfile.BgmVolume,
                Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.SfxVolume,
                Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.GameplayTutorialVersion,
                Is.EqualTo(
                    LobbySettingsProfile.CurrentGameplayTutorialVersion));

            settingsStore.ThrowOnSave = false;
            Assert.DoesNotThrow(account.UseServerAfterSyncConflict);
            Assert.That(account.HasPendingSyncConflict, Is.False);
            Assert.That(account.Phase, Is.EqualTo(
                MukJumpAccountPhase.OnlineReady));
            Assert.That(score.Best, Is.EqualTo(80));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(40));
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
            Assert.That(
                LobbySettingsProfile.BgmVolume,
                Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.SfxVolume,
                Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void DeviceChoicePersistenceFailureKeepsConflictAndCanRetry()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            host = new GameObject("DeviceChoicePersistenceFailureTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            typeof(MukJumpAccountRuntime).GetField(
                    "revision",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, 1L);
            typeof(MukJumpAccountRuntime).GetField(
                    "rowInDate",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, "local-row");
            var server = new MukJumpCloudSnapshot
            {
                bestHeight = 80,
                growthJson = ValidGrowthJson,
                revision = 3,
            };
            MethodInfo beginConflict = typeof(MukJumpAccountRuntime)
                .GetMethod(
                    "BeginSyncConflict",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginConflict, Is.Not.Null);
            beginConflict.Invoke(account, new object[] { server, "server-row" });
            FieldInfo persistenceHook = typeof(MukJumpAccountRuntime)
                .GetField(
                    "keepDeviceConflictPersistenceForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(persistenceHook, Is.Not.Null);
            persistenceHook.SetValue(
                account,
                new System.Action(() =>
                    throw new System.InvalidOperationException(
                        "device choice persist failed")));

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 이 기기 기록 선택 상태를 저장하지 못했습니다: " +
                "device choice persist failed");
            Assert.DoesNotThrow(account.KeepThisDeviceAfterSyncConflict);

            Assert.That(account.HasPendingSyncConflict, Is.True);
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            Assert.That(account.Phase, Is.EqualTo(
                MukJumpAccountPhase.NeedsSyncChoice));
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(scoreStore.Best, Is.EqualTo(25));

            persistenceHook.SetValue(account, null);
            Assert.DoesNotThrow(account.KeepThisDeviceAfterSyncConflict);
            Assert.That(account.HasPendingSyncConflict, Is.False);
            Assert.That(account.Phase, Is.EqualTo(
                MukJumpAccountPhase.OnlineReady));
            Assert.That(score.Best, Is.EqualTo(80));
            Assert.That(
                typeof(MukJumpAccountRuntime).GetField(
                        "dirty",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(account),
                Is.EqualTo(true));
        }

        [Test]
        public void AccountSwitchCannotClearScoreOrGrowthDuringActiveRun()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            host = new GameObject("ActiveRunAccountClearTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            score.ResetOrigin(0f);
            score.SampleWorldHeight(74f);
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out string before),
                Is.True);
            var manager = host.AddComponent<GameManager>();
            ActivateGameManager(manager);
            typeof(GameManager).GetProperty(
                    "State",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(manager, GameState.Playing);
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            MethodInfo clear = typeof(MukJumpAccountRuntime).GetMethod(
                "TryClearAccountProgressForSwitch",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(clear, Is.Not.Null);

            bool cleared = (bool)clear.Invoke(account, null);

            Assert.That(cleared, Is.False);
            Assert.That(score.Height, Is.EqualTo(74));
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(score.RunBestToBeat, Is.EqualTo(25));
            Assert.That(scoreStore.Best, Is.EqualTo(25));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(out string after),
                Is.True);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void AccountSwitchClearFailureRollsBackScoreGrowthAndSettings()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            var settingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settingsStore);
            LobbySettingsProfile.SetBgmVolume(0.25f);
            LobbySettingsProfile.SetSfxVolume(0.4f);
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string growthBefore),
                Is.True);

            host = new GameObject("AccountSwitchClearRollbackTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            MethodInfo clear = typeof(MukJumpAccountRuntime).GetMethod(
                "TryClearAccountProgressForSwitch",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(clear, Is.Not.Null);
            settingsStore.ThrowOnSave = true;

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 동기화 실패 뒤 로컬 설정 복원을 내구 저장하지 " +
                "못했습니다: Injected lobby settings save failure");
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 계정 전환용 로컬 초기화에 실패해 이전 상태로 " +
                "되돌렸습니다: Injected lobby settings save failure " +
                "(이전 상태의 내구 저장은 완료하지 못했습니다)");
            bool cleared = (bool)clear.Invoke(account, null);

            Assert.That(cleared, Is.False);
            Assert.That(score.Best, Is.EqualTo(25));
            Assert.That(scoreStore.Best, Is.EqualTo(25));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string growthAfterFailure),
                Is.True);
            Assert.That(growthAfterFailure, Is.EqualTo(growthBefore));
            Assert.That(
                LobbySettingsProfile.BgmVolume,
                Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.SfxVolume,
                Is.EqualTo(0.4f).Within(0.001f));

            settingsStore.ThrowOnSave = false;
            Assert.That((bool)clear.Invoke(account, null), Is.True);
            Assert.That(score.Best, Is.Zero);
            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(1f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(1f));
        }

        [Test]
        public void LocalGuestRestoreFailureRollsBackWholeCurrentProfile()
        {
            var scoreStore = new MemoryScoreStore { Best = 12 };
            ScoreManager.UseStoreForTests(scoreStore);
            var settingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settingsStore);
            LobbySettingsProfile.SetBgmVolume(0.2f);
            LobbySettingsProfile.SetSfxVolume(0.3f);

            host = new GameObject("LocalGuestRestoreRollbackTest");
            var score = host.AddComponent<ScoreManager>();
            InvokeLifecycle(score, "OnEnable");
            InvokeLifecycle(score, "Awake");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            MethodInfo saveGuest = typeof(MukJumpAccountRuntime).GetMethod(
                "SaveCurrentProfileAsLocalGuest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo restoreGuest = typeof(MukJumpAccountRuntime).GetMethod(
                "RestoreSavedLocalGuestProfile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(saveGuest, Is.Not.Null);
            Assert.That(restoreGuest, Is.Not.Null);
            Assert.That((bool)saveGuest.Invoke(account, null), Is.True);

            Assert.That(
                ScoreManager.TryReplaceVerifiedBestForAccountSwitch(80),
                Is.True);
            LobbySettingsProfile.SetBgmVolume(0.8f);
            LobbySettingsProfile.SetSfxVolume(0.9f);
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string currentGrowth),
                Is.True);
            settingsStore.ThrowOnSave = true;

            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 동기화 실패 뒤 로컬 설정 복원을 내구 저장하지 " +
                "못했습니다: Injected lobby settings save failure");
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 로컬 게스트 기록을 복원하지 못했습니다: " +
                "Injected lobby settings save failure " +
                "(이전 로컬 상태 복원도 완료하지 못했습니다)");
            bool restored = (bool)restoreGuest.Invoke(account, null);

            Assert.That(restored, Is.False);
            Assert.That(score.Best, Is.EqualTo(80));
            Assert.That(scoreStore.Best, Is.EqualTo(80));
            Assert.That(
                PermanentGrowthProfile.TryExportCloudJson(
                    out string growthAfterFailure),
                Is.True);
            Assert.That(growthAfterFailure, Is.EqualTo(currentGrowth));
            Assert.That(
                LobbySettingsProfile.BgmVolume,
                Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.SfxVolume,
                Is.EqualTo(0.9f).Within(0.001f));
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

        [Test]
        public void CloudReadAndSaveCannotOverlapAndSaveResumesAfterRead()
        {
            var account = CreateReadySaveRuntime();
            SetSaveSnapshotHook(account, CreateValidCloudSnapshot(2));
            var responses = new System.Collections.Generic.List<System.Action<BackEnd.BackendReturnObject>>();
            SetBackendRequestHook(account, "getMyDataForTests", callback => responses.Add(callback));
            InvokeLifecycle(account, "LoadCloudSnapshot");
            account.SaveNow();
            Assert.That(responses.Count, Is.EqualTo(1), "조회와 저장이 겹치면 늦은 구 조회가 새 성장을 덮을 수 있습니다.");
            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            responses[0](BackendResult(500));
            account.SaveNow();
            Assert.That(responses.Count, Is.EqualTo(2));
            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.True);
        }

        [Test]
        public void DisableInvalidatesOldSaveReplyAndAllowsNewSave()
        {
            var account = CreateReadySaveRuntime();
            SetSaveSnapshotHook(account, CreateValidCloudSnapshot(2));
            var responses = new System.Collections.Generic.List<System.Action<BackEnd.BackendReturnObject>>();
            SetBackendRequestHook(account, "getMyDataForTests", callback => responses.Add(callback));
            account.SaveNow();
            InvokeLifecycle(account, "OnDisable");
            InvokeLifecycle(account, "OnEnable");
            account.SaveNow();
            Assert.That(responses.Count, Is.EqualTo(2));
            string before = account.StatusMessage;
            responses[0](BackendResult(500));
            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.True);
            Assert.That(account.StatusMessage, Is.EqualTo(before));
            responses[1](BackendResult(500));
            Assert.That(ReadPrivateBool(account, "saveInFlight"), Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
        }

        [Test]
        public void DisabledCloudReadCannotCreateServerDataAndCanResume()
        {
            var account = CreateReadySaveRuntime();
            SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => "owner-a"));
            SetPrivateField(account, "dirty", false);
            SetSaveSnapshotHook(account, CreateValidCloudSnapshot(2));
            var responses = new System.Collections.Generic.List<System.Action<BackEnd.BackendReturnObject>>();
            int inserts = 0;
            SetBackendRequestHook(account, "getMyDataForTests", callback => responses.Add(callback));
            SetBackendRequestHook(account, "insertGameDataForTests", _ => inserts++);
            InvokeLifecycle(account, "LoadCloudSnapshot");
            InvokeLifecycle(account, "OnDisable");
            responses[0](BackendResult(200, "{\"rows\":[]}"));
            Assert.That(inserts, Is.Zero, "종료된 런타임의 응답이 새 서버 쓰기를 시작하면 안 됩니다.");
            InvokeLifecycle(account, "OnEnable");
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            InvokeLifecycle(account, "Update");
            Assert.That(responses.Count, Is.EqualTo(2), "중단된 읽기는 재활성화 뒤 다시 확인해야 합니다.");
            Assert.That(ReadPrivateBool(account, "dirty"), Is.False, "읽기 재시도를 로컬 변경으로 위장하지 않습니다.");
        }

        [TestCase("owner-b")]
        [TestCase("")]
        public void DisabledCloudReadCannotResumeUnderChangedOrMissingOwner(string newOwner)
        {
            var account = CreateReadySaveRuntime();
            string owner = "owner-a";
            SetPrivateField(account, "currentAccountScopeForTests", new System.Func<string>(() => owner));
            var responses = new System.Collections.Generic.List<System.Action<BackEnd.BackendReturnObject>>();
            SetBackendRequestHook(account, "getMyDataForTests", callback => responses.Add(callback));
            InvokeLifecycle(account, "LoadCloudSnapshot");
            owner = newOwner;
            InvokeLifecycle(account, "OnDisable");
            InvokeLifecycle(account, "OnEnable");
            InvokeLifecycle(account, "Update");
            account.SaveNow();
            Assert.That(responses.Count, Is.EqualTo(1), "이전 계정의 로컬 성장을 다른 계정으로 읽거나 쓰면 안 됩니다.");
            Assert.That(account.BlocksGameplayForAccountSync, Is.True);
            Assert.That(ReadPrivateBool(account, "providerResolutionBlocked"), Is.True);
            Assert.That(account.IsOnlineAuthenticated, Is.False);
            Assert.That(ReadPrivateBool(account, "dirty"), Is.True);
        }

        MukJumpAccountRuntime CreateReadySaveRuntime()
        {
            host = new GameObject("AccountSaveFailureTest");
            var account = host.AddComponent<MukJumpAccountRuntime>();
            ActivateAccountRuntime(account);
            typeof(MukJumpAccountRuntime).GetProperty(
                    nameof(MukJumpAccountRuntime.IsOnlineAuthenticated),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, true);
            typeof(MukJumpAccountRuntime).GetProperty(
                    nameof(MukJumpAccountRuntime.Phase),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(account, MukJumpAccountPhase.OnlineReady);
            MukJumpBackendSettings backendSettings =
                MukJumpBackendSettings.Load();
            Assert.That(backendSettings, Is.Not.Null);
            typeof(MukJumpAccountRuntime).GetField(
                    "settings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, backendSettings);
            typeof(MukJumpAccountRuntime).GetField(
                    "rowInDate",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, "row-1");
            typeof(MukJumpAccountRuntime).GetField(
                    "revision",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, 1L);
            typeof(MukJumpAccountRuntime).GetField(
                    "dirty",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(account, true);
            return account;
        }

        static MukJumpCloudSnapshot CreateValidCloudSnapshot(long revision)
        {
            return new MukJumpCloudSnapshot
            {
                bestHeight = 25,
                growthJson = ValidGrowthJson,
                revision = revision,
                lastOperationId = "test-operation",
            };
        }

        static void SetSaveSnapshotHook(
            MukJumpAccountRuntime account,
            MukJumpCloudSnapshot snapshot)
        {
            typeof(MukJumpAccountRuntime).GetField(
                    "captureNextSnapshotForTests",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    account,
                    new System.Func<MukJumpCloudSnapshot>(() => snapshot));
        }

        static void SetBackendRequestHook(
            MukJumpAccountRuntime account,
            string fieldName,
            System.Action<System.Action<BackEnd.BackendReturnObject>> hook)
        {
            FieldInfo field = typeof(MukJumpAccountRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(account, hook);
        }

        static bool ReadPrivateBool(
            MukJumpAccountRuntime account,
            string fieldName)
        {
            FieldInfo field = typeof(MukJumpAccountRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (bool)field.GetValue(account);
        }

        static void SetPrivateField<T>(
            MukJumpAccountRuntime account,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(MukJumpAccountRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(account, value);
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
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                $"{component.GetType().Name}.{methodName}을 찾을 수 없다.");
            method.Invoke(component, arguments);
        }

        static T InvokePrivate<T>(
            MonoBehaviour component,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                $"{component.GetType().Name}.{methodName}을 찾을 수 없다.");
            return (T)method.Invoke(component, arguments);
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
