using System;

namespace MukJump.Core
{
    /// 해금 권한을 저장하지 않는, 이번 세션의 공개 알림만 추적한다.
    public sealed class InkSkinUnlockState
    {
        object owner;
        ulong baseline;
        ulong lobbyPending;
        string runId;
        bool initialized, consumed;

        public static ulong ReadUnlocked()
        {
            ulong mask = 0;
            for (int i = 1; i < InkSkinCatalog.Count; i++)
                if (InkSkinAchievements.IsUnlocked((InkSkinId)i)) mask |= 1UL << i;
            return mask;
        }
        public void Initialize(object account, ulong unlocked)
        {
            owner = account; baseline = unlocked; runId = null;
            lobbyPending = 0;
            initialized = true; consumed = false;
        }
        // 저장이 성공한 새 로비 보상의 알림에서만 예약한다. 서버에서 읽은 기존 보유 목록은 예약하지 않는다.
        public void QueueLobby(object account, ulong reward, ulong unlocked)
        {
            reward &= unlocked & InkSkinCatalog.AvailableRewardMask;
            if (reward == 0) return;
            if (!initialized || !ReferenceEquals(owner, account)) Initialize(account, unlocked & ~reward);
            lobbyPending |= reward & ~baseline;
        }
        public ulong PeekLobby(object account, ulong unlocked) => initialized && ReferenceEquals(owner, account)
            ? lobbyPending & unlocked & InkSkinCatalog.AvailableRewardMask : 0;
        public ulong TakeLobby(object account, ulong unlocked)
        {
            ulong mask = PeekLobby(account, unlocked);
            lobbyPending &= ~mask;
            baseline |= mask;
            return mask;
        }
        public void BeginRun(string id, object account, ulong unlocked)
        {
            if (!initialized || !ReferenceEquals(owner, account)) Initialize(account, unlocked);
            // ScoreManager의 초기화가 늦어도 이미 보유한 최고점 의상을 다시 공개하지 않는다.
            // 로비에서 새로 발견한 비밀 의상은 다음 정산 때 알릴 수 있도록 남긴다.
            for (int i = 1; i < InkSkinCatalog.Count; i++)
                if (InkSkinCatalog.Get((InkSkinId)i).Metric == SkinMetric.Best)
                    baseline |= unlocked & (1UL << i);
            runId = id; consumed = false;
        }
        // 동기 정산 내부의 저장 실패 롤백도 프로필 객체를 교체한다. 이 경로만 같은 계정으로 잇는다.
        public void FollowSettlement(object before, object after)
        {
            if (initialized && ReferenceEquals(owner, before)) owner = after;
        }
        public ulong Take(string id, object account, ulong unlocked, GameOverResult result)
        {
            if (!initialized || consumed || string.IsNullOrEmpty(id) || id != runId ||
                !ReferenceEquals(owner, account) || result.IsGrowthPreview || !result.RewardsAllowed ||
                !result.RecordSaved || !result.GrowthRewardSaved ||
                result.PersistenceState != GameOverPersistenceState.Complete) return 0;
            consumed = true;
            ulong newlyUnlocked = unlocked & ~baseline & InkSkinCatalog.AvailableRewardMask;
            baseline = unlocked;
            lobbyPending = 0;
            return newlyUnlocked;
        }
    }
}
