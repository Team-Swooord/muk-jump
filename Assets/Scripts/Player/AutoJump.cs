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
        [Tooltip("접지 후 이 시간이 지나면 자동 점프")]
        [SerializeField] float jumpInterval = 1.6f;

        [Header("점프 궤적")]
        [SerializeField] float baseJumpSpeed = 12f;
        [Tooltip("0 = 항상 수직 점프, 1 = 발판 노멀 방향 그대로")]
        [Range(0f, 1f)]
        [SerializeField] float normalInfluence = 0.7f;

        [Header("발판 길이 → 점프력 보정")]
        [SerializeField] Vector2 platformLengthRange = new(1f, 5f);
        [SerializeField] Vector2 powerMultiplierRange = new(0.85f, 1.3f);

        Rigidbody2D rb;
        PlayerController player;
        float chargeTimer;

        /// 접지 중이며 다음 점프를 준비하고 있는가 (HUD 게이지용)
        public bool IsCharging => player.IsGrounded && GameManager.Instance.State == GameState.Playing;
        public float ChargeRatio => Mathf.Clamp01(chargeTimer / jumpInterval);

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (!IsCharging)
            {
                chargeTimer = 0f;
                return;
            }

            chargeTimer += Time.deltaTime;
            if (chargeTimer >= jumpInterval)
                Jump();
        }

        /// 로비에서 시작 터치 시 즉시 점프 — 착지하면 그 자리가 게임 시작점이 되는 연출.
        /// 이 점프에서는 시작 발판을 남겨둔다 (착지할 곳이 있어야 하므로)
        public void JumpNow()
        {
            if (player.IsGrounded)
                Jump(consumeStartPlatform: false);
        }

        void Jump(bool consumeStartPlatform = true)
        {
            chargeTimer = 0f;

            Vector2 direction = Vector3.Slerp(Vector3.up, player.GroundNormal, normalInfluence).normalized;
            float power = baseJumpSpeed * PowerMultiplier();

            // 첫 발판(시작 지형)은 최초로 그 위에서 자동 점프하는 순간 사라진다 —
            // 이후로는 반드시 직접 그린 발판으로만 진행해야 한다
            if (consumeStartPlatform &&
                player.CurrentPlatform != null && player.CurrentPlatform.IsStartPlatform)
                player.CurrentPlatform.Despawn();

            rb.linearVelocity = direction * power;
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
