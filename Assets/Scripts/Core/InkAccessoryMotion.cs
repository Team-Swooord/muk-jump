using UnityEngine;

namespace MukJump.Core
{
    /// UI·월드가 공유하는 장식 전용 변형. 몸체·충돌·게임 난수에는 접근하지 않는다.
    public static class InkAccessoryMotion
    {
        public const int Segments = 12;
        public const float Padding = .05f;
        public static bool Supports(InkSkinId id) => id is InkSkinId.SpiritHalo or InkSkinId.BlueWisps
            or InkSkinId.PaperLanterns or InkSkinId.MoonClouds or InkSkinId.OrigamiWings
            or InkSkinId.CraneWings or InkSkinId.ButterflyWings or InkSkinId.StormScarf or InkSkinId.DragonTail;
        public static float Strength => LobbySettingsProfile.ReducedMotionEnabled ? 0f :
            VfxQualityRuntime.Tier == VfxQualityTier.Low ? .55f : 1f;
        public static float SampleTime(float time)
        {
            float hz = VfxQualityRuntime.Tier == VfxQualityTier.Low ? 15f : 30f;
            return Mathf.Floor(time * hz) / hz;
        }
        // 정규화 원화 좌표를 변형하고 UV는 그대로 유지한다. 최대 이동량은 피팅 여백과 같다.
        public static Vector2 Deform(InkSkinId id, Vector2 uv, float time, float strength)
        {
            if (!Supports(id) || strength <= 0f) return uv;
            float x = uv.x - .5f, y = uv.y - .5f;
            Vector2 delta;
            if (id is InkSkinId.CraneWings or InkSkinId.ButterflyWings or InkSkinId.OrigamiWings)
            {
                float hinge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .48f, Mathf.Abs(x)));
                float beat = time * (id == InkSkinId.ButterflyWings ? 4.6f : id == InkSkinId.OrigamiWings ? 2.2f : 3f);
                // 뿌리 고정, 끝만 접고 편다. 좌우 날개가 서로 떨어지지 않는다.
                delta = new Vector2(-x * .18f * (.5f + .5f * Mathf.Sin(beat)) * hinge,
                    .028f * Mathf.Sin(beat + Mathf.Abs(x) * 2f) * hinge);
            }
            else if (id == InkSkinId.StormScarf)
            {
                float tail = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, .18f, uv.y)) *
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.67f, .88f, uv.x));
                delta = new Vector2(.024f * Mathf.Sin(time * 3f + y * 6f), .018f * Mathf.Sin(time * 2.5f + x * 5f)) * tail;
            }
            else if (id == InkSkinId.DragonTail)
            {
                float tip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, .88f, uv.x));
                delta = new Vector2(.010f * Mathf.Sin(time * 2.2f + x * 4f), .028f * Mathf.Sin(time * 2.2f + x * 4f)) * tip;
            }
            else if (id == InkSkinId.PaperLanterns)
                delta = new Vector2(.018f * Mathf.Sin(time * 1.5f + x * 8f), .018f * Mathf.Sin(time * 1.9f + x * 6f + 1f));
            else if (id == InkSkinId.MoonClouds)
                delta = new Vector2(.018f * Mathf.Sin(time * 1.1f + y * 5f), .016f * Mathf.Sin(time * 1.4f + x * 8f));
            else
            {
                float flame = Mathf.Lerp(.25f, 1f, uv.y);
                delta = new Vector2((.018f * Mathf.Sin(time * 3.2f + x * 12f + y * 8f) +
                    .006f * Mathf.Sin(time * 5.1f - y * 13f)) * flame,
                    .018f * Mathf.Sin(time * 2.4f + x * 9f) + .009f * Mathf.Sin(time * 3.7f + y * 7f) * flame);
            }
            delta.x = Mathf.Clamp(delta.x, -Padding, Padding);
            delta.y = Mathf.Clamp(delta.y, -Padding, Padding);
            return uv + delta * Mathf.Clamp01(strength);
        }
    }
}
