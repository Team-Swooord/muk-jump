using System;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class AppsInTossReleaseTests
    {
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
        public void TossLeaderboardKeepsPendingScoreWhenSdkReturnsNoResponse()
        {
            Assert.That(
                AppsInTossGameCenterRuntime.HasSubmissionResponse(null),
                Is.False);
            Assert.That(
                AppsInTossGameCenterRuntime.HasSubmissionResponse(
                    new AppsInToss
                        .SubmitGameCenterLeaderBoardScoreResponse()),
                Is.True);
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
            string publicDirectory = Path.Combine(root, "public");
            Directory.CreateDirectory(publicDirectory);
            string indexPath = Path.Combine(root, "index.html");
            try
            {
                File.WriteAllText(
                    indexPath,
                    "<script>if ('false' !== 'true') return;</script>");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        publicDirectory),
                    Is.Empty);

                File.WriteAllText(
                    indexPath,
                    "<script>if ('true' !== 'true') return;</script>");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        publicDirectory),
                    Has.Some.Contains("vConsole"));

                File.WriteAllText(indexPath, "%AIT_ENABLE_DEBUG_CONSOLE%");
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        publicDirectory),
                    Has.Some.Contains("치환"));
                Assert.That(
                    MukJumpAppsInTossSetup.CollectBuiltPayloadIssues(
                        publicDirectory,
                        allowPackagingTokens: true),
                    Is.Empty,
                    "용량 측정용 WebGL 원본은 패키징 전 토큰을 유지합니다.");
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
    }
}
