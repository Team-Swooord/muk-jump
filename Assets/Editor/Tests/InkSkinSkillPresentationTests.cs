using System.Collections;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkSkinSkillPresentationTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator RewardAndSkillFitInWardrobeAndSkillButtonConsumesInput()
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
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation();
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                foreach (var id in new[] { InkSkinId.BondKnot, InkSkinId.WindFan })
                {
                    GameLocalization.SetLanguage(language); wardrobe.Select(id); yield return Wait(1);
                    Canvas.ForceUpdateCanvases();
                    foreach (var name in new[] { "ItemLore", "Description", "Achievement", "AchievementProgress" })
                    {
                        var child = wardrobe.Paper.Find(name);
                        Assert.That(child, Is.Not.Null, language + " / " + name);
                        var text = child.GetComponent<UnityEngine.UI.Text>();
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), language + " / " + name);
                    }
                    var stage = (RectTransform)wardrobe.Paper.Find("ShowroomStage");
                    var preview = (RectTransform)stage.Find("Preview");
                    var bounds = LobbySkinPreview.CostumeBounds(id, InkSkinCatalog.Sprite(id));
                    foreach (var point in new[] { bounds.min, bounds.max, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMin) })
                        Assert.That(stage.rect.Contains(stage.InverseTransformPoint(preview.TransformPoint(point * preview.rect.width))), Is.True);
                    yield return Capture(id + "-" + language);
                }
                Assert.That(InkSkinPreferences.TryEquip(InkSkinId.WindFan), Is.True);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                wardrobe.BackButton.onClick.Invoke(); yield return Navigation();
                Object.FindAnyObjectByType<LobbyView>().StartButton.onClick.Invoke();
                float until = Time.realtimeSinceStartup + 20;
                while ((GameManager.Instance.State != GameState.Playing || GameManager.Instance.IsTransitioning) && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(GameManager.Instance.IsGameplayTicking, Is.True); yield return null;
                var view = InkSkinSkillView.Instance;
                Assert.That(view, Is.Not.Null); Assert.That(view.Visible, Is.True); Assert.That(view.SkillButton.interactable, Is.True);
                var rect = (RectTransform)view.SkillButton.transform;
                var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Assert.That(InkSkinSkillView.IsPointerOverControls(center), Is.True);
                Assert.That(InkSkinSkillView.IsPointerOverControls(new Vector2(Screen.width * .5f, Screen.height * .5f)), Is.False);
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (var corner in corners) Assert.That(MobileUiLayout.CurrentSafeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner)), Is.True);
                yield return Capture("skill-ready");
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = center };
                ExecuteEvents.Execute(view.SkillButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That((bool)typeof(PointerInput).GetField("suppressedUntilRelease", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.True);
                ExecuteEvents.Execute(view.SkillButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(view.SkillButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(RunGrowthController.Instance.SkinSkillCooldownRemaining, Is.EqualTo(20));
                Assert.That(RunGrowthController.Instance.TryUseActiveSkinSkill(), Is.False);
                yield return Wait(.25f); yield return Capture("skill-cooldown");
                Assert.That(GameManager.Instance.PauseGame(), Is.True); yield return null;
                Assert.That(view.Visible, Is.False);
                float remaining = RunGrowthController.Instance.SkinSkillCooldownRemaining;
                yield return Wait(.25f); Assert.That(RunGrowthController.Instance.SkinSkillCooldownRemaining, Is.EqualTo(remaining));
                Assert.That(RunGrowthController.Instance.TryUseActiveSkinSkill(), Is.False);
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
        static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static IEnumerator Navigation()
        {
            float until = Time.realtimeSinceStartup + 20;
            while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False); yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/skin-skills");
            File.Copy(path, "output/qa/skin-skills/" + name + ".png", true);
        }
    }
}
