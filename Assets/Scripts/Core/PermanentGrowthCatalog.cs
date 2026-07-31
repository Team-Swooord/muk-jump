using System;
using System.Collections.Generic;

namespace MukJump.Core
{
    /// 게임이 끝나도 저장되는 먹방울이 성장 종류.
    /// 한 판 전용 GrowthUpgradeType과 enum·저장 ID를 공유하지 않는다.
    public enum PermanentGrowthType
    {
        InkCapacity = 0,
        InkRecovery = 1,
        PlatformLifetime = 2,
        JumpCharge = 3,
        Vitality = 4,
        DamageGrace = 5,
        LastBreath = 6,
        JumpPower = 7,
        DrawnPlatformLeap = 8,
        StrokeGuard = 9,
    }

    /// 영구 성장 화면의 세 가지 큰 계보.
    public enum PermanentGrowthBranch
    {
        Survival,
        Leap,
        InkHandling,
    }

    public enum PermanentGrowthNodeKind
    {
        Stat,
        Capstone,
    }

    /// UI가 수치 뒤에 붙일 단위를 안전하게 고를 수 있도록 원시 효과값의 의미를 구분한다.
    public enum PermanentGrowthValueKind
    {
        Percent,
        Flat,
        Seconds,
    }

    public readonly struct PermanentGrowthRequirement
    {
        public PermanentGrowthRequirement(
            PermanentGrowthType type,
            int minimumLevel)
        {
            Type = type;
            MinimumLevel = Math.Max(1, minimumLevel);
        }

        public PermanentGrowthType Type { get; }
        public int MinimumLevel { get; }
    }

