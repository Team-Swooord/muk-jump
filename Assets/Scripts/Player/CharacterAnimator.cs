using UnityEngine;

namespace MukJump.Player
{
    /// 물리 상태(수직 속도)에 따라 점프 8프레임을 전환한다.
    /// 중력 아래에서 수직 속도는 이륙 직후 최대치에서 정점(0)을 지나 하강 최대치까지
    /// 단조 감소하므로, 속도 구간만으로 launch→rise→apex→fall→dive가 자연스럽게 이어진다.
    /// Animator 에셋 없이 코드로 직접 구동.
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(PlayerController))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("프레임 (muk_spritesheet: idle→crouch→launch→rise / apex→fall→dive→land)")]
        [SerializeField] Sprite idle;
        [SerializeField] Sprite crouch;
        [SerializeField] Sprite launch;
        [SerializeField] Sprite rise;
        [SerializeField] Sprite apex;
        [SerializeField] Sprite fall;
        [SerializeField] Sprite dive;
        [SerializeField] Sprite land;
        [Tooltip("죽음 포즈들 (X 눈) — 죽음 연출 동안 순환 재생 (허우적거리는 느낌)")]
        [SerializeField] Sprite[] deadFrames;
        [SerializeField] float deadFps = 10f;

        [Header("공중 상태 전환 속도 구간")]
        [Tooltip("수직 속도가 이보다 크면 도약(launch) 포즈")]
        [SerializeField] float highBand = 8f;
        [Tooltip("수직 속도 절대값이 이보다 작으면 정점(apex) 포즈")]
        [SerializeField] float apexBand = 2f;

        [Header("접지 상태 타이밍")]
        [Tooltip("점프 게이지가 이 비율을 넘으면 웅크림 포즈 (점프 예고)")]
        [SerializeField] float crouchChargeRatio = 0.85f;
        [Tooltip("착지 순간 몸이 눌리는 시간")]
        [SerializeField] float landDuration = 0.08f;

        SpriteRenderer sr;
        Rigidbody2D rb;
        PlayerController player;
        AutoJump jump;
        float landTimer;
        float deathTime;
        bool wasGrounded = true;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            player = GetComponent<PlayerController>();
            jump = GetComponent<AutoJump>();
        }

        void LateUpdate()
        {
            if (idle == null) return; // 프레임 미할당 시 기본 스프라이트 유지

            if (player.IsDead)
            {
                deathTime += Time.deltaTime;
                if (deadFrames != null && deadFrames.Length > 0)
                {
                    // 한 번만 재생하고 마지막 포즈에서 멈춘다
                    int frame = Mathf.Min((int)(deathTime * deadFps), deadFrames.Length - 1);
                    sr.sprite = deadFrames[frame];
                }
                return;
            }
            deathTime = 0f;

            if (!player.IsGrounded)
            {
                wasGrounded = false;
                sr.sprite = SpriteForVelocity(rb.linearVelocity.y);
                if (Mathf.Abs(rb.linearVelocity.x) > 0.25f)
                    sr.flipX = rb.linearVelocity.x < 0f;
                return;
            }

            if (!wasGrounded)
            {
                wasGrounded = true;
                landTimer = landDuration;
            }

            if (landTimer > 0f)
            {
                landTimer -= Time.deltaTime;
                sr.sprite = land;
                return;
            }

            bool preparingJump = jump != null && jump.IsCharging && jump.ChargeRatio >= crouchChargeRatio;
            sr.sprite = preparingJump ? crouch : idle;
        }

        Sprite SpriteForVelocity(float vy)
        {
            if (vy > highBand) return launch;
            if (vy > apexBand) return rise;
            if (vy > -apexBand) return apex;
            if (vy > -highBand) return fall;
            return dive;
        }
    }
}
