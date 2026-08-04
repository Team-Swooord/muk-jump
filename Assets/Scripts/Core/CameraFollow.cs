using UnityEngine;

namespace MukJump.Core
{
    /// 클라이밍 게임 카메라: 먹떼 중앙을 균형 추적선에 두고 상위 무리도
    /// 별도 안전선으로 보호하면서 위로만 올라간다.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        /// 7월 29일 이전 34% 선행 구도와 과보정된 75% 데드존의 중간값.
        /// 위쪽 진행 공간을 확보하되 같은 높이의 반복 점프는 누적 추적하지 않는다.
        public const float BalancedFollowViewportY = 0.55f;
        public const float SurvivorReframeViewportY = 0.46f;
        public const float SurvivorReframeSeconds = 0.5f;
        public const int CurrentFollowTuningVersion = 1;

        [SerializeField] Transform target;
        [Tooltip("플레이어가 이 화면 높이에 닿으면 카메라가 위로 따라갑니다.")]
        [SerializeField, Range(0.5f, 0.9f)] float upperFollowViewportY =
            BalancedFollowViewportY;
        [SerializeField, HideInInspector] int followTuningVersion;
        [SerializeField] float smoothSpeed = 4f;
        [Tooltip("먹물방울처럼 급상승할 때 캐릭터가 화면 밖으로 나가지 않게 하는 상단 한계선")]
        [SerializeField, Range(0.8f, 0.98f)] float hardCeilingViewportY = 0.9f;
        [Header("먹떼 생존 재구도")]
        [Tooltip("높은 개체가 죽었을 때 남은 먹떼를 다시 놓는 화면 높이")]
        [SerializeField, Range(0.35f, 0.55f)] float survivorReframeViewportY =
            SurvivorReframeViewportY;
        [Tooltip("개체 사망 뒤 생존 먹떼로 한 번만 내려오는 카메라 시간")]
        [SerializeField, Min(0.1f)] float survivorReframeDuration =
            SurvivorReframeSeconds;
        [Header("강한 점프 카메라 강조")]
        [SerializeField, Range(0f, 0.08f)] float jumpZoomAmount = 0.025f;
        [SerializeField, Range(0f, 0.2f)] float jumpShakeAmount = 0.055f;
        [SerializeField, Min(0.1f)] float jumpImpulseDuration = 0.28f;

        float highestFollowTargetY;
        float baseOrthographicSize;
        float impulseRemaining;
        float impulseStrength;
        Vector2 visualShake;
        Camera worldCamera;
        Transform impulseLeader;
        bool survivorReframeRequested;
        bool survivorReframeActive;
        float survivorReframeElapsed;
        float survivorReframeFromY;
        float survivorReframeTargetY;

        /// 높은 본체·분신이 사라진 뒤 남은 먹떼가 화면 아래에 고립되지 않도록
        /// 다음 프레임의 생존 대표 위치로 한 번만 재구도한다.
        public void RequestSurvivorReframe()
        {
            survivorReframeRequested = true;
        }

        public void PlayJumpImpulse(Transform source, float strength)
        {
            if (source == null) return;
            if (impulseLeader == null && GameManager.Instance != null)
            {
                GameManager.Instance.TryGetSwarmAnchor(
                    out var representative, out _);
                impulseLeader = representative != null
                    ? representative.transform
                    : null;
            }
            if (impulseLeader != null && impulseLeader != source) return;
            impulseStrength = Mathf.Max(impulseStrength, Mathf.Clamp(strength, 0.55f, 1.5f));
            impulseRemaining = jumpImpulseDuration;
        }

        public void DebugSnapTo(Transform t)
        {
            if (t == null) return;
            target = t;
            EnsureCameraMetrics();
            var position = transform.position;
            position.y = t.position.y - ViewportWorldOffset(
                SafeBaseHalfHeight, SafeUpperFollowViewportY);
            transform.position = position;
            highestFollowTargetY = position.y;
        }

