using System;
using System.Collections.Generic;
using MukJump.Drawing;
using MukJump.Player;
using MukJump.Items;
using UnityEngine;

namespace MukJump.Core
{
    /// 영구 성장 프로필을 판 시작에 고정하고 판 단위 공용 패시브 상태를 소유한다.
    /// 모든 효과는 저장된 성장 나무의 소유 노드와 장착 비기에서만 온다.
    [DisallowMultipleComponent]
    public sealed class RunGrowthController : MonoBehaviour
    {
        public static RunGrowthController Instance { get; private set; }

        /// 영구 생존 계보의 장착 비기는 먹분신별이 아니라 먹떼 전체가 한 번만 공유한다.
        public bool LastBreathAvailable { get; private set; }
        public int SafetyJumpProgress { get; private set; }
        public PermanentGrowthRunSnapshot PermanentSnapshot { get; private set; } =
            PermanentGrowthRunSnapshot.Empty;
        public event Action RunReset;
        [SerializeField] InkSkinId skinSnapshot;
        public InkSkinId SkinSnapshot => InkSkinCatalog.Normalize(skinSnapshot);
        public bool GoldSnapshot { get; private set; }

        GameManager manager;
        float stableHitReadyAt;
        float lastFallBrakeReadyAt;
        PlatformCollider activeSafetyPlatform;
        readonly List<PlayerController> skinPlayers = new(GameManager.MaxLivingPlayers);
        bool skinStartShieldGranted;
        [SerializeField] bool skinStartBoostGranted;
        [SerializeField] float skinSkillCooldown;
        [SerializeField] float maskStrikeCooldown;
        public const float MaskStrikeCooldownSeconds = 30f;
        public float MaskStrikeCooldownRemaining => maskStrikeCooldown;
        public const float WebClimbCooldownSeconds = 30f;
        [SerializeField] float webClimbCooldown = WebClimbCooldownSeconds;
        public float WebClimbCooldownRemaining => webClimbCooldown;
        public const float SkinSkillCooldownSeconds = 20f;
        public const float SkinSkillJumpHeight = 25f;
        public float SkinSkillCooldownRemaining => skinSkillCooldown;
        // HUD는 별도 타이머를 돌리지 않고 이번 판에 고정된 능력의 실제 시계를 읽는다.
        public float EquippedAbilityCooldownDuration => InkSkinChart.Cooldown(SkinSnapshot);
        public float EquippedAbilityCooldownRemaining => SkinSnapshot switch {
            InkSkinId.WindFan => SkinSkillCooldownRemaining,
            InkSkinId.YakshaMask => MaskStrikeCooldownRemaining,
            InkSkinId.SpiderMask => WebClimbCooldownRemaining,
            _ => 0f };
        public bool HasActiveSkinSkill => SkinSnapshot == InkSkinId.WindFan;
        public bool CanUseActiveSkinSkill => HasActiveSkinSkill && skinSkillCooldown <= 0 &&
            CanTickSkinAbility && OriginalSkinPlayer() is { IsInkDropBoosted: false };
        bool CanTickSkinAbility => manager != null && manager.IsGameplayTicking &&
            MobileApplicationLifecycle.IsApplicationActive &&
            MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true;

        void Update()
        {
            TickSkinCooldown(Time.deltaTime);
            TryGrantSkinStartBoost();
            if (SkinSnapshot == InkSkinId.SpiderMask && webClimbCooldown <= 0 && CanTickSkinAbility &&
                GetComponent<InkWebClimb>()?.TryStart(OriginalSkinPlayer()) == true) webClimbCooldown = EquippedAbilityCooldownDuration;
        }

        void TickSkinCooldown(float seconds)
        {
            if (CanTickSkinAbility && float.IsFinite(seconds) && seconds > 0)
            {
                skinSkillCooldown = Mathf.Max(0, skinSkillCooldown - seconds);
                maskStrikeCooldown = Mathf.Max(0, maskStrikeCooldown - seconds);
                webClimbCooldown = Mathf.Max(0, webClimbCooldown - seconds);
            }
        }

        PlayerController OriginalSkinPlayer()
        {
            if (manager == null) return null;
            manager.GetLivingPlayersNonAlloc(skinPlayers);
            foreach (var player in skinPlayers)
                if (player != null && player.isActiveAndEnabled && !player.IsDead && !player.IsRuntimeClone &&
                    player.Body != null && player.Body.simulated) return player;
            return null;
        }

