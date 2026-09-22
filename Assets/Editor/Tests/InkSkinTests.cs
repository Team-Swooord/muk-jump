using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkSkinTests
    {
        readonly List<Object> cleanup = new();
        MemoryLobbySettingsStore settings;
        MemoryPermanentGrowthStore growthStore;
        MemoryScoreStore scoreStore;
        ScoreManager score;

        [SetUp]
        public void Setup()
        {
            settings = new MemoryLobbySettingsStore();
            growthStore = new MemoryPermanentGrowthStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(growthStore);
            scoreStore = new MemoryScoreStore { Best = InkSkinAchievements.Goal(InkSkinId.Gat) };
            ScoreManager.UseStoreForTests(scoreStore);
            score = Track(new GameObject("SkinScore")).AddComponent<ScoreManager>();
            Invoke(score, "OnEnable"); Invoke(score, "Awake");
            for (int i = 0; i < InkSkinAchievements.Goal(InkSkinId.SpiderMask); i++)
                PermanentGrowthProfile.SettleRun("skin-test-" + i, 1, 0, true);
        }
        [TearDown]
        public void Teardown()
        {
            settings.ThrowOnSave = false;
            for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test]
        public void MissingInvalidAndFutureSelectionsFallBackWithoutOverwritingSave()
        {
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            settings.SetInt(InkSkinPreferences.StorageKey, 987);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip((InkSkinId)(-1)), Is.False);
            Assert.That(settings.GetInt(InkSkinPreferences.StorageKey, 0), Is.EqualTo(987));
        }
        [Test]
        public void EquipPersistsLocallyWithoutChangingGrowthAndFailedSaveRollsBack()
        {
            string before = growthStore.Json;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.True);
            InkSkinPreferences.UseStoreForTests(settings);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            settings.ThrowOnSave = true;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.False);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            Assert.That(growthStore.Json, Is.EqualTo(before));
        }
        [Test]
        public void RunLocksSkinAcrossReviveAndCannotEquipDuringPlay()
        {
            InkSkinPreferences.TryEquip(InkSkinId.Gat);
            var manager = Manager(out var growth);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Gat));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.False);
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.SpiderMask);
            Invoke(manager, "SetState", GameState.GameOver);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Gat));
            Invoke(manager, "SetState", GameState.Lobby);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.SpiderMask));
        }
        [TestCase(InkSkinId.Ink, false)]
        [TestCase(InkSkinId.Gat, true)]
        [TestCase(InkSkinId.SpiderMask, false)]
        public void OnlyGatAddsOneAirJumpAndKeepsPrimaryJump(InkSkinId id, bool expected)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var jump = player.gameObject.AddComponent<AutoJump>();
            Invoke(jump, "Awake");
            Field(jump, "baseJumpSpeed", 10f);
            Field(jump, "jumpStrengthMultiplier", 1f);
            Field(jump, "normalInfluence", 0f);
            Invoke(jump, "Jump");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(10).Within(.001));
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.EqualTo(expected));
            if (expected) Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
            Invoke(jump, "Jump");
            jump.CancelForSpecialLaunch();
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
            Invoke(jump, "Jump");
            jump.NotifyLanding(false);
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
        }
        [TestCase(InkSkinId.Ink, false)]
        [TestCase(InkSkinId.Gat, false)]
        [TestCase(InkSkinId.SpiderMask, false)]
        public void SpiderClingsAndReleasesThroughAutomaticJumpOnly(InkSkinId id, bool expected)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var wall = Track(new GameObject("SkinWall")).AddComponent<ScreenSideWall>();
            Assert.That(Invoke(player, "TryBeginWallCling", wall, 1f), Is.EqualTo(expected));
            Assert.That(player.IsWallClinging, Is.EqualTo(expected));
            if (!expected) return;
            Assert.That(player.Body.gravityScale, Is.Zero);
            Assert.That(player.IsGrounded, Is.True);
            var jump = player.gameObject.AddComponent<AutoJump>();
            Invoke(jump, "Awake");
            Invoke(jump, "Jump");
            Assert.That(player.IsWallClinging, Is.False);
            Assert.That(player.Body.gravityScale, Is.GreaterThan(0));
            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(0));
            Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(0));
        }
        [Test]
        public void GatWaitsForApexAndPauseDoesNotConsumeTheAirJump()
        {
            InkSkinPreferences.TryEquip(InkSkinId.Gat);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var jump = player.gameObject.AddComponent<AutoJump>(); Invoke(jump, "Awake");
            Field(jump, "baseJumpSpeed", 10f); Field(jump, "jumpStrengthMultiplier", 1f); Field(jump, "normalInfluence", 0f);
            Invoke(jump, "Jump"); Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(10).Within(.001));
            player.Body.linearVelocity = new Vector2(0, 1.5f);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(1.5f));
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
        }
        [Test]
        public void WardrobeOnlyOpensAtCoverAndPreviewDoesNotEquip()
        {
            var manager = Manager(out _);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle());
            var navigator = manager.gameObject.AddComponent<LobbyScreenNavigator>();
            navigator.BuildForTests();
            Assert.That(navigator.OpenWardrobe(), Is.True);
            Assert.That(wardrobe.IsOpen, Is.False);
            Assert.That(navigator.CanStartGame, Is.False);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            wardrobe.Select(InkSkinId.Gat);
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.Gat));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            wardrobe.BackButton.onClick.Invoke();
            navigator.CompleteCoverForTests(); navigator.CompleteRevealForTests();
            navigator.OpenWardrobe(); navigator.CompleteCoverForTests(); navigator.CompleteRevealForTests();
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.Ink));
            wardrobe.Select(InkSkinId.SpiderMask);
            wardrobe.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.SpiderMask));
            Assert.That(wardrobe.EquipButton.interactable, Is.False);
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void WardrobeLabelsAreTranslatedAndFit(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            var manager = Manager(out _);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle());
            wardrobe.SetNavigationPresentation(true, true);
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                if (!InkSkinCatalog.IsAvailable((InkSkinId)i)) continue;
                wardrobe.Select((InkSkinId)i);
                Canvas.ForceUpdateCanvases();
                foreach (var text in wardrobe.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    Assert.That(text.font, Is.Not.Null, text.name);
                    if (text.text.Length == 0) continue;
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name + ": " + text.text);
                    var binding = text.GetComponent<InkLocalizedText>();
                    Assert.That(binding, Is.Not.Null);
                    if (language != GameLanguage.Korean && binding.SourceText.Any(c => c >= '\uAC00' && c <= '\uD7A3'))
                        Assert.That(text.text, Is.Not.EqualTo(binding.SourceText), text.name);
                }
            }
        }
        [TestCase(InkSkinId.CloudDragon)]
        [TestCase(InkSkinId.InkMecha)]
        [TestCase(InkSkinId.ClockworkCat)]
        public void CosmeticCostumesUseOrdinaryAccessoryCopy(InkSkinId id)
        {
            Assert.That(InkSkinCatalog.Ability(id), Is.EqualTo("먹의 장식"));
            Assert.That(InkSkinCatalog.Description(id), Is.EqualTo(InkSkinCatalog.Description(InkSkinId.CatEars)));
            Assert.That(InkSkinCatalog.Ability(InkSkinId.Gat), Is.EqualTo("이단 도약"));
            Assert.That(InkSkinCatalog.Ability(InkSkinId.SpiderMask), Is.EqualTo("먹슈트"));
        }
        [Test, Category("SkinRetirement")]
        public void WardrobePolishOrdersByGoalWithoutChangingSavedIds()
        {
            var order=Enumerable.Range(0,InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt).ToArray();
            Assert.That(order.Distinct().Count(),Is.EqualTo(InkSkinCatalog.BrowseCount));
            Assert.That(order, Has.No.Member(InkSkinId.StormScarf));
            var view=Track(new GameObject("SortedWardrobe")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true,true);
            for(int i=0;i<order.Length;i++)
            {
                Assert.That(InkSkinCatalog.Get((InkSkinId)i).Id,Is.EqualTo((InkSkinId)i));
                Assert.That(InkSkinCatalog.BrowseIndex(order[i]),Is.EqualTo(i));
                Assert.That(view.CollectionContent.GetChild(i).name,Is.EqualTo("SkinCard"+(int)order[i]));
                Assert.That(view.Selected,Is.EqualTo(order[i]));
                if(i>0)
                {
                    Assert.That(InkSkinCatalog.BrowseGroup(order[i]),Is.GreaterThanOrEqualTo(InkSkinCatalog.BrowseGroup(order[i-1])));
                    if(InkSkinCatalog.BrowseGroup(order[i])==InkSkinCatalog.BrowseGroup(order[i-1]))
                        Assert.That(InkSkinCatalog.Get(order[i]).Goal,Is.GreaterThanOrEqualTo(InkSkinCatalog.Get(order[i-1]).Goal));
                }
                view.StepSelection(1);
            }
            Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
            view.Select(InkSkinId.RedShoes); view.StepSelection(-1);
            Assert.That(view.Selected,Is.EqualTo(InkSkinId.RedHeadband));
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        [Category("WardrobeInlineDetails"), Category("WardrobeUnlockPresentation")]
        public void WardrobePolishHasReadableLocalizedLoreAndLargerProgress(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            var view=Track(new GameObject("ReadableWardrobe")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true,true);
            Assert.That(SkinDebugSession.SetAllUnlocked(true),Is.True);
            try
            {
                for(int i=0;i<InkSkinCatalog.Count;i++)
                {
                    var id=(InkSkinId)i;
                    if (!InkSkinCatalog.IsAvailable(id)) continue;
                    var lore=InkSkinLore.Get(id);
                    Assert.That(lore.ko,Is.Not.Empty); Assert.That(lore.en,Is.Not.Empty); Assert.That(lore.ja,Is.Not.Empty);
                    view.Select(id); Canvas.ForceUpdateCanvases();
                    var stage=(RectTransform)view.Paper.Find("ShowroomStage");
                    var preview=(RectTransform)stage.Find("Preview");
                    Rect bounds=LobbySkinPreview.CostumeBounds(id,InkSkinCatalog.Sprite(id));
                    foreach(var corner in new[]{bounds.min,bounds.max,new Vector2(bounds.xMin,bounds.yMax),new Vector2(bounds.xMax,bounds.yMin)})
                        Assert.That(stage.rect.Contains(stage.InverseTransformPoint(preview.TransformPoint(corner*preview.rect.width))),Is.True,id+" 무대 잘림");
                    Assert.That(view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text,
                        Is.EqualTo(language==GameLanguage.Korean ? lore.ko : language==GameLanguage.Japanese ? lore.ja : lore.en));
                    foreach(var text in view.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                        if(!string.IsNullOrEmpty(text.text))
                            Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2),id+" / "+text.name+" / "+text.text);
                }
                // 일본어는 기존 Kaisei Decol의 넓은 행 메트릭에 맞춘 .76배 보정을 유지한다.
                float fontScale=language==GameLanguage.Japanese ? .76f : 1f;
                Assert.That(view.Paper.Find("Description").GetComponent<UnityEngine.UI.Text>().fontSize,Is.EqualTo(Mathf.RoundToInt(60*fontScale)));
                Assert.That(view.Paper.Find("AchievementProgress").GetComponent<UnityEngine.UI.Text>().fontSize,Is.EqualTo(Mathf.RoundToInt(52*fontScale)));
                Assert.That(((RectTransform)view.Paper.Find("AchievementTrack")).rect.height,Is.GreaterThanOrEqualTo(92));
                view.ToggleCollection();
                Assert.That(view.Paper.Find("ItemLore").gameObject.activeSelf,Is.False);
            }
            finally { SkinDebugSession.Reset(); }
            view.ToggleCollection(); view.Select(InkSkinId.Astronaut);
            Assert.That(view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text,Is.Empty);
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        [Category("WardrobeSkillPopup"), Category("WardrobeUnlockPresentation")]
        public void WardrobeSummaryOpensDetailsAndKeepsTheCollectionFixed(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            var view = Track(new GameObject("CompactCosmeticDetails")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true, true);
            var description = view.Paper.Find("Description").GetComponent<UnityEngine.UI.Text>();
            var achievement = view.Paper.Find("Achievement").GetComponent<UnityEngine.UI.Text>();
            var collection = (RectTransform)view.Paper.Find("CollectionScroll");
            Vector2 size = collection.sizeDelta, position = collection.anchoredPosition;
            Vector2 cell = view.CollectionContent.GetComponent<UnityEngine.UI.GridLayoutGroup>().cellSize;
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            try
            {
                for (int i = 0; i < InkSkinCatalog.BrowseCount; i++)
                {
                    var id = InkSkinCatalog.BrowseAt(i);
                    view.Select(id); view.Presentation.Cancel(); Canvas.ForceUpdateCanvases();
                    Assert.That(view.SkillButton.gameObject.activeSelf, Is.EqualTo(InkSkinCatalog.HasAbility(id)), id.ToString());
                    Assert.That(view.IsSkillDetailsOpen, Is.False);
                    Assert.That(description.text, Is.Empty); Assert.That(description.gameObject.activeSelf, Is.False);
                    Assert.That(view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text, Is.Not.Empty);
                    if (id != InkSkinId.Ink)
                        Assert.That(achievement.text, Is.EqualTo(GameLocalization.Translate(InkSkinAchievements.Requirement(id))));
                    Assert.That(collection.sizeDelta, Is.EqualTo(size), id + " 목록 크기");
                    Assert.That(collection.anchoredPosition, Is.EqualTo(position), id + " 목록 위치");
                    Assert.That(view.CollectionContent.GetComponent<UnityEngine.UI.GridLayoutGroup>().cellSize, Is.EqualTo(cell));
                    if (!InkSkinCatalog.HasAbility(id)) continue;
                    Assert.That(view.CanBrowse, Is.True);
                    var beforeOpen = view.CollectionContent.anchoredPosition;
                    view.SkillButton.onClick.Invoke();
                    Assert.That(view.IsSkillDetailsOpen, Is.True);
                    Assert.That(view.CanBrowse, Is.False);
                    Assert.That(view.SkillTitleText.text, Is.EqualTo(GameLocalization.Translate(InkSkinCatalog.Ability(id))));
                    Assert.That(view.SkillDescriptionText.text, Is.EqualTo(GameLocalization.Translate(InkSkinCatalog.Description(id))));
                    Assert.That(view.SkillDetailsRoot.Find("ConfirmButton"), Is.Null);
                    Assert.That(view.SkillDetailsRoot.Find("CostumeIcon").GetComponent<RectTransform>().anchoredPosition.x, Is.Zero);
                    Assert.That(view.SkillTitleText.rectTransform.anchoredPosition.x, Is.Zero);
                    Assert.That(view.SkillTitleText.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                    Assert.That(view.SkillDescriptionText.alignment, Is.EqualTo(TextAnchor.MiddleCenter).Or.EqualTo(TextAnchor.UpperCenter));
                    var mode = view.SkillDetailsRoot.Find("ModeBadge").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(mode.sprite, Is.EqualTo(InkUiTextureFactory.CreateBrushSprite()));
                    Assert.That(mode.raycastTarget, Is.False);
                    Assert.That(mode.GetComponent<UnityEngine.UI.Button>(), Is.Null);
                    var modeRect = (RectTransform)mode.transform;
                    var cooldownRect = (RectTransform)view.SkillDetailsRoot.Find("CooldownBadge");
                    bool timed = InkSkinChart.Cooldown(id) > 0;
                    Assert.That(cooldownRect.gameObject.activeSelf, Is.EqualTo(timed));
                    Assert.That(modeRect.anchoredPosition.x, Is.EqualTo(timed ? -cooldownRect.anchoredPosition.x : 0));
                    foreach (var badgeText in mode.GetComponentsInChildren<UnityEngine.UI.Text>())
                        Assert.That(badgeText.preferredHeight, Is.LessThanOrEqualTo(badgeText.rectTransform.rect.height + 2));
                    foreach (var text in new[] { view.SkillTitleText, view.SkillDescriptionText })
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), id + " / " + text.name);
                    view.Select(InkSkinId.Ink);
                    view.ToggleCollection();
                    Assert.That(view.Selected, Is.EqualTo(id), "상세 팝업 뒤의 선택은 잠근다");
                    Assert.That(view.CollectionExpanded, Is.False);
                    view.SkillDetailsRoot.Find("CloseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(view.IsSkillDetailsOpen, Is.False);
                    Assert.That(view.CollectionContent.anchoredPosition, Is.EqualTo(beforeOpen));
                    Assert.That(view.OpenSkillDetails(), Is.True);
                    view.SkillDetailsRoot.Find("CloseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(view.IsSkillDetailsOpen, Is.False);
                    view.Select(InkSkinId.Ink);
                    Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
                    Assert.That(view.SkillDescriptionText.text, Is.Empty);
                    Assert.That(collection.sizeDelta, Is.EqualTo(size));
                }
                view.ToggleCollection(); view.ToggleCollection();
                Assert.That(description.gameObject.activeSelf, Is.False, "목록 왕복 후 공통 안내가 다시 나오면 안 된다");
            }
            finally { SkinDebugSession.Reset(); }
            view.Select(InkSkinId.Astronaut);
            Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.False);
            Assert.That(description.gameObject.activeSelf, Is.True);
            Assert.That(description.text, Is.EqualTo(GameLocalization.Translate(InkSkinLore.Get(InkSkinId.Astronaut).ko)));
            Assert.That(collection.sizeDelta, Is.EqualTo(size));
            Assert.That(collection.anchoredPosition, Is.EqualTo(position));
        }

        [Test, Category("HiddenSkinHints"), Category("WardrobeUnlockPresentation")]
        public void HiddenSkinsShowOnlyHintsUntilDiscoveredInEveryLanguage()
        {
            InkSkinChart.Reset(); SkinDebugSession.Reset();
            var view = Track(new GameObject("HiddenHintDetails")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true, true);
            var description = view.Paper.Find("Description").GetComponent<UnityEngine.UI.Text>();
            var name = view.Paper.Find("SkinName").GetComponent<UnityEngine.UI.Text>();
            var lore = view.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>();
            var preview = view.Paper.Find("ShowroomStage/Preview");
            var body = preview.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
            var accessory = preview.Find("Accessory").GetComponent<UnityEngine.UI.Image>();
            var collection = (RectTransform)view.CollectionScroll.transform;
            Vector2 size = collection.sizeDelta, position = collection.anchoredPosition;
            var hidden = Enumerable.Range(0, InkSkinCatalog.Count).Select(i => (InkSkinId)i)
                .Where(id => InkSkinCatalog.BuiltIn(id).Tier is SkinTier.Hidden or SkinTier.UltraHidden).ToArray();
            var languageBefore = GameLocalization.Language;
            try
            {
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    foreach (var id in hidden)
                    {
                        view.Select(id); view.Presentation.Cancel(); Canvas.ForceUpdateCanvases();
                        Assert.That(InkSkinCatalog.IsConcealed(id), Is.True, id.ToString());
                        Assert.That(description.gameObject.activeSelf, Is.True);
                        Assert.That(description.text, Is.EqualTo(GameLocalization.Translate(InkSkinLore.Get(id).ko)));
                        Assert.That(description.preferredHeight, Is.LessThanOrEqualTo(description.rectTransform.rect.height + 2));
                        Assert.That(name.text, Is.Empty); Assert.That(lore.text, Is.Empty);
                        Assert.That(body.enabled || accessory.enabled || view.SkillButton.gameObject.activeSelf, Is.False);
                        Assert.That(collection.sizeDelta, Is.EqualTo(size));
                        Assert.That(collection.anchoredPosition, Is.EqualTo(position));
                    }
                }
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                foreach (var id in hidden)
                {
                    view.Select(id); view.Presentation.Cancel();
                    Assert.That(description.gameObject.activeSelf, Is.False);
                    Assert.That(name.text, Is.EqualTo(GameLocalization.Translate(InkSkinCatalog.Name(id))));
                    Assert.That(lore.text, Is.EqualTo(GameLocalization.Translate(InkSkinLore.Get(id).ko)));
                    Assert.That(body.enabled && accessory.enabled, Is.True);
                }
            }
            finally { SkinDebugSession.Reset(); GameLocalization.SetLanguage(languageBefore); }
        }

        [Test, Category("WardrobeSkillPopup"), Category("WardrobeUnlockPresentation")]
        public void WardrobeSkillPopupClearsHiddenAccountContentAndStopsBackgroundScroll()
        {
            var view = Track(new GameObject("InlineSkillLifecycle")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true, true);
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            view.Select(InkSkinId.PhoenixCrown);
            Assert.That(view.OpenSkillDetails(), Is.True);
            Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.True);
            SkinDebugSession.Reset();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Invoke(view, "RefreshSelection");
            Assert.That(view.IsSkillDetailsOpen, Is.False, "계정 교체로 잠긴 능력 팝업은 닫는다");
            Assert.That(view.SkillDetailsRoot.Find("CostumeIcon").GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(Color.black));
            view.CloseSkillDetails();
            view.Select(InkSkinId.Astronaut);
            Assert.That(view.SkillDetailsRoot.gameObject.activeSelf, Is.False);
            Assert.That(view.SkillTitleText.text, Is.Empty); Assert.That(view.SkillDescriptionText.text, Is.Empty);
            Assert.That(view.SkillDetailsRoot.Find("CostumeIcon").GetComponent<UnityEngine.UI.Image>().sprite, Is.Null);
            view.Select(InkSkinId.Gat);
            Assert.That(view.OpenSkillDetails(), Is.True);
            var scroll = view.SkillDescriptionText.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            try
            {
                scroll.velocity = new Vector2(0, 200);
                Invoke(view, "OnApplicationPause", true);
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(view.IsSkillDetailsOpen, Is.False);
                Assert.That(view.CanBrowse, Is.False);
            }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
            view.SetNavigationPresentation(false, false);
            Assert.That(view.IsOpen, Is.False);
        }

        [TestCase(InkSkinId.RedShoes)]
        [TestCase(InkSkinId.CloudBoots)]
        public void WardrobePolishFootwearUsesSameOutwardPartsInUiAndPlayer(InkSkinId id)
        {
            var source=InkSkinCatalog.Sprite(id);
            for(int side=0;side<2;side++)
            {
                bool right=side==1;
                var part=InkFootwearPose.Part(id,right);
                Assert.That(InkFootwearPose.SourceRight(id,right),Is.EqualTo(id==InkSkinId.RedShoes ? !right : right));
                Assert.That(part.rect.x,Is.EqualTo(source.rect.x+(InkFootwearPose.SourceRight(id,right)?source.rect.width*.5f:0)));
                Assert.That(part.pivot.x/part.rect.width,Is.EqualTo(InkFootwearPose.Pivot(id,right).x).Within(.0001f));
                Rect drawn=InkFootwearPose.DrawingRect(id,source,right);
                Vector2 anchor=drawn.position+Vector2.Scale(drawn.size,InkFootwearPose.Pivot(id,right));
                Assert.That(Vector2.Distance(anchor,InkFootwearPose.Anchor(null,right)),Is.LessThan(.0001f));
            }
        }
        [TestCase(InkSkinId.Gat)]
        [TestCase(InkSkinId.SpiderMask)]
        public void AccessoryFollowsBodyWithoutChangingPhysicsAndCloneKeepsItsOwnRenderer(InkSkinId id)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var body = player.GetComponent<SpriteRenderer>();
            body.sprite = Idle(); body.flipX = true;
            var view = player.gameObject.AddComponent<InkSkinView>();
            Invoke(view, "OnEnable"); Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.True);
            Assert.That(view.AccessoryRenderer.flipX, Is.True);
            Assert.That(body.sprite, Is.SameAs(Idle()));
            Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(player.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.4f));
            var clone = Track(Object.Instantiate(player.gameObject));
            var cloneView = clone.GetComponent<InkSkinView>();
            Invoke(cloneView, "OnEnable"); Invoke(cloneView, "LateUpdate");
            Assert.That(cloneView.AccessoryRenderer, Is.Not.SameAs(view.AccessoryRenderer));
            Assert.That(cloneView.AccessoryRenderer.transform.parent, Is.SameAs(clone.transform));
            Assert.That(clone.transform.Cast<Transform>().Count(t => t.name == "InkSkinAccessory"), Is.EqualTo(1));
            body.enabled = false; Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.False);
            body.enabled = true; Invoke(view, "LateUpdate");
            var health = player.gameObject.AddComponent<PlayerHealthBillboard>();
            Invoke(health, "OnEnable");
            Bounds combined = (Bounds)Invoke(health, "ResolveOwnerBounds");
            Assert.That(combined.max.y + .000001f, Is.GreaterThanOrEqualTo(view.AccessoryRenderer.bounds.max.y));
            player.Kill(); Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.False);
        }
        [Test]
        public void LobbyOrderingKeepsWardrobeImmediatelyAboveOptions()
        {
            Assert.That(LobbyMenuLayout.StartAnchor.y, Is.GreaterThan(LobbyMenuLayout.GrowthAnchor.y));
            Assert.That(LobbyMenuLayout.GrowthAnchor.y, Is.GreaterThan(LobbyMenuLayout.WardrobeAnchor.y));
            Assert.That(LobbyMenuLayout.WardrobeAnchor.y, Is.GreaterThan(LobbyMenuLayout.OptionsAnchor.y));
            Assert.That(LobbyMenuLayout.OptionsAnchor.x, Is.EqualTo(LobbyMenuLayout.WardrobeAnchor.x));
        }
        [TestCase(InkSkinId.RedHeadband)]
        [TestCase(InkSkinId.CatEars)]
        [TestCase(InkSkinId.ClockworkCat)]
        public void HeadbandsShareForeheadFitAcrossPreviewFramesAndFacing(InkSkinId id)
        {
            var manager=Manager(out _);
            var measured=SkinRunStats.Begin(true,InkSkinId.CatEars);
            measured.drawnLandings=40; measured.items=measured.inkDrops=10;
            PermanentGrowthProfile.SettleRun("headband",400,400,0,120,true,measured);
            PermanentGrowthProfile.SettleRun("headband-secret",400,400,0,120,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
            Assert.That(InkSkinPreferences.TryEquip(id),Is.True);
            var wardrobe=Track(new GameObject("HeadbandWardrobe")).AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle()); wardrobe.SetNavigationPresentation(true,true); wardrobe.Select(id);
            wardrobe.Presentation.Cancel();
            var preview=wardrobe.Paper.Find("ShowroomStage/Preview/Accessory").GetComponent<UnityEngine.UI.Image>();
            Vector2 fit=InkSkinCatalog.Offset(id);
            Assert.That(fit.y,Is.InRange(.1f,.29f),"이마와 귀 착용선을 유지합니다.");
            Assert.That(preview.rectTransform.anchoredPosition/500f,Is.EqualTo(fit));
            Assert.That(preview.rectTransform.sizeDelta.x/500f,Is.EqualTo(InkSkinCatalog.Width(id)).Within(.0001f));
            Invoke(manager,"SetState",GameState.Playing);
            var player=Player(); var body=player.GetComponent<SpriteRenderer>();
            var skin=player.gameObject.AddComponent<InkSkinView>(); Invoke(skin,"OnEnable");
            var frames=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().ToArray();
            Assert.That(frames.Length,Is.EqualTo(8));
            foreach(var frame in frames)
            foreach(bool flipped in new[]{false,true})
            {
                body.sprite=frame; body.flipX=flipped; Invoke(skin,"LateUpdate");
                Vector2 size=frame.rect.size/frame.pixelsPerUnit;
                Vector2 center=(frame.rect.size*.5f-frame.pivot)/frame.pixelsPerUnit;
                Vector2 expected=fit; if(flipped) expected.x=-expected.x;
                Vector2 normalized=((Vector2)skin.AccessoryRenderer.transform.localPosition-center)/size;
                Assert.That(Vector2.Distance(normalized,expected),Is.LessThan(.0001f),frame.name);
                Assert.That(skin.AccessoryRenderer.flipX,Is.EqualTo(flipped));
                Assert.That(skin.AccessoryRenderer.sprite,Is.SameAs(preview.sprite));
                Assert.That(body.sprite,Is.SameAs(frame));
                Assert.That(player.GetComponent<CircleCollider2D>().radius,Is.EqualTo(.4f));
            }
        }

        void ClearAchievements()
        {
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0), Is.True);
        }

        [Test]
        public void LockedPreviewAndRawEquipKeyCannotGrantAnAbility()
        {
            ClearAchievements();
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.Gat);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.False);
            var manager = Manager(out var growth);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle()); wardrobe.SetNavigationPresentation(true, true);
            wardrobe.Select(InkSkinId.SpiderMask);
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.SpiderMask));
            Assert.That(wardrobe.EquipButton.interactable, Is.False);
            wardrobe.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Ink));
        }

        [Test]
        public void GatUsesConfirmedBestNotLiveHeightOrFailedSave()
        {
            ClearAchievements();
            Assert.That(score.TryCommitBestCandidate(499), Is.True);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.Gat), Is.EqualTo(499));
            scoreStore.ThrowOnSave = true;
            Assert.That(score.TryCommitBestCandidate(500), Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.False);
            scoreStore.ThrowOnSave = false;
            Assert.That(score.TryCommitBestCandidate(500), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.True);
        }

        [Test]
        public void TwentyUniqueEligibleSettlementsUnlockWithoutDuplicateOrFailedSaves()
        {
            ClearAchievements();
            for (int i = 0; i < 19; i++) PermanentGrowthProfile.SettleRun("achievement-" + i, 1, 0, true);
            PermanentGrowthProfile.SettleRun("achievement-18", 1, 0, true);
            PermanentGrowthProfile.SettleRun("debug", 900, 0, false);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(19));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.False);
            growthStore.ThrowOnPrimarySave = true;
            Assert.That(PermanentGrowthProfile.SettleRun("achievement-19", 1, 0, true).Accepted, Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.False);
            growthStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(19));
            Assert.That(PermanentGrowthProfile.SettleRun("achievement-19", 1, 0, true).Accepted, Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
            for (int i = 20; i < 85; i++) PermanentGrowthProfile.SettleRun("achievement-" + i, 1, 0, true);
            Assert.That(PermanentGrowthProfile.CompletedRunHistoryCount, Is.EqualTo(64));
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(20));
            PermanentGrowthProfile.UseStoreForTests(growthStore);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
        }

        [Test]
        public void AccountReplacementDeletionAndGrowthResetRespectAchievementOwnership()
        {
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string earnedAccount), Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.True);
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
            ClearAchievements();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.False);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(earnedAccount), Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(500), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.True);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.SpiderMask));
        }

        void UnlockCombatSkins()
        {
            Assert.That(score.TryCommitBestCandidate(2000),Is.True);
            var stats=SkinRunStats.Begin(false,InkSkinId.Ink); stats.items=200; stats.maxLivingPlayers=8;
            Assert.That(PermanentGrowthProfile.SettleRun("combat-skins",2000,2000,0,30,true,stats).Accepted,Is.True);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        [TestCase(AnalyticsDeathCause.Obstacle)] [TestCase(AnalyticsDeathCause.Fall)]
        public void SpiritHaloHealsOriginalOncePerActualCloneDeathAndNeverAboveMaximum(AnalyticsDeathCause cause)
        {
            UnlockCombatSkins(); Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiritHalo),Is.True);
            var manager=Manager(out var growth); Invoke(manager,"SetState",GameState.Playing);
            var original=Player(); manager.RegisterPlayer(original);
            typeof(PlayerController).GetProperty("CurrentHealth").SetValue(original,1);
            int notifications=0; original.HealthChanged+=(_,_)=>notifications++;
            for(int i=0;i<4;i++)
            {
                var clone=Player(); Field(clone,"isRuntimeClone",true); manager.RegisterPlayer(clone);
                Invoke(clone,"KillWithCause",cause);
                int health=original.CurrentHealth;
                Invoke(clone,"KillWithCause",cause);
                Assert.That(original.CurrentHealth,Is.EqualTo(health),"같은 분신의 사망을 중복 보상하지 않는다.");
                Assert.That(health,Is.EqualTo(Mathf.Min(2+i,original.MaxHealth)));
            }
            Assert.That(notifications,Is.EqualTo(2)); Assert.That(manager.LivingPlayerCount,Is.EqualTo(1));
            Assert.That(manager.State,Is.EqualTo(GameState.Playing));
            settings.SetInt(InkSkinPreferences.StorageKey,(int)InkSkinId.Ink);
            Assert.That(growth.SkinSnapshot,Is.EqualTo(InkSkinId.SpiritHalo));
        }
        [TestCase("other")] [TestCase("lobby")] [TestCase("paused")]
        [TestCase("background")] [TestCase("wrong-skin")] [TestCase("dead-original")]
        public void SpiritHaloDoesNotHealDuringCleanupOrInvalidGameplay(string guard)
        {
            UnlockCombatSkins(); InkSkinPreferences.TryEquip(InkSkinId.SpiritHalo);
            var manager=Manager(out var growth); Invoke(manager,"SetState",GameState.Playing);
            var original=Player(); manager.RegisterPlayer(original);
            typeof(PlayerController).GetProperty("CurrentHealth").SetValue(original,1);
            var clone=Player(); Field(clone,"isRuntimeClone",true); manager.RegisterPlayer(clone);
            if(guard=="lobby") typeof(GameManager).GetProperty("State").SetValue(manager,GameState.Lobby);
            if(guard=="paused") typeof(GameManager).GetProperty("IsPaused").SetValue(manager,true);
            if(guard=="background") MobileApplicationLifecycle.SetPlatformVisibility(false);
            if(guard=="wrong-skin") Field(growth,"skinSnapshot",InkSkinId.Ink);
            if(guard=="dead-original") { typeof(PlayerController).GetProperty("IsDead").SetValue(original,true); typeof(PlayerController).GetProperty("CurrentHealth").SetValue(original,0); }
            try
            {
                int expected=original.CurrentHealth;
                Invoke(clone,"KillWithCause",guard=="other" ? AnalyticsDeathCause.Other : AnalyticsDeathCause.Obstacle);
                Assert.That(original.CurrentHealth,Is.EqualTo(expected));
            }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
        }
        [Test] public void TurtleStartsOnlyOriginalWithOneShieldAndDoesNotRefillOnResume()
        {
            UnlockCombatSkins(); InkSkinPreferences.TryEquip(InkSkinId.TurtleShell);
            var manager=Manager(out var growth);
            var original=Player(); manager.RegisterPlayer(original);
            var clone=Player(); Field(clone,"isRuntimeClone",true); manager.RegisterPlayer(clone);
            Assert.That(growth.TryGrantSkinStartShield(original),Is.False);
            Invoke(manager,"BeginPlayingAfterCover");
            Assert.That(original.HasShield,Is.True); Assert.That(clone.HasShield,Is.False);
            Invoke(original,"ConsumeShield");
            Assert.That(growth.TryGrantSkinStartShield(original),Is.False);
            Invoke(manager,"SetState",GameState.GameOver); Invoke(manager,"SetState",GameState.Playing);
            Assert.That(growth.TryGrantSkinStartShield(original),Is.False);
            Invoke(manager,"SetState",GameState.Lobby); Invoke(manager,"BeginPlayingAfterCover");
            Assert.That(original.HasShield,Is.True);
        }
        [TestCase(InkSkinId.PhoenixCrown)]
        [TestCase(InkSkinId.AngelWings, Category = "AngelWings")]
        public void RevivalCostumeConsumesOneSharedRevivalAcrossBothDamageTypesAndAdResume(InkSkinId id)
        {
            UnlockCombatSkins();
            PermanentGrowthProfile.SettleRun("angel-distance", 150000, 150000, 0, 600, true, SkinRunStats.Begin(false, InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip(id), Is.True);
            var manager=Manager(out var growth); Invoke(manager,"SetState",GameState.Playing);
            var original=Player(); manager.RegisterPlayer(original);
            var clone=Player(); Field(clone,"isRuntimeClone",true); manager.RegisterPlayer(clone);
            Assert.That(growth.TryReviveOriginalPlayer(clone),Is.False);
            typeof(PlayerController).GetProperty("CurrentHealth").SetValue(original,1);
            Assert.That(original.TakeHit(),Is.True);
            Assert.That(original.IsDead,Is.False); Assert.That(original.CurrentHealth,Is.EqualTo(1));
            Assert.That(original.IsInkDropBoosted,Is.True); Assert.That(growth.LastBreathAvailable,Is.False);
            Invoke(manager,"SetState",GameState.GameOver); Invoke(manager,"SetState",GameState.Playing);
            Assert.That(growth.TryReviveOriginalPlayer(original),Is.False);
            Invoke(manager,"SetState",GameState.Lobby); Invoke(manager,"SetState",GameState.Playing);
            Assert.That(growth.LastBreathAvailable,Is.True);
        }

        [Test, Category("CloudBoots")]
        public void CloudBootsUseRealJumpEffectOncePerLandingWithoutFakeItemCredit()
        {
            var manager = StartCloudBootsTest(out var growth, out var original, out var platform);
            Assert.That(growth.TryTriggerCloudBootsLanding(original, platform), Is.True);
            Assert.That(original.IsInkDropBoosted, Is.True);
            float speed = Mathf.Sqrt(2 * Mathf.Abs(Physics2D.gravity.y * original.Body.gravityScale) * 50);
            Assert.That(original.Body.linearVelocity.y, Is.EqualTo(speed).Within(.01));
            Assert.That(growth.TryTriggerCloudBootsLanding(original, platform), Is.False);
            Assert.That(original.Body.linearVelocity.y, Is.EqualTo(speed).Within(.01));
            var stats = (SkinRunStats)typeof(GameManager).GetField("skinRunStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(stats.items, Is.Zero); Assert.That(stats.inkDrops, Is.Zero);
            Assert.That(original.AchievementItemFlight, Is.Zero);
            // 다음 착지 시에는 다시 발동한다. 아이템 상승이 끝난 상태를 재현한다.
            typeof(PlayerController).GetProperty("IsInkDropBoosted").SetValue(original, false);
            Assert.That(growth.TryTriggerCloudBootsLanding(original, platform), Is.True);
        }

        [TestCase("drawn"), TestCase("starter"), TestCase("safety"), TestCase("paused")]
        [TestCase("background"), TestCase("clone"), TestCase("dead"), TestCase("already-rising")]
        [TestCase("disabled"), TestCase("other-skin"), TestCase("gameover"), Category("CloudBoots")]
        public void CloudBootsRejectWrongSurfaceAndInactiveGameplay(string scenario)
        {
            var manager = StartCloudBootsTest(out var growth, out var player, out var platform);
            if (scenario is "drawn" or "starter") Field(platform, "mapRestPlatform", false);
            if (scenario == "starter") Field(platform, "lifetime", 0f);
            if (scenario == "safety") Field(platform, "growthSafetyPlatform", true);
            if (scenario == "paused") typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            if (scenario == "background") MobileApplicationLifecycle.SetPlatformVisibility(false);
            if (scenario == "clone") Field(player, "isRuntimeClone", true);
            if (scenario == "dead") typeof(PlayerController).GetProperty("IsDead").SetValue(player, true);
            if (scenario == "already-rising") player.LaunchInkDrop(25f);
            if (scenario == "disabled") player.Body.simulated = false;
            if (scenario == "other-skin") Field(growth, "skinSnapshot", InkSkinId.Ink);
            if (scenario == "gameover") Invoke(manager, "SetState", GameState.GameOver);
            try { Assert.That(growth.TryTriggerCloudBootsLanding(player, platform), Is.False); }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
        }

        [Test, Category("CloudBoots")]
        public void CloudBootsAbilityCopyIsLocalizedAndKeepsTheExistingUnlock()
        {
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.CloudBoots), Is.True);
            Assert.That(InkSkinCatalog.Get(InkSkinId.CloudBoots).Metric, Is.EqualTo(SkinMetric.Distance));
            Assert.That(InkSkinCatalog.Get(InkSkinId.CloudBoots).Goal, Is.EqualTo(18000));
            foreach (string text in new[] { InkSkinCatalog.Ability(InkSkinId.CloudBoots), InkSkinCatalog.Description(InkSkinId.CloudBoots) })
                foreach (bool japanese in new[] { false, true })
                    Assert.That(InkSkinTranslations.TryTranslate(text, japanese, out _), Is.True);
        }

        GameManager StartCloudBootsTest(out RunGrowthController growth, out PlayerController player, out MukJump.Drawing.PlatformCollider platform)
        {
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            PermanentGrowthProfile.SettleRun("cloud-boots", 18000, 18000, 0, 300, true, SkinRunStats.Begin(false, InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.CloudBoots), Is.True);
            var manager = Manager(out growth); player = Player(); manager.RegisterPlayer(player);
            platform = Track(new GameObject("BootsMapPlatform")).AddComponent<MukJump.Drawing.PlatformCollider>();
            Field(platform, "mapRestPlatform", true); Field(platform, "lifetime", 0f);
            Invoke(manager, "BeginPlayingAfterCover");
            return manager;
        }

        [Test, Category("SkinSkills")] public void AppleSkinStartsWithExactlyOneRealInkDropAfterTutorialAndNeverOnRevive()
        {
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Assert.That(PermanentGrowthProfile.TryGrantAppleLinkSkin(PermanentGrowthProfile.SkinAccountToken), Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.BondKnot), Is.True);
            var manager = Manager(out var growth);
            var player = Player(); manager.RegisterPlayer(player);
            Invoke(manager, "BeginPlayingAfterCover");
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Assert.That(growth.TryGrantSkinStartBoost(), Is.False);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Assert.That(growth.TryGrantSkinStartBoost(), Is.True);
            Assert.That(player.IsInkDropBoosted, Is.True);
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(Mathf.Sqrt(2 * Mathf.Abs(Physics2D.gravity.y * player.Body.gravityScale) * 50)).Within(.01));
            Assert.That(growth.TryGrantSkinStartBoost(), Is.False);
            var stats = (SkinRunStats)typeof(GameManager).GetField("skinRunStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(stats.items, Is.EqualTo(1)); Assert.That(stats.inkDrops, Is.EqualTo(1));
            Invoke(manager, "SetState", GameState.GameOver); Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.TryGrantSkinStartBoost(), Is.False);
            Invoke(manager, "SetState", GameState.Lobby); Invoke(manager, "BeginPlayingAfterCover");
            Assert.That(growth.TryGrantSkinStartBoost(), Is.True);
        }

        [Test, Category("SkinSkills")] public void WindSkinSkillUsesOneOriginalLeapAndGameplayOnlyCooldownWithoutItemCredit()
        {
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink); stats.inkDrops = stats.items = 50;
            PermanentGrowthProfile.SettleRun("wind-skin-unlock", 100, 100, 0, 20, true, stats);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.WindFan), Is.True);
            var manager = Manager(out var growth);
            var original = Player(); manager.RegisterPlayer(original);
            var clone = Player(); Field(clone, "isRuntimeClone", true); manager.RegisterPlayer(clone);
            Invoke(manager, "BeginPlayingAfterCover");
            clone.Body.linearVelocity = Vector2.zero;
            Assert.That(growth.TryUseActiveSkinSkill(), Is.True);
            Assert.That(original.IsInkDropBoosted, Is.True); Assert.That(clone.IsInkDropBoosted, Is.False);
            Assert.That(original.Body.linearVelocity.y, Is.EqualTo(Mathf.Sqrt(2 * Mathf.Abs(Physics2D.gravity.y * original.Body.gravityScale) * 25)).Within(.01));
            Assert.That(growth.TryUseActiveSkinSkill(), Is.False);
            Assert.That(original.AchievementItemFlight, Is.Zero);
            stats = (SkinRunStats)typeof(GameManager).GetField("skinRunStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(stats.items, Is.Zero);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(growth, "TickSkinCooldown", 30f); Assert.That(growth.SkinSkillCooldownRemaining, Is.EqualTo(20));
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            try { Invoke(growth, "TickSkinCooldown", 30f); Assert.That(growth.SkinSkillCooldownRemaining, Is.EqualTo(20)); }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
            Invoke(manager, "SetState", GameState.GameOver); Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSkillCooldownRemaining, Is.EqualTo(20));
            Invoke(growth, "TickSkinCooldown", 20f);
            Assert.That(growth.CanUseActiveSkinSkill, Is.False, "특수 상승 중에는 재발동하지 않는다.");
            typeof(PlayerController).GetProperty("IsInkDropBoosted").SetValue(original, false);
            Assert.That(growth.CanUseActiveSkinSkill, Is.True);
            typeof(PlayerController).GetProperty("IsDead").SetValue(original, true);
            Assert.That(growth.TryUseActiveSkinSkill(), Is.False, "분신만 남아도 본체를 부활시키지 않는다.");
            Invoke(manager, "SetState", GameState.Lobby);
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.Ink);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.HasActiveSkinSkill, Is.False);
        }

        [Test, Category("SkinSkills")] public void SkillDescriptionsUseDeclarativeKoreanAndRetainTranslations()
        {
            var skills = Enumerable.Range(0, InkSkinCatalog.Count).Select(i => (InkSkinId)i)
                .Where(InkSkinCatalog.HasAbility).ToArray();
            Assert.That(skills.Length, Is.EqualTo(10));
            foreach (var id in skills)
            {
                string source = InkSkinCatalog.Description(id);
                Assert.That(source, Does.Not.Contain("요."), id.ToString());
                foreach (string sentence in source.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
                    Assert.That(sentence.Trim(), Does.EndWith("다"), id.ToString());
                foreach (var language in new[] { GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    string translated = GameLocalization.Translate(source);
                    Assert.That(translated, Is.Not.Empty.And.Not.EqualTo(source), id + "/" + language);
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(translated, "[가-힣]"), Is.False, id + "/" + language);
                }
            }
        }

        [Test, Category("SkinSkills")] public void NewSkillSkinsHaveStableIdsAndCompleteLocalizations()
        {
            Assert.That((int)InkSkinId.BondKnot, Is.EqualTo(43)); Assert.That((int)InkSkinId.WindFan, Is.EqualTo(44));
            foreach (var id in new[] { InkSkinId.BondKnot, InkSkinId.WindFan })
            {
                Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null);
                Assert.That(InkSkinCatalog.HasAbility(id), Is.True);
                foreach (var source in new[] { InkSkinCatalog.Name(id), InkSkinCatalog.Ability(id), InkSkinCatalog.Description(id), InkSkinAchievements.Requirement(id), InkSkinLore.Get(id).ko })
                    foreach (bool ja in new[] { false, true })
                    { Assert.That(InkSkinTranslations.TryTranslate(source, ja, out var translated), Is.True, source); Assert.That(translated, Is.Not.Empty); }
            }
            var saved = new SkinAchievementState(); saved.Grant(InkSkinId.BondKnot); saved.Grant(InkSkinId.WindFan);
            Assert.That(saved.IsValid, Is.True);
            Assert.That(JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(saved)).Has(InkSkinId.BondKnot), Is.True);
        }

        [Test, Category("MaskStrike")]
        public void MaskStrikeRemovesDragonBeforeDamageAndConsumesExactlyOneCharge()
        {
            var manager = StartMaskTest(out var growth, out var player);
            int health = player.CurrentHealth, releases = 0;
            var enemy = Track(new GameObject("MaskDragon")).AddComponent<MukJump.Obstacles.Obstacle>();
            enemy.Configure(0, 0, 0, MukJump.Obstacles.ObstacleKind.ChildDragon);
            enemy.ReleaseRequested += _ => releases++;
            Invoke(enemy, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
            Invoke(enemy, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(health));
            Assert.That(DragonStats(manager).diedToDragon, Is.False);
            Assert.That(growth.MaskStrikeCooldownRemaining, Is.EqualTo(30));
            Assert.That(growth.TryUseMaskStrike(player, Vector3.right), Is.False);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(growth, "TickSkinCooldown", 30f);
            Assert.That(growth.MaskStrikeCooldownRemaining, Is.EqualTo(30));
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Invoke(manager, "SetState", GameState.GameOver);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.MaskStrikeCooldownRemaining, Is.EqualTo(30), "광고 부활은 재충전하지 않는다.");
            Invoke(growth, "TickSkinCooldown", 29.99f);
            Assert.That(growth.TryUseMaskStrike(player, Vector3.right), Is.False);
            Invoke(growth, "TickSkinCooldown", .02f);
            Assert.That(growth.TryUseMaskStrike(player, Vector3.right), Is.True);
        }

        [Test, Category("MaskStrike")]
        public void MaskStrikeRemovesPouncingHaetaeAndDoesNotProtectAgainstSpikes()
        {
            StartMaskTest(out var growth, out var player);
            int health = player.CurrentHealth, releases = 0;
            var haetae = Track(new GameObject("MaskHaetae")).AddComponent<MukJump.Obstacles.HaetaeObstacle>();
            haetae.Configure(new[] { Idle(), Idle(), Idle(), Idle() }, null, ~0, _ => releases++);
            typeof(MukJump.Obstacles.HaetaeObstacle).GetProperty("State").SetValue(haetae, MukJump.Obstacles.HaetaeObstacleState.Pounce);
            Assert.That(Invoke(haetae, "ResolveContact", player.GetComponent<Collider2D>()), Is.True);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(player.CurrentHealth, Is.EqualTo(health));
            Invoke(growth, "TickSkinCooldown", 30f);
            var spike = Track(new GameObject("MaskSpike")).AddComponent<MukJump.Obstacles.Obstacle>();
            spike.Configure(0, 0, 0, MukJump.Obstacles.ObstacleKind.Spike);
            Invoke(spike, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
            Assert.That(growth.MaskStrikeCooldownRemaining, Is.Zero);
            Assert.That(player.CurrentHealth, Is.LessThan(health));
        }

        [TestCase("clone"), TestCase("dead"), TestCase("disabled"), TestCase("background"), TestCase("other"), Category("MaskStrike")]
        public void MaskStrikeRejectsInvalidCasters(string scenario)
        {
            StartMaskTest(out var growth, out var player);
            if (scenario == "clone") Field(player, "isRuntimeClone", true);
            if (scenario == "dead") typeof(PlayerController).GetProperty("IsDead").SetValue(player, true);
            if (scenario == "disabled") player.Body.simulated = false;
            if (scenario == "background") MobileApplicationLifecycle.SetPlatformVisibility(false);
            if (scenario == "other") Field(growth, "skinSnapshot", InkSkinId.Ink);
            try { Assert.That(growth.TryUseMaskStrike(player, Vector3.right), Is.False); Assert.That(growth.MaskStrikeCooldownRemaining, Is.Zero); }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
        }

        [Test, Category("MaskStrike")]
        public void MaskStrikeReusesFourAuthoredFramesAndPausesWithTheGame()
        {
            var manager = StartMaskTest(out _, out var player);
            var vfx = manager.gameObject.AddComponent<InkSkinStrikeVfx>();
            Invoke(vfx, "OnEnable");
            Assert.That(vfx.Play(player, Vector3.right), Is.True);
            var renderer = vfx.Renderer; var first = renderer.sprite;
            Invoke(vfx, "Advance", .10f); Assert.That(renderer.sprite, Is.Not.SameAs(first));
            float elapsed = vfx.Elapsed;
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(vfx, "Advance", 1f); Assert.That(vfx.Elapsed, Is.EqualTo(elapsed)); Assert.That(renderer.enabled, Is.False);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Invoke(vfx, "Advance", .3f); Assert.That(vfx.IsPlaying, Is.False);
            Assert.That(vfx.Play(player, Vector3.left), Is.True); Assert.That(vfx.Renderer, Is.SameAs(renderer));
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Invoke(vfx, "Advance", .21f); Assert.That(vfx.IsPlaying, Is.False);
            var samples = InkSkinStrikeSound.BuildSamples();
            Assert.That(samples.Length, Is.EqualTo((int)(22050 * .30f)));
            Assert.That(samples.All(float.IsFinite), Is.True); Assert.That(samples.Max(Mathf.Abs), Is.EqualTo(.72f).Within(.001f));
            foreach (string source in new[] { InkSkinCatalog.Ability(InkSkinId.YakshaMask), InkSkinCatalog.Description(InkSkinId.YakshaMask) })
                foreach (bool ja in new[] { false, true }) Assert.That(InkSkinTranslations.TryTranslate(source, ja, out _), Is.True);
        }

        GameManager StartMaskTest(out RunGrowthController growth, out PlayerController player)
        {
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            PermanentGrowthProfile.SettleRun("mask-unlock", 2000, 2000, 0, 180, true, SkinRunStats.Begin(false, InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.YakshaMask), Is.True);
            var manager = Manager(out growth); Invoke(manager, "BeginPlayingAfterCover");
            player = Player(); manager.RegisterPlayer(player);
            return manager;
        }

        [Test, Category("AngelWings"), Category("AngelRingRename")]
        public void AngelWingsKeepSavedBitsAndUnlockOnlyAtOneHundredFiftyThousandMetres()
        {
            Assert.That(InkSkinCatalog.Name(InkSkinId.AngelWings), Is.EqualTo("여명의 고리"));
            Assert.That(InkAchievementCatalog.Name(62), Is.EqualTo("여명의 고리"));
            Assert.That(InkAchievementCatalog.Id(62), Is.EqualTo("mukjump.achievement.skin.62"));
            Assert.That(InkSkinTranslations.TryTranslate("여명의 고리", false, out var english), Is.True);
            Assert.That(english, Is.EqualTo("Ring of Dawn"));
            Assert.That(InkSkinTranslations.TryTranslate("여명의 고리", true, out var japanese), Is.True);
            Assert.That(japanese, Is.EqualTo("夜明けの環"));
            var state = new SkinAchievementState();
            Assert.That(state.Progress(InkSkinId.AngelWings, 149999, 0), Is.EqualTo(149999));
            state.Settle(1, 1, SkinRunStats.Begin(false, InkSkinId.Ink), 149999, 0);
            Assert.That(state.Has(InkSkinId.AngelWings), Is.False);
            state.Settle(1, 1, SkinRunStats.Begin(false, InkSkinId.Ink), 150000, 0);
            Assert.That(state.Has(InkSkinId.AngelWings), Is.True);
            state.Grant(InkSkinId.ThreeCushion); state.Grant(InkSkinId.StormScarf);
            var saved = JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(state));
            Assert.That(saved.IsValid, Is.True);
            foreach (var id in new[] { InkSkinId.AngelWings, InkSkinId.ThreeCushion, InkSkinId.StormScarf }) Assert.That(saved.Has(id), Is.True);
            Assert.That(InkAchievementCatalog.SavedProgress(saved, 10000).Any(x => x.id.EndsWith(".62")), Is.False, "미등록 Apple 업적을 보내 기존 배치를 실패시키지 않는다.");
            foreach (string source in new[] { InkSkinCatalog.Name(InkSkinId.AngelWings), InkSkinCatalog.Ability(InkSkinId.AngelWings), InkSkinCatalog.Description(InkSkinId.AngelWings), InkSkinLore.Get(InkSkinId.AngelWings).ko })
                foreach (bool ja in new[] { false, true }) Assert.That(InkSkinTranslations.TryTranslate(source, ja, out _), Is.True);
            Assert.That(InkSkinCatalog.Sprite(InkSkinId.AngelWings), Is.SameAs(InkAccessoryMotion.Animation(InkSkinId.AngelWings).Frame(0)));
        }

        GameManager Manager(out RunGrowthController growth)
        {
            var host = Track(new GameObject("SkinTestSystems"));
            var manager = host.AddComponent<GameManager>(); Invoke(manager, "OnEnable");
            growth = host.GetComponent<RunGrowthController>() ?? host.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            return manager;
        }

        [Test, Category("SpiderWeb")]
        public void SpiderWaitsThirtyActiveSecondsAndUsesAnActualWall()
        {
            var manager = StartSpiderTest(out var growth, out var player, out var web);
            Assert.That(growth.WebClimbCooldownRemaining, Is.EqualTo(30));
            Invoke(growth, "TickSkinCooldown", 29.9f); Invoke(growth, "Update");
            Assert.That(web.IsClimbing, Is.False);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(growth, "TickSkinCooldown", 30f);
            Assert.That(growth.WebClimbCooldownRemaining, Is.GreaterThan(0));
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Invoke(growth, "TickSkinCooldown", 1f); Invoke(growth, "Update");
            Assert.That(web.IsClimbing, Is.True); Assert.That(growth.WebClimbCooldownRemaining, Is.EqualTo(30));
            Assert.That(web.Destination.x, Is.InRange(4f, 4.9f)); Assert.That(web.Destination.y, Is.EqualTo(18));
            Assert.That(player.IsInkDropBoosted, Is.False, "발사된 줄이 붙기 전에 상승하지 않는다.");
            Invoke(web, "Advance", .16f);
            Assert.That(player.IsInkDropBoosted, Is.True); Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(10));
            Assert.That(DragonStats(manager).items, Is.Zero);
            float elapsed = web.Elapsed;
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            try { Invoke(web, "Advance", 1f); Assert.That(web.Elapsed, Is.EqualTo(elapsed)); }
            finally { MobileApplicationLifecycle.SetPlatformVisibility(true); }
            player.LaunchInkDrop(50); Vector2 velocity = player.Body.linearVelocity;
            Invoke(web, "Advance", .02f); Assert.That(web.IsClimbing, Is.False);
            Assert.That(player.Body.linearVelocity, Is.EqualTo(velocity), "다른 아이템 효과를 덮어쓰지 않는다.");
            Invoke(manager, "SetState", GameState.GameOver); Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.WebClimbCooldownRemaining, Is.EqualTo(30));
        }

        [TestCase("clone"), TestCase("boost"), TestCase("no-wall"), TestCase("wrong-skin"), Category("SpiderWeb")]
        public void SpiderDoesNotSpendChargeWhenItCannotClimb(string guard)
        {
            StartSpiderTest(out var growth, out var player, out var web);
            if (guard == "clone") Field(player, "isRuntimeClone", true);
            if (guard == "boost") player.LaunchInkDrop(50);
            if (guard == "no-wall") cleanup.OfType<GameObject>().First(x => x.name == "WebTestWall").SetActive(false);
            if (guard == "wrong-skin") Field(growth, "skinSnapshot", InkSkinId.Ink);
            Physics2D.SyncTransforms();
            Assert.That(web.TryStart(player), Is.False);
        }

        [Test, Category("SpiderWeb")]
        public void SpiderReleasesInsideTheWallWithoutTeleportingOrLeavingAVisual()
        {
            StartSpiderTest(out _, out var player, out var web);
            Assert.That(web.TryStart(player), Is.True);
            Vector2 initial = player.Body.position;
            Invoke(web, "Advance", .2f);
            Assert.That(player.Body.position, Is.EqualTo(initial), "이동은 물리 속도로만 수행한다.");
            player.Body.position = web.Destination;
            Invoke(web, "Advance", .02f);
            Assert.That(web.IsClimbing, Is.False);
            Assert.That(player.Body.linearVelocity.x, Is.LessThan(0));
            Assert.That(web.GetComponentsInChildren<LineRenderer>().All(x => !x.enabled), Is.True);
            foreach (string source in new[] { InkSkinCatalog.Ability(InkSkinId.SpiderMask), InkSkinCatalog.Description(InkSkinId.SpiderMask) })
                foreach (bool ja in new[] { false, true }) Assert.That(InkSkinTranslations.TryTranslate(source, ja, out _), Is.True);
        }

        GameManager StartSpiderTest(out RunGrowthController growth, out PlayerController player, out InkWebClimb web)
        {
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.True);
            var manager = Manager(out growth); Invoke(manager, "BeginPlayingAfterCover");
            player = Player(); manager.RegisterPlayer(player);
            var wall = Track(new GameObject("WebTestWall"));
            wall.transform.position = new Vector3(5.5f, 0, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.6f, 60);
            wall.AddComponent<ScreenSideWall>().Initialize(null, false);
            Physics2D.SyncTransforms();
            web = manager.gameObject.AddComponent<InkWebClimb>(); Invoke(web, "OnEnable");
            return manager;
        }

        [TestCase("fatal", true)] [TestCase("health-left", false)]
        [TestCase("shield", false)] [TestCase("boost", false)] [TestCase("paused", false)]
        [TestCase("clone", false)] [TestCase("spike", false)] [TestCase("fall-after-hit", false)]
        [TestCase("last-breath", false)]
        [Category("DragonTail")]
        public void DragonTailRequiresAnActualFatalDragonCollision(string scenario, bool expected)
        {
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            try
            {
                var manager = Manager(out var growth);
                Invoke(manager, "SetState", GameState.Playing);
                Field(manager, "currentRunId", "dragon-collision-test");
                Field(manager, "skinRunStats", SkinRunStats.Begin(false, InkSkinId.Ink));
                var player = Player(); manager.RegisterPlayer(player);
                typeof(PlayerController).GetProperty("CurrentHealth").SetValue(player,
                    scenario is "health-left" or "fall-after-hit" ? 2 : 1);
                if (scenario == "clone") { Field(player, "isRuntimeClone", true); manager.RegisterPlayer(Player()); }
                if (scenario == "shield") player.GrantShield();
                if (scenario == "boost") player.LaunchInkDrop(50);
                if (scenario == "paused") typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
                if (scenario == "last-breath") typeof(RunGrowthController).GetProperty("LastBreathAvailable").SetValue(growth, true);
                bool recordedBeforeGameOver = false;
                manager.StateChanged += (_, next) => { if (next == GameState.GameOver) recordedBeforeGameOver = DragonStats(manager).diedToDragon; };
                var obstacle = Track(new GameObject("DragonTailTestObstacle")).AddComponent<MukJump.Obstacles.Obstacle>();
                obstacle.Configure(0, 0, 0, scenario == "spike" ? MukJump.Obstacles.ObstacleKind.Spike : MukJump.Obstacles.ObstacleKind.ChildDragon);
                Invoke(obstacle, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
                if (scenario == "fall-after-hit") Invoke(player, "KillWithCause", AnalyticsDeathCause.Fall);
                Assert.That(DragonStats(manager).diedToDragon, Is.EqualTo(expected));
                if (expected)
                {
                    Assert.That(player.IsDead, Is.True);
                    Assert.That(recordedBeforeGameOver, Is.True, "정산 복구본보다 먼저 사망 원인을 기록한다.");
                    Invoke(manager, "SettleGameOverResult");
                    Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
                    Assert.That(PermanentGrowthProfile.AchievementSnapshot().dragonDeaths, Is.EqualTo(1));
                    Invoke(obstacle, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
                    Assert.That(DragonStats(manager).diedToDragon, Is.True);
                }
                else Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            }
            finally { GameManager.RestorePendingGameOverSettlementStoreForTests(); }
        }

        [Test, Category("DragonTail")]
        public void DragonTailSettlementPersistsWithAccountAndNeverGrantsOnFailedOrUnmeasuredRuns()
        {
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink); stats.diedToDragon = true;
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string before), Is.True);
            var invalid = stats; invalid.version = 0;
            PermanentGrowthProfile.SettleRun("dragon-invalid", 1, 1, 0, 1, true, invalid);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            growthStore.ThrowOnPrimarySave = true;
            Assert.That(PermanentGrowthProfile.SettleRun("dragon-save", 1, 1, 0, 1, true, stats).Accepted, Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            growthStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.SettleRun("dragon-save", 1, 1, 0, 1, true, stats).Accepted, Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            PermanentGrowthProfile.SettleRun("dragon-save", 1, 1, 0, 1, true, stats);
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().dragonDeaths, Is.EqualTo(1), "중복 정산 제외");
            Assert.That(PermanentGrowthProfile.SettleRun("dragon-second", 1, 1, 0, 1, true, stats).Accepted, Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().dragonDeaths, Is.EqualTo(2));
            Assert.That(PermanentGrowthProfile.SettleRun("dragon-third", 1, 1, 0, 1, true, stats).Accepted, Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.True);
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string earned), Is.True);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(before), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.False);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(earned), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.DragonTail), Is.True);
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().dragonDeaths, Is.EqualTo(3));
            var roundTrip = JsonUtility.FromJson<SkinRunStats>(JsonUtility.ToJson(stats));
            Assert.That(roundTrip.diedToDragon, Is.True);
            Assert.That(JsonUtility.FromJson<SkinRunStats>("{\"version\":1,\"maxLivingPlayers\":1}").diedToDragon, Is.False);
        }

        [Test, Category("DragonTail")]
        public void DragonTailHasLocalizedRequirementAndPlaysAuthoredFrames()
        {
            Assert.That((int)InkSkinId.DragonTail, Is.EqualTo(45));
            Assert.That(InkSkinCatalog.Sprite(InkSkinId.DragonTail), Is.Not.Null);
            Assert.That(InkSkinCatalog.BehindBody(InkSkinId.DragonTail), Is.True);
            Assert.That(InkSkinCatalog.HasAbility(InkSkinId.DragonTail), Is.False);
            foreach (string source in new[] { InkSkinCatalog.Name(InkSkinId.DragonTail), InkSkinAchievements.Requirement(InkSkinId.DragonTail), InkSkinLore.Get(InkSkinId.DragonTail).ko })
                foreach (bool japanese in new[] { false, true })
                    Assert.That(InkSkinTranslations.TryTranslate(source, japanese, out _), Is.True);
            var animation = InkAccessoryMotion.Animation(InkSkinId.DragonTail);
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.FrameCount, Is.EqualTo(4));
            Assert.That(animation.Sample(0), Is.Not.SameAs(animation.Sample(1f / animation.FramesPerSecond)));
        }
        static SkinRunStats DragonStats(GameManager manager) => (SkinRunStats)typeof(GameManager)
            .GetField("skinRunStats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        PlayerController Player()
        {
            var host = Track(new GameObject("SkinTestPlayer", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D)));
            host.GetComponent<CircleCollider2D>().radius = .4f;
            var player = host.AddComponent<PlayerController>(); Invoke(player, "Awake");
            typeof(PlayerController).GetProperty("GroundNormal").SetValue(player, Vector2.up);
            return player;
        }
        static Sprite Idle() => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s => s.name == "idle");
        T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
        static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        public static void CompilePlayerAndRebuild()
        {
            var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
            {
                target = BuildTarget.iOS, group = BuildTargetGroup.iOS,
                options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
            };
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/MukJumpInkSkinsPlayer");
            if (result.assemblies == null || result.assemblies.Count == 0)
                throw new UnityEditor.Build.BuildFailedException("스킨 iOS Player 컴파일 실패");
            MukJump.EditorTools.MukJumpSceneBuilder.Build();
            var audit = Newtonsoft.Json.Linq.JObject.FromObject(MukJump.EditorTools.MukJumpAgentAudit.Audit());
            System.IO.File.WriteAllText("output/qa/ink-skins/scene-audit.json", audit.ToString());
            if ((bool?)audit["readyForValidation"] != true) throw new Exception("스킨 씬 감사 실패: " + audit);
            Debug.Log("[InkSkins] iOS Player assemblies=" + result.assemblies.Count + "; " + audit);
        }
    }
}
