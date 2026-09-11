using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MukJump.Player;

namespace MukJump.Core
{
    /// 첫 실행의 실제 게임 화면을 비추는 안내. 닉네임 저장까지 같은 판을 정지한다.
    [DisallowMultipleComponent]
    public sealed class FirstRunTutorialController : MonoBehaviour
    {
        // 옵션에서 다시 보는 설명 두루마리의 호환 규격.
        public const float PanelDesignWidth = 820f;
        public const float PanelDesignHeight = 1360f;
        public const float PanelEdgePadding = 24f;
        public const int StepCount = 4;
        public const float FocusTransitionSeconds = 0.26f;
        public static readonly Vector2 CalloutDesignSize = new(820f, 440f);

        static readonly GameplayTutorialPage[] steps =
        {
            new(GameplayTutorialTopic.AutoJump, "자동 점프",
                "먹방울은 스스로 점프해요.", string.Empty),
            new(GameplayTutorialTopic.DrawInk, "선 그리기",
                "내려올 곳에 선을 그려주세요.\n선의 기울기와 길이가 점프를 정해요.", string.Empty),
            new(GameplayTutorialTopic.InkBudget, "먹 게이지",
                "남은 먹이에요.\n먹이 부족하면 오래된 선부터 지워져요.", string.Empty),
            new(GameplayTutorialTopic.Obstacles, "체력",
                "부딪히거나 떨어지면 1칸 줄어요.\n모두 쓰러지면 게임이 끝나요.", string.Empty),
        };

        public static FirstRunTutorialController Instance { get; private set; }
        public static GameplayTutorialPage GetStep(int index) => steps[index];
        public bool IsActive => active;
        public bool IsAwaitingNickname => awaitingNickname;
        public int CurrentStep => currentStep;
        public GameplayTutorialTopic CurrentTopic => steps[Mathf.Clamp(currentStep, 0, StepCount - 1)].Topic;
        public bool ShowInkGauge => active && !awaitingNickname && !closing &&
            CurrentTopic == GameplayTutorialTopic.InkBudget &&
            Time.unscaledTime - stepStartedAt >= FocusTransitionSeconds;
        public Rect FocusScreenRect { get; private set; }
        public Rect CalloutScreenRect { get; private set; }

        CanvasGroup rootGroup;
        CanvasGroup calloutGroup;
        RectTransform panel;
        TutorialSpotlightGraphic spotlight;
        Text titleText;
        Text descriptionText;
        Text progressText;
        Text tapHint;
        GameManager manager;
        GameManager subscribedManager;
        LobbyOptionsView nicknameView;
        PlayerController focusPlayer;
        LineRenderer starterLine;
        int currentStep = -1;
        bool pendingFirstRun;
        bool autoStartFirstVisit;
        bool autoStartAttempted;
        bool active;
        bool awaitingNickname;
        bool ownsTutorialPause;
        bool closing;
        bool hasFocus;
        float stepStartedAt;
        float closeStartedAt;
        Rect focusFrom;
        int autoStartEarliestFrame;

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
            autoStartFirstVisit = LobbySettingsProfile.ShouldAutoStartGameplayTutorial;
            autoStartEarliestFrame = Time.frameCount + 1;
        }

        void OnDisable()
        {
            if (active) MukJumpAnalytics.TutorialInterrupted();
            nicknameView?.CancelFirstRunNickname();
            ReleaseTutorialPause();
            UnbindRuntimeSignals();
            pendingFirstRun = active = awaitingNickname = false;
            SetVisible(false);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            BindRuntimeSignals();
            if (!active)
            {
                if (!StartupBrandSplash.IsBlockingInput) TryAutoStartFirstVisit(false);
                return;
            }
            if (manager != null && (manager.State != GameState.Playing ||
                ownsTutorialPause && manager.PauseReason != GameplayPauseReason.FirstRunTutorial))
            {
                EndWithoutCompletion();
                return;
            }
            if (awaitingNickname) return;
            if (closing)
            {
                float t = LobbySettingsProfile.ReducedMotionEnabled ? 1f :
                    Mathf.Clamp01((Time.unscaledTime - closeStartedAt) / 0.18f);
                rootGroup.alpha = 1f - t;
                if (t >= 1f) OpenNicknameSetup();
            }
        }

