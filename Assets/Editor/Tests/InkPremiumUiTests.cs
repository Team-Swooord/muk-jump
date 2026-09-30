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
    public sealed class InkPremiumUiTests
    {
        public sealed class Store : IInkPremiumStore
        {
            public bool Owned, Busy;
            public int Purchases, Restores;
            public string Message = "";
            public string Price = "$2.99";
            public bool IsOwned => Owned;
            public bool IsBusy => Busy;
            public bool CanPurchase => !Owned && !Busy;
            public string LocalizedPrice => Price;
            public string StatusMessage => Message;
            public void Purchase() { Purchases++; Busy = true; }
            public void Restore() { Restores++; Busy = true; Message = "구입을 복원하고 있어요"; }
        }
        GameObject host;
        Store store;
        GameLanguage previousLanguage;
        static void Invoke(object target, string method) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        [SetUp] public void SetUp()
        {
            previousLanguage = GameLocalization.Language;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            store = new Store(); InkPremiumAppearance.UseForTests(true, store);
        }
        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            typeof(GoldInkVisual).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            InkPremiumAppearance.RestoreForTests();
            GameLocalization.SetLanguage(previousLanguage);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void PurchaseIsSinglePrimaryActionWithRealPriceAndNoRestoreButton(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("Premium UI test");
            var shop = host.AddComponent<InkPremiumShopView>();
            Invoke(shop, "Build"); Invoke(shop, "Refresh");
            var paper = host.transform.Find("PremiumShopCanvas/SafeArea/PremiumPaper");
            Assert.That(paper.Find("Restore"), Is.Null);
            Assert.That(paper.Find("Close"), Is.Null);
            AssertConcisePresentation(paper, language);
            Assert.That(paper.GetComponentsInChildren<Button>().Select(b => b.name), Is.EquivalentTo(new[] { "GoldInfo", "Purchase" }));
            var purchase = paper.Find("Purchase").GetComponent<Button>();
            Assert.That(purchase.GetComponentInChildren<Text>().text, Does.Contain("$2.99"));
            foreach (var label in paper.GetComponentsInChildren<Text>())
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            var status = paper.Find("Status").GetComponent<Text>();
            Assert.That(status.gameObject.activeSelf, Is.False, "정상 화면에는 별도 상태 문구를 노출하지 않는다");
            purchase.onClick.Invoke(); purchase.onClick.Invoke();
            Assert.That(store.Purchases, Is.EqualTo(1)); Assert.That(store.Restores, Is.Zero);
            store.Message = "구매하지 못했어요. 잠시 후 다시 시도해 주세요";
            Invoke(shop, "Refresh");
            Assert.That(status.gameObject.activeSelf, Is.True);
            Assert.That(status.text, Is.EqualTo(GameLocalization.Translate(store.Message)));
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 2));
            Assert.That(purchase.interactable, Is.False, "처리 중 중복 구매 차단은 유지한다");
            store.Message = ""; Invoke(shop, "Refresh");
            Assert.That(status.gameObject.activeSelf, Is.False);
            store.Busy = false; store.Owned = true; Invoke(shop, "Refresh");
            Assert.That(purchase.interactable, Is.False);
        }
        public static void AssertConcisePresentation(Transform paper, GameLanguage language)
        {
            var backdrop = paper.Find("GoldenBackdrop");
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.GetSiblingIndex(), Is.LessThan(paper.Find("GoldenInk").GetSiblingIndex()));
            Assert.That(backdrop.GetComponentsInChildren<Image>().Length, Is.EqualTo(3));
            foreach (var image in backdrop.GetComponentsInChildren<Image>())
            {
                Assert.That(image.sprite, Is.Not.Null, image.name);
                Assert.That(image.raycastTarget, Is.False, image.name);
            }
            Assert.That(paper.Find("CosmeticOnly"), Is.Null, "긴 중복 설명은 넣지 않는다");
            Assert.That(paper.Find("RestoreHint"), Is.Null);
            foreach (string name in new[] { "Permanent", "GoldenBonus", "Benefits", "ReviveRules" })
                Assert.That(paper.Find(name), Is.Null, "먹 주변에 설명을 흩어 배치하지 않는다: " + name);
            Assert.That(paper.GetComponentsInChildren<Text>(true).Select(t => t.name),
                Is.EquivalentTo(new[] { "Title", "BenefitsHeading", "AdBenefit", "SkinBenefit", "LandingBenefit", "InfoGlyph", "Label", "PurchaseNotice", "Status" }));
            Assert.That(paper.Find("Title").GetComponent<Text>().text, Is.EqualTo(GameLocalization.Translate("황금먹패키지(영구)")));
            string[] names = { "BenefitsHeading", "AdBenefit", "SkinBenefit", "LandingBenefit" };
            string[] sources = { "혜택", "1. 모든 광고 제거", "2. 황금 먹 스킨 제공", "특별한 착지 효과 포함" };
            for (int i = 0; i < names.Length; i++)
            {
                var label = paper.Find(names[i]).GetComponent<Text>();
                Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(sources[i])));
                Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter), names[i]);
                Assert.That(label.rectTransform.anchoredPosition.x, Is.Zero, names[i]);
                if (language != GameLanguage.Korean) Assert.That(label.text, Is.Not.EqualTo(sources[i]));
                if (i > 0)
                {
                    var previous = (RectTransform)paper.Find(names[i - 1]);
                    Assert.That(previous.anchoredPosition.y - previous.sizeDelta.y / 2,
                        Is.GreaterThan(label.rectTransform.anchoredPosition.y + label.rectTransform.sizeDelta.y / 2));
                }
            }
            var summary = paper.Find("BenefitsHeading").GetComponent<Text>();
            var lastBenefit = (RectTransform)paper.Find("LandingBenefit");
            Assert.That(paper.Find("GoldInfo/InfoGlyph").GetComponent<Text>().text, Is.EqualTo("i"));
            Assert.That(((RectTransform)paper.Find("GoldInfo/Circle")).sizeDelta, Is.EqualTo(Vector2.one * 54));
            Assert.That(((RectTransform)paper.Find("GoldInfo")).anchoredPosition, Is.EqualTo(new Vector2(225, 390)));
            Assert.That(((RectTransform)paper.Find("GoldInfo")).sizeDelta, Is.EqualTo(Vector2.one * 160),
                "시각 크기를 줄여도 터치 영역은 유지한다");
            foreach (string name in new[] { "BenefitsRuleLeft", "BenefitsRuleRight" })
                Assert.That(paper.Find(name).GetComponent<Image>().raycastTarget, Is.False);
            string noticeSource = "※ 구매 즉시 광고 제거 및 패키지 혜택이 적용됩니다. 디지털 콘텐츠 제공이 시작된 이후에는 관련 법령 및 스토어 정책에 따라 청약철회가 제한될 수 있습니다.";
            var notice = paper.Find("PurchaseNotice").GetComponent<Text>();
            Assert.That(notice.text, Is.EqualTo(GameLocalization.Translate(noticeSource)));
            if (language != GameLanguage.Korean) Assert.That(notice.text, Is.Not.EqualTo(noticeSource));
            // 일본어는 기존 공용 폰트의 상하 메트릭 보정(0.76배)을 유지한다.
            Assert.That(notice.fontSize, Is.EqualTo(language == GameLanguage.Japanese ? Mathf.RoundToInt(28 * .76f) : 28));
            // 구매 버튼·안내·동적 상태 문구는 서로 다른 행이다.
            var purchase = (RectTransform)paper.Find("Purchase");
            var status = (RectTransform)paper.Find("Status");
            var title = (RectTransform)paper.Find("Title");
            var body = (RectTransform)paper.Find("GoldenInk");
            Assert.That(title.anchoredPosition.y - title.sizeDelta.y / 2,
                Is.GreaterThan(((RectTransform)backdrop).anchoredPosition.y + 200));
            Assert.That(body.anchoredPosition.y - body.sizeDelta.y / 2,
                Is.GreaterThan(summary.rectTransform.anchoredPosition.y + summary.rectTransform.sizeDelta.y / 2));
            Assert.That(lastBenefit.anchoredPosition.y - lastBenefit.sizeDelta.y / 2,
                Is.GreaterThan(purchase.anchoredPosition.y + purchase.sizeDelta.y / 2));
            Assert.That(purchase.anchoredPosition.y - purchase.sizeDelta.y / 2,
                Is.GreaterThan(status.anchoredPosition.y + status.sizeDelta.y / 2));
            Assert.That(status.anchoredPosition.y - status.sizeDelta.y / 2,
                Is.GreaterThan(notice.rectTransform.anchoredPosition.y + notice.rectTransform.sizeDelta.y / 2));
            Assert.That(notice.rectTransform.anchoredPosition.y - notice.rectTransform.sizeDelta.y / 2,
                Is.GreaterThan(-((RectTransform)paper).sizeDelta.y / 2));
        }
        [TestCase(false)] [TestCase(true)]
        public void ShopDismissesOnlyOnOutsideTapWithoutChangingPendingPurchase(bool busy)
        {
            host = new GameObject("Premium dismiss test");
            var shop = host.AddComponent<InkPremiumShopView>();
            Invoke(shop, "Open");
            store.Busy = busy;
            var canvas = host.transform.Find("PremiumShopCanvas");
            var paper = (RectTransform)canvas.Find("SafeArea/PremiumPaper");
            var backdrop = canvas.Find("Blocker").GetComponent<EventTrigger>();
            Assert.That(backdrop, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, paper.position);
            Vector2 outside = new Vector2(-100, -100);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left,
                position = inside, pressPosition = inside };
            backdrop.OnPointerClick(pointer);
            Assert.That(InkPremiumShopView.IsOpen, Is.True, "종이 안의 빈 공간은 닫지 않는다");
            pointer.position = outside;
            backdrop.OnPointerClick(pointer);
            Assert.That(InkPremiumShopView.IsOpen, Is.True, "종이 안에서 시작한 입력은 닫지 않는다");
            pointer.pressPosition = outside; pointer.dragging = true;
            backdrop.OnPointerClick(pointer);
            Assert.That(InkPremiumShopView.IsOpen, Is.True);
            pointer.dragging = false;
            backdrop.OnPointerClick(pointer);
            Assert.That(InkPremiumShopView.IsOpen, Is.False);
            Assert.That(canvas.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(store.Busy, Is.EqualTo(busy));
            Assert.That(store.Purchases, Is.Zero); Assert.That(store.Restores, Is.Zero); Assert.That(store.Owned, Is.False);
            Invoke(shop, "Open");
            Assert.That(InkPremiumShopView.IsOpen, Is.True, "닫은 뒤 다시 열 수 있다");
            Assert.That(paper.Find("Purchase").GetComponent<Button>().interactable, Is.EqualTo(!busy));
        }
        [Test] public void OptionsRestoreUsesStoreOnlyAndTracksPendingAndResult()
        {
            host = new GameObject("Restore UI test");
            host.SetActive(false);
            var view = host.AddComponent<LobbyOptionsView>(); view.BuildForPlatformForTests(RuntimePlatform.IPhonePlayer);
            var button = host.GetComponentsInChildren<Button>(true).Single(b => b.name == "RestorePurchasesButton");
            Invoke(view, "RefreshPremiumRestore"); button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(store.Restores, Is.EqualTo(1)); Assert.That(store.Purchases, Is.Zero);
            Assert.That(store.Owned, Is.False); Assert.That(button.interactable, Is.False);
            var status = host.GetComponentsInChildren<Text>(true).Single(t => t.name == "ConnectionStatus");
            Assert.That(status.text, Is.EqualTo(GameLocalization.Translate("구입을 복원하고 있어요")));
            store.Busy = false; store.Message = "구입 복원을 완료하지 못했어요";
            Invoke(view, "RefreshPremiumRestore");
            Assert.That(button.interactable, Is.True);
            Assert.That(status.text, Is.EqualTo(GameLocalization.Translate(store.Message)));
            Assert.That(store.Owned, Is.False);
            InkPremiumAppearance.UseForTests(false, store); Invoke(view, "RefreshPremiumRestore");
            button.onClick.Invoke(); Assert.That(store.Restores, Is.EqualTo(1));
            Assert.That(button.gameObject.activeSelf, Is.False);
            Object.DestroyImmediate(host);
            host = new GameObject("Toss restore boundary"); host.SetActive(false);
            view = host.AddComponent<LobbyOptionsView>();
            view.BuildForPlatformForTests(RuntimePlatform.WebGLPlayer);
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(b => b.name == "RestorePurchasesButton"), Is.False);
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void TossRestoreUsesItsStoreWithoutOverlappingLocalSaveNotice(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("Toss restore UI"); host.SetActive(false);
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForPlatformForTests(RuntimePlatform.WebGLPlayer);
            var button = host.GetComponentsInChildren<Button>(true).Single(b => b.name == "RestorePurchasesButton");
            var restore = (RectTransform)button.transform;
            var guide = (RectTransform)restore.parent.Find("GuideButton");
            var notice = (RectTransform)restore.parent.Find("LocalSaveNoticeTitle");
            Assert.That(restore.anchoredPosition.x - restore.sizeDelta.x / 2,
                Is.GreaterThan(guide.anchoredPosition.x + guide.sizeDelta.x / 2));
            Assert.That(restore.anchoredPosition.y - restore.sizeDelta.y / 2,
                Is.GreaterThan(notice.anchoredPosition.y + notice.sizeDelta.y / 2));
            var label = button.GetComponentInChildren<Text>(true);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2));
            Invoke(view, "RefreshPremiumRestore"); button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(store.Restores, Is.EqualTo(1)); Assert.That(store.Purchases, Is.Zero);
            Assert.That(store.Owned, Is.False); Assert.That(button.interactable, Is.False);
            Assert.That(restore.parent.Find("AccountButton"), Is.Null);
        }
        [Test] public void SharedGoldMaterialsRespectReducedMotionAndCompile()
        {
            typeof(GoldInkVisual).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var world = GoldInkVisual.World; var ui = GoldInkVisual.Ui;
            Assert.That(ShaderUtil.ShaderHasError(world.shader), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(ui.shader), Is.False);
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Assert.That(world.GetFloat("_GoldShimmer"), Is.Zero);
            Assert.That(ui.GetFloat("_GoldShimmer"), Is.Zero);
            LobbySettingsProfile.SetReducedMotionEnabled(false);
            Assert.That(world.GetFloat("_GoldShimmer"), Is.EqualTo(1));
            Assert.That(GoldInkVisual.World, Is.SameAs(world)); Assert.That(GoldInkVisual.Ui, Is.SameAs(ui));
        }
        [Test] public void EveryGoldAnimationFrameCoversDarkBodyAndPreservesEyes()
        {
            // 실제 4×2 원본으로 검사한다. 어두운 몸체를 동공으로 오인한 틈도 포함한다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/Character/Player/muk_spritesheet.png"));
            var material = new Material(GoldInkVisual.Ui);
            var target = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var oldTarget = RenderTexture.active;
            var oldTime = Shader.GetGlobalVector("_Time");
            try
            {
                Color32[] Render(float time, Material effect)
                {
                    Shader.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
                    RenderTexture.active = target; GL.Clear(true, true, Color.clear);
                    if (effect == null) Graphics.Blit(source, target); else Graphics.Blit(source, target, effect);
                    pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); pixels.Apply();
                    return pixels.GetPixels32();
                }
                var baseline = Render(.95f, null);
                material.SetFloat("_GoldShimmer", 1);
                var first = Render(.95f, material); var later = Render(2.85f, material);
                var darkBody = new int[8]; var pupils = new int[8]; var changed = new int[8];
                int alphaChanges = 0;
                for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
                {
                    int i = y * source.width + x, frame = y / 1024 * 4 + x / 1024;
                    if (first[i].a != later[i].a) alphaChanges++;
                    if (baseline[i].a < 254) continue;
                    float u = (x % 1024 + .5f) / 1024, v = (y % 1024 + .5f) / 1024;
                    bool eye = v > .44f && v < .61f &&
                        (u > .33f && u < .47f || u > .53f && u < .68f);
                    int brightness = Mathf.Max(baseline[i].r, baseline[i].g, baseline[i].b);
                    if (eye && brightness < 20 || brightness > 235)
                    {
                        Assert.That(first[i].r, Is.EqualTo(baseline[i].r).Within(2), "눈 색 보존");
                        Assert.That(first[i].g, Is.EqualTo(baseline[i].g).Within(2));
                        Assert.That(first[i].b, Is.EqualTo(baseline[i].b).Within(2));
                        Assert.That(later[i], Is.EqualTo(first[i]), "눈에는 흐르는 색을 적용하지 않는다");
                        if (brightness < 20) pupils[frame]++;
                    }
                    else if (!eye && brightness <= 20)
                    {
                        darkBody[frame]++;
                        Assert.That(first[i].r, Is.GreaterThan(150), "몸체의 어두운 틈: " + frame + " " + x + "," + y);
                        Assert.That(first[i].g, Is.GreaterThan(85));
                        Assert.That(later[i].r, Is.GreaterThan(150));
                        if (Mathf.Abs(first[i].g - later[i].g) > 5) changed[frame]++;
                    }
                }
                for (int frame = 0; frame < 8; frame++)
                {
                    Assert.That(darkBody[frame], Is.GreaterThan(1000), "어두운 몸체 검사 수 " + frame);
                    Assert.That(pupils[frame], Is.GreaterThan(1000), "동공 검사 수 " + frame);
                    Assert.That(changed[frame], Is.GreaterThan(100), "완만한 색 흐름 " + frame);
                }
                Assert.That(alphaChanges, Is.Zero, "애니메이션이 알파를 바꾸지 않는다");
                material.SetFloat("_GoldShimmer", 0);
                var reducedFirst = Render(.95f, material); var reducedLater = Render(2.85f, material);
                Assert.That(reducedLater, Is.EqualTo(reducedFirst), "움직임 줄이기는 정적인 금색을 유지한다");
            }
            finally
            {
                Shader.SetGlobalVector("_Time", oldTime); RenderTexture.active = oldTarget;
                Object.DestroyImmediate(source); Object.DestroyImmediate(material);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
        [Test] public void GoldShimmerColorsDarkBodyAndPreservesWhiteAndAlpha()
        {
            var source = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var material = new Material(GoldInkVisual.Ui);
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var oldTarget = RenderTexture.active;
            var oldTime = Shader.GetGlobalVector("_Time");
            try
            {
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    source.SetPixel(x, y, x < 8 ? Color.black : x < 16 ? Color.white : x < 24 ? new Color(.12f, .12f, .12f, 1) : Color.clear);
                source.Apply(); material.SetFloat("_GoldShimmer", 1);
                Color[] Render(float time)
                {
                    Shader.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
                    RenderTexture.active = target; GL.Clear(true, true, Color.clear);
                    Graphics.Blit(source, target, material);
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); pixels.Apply();
                    return pixels.GetPixels();
                }
                var first = Render(0.95f); var later = Render(2.85f);
                int row = 16 * 32;
                Assert.That(first[row + 20].a, Is.GreaterThan(.9f), "몸체가 실제 렌더되어야 한다");
                Assert.That(Mathf.Abs(first[row + 20].g - later[row + 20].g), Is.GreaterThan(.02f));
                Assert.That(first[row + 4].r, Is.GreaterThan(.6f), "검은 몸체도 금색으로 채운다");
                foreach (int x in new[] { 12, 28 }) Assert.That(later[row + x], Is.EqualTo(first[row + x]));
                for (int x = 0; x < 32; x++) Assert.That(later[row + x].a, Is.EqualTo(first[row + x].a));
                material.SetFloat("_GoldShimmer", 0);
                var reducedFirst = Render(.95f); var reducedLater = Render(2.85f);
                Assert.That(reducedLater, Is.EqualTo(reducedFirst), "움직임 줄이기는 정적인 금색을 유지한다");
            }
            finally
            {
                Shader.SetGlobalVector("_Time", oldTime); RenderTexture.active = oldTarget;
                Object.DestroyImmediate(source); Object.DestroyImmediate(material);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
