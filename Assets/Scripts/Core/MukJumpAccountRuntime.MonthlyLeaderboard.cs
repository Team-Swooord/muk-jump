#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using BackEnd;
using UnityEngine;

namespace MukJump.Core
{
    public sealed partial class MukJumpAccountRuntime
    {
        readonly MonthlyServerClock monthlyClock = new();
        double monthlyTimeRetry, monthlySyncRetry;
        bool monthlyTimeInFlight, monthlySyncRequested = true;
        string monthlyVisiblePeriod = "", monthlySubmissionFailure = "";
        bool monthlyPeriodBlocked = true, monthlyRefreshPending;
        long monthlyClockRequest;
#if UNITY_EDITOR
        Action<Action<BackendReturnObject>> monthlyTimeForTests;
        Action<Param, Action<BackendReturnObject>> monthlyPublishForTests;
#endif
        public bool UsesMonthlyLeaderboard => settings != null && settings.UsesMonthlyLeaderboard;
        public string MonthlyRunOwner => UsesMonthlyLeaderboard && !BlocksGameplayForAccountSync ? KnownAccountScope() : "";
        public string MonthlyPeriodLabel => monthlyVisiblePeriod;
        public MonthlyRunRecord CaptureMonthlyRun(string owner, string run, BestRunRecord record)
        {
            if (!UsesMonthlyLeaderboard || string.IsNullOrEmpty(owner) || owner != KnownAccountScope() ||
                !monthlyClock.TryNow(Time.realtimeSinceStartupAsDouble, out var utc)) return null;
            return new MonthlyRunRecord { owner = owner, runId = run, period = MonthlyLeaderboard.Period(utc),
                completedUtc = utc.ToString("O"), height = record.Height, seconds = record.Seconds };
        }
        void PumpMonthlyLeaderboard()
        {
            if (!UsesMonthlyLeaderboard) return;
#if !UNITY_EDITOR
            if (!Backend.IsInitialized) { UpdateMonthlyPeriod(); return; }
#endif
            double now = Time.realtimeSinceStartupAsDouble;
            if (monthlyTimeInFlight && now >= monthlyTimeRetry) { monthlyTimeInFlight = false; monthlyClockRequest++; }
            if (!monthlyTimeInFlight && now >= monthlyTimeRetry)
            {
                monthlyTimeInFlight = true;
                monthlyTimeRetry = now + 30;
                long request = ++monthlyClockRequest;
                try { RequestMonthlyTime(bro =>
                {
                    if (this == null || !isActiveAndEnabled || request != monthlyClockRequest) return;
                    monthlyTimeInFlight = false;
                    monthlyTimeRetry = Time.realtimeSinceStartupAsDouble + 60;
                    try
                    {
                        if (bro != null && bro.IsSuccess() && MukJumpNicknameChange.TryUtc(bro.GetReturnValuetoJSON()["utcTime"].ToString(), out var utc))
                            monthlyClock.Set(utc, now, Time.realtimeSinceStartupAsDouble);
                    }
                    catch (Exception) { }
                    UpdateMonthlyPeriod();
                }); }
                catch (Exception) { monthlyTimeInFlight = false; monthlyTimeRetry = now + 60; }
            }
            bool available = UpdateMonthlyPeriod();
            if (available && monthlyRefreshPending && leaderboardRequested && IsOnlineAuthenticated && !LeaderboardLoading)
            { monthlyRefreshPending = false; RefreshLeaderboard(); }
            if (now >= monthlySyncRetry)
            {
                if (monthlySyncRequested || leaderboardRequested || MonthlyLeaderboard.ReadLocal(KnownAccountScope()) != null)
                    TrySyncMonthlyLeaderboard();
                // 미참가 계정의 빈 로컬 키를 매 프레임 읽지 않는다.
                else monthlySyncRetry = now + 60;
            }
        }
        void RequestMonthlyTime(Action<BackendReturnObject> done)
        {
#if UNITY_EDITOR
            if (monthlyTimeForTests != null) monthlyTimeForTests(done); else done(null);
#else
            if (Backend.IsInitialized) Backend.Utils.GetServerTime(bro => done(bro)); else done(null);
#endif
        }
        void SetMonthlyStatus(string text)
        {
            if (LeaderboardStatus == text) return;
            LeaderboardStatus = text; NotifyStateChangedSafely();
        }
        bool UpdateMonthlyPeriod()
        {
            if (!monthlyClock.TryNow(Time.realtimeSinceStartupAsDouble, out var utc))
            {
                monthlyPeriodBlocked = true;
                monthlyVisiblePeriod = "";
                if (leaderboardEntries.Count > 0) { leaderboardEntries.Clear(); NotifyStateChangedSafely(); }
                SetMonthlyStatus("월별 순위의 서버 시간을 확인하는 중입니다");
                return false;
            }
            string month = MonthlyLeaderboard.Period(utc);
            if (month != monthlyVisiblePeriod)
            {
                monthlyVisiblePeriod = month; leaderboardEntries.Clear(); monthlySyncRequested = true;
                monthlySubmissionFailure = ""; monthlyRefreshPending = true;
                monthlySyncRetry = 0;
                NotifyStateChangedSafely();
            }
            if (MonthlyLeaderboard.IsCounting(utc))
            {
                monthlyPeriodBlocked = true;
                leaderboardEntries.Clear();
                SetMonthlyStatus("월별 순위 정산 중 · 한국시간 01시 이후 반영됩니다");
                return false;
            }
            if (monthlyPeriodBlocked)
            {
                monthlyPeriodBlocked = false; monthlySyncRequested = true; monthlySyncRetry = 0;
                monthlyRefreshPending = true;
                SetMonthlyStatus("이번 달 완료한 도전부터 순위에 등록됩니다");
            }
            return true;
        }
        bool MonthlyRequestCurrent(long session, string owner, string month) =>
            IsCurrentAccountSession(session, owner) && monthlyClock.TryNow(Time.realtimeSinceStartupAsDouble, out var utc) &&
            MonthlyLeaderboard.Period(utc) == month && !MonthlyLeaderboard.IsCounting(utc);

