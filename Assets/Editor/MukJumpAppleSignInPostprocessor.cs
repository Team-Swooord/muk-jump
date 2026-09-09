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
            if (MukJump.Core.AppleGameCenterRuntime.Configured)
                manager.AddGameCenter();
            manager.WriteToFile();

            // capability writer 이후에 다시 열어 앱 타깃의 자동 서명을 명시한다.
            // 이전 Xcode 출력이나 다른 postprocessor가 수동 프로파일을 남겨도
            // 잘못된 팀의 인증서가 선택되지 않도록 타깃 설정이 우선하게 한다.
            project = new PBXProject();
            project.ReadFromString(File.ReadAllText(projectPath));
            string mainTargetGuid = project.GetUnityMainTargetGuid();
            // 네이티브 브리지는 UnityFramework에 들어간다. 서명 capability는 앱 타깃에 둔다.
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "GameKit.framework", false);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AuthenticationServices.framework", false);
            project.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", "MukJump.entitlements");
            project.SetBuildProperty(
                mainTargetGuid,
                "DEVELOPMENT_TEAM",
                MukJumpStoreBuild.DefaultAppleDeveloperTeamId);
            project.SetBuildProperty(
                mainTargetGuid,
                "CODE_SIGN_STYLE",
                "Automatic");
            project.SetBuildProperty(
                mainTargetGuid,
                "PROVISIONING_PROFILE",
                string.Empty);
            project.SetBuildProperty(
                mainTargetGuid,
                "PROVISIONING_PROFILE_SPECIFIER",
                string.Empty);
            project.SetBuildProperty(
                mainTargetGuid,
                "PROVISIONING_PROFILE_APP",
                string.Empty);
            project.SetBuildProperty(
                mainTargetGuid,
                "PROVISIONING_PROFILE_SPECIFIER_APP",
                string.Empty);
            File.WriteAllText(projectPath, project.WriteToString());
        }
    }
}
#endif
