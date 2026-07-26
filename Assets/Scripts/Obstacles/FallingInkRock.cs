using System.Collections;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Obstacles
{
    public enum FallingInkRockState
    {
        Warning,
        Falling,
        Resolved,
    }

    /// 화면 상단에서 예고한 뒤 수직 낙하하여 플레이어 또는 드로잉 발판과 충돌하는 낙묵석.
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class FallingInkRock : MonoBehaviour, IPoolableEntity
    {
        [Header("예고")]
        [Min(0.05f), SerializeField] float warningDuration = 0.9f;
        [Range(0f, 1f), SerializeField] float warningMinAlpha = 0.48f;
        [Range(0f, 1f), SerializeField] float warningMaxAlpha = 0.9f;
        [Min(0.01f), SerializeField] float warningMinScale = 0.94f;
        [Min(0.01f), SerializeField] float warningMaxScale = 1.08f;

        [Header("낙하")]
        [Min(0f), SerializeField] float initialFallSpeed = 4f;
        [Min(0f), SerializeField] float maxFallSpeed = 9f;
        [Min(0f), SerializeField] float fallAcceleration = 8f;
        [Min(0.1f), SerializeField] float maxLifetime = 8f;
        [Min(0f), SerializeField] float cleanupBelowViewport = 1f;
        [SerializeField] LayerMask collisionMask;

        [Header("소멸")]
        [Min(0f), SerializeField] float dissolveDuration = 0.12f;
        [Min(1f), SerializeField] float dissolveScale = 1.12f;

        public FallingInkRockState State { get; private set; } = FallingInkRockState.Warning;
        public bool IsResolved => State == FallingInkRockState.Resolved;

        SpriteRenderer spriteRenderer;
        Rigidbody2D body;
        CircleCollider2D hitbox;
        FallingInkRockSpawner owner;
        Camera worldCamera;
        Color baseColor;
        Vector3 baseScale;
        float warningElapsed;
        float lifetimeElapsed;
        float fallSpeed;

        public void Initialize(FallingInkRockSpawner spawner, Camera camera, LayerMask hitMask,
            float warningSeconds, float startSpeed, float maximumSpeed, float acceleration,
            float lifetime)
        {
            owner = spawner;
            worldCamera = camera;
            collisionMask = hitMask;
            warningDuration = warningSeconds;
            initialFallSpeed = startSpeed;
            maxFallSpeed = maximumSpeed;
            fallAcceleration = acceleration;
            maxLifetime = lifetime;

            baseScale = transform.localScale;
            baseColor = spriteRenderer.color;
            warningElapsed = 0f;
            lifetimeElapsed = 0f;
            fallSpeed = 0f;
            State = FallingInkRockState.Warning;
            spriteRenderer.enabled = true;
            hitbox.enabled = false;
            body.simulated = false;
        }

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<CircleCollider2D>();
            baseColor = spriteRenderer.color;
            baseScale = transform.localScale;
        }

        void Update()
        {
            if (State == FallingInkRockState.Resolved) return;

            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                ResolveImmediately();
                return;
            }

            lifetimeElapsed += Time.deltaTime;
            if (lifetimeElapsed >= maxLifetime)
            {
                ResolveImmediately();
                return;
            }

            if (State == FallingInkRockState.Warning)
                UpdateWarning();
            else if (worldCamera != null)
            {
                float cameraBottom = worldCamera.ViewportToWorldPoint(Vector3.zero).y;
                if (transform.position.y + WorldRadius < cameraBottom - cleanupBelowViewport)
                    ResolveImmediately();
            }
        }

        void FixedUpdate()
        {
            if (State != FallingInkRockState.Falling) return;

            fallSpeed = Mathf.Min(maxFallSpeed,
                fallSpeed + fallAcceleration * Time.fixedDeltaTime);
            float distance = fallSpeed * Time.fixedDeltaTime;
            Vector2 start = body.position;

            // 얇은 EdgeCollider2D를 빠르게 통과하지 않도록 이번 물리 스텝 전체를 검사한다.
            RaycastHit2D hit = Physics2D.CircleCast(start, WorldRadius, Vector2.down,
                distance, collisionMask);
            if (hit.collider != null && ResolveCollision(hit.collider)) return;

            body.MovePosition(start + Vector2.down * distance);
        }

        void UpdateWarning()
        {
            warningElapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(warningElapsed / warningDuration);
            float wave = 0.5f + 0.5f * Mathf.Sin(normalized * Mathf.PI * 4f);
            Color color = baseColor;
            color.a = Mathf.Lerp(warningMinAlpha, warningMaxAlpha, wave);
            spriteRenderer.color = color;
            transform.localScale = baseScale * Mathf.Lerp(warningMinScale, warningMaxScale, wave);

            if (warningElapsed < warningDuration) return;

            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            transform.localScale = baseScale;
            fallSpeed = initialFallSpeed;
            body.simulated = true;
            hitbox.enabled = true;
            State = FallingInkRockState.Falling;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (State != FallingInkRockState.Falling) return;
            ResolveCollision(other);
        }

        bool ResolveCollision(Collider2D other)
        {
            if (State != FallingInkRockState.Falling) return false;

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                BeginResolve(false);
                player.TakeHit();
                return true;
            }

            var platform = other.GetComponentInParent<PlatformCollider>();
            if (platform == null || !platform.BreakFromHazard()) return false;

            BeginResolve(true);
            return true;
        }

        void BeginResolve(bool dissolve)
        {
            if (!TryEnterResolvedState()) return;

            if (dissolve && dissolveDuration > 0f)
                StartCoroutine(Dissolve());
            else
                ReleaseOrDestroy();
        }

        bool TryEnterResolvedState()
        {
            if (State == FallingInkRockState.Resolved) return false;
            State = FallingInkRockState.Resolved;
            hitbox.enabled = false;
            body.simulated = false;
            return true;
        }

        IEnumerator Dissolve()
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;
            Vector3 startScale = transform.localScale;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);
                Color color = startColor;
                color.a = 1f - t;
                spriteRenderer.color = color;
                transform.localScale = Vector3.LerpUnclamped(startScale,
                    startScale * dissolveScale, t);
                yield return null;
            }
            ReleaseOrDestroy();
        }

        public void ResolveImmediately()
        {
            BeginResolve(false);
        }

        void ReleaseOrDestroy()
        {
            if (owner != null)
                owner.Release(this);
            else
                Destroy(gameObject);
        }

        public void OnPoolAcquire()
        {
            EnsureComponents();
            StopAllCoroutines();
            owner = null;
            worldCamera = null;
            warningElapsed = 0f;
            lifetimeElapsed = 0f;
            fallSpeed = 0f;
            State = FallingInkRockState.Warning;
            spriteRenderer.enabled = true;
            spriteRenderer.color = baseColor;
            transform.localScale = baseScale;
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            hitbox.enabled = false;
        }

        public void OnPoolRelease()
        {
            EnsureComponents();
            StopAllCoroutines();
            State = FallingInkRockState.Resolved;
            hitbox.enabled = false;
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            spriteRenderer.color = baseColor;
            spriteRenderer.enabled = false;
            transform.localScale = baseScale;
            GetComponent<ObstacleVisibilityView>()?.SetVisible(false);
            owner = null;
            worldCamera = null;
            warningElapsed = 0f;
            lifetimeElapsed = 0f;
            fallSpeed = 0f;
        }

        void EnsureComponents()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (hitbox == null) hitbox = GetComponent<CircleCollider2D>();
        }

        float WorldRadius => hitbox.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        void OnDestroy()
        {
            owner?.NotifyRemoved(this);
        }

        void OnValidate()
        {
            warningDuration = Mathf.Max(0.05f, warningDuration);
            warningMaxAlpha = Mathf.Max(warningMinAlpha, warningMaxAlpha);
            warningMaxScale = Mathf.Max(warningMinScale, warningMaxScale);
            maxFallSpeed = Mathf.Max(initialFallSpeed, maxFallSpeed);
            maxLifetime = Mathf.Max(warningDuration + 0.1f, maxLifetime);
            dissolveDuration = Mathf.Max(0f, dissolveDuration);
            dissolveScale = Mathf.Max(1f, dissolveScale);
        }
    }
}
