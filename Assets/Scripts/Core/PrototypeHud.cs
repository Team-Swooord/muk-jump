using UnityEngine;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Core
{
    /// 화면 하단에 남은 먹 용량을 표시하는 경량 HUD.
    public class PrototypeHud : MonoBehaviour
    {
        // 가로 WebGL에서만 화면을 가리지 않게 짧은 복고형 폭을 쓴다.
        const float GaugeWidthRatio = 0.48f;
        const float PortraitHorizontalInsetRatio = 0.08f;
        const float GaugeVisualHeightRatio = 0.12f;
        const float BrushIconSizeRatio = 0.14f;
        const float BrushOverlapRatio = 0.62f;
        // 게이지를 붓 몸통 중앙까지 넣고, 마지막에 그리는 붓 아이콘으로 끝을 덮는다.
        const float BrushContactHeightRatio = 0.5f;
        const float FallbackGaugeHeightRatio = 0.05f;
        const float GaugeBottomMarginRatio = 0.008f;
        const float MinimumGaugeBottomMargin = 6f;
        const float GaugeBottomSafeAreaBleed = 0.5f;
        const float WideWebGaugeWidthPerHeight = 1.05f;
        const float WideWebBottomMarginRatio = 0.025f;
        const float WideWebMinimumBottomMargin = 16f;
        const float EmptyGaugeGuideAlpha = 0.16f;
        const float GaugeConsumeFadeSeconds = 0.24f;
        const float GaugeRecoverFadeSeconds = 0.75f;

        [Header("먹 게이지 이미지 (붓 획 모양) — 미할당 시 단색 막대로 폴백")]
        [Tooltip("붓 획 실루엣, 채워진 상태 (왼쪽 가늘게 → 오른쪽 두껍게)")]
        [SerializeField] Texture2D inkGaugeFill;
        [Tooltip("같은 실루엣의 빈 상태 트랙 (fill과 캔버스·위치 동일)")]
        [SerializeField] Texture2D inkGaugeTrack;
        [Tooltip("게이지 오른쪽 끝의 붓 아이콘")]
        [SerializeField] Texture2D inkBrushIcon;

        StrokeCapture strokeCapture;
        GameManager boundManager;
        float displayedInkRatio = 1f;
        bool displayedInkRatioInitialized;
        bool displayedGolden;
        float goldenBlend;
        float goldenTransitionTime = 1f;
        float goldenTransitionStartRatio;
        float goldenTransitionStartBlend;
        Material gaugeTintMaterial;
        Material goldenTimerMaterial;
        GoldenGaugeVfx goldenGaugeVfx;
        Camera gameplayCamera;
        PlayerController goldenTimerOwner;
        SpriteRenderer goldenTimerBody;
        PlayerHealthBillboard goldenTimerHealth;
        static readonly int RecolorId = Shader.PropertyToID("_Recolor");
        static readonly int RemainingId = Shader.PropertyToID("_Remaining");

        void OnEnable()
        {
            EnsureRuntimeReferences();
            // 검은 RGB에 Gold를 곱하면 여전히 검정이다. 알파와 붓결은 유지하고
            // GPU에서 안료만 치환하며, 원본 크기의 복제 텍스처는 만들지 않는다.
            Shader shader = Resources.Load<Shader>("MukJump/Shaders/InkGaugeTint");
            if (shader != null && gaugeTintMaterial == null)
            {
                gaugeTintMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                gaugeTintMaterial.SetColor("_InkColor", InkPalette.Gold);
            }
            Shader timerShader = Resources.Load<Shader>("MukJump/Shaders/GoldenBrushTimer");
            if (timerShader != null && goldenTimerMaterial == null)
            {
                goldenTimerMaterial = new Material(timerShader) { hideFlags = HideFlags.HideAndDontSave };
                goldenTimerMaterial.SetColor("_FillColor", InkPalette.TimerGold);
                goldenTimerMaterial.SetColor("_TrackColor", InkPalette.Ink);
            }
            if (goldenGaugeVfx == null)
            {
                Shader sparkleShader = Resources.Load<Shader>("MukJump/Shaders/GoldenGaugeParticle");
                if (sparkleShader != null) goldenGaugeVfx = new GoldenGaugeVfx(sparkleShader);
            }
        }

        void OnDisable()
        {
            BindManager(null);
            goldenGaugeVfx?.Dispose();
            goldenGaugeVfx = null;
            gameplayCamera = null;
            goldenTimerOwner = null;
            goldenTimerBody = null;
            goldenTimerHealth = null;
            if (gaugeTintMaterial != null)
            {
                if (Application.isPlaying) Destroy(gaugeTintMaterial);
                else DestroyImmediate(gaugeTintMaterial);
                gaugeTintMaterial = null;
            }
            if (goldenTimerMaterial != null)
            {
                if (Application.isPlaying) Destroy(goldenTimerMaterial);
                else DestroyImmediate(goldenTimerMaterial);
                goldenTimerMaterial = null;
            }
        }

        void Start()
        {
            EnsureRuntimeReferences();
            InitializeDisplayedInkRatio();
        }

        void Update()
        {
            if (strokeCapture == null)
                EnsureRuntimeReferences();
            if (strokeCapture == null)
                return;

            if (boundManager != null && !boundManager.IsGameplayTicking)
                return;
            AdvanceGaugePresentation(strokeCapture.InkRemaining01,
                strokeCapture.HasUnlimitedInk, Time.unscaledDeltaTime);
            goldenGaugeVfx?.Advance(strokeCapture.HasUnlimitedInk,
                strokeCapture.UnlimitedInkActivationRevision, Time.deltaTime,
                VfxQualityRuntime.Tier, LobbySettingsProfile.ReducedMotionEnabled);
        }

        /// HUD 표시만 보간한다. 실제 먹 자원과 무제한 먹의 만료 시점은 건드리지 않는다.
        void AdvanceGaugePresentation(float actualRatio, bool golden, float deltaTime)
        {
            float target = golden ? 1f : Mathf.Clamp01(actualRatio);
            if (!displayedInkRatioInitialized)
            {
                displayedInkRatio = target;
                displayedInkRatioInitialized = true;
                displayedGolden = golden;
                goldenBlend = golden ? 1f : 0f;
                return;
            }

            if (displayedGolden != golden)
            {
                displayedGolden = golden;
                goldenTransitionTime = 0f;
                goldenTransitionStartRatio = displayedInkRatio;
                goldenTransitionStartBlend = goldenBlend;
            }
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            float duration = reduced ? 0.14f : golden ? 0.18f : 0.24f;
            if (goldenTransitionTime < duration)
            {
                goldenTransitionTime += Mathf.Max(0f, deltaTime);
                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(goldenTransitionTime / duration));
                displayedInkRatio = reduced ? target :
                    Mathf.Lerp(goldenTransitionStartRatio, target, t);
                goldenBlend = Mathf.Lerp(goldenTransitionStartBlend, golden ? 1f : 0f, t);
                return;
            }

            float seconds = target < displayedInkRatio
                ? GaugeConsumeFadeSeconds
                : GaugeRecoverFadeSeconds;
            goldenBlend = golden ? 1f : 0f;
            displayedInkRatio = Mathf.MoveTowards(
                displayedInkRatio,
                target,
                Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, seconds));
        }

        void OnGUI()
        {
            var manager = GameManager.Instance;
            bool tutorialGauge = manager != null && manager.State == GameState.Playing &&
                manager.PauseReason == GameplayPauseReason.FirstRunTutorial && !manager.IsTransitioning &&
                FirstRunTutorialController.Instance != null && FirstRunTutorialController.Instance.ShowInkGauge;
            if (manager == null || (!tutorialGauge && !ShouldRenderGameplayHud(manager.State, manager.IsPaused, manager.IsTransitioning))) return;

            // Play 중 스크립트 재컴파일에서는 Start가 다시 호출되지 않을 수 있다.
            // 비직렬화 참조가 사라진 경우 즉시 다시 묶어 게이지가 숨는 일을 막는다.
            if (strokeCapture == null)
                strokeCapture = FindAnyObjectByType<StrokeCapture>();

            // 화면 하단 먹 게이지: 화면에 더 남길 수 있는 먹 용량
            if (strokeCapture != null)
                DrawInkGauge(
                    displayedInkRatioInitialized
                        ? displayedInkRatio
                        : strokeCapture.InkRemaining01);
            if (!tutorialGauge) DrawGoldenBrushTimer();
        }

        // IMGUI/직접 그리기는 Canvas 정렬 뒤에도 표시될 수 있어 덮는 전환·모달에서는 아예 그리지 않는다.
        internal static bool ShouldRenderGameplayHud(GameState state, bool paused, bool transitioning) =>
            state == GameState.Playing && !paused && !transitioning;

        void EnsureRuntimeReferences()
        {
            if (strokeCapture == null)
                strokeCapture = FindAnyObjectByType<StrokeCapture>();
            BindManager(GameManager.Instance);
        }

        void BindManager(GameManager nextManager)
        {
            if (boundManager == nextManager)
                return;
            if (boundManager != null)
                boundManager.StateChanged -= HandleGameStateChanged;
            boundManager = nextManager;
            if (boundManager != null)
                boundManager.StateChanged += HandleGameStateChanged;
        }

        void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Lobby || previous == GameState.Lobby)
                goldenGaugeVfx?.Clear();
            // 새 판만 활성 획과 표시 캐시를 즉시 초기화한다. 광고 부활의
            // GameOver→Playing은 같은 판이므로 기존 먹 장부와 표시량을 보존한다.
            if (previous != GameState.Lobby || current != GameState.Playing)
                return;
            EnsureRuntimeReferences();
            strokeCapture?.CancelActiveStroke();
            displayedGolden = strokeCapture != null && strokeCapture.HasUnlimitedInk;
            goldenBlend = displayedGolden ? 1f : 0f;
            goldenTransitionTime = 1f;
            displayedInkRatio = displayedGolden || strokeCapture == null
                ? 1f : Mathf.Clamp01(strokeCapture.InkRemaining01);
            displayedInkRatioInitialized = true;
        }

        void InitializeDisplayedInkRatio()
        {
            if (strokeCapture == null)
                return;

            displayedGolden = strokeCapture.HasUnlimitedInk;
            goldenBlend = displayedGolden ? 1f : 0f;
            goldenTransitionTime = 1f;
            displayedInkRatio = displayedGolden ? 1f : Mathf.Clamp01(strokeCapture.InkRemaining01);
            displayedInkRatioInitialized = true;
        }

        /// 붓 획 모양 먹 게이지: 옅은 전체 용량 안내 위에서 남은 먹의 양만큼
        /// 불투명 채움 폭을 붓 쪽에 붙여 표시한다. 이미지 미할당 시 단색 막대 폴백.
        void DrawInkGauge(
            float ratio)
        {
            float baseRatio = Mathf.Clamp01(ratio);
            // 성장 여부와 무관하게 총량 붓획은 같은 크기·같은 비율을 유지한다.
            // 용량 증가는 실제 먹이 더 천천히 줄어드는 동작으로 이미 체감된다.
            Rect safeGui = MobileUiLayout.ToGuiSafeArea(
                MobileUiLayout.CurrentSafeArea,
                Screen.width,
                Screen.height);
            bool compactWideWeb = ShouldUseCompactWideWebLayout(
                Application.platform,
                Screen.width,
                Screen.height);
            float gaugeLayoutWidth = CalculateGaugeLayoutWidth(
                safeGui.width,
                safeGui.height,
                compactWideWeb);
            float horizontalMargin = Mathf.Max(18f, safeGui.width * 0.04f);
            float bottomMargin = CalculateGaugeLayoutBottomMargin(
                gaugeLayoutWidth,
                safeGui.height,
                compactWideWeb);
            float gaugeBottom = CalculateGaugeBottom(
                safeGui.yMax,
                Screen.height);
            float maximumGaugeWidth = Mathf.Max(1f,
                safeGui.width - horizontalMargin * 2f);
            // 바깥 먹붓 UI는 잔량과 무관하게 최대 용량 폭을 계속 유지한다.
            // ratio는 아래의 안쪽 채움에만 사용해, 먹을 써도 게이지 자체가
            // 작아지는 것처럼 보이지 않게 한다.
            Texture2D trackSilhouette = inkGaugeTrack != null
                ? inkGaugeTrack
                : inkGaugeFill;
            Texture2D fillSilhouette = inkGaugeFill != null
                ? inkGaugeFill
                : trackSilhouette;
            float iconSize = inkBrushIcon != null
                ? CalculateBrushIconSize(gaugeLayoutWidth)
                : 0f;
            float overlap = iconSize * BrushOverlapRatio;
            float portraitClusterWidth = CalculatePortraitClusterWidth(safeGui.width);
            float gaugeTrackWidth = compactWideWeb
                ? CalculateGaugeTrackWidth(gaugeLayoutWidth, horizontalMargin)
                : Mathf.Max(1f, portraitClusterWidth - iconSize + overlap);
            if (trackSilhouette == null)
            {
                float bw = gaugeTrackWidth;
                float bh = CalculateFallbackGaugeHeight(gaugeLayoutWidth);
                float by = gaugeBottom - bottomMargin - bh;
                float fallbackX = compactWideWeb
                    ? safeGui.center.x - bw * 0.5f
                    : safeGui.xMin + CalculatePortraitHorizontalInset(safeGui.width);
                var back = new Rect(
                    fallbackX,
                    by,
                    bw,
                    bh);
                DrawGoldenGaugeParticles(back, safeGui, true);
                // 비어 있어도 총 용량의 위치만 옅은 먹색으로 남긴다. 한지색 트랙을
                // 깔면 먹을 쓸수록 검정→흰색으로 바뀌어 농도 UI로 읽히지 않는다.
                Color guideColor = InkPalette.Ink;
                guideColor.a = EmptyGaugeGuideAlpha;
                DrawRect(back, guideColor);
                Color fillColor = Color.Lerp(InkPalette.Ink, InkPalette.Gold, goldenBlend);
                Rect fillRect = CalculateGaugeFillRect(
                    back,
                    baseRatio,
                    false);
                if (fillRect.width > 0f)
                    DrawRect(fillRect, fillColor);
                DrawGoldenGaugeParticles(back, safeGui, false);
                return;
            }

            // 세로 모바일은 안전 폭의 양쪽 8%를 비워 게이지와 붓이 가장자리에 붙지 않게 한다.
            // 양끝 붓터치는 아래 3-slice 렌더러가 보존한다.
            float w = gaugeTrackWidth;
            // 두께와 붓 크기는 기기 폭을 기준으로 고정해 19.5:9 실기기에서도
            // 시뮬레이터와 같은 시각 비율을 유지한다.
            float h = CalculateGaugeVisualHeight(gaugeLayoutWidth);
            float totalW = w + iconSize - overlap;
            if (compactWideWeb && totalW > maximumGaugeWidth)
            {
                float fit = maximumGaugeWidth / totalW;
                w *= fit;
                h *= fit;
                iconSize *= fit;
                overlap *= fit;
                totalW = w + iconSize - overlap;
            }

            // 아이콘(게이지보다 큼)까지 포함한 전체가 화면 아래로 짤리지 않도록 배치
            float clusterH = Mathf.Max(h, iconSize);
            float centerY = gaugeBottom - bottomMargin - clusterH * 0.5f;
            float x = compactWideWeb
                ? safeGui.center.x - totalW * 0.5f
                : safeGui.xMin + CalculatePortraitHorizontalInset(safeGui.width);
            var iconRect = new Rect(x + w - overlap,
                centerY - iconSize * 0.5f, iconSize, iconSize);
            Rect area = CalculateBrushConnectedTrackRect(x, w, h, centerY, iconSize);
            DrawGoldenGaugeParticles(area, safeGui, true);
            // 빈 상태도 채움과 완전히 같은 실루엣을 쓴다. 별도 트랙
            // 이미지를 깔면 실기기에서 먹색이 바뀌는 것처럼 보일 수 있다.
            DrawTextureWithTintSlicedHorizontal(
                area,
                trackSilhouette,
                InkPalette.Ink,
                EmptyGaugeGuideAlpha);

            Rect gaugeFillRect = CalculateGaugeFillRect(
                area,
                baseRatio,
                false);
            if (gaugeFillRect.width > 0f)
            {
                // 붓에 붙은 오른쪽 끝은 고정하고 왼쪽부터 실제 사용량만큼 비운다.
                // UV도 같은 비율로 잘라 부분 이미지가 눌려 보이지 않게 한다.
                Texture2D fillTexture = fillSilhouette;
                if (gaugeTintMaterial != null)
                    gaugeTintMaterial.SetFloat(RecolorId, goldenBlend);
                if (fillTexture != null)
                    DrawTextureWithTintSlicedHorizontalCropped(
                        area,
                        gaugeFillRect,
                        fillTexture,
                        Color.white,
                        1f,
                        goldenBlend > 0f ? gaugeTintMaterial : null);
            }
            DrawGoldenGaugeParticles(area, safeGui, false);
            if (inkBrushIcon != null)
            {
                GUI.DrawTexture(iconRect, inkBrushIcon, ScaleMode.ScaleToFit);
            }
        }

        void DrawGoldenGaugeParticles(Rect gauge, Rect safeGui, bool behind)
        {
            if (Event.current.type != EventType.Repaint) return;
            goldenGaugeVfx?.Draw(gauge, safeGui, new Vector2(Screen.width, Screen.height), behind);
        }

        void DrawGoldenBrushTimer()
        {
            if (strokeCapture == null || !strokeCapture.HasUnlimitedInk ||
                goldenTimerMaterial == null || Event.current.type != EventType.Repaint) return;
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
                gameplayCamera = Camera.main;
            Rect safeGui = MobileUiLayout.ToGuiSafeArea(
                MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);
            if (!TryGetGoldenTimerRect(GameManager.Instance, gameplayCamera,
                    safeGui, Screen.height, out Rect timerRect)) return;
            goldenTimerMaterial.SetFloat(RemainingId, strokeCapture.UnlimitedInkRemaining01);
            // 카메라·체력 표시의 LateUpdate 이후 투영한다. 별도 시계나 입력 영역은 없다.
            Graphics.DrawTexture(timerRect, Texture2D.whiteTexture,
                new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, goldenTimerMaterial);
        }

        bool TryGetGoldenTimerRect(GameManager manager, Camera camera,
            Rect safeGui, float screenHeight, out Rect timerRect)
        {
            timerRect = default;
            if (manager == null || !ShouldRenderGameplayHud(manager.State, manager.IsPaused, manager.IsTransitioning) || camera == null ||
                !manager.TryGetSwarmCameraFrame(out var owner, out _, out _) ||
                !owner.isActiveAndEnabled) return false;
            // 황금 붓은 먹떼 공통 효과다. 카메라가 따라가는 생존자에게 링 하나만 붙인다.
            if (goldenTimerOwner != owner || goldenTimerBody == null)
            {
                goldenTimerOwner = owner;
                goldenTimerBody = owner.GetComponent<SpriteRenderer>();
                goldenTimerHealth = owner.GetComponent<PlayerHealthBillboard>();
            }
            if (goldenTimerBody == null || !goldenTimerBody.enabled ||
                goldenTimerBody.forceRenderingOff || goldenTimerBody.sprite == null ||
                (camera.cullingMask & (1 << owner.gameObject.layer)) == 0 ||
                !TryProjectTimerBounds(goldenTimerBody.bounds, camera, screenHeight,
                    out Rect bodyRect)) return false;

            // 1024px 캐릭터 프레임의 투명 테두리 안쪽을 기준으로 붙인다.
            // 물리·원화·체력 위치는 유지하고 시간 링의 시각 앵커만 좁힌다.
            bodyRect = InsetGoldenTimerBodyRect(bodyRect);

            Rect healthRect = default;
            var health = goldenTimerHealth != null ? goldenTimerHealth.HealthRenderer : null;
            if (health != null && health.enabled && health.sprite != null)
                TryProjectTimerBounds(health.bounds, camera, screenHeight, out healthRect);
            return TryCalculateGoldenTimerRect(bodyRect, healthRect, safeGui, out timerRect);
        }

        static Rect InsetGoldenTimerBodyRect(Rect frame)
        {
            const float transparentInset = 0.12f;
            return new Rect(frame.x + frame.width * transparentInset,
                frame.y + frame.height * transparentInset,
                frame.width * (1f - transparentInset * 2f),
                frame.height * (1f - transparentInset * 2f));
        }

        static bool TryProjectTimerBounds(Bounds bounds, Camera camera,
            float screenHeight, out Rect rect)
        {
            rect = default;
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = camera.WorldToScreenPoint(new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z));
                if (point.z < camera.nearClipPlane || point.z > camera.farClipPlane) return false;
                var guiPoint = new Vector2(point.x, screenHeight - point.y);
                minimum = Vector2.Min(minimum, guiPoint);
                maximum = Vector2.Max(maximum, guiPoint);
            }
            rect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return rect.width > 0f && rect.height > 0f;
        }

        static bool TryCalculateGoldenTimerRect(Rect body, Rect health, Rect safeGui,
            out Rect timerRect)
        {
            timerRect = default;
            if (!safeGui.Overlaps(body)) return false;
            float shortSide = Mathf.Min(safeGui.width, safeGui.height);
            float diameter = Mathf.Clamp(shortSide * 0.0476f, 20f, 60f);
            const float inset = 8f;
            if (shortSide < diameter + inset * 2f) return false;
            float gap = Mathf.Max(1.5f, diameter * 0.06f);
            bool hasHealth = health.width > 0f && health.height > 0f;
            float desiredX = body.xMax + gap;
            // 좌우 간격은 유지하고 링 중심만 머리와 체력 바 사이로 올린다.
            float centerY = hasHealth && health.yMax <= body.yMin
                ? (health.yMax + body.yMin) * 0.5f : body.yMin;
            float y = centerY - diameter * 0.5f;
            // 긴 체력 바에 닿을 때도 옆으로 밀지 않고 세로 위치만 보정한다.
            if (hasHealth && health.Overlaps(new Rect(desiredX, y, diameter, diameter)))
                y = health.yMax + gap;
            float x = Mathf.Clamp(desiredX, safeGui.xMin + inset,
                safeGui.xMax - diameter - inset);
            // 오른쪽 벽에서는 머리 위로 조금 올려 본체·체력과 겹치지 않게 한다.
            if (x < desiredX - 0.01f)
                y = (hasHealth ? Mathf.Min(body.yMin, health.yMin) : body.yMin) - diameter - gap;
            y = Mathf.Clamp(y, safeGui.yMin + inset, safeGui.yMax - diameter - inset);
            var result = new Rect(x, y, diameter, diameter);
            if (result.Overlaps(body) || (hasHealth && result.Overlaps(health))) return false;
            timerRect = result;
            return true;
        }

        static Rect CalculateBrushConnectedTrackRect(float x, float width,
            float height, float clusterCenterY, float iconSize)
        {
            float contactY = clusterCenterY + Mathf.Max(0f, iconSize) *
                (BrushContactHeightRatio - 0.5f);
            return new Rect(x, contactY - height * 0.5f, width, height);
        }

        static float CalculateGaugeTrackWidth(
            float safeWidth,
            float horizontalMargin)
        {
            float width = Mathf.Max(1f, safeWidth);
            float maximumWidth = Mathf.Max(1f, width - Mathf.Max(0f, horizontalMargin) * 2f);
            return Mathf.Min(
                maximumWidth,
                width * GaugeWidthRatio);
        }

        static float CalculatePortraitHorizontalInset(float safeWidth) =>
            Mathf.Max(1f, safeWidth) * PortraitHorizontalInsetRatio;

        static float CalculatePortraitClusterWidth(float safeWidth) =>
            Mathf.Max(1f, Mathf.Max(1f, safeWidth) - CalculatePortraitHorizontalInset(safeWidth) * 2f);

        static Rect CalculatePortraitHorizontalBounds(
            float safeXMin,
            float safeWidth)
        {
            float width = CalculatePortraitClusterWidth(safeWidth);
            return new Rect(
                safeXMin + CalculatePortraitHorizontalInset(safeWidth),
                0f,
                width,
                0f);
        }

        static bool ShouldUseCompactWideWebLayout(
            RuntimePlatform platform,
            int screenWidth,
            int screenHeight)
        {
            return platform == RuntimePlatform.WebGLPlayer &&
                   screenWidth > screenHeight &&
                   screenHeight > 0;
        }

        static float CalculateGaugeLayoutWidth(
            float safeWidth,
            float safeHeight,
            bool compactWideWeb)
        {
            float width = Mathf.Max(1f, safeWidth);
            if (!compactWideWeb)
                return width;

            // 가로 WebGL은 브라우저 폭을 그대로 쓰면 붓이 게임 화면을 과도하게
            // 차지한다. 높이를 기준으로 제한해 세로 모바일과 같은 시각 무게를 유지한다.
            return Mathf.Min(
                width,
                Mathf.Max(1f, safeHeight) * WideWebGaugeWidthPerHeight);
        }

        static float CalculateGaugeLayoutBottomMargin(
            float gaugeLayoutWidth,
            float safeHeight,
            bool compactWideWeb)
        {
            if (!compactWideWeb)
                return CalculateGaugeBottomMargin(gaugeLayoutWidth);

            return Mathf.Max(
                WideWebMinimumBottomMargin,
                Mathf.Max(1f, safeHeight) * WideWebBottomMarginRatio);
        }

        static float CalculateGaugeVisualHeight(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * GaugeVisualHeightRatio;
        }

        /// 위험 예고가 하단 게이지 뒤에 숨지 않도록 실제 HUD 배치의 위쪽 경계를 공유한다.
        /// 화면 아래에서 위로 증가하는 픽셀 좌표이며, HUD 자체 위치는 변경하지 않는다.
        public static float CalculateGaugeTopScreenY(Rect safeArea, int screenWidth,
            int screenHeight, RuntimePlatform platform)
        {
            Rect safeGui = MobileUiLayout.ToGuiSafeArea(safeArea, screenWidth, screenHeight);
            bool compact = ShouldUseCompactWideWebLayout(platform, screenWidth, screenHeight);
            float width = CalculateGaugeLayoutWidth(safeGui.width, safeGui.height, compact);
            float bottom = CalculateGaugeBottom(safeGui.yMax, screenHeight);
            float margin = CalculateGaugeLayoutBottomMargin(width, safeGui.height, compact);
            float clusterHeight = Mathf.Max(CalculateGaugeVisualHeight(width), CalculateBrushIconSize(width));
            return screenHeight - bottom + margin + clusterHeight;
        }

        static float CalculateBrushIconSize(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * BrushIconSizeRatio;
        }

        static float CalculateFallbackGaugeHeight(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * FallbackGaugeHeightRatio;
        }

        static float CalculateGaugeBottomMargin(float safeWidth)
        {
            return Mathf.Max(
                MinimumGaugeBottomMargin,
                Mathf.Max(1f, safeWidth) * GaugeBottomMarginRatio);
        }

        static float CalculateGaugeBottom(
            float safeBottom,
            float screenHeight)
        {
            float physicalBottom = Mathf.Max(1f, screenHeight);
            float clampedSafeBottom = Mathf.Clamp(
                safeBottom,
                0f,
                physicalBottom);
            return Mathf.Lerp(
                clampedSafeBottom,
                physicalBottom,
                GaugeBottomSafeAreaBleed);
        }

        static float ResolveGaugeFillRatio(float ratio, bool golden)
        {
            return golden ? 1f : Mathf.Clamp01(ratio);
        }

        static Color ResolveGaugeFillTint(bool golden) =>
            golden ? InkPalette.Gold : Color.white;

        static Rect CalculateGaugeFillRect(
            Rect area,
            float ratio,
            bool golden)
        {
            float fillRatio = ResolveGaugeFillRatio(ratio, golden);
            float fillWidth = area.width * fillRatio;
            return new Rect(
                area.xMax - fillWidth,
                area.y,
                fillWidth,
                area.height);
        }

        static Rect CalculateGaugeFillUv(float ratio, bool golden)
        {
            float fillRatio = ResolveGaugeFillRatio(ratio, golden);
            return new Rect(1f - fillRatio, 0f, fillRatio, 1f);
        }

        static void DrawRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawTextureWithTint(
            Rect rect,
            Texture2D texture,
            Color tint,
            float alpha)
        {
            if (texture == null) return;
            Color previous = GUI.color;
            GUI.color = new Color(
                tint.r,
                tint.g,
                tint.b,
                tint.a * Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        static void DrawTextureWithTintCropped(
            Rect rect,
            Texture2D texture,
            Color tint,
            Rect uv)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previous;
        }

        static void DrawTextureWithTintSlicedHorizontal(
            Rect rect,
            Texture2D texture,
            Color tint,
            float alpha)
        {
            DrawTextureWithTintSlicedHorizontalCropped(
                rect,
                rect,
                texture,
                tint,
                alpha);
        }

        static void DrawTextureWithTintSlicedHorizontalCropped(
            Rect fullRect,
            Rect visibleRect,
            Texture2D texture,
            Color tint,
            float alpha,
            Material material = null)
        {
            if (texture == null ||
                fullRect.width <= 0f ||
                fullRect.height <= 0f ||
                visibleRect.width <= 0f ||
                visibleRect.height <= 0f)
                return;

            float sourceCapRatio = Mathf.Clamp(
                texture.height / (float)Mathf.Max(1, texture.width),
                0.08f,
                0.25f);
            float destinationCapWidth = Mathf.Min(
                fullRect.height,
                fullRect.width * 0.5f);
            float centerWidth = Mathf.Max(
                0f,
                fullRect.width - destinationCapWidth * 2f);

            Color previous = GUI.color;
            GUI.color = new Color(
                tint.r,
                tint.g,
                tint.b,
                tint.a * Mathf.Clamp01(alpha));

            DrawHorizontalSlice(
                new Rect(
                    fullRect.x,
                    fullRect.y,
                    destinationCapWidth,
                    fullRect.height),
                visibleRect,
                texture,
                new Rect(0f, 0f, sourceCapRatio, 1f), material);
            if (centerWidth > 0f)
            {
                DrawHorizontalSlice(
                    new Rect(
                        fullRect.x + destinationCapWidth,
                        fullRect.y,
                        centerWidth,
                        fullRect.height),
                    visibleRect,
                    texture,
                    new Rect(
                        sourceCapRatio,
                        0f,
                        1f - sourceCapRatio * 2f,
                        1f), material);
            }
            DrawHorizontalSlice(
                new Rect(
                    fullRect.xMax - destinationCapWidth,
                    fullRect.y,
                    destinationCapWidth,
                    fullRect.height),
                visibleRect,
                texture,
                new Rect(
                    1f - sourceCapRatio,
                    0f,
                    sourceCapRatio,
                    1f), material);

            GUI.color = previous;
        }

        static void DrawHorizontalSlice(
            Rect destination,
            Rect visibleRect,
            Texture2D texture,
            Rect uv,
            Material material = null)
        {
            float xMin = Mathf.Max(destination.xMin, visibleRect.xMin);
            float xMax = Mathf.Min(destination.xMax, visibleRect.xMax);
            float yMin = Mathf.Max(destination.yMin, visibleRect.yMin);
            float yMax = Mathf.Min(destination.yMax, visibleRect.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return;

            float xStart01 = destination.width > 0f
                ? (xMin - destination.xMin) / destination.width
                : 0f;
            float xEnd01 = destination.width > 0f
                ? (xMax - destination.xMin) / destination.width
                : 1f;
            float yStart01 = destination.height > 0f
                ? (yMin - destination.yMin) / destination.height
                : 0f;
            float yEnd01 = destination.height > 0f
                ? (yMax - destination.yMin) / destination.height
                : 1f;
            var clippedUv = new Rect(
                Mathf.Lerp(uv.xMin, uv.xMax, xStart01),
                Mathf.Lerp(uv.yMin, uv.yMax, yStart01),
                uv.width * (xEnd01 - xStart01),
                uv.height * (yEnd01 - yStart01));
            var drawRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            if (material != null)
            {
                if (Event.current.type == EventType.Repaint)
                    Graphics.DrawTexture(drawRect, texture, clippedUv, 0, 0, 0, 0,
                        Color.white, material);
            }
            else
                GUI.DrawTextureWithTexCoords(drawRect, texture, clippedUv, true);
        }

    }
}
