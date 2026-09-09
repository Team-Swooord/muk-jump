using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AppsInToss;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
        public const string TemplateIndexPath =
            "Assets/WebGLTemplates/AITTemplate/index.html";
        public const string RuntimePatchSourcePath =
            "Assets/Editor/ait-patch-cli.mjs";
        public const string TemplateRuntimePatchPath =
            "Assets/WebGLTemplates/AITTemplate/BuildConfig~/ait-patch-cli.mjs";
        public const string FinalWebBundlePath = "ait-build/dist/web";
        public const string ExpectedAitRuntimeVersion = "0.84.0";
        public const string FullscreenStyleId = "mukjump-fullscreen-webgl";
        public const string CanonicalFullscreenStyle =
            "    <style id=\"mukjump-fullscreen-webgl\">\n" +
            "        html,\n" +
            "        body {\n" +
            "            width: 100%;\n" +
            "            height: 100%;\n" +
            "            margin: 0;\n" +
            "            padding: 0;\n" +
            "            background: #EAE3D2 !important;\n" +
            "            overflow: hidden;\n" +
            "            overscroll-behavior: none;\n" +
            "        }\n\n" +
            "        #unity-container {\n" +
            "            position: fixed;\n" +
            "            inset: 0;\n" +
            "            width: 100vw;\n" +
            "            height: 100vh;\n" +
            "            height: 100dvh;\n" +
            "            background: #EAE3D2 !important;\n" +
            "        }\n\n" +
            "        #unity-canvas {\n" +
            "            display: block;\n" +
            "            width: 100% !important;\n" +
            "            height: 100% !important;\n" +
            "            background: #EAE3D2 !important;\n" +
            "            touch-action: none;\n" +
            "        }\n" +
            "    </style>";
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
            Dictionary<string, string> previousAitArtifacts = package
                ? CaptureAitArtifactHashes("ait-build")
                : null;
            try
            {
                PrepareFullscreenTemplateOrThrow();
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
                    package ? FinalWebBundlePath : AITConvertCore.webglDir,
                    allowPackagingTokens: !package);
                if (package)
                {
                    string aitPath = FindFreshAitPackagePath(
                        "ait-build",
                        previousAitArtifacts);
                    if (string.IsNullOrWhiteSpace(aitPath))
                        throw new BuildFailedException(
                            "이번 실행에서 새로 생성되거나 변경된 .ait 패키지가 없습니다. " +
                            "이전 산출물을 성공으로 오인하지 않도록 빌드를 중단합니다.");
                    if (!TryReadAitRuntimeVersion(
                            aitPath,
                            out string runtimeVersion,
                            out string metadataError) ||
                        !string.Equals(
                            runtimeVersion,
                            ExpectedAitRuntimeVersion,
                            StringComparison.Ordinal))
                        throw new BuildFailedException(
                            "Apps in Toss .ait runtimeVersion 검증에 실패했습니다. " +
                            $"기대값={ExpectedAitRuntimeVersion}, " +
                            $"실제값={runtimeVersion ?? "(없음)"}. {metadataError}");
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
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (enabledScenes.Length < 2 ||
                enabledScenes[0] != MukJumpSplashSceneBuilder.ScenePath ||
                enabledScenes[1] != MukJumpSplashSceneBuilder.MainScenePath)
                issues.Add("• 빌드 씬 순서는 Splash → Main이어야 합니다.");

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
            if (adSettings != null && IsTestAdGroupId(
                    adSettings.RewardedAdGroupId))
                issues.Add(
                    "• Production 토스 보상형 광고에 공식 테스트 광고 ID를 사용할 수 없습니다.");
            if (adSettings != null && IsTestAdGroupId(
                    adSettings.BannerAdGroupId))
                issues.Add(
                    "• Production 토스 배너 광고에 공식 테스트 광고 ID를 사용할 수 없습니다.");
            if (adSettings != null &&
                !string.IsNullOrWhiteSpace(adSettings.InterstitialAdGroupId))
            {
                issues.Add(
                    "• 먹점프 1.0은 강제 전면 광고를 사용하지 않습니다. " +
                    "토스 전면 광고 그룹 ID를 비워야 합니다.");
            }
            return issues;
        }

        public static bool IsTestAdGroupId(string value) =>
            string.Equals(
                value?.Trim(),
                AppsInTossAdRuntime.TestRewardedId,
                StringComparison.Ordinal) ||
            string.Equals(
                value?.Trim(),
                AppsInTossAdRuntime.TestBannerId,
                StringComparison.Ordinal);

        public static bool IsBackendToolkitExcludedFromWebGL()
        {
            if (!File.Exists(BackendToolkitAssemblyPath))
                return false;
            string json = File.ReadAllText(BackendToolkitAssemblyPath);
            return json.Contains("\"excludePlatforms\"") &&
                   json.Contains("\"WebGL\"");
        }

        /// SDK가 매번 생성하는 ignored 템플릿에 먹점프 전용 전체화면 규칙을
        /// 재주입한다. 변환 결과는 동일 입력에 대해 항상 byte-identical이다.
        public static void PrepareFullscreenTemplateOrThrow()
        {
            bool changed = AITConvertCore.EnsureWebGLTemplatesExist();
            changed |= CopyRuntimeVersionPatchOrThrow();
            if (!File.Exists(TemplateIndexPath))
                throw new BuildFailedException(
                    $"Apps in Toss WebGL 템플릿을 찾을 수 없습니다: {TemplateIndexPath}");

            string source = File.ReadAllText(TemplateIndexPath);
            if (!TryApplyFullscreenTemplate(
                    source,
                    out string updated,
                    out string error))
                throw new BuildFailedException(error);
            if (!string.Equals(source, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(
                    TemplateIndexPath,
                    updated,
                    new UTF8Encoding(false));
                changed = true;
            }

            if (!changed)
                return;
            // ignored 템플릿이 없는 clean checkout에서도 PROJECT:AITTemplate을
            // 같은 빌드에서 인식하도록 SDK 초기화와 동일한 동기 Refresh를 수행한다.
            EditorApplication.LockReloadAssemblies();
            try
            {
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        static bool CopyRuntimeVersionPatchOrThrow()
        {
            if (!File.Exists(RuntimePatchSourcePath))
                throw new BuildFailedException(
                    "추적 가능한 Apps in Toss runtimeVersion 패치 원본이 없습니다: " +
                    RuntimePatchSourcePath);
            string directory = Path.GetDirectoryName(TemplateRuntimePatchPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            byte[] source = File.ReadAllBytes(RuntimePatchSourcePath);
            if (File.Exists(TemplateRuntimePatchPath) &&
                source.SequenceEqual(
                    File.ReadAllBytes(TemplateRuntimePatchPath)))
                return false;
            File.Copy(
                RuntimePatchSourcePath,
                TemplateRuntimePatchPath,
                overwrite: true);
            return true;
        }

        public static bool TryApplyFullscreenTemplate(
            string source,
            out string updated,
            out string error)
        {
            const string endMarker = "<!-- USER_HEAD_END -->";
            updated = source ?? string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                error = "Apps in Toss index.html이 비어 있습니다.";
                return false;
            }

            int firstMarker = source.IndexOf(
                endMarker,
                StringComparison.Ordinal);
            if (firstMarker < 0 || firstMarker != source.LastIndexOf(
                    endMarker,
                    StringComparison.Ordinal))
            {
                error =
                    "Apps in Toss index.html의 USER_HEAD_END 마커가 없거나 중복되었습니다.";
                return false;
            }

            string stylePattern =
                @"[ \t]*<style\b(?=[^>]*\bid\s*=\s*[""']" +
                Regex.Escape(FullscreenStyleId) +
                @"[""'])[^>]*>.*?</style>[ \t]*(?:\r?\n)?";
            string withoutExisting = Regex.Replace(
                source,
                stylePattern,
                string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int marker = withoutExisting.IndexOf(
                endMarker,
                StringComparison.Ordinal);
            string prefix = withoutExisting.Substring(0, marker).TrimEnd();
            string suffix = withoutExisting.Substring(marker).TrimStart();
            updated = prefix + "\n\n" + CanonicalFullscreenStyle +
                      "\n    " + suffix;
            return true;
        }

        public static Dictionary<string, string> CaptureAitArtifactHashes(
            string directory)
        {
            var snapshots = new Dictionary<string, string>(
                StringComparer.Ordinal);
            if (!Directory.Exists(directory))
                return snapshots;
            foreach (string path in Directory.EnumerateFiles(
                         directory,
                         "*.ait",
                         SearchOption.AllDirectories))
                snapshots[Path.GetFullPath(path)] = ComputeFileSha256(path);
            return snapshots;
        }

        public static string FindFreshAitPackagePath(
            string directory,
            IReadOnlyDictionary<string, string> previousArtifacts)
        {
            if (!Directory.Exists(directory))
                return null;
            previousArtifacts ??= new Dictionary<string, string>();
            return Directory.EnumerateFiles(
                    directory,
                    "*.ait",
                    SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path =>
                    !previousArtifacts.TryGetValue(path, out string oldHash) ||
                    !string.Equals(
                        oldHash,
                        ComputeFileSha256(path),
                        StringComparison.Ordinal))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        public static bool TryReadAitRuntimeVersion(
            string path,
            out string runtimeVersion,
            out string error)
        {
            runtimeVersion = null;
            error = string.Empty;
            try
            {
                using FileStream stream = File.OpenRead(path);
                var header = new byte[20];
                if (!ReadExactly(stream, header, header.Length))
                {
                    error = ".ait 헤더가 잘렸습니다.";
                    return false;
                }
                byte[] magic = Encoding.ASCII.GetBytes("AITBUNDL");
                for (int i = 0; i < magic.Length; i++)
                {
                    if (header[i] == magic[i])
                        continue;
                    error = ".ait magic이 올바르지 않습니다.";
                    return false;
                }

                ulong bundleLength = 0UL;
                for (int i = 12; i < 20; i++)
                    bundleLength = (bundleLength << 8) | header[i];
                if (bundleLength == 0UL ||
                    bundleLength > int.MaxValue ||
                    bundleLength > (ulong)Math.Max(0L, stream.Length - 20L))
                {
                    error = ".ait protobuf 길이가 올바르지 않습니다.";
                    return false;
                }

                var bundle = new byte[(int)bundleLength];
                if (!ReadExactly(stream, bundle, bundle.Length))
                {
                    error = ".ait protobuf가 잘렸습니다.";
                    return false;
                }
                if (!TryFindLengthDelimitedField(
                        bundle,
                        0,
                        bundle.Length,
                        4,
                        out int metadataStart,
                        out int metadataLength,
                        out error))
                    return false;
                if (!TryFindLengthDelimitedField(
                        bundle,
                        metadataStart,
                        metadataStart + metadataLength,
                        3,
                        out int runtimeStart,
                        out int runtimeLength,
                        out error))
                    return false;

                runtimeVersion = Encoding.UTF8.GetString(
                    bundle,
                    runtimeStart,
                    runtimeLength);
                if (string.IsNullOrWhiteSpace(runtimeVersion))
                {
                    error = ".ait metadata.runtimeVersion이 비어 있습니다.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }
            return true;
        }

        static bool TryFindLengthDelimitedField(
            byte[] bytes,
            int start,
            int end,
            int targetField,
            out int valueStart,
            out int valueLength,
            out string error)
        {
            valueStart = 0;
            valueLength = 0;
            error = string.Empty;
            int offset = start;
            while (offset < end)
            {
                if (!TryReadVarint(bytes, ref offset, end, out ulong tag) ||
                    tag == 0UL)
                {
                    error = ".ait protobuf tag를 읽을 수 없습니다.";
                    return false;
                }
                int field = (int)(tag >> 3);
                int wire = (int)(tag & 7UL);
                if (wire == 2)
                {
                    if (!TryReadVarint(
                            bytes,
                            ref offset,
                            end,
                            out ulong rawLength) ||
                        rawLength > int.MaxValue ||
                        rawLength > (ulong)(end - offset))
                    {
                        error = ".ait protobuf 문자열 길이가 올바르지 않습니다.";
                        return false;
                    }
                    int length = (int)rawLength;
                    if (field == targetField)
                    {
                        valueStart = offset;
                        valueLength = length;
                        return true;
                    }
                    offset += length;
                    continue;
                }

                int fixedBytes;
                switch (wire)
                {
                    case 0:
                        if (!TryReadVarint(bytes, ref offset, end, out _))
                        {
                            error = ".ait protobuf varint가 잘렸습니다.";
                            return false;
                        }
                        continue;
                    case 1:
                        fixedBytes = 8;
                        break;
                    case 5:
                        fixedBytes = 4;
                        break;
                    default:
                        error = $"지원하지 않는 protobuf wire type입니다: {wire}";
                        return false;
                }
                if (fixedBytes > end - offset)
                {
                    error = ".ait protobuf 필드가 잘렸습니다.";
                    return false;
                }
                offset += fixedBytes;
            }
            error = $".ait protobuf field {targetField}를 찾지 못했습니다.";
            return false;
        }

        static bool TryReadVarint(
            byte[] bytes,
            ref int offset,
            int end,
            out ulong value)
        {
            value = 0UL;
            int shift = 0;
            while (offset < end && shift <= 63)
            {
                byte current = bytes[offset++];
                value |= (ulong)(current & 0x7f) << shift;
                if ((current & 0x80) == 0)
                    return true;
                shift += 7;
            }
            return false;
        }

        static string ComputeFileSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] digest = sha256.ComputeHash(stream);
            return BitConverter.ToString(digest)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
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
            if (!allowPackagingTokens)
            {
                string[] textExtensions =
                {
                    ".html", ".js", ".mjs", ".css", ".json",
                };
                foreach (string path in Directory.EnumerateFiles(
                             directory,
                             "*",
                             SearchOption.AllDirectories)
                         .Where(path => textExtensions.Contains(
                             Path.GetExtension(path),
                             StringComparer.OrdinalIgnoreCase)))
                {
                    try
                    {
                        if (Regex.IsMatch(
                                File.ReadAllText(path),
                                @"%(?:AIT|UNITY)_[A-Z0-9_]+%"))
                            issues.Add(
                                "• 출시 산출물에 미치환 빌드 토큰이 남았습니다: " +
                                Path.GetRelativePath(directory, path));
                    }
                    catch (Exception exception)
                    {
                        issues.Add(
                            "• 출시 산출물 텍스트를 검증할 수 없습니다: " +
                            $"{Path.GetRelativePath(directory, path)} ({exception.Message})");
                    }
                }

                string bridgePath = Path.Combine(
                    directory,
                    "Runtime",
                    "appsintoss-unity-bridge.js");
                if (!File.Exists(bridgePath))
                    issues.Add("• Production Apps in Toss 브리지 파일이 없습니다.");
                else if (!File.ReadAllText(bridgePath).Contains(
                             "var AIT_BUILD_IS_PRODUCTION = 'true';"))
                    issues.Add(
                        "• Apps in Toss 브리지가 Production 모드로 치환되지 않았습니다.");
            }
            if (html.Contains("if ('true' !== 'true') return"))
                issues.Add("• 출시 산출물에서 vConsole이 활성화되어 있습니다.");
            int fullscreenStyleCount = Regex.Matches(
                html,
                $@"id\s*=\s*[""']{Regex.Escape(FullscreenStyleId)}[""']",
                RegexOptions.IgnoreCase).Count;
            if (fullscreenStyleCount != 1)
                issues.Add(
                    $"• 먹점프 전체화면 스타일은 정확히 1개여야 합니다: {fullscreenStyleCount}개");
            if (!Regex.IsMatch(
                    html,
                    @"height\s*:\s*100dvh",
                    RegexOptions.IgnoreCase) ||
                !Regex.IsMatch(
                    html,
                    @"background\s*:\s*#EAE3D2",
                    RegexOptions.IgnoreCase))
                issues.Add(
                    "• 먹점프 전체화면 높이 또는 한지 배경 규칙이 빠졌습니다.");
            return issues.Distinct().ToList();
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

            return null;
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

    /// SDK 메뉴를 우회한 WebGL BuildPipeline 호출에서도 ignored 템플릿이
    /// 생성·갱신된 직후 먹점프 전체화면 규칙을 반드시 복원한다.
    public sealed class MukJumpAppsInTossTemplateBuildGuard :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -1900;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;
            MukJumpAppsInTossSetup.PrepareFullscreenTemplateOrThrow();
        }
    }
}
