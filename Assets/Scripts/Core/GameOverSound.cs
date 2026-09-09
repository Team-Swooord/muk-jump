using System;

namespace MukJump.Core
{
    /// 음정 진행이나 종소리 없이 낮은 북 한 타와 먹이 흩어지는 마찰 잔향을 합성한다.
    public static class GameOverSound
    {
        public const int SampleRate = 44100;
        public const float Duration = .85f;

        public static float[] BuildSamples()
        {
            var samples = new float[(int)(SampleRate * Duration)];
            uint noiseState = 0x61C88647;
            double softNoise = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                double noise = (noiseState >> 8) / 8388607.5 - 1;
                softNoise += .095 * (noise - softNoise);
                double attack = Math.Min(1, t / .004);
                // 한 번만 치는 낮은 북: 짧게 감쇠하는 비정수 배음으로 선율감을 없앤다.
                double drum = attack * (
                    .42 * Math.Sin(2 * Math.PI * 92 * t) * Math.Exp(-14 * t) +
                    .19 * Math.Sin(2 * Math.PI * 151 * t) * Math.Exp(-23 * t) +
                    .08 * Math.Sin(2 * Math.PI * 207 * t) * Math.Exp(-34 * t));
                double brush = softNoise * attack * (.32 * Math.Exp(-12 * t) +
                    .12 * Math.Exp(-5.5 * t)) + noise * attack * .045 * Math.Exp(-65 * t);
                double tail = Math.Clamp((Duration - t) / .24, 0, 1);
                tail = tail * tail * (3 - 2 * tail);
                samples[i] = (float)((drum + brush) * tail);
            }
            return samples;
        }

    }
}
