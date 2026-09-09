using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MukJump.EditorTools
{
    public enum MukJumpNativeBuildIntent
    {
        None,
        IosAppStore,
        IosTestFlightQa,
        IosLocalValidation,
        AndroidGooglePlay,
        AndroidLocalValidation,
    }

    /// App Store와 Google Play에 공통으로 사용하는 모바일 릴리스 설정을 재현한다.
    public static class MukJumpStoreBuild
    {
        public const string DefaultBundleIdentifier = "com.CYSB.MukJump";
        public const string DefaultVersion = "1.0.0";
        public const string ExpectedUnityVersion = "6000.5.9f1";
        public const string DefaultAppleDeveloperTeamId = "8AU359WZZ2";
        public const string ProjectSettingsPath =
            "ProjectSettings/ProjectSettings.asset";
        public const string AppIconPath =
            "Assets/Art/Brand/mukjump_app_icon_1024.png";
        public const string AndroidAdaptiveBackgroundPath =
            "Assets/Art/Brand/mukjump_adaptive_background_1024.png";
        public const string AndroidAdaptiveForegroundPath =
            "Assets/Art/Brand/mukjump_adaptive_foreground_1024.png";
        public const string IosValidationOutputPath =
            "output/ios/MukJumpValidation";
        public const string IosTestFlightQaOutputPath =
            "output/ios/MukJumpTestFlightQA";
        public const string TestFlightQaAdsDefine = "MUKJUMP_TEST_ADS";
        public const string IosWorkspaceName =
            "Unity-iPhone.xcworkspace";
        public const string AndroidReleaseOutputPath =
            "output/android/MukJump.aab";
        public const string AndroidValidationOutputPath =
            "output/android/MukJumpValidation.apk";
        public const string UnityConnectSettingsPath =
            "ProjectSettings/UnityConnectSettings.asset";
        public const string ThirdPartyNoticesPath =
            "THIRD_PARTY_NOTICES.md";
        public const string BundledThirdPartyNoticesPath =
            "Assets/StreamingAssets/ThirdParty/THIRD_PARTY_NOTICES.md";
        public const string FontLicensePath =
            "Assets/ThirdParty/NanumBrushScript/OFL.txt";
        public const string BundledFontLicensePath =
            "Assets/StreamingAssets/ThirdParty/NanumBrushScript-OFL.txt";
        static readonly string[] RequiredIosPods =
        {
            "Google-Mobile-Ads-SDK",
            "GoogleSignIn",
            "GoogleUserMessagingPlatform",
        };
        const string IosValidationRequestPath =
            "Temp/MukJumpBuildIosValidation.request";
        const string IosReleaseRequestPath =
            "Temp/MukJumpBuildIosRelease.request";
        const string IosTestFlightQaRequestPath =
            "Temp/MukJumpBuildIosTestFlightQA.request";
        // 열려 있는 에디터가 파일 요청을 안전하게 한 번씩만 소비한다.
        static double nextIosBuildRequestPollTime;

        [InitializeOnLoadMethod]
        static void InstallIosBuildRequestWatcher()
        {
            EditorApplication.update -= PollRequestedIosValidationBuild;
            EditorApplication.update += PollRequestedIosValidationBuild;
            PollRequestedIosValidationBuild();
        }

        static void PollRequestedIosValidationBuild()
        {
            if (EditorApplication.timeSinceStartup <
                nextIosBuildRequestPollTime)
                return;
            nextIosBuildRequestPollTime =
                EditorApplication.timeSinceStartup + 1d;
            if (File.Exists(IosTestFlightQaRequestPath))
            {
                File.Delete(IosTestFlightQaRequestPath);
                EditorApplication.delayCall +=
                    ResumeRequestedIosTestFlightQaBuild;
                return;
            }
            if (File.Exists(IosReleaseRequestPath))
            {
                File.Delete(IosReleaseRequestPath);
                EditorApplication.delayCall += ResumeRequestedIosReleaseBuild;
                return;
            }
            if (File.Exists(IosValidationRequestPath))
            {
                File.Delete(IosValidationRequestPath);
                EditorApplication.delayCall += ResumeRequestedIosValidationBuild;
            }
        }

        static void ResumeRequestedIosReleaseBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ResumeRequestedIosReleaseBuild;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += ResumeRequestedIosReleaseBuild;
                return;
            }
            BuildIosXcodeProject();
        }

        static void ResumeRequestedIosTestFlightQaBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall +=
                    ResumeRequestedIosTestFlightQaBuild;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall +=
                    ResumeRequestedIosTestFlightQaBuild;
                return;
            }
            BuildIosTestFlightQaProject();
        }

        static void ResumeRequestedIosValidationBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall +=
                    ResumeRequestedIosValidationBuild;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall +=
                    ResumeRequestedIosValidationBuild;
                return;
            }
            BuildIosLocalValidationProject();
        }

        [MenuItem("MukJump/Release/Configure Mobile Store Settings")]
        public static void ConfigureMobileStoreSettings()
        {
            string bundleIdentifier = Environment.GetEnvironmentVariable(
                "MUKJUMP_BUNDLE_ID") ?? DefaultBundleIdentifier;
            string version = ResolveAppVersion(
                Environment.GetEnvironmentVariable("MUKJUMP_APP_VERSION"),
                PlayerSettings.bundleVersion);
            string buildNumber = ResolveIosBuildNumber(
                Environment.GetEnvironmentVariable("MUKJUMP_IOS_BUILD_NUMBER"),
                PlayerSettings.iOS.buildNumber);
            int androidVersionCode = ResolveAndroidVersionCode(
                Environment.GetEnvironmentVariable("MUKJUMP_ANDROID_VERSION_CODE"),
                PlayerSettings.Android.bundleVersionCode);
            string appleDeveloperTeamId = Environment.GetEnvironmentVariable(
                "MUKJUMP_APPLE_TEAM_ID") ?? DefaultAppleDeveloperTeamId;

            ValidateBundleIdentifier(bundleIdentifier);
            ValidateVersion(version);
            ValidateAppleDeveloperTeamId(appleDeveloperTeamId);
            PlayerSettings.companyName = "CYSBand";
            PlayerSettings.productName = "먹점프";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.muteOtherAudioSources = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.iOS,
                bundleIdentifier);
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                bundleIdentifier);
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.iOS,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.iOS,
                ManagedStrippingLevel.Medium);

            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            // 1.0은 iPhone 전용으로 출시한다. iPad 지원을 켜면 별도의
            // 13인치 스크린샷과 레이아웃 검증이 제출 필수 항목이 된다.
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = buildNumber;
            PlayerSettings.iOS.appleDeveloperTeamID = appleDeveloperTeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.Android.bundleVersionCode = androidVersionCode;
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion =
                AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion =
                AndroidSdkVersions.AndroidApiLevel36;

            ConfigureStoreIcons();

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[MukJump] 모바일 스토어 설정 완료: {bundleIdentifier} / " +
                $"{version} (iOS {buildNumber}, Android {androidVersionCode})");
        }

        /// 환경 변수가 없으면 현재 프로젝트의 단조 증가 번호를 보존한다.
        /// 검증 메뉴를 여는 것만으로 TestFlight 빌드 번호가 1로 돌아가면 안 된다.
        public static string ResolveIosBuildNumber(
            string requestedBuildNumber,
            string currentBuildNumber)
        {
            if (!string.IsNullOrWhiteSpace(requestedBuildNumber))
            {
                string trimmed = requestedBuildNumber.Trim();
                if (!int.TryParse(trimmed, out int requested) || requested < 1)
                    throw new BuildFailedException(
                        "MUKJUMP_IOS_BUILD_NUMBER는 1 이상의 정수여야 합니다.");
                return trimmed;
            }

            string current = currentBuildNumber?.Trim();
            return int.TryParse(current, out int parsedCurrent) &&
                   parsedCurrent >= 1
                ? current
                : "1";
        }

        /// Android versionCode는 iOS CFBundleVersion과 별개의 단조 값이다.
        /// 명시값이 없으면 기존 프로젝트 값을 유지한다.
        public static int ResolveAndroidVersionCode(
            string requestedVersionCode,
            int currentVersionCode)
        {
            if (!string.IsNullOrWhiteSpace(requestedVersionCode))
            {
                if (!int.TryParse(requestedVersionCode.Trim(), out int requested) ||
                    requested < 1)
                    throw new BuildFailedException(
                        "MUKJUMP_ANDROID_VERSION_CODE는 1 이상의 정수여야 합니다.");
                return requested;
            }

            return Mathf.Max(1, currentVersionCode);
        }

        /// 환경 변수로 명시하지 않은 마케팅 버전은 현재 값을 보존한다.
        /// 검증 메뉴 실행만으로 다음 출시 버전을 기본값으로 되돌리지 않는다.
        public static string ResolveAppVersion(
            string requestedVersion,
            string currentVersion)
        {
            if (!string.IsNullOrWhiteSpace(requestedVersion))
            {
                string requested = requestedVersion.Trim();
                ValidateVersion(requested);
                return requested;
            }

            if (!string.IsNullOrWhiteSpace(currentVersion))
            {
                string current = currentVersion.Trim();
                ValidateVersion(current);
                return current;
            }

            return DefaultVersion;
        }

        [MenuItem("MukJump/Release/Validate App Store Readiness")]
        public static void ValidateAppStoreReadiness()
        {
            ConfigureMobileStoreSettings();
            ValidateLocalIosBuildInputs();

            var backendIssues = MukJumpBackendReleaseValidator.CollectIssues(
                BackendReleasePlatform.IOS);
            if (backendIssues.Count > 0)
                throw new BuildFailedException(
                    "뒤끝 출시 설정이 끝나지 않았습니다:\n" +
                    string.Join("\n", backendIssues));

            Debug.Log("[MukJump] App Store 프로젝트 설정 검증 완료");
        }

        internal static void ValidateLocalIosBuildInputs()
        {
            ValidateCommonStoreBuildInputs();
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.iOS,
                    BuildTarget.iOS))
                throw new BuildFailedException(
                    "Unity 6000.5.9f1 iOS Build Support가 설치되지 않았습니다. " +
                    "Unity Hub에서 모듈을 추가하세요.");
        }

        internal static void ValidateCommonStoreBuildInputs()
        {
            string projectSettings = File.Exists(ProjectSettingsPath)
                ? File.ReadAllText(ProjectSettingsPath)
                : null;
            string[] environmentIssues = CollectReleaseEnvironmentIssues(
                Application.unityVersion,
                projectSettings);
            if (environmentIssues.Length > 0)
                throw new BuildFailedException(
                    "출시 빌드 환경이 프로젝트 고정 조건과 다릅니다:\n" +
                    string.Join("\n", environmentIssues));

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new BuildFailedException(
                    "Build Settings에 활성화된 씬이 없습니다.");
            if (scenes.Length != 2 ||
                scenes[0] != MukJumpSplashSceneBuilder.ScenePath ||
                scenes[1] != MukJumpSplashSceneBuilder.MainScenePath)
                throw new BuildFailedException(
                    "출시 빌드의 활성 씬은 Splash → Main 두 개만 허용됩니다.");
            if (!MukJumpSceneBuilder.SavedMainSceneMatchesCurrentSource())
                throw new BuildFailedException(
                    "저장된 Main 씬이 현재 MukJumpSceneBuilder와 일치하지 않습니다. " +
                    "MukJump > Build Main Scene을 실행하고 저장 성공을 확인하세요.");
            string[] requiredAssetIssues =
                MukJumpSceneBuilder.CollectRequiredSceneAssetIssues();
            if (requiredAssetIssues.Length > 0)
                throw new BuildFailedException(
                    "Main 씬 필수 에셋이 출시 조건을 충족하지 않습니다:\n" +
                    string.Join("\n", requiredAssetIssues));

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
                throw new BuildFailedException(
                    $"앱 아이콘을 찾을 수 없습니다: {AppIconPath}");
            if (!IsValidStoreIconDimensions(icon.width, icon.height))
                throw new BuildFailedException(
                    $"앱 아이콘은 정확히 1024×1024여야 합니다: " +
                    $"현재 {icon.width}×{icon.height}");

            foreach (string adaptivePath in new[]
                     {
                         AndroidAdaptiveBackgroundPath,
                         AndroidAdaptiveForegroundPath,
                     })
            {
                Texture2D adaptive =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(adaptivePath);
                if (adaptive == null)
                    throw new BuildFailedException(
                        $"Android 적응형 아이콘 레이어가 없습니다: {adaptivePath}");
                if (adaptive.width != 1024 || adaptive.height != 1024)
                    throw new BuildFailedException(
                        $"Android 적응형 아이콘은 1024×1024여야 합니다: " +
                        $"{adaptivePath} ({adaptive.width}×{adaptive.height})");
            }

            string[] legalDocuments =
            {
                "docs/legal/terms-of-service.md",
                "docs/legal/privacy-policy.md",
                "docs/legal/account-deletion.md",
                "docs/store/privacy-disclosures.md",
                "docs/store/app-review-notes.md",
            };
            string missingLegal = legalDocuments.FirstOrDefault(
                path => !File.Exists(path));
            if (!string.IsNullOrEmpty(missingLegal))
                throw new BuildFailedException(
                    $"스토어 법적·심사 문서가 없습니다: {missingLegal}");

            string[] noticeIssues = CollectThirdPartyNoticeIssues();
            if (noticeIssues.Length > 0)
                throw new BuildFailedException(
                    "외부 에셋 고지가 배포물에 정확히 동봉되지 않았습니다:\n" +
                    string.Join("\n", noticeIssues));

            string[] unityServiceIssues = CollectUnityServiceIssues(
                UnityConnectSettingsPath);
            if (unityServiceIssues.Length > 0)
                throw new BuildFailedException(
                    "사용하지 않는 Unity 온라인 서비스가 켜져 있습니다:\n" +
                    string.Join("\n", unityServiceIssues));
        }

        public static string[] CollectReleaseEnvironmentIssues(
            string unityVersion,
            string projectSettingsYaml)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (!string.Equals(
                    unityVersion,
                    ExpectedUnityVersion,
                    StringComparison.Ordinal))
                issues.Add(
                    $"Unity {ExpectedUnityVersion}만 허용됩니다: 현재 {unityVersion ?? "없음"}");
            if (!UsesExclusiveInputSystem(projectSettingsYaml))
                issues.Add(
                    "Active Input Handling은 Input System Package (New) 전용 값 1이어야 합니다.");
            return issues.ToArray();
        }

        public static bool UsesExclusiveInputSystem(string projectSettingsYaml)
        {
            if (string.IsNullOrWhiteSpace(projectSettingsYaml))
                return false;

            string[] values = projectSettingsYaml
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(
                    "activeInputHandler:",
                    StringComparison.Ordinal))
                .Select(line => line.Substring(
                    "activeInputHandler:".Length).Trim())
                .ToArray();
            return values.Length == 1 &&
                   int.TryParse(values[0], out int value) &&
                   value == 1;
        }

        public static string[] CollectThirdPartyNoticeIssues(
            string noticesPath = ThirdPartyNoticesPath,
            string bundledNoticesPath = BundledThirdPartyNoticesPath,
            string fontLicensePath = FontLicensePath,
            string bundledFontLicensePath = BundledFontLicensePath)
        {
            var issues = new System.Collections.Generic.List<string>();
            CollectBundledFileIssue(
                noticesPath,
                bundledNoticesPath,
                "외부 에셋 고지",
                issues);
            CollectBundledFileIssue(
                fontLicensePath,
                bundledFontLicensePath,
                "Nanum Brush Script OFL 원문",
                issues);
            return issues.ToArray();
        }

        static void CollectBundledFileIssue(
            string sourcePath,
            string bundledPath,
            string label,
            System.Collections.Generic.List<string> issues)
        {
            if (!File.Exists(sourcePath))
            {
                issues.Add($"{label} 원본이 없습니다: {sourcePath}");
                return;
            }
            if (!File.Exists(bundledPath))
            {
                issues.Add($"{label} 동봉본이 없습니다: {bundledPath}");
                return;
            }
            if (!File.ReadAllBytes(sourcePath)
                    .SequenceEqual(File.ReadAllBytes(bundledPath)))
                issues.Add($"{label} 동봉본이 원본과 다릅니다: {bundledPath}");
        }

        public static string[] CollectUnityServiceIssues(string settingsPath)
        {
            if (string.IsNullOrWhiteSpace(settingsPath) ||
                !File.Exists(settingsPath))
                return new[] { "UnityConnectSettings.asset을 찾을 수 없습니다." };

            string yaml = File.ReadAllText(settingsPath);
            var issues = new System.Collections.Generic.List<string>();
            foreach (string section in new[]
                     {
                         "InsightsSettings",
                         "CrashReportingSettings",
                         "UnityPurchasingSettings",
                         "UnityAnalyticsSettings",
                         "UnityAdsSettings",
                         "PerformanceReportingSettings",
                     })
            {
                if (!IsUnityServiceDisabled(yaml, section))
                    issues.Add($"{section}의 m_Enabled를 0으로 유지해야 합니다.");
            }
            if (!SectionContainsSetting(
                    yaml,
                    "InsightsSettings",
                    "m_EngineDiagnosticsEnabled: 0"))
                issues.Add(
                    "InsightsSettings의 Engine Diagnostics를 꺼야 개인정보 공개와 실제 수집이 일치합니다.");
            return issues.ToArray();
        }

        static bool SectionContainsSetting(
            string yaml,
            string section,
            string expectedSetting)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            string header = $"  {section}:";
            bool insideSection = false;
            foreach (string line in lines)
            {
                if (!insideSection)
                {
                    insideSection = string.Equals(
                        line.TrimEnd(),
                        header,
                        StringComparison.Ordinal);
                    continue;
                }

                if (line.StartsWith("  ", StringComparison.Ordinal) &&
                    !line.StartsWith("    ", StringComparison.Ordinal) &&
                    line.Trim().Length > 0)
                    return false;
                if (line.Trim() == expectedSetting)
                    return true;
            }
            return false;
        }

        static bool IsUnityServiceDisabled(string yaml, string section)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            string header = $"  {section}:";
            bool insideSection = false;
            foreach (string line in lines)
            {
                if (!insideSection)
                {
                    insideSection = string.Equals(
                        line.TrimEnd(),
                        header,
                        StringComparison.Ordinal);
                    continue;
                }

                // UnityConnectSettings의 자식 섹션은 정확히 두 칸 들여쓰기다.
                // 네 칸 속성 줄을 다음 섹션으로 잘못 판정하지 않는다.
                if (line.StartsWith("  ", StringComparison.Ordinal) &&
                    !line.StartsWith("    ", StringComparison.Ordinal) &&
                    line.Trim().Length > 0)
                    return false;

                string trimmed = line.Trim();
                if (trimmed == "m_Enabled: 0")
                    return true;
                if (section == "CrashReportingSettings" &&
                    trimmed == "m_EnableCloudDiagnosticsReporting: 0")
                    return true;
            }

            return false;
        }

        internal static void ValidateLocalAndroidBuildInputs()
        {
            ValidateCommonStoreBuildInputs();
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Android,
                    BuildTarget.Android))
                throw new BuildFailedException(
                    "Unity 6000.5.9f1 Android Build Support가 설치되지 않았습니다. " +
                    "Unity Hub에서 Android SDK & NDK와 OpenJDK를 포함해 설치하세요.");
            if (PlayerSettings.Android.targetArchitectures !=
                AndroidArchitecture.ARM64)
                throw new BuildFailedException(
                    "Google Play 빌드는 Android ARM64 단일 아키텍처여야 합니다.");
            if (PlayerSettings.Android.targetSdkVersion <
                AndroidSdkVersions.AndroidApiLevel36)
                throw new BuildFailedException(
                    "2026-08-31 이후 Google Play 제출은 Android API 36 이상이 필요합니다.");
        }

        [MenuItem("MukJump/Release/Build iOS Xcode Project")]
        public static void BuildIosXcodeProject()
        {
            ValidateAppStoreReadiness();
            BuildIosProjectAt(
                "output/ios/MukJump",
                "iOS Xcode 프로젝트",
                requireReleaseIdentity: true,
                intent: MukJumpNativeBuildIntent.IosAppStore,
                options: BuildOptions.None);
        }

        [MenuItem("MukJump/Release/Build iOS TestFlight QA (Test Ads)")]
        public static void BuildIosTestFlightQaProject()
        {
            // 일반 Release와 분리된 1회 빌드 심볼로만 테스트 광고를 강제한다.
            string nextBuildNumber = ResolveNextIosTestFlightBuildNumber(
                Environment.GetEnvironmentVariable(
                    "MUKJUMP_IOS_BUILD_NUMBER"),
                PlayerSettings.iOS.buildNumber);
            ValidateAppStoreReadiness();
            PlayerSettings.iOS.buildNumber = nextBuildNumber;
            AssetDatabase.SaveAssets();
            BuildIosProjectAt(
                IosTestFlightQaOutputPath,
                $"iOS TestFlight QA 프로젝트 빌드 {nextBuildNumber}",
                requireReleaseIdentity: true,
                intent: MukJumpNativeBuildIntent.IosTestFlightQa,
                options: BuildOptions.None,
                extraScriptingDefines: new[] { TestFlightQaAdsDefine });
        }

        public static string NextIosBuildNumber(string currentBuildNumber)
        {
            if (!int.TryParse(currentBuildNumber, out int current) || current < 0)
                return "1";
            if (current == int.MaxValue)
                throw new BuildFailedException(
                    "iOS 빌드 번호가 최댓값이라 자동 증가할 수 없습니다.");
            return (current + 1).ToString();
        }

        public static string ResolveNextIosTestFlightBuildNumber(
            string requestedBuildNumber,
            string currentBuildNumber) =>
            NextIosBuildNumber(ResolveIosBuildNumber(
                requestedBuildNumber,
                currentBuildNumber));

        [MenuItem("MukJump/Release/Build iOS Local Validation Project")]
        public static void BuildIosLocalValidationProject()
        {
            ConfigureMobileStoreSettings();
            ValidateLocalIosBuildInputs();
            BuildIosProjectAt(
                IosValidationOutputPath,
                "iOS 로컬 검증 프로젝트",
                requireReleaseIdentity: false,
                intent: MukJumpNativeBuildIntent.IosLocalValidation,
                options: BuildOptions.Development);
        }

        [MenuItem("MukJump/Release/Validate Google Play Readiness")]
        public static void ValidateGooglePlayReadiness()
        {
            ConfigureMobileStoreSettings();
            ValidateLocalAndroidBuildInputs();

            var backendIssues = MukJumpBackendReleaseValidator.CollectIssues(
                BackendReleasePlatform.Android);
            if (backendIssues.Count > 0)
                throw new BuildFailedException(
                    "뒤끝 출시 설정이 끝나지 않았습니다:\n" +
                    string.Join("\n", backendIssues));

            string[] signingIssues = CollectAndroidSigningIssues(
                Environment.GetEnvironmentVariable(
                    "MUKJUMP_ANDROID_KEYSTORE_PATH"),
                Environment.GetEnvironmentVariable(
                    "MUKJUMP_ANDROID_KEYSTORE_PASS"),
                Environment.GetEnvironmentVariable(
                    "MUKJUMP_ANDROID_KEY_ALIAS"),
                Environment.GetEnvironmentVariable(
                    "MUKJUMP_ANDROID_KEY_ALIAS_PASS"));
            if (signingIssues.Length > 0)
                throw new BuildFailedException(
                    "Android 출시 서명 설정이 끝나지 않았습니다:\n" +
                    string.Join("\n", signingIssues));

            Debug.Log("[MukJump] Google Play 프로젝트·서명 설정 검증 완료");
        }

        [MenuItem("MukJump/Release/Build Android Local Validation APK")]
        public static void BuildAndroidLocalValidationApk()
        {
            ConfigureMobileStoreSettings();
            ValidateLocalAndroidBuildInputs();
            BuildAndroidPlayerAt(
                AndroidValidationOutputPath,
                buildAppBundle: false,
                options: BuildOptions.Development,
                configureReleaseSigning: false,
                intent: MukJumpNativeBuildIntent.AndroidLocalValidation,
                label: "Android 로컬 검증 APK");
        }

        [MenuItem("MukJump/Release/Build Android App Bundle")]
        public static void BuildAndroidAppBundle()
        {
            ValidateGooglePlayReadiness();
            BuildAndroidPlayerAt(
                AndroidReleaseOutputPath,
                buildAppBundle: true,
                options: BuildOptions.None,
                configureReleaseSigning: true,
                intent: MukJumpNativeBuildIntent.AndroidGooglePlay,
                label: "Google Play Android App Bundle");
        }

        public static string[] CollectAndroidSigningIssues(
            string keystorePath,
            string keystorePassword,
            string keyAlias,
            string keyAliasPassword)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(keystorePath))
                issues.Add("MUKJUMP_ANDROID_KEYSTORE_PATH가 비어 있습니다.");
            else if (!File.Exists(Path.GetFullPath(keystorePath)))
                issues.Add("Android keystore 파일을 찾을 수 없습니다.");
            if (string.IsNullOrWhiteSpace(keystorePassword))
                issues.Add("MUKJUMP_ANDROID_KEYSTORE_PASS가 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(keyAlias))
                issues.Add("MUKJUMP_ANDROID_KEY_ALIAS가 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(keyAliasPassword))
                issues.Add("MUKJUMP_ANDROID_KEY_ALIAS_PASS가 비어 있습니다.");
            return issues.ToArray();
        }

        static void BuildAndroidPlayerAt(
            string relativePath,
            bool buildAppBundle,
            BuildOptions options,
            bool configureReleaseSigning,
            MukJumpNativeBuildIntent intent,
            string label)
        {
            if (EditorUserBuildSettings.activeBuildTarget !=
                BuildTarget.Android)
                throw new BuildFailedException(
                    "Android 빌드는 Android 타깃이 활성화된 상태에서 시작해야 " +
                    "광고·Google 로그인 전처리기가 포함됩니다. 에디터에서 " +
                    "Android로 전환하거나 배치 명령에 -buildTarget Android를 " +
                    "추가하세요.");

            string outputPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            bool previousBuildAppBundle =
                EditorUserBuildSettings.buildAppBundle;
            bool previousUseCustomKeystore =
                PlayerSettings.Android.useCustomKeystore;
            string previousKeystoreName =
                PlayerSettings.Android.keystoreName;
            string previousKeystorePass =
                PlayerSettings.Android.keystorePass;
            string previousKeyaliasName =
                PlayerSettings.Android.keyaliasName;
            string previousKeyaliasPass =
                PlayerSettings.Android.keyaliasPass;

            try
            {
                EditorUserBuildSettings.buildAppBundle = buildAppBundle;
                if (configureReleaseSigning)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = Path.GetFullPath(
                        Environment.GetEnvironmentVariable(
                            "MUKJUMP_ANDROID_KEYSTORE_PATH"));
                    PlayerSettings.Android.keystorePass =
                        Environment.GetEnvironmentVariable(
                            "MUKJUMP_ANDROID_KEYSTORE_PASS");
                    PlayerSettings.Android.keyaliasName =
                        Environment.GetEnvironmentVariable(
                            "MUKJUMP_ANDROID_KEY_ALIAS");
                    PlayerSettings.Android.keyaliasPass =
                        Environment.GetEnvironmentVariable(
                            "MUKJUMP_ANDROID_KEY_ALIAS_PASS");
                }

                BuildReport report;
                using (MukJumpNativeReleaseBuildGuard.BeginIntent(
                           intent,
                           Array.Empty<string>()))
                {
                    report = BuildPipeline.BuildPlayer(
                        new BuildPlayerOptions
                        {
                            scenes = EnabledScenes(),
                            locationPathName = outputPath,
                            target = BuildTarget.Android,
                            options = options,
                        });
                }
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException(
                        $"{label} 생성 실패: {report.summary.result}");
                if (!File.Exists(outputPath))
                    throw new BuildFailedException(
                        $"{label} 결과 파일이 없습니다: {outputPath}");

                Debug.Log($"[MukJump] {label} 생성 완료: {outputPath}");
                if (!Application.isBatchMode)
                    EditorUtility.RevealInFinder(outputPath);
            }
            finally
            {
                PlayerSettings.Android.keystorePass =
                    previousKeystorePass;
                PlayerSettings.Android.keyaliasPass =
                    previousKeyaliasPass;
                PlayerSettings.Android.useCustomKeystore =
                    previousUseCustomKeystore;
                PlayerSettings.Android.keystoreName = previousKeystoreName;
                PlayerSettings.Android.keyaliasName = previousKeyaliasName;
                EditorUserBuildSettings.buildAppBundle =
                    previousBuildAppBundle;
            }
        }

        static void BuildIosProjectAt(
            string relativePath,
            string label,
            bool requireReleaseIdentity,
            MukJumpNativeBuildIntent intent,
            BuildOptions options,
            string[] extraScriptingDefines = null)
        {
            EnsureIosBuildTargetActive();
            string outputPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            string[] invocationDefines =
                (extraScriptingDefines ?? Array.Empty<string>()).ToArray();
            BuildReport report;
            using (MukJumpNativeReleaseBuildGuard.BeginIntent(
                       intent,
                       invocationDefines))
            {
                report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = EnabledScenes(),
                        locationPathName = outputPath,
                        target = BuildTarget.iOS,
                        options = options,
                        extraScriptingDefines = invocationDefines,
                    });
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(
                    $"{label} 생성 실패: {report.summary.result}");

            ValidateIosGeneratedOutput(
                outputPath,
                requireReleaseIdentity);

            string workspacePath = Path.Combine(
                outputPath,
                IosWorkspaceName);
            Debug.Log(
                $"[MukJump] {label} 생성 완료. Xcode에서는 프로젝트가 아니라 " +
                $"워크스페이스를 여세요: {workspacePath}");
            if (!Application.isBatchMode)
                EditorUtility.RevealInFinder(workspacePath);
        }

        static void EnsureIosBuildTargetActive()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
                return;
            if (Application.isBatchMode)
                throw new BuildFailedException(
                    "iOS 빌드는 배치 실행 시 -buildTarget iOS를 지정해야 합니다.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.iOS,
                    BuildTarget.iOS))
                throw new BuildFailedException(
                    "활성 빌드 타깃을 iOS로 전환하지 못했습니다. Build Settings에서 iOS 모듈을 확인하세요.");
        }

        public static string[] CollectIosGeneratedOutputIssues(
            string outputPath,
            bool requireReleaseIdentity = true)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                return new[] { "iOS 출력 경로가 비어 있습니다." };

            var issues = new System.Collections.Generic.List<string>();
            string projectPath = Path.Combine(
                outputPath,
                "Unity-iPhone.xcodeproj");
            string lockPath = Path.Combine(outputPath, "Podfile.lock");
            string infoPlistPath = Path.Combine(outputPath, "Info.plist");
            string pbxProjectPath = Path.Combine(
                projectPath,
                "project.pbxproj");
            if (!Directory.Exists(projectPath))
                issues.Add("Unity-iPhone.xcodeproj가 없습니다.");
            issues.AddRange(CollectIosWorkspaceIssues(outputPath));
            if (!File.Exists(infoPlistPath))
                issues.Add("Info.plist가 없습니다.");
            else
                issues.AddRange(CollectInfoPlistIssues(
                    File.ReadAllText(infoPlistPath),
                    PlayerSettings.bundleVersion?.Trim(),
                    PlayerSettings.iOS.buildNumber?.Trim(),
                    requireReleaseIdentity));
            if (!File.Exists(pbxProjectPath))
            {
                issues.Add("Xcode project.pbxproj가 없습니다.");
            }
            else
            {
                string pbxText = File.ReadAllText(pbxProjectPath);
                if (!pbxText.Contains("AuthenticationServices.framework"))
                    issues.Add("AuthenticationServices.framework 연결이 없습니다.");
                issues.AddRange(CollectLinkedAppleEntitlementIssues(
                    outputPath,
                    pbxText));
                issues.AddRange(CollectPbxTeamIssues(pbxText));
                issues.AddRange(CollectPbxMainTargetIdentityIssues(
                    outputPath,
                    pbxText));
                if (requireReleaseIdentity)
                {
                    issues.AddRange(CollectPbxStoreBundleIssues(pbxText));
                }
                else
                {
                    string bundleIdentifier =
                        PlayerSettings.GetApplicationIdentifier(
                            NamedBuildTarget.iOS);
                    if (!string.IsNullOrWhiteSpace(bundleIdentifier) &&
                        !pbxText.Contains(
                            $"PRODUCT_BUNDLE_IDENTIFIER = {bundleIdentifier};"))
                        issues.Add(
                            $"Xcode 번들 ID가 Unity 설정과 다릅니다: {bundleIdentifier}");
                }
            }
            if (!File.Exists(lockPath))
            {
                issues.Add("Podfile.lock이 없습니다. CocoaPods 설치 결과를 확인하세요.");
                return issues.ToArray();
            }

            string lockText = File.ReadAllText(lockPath);
            foreach (string podName in RequiredIosPods)
            {
                if (!lockText.Contains($"  - {podName} ("))
                    issues.Add($"필수 iOS Pod가 없습니다: {podName}");
            }

            return issues.ToArray();
        }

        public static string[] CollectIosWorkspaceIssues(string outputPath)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                issues.Add("iOS 출력 경로가 비어 있습니다.");
                return issues.ToArray();
            }

            string workspacePath = Path.Combine(
                outputPath,
                IosWorkspaceName);
            string workspaceDataPath = Path.Combine(
                workspacePath,
                "contents.xcworkspacedata");
            string podsProjectPath = Path.Combine(
                outputPath,
                "Pods",
                "Pods.xcodeproj",
                "project.pbxproj");
            if (!Directory.Exists(workspacePath))
            {
                issues.Add(
                    "Unity-iPhone.xcworkspace가 없습니다. CocoaPods 의존성 해석을 확인하세요.");
            }
            else if (!File.Exists(workspaceDataPath))
            {
                issues.Add(
                    "Unity-iPhone.xcworkspace/contents.xcworkspacedata가 없습니다.");
            }
            else
            {
                try
                {
                    var document = new System.Xml.XmlDocument
                    {
                        XmlResolver = null,
                    };
                    document.Load(workspaceDataPath);
                    var locations = new System.Collections.Generic.HashSet<string>(
                        StringComparer.Ordinal);
                    foreach (System.Xml.XmlNode node in
                             document.GetElementsByTagName("FileRef"))
                    {
                        string location = node.Attributes?["location"]?.Value?
                            .Trim();
                        if (!string.IsNullOrWhiteSpace(location))
                            locations.Add(location);
                    }

                    if (!locations.Contains(
                            "group:Unity-iPhone.xcodeproj"))
                        issues.Add(
                            "Xcode 워크스페이스가 Unity-iPhone.xcodeproj를 참조하지 않습니다.");
                    if (!locations.Contains(
                            "group:Pods/Pods.xcodeproj"))
                        issues.Add(
                            "Xcode 워크스페이스가 Pods/Pods.xcodeproj를 참조하지 않습니다.");
                }
                catch (Exception exception)
                {
                    issues.Add(
                        "Xcode 워크스페이스 XML을 읽을 수 없습니다: " +
                        exception.Message);
                }
            }

            if (!File.Exists(podsProjectPath))
                issues.Add(
                    "Pods/Pods.xcodeproj/project.pbxproj가 없습니다. CocoaPods 설치 결과를 확인하세요.");
            return issues.ToArray();
        }

        public static string[] CollectInfoPlistIssues(
            string infoPlistText,
            string expectedVersion,
            string expectedBuild,
            bool requireReleaseIdentity = true)
        {
            var issues = new System.Collections.Generic.List<string>();
            System.Xml.XmlElement rootDictionary;
            try
            {
                var document = new System.Xml.XmlDocument
                {
                    XmlResolver = null,
                };
                document.LoadXml(infoPlistText ?? string.Empty);
                rootDictionary = document.DocumentElement?
                    .ChildNodes
                    .OfType<System.Xml.XmlElement>()
                    .FirstOrDefault(element => element.Name == "dict");
                if (document.DocumentElement?.Name != "plist" ||
                    rootDictionary == null)
                    throw new System.Xml.XmlException(
                        "plist 루트 dict가 없습니다.");
            }
            catch (Exception exception)
            {
                return new[]
                {
                    "Info.plist XML 구조를 읽을 수 없습니다: " +
                    exception.Message,
                };
            }

            if (string.IsNullOrWhiteSpace(expectedVersion) ||
                !HasUniquePlistString(
                    rootDictionary,
                    "CFBundleShortVersionString",
                    expectedVersion))
                issues.Add(
                    $"iOS 마케팅 버전이 Unity 설정과 다릅니다: {expectedVersion ?? string.Empty}");
            if (string.IsNullOrWhiteSpace(expectedBuild) ||
                !HasUniquePlistString(
                    rootDictionary,
                    "CFBundleVersion",
                    expectedBuild))
                issues.Add(
                    $"iOS 빌드 번호가 Unity 설정과 다릅니다: {expectedBuild ?? string.Empty}");
            if (!HasUniquePlistString(
                    rootDictionary,
                    "NSUserTrackingUsageDescription",
                    MukJumpGoogleMobileAdsSetup.TrackingUsageDescription))
                issues.Add(
                    "ATT 설명 NSUserTrackingUsageDescription이 없거나 출시 문구와 다릅니다.");
            if (!TryGetUniquePlistValue(
                    rootDictionary,
                    "ITSAppUsesNonExemptEncryption",
                    out System.Xml.XmlElement encryptionValue) ||
                encryptionValue.Name != "false")
                issues.Add(
                    "면제 암호화 선언 ITSAppUsesNonExemptEncryption=NO가 없습니다.");

            if (!requireReleaseIdentity)
                return issues.ToArray();

            if (!TryGetUniquePlistValue(
                    rootDictionary,
                    "CFBundleIdentifier",
                    out System.Xml.XmlElement bundleIdentifierValue))
                issues.Add(
                    "iOS CFBundleIdentifier가 Info.plist에 없거나 중복됐습니다.");
            else if (bundleIdentifierValue.Name != "string" ||
                     !IsExpectedIosBundleIdentifier(
                         bundleIdentifierValue.InnerText))
                issues.Add(
                    "iOS CFBundleIdentifier가 먹점프 앱 번들 ID를 가리키지 않습니다.");

            if (!TryGetUniquePlistValue(
                    rootDictionary,
                    "GADApplicationIdentifier",
                    out System.Xml.XmlElement admobValue))
                issues.Add(
                    "iOS AdMob GADApplicationIdentifier가 Info.plist에 없거나 중복됐습니다.");
            else if (admobValue.Name != "string" ||
                     !string.Equals(
                         admobValue.InnerText.Trim(),
                         MukJumpGoogleAdsSettings.IosProductionAppId,
                         StringComparison.Ordinal))
                issues.Add(
                    "iOS AdMob 앱 ID가 확인된 먹점프 전용 값과 다릅니다.");

            string expectedClientId = MukJumpBackendReleaseValidator
                .ExpectedIosGoogleClientId;
            if (!TryGetUniquePlistValue(
                    rootDictionary,
                    "GIDClientID",
                    out System.Xml.XmlElement clientIdValue))
                issues.Add(
                    "Google iOS Client ID가 Info.plist에 없거나 중복됐습니다.");
            else if (clientIdValue.Name != "string" ||
                     !string.Equals(
                         clientIdValue.InnerText.Trim(),
                         expectedClientId,
                         StringComparison.Ordinal))
                issues.Add(
                    "Google iOS Client ID가 발급 확인된 먹점프 전용 값과 다릅니다.");

            string expectedUrlScheme = ReverseGoogleClientId(expectedClientId);
            if (expectedUrlScheme.Length == 0 ||
                !ContainsOnlyUrlScheme(rootDictionary, expectedUrlScheme))
                issues.Add(
                    "Google 로그인 URL Scheme이 먹점프 Client ID 하나로만 구성되지 않았습니다: " +
                    expectedUrlScheme);
            return issues.ToArray();
        }

        static bool IsExpectedIosBundleIdentifier(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            return string.Equals(
                       normalized,
                       DefaultBundleIdentifier,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       normalized,
                       "${PRODUCT_BUNDLE_IDENTIFIER}",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       normalized,
                       "$(PRODUCT_BUNDLE_IDENTIFIER)",
                       StringComparison.Ordinal);
        }

        static bool HasUniquePlistString(
            System.Xml.XmlElement dictionary,
            string key,
            string expectedValue) =>
            TryGetUniquePlistValue(
                dictionary,
                key,
                out System.Xml.XmlElement value) &&
            value.Name == "string" &&
            string.Equals(
                value.InnerText.Trim(),
                expectedValue?.Trim(),
                StringComparison.Ordinal);

        static bool TryGetUniquePlistValue(
            System.Xml.XmlElement dictionary,
            string key,
            out System.Xml.XmlElement value)
        {
            value = null;
            if (dictionary == null || dictionary.Name != "dict")
                return false;

            System.Xml.XmlElement[] elements = dictionary.ChildNodes
                .OfType<System.Xml.XmlElement>()
                .ToArray();
            int matches = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i].Name != "key" ||
                    !string.Equals(
                        elements[i].InnerText,
                        key,
                        StringComparison.Ordinal))
                    continue;

                matches++;
                value = i + 1 < elements.Length ? elements[i + 1] : null;
            }
            return matches == 1 && value != null && value.Name != "key";
        }

        static bool ContainsOnlyUrlScheme(
            System.Xml.XmlElement rootDictionary,
            string expectedScheme)
        {
            if (!TryGetUniquePlistValue(
                    rootDictionary,
                    "CFBundleURLTypes",
                    out System.Xml.XmlElement urlTypes) ||
                urlTypes.Name != "array")
                return false;

            var foundSchemes = new System.Collections.Generic.List<string>();
            System.Xml.XmlElement[] urlTypeElements = urlTypes.ChildNodes
                .OfType<System.Xml.XmlElement>()
                .ToArray();
            if (urlTypeElements.Any(element => element.Name != "dict"))
                return false;

            foreach (System.Xml.XmlElement urlType in urlTypeElements)
            {
                if (!TryGetUniquePlistValue(
                        urlType,
                        "CFBundleURLSchemes",
                        out System.Xml.XmlElement schemes) ||
                    schemes.Name != "array")
                    return false;
                System.Xml.XmlElement[] schemeElements = schemes.ChildNodes
                    .OfType<System.Xml.XmlElement>()
                    .ToArray();
                if (schemeElements.Any(element => element.Name != "string"))
                    return false;
                foundSchemes.AddRange(schemeElements.Select(element =>
                    element.InnerText.Trim()));
            }
            return foundSchemes.Count == 1 &&
                string.Equals(
                    foundSchemes[0],
                    expectedScheme,
                    StringComparison.Ordinal);
        }

        public static string[] CollectLinkedAppleEntitlementIssues(
            string outputPath,
            string pbxText)
        {
            var issues = new System.Collections.Generic.List<string>();
            string[] values = CollectPbxSettingValues(
                    pbxText,
                    "CODE_SIGN_ENTITLEMENTS")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (values.Length == 0)
            {
                issues.Add(
                    "Xcode 앱 타깃에 Sign in with Apple entitlements가 연결되지 않았습니다.");
                return issues.ToArray();
            }

            foreach (string value in values)
            {
                if (!TryResolveGeneratedOutputPath(
                        outputPath,
                        value,
                        out string entitlementsPath))
                {
                    issues.Add(
                        $"Xcode entitlements 경로가 출력 폴더 밖이거나 해석할 수 없습니다: {value}");
                    continue;
                }
                if (!File.Exists(entitlementsPath))
                {
                    issues.Add(
                        $"Xcode에 연결된 Sign in with Apple entitlements 파일이 없습니다: {value}");
                    continue;
                }

                string entitlementsText = File.ReadAllText(entitlementsPath);
                if (!HasExactAppleSignInEntitlement(entitlementsText))
                    issues.Add(
                        $"Xcode에 연결된 파일의 Sign in with Apple Default entitlement가 없거나 중복·변형됐습니다: {value}");
            }

            return issues.ToArray();
        }

        static bool HasExactAppleSignInEntitlement(string entitlementsText)
        {
            System.Xml.XmlElement dictionary;
            try
            {
                var document = new System.Xml.XmlDocument
                {
                    XmlResolver = null,
                };
                document.LoadXml(entitlementsText ?? string.Empty);
                dictionary = document.DocumentElement?
                    .ChildNodes
                    .OfType<System.Xml.XmlElement>()
                    .SingleOrDefault(element => element.Name == "dict");
            }
            catch
            {
                return false;
            }

            if (!TryGetUniquePlistValue(
                    dictionary,
                    "com.apple.developer.applesignin",
                    out System.Xml.XmlElement entitlement) ||
                entitlement.Name != "array")
                return false;

            System.Xml.XmlElement[] values = entitlement.ChildNodes
                .OfType<System.Xml.XmlElement>()
                .ToArray();
            return values.Length == 1 &&
                values[0].Name == "string" &&
                string.Equals(
                    values[0].InnerText.Trim(),
                    "Default",
                    StringComparison.Ordinal);
        }

        public static string[] CollectPbxMainTargetIdentityIssues(
            string outputPath,
            string pbxText)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(pbxText))
                return new[] { "Xcode Unity-iPhone 앱 타깃 설정이 없습니다." };

            System.Text.RegularExpressions.Match targetMatch =
                System.Text.RegularExpressions.Regex.Match(
                    pbxText,
                    "(?ms)^[ \\t]*[A-Fa-f0-9]{24} /\\* Unity-iPhone \\*/ = \\{" +
                    "[ \\t\\r\\n]*isa = PBXNativeTarget;" +
                    "(?<body>.*?)^[ \\t]*\\};");
            if (!targetMatch.Success ||
                !targetMatch.Groups["body"].Value.Contains(
                    "productType = \"com.apple.product-type.application\";"))
                return new[]
                {
                    "Xcode Unity-iPhone 앱 타깃을 확인할 수 없습니다."
                };

            System.Text.RegularExpressions.Match configurationListReference =
                System.Text.RegularExpressions.Regex.Match(
                    targetMatch.Groups["body"].Value,
                    "(?m)^[ \\t]*buildConfigurationList = ([A-Fa-f0-9]{24})\\b");
            if (!configurationListReference.Success)
                return new[]
                {
                    "Xcode Unity-iPhone 앱 타깃의 빌드 구성 연결을 확인할 수 없습니다."
                };

            string configurationListId =
                configurationListReference.Groups[1].Value;
            System.Text.RegularExpressions.Match listMatch =
                System.Text.RegularExpressions.Regex.Match(
                    pbxText,
                    $"(?ms)^[ \\t]*{System.Text.RegularExpressions.Regex.Escape(configurationListId)} " +
                    "/\\* [^*]+ \\*/ = \\{(?<body>.*?)^[ \\t]*\\};");
            if (!listMatch.Success)
                return new[]
                {
                    "Xcode Unity-iPhone 앱 타깃의 빌드 구성 목록을 확인할 수 없습니다."
                };

            System.Text.RegularExpressions.MatchCollection entries =
                System.Text.RegularExpressions.Regex.Matches(
                    listMatch.Groups["body"].Value,
                    "(?m)^[ \\t]*([A-Fa-f0-9]{24}) /\\* ([^*]+) \\*/,?[ \\t]*$");
            if (entries.Count == 0)
                return new[]
                {
                    "Xcode Unity-iPhone 앱 타깃에 빌드 구성이 없습니다."
                };

            string expectedEntitlementsPath = Path.GetFullPath(
                Path.Combine(outputPath, "MukJump.entitlements"));
            string expectedInfoPlistPath = Path.GetFullPath(
                Path.Combine(outputPath, "Info.plist"));
            foreach (System.Text.RegularExpressions.Match entry in entries)
            {
                string configurationId = entry.Groups[1].Value;
                string configurationName = entry.Groups[2].Value.Trim();
                System.Text.RegularExpressions.Match configurationMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        pbxText,
                        $"(?ms)^[ \\t]*{System.Text.RegularExpressions.Regex.Escape(configurationId)} " +
                        "/\\* [^*]+ \\*/ = \\{[ \\t\\r\\n]*" +
                        "isa = XCBuildConfiguration;" +
                        "(?<body>.*?)^[ \\t]*name = [^;]+;[ \\t\\r\\n]*^[ \\t]*\\};");
                if (!configurationMatch.Success)
                {
                    issues.Add(
                        $"Unity-iPhone {configurationName} 빌드 설정을 읽을 수 없습니다.");
                    continue;
                }

                string body = configurationMatch.Groups["body"].Value;
                string[] teams = CollectPbxSettingValues(
                    body,
                    "DEVELOPMENT_TEAM");
                if (teams.Length == 0 || teams.Any(value =>
                        !string.Equals(
                            value,
                            DefaultAppleDeveloperTeamId,
                            StringComparison.Ordinal)))
                    issues.Add(
                        $"Unity-iPhone {configurationName} 서명 팀은 {DefaultAppleDeveloperTeamId}여야 합니다.");

                string[] signingStyles = CollectPbxSettingValues(
                    body,
                    "CODE_SIGN_STYLE");
                if (signingStyles.Length == 0 || signingStyles.Any(value =>
                        !string.Equals(
                            value,
                            "Automatic",
                            StringComparison.Ordinal)))
                    issues.Add(
                        $"Unity-iPhone {configurationName} 서명 방식은 Automatic이어야 합니다.");

                if (!HasAutomaticProvisioningSettings(body))
                    issues.Add(
                        $"Unity-iPhone {configurationName}에 수동 provisioning profile이 설정됐습니다.");

                string[] bundles = CollectPbxSettingValues(
                    body,
                    "PRODUCT_BUNDLE_IDENTIFIER");
                if (bundles.Length == 0 || bundles.Any(value =>
                        !string.Equals(
                            value,
                            DefaultBundleIdentifier,
                            StringComparison.Ordinal)))
                    issues.Add(
                        $"Unity-iPhone {configurationName} 번들 ID는 {DefaultBundleIdentifier}여야 합니다.");

                string[] entitlements = CollectPbxSettingValues(
                    body,
                    "CODE_SIGN_ENTITLEMENTS");
                bool exactEntitlements = entitlements.Length > 0 &&
                    entitlements.All(value =>
                        TryResolveGeneratedOutputPath(
                            outputPath,
                            value,
                            out string resolved) &&
                        string.Equals(
                            resolved,
                            expectedEntitlementsPath,
                            StringComparison.Ordinal));
                if (!exactEntitlements)
                    issues.Add(
                        $"Unity-iPhone {configurationName}는 MukJump.entitlements를 직접 사용해야 합니다.");

                string[] infoPlists = CollectPbxSettingValues(
                    body,
                    "INFOPLIST_FILE");
                bool exactInfoPlist = infoPlists.Length > 0 &&
                    infoPlists.All(value =>
                        TryResolveGeneratedOutputPath(
                            outputPath,
                            value,
                            out string resolved) &&
                        string.Equals(
                            resolved,
                            expectedInfoPlistPath,
                            StringComparison.Ordinal));
                if (!exactInfoPlist)
                    issues.Add(
                        $"Unity-iPhone {configurationName}는 검증된 루트 Info.plist를 직접 사용해야 합니다.");

                string[] deviceFamilies = CollectPbxSettingValues(
                    body,
                    "TARGETED_DEVICE_FAMILY");
                if (deviceFamilies.Length == 0 || deviceFamilies.Any(value =>
                        !string.Equals(
                            value,
                            "1",
                            StringComparison.Ordinal)))
                    issues.Add(
                        $"Unity-iPhone {configurationName}는 iPhone 전용 TARGETED_DEVICE_FAMILY=1이어야 합니다.");
            }

            string[] signingIdentities = CollectPbxSettingValues(
                pbxText,
                "CODE_SIGN_IDENTITY");
            if (signingIdentities.Any(IsForbiddenSigningValue))
                issues.Add(
                    "Xcode 프로젝트에 금지된 Nvibe 서명 인증서가 설정됐습니다.");

            return issues.Distinct().ToArray();
        }

        static bool HasAutomaticProvisioningSettings(string body)
        {
            string[] profiles = CollectPbxSettingValues(
                body,
                "PROVISIONING_PROFILE");
            string[] specifiers = CollectPbxSettingValues(
                body,
                "PROVISIONING_PROFILE_SPECIFIER");
            if (profiles.Length == 0 || specifiers.Length == 0)
                return false;

            string[] profileApps = CollectPbxSettingValues(
                body,
                "PROVISIONING_PROFILE_APP");
            string[] specifierApps = CollectPbxSettingValues(
                body,
                "PROVISIONING_PROFILE_SPECIFIER_APP");
            if (profileApps.Any(value => !string.IsNullOrWhiteSpace(value)) ||
                specifierApps.Any(value => !string.IsNullOrWhiteSpace(value)))
                return false;

            return profiles.All(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    string.Equals(
                        value,
                        "$(PROVISIONING_PROFILE_APP)",
                        StringComparison.Ordinal)) &&
                specifiers.All(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    string.Equals(
                        value,
                        "$(PROVISIONING_PROFILE_SPECIFIER_APP)",
                        StringComparison.Ordinal));
        }

        static bool IsForbiddenSigningValue(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf(
                 "Nvibe",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf(
                 "4QY9W8JDW6",
                 StringComparison.OrdinalIgnoreCase) >= 0);

        static bool TryResolveGeneratedOutputPath(
            string outputPath,
            string configuredPath,
            out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(outputPath) ||
                string.IsNullOrWhiteSpace(configuredPath))
                return false;

            string relative = configuredPath.Trim().Trim('"')
                .Replace("$(SRCROOT)/", string.Empty)
                .Replace("${SRCROOT}/", string.Empty)
                .Replace("$(PROJECT_DIR)/", string.Empty)
                .Replace("${PROJECT_DIR}/", string.Empty);
            if (relative.Contains('$'))
                return false;

            string root = Path.GetFullPath(outputPath)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(
                Path.IsPathRooted(relative)
                    ? relative
                    : Path.Combine(root, relative));
            string rootPrefix = root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal))
                return false;

            resolvedPath = candidate;
            return true;
        }

        public static string[] CollectPbxTeamIssues(string pbxText)
        {
            var issues = new System.Collections.Generic.List<string>();
            string[] values = CollectPbxSettingValues(
                pbxText,
                "DEVELOPMENT_TEAM");
            if (!values.Any(value => string.Equals(
                    value,
                    DefaultAppleDeveloperTeamId,
                    StringComparison.Ordinal)))
                issues.Add(
                    $"Xcode 서명 팀에 CYSBand {DefaultAppleDeveloperTeamId}가 없습니다.");
            foreach (string value in values.Where(value =>
                         !string.IsNullOrWhiteSpace(value) &&
                         !string.Equals(
                             value,
                             DefaultAppleDeveloperTeamId,
                             StringComparison.Ordinal)))
                issues.Add(
                    $"Xcode 프로젝트에 허용되지 않은 서명 팀이 섞였습니다: {value}");
            return issues.Distinct().ToArray();
        }

        public static string[] CollectPbxStoreBundleIssues(string pbxText)
        {
            var issues = new System.Collections.Generic.List<string>();
            string[] values = CollectPbxSettingValues(
                pbxText,
                "PRODUCT_BUNDLE_IDENTIFIER");
            if (!values.Any(value => string.Equals(
                    value,
                    DefaultBundleIdentifier,
                    StringComparison.Ordinal)))
                issues.Add(
                    $"Xcode 앱 번들 ID에 {DefaultBundleIdentifier}가 없습니다.");
            foreach (string value in values.Where(value =>
                         !string.IsNullOrWhiteSpace(value) &&
                         !IsAllowedIosProjectBundleIdentifier(value)))
                issues.Add(
                    $"Xcode 프로젝트에 허용되지 않은 번들 ID가 섞였습니다: {value}");
            return issues.Distinct().ToArray();
        }

        static string[] CollectPbxSettingValues(
            string pbxText,
            string settingName)
        {
            if (string.IsNullOrWhiteSpace(pbxText))
                return Array.Empty<string>();
            return System.Text.RegularExpressions.Regex.Matches(
                    pbxText,
                    $"(?m)^[ \\t]*\"?{System.Text.RegularExpressions.Regex.Escape(settingName)}" +
                    "(?:\\[[^\\]\\r\\n]+\\])*\"?[ \\t]*=[ \\t]*([^;\\r\\n]+);")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Groups[1].Value.Trim().Trim('"'))
                .ToArray();
        }

        static bool IsAllowedIosProjectBundleIdentifier(string value) =>
            string.Equals(value, DefaultBundleIdentifier, StringComparison.Ordinal) ||
            string.Equals(value, "com.unity3d.framework", StringComparison.Ordinal) ||
            string.Equals(
                value,
                "com.unity3d.${PRODUCT_NAME:rfc1034identifier}",
                StringComparison.Ordinal);

        public static string ReverseGoogleClientId(string clientId)
        {
            string normalized = clientId?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return string.Empty;
            string[] parts = normalized.Split(
                new[] { '.' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return string.Empty;
            Array.Reverse(parts);
            return string.Join(".", parts);
        }

        static void ValidateIosGeneratedOutput(
            string outputPath,
            bool requireReleaseIdentity)
        {
            string[] issues = CollectIosGeneratedOutputIssues(
                outputPath,
                requireReleaseIdentity);
            if (issues.Length == 0)
                return;

            throw new BuildFailedException(
                "iOS 프로젝트의 네이티브 의존성 검증에 실패했습니다:\n" +
                string.Join("\n", issues));
        }

        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        static void ConfigureStoreIcons()
        {
            ConfigureIconImporter(AppIconPath, preserveAlpha: false);
            ConfigureIconImporter(
                AndroidAdaptiveBackgroundPath,
                preserveAlpha: false);
            ConfigureIconImporter(
                AndroidAdaptiveForegroundPath,
                preserveAlpha: true);
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                AppIconPath);
            if (icon == null)
            {
                Debug.LogWarning(
                    $"[MukJump] 앱 아이콘을 찾을 수 없습니다: {AppIconPath}");
                return;
            }

            SetIcons(NamedBuildTarget.iOS, icon);
            SetIcons(NamedBuildTarget.Android, icon);
            Texture2D adaptiveBackground =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AndroidAdaptiveBackgroundPath);
            Texture2D adaptiveForeground =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AndroidAdaptiveForegroundPath);
            SetAndroidPlatformIcons(
                icon,
                adaptiveBackground,
                adaptiveForeground);
        }

        static void ConfigureIconImporter(
            string assetPath,
            bool preserveAlpha)
        {
            if (AssetImporter.GetAtPath(assetPath) is not
                TextureImporter importer)
                return;

            TextureImporterAlphaSource expectedAlphaSource = preserveAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            bool changed = importer.mipmapEnabled ||
                           importer.maxTextureSize != 1024 ||
                           importer.textureCompression !=
                           TextureImporterCompression.Uncompressed ||
                           importer.crunchedCompression ||
                           importer.compressionQuality != 100 ||
                           importer.alphaSource != expectedAlphaSource ||
                           importer.alphaIsTransparency != preserveAlpha;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.compressionQuality = 100;
            importer.alphaSource = expectedAlphaSource;
            importer.alphaIsTransparency = preserveAlpha;

            string[] platforms = { "iPhone", "Android" };
            foreach (string platform in platforms)
            {
                TextureImporterPlatformSettings settings =
                    importer.GetPlatformTextureSettings(platform);
                if (!settings.overridden ||
                    settings.maxTextureSize != 1024 ||
                    settings.textureCompression !=
                    TextureImporterCompression.Uncompressed ||
                    settings.crunchedCompression)
                    changed = true;
                settings.overridden = true;
                settings.maxTextureSize = 1024;
                settings.textureCompression =
                    TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }

            if (changed)
                importer.SaveAndReimport();
        }

        internal static bool IsValidStoreIconDimensions(int width, int height) =>
            width == 1024 && height == 1024;

        static void SetIcons(NamedBuildTarget target, Texture2D icon)
        {
            IconKind[] kinds =
            {
                IconKind.Application,
                IconKind.Settings,
                IconKind.Notification,
                IconKind.Spotlight,
                IconKind.Store,
            };

            foreach (IconKind kind in kinds)
            {
                int slotCount = PlayerSettings.GetIconSizes(target, kind).Length;
                if (slotCount <= 0)
                    continue;
                PlayerSettings.SetIcons(
                    target,
                    Enumerable.Repeat(icon, slotCount).ToArray(),
                    kind);
            }
        }

        static void SetAndroidPlatformIcons(
            Texture2D icon,
            Texture2D adaptiveBackground,
            Texture2D adaptiveForeground)
        {
            // Android의 Legacy/Round/Adaptive 슬롯은 구형 IconKind API와 별도로 저장된다.
            // Android Build Support가 없는 에디터에서도 이 스크립트가 컴파일되도록
            // 플랫폼 확장 어셈블리의 종류를 런타임에 찾는다.
            string[] kindNames = { "Legacy", "Round", "Adaptive" };
            foreach (string kindName in kindNames)
            {
                PlatformIconKind kind = FindAndroidPlatformIconKind(kindName);
                if (kind == null)
                {
                    Debug.LogWarning(
                        $"[MukJump] Android {kindName} 아이콘 종류를 찾지 못했습니다. " +
                        "Unity Hub에서 Android Build Support를 설치한 뒤 설정 메뉴를 다시 실행하세요.");
                    continue;
                }

                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(
                    NamedBuildTarget.Android,
                    kind);
                foreach (PlatformIcon slot in slots)
                {
                    int layerCount = Math.Max(1, slot.minLayerCount);
                    Texture2D[] textures;
                    if (kindName == "Adaptive" && layerCount >= 2 &&
                        adaptiveBackground != null &&
                        adaptiveForeground != null)
                    {
                        textures = new Texture2D[layerCount];
                        textures[0] = adaptiveBackground;
                        textures[1] = adaptiveForeground;
                        for (int layer = 2; layer < layerCount; layer++)
                            textures[layer] = adaptiveForeground;
                    }
                    else
                    {
                        textures = Enumerable.Repeat(icon, layerCount)
                            .ToArray();
                    }
                    slot.SetTextures(textures);
                }

                PlayerSettings.SetPlatformIcons(
                    NamedBuildTarget.Android,
                    kind,
                    slots);
            }
        }

        static PlatformIconKind FindAndroidPlatformIconKind(string memberName)
        {
            const string typeName =
                "UnityEditor.Android.AndroidPlatformIconKind";
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.Static;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type == null)
                    continue;

                PropertyInfo property = type.GetProperty(memberName, flags);
                if (property?.GetValue(null) is PlatformIconKind propertyKind)
                    return propertyKind;

                FieldInfo field = type.GetField(memberName, flags);
                if (field?.GetValue(null) is PlatformIconKind fieldKind)
                    return fieldKind;
            }

            return null;
        }

        static void ValidateBundleIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 255 ||
                value.Split('.').Length < 3 ||
                value.Any(character =>
                    !(char.IsLetterOrDigit(character) ||
                      character == '.' || character == '-')))
                throw new BuildFailedException(
                    $"올바르지 않은 번들 ID입니다: {value}");
        }

        static void ValidateVersion(string value)
        {
            string[] parts = value?.Split('.') ?? Array.Empty<string>();
            if (parts.Length < 2 || parts.Length > 3 ||
                parts.Any(part =>
                    !int.TryParse(part, out int number) || number < 0))
                throw new BuildFailedException(
                    $"앱 버전은 1.0.0 같은 숫자 형식이어야 합니다: {value}");
        }

        public static void ValidateAppleDeveloperTeamId(string value)
        {
            if (!string.Equals(
                    value,
                    DefaultAppleDeveloperTeamId,
                    StringComparison.Ordinal))
                throw new BuildFailedException(
                    "먹점프 iOS 서명은 CYSBand Team " +
                    $"{DefaultAppleDeveloperTeamId}만 사용할 수 있습니다.");
        }
    }

    /// 일반 Build 버튼이나 임의 BuildPipeline 호출이 스토어 검증을 우회하지
    /// 못하게 한다. 전용 메뉴가 발급한 일회성 intent는 빌드가 끝나거나 예외가
    /// 나면 즉시 폐기된다.
    public sealed class MukJumpNativeReleaseBuildGuard :
        IPreprocessBuildWithReport
    {
        static MukJumpNativeBuildIntent currentIntent;
        static string[] currentExtraScriptingDefines = Array.Empty<string>();

        public int callbackOrder => -2000;

        internal static IDisposable BeginIntent(
            MukJumpNativeBuildIntent intent,
            string[] extraScriptingDefines)
        {
            if (intent == MukJumpNativeBuildIntent.None)
                throw new ArgumentException(
                    "네이티브 빌드 intent는 None일 수 없습니다.",
                    nameof(intent));
            if (currentIntent != MukJumpNativeBuildIntent.None)
                throw new BuildFailedException(
                    "다른 먹점프 네이티브 빌드가 아직 종료되지 않았습니다.");
            currentIntent = intent;
            currentExtraScriptingDefines =
                (extraScriptingDefines ?? Array.Empty<string>())
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Select(symbol => symbol.Trim())
                .ToArray();
            return new IntentScope(intent);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.iOS && target != BuildTarget.Android)
                return;

            string[] invocationIssues = CollectInvocationIssues(
                target,
                report.summary.options,
                currentIntent,
                EditorUserBuildSettings.buildAppBundle,
                HasGlobalTestAdsDefine(target),
                HasTestAdsDefine(currentExtraScriptingDefines),
                PlayerSettings.GetApplicationIdentifier(
                    target == BuildTarget.iOS
                        ? NamedBuildTarget.iOS
                        : NamedBuildTarget.Android));
            if (invocationIssues.Length > 0)
                throw new BuildFailedException(
                    "먹점프 네이티브 빌드 진입 검증에 실패했습니다:\n" +
                    string.Join("\n", invocationIssues));

            // Android Development는 서명 없는 로컬 디버그 경로를 유지한다.
            // iOS는 Development라도 잘못된 Apple 팀으로 Xcode 프로젝트를
            // 만들 수 있으므로 전용 Local Validation intent만 허용한다.
            if (currentIntent == MukJumpNativeBuildIntent.None)
                return;

            if (target == BuildTarget.iOS)
            {
                MukJumpStoreBuild.ValidateLocalIosBuildInputs();
                MukJumpStoreBuild.ValidateAppleDeveloperTeamId(
                    PlayerSettings.iOS.appleDeveloperTeamID);
                if (currentIntent == MukJumpNativeBuildIntent.IosAppStore ||
                    currentIntent == MukJumpNativeBuildIntent.IosTestFlightQa)
                    ValidateBackend(BackendReleasePlatform.IOS);
                return;
            }

            MukJumpStoreBuild.ValidateLocalAndroidBuildInputs();
            if (currentIntent != MukJumpNativeBuildIntent.AndroidGooglePlay)
                return;

            ValidateBackend(BackendReleasePlatform.Android);
            if (!PlayerSettings.Android.useCustomKeystore)
                throw new BuildFailedException(
                    "Google Play intent에 Android 출시 keystore가 적용되지 않았습니다.");
            string[] signingIssues = MukJumpStoreBuild.CollectAndroidSigningIssues(
                PlayerSettings.Android.keystoreName,
                PlayerSettings.Android.keystorePass,
                PlayerSettings.Android.keyaliasName,
                PlayerSettings.Android.keyaliasPass);
            if (signingIssues.Length > 0)
                throw new BuildFailedException(
                    "Google Play intent의 실제 서명 설정이 유효하지 않습니다:\n" +
                    string.Join("\n", signingIssues));
        }

        public static string[] CollectInvocationIssues(
            BuildTarget target,
            BuildOptions options,
            MukJumpNativeBuildIntent intent,
            bool buildAppBundle,
            bool hasGlobalTestAdsDefine,
            bool hasPerBuildTestAdsDefine,
            string applicationIdentifier)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (target != BuildTarget.iOS && target != BuildTarget.Android)
                return issues.ToArray();

            bool development = (options & BuildOptions.Development) != 0;
            if (intent == MukJumpNativeBuildIntent.None)
            {
                if (target == BuildTarget.iOS)
                    issues.Add(
                        "iOS 빌드는 Development라도 MukJump/Release 전용 메뉴로 실행해야 합니다.");
                else if (!development)
                    issues.Add(
                        "Release 네이티브 빌드는 MukJump/Release 전용 메뉴로 실행해야 합니다.");
                return issues.ToArray();
            }

            bool iosIntent = intent == MukJumpNativeBuildIntent.IosAppStore ||
                             intent == MukJumpNativeBuildIntent.IosTestFlightQa ||
                             intent == MukJumpNativeBuildIntent.IosLocalValidation;
            bool androidIntent =
                intent == MukJumpNativeBuildIntent.AndroidGooglePlay ||
                intent == MukJumpNativeBuildIntent.AndroidLocalValidation;
            if (target == BuildTarget.iOS && !iosIntent ||
                target == BuildTarget.Android && !androidIntent)
                issues.Add("빌드 intent와 대상 플랫폼이 일치하지 않습니다.");

            bool localIntent =
                intent == MukJumpNativeBuildIntent.IosLocalValidation ||
                intent == MukJumpNativeBuildIntent.AndroidLocalValidation;
            if (development != localIntent)
                issues.Add(
                    localIntent
                        ? "로컬 검증 intent는 Development Build여야 합니다."
                        : "스토어/TestFlight intent는 Development Build일 수 없습니다.");

            if (target == BuildTarget.Android)
            {
                bool expectedBundle =
                    intent == MukJumpNativeBuildIntent.AndroidGooglePlay;
                if (buildAppBundle != expectedBundle)
                    issues.Add(
                        expectedBundle
                            ? "Google Play intent는 Android App Bundle이어야 합니다."
                            : "Android 로컬 검증 intent는 APK여야 합니다.");
            }

            bool productionStoreIntent =
                intent == MukJumpNativeBuildIntent.IosAppStore ||
                intent == MukJumpNativeBuildIntent.IosTestFlightQa ||
                intent == MukJumpNativeBuildIntent.AndroidGooglePlay;
            if (productionStoreIntent && !string.Equals(
                    applicationIdentifier,
                    MukJumpStoreBuild.DefaultBundleIdentifier,
                    StringComparison.Ordinal))
                issues.Add(
                    "스토어/TestFlight 빌드의 번들 ID는 " +
                    $"{MukJumpStoreBuild.DefaultBundleIdentifier}여야 합니다.");

            // 전역 심볼은 이후 빌드에도 남으므로 어떤 전용 네이티브 빌드에서도
            // 허용하지 않는다. TestFlight QA 테스트 광고는 해당 BuildPlayer
            // 호출에만 전달되는 extraScriptingDefines로 증명한다.
            if (hasGlobalTestAdsDefine)
                issues.Add(
                    "네이티브 빌드에 전역 MUKJUMP_TEST_ADS 심볼이 남아 있습니다.");
            bool expectsPerBuildTestAds =
                intent == MukJumpNativeBuildIntent.IosTestFlightQa;
            if (hasPerBuildTestAdsDefine != expectsPerBuildTestAds)
                issues.Add(
                    expectsPerBuildTestAds
                        ? "TestFlight QA 빌드는 일회성 MUKJUMP_TEST_ADS 심볼이 필요합니다."
                        : "이 빌드 intent에는 MUKJUMP_TEST_ADS 심볼을 넣을 수 없습니다.");
            return issues.ToArray();
        }

        static void ValidateBackend(BackendReleasePlatform platform)
        {
            var issues = MukJumpBackendReleaseValidator.CollectIssues(platform);
            if (issues.Count > 0)
                throw new BuildFailedException(
                    "뒤끝 출시 설정이 끝나지 않았습니다:\n" +
                    string.Join("\n", issues));
        }

        static bool HasGlobalTestAdsDefine(BuildTarget target)
        {
            NamedBuildTarget namedTarget = target == BuildTarget.iOS
                ? NamedBuildTarget.iOS
                : NamedBuildTarget.Android;
            string symbols = PlayerSettings.GetScriptingDefineSymbols(
                namedTarget);
            return HasTestAdsDefine(symbols.Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries));
        }

        static bool HasTestAdsDefine(string[] symbols) =>
            symbols != null && symbols.Any(symbol => string.Equals(
                symbol?.Trim(),
                MukJumpStoreBuild.TestFlightQaAdsDefine,
                StringComparison.Ordinal));

        sealed class IntentScope : IDisposable
        {
            readonly MukJumpNativeBuildIntent intent;
            bool disposed;

            public IntentScope(MukJumpNativeBuildIntent intent)
            {
                this.intent = intent;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                if (currentIntent == intent)
                {
                    currentIntent = MukJumpNativeBuildIntent.None;
                    currentExtraScriptingDefines = Array.Empty<string>();
                }
            }
        }
    }
}
