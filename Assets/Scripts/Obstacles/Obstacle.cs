using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;

namespace MukJump.Obstacles
{
    public enum ObstacleMotion
    {
        Static,
        Horizontal,
        Vertical,
    }

    /// 닿으면 플레이어를 사망시키는 원형 먹 가시 장애물.
    /// 이동형도 Transform만 움직이며 트리거이므로 발판 접지 판정에는 관여하지 않는다.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Obstacle : MonoBehaviour, IPoolableEntity
    {
        ObstacleMotion motion;
        Vector3 origin;
        float amplitude;
        float speed;
        float phase;
        SpriteRenderer spriteRenderer;
        CircleCollider2D trigger;
        ObstacleVisibilityView visibility;

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(ObstacleMotion newMotion, float newAmplitude, float newSpeed, float newPhase)
        {
            EnsureComponents();
            motion = newMotion;
            origin = transform.position;
            amplitude = newAmplitude;
            speed = newSpeed;
            phase = newPhase;
            spriteRenderer.enabled = true;
            trigger.enabled = true;
        }

        void Update()
        {
            if (motion == ObstacleMotion.Static || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing) return;

            float offset = Mathf.Sin(Time.time * speed + phase) * amplitude;
            transform.position = motion == ObstacleMotion.Horizontal
                ? origin + Vector3.right * offset
                : origin + Vector3.up * offset;
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
            motion = ObstacleMotion.Static;
            origin = transform.position;
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
            trigger.enabled = true;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            motion = ObstacleMotion.Static;
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            trigger.enabled = false;
            spriteRenderer.color = Color.white;
            spriteRenderer.enabled = false;
            visibility?.SetVisible(false);
            transform.localRotation = Quaternion.identity;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (trigger == null) trigger = GetComponent<CircleCollider2D>();
            if (visibility == null) visibility = GetComponent<ObstacleVisibilityView>();
        }
    }
}
