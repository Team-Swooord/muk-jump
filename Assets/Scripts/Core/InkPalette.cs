using UnityEngine;

namespace MukJump.Core
{
    /// 수묵/한지 아트 팔레트 (CLAUDE.md 8절 확정 값)
    public static class InkPalette
    {
        public static readonly Color Ink = FromHex(0x1C1B1A);
        public static readonly Color Ink2 = FromHex(0x26241F);
        public static readonly Color Paper = FromHex(0xEAE3D2);
        public static readonly Color Paper2 = FromHex(0xDFD6BE);
        public static readonly Color Red = FromHex(0xAE1C3C);
        public static readonly Color Gold = FromHex(0x9C7A3C);
        public static readonly Color TextDark = FromHex(0x2B2620);
        public static readonly Color TextMuted = FromHex(0x6B6355);
        public static readonly Color TextLight = FromHex(0xF5F1E6);

        static Color FromHex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
        }
    }
}
