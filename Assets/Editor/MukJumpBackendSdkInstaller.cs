using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 공식 뒤끝 로그인 패키지를 같은 버전으로 다시 설치할 때 쓰는 개발 메뉴다.
    public static class MukJumpBackendSdkInstaller
    {
        const string AndroidGooglePackage =
            "BackendGoogleLogin-Android-2.3.0.unitypackage";
        const string IosGooglePackage =
            "BackendGoogleLogin-iOS-2.1.0.unitypackage";
        const string AndroidApplePackage =
            "BackendAppleLogin-1.2.0.unitypackage";

        [MenuItem("MukJump/Store/Backend/Import Android Google Login SDK")]
        public static void ImportAndroidGoogleLogin() =>
            ImportFromDownloads(AndroidGooglePackage);

        [MenuItem("MukJump/Store/Backend/Import iOS Google Login SDK")]
        public static void ImportIosGoogleLogin() =>
            ImportFromDownloads(IosGooglePackage);

        [MenuItem("MukJump/Store/Backend/Import Android Apple Login SDK")]
        public static void ImportAndroidAppleLogin() =>
            ImportFromDownloads(AndroidApplePackage);

        static void ImportFromDownloads(string fileName)
        {
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            string path = Path.Combine(userProfile, "Downloads", fileName);
            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[MukJump] 공식 뒤끝 패키지를 찾지 못했습니다: {path}");
                return;
            }

            AssetDatabase.ImportPackage(path, interactive: false);
            Debug.Log($"[MukJump] 뒤끝 패키지 가져오기 시작: {fileName}");
        }
    }
}
