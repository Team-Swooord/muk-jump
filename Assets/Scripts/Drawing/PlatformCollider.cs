using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// 스트로크 점열 하나 = 발판 하나. LineRenderer(붓선 비주얼) + EdgeCollider2D(물리).
    /// 플레이어가 그린 먹선은 일정 시간 뒤 마르고, 최대 먹 용량을 넘겨도
    /// 가장 오래된 획의 시작점부터 시각·물리가 함께 지워진다.
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public class PlatformCollider : MonoBehaviour
    {
        enum RemovalCause
        {
            None,
            NaturalExpiry,
            BudgetEviction,
            Hazard,
        }

        // 0.6m 최소 획만 반복하는 비정상 입력에서도 Collider가 무한히 늘지 않게 하는
        // 모바일 안전 상한이다. 정상 플레이의 실제 제한은 개수가 아니라 총 먹 길이다.
        const int MaxRuntimeDrawnPlatforms = 96;
        public const float DefaultNaturalHoldDuration = 3.4f;
        static readonly List<PlatformCollider> active = new();
        static readonly List<PlatformCollider> runtimeDrawn = new();
        public static float RuntimeInkCapacityMultiplier { get; set; } = 1f;

        [Tooltip("생성 후 유지 시간(초). 0 이하면 영구 발판")]
        [SerializeField] float lifetime = 4.5f;
        [SerializeField] float fadeDuration = 0.8f;
        [SerializeField] bool windCurrentPlatform;
        [SerializeField] bool growthSafetyPlatform;

        public float Length { get; private set; }
        public LineRenderer Line { get; private set; }
        public bool IsWindCurrentPlatform => windCurrentPlatform;
        public bool IsGrowthSafetyPlatform => growthSafetyPlatform;
        public bool IsOneWayPlatform =>
            windCurrentPlatform || growthSafetyPlatform;
        /// 런타임에서 플레이어가 그린 유한 수명 먹선만 해태 돌진과 상호작용한다.
        /// 시작 지형과 풍맥처럼 영구 배치된 발판은 수문장을 자동으로 제거하지 않는다.
        public bool IsTemporaryDrawnPlatform =>
            (runtimeDrawnPlatform || lifetime > 0f) &&
            !windCurrentPlatform && !growthSafetyPlatform && !removalRequested;
        public float RetainedInkCost => retainedInkCost;
        /// HUD가 실제 화면에 남은 먹 길이를 표시할 때 사용하는 값이다.
        /// 예산 장부는 새 획을 위해 즉시 반환할 수 있지만, 화면의 먹선은
        /// evictionFadeDuration 동안 마르므로 시각 진행도만큼 천천히 줄인다.
        public float VisibleInkCost =>
            runtimeDrawnPlatform && !removalRequested
                ? Mathf.Max(
                    0f,
                    initialInkCost * (1f - Mathf.Clamp01(evictionVisualFraction)))
                : 0f;
        public static float ActiveInkCost
        {
            get
            {
                float total = 0f;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    PlatformCollider platform = active[i];
                    if (platform == null)
                    {
                        active.RemoveAt(i);
                        continue;
                    }
                    total += Mathf.Max(0f, platform.retainedInkCost);
                }
                return total;
            }
        }
        /// 현재 화면에 실제로 남아 있는 플레이어 먹선의 총 비용.
        /// HUD 전용이며 FIFO 예산 판정에는 ActiveInkCost를 계속 사용한다.
        public static float ActiveVisibleInkCost
        {
            get
            {
                float total = 0f;
                for (int i = runtimeDrawn.Count - 1; i >= 0; i--)
                {
                    PlatformCollider platform = runtimeDrawn[i];
                    if (platform == null)
                    {
                        runtimeDrawn.RemoveAt(i);
                        continue;
                    }
                    total += platform.VisibleInkCost;
                }
                return total;
            }
        }
        public static int ActiveDrawnPlatformCount
        {
            get
            {
                for (int i = runtimeDrawn.Count - 1; i >= 0; i--)
                    if (runtimeDrawn[i] == null)
                        runtimeDrawn.RemoveAt(i);
                return runtimeDrawn.Count;
            }
        }
        EdgeCollider2D edge;
        readonly HashSet<int> windUsers = new();
        readonly Gradient fadeGradient = new();
        readonly Gradient outlineFadeGradient = new();
        readonly GradientColorKey[] fadeColorKeys = new GradientColorKey[2];
        readonly GradientAlphaKey[] fadeAlphaKeys = new GradientAlphaKey[4];
        readonly GradientColorKey[] outlineFadeColorKeys =
            new GradientColorKey[2];
        readonly GradientAlphaKey[] outlineFadeAlphaKeys =
            new GradientAlphaKey[4];
        LineRenderer specialOutline;
        [SerializeField, HideInInspector] Vector2[] originalPoints;
        [SerializeField, HideInInspector] float age;
        [SerializeField, HideInInspector] float lastEffectiveLifetime;
        [SerializeField, HideInInspector] bool removalRequested;
        [SerializeField, HideInInspector] bool runtimeDrawnPlatform;
        [SerializeField, HideInInspector] float initialInkCost;
        [SerializeField, HideInInspector] float retainedInkCost;
        [SerializeField, HideInInspector] float evictionVisualFraction;
        [SerializeField, HideInInspector] float evictionTargetFraction;
        [SerializeField, HideInInspector] float evictionFadeDuration = 1.1f;
        [SerializeField, HideInInspector] float evictionDelay;
        [SerializeField, HideInInspector] float evictionRequestedAt = float.PositiveInfinity;
        [SerializeField, HideInInspector] float naturalHoldDuration = DefaultNaturalHoldDuration;
        [SerializeField, HideInInspector] float naturalAge;
        [SerializeField, HideInInspector] bool naturalExpiryRequested;
        [SerializeField, HideInInspector] RemovalCause removalCause;
        [SerializeField, HideInInspector] int lastColliderCutoff = -1;

        /// 스무딩 완료된 월드 좌표 점열로 발판을 생성한다 (런타임 드로잉 경로)
        public static PlatformCollider Spawn(
            List<Vector2> worldPoints,
            float inkBudgetCost = 0f,
            float evictionFadeSeconds = 1.1f,
            float evictionDelaySeconds = 0f,
            float naturalHoldSeconds = DefaultNaturalHoldDuration)
        {
            var go = new GameObject("InkPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            var platform = go.AddComponent<PlatformCollider>();
            platform.runtimeDrawnPlatform = true;
            platform.lifetime = 0f;
            platform.Build(worldPoints);
            platform.initialInkCost = Mathf.Max(
                0.001f,
                inkBudgetCost > 0f ? inkBudgetCost : platform.Length);
            platform.retainedInkCost = platform.initialInkCost;
            platform.evictionFadeDuration = Mathf.Max(0.15f, evictionFadeSeconds);
            platform.evictionDelay = Mathf.Max(0f, evictionDelaySeconds);
            platform.naturalHoldDuration = Mathf.Max(0.1f, naturalHoldSeconds);

            active.Add(platform);
            runtimeDrawn.Add(platform);
            while (runtimeDrawn.Count > MaxRuntimeDrawnPlatforms)
            {
                var oldest = runtimeDrawn[0];
                if (oldest == null)
                {
                    runtimeDrawn.RemoveAt(0);
                    continue;
                }
                // 총량 확장 아이템을 비정상적으로 반복 획득한 장시간 세션에서도
                // 실제 Collider/Renderer 수만큼은 모바일 안전 상한을 넘지 않는다.
                oldest.ForceImmediateRemoval();
            }

            SketchToInkService.Instance?.Stylize(platform);
            return platform;
        }

        /// 현재 최대 먹 용량을 넘긴 길이만큼 가장 오래된 획부터 FIFO로 비운다.
        /// 비용은 즉시 ledger에서 빠지지만 비주얼과 콜라이더는 설정된 시간 동안
        /// 획의 시작점부터 같이 줄어들어 갑작스럽게 발판 전체가 꺼지지 않는다.
        public static void ReconcileActiveInkBudget(float capacity)
        {
            float overflow = Mathf.Max(0f, ActiveInkCost - Mathf.Max(0.001f, capacity));
            int guard = MaxRuntimeDrawnPlatforms * 2;
            while (overflow > 0.0001f && active.Count > 0 && guard-- > 0)
            {
                PlatformCollider oldest = active[0];
                if (oldest == null)
                {
                    active.RemoveAt(0);
                    continue;
                }

                float released = oldest.RequestBudgetEviction(overflow);
                if (released <= 0.0001f)
                {
                    active.RemoveAt(0);
                    continue;
                }
                overflow -= released;
            }
        }

        /// 아래에서는 통과하고, 위에 착지하면 상승 기류를 받는 영구 풍맥 발판.
        public static PlatformCollider SpawnWindCurrentPlatform(List<Vector2> worldPoints)
        {
            var go = new GameObject("WindCurrentPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            var platform = go.AddComponent<PlatformCollider>();
            platform.lifetime = 0f;
            platform.windCurrentPlatform = true;
            platform.Build(worldPoints);
            platform.ConfigureOneWay();
            SketchToInkService.Instance?.Stylize(platform);
            platform.ApplySpecialVisual(InkPalette.WindPlatform, 0.62f, 0.84f);
            return platform;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState()
        {
            active.Clear();
            runtimeDrawn.Clear();
            RuntimeInkCapacityMultiplier = 1f;
        }

        /// 영구 도약 비기가 만드는 단방향 안전 발판. 드로잉 예산과 먹 환급에서 제외하고
        /// 정확히 6초 뒤 사라져 한 판에 임시 콜라이더가 계속 쌓이지 않게 한다.
        public static PlatformCollider SpawnGrowthSafetyPlatform(
            List<Vector2> worldPoints,
            float lifetimeSeconds = 6f)
        {
            if (worldPoints == null || worldPoints.Count < 2)
                return null;

            var go = new GameObject("GrowthSafetyPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            var platform = go.AddComponent<PlatformCollider>();
            platform.lifetime = Mathf.Max(0.5f, lifetimeSeconds);
            platform.fadeDuration = Mathf.Min(0.8f, platform.lifetime * 0.3f);
            platform.growthSafetyPlatform = true;
            platform.Build(worldPoints);
            platform.ConfigureOneWay();
            SketchToInkService.Instance?.Stylize(platform);
            platform.ApplySpecialVisual(InkPalette.Gold, 0.58f, 0.82f);
            return platform;
        }

        void Awake()
        {
            RecoverRuntimeComponents();
        }

        void OnEnable()
        {
            RecoverRuntimeComponents();
            if (!Application.isPlaying || !runtimeDrawnPlatform || removalRequested)
                return;

            RegisterRuntimeDrawnPlatform(this);
        }

        void OnDisable()
        {
            active.Remove(this);
            runtimeDrawn.Remove(this);
        }

        void RecoverRuntimeComponents()
        {
            Line ??= GetComponent<LineRenderer>();
            edge ??= GetComponent<EdgeCollider2D>();
            fadeColorKeys[0] = new GradientColorKey(InkPalette.Ink, 0f);
            fadeColorKeys[1] = new GradientColorKey(InkPalette.Ink, 1f);
            outlineFadeColorKeys[0] = new GradientColorKey(InkPalette.Ink, 0f);
            outlineFadeColorKeys[1] = new GradientColorKey(InkPalette.Ink, 1f);

            // 이 변경이 처음 적용되는 Play 세션은 이전 어셈블리에서 런타임 필드를
            // 보존하지 못했을 수 있다. 전용 오브젝트 이름으로 기존 먹선을 한 번 복구한다.
            if (Application.isPlaying && !runtimeDrawnPlatform &&
                name == "InkPlatform" && lifetime <= 0f &&
                !windCurrentPlatform && !growthSafetyPlatform)
            {
                runtimeDrawnPlatform = true;
                naturalHoldDuration = DefaultNaturalHoldDuration;
                evictionFadeDuration = Mathf.Max(0.15f, evictionFadeDuration);
                evictionRequestedAt = float.PositiveInfinity;
            }

            if (runtimeDrawnPlatform &&
                (originalPoints == null || originalPoints.Length < 2) &&
                Line != null && Line.positionCount >= 2)
            {
                var positions = new Vector3[Line.positionCount];
                Line.GetPositions(positions);
                originalPoints = new Vector2[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                    originalPoints[i] = positions[i];
            }

            if (runtimeDrawnPlatform && originalPoints != null &&
                originalPoints.Length >= 2)
            {
                if (Length <= 0f)
                    Length = BezierSmoother.PolylineLength(
                        new List<Vector2>(originalPoints));
                if (initialInkCost <= 0.0001f)
                    initialInkCost = Mathf.Max(0.001f, Length);
                if (retainedInkCost <= 0.0001f &&
                    !naturalExpiryRequested && !removalRequested)
                    retainedInkCost = initialInkCost;
            }
        }

        static void RegisterRuntimeDrawnPlatform(PlatformCollider platform)
        {
            if (platform == null || platform.removalRequested)
                return;
            if (!runtimeDrawn.Contains(platform))
                runtimeDrawn.Add(platform);
            if (platform.retainedInkCost > 0.0001f && !active.Contains(platform))
                active.Add(platform);

            // 도메인 리로드 뒤 OnEnable 순서는 생성 순서와 다를 수 있다.
            // 더 오래 화면에 있던 획부터 비워지도록 자연 경과 시간을 기준으로 복구한다.
            runtimeDrawn.Sort(CompareOldestRuntimeStroke);
            active.Sort(CompareOldestRuntimeStroke);
        }

        static int CompareOldestRuntimeStroke(
            PlatformCollider left,
            PlatformCollider right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int ageOrder = right.naturalAge.CompareTo(left.naturalAge);
            return ageOrder != 0
                ? ageOrder
                : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        void Start()
        {
            // 씬에 미리 배치된 발판(시작 지형): 에디터에서 넣은 콜라이더 점으로 비주얼만 구성
            if (Length <= 0f && edge.pointCount >= 2)
                ConfigurePermanentInkLine(edge.points);
        }

        /// 씬에 영구 배치된 먹선의 물리·길이·붓선을 한 번에 맞춘다.
        /// 구버전 Play 백업의 짧은 시작 발판을 복구할 때 일부 상태만 남는 것을 막는다.
        public void ConfigurePermanentInkLine(Vector2[] localPoints)
        {
            if (localPoints == null || localPoints.Length < 2)
                return;

            Line ??= GetComponent<LineRenderer>();
            edge ??= GetComponent<EdgeCollider2D>();
            if (Line == null || edge == null)
                return;

            var points = new List<Vector2>(localPoints);
            edge.points = points.ToArray();
            edge.edgeRadius = 0.06f;
            edge.enabled = true;
            Line.enabled = true;
            lifetime = 0f;
            age = 0f;
            removalRequested = false;
            growthSafetyPlatform = false;
            runtimeDrawnPlatform = false;
            initialInkCost = 0f;
            retainedInkCost = 0f;
            evictionVisualFraction = 0f;
            evictionTargetFraction = 0f;
            evictionRequestedAt = float.PositiveInfinity;
            naturalHoldDuration = 0f;
            naturalAge = 0f;
            naturalExpiryRequested = false;
            removalCause = RemovalCause.None;
            lastColliderCutoff = -1;
            Length = BezierSmoother.PolylineLength(points);
            ApplyVisual(points);

            if (SketchToInkService.Instance != null)
                SketchToInkService.Instance.Stylize(this);
            else
                FallbackInkStyle.Apply(Line, Length);
        }

        void Build(List<Vector2> worldPoints)
        {
            // 원점을 스트로크 중심으로 잡고 로컬 좌표로 변환
            var center = Vector2.zero;
            foreach (var p in worldPoints) center += p;
            center /= worldPoints.Count;
            transform.position = center;

            var local = new List<Vector2>(worldPoints.Count);
            foreach (var p in worldPoints) local.Add(p - center);

            Length = BezierSmoother.PolylineLength(local);

            edge = GetComponent<EdgeCollider2D>();
            edge.points = local.ToArray();
            edge.edgeRadius = 0.06f;
            lastColliderCutoff = -1;

            ApplyVisual(local);
            lastEffectiveLifetime = EffectiveLifetime;
        }

        /// 풍맥·성장 안전 발판을 아래에서 통과하도록 단방향 Effector를 설정한다.
        /// 풀에서 다시 활성화해도 Effector가 중복 추가되지 않도록 기존 컴포넌트를 재사용한다.
        void ConfigureOneWay()
        {
            edge ??= GetComponent<EdgeCollider2D>();
            edge.usedByEffector = true;

            var effector = GetComponent<PlatformEffector2D>();
            if (effector == null)
                effector = gameObject.AddComponent<PlatformEffector2D>();

            effector.enabled = true;
            effector.useOneWay = true;
            effector.surfaceArc = 165f;
            effector.useColliderMask = false;
        }

        /// 같은 캐릭터가 같은 풍맥 발판에서 연속 충돌해 중복 발사되지 않게 한 번만 허용한다.
        public bool TryUseWindCurrent(Component player)
        {
            return windCurrentPlatform && player != null && windUsers.Add(player.GetInstanceID());
        }

        void ApplyVisual(List<Vector2> localPoints)
        {
            Line = GetComponent<LineRenderer>();
            Line.useWorldSpace = false;
            Line.positionCount = localPoints.Count;
            for (int i = 0; i < localPoints.Count; i++)
                Line.SetPosition(i, localPoints[i]);

            originalPoints = localPoints.ToArray();
        }

        /// 특수 발판은 같은 붓결의 검정 외곽선 위에 효과색 획을 겹쳐 물리 종류를 구분한다.
        /// 외곽선에는 콜라이더를 붙이지 않아 실제 단방향 충돌과 풍맥 발동 횟수에 영향을 주지 않는다.
        void ApplySpecialVisual(Color innerColor, float innerWidth, float outlineWidth)
        {
            if (Line == null || Line.positionCount < 2) return;

            // 제작용 LineSprite는 검정 RGB라 일반적인 색 곱셈으로는 금색·청회색이 나오지
            // 않는다. 특수 발판 안쪽만 흰색 알파 붓결 재질로 바꿔 색을 정확히 표시한다.
            Line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
            innerColor.a = 0.96f;
            Line.startColor = Line.endColor = innerColor;
            Line.widthMultiplier = innerWidth;
            fadeColorKeys[0] = new GradientColorKey(innerColor, 0f);
            fadeColorKeys[1] = new GradientColorKey(innerColor, 1f);

            var outlineObject = new GameObject("BrushOutline");
            outlineObject.transform.SetParent(transform, false);
            var outline = outlineObject.AddComponent<LineRenderer>();
            specialOutline = outline;
            outline.useWorldSpace = false;
            outline.loop = Line.loop;
            outline.positionCount = Line.positionCount;
            var positions = new Vector3[Line.positionCount];
            Line.GetPositions(positions);
            outline.SetPositions(positions);
            outline.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
            outline.textureMode = Line.textureMode;
            outline.numCapVertices = Line.numCapVertices;
            outline.numCornerVertices = Line.numCornerVertices;
            outline.widthCurve = new AnimationCurve(Line.widthCurve.keys);
            outline.widthMultiplier = outlineWidth;
            outline.sortingLayerID = Line.sortingLayerID;
            outline.sortingOrder = Line.sortingOrder - 1;
            var ink = InkPalette.Ink;
            ink.a = 0.94f;
            outline.startColor = outline.endColor = ink;
        }

        void Update()
        {
            if (removalRequested) return;
            if (runtimeDrawnPlatform)
            {
                UpdateRuntimeDrawnPlatform(Time.deltaTime, Time.time);
                return;
            }
            if (lifetime <= 0f) return; // 영구 발판

            float effectiveLifetime = EffectiveLifetime;
            SynchronizeLifetimeProgress(effectiveLifetime);

            age += Time.deltaTime;
            float remaining = effectiveLifetime - age;

            if (remaining <= fadeDuration)
            {
                float t = 1f - Mathf.Clamp01(remaining / fadeDuration);
                FadeVisual(t);
                TrimCollider(t);
            }
            else if (lastColliderCutoff >= 0)
            {
                // 수명 증가로 페이드 구간 밖으로 돌아온 경우 시각과 물리를 함께 복원한다.
                FadeVisual(0f);
                lastColliderCutoff = -1;
                if (originalPoints != null && originalPoints.Length >= 2)
                    edge.points = originalPoints;
            }

            if (remaining <= 0f)
            {
                if (removalCause == RemovalCause.None)
                    removalCause = RemovalCause.NaturalExpiry;
                // Destroy는 프레임 끝까지 지연된다. 그 사이 새 획의 예산 퇴출이
                // 자연 소멸 원인을 덮어써 환급을 지우지 않도록 즉시 목록에서 뺀다.
                removalRequested = true;
                active.Remove(this);
                Destroy(gameObject);
            }
        }

        void SynchronizeLifetimeProgress(float effectiveLifetime)
        {
            if (lastEffectiveLifetime > 0f &&
                !Mathf.Approximately(lastEffectiveLifetime, effectiveLifetime))
            {
                // 성장 선택 전후의 수명 진행률을 보존한다. 이미 마르는 중인 발판이
                // 수명만 늘어난 채 반투명 상태로 오래 남는 현상을 막는다.
                float normalizedAge = Mathf.Clamp01(age / lastEffectiveLifetime);
                age = normalizedAge * effectiveLifetime;
            }
            lastEffectiveLifetime = effectiveLifetime;
        }

        /// 풍맥 발판은 유지하고, 낙하 위험물에 맞은 일반 먹 발판만 등록 해제 후 제거한다.
        public bool BreakFromHazard()
        {
            if (windCurrentPlatform || growthSafetyPlatform) return false;
            if (removalRequested) return false;
            if (!TryBeginHazardRemoval()) return false;
            Destroy(gameObject);
            return true;
        }

        bool TryBeginHazardRemoval()
        {
            if (removalRequested) return false;
            removalRequested = true;
            removalCause = RemovalCause.Hazard;
            if (edge != null) edge.enabled = false;
            active.Remove(this);
            runtimeDrawn.Remove(this);
            retainedInkCost = 0f;
            return true;
        }

        void ForceImmediateRemoval()
        {
            if (removalRequested)
            {
                // Destroy 예약 뒤 프레임 끝까지 목록에 남아 있는 객체도 상한 루프에서
                // 즉시 제외해 97번째 획 생성 시 같은 항목을 영원히 반복하지 않는다.
                active.Remove(this);
                runtimeDrawn.Remove(this);
                retainedInkCost = 0f;
                return;
            }
            removalRequested = true;
            removalCause = RemovalCause.BudgetEviction;
            retainedInkCost = 0f;
            active.Remove(this);
            runtimeDrawn.Remove(this);
            if (edge != null)
                edge.enabled = false;
            if (Line != null)
                Line.enabled = false;
            if (specialOutline != null)
                specialOutline.enabled = false;
            Destroy(gameObject);
        }

        float RequestBudgetEviction(float requestedInk)
        {
            if (!runtimeDrawnPlatform || removalRequested ||
                retainedInkCost <= 0.0001f || initialInkCost <= 0.0001f)
                return 0f;

            float released = Mathf.Min(
                retainedInkCost,
                Mathf.Max(0f, requestedInk));
            if (released <= 0f)
                return 0f;

            retainedInkCost -= released;
            if (removalCause == RemovalCause.None)
                removalCause = RemovalCause.BudgetEviction;
            evictionTargetFraction = Mathf.Clamp01(
                1f - retainedInkCost / initialInkCost);
            if (float.IsPositiveInfinity(evictionRequestedAt))
                evictionRequestedAt = Time.time;
            if (retainedInkCost <= 0.0001f)
            {
                retainedInkCost = 0f;
                active.Remove(this);
            }
            return released;
        }

        void UpdateRuntimeDrawnPlatform(float deltaTime, float now)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            bool evictionWasAlreadyRequested =
                !float.IsPositiveInfinity(evictionRequestedAt);
            naturalAge += safeDeltaTime;
            float evictionDeltaTime = safeDeltaTime;
            if (!naturalExpiryRequested &&
                naturalHoldDuration > 0f &&
                naturalAge >= naturalHoldDuration)
            {
                float overtime = Mathf.Max(0f, naturalAge - naturalHoldDuration);
                RequestNaturalExpiry(now - overtime);
                // 한 프레임이 유지시간 경계를 크게 넘겨도 초과분만 페이드에 쓴다.
                // 이미 FIFO가 진행 중이었다면 그 소멸은 프레임 전체만큼 계속 간다.
                if (!evictionWasAlreadyRequested)
                    evictionDeltaTime = overtime;
            }

            UpdateBudgetEviction(evictionDeltaTime, now);
        }

        void RequestNaturalExpiry(float requestTime)
        {
            if (naturalExpiryRequested || removalRequested)
                return;

            naturalExpiryRequested = true;
            if (removalCause == RemovalCause.None)
                removalCause = RemovalCause.NaturalExpiry;

            retainedInkCost = 0f;
            active.Remove(this);
            evictionTargetFraction = 1f;
            if (float.IsPositiveInfinity(evictionRequestedAt))
                evictionRequestedAt = requestTime;
        }

        void UpdateBudgetEviction(float deltaTime, float now)
        {
            if (evictionTargetFraction <= evictionVisualFraction + 0.0001f ||
                now < evictionRequestedAt + evictionDelay)
                return;

            float speed = 1f / Mathf.Max(0.15f, evictionFadeDuration);
            evictionVisualFraction = Mathf.MoveTowards(
                evictionVisualFraction,
                evictionTargetFraction,
                speed * Mathf.Max(0f, deltaTime));
            FadeVisual(evictionVisualFraction);
            TrimCollider(evictionVisualFraction);

            if (evictionVisualFraction < 0.9999f)
                return;
            removalRequested = true;
            if (removalCause == RemovalCause.None)
                removalCause = RemovalCause.BudgetEviction;
            if (edge != null)
                edge.enabled = false;
            Destroy(gameObject);
        }

        /// 처음 붓을 댄 쪽(t=0 지점)부터 투명해지는 알파 스윕 — 선의 길이·두께는 그대로,
        /// 먹이 마르며 스며들 듯 투명도만 쓸려나간다
        void FadeVisual(float t)
        {
            const float feather = 0.3f; // 투명↔불투명 경계의 부드러운 폭

            // collider가 잘린 경계 t부터 오른쪽으로만 feather를 둔다. 보이는 먹선인데
            // 이미 밟을 수 없는 구간이 생기지 않게 시각·물리의 시작점을 맞춘다.
            float front = Mathf.Min(1f + feather, t + feather);

            // 페이드 중 매 프레임 Gradient와 키 배열을 새로 만들지 않고 버퍼를 재사용한다.
            fadeAlphaKeys[0] = new GradientAlphaKey(0f, 0f);
            fadeAlphaKeys[1] = new GradientAlphaKey(0f, Mathf.Clamp01(front - feather));
            fadeAlphaKeys[2] = new GradientAlphaKey(0.96f, Mathf.Clamp01(front));
            fadeAlphaKeys[3] = new GradientAlphaKey(0.96f, 1f);
            fadeGradient.SetKeys(fadeColorKeys, fadeAlphaKeys);
            Line.colorGradient = fadeGradient;

            if (specialOutline == null)
                return;
            outlineFadeAlphaKeys[0] = new GradientAlphaKey(0f, 0f);
            outlineFadeAlphaKeys[1] = new GradientAlphaKey(
                0f,
                Mathf.Clamp01(front - feather));
            outlineFadeAlphaKeys[2] = new GradientAlphaKey(
                0.94f,
                Mathf.Clamp01(front));
            outlineFadeAlphaKeys[3] = new GradientAlphaKey(0.94f, 1f);
            outlineFadeGradient.SetKeys(
                outlineFadeColorKeys,
                outlineFadeAlphaKeys);
            specialOutline.colorGradient = outlineFadeGradient;
        }

        /// 투명해진 구간은 밟을 수 없도록 콜라이더도 같은 진행도로 잘라낸다 (비주얼은 그대로)
        void TrimCollider(float t)
        {
            if (originalPoints == null || originalPoints.Length < 2) return;

            int maxCutoff = originalPoints.Length - 2;
            int rawCutoff = Mathf.Clamp(
                Mathf.FloorToInt(t * (originalPoints.Length - 1)),
                0,
                maxCutoff);
            // EdgeCollider 재베이크와 배열 할당을 획당 최대 약 16회로 양자화한다.
            int quantum = Mathf.Max(1, Mathf.CeilToInt(maxCutoff / 16f));
            int cutoff = t >= 0.9999f
                ? maxCutoff
                : Mathf.Min(maxCutoff, rawCutoff / quantum * quantum);
            if (cutoff == lastColliderCutoff) return;
            lastColliderCutoff = cutoff;
            if (cutoff == 0)
                return;

            var remainingPoints = new Vector2[originalPoints.Length - cutoff];
            for (int i = 0; i < remainingPoints.Length; i++)
                remainingPoints[i] = originalPoints[cutoff + i];
            edge.points = remainingPoints; // 배열 전체를 한 번에 대입해야 콜라이더에 반영된다
        }

        float EffectiveLifetime
        {
            get
            {
                return lifetime;
            }
        }

        void OnDestroy()
        {
            active.Remove(this);
            runtimeDrawn.Remove(this);
            retainedInkCost = 0f;
        }

    }
}
