#if UNITY_IOS
using System.IO;
using AppleAuth.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MukJump.EditorTools
{
    /// Unity가 iOS Xcode 프로젝트를 다시 만들어도 Apple 로그인 권한과
    /// AuthenticationServices.framework가 빠지지 않게 자동 적용한다.
    public static class MukJumpAppleSignInPostprocessor
    {
        [PostProcessBuild(210)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;

            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromString(File.ReadAllText(projectPath));
            var manager = new ProjectCapabilityManager(
                projectPath,
                "MukJump.entitlements",
                null,
                project.GetUnityMainTargetGuid());
            manager.AddSignInWithAppleWithCompatibility();
            manager.WriteToFile();
        }
    }
}
#endif
