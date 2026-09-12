using System;
using UnityEngine;
namespace MukJump.Core
{
    public enum InkSkinId { Ink = 0, Gat = 1, SpiderMask = 2, FirstRibbon = 3, Scholar = 4, InkGhost = 5, BambooHat = 6, RedHeadband = 7, LotusCrown = 8, CatEars = 9, BellRibbon = 10, CloudCap = 11, PaperBoat = 12, TeaCupHat = 13, FeatherHairpin = 14, PeachFlower = 15, FoxMask = 16, CraneCrest = 17, YakshaMask = 18, TurtleShell = 19, StormScarf = 20, MoonCrown = 21, BrushMaster = 22, SpiritHalo = 23, KoiMask = 24, JadeCrown = 25, PhoenixCrown = 26, TigerMask = 27, MountainKing = 28, CloudDragon = 29, InkMecha = 30, NightBat = 31, Astronaut = 32, ClockworkCat = 33, LuckyDice = 34, OrigamiWings = 35 }
    public enum SkinTier { Basic, Easy, Medium, Hard, Hidden, UltraHidden }
    public enum SkinMetric { None, Best, Runs, Distance, DrawnLandings, InkDrops, Items, Survival, Clones, NightBest, Growth, NoItemBest, ZeroRun, Secret }
    public sealed class InkSkinDefinition
    {
        public readonly InkSkinId Id;
        public readonly string Korean, English, Japanese, Asset;
        public readonly SkinTier Tier;
        public readonly SkinMetric Metric;
        public readonly int Goal;
        public InkSkinDefinition(InkSkinId id, string ko, string en, string ja, string asset, SkinTier tier, SkinMetric metric, int goal)
        { Id=id; Korean=ko; English=en; Japanese=ja; Asset=asset; Tier=tier; Metric=metric; Goal=goal; }
    }
    public static class InkSkinCatalog
    {
        public const int Count = 36;
        public const float AirJumpRatio = .4f;
        static readonly InkSkinDefinition[] definitions =
        {
            new(InkSkinId.Ink,"먹방울","Ink","墨","",SkinTier.Basic,SkinMetric.None,0),
            new(InkSkinId.Gat,"바람 갓","Wind Gat","風の笠","skin_gat_v1",SkinTier.Basic,SkinMetric.Best,100),
            new(InkSkinId.SpiderMask,"거미탈","Spider Mask","蜘蛛の面","skin_spider_mask_v1",SkinTier.Basic,SkinMetric.Runs,10),
            new(InkSkinId.FirstRibbon,"첫 먹매듭","First Knot","はじめの墨結び","skin_first_ribbon_v1",SkinTier.Basic,SkinMetric.Runs,1),
            new(InkSkinId.Scholar,"선비 관","Scholar Cap","書生の冠","skin_scholar_v1",SkinTier.Basic,SkinMetric.Distance,500),
            new(InkSkinId.InkGhost,"빈 먹 유령","Blank Ghost","空っぽの墨おばけ","skin_ink_ghost_v1",SkinTier.Hidden,SkinMetric.ZeroRun,1),
            new(InkSkinId.BambooHat,"대나무 삿갓","Bamboo Hat","竹笠","skin_bamboo_hat_v1",SkinTier.Easy,SkinMetric.Best,25),
            new(InkSkinId.RedHeadband,"수련 머리띠","Training Band","修行の鉢巻","skin_red_headband_v1",SkinTier.Easy,SkinMetric.DrawnLandings,10),
            new(InkSkinId.LotusCrown,"연꽃 관","Lotus Crown","蓮の冠","skin_lotus_crown_v1",SkinTier.Easy,SkinMetric.Runs,3),
            new(InkSkinId.CatEars,"고양이 귀","Cat Ears","猫耳","skin_cat_ears_v1",SkinTier.Easy,SkinMetric.InkDrops,1),
            new(InkSkinId.BellRibbon,"방울 매듭","Bell Knot","鈴結び","skin_bell_ribbon_v1",SkinTier.Easy,SkinMetric.Items,5),
            new(InkSkinId.CloudCap,"구름 두건","Cloud Hood","雲の頭巾","skin_cloud_cap_v1",SkinTier.Easy,SkinMetric.Best,50),
            new(InkSkinId.PaperBoat,"종이배 모자","Paper Boat","紙舟の帽子","skin_paper_boat_v1",SkinTier.Easy,SkinMetric.Distance,1000),
            new(InkSkinId.TeaCupHat,"찻잔 모자","Tea Cup Hat","湯のみ帽","skin_tea_cup_hat_v1",SkinTier.Easy,SkinMetric.Survival,45),
            new(InkSkinId.FeatherHairpin,"깃털 비녀","Feather Pin","羽のかんざし","skin_feather_hairpin_v1",SkinTier.Easy,SkinMetric.DrawnLandings,25),
            new(InkSkinId.PeachFlower,"복사꽃","Peach Blossom","桃の花","skin_peach_flower_v1",SkinTier.Easy,SkinMetric.Runs,5),
            new(InkSkinId.FoxMask,"여우탈","Fox Mask","狐の面","skin_fox_mask_v1",SkinTier.Medium,SkinMetric.Best,250),
            new(InkSkinId.CraneCrest,"학의 볏","Crane Crest","鶴の冠","skin_crane_crest_v1",SkinTier.Medium,SkinMetric.Distance,5000),
            new(InkSkinId.YakshaMask,"야차탈","Yaksha Mask","夜叉の面","skin_yaksha_mask_v1",SkinTier.Medium,SkinMetric.Best,500),
            new(InkSkinId.TurtleShell,"거북 투구","Turtle Helm","亀のかぶと","skin_turtle_shell_v1",SkinTier.Medium,SkinMetric.Clones,3),
            new(InkSkinId.StormScarf,"질풍 두건","Gale Scarf","疾風の頭巾","skin_storm_scarf_v1",SkinTier.Medium,SkinMetric.Survival,120),
            new(InkSkinId.MoonCrown,"달빛 관","Moon Crown","月光の冠","skin_moon_crown_v1",SkinTier.Medium,SkinMetric.NightBest,100),
            new(InkSkinId.BrushMaster,"붓 장인","Brush Master","筆の匠","skin_brush_master_v1",SkinTier.Medium,SkinMetric.DrawnLandings,100),
            new(InkSkinId.SpiritHalo,"도깨비 불","Spirit Halo","鬼火の輪","skin_spirit_halo_v1",SkinTier.Medium,SkinMetric.Items,30),
            new(InkSkinId.KoiMask,"잉어탈","Koi Mask","鯉の面","skin_koi_mask_v1",SkinTier.Medium,SkinMetric.Growth,5),
            new(InkSkinId.JadeCrown,"비취 용관","Jade Crown","翡翠の竜冠","skin_jade_crown_v1",SkinTier.Medium,SkinMetric.Distance,10000),
            new(InkSkinId.PhoenixCrown,"봉황 관","Phoenix Crown","鳳凰の冠","skin_phoenix_crown_v1",SkinTier.Hard,SkinMetric.Best,1000),
            new(InkSkinId.TigerMask,"산군 탈","Tiger Mask","山の虎面","skin_tiger_mask_v1",SkinTier.Hard,SkinMetric.NoItemBest,300),
            new(InkSkinId.MountainKing,"산왕 관","Mountain Crown","山王の冠","skin_mountain_king_v1",SkinTier.Hard,SkinMetric.Best,1500),
            new(InkSkinId.CloudDragon,"구름 용","Cloud Dragon","雲の竜","skin_cloud_dragon_v1",SkinTier.Hard,SkinMetric.Best,2000),
            new(InkSkinId.InkMecha,"먹빛 기갑","Ink Mecha","墨の機甲","skin_ink_mecha_v1",SkinTier.Hard,SkinMetric.Runs,100),
            new(InkSkinId.NightBat,"밤의 파수꾼","Night Watcher","夜の番人","skin_night_bat_v1",SkinTier.UltraHidden,SkinMetric.Secret,1),
            new(InkSkinId.Astronaut,"달 여행자","Moon Traveler","月の旅人","skin_astronaut_v1",SkinTier.UltraHidden,SkinMetric.Secret,1),
            new(InkSkinId.ClockworkCat,"태엽 고양이","Clockwork Cat","ぜんまい猫","skin_clockwork_cat_v1",SkinTier.UltraHidden,SkinMetric.Secret,1),
            new(InkSkinId.LuckyDice,"일곱 번째 눈","Seventh Eye","七つ目の目","skin_lucky_dice_v1",SkinTier.UltraHidden,SkinMetric.Secret,1),
            new(InkSkinId.OrigamiWings,"접힌 차원","Folded Dimension","折りたたむ次元","skin_origami_wings_v1",SkinTier.UltraHidden,SkinMetric.Secret,1)
        };
        public static bool IsValid(InkSkinId id) => (int)id >= 0 && (int)id < Count;
        public static InkSkinId Normalize(InkSkinId id) => IsValid(id) ? id : InkSkinId.Ink;
        public static InkSkinDefinition Get(InkSkinId id) => IsValid(id) ? definitions[(int)id] : null;
        public static bool IsSignatureCostume(InkSkinId id) => id is InkSkinId.CloudDragon or InkSkinId.InkMecha or InkSkinId.ClockworkCat;
        public static bool IsConcealed(InkSkinId id) => (Get(id)?.Tier is SkinTier.Hidden or SkinTier.UltraHidden) && !InkSkinAchievements.IsUnlocked(id);
        public static string Name(InkSkinId id) => Get(id)?.Korean ?? "???";
        public static string Ability(InkSkinId id) => id switch {
            InkSkinId.Gat => "이단 도약", InkSkinId.SpiderMask => "거미 걸음",
            _ => "먹의 장식" };
        public static string Description(InkSkinId id) => id switch {
            InkSkinId.Gat => "점프 정점에서 한 번 더 도약해요.\n첫 도약의 40% 힘으로 자동 발동해요.",
            InkSkinId.SpiderMask => "양옆 벽에 붙었다가\n화면 안쪽으로 자동 도약해요.",
            _ => "원래 먹에 장식을 더해요.\n기존 점프와 성장 효과를 유지해요." };
        public static string ResourcePath(InkSkinId id) => Get(id) is { Asset: { Length: > 0 } } d ? "MukJump/Skins/" + d.Asset : "";
        public static bool BehindBody(InkSkinId id) => id == InkSkinId.OrigamiWings;
        public static float Width(InkSkinId id) => IsSignatureCostume(id) ? .75f : id switch {
            InkSkinId.Gat or InkSkinId.BambooHat => .9f,
            InkSkinId.SpiderMask or InkSkinId.FoxMask or InkSkinId.YakshaMask or InkSkinId.KoiMask or InkSkinId.TigerMask or InkSkinId.NightBat => .55f,
            InkSkinId.InkGhost => .62f, InkSkinId.BellRibbon => .38f,
            InkSkinId.OrigamiWings => 1.08f, InkSkinId.Astronaut => .85f, _ => .68f };
        public static Vector2 Offset(InkSkinId id) => IsSignatureCostume(id) ? new Vector2(0,.35f) : new Vector2(0, id switch {
            InkSkinId.OrigamiWings => -.015f, InkSkinId.InkGhost => .45f, InkSkinId.Astronaut => .085f,
            InkSkinId.RedHeadband => .27f, InkSkinId.BellRibbon => .38f, _ => .4f });
        static readonly System.Collections.Generic.Dictionary<InkSkinId, Sprite[]> sprites = new();
        public static Sprite Sprite(InkSkinId id, int frame = 0)
        {
            if (id == InkSkinId.Ink || !IsValid(id)) return null;
            if (!sprites.TryGetValue(id, out var frames))
            {
                frames = Resources.LoadAll<Sprite>(ResourcePath(id));
                Array.Sort(frames, (a,b) => string.CompareOrdinal(a.name,b.name));
                if (frames.Length == 1) sprites[id] = frames;
                else return null;
            }
            return frames[Mathf.Clamp(frame, 0, frames.Length-1)];
        }
    }
    public static class InkSkinAchievements
    {
        public static int Goal(InkSkinId id) => InkSkinCatalog.Get(id)?.Goal ?? int.MaxValue;
        public static int Progress(InkSkinId id) => (int)Math.Min(Goal(id), PermanentGrowthProfile.SkinProgress(id));
        public static bool IsUnlocked(InkSkinId id) => InkSkinCatalog.IsValid(id) &&
            (id == InkSkinId.Ink || Goal(id) > 0 && Progress(id) >= Goal(id));
        public static string Requirement(InkSkinId id)
        {
            var d = InkSkinCatalog.Get(id);
            if (d == null) return "???";
            if (id == InkSkinId.Ink) return "처음부터 사용 가능";
            return d.Metric switch {
                SkinMetric.Best => $"최고 고도 {d.Goal}m 달성",
                SkinMetric.Runs => $"정상 플레이 {d.Goal}판 완료",
                SkinMetric.Distance => $"누적 고도 {d.Goal}m 달성",
                SkinMetric.DrawnLandings => $"그린 발판에 {d.Goal}회 착지",
                SkinMetric.InkDrops => $"먹방울 아이템 {d.Goal}회 획득",
                SkinMetric.Items => $"아이템 {d.Goal}회 획득",
                SkinMetric.Survival => $"한 판에서 {d.Goal}초 생존",
                SkinMetric.Clones => $"먹 {d.Goal}마리 동시 생존",
                SkinMetric.NightBest => $"밤에 {d.Goal}m 달성",
                SkinMetric.Growth => $"성장 노드 {d.Goal}개 획득",
                SkinMetric.NoItemBest => $"아이템 없이 {d.Goal}m 달성",
                SkinMetric.ZeroRun => "그린 발판에 착지하지 않고 한 판 완료",
                _ => id switch {
                    InkSkinId.NightBat => "로비를 밤으로 바꾸기",
                    InkSkinId.Astronaut => "낮과 밤을 여섯 번 바꾸기",
                    InkSkinId.ClockworkCat => "고양이 귀로 밤에 아이템 없이 60초 생존",
                    InkSkinId.LuckyDice => "정확히 777m로 한 판 완료",
                    InkSkinId.OrigamiWings => "먹 → 갓 → 먹 → 거미탈 → 먹 순서로 살펴보기",
                    _ => "???" }
            };
        }
    }
    /// 착용 취향만 기기에 저장한다. 사용 권한은 현재 계정 업적으로 다시 검증한다.
    public static class InkSkinPreferences
    {
        public const string StorageKey = "MukJump.Settings.EquippedSkin";
        static ILobbySettingsStore store = new PlayerPrefsLobbySettingsStore();
        public static InkSkinId Equipped
        {
            get
            {
                var chosen = InkSkinCatalog.Normalize((InkSkinId)store.GetInt(StorageKey, 0));
                return InkSkinAchievements.IsUnlocked(chosen) ? chosen : InkSkinId.Ink;
            }
        }

        public static bool TryEquip(InkSkinId id)
        {
            if (!InkSkinAchievements.IsUnlocked(id) ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                return false;
            int previous = store.GetInt(StorageKey, 0);
            if (previous == (int)id) return true;
            try
            {
                store.SetInt(StorageKey, (int)id);
                store.Save();
                return true;
            }
            catch (Exception)
            {
                // Save 실패 뒤 종료 시 새 값이 뒤늦게 저장되지 않도록 메모리 값도 되돌린다.
                try { store.SetInt(StorageKey, previous); } catch (Exception) { }
                return false;
            }
        }

#if UNITY_EDITOR
        public static void UseStoreForTests(ILobbySettingsStore value) => store = value;
        public static void RestoreDefaultStoreForTests() => store = new PlayerPrefsLobbySettingsStore();
#endif
    }
}
