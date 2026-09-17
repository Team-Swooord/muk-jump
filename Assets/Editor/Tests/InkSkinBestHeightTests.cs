using System.Linq;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkSkinBestHeightTests
    {
        MemoryPermanentGrowthStore store;
        GameObject wardrobeObject;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings);
            InkSkinPreferences.UseStoreForTests(settings);
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 9999 });
            SkinDebugSession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            store.ThrowOnPrimarySave = false;
            if (wardrobeObject != null) Object.DestroyImmediate(wardrobeObject);
            SkinDebugSession.Reset();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
        }

        static PermanentGrowthSettlement Finish(string run, InkSkinId skin, int height, bool eligible = true) =>
            PermanentGrowthProfile.SettleRun(run, height, height, 0, 30, eligible, SkinRunStats.Begin(false, skin));

        [Test]
        public void BestBelongsToStartingCostumeAndNeverDecreasesOrCopiesOverallBest()
        {
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(-1));
            Assert.That(Finish("first", InkSkinId.Ink, 237).Accepted, Is.True);
            Assert.That(Finish("hat", InkSkinId.Gat, 156).Accepted, Is.True);
            Assert.That(Finish("lower", InkSkinId.Ink, 40).Accepted, Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(237));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(156));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.WindFan), Is.EqualTo(-1));
            Assert.That(Finish("higher", InkSkinId.Gat, 300).Accepted, Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(300));
            Assert.That(PermanentGrowthProfile.SkinBestHeight((InkSkinId)999), Is.EqualTo(-1));
        }

        [Test]
        public void ZeroMeterRunIsDifferentFromNoRecordAndOldUnmeasuredRunsStayUnknown()
        {
            Assert.That(Finish("zero", InkSkinId.Ink, 0).Accepted, Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.Zero);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
            PermanentGrowthProfile.SettleRun("old", 999, 0, true);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.Zero);
        }

        [Test]
        public void PreviewDuplicateAndFailedSaveCannotCreateOrInflateARecord()
        {
            Assert.That(Finish("saved", InkSkinId.Ink, 100).Accepted, Is.True);
            Finish("saved", InkSkinId.Ink, 999);
            Finish("preview", InkSkinId.Gat, 777, false);
            string before = store.Json;
            store.ThrowOnPrimarySave = true;
            Assert.That(Finish("retry", InkSkinId.Ink, 200).Accepted, Is.False);
            Assert.That(store.Json, Is.EqualTo(before));
            store.ThrowOnPrimarySave = false;
            PermanentGrowthProfile.UseStoreForTests(store);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(100));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
            Assert.That(Finish("retry", InkSkinId.Ink, 200).Accepted, Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(200));
        }

        [Test]
        public void RecordsSurviveReloadAndCloudRoundTripButNotAccountReplacementOrDeletion()
        {
            Finish("account-a", InkSkinId.Gat, 237);
            PermanentGrowthProfile.UseStoreForTests(store);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(237));
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string cloudA), Is.True);
            var accountB = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(accountB);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
            Finish("account-b", InkSkinId.Ink, 80);
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string cloudB), Is.True);
            var wire = Newtonsoft.Json.JsonConvert.SerializeObject(new { growthJson = cloudA });
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(JObject.Parse(wire)["growthJson"].Value<string>()), Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(237));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(-1));
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(cloudB), Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(80));
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(-1));
        }

        [Test]
        public void LegacyPayloadLoadsWithoutInventingCostumeRecords()
        {
            Finish("legacy", InkSkinId.Gat, 237);
            var json = JObject.Parse(store.Json);
            ((JObject)json["skinAchievements"]).Remove("skinBestHeights");
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(json.ToString()), Is.True);
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().best, Is.EqualTo(237));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
            Finish("new", InkSkinId.Ink, 5);
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(5));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Gat), Is.EqualTo(-1));
        }

        [TestCase("null")]
        [TestCase("\"237\"")]
        [TestCase("[-2]")]
        [TestCase("[9999]")]
        [TestCase("[1.5]")]
        public void CorruptOptionalRecordsAreRejectedWithoutChangingSavedProgress(string invalid)
        {
            Finish("valid", InkSkinId.Ink, 237);
            string before = store.Json;
            var json = JObject.Parse(before);
            json["skinAchievements"]["skinBestHeights"] = JToken.Parse(invalid);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(json.ToString()), Is.False);
            Assert.That(store.Json, Is.EqualTo(before));
            Assert.That(PermanentGrowthProfile.SkinBestHeight(InkSkinId.Ink), Is.EqualTo(237));
        }

        [TestCase(GameLanguage.Korean, "최고 237m", "기록 없음")]
        [TestCase(GameLanguage.English, "Best 237m", "No record")]
        [TestCase(GameLanguage.Japanese, "最高 237m", "記録なし")]
        public void BoldInkStaysInsideDetailsAndPersonalRecordsKeepNormalTypography(GameLanguage language, string record, string empty)
        {
            Finish("card", InkSkinId.Ink, 237);
            GameLocalization.SetLanguage(language);
            wardrobeObject = new GameObject("CostumeRecordView");
            var view = wardrobeObject.AddComponent<InkWardrobeView>();
            var sprite = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First();
            view.BuildForTests(sprite);
            view.SetNavigationPresentation(true, true);
            view.Select(InkSkinId.WindFan);
            Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.True);
            var detailLabels = view.Paper.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (string name in new[] { "SkinName", "ItemLore", "Category", "SkillTitle", "Cooldown", "SkillDescription" })
            {
                var label = detailLabels.Single(x => x.name == name);
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Bold), name);
                Assert.That(label.color, Is.EqualTo(name == "Category" ? InkPalette.Red : InkPalette.Ink), name);
                Assert.That(label.GetComponent<UnityEngine.UI.Outline>().enabled, Is.False, name);
            }
            foreach (bool expanded in new[] { false, true })
            {
                if (view.CollectionExpanded != expanded) view.ToggleCollection();
                var labels = view.CollectionContent.GetComponentsInChildren<UnityEngine.UI.Text>(true).Where(x => x.name == "BestHeight").ToArray();
                var ink = labels.Single(x => x.transform.parent.name == "SkinCard0");
                var gat = labels.Single(x => x.transform.parent.name == "SkinCard" + (int)InkSkinId.Gat);
                Assert.That(ink.text, Is.EqualTo(record));
                Assert.That(gat.text, Is.EqualTo(empty));
                Assert.That(labels.All(x => x.fontStyle == FontStyle.Normal), Is.True);
                Assert.That(labels.All(x => x.color == InkPalette.TextMuted), Is.True);
                Assert.That(ink.fontSize, Is.EqualTo(language == GameLanguage.Japanese ? 20 : 26));
                Assert.That(view.CollectionContent.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(x => x.name == "UnlockMethod"), Is.False);
            }
        }
    }
}
