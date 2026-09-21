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
            Grant(InkSkinId.ZodiacRat, s.startedAtNight && s.items >= 3 && height >= 50);
            Grant(InkSkinId.ZodiacOx, s.drawnLandings >= 20 && seconds >= 60);
            Grant(InkSkinId.ZodiacTiger, final && s.startedAtNight && s.items == 0 && height >= 150);
            Grant(InkSkinId.ZodiacRabbit, s.startedAtNight && s.startingSkin == InkSkinId.MoonCrown && height >= 200);
            Grant(InkSkinId.ZodiacDragon, s.startingSkin == InkSkinId.DragonTail && height >= 300);
            Grant(InkSkinId.ZodiacSnake, s.startingSkin == InkSkinId.SpiderMask && s.drawnLandings >= 12 && height >= 120);
            // 제한 시간·무아이템은 판이 끝나야 확정된다. 중간 샘플에서 미리 지급하지 않는다.
            Grant(InkSkinId.ZodiacHorse, final && height >= 300 && seconds <= 60);
            Grant(InkSkinId.ZodiacGoat, s.drawnLandings >= 30 && height >= 200);
            Grant(InkSkinId.ZodiacMonkey, s.maxLivingPlayers >= 3 && s.items >= 5);
            Grant(InkSkinId.ZodiacRooster, !s.startedAtNight && s.inkDrops >= 3 && height >= 100);
            Grant(InkSkinId.ZodiacDog, s.maxLivingPlayers >= 2 && seconds >= 90);
            Grant(InkSkinId.ZodiacPig, s.items >= 12 && s.inkDrops >= 3);
            Grant(InkSkinId.HaetaeCrest, final && s.startingSkin == InkSkinId.TurtleShell && s.items == 0 && height >= 250);
            Grant(InkSkinId.ThreeLeggedCrow, s.startedAtNight && s.inkDrops >= 3 && height >= 333);
            Grant(InkSkinId.NineTails, s.startingSkin == InkSkinId.FoxMask && s.maxLivingPlayers >= 3 && height >= 300);
            return mask;
        }

        public static (string ko, string en, string ja) Requirement(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("밤 한 판에서 아이템 3개와 50m 달성", "In one night run, collect 3 items and reach 50m", "夜の1プレイでアイテム3個と50m達成"),
            InkSkinId.ZodiacOx => ("한 판에서 그린 발판 20회 착지와 60초 생존", "In one run, land on 20 drawn platforms and survive 60s", "1プレイで描いた足場に20回着地し60秒生存"),
            InkSkinId.ZodiacTiger => ("밤에 아이템 없이 150m 이상으로 한 판 완료", "Finish a night run at 150m or higher with no items", "夜にアイテムなしで150m以上のプレイを完了"),
            InkSkinId.ZodiacRabbit => ("달빛 관을 쓰고 밤에 200m 달성", "Wear Moon Crown and reach 200m at night", "月光の冠を着けて夜に200m達成"),
            InkSkinId.ZodiacDragon => ("용꼬리를 달고 300m 달성", "Wear Dragon Tail and reach 300m", "竜のしっぽを着けて300m達成"),
            InkSkinId.ZodiacSnake => ("거미탈로 한 판에서 발판 12회 착지와 120m 달성", "Spider Mask: 12 drawn landings + 120m in one run", "蜘蛛の面で1プレイ中に描いた足場へ12回着地し120m達成"),
            InkSkinId.ZodiacHorse => ("60초 안에 300m 이상으로 한 판 완료", "Finish a run at 300m or higher within 60s", "60秒以内に300m以上のプレイを完了"),
            InkSkinId.ZodiacGoat => ("한 판에서 그린 발판 30회 착지와 200m 달성", "In one run, land on 30 drawn platforms and reach 200m", "1プレイで描いた足場に30回着地し200m達成"),
            InkSkinId.ZodiacMonkey => ("한 판에서 먹 3마리 동시 생존과 아이템 5개 획득", "In one run, have 3 Inks alive together and collect 5 items", "1プレイで墨3体の同時生存とアイテム5個を達成"),
            InkSkinId.ZodiacRooster => ("낮 한 판에서 먹방울 3개와 100m 달성", "In one day run, collect 3 Ink Drops and reach 100m", "昼の1プレイで墨滴3個と100m達成"),
            InkSkinId.ZodiacDog => ("한 판에서 먹 2마리 동시 생존과 90초 생존", "In one run, have 2 Inks alive together and survive 90s", "1プレイで墨2体の同時生存と90秒生存を達成"),
            InkSkinId.ZodiacPig => ("한 판에서 먹방울 3개 포함 아이템 12개 획득", "Collect 12 items, including 3 Ink Drops, in one run", "1プレイで墨滴3個を含むアイテム12個を獲得"),
            InkSkinId.HaetaeCrest => ("현무로 아이템 없이 250m 이상 한 판 완료", "With Hyunmu, finish at 250m or higher without items", "玄武でアイテムなしの250m以上プレイを完了"),
            InkSkinId.ThreeLeggedCrow => ("밤 한 판에서 먹방울 3개와 333m 달성", "In one night run, collect 3 Ink Drops and reach 333m", "夜の1プレイで墨滴3個と333m達成"),
            InkSkinId.NineTails => ("여우탈로 한 판에서 먹 3마리 동시 생존과 300m 달성", "Fox Mask: 3 Inks alive together + 300m in one run", "狐の面で1プレイ中に墨3体の同時生存と300mを達成"),
            _ => ("???", "???", "???") };

        public static (string ko, string en, string ja) Lore(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("밤참 세 개, 50만큼", "Three night snacks, up to 50", "夜食は三つ、50まで"),
            InkSkinId.ZodiacOx => ("스무 번 꾹꾹, 일 분 버티기", "Twenty firm steps, one minute", "二十回ふみふみ、一分がまん"),
            InkSkinId.ZodiacTiger => ("밤사냥은 빈손으로", "Night hunting, empty-handed", "夜の狩りは手ぶらで"),
            InkSkinId.ZodiacRabbit => ("달을 쓰고 밤길 200", "Wear the moon, climb 200 at night", "月をかぶって、夜道を200"),
            InkSkinId.ZodiacDragon => ("꼬리 달고 300까지", "Tail on, up to 300", "しっぽをつけて300まで"),
            InkSkinId.ZodiacSnake => ("거미는 열두 번 콩콩", "The spider hops twelve times", "蜘蛛は十二回ぴょんぴょん"),
            InkSkinId.ZodiacHorse => ("300까지 일 분이면 돼", "One minute to finish at 300", "300まで、一分あればいい"),
            InkSkinId.ZodiacGoat => ("서른 번 딛고 200까지", "Thirty steps, up to 200", "三十回ふんで200まで"),
            InkSkinId.ZodiacMonkey => ("셋이서 간식 다섯 개", "Three friends, five snacks", "三人でおやつ五つ"),
            InkSkinId.ZodiacRooster => ("해 뜨면 먹방울 셋", "Sun's up, three Ink Drops", "日が出たら墨滴三つ"),
            InkSkinId.ZodiacDog => ("둘이서 일 분 반", "Two friends, a minute and a half", "二人で一分半"),
            InkSkinId.ZodiacPig => ("열둘 먹고 꿀꿀", "Twelve treats, oink oink", "十二個食べて、ぶうぶう"),
            InkSkinId.HaetaeCrest => ("현무는 250까지 빈손", "Hyunmu goes to 250 empty-handed", "玄武は250まで手ぶら"),
            InkSkinId.ThreeLeggedCrow => ("밤에는 셋셋셋", "Three-three-three by night", "夜は三・三・三"),
            InkSkinId.NineTails => ("여우 셋이서 300까지", "Three foxes, up to 300", "三匹の狐で300まで"),
            _ => ("???", "???", "???") };
    }
}
