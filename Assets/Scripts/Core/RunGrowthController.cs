using System;
using System.Collections.Generic;
using MukJump.Drawing;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 영구 성장 프로필을 판 시작에 고정하고 먹떼 공용 패시브와 쿨다운을 소유한다.
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

        GameManager manager;
        float stableHitReadyAt;
        float lastFallBrakeReadyAt;
        float doubleJumpReadyAt;
        PlayerController doubleJumpReservedPlayer;
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

        /// 이륙 순간의 먹떼 대표에게 2단점프 사용권을 예약한다. 정점에 도달할 때
        /// 대표가 바뀌어도 예약자는 사용권을 잃지 않는다.
        public bool TryReserveDoubleJump(PlayerController player)
        {
            if (doubleJumpReservedPlayer != null &&
                doubleJumpReservedPlayer.IsDead)
                doubleJumpReservedPlayer = null;
            if (player == null || player.IsDead ||
                !PermanentSnapshot.HasDoubleJump ||
                manager == null || manager.State != GameState.Playing ||
                Time.time < doubleJumpReadyAt)
                return false;
            if (doubleJumpReservedPlayer == player)
                return true;
            if (doubleJumpReservedPlayer != null || !IsSwarmRepresentative(player))
                return false;
            doubleJumpReservedPlayer = player;
            return true;
        }

        /// 예약된 자동 2단점프를 먹떼 공용 12초 사용권으로 소비한다.
        /// 먹물방울·풍맥은 AutoJump에서 이 경로를 호출하지 않는다.
        public bool TryUseDoubleJump(PlayerController player)
        {
            if (player == null || player.IsDead ||
                !PermanentSnapshot.HasDoubleJump ||
                manager == null || manager.State != GameState.Playing ||
                doubleJumpReservedPlayer != player ||
                Time.time < doubleJumpReadyAt)
                return false;

            doubleJumpReservedPlayer = null;
            doubleJumpReadyAt = Time.time + 12f;
            return true;
        }

        public void CancelDoubleJumpReservation(PlayerController player)
        {
            if (doubleJumpReservedPlayer == player)
                doubleJumpReservedPlayer = null;
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
            if (current == GameState.Playing && previous != GameState.Playing)
                ResetRun();
        }

        void ResetRun()
        {
            PermanentSnapshot = PermanentGrowthProfile.CreateRunSnapshot();
            LastBreathAvailable = PermanentSnapshot.HasLastBreath;
            stableHitReadyAt = float.NegativeInfinity;
            lastFallBrakeReadyAt = float.NegativeInfinity;
            doubleJumpReadyAt = float.NegativeInfinity;
            doubleJumpReservedPlayer = null;
            SafetyJumpProgress = 0;
            if (activeSafetyPlatform != null)
                Destroy(activeSafetyPlatform.gameObject);
            activeSafetyPlatform = null;
            RunReset?.Invoke();
        }
    }
}