    public readonly struct PermanentGrowthBranchMetadata
    {
        public PermanentGrowthBranchMetadata(
            PermanentGrowthBranch branch,
            string displayName,
            string description,
            int displayOrder)
        {
            Branch = branch;
            DisplayName = displayName;
            Description = description;
            DisplayOrder = displayOrder;
        }

        public PermanentGrowthBranch Branch { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int DisplayOrder { get; }
    }

    public sealed class PermanentGrowthDefinition
    {
        readonly int[] costs;
        readonly PermanentGrowthRequirement[] requirements;

        public PermanentGrowthDefinition(
            string id,
            PermanentGrowthType type,
            PermanentGrowthBranch branch,
            PermanentGrowthNodeKind nodeKind,
            int branchOrder,
            string name,
            string description,
            string effectUnit,
            float effectPerLevel,
            bool reducesValue,
            PermanentGrowthValueKind valueKind,
            PermanentGrowthRequirement[] requirements,
            params int[] costs)
        {
            Id = id;
            Type = type;
            Branch = branch;
            NodeKind = nodeKind;
            BranchOrder = branchOrder;
            Name = name;
            Description = description;
            EffectUnit = effectUnit;
            EffectPerLevel = effectPerLevel;
            ReducesValue = reducesValue;
            ValueKind = valueKind;
            this.requirements = requirements ?? Array.Empty<PermanentGrowthRequirement>();
            this.costs = costs ?? Array.Empty<int>();
        }

        public string Id { get; }
        public PermanentGrowthType Type { get; }
        public PermanentGrowthBranch Branch { get; }
        public PermanentGrowthNodeKind NodeKind { get; }
        public bool IsCapstone => NodeKind == PermanentGrowthNodeKind.Capstone;
        public int BranchOrder { get; }
        public string Name { get; }
        public string Description { get; }
        public string EffectUnit { get; }
        public float EffectPerLevel { get; }
        public bool ReducesValue { get; }
        public PermanentGrowthValueKind ValueKind { get; }
        public IReadOnlyList<PermanentGrowthRequirement> Requirements => requirements;
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

        public float GetDisplayValueAtLevel(int level)
        {
            float value =
                Math.Max(0, Math.Min(level, MaxLevel)) * EffectPerLevel;
            return ValueKind == PermanentGrowthValueKind.Percent
                ? value * 100f
                : value;
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
                PermanentGrowthBranch.InkHandling,
                PermanentGrowthNodeKind.Stat,
                0,
                "먹그릇",
                "한 번에 품을 수 있는 기본 먹이 늘어납니다",
                "최대 먹",
                0.015f,
                false,
                PermanentGrowthValueKind.Percent,
                Array.Empty<PermanentGrowthRequirement>(),
                6, 10, 16, 24, 34, 46),
            new(
                "permanent.ink_recovery",
                PermanentGrowthType.InkRecovery,
                PermanentGrowthBranch.InkHandling,
                PermanentGrowthNodeKind.Stat,
                1,
                "숨고르기",
                "붓을 쉬는 동안 기본 먹 회복이 빨라집니다",
                "먹 회복",
                0.02f,
                false,
                PermanentGrowthValueKind.Percent,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.InkCapacity,
                        2)),
                6, 10, 16, 24, 34, 46),
            new(
                "permanent.platform_lifetime",
                PermanentGrowthType.PlatformLifetime,
                PermanentGrowthBranch.InkHandling,
                PermanentGrowthNodeKind.Stat,
                2,
                "먹결",
                "그린 임시 발판의 기본 여운이 길어집니다",
                "발판 수명",
                0.0125f,
                false,
                PermanentGrowthValueKind.Percent,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.InkRecovery,
                        2)),
                7, 11, 17, 25, 35, 47),
            new(
                "permanent.jump_charge",
                PermanentGrowthType.JumpCharge,
                PermanentGrowthBranch.Leap,
                PermanentGrowthNodeKind.Stat,
                0,
                "발놀림",
                "다음 자동 점프를 준비하는 시간이 짧아집니다",
                "충전 시간",
                0.0075f,
                true,
                PermanentGrowthValueKind.Percent,
                Array.Empty<PermanentGrowthRequirement>(),
                7, 12, 18, 26, 36, 48),
            new(
                "permanent.vitality",
                PermanentGrowthType.Vitality,
                PermanentGrowthBranch.Survival,
                PermanentGrowthNodeKind.Stat,
                0,
                "먹심",
                "한 판을 시작할 때 기본 최대 체력이 한 칸 늘어납니다",
                "최대 체력",
                1f,
                false,
                PermanentGrowthValueKind.Flat,
                Array.Empty<PermanentGrowthRequirement>(),
                24),
            new(
                "permanent.damage_grace",
                PermanentGrowthType.DamageGrace,
                PermanentGrowthBranch.Survival,
                PermanentGrowthNodeKind.Stat,
                1,
                "먹숨",
                "피격 뒤 다시 다치지 않는 시간이 길어집니다",
                "피격 여유",
                0.08f,
                false,
                PermanentGrowthValueKind.Seconds,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.Vitality,
                        1)),
                8, 16, 28),
            new(
                "permanent.last_breath",
                PermanentGrowthType.LastBreath,
                PermanentGrowthBranch.Survival,
                PermanentGrowthNodeKind.Capstone,
                2,
                "마지막 먹숨",
                "한 판에 한 번 치명적인 장애물 피해를 견딥니다",
                "최종 패시브",
                1f,
                false,
                PermanentGrowthValueKind.Flat,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.Vitality,
                        1),
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.DamageGrace,
                        3)),
                56),
            new(
                "permanent.jump_power",
                PermanentGrowthType.JumpPower,
                PermanentGrowthBranch.Leap,
                PermanentGrowthNodeKind.Stat,
                1,
                "먹도약",
                "기본 자동 점프의 힘이 조금씩 강해집니다",
                "점프 힘",
                0.01f,
                false,
                PermanentGrowthValueKind.Percent,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.JumpCharge,
                        3)),
                8, 13, 19, 27, 37),
            new(
                "permanent.drawn_platform_leap",
                PermanentGrowthType.DrawnPlatformLeap,
                PermanentGrowthBranch.Leap,
                PermanentGrowthNodeKind.Capstone,
                2,
                "먹결 도약",
                "직접 그린 발판에서 자동 점프 힘이 크게 늘어납니다",
                "먹발판 도약",
                0.10f,
                false,
                PermanentGrowthValueKind.Percent,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.JumpCharge,
                        6),
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.JumpPower,
                        5)),
                52),
            new(
                "permanent.stroke_guard",
                PermanentGrowthType.StrokeGuard,
                PermanentGrowthBranch.InkHandling,
                PermanentGrowthNodeKind.Capstone,
                3,
                "굳은 먹결",
                "새로 그린 발판이 낙묵석 한 번을 견딥니다",
                "최종 패시브",
                1f,
                false,
                PermanentGrowthValueKind.Flat,
                Requirements(
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.InkCapacity,
                        6),
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.InkRecovery,
                        6),
                    new PermanentGrowthRequirement(
                        PermanentGrowthType.PlatformLifetime,
                        6)),
                56),
        };

        static readonly PermanentGrowthBranchMetadata[] BranchDefinitions =
        {
            new(
                PermanentGrowthBranch.Survival,
                "생존",
                "피격을 견디고 마지막 한 번을 버티는 먹의 몸",
                0),
            new(
                PermanentGrowthBranch.Leap,
                "도약",
                "자동 점프의 준비와 힘을 다듬는 먹의 발",
                1),
            new(
                PermanentGrowthBranch.InkHandling,
                "먹 운용",
                "먹의 양과 회복, 그린 발판을 다루는 먹의 결",
                2),
        };

        public static IReadOnlyList<PermanentGrowthDefinition> All => Definitions;
        public static IReadOnlyList<PermanentGrowthBranchMetadata> Branches =>
            BranchDefinitions;

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

        public static PermanentGrowthBranchMetadata GetBranch(
            PermanentGrowthBranch branch)
        {
            for (int i = 0; i < BranchDefinitions.Length; i++)
                if (BranchDefinitions[i].Branch == branch)
                    return BranchDefinitions[i];
            return default;
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

        static PermanentGrowthRequirement[] Requirements(
            params PermanentGrowthRequirement[] values)
        {
            return values ?? Array.Empty<PermanentGrowthRequirement>();
        }
    }
}
