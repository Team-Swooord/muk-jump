#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using BackEnd;
using UnityEngine;

namespace MukJump.Core
{
    public sealed partial class MukJumpAccountRuntime
    {
        const string RetiringGuestOwnerKey = "MukJump.Account.RetiringGuestOwner";
        const string RetiringGuestTargetKey = "MukJump.Account.RetiringGuestTarget";
        const string RetiringGuestDeletedKey = "MukJump.Account.RetiringGuestDeleted";
        const string GuestRetirementRetry = "게스트 정리를 마무리하려면 같은 Apple 계정으로 다시 로그인해 주세요";
        bool HasPendingGuestRetirement => PlayerPrefs.HasKey(RetiringGuestOwnerKey);
#if UNITY_EDITOR
        Action<string, Action<BackendReturnObject>> retiringGuestLookupForTests;
        Action<string, Func<bool, bool>, Action<BackendReturnObject>> retiringGuestDeleteForTests;
#endif
        bool BeginGuestRetirement()
        {
            if (HasPendingGuestRetirement) return true;
            if (AccountKind != MukJumpAccountKind.BackendGuest || !IsOnlineAuthenticated) return true;
            string owner = CurrentAccountScope();
            if (string.IsNullOrWhiteSpace(owner)) return false;
            try
            {
                PlayerPrefs.SetString(RetiringGuestOwnerKey, owner);
                PlayerPrefs.DeleteKey(RetiringGuestTargetKey);
                PlayerPrefs.DeleteKey(RetiringGuestDeletedKey);
                PlayerPrefs.Save();
                return PlayerPrefs.GetString(RetiringGuestOwnerKey) == owner;
            }
            catch (Exception) { return false; }
        }

        void ClearGuestRetirement()
        {
            PlayerPrefs.DeleteKey(RetiringGuestOwnerKey);
            PlayerPrefs.DeleteKey(RetiringGuestTargetKey);
            PlayerPrefs.DeleteKey(RetiringGuestDeletedKey);
            PlayerPrefs.Save();
        }

