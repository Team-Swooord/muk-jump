using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    [Category("HyunmuSplit")]
    public sealed class HyunmuSkinTests
    {
        [TearDown] public void Cleanup() => InkSkinChart.Reset();

        [Test] public void SplitKeepsIdsOwnershipAndTranslations()
        {
            Assert.That((int)InkSkinId.TurtleShell, Is.EqualTo(19));
            Assert.That((int)InkSkinId.HyunmuHelm, Is.EqualTo(63));
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.TurtleShell), Is.False);
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.HyunmuHelm), Is.True);
            Assert.That(InkSkinCatalog.BrowseIndex(InkSkinId.HyunmuHelm), Is.GreaterThanOrEqualTo(0));
            var legacy = new SkinAchievementState(); legacy.Grant(InkSkinId.TurtleShell);
            Assert.That(legacy.Progress(InkSkinId.HyunmuHelm, 0, 0), Is.EqualTo(3));
            Assert.That(legacy.Has(InkSkinId.TurtleShell), Is.True);
            foreach (var text in new[] { InkSkinCatalog.Name(InkSkinId.HyunmuHelm),
                InkSkinCatalog.Ability(InkSkinId.HyunmuHelm), InkSkinCatalog.Description(InkSkinId.HyunmuHelm),
                InkSkinLore.Get(InkSkinId.HyunmuHelm).ko, InkSkinAchievements.Requirement(InkSkinId.HyunmuHelm) })
                foreach (bool ja in new[] { false, true })
                {
                    Assert.That(InkSkinTranslations.TryTranslate(text, ja, out var translation), Is.True, text);
                    Assert.That(translation, Is.Not.Empty.And.Not.EqualTo(text));
                }
        }

        [Test] public void LastBitRoundTripsWithoutNegativeLegacyMaskOrLostRewards()
        {
            var state = new SkinAchievementState();
            state.Grant(InkSkinId.Gat); state.Grant(InkSkinId.TurtleShell); state.Grant(InkSkinId.HyunmuHelm);
            Assert.That(state.IsValid, Is.True);
            Assert.That(state.unlockedMask, Is.Positive);
            Assert.That(state.unlockedMaskHigh, Is.EqualTo(1));
            var restored = JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(state));
            Assert.That(restored.IsValid, Is.True);
            Assert.That(restored.UnlockedMask, Is.EqualTo(state.UnlockedMask));
            Assert.That(restored.Has(InkSkinId.HyunmuHelm), Is.True);
            Assert.That(InkSkinCatalog.AvailableRewardMask & (1UL << 63), Is.EqualTo(1UL << 63));
            Assert.That(InkSkinCatalog.AvailableRewardMask & (1UL << 19), Is.Not.Zero);
            Assert.That(InkSkinCatalog.AvailableRewardMask & (1UL << 20), Is.Zero);
            // Apple 콘솔에 아직 등록하지 않은 새 업적을 전송하지 않는다.
            Assert.That(InkAchievementCatalog.SavedProgress(restored, 0).Any(x => x.id.EndsWith(".63")), Is.False);
            var legacyJson = JObject.Parse(JsonUtility.ToJson(state)); legacyJson.Remove("unlockedMaskHigh");
            var old = JsonUtility.FromJson<SkinAchievementState>(legacyJson.ToString());
            Assert.That(old.IsValid, Is.True); Assert.That(old.Has(InkSkinId.TurtleShell), Is.True);
            old.unlockedMaskHigh = 2; Assert.That(old.IsValid, Is.False);
            old.unlockedMaskHigh = -1; Assert.That(old.IsValid, Is.False);
        }

        [Test] public void LiveGrantAndSettlementPersistNewSkinAndIndependentBestHeight()
        {
            var store = new MemoryPermanentGrowthStore(); PermanentGrowthProfile.UseStoreForTests(store);
            try
            {
                var stats = SkinRunStats.Begin(false, InkSkinId.Ink); stats.maxLivingPlayers = 3;
                ulong live = LiveInkAchievements.Evaluate(new SkinAchievementState(), stats, 10, 5);
                Assert.That(live & (1UL << 63), Is.Not.Zero);
                Assert.That(PermanentGrowthProfile.TryGrantLiveAchievements(PermanentGrowthProfile.SkinAccountToken, live, 0), Is.True);
                PermanentGrowthProfile.UseStoreForTests(store);
                Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.HyunmuHelm), Is.True);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.TurtleShell), Is.True);
                var run = SkinRunStats.Begin(false, InkSkinId.HyunmuHelm);
                Assert.That(PermanentGrowthProfile.SettleRun("hyunmu-record", 73, 73, 0, 12, true, run).Accepted, Is.True);
                PermanentGrowthProfile.UseStoreForTests(store);
                var saved = PermanentGrowthProfile.AchievementSnapshot();
                Assert.That(saved.IsValid, Is.True);
                Assert.That(saved.BestHeightForSkin(InkSkinId.HyunmuHelm), Is.EqualTo(73));
                Assert.That(saved.BestHeightForSkin(InkSkinId.TurtleShell), Is.EqualTo(-1));
            }
            finally { PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
        }

        [Test] public void OldChartKeepsCustomCopyButCannotRestoreTurtleAbility()
        {
            var rows = InkSkinChartExport.Rows();
            rows.Single(x => (string)x["skinKey"] == "HyunmuHelm").Remove();
            var turtle = rows.Single(x => (string)x["skinKey"] == "TurtleShell");
            turtle["abilityKey"] = "TurtleShell";
            turtle["nameKo"] = "거북이 탈";
            turtle["loreKo"] = "직접 수정한 거북 이야기";
            turtle["abilityNameKo"] = "시작 방어막";
            Assert.That(InkSkinChart.TryParse(rows.ToString(), out var parsed, out var error), Is.True, error);
            InkSkinChart.Apply(parsed);
            Assert.That(InkSkinCatalog.Name(InkSkinId.TurtleShell), Is.EqualTo("거북이 탈"));
            Assert.That(InkSkinLore.Get(InkSkinId.TurtleShell).ko, Is.EqualTo("직접 수정한 거북 이야기"));
            Assert.That(InkSkinCatalog.Ability(InkSkinId.TurtleShell), Is.Empty);
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.TurtleShell), Is.False);
            Assert.That(InkSkinCatalog.Name(InkSkinId.HyunmuHelm), Is.EqualTo("현무의 투구"));
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.HyunmuHelm), Is.True);
            Assert.That(InkSkinChart.TryParse(InkSkinChartExport.Rows().ToString(), out _, out error), Is.True, error);
        }

        [Test] public void NewArtIsTransparentAndUsesSingleMobileSprite()
        {
            var id = InkSkinId.HyunmuHelm;
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes("Assets/Resources/" + InkSkinCatalog.ResourcePath(id) + ".png")), Is.True);
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length * .25));
                Assert.That(pixels[0].a, Is.Zero); Assert.That(pixels[^1].a, Is.Zero);
                var sprite = InkSkinCatalog.Sprite(id);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.texture.width, Is.LessThanOrEqualTo(512));
                Assert.That(InkSkinCatalog.BehindBody(id), Is.False);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
