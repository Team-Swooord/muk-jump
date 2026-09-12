using System.Collections.Generic;

namespace MukJump.Core
{
    /// 기존 한국어 원문 기반 UI 번역 체계를 그대로 확장한다.
    public static class InkSkinTranslations
    {
        static readonly Dictionary<string, (string en, string ja)> entries = Build();
        public static bool TryTranslate(string source, bool japanese, out string text)
        {
            if (entries.TryGetValue(source, out var pair)) { text = japanese ? pair.ja : pair.en; return true; }
            text = null; return false;
        }
        static Dictionary<string, (string en, string ja)> Build()
        {
            var result = new Dictionary<string, (string, string)>
            {
                ["쇼룸"] = ("Showroom", "ショールーム"),
                ["새 의상 해금"] = ("Costume Unlocked", "衣装を解放"),
                ["비밀 의상 발견"] = ("Secret Discovered", "秘密の衣装を発見"),
                ["해금"] = ("NEW", "解放"),
                ["다음 의상"] = ("Next Costume", "次の衣装"),
                ["모두 확인"] = ("Done", "すべて確認"),
                ["먹의 장식"] = ("Ink Accessory", "墨の飾り"),
                ["비밀 의상"] = ("Secret Costume", "秘密の衣装"),
                ["숨겨진 행동으로 발견해 보세요."] = ("Discover it through a hidden action.", "隠された行動で見つけよう。"),
                ["원래 먹에 장식을 더해요.\n기존 점프와 성장 효과를 유지해요."] = ("Dress up your original Ink.\nJump and growth effects stay unchanged.", "元の墨に飾りを。\nジャンプと成長効果は変わりません。"),
                ["그린 발판에 착지하지 않고 한 판 완료"] = ("Finish a run without landing on a drawn platform", "描いた足場に着地せずプレイを終える"),
                ["로비를 밤으로 바꾸기"] = ("Turn the lobby into night", "ロビーを夜に変える"),
                ["낮과 밤을 여섯 번 바꾸기"] = ("Switch day and night six times", "昼と夜を6回切り替える"),
                ["고양이 귀로 밤에 아이템 없이 60초 생존"] = ("Cat Ears: survive 60s at night without items", "猫耳で夜にアイテムなしで60秒生存"),
                ["정확히 777m로 한 판 완료"] = ("Finish a run at exactly 777m", "ちょうど777mでプレイを終える"),
                ["먹 → 갓 → 먹 → 거미탈 → 먹 순서로 살펴보기"] = ("Browse Ink → Wind Gat → Ink → Spider Mask → Ink", "墨 → 風の笠 → 墨 → 蜘蛛の面 → 墨の順に見る"),
            };
            for (int i=0; i<InkSkinCatalog.Count; i++)
            {
                var id=(InkSkinId)i; var d=InkSkinCatalog.Get(id); int n=d.Goal;
                result[d.Korean]=(d.English,d.Japanese);
                var pair=d.Metric switch
                {
                    SkinMetric.Best => ($"Reach {n}m", $"最高高度{n}mに到達"),
                    SkinMetric.Runs => ($"Finish {n} valid runs", $"通常プレイを{n}回完了"),
                    SkinMetric.Distance => ($"Climb {n}m in total", $"累計高度{n}mに到達"),
                    SkinMetric.DrawnLandings => ($"Land on drawn platforms {n} times", $"描いた足場に{n}回着地"),
                    SkinMetric.Items => ($"Collect {n} items", $"アイテムを{n}回獲得"),
                    SkinMetric.InkDrops => ($"Collect {n} Ink Drop items", $"墨滴アイテムを{n}回獲得"),
                    SkinMetric.Survival => ($"Survive {n}s in one run", $"1回のプレイで{n}秒生存"),
                    SkinMetric.Clones => ($"Keep {n} Inks alive together", $"墨{n}体が同時に生存"),
                    SkinMetric.NightBest => ($"Reach {n}m at night", $"夜に{n}m到達"),
                    SkinMetric.Growth => ($"Own {n} growth nodes", $"成長ノードを{n}個獲得"),
                    SkinMetric.NoItemBest => ($"Reach {n}m without items", $"アイテムなしで{n}m到達"),
                    _ => (null, (string)null),
                };
                if (pair.Item1 != null) result[InkSkinAchievements.Requirement(id)]=pair;
            }
            return result;
        }
    }
}
