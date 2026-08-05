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
        // 기존 0~30 값은 저장 호환을 위해 그대로 두고 도약 v4 효과만 뒤에 붙인다.
        JumpHeight = 31,
        SafetyPlatform = 32,
        DoubleJump = 33,
        WallCling = 34,
        // 먹자리 v6. 기존 enum 값은 저장 호환 때문에 재사용하지 않고 뒤에 추가한다.
        InkBudgetEfficiency = 35,
        InkEvictionFade = 36,
        InkEvictionDelay = 37,
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

    /// 영구 성장 v4의 39개 stable node ID와 해금 그래프를 소유한다.
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
                "총 먹자리·획 점유 절약·오래된 획 소멸",
                1),
            new(
                PermanentGrowthBranch.Leap,
                "도약",
                "준비시간·점프 힘·점프 높이와 세 구조 비기",
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

            // 먹 운용 — 회복형 자원을 없애고 화면에 남길 수 있는 총 먹자리로 통일한다.
            Add(nodes, "I00", "먹자리의 씨", "화면에 오래 남길 수 있는 먹선의 자리를 넓힙니다.",
                "총 먹자리 +30%", "ink.capacity.seed", PermanentGrowthType.InkCapacity,
                0.30f, "총 먹자리", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Root,
                null, 0, "", 0f, -1040f, 1);
            Add(nodes, "I-A1", "넓은 벼루", "한 화면에 남겨 둘 수 있는 먹선을 늘립니다.",
                "총 먹자리 +30%", "ink.capacity.wide", PermanentGrowthType.InkCapacity,
                0.30f, "총 먹자리", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", -350f, -770f, 2);
            Add(nodes, "I-A2", "깊은 먹자리", "먹자리를 깊게 다져 더 긴 길을 남깁니다.",
                "총 먹자리 +30%", "ink.capacity.deep", PermanentGrowthType.InkCapacity,
                0.30f, "총 먹자리", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-A1"), 0, "", -430f, -430f);
            Add(nodes, "I-A3", "큰 먹자리", "넓어진 먹자리가 오래된 획을 더 늦게 밀어냅니다.",
                "총 먹자리 +30%", "ink.capacity.great", PermanentGrowthType.InkCapacity,
                0.30f, "총 먹자리", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-A2"), 0, "", -350f, -80f);
            Add(nodes, "I-KA", "넓어진 화폭", "넓은 화폭을 펼쳐 한 번에 더 많은 길을 남깁니다.",
                "총 먹자리 +30%", "ink.capacity.keystone",
                PermanentGrowthType.InkCapacity, 0.30f, "총 먹자리",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-A3"), 4, "ink", -500f, 300f);

            Add(nodes, "I-B1", "가는 붓끝", "같은 길이를 더 적은 먹자리로 그립니다.",
                "먹자리 점유 -1.5%", "ink.budget.fine",
                PermanentGrowthType.InkBudgetEfficiency,
                0.015f, "먹자리 점유", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 0f, -710f, 1);
            Add(nodes, "I-B2", "아낀 먹결", "먹결을 고르게 펴 획의 자리 낭비를 줄입니다.",
                "먹자리 점유 -1.5%", "ink.budget.even",
                PermanentGrowthType.InkBudgetEfficiency,
                0.015f, "먹자리 점유", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-B1"), 0, "", 70f, -370f, 2);
            Add(nodes, "I-B3", "짧은 먹선", "짧고 정확한 획은 먹자리를 더 적게 차지합니다.",
                "1.5m 이하 획 점유 -6%", "ink.budget.short",
                PermanentGrowthType.ShortStrokeEfficiency, 0.06f, "짧은 획 점유",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-B2"), 0, "", 0f, -20f);
            Add(nodes, "I-KB", "한 붓의 길", "모든 획의 먹자리 점유를 크게 줄입니다.",
                "먹자리 점유 -5%", "ink.budget.keystone",
                PermanentGrowthType.InkBudgetEfficiency, 0.05f, "먹자리 점유",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-B3"), 4, "ink", 0f, 360f);

            Add(nodes, "I-C1", "천천히 번진 먹", "밀려난 오래된 획이 더 천천히 사라집니다.",
                "소멸 시간 +0.10초", "ink.eviction.fade.first",
                PermanentGrowthType.InkEvictionFade, 0.10f, "소멸 시간",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 350f, -780f, 1);
            Add(nodes, "I-C2", "오래 남은 먹", "먹의 여운을 늘려 발판 변화를 읽기 쉽게 합니다.",
                "소멸 시간 +0.10초", "ink.eviction.fade.second",
                PermanentGrowthType.InkEvictionFade, 0.10f, "소멸 시간",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-C1"), 0, "", 430f, -450f, 2);
            Add(nodes, "I-C3", "머금은 먹", "먹자리를 넘겨도 오래된 획이 잠깐 버팁니다.",
                "소멸 시작 +0.10초 지연", "ink.eviction.delay",
                PermanentGrowthType.InkEvictionDelay, 0.10f, "소멸 지연",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Mechanic,
                P("I-C2"), 0, "", 350f, -110f);
            Add(nodes, "I-KC", "붙잡은 먹길", "밀려난 획이 충분히 보인 뒤 사라지게 합니다.",
                "소멸 시간 +0.45초", "ink.eviction.keystone",
                PermanentGrowthType.InkEvictionFade, 0.45f, "소멸 시간",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-C3"), 4, "ink", 500f, 280f);

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
                P("S-A3"), 4, "survival", -1480f, 300f);

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
            Add(nodes, "S-B3", "고요한 중심", "피격 뒤 몸을 추스를 시간을 조금 더 확보합니다.",
                "피격 뒤 무적 +0.08초", "survival.stability.grace",
                PermanentGrowthType.DamageGrace, 0.08f, "피격 여유",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Mechanic,
                P("S-B2"), 0, "", -950f, 0f);
            Add(nodes, "S-KB", "흐트러지지 않음", "주기적으로 체력만 잃고 속도와 발판 접착은 지킵니다.",
                "피격 움직임 보존 · 공용 12초", "survival.keystone.stable",
                PermanentGrowthType.StableHit, 12f, "재사용",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-B3"), 4, "survival", -1000f, 360f);

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
                P("S-C3"), 4, "survival", -520f, 290f);

            // 도약 — 공용 뿌리 뒤에 준비·힘·높이 세 갈래가 각각 3칸과 비기로 이어진다.
            Add(nodes, "J00", "도약의 씨", "세 갈래 도약 성장을 여는 첫 박자를 익힙니다.",
                "점프 준비시간 -1.5%", "leap.rhythm.seed", PermanentGrowthType.JumpCharge,
                0.015f, "준비시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Root,
                null, 0, "", 700f, -1080f, 1);

            Add(nodes, "J-A1", "고른 박자 I", "자동 점프의 준비 박자를 짧게 다듬습니다.",
                "점프 준비시간 -1.5%", "leap.rhythm.01", PermanentGrowthType.JumpCharge,
                0.015f, "준비시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 560f, -820f, 2);
            Add(nodes, "J-A2", "고른 박자 II", "자동 점프의 준비 박자를 짧게 다듬습니다.",
                "점프 준비시간 -1.5%", "leap.rhythm.02", PermanentGrowthType.JumpCharge,
                0.015f, "준비시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-A1"), 0, "", 520f, -510f, 3);
            Add(nodes, "J-A3", "고른 박자 III", "자동 점프의 준비 박자를 짧게 다듬습니다.",
                "점프 준비시간 -1.5%", "leap.rhythm.03", PermanentGrowthType.JumpCharge,
                0.015f, "준비시간", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-A2"), 0, "", 600f, -200f, 4);
            Add(nodes, "J-KA", "벽의 먹발", "측면 벽에 닿으면 잠시 매달려 박자를 채운 뒤 안쪽으로 점프합니다.",
                "벽 매달림 · 최대 1.2초", "leap.keystone.wall",
                PermanentGrowthType.WallCling, 1.2f, "매달림",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-A3"), 4, "leap", 520f, 110f);

            Add(nodes, "J-B1", "돋는 먹발 I", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1.67%", "leap.power.01", PermanentGrowthType.JumpPower,
                0.05f / 3f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 760f, -800f, 1);
            Add(nodes, "J-B2", "돋는 먹발 II", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1.67%", "leap.power.02", PermanentGrowthType.JumpPower,
                0.05f / 3f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-B1"), 0, "", 830f, -490f, 2);
            Add(nodes, "J-B3", "돋는 먹발 III", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1.67%", "leap.power.03", PermanentGrowthType.JumpPower,
                0.05f / 3f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-B2"), 0, "", 770f, -180f, 3);
            Add(nodes, "J-KB", "다섯 번째 먹자리", "먹떼의 일반 자동 점프 다섯 번마다 잠시 머물 안전 발판을 만듭니다.",
                "5회마다 단방향 발판 · 6초", "leap.keystone.safety",
                PermanentGrowthType.SafetyPlatform, 5f, "점프 횟수",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-B3"), 4, "leap", 850f, 130f);

            Add(nodes, "J-C1", "높은 먹발 I", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +2.08%", "leap.height.01", PermanentGrowthType.JumpHeight,
                0.0625f / 3f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 1020f, -820f, 1);
            Add(nodes, "J-C2", "높은 먹발 II", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +2.08%", "leap.height.02", PermanentGrowthType.JumpHeight,
                0.0625f / 3f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C1"), 0, "", 1110f, -510f, 2);
            Add(nodes, "J-C3", "높은 먹발 III", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +2.08%", "leap.height.03", PermanentGrowthType.JumpHeight,
                0.0625f / 3f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C2"), 0, "", 1040f, -200f, 3);
            Add(nodes, "J-KC", "겹친 먹발", "일반 자동 점프의 첫 정점에서 한 번 더 뛰어오릅니다.",
                "2단 점프 · 힘 40% · 공용 12초", "leap.keystone.double",
                PermanentGrowthType.DoubleJump, 0.40f, "추가 점프 힘",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-C3"), 4, "leap", 1140f, 110f);

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
