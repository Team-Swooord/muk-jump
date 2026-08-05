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
        public const float DefaultInkCapacity = 24f;
        public const float InkReserveItemRatio = 0.25f;
        const float LegacyInkCapacityV0 = 12f;
        const float LegacyInkCapacityV1 = 18f;
        public const int CurrentInkCapacityTuningVersion = 1;

        [Tooltip("이 간격(월드 단위) 이상 움직였을 때만 점 추가")]
        [SerializeField] float minPointDistance = 0.15f;
        [Tooltip("한 획의 최대 길이. 넘치면 그 지점에서 획을 끊고 이어 그린다")]
        [SerializeField] float maxContinuousStrokeLength = 30f;
        [Tooltip("이보다 짧은 획은 발판으로 만들지 않는다")]
        [SerializeField] float minStrokeLength = 0.6f;
        [SerializeField] float previewWidth = 0.4f;
        [Tooltip("LineSprite 프리팹의 600px 붓획 텍스처")]
        [SerializeField] Texture2D lineSpriteTexture;
        [Tooltip("캐릭터에서 이 거리 안의 획 부분만 잘라낸다 (물리 밀어내기 악용 방지)")]
        [SerializeField] float playerClearance = 0.55f;

        [Header("먹자리 — 화면에 유지되는 총 먹선 길이")]
        [Tooltip("동시에 유지할 수 있는 먹선의 기본 월드 길이")]
        [SerializeField] float inkCapacity = DefaultInkCapacity;
        [Tooltip("총 먹자리를 넘긴 오래된 획이 시작점부터 지워지는 시간")]
        [SerializeField] float evictionFadeDuration = 1.1f;
        // 기존 Main 씬에는 이 필드가 없으므로 0을 유지해야 12m 직렬화 값을
        // 재생 시 24m로 올릴 수 있다. 새 씬은 빌더가 현재 버전을 명시한다.
        [SerializeField, HideInInspector] int inkCapacityTuningVersion;

        readonly List<Vector2> points = new();
        Camera cam;
        bool drawing;
        float strokeLength;
        float inkCapacityBonusRatio;
        LineRenderer preview;
        float unlimitedInkUntil;
        RunGrowthController growthController;
        Player.ScreenSideWalls screenSideWalls;
        float appliedInkCapacity;
        bool unlimitedInkWasActive;
        readonly List<Player.PlayerController> livingPlayers = new();
        readonly List<Vector2> safeSegment = new();
        readonly List<Vector2> safeSegmentCandidate = new();

        /// 튜토리얼·분석 계층이 포인터 해제나 오브젝트 수를 추측하지 않고
        /// 실제 유효 발판 생성만 관찰하는 계약이다.
        public event Action<PlatformCollider, float, float> ValidStrokeCreated;

        /// HUD 먹 게이지용. 확정 획과 현재 그리고 있는 획을 모두 포함한다.
        public bool HasUnlimitedInk => Time.time < unlimitedInkUntil;
        public float PendingStrokeBudgetCost => drawing
            ? StrokeBudgetCost(
                strokeLength,
                ActivePermanentGrowth.InkBudgetCostMultiplier,
                ActivePermanentGrowth.ShortStrokeBudgetCostMultiplier)
            : 0f;
        public float CurrentInkUsage =>
            Mathf.Max(0f, PlatformCollider.ActiveInkCost + PendingStrokeBudgetCost);
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
        /// HUD 트랙의 실제 최대 길이. 영구 성장·날씨·붓 여유를 모두 반영한다.
        public float InkCapacityRatio =>
            EffectiveInkCapacity / Mathf.Max(0.001f, inkCapacity);
        public float InkCapacityBonusRatio => Mathf.Max(0f, inkCapacityBonusRatio);
        public float BaseEffectiveInkCapacity =>
            inkCapacity *
            ActivePermanentGrowth.InkCapacityMultiplier *
            Mathf.Clamp(PlatformCollider.RuntimeInkCapacityMultiplier, 0.35f, 1f);
        public float EffectiveInkCapacity =>
            BaseEffectiveInkCapacity * (1f + Mathf.Max(0f, inkCapacityBonusRatio));
        public float EffectiveEvictionFadeDuration =>
            evictionFadeDuration + ActivePermanentGrowth.InkEvictionFadeBonusSeconds;

        PermanentGrowthRunSnapshot ActivePermanentGrowth =>
            RunGrowthController.Instance != null
                ? RunGrowthController.Instance.PermanentSnapshot
                : PermanentGrowthProfile.CreateRunSnapshot();

        public void AddInkReserve(float capacityRatio)
        {
            inkCapacityBonusRatio += Mathf.Max(0f, capacityRatio);
            RefreshInkBudget(true);
        }

        void Awake()
        {
            UpgradeInkCapacityTuning();
        }

        public void ActivateUnlimitedInk(float duration)
        {
            unlimitedInkUntil = Mathf.Max(
                unlimitedInkUntil,
                Time.time + Mathf.Max(0f, duration));
            unlimitedInkWasActive = HasUnlimitedInk;
        }

        void OnEnable()
        {
            // Play 중 스크립트 재컴파일 뒤에도 열린 씬의 구형 12m 값이 즉시 갱신된다.
            UpgradeInkCapacityTuning();
            PermanentGrowthProfile.Changed += HandlePermanentGrowthChanged;
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

            var rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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

            var images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
            var rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (rawImages[i].name.Equals("LineSprite", System.StringComparison.OrdinalIgnoreCase))
                    rawImages[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
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

            RefreshInkBudget();

            if (PointerInput.TryGetPressed(out var screenPos))
            {
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
        {
            while (drawing && points.Count > 0)
            {
                Vector2 segmentStart = points[^1];
                float requestedStep = Vector2.Distance(segmentStart, requestedWorld);
                if (requestedStep < minPointDistance)
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

            // 캐릭터와 너무 가까운 부분 및 화면 먹벽 띠만 잘라내 콜라이더 밀어내기와
            // 측벽 사이 상승 래칫을 막되, 나머지 유효한 획은 발판으로 살린다.
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
                EffectiveEvictionFadeDuration,
                ActivePermanentGrowth.InkEvictionDelaySeconds);
            if (!HasUnlimitedInk)
                PlatformCollider.ReconcileActiveInkBudget(EffectiveInkCapacity);
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
            GameFeedbackController.Instance?.StopBrushDrawing();
            DestroyPreview();
        }

        public void CancelActiveStroke()
        {
            if (drawing)
                CancelStroke();
            else
                GameFeedbackController.Instance?.StopBrushDrawing();
        }

        void TryBindGrowthController()
        {
            var next = RunGrowthController.Instance;
            if (growthController == next) return;

            UnbindGrowthController();
            growthController = next;
            if (growthController == null) return;

            growthController.RunReset += HandleGrowthRunReset;
            if (appliedInkCapacity <= 0f)
                appliedInkCapacity = EffectiveInkCapacity;
        }

        void UnbindGrowthController()
        {
            if (growthController != null)
                growthController.RunReset -= HandleGrowthRunReset;
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

        void HandleGrowthRunReset()
        {
            CancelActiveStroke();
            inkCapacityBonusRatio = 0f;
            unlimitedInkUntil = 0f;
            appliedInkCapacity = EffectiveInkCapacity;
            unlimitedInkWasActive = false;
            PlatformCollider.ReconcileActiveInkBudget(appliedInkCapacity);
        }

        /// 캐릭터·화면 먹벽과 겹치는 부분만 잘라내고 가장 긴 안전 구간은 살린다.
        /// 획 전체를 취소해 입력이 먹히지 않은 것처럼 보이던 불편을 줄인다.
        List<Vector2> LongestSafeSegment(List<Vector2> strokePoints)
        {
            livingPlayers.Clear();
            GameManager.Instance?.GetLivingPlayersNonAlloc(livingPlayers);
            if (screenSideWalls == null && cam != null)
                screenSideWalls = cam.GetComponent<Player.ScreenSideWalls>();

            float minimumX = float.NegativeInfinity;
            float maximumX = float.PositiveInfinity;
            screenSideWalls?.TryGetDrawableWorldXRange(out minimumX, out maximumX);

            return SelectLongestPlayableSegment(
                strokePoints,
                livingPlayers,
                playerClearance,
                minimumX,
                maximumX,
                safeSegment,
                safeSegmentCandidate);
        }

        /// 실제 캐릭터 콜라이더 바깥에서 발판 Edge 반경 0.06m까지 포함한 여백이다.
        /// 먹떼가 커져도 물리 겹침이 생기지 않는 0.08m 아래로는 줄이지 않는다.
        static float ResolvePlayerSurfacePadding(int livingCount)
        {
            return Mathf.Lerp(
                0.15f,
                0.08f,
                Mathf.InverseLerp(1f, GameManager.MaxLivingPlayers, livingCount));
        }

        static List<Vector2> SelectLongestSafeSegment(
            IReadOnlyList<Vector2> strokePoints,
            IReadOnlyList<Player.PlayerController> players,
            float clearance,
            List<Vector2> longest,
            List<Vector2> current)
        {
            return SelectLongestPlayableSegment(
                strokePoints,
                players,
                clearance,
                float.NegativeInfinity,
                float.PositiveInfinity,
                longest,
                current);
        }

        static List<Vector2> SelectLongestPlayableSegment(
            IReadOnlyList<Vector2> strokePoints,
            IReadOnlyList<Player.PlayerController> players,
            float clearance,
            float minimumX,
            float maximumX,
            List<Vector2> longest,
            List<Vector2> current)
        {
            longest.Clear();
            current.Clear();
            float longestLength = 0f;
            float currentLength = 0f;
            float clearanceSquared = Mathf.Max(0f, clearance);
            clearanceSquared *= clearanceSquared;
            float surfacePadding = ResolvePlayerSurfacePadding(players.Count);
            float surfacePaddingSquared = surfacePadding * surfacePadding;

            for (int pointIndex = 0; pointIndex < strokePoints.Count; pointIndex++)
            {
                Vector2 point = strokePoints[pointIndex];
                bool blocked = point.x < minimumX || point.x > maximumX;
                for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
                {
                    if (blocked) break;
                    var player = players[playerIndex];
                    if (player == null || player.IsDead) continue;
                    var bodyShape = player.PrimaryCollider;
                    bool overlapsPlayer;
                    if (bodyShape != null && bodyShape.enabled)
                    {
                        Vector2 closest = bodyShape.ClosestPoint(point);
                        overlapsPlayer =
                            (point - closest).sqrMagnitude < surfacePaddingSquared;
                    }
                    else
                    {
                        Vector2 playerPosition = player.transform.position;
                        overlapsPlayer =
                            (point - playerPosition).sqrMagnitude < clearanceSquared;
                    }
                    if (!overlapsPlayer)
                    {
                        var visual = player.GetComponent<SpriteRenderer>();
                        if (visual != null && visual.sprite != null)
                            overlapsPlayer = ContainsExpandedVisualBounds(
                                visual.bounds,
                                point,
                                surfacePadding);
                    }
                    if (overlapsPlayer)
                    {
                        blocked = true;
                        break;
                    }
                }

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

        static bool ContainsExpandedVisualBounds(
            Bounds visualBounds,
            Vector2 point,
            float padding)
        {
            padding = Mathf.Max(0f, padding);
            return point.x >= visualBounds.min.x - padding &&
                   point.x <= visualBounds.max.x + padding &&
                   point.y >= visualBounds.min.y - padding &&
                   point.y <= visualBounds.max.y + padding;
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
            playerClearance = Mathf.Max(0f, playerClearance);
            inkCapacity = Mathf.Max(0.001f, inkCapacity);
            evictionFadeDuration = Mathf.Max(0.15f, evictionFadeDuration);
        }

        void UpgradeInkCapacityTuning()
        {
            if (inkCapacityTuningVersion >= CurrentInkCapacityTuningVersion)
                return;

            // 현재 Main의 12m와 이전 설계 중간값 18m를 모두 씬 재생성 전부터
            // 새 밸런스로 올린다. 사용자가 별도로 조정한 다른 값은 보존한다.
            if (Mathf.Approximately(inkCapacity, LegacyInkCapacityV0) ||
                Mathf.Approximately(inkCapacity, LegacyInkCapacityV1))
                inkCapacity = DefaultInkCapacity;
            inkCapacityTuningVersion = CurrentInkCapacityTuningVersion;
        }
    }
}
