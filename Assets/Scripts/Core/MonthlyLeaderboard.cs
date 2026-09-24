using System;
using System.Globalization;
using UnityEngine;

namespace MukJump.Core
{
    /// 개인 역대 기록과 독립된, 완료한 한 판의 월별 순위 증빙이다.
    [Serializable]
    public sealed class MonthlyRunRecord
    {
        public string owner = "", period = "", runId = "", completedUtc = "";
        public int height, seconds = -1;
        public BestRunRecord Record => new(height, seconds);
        public bool IsValid => !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(runId) &&
            height > 0 && seconds >= -1 && seconds <= BestRunRecord.MaximumTimedSeconds &&
            DateTimeOffset.TryParse(completedUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) &&
            period == MonthlyLeaderboard.Period(utc);
        public bool BelongsTo(string account, string month) => IsValid && owner == account && period == month;
    }

    public static class MonthlyLeaderboard
    {
        public const string SourceColumn = "monthlyBestJson";
        public const string ScoreColumn = "monthlyHeightTimeScore";
        public const string AppearanceColumn = "monthlyAppearance";
        const string KeyPrefix = "MukJump.Monthly.Record.";
        public static string Period(DateTimeOffset utc) => utc.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        public static bool IsCounting(DateTimeOffset utc)
        {
            var korea = utc.ToOffset(TimeSpan.FromHours(9));
            return korea.Day == 1 && korea.Hour == 0;
        }
        public static string Encode(MonthlyRunRecord value) => value == null ? "" : JsonUtility.ToJson(value);
        public static MonthlyRunRecord Decode(string value)
        {
            try { var record = JsonUtility.FromJson<MonthlyRunRecord>(value ?? ""); return record != null && record.IsValid ? record : null; }
            catch (Exception) { return null; }
        }
        public static MonthlyRunRecord Better(MonthlyRunRecord a, MonthlyRunRecord b, string owner, string period)
        {
            if (a == null || !a.BelongsTo(owner, period)) a = null;
            if (b == null || !b.BelongsTo(owner, period)) b = null;
            return a == null || b != null && b.Record.IsBetterThan(a.Record) ? b : a;
        }
        public static MonthlyRunRecord ReadLocal(string owner) => string.IsNullOrEmpty(owner) ? null : Decode(PlayerPrefs.GetString(KeyPrefix + owner, ""));
        public static bool StoreCompleted(MonthlyRunRecord candidate)
        {
            // 신뢰할 시각/소유자가 없는 판도 개인 최고·성장은 정상 저장한다.
            if (candidate == null || !candidate.IsValid) return true;
            try
            {
                var old = ReadLocal(candidate.owner);
                // 복구된 전월 판이 이미 저장한 새달 기록을 덮지 못한다.
                if (old != null && string.CompareOrdinal(old.period, candidate.period) > 0) return true;
                var best = Better(old, candidate, candidate.owner, candidate.period);
                PlayerPrefs.SetString(KeyPrefix + candidate.owner, Encode(best));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception) { return false; }
        }
        public static void ClearOwner(string owner)
        {
            if (!string.IsNullOrEmpty(owner)) PlayerPrefs.DeleteKey(KeyPrefix + owner);
        }
        public static bool TryAppearance(string data, string month, out string appearance)
        {
            string prefix = month + "|";
            appearance = data != null && data.StartsWith(prefix, StringComparison.Ordinal) ? data.Substring(prefix.Length) : "";
            return appearance.Length > 0;
        }
    }

    /// 기기 달력 변경과 무관한 서버 시각 표본. 재시작/긴 오프라인에서는 다시 확인한다.
    public sealed class MonthlyServerClock
    {
        DateTimeOffset utc;
        double received = double.NegativeInfinity, uncertainty;
        public void Set(DateTimeOffset serverUtc, double sentAt, double receivedAt)
        {
            utc = serverUtc; received = receivedAt; uncertainty = Math.Max(0, receivedAt - sentAt);
        }
        public bool TryNow(double realtime, out DateTimeOffset now)
        {
            now = default;
            double elapsed = realtime - received;
            if (elapsed < 0 || elapsed > 3600 || double.IsInfinity(elapsed) || double.IsNaN(elapsed)) return false;
            now = utc.AddSeconds(elapsed);
            // 네트워크 지연 범위가 월 경계를 걸치면 어느 달인지 추정하지 않는다.
            return MonthlyLeaderboard.Period(now) == MonthlyLeaderboard.Period(now.AddSeconds(uncertainty));
        }
    }

    /// 원본은 조건부 max 병합, 게시 점수는 별도로 갱신한다. 오래된 게시 응답 뒤에도 원본을 재검증한다.
    public static class MonthlyLeaderboardSync
    {
        public sealed class State { public string UpdatedAt; public MonthlyRunRecord Record; }
        public static void Run(string owner, string month, MonthlyRunRecord local, Func<bool> current,
            Action<Action<State>> read, Action<State, MonthlyRunRecord, Action<bool>> write,
            Action<MonthlyRunRecord, Action<bool>> publish, Action<bool> completed)
        {
            bool finished = false;
            int attempts = 0;
            bool Active() => !finished && current();
            void Finish(bool ok) { if (finished) return; finished = true; completed(ok); }
            void Step()
            {
                if (!Active()) { Finish(false); return; }
                if (++attempts > 4) { Finish(false); return; }
                bool readReceived = false;
                read(state =>
                {
                    if (readReceived) return; readReceived = true;
                    if (!Active()) { Finish(false); return; }
                    if (state == null || string.IsNullOrEmpty(state.UpdatedAt)) { Finish(false); return; }
                    if (state.Record != null && state.Record.owner == owner && string.CompareOrdinal(state.Record.period, month) > 0)
                    { Finish(false); return; }
                    var best = MonthlyLeaderboard.Better(state.Record, local, owner, month);
                    if (best == null) { Finish(true); return; }
                    if (state.Record == null || !state.Record.BelongsTo(owner, month) || best.Record.IsBetterThan(state.Record.Record))
                    {
                        bool writeReceived = false;
                        write(state, best, ok => { if (writeReceived) return; writeReceived = true; if (Active()) Step(); else Finish(false); });
                        return;
                    }
                    bool publishReceived = false;
                    publish(best, ok =>
                    {
                        if (publishReceived) return; publishReceived = true;
                        if (!Active() || !ok) { Finish(false); return; }
                        bool afterReceived = false;
                        read(after =>
                        {
                            if (afterReceived) return; afterReceived = true;
                            if (!Active() || after == null) { Finish(false); return; }
                            var latest = MonthlyLeaderboard.Better(after.Record, local, owner, month);
                            if (latest == null) { Finish(false); return; }
                            if (latest.Record.IsBetterThan(best.Record)) Step();
                            else Finish(true);
                        });
                    });
                });
            }
            Step();
        }
    }
}
