using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MukJump.Core
{
    /// 발밑 먹 튐·갈필 가루·번짐. 씬당 세 방출기를 재사용하고 판정에는 관여하지 않는다.
    /// 자동 재생 대신 게임 시간으로만 시뮬레이션해 일시정지와 앱 복귀를 동일하게 처리한다.
    public sealed class InkContactParticles : IDisposable
    {
        readonly GameObject root;
        readonly Material dropletMaterial;
        readonly Material washMaterial;
        readonly ParticleSystem drops;
        readonly ParticleSystem motes;
        readonly ParticleSystem wash;
        uint seed = 0x519AC821;
        bool disposed;
        bool reduced;
        VfxQualityTier tier;

        public int ActiveCount => disposed ? 0 : drops.particleCount + motes.particleCount + wash.particleCount;
        public int MovingCount => disposed ? 0 : drops.particleCount + motes.particleCount;
        public int Capacity => disposed ? 0 : drops.main.maxParticles + motes.main.maxParticles + wash.main.maxParticles;

        public InkContactParticles(Transform parent, Texture2D atlas, Texture2D splash, Shader shader)
        {
            root = new GameObject("InkContactParticles");
            root.transform.SetParent(parent, false);
            dropletMaterial = new Material(shader) { name = "ContactInk_Atlas", mainTexture = atlas };
            washMaterial = new Material(shader) { name = "ContactInk_Wash", mainTexture = splash };
            drops = CreateLayer("BallisticInk", dropletMaterial, true, 0.34f, 4, false);
            motes = CreateLayer("DryBrushMotes", dropletMaterial, true, 0.06f, 4, false);
            wash = CreateLayer("ContactWash", washMaterial, false, 0f, 3, true);
            // 큰 먹은 먼저 튀고 빠르게 감속한다. 응집에 쓰는 motes는 직선 도착을 유지한다.
            var resistance = drops.limitVelocityOverLifetime;
            resistance.enabled = true;
            resistance.limit = 100f;
            resistance.dampen = 0f;
            resistance.drag = 2.4f;
            var dustColor = motes.colorOverLifetime;
            var dustEnvelope = new Gradient();
            dustEnvelope.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.7f, 0.45f), new GradientAlphaKey(0f, 1f) });
            dustColor.color = dustEnvelope;
            tier = VfxQualityRuntime.Tier;
            reduced = LobbySettingsProfile.ReducedMotionEnabled;
            ApplyBudget();
            PrimePausedSystems();
            LobbySettingsProfile.Changed += SyncSettings;
            VfxQualityRuntime.Changed += OnQualityChanged;
        }

        ParticleSystem CreateLayer(string name, Material material, bool atlas, float gravity,
            int order, bool expands)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 0.65f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.gravityModifier = gravity;
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            system.useAutoRandomSeed = false;
            system.randomSeed = NextSeed();
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.94f, 0.2f),
                    new GradientAlphaKey(0.45f, 0.64f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, expands
                ? new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.24f, 1.05f), new Keyframe(1f, 1.26f))
                : new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0.15f)));
            if (atlas)
            {
                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = sheet.numTilesY = 4;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return system;
        }

        public void EmitJump(Vector3 position, Vector2 direction, bool airborne)
        {
            if (disposed) return;
            SyncSettings();
            // 움직임 줄이기에서는 기존 압축 고리만 사용한다.
            if (reduced) return;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            EmitFan(position, direction, airborne ? 0.65f : 0.42f, false, airborne);
        }

        public void EmitLanding(Vector3 position, float strength)
        {
            if (disposed) return;
            SyncSettings();
            if (reduced) return;
            EmitFan(position, Vector2.up, Mathf.Clamp01(strength), true, false);
        }

        /// 빠른 이동 뒤 갈필 두 조각만 남긴다. 접촉/응집을 위한 자리를 항상 보존한다.
        public void EmitAirSlip(Vector3 position, Vector2 velocity)
        {
            if (disposed) return;
            SyncSettings();
            if (reduced || velocity.sqrMagnitude < 9f) return;
            int count = tier == VfxQualityTier.Low ? 1 : 2;
            if (motes.particleCount + count > motes.main.maxParticles - 8) return;
            Vector2 direction = velocity.normalized;
            Vector2 tangent = new Vector2(direction.y, -direction.x);
            for (int i = 0; i < count; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Emit(motes, position + (Vector3)(tangent * side * Range(0.12f, 0.22f)),
                    -direction * Range(0.25f, 0.65f) + tangent * side * 0.15f,
                    new Vector3(0.055f, Range(0.18f, 0.29f), 1f), Range(0.2f, 0.34f),
                    0.36f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }
            motes.Pause(false);
        }

        /// 벽·위험 충격에 쓰는 방향성 먹 튐. 추가 방출기나 물리 충돌은 만들지 않는다.
        public void EmitImpact(Vector3 position, Vector2 outward, float strength)
        {
            if (disposed) return;
            SyncSettings();
            if (reduced) return;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector2.up;
            Vector2 tangent = new Vector2(-outward.y, outward.x);
            int count = Mathf.Min(VfxQualityRuntime.Profile.ScaleDecorativeCount(12, 4),
                drops.main.maxParticles - drops.particleCount);
            for (int i = 0; i < count; i++)
            {
                float fan = Mathf.Lerp(-1f, 1f, (i + 0.5f) / count);
                Vector2 velocity = outward * Range(1.7f, 3.8f) + tangent * fan * 2.5f;
                velocity *= Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(strength));
                float size = Range(0.14f, 0.26f);
                Emit(drops, position, velocity, new Vector3(size, size * 1.7f, 1f),
                    Range(0.3f, 0.56f), 0.9f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
            }
            drops.Pause(false);
        }

        /// 유효 획의 끝에서만 섬유가 낮게 흩어져 정착한다. 기존 세 방출기 예산을 공유한다.
        public void EmitStrokeSettle(Vector3 position)
        {
            if (disposed) return;
            SyncSettings();
            if (reduced) return;
            int count = Mathf.Min(VfxQualityRuntime.Profile.ScaleDecorativeCount(8, 3),
                motes.main.maxParticles - motes.particleCount);
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Emit(motes, position + new Vector3(Range(-0.14f, 0.14f), Range(-0.04f, 0.07f)),
                    new Vector2(side * Range(0.3f, 0.85f), Range(0.08f, 0.36f)),
                    new Vector3(Range(0.09f, 0.14f), Range(0.22f, 0.34f), 1f),
                    Range(0.28f, 0.5f), Range(0.68f, 0.88f), Range(65f, 115f));
            }
            int dots = Mathf.Min(tier == VfxQualityTier.High ? 2 : 1, drops.main.maxParticles - drops.particleCount);
            for (int i = 0; i < dots; i++)
                Emit(drops, position, new Vector2(Range(-0.5f, 0.5f), Range(0.15f, 0.4f)),
                    new Vector3(0.16f, 0.19f, 1f), 0.32f, 0.85f, Range(0f, 360f));
            drops.Pause(false);
            motes.Pause(false);
        }

        /// 분신의 기존 몸통 팝에 맞춰 작은 먹이 안으로 모인다. 충돌·생성 지연은 없다.
        public void EmitCloneConvergence(Vector3 position, Vector2 carrierVelocity = default)
        {
            if (disposed) return;
            SyncSettings();
            if (reduced) return;
            int count = Mathf.Min(VfxQualityRuntime.Profile.ScaleDecorativeCount(8, 3),
                motes.main.maxParticles - motes.particleCount);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f + Range(-0.15f, 0.15f);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(0.55f, 0.85f);
                float lifetime = Range(0.18f, 0.24f);
                float size = Range(0.19f, 0.3f);
                Emit(motes, position + (Vector3)offset, carrierVelocity - offset / lifetime,
                    new Vector3(size, size * 1.3f, 1f), lifetime, 0.85f, angle * Mathf.Rad2Deg - 90f);
            }
            motes.Pause(false);
        }

        void EmitFan(Vector3 position, Vector2 direction, float strength, bool landing, bool airborne)
        {
            Vector2 tangent = new Vector2(direction.y, -direction.x);
            int dropCount = VfxQualityRuntime.Profile.ScaleDecorativeCount(landing ? Mathf.RoundToInt(Mathf.Lerp(7f, 14f, strength)) : 10, 3);
            int moteCount = VfxQualityRuntime.Profile.ScaleDecorativeCount(landing ? 10 : 7, 2);
            dropCount = Mathf.Min(dropCount, drops.main.maxParticles - drops.particleCount);
            moteCount = Mathf.Min(moteCount, motes.main.maxParticles - motes.particleCount);
            for (int i = 0; i < dropCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 velocity = landing
                    ? new Vector2(side * Range(1.3f, 3.4f) * Mathf.Lerp(0.65f, 1f, strength), Range(0.8f, 2.3f))
                    : tangent * side * Range(1f, 2.5f) - direction * Range(0.3f, 1.1f) + Vector2.up * 0.65f;
                if (airborne) velocity += direction * Range(-0.4f, 1.2f);
                float width = Range(0.20f, 0.36f) * Mathf.Lerp(0.85f, 1.2f, strength);
                Emit(drops, position + (Vector3)(tangent * side * Range(0.08f, 0.3f)), velocity,
                    new Vector3(width, width * Range(1.2f, 1.85f), 1f), Range(0.38f, 0.62f),
                    Range(0.82f, 1f), Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
            }
            for (int i = 0; i < moteCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float size = Range(0.08f, 0.17f);
                Emit(motes, position + new Vector3(Range(-0.4f, 0.4f), Range(-0.05f, 0.15f)),
                    new Vector2(side * Range(0.5f, 1.7f), Range(0.15f, 0.8f)),
                    new Vector3(size, size, 1f), Range(0.5f, 0.85f), Range(0.45f, 0.68f), Range(0f, 360f));
            }
            if (!airborne && wash.particleCount < wash.main.maxParticles)
                Emit(wash, position, Vector2.zero,
                    new Vector3(Mathf.Lerp(1.7f, 2.6f, strength), Mathf.Lerp(0.45f, 0.72f, strength), 1f),
                    landing ? 0.48f : 0.35f, landing ? 0.34f : 0.23f, 0f);
            // Emit은 정지된 방출기를 다시 재생 상태로 바꿀 수 있다. 네이티브 자동
            // 업데이트가 같은 프레임에 한 번 더 진행하지 않도록 방출 직후 멈춘다.
            drops.Pause(false);
            motes.Pause(false);
            wash.Pause(false);
        }

        void Emit(ParticleSystem system, Vector3 position, Vector2 velocity, Vector3 size,
            float lifetime, float alpha, float rotation)
        {
            Color color = InkPalette.Ink;
            color.a = alpha;
            system.Emit(new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize3D = size,
                startLifetime = lifetime, startColor = color, rotation = rotation,
                randomSeed = NextSeed(), applyShapeToPosition = false,
            }, 1);
        }

        public void Advance(float deltaTime, bool gameplayTicking)
        {
            if (disposed) return;
            SyncSettings();
            if (!gameplayTicking || deltaTime <= 0f || ActiveCount == 0) return;
            // 복귀 직후 누적 시간을 따라잡는 폭발·이동을 금지한다.
            float step = Mathf.Min(deltaTime, 0.05f);
            if (drops.particleCount > 0) drops.Simulate(step, false, false, false);
            if (motes.particleCount > 0) motes.Simulate(step, false, false, false);
            if (wash.particleCount > 0) wash.Simulate(step, false, false, false);
        }

        void SyncSettings()
        {
            bool nextReduced = LobbySettingsProfile.ReducedMotionEnabled;
            if (nextReduced != reduced)
            {
                reduced = nextReduced;
                Clear();
            }
            if (tier == VfxQualityRuntime.Tier) return;
            // 강등 즉시 현재 입자까지 비워 새 예산을 초과하지 않는다. 재할당하지 않는다.
            tier = VfxQualityRuntime.Tier;
            Clear();
            ApplyBudget();
        }

        void OnQualityChanged(VfxQualityTier value, VfxQualityChangeReason reason) => SyncSettings();

        void ApplyBudget()
        {
            int index = (int)tier;
            var d = drops.main; d.maxParticles = index == 0 ? 22 : index == 1 ? 34 : 48;
            var m = motes.main; m.maxParticles = index == 0 ? 12 : index == 1 ? 24 : 36;
            var w = wash.main; w.maxParticles = index == 0 ? 3 : index == 1 ? 4 : 6;
        }

        public void Clear()
        {
            if (disposed || ActiveCount == 0) return;
            drops.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            motes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            wash.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            PrimePausedSystems();
        }

        void PrimePausedSystems()
        {
            // Stop 상태 그대로는 Editor의 수동 Emit이 네이티브 입자 버퍼를 준비하지 않는다.
            // 0초 Simulate로 초기화하되 Play로 자동 시간 진행을 켜지는 않는다.
            drops.Simulate(0f, false, true, false);
            motes.Simulate(0f, false, true, false);
            wash.Simulate(0f, false, true, false);
        }

        // 시각용 난수는 게임의 장애물·아이템 난수열을 바꾸지 않는다.
        uint NextSeed()
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return seed;
        }

        float Range(float min, float max) => Mathf.Lerp(min, max, (NextSeed() & 0xFFFFFF) / 16777216f);

        public void Dispose()
        {
            if (disposed) return;
            LobbySettingsProfile.Changed -= SyncSettings;
            VfxQualityRuntime.Changed -= OnQualityChanged;
            Clear();
            disposed = true;
            DestroyOwned(root);
            DestroyOwned(dropletMaterial);
            DestroyOwned(washMaterial);
        }

        static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
