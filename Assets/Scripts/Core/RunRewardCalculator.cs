using UnityEngine;

namespace MukJump.Core
{
    /// 영구 성장 v3의 한 판 먹빛 보상을 부작용 없이 계산한다.
    public static class RunRewardCalculator
    {
        public const int TutorialReward = 1;
        public const int MinimumProgressHeight = 12;
        public const float MinimumActiveSeconds = 20f;

        static readonly int[] FirstBestMilestones =
        {
            100,
            250,
            500,
            750,
            1000,
        };

        public static int Calculate(
            int swarmProgressHeight,
            int scoreHeight,
            int previousBest,
            float activeGameplaySeconds,
            bool includeFirstEligibleReward)
        {
            int reward = includeFirstEligibleReward
                ? TutorialReward
                : swarmProgressHeight >= MinimumProgressHeight &&
                  activeGameplaySeconds >= MinimumActiveSeconds
                    ? 1
                    : 0;

            int safeScore = Mathf.Max(0, scoreHeight);
            int safePreviousBest = Mathf.Max(0, previousBest);
            for (int i = 0; i < FirstBestMilestones.Length; i++)
            {
                int milestone = FirstBestMilestones[i];
                if (safePreviousBest < milestone && safeScore >= milestone)
                    reward++;
            }
            return reward;
        }

        /// 구 도구 호환. 실제 게임 정산은 진행 고도와 플레이 시간을 분리한다.
        public static int Calculate(
            int height,
            int previousBest,
            bool includeFirstEligibleReward)
        {
            return Calculate(
                height,
                height,
                previousBest,
                float.PositiveInfinity,
                includeFirstEligibleReward);
        }
    }
}
