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
            var restart = typeof(StartupBrandSplash).GetMethod("TryRestartForNewGuest",
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
            var restart = typeof(StartupBrandSplash).GetMethod("TryRestartForNewGuest",
                BindingFlags.Static | BindingFlags.NonPublic);
            LogAssert.Expect(LogType.Warning,
                "[MukJump] 새 게스트 시작 화면 복귀 실패: scene unavailable");
            Assert.That(restart.Invoke(null, null), Is.False);
            Assert.That(StartupBrandSplash.IsBlockingInput, Is.False);
            hook.SetValue(null, new System.Action<string>(_ => { }));
            Assert.That(restart.Invoke(null, null), Is.True);
        }

        [Test]
        public void DestroyedPreviousTutorialDoesNotBlockTheBrandRestart()
        {
            var property = typeof(FirstRunTutorialController).GetProperty("Instance");
            var previous = FirstRunTutorialController.Instance;
            var deletedHost = new GameObject("DeletedTutorialOwner");
            deletedHost.SetActive(false);
            var deleted = deletedHost.AddComponent<FirstRunTutorialController>();
            Object.DestroyImmediate(deletedHost);
            property.SetValue(null, deleted);
            try
            {
                int calls = 0;
                typeof(StartupBrandSplash).GetField("restartSceneForTests", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, new System.Action<string>(_ => calls++));
                var restart = typeof(StartupBrandSplash).GetMethod("TryRestartForNewGuest",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(restart.Invoke(null, null), Is.True);
                Assert.That(calls, Is.EqualTo(1));
            }
            finally { property.SetValue(null, previous); }
        }

        [Test]
        public void NativeSplashNeverDuplicatesTheAnimatedBrandScene()
        {
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
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
            Assert.That(logos, Is.Empty);
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
        public void BrandStartsTransparentAtTheOriginalShiftSceneSize()
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
            Assert.That(rootGroup.blocksRaycasts, Is.True);
            Assert.That(backdrop.color, Is.EqualTo(StartupBrandSplash.BackgroundColor));
            Assert.That(backdrop.raycastTarget, Is.True);
            Assert.That(logo, Is.Not.Null);
            Assert.That(logoGroup, Is.Not.Null);
            Assert.That(logoGroup.alpha, Is.EqualTo(1f));
            Assert.That(logo.color.a, Is.Zero);
            Assert.That(logoGroup.blocksRaycasts, Is.False);
            Assert.That(logo.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(logo.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(396.6099f, 396.6099f)));
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960f, 540f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
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
            Assert.That(logo.enabled, Is.True);
            Assert.That(logo.color.a, Is.Zero);
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

        [Test]
        public void ColdStartAndDeletionUseTheSameRealAnimationWithoutNativeDrawCalls()
        {
            string source = File.ReadAllText("Assets/Scripts/Core/StartupBrandSplash.cs");
            Assert.That(source, Does.Not.Contain("UnityEngine.Rendering.SplashScreen"));
            Assert.That(source, Does.Not.Contain("Application.isBatchMode"));
            Assert.That(source, Does.Contain("yield return AnimateLogo(false)"));
            Assert.That(source, Does.Contain("yield return AnimateLogo(true)"));
            Assert.That(StartupBrandSplash.NextSceneName, Is.EqualTo("Main"));
        }

        [TestCase(0f, 0f)]
        [TestCase(.25f, .15625f)]
        [TestCase(.5f, .5f)]
        [TestCase(.75f, .84375f)]
        [TestCase(1f, 1f)]
        public void OriginalShiftClipIsActuallyAppliedToTheLogo(float time, float alpha)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MukJumpSplashSceneBuilder.LogoFadePath);
            Assert.That(clip, Is.Not.Null);
            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = System.BitConverter.ToString(sha.ComputeHash(
                File.ReadAllBytes(MukJumpSplashSceneBuilder.LogoFadePath))).Replace("-", "").ToLowerInvariant();
            Assert.That(hash, Is.EqualTo("bf379cfcf2cab03d5c5c5bac68c6f6c181c33b11e2cf5b10d9ec16f9ac74072a"));
            Assert.That(clip.length, Is.EqualTo(1f));
            host = new GameObject("OriginalBrandCurveTest");
            var splash = host.AddComponent<StartupBrandSplash>();
            splash.SetLogo(AssetDatabase.LoadAssetAtPath<Sprite>(MukJumpSplashSceneBuilder.LogoPath),
                MukJumpSplashSceneBuilder.ReadLogoFadeCurve(clip));
            var sample = typeof(StartupBrandSplash).GetMethod("SampleLogo", BindingFlags.Instance | BindingFlags.NonPublic);
            var logo = host.transform.Find("StartupBrandCanvas/Logo").GetComponent<Image>();
            sample.Invoke(splash, new object[] { time });
            Assert.That(logo.color.a, Is.EqualTo(alpha).Within(.0001f));
            sample.Invoke(splash, new object[] { 1f - time });
            Assert.That(logo.color.a, Is.EqualTo(1f - alpha).Within(.0001f), "퇴장도 같은 곡선을 역방향으로 쓴다.");
            Assert.That(logo.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void EveryLogoSampleInvalidatesTheRenderedImageWithoutInspectorRefresh()
        {
            host = new GameObject("BrandRenderRefreshTest");
            var splash = host.AddComponent<StartupBrandSplash>();
            splash.SetLogo(AssetDatabase.LoadAssetAtPath<Sprite>(MukJumpSplashSceneBuilder.LogoPath),
                MukJumpSplashSceneBuilder.ReadLogoFadeCurve(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(MukJumpSplashSceneBuilder.LogoFadePath)));
            var logo = host.transform.Find("StartupBrandCanvas/Logo").GetComponent<UnityEngine.UI.Image>();
            logo.Rebuild(CanvasUpdate.PreRender);
            int refreshes = 0;
            logo.RegisterDirtyVerticesCallback(() => refreshes++);
            typeof(StartupBrandSplash).GetMethod("SampleLogo", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(splash, new object[] { .5f });
            Assert.That(logo.color.a, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(refreshes, Is.GreaterThan(0),
                "알파 필드뿐 아니라 실제 Canvas 정점도 다시 그려야 한다.");
        }

        [Test]
        public void PortableCurveMatchesOriginalClipAtEveryFrameInBothDirections()
        {
            host = new GameObject("PortableBrandCurveTest");
            var splash = host.AddComponent<StartupBrandSplash>();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MukJumpSplashSceneBuilder.LogoFadePath);
            splash.SetLogo(AssetDatabase.LoadAssetAtPath<Sprite>(MukJumpSplashSceneBuilder.LogoPath),
                MukJumpSplashSceneBuilder.ReadLogoFadeCurve(clip));
            var logo = host.transform.Find("StartupBrandCanvas/Logo").GetComponent<UnityEngine.UI.Image>();
            var sample = typeof(StartupBrandSplash).GetMethod("SampleLogo", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int step = 0; step <= 200; step++)
            {
                float time = step <= 100 ? step / 100f : (200 - step) / 100f;
                clip.SampleAnimation(logo.gameObject, time);
                float sourceAlpha = logo.color.a;
                logo.color = Color.clear;
                sample.Invoke(splash, new object[] { time });
                Assert.That(logo.color.a, Is.EqualTo(sourceAlpha).Within(.0001f), $"frame={step}");
            }
            string source = File.ReadAllText("Assets/Scripts/Core/StartupBrandSplash.cs");
            Assert.That(source, Does.Not.Contain(".SampleAnimation("),
                "Player는 클립의 직렬화 필드 바인딩 없이 표시를 갱신해야 한다.");
        }

        [Test]
        public void SavedSplashContainsThePortableCurveAndRealLogo()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(MukJumpSplashSceneBuilder.ScenePath);
            try
            {
                var splash = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StartupBrandSplash>(true)).Single();
                var logo = splash.transform.Find("StartupBrandCanvas/Logo").GetComponent<UnityEngine.UI.Image>();
                Assert.That(logo.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(MukJumpSplashSceneBuilder.LogoPath)));
                var serialized = new SerializedObject(splash);
                var curve = serialized.FindProperty("logoFadeCurve").animationCurveValue;
                var original = MukJumpSplashSceneBuilder.ReadLogoFadeCurve(
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(MukJumpSplashSceneBuilder.LogoFadePath));
                for (int step = 0; step <= 100; step++)
                    Assert.That(curve.Evaluate(step / 100f), Is.EqualTo(original.Evaluate(step / 100f)).Within(.0001f));
                Assert.That(File.ReadAllText(MukJumpSplashSceneBuilder.ScenePath), Does.Contain("logoFadeCurve:"));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(2f, 5f, true, 2.0333333f)]
        [TestCase(2f, 5f, false, 2f)]
        [TestCase(2f, .016f, true, 2.016f)]
        [TestCase(2f, -1f, true, 2f)]
        public void LoadingHitchesAndBackgroundCannotSkipTheFade(float elapsed, float delta, bool active, float expected)
        {
            var advance = typeof(StartupBrandSplash).GetMethod("AdvancePresentationTime",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((float)advance.Invoke(null, new object[] { elapsed, delta, active }),
                Is.EqualTo(expected).Within(.00001f));
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
