using System;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class DistanceExperienceTests
    {
        MemoryPermanentGrowthStore store;
        GameObject host;
        [SetUp] public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        }
        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [TestCase(0L, 0)] [TestCase(49L, 0)] [TestCase(50L, 1)]
        [TestCase(350L, 4)] [TestCase(500L, 5)] [TestCase(1000L, 8)]
        [TestCase(1550L, 12)] [TestCase(4550L, 24)] [TestCase(10150L, 40)]
        [TestCase(112149L, 243)] [TestCase(112150L, 244)]
        [TestCase(long.MaxValue, 244)] [TestCase(-1L, 0)]
        public void EarlyRewardsFollowApprovedCurve(long distance, int expected) =>
            Assert.That(RunRewardCalculator.GetRewardCountForDistance(distance), Is.EqualTo(expected));

        [Test]
        public void ShortRunsAccumulateAcrossReloadAndCarryTheRemainder()
        {
            Assert.That(PermanentGrowthProfile.SettleRun("a", 200, 0, true).Earned, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.DistanceRewardProgressMeters, Is.EqualTo(50));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.SettleRun("b", 300, 0, true).Earned, Is.EqualTo(3));
            Assert.That(PermanentGrowthProfile.DistanceRewardProgressMeters, Is.Zero);
            Assert.That(PermanentGrowthProfile.SettleRun("c", 1200, 0, true).Earned, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(12));
            Assert.That(PermanentGrowthProfile.DistanceRewardProgressMeters, Is.EqualTo(150));
            Assert.That(PermanentGrowthProfile.DistanceToNextRewardMeters, Is.EqualTo(100));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(1700));
            Assert.That(PermanentGrowthProfile.SettleRun("c", 1200, 0, true).Accepted, Is.False);
            Assert.That(PermanentGrowthProfile.SettleRun("debug", 9000, 0, false).Earned, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(1700));
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string json), Is.True);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(json), Is.True);
        }

        [TestCase(380, 245)]
        [TestCase(380, 1200)]
        [TestCase(121900, 500)]
        public void PreviewMatchesSettlementWithoutSavingOrAwarding(int before, int height)
        {
            PermanentGrowthProfile.SettleRun("before", before, 0, true);
            int writes = store.SaveCount, balance = PermanentGrowthProfile.Currency;
            string json = store.Json;
            var preview = PermanentGrowthProfile.PreviewRun(height, true);
            Assert.That(store.SaveCount, Is.EqualTo(writes));
            Assert.That(store.Json, Is.EqualTo(json));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(before));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(balance));
            var settled = PermanentGrowthProfile.SettleRun("after", height, 0, true);
            Assert.That(preview.Earned, Is.EqualTo(settled.Earned));
            Assert.That(preview.Balance, Is.EqualTo(settled.Balance));
            Assert.That(preview.CumulativeDistanceMeters, Is.EqualTo(settled.CumulativeDistanceMeters));
            Assert.That(preview.PreviousRewardDistanceMeters, Is.EqualTo(settled.PreviousRewardDistanceMeters));
            Assert.That(preview.NextRewardDistanceMeters, Is.EqualTo(settled.NextRewardDistanceMeters));
            Assert.That(preview.DistanceJourneyComplete, Is.EqualTo(settled.DistanceJourneyComplete));
        }

        [Test]
        public void V9KeepsWalletPurchasesRealDistanceAndUnpaidMetersWithoutRewardDebt()
        {
            store.Json = LegacyV9;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.GetLevel(PermanentGrowthType.InkCapacity), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(275));
            Assert.That(PermanentGrowthProfile.DistanceRewardProgressMeters, Is.EqualTo(25));
            Assert.That(PermanentGrowthProfile.DistanceToNextRewardMeters, Is.EqualTo(125));
            Assert.That(PermanentGrowthProfile.IsRunSettled("legacy-run"), Is.True);
            Assert.That(store.Json, Does.Contain("\"distanceRewardOffsetMeters\":700"));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(LegacyV9), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.SettleRun("almost", 124, 0, true).Earned, Is.Zero);
            Assert.That(PermanentGrowthProfile.SettleRun("cross", 1, 0, true).Earned, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(400));
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string json), Is.True);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(json), Is.True);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(json.Replace("\"distanceRewardOffsetMeters\":700,", "")), Is.False);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(json.Replace("\"distanceRewardOffsetMeters\":700", "\"distanceRewardOffsetMeters\":-1")), Is.False);
        }

        [TestCase(GameLanguage.Korean, "먹빛 +1 · 50 / 100m", "누적 200m")]
        [TestCase(GameLanguage.English, "Inklight +1 · 50 / 100m", "Total 200m")]
        public void GrowthScreenShowsReadableExperienceWithoutAnExtraPanel(GameLanguage language, string label, string total)
        {
            GameLocalization.SetLanguage(language);
            PermanentGrowthProfile.SettleRun("partial", 200, 0, true);
            host = new GameObject("ExperienceGrowthUi");
            var view = host.AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            Assert.That(view.DistanceProgressLabel, Is.EqualTo(label));
            Transform root = view.ScreenRoot.Find("SafeAreaRoot/PermanentGrowthScreen/HeaderGroup/DistanceExperience");
            Text progress = root.Find("Progress").GetComponent<Text>();
            Text totalLabel = root.Find("Total").GetComponent<Text>();
            Assert.That(totalLabel.text, Is.EqualTo(total));
            Assert.That(progress.fontSize, Is.EqualTo(44));
            Assert.That(totalLabel.fontSize, Is.EqualTo(40));
            Assert.That(progress.resizeTextForBestFit, Is.False);
            Assert.That(totalLabel.resizeTextForBestFit, Is.False);
            Assert.That(root.Find("Fill").GetComponent<Image>().fillAmount, Is.EqualTo(.5f).Within(.001f));
            Assert.That(root.GetComponent<Image>(), Is.Null);
            Assert.That(progress.preferredWidth, Is.LessThanOrEqualTo(progress.rectTransform.rect.width));
            Assert.That(progress.preferredHeight, Is.LessThanOrEqualTo(progress.rectTransform.rect.height));
            Assert.That(totalLabel.preferredWidth, Is.LessThanOrEqualTo(totalLabel.rectTransform.rect.width));
            Assert.That(totalLabel.preferredHeight, Is.LessThanOrEqualTo(totalLabel.rectTransform.rect.height));
            GameLocalization.SetLanguage(language == GameLanguage.Korean ? GameLanguage.English : GameLanguage.Korean);
            Assert.That(view.DistanceProgressLabel, Is.EqualTo(language == GameLanguage.Korean
                ? "Inklight +1 · 50 / 100m" : "먹빛 +1 · 50 / 100m"));
        }

        [Test]
        public void EveryRewardBoundaryAndIntervalIsMonotonic()
        {
            for (int n = 1; n <= RunRewardCalculator.MaxRewardCount; n++)
            {
                long threshold = RunRewardCalculator.GetThresholdForRewardCount(n);
                Assert.That(RunRewardCalculator.GetRewardCountForDistance(threshold - 1), Is.EqualTo(n - 1));
                Assert.That(RunRewardCalculator.GetRewardCountForDistance(threshold), Is.EqualTo(n));
                Assert.That(threshold - RunRewardCalculator.GetThresholdForRewardCount(n - 1),
                    Is.EqualTo(RunRewardCalculator.GetRequiredMetersForNextReward(n - 1)));
            }
        }

        [Test]
        public void V10PaysOnlyTheDifferenceAndReloadCloudReplayAndResetCannotDuplicateIt()
        {
            string old = LegacyV9.Replace("\"balanceVersion\":9", "\"balanceVersion\":10,\"distanceRewardOffsetMeters\":0")
                .Replace("\"wallet\":7", "\"wallet\":1")
                .Replace("\"claimedDistanceRewardCount\":8", "\"claimedDistanceRewardCount\":2")
                .Replace("\"cumulativeDistanceMeters\":275", "\"cumulativeDistanceMeters\":1000");
            store.Json = old;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7)); // 기수령 2 → 8, 구매 1 유지
            Assert.That(PermanentGrowthProfile.GetLevel(PermanentGrowthType.InkCapacity), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.DistanceRewardProgressMeters, Is.EqualTo(50));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(old), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(7));
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(8)); // 구매액만 환급
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.EqualTo(8));
            Assert.That(PermanentGrowthProfile.SettleRun("zero", 0, 0, true).Earned, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(1000));
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(store.Json), Is.True);
        }

        [Test]
        public void ExperienceContentIsCenteredOnBalanceWithUnchangedBarAndHeaderClearance()
        {
            host = new GameObject("ExperienceGrowthAlignment");
            var view = host.AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            Transform header = view.ScreenRoot.Find("SafeAreaRoot/PermanentGrowthScreen/HeaderGroup");
            var root = (RectTransform)header.Find("DistanceExperience");
            var progress = (RectTransform)root.Find("Progress");
            var total = (RectTransform)root.Find("Total");
            var track = (RectTransform)root.Find("Track");
            var fill = (RectTransform)root.Find("Fill");
            var balance = (RectTransform)header.Find("BalanceHud");
            var back = (RectTransform)header.Find("BackButton");
            float top = progress.anchoredPosition.y + progress.rect.yMax;
            float bottom = total.anchoredPosition.y + total.rect.yMin;
            Assert.That(root.anchoredPosition.y + (top + bottom) * .5f,
                Is.EqualTo(balance.anchoredPosition.y).Within(.01f));
            Assert.That(root.rect.yMax, Is.EqualTo(top));
            Assert.That(root.rect.yMin, Is.EqualTo(bottom));
            Assert.That(track.sizeDelta, Is.EqualTo(new Vector2(640f, 14f)));
            Assert.That(fill.sizeDelta, Is.EqualTo(track.sizeDelta));
            Assert.That(fill.anchoredPosition, Is.EqualTo(track.anchoredPosition));
            Assert.That(progress.anchoredPosition.y + progress.rect.yMin
                - (track.anchoredPosition.y + track.rect.yMax), Is.GreaterThanOrEqualTo(9f));
            Assert.That(track.anchoredPosition.y + track.rect.yMin
                - (total.anchoredPosition.y + total.rect.yMax), Is.GreaterThanOrEqualTo(9f));
            Assert.That(back.anchoredPosition.y + back.rect.yMin
                - (root.anchoredPosition.y + top), Is.GreaterThanOrEqualTo(10f));
            Assert.That(balance.anchoredPosition, Is.EqualTo(new Vector2(415f, 720f)));
            Assert.That(balance.sizeDelta, Is.EqualTo(new Vector2(230f, 120f)));
        }

        const string LegacyV9 = "{\"schemaVersion\":1,\"balanceVersion\":9,\"wallet\":7,\"spent\":1," +
            "\"tutorialRewardClaimed\":true,\"rewardMilestoneWatermarkInitialized\":true,\"rewardedBestHeight\":0," +
            "\"cumulativeDistanceMeters\":275,\"claimedDistanceRewardCount\":8," +
            "\"lastSettledRunId\":\"legacy-run\",\"settledRunIds\":[\"legacy-run\"],\"ranks\":[]," +
            "\"ownedNodeIds\":[\"ink.1\"],\"survivalKeystoneId\":\"\",\"leapKeystoneId\":\"\",\"inkHandlingKeystoneId\":\"\"}";
    }
}
