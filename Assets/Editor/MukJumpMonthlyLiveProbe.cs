using System;
using System.IO;
using BackEnd;
using MukJump.Core;
using UnityEditor;

namespace MukJump.EditorTools
{
    /// 명시적 실행 전용. 기존 인증이 없을 때만 만든 일회성 시험 계정 외에는 쓰지 않는다.
    public static class MukJumpMonthlyLiveProbe
    {
        const string OwnerKey = "MukJump.MonthlyProbe.Owner", RowKey = "MukJump.MonthlyProbe.Row";
        const string Output = "output/qa/monthly-20260924";
        static string Owner => SessionState.GetString(OwnerKey, "");
        static string Row => SessionState.GetString(RowKey, "");
        static void Require(BackendReturnObject result)
        {
            if (result == null || !result.IsSuccess()) throw new InvalidOperationException("Probe HTTP " + result?.GetStatusCode());
        }
        public static void Run(string action)
        {
            if (Environment.GetEnvironmentVariable("MUKJUMP_SERVER_PROBE") != "1") throw new InvalidOperationException("Explicit probe opt-in required");
            Directory.CreateDirectory(Output);
            try
            {
                if (action == "seed") Seed(); else if (action == "verify") Verify(); else if (action == "cleanup") Cleanup();
                else throw new InvalidOperationException("Unknown probe action");
                File.WriteAllText(Output + "/live-" + action + ".txt", "PASS " + action);
            }
            catch (Exception e)
            {
                File.WriteAllText(Output + "/live-" + action + ".txt", "FAIL " + e.GetType().Name + ": " + e.Message);
                throw;
            }
        }
        static void Seed()
        {
            Require(Backend.Initialize());
            if (!string.IsNullOrEmpty(Backend.UserInDate) || Backend.BMember.LoginWithTheBackendToken().IsSuccess())
                throw new InvalidOperationException("Existing authenticated session must not be replaced");
            Require(Backend.BMember.CustomSignUp("monthprobe" + Guid.NewGuid().ToString("N").Substring(0, 14), Guid.NewGuid().ToString("N")));
            SessionState.SetString(OwnerKey, Backend.UserInDate);
            var param = new Param(); param.Add("bestHeight", 1000); param.Add("growthJson", "monthly-probe-kept");
            param.Add("revision", 17); param.Add(MonthlyLeaderboard.SourceColumn, "");
            param.Add(MonthlyLeaderboard.ScoreColumn, 0L); param.Add(MonthlyLeaderboard.AppearanceColumn, "2026-09|KR|1|0");
            var inserted = Backend.GameData.Insert("MukJumpPlayer", param); Require(inserted);
            SessionState.SetString(RowKey, inserted.GetInDate());
        }
        static void CheckOwner()
        {
            if (string.IsNullOrEmpty(Owner) || string.IsNullOrEmpty(Row) || Backend.UserInDate != Owner)
                throw new InvalidOperationException("Probe owner mismatch; no mutation allowed");
        }
        static void Verify()
        {
            CheckOwner(); var settings = MukJumpBackendSettings.Load();
            if (string.IsNullOrEmpty(settings.MonthlyRankUuid)) throw new InvalidOperationException("Monthly board not configured");
            var clock = Backend.Utils.GetServerTime(); Require(clock);
            if (!MukJumpNicknameChange.TryUtc(clock.GetReturnValuetoJSON()["utcTime"].ToString(), out var utc) || MonthlyLeaderboard.IsCounting(utc))
                throw new InvalidOperationException("Monthly reset window");
            string month = MonthlyLeaderboard.Period(utc);
            foreach (var score in new[] { new BestRunRecord(1, 80), new BestRunRecord(1, 50), new BestRunRecord(2, 120) })
            {
                var local = new MonthlyRunRecord { owner = Owner, period = month, runId = Guid.NewGuid().ToString("N"),
                    completedUtc = utc.ToString("O"), height = score.Height, seconds = score.Seconds };
                bool completed = false;
                MonthlyLeaderboardSync.Run(Owner, month, local, () => Backend.UserInDate == Owner,
                    done =>
                    {
                        var result = Backend.GameData.GetMyData("MukJumpPlayer", new Where()); Require(result);
                        var rows = result.FlattenRows();
                        if (rows.Count != 1 || rows[0]["inDate"].ToString() != Row || rows[0]["owner_inDate"].ToString() != Owner ||
                            rows[0]["bestHeight"].ToString() != "1000" || rows[0]["revision"].ToString() != "17" ||
                            rows[0]["growthJson"].ToString() != "monthly-probe-kept") throw new InvalidOperationException("Personal data preservation failed");
                        done(new MonthlyLeaderboardSync.State { UpdatedAt = rows[0]["updatedAt"].ToString(),
                            Record = MonthlyLeaderboard.Decode(rows[0][MonthlyLeaderboard.SourceColumn].ToString()) });
                    },
                    (state, best, done) =>
                    {
                        var where = new Where(); where.Equal("updatedAt", state.UpdatedAt);
                        var data = new Param(); data.Add(MonthlyLeaderboard.SourceColumn, MonthlyLeaderboard.Encode(best));
                        var result = Backend.GameData.Update("MukJumpPlayer", where, data); Require(result); done(true);
                    },
                    (record, done) =>
                    {
                        var result = Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(settings.MonthlyRankUuid,
                            "MukJumpPlayer", Row, MukJumpAccountRuntime.CreateMonthlyLeaderboardParam(record, month, "KR|1|0"));
                        Require(result); done(true);
                    }, ok => completed = ok);
                if (!completed) throw new InvalidOperationException("Sync did not complete");
                var board = Backend.Leaderboard.User.GetLeaderboard(settings.MonthlyRankUuid, 10);
                Require(board); bool found = false;
                foreach (var item in board.GetUserLeaderboardList())
                    if (item.gamerInDate == Owner) found = item.score == score.RankScore.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                        item.extraData == month + "|KR|1|0";
                if (!found) throw new InvalidOperationException("Monthly TOP10 mismatch");
            }
        }
        static void Cleanup()
        {
            CheckOwner(); var settings = MukJumpBackendSettings.Load();
            if (!string.IsNullOrEmpty(settings.MonthlyRankUuid))
            {
                var param = new Param(); param.Add(MonthlyLeaderboard.ScoreColumn, 0L);
                Require(Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(settings.MonthlyRankUuid, "MukJumpPlayer", Row, param));
            }
            Require(Backend.GameData.DeleteV2("MukJumpPlayer", Row, Owner));
            Require(Backend.BMember.WithdrawAccount());
            SessionState.EraseString(OwnerKey); SessionState.EraseString(RowKey);
        }
    }
}