        void LateUpdate()
        {
            if (active && !awaitingNickname && !closing) RefreshFocus();
        }

        /// 브랜드가 걷히기 전에 새 설치의 게임 월드를 준비해 로비가 번쩍 보이지 않게 한다.
        public void PrepareBeforeStartupReveal() => TryAutoStartFirstVisit(true);

        /// 새 게스트 전환 뒤 현재 Main에서 자동 시작하지 않고 Splash로 돌아갈 준비만 한다.
        internal void PrepareForStartupReturn()
        {
            if (active) EndWithoutCompletion();
            pendingFirstRun = closing = awaitingNickname = false;
            autoStartAttempted = true;
            autoStartFirstVisit = false;
            var options = GetComponent<LobbyOptionsView>();
            if (options == null) options = FindAnyObjectByType<LobbyOptionsView>();
            options?.Close();
            PointerInput.SuppressUntilRelease();
        }

        void TryAutoStartFirstVisit(bool behindBrand)
        {
            // OnEnable 당시의 미완료 캐시로 로비 복귀 후 새 판을 시작하지 않는다.
            if (!LobbySettingsProfile.ShouldAutoStartGameplayTutorial)
            {
                autoStartFirstVisit = false;
                return;
            }
            if (!autoStartFirstVisit || autoStartAttempted || pendingFirstRun ||
                (!behindBrand && Time.frameCount < autoStartEarliestFrame)) return;
            manager ??= GameManager.Instance;
            if (manager == null || manager.State != GameState.Lobby ||
                manager.IsTransitioning || PermanentGrowthProfile.RequiresRecovery) return;
            // 자동 첫 실행은 초기 인증 대기를 계정 팝업으로 바꾸지 않는다.
            // 팝업이 남으면 CanStartGame이 계속 false여서 재실행 전까지 안내가 사라진다.
            if (MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) return;
            var navigator = LobbyScreenNavigator.Instance;
            if (navigator != null && !navigator.CanStartGame) return;
            var options = GetComponent<LobbyOptionsView>();
            if (options != null && options.IsOpen) return;
            if (behindBrand) manager.StartFirstRunFromStartup();
            else manager.StartGameFromMenu();
            // 인증·저장 확인으로 시작이 거절됐다면 다음 프레임에 다시 시도한다.
            if (manager.State != GameState.Playing && !manager.IsTransitioning) return;
            autoStartAttempted = true;
            autoStartFirstVisit = false;
        }

        public bool PrepareForGameStart()
        {
            pendingFirstRun = LobbySettingsProfile.NeedsGameplayTutorial;
            return pendingFirstRun;
        }

        public static bool IsPointerOverControls(Vector2 screenPosition) =>
            Instance != null && Instance.active;

        void BindRuntimeSignals()
        {
            GameManager next = GetComponent<GameManager>() ?? GameManager.Instance;
            if (subscribedManager == next) return;
            UnbindRuntimeSignals();
            manager = subscribedManager = next;
            if (subscribedManager != null) subscribedManager.StateChanged += HandleStateChanged;
        }

