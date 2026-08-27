using System;
using System.Collections.Generic;
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
        public const string TrackingUsageDescription =
            "광고 제공 및 광고 성과 측정을 위해 기기 활동 사용 권한을 요청합니다.";
        const string SettingsFolder = "Assets/Resources/MukJump/Settings";
        const string SettingsAssetPath =
            SettingsFolder + "/MukJumpGoogleAdsSettings.asset";
        const string RuntimeSourcePath =
            "Assets/Scripts/Core/GoogleMobileAdsRuntime.cs";
        const string RequestFactorySourcePath =
            "Assets/Scripts/Core/GoogleMobileAdsRequestFactory.cs";

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
            string[] privacyIssues = CollectPrivacyPolicyIssues();
            string message =
                $"Android: {(android ? "통과" : androidError)}\n" +
                $"iOS: {(ios ? "통과" : iosError)}\n" +
                $"광고 개인정보: " +
                (privacyIssues.Length == 0
                    ? "통과"
                    : string.Join(" / ", privacyIssues));
            if (android && ios && privacyIssues.Length == 0)
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
            string[] privacyIssues = CollectPrivacyPolicyIssues();
            if (privacyIssues.Length > 0)
                throw new BuildFailedException(
                    "먹점프 Release 광고 개인정보 정책이 코드와 다릅니다:\n" +
                    string.Join("\n", privacyIssues));
            SyncPluginSettings(settings, forceTestAppIds: false);
        }

        public static string[] CollectPrivacyPolicyIssues()
        {
            var issues = new List<string>();
            string runtime = File.Exists(RuntimeSourcePath)
                ? File.ReadAllText(RuntimeSourcePath)
                : string.Empty;
            string requestFactory = File.Exists(RequestFactorySourcePath)
                ? File.ReadAllText(RequestFactorySourcePath)
                : string.Empty;

            if (!runtime.Contains("PublisherFirstPartyIdEnabled = false"))
                issues.Add("퍼블리셔 1차 식별자 비활성화가 없습니다.");
            if (!runtime.Contains(
                    "PublisherPrivacyPersonalizationState.Disabled"))
                issues.Add("퍼블리셔 광고 개인화 비활성화가 없습니다.");
            if (!runtime.Contains("MaxAdContentRating.G"))
                issues.Add("광고 콘텐츠 등급 G 제한이 없습니다.");
            if (!runtime.Contains("AgeRestrictedTreatment.Unspecified"))
                issues.Add("연령 제한 처리의 미지정 안전값이 없습니다.");
            if (!runtime.Contains(
                    "RequestTrackingAuthorizationThenGatherConsent") ||
                !runtime.Contains("RequestAuthorizationTracking"))
                issues.Add("iOS ATT 응답 후 광고 동의를 시작하는 흐름이 없습니다.");
            if (runtime.Contains("TagForChildDirectedTreatment.False") ||
                runtime.Contains("TagForUnderAgeOfConsent.False") ||
                runtime.Contains("TagForUnderAgeOfConsent = false"))
                issues.Add("연령을 확인하지 않고 아동·청소년이 아니라고 단정합니다.");
            if (!requestFactory.Contains(
                    "request.Extras[\"npa\"] = \"1\""))
                issues.Add("비맞춤 광고 npa=1 요청이 없습니다.");
            return issues.ToArray();
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
            settings.ConfigureProductionAdMobIds();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "먹점프 전용 Google 광고 설정을 만들고 운영 ID를 연결했습니다.");
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
                TrackingUsageDescription);
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
