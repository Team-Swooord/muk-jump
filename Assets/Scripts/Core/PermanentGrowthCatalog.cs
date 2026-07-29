using System;
using System.Collections.Generic;

namespace MukJump.Core
{
    /// 게임이 끝나도 저장되는 먹방울이 성장 종류.
    /// 한 판 전용 GrowthUpgradeType과 enum·저장 ID를 공유하지 않는다.
    public enum PermanentGrowthType
    {
        InkCapacity,
        InkRecovery,
        PlatformLifetime,
        JumpCharge,
    }

    public sealed class PermanentGrowthDefinition
    {
        readonly int[] costs;

        public PermanentGrowthDefinition(
            string id,
            PermanentGrowthType type,
            string name,
            string description,
            string effectUnit,
            float effectPerLevel,
            bool reducesValue,
            params int[] costs)
        {
            Id = id;
            Type = type;
            Name = name;
            Description = description;
            EffectUnit = effectUnit;
            EffectPerLevel = effectPerLevel;
            ReducesValue = reducesValue;
            this.costs = costs ?? Array.Empty<int>();
        }

        public string Id { get; }
        public PermanentGrowthType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public string EffectUnit { get; }
        public float EffectPerLevel { get; }
        public bool ReducesValue { get; }
        public int MaxLevel => costs.Length;

        public int GetCost(int currentLevel)
        {
            return currentLevel >= 0 && currentLevel < costs.Length
                ? costs[currentLevel]
                : 0;
        }

        public int CostThroughLevel(int level)
        {
            int total = 0;
            int count = Math.Min(Math.Max(0, level), costs.Length);
            for (int i = 0; i < count; i++)
                total += Math.Max(0, costs[i]);
            return total;
        }

        public float GetPercentAtLevel(int level)
        {
            return Math.Max(0, Math.Min(level, MaxLevel)) * EffectPerLevel * 100f;
        }
    }

    /// 영구 성장의 이름·상한·비용·효과 수치를 소유하는 단일 진실 원천.
    public static class PermanentGrowthCatalog
    {
        static readonly PermanentGrowthDefinition[] Definitions =
        {
            new(
                "permanent.ink_capacity",
                PermanentGrowthType.InkCapacity,
                "먹그릇",
                "한 번에 품을 수 있는 기본 먹이 늘어납니다",
                "최대 먹",
                0.015f,
                false,
                6, 10, 16, 24, 34, 46),
            new(
                "permanent.ink_recovery",
                PermanentGrowthType.InkRecovery,
                "숨고르기",
                "붓을 쉬는 동안 기본 먹 회복이 빨라집니다",
                "먹 회복",
                0.02f,
                false,
                6, 10, 16, 24, 34, 46),
            new(
                "permanent.platform_lifetime",
                PermanentGrowthType.PlatformLifetime,
                "먹결",
                "그린 임시 발판의 기본 여운이 길어집니다",
                "발판 수명",
                0.0125f,
                false,
                7, 11, 17, 25, 35, 47),
            new(
                "permanent.jump_charge",
                PermanentGrowthType.JumpCharge,
                "발놀림",
                "다음 자동 점프를 준비하는 시간이 짧아집니다",
                "충전 시간",
                0.0075f,
                true,
                7, 12, 18, 26, 36, 48),
        };

        public static IReadOnlyList<PermanentGrowthDefinition> All => Definitions;

        public static int TotalCost
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Definitions.Length; i++)
                    total += Definitions[i].CostThroughLevel(Definitions[i].MaxLevel);
                return total;
            }
        }

        public static PermanentGrowthDefinition Get(PermanentGrowthType type)
        {
            for (int i = 0; i < Definitions.Length; i++)
                if (Definitions[i].Type == type)
                    return Definitions[i];
            return null;
        }

        public static bool TryGet(string id, out PermanentGrowthDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (!string.Equals(Definitions[i].Id, id, StringComparison.Ordinal))
                    continue;
                definition = Definitions[i];
                return true;
            }

            definition = null;
            return false;
        }
    }
}
