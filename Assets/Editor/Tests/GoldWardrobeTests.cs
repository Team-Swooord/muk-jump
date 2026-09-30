using System;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class GoldWardrobeTests
    {
        MemoryPermanentGrowthStore growth;
        MemoryLobbySettingsStore settings;
        InkPremiumUiTests.Store premium;
        [SetUp] public void Setup()
        {
            growth = new MemoryPermanentGrowthStore(); settings = new MemoryLobbySettingsStore();
            PermanentGrowthProfile.UseStoreForTests(growth); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings); ScoreManager.UseStoreForTests(new MemoryScoreStore());
            premium = new InkPremiumUiTests.Store(); InkPremiumAppearance.UseForTests(true, premium);
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            SkinDebugSession.Reset();
        }
        [TearDown] public void Cleanup()
        {
            SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests(); PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests();
        }
        static PermanentGrowthSettlement Finish(string run, int height, bool gold = true, bool eligible = true) =>
            PermanentGrowthProfile.SettleRun(run, height, height, 0, 30, eligible, SkinRunStats.Begin(false, InkSkinId.Ink, gold));

        [Test] public void DebugGoldNeverGrantsPurchasesAdsOrSavedSelection()
        {
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.False);
            string before = JsonUtility.ToJson(PermanentGrowthProfile.AchievementSyncSnapshot()), saved = growth.Json;
            int writes = settings.SaveCount;
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
            Assert.That(InkPremiumAppearance.IsEquipped && SkinDebugSession.IsGoldEquipped, Is.True);
            Assert.That(InkPremiumAppearance.IsOwned || InkPremiumAppearance.SuppressAds || InkSkinPreferences.GoldSelected, Is.False);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.Ink));
            SkinDebugSession.SetMenuOpen(true); SkinDebugSession.SetMenuOpen(false);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True, "메뉴를 닫아도 시험 착용은 유지");
            Assert.That(settings.SaveCount, Is.EqualTo(writes));
            Assert.That(growth.Json, Is.EqualTo(saved));
            Assert.That(JsonUtility.ToJson(PermanentGrowthProfile.AchievementSyncSnapshot()), Is.EqualTo(before));
            Assert.That(premium.Purchases + premium.Restores, Is.Zero);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.NightBat), Is.True);
            Assert.That(InkPremiumAppearance.IsEquipped || SkinDebugSession.IsGoldEquipped, Is.False);
            InkSkinPreferences.TryEquipGold(); SkinDebugSession.SetAllUnlocked(false);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
        }
        [Test] public void DebugGoldClearsOnOwnerReplacementRecoveryAndReset()
        {
            SkinDebugSession.SetAllUnlocked(true); InkSkinPreferences.TryEquipGold();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Assert.That(SkinDebugSession.IsGoldEquipped || InkPremiumAppearance.IsEquipped, Is.False);
            SkinDebugSession.SetAllUnlocked(true); InkSkinPreferences.TryEquipGold(); SkinDebugSession.Reset();
            Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
            SkinDebugSession.SetAllUnlocked(true); InkSkinPreferences.TryEquipGold();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore { Json = "broken" });
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(SkinDebugSession.IsGoldEquipped || InkPremiumAppearance.IsEquipped, Is.False);
        }
        [Test] public void UnsupportedPlatformCannotBorrowGoldEvenInDebug()
        {
            SkinDebugSession.SetAllUnlocked(true); InkPremiumAppearance.UseForTests(false, premium);
            Assert.That(InkSkinPreferences.TryEquipGold() || InkPremiumAppearance.CanEquipGold, Is.False);
        }
        [Test] public void GoldBestIsSeparateMonotonicAndZeroIsARecord()
        {
            Finish("ink", 237, false);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(-1));
            Finish("gold-zero", 0); Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.Zero);
            Finish("gold", 320); Finish("lower", 4);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(320));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(237));
            Assert.That(InkSkinCatalog.Count, Is.EqualTo(63));
        }
        [Test] public void DebugDuplicateAndSaveFailureNeverCreateGoldRecords()
        {
            Finish("debug", 9999, true, false);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(-1));
            Finish("gold", 100); Finish("gold", 9999);
            string before = growth.Json; growth.ThrowOnPrimarySave = true;
            Assert.That(Finish("retry", 200).Accepted, Is.False);
            Assert.That(growth.Json, Is.EqualTo(before)); growth.ThrowOnPrimarySave = false;
            PermanentGrowthProfile.UseStoreForTests(growth);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(100));
            Assert.That(Finish("retry", 200).Accepted, Is.True);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(200));
        }
        [Test] public void GoldCloudRoundTripLegacyAndAccountReplacementPreserveBoundaries()
        {
            Finish("ink", 237, false); Finish("gold", 320);
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string cloud), Is.True);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(-1));
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(cloud), Is.True);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(320));
            var old = JObject.Parse(cloud); var stats = (JObject)old["skinAchievements"];
            stats.Remove("hasGoldBestHeight"); stats.Remove("goldBestHeight");
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(old.ToString()), Is.True);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(-1));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(237));
        }
        [TestCase("hasGoldBestHeight", "null")]
        [TestCase("hasGoldBestHeight", "\"true\"")]
        [TestCase("hasGoldBestHeight", "1")]
        [TestCase("hasGoldBestHeight", "false")]
        [TestCase("goldBestHeight", "-1")]
        [TestCase("goldBestHeight", "1.5")]
        [TestCase("goldBestHeight", "\"100\"")]
        [TestCase("goldBestHeight", "9999")]
        [TestCase("goldBestHeight", "null")]
        [TestCase("hasGoldBestHeight", "missing")]
        [TestCase("goldBestHeight", "missing")]
        public void CorruptGoldFieldsAreRejectedWithoutChangingData(string field, string value)
        {
            Finish("gold", 100); string before = growth.Json;
            var data = JObject.Parse(before); var stats = (JObject)data["skinAchievements"];
            if (value == "missing") stats.Remove(field); else stats[field] = JToken.Parse(value);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(data.ToString()), Is.False);
            Assert.That(growth.Json, Is.EqualTo(before));
        }
        [Test] public void PendingGoldSettlementRetainsStartingAppearanceAndRecoversOnce()
        {
            var snapshot = new PendingGameOverSettlementSnapshot
            {
                runId = "d123456789abcdef0123456789abcdef", swarmProgressHeight = 35, scoreHeight = 40,
                activeGameplaySeconds = 12, eligible = true, skinStats = SkinRunStats.Begin(false, InkSkinId.Ink, true)
            };
            var pending = new MemoryPendingGameOverSettlementStore { Json = JsonUtility.ToJson(snapshot) };
            GameManager.UsePendingGameOverSettlementStoreForTests(pending);
            Assert.That(SkinRunStats.HasCompletePayload(pending.Json), Is.True);
            Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
            Assert.That(GameManager.TryRecoverPendingGameOverSettlement(), Is.True);
            Assert.That(PermanentGrowthProfile.GoldBestHeight, Is.EqualTo(40));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(-1));
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().runs, Is.EqualTo(1));
            var legacy = JObject.Parse(JsonUtility.ToJson(snapshot)); ((JObject)legacy["skinStats"]).Remove("startingGold");
            Assert.That(SkinRunStats.HasCompletePayload(legacy.ToString()), Is.True);
            legacy["skinStats"]["startingGold"] = "true";
            Assert.That(SkinRunStats.HasCompletePayload(legacy.ToString()), Is.False);
            Assert.That(SkinRunStats.Begin(false, InkSkinId.Gat, true).IsMeasured, Is.False);
        }
        [Test] public void GradientChangesColorWithoutChangingVerticesAlphaOrMaterial()
        {
            var host = new GameObject("GoldName", typeof(RectTransform), typeof(Text));
            try
            {
                var text = host.GetComponent<Text>(); var material = text.material;
                var gradient = host.AddComponent<GoldInkNameGradient>();
                using var mesh = new VertexHelper();
                foreach (var pos in new[] { Vector3.zero, new Vector3(100, 0), new Vector3(100, 30), new Vector3(0, 30) })
                    mesh.AddVert(pos, new Color32(28, 27, 26, 90), Vector2.zero);
                gradient.ModifyMesh(mesh); var a = new UIVertex(); var b = new UIVertex();
                mesh.PopulateUIVertex(ref a, 0); mesh.PopulateUIVertex(ref b, 2);
                Assert.That(a.color.a, Is.EqualTo(90)); Assert.That(b.position, Is.EqualTo(new Vector3(100, 30)));
                Assert.That(a.color, Is.Not.EqualTo(b.color)); Assert.That(a.color.r, Is.GreaterThan(a.color.b * 2));
                Assert.That(text.material, Is.SameAs(material));
                Assert.That(GoldInkNameGradient.TintAt(.2f, .5f, 0), Is.Not.EqualTo(GoldInkNameGradient.TintAt(.2f, .5f, .3f)));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
