using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class DeviceRegionTests
    {
        [TestCase("kr", "KR")]
        [TestCase(" US ", "US")]
        [TestCase("JP", "JP")]
        [TestCase("en-US", "")]
        [TestCase("日本", "")]
        [TestCase("001", "")]
        [TestCase(null, "")]
        public void NormalizesOnlyTwoLetterRegion(string input, string expected) =>
            Assert.That(DeviceRegion.Normalize(input), Is.EqualTo(expected));

        [TestCase("KR", "1f1f0-1f1f7")]
        [TestCase("US", "1f1fa-1f1f8")]
        [TestCase("JP", "1f1ef-1f1f5")]
        [TestCase("GB", "1f1ec-1f1e7")]
        [TestCase(null, "1f310")]
        public void RegionIconsAreBundledWithoutFontDependency(string code, string filename)
        {
            Assert.That(DeviceRegion.IconResource(code), Does.EndWith(filename));
            Assert.That(Resources.Load<Texture2D>(DeviceRegion.IconResource(code)), Is.Not.Null);
            Assert.That(RegionFlagImages.Get(code), Is.Not.Null);
        }

        [Test]
        public void LegacyOrUnknownRegionsUseGlobeWithoutChangingLeaderboardRecord()
        {
            var entry = new MukJumpLeaderboardEntry(2, 43, "먹테스트");
            Assert.That(entry.RegionCode, Is.Empty);
            Assert.That(entry.Height, Is.EqualTo(43));
            Assert.That(entry.DisplayName, Is.EqualTo("먹테스트"));
            Assert.That(RegionFlagImages.Get("ZZ").texture,
                Is.SameAs(RegionFlagImages.Get(null).texture));
            var japanese = new MukJumpLeaderboardEntry(2, 43, "먹테스트", regionCode: "jp");
            Assert.That(japanese.RegionCode, Is.EqualTo("JP"));
            Assert.That(japanese.Height, Is.EqualTo(entry.Height));
        }
    }
}
