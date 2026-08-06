using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using System;

namespace MukJump.Player
{
    /// 먹방울이의 물리 상태: 접지 판정, 착지한 발판 추적, 추락 감지.
    /// 점프 자체는 AutoJump가 담당한다 (플레이어는 점프를 조작할 수 없음).
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("캐릭터가 카메라 하단 가장자리보다 이만큼 내려가면 추락 피해 처리")]
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
        [SerializeField, Min(0f)] float cloneSpawnGraceDuration = 1f;
        [Header("체력")]
        [Tooltip("체력 피해 뒤 겹친 장애물에 연속으로 맞지 않는 시간")]
        [SerializeField, Min(0f)] float damageHitGraceDuration = 0.55f;
        [Tooltip("접촉 노멀의 y가 이 값 이상이어야 '발판 위'로 인정")]
        [SerializeField] float groundNormalMinY = 0.4f;
        [Tooltip("화면 하단 추락에서 살아남았을 때 다시 튀어 오르는 목표 높이")]
        [SerializeField] float shieldRecoveryHeight = 35f;
        [Header("좌우 벽 트램펄린")]
        [Tooltip("화면 좌우 벽에 닿았을 때 안쪽으로 되튀는 최소 수평 속도")]
        [SerializeField, Min(0f)] float sideWallBounceSpeed = 3.2f;
        [Tooltip("벽 반동의 최대 수평 속도. 방향은 항상 화면 안쪽이다")]
        [SerializeField, Min(0f)] float sideWallBounceMaxSpeed = 4.2f;
        [Tooltip("첫 벽 충돌에서 무작위로 선택하는 작은 수직 반동 범위")]
        [SerializeField] Vector2 sideWallVerticalBounceRange = new(2.4f, 3.2f);
        [Tooltip("벽 반동이 반복 점프가 되지 않도록 수직 반동을 다시 허용하기까지의 시간")]
        [SerializeField, Min(0f)] float sideWallBounceCooldown = 0.45f;
        [Tooltip("첫 수직 반동을 접촉 유지 보정이 즉시 지우지 않는 시간")]
        [SerializeField, Min(0f)] float sideWallBounceRiseGrace = 0.12f;
        [Header("벽의 먹발")]
        [Tooltip("벽 비기가 붙은 직후 너무 빨리 떨어지지 않게 보장하는 최소 체류 시간")]
        [SerializeField, Min(0f)] float wallClingMinimumDuration = 0.22f;
        [Tooltip("벽점프 직후 같은 벽에 즉시 다시 붙지 않는 시간")]
        [SerializeField, Min(0f)] float wallRelatchDelay = 0.35f;
        [Tooltip("수평 이동 중 캐릭터가 시각적으로 기울어지는 최대 각도")]
        [SerializeField, Range(0f, 8f)] float maxVisualRollAngle = 3f;
        [Tooltip("현재 이동 방향의 기울기로 따라가는 속도")]
        [SerializeField, Min(0f)] float visualRollSpeed = 18f;
        [Tooltip("자동 점프 비행의 기본 최대 낙하 속도. 영구 성장은 이 값만 비례 조정")]
        [SerializeField, Min(1f)] float permanentGrowthFallSpeedLimit = 18f;
        [Header("드로잉 발판 접착")]
        [Tooltip("대각선 발판에 붙어 있을 때 접선 방향 속도를 남기는 비율")]
        [SerializeField, Range(0f, 1f)] float platformGrip = 0.42f;
        [Tooltip("발판에서 미끄러지지 않도록 표면 쪽으로 누르는 약한 힘")]
        [SerializeField, Min(0f)] float adhesionSpeed = 0.18f;

        public const int DefaultMaxHealth = 1;
        public const int MaximumHealth = 5;

        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public PlatformCollider CurrentPlatform { get; private set; }
        public bool HasShield { get; private set; }
        public bool IsInkDropBoosted { get; private set; }
        public bool IsRuntimeClone => isRuntimeClone;
        public bool IsAutomaticJumpInFlight => automaticJumpInFlight;
        public bool IsWallClinging { get; private set; }
        public bool CanAutomaticJumpFromCurrentSurface =>
            !IsWallClinging || Time.time >= wallClingReleaseAllowedAt;
        public int MaxHealth => Mathf.Clamp(
            DefaultMaxHealth + ActivePermanentGrowth.MaxHealthBonus,
            DefaultMaxHealth,
            MaximumHealth);
        public int CurrentHealth { get; private set; }
        /// 피격 횟수에 맞춘 시각 단계. 최대 체력이 늘어도 매 비치명 피격을 구분한다.
        /// 물리 크기는 바꾸지 않고 CharacterAnimator가 렌더 스프라이트만 키운다.
        public int DamageStage => Mathf.Max(0, MaxHealth - CurrentHealth);
        public float NormalGravityScale => normalGravityScale;
        public Rigidbody2D Body => rb;
        public Collider2D PrimaryCollider
        {
            get
            {
                if (primaryCollider == null)
                    primaryCollider = GetComponent<Collider2D>();
                return primaryCollider;
            }
        }
        public event Action ShieldConsumed;
        public event Action<int, int> HealthChanged;

        Rigidbody2D rb;
        Collider2D primaryCollider;
        Camera cam;
        float camHalfHeight;
        bool inkDropHasRisen;
        float normalGravityScale;
        float damageInvulnerableUntil;
        float apexGravityUntil;
        float fallBrakeUntil;
        float fallBrakeVelocityFloor;
        bool automaticJumpInFlight;
        ScreenSideWall clingingWall;
        float wallClingReleaseAllowedAt;
        float wallClingExpiresAt;
        float wallRelatchAllowedAt;
        bool wallClingConsumedThisFlight;
        float nextSideWallBounceAt;
        float sideWallRiseGraceUntil;
        [SerializeField, HideInInspector] bool isRuntimeClone;
        static DeathInkStainPool deathStainPool;

        static DeathInkStainPool DeathStainPool =>
            deathStainPool ??= new DeathInkStainPool(CreateDeathStainObject);

        PermanentGrowthRunSnapshot ActivePermanentGrowth =>
            RunGrowthController.Instance != null
                ? RunGrowthController.Instance.PermanentSnapshot
                : PermanentGrowthProfile.CreateRunSnapshot();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDeathStainPool()
        {
            // Domain Reload를 꺼도 이전 Play 세션의 managed 참조를 유지하지 않는다.
            deathStainPool = null;
        }

        static GameObject CreateDeathStainObject()
        {
            var stainObject = new GameObject("DeathInkStain (Pooled)");
            if (GameManager.Instance != null)
                stainObject.transform.SetParent(GameManager.Instance.transform, false);
            stainObject.AddComponent<SpriteRenderer>();
            stainObject.SetActive(false);
            return stainObject;
        }

        void OnEnable()
        {
            GameManager.Instance?.RegisterPlayer(this);
        }

        void OnDisable()
        {
            GameManager.Instance?.UnregisterPlayer(this);
        }

        /// 로비에서는 메뉴를 고르는 동안 캐릭터가 먼저 추락하지 않도록 고정한다.
        /// 시작 버튼을 누르면 씬에 준비된 영구 시작 발판 위에서 물리를 시작한다.
        public void BeginFromLobby()
        {
            ResetHealth();
            ResetWallTraversalState(true);
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
            // EditMode 테스트와 Play 중 도메인 재로드에서는 Awake 캐시보다
            // 런타임 복제 설정이 먼저 호출될 수 있으므로 의존성을 즉시 복구한다.
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            isRuntimeClone = true;
            ResetHealth();
            normalGravityScale = Mathf.Max(0.01f, sourceNormalGravityScale);
            rb.gravityScale = normalGravityScale;
            ResetWallTraversalState(false);
            IsGrounded = false;
            CurrentPlatform = null;
            GroundNormal = Vector2.up;
            damageInvulnerableUntil = Time.time +
                                      Mathf.Max(1f, cloneSpawnGraceDuration);
            rb.WakeUp();
        }

        /// 개발용 고도 이동. 접착과 속도를 정리해 순간이동 직후 물리 튕김을 막는다.
        public void DebugTeleportBy(Vector2 offset)
        {
            if (IsDead) return;
            ResetWallTraversalState(true);
            DetachFromPlatform();
            rb.position += offset;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            damageInvulnerableUntil = Time.time + 0.5f;
            rb.WakeUp();
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            primaryCollider = GetComponent<Collider2D>();
            CurrentHealth = MaxHealth;
            normalGravityScale = rb.gravityScale;
            rb.freezeRotation = true;
            // 구 Main 씬에 직렬화된 약한 2.4 반동도 현재 트램펄린 밸런스로 승격한다.
            sideWallBounceSpeed = Mathf.Max(3.2f, sideWallBounceSpeed);
            sideWallBounceMaxSpeed = Mathf.Max(sideWallBounceSpeed, sideWallBounceMaxSpeed);
            // 정지 상태에서 Rigidbody가 잠들면 충돌 콜백이 멈춰 접지 판정이 풀린다 → 잠들지 않게 유지
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void Start()
        {
            GameManager.Instance?.RegisterPlayer(this);
            DeathStainPool.Prewarm(maxDeathStains);
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

            MaintainWallCling();

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                // 드로잉 발판에 붙어 있을 때는 캐릭터의 머리가 표면 바깥쪽을 향하도록
                // 발판 노멀에 맞춘다. 공중에서는 기존처럼 이동 방향만 살짝 따라간다.
                float targetAngle = CurrentPlatform != null || IsWallClinging
                    ? Mathf.Atan2(GroundNormal.y, GroundNormal.x) * Mathf.Rad2Deg - 90f
                    : Mathf.Clamp(-rb.linearVelocity.x * 0.45f,
                        -maxVisualRollAngle, maxVisualRollAngle);
                rb.rotation = Mathf.MoveTowardsAngle(rb.rotation, targetAngle,
                    visualRollSpeed * Time.fixedDeltaTime);

                ApplyPermanentAirControl();
            }

            // 접지 플래그는 매 물리 스텝 초기화 → OnCollisionStay2D가 다시 세운다
            IsGrounded = IsWallClinging;

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
                HandleFallBelowView();
            }
        }

        /// 화면 아래 추락은 개체별 피해로 처리한다. 방어막과 개발용 무적은 체력을
        /// 보존하며, 체력이 남은 먹방울은 안전선으로 옮긴 뒤 즉시 다시 튀어 오른다.
        /// 장애물 전용 마지막 생존 비기는 추락에는 적용하지 않는다.
        void HandleFallBelowView()
        {
            var manager = GameManager.Instance;
            if (manager != null && manager.DebugInvincible)
            {
                RecoverFromFall();
                return;
            }

            if (ConsumeShield())
            {
                RecoverFromFall();
                return;
            }

            GameFeedbackController.Instance?.PlayHitStop();
            CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0)
            {
                Kill();
                return;
            }

            damageInvulnerableUntil = Mathf.Max(
                damageInvulnerableUntil,
                Time.time + EffectiveDamageHitGraceDuration);
            GetComponent<ItemEffectView>()?.PlayVitalityHit();
            RecoverFromFall();
            Vector3 feedbackPosition = rb != null
                ? new Vector3(rb.position.x, rb.position.y, transform.position.z)
                : transform.position;
            GameFeedbackController.Instance?.PlayDamageHit(feedbackPosition);
        }

        /// 방어막은 한 번의 피해만 막는 비중첩 효과다. 이미 보유 중이면 false를
        /// 반환해 다음 픽업이 소비되지 않도록 한다.
        public bool TryGrantShield()
        {
            if (IsDead || HasShield)
                return false;

            HasShield = true;
            return true;
        }

        /// 기존 장애물·테스트 호출부와의 호환을 유지하는 명령형 래퍼.
        public void GrantShield() => TryGrantShield();

        /// 장애물 피해. 실제로 처리한 접촉이면 true를 반환해 장애물이 스스로 사라지게 한다.
        public bool TakeHit()
        {
            if (IsDead) return false;
            // 일시정지·전환 중 같은 Physics2D 스텝에 예약된 충돌이 이어져도
            // 닫힌 게임 화면 뒤에서 피해가 적용되지 않게 한다.
            var manager = GameManager.Instance;
            if (manager != null && !manager.IsGameplayTicking) return false;
            if (IsInkDropBoosted) return false;
            if (Time.time < damageInvulnerableUntil) return false;
            GameFeedbackController.Instance?.PlayHitStop();
            if (manager != null && manager.DebugInvincible)
            {
                ApplyObstacleHitRecovery(shieldHitGraceDuration, false);
                return true;
            }
            if (ConsumeShield())
            {
                ApplyObstacleHitRecovery(shieldHitGraceDuration, false);
                return true;
            }
            if (CurrentHealth <= 1 &&
                RunGrowthController.Instance != null &&
                RunGrowthController.Instance.TrySurviveLethalObstacleHit(this))
            {
                CurrentHealth = 1;
                HealthChanged?.Invoke(CurrentHealth, MaxHealth);
                ApplyObstacleHitRecovery(0.8f, true, false);
                return true;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0)
                Kill();
            else
            {
                RunGrowthController growth = RunGrowthController.Instance;
                bool preserveMotion = growth != null &&
                                      growth.TryPreserveHitMotion();
                ApplyObstacleHitRecovery(
                    EffectiveDamageHitGraceDuration,
                    true,
                    preserveMotion);
            }
            return true;
        }

        float EffectiveDamageHitGraceDuration =>
            Mathf.Max(
                0f,
                damageHitGraceDuration +
                ActivePermanentGrowth.DamageGraceBonusSeconds);

        void ApplyObstacleHitRecovery(
            float graceSeconds,
            bool playInkPuff,
            bool preserveMotion = false)
        {
            if (IsDead) return;
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            if (rb == null) return;
            if (normalGravityScale <= 0f)
                normalGravityScale = Mathf.Max(0.01f, rb.gravityScale);

            damageInvulnerableUntil = Mathf.Max(
                damageInvulnerableUntil,
                Time.time + Mathf.Max(0f, graceSeconds));
            if (!preserveMotion)
            {
                DetachFromPlatform();
                Vector2 velocity = rb.linearVelocity;
                velocity.x *= ActivePermanentGrowth.HitHorizontalRetention;
                velocity.y = Mathf.Max(
                    velocity.y,
                    ActivePermanentGrowth.MinimumHitRebound);
                rb.linearVelocity = velocity;
            }
            rb.WakeUp();
            if (playInkPuff)
            {
                GetComponent<ItemEffectView>()?.PlayVitalityHit();
                GameFeedbackController.Instance?.PlayDamageHit(transform.position);
            }
        }

        /// 규칙형 성장·비기가 기존 속도와 발판 상태를 건드리지 않고 보호 시간만 늘린다.
        public void GrantObstacleProtection(float duration)
        {
            if (IsDead)
                return;
            damageInvulnerableUntil = Mathf.Max(
                damageInvulnerableUntil,
                Time.time + Mathf.Max(0f, duration));
        }

        public bool RestoreHealth(int amount)
        {
            if (IsDead || amount <= 0 || CurrentHealth >= MaxHealth)
                return false;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            return true;
        }

        public void ApplyApexGravityWindow(float duration, float gravityMultiplier)
        {
            if (IsDead || IsGrounded || CurrentPlatform != null)
                return;
            apexGravityUntil = Mathf.Max(
                apexGravityUntil,
                Time.time + Mathf.Max(0f, duration));
            if (EnsureBody())
                rb.gravityScale = normalGravityScale *
                                  Mathf.Clamp(gravityMultiplier, 0.1f, 1f);
        }

        void ApplyPermanentAirControl()
        {
            if (rb == null || !rb.simulated || rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            bool isNaturalAirborne =
                !IsGrounded && CurrentPlatform == null && !IsInkDropBoosted;
            if (isNaturalAirborne)
            {
                rb.gravityScale = Time.time < apexGravityUntil
                    ? normalGravityScale * 0.8f
                    : normalGravityScale;
            }

            // 접착된 대각선 발판의 하강 접선 속도나 먹물방울 상승을 낙하로
            // 오인하면 비기가 착지 중 소모된다. 낙하 제어는 자연 공중 상태에만 적용한다.
            if (!isNaturalAirborne)
                return;

            Vector2 velocity = rb.linearVelocity;
            if (automaticJumpInFlight && velocity.y < 0f)
            {
                float limit = permanentGrowthFallSpeedLimit *
                              ActivePermanentGrowth.MaximumFallSpeedMultiplier;
                velocity.y = Mathf.Max(velocity.y, -limit);
            }

            RunGrowthController growth = RunGrowthController.Instance;
            if (velocity.y < 0f && growth != null &&
                growth.TryUseLastFallBrake(this))
            {
                velocity.y *= 0.65f;
                fallBrakeVelocityFloor = velocity.y;
                fallBrakeUntil = Time.time + 0.45f;
            }
            else if (Time.time < fallBrakeUntil && velocity.y < fallBrakeVelocityFloor)
            {
                // 음수 속도를 0 이상으로 만들지 않아 위쪽 힘처럼 보이지 않게 한다.
                velocity.y = Mathf.Min(-0.01f, fallBrakeVelocityFloor);
            }
            rb.linearVelocity = velocity;
        }

        /// 먹물방울: 현재 위치에서 지정 높이까지 오르는 물리 점프 속도를 적용한다.
        public void LaunchToHeight(float height)
        {
            if (!EnsureBody()) return;
            GetComponent<AutoJump>()?.CancelForSpecialLaunch();
            automaticJumpInFlight = false;
            // 대각선 발판 접착 중에는 gravityScale이 0이므로 먼저 접착을 풀어야
            // 목표 높이에 필요한 점프 속도가 정상적으로 계산된다.
            DetachFromPlatform();
            float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
            float speed = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, height));
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed);
        }

        /// 먹물방울 점프는 상승이 끝날 때까지 장애물 피해를 받지 않는다.
        public void LaunchInkDrop(float height, bool playCameraImpulse = true)
        {
            IsInkDropBoosted = true;
            inkDropHasRisen = false;
            LaunchToHeight(height);
            if (playCameraImpulse)
                Camera.main?.GetComponent<CameraFollow>()?.PlayJumpImpulse(
                    transform, Mathf.Lerp(1f, 1.5f,
                        Mathf.InverseLerp(25f, 50f, height)));
        }

        /// AutoJump가 만든 상승만 정점·낙하 영구 성장의 대상으로 표시한다.
        public void BeginAutomaticJumpFlight()
        {
            if (!IsDead)
                automaticJumpInFlight = true;
        }

        void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        bool ConsumeShield()
        {
            if (!HasShield) return false;
            HasShield = false;
            damageInvulnerableUntil = Time.time + shieldHitGraceDuration;
            ShieldConsumed?.Invoke();
            GameFeedbackController.Instance?.PlayShieldBreak(transform.position);
            return true;
        }

        void RecoverFromFall()
        {
            if (cam == null || !EnsureBody()) return;
            float safeY = cam.transform.position.y - camHalfHeight + 0.8f;
            rb.position = new Vector2(rb.position.x, safeY);
            LaunchToHeight(shieldRecoveryHeight);
        }

        /// 추락 또는 장애물 충돌의 공통 사망 진입점.
        /// 캐릭터를 숨기고 먹 번짐이 퍼졌다 사라지는 연출을 재생한다.
        public void Kill()
        {
            if (IsDead) return;

            if (CurrentHealth != 0)
            {
                CurrentHealth = 0;
                HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
            IsDead = true;
            ClearWallCling(false);
            IsInkDropBoosted = false;
            IsGrounded = false;
            CurrentPlatform = null;

            foreach (var col in GetComponents<Collider2D>())
                col.enabled = false;

            bool isLastPlayer = GameManager.Instance == null ||
                                GameManager.Instance.NotifyPlayerDied(this);
            GameFeedbackController.Instance?.PlayDeath(
                transform.position, isLastPlayer);
            StartCoroutine(DeathSequence(isLastPlayer));
        }

        System.Collections.IEnumerator DeathSequence(bool isLastPlayer)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.simulated = false;

            var playerRenderer = GetComponent<SpriteRenderer>();
            // CharacterAnimator의 사망 프레임이 먹 번짐과 함께 실제로 보이도록
            // 연출이 끝날 때까지 본체 렌더러를 유지한다.
            if (playerRenderer != null) playerRenderer.enabled = true;

            if (deathSplashSprite != null)
            {
                // 죽은 분신 오브젝트가 정리되어도 한지 위 먹 자국은 월드에 남긴다.
                float spriteWidth = Mathf.Max(0.01f, deathSplashSprite.bounds.size.x);
                float finalScale = deathSplashWorldWidth / spriteWidth;
                // 캐릭터와 아이템 아래, 드로잉 발판 위에 종이 얼룩처럼 남는다.
                DeathInkStainPool.Lease stain = DeathStainPool.Show(
                    deathSplashSprite,
                    transform.position,
                    Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-18f, 18f)),
                    finalScale * 0.18f,
                    2,
                    maxDeathStains);
                float elapsed = 0f;
                while (elapsed < deathSplashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / deathSplashDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    float scale = Mathf.Lerp(finalScale * 0.18f, finalScale, eased);
                    // 용량 초과로 같은 인스턴스가 다음 죽음에 재사용됐다면
                    // 이전 코루틴이 새 자국의 크기를 덮어쓰지 않는다.
                    GameObject currentStain = stain.GameObject;
                    if (currentStain != null)
                        currentStain.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
            }
            else
                yield return new WaitForSeconds(deathSplashDuration);

            if (playerRenderer != null) playerRenderer.enabled = false;

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
            var sideWall = collision.collider.GetComponent<ScreenSideWall>();
            if (IsWallClinging && sideWall != null && sideWall == clingingWall)
            {
                MaintainWallCling();
                return;
            }
            // 첫 반동의 짧은 상승은 보존하되 이후 접촉이 계속되면 수평으로 떼어 내고,
            // 급경사 먹선과 벽 사이에서 새 상승분을 반복 축적하지 못하게 한다.
            if (sideWall != null && rb != null)
            {
                float inwardDirection = sideWall.IsLeft ? 1f : -1f;
                rb.linearVelocity = ResolveSideWallEscapeVelocity(
                    rb.linearVelocity,
                    inwardDirection,
                    sideWallBounceSpeed,
                    Time.time < sideWallRiseGraceUntil);
            }

            var platform = collision.collider.GetComponentInParent<PlatformCollider>();
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (platform != null)
                {
                    // 풍맥 발판은 아래에서 통과한다. Effector 경계에서 발생할 수 있는
                    // 아래·옆면 접촉도 착지나 풍맥 효과로 처리하지 않는다.
                    if (platform.IsOneWayPlatform &&
                        contact.normal.y < groundNormalMinY)
                        continue;
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
            ClearWallCling(false);
            IsGrounded = false;
            CurrentPlatform = null;
            GroundNormal = Vector2.up;
            if (!EnsureBody()) return;
            rb.gravityScale = normalGravityScale;
            rb.WakeUp();
        }

        bool EnsureBody()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            if (rb == null) return false;
            if (normalGravityScale <= 0f)
                normalGravityScale = Mathf.Max(0.01f, rb.gravityScale);
            return true;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead) return;

            var platform = collision.collider.GetComponentInParent<PlatformCollider>();
            bool hasTopContact = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < groundNormalMinY) continue;
                hasTopContact = true;
                break;
            }

            if (hasTopContact && platform != null && platform.TryUseWindCurrent(this))
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.LaunchSwarmInkDrop(this, 36f);
                else
                    LaunchInkDrop(36f);
                GetComponent<InkDropJumpVfx>()?.Play();
                GameFeedbackController.Instance?.ShowZone("풍맥 상승", "바람길이 먹방울을 밀어 올립니다");
                return;
            }

            bool isSpecialPlatform = platform != null &&
                                     platform.IsOneWayPlatform;
            bool landed = hasTopContact || (platform != null && !isSpecialPlatform);
            if (landed)
            {
                if (IsWallClinging)
                    ClearWallCling(true);
                automaticJumpInFlight = false;
                wallClingConsumedThisFlight = false;
                GameFeedbackController.Instance?.PlayLanding(transform.position,
                    Mathf.Abs(collision.relativeVelocity.y));
                bool drawnPlatform = platform != null &&
                                     platform.IsTemporaryDrawnPlatform;
                GetComponent<AutoJump>()?.NotifyLanding(drawnPlatform);
                if (hasTopContact)
                    GameManager.Instance?.NotifyPlayerLanded(this, platform);
            }

            var sideWall = collision.collider.GetComponent<ScreenSideWall>();
            if (sideWall == null) return;

            float inwardDirection = sideWall.IsLeft ? 1f : -1f;
            if (TryBeginWallCling(sideWall, inwardDirection))
            {
                GameFeedbackController.Instance?.PlayWallHit(transform.position, inwardDirection);
                return;
            }

            if (Time.time < nextSideWallBounceAt)
            {
                rb.linearVelocity = ResolveSideWallEscapeVelocity(
                    rb.linearVelocity,
                    inwardDirection,
                    sideWallBounceSpeed,
                    Time.time < sideWallRiseGraceUntil);
                return;
            }

            float verticalRoll = GameplayRandom.Value(GameplayRandomStream.Player);
            rb.linearVelocity = ResolveSideWallBounceVelocity(
                rb.linearVelocity,
                inwardDirection,
                sideWallBounceSpeed,
                sideWallBounceMaxSpeed,
                sideWallVerticalBounceRange,
                verticalRoll);
            nextSideWallBounceAt = Time.time + sideWallBounceCooldown;
            sideWallRiseGraceUntil = Time.time + sideWallBounceRiseGrace;
            GameFeedbackController.Instance?.PlayWallHit(transform.position, inwardDirection);
        }

        /// 벽 충돌 한 번을 화면 안쪽의 짧은 트램펄린 반동으로 바꾼다. 수평 방향은
        /// 항상 예측 가능하게 안쪽이고, 수직 높이만 좁은 범위에서 달라진다.
        static Vector2 ResolveSideWallBounceVelocity(
            Vector2 currentVelocity,
            float inwardDirection,
            float minimumBounceSpeed,
            float maximumBounceSpeed,
            Vector2 verticalBounceRange,
            float verticalSample01)
        {
            float minimum = Mathf.Max(0f, minimumBounceSpeed);
            float maximum = Mathf.Max(minimum, maximumBounceSpeed);
            float bounceSpeed = Mathf.Clamp(
                Mathf.Max(minimum, Mathf.Abs(currentVelocity.x) * 0.7f),
                minimum,
                maximum);
            float minimumVertical = Mathf.Max(
                0f,
                Mathf.Min(verticalBounceRange.x, verticalBounceRange.y));
            float maximumVertical = Mathf.Max(
                minimumVertical,
                Mathf.Max(verticalBounceRange.x, verticalBounceRange.y));
            float fallTransfer = Mathf.Clamp(
                Mathf.Max(0f, -currentVelocity.y) * 0.12f,
                0f,
                0.8f);
            float verticalSpeed = Mathf.Min(
                4f,
                Mathf.Lerp(
                    minimumVertical,
                    maximumVertical,
                    Mathf.Clamp01(verticalSample01)) + fallTransfer);
            return new Vector2(
                Mathf.Sign(inwardDirection) * bounceSpeed,
                verticalSpeed);
        }

        /// 재접촉 중에는 수직 에너지를 새로 만들지 않고 화면 안쪽 분리만 보장한다.
        /// 첫 반동 보호 시간이 끝난 뒤의 양수 속도는 0으로 잘라 무한 벽타기를 막는다.
        static Vector2 ResolveSideWallEscapeVelocity(
            Vector2 currentVelocity,
            float inwardDirection,
            float minimumBounceSpeed,
            bool preserveRise)
        {
            float inward = Mathf.Sign(inwardDirection);
            float horizontalSpeed = currentVelocity.x * inward >= minimumBounceSpeed
                ? Mathf.Abs(currentVelocity.x)
                : Mathf.Max(0f, minimumBounceSpeed);
            return new Vector2(
                inward * horizontalSpeed,
                preserveRise ? currentVelocity.y : Mathf.Min(currentVelocity.y, 0f));
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (IsWallClinging &&
                collision.collider.GetComponent<ScreenSideWall>() == clingingWall)
                ClearWallCling(true);
            if (CurrentPlatform != null &&
                collision.collider.GetComponentInParent<PlatformCollider>() == CurrentPlatform)
            {
                DetachFromPlatform();
            }
        }

        bool TryBeginWallCling(ScreenSideWall wall, float inwardDirection)
        {
            GameManager game = GameManager.Instance;
            if (wall == null || IsDead || IsWallClinging ||
                !ActivePermanentGrowth.HasWallCling ||
                IsInkDropBoosted || !automaticJumpInFlight ||
                rb.linearVelocity.y > -0.1f ||
                Time.time < wallRelatchAllowedAt ||
                wallClingConsumedThisFlight ||
                game == null || game.State != GameState.Playing ||
                !game.TryGetSwarmAnchor(
                    out PlayerController representative,
                    out _) ||
                representative != this)
                return false;

            IsWallClinging = true;
            clingingWall = wall;
            wallClingConsumedThisFlight = true;
            wallClingReleaseAllowedAt = Time.time + wallClingMinimumDuration;
            wallClingExpiresAt = Time.time + Mathf.Max(
                wallClingMinimumDuration,
                ActivePermanentGrowth.WallClingDuration);
            GroundNormal = new Vector2(inwardDirection, 0f);
            CurrentPlatform = null;
            IsGrounded = true;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.WakeUp();
            return true;
        }

        void MaintainWallCling()
        {
            if (!IsWallClinging || rb == null)
                return;
            if (IsDead || !ActivePermanentGrowth.HasWallCling ||
                Time.time >= wallClingExpiresAt)
            {
                float inward = GroundNormal.x >= 0f ? 1f : -1f;
                ClearWallCling(true);
                if (!IsDead)
                    rb.linearVelocity = new Vector2(
                        inward * sideWallBounceSpeed,
                        rb.linearVelocity.y);
                return;
            }

            IsGrounded = true;
            CurrentPlatform = null;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        /// AutoJump는 벽 노멀을 먼저 복사한 뒤 이 메서드로 접착을 풀고 안쪽으로 뛴다.
        public void ReleaseWallClingForAutomaticJump()
        {
            if (!IsWallClinging)
                return;
            wallRelatchAllowedAt = Time.time + wallRelatchDelay;
            IsWallClinging = false;
            clingingWall = null;
            IsGrounded = false;
            CurrentPlatform = null;
            if (EnsureBody())
            {
                rb.gravityScale = normalGravityScale;
                rb.WakeUp();
            }
        }

        void ClearWallCling(bool restoreGravity)
        {
            bool wasClinging = IsWallClinging;
            IsWallClinging = false;
            clingingWall = null;
            wallClingReleaseAllowedAt = 0f;
            wallClingExpiresAt = 0f;
            if (wasClinging)
            {
                IsGrounded = false;
                CurrentPlatform = null;
                GroundNormal = Vector2.up;
                wallRelatchAllowedAt = Mathf.Max(
                    wallRelatchAllowedAt,
                    Time.time + wallRelatchDelay);
            }
            if (restoreGravity && EnsureBody())
                rb.gravityScale = normalGravityScale;
        }

        void ResetWallTraversalState(bool restoreGravity)
        {
            ClearWallCling(restoreGravity);
            wallClingConsumedThisFlight = false;
            wallRelatchAllowedAt = 0f;
            nextSideWallBounceAt = 0f;
            sideWallRiseGraceUntil = 0f;
        }
    }
}
