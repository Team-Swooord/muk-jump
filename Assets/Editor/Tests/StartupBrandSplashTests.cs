using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
            if (host != null)
                Object.DestroyImmediate(host);
            if (logoSprite != null)
                Object.DestroyImmediate(logoSprite);
            if (logoTexture != null)
                Object.DestroyImmediate(logoTexture);
        }

        [Test]
        public void UnitySplashIsDisabledInPlayerSettings()
        {
            string settings = File.ReadAllText(
                "ProjectSettings/ProjectSettings.asset");
            StringAssert.Contains("m_ShowUnitySplashScreen: 0", settings);
            StringAssert.Contains("m_ShowUnitySplashLogo: 0", settings);
        }

        [Test]
        public void CustomSplashKeepsBlackCanvasVisibleAndFadesOnlyLogo()
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
            Assert.That(backdrop.color, Is.EqualTo(Color.black));
            Assert.That(backdrop.raycastTarget, Is.True);
            Assert.That(logo, Is.Not.Null);
            Assert.That(logoGroup, Is.Not.Null);
            Assert.That(logoGroup.alpha, Is.Zero);
            Assert.That(logoGroup.blocksRaycasts, Is.False);
            Assert.That(logo.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(logo.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(1200f, 1200f)));
            Assert.That(logo.preserveAspect, Is.False);
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
        public void SplashUsesShortFadeTimelineAndLoadsMain()
        {
            Assert.That(StartupBrandSplash.InitialDelay, Is.EqualTo(0.1f));
            Assert.That(StartupBrandSplash.FadeInDuration, Is.EqualTo(0.35f));
            Assert.That(StartupBrandSplash.HoldDuration, Is.EqualTo(0.8f));
            Assert.That(StartupBrandSplash.FadeOutDuration, Is.EqualTo(0.3f));
            Assert.That(
                StartupBrandSplash.InitialDelay +
                StartupBrandSplash.FadeInDuration +
                StartupBrandSplash.HoldDuration +
                StartupBrandSplash.FadeOutDuration,
                Is.EqualTo(1.55f).Within(0.001f));
            Assert.That(StartupBrandSplash.NextSceneName, Is.EqualTo("Main"));
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
