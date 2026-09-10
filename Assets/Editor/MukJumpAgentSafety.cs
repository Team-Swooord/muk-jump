using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MukJump.EditorTools
{
    // 자동화 연결은 에디터에서만 사용한다. QA/출시 Player 모두 같은 차단 규칙을 적용한다.
    public sealed class MukJumpAgentSafety : IPreprocessBuildWithContext
    {
        public const string PackageId = "com.unity.pipeline";
        public const string AuditedPackageVersion = "0.6.0-exp.1";
        public const string SettingsPath =
            "ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json";

        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildCallbackContext context) => Validate();

        public static void Validate()
        {
            string[] issues = CollectIssues();
            if (issues.Length > 0)
                throw new BuildFailedException("먹점프 개발 도구 안전 검사 실패:\n" +
                                               string.Join("\n", issues));
        }

        internal static string InstalledPackageVersion() =>
            PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(package => package.name == PackageId)?.version;

        internal static string[] CollectIssues()
        {
            var issues = new List<string>();
            if (InstalledPackageVersion() != AuditedPackageVersion)
                issues.Add("Pipeline 버전이 검증된 고정 버전과 다릅니다. 호환성 검사를 먼저 실행하세요.");
            try
            {
                // Load()의 잘못된 JSON → null 폴백을 안전 판정으로 오인하지 않는다.
                issues.AddRange(InspectSettings(File.Exists(SettingsPath)
                    ? File.ReadAllText(SettingsPath) : null));
                // 중단된 빌드의 Resources 파일이나 수동 추가 파일도 자동 삭제하지 않고 알린다.
                foreach (string path in Directory.EnumerateFiles("Assets", "*.asset",
                             SearchOption.AllDirectories))
                    if (IsRuntimePipelineResource(path))
                        issues.Add("Player에 포함될 수 있는 개발 연결 설정이 남아 있습니다: " + path);
            }
            catch (Exception)
            {
                issues.Add("개발 연결 설정을 읽을 수 없습니다. 검사 실패 상태로 빌드하지 않습니다.");
            }
            return issues.ToArray();
        }

        internal static string[] InspectSettings(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new[] { "Player용 Pipeline 비활성화 설정이 없습니다." };
            try
            {
                JObject settings = JObject.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (!IsExplicitFalse(settings["enableInBuilds"]) ||
                    !IsExplicitFalse(settings["autoStart"]))
                    return new[] { "enableInBuilds와 autoStart는 모두 Boolean false여야 합니다." };
                return Array.Empty<string>();
            }
            catch (Exception)
            {
                return new[] { "Pipeline 설정 JSON이 올바르지 않습니다." };
            }
        }

        static bool IsExplicitFalse(JToken value) =>
            value != null && value.Type == JTokenType.Boolean && !value.Value<bool>();

        internal static bool IsRuntimePipelineResource(string path)
        {
            string normalized = path.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(normalized);
            return normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (name.Equals("RuntimePipelineConfig", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("RuntimePipelineBuildInfo", StringComparison.OrdinalIgnoreCase));
        }
    }
}
