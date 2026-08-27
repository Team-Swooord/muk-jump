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
