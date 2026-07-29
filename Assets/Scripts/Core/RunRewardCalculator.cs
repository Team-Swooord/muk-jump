using UnityEngine;

namespace MukJump.Core
{
    /// 최고 점수와 분리된 영구 성장 재화 보상을 순수 계산한다.
    public static class RunRewardCalculator
    {
        public const int TutorialReward = 6;
        public const int BaseRewardCap = 40;
        public const int BestBandMeters = 50;
        public const int MaxBestBandBonus = 6;

        public static int Calculate(
            int height,
            int previousBest,
            bool includeTutorialReward)
        {
            int safeHeight = Mathf.Max(0, height);
            int reward = 0;
            if (safeHeight >= 10)
            {
                reward = Mathf.Min(
                    BaseRewardCap,
                    Mathf.FloorToInt(
                        2f +
                        0.9f * Mathf.Sqrt(safeHeight) +
                        0.015f * safeHeight));
            }

            int previousBand = Mathf.Max(0, previousBest) / BestBandMeters;
            int reachedBand = safeHeight / BestBandMeters;
            int bandBonus = Mathf.Min(
                MaxBestBandBonus,
                Mathf.Max(0, reachedBand - previousBand) * 2);
            reward += bandBonus;

            if (includeTutorialReward)
                reward += TutorialReward;
            return Mathf.Max(0, reward);
        }
    }
}
