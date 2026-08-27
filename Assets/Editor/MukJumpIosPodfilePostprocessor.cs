using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace MukJump.EditorTools
{
    /// Xcode 26에서 오래된 하위 Pod의 배포 타깃 경고가 나지 않도록 생성된 Podfile을 보정한다.
    public static class MukJumpIosPodfilePostprocessor
    {
        const string Marker = "# MukJump iOS 15 pod target normalization";

        // EDM4U가 Podfile을 만드는 40 이후, pod install을 실행하는 50 이전에 추가해야 한다.
        [PostProcessBuild(45)]
        static void NormalizePodDeploymentTargets(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath))
                return;

            string contents = File.ReadAllText(podfilePath);
            if (contents.Contains(Marker))
                return;

            File.AppendAllText(
                podfilePath,
                "\n" + Marker + "\n" +
                "post_install do |installer|\n" +
                "  installer.pods_project.targets.each do |target|\n" +
                "    target.build_configurations.each do |config|\n" +
                "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                "    end\n" +
                "  end\n" +
                "end\n");
        }
    }
}
