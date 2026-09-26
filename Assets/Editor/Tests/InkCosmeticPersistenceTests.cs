using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkCosmeticPersistenceTests
    {
        MemoryPermanentGrowthStore growth;
        MemoryLobbySettingsStore settings;
        [SetUp] public void SetUp()
        {
            growth = new MemoryPermanentGrowthStore(); settings = new MemoryLobbySettingsStore();
            PermanentGrowthProfile.UseStoreForTests(growth);
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            SkinDebugSession.Reset(); InkPremiumAppearance.UseForTests(false, null);
        }
        [TearDown] public void TearDown()
        {
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests();
        }
        void Earn(int landings = 200, int items = 1000, int clones = 12)
        {
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
            stats.drawnLandings = landings; stats.items = items; stats.maxLivingPlayers = clones;
            Assert.That(PermanentGrowthProfile.SettleRun(System.Guid.NewGuid().ToString("N"), 500, 500, 0, 60, true, stats).Accepted, Is.True);
        }
        [Test] public void FreshReadDoesNotSaveOrEquipNewRewards()
        {
            Assert.That(PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.BambooLeaf), Is.False);
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.FollowBody));
            Assert.That(growth.SaveCount, Is.Zero); Assert.That(settings.SaveCount, Is.Zero);
        }
        [Test] public void ExactThresholdsGrantOnceAndNeverAutoEquip()
        {
            Earn(199, 999, 11);
            foreach (string id in InkCosmeticCatalog.NewItems) Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.False);
            Earn(1, 1, 12);
            foreach (string id in InkCosmeticCatalog.NewItems) Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.True);
            Assert.That(settings.SaveCount, Is.Zero);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.Tadpole), Is.True);
            Assert.That(InkSkinCatalog.Count, Is.EqualTo(63));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin), Is.EqualTo(InkCosmeticCatalog.Legacy(InkSkinId.Ink)));
        }
        [Test] public void Version13HistoryMigratesWithoutChangingProgressOrLegacyChoice()
        {
            Earn();
            var prior = JObject.Parse(growth.Json); prior["balanceVersion"] = 13; prior.Remove("cosmetics");
            string original = prior.ToString(Newtonsoft.Json.Formatting.None);
            growth.Json = original; growth.BackupJson = string.Empty;
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.Gat);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.BambooLeaf), Is.True);
            Assert.That(growth.BackupJson, Is.EqualTo(growth.Json));
            Assert.That(JToken.DeepEquals(JObject.Parse(growth.Json)["skinAchievements"], prior["skinAchievements"]), Is.True);
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(prior["cumulativeDistanceMeters"].Value<long>()));
            Assert.That(JObject.Parse(growth.Json)["balanceVersion"].Value<int>(), Is.EqualTo(14));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            Assert.That(settings.SaveCount, Is.Zero);
        }
        [Test] public void IndependentSelectionsPersistAndFailureRollsBackAllChoices()
        {
            Earn();
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf), Is.True);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole), Is.True);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest), Is.True);
            string saved = settings.GetString(InkSkinPreferences.CosmeticsKey, "");
            settings.ThrowOnSave = true;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.False);
            settings.ThrowOnSave = false;
            Assert.That(settings.GetString(InkSkinPreferences.CosmeticsKey, ""), Is.EqualTo(saved));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink), "까치 둥지에 갓 능력이 남아서는 안 됩니다.");
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.True);
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.Tadpole));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
        }
        [Test] public void SameAccountRefreshPreservesOwnershipAndExactSwitchDoesNotGrantIt()
        {
            PermanentGrowthProfile.TryExportCloudJson(out string empty);
            Earn();
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(empty, true), Is.True);
            Assert.That(PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.BambooLeaf), Is.True);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(empty), Is.True);
            Assert.That(PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.BambooLeaf), Is.False);
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.DefaultLanding));
            Assert.That(InkSkinPreferences.PreferredCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf), Is.False);
        }
        [Test] public void UnknownIdentifiersRoundTripAndDoNotGrantPaidGold()
        {
            Earn();
            var data = JObject.Parse(growth.Json);
            ((JArray)data["cosmetics"]["owned"]).Add("skin.future.v1");
            ((JArray)data["cosmetics"]["owned"]).Add(InkCosmeticCatalog.Gold);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(data.ToString()), Is.True);
            var selection = new InkCosmeticSelection { brush = "brush.future.v1" };
            settings.SetString(InkSkinPreferences.CosmeticsKey, JsonUtility.ToJson(selection));
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf), Is.True);
            Assert.That(settings.GetString(InkSkinPreferences.CosmeticsKey, ""), Does.Contain("brush.future.v1"));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Brush), Is.EqualTo(InkCosmeticCatalog.DefaultBrush));
            Earn(1, 1, 1);
            Assert.That(growth.Json, Does.Contain("skin.future.v1"));
            Assert.That(InkSkinPreferences.CanUseCosmetic(InkCosmeticCatalog.Gold), Is.False);
        }
        [TestCase(15, 1)] [TestCase(14, 2)]
        public void FutureOwnershipFormatIsRejectedWithoutChangingCurrentSave(int balance, int version)
        {
            Earn(); string saved = growth.Json;
            var unknown = JObject.Parse(saved); unknown["balanceVersion"] = balance; unknown["cosmetics"]["version"] = version;
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(unknown.ToString()), Is.False);
            Assert.That(growth.Json, Is.EqualTo(saved));
        }
        [Test] public void FutureSelectionIsPreservedWithoutOverwritingAnySlot()
        {
            Earn(); string future = JsonUtility.ToJson(new InkCosmeticSelection { version = 2, landing = "landing.future.v1" });
            settings.SetString(InkSkinPreferences.CosmeticsKey, future);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Ink), Is.False);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole), Is.False);
            Assert.That(settings.GetString(InkSkinPreferences.CosmeticsKey, ""), Is.EqualTo(future));
        }
        [Test] public void FailedSettlementCannotGrantCosmetics()
        {
            Earn(0, 0, 1); string saved = growth.Json;
            growth.ThrowOnPrimarySave = true;
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink); stats.drawnLandings = 200; stats.items = 1000; stats.maxLivingPlayers = 12;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("영구 성장.*"));
            Assert.That(PermanentGrowthProfile.SettleRun("failed-cosmetics", 500, 500, 0, 60, true, stats).Accepted, Is.False);
            growth.ThrowOnPrimarySave = false;
            Assert.That(growth.Json, Is.EqualTo(saved));
            foreach (string id in InkCosmeticCatalog.NewItems) Assert.That(PermanentGrowthProfile.OwnsCosmetic(id), Is.False);
        }
    }
}
