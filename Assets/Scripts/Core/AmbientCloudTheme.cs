using System;
using UnityEngine;

namespace MukJump.Core
{
    public enum AmbientCloudMotion { Clouds, Wind, RainMist, CliffHaze, GateDust, LotusMist, RiverFlow }

    /// 맵 번호가 아닌 배경 원화 참조로 연결한다. 맵 누락·무한 맵 추가에도 순서가 어긋나지 않는다.
    [Serializable]
    public sealed class AmbientCloudTheme
    {
        public Sprite background;
        public Sprite[] sprites;
        public AmbientCloudMotion motion;
    }

    public readonly struct AmbientCloudMotionProfile
    {
        public readonly float Flow, Sway, Roll, Breath, Period;
        AmbientCloudMotionProfile(float flow, float sway, float roll, float breath, float period)
        {
            Flow = flow; Sway = sway; Roll = roll; Breath = breath; Period = period;
        }

        // 화면 폭/높이 비율, 각도, 초. 전경처럼 보이는 빠른 이동·점멸은 사용하지 않는다.
        public static AmbientCloudMotionProfile For(AmbientCloudMotion motion) => motion switch
        {
            AmbientCloudMotion.Wind => new(1.15f, 0.006f, 0.4f, 0f, 85f),
            AmbientCloudMotion.RainMist => new(-0.7f, 0.009f, 0f, 0.04f, 105f),
            AmbientCloudMotion.CliffHaze => new(0.45f, 0.016f, 0.5f, 0.06f, 130f),
            AmbientCloudMotion.GateDust => new(0.6f, 0.01f, 1.2f, 0.10f, 110f),
            AmbientCloudMotion.LotusMist => new(-0.45f, 0.012f, 1.4f, 0.08f, 145f),
            AmbientCloudMotion.RiverFlow => new(0.85f, 0.014f, 0.8f, 0.06f, 120f),
            _ => new(1f, 0f, 0f, 0f, 100f),
        };
    }
}
