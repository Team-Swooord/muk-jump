using System;
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
        Sprite[] animationFrames;
        float animationFrameSeconds;
        float animationElapsed;
        int animationFrameIndex;
        bool consumed;

        public ObstacleKind Kind { get; private set; }
        public int AnimationFrameCount =>
            animationFrames != null ? animationFrames.Length : 0;
        public int CurrentAnimationFrameIndex => animationFrameIndex;
        public event Action<Obstacle> ReleaseRequested;

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
            consumed = false;
            body.simulated = true;
            spriteRenderer.enabled = true;
            circleTrigger.enabled = kind == ObstacleKind.Spike;
            capsuleTrigger.enabled = kind == ObstacleKind.ChildDragon;
            spriteRenderer.flipX = kind == ObstacleKind.ChildDragon &&
                                   Mathf.Cos(phase) > 0f;
        }

        /// 풀 오브젝트 자체를 교체하지 않고 SpriteRenderer 프레임만 순환한다.
        public void ConfigureSpriteAnimation(Sprite[] frames, float frameSeconds)
        {
            EnsureComponents();
            animationFrames = frames != null && frames.Length > 1
                ? frames
                : null;
            animationFrameSeconds = Mathf.Max(0.04f, frameSeconds);
            animationElapsed = 0f;
            animationFrameIndex = 0;
            if (frames != null && frames.Length > 0 && frames[0] != null)
                spriteRenderer.sprite = frames[0];
        }

        void Update()
        {
            if (animationFrames == null || animationFrames.Length <= 1) return;
            if (GameManager.Instance != null &&
                !GameManager.Instance.IsGameplayTicking) return;
            AdvanceSpriteAnimation(Time.deltaTime);
        }

        void AdvanceSpriteAnimation(float deltaTime)
        {
            if (animationFrames == null || animationFrames.Length <= 1) return;
            animationElapsed += Mathf.Max(0f, deltaTime);
            int steps = Mathf.FloorToInt(animationElapsed / animationFrameSeconds);
            if (steps <= 0) return;

            animationElapsed -= steps * animationFrameSeconds;
            animationFrameIndex =
                (animationFrameIndex + steps) % animationFrames.Length;
            var next = animationFrames[animationFrameIndex];
            if (next != null) spriteRenderer.sprite = next;
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
            if (consumed) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.TakeHit()) return;

            // 한 번 유효하게 피격시킨 장애물은 같은 물리 스텝의 다른 콜라이더나
            // 분신까지 연쇄 타격하지 않도록 즉시 판정을 끄고 풀 반환을 요청한다.
            consumed = true;
            circleTrigger.enabled = false;
            capsuleTrigger.enabled = false;
            ReleaseRequested?.Invoke(this);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            body.position = transform.position;
            origin = body.position;
            amplitude = 0f;
            speed = 0f;
            phase = 0f;
            consumed = false;
            ReleaseRequested = null;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = true;
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;
            ResetSpriteAnimation();
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
            consumed = false;
            ReleaseRequested = null;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Kind = ObstacleKind.Spike;
            circleTrigger.enabled = false;
            capsuleTrigger.enabled = false;
            spriteRenderer.color = Color.white;
            spriteRenderer.enabled = false;
            spriteRenderer.flipX = false;
            ResetSpriteAnimation();
            visibility?.DisableLegacyDecorations();
            transform.localRotation = Quaternion.identity;
        }

        void ResetSpriteAnimation()
        {
            animationFrames = null;
            animationFrameSeconds = 0f;
            animationElapsed = 0f;
            animationFrameIndex = 0;
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
