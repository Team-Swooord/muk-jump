using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// App Store와 Google Play에 공통으로 사용하는 모바일 릴리스 설정을 재현한다.
    public static class MukJumpStoreBuild
    {
        public const string DefaultBundleIdentifier = "com.CYSB.MukJump";
        public const string DefaultVersion = "1.0.0";
        public const string DefaultAppleDeveloperTeamId = "8AU359WZZ2";
        public const string AppIconPath =
            "Assets/Art/Brand/mukjump_app_icon_1024.png";
        public const string AndroidAdaptiveBackgroundPath =
            "Assets/Art/Brand/mukjump_adaptive_background_1024.png";
        public const string AndroidAdaptiveForegroundPath =
            "Assets/Art/Brand/mukjump_adaptive_foreground_1024.png";
        public const string IosValidationOutputPath =
            "output/ios/MukJumpValidation";
        public const string IosWorkspaceName =
            "Unity-iPhone.xcworkspace";
        public const string AndroidReleaseOutputPath =
            "output/android/MukJump.aab";
        public const string AndroidValidationOutputPath =
            "output/android/MukJumpValidation.apk";
        public const string UnityConnectSettingsPath =
            "ProjectSettings/UnityConnectSettings.asset";
        static readonly string[] RequiredIosPods =
        {
            "Google-Mobile-Ads-SDK",
            "GoogleSignIn",
            "GoogleUserMessagingPlatform",
        };
        const string IosValidationRequestPath =
            "Temp/MukJumpBuildIosValidation.request";
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
            if (!File.Exists(IosValidationRequestPath))
                return;
            File.Delete(IosValidationRequestPath);
            EditorApplication.delayCall += ResumeRequestedIosValidationBuild;
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
            string version = Environment.GetEnvironmentVariable(
                "MUKJUMP_APP_VERSION") ?? DefaultVersion;
            string buildNumber = Environment.GetEnvironmentVariable(
                "MUKJUMP_IOS_BUILD_NUMBER") ?? "1";
            string appleDeveloperTeamId = Environment.GetEnvironmentVariable(
                "MUKJUMP_APPLE_TEAM_ID") ?? DefaultAppleDeveloperTeamId;

            ValidateBundleIdentifier(bundleIdentifier);
            ValidateVersion(version);
            ValidateAppleDeveloperTeamId(appleDeveloperTeamId);
            if (!int.TryParse(buildNumber, out int parsedBuildNumber) ||
                parsedBuildNumber < 1)
                throw new BuildFailedException(
                    "MUKJUMP_IOS_BUILD_NUMBER는 1 이상의 정수여야 합니다.");

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
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = buildNumber;
            PlayerSettings.iOS.appleDeveloperTeamID = appleDeveloperTeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.Android.bundleVersionCode = parsedBuildNumber;
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
                $"{version} ({buildNumber})");
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

        static void ValidateLocalIosBuildInputs()
        {
            ValidateCommonStoreBuildInputs();
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.iOS,
                    BuildTarget.iOS))
                throw new BuildFailedException(
                    "Unity 6000.5.9f1 iOS Build Support가 설치되지 않았습니다. " +
                    "Unity Hub에서 모듈을 추가하세요.");
        }

        static void ValidateCommonStoreBuildInputs()
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new BuildFailedException(
                    "Build Settings에 활성화된 씬이 없습니다.");
            if (!scenes.Contains("Assets/Scenes/Main.unity"))
                throw new BuildFailedException(
                    "Assets/Scenes/Main.unity가 Build Settings에 포함되어야 합니다.");

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon == null)
                throw new BuildFailedException(
                    $"앱 아이콘을 찾을 수 없습니다: {AppIconPath}");
            if (icon.width != 1024 || icon.height != 1024)
                Debug.LogWarning(
                    $"[MukJump] 현재 앱 아이콘은 {icon.width}×{icon.height}입니다. " +
                    "App Store 제출 전 1024×1024 원본으로 교체하세요.");

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

            string[] unityServiceIssues = CollectUnityServiceIssues(
                UnityConnectSettingsPath);
            if (unityServiceIssues.Length > 0)
                throw new BuildFailedException(
                    "사용하지 않는 Unity 온라인 서비스가 켜져 있습니다:\n" +
                    string.Join("\n", unityServiceIssues));
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

        static void ValidateLocalAndroidBuildInputs()
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
                requireReleaseIdentity: true);
        }

        [MenuItem("MukJump/Release/Build iOS Local Validation Project")]
        public static void BuildIosLocalValidationProject()
        {
            ConfigureMobileStoreSettings();
            ValidateLocalIosBuildInputs();
            BuildIosProjectAt(
                IosValidationOutputPath,
                "iOS 로컬 검증 프로젝트",
                requireReleaseIdentity: false);
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
            string label)
        {
            string outputPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildTarget previousTarget =
                EditorUserBuildSettings.activeBuildTarget;
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
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Android,
                        BuildTarget.Android))
                    throw new BuildFailedException(
                        "Android 빌드 타깃으로 전환하지 못했습니다.");

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

                BuildReport report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = EnabledScenes(),
                        locationPathName = outputPath,
                        target = BuildTarget.Android,
                        options = options,
                    });
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
                RestoreBuildTarget(previousTarget);
            }
        }

        static void RestoreBuildTarget(BuildTarget target)
        {
            if (target == BuildTarget.NoTarget ||
                target == EditorUserBuildSettings.activeBuildTarget)
                return;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (group != BuildTargetGroup.Unknown)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        }

        static void BuildIosProjectAt(
            string relativePath,
            string label,
            bool requireReleaseIdentity)
        {
            string outputPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = EnabledScenes(),
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None,
                });

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
            string workspacePath = Path.Combine(
                outputPath,
                IosWorkspaceName);
            string lockPath = Path.Combine(outputPath, "Podfile.lock");
            string infoPlistPath = Path.Combine(outputPath, "Info.plist");
            string pbxProjectPath = Path.Combine(
                projectPath,
                "project.pbxproj");
            string entitlementsPath = Directory.Exists(outputPath)
                ? Directory.GetFiles(
                        outputPath,
                        "*.entitlements",
                        SearchOption.TopDirectoryOnly)
                    .FirstOrDefault()
                : null;

            if (!Directory.Exists(projectPath))
                issues.Add("Unity-iPhone.xcodeproj가 없습니다.");
            if (!Directory.Exists(workspacePath))
                issues.Add(
                    "Unity-iPhone.xcworkspace가 없습니다. CocoaPods 의존성 해석을 확인하세요.");
            if (!File.Exists(infoPlistPath))
                issues.Add("Info.plist가 없습니다.");
            else
            {
                string infoPlistText = File.ReadAllText(infoPlistPath);
                string expectedVersion = PlayerSettings.bundleVersion?.Trim() ??
                                         string.Empty;
                string expectedBuild = PlayerSettings.iOS.buildNumber?.Trim() ??
                                       string.Empty;
                if (expectedVersion.Length == 0 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        infoPlistText,
                        "<key>CFBundleShortVersionString</key>\\s*" +
                        $"<string>{System.Text.RegularExpressions.Regex.Escape(expectedVersion)}</string>"))
                    issues.Add(
                        $"iOS 마케팅 버전이 Unity 설정과 다릅니다: {expectedVersion}");
                if (expectedBuild.Length == 0 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        infoPlistText,
                        "<key>CFBundleVersion</key>\\s*" +
                        $"<string>{System.Text.RegularExpressions.Regex.Escape(expectedBuild)}</string>"))
                    issues.Add(
                        $"iOS 빌드 번호가 Unity 설정과 다릅니다: {expectedBuild}");
                if (infoPlistText.Contains(
                        "<key>NSUserTrackingUsageDescription</key>"))
                    issues.Add(
                        "ATT를 요청하지 않는 1.0 빌드에 NSUserTrackingUsageDescription이 남아 있습니다.");
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        infoPlistText,
                        "<key>ITSAppUsesNonExemptEncryption</key>\\s*" +
                        "<false\\s*/>"))
                    issues.Add(
                        "면제 암호화 선언 ITSAppUsesNonExemptEncryption=NO가 없습니다.");
                if (requireReleaseIdentity)
                {
                    System.Text.RegularExpressions.Match clientIdMatch =
                        System.Text.RegularExpressions.Regex.Match(
                            infoPlistText,
                            "<key>GIDClientID</key>\\s*<string>([^<]+)</string>");
                    if (!clientIdMatch.Success)
                    {
                        issues.Add("Google iOS Client ID가 Info.plist에 없습니다.");
                        issues.Add(
                            "Google 로그인 URL Scheme을 검증할 Client ID가 없습니다.");
                    }
                    else
                    {
                        string expectedUrlScheme =
                            ReverseGoogleClientId(clientIdMatch.Groups[1].Value);
                        if (expectedUrlScheme.Length == 0 ||
                            !infoPlistText.Contains(
                                $"<string>{expectedUrlScheme}</string>"))
                            issues.Add(
                                "Google 로그인 URL Scheme이 Client ID와 일치하지 않습니다: " +
                                expectedUrlScheme);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(entitlementsPath) ||
                !File.Exists(entitlementsPath))
            {
                issues.Add("Sign in with Apple entitlements 파일이 없습니다.");
            }
            else
            {
                string entitlementsText = File.ReadAllText(entitlementsPath);
                if (!entitlementsText.Contains(
                        "com.apple.developer.applesignin") ||
                    !entitlementsText.Contains("<string>Default</string>"))
                    issues.Add("Sign in with Apple entitlement가 없습니다.");
            }
            if (!File.Exists(pbxProjectPath))
            {
                issues.Add("Xcode project.pbxproj가 없습니다.");
            }
            else
            {
                string pbxText = File.ReadAllText(pbxProjectPath);
                if (!pbxText.Contains("AuthenticationServices.framework"))
                    issues.Add("AuthenticationServices.framework 연결이 없습니다.");
                if (!pbxText.Contains("CODE_SIGN_ENTITLEMENTS"))
                    issues.Add("Xcode 서명 대상에 entitlements가 연결되지 않았습니다.");
                string bundleIdentifier = PlayerSettings.GetApplicationIdentifier(
                    NamedBuildTarget.iOS);
                if (!string.IsNullOrWhiteSpace(bundleIdentifier) &&
                    !pbxText.Contains(
                        $"PRODUCT_BUNDLE_IDENTIFIER = {bundleIdentifier};"))
                    issues.Add(
                        $"Xcode 번들 ID가 Unity 설정과 다릅니다: {bundleIdentifier}");
                string teamId = PlayerSettings.iOS.appleDeveloperTeamID;
                if (!string.IsNullOrWhiteSpace(teamId) &&
                    !pbxText.Contains($"DEVELOPMENT_TEAM = {teamId};"))
                    issues.Add(
                        $"Xcode Apple Team ID가 Unity 설정과 다릅니다: {teamId}");
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

            string[] platforms = { "iOS", "Android" };
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

        static void ValidateAppleDeveloperTeamId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 10 ||
                value.Any(character => !char.IsLetterOrDigit(character)))
                throw new BuildFailedException(
                    $"Apple Developer Team ID는 공백 없는 10자리여야 합니다: {value}");
        }
    }
}
