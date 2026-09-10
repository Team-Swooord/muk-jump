using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비의 영구 성장 전용 화면.
    /// 복잡한 나무·갈래·장착 규칙 없이 네 장의 카드에서 힘을 골라 바로 키운다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        const float ResetConfirmationGuard = 0.35f;
        const float ResetConfirmationWindow = 3f;
        const int GrowthScreenTitleSize = 80;
        const int GrowthBalanceSize = 68;
        const int GrowthDistanceProgressSize = 44;
        const int GrowthTotalDistanceSize = 40;
        const int GrowthFocusTitleSize = 80;
        const int GrowthFocusSummarySize = 56;
        const int GrowthTabLabelSize = 56;
        const int GrowthTabEffectSize = 56;
        const int GrowthCostLabelSize = 56;
        const int GrowthPurchaseLabelSize = 80;
        const float GrowthTabWidth = 240f;
        const float GrowthTabHeight = 386f;
        const float GrowthTabSpacing = 260f;
        const float GrowthProgressMarkSize = 64f;
        const float GrowthProgressMarkGap = 80f;
        const float GrowthProgressMarkY = -150f;
        const string BalanceIconResourcePath =
            "MukJump/UI/PermanentGrowth/pg_inklight_sumukhwa_v1";
        const string BalanceIconFallbackResourcePath =
            "MukJump/UI/PermanentGrowth/pg_root_emblem";
        const string FocusRingResourcePath =
            "MukJump/UI/PermanentGrowth/pg_selected_ring";
        const string GrowthTitleResourcePath =
            "MukJump/UI/PermanentGrowth/pg_title_growth_ko_v1";
        public const string BrushIconResourcePath =
            "MukJump/UI/Growth/growth_brush_mini_v1";
        static readonly Vector2 HiddenScreenPosition = new(0f, ReferenceHeight);

        sealed class GrowthCardView
        {
            public PermanentGrowthChoiceDefinition Definition;
            public Image Icon;
            public Text Label;
            public Text EffectSummary;
            public Image SelectionInk;
            public Image SelectionWash;
            public Button Button;
        }

        sealed class GrowthProgressMarkView
        {
            public Image Drop;
        }

        readonly List<GrowthCardView> cards = new();
        readonly Dictionary<string, Sprite> spriteCache = new();
        Canvas rootCanvas;
        CanvasGroup rootGroup;
        GrowthBloomPresentation growthPresentation;
        RectTransform safeAreaRoot;
        RectTransform contentPanel;
        RectTransform recoveryPromptRoot;
        RectTransform recoverySafeAreaRoot;
        RectTransform recoveryContentPanel;
        RectTransform resetPromptRoot;
        RectTransform resetSafeAreaRoot;
        RectTransform resetContentPanel;
        HanjiScrollFrame resetPromptFrame;
        Text resetPromptMessage;
        Text balanceText;
        CanvasGroup balanceHelpGroup;
        RectTransform balanceHelpRect;
        Text balanceHelpReward;
        Text balanceHelpDetail;
        bool balanceHelpOpen;
        float balanceHelpProgress;
        Text screenTitleText;
        RawImage screenTitleArtwork;
        RectTransform distanceProgressRoot;
        Text distanceProgressText;
        Text totalDistanceText;
        Image distanceProgressFill;
        Image focusedGrowthIcon;
        Text focusedGrowthTitle;
        Text focusedGrowthSummary;
        Text focusedCompleteLabel;
        readonly List<GrowthProgressMarkView> focusedProgressMarks = new();
        [SerializeField] Texture2D purchaseButtonTexture;
        Text purchaseButtonText;
        Text selectedCostText;
        Image resetButtonIcon;
        Text recoveryMessageText;
        Text recoveryResetButtonText;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        float lastBannerInsetFraction = -1f;
        int selectedCardIndex;
        bool purchaseInProgress;
        bool purchaseUiLocked;
        float purchaseLockedUntil;
        bool resetConfirmationOpen;
        bool resetInProgress;
        bool recoveryResetArmed;
        float recoveryResetArmedAt;
        string recoveryMessageOverride = string.Empty;

        public bool IsOpen => rootGroup != null && rootGroup.blocksRaycasts;
        public Button BackButton { get; private set; }
        public Button BalanceInfoButton { get; private set; }
        public Button PurchaseButton { get; private set; }
        public Button NodeResetButton { get; private set; }
        public Button ConfirmResetButton { get; private set; }
        public Button CancelResetButton { get; private set; }
        public Button ResetDimmerButton { get; private set; }
        public bool IsResetConfirmationOpen => resetConfirmationOpen;
        public bool HasBlockingOverlay => IsResetModalBlocking || IsRecoveryPromptOpen;
        bool IsResetModalBlocking => resetConfirmationOpen || resetInProgress ||
            (resetPromptRoot != null && resetPromptRoot.gameObject.activeSelf);
        public Button RestoreBackupButton { get; private set; }
        public Button ResetGrowthSaveButton { get; private set; }
        // 구 호출부의 읽기 호환 표면. v8은 나무와 상세 팝업을 생성하지 않는다.
        public Button NodePopupDimmerButton => null;
        public RectTransform ScreenRoot { get; private set; }
        public RectTransform TreeViewport => null;
        public RectTransform TreeCanvas => null;
        public ScrollRect TreeScrollRect => null;
        public bool IsDedicatedScreen => ScreenRoot != null;
        public bool IsNodePopupOpen => false;
        public bool IsRecoveryPromptOpen =>
            recoveryPromptRoot != null && recoveryPromptRoot.gameObject.activeSelf;
        public int CreatedRowCount => cards.Count > 0 ? 1 : 0;
        public int CreatedNodeCount => cards.Count;
        public int CreatedCardCount => cards.Count;
        public int GridRowCount => cards.Count > 0 ? 1 : 0;
        public string BalanceLabel =>
            balanceText != null ? balanceText.text : string.Empty;
        public string DistanceProgressLabel => distanceProgressText != null ? distanceProgressText.text : string.Empty;
        public PermanentGrowthType SelectedGrowthType =>
            cards.Count > 0 && selectedCardIndex >= 0 &&
            selectedCardIndex < cards.Count
                ? cards[selectedCardIndex].Definition.Type
                : PermanentGrowthType.InkCapacity;
        public string SelectedNodeId =>
            cards.Count > 0 && selectedCardIndex >= 0 &&
            selectedCardIndex < cards.Count
                ? cards[selectedCardIndex].Definition.Id
                : string.Empty;

        void OnEnable()
        {
            BindManager();
            PermanentGrowthProfile.Changed += HandleProfileChanged;
            GameLocalization.Changed -= RefreshTitleArtwork;
            GameLocalization.Changed += RefreshTitleArtwork;
            RefreshTitleArtwork();
        }

        void OnDisable()
        {
            PermanentGrowthProfile.Changed -= HandleProfileChanged;
            GameLocalization.Changed -= RefreshTitleArtwork;
            UnbindManager();
            InkUiFeedbackController.CancelGrowthPresentation();
            if (growthPresentation != null) growthPresentation.Cancel();
            CloseImmediate();
        }

        void Update()
        {
            UpdateBalanceHelp();
            if (manager == null)
                BindManager();
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();
            if (purchaseUiLocked && Time.unscaledTime >= purchaseLockedUntil)
            {
                purchaseUiLocked = false;
                Refresh();
            }
            if (recoveryResetArmed &&
                Time.unscaledTime - recoveryResetArmedAt > ResetConfirmationWindow)
            {
                recoveryResetArmed = false;
                recoveryResetArmedAt = 0f;
                RefreshRecoveryPrompt();
            }
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                MobileUiLayout.CurrentSafeArea != lastSafeArea ||
                !Mathf.Approximately(lastBannerInsetFraction, LobbyAdLayout.GameplayTopInsetFraction))
                ApplySafeArea();
        }

        public void Open()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }
            if (MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
            {
                CloseImmediate();
                FindAnyObjectByType<LobbyOptionsView>()?
                    .OpenAccountForRequiredSync();
                return;
            }
            Refresh();
            SetVisible(true);
            MukJumpAnalytics.Screen(AnalyticsScreen.Growth);
        }

        public void Close()
        {
            CloseResetConfirmation(immediate: true);
            SetVisible(false);
        }

        public void BuildForTests()
        {
            BuildIfNeeded();
            Refresh();
            CloseImmediate();
        }

        void BindManager()
        {
            GameManager next = GameManager.Instance;
            if (ReferenceEquals(manager, next))
                return;
            UnbindManager();
            manager = next;
            if (manager != null)
                manager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Lobby)
                Close();
        }

        void HandleProfileChanged() => Refresh();

        void BuildIfNeeded()
        {
            if (rootGroup != null)
                return;
            Transform stale = transform.Find("PermanentGrowthCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "PermanentGrowthCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            rootCanvas = root.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = CanvasSortingOrder;
            rootCanvas.pixelPerfect = true;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            rootGroup = root.GetComponent<CanvasGroup>();

            ScreenRoot = CreateStretchRect("ScreenRoot", root.transform);
            ScreenRoot.anchoredPosition = HiddenScreenPosition;
            // 로비의 월드 산수화 배경을 그대로 노출한다. 투명 Image는 시각 요소를
            // 추가하지 않고 성장 화면 바깥에서 뒤쪽 로비 입력만 차단한다.
            Image backgroundBlocker = CreateStretchImage(
                "LobbyBackgroundInputBlocker", ScreenRoot, Color.clear);
            backgroundBlocker.raycastTarget = true;
            safeAreaRoot = CreateStretchRect("SafeAreaRoot", ScreenRoot);
            contentPanel = CreateRect(
                "PermanentGrowthScreen",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            BuildOrnaments(contentPanel);
            RectTransform header = CreateRect(
                "HeaderGroup", contentPanel, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            RectTransform focus = CreateRect(
                "FocusedGrowth", contentPanel, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            RectTransform grid = CreateRect(
                "ChoiceGrid", contentPanel, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            RectTransform actions = CreateRect(
                "BottomActions", contentPanel, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            BuildHeader(header);
            BuildFocusedGrowth(focus);
            BuildChoiceGrid(grid);
            BuildBottomActions(actions);
            BuildBalanceHelp();
            growthPresentation = GetComponent<GrowthBloomPresentation>() ??
                gameObject.AddComponent<GrowthBloomPresentation>();
            growthPresentation.Initialize(contentPanel);
            BuildResetConfirmation(ScreenRoot);
            BuildRecoveryPrompt(ScreenRoot);
            ApplySafeArea();
            Refresh();
        }

        void BuildOrnaments(Transform parent)
        {
            // 중앙 집중 아이콘의 장식만 별도 계층에 둔다. 선택 패널은 각 카드가 소유한다.
            RectTransform ornaments = CreateRect(
                "GrowthOrnaments", parent, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            CreateImage(
                "FocusPaperWash", ornaments,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 318f), new Vector2(430f, 360f),
                WithAlpha(InkPalette.TextLight, 0.72f));
            Image ring = CreateImage(
                "FocusBrushRing", ornaments,
                LoadResourceSprite(FocusRingResourcePath),
                new Vector2(0f, 318f), new Vector2(350f, 350f),
                WithAlpha(InkPalette.Ink, 0.26f));
            ring.preserveAspect = true;
            // 자산 누락 때 Image의 기본 흰 사각형이 나타나지 않게 한다.
            ring.enabled = ring.sprite != null;
        }

        void BuildHeader(Transform parent)
        {
            BackButton = CreateGrowthIconButton(
                "BackButton", parent, InkUiTextureFactory.CreateGrowthBackIconSprite(),
                new Vector2(-420f, 860f),
                new Vector2(220f, InkUiStyle.MinimumTapHeight), iconOffsetX: -45f);
            BackButton.onClick.AddListener(HandleBackRequested);
            screenTitleText = CreateText(
                "Title", parent, "성장", GrowthScreenTitleSize,
                new Vector2(0f, 860f), new Vector2(520f, 108f),
                InkPalette.TextDark, FontStyle.Bold);
            Texture2D titleTexture = Resources.Load<Texture2D>(GrowthTitleResourcePath);
            if (titleTexture != null)
            {
                Vector2 titleBounds = screenTitleText.rectTransform.sizeDelta;
                float fit = Mathf.Min(titleBounds.x / titleTexture.width, titleBounds.y / titleTexture.height);
                RectTransform artwork = CreateRect("TitleArtwork", screenTitleText.transform,
                    Vector2.zero, new Vector2(titleTexture.width, titleTexture.height) * fit);
                screenTitleArtwork = artwork.gameObject.AddComponent<RawImage>();
                screenTitleArtwork.texture = titleTexture;
                screenTitleArtwork.color = Color.white;
                screenTitleArtwork.raycastTarget = false;
            }
            RefreshTitleArtwork();
            NodeResetButton = CreateGrowthIconButton(
                "NodeResetButton", parent, InkUiTextureFactory.CreateGrowthResetIconSprite(),
                new Vector2(430f, 860f),
                new Vector2(200f, InkUiStyle.MinimumTapHeight), iconOffsetX: 35f);
            resetButtonIcon = NodeResetButton.transform.Find("Icon").GetComponent<Image>();
            NodeResetButton.onClick.AddListener(HandleResetRequested);
            RectTransform balanceHud = CreateRect(
                "BalanceHud", parent, new Vector2(415f, 720f),
                new Vector2(230f, 120f));
            // 새 원화 임포트 전에도 기존 먹빛을 유지하고, 모두 없으면 숫자만 표시한다.
            Sprite balanceSprite = LoadInklightSprite();
            Image balanceDrop = CreateImage(
                "InkDropIcon", balanceHud,
                balanceSprite,
                new Vector2(-64f, 0f), new Vector2(84f, 84f),
                Color.white);
            balanceDrop.preserveAspect = true;
            balanceDrop.enabled = balanceSprite != null;
            balanceText = CreateText(
                "Balance", balanceHud, string.Empty,
                GrowthBalanceSize,
                new Vector2(42f, 0f), new Vector2(136f, 100f),
                InkPalette.Red, FontStyle.Bold);
            Image balanceHitArea = balanceHud.gameObject.AddComponent<Image>();
            balanceHitArea.color = Color.clear;
            balanceHitArea.raycastTarget = true;
            BalanceInfoButton = balanceHud.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(BalanceInfoButton, balanceDrop);
            BalanceInfoButton.onClick.AddListener(ToggleBalanceHelp);
            distanceProgressRoot = CreateRect("DistanceExperience", parent,
                new Vector2(-150f, balanceHud.anchoredPosition.y), new Vector2(640f, 140f));
            // 두 문구와 막대의 전체 높이를 보유 먹빛 중심에 맞추고, 막대 위아래에 9px씩 둔다.
            distanceProgressText = CreateText("Progress", distanceProgressRoot, string.Empty,
                GrowthDistanceProgressSize,
                new Vector2(0f, 42f), new Vector2(640f, 56f), InkPalette.TextDark, FontStyle.Bold);
            distanceProgressText.alignment = TextAnchor.MiddleLeft;
            Sprite stroke = InkUiTextureFactory.CreateBrushSprite();
            CreateImage("Track", distanceProgressRoot, stroke, new Vector2(0f, -2f),
                new Vector2(640f, 14f), WithAlpha(InkPalette.Ink, .18f));
            distanceProgressFill = CreateImage("Fill", distanceProgressRoot, stroke, new Vector2(0f, -2f),
                new Vector2(640f, 14f), InkPalette.Ink);
            distanceProgressFill.type = Image.Type.Filled;
            distanceProgressFill.fillMethod = Image.FillMethod.Horizontal;
            distanceProgressFill.fillOrigin = 0;
            totalDistanceText = CreateText("Total", distanceProgressRoot, string.Empty,
                GrowthTotalDistanceSize,
                new Vector2(0f, -44f), new Vector2(640f, 52f), InkPalette.TextMuted, FontStyle.Normal);
            totalDistanceText.alignment = TextAnchor.MiddleLeft;
        }

        void BuildBalanceHelp()
        {
            // 다른 성장 그림보다 앞에, 보유 먹빛 바로 아래에 작은 한지 안내를 띄운다.
            balanceHelpRect = CreateRect("BalanceHelp", contentPanel,
                new Vector2(415f, 646f), new Vector2(520f, 176f));
            balanceHelpRect.pivot = new Vector2(.78f, 1f);
            balanceHelpGroup = balanceHelpRect.gameObject.AddComponent<CanvasGroup>();
            Image paper = CreateStretchImage("Paper", balanceHelpRect, Color.white);
            InkUiStyle.ConfigureHanjiSurface(paper);
            if (paper.sprite == null) paper.color = InkPalette.Paper;
            paper.raycastTarget = true;
            balanceHelpReward = CreateText("Reward", balanceHelpRect, string.Empty, 40,
                new Vector2(0f, 30f), new Vector2(464f, 60f), InkPalette.TextDark, FontStyle.Bold);
            balanceHelpDetail = CreateText("Detail", balanceHelpRect, string.Empty, 36,
                new Vector2(0f, -32f), new Vector2(464f, 64f), InkPalette.TextDark, FontStyle.Normal);
            RefreshBalanceHelp();
            SetBalanceHelpOpen(false, immediate: true);
        }

        void RefreshBalanceHelp()
        {
            if (balanceHelpReward == null) return;
            bool complete = PermanentGrowthProfile.IsDistanceJourneyComplete;
            InkLocalizedText.SetSource(balanceHelpReward, complete ? "먹빛 획득 완료"
                : $"다음 먹빛까지 {PermanentGrowthProfile.DistanceToNextRewardMeters}m");
            InkLocalizedText.SetSource(balanceHelpDetail, complete ? "거리 보상을 모두 받았어요."
                : "매 판 오른 높이가 합산돼요.");
        }

        void ToggleBalanceHelp()
        {
            if (!IsOpen || HasBlockingOverlay) return;
            RefreshBalanceHelp();
            SetBalanceHelpOpen(!balanceHelpOpen);
        }

        void SetBalanceHelpOpen(bool open, bool immediate = false)
        {
            balanceHelpOpen = open;
            if (balanceHelpGroup == null) return;
            balanceHelpGroup.blocksRaycasts = open;
            balanceHelpGroup.interactable = false;
            if (open) balanceHelpRect.gameObject.SetActive(true);
            if (!immediate) return;
            balanceHelpProgress = open ? 1f : 0f;
            balanceHelpGroup.alpha = balanceHelpProgress;
            balanceHelpRect.localScale = Vector3.one;
            balanceHelpRect.gameObject.SetActive(open);
        }

        void UpdateBalanceHelp()
        {
            if (balanceHelpGroup == null || !balanceHelpRect.gameObject.activeSelf) return;
            if (!IsOpen || HasBlockingOverlay)
            {
                SetBalanceHelpOpen(false, immediate: true);
                return;
            }
            // 바깥 터치는 닫기만 하고 소비하지 않아 뒤로가기·성장 선택도 한 번에 된다.
            if (balanceHelpOpen && PointerInput.WasPressedThisFrame() &&
                PointerInput.TryGetPressed(out Vector2 point) &&
                !RectTransformUtility.RectangleContainsScreenPoint(balanceHelpRect, point, null) &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)BalanceInfoButton.transform, point, null))
                SetBalanceHelpOpen(false);
            balanceHelpProgress = Mathf.MoveTowards(balanceHelpProgress, balanceHelpOpen ? 1f : 0f,
                Time.unscaledDeltaTime / (balanceHelpOpen ? .16f : .12f));
            float eased = 1f - Mathf.Pow(1f - balanceHelpProgress, 3f);
            balanceHelpGroup.alpha = eased;
            balanceHelpRect.localScale = Vector3.one * (LobbySettingsProfile.ReducedMotionEnabled
                ? 1f : Mathf.Lerp(.97f, 1f, eased));
            if (!balanceHelpOpen && balanceHelpProgress <= 0f)
                balanceHelpRect.gameObject.SetActive(false);
        }

        void RefreshTitleArtwork()
        {
            if (screenTitleText == null) return;
            // 원화는 글자만 담는다. 영문·원화 누락 시에는 기존 번역 제목을 즉시 사용한다.
            bool showArtwork = GameLocalization.Language == GameLanguage.Korean && screenTitleArtwork != null &&
                screenTitleArtwork.texture != null;
            screenTitleText.enabled = !showArtwork;
            if (screenTitleArtwork != null) screenTitleArtwork.enabled = showArtwork;
        }

        void BuildFocusedGrowth(Transform parent)
        {
            Image inkWash = CreateImage(
                "FocusInkWash", parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 318f), new Vector2(430f, 360f),
                WithAlpha(InkPalette.Ink, 0.08f));
            inkWash.preserveAspect = false;

            focusedGrowthIcon = CreateImage(
                "FocusIcon", parent, null,
                new Vector2(0f, 318f), new Vector2(310f, 310f),
                Color.white);
            focusedGrowthIcon.preserveAspect = true;
            focusedGrowthTitle = CreateText(
                "FocusTitle", parent, string.Empty, GrowthFocusTitleSize,
                new Vector2(0f, 78f), new Vector2(760f, 120f),
                InkPalette.TextDark, FontStyle.Bold);
            Image titleUnderline = CreateImage(
                "FocusTitleUnderline", parent, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 5f), new Vector2(150f, 5f),
                InkPalette.Red);
            titleUnderline.raycastTarget = false;
            focusedGrowthSummary = CreateText(
                "FocusSummary", parent, string.Empty, GrowthFocusSummarySize,
                new Vector2(0f, -52f), new Vector2(900f, 100f),
                InkPalette.TextDark, FontStyle.Normal);

            const int maximumProgressMarks = 8;
            Sprite progressSprite = LoadInklightSprite();
            for (int index = 0; index < maximumProgressMarks; index++)
            {
                Image mark = CreateImage(
                    $"FocusGrowthMark{index}", parent,
                    progressSprite,
                    new Vector2(0f, GrowthProgressMarkY),
                    Vector2.one * GrowthProgressMarkSize,
                    WithAlpha(Color.white, 0.32f));
                mark.preserveAspect = true;
                mark.enabled = progressSprite != null;
                // 잔액과 같은 붓방울 원화를 쓰고 미성장은 먹 농도만 낮춘다.
                // 종이색 속 그림을 겹치면 원화의 갈필·농담이 사라지므로 만들지 않는다.
                focusedProgressMarks.Add(new GrowthProgressMarkView
                {
                    Drop = mark,
                });
            }
            focusedCompleteLabel = CreateText(
                "FocusCompleteLabel", parent, "다 자랐어요",
                GrowthFocusSummarySize,
                new Vector2(0f, -150f), new Vector2(420f, 80f),
                InkPalette.Red, FontStyle.Bold);
            focusedCompleteLabel.gameObject.SetActive(false);
        }

        void BuildChoiceGrid(Transform parent)
        {
            IReadOnlyList<PermanentGrowthChoiceDefinition> choices =
                PermanentGrowthCatalog.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                int capturedIndex = i;
                GrowthCardView card = CreateChoiceTab(
                    parent, choices[i], i,
                    new Vector2((i - (choices.Count - 1) * .5f) * GrowthTabSpacing, -405f));
                card.Button.onClick.AddListener(() => SelectCard(capturedIndex));
                cards.Add(card);
            }
            selectedCardIndex = cards.Count > 0 ? 0 : -1;
        }

        GrowthCardView CreateChoiceTab(
            Transform parent,
            PermanentGrowthChoiceDefinition definition,
            int index,
            Vector2 position)
        {
            RectTransform root = CreateRect(
                $"GrowthCard{index}", parent, position,
                new Vector2(GrowthTabWidth, GrowthTabHeight));
            Image hitArea = root.gameObject.AddComponent<Image>();
            hitArea.sprite = null;
            hitArea.type = Image.Type.Simple;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, hitArea);

            // 기존 한지·원형 갈필 테두리를 네 카드에 그대로 공유한다.
            // 테두리의 바깥 번짐까지 계산해 종이 안쪽에 두고 카드 사이 여백을 남긴다.
            Image paper = CreateImage("Paper", root, InkUiTextureFactory.CreateGrowthPaperRibbonSprite(),
                Vector2.zero, new Vector2(GrowthTabWidth, GrowthTabHeight),
                WithAlpha(Color.Lerp(InkPalette.Paper, InkPalette.TextLight, .35f), .94f));
            paper.type = Image.Type.Simple;
            var frame = CreateRect("BrushFrame", root, Vector2.zero,
                new Vector2(GrowthTabWidth - 36f, GrowthTabHeight - 36f)).gameObject.AddComponent<GrowthRingFrameGraphic>();
            frame.sprite = LoadResourceSprite(FocusRingResourcePath);
            frame.color = WithAlpha(InkPalette.Ink, .26f);
            frame.raycastTarget = false;
            frame.enabled = frame.sprite != null;

            Image selectionWash = CreateImage(
                "SelectionWash", root, InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 14f), new Vector2(204f, 84f),
                WithAlpha(InkPalette.Red, 0.20f));
            Image icon = CreateImage(
                "Icon", root, LoadChoiceIcon(definition.Type),
                new Vector2(0f, 108f), new Vector2(108f, 108f),
                Color.white);
            icon.preserveAspect = true;
            // 누를 때는 아이콘만 살짝 짙어지고 공통 눌림 피드백은 유지한다.
            // 종이·먹 테두리 농도는 바꾸지 않고 카드 전체가 터치를 받는다.
            button.targetGraphic = icon;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.66f, 0.58f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.65f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;
            Text label = CreateText(
                "TabLabel", root, GetShortLabel(definition.Type),
                GrowthTabLabelSize,
                new Vector2(0f, 14f), new Vector2(190f, 72f),
                InkPalette.TextDark, FontStyle.Bold);
            Image selectionInk = CreateImage(
                "SelectionInk", root, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, -29f), new Vector2(120f, 6f),
                InkPalette.Red);
            Text effectSummary = CreateText(
                "TabEffectSummary", root,
                GetTabEffectSummary(definition.Type),
                GrowthTabEffectSize,
                new Vector2(0f, -99f), new Vector2(190f, 132f),
                InkPalette.TextDark, FontStyle.Normal);

            return new GrowthCardView
            {
                Definition = definition,
                Icon = icon,
                Label = label,
                EffectSummary = effectSummary,
                SelectionInk = selectionInk,
                SelectionWash = selectionWash,
                Button = button,
            };
        }

        void BuildBottomActions(Transform parent)
        {
            selectedCostText = CreateText(
                "SelectedCost", parent, string.Empty, GrowthCostLabelSize,
                new Vector2(0f, -670f), new Vector2(720f, 80f),
                InkPalette.TextDark, FontStyle.Bold);
            BuildPurchaseButton(parent);
            PurchaseButton.onClick.AddListener(HandlePurchase);
        }

        void BuildPurchaseButton(Transform parent)
        {
            // 넓어진 붓과 터치 영역 위에는 비용행과 30px 간격을 남긴다.
            Vector2 size = new(820f, 200f);
            RectTransform root = CreateRect("PurchaseButton", parent, new Vector2(0f, -840f), size);
            Vector2 scale = new(size.x / LobbyMenuLayout.BackgroundSize.x,
                size.y / LobbyMenuLayout.BackgroundSize.y);
            // 시작 버튼의 비대칭 먹 붓 원본을 그대로 쓰되, 글씨의 시각 중심은 화면 중앙에 둔다.
            RectTransform brush = CreateRect("BrushBackground", root,
                new Vector2(-LobbyMenuLayout.BrushArtworkLabelOffsetX * scale.x, 0f), size);
            RawImage background = brush.gameObject.AddComponent<RawImage>();
            if (purchaseButtonTexture == null)
            {
                // 기존 Main 씬도 재생성 없이 로비에 이미 연결된 같은 원본을 재사용한다.
                LobbyView lobby = FindAnyObjectByType<LobbyView>(FindObjectsInactive.Include);
                purchaseButtonTexture = lobby?.StartButton?.GetComponent<RawImage>()?.texture as Texture2D;
            }
            background.texture = purchaseButtonTexture != null
                ? purchaseButtonTexture : InkUiTextureFactory.CreateBrushSprite().texture;
            background.color = Color.white;
            PurchaseButton = root.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(PurchaseButton, background);
            purchaseButtonText = CreateText("Label", root, "성장하기", GrowthPurchaseLabelSize,
                new Vector2(0f, LobbyMenuLayout.LabelPosition.y * scale.y),
                Vector2.Scale(LobbyMenuLayout.LabelSize, scale), InkPalette.TextLight, FontStyle.Bold);
        }

        void BuildResetConfirmation(Transform parent)
        {
            resetPromptRoot = CreateStretchRect("GrowthResetConfirmation", parent);
            // 딤과 종이는 형제로 둬 종이 안의 빈 곳을 눌러도 취소로 전달되지 않게 한다.
            Image dim = CreateStretchImage("Dim", resetPromptRoot, InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);
            ResetDimmerButton = dim.gameObject.AddComponent<Button>();
            ResetDimmerButton.targetGraphic = dim;
            ResetDimmerButton.transition = Selectable.Transition.None;
            ResetDimmerButton.onClick.AddListener(HandleResetCancelled);
            resetSafeAreaRoot = CreateStretchRect("SafeAreaRoot", resetPromptRoot);
            resetContentPanel = CreateRect("PopupContent", resetSafeAreaRoot, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            RectTransform panel = CreateRect("ResetPanel", resetContentPanel, Vector2.zero,
                new Vector2(820f, 600f));
            Image paperInput = panel.gameObject.AddComponent<Image>();
            paperInput.color = Color.clear;
            paperInput.raycastTarget = true;
            resetPromptFrame = HanjiScrollFrame.Attach(panel, new Vector2(780f, 560f));
            CreateText("ResetTitle", panel, "초기화하시겠습니까?", 64,
                new Vector2(0f, 140f), new Vector2(690f, 100f),
                InkPalette.TextDark, FontStyle.Bold);
            resetPromptMessage = CreateText("ResetMessage", panel,
                "사용한 먹빛은 모두 돌려받습니다.", 42,
                new Vector2(0f, 22f), new Vector2(660f, 120f),
                InkPalette.TextDark, FontStyle.Normal);
            CancelResetButton = CreateActionButton("CancelResetButton", panel, "취소",
                new Vector2(-185f, -160f), new Vector2(320f, 120f), ActionButtonRole.Secondary);
            ConfirmResetButton = CreateActionButton("ConfirmResetButton", panel, "초기화",
                new Vector2(185f, -160f), new Vector2(320f, 120f), ActionButtonRole.Primary);
            CancelResetButton.onClick.AddListener(HandleResetCancelled);
            ConfirmResetButton.onClick.AddListener(HandleResetConfirmed);
            resetPromptRoot.gameObject.SetActive(false);
        }

        void BuildRecoveryPrompt(Transform parent)
        {
            recoveryPromptRoot = CreateStretchRect("GrowthRecoveryPrompt", parent);
            Image blocker = recoveryPromptRoot.gameObject.AddComponent<Image>();
            InkUiStyle.ConfigurePopupDim(blocker);
            recoverySafeAreaRoot = CreateStretchRect("SafeAreaRoot", recoveryPromptRoot);
            recoveryContentPanel = CreateRect(
                "PopupContent", recoverySafeAreaRoot, Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            RectTransform panel = CreateRect(
                "RecoveryPanel", recoveryContentPanel, Vector2.zero,
                new Vector2(820f, 660f));
            HanjiScrollFrame.Attach(panel, new Vector2(780, 620));
            CreateText(
                "RecoveryTitle", panel, "성장 저장 확인", 60,
                new Vector2(0f, 218f), new Vector2(680f, 84f),
                InkPalette.TextDark, FontStyle.Bold);
            recoveryMessageText = CreateText(
                "RecoveryMessage", panel, string.Empty,
                42,
                new Vector2(0f, 66f), new Vector2(660f, 210f),
                InkPalette.TextDark, FontStyle.Normal);
            RestoreBackupButton = CreateActionButton(
                "RestoreBackupButton", panel, "저장 복구",
                new Vector2(-190f, -194f),
                new Vector2(330f, InkUiStyle.MinimumTapHeight),
                ActionButtonRole.Primary);
            RestoreBackupButton.onClick.AddListener(HandleRestoreBackup);
            ResetGrowthSaveButton = CreateActionButton(
                "ResetGrowthSaveButton", panel, "새로 시작",
                new Vector2(190f, -194f),
                new Vector2(330f, InkUiStyle.MinimumTapHeight),
                ActionButtonRole.Secondary);
            recoveryResetButtonText = ResetGrowthSaveButton.transform
                .Find("Label")?.GetComponent<Text>();
            ResetGrowthSaveButton.onClick.AddListener(HandleResetGrowthSave);
            recoveryPromptRoot.gameObject.SetActive(false);
        }

        void SelectCard(int index)
        {
            if (index < 0 || index >= cards.Count || purchaseInProgress ||
                purchaseUiLocked || IsResetModalBlocking || Time.unscaledTime < purchaseLockedUntil)
                return;
            selectedCardIndex = index;
            Refresh();
        }

        void HandlePurchase()
        {
            if (selectedCardIndex < 0 || selectedCardIndex >= cards.Count ||
                IsResetModalBlocking || purchaseInProgress || purchaseUiLocked ||
                Time.unscaledTime < purchaseLockedUntil ||
                PermanentGrowthProfile.RequiresRecovery)
                return;
            GrowthCardView card = cards[selectedCardIndex];
            PermanentGrowthType type = card.Definition.Type;
            int previousLevel = PermanentGrowthProfile.GetLevel(type);
            PermanentGrowthNodeDefinition next =
                PermanentGrowthCatalog.GetNode(type, previousLevel + 1);
            if (next == null || !TryPurchaseWithoutReentry(next))
            {
                Refresh();
                return;
            }
            float duration = LobbySettingsProfile.ReducedMotionEnabled ? 0f : GrowthBloomPresentation.Duration;
            purchaseLockedUntil = Time.unscaledTime + duration;
            purchaseUiLocked = duration > 0f;
            Refresh();
            // 저장 성공 뒤 현재 성장 원화와 이번에 채워진 먹방울에서만 연출한다.
            RectTransform filledMark = previousLevel < focusedProgressMarks.Count
                ? focusedProgressMarks[previousLevel].Drop.rectTransform : null;
            if (growthPresentation != null)
            {
                var marks = new RectTransform[focusedProgressMarks.Count];
                for (int i = 0; i < marks.Length; i++) marks[i] = focusedProgressMarks[i].Drop.rectTransform;
                growthPresentation.Play(focusedGrowthIcon.rectTransform, filledMark, marks);
            }
        }

        bool TryPurchaseWithoutReentry(PermanentGrowthNodeDefinition node)
        {
            purchaseInProgress = true;
            try
            {
                return PermanentGrowthProfile.TryPurchaseNode(node);
            }
            finally
            {
                purchaseInProgress = false;
            }
        }

        void HandleResetRequested()
        {
            if (IsResetModalBlocking || purchaseInProgress || purchaseUiLocked ||
                Time.unscaledTime < purchaseLockedUntil || PermanentGrowthProfile.RequiresRecovery ||
                PermanentGrowthProfile.OwnedNodeCount <= 0)
                return;
            resetConfirmationOpen = true;
            InkLocalizedText.SetSource(resetPromptMessage, "사용한 먹빛은 모두 돌려받습니다.");
            resetPromptRoot.SetAsLastSibling();
            resetPromptFrame.ResetPresentation();
            resetPromptRoot.gameObject.SetActive(true);
            Refresh();
        }

        void HandleResetCancelled()
        {
            if (!resetConfirmationOpen || resetInProgress) return;
            CloseResetConfirmation();
            Refresh();
        }

        void HandleResetConfirmed()
        {
            if (!resetConfirmationOpen || resetInProgress || purchaseInProgress || purchaseUiLocked ||
                PermanentGrowthProfile.RequiresRecovery || PermanentGrowthProfile.OwnedNodeCount <= 0 ||
                (Application.isPlaying && !resetPromptFrame.IsReady))
                return;
            // 저장 콜백 재진입·연속 확인으로 환급이 중복되지 않도록 먼저 잠근다.
            var dispersalPositions = new List<Vector3>();
            foreach (var mark in focusedProgressMarks)
                if (mark.Drop.gameObject.activeInHierarchy && mark.Drop.color.a > .5f)
                    dispersalPositions.Add(mark.Drop.rectTransform.position);
            resetInProgress = true;
            Refresh();
            bool reset;
            try { reset = PermanentGrowthProfile.TryResetPurchasedNodes(); }
            finally { resetInProgress = false; }
            if (!reset)
            {
                InkLocalizedText.SetSource(resetPromptMessage, "초기화하지 못했어요. 다시 시도해 주세요.");
                Refresh();
                return;
            }
            selectedCardIndex = 0;
            CloseResetConfirmation(immediate: true);
            Refresh();
            growthPresentation?.PlayReset(focusedGrowthIcon.rectTransform, dispersalPositions.ToArray());
        }

        void CloseResetConfirmation(bool immediate = false)
        {
            resetConfirmationOpen = false;
            if (resetPromptRoot == null) return;
            ConfirmResetButton.interactable = CancelResetButton.interactable = ResetDimmerButton.interactable = false;
            if (!immediate && resetPromptRoot.gameObject.activeSelf && resetPromptFrame != null)
            {
                resetPromptFrame.Close(() =>
                {
                    resetPromptRoot.gameObject.SetActive(false);
                    Refresh();
                });
            }
            else
            {
                resetPromptFrame?.ResetPresentation();
                resetPromptRoot.gameObject.SetActive(false);
            }
        }

        void Refresh()
        {
            if (cards.Count == 0 || balanceText == null)
                return;
            bool recoveryBlocked = PermanentGrowthProfile.RequiresRecovery;
            if (resetConfirmationOpen && !resetInProgress &&
                (recoveryBlocked || PermanentGrowthProfile.OwnedNodeCount <= 0))
                CloseResetConfirmation(immediate: true);
            bool resetBlocked = IsResetModalBlocking;
            if (BalanceInfoButton != null)
                BalanceInfoButton.interactable = !recoveryBlocked && !resetBlocked;
            if (recoveryBlocked || resetBlocked) SetBalanceHelpOpen(false, immediate: true);
            RefreshBalanceHelp();
            if (ConfirmResetButton != null)
            {
                ConfirmResetButton.interactable = CancelResetButton.interactable =
                    ResetDimmerButton.interactable = resetConfirmationOpen && !resetInProgress;
            }
            InkLocalizedText.SetSource(balanceText, PermanentGrowthProfile.Currency.ToString());
            distanceProgressRoot.gameObject.SetActive(!recoveryBlocked);
            long distanceProgress = PermanentGrowthProfile.DistanceRewardProgressMeters;
            InkLocalizedText.SetSource(distanceProgressText, PermanentGrowthProfile.IsDistanceJourneyComplete
                ? "먹빛 획득 완료"
                : $"먹빛 +1 · {distanceProgress} / {PermanentGrowthProfile.DistanceRewardIntervalMeters}m");
            InkLocalizedText.SetSource(totalDistanceText,
                $"누적 {PermanentGrowthProfile.CumulativeDistanceMeters.ToString("N0", CultureInfo.InvariantCulture)}m");
            distanceProgressFill.fillAmount = (float)distanceProgress / PermanentGrowthProfile.DistanceRewardIntervalMeters;
            for (int i = 0; i < cards.Count; i++)
            {
                GrowthCardView card = cards[i];
                bool selected = i == selectedCardIndex;
                card.SelectionInk.gameObject.SetActive(selected);
                card.SelectionWash.gameObject.SetActive(selected);
                card.EffectSummary.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                card.Label.color = selected
                    ? InkPalette.Red
                    : InkPalette.TextDark;
                card.Button.interactable = !resetBlocked && !purchaseUiLocked && !recoveryBlocked;
            }
            bool hasSelection = selectedCardIndex >= 0 &&
                                selectedCardIndex < cards.Count;
            GrowthCardView selectedCard = hasSelection
                ? cards[selectedCardIndex]
                : null;
            PermanentGrowthType type = hasSelection
                ? selectedCard.Definition.Type
                : PermanentGrowthType.InkCapacity;
            int level = hasSelection
                ? PermanentGrowthProfile.GetLevel(type)
                : 0;
            int maximumLevel = hasSelection
                ? PermanentGrowthCatalog.Get(type)?.MaxLevel ?? 0
                : 0;
            bool completeSelection = hasSelection && maximumLevel > 0 &&
                                     level >= maximumLevel;
            focusedGrowthIcon.sprite = hasSelection
                ? selectedCard.Icon.sprite
                : null;
            focusedGrowthIcon.gameObject.SetActive(hasSelection);
            InkLocalizedText.SetSource(focusedGrowthTitle, hasSelection
                ? selectedCard.Definition.DisplayName
                : string.Empty);
            InkLocalizedText.SetSource(focusedGrowthSummary, hasSelection
                ? selectedCard.Definition.Summary
                : string.Empty);
            focusedCompleteLabel.gameObject.SetActive(completeSelection);
            for (int markIndex = 0;
                 markIndex < focusedProgressMarks.Count;
                 markIndex++)
            {
                GrowthProgressMarkView mark = focusedProgressMarks[markIndex];
                bool visible = !completeSelection && markIndex < maximumLevel;
                mark.Drop.gameObject.SetActive(visible);
                if (visible)
                {
                    // 각 성장의 실제 최대 단계 수로 가운데 정렬한다.
                    mark.Drop.rectTransform.anchoredPosition = new Vector2(
                        (markIndex - (maximumLevel - 1) * 0.5f) * GrowthProgressMarkGap,
                        GrowthProgressMarkY);
                    bool owned = markIndex < level;
                    mark.Drop.color = owned
                        ? Color.white
                        : WithAlpha(Color.white, 0.32f);
                }
            }
            int nextCost = hasSelection
                ? PermanentGrowthProfile.GetNextCost(type)
                : 0;
            bool affordable = nextCost > 0 &&
                              PermanentGrowthProfile.Currency >= nextCost;
            // 비용·상태는 버튼 위에서 읽고, 버튼에는 행동만 남긴다.
            // 완료/복구 상태를 0개 필요나 무료 성장으로 표현하지 않는다.
            InkLocalizedText.SetSource(selectedCostText, recoveryBlocked
                ? "저장 복구가 필요해요"
                : !hasSelection
                    ? "힘을 선택하세요"
                    : completeSelection
                        ? "다 자랐어요"
                        : $"먹빛 {nextCost}개 필요");
            selectedCostText.color = recoveryBlocked ||
                (hasSelection && !completeSelection && !affordable)
                    ? InkPalette.Red
                    : InkPalette.TextDark;
            InkLocalizedText.SetSource(purchaseButtonText, "성장하기");
            resetButtonIcon.sprite = InkUiTextureFactory.CreateGrowthResetIconSprite();
            resetButtonIcon.color = InkPalette.TextDark;
            PurchaseButton.interactable =
                !resetBlocked && !recoveryBlocked && !purchaseUiLocked &&
                hasSelection && !completeSelection && affordable;
            // 시작 버튼과 같은 먹 붓 눌림·비활성 색을 쓰고 밝은 글씨는 계속 선명하게 유지한다.
            purchaseButtonText.color = InkPalette.TextLight;
            NodeResetButton.interactable =
                !resetBlocked && !purchaseUiLocked && !recoveryBlocked &&
                PermanentGrowthProfile.OwnedNodeCount > 0;
            BackButton.interactable = !resetBlocked && !purchaseUiLocked && !recoveryBlocked;
            RefreshRecoveryPrompt();
        }

        void HandleBackRequested()
        {
            if (IsResetModalBlocking)
            {
                HandleResetCancelled();
                return;
            }
            if (Time.unscaledTime < purchaseLockedUntil)
                return;
            LobbyScreenNavigator navigator = LobbyScreenNavigator.Instance != null
                ? LobbyScreenNavigator.Instance
                : FindAnyObjectByType<LobbyScreenNavigator>();
            if (navigator != null && navigator.ReturnToLobby())
                return;
            Close();
        }

        void HandleRestoreBackup()
        {
            recoveryResetArmed = false;
            recoveryResetArmedAt = 0f;
            if (!PermanentGrowthProfile.TryRestoreBackup())
            {
                recoveryMessageOverride =
                    "저장 복구를 완료하지 못했습니다.\n잠시 뒤 다시 시도하세요.";
                RefreshRecoveryPrompt();
                return;
            }
            recoveryMessageOverride = string.Empty;
            Refresh();
        }

        void HandleResetGrowthSave()
        {
            if (!PermanentGrowthProfile.RequiresRecovery)
                return;
            if (!recoveryResetArmed)
            {
                recoveryMessageOverride = string.Empty;
                recoveryResetArmed = true;
                recoveryResetArmedAt = Time.unscaledTime;
                RefreshRecoveryPrompt();
                return;
            }
            if (Time.unscaledTime - recoveryResetArmedAt < ResetConfirmationGuard)
                return;
            if (!PermanentGrowthProfile.TryResetAfterLoadFailure())
            {
                recoveryResetArmed = false;
                recoveryResetArmedAt = 0f;
                recoveryMessageOverride =
                    "새 기록을 시작하지 못했습니다.\n잠시 뒤 다시 시도하세요.";
                RefreshRecoveryPrompt();
                return;
            }
            recoveryMessageOverride = string.Empty;
            recoveryResetArmed = false;
            recoveryResetArmedAt = 0f;
            Refresh();
        }

        void RefreshRecoveryPrompt()
        {
            if (recoveryPromptRoot == null)
                return;
            bool visible = PermanentGrowthProfile.RequiresRecovery;
            HanjiScrollFrame.SetActiveAnimated(recoveryPromptRoot.gameObject, visible);
            if (!visible)
            {
                recoveryResetArmed = false;
                recoveryResetArmedAt = 0f;
                recoveryMessageOverride = string.Empty;
                return;
            }
            bool canRestore = PermanentGrowthProfile.CanRestoreBackup;
            RestoreBackupButton.gameObject.SetActive(canRestore);
            RectTransform resetRect = ResetGrowthSaveButton.transform as RectTransform;
            resetRect.anchoredPosition = canRestore
                ? new Vector2(190f, -194f)
                : new Vector2(0f, -194f);
            InkUiStyle.SetActionButtonRole(
                RestoreBackupButton.GetComponent<Image>(),
                canRestore && !recoveryResetArmed
                    ? ActionButtonRole.Primary
                    : ActionButtonRole.Secondary);
            InkUiStyle.SetActionButtonRole(
                ResetGrowthSaveButton.GetComponent<Image>(),
                recoveryResetArmed || !canRestore
                    ? ActionButtonRole.Primary
                    : ActionButtonRole.Secondary);
            InkLocalizedText.SetSource(recoveryResetButtonText, recoveryResetArmed
                ? "초기화 확인"
                : "새로 시작");
            InkLocalizedText.SetSource(recoveryMessageText, !string.IsNullOrEmpty(recoveryMessageOverride)
                ? recoveryMessageOverride
                : recoveryResetArmed
                    ? "기존 저장은 복구용으로 보존됩니다.\n" +
                      "정말 새 기록으로 시작하려면 한 번 더 누르세요."
                    : canRestore
                        ? "성장 저장을 안전하게 잠갔습니다.\n" +
                          "검증된 백업을 복원하거나 새 기록으로 시작하세요."
                        : "성장 저장을 읽지 못했습니다. 원본은 보존됩니다.\n" +
                          "새 기록으로 시작하려면 아래에서 확인하세요.");
            recoveryPromptRoot.SetAsLastSibling();
        }

        public void SetNavigationPresentation(bool visible, bool interactive)
        {
            if (!visible && rootGroup == null)
                return;
            if (visible)
                BuildIfNeeded();
            if (rootGroup == null || ScreenRoot == null)
                return;
            if (!visible)
            {
                purchaseUiLocked = false;
                purchaseLockedUntil = 0f;
                CloseResetConfirmation(immediate: true);
                InkUiFeedbackController.CancelGrowthPresentation();
            }
            if (visible)
            {
                BindManager();
                if (manager == null || manager.State != GameState.Lobby)
                {
                    visible = false;
                    interactive = false;
                }
                else
                {
                    Refresh();
                }
            }
            if (!visible || !interactive)
            {
                SetBalanceHelpOpen(false, immediate: true);
                if (growthPresentation != null) growthPresentation.Cancel();
            }
            ScreenRoot.anchoredPosition = visible
                ? Vector2.zero
                : HiddenScreenPosition;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible && interactive;
            rootGroup.blocksRaycasts = visible && interactive;
            if (rootCanvas != null)
                rootCanvas.enabled = visible;
            ApplySafeArea();
        }

        void SetVisible(bool visible) => SetNavigationPresentation(visible, visible);

        void CloseImmediate()
        {
            CloseResetConfirmation(immediate: true);
            SetVisible(false);
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            Rect contentSafe = CalculateContentSafeArea(safe, Screen.width, Screen.height);
            ApplySafeAreaContent(safeAreaRoot, contentPanel, contentSafe, alignTop: true);
            ApplySafeAreaContent(recoverySafeAreaRoot, recoveryContentPanel, safe);
            ApplySafeAreaContent(resetSafeAreaRoot, resetContentPanel, safe);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
            lastBannerInsetFraction = LobbyAdLayout.GameplayTopInsetFraction;
        }

        public static Rect CalculateContentSafeArea(Rect safeArea, int width, int height)
        {
            if (width <= 0 || height <= 0) return Rect.zero;
            Rect safe = MobileUiLayout.SanitizeSafeArea(safeArea, width, height);
            // 노치는 Safe Area가 이미 제외했다. 그 아래 배너 높이만 한 번 뺀다.
            // 팝업·화면 전환 때 광고가 잠시 숨겨져도 본문 위치는 유지한다.
            safe.height = Mathf.Max(1f, safe.height - height * LobbyAdLayout.GameplayTopInsetFraction);
            return safe;
        }

        static void ApplySafeAreaContent(
            RectTransform safeRoot,
            RectTransform fittedContent,
            Rect safe,
            bool alignTop = false)
        {
            if (safeRoot == null || fittedContent == null)
                return;
            MobileUiLayout.ApplySafeArea(
                safeRoot, safe, Screen.width, Screen.height);
            if (safe.width <= 0f || safe.height <= 0f)
                return;
            float scale = MobileUiLayout.CalculateFitScale(
                new Vector2(ReferenceWidth, ReferenceHeight),
                safe, Screen.width, Screen.height, Vector2.zero);
            // 긴 휴대폰에서 남는 세로 공간은 아래로 보낸다. 확인 팝업은 중앙을 유지한다.
            float topOffset = alignTop
                ? (safe.height * ReferenceHeight / Screen.height - ReferenceHeight * scale) * .5f
                : 0f;
            fittedContent.anchoredPosition = new Vector2(0f, topOffset);
            fittedContent.localScale = Vector3.one * scale;
        }

        Sprite LoadChoiceIcon(PermanentGrowthType type)
        {
            string path = type switch
            {
                PermanentGrowthType.Vitality =>
                    "MukJump/UI/Growth/growth_vitality",
                PermanentGrowthType.InkCapacity =>
                    "MukJump/UI/PermanentGrowth/pg_icon_capacity",
                PermanentGrowthType.InkBudgetEfficiency =>
                    BrushIconResourcePath,
                PermanentGrowthType.JumpHeight =>
                    "MukJump/UI/PermanentGrowth/pg_icon_jump",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(path))
                return null;
            return LoadResourceSprite(path);
        }

        static string GetShortLabel(PermanentGrowthType type) => type switch
        {
            PermanentGrowthType.Vitality => "먹",
            PermanentGrowthType.InkCapacity => "먹물",
            PermanentGrowthType.InkBudgetEfficiency => "붓",
            PermanentGrowthType.JumpHeight => "도약",
            _ => string.Empty,
        };

        /// 카드 이름 아래에는 긴 설명 대신 한 번 키울 때의 실제 변화만 보여준다.
        /// 카탈로그 수치를 직접 읽어 밸런스 조정 뒤에도 UI 숫자가 어긋나지 않게 한다.
        static string GetTabEffectSummary(PermanentGrowthType type)
        {
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(type);
            if (definition == null)
                return string.Empty;

            string subject = type switch
            {
                PermanentGrowthType.Vitality => "체력",
                PermanentGrowthType.InkCapacity => "최대 먹물",
                PermanentGrowthType.InkBudgetEfficiency => "먹물 소모",
                PermanentGrowthType.JumpHeight => "점프",
                _ => string.Empty,
            };
            float amount = definition.GetDisplayValueAtLevel(1);
            string value = amount.ToString(
                "0.##", CultureInfo.InvariantCulture);
            string sign = definition.ReducesValue ? "-" : "+";
            string suffix = definition.ValueKind ==
                            PermanentGrowthValueKind.Percent
                ? "%"
                : type == PermanentGrowthType.Vitality
                    ? "칸"
                    : string.Empty;
            // 작은 한 줄에 수치를 밀어 넣지 않고 큰 두 줄로 읽는다. 값은 카탈로그를 유지한다.
            return $"{subject}\n{sign}{value}{suffix}";
        }

        Sprite LoadInklightSprite() => LoadResourceSprite(BalanceIconResourcePath) ??
            LoadResourceSprite(BalanceIconFallbackResourcePath);

        Sprite LoadResourceSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (spriteCache.TryGetValue(path, out Sprite cached))
                return cached;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null;
            }
            if (sprite != null)
                spriteCache[path] = sprite;
            return sprite;
        }