        // 전환·첫 안내 뒤 실제 물리가 재개된 첫 프레임에 한 번만 먹방울을 받는다.
        public bool TryGrantSkinStartBoost()
        {
            if (skinStartBoostGranted || SkinSnapshot != InkSkinId.BondKnot || !CanTickSkinAbility) return false;
            var player = OriginalSkinPlayer();
            if (player == null || !ItemEffect.Apply(ItemType.InkDrop, player)) return false;
            skinStartBoostGranted = true;
            manager.NotifySkinItemCollected(true);
            manager.NotifyAchievementPickup(true, 0);
            return true;
        }

        // 구름 장화는 맵의 쉬터 발판에 본체가 착지할 때만 발동한다.
        // 상승 상태가 같은 착지의 다중 Collider 콜백을 막으며, 실제 아이템 수집으로 세지 않는다.
        public bool TryTriggerCloudBootsLanding(PlayerController player, PlatformCollider platform)
        {
            if (SkinSnapshot != InkSkinId.CloudBoots || !CanTickSkinAbility ||
                platform == null || !platform.IsMapRestPlatform || platform.IsGrowthSafetyPlatform ||
                player == null || !player.isActiveAndEnabled || player.IsDead || player.IsRuntimeClone ||
                player.IsInkDropBoosted || player.Body == null || !player.Body.simulated)
                return false;
            return ItemEffect.Apply(ItemType.InkDrop, player);
        }

        // 적의 실제 충돌 처리에서 피해보다 먼저 호출한다. 분신·광고 부활로 충전하지 않는다.
        public bool TryUseMaskStrike(PlayerController player, Vector3 enemyPosition)
        {
            if (SkinSnapshot != InkSkinId.YakshaMask || maskStrikeCooldown > 0 || !CanTickSkinAbility ||
                player == null || !player.isActiveAndEnabled || player.IsDead || player.IsRuntimeClone ||
                player.Body == null || !player.Body.simulated)
                return false;
            maskStrikeCooldown = EquippedAbilityCooldownDuration;
            GetComponent<InkSkinStrikeVfx>()?.Play(player, enemyPosition);
            return true;
        }

        // 스킨 비기는 픽업이 아니다. 수집 업적·아이템 비행 연쇄를 늘리지 않는다.
        public bool TryUseActiveSkinSkill()
        {
            if (!CanUseActiveSkinSkill) return false;
            var player = OriginalSkinPlayer();
            if (player == null) return false;
            skinSkillCooldown = EquippedAbilityCooldownDuration;
            player.LaunchInkDrop(SkinSkillJumpHeight);
            player.GetComponent<InkDropJumpVfx>()?.Play();
            return true;
        }

        void Awake()
        {
            BindManager();
        }

        // Play 중 스크립트 재컴파일 뒤에도 static을 복구한다.
        void OnEnable()
        {
            Instance = this;
            BindManager();

            // Play 중 스크립트 재컴파일 뒤에는 StateChanged 델리게이트와
            // 비직렬화 스냅샷이 사라질 수 있다. 이미 진행 중인 판이라면
            // 저장된 성장값만 다시 붙이고 공용 쿨다운은 리셋하지 않는다.
            if (manager != null && manager.State == GameState.Playing)
                PermanentSnapshot = PermanentGrowthProfile.CreateRunSnapshot();
        }

        void OnDisable()
        {
            UnbindManager();
            if (Instance == this)
                Instance = null;
        }

        /// 숨 고르기 결실. 본체 먹방울이만 한 판에 한 번 체력 1로 부활한다.
        /// 장애물과 화면 하단 추락이 같은 사용권을 공유하며 분신은 소비할 수 없다.
        public bool TryReviveOriginalPlayer(PlayerController player)
        {
            if (player == null || player.IsDead ||
                player.IsRuntimeClone ||
                !LastBreathAvailable ||
                manager == null ||
                manager.State != GameState.Playing)
                return false;

            LastBreathAvailable = false;
            return true;
        }

        /// 구 호출부 호환. 새 의미는 마지막 생존자가 아니라 본체 전용 1회 부활이다.
        public bool TrySurviveLethalObstacleHit(PlayerController player) =>
            TryReviveOriginalPlayer(player);

