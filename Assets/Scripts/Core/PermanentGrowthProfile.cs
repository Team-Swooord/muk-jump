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

    public interface IPermanentGrowthRecoveryStore : IPermanentGrowthStore
    {
        string LoadBackup();
        void SaveBackup(string json);
        void SaveQuarantine(string json);
        void SaveBackupQuarantine(string json);
        bool LoadBackupSyncPending();
        void SaveBackupSyncPending(bool pending);
        string LoadBackupSyncTarget();
        void SaveBackupSyncTarget(string json);
        bool LoadResetPending();
        void SaveResetPending(bool pending);
    }

    sealed class PlayerPrefsPermanentGrowthStore : IPermanentGrowthRecoveryStore
    {
        const string SaveKey = "MukJump.PermanentGrowth.v1";
        const string BackupKey = SaveKey + ".backup";
        const string QuarantineKey = SaveKey + ".quarantine";
        const string BackupQuarantineKey =
            SaveKey + ".backup.quarantine";
        const string BackupSyncPendingKey = SaveKey + ".backup.pending";
        const string BackupSyncTargetKey = SaveKey + ".backup.pending.target";
        const string ResetPendingKey = SaveKey + ".reset.pending";
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

        public string LoadBackup() =>
            PlayerPrefs.GetString(BackupKey, string.Empty);

        public void SaveBackup(string json)
        {
            PlayerPrefs.SetString(BackupKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void SaveQuarantine(string json)
        {
            PlayerPrefs.SetString(QuarantineKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void SaveBackupQuarantine(string json)
        {
            PlayerPrefs.SetString(
                BackupQuarantineKey,
                json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool LoadBackupSyncPending() =>
            PlayerPrefs.GetInt(BackupSyncPendingKey, 0) != 0;

        public void SaveBackupSyncPending(bool pending)
        {
            if (pending)
                PlayerPrefs.SetInt(BackupSyncPendingKey, 1);
            else
                PlayerPrefs.DeleteKey(BackupSyncPendingKey);
            PlayerPrefs.Save();
        }

        public string LoadBackupSyncTarget() =>
            PlayerPrefs.GetString(BackupSyncTargetKey, string.Empty);

        public void SaveBackupSyncTarget(string json)
        {
            if (string.IsNullOrEmpty(json))
                PlayerPrefs.DeleteKey(BackupSyncTargetKey);
            else
                PlayerPrefs.SetString(BackupSyncTargetKey, json);
            PlayerPrefs.Save();
        }

        public bool LoadResetPending() =>
            PlayerPrefs.GetInt(ResetPendingKey, 0) != 0;

        public void SaveResetPending(bool pending)
        {
            if (pending)
                PlayerPrefs.SetInt(ResetPendingKey, 1);
            else
                PlayerPrefs.DeleteKey(ResetPendingKey);
            PlayerPrefs.Save();
        }
    }

    public enum PermanentGrowthLoadState
    {
        Ready,
        MissingPrimaryReadOnly,
        CorruptReadOnly,
        UnsupportedSchemaReadOnly,
        FutureBalanceReadOnly,
        PersistenceFailureReadOnly,
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
        const int BalanceVersion = 5;
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
            // 최고 기록 저장과 성장 저장이 서로 다른 PlayerPrefs 트랜잭션이어도
            // 최초 고도 이정표를 두 번 지급하지 않도록 성장 저장이 권리를 소유한다.
            public bool rewardMilestoneWatermarkInitialized;
            public int rewardedBestHeight;
            public string lastSettledRunId = string.Empty;
            public List<string> settledRunIds = new();
            // balanceVersion 1 역직렬화·마이그레이션 전용.
            public List<RankRecord> ranks = new();
            public List<string> ownedNodeIds = new();
            public string survivalKeystoneId = string.Empty;
            public string leapKeystoneId = string.Empty;
            public string inkHandlingKeystoneId = string.Empty;
        }

        [Serializable]
        sealed class SaveHeader
        {
            // 의도적으로 초기값을 두지 않는다. 누락 필드를 현재 버전으로 오인하면 안 된다.
            public int schemaVersion;
            public int balanceVersion;
        }

        readonly struct MutationSnapshot
        {
            public MutationSnapshot(string json, int debugCurrency)
            {
                Json = json;
                DebugCurrency = debugCurrency;
            }

            public string Json { get; }
            public int DebugCurrency { get; }
        }

        enum PrimaryWriteResult
        {
            Applied,
            DefinitelyNotApplied,
            Unknown,
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

        static readonly string[] CommonRequiredPayloadFields =
        {
            "wallet",
            "spent",
            "tutorialRewardClaimed",
            "lastSettledRunId",
            "ranks",
        };

        static readonly string[] NodeSaveRequiredPayloadFields =
        {
            "ownedNodeIds",
            "survivalKeystoneId",
            "leapKeystoneId",
            "inkHandlingKeystoneId",
        };

        static readonly string[] MilestoneRequiredPayloadFields =
        {
            "rewardMilestoneWatermarkInitialized",
            "rewardedBestHeight",
        };

        static IPermanentGrowthStore store = new PlayerPrefsPermanentGrowthStore();
        static SaveData data;
        static bool loaded;
        static bool writeBlocked;
        static bool backupAvailable;
        static bool backupReadFailed;
        static bool pendingTargetReadFailed;
        static bool pendingTargetInvalid;
        static bool primaryReadFailed;
        static bool preferPhysicalBackupForRecovery;
        static string rejectedPrimaryJson = string.Empty;
        static string rejectedBackupJson = string.Empty;
        static string rejectedPendingTargetJson = string.Empty;
        static string validatedRecoveryJson = string.Empty;
        static string primaryGenerationJson = string.Empty;
        static bool primaryGenerationKnown;
        static PermanentGrowthLoadState loadState = PermanentGrowthLoadState.Ready;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const int DebugGrowthCurrency = 999;
        static int debugCurrencyOverride = -1;
#endif

        public static event Action Changed;

        public static PermanentGrowthLoadState LoadState
        {
            get
            {
                EnsureLoaded();
                return loadState;
            }
        }

        public static bool RequiresRecovery
        {
            get
            {
                EnsureLoaded();
                return writeBlocked;
            }
        }

        public static bool CanRestoreBackup
        {
            get
            {
                EnsureLoaded();
                if (writeBlocked && primaryReadFailed)
                    RefreshRejectedPrimaryAfterReadFailure();
                if (writeBlocked && !backupAvailable && pendingTargetReadFailed)
                    RefreshPendingRecoveryTarget();
                if (writeBlocked && !backupAvailable &&
                    backupReadFailed && !pendingTargetReadFailed)
                    TryLoadSupportedBackup(out _);
                return writeBlocked &&
                       backupAvailable &&
                       store is IPermanentGrowthRecoveryStore;
            }
        }

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
            ResetLoadSafetyState();
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
            EnsureLoaded();
            return !writeBlocked &&
                   node != null &&
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
            if (writeBlocked ||
                node == null ||
                !PermanentGrowthCatalog.TryGetNode(node.Id, out var catalogNode) ||
                !CanPurchaseNode(catalogNode))
                return false;

            MutationSnapshot snapshot = CaptureMutationSnapshot();
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

            if (!Save(snapshot))
                return false;
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
            if (writeBlocked)
                return false;
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            if (node == null || !node.IsKeystone || !IsNodeUnlocked(node))
                return false;
            if (IsKeystoneActive(node.Id))
                return true;

            MutationSnapshot snapshot = CaptureMutationSnapshot();
            SetActiveKeystoneId(node.Branch, node.Id);
            if (!Save(snapshot))
                return false;
            Changed?.Invoke();
            return true;
        }

        public static bool ClearActiveKeystone(PermanentGrowthBranch branch)
        {
            EnsureLoaded();
            if (writeBlocked)
                return false;
            if (string.IsNullOrEmpty(GetActiveKeystoneId(branch)))
                return false;
            MutationSnapshot snapshot = CaptureMutationSnapshot();
            SetActiveKeystoneId(branch, string.Empty);
            if (!Save(snapshot))
                return false;
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
            if (writeBlocked)
                return new PermanentGrowthSettlement(0, data.wallet, false);
            if (string.IsNullOrEmpty(runId))
                return new PermanentGrowthSettlement(0, data.wallet, false);
            if (!eligible)
                return new PermanentGrowthSettlement(0, data.wallet, true);
            if (HasSettledRunId(runId))
                return new PermanentGrowthSettlement(0, data.wallet, false);

            MutationSnapshot snapshot = CaptureMutationSnapshot();
            data.lastSettledRunId = runId;
            data.settledRunIds.Add(runId);
            TrimSettledRunHistory();
            int earned = 0;
            if (!data.rewardMilestoneWatermarkInitialized)
            {
                data.rewardedBestHeight = Mathf.Max(0, previousBest);
                data.rewardMilestoneWatermarkInitialized = true;
            }
            // 표시 기록은 성장 저장보다 먼저 확정될 수 없지만, 오래된 backup을
            // 복원한 예외 상황에서는 더 앞선 기록이 남을 수 있다. 두 기준의
            // 최댓값을 권리 watermark로 사용해 같은 이정표를 다시 지급하지 않는다.
            int rewardedBestBeforeRun = Mathf.Max(
                data.rewardedBestHeight,
                Mathf.Max(0, previousBest));
            data.rewardedBestHeight = rewardedBestBeforeRun;
            earned = RunRewardCalculator.Calculate(
                swarmProgressHeight,
                scoreHeight,
                rewardedBestBeforeRun,
                activeGameplaySeconds,
                !data.tutorialRewardClaimed);
            data.tutorialRewardClaimed = true;
            data.rewardedBestHeight = Mathf.Max(
                data.rewardedBestHeight,
                Mathf.Max(0, scoreHeight));
            int remainingBudget = Mathf.Max(
                0,
                PermanentGrowthCatalog.TotalCost -
                data.ownedNodeIds.Count -
                data.wallet);
            earned = Mathf.Min(earned, remainingBudget);
            data.wallet += earned;

            if (!Save(snapshot))
                return new PermanentGrowthSettlement(0, data.wallet, false);
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

        static bool TryCompleteInterruptedReset(ref string primaryJson)
        {
            if (store is not IPermanentGrowthRecoveryStore recoveryStore)
                return true;

            bool resetPending;
            try
            {
                resetPending = recoveryStore.LoadResetPending();
            }
            catch (Exception exception)
            {
                TryLoadSupportedBackup(out SaveData fallbackData);
                EnterReadOnlyRecovery(
                    primaryJson,
                    PermanentGrowthLoadState.PersistenceFailureReadOnly,
                    fallbackData);
                Debug.LogWarning(
                    $"성장 초기화 완료 상태를 읽지 못했습니다: " +
                    exception.Message);
                return false;
            }
            if (!resetPending)
                return true;

            // reset 중 사용자가 backup 복원을 선택한 뒤 marker clear만 실패한
            // 경우에는 pending target이 복원 의도를 소유한다. 빈 초기화를 다시
            // 확정하기 전에 이 목표 세대를 우선 복원한다.
            if (TryLoadSupportedPendingTarget(
                    out SaveData restoreData,
                    out string restoreJson,
                    out bool restoreIntentReadFailed))
            {
                PrimaryWriteResult restoreWrite = TryWritePrimary(
                    restoreJson,
                    primaryJson,
                    out Exception restoreError,
                    out string unexpectedRestoreJson);
                if (restoreWrite != PrimaryWriteResult.Applied)
                {
                    EnterReadOnlyRecovery(
                        !string.IsNullOrWhiteSpace(unexpectedRestoreJson)
                            ? unexpectedRestoreJson
                            : primaryJson,
                        PermanentGrowthLoadState.PersistenceFailureReadOnly,
                        restoreData);
                    validatedRecoveryJson = restoreJson;
                    backupAvailable = true;
                    Debug.LogWarning(
                        $"중단된 성장 복원을 완료하지 못했습니다: " +
                        restoreError?.Message);
                    return false;
                }

                try
                {
                    SaveBackupPreservingRejected(recoveryStore, restoreJson);
                    // reset marker를 먼저 내려 이후 중단 시에도 빈 초기화가
                    // 복원 완료 primary를 다시 덮지 않게 한다.
                    recoveryStore.SaveResetPending(false);
                    recoveryStore.SaveBackupSyncPending(false);
                    recoveryStore.SaveBackupSyncTarget(string.Empty);
                }
                catch (Exception exception)
                {
                    EnterReadOnlyRecovery(
                        restoreJson,
                        PermanentGrowthLoadState.PersistenceFailureReadOnly,
                        restoreData);
                    validatedRecoveryJson = restoreJson;
                    backupAvailable = true;
                    Debug.LogWarning(
                        $"성장 복원 marker 정리를 다음 실행으로 미룹니다: " +
                        exception.Message);
                    return false;
                }
                primaryGenerationJson = restoreJson;
                primaryGenerationKnown = true;
                primaryJson = restoreJson;
                return true;
            }
            if (restoreIntentReadFailed)
            {
                TryLoadSupportedBackup(out SaveData fallbackData);
                preferPhysicalBackupForRecovery = fallbackData != null;
                EnterReadOnlyRecovery(
                    primaryJson,
                    PermanentGrowthLoadState.PersistenceFailureReadOnly,
                    fallbackData);
                return false;
            }

            if (!TryLoadSupportedBackup(out SaveData backupData))
            {
                EnterReadOnlyRecovery(
                    primaryJson,
                    PermanentGrowthLoadState.PersistenceFailureReadOnly,
                    null);
                return false;
            }

            string canonicalJson = JsonUtility.ToJson(new SaveData());
            PrimaryWriteResult resetWrite = TryWritePrimary(
                canonicalJson,
                primaryJson,
                out Exception primaryError,
                out string unexpectedPrimaryJson);
            if (resetWrite != PrimaryWriteResult.Applied)
            {
                EnterReadOnlyRecovery(
                    !string.IsNullOrWhiteSpace(unexpectedPrimaryJson)
                        ? unexpectedPrimaryJson
                        : primaryJson,
                    PermanentGrowthLoadState.PersistenceFailureReadOnly,
                    backupData);
                Debug.LogWarning(
                    $"중단된 성장 초기화를 완료하지 못했습니다: " +
                    primaryError?.Message);
                return false;
            }

            try
            {
                // reset은 검증 backup을 보존하는 사용자 선택이다. 일반 동기화 부채를
                // 없애고 reset marker를 마지막에 내려 중간 종료도 멱등하게 마무리한다.
                recoveryStore.SaveBackupSyncPending(false);
                recoveryStore.SaveBackupSyncTarget(string.Empty);
                recoveryStore.SaveResetPending(false);
            }
            catch (Exception exception)
            {
                EnterMarkerCleanupRecovery(new SaveData(), backupData != null);
                Debug.LogWarning(
                    $"성장 초기화 marker 정리를 다음 실행으로 미룹니다: " +
                    exception.Message);
                return false;
            }
            primaryGenerationJson = canonicalJson;
            primaryGenerationKnown = true;
            primaryJson = canonicalJson;
            return true;
        }

        static void EnterMarkerCleanupRecovery(
            SaveData safeData,
            bool canRestoreBackup)
        {
            data = safeData ?? new SaveData();
            writeBlocked = true;
            loadState = PermanentGrowthLoadState.PersistenceFailureReadOnly;
            backupAvailable = canRestoreBackup;
            backupReadFailed = false;
            primaryReadFailed = false;
            validatedRecoveryJson = string.Empty;
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;
            ResetLoadSafetyState();
            string json;
            try
            {
                json = store.Load();
                primaryGenerationJson = json ?? string.Empty;
                primaryGenerationKnown = true;
            }
            catch (Exception exception)
            {
                loaded = true;
                primaryReadFailed = true;
                bool hasPendingTarget = TryLoadSupportedPendingTarget(
                    out SaveData pendingData,
                    out string pendingJson,
                    out _);
                if (!hasPendingTarget)
                    TryLoadSupportedBackup(out pendingData);
                EnterReadOnlyRecovery(
                    string.Empty,
                    PermanentGrowthLoadState.PersistenceFailureReadOnly,
                    pendingData);
                if (hasPendingTarget)
                {
                    validatedRecoveryJson = pendingJson;
                    backupAvailable = true;
                }
                Debug.LogWarning(
                    $"영구 성장 primary를 읽지 못해 복구 상태로 전환했습니다: " +
                    exception.Message);
                return;
            }
            loaded = true;
            if (!TryCompleteInterruptedReset(ref json))
                return;
            if (string.IsNullOrEmpty(json))
            {
                if (TryLoadSupportedPendingTarget(
                        out SaveData pendingData,
                        out string pendingJson,
                        out bool pendingReadFailed))
                {
                    EnterReadOnlyRecovery(
                        json,
                        PermanentGrowthLoadState.MissingPrimaryReadOnly,
                        pendingData);
                    validatedRecoveryJson = pendingJson;
                    backupAvailable = true;
                    return;
                }
                if (pendingReadFailed)
                {
                    SaveData fallbackData = null;
                    if (pendingTargetInvalid)
                    {
                        TryLoadSupportedBackup(out fallbackData);
                        preferPhysicalBackupForRecovery = fallbackData != null;
                    }
                    EnterReadOnlyRecovery(
                        json,
                        PermanentGrowthLoadState.PersistenceFailureReadOnly,
                        fallbackData);
                    return;
                }
                if (TryLoadSupportedBackup(out SaveData backupData))
                {
                    EnterReadOnlyRecovery(
                        json,
                        PermanentGrowthLoadState.MissingPrimaryReadOnly,
                        backupData);
                    return;
                }
                if (backupReadFailed ||
                    !string.IsNullOrEmpty(rejectedBackupJson))
                {
                    EnterReadOnlyRecovery(
                        json,
                        PermanentGrowthLoadState.MissingPrimaryReadOnly,
                        null);
                    return;
                }
                data = new SaveData();
                return;
            }

            if (!TryReadSupportedSave(
                    json,
                    out SaveData loadedData,
                    out PermanentGrowthLoadState failureState))
            {
                if (TryLoadSupportedPendingTarget(
                        out SaveData pendingData,
                        out string pendingJson,
                        out _))
                {
                    EnterReadOnlyRecovery(json, failureState, pendingData);
                    validatedRecoveryJson = pendingJson;
                    backupAvailable = true;
                    return;
                }
                TryLoadSupportedBackup(out SaveData backupData);
                EnterReadOnlyRecovery(json, failureState, backupData);
                return;
            }

            data = loadedData;
            MutationSnapshot snapshot = CaptureMutationSnapshot();
            bool changed = PrepareSupportedData();
            if (changed)
            {
                if (!Save(snapshot, json))
                {
                    PrepareSupportedData();
                    writeBlocked = true;
                    loadState =
                        PermanentGrowthLoadState.PersistenceFailureReadOnly;
                }
            }
            else
                SeedBackupIfNeeded();
        }

        static bool PrepareSupportedData()
        {
            data.ranks ??= new List<RankRecord>();
            data.ownedNodeIds ??= new List<string>();
            string before = JsonUtility.ToJson(data);
            // 단계별로 올려야 v2의 ownedNodeIds를 구 ranks로 오인해 지우지 않는다.
            if (data.balanceVersion < 2)
                MigrateLegacyBalance();
            if (data.balanceVersion < 3)
                MigrateLeapTreeToV3();
            if (data.balanceVersion < 4)
                MigrateLeapTreeToV4();
            if (data.balanceVersion < 5)
                MigrateMilestoneWatermarkToV5();
            NormalizeLoadedData();
            return !string.Equals(
                before,
                JsonUtility.ToJson(data),
                StringComparison.Ordinal);
        }

        static bool TryReadSupportedSave(
            string json,
            out SaveData parsed,
            out PermanentGrowthLoadState failureState)
        {
            parsed = null;
            failureState = PermanentGrowthLoadState.CorruptReadOnly;
            if (string.IsNullOrEmpty(json))
            {
                failureState = PermanentGrowthLoadState.MissingPrimaryReadOnly;
                return false;
            }

            try
            {
                SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);
                if (header == null || header.schemaVersion == 0)
                    return false;
                if (header.schemaVersion != SchemaVersion)
                {
                    failureState =
                        PermanentGrowthLoadState.UnsupportedSchemaReadOnly;
                    return false;
                }
                if (header.balanceVersion < 1)
                    return false;
                if (header.balanceVersion > BalanceVersion)
                {
                    failureState = PermanentGrowthLoadState.FutureBalanceReadOnly;
                    return false;
                }
                if (!HasRequiredPayload(json, header.balanceVersion))
                    return false;

                parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null ||
                    parsed.ranks == null ||
                    parsed.lastSettledRunId == null)
                    return false;
                if (header.balanceVersion >= 2 &&
                    (parsed.ownedNodeIds == null ||
                     parsed.survivalKeystoneId == null ||
                     parsed.leapKeystoneId == null ||
                     parsed.inkHandlingKeystoneId == null))
                    return false;
                if (header.balanceVersion >= 2 &&
                    parsed.settledRunIds == null)
                    return false;
                if (header.balanceVersion == BalanceVersion &&
                    !HasValidOwnedGraph(parsed))
                    return false;
                return true;
            }
            catch (Exception)
            {
                parsed = null;
                return false;
            }
        }

        static bool TryLoadSupportedBackup(out SaveData backupData)
        {
            backupData = null;
            backupAvailable = false;
            backupReadFailed = false;
            rejectedBackupJson = string.Empty;
            if (store is not IPermanentGrowthRecoveryStore recoveryStore)
                return false;

            string backupJson;
            try
            {
                backupJson = recoveryStore.LoadBackup();
            }
            catch (Exception exception)
            {
                backupReadFailed = true;
                Debug.LogWarning(
                    $"영구 성장 backup을 읽지 못해 primary 복구 후보만 유지합니다: " +
                    exception.Message);
                return false;
            }
            if (!TryReadSupportedSave(backupJson, out backupData, out _))
            {
                rejectedBackupJson = backupJson ?? string.Empty;
                return false;
            }
            backupAvailable = true;
            return true;
        }

        static bool HasRequiredPayload(string json, int balanceVersion)
        {
            for (int i = 0; i < CommonRequiredPayloadFields.Length; i++)
                if (!HasTopLevelField(json, CommonRequiredPayloadFields[i]))
                    return false;

            if (balanceVersion >= 2)
            {
                for (int i = 0; i < NodeSaveRequiredPayloadFields.Length; i++)
                    if (!HasTopLevelField(
                            json,
                            NodeSaveRequiredPayloadFields[i]))
                        return false;
            }

            if (balanceVersion >= 5)
            {
                for (int i = 0;
                     i < MilestoneRequiredPayloadFields.Length;
                     i++)
                    if (!HasTopLevelField(
                            json,
                            MilestoneRequiredPayloadFields[i]))
                        return false;
            }

            return balanceVersion < 2 ||
                   HasTopLevelField(json, "settledRunIds");
        }

        static bool HasValidOwnedGraph(SaveData candidate)
        {
            if (candidate?.ownedNodeIds == null)
                return false;
            var owned = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidate.ownedNodeIds.Count; i++)
            {
                string nodeId = candidate.ownedNodeIds[i];
                if (!PermanentGrowthCatalog.TryGetNode(nodeId, out _) ||
                    !owned.Add(nodeId))
                    return false;
            }

            for (int i = 0; i < candidate.ownedNodeIds.Count; i++)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(candidate.ownedNodeIds[i]);
                for (int parentIndex = 0;
                     parentIndex < node.ParentIds.Count;
                     parentIndex++)
                    if (!owned.Contains(node.ParentIds[parentIndex]))
                        return false;
                if (CountGeneralNodes(owned, node.Branch) <
                    node.RequiredOwnedCountInBranch)
                    return false;
            }

            return IsEquippedNodeValid(
                       candidate.survivalKeystoneId,
                       PermanentGrowthBranch.Survival,
                       owned) &&
                   IsEquippedNodeValid(
                       candidate.leapKeystoneId,
                       PermanentGrowthBranch.Leap,
                       owned) &&
                   IsEquippedNodeValid(
                       candidate.inkHandlingKeystoneId,
                       PermanentGrowthBranch.InkHandling,
                       owned);
        }

        static int CountGeneralNodes(
            HashSet<string> owned,
            PermanentGrowthBranch branch)
        {
            int count = 0;
            foreach (string nodeId in owned)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(nodeId);
                if (node != null && node.Branch == branch && !node.IsKeystone)
                    count++;
            }
            return count;
        }

        static bool IsEquippedNodeValid(
            string nodeId,
            PermanentGrowthBranch branch,
            HashSet<string> owned)
        {
            if (string.IsNullOrEmpty(nodeId))
                return true;
            PermanentGrowthNodeDefinition node =
                PermanentGrowthCatalog.GetNode(nodeId);
            return node != null && node.IsKeystone && node.Branch == branch &&
                   owned.Contains(nodeId);
        }

        static bool HasTopLevelField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
                return false;

            int depth = 0;
            int stringStart = -1;
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char character = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (character == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (character != '"')
                        continue;

                    inString = false;
                    if (depth != 1 || stringStart < 0)
                        continue;
                    int length = i - stringStart;
                    int next = i + 1;
                    while (next < json.Length && char.IsWhiteSpace(json[next]))
                        next++;
                    if (next < json.Length &&
                        json[next] == ':' &&
                        length == fieldName.Length &&
                        string.CompareOrdinal(
                            json,
                            stringStart,
                            fieldName,
                            0,
                            length) == 0)
                        return true;
                    continue;
                }

                switch (character)
                {
                    case '{':
                    case '[':
                        depth++;
                        break;
                    case '}':
                    case ']':
                        depth--;
                        break;
                    case '"':
                        inString = true;
                        stringStart = i + 1;
                        break;
                }
            }
            return false;
        }

        static void EnterReadOnlyRecovery(
            string rejectedJson,
            PermanentGrowthLoadState failureState,
            SaveData backupData)
        {
            rejectedPrimaryJson = rejectedJson ?? string.Empty;
            loadState = failureState;
            writeBlocked = true;
            data = backupData ?? new SaveData();
            if (backupData != null)
                PrepareSupportedData();

            Debug.LogWarning(
                $"영구 성장 저장을 안전하게 보존했습니다. 상태: {loadState}");
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

        /// v5부터 최초 고도 이정표의 지급 권리를 성장 저장이 직접 소유한다.
        /// 구 저장은 다음 정상 정산에서 당시 최고기록으로 watermark를 초기화한다.
        static void MigrateMilestoneWatermarkToV5()
        {
            data.rewardedBestHeight = Mathf.Max(0, data.rewardedBestHeight);
            data.balanceVersion = 5;
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
            var requested = new HashSet<string>(
                data.ownedNodeIds,
                StringComparer.Ordinal);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new List<string>(PermanentGrowthCatalog.TotalCost);
            bool added;
            do
            {
                added = false;
                for (int i = 0; i < PermanentGrowthCatalog.Nodes.Count; i++)
                {
                    PermanentGrowthNodeDefinition node =
                        PermanentGrowthCatalog.Nodes[i];
                    if (!requested.Contains(node.Id) || accepted.Contains(node.Id))
                        continue;

                    bool parentsOwned = true;
                    for (int parentIndex = 0;
                         parentIndex < node.ParentIds.Count;
                         parentIndex++)
                    {
                        if (accepted.Contains(node.ParentIds[parentIndex]))
                            continue;
                        parentsOwned = false;
                        break;
                    }
                    if (!parentsOwned ||
                        CountAcceptedGeneralNodes(accepted, node.Branch) <
                        node.RequiredOwnedCountInBranch)
                        continue;

                    accepted.Add(node.Id);
                    normalized.Add(node.Id);
                    added = true;
                }
            } while (added);
            data.ownedNodeIds = normalized;
            data.spent = data.ownedNodeIds.Count;
            data.wallet = Mathf.Clamp(
                data.wallet,
                0,
                PermanentGrowthCatalog.TotalCost - data.spent);
            data.rewardedBestHeight = Mathf.Max(0, data.rewardedBestHeight);
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
        }

        static int CountAcceptedGeneralNodes(
            HashSet<string> accepted,
            PermanentGrowthBranch branch)
        {
            int count = 0;
            foreach (string nodeId in accepted)
            {
                PermanentGrowthNodeDefinition node =
                    PermanentGrowthCatalog.GetNode(nodeId);
                if (node != null && node.Branch == branch && !node.IsKeystone)
                    count++;
            }
            return count;
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

        static MutationSnapshot CaptureMutationSnapshot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int debugCurrency = debugCurrencyOverride;
#else
            int debugCurrency = -1;
#endif
            return new MutationSnapshot(
                JsonUtility.ToJson(data),
                debugCurrency);
        }

        static void RestoreMutationSnapshot(MutationSnapshot snapshot)
        {
            data = JsonUtility.FromJson<SaveData>(snapshot.Json) ??
                   new SaveData();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugCurrencyOverride = snapshot.DebugCurrency;
#endif
        }

        static bool Save(
            MutationSnapshot rollbackSnapshot,
            string recoveryRawJson = null)
        {
            if (data == null || writeBlocked)
                return false;

            string json = JsonUtility.ToJson(data);
            string previousPhysicalJson = recoveryRawJson ??
                (primaryGenerationKnown
                    ? primaryGenerationJson
                    : rollbackSnapshot.Json);
            var recoveryStore = store as IPermanentGrowthRecoveryStore;
            bool resetMarkerWasPending = false;
            bool resetMarkerStateUnknown = recoveryStore != null;
            try
            {
                // 이전 reset marker가 남아 있다면 새 진행을 쓰기 전에 반드시 취소한다.
                if (recoveryStore != null)
                {
                    resetMarkerWasPending = recoveryStore.LoadResetPending();
                    resetMarkerStateUnknown = false;
                    recoveryStore.SaveResetPending(false);
                }
                if (recoveryStore != null &&
                    !ResolveExistingBackupSync(
                        recoveryStore,
                        previousPhysicalJson))
                    throw new InvalidOperationException(
                        "기존 backup 동기화 상태가 현재 primary와 일치하지 않습니다.");
                // 목표 JSON을 먼저 남기고 pending을 올려 중간 종료 뒤 primary 반영
                // 여부를 판별할 수 있게 한다.
                recoveryStore?.SaveBackupSyncTarget(json);
                recoveryStore?.SaveBackupSyncPending(true);
            }
            catch (Exception exception)
            {
                RestoreMutationSnapshot(rollbackSnapshot);
                EnterPersistenceFailureRecovery(
                    recoveryRawJson ?? rollbackSnapshot.Json,
                    preferPhysicalBackup:
                        resetMarkerWasPending || resetMarkerStateUnknown);
                Debug.LogWarning(
                    $"영구 성장 저장 준비에 실패해 변경을 되돌렸습니다: " +
                    exception.Message);
                return false;
            }

            PrimaryWriteResult primaryWrite = TryWritePrimary(
                json,
                previousPhysicalJson,
                out Exception primaryError,
                out string unexpectedPrimaryJson);
            if (primaryWrite != PrimaryWriteResult.Applied)
            {
                RestoreMutationSnapshot(rollbackSnapshot);
                if (primaryWrite == PrimaryWriteResult.Unknown)
                    primaryReadFailed = unexpectedPrimaryJson == null;
                if (primaryWrite == PrimaryWriteResult.DefinitelyNotApplied &&
                    recoveryStore != null)
                {
                    try
                    {
                        recoveryStore.SaveBackupSyncPending(false);
                        recoveryStore.SaveBackupSyncTarget(string.Empty);
                    }
                    catch (Exception)
                    {
                        // 아래 복구 상태에서 다시 시도한다.
                    }
                }
                EnterPersistenceFailureRecovery(
                    previousPhysicalJson,
                    unexpectedPrimaryJson);
                Debug.LogWarning(
                    $"영구 성장 primary 저장에 실패해 변경을 되돌렸습니다: " +
                    primaryError?.Message);
                return false;
            }
            if (primaryError != null)
            {
                Debug.LogWarning(
                    $"primary 저장은 반영됐지만 저장 API가 오류를 반환했습니다: " +
                    primaryError.Message);
            }
            primaryGenerationJson = json;
            primaryGenerationKnown = true;

            if (recoveryStore == null)
                return true;

            try
            {
                SaveBackupPreservingRejected(recoveryStore, json);
                recoveryStore.SaveBackupSyncPending(false);
                recoveryStore.SaveBackupSyncTarget(string.Empty);
            }
            catch (Exception exception)
            {
                // readback으로만 반영을 추정한 primary는 backup flush까지 실패하면
                // 디스크 내구성을 증명할 수 없다. 목표 세대를 복구 후보로 잠그고
                // 성공 결과를 먼저 노출하지 않는다.
                if (primaryError != null)
                {
                    EnterReadOnlyRecovery(
                        previousPhysicalJson,
                        PermanentGrowthLoadState.PersistenceFailureReadOnly,
                        data);
                    validatedRecoveryJson = json;
                    backupAvailable = true;
                    Debug.LogWarning(
                        $"영구 성장 저장 내구성을 확인하지 못해 복구 상태로 전환했습니다: " +
                        exception.Message);
                    return false;
                }
                // 명확히 성공한 primary는 유지한다. pending은 다음 로드에서 같은
                // primary로 backup을 다시 동기화하는 안전 표식으로 남긴다.
                Debug.LogWarning(
                    $"영구 성장 backup 동기화를 다음 실행으로 미룹니다: " +
                    exception.Message);
            }
            return true;
        }

        static bool ResolveExistingBackupSync(
            IPermanentGrowthRecoveryStore recoveryStore,
            string currentPrimaryJson)
        {
            if (!recoveryStore.LoadBackupSyncPending())
                return true;

            string targetJson = recoveryStore.LoadBackupSyncTarget();
            if (!TryReadSupportedSave(targetJson, out _, out _) ||
                !string.Equals(
                    targetJson,
                    currentPrimaryJson,
                    StringComparison.Ordinal))
            {
                // 비어 있거나 손상·미래 버전인 목표도 자동 폐기하지 않는다.
                // 목표 세대의 반영 여부가 불명인 채 다음 변경으로 덮지 않는다.
                // 호출자가 read-only 복구로 전환해 두 세대 중 하나를 명시 복원한다.
                return false;
            }

            SaveBackupPreservingRejected(recoveryStore, currentPrimaryJson);
            recoveryStore.SaveBackupSyncPending(false);
            recoveryStore.SaveBackupSyncTarget(string.Empty);
            return true;
        }

        static PrimaryWriteResult TryWritePrimary(
            string json,
            string previousJson,
            out Exception error,
            out string unexpectedJson)
        {
            error = null;
            // null은 readback 자체가 실패했다는 뜻이고, 빈 문자열은 실제로 빈
            // primary를 관측했다는 뜻이다. 복구 전 재조회 필요 여부를 구분한다.
            unexpectedJson = null;
            try
            {
                store.Save(json);
                return PrimaryWriteResult.Applied;
            }
            catch (Exception exception)
            {
                error = exception;
                try
                {
                    // SetString 반영 뒤 디스크 flush에서 예외가 난 경우에는 현재 값이
                    // 이미 새 세대다. 메모리를 되돌리지 않고 backup 동기화를 마친다.
                    string currentJson = store.Load();
                    if (string.Equals(
                            currentJson,
                            json,
                            StringComparison.Ordinal))
                        return PrimaryWriteResult.Applied;
                    if (previousJson != null &&
                        string.Equals(
                            currentJson,
                            previousJson,
                            StringComparison.Ordinal))
                        return PrimaryWriteResult.DefinitelyNotApplied;
                    unexpectedJson = currentJson ?? string.Empty;
                    return PrimaryWriteResult.Unknown;
                }
                catch (Exception)
                {
                    return PrimaryWriteResult.Unknown;
                }
            }
        }

        static void EnterPersistenceFailureRecovery(
            string recoveryJson,
            string unexpectedPrimaryJson = null,
            bool preferPhysicalBackup = false)
        {
            writeBlocked = true;
            loadState = PermanentGrowthLoadState.PersistenceFailureReadOnly;
            preferPhysicalBackupForRecovery |= preferPhysicalBackup;
            rejectedPrimaryJson = !string.IsNullOrWhiteSpace(unexpectedPrimaryJson)
                ? unexpectedPrimaryJson
                : recoveryJson ?? string.Empty;
            validatedRecoveryJson = string.Empty;
            bool physicalBackupLoaded = false;
            if (TryLoadSupportedPendingTarget(
                    out _,
                    out string pendingJson,
                    out _))
            {
                validatedRecoveryJson = pendingJson;
                backupAvailable = true;
                preferPhysicalBackupForRecovery = false;
            }
            else
                physicalBackupLoaded = TryLoadSupportedBackup(out _);
            if (TryReadSupportedSave(recoveryJson, out _, out _))
            {
                if (string.IsNullOrEmpty(validatedRecoveryJson) &&
                    (!preferPhysicalBackupForRecovery || !backupAvailable))
                    validatedRecoveryJson = recoveryJson;
                backupAvailable = true;
                if (preferPhysicalBackupForRecovery &&
                    !physicalBackupLoaded &&
                    !backupReadFailed)
                    preferPhysicalBackupForRecovery = false;
            }
        }

        static bool TryLoadSupportedPendingTarget(
            out SaveData pendingData,
            out string pendingJson,
            out bool readFailed)
        {
            pendingData = null;
            pendingJson = string.Empty;
            readFailed = false;
            pendingTargetReadFailed = false;
            pendingTargetInvalid = false;
            if (store is not IPermanentGrowthRecoveryStore recoveryStore)
                return false;
            try
            {
                if (!recoveryStore.LoadBackupSyncPending())
                    return false;
                pendingJson = recoveryStore.LoadBackupSyncTarget();
                if (TryReadSupportedSave(pendingJson, out pendingData, out _))
                    return true;

                // pending marker가 있는데 목표가 비었거나 손상·미래 버전이면
                // '의도 없음'이 아니라 검증할 수 없는 복원 의도다. 원문을 유지하고
                // 자동 초기화·동기화를 모두 멈춘다.
                readFailed = true;
                pendingTargetInvalid = true;
                rejectedPendingTargetJson = pendingJson ?? string.Empty;
                Debug.LogWarning(
                    "성장 저장의 미완료 동기화 목표가 유효하지 않아 안전 복구를 기다립니다.");
                pendingData = null;
                return false;
            }
            catch (Exception exception)
            {
                readFailed = true;
                pendingTargetReadFailed = true;
                Debug.LogWarning(
                    $"성장 저장의 미완료 동기화 목표를 읽지 못했습니다: " +
                    exception.Message);
                pendingData = null;
                pendingJson = string.Empty;
                return false;
            }
        }

        static bool RefreshPendingRecoveryTarget()
        {
            if (!pendingTargetReadFailed)
                return backupAvailable;
            if (!TryLoadSupportedPendingTarget(
                    out _,
                    out string pendingJson,
                    out bool readFailed))
            {
                if (pendingTargetReadFailed)
                    return false;
                // I/O는 회복됐지만 marker target이 손상된 경우에는 자동 처리를
                // 계속 막되, 사용자의 명시적 backup 복원/초기화는 허용한다.
                if (pendingTargetInvalid)
                {
                    if (!backupAvailable)
                        TryLoadSupportedBackup(out _);
                    return true;
                }
                pendingTargetReadFailed = readFailed;
                if (!backupAvailable)
                    TryLoadSupportedBackup(out _);
                return true;
            }
            pendingTargetReadFailed = false;
            validatedRecoveryJson = pendingJson;
            backupAvailable = true;
            return true;
        }

        static void SeedBackupIfNeeded()
        {
            if (data == null ||
                store is not IPermanentGrowthRecoveryStore recoveryStore)
                return;

            try
            {
                bool syncPending = recoveryStore.LoadBackupSyncPending();
                string currentBackup = recoveryStore.LoadBackup();
                string canonicalPrimary = JsonUtility.ToJson(data);
                if (syncPending)
                {
                    string targetJson = recoveryStore.LoadBackupSyncTarget();
                    if (!TryReadSupportedSave(
                            targetJson,
                            out SaveData pendingData,
                            out PermanentGrowthLoadState pendingFailure))
                    {
                        // 다운그레이드의 future target과 손상 target을 같은 이유로
                        // 자동 삭제하지 않는다. marker/target 원문을 그대로 둔다.
                        pendingTargetInvalid = true;
                        rejectedPendingTargetJson = targetJson ?? string.Empty;
                        TryLoadSupportedBackup(out SaveData fallbackData);
                        EnterReadOnlyRecovery(
                            primaryGenerationKnown
                                ? primaryGenerationJson
                                : canonicalPrimary,
                            pendingFailure == PermanentGrowthLoadState.Ready
                                ? PermanentGrowthLoadState.PersistenceFailureReadOnly
                                : pendingFailure,
                            fallbackData);
                        preferPhysicalBackupForRecovery = fallbackData != null;
                        return;
                    }
                    if (!string.Equals(
                            targetJson,
                            canonicalPrimary,
                            StringComparison.Ordinal))
                    {
                        // primary와 목표 세대가 갈라졌다면 목표를 폐기하지 않고
                        // 사용자가 복원할 수 있는 read-only 상태로 보존한다.
                        EnterReadOnlyRecovery(
                            primaryGenerationKnown
                                ? primaryGenerationJson
                                : canonicalPrimary,
                            PermanentGrowthLoadState.PersistenceFailureReadOnly,
                            pendingData);
                        validatedRecoveryJson = targetJson;
                        backupAvailable = true;
                        return;
                    }
                }
                if (!syncPending &&
                    TryReadSupportedSave(currentBackup, out _, out _))
                    return;
                SaveBackupPreservingRejected(
                    recoveryStore,
                    canonicalPrimary);
                recoveryStore.SaveBackupSyncPending(false);
                recoveryStore.SaveBackupSyncTarget(string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"영구 성장 backup 초기화를 다음 실행으로 미룹니다: " +
                    exception.Message);
            }
        }

        static void SaveBackupPreservingRejected(
            IPermanentGrowthRecoveryStore recoveryStore,
            string replacementJson)
        {
            string currentBackup = recoveryStore.LoadBackup();
            if (!string.IsNullOrWhiteSpace(currentBackup) &&
                !TryReadSupportedSave(currentBackup, out _, out _))
                recoveryStore.SaveBackupQuarantine(currentBackup);
            recoveryStore.SaveBackup(replacementJson);
        }

        static void QuarantineRejectedPrimary(
            IPermanentGrowthRecoveryStore recoveryStore)
        {
            if (!string.IsNullOrWhiteSpace(rejectedPrimaryJson))
                recoveryStore.SaveQuarantine(rejectedPrimaryJson);
        }

        static bool RefreshRejectedPrimaryAfterReadFailure()
        {
            if (!primaryReadFailed)
                return true;
            try
            {
                string primaryJson = store.Load();
                if (!string.IsNullOrWhiteSpace(primaryJson))
                {
                    rejectedPrimaryJson = primaryJson;
                    if (TryReadSupportedSave(primaryJson, out _, out _))
                    {
                        validatedRecoveryJson = primaryJson;
                        backupAvailable = true;
                    }
                }
                primaryReadFailed = false;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"기존 성장 primary를 보존하기 위해 다시 읽지 못했습니다: " +
                    exception.Message);
                return false;
            }
        }

        public static bool TryRestoreBackup()
        {
            EnsureLoaded();
            if (!writeBlocked ||
                store is not IPermanentGrowthRecoveryStore recoveryStore)
                return false;
            if (!RefreshRejectedPrimaryAfterReadFailure())
                return false;
            if (pendingTargetReadFailed &&
                !RefreshPendingRecoveryTarget() &&
                !backupAvailable)
                return false;
            if (!backupAvailable && backupReadFailed)
                TryLoadSupportedBackup(out _);
            if (!backupAvailable && string.IsNullOrEmpty(validatedRecoveryJson))
                return false;

            string backupJson = preferPhysicalBackupForRecovery
                ? string.Empty
                : validatedRecoveryJson;
            if (string.IsNullOrEmpty(backupJson))
            {
                try
                {
                    backupJson = recoveryStore.LoadBackup();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"복원할 성장 backup을 읽지 못했습니다: " +
                        exception.Message);
                    return false;
                }
            }
            if (!TryReadSupportedSave(backupJson, out SaveData backupData, out _))
            {
                if (!preferPhysicalBackupForRecovery ||
                    string.IsNullOrEmpty(validatedRecoveryJson) ||
                    !TryReadSupportedSave(
                        validatedRecoveryJson,
                        out backupData,
                        out _))
                    return false;
                backupJson = validatedRecoveryJson;
            }

            data = backupData;
            PrepareSupportedData();
            string canonicalJson = JsonUtility.ToJson(data);
            try
            {
                // 실패한 reset 뒤 사용자가 복원을 선택하면 이전 reset 의도를 먼저
                // 취소해야 한다. 단, 복원 목표를 먼저 남겨 marker clear 실패 뒤
                // 다음 로드가 빈 초기화보다 복원 의도를 우선하도록 한다.
                QuarantineRejectedPrimary(recoveryStore);
                if (pendingTargetInvalid &&
                    !string.IsNullOrWhiteSpace(rejectedPendingTargetJson))
                    recoveryStore.SaveBackupQuarantine(
                        rejectedPendingTargetJson);
                recoveryStore.SaveBackupSyncTarget(canonicalJson);
                recoveryStore.SaveBackupSyncPending(true);
                recoveryStore.SaveResetPending(false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"영구 성장 백업 복원을 준비하지 못했습니다: " +
                    exception.Message);
                return false;
            }

            PrimaryWriteResult primaryWrite = TryWritePrimary(
                canonicalJson,
                rejectedPrimaryJson,
                out Exception primaryError,
                out string unexpectedPrimaryJson);
            if (primaryWrite != PrimaryWriteResult.Applied)
            {
                if (!string.IsNullOrWhiteSpace(unexpectedPrimaryJson))
                    rejectedPrimaryJson = unexpectedPrimaryJson;
                Debug.LogWarning(
                    $"영구 성장 백업 복원 primary 저장에 실패했습니다: " +
                    primaryError?.Message);
                return false;
            }
            primaryGenerationJson = canonicalJson;
            primaryGenerationKnown = true;

            try
            {
                SaveBackupPreservingRejected(recoveryStore, canonicalJson);
                recoveryStore.SaveBackupSyncPending(false);
                recoveryStore.SaveBackupSyncTarget(string.Empty);
            }
            catch (Exception exception)
            {
                // primary 복원은 확정됐다. pending을 남겨 다음 로드에서 backup만
                // 재동기화하고 사용자 진행은 즉시 정상 상태로 돌린다.
                Debug.LogWarning(
                    $"복원된 성장 backup 동기화를 다음 실행으로 미룹니다: " +
                    exception.Message);
            }
            ResetLoadSafetyState();
            Changed?.Invoke();
            return true;
        }

        public static bool TryResetAfterLoadFailure()
        {
            EnsureLoaded();
            if (!writeBlocked ||
                store is not IPermanentGrowthRecoveryStore recoveryStore)
                return false;
            bool wasPrimaryReadFailure = primaryReadFailed;
            if (!RefreshRejectedPrimaryAfterReadFailure())
                return false;
            if (pendingTargetReadFailed && !RefreshPendingRecoveryTarget())
                return false;
            if (wasPrimaryReadFailure &&
                !string.IsNullOrEmpty(validatedRecoveryJson))
            {
                // 일시적인 read 실패가 풀린 정상 primary를 즉시 파괴하지 않는다.
                // UI를 갱신해 사용자가 먼저 저장 복구를 선택할 수 있게 한다.
                Changed?.Invoke();
                return false;
            }

            string physicalBackupJson;
            try
            {
                physicalBackupJson = recoveryStore.LoadBackup();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"새 성장 기록을 만들기 전 backup을 확인하지 못했습니다: " +
                    exception.Message);
                return false;
            }
            bool hasPhysicalBackup = TryReadSupportedSave(
                physicalBackupJson,
                out _,
                out _);
            string backupToQuarantine =
                !hasPhysicalBackup &&
                !string.IsNullOrWhiteSpace(physicalBackupJson)
                    ? physicalBackupJson
                    : rejectedBackupJson;
            SaveData recoveryData = data;
            data = new SaveData();
            string canonicalJson = JsonUtility.ToJson(data);
            try
            {
                QuarantineRejectedPrimary(recoveryStore);
                if (pendingTargetInvalid &&
                    !string.IsNullOrWhiteSpace(rejectedPendingTargetJson))
                    recoveryStore.SaveBackupQuarantine(
                        rejectedPendingTargetJson);
                if (!string.IsNullOrWhiteSpace(backupToQuarantine))
                    recoveryStore.SaveBackupQuarantine(backupToQuarantine);
                if (hasPhysicalBackup)
                {
                    recoveryStore.SaveBackupSyncTarget(string.Empty);
                    recoveryStore.SaveBackupSyncPending(false);
                    recoveryStore.SaveResetPending(true);
                }
                else
                {
                    recoveryStore.SaveBackupSyncTarget(canonicalJson);
                    recoveryStore.SaveBackupSyncPending(true);
                }
            }
            catch (Exception exception)
            {
                data = recoveryData;
                Debug.LogWarning(
                    $"영구 성장 새 기록 저장을 준비하지 못했습니다: " +
                    exception.Message);
                return false;
            }

            PrimaryWriteResult primaryWrite = TryWritePrimary(
                canonicalJson,
                rejectedPrimaryJson,
                out Exception primaryError,
                out string unexpectedPrimaryJson);
            if (primaryWrite != PrimaryWriteResult.Applied)
            {
                data = recoveryData;
                if (!string.IsNullOrWhiteSpace(unexpectedPrimaryJson))
                    rejectedPrimaryJson = unexpectedPrimaryJson;
                Debug.LogWarning(
                    $"영구 성장 새 기록 primary 저장에 실패했습니다: " +
                    primaryError?.Message);
                return false;
            }
            primaryGenerationJson = canonicalJson;
            primaryGenerationKnown = true;

            try
            {
                if (!hasPhysicalBackup)
                {
                    recoveryStore.SaveBackup(canonicalJson);
                    recoveryStore.SaveBackupSyncPending(false);
                    recoveryStore.SaveBackupSyncTarget(string.Empty);
                }
                // physical backup 유무와 관계없이 명시적 초기화의 마지막 commit은
                // reset marker 해제다. 남기면 다음 로드가 초기화를 다시 실행한다.
                recoveryStore.SaveResetPending(false);
            }
            catch (Exception exception)
            {
                // primary 초기화는 이미 확정됐다. backup 동기화 표식이나 reset
                // 표식은 그대로 남겨 다음 로드/변경에서 멱등하게 마무리한다.
                // 여기서 복구 잠금으로 되돌리면 완료된 초기화가 실패처럼 보이고,
                // 사용자가 보존 backup을 다시 복원해 선택을 취소할 수 있다.
                Debug.LogWarning(
                    $"새 성장 기록의 backup 정리를 다음 실행으로 미룹니다: " +
                    exception.Message);
                ResetLoadSafetyState();
                Changed?.Invoke();
                return true;
            }
            ResetLoadSafetyState();
            Changed?.Invoke();
            return true;
        }

        static void ResetLoadSafetyState()
        {
            writeBlocked = false;
            backupAvailable = false;
            backupReadFailed = false;
            pendingTargetReadFailed = false;
            pendingTargetInvalid = false;
            primaryReadFailed = false;
            preferPhysicalBackupForRecovery = false;
            rejectedPrimaryJson = string.Empty;
            rejectedBackupJson = string.Empty;
            rejectedPendingTargetJson = string.Empty;
            validatedRecoveryJson = string.Empty;
            loadState = PermanentGrowthLoadState.Ready;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// 성장 화면 QA용. 보상 중복 방지 값은 보존하고 열매만 초기화한다.
        public static void DebugResetProgress()
        {
            EnsureLoaded();
            if (writeBlocked)
                return;
            MutationSnapshot snapshot = CaptureMutationSnapshot();
            data.ranks.Clear();
            data.ownedNodeIds.Clear();
            data.wallet = 0;
            data.spent = 0;
            data.survivalKeystoneId = string.Empty;
            data.leapKeystoneId = string.Empty;
            data.inkHandlingKeystoneId = string.Empty;
            debugCurrencyOverride = DebugGrowthCurrency;
            if (!Save(snapshot))
                return;
            Changed?.Invoke();
        }

        /// 저장 경제 상한은 건드리지 않고 현재 개발 세션에만 먹빛 999를 제공한다.
        public static void DebugRefillCurrency()
        {
            EnsureLoaded();
            if (writeBlocked)
                return;
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
            primaryGenerationJson = string.Empty;
            primaryGenerationKnown = false;
            ResetLoadSafetyState();
            Changed = null;
            debugCurrencyOverride = -1;
        }

        public static void ResetCacheForTests()
        {
            data = null;
            loaded = false;
            primaryGenerationJson = string.Empty;
            primaryGenerationKnown = false;
            ResetLoadSafetyState();
            Changed = null;
            debugCurrencyOverride = -1;
        }

        public static void RestoreDefaultStoreForTests()
        {
            store = new PlayerPrefsPermanentGrowthStore();
            data = null;
            loaded = false;
            primaryGenerationJson = string.Empty;
            primaryGenerationKnown = false;
            ResetLoadSafetyState();
            Changed = null;
            debugCurrencyOverride = -1;
        }
#endif
    }

