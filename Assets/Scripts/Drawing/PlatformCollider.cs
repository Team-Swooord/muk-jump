using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// 스트로크 점열 하나 = 발판 하나. LineRenderer(붓선 비주얼) + EdgeCollider2D(물리).
    /// 일정 시간이 지나면 먹이 마르듯 서서히 사라진다. 씬에 미리 배치하면(시작 지형) 영구 발판.
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

        const int MaxActivePlatforms = 4;
        static readonly List<PlatformCollider> active = new();
        public static float RuntimeLifetimeMultiplier { get; set; } = 1f;

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
        /// 런타임에서 플레이어가 그린 유한 수명 먹선만 해태 돌진을 막을 수 있다.
        /// 시작 지형과 풍맥처럼 영구 배치된 발판은 수문장을 자동으로 제거하지 않는다.
        public bool IsTemporaryDrawnPlatform =>
            lifetime > 0f && !windCurrentPlatform && !growthSafetyPlatform &&
            !removalRequested;
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
        Vector2[] originalPoints;
        float age;
        float lastEffectiveLifetime;
        bool removalRequested;
        bool firstLandingHandled;
        float lifetimePauseRemaining;
        float spentInk;
        RemovalCause removalCause;
        int lastColliderCutoff = -1;

        /// 스무딩 완료된 월드 좌표 점열로 발판을 생성한다 (런타임 드로잉 경로)
        public static PlatformCollider Spawn(
            List<Vector2> worldPoints,
            float consumedInk = 0f)
        {
            var go = new GameObject("InkPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            var platform = go.AddComponent<PlatformCollider>();
            platform.spentInk = Mathf.Max(0f, consumedInk);
            platform.Build(worldPoints);

            active.Add(platform);
            while (active.Count > ActivePlatformBudget)
            {
                var oldest = active[0];
                if (oldest == null)
                {
                    active.RemoveAt(0);
                    continue;
                }
                oldest.BeginFade(); // 가장 오래된 발판부터 먹이 마른다
            }

            SketchToInkService.Instance?.Stylize(platform);
            return platform;
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
            Line = GetComponent<LineRenderer>();
            edge = GetComponent<EdgeCollider2D>();
            fadeColorKeys[0] = new GradientColorKey(InkPalette.Ink, 0f);
            fadeColorKeys[1] = new GradientColorKey(InkPalette.Ink, 1f);
            outlineFadeColorKeys[0] = new GradientColorKey(InkPalette.Ink, 0f);
            outlineFadeColorKeys[1] = new GradientColorKey(InkPalette.Ink, 1f);
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
            firstLandingHandled = false;
            lifetimePauseRemaining = 0f;
            spentInk = 0f;
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
            if (lifetime <= 0f) return; // 영구 발판

            float effectiveLifetime = EffectiveLifetime;
            SynchronizeLifetimeProgress(effectiveLifetime);

            float ageDelta = Time.deltaTime;
            if (lifetimePauseRemaining > 0f)
            {
                float paused = Mathf.Min(lifetimePauseRemaining, ageDelta);
                lifetimePauseRemaining -= paused;
                ageDelta -= paused;
            }
            age += ageDelta;
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
            if (RunGrowthController.Instance != null &&
                RunGrowthController.Instance.TryUsePermanentStrokeGuard())
            {
                // 영구 굳은 먹결은 발판별 상태가 아니라 먹떼 공용 18초 사용권이다.
                return true;
            }
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
            return true;
        }

        /// 처음 붓을 댄 쪽(t=0 지점)부터 투명해지는 알파 스윕 — 선의 길이·두께는 그대로,
        /// 먹이 마르며 스며들 듯 투명도만 쓸려나간다
        void FadeVisual(float t)
        {
            const float feather = 0.3f; // 투명↔불투명 경계의 부드러운 폭

            float front = Mathf.Lerp(0f, 1f + feather, t); // t=1이면 끝까지 완전히 투명

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

            int cutoff = Mathf.Clamp(Mathf.FloorToInt(t * (originalPoints.Length - 1)), 0,
                originalPoints.Length - 2);
            if (cutoff == lastColliderCutoff) return;
            lastColliderCutoff = cutoff;

            var remainingPoints = new Vector2[originalPoints.Length - cutoff];
            for (int i = 0; i < remainingPoints.Length; i++)
                remainingPoints[i] = originalPoints[cutoff + i];
            edge.points = remainingPoints; // 배열 전체를 한 번에 대입해야 콜라이더에 반영된다
        }

        /// 발판 수 초과 시 수명을 앞당겨 페이드아웃 시작
        void BeginFade()
        {
            // 이미 페이드 예약된 발판을 예산 목록에서 먼저 빼야, 빠르게 여러 획을
            // 그어도 같은 첫 발판만 반복 예약되고 후속 발판이 무한 누적되지 않는다.
            active.Remove(this);
            // 최대 4개 규칙은 비주얼 수가 아니라 실제로 밟을 수 있는 발판 수다.
            // 먹이 마르는 모습은 남기되 예산에서 밀린 순간 물리 충돌은 즉시 제거한다.
            if (edge != null) edge.enabled = false;
            if (lifetime <= 0f || removalRequested) return;
            removalCause = RemovalCause.BudgetEviction;
            float effectiveLifetime = EffectiveLifetime;
            lastEffectiveLifetime = effectiveLifetime;
            age = Mathf.Max(age, effectiveLifetime - fadeDuration);
        }

        static int ActivePlatformBudget => MaxActivePlatforms;

        float EffectiveLifetime
        {
            get
            {
                PermanentGrowthRunSnapshot permanent =
                    RunGrowthController.Instance != null
                        ? RunGrowthController.Instance.PermanentSnapshot
                        : PermanentGrowthProfile.CreateRunSnapshot();
                if (growthSafetyPlatform)
                    return lifetime;
                return lifetime *
                       Mathf.Clamp(RuntimeLifetimeMultiplier, 0.35f, 1f) *
                       Mathf.Clamp(
                           permanent.PlatformLifetimeMultiplier,
                           1f,
                           1.04f);
            }
        }

        void OnDestroy()
        {
            if (removalCause == RemovalCause.NaturalExpiry && spentInk > 0f)
                RunGrowthController.Instance?.TryRefundExpiredPlatform(spentInk);
            active.Remove(this);
        }

        /// 각 발판의 첫 착지에서만 영구 먹결의 수명 정지를 적용한다.
        public void NotifyFirstLanding()
        {
            if (firstLandingHandled || !IsTemporaryDrawnPlatform)
                return;
            firstLandingHandled = true;
            PermanentGrowthRunSnapshot permanent =
                RunGrowthController.Instance != null
                    ? RunGrowthController.Instance.PermanentSnapshot
                    : PermanentGrowthProfile.CreateRunSnapshot();
            if (permanent.HasFirstLandingPause)
                lifetimePauseRemaining = Mathf.Max(lifetimePauseRemaining, 0.15f);
        }
    }
}
