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