#if UNITY_EDITOR
    public sealed class MemoryPermanentGrowthStore : IPermanentGrowthRecoveryStore
    {
        public string Json { get; set; } = string.Empty;
        public string BackupJson { get; set; } = string.Empty;
        public string QuarantineJson { get; private set; } = string.Empty;
        public string BackupQuarantineJson { get; private set; } = string.Empty;
        public int SaveCount { get; private set; }
        public int BackupSaveCount { get; private set; }
        public int QuarantineSaveCount { get; private set; }
        public int BackupQuarantineSaveCount { get; private set; }
        public bool BackupSyncPending { get; private set; }
        public int BackupSyncPendingSaveCount { get; private set; }
        public string BackupSyncTarget { get; private set; } = string.Empty;
        public bool ResetPending { get; private set; }
        public int ResetPendingSaveCount { get; private set; }
        public bool ThrowOnPrimarySave { get; set; }

        public string Load() => Json;

        public void Save(string json)
        {
            if (ThrowOnPrimarySave)
                throw new InvalidOperationException(
                    "Injected primary write failure");
            Json = json ?? string.Empty;
            SaveCount++;
        }

        public string LoadBackup() => BackupJson;

        public void SaveBackup(string json)
        {
            BackupJson = json ?? string.Empty;
            BackupSaveCount++;
        }

        public void SaveQuarantine(string json)
        {
            QuarantineJson = json ?? string.Empty;
            QuarantineSaveCount++;
        }

        public void SaveBackupQuarantine(string json)
        {
            BackupQuarantineJson = json ?? string.Empty;
            BackupQuarantineSaveCount++;
        }

        public bool LoadBackupSyncPending() => BackupSyncPending;

        public void SaveBackupSyncPending(bool pending)
        {
            BackupSyncPending = pending;
            BackupSyncPendingSaveCount++;
        }

        public string LoadBackupSyncTarget() => BackupSyncTarget;

        public void SaveBackupSyncTarget(string json)
        {
            BackupSyncTarget = json ?? string.Empty;
        }

        public bool LoadResetPending() => ResetPending;

        public void SaveResetPending(bool pending)
        {
            ResetPending = pending;
            ResetPendingSaveCount++;
        }
    }
#endif
}
