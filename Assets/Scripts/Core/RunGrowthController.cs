using System;
using System.Collections.Generic;
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

        readonly List<GrowthUpgradeType> currentOffers = new(3);
        readonly List<GrowthUpgradeType> offerCandidates = new(AllUpgrades.Length);
        GameManager manager;
        bool choiceSelected;
        bool focusOfferConsumed;

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
            VitalityLevel = 0;
            VitalityCharges = 0;
            JumpLevel = 0;
            InkCapacityLevel = 0;
            InkRecoveryLevel = 0;
            PlatformLifetimeLevel = 0;
            PlatformSlotsLevel = 0;
            StrokeGuardLevel = 0;
            ItemFortuneLevel = 0;
            HasPendingChoice = false;
            choiceSelected = false;
            focusOfferConsumed = false;
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

            GrowthUpgradeType? focusedUpgrade = null;
            if (!focusOfferConsumed)
            {
                focusOfferConsumed = true;
                if (GrowthFocusProfile.TryGetRuntimeUpgrade(out var preferred) &&
                    CanSelectUpgrade(preferred))
                {
                    currentOffers.Add(preferred);
                    focusedUpgrade = preferred;
                }
            }

            // 몸·드로잉에서 하나씩 먼저 보장해 선택지가 한 계통에만 몰리지 않게 한다.
            if (!focusedUpgrade.HasValue ||
                !Contains(BodyUpgrades, focusedUpgrade.Value))
                TryAddRandomOffer(BodyUpgrades);
            if (!focusedUpgrade.HasValue ||
                !Contains(DrawingUpgrades, focusedUpgrade.Value))
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

        static bool Contains(
            IReadOnlyList<GrowthUpgradeType> source,
            GrowthUpgradeType upgrade)
        {
            for (int i = 0; i < source.Count; i++)
                if (source[i] == upgrade)
                    return true;
            return false;
        }
    }
}
