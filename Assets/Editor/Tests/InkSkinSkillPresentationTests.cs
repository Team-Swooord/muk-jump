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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkSkinSkillPresentationTests
    {
        [UnityTest, Category("WardrobeSkillPopupVisual"), Timeout(120000)]
        public IEnumerator WardrobeAOpensCAndReturnsToTheSameCostume()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            if (MukJumpAccountRuntime.Instance != null) MukJumpAccountRuntime.Instance.enabled = false;
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
                if (MukJumpAccountRuntime.Instance != null) MukJumpAccountRuntime.Instance.enabled = false;
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return Navigation();
                var view = Object.FindAnyObjectByType<InkWardrobeView>();
                var collection = (RectTransform)view.CollectionScroll.transform;
                var listSize = collection.sizeDelta; var listPosition = collection.anchoredPosition;
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); view.Select(InkSkinId.CatEars); yield return Wait(.4f);
                    Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.False);
                    if (language == GameLanguage.Korean) yield return Capture("wardrobe-hanji-cosmetic");
                    view.Select(InkSkinId.Gat); yield return Wait(.4f);
                    Assert.That(view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text,
                        Is.EqualTo(language == GameLanguage.Korean ? "바람처럼 스쳐가는...." :
                            language == GameLanguage.English ? "Passing by like the wind...." : "風のように通り過ぎる...."));
                    Assert.That(view.Paper.Find("SkinName").GetComponent<RectTransform>().anchoredPosition.x, Is.Zero);
                    Assert.That(view.SkillSummaryText.rectTransform.anchoredPosition.y,
                        Is.GreaterThan(view.Paper.Find("SkinName").GetComponent<RectTransform>().anchoredPosition.y));
                    yield return Capture("wardrobe-centered-gat-" + language);
                    foreach (var id in new[] { InkSkinId.SpiderMask, InkSkinId.YakshaMask, InkSkinId.TurtleShell,
                        InkSkinId.SpiritHalo, InkSkinId.PhoenixCrown, InkSkinId.CloudBoots, InkSkinId.BondKnot, InkSkinId.AngelWings })
                    {
                        view.Select(id); yield return Wait(.1f);
                        Assert.That(view.Paper.Find("SkinName").GetComponent<UnityEngine.UI.Text>().text,
                            Is.EqualTo(GameLocalization.Translate(InkSkinCatalog.Name(id))));
                        Assert.That(view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text,
                            Is.EqualTo(GameLocalization.Translate(InkSkinLore.Get(id).ko)));
                        Assert.That(view.SkillSummaryText.text, Does.Contain(GameLocalization.Translate(InkSkinCatalog.Ability(id))));
                        Assert.That(collection.sizeDelta, Is.EqualTo(listSize));
                        if ((language == GameLanguage.Korean && id == InkSkinId.SpiritHalo) ||
                            (language == GameLanguage.English && id == InkSkinId.BondKnot) ||
                            (language == GameLanguage.Japanese && id == InkSkinId.AngelWings))
                            yield return Capture("wardrobe-renamed-" + language);
                    }
                    view.Select(InkSkinId.WindFan); yield return Wait(.4f);
                    Assert.That(collection.sizeDelta, Is.EqualTo(listSize)); Assert.That(collection.anchoredPosition, Is.EqualTo(listPosition));
                    Assert.That(view.SkillButton.gameObject.activeSelf, Is.True);
                    Assert.That(view.IsSkillDetailsOpen, Is.False);
                    Assert.That(view.CanBrowse, Is.True);
                    yield return Capture("wardrobe-A-" + language);
                    var contentPosition = view.CollectionContent.anchoredPosition;
                    yield return PlayerInputFrame(() => Click(view.SkillButton.GetComponent<RectTransform>()));
                    Assert.That(view.IsSkillDetailsOpen, Is.True);
                    Assert.That(view.CanBrowse, Is.False);
                    Assert.That(view.SkillDescriptionText.text, Is.EqualTo(GameLocalization.Translate(InkSkinCatalog.Description(InkSkinId.WindFan))));
                    yield return PlayerInputFrame(() =>
                    {
                        var corners = new Vector3[4]; view.SkillDetailsRoot.GetWorldCorners(corners);
                        foreach (var corner in corners)
                            Assert.That(MobileUiLayout.CurrentSafeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner)), Is.True);
                    });
                    yield return Capture("wardrobe-C-" + language);
                    // 긴 운영 문구는 상세 팝업 안에서 읽으며 뒤쪽 카드 위치를 바꾸지 않는다.
                    yield return PlayerInputFrame(() =>
                    {
                        InkLocalizedText.SetSource(view.SkillDescriptionText, string.Join("\n", System.Linq.Enumerable.Repeat("긴 설명을 확인한다.", 18)));
                        typeof(InkWardrobeView).GetMethod("LayoutInlineSkill", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
                        var scroll = view.SkillDescriptionText.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                        scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                        Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0));
                        // 팝업 뒤 다음 버튼에는 터치가 도달하지 않는다.
                        var next = (RectTransform)view.Paper.Find("ShowroomStage/NextCostume");
                        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                            position = RectTransformUtility.WorldToScreenPoint(null, next.TransformPoint(next.rect.center)) };
                        var hits = new System.Collections.Generic.List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointer, hits);
                        Assert.That(hits, Is.Not.Empty);
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.Not.EqualTo(next.gameObject));
                        view.StepSelection(1);
                        Assert.That(view.Selected, Is.EqualTo(InkSkinId.WindFan));
                        Click((RectTransform)view.SkillDetailsRoot.Find("ConfirmButton"));
                    });
                    Assert.That(view.IsSkillDetailsOpen, Is.False);
                    Assert.That(view.Selected, Is.EqualTo(InkSkinId.WindFan));
                    Assert.That(view.CollectionContent.anchoredPosition, Is.EqualTo(contentPosition));
                    yield return PlayerInputFrame(() => Click((RectTransform)view.Paper.Find("ShowroomStage/NextCostume")));
                    Assert.That(view.Selected, Is.Not.EqualTo(InkSkinId.WindFan));
                    Assert.That(collection.sizeDelta, Is.EqualTo(listSize)); Assert.That(collection.anchoredPosition, Is.EqualTo(listPosition));
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                view.ToggleCollection(); yield return Wait(.3f);
                Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.False);
                yield return Capture("wardrobe-hanji-expanded");
                view.ToggleCollection(); Assert.That(collection.sizeDelta, Is.EqualTo(listSize));
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

        static void Click(RectTransform target)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(target.gameObject), target.name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        static IEnumerator PlayerInputFrame(System.Action check)
        {
            // Test Runner 창의 Screen 크기 대신 실제 Game View 입력 프레임에서 검증한다.
            bool sampled = false; System.Exception failure = null;
            System.Action after = () =>
            {
                if (sampled || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                sampled = true;
                try { check(); } catch (System.Exception error) { failure = error; }
            };
            InputSystem.onAfterUpdate += after;
            try
            {
                float until = Time.realtimeSinceStartup + 5;
                while (!sampled && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(sampled, Is.True); Assert.That(failure, Is.Null, failure?.ToString());
            }
            finally { InputSystem.onAfterUpdate -= after; }
        }

        [UnityTest, Category("SecondNightMoon"), Timeout(120000)]
        public IEnumerator OnlySecondNightShowsBatMoonAndThirdNightAwardsAfterArrival()
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
            SkinDebugSession.Reset(); LobbyNightState.Reset();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                float until = Time.realtimeSinceStartup + 25;
                while ((GameManager.Instance == null || GameManager.Instance.State != GameState.Lobby ||
                    GameManager.Instance.IsTransitioning || StartupBrandSplash.IsBlockingInput) && Time.realtimeSinceStartup < until) yield return null;
                yield return Wait(.5f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                GameLocalization.SetLanguage(GameLanguage.Korean);
                var sky = Object.FindAnyObjectByType<LobbyNightSkyView>();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var toast = GameManager.Instance.GetComponent<InkAchievementToast>();
                var moon = sky.transform.Find("LobbyMoon").GetComponent<SpriteRenderer>();
                Assert.That(lobby.IsInteractive, Is.True);
                for (int phase = 1; phase <= 7; phase++)
                {
                    // 이전 밤/낮에서 이미 도착한 해금 알림과 다음 전환의 신규 알림을 구분한다.
                    var previousNotice = toast.transform.Find("AchievementToastCanvas")?.GetComponent<CanvasGroup>();
                    until = Time.realtimeSinceStartup + 6;
                    while (previousNotice != null && previousNotice.alpha > 0 && Time.realtimeSinceStartup < until) yield return null;
                    sky.SetLogoForTests(lobby.LogoRect);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(null, lobby.LogoRect.TransformPoint(lobby.LogoRect.rect.center));
                    for (int tap = 0; tap < 10; tap++)
                    {
                        sky.PointerForTests(point, true, true, false);
                        sky.PointerForTests(point, false, false, true);
                    }
                    Assert.That(sky.IsTransitioning, Is.True, "phase " + phase);
                    var group = toast.transform.Find("AchievementToastCanvas")?.GetComponent<CanvasGroup>();
                    Assert.That(group == null || group.alpha == 0, Is.True, "달이 도착하기 전 알림");
                    for (int frame = 0; frame < 360; frame++) sky.TickForTests(1f / 60);
                    yield return Wait(.3f);
                    Assert.That(sky.IsTransitioning, Is.False);
                    if (phase % 2 == 0) continue;
                    Assert.That(moon.enabled, Is.True);
                    Assert.That(moon.sprite.name, Is.EqualTo(phase == 3 ? "MukJump_BatMoon" : "MukJump_HanjiMoon"));
                    Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat), Is.EqualTo(phase >= 5));
                    if (phase == 5)
                    {
                        group = toast.transform.Find("AchievementToastCanvas")?.GetComponent<CanvasGroup>();
                        Assert.That(group, Is.Not.Null); Assert.That(group.alpha, Is.GreaterThan(.95f));
                        Assert.That(group.blocksRaycasts || group.interactable, Is.False);
                        Assert.That(System.Array.Exists(toast.GetComponentsInChildren<UnityEngine.UI.Text>(), t => t.text == "밤의 파수꾼"), Is.True);
                        Canvas.ForceUpdateCanvases();
                        var brush = (RectTransform)group.transform.Find("SafeArea/InkBanner/Brush");
                        var corners = new Vector3[4]; brush.GetWorldCorners(corners);
                        var recordCorners = new Vector3[4]; lobby.BestRecordRect.GetWorldCorners(recordCorners);
                        Assert.That(corners[0].y, Is.GreaterThan(recordCorners[1].y), "최고 기록 위 여백");
                        Assert.That(corners[0].x, Is.GreaterThan(Camera.main.WorldToScreenPoint(moon.bounds.max).x), "달 오른쪽 여백");
                        Assert.That(corners[1].y, Is.LessThanOrEqualTo(MobileUiLayout.CurrentSafeArea.yMax -
                            Screen.height * LobbyAdLayout.MeasuredTopInsetFraction), "배너 아래 여백");
                    }
                    yield return CaptureNight("night-" + (phase + 1) / 2);
                }
                Assert.That(SkinSecrets.AcceptedNightChange(true), Is.Zero);
                yield return Wait(3.2f);
                Assert.That(toast.transform.Find("AchievementToastCanvas").GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string saved), Is.True);
                Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(saved), Is.True);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat), Is.True);
                yield return null;
                Assert.That(SkinSecrets.HasBatMoon, Is.False, "다른 프로필의 밤 횟수를 이어받지 않는다");
            }
            finally
            {
                SkinDebugSession.Reset(); LobbyNightState.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator CaptureNight(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/night-secret");
            File.Copy(path, "output/qa/night-secret/" + name + ".png", true);
        }
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
                foreach (var id in new[] { InkSkinId.BondKnot, InkSkinId.WindFan, InkSkinId.DragonTail })
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
