using System.Collections.Generic;

namespace MukJump.Core
{
    /// 기존 한국어 원문 기반 UI 번역 체계를 그대로 확장한다.
    public static class InkSkinTranslations
    {
        static Dictionary<string, (string en, string ja)> entries = Build();
        public static void Refresh() => entries = Build();
        public static bool TryTranslate(string source, bool japanese, out string text)
        {
            if (InkSkinChart.TryTranslate(source, japanese, out text)) return true;
            if (entries.TryGetValue(source, out var pair)) { text = japanese ? pair.ja : pair.en; return true; }
            text = null; return false;
        }
        static Dictionary<string, (string en, string ja)> Build()
        {
            var result = new Dictionary<string, (string, string)>
            {
                ["다시 피는 빛"] = ("Light Reborn", "よみがえる光"),
                ["먹슈트"] = ("Ink Suit", "墨スーツ"),
                ["30초마다 가까운 벽에 거미줄을 쏘아 본체를 18m 끌어올린다\n상승 중에는 피해를 막으며 다른 점프 효과가 발동하면 줄을 놓는다"] = ("Every 30 seconds, a web pulls the original Ink 18m toward a nearby wall\nBlocks damage during ascent Another launch effect releases the web", "30秒ごとに近くの壁へ糸を放ち、本体を18m引き上げる\n上昇中はダメージを防ぎ、別の跳躍効果が発動すると糸を離す"),
                ["의상 시험실"] = ("Outfit test room", "衣装テスト"),
                ["닫기"] = ("Close", "閉じる"),
                ["해금 연출 보기"] = ("Preview unlock", "解放演出を見る"),
                ["히든 해금 연출"] = ("Preview secret unlock", "秘密の解放演出"),
                ["전체 의상 해금 ON"] = ("Unlock all outfits ON", "全衣装を解放 ON"),
                ["전체 의상 해금 OFF"] = ("Unlock all outfits OFF", "全衣装を解放 OFF"),
                ["시험 착용 중 · 플레이 기록 저장 안 됨"] = ("Test outfits · runs are not saved", "試着中 · プレイ記録は保存されません"),
                ["실제 기록은 바뀌지 않아요"] = ("Your real progress stays unchanged", "実際の進行状況は変わりません"),
                ["꾸미기"] = ("Wardrobe", "着せ替え"),
                ["기록 없음"] = ("No record", "記録なし"),
                ["특수 능력"] = ("Special", "特殊能力"),
                ["직접 사용"] = ("Manual", "手動発動"),
                ["자동 발동"] = ("Automatic", "自動発動"),
                ["재사용"] = ("Cooldown", "再使用"),
                ["전체 보기"] = ("View all", "一覧"),
                ["크게 보기"] = ("Preview", "試着"),
                ["같은 해금 조건끼리"] = ("Grouped by unlock goal", "解放条件ごとに表示"),
                ["일반 의상"] = ("Regular Outfits", "通常の衣装"),
                ["부활하기"] = ("Revive", "復活する"),
                ["부활 준비 중..."] = ("Preparing revival...", "復活の準備中..."),
                ["광고 제거 · 금색 먹"] = ("No Ads · Golden Ink", "広告なし・金色の墨"),
                ["모든 광고 제거"] = ("Remove All Ads", "すべての広告を削除"),
                ["한 번 구매 · 영구 적용"] = ("Buy once · Keep forever", "一度の購入で永久に"),
                ["금색 먹 포함"] = ("Golden Ink included", "金色の墨つき"),
                ["배너·전면·부활 광고 없이"] = ("No banner, full-screen or revival ads", "バナー・全画面・復活の広告なし"),
                ["부활 한 판 1회 · 기존 조건 유지"] = ("Revive once per run · Usual conditions apply", "復活は1プレイ1回・利用条件は同じ"),
                ["구매 즉시 적용됩니다.\n제공 개시 후 청약철회는 법령·스토어 정책에 따라 제한될 수 있습니다."] =
                    ("Applies upon purchase.\nCancellation rights may be limited after delivery begins, subject to law and store policy.",
                     "購入後すぐに適用されます。\n提供開始後は、法令・ストアの規定により購入の撤回が制限される場合があります。"),
                ["반짝이는 금색 먹 포함"] = ("Includes shimmering Golden Ink", "きらめく金色の墨つき"),
                ["배너 · 전면 광고 제거\n부활도 광고 없이"] = ("No banner or full-screen ads\nAd-free revival, too", "バナー・全画面広告を削除\n復活も広告なし"),
                ["부활은 한 판에 한 번, 기존 조건 그대로"] = ("Revive once per run, with the usual requirements", "復活は1プレイに1回、利用条件は同じです"),
                ["금색 먹은 외형 전용이에요\n점프·성장 능력은 같아요"] = ("Golden Ink is cosmetic only\nSame jumping and growth abilities", "金色の墨は見た目だけの特典です\nジャンプ・成長能力は同じです"),
                ["이전 구매는 설정에서 구입 복원"] = ("Previous purchase? Restore in Settings", "以前の購入は設定から復元できます"),
                ["광고\n제거"] = ("Remove\nAds", "広告\n削除"),
                ["광고\n없음"] = ("No\nAds", "広告\nなし"),
                ["모든 광고 없이 편안하게\n구매 전용 금색 먹 의상"] = ("Enjoy the game without ads\nIncludes the Golden Ink outfit", "すべての広告をなくして快適に\n購入限定の金色の墨つき"),
                ["부활은 광고 없이 이용해요\n한 판에 한 번, 기존 이용 조건은 같아요"] = ("Revive without watching an ad\nOnce per run, with the usual requirements", "広告を見ずに復活できます\n1プレイに1回、利用条件は同じです"),
                ["금색 먹은 모습과 착지 효과만 바뀌어요\n점프와 성장 능력은 그대로예요"] = ("A new look and golden landing particles\nJumping and growth abilities stay the same", "見た目と着地の演出だけが変わります\nジャンプと成長の能力は同じです"),
                ["구매 완료"] = ("Purchased", "購入済み"),
                ["구매하기"] = ("Purchase", "購入する"),
                ["상품 확인 중..."] = ("Loading product...", "商品を確認中..."),
                ["구입 복원"] = ("Restore Purchases", "購入を復元"),
                ["스토어에 연결하고 있어요"] = ("Connecting to the store", "ストアに接続しています"),
                ["한 번 구매로 모든 광고 제거\n금색 먹 의상을 계속 이용해요"] = ("One purchase removes all ads\nKeep the Golden Ink outfit", "1回の購入ですべての広告を削除\n金色の墨をずっと使えます"),
                ["스토어 연결을 다시 시도하고 있어요"] = ("Reconnecting to the store", "ストアへの接続を再試行しています"),
                ["가격을 불러오지 못했어요. 구입 복원은 이용할 수 있어요"] = ("Price unavailable. You can still restore purchases.", "価格を取得できません。購入の復元は利用できます"),
                ["구매 내역을 확인하고 있어요"] = ("Checking your purchases", "購入履歴を確認しています"),
                ["구매는 확인했어요. 저장 후 다시 마무리할게요"] = ("Purchase verified. Finishing after saving.", "購入を確認しました。保存後に完了します"),
                ["광고 제거와 금색 먹을 이용할 수 있어요"] = ("No Ads and Golden Ink are available", "広告なしと金色の墨を利用できます"),
                ["현재 App Store 계정의 구매 내역이 없어요"] = ("No purchase found for this App Store account", "現在のApp Storeアカウントに購入がありません"),
                ["구매 검증을 기다리고 있어요"] = ("Waiting for purchase verification", "購入の検証を待っています"),
                ["구매 승인 대기 중이에요"] = ("Waiting for purchase approval", "購入の承認待ちです"),
                ["한 번 구매하면 계속 이용할 수 있어요"] = ("Buy once and keep it", "1回の購入でずっと利用できます"),
                ["구매 확인을 다시 시도해 주세요. 기존 구매는 유지돼요"] = ("Please retry verification. Existing purchases are kept.", "確認を再試行してください。以前の購入は保持されます"),
                ["구매는 확인했어요. 연결되면 마무리할게요"] = ("Purchase verified. Finishing when connected.", "購入を確認しました。接続後に完了します"),
                ["구매 권한이 변경되었어요. 필요하면 구입을 복원해 주세요"] = ("Purchase access changed. Restore purchases if needed.", "購入状態が変わりました。必要なら購入を復元してください"),
                ["구매를 취소했어요"] = ("Purchase cancelled", "購入をキャンセルしました"),
                ["구매하지 못했어요. 잠시 후 다시 시도해 주세요"] = ("Purchase failed. Please try again later.", "購入できませんでした。後でもう一度お試しください"),
                ["App Store에서 구매를 진행해 주세요"] = ("Complete your purchase in the App Store", "App Storeで購入を進めてください"),
                ["구입을 복원하고 있어요"] = ("Restoring purchases", "購入を復元しています"),
                ["구입 복원을 완료하지 못했어요"] = ("Could not restore purchases", "購入を復元できませんでした"),
                ["스토어 응답을 기다리고 있어요. 구입 복원으로 확인해 주세요"] = ("Waiting for the store. Check with Restore Purchases.", "ストアの応答待ちです。購入の復元で確認してください"),
                ["금색 먹"] = ("Golden Ink", "金色の墨"),
                ["구매 전용 의상"] = ("Premium outfit", "購入限定衣装"),
                ["구매 보기"] = ("View purchase", "購入について"),
                ["익숙한 먹에 금빛을 더해요\n능력과 성장 효과는 그대로예요"] =
                    ("A golden touch for your familiar Ink\nAbilities and growth stay unchanged", "いつもの墨に金色の輝きを\n能力と成長効果は変わりません"),
                ["기본 의상"] = ("Starter outfit", "基本の衣装"),
                ["연동 보상"] = ("Link reward", "連携報酬"),
                ["용과의 만남"] = ("Dragon encounter", "竜との出会い"),
                ["본체가 용과 충돌해 사망하기"] = ("Let the original Ink die from a dragon collision", "本体が竜との衝突で倒れる"),
                ["Apple 계정 연동 완료"] = ("Link your Apple account", "Appleアカウントを連携"),
                ["토스 사용자 확인 완료"] = ("Verify your Toss identity", "トスユーザー確認完了"),
                ["인연의 이끌림"] = ("Bond's Pull", "縁の導き"),
                ["바람 도약"] = ("Wind Leap", "風の跳躍"),
                ["구름 도약"] = ("Cloud Leap", "雲の跳躍"),
                ["탈춤"] = ("Mask Dance", "仮面舞"),
                ["본체가 적과 충돌하면 피해 없이 적을 제거한다\n30초마다 발동하며 가시·낙석에는 적용되지 않는다"] =
                    ("Defeat an enemy on contact without taking damage\nCooldown: 30s Not effective against spikes or falling rocks", "本体が敵に接触すると、ダメージを受けずに敵を倒す\n30秒ごとに発動 トゲ・落石には無効"),
                ["본체가 맵 발판에 착지하면 먹방울 효과로 50m 상승한다\n직접 그린 발판에는 발동하지 않는다"] =
                    ("Land on a map platform to boost Ink 50m with an Ink Drop\nDoes not trigger on drawn platforms", "本体がマップの足場に着地すると、墨滴の効果で50m上昇\n描いた足場では発動しません"),
                ["도약"] = ("Leap", "跳躍"),
                ["준비"] = ("Ready", "準備完了"),
                ["대기"] = ("Wait", "待機"),
                ["매 판 시작 시 먹방울 아이템으로 50m 상승한다\n튜토리얼 종료 후 발동하며 부활 시에는 재발동하지 않는다"] =
                    ("Start each run with a 50m Ink Drop boost\nAfter the tutorial; not granted on revival", "毎回、墨滴で50m上昇してスタート\nチュートリアル後に発動 復活時は再発動しません"),
                ["전용 버튼으로 본체가 25m 도약한다\n상승 중 피해를 막고, 20초 후 다시 사용할 수 있다"] =
                    ("Tap the skill button to leap 25m with Ink\nProtected while rising Cooldown: 20s", "専用ボタンで本体が25m跳躍\n上昇中はダメージを防ぎ、20秒後に再使用できます"),
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
                ["윤회"] = ("Rebirth", "輪廻"),
                ["금강"] = ("Vajra", "金剛"),
                ["봉황의 숨결"] = ("Phoenix Breath", "鳳凰の息吹"),
                ["분신이 쓰러질 때마다 본체 체력을 1칸 회복한다\n최대 체력을 넘거나 쓰러진 본체를 되살리지는 않는다"] =
                    ("Each fallen clone restores 1 HP to Ink\nCannot exceed max HP or revive Ink", "分身が倒れるたび、本体の体力を1回復\n最大体力を超えず、倒れた本体は蘇りません"),
                ["매 판 시작 시 본체에게 방어막 1개를 부여한다\n충돌·추락을 한 번 막으며 분신에게는 적용되지 않는다"] =
                    ("Start each run with a shield on Ink\nBlocks one hit or fall; clones excluded", "毎回、本体に盾を1つ付けてスタート\n衝突・落下を1回防ぎます 分身は対象外です"),
                ["한 판에 한 번 본체가 체력 1칸으로 살아남아 50m 상승한다\n성장 부활과 횟수를 공유하며 분신에게는 적용되지 않는다"] =
                    ("Survive one fatal hit with 1 HP per run\nRise 50m; shared with growth revival", "1プレイに1回、本体が体力1で耐えて50m上昇\n成長の復活と回数を共有 分身は対象外です"),
                ["새 의상 해금"] = ("Costume Unlocked", "衣装を解放"),
                ["바로 장착"] = ("Equip now", "今すぐ着用"),
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
                ["원래 먹에 장식을 더해요\n기존 점프와 성장 효과를 유지해요"] = ("Dress up your original Ink\nJump and growth effects stay unchanged", "元の墨に飾りを\nジャンプと成長効果は変わりません"),
                ["그린 발판에 착지 없이 150m·30초 이상 한 판 완료"] = ("Finish at 150m or higher after 30s without landing on a drawn platform", "描いた足場に着地せず150m以上・30秒以上のプレイを完了"),
                ["로비를 세 번째 밤으로 바꾸기"] = ("Bring on the third night in the lobby", "ロビーで三度目の夜を迎える"),
                ["낮과 밤을 여섯 번 바꾸기"] = ("Switch day and night six times", "昼と夜を6回切り替える"),
                ["고양이 귀로 밤에 아이템 없이 400m·120초 이상 한 판 완료"] = ("Cat Ears: finish a night run at 400m or higher after 120s with no items", "猫耳で夜にアイテムなしで400m以上・120秒以上のプレイを完了"),
                ["정확히 777m로 한 판 완료"] = ("Finish a run at exactly 777m", "ちょうど777mでプレイを終える"),
                ["로비에서 한 번 던진 먹을 벽에 3회 연속 튕기기"] = ("In the lobby, get 3 wall bounces from one toss.", "ロビーで墨を1回投げ、壁で連続3回跳ね返す"),
            };
            for (int i=0; i<InkSkinCatalog.Count; i++)
            {
                if (!InkSkinCatalog.IsAvailable((InkSkinId)i)) continue;
                var id=(InkSkinId)i; var d=InkSkinCatalog.Get(id); int n=d.Goal;
                result[d.Korean]=(d.English,d.Japanese);
                var lore=InkSkinLore.Get(id);
                result[lore.ko]=(lore.en,lore.ja);
                if (InkMythicSkins.Contains(id))
                {
                    var requirement = InkMythicSkins.Requirement(id);
                    result[requirement.ko] = (requirement.en, requirement.ja);
                }
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
                    SkinMetric.DragonDeath => ($"Finish {n} runs where the original Ink dies from a dragon collision", $"本体が竜との衝突で倒れたプレイを{n}回完了"),
                    _ => (null, (string)null),
                };
                if (pair.Item1 != null) result[InkSkinAchievements.Requirement(id)]=pair;
            }
            return result;
        }
    }
    /// 일반 의상은 짧은 이야기, 비밀 의상은 발견 전에도 읽을 수 있는 한 줄 해금 힌트.
    public static class InkSkinLore
    {
        public static (string ko, string en, string ja) Get(InkSkinId id)
        {
            var d = BuiltIn(id);
            return (InkSkinChart.Text(id, "loreKo", d.ko), InkSkinChart.Text(id, "loreEn", d.en), InkSkinChart.Text(id, "loreJa", d.ja));
        }
        // 구 차트의 창작 이야기만 힌트로 바꾼다. 엑셀에서 새로 작성한 힌트는 그대로 보존한다.
        internal static string UpgradeLegacyHiddenHint(InkSkinId id, string language, string text)
        {
            (string ko, string en, string ja) legacy = id switch
            {
                InkSkinId.InkGhost => ("다 쓴 종이도 가끔 꿈을 꾼다.", "Even used paper dreams sometimes.", "使い終えた紙も、ときどき夢を見る。"),
                InkSkinId.NightBat => ("달에 신호가 뜨면, 이름 없는 지붕의 주인이 깨어난다.", "When the signal reaches the moon, a nameless rooftop guardian wakes.", "月に合図が浮かぶと、名もなき屋根の守り手が目を覚ます。"),
                InkSkinId.Astronaut => ("달에 떡이 있는지 확인하러 간다.", "Off to check if the moon has rice cakes.", "月にお餅があるか、確かめに行く。"),
                InkSkinId.ClockworkCat => ("태엽을 감으면 오히려 잠이 든다.", "Winding it up only puts it to sleep.", "ぜんまいを巻くと、かえって眠くなる。"),
                InkSkinId.LuckyDice => ("일곱 번째 눈은 굴린 먹만 보인다.", "Only the roller can see the seventh face.", "七つ目の目は、振った墨にしか見えない。"),
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
                InkSkinId.ThreeCushion => ("큐대는 못 잡아도, 각은 제법 잘 본다.", "Can't hold a cue. Can still work the angles.", "キューは持てないが、角度を読むのは得意。"),
                _ => (null, null, null)
            };
            string previous = language switch { "Ko" => legacy.ko, "En" => legacy.en, _ => legacy.ja };
            var previousHint = InkMythicSkins.Contains(id) ? InkMythicSkins.PreviousLore(id) : id switch
            {
                InkSkinId.InkGhost => ("그린 길은 밟지 않아", "Drawn paths? Not stepping there", "描いた道は踏まないよ"),
                InkSkinId.ClockworkCat => ("밤고양이는 60초째 빈손", "Night cat, empty paws for 60s", "夜の猫、60秒ずっと手ぶら"),
                _ => ((string)null, (string)null, (string)null)
            };
            string oldHint = language switch { "Ko" => previousHint.Item1, "En" => previousHint.Item2, _ => previousHint.Item3 };
            if (text != previous && text != oldHint) return text;
            var hint = BuiltIn(id);
            return language switch { "Ko" => hint.ko, "En" => hint.en, _ => hint.ja };
        }
        public static (string ko, string en, string ja) BuiltIn(InkSkinId id) => InkMythicSkins.Contains(id) ? InkMythicSkins.Lore(id) : id switch {
            InkSkinId.AngelWings => ("돌아갈 곳을 잃은 빛이 먹 위에 머문다", "A light with nowhere to return rests above Ink", "帰る場所を失った光が、墨の上にとどまる"),
            InkSkinId.Ink => ("벼루 밖으로 처음 굴러 나온 날", "The day a drop rolled out of the inkstone", "硯の外へ、はじめて転がり出た日"),
            InkSkinId.Gat => ("바람처럼 스쳐가는....", "Passing by like the wind....", "風のように通り過ぎる...."),
            InkSkinId.SpiderMask => ("거미에게 물린 적은 없다", "Never been bitten by a spider", "蜘蛛に噛まれたことはない"),
            InkSkinId.FirstRibbon => ("처음 묶은 모양 그대로", "Still tied as it was the first day", "初めて結んだ、そのままの形"),
            InkSkinId.Scholar => ("벼슬보다 바람이 잘 통한다", "Better for catching a breeze than gaining rank", "出世よりも風通しがいい"),
            InkSkinId.InkGhost => ("그린 길 없이 150, 반 분", "No drawn paths, 150 and half a minute", "描いた道なしで150、三十秒"),
            InkSkinId.BambooHat => ("비는 막아도 먹 번짐은 못 막는다", "Keeps out rain Ink stains, not so much", "雨は防げても、墨のにじみは防げない"),
            InkSkinId.RedHeadband => ("백 번 넘어져도 매듭은 그대로였다", "A hundred falls The knot held firm", "百回転んでも、結び目はそのまま"),
            InkSkinId.LotusCrown => ("물 위에 남은 꽃 한 송이", "A single blossom left on the water", "水面に残った一輪の花"),
            InkSkinId.CatEars => ("생선 냄새가 나면 살짝 기울어진다", "Tilts ever so slightly toward fish", "魚の匂いがすると、少し傾く"),
            InkSkinId.BellRibbon => ("소리 없이 흔들리는 방울", "A bell that sways without a sound", "音もなく揺れる鈴"),
            InkSkinId.CloudCap => ("잠시 머물다 갈 구름 한 조각", "A passing cloud, resting for a while", "しばらく留まる、一片の雲"),
            InkSkinId.PaperBoat => ("아직 물에 띄운 적은 없다", "It has never touched the water", "まだ水に浮かべたことはない"),
            InkSkinId.TeaCupHat => ("차는 식었고 향은 남았다", "The tea has cooled Its scent remains", "茶は冷め、香りが残った"),
            InkSkinId.FeatherHairpin => ("주인 모를 깃 하나를 꽂았다", "A feather left behind, worn as a pin", "持ち主の知れない羽を一本、挿した"),
            InkSkinId.PeachFlower => ("봄이 급히 두고 간 물건", "Spring left this behind in a hurry", "春があわてて置いていったもの"),
            InkSkinId.FoxMask => ("웃는 얼굴 뒤는 알 수 없다", "No telling what lies behind the smile", "笑顔の裏はわからない"),
            InkSkinId.CraneCrest => ("날개에서 떨어져 여기까지 왔다", "Shed from a wing, it drifted this far", "翼を離れ、ここまで流れ着いた"),
            InkSkinId.YakshaMask => ("정체는 비밀인거같다", "Seems to keep its identity a secret", "正体は秘密らしい"),
            InkSkinId.TurtleShell => ("느린 장인이 끝내 완성한 투구", "A very slow craftsman finally finished it", "のんびり屋の職人が、ついに仕上げた"),
            InkSkinId.MoonCrown => ("기울지 않는 초승달", "A crescent that never wanes", "欠けることのない三日月"),
            InkSkinId.BrushMaster => ("붓을 쥘 손이 없어 머리에 꽂았다", "No hands to hold them, so here they rest", "筆を持つ手がないので、頭に挿した"),
            InkSkinId.SpiritHalo => ("끝은 또다른 시작", "Every end is another beginning", "終わりは新たな始まり"),
            InkSkinId.KoiMask => ("용문 앞에서 잠깐 쉬어 가는 중", "Taking a short rest before the Dragon Gate", "竜門の手前で、ちょっとひと休み"),
            InkSkinId.JadeCrown => ("오래된 옥에 빛이 남아 있다", "A trace of light remains in the old jade", "古い玉に、光が残っている"),
            InkSkinId.PhoenixCrown => ("타지 않은 깃은 딱 다섯 개였다", "Exactly five feathers escaped the fire", "燃え残った羽は、ちょうど五枚だった"),
            InkSkinId.TigerMask => ("눈을 마주치면 먼저 피하게 된다", "Meet its eyes, and you look away first", "目が合うと、こちらが先にそらす"),
            InkSkinId.MountainKing => ("산을 내려온 뒤에도 벗지 않았다", "Kept on, even after leaving the mountain", "山を下りてからも、脱がなかった"),
            InkSkinId.CloudDragon => ("구름 사이로 뿔이 드러난다", "Horns emerge between the clouds", "雲の間から角がのぞく"),
            InkSkinId.InkMecha => ("차가운 겉모습 안에는 같은 먹이 있다", "The same Ink beneath a cold exterior", "冷たい外見の奥には、いつもの墨がいる"),
            InkSkinId.NightBat => ("로비의 세 번째 밤", "The lobby's third night", "ロビーの三度目の夜"),
            InkSkinId.Astronaut => ("낮밤낮밤낮밤", "Day, night, day, night, day, night", "昼夜昼夜昼夜"),
            InkSkinId.ClockworkCat => ("밤고양이, 빈손으로 400과 이 분", "Night cat, empty paws, 400 and two minutes", "夜の猫、手ぶらで400と二分"),
            InkSkinId.LuckyDice => ("딱 777이 좋아", "Exactly 777, please", "ぴったり777が好き"),
            InkSkinId.OrigamiWings => ("끝까지 버틴 종이는 날개가 된다", "Paper that holds on becomes a pair of wings", "最後まで持ちこたえた紙は、翼になる"),
            InkSkinId.CraneWings => ("접어 두었던 날개를 펼친다", "Wings, folded until now, open", "畳んでいた翼を広げる"),
            InkSkinId.ButterflyWings => ("먹이 묻은 줄 모르고 날아온 나비", "A butterfly unaware of its ink stains", "墨がついたとも知らず、飛んできた蝶"),
            InkSkinId.RedShoes => ("발자국부터 먼저 길을 나섰다", "The footprints set off before their owner", "足跡のほうが、先に旅に出た"),
            InkSkinId.CloudBoots => ("비 오는 날엔 양말까지 구름이 된다", "On rainy days, even the socks turn to cloud", "雨の日には、靴下まで雲になる"),
            InkSkinId.BlueWisps => ("바람이 없어도 불은 흔들린다", "The flames stir without a breeze", "風がなくても、火は揺れる"),
            InkSkinId.PaperLanterns => ("어디로 가는지도 모르고 따라온다", "They follow without knowing the way", "行き先も知らずについてくる"),
            InkSkinId.MoonClouds => ("구름에 가려도 달은 남아 있다", "The moon remains behind the clouds", "雲に隠れても、月はそこにある"),
            InkSkinId.BondKnot => ("서로 다른 시간을 하나로 묶은 매듭", "A knot that binds different times together", "異なる時間をひとつに結んだ結び目"),
            InkSkinId.WindFan => ("부채를 펼치면, 바람의 흐름이 바뀔거같다", "Open the fan, and the wind might change its course", "扇を開けば、風の流れが変わりそうだ"),
            InkSkinId.DragonTail => ("스쳐 간 용이 흔적을 남겼다", "A passing dragon left its mark", "通り過ぎた竜が、跡を残した"),
            InkSkinId.ThreeCushion => ("한 번 던져, 벽에 세 번", "One toss, three wall bounces", "一度投げて、壁に三回"),
            _ => ("", "", "")
        };
    }
}
