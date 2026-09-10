using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class StartupBrandSplashTests
    {
        GameObject host;
        Texture2D logoTexture;
        Sprite logoSprite;

        [TearDown]
        public void TearDown()
        {
            typeof(StartupBrandSplash).GetField("restartSceneForTests",
                BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            typeof(StartupBrandSplash).GetProperty("IsBlockingInput").SetValue(null, false);
            typeof(StartupBrandSplash).GetField("replayRequested",
                BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            PointerInput.ResetSuppressionForTests();
            if (host != null)
                Object.DestroyImmediate(host);
            if (logoSprite != null)
                Object.DestroyImmediate(logoSprite);
            if (logoTexture != null)
                Object.DestroyImmediate(logoTexture);
        }

        [Test]
        public void DeletionRestartsSplashOnceAndBlocksOldSceneInputImmediately()
        {
            int requests = 0;
            string requestedScene = null;
            typeof(StartupBrandSplash).GetField("restartSceneForTests",
                BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
                    new System.Action<string>(scene => { requests++; requestedScene = scene; }));
            var restart = typeof(StartupBrandSplash).GetMethod("TryRestartAfterAccountDeletion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(restart.Invoke(null, null), Is.True);
            Assert.That(StartupBrandSplash.IsBlockingInput, Is.True);
            Assert.That(restart.Invoke(null, null), Is.True);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(requestedScene, Is.EqualTo("Splash"));
        }

        [Test]
        public void FailedSplashRequestReleasesInputAndCanBeRetriedWithoutDeletingAgain()
        {
            var hook = typeof(StartupBrandSplash).GetField("restartSceneForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            hook.SetValue(null, new System.Action<string>(_ =>
                throw new System.InvalidOperationException("scene unavailable")));
            var restart = typeof(StartupBrandSplash).GetMethod("TryRestartAfterAccountDeletion",
                BindingFlags.Static | BindingFlags.NonPublic);
            LogAssert.Expect(LogType.Warning,
                "[MukJump] 계정 삭제 후 시작 화면 복귀 실패: scene unavailable");
            Assert.That(restart.Invoke(null, null), Is.False);
            Assert.That(StartupBrandSplash.IsBlockingInput, Is.False);
            hook.SetValue(null, new System.Action<string>(_ => { }));
            Assert.That(restart.Invoke(null, null), Is.True);
        }

        [Test]
        public void NativeSplashUsesExactlyTheShiftBrandAndSettings()
        {
            Assert.That(PlayerSettings.SplashScreen.show, Is.True);
            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
            Assert.That(PlayerSettings.SplashScreen.backgroundColor,
                Is.EqualTo(StartupBrandSplash.BackgroundColor));
            Assert.That(PlayerSettings.SplashScreen.overlayOpacity, Is.EqualTo(1f));
            Assert.That(PlayerSettings.SplashScreen.animationMode,
                Is.EqualTo(PlayerSettings.SplashScreen.AnimationMode.Dolly));
            Assert.That(PlayerSettings.SplashScreen.animationBackgroundZoom, Is.EqualTo(1f));
            Assert.That(PlayerSettings.SplashScreen.animationLogoZoom, Is.EqualTo(1f));
            Assert.That(PlayerSettings.SplashScreen.unityLogoStyle,
                Is.EqualTo(PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark));
            Assert.That(PlayerSettings.SplashScreen.drawMode,
                Is.EqualTo(PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow));
            Assert.That(PlayerSettings.SplashScreen.background, Is.Null);
            Assert.That(PlayerSettings.SplashScreen.backgroundPortrait, Is.Null);
            Assert.That(PlayerSettings.SplashScreen.blurBackgroundImage, Is.True);
            var logos = PlayerSettings.SplashScreen.logos;
            Assert.That(logos.Length, Is.EqualTo(1));
            Assert.That(logos[0].duration, Is.EqualTo(2f));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(logos[0].logo)),
                Is.EqualTo(MukJumpSplashSceneBuilder.LogoGuid));
        }

        [Test]
        public void BrandOriginalBytesAndImporterArePreserved()
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = System.BitConverter.ToString(sha.ComputeHash(
                File.ReadAllBytes(MukJumpSplashSceneBuilder.LogoPath))).Replace("-", "").ToLowerInvariant();
            Assert.That(hash, Is.EqualTo("d114445ffa2df2c698d8d3dc9c7ca328c991225f430d0397f043833f2cc44861"));
            var importer = AssetImporter.GetAtPath(MukJumpSplashSceneBuilder.LogoPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void BootstrapKeepsBrandBackgroundButNeverDuplicatesNativeLogo()
        {
            host = new GameObject("StartupBrandSplashTest");
            StartupBrandSplash splash =
                host.AddComponent<StartupBrandSplash>();
            MethodInfo buildMethod = typeof(StartupBrandSplash).GetMethod(
                "BuildIfNeeded",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);
            buildMethod.Invoke(splash, null);

            Transform canvasRoot = host.transform.Find("StartupBrandCanvas");
            Assert.That(canvasRoot, Is.Not.Null);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            CanvasGroup rootGroup = canvasRoot.GetComponent<CanvasGroup>();
            Image backdrop = canvasRoot.Find("BlackBackdrop")
                ?.GetComponent<Image>();
            Image logo = canvasRoot.Find("Logo")?.GetComponent<Image>();
            CanvasGroup logoGroup = logo?.GetComponent<CanvasGroup>();

            Assert.That(canvas.renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(rootGroup.alpha, Is.EqualTo(1f));
            Assert.That(rootGroup.blocksRaycasts, Is.False);
            Assert.That(backdrop.color, Is.EqualTo(StartupBrandSplash.BackgroundColor));
            Assert.That(backdrop.raycastTarget, Is.True);
            Assert.That(logo, Is.Not.Null);
            Assert.That(logoGroup, Is.Not.Null);
            Assert.That(logoGroup.alpha, Is.Zero);
            Assert.That(logoGroup.blocksRaycasts, Is.False);
            Assert.That(logo.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(logo.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(1200f, 1200f)));
            Assert.That(logo.preserveAspect, Is.True);
            Assert.That(logo.enabled, Is.False);
            Assert.That(logo.raycastTarget, Is.False);
            Assert.That(canvasRoot.GetComponentsInChildren<Text>(true), Is.Empty);
        }

        [Test]
        public void ConfiguredCysBandLogoIsTheOnlyBrandElement()
        {
            host = new GameObject("StartupBrandSplashTest");
            StartupBrandSplash splash =
                host.AddComponent<StartupBrandSplash>();
            logoTexture = new Texture2D(2, 2);
            logoSprite = Sprite.Create(
                logoTexture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));

            splash.SetLogo(logoSprite);

            Transform canvasRoot = host.transform.Find("StartupBrandCanvas");
            Image logo = canvasRoot.Find("Logo").GetComponent<Image>();
            Assert.That(logo.sprite, Is.SameAs(logoSprite));
            Assert.That(logo.enabled, Is.False);
            Assert.That(canvasRoot.GetComponentsInChildren<Text>(true), Is.Empty);
        }

        [Test]
        public void StartupGoesDirectlyFromBrandToMainWithoutLoadingScreenOrExtraHold()
        {
            string source = File.ReadAllText("Assets/Scripts/Core/StartupBrandSplash.cs");
            Assert.That(source, Does.Not.Contain("StartupLoadingView"));
            Assert.That(source, Does.Not.Contain("LoadingMinimumDuration"));
            Assert.That(source, Does.Not.Contain("LobbyRevealDuration"));
            Assert.That(source, Does.Contain("Fade(rootGroup, 1f, 0f, FadeOutDuration)"));
            Assert.That(source, Does.Contain("SceneManager.LoadSceneAsync(NextSceneName"));
            Assert.That(File.Exists("Assets/Scripts/Core/StartupLoadingView.cs"), Is.False);
            string builder = File.ReadAllText("Assets/Editor/MukJumpSplashSceneBuilder.cs");
            Assert.That(builder, Does.Not.Contain("splash.ConfigureLoading"));
            string scene = File.ReadAllText(MukJumpSplashSceneBuilder.ScenePath);
            Assert.That(scene, Does.Not.Contain("StartupLoadingCanvas"));
            Assert.That(scene, Does.Not.Contain("835b1e1640f647a39d851d0afefaf1dc"));
        }

        [TestCase(GameLanguage.Korean, "게임을 불러오지 못했어요", "화면을 눌러 다시 시도")]
        [TestCase(GameLanguage.English, "Could not load the game", "Tap to retry")]
        public void OnlyRealFailureAddsLocalizedRetryOnExistingBrandCanvas(
            GameLanguage language, string expectedFailure, string expectedRetry)
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            try
            {
                GameLocalization.SetLanguage(language);
                host = new GameObject("StartupRetryTest");
                var splash = host.AddComponent<StartupBrandSplash>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(StartupBrandSplash).GetMethod("BuildIfNeeded", flags).Invoke(splash, null);
                Assert.That(host.GetComponentsInChildren<Text>(true), Is.Empty);
                for (int retry = 0; retry < 2; retry++)
                    typeof(StartupBrandSplash).GetMethod("ShowLoadFailure", flags).Invoke(splash, null);
                Assert.That(host.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
                Assert.That(host.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(2));
                Assert.That(host.transform.Find("StartupLoadingCanvas"), Is.Null);
                Transform brand = host.transform.Find("StartupBrandCanvas");
                Assert.That(brand.Find("LoadFailure").GetComponent<Text>().text, Is.EqualTo(expectedFailure));
                Assert.That(brand.Find("RetryHint").GetComponent<Text>().text, Is.EqualTo(expectedRetry));
                Assert.That(brand.GetComponent<Canvas>().sortingOrder, Is.EqualTo(30000));
            }
            finally
            {
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                InkLocalizedText.RefreshAll();
            }
        }

        [TestCase(false, false, false, false)]
        [TestCase(false, true, false, true)]
        [TestCase(true, false, false, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        public void NativeLogoReplaysOnlyForEditorOrAccountDeletion(
            bool editor, bool accountDeleted, bool batchMode, bool expected)
        {
            var replay = typeof(StartupBrandSplash).GetMethod("ShouldReplayNativeSplash",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(replay.Invoke(null, new object[] { editor, accountDeleted, batchMode }), Is.EqualTo(expected));
            Assert.That(StartupBrandSplash.NextSceneName, Is.EqualTo("Main"));
        }

        [Test]
        public void StoreBuildCannotTurnOffBrandSplashAgain()
        {
            string source = File.ReadAllText("Assets/Editor/MukJumpStoreBuild.cs");
            Assert.That(source, Does.Contain("MukJumpSplashSceneBuilder.ConfigureBrandPlayerSettings();"));
            Assert.That(source, Does.Not.Contain("PlayerSettings.SplashScreen.show = false"));
        }

        [Test]
        public void SplashFadeUsesClampedCubicEaseOut()
        {
            MethodInfo evaluateMethod = typeof(StartupBrandSplash).GetMethod(
                "EvaluateFadeProgress",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(evaluateMethod, Is.Not.Null);

            float start = (float)evaluateMethod.Invoke(null, new object[] { -1f });
            float midpoint = (float)evaluateMethod.Invoke(null, new object[] { 0.5f });
            float end = (float)evaluateMethod.Invoke(null, new object[] { 2f });

            Assert.That(start, Is.Zero);
            Assert.That(midpoint, Is.EqualTo(0.875f).Within(0.0001f));
            Assert.That(end, Is.EqualTo(1f));
        }

        [Test]
        public void SplashIsARealFirstSceneInsteadOfGlobalRuntimeOverlay()
        {
            bool hasGlobalBootstrap = typeof(StartupBrandSplash)
                .GetMethods(BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                .Any(method => method.GetCustomAttribute<
                    RuntimeInitializeOnLoadMethodAttribute>() != null);
            Assert.That(hasGlobalBootstrap, Is.False);

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[0].path,
                Is.EqualTo(MukJumpSplashSceneBuilder.ScenePath));
            Assert.That(
                scenes[0].guid.ToString(),
                Is.EqualTo(
                    AssetDatabase.AssetPathToGUID(
                        MukJumpSplashSceneBuilder.ScenePath)),
                "Splash Build Settings GUID는 현재 scene meta와 같아야 합니다.");
            Assert.That(scenes[1].enabled, Is.True);
            Assert.That(scenes[1].path,
                Is.EqualTo(MukJumpSplashSceneBuilder.MainScenePath));
            Assert.That(
                scenes[1].guid.ToString(),
                Is.EqualTo(
                    AssetDatabase.AssetPathToGUID(
                        MukJumpSplashSceneBuilder.MainScenePath)),
                "Main Build Settings GUID는 현재 scene meta와 같아야 합니다.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    MukJumpSplashSceneBuilder.ScenePath),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    MukJumpSplashSceneBuilder.LogoPath),
                Is.Not.Null);
            string splashSource = File.ReadAllText(
                MukJumpSplashSceneBuilder.ScenePath);
            string logoGuid = AssetDatabase.AssetPathToGUID(
                MukJumpSplashSceneBuilder.LogoPath);
            Assert.That(logoGuid, Is.Not.Empty);
            StringAssert.Contains(logoGuid, splashSource);
        }
    }
}
