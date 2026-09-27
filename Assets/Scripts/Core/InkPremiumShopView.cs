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
            Label("Permanent", "한 번 구매하면 계속 이용할 수 있어요", 40, 460, 76, false);
            var body = Rect("GoldenInk", paper, new Vector2(0, 280), new Vector2(300, 300)).gameObject.AddComponent<Image>();
            body.sprite = previewBodySprite; body.preserveAspect = true; body.raycastTarget = false;
            GoldInkVisual.Apply(body, true);
            Label("GoldenBonus", "반짝이는 금색 먹 포함", 43, 85, 86, true);
            Label("Benefits", "배너 · 전면 광고 제거\n부활도 광고 없이", 48, -40, 128, true);
            Label("ReviveRules", "부활은 한 판에 한 번, 기존 조건 그대로", 36, -172, 90, false);
            Label("CosmeticOnly", "금색 먹은 외형 전용이에요\n점프·성장 능력은 같아요", 36, -286, 100, false);
            purchase = Action("Purchase", "상품 확인 중...", -426, out purchaseLabel);
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
            Label("RestoreHint", "이전 구매는 설정에서 구입 복원", 32, -544, 62, false);
            status = Label("Status", "", 32, -621, 86, false);
            Hide();
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
