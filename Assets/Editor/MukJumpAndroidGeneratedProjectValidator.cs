using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

namespace MukJump.EditorTools
{
    /// Unity가 만든 Android Gradle 프로젝트에 로그인·광고 네이티브 의존성이
    /// 실제로 포함됐는지 확인한다. EDM4U 오류를 빌드 성공으로 오인하지 않게 한다.
    public static class MukJumpAndroidGeneratedProjectValidator
    {
        public const string GoogleAdsPluginSettingsPath =
            "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        static readonly string[] RequiredGradleArtifacts =
        {
            "com.google.android.gms:play-services-ads",
            "com.google.android.ump:user-messaging-platform",
            "com.google.android.gms:play-services-auth",
        };

        static readonly string[] RequiredNativeLibraries =
        {
            "googlemobileads-unity.aar",
            "io.thebackend.googlelogin.aar",
        };

        public static string[] CollectIssues(
            string unityLibraryPath,
            string expectedAndroidAppId)
        {
            var issues = new List<string>();
            unityLibraryPath = ResolveUnityLibraryPath(unityLibraryPath);
            if (string.IsNullOrWhiteSpace(unityLibraryPath))
                return new[] { "생성된 unityLibrary 경로가 없습니다." };

            string gradlePath = Path.Combine(
                unityLibraryPath,
                "build.gradle");
            string gradle = ReadTextOrIssue(
                gradlePath,
                "unityLibrary/build.gradle이 없습니다.",
                issues);
            foreach (string artifact in RequiredGradleArtifacts)
            {
                if (!ContainsGradleDependency(gradle, artifact))
                    issues.Add(
                        $"Android 네이티브 의존성이 빠졌습니다: {artifact}");
            }
            if (!Regex.IsMatch(
                    RemoveGradleComments(gradle),
                    "(?m)^\\s*(implementation|api)\\s*\\(\\s*name\\s*:\\s*['\\\"]" +
                    "io\\.thebackend\\.googlelogin['\\\"]",
                    RegexOptions.CultureInvariant))
                issues.Add("뒤끝 Google 로그인 AAR 선언이 빠졌습니다.");

            string manifestPath = Path.Combine(
                unityLibraryPath,
                "GoogleMobileAdsPlugin.androidlib",
                "AndroidManifest.xml");
            string manifest = ReadTextOrIssue(
                manifestPath,
                "Google Mobile Ads AndroidManifest.xml이 없습니다.",
                issues);
            CollectManifestIssues(manifest, expectedAndroidAppId, issues);

            foreach (string library in RequiredNativeLibraries)
            {
                string libraryPath = Path.Combine(
                    unityLibraryPath,
                    "libs",
                    library);
                if (!File.Exists(libraryPath) ||
                    new FileInfo(libraryPath).Length == 0)
                    issues.Add($"Android 네이티브 라이브러리가 없습니다: {library}");
            }

            return issues.ToArray();
        }

        public static string ResolveUnityLibraryPath(string callbackPath)
        {
            if (string.IsNullOrWhiteSpace(callbackPath))
                return string.Empty;
            string path = Path.GetFullPath(callbackPath);
            if (Directory.Exists(path) &&
                string.Equals(
                    new DirectoryInfo(path).Name,
                    "unityLibrary",
                    StringComparison.OrdinalIgnoreCase))
                return path;

            string nested = Path.Combine(path, "unityLibrary");
            return Directory.Exists(nested) ? nested : string.Empty;
        }

        public static string ReadConfiguredAndroidAppId(string settingsPath)
        {
            if (string.IsNullOrWhiteSpace(settingsPath) ||
                !File.Exists(settingsPath))
                return string.Empty;

            foreach (string line in File.ReadAllLines(settingsPath))
            {
                string trimmed = line.Trim();
                const string prefix = "adMobAndroidAppId:";
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                return trimmed.Substring(prefix.Length).Trim();
            }

            return string.Empty;
        }

        static string ReadTextOrIssue(
            string path,
            string message,
            ICollection<string> issues)
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
            issues.Add(message);
            return string.Empty;
        }

        static bool ContainsGradleDependency(string gradle, string artifact)
        {
            string source = RemoveGradleComments(gradle);
            string pattern =
                "(?m)^\\s*(implementation|api)\\s*(?:\\(\\s*)?['\\\"]" +
                Regex.Escape(artifact) +
                ":[^'\\\"]+['\\\"]";
            return Regex.IsMatch(
                source,
                pattern,
                RegexOptions.CultureInvariant);
        }

        static string RemoveGradleComments(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            string withoutBlocks = Regex.Replace(
                source,
                @"/\*.*?\*/",
                string.Empty,
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            return Regex.Replace(
                withoutBlocks,
                @"//.*$",
                string.Empty,
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        static void CollectManifestIssues(
            string manifest,
            string expectedAndroidAppId,
            ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(manifest)) return;
            XDocument document;
            try
            {
                document = XDocument.Parse(manifest);
            }
            catch (Exception)
            {
                issues.Add("Google Mobile Ads AndroidManifest.xml이 올바르지 않습니다.");
                return;
            }

            XNamespace android =
                "http://schemas.android.com/apk/res/android";
            XElement[] appIds = document
                .Descendants("meta-data")
                .Where(element => string.Equals(
                    element.Attribute(android + "name")?.Value,
                    "com.google.android.gms.ads.APPLICATION_ID",
                    StringComparison.Ordinal))
                .ToArray();
            if (appIds.Length != 1)
            {
                issues.Add(
                    "AdMob APPLICATION_ID 메타데이터는 정확히 하나여야 합니다.");
                return;
            }

            string manifestAppId =
                appIds[0].Attribute(android + "value")?.Value?.Trim() ??
                string.Empty;
            if (string.IsNullOrWhiteSpace(expectedAndroidAppId))
                issues.Add("빌드에 사용할 AdMob Android 앱 ID가 비어 있습니다.");
            else if (!string.Equals(
                         manifestAppId,
                         expectedAndroidAppId.Trim(),
                         StringComparison.Ordinal))
                issues.Add("AdMob Android 앱 ID가 설정 에셋과 다릅니다.");
        }
    }

    /// Google Mobile Ads의 후처리가 끝난 뒤, Gradle 실행 전에 검증한다.
    public sealed class MukJumpAndroidGradlePostprocessor :
        IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string appId = MukJumpAndroidGeneratedProjectValidator
                .ReadConfiguredAndroidAppId(
                    MukJumpAndroidGeneratedProjectValidator
                        .GoogleAdsPluginSettingsPath);
            string[] issues = MukJumpAndroidGeneratedProjectValidator
                .CollectIssues(path, appId);
            if (issues.Length > 0)
                throw new BuildFailedException(
                    "Android 광고·로그인 네이티브 구성이 완전하지 않습니다:\n" +
                    string.Join("\n", issues));
        }
    }
}
