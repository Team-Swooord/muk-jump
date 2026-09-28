using System;
using System.Linq;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class NextUpdateScopeTests
    {
        MemoryLobbySettingsStore settings;
        MemoryPermanentGrowthStore growth;
        GameObject host;
        [SetUp] public void Setup()
        {
            NextUpdateScope.UseDeferredPreviewForTests(false);
            settings = new MemoryLobbySettingsStore(); growth = new MemoryPermanentGrowthStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(growth); ScoreManager.UseStoreForTests(new MemoryScoreStore());
            SkinDebugSession.Reset(); InkPremiumAppearance.UseForTests(true, new InkPremiumUiTests.Store { Owned = true });
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        [TearDown] public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            NextUpdateScope.UseDeferredPreviewForTests(false);
            SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        void Earn()
        {
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
            stats.drawnLandings = 200; stats.items = 1000; stats.maxLivingPlayers = 12;
            Assert.That(PermanentGrowthProfile.SettleRun(Guid.NewGuid().ToString("N"), 500, 500, 0, 60, true, stats).Accepted, Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void ReleaseCatalogContainsEveryLegacySkinAndOnlySupportedGold(bool premium)
        {
            Assert.That(NextUpdateScope.DeferredWardrobeEnabled, Is.False);
            Assert.That(NextUpdateScope.GrowthLengthComparisonEnabled, Is.False);
            InkPremiumAppearance.UseForTests(premium, null);
            var entries = Enumerable.Range(0, InkWardrobeEntry.Count).Select(InkWardrobeEntry.At).ToArray();
            Assert.That(entries.Length, Is.EqualTo(InkSkinCatalog.BrowseCount + (premium ? 1 : 0)));
            Assert.That(entries.Any(e => e.Nest), Is.False);
            Assert.That(entries.Count(e => e.Gold), Is.EqualTo(premium ? 1 : 0));
            Assert.That(entries.Select(e => e.CardKey).Distinct().Count(), Is.EqualTo(entries.Length));
            for (int i = 0; i < InkSkinCatalog.BrowseCount; i++)
            {
                var skin = InkSkinCatalog.BrowseAt(i);
                Assert.That(InkWardrobeEntry.At(InkWardrobeEntry.Index(skin)).Skin, Is.EqualTo(skin));
            }
            Assert.That(InkSkinCatalog.Count, Is.EqualTo(63));
        }
        [Test] public void ReachingAllThresholdsDoesNotGrantDeferredRewards()
        {
            Earn();
            foreach (string id in InkCosmeticCatalog.NewItems)
            {
                Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.False);
                Assert.That(InkSkinPreferences.CanUseCosmetic(id), Is.False);
            }
            Assert.That(settings.SaveCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(500));
        }
        [Test] public void DeferredOwnershipAndSelectionsSurviveWithoutRenderingOrEquipping()
        {
            NextUpdateScope.UseDeferredPreviewForTests(true); Earn();
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest), Is.True);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf), Is.True);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole), Is.True);
            string selection = settings.GetString(InkSkinPreferences.CosmeticsKey, "");
            string progress = growth.Json; int saves = settings.SaveCount;
            NextUpdateScope.UseDeferredPreviewForTests(false);
            PermanentGrowthProfile.ResetCacheForTests();
            foreach (string id in InkCosmeticCatalog.NewItems) Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.True);
            foreach (InkCosmeticSlot slot in Enum.GetValues(typeof(InkCosmeticSlot)))
            {
                Assert.That(InkSkinPreferences.EquippedCosmetic(slot), Is.EqualTo(InkCosmeticCatalog.Default(slot)));
                Assert.That(InkCosmeticCatalog.InRun(slot), Is.EqualTo(InkCosmeticCatalog.Default(slot)));
            }
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest), Is.False);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf), Is.False);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole), Is.False);
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            Assert.That(settings.GetString(InkSkinPreferences.CosmeticsKey, ""), Is.EqualTo(selection));
            Assert.That(JToken.DeepEquals(JObject.Parse(progress), JObject.Parse(growth.Json)), Is.True);
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
            Assert.That(InkSkinPreferences.PreferredCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
        }
        [Test] public void OldSchemaProgressMigratesWithoutGivingNewCosmetics()
        {
            Earn(); var old = JObject.Parse(growth.Json);
            old["balanceVersion"] = 13; old.Remove("cosmetics");
            growth.Json = old.ToString(Newtonsoft.Json.Formatting.None); growth.BackupJson = string.Empty;
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(500));
            foreach (string id in InkCosmeticCatalog.NewItems) Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.False);
            Assert.That(JToken.DeepEquals(JObject.Parse(growth.Json)["skinAchievements"], old["skinAchievements"]), Is.True);
        }
        [Test] public void WardrobeOpensLegacyListAndRejectsHiddenEntrances()
        {
            host = new GameObject("NextUpdateWardrobeTest");
            var view = host.AddComponent<InkWardrobeView>();
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png")
                .OfType<Sprite>().First(s => s.name == "idle");
            view.BuildForTests(idle); view.SetNavigationPresentation(true, true);
            Assert.That(view.IsCategoryHome, Is.False);
            Assert.That(view.Paper.Find("CosmeticCategories"), Is.Null);
            Assert.That(view.Paper.Find("EffectChoices"), Is.Null);
            Assert.That(view.Paper.Find("ShowroomStage/CombinedClonePreview"), Is.Null);
            Assert.That(view.Paper.Find("ShowroomStage").gameObject.activeSelf, Is.True);
            Assert.That(view.CollectionScroll.gameObject.activeSelf, Is.True);
            view.SelectSlot(InkCosmeticSlot.Landing); view.SelectNest();
            Assert.That(view.SelectedSlot, Is.EqualTo(InkCosmeticSlot.Skin));
            Assert.That(view.SelectedNest, Is.False);
            view.SelectGold(); view.EquipButton.onClick.Invoke();
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
            view.ToggleCollection(); Assert.That(view.CollectionExpanded, Is.True);
            view.ToggleCollection(); Assert.That(view.CollectionExpanded, Is.False);
        }
    }
}
