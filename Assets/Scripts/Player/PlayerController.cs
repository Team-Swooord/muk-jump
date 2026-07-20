using System;
using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;

namespace MukJump.Player
{
    /// <summary>
    /// 먹방울이 캐릭터 본체. 접지 판정, 발판과의 상호작용, 낙하 판정을 담당한다.
    /// 점프 "타이밍"은 AutoJump가, 점프 "힘"의 실제 적용은 이 스크립트가 담당한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public enum AnimState { Idle, Jumping, Landing, Falling }

        [Header("Jump Tuning")]
        [SerializeField] private float jumpVerticalForce = 9f;
        [Tooltip("발판 각도가 얼마나 수평 속도에 반영될지 (0=수직으로만 튐, 1=발판 각도 그대로 반영)")]
        [SerializeField, Range(0f, 1f)] private float platformAngleInfluence = 0.6f;

        [Header("Fall Detection")]
        [SerializeField] private LayerMask platformLayer;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private float cameraLowerPaddingWorldUnits = 1.5f;

        public bool IsGrounded { get; private set; }
        public AnimState CurrentAnim { get; private set; } = AnimState.Idle;

        /// <summary>마지막으로 접지한 발판의 표면 각도(도, 0=수평).</summary>
        public float LastPlatformAngleDeg { get; private set; }

        public event Action OnJumpStarted;
        public event Action OnLanded;
        public event Action OnFalling;

        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void Start()
        {
            GameManager.Instance?.ReportPlayerHeight(transform.position.y, GetLowestVisibleY());
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.ReportPlayerHeight(transform.position.y, GetLowestVisibleY());

            // 접지하지 않은 채 하강 중이면 낙하 상태로 전이 (발판을 완전히 놓친 경우)
            if (!IsGrounded && rb.velocity.y < -0.1f && CurrentAnim != AnimState.Falling)
            {
                SetAnim(AnimState.Falling);
                OnFalling?.Invoke();
                GameManager.Instance.NotifyFallingStarted();
            }
        }

        private float GetLowestVisibleY()
        {
            if (gameCamera == null) return float.NegativeInfinity;
            float halfHeight = gameCamera.orthographicSize;
            return gameCamera.transform.position.y - halfHeight - cameraLowerPaddingWorldUnits;
        }

        /// <summary>
        /// AutoJump가 타이밍이 되었을 때 호출한다. 접지 상태가 아니면 무시된다.
        /// </summary>
        public bool TryPerformJump()
        {
            if (!IsGrounded) return false;

            IsGrounded = false;

            // 마지막 발판 각도를 수평 속도로 환산 (오른쪽으로 기운 발판 = +x)
            float angleRad = LastPlatformAngleDeg * Mathf.Deg2Rad;
            float horizontal = Mathf.Sin(angleRad) * jumpVerticalForce * platformAngleInfluence;
            float vertical = jumpVerticalForce;

            rb.velocity = new Vector2(horizontal, vertical);

            SetAnim(AnimState.Jumping);
            OnJumpStarted?.Invoke();
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (((1 << collision.gameObject.layer) & platformLayer) == 0) return;
            if (rb.velocity.y > 0.1f) return; // 상승 중 옆면 충돌은 착지로 취급하지 않음

            ContactPoint2D contact = collision.GetContact(0);
            LastPlatformAngleDeg = Vector2.SignedAngle(Vector2.up, contact.normal);

            IsGrounded = true;
            SetAnim(AnimState.Landing);
            OnLanded?.Invoke();

            // PlatformDrawing 쪽에서 발판 소모/유지 정책을 결정하도록 알림
            collision.collider.GetComponent<DrawnPlatform>()?.NotifyLanded();
        }

        private void SetAnim(AnimState next)
        {
            CurrentAnim = next;
            // Animator 연동은 아트 애니메이션 준비되는 대로 여기서 트리거 설정
        }
    }
}
