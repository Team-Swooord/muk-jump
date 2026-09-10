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
            ApplyLocalizedTrackingDescriptions(buildPath);
            ApplyUserDefaultsReason(buildPath);
        }

        public static void ApplyLocalizedTrackingDescriptions(string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string[] languages = { "ko", "en", "ja" };
            string[] descriptions = {
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription,
                "Allow tracking to deliver ads and measure advertising performance.",
                "広告の配信と広告効果の測定のため、トラッキングの許可をお願いします。"
            };
            for (int i = 0; i < languages.Length; i++)
            {
                string relative = languages[i] + ".lproj/InfoPlist.strings";
                string path = Path.Combine(buildPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "\"" + TrackingUsageKey + "\" = \"" + descriptions[i] + "\";\n");
                string guid = project.FindFileGuidByProjectPath(relative);
                if (string.IsNullOrEmpty(guid)) guid = project.AddFile(relative, relative, PBXSourceTree.Source);
                project.AddFileToBuild(project.GetUnityMainTargetGuid(), guid);
            }
            project.WriteToFile(projectPath);
        }

        public static void ApplyUserDefaultsReason(string buildPath)
        {
            string path = Path.Combine(buildPath, "UnityFramework/PrivacyInfo.xcprivacy");
            var manifest = new PlistDocument();
            if (File.Exists(path)) manifest.ReadFromFile(path);
            var root = manifest.root;
            var types = root.values.TryGetValue("NSPrivacyAccessedAPITypes", out var existing)
                ? existing.AsArray() : root.CreateArray("NSPrivacyAccessedAPITypes");
            foreach (var entry in types.values)
            {
                var item = entry.AsDict();
                if (!item.values.TryGetValue("NSPrivacyAccessedAPIType", out var category) ||
                    category.AsString() != "NSPrivacyAccessedAPICategoryUserDefaults") continue;
                var reasons = item.values.TryGetValue("NSPrivacyAccessedAPITypeReasons", out var saved)
                    ? saved.AsArray() : item.CreateArray("NSPrivacyAccessedAPITypeReasons");
                foreach (var reason in reasons.values)
                    if (reason.AsString() == "CA92.1") return;
                reasons.AddString("CA92.1");
                File.WriteAllText(path, manifest.WriteToString());
                return;
            }
            // Game Center 재전송 대기 기록은 이 앱의 standardUserDefaults에만 저장한다.
            var declaration = types.AddDict();
            declaration.SetString("NSPrivacyAccessedAPIType", "NSPrivacyAccessedAPICategoryUserDefaults");
            declaration.CreateArray("NSPrivacyAccessedAPITypeReasons").AddString("CA92.1");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, manifest.WriteToString());
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
            var languages = plist.root.CreateArray("CFBundleLocalizations");
            foreach (string language in new[] { "ko", "en", "ja" }) languages.AddString(language);
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
