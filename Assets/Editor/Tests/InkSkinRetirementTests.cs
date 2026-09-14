using System.Linq;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    [Category("SkinRetirement")]
    public sealed class InkSkinRetirementTests
    {
        MemoryPermanentGrowthStore growth;
        MemoryLobbySettingsStore settings;
        [SetUp] public void Setup()
        {
            growth = new MemoryPermanentGrowthStore(); settings = new MemoryLobbySettingsStore();
            PermanentGrowthProfile.UseStoreForTests(growth);
            InkSkinPreferences.UseStoreForTests(settings);
        }
        [TearDown] public void Teardown()
        {
            SkinDebugSession.Reset();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests();
        }
        [Test] public void RetiredIdStaysStableButCannotBeBrowsedOrEquipped()
        {
            Assert.That((int)InkSkinId.StormScarf, Is.EqualTo(20));
            Assert.That((int)InkSkinId.OrigamiWings, Is.EqualTo(35));
            var ids = Enumerable.Range(0, InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt).ToArray();
            Assert.That(ids.Length, Is.EqualTo(InkSkinCatalog.Count - 1));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Has.No.Member(InkSkinId.StormScarf));
            SkinDebugSession.SetAllUnlocked(true);
            Assert.That(SkinDebugSession.CanEquip(InkSkinId.StormScarf), Is.False);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.StormScarf), Is.False);
            Assert.That(InkAccessoryMotion.Supports(InkSkinId.StormScarf), Is.False);
            Assert.That(InkSkinCatalog.Sprite(InkSkinId.StormScarf), Is.SameAs(InkSkinCatalog.Sprite(InkSkinId.OrigamiWings)));
        }
        [TestCase(InkSkinId.StormScarf), TestCase(InkSkinId.OrigamiWings)]
        public void OldOwnershipSurvivesWithoutRewritingAccountOrEquippedPreference(InkSkinId owned)
        {
            var state = new SkinAchievementState(); state.Grant(owned);
            state = JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(state));
            Assert.That(state.IsValid, Is.True);
            Assert.That(state.Progress(InkSkinId.OrigamiWings, 0, 0), Is.EqualTo(120));
            PermanentGrowthProfile.SettleRun("seed", 1, 1, 0, 1, true, SkinRunStats.Begin(false, InkSkinId.Ink));
            var json = JObject.Parse(growth.Json);
            json["skinAchievements"] = JObject.FromObject(state);
            growth.Json = json.ToString(); PermanentGrowthProfile.UseStoreForTests(growth);
            settings.SetInt(InkSkinPreferences.StorageKey, (int)owned);
            string before = growth.Json;
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.OrigamiWings));
            Assert.That(settings.GetInt(InkSkinPreferences.StorageKey, 0), Is.EqualTo((int)owned));
            Assert.That(growth.Json, Is.EqualTo(before));
            var reports = InkAchievementCatalog.SavedProgress(state, 0);
            Assert.That(reports.Any(x => x.id == InkAchievementCatalog.Id(20)), Is.False);
            Assert.That(reports.Single(x => x.id == InkAchievementCatalog.Id(35)).percent, Is.EqualTo(100));
        }
        [TestCase(119.99f, false), TestCase(120f, true)]
        public void LiveAndFinalSettlementUseSameTransferredSurvivalThreshold(float seconds, bool unlocked)
        {
            var state = new SkinAchievementState(); var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
            var live = LiveInkAchievements.Evaluate(state, stats, 1, seconds);
            Assert.That((live & (1UL << 35)) != 0, Is.EqualTo(unlocked));
            Assert.That((live & (1UL << 20)), Is.Zero);
            state.Settle(1, seconds, stats, 0, 0);
            Assert.That(state.Has(InkSkinId.OrigamiWings), Is.EqualTo(unlocked));
            Assert.That(state.Has(InkSkinId.StormScarf), Is.False);
        }
        [Test] public void NewPaperWingsCopyAndRequirementHaveAllThreeLanguages()
        {
            Assert.That(InkSkinCatalog.Name(InkSkinId.OrigamiWings), Is.EqualTo("종이 날개"));
            Assert.That(InkSkinCatalog.Get(InkSkinId.OrigamiWings).Metric, Is.EqualTo(SkinMetric.Survival));
            string requirement = InkSkinAchievements.Requirement(InkSkinId.OrigamiWings);
            Assert.That(requirement, Does.Contain("120"));
            foreach (bool japanese in new[] { false, true })
            {
                Assert.That(InkSkinTranslations.TryTranslate(requirement, japanese, out var text), Is.True);
                Assert.That(text, Does.Contain("120"));
                Assert.That(InkSkinTranslations.TryTranslate("종이 날개", japanese, out text), Is.True);
                Assert.That(text, Is.EqualTo(japanese ? "紙の翼" : "Paper Wings"));
            }
        }
    }
}
