#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTests
{
    public sealed class IosLocalizationBuildTests
    {
        string directory;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "mukjump-ios-locales-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "Unity-iPhone.xcodeproj"));
            var project = new PBXProject();
            project.ReadFromString(@"{
                archiveVersion = 1; objectVersion = 46; classes = {};
                objects = {
                    A00000000000000000000001 = { isa = PBXProject; mainGroup = A00000000000000000000002;
                        buildConfigurationList = A00000000000000000000003; targets = ();
                        developmentRegion = English; knownRegions = (English, Japanese, French, German, en, Base); };
                    A00000000000000000000002 = { isa = PBXGroup; children = (); sourceTree = ""<group>""; };
                    A00000000000000000000003 = { isa = XCConfigurationList; buildConfigurations = (); };
                }; rootObject = A00000000000000000000001;
            }");
            string target = project.AddTarget("Unity-iPhone", "app", "com.apple.product-type.application");
            project.AddResourcesBuildPhase(target);
            project.WriteToFile(Path.Combine(directory, "Unity-iPhone.xcodeproj/project.pbxproj"));
            var plist = new PlistDocument();
            plist.root.SetString("CFBundleIdentifier", "com.CYSB.MukJump");
            plist.WriteToFile(Path.Combine(directory, "Info.plist"));
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ProjectAndBundleDeclareAllThreeLanguagesAndEnglishFallback()
        {
            MukJumpIosPrivacyPostprocessor.ApplyToInfoPlist(directory);
            MukJumpIosPrivacyPostprocessor.ApplyLocalizedTrackingDescriptions(directory);
            var project = new PBXProject();
            project.ReadFromFile(PBXProject.GetPBXProjectPath(directory));
            string regionList = Regex.Match(project.WriteToString(), @"knownRegions\s*=\s*\(([^)]*)\)").Groups[1].Value;
            Assert.That(regionList.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0),
                Is.EquivalentTo(new[] { "ko", "en", "ja", "Base" }));
            Assert.That(project.WriteToString(), Does.Contain("developmentRegion = en;"));
            var plist = new PlistDocument();
            plist.ReadFromFile(Path.Combine(directory, "Info.plist"));
            Assert.That(plist.root["CFBundleLocalizations"].AsArray().values.Select(v => v.AsString()),
                Is.EquivalentTo(new[] { "ko", "en", "ja" }));
            Assert.That(plist.root["CFBundleDevelopmentRegion"].AsString(), Is.EqualTo("en"));
            Assert.That(plist.root["ITSAppUsesNonExemptEncryption"].AsBoolean(), Is.False);
            Assert.That(plist.root["CFBundleIdentifier"].AsString(), Is.EqualTo("com.CYSB.MukJump"));
        }

        [TestCase("ko", "먹점프", "광고")]
        [TestCase("en", "MukJump", "Allow tracking")]
        [TestCase("ja", "MukJump", "トラッキング")]
        public void LocalizedNameAndPermissionAreBundledWithoutDuplicates(string language, string name, string permission)
        {
            string folder = Path.Combine(directory, language + ".lproj");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "InfoPlist.strings");
            File.WriteAllText(path, "// 다른 기능의 문구는 보존한다.\n\"OtherFeatureKey\" = \"Keep me\";\n");
            MukJumpIosPrivacyPostprocessor.ApplyLocalizedTrackingDescriptions(directory);
            string firstProject = File.ReadAllText(PBXProject.GetPBXProjectPath(directory));
            string firstContents = File.ReadAllText(path);
            MukJumpIosPrivacyPostprocessor.ApplyLocalizedTrackingDescriptions(directory);
            Assert.That(File.ReadAllText(PBXProject.GetPBXProjectPath(directory)), Is.EqualTo(firstProject));
            Assert.That(File.ReadAllText(path), Is.EqualTo(firstContents));
            Assert.That(firstContents, Does.Contain("\"CFBundleDisplayName\" = \"" + name + "\";"));
            Assert.That(firstContents, Does.Contain(permission));
            Assert.That(firstContents, Does.Contain("\"OtherFeatureKey\" = \"Keep me\";"));
            var project = new PBXProject();
            project.ReadFromString(firstProject);
            Assert.That(project.FindFileGuidByProjectPath(language + ".lproj/InfoPlist.strings"), Is.Not.Empty);
        }
    }
}
#endif
