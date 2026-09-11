#if UNITY_IOS
using System;
using System.IO;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTests
{
    public sealed class AppleGameCenterBuildTests
    {
        public static void ValidatePlayerAndRebuildScenes()
        {
            var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
            {
                target = BuildTarget.iOS,
                group = BuildTargetGroup.iOS,
                options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
            };
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(
                settings, "Temp/MukJumpGameCenterPlayer");
            if (result.assemblies == null || result.assemblies.Count == 0)
                throw new UnityEditor.Build.BuildFailedException("iOS Player 스크립트 컴파일 실패");
            UnityEngine.Debug.Log("[MukJump] Game Center iOS Player 컴파일 완료: " + result.assemblies.Count);
            MukJumpSceneBuilder.Build();
        }

        [Test] public void ExportAddsGameCenterWithoutLosingAppleSignInAndIsRepeatable()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mukjump-gamecenter-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(directory, "Unity-iPhone.xcodeproj"));
                var project = new PBXProject();
                project.ReadFromString(@"{
                    archiveVersion = 1; objectVersion = 46; classes = {};
                    objects = {
                        A00000000000000000000001 = { isa = PBXProject; mainGroup = A00000000000000000000002;
                            buildConfigurationList = A00000000000000000000003; targets = (); };
                        A00000000000000000000002 = { isa = PBXGroup; children = (); sourceTree = ""<group>""; };
                        A00000000000000000000003 = { isa = XCConfigurationList;
                            buildConfigurations = (A00000000000000000000004); defaultConfigurationName = Release; };
                        A00000000000000000000004 = { isa = XCBuildConfiguration; name = Release; buildSettings = {}; };
                    }; rootObject = A00000000000000000000001;
                }");
                string main = project.AddTarget("Unity-iPhone", "app", "com.apple.product-type.application");
                string framework = project.AddTarget("UnityFramework", "framework", "com.apple.product-type.framework");
                project.AddFrameworksBuildPhase(main);
                project.AddFrameworksBuildPhase(framework);
                project.WriteToFile(Path.Combine(directory, "Unity-iPhone.xcodeproj/project.pbxproj"));
                MukJumpAppleSignInPostprocessor.OnPostProcessBuild(BuildTarget.iOS, directory);
                var entitlement = new PlistDocument();
                entitlement.ReadFromFile(Path.Combine(directory, "MukJump.entitlements"));
                Assert.That(entitlement.root["com.apple.developer.game-center"].AsBoolean(), Is.True);
                Assert.That(entitlement.root["com.apple.developer.applesignin"].AsArray().values[0].AsString(), Is.EqualTo("Default"));
                string first = File.ReadAllText(PBXProject.GetPBXProjectPath(directory));
                Assert.That(first, Does.Contain("GameKit.framework"));
                Assert.That(first, Does.Contain("AuthenticationServices.framework"));
                project.ReadFromString(first);
                Assert.That(project.GetBuildPropertyForAnyConfig(project.GetUnityMainTargetGuid(), "DEVELOPMENT_TEAM"),
                    Is.EqualTo(MukJumpStoreBuild.DefaultAppleDeveloperTeamId));
                Assert.That(project.GetBuildPropertyForAnyConfig(project.GetUnityMainTargetGuid(), "CODE_SIGN_ENTITLEMENTS"),
                    Is.EqualTo("MukJump.entitlements"));
                MukJumpAppleSignInPostprocessor.OnPostProcessBuild(BuildTarget.iOS, directory);
                Assert.That(File.ReadAllText(PBXProject.GetPBXProjectPath(directory)), Is.EqualTo(first));
            }
            finally
            {
                // 이 테스트에서 만든 임시 Xcode fixture만 정리한다.
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
#endif
