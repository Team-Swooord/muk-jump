using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    public interface IPermanentGrowthStore
    {
        string Load();
        void Save(string json);
    }

    sealed class PlayerPrefsPermanentGrowthStore : IPermanentGrowthStore
    {
        const string SaveKey = "MukJump.PermanentGrowth.v1";
        const string ObsoleteFocusKey = "MukJump.GrowthFocusId";

        public string Load()
        {
            if (PlayerPrefs.HasKey(ObsoleteFocusKey))
            {
                PlayerPrefs.DeleteKey(ObsoleteFocusKey);
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetString(SaveKey, string.Empty);
        }

        public void Save(string json)
        {
            PlayerPrefs.SetString(SaveKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public readonly struct PermanentGrowthSettlement
    {
        public PermanentGrowthSettlement(int earned, int balance, bool accepted)
        {
            Earned = earned;
            Balance = balance;
            Accepted = accepted;
        }

        public int Earned { get; }
        public int Balance { get; }
        public bool Accepted { get; }
    }

    /// 게임 종료 뒤에도 유지되는 먹빛·열매 소유·비기 장착 상태를 소유한다.
    public static class PermanentGrowthProfile
    {
        const int SchemaVersion = 1;
        const int BalanceVersion = 4;
        const int V2TotalCost = 39;
        const int LegacyTotalCost = 957;
        const int SettledRunHistoryLimit = 64;

        [Serializable]
        sealed class RankRecord
        {
            public string id;
            public int level;
        }

        [Serializable]
        sealed class SaveData
        {
            public int schemaVersion = SchemaVersion;
            public int balanceVersion = BalanceVersion;
            public int wallet;
            public int spent;
            public bool tutorialRewardClaimed;
            public string lastSettledRunId = string.Empty;
            public List<string> settledRunIds = new();
            // balanceVersion 1 역직렬화·마이그레이션 전용.
            public List<RankRecord> ranks = new();
            public List<string> ownedNodeIds = new();
            public string survivalKeystoneId = string.Empty;
            public string leapKeystoneId = string.Empty;
            public string inkHandlingKeystoneId = string.Empty;
        }

        readonly struct LegacyTrack
        {
            public LegacyTrack(
                string id,
                PermanentGrowthBranch branch,
                params int[] costs)
            {
                Id = id;
                Branch = branch;
                Costs = costs ?? Array.Empty<int>();
            }

            public string Id { get; }
            public PermanentGrowthBranch Branch { get; }
            public int[] Costs { get; }
        }

        static readonly LegacyTrack[] LegacyTracks =
        {
            new("permanent.ink_capacity", PermanentGrowthBranch.InkHandling,
                6, 10, 16, 24, 34, 46),
            new("permanent.ink_recovery", PermanentGrowthBranch.InkHandling,
                6, 10, 16, 24, 34, 46),
            new("permanent.platform_lifetime", PermanentGrowthBranch.InkHandling,
                7, 11, 17, 25, 35, 47),
            new("permanent.jump_charge", PermanentGrowthBranch.Leap,
                7, 12, 18, 26, 36, 48),
            new("permanent.vitality", PermanentGrowthBranch.Survival, 24),
            new("permanent.damage_grace", PermanentGrowthBranch.Survival,
                8, 16, 28),
            new("permanent.last_breath", PermanentGrowthBranch.Survival, 56),
            new("permanent.jump_power", PermanentGrowthBranch.Leap,
                8, 13, 19, 27, 37),
            new("permanent.drawn_platform_leap", PermanentGrowthBranch.Leap, 52),
            new("permanent.stroke_guard", PermanentGrowthBranch.InkHandling, 56),
            new("permanent.clone_spawn_grace", PermanentGrowthBranch.Survival,
                8, 16, 28),
        };

        static readonly HashSet<string> RetiredLeapNodeIds = new(
            new[]
            {
                "J-A4", "J-A5",
                "J-B4", "J-B5",
                "J-C4", "J-C5",
            },
            StringComparer.Ordinal);

        static IPermanentGrowthStore store = new PlayerPrefsPermanentGrowthStore();
        static SaveData data;
        static bool loaded;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const int DebugGrowthCurrency = 999;
        static int debugCurrencyOverride = -1;
#endif

        public static event Action Changed;

        public static int Currency
        {
            get
            {
                EnsureLoaded();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugCurrencyOverride >= 0)
                    return debugCurrencyOverride;
#endif
                return data.wallet;
            }
        }

        public static int SpentCurrency
        {
            get
            {
                EnsureLoaded();
                return data.spent;
            }
        }

        public static int OwnedNodeCount
        {
            get
            {
                EnsureLoaded();
                return data.ownedNodeIds.Count;
            }
        }

        // 로비·구 코드 호환용 조회. 실제 판에서는 영구 성장 런타임 스냅샷을 쓴다.
        public static float InkCapacityMultiplier =>
            CreateRunSnapshot().InkCapacityMultiplier;
        public static float InkRecoveryMultiplier =>
            CreateRunSnapshot().InkRecoveryMultiplier;
        public static float PlatformLifetimeMultiplier =>
            CreateRunSnapshot().PlatformLifetimeMultiplier;
        public static float JumpChargeMultiplier =>
            CreateRunSnapshot().JumpChargeMultiplier;
        public static int MaxHealthBonus => CreateRunSnapshot().MaxHealthBonus;
        public static float DamageGraceBonusSeconds =>
            CreateRunSnapshot().DamageGraceBonusSeconds;
        public static float CloneSpawnGraceBonusSeconds =>
            CreateRunSnapshot().CloneSpawnGraceBonusSeconds;
        public static bool HasLastBreath => CreateRunSnapshot().HasLastBreath;
        public static float JumpPowerMultiplier =>
            CreateRunSnapshot().JumpPowerMultiplier;
        public static float DrawnPlatformLeapMultiplier =>
            CreateRunSnapshot().DrawnPlatformLeapMultiplier;
        public static bool NewPlatformsHaveStrokeGuard =>
            CreateRunSnapshot().HasSharedStrokeGuard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            store = new PlayerPrefsPermanentGrowthStore();
            data = null;
            loaded = false;
            Changed = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugCurrencyOverride = -1;
#endif
        }

        public static PermanentGrowthRunSnapshot CreateRunSnapshot()
        {
            EnsureLoaded();
            var equipped = new Dictionary<PermanentGrowthBranch, string>(3)
            {
                [PermanentGrowthBranch.Survival] = data.survivalKeystoneId,
                [PermanentGrowthBranch.Leap] = data.leapKeystoneId,
                [PermanentGrowthBranch.InkHandling] = data.inkHandlingKeystoneId,
            };
            return new PermanentGrowthRunSnapshot(data.ownedNodeIds, equipped);
        }

        public static int GetLevel(PermanentGrowthType type)
        {
            EnsureLoaded();
            int level = 0;
            for (int i = 0; i < data.ownedNodeIds.Count; i++)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(data.ownedNodeIds[i]);
                if (node != null && node.EffectId == type)
                    level++;
            }
            return level;
        }

        public static int GetNextCost(PermanentGrowthType type) =>
            FindNextNode(type) != null ? 1 : 0;

        public static bool CanPurchase(PermanentGrowthType type) =>
            CanPurchaseNode(FindNextNode(type));

        public static bool TryPurchase(PermanentGrowthType type) =>
            TryPurchaseNode(FindNextNode(type));

        public static bool IsNodeUnlocked(PermanentGrowthNodeDefinition node) =>
            node != null && IsNodeUnlocked(node.Id);

        public static bool IsNodeUnlocked(string nodeId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(nodeId))
                return false;
            for (int i = 0; i < data.ownedNodeIds.Count; i++)
                if (string.Equals(data.ownedNodeIds[i], nodeId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static bool IsNodeUnlocked(PermanentGrowthType type, int rank) =>
            IsNodeUnlocked(PermanentGrowthCatalog.GetNode(type, rank));

        public static bool MeetsNodeRequirements(PermanentGrowthNodeDefinition node)
        {
            if (node == null)
                return false;
            if (IsNodeUnlocked(node))
                return true;

            for (int i = 0; i < node.ParentIds.Count; i++)
                if (!IsNodeUnlocked(node.ParentIds[i]))
                    return false;

            return CountOwnedGeneralNodes(node.Branch) >=
                   node.RequiredOwnedCountInBranch;
        }

        public static bool MeetsNodeRequirements(string nodeId) =>
            MeetsNodeRequirements(PermanentGrowthCatalog.GetNode(nodeId));

        public static bool MeetsNodeRequirements(PermanentGrowthType type, int rank) =>
            MeetsNodeRequirements(PermanentGrowthCatalog.GetNode(type, rank));

        public static bool CanPurchaseNode(PermanentGrowthNodeDefinition node)
        {
            return node != null &&
                   !IsNodeUnlocked(node) &&
                   node.Cost > 0 &&
                   Currency >= node.Cost &&
                   MeetsNodeRequirements(node);
        }

        public static bool CanPurchaseNode(string nodeId) =>
            CanPurchaseNode(PermanentGrowthCatalog.GetNode(nodeId));

        public static bool CanPurchaseNode(PermanentGrowthType type, int rank) =>
            CanPurchaseNode(PermanentGrowthCatalog.GetNode(type, rank));

        public static bool TryPurchaseNode(PermanentGrowthNodeDefinition node)
        {
            EnsureLoaded();
            if (node == null ||
                !PermanentGrowthCatalog.TryGetNode(node.Id, out var catalogNode) ||
                !CanPurchaseNode(catalogNode))
                return false;

            data.ownedNodeIds.Add(catalogNode.Id);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugCurrencyOverride >= 0)
                debugCurrencyOverride -= catalogNode.Cost;
            else
#endif
            data.wallet -= catalogNode.Cost;
            data.spent = data.ownedNodeIds.Count;

            if (catalogNode.IsKeystone &&
                string.IsNullOrEmpty(GetActiveKeystoneId(catalogNode.Branch)))
                SetActiveKeystoneId(catalogNode.Branch, catalogNode.Id);

            Save();
            Changed?.Invoke();
            return true;
        }

        public static bool TryPurchaseNode(string nodeId) =>
            TryPurchaseNode(PermanentGrowthCatalog.GetNode(nodeId));

        public static bool TryPurchaseNode(PermanentGrowthType type, int rank) =>
            TryPurchaseNode(PermanentGrowthCatalog.GetNode(type, rank));

        public static bool IsKeystoneActive(string nodeId)
        {
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            return node != null &&
                   node.IsKeystone &&
                   string.Equals(
                       GetActiveKeystoneId(node.Branch),
                       node.Id,
                       StringComparison.Ordinal);
        }

        public static string GetActiveKeystoneId(PermanentGrowthBranch branch)
        {
            EnsureLoaded();
            return branch switch
            {
                PermanentGrowthBranch.Survival => data.survivalKeystoneId,
                PermanentGrowthBranch.Leap => data.leapKeystoneId,
                _ => data.inkHandlingKeystoneId,
            } ?? string.Empty;
        }

        /// 로비에서만 쓰는 무료 비기 장착. 한 계보에는 최대 하나만 저장한다.
        public static bool TryEquipKeystone(string nodeId)
        {
            EnsureLoaded();
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            if (node == null || !node.IsKeystone || !IsNodeUnlocked(node))
                return false;
            if (IsKeystoneActive(node.Id))
                return true;

            SetActiveKeystoneId(node.Branch, node.Id);
            Save();
            Changed?.Invoke();
            return true;
        }

        public static bool ClearActiveKeystone(PermanentGrowthBranch branch)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(GetActiveKeystoneId(branch)))
                return false;
            SetActiveKeystoneId(branch, string.Empty);
            Save();
            Changed?.Invoke();
            return true;
        }

        public static string GetNodeLockReason(PermanentGrowthNodeDefinition node)
        {
            if (node == null)
                return "알 수 없는 성장";
            if (IsNodeUnlocked(node))
                return string.Empty;

            var missing = new List<string>();
            for (int i = 0; i < node.ParentIds.Count; i++)
            {
                if (IsNodeUnlocked(node.ParentIds[i]))
                    continue;
                PermanentGrowthNodeDefinition parent =
                    PermanentGrowthCatalog.GetNode(node.ParentIds[i]);
                missing.Add(parent != null
                    ? $"{parent.DisplayName} 필요"
                    : $"{node.ParentIds[i]} 필요");
            }

            int ownedGeneral = CountOwnedGeneralNodes(node.Branch);
            if (ownedGeneral < node.RequiredOwnedCountInBranch)
                missing.Add(
                    $"{PermanentGrowthCatalog.GetBranch(node.Branch).DisplayName} 일반 열매 " +
                    $"{node.RequiredOwnedCountInBranch}개 필요 ({ownedGeneral}개 보유)");

            return missing.Count == 0
                ? string.Empty
                : string.Join(" · ", missing);
        }

        public static string GetNodeLockReason(string nodeId) =>
            GetNodeLockReason(PermanentGrowthCatalog.GetNode(nodeId));

        public static string GetNodeLockReason(PermanentGrowthType type, int rank) =>
            GetNodeLockReason(PermanentGrowthCatalog.GetNode(type, rank));

        public static bool MeetsRequirements(PermanentGrowthType type)
        {
            PermanentGrowthNodeDefinition node = FindNextNode(type);
            return node == null || MeetsNodeRequirements(node);
        }

        public static string GetLockReason(PermanentGrowthType type) =>
            GetNodeLockReason(FindNextNode(type));

        /// 정상 게임오버를 runId로 멱등 정산한다. 기본 보상은 먹떼 진행 고도와
        /// 실제 플레이 시간, 이정표는 최고 점수 고도를 서로 분리해 계산한다.
        public static PermanentGrowthSettlement SettleRun(
            string runId,
            int swarmProgressHeight,
            int scoreHeight,
            int previousBest,
            float activeGameplaySeconds,
            bool eligible)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(runId))
                return new PermanentGrowthSettlement(0, data.wallet, false);
            if (HasSettledRunId(runId))
                return new PermanentGrowthSettlement(0, data.wallet, false);

            data.lastSettledRunId = runId;
            data.settledRunIds.Add(runId);
            TrimSettledRunHistory();
            int earned = 0;
            if (eligible)
            {
                earned = RunRewardCalculator.Calculate(
                    swarmProgressHeight,
                    scoreHeight,
                    previousBest,
                    activeGameplaySeconds,
                    !data.tutorialRewardClaimed);
                data.tutorialRewardClaimed = true;
                int remainingBudget = Mathf.Max(
                    0,
                    PermanentGrowthCatalog.TotalCost -
                    data.ownedNodeIds.Count -
                    data.wallet);
                earned = Mathf.Min(earned, remainingBudget);
                data.wallet += earned;
            }

            Save();
            if (earned > 0)
                Changed?.Invoke();
            return new PermanentGrowthSettlement(earned, data.wallet, true);
        }

        /// 구 호출부 호환. 새 게임 코드는 진행 고도·실제 시간을 명시하는 오버로드를 쓴다.
        public static PermanentGrowthSettlement SettleRun(
            string runId,
            int height,
            int previousBest,
            bool eligible)
        {
            return SettleRun(
                runId,
                height,
                height,
                previousBest,
                float.PositiveInfinity,
                eligible);
        }

        static PermanentGrowthNodeDefinition FindNextNode(PermanentGrowthType type)
        {
            IReadOnlyList<PermanentGrowthNodeDefinition> nodes =
                PermanentGrowthCatalog.Nodes;
            PermanentGrowthNodeDefinition fallback = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                PermanentGrowthNodeDefinition node = nodes[i];
                if (node.EffectId != type || IsNodeUnlocked(node))
                    continue;
                fallback ??= node;
                if (MeetsNodeRequirements(node))
                    return node;
            }
            return fallback;
        }

        static int CountOwnedGeneralNodes(PermanentGrowthBranch branch)
        {
            EnsureLoaded();
            int count = 0;
            for (int i = 0; i < data.ownedNodeIds.Count; i++)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(data.ownedNodeIds[i]);
                if (node != null && node.Branch == branch && !node.IsKeystone)
                    count++;
            }
            return count;
        }

        static void SetActiveKeystoneId(
            PermanentGrowthBranch branch,
            string nodeId)
        {
            nodeId ??= string.Empty;
            switch (branch)
            {
                case PermanentGrowthBranch.Survival:
                    data.survivalKeystoneId = nodeId;
                    break;
                case PermanentGrowthBranch.Leap:
                    data.leapKeystoneId = nodeId;
                    break;
                default:
                    data.inkHandlingKeystoneId = nodeId;
                    break;
            }
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            string json = store.Load();
            if (string.IsNullOrEmpty(json))
            {
                data = new SaveData();
                return;
            }

            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                data = null;
            }

            if (data == null || data.schemaVersion != SchemaVersion)
            {
                data = new SaveData();
                Save();
                return;
            }

            data.ranks ??= new List<RankRecord>();
            data.ownedNodeIds ??= new List<string>();
            // 단계별로 올려야 v2의 ownedNodeIds를 구 ranks로 오인해 지우지 않는다.
            if (data.balanceVersion < 2)
                MigrateLegacyBalance();
            if (data.balanceVersion < 3)
                MigrateLeapTreeToV3();
            if (data.balanceVersion < 4)
                MigrateLeapTreeToV4();
            NormalizeLoadedData();
        }

        static void MigrateLegacyBalance()
        {
            var branchLevels = new Dictionary<PermanentGrowthBranch, int>
            {
                [PermanentGrowthBranch.Survival] = 0,
                [PermanentGrowthBranch.Leap] = 0,
                [PermanentGrowthBranch.InkHandling] = 0,
            };
            int legacyCalculatedSpent = 0;

            for (int recordIndex = 0; recordIndex < data.ranks.Count; recordIndex++)
            {
                RankRecord record = data.ranks[recordIndex];
                if (record == null || string.IsNullOrEmpty(record.id))
                    continue;
                for (int trackIndex = 0; trackIndex < LegacyTracks.Length; trackIndex++)
                {
                    LegacyTrack track = LegacyTracks[trackIndex];
                    if (!string.Equals(track.Id, record.id, StringComparison.Ordinal))
                        continue;
                    int level = Mathf.Clamp(record.level, 0, track.Costs.Length);
                    branchLevels[track.Branch] += level;
                    for (int costIndex = 0; costIndex < level; costIndex++)
                        legacyCalculatedSpent += Mathf.Max(0, track.Costs[costIndex]);
                    break;
                }
            }

            data.ownedNodeIds.Clear();
            int overflowRefund = 0;
            foreach (PermanentGrowthBranch branch
                     in Enum.GetValues(typeof(PermanentGrowthBranch)))
            {
                IReadOnlyList<string> order = PermanentGrowthCatalog.MigrationOrder(branch);
                int legacyCount = Mathf.Max(0, branchLevels[branch]);
                int mappedCount = Mathf.Min(order.Count, legacyCount);
                for (int i = 0; i < mappedCount; i++)
                    data.ownedNodeIds.Add(order[i]);
                overflowRefund += Mathf.Max(0, legacyCount - mappedCount);
            }

            int oldSpent = Mathf.Clamp(
                Mathf.Max(legacyCalculatedSpent, data.spent),
                0,
                LegacyTotalCost);
            int oldRemainingCost = Mathf.Max(1, LegacyTotalCost - oldSpent);
            int newRemainingNodes = Mathf.Max(
                0,
                V2TotalCost - data.ownedNodeIds.Count);
            float walletProgress = Mathf.Clamp01(
                Mathf.Max(0, data.wallet) / (float)oldRemainingCost);
            int convertedWallet = Mathf.RoundToInt(
                walletProgress * newRemainingNodes);
            data.wallet = Mathf.Clamp(
                convertedWallet + overflowRefund,
                0,
                newRemainingNodes);
            data.spent = data.ownedNodeIds.Count;
            data.ranks.Clear();
            data.balanceVersion = 2;

            AutoEquipFirstOwnedKeystone(PermanentGrowthBranch.Survival);
            AutoEquipFirstOwnedKeystone(PermanentGrowthBranch.Leap);
            AutoEquipFirstOwnedKeystone(PermanentGrowthBranch.InkHandling);
        }

        /// 도약 계보가 5단계씩으로 늘어난 v3 규칙 마이그레이션.
        /// 기존 비기를 보유한 저장은 해당 길을 이미 완주한 것으로 보고 새 중간 노드를
        /// 채워, 해금된 비기가 끊긴 가지 끝에 떠 보이지 않게 한다.
        static void MigrateLeapTreeToV3()
        {
            data.ownedNodeIds ??= new List<string>();
            var owned = new HashSet<string>(
                data.ownedNodeIds,
                StringComparer.Ordinal);

            CompleteGrandfatheredPath(owned, "J-KA", "J00",
                "J-A1", "J-A2", "J-A3", "J-A4", "J-A5");
            CompleteGrandfatheredPath(owned, "J-KB", "J00",
                "J-B1", "J-B2", "J-B3", "J-B4", "J-B5");
            CompleteGrandfatheredPath(owned, "J-KC", "J00",
                "J-C1", "J-C2", "J-C3", "J-C4", "J-C5");

            var migrated = new List<string>(PermanentGrowthCatalog.TotalCost);
            for (int i = 0; i < PermanentGrowthCatalog.Nodes.Count; i++)
            {
                string nodeId = PermanentGrowthCatalog.Nodes[i].Id;
                if (owned.Contains(nodeId))
                    migrated.Add(nodeId);
            }
            data.ownedNodeIds = migrated;
            data.spent = data.ownedNodeIds.Count;
            // 지갑 상한은 바로 뒤 NormalizeLoadedData에서 유효 ID 수를 확정한 뒤
            // 한 번만 계산한다. 먼저 줄이면 구 저장의 먹빛을 잃을 수 있다.
            data.balanceVersion = 3;
        }

        /// 도약 계보를 다른 계보와 같은 3단계 구조로 줄인다.
        /// v3에서 구매했던 삭제 노드는 한 개당 먹빛 하나로 돌려준다.
        static void MigrateLeapTreeToV4()
        {
            data.ownedNodeIds ??= new List<string>();
            var kept = new List<string>(data.ownedNodeIds.Count);
            var seenRetired = new HashSet<string>(StringComparer.Ordinal);
            int refund = 0;

            for (int i = 0; i < data.ownedNodeIds.Count; i++)
            {
                string nodeId = data.ownedNodeIds[i];
                if (RetiredLeapNodeIds.Contains(nodeId))
                {
                    if (seenRetired.Add(nodeId))
                        refund++;
                    continue;
                }
                kept.Add(nodeId);
            }

            data.ownedNodeIds = kept;
            data.wallet = Mathf.Max(0, data.wallet) + refund;
            data.spent = data.ownedNodeIds.Count;
            data.balanceVersion = 4;
        }

        static void CompleteGrandfatheredPath(
            HashSet<string> owned,
            string keystoneId,
            params string[] pathIds)
        {
            if (owned == null || !owned.Contains(keystoneId) || pathIds == null)
                return;
            for (int i = 0; i < pathIds.Length; i++)
                owned.Add(pathIds[i]);
        }

        static void NormalizeLoadedData()
        {
            var normalized = new List<string>(PermanentGrowthCatalog.TotalCost);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.ownedNodeIds.Count; i++)
            {
                string nodeId = data.ownedNodeIds[i];
                if (!PermanentGrowthCatalog.TryGetNode(nodeId, out _) ||
                    !seen.Add(nodeId))
                    continue;
                normalized.Add(nodeId);
            }
            data.ownedNodeIds = normalized;
            data.spent = data.ownedNodeIds.Count;
            data.wallet = Mathf.Clamp(
                data.wallet,
                0,
                PermanentGrowthCatalog.TotalCost - data.spent);
            data.lastSettledRunId ??= string.Empty;
            NormalizeSettledRunHistory();
            data.survivalKeystoneId = NormalizeEquipped(
                PermanentGrowthBranch.Survival,
                data.survivalKeystoneId);
            data.leapKeystoneId = NormalizeEquipped(
                PermanentGrowthBranch.Leap,
                data.leapKeystoneId);
            data.inkHandlingKeystoneId = NormalizeEquipped(
                PermanentGrowthBranch.InkHandling,
                data.inkHandlingKeystoneId);
            data.balanceVersion = BalanceVersion;
            Save();
        }

        static bool HasSettledRunId(string runId)
        {
            if (string.Equals(data.lastSettledRunId, runId, StringComparison.Ordinal))
                return true;
            if (data.settledRunIds == null)
                return false;
            for (int i = 0; i < data.settledRunIds.Count; i++)
                if (string.Equals(data.settledRunIds[i], runId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        static void NormalizeSettledRunHistory()
        {
            data.settledRunIds ??= new List<string>();
            var normalized = new List<string>(SettledRunHistoryLimit);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int first = Mathf.Max(
                0,
                data.settledRunIds.Count - SettledRunHistoryLimit);
            for (int i = first; i < data.settledRunIds.Count; i++)
            {
                string runId = data.settledRunIds[i];
                if (!string.IsNullOrEmpty(runId) && seen.Add(runId))
                    normalized.Add(runId);
            }
            if (!string.IsNullOrEmpty(data.lastSettledRunId) &&
                seen.Add(data.lastSettledRunId))
                normalized.Add(data.lastSettledRunId);
            data.settledRunIds = normalized;
            TrimSettledRunHistory();
        }

        static void TrimSettledRunHistory()
        {
            data.settledRunIds ??= new List<string>();
            int overflow = data.settledRunIds.Count - SettledRunHistoryLimit;
            if (overflow > 0)
                data.settledRunIds.RemoveRange(0, overflow);
        }

        static string NormalizeEquipped(
            PermanentGrowthBranch branch,
            string nodeId)
        {
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            return node != null &&
                   node.IsKeystone &&
                   node.Branch == branch &&
                   IsNodeUnlocked(node)
                ? node.Id
                : string.Empty;
        }

        static void AutoEquipFirstOwnedKeystone(PermanentGrowthBranch branch)
        {
            if (!string.IsNullOrEmpty(GetActiveKeystoneId(branch)))
                return;
            IReadOnlyList<PermanentGrowthNodeDefinition> nodes =
                PermanentGrowthCatalog.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                PermanentGrowthNodeDefinition node = nodes[i];
                if (node.Branch != branch || !node.IsKeystone || !IsNodeUnlocked(node))
                    continue;
                SetActiveKeystoneId(branch, node.Id);
                return;
            }
        }

        static void Save()
        {
            if (data != null)
                store.Save(JsonUtility.ToJson(data));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// 성장 화면 QA용. 보상 중복 방지 값은 보존하고 열매만 초기화한다.
        public static void DebugResetProgress()
        {
            EnsureLoaded();
            data.ranks.Clear();
            data.ownedNodeIds.Clear();
            data.wallet = 0;
            data.spent = 0;
            data.survivalKeystoneId = string.Empty;
            data.leapKeystoneId = string.Empty;
            data.inkHandlingKeystoneId = string.Empty;
            debugCurrencyOverride = DebugGrowthCurrency;
            Save();
            Changed?.Invoke();
        }

        /// 저장 경제 상한은 건드리지 않고 현재 개발 세션에만 먹빛 999를 제공한다.
        public static void DebugRefillCurrency()
        {
            EnsureLoaded();
            debugCurrencyOverride = DebugGrowthCurrency;
            Changed?.Invoke();
        }

        public static bool IsDebugCurrencyActive => debugCurrencyOverride >= 0;
#endif

#if UNITY_EDITOR
        public static void UseStoreForTests(IPermanentGrowthStore testStore)
        {
            store = testStore ?? throw new ArgumentNullException(nameof(testStore));
            data = null;
            loaded = false;
            Changed = null;
            debugCurrencyOverride = -1;
        }

        public static void ResetCacheForTests()
        {
            data = null;
            loaded = false;
            Changed = null;
            debugCurrencyOverride = -1;
        }

        public static void RestoreDefaultStoreForTests()
        {
            store = new PlayerPrefsPermanentGrowthStore();
            data = null;
            loaded = false;
            Changed = null;
            debugCurrencyOverride = -1;
        }
#endif
    }

#if UNITY_EDITOR
    public sealed class MemoryPermanentGrowthStore : IPermanentGrowthStore
    {
        public string Json { get; set; } = string.Empty;
        public int SaveCount { get; private set; }

        public string Load() => Json;

        public void Save(string json)
        {
            Json = json ?? string.Empty;
            SaveCount++;
        }
    }
#endif
}
