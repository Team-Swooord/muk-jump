using System;

namespace MukJump.Core
{
    /// <summary>
    /// 한 판의 결과 고도를 계정 누적 거리로 더해 먹빛 39단계를 계산한다.
    /// 문턱은 저장 호환 계약이므로 밸런스 버전 없이 임의로 바꾸지 않는다.
    /// </summary>
    public static class RunRewardCalculator
    {
        public const int MaxRewardCount = 39;
        public const long FinalRewardDistance = 3750L;

        /// <summary>이미 받은 먹빛 개수에 대응하는 누적 거리 문턱.</summary>
        public static long GetThresholdForRewardCount(int rewardCount)
        {
            int safeCount = Math.Clamp(rewardCount, 0, MaxRewardCount);
            if (safeCount <= 0)
                return 0L;
            if (safeCount <= 5)
                return 20L * safeCount;
            if (safeCount <= 13)
                return 100L + 50L * (safeCount - 5);
            if (safeCount <= 26)
                return 500L + 100L * (safeCount - 13);
            return 1800L + 150L * (safeCount - 26);
        }

        /// <summary>누적 거리에서 이미 통과한 먹빛 단계 수를 계산한다.</summary>
        public static int GetRewardCountForDistance(long cumulativeDistanceMeters)
        {
            long safeDistance = Math.Max(0L, cumulativeDistanceMeters);
            if (safeDistance >= FinalRewardDistance)
                return MaxRewardCount;

            int low = 0;
            int high = MaxRewardCount;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (GetThresholdForRewardCount(middle) <= safeDistance)
                    low = middle;
                else
                    high = middle - 1;
            }
            return low;
        }

        public static long GetPreviousRewardDistance(int claimedRewardCount) =>
            GetThresholdForRewardCount(claimedRewardCount);

        public static long GetNextRewardDistance(int claimedRewardCount) =>
            claimedRewardCount >= MaxRewardCount
                ? FinalRewardDistance
                : GetThresholdForRewardCount(claimedRewardCount + 1);

        public static long GetDistanceToNextReward(
            long cumulativeDistanceMeters,
            int claimedRewardCount)
        {
            if (claimedRewardCount >= MaxRewardCount)
                return 0L;
            return Math.Max(
                0L,
                GetNextRewardDistance(claimedRewardCount) -
                Math.Max(0L, cumulativeDistanceMeters));
        }

        public static int CalculateEarnedRewardCount(
            long cumulativeDistanceBeforeRun,
            int runDistanceMeters,
            int claimedRewardCount)
        {
            long total = SaturatingAdd(
                Math.Max(0L, cumulativeDistanceBeforeRun),
                Math.Max(0, runDistanceMeters));
            int reached = GetRewardCountForDistance(total);
            return Math.Max(
                0,
                reached - Math.Clamp(
                    claimedRewardCount,
                    0,
                    MaxRewardCount));
        }

        public static long SaturatingAdd(long value, int addition)
        {
            long safeValue = Math.Max(0L, value);
            int safeAddition = Math.Max(0, addition);
            return safeValue > long.MaxValue - safeAddition
                ? long.MaxValue
                : safeValue + safeAddition;
        }
    }
}
