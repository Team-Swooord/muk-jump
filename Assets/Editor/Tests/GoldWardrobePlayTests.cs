using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed partial class GoldPackagePreviewPlayTests
    {
        [UnityTest, Timeout(150000)]
        public IEnumerator GoldWardrobeNameRecordsAndUnownedDebugWear()
        {
            yield return new EnterPlayMode();
            using (var fixture = new UiMemoryFixture())
            {
                SkinDebugSession.Reset();
                try
                {
                    yield return SceneManager.LoadSceneAsync("Main");
                    yield return WaitForGoldUi(() => LobbyScreenNavigator.Instance != null && LobbyScreenNavigator.Instance.CanStartGame &&
                        GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby && !GameManager.Instance.IsTransitioning &&
                        !StartupBrandSplash.IsBlockingInput && Object.FindObjectsByType<BrushTransitionView>(FindObjectsSortMode.None).All(x => !x.IsPlaying), "로비 준비");
                    Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                    Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                    var view = Object.FindAnyObjectByType<InkWardrobeView>();
                    yield return WaitForGoldUi(() => view != null && view.CanBrowse, "꾸미기 전환 완료");
                    Assert.That(view.CanBrowse, Is.True);
                    view.SelectGold(); view.Presentation.Cancel(); yield return Wait(.3f);
                    Text record = view.CollectionContent.Find("SkinCard63/BestHeight").GetComponent<Text>();
                    Assert.That(record.text, Is.EqualTo(GameLocalization.Translate("기록 없음")));
                    // 촬영용 메모리 기록이다. 사용자 저장소·실제 기기 기록을 생성하지 않는다.
                    PermanentGrowthProfile.SettleRun("gold-ui-fixture", 237, 237, 0, 30, true, SkinRunStats.Begin(false, InkSkinId.Ink, true));
                    string before = JsonUtility.ToJson(PermanentGrowthProfile.AchievementSnapshot());
                    Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                    yield return Wait(.3f);
                    yield return InGameFrame(() => Click(view.EquipButton)); yield return Wait(.4f);
                    Assert.That(InkPremiumAppearance.IsEquipped && SkinDebugSession.IsGoldEquipped, Is.True);
                    Assert.That(InkPremiumAppearance.IsOwned || InkPremiumAppearance.SuppressAds || InkPremiumShopView.IsOpen, Is.False);
                    Assert.That(fixture.Store.Purchases + fixture.Store.Restores, Is.Zero);
                    var name = view.Paper.Find("SkinName").GetComponent<Text>();
                    var lore = view.Paper.Find("ItemLore").GetComponent<Text>();
                    var gradient = name.GetComponent<GoldInkNameGradient>();
                    foreach (var dimensions in new[] { new Vector2Int(320,568), new Vector2Int(390,844) })
                    using (var size = new TemporaryGameViewSize(dimensions.x, dimensions.y))
                    {
                        yield return Wait(.35f);
                        foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                        {
                            GameLocalization.SetLanguage(language); yield return Wait(.35f);
                            yield return InGameFrame(() =>
                            {
                                Assert.That(name.text, Is.EqualTo(GameLocalization.Translate("금빛 먹")));
                                Assert.That(lore.text, Is.EqualTo(GameLocalization.Translate("햇살을 머금은 한 방울\n발끝마다 금빛 여운을 남겨요")));
                                Assert.That(record.text, Is.EqualTo(GameLocalization.Translate("최고 237m")));
                                Assert.That(name.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                                Assert.That(lore.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                                foreach (var text in new[] {name, lore, record})
                                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height+2), text.name+" "+language);
                                Assert.That(view.CollectionContent.Find("SkinCard63/State").GetComponent<Text>().text, Is.EqualTo(GameLocalization.Translate("착용 중")));
                                Assert.That(view.CollectionContent.Find("SkinCard0/State").GetComponent<Text>().text, Is.Not.EqualTo(GameLocalization.Translate("착용 중")));
                            });
                            yield return CapturePackage("wardrobe-"+dimensions.x+"-"+language);
                        }
                    }
                    GameLocalization.SetLanguage(GameLanguage.Korean);
                    using (var size = new TemporaryGameViewSize(1024, 1024))
                    {
                        var icon = new GameObject("TossGoldProductImage", typeof(Canvas));
                        var canvas = icon.GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
                        var surface = new GameObject("Paper", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                        surface.transform.SetParent(icon.transform, false);
                        surface.rectTransform.anchorMin = Vector2.zero; surface.rectTransform.anchorMax = Vector2.one;
                        surface.rectTransform.offsetMin = surface.rectTransform.offsetMax = Vector2.zero;
                        surface.color = InkPalette.Paper;
                        var source = view.Paper.Find("ShowroomStage/Preview/OriginalInkBody").GetComponent<Image>();
                        var body = Object.Instantiate(source, icon.transform);
                        body.rectTransform.anchorMin = body.rectTransform.anchorMax = body.rectTransform.pivot = Vector2.one * .5f;
                        body.rectTransform.anchoredPosition = new Vector2(0, 86);
                        body.rectTransform.sizeDelta = Vector2.one * 650;
                        body.rectTransform.localScale = Vector3.one;
                        var title = Object.Instantiate(name, icon.transform);
                        title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = Vector2.one * .5f;
                        title.rectTransform.anchoredPosition = new Vector2(0, -270);
                        title.rectTransform.sizeDelta = new Vector2(920, 140); title.fontSize = 100;
                        var benefit = Object.Instantiate(lore, icon.transform);
                        benefit.rectTransform.anchorMin = benefit.rectTransform.anchorMax = benefit.rectTransform.pivot = Vector2.one * .5f;
                        benefit.rectTransform.anchoredPosition = new Vector2(0, -384);
                        benefit.rectTransform.sizeDelta = new Vector2(920, 100); benefit.fontSize = 42;
                        InkLocalizedText.SetSource(benefit, "모든 광고 제거 · 영구 보유");
                        try
                        {
                            yield return Wait(.4f);
                            yield return InGameFrame(() =>
                            { Assert.That(Screen.width, Is.EqualTo(1024)); Assert.That(Screen.height, Is.EqualTo(1024)); });
                            yield return CapturePackage("toss-gold-product-1024");
                        }
                        finally { Object.Destroy(icon); }
                        yield return null;
                    }
                    float phase = gradient.Phase; yield return Wait(.25f);
                    Assert.That(gradient.Phase, Is.Not.EqualTo(phase));
                    LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.2f);
                    Assert.That(gradient.Phase, Is.Zero);
                    LobbySettingsProfile.SetReducedMotionEnabled(false); yield return Wait(.2f);
                    MobileApplicationLifecycle.SetPlatformVisibility(false); phase = gradient.Phase; yield return Wait(.2f);
                    Assert.That(gradient.Phase, Is.EqualTo(phase)); MobileApplicationLifecycle.SetPlatformVisibility(true);
                    view.Select(InkSkinId.Ink); yield return Wait(.2f);
                    Assert.That(gradient.enabled, Is.False, "일반 의상 이름에는 금빛이 남지 않음");
                    view.SelectGold(); yield return Wait(.2f);
                    Assert.That(gradient.enabled, Is.True);
                    LobbyScreenNavigator.Instance.ReturnToLobby();
                    yield return WaitForGoldUi(() => LobbyScreenNavigator.Instance.CanStartGame, "로비 복귀");
                    GameManager.Instance.StartGameFromMenu();
                    yield return WaitForGoldUi(() => GameManager.Instance.State == GameState.Playing, "게임 시작");
                    yield return null;
                    var manager = GameManager.Instance;
                    Assert.That(manager.State, Is.EqualTo(GameState.Playing));
                    Assert.That(RunGrowthController.Instance.GoldSnapshot && RunGrowthController.Instance.DebugGoldSnapshot, Is.True);
                    Assert.That(InkPremiumAppearance.IsGoldInRun, Is.True);
                    Assert.That(ScoreManager.Instance.RecordsAllowed, Is.False);
                    Assert.That(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                        .Any(p => p.GetComponentsInChildren<SpriteRenderer>().Any(r => GoldInkVisual.IsWorld(r.sharedMaterial))), Is.True);
                    using (var size = new TemporaryGameViewSize(390,844))
                    { yield return Wait(.3f); yield return CapturePackage("debug-gold-playing"); }
                    typeof(ScoreManager).GetProperty("Height").SetValue(ScoreManager.Instance, 9999);
                    typeof(GameManager).GetMethod("CommitCurrentBestBeforeGameOver", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                    typeof(GameManager).GetMethod("EnterGameOver", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                    yield return Wait(.5f);
                    Assert.That(JsonUtility.ToJson(PermanentGrowthProfile.AchievementSnapshot()), Is.EqualTo(before));
                    Assert.That(ScoreManager.Instance.Best, Is.Zero);
                    SkinDebugSession.Reset();
                    Assert.That(InkPremiumAppearance.IsGoldInRun, Is.False, "이전 판의 시험 외형이 세션을 넘지 않음");
                    Assert.That(fixture.Store.Purchases + fixture.Store.Restores, Is.Zero);
                }
                finally { SkinDebugSession.Reset(); }
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator WaitForGoldUi(Func<bool> ready, string context)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(ready(), Is.True, context);
        }
    }
}
