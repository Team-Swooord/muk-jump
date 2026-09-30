using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed partial class GoldPackagePreviewPlayTests
    {
        [Test]
        public void RecordedAtlasHasAllFramesWithoutCrossingCellBoundaries()
        {
            var texture = Resources.Load<Texture2D>(GoldInkRecordedPreview.ResourcePath);
            Assert.That(texture, Is.Not.Null); Assert.That(texture.isReadable, Is.False);
            Assert.That(texture.width, Is.EqualTo(1152)); Assert.That(texture.height, Is.EqualTo(2048));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            for (int i = 0; i < GoldInkRecordedPreview.FrameCount; i++)
            {
                Rect uv = GoldInkRecordedPreview.FrameUv(i);
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(1)); Assert.That(uv.yMax, Is.LessThanOrEqualTo(1));
            }
            Resources.UnloadAsset(texture);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BenefitsAndRecordedPreviewInThreeLanguages()
        {
            yield return new EnterPlayMode();
            using (var fixture = new UiMemoryFixture())
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var shop = Object.FindAnyObjectByType<InkPremiumShopView>();
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                foreach (var dimensions in new[] { new Vector2Int(320, 568), new Vector2Int(390, 844) })
                using (var size = new TemporaryGameViewSize(dimensions.x, dimensions.y))
                {
                    yield return Wait(.3f);
                    yield return InGameFrame(() => MobileUiLayout.SetPlatformSafeAreaOverride(new Rect(8, 20, Screen.width - 16, Screen.height - 55)));
                    foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        fixture.Store.Price = language == GameLanguage.Korean ? "₩4,900" : language == GameLanguage.Japanese ? "¥500" : "$2.99";
                        GameLocalization.SetLanguage(language); InkPremiumAppearance.OpenShop(); yield return Wait(.5f);
                        var paper = shop.transform.Find("PremiumShopCanvas/SafeArea/PremiumPaper");
                        var info = paper.Find("GoldInfo").GetComponent<Button>();
                        var purchase = paper.Find("Purchase").GetComponent<Button>();
                        yield return InGameFrame(() =>
                        {
                            Assert.That(Screen.width, Is.EqualTo(dimensions.x)); Assert.That(Screen.height, Is.EqualTo(dimensions.y));
                            InkPremiumUiTests.AssertConcisePresentation(paper, language);
                            foreach (var label in paper.GetComponentsInChildren<Text>())
                                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name + " " + language);
                            foreach (var button in new[] { info, purchase })
                                Assert.That(Bounds((RectTransform)button.transform).height, Is.GreaterThanOrEqualTo(44), button.name);
                            Assert.That(Bounds((RectTransform)info.transform.Find("Circle")).height, Is.LessThan(28),
                                "작은 원형 i의 시각 크기");
                            foreach (string name in new[] { "Title", "GoldenInk", "BenefitsHeading", "AdBenefit", "SkinBenefit", "LandingBenefit", "Purchase" })
                                Assert.That(Bounds((RectTransform)info.transform).Overlaps(Bounds((RectTransform)paper.Find(name))), Is.False, name);
                        });
                        yield return CapturePackage("shop-" + dimensions.x + "-" + language);
                        yield return InGameFrame(() => Click(info)); yield return Wait(.5f);
                        var preview = shop.GetComponentInChildren<GoldInkRecordedPreview>();
                        Assert.That(preview.IsOpen && preview.HasRecording, Is.True);
                        Assert.That(InkPremiumShopView.IsOpen, Is.True);
                        Assert.That(purchase.IsInteractable(), Is.False, "미리보기 뒤 구매를 막는다");
                        purchase.onClick.Invoke(); Assert.That(fixture.Store.Purchases, Is.Zero);
                        int initial = preview.DisplayedFrame; yield return Wait(.2f);
                        Assert.That(preview.DisplayedFrame, Is.Not.EqualTo(initial), "녹화 프레임 재생");
                        LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                        Assert.That(preview.DisplayedFrame, Is.EqualTo(GoldInkRecordedPreview.StillFrame));
                        yield return Wait(.2f); Assert.That(preview.DisplayedFrame, Is.EqualTo(GoldInkRecordedPreview.StillFrame));
                        var previewPaper = preview.transform.Find("GoldPreviewCanvas/SafeArea/PreviewPaper");
                        foreach (var entry in new[] { ("Title", "황금 착지 이펙트"), ("Caption", "황금 먹의 특별한 착지효과") })
                        {
                            var label = previewPaper.Find(entry.Item1).GetComponent<Text>();
                            Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(entry.Item2)));
                            if (language != GameLanguage.Korean) Assert.That(label.text, Is.Not.EqualTo(entry.Item2));
                        }
                        foreach (var label in previewPaper.GetComponentsInChildren<Text>())
                            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name + " " + language);
                        yield return CapturePackage("preview-" + dimensions.x + "-" + language);
                        LobbySettingsProfile.SetReducedMotionEnabled(false);
                        MobileApplicationLifecycle.SetPlatformVisibility(false); initial = preview.DisplayedFrame;
                        yield return Wait(.2f); Assert.That(preview.DisplayedFrame, Is.EqualTo(initial), "백그라운드 재생 정지");
                        MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.2f);
                        Assert.That(preview.DisplayedFrame, Is.Not.EqualTo(initial));
                        fixture.Store.Busy = true;
                        yield return InGameFrame(() => DismissPreview(preview)); yield return Wait(.4f);
                        Assert.That(preview.IsOpen, Is.False); Assert.That(preview.HasRecording, Is.False, "닫으면 텍스처 반환");
                        Assert.That(InkPremiumShopView.IsOpen, Is.True); Assert.That(fixture.Store.Busy, Is.True);
                        Assert.That(shop.transform.Find("PremiumShopCanvas").GetComponent<CanvasGroup>().interactable, Is.True);
                        fixture.Store.Busy = false;
                        yield return InGameFrame(() => Click(info)); yield return Wait(.2f);
                        Assert.That(preview.HasRecording && preview.IsOpen, Is.True, "반복 열기");
                        shop.Close(); yield return Wait(.4f);
                        Assert.That(InkPremiumShopView.IsOpen, Is.True, "상위 닫기 요청도 하위부터 닫는다");
                        shop.Close(); yield return Wait(.4f);
                        Assert.That(InkPremiumShopView.IsOpen, Is.False);
                        Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                    }
                }
                Assert.That(fixture.Store.Purchases, Is.Zero); Assert.That(fixture.Store.Restores, Is.Zero);
                Assert.That(fixture.Store.Owned, Is.False); Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
            }
            yield return new ExitPlayMode();
        }

        static void DismissPreview(GoldInkRecordedPreview preview)
        {
            var canvas = preview.transform.Find("GoldPreviewCanvas");
            var paper = (RectTransform)canvas.Find("SafeArea/PreviewPaper");
            var blocker = canvas.Find("Blocker").GetComponent<EventTrigger>();
            Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, paper.position), outside = new Vector2(2, 2);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = inside, pressPosition = inside };
            blocker.OnPointerClick(pointer); Assert.That(preview.IsOpen, Is.True);
            pointer.position = outside; blocker.OnPointerClick(pointer); Assert.That(preview.IsOpen, Is.True, "안에서 밖으로 끌기");
            pointer.pressPosition = outside; pointer.dragging = true;
            blocker.OnPointerClick(pointer); Assert.That(preview.IsOpen, Is.True);
            pointer.dragging = false; blocker.OnPointerClick(pointer);
        }
        static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); Assert.That(button.IsInteractable(), Is.True);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0)); Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]), max = min;
            foreach (var corner in corners) { var p = RectTransformUtility.WorldToScreenPoint(null, corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static IEnumerator InGameFrame(Action action)
        {
            var host = new GameObject("Gold preview interaction test"); var runner = host.AddComponent<NextUpdateTestClick>();
            bool completed = false; Exception failure = null;
            runner.Action = () => { try { action(); } catch (Exception e) { failure = e; } finally { completed = true; } };
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!completed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Object.Destroy(host); Assert.That(completed, Is.True); if (failure != null) throw failure;
        }
        static IEnumerator CapturePackage(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi()).Value<string>("path");
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True);
            string destination = Environment.GetEnvironmentVariable("MUKJUMP_PREMIUM_UI_OUTPUT") ?? Output + "/ui";
            Directory.CreateDirectory(destination); File.Copy(path, destination + "/" + name + ".png", true);
        }
        sealed class UiMemoryFixture : IDisposable
        {
            public readonly InkPremiumUiTests.Store Store = new InkPremiumUiTests.Store();
            readonly bool background = Application.runInBackground;
            readonly GameLanguage language = GameLocalization.Language;
            public UiMemoryFixture()
            {
                Application.runInBackground = true;
                var memory = new MemoryLobbySettingsStore(); LobbySettingsProfile.UseStoreForTests(memory); InkSkinPreferences.UseStoreForTests(memory);
                LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
                PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore()); ScoreManager.UseStoreForTests(new MemoryScoreStore());
                GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
                InkPremiumAppearance.UseForTests(true, Store); MobileApplicationLifecycle.SetPlatformVisibility(true);
            }
            public void Dispose()
            {
                InkPremiumAppearance.RestoreForTests(); GameLocalization.SetLanguage(language); Application.runInBackground = background;
                MobileUiLayout.ClearPlatformSafeAreaOverride(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
        }
    }
}
