using System;
using System.IO;
using System.Linq;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace MukJump.EditorTests
{
    public sealed class IosReleaseBuildTests
    {
        [Test]
        public void StoreIconKeepsLosslessMobileImportSettings()
        {
            var importer = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AppIconPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));

            foreach (string platform in new[] { "iOS", "Android" })
            {
                TextureImporterPlatformSettings settings =
                    importer.GetPlatformTextureSettings(platform);
                Assert.That(settings.overridden, Is.True, platform);
                Assert.That(settings.maxTextureSize, Is.EqualTo(1024),
                    platform);
                Assert.That(
                    settings.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    platform);
            }
        }

        [Test]
        public void AndroidAdaptiveIconUsesSeparateOpaqueAndTransparentLayers()
        {
            var background = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AndroidAdaptiveBackgroundPath) as
                TextureImporter;
            var foreground = AssetImporter.GetAtPath(
                MukJumpStoreBuild.AndroidAdaptiveForegroundPath) as
                TextureImporter;

            Assert.That(background, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(
                background.assetPath,
                Is.Not.EqualTo(foreground.assetPath));
            Assert.That(
                background.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(
                foreground.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(foreground.alphaIsTransparency, Is.True);

            foreach (TextureImporter importer in new[]
                     {
                         background,
                         foreground,
                     })
            {
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }

        [Test]
        public void AndroidAdaptiveSlotsUseBackgroundThenForegroundAtEveryDensity()
        {
            string backgroundGuid = ReadAssetGuid(
                MukJumpStoreBuild.AndroidAdaptiveBackgroundPath + ".meta");
            string foregroundGuid = ReadAssetGuid(
                MukJumpStoreBuild.AndroidAdaptiveForegroundPath + ".meta");
            string projectSettings = File.ReadAllText(
                "ProjectSettings/ProjectSettings.asset");
            string pair =
                $"- {{fileID: 2800000, guid: {backgroundGuid}, type: 3}}\n" +
                $"      - {{fileID: 2800000, guid: {foregroundGuid}, type: 3}}";

            Assert.That(
                CountOccurrences(projectSettings, pair),
                Is.EqualTo(6),
                "Android Adaptive 아이콘 6개 밀도 슬롯에 배경·전경 레이어가 모두 필요합니다.");
        }

        [Test]
        public void UnusedUnityOnlineServicesStayDisabledForRelease()
        {
            Assert.That(
                MukJumpStoreBuild.CollectUnityServiceIssues(
                    MukJumpStoreBuild.UnityConnectSettingsPath),
                Is.Empty,
                "분석·광고·결제를 사용하지 않는다는 스토어 답변과 프로젝트 설정이 달라집니다.");
        }

        [Test]
        public void GeneratedOutputRequiresWorkspaceAndNativePods()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            Directory.CreateDirectory(
                Path.Combine(root, MukJumpStoreBuild.IosWorkspaceName));
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>GIDClientID</key><string>client.apps.googleusercontent.com</string>" +
                "<key>CFBundleURLTypes</key><array><dict>" +
                "<key>CFBundleURLSchemes</key><array>" +
                "<string>com.googleusercontent.apps.client</string>" +
                "</array></dict></array>" +
                $"<key>CFBundleShortVersionString</key><string>{PlayerSettings.bundleVersion}</string>" +
                $"<key>CFBundleVersion</key><string>{PlayerSettings.iOS.buildNumber}</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "MukJump.entitlements"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>com.apple.developer.applesignin</key>" +
                "<array><string>Default</string></array>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(
                    root,
                    "Unity-iPhone.xcodeproj",
                    "project.pbxproj"),
                "AuthenticationServices.framework\n" +
                "CODE_SIGN_ENTITLEMENTS = MukJump.entitlements;\n" +
                $"DEVELOPMENT_TEAM = {PlayerSettings.iOS.appleDeveloperTeamID};\n" +
                $"PRODUCT_BUNDLE_IDENTIFIER = " +
                $"{PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)};\n");
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n" +
                "  - Google-Mobile-Ads-SDK (13.7.0)\n" +
                "  - GoogleSignIn (7.1.0)\n" +
                "  - GoogleUserMessagingPlatform (3.1.0)\n");

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Is.Empty);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void MissingWorkspaceOrPodIsReportedBeforeXcodeHandoff()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-output-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                "<key>NSUserTrackingUsageDescription</key>" +
                "<string>tracking</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><true/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n  - GoogleSignIn (7.1.0)\n");
            File.WriteAllText(
                Path.Combine(
                    root,
                    "Unity-iPhone.xcodeproj",
                    "project.pbxproj"),
                string.Empty);

            try
            {
                string[] issues =
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root);
                Assert.That(
                    issues.Any(issue => issue.Contains("xcworkspace")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("Google-Mobile-Ads-SDK")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("GoogleUserMessagingPlatform")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("NSUserTrackingUsageDescription")),
                    Is.True);
                Assert.That(
                    issues.Any(issue =>
                        issue.Contains("ITSAppUsesNonExemptEncryption")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("Google iOS Client ID")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("URL Scheme")),
                    Is.True);
                Assert.That(
                    issues.Any(issue => issue.Contains("Apple entitlement")),
                    Is.True);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void LocalValidationMayCompileBeforeGoogleOauthIsIssued()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "mukjump-ios-local-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(
                Path.Combine(root, "Unity-iPhone.xcodeproj"));
            Directory.CreateDirectory(
                Path.Combine(root, MukJumpStoreBuild.IosWorkspaceName));
            File.WriteAllText(
                Path.Combine(root, "Info.plist"),
                "<?xml version=\"1.0\"?><plist><dict>" +
                $"<key>CFBundleShortVersionString</key><string>{PlayerSettings.bundleVersion}</string>" +
                $"<key>CFBundleVersion</key><string>{PlayerSettings.iOS.buildNumber}</string>" +
                "<key>ITSAppUsesNonExemptEncryption</key><false/>" +
                "</dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "MukJump.entitlements"),
                "<plist><dict><key>com.apple.developer.applesignin</key>" +
                "<array><string>Default</string></array></dict></plist>");
            File.WriteAllText(
                Path.Combine(root, "Unity-iPhone.xcodeproj", "project.pbxproj"),
                "AuthenticationServices.framework\n" +
                "CODE_SIGN_ENTITLEMENTS = MukJump.entitlements;\n" +
                $"DEVELOPMENT_TEAM = {PlayerSettings.iOS.appleDeveloperTeamID};\n" +
                $"PRODUCT_BUNDLE_IDENTIFIER = " +
                $"{PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)};\n");
            File.WriteAllText(
                Path.Combine(root, "Podfile.lock"),
                "PODS:\n" +
                "  - Google-Mobile-Ads-SDK (13.7.0)\n" +
                "  - GoogleSignIn (7.1.0)\n" +
                "  - GoogleUserMessagingPlatform (3.1.0)\n");

            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(
                        root,
                        requireReleaseIdentity: false),
                    Is.Empty);
                Assert.That(
                    MukJumpStoreBuild.CollectIosGeneratedOutputIssues(root),
                    Does.Contain("Google iOS Client ID가 Info.plist에 없습니다."));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestCase(
            "123-example.apps.googleusercontent.com",
            "com.googleusercontent.apps.123-example")]
        [TestCase(" client.apps.googleusercontent.com ",
            "com.googleusercontent.apps.client")]
        [TestCase("", "")]
        public void GoogleClientIdProducesExactIosUrlScheme(
            string clientId,
            string expected)
        {
            Assert.That(
                MukJumpStoreBuild.ReverseGoogleClientId(clientId),
                Is.EqualTo(expected));
        }

        [Test]
        public void AndroidReleaseSigningRequiresExternalSecretsAndExistingStore()
        {
            string missingPath = Path.Combine(
                Path.GetTempPath(),
                "mukjump-missing-" + Guid.NewGuid().ToString("N") +
                ".keystore");

            string[] issues = MukJumpStoreBuild.CollectAndroidSigningIssues(
                missingPath,
                string.Empty,
                string.Empty,
                string.Empty);

            Assert.That(issues.Length, Is.EqualTo(4));
            Assert.That(
                issues.Any(issue => issue.Contains("찾을 수 없습니다")),
                Is.True);
        }

        [Test]
        public void CompleteAndroidReleaseSigningInputPassesValidation()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mukjump-keystore-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            try
            {
                Assert.That(
                    MukJumpStoreBuild.CollectAndroidSigningIssues(
                        path,
                        "store-secret",
                        "mukjump",
                        "alias-secret"),
                    Is.Empty);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static string ReadAssetGuid(string metaPath)
        {
            const string prefix = "guid: ";
            string line = File.ReadLines(metaPath)
                .First(value => value.StartsWith(
                    prefix,
                    StringComparison.Ordinal));
            return line[prefix.Length..].Trim();
        }

        static int CountOccurrences(string value, string pattern)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(
                       pattern,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += pattern.Length;
            }
            return count;
        }
    }
}
