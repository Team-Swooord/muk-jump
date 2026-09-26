using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class InkCosmeticPlayTests
    {
        [UnityTest, Timeout(150000)]
        public IEnumerator CategoryHomeOpensChoicesAndReturnsWithoutEquippingInThreeLanguages()
        {
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            InkPremiumAppearance.UseForTests(false, null); SkinDebugSession.Reset();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var window = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            int oldSize = (int)window.GetType().GetProperty("selectedSizeIndex", flags).GetValue(window);
            var sizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizeType).GetProperty("instance").GetValue(null);
            var group = sizeType.GetProperty("currentGroup", flags).GetValue(sizes);
            int firstTemporary = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), added = 0;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var view = Object.FindFirstObjectByType<InkWardrobeView>();
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844) })
                {
                    var kind = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var temporary = System.Activator.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                        System.Enum.Parse(kind, "FixedResolution"), size.x, size.y, "MukJump Wardrobe Hub TEMP");
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { temporary });
                    window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { firstTemporary + added++, null });
                    window.Focus(); window.Repaint(); yield return Wait(.5f);
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        GameLocalization.SetLanguage(language);
                        Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation(); yield return Wait(.4f);
                        Assert.That(view.IsCategoryHome, Is.True);
                        int saves = settings.SaveCount;
                        foreach (var text in view.Paper.Find("CosmeticCategories").GetComponentsInChildren<Text>())
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name + " " + language);
                        yield return Capture("hub-" + size.x + "-" + language, "output/qa/wardrobe-hub-20260926/screens");
                        foreach (var slot in new[] { InkCosmeticSlot.Landing, InkCosmeticSlot.Skin, InkCosmeticSlot.Clone })
                        {
                            var button = view.Paper.Find("CosmeticCategories/Category" + slot).GetComponent<Button>();
                            ClickVisible(button);
                            yield return Wait(.15f);
                            Assert.That(view.IsCategoryHome, Is.False); Assert.That(view.SelectedSlot, Is.EqualTo(slot));
                            if (slot == InkCosmeticSlot.Landing)
                            {
                                ClickVisible(view.Paper.Find("EffectChoices/Choice1").GetComponent<Button>());
                                Assert.That(view.SelectedEffect, Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
                                yield return Wait(.3f);
                                yield return Capture("choices-" + size.x + "-" + language, "output/qa/wardrobe-hub-20260926/screens");
                            }
                            // 새 화면의 Canvas가 그려진 뒤 다음 버튼을 누른다.
                            ClickVisible(view.BackButton); yield return Wait(.15f);
                            Assert.That(view.IsCategoryHome, Is.True); Assert.That(view.IsOpen, Is.True);
                        }
                        Assert.That(settings.SaveCount, Is.EqualTo(saves), "목록 확인과 뒤로 가기만으로 저장하지 않는다");
                        ClickVisible(view.BackButton); yield return Navigation();
                        Assert.That(view.IsOpen, Is.False);
                    }
                }
            }
            finally
            {
                window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { oldSize, null });
                for (int i = added - 1; i >= 0; i--) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { firstTemporary + i });
                SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static void ClickVisible(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(button.IsInteractable(), Is.True, button.name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name);
            var clicked = hits[0].gameObject.GetComponentInParent<Button>();
            Assert.That(clicked, Is.SameAs(button), button.name + " 위를 다른 화면이 덮지 않는다: " + hits[0].gameObject.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        [UnityTest, Timeout(150000)]
        public IEnumerator IndependentCosmeticsRenderInThreeLanguagesAndRealPlay()
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
            InkPremiumAppearance.UseForTests(false, null); SkinDebugSession.Reset();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation();
                var view = Object.FindFirstObjectByType<InkWardrobeView>();
                view.SelectSlot(InkCosmeticSlot.Clone); view.SelectEffect(InkCosmeticCatalog.Tadpole);
                yield return Wait(.5f); yield return Capture("locked-tadpole");
                var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
                stats.drawnLandings = 200; stats.items = 1000; stats.maxLivingPlayers = 12;
                Assert.That(PermanentGrowthProfile.SettleRun("cosmetic-play-fixture", 500, 500, 0, 60, true, stats).Accepted, Is.True);
                view.SelectNest(); view.EquipButton.onClick.Invoke();
                view.SelectSlot(InkCosmeticSlot.Clone); view.SelectEffect(InkCosmeticCatalog.Tadpole); view.EquipButton.onClick.Invoke();
                view.SelectSlot(InkCosmeticSlot.Landing); view.SelectEffect(InkCosmeticCatalog.BambooLeaf); view.EquipButton.onClick.Invoke();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    view.SelectSlot(InkCosmeticSlot.Skin); yield return Wait(.5f); yield return Capture("nest-" + language);
                    view.SelectSlot(InkCosmeticSlot.Landing); yield return Wait(.5f); yield return Capture("landing-" + language);
                    view.SelectSlot(InkCosmeticSlot.Clone); yield return Wait(.5f); yield return Capture("clone-" + language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                view.BackButton.onClick.Invoke(); // 선택 목록 → 꾸미기 첫 화면
                view.BackButton.onClick.Invoke(); yield return Navigation(); yield return Wait(.3f); yield return Capture("lobby-nest");
                Object.FindFirstObjectByType<LobbyView>().StartButton.onClick.Invoke();
                float until = Time.realtimeSinceStartup + 20;
                while ((GameManager.Instance.State == GameState.Lobby || GameManager.Instance.IsTransitioning) && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                Assert.That(GameManager.Instance.PauseGame(), Is.True);
                var original = Object.FindFirstObjectByType<PlayerController>();
                var clone = Object.Instantiate(original.gameObject); clone.GetComponent<PlayerController>().ConfigureAsClone(3f);
                clone.transform.position = original.transform.position + Vector3.right * 1.2f;
                Assert.That(clone.GetComponent<PlayerController>().IsRuntimeClone, Is.True);
                Assert.That(RunGrowthController.Instance.CloneSnapshot, Is.EqualTo(InkCosmeticCatalog.Tadpole));
                // 복제 직후에는 원본 장식이 남는다. 실제 LateUpdate 표시 갱신을 기다린다.
                var cloneView = clone.GetComponent<InkSkinView>();
                var tail = InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole);
                float appearanceDeadline = Time.realtimeSinceStartup + 2f;
                while (cloneView.AccessoryRenderer.sprite != tail && Time.realtimeSinceStartup < appearanceDeadline)
                    yield return null;
                Assert.That(original.GetComponent<InkSkinView>().AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
                Assert.That(clone.GetComponent<InkSkinView>().AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole)));
                Assert.That(RunGrowthController.Instance.LandingSnapshot, Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
                yield return Capture("world-nest-and-tadpole");
                Object.Destroy(clone);
            }
            finally
            {
                SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
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
        static IEnumerator Capture(string name, string output = "output/qa/wardrobe-next-20260926/screens")
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory(output);
            File.Copy(path, output + "/" + name + ".png", true);
        }
    }
}
