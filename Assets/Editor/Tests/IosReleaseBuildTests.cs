using System;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace MukJump.EditorTests
{
    public sealed class IosReleaseBuildTests
    {
        [TestCase("3", "4")]
        [TestCase("0", "1")]
        [TestCase("invalid", "1")]
        public void TestFlightQaBuildNumberAdvancesSafely(
            string current,
            string expected)
        {
            Assert.That(
                MukJumpStoreBuild.NextIosBuildNumber(current),
                Is.EqualTo(expected));
        }

        [Test]
        public void TestFlightQaAdvancesExplicitConfiguredBuildNumber()
        {
            Assert.That(
                MukJumpStoreBuild.ResolveNextIosTestFlightBuildNumber(
                    "11",
                    "7"),
                Is.EqualTo("12"),
                "환경변수로 확정된 번호를 적용한 뒤 다음 TestFlight 번호를 계산해야 합니다.");
        }

        [Test]
        public void StoreConfigurationPreservesIndependentBuildNumbersByDefault()
        {
            Assert.That(
                MukJumpStoreBuild.ResolveIosBuildNumber(null, "7"),
                Is.EqualTo("7"),
                "검증 메뉴가 기존 TestFlight 빌드 번호를 1로 되돌리면 안 됩니다.");
            Assert.That(
                MukJumpStoreBuild.ResolveAndroidVersionCode(null, 23),
                Is.EqualTo(23),
                "iOS 검증이 Android versionCode를 덮어쓰면 안 됩니다.");
        }

        [Test]
        public void StoreConfigurationUsesPlatformSpecificExplicitBuildNumbers()
        {
            Assert.That(
                MukJumpStoreBuild.ResolveIosBuildNumber(" 11 ", "7"),
                Is.EqualTo("11"));
            Assert.That(
                MukJumpStoreBuild.ResolveAndroidVersionCode(" 31 ", 23),
                Is.EqualTo(31));
        }

        [Test]
        public void StoreConfigurationPreservesCurrentMarketingVersion()
        {
            Assert.That(
                MukJumpStoreBuild.ResolveAppVersion(null, "1.0.1"),
                Is.EqualTo("1.0.1"));
            Assert.That(
                MukJumpStoreBuild.ResolveAppVersion(" 1.1.0 ", "1.0.1"),
                Is.EqualTo("1.1.0"));
            Assert.That(
                MukJumpStoreBuild.ResolveAppVersion(null, null),
                Is.EqualTo(MukJumpStoreBuild.DefaultVersion));
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ResolveAppVersion(null, "invalid"));
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ResolveAppVersion("invalid", "1.0.1"));
        }

        [Test]
        public void ReleaseEnvironmentRequiresPinnedUnityAndNewInputSystemOnly()
        {
            const string validSettings = "PlayerSettings:\n  activeInputHandler: 1\n";
            Assert.That(
                MukJumpStoreBuild.CollectReleaseEnvironmentIssues(
                    MukJumpStoreBuild.ExpectedUnityVersion,
                    validSettings),
                Is.Empty);

            foreach (string invalid in new[]
                     {
                         "activeInputHandler: 0\n",
                         "activeInputHandler: 2\n",
                         "activeInputHandler: 1\nactiveInputHandler: 1\n",
                         string.Empty,
                     })
                Assert.That(
                    MukJumpStoreBuild.UsesExclusiveInputSystem(invalid),
                    Is.False);

            Assert.That(
                MukJumpStoreBuild.CollectReleaseEnvironmentIssues(
                    "6000.5.10f1",
                    validSettings),
                Has.Some.Contains("6000.5.9f1"));
        }

        [TestCase("0")]
        [TestCase("invalid")]
        public void StoreConfigurationRejectsInvalidExplicitIosBuildNumber(
            string requested)
        {
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ResolveIosBuildNumber(requested, "7"));
        }

        [TestCase("0")]
        [TestCase("invalid")]
        public void StoreConfigurationRejectsInvalidExplicitAndroidVersionCode(
            string requested)
        {
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ResolveAndroidVersionCode(requested, 23));
        }

        [Test]
        public void TestFlightQaUsesSeparateOutputAndForcedAdsDefine()
        {
            Assert.That(
                MukJumpStoreBuild.IosTestFlightQaOutputPath,
                Is.EqualTo("output/ios/MukJumpTestFlightQA"));
            Assert.That(
                MukJumpStoreBuild.TestFlightQaAdsDefine,
                Is.EqualTo("MUKJUMP_TEST_ADS"));
        }

        [Test]
        public void MainSceneSourceStampRejectsMissingOrStaleGeneration()
        {
            string current = MukJumpSceneBuilder.CurrentSceneSourceStampName();

            Assert.That(current, Does.StartWith(
                MukJumpSceneBuilder.SceneSourceStampPrefix));
            Assert.That(
                MukJumpSceneBuilder.SceneTextContainsCurrentSourceStamp(
                    "m_Name: @MukJumpSceneSource_stale"),
                Is.False);
            Assert.That(
                MukJumpSceneBuilder.SceneTextContainsCurrentSourceStamp(
                    "--- !u!1\nm_Name: '" + current + "'\n"),
                Is.True);
            Assert.That(
                MukJumpSceneBuilder.SceneTextContainsCurrentSourceStamp(
                    "m_Name: '" + current + "-suffix'\n"),
                Is.False,
                "부분 문자열이나 다른 세대 표식은 통과시키면 안 됩니다.");
            Assert.That(
                MukJumpSceneBuilder.SceneTextContainsCurrentSourceStamp(
                    "m_Name: '" + current + "'\n" +
                    "m_Name: '" + current + "'\n"),
                Is.False,
                "동일한 현재 표식도 둘이면 중복 생성된 씬이므로 거절해야 합니다.");
            Assert.That(
                MukJumpSceneBuilder.SceneTextContainsCurrentSourceStamp(
                    "m_Name: '" + current + "'\n" +
                    "m_Name: @MukJumpSceneSource_stale\n"),
                Is.False,
                "현재 표식과 오래된 표식이 함께 남은 씬을 통과시키면 안 됩니다.");
        }

        [Test]
        public void MainSceneStampInputsAreNormalizedAndCoverReleaseIdentity()
        {
            string[] paths = MukJumpSceneBuilder.CurrentSceneSourceInputPaths();

            Assert.That(paths, Does.Contain("ProjectSettings/TagManager.asset"));
            Assert.That(paths, Does.Contain("ProjectSettings/ProjectVersion.txt"));
            Assert.That(paths, Does.Contain("Packages/manifest.json"));
            Assert.That(paths, Does.Contain("Packages/packages-lock.json"));
            Assert.That(paths,
                Does.Contain("Assets/Editor/MukJumpSplashSceneBuilder.cs"));
            Assert.That(paths.Any(path => path.EndsWith(".meta")), Is.True);
            Assert.That(paths.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(paths.Length));
            Assert.That(paths.All(path => !path.Contains('\\')), Is.True);
            for (int i = 1; i < paths.Length; i++)
                Assert.That(
                    StringComparer.Ordinal.Compare(paths[i - 1], paths[i]),
                    Is.LessThan(0),
                    "해시 입력은 OS 디렉터리 열거 순서와 무관해야 합니다.");
        }

        [Test]
        public void SceneAttestationRejectsMainOrSplashContentDrift()
        {
            string current = MukJumpSceneBuilder.CurrentSceneSourceStampName();
            string main = "--- !u!1\nm_Name: '" + current + "'\nvalue: 1\n";
            string splash = "--- !u!1\nm_Name: @SplashScene\nvalue: 2\n";
            MukJumpSceneBuilder.SceneAttestation attestation =
                MukJumpSceneBuilder.CreateSceneAttestation(main, splash);

            Assert.That(
                MukJumpSceneBuilder.SceneTextsMatchAttestation(
                    main,
                    splash,
                    attestation),
                Is.True);
            Assert.That(
                MukJumpSceneBuilder.SceneTextsMatchAttestation(
                    main.Replace("value: 1", "value: 9"),
                    splash,
                    attestation),
                Is.False,
                "표식이 남아 있어도 Main 직렬화 값이 바뀌면 거절해야 합니다.");
            Assert.That(
                MukJumpSceneBuilder.SceneTextsMatchAttestation(
                    main,
                    splash + "manualRoot: 1\n",
                    attestation),
                Is.False,
                "Splash만 수동 변경해도 출시 검증이 실패해야 합니다.");
        }

        [Test]
        public void RequiredHudAssetsIncludeInkGaugeBrushIcon()
        {
            Assert.That(
                MukJumpSceneBuilder.RequiredHudTexturePaths(),
                Does.Contain("Assets/Art/UI/muk_brush_icon.png"),
                "게이지 붓 아이콘이 없으면 출시 씬 생성을 차단해야 합니다.");
        }

        [Test]
        public void SplashBuildSettingsDisableEveryNonReleaseScene()
        {
            var existing = new[]
            {
                new EditorBuildSettingsScene(
                    MukJumpSplashSceneBuilder.MainScenePath,
                    true),
                new EditorBuildSettingsScene(
                    "Assets/Scenes/DebugShowcase.unity",
                    true),
                new EditorBuildSettingsScene(
                    "Assets/Scenes/Reference.unity",
                    false),
            };

            EditorBuildSettingsScene[] resolved =
                MukJumpSplashSceneBuilder.ResolveBuildSettings(existing);

            Assert.That(resolved[0].path,
                Is.EqualTo(MukJumpSplashSceneBuilder.ScenePath));
            Assert.That(resolved[0].enabled, Is.True);
            Assert.That(resolved[1].path,
                Is.EqualTo(MukJumpSplashSceneBuilder.MainScenePath));
            Assert.That(resolved[1].enabled, Is.True);
            Assert.That(resolved.Skip(2).All(scene => !scene.enabled), Is.True,
                "개발·참조 씬은 목록에 보존하되 출시 빌드에서는 비활성화해야 합니다.");
        }

        [TestCase(1024, 1024, true)]
        [TestCase(512, 512, false)]
        [TestCase(1024, 512, false)]
        public void StoreIconRequiresExactSquareSource(
            int width,
            int height,
            bool expected)
        {
            Assert.That(
                MukJumpStoreBuild.IsValidStoreIconDimensions(width, height),
                Is.EqualTo(expected));
        }

        [TestCase(4096, 2048, true)]
        [TestCase(2048, 1024, false)]
        [TestCase(4096, 4096, false)]
        public void CharacterSheetRequiresExactSourceGrid(
            int width,
            int height,
            bool expected)
        {
            Assert.That(
                MukJumpSceneBuilder.IsExpectedCharacterSheetDimensions(
                    width,
                    height),
                Is.EqualTo(expected));
        }

        [Test]
        public void CurrentMainSceneAssetsPassStrictReleaseContract()
        {
            Assert.That(
                MukJumpSceneBuilder.CollectRequiredSceneAssetIssues(),
                Is.Empty);
        }

        [Test]
        public void NativeBuildWithoutDedicatedIntentIsFailClosedForReleaseAndIos()
        {
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.None,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Is.Not.Empty);
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.Android,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.None,
                    buildAppBundle: true,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Is.Not.Empty);
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.Development,
                    MukJumpNativeBuildIntent.None,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Is.Not.Empty,
                "iOS Development도 CYSBand 팀을 강제하는 전용 메뉴로만 빌드합니다.");
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.Android,
                    BuildOptions.Development,
                    MukJumpNativeBuildIntent.None,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Is.Empty,
                "Android Development는 서명 없는 로컬 디버그 용도로 허용합니다.");
        }

        [Test]
        public void NativeBuildIntentMustMatchPlatformModeAndBundleKind()
        {
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.AndroidGooglePlay,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("플랫폼"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosLocalValidation,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("Development"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.Android,
                    BuildOptions.Development,
                    MukJumpNativeBuildIntent.AndroidLocalValidation,
                    buildAppBundle: true,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("APK"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.Android,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.AndroidGooglePlay,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("App Bundle"));
        }

        [Test]
        public void NativeBuildUsesOneShotTestAdsOnlyForTestFlightQa()
        {
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosAppStore,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: true,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("MUKJUMP_TEST_ADS"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.Android,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.AndroidGooglePlay,
                    buildAppBundle: true,
                    hasGlobalTestAdsDefine: true,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("MUKJUMP_TEST_ADS"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosTestFlightQa,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: true,
                    hasPerBuildTestAdsDefine: true,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("전역"),
                "TestFlight도 다음 빌드에 남는 전역 테스트 광고 심볼은 금지합니다.");
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosTestFlightQa,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: true,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Is.Empty,
                "테스트 광고는 TestFlight QA BuildPlayer 호출 한 번에만 허용합니다.");
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosTestFlightQa,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: false,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("일회성"));
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    BuildTarget.iOS,
                    BuildOptions.None,
                    MukJumpNativeBuildIntent.IosAppStore,
                    buildAppBundle: false,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: true,
                    applicationIdentifier:
                        MukJumpStoreBuild.DefaultBundleIdentifier),
                Has.Some.Contains("넣을 수 없습니다"));
        }

        [TestCase(MukJumpNativeBuildIntent.IosAppStore, BuildTarget.iOS, false)]
        [TestCase(MukJumpNativeBuildIntent.IosTestFlightQa, BuildTarget.iOS, true)]
        [TestCase(MukJumpNativeBuildIntent.AndroidGooglePlay, BuildTarget.Android, false)]
        public void StoreIntentRejectsUnexpectedBundleIdentifier(
            MukJumpNativeBuildIntent intent,
            BuildTarget target,
            bool testAds)
        {
            Assert.That(
                MukJumpNativeReleaseBuildGuard.CollectInvocationIssues(
                    target,
                    BuildOptions.None,
                    intent,
                    buildAppBundle:
                        intent == MukJumpNativeBuildIntent.AndroidGooglePlay,
                    hasGlobalTestAdsDefine: false,
                    hasPerBuildTestAdsDefine: testAds,
                    applicationIdentifier: "com.other.game"),
                Has.Some.Contains("번들 ID"));
        }

        [Test]
        public void NativeBuildIntentScopeAlwaysClearsAndRejectsNesting()
        {
            using (MukJumpNativeReleaseBuildGuard.BeginIntent(
                       MukJumpNativeBuildIntent.IosAppStore,
                       Array.Empty<string>()))
            {
                Assert.Throws<BuildFailedException>(() =>
                    MukJumpNativeReleaseBuildGuard.BeginIntent(
                        MukJumpNativeBuildIntent.AndroidGooglePlay,
                        Array.Empty<string>()));
            }

            Assert.DoesNotThrow(() =>
            {
                using IDisposable scope =
                    MukJumpNativeReleaseBuildGuard.BeginIntent(
                        MukJumpNativeBuildIntent.AndroidLocalValidation,
                        Array.Empty<string>());
            });
        }

        [Test]
        public void InitialAppStoreReleaseTargetsIphoneOnly()
        {
            Assert.That(
                PlayerSettings.iOS.targetDevice,
                Is.EqualTo(iOSTargetDevice.iPhoneOnly));
        }

        [Test]
        public void AppleSigningUsesCysbandTeamAndNeverNvibeTeam()
        {
            Assert.That(
                MukJumpStoreBuild.DefaultAppleDeveloperTeamId,
                Is.EqualTo("8AU359WZZ2"));
            Assert.That(
                PlayerSettings.iOS.appleDeveloperTeamID,
                Is.EqualTo(MukJumpStoreBuild.DefaultAppleDeveloperTeamId));
            Assert.That(
                PlayerSettings.iOS.appleDeveloperTeamID,
                Is.Not.EqualTo("4QY9W8JDW6"),
                "먹점프는 Nvibe Corporation 팀으로 서명하면 안 됩니다.");
        }

        [Test]
        public void AppleSigningValidatorRejectsEveryNonCysbandTeam()
        {
            Assert.DoesNotThrow(() =>
                MukJumpStoreBuild.ValidateAppleDeveloperTeamId("8AU359WZZ2"));
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ValidateAppleDeveloperTeamId("4QY9W8JDW6"));
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ValidateAppleDeveloperTeamId("AAAAAAAAAA"));
            Assert.Throws<BuildFailedException>(() =>
                MukJumpStoreBuild.ValidateAppleDeveloperTeamId(null));
        }

        [TestCase("appstore-iphone-69", 1320, 2868)]
        [TestCase("appstore-iphone-65", 1284, 2778)]
        public void AppStoreScreenshotsMeetAppleIphoneSpecification(
            string deviceFolder,
            int expectedWidth,
            int expectedHeight)
        {
            string root = Path.Combine("store-screenshots", deviceFolder);
            Assert.That(Directory.Exists(root), Is.True, root);

            string[] files = Directory.GetFiles(root);
            Assert.That(files.Length, Is.EqualTo(6), root);
            Assert.That(
                files.All(path => string.Equals(
                    Path.GetExtension(path),
                    ".png",
                    StringComparison.OrdinalIgnoreCase)),
                Is.True,
                "스크린샷 폴더에는 App Store에 올릴 최종 PNG만 있어야 합니다.");

            foreach (string path in files)
            {
                byte[] png = File.ReadAllBytes(path);
                Assert.That(png.Length, Is.GreaterThan(26), path);
                Assert.That(
                    png.Take(8),
                    Is.EqualTo(new byte[]
                    {
                        137, 80, 78, 71, 13, 10, 26, 10,
                    }),
                    path);
                Assert.That(ReadBigEndianInt32(png, 16),
                    Is.EqualTo(expectedWidth), path);
                Assert.That(ReadBigEndianInt32(png, 20),
                    Is.EqualTo(expectedHeight), path);
                Assert.That(
                    png[25],
                    Is.Not.EqualTo(4).And.Not.EqualTo(6),
                    "App Store 스크린샷 PNG에는 알파 채널이 없어야 합니다: " +
                    path);
            }
        }

        [Test]
        public void StoreIconKeepsLosslessMobileImportSettings()
        {
            var importer = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AppIconPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));

            foreach (string platform in new[] { "iPhone", "Android" })
            {
                TextureImporterPlatformSettings settings =
                    importer.GetPlatformTextureSettings(platform);
                Assert.That(settings.overridden, Is.True, platform);
                Assert.That(settings.maxTextureSize, Is.EqualTo(1024),
                    platform);
                Assert.That(
                    settings.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    platform);
            }
        }

        [Test]
        public void AndroidAdaptiveIconUsesSeparateOpaqueAndTransparentLayers()
        {
            var background = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AndroidAdaptiveBackgroundPath) as
                TextureImporter;
            var foreground = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AndroidAdaptiveForegroundPath) as
                TextureImporter;

            Assert.That(background, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(
                background.assetPath,
                Is.Not.EqualTo(foreground.assetPath));
            Assert.That(
                background.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(
                foreground.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(foreground.alphaIsTransparency, Is.True);

            foreach (TextureImporter importer in new[]
                     {
                         background,
                         foreground,
                     })
            {
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }

        [Test]
        public void AndroidAdaptiveSlotsUseBackgroundThenForegroundAtEveryDensity()
        {
            string backgroundGuid = ReadAssetGuid(
                MukJumpStoreBuild.AndroidAdaptiveBackgroundPath + ".meta");
            string foregroundGuid = ReadAssetGuid(
                MukJumpStoreBuild.AndroidAdaptiveForegroundPath + ".meta");
            string projectSettings = File.ReadAllText(
                "ProjectSettings/ProjectSettings.asset");
            string pair =
                $"- {{fileID: 2800000, guid: {backgroundGuid}, type: 3}}\n" +
                $"      - {{fileID: 2800000, guid: {foregroundGuid}, type: 3}}";

            Assert.That(
                CountOccurrences(projectSettings, pair),
                Is.EqualTo(6),
                "Android Adaptive 아이콘 6개 밀도 슬롯에 배경·전경 레이어가 모두 필요합니다.");
        }

        [Test]
        public void UnusedUnityOnlineServicesStayDisabledForRelease()
        {
            Assert.That(
                MukJumpStoreBuild.CollectUnityServiceIssues(
                    MukJumpStoreBuild.UnityConnectSettingsPath),
                Is.Empty,
                "분석·광고·결제를 사용하지 않는다는 스토어 답변과 프로젝트 설정이 달라집니다.");
        }

        [Test]
        public void GeneratedOutputRequiresWorkspaceAndNativePods()
        {
            string googleClientId =
                MukJumpBackendReleaseValidator.ExpectedIosGoogleClientId;
            string googleUrlScheme =
                MukJumpStoreBuild.ReverseGoogleClientId(googleClientId);
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            WriteCocoaPodsWorkspaceFixture(root);
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>CFBundleIdentifier</key>" +
                "<string>${PRODUCT_BUNDLE_IDENTIFIER}</string>" +
                "<key>GADApplicationIdentifier</key><string>" +
                MukJumpGoogleAdsSettings.IosProductionAppId +
                "</string>" +
                $"<key>GIDClientID</key><string>{googleClientId}</string>" +
                "<key>CFBundleURLTypes</key><array><dict>" +
                "<key>CFBundleURLSchemes</key><array>" +
                $"<string>{googleUrlScheme}</string>" +
                "</array></dict></array>" +
                $"<key>CFBundleShortVersionString</key><string>{PlayerSettings.bundleVersion}</string>" +
                $"<key>CFBundleVersion</key><string>{PlayerSettings.iOS.buildNumber}</string>" +
                "<key>NSUserTrackingUsageDescription</key><string>" +
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription +
                "</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "MukJump.entitlements"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>com.apple.developer.applesignin</key>" +
                "<array><string>Default</string></array>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(
                    root,
                    "Unity-iPhone.xcodeproj",
                    "project.pbxproj"),
                BuildPbxFixture(
                    PlayerSettings.iOS.appleDeveloperTeamID,
                    PlayerSettings.GetApplicationIdentifier(
                        NamedBuildTarget.iOS),
                    "MukJump.entitlements"));
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n" +
                "  - Google-Mobile-Ads-SDK (13.7.0)\n" +
                "  - GoogleSignIn (7.1.0)\n" +
                "  - GoogleUserMessagingPlatform (3.1.0)\n");

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Is.Empty);

                string infoPlistPath = Path.Combine(root, "Info.plist");
                File.WriteAllText(
                    infoPlistPath,
                    File.ReadAllText(infoPlistPath)
                        .Replace(
                            googleClientId,
                            "other.apps.googleusercontent.com")
                        .Replace(
                            googleUrlScheme,
                            "com.googleusercontent.apps.other"));
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Has.Some.Contains("먹점프 전용 값"),
                    "임의 OAuth Client ID와 그에 맞춘 URL scheme도 출시 산출물에서는 거절해야 합니다.");

                File.WriteAllText(
                    infoPlistPath,
                    File.ReadAllText(infoPlistPath).Replace(
                        MukJumpGoogleAdsSettings.IosProductionAppId,
                        "ca-app-pub-2944517353618559~0000000000"));
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Has.Some.Contains("AdMob 앱 ID"),
                    "같은 게시자의 다른 앱 ID도 iOS 출시 산출물에서는 거절해야 합니다.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void PbxReleaseIdentityRejectsMixedTeamAndAppBundle()
        {
            string pbx =
                "DEVELOPMENT_TEAM = \"\";\n" +
                $"DEVELOPMENT_TEAM = {MukJumpStoreBuild.DefaultAppleDeveloperTeamId};\n" +
                "DEVELOPMENT_TEAM = 4QY9W8JDW6;\n" +
                $"PRODUCT_BUNDLE_IDENTIFIER = {MukJumpStoreBuild.DefaultBundleIdentifier};\n" +
                "PRODUCT_BUNDLE_IDENTIFIER = com.unity3d.framework;\n" +
                "PRODUCT_BUNDLE_IDENTIFIER = com.other.game;\n";

            Assert.That(
                MukJumpStoreBuild.CollectPbxTeamIssues(pbx),
                Has.Some.Contains("4QY9W8JDW6"));
            Assert.That(
                MukJumpStoreBuild.CollectPbxStoreBundleIssues(pbx),
                Has.Some.Contains("com.other.game"));

            string validPbx =
                $"DEVELOPMENT_TEAM = {MukJumpStoreBuild.DefaultAppleDeveloperTeamId};\n" +
                $"PRODUCT_BUNDLE_IDENTIFIER = {MukJumpStoreBuild.DefaultBundleIdentifier};\n" +
                "PRODUCT_BUNDLE_IDENTIFIER = com.unity3d.framework;\n" +
                "PRODUCT_BUNDLE_IDENTIFIER = \"com.unity3d.${PRODUCT_NAME:rfc1034identifier}\";\n";
            Assert.That(
                MukJumpStoreBuild.CollectPbxTeamIssues(validPbx),
                Is.Empty);
            Assert.That(
                MukJumpStoreBuild.CollectPbxStoreBundleIssues(validPbx),
                Is.Empty);

            string conditionalPbx =
                $"DEVELOPMENT_TEAM = {MukJumpStoreBuild.DefaultAppleDeveloperTeamId};\n" +
                "\"DEVELOPMENT_TEAM[sdk=iphoneos*][arch=arm64]\" = 4QY9W8JDW6;\n" +
                $"PRODUCT_BUNDLE_IDENTIFIER = {MukJumpStoreBuild.DefaultBundleIdentifier};\n" +
                "\"PRODUCT_BUNDLE_IDENTIFIER[sdk=iphoneos*][arch=arm64]\" = com.other.game;\n";
            Assert.That(
                MukJumpStoreBuild.CollectPbxTeamIssues(conditionalPbx),
                Has.Some.Contains("4QY9W8JDW6"),
                "조건부 SDK 서명 override도 금지 팀 검사 대상입니다.");
            Assert.That(
                MukJumpStoreBuild.CollectPbxStoreBundleIssues(conditionalPbx),
                Has.Some.Contains("com.other.game"),
                "조건부 SDK 번들 ID override도 출시 ID 검사 대상입니다.");

            string allowedConditionalPbx =
                $"\"DEVELOPMENT_TEAM[sdk=iphoneos*][arch=arm64]\" = {MukJumpStoreBuild.DefaultAppleDeveloperTeamId};\n" +
                $"\"PRODUCT_BUNDLE_IDENTIFIER[sdk=iphoneos*][arch=arm64]\" = {MukJumpStoreBuild.DefaultBundleIdentifier};\n";
            Assert.That(
                MukJumpStoreBuild.CollectPbxTeamIssues(allowedConditionalPbx),
                Is.Empty);
            Assert.That(
                MukJumpStoreBuild.CollectPbxStoreBundleIssues(allowedConditionalPbx),
                Is.Empty);
        }

        [Test]
        public void EntitlementValidationUsesOnlyPbxLinkedFiles()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-entitlements-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string valid =
                "<plist><dict><key>com.apple.developer.applesignin</key>" +
                "<array><string>Default</string></array></dict></plist>";
            File.WriteAllText(Path.Combine(root, "Old.entitlements"), valid);
            File.WriteAllText(
                Path.Combine(root, "Linked.entitlements"),
                "<plist><dict/></plist>");

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectLinkedAppleEntitlementIssues(
                        root,
                        "CODE_SIGN_ENTITLEMENTS = Linked.entitlements;\n"),
                    Has.Some.Contains("Default entitlement"),
                    "연결되지 않은 정상 파일이 실제 연결 파일 오류를 숨기면 안 됩니다.");
                Assert.That(
                    MukJumpStoreBuild.CollectLinkedAppleEntitlementIssues(
                        root,
                        "CODE_SIGN_ENTITLEMENTS = Missing.entitlements;\n"),
                    Has.Some.Contains("파일이 없습니다"));

                File.WriteAllText(
                    Path.Combine(root, "Linked.entitlements"),
                    valid);
                Assert.That(
                    MukJumpStoreBuild.CollectLinkedAppleEntitlementIssues(
                        root,
                        "\"CODE_SIGN_ENTITLEMENTS[sdk=iphoneos*]\" = \"$(SRCROOT)/Linked.entitlements\";\n"),
                    Is.Empty,
                    "조건부로 연결된 실제 정상 파일은 통과해야 합니다.");

                File.WriteAllText(
                    Path.Combine(root, "Linked.entitlements"),
                    "<plist><dict>" +
                    "<key>com.apple.developer.applesignin</key>" +
                    "<array><string>Default</string></array>" +
                    "<key>com.apple.developer.applesignin</key>" +
                    "<array/>" +
                    "</dict></plist>");
                Assert.That(
                    MukJumpStoreBuild.CollectLinkedAppleEntitlementIssues(
                        root,
                        "CODE_SIGN_ENTITLEMENTS = Linked.entitlements;\n"),
                    Has.Some.Contains("중복·변형"),
                    "중복된 entitlement 키의 정상 조각만 보고 통과하면 안 됩니다.");

                File.WriteAllText(
                    Path.Combine(root, "Linked.entitlements"),
                    "<plist><dict>" +
                    "<key>com.apple.developer.applesignin</key>" +
                    "<array><string>Default</string><string>Other</string></array>" +
                    "</dict></plist>");
                Assert.That(
                    MukJumpStoreBuild.CollectLinkedAppleEntitlementIssues(
                        root,
                        "CODE_SIGN_ENTITLEMENTS = Linked.entitlements;\n"),
                    Has.Some.Contains("중복·변형"),
                    "Apple 로그인 entitlement는 Default 하나만 허용해야 합니다.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void MainAppTargetIdentityCannotBeSatisfiedByAnotherTarget()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-pbx-main-target-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string valid = BuildPbxFixture(
                    MukJumpStoreBuild.DefaultAppleDeveloperTeamId,
                    MukJumpStoreBuild.DefaultBundleIdentifier,
                    "MukJump.entitlements");
                Assert.That(
                    MukJumpStoreBuild.CollectPbxMainTargetIdentityIssues(
                        root,
                        valid),
                    Is.Empty);

                string invalidMain = BuildPbxFixture(
                        string.Empty,
                        "com.unity3d.framework",
                        "Missing.entitlements") +
                    "\nDEVELOPMENT_TEAM = " +
                    MukJumpStoreBuild.DefaultAppleDeveloperTeamId + ";\n" +
                    "PRODUCT_BUNDLE_IDENTIFIER = " +
                    MukJumpStoreBuild.DefaultBundleIdentifier + ";\n" +
                    "CODE_SIGN_ENTITLEMENTS = MukJump.entitlements;\n";
                string[] issues =
                    MukJumpStoreBuild.CollectPbxMainTargetIdentityIssues(
                        root,
                        invalidMain);
                Assert.That(issues, Has.Some.Contains("서명 팀"));
                Assert.That(issues, Has.Some.Contains("번들 ID"));
                Assert.That(issues, Has.Some.Contains("MukJump.entitlements"));

                string overridden = valid.Replace(
                    "INFOPLIST_FILE = Info.plist;",
                    "INFOPLIST_FILE = Info.plist;\n" +
                    "    \"INFOPLIST_FILE[sdk=iphoneos*][arch=arm64]\" = Bad.plist;")
                    .Replace(
                        "TARGETED_DEVICE_FAMILY = 1;",
                        "TARGETED_DEVICE_FAMILY = 1;\n" +
                        "    \"TARGETED_DEVICE_FAMILY[sdk=iphoneos*][arch=arm64]\" = \"1,2\";")
                    .Replace(
                        "CODE_SIGN_STYLE = Automatic;",
                        "CODE_SIGN_STYLE = Automatic;\n" +
                        "    \"CODE_SIGN_STYLE[sdk=iphoneos*][arch=arm64]\" = Manual;")
                    .Replace(
                        "PROVISIONING_PROFILE = \"\";",
                        "PROVISIONING_PROFILE = \"\";\n" +
                        "    \"PROVISIONING_PROFILE[sdk=iphoneos*][arch=arm64]\" = NvibeProfile;") +
                    "\nCODE_SIGN_IDENTITY = \"iPhone Distribution: Nvibe Corporation\";\n";
                string[] overrideIssues =
                    MukJumpStoreBuild.CollectPbxMainTargetIdentityIssues(
                        root,
                        overridden);
                Assert.That(
                    overrideIssues,
                    Has.Some.Contains("루트 Info.plist"),
                    "앱 구성의 조건부 plist override가 검증을 우회하면 안 됩니다.");
                Assert.That(
                    overrideIssues,
                    Has.Some.Contains("TARGETED_DEVICE_FAMILY=1"),
                    "앱 구성의 조건부 iPad override가 검증을 우회하면 안 됩니다.");
                Assert.That(
                    overrideIssues,
                    Has.Some.Contains("Automatic"),
                    "앱 구성의 조건부 Manual 서명이 검증을 우회하면 안 됩니다.");
                Assert.That(
                    overrideIssues,
                    Has.Some.Contains("provisioning profile"),
                    "구체적인 수동 프로파일을 앱 구성에 남기면 안 됩니다.");
                Assert.That(
                    overrideIssues,
                    Has.Some.Contains("Nvibe 서명 인증서"),
                    "금지된 Nvibe 인증서 이름이 다른 구성에 숨어도 거절해야 합니다.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void InfoPlistIdentityRejectsDuplicateKeysAndUnrelatedUrlString()
        {
            string expectedClientId =
                MukJumpBackendReleaseValidator.ExpectedIosGoogleClientId;
            string expectedScheme =
                MukJumpStoreBuild.ReverseGoogleClientId(expectedClientId);
            string plist =
                "<plist><dict>" +
                "<key>CFBundleIdentifier</key>" +
                $"<string>{MukJumpStoreBuild.DefaultBundleIdentifier}</string>" +
                "<key>CFBundleIdentifier</key><string>com.other.game</string>" +
                "<key>CFBundleShortVersionString</key><string>1.0.0</string>" +
                "<key>CFBundleVersion</key><string>1</string>" +
                "<key>NSUserTrackingUsageDescription</key><string>" +
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription +
                "</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "<key>GADApplicationIdentifier</key><string>" +
                MukJumpGoogleAdsSettings.IosProductionAppId + "</string>" +
                "<key>GADApplicationIdentifier</key><string>other</string>" +
                $"<key>GIDClientID</key><string>{expectedClientId}</string>" +
                "<key>GIDClientID</key><string>other</string>" +
                "<key>CFBundleURLTypes</key><array><dict>" +
                "<key>CFBundleURLSchemes</key><array>" +
                $"<string>{expectedScheme}</string>" +
                "<string>com.googleusercontent.apps.wrong</string>" +
                "</array></dict></array>" +
                $"<key>Unrelated</key><string>{expectedScheme}</string>" +
                "</dict></plist>";

            string[] issues = MukJumpStoreBuild.CollectInfoPlistIssues(
                plist,
                "1.0.0",
                "1",
                requireReleaseIdentity: true);
            Assert.That(issues, Has.Some.Contains("GADApplicationIdentifier"));
            Assert.That(issues, Has.Some.Contains("Client ID"));
            Assert.That(issues, Has.Some.Contains("URL Scheme"));
            Assert.That(issues, Has.Some.Contains("CFBundleIdentifier"));
        }

        [TestCase("${PRODUCT_BUNDLE_IDENTIFIER}")]
        [TestCase("$(PRODUCT_BUNDLE_IDENTIFIER)")]
        [TestCase(MukJumpStoreBuild.DefaultBundleIdentifier)]
        public void InfoPlistAcceptsOnlyExpectedBundleIdentifierForms(
            string bundleIdentifier)
        {
            string expectedClientId =
                MukJumpBackendReleaseValidator.ExpectedIosGoogleClientId;
            string plist =
                "<plist><dict>" +
                $"<key>CFBundleIdentifier</key><string>{bundleIdentifier}</string>" +
                "<key>CFBundleShortVersionString</key><string>1.0.0</string>" +
                "<key>CFBundleVersion</key><string>1</string>" +
                "<key>NSUserTrackingUsageDescription</key><string>" +
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription +
                "</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "<key>GADApplicationIdentifier</key><string>" +
                MukJumpGoogleAdsSettings.IosProductionAppId + "</string>" +
                $"<key>GIDClientID</key><string>{expectedClientId}</string>" +
                "<key>CFBundleURLTypes</key><array><dict>" +
                "<key>CFBundleURLSchemes</key><array><string>" +
                MukJumpStoreBuild.ReverseGoogleClientId(expectedClientId) +
                "</string></array></dict></array>" +
                "</dict></plist>";

            Assert.That(
                MukJumpStoreBuild.CollectInfoPlistIssues(
                    plist,
                    "1.0.0",
                    "1",
                    requireReleaseIdentity: true),
                Is.Empty);
        }

        [Test]
        public void MissingWorkspaceOrPodIsReportedBeforeXcodeHandoff()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>NSUserTrackingUsageDescription</key>" +
                "<string>tracking</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><true/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n  - GoogleSignIn (7.1.0)\n");
            File.WriteAllText(
                Path.Combine(
                    root,
                    "Unity-iPhone.xcodeproj",
                    "project.pbxproj"),
                string.Empty);

            try
            {
                string[] issues =
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root);
                Assert.That(
                    issues.Any(issue => issue.Contains("xcworkspace")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("Google-Mobile-Ads-SDK")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("GoogleUserMessagingPlatform")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("NSUserTrackingUsageDescription")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("ITSAppUsesNonExemptEncryption")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("Google iOS Client ID")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("GADApplicationIdentifier")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("URL Scheme")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("Apple entitlement")),
                    Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void EmptyOrUnlinkedIosWorkspaceIsRejected()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-workspace-" + Guid.NewGuid().ToString("N"));
            string workspace = Path.Combine(
                root,
                MukJumpStoreBuild.IosWorkspaceName);
            Directory.CreateDirectory(workspace);

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectIosWorkspaceIssues(root),
                    Has.Some.Contains("contents.xcworkspacedata"),
                    "이름만 있는 빈 xcworkspace는 CocoaPods 완료 증거가 아닙니다.");

                File.WriteAllText(
                    Path.Combine(workspace, "contents.xcworkspacedata"),
                    "<?xml version=\"1.0\"?><Workspace version=\"1.0\">" +
                    "<FileRef location=\"group:Unity-iPhone.xcodeproj\"/>" +
                    "</Workspace>");
                string[] unlinked =
                    MukJumpStoreBuild.CollectIosWorkspaceIssues(root);
                Assert.That(
                    unlinked,
                    Has.Some.Contains("Pods/Pods.xcodeproj를 참조"));
                Assert.That(
                    unlinked,
                    Has.Some.Contains("Pods/Pods.xcodeproj/project.pbxproj"));

                WriteCocoaPodsWorkspaceFixture(root);
                Assert.That(
                    MukJumpStoreBuild.CollectIosWorkspaceIssues(root),
                    Is.Empty);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void LocalValidationMayCompileBeforeGoogleOauthIsIssued()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-local-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            WriteCocoaPodsWorkspaceFixture(root);
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                $"<key>CFBundleShortVersionString</key><string>{PlayerSettings.bundleVersion}</string>" +
                $"<key>CFBundleVersion</key><string>{PlayerSettings.iOS.buildNumber}</string>" +
                "<key>NSUserTrackingUsageDescription</key><string>" +
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription +
                "</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "MukJump.entitlements"),
                "<plist><dict><key>com.apple.developer.applesignin</key>" +
                "<array><string>Default</string></array></dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "Unity-iPhone.xcodeproj", "project.pbxproj"),
                BuildPbxFixture(
                    PlayerSettings.iOS.appleDeveloperTeamID,
                    PlayerSettings.GetApplicationIdentifier(
                        NamedBuildTarget.iOS),
                    "MukJump.entitlements"));
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n" +
                "  - Google-Mobile-Ads-SDK (13.7.0)\n" +
                "  - GoogleSignIn (7.1.0)\n" +
                "  - GoogleUserMessagingPlatform (3.1.0)\n");

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(
                        root,
                        requireReleaseIdentity: false),
                    Is.Empty);
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Has.Some.Contains("Google iOS Client ID가 Info.plist에 없거나 중복됐습니다."));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestCase(
            "123-example.apps.googleusercontent.com",
            "com.googleusercontent.apps.123-example")]
        [TestCase(" client.apps.googleusercontent.com ",
            "com.googleusercontent.apps.client")]
        [TestCase("", "")]
        public void GoogleClientIdProducesExactIosUrlScheme(
            string clientId,
            string expected)
        {
            Assert.That(
                MukJumpStoreBuild.ReverseGoogleClientId(clientId),
                Is.EqualTo(expected));
        }

        [Test]
        public void AndroidReleaseSigningRequiresExternalSecretsAndExistingStore()
        {
            string missingPath = Path.Combine(
                Path.GetTempPath(),
                "mukjump-missing-" + Guid.NewGuid().ToString("N") +
                ".keystore");

            string[] issues = MukJumpStoreBuild.CollectAndroidSigningIssues(
                missingPath,
                string.Empty,
                string.Empty,
                string.Empty);

            Assert.That(issues.Length, Is.EqualTo(4));
            Assert.That(
                issues.Any(issue => issue.Contains("찾을 수 없습니다")),
                Is.True);
        }

        [Test]
        public void CompleteAndroidReleaseSigningInputPassesValidation()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mukjump-keystore-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectAndroidSigningIssues(
                        path,
                        "store-secret",
                        "mukjump",
                        "alias-secret"),
                    Is.Empty);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static string ReadAssetGuid(string metaPath)
        {
            const string prefix = "guid: ";
            string line = File.ReadLines(metaPath)
                .First(value => value.StartsWith(
                    prefix,
                    StringComparison.Ordinal));
            return line[prefix.Length..].Trim();
        }

        static string BuildPbxFixture(
            string team,
            string bundleIdentifier,
            string entitlementsPath)
        {
            const string configurationListId =
                "1D6058960D05DD3E006BFB54";
            const string releaseConfigurationId =
                "1D6058950D05DD3E006BFB54";
            return
                "AuthenticationServices.framework\n" +
                "1D6058900D05DD3D006BFB54 /* Unity-iPhone */ = {\n" +
                "  isa = PBXNativeTarget;\n" +
                $"  buildConfigurationList = {configurationListId} /* app configs */;\n" +
                "  productType = \"com.apple.product-type.application\";\n" +
                "};\n" +
                $"{releaseConfigurationId} /* Release */ = {{\n" +
                "  isa = XCBuildConfiguration;\n" +
                "  buildSettings = {\n" +
                $"    CODE_SIGN_ENTITLEMENTS = {entitlementsPath};\n" +
                "    CODE_SIGN_STYLE = Automatic;\n" +
                $"    DEVELOPMENT_TEAM = {team};\n" +
                "    INFOPLIST_FILE = Info.plist;\n" +
                $"    PRODUCT_BUNDLE_IDENTIFIER = {bundleIdentifier};\n" +
                "    PROVISIONING_PROFILE = \"\";\n" +
                "    PROVISIONING_PROFILE_SPECIFIER = \"\";\n" +
                "    TARGETED_DEVICE_FAMILY = 1;\n" +
                "  };\n" +
                "  name = Release;\n" +
                "};\n" +
                $"{configurationListId} /* app configs */ = {{\n" +
                "  isa = XCConfigurationList;\n" +
                "  buildConfigurations = (\n" +
                $"    {releaseConfigurationId} /* Release */,\n" +
                "  );\n" +
                "  defaultConfigurationName = Release;\n" +
                "};\n";
        }

        static void WriteCocoaPodsWorkspaceFixture(string root)
        {
            string workspace = Path.Combine(
                root,
                MukJumpStoreBuild.IosWorkspaceName);
            string podsProject = Path.Combine(
                root,
                "Pods",
                "Pods.xcodeproj");
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(podsProject);
            File.WriteAllText(
                Path.Combine(workspace, "contents.xcworkspacedata"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Workspace version=\"1.0\">" +
                "<FileRef location=\"group:Unity-iPhone.xcodeproj\"/>" +
                "<FileRef location=\"group:Pods/Pods.xcodeproj\"/>" +
                "</Workspace>");
            File.WriteAllText(
                Path.Combine(podsProject, "project.pbxproj"),
                "// CocoaPods fixture\n");
        }

        static int CountOccurrences(string value, string pattern)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(
                       pattern,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += pattern.Length;
            }
            return count;
        }

        static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }
    }
}
