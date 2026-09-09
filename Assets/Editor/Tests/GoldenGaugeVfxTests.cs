using System;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class GoldenGaugeVfxTests
    {
        GoldenGaugeVfx vfx;
        Shader shader;

        [SetUp]
        public void SetUp()
        {
            shader = Resources.Load<Shader>("MukJump/Shaders/GoldenGaugeParticle");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(shader.isSupported, Is.True);
            vfx = new GoldenGaugeVfx(shader);
        }

        [TearDown]
        public void TearDown() => vfx.Dispose();

        [TestCase(VfxQualityTier.Low, 24, 16)]
        [TestCase(VfxQualityTier.Medium, 44, 28)]
        [TestCase(VfxQualityTier.High, 64, 40)]
        public void BurstAndSustainRemainInsideQualityBudget(VfxQualityTier tier, int limit, int burst)
        {
            vfx.Advance(true, 1, 0f, tier, false);
            Assert.That(vfx.ActiveCount, Is.EqualTo(burst));
            Assert.That(vfx.BurstCount, Is.EqualTo(1));
            Assert.That(vfx.ParticleLimit, Is.EqualTo(limit));
            for (uint frame = 1; frame <= 1800; frame++)
            {
                vfx.Advance(true, frame / 24 + 1, 1f / 60f, tier, false);
                Assert.That(vfx.ActiveCount, Is.InRange(1, limit));
            }
        }

        [Test]
        public void ReacquisitionIsCoalescedAndExpirationHasShortTail()
        {
            vfx.Advance(true, 1, .016f, VfxQualityTier.High, false);
            vfx.Advance(true, 2, .016f, VfxQualityTier.High, false);
            Assert.That(vfx.BurstCount, Is.EqualTo(1));
            for (int i = 0; i < 60; i++) vfx.Advance(true, 2, 1f / 60, VfxQualityTier.High, false);
            vfx.Advance(true, 3, .016f, VfxQualityTier.High, false);
            Assert.That(vfx.BurstCount, Is.EqualTo(2));
            for (int i = 0; i < 18; i++) vfx.Advance(false, 3, 1f / 60, VfxQualityTier.High, false);
            Assert.That(vfx.ActiveCount, Is.Zero);
            Assert.That(Field<float>(vfx, "fade"), Is.Zero);
            vfx.Advance(true, 4, .016f, VfxQualityTier.High, false);
            vfx.Clear();
            Assert.That(vfx.ActiveCount, Is.Zero);
            Assert.That(Field<float>(vfx, "fade"), Is.Zero);
        }

        [Test]
        public void QualityDowngradeAndReducedMotionClearExcessParticles()
        {
            vfx.Advance(true, 1, .016f, VfxQualityTier.High, false);
            vfx.Advance(true, 1, .016f, VfxQualityTier.Low, false);
            Assert.That(vfx.ActiveCount, Is.LessThanOrEqualTo(24));
            vfx.Advance(true, 1, .016f, VfxQualityTier.High, true);
            Assert.That(vfx.ActiveCount, Is.Zero);
            Assert.That(Field<float>(vfx, "fade"), Is.EqualTo(1f));
            Assert.That(Field<float>(vfx, "burstAge"), Is.GreaterThan(1f));
        }

        [Test]
        public void ResumeDeltaIsBoundedAndDoesNotAccumulateMissedEmission()
        {
            vfx.Advance(true, 1, .016f, VfxQualityTier.High, false);
            float age = Field<float>(vfx, "burstAge");
            vfx.Advance(true, 1, 30f, VfxQualityTier.High, false);
            Assert.That(Field<float>(vfx, "burstAge") - age, Is.EqualTo(.05f).Within(.001f));
            Assert.That(vfx.ActiveCount, Is.LessThanOrEqualTo(41));
            Assert.That(vfx.BurstCount, Is.EqualTo(1));
        }

        [Test]
        public void SimulationDoesNotAllocateOrAffectGameplayRandom()
        {
            UnityEngine.Random.State previous = UnityEngine.Random.state;
            vfx.Advance(true, 1, .016f, VfxQualityTier.High, false);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                vfx.Advance(true, (uint)(i / 60 + 1), 1f / 60, VfxQualityTier.High, false);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(previous));
        }

        [Test]
        public void HudPauseAndSameRunReviveDoNotRestartBurst()
        {
            var root = new GameObject("GoldenGaugeLifecycle");
            var captureRoot = new GameObject("GoldenGaugeCapture");
            try
            {
                var manager = root.AddComponent<GameManager>();
                Invoke(manager, "OnEnable");
                Invoke(manager, "SetState", GameState.Playing);
                var capture = captureRoot.AddComponent<StrokeCapture>();
                var hud = root.AddComponent<PrototypeHud>();
                Invoke(hud, "OnEnable");
                typeof(PrototypeHud).GetField("strokeCapture", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hud, capture);
                capture.ActivateUnlimitedInk(8);
                Invoke(hud, "Update");
                var effect = Field<GoldenGaugeVfx>(hud, "goldenGaugeVfx");
                Assert.That(effect.BurstCount, Is.EqualTo(1));
                int count = effect.ActiveCount;
                float age = Field<float>(effect, "burstAge");
                Assert.That(manager.PauseGame(), Is.True);
                for (int i = 0; i < 30; i++) Invoke(hud, "Update");
                Assert.That(effect.ActiveCount, Is.EqualTo(count));
                Assert.That(Field<float>(effect, "burstAge"), Is.EqualTo(age));
                Invoke(manager, "SetState", GameState.GameOver);
                Invoke(hud, "Update");
                Assert.That(effect.ActiveCount, Is.EqualTo(count));
                Invoke(manager, "SetState", GameState.Playing);
                manager.ResumeGame();
                Invoke(hud, "Update");
                Assert.That(effect.BurstCount, Is.EqualTo(1));
                Invoke(manager, "SetState", GameState.Lobby);
                Assert.That(effect.ActiveCount, Is.Zero);
                Invoke(hud, "OnDisable");
                Assert.That(Field<GoldenGaugeVfx>(hud, "goldenGaugeVfx"), Is.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(captureRoot); }
        }

        [Test]
        public void ActivationRevisionIncludesSameTimePickupButRejectsInvalidDuration()
        {
            var go = new GameObject("GoldenGaugeActivationRevision");
            try
            {
                var capture = go.AddComponent<StrokeCapture>();
                capture.ActivateUnlimitedInk(8);
                uint first = capture.UnlimitedInkActivationRevision;
                capture.ActivateUnlimitedInk(8);
                Assert.That(capture.UnlimitedInkActivationRevision, Is.EqualTo(first + 1));
                capture.ActivateUnlimitedInk(4);
                Assert.That(capture.UnlimitedInkActivationRevision, Is.EqualTo(first + 2));
                Assert.That(capture.UnlimitedInkRemainingSeconds, Is.EqualTo(8));
                capture.ActivateUnlimitedInk(0);
                capture.ActivateUnlimitedInk(float.NaN);
                capture.ActivateUnlimitedInk(float.PositiveInfinity);
                Assert.That(capture.UnlimitedInkActivationRevision, Is.EqualTo(first + 2));
                Assert.That(capture.UnlimitedInkRemainingSeconds, Is.EqualTo(8));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DisposeReleasesMeshAndMaterialAndIsRepeatSafe()
        {
            var mesh = Field<Mesh>(vfx, "mesh");
            var material = Field<Material>(vfx, "material");
            vfx.Dispose();
            vfx.Dispose();
            Assert.That(mesh == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(vfx.ActiveCount, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RenderProjectionKeepsParticlesAtTheirGuiPixelPositions(bool behind)
        {
            // 검은 RT에서 게이지 원화 없이 검사해 금색 막대만으로 통과하거나 Y가 뒤집히지 않게 한다.
            var target = RenderTexture.GetTemporary(400, 700, 0);
            var previous = RenderTexture.active;
            var capture = new Texture2D(400, 700, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < 12; i++) vfx.Advance(true, 1u, .03f, VfxQualityTier.High, false);
                RenderTexture.active = target;
                GL.Clear(true, true, Color.black);
                vfx.Draw(new Rect(30, 580, 330, 60), new Rect(0, 0, 400, 700),
                    new Vector2(400, 700), behind);
                capture.ReadPixels(new Rect(0, 0, 400, 700), 0, 0);
                capture.Apply();
                Mesh mesh = Field<Mesh>(vfx, "mesh");
                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;
                int tested = 0;
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    if (colors[i].a < .05f) continue;
                    Vector3 center = (vertices[i] + vertices[i + 2]) * .5f;
                    int x = Mathf.RoundToInt(center.x), y = Mathf.RoundToInt(700 - center.y);
                    float brightest = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            brightest = Mathf.Max(brightest, capture.GetPixel(x + dx, y + dy).g);
                    Assert.That(brightest, Is.GreaterThan(.03f), $"GUI 입자 중심 {center}에 실제 빛이 있어야 합니다.");
                    tested++;
                }
                Assert.That(tested, Is.GreaterThan(0));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(capture);
            }
        }

        [TestCase(VfxQualityTier.Low)]
        [TestCase(VfxQualityTier.Medium)]
        [TestCase(VfxQualityTier.High)]
        public void RenderGoldenGaugeSequence(VfxQualityTier tier)
        {
            var fill = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/muk_gauge_fill.png");
            var brush = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/muk_brush_icon.png");
            Assert.That(fill, Is.Not.Null);
            Assert.That(brush, Is.Not.Null);
            var tint = new Material(Resources.Load<Shader>("MukJump/Shaders/InkGaugeTint"));
            tint.SetColor("_InkColor", InkPalette.Gold);
            var target = RenderTexture.GetTemporary(720, 280, 0);
            var previous = RenderTexture.active;
            var capture = new Texture2D(720, 280, TextureFormat.RGB24, false);
            string folder = $"output/quality-polish/golden-gauge-vfx/{tier}";
            Directory.CreateDirectory(folder);
            try
            {
                var gauge = new Rect(30, 178, 605, 86.4f);
                var safe = new Rect(0, 0, 720, 1280);
                for (int frame = 0; frame < 72; frame++)
                {
                    Color[] beforeSparkles = null;
                    // 0.4초 전 상태 → 획득 → 유지 → 3.6초 종료. 매 프레임 실제 런타임 풀을 사용한다.
                    float time = frame / 18f;
                    bool active = time >= .4f && time < 3.6f;
                    vfx.Advance(active, active || time >= 3.6f ? 1u : 0u, 1f / 36f, tier, false);
                    vfx.Advance(active, active || time >= 3.6f ? 1u : 0u, 1f / 36f, tier, false);
                    RenderTexture.active = target;
                    GL.Clear(true, true, InkPalette.Paper);
                    vfx.Draw(gauge, safe, new Vector2(720, 280), true);
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(0, 720, 280, 0);
                        tint.SetFloat("_Recolor", active ? 1f : 0f);
                        DrawSlicedGauge(gauge, fill, tint);
                    }
                    finally { GL.PopMatrix(); }
                    if (frame == 14)
                    {
                        capture.ReadPixels(new Rect(0, 0, 720, 280), 0, 0);
                        capture.Apply();
                        beforeSparkles = capture.GetPixels();
                    }
                    vfx.Draw(gauge, safe, new Vector2(720, 280), false);
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(0, 720, 280, 0);
                        tint.SetFloat("_Recolor", 0f);
                        Graphics.DrawTexture(new Rect(574, 170, 101, 101), brush,
                            new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, tint);
                    }
                    finally { GL.PopMatrix(); }
                    capture.ReadPixels(new Rect(0, 0, 720, 280), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:000}.png"), capture.EncodeToPNG());
                    if (frame == 14)
                    {
                        int goldenPixels = 0;
                        // 금색 원화·후광을 그린 직후와 비교한다. 별도의 위치 검사는 위 투영 테스트가 담당한다.
                        for (int y = 70; y < 130; y++)
                            for (int x = 30; x < 560; x++)
                            {
                                Color pixel = capture.GetPixel(x, y);
                                Color before = beforeSparkles[y * 720 + x];
                                float difference = Mathf.Abs(pixel.r - before.r) + Mathf.Abs(pixel.g - before.g) +
                                                   Mathf.Abs(pixel.b - before.b);
                                if (difference > .06f && pixel.r > pixel.b + .02f) goldenPixels++;
                            }
                        Assert.That(goldenPixels, Is.GreaterThan(40), "실제 게이지 위에서 금빛 입자를 렌더합니다.");
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(tint);
            }
        }

        static void DrawSlicedGauge(Rect area, Texture2D texture, Material material)
        {
            float capUv = Mathf.Clamp(texture.height / (float)texture.width, .08f, .25f);
            float cap = Mathf.Min(area.height, area.width * .5f);
            Graphics.DrawTexture(new Rect(area.x, area.y, cap, area.height), texture,
                new Rect(0, 0, capUv, 1), 0, 0, 0, 0, Color.white, material);
            Graphics.DrawTexture(new Rect(area.x + cap, area.y, area.width - 2 * cap, area.height), texture,
                new Rect(capUv, 0, 1 - 2 * capUv, 1), 0, 0, 0, 0, Color.white, material);
            Graphics.DrawTexture(new Rect(area.xMax - cap, area.y, cap, area.height), texture,
                new Rect(1 - capUv, 0, capUv, 1), 0, 0, 0, 0, Color.white, material);
        }

        static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static object Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
