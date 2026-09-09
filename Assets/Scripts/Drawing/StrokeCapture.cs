using System;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.AI;
using UnityEngine.UI;

namespace MukJump.Drawing
{
    /// 터치/마우스 스트로크를 월드 좌표 점열로 캡처한다.
    /// 손을 떼면 BezierSmoother로 다듬어 PlatformCollider 발판을 생성한다.
    public class StrokeCapture : MonoBehaviour
    {
        public const float DefaultInkCapacity = 5.2f;
        const float LegacyInkCapacityV0 = 12f;
        const float LegacyInkCapacityV1 = 18f;
        const float LegacyInkCapacityV2 = 24f;
        const float LegacyInkCapacityV3 = 3.2f;
        const float LegacyInkCapacityV4Baseline = 4.8f;
        public const int CurrentInkCapacityTuningVersion = 4;

        [Tooltip("이 간격(월드 단위) 이상 움직였을 때만 점 추가")]
        [SerializeField] float minPointDistance = 0.15f;
        [Tooltip("한 획의 최대 길이. 넘치면 그 지점에서 획을 끊고 이어 그린다")]
        [SerializeField] float maxContinuousStrokeLength = 30f;
        [Tooltip("이보다 짧은 획은 발판으로 만들지 않는다")]
        [SerializeField] float minStrokeLength = 0.6f;
        [SerializeField] float previewWidth = 0.4f;
        [Tooltip("LineSprite 프리팹의 600px 붓획 텍스처")]
        [SerializeField] Texture2D lineSpriteTexture;

        [Header("최대 먹 용량 — 화면에 유지되는 총 먹선 길이")]
        [Tooltip("동시에 유지할 수 있는 먹선의 기본 월드 길이")]
        [SerializeField] float inkCapacity = DefaultInkCapacity;
        [Tooltip("최대 먹 용량을 넘긴 오래된 획이 시작점부터 지워지는 시간")]
        [SerializeField] float evictionFadeDuration = 1.1f;
        [Tooltip("유효 먹선이 선명하게 유지된 뒤 자연 소멸을 시작하는 시간")]
        [SerializeField] float naturalHoldDuration =
            PlatformCollider.DefaultNaturalHoldDuration;
        // 기존 Main 씬에는 이 필드가 없으므로 0을 유지해야 구 12/18/24m 값과
        // 이전 3.2m를 v3 4.8m로 정규한 뒤, v4 5.2m로 단계 이관할 수 있다.
        // 새 씬은 빌더가 현재 버전을 명시한다.
        [SerializeField, HideInInspector] int inkCapacityTuningVersion;

        readonly List<Vector2> points = new();
        Camera cam;
        bool drawing;
#if UNITY_EDITOR
        // Recorder가 포인터 장치 대신 실제 획 처리 경로를 구동하는 동안에는
        // Update의 "포인터가 떼어짐" 판정이 같은 프레임에 획을 끝내면 안 된다.
        bool recordingStrokeActive;
#endif
        float strokeLength;
        LineRenderer preview;
        float unlimitedInkRemaining;
        float unlimitedInkDuration;
        RunGrowthController growthController;
        bool growthControllerBound;
        Player.ScreenSideWalls screenSideWalls;
        float appliedInkCapacity;
        bool unlimitedInkWasActive;
        readonly List<Vector2> safeSegment = new();
        readonly List<Vector2> safeSegmentCandidate = new();

        /// 튜토리얼·분석 계층이 포인터 해제나 오브젝트 수를 추측하지 않고
        /// 실제 유효 발판 생성만 관찰하는 계약이다.
        public event Action<PlatformCollider, float, float> ValidStrokeCreated;

