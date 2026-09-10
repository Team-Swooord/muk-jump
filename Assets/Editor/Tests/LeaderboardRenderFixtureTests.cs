using System;
using System.Collections;
using System.IO;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTools
{
    public sealed class LeaderboardRenderFixtureTests
    {
        [UnityTest]
        [Ignore("격리 미완료: BeforeSceneLoad 계정 Bootstrap을 Play 진입 전에 차단하는 검증된 테스트 경로가 필요합니다. UI 렌더 증거로 사용 금지.")]
        public IEnumerator RenderIsolatedLeaderboardWithoutNetworkOrSceneWrites()
        {
            yield return new EnterPlayMode();
            string failure = null;
            try { RenderFixture(); }
            catch (Exception error) { failure = error.ToString(); }
            yield return new ExitPlayMode();
            Assert.That(failure, Is.Null);
        }

        static void RenderFixture()
        {
            Assert.That(MukJumpAccountRuntime.Instance, Is.Null, "실제 계정이 있으면 fixture를 실행하지 않습니다.");
            Scene fixtureScene = default;
            Scene originalScene = SceneManager.GetActiveScene();
            bool originalDirty = originalScene.isDirty;
            GameObject host = null;
            GameObject cameraHost = null;
            Texture2D readback = null;
            RenderTexture texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Test Runner의 미저장 임시 씬을 저장하거나 버리지 않고 빈 씬만 추가한다.
                fixtureScene = SceneManager.CreateScene("IsolatedLeaderboardRender-" + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(fixtureScene);
                LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
                PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
                const int width = 1170, height = 2532;
                host = new GameObject("IsolatedLeaderboardRender");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, fixtureScene);
                var view = host.AddComponent<LobbyOptionsView>();
                view.enabled = false;
                view.SetDisplayMetricsForTests(width, height, new Rect(0, 102, width, height - 243));
                view.BuildForPlatformForTests(RuntimePlatform.IPhonePlayer);
                // Awake는 화면을 닫으므로 먼저 활성화한 뒤 표시 상태만 설정한다.
                host.SetActive(true);
                var canvas = host.GetComponentInChildren<Canvas>(true);
                var panel = canvas.transform.Find("SafeAreaRoot/OptionsScroll");
                foreach (Transform child in panel)
                {
                    var group = child.GetComponent<CanvasGroup>();
                    if (group == null || !child.name.EndsWith("Page")) continue;
                    bool visible = child.name == "LeaderboardPage";
                    group.alpha = visible ? 1 : 0;
                    group.interactable = visible;
                    group.blocksRaycasts = visible;
                }
                foreach (var frame in host.GetComponentsInChildren<HanjiScrollFrame>(true)) frame.SetPose(1, 0, false);
                canvas.GetComponent<CanvasGroup>().alpha = 1;
                panel.GetComponent<CanvasGroup>().alpha = 1;
                panel.Find("HanjiScrollArt").GetComponent<CanvasGroup>().alpha = 1;
                var ranking = panel.Find("LeaderboardPage");
                typeof(LobbyOptionsView).GetMethod("SetPageVisible",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { ranking.GetComponent<CanvasGroup>(), true });
                string[] names = { "WWWWWW", "가나다라마바…", "이름 없는 먹방울", "먹방울", "👨‍👩‍👧‍👦먹방울" };
                for (int i = 1; i <= 10; i++)
                {
                    LobbyOptionsView.FitLeaderboardName(ranking.Find($"NameCell{i}/Name{i}").GetComponent<Text>(), names[(i - 1) % names.Length]);
                    ranking.Find($"LeaderboardRow{i}").GetComponent<Text>().text = $"{12000 - i * 132:N0} m";
                    ranking.Find($"Source{i}").GetComponent<Text>().text = "뒤끝";
                    ranking.Find($"Rank{i}").GetComponent<Text>().text = i.ToString();
                    ranking.Find($"SourceSeal{i}").GetComponent<Image>().enabled = true;
                }
                foreach (Transform node in host.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                texture.Create();
                cameraHost = new GameObject("IsolatedGameCamera");
                SceneManager.MoveGameObjectToScene(cameraHost, fixtureScene);
                var camera = cameraHost.AddComponent<Camera>();
                SceneManager.SetActiveScene(originalScene);
                camera.enabled = false;
                Assert.That(camera.cameraType, Is.EqualTo(CameraType.Game));
                camera.targetTexture = texture;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = InkPalette.Paper;
                camera.orthographic = true;
                camera.orthographicSize = 960;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.transform.rotation = Quaternion.identity;
                // Preview camera의 screen-space UI 경로를 우회하되, 같은 1920 기준
                // 논리 크기와 앞서 주입한 safe-area 배치를 그대로 사용한다.
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(width * 1920f / height, 1920f);
                canvasRect.position = Vector3.zero;
                canvasRect.rotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;
                canvas.enabled = false;
                canvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                Assert.That(canvas.isActiveAndEnabled, Is.True);
                Assert.That(((RectTransform)canvas.transform).rect.width, Is.GreaterThan(0));
                Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                camera.Render();
                RenderTexture.active = texture;
                readback = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                string folder = Path.Combine("output/release-qa/2026-09-06", "render-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "leaderboard-fixture.png"), readback.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "manifest.txt"),
                    $"Synthetic isolated Unity render; WorldSpace projection of injected layout, not device/server evidence.\nViewport={width}x{height}\nActualTexture={texture.width}x{texture.height}\nSafeArea=0,102,1170,2289\nUTC={DateTime.UtcNow:O}\nEditorDll={File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll"):O}\n");
                Assert.That(texture.width, Is.EqualTo(width));
                Assert.That(texture.height, Is.EqualTo(height));
                int inkPixels = 0;
                foreach (Color32 pixel in readback.GetPixels32())
                    if (pixel.r < 100 && pixel.g < 100 && pixel.b < 100) inkPixels++;
                Assert.That(inkPixels, Is.GreaterThan(width * height / 1000), "먹색 UI가 없는 빈 캡처는 증거가 아닙니다.");
                Assert.That(MukJumpAccountRuntime.Instance, Is.Null);
                Assert.That(originalScene.isDirty, Is.EqualTo(originalDirty), "원래 씬 dirty 상태를 바꾸면 안 됩니다.");
            }
            finally
            {
                try
                {
                    RenderTexture.active = previous;
                    if (readback != null) Object.DestroyImmediate(readback);
                    if (host != null) Object.DestroyImmediate(host);
                    if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                    if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                    if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                    if (fixtureScene.IsValid()) EditorSceneManager.CloseScene(fixtureScene, true);
                }
                finally
                {
                    try { PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
                    finally { LobbySettingsProfile.RestoreDefaultStoreForTests(); }
                }
            }
        }
    }
}
