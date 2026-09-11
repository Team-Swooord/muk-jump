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
        public const string LogoPath = "Assets/Art/Brand/Logo_CYSBand.png";
        public const string LogoGuid = "6fd6fcc61df57486b9fe21a3fbad2e42";
        public const string LogoFadePath = "Assets/Art/Brand/CYSBand_Logo.anim";
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
            ConfigureBrandPlayerSettings();
            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            AnimationClip fade = AssetDatabase.LoadAssetAtPath<AnimationClip>(LogoFadePath);
            if (logo == null)
                throw new InvalidOperationException(
                    $"CYSBand 스플래시 로고를 불러오지 못했습니다: {LogoPath}");
            if (fade == null)
                throw new InvalidOperationException("SHIFT 원본 로고 페이드 클립이 없습니다: " + LogoFadePath);

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
                splash.SetLogo(logo, ReadLogoFadeCurve(fade));

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

        internal static AnimationCurve ReadLogoFadeCurve(AnimationClip clip)
        {
            if (clip == null || !Mathf.Approximately(clip.length, StartupBrandSplash.LogoFadeDuration))
                throw new InvalidOperationException("제작사 로고 원본의 1초 페이드 클립을 확인하세요.");
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(UnityEngine.UI.Image), "m_Color.a");
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length < 2)
                throw new InvalidOperationException("제작사 로고 원본의 알파 곡선이 없습니다.");
            // 런타임 AnimationClip 바인딩에 기대지 않고 원본 키·접선을 그대로 직렬화한다.
            return curve;
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

        /// 제작사 연출은 Splash 씬에서 한 번만 재생한다. 엔진 로고와 중복하지 않는다.
        /// 앱/서명/게임 설정은 복사하지 않는다.
        public static void ConfigureBrandPlayerSettings()
        {
            // PNG와 .meta는 원본 한 쌍으로 보존한다. 예전 로고 임포터 보정으로 덮지 않는다.
            AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceSynchronousImport);
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo == null || AssetDatabase.AssetPathToGUID(LogoPath) != LogoGuid)
                throw new InvalidOperationException("SHIFT 원본 CYSBand 로고/GUID가 없습니다: " + LogoPath);

            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = StartupBrandSplash.BackgroundColor;
            PlayerSettings.SplashScreen.overlayOpacity = 1f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.animationBackgroundZoom = 1f;
            PlayerSettings.SplashScreen.animationLogoZoom = 1f;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.backgroundPortrait = null;
            PlayerSettings.SplashScreen.blurBackgroundImage = true;
            PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();
        }

        /// 프레임 기반 시작 연출을 필요할 때 별도 로컬 실행 파일로 확인한다.
        /// 스토어·계정 삭제 작업과 분리하며 기존 결과물은 덮어쓰지 않는다.
        public static void BuildNativeSplashValidationPlayer()
        {
            string output = Path.GetFullPath("output/qa/brand-splash-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/MukJumpBrandValidation.app");
            if (Directory.Exists(output))
                throw new InvalidOperationException("기존 검증 빌드는 덮어쓰지 않습니다: " + output);
            ConfigureBrandPlayerSettings();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new UnityEditor.Build.BuildFailedException("네이티브 스플래시 검증 빌드 실패");
            Debug.Log("[MukJump] 네이티브 스플래시 검증 빌드 완료: " + output);
        }
    }
}
