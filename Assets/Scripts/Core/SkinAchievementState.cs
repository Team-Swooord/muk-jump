using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 결과 정산과 같은 원자적 저장에 포함한다. 분석 수집 동의와 무관하다.
    [Serializable]
    public struct SkinRunStats
    {
        public int version;
        public int drawnLandings, items, inkDrops, maxLivingPlayers;
        public bool startedAtNight;
        // 기존 v1 정산 복구본에서는 누락 시 false. 치명적인 용 접촉을 직접 확인한 판만 인정한다.
        public bool diedToDragon;
        public InkSkinId startingSkin;
        public static SkinRunStats Begin(bool night, InkSkinId skin) => new()
        { version = 1, startedAtNight = night, startingSkin = skin, maxLivingPlayers = 1 };
        public bool IsMeasured => version == 1 && drawnLandings >= 0 && items >= 0 && inkDrops >= 0 &&
            inkDrops <= items && maxLivingPlayers >= 1 && InkSkinCatalog.IsValid(startingSkin);
        public static bool HasCompletePayload(string json)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json,
                "\"skinStats\"\\s*:\\s*(\\{[^{}]*\\})");
            if (!match.Success) return false;
            foreach (string field in new[] { "version", "drawnLandings", "items", "inkDrops", "maxLivingPlayers", "startedAtNight", "startingSkin" })
                if (!System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"" + field + "\"\\s*:")) return false;
            return true;
        }
    }

    [Serializable]
    public sealed class SkinAchievementState
    {
        public int version = 2;
        public int runs, best, drawnLandings, items, inkDrops, survival, clones, nightBest, noItemBest, growth;
        public long unlockedMask;
        public int gameplayMask;
        public bool IsValid => (version == 1 || version == 2) && gameplayMask >= 0 && gameplayMask <= 3 && runs >= 0 && best >= 0 && drawnLandings >= 0 &&
            items >= 0 && inkDrops >= 0 && inkDrops <= items && survival >= 0 && clones >= 0 &&
            nightBest >= 0 && noItemBest >= 0 && growth >= 0 && unlockedMask >= 0 &&
            (unlockedMask >> InkSkinCatalog.Count) == 0;
        public static bool HasCompletePayload(string json)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json,
                "\"skinAchievements\"\\s*:\\s*(\\{[^{}]*\\})");
            if (!match.Success) return false;
            foreach (string field in new[] { "version", "runs", "best", "drawnLandings", "items", "inkDrops",
                "survival", "clones", "nightBest", "noItemBest", "growth", "unlockedMask" })
                if (!System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"" + field + "\"\\s*:")) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"version\"\\s*:\\s*2\\b") &&
                !System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"gameplayMask\"\\s*:")) return false;
            return true;
        }
        public void Grant(InkSkinId id) { if (InkSkinCatalog.IsValid(id)) unlockedMask |= 1L << (int)id; }
        public bool Has(InkSkinId id) => InkSkinCatalog.IsValid(id) && (unlockedMask & (1L << (int)id)) != 0;
        public long Progress(InkSkinId id, long distance, int ownedGrowth)
        {
            var d = InkSkinCatalog.Get(id);
            if (d == null) return 0;
            if (Has(id)) return d.Goal;
            return d.Metric switch
            {
                SkinMetric.Best => best, SkinMetric.Runs => runs, SkinMetric.Distance => distance,
                SkinMetric.DrawnLandings => drawnLandings, SkinMetric.Items => items, SkinMetric.InkDrops => inkDrops,
                SkinMetric.Survival => survival, SkinMetric.Clones => clones, SkinMetric.NightBest => nightBest,
                SkinMetric.NoItemBest => noItemBest, SkinMetric.Growth => Math.Max(growth, ownedGrowth), _ => 0,
            };
        }
        public void Settle(int height, float seconds, SkinRunStats stats, long distance, int ownedGrowth)
        {
            runs = Add(runs, 1);
            best = Math.Max(best, height);
            growth = Math.Max(growth, ownedGrowth);
            if (stats.IsMeasured)
            {
                drawnLandings = Add(drawnLandings, stats.drawnLandings);
                items = Add(items, stats.items);
                inkDrops = Add(inkDrops, stats.inkDrops);
                clones = Math.Max(clones, stats.maxLivingPlayers);
                if (!float.IsInfinity(seconds) && !float.IsNaN(seconds) && seconds >= 0)
                    survival = Math.Max(survival, Mathf.FloorToInt(Mathf.Min(seconds, 86400f)));
                if (stats.startedAtNight) nightBest = Math.Max(nightBest, height);
                if (stats.items == 0) noItemBest = Math.Max(noItemBest, height);
                if (stats.drawnLandings == 0) Grant(InkSkinId.InkGhost);
                if (stats.diedToDragon) Grant(InkSkinId.DragonTail);
                if (height == 777) Grant(InkSkinId.LuckyDice);
                if (stats.startedAtNight && stats.startingSkin == InkSkinId.CatEars && stats.items == 0 &&
                    seconds >= 60 && !float.IsInfinity(seconds)) Grant(InkSkinId.ClockworkCat);
            }
            for (int i = 1; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                if (Progress(id, distance, ownedGrowth) >= InkSkinAchievements.Goal(id)) Grant(id);
            }
        }
        public static int Add(int value, int delta) => (int)Math.Min(int.MaxValue, (long)value + Math.Max(0, delta));
    }

    /// 세션 내 비밀 입력은 계정 데이터 객체가 교체되면 버린다. 자동 화면 복원은 입력이 아니다.
    public static class SkinSecrets
    {
        static object owner;
        static int nightChanges, selectionStep;
        static readonly InkSkinId[] sequence = { InkSkinId.Ink, InkSkinId.Gat, InkSkinId.Ink, InkSkinId.SpiderMask, InkSkinId.Ink };
        static bool Ready()
        {
            if (SkinDebugSession.Active) { nightChanges = selectionStep = 0; return false; }
            if (PermanentGrowthProfile.RequiresRecovery ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) return false;
            var current = PermanentGrowthProfile.SkinAccountToken;
            if (!ReferenceEquals(current, owner)) { owner = current; nightChanges = selectionStep = 0; }
            return true;
        }
        public static void AcceptedNightChange(bool night)
        {
            if (!Ready()) return;
            if (night) PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat);
            if (++nightChanges >= 6) PermanentGrowthProfile.GrantSkinSecret(InkSkinId.Astronaut);
        }
        public static void SelectedByUser(InkSkinId id)
        {
            if (!Ready()) return;
            selectionStep = sequence[selectionStep] == id ? selectionStep + 1 : id == sequence[0] ? 1 : 0;
            if (selectionStep == sequence.Length) { selectionStep = 0; PermanentGrowthProfile.GrantSkinSecret(InkSkinId.OrigamiWings); }
        }
    }
}
