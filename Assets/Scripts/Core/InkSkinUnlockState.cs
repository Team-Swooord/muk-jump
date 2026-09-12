using System;

namespace MukJump.Core
{
    /// 해금 권한을 저장하지 않는, 이번 세션의 공개 알림만 추적한다.
    public sealed class InkSkinUnlockState
    {
        object owner;
        ulong baseline;
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
            initialized = true; consumed = false;
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
            ulong newlyUnlocked = unlocked & ~baseline & ~1UL;
            baseline = unlocked;
            return newlyUnlocked;
        }
    }
}
