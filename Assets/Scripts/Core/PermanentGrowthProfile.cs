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

    /// 게임 종료 뒤에도 유지되는 먹방울이의 재화·단계를 소유한다.
    /// RunGrowthController의 한 판 단계와 저장·enum·이벤트를 공유하지 않는다.
    public static class PermanentGrowthProfile
    {
        const int SchemaVersion = 1;
        const int BalanceVersion = 1;

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
            public List<RankRecord> ranks = new();
        }

        static IPermanentGrowthStore store = new PlayerPrefsPermanentGrowthStore();
        static SaveData data;
        static bool loaded;

        public static event Action Changed;

        public static int Currency
        {
            get
            {
                EnsureLoaded();
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

        public static float InkCapacityMultiplier =>
            1f + GetEffect(PermanentGrowthType.InkCapacity);
        public static float InkRecoveryMultiplier =>
            1f + GetEffect(PermanentGrowthType.InkRecovery);
        public static float PlatformLifetimeMultiplier =>
            1f + GetEffect(PermanentGrowthType.PlatformLifetime);
        public static float JumpChargeMultiplier =>
            Mathf.Max(0.5f, 1f - GetEffect(PermanentGrowthType.JumpCharge));
        public static int MaxHealthBonus =>
            GetLevel(PermanentGrowthType.Vitality);
        public static float DamageGraceBonusSeconds =>
            GetEffect(PermanentGrowthType.DamageGrace);
        public static float CloneSpawnGraceBonusSeconds =>
            GetEffect(PermanentGrowthType.CloneSpawnGrace);
        public static bool HasLastBreath =>
            GetLevel(PermanentGrowthType.LastBreath) > 0;
        public static float JumpPowerMultiplier =>
            1f + GetEffect(PermanentGrowthType.JumpPower);
        public static float DrawnPlatformLeapMultiplier =>
            1f + GetEffect(PermanentGrowthType.DrawnPlatformLeap);
        public static bool NewPlatformsHaveStrokeGuard =>
            GetLevel(PermanentGrowthType.StrokeGuard) > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            store = new PlayerPrefsPermanentGrowthStore();
            data = null;
            loaded = false;
            Changed = null;
        }

        public static int GetLevel(PermanentGrowthType type)
        {
            EnsureLoaded();
            var definition = PermanentGrowthCatalog.Get(type);
            if (definition == null) return 0;
            RankRecord record = FindRank(definition.Id);
            return record != null
                ? Mathf.Clamp(record.level, 0, definition.MaxLevel)
                : 0;
        }

        public static int GetNextCost(PermanentGrowthType type)
        {
            var definition = PermanentGrowthCatalog.Get(type);
            return definition?.GetCost(GetLevel(type)) ?? 0;
        }

        public static bool CanPurchase(PermanentGrowthType type)
        {
            return CanPurchaseNode(
                PermanentGrowthCatalog.GetNode(type, GetLevel(type) + 1));
        }

        public static bool TryPurchase(PermanentGrowthType type)
        {
            return TryPurchaseNode(
                PermanentGrowthCatalog.GetNode(type, GetLevel(type) + 1));
        }

        public static bool IsNodeUnlocked(
            PermanentGrowthNodeDefinition node)
        {
            return node != null && GetLevel(node.Type) >= node.Rank;
        }

        public static bool IsNodeUnlocked(string nodeId)
        {
            return IsNodeUnlocked(PermanentGrowthCatalog.GetNode(nodeId));
        }

        public static bool IsNodeUnlocked(
            PermanentGrowthType type,
            int rank)
        {
            return IsNodeUnlocked(PermanentGrowthCatalog.GetNode(type, rank));
        }

        public static bool MeetsNodeRequirements(
            PermanentGrowthNodeDefinition node)
        {
            if (node == null)
                return false;
            if (IsNodeUnlocked(node))
                return true;

            for (int i = 0; i < node.ParentIds.Count; i++)
                if (!IsNodeUnlocked(node.ParentIds[i]))
                    return false;
            return true;
        }

        public static bool MeetsNodeRequirements(string nodeId)
        {
            return MeetsNodeRequirements(PermanentGrowthCatalog.GetNode(nodeId));
        }

        public static bool MeetsNodeRequirements(
            PermanentGrowthType type,
            int rank)
        {
            return MeetsNodeRequirements(
                PermanentGrowthCatalog.GetNode(type, rank));
        }

        public static bool CanPurchaseNode(
            PermanentGrowthNodeDefinition node)
        {
            if (node == null)
                return false;
            int level = GetLevel(node.Type);
            return node.Rank == level + 1 &&
                   node.Cost > 0 &&
                   Currency >= node.Cost &&
                   MeetsNodeRequirements(node);
        }

        public static bool CanPurchaseNode(string nodeId)
        {
            return CanPurchaseNode(PermanentGrowthCatalog.GetNode(nodeId));
        }

        public static bool CanPurchaseNode(
            PermanentGrowthType type,
            int rank)
        {
            return CanPurchaseNode(PermanentGrowthCatalog.GetNode(type, rank));
        }

        public static bool TryPurchaseNode(
            PermanentGrowthNodeDefinition node)
        {
            EnsureLoaded();
            if (node == null ||
                !PermanentGrowthCatalog.TryGetNode(
                    node.Id,
                    out PermanentGrowthNodeDefinition catalogNode))
                return false;

            node = catalogNode;
            int level = GetLevel(node.Type);
            int cost = node.Cost;
            if (node.Rank != level + 1 ||
                cost <= 0 ||
                data.wallet < cost ||
                !MeetsNodeRequirements(node))
                return false;

            PermanentGrowthDefinition definition = node.TrackDefinition;
            RankRecord record = FindRank(definition.Id);
            if (record == null)
            {
                record = new RankRecord { id = definition.Id };
                data.ranks.Add(record);
            }
            record.level = node.Rank;
            data.wallet -= cost;
            data.spent += cost;
            Save();
            Changed?.Invoke();
            return true;
        }

        public static bool TryPurchaseNode(string nodeId)
        {
            return TryPurchaseNode(PermanentGrowthCatalog.GetNode(nodeId));
        }

        public static bool TryPurchaseNode(
            PermanentGrowthType type,
            int rank)
        {
            return TryPurchaseNode(PermanentGrowthCatalog.GetNode(type, rank));
        }

        public static string GetNodeLockReason(
            PermanentGrowthNodeDefinition node)
        {
            if (node == null)
                return "알 수 없는 성장";
            if (IsNodeUnlocked(node))
                return string.Empty;

            var missing = new List<string>();
            for (int i = 0; i < node.ParentIds.Count; i++)
            {
                string parentId = node.ParentIds[i];
                if (IsNodeUnlocked(parentId))
                    continue;

                PermanentGrowthNodeDefinition parent =
                    PermanentGrowthCatalog.GetNode(parentId);
                string name = parent != null
                    ? parent.TrackMaxLevel > 1
                        ? $"{parent.Name} {parent.Rank}단계 필요"
                        : $"{parent.Name} 필요"
                    : $"{parentId} 필요";
                missing.Add(name);
            }

            return missing.Count == 0
                ? string.Empty
                : string.Join(" · ", missing);
        }

        public static string GetNodeLockReason(string nodeId)
        {
            return GetNodeLockReason(PermanentGrowthCatalog.GetNode(nodeId));
        }

        public static string GetNodeLockReason(
            PermanentGrowthType type,
            int rank)
        {
            return GetNodeLockReason(
                PermanentGrowthCatalog.GetNode(type, rank));
        }

        /// 기존 저장에서 이미 한 단계 이상 구매한 노드는 새 계보 선행 조건을
        /// 소급 적용하지 않는다. 새 0레벨 노드만 현재 선행 단계를 검사한다.
        public static bool MeetsRequirements(PermanentGrowthType type)
        {
            var definition = PermanentGrowthCatalog.Get(type);
            if (definition == null)
                return false;
            if (GetLevel(type) > 0)
                return true;

            for (int i = 0; i < definition.Requirements.Count; i++)
            {
                PermanentGrowthRequirement requirement =
                    definition.Requirements[i];
                if (GetLevel(requirement.Type) < requirement.MinimumLevel)
                    return false;
            }
            return true;
        }

        /// 잠긴 0레벨 노드의 선행 조건을 UI가 별도 규칙 계산 없이 표시한다.
        /// 구매 가능하거나 기존 저장으로 이미 열린 노드는 빈 문자열을 반환한다.
        public static string GetLockReason(PermanentGrowthType type)
        {
            var definition = PermanentGrowthCatalog.Get(type);
            if (definition == null)
                return "알 수 없는 성장";
            if (GetLevel(type) > 0)
                return string.Empty;

            var missing = new List<string>();
            for (int i = 0; i < definition.Requirements.Count; i++)
            {
                PermanentGrowthRequirement requirement =
                    definition.Requirements[i];
                int level = GetLevel(requirement.Type);
                if (level >= requirement.MinimumLevel)
                    continue;

                PermanentGrowthDefinition requiredDefinition =
                    PermanentGrowthCatalog.Get(requirement.Type);
                string name = requiredDefinition != null
                    ? requiredDefinition.Name
                    : requirement.Type.ToString();
                missing.Add($"{name} Lv. {requirement.MinimumLevel} 필요");
            }

            return missing.Count == 0
                ? string.Empty
                : string.Join(" · ", missing);
        }

        /// 정상 게임오버 한 번을 runId로 멱등 정산한다.
        /// 디버그·중도 포기 판은 보상을 주지 않으며 첫 무료 보상도 소비하지 않는다.
        public static PermanentGrowthSettlement SettleRun(
            string runId,
            int height,
            int previousBest,
            bool eligible)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(runId))
                return new PermanentGrowthSettlement(0, data.wallet, false);
            if (string.Equals(data.lastSettledRunId, runId, StringComparison.Ordinal))
                return new PermanentGrowthSettlement(0, data.wallet, false);

            data.lastSettledRunId = runId;
            int earned = 0;
            if (eligible)
            {
                earned = RunRewardCalculator.Calculate(
                    height,
                    previousBest,
                    !data.tutorialRewardClaimed);
                data.tutorialRewardClaimed = true;
                int remainingBudget = Mathf.Max(
                    0,
                    PermanentGrowthCatalog.TotalCost - data.wallet - data.spent);
                earned = Mathf.Min(earned, remainingBudget);
                data.wallet += earned;
            }

            Save();
            if (earned > 0)
                Changed?.Invoke();
            return new PermanentGrowthSettlement(earned, data.wallet, true);
        }

        static float GetEffect(PermanentGrowthType type)
        {
            var definition = PermanentGrowthCatalog.Get(type);
            return definition != null
                ? definition.EffectPerLevel * GetLevel(type)
                : 0f;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
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

            if (data.ranks == null)
                data.ranks = new List<RankRecord>();
            if (data.balanceVersion != BalanceVersion)
            {
                data.wallet = Mathf.Clamp(
                    data.wallet + Mathf.Max(0, data.spent),
                    0,
                    PermanentGrowthCatalog.TotalCost);
                data.spent = 0;
                data.ranks.Clear();
                data.balanceVersion = BalanceVersion;
            }

            NormalizeLoadedData();
        }

        static void NormalizeLoadedData()
        {
            int calculatedSpent = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = data.ranks.Count - 1; i >= 0; i--)
            {
                RankRecord record = data.ranks[i];
                if (record == null ||
                    !PermanentGrowthCatalog.TryGet(record.id, out var definition) ||
                    !seen.Add(record.id))
                {
                    data.ranks.RemoveAt(i);
                    continue;
                }

                record.level = Mathf.Clamp(record.level, 0, definition.MaxLevel);
                calculatedSpent += definition.CostThroughLevel(record.level);
            }

            int refund = Mathf.Max(0, data.spent - calculatedSpent);
            data.spent = Mathf.Clamp(
                calculatedSpent,
                0,
                PermanentGrowthCatalog.TotalCost);
            data.wallet = Mathf.Clamp(
                Mathf.Max(0, data.wallet) + refund,
                0,
                PermanentGrowthCatalog.TotalCost - data.spent);
            if (data.lastSettledRunId == null)
                data.lastSettledRunId = string.Empty;
            Save();
        }

        static RankRecord FindRank(string id)
        {
            if (data?.ranks == null) return null;
            for (int i = 0; i < data.ranks.Count; i++)
                if (data.ranks[i] != null &&
                    string.Equals(data.ranks[i].id, id, StringComparison.Ordinal))
                    return data.ranks[i];
            return null;
        }

        static void Save()
        {
            if (data == null) return;
            store.Save(JsonUtility.ToJson(data));
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(IPermanentGrowthStore testStore)
        {
            store = testStore ?? throw new ArgumentNullException(nameof(testStore));
            data = null;
            loaded = false;
            Changed = null;
        }

        public static void ResetCacheForTests()
        {
            data = null;
            loaded = false;
            Changed = null;
        }

        public static void RestoreDefaultStoreForTests()
        {
            store = new PlayerPrefsPermanentGrowthStore();
            data = null;
            loaded = false;
            Changed = null;
        }
#endif
    }

#if UNITY_EDITOR
    /// 로컬 PlayerPrefs와 최고 기록을 건드리지 않는 EditMode 테스트 저장소.
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
