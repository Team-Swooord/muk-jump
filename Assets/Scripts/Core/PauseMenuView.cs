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
            if (boundManager.IsPaused != overlayVisible)
                SetOverlayVisible(boundManager.IsPaused, true);
        }

        public static bool IsPointerOverControls(Vector2 screenPosition)
        {
            if (Instance == null) return false;
            if (Instance.overlayVisible &&
                Instance.overlayGroup != null &&
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
            if (!paused)
            {
                if (resumeButton != null) resumeButton.interactable = true;
                if (lobbyButton != null) lobbyButton.interactable = true;
            }
            SetOverlayVisible(paused, true);
            if (pauseButton != null)
                pauseButton.gameObject.SetActive(
                    !paused && boundManager != null &&
                    boundManager.State == GameState.Playing &&
                    !boundManager.IsTransitioning &&
                    visibilityRoutine == null);
        }

        void RefreshImmediate()
        {
            ApplySafeArea();
            bool paused = boundManager != null && boundManager.IsPaused;
            SetOverlayVisible(paused, false);
            if (pauseButton != null)
                pauseButton.gameObject.SetActive(
                    boundManager != null &&
                    boundManager.State == GameState.Playing &&
                    !paused &&
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
                panel.localScale = Vector3.one;
                return;
            }
            visibilityRoutine = StartCoroutine(AnimateVisibility(visible));
        }

        IEnumerator AnimateVisibility(bool visible)
        {
            float startAlpha = overlayGroup.alpha;
            float targetAlpha = visible ? 1f : 0f;
            float startScale = panel.localScale.x;
            float targetScale = visible ? 1f : 0.98f;
            if (visible && startAlpha <= 0.001f)
            {
                startScale = 0.96f;
                panel.localScale = Vector3.one * startScale;
            }

            float duration = visible ? ShowDuration : HideDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = visible ? EaseOutCubic(progress) : Smooth01(progress);
                overlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                panel.localScale = Vector3.one *
                                   Mathf.Lerp(startScale, targetScale, eased);
                yield return null;
            }

            overlayGroup.alpha = targetAlpha;
            panel.localScale = Vector3.one * targetScale;
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            pauseButton = CreatePauseButton(rootObject.transform);
            pauseButtonRect = pauseButton.transform as RectTransform;

            overlayRoot = CreateStretchRect("PauseOverlay", rootObject.transform);
            overlayGroup = overlayRoot.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkDim", overlayRoot, new Color(0.035f, 0.032f, 0.028f, 0.58f));
            backdrop.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", overlayRoot);
            panel = CreateRect("PauseScroll", safeAreaRoot, Vector2.zero,
                new Vector2(800f, 680f));

            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            CreateImage("InkBorder", panel, blob, Vector2.zero,
                new Vector2(800f, 680f), InkPalette.Ink);
            CreateImage("HanjiPaper", panel, blob, Vector2.zero,
                new Vector2(752f, 632f), InkPalette.Paper);

            var seal = CreateImage("PauseSeal", panel, blob, new Vector2(-284f, 220f),
                new Vector2(72f, 72f), InkPalette.Red);
            CreateText("SealText", seal.transform, "쉼", 28, Vector2.zero,
                new Vector2(58f, 52f), InkPalette.Paper, FontStyle.Normal);

            var title = CreateText("Title", panel, "잠시 멈춤", 62,
                new Vector2(0f, 206f), new Vector2(530f, 90f),
                InkPalette.TextDark, FontStyle.Normal);
            AddSoftWeight(title, InkPalette.Ink, 0.18f);
            CreateText("Subtitle", panel, "먹길을 잠시 쉬어갑니다", 30,
                new Vector2(0f, 126f), new Vector2(580f, 58f),
                ReadableMutedColor(), FontStyle.Normal);
            CreateImage("Divider", panel, null, new Vector2(0f, 78f),
                new Vector2(520f, 3f),
                new Color(InkPalette.Red.r, InkPalette.Red.g, InkPalette.Red.b, 0.72f));

            resumeButton = CreateBrushButton("ResumeButton", panel, "계속하기",
                new Vector2(0f, -18f), true);
            lobbyButton = CreateBrushButton("LobbyButton", panel, "로비로",
                new Vector2(0f, -154f), false);
            CreateText("SessionHint", panel, "현재 도전은 그대로 보존됩니다", 27,
                new Vector2(0f, -264f), new Vector2(620f, 48f),
                ReadableMutedColor(), FontStyle.Normal);

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
                            overlayRoot != null && overlayGroup != null &&
                            safeAreaRoot != null && panel != null &&
                            resumeButton != null && lobbyButton != null;
            if (!complete) return false;
            EnableFullButtonRaycast(pauseButton);
            EnableFullButtonRaycast(resumeButton);
            EnableFullButtonRaycast(lobbyButton);
            return true;
        }

        Button CreatePauseButton(Transform parent)
        {
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var outer = CreateImage("PauseButton", parent, blob, Vector2.zero,
                new Vector2(78f, 78f), InkPalette.Ink);
            var inner = CreateImage("Paper", outer.transform, blob, Vector2.zero,
                new Vector2(62f, 62f), InkPalette.Paper);
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = inner;
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
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var outer = CreateImage(objectName, parent, brush, position,
                new Vector2(580f, 112f), InkPalette.Ink);
            Image target = outer;
            Color textColor = InkPalette.Paper;
            if (!filled)
            {
                target = CreateImage("Paper", outer.transform, brush, Vector2.zero,
                    new Vector2(558f, 92f), InkPalette.Paper2);
                textColor = InkPalette.TextDark;
            }

            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            EnableFullButtonRaycast(button);
            button.colors = ReadableButtonColors();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = CreateText("Label", outer.transform, label, 38, Vector2.zero,
                new Vector2(470f, 76f), textColor, FontStyle.Bold);
            AddSoftWeight(text, InkPalette.Ink, 0.14f);
            return button;
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || pauseButtonRect == null ||
                Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            Vector2 minimum = new(
                Mathf.Clamp01(safe.xMin / Screen.width),
                Mathf.Clamp01(safe.yMin / Screen.height));
            Vector2 maximum = new(
                Mathf.Clamp01(safe.xMax / Screen.width),
                Mathf.Clamp01(safe.yMax / Screen.height));
            safeAreaRoot.anchorMin = minimum;
            safeAreaRoot.anchorMax = maximum;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;

            pauseButtonRect.anchorMin = pauseButtonRect.anchorMax =
                new Vector2(maximum.x, maximum.y);
            pauseButtonRect.pivot = new Vector2(0.5f, 0.5f);
            // 가독성 보강으로 높아진 상단 HUD와 겹치지 않도록 한 칸 아래에 둔다.
            pauseButtonRect.anchoredPosition = new Vector2(-62f, -245f);
            pauseButtonRect.sizeDelta = new Vector2(78f, 78f);

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
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

        static Color ReadableMutedColor()
        {
            Color color = InkPalette.TextDark;
            color.a = 0.84f;
            return color;
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
