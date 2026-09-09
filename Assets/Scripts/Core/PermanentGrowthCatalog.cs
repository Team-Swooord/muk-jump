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
            costs.Take(Math.Clamp(level, 0, costs.Length)).Sum();

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
            int effectRank = 1,
            int cost = 1)
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
            Cost = Math.Max(1, cost);
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
        public int Cost { get; }
        public int Rank { get; }
    }

    public sealed class PermanentGrowthChoiceDefinition
    {
        public PermanentGrowthChoiceDefinition(
            string id,
            PermanentGrowthType type,
            string displayName,
            string summary,
            string iconKey)
        {
            Id = id;
            Type = type;
            DisplayName = displayName;
            Summary = summary;
            IconKey = iconKey;
        }

        public string Id { get; }
        public PermanentGrowthType Type { get; }
        public string DisplayName { get; }
        public string Summary { get; }
        public string IconKey { get; }
    }

    /// v9는 항상 보이는 네 성장 선택지에 각 8단계, 총 32단계를 제공한다.
    /// v7 이전의 39노드 그래프는 아래 frozen 검증기에서만 읽는다.
    public static class PermanentGrowthCatalog
    {
        static readonly PermanentGrowthBranchMetadata[] BranchDefinitions =
        {
            new(
                PermanentGrowthBranch.Survival,
                "생존",
                "본체 체력·피격 여유·피격 안정·분신 성장·본체 부활과 50m 상승·먹떼 결실",
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
        static readonly PermanentGrowthChoiceDefinition[] ChoiceDefinitions =
        {
            new("body", PermanentGrowthType.Vitality,
                "튼튼한 먹", "부딪혀도 더 오래 버텨요.",
                "survival.guard.thin"),
            new("ink", PermanentGrowthType.InkCapacity,
                "넉넉한 먹물", "먹물을 넉넉히 담아 둘 수 있어요.",
                "ink.capacity.wide"),
            new("brush", PermanentGrowthType.InkBudgetEfficiency,
                "알뜰한 붓", "그릴 때 먹물이 천천히 줄어요.",
                "ink.budget.fine"),
            new("jump", PermanentGrowthType.JumpHeight,
                "높은 도약", "더 높은 곳까지 뛰어요.",
                "leap.height.01"),
        };
        static readonly PermanentGrowthDefinition[] Definitions =
            BuildEffectDefinitions();
        static readonly Dictionary<string, PermanentGrowthNodeDefinition> NodesById =
            NodeDefinitions.ToDictionary(node => node.Id, StringComparer.Ordinal);

        public static IReadOnlyList<PermanentGrowthDefinition> All => Definitions;
        public static IReadOnlyList<PermanentGrowthNodeDefinition> Nodes =>
            NodeDefinitions;
        public static IReadOnlyList<PermanentGrowthChoiceDefinition> Choices =>
            ChoiceDefinitions;
        public static IReadOnlyList<PermanentGrowthBranchMetadata> Branches =>
            BranchDefinitions;
        public static int TotalCost => NodeDefinitions.Sum(node => node.Cost);

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
            var nodes = new List<PermanentGrowthNodeDefinition>(32);
            // 기존 1~4단계 비용은 유지하고 후반부만 완만하게 늘린다.
            int[] costs = { 1, 2, 3, 5, 7, 10, 14, 19 };
            AddTrack(nodes, "body", "튼튼한 먹", "부딪혀도 더 오래 버텨요.",
                "survival.guard.thin", PermanentGrowthType.Vitality,
                1f, "본체 최대 체력", PermanentGrowthValueKind.Flat, false,
                PermanentGrowthBranch.Survival, costs);
            AddTrack(nodes, "ink", "넉넉한 먹물", "먹물을 넉넉히 담아 둘 수 있어요.",
                "ink.capacity.wide", PermanentGrowthType.InkCapacity,
                0.125f, "최대 먹 용량", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.InkHandling, costs);
            AddTrack(nodes, "brush", "알뜰한 붓", "그릴 때 먹물이 천천히 줄어요.",
                "ink.budget.fine", PermanentGrowthType.InkBudgetEfficiency,
                0.03f, "먹 소모량", PermanentGrowthValueKind.Percent, true,
                PermanentGrowthBranch.InkHandling, costs);
            AddTrack(nodes, "jump", "높은 도약", "더 높은 곳까지 뛰어요.",
                "leap.height.01", PermanentGrowthType.JumpHeight,
                0.0125f, "점프 높이", PermanentGrowthValueKind.Percent, false,
                PermanentGrowthBranch.Leap, costs);

            return nodes.ToArray();
        }

        static void AddTrack(
            ICollection<PermanentGrowthNodeDefinition> nodes,
            string idPrefix,
            string name,
            string description,
            string iconKey,
            PermanentGrowthType effectId,
            float effectValue,
            string effectUnit,
            PermanentGrowthValueKind valueKind,
            bool reducesValue,
            PermanentGrowthBranch branch,
            IReadOnlyList<int> costs)
        {
            string previousId = string.Empty;
            for (int rank = 1; rank <= costs.Count; rank++)
            {
                string id = $"{idPrefix}.{rank}";
                Add(nodes, id, name, description, string.Empty, iconKey,
                    effectId, effectValue, effectUnit, valueKind, reducesValue,
                    branch, PermanentGrowthNodeKind.Stat,
                    string.IsNullOrEmpty(previousId) ? null : P(previousId),
                    0, string.Empty, 0f, 0f, rank, costs[rank - 1]);
                previousId = id;
            }
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
            int effectRank = 1,
            int cost = 1)
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
                effectRank,
                cost));
        }

        static PermanentGrowthDefinition[] BuildEffectDefinitions()
        {
            return NodeDefinitions
                .GroupBy(node => node.EffectId)
                .Select((group, order) =>
                {
                    PermanentGrowthNodeDefinition first = group.First();
                    int[] costs = group.OrderBy(node => node.Rank)
                        .Select(node => node.Cost).ToArray();
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

    /// v5~v7 저장을 새 카탈로그로 정규화하기 전에 검증하기 위한 동결된
    /// 39노드 그래프다. 게임플레이·UI·신규 구매에는 절대 노출하지 않는다.
    static class PermanentGrowthLegacyV7Catalog
    {
        static readonly Dictionary<PermanentGrowthBranch, string[]> Orders =
            new()
            {
                [PermanentGrowthBranch.Survival] = new[]
                {
                    "S00", "S-A1", "S-B1", "S-C1", "S-A2", "S-B2", "S-C2",
                    "S-A3", "S-B3", "S-C3", "S-KA", "S-KB", "S-KC",
                },
                [PermanentGrowthBranch.Leap] = new[]
                {
                    "J00", "J-A1", "J-B1", "J-C1", "J-A2", "J-B2", "J-C2",
                    "J-A3", "J-B3", "J-C3", "J-KA", "J-KB", "J-KC",
                },
                [PermanentGrowthBranch.InkHandling] = new[]
                {
                    "I00", "I-A1", "I-B1", "I-C1", "I-A2", "I-B2", "I-C2",
                    "I-A3", "I-B3", "I-C3", "I-KA", "I-KB", "I-KC",
                },
            };

        static readonly HashSet<string> AllIds = new(
            Orders.Values.SelectMany(order => order),
            StringComparer.Ordinal);

        public static IReadOnlyList<string> MigrationOrder(
            PermanentGrowthBranch branch) => Orders[branch];

        public static List<string> NormalizeOwnedIds(
            IEnumerable<string> requestedIds)
        {
            var requested = new HashSet<string>(
                requestedIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new List<string>(39);
            foreach (PermanentGrowthBranch branch
                     in Enum.GetValues(typeof(PermanentGrowthBranch)))
            {
                IReadOnlyList<string> order = Orders[branch];
                for (int i = 0; i < order.Count; i++)
                {
                    string id = order[i];
                    if (!requested.Contains(id))
                        continue;
                    string parent = ParentOf(id);
                    if (!string.IsNullOrEmpty(parent) &&
                        !accepted.Contains(parent))
                        continue;
                    if (IsKeystone(id) && CountGeneral(accepted, branch) < 4)
                        continue;
                    accepted.Add(id);
                    normalized.Add(id);
                }
            }
            return normalized;
        }

        public static bool IsValidOwnedGraph(
            IReadOnlyList<string> ownedIds,
            string survivalKeystoneId,
            string leapKeystoneId,
            string inkKeystoneId)
        {
            if (ownedIds == null)
                return false;
            var owned = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ownedIds.Count; i++)
                if (!AllIds.Contains(ownedIds[i]) || !owned.Add(ownedIds[i]))
                    return false;

            foreach (string id in owned)
            {
                string parent = ParentOf(id);
                if (!string.IsNullOrEmpty(parent) && !owned.Contains(parent))
                    return false;
                if (IsKeystone(id) && CountGeneral(owned, BranchOf(id)) < 4)
                    return false;
            }

            return IsEquippedValid(
                       survivalKeystoneId,
                       PermanentGrowthBranch.Survival,
                       owned) &&
                   IsEquippedValid(
                       leapKeystoneId,
                       PermanentGrowthBranch.Leap,
                       owned) &&
                   IsEquippedValid(
                       inkKeystoneId,
                       PermanentGrowthBranch.InkHandling,
                       owned);
        }

        static string ParentOf(string id)
        {
            if (id == null || id.EndsWith("00", StringComparison.Ordinal))
                return string.Empty;
            if (IsKeystone(id))
                return $"{id[0]}-{id[3]}3";
            int rank = id[id.Length - 1] - '0';
            return rank <= 1
                ? $"{id[0]}00"
                : $"{id[0]}-{id[2]}{rank - 1}";
        }

        static bool IsEquippedValid(
            string equippedId,
            PermanentGrowthBranch branch,
            HashSet<string> owned)
        {
            if (string.IsNullOrEmpty(equippedId))
                return true;
            if (!AllIds.Contains(equippedId) ||
                !IsKeystone(equippedId) ||
                BranchOf(equippedId) != branch)
                return false;
            char path = equippedId[3];
            foreach (string id in owned)
                if (BranchOf(id) == branch && PathOf(id) == path)
                    return true;
            return false;
        }

        static int CountGeneral(
            HashSet<string> owned,
            PermanentGrowthBranch branch) =>
            owned.Count(id => BranchOf(id) == branch && !IsKeystone(id));

        static bool IsKeystone(string id) =>
            id != null && id.Length == 4 && id[2] == 'K';

        static char PathOf(string id) =>
            id != null && id.Length >= 3 && id[1] == '-'
                ? id[id[2] == 'K' ? 3 : 2]
                : '\0';

        static PermanentGrowthBranch BranchOf(string id) => id?[0] switch
        {
            'S' => PermanentGrowthBranch.Survival,
            'J' => PermanentGrowthBranch.Leap,
            _ => PermanentGrowthBranch.InkHandling,
        };
    }
}
