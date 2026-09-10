using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class EnglishLocalizationTests
    {
        readonly List<GameObject> cleanup = new();
        MemoryLobbySettingsStore settings;
        MemoryPermanentGrowthStore growth;

        [SetUp]
        public void SetUp()
        {
            settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings);
            growth = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(growth);
        }

        [TearDown]
        public void TearDown()
        {
            settings.ThrowOnSave = false;
            GameLocalization.SetLanguage(GameLanguage.Korean);
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test]
        public void LanguagePersistsWithoutTouchingGrowthOrCloudSettings()
        {
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.Korean));
            PermanentGrowthProfile.SettleRun("language-test", 0, 300, 0, 0f, true);
            string original = growth.Json;
            Assert.That(GameLocalization.SetLanguage(GameLanguage.English), Is.True);
            LobbySettingsProfile.UseStoreForTests(settings);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.English));
            LobbySettingsProfile.ApplyCloudSettings(.2f, .3f, 5);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(growth.Json, Is.EqualTo(original));
            Assert.That(GameLocalization.SetLanguage((GameLanguage)999), Is.False);
            settings.ThrowOnSave = true;
            Assert.That(GameLocalization.SetLanguage(GameLanguage.Korean), Is.False);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.English));
            settings.ThrowOnSave = false;
            LobbySettingsProfile.UseStoreForTests(settings);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.English));
        }

        [TestCase("기록은 저장됐지만 순위 등록을 재시도 중입니다 (403)", "Progress saved. Retrying leaderboard submission (403)")]
        [TestCase("기록은 저장됐지만 순위 등록을 재시도 중입니다 (응답 없음)", "Progress saved. Retrying leaderboard submission (no response)")]
        [TestCase("저장된 최고 기록을 순위에 반영하는 중입니다", "Submitting your saved best to the leaderboard...")]
        [TestCase("이전 저장을 확인했습니다. 최신 기록을 이어서 저장합니다", "Previous save confirmed. Saving your latest progress...")]
        public void AccountRecoveryStatusPreservesMeaningAcrossLanguages(string source, string expected)
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            Assert.That(GameLocalization.Translate(source), Is.EqualTo(expected));
            GameLocalization.SetLanguage(GameLanguage.Korean);
            Assert.That(GameLocalization.Translate(source), Is.EqualTo(source));
        }

        [Test]
        public void BoundTextUpdatesImmediatelyAndRetainsSourceAcrossLanguageChanges()
        {
            Text label = NewText("DynamicLabel", "먹빛 19개 필요", new Vector2(720, 80), 56);
            InkLocalizedText.Bind(label);
            Assert.That(GameLocalization.SetLanguage(GameLanguage.English), Is.True);
            Assert.That(label.text, Is.EqualTo("Need 19 Inklight"));
            label.text = "먹빛 7개 필요";
            Assert.That(label.text, Is.EqualTo("Need 7 Inklight"));
            GameLocalization.SetLanguage(GameLanguage.Korean);
            Assert.That(label.text, Is.EqualTo("먹빛 7개 필요"));
            GameLocalization.SetLanguage(GameLanguage.English);
            var binding = label.GetComponent<InkLocalizedText>();
            Assert.That(binding.SourceText, Is.EqualTo("먹빛 7개 필요"));
            Invoke(binding, "OnDisable");
            label.gameObject.SetActive(false);
            label.text = "최고 123m";
            GameLocalization.SetLanguage(GameLanguage.Korean);
            label.gameObject.SetActive(true);
            Invoke(binding, "OnEnable");
            Assert.That(label.text, Is.EqualTo("최고 123m"));
            GameLocalization.SetLanguage(GameLanguage.English);
            Assert.That(label.text, Is.EqualTo("Best 123m"));
        }

        [Test]
        public void InklightHelpUsesTheRewardIntervalAndSwitchesLanguageImmediately()
        {
            string source = "다음 먹빛까지 50m\n매 판 오른 높이가 합산돼요.";
            Text label = NewText("InklightHelp", source, new Vector2(464, 128), 36);
            InkLocalizedText.Bind(label);
            GameLocalization.SetLanguage(GameLanguage.English);
            Assert.That(label.text, Is.EqualTo(
                "50m to next Inklight\nEach run's height adds up."));
            GameLocalization.SetLanguage(GameLanguage.Korean);
            Assert.That(label.text, Is.EqualTo(source));
        }

        [Test]
        public void GameLogoSwitchesImmediatelyEvenWhileHiddenAndRestoresOriginalArtwork()
        {
            var korean = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                MukJump.EditorTools.MukJumpSplashSceneBuilder.GameLogoPath);
            var english = Resources.Load<Texture2D>(InkLocalizedGameLogo.EnglishResourcePath);
            Assert.That(korean, Is.Not.Null);
            Assert.That(english, Is.Not.Null);
            var image = NewHost("LocalizedLogo").AddComponent<RawImage>();
            image.texture = korean;
            image.uvRect = new Rect(0, 0, 1, 1);
            image.rectTransform.sizeDelta = new Vector2(900, 600);
            InkLocalizedGameLogo.Bind(image);
            image.gameObject.SetActive(false);
            foreach (bool englishSelected in new[] { true, false, true, false })
            {
                GameLocalization.SetLanguage(englishSelected ? GameLanguage.English : GameLanguage.Korean);
                Assert.That(image.texture, Is.SameAs(englishSelected ? english : korean));
                Assert.That(image.uvRect, Is.EqualTo(englishSelected
                    ? InkLocalizedGameLogo.EnglishUvRect : new Rect(0, 0, 1, 1)));
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(900, 600)));
                InkLocalizedGameLogo.Bind(image);
                Assert.That(image.texture, Is.SameAs(englishSelected ? english : korean));
            }
        }

        [Test]
        public void RebindingLogoReplacesStaleEditorArtworkCache()
        {
            var stale = new Texture2D(2, 2) { name = "PreviousEnglishLogo" };
            var cache = typeof(InkLocalizedGameLogo).GetField("englishTexture", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                GameLocalization.SetLanguage(GameLanguage.English);
                cache.SetValue(null, stale);
                var image = NewHost("ReimportedLogo").AddComponent<RawImage>();
                var korean = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    MukJump.EditorTools.MukJumpSplashSceneBuilder.GameLogoPath);
                InkLocalizedGameLogo.Bind(image, korean);
                Assert.That(image.texture, Is.SameAs(Resources.Load<Texture2D>(InkLocalizedGameLogo.EnglishResourcePath)));
                Assert.That(image.texture, Is.Not.SameAs(stale));
            }
            finally { cache.SetValue(null, null); Object.DestroyImmediate(stale); }
        }

        [Test]
        public void EnglishLogoSourceHasRealTransparencyNotAPaintedCheckerboard()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(InkLocalizedGameLogo.EnglishAssetPath)), Is.True);
                Assert.That(source.width, Is.EqualTo(1536));
                Assert.That(source.height, Is.EqualTo(1024));
                Color32[] pixels = source.GetPixels32();
                Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length * .85f));
                Assert.That(pixels.Count(p => p.a > 128 && p.r < 120 && p.g < 120 && p.b < 120), Is.GreaterThan(50000));
                Assert.That(source.GetPixel(0, 0).a, Is.Zero);
                Assert.That(source.GetPixel(1535, 1023).a, Is.Zero);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void MukLogoUsesLongTaperedJWithoutOldPSwashesAndKeepsKoreanAlignment()
        {
            var english = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var korean = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(english.LoadImage(File.ReadAllBytes(InkLocalizedGameLogo.EnglishAssetPath)), Is.True);
                Assert.That(korean.LoadImage(File.ReadAllBytes(MukJump.EditorTools.MukJumpSplashSceneBuilder.GameLogoPath)), Is.True);
                Rect InkBounds(Texture2D texture)
                {
                    Color32[] pixels = texture.GetPixels32();
                    int left = texture.width, bottom = texture.height, right = 0, top = 0;
                    for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a <= 128) continue;
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1);
                    }
                    return Rect.MinMaxRect(left, bottom, right, top);
                }
                Rect en = InkBounds(english), ko = InkBounds(korean);
                Assert.That(en, Is.EqualTo(new Rect(93, 347, 1351, 307)),
                    "INK 문구나 P 장식 획이 남은 구 원화를 다시 연결하면 안 됩니다.");
                int Thickness(int x) => Enumerable.Range(619, 50).Count(y => english.GetPixel(x, y).a > .5f);
                Assert.That(Thickness(1100), Is.GreaterThan(Thickness(1250)));
                Assert.That(Thickness(1250), Is.GreaterThan(Thickness(1330)));
                Assert.That(Thickness(1330), Is.GreaterThan(0), "J 윗획은 M 너머까지 길고 가늘게 남깁니다.");
                Rect uv = InkLocalizedGameLogo.EnglishUvRect;
                Assert.That(en.width / uv.width, Is.EqualTo(ko.width).Within(.01));
                Assert.That((en.center.x - uv.x * english.width) / uv.width, Is.EqualTo(ko.center.x).Within(.01));
                Assert.That((en.center.y - uv.y * english.height) / uv.height, Is.EqualTo(ko.center.y).Within(.01));
            }
            finally { Object.DestroyImmediate(english); Object.DestroyImmediate(korean); }
        }

        [Test]
        public void RenderUpdatedEnglishLogoFromGeneratedLobby()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            var scene = MukJump.EditorTools.MukJumpSceneBuilder.BuildForTests();
            try
            {
                var lobby = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LobbyView>(true)).Single();
                lobby.SetNavigationPresentation(true, true);
                var logo = lobby.GetComponentInChildren<InkLocalizedGameLogo>(true).GetComponent<RawImage>();
                Assert.That(logo.texture, Is.SameAs(Resources.Load<Texture2D>(InkLocalizedGameLogo.EnglishResourcePath)));
                Assert.That(logo.uvRect, Is.EqualTo(InkLocalizedGameLogo.EnglishUvRect));
                RenderCanvas(lobby.gameObject, "lobby-logo");
            }
            finally { MukJump.EditorTools.MukJumpSceneBuilder.CloseTestScene(scene); }
        }

        [Test]
        public void SelectingSavedLanguageRepairsAnUnregisteredSceneLabel()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            Text label = NewText("OldScenePlay", "시작", new Vector2(440, 80), 46);
            InkLocalizedText.Bind(label);
            Invoke(label.GetComponent<InkLocalizedText>(), "Disconnect");
            label.text = "시작";
            Assert.That(GameLocalization.SetLanguage(GameLanguage.English), Is.True);
            Assert.That(label.text, Is.EqualTo("Play"));
        }

        [Test]
        public void InactiveDynamicTextAndRepeatedLayoutKeepTheirKoreanSource()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            Text label = NewText("HiddenBest", "최고 0m", new Vector2(440, 80), 46);
            label.gameObject.SetActive(false);
            InkLocalizedText.SetSource(label, "최고 204m");
            Assert.That(label.text, Is.EqualTo("Best 204m"));
            LobbyMenuLayout.ApplyRecord(label);
            LobbyMenuLayout.ApplyRecord(label);
            GameLocalization.SetLanguage(GameLanguage.Korean);
            Assert.That(label.text, Is.EqualTo("최고 204m"));
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        public void GeneratedLobbyAndHiddenHudUseSelectedLanguageImmediately(GameLanguage initial)
        {
            GameLocalization.SetLanguage(initial);
            var scene = MukJump.EditorTools.MukJumpSceneBuilder.BuildForTests();
            try
            {
                var roots = scene.GetRootGameObjects();
                var lobby = roots.SelectMany(root => root.GetComponentsInChildren<LobbyView>(true)).Single();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<GameplayHudView>(true)).Single();
                var labels = new[] { lobby.StartButton, lobby.GrowthButton, lobby.OptionsButton }
                    .Select(button => button.GetComponentInChildren<Text>(true)).ToArray();
                var logo = lobby.GetComponentsInChildren<InkLocalizedGameLogo>(true).Single().GetComponent<RawImage>();
                var koreanLogo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    MukJump.EditorTools.MukJumpSplashSceneBuilder.GameLogoPath);
                var englishLogo = Resources.Load<Texture2D>(InkLocalizedGameLogo.EnglishResourcePath);
                Assert.That(logo.texture, Is.SameAs(initial == GameLanguage.English ? englishLogo : koreanLogo));
                Assert.That(labels.Select(label => label.text), Is.EqualTo(initial == GameLanguage.English
                    ? new[] { "Play", "Growth", "Options" } : new[] { "시작", "성장", "옵션" }));
                lobby.SetNavigationPresentation(false, false);
                var options = NewHost("SwitchAllOptions").AddComponent<LobbyOptionsView>();
                options.BuildForTests();
                var languagePage = options.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LanguagePage");
                foreach (bool english in new[] { true, false, true })
                {
                    languagePage.Find(english ? "EnglishButton" : "KoreanButton").GetComponent<Button>().onClick.Invoke();
                    Assert.That(logo.texture, Is.SameAs(english ? englishLogo : koreanLogo));
                    Assert.That(labels.Select(label => label.text), Is.EqualTo(english
                        ? new[] { "Play", "Growth", "Options" } : new[] { "시작", "성장", "옵션" }));
                    foreach (var label in hud.GetComponentsInChildren<Text>(true)
                                 .Where(label => label.name is "HeightText" or "BestText" or "WindStateText"))
                    {
                        var binding = label.GetComponent<InkLocalizedText>();
                        Assert.That(binding, Is.Not.Null, label.name);
                        Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(binding.SourceText)), label.name);
                        if (english) Assert.That(label.text, Does.Not.Match("[가-힣]"), label.name);
                    }
                }
            }
            finally { MukJump.EditorTools.MukJumpSceneBuilder.CloseTestScene(scene); }
        }

        [Test]
        public void HiddenLobbyLabelsSwitchBeforeTheyAreShownAgain()
        {
            var root = NewHost("HiddenLobby");
            foreach (string copy in new[] { "시작", "성장", "옵션", "최고 132m" })
            {
                Text label = NewText(copy, copy, new Vector2(440, 80), 46);
                label.transform.SetParent(root.transform, false);
                InkLocalizedText.Bind(label);
                Invoke(label.GetComponent<InkLocalizedText>(), "OnDisable");
            }
            root.SetActive(false);
            GameLocalization.SetLanguage(GameLanguage.English);
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(label.name)), label.name);
            GameLocalization.SetLanguage(GameLanguage.Korean);
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                Assert.That(label.text, Is.EqualTo(label.name), label.name);
        }

        [Test]
        public void EnglishAdsHaveNoKoreanBeforeAnyCanvasRefresh()
        {
            var host = NewHost("EnglishAds");
            host.SetActive(false); // 광고 공급자 등록 없이 실제 UI 생성 경로만 검증한다.
            var ads = host.AddComponent<EditorTestAdsRuntime>();
            Invoke(ads, "BuildUi");
            GameLocalization.SetLanguage(GameLanguage.English);
            foreach (var placement in new[] { FullScreenAdPlacement.GameOverReviveReward,
                         FullScreenAdPlacement.PostRunInterstitial })
            {
                Invoke(ads, "ShowFullScreen", placement, (Action<bool>)(_ => { }));
                foreach (Text label in host.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(label.text, Does.Not.Match("[가-힣]"), label.name + ": " + label.text);
                    AssertFits(label);
                }
                Invoke(ads, "Complete", false);
            }
        }

        [Test]
        public void ExcludedPlayerNamesNeverTranslateEvenWhenMatchingUiCopy()
        {
            Text name = NewText("Name", "", new Vector2(440, 80), 40);
            InkLocalizedText.Bind(name);
            GameLocalization.SetLanguage(GameLanguage.English);
            LobbyOptionsView.FitLeaderboardName(name, "성장");
            Assert.That(name.text, Is.EqualTo("성장"));
            InkLocalizedText.BindTree(name.transform);
            InkLocalizedText.RefreshAll();
            Assert.That(name.text, Is.EqualTo("성장"));
            Assert.That(name.supportRichText, Is.False);
            Text unbound = NewText("UnboundName", "시작", new Vector2(440, 80), 40);
            InkLocalizedText.Exclude(unbound);
            InkLocalizedText.BindTree(unbound.transform);
            InkLocalizedText.RefreshAll();
            Assert.That(unbound.text, Is.EqualTo("시작"));
        }

        [Test]
        public void TemporaryReadFailureDoesNotLatchFallbackLanguage()
        {
            settings.SetString("MukJump.Settings.Language", "en");
            var unstable = new ReadFailingStore(settings);
            LobbySettingsProfile.UseStoreForTests(unstable);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.Korean));
            unstable.FailReads = false;
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.English));
        }

        [Test]
        public void RankHeadingUsesShortEnglishAndLiteralComparisonIsNotMarkup()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            Assert.That(GameLocalization.Translate("HP < 3"), Is.EqualTo("HP < 3"));
            Assert.That(GameLocalization.Translate("먹점프"), Is.EqualTo("Muk Jump"));
            Assert.That(Uri.UnescapeDataString(LobbyOptionsView.CustomerSupportMailUri), Does.EndWith("subject=Muk Jump Support"));
            foreach (string copy in new[] { "풍맥 상승", "바람길이 먹방울을 밀어 올립니다",
                         "상승기류 접근", "잠시 뒤 낙하를 받쳐 주는 강한 바람이 붑니다" })
                Assert.That(GameLocalization.Translate(copy), Does.Not.Match("[가-힣]"));
            var view = NewHost("EnglishLeaderboard").AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Invoke(view, "ShowLeaderboardPage");
            var page = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            var heading = page.Find("RankHeading").GetComponent<Text>();
            Assert.That(heading.text, Is.EqualTo("Rank"));
            AssertFits(heading);
            GameLocalization.SetLanguage(GameLanguage.Korean);
            Assert.That(heading.text, Is.EqualTo("순위"));
        }

        [Test]
        public void SettingsLanguageButtonsSwitchBothWaysAndReturnToOptions()
        {
            var view = NewHost("EnglishOptions").AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Transform panel = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
            Transform page = panel.Find("OptionsPage");
            var language = page.Find("LanguageButton").GetComponent<Button>();
            language.onClick.Invoke();
            Assert.That(panel.Find("LanguagePage").GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            panel.Find("LanguagePage/EnglishButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameLocalization.IsEnglish, Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(page.Find("Title").GetComponent<Text>().text, Is.EqualTo("Settings"));
            Assert.That(page.Find("LanguageButton/Paper/Label").GetComponent<Text>().text, Is.EqualTo("English"));
            AssertEnglishAndFits(page);
            language.onClick.Invoke();
            Assert.That(panel.Find("LanguagePage/KoreanButton/Paper/Label").GetComponent<Text>().text, Is.EqualTo("한국어"));
            panel.Find("LanguagePage/KoreanButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameLocalization.IsEnglish, Is.False);
            Assert.That(page.Find("Title").GetComponent<Text>().text, Is.EqualTo("설정"));
        }

        [Test]
        public void JapaneseSettingsAndGrowthAreReadable()
        {
            GameLocalization.SetLanguage(GameLanguage.Japanese);
            var options = NewHost("JapaneseOptions").AddComponent<LobbyOptionsView>();
            options.BuildForTests();
            var page = options.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
            foreach (Text label in page.GetComponentsInChildren<Text>(true)) AssertFits(label);
            var view = NewHost("JapaneseGrowth").AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            view.SelectGrowthForTests(0);
            var screen = view.transform.Find("PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/PermanentGrowthScreen");
            foreach (Text label in screen.GetComponentsInChildren<Text>(true)) AssertFits(label);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void JapaneseTutorialFits(int pageIndex)
        {
            GameLocalization.SetLanguage(GameLanguage.Japanese);
            var first = NewHost("JapaneseFirstTutorial").AddComponent<FirstRunTutorialController>();
            first.BuildForTests();
            first.BeginForTests();
            for (int i = 0; i < pageIndex; i++) first.AdvanceForTests();
            var firstPanel = first.transform.Find("FirstRunTutorialCanvas/SafeAreaRoot/TutorialPanel");
            foreach (Text label in firstPanel.GetComponentsInChildren<Text>(true)) AssertFits(label);
            var options = NewHost("JapaneseTutorialReplay").AddComponent<LobbyOptionsView>();
            options.BuildForTests();
            Invoke(options, "ShowTutorialPage", pageIndex);
            var replay = options.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/TutorialPage");
            foreach (Text label in replay.GetComponentsInChildren<Text>(true)) AssertFits(label);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SpotlightAndReviewUseConciseEnglishWithoutClipping(int pageIndex)
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            var first = NewHost("EnglishFirstTutorial").AddComponent<FirstRunTutorialController>();
            first.BuildForTests();
            first.BeginForTests();
            for (int i = 0; i < pageIndex; i++) first.AdvanceForTests();
            var firstPanel = first.transform.Find("FirstRunTutorialCanvas/SafeAreaRoot/TutorialPanel");
            AssertEnglishAndFits(firstPanel);
            var options = NewHost("EnglishTutorialReplay").AddComponent<LobbyOptionsView>();
            options.BuildForTests();
            Invoke(options, "ShowTutorialPage", pageIndex);
            var replay = options.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/TutorialPage");
            AssertEnglishAndFits(replay);
            Assert.That(firstPanel.Find("Title").GetComponent<Text>().text,
                Is.EqualTo(GameLocalization.Translate(FirstRunTutorialController.GetStep(pageIndex).Title)));
            Assert.That(replay.Find("TutorialTitle").GetComponent<Text>().text,
                Is.EqualTo(GameLocalization.Translate(GameplayTutorialCatalog.Get(Mathf.Min(pageIndex, 2)).Title)));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EightStageGrowthTranslatesStaticAndDynamicCopy(int selected)
        {
            var view = NewHost("EnglishGrowth").AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            PermanentGrowthProfile.DebugRefillCurrency();
            view.SelectGrowthForTests(selected);
            GameLocalization.SetLanguage(GameLanguage.English);
            var screen = view.transform.Find("PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/PermanentGrowthScreen");
            AssertEnglishAndFits(screen);
            var choice = PermanentGrowthCatalog.Choices[selected];
            for (int level = 0; level < 8; level++)
            {
                Assert.That(PermanentGrowthProfile.TryPurchase(choice.Type), Is.True);
                Invoke(view, "Refresh");
                AssertEnglishAndFits(screen);
            }
            Assert.That(screen.Find("FocusedGrowth/FocusCompleteLabel").GetComponent<Text>().text, Is.EqualTo("Fully Grown"));
        }

        [Test]
        public void PauseAndResultReuseTranslatedLabels()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            var pause = NewHost("EnglishPause").AddComponent<PauseMenuView>();
            Invoke(pause, "BuildIfNeeded");
            AssertEnglishAndFits(pause.transform.Find("PauseMenuCanvas/PauseOverlay/SafeAreaRoot/PauseScroll"));
            var result = NewHost("EnglishResult").AddComponent<GameOverPopupView>();
            Invoke(result, "BuildIfNeeded");
            foreach (int height in new[] { 0, 8, 15, 39, 132 })
            {
                Invoke(result, "BindResult", new GameOverResult(height, 132, height > 132, 1, 1, true));
                AssertEnglishAndFits(result.transform);
            }
        }

        [Test]
        public void AllAccountAndRecoveryMessagesHaveEnglishAndFitCaptionArea()
        {
            GameLocalization.SetLanguage(GameLanguage.English);
            Type table = typeof(GameLocalization).Assembly.GetType("MukJump.Core.EnglishTranslationTable");
            var values = (Dictionary<string, string>)table.GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Text label = NewText("AccountStatus", "", new Vector2(680, 130), 32);
            foreach (var pair in values)
            {
                Assert.That(GameLocalization.Translate(pair.Key), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(Regex.IsMatch(pair.Value, "[가-힣]"), Is.False, pair.Key);
                if (pair.Key.Length < 25 || pair.Key.Contains('\n') || pair.Value.Contains('\n')) continue;
                label.text = pair.Value;
                AssertFits(label);
            }
            string combined = "Apple 연결 해제 요청에 실패했습니다 현재 계정 소유자를 다시 확인해 주세요";
            Assert.That(GameLocalization.Translate(combined), Does.Not.Match("[가-힣]"));
            Assert.That(GameLocalization.Translate("바람 고개\n<size=30>산등성이의 바람이 조금 거세집니다</size>"),
                Is.EqualTo("Wind Pass\n<size=30>The ridge winds grow stronger.</size>"));
        }

        [TestCase("settings")]
        [TestCase("privacy")]
        [TestCase("language")]
        [TestCase("account")]
        [TestCase("account-toast")]
        [TestCase("tutorial")]
        [TestCase("growth")]
        [TestCase("growth-reset")]
        public void RenderEnglishUiFixture(string pageName)
        {
            RenderLocalizedUiFixture(pageName, GameLanguage.English);
        }

        [TestCase("settings")] [TestCase("language")] [TestCase("account")]
        [TestCase("tutorial")] [TestCase("growth")] [TestCase("growth-reset")]
        public void RenderJapaneseUiFixture(string pageName)
        {
            RenderLocalizedUiFixture(pageName, GameLanguage.Japanese);
        }

        void RenderLocalizedUiFixture(string pageName, GameLanguage language)
        {
            // 실제 uGUI를 격리된 EditMode 카메라로 렌더한다. 실기기/서버 검증 자료는 아니다.
            GameLocalization.SetLanguage(language);
            GameObject host = NewHost("EnglishRender");
            if (pageName == "growth" || pageName == "growth-reset")
            {
                var view = host.AddComponent<PermanentGrowthView>();
                view.BuildForTests();
                PermanentGrowthProfile.DebugRefillCurrency();
                view.SelectGrowthForTests(1);
                view.ScreenRoot.anchoredPosition = Vector2.zero;
                var safe = (RectTransform)view.ScreenRoot.Find("SafeAreaRoot");
                safe.anchorMin = Vector2.zero;
                safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                var screen = (RectTransform)safe.Find("PermanentGrowthScreen");
                screen.localScale = Vector3.one;
                screen.anchoredPosition = Vector2.zero;
                if (pageName == "growth-reset")
                {
                    Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
                    view.NodeResetButton.onClick.Invoke();
                    Assert.That(view.IsResetConfirmationOpen, Is.True);
                    var popup = view.ScreenRoot.Find("GrowthResetConfirmation/SafeAreaRoot/PopupContent/ResetPanel");
                    popup.GetComponent<HanjiScrollFrame>().SetPose(1, 0, false);
                    popup.GetComponent<CanvasGroup>().alpha = 1;
                    popup.GetComponent<CanvasGroup>().interactable = true;
                    popup.Find("HanjiScrollArt").GetComponent<CanvasGroup>().alpha = 1;
                }
            }
            else
            {
                var view = host.AddComponent<LobbyOptionsView>();
                view.SetDisplayMetricsForTests(1080, 1920, new Rect(0, 0, 1080, 1920));
                view.BuildForTests();
                string method = pageName == "privacy" ? "ShowAnalyticsPrivacyPage" : pageName == "language" ? "ShowLanguagePage" :
                    pageName.StartsWith("account") ? "ShowAccountPage" :
                    pageName == "tutorial" ? "ShowTutorialPage" : "ShowOptionsPage";
                if (pageName == "privacy") Invoke(view, "SetVisible", true);
                if (pageName == "tutorial") Invoke(view, method, 2);
                else Invoke(view, method);
                var panel = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
                if (pageName == "privacy")
                    Assert.That(panel.Find("AnalyticsPrivacyPage").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                panel.GetComponent<CanvasGroup>().alpha = 1;
                foreach (var frame in host.GetComponentsInChildren<HanjiScrollFrame>(true))
                    frame.SetPose(1, 0, false);
                panel.Find("HanjiScrollArt").GetComponent<CanvasGroup>().alpha = 1;
                if (pageName == "account-toast")
                {
                    Invoke(view, "ShowAccountNotice", "서버 연결을 확인할 수 없습니다. 네트워크를 확인한 뒤 다시 시도해 주세요");
                    Invoke(view, "AdvanceAccountToast", .2f, true);
                }
            }
            RenderCanvas(host, language == GameLanguage.Japanese ? "ja-" + pageName : pageName);
        }

        [Test]
        public void AccountToastReplacementKeepsCurrentOpacity()
        {
            var view = NewHost("ToastContinuity").AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            Invoke(view, "ShowAccountPage");
            Invoke(view, "ShowAccountNotice", "첫 번째 안내");
            Invoke(view, "AdvanceAccountToast", .08f, true);
            var toast = view.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/AccountToast").GetComponent<CanvasGroup>();
            float before = toast.alpha;
            Assert.That(before, Is.GreaterThan(0f));
            Invoke(view, "ShowAccountNotice", "두 번째 안내");
            Assert.That(toast.alpha, Is.EqualTo(before).Within(.001f));
            Invoke(view, "AdvanceAccountToast", .08f, true);
            Assert.That(toast.alpha, Is.GreaterThanOrEqualTo(before));
            Invoke(view, "ClearAccountToast");
            Assert.That(toast.alpha, Is.Zero);
            Assert.That(toast.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        public void ResetPopupLocalizesEveryLabelBeforeOpeningAndCancelPreservesUpgrades(GameLanguage initial)
        {
            GameLocalization.SetLanguage(initial);
            var view = NewHost("LocalizedResetPopup").AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            PermanentGrowthProfile.DebugRefillCurrency();
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            string savedGrowth = growth.Json;
            var popup = view.ScreenRoot.Find("GrowthResetConfirmation/SafeAreaRoot/PopupContent/ResetPanel");
            foreach (GameLanguage language in new[] { initial, GameLanguage.English, GameLanguage.Korean, GameLanguage.English })
            {
                Assert.That(view.IsResetConfirmationOpen, Is.False);
                GameLocalization.SetLanguage(language);
                bool english = language == GameLanguage.English;
                Assert.That(view.CancelResetButton.GetComponentInChildren<Text>(true).text,
                    Is.EqualTo(english ? "Cancel" : "취소"), "숨겨진 팝업도 열기 전에 번역해야 합니다.");
                view.NodeResetButton.onClick.Invoke();
                Assert.That(view.IsResetConfirmationOpen, Is.True);
                Assert.That(view.ConfirmResetButton.GetComponentInChildren<Text>(true).text,
                    Is.EqualTo(english ? "Reset" : "초기화"));
                Assert.That(popup.Find("ResetTitle").GetComponent<Text>().text,
                    Is.EqualTo(english ? "Reset your upgrades?" : "초기화하시겠습니까?"));
                Assert.That(popup.Find("ResetMessage").GetComponent<Text>().text,
                    Is.EqualTo(english ? "All spent Inklight will be refunded." : "사용한 먹빛은 모두 돌려받습니다."));
                if (english) AssertEnglishAndFits(popup);
                view.CancelResetButton.onClick.Invoke();
                Assert.That(view.IsResetConfirmationOpen, Is.False);
                Assert.That(growth.Json, Is.EqualTo(savedGrowth), "취소는 성장이나 먹빛을 변경하지 않습니다.");
            }
        }

        void RenderCanvas(GameObject host, string pageName)
        {
            Canvas canvas = host.GetComponentInChildren<Canvas>(true);
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(1080, 1920);
            rect.position = Vector3.zero;
            rect.localScale = Vector3.one;
            canvas.GetComponent<CanvasGroup>().alpha = 1;
            foreach (Transform node in host.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            foreach (var button in host.GetComponentsInChildren<Button>(true))
            {
                button.enabled = false;
                button.enabled = true;
            }
            var camera = NewHost("EnglishRenderCamera").AddComponent<Camera>();
            camera.scene = host.scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            const int width = 864, height = 1536;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D capture = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                canvas.enabled = false;
                canvas.enabled = true;
                InkLocalizedText.RefreshAll();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                const string folder = "output/quality-polish/english-ui";
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes($"{folder}/{pageName}.png", capture.EncodeToPNG());
                Assert.That(capture.GetPixels32().Count(pixel => pixel.r < 100 && pixel.g < 100 && pixel.b < 100),
                    Is.GreaterThan(500), "빈 렌더는 UI 검증으로 인정하지 않습니다.");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (capture != null) Object.DestroyImmediate(capture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        sealed class ReadFailingStore : ILobbySettingsStore
        {
            readonly ILobbySettingsStore inner;
            public bool FailReads = true;
            public ReadFailingStore(ILobbySettingsStore inner) => this.inner = inner;
            public float GetFloat(string key, float fallback) => FailReads
                ? throw new InvalidOperationException("Injected transient read failure") : inner.GetFloat(key, fallback);
            public int GetInt(string key, int fallback) => inner.GetInt(key, fallback);
            public string GetString(string key, string fallback) => inner.GetString(key, fallback);
            public void SetFloat(string key, float value) => inner.SetFloat(key, value);
            public void SetInt(string key, int value) => inner.SetInt(key, value);
            public void SetString(string key, string value) => inner.SetString(key, value);
            public void Save() => inner.Save();
        }

        Text NewText(string name, string source, Vector2 size, int fontSize)
        {
            var host = NewHost(name);
            host.AddComponent<RectTransform>().sizeDelta = size;
            Text label = host.AddComponent<Text>();
            label.font = InkPalette.UiFont;
            label.fontSize = fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = source;
            return label;
        }

        GameObject NewHost(string name)
        {
            var host = new GameObject(name);
            cleanup.Add(host);
            return host;
        }

        static void AssertEnglishAndFits(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            // 강제 번역/렌더 없이 검사해야 첫 프레임에 한국어가 보이는 회귀를 잡는다.
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (string.IsNullOrEmpty(label.text)) continue;
                if (label.transform.IsChildOf(root) && label.name == "SealText") { AssertFits(label); continue; }
                Assert.That(label.text, Does.Not.Match("[가-힣]"), label.name + ": " + label.text);
                AssertFits(label);
            }
        }

        static void AssertFits(Text label)
        {
            var bounded = new TextGenerator();
            var full = new TextGenerator();
            Vector2 size = label.rectTransform.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            var settings = label.GetGenerationSettings(size);
            settings.verticalOverflow = VerticalWrapMode.Truncate;
            bounded.Populate(label.text, settings);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            full.Populate(label.text, settings);
            Assert.That(bounded.characterCountVisible, Is.EqualTo(full.characterCountVisible), label.name + ": " + label.text);
            if (label.horizontalOverflow == HorizontalWrapMode.Overflow)
                Assert.That(full.GetPreferredWidth(label.text, settings) / Mathf.Max(0.001f, label.pixelsPerUnit),
                    Is.LessThanOrEqualTo(size.x + 1), label.name + ": " + label.text);
        }

        static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    }
}
