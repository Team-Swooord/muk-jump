using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// iOS 구매 안내. 결제·복원은 StoreKit 공급자만 실행하고 화면은 권한을 직접 부여하지 않는다.
    [DisallowMultipleComponent]
    public sealed class InkPremiumShopView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        CanvasGroup group;
        RectTransform safeRoot, paper;
        HanjiScrollFrame frame;
        Button purchase;
        Text purchaseLabel, status;
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
            frame.CancelClose();
            IsOpen = true;
            group.alpha = 1; group.interactable = group.blocksRaycasts = true;
            PointerInput.SuppressUntilRelease();
            Refresh();
        }
        public void Close()
        {
            if (group == null || !IsOpen) return;
            PointerInput.SuppressUntilRelease();
            frame.Close(Hide, group);
        }
        void Hide()
        {
            IsOpen = false;
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
                new Vector2(920, 1800), MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height, Vector2.zero);
            var store = InkPremiumAppearance.Store;
            bool owned = InkPremiumAppearance.IsOwned;
            purchase.interactable = !owned && store != null && store.CanPurchase && !store.IsBusy;
            string caption = owned ? "구매 완료" : store?.CanPurchase == true ? "구매하기" : "상품 확인 중...";
            string localized = GameLocalization.Translate(caption);
            purchaseLabel.text = localized + (!owned && !string.IsNullOrEmpty(store?.LocalizedPrice) ? "  " + store.LocalizedPrice : "");
            InkLocalizedText.SetSource(status, store?.StatusMessage ?? "스토어에 연결하고 있어요");
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
            paper = Rect("PremiumPaper", safeRoot, Vector2.zero, new Vector2(880, 1420));
            frame = HanjiScrollFrame.Attach(paper, new Vector2(880, 1400));
            InkUiStyle.ConfigurePopupDismiss(dim, paper, Close,
                () => IsOpen && group.interactable && (!Application.isPlaying || frame.IsReady));
            Label("Title", "모든 광고 제거", 68, 555, 100, true);
            Label("Permanent", "한 번 구매 · 영구 적용", 40, 460, 76, false);
            BuildGoldenBackdrop();
            var body = Rect("GoldenInk", paper, new Vector2(0, 230), new Vector2(340, 340)).gameObject.AddComponent<Image>();
            body.sprite = previewBodySprite; body.preserveAspect = true; body.raycastTarget = false;
            GoldInkVisual.Apply(body, true);
            Label("GoldenBonus", "금색 먹 포함", 46, -4, 80, true);
            Label("Benefits", "배너·전면·부활 광고 없이", 42, -100, 100, true);
            Label("ReviveRules", "부활 한 판 1회 · 기존 조건 유지", 34, -187, 64, false);
            purchase = Action("Purchase", "상품 확인 중...", -318, out purchaseLabel);
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
                    store == null || store.IsBusy || !store.CanPurchase) return;
                store.Purchase();
                Refresh();
            });
            Label("PurchaseNotice", "구매 즉시 적용됩니다.\n제공 개시 후 청약철회는 법령·스토어 정책에 따라 제한될 수 있습니다.", 30, -493, 154, false);
            status = Label("Status", "", 30, -621, 86, false);
            Hide();
        }
        void BuildGoldenBackdrop()
        {
            // 기존 공용 붓 자산만 재사용한다. 장식은 먹 뒤에 놓고 입력·권한·능력을 바꾸지 않는다.
            var backdrop = Rect("GoldenBackdrop", paper, new Vector2(0, 230), Vector2.one * 390);
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
