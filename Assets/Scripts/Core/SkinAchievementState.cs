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
        // 구 저장은 0회로 시작한다. 과거 용꼬리 해금 비트는 그대로 보존한다.
        public int dragonDeaths;
        public long unlockedMask;
        public int gameplayMask;
        // 의상 ID별 정상 정산 최고 고도. -1은 미기록이며 0m와 구분한다.
        // 구 저장에는 이 선택 필드가 없다. 전체 최고점으로 과거 착용 기록을 추정하지 않는다.
        public int[] skinBestHeights = Array.Empty<int>();
        public bool IsValid => (version == 1 || version == 2) && gameplayMask >= 0 && gameplayMask <= 3 && runs >= 0 && best >= 0 && drawnLandings >= 0 &&
            items >= 0 && inkDrops >= 0 && inkDrops <= items && survival >= 0 && clones >= 0 &&
            nightBest >= 0 && noItemBest >= 0 && growth >= 0 && dragonDeaths >= 0 && unlockedMask >= 0 &&
            (unlockedMask >> InkSkinCatalog.Count) == 0 && ValidSkinBestHeights();
        bool ValidSkinBestHeights()
        {
            if (skinBestHeights == null) return true;
            if (skinBestHeights.Length > InkSkinCatalog.Count) return false;
            foreach (int height in skinBestHeights)
                if (height < -1 || height > best) return false;
            return true;
        }
        public int BestHeightForSkin(InkSkinId id) => InkSkinCatalog.IsValid(id) &&
            skinBestHeights != null && (int)id < skinBestHeights.Length ? skinBestHeights[(int)id] : -1;
        void RecordSkinBestHeight(InkSkinId id, int height)
        {
            int previousLength = skinBestHeights?.Length ?? 0;
            if (previousLength < InkSkinCatalog.Count)
            {
                Array.Resize(ref skinBestHeights, InkSkinCatalog.Count);
                for (int i = previousLength; i < skinBestHeights.Length; i++) skinBestHeights[i] = -1;
            }
            skinBestHeights[(int)id] = Math.Max(skinBestHeights[(int)id], height);
        }
        public static bool HasCompletePayload(string json, bool requireDragonDeaths = false)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json,
                "\"skinAchievements\"\\s*:\\s*(\\{[^{}]*\\})");
            if (!match.Success) return false;
            bool hasDragonDeaths = System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"dragonDeaths\"\\s*:");
            if (requireDragonDeaths && !hasDragonDeaths || hasDragonDeaths &&
                !System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"dragonDeaths\"\\s*:\\s*\\d+\\s*[,}]")) return false;
            foreach (string field in new[] { "version", "runs", "best", "drawnLandings", "items", "inkDrops",
                "survival", "clones", "nightBest", "noItemBest", "growth", "unlockedMask" })
                if (!System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"" + field + "\"\\s*:")) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"version\"\\s*:\\s*2\\b") &&
                !System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"gameplayMask\"\\s*:")) return false;
            if (System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value, "\"skinBestHeights\"\\s*:") &&
                !System.Text.RegularExpressions.Regex.IsMatch(match.Groups[1].Value,
                    "\"skinBestHeights\"\\s*:\\s*\\[(?:\\s*-?\\d+\\s*(?:,\\s*-?\\d+\\s*)*)?\\]")) return false;
            return true;
        }
        public void Grant(InkSkinId id) { if (InkSkinCatalog.IsValid(id)) unlockedMask |= 1L << (int)id; }
        public bool Has(InkSkinId id) => InkSkinCatalog.IsValid(id) && (unlockedMask & (1L << (int)id)) != 0;
        public long Progress(InkSkinId id, long distance, int ownedGrowth)
        {
            var d = InkSkinCatalog.Get(id);
            if (d == null) return 0;
            if (Has(id)) return d.Goal;
            // 은퇴한 두건의 보유자는 조건 이전 후 종이 날개를 잃지 않는다.
            if (id == InkSkinId.OrigamiWings && Has(InkSkinId.StormScarf)) return d.Goal;
            return d.Metric switch
            {
                SkinMetric.Best => best, SkinMetric.Runs => runs, SkinMetric.Distance => distance,
                SkinMetric.DrawnLandings => drawnLandings, SkinMetric.Items => items, SkinMetric.InkDrops => inkDrops,
                SkinMetric.Survival => survival, SkinMetric.Clones => clones, SkinMetric.NightBest => nightBest,
                SkinMetric.NoItemBest => noItemBest, SkinMetric.Growth => Math.Max(growth, ownedGrowth),
                SkinMetric.DragonDeath => dragonDeaths, _ => 0,
            };
        }
        public void Settle(int height, float seconds, SkinRunStats stats, long distance, int ownedGrowth)
        {
            runs = Add(runs, 1);
            best = Math.Max(best, height);
            growth = Math.Max(growth, ownedGrowth);
            if (stats.IsMeasured)
            {
                // 현재 미리보기/장착 설정이 아닌, 판 시작 때 확정한 의상에 기록한다.
                if (height >= 0 && seconds >= 0 && !float.IsNaN(seconds) && !float.IsInfinity(seconds))
                    RecordSkinBestHeight(stats.startingSkin, height);
                drawnLandings = Add(drawnLandings, stats.drawnLandings);
                items = Add(items, stats.items);
                inkDrops = Add(inkDrops, stats.inkDrops);
                clones = Math.Max(clones, stats.maxLivingPlayers);
                if (!float.IsInfinity(seconds) && !float.IsNaN(seconds) && seconds >= 0)
                    survival = Math.Max(survival, Mathf.FloorToInt(Mathf.Min(seconds, 86400f)));
                if (stats.startedAtNight) nightBest = Math.Max(nightBest, height);
                if (stats.items == 0) noItemBest = Math.Max(noItemBest, height);
                if (stats.drawnLandings == 0 && height >= 150 && seconds >= 30 && float.IsFinite(seconds))
                    Grant(InkSkinId.InkGhost);
                if (stats.diedToDragon && height >= 0 && seconds >= 0 && float.IsFinite(seconds))
                    dragonDeaths = Add(dragonDeaths, 1);
                if (height == 777) Grant(InkSkinId.LuckyDice);
                if (stats.startedAtNight && stats.startingSkin == InkSkinId.CatEars && stats.items == 0 &&
                    height >= 400 && seconds >= 120 && float.IsFinite(seconds)) Grant(InkSkinId.ClockworkCat);
                unlockedMask |= (long)InkMythicSkins.Evaluate(stats, height, seconds, true);
            }
            for (int i = 1; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                if (!InkSkinCatalog.IsAvailable(id)) continue;
                if (Progress(id, distance, ownedGrowth) >= InkSkinAchievements.Goal(id)) Grant(id);
            }
        }
        public static int Add(int value, int delta) => (int)Math.Min(int.MaxValue, (long)value + Math.Max(0, delta));
    }

    /// 세션 내 비밀 입력은 계정 데이터 객체가 교체되면 버린다. 자동 화면 복원은 입력이 아니다.
    public static class SkinSecrets
    {
        static object owner;
        static int nightChanges, nightEntries;
        static bool? lastNight;
        // 박쥐 그림자는 두 번째 밤의 힌트만 담당한다. 세 번째 밤 해금 이후에는 원래 달로 돌아간다.
        public static bool HasBatMoon => !SkinDebugSession.Active && nightEntries == 2 &&
            ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken);
        static void ResetInputs() { nightChanges = nightEntries = 0; lastNight = null; }
        static bool Ready()
        {
            if (SkinDebugSession.Active) { ResetInputs(); return false; }
            if (PermanentGrowthProfile.RequiresRecovery ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) return false;
            var current = PermanentGrowthProfile.SkinAccountToken;
            if (!ReferenceEquals(current, owner)) { owner = current; ResetInputs(); }
            return true;
        }
        // 달의 복원·중복 호출은 세지 않는다. 저장까지 성공한 새 보상만 알림에 전달한다.
        public static ulong AcceptedNightChange(bool night)
        {
            if (!Ready() || lastNight == night) return 0;
            lastNight = night;
            if (night) nightEntries = Math.Min(3, nightEntries + 1);
            nightChanges = Math.Min(6, nightChanges + 1);
            ulong awarded = 0;
            if (nightEntries >= 3) awarded |= GrantNew(InkSkinId.NightBat);
            if (nightChanges >= 6) awarded |= GrantNew(InkSkinId.Astronaut);
            return awarded;
        }
        static ulong GrantNew(InkSkinId id) => !InkSkinAchievements.IsUnlocked(id) &&
            PermanentGrowthProfile.GrantSkinSecret(id) ? 1UL << (int)id : 0;
        // 던지기 시작 계정과 지금 저장 계정이 같아야 한다. 저장 실패·디버그에는 알리지 않는다.
        public static ulong CompletedThreeCushion(object attemptOwner)
        {
            if (!Ready() || !ReferenceEquals(attemptOwner, PermanentGrowthProfile.SkinAccountToken) ||
                InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion)) return 0;
            ulong reward = 1UL << (int)InkSkinId.ThreeCushion;
            return PermanentGrowthProfile.TryGrantLiveAchievements(attemptOwner, reward, 0) ? reward : 0;
        }
        public static void SelectedByUser(InkSkinId id)
        {
            // 이전 씬/콜백과의 호환만 유지한다. 종이 날개는 현재 생존 시간 조건으로만 새로 해금한다.
        }
    }
}
