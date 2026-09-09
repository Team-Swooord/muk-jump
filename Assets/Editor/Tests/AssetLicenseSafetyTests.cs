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
        const string BuildFontLicensePath =
            "Assets/StreamingAssets/ThirdParty/NanumBrushScript-OFL.txt";
        const string ButtonPath =
            "Assets/Resources/MukJump/UI/Common/action_button_brush.png";
        const string ActiveHanjiButtonPath =
            "Assets/Resources/MukJump/UI/Common/action_button_hanji_v1.png";

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
                    "90f6a909ffb2af7f6422ea20042a26180fe5520cba80bbbf75a5e660320d7924"));
            Assert.That(File.Exists(BuildFontLicensePath), Is.True);
            Assert.That(
                Sha256(BuildFontLicensePath),
                Is.EqualTo(Sha256(FontLicensePath)),
                "배포물에 포함되는 OFL 원문은 검증된 원본과 같아야 합니다.");
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

        [Test]
        public void ActiveHanjiActionButtonIsPinnedToGeneratedProjectAsset()
        {
            Assert.That(File.Exists(ActiveHanjiButtonPath), Is.True);
            Assert.That(
                Sha256(ActiveHanjiButtonPath),
                Is.EqualTo(
                    "fa19515d6559272d8d28f6e0729a06c78f8ffe4d24bf328608a5092e2766995e"),
                "활성 한지 버튼이 누락되거나 다른 이미지로 바뀌면 출시 검증이 실패해야 합니다.");
        }

        [Test]
        public void ReleasePreflightHasMatchingThirdPartyNotices()
        {
            Assert.That(
                MukJump.EditorTools.MukJumpStoreBuild
                    .CollectThirdPartyNoticeIssues(),
                Is.Empty);

            string notices = File.ReadAllText(
                MukJump.EditorTools.MukJumpStoreBuild
                    .BundledThirdPartyNoticesPath);
            foreach (string marker in new[]
                     {
                         "Nanum Brush Script",
                         "SIL Open Font License 1.1",
                         "Reitanna",
                         "CC0",
                         "Inkdrop Ascent",
                     })
                Assert.That(notices, Does.Contain(marker));
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
