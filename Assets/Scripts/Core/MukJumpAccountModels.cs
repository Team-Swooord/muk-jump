using System;
using UnityEngine;

namespace MukJump.Core
{
    public enum MukJumpAccountKind
    {
        LocalGuest,
        BackendGuest,
        Google,
        Apple,
    }

    public enum MukJumpAccountPhase
    {
        LocalReady,
        Connecting,
        OnlineReady,
        NeedsAccountChoice,
        NeedsSyncChoice,
        Deleting,
        Error,
    }

    [Serializable]
    public sealed class MukJumpCloudSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int bestHeight;
        public string growthJson = string.Empty;
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public int tutorialVersion;
        public long revision;
        public string updatedAtUtc = string.Empty;
        public string lastOperationId = string.Empty;

        public static MukJumpCloudSnapshot Capture(
            int bestHeight,
            long revision,
            string operationId)
        {
            PermanentGrowthProfile.TryExportCloudJson(out string growth);
            return new MukJumpCloudSnapshot
            {
                bestHeight = Mathf.Max(0, bestHeight),
                growthJson = growth ?? string.Empty,
                bgmVolume = LobbySettingsProfile.BgmVolume,
                sfxVolume = LobbySettingsProfile.SfxVolume,
                tutorialVersion = LobbySettingsProfile.GameplayTutorialVersion,
                revision = Math.Max(0L, revision),
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                lastOperationId = operationId ?? string.Empty,
            };
        }

        public bool IsSupported =>
            schemaVersion == CurrentSchemaVersion &&
            bestHeight >= 0 &&
            revision >= 0 &&
            !string.IsNullOrWhiteSpace(growthJson);
    }

    public static class MukJumpCloudMergePolicy
    {
        /// 최고 고도는 손실 없이 합친다. 소비 가능한 성장 재화와 구매 상태는
        /// 계정 중복 지급을 막기 위해 기존 서버 세대를 그대로 사용한다.
        public static int MergeBestHeight(int localBest, int serverBest) =>
            Mathf.Max(0, Mathf.Max(localBest, serverBest));

        public static bool ShouldAcceptServerGrowth(
            MukJumpCloudSnapshot server) =>
            server != null && server.IsSupported;
    }

    [Serializable]
    public sealed class MukJumpLeaderboardEntry
    {
        public int Rank { get; }
        public int Height { get; }

        public MukJumpLeaderboardEntry(int rank, int height)
        {
            Rank = Mathf.Max(1, rank);
            Height = Mathf.Max(0, height);
        }
    }
}
