using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;

namespace MukJump.Player
{
    /// 먹방울이의 물리 상태: 접지 판정, 착지한 발판 추적, 추락 감지.
    /// 점프 자체는 AutoJump가 담당한다 (플레이어는 점프를 조작할 수 없음).
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("캐릭터가 카메라 하단 가장자리에 이만큼 걸치면 죽음 연출 시작")]
        [SerializeField] float deathEdgeMargin = 0.3f;
        [Tooltip("죽음 직후 잠깐 멈칫하는 시간 (마리오식 타격감)")]
        [SerializeField] float deathFreezeDuration = 0.25f;
        [Tooltip("멈칫 후 위로 튀어 오르는 속도")]
        [SerializeField] float deathPopSpeed = 16f;
        [Tooltip("정점을 지난 뒤 낙하 중력 배율 — 클수록 무겁게 뚝 떨어진다")]
        [SerializeField] float deathFallGravityMultiplier = 1.8f;
        [Tooltip("접촉 노멀의 y가 이 값 이상이어야 '발판 위'로 인정")]
        [SerializeField] float groundNormalMinY = 0.4f;

        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public PlatformCollider CurrentPlatform { get; private set; }

        Rigidbody2D rb;
        Camera cam;
        float camHalfHeight;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // 정지 상태에서 Rigidbody가 잠들면 충돌 콜백이 멈춰 접지 판정이 풀린다 → 잠들지 않게 유지
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void Start()
        {
            cam = Camera.main;
            camHalfHeight = cam.orthographicSize;
        }

        void FixedUpdate()
        {
            if (IsDead) return;

            // 접지 플래그는 매 물리 스텝 초기화 → OnCollisionStay2D가 다시 세운다
            IsGrounded = false;

            if (GameManager.Instance.State == GameState.Playing &&
                transform.position.y < cam.transform.position.y - camHalfHeight - deathEdgeMargin)
            {
                Die();
            }
        }

        /// 마리오식 죽음 연출: 멈칫 → 위로 폴짝 → 정점 후 무거운 중력으로 화면 밖까지 낙하
        void Die()
        {
            IsDead = true;
            IsGrounded = false;
            CurrentPlatform = null;

            foreach (var col in GetComponents<Collider2D>())
                col.enabled = false;

            GameManager.Instance.OnPlayerFell();
            StartCoroutine(DeathSequence());
        }

        System.Collections.IEnumerator DeathSequence()
        {
            float normalGravity = rb.gravityScale;

            // 1) 멈칫: 그 자리에 잠깐 정지
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            yield return new WaitForSeconds(deathFreezeDuration);

            // 2) 폴짝: 위로 튀어 오름
            rb.gravityScale = normalGravity;
            rb.linearVelocity = new Vector2(0f, deathPopSpeed);
            while (rb.linearVelocity.y > 0f)
                yield return null;

            // 3) 낙하: 평소보다 무거운 중력으로 뚝 떨어진다
            rb.gravityScale = normalGravity * deathFallGravityMultiplier;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (contact.normal.y < groundNormalMinY) continue;

                IsGrounded = true;
                GroundNormal = contact.normal;
                CurrentPlatform = collision.collider.GetComponentInParent<PlatformCollider>();
                return;
            }
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (CurrentPlatform != null &&
                collision.collider.GetComponentInParent<PlatformCollider>() == CurrentPlatform)
            {
                CurrentPlatform = null;
            }
        }
    }
}
