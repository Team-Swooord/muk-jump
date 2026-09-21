using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkSkinChartTests
    {
        [SetUp] public void Before() => InkSkinChart.Reset();
        [TearDown] public void After() => InkSkinChart.Reset();
        static bool Parse(JArray rows, out InkSkinChart.Snapshot snapshot) =>
            InkSkinChart.TryParse(rows.ToString(Formatting.None), out snapshot, out _);
        [Test] public void GatUsesOnlyTheRequestedShortLoreIncludingLegacyChartData()
        {
            Assert.That(InkSkinCatalog.BuiltIn(InkSkinId.Gat).Korean, Is.EqualTo("바람 갓"));
            Assert.That(InkSkinLore.Get(InkSkinId.Gat).ko, Is.EqualTo("바람처럼 스쳐가는...."));
            var rows = InkSkinChartExport.Rows();
            var gat = rows.Single(x => (string)x["skinKey"] == "Gat");
            Assert.That((string)gat["loreKo"], Is.EqualTo("바람처럼 스쳐가는...."));
            gat["loreKo"] = "쓰고 나면 어째서인지 바람처럼 스쳐 가는 노래가 들린다.";
            gat["loreEn"] = "Put it on, and an old street anthem drifts past on the wind.";
            gat["loreJa"] = "かぶると、なぜか風に乗って昔の街の歌が聞こえる。";
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinLore.Get(InkSkinId.Gat), Is.EqualTo(InkSkinLore.BuiltIn(InkSkinId.Gat)));
            Assert.That(InkSkinTranslations.TryTranslate("바람처럼 스쳐가는....", false, out var en), Is.True);
            Assert.That(en, Is.EqualTo("Passing by like the wind...."));
            Assert.That(InkSkinTranslations.TryTranslate("바람처럼 스쳐가는....", true, out var ja), Is.True);
            Assert.That(ja, Is.EqualTo("風のように通り過ぎる...."));
        }
        [Test] public void ExportsEveryAvailableSkinWithoutReusingRetiredId()
        {
            var rows = InkSkinChartExport.Rows();
            Assert.That(rows.Count, Is.EqualTo(63));
            Assert.That(rows.Any(x => (string)x["skinId"] == "20"), Is.False);
            Assert.That(Parse(rows, out _), Is.True);
            foreach (var r in rows.OfType<JObject>()) foreach (var p in r.Properties())
            { Assert.That(p.Name, Does.Match("^[A-Za-z][A-Za-z0-9]*$")); Assert.That(p.Value.ToString(), Does.Not.Contain("\\")); }
            foreach (var r in rows.OfType<JObject>()) foreach (var p in r.Properties().Where(x => x.Name.EndsWith("En") || x.Name.EndsWith("Ja")))
                Assert.That(p.Value.ToString(), Does.Not.Match("[가-힣]"), r["skinKey"] + ":" + p.Name);
        }
        [Test] public void AppliesNamesLoreGoalTranslationsAndLiveCooldownWithoutChangingUnlockMask()
        {
            var state = new SkinAchievementState(); state.Grant(InkSkinId.Gat);
            long mask = state.unlockedMask;
            var rows = InkSkinChartExport.Rows();
            rows[1]["nameKo"] = "시험 바람 갓"; rows[1]["nameEn"] = "Test Wind Gat"; rows[1]["nameJa"] = "テスト風の笠";
            rows[1]["loreKo"] = "새 이야기"; rows[1]["loreEn"] = "New story"; rows[1]["loreJa"] = "新しい物語";
            rows[1]["unlockGoal"] = 999;
            var fan = rows.Single(x => (string)x["skinKey"] == "WindFan"); fan["cooldownSeconds"] = 12;
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinCatalog.Name(InkSkinId.Gat), Is.EqualTo("시험 바람 갓"));
            Assert.That(InkSkinLore.Get(InkSkinId.Gat).en, Is.EqualTo("New story"));
            Assert.That(InkSkinAchievements.Goal(InkSkinId.Gat), Is.EqualTo(999));
            Assert.That(InkSkinTranslations.TryTranslate("시험 바람 갓", true, out string ja), Is.True);
            Assert.That(ja, Is.EqualTo("テスト風の笠"));
            string rule = InkSkinAchievements.Requirement(InkSkinId.Gat);
            Assert.That(InkSkinTranslations.TryTranslate(rule, false, out string en), Is.True);
            Assert.That(en, Does.Contain("999"));
            Assert.That(InkSkinCatalog.Description(InkSkinId.WindFan), Does.Contain("12초"));
            var go = new GameObject("ChartCooldownTest");
            try {
                var controller = go.AddComponent<RunGrowthController>();
                typeof(RunGrowthController).GetField("skinSnapshot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, InkSkinId.WindFan);
                Assert.That(controller.EquippedAbilityCooldownDuration, Is.EqualTo(12));
            } finally { Object.DestroyImmediate(go); }
            Assert.That(state.unlockedMask, Is.EqualTo(mask));
            Assert.That(state.Progress(InkSkinId.Gat, 0, 0), Is.EqualTo(999));
        }
        [TestCase("skinId", "20")]
        [TestCase("skinKey", "CatEars")]
        [TestCase("unlockMetric", "Secret")]
        [TestCase("unlockGoal", "-1")]
        [TestCase("unlockGoal", "1.2")]
        [TestCase("schemaVersion", "2")]
        [TestCase("nameJa", "")]
        [TestCase("nameKo", "<b>bad</b>")]
        [TestCase("nameEn", "back\\slash")]
        [TestCase("cooldownSeconds", "NaN")]
        [TestCase("cooldownSeconds", "5")]
        public void RejectsInvalidRowsAtomically(string field, string value)
        {
            var rows = InkSkinChartExport.Rows(); rows[1][field] = value;
            Assert.That(Parse(rows, out var snapshot), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(InkSkinCatalog.Name(InkSkinId.Gat), Is.EqualTo(InkSkinCatalog.BuiltIn(InkSkinId.Gat).Korean));
        }
        [Test] public void RejectsDuplicateMissingAndHiddenRuleChanges()
        {
            var rows = InkSkinChartExport.Rows(); rows[2] = rows[1].DeepClone(); Assert.That(Parse(rows, out _), Is.False);
            rows = InkSkinChartExport.Rows(); rows.RemoveAt(0); Assert.That(Parse(rows, out _), Is.False);
            rows = InkSkinChartExport.Rows(); rows.Single(x => (string)x["skinKey"] == "NightBat")["unlockGoal"] = 2;
            Assert.That(Parse(rows, out _), Is.False);
        }
        [Test] public void BadDownloadLeavesPreviousAcceptedCatalogIntact()
        {
            var rows = InkSkinChartExport.Rows(); rows[1]["unlockGoal"] = 250;
            Assert.That(Parse(rows, out var first), Is.True); InkSkinChart.Apply(first);
            Assert.That(InkSkinChart.TryParse("{broken", out _, out _), Is.False);
            Assert.That(InkSkinCatalog.Get(InkSkinId.Gat).Goal, Is.EqualTo(250));
        }
        [Test] public void TimedAbilitiesRequireWholeSecondsAndMatchingDescriptionTokens()
        {
            var rows = InkSkinChartExport.Rows();
            var spider = rows.Single(x => (string)x["skinKey"] == "SpiderMask");
            spider["cooldownSeconds"] = "12.5";
            Assert.That(Parse(rows, out _), Is.False);
            spider["cooldownSeconds"] = "12";
            spider["abilityDescriptionEn"] = "Every 30 seconds.";
            Assert.That(Parse(rows, out _), Is.False);
        }
        [TestCase(false, GameState.Lobby, true)]
        [TestCase(false, GameState.Playing, false)]
        [TestCase(false, GameState.GameOver, false)]
        [TestCase(true, GameState.Lobby, false)]
        public void NeverChangesRulesDuringRunOrSettlement(bool started, GameState state, bool expected) =>
            Assert.That(InkSkinChartRuntime.CanApplyDownloaded(started, state), Is.EqualTo(expected));
    }
}
