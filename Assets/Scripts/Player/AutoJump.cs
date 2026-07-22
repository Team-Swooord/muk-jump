using UnityEngine;
using MukJump.Core;

namespace MukJump.Player
{
    /// 핵심 메커닉: 먹방울이는 접지 상태에서 일정 주기마다 스스로 점프한다.
    /// 점프 방향은 밟고 있는 발판의 표면 노멀(기울기), 세기는 발판 길이가 결정한다.
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController))]
    public class AutoJump : MonoBehaviour
    {
        [Header("점프 주기")]
        [Tooltip("점프 정점부터 다음 자동 점프까지 충전되는 시간")]
        [SerializeField] float jumpIntervalSeconds = 1f;

        [Header("점프 궤적")]
        [SerializeField] float baseJumpSpeed = 12f;
        [Tooltip("기존 점프 궤적을 유지하면서 전체 점프 힘을 조절하는 배율")]
        [SerializeField, Min(1f)] float jumpStrengthMultiplier = 1.12f;
        [Tooltip("0 = 항상 수직 점프, 1 = 발판 노멀 방향 그대로")]
        [Range(0f, 1f)]
        [SerializeField] float normalInfluence = 0.7f;
        [Tooltip("이전 점프의 수평 관성을 다음 점프에 남기는 비율")]
        [Range(0f, 0.8f)]
        [SerializeField] float horizontalMomentumRetention = 0.28f;
        [Tooltip("평평한 발판에서도 완전히 수직으로만 반복되지 않게 하는 약한 좌우 이동")]
        [SerializeField, Min(0f)] float flatPlatformWanderSpeed = 0.35f;
        [SerializeField, Min(1f)] float maxHorizontalSpeed = 5.5f;

        [Header("발판 길이 → 점프력 보정")]
        [SerializeField] Vector2 platformLengthRange = new(1f, 5f);
        [SerializeField] Vector2 powerMultiplierRange = new(0.85f, 1.3f);

        Rigidbody2D rb;
        PlayerController player;
        float chargeTimer;
        bool hasLaunched;
        bool wasRising;
        bool chargeStarted;
        float wanderDirection;

        /// 첫 점프는 접지 중, 이후 점프는 정점부터 다음 점프를 준비한다 (HUD 게이지용).
        public bool IsCharging => player != null && (chargeStarted || (!hasLaunched && player.IsGrounded)) &&
                                  GameManager.Instance != null &&
                                  GameManager.Instance.State == GameState.Playing;
        public float ChargeRatio => Mathf.Clamp01(chargeTimer / jumpIntervalSeconds);

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
            wanderDirection = Random.value < 0.5f ? -1f : 1f;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing ||
                player == null || player.IsDead)
            {
                chargeTimer = 0f;
                chargeStarted = false;
                hasLaunched = false;
                wasRising = false;
                return;
            }

            float verticalSpeed = rb.linearVelocity.y;
            if (verticalSpeed > 0.1f)
            {
                // 자동 점프뿐 아니라 먹물방울 점프도 새로운 상승으로 인식한다.
                if (!wasRising)
                {
                    hasLaunched = true;
                    chargeStarted = false;
                    chargeTimer = 0f;
                }
                wasRising = true;
            }
            else if (wasRising)
            {
                // 상승에서 하강으로 바뀌는 정점부터 다음 점프 충전을 시작한다.
                wasRising = false;
                chargeStarted = true;
                chargeTimer = 0f;
            }

            if (!hasLaunched && player.IsGrounded)
                chargeStarted = true;

            if (chargeStarted)
                chargeTimer = Mathf.Min(jumpIntervalSeconds, chargeTimer + Time.deltaTime);

            // 공중에서는 충전만 유지하고, 착지한 순간 가득 찼다면 바로 점프한다.
            if (chargeStarted && player.IsGrounded && chargeTimer >= jumpIntervalSeconds)
                Jump();
        }

        void Jump()
        {
            chargeTimer = 0f;
            chargeStarted = false;
            hasLaunched = true;
            wasRising = true;

            Vector2 direction = Vector3.Slerp(Vector3.up, player.GroundNormal, normalInfluence).normalized;
            float power = baseJumpSpeed * jumpStrengthMultiplier * PowerMultiplier();
            float horizontal = direction.x * power + rb.linearVelocity.x * horizontalMomentumRetention;
            if (Mathf.Abs(direction.x) < 0.08f)
            {
                if (Random.value < 0.3f) wanderDirection = -wanderDirection;
                horizontal += wanderDirection * flatPlatformWanderSpeed;
            }

            horizontal = Mathf.Clamp(horizontal, -maxHorizontalSpeed, maxHorizontalSpeed);
            rb.linearVelocity = new Vector2(horizontal, direction.y * power);
        }

        float PowerMultiplier()
        {
            var platform = player.CurrentPlatform;
            if (platform == null) return 1f; // 시작 지형 등 기본 발판

            float t = Mathf.InverseLerp(platformLengthRange.x, platformLengthRange.y, platform.Length);
            return Mathf.Lerp(powerMultiplierRange.x, powerMultiplierRange.y, t);
        }
    }
}
