using System;
using System.Collections.Generic;
using System.Linq;

namespace MukJump.Core
{
    /// 영구 성장의 실제 효과 ID. 기존 0~10 값은 구 저장 마이그레이션을 위해 보존한다.
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
        CloneSpawnGrace = 10,
        HitHorizontalStability = 11,
        HitReboundControl = 12,
        HitInkRecovery = 13,
        StableHit = 14,
        CloneSourceGrace = 15,
        CloneDeathHeal = 16,
        CloneBond = 17,
        DrawnChargeRhythm = 18,
        ConsecutiveLandingRhythm = 19,
        ShortPlatformControl = 20,
        ApexHang = 21,
        FallControl = 22,
        WindControl = 23,
        LastFallBrake = 24,
        ShortStrokeEfficiency = 25,
        IdleStrokeEfficiency = 26,
        NaturalExpiryRefund = 27,
        DrawnLandingInk = 28,
        LowInkRecovery = 29,
        FirstLandingPause = 30,
    }

    public enum PermanentGrowthBranch
    {
        Survival,
        Leap,
        InkHandling,
    }

    public enum PermanentGrowthNodeKind
    {
        Root,
        Stat,
        Mechanic,
        Keystone,
    }

    public enum PermanentGrowthValueKind
    {
        Percent,
        Flat,
        Seconds,
    }

    /// 구 테스트·도구가 효과 그룹을 읽을 때 사용하는 호환 구조다.
    /// 구매 그래프의 단일 진실 원천은 PermanentGrowthNodeDefinition이다.
    public sealed class PermanentGrowthDefinition
    {
        readonly int[] costs;

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
            this.costs = costs ?? Array.Empty<int>();
        }

        public string Id { get; }
        public PermanentGrowthType Type { get; }
        public PermanentGrowthBranch Branch { get; }
        public PermanentGrowthNodeKind NodeKind { get; }
        public bool IsCapstone => NodeKind == PermanentGrowthNodeKind.Keystone;
        public int BranchOrder { get; }
        public string Name { get; }
        public string Description { get; }
        public string EffectUnit { get; }
        public float EffectPerLevel { get; }
        public bool ReducesValue { get; }
        public PermanentGrowthValueKind ValueKind { get; }
        public int MaxLevel => costs.Length;
        public IReadOnlyList<PermanentGrowthRequirement> Requirements =>
            Array.Empty<PermanentGrowthRequirement>();

        public int GetCost(int currentLevel) =>
            currentLevel >= 0 && currentLevel < costs.Length
                ? costs[currentLevel]
                : 0;

        public int CostThroughLevel(int level) =>
            Math.Clamp(level, 0, costs.Length);

        public float GetDisplayValueAtLevel(int level)
        {
            float value = Math.Clamp(level, 0, MaxLevel) * EffectPerLevel;
            return ValueKind == PermanentGrowthValueKind.Percent
                ? value * 100f
                : value;
        }
    }

    /// 구 저장의 선행조건 타입을 역직렬화하는 호환 구조다.
    public readonly struct PermanentGrowthRequirement
    {
        public PermanentGrowthRequirement(PermanentGrowthType type, int minimumLevel)
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

    /// 한 번만 해금하는 열매 하나의 완전한 정의. 이름·아이콘·효과·부모를 직접 소유한다.
    public sealed class PermanentGrowthNodeDefinition
    {
        readonly string[] parentIds;

        public PermanentGrowthNodeDefinition(
            string id,
            string displayName,
            string description,
            string effectSummary,
            string iconKey,
            PermanentGrowthType effectId,
            float effectValue,
            string effectUnit,
            PermanentGrowthValueKind valueKind,
            bool reducesValue,
            PermanentGrowthBranch branch,
            PermanentGrowthNodeKind nodeKind,
            string[] parentIds,
            int requiredOwnedCountInBranch,
            string keystoneGroup,
            float layoutX,
            float layoutY,
            int effectRank = 1)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            EffectSummary = effectSummary;
            IconKey = iconKey;
            EffectId = effectId;
            EffectValue = effectValue;
            EffectUnit = effectUnit;
            ValueKind = valueKind;
            ReducesValue = reducesValue;
            Branch = branch;
            NodeKind = nodeKind;
            this.parentIds = parentIds ?? Array.Empty<string>();
            RequiredOwnedCountInBranch = Math.Max(0, requiredOwnedCountInBranch);
            KeystoneGroup = keystoneGroup ?? string.Empty;
            LayoutX = layoutX;
            LayoutY = layoutY;
            Rank = Math.Max(1, effectRank);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Name => DisplayName;
        public string Description { get; }
        public string EffectSummary { get; }
        public string IconKey { get; }
        public PermanentGrowthType EffectId { get; }
        public PermanentGrowthType Type => EffectId;
        public float EffectValue { get; }
        public string EffectUnit { get; }
        public PermanentGrowthValueKind ValueKind { get; }
        public bool ReducesValue { get; }
        public PermanentGrowthBranch Branch { get; }
        public PermanentGrowthNodeKind NodeKind { get; }
        public bool IsKeystone => NodeKind == PermanentGrowthNodeKind.Keystone;
        public IReadOnlyList<string> ParentIds => parentIds;
        public int RequiredOwnedCountInBranch { get; }
        public string KeystoneGroup { get; }
        public float LayoutX { get; }
        public float LayoutY { get; }
        public int Cost => 1;
        public int Rank { get; }
    }

    /// 영구 성장 v3의 39개 stable node ID와 해금 그래프를 소유한다.
    public static class PermanentGrowthCatalog
    {
        static readonly PermanentGrowthBranchMetadata[] BranchDefinitions =
        {
            new(
                PermanentGrowthBranch.Survival,
                "생존",
                "개체 내구·피격 안정·먹떼 연계",
                0),
            new(
                PermanentGrowthBranch.InkHandling,
                "먹 운용",
                "먹 절약·회복 순환·발판 유지",
                1),
            new(
                PermanentGrowthBranch.Leap,
                "도약",
                "자동 점프 박자·짧은 발판·낙하 대응",
                2),
        };

        // UI 호환을 위해 먹 운용 뿌리를 첫 슬롯에 둔다. 그래프 자체는 세 뿌리가 독립이다.
        static readonly PermanentGrowthNodeDefinition[] NodeDefinitions =
            BuildNodeDefinitions();
        static readonly PermanentGrowthDefinition[] Definitions =
            BuildEffectDefinitions();
        static readonly Dictionary<string, PermanentGrowthNodeDefinition> NodesById =
            NodeDefinitions.ToDictionary(node => node.Id, StringComparer.Ordinal);

        public static IReadOnlyList<PermanentGrowthDefinition> All => Definitions;
        public static IReadOnlyList<PermanentGrowthNodeDefinition> Nodes =>
            NodeDefinitions;
        public static IReadOnlyList<PermanentGrowthBranchMetadata> Branches =>
            BranchDefinitions;
        public static int TotalCost => NodeDefinitions.Length;

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

        public static bool TryGet(
            string id,
            out PermanentGrowthDefinition definition)
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

        public static PermanentGrowthNodeDefinition GetNode(string id) =>
            TryGetNode(id, out PermanentGrowthNodeDefinition node) ? node : null;

        public static bool TryGetNode(
            string id,
            out PermanentGrowthNodeDefinition definition)
        {
            if (!string.IsNullOrEmpty(id) && NodesById.TryGetValue(id, out definition))
                return true;
            definition = null;
            return false;
        }

        /// 구 도구가 효과와 단계로 찾을 때 사용하는 호환 조회다.
        public static PermanentGrowthNodeDefinition GetNode(
            PermanentGrowthType type,
            int rank)
        {
            for (int i = 0; i < NodeDefinitions.Length; i++)
                if (NodeDefinitions[i].EffectId == type &&
                    NodeDefinitions[i].Rank == rank)
                    return NodeDefinitions[i];
            return null;
        }

        public static string GetNodeId(PermanentGrowthType type, int rank) =>
            GetNode(type, rank)?.Id ?? string.Empty;

        public static int CountGeneralNodes(PermanentGrowthBranch branch)
        {
            int count = 0;
            for (int i = 0; i < NodeDefinitions.Length; i++)
                if (NodeDefinitions[i].Branch == branch &&
                    !NodeDefinitions[i].IsKeystone)
                    count++;
            return count;
        }

        public static IReadOnlyList<string> MigrationOrder(
            PermanentGrowthBranch branch)
        {
            return branch switch
            {
                PermanentGrowthBranch.Survival => SurvivalMigrationOrder,
                PermanentGrowthBranch.Leap => LeapMigrationOrder,
                _ => InkMigrationOrder,
            };
        }

        static readonly string[] SurvivalMigrationOrder =
        {
            "S00", "S-A1", "S-B1", "S-C1", "S-A2", "S-B2", "S-C2",
            "S-A3", "S-B3", "S-C3", "S-KA", "S-KB", "S-KC",
        };

        static readonly string[] LeapMigrationOrder =
        {
            "J00", "J-A1", "J-B1", "J-C1", "J-A2", "J-B2", "J-C2",
            "J-A3", "J-B3", "J-C3", "J-KA", "J-KB", "J-KC",
        };

        static readonly string[] InkMigrationOrder =
        {
            "I00", "I-A1", "I-B1", "I-C1", "I-A2", "I-B2", "I-C2",
            "I-A3", "I-B3", "I-C3", "I-KA", "I-KB", "I-KC",
        };

        static PermanentGrowthNodeDefinition[] BuildNodeDefinitions()
        {
            var nodes = new List<PermanentGrowthNodeDefinition>(39);

            // 먹 운용 — 중앙 주가지
            Add(nodes, "I00", "작은 벼루", "먹을 담는 기본 그릇을 넓힙니다.",
                "최대 먹 +3%", "ink.capacity.seed", PermanentGrowthType.InkCapacity,
                0.03f, "최대 먹", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Root,
                null, 0, "", 0f, -1040f, 1);
            Add(nodes, "I-A1", "깊은 벼루", "벼루의 깊이를 더해 먹을 오래 품습니다.",
                "최대 먹 +3%", "ink.capacity.deep", PermanentGrowthType.InkCapacity,
                0.03f, "최대 먹", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", -350f, -770f, 2);
            Add(nodes, "I-A2", "마른 붓끝", "짧고 정확한 획의 먹 소모를 줄입니다.",
                "1.5m 이하 유효 획 비용 -8%", "ink.stroke.short",
                PermanentGrowthType.ShortStrokeEfficiency, 0.08f, "짧은 획 비용",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-A1"), 0, "", -430f, -430f);
            Add(nodes, "I-A3", "남겨 둔 먹", "잠시 붓을 쉬었다 그린 첫 획을 아낍니다.",
                "2초 휴식 뒤 첫 유효 획 비용 -10%", "ink.stroke.idle",
                PermanentGrowthType.IdleStrokeEfficiency, 0.10f, "휴식 획 비용",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-A2"), 0, "", -350f, -80f);
            Add(nodes, "I-KA", "되돌아온 먹", "자연스럽게 마른 발판의 먹 일부가 벼루로 돌아옵니다.",
                "자연 소멸 시 10% 환급 · 최대 0.6", "ink.refund.expiry",
                PermanentGrowthType.NaturalExpiryRefund, 0.10f, "환급",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-A3"), 6, "ink", -500f, 300f);

            Add(nodes, "I-B1", "첫 숨", "먹이 차오르는 기본 호흡을 빠르게 합니다.",
                "먹 회복 +4%", "ink.recovery.first", PermanentGrowthType.InkRecovery,
                0.04f, "먹 회복", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 0f, -710f, 1);
            Add(nodes, "I-B2", "고른 숨", "먹의 회복 호흡을 한 번 더 다듬습니다.",
                "먹 회복 +4%", "ink.recovery.even", PermanentGrowthType.InkRecovery,
                0.04f, "먹 회복", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-B1"), 0, "", 70f, -370f, 2);
            Add(nodes, "I-B3", "산속 먹샘", "직접 그린 발판에 착지하면 작은 먹샘이 솟습니다.",
                "착지 시 먹 0.20 회복 · 4초", "ink.recovery.landing",
                PermanentGrowthType.DrawnLandingInk, 0.20f, "착지 먹",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-B2"), 0, "", 0f, -20f);
            Add(nodes, "I-KB", "마르지 않는 벼루", "먹이 바닥날 때 회복이 빨라져 다시 획을 준비합니다.",
                "먹 25% 미만 회복 +30% · 40%까지", "ink.recovery.low",
                PermanentGrowthType.LowInkRecovery, 0.30f, "저먹 회복",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-B3"), 6, "ink", 0f, 360f);

            Add(nodes, "I-C1", "남은 획", "발판에 남는 먹의 여운을 늘립니다.",
                "발판 수명 +2%", "ink.platform.remaining",
                PermanentGrowthType.PlatformLifetime, 0.02f, "발판 수명",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 350f, -780f, 1);
            Add(nodes, "I-C2", "이어진 획", "발판의 여운을 한 번 더 이어 줍니다.",
                "발판 수명 +2%", "ink.platform.long", PermanentGrowthType.PlatformLifetime,
                0.02f, "발판 수명", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-C1"), 0, "", 430f, -450f, 2);
            Add(nodes, "I-C3", "붙잡은 획", "첫 착지 순간 발판이 잠시 마르지 않습니다.",
                "첫 착지 때 수명 감소 0.15초 정지", "ink.platform.pause",
                PermanentGrowthType.FirstLandingPause, 0.15f, "수명 정지",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-C2"), 0, "", 350f, -110f);
            Add(nodes, "I-KC", "굳은 먹결", "먹떼가 낙묵석 한 번을 받아내 발판을 지킵니다.",
                "낙묵석 방어 1회 · 공용 18초", "ink.platform.guard",
                PermanentGrowthType.StrokeGuard, 18f, "재사용",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-C3"), 6, "ink", 500f, 280f);

            // 생존 — 왼쪽 주가지
            Add(nodes, "S00", "먹피의 씨", "먹피가 굳어 연속 장애물 피해를 늦춥니다.",
                "피격 뒤 무적 +0.05초", "survival.guard.seed",
                PermanentGrowthType.DamageGrace, 0.05f, "피격 여유",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Root,
                null, 0, "", -700f, -980f, 1);
            Add(nodes, "S-A1", "얇은 먹피", "보호 먹피를 한 겹 더 두릅니다.",
                "피격 뒤 무적 +0.05초", "survival.guard.thin",
                PermanentGrowthType.DamageGrace, 0.05f, "피격 여유",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -1320f, -720f, 2);
            Add(nodes, "S-A2", "겹친 먹피", "겹친 먹피가 다음 충돌까지 시간을 벌어 줍니다.",
                "피격 뒤 무적 +0.05초", "survival.guard.layered",
                PermanentGrowthType.DamageGrace, 0.05f, "피격 여유",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-A1"), 0, "", -1420f, -390f, 3);
            Add(nodes, "S-A3", "깊은 먹심", "모든 현재·미래 먹방울이의 몸이 한 칸 단단해집니다.",
                "모든 먹방울이 최대 체력 +1", "survival.guard.heart",
                PermanentGrowthType.Vitality, 1f, "최대 체력",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-A2"), 0, "", -1320f, -60f);
            Add(nodes, "S-KA", "마지막 먹숨", "마지막 생존자가 장애물로 쓰러질 때 한 번 버팁니다. 추락은 막지 않습니다.",
                "한 판 1회 · 체력 1 · 무적 0.8초", "survival.keystone.last",
                PermanentGrowthType.LastBreath, 0.8f, "생존 무적",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-A3"), 6, "survival", -1480f, 300f);

            Add(nodes, "S-B1", "낮은 흔들림", "피격 뒤 수평 관성을 더 많이 보존합니다.",
                "수평 속도 보존 82% → 90%", "survival.stability.horizontal",
                PermanentGrowthType.HitHorizontalStability, 0.90f, "속도 보존",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -980f, -660f);
            Add(nodes, "S-B2", "굳은 중심", "장애물 반동이 아이템 점프처럼 솟지 않게 줄입니다.",
                "최소 상승 반동 1.6 → 1.3", "survival.stability.rebound",
                PermanentGrowthType.HitReboundControl, 1.3f, "최소 반동",
                PermanentGrowthValueKind.Flat, true,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-B1"), 0, "", -1040f, -330f);
            Add(nodes, "S-B3", "되찾은 먹", "비치명 피해가 먹의 일부를 되돌립니다.",
                "최대 먹 4% 회복 · 공용 8초", "survival.stability.ink",
                PermanentGrowthType.HitInkRecovery, 0.04f, "먹 회복",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-B2"), 0, "", -950f, 0f);
            Add(nodes, "S-KB", "흐트러지지 않음", "주기적으로 체력만 잃고 속도와 발판 접착은 지킵니다.",
                "피격 움직임 보존 · 공용 12초", "survival.keystone.stable",
                PermanentGrowthType.StableHit, 12f, "재사용",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-B3"), 6, "survival", -1000f, 360f);

            Add(nodes, "S-C1", "첫 분신숨", "새 분신이 태어난 뒤 장애물 보호를 더 받습니다.",
                "새 분신 보호 +0.15초", "survival.clone.first",
                PermanentGrowthType.CloneSpawnGrace, 0.15f, "분신 보호",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -650f, -720f);
            Add(nodes, "S-C2", "나눠진 숨", "분신을 만든 원본도 잠깐 장애물에서 보호됩니다.",
                "분신 생성 시 원본 보호 0.25초", "survival.clone.source",
                PermanentGrowthType.CloneSourceGrace, 0.25f, "원본 보호",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-C1"), 0, "", -560f, -400f);
            Add(nodes, "S-C3", "남은 먹맥", "분신이 사라지면 가장 약한 생존자에게 먹맥이 이어집니다.",
                "분신 사망 시 체력 +1 · 공용 30초", "survival.clone.heal",
                PermanentGrowthType.CloneDeathHeal, 30f, "재사용",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-C2"), 0, "", -650f, -70f);
            Add(nodes, "S-KC", "함께 맺힘", "먹분신 획득 순간 먹떼 전체가 하나의 숨을 나눕니다.",
                "최저 체력 +1 · 전체 보호 0.35초", "survival.keystone.bond",
                PermanentGrowthType.CloneBond, 0.35f, "전체 보호",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-C3"), 6, "survival", -520f, 290f);

            // 도약 — 오른쪽 주가지
            Add(nodes, "J00", "첫 박자", "자동 점프의 기본 준비 박자를 줄입니다.",
                "점프 준비시간 -2%", "leap.rhythm.seed", PermanentGrowthType.JumpCharge,
                0.02f, "충전 시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Root,
                null, 0, "", 700f, -980f, 1);
            Add(nodes, "J-A1", "고른 박자", "자동 점프의 준비 박자를 한 번 더 고릅니다.",
                "점프 준비시간 -2%", "leap.rhythm.even", PermanentGrowthType.JumpCharge,
                0.02f, "충전 시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 650f, -720f, 2);
            Add(nodes, "J-A2", "잰 박자", "자동 점프의 준비를 더 빠르게 잇습니다.",
                "점프 준비시간 -2%", "leap.rhythm.fast", PermanentGrowthType.JumpCharge,
                0.02f, "충전 시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-A1"), 0, "", 560f, -390f, 3);
            Add(nodes, "J-A3", "이어진 박자", "그린 발판 착지 뒤 다음 충전만 더 빨라집니다.",
                "다음 점프 준비시간 추가 -4%", "leap.rhythm.drawn",
                PermanentGrowthType.DrawnChargeRhythm, 0.04f, "다음 충전",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Mechanic,
                P("J-A2"), 0, "", 650f, -60f);
            Add(nodes, "J-KA", "무심의 박자", "그린 발판 세 번을 연속 밟으면 다음 충전을 앞당깁니다.",
                "3연속 착지 시 충전 +20%p · 10초", "leap.keystone.rhythm",
                PermanentGrowthType.ConsecutiveLandingRhythm, 0.20f, "충전 진행도",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-A3"), 6, "leap", 520f, 300f);

            Add(nodes, "J-B1", "돋는 먹발", "기본 자동 점프 힘을 조금 높입니다.",
                "기본 자동 점프 힘 +2%", "leap.power.base", PermanentGrowthType.JumpPower,
                0.02f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 980f, -660f);
            Add(nodes, "J-B2", "짧은 먹발", "짧은 발판도 지나치게 약한 점프가 되지 않게 합니다.",
                "짧은 발판 배율 하한 0.85 → 0.90", "leap.power.short",
                PermanentGrowthType.ShortPlatformControl, 0.90f, "배율 하한",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Mechanic,
                P("J-B1"), 0, "", 1040f, -330f);
            Add(nodes, "J-B3", "먹결 탄성", "직접 그린 발판의 반동을 더 살립니다.",
                "그린 발판 점프 힘 +3%", "leap.power.drawn",
                PermanentGrowthType.DrawnPlatformLeap, 0.03f, "먹발판 도약",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-B2"), 0, "", 950f, 0f);
            Add(nodes, "J-KB", "먹결 도약", "직접 그린 짧은 발판도 온전한 점프를 냅니다.",
                "그린 짧은 발판 배율 하한 1.00", "leap.keystone.platform",
                PermanentGrowthType.DrawnPlatformLeap, 1f, "배율 하한",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-B3"), 6, "leap", 1000f, 360f, 2);

            Add(nodes, "J-C1", "긴 정점", "점프 정점 직후 잠깐 가벼워져 발판을 볼 시간을 줍니다.",
                "정점 0.08초 동안 중력 -20%", "leap.fall.apex",
                PermanentGrowthType.ApexHang, 0.08f, "정점 유지",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Mechanic,
                P("J00"), 0, "", 1320f, -720f);
            Add(nodes, "J-C2", "늦은 낙하", "자동 점프 뒤 최대 낙하 속도를 낮춥니다.",
                "최대 낙하 속도 -4%", "leap.fall.slow",
                PermanentGrowthType.FallControl, 0.04f, "낙하 속도",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C1"), 0, "", 1420f, -390f);
            Add(nodes, "J-C3", "바람 읽기", "일반 바람과 강풍의 수평 영향을 덜 받습니다.",
                "바람 수평 영향 -10%", "leap.fall.wind",
                PermanentGrowthType.WindControl, 0.10f, "바람 영향",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C2"), 0, "", 1320f, -60f);
            Add(nodes, "J-KC", "한 획의 틈", "마지막 생존자가 화면 아래로 떨어질 때 낙하만 잠깐 늦춥니다.",
                "하단 25% 낙하 -35% · 0.45초 · 18초", "leap.keystone.fall",
                PermanentGrowthType.LastFallBrake, 0.35f, "낙하 감속",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-C3"), 6, "leap", 1480f, 300f);

            return nodes.ToArray();
        }

        static void Add(
            ICollection<PermanentGrowthNodeDefinition> nodes,
            string id,
            string name,
            string description,
            string effectSummary,
            string iconKey,
            PermanentGrowthType effectId,
            float effectValue,
            string effectUnit,
            PermanentGrowthValueKind valueKind,
            bool reducesValue,
            PermanentGrowthBranch branch,
            PermanentGrowthNodeKind kind,
            string[] parents,
            int requiredOwned,
            string keystoneGroup,
            float x,
            float y,
            int effectRank = 1)
        {
            nodes.Add(new PermanentGrowthNodeDefinition(
                id,
                name,
                description,
                effectSummary,
                iconKey,
                effectId,
                effectValue,
                effectUnit,
                valueKind,
                reducesValue,
                branch,
                kind,
                parents,
                requiredOwned,
                keystoneGroup,
                x,
                y,
                effectRank));
        }

        static PermanentGrowthDefinition[] BuildEffectDefinitions()
        {
            return NodeDefinitions
                .GroupBy(node => node.EffectId)
                .Select((group, order) =>
                {
                    PermanentGrowthNodeDefinition first = group.First();
                    int[] costs = Enumerable.Repeat(1, group.Count()).ToArray();
                    return new PermanentGrowthDefinition(
                        $"permanent.effect.{group.Key}",
                        group.Key,
                        first.Branch,
                        group.Any(node => node.IsKeystone)
                            ? PermanentGrowthNodeKind.Keystone
                            : first.NodeKind,
                        order,
                        first.DisplayName,
                        first.Description,
                        first.EffectUnit,
                        first.EffectValue,
                        first.ReducesValue,
                        first.ValueKind,
                        costs);
                })
                .ToArray();
        }

        static string[] P(string id) => new[] { id };
    }
}