        /// HUD 먹 게이지용. 아직 화면에 남은 소멸 잔상이 아니라,
        /// 지금 실제로 다시 그릴 수 있는 총 먹 예산을 표시한다.
        public bool HasUnlimitedInk => unlimitedInkRemaining > 0f;
        /// 같은 프레임 재획득도 HUD가 시간 변화와 무관하게 관찰한다. 게임 판정에는 사용하지 않는다.
        public uint UnlimitedInkActivationRevision { get; private set; }
        public float UnlimitedInkRemainingSeconds => Mathf.Max(0f, unlimitedInkRemaining);
        public float UnlimitedInkRemaining01 => unlimitedInkDuration > 0f
            ? Mathf.Clamp01(unlimitedInkRemaining / unlimitedInkDuration) : 0f;
        public float PendingStrokeBudgetCost => drawing
            ? StrokeBudgetCost(
                strokeLength,
                ActivePermanentGrowth.InkBudgetCostMultiplier,
                ActivePermanentGrowth.ShortStrokeBudgetCostMultiplier)
            : 0f;
        public float CurrentInkUsage =>
            Mathf.Max(
                0f,
                PlatformCollider.ActiveInkCost + PendingStrokeBudgetCost);
        public float CurrentInkRemaining =>
            Mathf.Max(0f, EffectiveInkCapacity - CurrentInkUsage);
        public float InkRemaining01
        {
            get
            {
                return CurrentInkRemaining /
                       Mathf.Max(0.001f, EffectiveInkCapacity);
            }
        }
        /// HUD 트랙의 실제 최대 길이. 영구 성장과 날씨를 반영한다.
        public float InkCapacityRatio =>
            EffectiveInkCapacity / Mathf.Max(0.001f, inkCapacity);
        public float BaseEffectiveInkCapacity =>
            inkCapacity *
            ActivePermanentGrowth.InkCapacityMultiplier *
            Mathf.Clamp(PlatformCollider.RuntimeInkCapacityMultiplier, 0.35f, 1f);
        public float EffectiveInkCapacity => BaseEffectiveInkCapacity;
        public float EffectiveEvictionFadeDuration =>
            evictionFadeDuration /
            Mathf.Max(1f, ActivePermanentGrowth.InkRecoverySpeedMultiplier);
        public float EffectiveNaturalHoldDuration =>
            naturalHoldDuration +
            ActivePermanentGrowth.NaturalInkHoldBonusSeconds;
        /// 일반 회복 성장은 번짐 구간만 단축하고, 넓은 벼루 결실만 선명 유지시간을 늘린다.
        public float EffectiveNaturalInkLifetime =>
            EffectiveNaturalHoldDuration +
            EffectiveEvictionFadeDuration;

        PermanentGrowthRunSnapshot ActivePermanentGrowth =>
            RunGrowthController.Instance != null
                ? RunGrowthController.Instance.PermanentSnapshot
                : PermanentGrowthProfile.CreateRunSnapshot();

        void Awake()
        {
            UpgradeInkCapacityTuning();
        }

        public void ActivateUnlimitedInk(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return;
            unchecked { UnlimitedInkActivationRevision++; }
            float nextRemaining = Mathf.Max(
                unlimitedInkRemaining,
                Mathf.Max(0f, duration));
            // 재획득으로 시간이 실제 연장될 때만 링을 가득 채운다.
            if (nextRemaining > unlimitedInkRemaining)
                unlimitedInkDuration = nextRemaining;
            unlimitedInkRemaining = nextRemaining;
            unlimitedInkWasActive = HasUnlimitedInk;
        }

        void OnEnable()
        {
            // Play 중 스크립트 재컴파일 뒤에도 열린 씬의 구형 12m 값이 즉시 갱신된다.
            UpgradeInkCapacityTuning();
            PermanentGrowthProfile.Changed -= HandlePermanentGrowthChanged;
            PermanentGrowthProfile.Changed += HandlePermanentGrowthChanged;
            RecoverRuntimeReferences();
            TryBindGrowthController();
        }