#if UNITY_EDITOR
        public void SelectGrowthForTests(int index) => SelectCard(index);

        public void SelectGrowthForTests(string id)
        {
            PermanentGrowthNodeDefinition node = PermanentGrowthCatalog.GetNode(id);
            for (int i = 0; i < cards.Count; i++)
            {
                if (!string.Equals(cards[i].Definition.Id, id, StringComparison.Ordinal) &&
                    (node == null || cards[i].Definition.Type != node.Type))
                    continue;
                SelectCard(i);
                return;
            }
        }

        public static float CalculateTreeZoomForTests(
            float screenWidth,
            float screenHeight) => 1f;
#endif

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreateRect(objectName, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(
            string objectName,
            Transform parent,
            Color color)
        {
            RectTransform rect = CreateStretchRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            FontStyle style)
        {
            RectTransform rect = CreateRect(objectName, parent, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(text, value);
            text.color = color;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                TextAnchor.MiddleCenter,
                strong: style is FontStyle.Bold or FontStyle.BoldAndItalic,
                wrap: true);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button CreateActionButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            ActionButtonRole role)
        {
            Image border = CreateImage(
                objectName, parent, null, position, size, InkPalette.Ink);
            border.raycastTarget = true;
            Button button = border.gameObject.AddComponent<Button>();
            Text text = CreateText(
                "Label", border.transform, label,
                InkUiStyle.ActionButtonLabelSize,
                Vector2.zero, size - new Vector2(64f, 32f),
                InkPalette.TextDark, FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(button, border, text, role);
            return button;
        }

        // 넓은 터치 영역은 안전 영역 안에 유지하고, 먹선 아이콘만 양끝으로 옮긴다.
        static Button CreateGrowthIconButton(string objectName, Transform parent, Sprite sprite,
            Vector2 position, Vector2 size, float iconOffsetX)
        {
            Image hitArea = CreateImage(objectName, parent, null, position, size, Color.clear);
            hitArea.raycastTarget = true;
            Image icon = CreateImage("Icon", hitArea.transform, sprite, new Vector2(iconOffsetX, 0f),
                new Vector2(84f, 84f), InkPalette.TextDark);
            icon.preserveAspect = true;
            Button button = hitArea.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, icon);
            icon.raycastTarget = false;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, .32f);
            button.colors = colors;
            return button;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
