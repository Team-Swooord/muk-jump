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
                ["같은 해금 조건끼리"] = ("Grouped by unlock goal", "解放条件ごとに表示"),
                ["기본 의상"] = ("Starter outfit", "基本の衣装"),
                ["최고 고도"] = ("Best height", "最高高度"),
                ["플레이 횟수"] = ("Runs completed", "プレイ回数"),
                ["누적 고도"] = ("Total height", "累計高度"),
                ["발판 착지"] = ("Platform landings", "足場への着地"),
                ["먹방울 수집"] = ("Ink Drop collection", "墨滴集め"),
                ["아이템 수집"] = ("Item collection", "アイテム集め"),
                ["생존 시간"] = ("Survival time", "生存時間"),
                ["분신"] = ("Clones", "分身"),
                ["밤의 고도"] = ("Night height", "夜の高度"),
                ["아이템 없는 도전"] = ("No-item challenge", "アイテムなしの挑戦"),
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
                var lore=InkSkinLore.Get(id);
                result[lore.ko]=(lore.en,lore.ja);
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
    /// 효과 수치와 분리된 짧은 창작 뒷이야기. 발견 전 비밀 의상에는 표시하지 않는다.
    public static class InkSkinLore
    {
        public static (string ko, string en, string ja) Get(InkSkinId id) => id switch {
            InkSkinId.Ink => ("벼루 밖으로 처음 굴러 나온 날.", "The day a drop rolled out of the inkstone.", "硯の外へ、はじめて転がり出た日。"),
            InkSkinId.Gat => ("주인은 날아갔고, 갓만 돌아왔다.", "The hat came back. Its owner did not.", "持ち主は飛んでいき、笠だけ戻った。"),
            InkSkinId.SpiderMask => ("처마 밑 거미에게 빌린 얼굴.", "A face borrowed from a spider in the eaves.", "軒下の蜘蛛から借りた顔。"),
            InkSkinId.FirstRibbon => ("풀 줄 몰라서 아직 첫 매듭이다.", "Still the first knot. Nobody can untie it.", "ほどけないので、まだ最初の結び目。"),
            InkSkinId.Scholar => ("붓보다 낮잠에 더 익숙한 선비의 관.", "Its scholar preferred naps to calligraphy.", "書より昼寝が得意な書生の冠。"),
            InkSkinId.InkGhost => ("다 쓴 종이도 가끔 꿈을 꾼다.", "Even used paper dreams sometimes.", "使い終えた紙も、ときどき夢を見る。"),
            InkSkinId.BambooHat => ("비는 막아도 먹 번짐은 못 막는다.", "Keeps out rain. Ink stains, not so much.", "雨は防げても、墨のにじみは防げない。"),
            InkSkinId.RedHeadband => ("백 번 넘어져도 매듭은 그대로였다.", "A hundred falls. The knot held firm.", "百回転んでも、結び目はそのまま。"),
            InkSkinId.LotusCrown => ("연못지기가 잃어버린 작은 왕관.", "The pond keeper's missing little crown.", "池の番人がなくした小さな冠。"),
            InkSkinId.CatEars => ("생선 냄새가 나면 살짝 기울어진다.", "Tilts ever so slightly toward fish.", "魚の匂いがすると、少し傾く。"),
            InkSkinId.BellRibbon => ("소리가 안 나서 도둑이 탐냈다.", "A thief coveted this silent bell.", "音がしないので、泥棒が欲しがった。"),
            InkSkinId.CloudCap => ("구름 한 조각을 말렸더니 줄어들었다.", "One cloud, shrunk in the wash.", "雲をひとかけ洗ったら、縮んだ。"),
            InkSkinId.PaperBoat => ("강 대신 머리 위를 항해하기로 했다.", "It chose a head over the open river.", "川ではなく、頭の上を旅することにした。"),
            InkSkinId.TeaCupHat => ("차는 식었지만 고집은 따뜻하다.", "The tea is cold. The stubbornness is warm.", "お茶は冷めても、意地はまだ温かい。"),
            InkSkinId.FeatherHairpin => ("학은 빠진 깃 하나를 아직 찾고 있다.", "A crane is still looking for this feather.", "鶴はまだ、この羽を探している。"),
            InkSkinId.PeachFlower => ("봄이 급히 두고 간 물건.", "Spring left this behind in a hurry.", "春があわてて置いていったもの。"),
            InkSkinId.FoxMask => ("속인 건 아무도 없고, 본인만 신났다.", "Fooled nobody. Had a wonderful time.", "誰もだませず、自分だけ大喜び。"),
            InkSkinId.CraneCrest => ("높이 날던 학도 빗질은 서툴렀다.", "A fine flier. A terrible hairdresser.", "高く飛ぶ鶴も、髪を整えるのは苦手。"),
            InkSkinId.YakshaMask => ("무서운 얼굴은 동네 아이가 그렸다.", "A village child drew the scary face.", "怖い顔は、近所の子が描いた。"),
            InkSkinId.TurtleShell => ("느린 장인이 끝내 완성한 투구.", "A very slow craftsman finally finished it.", "のんびり屋の職人が、ついに仕上げた。"),
            InkSkinId.StormScarf => ("바람을 묶으려다 두건이 되었다.", "An attempt to tie up the wind.", "風を結ぼうとしたら、頭巾になった。"),
            InkSkinId.MoonCrown => ("달에서 빌렸다. 반납일은 모른다.", "Borrowed from the moon. No due date.", "月から借りた。返す日は知らない。"),
            InkSkinId.BrushMaster => ("장인은 붓을 찾고, 붓은 여기 있다.", "The master is looking for these brushes.", "職人は筆を探している。筆はここにある。"),
            InkSkinId.SpiritHalo => ("꺼진 줄 알았던 불씨가 따라왔다.", "An ember thought extinguished followed home.", "消えたはずの火種が、ついてきた。"),
            InkSkinId.KoiMask => ("용문 앞에서 잠깐 쉬어 가는 중.", "Taking a short rest before the Dragon Gate.", "竜門の手前で、ちょっとひと休み。"),
            InkSkinId.JadeCrown => ("돌인 줄 알고 문을 괴어 두던 용관.", "Once mistaken for a very good doorstop.", "ただの石だと思われ、戸止めにされていた。"),
            InkSkinId.PhoenixCrown => ("타지 않은 깃은 딱 다섯 개였다.", "Exactly five feathers escaped the fire.", "燃え残った羽は、ちょうど五枚だった。"),
            InkSkinId.TigerMask => ("산군도 가끔은 귀여워 보이고 싶다.", "Even a mountain tiger wants to look cute.", "山の虎も、たまには可愛く見られたい。"),
            InkSkinId.MountainKing => ("산꼭대기에 먼저 앉은 먹의 왕관.", "Crown of the first Ink to sit on the summit.", "山頂に一番乗りした墨の冠。"),
            InkSkinId.CloudDragon => ("승천 연습은 아직 구름 속에서만.", "Still practicing ascension inside a cloud.", "昇天の練習は、まだ雲の中だけ。"),
            InkSkinId.InkMecha => ("대장간의 실패작. 종이로는 첫 성공작.", "Failed in iron. A triumph in cardboard.", "鉄では失敗作。紙では初の成功作。"),
            InkSkinId.NightBat => ("밤마다 지붕을 지킨다. 낮에는 잔다.", "Guards the rooftops by night. Naps by day.", "夜は屋根を守る。昼は寝る。"),
            InkSkinId.Astronaut => ("달에 떡이 있는지 확인하러 간다.", "Off to check if the moon has rice cakes.", "月にお餅があるか、確かめに行く。"),
            InkSkinId.ClockworkCat => ("태엽을 감으면 오히려 잠이 든다.", "Winding it up only puts it to sleep.", "ぜんまいを巻くと、かえって眠くなる。"),
            InkSkinId.LuckyDice => ("일곱 번째 눈은 굴린 먹만 보인다.", "Only the roller can see the seventh face.", "七つ目の目は、振った墨にしか見えない。"),
            InkSkinId.OrigamiWings => ("접는 순서를 틀렸더니 문이 열렸다.", "One wrong fold opened a doorway.", "折る順番を間違えたら、扉が開いた。"),
            InkSkinId.CraneWings => ("빌린 날개라 깃털은 돌려줘야 한다.", "Borrowed wings. Please return every feather.", "借りた翼なので、羽は全部返すこと。"),
            InkSkinId.ButterflyWings => ("먹이 묻은 줄 모르고 날아온 나비.", "A butterfly unaware of its ink stains.", "墨がついたとも知らず、飛んできた蝶。"),
            InkSkinId.RedShoes => ("발자국부터 먼저 길을 나섰다.", "The footprints set off before their owner.", "足跡のほうが、先に旅に出た。"),
            InkSkinId.CloudBoots => ("비 오는 날엔 양말까지 구름이 된다.", "On rainy days, even the socks turn to cloud.", "雨の日には、靴下まで雲になる。"),
            _ => ("", "", "")
        };
    }
}
