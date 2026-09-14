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
            InkSkinId.HaetaeCrest => ("거북 투구로 아이템 없이 250m 이상 한 판 완료", "With Turtle Helm, finish at 250m or higher without items", "亀のかぶとでアイテムなしの250m以上プレイを完了"),
            InkSkinId.ThreeLeggedCrow => ("밤 한 판에서 먹방울 3개와 333m 달성", "In one night run, collect 3 Ink Drops and reach 333m", "夜の1プレイで墨滴3個と333m達成"),
            InkSkinId.NineTails => ("여우탈로 한 판에서 먹 3마리 동시 생존과 300m 달성", "Fox Mask: 3 Inks alive together + 300m in one run", "狐の面で1プレイ中に墨3体の同時生存と300mを達成"),
            _ => ("???", "???", "???") };

        public static (string ko, string en, string ja) Lore(InkSkinId id) => id switch {
            InkSkinId.ZodiacRat => ("달 뜨면 벼루 창고가 한 칸 빈다.", "Moon up. One shelf of ink goes missing.", "月が出ると、硯の棚がひとつ空く。"),
            InkSkinId.ZodiacOx => ("서두르지 않았는데 제일 먼저 왔다.", "Never hurried. Arrived first anyway.", "急がなかったのに、いちばん乗り。"),
            InkSkinId.ZodiacTiger => ("아직 포효보다 하품이 더 크다.", "Its yawn is still louder than its roar.", "雄叫びより、あくびの方が大きい。"),
            InkSkinId.ZodiacRabbit => ("떡은 두고 귀만 빌려 왔다.", "Left the rice cakes. Borrowed the ears.", "お餅は置いて、耳だけ借りてきた。"),
            InkSkinId.ZodiacDragon => ("승천 연습은 낮잠 다음에.", "Ascension practice comes after a nap.", "昇天の練習は、お昼寝のあと。"),
            InkSkinId.ZodiacSnake => ("리본인 줄 알고 묶을 뻔했다.", "Almost tied it. Thought it was a ribbon.", "リボンと思って、結ぶところだった。"),
            InkSkinId.ZodiacHorse => ("발굽 대신 바람 소리만 남겼다.", "Left a breeze instead of hoofprints.", "ひづめの跡の代わりに、風の音。"),
            InkSkinId.ZodiacGoat => ("산길은 곧게 걷고, 뿔만 둥글게 말았다.", "Walked the mountain trail straight. Only the horns took a turn.", "山道はまっすぐ歩いた。巻いたのは角だけ。"),
            InkSkinId.ZodiacMonkey => ("손이 모자라면 꼬리부터 내민다.", "Short a hand? The tail volunteers first.", "手が足りなければ、しっぽが先に伸びる。"),
            InkSkinId.ZodiacRooster => ("해보다 조금 늦게 일어난다.", "Wakes just a little after the sun.", "お日さまより、少し寝坊する。"),
            InkSkinId.ZodiacDog => ("문 대신 친구의 그림자를 지킨다.", "Guards a friend's shadow, not a gate.", "門ではなく、友だちの影を守る。"),
            InkSkinId.ZodiacPig => ("복을 불렀더니 간식도 왔다.", "Called for luck. Snacks came too.", "福を呼んだら、おやつも来た。"),
            InkSkinId.HaetaeCrest => ("나쁜 꿈만 콕 찌르는 작은 뿔.", "A tiny horn for poking bad dreams away.", "悪い夢だけ、ちくりと追い払う角。"),
            InkSkinId.ThreeLeggedCrow => ("세 발로도 아침엔 자꾸 헛디딘다.", "Three feet. Still stumbles in the morning.", "三本足でも、朝はつまずく。"),
            InkSkinId.NineTails => ("꼬리는 아홉인데, 속마음은 하나도 안 보인다.", "Nine tails. Not a single clue what it is thinking.", "尾は九つ。心の内はひとつも見えない。"),
            _ => ("???", "???", "???") };
    }
}