        void OnDisable()
        {
            // 컴포넌트 비활성화가 입력 도중 발생해도 미리보기와 붓 루프음이
            // 다음 화면에 남지 않도록 드로잉 상태까지 함께 정리한다.
            CancelActiveStroke();
            PermanentGrowthProfile.Changed -= HandlePermanentGrowthChanged;
            UnbindGrowthController();
        }

        void Start()
        {
            cam = Camera.main;
            if (cam == null)
                Debug.LogError("[MukJump] MainCamera를 찾을 수 없어 드로잉 좌표를 변환할 수 없습니다.", this);
            else
                screenSideWalls = cam.GetComponent<Player.ScreenSideWalls>();
            TryBindGrowthController();
            appliedInkCapacity = EffectiveInkCapacity;
            unlimitedInkWasActive = HasUnlimitedInk;
            UseLineSpriteFromMainUi();
        }

        void UseLineSpriteFromMainUi()
        {
            // LineSprite는 붓결 텍스처를 보관하는 제작용 프리팹이다. 씬에 남아 있는
            // 이전 빌드의 인스턴스는 화면 중앙에 획처럼 보이지 않도록 즉시 숨긴다.
            HideLineSpriteTemplates();

            if (lineSpriteTexture != null)
            {
                FallbackInkStyle.SetBrushTexture(lineSpriteTexture);
                return;
            }

            var rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include);
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (!rawImages[i].name.Equals("LineSprite", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                FallbackInkStyle.SetBrushTexture(rawImages[i].texture as Texture2D);
                return;
            }

            // 기존 Main을 재빌드하지 않아도 LineSprite 프리팹과 같은 원본 텍스처를 쓰는
            // 고도 먹 UI에서 텍스처를 가져올 수 있다.
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (rawImages[i].texture is not Texture2D texture ||
                    !texture.name.Equals("muk_start_button", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                FallbackInkStyle.SetBrushTexture(texture);
                return;
            }

            var images = FindObjectsByType<Image>(FindObjectsInactive.Include);
            for (int i = 0; i < images.Length; i++)
            {
                if (!images[i].name.Equals("LineSprite", System.StringComparison.OrdinalIgnoreCase) ||
                    images[i].sprite == null) continue;
                FallbackInkStyle.SetBrushTexture(images[i].sprite.texture);
                return;
            }

            Debug.LogWarning("[MukJump] Main UI에서 LineSprite를 찾지 못해 기존 절차적 붓선을 사용합니다.", this);
        }

        static void HideLineSpriteTemplates()
        {
            var rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include);
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (rawImages[i].name.Equals("LineSprite", System.StringComparison.OrdinalIgnoreCase))
                    rawImages[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            RecoverRuntimeReferences();
            if (cam == null) return;
            TryBindGrowthController();

            if (GameManager.Instance == null)
            {
                if (drawing) CancelStroke();
                return;
            }

            if (GameManager.Instance.State == GameState.Lobby)
            {
                // 로비는 명시적인 시작·성장·옵션 버튼만 입력받는다.
                // 여기서 획을 받으면 UI 탭과 동시에 발판이 생기는 입력 경합이 발생한다.
                if (drawing) CancelStroke();
                return;
            }

            if (GameManager.Instance.State != GameState.Playing)
            {
                if (drawing) CancelStroke();
                return;
            }

            if (GameManager.Instance.IsPaused)
            {
                if (drawing) CancelStroke();
                return;
            }

            AdvanceUnlimitedInk(Time.deltaTime);
            RefreshInkBudget();

#if UNITY_EDITOR
            if (!ShouldProcessLivePointer(recordingStrokeActive))
                return;
#endif

            if (PointerInput.TryGetPressed(out var screenPos))
            {
                if (LobbyAdLayout.IsPointerInBannerSlot(screenPos))
                {
                    if (drawing) CancelStroke();
                    PointerInput.SuppressUntilRelease();
                    return;
                }
                if (GameplayHudView.IsPointerOverItemTestControls(screenPos) ||
                    PauseMenuView.IsPointerOverControls(screenPos) ||
                    FirstRunTutorialController.IsPointerOverControls(screenPos))
                {
                    if (drawing) CancelStroke();
                    return;
                }

                if (drawing)
                    ContinueStroke(screenPos);
                else
                    BeginStroke(screenPos);
            }
            else if (drawing)
            {
                if (PointerInput.TryGetReleased(out var releasePosition) &&
                    !LobbyAdLayout.IsPointerInBannerSlot(releasePosition))
                    AppendWorldSampleWithTail(ToWorld(releasePosition), true);
                EndStroke();
            }
        }

        Vector2 ToWorld(Vector2 screenPos)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        }

