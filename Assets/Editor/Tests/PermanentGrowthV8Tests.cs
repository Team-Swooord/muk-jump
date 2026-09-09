using System;
using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthV8Tests
    {
        // 실제 초기 v7 서버 저장 구조. 사용자 식별자와 개인 기록은 포함하지 않는다.
        internal const string InitialV7ServerJson =
            "{\"schemaVersion\":1,\"balanceVersion\":7,\"wallet\":0,\"spent\":0," +
            "\"rewardedBestHeight\":0,\"cumulativeDistanceMeters\":0," +
            "\"claimedDistanceRewardCount\":0,\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
            "\"ranks\":[],\"ownedNodeIds\":[],\"survivalKeystoneId\":\"\"," +
            "\"leapKeystoneId\":\"\",\"inkHandlingKeystoneId\":\"\"}";

        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void CatalogHasFourPlainChoicesEightStagesEachAnd244Cost()
        {
            Assert.That(PermanentGrowthCatalog.Choices.Count, Is.EqualTo(4));
            Assert.That(PermanentGrowthCatalog.Nodes.Count, Is.EqualTo(32));
            Assert.That(PermanentGrowthCatalog.TotalCost, Is.EqualTo(244));

            string[] names =
            {
                "튼튼한 먹", "넉넉한 먹물", "알뜰한 붓", "높은 도약",
            };
            string[] summaries =
            {
                "부딪혀도 더 오래 버텨요.",
                "먹물을 넉넉히 담아 둘 수 있어요.",
                "그릴 때 먹물이 천천히 줄어요.",
                "더 높은 곳까지 뛰어요.",
            };
            int[] stageCounts = { 8, 8, 8, 8 };
            int[][] costs =
            {
                new[] { 1, 2, 3, 5, 7, 10, 14, 19 },
                new[] { 1, 2, 3, 5, 7, 10, 14, 19 },
                new[] { 1, 2, 3, 5, 7, 10, 14, 19 },
                new[] { 1, 2, 3, 5, 7, 10, 14, 19 },
            };

            for (int choiceIndex = 0;
                 choiceIndex < PermanentGrowthCatalog.Choices.Count;
                 choiceIndex++)
            {
                PermanentGrowthChoiceDefinition choice =
                    PermanentGrowthCatalog.Choices[choiceIndex];
                Assert.That(choice.DisplayName, Is.EqualTo(names[choiceIndex]));
                Assert.That(choice.Summary, Is.EqualTo(summaries[choiceIndex]));
                Assert.That(choice.Summary, Does.Not.Match("[0-9%+→]"));

                PermanentGrowthDefinition track =
                    PermanentGrowthCatalog.Get(choice.Type);
                Assert.That(track.MaxLevel, Is.EqualTo(stageCounts[choiceIndex]));
                Assert.That(
                    Enumerable.Range(0, track.MaxLevel)
                        .Select(track.GetCost),
                    Is.EqualTo(costs[choiceIndex]));
            }

            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.Id).Distinct().Count(),
                Is.EqualTo(32));
            Assert.That(
                PermanentGrowthCatalog.Nodes.Select(node => node.EffectId).Distinct(),
                Is.EquivalentTo(new[]
                {
                    PermanentGrowthType.Vitality,
                    PermanentGrowthType.InkCapacity,
                    PermanentGrowthType.InkBudgetEfficiency,
                    PermanentGrowthType.JumpHeight,
                }));
        }

        [Test]
        public void BuyingAllFourTracksUsesVariableCostsAndAppliesAllEffects()
        {
            store.Json = SaveJson(9, 244, 0, Array.Empty<string>(), 244, 34500, "");
            PermanentGrowthProfile.ResetCacheForTests();

            foreach (PermanentGrowthChoiceDefinition choice
                     in PermanentGrowthCatalog.Choices)
            {
                PermanentGrowthDefinition track =
                    PermanentGrowthCatalog.Get(choice.Type);
                for (int level = 0; level < track.MaxLevel; level++)
                    Assert.That(
                        PermanentGrowthProfile.TryPurchase(choice.Type),
                        Is.True,
                        $"{choice.Id} {level + 1}단계 구매");
            }

            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(244));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(32));
            PermanentGrowthRunSnapshot snapshot =
                PermanentGrowthProfile.CreateRunSnapshot();
            Assert.That(snapshot.MaxHealthBonus, Is.EqualTo(8));
            Assert.That(snapshot.InkCapacityMultiplier,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(snapshot.InkBudgetCostMultiplier,
                Is.EqualTo(0.76f).Within(0.0001f));
            Assert.That(snapshot.JumpHeightMultiplier,
                Is.EqualTo(1.10f).Within(0.0001f));
            Assert.That(snapshot.JumpVerticalSpeedMultiplier,
                Is.EqualTo(Mathf.Sqrt(1.10f)).Within(0.0001f));
            Assert.That(snapshot.HasLastBreath, Is.False);
            Assert.That(snapshot.HasPostHitShield, Is.False);
            Assert.That(snapshot.HasDoubleJump, Is.False);
            Assert.That(snapshot.HasWallCling, Is.False);
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(store.Json, Does.Contain("\"survivalKeystoneId\":\"\""));
            Assert.That(store.Json, Does.Contain("\"leapKeystoneId\":\"\""));
            Assert.That(store.Json, Does.Contain("\"inkHandlingKeystoneId\":\"\""));
        }

        [Test]
        public void ResetRefundsSpentCostAndPreservesJourney()
        {
            string[] owned =
            {
                "body.1", "body.2", "ink.1", "ink.2", "ink.3",
            };
            int spent = OwnedCost(owned);
            store.Json = V8Json(39 - spent, owned, "settled-v8");
            PermanentGrowthProfile.ResetCacheForTests();

            long distance = PermanentGrowthProfile.CumulativeDistanceMeters;
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);

            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(39));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters,
                Is.EqualTo(distance));
            Assert.That(store.Json, Does.Contain("settled-v8"));
        }

        [Test]
        public void ValidV7RefundsAllOldFruitOnceAndPreservesHistory()
        {
            string[] oldOwned = { "S00", "J00", "I00" };
            int claimed = 9;
            store.Json = V7Json(6, oldOwned, claimed, "old-run");
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount,
                Is.EqualTo(claimed));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(store.Json, Does.Contain("old-run"));

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9),
                "v8 재로드에서 환급을 다시 적용하면 안 됩니다.");
        }

        [Test]
        public void InitialV7ServerWithoutRetiredRewardFlagsMigratesWithoutInventingProgress()
        {
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(InitialV7ServerJson), Is.True);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(InitialV7ServerJson), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.Zero);
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(store.Json), Is.True);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
        }

        [Test]
        public void V7RetiredFlagsAreNotNeededForValidatedDistanceEconomy()
        {
            string json = V7Json(6, new[] { "S00", "J00", "I00" }, 9, "old-run")
                .Replace("\"tutorialRewardClaimed\":true,", string.Empty)
                .Replace("\"rewardMilestoneWatermarkInitialized\":true,", string.Empty);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(json), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.Zero);
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.EqualTo(9));
            Assert.That(store.Json, Does.Contain("old-run"));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(9));
        }

        [TestCase("\"wallet\":0,")]
        [TestCase("\"spent\":0,")]
        [TestCase("\"rewardedBestHeight\":0,")]
        [TestCase("\"cumulativeDistanceMeters\":0,")]
        [TestCase("\"claimedDistanceRewardCount\":0,")]
        [TestCase("\"settledRunIds\":[],")]
        [TestCase("\"ranks\":[],")]
        [TestCase("\"ownedNodeIds\":[],")]
        public void InitialV7StillRequiresProgressAndOwnershipFields(string missingField)
        {
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(
                InitialV7ServerJson.Replace(missingField, string.Empty)), Is.False);
        }

        [TestCase(6)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void OnlyInitialV7MayOmitRetiredRewardFlags(int version)
        {
            string json = SaveJson(version, 0, 0, Array.Empty<string>(), 0, 0, "")
                .Replace("\"tutorialRewardClaimed\":true,", string.Empty)
                .Replace("\"rewardMilestoneWatermarkInitialized\":true,", string.Empty);
            if (version >= 10)
                json = json.Replace("\"wallet\":0,", "\"wallet\":0,\"distanceRewardOffsetMeters\":0,");
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(json), Is.False);
        }

        [TestCase("\"wallet\":0", "\"wallet\":1")]
        [TestCase("\"wallet\":0", "\"wallet\":-1")]
        [TestCase("\"claimedDistanceRewardCount\":0", "\"claimedDistanceRewardCount\":1")]
        public void InitialV7StillRejectsInvalidEconomy(string before, string after)
        {
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(
                InitialV7ServerJson.Replace(before, after)), Is.False);
        }

        [Test]
        public void InvalidV7GraphIsRejectedBeforeRefund()
        {
            const string invalid =
                "{\"schemaVersion\":1,\"balanceVersion\":7," +
                "\"wallet\":1,\"spent\":2,\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":true," +
                "\"rewardedBestHeight\":0,\"cumulativeDistanceMeters\":60," +
                "\"claimedDistanceRewardCount\":3," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[],\"ranks\":[]," +
                "\"ownedNodeIds\":[\"S00\",\"S00\"]," +
                "\"survivalKeystoneId\":\"\",\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            store.Json = invalid;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(store.Json, Is.EqualTo(invalid));
        }

        [TestCase(100L, 5)]
        [TestCase(3750L, 39)]
        [TestCase(3899L, 39)]
        [TestCase(3900L, 40)]
        [TestCase(34500L, 244)]
        [TestCase(long.MaxValue, 244)]
        public void V8MigrationKeepsPurchasesAndCreditsExtendedDistanceOnlyOnce(
            long distance, int expected)
        {
            string[] owned = { "body.1", "ink.1" };
            int oldClaimed = Math.Min(expected, 39);
            string legacy = SaveJson(8, oldClaimed - 2, 2, owned,
                oldClaimed, distance, "settled-before-expansion");
            store.Json = legacy;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.GetLevel(PermanentGrowthType.Vitality), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.GetLevel(PermanentGrowthType.InkCapacity), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(expected - 2));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(distance));
            Assert.That(store.Json, Does.Contain("\"balanceVersion\":11"));
            Assert.That(store.Json, Does.Contain("settled-before-expansion"));

            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(expected - 2));
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(legacy), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(expected - 2),
                "동일한 구 서버 저장을 다시 적용해도 보상을 누적 지급하면 안 됩니다.");
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.EqualTo(expected));
            PermanentGrowthProfile.SettleRun("settled-before-expansion", 0, 1000, 0, 0f, true);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(distance));
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(expected));
        }

        [Test]
        public void FullyPurchasedV8KeepsAllFifteenStagesAndOpensNewRanks()
        {
            string[] owned = PermanentGrowthCatalog.Nodes
                .Where(node => node.Rank <= (node.EffectId == PermanentGrowthType.Vitality ? 3 : 4))
                .Select(node => node.Id).ToArray();
            store.Json = SaveJson(8, 0, 39, owned, 39, 3900, "old-full");
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(15));
            Assert.That(PermanentGrowthProfile.SpentCurrency, Is.EqualTo(39));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.GetNextCost(PermanentGrowthType.Vitality), Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.GetNextCost(PermanentGrowthType.InkCapacity), Is.EqualTo(7));
        }

        [TestCase("body", 4)]
        [TestCase("ink", 5)]
        [TestCase("brush", 5)]
        [TestCase("jump", 5)]
        public void V8CannotClaimNewRanks(string prefix, int rank)
        {
            string[] owned = Enumerable.Range(1, rank).Select(i => $"{prefix}.{i}").ToArray();
            string invalid = V8Json(0, owned);
            store.Json = invalid;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(PermanentGrowthProfile.IsSupportedCloudJson(invalid), Is.False);
            Assert.That(store.Json, Is.EqualTo(invalid));
        }

        [TestCase(40, 0, 40)]
        [TestCase(38, 0, 39)]
        [TestCase(39, 1, 39)]
        public void V8CannotForgeExtendedRewardsOrWallet(int wallet, int spent, int claimed)
        {
            string invalid = SaveJson(8, wallet, spent, Array.Empty<string>(), claimed, 3900, "");
            store.Json = invalid;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(store.Json, Is.EqualTo(invalid));
        }

        [Test]
        public void V8MigrationPrimaryFailureCanRecoverWithoutPayingTwice()
        {
            string legacy = SaveJson(8, 39, 0, Array.Empty<string>(), 39, 3900, "old-run");
            store.Json = legacy;
            store.ThrowOnPrimarySave = true;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(store.Json, Is.EqualTo(legacy));
            Assert.That(store.BackupJson, Is.EqualTo(legacy));
            store.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(40));
            Assert.That(PermanentGrowthProfile.ClaimedDistanceRewardCount, Is.EqualTo(40));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(40));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
        }

        static string V8Json(
            int wallet,
            IReadOnlyList<string> owned,
            string settledRunId = "")
        {
            int spent = OwnedCost(owned);
            int claimed = wallet + spent;
            long distance = RunRewardCalculator.GetLegacyThresholdForRewardCount(claimed);
            return SaveJson(8, wallet, spent, owned, claimed, distance, settledRunId);
        }

        static string V7Json(
            int wallet,
            IReadOnlyList<string> owned,
            int claimed,
            string settledRunId)
        {
            return SaveJson(
                7,
                wallet,
                owned.Count,
                owned,
                claimed,
                RunRewardCalculator.GetLegacyThresholdForRewardCount(claimed),
                settledRunId);
        }

        static string SaveJson(
            int version,
            int wallet,
            int spent,
            IReadOnlyList<string> owned,
            int claimed,
            long distance,
            string settledRunId)
        {
            string ownedJson = owned.Count == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", owned) + "\"]";
            string settledJson = string.IsNullOrEmpty(settledRunId)
                ? "[]"
                : $"[\"{settledRunId}\"]";
            return
                $"{{\"schemaVersion\":1,\"balanceVersion\":{version}," +
                $"\"wallet\":{wallet},\"spent\":{spent}," +
                "\"tutorialRewardClaimed\":true," +
                "\"rewardMilestoneWatermarkInitialized\":true," +
                "\"rewardedBestHeight\":0," +
                $"\"cumulativeDistanceMeters\":{distance}," +
                $"\"claimedDistanceRewardCount\":{claimed}," +
                $"\"lastSettledRunId\":\"{settledRunId}\"," +
                $"\"settledRunIds\":{settledJson},\"ranks\":[]," +
                $"\"ownedNodeIds\":{ownedJson}," +
                "\"survivalKeystoneId\":\"\",\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
        }

        static int OwnedCost(IEnumerable<string> owned) =>
            owned.Sum(id => PermanentGrowthCatalog.GetNode(id)?.Cost ?? 0);
    }

    public sealed class PermanentGrowthV8ViewTests
    {
        GameObject host;
        PermanentGrowthView view;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            GameLocalization.SetLanguage(GameLanguage.Korean);
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
            host = new GameObject("GrowthV8ViewHost");
            view = host.AddComponent<PermanentGrowthView>();
            view.BuildForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void ViewBuildsFocusedGrowthWithFourFramedPanels()
        {
            Assert.That(view.CreatedCardCount, Is.EqualTo(4));
            Assert.That(view.GridRowCount, Is.EqualTo(1));
            Assert.That(view.TreeViewport, Is.Null);
            Assert.That(view.TreeCanvas, Is.Null);
            Assert.That(host.GetComponentInChildren<ScrollRect>(true), Is.Null);
            Assert.That(view.NodePopupDimmerButton, Is.Null);
            Assert.That(view.IsNodePopupOpen, Is.False);
            Assert.That(FindDescendantByName(view.transform, "TreeCanvas"), Is.Null);
            Assert.That(FindDescendantByName(
                view.transform, "GrowthNodePopupOverlay"), Is.Null);
            Assert.That(view.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name.StartsWith(
                    "GrowthNode_", StringComparison.Ordinal)), Is.False);

            Transform screen = view.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(screen, Is.Not.Null);
            Transform focus = screen.Find("FocusedGrowth");
            Assert.That(focus, Is.Not.Null);
            Image wash = focus.Find("FocusInkWash")?.GetComponent<Image>();
            Image focusIcon = focus.Find("FocusIcon")?.GetComponent<Image>();
            Text focusTitle = focus.Find("FocusTitle")?.GetComponent<Text>();
            Text focusSummary = focus.Find("FocusSummary")?.GetComponent<Text>();
            Assert.That(wash, Is.Not.Null);
            Assert.That(wash.sprite?.name, Is.EqualTo("MukJump_InkBlobMask"));
            Assert.That(wash.color.a, Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(focusIcon, Is.Not.Null);
            Assert.That(focusIcon.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(310f, 310f)));
            Assert.That(focusIcon.preserveAspect, Is.True);
            Assert.That(focusTitle.text,
                Is.EqualTo(PermanentGrowthCatalog.Choices[0].DisplayName));
            Assert.That(focusTitle.fontSize, Is.EqualTo(80));
            Assert.That(focusSummary.text,
                Is.EqualTo(PermanentGrowthCatalog.Choices[0].Summary));
            Assert.That(focusSummary.fontSize, Is.EqualTo(56));
            Assert.That(focusSummary.text, Does.Not.Match("[0-9%+→]"));
            Assert.That(focus.GetComponentsInChildren<Image>(true).Count(
                    image => image.name.StartsWith(
                        "FocusGrowthMark", StringComparison.Ordinal)),
                Is.EqualTo(8));

            Vector2[] positions =
            {
                new(-390f, -405f), new(-130f, -405f),
                new(130f, -405f), new(390f, -405f),
            };
            string[] compactEffects =
            {
                "체력\n+1칸", "최대 먹물\n+12.5%", "먹물 소모\n-3%", "점프\n+1.25%",
            };
            for (int i = 0; i < 4; i++)
            {
                RectTransform card = screen.Find($"ChoiceGrid/GrowthCard{i}")
                    as RectTransform;
                Assert.That(card, Is.Not.Null);
                Assert.That(card.anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(240f, 386f)));
                Assert.That(card.GetComponent<InkActionButtonVisual>(), Is.Null,
                    "선택 탭은 행동 버튼 스킨을 쓰지 않습니다.");
                Image hitArea = card.GetComponent<Image>();
                Assert.That(hitArea.sprite, Is.Null);
                Assert.That(hitArea.color.a, Is.Zero.Within(0.001f));
                Assert.That(card.Find("Paper").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(card.Find("BrushFrame").GetComponent<GrowthRingFrameGraphic>(), Is.Not.Null);
                Assert.That(hitArea.raycastTarget, Is.True);
                Assert.That(card.GetComponent<Button>().targetGraphic,
                    Is.SameAs(card.Find("Icon").GetComponent<Image>()));
                Assert.That(card.Find("Icon").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(card.Find("Surface"), Is.Null);
                RectTransform iconRect = card.Find("Icon")
                    .GetComponent<RectTransform>();
                Assert.That(iconRect.sizeDelta,
                    Is.EqualTo(new Vector2(108f, 108f)));
                Assert.That(iconRect.anchoredPosition,
                    Is.EqualTo(new Vector2(0f, 108f)));
                Text label = card.Find("TabLabel").GetComponent<Text>();
                Text effect = card.Find("TabEffectSummary")
                    .GetComponent<Text>();
                Image selectionInk = card.Find("SelectionInk")
                    .GetComponent<Image>();
                Assert.That(label.fontSize, Is.EqualTo(56));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(effect.text, Is.EqualTo(compactEffects[i]));
                Assert.That(effect.fontSize, Is.EqualTo(56));
                Assert.That(effect.color, Is.EqualTo(InkPalette.TextDark));
                Assert.That(effect.resizeTextForBestFit, Is.False);
                Assert.That(effect.preferredWidth,
                    Is.LessThanOrEqualTo(effect.rectTransform.rect.width + 0.01f));
                Assert.That(effect.preferredHeight,
                    Is.LessThanOrEqualTo(effect.rectTransform.rect.height + 0.01f));
                Assert.That(selectionInk.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(120f, 6f)));
                Assert.That(selectionInk.gameObject.activeSelf,
                    Is.EqualTo(i == 0));
            }
        }

        [Test]
        public void CurrencyBadgeEmphasizesOwnedInkWithAVisibleInkDrop()
        {
            Transform screen = view.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Transform badge = screen.Find("HeaderGroup/BalanceHud");
            Image plate = badge?.GetComponent<Image>();
            Image drop = badge?.Find("InkDropIcon")?.GetComponent<Image>();
            Text amount = badge?.Find("Balance")?.GetComponent<Text>();

            Assert.That(badge, Is.Not.Null);
            Assert.That(plate, Is.Not.Null);
            Assert.That(plate.color.a, Is.Zero, "잔액 입력 영역은 투명하게 유지합니다.");
            Assert.That(plate.raycastTarget, Is.True);
            Assert.That(badge.GetComponent<Button>(), Is.SameAs(view.BalanceInfoButton));
            Assert.That(badge.GetComponent<InkActionButtonVisual>(), Is.Null);

            Assert.That(drop, Is.Not.Null);
            Assert.That(drop.sprite, Is.Not.Null);
            Sprite expectedDrop = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_inklight_sumukhwa_v1");
            Assert.That(expectedDrop, Is.Not.Null);
            Assert.That(drop.sprite, Is.EqualTo(expectedDrop));
            Assert.That(drop.sprite.name,
                Does.StartWith("pg_inklight_sumukhwa_v1"));
            Assert.That(drop.sprite.name,
                Is.Not.EqualTo("MukJump_InkDropMask"));
            Assert.That(drop.preserveAspect, Is.True);
            Assert.That(drop.raycastTarget, Is.True, "보유 먹빛 아이콘도 안내 버튼의 입력 영역입니다.");
            Assert.That(drop.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(84f, 84f)));
            Assert.That(Approximately(drop.color, Color.white), Is.True);

            Assert.That(badge.Find("BalanceCaption"), Is.Null,
                "짧은 상단 잔액에는 반복 설명을 붙이지 않습니다.");
            Assert.That(amount.text, Is.EqualTo("0"));
            Assert.That(amount.fontSize, Is.EqualTo(68));
            Assert.That(amount.resizeTextForBestFit, Is.False);
            Assert.That(amount.raycastTarget, Is.False);
            Assert.That(Approximately(amount.color, InkPalette.Red), Is.True);
            Assert.That(view.BalanceLabel, Is.EqualTo("0"));

            amount.text = "999";
            Assert.That(amount.preferredWidth,
                Is.LessThanOrEqualTo(amount.rectTransform.rect.width + 0.01f),
                "세 자리 보유 먹빛도 축소 없이 배지 안에 보여야 합니다.");
            Assert.That(amount.preferredHeight,
                Is.LessThanOrEqualTo(amount.rectTransform.rect.height + 0.01f));
        }

        [Test]
        public void ViewReusesLobbyWorldBackgroundWithoutOpaqueOverlay()
        {
            RectTransform screenRoot = view.ScreenRoot;
            Image blocker = screenRoot.Find("LobbyBackgroundInputBlocker")
                ?.GetComponent<Image>();

            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.color.a, Is.Zero.Within(0.001f));
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(screenRoot.Find("OpaqueHanjiBackground"), Is.Null);
            Assert.That(screenRoot.Find("LeftInkWash"), Is.Null);
            Assert.That(screenRoot.Find("RightInkWash"), Is.Null);
        }

        [Test]
        public void SelectionTabsAndActionsUseConsistentQuietHanjiButtons()
        {
            view.SelectGrowthForTests(2);
            Transform screen = view.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            int selectedUnderlines = 0;
            int redLabels = 0;
            for (int i = 0; i < 4; i++)
            {
                Transform tab = screen.Find($"ChoiceGrid/GrowthCard{i}");
                Image underline = tab.Find("SelectionInk").GetComponent<Image>();
                Text label = tab.Find("TabLabel").GetComponent<Text>();
                if (underline.gameObject.activeSelf)
                    selectedUnderlines++;
                if (Approximately(label.color, InkPalette.Red))
                    redLabels++;
            }
            Assert.That(selectedUnderlines, Is.EqualTo(1));
            Assert.That(redLabels, Is.EqualTo(1));
            Assert.That(view.SelectedGrowthType,
                Is.EqualTo(PermanentGrowthType.InkBudgetEfficiency));
            Assert.That(screen.Find("FocusedGrowth/FocusTitle")
                .GetComponent<Text>().text, Is.EqualTo("알뜰한 붓"));

            AssertNavigationIconButton(view.BackButton);
            AssertFocusedInkButton(view.PurchaseButton);
            AssertNavigationIconButton(view.NodeResetButton);
            Assert.That(view.NodeResetButton.GetComponent<RectTransform>()
                .sizeDelta, Is.EqualTo(new Vector2(200f, 120f)));
            Assert.That(screen.GetComponentsInChildren<Image>(false).Count(
                    image => image.sprite == InkUiStyle.ActionButtonSprite),
                Is.Zero,
                "로비·초기화는 아이콘만, 구매는 시작 버튼과 같은 먹 붓을 씁니다.");
            Assert.That(screen.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == "RoleWash"), Is.Zero);
        }

        static void AssertNavigationIconButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(120f));
            Assert.That(button.GetComponent<InkActionButtonVisual>(), Is.Null);
            Image hitArea = button.GetComponent<Image>();
            Image icon = button.transform.Find("Icon").GetComponent<Image>();
            Assert.That(hitArea.sprite, Is.Null);
            Assert.That(hitArea.color.a, Is.Zero);
            Assert.That(hitArea.raycastTarget, Is.True);
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(84f, 84f)));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(button.targetGraphic, Is.SameAs(icon));
            Assert.That(button.transform.Find("PrimaryAccent"), Is.Null);
            Assert.That(button.GetComponentsInChildren<Text>(true), Is.Empty);
        }

        static void AssertFocusedInkButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            RectTransform rect = button.GetComponent<RectTransform>();
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(120f));
            Assert.That(button.GetComponent<InkActionButtonVisual>(), Is.Null);
            Assert.That(button.GetComponent<Image>(), Is.Null);
            RawImage brush = button.transform.Find("BrushBackground").GetComponent<RawImage>();
            Assert.That(brush.texture, Is.Not.Null);
            Assert.That(brush.color, Is.EqualTo(Color.white));
            Assert.That(button.targetGraphic, Is.SameAs(brush));
            Assert.That(button.transform.Find("RoleWash"), Is.Null);
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.fontSize, Is.EqualTo(80));
            Assert.That(label.rectTransform.anchoredPosition.x, Is.Zero);
            Assert.That(label.color, Is.EqualTo(InkPalette.TextLight));
            Assert.That(label.resizeTextForBestFit, Is.False);
            Assert.That(label.preferredWidth,
                Is.LessThanOrEqualTo(
                    label.rectTransform.rect.width + 0.01f),
                $"{label.text} 문구가 80px에서 버튼 밖으로 나가면 안 됩니다.");
            Assert.That(label.preferredHeight,
                Is.LessThanOrEqualTo(
                    label.rectTransform.rect.height + 0.01f));
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.001f &&
            Mathf.Abs(a.g - b.g) < 0.001f &&
            Mathf.Abs(a.b - b.b) < 0.001f &&
            Mathf.Abs(a.a - b.a) < 0.001f;

        static Transform FindDescendantByName(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
    }
}
