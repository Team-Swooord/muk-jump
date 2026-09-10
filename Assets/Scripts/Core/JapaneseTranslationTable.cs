using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MukJump.Core
{
    // 기존 한국어 원문/영어 카탈로그를 공통 키로 사용한다. 사용자 이름은 번역하지 않는다.
    internal static partial class JapaneseTranslationTable
    {
        internal static readonly Dictionary<string, string> Values = Build();
        static Dictionary<string, string> Build()
        {
            var values = new Dictionary<string, string>
            {
                ["Play"] = "遊ぶ", ["Growth"] = "成長", ["Options"] = "設定", ["Settings"] = "設定",
                ["English"] = "日本語", ["Language"] = "言語",
                ["Could not save the language. Please try again."] = "言語を保存できませんでした。再試行してください。",
                ["Lobby"] = "ロビー", ["Leave this run?"] = "挑戦を終了しますか？",
                ["If you leave now, this run's record will not be saved."] = "終了すると今回の記録は保存されません。",
                ["Leave"] = "終了", ["Main Menu"] = "メインへ", ["Done"] = "完了", ["Cancel"] = "キャンセル",
                ["Music"] = "音楽", ["Sound"] = "効果音", ["Vibration"] = "振動", ["Haptics"] = "触覚",
                ["On"] = "オン", ["Off"] = "オフ", ["Support"] = "サポート", ["Tutorial"] = "遊び方",
                ["Leaderboard"] = "ランキング", ["World’s Finest Ink"] = "世界最高の墨", ["Rank"] = "順位",
                ["Terms of Use"] = "利用規約", ["Privacy Policy"] = "プライバシー", ["Gameplay Analytics"] = "プレイ分析",
                ["Help improve the game by sending an installation ID, device info and play events to Google Analytics.\nOptional. Turn it off anytime. Account UUIDs and nicknames are not sent."] = "インストールID・端末情報・プレイ情報を\nGoogle Analyticsに送信し、改善に役立てます。\n任意で、いつでもオフにできます。\nアカウントUUID・名前は送信しません。",
                ["Enable analytics"] = "同意してオン", ["Disable analytics"] = "分析をオフ", ["Analytics on"] = "分析オン", ["Analytics off"] = "分析オフ",
                ["Could not save. Please try again."] = "保存できませんでした。再試行してください。",
                ["Link Account"] = "アカウント連携", ["Linked"] = "連携済み", ["Not Linked"] = "未連携",
                ["Apple Linked"] = "Apple連携済み", ["Google Linked"] = "Google連携済み",
                ["Verify Apple"] = "Appleを確認", ["Verify Google"] = "Googleを確認",
                ["Continue with Apple"] = "Appleで続ける", ["Continue with Google"] = "Googleで続ける",
                ["Delete Account"] = "アカウント削除", ["Sign Out"] = "ログアウト", ["Copy Support Code"] = "問い合わせIDをコピー",
                ["Support code copied."] = "問い合わせIDをコピーしました。", ["UUID copied"] = "UUIDをコピーしました", ["Could not copy"] = "コピーできませんでした",
                ["Your account and cloud save\nwill be permanently deleted."] = "アカウントとサーバーの記録を\n完全に削除します。",
                ["Confirm Delete"] = "削除を確定", ["Wait a moment, then tap again to confirm."] = "少し待ってから、もう一度押してください。",
                ["Confirmation expired. Wait a moment, then tap again."] = "確認時間が過ぎました。少し待って再度押してください。",
                ["Choose an Account"] = "アカウント選択", ["Choose a Save"] = "記録を選択",
                ["Choose an account.\nProgress will not be merged."] = "使用するアカウントを選んでください。\n記録は統合されません。",
                ["Choose which upgrades to keep.\nCloud and device progress will not be merged."] = "使用する成長記録を選んでください。\nサーバーと端末の記録は統合されません。",
                ["Use Existing"] = "既存アカウント", ["Keep Guest"] = "ゲストを維持", ["Use Cloud Save"] = "サーバーの記録", ["Use Device Save"] = "この端末の記録",
                ["Checking Progress"] = "記録を確認中", ["Checking your cloud save safely..."] = "サーバーの記録を確認しています。",
                ["You can play once the check is complete."] = "確認が終わるとプレイできます。", ["Retry"] = "再試行", ["Return to Guest"] = "ゲストに戻る",
                ["Finishing Deletion"] = "削除を完了中", ["Removing the remaining device data..."] = "端末に残ったデータを削除しています。",
                ["Check Progress"] = "記録を確認", ["Could Not Verify Save"] = "記録を確認できません",
                ["Checking Toss Identity"] = "Tossの認証中", ["Could not verify your identity. Try again."] = "本人確認ができませんでした。再試行してください。",
                ["See your rank in Toss Game Center."] = "順位はTossゲームセンターで確認できます。",
                ["Could not connect to the leaderboard."] = "ランキングに接続できませんでした。",
                ["Name"] = "名前", ["Height"] = "高度", ["Best -"] = "最高 -", ["Best"] = "最高", ["NEW"] = "新記録",
                ["Preparing game…"] = "準備中…", ["All Inklight earned"] = "墨光をすべて獲得", ["Each run's height adds up."] = "各プレイの高度が累積されます。",
                ["All distance rewards claimed."] = "距離報酬をすべて獲得しました。", ["Google test ad · Top banner"] = "Googleテスト広告 · バナー",
                ["Ready"] = "準備完了", ["Could not load the game"] = "ゲームを読み込めませんでした", ["Tap to retry"] = "タップして再試行",
                ["W"] = "風", ["Toss"] = "Toss", ["Global"] = "世界", ["Nameless Inkdrop"] = "名無しの墨",
                ["Paused"] = "ひと休み", ["Resume"] = "続ける", ["Back"] = "戻る", ["Next"] = "次へ", ["Skip"] = "スキップ", ["Confirm Skip"] = "スキップを確定",
                ["This Run"] = "今回の高度", ["Personal Best"] = "最高高度", ["Watch Ad & Revive"] = "広告を見て復活", ["Opening Ad..."] = "広告を開いています…", ["Loading Ad..."] = "広告を準備中…",
                ["Google Test Ad  ·  Lobby Banner"] = "Googleテスト広告 · ロビー", ["Rewarded Test Ad"] = "報酬広告テスト", ["Interstitial Test Ad"] = "全画面広告テスト",
                ["Complete Test Reward"] = "テスト報酬を受け取る", ["Close Test Ad"] = "テスト広告を閉じる", ["Close Without Reward"] = "報酬なしで閉じる",
                ["This preview replaces a real ad.\nTest the revive reward or cancel below."] = "実際の広告の代わりのテスト画面です。\n復活報酬の受取やキャンセルを確認できます。",
                ["This is an interstitial ad preview.\nClose it to return to the game."] = "全画面広告のテストです。\n閉じるとゲームに戻ります。",
                ["Test privacy options completed."] = "プライバシー設定のテスト完了。", ["Ad privacy options are not required here."] = "この環境では広告の同意設定は不要です。",
                ["Ad privacy preferences saved."] = "広告の同意設定を保存しました。", ["Could not finish ad privacy options."] = "広告の同意設定を完了できませんでした。",
                ["Could not open ad privacy options."] = "広告の同意設定を開けませんでした。", ["Ad privacy options unavailable"] = "広告の同意設定を利用できません",
                ["Unknown Upgrade"] = "不明な成長", ["Run Over"] = "挑戦終了", ["Inklight"] = "墨光", ["Inklight (preview)"] = "墨光（予測）",
                ["The Ink Is Still Wet"] = "墨はまだ乾かない", ["An Early Exit"] = "早めのひと休み", ["Double Digits!"] = "二桁に到達", ["A Small but Mighty Climb"] = "小さな一歩", ["High for an Inkdrop"] = "墨も高みへ",
                ["Retrying your save..."] = "保存を再試行中…", ["Please recover your upgrade save."] = "成長記録を復元してください。", ["Recover in Lobby"] = "ロビーで復元",
                ["Discard This Run"] = "今回の記録を破棄", ["Discard score and Inklight?\nTap again to confirm."] = "記録と墨光を破棄しますか？\nもう一度押して確定。", ["Stop Retrying & Leave"] = "再試行をやめて戻る",
                ["Body"] = "体", ["Muk"] = "墨", ["Ink"] = "墨汁", ["Brush"] = "筆", ["Jump"] = "跳躍", ["Health"] = "体力", ["Max Ink"] = "墨汁容量", ["Ink Cost"] = "墨汁消費",
                ["Stronger Body"] = "丈夫な体", ["More Ink"] = "豊かな墨汁", ["Stronger Muk"] = "丈夫な墨", ["Thrifty Brush"] = "巧みな筆", ["Higher Jump"] = "高い跳躍",
                ["Survive more hits."] = "ぶつかっても長く耐えられます。", ["Carry more ink."] = "墨汁をより多く蓄えられます。", ["Use less ink as you draw."] = "描くときの墨汁消費を抑えます。", ["Reach higher with each jump."] = "より高く跳べます。",
                ["Fully Grown"] = "成長完了", ["Choose an Upgrade"] = "成長を選んでください", ["Upgrade"] = "成長させる", ["Reset"] = "リセット", ["Confirm Reset"] = "リセット確認",
                ["Reset your upgrades?"] = "成長をリセットしますか？", ["All spent Inklight will be refunded."] = "使った墨光はすべて戻ります。", ["Could not reset. Please try again."] = "リセットできませんでした。再試行してください。",
                ["Confirm"] = "確定", ["Save Recovery Needed"] = "記録の復元が必要です", ["Check Upgrade Save"] = "成長記録を確認", ["Recover Save"] = "記録を復元", ["Start Fresh"] = "新しく始める",
                ["Your upgrade save is safely locked."] = "成長記録を保護しています。", ["Could not read your upgrade save.\nThe original is preserved."] = "成長記録を読み込めませんでした。\n元の記録は保存されています。",
                ["Restore a verified backup or start fresh."] = "バックアップを復元するか、新しく始めてください。", ["Confirm below to start fresh."] = "新しく始めるには下で確定してください。", ["Your old save will be kept for recovery."] = "以前の記録は復元用に保存されます。",
                ["Tap again to confirm a fresh start."] = "もう一度押すと新しい記録で始めます。", ["Could not start a new save."] = "新しい記録を作れませんでした。", ["Could not finish save recovery."] = "記録の復元を完了できませんでした。", ["Please try again shortly."] = "少し待って再試行してください。",
                ["Locked"] = "未解放", ["Upgraded"] = "成長", ["Power Gained"] = "力が育ちました", ["New Power"] = "新しい力", ["Inklight becomes your strength."] = "墨光が力になりました。", ["A new power awakens."] = "新しい力が目覚めました。",
                ["Warning"] = "強風注意", ["Updraft"] = "上昇気流", ["Downdraft"] = "下降気流", ["Downdraft Soon"] = "下降気流接近", ["Updraft Soon"] = "上昇気流接近", ["Gale Soon"] = "狂風接近", ["Wild Gale"] = "狂風",
                ["Light"] = "微風", ["Breeze"] = "そよ風", ["Wind Shift"] = "風向き変化", ["Wind Pass"] = "風の峠", ["The ridge winds grow stronger."] = "尾根の風が強まります。",
                ["Ink Rain Valley"] = "墨雨の谷", ["Ink rain reduces your ink capacity."] = "墨雨で墨汁容量が減ります。", ["Falling Ink Gorge"] = "落墨の峡谷", ["Ink rocks fall more often."] = "墨の岩が多く落ちてきます。",
                ["Quiet Mountain Path"] = "静かな山道", ["Take a breath."] = "ひと息つきましょう。", ["Inklight Gate"] = "墨光の門", ["Mountains and stars meet in one stroke."] = "山と星が一筆でつながります。",
                ["Moonlit Lotus Sea"] = "月蓮の星海", ["Moonlit lotus nebulae bloom."] = "月明かりに蓮の星雲が咲きます。", ["Celestial Ink River"] = "天の墨河", ["The galaxy flows like a river of ink."] = "銀河が墨の川のように流れます。",
                ["Draw a Line"] = "線を描く", ["Auto Jump"] = "自動ジャンプ", ["Your Muk jumps on its own."] = "墨は自動でジャンプします。",
                ["Draw a line where it will land.\nTilt and length shape its jump."] = "着地する場所に線を描きましょう。\n傾きと長さで跳び方が変わります。",
                ["This is your remaining ink.\nLow on ink? The oldest lines fade first."] = "残りの墨汁です。\n足りなくなると古い線から消えます。",
                ["Hits and falls cost 1 HP.\nThe run ends when all Muks fall."] = "衝突や落下で体力が1減ります。\n全員倒れると挑戦終了です。", ["Tap to continue"] = "タップして次へ",
                ["Your Inkdrop jumps on its own.\nDraw a line where it will land.\nTilt sets direction; length sets jump power."] = "墨は自動でジャンプします。\n着地する場所に線を描きましょう。\n傾きで方向、長さで強さが変わります。",
                ["Ink Gauge"] = "墨汁ゲージ", ["The bottom gauge shows your ink left.\nLines fade over time.\nWhen ink runs low, older lines are erased."] = "下のゲージが残りの墨汁です。\n線は時間が経つと消えます。\n墨汁が足りないと古い線から消えます。",
                ["Hazards and falls cost 1 health point.\nThe run ends when no Inkdrops remain."] = "障害物や落下で体力が1減ります。\n分身もすべて倒れると挑戦終了です。",
                ["Wind Boost"] = "風脈上昇", ["A wind current lifts your Inkdrops."] = "風の流れが墨を押し上げます。", ["Updraft Ahead"] = "上昇気流接近", ["Rising winds will soon slow your fall."] = "まもなく上昇する風が吹きます。",
                ["Muk Jump Support"] = "Muk Jump サポート", ["Email unavailable: cysbandcs@gmail.com"] = "メールを開けません: cysbandcs@gmail.com",
                ["Opening privacy and account deletion information."] = "プライバシーと削除の案内を開きます。", ["Sound & Display"] = "音と画面", ["Muk Jump"] = "Muk Jump",
            };
            AddAccount(values);
            return values;
        }
        static partial void AddAccount(Dictionary<string, string> values);

        internal static string Translate(string source, string english)
        {
            if (source == "한국어") return "日本語";
            return TranslateEnglish(english);
        }

        static string TranslateEnglish(string value)
        {
            if (Values.TryGetValue(value, out string exact)) return exact;
            string result = value;
            result = Regex.Replace(result, @"^Need (\d+) Inklight$", "墨光が$1個必要");
            result = Regex.Replace(result, @"^(Height|Best) (.+)$", m => (m.Groups[1].Value == "Height" ? "高度 " : "最高 ") + m.Groups[2].Value);
            result = Regex.Replace(result, @"^\+(\d+) HP$", "+$1体力");
            result = Regex.Replace(result, @"^Inklight \+1 · (.+)$", "墨光 +1 · $1");
            result = Regex.Replace(result, @"^Total (.+)$", "累積 $1");
            result = Regex.Replace(result, @"^1 Inklight per (.+)m total$", "累積$1mごとに墨光1個");
            result = Regex.Replace(result, @"^(.+)m to next Inklight$", "次の墨光まで$1m");
            result = Regex.Replace(result, @"^(Est\. )?Inklight \+(\d+)$", m => (m.Groups[1].Success ? "予測 墨光 +" : "墨光 +") + m.Groups[2].Value);
            result = result.Replace("Retrying cloud save (", "サーバー保存を再試行中 (")
                .Replace("Progress saved. Retrying leaderboard submission (", "記録保存済み。順位登録を再試行中 (")
                .Replace("no response", "応答なし");
            if (result != value) return result;
            if (value.Contains('\n')) return string.Join("\n", Array.ConvertAll(value.Split('\n'), TranslateEnglish));
            if (value.Contains('<')) return Regex.Replace(value, @"(^|>)([^<]+)", m => m.Groups[1].Value + TranslateEnglish(m.Groups[2].Value));
            foreach (var suffix in EnglishTranslationTable.AccountSuffixes)
                if (value.EndsWith(suffix.Value, StringComparison.Ordinal))
                    return TranslateEnglish(value.Substring(0, value.Length - suffix.Value.Length)) + TranslateEnglish(suffix.Value);
            return value;
        }
    }
}
