using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MukJump.EditorTools
{
    /// 원본·GUID를 보존한 채 네이티브 설정을 WebGL의 Resources 수집에서 제외한다.
    public sealed class MukJumpWebNativeResourceScope : IDisposable
    {
        public const string HoldingFolder = "Assets/Editor/MukJumpWebExcluded";
        public static readonly string[] NativeResourcePaths =
        {
            "Assets/Resources/MukJump/Settings/MukJumpBackendSettings.asset",
            "Assets/Resources/MukJump/Settings/MukJumpGoogleAdsSettings.asset",
            "Assets/Resources/MukJump/Settings/MukJumpAnalyticsSettings.asset",
            "Assets/TheBackend/Resources/TheBackendSettings.asset",
            "Assets/TheBackend/Resources/TheBackendGoogleSettingsForAndroid.asset",
            "Assets/TheBackend/Resources/TheBackendGoogleSettingsForIOS.asset",
            "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset",
        };

        public MukJumpWebNativeResourceScope()
        {
            Restore();
            if (!AssetDatabase.IsValidFolder(HoldingFolder))
                AssetDatabase.CreateFolder("Assets/Editor", "MukJumpWebExcluded");
            try
            {
                foreach (string source in NativeResourcePaths)
                {
                    if (!File.Exists(source)) continue;
                    MoveOrThrow(source, HeldPath(source));
                }
                AssetDatabase.SaveAssets();
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public void Dispose() => Restore();

        [MenuItem("MukJump/Release/Restore Native Settings After Interrupted Web Build")]
        public static void Restore()
        {
            foreach (string target in NativeResourcePaths)
            {
                string source = HeldPath(target);
                if (!File.Exists(source)) continue;
                if (File.Exists(target))
                    throw new BuildFailedException("네이티브 설정 복원 대상이 이미 있습니다. 덮어쓰지 않습니다: " + target);
                MoveOrThrow(source, target);
            }
            AssetDatabase.SaveAssets();
            if (Directory.Exists(HoldingFolder) && Directory.GetFileSystemEntries(HoldingFolder).Length == 0)
                AssetDatabase.DeleteAsset(HoldingFolder);
        }

        public static bool IsNativeResource(string path) =>
            Array.Exists(NativeResourcePaths, candidate => string.Equals(candidate, path, StringComparison.Ordinal)) ||
            path.StartsWith(HoldingFolder + "/", StringComparison.Ordinal);

        static string HeldPath(string source) => HoldingFolder + "/" + Path.GetFileName(source);

        static void MoveOrThrow(string source, string target)
        {
            string error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
        }
    }

    /// 브라우저 산출물에 네이티브 설정이 한 개라도 섞이면 배포를 중단한다.
    public sealed class MukJumpWebPayloadGuard : IPostprocessBuildWithReport
    {
        public static readonly string[] NativeStreamingNames =
        {
            "google-services-desktop.json", "google-services.json", "GoogleService-Info.plist"
        };
        public int callbackOrder => 20000;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            var included = new List<string>();
            foreach (var packed in report.packedAssets)
                foreach (var entry in packed.contents)
                    if (MukJumpWebNativeResourceScope.IsNativeResource(entry.sourceAssetPath))
                        included.Add(entry.sourceAssetPath);
            if (included.Count > 0)
                throw new BuildFailedException("WebGL에 네이티브 설정이 포함됐습니다:\n" + string.Join("\n", included));
            RemoveNativeStreamingSettings(report.summary.outputPath);
        }

        public static void RemoveNativeStreamingSettings(string buildRoot)
        {
            // 원본 StreamingAssets는 보존한다. 확인된 WebGL 산출물의 정해진 파일만 제외한다.
            if (string.IsNullOrEmpty(buildRoot) || !File.Exists(Path.Combine(buildRoot, "index.html")))
                throw new BuildFailedException("WebGL 출력 폴더를 확인할 수 없습니다.");
            foreach (string name in NativeStreamingNames)
            {
                string path = Path.Combine(buildRoot, "StreamingAssets", name);
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
