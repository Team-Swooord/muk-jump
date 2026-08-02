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

        [Header("먹(잉크) 자원 — 무한 드로잉 방지")]
        [Tooltip("먹 총량 (월드 단위 길이). 그은 만큼 소모된다")]
        [SerializeField] float inkCapacity = 12f;
        [Tooltip("초당 먹 회복량 (그리는 중에는 회복하지 않음)")]
        [SerializeField] float inkRegenPerSecond = 3f;
        [Tooltip("먹이 이보다 적으면 새 획을 시작할 수 없다")]
        [SerializeField] float minInkToStart = 0.8f;

        readonly List<Vector2> points = new();
        Camera cam;
        bool drawing;
        float strokeLength;
        float ink;
        float inkReserve;
        LineRenderer preview;
        float unlimitedInkUntil;
        RunGrowthController growthController;
        float appliedInkCapacity;
        float strokeBaseInkSpent;
        float strokeReserveInkSpent;
        float lastValidStrokeAt = float.NegativeInfinity;
        bool idleStrokeDiscountEligible;
        bool lowInkRecoveryActive;
        readonly List<Player.PlayerController> livingPlayers = new();
        readonly List<Vector2> safeSegment = new();
        readonly List<Vector2> safeSegmentCandidate = new();

        /// HUD 먹 게이지용. 1을 넘는 값은 아이템으로 쌓은 일회성 여유분이다.
        public bool HasUnlimitedInk => Time.time < unlimitedInkUntil;
        public float InkRemaining01 => HasUnlimitedInk
            ? 1f
            : (ink + inkReserve) / Mathf.Max(0.001f, EffectiveInkCapacity);
        public float EffectiveInkCapacity =>
            inkCapacity *
            ActivePermanentGrowth.InkCapacityMultiplier *
            (RunGrowthController.Instance != null
                ? RunGrowthController.Instance.InkCapacityMultiplier
                : 1f);
        public float EffectiveInkRegenPerSecond =>
            inkRegenPerSecond *
            ActivePermanentGrowth.InkRecoveryMultiplier *
            (RunGrowthController.Instance != null
                ? RunGrowthController.Instance.InkRecoveryMultiplier
                : 1f) *
            (lowInkRecoveryActive ? 1.30f : 1f);

        PermanentGrowthRunSnapshot ActivePermanentGrowth =>
            RunGrowthController.Instance != null
                ? RunGrowthController.Instance.PermanentSnapshot
                : PermanentGrowthProfile.CreateRunSnapshot();

        public void AddInkReserve(float capacityRatio)
        {
            inkReserve += EffectiveInkCapacity * Mathf.Max(0f, capacityRatio);
        }

        public void ActivateUnlimitedInk(float duration)
        {
            unlimitedInkUntil = Mathf.Max(unlimitedInkUntil, Time.time + duration);
        }

        void OnEnable()
        {
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
            TryBindGrowthController();
            appliedInkCapacity = EffectiveInkCapacity;
            ink = appliedInkCapacity;
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
                // 로비는 명시적인 시작·성장·도감 버튼만 입력받는다.
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

            if (!drawing)
            {
                UpdateLowInkRecoveryState();
                ink = Mathf.Min(
                    EffectiveInkCapacity,
                    ink + EffectiveInkRegenPerSecond * Time.deltaTime);
            }

            if (PointerInput.TryGetPressed(out var screenPos))
            {
                if (GameplayHudView.IsPointerOverItemTestControls(screenPos) ||
                    PauseMenuView.IsPointerOverControls(screenPos))
                {
                    if (drawing) CancelStroke();
                    return;
                }

                if (drawing)
                    ContinueStroke(screenPos);
                else if (HasUnlimitedInk || ink + inkReserve >= minInkToStart)
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
            strokeBaseInkSpent = 0f;
            strokeReserveInkSpent = 0f;
            idleStrokeDiscountEligible =
                Time.time - lastValidStrokeAt >= 2f;
            points.Clear();
            points.Add(worldPos);
            GameFeedbackController.Instance?.StartBrushDrawing();
            CreatePreview();
        }

        void ContinueStroke(Vector2 screenPos)
        {
            Vector2 world = ToWorld(screenPos);
            float step = Vector2.Distance(points[^1], world);
            if (step < minPointDistance) return;

            // 먹이 다 떨어지면 그 지점에서 획이 끝난다 — 회복될 때까지 더 그릴 수 없다
            if (!HasUnlimitedInk && ink + inkReserve <= 0f)
            {
                EndStroke();
                return;
            }

            // 한 획의 최대 길이 초과 시 그 지점에서 끊고, 손을 떼지 않았다면 바로 그
            // 지점에서 새 획을 이어 시작한다 (끊지 않으면 다음 프레임의 이동분만큼 틈이
            // 생겨 일직선으로 길게 그은 발판 중간이 붕 뜨는 문제가 있었음)
            if (strokeLength + step > maxContinuousStrokeLength)
            {
                Vector2 seam = points[^1];
                EndStroke();
                BeginStrokeAtWorld(seam);
                return;
            }

            bool exhaustsInk = false;
            if (!HasUnlimitedInk)
            {
                float availableInk = Mathf.Max(0f, ink) + Mathf.Max(0f, inkReserve);
                float affordableStep = LimitStepToAvailableInk(step, ink, inkReserve);
                exhaustsInk = availableInk <= step;
                if (affordableStep <= 0f)
                {
                    EndStroke();
                    return;
                }

                // 포인터가 한 프레임에 멀리 이동해도 남은 먹으로 지불할 수 있는
                // 지점까지만 선과 콜라이더를 만든다.
                world = Vector2.MoveTowards(points[^1], world, affordableStep);
                step = affordableStep;
            }

            strokeLength += step;
            if (!HasUnlimitedInk)
                ConsumeInk(step);
            points.Add(world);
            GameFeedbackController.Instance?.PlayBrushMovement(step);
            UpdatePreview();

            if (exhaustsInk)
                EndStroke();
        }

        static float LimitStepToAvailableInk(float requestedStep, float currentInk,
            float reserve)
        {
            return Mathf.Min(
                Mathf.Max(0f, requestedStep),
                Mathf.Max(0f, currentInk) + Mathf.Max(0f, reserve));
        }

        void ConsumeInk(float amount)
        {
            float reserveUse = Mathf.Min(inkReserve, amount);
            inkReserve -= reserveUse;
            float baseUse = amount - reserveUse;
            ink = Mathf.Max(0f, ink - baseUse);
            strokeReserveInkSpent += reserveUse;
            strokeBaseInkSpent += baseUse;
        }

        void EndStroke()
        {
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

            // 캐릭터와 너무 가까운 부분만 잘라내 콜라이더 밀어내기로 캐릭터를
            // 튕겨 올리는 악용은 막되, 나머지 유효한 획은 발판으로 살린다.
            smoothed = LongestSafeSegment(smoothed);
            if (smoothed.Count < 2 ||
                BezierSmoother.PolylineLength(smoothed) < minStrokeLength)
            {
                GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, false);
                return;
            }

            float validLength = BezierSmoother.PolylineLength(smoothed);
            float consumedInk = ApplyPermanentStrokeDiscount(validLength);
            PlatformCollider.Spawn(smoothed, consumedInk);
            lastValidStrokeAt = Time.time;
            GameFeedbackController.Instance?.PlayStrokeResolved(feedbackPosition, true);
        }

        float ApplyPermanentStrokeDiscount(float validLength)
        {
            float multiplier = 1f;
            if (ActivePermanentGrowth.HasShortStrokeDiscount && validLength <= 1.5f)
                multiplier *= 0.92f;
            if (ActivePermanentGrowth.HasIdleStrokeDiscount && idleStrokeDiscountEligible)
                multiplier *= 0.90f;

            float reserveRefund = strokeReserveInkSpent * (1f - multiplier);
            float baseRefund = strokeBaseInkSpent * (1f - multiplier);
            inkReserve += reserveRefund;
            ink = Mathf.Min(EffectiveInkCapacity, ink + baseRefund);
            return (strokeReserveInkSpent + strokeBaseInkSpent) * multiplier;
        }

        void UpdateLowInkRecoveryState()
        {
            if (!ActivePermanentGrowth.HasLowInkRecovery)
            {
                lowInkRecoveryActive = false;
                return;
            }

            float usableInk = Mathf.Max(0f, ink) + Mathf.Max(0f, inkReserve);
            float ratio = usableInk / Mathf.Max(0.001f, EffectiveInkCapacity);
            if (!lowInkRecoveryActive && ratio < 0.25f)
                lowInkRecoveryActive = true;
            else if (lowInkRecoveryActive && ratio >= 0.40f)
                lowInkRecoveryActive = false;
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

            growthController.UpgradeSelected += HandleGrowthUpgradeSelected;
            growthController.RunReset += HandleGrowthRunReset;
            growthController.InkRestoreRequested += HandleInkRestoreRequested;
            growthController.InkRestoreRatioRequested += HandleInkRestoreRatioRequested;
            if (appliedInkCapacity <= 0f)
                appliedInkCapacity = EffectiveInkCapacity;
        }

        void UnbindGrowthController()
        {
            if (growthController != null)
            {
                growthController.UpgradeSelected -= HandleGrowthUpgradeSelected;
                growthController.RunReset -= HandleGrowthRunReset;
                growthController.InkRestoreRequested -= HandleInkRestoreRequested;
                growthController.InkRestoreRatioRequested -= HandleInkRestoreRatioRequested;
            }
            growthController = null;
        }

        void HandleGrowthUpgradeSelected(GrowthUpgradeType upgrade)
        {
            if (upgrade != GrowthUpgradeType.InkCapacity) return;

            ApplyCapacityIncrease();
        }

        void HandlePermanentGrowthChanged()
        {
            ApplyCapacityIncrease();
        }

        void ApplyCapacityIncrease()
        {
            float nextCapacity = EffectiveInkCapacity;
            if (appliedInkCapacity <= 0f)
            {
                appliedInkCapacity = nextCapacity;
                return;
            }
            float addedCapacity = Mathf.Max(0f, nextCapacity - appliedInkCapacity);
            ink = Mathf.Min(nextCapacity, ink + addedCapacity);
            appliedInkCapacity = nextCapacity;
        }

        void HandleGrowthRunReset()
        {
            CancelActiveStroke();
            inkReserve = 0f;
            unlimitedInkUntil = 0f;
            strokeBaseInkSpent = 0f;
            strokeReserveInkSpent = 0f;
            lastValidStrokeAt = float.NegativeInfinity;
            idleStrokeDiscountEligible = false;
            lowInkRecoveryActive = false;
            appliedInkCapacity = EffectiveInkCapacity;
            ink = appliedInkCapacity;
        }

        void HandleInkRestoreRequested(float amount)
        {
            if (amount <= 0f)
                return;
            ink = Mathf.Min(EffectiveInkCapacity, ink + amount);
        }

        void HandleInkRestoreRatioRequested(float ratio)
        {
            HandleInkRestoreRequested(
                EffectiveInkCapacity * Mathf.Max(0f, ratio));
        }

        /// 캐릭터와 겹치는 부분만 잘라내고 가장 긴 안전 구간은 살린다.
        /// 획 전체를 취소해 입력이 먹히지 않은 것처럼 보이던 불편을 줄인다.
        List<Vector2> LongestSafeSegment(List<Vector2> strokePoints)
        {
            livingPlayers.Clear();
            GameManager.Instance?.GetLivingPlayersNonAlloc(livingPlayers);
            if (livingPlayers.Count == 0) return strokePoints;

            return SelectLongestSafeSegment(strokePoints, livingPlayers, playerClearance,
                safeSegment, safeSegmentCandidate);
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
                bool blocked = false;
                for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
                {
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
            minPointDistance = Mathf.Max(0.001f, minPointDistance);
            maxContinuousStrokeLength = Mathf.Max(minPointDistance, maxContinuousStrokeLength);
            minStrokeLength = Mathf.Max(minPointDistance, minStrokeLength);
            previewWidth = Mathf.Max(0.01f, previewWidth);
            playerClearance = Mathf.Max(0f, playerClearance);
            inkCapacity = Mathf.Max(0.001f, inkCapacity);
            inkRegenPerSecond = Mathf.Max(0f, inkRegenPerSecond);
            minInkToStart = Mathf.Clamp(minInkToStart, 0f, inkCapacity);
        }
    }
}
