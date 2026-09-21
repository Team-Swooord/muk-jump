using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkSkinChartTests
    {
        [SetUp] public void Before() => InkSkinChart.Reset();
        [TearDown] public void After() => InkSkinChart.Reset();
        [Test, Category("SkinDescriptionPunctuation")]
        public void BuiltInDescriptionsOmitSentencePeriodsInEveryLanguage()
        {
            var rows = InkSkinChartExport.Rows();
            foreach (var row in rows)
            foreach (string field in new[] { "loreKo", "loreEn", "loreJa", "abilityDescriptionKo", "abilityDescriptionEn", "abilityDescriptionJa" })
            {
                string text = (string)row[field];
                if ((string)row["skinKey"] == "Gat" && field.StartsWith("lore"))
                    Assert.That(text, Does.EndWith("...."), field);
                else
                    Assert.That(text, Does.Not.Contain(".").And.Not.Contain("。"), row["skinKey"] + "/" + field);
            }
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(JToken.DeepEquals(rows, InkSkinChartExport.Rows()), Is.True);
        }

        [Test, Category("SkinDescriptionPunctuation")]
        public void OldChartDescriptionPeriodsAreRemovedWithoutChangingNamesOrRules()
        {
            var rows = InkSkinChartExport.Rows();
            var row = rows.Single(x => (string)x["skinKey"] == "WindFan");
            row["loreKo"] = "바람이 분다.";
            row["loreEn"] = "The wind blows.";
            row["loreJa"] = "風が吹く。";
            row["abilityDescriptionKo"] = "{cooldown}초 대기.\n25.5m 도약.";
            row["abilityDescriptionEn"] = "Wait {cooldown}s.\nLeap 25.5m.";
            row["abilityDescriptionJa"] = "{cooldown}秒待つ。\n25.5m跳躍。";
            row["nameKo"] = "바람부채.";
            var original = (JArray)rows.DeepClone();
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinLore.Get(InkSkinId.WindFan), Is.EqualTo(("바람이 분다", "The wind blows", "風が吹く")));
            string description = InkSkinCatalog.Description(InkSkinId.WindFan);
            Assert.That(description, Is.EqualTo("20초 대기\n25.5m 도약"));
            Assert.That(GameLocalization.Translate(description, GameLanguage.English), Is.EqualTo("Wait 20s\nLeap 25.5m"));
            Assert.That(GameLocalization.Translate(description, GameLanguage.Japanese), Is.EqualTo("20秒待つ\n25.5m跳躍"));
            var exported = InkSkinChartExport.Rows();
            for (int i = 0; i < original.Count; i++)
            foreach (var property in ((JObject)original[i]).Properties())
            {
                if (property.Name.StartsWith("lore") || property.Name.StartsWith("abilityDescription")) continue;
                Assert.That(JToken.DeepEquals(exported[i][property.Name], property.Value), Is.True, property.Name);
            }
        }

        [Test, Category("SkinDescriptionPunctuation")]
        public void SentenceCleanupPreservesEllipsesDecimalsAndLineBreaks()
        {
            const string source = "바람.... 1.5초.\n도약한다.\n光が戻る。また始まる。";
            const string expected = "바람.... 1.5초\n도약한다\n光が戻る また始まる";
            var method = typeof(InkSkinChart).GetMethod("WithoutSentencePeriods", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method.Invoke(null, new object[] { source }), Is.EqualTo(expected));
        }

        static bool Parse(JArray rows, out InkSkinChart.Snapshot snapshot) =>
            InkSkinChart.TryParse(rows.ToString(Formatting.None), out snapshot, out _);
        static readonly (InkSkinId id, string oldKo, string oldEn, string oldJa, string hint)[] hiddenHints =
        {
            (InkSkinId.InkGhost, "다 쓴 종이도 가끔 꿈을 꾼다.", "Even used paper dreams sometimes.", "使い終えた紙も、ときどき夢を見る。", "그린 길은 밟지 않아"),
            (InkSkinId.NightBat, "달에 신호가 뜨면, 이름 없는 지붕의 주인이 깨어난다.", "When the signal reaches the moon, a nameless rooftop guardian wakes.", "月に合図が浮かぶと、名もなき屋根の守り手が目を覚ます。", "로비의 세 번째 밤"),
            (InkSkinId.Astronaut, "달에 떡이 있는지 확인하러 간다.", "Off to check if the moon has rice cakes.", "月にお餅があるか、確かめに行く。", "낮밤낮밤낮밤"),
            (InkSkinId.ClockworkCat, "태엽을 감으면 오히려 잠이 든다.", "Winding it up only puts it to sleep.", "ぜんまいを巻くと、かえって眠くなる。", "밤고양이는 60초째 빈손"),
            (InkSkinId.LuckyDice, "일곱 번째 눈은 굴린 먹만 보인다.", "Only the roller can see the seventh face.", "七つ目の目は、振った墨にしか見えない。", "딱 777이 좋아"),
            (InkSkinId.ZodiacRat, "달 뜨면 벼루 창고가 한 칸 빈다.", "Moon up. One shelf of ink goes missing.", "月が出ると、硯の棚がひとつ空く。", "밤참 세 개, 50만큼"),
            (InkSkinId.ZodiacOx, "서두르지 않았는데 제일 먼저 왔다.", "Never hurried. Arrived first anyway.", "急がなかったのに、いちばん乗り。", "스무 번 딛고, 일 분 버티기"),
            (InkSkinId.ZodiacTiger, "아직 포효보다 하품이 더 크다.", "Its yawn is still louder than its roar.", "雄叫びより、あくびの方が大きい。", "밤사냥은 빈손으로"),
            (InkSkinId.ZodiacRabbit, "떡은 두고 귀만 빌려 왔다.", "Left the rice cakes. Borrowed the ears.", "お餅は置いて、耳だけ借りてきた。", "달을 쓰고 밤길 200"),
            (InkSkinId.ZodiacDragon, "승천 연습은 낮잠 다음에.", "Ascension practice comes after a nap.", "昇天の練習は、お昼寝のあと。", "꼬리 달고 300까지"),
            (InkSkinId.ZodiacSnake, "리본인 줄 알고 묶을 뻔했다.", "Almost tied it. Thought it was a ribbon.", "リボンと思って、結ぶところだった。", "거미의 열두 번째 착지"),
            (InkSkinId.ZodiacHorse, "발굽 대신 바람 소리만 남겼다.", "Left a breeze instead of hoofprints.", "ひづめの跡の代わりに、風の音。", "300까지 일 분이면 돼"),
            (InkSkinId.ZodiacGoat, "산길은 곧게 걷고, 뿔만 둥글게 말았다.", "Walked the mountain trail straight. Only the horns took a turn.", "山道はまっすぐ歩いた。巻いたのは角だけ。", "서른 번 딛고 200까지"),
            (InkSkinId.ZodiacMonkey, "손이 모자라면 꼬리부터 내민다.", "Short a hand? The tail volunteers first.", "手が足りなければ、しっぽが先に伸びる。", "셋이서 간식 다섯 개"),
            (InkSkinId.ZodiacRooster, "해보다 조금 늦게 일어난다.", "Wakes just a little after the sun.", "お日さまより、少し寝坊する。", "해 뜨면 먹방울 셋"),
            (InkSkinId.ZodiacDog, "문 대신 친구의 그림자를 지킨다.", "Guards a friend's shadow, not a gate.", "門ではなく、友だちの影を守る。", "둘이서 일 분 반"),
            (InkSkinId.ZodiacPig, "복을 불렀더니 간식도 왔다.", "Called for luck. Snacks came too.", "福を呼んだら、おやつも来た。", "열두 개도 모자라"),
            (InkSkinId.HaetaeCrest, "나쁜 꿈만 콕 찌르는 작은 뿔.", "A tiny horn for poking bad dreams away.", "悪い夢だけ、ちくりと追い払う角。", "현무는 250까지 빈손"),
            (InkSkinId.ThreeLeggedCrow, "세 발로도 아침엔 자꾸 헛디딘다.", "Three feet. Still stumbles in the morning.", "三本足でも、朝はつまずく。", "밤에는 셋셋셋"),
            (InkSkinId.NineTails, "꼬리는 아홉인데, 속마음은 하나도 안 보인다.", "Nine tails. Not a single clue what it is thinking.", "尾は九つ。心の内はひとつも見えない。", "여우 셋이서 300까지"),
            (InkSkinId.ThreeCushion, "큐대는 못 잡아도, 각은 제법 잘 본다.", "Can't hold a cue. Can still work the angles.", "キューは持てないが、角度を読むのは得意。", "한 번 던져, 벽에 세 번"),
        };
        [Test, Category("HiddenSkinHints")]
        public void AllHiddenSkinsHaveShortDistinctLocalizedHints()
        {
            var ids = Enumerable.Range(0, InkSkinCatalog.Count).Select(i => (InkSkinId)i)
                .Where(id => InkSkinCatalog.BuiltIn(id).Tier is SkinTier.Hidden or SkinTier.UltraHidden).ToArray();
            Assert.That(hiddenHints.Select(x => x.id), Is.EquivalentTo(ids));
            Assert.That(hiddenHints.Select(x => x.hint).Distinct().Count(), Is.EqualTo(21));
            foreach (var item in hiddenHints)
            {
                var lore = InkSkinLore.Get(item.id);
                Assert.That(lore.ko, Is.EqualTo(item.hint));
                Assert.That(lore.ko.Length, Is.LessThanOrEqualTo(26));
                foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
                {
                    var text = GameLocalization.Translate(lore.ko, language);
                    Assert.That(text, Is.Not.Empty.And.Not.EqualTo(lore.ko));
                    Assert.That(text, Does.Not.Match("[가-힣]"));
                }
            }
        }
        [Test, Category("HiddenSkinHints")]
        public void LegacyHiddenStoriesBecomeHintsButCustomChartHintsRemainEditable()
        {
            var expected = InkSkinChartExport.Rows();
            var legacy = (JArray)expected.DeepClone();
            foreach (var item in hiddenHints)
            {
                var row = legacy.Single(x => (string)x["skinKey"] == item.id.ToString());
                row["loreKo"] = item.oldKo; row["loreEn"] = item.oldEn; row["loreJa"] = item.oldJa;
            }
            Assert.That(Parse(legacy, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(JToken.DeepEquals(InkSkinChartExport.Rows(), expected), Is.True,
                "힌트 외의 이름·조건·능력·ID는 보존한다");
            foreach (var item in hiddenHints)
            {
                var row = legacy.Single(x => (string)x["skinKey"] == item.id.ToString());
                row["loreKo"] = "새 힌트 " + item.id;
                row["loreEn"] = "New hint " + item.id; row["loreJa"] = "新しいヒント " + item.id;
            }
            Assert.That(Parse(legacy, out snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            foreach (var item in hiddenHints)
                Assert.That(InkSkinLore.Get(item.id).ko, Is.EqualTo("새 힌트 " + item.id));
        }
        [TestCase(InkSkinId.FirstRibbon, "먹매듭")]
        [TestCase(InkSkinId.InkGhost, "먹유령")]
        [TestCase(InkSkinId.PaperBoat, "종이배")]
        [TestCase(InkSkinId.TeaCupHat, "찻잔")]
        [TestCase(InkSkinId.CraneCrest, "학깃")]
        [TestCase(InkSkinId.BrushMaster, "쌍붓")]
        [TestCase(InkSkinId.JadeCrown, "옥관")]
        [TestCase(InkSkinId.CloudDragon, "운룡")]
        [TestCase(InkSkinId.InkMecha, "철가면")]
        [TestCase(InkSkinId.ButterflyWings, "먹나비")]
        [TestCase(InkSkinId.BlueWisps, "도깨비불")]
        [TestCase(InkSkinId.MoonClouds, "달구름")]
        [TestCase(InkSkinId.ZodiacRat, "밤쥐")]
        [TestCase(InkSkinId.ZodiacOx, "황소")]
        [TestCase(InkSkinId.ZodiacTiger, "호피 두건")]
        [TestCase(InkSkinId.ZodiacRabbit, "달토끼")]
        [TestCase(InkSkinId.ZodiacDragon, "용각")]
        [TestCase(InkSkinId.ZodiacSnake, "뱀 매듭")]
        [TestCase(InkSkinId.ZodiacHorse, "갈기")]
        [TestCase(InkSkinId.ZodiacGoat, "산양")]
        [TestCase(InkSkinId.ZodiacRooster, "닭벼슬")]
        [TestCase(InkSkinId.ZodiacDog, "사냥개")]
        [TestCase(InkSkinId.ZodiacPig, "돼지코")]
        [TestCase(InkSkinId.ThreeLeggedCrow, "삼족오")]
        [TestCase(InkSkinId.NineTails, "구미호")]
        [Category("SkinToneCopy")]
        public void AccessoryNamesStayConciseAndTranslated(InkSkinId id, string name)
        {
            var skin = InkSkinCatalog.Get(id);
            Assert.That(skin.Korean, Is.EqualTo(name));
            Assert.That(skin.Korean.Length, Is.LessThanOrEqualTo(8));
            Assert.That(InkSkinTranslations.TryTranslate(name, false, out var en), Is.True);
            Assert.That(InkSkinTranslations.TryTranslate(name, true, out var ja), Is.True);
            Assert.That(en, Is.EqualTo(skin.English).And.Not.Empty);
            Assert.That(ja, Is.EqualTo(skin.Japanese).And.Not.Empty);
        }
        [Test, Category("SkinToneCopy")]
        public void DisplayNamesRemainUniqueAndRetiredSkinStaysExcluded()
        {
            var rows = InkSkinChartExport.Rows();
            foreach (string field in new[] { "nameKo", "nameEn", "nameJa" })
                Assert.That(rows.Select(r => (string)r[field]).Distinct().Count(), Is.EqualTo(rows.Count), field);
            Assert.That(rows.Count, Is.EqualTo(62));
        }
        [TestCase(InkSkinId.Gat, "바람 갓", "바람처럼 스쳐가는....", "이단 도약")]
        [TestCase(InkSkinId.SpiderMask, "거미탈", "거미에게 물린 적은 없다", "먹슈트")]
        [TestCase(InkSkinId.YakshaMask, "각시탈", "정체는 비밀인거같다", "탈춤")]
        [TestCase(InkSkinId.TurtleShell, "현무", "느린 장인이 끝내 완성한 투구", "금강")]
        [TestCase(InkSkinId.SpiritHalo, "윤회의 고리", "끝은 또다른 시작", "윤회")]
        [TestCase(InkSkinId.PhoenixCrown, "봉황 관", "타지 않은 깃은 딱 다섯 개였다", "봉황의 숨결")]
        [TestCase(InkSkinId.CloudBoots, "구름 장화", "비 오는 날엔 양말까지 구름이 된다", "구름 도약")]
        [TestCase(InkSkinId.BondKnot, "무스비", "서로 다른 시간을 하나로 묶은 매듭", "인연의 이끌림")]
        [TestCase(InkSkinId.WindFan, "바람부채", "부채를 펼치면, 바람의 흐름이 바뀔거같다", "바람 도약")]
        [TestCase(InkSkinId.AngelWings, "여명의 고리", "돌아갈 곳을 잃은 빛이 먹 위에 머문다", "다시 피는 빛")]
        public void RequestedSkinCopyMatchesBuiltInAndChart(InkSkinId id, string name, string lore, string ability)
        {
            var rows = InkSkinChartExport.Rows();
            Assert.That(Parse(rows, out var snapshot), Is.True);
            // 기본값과 다운로드 차트 양쪽에서 같은 문구·번역을 표시한다.
            foreach (bool remote in new[] { false, true })
            {
                if (remote) InkSkinChart.Apply(snapshot);
                Assert.That(InkSkinCatalog.Name(id), Is.EqualTo(name));
                Assert.That(InkSkinLore.Get(id).ko, Is.EqualTo(lore));
                Assert.That(InkSkinCatalog.Ability(id), Is.EqualTo(ability));
                foreach (string ko in new[] { name, lore, ability })
                foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
                {
                    string translated = GameLocalization.Translate(ko, language);
                    Assert.That(translated, Is.Not.Empty.And.Not.EqualTo(ko));
                    Assert.That(translated, Does.Not.Match("[가-힣]"));
                }
            }
        }
        [TestCase(InkSkinId.SpiderMask), TestCase(InkSkinId.YakshaMask), TestCase(InkSkinId.TurtleShell)]
        [TestCase(InkSkinId.SpiritHalo), TestCase(InkSkinId.BondKnot), TestCase(InkSkinId.WindFan), TestCase(InkSkinId.AngelWings)]
        public void CopyMigrationPreservesOperatorAuthoredText(InkSkinId id)
        {
            var rows = InkSkinChartExport.Rows();
            var row = rows.Single(x => (string)x["skinKey"] == id.ToString());
            foreach (string field in new[] { "name", "lore", "abilityName" })
            foreach (string lang in new[] { "Ko", "En", "Ja" })
                row[field + lang] = "Custom " + field + lang;
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinCatalog.Name(id), Is.EqualTo("Custom nameKo"));
            Assert.That(InkSkinLore.Get(id), Is.EqualTo(("Custom loreKo", "Custom loreEn", "Custom loreJa")));
            Assert.That(InkSkinCatalog.Ability(id), Is.EqualTo("Custom abilityNameKo"));
        }
        [Test] public void GatUsesOnlyTheRequestedShortLoreIncludingLegacyChartData()
        {
            Assert.That(InkSkinCatalog.BuiltIn(InkSkinId.Gat).Korean, Is.EqualTo("바람 갓"));
            Assert.That(InkSkinLore.Get(InkSkinId.Gat).ko, Is.EqualTo("바람처럼 스쳐가는...."));
            var rows = InkSkinChartExport.Rows();
            var gat = rows.Single(x => (string)x["skinKey"] == "Gat");
            Assert.That((string)gat["loreKo"], Is.EqualTo("바람처럼 스쳐가는...."));
            gat["loreKo"] = "쓰고 나면 어째서인지 바람처럼 스쳐 가는 노래가 들린다.";
            gat["loreEn"] = "Put it on, and an old street anthem drifts past on the wind.";
            gat["loreJa"] = "かぶると、なぜか風に乗って昔の街の歌が聞こえる。";
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinLore.Get(InkSkinId.Gat), Is.EqualTo(InkSkinLore.BuiltIn(InkSkinId.Gat)));
            Assert.That(InkSkinTranslations.TryTranslate("바람처럼 스쳐가는....", false, out var en), Is.True);
            Assert.That(en, Is.EqualTo("Passing by like the wind...."));
            Assert.That(InkSkinTranslations.TryTranslate("바람처럼 스쳐가는....", true, out var ja), Is.True);
            Assert.That(ja, Is.EqualTo("風のように通り過ぎる...."));
        }
        [Test] public void ExportsEveryAvailableSkinWithoutReusingRetiredId()
        {
            var rows = InkSkinChartExport.Rows();
            Assert.That(rows.Count, Is.EqualTo(62));
            Assert.That(rows.Any(x => (string)x["skinId"] == "20"), Is.False);
            Assert.That(Parse(rows, out _), Is.True);
            foreach (var r in rows.OfType<JObject>()) foreach (var p in r.Properties())
            { Assert.That(p.Name, Does.Match("^[A-Za-z][A-Za-z0-9]*$")); Assert.That(p.Value.ToString(), Does.Not.Contain("\\")); }
            foreach (var r in rows.OfType<JObject>()) foreach (var p in r.Properties().Where(x => x.Name.EndsWith("En") || x.Name.EndsWith("Ja")))
                Assert.That(p.Value.ToString(), Does.Not.Match("[가-힣]"), r["skinKey"] + ":" + p.Name);
        }
        [Test] public void AppliesNamesLoreGoalTranslationsAndLiveCooldownWithoutChangingUnlockMask()
        {
            var state = new SkinAchievementState(); state.Grant(InkSkinId.Gat);
            long mask = state.unlockedMask;
            var rows = InkSkinChartExport.Rows();
            rows[1]["nameKo"] = "시험 바람 갓"; rows[1]["nameEn"] = "Test Wind Gat"; rows[1]["nameJa"] = "テスト風の笠";
            rows[1]["loreKo"] = "새 이야기"; rows[1]["loreEn"] = "New story"; rows[1]["loreJa"] = "新しい物語";
            rows[1]["unlockGoal"] = 999;
            var fan = rows.Single(x => (string)x["skinKey"] == "WindFan"); fan["cooldownSeconds"] = 12;
            Assert.That(Parse(rows, out var snapshot), Is.True);
            InkSkinChart.Apply(snapshot);
            Assert.That(InkSkinCatalog.Name(InkSkinId.Gat), Is.EqualTo("시험 바람 갓"));
            Assert.That(InkSkinLore.Get(InkSkinId.Gat).en, Is.EqualTo("New story"));
            Assert.That(InkSkinAchievements.Goal(InkSkinId.Gat), Is.EqualTo(999));
            Assert.That(InkSkinTranslations.TryTranslate("시험 바람 갓", true, out string ja), Is.True);
            Assert.That(ja, Is.EqualTo("テスト風の笠"));
            string rule = InkSkinAchievements.Requirement(InkSkinId.Gat);
            Assert.That(InkSkinTranslations.TryTranslate(rule, false, out string en), Is.True);
            Assert.That(en, Does.Contain("999"));
            Assert.That(InkSkinCatalog.Description(InkSkinId.WindFan), Does.Contain("12초"));
            var go = new GameObject("ChartCooldownTest");
            try {
                var controller = go.AddComponent<RunGrowthController>();
                typeof(RunGrowthController).GetField("skinSnapshot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, InkSkinId.WindFan);
                Assert.That(controller.EquippedAbilityCooldownDuration, Is.EqualTo(12));
            } finally { Object.DestroyImmediate(go); }
            Assert.That(state.unlockedMask, Is.EqualTo(mask));
            Assert.That(state.Progress(InkSkinId.Gat, 0, 0), Is.EqualTo(999));
        }
        [TestCase("skinId", "20")]
        [TestCase("skinKey", "CatEars")]
        [TestCase("unlockMetric", "Secret")]
        [TestCase("unlockGoal", "-1")]
        [TestCase("unlockGoal", "1.2")]
        [TestCase("schemaVersion", "2")]
        [TestCase("nameJa", "")]
        [TestCase("nameKo", "<b>bad</b>")]
        [TestCase("nameEn", "back\\slash")]
        [TestCase("cooldownSeconds", "NaN")]
        [TestCase("cooldownSeconds", "5")]
        public void RejectsInvalidRowsAtomically(string field, string value)
        {
            var rows = InkSkinChartExport.Rows(); rows[1][field] = value;
            Assert.That(Parse(rows, out var snapshot), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(InkSkinCatalog.Name(InkSkinId.Gat), Is.EqualTo(InkSkinCatalog.BuiltIn(InkSkinId.Gat).Korean));
        }
        [Test] public void RejectsDuplicateMissingAndHiddenRuleChanges()
        {
            var rows = InkSkinChartExport.Rows(); rows[2] = rows[1].DeepClone(); Assert.That(Parse(rows, out _), Is.False);
            rows = InkSkinChartExport.Rows(); rows.RemoveAt(0); Assert.That(Parse(rows, out _), Is.False);
            rows = InkSkinChartExport.Rows(); rows.Single(x => (string)x["skinKey"] == "NightBat")["unlockGoal"] = 2;
            Assert.That(Parse(rows, out _), Is.False);
        }
        [Test] public void BadDownloadLeavesPreviousAcceptedCatalogIntact()
        {
            var rows = InkSkinChartExport.Rows(); rows[1]["unlockGoal"] = 250;
            Assert.That(Parse(rows, out var first), Is.True); InkSkinChart.Apply(first);
            Assert.That(InkSkinChart.TryParse("{broken", out _, out _), Is.False);
            Assert.That(InkSkinCatalog.Get(InkSkinId.Gat).Goal, Is.EqualTo(250));
        }
        [Test] public void TimedAbilitiesRequireWholeSecondsAndMatchingDescriptionTokens()
        {
            var rows = InkSkinChartExport.Rows();
            var spider = rows.Single(x => (string)x["skinKey"] == "SpiderMask");
            spider["cooldownSeconds"] = "12.5";
            Assert.That(Parse(rows, out _), Is.False);
            spider["cooldownSeconds"] = "12";
            spider["abilityDescriptionEn"] = "Every 30 seconds.";
            Assert.That(Parse(rows, out _), Is.False);
        }
        [TestCase(false, GameState.Lobby, true)]
        [TestCase(false, GameState.Playing, false)]
        [TestCase(false, GameState.GameOver, false)]
        [TestCase(true, GameState.Lobby, false)]
        public void NeverChangesRulesDuringRunOrSettlement(bool started, GameState state, bool expected) =>
            Assert.That(InkSkinChartRuntime.CanApplyDownloaded(started, state), Is.EqualTo(expected));
    }
}
