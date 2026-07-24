using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using System;
using System.Collections.Generic;

namespace MukJump.Player
{
    /// 먹방울이의 물리 상태: 접지 판정, 착지한 발판 추적, 추락 감지.
    /// 점프 자체는 AutoJump가 담당한다 (플레이어는 점프를 조작할 수 없음).
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("캐릭터가 카메라 하단 가장자리에 이만큼 걸치면 죽음 연출 시작")]
        [SerializeField] float deathEdgeMargin = 0.3f;
        [Header("사망 먹 번짐 연출")]
        [SerializeField] Sprite deathSplashSprite;
        [SerializeField, Min(0.1f)] float deathSplashDuration = 0.65f;
        [SerializeField, Min(0.1f)] float deathSplashWorldWidth = 2.6f;
        [Tooltip("한 화면에 누적할 수 있는 먹 사망 자국 수")]
        [SerializeField, Min(1)] int maxDeathStains = 20;
        [Tooltip("방어막 소모 직후 겹친 장애물에 같은 프레임으로 다시 맞는 것을 막는 시간")]
        [SerializeField, Min(0f)] float shieldHitGraceDuration = 0.35f;
        [Tooltip("새 분신이 장애물 위에 생성되어 즉사하지 않도록 보호하는 시간")]
        [SerializeField, Min(0f)] float cloneSpawnGraceDuration = 0.6f;
        [Tooltip("접촉 노멀의 y가 이 값 이상이어야 '발판 위'로 인정")]
        [SerializeField] float groundNormalMinY = 0.4f;
        [Tooltip("먹 방어막으로 추락을 막았을 때 다시 튀어 오르는 목표 높이")]
        [SerializeField] float shieldRecoveryHeight = 35f;
        [Tooltip("화면 좌우 벽에 닿았을 때 안쪽으로 되튀는 최소 수평 속도")]
        [SerializeField, Min(0f)] float sideWallBounceSpeed = 2.4f;
        [Tooltip("수평 이동 중 캐릭터가 시각적으로 기울어지는 최대 각도")]
        [SerializeField, Range(0f, 8f)] float maxVisualRollAngle = 3f;
        [Tooltip("현재 이동 방향의 기울기로 따라가는 속도")]
        [SerializeField, Min(0f)] float visualRollSpeed = 18f;
        [Header("드로잉 발판 접착")]
        [Tooltip("대각선 발판에 붙어 있을 때 접선 방향 속도를 남기는 비율")]
        [SerializeField, Range(0f, 1f)] float platformGrip = 0.42f;
        [Tooltip("발판에서 미끄러지지 않도록 표면 쪽으로 누르는 약한 힘")]
        [SerializeField, Min(0f)] float adhesionSpeed = 0.18f;

        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public PlatformCollider CurrentPlatform { get; private set; }
        public bool HasShield { get; private set; }
        public bool IsInkDropBoosted { get; private set; }
        public float NormalGravityScale => normalGravityScale;
        public event Action ShieldConsumed;

        Rigidbody2D rb;
        Camera cam;
        float camHalfHeight;
        bool inkDropHasRisen;
        float normalGravityScale;
        float damageInvulnerableUntil;
        static readonly Queue<GameObject> deathStains = new();

        void OnEnable()
        {
            GameManager.Instance?.RegisterPlayer(this);
        }

        /// 로비에서는 시작선을 그리는 동안 캐릭터가 먼저 추락하지 않도록 그 자리에 고정한다.
        /// 선이 완성되면 현재 위치에서 물리를 시작하므로 아래에 그린 선만 첫 발판이 된다.
        public void BeginFromLobby()
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            IsGrounded = false;
            CurrentPlatform = null;
            rb.WakeUp();
        }

        /// 접착 중인 원본을 복제하면 Rigidbody의 현재 중력 0까지 복사되므로,
        /// 분신에는 원본이 기억하는 정상 중력을 별도로 전달한다.
        public void ConfigureAsClone(float sourceNormalGravityScale)
        {
            normalGravityScale = Mathf.Max(0.01f, sourceNormalGravityScale);
            rb.gravityScale = normalGravityScale;
            IsGrounded = false;
            CurrentPlatform = null;
            GroundNormal = Vector2.up;
            damageInvulnerableUntil = Time.time + cloneSpawnGraceDuration;
            rb.WakeUp();
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            normalGravityScale = rb.gravityScale;
            rb.freezeRotation = true;
            // 정지 상태에서 Rigidbody가 잠들면 충돌 콜백이 멈춰 접지 판정이 풀린다 → 잠들지 않게 유지
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void Start()
        {
            GameManager.Instance?.RegisterPlayer(this);
            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[MukJump] MainCamera를 찾을 수 없어 추락 판정을 비활성화합니다.", this);
                return;
            }
            camHalfHeight = cam.orthographicSize;

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        void FixedUpdate()
        {
            if (IsDead) return;

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                // 드로잉 발판에 붙어 있을 때는 캐릭터의 머리가 표면 바깥쪽을 향하도록
                // 발판 노멀에 맞춘다. 공중에서는 기존처럼 이동 방향만 살짝 따라간다.
                float targetAngle = CurrentPlatform != null
                    ? Mathf.Atan2(GroundNormal.y, GroundNormal.x) * Mathf.Rad2Deg - 90f
                    : Mathf.Clamp(-rb.linearVelocity.x * 0.45f,
                        -maxVisualRollAngle, maxVisualRollAngle);
                rb.rotation = Mathf.MoveTowardsAngle(rb.rotation, targetAngle,
                    visualRollSpeed * Time.fixedDeltaTime);
            }

            // 접지 플래그는 매 물리 스텝 초기화 → OnCollisionStay2D가 다시 세운다
            IsGrounded = false;

            if (IsInkDropBoosted)
            {
                if (rb.linearVelocity.y > 0.1f)
                    inkDropHasRisen = true;
                else if (inkDropHasRisen)
                    IsInkDropBoosted = false;
            }

            if (cam != null && GameManager.Instance != null &&
                GameManager.Instance.State == GameState.Playing &&
                transform.position.y < cam.transform.position.y - camHalfHeight - deathEdgeMargin)
            {
                if (GameManager.Instance.DebugInvincible)
                    RecoverFromFall();
                else if (ConsumeShield())
                    RecoverFromFall();
                else
                    Kill();
            }
        }

        public void GrantShield() => HasShield = true;

        /// 장애물 피해. 방어막이 있으면 1회 소모하고 작은 반동만 준다.
        public void TakeHit()
        {
            if (IsDead) return;
            if (IsInkDropBoosted) return;
            if (Time.time < damageInvulnerableUntil) return;
            if (GameManager.Instance != null && GameManager.Instance.DebugInvincible)
            {
                damageInvulnerableUntil = Time.time + shieldHitGraceDuration;
                LaunchToHeight(12f);
                return;
            }
            if (ConsumeShield())
            {
                damageInvulnerableUntil = Time.time + shieldHitGraceDuration;
                LaunchToHeight(12f);
                return;
            }
            Kill();
        }

        /// 먹물방울: 현재 위치에서 지정 높이까지 오르는 물리 점프 속도를 적용한다.
        public void LaunchToHeight(float height)
        {
            // 대각선 발판 접착 중에는 gravityScale이 0이므로 먼저 접착을 풀어야
            // 목표 높이에 필요한 점프 속도가 정상적으로 계산된다.
            DetachFromPlatform();
            float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            float speed = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height));
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed);
        }

        /// 먹물방울 점프는 상승이 끝날 때까지 장애물 피해를 받지 않는다.
        public void LaunchInkDrop(float height)
        {
            IsInkDropBoosted = true;
            inkDropHasRisen = false;
            LaunchToHeight(height);
        }

        bool ConsumeShield()
        {
            if (!HasShield) return false;
            HasShield = false;
            ShieldConsumed?.Invoke();
            return true;
        }

        void RecoverFromFall()
        {
            float safeY = cam.transform.position.y - camHalfHeight + 0.8f;
            rb.position = new Vector2(rb.position.x, safeY);
            LaunchToHeight(shieldRecoveryHeight);
        }

        /// 추락 또는 장애물 충돌의 공통 사망 진입점.
        /// 캐릭터를 숨기고 먹 번짐이 퍼졌다 사라지는 연출을 재생한다.
        public void Kill()
        {
            if (IsDead) return;

            IsDead = true;
            IsInkDropBoosted = false;
            IsGrounded = false;
            CurrentPlatform = null;

            foreach (var col in GetComponents<Collider2D>())
                col.enabled = false;

            bool isLastPlayer = GameManager.Instance == null ||
                                GameManager.Instance.NotifyPlayerDied(this);
            StartCoroutine(DeathSequence(isLastPlayer));
        }

        System.Collections.IEnumerator DeathSequence(bool isLastPlayer)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.simulated = false;

            var playerRenderer = GetComponent<SpriteRenderer>();
            if (playerRenderer != null) playerRenderer.enabled = false;

            if (deathSplashSprite != null)
            {
                // 죽은 분신 오브젝트가 정리되어도 한지 위 먹 자국은 월드에 남긴다.
                var splashObject = new GameObject("DeathInkStain");
                splashObject.transform.position = transform.position;
                splashObject.transform.rotation =
                    Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-18f, 18f));
                var splashRenderer = splashObject.AddComponent<SpriteRenderer>();
                splashRenderer.sprite = deathSplashSprite;
                // 캐릭터와 아이템 아래, 드로잉 발판 위에 종이 얼룩처럼 남는다.
                splashRenderer.sortingOrder = 2;

                float spriteWidth = Mathf.Max(0.01f, deathSplashSprite.bounds.size.x);
                float finalScale = deathSplashWorldWidth / spriteWidth;
                float elapsed = 0f;
                while (elapsed < deathSplashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / deathSplashDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    float scale = Mathf.Lerp(finalScale * 0.18f, finalScale, eased);
                    splashObject.transform.localScale = Vector3.one * scale;
                    yield return null;
                }

                deathStains.Enqueue(splashObject);
                while (deathStains.Count > Mathf.Max(1, maxDeathStains))
                {
                    var oldest = deathStains.Dequeue();
                    if (oldest != null) Destroy(oldest);
                }
            }
            else
                yield return new WaitForSeconds(deathSplashDuration);

            // 마지막 캐릭터는 게임오버 씬이 유지하므로 숨긴 채 남기고,
            // 먹분신이 살아 있으면 죽은 개체만 정리한다.
            if (!isLastPlayer) Destroy(gameObject);
        }

        void OnDestroy()
        {
            GameManager.Instance?.UnregisterPlayer(this);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            var platform = collision.collider.GetComponentInParent<PlatformCollider>();
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (platform != null)
                {
                    // 먹물방울 상승 중에는 방금 떨어져 나온 대각선 발판이 같은 물리
                    // 스텝에서 다시 캐릭터를 붙잡아 점프 속도를 덮지 못하게 한다.
                    if (IsInkDropBoosted) return;
                    // 실제 드로잉 발판은 가파른 대각선도 스파이더처럼 붙는다.
                    // 이미 표면 바깥으로 점프 중이면 다시 붙잡지 않는다.
                    if (Vector2.Dot(rb.linearVelocity, contact.normal) > 0.2f) continue;
                    AttachToDrawnPlatform(contact.normal, platform);
                    return;
                }

                if (contact.normal.y < groundNormalMinY) continue;

                IsGrounded = true;
                GroundNormal = contact.normal;
                CurrentPlatform = null;
                return;
            }
        }

        void AttachToDrawnPlatform(Vector2 normal, PlatformCollider platform)
        {
            normal.Normalize();
            Vector2 tangent = new(-normal.y, normal.x);
            float tangentVelocity = Vector2.Dot(rb.linearVelocity, tangent) * platformGrip;
            rb.linearVelocity = tangent * tangentVelocity - normal * adhesionSpeed;
            rb.gravityScale = 0f;
            IsGrounded = true;
            GroundNormal = normal;
            CurrentPlatform = platform;
            rb.rotation = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
        }

        void DetachFromPlatform()
        {
            IsGrounded = false;
            CurrentPlatform = null;
            GroundNormal = Vector2.up;
            rb.gravityScale = normalGravityScale;
            rb.WakeUp();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead || collision.collider.GetComponent<ScreenSideWall>() == null) return;

            float inwardDirection = transform.position.x >= collision.transform.position.x ? 1f : -1f;
            float bounceSpeed = Mathf.Max(sideWallBounceSpeed, Mathf.Abs(rb.linearVelocity.x) * 0.55f);
            rb.linearVelocity = new Vector2(inwardDirection * bounceSpeed, rb.linearVelocity.y);
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (CurrentPlatform != null &&
                collision.collider.GetComponentInParent<PlatformCollider>() == CurrentPlatform)
            {
                DetachFromPlatform();
            }
        }
    }
}
