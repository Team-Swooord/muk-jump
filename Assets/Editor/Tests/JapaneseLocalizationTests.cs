using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class JapaneseLocalizationTests
    {
        [SetUp] public void Setup() => LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        [TearDown] public void Cleanup()
        {
            GameLocalization.SetLanguage(GameLanguage.Korean);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [TestCase(SystemLanguage.Japanese, GameLanguage.Japanese)]
        [TestCase(SystemLanguage.Korean, GameLanguage.Korean)]
        [TestCase(SystemLanguage.English, GameLanguage.English)]
        [TestCase(SystemLanguage.French, GameLanguage.English)]
        public void FirstLanguageFollowsDeviceLanguage(SystemLanguage source, GameLanguage expected) =>
            Assert.That(LobbySettingsProfile.DetectLanguage(source), Is.EqualTo(expected));

        [Test] public void EntireExistingCatalogHasJapaneseTranslationsAndGlyphs()
        {
            var assembly = typeof(GameLocalization).Assembly;
            var en = (Dictionary<string, string>)assembly.GetType("MukJump.Core.EnglishTranslationTable")
                .GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var ja = (Dictionary<string, string>)assembly.GetType("MukJump.Core.JapaneseTranslationTable")
                .GetField("Values", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var font = Resources.Load<Font>("MukJump/Fonts/KaiseiDecol-Regular");
            Assert.That(font, Is.Not.Null);
            GameLocalization.SetLanguage(GameLanguage.Japanese);
            foreach (var item in en)
            {
                Assert.That(ja.ContainsKey(item.Value), Is.True, "Missing: " + item.Key);
                string rendered = GameLocalization.Translate(item.Key);
                foreach (char c in rendered)
                    if (!char.IsWhiteSpace(c)) Assert.That(font.HasCharacter(c), Is.True, "Glyph: " + c + " in " + item.Key);
            }
        }

        [TestCase("먹빛 19개 필요", "墨光が19個必要")]
        [TestCase("최고 237m", "最高 237m")]
        [TestCase("서버 저장을 다시 시도합니다 (400/ValidationException)", "サーバー保存を再試行中 (400/ValidationException)")]
        [TestCase("예상 먹빛 +3", "予測 墨光 +3")]
        public void DynamicValuesRemainIntact(string source, string expected)
        {
            GameLocalization.SetLanguage(GameLanguage.Japanese);
            Assert.That(GameLocalization.Translate(source), Is.EqualTo(expected));
        }

        [Test] public void ChoiceSurvivesReloadAndCloudSettings()
        {
            var store = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(store);
            GameLocalization.SetLanguage(GameLanguage.Japanese);
            LobbySettingsProfile.UseStoreForTests(store);
            LobbySettingsProfile.ApplyCloudSettings(.4f, .5f, 9);
            Assert.That(GameLocalization.Language, Is.EqualTo(GameLanguage.Japanese));
        }

        [Test] public void BoundTextRoundTripsWithoutLosingOriginal()
        {
            GameLocalization.SetLanguage(GameLanguage.Korean);
            var go = new GameObject("JapaneseLabel", typeof(Text));
            try
            {
                var text = go.GetComponent<Text>();
                var original = Resources.Load<Font>("MukJump/Fonts/NanumBrushScript-Regular");
                text.font = original;
                text.text = "성장";
                InkLocalizedText.Bind(text);
                GameLocalization.SetLanguage(GameLanguage.Japanese);
                Assert.That(text.text, Is.EqualTo("成長"));
                Assert.That(text.font.name, Does.Contain("Kaisei"));
                GameLocalization.SetLanguage(GameLanguage.English);
                Assert.That(text.text, Is.EqualTo("Growth"));
                Assert.That(text.font, Is.EqualTo(original));
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(text.text, Is.EqualTo("성장"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("墨の旅人", true)] [TestCase("そらねこ", true)]
        [TestCase("シネ", false)] [TestCase("ま_ん_こ", false)]
        public void JapaneseNicknamesFollowContentPolicy(string name, bool allowed) =>
            Assert.That(MukJumpIdentityProfile.TryNormalizeNickname(name, out _, out _), Is.EqualTo(allowed));
    }
}
