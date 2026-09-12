using System.Linq;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkShowroomTests
    {
        GameObject host, scoreHost, eventHost;
        InkWardrobeView view;
        MemoryLobbySettingsStore settings;
        VfxQualityTier originalTier;
        RectTransform Focus => (RectTransform)view.Paper.Find("ShowroomStage/Preview");
        [SetUp] public void Setup()
        {
            settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 100 });
            scoreHost = new GameObject("ShowroomTestScore");
            var score = scoreHost.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable"); Invoke(score, "Awake");
            for (int i = 0; i < 10; i++) PermanentGrowthProfile.SettleRun("showroom-" + i, 10, 0, true);
            originalTier = VfxQualityRuntime.Tier;
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            host = new GameObject("ShowroomTestHost");
            view = host.AddComponent<InkWardrobeView>();
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png")
                .OfType<Sprite>().First(s => s.name == "idle");
            view.BuildForTests(idle); view.SetNavigationPresentation(true, true);
        }
        [TearDown] public void Teardown()
        {
            settings.ThrowOnSave = false;
            Object.DestroyImmediate(host); Object.DestroyImmediate(scoreHost);
            if (eventHost != null) Object.DestroyImmediate(eventHost);
            InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            VfxQualityRuntime.SetTier(originalTier, VfxQualityChangeReason.DebugOverride);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        [TestCase(GameLanguage.Korean, "꾸미기")]
        [TestCase(GameLanguage.English, "Wardrobe")]
        [TestCase(GameLanguage.Japanese, "着せ替え")]
        public void ShowroomTitleMatchesLocale(GameLanguage language, string expected)
        {
            GameLocalization.SetLanguage(language);
            Assert.That(view.Paper.Find("Title").GetComponent<Text>().text, Is.EqualTo(expected));
        }
        [Test] public void WardrobeIsADedicatedLandscapeScreenWithoutParchmentOrModalDim()
        {
            Assert.That(view.Paper.name,Is.EqualTo("WardrobeScreen"));
            Assert.That(view.Paper.sizeDelta,Is.EqualTo(new Vector2(1080,1920)));
            Assert.That(host.GetComponentsInChildren<HanjiScrollFrame>(true),Is.Empty);
            var canvas=view.GetComponentInChildren<Canvas>();
            var blocker=canvas.transform.Find("LobbyBackgroundInputBlocker").GetComponent<Image>();
            Assert.That(blocker.color,Is.EqualTo(Color.clear));
            Assert.That(blocker.raycastTarget,Is.True);
            Assert.That(blocker.rectTransform.anchorMin,Is.EqualTo(Vector2.zero));
            Assert.That(blocker.rectTransform.anchorMax,Is.EqualTo(Vector2.one));
            Assert.That(view.BackButton.transform.parent,Is.EqualTo(view.Paper));
            Assert.That(view.BackButton.transform.Find("Paper"),Is.Null);
            Assert.That(view.BackButton.transform.Find("Icon"),Is.Not.Null);
            Assert.That(view.BackButton.GetComponent<RectTransform>().anchoredPosition.y,Is.EqualTo(860));
            Assert.That(view.EquipButton.transform.Find("Paper"),Is.Null);
            Assert.That(view.EquipButton.transform.Find("BrushBackground"),Is.Not.Null);
            Assert.That(view.CanBrowse,Is.True);
        }
        [Test] public void OneLargeOriginalInkIsFramedByTwoArrowButtonsAndAWideCollection()
        {
            view.Select(InkSkinId.Gat);
            var body = Focus.Find("OriginalInkBody").GetComponent<Image>();
            Assert.That(Focus.parent.GetComponent<RectMask2D>(), Is.Not.Null);
            foreach (string side in new[] { "PreviousCostume", "NextCostume" })
            {
                var neighbor = view.Paper.Find("ShowroomStage/" + side);
                Assert.That(neighbor.Find("OriginalInkBody"), Is.Null);
                Assert.That(neighbor.GetComponent<RectTransform>().sizeDelta.x, Is.GreaterThanOrEqualTo(116));
                Assert.That(neighbor.Find("Label").GetComponent<Text>().text, Is.EqualTo(side == "PreviousCostume" ? "‹" : "›"));
                Assert.That(neighbor.GetComponent<Button>(), Is.Not.Null);
                Assert.That(neighbor.GetSiblingIndex(), Is.GreaterThan(Focus.GetSiblingIndex()));
            }
            Assert.That(body.enabled, Is.True);
            Assert.That(body.rectTransform.sizeDelta.x, Is.EqualTo(500));
            Assert.That(AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
            var strip = view.CollectionContent.Cast<Transform>().Cast<RectTransform>().ToArray();
            Assert.That(strip.Length,Is.EqualTo(InkSkinCatalog.Count));
            Assert.That(strip.Select(t=>t.anchoredPosition.y).Distinct().Count(), Is.EqualTo(8));
            Assert.That(strip[0].sizeDelta, Is.EqualTo(new Vector2(180,190)));
            Assert.That(view.CollectionScroll.vertical && !view.CollectionScroll.horizontal,Is.True);
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void EveryCardFitsItsArtworkAndStateWithoutOverlappingTheNextRow(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            foreach (bool foundSecrets in new[] { false, true })
            {
                if (foundSecrets)
                {
                    PermanentGrowthProfile.SettleRun("card-fit-secrets",777,777,0,60,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
                    foreach (var id in new[] { InkSkinId.NightBat, InkSkinId.Astronaut, InkSkinId.OrigamiWings })
                        Assert.That(PermanentGrowthProfile.GrantSkinSecret(id), Is.True);
                }
                for (int selected = 0; selected < InkSkinCatalog.Count; selected++)
                {
                    view.Select((InkSkinId)selected); Canvas.ForceUpdateCanvases();
                    var strip = view.CollectionContent.Cast<Transform>().Where(t=>t.name.StartsWith("SkinCard") && t.gameObject.activeSelf)
                        .Cast<RectTransform>().ToArray();
                    Assert.That(strip.Length, Is.EqualTo(InkSkinCatalog.Count));
                    for (int slot = 0; slot < strip.Length; slot++)
                    {
                        var card = strip[slot];
                        AssertInside(BoundsIn(view.CollectionContent,card),view.CollectionContent.rect,0,card.name);
                        if(slot>=5) Assert.That(card.anchoredPosition.y,Is.LessThan(strip[slot-5].anchoredPosition.y-190));
                        var viewport = (RectTransform)card.Find("PreviewViewport");
                        Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                        var preview = (RectTransform)viewport.Find("Preview");
                        Assert.That(preview.localScale.x, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
                        if(slot==0) Assert.That(preview.localScale, Is.EqualTo(Vector3.one));
                        foreach (var image in preview.GetComponentsInChildren<Image>().Where(i=>i.enabled))
                            AssertInside(BoundsIn(viewport,image.rectTransform),viewport.rect,3.9f,card.name+"/"+image.name);
                        var state = card.Find("State").GetComponent<Text>();
                        Rect stateBounds = BoundsIn(card,state.rectTransform);
                        AssertInside(stateBounds,card.rect,12,card.name+"/State");
                        Assert.That(state.preferredWidth, Is.LessThanOrEqualTo(state.rectTransform.rect.width+.1f),state.text);
                        Assert.That(state.preferredHeight, Is.LessThanOrEqualTo(state.rectTransform.rect.height+.1f),state.text);
                        Assert.That(BoundsIn(card,viewport).yMin, Is.GreaterThanOrEqualTo(stateBounds.yMax+4));
                    }
                    string[] rows = { "SkinName", "Ability", "Description", "Achievement", "AchievementProgress", "AchievementTrack", "CollectionScroll", "EquipButton" };
                    for (int row = 1; row < rows.Length; row++)
                        Assert.That(BoundsIn(view.Paper,(RectTransform)view.Paper.Find(rows[row-1])).yMin,
                            Is.GreaterThanOrEqualTo(BoundsIn(view.Paper,(RectTransform)view.Paper.Find(rows[row])).yMax+2),rows[row]);
                }
            }
        }
        [Test] public void ArrowsSwapOnlyClothesAndPeriodicRefreshDoesNotSnapTheAnimation()
        {
            view.Select(InkSkinId.Gat); view.Presentation.Cancel();
            var body = Focus.Find("OriginalInkBody").GetComponent<Image>();
            var original = body.sprite;
            var costume = Focus.Find("Accessory").GetComponent<Image>();
            var previous = costume.sprite;
            view.Paper.Find("ShowroomStage/NextCostume").GetComponent<Button>().onClick.Invoke();
            var outgoing = Focus.Find("OutgoingCostume").GetComponent<Image>();
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.SpiderMask));
            Assert.That(outgoing.enabled, Is.True);
            Assert.That(outgoing.sprite, Is.SameAs(previous));
            Assert.That(costume.sprite, Is.SameAs(InkSkinCatalog.Sprite(InkSkinId.SpiderMask)));
            view.Presentation.SampleForTests(.08f);
            var movingPosition = costume.rectTransform.anchoredPosition;
            Assert.That(movingPosition, Is.Not.EqualTo(InkSkinCatalog.Offset(InkSkinId.SpiderMask) * 500));
            Invoke(view, "RefreshSelection");
            Assert.That(costume.rectTransform.anchoredPosition, Is.EqualTo(movingPosition));
            Assert.That(body.sprite, Is.SameAs(original));
            Assert.That(Focus.GetComponentsInChildren<Image>().Count(i=>i.sprite == original), Is.EqualTo(1));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            view.Presentation.SampleForTests(InkShowroomPresentation.BrowseDuration);
            Assert.That(Vector2.Distance(costume.rectTransform.anchoredPosition, InkSkinCatalog.Offset(InkSkinId.SpiderMask) * 500), Is.LessThan(.01f));
            view.Presentation.Cancel(); AssertStopped();
        }
        [Test] public void RapidArrowReversalUsesLastCostumeWithoutAllocatingOrLeavingAGhost()
        {
            view.Select(InkSkinId.Gat); view.Presentation.Cancel();
            int count = host.GetComponentsInChildren<Transform>(true).Length;
            for(int i=0;i<40;i++)
            {
                view.StepSelection(i%2==0 ? 1 : -1); view.Presentation.SampleForTests(.05f);
                Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().sprite,
                    Is.SameAs(InkSkinCatalog.Sprite(i%2==0 ? InkSkinId.Gat : InkSkinId.SpiderMask)));
            }
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            view.SetNavigationPresentation(false,false); AssertStopped();
        }
        [Test] public void BrowseDoesNotEquipAndSuccessfulSavePlaysOnlyOnce()
        {
            view.Select(InkSkinId.Gat);
            Assert.That(view.Presentation.IsPlaying, Is.True);
            Assert.That(view.Presentation.IsEquipping, Is.False);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            view.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            Assert.That(view.Presentation.IsEquipping, Is.True);
            view.EquipButton.onClick.Invoke();
            Assert.That(view.Presentation.EquipPlayCount, Is.EqualTo(1));
        }
        [Test] public void FailedSaveDoesNotCelebrateOrChangeEquippedSkin()
        {
            view.Select(InkSkinId.Gat); settings.ThrowOnSave = true;
            view.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(view.Presentation.EquipPlayCount, Is.Zero);
            Assert.That(view.Paper.Find("SaveStatus").GetComponent<Text>().text, Is.Not.Empty);
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void SaveFailureFitsBetweenDescriptionAndEquipAndRestoresProgressOnRetry(GameLanguage language)
        {
            GameLocalization.SetLanguage(language); view.Select(InkSkinId.Gat);
            settings.ThrowOnSave = true; view.EquipButton.onClick.Invoke(); Canvas.ForceUpdateCanvases();
            var status = view.Paper.Find("SaveStatus").GetComponent<Text>();
            Assert.That(status.text, Is.Not.Empty);
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height));
            Rect bounds = BoundsIn(view.Paper,status.rectTransform);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(BoundsIn(view.Paper,(RectTransform)view.Paper.Find("Description")).yMin-10));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(BoundsIn(view.Paper,view.EquipButton.GetComponent<RectTransform>()).yMax+10));
            foreach (var name in new[] { "Achievement", "AchievementProgress" })
                Assert.That(view.Paper.Find(name).GetComponent<Text>().enabled, Is.False);
            Assert.That(view.Paper.Find("AchievementTrack").gameObject.activeSelf, Is.False);
            settings.ThrowOnSave = false; view.EquipButton.onClick.Invoke();
            Assert.That(status.text, Is.Empty);
            foreach (var name in new[] { "Achievement", "AchievementProgress" })
                Assert.That(view.Paper.Find(name).GetComponent<Text>().enabled, Is.True);
            Assert.That(view.Paper.Find("AchievementTrack").gameObject.activeSelf, Is.True);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void BrushProgressUsesAccurateFillAndHidesTheSealWhenResetOrConcealed(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0),Is.True);
            view.Select(InkSkinId.Gat);
            var track=view.Paper.Find("AchievementTrack").GetComponent<Image>();
            var fill=track.transform.Find("Progress").GetComponent<Image>();
            var seal=track.transform.Find("CompletionSeal").gameObject;
            var number=view.Paper.Find("AchievementProgress").GetComponent<Text>();
            int objects=view.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(track.sprite,Is.SameAs(InkUiTextureFactory.CreateBrushSprite()));
            Assert.That(fill.sprite,Is.SameAs(track.sprite));
            Assert.That(fill.type,Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod,Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.fillOrigin,Is.Zero);
            Assert.That(fill.preserveAspect,Is.False);
            foreach (int value in new[] {0,43,100,1000,0})
            {
                Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(value),Is.True);
                for(int i=0;i<4;i++) Invoke(view,"RefreshSelection");
                Assert.That(fill.fillAmount,Is.EqualTo(Mathf.Clamp01(value/100f)).Within(.001f));
                Assert.That(number.text,Is.EqualTo($"{Mathf.Min(value,100)} / 100"));
                Assert.That(seal.activeSelf,Is.EqualTo(value>=100));
                Canvas.ForceUpdateCanvases();
                Assert.That(fill.rectTransform.rect.size,Is.EqualTo(track.rectTransform.rect.size));
            }
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(100),Is.True);
            view.Select(InkSkinId.Gat); Assert.That(seal.activeSelf,Is.True);
            foreach(var id in new[]{InkSkinId.NightBat,InkSkinId.Ink})
            {
                view.Select(id);
                Assert.That(track.gameObject.activeSelf,Is.False);
                Assert.That(fill.fillAmount,Is.Zero);
                Assert.That(seal.activeSelf,Is.False);
                Assert.That(number.text,Is.Empty);
            }
            Assert.That(view.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void CompletionSealFitsBesideBrushAndDisappearsDuringSaveFailure(GameLanguage language)
        {
            GameLocalization.SetLanguage(language); view.Select(InkSkinId.Gat); Canvas.ForceUpdateCanvases();
            var track=(RectTransform)view.Paper.Find("AchievementTrack");
            var seal=(RectTransform)track.Find("CompletionSeal");
            Rect bounds=BoundsIn(view.Paper,seal);
            AssertInside(bounds,view.Paper.rect,50,"CompletionSeal");
            Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(BoundsIn(view.Paper,track).xMax+12));
            Assert.That(bounds.Overlaps(BoundsIn(view.Paper,(RectTransform)view.EquipButton.transform)),Is.False);
            Assert.That(bounds.Overlaps(BoundsIn(view.Paper,(RectTransform)view.Paper.Find("AchievementProgress"))),Is.False);
            Assert.That(seal.GetComponentInChildren<InkHudSurface>().raycastTarget,Is.False);
            Assert.That(seal.Find("Mark").GetComponent<Image>().raycastTarget,Is.False);
            settings.ThrowOnSave=true; view.EquipButton.onClick.Invoke();
            Assert.That(seal.gameObject.activeSelf,Is.False);
            settings.ThrowOnSave=false; view.EquipButton.onClick.Invoke();
            Assert.That(seal.gameObject.activeInHierarchy,Is.True);
        }
        [Test] public void LockedAndConcealedCostumesNeverEquipOrLeakArtThroughTheOutgoingLayer()
        {
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0), Is.True);
            var secret = Enumerable.Range(1, InkSkinCatalog.Count-1).Select(i => (InkSkinId)i).First(InkSkinCatalog.IsConcealed);
            view.Select((InkSkinId)((int)secret-1));
            view.Select(secret); view.EquipButton.onClick.Invoke();
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().enabled, Is.False);
            Assert.That(view.Paper.Find("SkinName").GetComponent<Text>().text, Is.Empty);
            Assert.That(Focus.Find("QuestionMark").GetComponent<Text>().enabled, Is.True);
            view.StepSelection(-1);
            Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().sprite, Is.Null);
            Assert.That(view.Presentation.EquipPlayCount, Is.Zero);
        }
        [Test] public void CloseCancelsEverythingAndReopenUsesSavedSelection()
        {
            view.Select(InkSkinId.Gat); view.EquipButton.onClick.Invoke();
            view.Presentation.SampleForTests(.25f);
            Assert.That(view.Presentation.ParticleCount, Is.GreaterThan(0));
            view.SetNavigationPresentation(false, false);
            AssertStopped();
            view.SetNavigationPresentation(true, true);
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat)); AssertStopped();
        }
        [Test, Category("NightSecret")] public void AccountClearBeforeTheNextRefreshDoesNotCopyTheOldSecretIntoTheOutgoingLayer()
        {
            PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NightBat), Is.True);
            view.Select(InkSkinId.NightBat); view.Presentation.Cancel();
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().enabled, Is.True);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(InkSkinCatalog.IsConcealed(InkSkinId.NightBat), Is.True);
            view.Select(InkSkinId.Gat);
            var outgoing = Focus.Find("OutgoingCostume").GetComponent<Image>();
            Assert.That(outgoing.sprite, Is.Null); Assert.That(outgoing.enabled, Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void LockedCostumesKeepBlackSilhouettesThroughMotionAndCancellation(bool reduced)
        {
            LobbySettingsProfile.SetReducedMotionEnabled(reduced);
            foreach (var id in Enumerable.Range(1,InkSkinCatalog.Count-1).Select(i=>(InkSkinId)i)
                .Where(id=>!InkSkinCatalog.IsConcealed(id) && !InkSkinAchievements.IsUnlocked(id)))
            {
                view.Select(id);
                foreach (float time in new[] { 0f, .06f, .2f, .42f })
                {
                    view.Presentation.SampleForTests(time); Invoke(view,"RefreshSelection");
                    AssertBlack(Focus.Find("OriginalInkBody").GetComponent<Image>());
                    AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
                }
                view.Presentation.Cancel();
                AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
                var card=view.CollectionContent.Find("SkinCard"+(int)id);
                AssertBlack(card.Find("PreviewViewport/Preview/OriginalInkBody").GetComponent<Image>());
                AssertBlack(card.Find("PreviewViewport/Preview/Accessory").GetComponent<Image>());
                Assert.That(card.Find("PreviewViewport/QuestionMark").GetComponent<Text>().enabled,Is.False);
            }
            for (int i=0;i<6;i++)
            {
                view.Select(i%2==0 ? InkSkinId.RedHeadband : InkSkinId.CatEars);
                view.Presentation.SampleForTests(.05f);
                AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
                var outgoing=Focus.Find("OutgoingCostume").GetComponent<Image>();
                if (outgoing.enabled) AssertBlack(outgoing);
            }
            MobileApplicationLifecycle.SetPlatformVisibility(false); Invoke(view.Presentation,"Update");
            AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            view.SetNavigationPresentation(false,false); AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
            view.SetNavigationPresentation(true,true);
            Assert.That(Focus.Find("OriginalInkBody").GetComponent<Image>().color,Is.EqualTo(Color.white));
            view.Select(InkSkinId.Gat); view.Presentation.Cancel();
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().color,Is.EqualTo(Color.white));
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void UndiscoveredRewardsShowOnlyQuestionsAndGenericHelp(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            foreach (var id in Enumerable.Range(1,InkSkinCatalog.Count-1).Select(i=>(InkSkinId)i).Where(InkSkinCatalog.IsConcealed))
            {
                view.Select(InkSkinId.Gat); view.Presentation.SampleForTests(.1f); view.Select(id);
                foreach (float time in new[] { 0f, .06f, .2f, .42f })
                {
                    view.Presentation.SampleForTests(time);
                    foreach (string layer in new[] { "OriginalInkBody", "Accessory", "OutgoingCostume" })
                    {
                        var image=Focus.Find(layer).GetComponent<Image>();
                        Assert.That(image.enabled,Is.False,layer); Assert.That(image.sprite,Is.Null,layer);
                    }
                }
                view.Presentation.Cancel();
                var question=Focus.Find("QuestionMark").GetComponent<Text>();
                Assert.That(question.enabled,Is.True); Assert.That(question.text,Is.EqualTo("?"));
                var card=view.CollectionContent.Find("SkinCard"+(int)id);
                foreach (var image in card.Find("PreviewViewport/Preview").GetComponentsInChildren<Image>())
                { Assert.That(image.enabled,Is.False); Assert.That(image.sprite,Is.Null); }
                question=card.Find("PreviewViewport/QuestionMark").GetComponent<Text>();
                Assert.That(question.enabled,Is.True); Assert.That(question.text,Is.EqualTo("?"));
                Assert.That(question.rectTransform.anchoredPosition,Is.EqualTo(Vector2.zero));
                Assert.That(card.Find("State").GetComponent<Text>().text,Is.Empty);
                foreach (string label in new[] { "SkinName", "Ability", "Achievement", "AchievementProgress" })
                    Assert.That(view.Paper.Find(label).GetComponent<Text>().text,Is.Empty,label);
                Assert.That(view.Paper.Find("Description").GetComponent<Text>().text,
                    Is.EqualTo(GameLocalization.Translate("숨겨진 행동으로 발견해 보세요.")));
                Assert.That(view.EquipButton.gameObject.activeSelf,Is.False);
                Assert.That(view.Paper.Find("AchievementTrack").gameObject.activeSelf,Is.False);
            }
        }
        [Test] public void SameSelectionUpdatesAfterUnlockAndAccountClear()
        {
            view.Select(InkSkinId.RedHeadband); view.Presentation.Cancel();
            AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
            var stats=SkinRunStats.Begin(false,InkSkinId.Ink); stats.drawnLandings=10;
            PermanentGrowthProfile.SettleRun("silhouette-unlock",10,10,0,10,true,stats);
            Invoke(view,"RefreshSelection"); view.Presentation.Cancel();
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().color,Is.EqualTo(Color.white));
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            Invoke(view,"RefreshSelection"); AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
            view.Select(InkSkinId.NightBat);
            Assert.That(Focus.Find("QuestionMark").GetComponent<Text>().enabled,Is.True);
            Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat),Is.True);
            Invoke(view,"RefreshSelection");
            Assert.That(Focus.Find("QuestionMark").GetComponent<Text>().enabled,Is.False);
            Assert.That(Focus.Find("OriginalInkBody").GetComponent<Image>().enabled,Is.True);
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().color,Is.EqualTo(Color.white));
            Assert.That(view.EquipButton.gameObject.activeSelf,Is.True);
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            Invoke(view,"RefreshSelection");
            Assert.That(Focus.Find("QuestionMark").GetComponent<Text>().enabled,Is.True);
            Assert.That(Focus.Find("OriginalInkBody").GetComponent<Image>().sprite,Is.Null);
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().sprite,Is.Null);
            Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().sprite,Is.Null);
        }
        [Test] public void AccountClearBeforeRefreshDoesNotCopyAnOldColouredOrdinaryCostume()
        {
            view.Select(InkSkinId.Gat); view.Presentation.Cancel();
            Assert.That(Focus.Find("Accessory").GetComponent<Image>().color,Is.EqualTo(Color.white));
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(),Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0),Is.True);
            view.Select(InkSkinId.RedHeadband);
            AssertBlack(Focus.Find("Accessory").GetComponent<Image>());
            Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().sprite,Is.Null);
        }
        [Test] public void RapidChangesReuseOneEffectAndRestoreNeutralPose()
        {
            int before = host.GetComponentsInChildren<Transform>(true).Length;
            for (int i=0; i<40; i++)
            {
                view.Select(i%2==0 ? InkSkinId.Gat : InkSkinId.Ink);
                view.EquipButton.onClick.Invoke(); view.Presentation.SampleForTests(.23f);
            }
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(before));
            Assert.That(host.GetComponentsInChildren<InkShowroomMotes>(true).Length, Is.EqualTo(1));
            view.Presentation.Cancel(); AssertStopped();
        }
        [TestCase(VfxQualityTier.Low)] [TestCase(VfxQualityTier.Medium)] [TestCase(VfxQualityTier.High)]
        public void ParticleBudgetMatchesQualityAndEffectsCannotBlockTouches(VfxQualityTier tier)
        {
            VfxQualityRuntime.SetTier(tier, VfxQualityChangeReason.DebugOverride);
            view.Select(InkSkinId.Gat); view.EquipButton.onClick.Invoke(); view.Presentation.SampleForTests(.2f);
            Assert.That(view.Presentation.ParticleCount, Is.EqualTo(VfxQualityRuntime.Profile.ScaleDecorativeCount(28, 8)));
            foreach (var graphic in view.Paper.Find("ShowroomStage/ShowroomInkEffect").GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            Assert.That(view.Presentation.ParticleCount, Is.LessThanOrEqualTo(28));
        }
        [Test] public void ReducedMotionKeepsPoseAndSuppressesRipplesAndParticles()
        {
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            view.Select(InkSkinId.Gat); Assert.That(view.Presentation.IsPlaying, Is.False);
            view.EquipButton.onClick.Invoke(); view.Presentation.SampleForTests(.3f);
            Assert.That(Focus.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Focus.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(view.Presentation.ParticleCount, Is.Zero);
            foreach (var ripple in view.GetComponentsInChildren<GrowthBloomArc>(true)) Assert.That(ripple.color.a, Is.Zero);
        }
        [Test] public void BackgroundAndInputTransitionCancelAndBlockNewSelections()
        {
            view.Select(InkSkinId.Gat); view.EquipButton.onClick.Invoke();
            MobileApplicationLifecycle.SetPlatformVisibility(false); Invoke(view.Presentation, "Update");
            view.StepSelection(1); Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat)); AssertStopped();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            view.SetNavigationPresentation(true, false); view.StepSelection(1);
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat)); AssertStopped();
        }
        [Test] public void HorizontalSwipeChangesOnePreviewButVerticalShortAndStaleDragsDoNot()
        {
            eventHost = new GameObject("ShowroomTestEvents", typeof(EventSystem));
            var swipe = view.GetComponentInChildren<InkShowroomSwipe>(true);
            var e = new PointerEventData(eventHost.GetComponent<EventSystem>()) { pointerId=1, pressPosition=Vector2.zero };
            swipe.OnBeginDrag(e); e.position=new Vector2(-Screen.width,0); swipe.OnEndDrag(e);
            Assert.That((int)view.Selected, Is.EqualTo(1));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            foreach(var delta in new[]{new Vector2(0,900),new Vector2(10,0)})
            { swipe.OnBeginDrag(e); e.position=delta; swipe.OnEndDrag(e); Assert.That((int)view.Selected, Is.EqualTo(1)); }
            swipe.OnBeginDrag(e); view.SetNavigationPresentation(false,false); view.SetNavigationPresentation(true,true);
            e.position=new Vector2(-Screen.width,0); swipe.OnEndDrag(e);
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
        }
        [Test] public void NavigationStopsAtEndsAndKeepsAllCardsInTheScrollCollection()
        {
            view.StepSelection(-1); Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
            Assert.That(view.Paper.Find("ShowroomStage/PreviousCostume").GetComponent<Button>().interactable, Is.False);
            view.Select((InkSkinId)(InkSkinCatalog.Count-1)); view.StepSelection(1);
            Assert.That((int)view.Selected, Is.EqualTo(InkSkinCatalog.Count-1));
            Assert.That(view.Paper.Find("ShowroomStage/NextCostume").GetComponent<Button>().interactable, Is.False);
            Assert.That(view.CollectionContent.Cast<Transform>().Count(t=>t.name.StartsWith("SkinCard") && t.gameObject.activeSelf), Is.EqualTo(InkSkinCatalog.Count));
        }
        [Test] public void ScrollPositionSurvivesRefreshAndAllViewFitsEveryCardWithoutLeakingSecrets()
        {
            Canvas.ForceUpdateCanvases();
            var scroll=view.CollectionScroll;
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(),Is.Not.Null);
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition=0;
            Vector2 offset=scroll.content.anchoredPosition;
            Invoke(view,"RefreshSelection");
            Assert.That(scroll.content.anchoredPosition,Is.EqualTo(offset));
            AssertInside(BoundsIn(scroll.viewport,(RectTransform)view.CollectionContent.Find("SkinCard"+(InkSkinCatalog.Count-1))),scroll.viewport.rect,0,"last card");
            view.ToggleCollection(); Canvas.ForceUpdateCanvases();
            Assert.That(view.CollectionExpanded,Is.True);
            Assert.That(Focus.parent.gameObject.activeSelf,Is.False);
            Assert.That(view.EquipButton.gameObject.activeSelf,Is.False);
            foreach(RectTransform card in view.CollectionContent)
                AssertInside(BoundsIn(scroll.viewport,card),scroll.viewport.rect,0,card.name);
            var hidden=view.CollectionContent.Find("SkinCard31/PreviewViewport/Preview/Accessory").GetComponent<Image>();
            Assert.That(hidden.sprite,Is.Null);
            view.CollectionContent.Find("SkinCard1").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.CollectionExpanded,Is.False); Assert.That(view.Selected,Is.EqualTo(InkSkinId.Gat));
            Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
            scroll.velocity=new Vector2(0,1000); view.SetNavigationPresentation(false,false);
            Assert.That(scroll.velocity,Is.EqualTo(Vector2.zero)); Assert.That(scroll.enabled,Is.False);
        }
        [Test] public void DragStartedBeforeTransitionCompletesIsIgnored()
        {
            eventHost = new GameObject("ShowroomTransitionEvents", typeof(EventSystem));
            var swipe = view.GetComponentInChildren<InkShowroomSwipe>(true);
            var e = new PointerEventData(eventHost.GetComponent<EventSystem>()) { pointerId=1, pressPosition=Vector2.zero };
            view.SetNavigationPresentation(true,false); swipe.OnBeginDrag(e);
            view.SetNavigationPresentation(true,true); e.position=new Vector2(-Screen.width,0); swipe.OnEndDrag(e);
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
        }
        [Test] public void HostHideAndResumeWithoutIntermediateFrameInvalidatesDragAndEffect()
        {
            view.Select(InkSkinId.Gat); view.EquipButton.onClick.Invoke();
            eventHost = new GameObject("ShowroomVisibilityEvents", typeof(EventSystem));
            var swipe = view.GetComponentInChildren<InkShowroomSwipe>(true);
            var e = new PointerEventData(eventHost.GetComponent<EventSystem>()) { pointerId=1, pressPosition=Vector2.zero };
            swipe.OnBeginDrag(e);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            e.position=new Vector2(-Screen.width,0); swipe.OnEndDrag(e); Invoke(view.Presentation,"Update");
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat)); AssertStopped();
        }
        [Test] public void LostOldPointerDoesNotBlockNewSwipeAfterResume()
        {
            eventHost = new GameObject("ShowroomLostPointerEvents", typeof(EventSystem));
            var swipe = view.GetComponentInChildren<InkShowroomSwipe>(true);
            var e = new PointerEventData(eventHost.GetComponent<EventSystem>()) { pointerId=1, pressPosition=Vector2.zero };
            swipe.OnBeginDrag(e);
            MobileApplicationLifecycle.SetPlatformVisibility(false); MobileApplicationLifecycle.SetPlatformVisibility(true);
            e.pointerId=2; swipe.OnBeginDrag(e); e.position=new Vector2(-Screen.width,0); swipe.OnEndDrag(e);
            Assert.That((int)view.Selected, Is.EqualTo(1));
        }
        [Test] public void EffectBoundsKeepRipplesAwayFromTitleAndCostumeName()
        {
            var clip=(RectTransform)view.Paper.Find("ShowroomStage/ShowroomInkEffect");
            var title=(RectTransform)view.Paper.Find("Title");
            var name=(RectTransform)view.Paper.Find("SkinName");
            Assert.That(clip.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(clip.TransformPoint(new Vector3(0,clip.rect.yMin)).y,
                Is.GreaterThan(name.TransformPoint(new Vector3(0,name.rect.yMax)).y));
            Assert.That(clip.TransformPoint(new Vector3(0,clip.rect.yMax)).y,
                Is.LessThan(title.TransformPoint(new Vector3(0,title.rect.yMin)).y));
        }
        [Test] public void EveryStationaryCostumeFitsBelowTitleWithoutCroppingItsTop()
        {
            var stage=(RectTransform)Focus.parent;
            var title=(RectTransform)view.Paper.Find("Title");
            Assert.That(stage.TransformPoint(new Vector3(0,stage.rect.yMax)).y,
                Is.LessThan(title.TransformPoint(new Vector3(0,title.rect.yMin)).y));
            for(int i=1;i<InkSkinCatalog.Count;i++)
            {
                var id=(InkSkinId)i; var sprite=InkSkinCatalog.Sprite(id);
                Assert.That(sprite, Is.Not.Null, id.ToString());
                float height=500*InkSkinCatalog.Width(id)*sprite.rect.height/sprite.rect.width;
                float top=InkSkinCatalog.Offset(id).y*500+height*.5f;
                Assert.That(top, Is.LessThanOrEqualTo(stage.rect.yMax-16), id.ToString());
            }
        }
        void AssertStopped()
        {
            Assert.That(view.Presentation.IsPlaying, Is.False); Assert.That(view.Presentation.ParticleCount, Is.Zero);
            Assert.That(Focus.localScale, Is.EqualTo(Vector3.one)); Assert.That(Focus.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(Focus.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(Focus.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().enabled, Is.False);
            Assert.That(Focus.Find("OutgoingCostume").GetComponent<Image>().sprite, Is.Null);
            var costume = Focus.Find("Accessory").GetComponent<Image>();
            Assert.That(costume.color.a, Is.EqualTo(1));
            Assert.That(costume.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }
        static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            foreach (var corner in corners)
            {
                Vector2 point = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min,point); max = Vector2.Max(max,point);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static void AssertBlack(Image image)
        {
            Assert.That(image.enabled,Is.True,image.name);
            Assert.That(image.sprite,Is.Not.Null,image.name);
            Assert.That(image.color.r,Is.Zero,image.name);
            Assert.That(image.color.g,Is.Zero,image.name);
            Assert.That(image.color.b,Is.Zero,image.name);
        }
        static void AssertInside(Rect content, Rect frame, float inset, string message)
        {
            Assert.That(content.xMin, Is.GreaterThanOrEqualTo(frame.xMin+inset-.01f),message);
            Assert.That(content.yMin, Is.GreaterThanOrEqualTo(frame.yMin+inset-.01f),message);
            Assert.That(content.xMax, Is.LessThanOrEqualTo(frame.xMax-inset+.01f),message);
            Assert.That(content.yMax, Is.LessThanOrEqualTo(frame.yMax-inset+.01f),message);
        }
        static void Invoke(object target,string name) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
