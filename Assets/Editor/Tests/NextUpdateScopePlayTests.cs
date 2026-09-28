using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class NextUpdateScopePlayTests
    {
        [UnityTest, Timeout(150000)]
        public IEnumerator LegacyWardrobeAndGoldRemainUsableInThreeLanguagesAndTwoSizes()
        {
            yield return new EnterPlayMode();
            Assert.That(NextUpdateScope.DeferredWardrobeEnabled, Is.False);
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
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
                InkPremiumAppearance.UseForTests(true, new InkPremiumUiTests.Store { Owned = true });
                var view = Object.FindFirstObjectByType<InkWardrobeView>();
                foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844) })
                {
                    var kind = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var temporary = System.Activator.CreateInstance(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                        System.Enum.Parse(kind, "FixedResolution"), size.x, size.y, "MukJump Next Scope TEMP");
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { temporary });
                    window.GetType().GetMethod("SizeSelectionCallback", flags).Invoke(window, new object[] { firstTemporary + added++, null });
                    window.Focus(); window.Repaint(); yield return Wait(.5f);
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        GameLocalization.SetLanguage(language);
                        Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation(); yield return Wait(.3f);
                        Assert.That(view.IsOpen, Is.True);
                        // 같은 호스트의 닫힌 다른 팝업이 아니라 실제 꾸미기 Canvas와 조상 투명도를 확인한다.
                        Assert.That(view.Paper.GetComponentInParent<Canvas>().name, Is.EqualTo("InkWardrobeCanvas"));
                        foreach (var groupAlpha in view.Paper.GetComponentsInParent<CanvasGroup>())
                            Assert.That(groupAlpha.alpha, Is.GreaterThan(.99f), groupAlpha.name);
                        Assert.That(view.IsCategoryHome, Is.False);
                        Assert.That(view.Paper.Find("CosmeticCategories"), Is.Null);
                        Assert.That(view.Paper.Find("EffectChoices"), Is.Null);
                        Assert.That(view.Paper.Find("ShowroomStage").gameObject.activeInHierarchy, Is.True);
                        Assert.That(view.CollectionScroll.gameObject.activeInHierarchy, Is.True);
                        int saves = settings.SaveCount;
                        view.SelectSlot(InkCosmeticSlot.Clone); view.SelectNest();
                        Assert.That(view.SelectedNest, Is.False); Assert.That(settings.SaveCount, Is.EqualTo(saves));
                        view.SelectGold(); yield return Wait(.4f);
                        if (view.EquipButton.IsInteractable()) yield return ClickVisible(view.EquipButton);
                        Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
                        yield return Wait(.3f);
                        yield return Capture("legacy-gold-" + size.x + "-" + language);
                        yield return Wait(.2f);
                        yield return ClickVisible(view.Paper.Find("CollectionToggle").GetComponent<Button>()); yield return Wait(.15f);
                        Assert.That(view.CollectionExpanded, Is.True);
                        yield return ClickVisible(view.Paper.Find("CollectionToggle").GetComponent<Button>()); yield return Wait(.15f);
                        Assert.That(view.CollectionExpanded, Is.False);
                        yield return ClickVisible(view.BackButton); yield return Navigation();
                        Assert.That(view.IsOpen, Is.False, "보류한 갈래 화면을 거치지 않고 로비로 돌아간다");
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
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator ClickVisible(Button button)
        {
            // EnterPlayMode를 포함한 EditMode IEnumerator의 실행 시점에는 Screen이 SceneView 크기일 수 있다.
            // 실제 게임 Update에서 Raycast·클릭을 실행해 게임 화면 좌표로 검사한다.
            var host = new GameObject("NextUpdateTestClick");
            var runner = host.AddComponent<NextUpdateTestClick>();
            bool completed = false; System.Exception failure = null;
            runner.Action = () =>
            {
                try { RaycastAndClick(button); }
                catch (System.Exception error) { failure = error; }
                finally { completed = true; }
            };
            float until = Time.realtimeSinceStartup + 5;
            while (!completed && Time.realtimeSinceStartup < until) yield return null;
            Object.Destroy(host);
            Assert.That(completed, Is.True, "게임 프레임에서 클릭을 실행하지 못했습니다.");
            if (failure != null) throw failure;
        }
        static void RaycastAndClick(Button button)
        {
            Canvas.ForceUpdateCanvases(); Assert.That(button.IsInteractable(), Is.True, button.name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), button.name + " point=" + pointer.position +
                " screen=" + Screen.width + "x" + Screen.height + " canvas=" + button.GetComponentInParent<Canvas>().pixelRect);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        static IEnumerator Navigation()
        {
            float until = Time.realtimeSinceStartup + 4;
            while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
        }
        static IEnumerator Capture(string name)
        {
            var result = JObject.FromObject(MukJumpAgentAudit.CaptureUi());
            string source = result.Value<string>("path");
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(source) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(source), Is.True, result.ToString());
            string folder = Path.GetFullPath("output/qa/next-update-scope-20260928/ui"); Directory.CreateDirectory(folder);
            File.Copy(source, Path.Combine(folder, name + ".png"), true);
        }
    }

    public sealed class NextUpdateTestClick : MonoBehaviour
    {
        public System.Action Action;
        void Update()
        {
            var pending = Action; Action = null; pending?.Invoke();
        }
    }
}
