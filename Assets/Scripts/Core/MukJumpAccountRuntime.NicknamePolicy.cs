#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using BackEnd;

namespace MukJump.Core
{
    public sealed partial class MukJumpAccountRuntime
    {
#if UNITY_EDITOR
        Action<Action<BackendReturnObject>> nicknameTimeForTests;
        Action<Action<MukJumpNicknameChange.State>> nicknamePolicyReadForTests;
        Action<MukJumpNicknameChange.State, Action<bool>> nicknamePolicyWriteForTests;
#endif
        void ChangeNicknameWithCooldown(string value, Action<bool, string> completed)
        {
            string scope = CurrentAccountScope();
            long session = accountSessionGeneration;
            long request = BeginIdentityRequest(completed);
            MukJumpNicknameChange.Run(value, () => IsIdentityRequestCurrent(request, session, scope),
                RequestNicknameServerTime, RequestIdentityInfo,
                done => ReadNicknamePolicy(scope, request, session, done),
                (state, done) => WriteNicknamePolicy(scope, state, done), RequestNicknameUpdate,
                (success, message) =>
                {
                    if (success)
                    {
                        try { CacheIdentity(scope, value); } catch (Exception) { }
                        QueueNicknameLeaderboardRefresh();
                    }
                    FinishIdentityRequest(success, message);
                });
        }

        void RequestNicknameServerTime(Action<BackendReturnObject> done)
        {
#if UNITY_EDITOR
            if (nicknameTimeForTests != null) { nicknameTimeForTests(done); return; }
            // 자동 테스트에서 실제 사용자 계정으로 요청하지 않는다.
            done(null);
#else
            Backend.Utils.GetServerTime(bro => done(bro));
#endif
        }

        void ReadNicknamePolicy(string owner, long request, long session, Action<MukJumpNicknameChange.State> done, bool allowCreate = true)
        {
#if UNITY_EDITOR
            if (nicknamePolicyReadForTests != null) { nicknamePolicyReadForTests(done); return; }
#endif
            if (settings == null || !settings.HasRequiredRuntimeValues) { done(null); return; }
            RequestMyGameData(settings.PlayerTableName, new Where(), bro =>
            {
                if (!IsIdentityRequestCurrent(request, session, owner)) return;
                if (bro == null || !bro.IsSuccess()) { done(null); return; }
                try
                {
                    var rows = bro.FlattenRows();
                    if (rows == null || !rows.IsArray || rows.Count > 1) { done(null); return; }
                    if (rows.Count == 0)
                    {
                        // 아직 한 판도 없는 계정도 정상 스냅샷을 저장하되 0m 순위는 제출하지 않는다.
                        if (!allowCreate || profileResolutionPending || cloudLoadInFlight || saveInFlight || syncWriteBlocked)
                        { done(null); return; }
                        int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best :
                            UnityEngine.PlayerPrefs.GetInt("MukJump.BestHeight", 0);
                        var snapshot = MukJumpCloudSnapshot.Capture(best, revision, "");
                        if (!snapshot.IsSupported || !PermanentGrowthProfile.IsSupportedCloudJson(snapshot.growthJson))
                        { done(null); return; }
                        RequestInsertGameData(settings.PlayerTableName, ToParam(snapshot), inserted =>
                        {
                            if (!IsIdentityRequestCurrent(request, session, owner)) return;
                            if (inserted == null || !inserted.IsSuccess()) { done(null); return; }
                            ReadNicknamePolicy(owner, request, session, done, allowCreate: false);
                        });
                        return;
                    }
                    var row = rows[0];
                    if (ReadString(row, "owner_inDate") != owner || string.IsNullOrEmpty(ReadString(row, "inDate")) ||
                        string.IsNullOrEmpty(ReadString(row, "updatedAt")))
                    { done(null); return; }
                    if (string.IsNullOrEmpty(rowInDate) && !profileResolutionPending)
                        rowInDate = ReadString(row, "inDate");
                    done(new MukJumpNicknameChange.State { Row = ReadString(row, "inDate"), UpdatedAt = ReadString(row, "updatedAt"),
                        ChangedAt = ReadString(row, MukJumpNicknameChange.ChangedAtColumn),
                        PendingName = ReadString(row, MukJumpNicknameChange.PendingNameColumn),
                        PreviousAt = ReadString(row, MukJumpNicknameChange.PreviousAtColumn) });
                }
                catch (Exception) { done(null); }
            });
        }

        void WriteNicknamePolicy(string owner, MukJumpNicknameChange.State state, Action<bool> done)
        {
#if UNITY_EDITOR
            if (nicknamePolicyWriteForTests != null) { nicknamePolicyWriteForTests(state, done); return; }
#endif
            if (CurrentAccountScope() != owner || string.IsNullOrEmpty(state.Row)) { done(false); return; }
            var param = new Param();
            param.Add(MukJumpNicknameChange.ChangedAtColumn, state.ChangedAt);
            param.Add(MukJumpNicknameChange.PendingNameColumn, state.PendingName);
            param.Add(MukJumpNicknameChange.PreviousAtColumn, state.PreviousAt);
            // 게임 기록 전체를 덮지 않고 계정의 이름 변경 이력만 갱신한다.
            var where = new Where();
            where.Equal("inDate", state.Row);
            if (!string.IsNullOrEmpty(state.ExpectedUpdatedAt)) where.Equal("updatedAt", state.ExpectedUpdatedAt);
            else
            {
                where.Equal(MukJumpNicknameChange.ChangedAtColumn, state.ExpectedChangedAt);
                where.Equal(MukJumpNicknameChange.PendingNameColumn, state.ExpectedPendingName);
            }
            // 다른 기기의 예약/완료가 먼저 반영됐으면 덮어쓰지 않는다.
            Backend.GameData.Update(settings.PlayerTableName, where, param,
                bro => done(bro != null && bro.IsSuccess()));
        }
    }
}
#endif
