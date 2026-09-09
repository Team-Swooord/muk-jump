using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class FirebaseAnalyticsTests
    {
        readonly List<MukJumpAnalyticsEvent> events = new();
        MemoryPermanentGrowthStore growthStore;
        int savedConsent;
        bool hadConsent;

        [SetUp]
        public void SetUp()
        {
            hadConsent = PlayerPrefs.HasKey(MukJumpAnalyticsPrivacy.ConsentKey);
            savedConsent = PlayerPrefs.GetInt(MukJumpAnalyticsPrivacy.ConsentKey);
            growthStore = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(growthStore);
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            events.Clear();
            MukJumpAnalytics.UseSinkForTests(events.Add);
        }
        [TearDown]
        public void TearDown()
        {
            MukJumpAnalytics.ResetForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            if (hadConsent) PlayerPrefs.SetInt(MukJumpAnalyticsPrivacy.ConsentKey, savedConsent);
            else PlayerPrefs.DeleteKey(MukJumpAnalyticsPrivacy.ConsentKey);
            PlayerPrefs.Save();
        }

        void Begin() => MukJumpAnalytics.BeginRun("private-run-id-never-send", true, 2);
        int Count(string name) => events.Count(e => e.Name == name);

        [Test]
        public void NoConsentDropsEventsInsteadOfReplayingThemLater()
        {
            MukJumpAnalytics.SetCollectionEnabled(false);
            Begin(); MukJumpAnalytics.EarnCurrency(1, 1); MukJumpAnalytics.Screen(AnalyticsScreen.Growth);
            Assert.That(events, Is.Empty);
            MukJumpAnalytics.SetCollectionEnabled(true);
            Assert.That(events, Is.Empty);
            MukJumpAnalytics.Stroke(2);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void WithdrawalClearsQueueAndActiveJourney()
        {
            MukJumpAnalytics.UseSinkForTests(null);
            Begin(); MukJumpAnalytics.EarnCurrency(1, 1);
            Assert.That(MukJumpAnalytics.PendingCountForTests, Is.GreaterThan(0));
            MukJumpAnalytics.SetCollectionEnabled(false);
            Assert.That(MukJumpAnalytics.PendingCountForTests, Is.Zero);
            MukJumpAnalytics.SetCollectionEnabled(true);
            MukJumpAnalytics.EndRun("private-run-id-never-send", 100, 20, false, false);
            Assert.That(MukJumpAnalytics.PendingCountForTests, Is.Zero);
        }

        [Test]
        public void InitializationQueueIsBounded()
        {
            MukJumpAnalytics.UseSinkForTests(null);
            for (int i = 0; i < 1000; i++) MukJumpAnalytics.Ad(AnalyticsAdStage.Loaded);
            Assert.That(MukJumpAnalytics.PendingCountForTests, Is.EqualTo(128));
        }

        [Test]
        public void ThrowingSdkCannotInterruptGameActions()
        {
            MukJumpAnalytics.UseSinkForTests(_ => throw new InvalidOperationException());
            Assert.DoesNotThrow(() => { Begin(); MukJumpAnalytics.Stroke(2); MukJumpAnalytics.EarnCurrency(1, 1); });
        }

        [Test]
        public void ReviveContinuesSameRunAndFinalSettlementIsDeduplicated()
        {
            Begin(); Begin();
            MukJumpAnalytics.GameOver(70, 40);
            Assert.That(Count("level_end"), Is.Zero);
            MukJumpAnalytics.Revive();
            MukJumpAnalytics.GameOver(170, 100);
            MukJumpAnalytics.EndRun("private-run-id-never-send", 170, 100, false, true);
            MukJumpAnalytics.EndRun("private-run-id-never-send", 170, 100, false, true);
            Assert.That(Count("level_start"), Is.EqualTo(1));
            Assert.That(Count("run_death"), Is.EqualTo(2));
            Assert.That(Count("level_end"), Is.EqualTo(1));
            Assert.That(Count("post_score"), Is.EqualTo(1));
            Assert.That(events.Single(e => e.Name == "level_end").Parameters["revive_count"], Is.EqualTo(1L));
        }

        [Test]
        public void AbandonedRunDoesNotPostScore()
        {
            Begin(); MukJumpAnalytics.EndRun("private-run-id-never-send", 10, 4, true, false);
            Assert.That(Count("post_score"), Is.Zero);
            Assert.That(events.Single(e => e.Name == "level_end").Parameters["end_reason"], Is.EqualTo("abandon"));
        }

        [Test]
        public void DebugAndRestoredRunsDoNotInventSessions()
        {
            MukJumpAnalytics.BeginRun("debug", false, 32);
            MukJumpAnalytics.EndRun("restored", 100, 20, false, false);
            Assert.That(events, Is.Empty);
            Begin(); MukJumpAnalytics.ExcludeDebugRun();
            MukJumpAnalytics.GameOver(10000, 100);
            Assert.That(Count("run_death"), Is.Zero);
        }

        [Test]
        public void HighFrequencyActionsAreAggregatedAndMilestonesAreOncePerRun()
        {
            Begin();
            for (int i = 0; i < 1000; i++)
            {
                MukJumpAnalytics.Stroke(1.5f);
                MukJumpAnalytics.Item(MukJump.Items.ItemType.GoldenBrush);
                MukJumpAnalytics.Damage(i % 2 == 0);
                MukJumpAnalytics.Progress(1000, 24);
            }
            MukJumpAnalytics.GameOver(1000, 200);
            Assert.That(Count("first_stroke"), Is.EqualTo(1));
            Assert.That(Count("item_first_pickup"), Is.EqualTo(1));
            Assert.That(Count("height_milestone"), Is.EqualTo(6));
            var p = events.Single(e => e.Name == "run_death").Parameters;
            Assert.That(p["stroke_count"], Is.EqualTo(1000L));
            Assert.That(p["goldenbrush_count"], Is.EqualTo(1000L));
            Assert.That(p["fall_damage_count"], Is.EqualTo(500L));
            Assert.That(p["peak_swarm"], Is.EqualTo(24L));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TutorialSkipAndCompletionAreDifferent(bool skipped)
        {
            MukJumpAnalytics.TutorialBegin(); MukJumpAnalytics.TutorialBegin();
            MukJumpAnalytics.TutorialStep(0); MukJumpAnalytics.TutorialStep(0);
            MukJumpAnalytics.TutorialStep(1);
            MukJumpAnalytics.TutorialEnd(skipped, true); MukJumpAnalytics.TutorialEnd(skipped, true);
            Assert.That(Count("tutorial_begin"), Is.EqualTo(1));
            Assert.That(Count("tutorial_step"), Is.EqualTo(2));
            Assert.That(Count(skipped ? "tutorial_skip" : "tutorial_complete"), Is.EqualTo(1));
            Assert.That(Count(skipped ? "tutorial_complete" : "tutorial_skip"), Is.Zero);
        }

        [Test]
        public void CurrencyAndGrowthOnlyLogCommittedTransactions()
        {
            var result = PermanentGrowthProfile.SettleRun("earned", 50, 50, 0, 80, true);
            Assert.That(result.Accepted, Is.True);
            Assert.That(Count("earn_virtual_currency"), Is.EqualTo(1));
            PermanentGrowthProfile.SettleRun("earned", 50, 50, 0, 80, true);
            Assert.That(Count("earn_virtual_currency"), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.Vitality), Is.True);
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.Vitality), Is.False);
            Assert.That(Count("spend_virtual_currency"), Is.EqualTo(1));
            Assert.That(Count("level_up"), Is.EqualTo(1));
            PermanentGrowthProfile.TryResetPurchasedNodes(); PermanentGrowthProfile.TryResetPurchasedNodes();
            Assert.That(Count("growth_reset"), Is.EqualTo(1));
            Assert.That(Count("earn_virtual_currency"), Is.EqualTo(2));
        }

        [Test]
        public void FailedSaveDoesNotLogCurrencyOrPurchase()
        {
            PermanentGrowthProfile.SettleRun("fund", 1000, 1000, 0, 80, true);
            events.Clear();
            growthStore.ThrowOnPrimarySave = true;
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.Vitality), Is.False);
            Assert.That(Count("level_up"), Is.Zero);
            Assert.That(Count("spend_virtual_currency"), Is.Zero);
            PermanentGrowthProfile.SettleRun("failed", 500, 500, 0, 20, true);
            Assert.That(Count("earn_virtual_currency"), Is.Zero);
        }

        [Test]
        public void InterruptedTutorialCanBeginAgainWithoutFakeCompletion()
        {
            MukJumpAnalytics.TutorialBegin(); MukJumpAnalytics.TutorialStep(0);
            MukJumpAnalytics.TutorialInterrupted(); MukJumpAnalytics.TutorialInterrupted();
            MukJumpAnalytics.TutorialBegin(); MukJumpAnalytics.TutorialStep(0);
            Assert.That(Count("tutorial_begin"), Is.EqualTo(2));
            Assert.That(Count("tutorial_step"), Is.EqualTo(2));
            Assert.That(Count("tutorial_exit"), Is.EqualTo(1));
            Assert.That(Count("tutorial_complete"), Is.Zero);
        }

        [TestCase(AnalyticsDeathCause.Fall, "fall")]
        [TestCase(AnalyticsDeathCause.Obstacle, "obstacle")]
        public void LastCharacterDeathCauseIsIncluded(AnalyticsDeathCause cause, string expected)
        {
            Begin(); MukJumpAnalytics.PlayerDied(cause); MukJumpAnalytics.GameOver(100, 20);
            Assert.That(events.Single(e => e.Name == "run_death").Parameters["death_cause"], Is.EqualTo(expected));
        }

        [Test]
        public void InstalledAdMobSdkProvidesInstrumentedCallbacks()
        {
            Assert.That(typeof(GoogleMobileAds.Api.RewardedAd).GetEvent("OnAdFullScreenContentOpened"), Is.Not.Null);
            Assert.That(typeof(GoogleMobileAds.Api.RewardedAd).GetEvent("OnAdClicked"), Is.Not.Null);
        }

        [Test]
        public void AllEventPayloadsStayWithinFirebaseLimitsAndContainNoRunIdentifier()
        {
            Begin(); MukJumpAnalytics.Progress(int.MaxValue, 999);
            foreach (MukJump.Items.ItemType item in Enum.GetValues(typeof(MukJump.Items.ItemType))) MukJumpAnalytics.Item(item);
            MukJumpAnalytics.Stroke(float.NaN); MukJumpAnalytics.Stroke(1);
            MukJumpAnalytics.TutorialBegin(); MukJumpAnalytics.TutorialStep(0); MukJumpAnalytics.TutorialEnd(false, true);
            MukJumpAnalytics.Upgrade(PermanentGrowthType.Vitality, 1, 1, 0);
            MukJumpAnalytics.EarnCurrency(1, 1); MukJumpAnalytics.Ad(AnalyticsAdStage.Failed);
            MukJumpAnalytics.Account(AnalyticsAccountAction.AppleLink, AnalyticsOutcome.Requested);
            MukJumpAnalytics.Night(true);
            MukJumpAnalytics.GameOver(1000, float.NaN);
            MukJumpAnalytics.EndRun("private-run-id-never-send", 1000, 50, false, true);
            foreach (var e in events)
            {
                Assert.That(e.Name, Does.Match("^[a-z][a-z0-9_]{0,39}$"));
                Assert.That(e.Parameters.Count, Is.LessThanOrEqualTo(25));
                foreach (var pair in e.Parameters)
                {
                    Assert.That(pair.Key, Does.Match("^[a-z][a-z0-9_]{0,39}$"));
                    Assert.That(pair.Key, Does.Not.Match("uuid|user_id|nickname|token|run_id|email|error_message"));
                    Assert.That(pair.Value, Is.TypeOf<string>().Or.TypeOf<long>().Or.TypeOf<double>());
                    Assert.That(pair.Value.ToString(), Does.Not.Contain("private-run-id"));
                    if (pair.Value is string s) Assert.That(s.Length, Is.LessThanOrEqualTo(100));
                    if (pair.Value is double d) Assert.That(double.IsNaN(d) || double.IsInfinity(d), Is.False);
                }
            }
        }

        [TestCase("wrong", false)]
        [TestCase("com.CYSB.MukJump", true)]
        public void ConfigMustMatchActualBundle(string bundle, bool expected)
        {
            const string xml = "<plist><dict><key>BUNDLE_ID</key><string>com.CYSB.MukJump</string><key>GOOGLE_APP_ID</key><string>1:123:ios:abc</string><key>PROJECT_ID</key><string>muk-test</string><key>API_KEY</key><string>not-real</string></dict></plist>";
            const string json = "{\"project_info\":{\"project_id\":\"muk-test\"},\"client\":[{\"client_info\":{\"mobilesdk_app_id\":\"1:123:android:abc\",\"android_client_info\":{\"package_name\":\"com.CYSB.MukJump\"}},\"api_key\":[{\"current_key\":\"not-real\"}]}]}";
            Assert.That(MukJumpFirebaseAnalyticsSetup.ValidateIosConfig(xml, bundle), Is.EqualTo(expected));
            Assert.That(MukJumpFirebaseAnalyticsSetup.ValidateAndroidConfig(json, bundle), Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("broken")]
        [TestCase("{}")]
        public void MissingAndInvalidConfigsFailClosed(string content)
        {
            Assert.That(MukJumpFirebaseAnalyticsSetup.ValidateIosConfig(content, "com.CYSB.MukJump"), Is.False);
            Assert.That(MukJumpFirebaseAnalyticsSetup.ValidateAndroidConfig(content, "com.CYSB.MukJump"), Is.False);
        }

        [Test]
        public void NativePrivacyDefaultsAreIdempotentAndKeepOtherSdkSettings()
        {
            string xml = "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><meta-data android:name='admob_keep' android:value='keep'/></application></manifest>";
            string once = MukJumpFirebaseAnalyticsSetup.ApplyAndroidPrivacy(xml);
            Assert.That(MukJumpFirebaseAnalyticsSetup.ApplyAndroidPrivacy(once), Is.EqualTo(once));
            var entries = XDocument.Parse(once).Root.Element("application").Elements().ToArray();
            XNamespace android = "http://schemas.android.com/apk/res/android";
            Assert.That(entries.Length, Is.EqualTo(8));
            Assert.That((string)entries[0].Attribute(android + "value"), Is.EqualTo("keep"));
            foreach (var e in entries.Skip(1)) Assert.That((string)e.Attribute(android + "value"), Is.EqualTo("false"));
            const string pods = "pod 'Firebase/Core', '12.18.0'\npod 'Firebase/Analytics', '12.18.0'\npod 'Google-Mobile-Ads-SDK', '13.9.0'";
            string core = MukJumpFirebaseAnalyticsSetup.UseAnalyticsCorePod(pods);
            Assert.That(core, Does.Contain("FirebaseAnalytics/Core").And.Contain("Google-Mobile-Ads-SDK"));
            Assert.That(core, Does.Contain("Firebase/CoreOnly").And.Not.Contain("'Firebase/Core'"));
            Assert.That(MukJumpFirebaseAnalyticsSetup.UseAnalyticsCorePod(core), Is.EqualTo(core));
            const string packages = "productName = FirebaseAnalytics; productName = FirebaseCore; productName = \"FirebaseAnalytics\";";
            string corePackages = MukJumpFirebaseAnalyticsSetup.UseAnalyticsCoreSwiftPackage(packages);
            Assert.That(corePackages, Is.EqualTo("productName = FirebaseAnalyticsCore; productName = FirebaseCore; productName = FirebaseAnalyticsCore;"));
            Assert.That(MukJumpFirebaseAnalyticsSetup.UseAnalyticsCoreSwiftPackage(corePackages), Is.EqualTo(corePackages));
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        public void PrivacyPageFitsAndChoiceDoesNotStartEditorSdk(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            PlayerPrefs.SetInt(MukJumpAnalyticsPrivacy.ConsentKey, 0);
            var host = new GameObject("AnalyticsPrivacyTest");
            try
            {
                var view = host.AddComponent<LobbyOptionsView>(); view.BuildForTests();
                var panel = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
                var page = panel.Find("AnalyticsPrivacyPage");
                Assert.That(page, Is.Not.Null);
                Call(view, "SetVisible", true);
                panel.Find("OptionsPage/PrivacyButton").GetComponent<Button>().onClick.Invoke();
                Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Canvas.ForceUpdateCanvases();
                foreach (var text in page.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.resizeTextForBestFit, Is.False);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
                    if (language == GameLanguage.English) Assert.That(text.text, Does.Not.Match("[가-힣]"));
                }
                page.Find("EnableAnalytics").GetComponent<Button>().onClick.Invoke();
                Assert.That(MukJumpAnalyticsPrivacy.HasConsent, Is.True);
                page.Find("DisableAnalytics").GetComponent<Button>().onClick.Invoke();
                Assert.That(MukJumpAnalyticsPrivacy.HasConsent, Is.False);
                Assert.That(UnityEngine.Object.FindAnyObjectByType<FirebaseAnalyticsRuntime>(), Is.Null);
                Call(view, "ShowOptionsPage");
                Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
}
