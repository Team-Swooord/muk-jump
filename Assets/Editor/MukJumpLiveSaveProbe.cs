using System;
using BackEnd;
using MukJump.Core;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 명시적 서버 진단 실행만 허용한다. 사용자 계정 대신 매번 고립된 시험 계정을 쓴다.
    public static class MukJumpLiveSaveProbe
    {
        // 운영 계정은 읽거나 변경하지 않고, 명시적으로 만든 시험 계정 한 개만 사용한다.
        public static void RunNickname()
        {
            if (Environment.GetEnvironmentVariable("MUKJUMP_SERVER_PROBE") != "1")
                throw new InvalidOperationException("Explicit live probe opt-in required.");
            Require("nickname-initialize", Backend.Initialize());
            string id = "nickprobe" + Guid.NewGuid().ToString("N").Substring(0, 16);
            Require("nickname-signup", Backend.BMember.CustomSignUp(id, Guid.NewGuid().ToString("N")));
            string owner = Backend.UserInDate;
            var settings = MukJumpBackendSettings.Load();
            string rowId = null;
            try
            {
                var initial = new Param();
                initial.Add("revision", 1L);
                initial.Add(settings.BestHeightColumn, 156);
                initial.Add("growthJson", "nickname-probe-unchanged");
                var inserted = Backend.GameData.Insert(settings.PlayerTableName, initial);
                Require("nickname-insert", inserted); rowId = inserted.GetInDate();
                var original = new Where(); original.Equal("inDate", rowId);
                original.Equal("updatedAt", ReadState().UpdatedAt);
                var metadata = new Param(); metadata.Add(MukJumpNicknameChange.PendingNameColumn, "probe");
                var rejected = Backend.GameData.Update(settings.PlayerTableName, original, metadata);
                Report("nickname-original-query", rejected);
                if (rejected.IsSuccess()) throw new InvalidOperationException("Original query unexpectedly accepted");

                string requested = "n" + Guid.NewGuid().ToString("N").Substring(0, 9);
                Run(requested, true);
                string changedAt = ReadState().ChangedAt;
                if (string.IsNullOrEmpty(changedAt) || ReadState().PendingName != "")
                    throw new InvalidOperationException("Nickname reservation was not committed");
                Run(requested, true);
                if (ReadState().ChangedAt != changedAt) throw new InvalidOperationException("Same name extended cooldown");
                Run("n" + Guid.NewGuid().ToString("N").Substring(0, 9), false);
                var final = Backend.GameData.GetMyData(settings.PlayerTableName, new Where());
                Require("nickname-readback", final);
                var row = final.FlattenRows()[0];
                if (row[settings.BestHeightColumn].ToString() != "156" ||
                    row["growthJson"].ToString() != "nickname-probe-unchanged")
                    throw new InvalidOperationException("Nickname changed game progress");
                Debug.Log("NICKNAME_PROBE PASS: save/readback, same-name retry, 14-day block, score/growth preserved");

                MukJumpNicknameChange.State ReadState()
                {
                    var response = Backend.GameData.GetMyData(settings.PlayerTableName, new Where());
                    Require("nickname-read-policy", response);
                    var rows = response.FlattenRows();
                    if (rows.Count != 1 || rows[0]["owner_inDate"].ToString() != owner ||
                        rows[0]["inDate"].ToString() != rowId) throw new InvalidOperationException("Probe owner/row mismatch");
                    string Value(string key) => rows[0].ContainsKey(key) ? rows[0][key]?.ToString() ?? "" : "";
                    return new MukJumpNicknameChange.State { Row = rowId, UpdatedAt = Value("updatedAt"),
                        ChangedAt = Value(MukJumpNicknameChange.ChangedAtColumn),
                        PendingName = Value(MukJumpNicknameChange.PendingNameColumn),
                        PreviousAt = Value(MukJumpNicknameChange.PreviousAtColumn) };
                }
                void Run(string name, bool expected)
                {
                    bool? accepted = null; string outcome = "";
                    MukJumpNicknameChange.Run(name, () => Backend.UserInDate == owner,
                        cb => cb(Backend.Utils.GetServerTime()), cb => cb(Backend.BMember.GetUserInfo()),
                        cb => cb(ReadState()), (state, cb) =>
                        {
                            var param = new Param();
                            param.Add(MukJumpNicknameChange.ChangedAtColumn, state.ChangedAt);
                            param.Add(MukJumpNicknameChange.PendingNameColumn, state.PendingName);
                            param.Add(MukJumpNicknameChange.PreviousAtColumn, state.PreviousAt);
                            var result = Backend.GameData.Update(settings.PlayerTableName,
                                MukJumpAccountRuntime.BuildNicknameUpdateCondition(state), param);
                            Report("nickname-policy-write", result); cb(result.IsSuccess());
                        }, (value, cb) => cb(Backend.BMember.UpdateNickname(value)),
                        (ok, message) => { accepted = ok; outcome = message; });
                    if (accepted != expected) throw new InvalidOperationException("Nickname result mismatch");
                    if (!expected && outcome != MukJumpNicknameChange.WaitMessage)
                        throw new InvalidOperationException("Expected cooldown rejection");
                    if (expected && Backend.BMember.GetUserInfo().GetReturnValuetoJSON()["row"]["nickname"].ToString() != name)
                        throw new InvalidOperationException("Server nickname mismatch");
                }
            }
            finally
            {
                if (Backend.UserInDate != owner) throw new InvalidOperationException("Refusing cleanup of another account");
                if (!string.IsNullOrEmpty(rowId))
                    Require("nickname-delete-probe-row", Backend.GameData.DeleteV2(settings.PlayerTableName, rowId, owner));
                Require("nickname-withdraw-probe-only", Backend.BMember.WithdrawAccount());
            }
        }

        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("MUKJUMP_SERVER_PROBE") != "1")
                throw new InvalidOperationException("Explicit live probe opt-in required.");
            Require("initialize", Backend.Initialize());
            string id = "saveprobe" + Guid.NewGuid().ToString("N").Substring(0, 16);
            Require("signup", Backend.BMember.CustomSignUp(id, Guid.NewGuid().ToString("N")));
            Debug.Log("SAVE_PROBE account=" + id);
            try
            {
                var settings = MukJumpBackendSettings.Load();
                var initial = new Param();
                initial.Add("revision", 1L);
                initial.Add("lastOperationId", "probe-initial");
                initial.Add(settings.BestHeightColumn, 0);
                initial.Add(DeviceRegion.Column, DeviceRegion.Current);
                var inserted = Backend.GameData.Insert(settings.PlayerTableName, initial);
                Require("insert", inserted);
                var where = new Where();
                where.Equal("inDate", inserted.GetInDate());
                where.Equal("revision", 1L);
                var update = new Param();
                update.Add("revision", 2L);
                update.Add(settings.BestHeightColumn, 237);
                var result = Backend.GameData.Update(settings.PlayerTableName, where, update);
                Report("update-original-query", result);
                var corrected = Backend.GameData.Update(settings.PlayerTableName,
                    MukJumpAccountRuntime.BuildCloudUpdateCondition(1L, "probe-initial"), update);
                Require("update-corrected-query", corrected);
                var rows = Backend.GameData.GetMyData(settings.PlayerTableName, new Where());
                Require("readback", rows);
                int actual = int.Parse(rows.FlattenRows()[0][settings.BestHeightColumn].ToString());
                Debug.Log("SAVE_PROBE verified-height=" + actual);
                if (actual != 237) throw new InvalidOperationException("Saved height mismatch");
                var stale = Backend.GameData.Update(settings.PlayerTableName,
                    MukJumpAccountRuntime.BuildCloudUpdateCondition(1L, "probe-initial"), update);
                Report("stale-revision-rejected", stale);
                if (stale.IsSuccess()) throw new InvalidOperationException("Stale write was accepted");
                Require("nickname", Backend.BMember.UpdateNickname("p" + id.Substring(id.Length - 9)));
                var rankData = new Param();
                rankData.Add(settings.BestHeightColumn, 237);
                rankData.Add(DeviceRegion.Column, DeviceRegion.Current);
                Require("leaderboard-update", Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(
                    settings.AllTimeRankUuid, settings.PlayerTableName, inserted.GetInDate(), rankData));
                var leaderboard = Backend.Leaderboard.User.GetLeaderboard(settings.AllTimeRankUuid, 10);
                if (!leaderboard.IsSuccess()) throw new InvalidOperationException("Leaderboard read failed");
                bool matched = false;
                foreach (var item in leaderboard.GetUserLeaderboardList())
                    if (item.nickname == "p" + id.Substring(id.Length - 9))
                    {
                        Debug.Log("SAVE_PROBE rank-height=" + item.score + " region=" + item.extraData);
                        matched = item.score == "237" && item.extraData == DeviceRegion.Current;
                    }
                if (!matched) throw new InvalidOperationException("Leaderboard height/region mismatch");
            }
            finally
            {
                // 콘솔의 스키마 미정의 필드 탐색을 위한 명시적 시험 데이터만 잠시 유지한다.
                if (Environment.GetEnvironmentVariable("MUKJUMP_PROBE_KEEP_ACCOUNT") == "1")
                    Debug.Log("SAVE_PROBE console-cleanup-required account=" + id);
                else Require("withdraw-probe-only", Backend.BMember.WithdrawAccount());
            }
        }

        static void Require(string stage, BackendReturnObject result)
        {
            Report(stage, result);
            if (result == null || !result.IsSuccess()) throw new InvalidOperationException(stage);
        }

        static void Report(string stage, BackendReturnObject result) => Debug.Log(
            "SAVE_PROBE " + stage + " status=" + result?.GetStatusCode() +
            " error=" + result?.GetErrorCode() + " message=" + result?.GetMessage());
    }
}
