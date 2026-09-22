using System;
using UnityEngine;
namespace MukJump.Core
{
    /// 시작 시점 누계와 비교만 하고, 최종 정산 전에는 누계를 증가시키지 않는다.
    public sealed class LiveInkAchievements
    {
        object owner;
        SkinAchievementState baseline;
        ulong observed;
        int observedGameplay;
        public void Begin()
        {
            owner = PermanentGrowthProfile.SkinAccountToken;
            baseline = PermanentGrowthProfile.AchievementSnapshot();
            observed = InkSkinUnlockState.ReadUnlocked();
            observedGameplay = baseline?.gameplayMask ?? 0;
        }
        public static ulong Evaluate(SkinAchievementState start, SkinRunStats stats, int height, float seconds)
        {
            if (start == null || !stats.IsMeasured || height < 0 || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
            ulong mask = InkMythicSkins.Evaluate(stats, height, seconds, false);
            for (int i = 1; i < InkSkinCatalog.Count; i++)
            {
                if (!InkSkinCatalog.IsAvailable((InkSkinId)i)) continue;
                var d = InkSkinCatalog.Get((InkSkinId)i);
                long value = d.Metric switch
                {
                    SkinMetric.Best => Math.Max(start.best, height),
                    SkinMetric.DrawnLandings => (long)start.drawnLandings + stats.drawnLandings,
                    SkinMetric.Items => (long)start.items + stats.items,
                    SkinMetric.InkDrops => (long)start.inkDrops + stats.inkDrops,
                    SkinMetric.Survival => Math.Max(start.survival, Mathf.FloorToInt(Mathf.Clamp(seconds, 0, 86400))),
                    SkinMetric.Clones => Math.Max(start.clones, stats.maxLivingPlayers),
                    SkinMetric.NightBest => Math.Max(start.nightBest, stats.startedAtNight ? height : 0),
                    // 판 종료에 확정되는 무아이템·고도 일치·횟수·거리·히든은 정산에서만 판정한다.
                    _ => -1,
                };
                if (value >= d.Goal) mask |= 1UL << i;
            }
            return mask;
        }
        public void Sample(SkinRunStats stats, int height, float seconds, int gameplay, Action<int> notify)
        {
            if (baseline == null || MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync == true ||
                !ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken)) return;
            ulong fresh = Evaluate(baseline, stats, height, seconds) & ~observed;
            int extra = gameplay & ~observedGameplay & 3;
            if (fresh == 0 && extra == 0) return;
            if (!PermanentGrowthProfile.TryGrantLiveAchievements(owner, fresh, extra)) return;
            observed |= fresh; observedGameplay |= extra;
            for (int i = 1; i < InkSkinCatalog.Count; i++) if ((fresh & (1UL << i)) != 0) notify?.Invoke(i);
            if ((extra & 1) != 0) notify?.Invoke(InkSkinCatalog.Count);
            if ((extra & 2) != 0) notify?.Invoke(InkSkinCatalog.Count + 1);
        }
    }
}
