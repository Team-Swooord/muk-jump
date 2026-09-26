using UnityEngine;

namespace MukJump.Core
{
    /// 원본 프레임·색상 애니메이션을 건드리지 않는 공용 금색 머티리얼 두 개만 사용한다.
    public static class GoldInkVisual
    {
        static Material world, ui;
        static bool observingSettings;
        static readonly int ShimmerId = Shader.PropertyToID("_GoldShimmer");
        public static bool IsWorld(Material material) => material != null && material.shader != null &&
            material.shader.name == "MukJump/GoldInkSprite";
        public static Material World => Load(ref world, "MukJump/Shaders/GoldInkSprite");
        public static Material Ui => Load(ref ui, "MukJump/Shaders/GoldInkUi");
        static Material Load(ref Material material, string path)
        {
            if (!observingSettings)
            {
                LobbySettingsProfile.Changed += RefreshMotion;
                observingSettings = true;
            }
            if (material != null) return material;
            var shader = Resources.Load<Shader>(path);
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            RefreshMotion();
            return material;
        }
        static void RefreshMotion()
        {
            float strength = LobbySettingsProfile.ReducedMotionEnabled ? 0f : 1f;
            if (world != null) world.SetFloat(ShimmerId, strength);
            if (ui != null) ui.SetFloat(ShimmerId, strength);
        }
        public static void Apply(UnityEngine.UI.Image body, bool gold)
        {
            Material desired = gold ? Ui : null;
            if (body != null && body.material != (desired != null ? desired : body.defaultMaterial)) body.material = desired;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            LobbySettingsProfile.Changed -= RefreshMotion;
            observingSettings = false;
            Release(world);
            Release(ui);
            world = ui = null;
        }
        static void Release(Material material)
        {
            if (material == null) return;
            // 에디터 미리보기 검증에서도 이 클래스가 만든 임시 재질만 정리한다.
            if (Application.isPlaying) Object.Destroy(material);
            else Object.DestroyImmediate(material);
        }
    }
}
