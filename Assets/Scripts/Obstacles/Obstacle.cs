using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;

namespace MukJump.Obstacles
{
    public enum ObstacleKind
    {
        Spike,
        ChildDragon,
    }

    /// 닿으면 플레이어를 사망시키는 원형 먹 가시 장애물.
    /// 좌우 이동은 kinematic body가 담당하며 트리거이므로 발판 접지 판정에는 관여하지 않는다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class Obstacle : MonoBehaviour, IPoolableEntity
    {
        Vector2 origin;
        float amplitude;
        float speed;
        float phase;
        SpriteRenderer spriteRenderer;
        CircleCollider2D circleTrigger;
        CapsuleCollider2D capsuleTrigger;
        Rigidbody2D body;
        ObstacleVisibilityView visibility;

        public ObstacleKind Kind { get; private set; }

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(float newAmplitude, float newSpeed, float newPhase,
            ObstacleKind kind = ObstacleKind.Spike)
        {
            EnsureComponents();
            body.position = transform.position;
            origin = body.position;
            amplitude = newAmplitude;
            speed = newSpeed;
            phase = newPhase;
            Kind = kind;
            body.simulated = true;
            spriteRenderer.enabled = true;
            circleTrigger.enabled = kind == ObstacleKind.Spike;
            capsuleTrigger.enabled = kind == ObstacleKind.ChildDragon;
            spriteRenderer.flipX = kind == ObstacleKind.ChildDragon &&
                                   Mathf.Cos(phase) > 0f;
        }

        void FixedUpdate()
        {
            if (speed <= 0f || GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking) return;

            float offset = Mathf.Sin(Time.fixedTime * speed + phase) * amplitude;
            body.MovePosition(origin + Vector2.right * offset);
            if (Kind == ObstacleKind.ChildDragon)
            {
                float horizontalDirection = Mathf.Cos(Time.fixedTime * speed + phase);
                if (Mathf.Abs(horizontalDirection) > 0.05f)
                    // 원본 용의 머리는 왼쪽을 향한다.
                    spriteRenderer.flipX = horizontalDirection > 0f;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                player.TakeHit();
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            body.position = transform.position;
            origin = body.position;
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = true;
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;
            Kind = ObstacleKind.Spike;
            // Configure가 종류별 판정을 선택하기 전에는 ghost trigger가 없어야 한다.
            circleTrigger.enabled = false;
            capsuleTrigger.enabled = false;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Kind = ObstacleKind.Spike;
            circleTrigger.enabled = false;
            capsuleTrigger.enabled = false;
            spriteRenderer.color = Color.white;
            spriteRenderer.enabled = false;
            spriteRenderer.flipX = false;
            visibility?.DisableLegacyDecorations();
            transform.localRotation = Quaternion.identity;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (circleTrigger == null) circleTrigger = GetComponent<CircleCollider2D>();
            if (capsuleTrigger == null)
            {
                capsuleTrigger = GetComponent<CapsuleCollider2D>();
                if (capsuleTrigger == null)
                    capsuleTrigger = gameObject.AddComponent<CapsuleCollider2D>();
                capsuleTrigger.direction = CapsuleDirection2D.Horizontal;
                capsuleTrigger.isTrigger = true;
            }
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (visibility == null) visibility = GetComponent<ObstacleVisibilityView>();

            circleTrigger.isTrigger = true;
            capsuleTrigger.isTrigger = true;
            capsuleTrigger.direction = CapsuleDirection2D.Horizontal;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }
}
