using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using MukJump.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed partial class GoldPackagePreviewPlayTests
    {
        public const string Output = "output/qa/gold-package-preview-20260928";
        public const string AtlasPath = "Assets/Resources/MukJump/UI/GoldPreview/gold-jump.png";
        static string RecordingOutput => Environment.GetEnvironmentVariable("MUKJUMP_GOLD_RECORDING_OUTPUT") ?? Output;

        [UnityTest, Timeout(30000)]
        public IEnumerator GoldenContactStaysGoldBehindBodyAndRefundRestoresInk()
        {
            yield return new EnterPlayMode();
            using (var fixture = new UiMemoryFixture())
            {
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                LobbySettingsProfile.SetHapticsEnabled(false);
                fixture.Store.Owned = true;
                Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
                var host = new GameObject("Isolated landing feedback color test");
                var feedback = host.AddComponent<GameFeedbackController>();
                var previousTier = VfxQualityRuntime.Tier;
                VfxQualityRuntime.SetTier(VfxQualityTier.Medium, VfxQualityChangeReason.DebugOverride);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Landing() => typeof(GameFeedbackController).GetMethod("EmitLanding", flags)
                    .Invoke(feedback, new object[] { Vector3.zero, 12f });
                void Jump() => typeof(GameFeedbackController).GetMethod("EmitJump", flags)
                    .Invoke(feedback, new object[] { Vector3.zero, Vector2.up, false });
                void Check(Color expected)
                {
                    var rings = host.GetComponentsInChildren<LineRenderer>()
                        .Where(line => line.enabled && line.name == "FeedbackRing").ToArray();
                    bool gold = expected == GoldInkVisual.LandingTint;
                    bool doubleRing = gold && !LobbySettingsProfile.ReducedMotionEnabled && VfxQualityRuntime.Tier >= VfxQualityTier.Medium;
                    Assert.That(rings.Length, Is.EqualTo(doubleRing ? 2 : 1),
                        $"gold={gold}, reduced={LobbySettingsProfile.ReducedMotionEnabled}, tier={VfxQualityRuntime.Tier}");
                    foreach (var ring in rings)
                    {
                        Assert.That(ring.sortingOrder, Is.EqualTo(gold ? GoldInkVisual.ContactSortingOrder : 12));
                        Assert.That(ring.sharedMaterial, Is.SameAs(gold
                            ? MukJump.AI.FallbackInkStyle.SharedTintableBrushMaterial
                            : MukJump.AI.FallbackInkStyle.SharedInkMaterial));
                        var tint = ring.startColor;
                        Color target = gold && tint.g > .8f ? GoldInkVisual.LandingHighlight : expected;
                        // 렌더러의 Color32 양자화 오차(1/255)를 허용한다.
                        Assert.That(tint.r, Is.EqualTo(target.r).Within(1f / 255));
                        Assert.That(tint.g, Is.EqualTo(target.g).Within(1f / 255));
                        Assert.That(tint.b, Is.EqualTo(target.b).Within(1f / 255));
                    }
                    if (gold) Assert.That(rings.Max(ring => ring.startWidth), Is.GreaterThan(.18f));
                }
                void CheckJump(bool gold)
                {
                    var lines = host.GetComponentsInChildren<LineRenderer>()
                        .Where(line => line.enabled).ToArray();
                    Assert.That(lines.Any(line => line.name == "JumpBrushStreak"), Is.True);
                    foreach (var line in lines)
                    {
                        Color color = line.startColor;
                        if (gold)
                        {
                            Assert.That(color.r, Is.GreaterThan(.9f));
                            Assert.That(color.g, Is.GreaterThan(.6f));
                            Assert.That(color.b, Is.LessThan(.3f));
                            Assert.That(line.sortingOrder, Is.EqualTo(GoldInkVisual.ContactSortingOrder));
                            Assert.That(line.sortingOrder, Is.InRange(3, 4), "바닥 먹선 위·몸체 뒤");
                        }
                        else
                        {
                            Assert.That(color.r, Is.EqualTo(InkPalette.Ink.r).Within(1f / 255));
                            Assert.That(color.g, Is.EqualTo(InkPalette.Ink.g).Within(1f / 255));
                            Assert.That(color.b, Is.EqualTo(InkPalette.Ink.b).Within(1f / 255));
                            Assert.That(line.sortingOrder, Is.EqualTo(line.name == "JumpBrushStreak" ? 11 : 12));
                        }
                        Assert.That(line.sharedMaterial, Is.SameAs(gold
                            ? MukJump.AI.FallbackInkStyle.SharedTintableBrushMaterial
                            : MukJump.AI.FallbackInkStyle.SharedInkMaterial));
                    }
                }
                try
                {
                    var poolElements = host.GetComponentsInChildren<LineRenderer>(true);
                    yield return InGameFrame(() =>
                    {
                        Assert.That(InkPremiumAppearance.IsGoldInRun, Is.True);
                        Landing(); Check(GoldInkVisual.LandingTint);
                        // 입자 자산이 없는 폴백도 검은 방울로 돌아가지 않는다.
                        foreach (var drop in host.GetComponentsInChildren<SpriteRenderer>().Where(x => x.enabled))
                        {
                            Assert.That(drop.color.r, Is.EqualTo(GoldInkVisual.LandingTint.r).Within(.001f));
                            Assert.That(drop.sortingOrder, Is.EqualTo(GoldInkVisual.ContactSortingOrder));
                        }
                        Jump(); CheckJump(true);
                    });
                    yield return WaitFeedbackRelease(feedback);
                    foreach (var tier in new[] { VfxQualityTier.Low, VfxQualityTier.High })
                    {
                        VfxQualityRuntime.SetTier(tier, VfxQualityChangeReason.DebugOverride);
                        yield return InGameFrame(() => { Landing(); Check(GoldInkVisual.LandingTint); Jump(); CheckJump(true); });
                        yield return WaitFeedbackRelease(feedback);
                    }
                    fixture.Store.Owned = false;
                    yield return InGameFrame(() => { Landing(); Check(InkPalette.Ink); Jump(); CheckJump(false); });
                    yield return WaitFeedbackRelease(feedback);
                    fixture.Store.Owned = true;
                    yield return InGameFrame(() =>
                    {
                        Jump(); CheckJump(true);
                    });
                    yield return WaitFeedbackRelease(feedback);
                    LobbySettingsProfile.SetReducedMotionEnabled(true);
                    yield return InGameFrame(() => { Landing(); Check(GoldInkVisual.LandingTint); });
                    yield return WaitFeedbackRelease(feedback);
                    CollectionAssert.AreEquivalent(poolElements,
                        host.GetComponentsInChildren<LineRenderer>(true));
                    Assert.That(feedback.ActiveLineVfxCount + feedback.ActiveSpriteVfxCount, Is.Zero);
                }
                finally
                {
                    Object.Destroy(host);
                    VfxQualityRuntime.SetTier(previousTier, VfxQualityChangeReason.DebugOverride);
                }
            }
            yield return new ExitPlayMode();
        }

        static IEnumerator WaitFeedbackRelease(GameFeedbackController feedback)
        {
            // 에디터 검사 프레임과 실제 게임 프레임 속도가 달라도 이전 파동이 다음 검사에 섞이지 않는다.
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (feedback.ActiveLineVfxCount + feedback.ActiveSpriteVfxCount > 0 &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(feedback.ActiveLineVfxCount + feedback.ActiveSpriteVfxCount, Is.Zero,
                "착지 효과가 게임 시간으로 끝나고 풀에 반환되어야 한다");
        }

        // 녹화는 요청할 때만 실행한다. 실제 저장·구매 대신 메모리 공급자를 사용한다.
        [UnityTest, Explicit, Timeout(120000)]
        public IEnumerator RecordActualGoldenJump()
        {
            yield return new EnterPlayMode();
            Debug.Log("Gold recording: Play entered");
            var memory = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(memory); InkSkinPreferences.UseStoreForTests(memory);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            LobbySettingsProfile.SetReducedMotionEnabled(false);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            var store = new InkPremiumUiTests.Store { Owned = true };
            InkPremiumAppearance.UseForTests(true, store); SkinDebugSession.Reset();
            bool background = Application.runInBackground; Application.runInBackground = true;
            int captureRate = Time.captureFramerate;
            Debug.Log("Gold recording: memory stores ready");
            var size = new TemporaryGameViewSize(384, 512);
            Debug.Log("Gold recording: view configured");
            var run = new RecordingRun();
            var paths = new List<string>(); var evidence = new JArray();
            try
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance, Is.Not.Null, "Main account runtime");
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
                GameManager.Instance.StartGameFromMenu();
                double deadline = Time.realtimeSinceStartupAsDouble + 12;
                while ((GameManager.Instance.State != GameState.Playing || GameManager.Instance.IsTransitioning) &&
                    Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                GameManager.Instance.StartCoroutine(RecordRuntime(paths, evidence, run.Complete));
                deadline = Time.realtimeSinceStartupAsDouble + 60;
                while (!run.Completed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(run.Completed, Is.True); Assert.That(run.Failure, Is.Null, run.Failure?.ToString());
                Assert.That(paths.Count, Is.EqualTo(GoldInkRecordedPreview.FrameCount));
                foreach (string path in paths)
                {
                    deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(File.Exists(path), Is.True, path);
                }
                string directory = Path.GetFullPath(RecordingOutput + "/recorded-frames"); Directory.CreateDirectory(directory);
                for (int i = 0; i < paths.Count; i++) File.Copy(paths[i], directory + "/" + i.ToString("D3") + ".png", true);
                File.WriteAllText(RecordingOutput + "/recording.json", JsonConvert.SerializeObject(new
                {
                    source = "actual Unity Play Mode, production PlayerController/AutoJump/GoldInkVisual/InkContactParticles",
                    fixture = "memory stores, isolated flat ink platform and fixed camera, HUD hidden, no native store",
                    width = 384, height = 512, framesPerSecond = GoldInkRecordedPreview.FramesPerSecond,
                    store.Purchases, store.Restores, nativeStore = false, evidence
                }, Formatting.Indented));
                Assert.That(evidence.Max(x => x.Value<float>("velocityY")), Is.GreaterThan(3), "실제 자동 점프");
                Assert.That(evidence.Min(x => x.Value<float>("velocityY")), Is.LessThan(-3), "실제 낙하");
                Assert.That(evidence.Count(x => x.Value<int>("contactParticles") > 0), Is.GreaterThan(1), "실제 착지 입자");
                Assert.That(evidence.Count(x => x.Value<int>("goldLandingRings") > 0), Is.GreaterThan(0), "실제 금색 착지 파동");
                Assert.That(evidence.Sum(x => x.Value<int>("nonGoldContactParticles")), Is.Zero, "낙하→착지→재점프 전 구간의 검은 입자 혼입");
                Assert.That(evidence.Sum(x => x.Value<int>("contactLinesOverBody")), Is.Zero, "파동·점프 붓 선이 얼굴 앞으로 나오면 안 된다");
                Assert.That(evidence.Sum(x => x.Value<int>("nonGoldContactLines")), Is.Zero);
            }
            finally
            {
                Time.captureFramerate = captureRate; size.Dispose(); Application.runInBackground = background;
                InkPremiumAppearance.RestoreForTests(); MobileUiLayout.ClearPlatformSafeAreaOverride();
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
            PackRecording();
        }

        static IEnumerator RecordRuntime(List<string> paths, JArray evidence, Action<Exception> done)
        {
            Exception error = null;
            var player = Object.FindObjectsByType<PlayerController>().First(p => !p.IsRuntimeClone);
            var body = player.GetComponent<Rigidbody2D>();
            try
            {
                Assert.That(InkPremiumAppearance.IsGoldInRun, Is.True);
                foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>())
                    if (behaviour is CameraFollow || behaviour is PrototypeHud || behaviour is PlayerHealthBillboard ||
                        behaviour.GetType().Name == "RestPlatformSpawner" || behaviour.GetType().Name == "ObstacleSpawner")
                        behaviour.enabled = false;
                foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>())
                {
                    if (behaviour.GetType().Namespace == "MukJump.Obstacles" || behaviour is MukJump.Items.ItemSpawner)
                        behaviour.enabled = false;
                    if (behaviour is MukJump.Items.ItemPickup || behaviour is MukJump.Obstacles.Obstacle ||
                        behaviour is MukJump.Obstacles.FallingInkRock || behaviour is MukJump.Obstacles.HaetaeObstacle)
                        behaviour.gameObject.SetActive(false);
                }
                foreach (var other in Object.FindObjectsByType<PlayerController>())
                    if (other != player) other.gameObject.SetActive(false);
                HideRecordingHud();
                var camera = Camera.main; camera.orthographicSize = 3.7f; camera.transform.position = new Vector3(0, 3, -10);
                var floor = new GameObject("Recording only ink platform", typeof(LineRenderer), typeof(EdgeCollider2D));
                floor.layer = LayerMask.NameToLayer("Platform"); floor.GetComponent<LineRenderer>().useWorldSpace = false;
                floor.AddComponent<PlatformCollider>().ConfigurePermanentInkLine(new[] { new Vector2(-1.65f, 0), new Vector2(1.65f, 0) });
                body.position = new Vector2(0, .8f); body.linearVelocity = new Vector2(0, -2);
                Time.captureFramerate = GoldInkRecordedPreview.FramesPerSecond;
            }
            catch (Exception e) { error = e; }
            if (error != null) { done(error); yield break; }
            for (int i = 0; i < GoldInkRecordedPreview.FrameCount; i++)
            {
                yield return null;
                try
                {
                    HideRecordingHud();
                    Assert.That(Screen.width, Is.EqualTo(384)); Assert.That(Screen.height, Is.EqualTo(512));
                    paths.Add(JObject.FromObject(MukJumpAgentAudit.CaptureUi()).Value<string>("path"));
                    var feedback = GameFeedbackController.Instance;
                    var contactLines = feedback.GetComponentsInChildren<LineRenderer>()
                        .Where(line => line.enabled && (line.name == "FeedbackRing" || line.name == "JumpBrushStreak")).ToArray();
                    int nonGoldParticles = 0;
                    foreach (var system in feedback.GetComponentsInChildren<ParticleSystem>())
                    {
                        var particles = new ParticleSystem.Particle[system.main.maxParticles];
                        int count = system.GetParticles(particles);
                        for (int p = 0; p < count; p++)
                            if (!IsGoldContactColor(particles[p].startColor)) nonGoldParticles++;
                    }
                    evidence.Add(new JObject { ["frame"] = i, ["x"] = body.position.x, ["y"] = body.position.y,
                        ["velocityY"] = body.linearVelocity.y, ["gold"] = InkPremiumAppearance.IsGoldInRun,
                        ["contactParticles"] = feedback.ActiveContactParticleCount,
                        ["nonGoldContactParticles"] = nonGoldParticles,
                        ["contactLinesOverBody"] = contactLines.Count(line => line.sortingOrder >= player.GetComponent<SpriteRenderer>().sortingOrder),
                        ["nonGoldContactLines"] = contactLines.Count(line => !IsGoldContactColor(line.startColor)),
                        ["goldLandingRings"] = contactLines
                            .Count(line => line.name == "FeedbackRing" && line.enabled &&
                                line.startColor.r > .8f && line.startColor.g > .6f && line.startColor.b < .3f) });
                }
                catch (Exception e) { error = e; break; }
            }
            yield return null; done(error);
        }

        static bool IsGoldContactColor(Color color) => color.r > .9f && color.g > .6f && color.b < .3f;

        static void HideRecordingHud()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>()) canvas.enabled = false;
            foreach (var group in Object.FindObjectsByType<CanvasGroup>()) group.alpha = 0;
            foreach (var hud in Object.FindObjectsByType<PrototypeHud>()) hud.enabled = false;
            foreach (var health in Object.FindObjectsByType<PlayerHealthBillboard>()) health.enabled = false;
        }

        sealed class RecordingRun
        {
            public bool Completed;
            public Exception Failure;
            public void Complete(Exception failure) { Failure = failure; Completed = true; }
        }

        public static void PackRecording()
        {
            const int width = 192, height = 256;
            var atlas = new Texture2D(width * GoldInkRecordedPreview.Columns, height * GoldInkRecordedPreview.Rows, TextureFormat.RGB24, false);
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < GoldInkRecordedPreview.FrameCount; i++)
                {
                    source.LoadImage(File.ReadAllBytes(RecordingOutput + "/recorded-frames/" + i.ToString("D3") + ".png"));
                    Assert.That(source.width, Is.EqualTo(384)); Assert.That(source.height, Is.EqualTo(512));
                    var colors = new Color[width * height];
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        colors[y * width + x] = source.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
                    atlas.SetPixels(i % GoldInkRecordedPreview.Columns * width,
                        (GoldInkRecordedPreview.Rows - 1 - i / GoldInkRecordedPreview.Columns) * height, width, height, colors);
                }
                atlas.Apply(); Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
                File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG()); AssetDatabase.ImportAsset(AtlasPath);
                MukJumpSceneBuilder.ConfigureGoldPreviewRecording();
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(atlas); }
        }

        static IEnumerator Wait(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }

        sealed class TemporaryGameViewSize : IDisposable
        {
            readonly EditorWindow window; readonly object group; readonly int oldSize, index;
            static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            public TemporaryGameViewSize(int width, int height)
            {
                var assembly = typeof(Editor).Assembly;
                window = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
                oldSize = (int)window.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(window);
                var type = assembly.GetType("UnityEditor.GameViewSizes");
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
                group = type.GetProperty("currentGroup", Flags).GetValue(sizes);
                index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                var temporary = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                    Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "MukJump Gold Preview TEMP");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { temporary });
                window.GetType().GetMethod("SizeSelectionCallback", Flags).Invoke(window, new object[] { index, null });
                window.Focus(); window.Repaint();
            }
            public void Dispose()
            {
                window.GetType().GetMethod("SizeSelectionCallback", Flags).Invoke(window, new object[] { oldSize, null });
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
            }
        }
    }
}
