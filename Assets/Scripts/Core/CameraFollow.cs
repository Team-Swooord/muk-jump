using UnityEngine;

namespace MukJump.Core
{
    /// 클라이밍 게임 카메라: 플레이어를 따라 위로만 올라가고, 절대 내려오지 않는다.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("플레이어를 화면 중앙보다 얼마나 아래에 둘지 (월드 단위)")]
        [SerializeField] float lookAhead = 3f;
        [SerializeField] float smoothSpeed = 4f;
        [Header("강한 점프 카메라 강조")]
        [SerializeField, Range(0f, 0.08f)] float jumpZoomAmount = 0.025f;
        [SerializeField, Range(0f, 0.2f)] float jumpShakeAmount = 0.055f;
        [SerializeField, Min(0.1f)] float jumpImpulseDuration = 0.28f;

        float highestY;
        float baseOrthographicSize;
        float impulseRemaining;
        float impulseStrength;
        Vector2 visualShake;
        Camera worldCamera;

        public void PlayJumpImpulse(Transform source, float strength)
        {
            var highest = GameManager.Instance != null
                ? GameManager.Instance.HighestLivingPlayer
                : null;
            if (source == null || (highest != null && highest.transform != source)) return;
            impulseStrength = Mathf.Max(impulseStrength, Mathf.Clamp(strength, 0.55f, 1.5f));
            impulseRemaining = jumpImpulseDuration;
        }

        public void DebugSnapTo(Transform t)
        {
            if (t == null) return;
            target = t;
            highestY = t.position.y + lookAhead;
            var position = transform.position;
            position.y = highestY;
            transform.position = position;
        }

        void Start()
        {
            highestY = transform.position.y;
            worldCamera = GetComponent<Camera>();
            if (worldCamera != null) baseOrthographicSize = worldCamera.orthographicSize;
        }

        void LateUpdate()
        {
            var livingPlayer = GameManager.Instance != null
                ? GameManager.Instance.HighestLivingPlayer
                : null;
            if (livingPlayer != null) target = livingPlayer.transform;
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

            float desired = target.position.y + lookAhead;
            highestY = Mathf.Max(highestY, desired);

            var pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, highestY, smoothSpeed * Time.deltaTime);
            transform.position = pos;
            UpdateJumpImpulse();
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
            if (worldCamera != null && baseOrthographicSize > 0f)
                worldCamera.orthographicSize = baseOrthographicSize;
        }
    }
}
