using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;

namespace MukJump.Obstacles
{
    /// 닿으면 플레이어를 사망시키는 원형 먹 가시 장애물.
    /// 좌우 이동은 kinematic body가 담당하며 트리거이므로 발판 접지 판정에는 관여하지 않는다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public class Obstacle : MonoBehaviour, IPoolableEntity
    {
        Vector2 origin;
        float amplitude;
        float speed;
        float phase;
        SpriteRenderer spriteRenderer;
        CircleCollider2D trigger;
        Rigidbody2D body;
        ObstacleVisibilityView visibility;

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(float newAmplitude, float newSpeed, float newPhase)
        {
            EnsureComponents();
            body.position = transform.position;
            origin = body.position;
            amplitude = newAmplitude;
            speed = newSpeed;
            phase = newPhase;
            body.simulated = true;
            spriteRenderer.enabled = true;
            trigger.enabled = true;
        }

        void FixedUpdate()
        {
            if (speed <= 0f || GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking) return;

            float offset = Mathf.Sin(Time.fixedTime * speed + phase) * amplitude;
            body.MovePosition(origin + Vector2.right * offset);
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
            trigger.enabled = true;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            trigger.enabled = false;
            spriteRenderer.color = Color.white;
            spriteRenderer.enabled = false;
            visibility?.DisableLegacyDecorations();
            transform.localRotation = Quaternion.identity;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (trigger == null) trigger = GetComponent<CircleCollider2D>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (visibility == null) visibility = GetComponent<ObstacleVisibilityView>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }
}
