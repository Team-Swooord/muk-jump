#if UNITY_IOS
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTools
{
    /// 지원 언어·앱 이름·ATT 설명과 면제 암호화 선언을 최종 iOS 결과물에 고정한다.
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
            string[] displayNames = { "먹점프", "MukJump", "MukJump" };
            string[] descriptions = {
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription,
                "Allow tracking to deliver ads and measure advertising performance.",
                "広告の配信と広告効果の測定のため、トラッキングの許可をお願いします。"
            };
            // Unity 템플릿의 English/Japanese/French/German 잔여 목록 대신
            // 실제 지원 언어를 Xcode의 Project > Info에도 표시한다.
            string knownRegions = Regex.Match(project.WriteToString(), @"knownRegions\s*=\s*\(([^)]*)\)").Groups[1].Value;
            bool hasBase = Regex.IsMatch(knownRegions, @"\bBase\b");
            project.ClearKnownRegions();
            project.SetDevelopmentRegion("en");
            if (hasBase) project.AddKnownRegion("Base");
            for (int i = 0; i < languages.Length; i++)
            {
                project.AddKnownRegion(languages[i]);
                string relative = languages[i] + ".lproj/InfoPlist.strings";
                string path = Path.Combine(buildPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string contents = File.Exists(path) ? File.ReadAllText(path) : "";
                contents = SetLocalizedValue(contents, TrackingUsageKey, descriptions[i]);
                contents = SetLocalizedValue(contents, "CFBundleDisplayName", displayNames[i]);
                File.WriteAllText(path, contents);
                string guid = project.FindFileGuidByProjectPath(relative);
                if (string.IsNullOrEmpty(guid)) guid = project.AddFile(relative, relative, PBXSourceTree.Source);
                project.AddFileToBuild(project.GetUnityMainTargetGuid(), guid);
            }
            project.WriteToFile(projectPath);
        }

        static string SetLocalizedValue(string contents, string key, string value)
        {
            // 다른 네이티브 기능이 추가한 권한 문구와 주석은 보존한다.
            string line = "\"" + key + "\" = \"" + value + "\";";
            string pattern = "(?m)^\\s*\"" + Regex.Escape(key) + "\"\\s*=\\s*\"(?:\\\\.|[^\"\\\\])*\"\\s*;";
            if (Regex.IsMatch(contents, pattern))
                return Regex.Replace(contents, pattern, _ => line);
            return contents.TrimEnd() + (contents.Length > 0 ? "\n" : "") + line + "\n";
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
            // 출시 전 초기화 등 이 앱의 standardUserDefaults 접근에 필요한 선언이다.
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
            plist.root.SetString("CFBundleDevelopmentRegion", "en");
            var languages = plist.root.CreateArray("CFBundleLocalizations");
            foreach (string language in new[] { "ko", "en", "ja" }) languages.AddString(language);
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
