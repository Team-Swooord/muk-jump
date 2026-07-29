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
            var definition = PermanentGrowthCatalog.Get(type);
            if (definition == null) return false;
            int level = GetLevel(type);
            int cost = definition.GetCost(level);
            return level < definition.MaxLevel && cost > 0 && Currency >= cost;
        }

        public static bool TryPurchase(PermanentGrowthType type)
        {
            EnsureLoaded();
            var definition = PermanentGrowthCatalog.Get(type);
            if (definition == null) return false;

            int level = GetLevel(type);
            int cost = definition.GetCost(level);
            if (level >= definition.MaxLevel || cost <= 0 || data.wallet < cost)
                return false;

            RankRecord record = FindRank(definition.Id);
            if (record == null)
            {
                record = new RankRecord { id = definition.Id };
                data.ranks.Add(record);
            }
            record.level = level + 1;
            data.wallet -= cost;
            data.spent += cost;
            Save();
            Changed?.Invoke();
            return true;
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
