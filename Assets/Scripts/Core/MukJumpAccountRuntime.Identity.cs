#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using BackEnd;
using UnityEngine;

namespace MukJump.Core
{
    public sealed partial class MukJumpAccountRuntime
    {
        bool identityLoaded;
        string identityLoadedScope = string.Empty;
        string identityNickname = string.Empty;
        string identityStatus = string.Empty;
        bool identityBusy;
        float displayIdentityRetryAt;
        long identityRequest;
        float identityDeadline;
        Action<bool, string> identityCompletion;
#if UNITY_EDITOR
        // 실제 계정·서버와 격리된 테스트의 경계.
        Action<Action<BackendReturnObject>> identityInfoForTests;
        Action<string, Action<BackendReturnObject>> nicknameUpdateForTests;
        Func<string> backendUidForTests;
#endif

        string IdentityScope => AccountKind == MukJumpAccountKind.LocalGuest
            ? MukJumpIdentityProfile.LocalScope
            : IsOnlineAuthenticated ? CurrentAccountScope()
            : PlayerPrefs.GetString(StoredAccountScopeKey, string.Empty);

        public string Nickname
        {
            get
            {
                string scope = IdentityScope;
                if (scope == MukJumpIdentityProfile.LocalScope) return MukJumpIdentityProfile.GuestNickname;
                // 저장된 계정 이름은 인증 증거가 아니다. 삭제·만료된 계정의
                // 이전 이름을 새 실행에서 연동 완료처럼 표시하지 않는다.
                if (!IsOnlineAuthenticated) return MukJumpIdentityProfile.GuestNickname;
                string cached = identityLoaded && identityLoadedScope == scope
                    ? identityNickname : MukJumpIdentityProfile.ReadNickname(scope);
                if (AccountKind == MukJumpAccountKind.BackendGuest &&
                    (!MukJumpIdentityProfile.IsGeneratedNickname(cached) || cached.Length > MukJumpIdentityProfile.MaxNicknameLength))
                    return MukJumpIdentityProfile.GuestNickname;
                if (!string.IsNullOrWhiteSpace(cached)) return cached;
                return AccountKind == MukJumpAccountKind.BackendGuest
                    ? MukJumpIdentityProfile.GuestNickname : string.Empty;
            }
        }
        public string NicknameStatus => identityStatus;
        // 표시용 짧은 숫자 UID다. 로그인 계정 소유권은 계속 UserInDate로 판단한다.
        public string BackendUid
        {
            get
            {
                if (AccountKind == MukJumpAccountKind.LocalGuest || !IsOnlineAuthenticated) return string.Empty;
                string scope = IdentityScope;
                if (string.IsNullOrWhiteSpace(scope)) return string.Empty;
                string live = IsOnlineAuthenticated ? ReadNativeBackendUid() : string.Empty;
                return !string.IsNullOrEmpty(live) ? live :
                    NormalizeBackendUid(MukJumpIdentityProfile.ReadUid(scope));
            }
        }
        public bool IsNicknameBusy => identityBusy;
        public bool NeedsNicknameSetup => IsOnlineAuthenticated && AccountKind == MukJumpAccountKind.Apple &&
            identityLoaded && identityLoadedScope == CurrentAccountScope() &&
            (string.IsNullOrEmpty(identityNickname) || MukJumpIdentityProfile.IsGeneratedNickname(identityNickname));
        public bool CanChangeNickname => !identityBusy && !backendProviderVerificationInFlight &&
            !cloudLoadInFlight && !saveInFlight &&
            !guestLoginInFlight && !HasPendingAuthorizedTransition &&
            !localLogoutCleanupPending &&
            !accountDeletionCleanupPending && !federationRequestInFlight && !providerResolutionBlocked &&
            !temporaryBackendPause && Phase != MukJumpAccountPhase.Connecting &&
            Phase != MukJumpAccountPhase.Deleting && !HasPendingAccountConflict &&
            IsOnlineAuthenticated && AccountKind == MukJumpAccountKind.Apple;

        string ReadDisplayPlayerId()
        {
            if (AccountKind == MukJumpAccountKind.LocalGuest) return MukJumpIdentityProfile.LocalUid;
            string uid = BackendUid;
            return !string.IsNullOrEmpty(uid) ? uid :
                AccountKind == MukJumpAccountKind.BackendGuest ? MukJumpIdentityProfile.LocalUid : string.Empty;
        }

        string ReadNativeBackendUid()
        {
#if UNITY_EDITOR
            if (backendUidForTests != null) return NormalizeBackendUid(backendUidForTests());
#endif
            return NormalizeBackendUid(Backend.UID);
        }

        static string NormalizeBackendUid(string value)
        {
            value = value?.Trim() ?? string.Empty;
            foreach (char c in value) if (c < '0' || c > '9') return string.Empty;
            return value;
        }