        /// 실제 분신 사망 전이에서만 호출한다. 씬 정리·촬영용 제거는 보상으로 세지 않는다.
        public bool NotifySkinCloneDeath(PlayerController clone, AnalyticsDeathCause cause)
        {
            if (SkinSnapshot != InkSkinId.SpiritHalo || clone == null || !clone.IsRuntimeClone || !clone.IsDead ||
                cause is not (AnalyticsDeathCause.Fall or AnalyticsDeathCause.Obstacle) ||
                manager == null || !manager.IsGameplayTicking || !MobileApplicationLifecycle.IsApplicationActive)
                return false;
            manager.GetLivingPlayersNonAlloc(skinPlayers);
            foreach (var player in skinPlayers)
                if (player.isActiveAndEnabled && !player.IsRuntimeClone)
                    return player.RestoreHealth(1);
            return false;
        }

        /// 새 판의 체력 초기화 직후 본체에게만 지급한다. 광고 부활·분신 생성은 재충전하지 않는다.
        public bool TryGrantSkinStartShield(PlayerController player)
        {
            if (skinStartShieldGranted || SkinSnapshot != InkSkinId.TurtleShell ||
                player == null || !player.isActiveAndEnabled || player.IsDead || player.IsRuntimeClone ||
                manager == null || manager.State != GameState.Playing)
                return false;
            skinStartShieldGranted = true;
            return player.TryGrantShield();
        }

        /// 제거된 구 피격 보존 비기의 호환 경로. 현재 성장 스냅샷에서는 활성화되지 않는다.
        public bool TryPreserveHitMotion()
        {
            if (!PermanentSnapshot.HasStableHit ||
                Time.time < stableHitReadyAt)
                return false;
            stableHitReadyAt = Time.time + 12f;
            return true;
        }

        /// 먹떼 대표의 일반 1차 자동점프만 센다. 분신마다 세면 한 프레임에 최대
        /// 24개 발판이 생길 수 있으므로 런 전체가 하나의 5회 카운터를 공유한다.
        public bool NotifyPrimaryAutomaticJump(
            PlayerController player,
            Vector2 launchVelocity)
        {
            if (player == null || player.IsDead ||
                !PermanentSnapshot.HasSafetyPlatform ||
                manager == null || manager.State != GameState.Playing ||
                !IsSwarmRepresentative(player))
                return false;

            SafetyJumpProgress = Mathf.Min(5, SafetyJumpProgress + 1);
            if (SafetyJumpProgress < 5)
                return false;

            // 이전 발판은 약속한 6초를 온전히 유지한다. 그동안 완성된 다음 5회는
            // 진행도 5에 대기시키고, 기존 발판이 사라진 뒤 첫 대표 점프에서 생성한다.
            if (activeSafetyPlatform != null)
                return false;

            SafetyJumpProgress = 0;
            activeSafetyPlatform = SpawnSafetyPlatform(player, launchVelocity);
            if (activeSafetyPlatform == null)
                SafetyJumpProgress = 5;
            return activeSafetyPlatform != null;
        }

        /// 카메라·난이도와 같은 하위 중앙 먹떼 대표를 사용한다. 선두만 구조 비기를
        /// 독점해 화면 밖으로 더 멀어지거나 안전 발판이 카메라 밖에 생기는 일을 막는다.
        bool IsSwarmRepresentative(PlayerController player)
        {
            return manager != null && player != null &&
                   manager.TryGetSwarmAnchor(
                       out PlayerController representative,
                       out _) &&
                   representative == player;
        }