        void OnEnable()
        {
            UpgradeFollowTuning();
            // Play 중 재컴파일로 비직렬화 필드가 초기화돼도 현재 카메라보다 낮은
            // 목표로 되돌아가며 화면이 하강하지 않게 즉시 복구한다.
            EnsureCameraMetrics();
            highestFollowTargetY =
                Mathf.Max(highestFollowTargetY, transform.position.y);
        }

        void Start()
        {
            highestFollowTargetY = transform.position.y;
            EnsureCameraMetrics();
        }

        void LateUpdate()
        {
            float followY = target != null ? target.position.y : float.NegativeInfinity;
            float upperGuardY = followY;
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.TryGetSwarmCameraFrame(
                        out var representative,
                        out float clusterY,
                        out float swarmUpperGuardY))
                {
                    followY = clusterY;
                    upperGuardY = swarmUpperGuardY;
                    target = representative.transform;
                    impulseLeader = target;
                }
            }
            if (target == null) return;

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                // 줌·흔들림 중간 프레임까지 포함해 화면을 그대로 얼리고 재개한다.
                return;
            }

            // 사망 팝 연출을 따라 카메라까지 올라가면 게임오버 배경과 다음 도전의 기준점이
            // 흔들린다. 마지막 플레이 위치에서 카메라를 고정해 죽음 연출만 화면 안에서 보인다.
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                impulseRemaining = 0f;
                UpdateJumpImpulse();
                return;
            }

            if (float.IsNegativeInfinity(followY))
                followY = target.position.y;
            if (float.IsNegativeInfinity(upperGuardY))
                upperGuardY = followY;

            var pos = transform.position;
            EnsureCameraMetrics();
            BeginSurvivorReframeIfRequested(pos.y, followY);
            if (survivorReframeActive)
            {
                survivorReframeElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(
                    survivorReframeElapsed /
                    SafeSurvivorReframeDuration);
                pos.y = Mathf.Lerp(
                    survivorReframeFromY,
                    survivorReframeTargetY,
                    Mathf.SmoothStep(0f, 1f, progress));
                highestFollowTargetY = pos.y;
                if (progress >= 1f)
                    survivorReframeActive = false;
            }
            else
            {
                highestFollowTargetY = Mathf.Max(highestFollowTargetY, pos.y);
                highestFollowTargetY = ResolveHighestFollowTargetY(
                    highestFollowTargetY,
                    followY,
                    SafeBaseHalfHeight,
                    SafeUpperFollowViewportY);
                pos.y = Mathf.Lerp(
                    pos.y, highestFollowTargetY, smoothSpeed * Time.deltaTime);
            }

            // 먹물방울·엇갈린 분신 점프처럼 보간보다 빠른 상위 무리를 화면 상단
            // 안전선으로 붙잡는다. 다수 먹떼에서는 단독 이상치를 제외한 상위 75%를 쓴다.
            // 점프 줌으로 순간 변경되는 orthographicSize가 아닌 기본 반높이를 사용해야
            // 같은 점프 안에서 추적선이 흔들리며 카메라가 조금씩 올라가는 현상이 없다.
            pos.y = ResolveHardCeilingCameraY(
                pos.y,
                upperGuardY,
                SafeBaseHalfHeight,
                SafeHardCeilingViewportY);
            highestFollowTargetY = Mathf.Max(highestFollowTargetY, pos.y);
            transform.position = pos;
            UpdateJumpImpulse();
        }

        void BeginSurvivorReframeIfRequested(float currentCameraY, float clusterY)
        {
            if (!survivorReframeRequested)
                return;
            survivorReframeRequested = false;

            float targetY = ResolveSurvivorReframeCameraY(
                currentCameraY,
                clusterY,
                SafeBaseHalfHeight,
                SafeSurvivorReframeViewportY);
            if (targetY >= currentCameraY - 0.01f)
            {
                survivorReframeActive = false;
                return;
            }

            survivorReframeFromY = currentCameraY;
            survivorReframeTargetY = targetY;
            survivorReframeElapsed = 0f;
            survivorReframeActive = true;
        }

        /// 같은 높이의 점프 정점을 반복해도 카메라가 누적 상승하지 않게 이미 확정한
        /// 최고 추적 목표를 기준으로 추적선을 넘긴 만큼만 반환한다.
        public static float ResolveHighestFollowTargetY(
            float highestTargetY,
            float trackedY,
            float baseHalfHeight,
            float followViewportY)
        {
            if (float.IsNaN(trackedY) || float.IsInfinity(trackedY) ||
                baseHalfHeight <= 0f)
                return highestTargetY;
            float followOffset = ViewportWorldOffset(
                baseHalfHeight, followViewportY);
            return Mathf.Max(highestTargetY, trackedY - followOffset);
        }

        /// 급상승 중 추적 보간이 늦더라도 캐릭터가 지정한 뷰포트 상단선을 넘지 않게 한다.
        public static float ResolveHardCeilingCameraY(
            float currentCameraY,
            float trackedY,
            float baseHalfHeight,
            float ceilingViewportY)
        {
            if (float.IsNaN(trackedY) || float.IsInfinity(trackedY) ||
                baseHalfHeight <= 0f)
                return currentCameraY;

            float ceilingOffset = ViewportWorldOffset(
                baseHalfHeight, ceilingViewportY);
            return Mathf.Max(currentCameraY, trackedY - ceilingOffset);
        }

        /// 사망 이벤트에서만 사용하는 1회성 하강 목표. 일반 낙하 중에는 호출하지
        /// 않으므로 카메라가 추락을 따라 내려가 사망선을 무력화하지 않는다.
        public static float ResolveSurvivorReframeCameraY(
            float currentCameraY,
            float clusterY,
            float baseHalfHeight,
            float viewportY)
        {
            if (float.IsNaN(clusterY) || float.IsInfinity(clusterY) ||
                baseHalfHeight <= 0f)
                return currentCameraY;
            float safeViewportY = float.IsNaN(viewportY) || float.IsInfinity(viewportY)
                ? 0.46f
                : Mathf.Clamp(viewportY, 0.2f, 0.55f);
            float viewportOffset = Mathf.Max(0.01f, baseHalfHeight) *
                                   (safeViewportY * 2f - 1f);
            float targetY = clusterY - viewportOffset;
            return Mathf.Min(currentCameraY, targetY);
        }

        /// 낮은 개체의 추락까지 카메라 하강 사유로 삼으면 연속 사망 때마다
        /// 화면 하단 사망선도 함께 내려간다. 기존 상단 안전점을 실제로 이끌던
        /// 개체가 사라졌을 때만 남은 먹떼로 재구도한다.
        public static bool ShouldReframeAfterDeath(
            float dyingPlayerY,
            float survivingUpperGuardY)
        {
            if (float.IsNaN(dyingPlayerY) || float.IsInfinity(dyingPlayerY) ||
                float.IsNaN(survivingUpperGuardY) ||
                float.IsInfinity(survivingUpperGuardY))
                return false;
            return dyingPlayerY >= survivingUpperGuardY - 0.05f;
        }

        public static float ViewportWorldOffset(
            float baseHalfHeight,
            float viewportY)
        {
            if (baseHalfHeight <= 0f)
                return 0f;
            float safeViewportY = float.IsNaN(viewportY) ||
                                  float.IsInfinity(viewportY)
                ? BalancedFollowViewportY
                : Mathf.Clamp(viewportY, 0.5f, 0.98f);
            return Mathf.Max(0.01f, baseHalfHeight) *
                   (safeViewportY * 2f - 1f);
        }

        void UpdateJumpImpulse()
        {
            if (worldCamera == null) return;
            if (impulseRemaining <= 0f)
            {
                visualShake = Vector2.zero;
                worldCamera.orthographicSize = baseOrthographicSize;
                impulseStrength = 0f;
                return;
            }

            impulseRemaining -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(impulseRemaining / jumpImpulseDuration);
            float envelope = Mathf.Pow(1f - progress, 2f) * impulseStrength;
            float zoomPulse = Mathf.Sin(progress * Mathf.PI) * jumpZoomAmount * impulseStrength;
            worldCamera.orthographicSize = baseOrthographicSize * (1f - zoomPulse);
            visualShake = new Vector2(
                Mathf.Sin(Time.unscaledTime * 83f + 0.7f),
                Mathf.Sin(Time.unscaledTime * 67f + 2.1f)) * (jumpShakeAmount * envelope);
        }

        void OnPreCull()
        {
            if (worldCamera == null || visualShake.sqrMagnitude <= 0f) return;
            worldCamera.ResetProjectionMatrix();
            float halfHeight = Mathf.Max(0.01f, worldCamera.orthographicSize);
            float halfWidth = halfHeight * worldCamera.aspect;
            var offset = new Vector3(visualShake.x / halfWidth, visualShake.y / halfHeight, 0f);
            worldCamera.projectionMatrix =
                Matrix4x4.Translate(offset) * worldCamera.projectionMatrix;
        }

        void OnPostRender()
        {
            if (worldCamera != null) worldCamera.ResetProjectionMatrix();
        }

        void OnDisable()
        {
            visualShake = Vector2.zero;
            impulseLeader = null;
            if (worldCamera != null && baseOrthographicSize > 0f)
                worldCamera.orthographicSize = baseOrthographicSize;
        }

        void OnValidate()
        {
            UpgradeFollowTuning();
            upperFollowViewportY = SafeUpperFollowViewportY;
            smoothSpeed = Mathf.Max(0f, smoothSpeed);
            hardCeilingViewportY = SafeHardCeilingViewportY;
            survivorReframeViewportY = SafeSurvivorReframeViewportY;
            survivorReframeDuration = SafeSurvivorReframeDuration;
            jumpImpulseDuration = Mathf.Max(0.1f, jumpImpulseDuration);
        }

        void EnsureCameraMetrics()
        {
            if (worldCamera == null)
                worldCamera = GetComponent<Camera>();
            if (baseOrthographicSize <= 0f && worldCamera != null)
                baseOrthographicSize = worldCamera.orthographicSize;
        }

        void UpgradeFollowTuning()
        {
            if (followTuningVersion >= CurrentFollowTuningVersion)
                return;

            // 구버전 Main/Play 백업에 직렬화된 75% 과보정 값을 즉시 새 균형값으로 바꾼다.
            upperFollowViewportY = BalancedFollowViewportY;
            followTuningVersion = CurrentFollowTuningVersion;
        }

        float SafeBaseHalfHeight =>
            baseOrthographicSize > 0f
                ? baseOrthographicSize
                : worldCamera != null
                    ? Mathf.Max(0.01f, worldCamera.orthographicSize)
                    : 0f;

        float SafeUpperFollowViewportY =>
            float.IsNaN(upperFollowViewportY) ||
            float.IsInfinity(upperFollowViewportY) ||
            upperFollowViewportY < 0.5f
                ? BalancedFollowViewportY
                : Mathf.Clamp(upperFollowViewportY, 0.5f, 0.9f);

        float SafeHardCeilingViewportY =>
            float.IsNaN(hardCeilingViewportY) ||
            float.IsInfinity(hardCeilingViewportY) ||
            hardCeilingViewportY < 0.5f
                ? 0.9f
                : Mathf.Clamp(
                    hardCeilingViewportY,
                    Mathf.Max(0.8f, SafeUpperFollowViewportY),
                    0.98f);

        float SafeSurvivorReframeViewportY =>
            float.IsNaN(survivorReframeViewportY) ||
            float.IsInfinity(survivorReframeViewportY) ||
            survivorReframeViewportY < 0.35f
                ? SurvivorReframeViewportY
                : Mathf.Clamp(survivorReframeViewportY, 0.35f, 0.55f);

        float SafeSurvivorReframeDuration =>
            float.IsNaN(survivorReframeDuration) ||
            float.IsInfinity(survivorReframeDuration) ||
            survivorReframeDuration < 0.1f
                ? SurvivorReframeSeconds
                : survivorReframeDuration;
    }
}
