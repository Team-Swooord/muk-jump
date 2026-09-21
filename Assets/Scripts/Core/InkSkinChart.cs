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
