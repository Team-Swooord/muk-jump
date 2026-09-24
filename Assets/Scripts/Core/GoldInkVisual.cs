using UnityEngine;

namespace MukJump.Core
{
    /// 원본 프레임·색상 애니메이션을 건드리지 않는 공용 금색 머티리얼 두 개만 사용한다.
    public static class GoldInkVisual
    {
        static Material world, ui;
        public static bool IsWorld(Material material) => material != null && material.shader != null &&
            material.shader.name == "MukJump/GoldInkSprite";
        public static Material World => Load(ref world, "MukJump/Shaders/GoldInkSprite");
        public static Material Ui => Load(ref ui, "MukJump/Shaders/GoldInkUi");
        static Material Load(ref Material material, string path)
        {
            if (material != null) return material;
            var shader = Resources.Load<Shader>(path);
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return material;
        }
        public static void Apply(UnityEngine.UI.Image body, bool gold)
        {
            Material desired = gold ? Ui : null;
            if (body != null && body.material != (desired != null ? desired : body.defaultMaterial)) body.material = desired;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (world != null) Object.Destroy(world);
            if (ui != null) Object.Destroy(ui);
            world = ui = null;
        }
    }
}
