using UnityEngine;

namespace MukJump.Core
{
    /// 클라이밍 게임 카메라: 먹떼 대표를 화면의 균형 추적선에 두고 위로만 올라간다.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        /// 7월 29일 이전 34% 선행 구도와 과보정된 75% 데드존의 중간값.
        /// 위쪽 진행 공간을 확보하되 같은 높이의 반복 점프는 누적 추적하지 않는다.
        public const float BalancedFollowViewportY = 0.55f;
        public const int CurrentFollowTuningVersion = 1;

        [SerializeField] Transform target;
        [Tooltip("플레이어가 이 화면 높이에 닿으면 카메라가 위로 따라갑니다.")]
        [SerializeField, Range(0.5f, 0.9f)] float upperFollowViewportY =
            BalancedFollowViewportY;
        [SerializeField, HideInInspector] int followTuningVersion;
        [SerializeField] float smoothSpeed = 4f;
        [Tooltip("먹물방울처럼 급상승할 때 캐릭터가 화면 밖으로 나가지 않게 하는 상단 한계선")]
        [SerializeField, Range(0.8f, 0.98f)] float hardCeilingViewportY = 0.9f;
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
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.TryGetSwarmAnchor(
                        out var representative, out float swarmY))
                {
                    followY = swarmY;
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

            var pos = transform.position;
            EnsureCameraMetrics();
            highestFollowTargetY = Mathf.Max(highestFollowTargetY, pos.y);
            highestFollowTargetY = ResolveHighestFollowTargetY(
                highestFollowTargetY,
                followY,
                SafeBaseHalfHeight,
                SafeUpperFollowViewportY);
            pos.y = Mathf.Lerp(
                pos.y, highestFollowTargetY, smoothSpeed * Time.deltaTime);

            // 먹물방울 50m 상승처럼 보간보다 빠른 이동만 화면 상단 안전선으로 붙잡는다.
            // 점프 줌으로 순간 변경되는 orthographicSize가 아닌 기본 반높이를 사용해야
            // 같은 점프 안에서 추적선이 흔들리며 카메라가 조금씩 올라가는 현상이 없다.
            pos.y = ResolveHardCeilingCameraY(
                pos.y,
                followY,
                SafeBaseHalfHeight,
                SafeHardCeilingViewportY);
            highestFollowTargetY = Mathf.Max(highestFollowTargetY, pos.y);
            transform.position = pos;
            UpdateJumpImpulse();
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
    }
}