        void BeginStroke(Vector2 screenPos) => BeginStrokeAtWorld(ToWorld(screenPos));

        void BeginStrokeAtWorld(Vector2 worldPos)
        {
            drawing = true;
            strokeLength = 0f;
            points.Clear();
            points.Add(worldPos);
            GameFeedbackController.Instance?.StartBrushDrawing();
            CreatePreview();
        }

        void ContinueStroke(Vector2 screenPos)
        {
            AppendWorldSample(ToWorld(screenPos));
        }

        /// 한 프레임의 큰 포인터 이동도 30m 경계에서 정확히 보간한다.
        /// 경계 뒤의 잔여 구간은 같은 프레임에 다음 획으로 넘겨 틈을 만들지 않는다.
        void AppendWorldSample(Vector2 requestedWorld)
            => AppendWorldSampleWithTail(requestedWorld, false);

        void AppendWorldSampleWithTail(Vector2 requestedWorld, bool includeShortTail)
        {
            while (drawing && points.Count > 0)
            {
                Vector2 segmentStart = points[^1];
                float requestedStep = Vector2.Distance(segmentStart, requestedWorld);
                if (requestedStep < (includeShortTail ? 0.0001f : minPointDistance))
                    return;

                float remainingStroke = Mathf.Max(
                    0f,
                    maxContinuousStrokeLength - strokeLength);
                if (remainingStroke <= 0.0001f)
                {
                    FinalizeStrokeAndRestartAtSeam();
                    continue;
                }

                bool crossesStrokeBoundary = requestedStep > remainingStroke;
                float targetStep = crossesStrokeBoundary
                    ? remainingStroke
                    : requestedStep;
                Vector2 targetWorld = Vector2.MoveTowards(
                    segmentStart,
                    requestedWorld,
                    targetStep);

                strokeLength += targetStep;
                points.Add(targetWorld);
                GameFeedbackController.Instance?.PlayBrushMovement(targetStep);
                UpdatePreview();

                if (!crossesStrokeBoundary)
                    return;

                FinalizeStrokeAndRestartAtSeam();
                // requestedWorld은 그대로 유지해 seam 뒤 꼬리를 즉시 처리한다.
            }
        }

        static float StrokeBudgetCost(
            float rawLength,
            float globalCostMultiplier,
            float shortStrokeMultiplier)
        {
            rawLength = Mathf.Max(0f, rawLength);
            float multiplier = Mathf.Clamp(globalCostMultiplier, 0.55f, 1f);
            if (rawLength <= 1.5f + 0.0001f)
                multiplier *= Mathf.Clamp(shortStrokeMultiplier, 0.55f, 1f);
            return rawLength * multiplier;
        }

        void EndStroke()
        {
            // 최대 길이 분할과 포인터 해제가 같은 획을 연달아 종료해도 이미 확정한
            // 점열로 발판을 다시 만들지 않는다. 활성 획만 한 번 선점해 확정한다.
            if (!drawing)
                return;

            drawing = false;
            GameFeedbackController.Instance?.StopBrushDrawing();
            DestroyPreview();
            Vector2 feedbackPosition = points.Count > 0 ? points[^1] : Vector2.zero;

            if (points.Count < 2 || strokeLength < minStrokeLength)
            {
                GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, false);
                return;
            }

