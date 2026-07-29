using System;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Obstacles
{
    /// 먹해태의 한 차례 습격 상태. Hidden은 카메라 진입 대기와 풀 유휴 상태를 함께 나타낸다.
    public enum HaetaeObstacleState
    {
        Hidden,
        Telegraph,
        Pounce,
        Land,
        Retreat,
    }

    /// 화면 옆에서 경로를 예고한 뒤 한 번만 덮치는 중반 수문장 장애물.
    /// 경로와 표적은 예고 시작 순간 고정하며, 플레이어 또는 임시 먹 발판과 처음 닿으면
    /// 공격을 즉시 소비한다. 경고선과 발자국은 자식 오브젝트를 한 번만 만들어 재사용한다.
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class HaetaeObstacle : MonoBehaviour, IPoolableEntity
    {
        const int RequiredFrameCount = 4;
        const int PawMarkerCount = 3;
        const int PawPointCount = 10;
        const float DefaultVisibleTopMargin = 0.5f;
        const float OffscreenHorizontalMargin = 0.72f;
        const float PounceArcHeight = 0.34f;
        const int CastHitCapacity = GameManager.MaxLivingPlayers * 2 + 8;

        SpriteRenderer spriteRenderer;
        Rigidbody2D body;
        CapsuleCollider2D hitbox;
        Sprite[] frames;
        Camera worldCamera;
        LayerMask interactionMask = Physics2D.DefaultRaycastLayers;
        Action<HaetaeObstacle> releaseHandler;
        Func<PlayerController> targetResolver;
        Func<bool> telegraphGate;

        readonly LineRenderer[] pawMarkers = new LineRenderer[PawMarkerCount];
        readonly RaycastHit2D[] castHits = new RaycastHit2D[CastHitCapacity];
        LineRenderer routeGuide;
        PlayerController deferredTarget;

        float telegraphDuration = 1.2f;
        float pounceDuration = 0.72f;
        float landDuration = 0.14f;
        float retreatDuration = 0.35f;
        float stateElapsed;
        float activationWorldY;
        float deferredVerticalOffset;
        float visibleTopMargin = DefaultVisibleTopMargin;
        bool enterFromLeft;
        bool attackConsumed;
        bool wasBlockedByPlatform;
        bool hasLockedPath;
        bool releaseRequested;
        bool hazardReservationRegistered;
        Vector2 lockedStart;
        Vector2 lockedTarget;
        Vector2 retreatStart;
        Vector2 retreatEnd;
        Vector3 baseScale = Vector3.one;
        Color baseColor = Color.white;
        int currentFrameIndex;

        public HaetaeObstacleState State { get; private set; } =
            HaetaeObstacleState.Hidden;
        public Vector2 LockedStart => lockedStart;
        public Vector2 LockedTarget => lockedTarget;
        public bool HasLockedPath => hasLockedPath;
        public bool AttackConsumed => attackConsumed;
        public bool WasBlockedByPlatform => wasBlockedByPlatform;
        public bool IsHitboxEnabled => hitbox != null && hitbox.enabled;
        public int CurrentFrameIndex => currentFrameIndex;
        public bool IsReleaseRequested => releaseRequested;
        public float ActivationWorldY => activationWorldY;

        void Awake()
        {
            EnsureComponents();
            EnsureWarningVisuals();
            baseScale = transform.localScale;
            baseColor = spriteRenderer.color;
            ResetRuntimeState();
        }

        /// 스프라이트·카메라·풀 반납 계약과 난이도 수치를 설정한다.
        /// 같은 참조를 재사용하므로 매 활성화마다 새 배열이나 경고 오브젝트를 만들지 않는다.
        public void Configure(
            Sprite[] animationFrames,
            Camera camera,
            LayerMask hitMask,
            Action<HaetaeObstacle> onRelease,
            float telegraphSeconds = 1.2f,
            float pounceSeconds = 0.72f,
            float landSeconds = 0.14f,
            float retreatSeconds = 0.35f,
            Vector2? colliderSize = null,
            Vector2? colliderOffset = null,
            Func<PlayerController> currentTargetResolver = null,
            Func<bool> canBeginTelegraph = null)
        {
            EnsureComponents();
            EnsureWarningVisuals();
            baseScale = transform.localScale;
            baseColor = spriteRenderer.color;
            frames = animationFrames;
            worldCamera = camera;
            interactionMask = hitMask;
            releaseHandler = onRelease;
            targetResolver = currentTargetResolver;
            telegraphGate = canBeginTelegraph;
            telegraphDuration = Mathf.Max(0.2f, telegraphSeconds);
            pounceDuration = Mathf.Max(0.2f, pounceSeconds);
            landDuration = Mathf.Max(0.02f, landSeconds);
            retreatDuration = Mathf.Max(0.05f, retreatSeconds);
            hitbox.size = colliderSize ?? new Vector2(1.42f, 0.76f);
            hitbox.offset = colliderOffset ?? new Vector2(0.02f, -0.03f);
            SyncWarningSorting();
            SetFrame(0);
        }

        /// 예약된 코스 높이가 카메라 안으로 들어올 때까지 완전히 숨겨 둔다.
        /// 실제 예고 시작 순간 살아 있는 표적 위치를 딱 한 번 읽고 이후에는 추적하지 않는다.
        public void Activate(
            PlayerController target,
            float courseWorldY,
            bool fromLeft,
            float verticalOffset = 0.6f,
            float topMargin = DefaultVisibleTopMargin)
        {
            EnsureComponents();
            baseScale = transform.localScale;
            baseColor = spriteRenderer.color;
            ResetRuntimeState();
            RegisterHazardReservation();
            deferredTarget = target;
            activationWorldY = courseWorldY;
            enterFromLeft = fromLeft;
            deferredVerticalOffset = Mathf.Clamp(verticalOffset, -1.2f, 1.2f);
            visibleTopMargin = Mathf.Max(0f, topMargin);
            body.position = new Vector2(body.position.x, courseWorldY);
        }

        /// 디버그와 테스트에서 이미 보이는 고정 경로를 즉시 예고한다.
        public void Activate(Vector2 startPosition, Vector2 targetPosition, bool fromLeft)
        {
            EnsureComponents();
            baseScale = transform.localScale;
            baseColor = spriteRenderer.color;
            ResetRuntimeState();
            RegisterHazardReservation();
            activationWorldY = Mathf.Max(startPosition.y, targetPosition.y);
            enterFromLeft = fromLeft;
            LockPathAndBeginTelegraph(startPosition, targetPosition);
        }

        /// 디버그 소환 또는 카메라 진입 직후의 명시적 예고 시작에 사용한다.
        /// 이미 경로가 고정됐거나 표적이 사라졌다면 아무것도 시작하지 않는다.
        public bool TryBeginTelegraphNow()
        {
            if (State != HaetaeObstacleState.Hidden || hasLockedPath)
                return false;
            if (telegraphGate != null && !telegraphGate())
                return false;

            PlayerController target = targetResolver?.Invoke();
            if (target == null || target.IsDead)
                target = deferredTarget;
            if (target == null || target.IsDead)
            {
                ForceRelease();
                return false;
            }

            Vector2 targetPosition = target.transform.position;
            Vector2 startPosition = ResolveScreenEdgeStart(targetPosition);
            LockPathAndBeginTelegraph(startPosition, targetPosition);
            deferredTarget = null;
            return true;
        }

        void FixedUpdate()
        {
            if (!CanTickGameplay()) return;
            AdvanceState(Time.fixedDeltaTime);
        }

        /// 단일 상태 진행 함수로, 에디터 테스트에서도 reflection을 통해 결정적으로 검증한다.
        void AdvanceState(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            switch (State)
            {
                case HaetaeObstacleState.Hidden:
                    if (IsActivationHeightVisible())
                        TryBeginTelegraphNow();
                    break;
                case HaetaeObstacleState.Telegraph:
                    AdvanceTelegraph(step);
                    break;
                case HaetaeObstacleState.Pounce:
                    AdvancePounce(step);
                    break;
                case HaetaeObstacleState.Land:
                    stateElapsed += step;
                    if (stateElapsed >= landDuration)
                        BeginRetreat();
                    break;
                case HaetaeObstacleState.Retreat:
                    AdvanceRetreat(step);
                    break;
            }
        }

        void AdvanceTelegraph(float deltaTime)
        {
            stateElapsed += deltaTime;
            float normalized = Mathf.Clamp01(stateElapsed / telegraphDuration);
            SetFrame(normalized < 0.28f ? 0 : 1);
            float pulse = 0.5f + 0.5f * Mathf.Sin(normalized * Mathf.PI * 6f);
            transform.localScale = baseScale * Mathf.Lerp(0.97f, 1.035f, pulse);
            UpdateWarningAlpha(Mathf.Lerp(0.34f, 0.72f, pulse));

            if (stateElapsed < telegraphDuration) return;

            stateElapsed = 0f;
            State = HaetaeObstacleState.Pounce;
            transform.localScale = baseScale;
            SetFrame(2);
            SetWarningVisible(false);
            body.simulated = true;
            hitbox.enabled = true;
        }

        void AdvancePounce(float deltaTime)
        {
            stateElapsed += deltaTime;
            float normalized = Mathf.Clamp01(stateElapsed / pounceDuration);
            Vector2 nextPosition = EvaluatePouncePosition(normalized);
            Vector2 currentPosition = body.position;
            Vector2 displacement = nextPosition - currentPosition;

            if (displacement.sqrMagnitude > 0.000001f &&
                TryResolveCastContact(currentPosition, displacement))
                return;

            if (normalized >= 1f)
            {
                // MovePosition 예약 직후 simulated를 끄면 마지막 좌표가 적용되지 않을 수 있어
                // 착지 프레임만은 고정된 종점에 즉시 맞춘다.
                body.position = nextPosition;
                BeginLand(false);
            }
            else
                body.MovePosition(nextPosition);
        }

        void AdvanceRetreat(float deltaTime)
        {
            stateElapsed += deltaTime;
            float normalized = Mathf.Clamp01(stateElapsed / retreatDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            transform.position = Vector2.LerpUnclamped(retreatStart, retreatEnd, eased);
            float rotationDirection = enterFromLeft ? 1f : -1f;
            transform.localRotation =
                Quaternion.Euler(0f, 0f, rotationDirection * 28f * eased);
            Color color = baseColor;
            color.a = baseColor.a * (1f - normalized);
            spriteRenderer.color = color;

            if (normalized >= 1f)
                ForceRelease();
        }

        bool TryResolveCastContact(Vector2 origin, Vector2 displacement)
        {
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon || interactionMask.value == 0)
                return false;

            Vector2 worldScale = new(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
            Vector2 castSize = Vector2.Scale(hitbox.size, worldScale);
            var filter = new ContactFilter2D
            {
                useTriggers = true,
            };
            filter.SetLayerMask(interactionMask);
            int hitCount = Physics2D.CapsuleCast(
                origin + Vector2.Scale(hitbox.offset, worldScale),
                castSize,
                CapsuleDirection2D.Horizontal,
                transform.eulerAngles.z,
                displacement / distance,
                filter,
                castHits,
                distance);

            Collider2D nearestValidCollider = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D candidate = castHits[i].collider;
                if (!IsResolvableContact(candidate) ||
                    castHits[i].distance >= nearestDistance)
                    continue;
                nearestValidCollider = candidate;
                nearestDistance = castHits[i].distance;
            }
            return nearestValidCollider != null &&
                   ResolveContact(nearestValidCollider);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            ResolveContact(other);
        }

        bool ResolveContact(Collider2D other)
        {
            if (State != HaetaeObstacleState.Pounce || attackConsumed || other == null)
                return false;

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (player.IsDead) return false;
                // TakeHit 결과와 무관하게 첫 살아 있는 캐릭터 접촉이 이번 습격의 전부다.
                // 방어막·먹물방울·디버그 무적이어도 뒤의 분신까지 연속으로 쓸지 않는다.
                attackConsumed = true;
                GameFeedbackController.Instance?.PlayHazardImpact(transform.position);
                BeginLand(false);
                player.TakeHit();
                return true;
            }

            var platform = other.GetComponentInParent<PlatformCollider>();
            if (platform == null || !platform.IsTemporaryDrawnPlatform)
                return false;

            // 선은 파괴하지 않는다. 플레이어가 가로질러 그은 임시 먹 발판만 방패가 된다.
            attackConsumed = true;
            wasBlockedByPlatform = true;
            GameFeedbackController.Instance?.PlayHazardImpact(transform.position);
            BeginLand(true);
            return true;
        }

        static bool IsResolvableContact(Collider2D other)
        {
            if (other == null) return false;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                return !player.IsDead;
            var platform = other.GetComponentInParent<PlatformCollider>();
            return platform != null && platform.IsTemporaryDrawnPlatform;
        }

        void BeginLand(bool blocked)
        {
            hitbox.enabled = false;
            body.simulated = false;
            stateElapsed = 0f;
            State = HaetaeObstacleState.Land;
            SetWarningVisible(false);
            SetFrame(3);
            if (blocked)
                transform.localScale = new Vector3(
                    baseScale.x * 1.05f, baseScale.y * 0.9f, baseScale.z);
            else
                transform.localScale = baseScale;
        }

        void BeginRetreat()
        {
            stateElapsed = 0f;
            State = HaetaeObstacleState.Retreat;
            hitbox.enabled = false;
            body.simulated = false;
            retreatStart = transform.position;
            Vector2 routeDirection = lockedTarget - lockedStart;
            if (routeDirection.sqrMagnitude < 0.0001f)
                routeDirection = enterFromLeft ? Vector2.right : Vector2.left;
            routeDirection.Normalize();
            retreatEnd = retreatStart - routeDirection * 0.62f + Vector2.up * 0.2f;
            transform.localScale = baseScale;
        }

        void LockPathAndBeginTelegraph(Vector2 startPosition, Vector2 targetPosition)
        {
            lockedStart = startPosition;
            lockedTarget = targetPosition;
            hasLockedPath = true;
            attackConsumed = false;
            wasBlockedByPlatform = false;
            stateElapsed = 0f;
            body.simulated = false;
            // 비시뮬레이션 Rigidbody2D는 position만 바꾸면 simulated=true 전환 때
            // Transform의 이전 좌표로 되돌아갈 수 있다. 둘을 같은 시작점에 맞춘다.
            transform.position = new Vector3(
                startPosition.x, startPosition.y, transform.position.z);
            body.position = startPosition;
            hitbox.enabled = false;
            State = HaetaeObstacleState.Telegraph;
            spriteRenderer.enabled = true;
            spriteRenderer.color = baseColor;
            Vector2 direction = lockedTarget - lockedStart;
            if (direction.sqrMagnitude > 0.0001f)
                enterFromLeft = direction.x >= 0f;
            // 원본 해태의 머리는 왼쪽을 향한다. 왼쪽에서 오른쪽으로 덮칠 때만
            // 좌우 반전해 항상 진행 방향을 바라보게 한다.
            spriteRenderer.flipX = enterFromLeft;
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            SetFrame(0);
            SyncWarningSorting();
            LayoutLockedWarningPath();
            SetWarningVisible(true);
        }

        Vector2 ResolveScreenEdgeStart(Vector2 targetPosition)
        {
            if (worldCamera == null)
            {
                float fallbackX = targetPosition.x +
                    (enterFromLeft ? -5.8f : 5.8f);
                return new Vector2(fallbackX, targetPosition.y + deferredVerticalOffset);
            }

            float cameraDistance = Mathf.Abs(
                worldCamera.transform.position.z - transform.position.z);
            float viewportX = enterFromLeft ? 0f : 1f;
            float edgeX = worldCamera.ViewportToWorldPoint(
                new Vector3(viewportX, 0.5f, cameraDistance)).x;
            float startX = edgeX +
                (enterFromLeft ? -OffscreenHorizontalMargin : OffscreenHorizontalMargin);
            return new Vector2(startX, targetPosition.y + deferredVerticalOffset);
        }

        bool IsActivationHeightVisible()
        {
            if (worldCamera == null) return true;
            float cameraDistance = Mathf.Abs(
                worldCamera.transform.position.z - transform.position.z);
            float cameraTop = worldCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f, cameraDistance)).y;
            return activationWorldY <= cameraTop - visibleTopMargin;
        }

        Vector2 EvaluatePouncePosition(float normalized)
        {
            Vector2 position = Vector2.LerpUnclamped(
                lockedStart, lockedTarget, normalized);
            position.y += 4f * PounceArcHeight * normalized * (1f - normalized);
            return position;
        }

        void LayoutLockedWarningPath()
        {
            EnsureWarningVisuals();
            routeGuide.positionCount = 4;
            routeGuide.SetPosition(0, EvaluatePouncePosition(0f));
            routeGuide.SetPosition(1, EvaluatePouncePosition(0.33f));
            routeGuide.SetPosition(2, EvaluatePouncePosition(0.66f));
            routeGuide.SetPosition(3, EvaluatePouncePosition(1f));

            Vector2 direction = lockedTarget - lockedStart;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;
            direction.Normalize();
            Vector2 side = new(-direction.y, direction.x);
            for (int markerIndex = 0; markerIndex < pawMarkers.Length; markerIndex++)
            {
                float pathT = 0.3f + markerIndex * 0.22f;
                Vector2 center = EvaluatePouncePosition(pathT) +
                    side * (markerIndex % 2 == 0 ? 0.055f : -0.055f);
                LayoutPawMarker(pawMarkers[markerIndex], center, direction, side);
            }
        }

        static void LayoutPawMarker(
            LineRenderer marker,
            Vector2 center,
            Vector2 forward,
            Vector2 side)
        {
            for (int pointIndex = 0; pointIndex < PawPointCount; pointIndex++)
            {
                float angle = pointIndex * Mathf.PI * 2f / PawPointCount;
                float toeBump = pointIndex >= 2 && pointIndex <= 4 ? 1.18f : 1f;
                float forwardRadius = Mathf.Cos(angle) * 0.13f * toeBump;
                float sideRadius = Mathf.Sin(angle) * 0.095f;
                marker.SetPosition(pointIndex,
                    center + forward * forwardRadius + side * sideRadius);
            }
        }

        void EnsureWarningVisuals()
        {
            if (routeGuide == null)
                routeGuide = CreateWarningLine(
                    "HaetaeLockedRoute", false, 4, 0.035f, -1);
            for (int i = 0; i < pawMarkers.Length; i++)
            {
                if (pawMarkers[i] == null)
                    pawMarkers[i] = CreateWarningLine(
                        $"HaetaePawMarker{i + 1}", true, PawPointCount, 0.024f, 0);
            }
            SetWarningVisible(false);
        }

        LineRenderer CreateWarningLine(
            string objectName,
            bool loop,
            int positionCount,
            float width,
            int sortingOffset)
        {
            Transform existing = transform.Find(objectName);
            GameObject lineObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName);
            if (existing == null)
                lineObject.transform.SetParent(transform, false);
            var line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
                line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = positionCount;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
            line.sortingLayerID = spriteRenderer != null
                ? spriteRenderer.sortingLayerID
                : 0;
            line.sortingOrder = (spriteRenderer != null
                ? spriteRenderer.sortingOrder
                : 0) + sortingOffset;
            Color ink = InkPalette.Ink;
            ink.a = 0.5f;
            line.startColor = line.endColor = ink;
            line.enabled = false;
            return line;
        }

        void UpdateWarningAlpha(float alpha)
        {
            Color ink = InkPalette.Ink;
            ink.a = alpha;
            routeGuide.startColor = routeGuide.endColor = ink;
            Color pawInk = ink;
            pawInk.a = Mathf.Min(0.82f, alpha + 0.12f);
            for (int i = 0; i < pawMarkers.Length; i++)
                pawMarkers[i].startColor = pawMarkers[i].endColor = pawInk;
        }

        void SyncWarningSorting()
        {
            if (spriteRenderer == null) return;
            if (routeGuide != null)
            {
                routeGuide.sortingLayerID = spriteRenderer.sortingLayerID;
                routeGuide.sortingOrder = spriteRenderer.sortingOrder - 1;
            }
            for (int i = 0; i < pawMarkers.Length; i++)
            {
                if (pawMarkers[i] == null) continue;
                pawMarkers[i].sortingLayerID = spriteRenderer.sortingLayerID;
                pawMarkers[i].sortingOrder = spriteRenderer.sortingOrder;
            }
        }

        void SetWarningVisible(bool visible)
        {
            if (routeGuide != null) routeGuide.enabled = visible;
            for (int i = 0; i < pawMarkers.Length; i++)
                if (pawMarkers[i] != null)
                    pawMarkers[i].enabled = visible;
        }

        void SetFrame(int frameIndex)
        {
            currentFrameIndex = Mathf.Clamp(frameIndex, 0, RequiredFrameCount - 1);
            if (frames == null || frames.Length <= currentFrameIndex ||
                frames[currentFrameIndex] == null)
                return;
            spriteRenderer.sprite = frames[currentFrameIndex];
        }

        bool CanTickGameplay()
        {
            return GameManager.Instance == null ||
                   GameManager.Instance.IsGameplayTicking;
        }

        public void ForceRelease()
        {
            if (releaseRequested) return;
            releaseRequested = true;
            UnregisterHazardReservation();
            hitbox.enabled = false;
            body.simulated = false;
            spriteRenderer.enabled = false;
            SetWarningVisible(false);
            State = HaetaeObstacleState.Hidden;
            Action<HaetaeObstacle> callback = releaseHandler;
            if (callback != null)
                callback(this);
            else
                gameObject.SetActive(false);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            EnsureWarningVisuals();
            ResetRuntimeState();
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            ResetRuntimeState();
        }

        void OnDisable()
        {
            UnregisterHazardReservation();
        }

        void ResetRuntimeState()
        {
            State = HaetaeObstacleState.Hidden;
            stateElapsed = 0f;
            activationWorldY = 0f;
            deferredVerticalOffset = 0f;
            visibleTopMargin = DefaultVisibleTopMargin;
            deferredTarget = null;
            attackConsumed = false;
            wasBlockedByPlatform = false;
            hasLockedPath = false;
            releaseRequested = false;
            lockedStart = Vector2.zero;
            lockedTarget = Vector2.zero;
            retreatStart = Vector2.zero;
            retreatEnd = Vector2.zero;
            currentFrameIndex = 0;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            hitbox.enabled = false;
            spriteRenderer.enabled = false;
            spriteRenderer.flipX = false;
            spriteRenderer.color = baseColor;
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            SetFrame(0);
            SetWarningVisible(false);
        }

        void RegisterHazardReservation()
        {
            if (hazardReservationRegistered) return;
            hazardReservationRegistered = true;
            HazardConcurrencyGate.RegisterHaetae();
        }

        void UnregisterHazardReservation()
        {
            if (!hazardReservationRegistered) return;
            hazardReservationRegistered = false;
            HazardConcurrencyGate.UnregisterHaetae();
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (body == null)
                body = GetComponent<Rigidbody2D>();
            if (hitbox == null)
                hitbox = GetComponent<CapsuleCollider2D>();

            hitbox.isTrigger = true;
            hitbox.direction = CapsuleDirection2D.Horizontal;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }
}
