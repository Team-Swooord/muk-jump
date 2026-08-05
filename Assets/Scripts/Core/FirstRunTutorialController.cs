using MukJump.Drawing;
using MukJump.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 첫 시작 직후 실제 플레이 위에서 핵심 조작을 한 가지씩 익히게 한다.
    /// 초반 30m 안전 구간 안에서 끝나며 게임 상태와 물리를 별도로 복제하지 않는다.
    [DisallowMultipleComponent]
    public sealed class FirstRunTutorialController : MonoBehaviour
    {
        public const float PanelDesignWidth = 900f;
        public const float PanelDesignHeight = 320f;
        public const float PanelEdgePadding = 24f;

        const int CanvasSortingOrder = 900;
        const float LandingHintTimeout = 6f;
        const float InformationPageDuration = 2.4f;
        const float SkipConfirmationSeconds = 2.5f;

        public static FirstRunTutorialController Instance { get; private set; }

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        RectTransform skipHitRect;
        Image topicIcon;
        Text titleText;
        Text descriptionText;
        Text progressText;
        Text skipLabel;
        GameManager manager;
        GameManager subscribedManager;
        StrokeCapture subscribedStrokeCapture;
        int currentStep = -1;
        float stepElapsed;
        float skipArmedUntil;
        bool pendingFirstRun;
        bool active;
        bool skipArmed;
        int lastScreenWidth;
        int lastScreenHeight;
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
        }

        void OnDisable()
        {
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
                return;
            if (manager == null || manager.State != GameState.Playing)
            {
                EndWithoutCompletion();
                return;
            }

            if (skipArmed && Time.unscaledTime > skipArmedUntil)
                ResetSkipConfirmation();
            if (!manager.IsGameplayTicking)
                return;

            stepElapsed += Time.unscaledDeltaTime;
            if (currentStep == 1 && stepElapsed >= LandingHintTimeout)
            {
                AdvanceStep();
                return;
            }

            if (currentStep >= 2 &&
                stepElapsed >= InformationPageDuration)
                AdvanceStep();
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
            return tutorial != null &&
                   tutorial.active &&
                   (tutorial.panel != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        tutorial.panel,
                        screenPosition,
                        null) ||
                    tutorial.skipHitRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        tutorial.skipHitRect,
                        screenPosition,
                        null));
        }

        void BindRuntimeSignals()
        {
            GameManager nextManager = GetComponent<GameManager>();
            if (nextManager == null)
                nextManager = GameManager.Instance;
            if (subscribedManager != nextManager)
            {
                if (subscribedManager != null)
                {
                    subscribedManager.StateChanged -= HandleStateChanged;
                    subscribedManager.PlayerLanded -= HandlePlayerLanded;
                }
                subscribedManager = nextManager;
                manager = nextManager;
                if (subscribedManager != null)
                {
                    subscribedManager.StateChanged += HandleStateChanged;
                    subscribedManager.PlayerLanded += HandlePlayerLanded;
                }
            }

            StrokeCapture nextCapture = GetComponent<StrokeCapture>();
            if (nextCapture == null)
                nextCapture = FindFirstObjectByType<StrokeCapture>();
            if (subscribedStrokeCapture == nextCapture)
                return;
            if (subscribedStrokeCapture != null)
                subscribedStrokeCapture.ValidStrokeCreated -=
                    HandleValidStrokeCreated;
            subscribedStrokeCapture = nextCapture;
            if (subscribedStrokeCapture != null)
                subscribedStrokeCapture.ValidStrokeCreated +=
                    HandleValidStrokeCreated;
        }

        void UnbindRuntimeSignals()
        {
            if (subscribedManager != null)
            {
                subscribedManager.StateChanged -= HandleStateChanged;
                subscribedManager.PlayerLanded -= HandlePlayerLanded;
            }
            if (subscribedStrokeCapture != null)
                subscribedStrokeCapture.ValidStrokeCreated -=
                    HandleValidStrokeCreated;
            subscribedManager = null;
            subscribedStrokeCapture = null;
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

        void HandleValidStrokeCreated(
            PlatformCollider platform,
            float validLength,
            float inkBudgetCost)
        {
            if (!active || currentStep != 0 || platform == null)
                return;
            AdvanceStep();
        }

        void HandlePlayerLanded(
            PlayerController player,
            PlatformCollider platform)
        {
            if (!active || currentStep != 1 || player == null ||
                platform == null || !platform.IsTemporaryDrawnPlatform)
                return;
            AdvanceStep();
        }

        void BeginTutorial()
        {
            pendingFirstRun = false;
            active = true;
            currentStep = 0;
            stepElapsed = 0f;
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
            stepElapsed = 0f;
            ResetSkipConfirmation();
            ShowCurrentPage();
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
                skipLabel.text = "한 번 더 눌러 건너뛰기";
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

            safeAreaRoot = CreateStretchRect(
                "SafeAreaRoot",
                root.transform);
            panel = CreateRect(
                "TutorialPanel",
                safeAreaRoot,
                new Vector2(0f, -145f),
                new Vector2(PanelDesignWidth, PanelDesignHeight),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f));

            var paper = panel.gameObject.AddComponent<Image>();
            paper.sprite = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_hanji_card");
            if (paper.sprite == null)
                paper.sprite = InkUiTextureFactory.CreateBlobSprite();
            paper.type = Image.Type.Simple;
            paper.color = new Color(
                InkPalette.Paper.r,
                InkPalette.Paper.g,
                InkPalette.Paper.b,
                0.97f);
            // 빈 한지 영역도 EventSystem 입력을 받아 아래 HUD 버튼으로 탭이 새지 않게 한다.
            // 월드 드로잉은 IsPointerOverControls에서 같은 Rect를 별도로 차단한다.
            paper.raycastTarget = true;

            topicIcon = CreateImage(
                "TopicIcon",
                panel,
                new Vector2(-338f, -148f),
                new Vector2(132f, 132f),
                Color.white,
                new Vector2(0.5f, 1f));
            topicIcon.preserveAspect = true;
            titleText = CreateText(
                "Title",
                panel,
                string.Empty,
                InkUiStyle.CardTitleSize,
                new Vector2(10f, -72f),
                new Vector2(500f, 70f),
                TextAnchor.MiddleLeft,
                true,
                new Vector2(0.5f, 1f));
            descriptionText = CreateText(
                "Description",
                panel,
                string.Empty,
                32,
                new Vector2(15f, -187f),
                new Vector2(550f, 118f),
                TextAnchor.MiddleLeft,
                false,
                new Vector2(0.5f, 1f));
            descriptionText.lineSpacing = 1.08f;
            progressText = CreateText(
                "Progress",
                panel,
                "1 / 5",
                InkUiStyle.CaptionSize,
                new Vector2(-330f, -276f),
                new Vector2(170f, 52f),
                TextAnchor.MiddleLeft,
                false,
                new Vector2(0.5f, 1f));

            RectTransform skipRect = CreateRect(
                "SkipButton",
                panel,
                new Vector2(322f, -64f),
                new Vector2(220f, InkUiStyle.MinimumTapHeight),
                new Vector2(0.5f, 1f));
            skipHitRect = skipRect;
            var skipBackground = skipRect.gameObject.AddComponent<Image>();
            var skipButton = skipRect.gameObject.AddComponent<Button>();
            skipLabel = CreateText(
                "Label",
                skipRect,
                "건너뛰기",
                InkUiStyle.CaptionSize,
                Vector2.zero,
                new Vector2(210f, 86f),
                TextAnchor.MiddleCenter,
                true);
            InkUiStyle.ConfigureActionButton(
                skipButton,
                skipBackground,
                skipLabel);
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
            return MobileUiLayout.CalculateWidthFitScale(
                PanelDesignWidth,
                safeArea,
                screenWidth,
                screenHeight,
                PanelEdgePadding);
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
            pendingFirstRun = true;
            BeginTutorial();
        }

        public void AdvanceForTests() => AdvanceStep();
#endif
    }
}
