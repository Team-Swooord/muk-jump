#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTools
{
    /// ATT 설명과 면제 암호화 선언을 최종 Info.plist에 고정한다.
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

            ApplyToInfoPlist(buildPath);
        }

        public static void ApplyToInfoPlist(string buildPath)
        {
            string plistPath = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(plistPath))
                return;

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString(
                TrackingUsageKey,
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription);
            // 먹점프는 SDK의 표준 HTTPS 통신만 사용하고 독자 암호화를
            // 구현하지 않는다. App Store Connect의 면제 암호화 선언을
            // 빌드마다 동일하게 유지한다.
            plist.root.SetBoolean(NonExemptEncryptionKey, false);
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