            var smoothed = BezierSmoother.Smooth(points);
            if (smoothed.Count < 2)
            {
                GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, false);
                return;
            }

            // 캐릭터가 지나와도 그린 획은 보존한다. 화면 양옆 먹벽 띠만 제외하고,
            // 생성 순간 겹친 몸체의 충돌은 PlatformCollider에서 개별 유예한다.
            smoothed = LongestSafeSegment(smoothed);
            float validLength = BezierSmoother.PolylineLength(smoothed);
            if (smoothed.Count < 2 || validLength < minStrokeLength)
            {
                GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, false);
                return;
            }

            float budgetCost = StrokeBudgetCost(
                validLength,
                ActivePermanentGrowth.InkBudgetCostMultiplier,
                ActivePermanentGrowth.ShortStrokeBudgetCostMultiplier);
            PlatformCollider platform = PlatformCollider.Spawn(
                smoothed,
                budgetCost,
                evictionFadeSeconds: EffectiveEvictionFadeDuration,
                evictionDelaySeconds: 0f,
                naturalHoldSeconds: EffectiveNaturalHoldDuration);
            if (!HasUnlimitedInk)
                PlatformCollider.ReconcileActiveInkBudget(EffectiveInkCapacity);
            MukJumpAnalytics.Stroke(validLength);
            ValidStrokeCreated?.Invoke(platform, validLength, budgetCost);
            GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, true);
        }

        void FinalizeStrokeAndRestartAtSeam()
        {
            if (!drawing || points.Count == 0)
                return;

            Vector2 seam = points[^1];
            EndStroke();
            BeginStrokeAtWorld(seam);
        }

        void CancelStroke()
        {
            drawing = false;
#if UNITY_EDITOR
            recordingStrokeActive = false;
#endif
            GameFeedbackController.Instance?.StopBrushDrawing();
            DestroyPreview();
        }

        public void CancelActiveStroke()
        {
            if (drawing)
                CancelStroke();
            else
            {
#if UNITY_EDITOR
                recordingStrokeActive = false;
#endif
                GameFeedbackController.Instance?.StopBrushDrawing();
            }
        }

#if UNITY_EDITOR
        /// Unity Recorder 촬영에서 실제 포인터 장치를 흉내 내지 않고도 본 게임의
        /// 먹 비용·스무딩·붓소리·발판 생성 경로를 그대로 재생하는 전용 진입점이다.
        /// 플레이어 빌드에는 포함되지 않는다.
        public bool BeginRecordingStroke(Vector2 worldPosition)
        {
            if (GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking)
                return false;

            CancelActiveStroke();
            BeginStrokeAtWorld(worldPosition);
            recordingStrokeActive = true;
            return true;
        }

        public void AppendRecordingStroke(Vector2 worldPosition)
        {
            if (drawing)
                AppendWorldSample(worldPosition);
        }

        public void EndRecordingStroke()
        {
            if (drawing)
                EndStroke();
            recordingStrokeActive = false;
        }

        public bool IsRecordingStrokeActive => recordingStrokeActive;

        /// 촬영용 획이 진행 중일 때 실제 포인터의 미입력을 해제 신호로 해석하지 않는다.
        public static bool ShouldProcessLivePointer(bool recorderOwnsStroke) =>
            !recorderOwnsStroke;
