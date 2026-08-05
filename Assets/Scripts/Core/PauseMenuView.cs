using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 플레이 세션은 그대로 보존하면서 계속하기와 로비 복귀를 제공하는 한지 일시정지판.
    /// 구형 Main 씬에서도 동작하도록 실제 UI는 최초 실행 시 지연 생성한다.
    public sealed class PauseMenuView : MonoBehaviour
    {
        const int CanvasSortingOrder = 1000;
        const float ShowDuration = 0.18f;
        const float HideDuration = 0.12f;
        static readonly Vector2 PanelDesignSize = new(760f, 680f);
        static readonly Vector2 PanelEdgePadding = new(28f, 32f);

        public static PauseMenuView Instance { get; private set; }

        Canvas rootCanvas;
        RectTransform pauseButtonRect;
        RectTransform overlayRoot;
        RectTransform safeAreaRoot;
        RectTransform panel;
        CanvasGroup overlayGroup;
        Button pauseButton;
        Button resumeButton;
        Button lobbyButton;
        GameManager boundManager;
        Coroutine visibilityRoutine;
        bool overlayVisible;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;
        float panelLayoutScale = 1f;

        void Awake()
        {
            if (Application.isPlaying)
                BuildIfNeeded();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            BuildIfNeeded();
            BindManager();
            BindButtons();
            RefreshImmediate();
        }

        void OnDisable()
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }
            UnbindButtons();
            UnbindManager();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            BindManager();
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != Screen.safeArea)
                ApplySafeArea();

            if (boundManager == null)
            {
                if (pauseButton != null) pauseButton.gameObject.SetActive(false);
                if (overlayVisible) SetOverlayVisible(false, false);
                return;
            }

            bool canPause = boundManager.State == GameState.Playing &&
                            !boundManager.IsPaused &&
                            !boundManager.IsTransitioning &&
                            visibilityRoutine == null;
            if (pauseButton != null && pauseButton.gameObject.activeSelf != canPause)
                pauseButton.gameObject.SetActive(canPause);
            bool menuPaused =
                boundManager.PauseReason == GameplayPauseReason.UserMenu;
            if (menuPaused != overlayVisible)
                SetOverlayVisible(menuPaused, true);
        }

        public static bool IsPointerOverControls(Vector2 screenPosition)
        {
            if (Instance == null) return false;
            if (Instance.overlayGroup != null &&
                Instance.overlayGroup.blocksRaycasts)
                return true;
            return Instance.pauseButtonRect != null &&
                   Instance.pauseButton != null &&
                   Instance.pauseButton.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       Instance.pauseButtonRect, screenPosition, null);
        }

        void BindManager()
        {
            var manager = GameManager.Instance;
            if (manager == boundManager) return;
            UnbindManager();
            boundManager = manager;
            if (boundManager != null)
                boundManager.PauseChanged += HandlePauseChanged;
        }

        void UnbindManager()
        {
            if (boundManager != null)
                boundManager.PauseChanged -= HandlePauseChanged;
            boundManager = null;
        }

        void BindButtons()
        {
            UnbindButtons();
            pauseButton?.onClick.AddListener(HandlePausePressed);
            resumeButton?.onClick.AddListener(HandleResumePressed);
            lobbyButton?.onClick.AddListener(HandleLobbyPressed);
        }

        void UnbindButtons()
        {
            pauseButton?.onClick.RemoveListener(HandlePausePressed);
            resumeButton?.onClick.RemoveListener(HandleResumePressed);
            lobbyButton?.onClick.RemoveListener(HandleLobbyPressed);
        }

        void HandlePausePressed()
        {
            boundManager?.PauseGame();
        }

        void HandleResumePressed()
        {
            boundManager?.ResumeGame();
        }

        void HandleLobbyPressed()
        {
            if (resumeButton != null) resumeButton.interactable = false;
            if (lobbyButton != null) lobbyButton.interactable = false;
            if (boundManager == null || !boundManager.ReturnToLobby())
            {
                if (resumeButton != null) resumeButton.interactable = true;
                if (lobbyButton != null) lobbyButton.interactable = true;
            }
        }

        void HandlePauseChanged(bool paused)
        {
            bool menuPaused = paused &&
                              boundManager != null &&
                              boundManager.PauseReason ==
                              GameplayPauseReason.UserMenu;
            if (!menuPaused)
            {
                if (resumeButton != null) resumeButton.interactable = true;
                if (lobbyButton != null) lobbyButton.interactable = true;
            }
            SetOverlayVisible(menuPaused, true);
            if (pauseButton != null)
                pauseButton.gameObject.SetActive(
                    boundManager != null &&
                    !boundManager.IsPaused &&
                    boundManager.State == GameState.Playing &&
                    !boundManager.IsTransitioning &&
                    visibilityRoutine == null);
        }

        void RefreshImmediate()
        {
            ApplySafeArea();
            bool menuPaused = boundManager != null &&
                              boundManager.PauseReason ==
                              GameplayPauseReason.UserMenu;
            SetOverlayVisible(menuPaused, false);
            if (pauseButton != null)
                pauseButton.gameObject.SetActive(
                    boundManager != null &&
                    boundManager.State == GameState.Playing &&
                    !boundManager.IsPaused &&
                    !boundManager.IsTransitioning);
        }

        void SetOverlayVisible(bool visible, bool animate)
        {
            overlayVisible = visible;
            if (overlayGroup == null || panel == null) return;
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            if (visible || !animate)
                overlayGroup.blocksRaycasts = visible;
            overlayGroup.interactable = visible;
            if (!animate || !Application.isPlaying)
            {
                overlayGroup.alpha = visible ? 1f : 0f;
                ApplyPanelPresentationScale(1f);
                return;
            }
            visibilityRoutine = StartCoroutine(AnimateVisibility(visible));
        }

        IEnumerator AnimateVisibility(bool visible)
        {
            float startAlpha = overlayGroup.alpha;
            float targetAlpha = visible ? 1f : 0f;
            float safeLayoutScale = Mathf.Max(0.01f, panelLayoutScale);
            float startScale = panel.localScale.x / safeLayoutScale;
            float targetScale = visible ? 1f : 0.98f;
            if (visible && startAlpha <= 0.001f)
            {
                startScale = 0.96f;
                ApplyPanelPresentationScale(startScale);
            }

            float duration = visible ? ShowDuration : HideDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = visible ? EaseOutCubic(progress) : Smooth01(progress);
                overlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                ApplyPanelPresentationScale(
                    Mathf.Lerp(startScale, targetScale, eased));
                yield return null;
            }

            overlayGroup.alpha = targetAlpha;
            ApplyPanelPresentationScale(targetScale);
            if (!visible)
                overlayGroup.blocksRaycasts = false;
            visibilityRoutine = null;
        }

        void BuildIfNeeded()
        {
            if (rootCanvas != null) return;

            var existing = transform.Find("PauseMenuCanvas");
            if (existing != null && RestoreExistingReferences(existing))
            {
                ApplySafeArea();
                return;
            }
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            var rootObject = new GameObject(
                "PauseMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            rootObject.transform.SetParent(transform, false);
            rootCanvas = rootObject.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = CanvasSortingOrder;
            rootCanvas.pixelPerfect = true;
            var scaler = rootObject.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            pauseButton = CreatePauseButton(rootObject.transform);
            pauseButtonRect = pauseButton.transform as RectTransform;

            overlayRoot = CreateStretchRect("PauseOverlay", rootObject.transform);
            overlayGroup = overlayRoot.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkDim", overlayRoot, new Color(0.035f, 0.032f, 0.028f, 0.56f));
            backdrop.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", overlayRoot);
            panel = CreateRect(
                "PauseScroll",
                safeAreaRoot,
                Vector2.zero,
                PanelDesignSize);

            BuildPauseScrollFrame(panel);

            var title = CreateText("Title", panel, "잠시 멈춤", 58,
                new Vector2(0f, 165f), new Vector2(520f, 78f),
                InkPalette.TextDark, FontStyle.Normal);
            title.alignment = TextAnchor.MiddleCenter;
            AddSoftWeight(title, InkPalette.Ink, 0.2f);

            resumeButton = CreateBrushButton("ResumeButton", panel, "계속하기",
                new Vector2(0f, 25f), true);
            lobbyButton = CreateBrushButton("LobbyButton", panel, "로비로",
                new Vector2(0f, -120f), false);
            ApplyActionPriority(resumeButton, 1f);
            ApplyActionPriority(lobbyButton, 0.72f);

            ApplySafeArea();
        }

        bool RestoreExistingReferences(Transform existing)
        {
            rootCanvas = existing.GetComponent<Canvas>();
            pauseButton = existing.Find("PauseButton")?.GetComponent<Button>();
            pauseButtonRect = pauseButton != null
                ? pauseButton.transform as RectTransform
                : null;
            overlayRoot = existing.Find("PauseOverlay") as RectTransform;
            overlayGroup = overlayRoot != null
                ? overlayRoot.GetComponent<CanvasGroup>()
                : null;
            safeAreaRoot = overlayRoot != null
                ? overlayRoot.Find("SafeAreaRoot") as RectTransform
                : null;
            panel = safeAreaRoot != null
                ? safeAreaRoot.Find("PauseScroll") as RectTransform
                : null;
            resumeButton = panel != null
                ? panel.Find("ResumeButton")?.GetComponent<Button>()
                : null;
            lobbyButton = panel != null
                ? panel.Find("LobbyButton")?.GetComponent<Button>()
                : null;
            bool complete = rootCanvas != null && pauseButton != null &&
                            pauseButton.transform.Find("Visual") != null &&
                            overlayRoot != null && overlayGroup != null &&
                            safeAreaRoot != null && panel != null &&
                            resumeButton != null && lobbyButton != null;
            if (!complete) return false;
            ApplyActionPriority(resumeButton, 1f);
            ApplyActionPriority(lobbyButton, 0.72f);
            EnableFullButtonRaycast(pauseButton);
            EnableFullButtonRaycast(resumeButton);
            EnableFullButtonRaycast(lobbyButton);
            return true;
        }

        static void BuildPauseScrollFrame(Transform parent)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var body = CreateRect(
                "ScrollBody",
                parent,
                Vector2.zero,
                new Vector2(700f, 560f));

            var shadow = CreateImage(
                "InkBleedShadow",
                body,
                brush,
                new Vector2(10f, -12f),
                new Vector2(580f, 720f),
                new Color(0f, 0f, 0f, 0.14f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollBodyOutline",
                body,
                brush,
                Vector2.zero,
                new Vector2(568f, 708f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "HanjiPaper",
                body,
                brush,
                Vector2.zero,
                new Vector2(548f, 680f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            // 붓 스프라이트 안쪽의 투명 섬유 틈만 막아 세로 빗줄기처럼 보이는
            // 아티팩트를 없앤다. 외곽의 수묵 붓결은 종이 레이어에서 유지된다.
            CreateImage(
                "PaperCore",
                body,
                null,
                Vector2.zero,
                new Vector2(620f, 520f),
                InkPalette.Paper);

            CreatePauseRoll(parent, 285f, true);
            CreatePauseRoll(parent, -285f, false);
        }

        static void CreatePauseRoll(Transform parent, float y, bool top)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(750f, 88f));

            CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(7f, -6f),
                new Vector2(724f, 72f),
                new Color(0f, 0f, 0f, 0.16f));
            var roll = CreateImage(
                "PaperRoll",
                root,
                brush,
                Vector2.zero,
                new Vector2(736f, 74f),
                InkPalette.Ink);
            CreateImage(
                "Paper",
                roll.transform,
                brush,
                Vector2.zero,
                new Vector2(710f, 54f),
                InkPalette.Paper2);
            CreateImage(
                "FoldShade",
                roll.transform,
                brush,
                new Vector2(0f, top ? -12f : 12f),
                new Vector2(670f, 7f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(
                    side < 0 ? "LeftCap" : "RightCap",
                    root,
                    blob,
                    new Vector2(side * 352f, 0f),
                    new Vector2(78f, 78f),
                    InkPalette.Ink);
                CreateImage(
                    "Paper",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(58f, 58f),
                    InkPalette.Paper2);
                CreateImage(
                    "Axis",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(18f, 18f),
                    InkPalette.Ink);
            }
        }

        Button CreatePauseButton(Transform parent)
        {
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            RectTransform hitSurface = CreateRect(
                "PauseButton",
                parent,
                Vector2.zero,
                new Vector2(
                    InkUiStyle.MinimumTapHeight,
                    InkUiStyle.MinimumTapHeight));
            var hitImage = hitSurface.gameObject.AddComponent<Image>();
            hitImage.color = Color.clear;
            hitImage.raycastTarget = true;
            var outer = CreateImage("Visual", hitSurface, blob, Vector2.zero,
                new Vector2(78f, 78f), InkPalette.Ink);
            var inner = CreateImage("Paper", outer.transform, blob, Vector2.zero,
                new Vector2(62f, 62f), InkPalette.Paper);
            var button = hitSurface.gameObject.AddComponent<Button>();
            button.targetGraphic = outer;
            EnableFullButtonRaycast(button);
            button.colors = ReadableButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            CreateImage("LeftBar", inner.transform, null, new Vector2(-8f, 0f),
                new Vector2(7f, 28f), InkPalette.Ink);
            CreateImage("RightBar", inner.transform, null, new Vector2(8f, 0f),
                new Vector2(7f, 28f), InkPalette.Ink);
            return button;
        }

        Button CreateBrushButton(
            string objectName, Transform parent, string label, Vector2 position, bool filled)
        {
            var outer = CreateImage(objectName, parent, null, position,
                new Vector2(580f, 104f), InkPalette.Ink);
            var button = outer.gameObject.AddComponent<Button>();
            var text = CreateText(
                "Label", outer.transform, label, filled ? 40 : 36,
                Vector2.zero,
                new Vector2(470f, 76f), InkPalette.Paper, FontStyle.Bold);
            AddSoftWeight(text, InkPalette.Ink, 0.14f);
            InkUiStyle.ConfigureActionButton(button, outer, text);
            EnableFullButtonRaycast(button);
            return button;
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || pauseButtonRect == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);

            float previousLayoutScale = Mathf.Max(0.01f, panelLayoutScale);
            float presentationScale = panel != null
                ? panel.localScale.x / previousLayoutScale
                : 1f;
            panelLayoutScale = MobileUiLayout.CalculateFitScale(
                PanelDesignSize,
                safe,
                Screen.width,
                Screen.height,
                PanelEdgePadding);
            if (panel != null)
            {
                panel.anchoredPosition = Vector2.zero;
                ApplyPanelPresentationScale(presentationScale);
            }

            pauseButtonRect.anchorMin = pauseButtonRect.anchorMax =
                new Vector2(
                    safe.xMax / Screen.width,
                    safe.yMax / Screen.height);
            pauseButtonRect.pivot = new Vector2(0.5f, 0.5f);
            // 가독성 보강으로 높아진 상단 HUD와 겹치지 않도록 한 칸 아래에 둔다.
            pauseButtonRect.anchoredPosition = new Vector2(-82f, -245f);
            pauseButtonRect.sizeDelta = new Vector2(
                InkUiStyle.MinimumTapHeight,
                InkUiStyle.MinimumTapHeight);

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        void ApplyPanelPresentationScale(float presentationScale)
        {
            if (panel == null) return;
            panel.localScale = Vector3.one *
                               (panelLayoutScale * presentationScale);
        }

        static RectTransform CreateRect(
            string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
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
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName, Transform parent, Sprite sprite, Vector2 position,
            Vector2 size, Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(string objectName, Transform parent, Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(
            string objectName, Transform parent, string value, int fontSize,
            Vector2 position, Vector2 size, Color color, FontStyle style)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            return text;
        }

        static void AddSoftWeight(Text text, Color color, float alpha)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(color.r, color.g, color.b, alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        static ColorBlock ReadableButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.93f, 0.88f, 1f);
            colors.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.5f, 0.48f, 0.44f, 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        static void EnableFullButtonRaycast(Button button)
        {
            if (button == null) return;
            var outer = button.GetComponent<Graphic>();
            if (outer != null) outer.raycastTarget = true;
            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = true;
        }

        static void ApplyActionPriority(Button button, float alpha)
        {
            if (button == null) return;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            Graphic background = button.targetGraphic;
            if (background == null)
                background = button.GetComponent<Graphic>();
            if (background == null) return;
            Color color = background.color;
            color.a = Mathf.Clamp01(alpha);
            background.color = color;
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
