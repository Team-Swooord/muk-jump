using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace MukJump.EditorTests
{
    public sealed class AssetLicenseSafetyTests
    {
        const string FontPath =
            "Assets/Resources/MukJump/Fonts/NanumBrushScript-Regular.ttf";
        const string FontLicensePath =
            "Assets/ThirdParty/NanumBrushScript/OFL.txt";
        const string ButtonPath =
            "Assets/Resources/MukJump/UI/Common/action_button_brush.png";

        [Test]
        public void RedistributableFontAndLicenseArePinnedByHash()
        {
            Assert.That(File.Exists(FontPath), Is.True);
            Assert.That(
                Sha256(FontPath),
                Is.EqualTo(
                    "27ceaf578c96f594cdf07fe0181b251790acbb746a164e45c1f6473f89911a31"));
            Assert.That(File.Exists(FontLicensePath), Is.True);
            Assert.That(
                Sha256(FontLicensePath),
                Is.EqualTo(
                    "eeacf16032901d0ed0456876ec77b8f0fda6b3fecec7d972f8543eb602e6c30f"));
        }

        [Test]
        public void RestrictedRawMediaIsAbsentFromBuildAssetTree()
        {
            Assert.That(
                File.Exists(
                    "Assets/Resources/MukJump/Fonts/" +
                    "HealthsetJoritdaeStd.otf"),
                Is.False);
            Assert.That(
                File.Exists(
                    "Assets/Resources/MukJump/Audio/SFX/" +
                    "SFX_Character_Death_Slime.mp3"),
                Is.False);
            Assert.That(
                File.Exists(
                    "Assets/Resources/MukJump/Audio/SFX/" +
                    "SFX_Game_Over_Ink_Spill.mp3"),
                Is.False);
        }

        [Test]
        public void SharedActionButtonIsTheOwnedProceduralMask()
        {
            Assert.That(File.Exists(ButtonPath), Is.True);
            Assert.That(
                Sha256(ButtonPath),
                Is.EqualTo(
                    "585fb981afeb49ad525ea7405697cf97caaa5c428599618b92f07a42607ccab8"));
        }

        static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(
                    sha.ComputeHash(File.ReadAllBytes(path)))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
