using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MukJump.Core
{
    public enum GameLanguage { Korean, English }

    /// 원문은 게임 로직에 남기고 표시 경계에서만 번역한다. 계정·저장 키는 번역하지 않는다.
    public static class GameLocalization
    {
        public static GameLanguage Language
        {
            get
            {
                try { return LobbySettingsProfile.Language; }
                catch (Exception) { return GameLanguage.Korean; }
            }
        }
        public static bool IsEnglish => Language == GameLanguage.English;
        public static event Action Changed;

        public static bool SetLanguage(GameLanguage language) =>
            LobbySettingsProfile.TrySetLanguage(language);

        internal static void NotifyChanged()
        {
            InkLocalizedText.RefreshAll();
            InkLocalizedGameLogo.RefreshAll();
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); }
                catch (Exception exception)
                {
                    Debug.LogWarning("[MukJump] 언어 변경 알림 실패: " + exception.Message);
                }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        static readonly Regex InklightCost = new(@"^먹빛 (\d+)개 필요$");
        static readonly Regex HeightLabel = new(@"^(고도|최고) (.+)$");
        static readonly Regex HealthValue = new(@"^\+(\d+)칸$");
        static readonly Regex DistanceExperience = new(@"^먹빛 \+1 · ([0-9]+ / [0-9]+m)$");
        static readonly Regex TotalDistance = new(@"^누적 ([0-9,]+m)$");
        static readonly Regex DistanceRewardInfo = new(@"^누적 ([0-9,]+)m마다 먹빛 1개$");
        static readonly Regex NextDistanceReward = new(@"^다음 먹빛까지 ([0-9,]+)m$");
        static readonly Regex ResultInklight = new(@"^(예상 )?먹빛 \+(\d+)$");
        static readonly Regex RichTag = new(@"(<[^>]+>)");
        static readonly Regex LeaderboardRetry = new(@"^기록은 저장됐지만 순위 등록을 재시도 중입니다 \(([0-9]{3}|응답 없음)\)$");

        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source) || !IsEnglish) return source ?? string.Empty;
            return English(source);
        }

        static string English(string source)
        {
            const string saveRetryPrefix = "서버 저장을 다시 시도합니다 (";
            if (source.StartsWith(saveRetryPrefix, StringComparison.Ordinal))
                return "Retrying cloud save (" + source.Substring(saveRetryPrefix.Length);
            if (EnglishTranslationTable.Values.TryGetValue(source, out string value)) return value;
            Match retry = LeaderboardRetry.Match(source);
            if (retry.Success)
                return "Progress saved. Retrying leaderboard submission (" +
                    (retry.Groups[1].Value == "응답 없음" ? "no response" : retry.Groups[1].Value) + ")";
            Match match = InklightCost.Match(source);
            if (match.Success) return "Need " + match.Groups[1].Value + " Inklight";
            match = HeightLabel.Match(source);
            if (match.Success) return (match.Groups[1].Value == "고도" ? "Height " : "Best ") +
                match.Groups[2].Value.Replace("—", "-");
            match = HealthValue.Match(source);
            if (match.Success) return "+" + match.Groups[1].Value + " HP";
            match = DistanceExperience.Match(source);
            if (match.Success) return "Inklight +1 · " + match.Groups[1].Value;
            match = TotalDistance.Match(source);
            if (match.Success) return "Total " + match.Groups[1].Value;
            match = DistanceRewardInfo.Match(source);
            if (match.Success) return "1 Inklight per " + match.Groups[1].Value + "m total";
            match = NextDistanceReward.Match(source);
            if (match.Success) return match.Groups[1].Value + "m to next Inklight";
            match = ResultInklight.Match(source);
            if (match.Success) return (match.Groups[1].Success ? "Est. Inklight +" : "Inklight +") + match.Groups[2].Value;
            if (source.Contains('\n'))
            {
                string[] lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = English(lines[i]);
                return string.Join("\n", lines);
            }
            if (RichTag.IsMatch(source))
            {
                string[] parts = RichTag.Split(source);
                for (int i = 0; i < parts.Length; i++)
                    if (!parts[i].StartsWith("<", StringComparison.Ordinal)) parts[i] = English(parts[i]);
                return string.Concat(parts);
            }
            // 계정 오류 뒤에 붙이는 추가 지시문은 알려진 접미 문장만 분리한다.
            foreach (var suffix in EnglishTranslationTable.AccountSuffixes)
                if (source.EndsWith(suffix.Key, StringComparison.Ordinal))
                    return English(source.Substring(0, source.Length - suffix.Key.Length)) + suffix.Value;
            return source;
        }
    }
}
