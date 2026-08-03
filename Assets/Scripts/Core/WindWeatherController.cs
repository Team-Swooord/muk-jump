using System.Collections.Generic;
using UnityEngine;
using MukJump.Player;

namespace MukJump.Core
{
    public enum WindWeatherPhase
    {
        Breeze,
        Warning,
        Updraft,
        Recovery,
    }

    /// 전 맵에 약한 횡풍을 적용하고, 수백 m마다 낙하를 잠시 멈추는 상승기류를 만든다.
    /// 중력값은 건드리지 않고 현재 속도만 보정하므로 발판 접착·아이템 점프와 충돌하지 않는다.
    [DefaultExecutionOrder(-80)]
    public class WindWeatherController : MonoBehaviour
    {
        public static WindWeatherController Instance { get; private set; }

        const float WarningDuration = 1.35f;
        const float UpdraftDuration = 5.5f;
        const float RecoveryDuration = 0.8f;
        const float UpdraftHoverSpeed = 0.55f;

        [Header("약한 바람")]
        [SerializeField, Min(0f)] float breezeAcceleration = 0.78f;
        [SerializeField, Min(0f)] float breezeSpeedLimit = 2.25f;
        [SerializeField, Min(0.01f)] float directionEaseSpeed = 0.28f;
        [SerializeField] Vector2 directionHoldSeconds = new(28f, 45f);

        [Header("상승기류")]
        [SerializeField, Min(0f)] float updraftHorizontalMultiplier = 1.35f;
        [SerializeField, Min(0f)] float updraftRiseAcceleration = 0.72f;

        readonly List<PlayerController> livingPlayers = new();

        GameManager subscribedManager;
        HeightZoneController heightZoneController;
        float phaseElapsed;
        float directionHoldRemaining;
        bool sessionActive;

        public int DirectionSign { get; private set; } = 1;
        public float DirectionBlend { get; private set; } = 1f;
        public float Strength01 { get; private set; }
        public WindWeatherPhase Phase { get; private set; } = WindWeatherPhase.Breeze;
        public int NextUpdraftHeight { get; private set; }
        public bool IsUpdraftActive => Phase == WindWeatherPhase.Updraft;

        /// 현재 맵 구간은 바람을 없애지 않고 세기만 조금 다르게 만든다.
        public float ZoneStrengthMultiplier
        {
            get
            {
                if (heightZoneController == null)
                    heightZoneController = GetComponent<HeightZoneController>();
                return heightZoneController != null
                    ? GetZoneStrengthMultiplier(heightZoneController.CurrentZone)
                    : 1f;
            }
        }

        void OnEnable()
        {
            Instance = this;
            if (Application.isPlaying)
                ApplyRuntimeDirectionTuning();
            if (DirectionSign == 0)
                DirectionSign = GameplayRandom.Value(
                    GameplayRandomStream.Weather) < 0.5f ? -1 : 1;
            DirectionBlend = DirectionSign;
            directionHoldRemaining = SampleDirectionHold();
            BindGameManager();
        }

        /// 예전 Main 씬에 직렬화된 빠른 값이 남아 있어도 새 완만한 풍향 규칙을 적용한다.
        void ApplyRuntimeDirectionTuning()
        {
            directionEaseSpeed = Mathf.Min(directionEaseSpeed, 0.28f);
            directionHoldSeconds = new Vector2(
                Mathf.Max(directionHoldSeconds.x, 28f),
                Mathf.Max(directionHoldSeconds.y, 45f));
        }

        void Start()
        {
            heightZoneController = GetComponent<HeightZoneController>();
            BindGameManager();
        }

        void Update()
        {
            BindGameManager();
            if (subscribedManager == null ||
                subscribedManager.State != GameState.Playing)
            {
                Strength01 = 0f;
                return;
            }
            // 일시정지는 풍향·세기·단계 타이머를 현재 값 그대로 보존한다.
            if (!subscribedManager.IsGameplayTicking)
                return;

            if (!sessionActive)
                BeginSession(CurrentHeight);

            float deltaTime = Time.deltaTime;
            UpdateDirection(deltaTime);
            UpdateWeatherPhase(deltaTime);
            UpdateStrength();
        }

