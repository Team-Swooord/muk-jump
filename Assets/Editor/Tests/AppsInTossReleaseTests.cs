using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class AppsInTossReleaseTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TossSettingsIdIsOnlyAvailableWhileIdentityIsVerified(bool verified)
        {
            var type = typeof(AppsInTossIdentityPolicy);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var valid = type.GetField("hasVerifiedIdentity", flags);
            var hash = type.GetField("verifiedUserHash", flags);
            var owner = type.GetField("verifiedOwnerToken", flags);
            object oldValid = valid.GetValue(null), oldHash = hash.GetValue(null), oldOwner = owner.GetValue(null);
            try
            {
                valid.SetValue(null, verified);
                hash.SetValue(null, "test-display-hash");
                Assert.That(AppsInTossIdentityPolicy.VerifiedUserHash, Is.EqualTo(verified ? "test-display-hash" : ""));
                AppsInTossIdentityPolicy.MarkIdentityUnverified();
                Assert.That(AppsInTossIdentityPolicy.VerifiedUserHash, Is.Empty);
                Assert.That(hash.GetValue(null), Is.EqualTo(string.Empty));
            }
            finally { valid.SetValue(null, oldValid); hash.SetValue(null, oldHash); owner.SetValue(null, oldOwner); }
        }

        [Test]
        public void BackendNativePluginsAreExcludedFromWebGL()
        {
            Assert.That(
                MukJumpPlatformPluginFilter
                    .CollectWebGLCompatibleBackendPlugins(),
                Is.Empty,
                "뒤끝 모바일 DLL이 WebGL에 포함되면 토스 번들 용량과 컴파일이 깨집니다.");
        }

        [Test]
        public void BackendToolkitAssemblyIsExcludedFromWebGL()
        {
            Assert.That(
                MukJumpAppsInTossSetup.IsBackendToolkitExcludedFromWebGL(),
                Is.True,
                "뒤끝 Toolkit C# 코드가 WebGL에 포함되면 컴파일이 깨집니다.");
        }

        [Test]
        public void TossLeaderboardOnlyAcceptsCompletedVerifiedResult()
        {
            var completed = new GameOverResult(
                40,
                80,
                false,
                0,
                0,
                rewardsAllowed: true,
                growthRewardSaved: true,
                recordSaved: true);
            var debugRun = new GameOverResult(
                999,
                999,
                true,
                0,
                0,
                rewardsAllowed: false);
            var pendingWrite = new GameOverResult(
                100,
                100,
                true,
                0,
                0,
                rewardsAllowed: true,
                growthRewardSaved: true,
                recordSaved: false,
                persistenceState:
                    GameOverPersistenceState.RecordWritePending);

            Assert.That(
                AppsInTossGameCenterRuntime.IsEligible(completed),
                Is.True);
            Assert.That(
                AppsInTossGameCenterRuntime.IsEligible(debugRun),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime.IsEligible(pendingWrite),
                Is.False);
        }

        [TestCase(0, 80, 80)]
        [TestCase(120, 80, 120)]
        [TestCase(80, 120, 120)]
        [TestCase(-1, -10, 0)]
        public void TossLeaderboardRetryKeepsHighestPendingScore(
            int storedBest,
            int candidateBest,
            int expected)
        {
            Assert.That(
                AppsInTossGameCenterRuntime.ResolvePendingBestHeight(
                    storedBest,
                    candidateBest),
                Is.EqualTo(expected));
        }

        [TestCase(80, 80, true)]
        [TestCase(40, 80, true)]
        [TestCase(120, 80, false)]
        public void TossLeaderboardClearsOnlySubmittedOrOlderPendingScore(
            int storedBest,
            int submittedBest,
            bool expected)
        {
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldClearPendingBestHeight(
                        storedBest,
                        submittedBest),
                Is.EqualTo(expected));
        }

        [Test]
        public void TossLeaderboardClearsPendingOnlyForExplicitSuccess()
        {
            Assert.That(
                AppsInTossGameCenterRuntime.IsSuccessfulSubmission(null),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime.IsSuccessfulSubmission(
                    new AppsInToss.SubmitGameCenterLeaderBoardScoreResponse()),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime.IsSuccessfulSubmission(
                    new AppsInToss.SubmitGameCenterLeaderBoardScoreResponse
                    {
                        StatusCode = "LeaderBoard not found",
                    }),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime.IsSuccessfulSubmission(
                    new AppsInToss.SubmitGameCenterLeaderBoardScoreResponse
                    {
                        StatusCode = "success",
                    }),
                Is.True);
        }

        [TestCase(
            "{\"type\":\"HASH\",\"hash\":\"user-123\"}",
            true,
            "user-123")]
        [TestCase(
            "{\"type\":\"hash\",\"hash\":\" spaced \"}",
            true,
            "spaced")]
        [TestCase("future-direct-hash", false, "")]
        [TestCase("UNSUPPORTED_VERSION", false, "")]
        [TestCase("true", false, "")]
        [TestCase("123", false, "")]
        [TestCase("ERROR", false, "")]
        [TestCase("INVALID_CATEGORY", false, "")]
        [TestCase("NOT_AVAILABLE", false, "")]
        [TestCase("", false, "")]
        [TestCase("{\"type\":\"HASH\"}", false, "")]
        [TestCase(
            "{\"type\":\"ERROR\",\"hash\":\"must-not-pass\"}",
            false,
            "")]
        public void TossUserKeyParserAcceptsOnlyVerifiedHashResults(
            string raw,
            bool expectedSuccess,
            string expectedHash)
        {
            bool success = AppsInTossIdentityPolicy.TryExtractUserHash(
                raw,
                out string userHash);

            Assert.That(success, Is.EqualTo(expectedSuccess));
            Assert.That(userHash, Is.EqualTo(expectedHash));
        }

        [TestCase("SUCCESS", true)]
        [TestCase(" success ", true)]
        [TestCase("PROFILE_NOT_FOUND", false)]
        [TestCase("ERROR", false)]
        [TestCase(null, false)]
        public void TossGameCenterProfileMustReturnExplicitSuccess(
            string statusCode,
            bool expected)
        {
            Assert.That(
                AppsInTossIdentityPolicy.IsGameCenterProfileReady(
                    statusCode),
                Is.EqualTo(expected));
        }

        [TestCase(
            "",
            "current-user",
            AppsInTossOwnerBindingDecision.BindNew)]
        [TestCase(
            "current-user",
            "current-user",
            AppsInTossOwnerBindingDecision.AcceptExisting)]
        [TestCase(
            "previous-user",
            "current-user",
            AppsInTossOwnerBindingDecision.RejectOwnerMismatch)]
        [TestCase(
            "",
            "ERROR",
            AppsInTossOwnerBindingDecision.RejectInvalid)]
        public void TossLocalRecordsNeverSilentlySwitchOwners(
            string storedHash,
            string incomingHash,
            AppsInTossOwnerBindingDecision expected)
        {
            Assert.That(
                AppsInTossIdentityPolicy.ResolveOwnerBinding(
                    storedHash,
                    incomingHash),
                Is.EqualTo(expected));
        }

        [Test]
        public void TossPendingLeaderboardClearsOnlyForSameVerifiedOwner()
        {
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldClearPendingBestHeightForOwner(
                        "owner-a",
                        "owner-a",
                        "owner-a",
                        70,
                        80),
                Is.True);
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldClearPendingBestHeightForOwner(
                        "owner-a",
                        "owner-a",
                        "owner-b",
                        70,
                        80),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldClearPendingBestHeightForOwner(
                        string.Empty,
                        "owner-a",
                        "owner-a",
                        70,
                        80),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldClearPendingBestHeightForOwner(
                        "owner-a",
                        "owner-a",
                        "owner-a",
                        90,
                        80),
                Is.False);
        }

        [Test]
        public void TossNewRunNeverMergesUnownedOrForeignPendingScore()
        {
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ResolvePendingBestHeightForOwner(
                        999,
                        80,
                        string.Empty,
                        "owner-a"),
                Is.EqualTo(80));
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ResolvePendingBestHeightForOwner(
                        999,
                        80,
                        "owner-b",
                        "owner-a"),
                Is.EqualTo(80));
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ResolvePendingBestHeightForOwner(
                        90,
                        80,
                        "owner-a",
                        "owner-a"),
                Is.EqualTo(90));
        }

        [Test]
        public void TossChangedVerifiedOwnerCanReplaceForeignPendingSlot()
        {
            Assert.That(
                AppsInTossGameCenterRuntime.ResolvePendingBestHeightForOwner(
                    100,
                    200,
                    "owner-a",
                    "owner-b"),
                Is.EqualTo(200));
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldRetryCurrentOwnerAfterAttempt(
                        "owner-a",
                        "owner-b"),
                Is.True,
                "이전 소유자의 늦은 응답 뒤에는 현재 소유자 pending을 제출해야 합니다.");
            Assert.That(
                AppsInTossGameCenterRuntime
                    .ShouldRetryCurrentOwnerAfterAttempt(
                        "owner-b",
                        "owner-b"),
                Is.False,
                "같은 소유자의 실패를 즉시 무한 재귀 재시도하면 안 됩니다.");
        }

        [Test]
        public void TossPendingOwnerTokenIsStableAndDoesNotExposeRawHash()
        {
            const string rawHash = "sensitive-user-hash";
            string first = AppsInTossIdentityPolicy.CreateOwnerToken(rawHash);
            string second = AppsInTossIdentityPolicy.CreateOwnerToken(rawHash);

            Assert.That(first, Is.Not.Empty);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Not.Contain(rawHash));
        }

        [Test]
        public void PayloadSizeCalculatorCountsNestedFilesExactly()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ait-size-" + Guid.NewGuid().ToString("N"));
            string nested = Path.Combine(root, "Build");
            Directory.CreateDirectory(nested);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "index.html"), new byte[7]);
                File.WriteAllBytes(Path.Combine(nested, "game.wasm"), new byte[11]);

                Assert.That(
                    MukJumpAppsInTossSetup.CalculateDirectorySize(root),
                    Is.EqualTo(18L));
                Assert.That(
                    MukJumpAppsInTossSetup.FormatBytes(1024L * 1024L),
                    Is.EqualTo("1.00 MB"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReleasePayloadRejectsEnabledOrUnresolvedDebugConsole()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ait-debug-" + Guid.NewGuid().ToString("N"));
            string indexPath = Path.Combine(root, "index.html");
            try
            {
                WriteValidReleasePayload(
                    root,
                    "<script>if ('false' !== 'true') return;</script>");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        root),
                    Is.Empty);

                WriteValidReleasePayload(
                    root,
                    "<script>if ('true' !== 'true') return;</script>");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        root),
                    Has.Some.Contains("vConsole"));

                WriteValidReleasePayload(
                    root,
                    "%AIT_ENABLE_DEBUG_CONSOLE%<script src=\"token.js\"></script>");
                File.WriteAllText(
                    Path.Combine(root, "token.js"),
                    "const unresolved = '%UNITY_WEBGL_LOADER_URL%';");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        root),
                    Has.Some.Contains("치환"));
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        root,
                        allowPackagingTokens: true),
                    Is.Empty,
                    "용량 측정용 WebGL 원본은 패키징 전 토큰을 유지합니다.");

                File.WriteAllText(
                    indexPath,
                    MukJumpAppsInTossSetup.CanonicalFullscreenStyle +
                    MukJumpAppsInTossSetup.CanonicalFullscreenStyle);
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(root),
                    Has.Some.Contains("정확히 1개"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void FullscreenTemplateTransformIsIdempotentAndFailClosed()
        {
            const string fresh =
                "<html><head>\n<!-- USER_HEAD_START -->\n" +
                "<!-- USER_HEAD_END -->\n</head></html>";
            Assert.That(
                MukJumpAppsInTossSetup.TryApplyFullscreenTemplate(
                    fresh,
                    out string once,
                    out string error),
                Is.True,
                error);
            Assert.That(
                CountOccurrences(
                    once,
                    "id=\"" +
                    MukJumpAppsInTossSetup.FullscreenStyleId +
                    "\""),
                Is.EqualTo(1));
            Assert.That(once, Does.Contain("height: 100dvh;"));
            Assert.That(once, Does.Contain("#EAE3D2"));

            Assert.That(
                MukJumpAppsInTossSetup.TryApplyFullscreenTemplate(
                    once,
                    out string twice,
                    out error),
                Is.True,
                error);
            Assert.That(twice, Is.EqualTo(once));

            string legacy = fresh.Replace(
                "<!-- USER_HEAD_END -->",
                "<style class=\"old\" id=\"mukjump-fullscreen-webgl\">" +
                "body{background:red}</style>\n<!-- USER_HEAD_END -->");
            Assert.That(
                MukJumpAppsInTossSetup.TryApplyFullscreenTemplate(
                    legacy,
                    out string replaced,
                    out error),
                Is.True,
                error);
            Assert.That(replaced, Does.Not.Contain("background:red"));
            Assert.That(
                CountOccurrences(replaced, "id=\"mukjump-fullscreen-webgl\""),
                Is.EqualTo(1));

            Assert.That(
                MukJumpAppsInTossSetup.TryApplyFullscreenTemplate(
                    "<html><head></head></html>",
                    out _,
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("USER_HEAD_END"));
        }

        [Test]
        public void FinalPayloadNeverFallsBackToParentSourceIndex()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ait-dist-" + Guid.NewGuid().ToString("N"));
            string dist = Path.Combine(root, "dist", "web");
            Directory.CreateDirectory(dist);
            try
            {
                WriteValidReleasePayload(root);
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(dist),
                    Has.Some.Contains("index.html"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void AitArtifactMustBeNewOrContentChangedByCurrentBuild()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ait-artifact-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string existing = Path.Combine(root, "muk-jump.ait");
            try
            {
                File.WriteAllText(existing, "stale");
                var before =
                    MukJumpAppsInTossSetup.CaptureAitArtifactHashes(root);
                Assert.That(
                    MukJumpAppsInTossSetup.FindFreshAitPackagePath(
                        root,
                        before),
                    Is.Null,
                    "변하지 않은 이전 .ait를 현재 빌드 성공으로 인정하면 안 됩니다.");

                File.WriteAllText(existing, "rebuilt");
                Assert.That(
                    MukJumpAppsInTossSetup.FindFreshAitPackagePath(
                        root,
                        before),
                    Is.EqualTo(Path.GetFullPath(existing)));

                var afterRebuild =
                    MukJumpAppsInTossSetup.CaptureAitArtifactHashes(root);
                string added = Path.Combine(root, "new-output.ait");
                File.WriteAllText(added, "new");
                Assert.That(
                    MukJumpAppsInTossSetup.FindFreshAitPackagePath(
                        root,
                        afterRebuild),
                    Is.EqualTo(Path.GetFullPath(added)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void AitArtifactRequiresExactRuntimeVersionMetadata()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ait-metadata-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string valid = Path.Combine(root, "valid.ait");
                File.WriteAllBytes(
                    valid,
                    BuildAitFixture(
                        MukJumpAppsInTossSetup.ExpectedAitRuntimeVersion));
                Assert.That(
                    MukJumpAppsInTossSetup.TryReadAitRuntimeVersion(
                        valid,
                        out string runtimeVersion,
                        out string error),
                    Is.True,
                    error);
                Assert.That(
                    runtimeVersion,
                    Is.EqualTo(
                        MukJumpAppsInTossSetup.ExpectedAitRuntimeVersion));

                string missing = Path.Combine(root, "missing.ait");
                File.WriteAllBytes(missing, BuildAitFixture(null));
                Assert.That(
                    MukJumpAppsInTossSetup.TryReadAitRuntimeVersion(
                        missing,
                        out _,
                        out error),
                    Is.False);
                Assert.That(error, Does.Contain("field 3"));

                string wrong = Path.Combine(root, "wrong.ait");
                File.WriteAllBytes(wrong, BuildAitFixture("other"));
                Assert.That(
                    MukJumpAppsInTossSetup.TryReadAitRuntimeVersion(
                        wrong,
                        out runtimeVersion,
                        out error),
                    Is.True,
                    error);
                Assert.That(
                    runtimeVersion,
                    Is.Not.EqualTo(
                        MukJumpAppsInTossSetup.ExpectedAitRuntimeVersion),
                    "RunBuild의 exact gate가 다른 런타임을 거절해야 합니다.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void TossCoreConfigurationMatchesConsoleIdentity()
        {
            AppsInToss.AITEditorScriptObject config =
                AppsInToss.UnityUtil.GetEditorConf();
            Assert.That(config, Is.Not.Null);
            Assert.That(config.appName, Is.EqualTo("muk-jump"));
            Assert.That(config.displayName, Is.EqualTo("먹점프"));
            Assert.That(config.primaryColor, Is.EqualTo("#AE1C3C"));
            Assert.That(config.productionProfile.enableMockBridge, Is.False);
            Assert.That(config.productionProfile.enableDebugConsole, Is.False);
            Assert.That(config.productionProfile.developmentBuild, Is.False);
        }

        [Test]
        public void DevelopmentBundleAlwaysUsesTossMockAdIds()
        {
            AppsInTossAdSettings settings =
                ScriptableObject.CreateInstance<AppsInTossAdSettings>();
            try
            {
                settings.Configure(
                    "operation-rewarded",
                    "operation-interstitial",
                    "operation-banner");

                Assert.That(
                    AppsInTossAdRuntime.ResolveRewardedId(settings, true),
                    Is.EqualTo(AppsInTossAdRuntime.TestRewardedId));
                Assert.That(
                    AppsInTossAdRuntime.ResolveBannerId(settings, true),
                    Is.EqualTo(AppsInTossAdRuntime.TestBannerId));
                Assert.That(
                    AppsInTossAdRuntime.ResolveInterstitialId(settings, true),
                    Is.Empty,
                    "1.0 개발 번들에서 전면 광고를 예열하면 안 됩니다.");
                Assert.That(
                    AppsInTossAdRuntime.ResolveRewardedId(settings, false),
                    Is.EqualTo("operation-rewarded"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TossReleaseRejectsInterstitialAdGroupForVersionOnePolicy()
        {
            AppsInTossAdSettings settings =
                ScriptableObject.CreateInstance<AppsInTossAdSettings>();
            try
            {
                settings.Configure(
                    "operation-rewarded",
                    "operation-interstitial",
                    "operation-banner");

                Assert.That(
                    MukJumpAppsInTossSetup.CollectAdPolicyIssues(settings),
                    Has.Some.Contains("전면 광고"));

                settings.Configure(
                    "operation-rewarded",
                    string.Empty,
                    "operation-banner");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectAdPolicyIssues(settings),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TossReleaseRejectsOfficialTestAdGroupIds()
        {
            AppsInTossAdSettings settings =
                ScriptableObject.CreateInstance<AppsInTossAdSettings>();
            try
            {
                settings.Configure(
                    AppsInTossAdRuntime.TestRewardedId,
                    string.Empty,
                    AppsInTossAdRuntime.TestBannerId);

                var issues =
                    MukJumpAppsInTossSetup.CollectAdPolicyIssues(settings);
                Assert.That(issues, Has.Count.EqualTo(2));
                Assert.That(issues, Has.Some.Contains("보상형"));
                Assert.That(issues, Has.Some.Contains("배너"));

                settings.Configure(
                    "operation-rewarded",
                    string.Empty,
                    "operation-banner");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectAdPolicyIssues(settings),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TossBannerShowFailureCanRetryWithoutFalseVisibleState()
        {
            LobbyAdLayout.ClearTopInset();
            var host = new GameObject("TossBannerShowFailure");
            host.SetActive(false);
            AppsInTossAdRuntime runtime =
                host.AddComponent<AppsInTossAdRuntime>();
            try
            {
                SetPrivateField(runtime, "bannerId", "test-banner");
                SetPrivateField(
                    runtime,
                    "showBannerForTests",
                    new Action<string>(_ =>
                        throw new InvalidOperationException("show failed")));
                SetPrivateField(
                    runtime,
                    "hideBannerForTests",
                    new Action(() => { }));
                LogAssert.Expect(
                    LogType.Warning,
                    "먹점프 토스 배너 표시 실패: show failed");

                Assert.That(
                    InvokePrivate<bool>(runtime, "TryShowBanner"),
                    Is.False);
                InvokePrivate(runtime, "ScheduleBannerRetry");

                Assert.That(
                    GetPrivateField<bool>(runtime, "bannerVisible"),
                    Is.False);
                Assert.That(LobbyAdLayout.IsBannerVisible, Is.False);
                Assert.That(
                    GetPrivateField<float>(runtime, "nextBannerRetryAt"),
                    Is.GreaterThan(Time.realtimeSinceStartup));
            }
            finally
            {
                LobbyAdLayout.ClearTopInset();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TossBannerHideFailureCannotSkipRetryStateOrDisableCleanup()
        {
            LobbyAdLayout.ClearTopInset();
            var host = new GameObject("TossBannerHideFailure");
            host.SetActive(false);
            AppsInTossAdRuntime runtime =
                host.AddComponent<AppsInTossAdRuntime>();
            try
            {
                SetPrivateField(runtime, "runtimeActive", true);
                SetPrivateField(runtime, "bannerVisible", true);
                SetPrivateField(
                    runtime,
                    "hideBannerForTests",
                    new Action(() =>
                        throw new InvalidOperationException("hide failed")));
                LobbyAdLayout.ReserveDefaultTopInset();
                LobbyAdLayout.MarkBannerVisible();
                LogAssert.Expect(
                    LogType.Warning,
                    "먹점프 토스 배너 숨김 실패: hide failed");

                InvokePrivate(runtime, "ScheduleBannerRetry");

                Assert.That(
                    GetPrivateField<bool>(runtime, "bannerVisible"),
                    Is.False);
                Assert.That(LobbyAdLayout.IsBannerVisible, Is.False);
                Assert.That(
                    GetPrivateField<float>(runtime, "nextBannerRetryAt"),
                    Is.GreaterThan(Time.realtimeSinceStartup));

                LogAssert.Expect(
                    LogType.Warning,
                    "먹점프 토스 배너 숨김 실패: hide failed");
                InvokePrivate(runtime, "OnDisable");

                Assert.That(
                    GetPrivateField<bool>(runtime, "runtimeActive"),
                    Is.False);
                Assert.That(LobbyAdLayout.HasReservedTopInset, Is.False);
                Assert.That(MonetizationAds.HasProvider, Is.False);
            }
            finally
            {
                MonetizationAds.ResetProvider();
                LobbyAdLayout.ClearTopInset();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TossBannerLateErrorAfterDisableDoesNotRestartRetry()
        {
            var host = new GameObject("TossBannerLateError");
            host.SetActive(false);
            AppsInTossAdRuntime runtime =
                host.AddComponent<AppsInTossAdRuntime>();
            try
            {
                SetPrivateField(runtime, "runtimeActive", false);
                SetPrivateField(runtime, "nextBannerRetryAt", 42f);

                InvokePrivate(runtime, "HandleBannerError", "late");

                Assert.That(
                    GetPrivateField<float>(runtime, "nextBannerRetryAt"),
                    Is.EqualTo(42f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static void WriteValidReleasePayload(
            string root,
            string extraHtml = "")
        {
            Directory.CreateDirectory(root);
            string runtime = Path.Combine(root, "Runtime");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(
                Path.Combine(root, "index.html"),
                "<html><head>" +
                MukJumpAppsInTossSetup.CanonicalFullscreenStyle +
                "</head><body>" + extraHtml + "</body></html>");
            File.WriteAllText(
                Path.Combine(runtime, "appsintoss-unity-bridge.js"),
                "var AIT_BUILD_IS_PRODUCTION = 'true';");
        }

        static void SetPrivateField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        static void InvokePrivate(
            object target,
            string name,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }

        static T InvokePrivate<T>(
            object target,
            string name,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return (T)method.Invoke(target, arguments);
        }

        static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(
                       value,
                       index,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        static byte[] BuildAitFixture(string runtimeVersion)
        {
            var metadata = new List<byte>();
            if (runtimeVersion != null)
            {
                byte[] runtime = Encoding.UTF8.GetBytes(runtimeVersion);
                metadata.Add(0x1a);
                WriteVarint(metadata, (ulong)runtime.Length);
                metadata.AddRange(runtime);
            }

            var bundle = new List<byte> { 0x22 };
            WriteVarint(bundle, (ulong)metadata.Count);
            bundle.AddRange(metadata);

            var result = new List<byte>();
            result.AddRange(Encoding.ASCII.GetBytes("AITBUNDL"));
            result.AddRange(new byte[] { 0, 0, 0, 1 });
            ulong length = (ulong)bundle.Count;
            for (int shift = 56; shift >= 0; shift -= 8)
                result.Add((byte)(length >> shift));
            result.AddRange(bundle);
            return result.ToArray();
        }

        static void WriteVarint(List<byte> target, ulong value)
        {
            while (value >= 0x80)
            {
                target.Add((byte)((value & 0x7f) | 0x80));
                value >>= 7;
            }
            target.Add((byte)value);
        }
    }
}
