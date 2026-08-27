using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 모바일 전용 뒤끝 SDK가 Apps in Toss WebGL 코드와 용량에 섞이지 않게 한다.
    public static class MukJumpPlatformPluginFilter
    {
        const string BackendRoot = "Assets/TheBackend";

        [MenuItem("MukJump/Store/Backend/Exclude Native SDKs From WebGL")]
        public static void EnsureBackendExcludedFromWebGL()
        {
            int changed = 0;
            foreach (string path in EnumerateBackendPluginPaths())
            {
                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null ||
                    importer.GetExcludeFromAnyPlatform(BuildTarget.WebGL))
                    continue;

                importer.SetExcludeFromAnyPlatform(BuildTarget.WebGL, true);
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log(
                changed == 0
                    ? "[MukJump] 뒤끝 SDK는 이미 WebGL에서 제외되어 있습니다."
                    : $"[MukJump] 뒤끝 플러그인 {changed}개를 WebGL에서 제외했습니다.");
        }

        public static List<string> CollectWebGLCompatibleBackendPlugins()
        {
            var issues = new List<string>();
            foreach (string path in EnumerateBackendPluginPaths())
            {
                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null)
                    continue;
                bool included = importer.GetCompatibleWithPlatform(
                        BuildTarget.WebGL) ||
                    importer.GetCompatibleWithAnyPlatform() &&
                    !importer.GetExcludeFromAnyPlatform(BuildTarget.WebGL);
                if (included)
                    issues.Add(path);
            }
            return issues;
        }

        static IEnumerable<string> EnumerateBackendPluginPaths()
        {
            if (!Directory.Exists(BackendRoot))
                yield break;

            string[] guids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { BackendRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }
    }
}