        // 기존 Apple 인증이 먼저 성공한 뒤에만 옛 게스트로 잠깐 인증하여 본인 데이터만 정리한다.
        // Apple 토큰은 메모리에만 두며, 종료/타임아웃 시 소유자 표식과 새 Apple 인증으로 재개한다.
        void RetireGuestAfterTargetAuthenticated(string token, FederationType type, MukJumpAccountKind kind)
        {
            string guest = PlayerPrefs.GetString(RetiringGuestOwnerKey, "");
            string target = CurrentAccountScope();
            string savedTarget = PlayerPrefs.GetString(RetiringGuestTargetKey, "");
            if (kind != MukJumpAccountKind.Apple || string.IsNullOrEmpty(guest) ||
                string.IsNullOrEmpty(target) || guest == target ||
                !string.IsNullOrEmpty(savedTarget) && savedTarget != target)
            { EnterProviderResolutionBlock(GuestRetirementRetry); return; }
            PlayerPrefs.SetString(RetiringGuestTargetKey, target);
            PlayerPrefs.Save();
            IsOnlineAuthenticated = false;
            federationRequestInFlight = true;
            long generation = ++federationRequestGeneration;
            int step = 0;
            SetState(MukJumpAccountPhase.Connecting, "임시 게스트 계정과 기록을 정리하는 중");
            bool Current() => this != null && federationRequestInFlight &&
                generation == federationRequestGeneration &&
                PlayerPrefs.GetString(RetiringGuestOwnerKey, "") == guest &&
                PlayerPrefs.GetString(RetiringGuestTargetKey, "") == target;
            void Fail()
            {
                if (!Current()) return;
                federationRequestInFlight = false;
                EnterProviderResolutionBlock(GuestRetirementRetry);
            }
            Action<BackendReturnObject> Next(Action<BackendReturnObject> action)
            {
                int expected = ++step;
                bool received = false;
                federationRequestDeadlineRealtime = Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
                return result =>
                {
                    if (!Current() || received || step != expected) return;
                    received = true;
                    try { action(result); } catch (Exception) { Fail(); }
                };
            }
            void Finish()
            {
                if (!Current() || CurrentAccountScope() != target) { Fail(); return; }
                // 정리되지 않은 백업이 다음 로그아웃/재실행에서 다시 살아나지 않게 한다.
                if (!MukJumpIdentityProfile.TryResetForAccountDeletion(guest)) { Fail(); return; }
                PlayerPrefs.DeleteKey(LocalGuestSnapshotKey);
                ClearPendingLocalGuestImport();
                ClearBackendGuestInfo();
                ClearGuestRetirement();
                federationRequestInFlight = false;
                FinishAuthorizedAccountTransition(kind, true, false, "기존 계정으로 전환했습니다");
            }
            void ReturnToApple()
            {
                RequestFederationAuthorization(token, type, Next(result =>
                {
                    if (result == null || !result.IsSuccess() || CurrentAccountScope() != target) { Fail(); return; }
                    Finish();
                }));
            }
            void MarkDeletedAndReturn()
            {
                PlayerPrefs.SetInt(RetiringGuestDeletedKey, 1);
                PlayerPrefs.Save();
                ReturnToApple();
            }
            try
            {
                if (PlayerPrefs.GetInt(RetiringGuestDeletedKey, 0) != 0) { Finish(); return; }
                LookupRetiringGuest(guest, Next(exists =>
                {
                    // 탈퇴 성공 직후 종료된 경우 다음 정시의 서버 404로 완료를 확인한다.
                    if (exists?.GetStatusCode() == "404")
                    { PlayerPrefs.SetInt(RetiringGuestDeletedKey, 1); PlayerPrefs.Save(); Finish(); return; }
                    if (exists == null || !exists.IsSuccess() || string.IsNullOrEmpty(ReadStoredGuestId())) { Fail(); return; }
                    RequestBackendGuestLogin(Next(login =>
                    {
                        if (login == null || !login.IsSuccess() || CurrentAccountScope() != guest) { Fail(); return; }
                        RequestBackendUserInfo(Next(info =>
                        {
                            var json = info != null && info.IsSuccess() ? info.GetReturnValuetoJSON() : null;
                            var row = json != null && json.IsObject && json.ContainsKey("row") ? json["row"] : null;
                            if (!TryResolveAccountKind(ReadString(row, "subscriptionType"), out var verified) ||
                                verified != MukJumpAccountKind.BackendGuest || CurrentAccountScope() != guest)
                            { Fail(); return; }
                            DeleteRetiringGuest(guest,
                                allowEmpty => Current() && (CurrentAccountScope() == guest ||
                                    allowEmpty && string.IsNullOrEmpty(CurrentAccountScope())),
                                Next(deleted =>
                                {
                                    if (deleted == null || !deleted.IsSuccess()) { Fail(); return; }
                                    MarkDeletedAndReturn();
                                }));
                        }));
                    }));
                }));
            }
            catch (Exception) { Fail(); }
        }

        void LookupRetiringGuest(string guest, Action<BackendReturnObject> done)
        {
#if UNITY_EDITOR
            if (retiringGuestLookupForTests != null) { retiringGuestLookupForTests(guest, done); return; }
            done(null);
#else
            Backend.Social.GetUserInfoByInDate(guest, bro => done(bro));
#endif
        }

        void DeleteRetiringGuest(string guest, Func<bool, bool> current, Action<BackendReturnObject> done)
        {
#if UNITY_EDITOR
            if (retiringGuestDeleteForTests != null) { retiringGuestDeleteForTests(guest, current, done); return; }
#endif
            MukJumpAccountDeletionCleanup.Run(guest, current,
                cb => RequestMyGameData(settings.PlayerTableName, new Where(), cb),
                (row, cb) =>
                {
                    var param = new Param(); param.Add(settings.BestHeightColumn, 0);
                    Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(settings.AllTimeRankUuid,
                        settings.PlayerTableName, row, param, bro => cb(bro));
                },
                (row, cb) => Backend.GameData.DeleteV2(settings.PlayerTableName, row, guest, bro => cb(bro)),
                cb => Backend.BMember.WithdrawAccount(bro => cb(bro)), done);
        }
    }
}
#endif