        public void RefreshDisplayIdentity()
        {
#if UNITY_EDITOR
            // 에디터 표시·자동 UI 검사는 명시적 테스트 응답 없이 서버에 접속하지 않는다.
            if (identityInfoForTests == null) return;
#endif
            if (!IsOnlineAuthenticated)
            {
                TryReconnectGuest();
                return;
            }
            bool verifiedIdentity = identityLoaded && identityLoadedScope == CurrentAccountScope();
            bool guestNameMissing = AccountKind == MukJumpAccountKind.BackendGuest &&
                (!MukJumpIdentityProfile.IsGeneratedNickname(identityNickname) ||
                 identityNickname.Length > MukJumpIdentityProfile.MaxNicknameLength);
            if (AccountKind == MukJumpAccountKind.LocalGuest || identityBusy || temporaryBackendPause ||
                Phase == MukJumpAccountPhase.Connecting || Phase == MukJumpAccountPhase.Deleting ||
                HasPendingAuthorizedTransition ||
                backendProviderVerificationInFlight || federationRequestInFlight ||
                localLogoutCleanupPending || accountDeletionCleanupPending ||
                (verifiedIdentity && !guestNameMissing && !string.IsNullOrEmpty(BackendUid)) ||
                Time.realtimeSinceStartup < displayIdentityRetryAt)
                return;
            // 설정의 매 프레임 갱신이 서버 요청을 반복하지 않도록 제한한다.
            displayIdentityRetryAt = Time.realtimeSinceStartup + 5f;
            RefreshAuthenticatedIdentity();
        }

