using System.IO;
using System.Globalization;
using MukJump.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MukJump.EditorTools
{
    public static class InkSkinChartExport
    {
        public static JArray Rows()
        {
            var rows = new JArray();
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                if (!InkSkinCatalog.IsAvailable(id)) continue;
                var d = InkSkinCatalog.Get(id);
                var lore = InkSkinLore.Get(id);
                string rule = InkSkinAchievements.Requirement(id);
                bool ability = InkSkinCatalog.HasAbility(id);
                string name = ability ? InkSkinCatalog.Ability(id) : "";
                string description = ability ? InkSkinCatalog.Description(id) : "";
                string cd = InkSkinChart.Cooldown(id).ToString("0.###", CultureInfo.InvariantCulture);
                string Template(string text) => InkSkinChart.DefaultCooldown(id) > 0 ? text.Replace(cd, "{cooldown}") : text;
                rows.Add(new JObject {
                    ["schemaVersion"] = "1", ["skinId"] = i.ToString(), ["skinKey"] = id.ToString(),
                    ["nameKo"] = d.Korean, ["nameEn"] = d.English, ["nameJa"] = d.Japanese,
                    ["loreKo"] = lore.ko, ["loreEn"] = lore.en, ["loreJa"] = lore.ja,
                    ["tier"] = d.Tier.ToString(), ["unlockMetric"] = d.Metric.ToString(), ["unlockGoal"] = d.Goal.ToString(),
                    ["goalEditable"] = InkSkinChart.GoalEditable(id) ? "YES" : "NO",
                    ["unlockRuleKo"] = rule, ["unlockRuleEn"] = Translate(rule, false), ["unlockRuleJa"] = Translate(rule, true),
                    ["abilityKey"] = ability ? id.ToString() : "None",
                    ["abilityNameKo"] = name, ["abilityNameEn"] = Translate(name, false), ["abilityNameJa"] = Translate(name, true),
                    ["abilityDescriptionKo"] = Template(description), ["abilityDescriptionEn"] = Template(Translate(description, false)),
                    ["abilityDescriptionJa"] = Template(Translate(description, true)), ["cooldownSeconds"] = cd,
                    ["assetKey"] = d.Asset
                });
            }
            return rows;
        }
        static string Translate(string ko, bool japanese) =>
            GameLocalization.Translate(ko, japanese ? GameLanguage.Japanese : GameLanguage.English);
        public static string ExportJson()
        {
            const string path = "outputs/skin-chart-20260914/skin-catalog.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = Rows().ToString(Formatting.Indented);
            if (!InkSkinChart.TryParse(json, out _, out string error)) throw new System.InvalidOperationException(error);
            File.WriteAllText(path, json);
            return Path.GetFullPath(path);
        }
    }
}
