using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class PopupTypographyTests
    {
        GameObject host;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            PointerInput.ResetSuppressionForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void PauseTitleAndActionsAreLargeDarkAndUnclippedAfterRebind()
        {
            host = new GameObject("PauseTypographyTest");
            var view = host.AddComponent<PauseMenuView>();
            Invoke(view, "BuildIfNeeded");
            var panel = host.transform.Find("PauseMenuCanvas/PauseOverlay/SafeAreaRoot/PauseScroll");
            Text title = panel.Find("Title").GetComponent<Text>();
            AssertPauseLabels(panel);
            // Play 중 재컴파일로 참조가 초기화돼도 구형 58px 제목으로 남으면 안 된다.
            title.fontSize = 58;
            title.fontStyle = FontStyle.Normal;
            typeof(PauseMenuView).GetField("rootCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, null);
            Invoke(view, "BuildIfNeeded");
            AssertPauseLabels(panel);
            Assert.That(panel.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(3));
        }

        [TestCase(0, GameLanguage.Korean)]
        [TestCase(1, GameLanguage.Korean)]
        [TestCase(2, GameLanguage.Korean)]
        [TestCase(3, GameLanguage.Korean)]
        [TestCase(0, GameLanguage.English)]
        [TestCase(1, GameLanguage.English)]
        [TestCase(2, GameLanguage.English)]
        [TestCase(3, GameLanguage.English)]
        public void EveryTutorialPageFitsWithoutShrinking(int page, GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("TutorialTypographyTest");
            var view = host.AddComponent<FirstRunTutorialController>();
            view.BuildForTests();
            view.BeginForTests();
            for (int i = 0; i < page; i++) view.AdvanceForTests();
            var panel = host.transform.Find("FirstRunTutorialCanvas/SafeAreaRoot/TutorialPanel");
            Text title = panel.Find("Title").GetComponent<Text>();
            Text body = panel.Find("Description").GetComponent<Text>();
            Assert.That(title.fontSize, Is.EqualTo(64));
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(body.fontSize, Is.EqualTo(52));
            Assert.That(body.color, Is.EqualTo(InkPalette.TextLight));
            AssertFits(title, 1);
            AssertFits(body, 5);
            Assert.That(panel.Find("PauseHint"), Is.Null);
            Text progress = panel.Find("Progress").GetComponent<Text>();
            Assert.That(progress.fontSize, Is.EqualTo(44));
            Assert.That(progress.text, Is.EqualTo($"{page + 1} / 4"));
            AssertFits(progress, 1);
            var next = panel.Find("TapHint").GetComponent<Text>();
            Assert.That(next.text, Is.EqualTo(GameLocalization.Translate(page == 3 ? string.Empty : "탭하여 다음")));
            if (language == GameLanguage.English) Assert.That(body.text, Does.Not.Match("[가-힣]"));
            Assert.That(title.rectTransform.anchoredPosition.y - title.rectTransform.rect.height / 2f,
                Is.GreaterThanOrEqualTo(body.rectTransform.anchoredPosition.y + body.rectTransform.rect.height / 2f + 12f));
        }

        [TestCase(1080, 1920, 0, 0)]
        [TestCase(1179, 2556, 177, 102)]
        [TestCase(1440, 3200, 100, 80)]
        public void PauseTypographyStaysReadableAndInsideSafeArea(int width, int height, int top, int bottom)
        {
            Rect safe = new Rect(0, bottom, width, height - top - bottom);
            float scale = MobileUiLayout.CalculateFitScale(new Vector2(760, 680), safe,
                width, height, new Vector2(28, 32));
            float screenScale = height / 1920f * scale;
            Assert.That(80f * screenScale / width, Is.GreaterThanOrEqualTo(0.069f),
                "가로 화면 대비 제목이 다시 작은 캡션 수준으로 축소되면 안 됩니다.");
            Assert.That(760f * screenScale, Is.LessThanOrEqualTo(safe.width));
            Assert.That(680f * screenScale, Is.LessThanOrEqualTo(safe.height));
        }

        [TestCase(0, GameLanguage.Korean)]
        [TestCase(1, GameLanguage.Korean)]
        [TestCase(2, GameLanguage.Korean)]
        [TestCase(0, GameLanguage.English)]
        [TestCase(1, GameLanguage.English)]
        [TestCase(2, GameLanguage.English)]
        public void OptionsTutorialUsesSameLargeTypeAndFitsEveryPage(int page, GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("OptionsTypographyTest");
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            typeof(LobbyOptionsView).GetMethod("ShowTutorialPage", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { page });
            Text title = null;
            Text body = null;
            foreach (Text text in host.GetComponentsInChildren<Text>(true))
            {
                if (text.name == "TutorialTitle") title = text;
                if (text.name == "TutorialDescription") body = text;
            }
            Assert.That(title, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(title.fontSize, Is.EqualTo(64));
            Assert.That(body.fontSize, Is.EqualTo(52));
            Assert.That(title.text, Is.EqualTo(GameLocalization.Translate(GameplayTutorialCatalog.Get(page).Title)));
            Assert.That(body.text, Is.EqualTo(GameLocalization.Translate(GameplayTutorialCatalog.Get(page).Description)));
            AssertFits(title, 1);
            AssertFits(body, 5);
        }

        static void AssertPauseLabels(Transform panel)
        {
            Text title = panel.Find("Title").GetComponent<Text>();
            Assert.That(title.fontSize, Is.EqualTo(80));
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(title.color, Is.EqualTo(InkPalette.Ink));
            foreach (Shadow effect in title.GetComponents<Shadow>()) Assert.That(effect.enabled, Is.False);
            AssertFits(title, 1);
            foreach (string button in new[] { "ResumeButton", "LobbyButton" })
            {
                Text label = panel.Find(button + "/Label").GetComponent<Text>();
                Assert.That(label.fontSize, Is.EqualTo(56));
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Bold));
                AssertFits(label, 1);
                RectTransform rect = (RectTransform)label.transform.parent;
                Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(120));
            }
        }

        static void AssertFits(Text label, int maxLines)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(label.resizeTextForBestFit, Is.False);
            Assert.That(label.color.a, Is.EqualTo(1f));
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.01f), label.text);
            var bounded = new TextGenerator();
            var full = new TextGenerator();
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            // CanvasScaler의 실제 pixelsPerUnit을 보존해야 표시 크기 그대로 검증된다.
            settings.verticalOverflow = VerticalWrapMode.Truncate;
            bounded.Populate(label.text, settings);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            full.Populate(label.text, settings);
            Assert.That(bounded.characterCountVisible, Is.EqualTo(full.characterCountVisible), label.text);
            Assert.That(full.lineCount, Is.LessThanOrEqualTo(maxLines), label.text);
        }

        static void Invoke(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
