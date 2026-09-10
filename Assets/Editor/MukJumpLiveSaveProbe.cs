using System;
using BackEnd;
using MukJump.Core;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 명시적 서버 진단 실행만 허용한다. 사용자 계정 대신 매번 고립된 시험 계정을 쓴다.
    public static class MukJumpLiveSaveProbe
    {
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
            }
            finally { Require("withdraw-probe-only", Backend.BMember.WithdrawAccount()); }
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
