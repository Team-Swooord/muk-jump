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
        [Tooltip("카메라 하단에서 이만큼 더 내려가면 추락 처리")]
        [SerializeField] float fallMargin = 3f;
        [Tooltip("접촉 노멀의 y가 이 값 이상이어야 '발판 위'로 인정")]
        [SerializeField] float groundNormalMinY = 0.4f;

        public bool IsGrounded { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public PlatformCollider CurrentPlatform { get; private set; }

        Camera cam;
        float camHalfHeight;

        void Start()
        {
            cam = Camera.main;
            camHalfHeight = cam.orthographicSize;
        }

        void FixedUpdate()
        {
            // 접지 플래그는 매 물리 스텝 초기화 → OnCollisionStay2D가 다시 세운다
            IsGrounded = false;

            if (GameManager.Instance.State == GameState.Playing &&
                transform.position.y < cam.transform.position.y - camHalfHeight - fallMargin)
            {
                GameManager.Instance.OnPlayerFell();
            }
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
