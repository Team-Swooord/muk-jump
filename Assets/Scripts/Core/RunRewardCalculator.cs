using System;

namespace MukJump.Core
{
    /// <summary>
    /// 결과 고도를 누적해 초반에는 자주, 후반에는 500m마다 먹빛을 지급한다(v11).
    /// 구 문턱은 기존 저장 검증·이관에서만 사용하며 변경하지 않는다.
    /// </summary>
    public static class RunRewardCalculator
    {
        public const int MaxRewardCount = 244;
        public const int MetersPerReward = 500;
        public const long FinalRewardDistance = 112150L;

        public static long GetThresholdForRewardCount(int rewardCount)
        {
            int n = Math.Clamp(rewardCount, 0, MaxRewardCount);
            if (n == 0) return 0L;
            if (n <= 4) return 50L + (n - 1) * 100L;
            if (n <= 12) return 350L + (n - 4) * 150L;
            if (n <= 24) return 1550L + (n - 12) * 250L;
            if (n <= 40) return 4550L + (n - 24) * 350L;
            return 10150L + (n - 40) * MetersPerReward;
        }

        public static int GetRequiredMetersForNextReward(int claimedRewardCount)
        {
            if (claimedRewardCount <= 0) return 50;
            if (claimedRewardCount < 4) return 100;
            if (claimedRewardCount < 12) return 150;
            if (claimedRewardCount < 24) return 250;
            if (claimedRewardCount < 40) return 350;
            return MetersPerReward;
        }

        public static int GetRewardCountForDistance(long cumulativeDistanceMeters)
        {
            int low = 0, high = MaxRewardCount;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (GetThresholdForRewardCount(middle) <= cumulativeDistanceMeters) low = middle;
                else high = middle - 1;
            }
            return low;
        }

        // v10 저장은 반드시 당시의 500m 규칙으로 검증한 뒤 이관한다.
        public static long GetV10ThresholdForRewardCount(int rewardCount) =>
            (long)Math.Clamp(rewardCount, 0, MaxRewardCount) * MetersPerReward;

        public static int GetV10RewardCountForDistance(long distance) =>
            (int)Math.Min(MaxRewardCount, Math.Max(0L, distance) / MetersPerReward);

        public static long GetMigratedV10RewardDistance(long actualDistance, long oldOffset, int claimed)
        {
            long actual = Math.Max(0L, actualDistance);
            long remainder = claimed >= MaxRewardCount ? 0L : Math.Max(0L,
                SaturatingAdd(actual, oldOffset) - GetV10ThresholdForRewardCount(claimed));
            // 실제 누적 거리의 새 보상과 구 계정의 기수령분+미지급 거리를 모두 보존한다.
            // 구 버전의 가상 보정 거리를 새 곡선에 그대로 넣어 과다 지급하지 않는다.
            return Math.Max(actual, SaturatingAdd(GetThresholdForRewardCount(claimed), remainder));
        }

        /// <summary>이미 받은 먹빛 개수에 대응하는 누적 거리 문턱.</summary>
        public static long GetLegacyThresholdForRewardCount(int rewardCount)
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
        public static int GetLegacyRewardCountForDistance(long cumulativeDistanceMeters)
        {
            long safeDistance = Math.Max(0L, cumulativeDistanceMeters);
            if (safeDistance >= 34500L)
                return MaxRewardCount;

            int low = 0;
            int high = MaxRewardCount;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (GetLegacyThresholdForRewardCount(middle) <= safeDistance)
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
            => SaturatingAdd(value, (long)addition);

        public static long SaturatingAdd(long value, long addition)
        {
            long safeValue = Math.Max(0L, value);
            long safeAddition = Math.Max(0L, addition);
            return safeValue > long.MaxValue - safeAddition
                ? long.MaxValue
                : safeValue + safeAddition;
        }
    }
}
