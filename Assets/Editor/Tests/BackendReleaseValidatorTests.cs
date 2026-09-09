using System;
using System.IO;
using MukJump.EditorTools;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class BackendReleaseValidatorTests
    {
        [Test]
        public void SerializedSettingRequiresANonEmptyValue()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mukjump-backend-setting-" +
                Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(
                    path,
                    "clientAppID: \n" +
                    "signatureKey: configured\n");

                Assert.That(
                    MukJumpBackendReleaseValidator.HasSerializedValue(
                        path,
                        "clientAppID"),
                    Is.False);
                Assert.That(
                    MukJumpBackendReleaseValidator.HasSerializedValue(
                        path,
                        "signatureKey"),
                    Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void GoogleIosUrlSchemeMustBeReverseClientId()
        {
            const string clientId =
                "123456-example.apps.googleusercontent.com";
            Assert.That(
                MukJumpBackendReleaseValidator
                    .IsMatchingGoogleIosUrlScheme(
                        clientId,
                        "com.googleusercontent.apps.123456-example"),
                Is.True);
            Assert.That(
                MukJumpBackendReleaseValidator
                    .IsMatchingGoogleIosUrlScheme(
                        clientId,
                        "com.googleusercontent.apps.other"),
                Is.False);
        }

        [Test]
        public void MukJumpGoogleIosOauthSettingsMatchIssuedClient()
        {
            const string path =
                "Assets/TheBackend/Resources/" +
                "TheBackendGoogleSettingsForIOS.asset";
            const string clientId =
                "58212920281-bm72q2dnoc79ee4214e3k9fojb636u2e" +
                ".apps.googleusercontent.com";
            const string urlScheme =
                "com.googleusercontent.apps." +
                "58212920281-bm72q2dnoc79ee4214e3k9fojb636u2e";

            Assert.That(
                MukJumpBackendReleaseValidator.SerializedValueMatches(
                    path,
                    "iosClientID",
                    clientId),
                Is.True);
            Assert.That(
                MukJumpBackendReleaseValidator.SerializedValueMatches(
                    path,
                    "iosURLSchema",
                    urlScheme),
                Is.True);
            Assert.That(
                MukJumpBackendReleaseValidator
                    .IsMatchingGoogleIosUrlScheme(clientId, urlScheme),
                Is.True);
            Assert.That(
                MukJumpBackendReleaseValidator.ExpectedIosGoogleClientId,
                Is.EqualTo(clientId));
        }

        [Test]
        public void BackendReleaseIdentityIsPinnedToMukJumpProject()
        {
            const string backendPath =
                "Assets/TheBackend/Resources/TheBackendSettings.asset";
            Assert.That(
                MukJumpBackendReleaseValidator.ExpectedPackageName,
                Is.EqualTo(MukJumpStoreBuild.DefaultBundleIdentifier));
            Assert.That(
                MukJumpBackendReleaseValidator.SerializedValueMatches(
                    backendPath,
                    "packageName",
                    MukJumpBackendReleaseValidator.ExpectedPackageName),
                Is.True);
            Assert.That(
                MukJumpBackendReleaseValidator.SerializedValueSha256Matches(
                    backendPath,
                    "clientAppID",
                    "417028671d793073b90595866eb26cdd21e9d755cc755c7b7ae4abc8619d9d40"),
                Is.True,
                "다른 프로젝트의 뒤끝 앱 ID가 섞이면 출시 검증이 실패해야 합니다.");
            Assert.That(
                MukJumpBackendReleaseValidator.SerializedValueSha256Matches(
                    backendPath,
                    "signatureKey",
                    "d142f72c7d636df04c7b766997a7e6f18acb085df6d64c361c1b2e683c0ed143"),
                Is.True,
                "다른 프로젝트의 뒤끝 서명키가 섞이면 출시 검증이 실패해야 합니다.");
            Assert.That(
                MukJumpBackendSettings.Load().AllTimeRankUuid,
                Is.EqualTo(
                    MukJumpBackendReleaseValidator.ExpectedAllTimeRankUuid));
            Assert.That(
                MukJumpBackendSettings.Load().PlayerTableName,
                Is.EqualTo(
                    MukJumpBackendReleaseValidator.ExpectedPlayerTableName));
            Assert.That(
                MukJumpBackendSettings.Load().BestHeightColumn,
                Is.EqualTo(
                    MukJumpBackendReleaseValidator.ExpectedBestHeightColumn));
        }

        [Test]
        public void PrivacyMinimizingBackendFlagsMustBeDisabled()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mukjump-backend-privacy-" +
                Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(
                    path,
                    "sendLogReport: 0\n" +
                    "autoLoadLocationProperties: 0\n");

                Assert.That(
                    MukJumpBackendReleaseValidator.SerializedValueMatches(
                        path,
                        "sendLogReport",
                        "0"),
                    Is.True);
                Assert.That(
                    MukJumpBackendReleaseValidator.SerializedValueMatches(
                        path,
                        "autoLoadLocationProperties",
                        "0"),
                    Is.True);
                Assert.That(
                    MukJumpBackendReleaseValidator.SerializedValueMatches(
                        path,
                        "sendLogReport",
                        "1"),
                    Is.False);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ExternalSecurityChecksDefaultToUnverified()
        {
            var settings = ScriptableObject.CreateInstance<
                MukJumpBackendSettings>();
            try
            {
                Assert.That(
                    settings.AppleRevocationConfigurationVerified,
                    Is.False);
                Assert.That(
                    settings.AppleAccountChangeWebhookVerified,
                    Is.False);
                Assert.That(
                    settings.AndroidSigningHashesVerified,
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