        static PlatformCollider SpawnSafetyPlatform(
            PlayerController player,
            Vector2 launchVelocity)
        {
            float gravity = Mathf.Abs(
                Physics2D.gravity.y * Mathf.Max(0.01f, player.NormalGravityScale));
            float verticalSpeed = Mathf.Max(0f, launchVelocity.y);
            float timeToApex = verticalSpeed / Mathf.Max(0.01f, gravity);
            float rise = verticalSpeed * verticalSpeed /
                         (2f * Mathf.Max(0.01f, gravity));

            float centerX = player.transform.position.x +
                            launchVelocity.x * timeToApex * 0.65f;
            Camera worldCamera = Camera.main;
            if (worldCamera != null)
            {
                float left = worldCamera.ViewportToWorldPoint(
                    new Vector3(0.12f, 0.5f, 0f)).x;
                float right = worldCamera.ViewportToWorldPoint(
                    new Vector3(0.88f, 0.5f, 0f)).x;
                centerX = Mathf.Clamp(centerX, left, right);
            }

            // 낮고 비스듬한 점프에서도 발판을 실제 정점 위에 놓지 않는다.
            float catchRise = Mathf.Min(
                Mathf.Clamp(rise * 0.68f, 0.25f, 7.2f),
                rise * 0.82f);
            float centerY = player.transform.position.y + catchRise;
            const float width = 3.4f;
            var points = new List<Vector2>(7);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float x = Mathf.Lerp(centerX - width * 0.5f,
                    centerX + width * 0.5f, t);
                float curve = -0.08f * Mathf.Pow(t * 2f - 1f, 2f);
                points.Add(new Vector2(x, centerY + curve));
            }
            return PlatformCollider.SpawnGrowthSafetyPlatform(points, 6f);
        }

        /// 마지막 생존자가 하단에 진입했을 때 위로 밀지 않고 낙하만 늦춘다.
        public bool TryUseLastFallBrake(PlayerController player)
        {
            if (player == null ||
                player.IsDead ||
                !PermanentSnapshot.HasLastFallBrake ||
                manager == null ||
                manager.LivingPlayerCount != 1 ||
                Time.time < lastFallBrakeReadyAt)
                return false;
            Camera worldCamera = Camera.main;
            if (worldCamera == null ||
                worldCamera.WorldToViewportPoint(player.transform.position).y > 0.25f)
                return false;

            lastFallBrakeReadyAt = Time.time + 14f;
            return true;
        }

        void BindManager()
        {
            var nextManager = GetComponent<GameManager>();
            if (nextManager == null)
                nextManager = GameManager.Instance;

            if (manager != null && manager != nextManager)
                manager.StateChanged -= HandleStateChanged;
            manager = nextManager;
            if (manager != null)
            {
                // 같은 참조여도 도메인 리로드 뒤 델리게이트만 유실될 수 있다.
                manager.StateChanged -= HandleStateChanged;
                manager.StateChanged += HandleStateChanged;
            }
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            // 광고 부활은 GameOver -> Playing이지만 같은 판이다. 성장 스냅샷과
            // 먹선 장부·무한 먹 상태는 새 판인 Lobby -> Playing에서만 초기화한다.
            if (current == GameState.Playing && previous == GameState.Lobby)
                ResetRun();
        }

        /// 옵션의 개발용 연출 시나리오가 사용자 저장을 수정하지 않고 이 판의
        /// 성장 빌드만 교체할 때 사용한다. 정상 출시 빌드에서는 항상 거부한다.
        public bool DebugApplyPermanentSnapshot(
            PermanentGrowthRunSnapshot snapshot)
        {
            if (!GameManager.DebugToolsAvailable ||
                snapshot == null ||
                manager == null ||
                manager.State != GameState.Playing)
                return false;

            PermanentSnapshot = snapshot;
            ResetSharedRunState();
            RunReset?.Invoke();
            return true;
        }

        void ResetRun()
        {
            PermanentSnapshot = PermanentGrowthProfile.CreateRunSnapshot();
            // 광고 부활과 분신 생성은 새 판이 아니다. 로비에서 시작할 때만 고정한다.
            skinSnapshot = InkSkinPreferences.Equipped;
            GoldSnapshot = InkPremiumAppearance.IsEquipped;
            ResetSharedRunState();
            RunReset?.Invoke();
        }

        void ResetSharedRunState()
        {
            LastBreathAvailable = PermanentSnapshot.HasLastBreath || SkinSnapshot is InkSkinId.PhoenixCrown or InkSkinId.AngelWings;
            skinStartShieldGranted = false;
            skinStartBoostGranted = false;
            skinSkillCooldown = 0;
            maskStrikeCooldown = 0;
            webClimbCooldown = InkSkinChart.Cooldown(InkSkinId.SpiderMask);
            stableHitReadyAt = float.NegativeInfinity;
            lastFallBrakeReadyAt = float.NegativeInfinity;
            SafetyJumpProgress = 0;
            if (activeSafetyPlatform != null)
                Destroy(activeSafetyPlatform.gameObject);
            activeSafetyPlatform = null;
        }
    }
}
