using System;
using System.Collections.Generic;
using MukJump.Drawing;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 한 판 동안만 유지되는 성장 선택 종류.
    public enum GrowthUpgradeType
    {
        Vitality,
        JumpPower,
        InkCapacity,
        InkRecovery,
        PlatformLifetime,
        PlatformSlots,
        StrokeGuard,
        ItemFortune,
    }

    /// 성장 두루마리의 세션 진행도를 한 곳에서 소유한다.
    /// 체력은 먹분신별 목숨을 곱하지 않고 먹떼 전체가 공유하는 완충 횟수로 계산한다.
    [DisallowMultipleComponent]
    public sealed class RunGrowthController : MonoBehaviour
    {
        public const int MaxVitalityLevel = 3;
        public const int MaxJumpLevel = 5;
        public const int MaxInkCapacityLevel = 4;
        public const int MaxInkRecoveryLevel = 4;
        public const int MaxPlatformLifetimeLevel = 3;
        public const int MaxPlatformSlotsLevel = 1;
        public const int MaxStrokeGuardLevel = 1;
        public const int MaxItemFortuneLevel = 3;
        public const float JumpPowerPerLevel = 0.04f;
        public const float InkCapacityPerLevel = 0.10f;
        public const float InkRecoveryPerLevel = 0.12f;
        public const float PlatformLifetimePerLevel = 0.10f;
        public const float ItemSpacingReductionPerLevel = 0.07f;
        public const float VitalityHitGraceSeconds = 0.55f;

        static readonly GrowthUpgradeType[] BodyUpgrades =
        {
            GrowthUpgradeType.Vitality,
            GrowthUpgradeType.JumpPower,
        };

        static readonly GrowthUpgradeType[] DrawingUpgrades =
        {
            GrowthUpgradeType.InkCapacity,
            GrowthUpgradeType.InkRecovery,
            GrowthUpgradeType.PlatformLifetime,
            GrowthUpgradeType.PlatformSlots,
            GrowthUpgradeType.StrokeGuard,
        };

        static readonly GrowthUpgradeType[] AllUpgrades =
        {
            GrowthUpgradeType.Vitality,
            GrowthUpgradeType.JumpPower,
            GrowthUpgradeType.InkCapacity,
            GrowthUpgradeType.InkRecovery,
            GrowthUpgradeType.PlatformLifetime,
            GrowthUpgradeType.PlatformSlots,
            GrowthUpgradeType.StrokeGuard,
            GrowthUpgradeType.ItemFortune,
        };

        public static RunGrowthController Instance { get; private set; }

        public int VitalityLevel { get; private set; }
        public int VitalityCharges { get; private set; }
        public int JumpLevel { get; private set; }
        public int InkCapacityLevel { get; private set; }
        public int InkRecoveryLevel { get; private set; }
        public int PlatformLifetimeLevel { get; private set; }
        public int PlatformSlotsLevel { get; private set; }
        public int StrokeGuardLevel { get; private set; }
        public int ItemFortuneLevel { get; private set; }
        public float JumpPowerMultiplier =>
            1f + JumpLevel * JumpPowerPerLevel;
        public float InkCapacityMultiplier =>
            1f + InkCapacityLevel * InkCapacityPerLevel;
        public float InkRecoveryMultiplier =>
            1f + InkRecoveryLevel * InkRecoveryPerLevel;
        public float PlatformLifetimeMultiplier =>
            1f + PlatformLifetimeLevel * PlatformLifetimePerLevel;
        public int AdditionalPlatformSlots => PlatformSlotsLevel;
        public bool NewPlatformsHaveStrokeGuard => StrokeGuardLevel > 0;
        /// 영구 생존 계보의 장착 비기는 먹분신별이 아니라 먹떼 전체가 한 번만 공유한다.
        public bool LastBreathAvailable { get; private set; }
        public int SafetyJumpProgress { get; private set; }
        public PermanentGrowthRunSnapshot PermanentSnapshot { get; private set; } =
            PermanentGrowthRunSnapshot.Empty;
        public float ItemSpacingMultiplier =>
            Mathf.Max(0.1f, 1f - ItemFortuneLevel * ItemSpacingReductionPerLevel);
        public bool HasPendingChoice { get; private set; }
        public bool HasSelectedPendingChoice => HasPendingChoice && choiceSelected;
        public bool IsFullyUpgraded
        {
            get
            {
                for (int i = 0; i < AllUpgrades.Length; i++)
                    if (CanSelectUpgrade(AllUpgrades[i]))
                        return false;
                return true;
            }
        }
        public IReadOnlyList<GrowthUpgradeType> CurrentOffers => currentOffers;

        /// 선택판은 이 이벤트를 받아 런타임 UI를 연다. 이벤트는 시간 정지가 성공한 뒤에만 발생한다.
        public event Action ChoiceRequested;
        public event Action<GrowthUpgradeType> UpgradeSelected;
        public event Action ChoiceCancelled;
        public event Action RunReset;
        public event Action Changed;
        /// 먹 자원은 StrokeCapture 한 곳이 소유하므로 규칙형 성장도 복구 요청만 전달한다.
        public event Action<float> InkRestoreRequested;
        public event Action<float> InkRestoreRatioRequested;

        readonly List<GrowthUpgradeType> currentOffers = new(3);
        readonly List<GrowthUpgradeType> offerCandidates = new(AllUpgrades.Length);
        GameManager manager;
        bool choiceSelected;
        readonly List<PlayerController> livingPlayers = new(GameManager.MaxLivingPlayers);
        float hitInkRecoveryReadyAt;
        float stableHitReadyAt;
        float cloneDeathHealReadyAt;
        float drawnLandingInkReadyAt;
        float sharedStrokeGuardReadyAt;
        float lastFallBrakeReadyAt;
        float doubleJumpReadyAt;
        PlatformCollider activeSafetyPlatform;

        void Awake()
        {
            BindManager();
        }

        // Play 중 스크립트 재컴파일 뒤에도 static을 복구한다.
        void OnEnable()
        {
            Instance = this;
            BindManager();
        }

        void OnDisable()
        {
            if (HasPendingChoice &&
                manager != null &&
                manager.PauseReason == GameplayPauseReason.GrowthChoice)
            {
                ClearPendingChoice();
                manager.EndGrowthChoicePause();
            }
            UnbindManager();
            if (Instance == this)
                Instance = null;
        }

        /// 두루마리 획득 또는 디버그 버튼이 선택판을 열 때 호출한다.
        public bool RequestChoice(bool debug = false)
        {
            BindManager();
            if (manager == null ||
                manager.State != GameState.Playing ||
                manager.IsTransitioning ||
                HasPendingChoice ||
                IsFullyUpgraded)
                return false;

            if (!manager.BeginGrowthChoicePause())
                return false;

            BuildCurrentOffers();
            if (currentOffers.Count == 0)
            {
                manager.EndGrowthChoicePause();
                return false;
            }

            HasPendingChoice = true;
            choiceSelected = false;
            if (debug && GameManager.DebugToolsAvailable)
                ScoreManager.Instance?.InvalidateCurrentRunForRecords();
            ChoiceRequested?.Invoke();
            return true;
        }

        /// 선택 결과를 먼저 적용하되 UI가 접히기 전까지는 시간을 멈춘 채 유지한다.
        public bool TrySelectUpgrade(GrowthUpgradeType upgrade)
        {
            if (!HasPendingChoice || choiceSelected ||
                !IsCurrentOffer(upgrade) ||
                !CanSelectUpgrade(upgrade))
                return false;

            switch (upgrade)
            {
                case GrowthUpgradeType.Vitality:
                    VitalityLevel++;
                    // 최대치와 현재치를 함께 한 칸 늘린다. 이미 소모한 완충은 되살리지 않는다.
                    VitalityCharges = Mathf.Min(
                        VitalityLevel,
                        VitalityCharges + 1);
                    break;
                case GrowthUpgradeType.JumpPower:
                    JumpLevel++;
                    break;
                case GrowthUpgradeType.InkCapacity:
                    InkCapacityLevel++;
                    break;
                case GrowthUpgradeType.InkRecovery:
                    InkRecoveryLevel++;
                    break;
                case GrowthUpgradeType.PlatformLifetime:
                    PlatformLifetimeLevel++;
                    break;
                case GrowthUpgradeType.PlatformSlots:
                    PlatformSlotsLevel++;
                    break;
                case GrowthUpgradeType.StrokeGuard:
                    StrokeGuardLevel++;
                    break;
                case GrowthUpgradeType.ItemFortune:
                    ItemFortuneLevel++;
                    break;
                default:
                    return false;
            }

            choiceSelected = true;
            Changed?.Invoke();
            UpgradeSelected?.Invoke(upgrade);
            return true;
        }

        public bool CanSelectUpgrade(GrowthUpgradeType upgrade)
        {
            return upgrade switch
            {
                GrowthUpgradeType.Vitality =>
                    VitalityLevel < MaxVitalityLevel,
                GrowthUpgradeType.JumpPower =>
                    JumpLevel < MaxJumpLevel,
                GrowthUpgradeType.InkCapacity =>
                    InkCapacityLevel < MaxInkCapacityLevel,
                GrowthUpgradeType.InkRecovery =>
                    InkRecoveryLevel < MaxInkRecoveryLevel,
                GrowthUpgradeType.PlatformLifetime =>
                    PlatformLifetimeLevel < MaxPlatformLifetimeLevel,
                GrowthUpgradeType.PlatformSlots =>
                    PlatformSlotsLevel < MaxPlatformSlotsLevel,
                GrowthUpgradeType.StrokeGuard =>
                    StrokeGuardLevel < MaxStrokeGuardLevel,
                GrowthUpgradeType.ItemFortune =>
                    ItemFortuneLevel < MaxItemFortuneLevel,
                _ => false,
            };
        }

        public int GetLevel(GrowthUpgradeType upgrade)
        {
            return upgrade switch
            {
                GrowthUpgradeType.Vitality => VitalityLevel,
                GrowthUpgradeType.JumpPower => JumpLevel,
                GrowthUpgradeType.InkCapacity => InkCapacityLevel,
                GrowthUpgradeType.InkRecovery => InkRecoveryLevel,
                GrowthUpgradeType.PlatformLifetime => PlatformLifetimeLevel,
                GrowthUpgradeType.PlatformSlots => PlatformSlotsLevel,
                GrowthUpgradeType.StrokeGuard => StrokeGuardLevel,
                GrowthUpgradeType.ItemFortune => ItemFortuneLevel,
                _ => 0,
            };
        }

        public int GetMaxLevel(GrowthUpgradeType upgrade)
        {
            return upgrade switch
            {
                GrowthUpgradeType.Vitality => MaxVitalityLevel,
                GrowthUpgradeType.JumpPower => MaxJumpLevel,
                GrowthUpgradeType.InkCapacity => MaxInkCapacityLevel,
                GrowthUpgradeType.InkRecovery => MaxInkRecoveryLevel,
                GrowthUpgradeType.PlatformLifetime => MaxPlatformLifetimeLevel,
                GrowthUpgradeType.PlatformSlots => MaxPlatformSlotsLevel,
                GrowthUpgradeType.StrokeGuard => MaxStrokeGuardLevel,
                GrowthUpgradeType.ItemFortune => MaxItemFortuneLevel,
                _ => 0,
            };
        }

        /// 선택판의 닫기 연출이 끝난 뒤 호출한다.
        public bool FinishChoice()
        {
            if (!HasPendingChoice || !choiceSelected)
                return false;

            ClearPendingChoice();
            return manager != null && manager.EndGrowthChoicePause();
        }

        /// 씬 종료나 선택판 비활성화 같은 예외 경로에서도 시간 정지가 남지 않게 한다.
        public bool CancelChoice()
        {
            if (!HasPendingChoice)
                return false;

            ClearPendingChoice();
            ChoiceCancelled?.Invoke();
            return manager != null && manager.EndGrowthChoicePause();
        }

        /// 방어막이 없을 때 장애물 피해 한 번을 먹떼 공유 완충으로 흡수한다.
        /// 추락 판정은 이 경로를 호출하지 않으므로 기존 사망 규칙을 그대로 유지한다.
        public bool TryAbsorbObstacleHit(PlayerController player)
        {
            if (player == null || player.IsDead ||
                VitalityCharges <= 0 ||
                manager == null ||
                manager.State != GameState.Playing)
                return false;

            VitalityCharges--;
            player.ApplyVitalityHitRecovery(VitalityHitGraceSeconds);
            Changed?.Invoke();
            return true;
        }

        /// 체력이 소진될 장애물 피해를 한 판에 한 번 1체력으로 버틴다.
        /// 추락은 PlayerController.Kill 경로에서 직접 처리하므로 이 패시브를 소비하지 않는다.
        public bool TrySurviveLethalObstacleHit(PlayerController player)
        {
            if (player == null || player.IsDead ||
                !LastBreathAvailable ||
                manager == null ||
                manager.State != GameState.Playing ||
                manager.LivingPlayerCount != 1)
                return false;

            LastBreathAvailable = false;
            Changed?.Invoke();
            return true;
        }

        /// 실제 체력만 줄어든 비치명 장애물 피해 뒤 발동하는 먹 회복이다.
        public void NotifyNonLethalObstacleHit()
        {
            if (!PermanentSnapshot.HasHitInkRecovery ||
                Time.time < hitInkRecoveryReadyAt)
                return;
            hitInkRecoveryReadyAt = Time.time + 8f;
            InkRestoreRatioRequested?.Invoke(0.04f);
        }

        /// 장착 비기 S-KB의 공용 12초 사용권. true면 피해 뒤 물리를 바꾸지 않는다.
        public bool TryPreserveHitMotion()
        {
            if (!PermanentSnapshot.HasStableHit ||
                Time.time < stableHitReadyAt)
                return false;
            stableHitReadyAt = Time.time + 12f;
            return true;
        }

        public void NotifyCloneCreated(
            PlayerController source,
            PlayerController clone)
        {
            if (source == null || clone == null)
                return;
            if (PermanentSnapshot.HasCloneSourceGrace)
                source.GrantObstacleProtection(0.25f);
            if (!PermanentSnapshot.HasCloneBond)
                return;

            RestoreLowestHealth(1);
            manager?.GetLivingPlayersNonAlloc(livingPlayers);
            for (int i = 0; i < livingPlayers.Count; i++)
                livingPlayers[i]?.GrantObstacleProtection(0.35f);
        }

        public void NotifyPlayerDied(PlayerController player)
        {
            if (player == null ||
                !player.IsRuntimeClone ||
                !PermanentSnapshot.HasCloneDeathHeal ||
                Time.time < cloneDeathHealReadyAt)
                return;
            if (!RestoreLowestHealth(1))
                return;
            cloneDeathHealReadyAt = Time.time + 30f;
        }

        public void NotifyDrawnPlatformLanding()
        {
            if (!PermanentSnapshot.HasDrawnLandingInk ||
                Time.time < drawnLandingInkReadyAt)
                return;
            drawnLandingInkReadyAt = Time.time + 4f;
            InkRestoreRequested?.Invoke(0.20f);
        }

        public bool TryRefundExpiredPlatform(float spentInk)
        {
            if (!PermanentSnapshot.HasNaturalExpiryRefund || spentInk <= 0f)
                return false;
            InkRestoreRequested?.Invoke(Mathf.Min(0.6f, spentInk * 0.10f));
            return true;
        }

        /// 한 판 굳은 획이 없는 충돌에서만 검사하는 공용 낙묵석 방어다.
        public bool TryUsePermanentStrokeGuard()
        {
            if (!PermanentSnapshot.HasSharedStrokeGuard ||
                Time.time < sharedStrokeGuardReadyAt)
                return false;
            sharedStrokeGuardReadyAt = Time.time + 18f;
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

        /// 자동 2단점프는 대표 한 체만, 먹떼 공용 12초마다 한 번 허용한다.
        /// 먹물방울·풍맥은 AutoJump에서 이 경로를 호출하지 않는다.
        public bool TryUseDoubleJump(PlayerController player)
        {
            if (player == null || player.IsDead ||
                !PermanentSnapshot.HasDoubleJump ||
                manager == null || manager.State != GameState.Playing ||
                !IsSwarmRepresentative(player) ||
                Time.time < doubleJumpReadyAt)
                return false;

            doubleJumpReadyAt = Time.time + 12f;
            return true;
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

            lastFallBrakeReadyAt = Time.time + 18f;
            return true;
        }

        bool RestoreLowestHealth(int amount)
        {
            if (manager == null || amount <= 0)
                return false;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            PlayerController target = null;
            for (int i = 0; i < livingPlayers.Count; i++)
            {
                PlayerController candidate = livingPlayers[i];
                if (candidate == null || candidate.CurrentHealth >= candidate.MaxHealth)
                    continue;
                if (target == null || candidate.CurrentHealth < target.CurrentHealth)
                    target = candidate;
            }
            return target != null && target.RestoreHealth(amount);
        }

        void BindManager()
        {
            var nextManager = GetComponent<GameManager>();
            if (nextManager == null)
                nextManager = GameManager.Instance;
            if (manager == nextManager)
                return;

            UnbindManager();
            manager = nextManager;
            if (manager != null)
                manager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing && previous != GameState.Playing)
            {
                ResetRun();
                return;
            }

            if (current != GameState.Playing && HasPendingChoice)
                CancelChoice();
        }

        void ResetRun()
        {
            PermanentSnapshot = PermanentGrowthProfile.CreateRunSnapshot();
            VitalityLevel = 0;
            VitalityCharges = 0;
            JumpLevel = 0;
            InkCapacityLevel = 0;
            InkRecoveryLevel = 0;
            PlatformLifetimeLevel = 0;
            PlatformSlotsLevel = 0;
            StrokeGuardLevel = 0;
            ItemFortuneLevel = 0;
            LastBreathAvailable = PermanentSnapshot.HasLastBreath;
            hitInkRecoveryReadyAt = float.NegativeInfinity;
            stableHitReadyAt = float.NegativeInfinity;
            cloneDeathHealReadyAt = float.NegativeInfinity;
            drawnLandingInkReadyAt = float.NegativeInfinity;
            sharedStrokeGuardReadyAt = float.NegativeInfinity;
            lastFallBrakeReadyAt = float.NegativeInfinity;
            doubleJumpReadyAt = float.NegativeInfinity;
            SafetyJumpProgress = 0;
            if (activeSafetyPlatform != null)
                Destroy(activeSafetyPlatform.gameObject);
            activeSafetyPlatform = null;
            HasPendingChoice = false;
            choiceSelected = false;
            currentOffers.Clear();
            RunReset?.Invoke();
            Changed?.Invoke();
        }

        void ClearPendingChoice()
        {
            HasPendingChoice = false;
            choiceSelected = false;
            currentOffers.Clear();
        }

        void BuildCurrentOffers()
        {
            currentOffers.Clear();

            // 몸·드로잉에서 하나씩 먼저 보장해 선택지가 한 계통에만 몰리지 않게 한다.
            TryAddRandomOffer(BodyUpgrades);
            TryAddRandomOffer(DrawingUpgrades);
            if (currentOffers.Count < 3)
                TryAddRandomOffer(AllUpgrades);

            // 한 계통이 모두 최대라 보장 슬롯이 비었다면 남은 전체 풀에서 채운다.
            while (currentOffers.Count < 3 && TryAddRandomOffer(AllUpgrades))
            {
            }
        }

        bool TryAddRandomOffer(IReadOnlyList<GrowthUpgradeType> source)
        {
            offerCandidates.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                GrowthUpgradeType candidate = source[i];
                if (CanSelectUpgrade(candidate) && !IsCurrentOffer(candidate))
                    offerCandidates.Add(candidate);
            }

            if (offerCandidates.Count == 0)
                return false;

            int index = GameplayRandom.Range(
                GameplayRandomStream.Growth, 0, offerCandidates.Count);
            currentOffers.Add(offerCandidates[index]);
            return true;
        }

        bool IsCurrentOffer(GrowthUpgradeType upgrade)
        {
            for (int i = 0; i < currentOffers.Count; i++)
                if (currentOffers[i] == upgrade)
                    return true;
            return false;
        }

    }
}
