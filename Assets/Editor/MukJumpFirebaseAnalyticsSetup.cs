using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace MukJump.EditorTools
{
    /// 설정 파일·번들 일치를 빌드 전에 검증하고 네이티브 자동 수집도 기본 거부한다.
    [InitializeOnLoad]
    public sealed class MukJumpFirebaseAnalyticsSetup : IPreprocessBuildWithReport
    {
        public const string SettingsPath = "Assets/Resources/MukJump/Settings/MukJumpAnalyticsSettings.asset";
        public int callbackOrder => -1100;
        static MukJumpFirebaseAnalyticsSetup() => EditorApplication.delayCall += RefreshConfiguration;

        [MenuItem("MukJump/Store/Firebase/설정 확인")]
        public static void RefreshConfiguration()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MukJumpAnalyticsSettings>(SettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<MukJumpAnalyticsSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            bool ios = HasValidConfig(BuildTarget.iOS);
            bool android = HasValidConfig(BuildTarget.Android);
            if (settings.iosConfigured == ios && settings.androidConfigured == android) return;
            settings.iosConfigured = ios;
            settings.androidConfigured = android;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        public static bool HasValidConfig(BuildTarget target)
        {
            string path = target == BuildTarget.iOS ? "Assets/GoogleService-Info.plist" : "Assets/google-services.json";
            if (!File.Exists(path)) return false;
            string bundle = PlayerSettings.GetApplicationIdentifier(
                target == BuildTarget.iOS ? UnityEditor.Build.NamedBuildTarget.iOS : UnityEditor.Build.NamedBuildTarget.Android);
            try
            {
                return target == BuildTarget.iOS ? ValidateIosConfig(File.ReadAllText(path), bundle)
                    : ValidateAndroidConfig(File.ReadAllText(path), bundle);
            }
            catch { return false; }
        }

        public static bool ValidateIosConfig(string xml, string bundle)
        {
            try
            {
                var entries = XDocument.Parse(xml).Root?.Element("dict")?.Elements().ToArray();
                if (entries == null) return false;
                var values = new Dictionary<string, string>();
                for (int i = 0; i + 1 < entries.Length; i += 2)
                    if (entries[i].Name == "key") values[entries[i].Value] = entries[i + 1].Value;
                return values.TryGetValue("BUNDLE_ID", out var id) && id == bundle &&
                    values.TryGetValue("GOOGLE_APP_ID", out var app) && app.StartsWith("1:", StringComparison.Ordinal) && app.Contains(":ios:") &&
                    values.TryGetValue("PROJECT_ID", out var project) && !string.IsNullOrWhiteSpace(project) &&
                    values.TryGetValue("API_KEY", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey);
            }
            catch { return false; }
        }
        [Serializable] sealed class AndroidConfig { public ProjectInfo project_info; public Client[] client; }
        [Serializable] sealed class ProjectInfo { public string project_id; }
        [Serializable] sealed class Client { public ClientInfo client_info; public ApiKey[] api_key; }
        [Serializable] sealed class ApiKey { public string current_key; }
        [Serializable] sealed class ClientInfo { public string mobilesdk_app_id; public AndroidInfo android_client_info; }
        [Serializable] sealed class AndroidInfo { public string package_name; }

        public static bool ValidateAndroidConfig(string json, string bundle)
        {
            try
            {
                var config = JsonUtility.FromJson<AndroidConfig>(json);
                return !string.IsNullOrWhiteSpace(config?.project_info?.project_id) && config.client != null &&
                    config.client.Any(client => client?.client_info?.android_client_info?.package_name == bundle &&
                        client.client_info.mobilesdk_app_id?.Contains(":android:") == true &&
                        client.api_key?.Any(key => !string.IsNullOrWhiteSpace(key?.current_key)) == true);
            }
            catch { return false; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS && report.summary.platform != BuildTarget.Android) return;
            RefreshConfiguration();
            if (!HasValidConfig(report.summary.platform))
                throw new BuildFailedException("Firebase 설정이 없습니다/번들이 다릅니다. 먹점프용 GoogleService-Info.plist(iOS) 또는 google-services.json(Android)을 Assets에 추가하세요.");
        }

#if UNITY_IOS
        [PostProcessBuild(10010)]
        static void ApplyIos(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string file = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(file);
            plist.root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            plist.root.SetBoolean("GOOGLE_ANALYTICS_IDFV_COLLECTION_ENABLED", false);
            plist.root.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE", false);
            plist.root.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE", false);
            plist.root.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA", false);
            plist.root.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);
            plist.root.SetBoolean("FirebaseAutomaticScreenReportingEnabled", false);
            File.WriteAllText(file, plist.WriteToString());
            ApplyIosProjectSettings(path);
        }

        static void ApplyIosProjectSettings(string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string appTarget = project.GetUnityMainTargetGuid();
            string frameworkTarget = project.GetUnityFrameworkTargetGuid();

            // Firebase는 메인 앱 번들에서 구성 파일을 찾는다. SDK 후처리가 이미
            // 등록했다면 같은 파일 참조를 재사용해 중복 리소스 복사를 만들지 않는다.
            const string configName = "GoogleService-Info.plist";
            File.Copy(Path.Combine(Application.dataPath, configName), Path.Combine(buildPath, configName), true);
            string configGuid = project.FindFileGuidByProjectPath(configName);
            if (string.IsNullOrEmpty(configGuid))
                configGuid = project.AddFile(configName, configName, PBXSourceTree.Source);
            project.AddFileToBuild(appTarget, configGuid);

            foreach (string target in new[] { appTarget, frameworkTarget })
            {
                project.SetBuildProperty(target, "CLANG_ENABLE_MODULES", "YES");
                project.SetBuildProperty(target, "ENABLE_BITCODE", "NO");
                // 기존 CocoaPods/Apple 로그인 링커 옵션은 지우지 않는다.
                project.UpdateBuildProperty(target, "OTHER_LDFLAGS",
                    new[] { "$(inherited)", "-ObjC" }, Array.Empty<string>());
            }
            // Swift 런타임은 최종 앱에만 포함한다. UnityFramework 안에 중첩
            // Frameworks 폴더가 생겨 Archive 검증에 걸리지 않게 구분한다.
            project.SetBuildProperty(appTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.SetBuildProperty(frameworkTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.UpdateBuildProperty(appTarget, "LD_RUNPATH_SEARCH_PATHS",
                new[] { "$(inherited)", "@executable_path/Frameworks" }, Array.Empty<string>());
            File.WriteAllText(projectPath, UseAnalyticsCoreSwiftPackage(project.WriteToString()));
        }
#endif

        // EDM4U의 Podfile 생성(40) 이후, 설치(50) 이전. AdMob의 AdSupport는 유지한다.
        [PostProcessBuild(46)]
        static void ConfigureAnalyticsCorePod(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string podfile = Path.Combine(path, "Podfile");
            if (!File.Exists(podfile)) throw new BuildFailedException("Firebase Analytics는 CocoaPods 작업공간이 필요합니다.");
            File.WriteAllText(podfile, UseAnalyticsCorePod(File.ReadAllText(podfile)));
        }

        public static string UseAnalyticsCorePod(string podfile) =>
            podfile.Replace("'Firebase/Analytics'", "'FirebaseAnalytics/Core'")
                .Replace("\"Firebase/Analytics\"", "\"FirebaseAnalytics/Core\"")
                // App 패키지의 Firebase/Core도 기본 Analytics(IDFA 포함)를
                // 간접 추가하므로 CoreOnly로 맞춰야 Core 구성이 유지된다.
                .Replace("'Firebase/Core'", "'Firebase/CoreOnly'")
                .Replace("\"Firebase/Core\"", "\"Firebase/CoreOnly\"");

        // SDK 13.16은 Firebase를 Swift Package로 연결할 수 있다. Podfile만
        // 보정하면 이 경로에서 광고 식별자 지원 제품이 다시 포함된다.
        public static string UseAnalyticsCoreSwiftPackage(string project) =>
            System.Text.RegularExpressions.Regex.Replace(project,
                @"(\bproductName\s*=\s*)""?FirebaseAnalytics""?(\s*;)",
                "${1}FirebaseAnalyticsCore${2}");

        public static string ApplyAndroidPrivacy(string xml)
        {
            var document = XDocument.Parse(xml);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";
            var app = document.Root.Element("application");
            if (app == null) throw new InvalidDataException("Android application 노드가 없습니다.");
            foreach (string key in new[] { "firebase_analytics_collection_enabled", "google_analytics_adid_collection_enabled",
                "google_analytics_default_allow_analytics_storage", "google_analytics_default_allow_ad_storage",
                "google_analytics_default_allow_ad_user_data", "google_analytics_default_allow_ad_personalization_signals",
                "google_analytics_automatic_screen_reporting_enabled" })
            {
                var entry = app.Elements("meta-data").FirstOrDefault(e => (string)e.Attribute(android + "name") == key);
                if (entry == null) { entry = new XElement("meta-data", new XAttribute(android + "name", key)); app.Add(entry); }
                entry.SetAttributeValue(android + "value", "false");
                entry.SetAttributeValue(tools + "replace", "android:value");
            }
            document.Root.SetAttributeValue(XNamespace.Xmlns + "android", android.NamespaceName);
            document.Root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
            return document.ToString();
        }
    }

#if UNITY_ANDROID
    public sealed class MukJumpFirebaseAndroidPrivacy : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10010;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            File.WriteAllText(manifest, MukJumpFirebaseAnalyticsSetup.ApplyAndroidPrivacy(File.ReadAllText(manifest)));
        }
    }
#endif
}
