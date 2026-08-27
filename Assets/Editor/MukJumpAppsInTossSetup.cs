using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AppsInToss;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MukJump.EditorTools
{
    public static class MukJumpAppsInTossSetup
    {
        public const string DefaultAppName = "muk-jump";
        public const string AdSettingsPath =
            "Assets/Resources/MukJump/Settings/AppsInTossAdSettings.asset";
        public const string BackendToolkitAssemblyPath =
            "Assets/TheBackend/Toolkit/MukJump.TheBackend.Toolkit.asmdef";
        public const long MaximumUnpackedBytes = 100L * 1024L * 1024L;
        const string PendingBuildKey = "MukJump.AIT.PendingBuild";
        const string PreviousBuildTargetKey =
            "MukJump.AIT.PreviousBuildTarget";

        [MenuItem("MukJump/Release/Configure Apps in Toss Settings")]
        public static void Configure()
        {
            AITEditorScriptObject config = UnityUtil.GetEditorConf();
            config.appName = Environment.GetEnvironmentVariable(
                "MUKJUMP_AIT_APP_NAME") ?? DefaultAppName;
            config.displayName = "먹점프";
            config.version = PlayerSettings.bundleVersion;
            config.description =
                "손가락으로 먹선을 그려 작은 먹방울을 위로 올리는 세로형 드로잉 점프 게임";
            config.primaryColor = "#AE1C3C";
            config.bridgeColorMode = 0;
            config.webViewType = 0;
            config.navigationBarTransparentBackground = true;
            config.runInBackground = 0;

            string iconUrl = Environment.GetEnvironmentVariable(
                "MUKJUMP_AIT_ICON_URL");
            if (!string.IsNullOrWhiteSpace(iconUrl))
                config.iconUrl = iconUrl.Trim();

            EditorUtility.SetDirty(config);
            ConfigureAdSettings();
            MukJumpPlatformPluginFilter.EnsureBackendExcludedFromWebGL();
            AssetDatabase.SaveAssets();

            if (string.IsNullOrWhiteSpace(config.iconUrl))
                Debug.LogWarning(
                    "[MukJump] Apps in Toss 아이콘 URL이 비어 있습니다. " +
                    "MUKJUMP_AIT_ICON_URL 또는 AIT > Configuration에서 입력하세요.");

            Debug.Log(
                $"[MukJump] Apps in Toss 설정 완료: {config.appName} / " +
                $"{config.displayName} / {config.primaryColor}");
        }

        [MenuItem("MukJump/Release/Build Apps in Toss Package")]
        public static void BuildPackage()
        {
            Configure();
            ValidateReadinessOrThrow();
            StartOrRunBuild("package");
        }

        [MenuItem("MukJump/Release/Build Apps in Toss Size Probe")]
        public static void BuildSizeProbe()
        {
            Configure();
            StartOrRunBuild("probe");
        }

        [MenuItem("MukJump/Release/Return to iOS Build Target")]
        public static void ReturnToIosBuildTarget()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.iOS,
                    BuildTarget.iOS))
                throw new BuildFailedException("iOS 빌드 타깃 전환에 실패했습니다.");
        }

        [InitializeOnLoadMethod]
        static void ResumeBuildAfterTargetSwitch()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(PendingBuildKey, "")))
                return;
            EditorApplication.delayCall += ResumePendingBuildWhenReady;
        }

        static void ResumePendingBuildWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ResumePendingBuildWhenReady;
                return;
            }

            string action = SessionState.GetString(PendingBuildKey, "");
            if (string.IsNullOrEmpty(action))
                return;
            BuildTarget previousTarget = (BuildTarget)SessionState.GetInt(
                PreviousBuildTargetKey,
                (int)BuildTarget.iOS);
            SessionState.EraseString(PendingBuildKey);
            SessionState.EraseInt(PreviousBuildTargetKey);
            RunBuild(action, previousTarget);
        }

        static void StartOrRunBuild(string action)
        {
            BuildTarget current = EditorUserBuildSettings.activeBuildTarget;
            if (current == BuildTarget.WebGL)
            {
                RunBuild(action, current);
                return;
            }

            SessionState.SetString(PendingBuildKey, action);
            SessionState.SetInt(PreviousBuildTargetKey, (int)current);
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL))
            {
                SessionState.EraseString(PendingBuildKey);
                SessionState.EraseInt(PreviousBuildTargetKey);
                throw new BuildFailedException(
                    "Apps in Toss 빌드를 위한 WebGL 전환에 실패했습니다.");
            }
        }

        static void RunBuild(string action, BuildTarget previousTarget)
        {
            bool package = action == "package";
            AITEditorScriptObject config = UnityUtil.GetEditorConf();
            try
            {
                AITConvertCore.AITExportError result = AITConvertCore.DoExport(
                    buildWebGL: true,
                    doPackaging: package,
                    cleanBuild: true,
                    profile: config.productionProfile,
                    profileName: package
                        ? "MukJump Release"
                        : "MukJump Size Probe");
                if (result != AITConvertCore.AITExportError.SUCCEED)
                    throw new BuildFailedException(
                        package
                            ? $"Apps in Toss 패키징 실패: {result}"
                            : $"Apps in Toss 용량 측정 빌드 실패: {result}");

                ValidateBuiltPayloadOrThrow(
                    package ? "ait-build/public" : AITConvertCore.webglDir,
                    allowPackagingTokens: !package);
                if (package)
                {
                    string aitPath = FindAitPackagePath();
                    Debug.Log(
                        $"[MukJump] Apps in Toss 패키징 완료: {aitPath} " +
                        $"({FormatBytes(new FileInfo(aitPath).Length)})");
                }
            }
            finally
            {
                RestoreBuildTarget(
                    BuildPipeline.GetBuildTargetGroup(previousTarget),
                    previousTarget);
            }
        }

        [MenuItem("MukJump/Release/Validate Apps in Toss Readiness")]
        public static void ValidateReadiness()
        {
            Configure();
            ValidateReadinessOrThrow();
            Debug.Log("[MukJump] Apps in Toss 출시 설정 검증 완료");
        }

        public static List<string> CollectReleaseIssues()
        {
            var issues = new List<string>();
            AITEditorScriptObject config = UnityUtil.GetEditorConf();
            if (config == null)
                return new List<string> { "• Apps in Toss Configuration이 없습니다." };
            if (!string.Equals(
                    config.appName,
                    DefaultAppName,
                    StringComparison.Ordinal))
                issues.Add("• appName은 콘솔과 같은 muk-jump여야 합니다.");
            if (config.displayName != "먹점프")
                issues.Add("• 표시 이름은 먹점프여야 합니다.");
            if (!Uri.TryCreate(config.iconUrl, UriKind.Absolute, out Uri icon) ||
                icon.Scheme != Uri.UriSchemeHttps)
                issues.Add("• 공개 HTTPS 아이콘 URL을 입력해야 합니다.");
            if (config.primaryColor != "#AE1C3C")
                issues.Add("• 기본 색상은 #AE1C3C여야 합니다.");
            if (config.productionProfile == null ||
                config.productionProfile.enableMockBridge ||
                config.productionProfile.enableDebugConsole ||
                config.productionProfile.developmentBuild)
                issues.Add("• Production 프로필의 Mock·디버그·개발 빌드를 꺼야 합니다.");
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.WebGL,
                    BuildTarget.WebGL))
                issues.Add("• Unity WebGL Build Support가 설치되지 않았습니다.");
            if (!EditorBuildSettings.scenes.Any(scene =>
                    scene.enabled && scene.path == "Assets/Scenes/Main.unity"))
                issues.Add("• Main.unity를 Build Settings에 활성화해야 합니다.");

            AppsInTossAdSettings adSettings =
                AssetDatabase.LoadAssetAtPath<AppsInTossAdSettings>(
                    AdSettingsPath);
            if (adSettings == null ||
                string.IsNullOrWhiteSpace(adSettings.RewardedAdGroupId))
                issues.Add("• 토스 보상형 광고 그룹 ID를 입력해야 합니다.");
            if (adSettings == null ||
                string.IsNullOrWhiteSpace(adSettings.BannerAdGroupId))
                issues.Add("• 토스 배너 광고 그룹 ID를 입력해야 합니다.");
            issues.AddRange(CollectAdPolicyIssues(adSettings));

            List<string> backendPlugins =
                MukJumpPlatformPluginFilter
                    .CollectWebGLCompatibleBackendPlugins();
            if (backendPlugins.Count > 0)
                issues.Add(
                    $"• 뒤끝 모바일 DLL {backendPlugins.Count}개가 WebGL에 포함됩니다.");
            if (!IsBackendToolkitExcludedFromWebGL())
                issues.Add("• 뒤끝 Toolkit C# assembly를 WebGL에서 제외해야 합니다.");
            return issues;
        }

        public static List<string> CollectAdPolicyIssues(
            AppsInTossAdSettings adSettings)
        {
            var issues = new List<string>();
            if (adSettings != null &&
                !string.IsNullOrWhiteSpace(adSettings.InterstitialAdGroupId))
            {
                issues.Add(
                    "• 먹점프 1.0은 강제 전면 광고를 사용하지 않습니다. " +
                    "토스 전면 광고 그룹 ID를 비워야 합니다.");
            }
            return issues;
        }

        public static bool IsBackendToolkitExcludedFromWebGL()
        {
            if (!File.Exists(BackendToolkitAssemblyPath))
                return false;
            string json = File.ReadAllText(BackendToolkitAssemblyPath);
            return json.Contains("\"excludePlatforms\"") &&
                   json.Contains("\"WebGL\"");
        }

        public static long CalculateDirectorySize(string directory)
        {
            if (!Directory.Exists(directory))
                return 0L;
            return Directory.EnumerateFiles(
                    directory,
                    "*",
                    SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length);
        }

        public static string FormatBytes(long bytes) =>
            $"{bytes / (1024d * 1024d):0.00} MB";

        public static List<string> CollectBuiltPayloadIssues(
            string directory,
            bool allowPackagingTokens = false)
        {
            var issues = new List<string>();
            string indexPath = ResolveBuiltIndexPath(directory);
            if (string.IsNullOrEmpty(indexPath))
            {
                issues.Add("• Apps in Toss 산출물의 index.html을 찾을 수 없습니다.");
                return issues;
            }

            string html = File.ReadAllText(indexPath);
            // SDK는 WebGL 원본의 토큰을 패키징 단계에서 Production 값으로 치환한다.
            // 따라서 용량 측정용 원본 빌드에서는 토큰을 허용하되, 최종 .ait 공개
            // 디렉터리에서는 미치환 토큰을 반드시 실패시킨다.
            if (!allowPackagingTokens &&
                html.Contains("%AIT_ENABLE_DEBUG_CONSOLE%"))
                issues.Add("• 디버그 콘솔 빌드 토큰이 치환되지 않았습니다.");
            if (html.Contains("if ('true' !== 'true') return"))
                issues.Add("• 출시 산출물에서 vConsole이 활성화되어 있습니다.");
            return issues;
        }

        static void ValidateReadinessOrThrow()
        {
            List<string> issues = CollectReleaseIssues();
            if (issues.Count > 0)
                throw new BuildFailedException(
                    "Apps in Toss 출시 설정이 끝나지 않았습니다:\n" +
                    string.Join("\n", issues));
        }

        static void ValidateBuiltPayloadOrThrow(
            string directory,
            bool allowPackagingTokens)
        {
            long bytes = CalculateDirectorySize(directory);
            if (bytes <= 0L)
                throw new BuildFailedException(
                    $"Apps in Toss 빌드 결과가 없습니다: {directory}");
            if (bytes > MaximumUnpackedBytes)
                throw new BuildFailedException(
                    $"Apps in Toss 압축 해제 기준 용량이 {FormatBytes(bytes)}로 " +
                    "100MB 제한을 초과합니다.");

            List<string> issues = CollectBuiltPayloadIssues(
                directory,
                allowPackagingTokens);
            if (issues.Count > 0)
                throw new BuildFailedException(
                    "Apps in Toss 출시 산출물 검증에 실패했습니다:\n" +
                    string.Join("\n", issues));
            Debug.Log(
                $"[MukJump] Apps in Toss 압축 해제 기준 용량: " +
                $"{FormatBytes(bytes)} / 100MB");
        }

        static string ResolveBuiltIndexPath(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            string fullDirectory = Path.GetFullPath(directory);
            string direct = Path.Combine(fullDirectory, "index.html");
            if (File.Exists(direct))
                return direct;

            DirectoryInfo parent = Directory.GetParent(fullDirectory);
            if (parent == null)
                return null;
            string parentIndex = Path.Combine(parent.FullName, "index.html");
            return File.Exists(parentIndex) ? parentIndex : null;
        }

        static string FindAitPackagePath()
        {
            string root = Path.GetFullPath("ait-build");
            if (!Directory.Exists(root))
                throw new BuildFailedException("ait-build 폴더가 없습니다.");
            string path = Directory.EnumerateFiles(
                    root,
                    "*.ait",
                    SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(path))
                throw new BuildFailedException("생성된 .ait 파일을 찾지 못했습니다.");
            return path;
        }

        static void RestoreBuildTarget(
            BuildTargetGroup group,
            BuildTarget target)
        {
            if (target == EditorUserBuildSettings.activeBuildTarget)
                return;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                Debug.LogError(
                    $"[MukJump] 원래 빌드 타깃 {target} 복원에 실패했습니다.");
        }

        static void ConfigureAdSettings()
        {
            AppsInTossAdSettings settings =
                AssetDatabase.LoadAssetAtPath<AppsInTossAdSettings>(
                    AdSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AdSettingsPath));
                settings = ScriptableObject.CreateInstance<AppsInTossAdSettings>();
                AssetDatabase.CreateAsset(settings, AdSettingsPath);
            }

            string rewardedId = Environment.GetEnvironmentVariable(
                "MUKJUMP_AIT_REWARDED_AD_ID");
            string interstitialId = Environment.GetEnvironmentVariable(
                "MUKJUMP_AIT_INTERSTITIAL_AD_ID");
            string bannerId = Environment.GetEnvironmentVariable(
                "MUKJUMP_AIT_BANNER_AD_ID");
            if (!string.IsNullOrWhiteSpace(rewardedId) ||
                !string.IsNullOrWhiteSpace(interstitialId) ||
                !string.IsNullOrWhiteSpace(bannerId))
            {
                settings.Configure(
                    string.IsNullOrWhiteSpace(rewardedId)
                        ? settings.RewardedAdGroupId
                        : rewardedId,
                    string.IsNullOrWhiteSpace(interstitialId)
                        ? settings.InterstitialAdGroupId
                        : interstitialId,
                    string.IsNullOrWhiteSpace(bannerId)
                        ? settings.BannerAdGroupId
                        : bannerId);
                EditorUtility.SetDirty(settings);
            }
        }
    }
}
