#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 에디터에서는 네이티브 SDK 창 대신 실제 게임 흐름을 확인할 수 있는 테스트 광고를 띄운다.
    public sealed class EditorTestAdsRuntime : MonoBehaviour
    {
        const int CanvasSortingOrder = 9000;
        const float BannerHeight = 96f;

        sealed class EditorTestProvider : IFullScreenAdProvider
        {
            readonly EditorTestAdsRuntime owner;

            public EditorTestProvider(EditorTestAdsRuntime owner)
            {
                this.owner = owner;
            }

            public bool IsReady(FullScreenAdPlacement placement)
            {
                return owner != null && owner.isActiveAndEnabled &&
                       !owner.IsFullScreenOpen;
            }

            public void Preload(FullScreenAdPlacement placement) { }

            public void Show(
                FullScreenAdPlacement placement,
                Action<bool> onCompleted)
            {
                if (!IsReady(placement))
                {
                    onCompleted?.Invoke(false);
                    return;
                }
                owner.ShowFullScreen(placement, onCompleted);
            }
        }

        CanvasGroup bannerGroup;
        CanvasGroup fullScreenGroup;
        RectTransform safeAreaRoot;
        Text fullScreenTitle;
        Text fullScreenDescription;
        Text confirmLabel;
        Button confirmButton;
        Button cancelButton;
        EditorTestProvider provider;
        LobbyScreenNavigator navigator;
        LobbyOptionsView options;
        Action<bool> pendingCompletion;
        int lastWidth;
        int lastHeight;
        Rect lastSafeArea;

        public bool IsFullScreenOpen => pendingCompletion != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<EditorTestAdsRuntime>() == null)
                new GameObject(nameof(EditorTestAdsRuntime))
                    .AddComponent<EditorTestAdsRuntime>();
        }

        void OnEnable()
        {
            DontDestroyOnLoad(gameObject);
            BuildUi();
            provider = new EditorTestProvider(this);
            MonetizationAds.RegisterProvider(provider);
            GoogleMobileAdsPrivacy.Register(
                callback => callback?.Invoke(
                    "에디터 테스트 · 개인정보 선택 화면 확인 완료"),
                isRequired: true);
            Debug.Log(
                "먹점프 Google 광고: 에디터 테스트 배너·보상형 모의 광고 사용");
        }

        void Update()
        {
            if (Screen.width != lastWidth ||
                Screen.height != lastHeight ||
                MobileUiLayout.CurrentSafeArea != lastSafeArea)
                ApplySafeArea();

            if (navigator == null)
                navigator = LobbyScreenNavigator.Instance;
            if (options == null)
                options = FindAnyObjectByType<LobbyOptionsView>();
            bool showBanner = !IsFullScreenOpen &&
                              GoogleMobileAdsPresentation
                                  .ShouldShowLobbyBanner(
                                      GameManager.Instance,
                                      navigator,
                                      options);
            SetGroupVisible(bannerGroup, showBanner, false);
        }

        void OnDisable()
        {
            Complete(false);
            if (ReferenceEquals(MonetizationAds.Provider, provider))
                MonetizationAds.ResetProvider();
            provider = null;
            GoogleMobileAdsPrivacy.Reset();
        }

        void BuildUi()
        {
            if (safeAreaRoot != null) return;

            var canvasObject = new GameObject(
                "EditorGoogleTestAdsCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            safeAreaRoot = CreateStretchRect(
                "SafeAreaRoot",
                canvasObject.transform);
            BuildBanner();
            BuildFullScreen();
            ApplySafeArea();
        }

        void BuildBanner()
        {
            RectTransform root = CreateRect(
                "GoogleTestBanner",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(920f, BannerHeight));
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -8f);
            Image outline = root.gameObject.AddComponent<Image>();
            outline.color = new Color(
                InkPalette.Ink.r,
                InkPalette.Ink.g,
                InkPalette.Ink.b,
                0.94f);
            outline.raycastTarget = false;

            RectTransform paper = CreateRect(
                "Paper",
                root,
                Vector2.zero,
                new Vector2(912f, BannerHeight - 8f));
            Image paperImage = paper.gameObject.AddComponent<Image>();
            paperImage.color = InkPalette.Paper2;
            paperImage.raycastTarget = false;
            Text label = CreateText(
                "Label",
                paper,
                "Google 테스트 광고  ·  로비 상단 배너",
                34,
                Vector2.zero,
                new Vector2(850f, 70f),
                InkPalette.TextDark);
            label.fontStyle = FontStyle.Bold;
            bannerGroup = root.gameObject.AddComponent<CanvasGroup>();
            SetGroupVisible(bannerGroup, false, false);
        }

        void BuildFullScreen()
        {
            RectTransform root = CreateStretchRect(
                "GoogleTestFullScreen",
                safeAreaRoot);
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);
            dim.raycastTarget = true;
            fullScreenGroup = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform panel = CreateRect(
                "Panel",
                root,
                Vector2.zero,
                new Vector2(820f, 920f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = InkPalette.Paper;

            CreateText(
                "Badge",
                panel,
                "GOOGLE TEST AD",
                30,
                new Vector2(0f, 342f),
                new Vector2(620f, 58f),
                InkPalette.Red);
            fullScreenTitle = CreateText(
                "Title",
                panel,
                "보상형 광고 테스트",
                52,
                new Vector2(0f, 230f),
                new Vector2(680f, 100f),
                InkPalette.TextDark);
            fullScreenTitle.fontStyle = FontStyle.Bold;
            fullScreenDescription = CreateText(
                "Description",
                panel,
                string.Empty,
                38,
                new Vector2(0f, 60f),
                new Vector2(650f, 220f),
                InkPalette.TextDark);

            confirmButton = CreateButton(
                "Confirm",
                panel,
                "테스트 보상 완료",
                new Vector2(0f, -150f),
                new Vector2(620f, 126f),
                primary: true,
                out confirmLabel);
            confirmButton.onClick.AddListener(() => Complete(true));
            cancelButton = CreateButton(
                "Cancel",
                panel,
                "닫기 · 보상 없음",
                new Vector2(0f, -310f),
                new Vector2(620f, 112f),
                primary: false,
                out _);
            cancelButton.onClick.AddListener(() => Complete(false));
            SetGroupVisible(fullScreenGroup, false, true);
        }

        void ShowFullScreen(
            FullScreenAdPlacement placement,
            Action<bool> onCompleted)
        {
            pendingCompletion = onCompleted;
            bool rewarded = placement !=
                            FullScreenAdPlacement.PostRunInterstitial;
            fullScreenTitle.text = rewarded
                ? "보상형 광고 테스트"
                : "전면 광고 테스트";
            fullScreenDescription.text = rewarded
                ? "에디터에서는 실제 광고 대신 이 화면으로\n부활 보상 지급과 취소 흐름을 확인합니다."
                : "에디터 전면 광고 모의 화면입니다.\n닫은 뒤 게임 흐름이 정상인지 확인하세요.";
            confirmLabel.text = rewarded
                ? "테스트 보상 완료"
                : "테스트 광고 닫기";
            cancelButton.gameObject.SetActive(rewarded);
            SetGroupVisible(fullScreenGroup, true, true);
            SetGroupVisible(bannerGroup, false, false);
        }

        void Complete(bool completed)
        {
            if (pendingCompletion == null)
            {
                if (fullScreenGroup != null)
                    SetGroupVisible(fullScreenGroup, false, true);
                return;
            }
            Action<bool> callback = pendingCompletion;
            pendingCompletion = null;
            SetGroupVisible(fullScreenGroup, false, true);
            callback.Invoke(completed);
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = safe;
        }

        static Button CreateButton(
            string name,
            Transform parent,
            string value,
            Vector2 position,
            Vector2 size,
            bool primary,
            out Text label)
        {
            RectTransform root = CreateRect(name, parent, position, size);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = primary ? InkPalette.Ink : InkPalette.Paper2;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = CreateText(
                "Label",
                root,
                value,
                40,
                Vector2.zero,
                size - new Vector2(32f, 20f),
                primary ? InkPalette.TextLight : InkPalette.TextDark);
            label.fontStyle = FontStyle.Bold;
            return button;
        }

        static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = color;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                TextAnchor.MiddleCenter,
                false,
                true);
            return text;
        }

        static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void SetGroupVisible(
            CanvasGroup group,
            bool visible,
            bool interactive)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible && interactive;
            group.blocksRaycasts = visible && interactive;
        }
    }
}
#endif
