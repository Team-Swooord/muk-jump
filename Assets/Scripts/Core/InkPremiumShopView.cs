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
        Button purchase, restore;
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
            restore.interactable = store != null && !store.IsBusy;
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
            Label("Title", "광고 제거 · 금색 먹", 62, 542, 150, true);
            var body = Rect("GoldenInk", paper, new Vector2(0, 300), new Vector2(330, 330)).gameObject.AddComponent<Image>();
            body.sprite = previewBodySprite; body.preserveAspect = true; body.raycastTarget = false;
            GoldInkVisual.Apply(body, true);
            Label("Benefits", "모든 광고 없이 편안하게\n구매 전용 금색 먹 의상", 46, 67, 150, true);
            Label("ReviveRules", "부활은 광고 없이 이용해요\n한 판에 한 번, 기존 이용 조건은 같아요", 33, -86, 115, false);
            Label("CosmeticOnly", "금색 먹은 모습과 착지 효과만 바뀌어요\n점프와 성장 능력은 그대로예요", 32, -213, 108, false);
            purchase = Action("Purchase", "상품 확인 중...", -355, out purchaseLabel);
            InkLocalizedText.Exclude(purchaseLabel);
            purchase.onClick.AddListener(() => { if (purchase.interactable) InkPremiumAppearance.Store?.Purchase(); });
            restore = Action("Restore", "구입 복원", -487, out _);
            restore.onClick.AddListener(() => { if (restore.interactable) InkPremiumAppearance.Store?.Restore(); });
            status = Label("Status", "", 29, -603, 92, false);
            var close = Action("Close", "닫기", 0, out _);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchoredPosition = new Vector2(0, -800); closeRect.sizeDelta = new Vector2(300, 110);
            close.onClick.AddListener(Close);
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
            var rect = Rect(name, paper, new Vector2(0, y), new Vector2(670, 112));
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
