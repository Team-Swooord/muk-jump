using System;
using System.IO;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class GoogleMobileAdsConfigurationTests
    {
        MukJumpGoogleAdsSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject
                .CreateInstance<MukJumpGoogleAdsSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            if (settings != null)
                UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void EditorAndDevelopmentUseOfficialGoogleTestUnits()
        {
            GoogleAdUnitSet android =
                GoogleMobileAdsTestIds.For(GoogleAdsPlatform.Android);
            GoogleAdUnitSet ios =
                GoogleMobileAdsTestIds.For(GoogleAdsPlatform.IOS);

            Assert.That(settings.ShouldUseTestAds(true, false), Is.True);
            Assert.That(settings.ShouldUseTestAds(false, true), Is.True);
            Assert.That(settings.ShouldUseTestAds(false, false, true), Is.True);
            Assert.That(settings.ShouldUseTestAds(false, false, false), Is.False);
            Assert.That(android.Banner,
                Is.EqualTo("ca-app-pub-3940256099942544/9214589741"));
            Assert.That(android.Rewarded,
                Is.EqualTo("ca-app-pub-3940256099942544/5224354917"));
            Assert.That(android.Interstitial,
                Is.EqualTo("ca-app-pub-3940256099942544/1033173712"));
            Assert.That(ios.Banner,
                Is.EqualTo("ca-app-pub-3940256099942544/2435281174"));
            Assert.That(ios.Rewarded,
                Is.EqualTo("ca-app-pub-3940256099942544/1712485313"));
            Assert.That(ios.Interstitial,
                Is.EqualTo("ca-app-pub-3940256099942544/4411468910"));
        }

        [Test]
        public void TestFlightQaVariantForcesTestAdsWithoutChangingProductionIds()
        {
            string runtime = File.ReadAllText(
                "Assets/Scripts/Core/GoogleMobileAdsRuntime.cs");
            string build = File.ReadAllText(
                "Assets/Editor/MukJumpStoreBuild.cs");

            Assert.That(
                MukJumpStoreBuild.TestFlightQaAdsDefine,
                Is.EqualTo("MUKJUMP_TEST_ADS"));
            Assert.That(runtime, Does.Contain("#if MUKJUMP_TEST_ADS"));
            Assert.That(runtime, Does.Contain("ForceTestAdsForBuild = true"));
            Assert.That(runtime, Does.Contain("GoogleMobileAdsTestIds.For(platform)"));
            Assert.That(build, Does.Contain("extraScriptingDefines"));
            Assert.That(build, Does.Contain("new[] { TestFlightQaAdsDefine }"));

            settings.ConfigureProductionAdMobIds();
            Assert.That(
                settings.AppIdFor(GoogleAdsPlatform.IOS),
                Is.EqualTo("ca-app-pub-2944517353618559~8630103905"));
        }

        [Test]
        public void ReleaseRejectsShiftPublisherMismatchAndMissingBanner()
        {
            settings.ConfigureForTests(
                true,
                true,
                false,
                MukJumpGoogleAdsSettings.AndroidProductionAppId,
                new GoogleAdUnitSet(
                    string.Empty,
                    "ca-app-pub-9163142359221291/3658718565",
                    "ca-app-pub-9163142359221291/4477669111"),
                MukJumpGoogleAdsSettings.IosProductionAppId,
                new GoogleAdUnitSet(
                    string.Empty,
                    "ca-app-pub-9163142359221291/3288053012",
                    "ca-app-pub-9163142359221291/7375295191"));

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.IOS,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("배너"));
            Assert.That(
                MukJumpGoogleAdsSettings.PublisherPrefix(
                    settings.AppIdFor(GoogleAdsPlatform.IOS)),
                Is.Not.EqualTo(
                    MukJumpGoogleAdsSettings.PublisherPrefix(
                        settings.UnitsFor(GoogleAdsPlatform.IOS).Rewarded)));
        }

        [Test]
        public void ReleaseRejectsCompleteConfigurationFromOtherPublisher()
        {
            var units = new GoogleAdUnitSet(
                "ca-app-pub-1234567890123456/1000000001",
                "ca-app-pub-1234567890123456/1000000002",
                "ca-app-pub-1234567890123456/1000000003");
            settings.ConfigureForTests(
                true,
                true,
                false,
                "ca-app-pub-1234567890123456~2000000001",
                units,
                "ca-app-pub-1234567890123456~2000000002",
                units);

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string androidError),
                Is.False);
            Assert.That(androidError, Does.Contain("cysbandcs@gmail.com"));
            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.IOS,
                    out string iosError),
                Is.False);
            Assert.That(iosError, Does.Contain("cysbandcs@gmail.com"));
        }

        [Test]
        public void ReleaseRejectsOtherAppAndUnitsFromSamePublisher()
        {
            var units = new GoogleAdUnitSet(
                "ca-app-pub-2944517353618559/1000000001",
                "ca-app-pub-2944517353618559/1000000002",
                string.Empty);
            settings.ConfigureForTests(
                true,
                true,
                false,
                "ca-app-pub-2944517353618559~2000000001",
                units,
                "ca-app-pub-2944517353618559~2000000002",
                units);

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string androidError),
                Is.False);
            Assert.That(androidError, Does.Contain("먹점프 전용"));
            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.IOS,
                    out string iosError),
                Is.False);
            Assert.That(iosError, Does.Contain("먹점프 전용"));
        }

        [Test]
        public void ReleaseRejectsPostRunInterstitialForVersionOnePolicy()
        {
            var units = new GoogleAdUnitSet(
                "ca-app-pub-1234567890123456/1000000001",
                "ca-app-pub-1234567890123456/1000000002",
                "ca-app-pub-1234567890123456/1000000003");
            settings.ConfigureForTests(
                true,
                true,
                true,
                "ca-app-pub-1234567890123456~2000000001",
                units,
                "ca-app-pub-1234567890123456~2000000002",
                units);

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("전면 광고"));
        }

        [Test]
        public void ReleaseRequiresLobbyBannerForVersionOnePolicy()
        {
            settings.ConfigureForTests(
                true,
                false,
                false,
                MukJumpGoogleAdsSettings.AndroidProductionAppId,
                new GoogleAdUnitSet(
                    MukJumpGoogleAdsSettings.AndroidProductionBannerId,
                    MukJumpGoogleAdsSettings.AndroidProductionRewardedId,
                    string.Empty),
                MukJumpGoogleAdsSettings.IosProductionAppId,
                new GoogleAdUnitSet(
                    MukJumpGoogleAdsSettings.IosProductionBannerId,
                    MukJumpGoogleAdsSettings.IosProductionRewardedId,
                    string.Empty));

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("로비 배너"));
        }

        [Test]
        public void ProductionPresetUsesMukJumpAdMobUnits()
        {
            settings.ConfigureProductionAdMobIds();

            Assert.That(
                MukJumpGoogleAdsSettings.ProductionPublisherPrefix,
                Is.EqualTo("ca-app-pub-2944517353618559"));

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string androidError),
                Is.True,
                androidError);
            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.IOS,
                    out string iosError),
                Is.True,
                iosError);
            Assert.That(
                settings.AppIdFor(GoogleAdsPlatform.Android),
                Is.EqualTo(MukJumpGoogleAdsSettings.AndroidProductionAppId));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.Android).Banner,
                Is.EqualTo(MukJumpGoogleAdsSettings.AndroidProductionBannerId));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.Android).Rewarded,
                Is.EqualTo(MukJumpGoogleAdsSettings.AndroidProductionRewardedId));
            Assert.That(
                settings.AppIdFor(GoogleAdsPlatform.IOS),
                Is.EqualTo(MukJumpGoogleAdsSettings.IosProductionAppId));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.IOS).Banner,
                Is.EqualTo(MukJumpGoogleAdsSettings.IosProductionBannerId));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.IOS).Rewarded,
                Is.EqualTo(MukJumpGoogleAdsSettings.IosProductionRewardedId));
        }

        [Test]
        public void ReleaseRejectsGoogleTestUnits()
        {
            settings.ConfigureForTests(
                true,
                true,
                false,
                GoogleMobileAdsTestIds.AndroidApp,
                GoogleMobileAdsTestIds.For(GoogleAdsPlatform.Android),
                GoogleMobileAdsTestIds.IOSApp,
                GoogleMobileAdsTestIds.For(GoogleAdsPlatform.IOS));

            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.Android,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("테스트"));
        }

        [Test]
        public void NativeAdsDisablePublisherPersonalizationAndUseNpa()
        {
            string runtime = File.ReadAllText(
                "Assets/Scripts/Core/GoogleMobileAdsRuntime.cs");
            string requestFactory = File.ReadAllText(
                "Assets/Scripts/Core/GoogleMobileAdsRequestFactory.cs");

            Assert.That(
                runtime,
                Does.Contain("PublisherFirstPartyIdEnabled = false"));
            Assert.That(
                runtime,
                Does.Contain(
                    "PublisherPrivacyPersonalizationState.Disabled"));
            Assert.That(runtime, Does.Contain("MaxAdContentRating.G"));
            Assert.That(
                runtime,
                Does.Contain("RequestTrackingAuthorizationThenGatherConsent"));
            Assert.That(runtime, Does.Contain("RequestAuthorizationTracking"));
            Assert.That(
                MukJumpGoogleMobileAdsSetup.TrackingUsageDescription,
                Is.Not.Empty);
            Assert.That(
                runtime,
                Does.Contain("AgeRestrictedTreatment.Unspecified"));
            Assert.That(
                runtime,
                Does.Not.Contain("TagForChildDirectedTreatment.False"));
            Assert.That(
                runtime,
                Does.Not.Contain("TagForUnderAgeOfConsent.False"));
            Assert.That(
                runtime,
                Does.Not.Contain("TagForUnderAgeOfConsent = false"));
            Assert.That(requestFactory, Does.Contain("request.Extras[\"npa\"] = \"1\""));
            Assert.That(
                MukJumpGoogleMobileAdsSetup.CollectPrivacyPolicyIssues(),
                Is.Empty);
        }

        [TestCase(true, true, false, true, true)]
        [TestCase(false, true, false, true, false)]
        [TestCase(true, false, false, true, false)]
        [TestCase(true, true, true, true, false)]
        [TestCase(true, true, false, false, false)]
        public void AttWaitsForActiveMainScreen(bool focused, bool active, bool splash, bool main, bool expected)
        {
            Assert.That(GoogleMobileAdsRuntimePolicy.CanRequestTrackingAuthorization(
                focused, active, splash, main), Is.EqualTo(expected));
        }

        [TestCase(false, 40d, 30d, false)]
        [TestCase(true, 29.999d, 30d, false)]
        [TestCase(true, 30d, 30d, true)]
        [TestCase(true, 40d, 0d, false)]
        public void AttWatchdogOnlyExpiresAnActiveRequestWithADeadline(
            bool requestInFlight,
            double now,
            double deadline,
            bool expected)
        {
            Assert.That(
                GoogleMobileAdsRuntimePolicy
                    .HasTrackingAuthorizationTimedOut(
                        requestInFlight,
                        now,
                        deadline),
                Is.EqualTo(expected));
        }

        [Test]
        public void NativeAdSdkBoundariesHaveRetryAndFailOpenGuards()
        {
            string runtime = File.ReadAllText(
                "Assets/Scripts/Core/GoogleMobileAdsRuntime.cs");

            Assert.That(runtime, Does.Contain("TryConfigureSdkAndBeginFlow"));
            Assert.That(runtime, Does.Contain("nextSdkSetupRetryTime"));
            Assert.That(runtime, Does.Contain("TryGetTrackingAuthorizationStatus"));
            Assert.That(runtime, Does.Contain("trackingAuthorizationDeadline"));
            Assert.That(
                runtime,
                Does.Contain("ResolveTrackingAuthorizationAfterFailure"));
            Assert.That(runtime, Does.Contain("completionSent"));
        }

        [Test]
        public void GeneratedAndroidProjectRequiresAdsConsentAndGoogleLogin()
        {
            string projectRoot = Path.Combine(
                Path.GetTempPath(),
                "MukJumpAndroidGradle-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(projectRoot, "unityLibrary");
            string manifestFolder = Path.Combine(
                root,
                "GoogleMobileAdsPlugin.androidlib");
            string libsFolder = Path.Combine(root, "libs");
            Directory.CreateDirectory(manifestFolder);
            Directory.CreateDirectory(libsFolder);
            const string appId =
                "ca-app-pub-3940256099942544~3347511713";

            try
            {
                File.WriteAllText(
                    Path.Combine(root, "build.gradle"),
                    "implementation 'com.google.android.gms:play-services-ads:25.4.0'\n" +
                    "implementation 'com.google.android.ump:user-messaging-platform:4.0.0'\n" +
                    "implementation 'com.google.android.gms:play-services-auth:19.0.0'\n" +
                    "implementation(name: 'io.thebackend.googlelogin', ext:'aar')\n");
                File.WriteAllText(
                    Path.Combine(manifestFolder, "AndroidManifest.xml"),
                    "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
                    "<application><meta-data " +
                    "android:name=\"com.google.android.gms.ads.APPLICATION_ID\" " +
                    $"android:value=\"{appId}\" /></application></manifest>");
                File.WriteAllBytes(
                    Path.Combine(libsFolder, "googlemobileads-unity.aar"),
                    new byte[] { 1 });
                File.WriteAllBytes(
                    Path.Combine(libsFolder, "io.thebackend.googlelogin.aar"),
                    new byte[] { 1 });

                Assert.That(
                    MukJumpAndroidGeneratedProjectValidator.CollectIssues(
                        root,
                        appId),
                    Is.Empty);

                File.WriteAllText(
                    Path.Combine(root, "build.gradle"),
                    "implementation 'com.google.android.gms:play-services-ads:25.4.0'");
                string[] issues = MukJumpAndroidGeneratedProjectValidator
                    .CollectIssues(root, "ca-app-pub-0000000000000000~0000000000");
                Assert.That(issues, Has.Some.Contains("user-messaging-platform"));
                Assert.That(issues, Has.Some.Contains("play-services-auth"));
                Assert.That(issues, Has.Some.Contains("앱 ID"));
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                    Directory.Delete(projectRoot, recursive: true);
            }
        }
    }
}
