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
                ["의상 시험실"] = ("Outfit test room", "衣装テスト"),
                ["닫기"] = ("Close", "閉じる"),
                ["해금 연출 보기"] = ("Preview unlock", "解放演出を見る"),
                ["히든 해금 연출"] = ("Preview secret unlock", "秘密の解放演出"),
                ["전체 의상 해금 ON"] = ("Unlock all outfits ON", "全衣装を解放 ON"),
                ["전체 의상 해금 OFF"] = ("Unlock all outfits OFF", "全衣装を解放 OFF"),
                ["시험 착용 중 · 플레이 기록 저장 안 됨"] = ("Test outfits · runs are not saved", "試着中 · プレイ記録は保存されません"),
                ["실제 기록은 바뀌지 않아요"] = ("Your real progress stays unchanged", "実際の進行状況は変わりません"),
                ["꾸미기"] = ("Wardrobe", "着せ替え"),
                ["전체 보기"] = ("View all", "一覧"),
                ["크게 보기"] = ("Preview", "試着"),
                ["먹의 환원"] = ("Ink Return", "墨の還元"),
                ["시작 방어막"] = ("Starting Shield", "開幕の盾"),
                ["봉황의 숨결"] = ("Phoenix Breath", "鳳凰の息吹"),
                ["분신이 쓰러질 때마다 본체 체력을 1칸 회복해요.\n최대 체력을 넘거나 쓰러진 본체를 되살리지는 않아요."] =
                    ("Each fallen clone restores 1 HP to Ink.\nCannot exceed max HP or revive Ink.", "分身が倒れるたび、本体の体力を1回復。\n最大体力を超えず、倒れた本体は蘇りません。"),
                ["매 판 본체에게 방어막 1개를 주고 시작해요.\n충돌·추락을 한 번 막으며 분신에게는 적용되지 않아요."] =
                    ("Start each run with a shield on Ink.\nBlocks one hit or fall; clones excluded.", "毎回、本体に盾を1つ付けてスタート。\n衝突・落下を1回防ぎます。分身は対象外です。"),
                ["한 판에 한 번 본체가 체력 1칸으로 살아남아 50m 올라가요.\n성장 부활과 횟수를 공유하며 분신에게는 적용되지 않아요."] =
                    ("Survive one fatal hit with 1 HP per run.\nRise 50m; shared with growth revival.", "1プレイに1回、本体が体力1で耐えて50m上昇。\n成長の復活と回数を共有。分身は対象外です。"),
                ["새 의상 해금"] = ("Costume Unlocked", "衣装を解放"),
                ["비밀 의상 발견"] = ("Secret Discovered", "秘密の衣装を発見"),
                ["해금"] = ("NEW", "解放"),
                ["다음 의상"] = ("Next Costume", "次の衣装"),
                ["모두 확인"] = ("Done", "すべて確認"),
                ["업적 달성"] = ("Achievement Unlocked", "実績を達成"),
                ["벽을 타는 먹"] = ("Wall Walker", "壁を渡る墨"),
                ["한 번에 셋"] = ("Three in One Flight", "一度の飛翔で三つ"),
                ["벽타기로 벽에 붙기"] = ("Cling to a wall with Wall Cling", "壁登りで壁に張り付く"),
                ["먹방울 상승 한 번으로 추가 아이템 3개 획득"] = ("Collect 3 more items during one Ink Drop ascent", "墨滴による1回の上昇中に追加アイテムを3個獲得"),
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
