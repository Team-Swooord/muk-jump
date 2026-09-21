using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MukJump.Core
{
    /// 뒤끝 차트는 공통 설정만 바꾼다. 계정의 해금 비트·장착 ID·진척 저장은 건드리지 않는다.
    public static class InkSkinChart
    {
        public const string ChartName = "MukJumpSkinsV1";
        public const int MaxJsonLength = 512 * 1024;
        public sealed class Snapshot
        {
            internal readonly Dictionary<InkSkinId, InkSkinDefinition> definitions = new();
            internal readonly Dictionary<InkSkinId, JObject> rows = new();
            internal readonly Dictionary<string, (string en, string ja)> translations = new();
        }
        static Snapshot current;
        public static bool HasRemoteData => current != null;
        public static InkSkinDefinition Definition(InkSkinId id) =>
            current != null && current.definitions.TryGetValue(id, out var value) ? value : InkSkinCatalog.BuiltIn(id);
        public static float DefaultCooldown(InkSkinId id) => id switch {
            InkSkinId.WindFan => 20, InkSkinId.YakshaMask or InkSkinId.SpiderMask => 30, _ => 0 };
        public static float Cooldown(InkSkinId id) => current != null && current.rows.TryGetValue(id, out var row)
            ? float.Parse((string)row["cooldownSeconds"], CultureInfo.InvariantCulture) : DefaultCooldown(id);
        public static bool GoalEditable(InkSkinId id) => InkSkinCatalog.BuiltIn(id)?.Metric is
            SkinMetric.Best or SkinMetric.Runs or SkinMetric.Distance or SkinMetric.DrawnLandings or
            SkinMetric.InkDrops or SkinMetric.Items or SkinMetric.Survival or SkinMetric.Clones or
            SkinMetric.NightBest or SkinMetric.Growth or SkinMetric.NoItemBest;
        public static string Text(InkSkinId id, string field, string fallback) =>
            current != null && current.rows.TryGetValue(id, out var row) ? Expand((string)row[field], row) : fallback;
        public static bool TryTranslate(string source, bool japanese, out string text)
        {
            if (current != null && current.translations.TryGetValue(source, out var value))
            { text = japanese ? value.ja : value.en; return true; }
            text = null; return false;
        }
        static string Expand(string value, JObject row) => (value ?? "").Replace("{cooldown}", (string)row["cooldownSeconds"]);
        public static bool TryParse(string json, out Snapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength) throw new FormatException("차트 크기 오류");
                var rows = JArray.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (rows.Count != InkSkinCatalog.Count - 1) throw new FormatException("62개 행 필요 (은퇴 ID 20 제외)");
                var candidate = new Snapshot();
                foreach (var token in rows)
                {
                    if (token is not JObject row) throw new FormatException("행 형식 오류");
                    int number = Integer(row, "skinId", 0, InkSkinCatalog.Count - 1);
                    var id = (InkSkinId)number;
                    var original = InkSkinCatalog.BuiltIn(id);
                    if (!InkSkinCatalog.IsAvailable(id) || candidate.rows.ContainsKey(id)) throw new FormatException("중복 또는 은퇴 ID");
                    if (Integer(row, "schemaVersion", 1, 1) != 1 || Required(row, "skinKey", 60) != id.ToString() ||
                        Required(row, "unlockMetric", 40) != original.Metric.ToString() ||
                        Required(row, "tier", 30) != original.Tier.ToString() ||
                        Required(row, "abilityKey", 60) != (InkSkinCatalog.HasAbility(id) ? id.ToString() : "None"))
                        throw new FormatException("고정 식별자·조건 종류 변경 불가");
                    int goal = Integer(row, "unlockGoal", id == InkSkinId.Ink ? 0 : 1, 100000000);
                    if (!GoalEditable(id) && goal != original.Goal) throw new FormatException("복합 히든 조건은 코드에서 관리");
                    int cooldown = Integer(row, "cooldownSeconds", 0, 600);
                    if (DefaultCooldown(id) == 0 ? cooldown != 0 : cooldown < 1)
                        throw new FormatException("쿨타임 범위 오류");
                    // 숫자 셀도 문자열 셀도 동일한 표준값으로 보관한다.
                    row["cooldownSeconds"] = cooldown.ToString("0.###", CultureInfo.InvariantCulture);
                    row["unlockGoal"] = goal.ToString(CultureInfo.InvariantCulture);
                    foreach (var field in new[] { "name", "lore", "abilityName", "abilityDescription" })
                    {
                        bool required = field != "abilityName" && field != "abilityDescription" || InkSkinCatalog.HasAbility(id);
                        foreach (var lang in new[] { "Ko", "En", "Ja" })
                        {
                            string text = required ? Required(row, field + lang, field == "name" ? 80 : 700) : Optional(row, field + lang, 700);
                            // 구 차트·오프라인 캐시의 기본 이야기만 교정하고, 운영자가 새로 쓴 문구는 보존한다.
                            if (id == InkSkinId.Gat && field == "lore")
                            {
                                var lore = InkSkinLore.BuiltIn(id);
                                text = (lang, text) switch
                                {
                                    ("Ko", "쓰고 나면 어째서인지 바람처럼 스쳐 가는 노래가 들린다.") => lore.ko,
                                    ("En", "Put it on, and an old street anthem drifts past on the wind.") => lore.en,
                                    ("Ja", "かぶると、なぜか風に乗って昔の街の歌が聞こえる。") => lore.ja,
                                    _ => text
                                };
                            }
                            if (field == "lore") text = InkSkinLore.UpgradeLegacyHiddenHint(id, lang, text);
                            text = UpgradeLegacyCopy(id, field + lang, text);
                            if (field == "abilityDescription" && cooldown > 0 && !text.Contains("{cooldown}"))
                                throw new FormatException(field + lang + " 쿨타임 치환자 필요");
                            row[field + lang] = text;
                        }
                        string ko = Expand((string)row[field + "Ko"], row);
                        var pair = (Expand((string)row[field + "En"], row), Expand((string)row[field + "Ja"], row));
                        if (ko.Length > 0)
                        {
                            if (candidate.translations.TryGetValue(ko, out var previous) && previous != pair)
                                throw new FormatException("같은 한국어 원문의 번역 불일치");
                            candidate.translations[ko] = pair;
                        }
                    }
                    candidate.definitions[id] = new InkSkinDefinition(id, (string)row["nameKo"], (string)row["nameEn"],
                        (string)row["nameJa"], original.Asset, original.Tier, original.Metric, goal);
                    candidate.rows[id] = row;
                }
                snapshot = candidate; return true;
            }
            catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or OverflowException)
            { error = ex.Message; return false; }
        }
        // 이전 기본 문구만 이관한다. 차트에서 별도로 작성한 문구·수치·식별자는 그대로 둔다.
        static string UpgradeLegacyCopy(InkSkinId id, string field, string text) => (id, field, text) switch
        {
            (InkSkinId.FirstRibbon, "nameKo", "첫 먹매듭") => "먹매듭",
            (InkSkinId.FirstRibbon, "nameEn", "First Knot") => "Ink Knot",
            (InkSkinId.FirstRibbon, "nameJa", "はじめの墨結び") => "墨結び",
            (InkSkinId.InkGhost, "nameKo", "빈 먹 유령") => "먹유령",
            (InkSkinId.InkGhost, "nameEn", "Blank Ghost") => "Ink Ghost",
            (InkSkinId.InkGhost, "nameJa", "空っぽの墨おばけ") => "墨の幽霊",
            (InkSkinId.PaperBoat, "nameKo", "종이배 모자") => "종이배",
            (InkSkinId.PaperBoat, "nameJa", "紙舟の帽子") => "紙の舟",
            (InkSkinId.TeaCupHat, "nameKo", "찻잔 모자") => "찻잔",
            (InkSkinId.TeaCupHat, "nameEn", "Tea Cup Hat") => "Teacup",
            (InkSkinId.TeaCupHat, "nameJa", "湯のみ帽") => "茶碗",
            (InkSkinId.CraneCrest, "nameKo", "학의 볏") => "학깃",
            (InkSkinId.CraneCrest, "nameEn", "Crane Crest") => "Crane Plume",
            (InkSkinId.CraneCrest, "nameJa", "鶴の冠") => "鶴の羽飾り",
            (InkSkinId.BrushMaster, "nameKo", "붓 장인") => "쌍붓",
            (InkSkinId.BrushMaster, "nameEn", "Brush Master") => "Twin Brushes",
            (InkSkinId.BrushMaster, "nameJa", "筆の匠") => "二本の筆",
            (InkSkinId.JadeCrown, "nameKo", "비취 용관") => "옥관",
            (InkSkinId.JadeCrown, "nameJa", "翡翠の竜冠") => "玉冠",
            (InkSkinId.CloudDragon, "nameKo", "구름 용") => "운룡",
            (InkSkinId.CloudDragon, "nameJa", "雲の竜") => "雲竜",
            (InkSkinId.InkMecha, "nameKo", "먹빛 기갑") => "철가면",
            (InkSkinId.InkMecha, "nameEn", "Ink Mecha") => "Iron Mask",
            (InkSkinId.InkMecha, "nameJa", "墨の機甲") => "鉄仮面",
            (InkSkinId.ButterflyWings, "nameKo", "먹나비 날개") => "먹나비",
            (InkSkinId.ButterflyWings, "nameJa", "墨蝶の羽") => "墨の蝶",
            (InkSkinId.BlueWisps, "nameKo", "도깨비불 나들이") => "도깨비불",
            (InkSkinId.BlueWisps, "nameEn", "Wisp Parade") => "Ghostfire",
            (InkSkinId.BlueWisps, "nameJa", "鬼火のおさんぽ") => "鬼火",
            (InkSkinId.MoonClouds, "nameKo", "달구름 둥지") => "달구름",
            (InkSkinId.MoonClouds, "nameEn", "Mooncloud Nest") => "Mooncloud",
            (InkSkinId.MoonClouds, "nameJa", "月雲の巣") => "月雲",
            (InkSkinId.ZodiacRat, "nameKo", "밤쥐의 동그란 귀") => "밤쥐",
            (InkSkinId.ZodiacRat, "nameEn", "Night Mouse's Round Ears") => "Night Mouse",
            (InkSkinId.ZodiacRat, "nameJa", "夜ねずみの丸い耳") => "夜ねずみ",
            (InkSkinId.ZodiacOx, "nameKo", "들소의 뿔") => "황소",
            (InkSkinId.ZodiacOx, "nameEn", "Bison Horns") => "Bull",
            (InkSkinId.ZodiacOx, "nameJa", "野牛の角") => "雄牛",
            (InkSkinId.ZodiacTiger, "nameKo", "산군의 줄무늬 두건") => "호피 두건",
            (InkSkinId.ZodiacTiger, "nameEn", "Tiger's Striped Wrap") => "Tiger Bandana",
            (InkSkinId.ZodiacTiger, "nameJa", "山守虎の縞ずきん") => "虎柄の頭巾",
            (InkSkinId.ZodiacRabbit, "nameKo", "달토끼의 접힌 귀") => "달토끼",
            (InkSkinId.ZodiacRabbit, "nameEn", "Folded Moonrabbit Ears") => "Moon Rabbit",
            (InkSkinId.ZodiacRabbit, "nameJa", "月うさぎの折れ耳") => "月兎",
            (InkSkinId.ZodiacDragon, "nameKo", "꼬마 용의 새싹 뿔") => "용각",
            (InkSkinId.ZodiacDragon, "nameEn", "Sprouting Dragon Horns") => "Dragon Horns",
            (InkSkinId.ZodiacDragon, "nameJa", "ちび竜の芽ぶく角") => "竜角",
            (InkSkinId.ZodiacSnake, "nameKo", "실뱀의 느슨한 매듭") => "뱀 매듭",
            (InkSkinId.ZodiacSnake, "nameEn", "Little Snake's Loose Knot") => "Serpent Knot",
            (InkSkinId.ZodiacSnake, "nameJa", "小へびのゆる結び") => "蛇の結び目",
            (InkSkinId.ZodiacHorse, "nameKo", "바람말의 흩날린 갈기") => "갈기",
            (InkSkinId.ZodiacHorse, "nameEn", "Wind Horse's Tousled Mane") => "Mane",
            (InkSkinId.ZodiacHorse, "nameJa", "風馬のなびくたてがみ") => "たてがみ",
            (InkSkinId.ZodiacGoat, "nameKo", "산양의 굽은 뿔") => "산양",
            (InkSkinId.ZodiacGoat, "nameEn", "Curled Ram Horns") => "Mountain Goat",
            (InkSkinId.ZodiacGoat, "nameJa", "山羊の巻き角") => "山羊",
            (InkSkinId.ZodiacRooster, "nameKo", "새벽닭의 붉은 볏") => "닭벼슬",
            (InkSkinId.ZodiacRooster, "nameEn", "Dawn Rooster's Red Comb") => "Rooster Comb",
            (InkSkinId.ZodiacRooster, "nameJa", "朝どりの赤いとさか") => "とさか",
            (InkSkinId.ZodiacDog, "nameKo", "지킴개의 늘어진 귀") => "사냥개",
            (InkSkinId.ZodiacDog, "nameEn", "Guardian Dog's Floppy Ears") => "Hound",
            (InkSkinId.ZodiacDog, "nameJa", "見守り犬のたれ耳") => "猟犬",
            (InkSkinId.ZodiacPig, "nameKo", "복돼지 코") => "돼지코",
            (InkSkinId.ZodiacPig, "nameEn", "Lucky Pig Snout") => "Pig Snout",
            (InkSkinId.ZodiacPig, "nameJa", "福ぶたの鼻") => "豚の鼻",
            (InkSkinId.ThreeLeggedCrow, "nameKo", "삼족오의 해") => "삼족오",
            (InkSkinId.ThreeLeggedCrow, "nameEn", "Three-Legged Sun Crow") => "Three-Legged Crow",
            (InkSkinId.ThreeLeggedCrow, "nameJa", "三足烏の太陽") => "三足烏",
            (InkSkinId.NineTails, "nameKo", "구미호 꼬리") => "구미호",
            (InkSkinId.NineTails, "nameEn", "Nine Fox Tails") => "Nine-Tailed Fox",
            (InkSkinId.NineTails, "nameJa", "九尾のしっぽ") => "九尾の狐",
            (InkSkinId.FirstRibbon, "loreKo", "풀 줄 몰라서 아직 첫 매듭이다.") => "처음 묶은 모양 그대로.",
            (InkSkinId.FirstRibbon, "loreEn", "Still the first knot. Nobody can untie it.") => "Still tied as it was the first day.",
            (InkSkinId.FirstRibbon, "loreJa", "ほどけないので、まだ最初の結び目。") => "初めて結んだ、そのままの形。",
            (InkSkinId.Scholar, "loreKo", "붓보다 낮잠에 더 익숙한 선비의 관.") => "벼슬보다 바람이 잘 통한다.",
            (InkSkinId.Scholar, "loreEn", "Its scholar preferred naps to calligraphy.") => "Better for catching a breeze than gaining rank.",
            (InkSkinId.Scholar, "loreJa", "書より昼寝が得意な書生の冠。") => "出世よりも風通しがいい。",
            (InkSkinId.LotusCrown, "loreKo", "연못지기가 잃어버린 작은 왕관.") => "물 위에 남은 꽃 한 송이.",
            (InkSkinId.LotusCrown, "loreEn", "The pond keeper's missing little crown.") => "A single blossom left on the water.",
            (InkSkinId.LotusCrown, "loreJa", "池の番人がなくした小さな冠。") => "水面に残った一輪の花。",
            (InkSkinId.BellRibbon, "loreKo", "소리가 안 나서 도둑이 탐냈다.") => "소리 없이 흔들리는 방울.",
            (InkSkinId.BellRibbon, "loreEn", "A thief coveted this silent bell.") => "A bell that sways without a sound.",
            (InkSkinId.BellRibbon, "loreJa", "音がしないので、泥棒が欲しがった。") => "音もなく揺れる鈴。",
            (InkSkinId.CloudCap, "loreKo", "구름 한 조각을 말렸더니 줄어들었다.") => "잠시 머물다 갈 구름 한 조각.",
            (InkSkinId.CloudCap, "loreEn", "One cloud, shrunk in the wash.") => "A passing cloud, resting for a while.",
            (InkSkinId.CloudCap, "loreJa", "雲をひとかけ洗ったら、縮んだ。") => "しばらく留まる、一片の雲。",
            (InkSkinId.PaperBoat, "loreKo", "강 대신 머리 위를 항해하기로 했다.") => "아직 물에 띄운 적은 없다.",
            (InkSkinId.PaperBoat, "loreEn", "It chose a head over the open river.") => "It has never touched the water.",
            (InkSkinId.PaperBoat, "loreJa", "川ではなく、頭の上を旅することにした。") => "まだ水に浮かべたことはない。",
            (InkSkinId.TeaCupHat, "loreKo", "차는 식었지만 고집은 따뜻하다.") => "차는 식었고 향은 남았다.",
            (InkSkinId.TeaCupHat, "loreEn", "The tea is cold. The stubbornness is warm.") => "The tea has cooled. Its scent remains.",
            (InkSkinId.TeaCupHat, "loreJa", "お茶は冷めても、意地はまだ温かい。") => "茶は冷め、香りが残った。",
            (InkSkinId.FeatherHairpin, "loreKo", "학은 빠진 깃 하나를 아직 찾고 있다.") => "주인 모를 깃 하나를 꽂았다.",
            (InkSkinId.FeatherHairpin, "loreEn", "A crane is still looking for this feather.") => "A feather left behind, worn as a pin.",
            (InkSkinId.FeatherHairpin, "loreJa", "鶴はまだ、この羽を探している。") => "持ち主の知れない羽を一本、挿した。",
            (InkSkinId.FoxMask, "loreKo", "속인 건 아무도 없고, 본인만 신났다.") => "웃는 얼굴 뒤는 알 수 없다.",
            (InkSkinId.FoxMask, "loreEn", "Fooled nobody. Had a wonderful time.") => "No telling what lies behind the smile.",
            (InkSkinId.FoxMask, "loreJa", "誰もだませず、自分だけ大喜び。") => "笑顔の裏はわからない。",
            (InkSkinId.CraneCrest, "loreKo", "높이 날던 학도 빗질은 서툴렀다.") => "날개에서 떨어져 여기까지 왔다.",
            (InkSkinId.CraneCrest, "loreEn", "A fine flier. A terrible hairdresser.") => "Shed from a wing, it drifted this far.",
            (InkSkinId.CraneCrest, "loreJa", "高く飛ぶ鶴も、髪を整えるのは苦手。") => "翼を離れ、ここまで流れ着いた。",
            (InkSkinId.MoonCrown, "loreKo", "달에서 빌렸다. 반납일은 모른다.") => "기울지 않는 초승달.",
            (InkSkinId.MoonCrown, "loreEn", "Borrowed from the moon. No due date.") => "A crescent that never wanes.",
            (InkSkinId.MoonCrown, "loreJa", "月から借りた。返す日は知らない。") => "欠けることのない三日月。",
            (InkSkinId.BrushMaster, "loreKo", "장인은 붓을 찾고, 붓은 여기 있다.") => "붓을 쥘 손이 없어 머리에 꽂았다.",
            (InkSkinId.BrushMaster, "loreEn", "The master is looking for these brushes.") => "No hands to hold them, so here they rest.",
            (InkSkinId.BrushMaster, "loreJa", "職人は筆を探している。筆はここにある。") => "筆を持つ手がないので、頭に挿した。",
            (InkSkinId.JadeCrown, "loreKo", "돌인 줄 알고 문을 괴어 두던 용관.") => "오래된 옥에 빛이 남아 있다.",
            (InkSkinId.JadeCrown, "loreEn", "Once mistaken for a very good doorstop.") => "A trace of light remains in the old jade.",
            (InkSkinId.JadeCrown, "loreJa", "ただの石だと思われ、戸止めにされていた。") => "古い玉に、光が残っている。",
            (InkSkinId.TigerMask, "loreKo", "산군도 가끔은 귀여워 보이고 싶다.") => "눈을 마주치면 먼저 피하게 된다.",
            (InkSkinId.TigerMask, "loreEn", "Even a mountain tiger wants to look cute.") => "Meet its eyes, and you look away first.",
            (InkSkinId.TigerMask, "loreJa", "山の虎も、たまには可愛く見られたい。") => "目が合うと、こちらが先にそらす。",
            (InkSkinId.MountainKing, "loreKo", "산꼭대기에 먼저 앉은 먹의 왕관.") => "산을 내려온 뒤에도 벗지 않았다.",
            (InkSkinId.MountainKing, "loreEn", "Crown of the first Ink to sit on the summit.") => "Kept on, even after leaving the mountain.",
            (InkSkinId.MountainKing, "loreJa", "山頂に一番乗りした墨の冠。") => "山を下りてからも、脱がなかった。",
            (InkSkinId.CloudDragon, "loreKo", "승천 연습은 아직 구름 속에서만.") => "구름 사이로 뿔이 드러난다.",
            (InkSkinId.CloudDragon, "loreEn", "Still practicing ascension inside a cloud.") => "Horns emerge between the clouds.",
            (InkSkinId.CloudDragon, "loreJa", "昇天の練習は、まだ雲の中だけ。") => "雲の間から角がのぞく。",
            (InkSkinId.InkMecha, "loreKo", "대장간의 실패작. 종이로는 첫 성공작.") => "차가운 겉모습 안에는 같은 먹이 있다.",
            (InkSkinId.InkMecha, "loreEn", "Failed in iron. A triumph in cardboard.") => "The same Ink beneath a cold exterior.",
            (InkSkinId.InkMecha, "loreJa", "鉄では失敗作。紙では初の成功作。") => "冷たい外見の奥には、いつもの墨がいる。",
            (InkSkinId.CraneWings, "loreKo", "빌린 날개라 깃털은 돌려줘야 한다.") => "접어 두었던 날개를 펼친다.",
            (InkSkinId.CraneWings, "loreEn", "Borrowed wings. Please return every feather.") => "Wings, folded until now, open.",
            (InkSkinId.CraneWings, "loreJa", "借りた翼なので、羽は全部返すこと。") => "畳んでいた翼を広げる。",
            (InkSkinId.BlueWisps, "loreKo", "무섭게 따라오려다 산책이 되어 버렸다.") => "바람이 없어도 불은 흔들린다.",
            (InkSkinId.BlueWisps, "loreEn", "They meant to haunt you. It turned into a stroll.") => "The flames stir without a breeze.",
            (InkSkinId.BlueWisps, "loreJa", "脅かすつもりが、おさんぽになった。") => "風がなくても、火は揺れる。",
            (InkSkinId.PaperLanterns, "loreKo", "집으로 가는 길은 모르지만 일단 따라온다.") => "어디로 가는지도 모르고 따라온다.",
            (InkSkinId.PaperLanterns, "loreEn", "They forgot the way home, so they follow you.") => "They follow without knowing the way.",
            (InkSkinId.PaperLanterns, "loreJa", "帰り道は忘れたけれど、とりあえずついてくる。") => "行き先も知らずについてくる。",
            (InkSkinId.MoonClouds, "loreKo", "달이 낮잠 잘 자리를 잘못 골랐다.") => "구름에 가려도 달은 남아 있다.",
            (InkSkinId.MoonClouds, "loreEn", "The moon picked the wrong place for a nap.") => "The moon remains behind the clouds.",
            (InkSkinId.MoonClouds, "loreJa", "月がお昼寝する場所を間違えた。") => "雲に隠れても、月はそこにある。",
            (InkSkinId.DragonTail, "loreKo", "한 번 부딪혔을 뿐인데 꼬리가 따라왔다.") => "스쳐 간 용이 흔적을 남겼다.",
            (InkSkinId.DragonTail, "loreEn", "One little bump. A tail followed me home.") => "A passing dragon left its mark.",
            (InkSkinId.DragonTail, "loreJa", "一度ぶつかっただけなのに、しっぽがついてきた。") => "通り過ぎた竜が、跡を残した。",
            (InkSkinId.ZodiacOx, "loreKo", "스무 번 꾹꾹, 일 분 버티기") => "스무 번 딛고, 일 분 버티기",
            (InkSkinId.ZodiacOx, "loreEn", "Twenty firm steps, one minute") => "Twenty landings, one minute",
            (InkSkinId.ZodiacOx, "loreJa", "二十回ふみふみ、一分がまん") => "二十回着地して、一分耐える",
            (InkSkinId.ZodiacSnake, "loreKo", "거미는 열두 번 콩콩") => "거미의 열두 번째 착지",
            (InkSkinId.ZodiacSnake, "loreEn", "The spider hops twelve times") => "The spider's twelfth landing",
            (InkSkinId.ZodiacSnake, "loreJa", "蜘蛛は十二回ぴょんぴょん") => "蜘蛛の十二回目の着地",
            (InkSkinId.ZodiacPig, "loreKo", "열둘 먹고 꿀꿀") => "열두 개도 모자라",
            (InkSkinId.ZodiacPig, "loreEn", "Twelve treats, oink oink") => "Twelve is not enough",
            (InkSkinId.ZodiacPig, "loreJa", "十二個食べて、ぶうぶう") => "十二個でも足りない",
            (InkSkinId.TurtleShell, "nameKo", "거북 투구") => "현무",
            (InkSkinId.TurtleShell, "nameEn", "Turtle Helm") => "Hyunmu",
            (InkSkinId.TurtleShell, "nameJa", "亀のかぶと") => "玄武",
            (InkSkinId.SpiritHalo, "nameKo", "금빛 후광") => "윤회의 고리",
            (InkSkinId.SpiritHalo, "nameEn", "Golden Halo") => "Ring of Rebirth",
            (InkSkinId.SpiritHalo, "nameJa", "金色の後光") => "輪廻の環",
            (InkSkinId.BondKnot, "nameKo", "인연 매듭") => "무스비",
            (InkSkinId.BondKnot, "nameEn", "Bond Knot") => "Musubi",
            (InkSkinId.BondKnot, "nameJa", "縁結び") => "むすび",
            (InkSkinId.AngelWings, "nameKo", "천사링") => "여명의 고리",
            (InkSkinId.AngelWings, "nameEn", "Angel Ring") => "Ring of Dawn",
            (InkSkinId.AngelWings, "nameJa", "天使の輪") => "夜明けの環",
            (InkSkinId.SpiderMask, "loreKo", "이웃의 빨래줄까지 지키는 작은 영웅.") => "거미에게 물린 적은 없다",
            (InkSkinId.SpiderMask, "loreEn", "A little neighborhood hero. Even the clotheslines are safe.") => "Never been bitten by a spider",
            (InkSkinId.SpiderMask, "loreJa", "ご近所の物干しロープまで守る、小さなヒーロー。") => "蜘蛛に噛まれたことはない",
            (InkSkinId.YakshaMask, "loreKo", "탈은 웃고 있지만 주먹은 웃지 않는다.") => "정체는 비밀인거같다.",
            (InkSkinId.YakshaMask, "loreEn", "The mask smiles. The fists do not.") => "Seems to keep its identity a secret.",
            (InkSkinId.YakshaMask, "loreJa", "仮面は笑うが、拳は笑わない。") => "正体は秘密らしい。",
            (InkSkinId.SpiritHalo, "loreKo", "뒤가 환해졌지만 득도한 것은 아니다.") => "끝은 또다른 시작",
            (InkSkinId.SpiritHalo, "loreEn", "A radiant ring. Enlightenment sold separately.") => "Every end is another beginning",
            (InkSkinId.SpiritHalo, "loreJa", "背中は輝いたが、悟ったわけではない。") => "終わりは新たな始まり",
            (InkSkinId.BondKnot, "loreKo", "인연을 묶었더니 발이 먼저 떠올랐다.") => "서로 다른 시간을 하나로 묶은 매듭",
            (InkSkinId.BondKnot, "loreEn", "One little bond. Two feet off the ground.") => "A knot that binds different times together",
            (InkSkinId.BondKnot, "loreJa", "縁を結んだら、足が先に浮かんだ。") => "異なる時間をひとつに結んだ結び目",
            (InkSkinId.WindFan, "loreKo", "불을 끄려다 주인부터 날려 버렸다.") => "부채를 펼치면, 바람의 흐름이 바뀔거같다.",
            (InkSkinId.WindFan, "loreEn", "Meant to cool the fire. Blew its owner away.") => "Open the fan, and the wind might change its course.",
            (InkSkinId.WindFan, "loreJa", "火を消すつもりが、持ち主を飛ばした。") => "扇を開けば、風の流れが変わりそうだ。",
            (InkSkinId.AngelWings, "loreKo", "하늘과 지옥 사이에서도 퇴근 시간은 지킨다.") => "돌아갈 곳을 잃은 빛이 먹 위에 머문다",
            (InkSkinId.AngelWings, "loreEn", "Even between heaven and hell, it clocks out on time.") => "A light with nowhere to return rests above Ink",
            (InkSkinId.AngelWings, "loreJa", "天と地獄の間でも、定時には帰る。") => "帰る場所を失った光が、墨の上にとどまる",
            (InkSkinId.SpiderMask, "abilityNameKo", "거미줄 오르기") => "먹슈트",
            (InkSkinId.SpiderMask, "abilityNameEn", "Web Ascent") => "Ink Suit",
            (InkSkinId.SpiderMask, "abilityNameJa", "糸のぼり") => "墨スーツ",
            (InkSkinId.YakshaMask, "abilityNameKo", "퇴마의 일격") => "탈춤",
            (InkSkinId.YakshaMask, "abilityNameEn", "Banishing Strike") => "Mask Dance",
            (InkSkinId.YakshaMask, "abilityNameJa", "退魔の一撃") => "仮面舞",
            (InkSkinId.TurtleShell, "abilityNameKo", "시작 방어막") => "금강",
            (InkSkinId.TurtleShell, "abilityNameEn", "Starting Shield") => "Vajra",
            (InkSkinId.TurtleShell, "abilityNameJa", "開幕の盾") => "金剛",
            (InkSkinId.SpiritHalo, "abilityNameKo", "먹의 환원") => "윤회",
            (InkSkinId.SpiritHalo, "abilityNameEn", "Ink Return") => "Rebirth",
            (InkSkinId.SpiritHalo, "abilityNameJa", "墨の還元") => "輪廻",
            (InkSkinId.BondKnot, "abilityNameKo", "첫걸음 도약") => "인연의 이끌림",
            (InkSkinId.BondKnot, "abilityNameEn", "First Leap") => "Bond's Pull",
            (InkSkinId.BondKnot, "abilityNameJa", "はじまりの跳躍") => "縁の導き",
            _ => text
        };
        static string Optional(JObject row, string key, int max)
        {
            var token = row[key];
            if (token != null && token.Type is not (JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Null))
                throw new FormatException(key + " 셀 형식 오류");
            string value = token?.ToString() ?? "";
            if (value.Length > max || value.Contains('\\') || value.Contains('<') || value.Contains('>'))
                throw new FormatException(key + " 길이 또는 특수문자 오류");
            foreach (char c in value) if (char.IsControl(c) && c != '\n' && c != '\r') throw new FormatException(key + " 제어문자 오류");
            return value.Trim();
        }
        static string Required(JObject row, string key, int max)
        {
            string text = Optional(row, key, max);
            return text.Length > 0 ? text : throw new FormatException(key + " 빈 셀");
        }
        static int Integer(JObject row, string key, int min, int max) =>
            int.TryParse(Required(row, key, 20), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max
                ? value : throw new FormatException(key + " 정수 범위 오류");
        public static void Apply(Snapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            // 다운로드 콜백은 결과를 예약하고, 런타임 로더가 안전한 로비에서만 적용한다.
            current = snapshot;
            InkSkinCatalog.RefreshBrowseOrder();
            InkSkinTranslations.Refresh();
            GameLocalization.NotifyChanged();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            current = null;
            InkSkinCatalog.RefreshBrowseOrder();
            InkSkinTranslations.Refresh();
        }
    }
}
