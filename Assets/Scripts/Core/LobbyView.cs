using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 씬 빌더가 구성한 로비 Canvas의 표시와 시작·성장·옵션 진입을 담당한다.
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasGroup))]
    public class LobbyView : MonoBehaviour
    {
        const float LobbyHorizontalPadding = 24f;
        // 원본 로고 PNG의 유효 알파 폭과 중심 편차를 함께 잡는다.
        // 투명 캔버스 전체(1281px)를 맞추면 일반 9:16에서도 제목이 불필요하게 작아진다.
        const float LobbyLogoVisibleWidth = 872.88f;
        const float LobbyLogoVisibleCenterOffset = 30.88f;
        const float LobbyLogoAnchorOffset = 12f;
        static readonly float LobbyContentDesignWidth =
            (Mathf.Abs(LobbyMenuLayout.ButtonPosition.x) +
             LobbyMenuLayout.BackgroundSize.x * 0.5f) * 2f;

        [SerializeField] Text bestText;
        [SerializeField] Button startButton;
        [SerializeField] Button growthButton;
        [SerializeField] Button optionsButton;

        PermanentGrowthView permanentGrowthView;
        LobbyOptionsView optionsView;
        LobbyScreenNavigator screenNavigator;
        CanvasGroup canvasGroup;
        bool listenersBound;
        bool lastVisible = true;
        bool lastInteractive = true;
        bool navigationVisible = true;
        bool navigationInteractive = true;
        int lastDisplayedBest = int.MinValue;
        LobbyMenuSelection activeMenu = LobbyMenuSelection.Start;
        RectTransform safeAreaRoot;
        RectTransform lobbyContentRoot;
        RectTransform logoRect;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;

        public Button StartButton => startButton;
        public Button GrowthButton => growthButton;
        public Button OptionsButton => optionsButton;
        public bool IsInteractive =>
            canvasGroup != null && canvasGroup.blocksRaycasts;
        public bool IsVisible =>
            canvasGroup != null && canvasGroup.alpha > 0.001f;
        public LobbyMenuSelection ActiveMenu => activeMenu;

        void OnEnable()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            RefreshResponsiveLayout();
            if (Application.isPlaying)
            {
                BindListeners();
            }
        }

        void Start()
        {
            RefreshResponsiveLayout();
            BindListeners();
            RefreshBest();
        }

        void OnDisable()
        {
            UnbindListeners();
        }

        void Update()
        {
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != Screen.safeArea ||
                safeAreaRoot == null || lobbyContentRoot == null ||
                logoRect == null)
                EnsureSafeAreaLayout();

            if (!Application.isPlaying)
                return;

            // 자기 GameObject를 비활성화하면 Update가 멈춰 로비로 돌아와도 다시 켤 수 없다.
            // CanvasGroup으로만 숨겨 입력 차단과 재활성화를 같은 활성 객체에서 처리한다.
            bool lobbyState = GameManager.Instance == null ||
                              GameManager.Instance.State == GameState.Lobby;
            bool show = lobbyState && navigationVisible;
            RefreshMenuSelection();
            SetVisible(show, show && navigationInteractive);
            if (!show) return;
            RefreshBest();
        }

        /// 씬 빌더와 런타임이 같은 로비 배치 계산을 공유한다.
        public void RefreshResponsiveLayout()
        {
            ApplyUiFont();
            EnsureMenuLayout();
            EnsureSafeAreaLayout();
        }

        void BindListeners()
        {
            if (listenersBound) return;
            startButton?.onClick.AddListener(HandleStartPressed);
            growthButton?.onClick.AddListener(HandleGrowthPressed);
            optionsButton?.onClick.AddListener(HandleOptionsPressed);
            listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!listenersBound) return;
            startButton?.onClick.RemoveListener(HandleStartPressed);
            growthButton?.onClick.RemoveListener(HandleGrowthPressed);
            optionsButton?.onClick.RemoveListener(HandleOptionsPressed);
            listenersBound = false;
        }

        void HandleStartPressed()
        {
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (PermanentGrowthProfile.RequiresRecovery)
            {
                ResolveOptionsView()?.Close();
                if (navigator != null)
                    navigator.OpenGrowth();
                else
                    ResolvePermanentGrowthView()?.Open();
                return;
            }
            if (navigator != null && !navigator.CanStartGame)
                return;
            permanentGrowthView?.Close();
            optionsView?.Close();
            GameManager.Instance?.StartGameFromMenu();
        }

        void HandleGrowthPressed()
        {
            ResolveOptionsView()?.Close();
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null)
            {
                navigator.OpenGrowth();
                return;
            }
            PermanentGrowthView growth = ResolvePermanentGrowthView();
            growth?.Open();
            if (growth != null && growth.IsOpen)
                SetActiveMenu(LobbyMenuSelection.Growth);
        }

        void HandleOptionsPressed()
        {
            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null && !navigator.CanStartGame)
                return;
            ResolvePermanentGrowthView()?.Close();
            LobbyOptionsView options = ResolveOptionsView();
            options?.Open();
            if (options != null && options.IsOpen)
                SetActiveMenu(LobbyMenuSelection.Options);
        }

        PermanentGrowthView ResolvePermanentGrowthView()
        {
            if (permanentGrowthView == null)
                permanentGrowthView = FindFirstObjectByType<PermanentGrowthView>();
            return permanentGrowthView;
        }

        LobbyOptionsView ResolveOptionsView()
        {
            if (optionsView == null)
                optionsView = FindFirstObjectByType<LobbyOptionsView>();
            return optionsView;
        }

        LobbyScreenNavigator ResolveScreenNavigator()
        {
            if (screenNavigator == null)
            {
                screenNavigator = LobbyScreenNavigator.Instance != null
                    ? LobbyScreenNavigator.Instance
                    : FindFirstObjectByType<LobbyScreenNavigator>();
            }
            return screenNavigator;
        }

        public void SetNavigationPresentation(bool visible, bool interactive)
        {
            navigationVisible = visible;
            navigationInteractive = interactive;
            bool lobbyState = GameManager.Instance == null ||
                              GameManager.Instance.State == GameState.Lobby;
            bool show = lobbyState && navigationVisible;
            SetVisible(show, show && navigationInteractive);
        }

        /// 실행 중이던 구버전 Main 백업이 복원돼도 세 메뉴가 즉시 같은 규칙을 쓴다.
        /// 옵션 버튼이 없으면 성장 버튼의 수묵 그래픽을 복제한다.
        void EnsureMenuLayout()
        {
            if (optionsButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].name == "OptionsButton")
                    {
                        optionsButton = buttons[i];
                        break;
                    }
                }
            }
            if (optionsButton == null)
            {
                Button source = growthButton != null
                    ? growthButton
                    : startButton;
                if (source != null && source.transform.parent != null)
                {
                    GameObject clone = Instantiate(
                        source.gameObject,
                        source.transform.parent);
                    clone.name = "OptionsButton";
                    optionsButton = clone.GetComponent<Button>();
                    optionsButton?.onClick.RemoveAllListeners();
                    clone.transform.SetAsLastSibling();
                }
            }

            LobbyMenuLayout.ApplyRecord(bestText);
            LobbyMenuLayout.ApplyButton(
                startButton,
                "시작",
                LobbyMenuLayout.StartAnchor,
                primary: activeMenu == LobbyMenuSelection.Start);
            LobbyMenuLayout.ApplyButton(
                growthButton,
                "성장",
                LobbyMenuLayout.GrowthAnchor,
                primary: activeMenu == LobbyMenuSelection.Growth);
            LobbyMenuLayout.ApplyButton(
                optionsButton,
                "옵션",
                LobbyMenuLayout.OptionsAnchor,
                primary: activeMenu == LobbyMenuSelection.Options);
        }

        void EnsureSafeAreaLayout()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            if (safeAreaRoot == null)
                safeAreaRoot = transform.Find("SafeAreaRoot") as RectTransform;
            if (safeAreaRoot == null && Application.isPlaying)
            {
                var safeObject = new GameObject(
                    "SafeAreaRoot",
                    typeof(RectTransform));
                safeAreaRoot = safeObject.GetComponent<RectTransform>();
                safeAreaRoot.SetParent(transform, false);
                safeAreaRoot.anchorMin = Vector2.zero;
                safeAreaRoot.anchorMax = Vector2.one;
                safeAreaRoot.offsetMin = Vector2.zero;
                safeAreaRoot.offsetMax = Vector2.zero;
            }
            if (safeAreaRoot == null) return;

            if (lobbyContentRoot == null)
                lobbyContentRoot = safeAreaRoot.Find("LobbyContentRoot")
                    as RectTransform;
            if (lobbyContentRoot == null && Application.isPlaying)
            {
                var contentObject = new GameObject(
                    "LobbyContentRoot",
                    typeof(RectTransform));
                lobbyContentRoot = contentObject.GetComponent<RectTransform>();
                lobbyContentRoot.SetParent(safeAreaRoot, false);
                lobbyContentRoot.anchorMin = Vector2.zero;
                lobbyContentRoot.anchorMax = Vector2.one;
                lobbyContentRoot.offsetMin = Vector2.zero;
                lobbyContentRoot.offsetMax = Vector2.zero;
            }
            if (lobbyContentRoot == null) return;

            MoveLobbyContentToSafeArea();
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);

            float contentScale = MobileUiLayout.CalculateWidthFitScale(
                LobbyContentDesignWidth,
                safe,
                Screen.width,
                Screen.height,
                LobbyHorizontalPadding);
            lobbyContentRoot.anchoredPosition = Vector2.zero;
            lobbyContentRoot.localScale = Vector3.one * contentScale;

            if (logoRect == null)
                logoRect = lobbyContentRoot.Find("Logo") as RectTransform;
            if (logoRect != null)
            {
                float logoScale =
                    MobileUiLayout.CalculateVisibleContentFitScale(
                    LobbyLogoVisibleWidth,
                    LobbyLogoVisibleCenterOffset,
                    LobbyLogoAnchorOffset,
                    contentScale,
                    safe,
                    Screen.width,
                    Screen.height,
                    LobbyHorizontalPadding);
                logoRect.localScale = Vector3.one * logoScale;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        void MoveLobbyContentToSafeArea()
        {
            if (safeAreaRoot == null) return;
            MoveDirectChild("Logo");
            if (bestText != null)
                MoveRootToSafeArea(bestText.transform.parent);
            MoveRootToSafeArea(startButton?.transform);
            MoveRootToSafeArea(growthButton?.transform);
            MoveRootToSafeArea(optionsButton?.transform);
        }

        void MoveDirectChild(string objectName)
        {
            Transform child = transform.Find(objectName);
            if (child == null)
                child = safeAreaRoot.Find(objectName);
            MoveRootToSafeArea(child);
        }

        void MoveRootToSafeArea(Transform child)
        {
            if (child == null || child == safeAreaRoot ||
                child == lobbyContentRoot ||
                child.parent == lobbyContentRoot)
                return;
            if (child.parent == transform || child.parent == safeAreaRoot)
                child.SetParent(lobbyContentRoot, false);
        }

        public void SetActiveMenu(LobbyMenuSelection selection)
        {
            if (activeMenu == selection) return;
            activeMenu = selection;
            LobbyMenuLayout.ApplySelectionEmphasis(
                startButton,
                selection == LobbyMenuSelection.Start);
            LobbyMenuLayout.ApplySelectionEmphasis(
                growthButton,
                selection == LobbyMenuSelection.Growth);
            LobbyMenuLayout.ApplySelectionEmphasis(
                optionsButton,
                selection == LobbyMenuSelection.Options);
        }

        void RefreshMenuSelection()
        {
            LobbyOptionsView options = ResolveOptionsView();
            if (options != null && options.IsOpen)
            {
                SetActiveMenu(LobbyMenuSelection.Options);
                return;
            }

            LobbyScreenNavigator navigator = ResolveScreenNavigator();
            if (navigator != null)
            {
                LobbyScreenNavigator.LobbySection section =
                    navigator.IsTransitioning
                        ? navigator.PendingSection
                        : navigator.CurrentSection;
                SetActiveMenu(SelectionForSection(section));
                return;
            }

            if (ResolvePermanentGrowthView()?.IsOpen == true)
                SetActiveMenu(LobbyMenuSelection.Growth);
            else
                SetActiveMenu(LobbyMenuSelection.Start);
        }

        static LobbyMenuSelection SelectionForSection(
            LobbyScreenNavigator.LobbySection section)
        {
            return section switch
            {
                LobbyScreenNavigator.LobbySection.PermanentGrowth =>
                    LobbyMenuSelection.Growth,
                _ => LobbyMenuSelection.Start,
            };
        }

#if UNITY_EDITOR
        public void ApplyMenuLayoutForTests()
        {
            EnsureMenuLayout();
        }
#endif

        void RefreshBest()
        {
            int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
            if (bestText == null || best == lastDisplayedBest) return;
            lastDisplayedBest = best;
            bestText.text = $"최고 {best}";
        }

        void SetVisible(bool visible, bool interactive)
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null ||
                lastVisible == visible &&
                lastInteractive == interactive &&
                canvasGroup.blocksRaycasts == interactive)
                return;
            lastVisible = visible;
            lastInteractive = interactive;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = interactive;
            canvasGroup.blocksRaycasts = interactive;
        }

        void ApplyUiFont()
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = InkPalette.UiFont;
                texts[i].fontStyle = FontStyle.Bold;
                texts[i].resizeTextForBestFit = false;
                texts[i].alignByGeometry = true;
            }
        }
    }
}
