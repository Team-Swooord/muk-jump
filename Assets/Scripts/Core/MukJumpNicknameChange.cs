#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Globalization;
using BackEnd;

namespace MukJump.Core
{
    /// 서버에 먼저 변경 슬롯을 예약한다. 응답 유실 때도 재로그인으로 14일 제한을 우회하지 않는다.
    public static class MukJumpNicknameChange
    {
        public const string Hint = "닉네임은 2주에 한 번 변경할 수 있어요";
        public const string WaitMessage = "마지막 닉네임 변경 후 14일이 지나야 변경할 수 있어요";
        public const string Failure = "닉네임을 저장하지 못했어요. 다시 시도해 주세요";
        public const string ChangedAtColumn = "nicknameChangedAtUtc";
        public const string PendingNameColumn = "nicknamePendingName";
        public const string PreviousAtColumn = "nicknamePreviousChangedAtUtc";

        public sealed class State
        {
            public string Row = "";
            public string ChangedAt = "";
            public string PendingName = "";
            public string PreviousAt = "";
            public string UpdatedAt = "";
            public string ExpectedUpdatedAt = "";
            public string ExpectedChangedAt = "";
            public string ExpectedPendingName = "";
        }

        public static bool TryUtc(string value, out DateTimeOffset time) =>
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);

        public static bool CanChange(string changedAt, DateTimeOffset now) =>
            string.IsNullOrEmpty(changedAt) ||
            TryUtc(changedAt, out var changed) && now >= changed && now - changed >= TimeSpan.FromDays(14);

        public static void Run(string name, Func<bool> isCurrent,
            Action<Action<BackendReturnObject>> readTime,
            Action<Action<BackendReturnObject>> readIdentity,
            Action<Action<State>> readState,
            Action<State, Action<bool>> writeState,
            Action<string, Action<BackendReturnObject>> updateName,
            Action<bool, string> completed)
        {
            bool finished = false;
            int step = 0;
            void Finish(bool ok, string message)
            {
                if (finished || !isCurrent()) return;
                finished = true;
                completed(ok, message);
            }
            Action<T> Once<T>(Action<T> action)
            {
                int expected = ++step;
                bool received = false;
                return value =>
                {
                    if (finished || received || step != expected || !isCurrent()) return;
                    received = true;
                    try { action(value); }
                    catch (Exception) { Finish(false, Failure); }
                };
            }
            string Nickname(BackendReturnObject response)
            {
                if (response == null || !response.IsSuccess()) return null;
                var json = response.GetReturnValuetoJSON();
                if (json == null || !json.IsObject || !json.ContainsKey("row")) return null;
                var row = json["row"];
                return row != null && row.IsObject && row.ContainsKey("nickname")
                    ? row["nickname"]?.ToString() ?? "" : "";
            }
            try
            {
                readTime(Once<BackendReturnObject>(timeResult =>
                {
                    var json = timeResult != null && timeResult.IsSuccess() ? timeResult.GetReturnValuetoJSON() : null;
                    if (json == null || !json.IsObject || !json.ContainsKey("utcTime") ||
                        !TryUtc(json["utcTime"]?.ToString(), out var now)) { Finish(false, Failure); return; }
                    readIdentity(Once<BackendReturnObject>(identity =>
                    {
                        string currentName = Nickname(identity);
                        if (currentName == null) { Finish(false, Failure); return; }
                        // 같은 이름 저장·응답 유실 재확인은 변경 주기를 다시 시작하지 않는다.
                        if (currentName == name) { Finish(true, "닉네임을 변경했어요"); return; }
                        readState(Once<State>(state =>
                        {
                            if (state == null) { Finish(false, Failure); return; }
                            bool retry = state.PendingName == name &&
                                TryUtc(state.ChangedAt, out var reservedAt) && now >= reservedAt &&
                                now - reservedAt < TimeSpan.FromDays(14);
                            if (!retry && !CanChange(state.ChangedAt, now)) { Finish(false, WaitMessage); return; }
                            var reservation = new State { Row = state.Row,
                                // 서버 이름이 아직 다르면 이번 재시도가 실제 변경이다. 이전 실패 시각을 재사용하지 않는다.
                                ChangedAt = now.ToString("O", CultureInfo.InvariantCulture),
                                PendingName = name, PreviousAt = retry ? state.PreviousAt : state.ChangedAt,
                                ExpectedUpdatedAt = state.UpdatedAt,
                                ExpectedChangedAt = state.ChangedAt, ExpectedPendingName = state.PendingName };
                            writeState(reservation, Once<bool>(reserved =>
                            {
                                if (!reserved) { Finish(false, Failure); return; }
                                void Reject(string message)
                                {
                                    var rollback = new State { Row = reservation.Row, ChangedAt = reservation.PreviousAt,
                                        ExpectedChangedAt = reservation.ChangedAt, ExpectedPendingName = reservation.PendingName };
                                    writeState(rollback, Once<bool>(restored => Finish(false, restored ? message : Failure)));
                                }
                                void Commit()
                                {
                                    var confirmed = new State { Row = reservation.Row, ChangedAt = reservation.ChangedAt,
                                        ExpectedChangedAt = reservation.ChangedAt, ExpectedPendingName = reservation.PendingName };
                                    writeState(confirmed, Once<bool>(saved => Finish(saved,
                                        saved ? "닉네임을 변경했어요" : Failure)));
                                }
                                updateName(name, Once<BackendReturnObject>(result =>
                                {
                                    if (result != null && result.IsSuccess()) { Commit(); return; }
                                    if (result?.GetStatusCode() == "409")
                                    {
                                        readIdentity(Once<BackendReturnObject>(check =>
                                        {
                                            string confirmed = Nickname(check);
                                            if (confirmed == name) Commit();
                                            else if (confirmed != null) Reject("이미 사용 중인 닉네임이에요");
                                            else Finish(false, Failure);
                                        }));
                                    }
                                    else if (result?.GetStatusCode() == "400") Reject(Failure);
                                    // 타임아웃·5xx는 서버 적용 여부가 불명확하므로 슬롯을 보존한다.
                                    else Finish(false, Failure);
                                }));
                            }));
                        }));
                    }));
                }));
            }
            catch (Exception) { Finish(false, Failure); }
        }
    }
}
#endif
