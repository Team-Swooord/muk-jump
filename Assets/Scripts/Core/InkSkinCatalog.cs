using System;
using UnityEngine;
namespace MukJump.Core
{
    public enum InkSkinId { Ink = 0, Gat = 1, SpiderMask = 2, FirstRibbon = 3, Scholar = 4, InkGhost = 5, BambooHat = 6, RedHeadband = 7, LotusCrown = 8, CatEars = 9, BellRibbon = 10, CloudCap = 11, PaperBoat = 12, TeaCupHat = 13, FeatherHairpin = 14, PeachFlower = 15, FoxMask = 16, CraneCrest = 17, YakshaMask = 18, TurtleShell = 19, StormScarf = 20, MoonCrown = 21, BrushMaster = 22, SpiritHalo = 23, KoiMask = 24, JadeCrown = 25, PhoenixCrown = 26, TigerMask = 27, MountainKing = 28, CloudDragon = 29, InkMecha = 30, NightBat = 31, Astronaut = 32, ClockworkCat = 33, LuckyDice = 34, OrigamiWings = 35, CraneWings = 36, ButterflyWings = 37, RedShoes = 38, CloudBoots = 39 }
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
        public const int Count = 40;
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
            new(InkSkinId.OrigamiWings,"접힌 차원","Folded Dimension","折りたたむ次元","skin_origami_wings_v1",SkinTier.UltraHidden,SkinMetric.Secret,1),
            new(InkSkinId.CraneWings,"학의 날개","Crane Wings","鶴の翼","skin_crane_wings_v1",SkinTier.Medium,SkinMetric.Best,350),
            new(InkSkinId.ButterflyWings,"먹나비 날개","Ink Butterfly","墨蝶の羽","skin_butterfly_wings_v1",SkinTier.Medium,SkinMetric.Items,50),
            new(InkSkinId.RedShoes,"붉은 버선","Crimson Steps","紅の足袋","skin_red_shoes_v1",SkinTier.Easy,SkinMetric.DrawnLandings,15),
            new(InkSkinId.CloudBoots,"구름 장화","Cloud Boots","雲の靴","skin_cloud_boots_v1",SkinTier.Medium,SkinMetric.Distance,3000)
        };
        public static bool IsValid(InkSkinId id) => (int)id >= 0 && (int)id < Count;
        public static InkSkinId Normalize(InkSkinId id) => IsValid(id) ? id : InkSkinId.Ink;
        public static InkSkinDefinition Get(InkSkinId id) => IsValid(id) ? definitions[(int)id] : null;
        // 표시 순서만 변경한다. 서버·해금·장착에 저장된 ID는 절대로 재배열하지 않는다.
        static readonly InkSkinId[] browseOrder = BuildBrowseOrder();
        static InkSkinId[] BuildBrowseOrder()
        {
            var order = new InkSkinId[Count];
            for (int i = 0; i < Count; i++) order[i] = (InkSkinId)i;
            Array.Sort(order, (a, b) => {
                int group = BrowseGroup(a).CompareTo(BrowseGroup(b));
                if (group != 0) return group;
                int goal = Get(a).Goal.CompareTo(Get(b).Goal);
                return goal != 0 ? goal : ((int)a).CompareTo((int)b);
            });
            return order;
        }
        public static int BrowseGroup(InkSkinId id) => Get(id)?.Tier is SkinTier.Hidden or SkinTier.UltraHidden
            ? 100 : (int)(Get(id)?.Metric ?? SkinMetric.None);
        public static InkSkinId BrowseAt(int index) => browseOrder[Mathf.Clamp(index, 0, Count - 1)];
        public static int BrowseIndex(InkSkinId id) => Array.IndexOf(browseOrder, Normalize(id));
        public static string GroupName(InkSkinId id) => BrowseGroup(id) == 100 ? "비밀 의상" : Get(id)?.Metric switch {
            SkinMetric.Best => "최고 고도", SkinMetric.Runs => "플레이 횟수", SkinMetric.Distance => "누적 고도",
            SkinMetric.DrawnLandings => "발판 착지", SkinMetric.InkDrops => "먹방울 수집", SkinMetric.Items => "아이템 수집",
            SkinMetric.Survival => "생존 시간", SkinMetric.Clones => "분신", SkinMetric.NightBest => "밤의 고도",
            SkinMetric.Growth => "성장", SkinMetric.NoItemBest => "아이템 없는 도전", _ => "기본 의상" };
        public static bool IsSignatureCostume(InkSkinId id) => id is InkSkinId.CloudDragon or InkSkinId.InkMecha or InkSkinId.ClockworkCat;
        public static bool IsConcealed(InkSkinId id) => (Get(id)?.Tier is SkinTier.Hidden or SkinTier.UltraHidden) && !InkSkinAchievements.IsUnlocked(id);
        public static string Name(InkSkinId id) => Get(id)?.Korean ?? "???";
        public static bool HasAbility(InkSkinId id) => id is InkSkinId.Gat or InkSkinId.SpiderMask or
            InkSkinId.SpiritHalo or InkSkinId.TurtleShell or InkSkinId.PhoenixCrown;
        public static string Ability(InkSkinId id) => id switch {
            InkSkinId.Gat => "이단 도약", InkSkinId.SpiderMask => "거미 걸음",
            InkSkinId.SpiritHalo => "먹의 환원", InkSkinId.TurtleShell => "시작 방어막", InkSkinId.PhoenixCrown => "봉황의 숨결",
            _ => "먹의 장식" };
        public static string Description(InkSkinId id) => id switch {
            InkSkinId.Gat => "점프 정점에서 한 번 더 도약해요.\n첫 도약의 40% 힘으로 자동 발동해요.",
            InkSkinId.SpiderMask => "양옆 벽에 붙었다가\n화면 안쪽으로 자동 도약해요.",
            InkSkinId.SpiritHalo => "분신이 쓰러질 때마다 본체 체력을 1칸 회복해요.\n최대 체력을 넘거나 쓰러진 본체를 되살리지는 않아요.",
            InkSkinId.TurtleShell => "매 판 본체에게 방어막 1개를 주고 시작해요.\n충돌·추락을 한 번 막으며 분신에게는 적용되지 않아요.",
            InkSkinId.PhoenixCrown => "한 판에 한 번 본체가 체력 1칸으로 살아남아 50m 올라가요.\n성장 부활과 횟수를 공유하며 분신에게는 적용되지 않아요.",
            _ => "원래 먹에 장식을 더해요.\n기존 점프와 성장 효과를 유지해요." };
        public static string ResourcePath(InkSkinId id) => Get(id) is { Asset: { Length: > 0 } } d ? "MukJump/Skins/" + d.Asset : "";
        public static bool BehindBody(InkSkinId id) => id is InkSkinId.OrigamiWings or InkSkinId.CraneWings or InkSkinId.ButterflyWings or InkSkinId.SpiritHalo;
        public static bool IsFootwear(InkSkinId id) => id is InkSkinId.RedShoes or InkSkinId.CloudBoots;
        // 1024 프레임 전체가 기준이다. 실제 먹 몸통은 약 0.7배 폭이며 넉넉한 착용선을 사용한다.
        public static float Width(InkSkinId id) => id switch {
            InkSkinId.Gat => .95f, InkSkinId.Scholar => .92f, InkSkinId.BambooHat => .90f,
            InkSkinId.SpiderMask => .70f, InkSkinId.FoxMask => .70f,
            InkSkinId.YakshaMask => .74f, InkSkinId.KoiMask => .70f,
            InkSkinId.TigerMask => .70f, InkSkinId.NightBat => .68f,
            InkSkinId.FirstRibbon => .42f, InkSkinId.InkGhost => .80f, InkSkinId.BellRibbon => .35f,
            InkSkinId.RedHeadband => 1.00f, InkSkinId.StormScarf => 1.02f,
            InkSkinId.LotusCrown => .85f, InkSkinId.CatEars => .98f, InkSkinId.CloudCap => .95f,
            InkSkinId.PaperBoat => .80f, InkSkinId.TeaCupHat => .68f,
            InkSkinId.FeatherHairpin => .43f, InkSkinId.PeachFlower => .80f,
            InkSkinId.CraneCrest => .88f, InkSkinId.TurtleShell => 1.02f,
            InkSkinId.MoonCrown => .95f, InkSkinId.BrushMaster => .90f,
            InkSkinId.JadeCrown => .94f, InkSkinId.PhoenixCrown => .90f, InkSkinId.MountainKing => .90f,
            InkSkinId.CloudDragon => 1.00f, InkSkinId.InkMecha => 1.00f, InkSkinId.Astronaut => 1.05f,
            InkSkinId.ClockworkCat => 1.02f, InkSkinId.SpiritHalo => 1.04f, InkSkinId.LuckyDice => .88f,
            InkSkinId.CraneWings => 1.35f, InkSkinId.ButterflyWings => 1.30f, InkSkinId.OrigamiWings => 1.30f,
            InkSkinId.RedShoes or InkSkinId.CloudBoots => 1f, _ => .68f };
        public static Vector2 Offset(InkSkinId id)
        {
            // 뒤쪽 끈은 그리지 않는다. 이마 띠와 옆머리 매듭의 중심을 따로 맞춘다.
            if (id == InkSkinId.RedHeadband) return new Vector2(.015f, .23f);
            if (id == InkSkinId.FirstRibbon) return new Vector2(.23f, .25f);
            if (id == InkSkinId.BellRibbon) return new Vector2(.25f, .18f);
            if (id == InkSkinId.FeatherHairpin) return new Vector2(.22f, .27f);
            if (id == InkSkinId.StormScarf) return new Vector2(.045f, .16f);
            if (id == InkSkinId.ClockworkCat) return new Vector2(.04f, .10f);
            return new Vector2(0, id switch {
                InkSkinId.Gat => .32f, InkSkinId.Scholar => .34f, InkSkinId.BambooHat => .36f,
                InkSkinId.LotusCrown => .33f, InkSkinId.CraneCrest => .31f, InkSkinId.PhoenixCrown => .40f,
                InkSkinId.PaperBoat => .33f, InkSkinId.JadeCrown => .32f, InkSkinId.MountainKing => .36f,
                InkSkinId.TeaCupHat => .37f, InkSkinId.BrushMaster => .36f,
                InkSkinId.MoonCrown => .28f, InkSkinId.PeachFlower => .29f,
                InkSkinId.SpiderMask => .085f, InkSkinId.FoxMask => .095f,
                InkSkinId.YakshaMask => .055f, InkSkinId.KoiMask => .06f, InkSkinId.TigerMask => .075f, InkSkinId.NightBat => .095f,
                InkSkinId.CatEars => .11f, InkSkinId.TurtleShell => .30f, InkSkinId.CloudCap => .22f,
                InkSkinId.CloudDragon => .21f, InkSkinId.InkMecha => .10f, InkSkinId.SpiritHalo => .10f,
                InkSkinId.InkGhost => .015f, InkSkinId.Astronaut => .06f,
                InkSkinId.OrigamiWings => .03f, InkSkinId.CraneWings => .04f, InkSkinId.ButterflyWings => .03f,
                InkSkinId.RedShoes or InkSkinId.CloudBoots => -.25f, InkSkinId.LuckyDice => .34f, _ => 0f });
        }
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
        public static event Action Changed;
        // 디버그 시험 착용은 공개 리더보드로 전송하지 않는다.
        public static InkSkinId PersistedEquipped
        {
            get
            {
                var chosen = InkSkinCatalog.Normalize((InkSkinId)store.GetInt(StorageKey, 0));
                return InkSkinAchievements.IsUnlocked(chosen) ? chosen : InkSkinId.Ink;
            }
        }
        public static InkSkinId Equipped
        {
            get
            {
                if (SkinDebugSession.TryGetEquipped(out var preview)) return preview;
                return PersistedEquipped;
            }
        }

        public static bool TryEquip(InkSkinId id)
        {
            if (!SkinDebugSession.CanEquip(id) ||
                GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby ||
                MukJumpAccountRuntime.Instance != null && MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                return false;
            if (SkinDebugSession.Active) return SkinDebugSession.TryEquip(id);
            int previous = store.GetInt(StorageKey, 0);
            if (previous == (int)id) return true;
            try
            {
                store.SetInt(StorageKey, (int)id);
                store.Save();
                // 로컬 저장 성공 뒤에만 기존 클라우드 저장 대기열에 변경을 알린다.
                // 수신자 오류 때문에 이미 저장된 장착을 실패/롤백으로 취급하지 않는다.
                if (Changed != null)
                    foreach (Action listener in Changed.GetInvocationList())
                        try { listener(); }
                        catch (Exception error) { UnityEngine.Debug.LogWarning("[MukJump] 착용 변경 알림 실패: " + error.Message); }
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
