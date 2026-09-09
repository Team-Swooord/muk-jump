using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 산수화는 고정하고 맵별 구름·안개·성운만 눈에 보일 만큼 천천히 흘려 보낸다.
    /// 빌더가 만든 네 렌더러를 재사용하며 입력·물리·게임 난수에는 관여하지 않는다.
    [DisallowMultipleComponent]
    public sealed class AmbientCloudView : MonoBehaviour
    {
        public const int MaximumLayers = 4;
        public const int SortingOrder = -8;
        public const float DriftSpeedMultiplier = 1.8f;
        const float EdgePadding = 0.025f;
        const float ThemeFadeHalfSeconds = 0.5f;

        [SerializeField] Camera worldCamera;
        [SerializeField] SpriteRenderer[] cloudRenderers;
        [SerializeField] Sprite[] fallbackSprites;
        [SerializeField] AmbientCloudTheme[] themes;

        static readonly float[] StartX = { 0.16f, 0.82f, 0.38f, 1.02f };
        static readonly float[] Height = { 0.81f, 0.63f, 0.46f, 0.92f };
        static readonly float[] Width = { 0.86f, 0.74f, 0.96f, 0.56f };
        static readonly float[] Speed = { 0.0017f, 0.0024f, 0.0020f, 0.0015f };
        static readonly float[] Opacity = { 0.42f, 0.40f, 0.32f, 0.26f };

        readonly float[] visibility = new float[MaximumLayers];
        double elapsedSeconds;
        double flowSeconds;
        bool initialized;
        bool themeInitialized;
        int currentTheme;
        int pendingTheme;
        bool currentMirrored;
        bool pendingMirrored;
        float themeFade = 1f;

        public int CurrentThemeIndex => currentTheme;
        public AmbientCloudMotion CurrentMotion => HasThemeSprites(currentTheme)
            ? themes[currentTheme].motion : AmbientCloudMotion.Clouds;

        public void Configure(Camera camera, SpriteRenderer[] renderers, AmbientCloudTheme[] mapThemes = null)
        {
            worldCamera = camera;
            cloudRenderers = renderers;
            themes = mapThemes;
            fallbackSprites = new Sprite[renderers?.Length ?? 0];
            for (int i = 0; i < fallbackSprites.Length; i++)
                fallbackSprites[i] = renderers[i] != null ? renderers[i].sprite : null;
            initialized = false;
            SetBackground(null, true);
        }

        void OnEnable()
        {
            if (worldCamera == null) worldCamera = GetComponentInParent<Camera>();
            if (!themeInitialized) SetBackground(null, true);
            Refresh(0f, false, VfxQualityRuntime.Tier);
        }

        void OnDisable()
        {
            // 부모 배경도 OnDisable에서 전환 목적지를 확정한다. 재활성화 시 구형 테마를 남기지 않는다.
            ApplyTheme(pendingTheme, pendingMirrored);
            themeFade = 1f;
            if (cloudRenderers == null) return;
            for (int i = 0; i < cloudRenderers.Length; i++)
                if (cloudRenderers[i] != null) cloudRenderers[i].enabled = false;
        }

        public void SetBackground(Sprite background, bool immediate = false, bool mirrorX = false)
        {
            pendingTheme = ResolveTheme(background);
            pendingMirrored = mirrorX;
            if (immediate || !themeInitialized)
            {
                ApplyTheme(pendingTheme, pendingMirrored);
                themeFade = 1f;
                Refresh(0f, false, VfxQualityRuntime.Tier);
            }
            // 같은 요청은 진행 중인 페이드를 다시 시작하지 않는다. 최신 목적지만 보관한다.
        }

        int ResolveTheme(Sprite background)
        {
            if (background != null && themes != null)
                for (int i = 0; i < themes.Length; i++)
                    if (themes[i] != null && themes[i].background == background && HasThemeSprites(i))
                        return i;
            return 0;
        }

        bool HasThemeSprites(int index) => themes != null && index >= 0 && index < themes.Length &&
            themes[index] != null && CountValid(themes[index].sprites) > 0;

        static int CountValid(Sprite[] sprites)
        {
            if (sprites == null) return 0;
            int count = 0;
            for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null) count++;
            return count;
        }

        static Sprite PickValid(Sprite[] sprites, int index)
        {
            int count = CountValid(sprites);
            if (count == 0) return null;
            int wanted = index % count;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null && wanted-- == 0) return sprites[i];
            return null;
        }

        void ApplyTheme(int theme, bool mirrored)
        {
            currentTheme = theme;
            currentMirrored = mirrored;
            themeInitialized = true;
            if (cloudRenderers == null) return;
            Sprite[] selected = HasThemeSprites(theme) ? themes[theme].sprites :
                HasThemeSprites(0) ? themes[0].sprites : fallbackSprites;
            for (int i = 0; i < Mathf.Min(cloudRenderers.Length, MaximumLayers); i++)
            {
                if (cloudRenderers[i] == null) continue;
                cloudRenderers[i].sprite = PickValid(selected, i);
                cloudRenderers[i].flipX = (i % 2 != 0) ^ mirrored;
            }
        }

        void AdvanceTheme(float deltaTime)
        {
            if (currentTheme != pendingTheme || currentMirrored != pendingMirrored)
            {
                themeFade = Mathf.MoveTowards(themeFade, 0f, deltaTime / ThemeFadeHalfSeconds);
                if (themeFade <= 0f) ApplyTheme(pendingTheme, pendingMirrored);
            }
            else themeFade = Mathf.MoveTowards(themeFade, 1f, deltaTime / ThemeFadeHalfSeconds);
        }

        void LateUpdate()
        {
            GameManager manager = GameManager.Instance;
            bool animate = CanAnimate(
                Application.isPlaying,
                MobileApplicationLifecycle.IsApplicationActive,
                manager != null && manager.IsPaused,
                LobbySettingsProfile.ReducedMotionEnabled);
            Refresh(Time.unscaledDeltaTime, animate, VfxQualityRuntime.Tier);
        }

        public static bool CanAnimate(bool playing, bool applicationActive,
            bool paused, bool reducedMotion) =>
            playing && applicationActive && !paused && !reducedMotion;

        public static int VisibleLayerCount(VfxQualityTier tier) => tier switch
        {
            VfxQualityTier.Low => 2,
            VfxQualityTier.Medium => 3,
            _ => MaximumLayers,
        };

        // 맵별 방향·속도 차이는 보존하고 가로 이동만 공통으로 조금 더 뚜렷하게 한다.
        public static float LayerSpeed(int index) => Speed[index] * DriftSpeedMultiplier;
        public static float LayerWidth(int index) => Width[index];

        public static float ViewportX(int index, double seconds) =>
            ViewportXFromFlow(index, Math.Max(0d, seconds));

        static float ViewportXFromFlow(int index, double flow)
        {
            double left = -Width[index] * 0.5d - EdgePadding;
            double span = 1d + Width[index] + EdgePadding * 2d;
            double position = StartX[index] - left + flow * LayerSpeed(index);
            // 역방향 흐름도 양의 나머지로 계산해 화면 밖에서만 순환한다.
            return (float)(left + (position % span + span) % span);
        }

        void Refresh(float deltaTime, bool animate, VfxQualityTier tier)
        {
            if (worldCamera == null || cloudRenderers == null) return;
            float dt = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                ? 0f : Mathf.Clamp(deltaTime, 0f, 0.05f);
            AmbientCloudMotionProfile profile = AmbientCloudMotionProfile.For(CurrentMotion);
            if (animate)
            {
                elapsedSeconds += dt;
                // 테마가 달라져도 누적 X를 유지한다. 새 속도는 다음 이동분에만 반영한다.
                flowSeconds += dt * profile.Flow;
            }
            // 위치 정지와 테마 교체는 별도다. 일시정지 중 배경이 바뀌어도 새 테마를 따라간다.
            AdvanceTheme(dt);
            profile = AmbientCloudMotionProfile.For(CurrentMotion);

            float cameraHeight = worldCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * worldCamera.aspect;
            int count = VisibleLayerCount(tier);
            int limit = Mathf.Min(cloudRenderers.Length, MaximumLayers);
            for (int i = 0; i < limit; i++)
            {
                SpriteRenderer layer = cloudRenderers[i];
                if (layer == null) continue;
                if (layer.sprite == null) { layer.enabled = false; continue; }

                float target = i < count ? 1f : 0f;
                visibility[i] = !initialized ? target : Mathf.MoveTowards(visibility[i], target, dt * 0.4f);
                double phase = elapsedSeconds * Math.PI * 2d / profile.Period + i * 1.7d;
                float wave = (float)Math.Sin(phase);
                float breath = 1f - profile.Breath * (0.5f + 0.5f * wave);
                float alpha = Opacity[i] * visibility[i] * themeFade * breath;
                layer.enabled = alpha > 0f;
                Color tint = LobbyNightState.CloudTint;
                layer.color = new Color(tint.r, tint.g, tint.b, alpha);
                layer.sortingOrder = SortingOrder;

                Vector2 spriteSize = layer.sprite.bounds.size;
                float roll = profile.Roll * wave;
                float radians = roll * Mathf.Deg2Rad;
                float rotatedHeight = Mathf.Abs(Mathf.Sin(radians)) * spriteSize.x +
                                      Mathf.Abs(Mathf.Cos(radians)) * spriteSize.y;
                float scale = cameraWidth * Width[i] / Mathf.Max(0.01f, spriteSize.x);
                scale = Mathf.Min(scale, cameraHeight * 0.16f / Mathf.Max(0.01f, rotatedHeight));
                layer.transform.localScale = Vector3.one * scale;
                layer.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
                layer.transform.localPosition = new Vector3(
                    (ViewportXFromFlow(i, flowSeconds) - 0.5f) * cameraWidth,
                    (Height[i] - 0.5f + profile.Sway * wave) * cameraHeight, 0f);
            }
            initialized = true;
        }
    }
}
