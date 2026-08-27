using System.Collections.Generic;
using System.IO;
using MukJump.Core;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTools
{
    public enum BackendReleasePlatform
    {
        All,
        IOS,
        Android,
    }

    public static class MukJumpBackendReleaseValidator
    {
        const string BackendSettingsPath =
            "Assets/TheBackend/Resources/TheBackendSettings.asset";
        const string AndroidGoogleSettingsPath =
            "Assets/TheBackend/Resources/" +
            "TheBackendGoogleSettingsForAndroid.asset";
        const string IosGoogleSettingsPath =
            "Assets/TheBackend/Resources/" +
            "TheBackendGoogleSettingsForIOS.asset";

        [MenuItem("MukJump/Store/Backend/Validate Release Setup")]
        public static void ValidateReleaseSetup()
        {
            List<string> issues = CollectIssues();
            if (issues.Count == 0)
            {
                Debug.Log("[MukJump] 뒤끝·소셜 로그인·리더보드 출시 설정 확인 완료");
                EditorUtility.DisplayDialog(
                    "먹점프 출시 설정",
                    "뒤끝·Google·Apple 로그인과 리더보드 필수 설정이 채워졌습니다.",
                    "확인");
                return;
            }

            string message = string.Join("\n", issues);
            Debug.LogWarning("[MukJump] 출시 전 남은 서버 설정:\n" + message);
            EditorUtility.DisplayDialog(
                "먹점프 출시 전 남은 설정",
                message,
                "확인");
        }

        public static List<string> CollectIssues(
            BackendReleasePlatform platform = BackendReleasePlatform.All)
        {
            var issues = new List<string>();
            bool includeIos = platform == BackendReleasePlatform.All ||
                              platform == BackendReleasePlatform.IOS;
            bool includeAndroid = platform == BackendReleasePlatform.All ||
                                  platform == BackendReleasePlatform.Android;
            if (!File.Exists("Assets/TheBackend/Plugins/Backend.dll"))
                issues.Add("• 공식 뒤끝 Base SDK가 없습니다.");
            if (includeAndroid && !File.Exists(
                    "Assets/TheBackend/Toolkit/GoogleLogin/Android/" +
                    "TheBackend.ToolKit.GoogleLogin.Android.dll"))
                issues.Add("• Android Google 로그인 SDK가 없습니다.");
            if (includeIos && !File.Exists(
                    "Assets/TheBackend/Toolkit/GoogleLogin/iOS/" +
                    "TheBackend.ToolKit.GoogleLogin.iOS.dll"))
                issues.Add("• iOS Google 로그인 SDK가 없습니다.");

            MukJumpBackendSettings settings = MukJumpBackendSettings.Load();
            if (settings == null)
            {
                issues.Add("• MukJumpBackendSettings 에셋이 없습니다.");
                return issues;
            }
            if (!settings.ProductionConfigurationVerified)
                issues.Add("• 뒤끝 콘솔 앱 연결 확인 체크가 꺼져 있습니다.");
            if (string.IsNullOrWhiteSpace(settings.PlayerTableName))
                issues.Add("• 비공개 MukJumpPlayer 테이블 이름이 비었습니다.");
            if (string.IsNullOrWhiteSpace(settings.BestHeightColumn))
                issues.Add("• 최고 고도 컬럼 이름이 비었습니다.");
            if (string.IsNullOrWhiteSpace(settings.AllTimeRankUuid))
                issues.Add("• 전체 최고 고도 리더보드 UUID가 비었습니다.");
            if (includeAndroid &&
                string.IsNullOrWhiteSpace(settings.AndroidGoogleWebClientId))
                issues.Add("• Android Google Web Client ID가 비었습니다.");
            if (includeAndroid && !settings.AndroidSigningHashesVerified)
                issues.Add(
                    "• 뒤끝 콘솔의 Android 디버그·출시 서명 해시 확인이 끝나지 않았습니다.");
            if (!HasSerializedValue(BackendSettingsPath, "clientAppID") ||
                !HasSerializedValue(BackendSettingsPath, "signatureKey"))
                issues.Add(
                    "• The Backend > Edit Settings에서 먹점프 앱 ID·서명키를 연결해야 합니다.");
            if (!SerializedValueEquals(
                    BackendSettingsPath,
                    "sendLogReport",
                    "0"))
                issues.Add(
                    "• 뒤끝 Send Log Report를 꺼 개인정보 수집을 최소화해야 합니다.");
            if (!SerializedValueEquals(
                    BackendSettingsPath,
                    "autoLoadLocationProperties",
                    "0"))
                issues.Add(
                    "• 사용하지 않는 뒤끝 IP 기반 위치 자동 조회를 꺼야 합니다.");

            if (includeAndroid)
            {
                if (!HasSerializedValue(
                        AndroidGoogleSettingsPath,
                        "webClientID"))
                    issues.Add(
                        "• The Backend > ToolKit > GoogleLogin > Android Settings의 Web Client ID가 비었습니다.");
                else if (!SerializedValueEquals(
                             AndroidGoogleSettingsPath,
                             "webClientID",
                             settings.AndroidGoogleWebClientId))
                    issues.Add(
                        "• Android Google Settings와 먹점프 설정의 Web Client ID가 서로 다릅니다.");
            }

            if (includeIos)
            {
                if (!settings.AppleRevocationConfigurationVerified)
                    issues.Add(
                        "• 뒤끝 콘솔의 Apple Team ID·Key 이름·p8 토큰 철회 설정 확인이 끝나지 않았습니다.");
                if (!settings.AppleAccountChangeWebhookVerified)
                    issues.Add(
                        "• Apple 계정 변경 웹훅 등록 확인이 끝나지 않았습니다.");
                string iosClientId = ReadSerializedValue(
                    IosGoogleSettingsPath,
                    "iosClientID");
                string iosUrlScheme = ReadSerializedValue(
                    IosGoogleSettingsPath,
                    "iosURLSchema");
                if (string.IsNullOrWhiteSpace(iosClientId))
                    issues.Add(
                        "• The Backend > ToolKit > GoogleLogin > iOS Settings의 iOS Client ID가 비었습니다.");
                if (string.IsNullOrWhiteSpace(iosUrlScheme))
                    issues.Add(
                        "• iOS Google 로그인 URL Scheme이 비었습니다.");
                else if (!IsMatchingGoogleIosUrlScheme(
                             iosClientId,
                             iosUrlScheme))
                    issues.Add(
                        "• iOS Google Client ID와 URL Scheme이 서로 맞지 않습니다.");
            }
            return issues;
        }

        public static bool HasSerializedValue(
            string path,
            string fieldName) =>
            !string.IsNullOrWhiteSpace(
                ReadSerializedValue(path, fieldName));

        public static bool SerializedValueMatches(
            string path,
            string fieldName,
            string expected) =>
            SerializedValueEquals(path, fieldName, expected);

        public static bool IsMatchingGoogleIosUrlScheme(
            string clientId,
            string urlScheme)
        {
            const string suffix = ".apps.googleusercontent.com";
            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(urlScheme) ||
                !clientId.EndsWith(suffix))
                return false;

            string clientPrefix = clientId.Substring(
                0,
                clientId.Length - suffix.Length);
            return urlScheme ==
                   "com.googleusercontent.apps." + clientPrefix;
        }

        static bool SerializedValueEquals(
            string path,
            string fieldName,
            string expected) =>
            !string.IsNullOrWhiteSpace(expected) &&
            ReadSerializedValue(path, fieldName) == expected.Trim();

        static string ReadSerializedValue(
            string path,
            string fieldName)
        {
            if (!File.Exists(path) || string.IsNullOrWhiteSpace(fieldName))
                return string.Empty;

            string prefix = fieldName + ":";
            foreach (string line in File.ReadLines(path))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(prefix))
                    return trimmed.Substring(prefix.Length).Trim();
            }

            return string.Empty;
        }
    }
}
