using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class InkWardrobeSectionsPlayTests
    {
        [UnityTest, Timeout(120000), Category("WardrobeSectionsPlay")]
        public IEnumerator ThreeSectionsRenderAndBrowseWithoutChangingOwnedOrEquippedOutfits()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            InkPremiumAppearance.UseForTests(false, null);
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var oldLanguage = GameLocalization.Language;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                var view = Object.FindAnyObjectByType<InkWardrobeView>();
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!view.CanBrowse && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(view.CanBrowse, Is.True);
                var ids = Enumerable.Range(0, InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt).ToArray();
                var owned = ids.Select(InkSkinAchievements.IsUnlocked).ToArray();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); view.Select(InkSkinId.Ink);
                    view.Presentation.Cancel(); view.ToggleCollection(); yield return Wait(.3f);
                    Assert.That(view.CollectionExpanded, Is.True);
                    for (int group = 0; group < 3; group++)
                    {
                        var heading = view.CollectionContent.Find("CollectionSection" + group).GetComponent<Text>();
                        string[] titles = language == GameLanguage.Japanese
                            ? new[] { "通常の衣装", "特殊能力", "秘密の衣装" }
                            : language == GameLanguage.English
                                ? new[] { "Regular Outfits", "Special", "Secret Costume" }
                                : new[] { "일반 의상", "특수 능력", "비밀 의상" };
                        Assert.That(heading.text, Is.EqualTo(titles[group]));
                        Assert.That(heading.preferredWidth, Is.LessThanOrEqualTo(heading.rectTransform.rect.width));
                        float top = -heading.rectTransform.anchoredPosition.y - heading.rectTransform.rect.height * .5f;
                        view.CollectionScroll.StopMovement();
                        view.CollectionContent.anchoredPosition = new Vector2(0, Mathf.Clamp(top - 8, 0,
                            view.CollectionContent.rect.height - view.CollectionScroll.viewport.rect.height));
                        yield return Wait(.3f); Canvas.ForceUpdateCanvases();
                        Assert.That(heading.canvasRenderer.cull, Is.False);
                        var first = ids.First(id => InkSkinCatalog.BrowseGroup(id) == group);
                        var card = (RectTransform)view.CollectionContent.Find("SkinCard" + (int)first);
                        Assert.That(card.anchoredPosition.y + card.rect.height * .5f,
                            Is.LessThan(heading.rectTransform.anchoredPosition.y - heading.rectTransform.rect.height * .5f));
                        if (group == 2)
                            Assert.That(card.Find("CardName").GetComponent<Text>().text, Is.EqualTo("?"));
                        yield return Capture("sections-" + language + "-" + group);
                    }
                    view.ToggleCollection();
                }
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                int saves = settings.SaveCount;
                var ability = ids.First(id => InkSkinCatalog.BrowseGroup(id) == 1);
                view.Select(ability); view.ToggleCollection(); yield return Wait(.2f);
                view.CollectionContent.Find("SkinCard" + (int)ability).GetComponent<Button>().onClick.Invoke();
                Assert.That(view.Selected, Is.EqualTo(ability));
                Assert.That(view.CollectionExpanded, Is.False);
                Assert.That(view.CollectionContent.Find("CollectionSection0").gameObject.activeSelf, Is.False);
                view.Select(InkSkinId.Ink); view.Presentation.Cancel();
                view.StepSelection(-1); Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
                var scroll = (InkWardrobeScroll)view.CollectionScroll;
                scroll.content.anchoredPosition = new Vector2(-3.35f * scroll.CardStride, 0);
                var pointer = new PointerEventData(EventSystem.current) { pointerId = 11 };
                scroll.OnPointerDown(pointer); scroll.OnPointerUp(pointer);
                Assert.That(view.Selected, Is.EqualTo(ids[3]));
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
                Assert.That(settings.SaveCount, Is.EqualTo(saves));
                Assert.That(ids.Select(InkSkinAchievements.IsUnlocked), Is.EqualTo(owned));
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
            }
            finally
            {
                InkPremiumAppearance.RestoreForTests();
                GameLocalization.SetLanguage(oldLanguage); Application.runInBackground = oldBackground;
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/wardrobe-sections-20260924");
            File.Copy(path, "output/qa/wardrobe-sections-20260924/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
