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
        SealAway,
    }

    /// 화면 한쪽의 느낌표와 붉은 먹빛으로 예고한 뒤 가로로 한 번만 달려오는 중반 수문장.
    /// 공격 높이와 방향은 예고 시작 순간 고정하며, 플레이어 또는 임시 먹 발판과 처음 닿으면
    /// 공격을 즉시 소비한다. 경고 띠·느낌표·경로선은 자식 오브젝트를 한 번만 만들어 재사용한다.
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class HaetaeObstacle : MonoBehaviour, IPoolableEntity
    {
        const int RequiredFrameCount = 4;
        const int SideWarningLayerCount = 3;
        const int WarningCirclePointCount = 10;
        const float DefaultVisibleTopMargin = 0.5f;
        const float ScreenEdgeInset = 0.52f;
        const float WarningBandHalfHeight = 1.45f;
        const float WarningBandWidth = 0.82f;
        const float WarningOnlyFraction = 0.48f;
        const float MaterializeDuration = 0.34f;
        const string MaterializeSealName = "HaetaeMaterializeSeal";
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

        // 0: 반투명 위험 띠, 1: 느낌표 줄기, 2: 느낌표 점.
        readonly LineRenderer[] sideWarningLayers =
            new LineRenderer[SideWarningLayerCount];
        readonly RaycastHit2D[] castHits = new RaycastHit2D[CastHitCapacity];
        LineRenderer routeGuide;
        SpriteRenderer materializeSeal;
        PlayerController deferredTarget;

        float telegraphDuration = 1.2f;
        float pounceDuration = 0.72f;
        float landDuration = 0.14f;
        float sealAwayDuration = 0.35f;
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
        Vector2 sealAwayAnchor;
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
        public bool IsMaterializeSealVisible =>
            materializeSeal != null && materializeSeal.enabled;
        public float MaterializeSealAlpha =>
            materializeSeal != null ? materializeSeal.color.a : 0f;
        public float BodyAlpha =>
            spriteRenderer != null ? spriteRenderer.color.a : 0f;
        public bool IsSideWarningVisible =>
            sideWarningLayers[0] != null && sideWarningLayers[0].enabled;
        public bool IsExclamationVisible =>
            sideWarningLayers[1] != null && sideWarningLayers[1].enabled &&
            sideWarningLayers[2] != null && sideWarningLayers[2].enabled;
        public float WarningBandAlpha =>
            sideWarningLayers[0] != null
                ? sideWarningLayers[0].startColor.a
                : 0f;

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
            float sealAwaySeconds = 0.35f,
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
            sealAwayDuration = Mathf.Max(0.05f, sealAwaySeconds);
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
            float verticalOffset = -0.9f,
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
            deferredVerticalOffset = Mathf.Clamp(verticalOffset, -1.5f, 1.5f);
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

            ResolveSideChargePath(
                target.transform.position,
                out Vector2 startPosition,
                out Vector2 endPosition);
            LockPathAndBeginTelegraph(startPosition, endPosition);
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
                        BeginSealAway();
                    break;
                case HaetaeObstacleState.SealAway:
                    AdvanceSealAway(step);
                    break;
            }
        }

        void AdvanceTelegraph(float deltaTime)
        {
            stateElapsed += deltaTime;
            float normalized = Mathf.Clamp01(stateElapsed / telegraphDuration);
            float materializeStart = telegraphDuration * WarningOnlyFraction;
            float materializeElapsed = stateElapsed - materializeStart;
            bool materializing = false;
            if (materializeElapsed < 0f)
            {
                Color hiddenColor = baseColor;
                hiddenColor.a = 0f;
                spriteRenderer.color = hiddenColor;
                SetMaterializeSealVisible(false);
                SetFrame(0);
            }
            else
            {
                SetFrame(1);
                materializing = AdvanceMaterializeVisual(materializeElapsed);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(normalized * Mathf.PI * 6f);
            transform.localScale = materializing
                ? baseScale
                : baseScale * Mathf.Lerp(0.97f, 1.035f, pulse);
            UpdateWarningPulse(pulse);

            if (stateElapsed < telegraphDuration) return;

            stateElapsed = 0f;
            State = HaetaeObstacleState.Pounce;
            transform.localScale = baseScale;
            spriteRenderer.color = baseColor;
            SetMaterializeSealVisible(false);
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

        bool AdvanceMaterializeVisual(float elapsed)
        {
            if (elapsed >= MaterializeDuration)
            {
                spriteRenderer.color = baseColor;
                SetMaterializeSealVisible(false);
                return false;
            }

            float normalized = Mathf.Clamp01(elapsed / MaterializeDuration);
            float bodyReveal = SmoothRange(0.08f, 0.82f, normalized);
            Color color = baseColor;
            color.a = baseColor.a * bodyReveal;
            spriteRenderer.color = color;

            float sealAlpha = 1f - SmoothRange(0.3f, 0.95f, normalized);
            float sealScale = Mathf.Lerp(
                0.62f,
                1.08f,
                1f - Mathf.Pow(1f - normalized, 3f));
            UpdateMaterializeSeal(sealAlpha, sealScale);
            return true;
        }

        void AdvanceSealAway(float deltaTime)
        {
            stateElapsed += deltaTime;
            float normalized = Mathf.Clamp01(stateElapsed / sealAwayDuration);

            // 퇴장 중 본체를 날리거나 회전·비균일 축소하지 않는다.
            // 착지 지점에 고정된 낙관으로 스며들어 공격이 끝났음을 명확히 보여 준다.
            transform.position = new Vector3(
                sealAwayAnchor.x, sealAwayAnchor.y, transform.position.z);
            transform.localRotation = Quaternion.identity;
            transform.localScale = baseScale;

            Color color = baseColor;
            color.a = baseColor.a *
                (1f - SmoothRange(0.08f, 0.82f, normalized));
            spriteRenderer.color = color;

            float sealEnvelope = Mathf.Sin(Mathf.PI * normalized);
            float sealScale = Mathf.Lerp(
                0.7f,
                1.12f,
                Mathf.SmoothStep(0f, 1f, normalized));
            UpdateMaterializeSeal(0.82f * sealEnvelope, sealScale);

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

        void BeginSealAway()
        {
            stateElapsed = 0f;
            State = HaetaeObstacleState.SealAway;
            hitbox.enabled = false;
            body.simulated = false;
            sealAwayAnchor = transform.position;
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            UpdateMaterializeSeal(0.18f, 0.7f);
        }

        void LockPathAndBeginTelegraph(Vector2 startPosition, Vector2 targetPosition)
        {
            // 벽에서 달려오는 위험은 한 높이의 수평 차선으로 고정한다.
            // 예고가 끝난 뒤 플레이어를 따라 꺾이지 않아 보고 피하거나 먹선으로 막을 수 있다.
            targetPosition.y = startPosition.y;
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
            Color hiddenColor = baseColor;
            hiddenColor.a = 0f;
            spriteRenderer.color = hiddenColor;
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
            SetMaterializeSealVisible(false);
            UpdateWarningPulse(0.5f);
        }

        static float SmoothRange(float minimum, float maximum, float value)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(minimum, maximum, value));
        }

        void ResolveSideChargePath(
            Vector2 targetPosition,
            out Vector2 startPosition,
            out Vector2 endPosition)
        {
            if (worldCamera == null)
            {
                float fallbackLeft = targetPosition.x - 5.8f;
                float fallbackRight = targetPosition.x + 5.8f;
                float fallbackLaneY = targetPosition.y + deferredVerticalOffset;
                startPosition = new Vector2(
                    enterFromLeft ? fallbackLeft : fallbackRight,
                    fallbackLaneY);
                endPosition = new Vector2(
                    enterFromLeft ? fallbackRight : fallbackLeft,
                    fallbackLaneY);
                return;
            }

            float cameraDistance = Mathf.Abs(
                worldCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = worldCamera.ViewportToWorldPoint(
                new Vector3(0f, 0f, cameraDistance));
            Vector3 topRight = worldCamera.ViewportToWorldPoint(
                new Vector3(1f, 1f, cameraDistance));
            float left = bottomLeft.x + ScreenEdgeInset;
            float right = topRight.x - ScreenEdgeInset;
            float minimumLaneY = bottomLeft.y + WarningBandHalfHeight;
            float maximumLaneY = topRight.y - WarningBandHalfHeight;
            float laneY = targetPosition.y + deferredVerticalOffset;
            laneY = maximumLaneY >= minimumLaneY
                ? Mathf.Clamp(laneY, minimumLaneY, maximumLaneY)
                : (bottomLeft.y + topRight.y) * 0.5f;
            startPosition = new Vector2(enterFromLeft ? left : right, laneY);
            endPosition = new Vector2(enterFromLeft ? right : left, laneY);
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
            return Vector2.LerpUnclamped(lockedStart, lockedTarget, normalized);
        }

        void LayoutLockedWarningPath()
        {
            EnsureWarningVisuals();
            routeGuide.positionCount = 4;
            routeGuide.SetPosition(0, EvaluatePouncePosition(0f));
            routeGuide.SetPosition(1, EvaluatePouncePosition(0.33f));
            routeGuide.SetPosition(2, EvaluatePouncePosition(0.66f));
            routeGuide.SetPosition(3, EvaluatePouncePosition(1f));

            float warningX = lockedStart.x;
            sideWarningLayers[0].positionCount = 2;
            sideWarningLayers[0].SetPosition(
                0, new Vector3(warningX, lockedStart.y - WarningBandHalfHeight));
            sideWarningLayers[0].SetPosition(
                1, new Vector3(warningX, lockedStart.y + WarningBandHalfHeight));

            float inward = enterFromLeft ? 1f : -1f;
            float iconX = warningX + inward * 0.16f;
            sideWarningLayers[1].positionCount = 2;
            sideWarningLayers[1].SetPosition(
                0, new Vector3(iconX, lockedStart.y + 0.18f));
            sideWarningLayers[1].SetPosition(
                1, new Vector3(iconX, lockedStart.y + 0.78f));
            LayoutWarningDot(
                sideWarningLayers[2],
                new Vector2(iconX, lockedStart.y - 0.16f));
        }

        static void LayoutWarningDot(
            LineRenderer marker,
            Vector2 center)
        {
            for (int pointIndex = 0;
                 pointIndex < WarningCirclePointCount;
                 pointIndex++)
            {
                float angle = pointIndex * Mathf.PI * 2f /
                              WarningCirclePointCount;
                marker.SetPosition(pointIndex,
                    center + new Vector2(
                        Mathf.Cos(angle) * 0.09f,
                        Mathf.Sin(angle) * 0.09f));
            }
        }

        void EnsureWarningVisuals()
        {
            EnsureMaterializeSeal();
            if (routeGuide == null)
                routeGuide = CreateWarningLine(
                    "HaetaeLockedRoute", false, 4, 0.035f, -1);
            if (sideWarningLayers[0] == null)
                sideWarningLayers[0] = CreateWarningLine(
                    "HaetaeSideDangerBand", false, 2, WarningBandWidth, -2);
            if (sideWarningLayers[1] == null)
                sideWarningLayers[1] = CreateWarningLine(
                    "HaetaeExclamationStem", false, 2, 0.13f, 1);
            if (sideWarningLayers[2] == null)
                sideWarningLayers[2] = CreateWarningLine(
                    "HaetaeExclamationDot", true,
                    WarningCirclePointCount, 0.08f, 1);
            SetWarningVisible(false);
        }

        void EnsureMaterializeSeal()
        {
            if (materializeSeal != null) return;

            Transform existing = transform.Find(MaterializeSealName);
            GameObject sealObject = existing != null
                ? existing.gameObject
                : new GameObject(MaterializeSealName);
            if (existing == null)
                sealObject.transform.SetParent(transform, false);

            materializeSeal = sealObject.GetComponent<SpriteRenderer>();
            if (materializeSeal == null)
                materializeSeal = sealObject.AddComponent<SpriteRenderer>();
            materializeSeal.sprite = InkUiTextureFactory.CreateBlobSprite();
            materializeSeal.sharedMaterial =
                spriteRenderer != null ? spriteRenderer.sharedMaterial : null;
            materializeSeal.flipX = false;
            materializeSeal.flipY = false;
            materializeSeal.enabled = false;
            sealObject.transform.localPosition = Vector3.zero;
            sealObject.transform.localRotation = Quaternion.identity;
            sealObject.transform.localScale = Vector3.one * 0.62f;
            SyncMaterializeSealSorting();
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
            Color warningRed = InkPalette.Red;
            warningRed.a = 0.5f;
            line.startColor = line.endColor = warningRed;
            line.enabled = false;
            return line;
        }

        void UpdateWarningPulse(float pulse)
        {
            Color warningRed = InkPalette.Red;
            warningRed.a = Mathf.Lerp(0.22f, 0.5f, pulse);
            routeGuide.startColor = routeGuide.endColor = warningRed;

            Color band = InkPalette.Red;
            band.a = Mathf.Lerp(0.08f, 0.22f, pulse);
            sideWarningLayers[0].startColor =
                sideWarningLayers[0].endColor = band;

            Color exclamation = InkPalette.Paper;
            exclamation.a = Mathf.Lerp(0.62f, 1f, pulse);
            sideWarningLayers[1].startColor =
                sideWarningLayers[1].endColor = exclamation;
            sideWarningLayers[2].startColor =
                sideWarningLayers[2].endColor = exclamation;
        }

        void SyncWarningSorting()
        {
            if (spriteRenderer == null) return;
            if (routeGuide != null)
            {
                routeGuide.sortingLayerID = spriteRenderer.sortingLayerID;
                routeGuide.sortingOrder = spriteRenderer.sortingOrder - 1;
            }
            SyncMaterializeSealSorting();
            for (int i = 0; i < sideWarningLayers.Length; i++)
            {
                if (sideWarningLayers[i] == null) continue;
                sideWarningLayers[i].sortingLayerID = spriteRenderer.sortingLayerID;
                sideWarningLayers[i].sortingOrder = spriteRenderer.sortingOrder +
                    (i == 0 ? -2 : 1);
            }
        }

        void SyncMaterializeSealSorting()
        {
            if (spriteRenderer == null || materializeSeal == null) return;
            materializeSeal.sortingLayerID = spriteRenderer.sortingLayerID;
            materializeSeal.sortingOrder = spriteRenderer.sortingOrder - 2;
        }

        void UpdateMaterializeSeal(float alpha, float scale)
        {
            EnsureMaterializeSeal();
            Color sealColor = InkPalette.Red;
            sealColor.a = Mathf.Clamp01(alpha);
            materializeSeal.color = sealColor;
            materializeSeal.transform.localPosition = Vector3.zero;
            materializeSeal.transform.localRotation = Quaternion.identity;
            materializeSeal.transform.localScale =
                Vector3.one * Mathf.Max(0.01f, scale);
            materializeSeal.enabled = sealColor.a > 0.001f;
        }

        void SetMaterializeSealVisible(bool visible)
        {
            if (materializeSeal == null) return;
            materializeSeal.enabled = visible;
            if (!visible)
            {
                Color sealColor = InkPalette.Red;
                sealColor.a = 0f;
                materializeSeal.color = sealColor;
            }
        }

        void SetWarningVisible(bool visible)
        {
            if (routeGuide != null) routeGuide.enabled = visible;
            for (int i = 0; i < sideWarningLayers.Length; i++)
                if (sideWarningLayers[i] != null)
                    sideWarningLayers[i].enabled = visible;
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
            SetMaterializeSealVisible(false);
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
            sealAwayAnchor = Vector2.zero;
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
            SetMaterializeSealVisible(false);
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
