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

    /// 인증 전환 복구 시 이전 소유자와 현재 서버 소유자의 관계.
    /// 현재 소유자를 아직 읽지 못한 상태를 같은 계정으로 간주하면 복구 표식이
    /// 조기에 사라질 수 있으므로 Unknown을 명시적으로 분리한다.
    public enum MukJumpAccountScopeRelation
    {
        Unknown,
        Same,
        Changed,
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
        public string DisplayName { get; }
        public string Source { get; }

        public MukJumpLeaderboardEntry(int rank, int height, string displayName = null,
            string source = "BACKND")
        {
            Rank = Mathf.Max(1, rank);
            Height = Mathf.Max(0, height);
            DisplayName = CleanDisplayName(displayName);
            // 출처는 실제 공급자만 표시한다. 로그인 수단으로 OS를 추측하지 않는다.
            Source = source == "APPLE" || source == "TOSS" ? source : "BACKND";
        }

        public static string CleanDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "이름 없는 먹방울";
            var clean = new System.Text.StringBuilder();
            var elements = System.Globalization.StringInfo.GetTextElementEnumerator(value.Trim());
            int count = 0;
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(element, 0);
                if (category == System.Globalization.UnicodeCategory.Control ||
                    category == System.Globalization.UnicodeCategory.Format ||
                    category == System.Globalization.UnicodeCategory.LineSeparator ||
                    category == System.Globalization.UnicodeCategory.ParagraphSeparator)
                    continue;
                // 정상 닉네임 최대 20자는 보존하고, 화면 폭에 따른 생략은 뷰가 맡는다.
                // 여섯 글자로 먼저 자르면 모든 guest(난수)가 같은 이름으로 보인다.
                if (count++ == 20) { clean.Append('…'); break; }
                clean.Append(element);
            }
            return clean.Length == 0 ? "이름 없는 먹방울" : clean.ToString();
        }
    }
}
