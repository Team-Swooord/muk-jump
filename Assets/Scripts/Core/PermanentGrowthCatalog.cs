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
        // 최대 먹 용량 v6. 기존 enum 값은 저장 호환 때문에 재사용하지 않고 뒤에 추가한다.
        InkBudgetEfficiency = 35,
        InkEvictionFade = 36,
        InkEvictionDelay = 37,
        // 단일 수치 성장 재정렬. 기존 enum 값은 저장 호환을 위해 그대로 둔다.
        InkCloneItemExtraCount = 38,
        // 숨 고르기 최종 열매. 기존 enum 값은 저장 호환을 위해 재사용하지 않는다.
        PostHitShield = 39,
        // 결실 v5. 기존 번호는 구 저장·테스트 호환을 위해 그대로 두고 새 효과만 붙인다.
        InkCapacityDouble = 40,
        GoldenBrushShield = 41,
        InkDropEndShield = 42,
        CloneMaxHealth = 43,
    }

    public enum PermanentGrowthBranch
    {
        Survival,
        Leap,
        InkHandling,
    }

    /// 각 계보에서 실제 판에 적용할 한 갈래. 씨앗(root)은 선택과 무관하게 적용된다.
    public enum PermanentGrowthPath
    {
        None,
        A,
        B,
        C,
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
                "본체 체력·피격 여유·피격 안정·분신 성장·본체 부활·먹떼 결실",
                0),
            new(
                PermanentGrowthBranch.InkHandling,
                "먹 운용",
                "최대 먹 용량·획당 먹 소모·게이지 회복",
                1),
            new(
                PermanentGrowthBranch.Leap,
                "도약",
                "준비시간·점프 힘·점프 높이",
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

        public static PermanentGrowthPath GetPath(
            PermanentGrowthNodeDefinition node) =>
            node == null ? PermanentGrowthPath.None : GetPath(node.Id);

        public static PermanentGrowthPath GetPath(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodeId.Length < 3 ||
                nodeId[1] != '-')
                return PermanentGrowthPath.None;

            int pathIndex = nodeId[2] == 'K' ? 3 : 2;
            if (pathIndex >= nodeId.Length)
                return PermanentGrowthPath.None;
            return nodeId[pathIndex] switch
            {
                'A' => PermanentGrowthPath.A,
                'B' => PermanentGrowthPath.B,
                'C' => PermanentGrowthPath.C,
                _ => PermanentGrowthPath.None,
            };
        }

        public static string GetKeystoneId(
            PermanentGrowthBranch branch,
            PermanentGrowthPath path)
        {
            if (path == PermanentGrowthPath.None)
                return string.Empty;
            string prefix = branch switch
            {
                PermanentGrowthBranch.Survival => "S",
                PermanentGrowthBranch.Leap => "J",
                _ => "I",
            };
            return $"{prefix}-K{path}";
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

            // 먹 운용 — 한 줄은 하나의 수치만 반복 강화한다.
            // 공용 뿌리 I00은 가운데 절약 줄의 첫 단계이며, 좌우로 총량·회복 줄이 갈라진다.
            Add(nodes, "I00", "아끼는 먹의 씨", "같은 길이를 조금 더 적은 먹으로 그립니다.",
                "획당 먹 소모 -2%", "ink.budget.seed",
                PermanentGrowthType.InkBudgetEfficiency,
                0.02f, "먹 소모량", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Root,
                null, 0, "", 0f, -1040f, 1);
            Add(nodes, "I-A1", "넓은 벼루 I", "한 화면에 남겨 둘 수 있는 먹선의 총량을 늘립니다.",
                "최대 먹 용량 +37.5%", "ink.capacity.wide", PermanentGrowthType.InkCapacity,
                0.375f, "최대 먹 용량", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", -300f, -770f, 2);
            Add(nodes, "I-A2", "넓은 벼루 II", "한 화면에 남겨 둘 수 있는 먹선의 총량을 늘립니다.",
                "최대 먹 용량 +37.5%", "ink.capacity.deep", PermanentGrowthType.InkCapacity,
                0.375f, "최대 먹 용량", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-A1"), 0, "", -360f, -430f);
            Add(nodes, "I-A3", "넓은 벼루 III", "한 화면에 남겨 둘 수 있는 먹선의 총량을 늘립니다.",
                "최대 먹 용량 +37.5%", "ink.capacity.great", PermanentGrowthType.InkCapacity,
                0.375f, "최대 먹 용량", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-A2"), 0, "", -300f, -80f);
            Add(nodes, "I-KA", "넓은 벼루 결실", "현재까지 키운 최대 먹 게이지 용량을 두 배로 늘립니다.",
                "최대 먹 게이지 용량 ×2", "ink.capacity.keystone",
                PermanentGrowthType.InkCapacityDouble, 2f, "최대 먹 용량",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-A3"), 4, "ink", -300f, 230f);

            Add(nodes, "I-B1", "가는 붓끝 I", "같은 길이를 조금 더 적은 먹으로 그립니다.",
                "획당 먹 소모 -2%", "ink.budget.fine",
                PermanentGrowthType.InkBudgetEfficiency,
                0.02f, "먹 소모량", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 0f, -710f, 1);
            Add(nodes, "I-B2", "가는 붓끝 II", "같은 길이를 조금 더 적은 먹으로 그립니다.",
                "획당 먹 소모 -2%", "ink.budget.even",
                PermanentGrowthType.InkBudgetEfficiency,
                0.02f, "먹 소모량", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-B1"), 0, "", 70f, -370f, 2);
            Add(nodes, "I-B3", "가는 붓끝 III", "같은 길이를 조금 더 적은 먹으로 그립니다.",
                "획당 먹 소모 -2%", "ink.budget.short",
                PermanentGrowthType.InkBudgetEfficiency, 0.02f, "먹 소모량",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-B2"), 0, "", 0f, -20f);
            Add(nodes, "I-KB", "가는 붓끝 결실", "황금 붓 아이템을 먹을 때 획득한 먹방울에게 1회용 방어막을 줍니다.",
                "황금 붓 획득 시 방어막 1회", "ink.budget.keystone",
                PermanentGrowthType.GoldenBrushShield, 1f, "1회 방어막",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-B3"), 4, "ink", 0f, 290f);

            Add(nodes, "I-C1", "마르는 먹 I", "오래된 획이 사라질 때 먹 게이지가 더 빨리 돌아옵니다.",
                "먹 게이지 회복 속도 +10%", "ink.recovery.first",
                PermanentGrowthType.InkRecovery, 0.10f, "회복 속도",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I00"), 0, "", 350f, -780f, 1);
            Add(nodes, "I-C2", "마르는 먹 II", "오래된 획이 사라질 때 먹 게이지가 더 빨리 돌아옵니다.",
                "먹 게이지 회복 속도 +10%", "ink.recovery.second",
                PermanentGrowthType.InkRecovery, 0.10f, "회복 속도",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-C1"), 0, "", 430f, -450f, 2);
            Add(nodes, "I-C3", "마르는 먹 III", "오래된 획이 사라질 때 먹 게이지가 더 빨리 돌아옵니다.",
                "먹 게이지 회복 속도 +10%", "ink.recovery.third",
                PermanentGrowthType.InkRecovery, 0.10f, "회복 속도",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Stat,
                P("I-C2"), 0, "", 350f, -110f);
            Add(nodes, "I-KC", "마르는 먹 결실", "오래된 먹선이 사라질 때 먹 게이지가 더 빠르게 돌아옵니다.",
                "먹 게이지 회복 속도 +10%", "ink.recovery.keystone",
                PermanentGrowthType.InkRecovery, 0.10f, "회복 속도",
                PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, PermanentGrowthNodeKind.Keystone,
                P("I-C3"), 4, "ink", 350f, 200f);

            // 생존 — 본체 최대 체력·피격 여유·피격 안정 세 줄을 사용한다.
            // 각 결실은 분신 체력, 본체 1회 부활, 분신 아이템 생성 수를 맡는다.
            // 공용 뿌리 S00은 가운데 피격 여유 줄의 첫 단계다.
            Add(nodes, "S00", "숨 고르기의 씨", "피격 직후 다시 맞지 않는 시간을 늘립니다.",
                "피격 뒤 무적 +0.04초", "survival.grace.seed",
                PermanentGrowthType.DamageGrace, 0.04f, "피격 뒤 무적",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Root,
                null, 0, "", -700f, -980f, 1);
            Add(nodes, "S-A1", "먹피 I", "본체 먹방울이의 최대 체력을 한 칸 늘립니다. 분신 체력은 1로 유지됩니다.",
                "본체 최대 체력 +1", "survival.guard.thin",
                PermanentGrowthType.Vitality, 1f, "본체 최대 체력",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -1320f, -720f, 2);
            Add(nodes, "S-A2", "먹피 II", "본체 먹방울이의 최대 체력을 한 칸 늘립니다. 분신 체력은 1로 유지됩니다.",
                "본체 최대 체력 +1", "survival.guard.layered",
                PermanentGrowthType.Vitality, 1f, "본체 최대 체력",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-A1"), 0, "", -1420f, -390f, 3);
            Add(nodes, "S-A3", "먹피 III", "본체 먹방울이의 최대 체력을 한 칸 늘립니다. 분신 체력은 1로 유지됩니다.",
                "본체 최대 체력 +1", "survival.guard.heart",
                PermanentGrowthType.Vitality, 1f, "본체 최대 체력",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-A2"), 0, "", -1320f, -60f);
            Add(nodes, "S-KA", "먹피 결실", "모든 먹분신의 최대 체력을 기본 1칸에서 2칸으로 늘립니다.",
                "먹분신 최대 체력 +1", "survival.guard.keystone",
                PermanentGrowthType.CloneMaxHealth, 1f, "먹분신 최대 체력",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-A3"), 4, "survival", -1320f, 250f);

            Add(nodes, "S-B1", "숨 고르기 I", "피격 직후 다시 맞지 않는 시간을 늘립니다.",
                "피격 뒤 무적 +0.04초", "survival.grace.first",
                PermanentGrowthType.DamageGrace, 0.04f, "피격 뒤 무적",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -980f, -660f);
            Add(nodes, "S-B2", "숨 고르기 II", "피격 직후 다시 맞지 않는 시간을 늘립니다.",
                "피격 뒤 무적 +0.04초", "survival.grace.second",
                PermanentGrowthType.DamageGrace, 0.04f, "피격 뒤 무적",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-B1"), 0, "", -1040f, -330f);
            Add(nodes, "S-B3", "숨 고르기 III", "피격 직후 다시 맞지 않는 시간을 늘립니다.",
                "피격 뒤 무적 +0.04초", "survival.grace.third",
                PermanentGrowthType.DamageGrace, 0.04f, "피격 뒤 무적",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-B2"), 0, "", -950f, 0f);
            Add(nodes, "S-KB", "숨 고르기 결실", "본체 먹방울이가 쓰러질 때 한 판에 한 번 체력 1칸으로 부활합니다.",
                "본체 1회 부활 · 체력 1", "survival.last_breath.keystone",
                PermanentGrowthType.LastBreath, 1f, "판당 부활",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-B3"), 4, "survival", -950f, 310f);

            Add(nodes, "S-C1", "먹발 버팀 I", "피격 뒤 수평으로 밀려나는 힘을 줄입니다.",
                "피격 수평 밀림 -6%", "survival.stability.first",
                PermanentGrowthType.HitHorizontalStability, 0.06f, "피격 수평 밀림",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S00"), 0, "", -720f, -720f, 1);
            Add(nodes, "S-C2", "먹발 버팀 II", "피격 뒤 수평으로 밀려나는 힘을 줄입니다.",
                "피격 수평 밀림 -6%", "survival.stability.second",
                PermanentGrowthType.HitHorizontalStability, 0.06f, "피격 수평 밀림",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-C1"), 0, "", -680f, -400f, 2);
            Add(nodes, "S-C3", "먹발 버팀 III", "피격 뒤 수평으로 밀려나는 힘을 줄입니다.",
                "피격 수평 밀림 -6%", "survival.stability.third",
                PermanentGrowthType.HitHorizontalStability, 0.06f, "피격 수평 밀림",
                PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Stat,
                P("S-C2"), 0, "", -740f, -70f, 3);
            Add(nodes, "S-KC", "먹떼 결실", "먹분신 아이템을 먹을 때 생성할 수 있는 분신을 한 마리 늘립니다.",
                "먹분신 아이템 생성 최대 +1", "survival.clone.keystone",
                PermanentGrowthType.InkCloneItemExtraCount, 1f, "먹분신 생성 수",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, PermanentGrowthNodeKind.Keystone,
                P("S-C3"), 4, "survival", -740f, 240f);

            // 도약 — 준비시간·점프 힘·점프 높이 세 줄만 반복 강화한다.
            // 공용 뿌리 J00은 가운데 점프 힘 줄의 첫 단계다.
            Add(nodes, "J00", "솟는 힘의 씨", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1%", "leap.power.seed", PermanentGrowthType.JumpPower,
                0.01f, "점프 힘", PermanentGrowthValueKind.Percent, false,
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
            Add(nodes, "J-KA", "고른 박자 결실", "점프 아이템의 상승이 끝나면 아이템을 먹은 먹방울에게 1회용 방어막을 줍니다.",
                "점프 아이템 종료 시 방어막 1회", "leap.rhythm.keystone",
                PermanentGrowthType.InkDropEndShield, 1f, "1회 방어막",
                PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-A3"), 4, "leap", 520f, 110f);

            Add(nodes, "J-B1", "돋는 먹발 I", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1%", "leap.power.01", PermanentGrowthType.JumpPower,
                0.01f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 760f, -800f, 1);
            Add(nodes, "J-B2", "돋는 먹발 II", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1%", "leap.power.02", PermanentGrowthType.JumpPower,
                0.01f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-B1"), 0, "", 830f, -490f, 2);
            Add(nodes, "J-B3", "돋는 먹발 III", "기본 자동 점프 힘을 고르게 키웁니다.",
                "기본 점프 힘 +1%", "leap.power.03", PermanentGrowthType.JumpPower,
                0.01f, "점프 힘", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-B2"), 0, "", 770f, -180f, 3);
            Add(nodes, "J-KB", "돋는 먹발 결실", "하강 중 좌우 벽에 닿으면 잠시 붙고, 벽에서 안쪽으로 다시 점프할 수 있습니다.",
                "벽 매달림 · 최대 1.2초", "leap.power.keystone",
                PermanentGrowthType.WallCling, 1.2f, "벽 매달림",
                PermanentGrowthValueKind.Seconds, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Keystone,
                P("J-B3"), 4, "leap", 850f, 130f);

            Add(nodes, "J-C1", "높은 먹발 I", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +1.56%", "leap.height.01", PermanentGrowthType.JumpHeight,
                0.0625f / 4f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J00"), 0, "", 1020f, -820f, 1);
            Add(nodes, "J-C2", "높은 먹발 II", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +1.56%", "leap.height.02", PermanentGrowthType.JumpHeight,
                0.0625f / 4f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C1"), 0, "", 1110f, -510f, 2);
            Add(nodes, "J-C3", "높은 먹발 III", "자동 점프가 닿는 정점 높이를 늘립니다.",
                "점프 높이 +1.56%", "leap.height.03", PermanentGrowthType.JumpHeight,
                0.0625f / 4f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, PermanentGrowthNodeKind.Stat,
                P("J-C2"), 0, "", 1040f, -200f, 3);
            Add(nodes, "J-KC", "높은 먹발 결실", "모든 일반 자동 점프의 상승이 잦아드는 정점 직전에 한 번 더 도약합니다.",
                "매 자동점프 2단도약 · 힘 40%", "leap.height.keystone",
                PermanentGrowthType.DoubleJump, 0.40f, "2단점프 힘",
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
