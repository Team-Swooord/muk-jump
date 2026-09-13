using System;
using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkMythicSkinTests
    {
        public static IEnumerable<InkSkinId> Rewards => Enumerable.Range(InkMythicSkins.First, 15).Select(i => (InkSkinId)i);
        static bool Has(ulong mask, InkSkinId id) => (mask & (1UL << (int)id)) != 0;
        static (SkinRunStats stats, int height, float seconds) Case(InkSkinId id)
        {
            var s = SkinRunStats.Begin(false, InkSkinId.Ink); int h = 0; float t = 1;
            switch (id)
            {
                case InkSkinId.ZodiacRat: s.startedAtNight = true; s.items = 3; h = 50; break;
                case InkSkinId.ZodiacOx: s.drawnLandings = 20; t = 60; break;
                case InkSkinId.ZodiacTiger: s.startedAtNight = true; h = 150; break;
                case InkSkinId.ZodiacRabbit: s.startedAtNight = true; s.startingSkin = InkSkinId.MoonCrown; h = 200; break;
                case InkSkinId.ZodiacDragon: s.startingSkin = InkSkinId.DragonTail; h = 300; break;
                case InkSkinId.ZodiacSnake: s.startingSkin = InkSkinId.SpiderMask; s.drawnLandings = 12; h = 120; break;
                case InkSkinId.ZodiacHorse: h = 300; t = 60; break;
                case InkSkinId.ZodiacGoat: s.drawnLandings = 30; h = 200; break;
                case InkSkinId.ZodiacMonkey: s.maxLivingPlayers = 3; s.items = 5; break;
                case InkSkinId.ZodiacRooster: s.items = s.inkDrops = 3; h = 100; break;
                case InkSkinId.ZodiacDog: s.maxLivingPlayers = 2; t = 90; break;
                case InkSkinId.ZodiacPig: s.items = 12; s.inkDrops = 3; break;
                case InkSkinId.HaetaeCrest: s.startingSkin = InkSkinId.TurtleShell; h = 250; break;
                case InkSkinId.ThreeLeggedCrow: s.startedAtNight = true; s.items = s.inkDrops = 3; h = 333; break;
                case InkSkinId.NineTails: s.startingSkin = InkSkinId.FoxMask; s.maxLivingPlayers = 3; h = 300; break;
            }
            return (s, h, t);
        }

        [TestCaseSource(nameof(Rewards))]
        public void EachSecretHasARealThresholdAndCompleteTranslations(InkSkinId id)
        {
            var c = Case(id);
            Assert.That(Has(InkMythicSkins.Evaluate(c.stats, c.height, c.seconds, true), id), Is.True, "조건 충족");
            if (c.height > 0) c.height--;
            else if (id == InkSkinId.ZodiacOx || id == InkSkinId.ZodiacDog) c.seconds--;
            else c.stats.items--;
            Assert.That(Has(InkMythicSkins.Evaluate(c.stats, c.height, c.seconds, true), id), Is.False, "경계 바로 아래");
            Assert.That(InkSkinCatalog.Get(id).Tier, Is.EqualTo(SkinTier.Hidden).Or.EqualTo(SkinTier.UltraHidden));
            foreach (string source in new[] { InkSkinCatalog.Name(id), InkSkinAchievements.Requirement(id), InkSkinLore.Get(id).ko })
                foreach (bool japanese in new[] { false, true })
                {
                    Assert.That(InkSkinTranslations.TryTranslate(source, japanese, out var translated), Is.True, source);
                    Assert.That(translated, Is.Not.Empty.And.Not.EqualTo("???").And.Not.EqualTo(source));
                }
        }

        [TestCaseSource(nameof(Rewards))]
        public void InvalidMeasurementsCannotGrantSecrets(InkSkinId id)
        {
            var c = Case(id);
            Assert.That(InkMythicSkins.Evaluate(c.stats, c.height, float.NaN, true), Is.Zero);
            Assert.That(InkMythicSkins.Evaluate(c.stats, c.height, float.PositiveInfinity, true), Is.Zero);
            Assert.That(InkMythicSkins.Evaluate(c.stats, c.height, -1, true), Is.Zero);
            c.stats.version = 0;
            Assert.That(InkMythicSkins.Evaluate(c.stats, c.height, c.seconds, true), Is.Zero);
        }

        [TestCase(InkSkinId.ZodiacTiger), TestCase(InkSkinId.ZodiacHorse), TestCase(InkSkinId.HaetaeCrest)]
        public void EndOfRunRestrictionsNeverUnlockEarly(InkSkinId id)
        {
            var c = Case(id);
            Assert.That(Has(InkMythicSkins.Evaluate(c.stats, c.height, c.seconds, false), id), Is.False);
            if (id == InkSkinId.ZodiacHorse) c.seconds += .01f; else c.stats.items = 1;
            Assert.That(Has(InkMythicSkins.Evaluate(c.stats, c.height, c.seconds, true), id), Is.False);
        }

        [Test]
        public void HighBitsRoundTripWithoutChangingOldIdsAndStayConcealedUntilSaved()
        {
            Assert.That((int)InkSkinId.DragonTail, Is.EqualTo(45));
            Assert.That(InkSkinCatalog.Count, Is.LessThanOrEqualTo(63));
            var store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            try
            {
                foreach (var id in Rewards) Assert.That(InkSkinCatalog.IsConcealed(id), Is.True);
                foreach (var id in Rewards)
                {
                    var c = Case(id);
                    Assert.That(PermanentGrowthProfile.SettleRun("myth-" + id, c.height, c.height, 0, c.seconds, true, c.stats).Accepted, Is.True);
                    Assert.That(InkSkinAchievements.IsUnlocked(id), Is.True);
                }
                long mask = PermanentGrowthProfile.AchievementSnapshot().unlockedMask;
                Assert.That(mask, Is.Positive);
                PermanentGrowthProfile.UseStoreForTests(store);
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().unlockedMask, Is.EqualTo(mask));
                var json = JsonUtility.ToJson(PermanentGrowthProfile.AchievementSnapshot());
                Assert.That(JsonUtility.FromJson<SkinAchievementState>(json).unlockedMask, Is.EqualTo(mask));
                Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string cloud), Is.True);
                // 뒤끝 growthJson은 문자열이다. 53비트 이상도 JSON 숫자로 재해석하지 않고 왕복한다.
                var wire = Newtonsoft.Json.JsonConvert.SerializeObject(new { growthJson = cloud });
                string restored = Newtonsoft.Json.Linq.JObject.Parse(wire)["growthJson"].ToObject<string>();
                Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(restored), Is.True);
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().unlockedMask, Is.EqualTo(mask));
                foreach (var id in Rewards) Assert.That(InkSkinCatalog.IsConcealed(id), Is.False);
                Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
                foreach (var id in Rewards) Assert.That(InkSkinCatalog.IsConcealed(id), Is.True);
            }
            finally { PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
        }

        [Test, Category("MythicAccessoryNaming")]
        public void NewSecretNamesFitTheExistingAchievementNoticeInAllLanguages()
        {
            var settings = new MemoryLobbySettingsStore(); LobbySettingsProfile.UseStoreForTests(settings);
            var host = new GameObject("MythicNoticeTextTest");
            try
            {
                var toast = host.AddComponent<InkAchievementToast>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(InkAchievementToast).GetMethod("Build", flags).Invoke(toast, null);
                var label = (UnityEngine.UI.Text)typeof(InkAchievementToast).GetField("label", flags).GetValue(toast);
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    foreach (var id in Rewards)
                    {
                        InkLocalizedText.SetSource(label, InkSkinCatalog.Name(id));
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height), id + " / " + language);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); LobbySettingsProfile.RestoreDefaultStoreForTests(); }
        }

        [Test, Category("MythicAccessoryNaming")]
        public void AccessoryNamesHaveNoZodiacPrefixesAndKeepExistingRewardIds()
        {
            string[] names = { "밤쥐의 동그란 귀", "느긋한 소의 뿔", "산군의 줄무늬 두건", "달토끼의 접힌 귀",
                "꼬마 용의 새싹 뿔", "실뱀의 느슨한 매듭", "바람말의 흩날린 갈기", "구름양의 돌돌 말린 뿔",
                "잔꾀 원숭이의 둥근 귀", "새벽닭의 붉은 볏", "지킴개의 늘어진 귀", "복돼지의 분홍 귀" };
            string[] assets = { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" };
            for (int i=0; i<names.Length; i++)
            {
                var id=(InkSkinId)(46+i); var d=InkSkinCatalog.Get(id);
                Assert.That(d.Id,Is.EqualTo(id));
                Assert.That(d.Asset,Is.EqualTo("skin_zodiac_"+assets[i]+"_v1"));
                Assert.That(d.Metric,Is.EqualTo(SkinMetric.Secret));
                Assert.That(d.Tier,Is.EqualTo(SkinTier.Hidden));
                Assert.That(d.Goal,Is.EqualTo(1));
                Assert.That(d.Korean,Is.EqualTo(names[i]));
                Assert.That(d.English.Contains("·") || d.Japanese.Contains("・"),Is.False);
                Assert.That(InkSkinTranslations.TryTranslate(d.Korean,false,out var en),Is.True);
                Assert.That(InkSkinTranslations.TryTranslate(d.Korean,true,out var ja),Is.True);
                Assert.That(en,Is.Not.Empty.And.EqualTo(d.English));
                Assert.That(ja,Is.Not.Empty.And.EqualTo(d.Japanese));
            }
            Assert.That(InkSkinCatalog.Name(InkSkinId.HaetaeCrest),Is.EqualTo("해태의 작은 뿔"));
        }

        [Test]
        public void LiveSecretsUseThisRunOnlyAndFailedSaveNeverNotifies()
        {
            var store = new MemoryPermanentGrowthStore(); PermanentGrowthProfile.UseStoreForTests(store);
            try
            {
                var live = new LiveInkAchievements(); live.Begin(); var notices = new List<int>();
                var c = Case(InkSkinId.ThreeLeggedCrow);
                store.ThrowOnPrimarySave = true;
                live.Sample(c.stats, c.height, c.seconds, 0, notices.Add);
                Assert.That(notices, Is.Empty);
                PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
                live.Sample(c.stats, c.height, c.seconds, 0, notices.Add);
                Assert.That(notices, Is.Empty, "다른 계정으로 이어진 판은 지급하지 않는다");
                live.Begin();
                live.Sample(c.stats, c.height, c.seconds, 0, notices.Add);
                live.Sample(c.stats, c.height, c.seconds, 0, notices.Add);
                Assert.That(notices.Count(i => i == (int)InkSkinId.ThreeLeggedCrow), Is.EqualTo(1));
                Assert.That(PermanentGrowthProfile.AchievementSnapshot().Has(InkSkinId.ThreeLeggedCrow), Is.True);
                var separateRun = SkinRunStats.Begin(true, InkSkinId.Ink);
                Assert.That(Has(InkMythicSkins.Evaluate(separateRun, 333, 30, true), InkSkinId.ThreeLeggedCrow), Is.False);
            }
            finally { PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
        }
    }
}
