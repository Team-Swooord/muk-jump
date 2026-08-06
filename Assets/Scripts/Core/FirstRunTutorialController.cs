using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 첫 시작 직후 게임 시간을 멈추고 핵심 규칙을 5장 설명 팝업으로 익히게 한다.
    /// 마지막 장을 닫은 뒤에만 자동 점프와 월드 진행을 다시 시작한다.
    [DisallowMultipleComponent]
    public sealed class FirstRunTutorialController : MonoBehaviour
    {
        public const float PanelDesignWidth = 820f;
        public const float PanelDesignHeight = 1180f;
        public const float PanelEdgePadding = 24f;

        const int CanvasSortingOrder = 1200;
        const float SkipConfirmationSeconds = 2.5f;

        public static FirstRunTutorialController Instance { get; private set; }

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        Image topicIcon;
        Text titleText;
        Text descriptionText;
        Text progressText;
        Text skipLabel;
        Text nextLabel;
        Button previousButton;
        Button nextButton;
        GameManager manager;
        GameManager subscribedManager;
        int currentStep = -1;
        float skipArmedUntil;
        bool pendingFirstRun;
        bool autoStartFirstVisit;
        bool autoStartAttempted;
        bool active;
        bool skipArmed;
        bool ownsTutorialPause;
        int lastScreenWidth;
        int lastScreenHeight;
        int autoStartEarliestFrame;
        Rect lastSafeArea;

        public bool IsActive => active;
        public int CurrentStep => currentStep;
        public GameplayTutorialTopic CurrentTopic =>
            currentStep >= 0 && currentStep < GameplayTutorialCatalog.Count
                ? GameplayTutorialCatalog.Get(currentStep).Topic
                : GameplayTutorialTopic.DrawInk;

        void Awake()
        {
            BuildIfNeeded();
            SetVisible(false);
        }

        void OnEnable()
        {
            Instance = this;
            BuildIfNeeded();
            BindRuntimeSignals();
            autoStartFirstVisit =
                LobbySettingsProfile.ShouldAutoStartGameplayTutorial;
            autoStartEarliestFrame = Time.frameCount + 1;
        }

        void OnDisable()
        {
            ReleaseTutorialPause();
            UnbindRuntimeSignals();
            pendingFirstRun = false;
            active = false;
            SetVisible(false);
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            BindRuntimeSignals();
            RefreshResponsiveLayoutIfNeeded();

            if (!active)
            {
                TryAutoStartFirstVisit();
                return;
            }
            if (manager == null || manager.State != GameState.Playing)
            {
                EndWithoutCompletion();
                return;
            }

            if (ownsTutorialPause &&
                manager.PauseReason != GameplayPauseReason.FirstRunTutorial)
            {
                EndWithoutCompletion();
                return;
            }

            if (skipArmed && Time.unscaledTime > skipArmedUntil)
                ResetSkipConfirmation();
        }

        /// 최초 설치 프로필만 로비를 한 프레임 보여 준 뒤 자동으로 게임에 진입한다.
        /// 실제 팝업은 Playing 전환 뒤에 열려 로비가 아니라 게임 월드 위에 표시된다.
        void TryAutoStartFirstVisit()
        {
            if (!autoStartFirstVisit || autoStartAttempted ||
                pendingFirstRun || Time.frameCount < autoStartEarliestFrame)
                return;

            manager ??= GameManager.Instance;
            if (manager == null || manager.State != GameState.Lobby ||
                manager.IsTransitioning ||
                PermanentGrowthProfile.RequiresRecovery)
                return;

            LobbyScreenNavigator navigator =
                LobbyScreenNavigator.Instance != null
                    ? LobbyScreenNavigator.Instance
                    : FindFirstObjectByType<LobbyScreenNavigator>();
            if (navigator != null && !navigator.CanStartGame)
                return;

            autoStartAttempted = true;
            autoStartFirstVisit = false;
            manager.StartGameFromMenu();
        }

        /// 실제 시작 진입점이 복구 검사를 통과한 뒤 호출한다.
        public bool PrepareForGameStart()
        {
            pendingFirstRun = LobbySettingsProfile.NeedsGameplayTutorial;
            return pendingFirstRun;
        }

        public static bool IsPointerOverControls(Vector2 screenPosition)
        {
            FirstRunTutorialController tutorial = Instance;
            // 전체 화면 dim이 입력을 소유하므로 팝업 바깥도 월드 먹선으로 전달하지 않는다.
            return tutorial != null && tutorial.active;
        }

        void BindRuntimeSignals()
        {
            GameManager nextManager = GetComponent<GameManager>();
            if (nextManager == null)
                nextManager = GameManager.Instance;
            if (subscribedManager != nextManager)
            {
                if (subscribedManager != null)
                    subscribedManager.StateChanged -= HandleStateChanged;
                subscribedManager = nextManager;
                manager = nextManager;
                if (subscribedManager != null)
                    subscribedManager.StateChanged += HandleStateChanged;
            }
        }

        void UnbindRuntimeSignals()
        {
            if (subscribedManager != null)
                subscribedManager.StateChanged -= HandleStateChanged;
            subscribedManager = null;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing && pendingFirstRun)
            {
                BeginTutorial();
                return;
            }
            if (current != GameState.Playing && active)
                EndWithoutCompletion();
        }

        void BeginTutorial()
        {
            pendingFirstRun = false;
            manager ??= GameManager.Instance;
            ownsTutorialPause =
                manager != null && manager.PauseForFirstRunTutorial();
            if (manager != null && !ownsTutorialPause)
            {
                EndWithoutCompletion();
                return;
            }
            active = true;
            currentStep = 0;
            ResetSkipConfirmation();
            ShowCurrentPage();
            SetVisible(true);
        }

        void AdvanceStep()
        {
            if (!active)
                return;
            if (currentStep >= GameplayTutorialCatalog.Count - 1)
            {
                CompleteTutorial(false);
                return;
            }

            currentStep++;
            ResetSkipConfirmation();
            ShowCurrentPage();
        }

        void PreviousStep()
        {
            if (!active || currentStep <= 0)
                return;
            currentStep--;
            ResetSkipConfirmation();
            ShowCurrentPage();
        }

        void HandleNextPressed()
        {
            if (!active)
                return;
            if (currentStep >= GameplayTutorialCatalog.Count - 1)
            {
                CompleteTutorial(true);
                return;
            }
            AdvanceStep();
        }

        void HandleSkipPressed()
        {
            if (!active)
                return;
            if (!skipArmed)
            {
                skipArmed = true;
                skipArmedUntil = Time.unscaledTime +
                                 SkipConfirmationSeconds;
                skipLabel.text = "다시 눌러 확인";
                return;
            }

            CompleteTutorial(true);
        }

        void CompleteTutorial(bool suppressPointerUntilRelease)
        {
            if (!active)
                return;
            active = false;
            pendingFirstRun = false;
            currentStep = GameplayTutorialCatalog.Count;
            ResetSkipConfirmation();
            SetVisible(false);
            ReleaseTutorialPause();
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            if (suppressPointerUntilRelease)
                PointerInput.SuppressUntilRelease();
        }

        void EndWithoutCompletion()
        {
            active = false;
            pendingFirstRun = false;
            currentStep = -1;
            ResetSkipConfirmation();
            SetVisible(false);
            ReleaseTutorialPause();
        }

        void ReleaseTutorialPause()
        {
            if (!ownsTutorialPause)
                return;
            if (manager != null)
                manager.ResumeFirstRunTutorial();
            ownsTutorialPause = false;
        }

        void ResetSkipConfirmation()
        {
            skipArmed = false;
            skipArmedUntil = 0f;
            if (skipLabel != null)
                skipLabel.text = "건너뛰기";
        }

        void ShowCurrentPage()
        {
            if (currentStep < 0 ||
                currentStep >= GameplayTutorialCatalog.Count)
                return;
            GameplayTutorialPage page =
                GameplayTutorialCatalog.Get(currentStep);
            titleText.text = page.Title;
            descriptionText.text = page.Description;
            progressText.text =
                $"{currentStep + 1} / {GameplayTutorialCatalog.Count}";
            previousButton.interactable = currentStep > 0;
            nextLabel.text =
                currentStep == GameplayTutorialCatalog.Count - 1
                    ? "시작하기"
                    : "다음";
            topicIcon.sprite = Resources.Load<Sprite>(
                page.SpriteResourcePath);
            topicIcon.color = topicIcon.sprite != null
                ? Color.white
                : InkPalette.Ink;
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null)
                return;

            Transform stale = transform.Find("FirstRunTutorialCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "FirstRunTutorialCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            MobileUiLayout.ConfigurePortraitScaler(
                root.GetComponent<CanvasScaler>());
            rootGroup = root.GetComponent<CanvasGroup>();

            Image dim = CreateStretchImage(
                "TutorialDim",
                root.transform,
                InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);

            safeAreaRoot = CreateStretchRect(
                "SafeAreaRoot",
                root.transform);
            panel = CreateRect(
                "TutorialPanel",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(PanelDesignWidth, PanelDesignHeight));

            Sprite cardSprite = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_hanji_card");
            if (cardSprite == null)
                cardSprite = InkUiTextureFactory.CreateBlobSprite();
            CreateImage(
                "Shadow",
                panel,
                new Vector2(0f, -12f),
                new Vector2(830f, 1190f),
                WithAlpha(InkPalette.Ink, 0.28f)).sprite = cardSprite;
            CreateImage(
                "Outline",
                panel,
                Vector2.zero,
                new Vector2(PanelDesignWidth, PanelDesignHeight),
                InkPalette.Ink).sprite = cardSprite;
            Image paper = CreateImage(
                "Paper",
                panel,
                Vector2.zero,
                new Vector2(812f, 1172f),
                new Color(
                    InkPalette.Paper.r,
                    InkPalette.Paper.g,
                    InkPalette.Paper.b,
                    0.99f));
            paper.sprite = cardSprite;
            paper.type = Image.Type.Simple;
            paper.raycastTarget = true;

            progressText = CreateText(
                "Progress",
                panel,
                "1 / 5",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 440f),
                new Vector2(180f, 58f),
                TextAnchor.MiddleCenter,
                false);

            Image iconPaper = CreateImage(
                "TopicIconPaper",
                panel,
                new Vector2(0f, 265f),
                new Vector2(280f, 260f),
                new Color(
                    InkPalette.Paper2.r,
                    InkPalette.Paper2.g,
                    InkPalette.Paper2.b,
                    0.94f));
            iconPaper.sprite = InkUiTextureFactory.CreateBlobSprite();
            topicIcon = CreateImage(
                "TopicIcon",
                panel,
                new Vector2(0f, 265f),
                new Vector2(200f, 200f),
                Color.white);
            topicIcon.preserveAspect = true;
            titleText = CreateText(
                "Title",
                panel,
                string.Empty,
                InkUiStyle.CardTitleSize,
                new Vector2(0f, 95f),
                new Vector2(700f, 72f),
                TextAnchor.MiddleCenter,
                true);
            descriptionText = CreateText(
                "Description",
                panel,
                string.Empty,
                InkUiStyle.BodySize,
                new Vector2(0f, -65f),
                new Vector2(700f, 230f),
                TextAnchor.MiddleCenter,
                false);
            descriptionText.lineSpacing = 1.12f;
            CreateText(
                "PauseHint",
                panel,
                "안내 후 게임이 시작돼요",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -220f),
                new Vector2(700f, 46f),
                TextAnchor.MiddleCenter,
                false);

            previousButton = CreateBrushButton(
                "PreviousButton",
                panel,
                "이전",
                new Vector2(-190f, -305f),
                new Vector2(300f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            previousButton.onClick.AddListener(PreviousStep);
            nextButton = CreateBrushButton(
                "NextButton",
                panel,
                "다음",
                new Vector2(190f, -305f),
                new Vector2(320f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            nextLabel = nextButton.transform
                .Find("Label")?.GetComponent<Text>();
            nextButton.onClick.AddListener(HandleNextPressed);

            Button skipButton = CreateBrushButton(
                "SkipButton",
                panel,
                "건너뛰기",
                new Vector2(0f, -425f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            skipLabel = skipButton.transform
                .Find("Label")?.GetComponent<Text>();
            skipButton.onClick.AddListener(HandleSkipPressed);

            ApplySafeAreaAndScale();
        }

        void RefreshResponsiveLayoutIfNeeded()
        {
            if (lastScreenWidth == Screen.width &&
                lastScreenHeight == Screen.height &&
                lastSafeArea == Screen.safeArea)
                return;
            ApplySafeAreaAndScale();
        }

        void ApplySafeAreaAndScale()
        {
            if (safeAreaRoot == null || panel == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);
            float scale = CalculatePanelScaleForTests(
                safe,
                Screen.width,
                Screen.height);
            panel.localScale = Vector3.one * scale;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        void SetVisible(bool visible)
        {
            if (rootGroup == null)
                return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
        }

        static RectTransform CreateStretchRect(
            string objectName,
            Transform parent)
        {
            var rect = new GameObject(
                objectName,
                typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Vector2? anchor = null,
            Vector2? pivot = null)
        {
            var rect = new GameObject(
                objectName,
                typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            Vector2? anchor = null)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                position,
                size,
                anchor);
            var image = rect.gameObject.AddComponent<Image>();
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
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            Image background = CreateImage(
                objectName,
                parent,
                position,
                size,
                InkPalette.Ink);
            var button = background.gameObject.AddComponent<Button>();
            Text labelText = CreateText(
                "Label",
                background.transform,
                label,
                fontSize,
                Vector2.zero,
                size - new Vector2(36f, 14f),
                TextAnchor.MiddleCenter,
                true);
            InkUiStyle.ConfigureActionButton(
                button,
                background,
                labelText);
            return button;
        }

        static void CreateDivider(
            Transform parent,
            string objectName,
            float y,
            float width)
        {
            CreateImage(
                objectName,
                parent,
                new Vector2(0f, y),
                new Vector2(width, 2f),
                WithAlpha(InkPalette.Ink, 0.2f));
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment,
            bool strong,
            Vector2? anchor = null)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                position,
                size,
                anchor);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = InkPalette.TextDark;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                alignment,
                strong,
                true);
            return text;
        }

        public static float CalculatePanelScaleForTests(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            return MobileUiLayout.CalculateFitScale(
                new Vector2(PanelDesignWidth, PanelDesignHeight),
                safeArea,
                screenWidth,
                screenHeight,
                Vector2.one * PanelEdgePadding);
        }

#if UNITY_EDITOR
        public void BuildForTests()
        {
            BuildIfNeeded();
            ApplySafeAreaAndScale();
        }

        public void BeginForTests()
        {
            BuildIfNeeded();
            // EditMode에서는 MonoBehaviour.OnEnable 호출 순서가 런타임과 다를 수 있다.
            // 전체 화면 입력 차단의 정적 진입점도 실제 실행과 동일하게 연결한다.
            Instance = this;
            pendingFirstRun = true;
            BeginTutorial();
        }

        public void AdvanceForTests() => AdvanceStep();
#endif
    }
}
