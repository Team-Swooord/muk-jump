using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 공통 구매 안내. 결제·복원은 플랫폼 스토어만 실행하고 화면은 권한을 직접 부여하지 않는다.
    [DisallowMultipleComponent]
    public sealed class InkPremiumShopView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        CanvasGroup group;
        RectTransform safeRoot, paper;
        HanjiScrollFrame frame;
        Button purchase;
        Text purchaseLabel, status;
        GoldInkRecordedPreview effectPreview;
        float refreshAt;
        public static bool IsOpen { get; private set; }
        public void ConfigurePreview(Sprite value) => previewBodySprite = value;
        void OnEnable() => InkPremiumAppearance.ShopRequested += Open;
        void OnDisable()
        {
            InkPremiumAppearance.ShopRequested -= Open;
            Hide();
        }
        void Open()
        {
            if (!InkPremiumAppearance.IsSupported || GameManager.Instance != null &&
                (GameManager.Instance.State != GameState.Lobby || GameManager.Instance.IsTransitioning)) return;
            Build();
            effectPreview?.Hide();
            frame.CancelClose();
            IsOpen = true;
            group.alpha = 1; group.interactable = group.blocksRaycasts = true;
            PointerInput.SuppressUntilRelease();
            Refresh();
        }
        public void Close()
        {
            if (group == null || !IsOpen) return;
            if (effectPreview != null && effectPreview.IsOpen) { effectPreview.Close(); return; }
            PointerInput.SuppressUntilRelease();
            frame.Close(Hide, group);
        }
        void Hide()
        {
            IsOpen = false;
            effectPreview?.Hide();
            if (group == null) return;
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        }
        void Update()
        {
            if (!IsOpen) return;
            if (!InkPremiumAppearance.IsSupported || GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Lobby) { Hide(); return; }
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .2f; Refresh(); }
        }
        void Refresh()
        {
            MobileUiLayout.ApplyCurrentSafeArea(safeRoot);
            paper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                new Vector2(920, 1640), MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height, Vector2.zero);
            var store = InkPremiumAppearance.Store;
            bool owned = InkPremiumAppearance.IsOwned;
            purchase.interactable = !owned && store != null && store.CanPurchase && !store.IsBusy;
            string caption = owned ? "구매 완료" : store?.CanPurchase == true ? "구매하기" : "상품 확인 중...";
            string localized = GameLocalization.Translate(caption);
            purchaseLabel.text = localized + (!owned && !string.IsNullOrEmpty(store?.LocalizedPrice) ? "  " + store.LocalizedPrice : "");
            string message = store?.StatusMessage ?? "스토어에 연결하고 있어요";
            InkLocalizedText.SetSource(status, message);
            status.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        void Build()
        {
            if (group != null) return;
            var root = new GameObject("PremiumShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4300;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            group = root.GetComponent<CanvasGroup>();
            var dim = Stretch("Blocker", root.transform).gameObject.AddComponent<Image>();
            InkUiStyle.ConfigurePopupDim(dim); dim.raycastTarget = true;
            safeRoot = Stretch("SafeArea", root.transform);
            paper = Rect("PremiumPaper", safeRoot, Vector2.zero, new Vector2(880, 1400));
            frame = HanjiScrollFrame.Attach(paper, new Vector2(880, 1380));
            InkUiStyle.ConfigurePopupDismiss(dim, paper, Close,
                () => IsOpen && group.interactable && (!Application.isPlaying || frame.IsReady));
            Label("Title", "황금먹패키지(영구)", 58, 550, 100, true);
            BuildGoldenBackdrop();
            var body = Rect("GoldenInk", paper, new Vector2(0, 275), new Vector2(340, 340)).gameObject.AddComponent<Image>();
            body.sprite = previewBodySprite; body.preserveAspect = true; body.raycastTarget = false;
            GoldInkVisual.Apply(body, true);
            BuildInfoButton();
            BuildBenefitsDivider();
            Benefit("BenefitsHeading", "혜택", 40, 40, 56, true).color = new Color(.46f, .31f, .12f);
            Benefit("AdBenefit", "1. 모든 광고 제거", 44, -38, 64, true);
            Benefit("SkinBenefit", "2. 황금 먹 스킨 제공", 44, -118, 64, true);
            Benefit("LandingBenefit", "특별한 착지 효과 포함", 32, -184, 48, false).color = new Color(.46f, .31f, .12f);
            purchase = Action("Purchase", "상품 확인 중...", -310, out purchaseLabel);
            purchaseLabel.fontSize = 48;
            var purchaseSurface = purchase.targetGraphic as Image;
            purchaseSurface.color = InkPalette.Red;
            var purchaseColors = purchase.colors;
            purchaseColors.normalColor = Color.white;
            purchaseColors.highlightedColor = new Color(1f, .94f, .94f);
            purchaseColors.selectedColor = Color.white;
            purchaseColors.pressedColor = new Color(.8f, .8f, .8f);
            purchase.colors = purchaseColors;
            purchaseLabel.color = InkPalette.Paper;
            InkLocalizedText.Exclude(purchaseLabel);
            purchase.onClick.AddListener(() =>
            {
                var store = InkPremiumAppearance.Store;
                if (!InkPremiumAppearance.IsSupported || InkPremiumAppearance.IsOwned ||
                    store == null || store.IsBusy || !store.CanPurchase || effectPreview?.IsOpen == true) return;
                store.Purchase();
                Refresh();
            });
            status = Label("Status", "", 28, -442, 72, false);
            Label("PurchaseNotice", "※ 구매 즉시 광고 제거 및 패키지 혜택이 적용됩니다. 디지털 콘텐츠 제공이 시작된 이후에는 관련 법령 및 스토어 정책에 따라 청약철회가 제한될 수 있습니다.", 28, -580, 180, false);
            Hide();
        }
        Text Benefit(string name, string source, int font, float y, float height, bool strong)
        {
            var text = Label(name, source, font, y, height, strong);
            text.rectTransform.sizeDelta = new Vector2(660, height);
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }
        void BuildBenefitsDivider()
        {
            // 혜택 제목을 중심으로 가느다란 금빛 선만 더하고 터치는 가로채지 않는다.
            foreach (float x in new[] { -150f, 150f })
            {
                var line = Rect(x < 0 ? "BenefitsRuleLeft" : "BenefitsRuleRight", paper,
                    new Vector2(x, 40), new Vector2(108, 2)).gameObject.AddComponent<Image>();
                line.color = new Color(.46f, .31f, .12f, .34f);
                line.raycastTarget = false;
            }
        }
        void BuildInfoButton()
        {
            // 작은 원의 시각 크기와 별개로 모바일 터치 영역은 충분히 확보한다.
            var hit = Rect("GoldInfo", paper, new Vector2(150, 390), Vector2.one * 160);
            var target = hit.gameObject.AddComponent<Image>(); target.color = Color.clear;
            var ring = BackdropImage("Circle", hit, InkUiTextureFactory.CreateCelestialDiscSprite(),
                Vector2.zero, Vector2.one * 42, new Color(.46f, .31f, .12f));
            BackdropImage("Fill", hit, ring.sprite, Vector2.zero, Vector2.one * 36, InkPalette.Paper);
            var label = Rect("InfoGlyph", hit, Vector2.zero, Vector2.one * 36).gameObject.AddComponent<Text>();
            InkUiStyle.ApplyReadableText(label, 28, TextAnchor.MiddleCenter, true, true);
            label.text = "i"; label.raycastTarget = false; label.color = InkPalette.TextDark;
            InkLocalizedText.Exclude(label);
            var button = hit.gameObject.AddComponent<Button>(); InkUiStyle.ConfigureButton(button, ring);
            button.onClick.AddListener(() =>
            {
                if (!IsOpen || !group.interactable || !InkPremiumAppearance.IsSupported) return;
                if (effectPreview == null)
                    effectPreview = new GameObject("GoldenLandingPreview").AddComponent<GoldInkRecordedPreview>();
                effectPreview.transform.SetParent(transform, false);
                effectPreview.Open(() =>
                {
                    if (IsOpen) { group.interactable = true; PointerInput.SuppressUntilRelease(); }
                });
                group.interactable = false;
            });
        }
        void BuildGoldenBackdrop()
        {
            // 기존 공용 붓 자산만 재사용한다. 장식은 먹 뒤에 놓고 입력·권한·능력을 바꾸지 않는다.
            var backdrop = Rect("GoldenBackdrop", paper, new Vector2(0, 275), Vector2.one * 390);
            BackdropImage("GoldWash", backdrop, InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, Vector2.one * 400, new Color(.74f, .50f, .15f, .16f));
            BackdropImage("BrushCircle", backdrop, Resources.Load<Sprite>("MukJump/UI/PermanentGrowth/pg_selected_ring"),
                Vector2.zero, Vector2.one * 390, new Color(.46f, .31f, .12f, .48f));
            var floor = BackdropImage("GroundBrush", backdrop, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0, -149), new Vector2(245, 22), new Color(.38f, .25f, .10f, .24f));
            floor.preserveAspect = false;
        }
        static Image BackdropImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = true;
            image.raycastTarget = false; image.enabled = sprite != null;
            return image;
        }
        Text Label(string name, string source, int font, float y, float height, bool strong)
        {
            var text = Rect(name, paper, new Vector2(0, y), new Vector2(752, height)).gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(text, source);
            InkUiStyle.ApplyReadableText(text, font, TextAnchor.MiddleCenter, strong, true);
            text.color = InkPalette.TextDark; text.raycastTarget = false;
            return text;
        }
        Button Action(string name, string source, float y, out Text label)
        {
            var rect = Rect(name, paper, new Vector2(0, y), new Vector2(670, 160));
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var surface = Stretch("Paper", rect).gameObject.AddComponent<Image>();
            surface.sprite = InkUiStyle.ActionButtonSprite; surface.raycastTarget = false;
            InkUiStyle.ConfigureHanjiSurface(surface);
            var button = rect.gameObject.AddComponent<Button>(); InkUiStyle.ConfigureButton(button, surface);
            button.colors = InkUiStyle.ActionButtonColors();
            label = Rect("Label", rect, Vector2.zero, new Vector2(630, 100)).gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(label, source); InkUiStyle.ApplyReadableText(label, 39, TextAnchor.MiddleCenter, true, true);
            label.raycastTarget = false; label.color = InkPalette.TextDark;
            return button;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static RectTransform Stretch(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
    }
}
