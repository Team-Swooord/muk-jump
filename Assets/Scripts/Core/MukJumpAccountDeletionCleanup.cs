#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;

namespace MukJump.Core
{
    /// 뒤끝은 탈퇴 요청 후 정시에 계정·순위를 물리 삭제한다.
    /// 그 전에는 본인 순위를 0으로 무효화하고 게임 정보를 삭제한다.
    /// 삭제 의도는 호출자의 내구 표식으로 유지하며, 재시도는 항상 서버를 다시 읽는다.
    public static class MukJumpAccountDeletionCleanup
    {
        public static void Run(
            string owner,
            Func<bool, bool> isCurrent,
            Action<Action<BackendReturnObject>> readOwnedRows,
            Action<string, Action<BackendReturnObject>> unpublish,
            Action<string, Action<BackendReturnObject>> deleteRow,
            Action<Action<BackendReturnObject>> withdraw,
            Action<BackendReturnObject> completed)
        {
            bool finished = false;
            int step = 0;
            var rows = new List<string>();

            void Finish(BackendReturnObject result, bool withdrawalMayHaveCompleted = false)
            {
                if (finished || !isCurrent(withdrawalMayHaveCompleted)) return;
                finished = true;
                completed(result);
            }

            void Send(Action<Action<BackendReturnObject>> request,
                Action<BackendReturnObject> next, bool isWithdrawal = false)
            {
                if (finished || !isCurrent(false)) return;
                int expectedStep = ++step;
                bool received = false;
                try
                {
                    request(result =>
                    {
                        if (received || finished || expectedStep != step || !isCurrent(isWithdrawal)) return;
                        received = true;
                        try
                        {
                            if (result == null || !result.IsSuccess()) Finish(result, isWithdrawal);
                            else next(result);
                        }
                        catch (Exception) { Finish(null, isWithdrawal); }
                    });
                }
                catch (Exception) { Finish(null, isWithdrawal); }
            }

            void DeleteNext(int index)
            {
                if (index == rows.Count)
                {
                    // SDK는 탈퇴 응답을 전달하기 전에 현재 인증 UID를 지울 수 있다.
                    // 마지막 응답만 빈 UID를 허용하되 요청 세대·삭제 소유권은 유지한다.
                    Send(withdraw, result => Finish(result, true), isWithdrawal: true);
                    return;
                }
                string row = rows[index];
                // 순위 갱신 실패 후 탈퇴하면 인증이 끊겨 재시도할 수 없다.
                // 반드시 무효화 → 게임 정보 삭제 → 탈퇴 순서로 완료한다.
                Send(done => unpublish(row, done), _ =>
                    Send(done => deleteRow(row, done), __ => DeleteNext(index + 1)));
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                Finish(null);
                return;
            }
            Send(readOwnedRows, result =>
            {
                JsonData data = result.FlattenRows();
                if (data == null || !data.IsArray) { Finish(null); return; }
                // 모든 소유권을 먼저 확인한다. 잘못된 응답이면 한 행도 변경하지 않는다.
                for (int i = 0; i < data.Count; i++)
                {
                    JsonData row = data[i];
                    if (row == null || !row.IsObject ||
                        !row.ContainsKey("owner_inDate") ||
                        !string.Equals(row["owner_inDate"]?.ToString(), owner, StringComparison.Ordinal) ||
                        !row.ContainsKey("inDate") || string.IsNullOrWhiteSpace(row["inDate"]?.ToString()))
                    { Finish(null); return; }
                    string id = row["inDate"].ToString();
                    if (!rows.Contains(id)) rows.Add(id);
                }
                DeleteNext(0);
            });
        }
    }
}
#endif