#endif

        void TryBindGrowthController()
        {
            var next = RunGrowthController.Instance;
            if (growthController == next && growthControllerBound)
                return;

            UnbindGrowthController();
            growthController = next;
            if (growthController == null)
                return;

            growthController.RunReset += HandleGrowthRunReset;
            growthControllerBound = true;
            if (appliedInkCapacity <= 0f)
                appliedInkCapacity = EffectiveInkCapacity;
        }

        void RecoverRuntimeReferences()
        {
            if (cam == null)
                cam = Camera.main;
            if (screenSideWalls == null && cam != null)
                screenSideWalls = cam.GetComponent<Player.ScreenSideWalls>();
        }

        void UnbindGrowthController()
        {
            if (growthController != null && growthControllerBound)
                growthController.RunReset -= HandleGrowthRunReset;
            growthControllerBound = false;
            growthController = null;
        }

        void HandlePermanentGrowthChanged()
        {
            RefreshInkBudget(true);
        }

        void RefreshInkBudget(bool force = false)
        {
            float nextCapacity = EffectiveInkCapacity;
            bool unlimited = HasUnlimitedInk;
            bool unlimitedEnded = unlimitedInkWasActive && !unlimited;
            if (!unlimited &&
                (force || unlimitedEnded ||
                 !Mathf.Approximately(appliedInkCapacity, nextCapacity)))
                PlatformCollider.ReconcileActiveInkBudget(nextCapacity);
            appliedInkCapacity = nextCapacity;
            unlimitedInkWasActive = unlimited;
        }

        void AdvanceUnlimitedInk(float deltaTime)
        {
            if (unlimitedInkRemaining <= 0f)
                return;
            unlimitedInkRemaining = Mathf.Max(
                0f,
                unlimitedInkRemaining - Mathf.Max(0f, deltaTime));
        }

        void HandleGrowthRunReset()
        {
            CancelActiveStroke();
            unlimitedInkRemaining = 0f;
            unlimitedInkDuration = 0f;
            appliedInkCapacity = EffectiveInkCapacity;
            unlimitedInkWasActive = false;
            PlatformCollider.ReconcileActiveInkBudget(appliedInkCapacity);
        }

        /// 화면 먹벽 띠만 제외한다. 캐릭터의 현재 위치·그림 크기는 획을 자르지 않는다.
        List<Vector2> LongestSafeSegment(List<Vector2> strokePoints)
        {
            if (screenSideWalls == null && cam != null)
                screenSideWalls = cam.GetComponent<Player.ScreenSideWalls>();

            float minimumX = float.NegativeInfinity;
            float maximumX = float.PositiveInfinity;
            screenSideWalls?.TryGetDrawableWorldXRange(out minimumX, out maximumX);

            return SelectLongestPlayableSegment(
                strokePoints,
                minimumX,
                maximumX,
                safeSegment,
                safeSegmentCandidate);
        }

        static List<Vector2> SelectLongestPlayableSegment(
            IReadOnlyList<Vector2> strokePoints,
            float minimumX,
            float maximumX,
            List<Vector2> longest,
            List<Vector2> current)
        {
            longest.Clear();
            current.Clear();
            float longestLength = 0f;
            float currentLength = 0f;

            for (int pointIndex = 0; pointIndex < strokePoints.Count; pointIndex++)
            {
                Vector2 point = strokePoints[pointIndex];
                bool blocked = point.x < minimumX || point.x > maximumX;

                if (!blocked)
                {
                    if (current.Count > 0)
                        currentLength += Vector2.Distance(current[^1], point);
                    current.Add(point);
                    continue;
                }

                KeepLongerSegment(current, currentLength, longest, ref longestLength);
                current.Clear();
                currentLength = 0f;
            }

            KeepLongerSegment(current, currentLength, longest, ref longestLength);
            return longest;
        }

        static void KeepLongerSegment(
            List<Vector2> candidate,
            float candidateLength,
            List<Vector2> longest,
            ref float longestLength)
        {
            if (candidateLength <= longestLength) return;
            longest.Clear();
            longest.AddRange(candidate);
            longestLength = candidateLength;
        }

        // ---- 그리는 동안 옅은 먹선 미리보기 ----

        void CreatePreview()
        {
            // 매 획마다 생성/파괴하면 드로잉 빈도만큼 GC가 발생하므로 미리보기 하나를
            // 최초 사용 시 만들고 이후에는 활성 상태만 전환한다.
            if (preview == null)
            {
                var go = new GameObject("StrokePreview");
                preview = go.AddComponent<LineRenderer>();
                preview.useWorldSpace = true;
                preview.startWidth = preview.endWidth = previewWidth;
                preview.sharedMaterial = AI.FallbackInkStyle.SharedInkMaterial;
                var faint = InkPalette.Ink;
                faint.a = 0.35f;
                preview.startColor = preview.endColor = faint;
                preview.numCapVertices = 4;
                preview.sortingOrder = 10;
            }
            else
                preview.gameObject.SetActive(true);

            UpdatePreview();
        }

        void UpdatePreview()
        {
            if (preview == null) return;

            if (points.Count == 1)
            {
                // 손이 닿은 즉시 붓점이 찍히는 느낌: 점 하나로는 선이 그려지지 않으므로
                // 같은 위치를 두 번 찍어 둥근 캡만 있는 점으로 보이게 한다
                preview.positionCount = 2;
                preview.SetPosition(0, points[0]);
                preview.SetPosition(1, points[0]);
                return;
            }

            preview.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                preview.SetPosition(i, points[i]);
        }

        void DestroyPreview()
        {
            if (preview == null) return;
            preview.positionCount = 0;
            preview.gameObject.SetActive(false);
        }

        void OnValidate()
        {
            UpgradeInkCapacityTuning();
            minPointDistance = Mathf.Max(0.001f, minPointDistance);
            maxContinuousStrokeLength = Mathf.Max(minPointDistance, maxContinuousStrokeLength);
            minStrokeLength = Mathf.Max(minPointDistance, minStrokeLength);
            previewWidth = Mathf.Max(0.01f, previewWidth);
            inkCapacity = Mathf.Max(0.001f, inkCapacity);
            evictionFadeDuration = Mathf.Max(0.15f, evictionFadeDuration);
            naturalHoldDuration = Mathf.Max(0.1f, naturalHoldDuration);
        }

        void UpgradeInkCapacityTuning()
        {
            if (inkCapacityTuningVersion >= CurrentInkCapacityTuningVersion)
                return;

            int sourceVersion = inkCapacityTuningVersion;
            bool promoteV3Baseline =
                sourceVersion == 3 &&
                Mathf.Approximately(
                    inkCapacity,
                    LegacyInkCapacityV4Baseline);

            // v3 이전의 씬 기본값만 먼저 4.8m로 정규화한다. 별도로 조정한 값은
            // 어느 세대에서도 덮지 않아야 하므로 버전 단계를 건너뛰지 않는다.
            if (inkCapacityTuningVersion < 3)
            {
                bool knownLegacyDefault =
                    Mathf.Approximately(inkCapacity, LegacyInkCapacityV0) ||
                    Mathf.Approximately(inkCapacity, LegacyInkCapacityV1) ||
                    Mathf.Approximately(inkCapacity, LegacyInkCapacityV2) ||
                    Mathf.Approximately(inkCapacity, LegacyInkCapacityV3);
                if (knownLegacyDefault)
                {
                    inkCapacity = LegacyInkCapacityV4Baseline;
                    promoteV3Baseline = true;
                }
                inkCapacityTuningVersion = 3;
            }

            // v3에서 실제 기본값이던 4.8m만 5.2m로 올린다. v3의 사용자 지정값과
            // 이미 v4로 저장된 4.8m는 의도된 값일 수 있으므로 그대로 보존한다.
            if (inkCapacityTuningVersion == 3)
            {
                if (promoteV3Baseline)
                    inkCapacity = DefaultInkCapacity;
                inkCapacityTuningVersion = 4;
            }
            inkCapacityTuningVersion = CurrentInkCapacityTuningVersion;
        }
    }
}