        void UnbindRuntimeSignals()
        {
            if (subscribedManager != null) subscribedManager.StateChanged -= HandleStateChanged;
            subscribedManager = null;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing && pendingFirstRun) BeginTutorial();
            else if (current != GameState.Playing && active) EndWithoutCompletion();
        }

        void BeginTutorial()
        {
            pendingFirstRun = false;
            manager ??= GameManager.Instance;
            ownsTutorialPause = manager != null && manager.PauseForFirstRunTutorial();
            if (manager != null && !ownsTutorialPause) { EndWithoutCompletion(); return; }
            active = true;
            awaitingNickname = closing = hasFocus = false;
            focusPlayer = manager != null ? manager.HighestLivingPlayer : null;
            starterLine = GameObject.Find(LobbyWorldSetup.StarterPlatformObjectName)?.GetComponent<LineRenderer>();
            currentStep = 0;
            MukJumpAnalytics.TutorialBegin();
            SetVisible(true);
            ShowCurrentPage();
            PointerInput.SuppressUntilRelease();
        }

        void HandleTap(BaseEventData data)
        {
            if (data is not PointerEventData pointer ||
                pointer.button != PointerEventData.InputButton.Left || pointer.dragging ||
                !MobileApplicationLifecycle.IsApplicationActive || StartupBrandSplash.IsBlockingInput) return;
            // 열린 순간의 잔여 터치·더블 탭으로 두 설명이 동시에 넘어가지 않는다.
            if (Application.isPlaying && Time.unscaledTime - stepStartedAt < 0.35f) return;
            AdvanceStep();
        }

        void AdvanceStep()
        {
            if (!active || awaitingNickname || closing) return;
            if (currentStep == StepCount - 1)
            {
                closing = true;
                closeStartedAt = Time.unscaledTime;
                if (!Application.isPlaying) OpenNicknameSetup();
                return;
            }
            currentStep++;
            ShowCurrentPage();
        }

        void ShowCurrentPage()
        {
            var page = steps[currentStep];
            InkLocalizedText.SetSource(titleText, page.Title);
            InkLocalizedText.SetSource(descriptionText, page.Description);
            InkLocalizedText.SetSource(tapHint,
                currentStep == StepCount - 1 ? string.Empty : "탭하여 다음");
            progressText.text = $"{currentStep + 1} / {StepCount}";
            MukJumpAnalytics.TutorialStep(currentStep);
            stepStartedAt = Time.unscaledTime;
            focusFrom = FocusScreenRect;
            RefreshFocus();
        }

        void OpenNicknameSetup()
        {
            closing = false;
            awaitingNickname = true;
            rootGroup.alpha = 0f;
            rootGroup.interactable = rootGroup.blocksRaycasts = false;
            nicknameView = GetComponent<LobbyOptionsView>();
            if (nicknameView == null) nicknameView = gameObject.AddComponent<LobbyOptionsView>();
            nicknameView.OpenFirstRunNickname(FinishTutorial);
        }

        void FinishTutorial()
        {
            if (!active || !awaitingNickname) return;
            active = pendingFirstRun = awaitingNickname = false;
            currentStep = StepCount;
            SetVisible(false);
            bool saved = LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            MukJumpAnalytics.TutorialEnd(false, saved);
            PointerInput.SuppressUntilRelease();
            ReleaseTutorialPause();
        }

        void EndWithoutCompletion()
        {
            if (active) MukJumpAnalytics.TutorialInterrupted();
            nicknameView?.CancelFirstRunNickname();
            active = pendingFirstRun = awaitingNickname = false;
            currentStep = -1;
            SetVisible(false);
            ReleaseTutorialPause();
        }

        void ReleaseTutorialPause()
        {
            if (!ownsTutorialPause) return;
            if (manager != null) manager.ResumeFirstRunTutorial();
            ownsTutorialPause = false;
        }

        void RefreshFocus()
        {
            if (Screen.width <= 0 || Screen.height <= 0 || spotlight == null) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            Rect target = ResolveFocus(safe);
            if (!hasFocus) focusFrom = target;
            float t = !Application.isPlaying || LobbySettingsProfile.ReducedMotionEnabled || !hasFocus
                ? 1f : Mathf.Clamp01((Time.unscaledTime - stepStartedAt) / FocusTransitionSeconds);
            // 화면 위에서 자리를 옮기는 조명은 부드럽게 가속·감속한다.
            float eased = t * t * (3f - 2f * t);
            FocusScreenRect = LerpRect(focusFrom, target, eased);
            hasFocus = true;
            var root = (RectTransform)rootGroup.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, FocusScreenRect.min, null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, FocusScreenRect.max, null, out Vector2 max);
            spotlight.SetFocus(Rect.MinMaxRect(min.x, min.y, max.x, max.y),
                CurrentTopic == GameplayTutorialTopic.AutoJump ? Mathf.Min(max.x - min.x, max.y - min.y) * .5f : 24f);
            Rect textAvoidance = target;
            if (CurrentTopic == GameplayTutorialTopic.DrawInk || CurrentTopic == GameplayTutorialTopic.InkBudget)
            {
                var body = focusPlayer != null ? focusPlayer.GetComponent<SpriteRenderer>() : null;
                if (body != null && Camera.main != null)
                    textAvoidance.yMax = Mathf.Max(textAvoidance.yMax,
                        BoundsToScreen(body.bounds, Camera.main).yMax + safe.width * .055f);
            }
            CalloutScreenRect = CalculateCalloutRect(textAvoidance, safe, Screen.height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel.parent as RectTransform,
                CalloutScreenRect.center, null, out Vector2 center);
            panel.anchoredPosition = center;
            float scale = CalloutScreenRect.width / CalloutDesignSize.x / (Screen.height / 1920f);
            panel.localScale = Vector3.one * scale;
            calloutGroup.alpha = .35f + .65f * eased;
        }

        Rect ResolveFocus(Rect safe)
        {
            float unit = safe.width / 1080f;
            Rect body = new(safe.center.x - safe.width * .08f, safe.yMin + safe.height * .2f,
                safe.width * .16f, safe.width * .16f);
            if (focusPlayer == null && manager != null) focusPlayer = manager.HighestLivingPlayer;
            var camera = Camera.main;
            var renderer = focusPlayer != null ? focusPlayer.GetComponent<SpriteRenderer>() : null;
            if (camera != null && renderer != null) body = BoundsToScreen(renderer.bounds, camera);
            Rect focus = body;
            switch (CurrentTopic)
            {
                case GameplayTutorialTopic.DrawInk:
                    float y = camera != null && starterLine != null
                        ? camera.WorldToScreenPoint(starterLine.bounds.center).y : body.yMin - 24f * unit;
                    focus = new Rect(safe.center.x - safe.width * .34f, y - 48f * unit,
                        safe.width * .68f, 96f * unit);
                    break;
                case GameplayTutorialTopic.InkBudget:
                    float top = PrototypeHud.CalculateGaugeTopScreenY(safe, Screen.width, Screen.height, Application.platform);
                    focus = new Rect(safe.xMin + safe.width * .07f, top - safe.width * .14f,
                        safe.width * .86f, safe.width * .14f);
                    break;
                case GameplayTutorialTopic.Obstacles:
                    var health = focusPlayer != null ? focusPlayer.GetComponent<PlayerHealthBillboard>()?.HealthRenderer : null;
                    focus = camera != null && health != null ? BoundsToScreen(health.bounds, camera) :
                        new Rect(body.xMin, body.yMax + 8f * unit, body.width, 20f * unit);
                    break;
            }
            float padding = 24f * unit;
            focus = Rect.MinMaxRect(focus.xMin - padding, focus.yMin - padding,
                focus.xMax + padding, focus.yMax + padding);
            // 실제 게이지는 홈 인디케이터 여백에 일부 걸칠 수 있다. 조명은 실제 그림을
            // 따라가고 설명 글자만 Safe Area에 제한해야 구멍이 위로 밀리지 않는다.
            return ClampRect(focus, new Rect(0, 0, Screen.width, Screen.height));
        }

        static Rect BoundsToScreen(Bounds bounds, Camera camera)
        {
            Vector3 a = camera.WorldToScreenPoint(bounds.min);
            Vector3 b = camera.WorldToScreenPoint(bounds.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public static Rect CalculateCalloutRect(Rect focus, Rect safe, int screenHeight)
        {
            float unit = Mathf.Max(1, screenHeight) / 1920f;
            float scale = Mathf.Min(unit, (safe.width - 48f * unit) / CalloutDesignSize.x,
                (safe.height * .42f) / CalloutDesignSize.y);
            Vector2 size = CalloutDesignSize * Mathf.Max(.01f, scale);
            float gap = 36f * unit;
            bool above = safe.yMax - focus.yMax >= size.y + gap ||
                safe.yMax - focus.yMax >= focus.yMin - safe.yMin;
            float y = above ? focus.yMax + gap : focus.yMin - gap - size.y;
            return ClampRect(new Rect(safe.center.x - size.x * .5f, y, size.x, size.y), safe);
        }

        static Rect ClampRect(Rect rect, Rect bounds)
        {
            rect.size = Vector2.Min(rect.size, bounds.size);
            rect.position = new Vector2(Mathf.Clamp(rect.x, bounds.xMin, bounds.xMax - rect.width),
                Mathf.Clamp(rect.y, bounds.yMin, bounds.yMax - rect.height));
            return rect;
        }

        static Rect LerpRect(Rect a, Rect b, float t) =>
            new(Vector2.Lerp(a.position, b.position, t), Vector2.Lerp(a.size, b.size, t));

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;
            Transform stale = transform.Find("FirstRunTutorialCanvas");
            if (stale != null)
            {
                stale.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(stale.gameObject);
                else DestroyImmediate(stale.gameObject);
            }
            var root = new GameObject("FirstRunTutorialCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4200;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            rootGroup = root.GetComponent<CanvasGroup>();
            var dim = CreateRect("TutorialDim", root.transform, Vector2.zero, Vector2.zero);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            spotlight = dim.gameObject.AddComponent<TutorialSpotlightGraphic>();
            spotlight.color = new Color(0f, 0f, 0f, .78f);
            spotlight.raycastTarget = true;
            var trigger = dim.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(HandleTap);
            trigger.triggers.Add(click);
            var safeRoot = CreateRect("SafeAreaRoot", root.transform, Vector2.zero, Vector2.zero);
            safeRoot.anchorMin = Vector2.zero; safeRoot.anchorMax = Vector2.one;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            panel = CreateRect("TutorialPanel", safeRoot, Vector2.zero, CalloutDesignSize);
            calloutGroup = panel.gameObject.AddComponent<CanvasGroup>();
            calloutGroup.blocksRaycasts = false;
            progressText = CreateText("Progress", panel, string.Empty, 44, 190f, 48f, false);
            InkLocalizedText.Exclude(progressText);
            titleText = CreateText("Title", panel, string.Empty, 64, 105f, 90f, true);
            descriptionText = CreateText("Description", panel, string.Empty, 52, -40f, 166f, false);
            descriptionText.lineSpacing = 1.08f;
            tapHint = CreateText("TapHint", panel, "탭하여 다음", 40, -174f, 60f, false);
        }

        void SetVisible(bool visible)
        {
            closing = false;
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = rootGroup.blocksRaycasts = visible;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Text CreateText(string name, Transform parent, string source, int fontSize, float y, float height, bool bold)
        {
            var rect = CreateRect(name, parent, new Vector2(0, y), new Vector2(800, height));
            var text = rect.gameObject.AddComponent<Text>();
            InkUiStyle.ApplyReadableText(text, fontSize, TextAnchor.MiddleCenter, bold, true);
            text.color = InkPalette.TextLight;
            text.raycastTarget = false;
            InkLocalizedText.SetSource(text, source);
            return text;
        }

        public static float CalculatePanelScaleForTests(Rect safeArea, int screenWidth, int screenHeight) =>
            MobileUiLayout.CalculateFitScale(new Vector2(PanelDesignWidth, PanelDesignHeight),
                safeArea, screenWidth, screenHeight, Vector2.one * PanelEdgePadding);

#if UNITY_EDITOR
        public void BuildForTests() => BuildIfNeeded();
        public void BeginForTests()
        {
            BuildIfNeeded();
            Instance = this;
            BeginTutorial();
            Canvas.ForceUpdateCanvases();
            RefreshFocus();
        }
        public void AdvanceForTests() => AdvanceStep();
#endif
    }
}
