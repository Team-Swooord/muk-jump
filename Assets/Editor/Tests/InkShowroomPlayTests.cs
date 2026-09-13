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
    public sealed class InkShowroomPlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SpecialSkillCardsRenderDistinctlyWithoutBlockingBrowse()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground; Application.runInBackground = true;
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
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return WaitForWardrobe();
                var view = Object.FindAnyObjectByType<InkWardrobeView>();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    foreach (var id in new[] { InkSkinId.Ink, InkSkinId.Gat, InkSkinId.SpiritHalo, InkSkinId.PhoenixCrown, InkSkinId.WindFan })
                    {
                        view.Select(id); yield return Wait(.6f); Canvas.ForceUpdateCanvases();
                        var stage = view.Paper.Find("ShowroomStage");
                        foreach (string path in new[] { "PreviewBackdrop/FocusPaperWash", "PreviewBackdrop/FocusBrushRing", "InkStageFloor" })
                        {
                            var image = stage.Find(path).GetComponent<UnityEngine.UI.Image>();
                            Assert.That(image.enabled && !image.canvasRenderer.cull, Is.True, path);
                            Assert.That(image.raycastTarget, Is.False, path);
                        }
                        Assert.That(view.Paper.Find("CollectionGrouping"), Is.Null);
                        foreach (string surface in new[] { "SpecialSkillPaper", "SpecialSkillHeader", "SpecialSkillSeal" })
                            Assert.That(view.Paper.Find(surface).gameObject.activeInHierarchy, Is.EqualTo(InkSkinCatalog.HasAbility(id)), surface);
                        if (id == InkSkinId.Ink)
                        {
                            var requirement = view.Paper.Find("Achievement").GetComponent<UnityEngine.UI.Text>();
                            Assert.That(requirement.text, Is.Empty);
                            Assert.That(requirement.gameObject.activeSelf, Is.False);
                        }
                        foreach (string label in new[] { "Title", "SkinName", "ItemLore", "Ability", "Description" })
                        {
                            var text = view.Paper.Find(label).GetComponent<UnityEngine.UI.Text>();
                            if (!text.gameObject.activeSelf) continue;
                            Assert.That(text.canvasRenderer.cull, Is.False, label);
                            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), label);
                            // 높이 수치만 맞고 실제 마지막 글자가 잘리는 CJK 경계도 검사한다.
                            if (label == "Description")
                            {
                                var complete = new TextGenerator();
                                var fullSettings = text.GetGenerationSettings(text.rectTransform.rect.size);
                                fullSettings.verticalOverflow = VerticalWrapMode.Overflow;
                                complete.PopulateWithErrors(text.text, fullSettings, text.gameObject);
                                Assert.That(text.cachedTextGenerator.verts.Count, Is.EqualTo(complete.verts.Count), language + "/" + id + "/설명 끝 글자");
                            }
                        }
                        yield return Capture("special-skill-2026-09-13-" + language + "-" + id);
                    }
                }
                var next = InkSkinCatalog.BrowseAt(InkSkinCatalog.BrowseIndex(view.Selected) + 1);
                view.StepSelection(1); Assert.That(view.Selected, Is.EqualTo(next));
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            }
            finally
            {
                SkinDebugSession.Reset(); Application.runInBackground = oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator RefittedAccessoriesAndFootwearRenderInWardrobeAndUnlock()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(SkinDebugSession.SetAllUnlocked(true),Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return WaitForWardrobe();
                var view=Object.FindAnyObjectByType<InkWardrobeView>(); GameLocalization.SetLanguage(GameLanguage.Korean);
                foreach(var id in new[]{InkSkinId.RedHeadband,InkSkinId.Gat,InkSkinId.FoxMask,InkSkinId.InkGhost,InkSkinId.CloudCap,InkSkinId.Astronaut,InkSkinId.CraneWings,InkSkinId.ButterflyWings,InkSkinId.RedShoes,InkSkinId.CloudBoots})
                {
                    view.Select(id); yield return Wait(.55f); Canvas.ForceUpdateCanvases();
                    var art=view.Paper.Find("ShowroomStage/Preview/Accessory").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(art.enabled,Is.True); Assert.That(art.canvasRenderer.cull,Is.False);
                    if(InkSkinCatalog.IsFootwear(id))
                    {
                        Assert.That(art,Is.InstanceOf<InkAccessoryImage>());
                        Assert.That(art.canvasRenderer.GetMesh().vertexCount,Is.EqualTo(8));
                    }
                    yield return Capture("fitted-"+id);
                }
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); view.ToggleCollection(); yield return Wait(.3f);
                    Assert.That(view.CollectionExpanded,Is.True);
                    foreach(var graphic in view.CollectionContent.GetComponentsInChildren<UnityEngine.UI.Image>())
                        if(graphic.enabled && graphic.sprite!=null && !IsClippedCollectionCard(view,graphic.transform))
                            Assert.That(graphic.canvasRenderer.cull,Is.False,graphic.name);
                    yield return Capture("fitted-all-"+language);
                    view.CollectionContent.Find("SkinCard39").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(view.Selected,Is.EqualTo(InkSkinId.CloudBoots)); Assert.That(view.CollectionExpanded,Is.False);
                }
                view.BackButton.onClick.Invoke(); yield return Wait(2);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                SkinUnlockSceneController.PreparePreview((1UL<<(int)InkSkinId.RedHeadband)|(1UL<<(int)InkSkinId.CloudBoots),PermanentGrowthProfile.SkinAccountToken);
                yield return SceneManager.LoadSceneAsync(SkinUnlockSceneController.SceneName);
                var unlock=Object.FindAnyObjectByType<InkSkinUnlockView>();
                float revealDeadline=Time.realtimeSinceStartup+8;
                while(!unlock.IsOpen && Time.realtimeSinceStartup<revealDeadline) yield return null;
                Assert.That(unlock.IsOpen,Is.True);
                yield return Wait(1.5f); yield return Capture("fitted-unlock-headband");
                unlock.Advance(); yield return Wait(.2f);
                // 낮은 에디터 프레임에서는 첫 누름이 연출 건너뛰기만 완료한다.
                if(unlock.Current==InkSkinId.RedHeadband) unlock.Advance();
                yield return Wait(1.5f);
                Assert.That(unlock.Current,Is.EqualTo(InkSkinId.CloudBoots)); yield return Capture("fitted-unlock-boots");
                unlock.AcknowledgeAll();
            }
            finally
            {
                SkinDebugSession.Reset(); Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator WideCollectionScrollsWithoutEquippingAndShowsAllInThreeLanguages()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var stats=SkinRunStats.Begin(false,InkSkinId.Ink); stats.items=30; stats.maxLivingPlayers=3;
                Assert.That(PermanentGrowthProfile.SettleRun("wide-collection",2000,2000,0,60,true,stats).Accepted,Is.True);
                Assert.That(ScoreManager.Instance.TryCommitBestCandidate(2000),Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return WaitForWardrobe();
                var view=Object.FindAnyObjectByType<InkWardrobeView>(); var scroll=view.CollectionScroll;
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); view.Select(InkSkinId.SpiritHalo); yield return WaitForCarousel(view);
                    foreach(string name in new[]{"SkinName","Ability","Description","Achievement"})
                    {
                        var label=view.Paper.Find(name).GetComponent<UnityEngine.UI.Text>();
                        Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.1f),name+": "+label.text);
                        Assert.That(label.canvasRenderer.cull,Is.False,name);
                    }
                    yield return Capture("wardrobe-wide-"+language);
                    view.ToggleCollection(); yield return Wait(.2f);
                    Assert.That(view.CollectionExpanded,Is.True);
                    foreach(var image in view.CollectionContent.GetComponentsInChildren<UnityEngine.UI.Image>())
                    {
                        if(!image.enabled || image.sprite==null || IsClippedCollectionCard(view,image.transform)) continue;
                        Assert.That(image.canvasRenderer.cull,Is.False,image.name);
                    }
                    foreach(var label in view.CollectionContent.GetComponentsInChildren<UnityEngine.UI.Text>())
                    {
                        if(!label.enabled || !label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text)) continue;
                        Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.1f),language+"/"+label.name+"/"+label.text);
                        if(!IsClippedCollectionCard(view,label.transform))
                        {
                            var complete=new TextGenerator();
                            var fullSettings=label.GetGenerationSettings(label.rectTransform.rect.size);
                            fullSettings.verticalOverflow=VerticalWrapMode.Overflow;
                            complete.PopulateWithErrors(label.text,fullSettings,label.gameObject);
                            Assert.That(label.cachedTextGenerator.verts.Count,Is.EqualTo(complete.verts.Count),language+"/"+label.name+"/"+label.text+" 실제 글자");
                        }
                    }
                    yield return Capture("wardrobe-all-"+language);
                    view.CollectionContent.Find("SkinCard19").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(view.CollectionExpanded,Is.False); Assert.That(view.Selected,Is.EqualTo(InkSkinId.TurtleShell));
                }
                view.Select(InkSkinId.Ink); yield return Wait(.7f);
                var events=UnityEngine.EventSystems.EventSystem.current;
                var e=new UnityEngine.EventSystems.PointerEventData(events) { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left, pointerId=91 };
                e.position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.position);
                scroll.OnInitializePotentialDrag(e); scroll.OnBeginDrag(e);
                for(int frame=0;frame<8;frame++)
                {
                    e.position+=new Vector2(-24,0); scroll.OnDrag(e); yield return null;
                }
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(-100));
                Assert.That(view.Selected,Is.EqualTo(InkSkinId.Ink)); Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
                float releasedX=scroll.content.anchoredPosition.x;
                scroll.OnEndDrag(e); yield return Wait(.12f);
                Assert.That(scroll.content.anchoredPosition.x,Is.LessThan(releasedX-5),"손을 놓은 뒤 관성이 이어진다");
                // 흐르는 도중 다시 잡으면 멈추고, 두 번째 손가락은 위치를 가로채지 않는다.
                scroll.OnInitializePotentialDrag(e); scroll.OnBeginDrag(e);
                var other=new UnityEngine.EventSystems.PointerEventData(events) { pointerId=92, position=e.position+Vector2.right*300 };
                Vector2 held=scroll.content.anchoredPosition;
                scroll.OnBeginDrag(other); scroll.OnDrag(other); scroll.OnEndDrag(other);
                view.CollectionContent.Find("SkinCard1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(view.Selected,Is.EqualTo(InkSkinId.Ink),"두 번째 손가락의 카드 탭은 진행 중인 드래그를 가로채지 않는다");
                yield return Wait(.1f);
                Assert.That(Vector2.Distance(held,scroll.content.anchoredPosition),Is.LessThan(.1f));
                scroll.OnEndDrag(e); yield return Wait(1.2f);
                var carousel=(InkWardrobeScroll)scroll;
                Assert.That(carousel.IsSettling,Is.False);
                Assert.That(view.Selected,Is.Not.EqualTo(InkSkinId.Ink));
                Assert.That(scroll.content.anchoredPosition.x,Is.EqualTo(-InkSkinCatalog.BrowseIndex(view.Selected)*carousel.CardStride).Within(.1f));
                Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
                Vector2 offset=scroll.content.anchoredPosition; yield return Wait(.4f);
                Assert.That(Vector2.Distance(offset,scroll.content.anchoredPosition),Is.LessThan(.1f));
                yield return Capture("wardrobe-carousel-settled");
                foreach(int edge in new[]{0,InkSkinCatalog.Count-1})
                {
                    view.Select(InkSkinCatalog.BrowseAt(edge)); yield return WaitForCarousel(view);
                    e.position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.position);
                    scroll.OnInitializePotentialDrag(e); scroll.OnBeginDrag(e);
                    for(int frame=0;frame<5;frame++)
                    { e.position+=Vector2.right*(edge==0 ? 25 : -25); scroll.OnDrag(e); yield return null; }
                    scroll.OnEndDrag(e); yield return WaitForCarousel(view);
                    Assert.That(view.Selected,Is.EqualTo(InkSkinCatalog.BrowseAt(edge)));
                    Assert.That(scroll.content.anchoredPosition.x,Is.EqualTo(-edge*carousel.CardStride).Within(.1f),"양 끝을 넘겨 밀어도 마지막 카드로 복귀한다");
                }
                view.ToggleCollection(); scroll.verticalNormalizedPosition=0; yield return Wait(.2f);
                yield return Capture("wardrobe-scroll-bottom");
                var last=view.CollectionContent.GetChild(InkSkinCatalog.Count-1);
                Assert.That(last.Find("PreviewViewport/QuestionMark").GetComponent<UnityEngine.UI.Text>().canvasRenderer.cull,Is.False);
                scroll.velocity=new Vector2(0,200); MobileApplicationLifecycle.SetPlatformVisibility(false); yield return null;
                Assert.That(scroll.velocity,Is.EqualTo(Vector2.zero)); Assert.That(scroll.enabled,Is.False);
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.1f);
                Assert.That(scroll.enabled,Is.True,$"active={MobileApplicationLifecycle.IsApplicationActive}, browse={view.CanBrowse}, open={view.IsOpen}");
            }
            finally
            {
                Application.runInBackground=oldBackground; MobileApplicationLifecycle.SetPlatformVisibility(true);
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator BrushProgressRendersAndSettlesAcrossRefreshesAndDisclosureChanges()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1f);
                Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Wait(3f);
                var view=Object.FindAnyObjectByType<InkWardrobeView>();
                var track=view.Paper.Find("AchievementTrack");
                var fill=track.Find("Progress").GetComponent<UnityEngine.UI.Image>();
                var seal=track.Find("CompletionSeal");
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language);
                    Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0),Is.True);
                    view.Select(InkSkinId.Gat); yield return Wait(.5f);
                    Assert.That(fill.fillAmount,Is.Zero); Assert.That(seal.gameObject.activeSelf,Is.False);
                    Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(43),Is.True);
                    // 같은 항목 갱신으로 시작하고 매 0.25초 갱신 뒤에도 목표값까지 도착해야 한다.
                    view.Select(InkSkinId.Gat);
                    Assert.That(fill.fillAmount,Is.LessThan(.43f));
                    yield return Wait(.75f);
                    Assert.That(fill.fillAmount,Is.EqualTo(.43f).Within(.001f));
                    Assert.That(seal.gameObject.activeSelf,Is.False);
                    yield return Capture("progress-partial-"+language);
                    Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(100),Is.True);
                    view.Select(InkSkinId.Gat); Assert.That(seal.gameObject.activeSelf,Is.False);
                    yield return Wait(.75f); Canvas.ForceUpdateCanvases();
                    Assert.That(fill.fillAmount,Is.EqualTo(1f));
                    Assert.That(seal.gameObject.activeSelf,Is.True);
                    foreach(var graphic in seal.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    {
                        Assert.That(graphic.canvasRenderer.cull,Is.False,graphic.name);
                        Assert.That(graphic.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),graphic.name);
                    }
                    yield return Capture("progress-complete-"+language);
                }
                Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0),Is.True);
                view.Select(InkSkinId.Gat);
                Assert.That(fill.fillAmount,Is.Zero); Assert.That(seal.gameObject.activeSelf,Is.False);
                Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(100),Is.True);
                view.Select(InkSkinId.Gat); yield return Wait(.1f);
                Assert.That(fill.fillAmount,Is.InRange(.001f,.999f));
                view.Select(InkSkinId.NightBat);
                Assert.That(track.gameObject.activeSelf,Is.False); Assert.That(fill.fillAmount,Is.Zero);
                Assert.That(seal.gameObject.activeSelf,Is.False);
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                view.Select(InkSkinId.Gat);
                Assert.That(fill.fillAmount,Is.EqualTo(1f)); Assert.That(seal.gameObject.activeSelf,Is.True);
                view.Select(InkSkinId.Ink); Assert.That(track.gameObject.activeSelf,Is.False);
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                view.Select(InkSkinId.Gat); yield return Wait(.1f);
                view.SetNavigationPresentation(false,false);
                Assert.That(fill.fillAmount,Is.EqualTo(1f));
                view.SetNavigationPresentation(true,true); yield return Wait(3f);
                Assert.That(view.Selected,Is.EqualTo(InkSkinId.Ink));
                Assert.That(track.gameObject.activeSelf,Is.False);
                view.Select(InkSkinId.Gat); yield return Wait(.1f);
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.06f);
                Assert.That(fill.fillAmount,Is.EqualTo(1f));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                view.Select(InkSkinId.Ink); view.Select(InkSkinId.Gat); yield return Wait(.06f);
                Assert.That(fill.fillAmount,Is.InRange(.001f,.999f));
                // 숨김 중 Update가 한 번도 없어도 복귀 시 이전 붓질을 이어가지 않는다.
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.06f);
                Assert.That(fill.fillAmount,Is.EqualTo(1f));
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator LockedAndHiddenPreviewsRenderWithoutRevealingArtwork()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1f);
                Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Wait(3f);
                var view=Object.FindAnyObjectByType<InkWardrobeView>();
                var focus=view.Paper.Find("ShowroomStage/Preview");
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    view.Select(InkSkinId.PhoenixCrown); yield return Wait(.6f);
                    Assert.That(focus.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(Color.black));
                    Assert.That(focus.Find("Accessory").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(Color.black));
                    yield return Capture("locked-silhouette-"+language);
                    view.Select(InkSkinId.NightBat); yield return Wait(.6f); Canvas.ForceUpdateCanvases();
                    foreach (string layer in new[] { "OriginalInkBody", "Accessory", "OutgoingCostume" })
                        Assert.That(focus.Find(layer).GetComponent<UnityEngine.UI.Image>().enabled,Is.False,layer);
                    foreach (var text in view.Paper.GetComponentsInChildren<UnityEngine.UI.Text>())
                    {
                        if (!text.enabled || string.IsNullOrEmpty(text.text)) continue;
                        Assert.That(text.canvasRenderer.cull,Is.False,text.name);
                        Assert.That(text.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),text.name);
                    }
                    yield return Capture("hidden-question-"+language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(PermanentGrowthProfile.GrantSkinSecret(InkSkinId.NightBat),Is.True);
                yield return Wait(.5f);
                Assert.That(focus.Find("QuestionMark").GetComponent<UnityEngine.UI.Text>().enabled,Is.False);
                Assert.That(focus.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(Color.white));
                Assert.That(focus.Find("Accessory").GetComponent<UnityEngine.UI.Image>().enabled,Is.True);
                yield return Capture("discovered-colour-Korean");
            }
            finally
            {
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator CostumeCardsRenderInsideTheirFramesInAllLocales()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1f);
                Assert.That(GameManager.Instance.State,Is.EqualTo(GameState.Lobby));
                Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                PermanentGrowthProfile.SettleRun("card-capture",777,777,0,60,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
                Assert.That(ScoreManager.Instance.TryCommitBestCandidate(777),Is.True);
                foreach (var secret in new[] { InkSkinId.NightBat, InkSkinId.Astronaut, InkSkinId.OrigamiWings })
                    Assert.That(PermanentGrowthProfile.GrantSkinSecret(secret),Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Wait(3f);
                var view=Object.FindAnyObjectByType<InkWardrobeView>();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language);
                    foreach (var id in new[] { InkSkinId.Gat, InkSkinId.PhoenixCrown, InkSkinId.OrigamiWings })
                    {
                        view.Select(id); yield return Wait(.6f); Canvas.ForceUpdateCanvases();
                        foreach (var text in view.Paper.GetComponentsInChildren<UnityEngine.UI.Text>())
                        {
                            if (!text.enabled || string.IsNullOrEmpty(text.text) || IsClippedCollectionCard(view,text.transform)) continue;
                            Assert.That(text.canvasRenderer.cull,Is.False,text.name+": "+text.text);
                            Assert.That(text.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),text.name+": "+text.text);
                        }
                        foreach (var graphic in view.Paper.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                        {
                            if (!graphic.enabled || IsClippedCollectionCard(view,graphic.transform) || !graphic.transform.parent.name.StartsWith("Preview") ||
                                graphic.transform.parent.parent.name!="PreviewViewport") continue;
                            Assert.That(graphic.canvasRenderer.cull,Is.False,graphic.name);
                            Assert.That(graphic.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),graphic.name);
                        }
                        yield return Capture("card-strip-"+language+"-"+id);
                    }
                    view.Select(InkSkinId.Gat); yield return Wait(.6f);
                    settings.ThrowOnSave=true; view.EquipButton.onClick.Invoke(); Canvas.ForceUpdateCanvases();
                    var status=view.Paper.Find("SaveStatus").GetComponent<UnityEngine.UI.Text>();
                    Assert.That(status.text,Is.Not.Empty);
                    Assert.That(status.canvasRenderer.cull,Is.False);
                    Assert.That(status.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0));
                    yield return Capture("card-strip-save-failure-"+language);
                    settings.ThrowOnSave=false;
                }
                view.Select(InkSkinId.Gat); yield return Wait(.6f);
                view.CollectionContent.Find("SkinCard0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(view.Selected,Is.EqualTo(InkSkinId.Ink));
                Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(InkSkinId.Ink));
            }
            finally
            {
                settings.ThrowOnSave=false;
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator HeadbandsSitOnTheForeheadInShowroomAndActualGameplay()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground=Application.runInBackground; Application.runInBackground=true;
            var settings=new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1f);
                var stats=SkinRunStats.Begin(true,InkSkinId.CatEars);
                stats.drawnLandings=10; stats.items=stats.inkDrops=1;
                PermanentGrowthProfile.SettleRun("headband-fit",100,100,0,60,true,stats);
                PermanentGrowthProfile.SettleRun("headband-fit-secret",100,100,0,60,true,SkinRunStats.Begin(true,InkSkinId.CatEars));
                foreach(var id in new[]{InkSkinId.RedHeadband,InkSkinId.CatEars,InkSkinId.ClockworkCat})
                {
                    yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(.8f);
                    var manager=GameManager.Instance;
                    Assert.That(manager.State,Is.EqualTo(GameState.Lobby));
                    Assert.That(MukJumpAccountRuntime.Instance==null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated,Is.True);
                    Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True); yield return Wait(3f);
                    var view=Object.FindAnyObjectByType<InkWardrobeView>();
                    view.Select(id); yield return Wait(.6f); yield return Capture("headband-preview-"+id);
                    view.EquipButton.onClick.Invoke(); Assert.That(InkSkinPreferences.Equipped,Is.EqualTo(id));
                    view.BackButton.onClick.Invoke(); yield return Wait(3f);
                    manager.StartGameFromMenu();
                    float deadline=Time.realtimeSinceStartup+8;
                    while(!manager.IsGameplayTicking && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(manager.IsGameplayTicking,Is.True);
                    var debugControls=Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                    if(debugControls!=null) debugControls.gameObject.SetActive(false);
                    yield return Wait(.5f);
                    Assert.That(RunGrowthController.Instance.SkinSnapshot,Is.EqualTo(id));
                    var body=manager.HighestLivingPlayer.GetComponent<SpriteRenderer>();
                    var accessory=body.GetComponent<MukJump.Player.InkSkinView>().AccessoryRenderer;
                    Assert.That(accessory.enabled && accessory.isVisible,Is.True);
                    Assert.That(accessory.sprite,Is.SameAs(InkSkinCatalog.Sprite(id)));
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(body.sprite),Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
                    yield return Capture("headband-player-"+id);
                }
            }
            finally
            {
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ShowroomRendersBrowsingEquipAndCloseInRealMainWithoutServerWrites()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1.2f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.True);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                Assert.That(lobby.WardrobeButton.transform.position.y, Is.GreaterThan(lobby.OptionsButton.transform.position.y));
                yield return Capture("lobby");
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                yield return WaitForWardrobe();
                var view = Object.FindAnyObjectByType<InkWardrobeView>(); Assert.That(view.IsOpen, Is.True);
                var wardrobeCanvas=view.Paper.GetComponentInParent<Canvas>();
                Assert.That(wardrobeCanvas.GetComponentsInChildren<HanjiScrollFrame>(true),Is.Empty);
                Assert.That(LobbyScreenNavigator.Instance.CanStartGame,Is.False);
                Assert.That(LobbyScreenNavigator.Instance.CanShowTopBanner,Is.False);
                Assert.That(wardrobeCanvas.transform.Find("LobbyBackgroundInputBlocker")
                    .GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(Color.clear));
                var measured = SkinRunStats.Begin(true, InkSkinId.CatEars);
                for (int i=0;i<100;i++) PermanentGrowthProfile.SettleRun("showroom-capture-"+i,200,200,0,120,true,measured);
                PermanentGrowthProfile.SettleRun("showroom-peak",2000,2000,0,120,true,measured);
                Assert.That(ScoreManager.Instance.TryCommitBestCandidate(2000), Is.True);
                foreach (var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language); view.Select(InkSkinId.Gat);
                    Assert.That(lobby.WardrobeButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                        Is.EqualTo(GameLocalization.Translate("꾸미기")));
                    Assert.That(view.Paper.Find("Title").GetComponent<UnityEngine.UI.Text>().text,
                        Is.EqualTo(GameLocalization.Translate("꾸미기")));
                    yield return Wait(.6f); yield return Capture("showroom-"+language);
                }
                GameLocalization.SetLanguage(GameLanguage.Korean);
                foreach (var id in new[]{InkSkinId.InkMecha,InkSkinId.ClockworkCat,InkSkinId.CloudDragon})
                {
                    view.Select(id); yield return Wait(.6f); yield return Capture("showroom-"+id);
                    var body=view.Paper.Find("ShowroomStage/Preview/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                    Assert.That(body.enabled, Is.True);
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(body.sprite), Is.EqualTo("Assets/Art/Character/Player/muk_spritesheet.png"));
                }
                foreach(var tall in new[]{InkSkinId.PhoenixCrown,InkSkinId.InkGhost})
                {
                    Assert.That(InkSkinAchievements.IsUnlocked(tall), Is.True);
                    view.Select(tall); yield return Wait(.6f); yield return Capture("tall-rest-"+tall);
                    view.StepSelection(1); yield return Wait(.06f); yield return Capture("tall-change-"+tall);
                    yield return Wait(.6f);
                }
                view.Select(InkSkinId.Gat); yield return Wait(.6f);
                view.Paper.Find("ShowroomStage/NextCostume").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(view.Selected, Is.EqualTo(InkSkinId.SpiderMask));
                Assert.That(view.Presentation.IsPlaying, Is.True);
                yield return Wait(.06f); yield return Capture("arrow-change-start");
                yield return Wait(.07f); yield return Capture("arrow-change-settle");
                yield return Wait(.6f);
                Assert.That(view.Presentation.IsPlaying, Is.False);
                Assert.That(view.Paper.Find("ShowroomStage/Preview/OutgoingCostume").GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                yield return Capture("arrow-change-complete");
                view.Select(InkSkinId.Gat); yield return Wait(.6f);
                view.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
                Assert.That(view.Presentation.IsEquipping, Is.True);
                yield return Wait(.17f);
                Assert.That(view.Presentation.IsEquipping, Is.True);
                Canvas.ForceUpdateCanvases();
                var fxRoot = view.Paper.Find("ShowroomStage/ShowroomInkEffect");
                Assert.That(fxRoot.gameObject.activeInHierarchy, Is.True);
                foreach (var graphic in fxRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                {
                    Assert.That(graphic.canvasRenderer.cull, Is.False, graphic.name);
                    Assert.That(graphic.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.9f), graphic.name);
                    var mesh = graphic.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0), graphic.name);
                    Debug.Log("[ShowroomRender] " + graphic.name + " vertices=" + mesh.vertexCount + " alpha=" + graphic.color.a);
                }
                yield return Capture("equip-ripple");
                yield return Wait(.18f); yield return Capture("equip-hanji");
                yield return Wait(1.4f); Assert.That(view.Presentation.IsPlaying, Is.False);
                yield return Capture("equipped");
                view.Select(InkSkinId.SpiderMask); view.EquipButton.onClick.Invoke();
                view.BackButton.onClick.Invoke(); yield return Wait(3f);
                Assert.That(view.IsOpen, Is.False); Assert.That(view.Presentation.IsPlaying, Is.False);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True); yield return WaitForWardrobe();
                Assert.That(view.Selected, Is.EqualTo(InkSkinId.SpiderMask));
                Assert.That(view.Presentation.ParticleCount, Is.Zero);
            }
            finally
            {
                Application.runInBackground=oldBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static bool IsClippedCollectionCard(InkWardrobeView view,Transform child)
        {
            var card=child;
            while(card!=null && card.parent!=view.CollectionContent) card=card.parent;
            if(card==null) return false;
            var corners=new Vector3[4]; ((RectTransform)card).GetWorldCorners(corners);
            var viewport=view.CollectionScroll.viewport;
            return corners.Any(c=>!viewport.rect.Contains(viewport.InverseTransformPoint(c)));
        }
        static IEnumerator WaitForCarousel(InkWardrobeView view)
        {
            var carousel=(InkWardrobeScroll)view.CollectionScroll;
            float deadline=Time.realtimeSinceStartup+3;
            while(carousel.IsSettling && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(carousel.IsSettling,Is.False,"카드 중앙 안착 완료");
            yield return null;
        }
        static IEnumerator WaitForWardrobe()
        {
            float deadline=Time.realtimeSinceStartup+10f;
            while(Time.realtimeSinceStartup<deadline)
            {
                var view=Object.FindAnyObjectByType<InkWardrobeView>();
                if(view!=null && view.CanBrowse) yield break;
                yield return null;
            }
            Assert.Fail("꾸미기 전체 화면 전환이 완료되지 않았습니다.");
        }
        static IEnumerator Capture(string name)
        {
            string path=JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(File.Exists(path), Is.True);
            string destination=Path.GetFullPath("output/qa/ink-showroom/"+name+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path,destination,true); File.Delete(path);
        }
        static IEnumerator Wait(float duration)
        {
            float deadline=Time.realtimeSinceStartup+duration;
            while(Time.realtimeSinceStartup<deadline) yield return null;
        }
    }
}