        void RefreshAuthenticatedIdentity()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && identityInfoForTests == null) return;
#endif
            if (!IsOnlineAuthenticated || identityBusy) return;
            displayIdentityRetryAt = Time.realtimeSinceStartup + 5f;
            string scope = CurrentAccountScope();
            long session = accountSessionGeneration;
            long request = BeginIdentityRequest(null);
            identityLoaded = false;
            try
            {
                MukJumpIdentityProfile.SaveUid(scope, ReadNativeBackendUid());
                RequestIdentityInfo(bro =>
                {
                    if (!IsIdentityRequestCurrent(request, session, scope)) return;
                    if (bro == null || !bro.IsSuccess())
                    {
                        FinishIdentityRequest(false, "닉네임을 확인하지 못했어요. 다시 시도해 주세요");
                        return;
                    }
                    try
                    {
                        var root = bro.GetReturnValuetoJSON();
                        var row = root != null && root.IsObject && root.ContainsKey("row") ? root["row"] : null;
                        if (row == null) throw new InvalidOperationException("사용자 정보가 비어 있습니다.");
                        CacheIdentity(scope, ReadString(row, "nickname"));
                        FinishIdentityRequest(true, string.Empty);
                        if (AccountKind == MukJumpAccountKind.BackendGuest &&
                            (!MukJumpIdentityProfile.IsGeneratedNickname(identityNickname) ||
                              identityNickname.Length > MukJumpIdentityProfile.MaxNicknameLength))
                            SaveNicknameOnServer(MukJumpIdentityProfile.GuestNickname, null, 0);
                    }
                    catch (Exception)
                    {
                        FinishIdentityRequest(false, "닉네임을 확인하지 못했어요. 다시 시도해 주세요");
                    }
                });
            }
            catch (Exception)
            {
                FinishIdentityRequest(false, "닉네임을 확인하지 못했어요. 다시 시도해 주세요");
            }
        }

        public void ChangeNickname(string input, Action<bool, string> completed)
        {
            if (!MukJumpIdentityProfile.TryNormalizeNickname(input, out string value, out string error))
            { completed?.Invoke(false, error); return; }
            if (!CanChangeNickname)
            { completed?.Invoke(false, "계정 연결을 확인한 뒤 다시 시도해 주세요"); return; }
            if (AccountKind == MukJumpAccountKind.LocalGuest)
            {
                try
                {
                    MukJumpIdentityProfile.SaveNickname(MukJumpIdentityProfile.LocalScope, value);
                    completed?.Invoke(true, "닉네임을 변경했어요");
                    NotifyStateChangedSafely();
                }
                catch (Exception) { completed?.Invoke(false, "닉네임을 저장하지 못했어요. 다시 시도해 주세요"); }
                return;
            }
            ChangeNicknameWithCooldown(value, completed);
        }

        void SaveNicknameOnServer(string value, Action<bool, string> completed, int guestAttempt)
        {
            string scope = CurrentAccountScope();
            long session = accountSessionGeneration;
            if (!IsOnlineAuthenticated || string.IsNullOrWhiteSpace(scope))
            { completed?.Invoke(false, "계정 연결을 확인한 뒤 다시 시도해 주세요"); return; }
            if (identityLoaded && identityLoadedScope == scope && identityNickname == value)
            { completed?.Invoke(true, "닉네임을 변경했어요"); return; }
            long request = BeginIdentityRequest(completed);
            try
            {
                RequestNicknameUpdate(value, bro =>
                {
                    if (!IsIdentityRequestCurrent(request, session, scope)) return;
                    if (bro != null && bro.IsSuccess())
                    {
                        try { CacheIdentity(scope, value); }
                        catch (Exception) { /* 서버가 원본이다. 다음 로그인에 표시 캐시를 복구한다. */ }
                        QueueNicknameLeaderboardRefresh();
                        FinishIdentityRequest(true, "닉네임을 변경했어요");
                        return;
                    }
                    if (bro != null && bro.GetStatusCode() == "409")
                    {
                        // 시간 초과 후 재시도에서 이미 내 이름이 됐으면 중복 오류로 남기지 않는다.
                        RequestIdentityInfo(info =>
                        {
                            if (!IsIdentityRequestCurrent(request, session, scope)) return;
                            try
                            {
                                var root = info != null && info.IsSuccess() ? info.GetReturnValuetoJSON() : null;
                                var row = root != null && root.IsObject && root.ContainsKey("row") ? root["row"] : null;
                                if (row != null && ReadString(row, "nickname") == value)
                                {
                                    CacheIdentity(scope, value);
                                    QueueNicknameLeaderboardRefresh();
                                    FinishIdentityRequest(true, "닉네임을 변경했어요");
                                    return;
                                }
                            }
                            catch (Exception) { }
                            if (guestAttempt >= 0 && guestAttempt < 3)
                            {
                                FinishIdentityRequest(false, string.Empty);
                                SaveNicknameOnServer(MukJumpIdentityProfile.CreateGuestNickname(Guid.NewGuid().ToString("N")), null, guestAttempt + 1);
                            }
                            else FinishIdentityRequest(false, "이미 사용 중인 닉네임이에요");
                        });
                    }
                    else FinishIdentityRequest(false, "닉네임을 저장하지 못했어요. 다시 시도해 주세요");
                });
            }
            catch (Exception)
            { FinishIdentityRequest(false, "닉네임을 저장하지 못했어요. 다시 시도해 주세요"); }
        }

        void QueueNicknameLeaderboardRefresh()
        {
            // 검증된 게임 기록 저장 → 리더보드 갱신 경로를 공유한다.
            // 기록 복구 중에는 기존 저장 잠금을 유지하고, 새 이름만으로 0m를 제출하지 않는다.
            if (settings != null && settings.HasRequiredRuntimeValues && IsOnlineAuthenticated)
                MarkDirty();
        }

        void CacheIdentity(string scope, string nickname)
        {
            identityLoaded = true;
            identityLoadedScope = scope;
            identityNickname = nickname ?? string.Empty;
            MukJumpIdentityProfile.SaveUid(scope, ReadNativeBackendUid());
            MukJumpIdentityProfile.SaveNickname(scope, identityNickname);
        }

        long BeginIdentityRequest(Action<bool, string> completed)
        {
            identityBusy = true;
            identityStatus = string.Empty;
            identityDeadline = Time.realtimeSinceStartup + AccountRequestWatchdogSeconds;
            identityCompletion = completed;
            return ++identityRequest;
        }
        bool IsIdentityRequestCurrent(long request, long session, string scope) =>
            this != null && identityBusy && request == identityRequest &&
            IsCurrentAccountSession(session, scope);
        void FinishIdentityRequest(bool success, string message)
        {
            identityBusy = false;
            identityStatus = message;
            var callback = identityCompletion;
            identityCompletion = null;
            callback?.Invoke(success, message);
            NotifyStateChangedSafely();
        }
        void CancelIdentityRequest()
        {
            displayIdentityRetryAt = 0f;
            identityRequest++;
            identityLoaded = false;
            identityLoadedScope = string.Empty;
            identityNickname = string.Empty;
            identityBusy = false;
            identityCompletion = null;
        }
        void PollIdentityRequest()
        {
            if (identityBusy && Time.realtimeSinceStartup >= identityDeadline)
            {
                identityRequest++;
                FinishIdentityRequest(false, "닉네임 확인 시간이 초과됐어요. 다시 시도해 주세요");
            }
            // UID 수신 여부와 닉네임 확인 성공은 별개다. 최초 조회가 실패해도
            // 계정 창을 다시 열거나 앱을 재실행할 필요 없이 제한된 간격으로 복구한다.
            if (!identityLoaded || (AccountKind == MukJumpAccountKind.BackendGuest &&
                (!MukJumpIdentityProfile.IsGeneratedNickname(identityNickname) ||
                 identityNickname.Length > MukJumpIdentityProfile.MaxNicknameLength)))
                RefreshDisplayIdentity();
        }
        void RequestIdentityInfo(Action<BackendReturnObject> completed)
        {
#if UNITY_EDITOR
            if (identityInfoForTests != null) { identityInfoForTests(completed); return; }
#endif
            Backend.BMember.GetUserInfo(bro => completed(bro));
        }
        void RequestNicknameUpdate(string value, Action<BackendReturnObject> completed)
        {
#if UNITY_EDITOR
            if (nicknameUpdateForTests != null) { nicknameUpdateForTests(value, completed); return; }
#endif
            Backend.BMember.UpdateNickname(value, bro => completed(bro));
        }
    }
}
#endif
