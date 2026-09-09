using System.Collections;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class AmbientCloudRuntimeTests
    {
        [UnityTest]
        public IEnumerator RealMapCrossfadeKeepsLatestThemeWhileMotionIsReduced()
        {
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            var host = new GameObject("MapAtmosphereRuntimeFixture");
            host.SetActive(false);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            var map = host.AddComponent<MapBackgroundView>();
            var themes = MukJumpSceneBuilder.BuildAmbientThemes();
            var current = MakeRenderer(host.transform, "Current");
            var next = MakeRenderer(host.transform, "Next");
            var clouds = new GameObject("Clouds");
            clouds.transform.SetParent(host.transform, false);
            var view = clouds.AddComponent<AmbientCloudView>();
            var layers = new SpriteRenderer[4];
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = MakeRenderer(host.transform, "Atmosphere_" + i);
                layers[i].transform.SetParent(clouds.transform, false);
                layers[i].sprite = themes[0].sprites[i % 3];
            }
            view.Configure(camera, layers, themes);
            SetMap(map, "worldCamera", camera);
            SetMap(map, "currentRenderer", current);
            SetMap(map, "nextRenderer", next);
            SetMap(map, "stageSprites", themes.Take(4).Select(theme => theme.background).ToArray());
            SetMap(map, "endlessStageSprites", themes.Skip(4).Select(theme => theme.background).ToArray());
            FieldInfo clock = typeof(AmbientCloudView).GetField("elapsedSeconds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            SetMap(map, "ambientClouds", view);
            bool settled = false, frozen = false, immediateReset = false;
            try
            {
                host.SetActive(true);
                yield return null;
                double before = (double)clock.GetValue(view);
                map.SetStage(1, false);
                yield return new WaitForSecondsRealtime(0.2f);
                map.SetStage(2, false);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < deadline &&
                       ((int)GetMap(map, "currentStage") != 2 || view.CurrentThemeIndex != 2 ||
                        layers[0].color.a < 0.35f))
                    yield return null;
                settled = ((SpriteRenderer)GetMap(map, "currentRenderer")).sprite == themes[2].background &&
                          view.CurrentMotion == AmbientCloudMotion.RainMist;
                foreach (SpriteRenderer layer in layers)
                    settled &= layer.sprite.texture == themes[2].sprites[0].texture;
                frozen = before == (double)clock.GetValue(view);
                map.SetStage(1, false);
                yield return null;
                map.SetStage(0, true);
                host.SetActive(false);
                host.SetActive(true);
                immediateReset = (int)GetMap(map, "currentStage") == 0 &&
                                 view.CurrentThemeIndex == 0 &&
                                 ((SpriteRenderer)GetMap(map, "currentRenderer")).sprite == themes[0].background;
            }
            finally
            {
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                Object.Destroy(host);
            }
            yield return new ExitPlayMode();
            Application.runInBackground = previousBackground;
            Assert.That(settled, Is.True, "실제 배경 코루틴과 테마가 가장 최근 요청에 함께 도달해야 합니다.");
            Assert.That(frozen, Is.True, "움직임 줄이기는 이동만 멈추고 맵 교체는 완료해야 합니다.");
            Assert.That(immediateReset, Is.True, "즉시 복귀 후 비활성화가 이전 맵을 되살리면 안 됩니다.");
        }

        // EnterPlayMode의 도메인 재로드에서 캡처 클로저가 유실되지 않도록 정적 헬퍼를 사용합니다.
        static SpriteRenderer MakeRenderer(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<SpriteRenderer>();
        }

        static void SetMap(MapBackgroundView map, string name, object value) => typeof(MapBackgroundView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(map, value);

        static object GetMap(MapBackgroundView map, string name) => typeof(MapBackgroundView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);

        static IEnumerator WaitForRuntimeFrames()
        {
            // EditMode 러너의 yield null은 에디터 틱이며 실제 LateUpdate 한 번과
            // 일대일 대응하지 않는다. 복귀/정지 모두 렌더 프레임 경계를 관찰한다.
            int targetFrame = Time.frameCount + 2;
            double deadline = EditorApplication.timeSinceStartup + 3d;
            while (Time.frameCount < targetFrame && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(targetFrame),
                "Play 모드의 실제 프레임이 진행되지 않아 배경 동작을 검증할 수 없습니다.");
        }

        [UnityTest]
        public IEnumerator RealLateUpdateFreezesAndResumesWithRuntimeSettings()
        {
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var host = new GameObject("AmbientCloudRuntimeFixture");
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 9f / 16f;
            camera.orthographicSize = 9.6f;
            var child = new GameObject("Clouds");
            child.transform.SetParent(host.transform, false);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(MukJumpSceneBuilder.AmbientCloudAtlasPath)
                .OfType<Sprite>().ToArray();
            var layers = new SpriteRenderer[4];
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = new GameObject("Layer_" + i);
                layer.transform.SetParent(child.transform, false);
                layers[i] = layer.AddComponent<SpriteRenderer>();
                layers[i].sprite = sprites[i % sprites.Length];
            }
            var view = child.AddComponent<AmbientCloudView>();
            view.Configure(camera, layers);
            FieldInfo clock = typeof(AmbientCloudView).GetField(
                "elapsedSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            bool advanced, reducedFrozen, hiddenFrozen, resumed;
            string resumeState = string.Empty;
            try
            {
                yield return WaitForRuntimeFrames();
                double moving = (double)clock.GetValue(view);
                advanced = moving > 0d;
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                yield return WaitForRuntimeFrames();
                reducedFrozen = (double)clock.GetValue(view) == moving;
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                yield return WaitForRuntimeFrames();
                hiddenFrozen = (double)clock.GetValue(view) == moving;
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                yield return WaitForRuntimeFrames();
                resumed = (double)clock.GetValue(view) > moving;
                var game = GameManager.Instance;
                resumeState = $"active={MobileApplicationLifecycle.IsApplicationActive}, " +
                    $"state={game?.State}, pause={game?.PauseReason}, transition={game?.IsTransitioning}, " +
                    $"reduced={LobbySettingsProfile.ReducedMotionEnabled}, delta={Time.unscaledDeltaTime}, " +
                    $"clock={moving}->{clock.GetValue(view)}";
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                Object.Destroy(host);
            }
            yield return new ExitPlayMode();
            Application.runInBackground = previousBackground;
            Assert.That(advanced, Is.True, "로비에서도 실제 LateUpdate가 구름 시간을 진행해야 합니다.");
            Assert.That(reducedFrozen, Is.True, "움직임 줄이기를 켜면 그 프레임부터 멈춰야 합니다.");
            Assert.That(hiddenFrozen, Is.True, "플랫폼이 숨겨지면 움직임을 중단해야 합니다.");
            Assert.That(resumed, Is.True, "복귀 후 기존 구름 위치에서 이어가야 합니다. " + resumeState);
        }
    }
}
