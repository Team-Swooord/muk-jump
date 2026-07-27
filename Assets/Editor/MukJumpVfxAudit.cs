using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MukJump.Core;

namespace MukJump.EditorTools
{
    /// 현재 프로젝트를 수정하지 않고 모바일 VFX 예산과 재현 가능한 씬 구성을 점검한다.
    public static class MukJumpVfxAudit
    {
        const string VfxTextureRoot = "Assets/MukJump/VFX";

        [MenuItem("MukJump/Diagnostics/Audit Mobile VFX")]
        public static void Run()
        {
            Debug.Log(BuildReport());
        }

        public static string BuildReport()
        {
            var report = new StringBuilder(1024);
            report.AppendLine("[MukJump VFX Audit]");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Render Pipeline: " +
                              $"{UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "Built-in"}");
            report.AppendLine($"Color Space: {PlayerSettings.colorSpace}");
            report.AppendLine($"Android Graphics API: " +
                              $"{(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ? "Auto" : "Manual")}");
            AppendQualityProfiles(report);
            AppendVfxTextureSummary(report);
            AppendGeneratedSceneSummary(report);
            report.AppendLine(
                "실기 필수: GLES3/Vulkan 빌드, 15~30분 발열, Pause/Resume, Low Memory, " +
                "24분신 동시 점프를 별도로 검증하세요.");
            return report.ToString();
        }

        static void AppendQualityProfiles(StringBuilder report)
        {
            report.AppendLine("Quality profiles:");
            for (int i = 0; i <= (int)VfxQualityTier.High; i++)
            {
                var tier = (VfxQualityTier)i;
                var profile = VfxQualityRuntime.GetProfile(tier);
                report.AppendLine(
                    $"- {tier}: line {profile.TransientLineLimit}, " +
                    $"sprite {profile.TransientSpriteLimit}, " +
                    $"weather {profile.WeatherLineCount}, " +
                    $"composite {profile.CompositeConcurrentLimit}");
            }
        }

        static void AppendVfxTextureSummary(StringBuilder report)
        {
            string[] textureGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { VfxTextureRoot });
            long estimatedRgbaBytes = 0;
            int uncompressedCount = 0;
            int mipmappedCount = 0;
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (texture != null)
                    estimatedRgbaBytes += (long)texture.width * texture.height * 4;
                if (importer == null) continue;
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                    uncompressedCount++;
                if (importer.mipmapEnabled)
                    mipmappedCount++;
            }

            report.AppendLine(
                $"VFX textures: {textureGuids.Length}, " +
                $"estimated RGBA {estimatedRgbaBytes / (1024f * 1024f):0.00} MiB, " +
                $"uncompressed {uncompressedCount}, mipmapped {mipmappedCount}");
        }

        static void AppendGeneratedSceneSummary(StringBuilder report)
        {
            Scene preview = default;
            try
            {
                preview = MukJumpSceneBuilder.BuildForTests();
                var roots = preview.GetRootGameObjects();
                int lineRenderers = 0;
                int spriteRenderers = 0;
                int particleSystems = 0;
                int audioSources = 0;
                Camera camera = null;
                VfxRuntimeMonitor monitor = null;
                for (int i = 0; i < roots.Length; i++)
                {
                    lineRenderers += roots[i]
                        .GetComponentsInChildren<LineRenderer>(true).Length;
                    spriteRenderers += roots[i]
                        .GetComponentsInChildren<SpriteRenderer>(true).Length;
                    particleSystems += roots[i]
                        .GetComponentsInChildren<ParticleSystem>(true).Length;
                    audioSources += roots[i]
                        .GetComponentsInChildren<AudioSource>(true).Length;
                    camera ??= roots[i].GetComponentInChildren<Camera>(true);
                    monitor ??= roots[i].GetComponentInChildren<VfxRuntimeMonitor>(true);
                }

                report.AppendLine(
                    $"Generated scene: LineRenderer {lineRenderers}, " +
                    $"SpriteRenderer {spriteRenderers}, ParticleSystem {particleSystems}, " +
                    $"AudioSource {audioSources}");
                report.AppendLine(
                    $"Generated camera: HDR {(camera != null && camera.allowHDR ? "On" : "Off")}, " +
                    $"MSAA {(camera != null && camera.allowMSAA ? "On" : "Off")}");
                report.AppendLine(
                    $"VFX runtime monitor: {(monitor != null ? "Present" : "Missing")}");
            }
            finally
            {
                if (preview.IsValid() && preview.isLoaded)
                    MukJumpSceneBuilder.CloseTestScene(preview);
            }
        }
    }
}
