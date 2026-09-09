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
        const int PauseActionLabelSize = 56;
        public const float PauseVisualSize = 72f;
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
        RectTransform exitPromptRoot;
        RectTransform exitSafeAreaRoot;
        RectTransform exitPanel;
        HanjiScrollFrame exitFrame;
        Button confirmExitButton;
        Button cancelExitButton;
        Button exitDimButton;
        bool exitConfirmationOpen;
        bool exitClosing;
        GameManager exitOwner;
        GameManager boundManager;
        Coroutine visibilityRoutine;
        bool overlayVisible;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;
        float lastBannerInset = -1f;
        float panelLayoutScale = 1f;

        // 로비 전환은 화면이 덮일 때까지 UserMenu 일시정지를 유지한다.
        // 이 동안 닫힌 두루마리를 새 일시정지 요청으로 오인해 다시 열면 안 된다.
        bool ShouldShowPauseMenu => boundManager != null &&
                                    boundManager.State == GameState.Playing &&
                                    boundManager.PauseReason == GameplayPauseReason.UserMenu &&
                                    !boundManager.IsTransitioning;

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
            SetOverlayVisible(false, false);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            BindManager();
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != MobileUiLayout.CurrentSafeArea ||
                lastBannerInset != LobbyAdLayout.GameplayTopInsetFraction)
                ApplySafeArea();

            RefreshManagerState();
        }

        void RefreshManagerState()
        {
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
            bool menuPaused = ShouldShowPauseMenu;
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
            ResetExitConfirmation();
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
            confirmExitButton?.onClick.AddListener(HandleExitConfirmed);
            cancelExitButton?.onClick.AddListener(HandleExitCancelled);
            exitDimButton?.onClick.AddListener(HandleExitCancelled);
        }

        void UnbindButtons()
        {
            pauseButton?.onClick.RemoveListener(HandlePausePressed);
            resumeButton?.onClick.RemoveListener(HandleResumePressed);
            lobbyButton?.onClick.RemoveListener(HandleLobbyPressed);
            confirmExitButton?.onClick.RemoveListener(HandleExitConfirmed);
            cancelExitButton?.onClick.RemoveListener(HandleExitCancelled);
            exitDimButton?.onClick.RemoveListener(HandleExitCancelled);
        }

        void HandlePausePressed()
        {
            boundManager?.PauseGame();
        }

        void HandleResumePressed()
        {
            CloseBeforeAction(() =>
            {
                if (boundManager != null && !boundManager.ResumeGame())
                    SetOverlayVisible(true, false);
            });
        }

        void HandleLobbyPressed()
        {
            if (!ShouldShowPauseMenu)
            {
                SetOverlayVisible(false, false);
                return;
            }
            if (!overlayVisible || exitConfirmationOpen || exitClosing ||
                panel.GetComponent<HanjiScrollFrame>().IsClosing) return;
            BuildExitConfirmation();
            exitOwner = boundManager;
            exitConfirmationOpen = true;
            exitFrame.ResetPresentation();
            exitPromptRoot.gameObject.SetActive(true);
            confirmExitButton.interactable = cancelExitButton.interactable = exitDimButton.interactable = true;
            resumeButton.interactable = lobbyButton.interactable = false;
        }

        void HandleExitCancelled()
        {
            if (!exitConfirmationOpen || exitClosing) return;
            CloseExitConfirmation(null);
        }

        void HandleExitConfirmed()
        {
            if (!exitConfirmationOpen || exitClosing || exitOwner != boundManager || !ShouldShowPauseMenu) return;
            GameManager owner = exitOwner;
            CloseExitConfirmation(() =>
            {
                if (owner == boundManager && ShouldShowPauseMenu)
                    LeaveAfterConfirmation();
            });
        }

        void LeaveAfterConfirmation()
        {
            CloseBeforeAction(() =>
            {
                if (boundManager == null || !boundManager.ReturnToLobby())
                {
                    if (resumeButton != null) resumeButton.interactable = true;
                    if (lobbyButton != null) lobbyButton.interactable = true;
                    SetOverlayVisible(true, false);
                }
            });
        }

        void CloseExitConfirmation(System.Action completed)
        {
            exitConfirmationOpen = false;
            exitClosing = true;
            confirmExitButton.interactable = cancelExitButton.interactable = exitDimButton.interactable = false;
            exitFrame.Close(() =>
            {
                exitPromptRoot.gameObject.SetActive(false);
                exitClosing = false;
                exitOwner = null;
                if (ShouldShowPauseMenu)
                    resumeButton.interactable = lobbyButton.interactable = true;
                completed?.Invoke();
            });
        }

        void ResetExitConfirmation()
        {
            exitConfirmationOpen = exitClosing = false;
            exitOwner = null;
            exitFrame?.ResetPresentation();
            if (exitPromptRoot != null) exitPromptRoot.gameObject.SetActive(false);
        }

        void CloseBeforeAction(System.Action action)
        {
            if (panel == null || !overlayVisible || exitConfirmationOpen || exitClosing) return;
            var frame = panel.GetComponent<HanjiScrollFrame>();
            if (frame.IsClosing) return;
            GameManager owner = boundManager;
            if (visibilityRoutine != null) StopCoroutine(visibilityRoutine);
            visibilityRoutine = null;
            if (resumeButton != null) resumeButton.interactable = false;
            if (lobbyButton != null) lobbyButton.interactable = false;
            overlayGroup.interactable = false;
            frame.Close(() =>
            {
                SetOverlayVisible(false, false);
                if (owner != null && boundManager == owner &&
                    owner.State == GameState.Playing &&
                    !owner.IsTransitioning &&
                    owner.PauseReason == GameplayPauseReason.UserMenu)
                    action?.Invoke();
            }, overlayGroup);
        }

        void HandlePauseChanged(bool paused)
        {
            bool menuPaused = paused && ShouldShowPauseMenu;
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
            bool menuPaused = ShouldShowPauseMenu;
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
            if (!visible) ResetExitConfirmation();
            if (overlayGroup == null || panel == null) return;
            var frame = panel.GetComponent<HanjiScrollFrame>();
            if (!visible && animate && Application.isPlaying)
            {
                if (visibilityRoutine != null) StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
                overlayGroup.interactable = false;
                frame.Close(() => SetOverlayVisible(false, false), overlayGroup);
                return;
            }
            if (visible)
            {
                frame.CancelClose();
                // 씬 전환이 취소되면 PauseChanged 없이 Update에서 복구될 수도 있다.
                bool canAct = !exitConfirmationOpen && !exitClosing;
                if (resumeButton != null) resumeButton.interactable = canAct;
                if (lobbyButton != null) lobbyButton.interactable = canAct;
            }
            else frame.ResetPresentation();
            if (LobbySettingsProfile.ReducedMotionEnabled)
                animate = false;
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
            float startScale = 1f;
            float targetScale = 1f;
            if (visible && startAlpha <= 0.001f)
            {
                startScale = 1f;
                ApplyPanelPresentationScale(startScale);
            }

            float duration = ShowDuration;
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
            if (rootCanvas != null)
            {
                BuildExitConfirmation();
                return;
            }

            var existing = transform.Find("PauseMenuCanvas");
            if (existing != null && RestoreExistingReferences(existing))
            {
                BuildExitConfirmation();
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
                "InkDim", overlayRoot, InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(backdrop);

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", overlayRoot);
            panel = CreateRect(
                "PauseScroll",
                safeAreaRoot,
                Vector2.zero,
                PanelDesignSize);

            BuildPauseScrollFrame(panel);

            var title = CreateText("Title", panel, "잠시 멈춤", InkUiStyle.PauseTitleSize,
                new Vector2(0f, 165f), new Vector2(580f, 112f),
                InkPalette.Ink, FontStyle.Bold);
            ConfigurePauseTitle(title);

            resumeButton = CreateBrushButton("ResumeButton", panel, "계속하기",
                new Vector2(0f, 25f), ActionButtonRole.Primary);
            lobbyButton = CreateBrushButton("LobbyButton", panel, "로비로",
                new Vector2(0f, -120f), ActionButtonRole.Secondary);
            ApplyActionPriority(resumeButton, ActionButtonRole.Primary);
            ApplyActionPriority(lobbyButton, ActionButtonRole.Secondary);

            BuildExitConfirmation();
            ApplySafeArea();
        }

        void BuildExitConfirmation()
        {
            if (exitPromptRoot != null || rootCanvas == null) return;
            exitPromptRoot = rootCanvas.transform.Find("ExitConfirmation") as RectTransform;
            if (exitPromptRoot != null)
            {
                exitSafeAreaRoot = (RectTransform)exitPromptRoot.Find("SafeAreaRoot");
                exitPanel = (RectTransform)exitSafeAreaRoot.Find("ExitScroll");
                exitFrame = exitPanel.GetComponent<HanjiScrollFrame>();
                confirmExitButton = exitPanel.Find("ConfirmExitButton").GetComponent<Button>();
                cancelExitButton = exitPanel.Find("CancelExitButton").GetComponent<Button>();
                exitDimButton = exitPromptRoot.Find("Dim").GetComponent<Button>();
                ResetExitConfirmation();
                return;
            }
            exitPromptRoot = CreateStretchRect("ExitConfirmation", rootCanvas.transform);
            Image dim = CreateStretchImage("Dim", exitPromptRoot, InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);
            exitDimButton = dim.gameObject.AddComponent<Button>();
            exitDimButton.targetGraphic = dim;
            exitDimButton.transition = Selectable.Transition.None;
            exitSafeAreaRoot = CreateStretchRect("SafeAreaRoot", exitPromptRoot);
            exitPanel = CreateRect("ExitScroll", exitSafeAreaRoot, Vector2.zero, PanelDesignSize);
            var paperHit = CreateStretchImage("PaperHitArea", exitPanel, Color.clear);
            paperHit.raycastTarget = true;
            exitFrame = HanjiScrollFrame.Attach(exitPanel, new Vector2(700, 570));
            CreateText("Title", exitPanel, "종료하시겠습니까?", 64,
                new Vector2(0, 165), new Vector2(620, 100), InkPalette.Ink, FontStyle.Bold);
            CreateText("Message", exitPanel, "지금 종료하면 이번 판 기록이 저장되지 않습니다.", 44,
                new Vector2(0, 25), new Vector2(600, 150), InkPalette.Ink, FontStyle.Bold);
            cancelExitButton = CreateExitButton("CancelExitButton", "취소", -150f, ActionButtonRole.Secondary);
            confirmExitButton = CreateExitButton("ConfirmExitButton", "종료", 150f, ActionButtonRole.Primary);
            ResetExitConfirmation();
        }

        Button CreateExitButton(string name, string label, float x, ActionButtonRole role)
        {
            var background = CreateImage(name, exitPanel, null, new Vector2(x, -155),
                new Vector2(270, 120), Color.white);
            var button = background.gameObject.AddComponent<Button>();
            var text = CreateText("Label", background.transform, label, 52, Vector2.zero,
                new Vector2(220, 80), InkPalette.Ink, FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(button, background, text, role);
            text.fontSize = 52;
            return button;
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
            if (!complete || panel.GetComponent<HanjiScrollFrame>() == null) return false;
            ConfigurePauseTitle(panel.Find("Title")?.GetComponent<Text>());
            ReconfigureActionButton(resumeButton, ActionButtonRole.Primary);
            ReconfigureActionButton(lobbyButton, ActionButtonRole.Secondary);
            EnableFullButtonRaycast(pauseButton);
            ConfigurePauseButtonIcon(pauseButton);
            EnableFullButtonRaycast(resumeButton);
            EnableFullButtonRaycast(lobbyButton);
            return true;
        }

        static void BuildPauseScrollFrame(Transform parent)
        {
            HanjiScrollFrame.Attach((RectTransform)parent, new Vector2(700, 570));
        }

        Button CreatePauseButton(Transform parent)
        {
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
            var outer = CreateImage("Visual", hitSurface, null, Vector2.zero,
                new Vector2(PauseVisualSize, PauseVisualSize), Color.clear);
            var inner = CreateImage("Paper", outer.transform, null, Vector2.zero,
                new Vector2(62f, 62f), Color.clear);
            var button = hitSurface.gameObject.AddComponent<Button>();
            CreateImage("LeftBar", inner.transform, null, new Vector2(-8f, 0f),
                new Vector2(7f, 28f), InkPalette.Ink);
            CreateImage("RightBar", inner.transform, null, new Vector2(8f, 0f),
                new Vector2(7f, 28f), InkPalette.Ink);
            ConfigurePauseButtonIcon(button);
            return button;
        }

        static void ConfigurePauseButtonIcon(Button button)
        {
            var face = button != null ? button.transform.Find("Visual")?.GetComponent<Image>() : null;
            if (face == null) return;
            face.rectTransform.sizeDelta = new Vector2(PauseVisualSize, PauseVisualSize);
            InkUiStyle.ConfigureButton(button, face);
            // 기존 Play 계층을 복원해도 한지 면을 되살리지 않는다.
            // 투명 부모의 터치 영역과 공통 눌림 반응만 남기고 두 막대만 그린다.
            face.sprite = null;
            face.color = Color.clear;
            face.enabled = false;
            face.raycastTarget = false;
            // 구형 한지 버튼에서 복구한 경우 별도 갈필 프레임도 숨긴다.
            var legacyFrame = face.GetComponent<HanjiCardFrame>();
            if (legacyFrame != null) legacyFrame.enabled = false;
            var legacyOutline = face.transform.Find("BrushFrame");
            if (legacyOutline != null) legacyOutline.gameObject.SetActive(false);
            button.transition = Selectable.Transition.None;
            var hitImage = button.GetComponent<Image>();
            if (hitImage != null)
            {
                hitImage.color = Color.clear;
                hitImage.enabled = true;
                hitImage.raycastTarget = true;
            }
            var legacyPaper = face.transform.Find("Paper")?.GetComponent<Image>();
            if (legacyPaper != null)
            {
                legacyPaper.sprite = null;
                legacyPaper.color = Color.clear;
                legacyPaper.enabled = false;
                legacyPaper.raycastTarget = false;
            }
            foreach (string name in new[] { "Paper/LeftBar", "Paper/RightBar" })
            {
                var oldBar = face.transform.Find(name)?.GetComponent<Graphic>();
                if (oldBar != null) oldBar.enabled = false;
            }
            var icon = face.transform.Find("BrushPause")?.GetComponent<InkBrushIcon>();
            if (icon == null)
            {
                var go = new GameObject("BrushPause", typeof(RectTransform), typeof(CanvasRenderer), typeof(InkBrushIcon));
                go.transform.SetParent(face.transform, false);
                icon = go.GetComponent<InkBrushIcon>();
            }
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(48f, 48f);
            icon.Configure(InkBrushIcon.Symbol.Pause);
        }

        Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            ActionButtonRole role)
        {
            var outer = CreateImage(objectName, parent, null, position,
                new Vector2(580f, 104f), InkPalette.Ink);
            var button = outer.gameObject.AddComponent<Button>();
            var text = CreateText(
                "Label", outer.transform, label, InkUiStyle.ActionButtonLabelSize,
                Vector2.zero,
                new Vector2(470f, 76f), InkPalette.Paper, FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(button, outer, text, role);
            text.fontSize = PauseActionLabelSize;
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
            if (exitSafeAreaRoot != null)
                MobileUiLayout.ApplySafeArea(exitSafeAreaRoot, safe, Screen.width, Screen.height);

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
            if (exitPanel != null)
            {
                exitPanel.anchoredPosition = Vector2.zero;
                exitPanel.localScale = Vector3.one * panelLayoutScale;
            }
            if (panel != null)
            {
                panel.anchoredPosition = Vector2.zero;
                ApplyPanelPresentationScale(presentationScale);
            }

            pauseButtonRect.anchorMin = pauseButtonRect.anchorMax =
                new Vector2(0.5f, 1f);
            pauseButtonRect.pivot = new Vector2(0.5f, 0.5f);
            // 종전 오른쪽 신기록 자리. 아이콘만 HUD와 같이 축소하고 터치 면적은 유지한다.
            Vector2 iconPosition = GameplayHudView.CalculatePauseButtonPosition(
                safe, Screen.width, Screen.height);
            Rect touchRect = GameplayHudView.CalculatePauseTouchRect(safe, Screen.width, Screen.height);
            pauseButtonRect.anchoredPosition = touchRect.center;
            pauseButtonRect.sizeDelta = touchRect.size;
            var face = pauseButtonRect.Find("Visual") as RectTransform;
            if (face != null)
            {
                face.anchoredPosition = iconPosition - touchRect.center;
                face.localScale = Vector3.one * GameplayHudView.CalculateTopHudScale(
                    safe, Screen.width, Screen.height);
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
            lastBannerInset = LobbyAdLayout.GameplayTopInsetFraction;
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
            InkLocalizedText.SetSource(text, value);
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            InkLocalizedText.Bind(text);
            return text;
        }

        static void ConfigurePauseTitle(Text text)
        {
            if (text == null) return;
            InkUiStyle.ApplyReadableText(text, InkUiStyle.PauseTitleSize);
            text.color = InkPalette.Ink;
            text.rectTransform.sizeDelta = new Vector2(580f, 112f);
            // 작은 반투명 그림자로 두께를 흉내 내지 않고 큰 Bold 획 자체를 읽게 한다.
            foreach (Shadow shadow in text.GetComponents<Shadow>())
                shadow.enabled = false;
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

        static void ReconfigureActionButton(
            Button button,
            ActionButtonRole role)
        {
            if (button == null) return;
            Image border = button.GetComponent<Image>();
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            if (border == null || label == null) return;
            InkUiStyle.ConfigureActionButton(button, border, label, role);
            label.fontSize = PauseActionLabelSize;
            ApplyActionPriority(button, role);
        }

        static void ApplyActionPriority(
            Button button,
            ActionButtonRole role)
        {
            if (button == null) return;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            InkUiStyle.SetActionButtonRole(
                button.GetComponent<Image>(),
                role);
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
