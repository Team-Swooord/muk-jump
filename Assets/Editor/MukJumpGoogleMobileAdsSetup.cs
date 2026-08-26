using System;
using System.IO;
using System.Reflection;
using MukJump.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// Google 광고 설정 에셋을 만들고 Development/Release 빌드의 앱 ID 혼입을 막는다.
    [InitializeOnLoad]
    public sealed class MukJumpGoogleMobileAdsSetup : IPreprocessBuildWithReport
    {
        const string SettingsFolder = "Assets/Resources/MukJump/Settings";
        const string SettingsAssetPath =
            SettingsFolder + "/MukJumpGoogleAdsSettings.asset";
        const string TrackingDescription =
            "맞춤형 광고 제공과 광고 성과 측정을 위해 기기 식별자 사용 권한을 요청합니다.";

        public int callbackOrder => -1000;

        static MukJumpGoogleMobileAdsSetup()
        {
            EditorApplication.delayCall += EnsureProjectSettings;
        }

        [MenuItem("MukJump/Store/Google Ads/설정 만들기 및 SDK 동기화")]
        public static void EnsureProjectSettings()
        {
            MukJumpGoogleAdsSettings settings = EnsureSettingsAsset();
            SyncPluginSettings(settings, forceTestAppIds: false);
        }

        [MenuItem("MukJump/Store/Google Ads/운영 설정 검증")]
        public static void ValidateProductionSettings()
        {
            MukJumpGoogleAdsSettings settings = EnsureSettingsAsset();
            bool android = settings.TryValidateProduction(
                GoogleAdsPlatform.Android,
                out string androidError);
            bool ios = settings.TryValidateProduction(
                GoogleAdsPlatform.IOS,
                out string iosError);
            string message =
                $"Android: {(android ? "통과" : androidError)}\n" +
                $"iOS: {(ios ? "통과" : iosError)}";
            if (android && ios)
                Debug.Log($"먹점프 Google 광고 운영 설정 검증 완료\n{message}");
            else
                Debug.LogWarning($"먹점프 Google 광고 운영 설정 미완료\n{message}");
            Selection.activeObject = settings;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android &&
                report.summary.platform != BuildTarget.iOS)
                return;

            MukJumpGoogleAdsSettings settings = EnsureSettingsAsset();
            bool development = (report.summary.options &
                                BuildOptions.Development) != 0;
            if (development)
            {
                SyncPluginSettings(settings, forceTestAppIds: true);
                Debug.Log(
                    "먹점프 Development Build: Google 공식 테스트 앱/광고 ID를 사용합니다.");
                return;
            }

            GoogleAdsPlatform platform = report.summary.platform ==
                                         BuildTarget.Android
                ? GoogleAdsPlatform.Android
                : GoogleAdsPlatform.IOS;
            if (!settings.TryValidateProduction(platform, out string error))
            {
                throw new BuildFailedException(
                    "먹점프 Release 빌드의 Google 광고 설정이 안전하지 않습니다. " +
                    error + " 설정 에셋에서 먹점프 전용 AdMob 값을 입력하세요.");
            }
            SyncPluginSettings(settings, forceTestAppIds: false);
        }

        static MukJumpGoogleAdsSettings EnsureSettingsAsset()
        {
            MukJumpGoogleAdsSettings settings =
                AssetDatabase.LoadAssetAtPath<MukJumpGoogleAdsSettings>(
                    SettingsAssetPath);
            if (settings != null) return settings;

            Directory.CreateDirectory(SettingsFolder);
            settings = ScriptableObject
                .CreateInstance<MukJumpGoogleAdsSettings>();
            // 사용자 요청대로 SHIFT의 값을 조사해 후보로 기록한다. 게시자 불일치와
            // 배너 부재 때문에 검증 플래그는 켜지 않으며 운영 빌드에는 쓰지 않는다.
            settings.ConfigureDetectedShiftCandidates();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "먹점프 Google 광고 설정을 만들었습니다. SHIFT 후보 ID는 검증 전까지 운영 요청에서 차단됩니다.");
            return settings;
        }

        static void SyncPluginSettings(
            MukJumpGoogleAdsSettings settings,
            bool forceTestAppIds)
        {
            Type type = FindGoogleSettingsType();
            MethodInfo load = type?.GetMethod(
                "LoadInstance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (load?.Invoke(null, null) is not UnityEngine.Object pluginSettings)
                return;

            string androidId = ResolvePluginAppId(
                settings,
                GoogleAdsPlatform.Android,
                forceTestAppIds);
            string iosId = ResolvePluginAppId(
                settings,
                GoogleAdsPlatform.IOS,
                forceTestAppIds);
            var serialized = new SerializedObject(pluginSettings);
            SetString(serialized, "adMobAndroidAppId", androidId);
            SetString(serialized, "adMobIOSAppId", iosId);
            SetString(
                serialized,
                "userTrackingUsageDescription",
                TrackingDescription);
            SetString(serialized, "userLanguage", "ko");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pluginSettings);
            AssetDatabase.SaveAssets();
        }

        static string ResolvePluginAppId(
            MukJumpGoogleAdsSettings settings,
            GoogleAdsPlatform platform,
            bool forceTestAppIds)
        {
            if (!forceTestAppIds &&
                settings != null &&
                settings.TryValidateProduction(platform, out _))
                return settings.AppIdFor(platform);
            return GoogleMobileAdsTestIds.AppIdFor(platform);
        }

        static Type FindGoogleSettingsType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(
                    "GoogleMobileAds.Editor.GoogleMobileAdsSettings",
                    throwOnError: false);
                if (type != null) return type;
            }
            return null;
        }

        static void SetString(
            SerializedObject serialized,
            string propertyName,
            string value)
        {
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? string.Empty;
        }
    }
}
