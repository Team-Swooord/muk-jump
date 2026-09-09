using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.Core
{
    /// 황금 붓의 하단 HUD 전용 금박·별빛. 화면 좌표의 고정 풀을 두 번의 드로로 묶는다.
    /// 0~0.7초 획득 빛줄기 → 0.15~1.35초 흩날림 → 활성 중 낮은 밀도의 반짝임.
    /// 별도 카메라·RT·GameObject·전역 난수 없이 작동하며 실제 버프 시간은 변경하지 않는다.
    public sealed class GoldenGaugeVfx : IDisposable
    {
        public const int MaximumParticles = 64;
        const int MaximumQuads = MaximumParticles + 2;
        const float BurstDuration = .7f;
        const float EndFadeDuration = .28f;
        static readonly int GuiProjectionId = Shader.PropertyToID("_GuiProjection");

        struct Spark
        {
            // X는 게이지 폭의 비율, Y·크기는 540px 기준이다. 회전·해상도 변경에 재투영한다.
            public Vector2 Position, Velocity;
            public float Age, Lifetime, Size, Angle, Spin;
            public int Shape;
            public Color Tint;
        }

        readonly Spark[] sparks = new Spark[MaximumParticles];
        readonly Vector3[] vertices = new Vector3[MaximumQuads * 4];
        readonly Vector3[] uvs = new Vector3[MaximumQuads * 4];
        readonly Color[] colors = new Color[MaximumQuads * 4];
        readonly int[] indices = new int[MaximumQuads * 6];
        readonly Mesh mesh;
        readonly Material material;
        uint seed = 0x719CA4B3;
        uint observedRevision;
        int count, quads;
        float emissionCredit, burstAge = 10f, cooldown, fade;
        bool wasActive, reduced, disposed;

        public int ActiveCount => disposed ? 0 : count;
        public int ParticleLimit { get; private set; }
        public int BurstCount { get; private set; }

        public GoldenGaugeVfx(Shader shader)
        {
            material = new Material(shader)
            { name = "GoldenGauge_Sparkle", hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "GoldenGauge_PooledMesh", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            for (int i = 0; i < MaximumQuads; i++)
            {
                int v = i * 4, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
        }

        public void Advance(bool active, uint revision, float deltaTime,
            VfxQualityTier tier, bool reduceMotion)
        {
            if (disposed) return;
            float dt = float.IsNaN(deltaTime) ? 0f : Mathf.Clamp(deltaTime, 0f, .05f);
            ParticleLimit = tier switch { VfxQualityTier.Low => 24, VfxQualityTier.High => 64, _ => 44 };
            reduced = reduceMotion;
            if (count > ParticleLimit) count = ParticleLimit;
            cooldown = Mathf.Max(0f, cooldown - dt);
            burstAge += dt;
            bool acquired = active && (!wasActive || revision != observedRevision);
            observedRevision = revision;
            wasActive = active;
            fade = active ? 1f : Mathf.Max(0f, fade - dt / EndFadeDuration);

            if (reduced)
            {
                count = 0;
                emissionCredit = 0f;
                burstAge = 10f;
                return; // 원래 금빛 게이지와 낮은 정적 후광은 남긴다.
            }
            for (int i = count - 1; i >= 0; i--)
            {
                ref Spark spark = ref sparks[i];
                spark.Age += dt;
                if (spark.Age >= spark.Lifetime || fade <= 0f)
                { sparks[i] = sparks[--count]; continue; }
                if (spark.Age < 0f) continue;
                spark.Position += spark.Velocity * dt;
                spark.Velocity *= 1f / (1f + dt * 1.9f);
                spark.Velocity.y -= dt * 6f;
                spark.Angle += spark.Spin * dt;
            }
            if (acquired && cooldown <= 0f)
            {
                burstAge = 0f;
                cooldown = .4f;
                BurstCount++;
                int burst = tier switch { VfxQualityTier.Low => 16, VfxQualityTier.High => 40, _ => 28 };
                // 재획득 때도 폭발 수를 누적하지 않고 남아 있는 장식 일부를 재사용한다.
                count = Mathf.Min(count, ParticleLimit - burst);
                for (int i = 0; i < burst; i++) Emit(true, i, burst);
            }
            if (!active) { emissionCredit = 0f; return; }
            emissionCredit += dt * (tier == VfxQualityTier.Low ? 5f : tier == VfxQualityTier.High ? 15f : 10f);
            while (emissionCredit >= 1f)
            {
                emissionCredit -= 1f;
                if (count < ParticleLimit) Emit(false, 0, 1);
            }
        }

        void Emit(bool burst, int index, int total)
        {
            if (count >= ParticleLimit) return;
            int shape = burst ? index % 3 : (Next01() < .45f ? 0 : 1);
            float u = burst ? Mathf.Lerp(.1f, .96f, (index + Next01()) / total) : Range(.1f, .96f);
            sparks[count++] = new Spark
            {
                Position = new Vector2(u, Range(-4f, 5f)),
                Velocity = new Vector2(Range(-.045f, .045f), burst ? Range(-112f, -35f) : Range(-30f, -12f)),
                Age = burst ? -Range(0f, .22f) : 0f,
                Lifetime = shape == 2 ? Range(.3f, .62f) : Range(.6f, 1.35f),
                Size = shape == 0 ? Range(15f, burst ? 30f : 22f) : shape == 1 ? Range(4f, 9f) : Range(11f, 20f),
                Angle = Range(-.55f, .55f), Spin = Range(-.4f, .4f), Shape = shape,
                Tint = Color.Lerp(InkPalette.Gold, InkPalette.TimerGold, Range(.45f, 1f)),
            };
        }

        public void Draw(Rect gauge, Rect safeGui, Vector2 viewport, bool behind)
        {
            if (disposed || fade <= 0f || gauge.width <= 0f || viewport.x <= 0f || viewport.y <= 0f) return;
            quads = 0;
            float scale = Mathf.Min(safeGui.width, safeGui.height) / 540f;
            // 붓 이미지의 가는 왼쪽 끝보다 잉크가 남는 중앙~오른쪽에서 주로 피어난다.
            float baseline = gauge.center.y;
            if (behind)
            {
                float impact = Mathf.Clamp01(1f - burstAge / BurstDuration);
                Color glow = InkPalette.TimerGold;
                glow.a = fade * (reduced ? .055f : .075f + impact * .22f);
                AddQuad(new Vector2(gauge.center.x, baseline),
                    new Vector2(gauge.width * 1.02f, 54f * scale), 0f, 3, glow);
            }
            else if (!reduced)
            {
                for (int i = 0; i < count; i++)
                {
                    Spark spark = sparks[i];
                    if (spark.Age < 0f) continue;
                    float t = Mathf.Clamp01(spark.Age / spark.Lifetime);
                    float envelope = Mathf.SmoothStep(0f, 1f, t / .14f) *
                                     (1f - Mathf.SmoothStep(.32f, 1f, t));
                    Vector2 position = new(gauge.x + gauge.width * spark.Position.x,
                        baseline + spark.Position.y * scale);
                    if (position.x < safeGui.xMin || position.x > safeGui.xMax ||
                        position.y < safeGui.yMin || position.y > viewport.y) continue;
                    float size = spark.Size * scale * (.55f + .45f * envelope);
                    Vector2 dimensions = spark.Shape == 2 ? new Vector2(size * .28f, size) : Vector2.one * size;
                    Color color = spark.Tint;
                    color.a = envelope * fade;
                    AddQuad(position, dimensions, spark.Angle, spark.Shape, color);
                }
                if (burstAge < BurstDuration)
                {
                    float t = burstAge / BurstDuration;
                    float progress = 1f - (1f - t) * (1f - t);
                    Color shine = InkPalette.TextLight;
                    shine.a = Mathf.Sin(t * Mathf.PI) * fade * .8f;
                    AddQuad(new Vector2(Mathf.Lerp(gauge.x + gauge.width * .06f, gauge.xMax, progress), baseline),
                        new Vector2(94f * scale, 16f * scale), 0f, 0, shine);
                }
            }
            if (quads == 0) return;
            mesh.Clear(false);
            mesh.SetVertices(vertices, 0, quads * 4);
            mesh.SetColors(colors, 0, quads * 4);
            mesh.SetUVs(0, uvs, 0, quads * 4);
            mesh.SetTriangles(indices, 0, quads * 6, 0, false);
            mesh.bounds = new Bounds(new Vector3(viewport.x * .5f, viewport.y * .5f),
                new Vector3(viewport.x * 2f, viewport.y * 2f, 10f));
            // 월드 카메라 행렬은 사용하지 않는다. Metal 등에서 RT와 화면의 Y 방향도 보정한다.
            Matrix4x4 projection = Matrix4x4.Ortho(0f, viewport.x, viewport.y, 0f, -1f, 1f);
            material.SetMatrix(GuiProjectionId,
                GL.GetGPUProjectionMatrix(projection, RenderTexture.active != null));
            if (material.SetPass(0)) Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
        }

        void AddQuad(Vector2 center, Vector2 size, float angle, int shape, Color color)
        {
            if (quads >= MaximumQuads) return;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            int start = quads++ * 4;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 0 || corner == 3 ? 0f : 1f;
                float y = corner < 2 ? 0f : 1f;
                Vector2 offset = new((x - .5f) * size.x, (y - .5f) * size.y);
                vertices[start + corner] = center + new Vector2(offset.x * cos - offset.y * sin,
                    offset.x * sin + offset.y * cos);
                uvs[start + corner] = new Vector3(x, y, shape);
                colors[start + corner] = color;
            }
        }

        public void Clear()
        {
            count = 0;
            emissionCredit = cooldown = fade = 0f;
            burstAge = 10f;
            wasActive = false;
            observedRevision = 0;
        }

        float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, Next01());
        float Next01()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            if (Application.isPlaying) { Object.Destroy(mesh); Object.Destroy(material); }
            else { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }
    }
}
