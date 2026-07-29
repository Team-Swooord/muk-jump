using System;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 한 판 동안만 유지되는 성장 선택 종류.
    public enum GrowthUpgradeType
    {
        Vitality,
        JumpPower,
    }

    /// 성장 두루마리의 세션 진행도를 한 곳에서 소유한다.
    /// 체력은 먹분신별 목숨을 곱하지 않고 먹떼 전체가 공유하는 완충 횟수로 계산한다.
    [DisallowMultipleComponent]
    public sealed class RunGrowthController : MonoBehaviour
    {
        public const int MaxVitalityLevel = 3;
        public const int MaxJumpLevel = 5;
        public const float JumpPowerPerLevel = 0.04f;
        public const float VitalityHitGraceSeconds = 0.55f;

        public static RunGrowthController Instance { get; private set; }

        public int VitalityLevel { get; private set; }
        public int VitalityCharges { get; private set; }
        public int JumpLevel { get; private set; }
        public float JumpPowerMultiplier =>
            1f + JumpLevel * JumpPowerPerLevel;
        public bool HasPendingChoice { get; private set; }
        public bool HasSelectedPendingChoice => HasPendingChoice && choiceSelected;
        public bool IsFullyUpgraded =>
            VitalityLevel >= MaxVitalityLevel &&
            JumpLevel >= MaxJumpLevel;

        /// 선택판은 이 이벤트를 받아 런타임 UI를 연다. 이벤트는 시간 정지가 성공한 뒤에만 발생한다.
        public event Action ChoiceRequested;
        public event Action<GrowthUpgradeType> UpgradeSelected;
        public event Action ChoiceCancelled;
        public event Action Changed;

        GameManager manager;
        bool choiceSelected;

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
                _ => false,
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
            HasPendingChoice = false;
            choiceSelected = false;
            Changed?.Invoke();
        }

        void ClearPendingChoice()
        {
            HasPendingChoice = false;
            choiceSelected = false;
        }
    }
}
