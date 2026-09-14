using System;
using System.Collections.Generic;
namespace MukJump.Core
{
    [Serializable] public sealed class AppleAchievementProgress
    { public string id; public double percent; }
    public static class InkAchievementCatalog
    {
        public const int Count = InkSkinCatalog.Count + 1;
        // Apple에 등록·전송할 업적은 기본 먹을 제외한 의상 해금과 1:1이다.
        // 게임 내부 벽타기/연속 획득 알림 ID는 기존 저장 호환을 위해 유지한다.
        // 등록 자료에는 신규 의상도 포함하되, 실제 전송은 ASC 등록 확인된 ID까지만 한다.
        public const int GameCenterCount = InkSkinCatalog.Count - 1;
        public const int RegisteredGameCenterCount = 61;
        public static string Id(int index) => index < InkSkinCatalog.Count
            ? $"mukjump.achievement.skin.{index:00}"
            : index == InkSkinCatalog.Count ? "mukjump.achievement.wall" : "mukjump.achievement.flight3";
        public static string Name(int index) => index < InkSkinCatalog.Count
            ? InkSkinCatalog.Name((InkSkinId)index) : index == InkSkinCatalog.Count ? "벽을 타는 먹" : "한 번에 셋";
        public static string Requirement(int index) => index < InkSkinCatalog.Count
            ? InkSkinAchievements.Requirement((InkSkinId)index) : index == InkSkinCatalog.Count
                ? "벽타기로 벽에 붙기" : "먹방울 상승 한 번으로 추가 아이템 3개 획득";
        public static bool Hidden(int index) => index < InkSkinCatalog.Count &&
            InkSkinCatalog.Get((InkSkinId)index).Tier is SkinTier.Hidden or SkinTier.UltraHidden;
        public static List<AppleAchievementProgress> SavedProgress(SkinAchievementState state, long distance)
        {
            var result = new List<AppleAchievementProgress>();
            if (state == null || !state.IsValid) return result;
            for (int i = 1; i <= RegisteredGameCenterCount; i++)
            {
                var id = (InkSkinId)i;
                if (!InkSkinCatalog.IsAvailable(id)) continue;
                double percent = Math.Min(100, Math.Floor(100d * state.Progress(id, distance, state.growth) / InkSkinAchievements.Goal(id)));
                // 0%도 비밀 업적을 공개하므로 잠긴 비밀은 요청에 포함하지 않는다.
                if (percent <= 0 || Hidden(i) && percent < 100) continue;
                result.Add(new AppleAchievementProgress { id = Id(i), percent = percent });
            }
            return result;
        }
    }
    /// GC 계정과 게임 계정의 세대가 모두 맞는 응답만 인정한다. GC 기록을 게임 저장에 역수입하지 않는다.
    public sealed class AppleAchievementSyncState
    {
        object owner;
        string player;
        int sequence;
        readonly Dictionary<string, double> confirmed = new();
        AppleAchievementProgress[] sent;
        public int Pending { get; private set; }
        public bool Loaded { get; private set; }
        public void Bind(string currentPlayer, object currentOwner)
        {
            if (player == currentPlayer && ReferenceEquals(owner, currentOwner)) return;
            player = currentPlayer; owner = currentOwner; Pending = 0; sequence++;
            Loaded = false; sent = null; confirmed.Clear();
        }
        public int Begin(AppleAchievementProgress[] batch = null)
        { sent = batch; return Pending = ++sequence; }
        public void Cancel() { sequence++; Pending = 0; sent = null; }
        public bool Accept(int request, string responsePlayer, bool success, AppleAchievementProgress[] items)
        {
            if (Pending == 0 || request != Pending || responsePlayer != player) return false;
            Pending = 0;
            if (!success) { sent = null; return false; }
            foreach (var item in sent ?? items ?? Array.Empty<AppleAchievementProgress>())
                if (!string.IsNullOrEmpty(item.id) && !double.IsNaN(item.percent) && !double.IsInfinity(item.percent))
                    confirmed[item.id] = Math.Max(confirmed.TryGetValue(item.id, out var value) ? value : 0, Math.Clamp(item.percent, 0, 100));
            Loaded = true; sent = null; return true;
        }
        public AppleAchievementProgress[] Changes(SkinAchievementState state, long distance) =>
            InkAchievementCatalog.SavedProgress(state, distance).FindAll(item =>
                !confirmed.TryGetValue(item.id, out double value) || item.percent > value).ToArray();
    }
}
