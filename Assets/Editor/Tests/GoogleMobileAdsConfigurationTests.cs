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
                Object.DestroyImmediate(settings);
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
        public void ReleaseRejectsShiftPublisherMismatchAndMissingBanner()
        {
            settings.ConfigureForTests(
                true,
                true,
                false,
                "ca-app-pub-2944517353618559~3326566482",
                new GoogleAdUnitSet(
                    string.Empty,
                    "ca-app-pub-9163142359221291/3658718565",
                    "ca-app-pub-9163142359221291/4477669111"),
                "ca-app-pub-2944517353618559~6105294520",
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
        public void ReleaseAcceptsCompleteSamePublisherConfiguration()
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
                Is.True,
                androidError);
            Assert.That(
                settings.TryValidateProduction(
                    GoogleAdsPlatform.IOS,
                    out string iosError),
                Is.True,
                iosError);
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
        public void ProductionPresetUsesMukJumpAdMobUnits()
        {
            settings.ConfigureProductionAdMobIds();

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
                Is.EqualTo("ca-app-pub-2944517353618559~3718407207"));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.Android).Banner,
                Is.EqualTo("ca-app-pub-2944517353618559/6947205773"));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.Android).Rewarded,
                Is.EqualTo("ca-app-pub-2944517353618559/4202000231"));
            Assert.That(
                settings.AppIdFor(GoogleAdsPlatform.IOS),
                Is.EqualTo("ca-app-pub-2944517353618559~8630103905"));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.IOS).Banner,
                Is.EqualTo("ca-app-pub-2944517353618559/3377777224"));
            Assert.That(
                settings.UnitsFor(GoogleAdsPlatform.IOS).Rewarded,
                Is.EqualTo("ca-app-pub-2944517353618559/5343691518"));
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
                Does.Contain("TagForChildDirectedTreatment.False"));
            Assert.That(
                runtime,
                Does.Contain("TagForUnderAgeOfConsent.False"));
            Assert.That(requestFactory, Does.Contain("request.Extras[\"npa\"] = \"1\""));
            Assert.That(
                MukJumpGoogleMobileAdsSetup.CollectPrivacyPolicyIssues(),
                Is.Empty);
        }
    }
}