        void FixedUpdate()
        {
            BindGameManager();
            if (subscribedManager == null || !subscribedManager.IsGameplayTicking)
                return;

            subscribedManager.GetLivingPlayersNonAlloc(livingPlayers);
            float zoneMultiplier = ZoneStrengthMultiplier;
            float horizontalMultiplier = IsUpdraftActive ? updraftHorizontalMultiplier : 1f;
            float permanentWindMultiplier =
                RunGrowthController.Instance != null
                    ? RunGrowthController.Instance.PermanentSnapshot
                        .WindInfluenceMultiplier
                    : 1f;
            float horizontalAcceleration = breezeAcceleration * zoneMultiplier *
                                           horizontalMultiplier *
                                           permanentWindMultiplier;
            float horizontalLimit = breezeSpeedLimit * zoneMultiplier *
                                    permanentWindMultiplier;
            float deltaTime = Time.fixedDeltaTime;

            for (int i = 0; i < livingPlayers.Count; i++)
            {
                var player = livingPlayers[i];
                if (player == null || player.IsDead || player.IsGrounded ||
                    player.CurrentPlatform != null)
                    continue;

                var body = player.Body;
                if (body == null || !body.simulated ||
                    body.bodyType != RigidbodyType2D.Dynamic)
                    continue;

                bool applyVerticalUpdraft = IsUpdraftActive &&
                                             !player.IsInkDropBoosted;
                body.linearVelocity = CalculateVelocity(
                    body.linearVelocity,
                    DirectionBlend,
                    horizontalAcceleration,
                    horizontalLimit,
                    applyVerticalUpdraft,
                    updraftRiseAcceleration,
                    UpdraftHoverSpeed,
                    Mathf.Abs(Physics2D.gravity.y * body.gravityScale),
                    deltaTime);
            }
        }

        void BindGameManager()
        {
            var manager = GameManager.Instance;
            if (manager == subscribedManager) return;

            UnbindGameManager();
            subscribedManager = manager;
            if (subscribedManager == null) return;

            subscribedManager.StateChanged += HandleStateChanged;
            subscribedManager.WorldHeightTeleported += HandleWorldHeightTeleported;
            if (subscribedManager.State == GameState.Playing)
                BeginSession(CurrentHeight, false);
        }

        void UnbindGameManager()
        {
            if (subscribedManager == null) return;
            subscribedManager.StateChanged -= HandleStateChanged;
            subscribedManager.WorldHeightTeleported -= HandleWorldHeightTeleported;
            subscribedManager = null;
        }

        void HandleStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.Playing)
            {
                // GameManager가 직후 점수 원점을 0m로 맞추므로 새 판은 항상 첫 간격을 사용한다.
                BeginSession(0);
                return;
            }

