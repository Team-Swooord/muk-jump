using UnityEngine;

namespace MukJump.Core
{
    /// 장식의 그림을 변형하지 않는다. UI·플레이가 같은 원화 프레임을 공유한다.
    public static class InkAccessoryMotion
    {
        public const float Padding = .05f;
        static readonly InkSpriteAnimation[] animations = new InkSpriteAnimation[InkSkinCatalog.Count];
        static InkSpriteAnimation held;
        public static float Strength => LobbySettingsProfile.ReducedMotionEnabled ? 0f : 1f;
        public static bool Supports(InkSkinId id) => Key(id) != null;
        public static string Key(InkSkinId id) => id switch
        {
            InkSkinId.SpiritHalo => "spirit_halo", InkSkinId.BlueWisps => "blue_wisps",
            InkSkinId.PaperLanterns => "paper_lanterns", InkSkinId.MoonClouds => "moon_clouds",
            InkSkinId.OrigamiWings => "origami_wings", InkSkinId.CraneWings => "crane_wings",
            InkSkinId.ButterflyWings => "butterfly_wings",
            InkSkinId.DragonTail => "dragon_tail", InkSkinId.NineTails => "nine_tails", _ => null
        };
        public static InkSpriteAnimation Animation(InkSkinId id)
        {
            string key = Key(id);
            if (key == null) return null;
            return animations[(int)id] != null ? animations[(int)id] :
                animations[(int)id] = Resources.Load<InkSpriteAnimation>("MukJump/SpriteAnimations/" + key);
        }
        public static InkSpriteAnimation Held => held != null ? held :
            held = Resources.Load<InkSpriteAnimation>("MukJump/SpriteAnimations/muk_held");
        public static Sprite Sample(InkSkinId id, float seconds, bool animate = true) => Animation(id)?.Sample(seconds, animate);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { System.Array.Clear(animations, 0, animations.Length); held = null; }
    }
}
