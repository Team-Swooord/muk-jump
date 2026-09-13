using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkMythicPresentationTests
    {
        [UnityTest, Category("NaturalAccessoryPresentation"), Timeout(120000)]
        public IEnumerator NaturalAccessoriesAttachWithoutReplacingTheInkBody()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                float deadline = Time.realtimeSinceStartup + 25;
                while ((GameManager.Instance == null || GameManager.Instance.IsTransitioning || StartupBrandSplash.IsBlockingInput)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                yield return Wait(.4f);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>();
                var stage = (RectTransform)wardrobe.Paper.Find("ShowroomStage");
                var preview = (RectTransform)stage.Find("Preview");
                var body = preview.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                var accessory = preview.Find("Accessory").GetComponent<UnityEngine.UI.Image>();
                var presentation = stage.GetComponent<InkShowroomPresentation>();
                var original = body.sprite;
                GameLocalization.SetLanguage(GameLanguage.Korean);
                foreach (var id in InkNaturalAccessoryTests.Changed)
                {
                    wardrobe.Select(id);
                    // 에디터 프레임 지연 시에도 실제 교체 연출이 끝난 착용 위치를 검사한다.
                    deadline = Time.realtimeSinceStartup + 5;
                    while (presentation.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(presentation.IsPlaying, Is.False, "교체 연출 완료: " + id);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(body.sprite, Is.SameAs(original));
                    Assert.That(accessory.sprite, Is.SameAs(InkSkinCatalog.Sprite(id)));
                    Assert.That(accessory.transform.GetSiblingIndex() < body.transform.GetSiblingIndex(),
                        Is.EqualTo(InkSkinCatalog.BehindBody(id)));
                    var bounds = LobbySkinPreview.CostumeBounds(id, accessory.sprite);
                    foreach (var point in new[] { bounds.min, bounds.max, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMin) })
                        Assert.That(stage.rect.Contains(stage.InverseTransformPoint(preview.TransformPoint(point * preview.rect.width))), Is.True, "의상 잘림: " + id);
                    yield return Capture("natural-" + id);
                }
                SkinDebugSession.SetAllUnlocked(false); wardrobe.Select(InkSkinId.ZodiacDragon); yield return Wait(.6f);
                Assert.That(body.enabled || accessory.enabled, Is.False, "히든 미발견 상태는 유지한다");
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NewSecretsAndRefinedTailFitOriginalInkInThreeLanguages()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                float deadline = Time.realtimeSinceStartup + 25;
                while ((GameManager.Instance == null || GameManager.Instance.IsTransitioning || StartupBrandSplash.IsBlockingInput)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                yield return Wait(.4f);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>();
                var stage = (RectTransform)wardrobe.Paper.Find("ShowroomStage");
                var preview = (RectTransform)stage.Find("Preview");
                var body = preview.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                var originalBody = body.sprite;
                foreach (var id in new[] { InkSkinId.DragonTail }.Concat(InkMythicSkinTests.Rewards))
                {
                    GameLocalization.SetLanguage(GameLanguage.Korean); wardrobe.Select(id); yield return Wait(.8f);
                    Assert.That(body.sprite, Is.SameAs(originalBody), "먹 몸체 교체 금지: " + id);
                    Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null, id.ToString());
                    var bounds = LobbySkinPreview.CostumeBounds(id, InkSkinCatalog.Sprite(id));
                    foreach (var point in new[] { bounds.min, bounds.max, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMin) })
                        Assert.That(stage.rect.Contains(stage.InverseTransformPoint(preview.TransformPoint(point * preview.rect.width))), Is.True, "의상 잘림: " + id);
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        GameLocalization.SetLanguage(language); yield return Wait(.1f); Canvas.ForceUpdateCanvases();
                        foreach (var name in new[] { "ItemLore", "Description", "Achievement", "AchievementProgress" })
                        {
                            var text = wardrobe.Paper.Find(name).GetComponent<UnityEngine.UI.Text>();
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), id + " / " + language + " / " + name);
                        }
                        if (language == GameLanguage.Korean || id == InkSkinId.NineTails || id == InkSkinId.ZodiacSnake)
                            yield return Capture(id + "-" + language);
                    }
                }
                wardrobe.ToggleCollection(); yield return Wait(.3f); Canvas.ForceUpdateCanvases();
                var grid = wardrobe.Paper.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>();
                Assert.That(grid.cellSize.y, Is.GreaterThanOrEqualTo(180), "61종 전체 보기의 최소 썸네일 크기");
                var scroll = wardrobe.Paper.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                scroll.verticalNormalizedPosition = 0; yield return Wait(.2f);
                yield return Capture("collection-last-row");
                wardrobe.ToggleCollection(); yield return Wait(.3f);
                SkinDebugSession.SetAllUnlocked(false); wardrobe.Select(InkSkinId.NineTails); yield return Wait(.8f);
                Assert.That(body.enabled, Is.False, "미발견 히든은 원먹·장식도 공개하지 않는다");
                Assert.That(preview.Find("Accessory").GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                yield return Capture("concealed");
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/mythic-skins");
            File.Copy(path, "output/qa/mythic-skins/" + name + ".png", true);
        }
    }
}
