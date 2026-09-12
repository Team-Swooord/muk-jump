using System;

namespace MukJump.Core
{
    /// 짧은 현 네 음을 위로 튕기는 해금 전용 소리. 외부 음원·게임 난수를 사용하지 않는다.
    public static class InkSkinUnlockSound
    {
        public const int SampleRate = 44100;
        public const float Duration = .72f;
        public const float Volume = .68f;

        public static float[] BuildSamples()
        {
            var samples = new float[(int)(SampleRate * Duration)];
            double[] starts = { 0, .058, .122, .202 };
            double[] notes = { 587.3295, 739.9888, 880, 1174.659 };
            double[] gains = { .72, .60, .66, .92 };
            uint noiseState = 0x741F295B;
            double filteredNoise = 0, peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate, value = 0;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                filteredNoise += .22 * ((noiseState >> 8) / 8388607.5 - 1 - filteredNoise);
                for (int note = 0; note < notes.Length; note++)
                {
                    double age = t - starts[note];
                    if (age < 0) continue;
                    double attack = Math.Min(1, age / .0035);
                    attack *= attack * (3 - 2 * attack);
                    double phase = 2 * Math.PI * notes[note] * age;
                    // 금속성 긴 종 대신 부드러운 현 몸통과 빠르게 사라지는 목질 어택.
                    double pluck = Math.Sin(phase) * Math.Exp(-10 * age)
                        + .30 * Math.Sin(phase * 2.003) * Math.Exp(-22 * age)
                        + .11 * Math.Sin(phase * 3.008) * Math.Exp(-34 * age)
                        + .10 * filteredNoise * Math.Exp(-95 * age);
                    value += gains[note] * attack * pluck;
                }
                double release = Math.Clamp((Duration - t) / .09, 0, 1);
                release *= release * (3 - 2 * release);
                samples[i] = (float)(value * release);
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            // 한 클립 안의 겹침을 먼저 제한해 효과음 설정을 높여도 디지털 클리핑을 만들지 않는다.
            float gain = peak > 0 ? (float)(.76 / peak) : 0;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }
    }
}
