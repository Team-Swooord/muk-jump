using System;

namespace MukJump.Core
{
    /// 스팀 음원을 복제하지 않는 짧은 두 음의 알림. 현을 튕긴 듯한 어택만 남기고 잔향을 억제한다.
    public static class InkAchievementNoticeSound
    {
        public const int SampleRate = 44100;
        public const float Duration = .48f;
        public const float Volume = .5f;

        public static float[] BuildSamples()
        {
            var samples = new float[(int)(SampleRate * Duration)];
            double peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)SampleRate;
                double value = Note(t, 659.255, .7) + Note(t - .105, 880, 1);
                double tail = Math.Clamp((Duration - t) / .08, 0, 1);
                samples[i] = (float)(value * tail * tail * (3 - 2 * tail));
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            float gain = peak > 0 ? (float)(.65 / peak) : 0;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }

        static double Note(double age, double frequency, double gain)
        {
            if (age < 0) return 0;
            double attack = Math.Min(1, age / .004);
            double phase = 2 * Math.PI * frequency * age;
            return gain * attack * attack * (3 - 2 * attack) *
                (Math.Sin(phase) * Math.Exp(-16 * age) +
                 .18 * Math.Sin(phase * 2.002) * Math.Exp(-35 * age));
        }
    }
}