            sessionActive = false;
            Phase = WindWeatherPhase.Breeze;
            phaseElapsed = 0f;
            Strength01 = 0f;
        }

        void HandleWorldHeightTeleported(int targetHeight)
        {
            // 디버그 이동으로 지나온 예약을 한꺼번에 발동하지 않고 다음 구간부터 다시 센다.
            BeginSession(Mathf.Max(0, targetHeight), false);
        }

        void BeginSession(int currentHeight, bool useFirstInterval = true)
        {
            sessionActive = true;
            Phase = WindWeatherPhase.Breeze;
            phaseElapsed = 0f;
            DirectionSign = GameplayRandom.Value(
                GameplayRandomStream.Weather) < 0.5f ? -1 : 1;
            DirectionBlend = DirectionSign;
            directionHoldRemaining = SampleDirectionHold();
            NextUpdraftHeight = Mathf.Max(0, currentHeight) +
                                (useFirstInterval
                                    ? GameplayRandom.Range(
                                        GameplayRandomStream.Weather, 180, 261)
                                    : GameplayRandom.Range(
                                        GameplayRandomStream.Weather, 220, 341));
            Strength01 = 0f;
        }

        void UpdateDirection(float deltaTime)
        {
            // 상승기류 연출 중에는 다음 풍향까지 남은 시간을 소모하지 않는다.
            // 평상시에도 한 방향을 충분히 오래 유지해 플레이어가 미리 대응할 수 있게 한다.
            if (Phase == WindWeatherPhase.Breeze)
            {
                directionHoldRemaining -= deltaTime;
                if (directionHoldRemaining <= 0f)
                {
                    DirectionSign = -DirectionSign;
                    directionHoldRemaining = SampleDirectionHold();
                }
            }

            DirectionBlend = Mathf.MoveTowards(
                DirectionBlend,
                DirectionSign,
                directionEaseSpeed * deltaTime);
        }

        void UpdateWeatherPhase(float deltaTime)
        {
            phaseElapsed += deltaTime;
            switch (Phase)
            {
                case WindWeatherPhase.Breeze:
                    if (CurrentHeight >= NextUpdraftHeight &&
                        !HazardConcurrencyGate.HasHaetaeReservation)
                        BeginWarning();
                    break;
                case WindWeatherPhase.Warning:
                    if (phaseElapsed >= WarningDuration)
                        SetPhase(WindWeatherPhase.Updraft);
                    break;
                case WindWeatherPhase.Updraft:
                    if (phaseElapsed >= UpdraftDuration)
                        SetPhase(WindWeatherPhase.Recovery);
                    break;
                case WindWeatherPhase.Recovery:
                    if (phaseElapsed >= RecoveryDuration)
                        SetPhase(WindWeatherPhase.Breeze);
                    break;
            }
        }

        void BeginWarning()
        {
            SetPhase(WindWeatherPhase.Warning);
            NextUpdraftHeight = CurrentHeight + GameplayRandom.Range(
                GameplayRandomStream.Weather, 220, 341);
            GameFeedbackController.Instance?.ShowZone(
                "상승기류 접근",
                "잠시 뒤 낙하를 받쳐 주는 강한 바람이 붑니다");
        }

        void SetPhase(WindWeatherPhase next)
        {
            Phase = next;
            phaseElapsed = 0f;
        }

        void UpdateStrength()
        {
            float breeze = Mathf.Clamp01(
                (0.28f + Mathf.Abs(DirectionBlend) * 0.14f) *
                ZoneStrengthMultiplier);
            Strength01 = Phase switch
            {
                WindWeatherPhase.Warning => Mathf.Lerp(
                    breeze, 0.82f, Mathf.Clamp01(phaseElapsed / WarningDuration)),
                WindWeatherPhase.Updraft => 1f,
                WindWeatherPhase.Recovery => Mathf.Lerp(
                    1f, breeze, Mathf.Clamp01(phaseElapsed / RecoveryDuration)),
                _ => breeze,
            };
        }

        float SampleDirectionHold()
        {
            float minimum = Mathf.Max(0.1f,
                Mathf.Min(directionHoldSeconds.x, directionHoldSeconds.y));
            float maximum = Mathf.Max(minimum,
                Mathf.Max(directionHoldSeconds.x, directionHoldSeconds.y));
            return GameplayRandom.Range(
                GameplayRandomStream.Weather, minimum, maximum);
        }

        int CurrentHeight => ScoreManager.Instance != null
            ? Mathf.Max(0, ScoreManager.Instance.Height)
            : 0;

        /// 디버그 패널에서 풍향 전환과 완만한 방향 보간을 함께 확인한다.
        public void DebugFlipDirection()
        {
            DirectionSign = -DirectionSign;
            directionHoldRemaining = SampleDirectionHold();
        }

        /// 디버그 패널에서 예고를 포함한 강풍 한 사이클을 즉시 시작한다.
        public void DebugTriggerUpdraft()
        {
            if (subscribedManager == null ||
                subscribedManager.State != GameState.Playing)
                return;
            BeginWarning();
        }

        /// 물리 컴포넌트 없이도 바람 속도 규칙을 검증할 수 있는 순수 계산 함수.
        public static Vector2 CalculateVelocity(
            Vector2 currentVelocity,
            float directionBlend,
            float horizontalAcceleration,
            float horizontalSpeedLimit,
            bool applyUpdraft,
            float updraftAcceleration,
            float updraftHoverSpeed,
            float updraftGravityCompensation,
            float deltaTime)
        {
            float blend = Mathf.Clamp(directionBlend, -1f, 1f);
            float acceleration = Mathf.Max(0f, horizontalAcceleration);
            float limit = Mathf.Max(0f, horizontalSpeedLimit);
            float step = acceleration * Mathf.Abs(blend) * Mathf.Max(0f, deltaTime);
            float targetX = Mathf.Sign(blend) * limit * Mathf.Abs(blend);
            float velocityX = currentVelocity.x;

            if (blend > 0f && velocityX < targetX)
                velocityX = Mathf.Min(targetX, velocityX + step);
            else if (blend < 0f && velocityX > targetX)
                velocityX = Mathf.Max(targetX, velocityX - step);

            float velocityY = currentVelocity.y;
            if (applyUpdraft && velocityY <= updraftHoverSpeed)
            {
                // FixedUpdate 뒤 적용될 중력 한 스텝을 미리 상쇄한다. 화면에 보이는
                // 실제 속도는 0에서 hover 속도까지만 천천히 증가해 강제 점프가 되지 않는다.
                float postGravityVelocity = Mathf.Max(0f, velocityY);
                postGravityVelocity = Mathf.MoveTowards(
                    postGravityVelocity,
                    Mathf.Max(0f, updraftHoverSpeed),
                    Mathf.Max(0f, updraftAcceleration) *
                    Mathf.Max(0f, deltaTime));
                velocityY = postGravityVelocity +
                            Mathf.Max(0f, updraftGravityCompensation) *
                            Mathf.Max(0f, deltaTime);
            }

            return new Vector2(velocityX, velocityY);
        }

        /// 네 구간 모두 바람이 존재하며, 바람 고개만 조금 더 강하게 느껴진다.
        public static float GetZoneStrengthMultiplier(HeightZoneController.Zone zone)
        {
            return zone switch
            {
                HeightZoneController.Zone.QuietMountain => 0.82f,
                HeightZoneController.Zone.WindPass => 1.25f,
                HeightZoneController.Zone.InkRain => 0.92f,
                HeightZoneController.Zone.RockGorge => 1.05f,
                _ => 1f,
            };
        }

        void OnDisable()
        {
            UnbindGameManager();
            if (Instance == this)
                Instance = null;
            livingPlayers.Clear();
            sessionActive = false;
            Strength01 = 0f;
        }
    }
}
