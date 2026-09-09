using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MukJump.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MukJump.EditorTools
{
    /// 먹점프의 첫 빌드 씬인 CYSBand 스플래시를 코드로 재현한다.
    /// Main.unity와 마찬가지로 씬 YAML을 직접 수정하지 않는다.
    public static class MukJumpSplashSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Splash.unity";
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string LogoPath =
            "Assets/Art/Brand/cysband_logo_white.png";
        public const string GameLogoPath = "Assets/Art/UI/muk_logo.png";
        const string RequestPath = "Temp/MukJumpBuildSplashScene.request";
        const string ResultPath = "Temp/MukJumpBuildSplashScene.result";
        static double nextRequestPollTime;
        static bool buildScheduled;

        [InitializeOnLoadMethod]
        static void InstallRequestWatcher()
        {
            EditorApplication.update -= PollRequestedBuild;
            EditorApplication.update += PollRequestedBuild;
            PollRequestedBuild();
        }

        static void PollRequestedBuild()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPollTime)
                return;
            nextRequestPollTime = EditorApplication.timeSinceStartup + 1d;
            if (!File.Exists(RequestPath) || buildScheduled)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            buildScheduled = true;
            EditorApplication.delayCall += RunRequestedBuild;
        }

        static void RunRequestedBuild()
        {
            buildScheduled = false;
            if (!File.Exists(RequestPath))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            File.Delete(RequestPath);
            try
            {
                Build();
                File.WriteAllText(ResultPath, "ok\n");
            }
            catch (Exception exception)
            {
                File.WriteAllText(
                    ResultPath,
                    $"error={exception.GetType().Name}: {exception.Message}\n");
                throw;
            }
        }

        [MenuItem("MukJump/Build Splash Scene")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning(
                    "[MukJump] 컴파일·에셋 갱신이 끝난 뒤 Splash 씬을 생성하세요.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[MukJump] Play Mode를 종료한 뒤 Splash 씬을 생성하세요.");
                return;
            }

            BuildSceneFile();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[MukJump] Splash 씬 구성 완료 — 출시 전에는 Main 씬을 다시 생성해 " +
                "두 씬을 함께 증명하세요. 빌드 순서: Splash → Main");
        }

        internal static void BuildSceneFile()
        {
            ConfigureLogoSprite();
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo == null)
                throw new InvalidOperationException(
                    $"CYSBand 스플래시 로고를 불러오지 못했습니다: {LogoPath}");

            Scene previousScene = SceneManager.GetActiveScene();
            Scene alreadyLoaded = SceneManager.GetSceneByPath(ScenePath);
            if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded)
                throw new InvalidOperationException(
                    "열려 있는 Splash 씬은 자동 교체하지 않습니다. " +
                    "씬을 닫은 뒤 다시 실행하세요.");

            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                ScenePath) != null;
            Scene splashScene = sceneExists
                ? EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(splashScene);
                foreach (GameObject rootObject in splashScene.GetRootGameObjects())
                    UnityEngine.Object.DestroyImmediate(rootObject);

                var root = new GameObject("@SplashScene");
                StartupBrandSplash splash = root.AddComponent<StartupBrandSplash>();
                splash.SetLogo(logo);

                if (!EditorSceneManager.SaveScene(splashScene, ScenePath))
                    throw new InvalidOperationException(
                        $"Splash 씬을 저장하지 못했습니다: {ScenePath}");
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousScene);
                if (splashScene.IsValid() && splashScene.isLoaded)
                    EditorSceneManager.CloseScene(splashScene, true);
            }
        }

        internal static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = ResolveBuildSettings(
                EditorBuildSettings.scenes);
        }

        [MenuItem("MukJump/Configure English Logo Artwork")]
        internal static void ConfigureLocalizedLogoArtwork()
        {
            string path = InkLocalizedGameLogo.EnglishAssetPath;
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("영문 먹점프 로고 원화가 없습니다: " + path);
            importer.textureType = TextureImporterType.GUI;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        internal static EditorBuildSettingsScene[] ResolveBuildSettings(
            IEnumerable<EditorBuildSettingsScene> existingScenes)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(ScenePath, true),
                new(MainScenePath, true),
            };
            if (existingScenes != null)
            {
                scenes.AddRange(existingScenes
                    .Where(scene =>
                        !string.Equals(
                            scene.path,
                            ScenePath,
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            scene.path,
                            MainScenePath,
                            StringComparison.Ordinal))
                    .Select(scene => new EditorBuildSettingsScene(
                        scene.path,
                        false)));
            }
            return scenes.ToArray();
        }

        static void ConfigureLogoSprite()
        {
            if (AssetImporter.GetAtPath(LogoPath) is not TextureImporter importer)
            {
                AssetDatabase.ImportAsset(
                    LogoPath,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            }
            if (importer == null)
                throw new InvalidOperationException(
                    $"CYSBand 로고 임포터를 찾지 못했습니다: {LogoPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }
    }
}
