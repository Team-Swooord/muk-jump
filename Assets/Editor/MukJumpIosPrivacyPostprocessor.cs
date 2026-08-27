#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTools
{
    /// 먹점프 1.0은 비맞춤 광고만 사용하고 ATT 권한을 요청하지 않는다.
    /// Google Mobile Ads 편집기 설정의 예전 문구가 남아 있어도 최종 plist에서 제거한다.
    public static class MukJumpIosPrivacyPostprocessor
    {
        const string TrackingUsageKey =
            "NSUserTrackingUsageDescription";
        const string NonExemptEncryptionKey =
            "ITSAppUsesNonExemptEncryption";

        [PostProcessBuild(10000)]
        static void ApplyPrivacyDeclarations(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            RemoveFromInfoPlist(buildPath);
        }

        public static void RemoveFromInfoPlist(string buildPath)
        {
            string plistPath = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(plistPath))
                return;

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.values.Remove(TrackingUsageKey);
            // 먹점프는 SDK의 표준 HTTPS 통신만 사용하고 독자 암호화를
            // 구현하지 않는다. App Store Connect의 면제 암호화 선언을
            // 빌드마다 동일하게 유지한다.
            plist.root.SetBoolean(NonExemptEncryptionKey, false);
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
