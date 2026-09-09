using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MukJump.EditorTools
{
    /// 저장된 씬을 건드리지 않고 실제 빌더·URP로 배경의 시간별 검증 이미지를 만든다.
    public static class MukJumpAmbientCloudPreview
    {
        [MenuItem("MukJump/검증/느린 구름 프리뷰 저장")]
        public static void Capture() => CaptureCore(false);

        [MenuItem("MukJump/검증/맵별 움직이는 배경 프리뷰 저장")]
        public static void CaptureAllMaps() => CaptureCore(true);

        public static void CaptureAllMapsTo(string directory) => CaptureCore(true, directory);

        static void CaptureCore(bool allMaps, string outputDirectory = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("구름 프리뷰는 Play 종료 후 실행하세요.");

            string directory = Path.GetFullPath(outputDirectory ??
                (allMaps ? "output/map-atmospheres/rendered" : "output/ambient-clouds/rendered"));
            Directory.CreateDirectory(directory);
            var scene = MukJumpSceneBuilder.BuildForTests();
            try
            {
                Camera camera = scene.GetRootGameObjects()
                    .Select(go => go.GetComponent<Camera>()).First(value => value != null);
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != camera.gameObject) root.SetActive(false);
                camera.scene = scene;
                camera.enabled = false;
                var background = camera.GetComponentInChildren<MapBackgroundView>();
                var clouds = camera.GetComponentInChildren<AmbientCloudView>();
                FieldInfo clock = typeof(AmbientCloudView).GetField(
                    "elapsedSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo flow = typeof(AmbientCloudView).GetField(
                    "flowSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo refresh = typeof(AmbientCloudView).GetMethod(
                    "Refresh", BindingFlags.Instance | BindingFlags.NonPublic);

                void SetTime(double seconds)
                {
                    clock.SetValue(clouds, seconds);
                    flow.SetValue(clouds, seconds * AmbientCloudMotionProfile.For(clouds.CurrentMotion).Flow);
                    refresh.Invoke(clouds, new object[] { 0f, false, VfxQualityTier.High });
                }

                for (int i = 0; i < 60; i++)
                    refresh.Invoke(clouds, new object[] { 0.05f, false, VfxQualityTier.High });

                // 15초 / 4fps. 재생 속도를 과장하지 않은 실제 이동량이다.
                camera.aspect = 9f / 16f;
                background.SetStage(0, true);
                SetTime(0d);
                // 수동 프리뷰의 첫 URP 요청을 예열한 뒤 같은 시점부터 저장한다.
                Save(camera, 540, 960, null);
                int mapCount = background.BaseStageCount + background.EndlessStageCount;
                for (int stage = 0; stage < (allMaps ? mapCount : 1); stage++)
                {
                    string clipDirectory = allMaps ? Path.Combine(directory, $"map-{stage:00}") : directory;
                    Directory.CreateDirectory(clipDirectory);
                    background.SetStage(stage, true);
                    SetTime(0d);
                    Save(camera, 270, 480, null);
                    for (int frame = 0; frame < 60; frame++)
                    {
                        SetTime(frame / 4d);
                        Save(camera, allMaps ? 270 : 540, allMaps ? 480 : 960,
                            Path.Combine(clipDirectory, $"frame-{frame:000}.png"));
                    }
                }

                foreach (Vector2Int size in new[] {
                    new Vector2Int(1080, 1920), new Vector2Int(1179, 2556),
                    new Vector2Int(1440, 3200), new Vector2Int(1920, 1080) })
                {
                    camera.aspect = size.x / (float)size.y;
                    for (int stage = 0; stage < (allMaps ? mapCount : 1); stage++)
                    {
                        background.SetStage(stage, true);
                        SetTime(30d);
                        Save(camera, size.x, size.y,
                            Path.Combine(directory, $"map-{stage:00}-viewport-{size.x}x{size.y}.png"));
                    }
                }

                camera.aspect = 9f / 16f;
                for (int stage = 0; stage < background.BaseStageCount + background.EndlessStageCount; stage++)
                {
                    background.SetStage(stage, true, mirrorX: true);
                    SetTime(30d);
                    Save(camera, 540, 960, Path.Combine(directory, $"stage-{stage:00}.png"));
                }
                Debug.Log($"[MukJump] 실제 URP 구름 프리뷰 저장 완료: {directory}");
            }
            finally { MukJumpSceneBuilder.CloseTestScene(scene); }
        }

        internal static void Save(Camera camera, int width, int height, string path)
        {
            var target = RenderTexture.GetTemporary(width, height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("현재 렌더 파이프라인이 프리뷰 요청을 지원하지 않습니다.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply(false, false);
                if (path != null) File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