        void TrySyncMonthlyLeaderboard()
        {
            if (Time.realtimeSinceStartupAsDouble < monthlySyncRetry ||
                !IsOnlineAuthenticated || BlocksGameplayForAccountSync || syncWriteBlocked || temporaryBackendPause ||
                identityBusy || dirty || saveInFlight || cloudLoadInFlight || leaderboardSaveInFlight ||
                string.IsNullOrEmpty(rowInDate) || !UpdateMonthlyPeriod()) return;
            string owner = CurrentAccountScope(), month = monthlyVisiblePeriod;
            long session = accountSessionGeneration;
            if (string.IsNullOrEmpty(owner)) return;
            leaderboardSaveInFlight = true;
            leaderboardSaveDeadlineRealtime = Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            monthlySyncRequested = false;
            monthlySyncRetry = Time.realtimeSinceStartupAsDouble + 60;
            string error = "";
            try
            {
                MonthlyLeaderboardSync.Run(owner, month, MonthlyLeaderboard.ReadLocal(owner),
                    () => MonthlyRequestCurrent(session, owner, month),
                    done => RequestMyGameData(settings.PlayerTableName, new Where(), bro =>
                    {
                        if (!MonthlyRequestCurrent(session, owner, month)) { done(null); return; }
                        try
                        {
                            if (bro == null || !bro.IsSuccess()) { done(null); return; }
                            var rows = bro.FlattenRows();
                            if (rows == null || rows.Count != 1 || ReadString(rows[0], "inDate") != rowInDate ||
                                ReadString(rows[0], "owner_inDate") != owner) { done(null); return; }
                            done(new MonthlyLeaderboardSync.State { UpdatedAt = ReadString(rows[0], "updatedAt"),
                                Record = MonthlyLeaderboard.Decode(ReadString(rows[0], MonthlyLeaderboard.SourceColumn)) });
                        }
                        catch (Exception) { done(null); }
                    }),
                    (state, best, done) =>
                    {
                        var where = new Where(); where.Equal("updatedAt", state.UpdatedAt);
                        var param = new Param(); param.Add(MonthlyLeaderboard.SourceColumn, MonthlyLeaderboard.Encode(best));
                        RequestUpdateGameData(settings.PlayerTableName, where, param, bro => done(bro != null && bro.IsSuccess()));
                    },
                    (best, done) => RequestMonthlyPublish(best, month, bro =>
                    {
                        error = bro?.GetStatusCode() ?? "응답 없음";
                        done(bro != null && bro.IsSuccess());
                    }),
                    ok =>
                    {
                        if (!IsCurrentAccountSession(session, owner)) return;
                        leaderboardSaveInFlight = false;
                        monthlySyncRequested = !ok;
                        monthlySyncRetry = Time.realtimeSinceStartupAsDouble + (error == "428" ? 300 : 60);
                        monthlySubmissionFailure = ok ? "" : error == "428"
                            ? "월별 순위 정산 중 · 한국시간 01시 이후 반영됩니다"
                            : "이번 달 기록은 저장되었으며 순위 반영을 다시 시도합니다";
                        if (!ok) LeaderboardStatus = monthlySubmissionFailure;
                        if (ok && leaderboardRequested) RefreshLeaderboard();
                        NotifyStateChangedSafely();
                    });
            }
            catch (Exception)
            {
                leaderboardSaveInFlight = false; monthlySyncRequested = true;
                monthlySyncRetry = Time.realtimeSinceStartupAsDouble + 60;
            }
        }
        void RequestMonthlyPublish(MonthlyRunRecord record, string month, Action<BackendReturnObject> done)
        {
            var param = CreateMonthlyLeaderboardParam(record, month, LeaderboardAppearance.Current);
#if UNITY_EDITOR
            if (monthlyPublishForTests != null) { monthlyPublishForTests(param, done); return; }
#endif
            Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(settings.MonthlyRankUuid,
                settings.PlayerTableName, rowInDate, param, bro => done(bro));
        }
        public static Param CreateMonthlyLeaderboardParam(MonthlyRunRecord record, string month, string appearance)
        {
            var param = new Param();
            param.Add(MonthlyLeaderboard.ScoreColumn, record.Record.RankScore);
            param.Add(MonthlyLeaderboard.AppearanceColumn, month + "|" + appearance);
            return param;
        }
    }
}
#endif
