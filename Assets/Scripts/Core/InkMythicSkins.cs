using UnityEngine;

namespace MukJump.Core
{
    /// 십이지신·신화 의상은 실제 한 판의 계측만 사용한다. 기기 시각·이전 계정 기록은 조건이 아니다.
    public static class InkMythicSkins
    {
        public const int First = 46, Last = 60;
        public static bool Contains(InkSkinId id) => (int)id >= First && (int)id <= Last;

        public static ulong Evaluate(SkinRunStats s, int height, float seconds, bool final)
        {
            if (!s.IsMeasured || height < 0 || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
            ulong mask = 0;
            void Grant(InkSkinId id, bool earned) { if (earned) mask |= 1UL << (int)id; }
            Grant(InkSkinId.ZodiacRat, s.startingSkin == InkSkinId.NightBat && s.startedAtNight && s.items >= 8 && height >= 500);
            Grant(InkSkinId.ZodiacOx, s.startingSkin == InkSkinId.RedHeadband && s.drawnLandings >= 50 && seconds >= 120 && height >= 400);
            Grant(InkSkinId.ZodiacTiger, final && s.startingSkin == InkSkinId.TigerMask && s.startedAtNight && s.items == 0 && height >= 1000);
            Grant(InkSkinId.ZodiacRabbit, s.startedAtNight && s.startingSkin == InkSkinId.MoonCrown && height >= 1500);
            Grant(InkSkinId.ZodiacDragon, s.startingSkin == InkSkinId.DragonTail && height >= 1800);
            Grant(InkSkinId.ZodiacSnake, s.startingSkin == InkSkinId.SpiderMask && s.drawnLandings >= 40 && height >= 1000);
            // 제한 시간·무아이템은 판이 끝나야 확정된다. 중간 샘플에서 미리 지급하지 않는다.
            Grant(InkSkinId.ZodiacHorse, final && s.startingSkin == InkSkinId.RedShoes && height >= 1000 && seconds <= 90);
            Grant(InkSkinId.ZodiacGoat, s.startingSkin == InkSkinId.FeatherHairpin && s.drawnLandings >= 80 && height >= 1500);
            Grant(InkSkinId.ZodiacMonkey, s.startingSkin == InkSkinId.BellRibbon && s.maxLivingPlayers >= 8 && s.items >= 20 && height >= 600);
            Grant(InkSkinId.ZodiacRooster, s.startingSkin == InkSkinId.Gat && !s.startedAtNight && s.inkDrops >= 6 && height >= 900);
            Grant(InkSkinId.ZodiacDog, s.startingSkin == InkSkinId.FirstRibbon && s.maxLivingPlayers >= 6 && seconds >= 180 && height >= 800);
            Grant(InkSkinId.ZodiacPig, s.startingSkin == InkSkinId.CatEars && s.items >= 30 && s.inkDrops >= 8 && height >= 1000);
            Grant(InkSkinId.HaetaeCrest, final && s.startingSkin == InkSkinId.TurtleShell && s.items == 0 && height >= 2000);
            Grant(InkSkinId.ThreeLeggedCrow, s.startingSkin == InkSkinId.NightBat && s.startedAtNight && s.inkDrops >= 9 && height >= 3333);
            Grant(InkSkinId.NineTails, s.startingSkin == InkSkinId.FoxMask && s.maxLivingPlayers >= 9 && height >= 2500);
            return mask;
        }

        public static (string ko, string en, string ja) Requirement(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("밤의 파수꾼으로 밤 한 판에서 아이템 8개·500m 달성", "Night Watcher: 8 items and 500m in one night run", "夜の番人で夜の1プレイ中にアイテム8個・500m達成"),
            InkSkinId.ZodiacOx => ("수련 머리띠로 한 판에서 그린 발판 50회·120초·400m 달성", "Training Band: 50 drawn landings, 120s and 400m in one run", "修行の鉢巻で1プレイ中に描いた足場50回・120秒・400m達成"),
            InkSkinId.ZodiacTiger => ("산군 탈로 밤에 아이템 없이 1000m 이상 한 판 완료", "Tiger Mask: finish a night run at 1000m or higher without items", "山の虎面で夜にアイテムなしで1000m以上のプレイを完了"),
            InkSkinId.ZodiacRabbit => ("달빛 관을 쓰고 밤에 1500m 달성", "Wear Moon Crown and reach 1500m at night", "月光の冠を着けて夜に1500m達成"),
            InkSkinId.ZodiacDragon => ("용꼬리를 달고 1800m 달성", "Wear Dragon Tail and reach 1800m", "竜のしっぽを着けて1800m達成"),
            InkSkinId.ZodiacSnake => ("거미탈로 한 판에서 그린 발판 40회·1000m 달성", "Spider Mask: 40 drawn landings and 1000m in one run", "蜘蛛の面で1プレイ中に描いた足場40回・1000m達成"),
            InkSkinId.ZodiacHorse => ("붉은 버선으로 90초 안에 1000m 이상 한 판 완료", "Crimson Steps: finish at 1000m or higher within 90s", "紅の足袋で90秒以内に1000m以上のプレイを完了"),
            InkSkinId.ZodiacGoat => ("깃털 비녀로 한 판에서 그린 발판 80회·1500m 달성", "Feather Pin: 80 drawn landings and 1500m in one run", "羽のかんざしで1プレイ中に描いた足場80回・1500m達成"),
            InkSkinId.ZodiacMonkey => ("방울 매듭으로 한 판에서 먹 8마리 동시 생존·아이템 20개·600m 달성", "Bell Knot: 8 Inks alive together, 20 items and 600m in one run", "鈴結びで1プレイ中に墨8体同時生存・アイテム20個・600m達成"),
            InkSkinId.ZodiacRooster => ("바람 갓으로 낮 한 판에서 먹방울 6개·900m 달성", "Wind Gat: 6 Ink Drops and 900m in one day run", "風の笠で昼の1プレイ中に墨滴6個・900m達成"),
            InkSkinId.ZodiacDog => ("먹매듭으로 한 판에서 먹 6마리 동시 생존·180초·800m 달성", "Ink Knot: 6 Inks alive together, 180s and 800m in one run", "墨結びで1プレイ中に墨6体同時生存・180秒・800m達成"),
            InkSkinId.ZodiacPig => ("고양이 귀로 한 판에서 먹방울 8개 포함 아이템 30개·1000m 달성", "Cat Ears: 30 items including 8 Ink Drops, and 1000m in one run", "猫耳で1プレイ中に墨滴8個を含むアイテム30個・1000m達成"),
            InkSkinId.HaetaeCrest => ("현무로 아이템 없이 2000m 이상 한 판 완료", "With Hyunmu, finish at 2000m or higher without items", "玄武でアイテムなしの2000m以上プレイを完了"),
            InkSkinId.ThreeLeggedCrow => ("밤의 파수꾼으로 밤 한 판에서 먹방울 9개·3333m 달성", "Night Watcher: 9 Ink Drops and 3333m in one night run", "夜の番人で夜の1プレイ中に墨滴9個・3333m達成"),
            InkSkinId.NineTails => ("여우탈로 한 판에서 먹 9마리 동시 생존·2500m 달성", "Fox Mask: 9 Inks alive together and 2500m in one run", "狐の面で1プレイ中に墨9体同時生存・2500m達成"),
            _ => ("???", "???", "???") };

        public static (string ko, string en, string ja) Lore(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("밤의 파수꾼, 밤참 여덟 개", "Night watcher, eight night snacks", "夜の番人、夜食は八つ"),
            InkSkinId.ZodiacOx => ("머리띠 조이고 쉰 번 딛기", "Band tied tight, fifty landings", "鉢巻を締めて、五十回着地"),
            InkSkinId.ZodiacTiger => ("산군의 밤사냥은 빈손으로", "The tiger hunts at night, empty-handed", "山の虎は夜の狩りも手ぶら"),
            InkSkinId.ZodiacRabbit => ("달을 쓰고 밤길 1500", "Wear the moon, climb 1500 at night", "月をかぶって、夜道を1500"),
            InkSkinId.ZodiacDragon => ("꼬리 달고 1800까지", "Tail on, up to 1800", "しっぽをつけて1800まで"),
            InkSkinId.ZodiacSnake => ("거미의 마흔 번째 착지", "The spider's fortieth landing", "蜘蛛の四十回目の着地"),
            InkSkinId.ZodiacHorse => ("붉은 발로 1000, 일 분 반", "Crimson feet, 1000 in ninety seconds", "紅の足で1000、一分半"),
            InkSkinId.ZodiacGoat => ("깃털 달고 여든 번 딛기", "Feather on, eighty landings", "羽をつけて八十回着地"),
            InkSkinId.ZodiacMonkey => ("방울 울려 여덟이 모이면", "Ring the bell, gather eight", "鈴を鳴らして八人集合"),
            InkSkinId.ZodiacRooster => ("갓 쓰고 아침 먹방울 여섯", "Gat on, six morning Ink Drops", "笠をかぶって朝の墨滴六つ"),
            InkSkinId.ZodiacDog => ("첫 매듭, 여섯 친구, 삼 분", "First knot, six friends, three minutes", "最初の結び目、六人、三分"),
            InkSkinId.ZodiacPig => ("고양이도 서른 개는 배불러", "Even a cat is full after thirty", "猫も三十個でお腹いっぱい"),
            InkSkinId.HaetaeCrest => ("현무는 2000까지 빈손", "Hyunmu goes to 2000 empty-handed", "玄武は2000まで手ぶら"),
            InkSkinId.ThreeLeggedCrow => ("파수꾼의 밤, 아홉 방울, 3333", "Watcher's night, nine drops, 3333", "番人の夜、九滴、3333"),
            InkSkinId.NineTails => ("여우 아홉이서 2500까지", "Nine foxes, up to 2500", "九匹の狐で2500まで"),
            _ => ("???", "???", "???") };

        internal static (string ko, string en, string ja) PreviousLore(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("밤참 세 개, 50만큼", "Three night snacks, up to 50", "夜食は三つ、50まで"),
            InkSkinId.ZodiacOx => ("스무 번 딛고, 일 분 버티기", "Twenty landings, one minute", "二十回着地して、一分耐える"),
            InkSkinId.ZodiacTiger => ("밤사냥은 빈손으로", "Night hunting, empty-handed", "夜の狩りは手ぶらで"),
            InkSkinId.ZodiacRabbit => ("달을 쓰고 밤길 200", "Wear the moon, climb 200 at night", "月をかぶって、夜道を200"),
            InkSkinId.ZodiacDragon => ("꼬리 달고 300까지", "Tail on, up to 300", "しっぽをつけて300まで"),
            InkSkinId.ZodiacSnake => ("거미의 열두 번째 착지", "The spider's twelfth landing", "蜘蛛の十二回目の着地"),
            InkSkinId.ZodiacHorse => ("300까지 일 분이면 돼", "One minute to finish at 300", "300まで、一分あればいい"),
            InkSkinId.ZodiacGoat => ("서른 번 딛고 200까지", "Thirty steps, up to 200", "三十回ふんで200まで"),
            InkSkinId.ZodiacMonkey => ("셋이서 간식 다섯 개", "Three friends, five snacks", "三人でおやつ五つ"),
            InkSkinId.ZodiacRooster => ("해 뜨면 먹방울 셋", "Sun's up, three Ink Drops", "日が出たら墨滴三つ"),
            InkSkinId.ZodiacDog => ("둘이서 일 분 반", "Two friends, a minute and a half", "二人で一分半"),
            InkSkinId.ZodiacPig => ("열두 개도 모자라", "Twelve is not enough", "十二個でも足りない"),
            InkSkinId.HaetaeCrest => ("현무는 250까지 빈손", "Hyunmu goes to 250 empty-handed", "玄武は250まで手ぶら"),
            InkSkinId.ThreeLeggedCrow => ("밤에는 셋셋셋", "Three-three-three by night", "夜は三・三・三"),
            InkSkinId.NineTails => ("여우 셋이서 300까지", "Three foxes, up to 300", "三匹の狐で300まで"),
            _ => ("???", "???", "???") };
    }
}
