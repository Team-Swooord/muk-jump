using System.Collections;
using System.Collections.Generic;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
    /// 옵션의 개발용 연출 시나리오. 일반은 실제 사용자 성장과 정상 시작 규칙을 사용한다.
    public enum DebugShowcaseScenarioId
    {
        Normal = 0,
        SwarmParade = 1,
        GoldenInkRain = 2,
        HaetaeDescent = 3,
        CelestialLeap = 4,
        InkRiverRelay = 5,
    }

    /// 한 시나리오의 맵·먹떼·성장 빌드·시작 연출을 한곳에서 정의한다.
    /// 사용자 PlayerPrefs를 바꾸지 않고 다음 판의 런 스냅샷에만 적용된다.
    public sealed class DebugShowcaseScenarioDefinition
    {
        public DebugShowcaseScenarioDefinition(
            DebugShowcaseScenarioId id,
            string title,
            string summary,
            string bannerSubtitle,
            int targetHeight,
            int desiredLivingPlayers,
            PermanentGrowthPath survivalPath,
            PermanentGrowthPath leapPath,
            PermanentGrowthPath inkPath,
            bool spawnWindPlatform = false,
            bool flipWind = false,
            bool triggerUpdraft = false,
            bool spawnHaetae = false,
            bool applyGoldenBrush = false,
            bool applyInkDrop = false,
            bool shieldAllPlayers = false,
            bool runInkSwarmCascade = false)
        {
            Id = id;
            Title = title;
            Summary = summary;
            BannerSubtitle = bannerSubtitle;
            TargetHeight = Mathf.Max(0, targetHeight);
            DesiredLivingPlayers = Mathf.Clamp(
                desiredLivingPlayers,
                1,
                GameManager.MaxLivingPlayers);
            SurvivalPath = survivalPath;
            LeapPath = leapPath;
            InkPath = inkPath;
            SpawnWindPlatform = spawnWindPlatform;
            FlipWind = flipWind;
            TriggerUpdraft = triggerUpdraft;
            SpawnHaetae = spawnHaetae;
            ApplyGoldenBrush = applyGoldenBrush;
            ApplyInkDrop = applyInkDrop;
            ShieldAllPlayers = shieldAllPlayers;
            RunInkSwarmCascade = runInkSwarmCascade;
        }

        public DebugShowcaseScenarioId Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public string BannerSubtitle { get; }
        public int TargetHeight { get; }
        public int DesiredLivingPlayers { get; }
        public PermanentGrowthPath SurvivalPath { get; }
        public PermanentGrowthPath LeapPath { get; }
        public PermanentGrowthPath InkPath { get; }
        public bool SpawnWindPlatform { get; }
        public bool FlipWind { get; }
        public bool TriggerUpdraft { get; }
        public bool SpawnHaetae { get; }
        public bool ApplyGoldenBrush { get; }
        public bool ApplyInkDrop { get; }
        public bool ShieldAllPlayers { get; }
        public bool RunInkSwarmCascade { get; }

        public PermanentGrowthRunSnapshot CreateGrowthSnapshot()
        {
            var owned = new List<string>(15);
            var active = new Dictionary<PermanentGrowthBranch, string>(3);
            AddPath(
                owned,
                active,
                PermanentGrowthBranch.Survival,
                "S",
                SurvivalPath);
            AddPath(
                owned,
                active,
                PermanentGrowthBranch.Leap,
                "J",
                LeapPath);
            AddPath(
                owned,
                active,
                PermanentGrowthBranch.InkHandling,
                "I",
                InkPath);
            return new PermanentGrowthRunSnapshot(owned, active);
        }

        static void AddPath(
            ICollection<string> owned,
            IDictionary<PermanentGrowthBranch, string> active,
            PermanentGrowthBranch branch,
            string prefix,
            PermanentGrowthPath path)
        {
            owned.Add(prefix + "00");
            if (path == PermanentGrowthPath.None)
                return;

            string suffix = path.ToString();
            owned.Add($"{prefix}-{suffix}1");
            owned.Add($"{prefix}-{suffix}2");
            owned.Add($"{prefix}-{suffix}3");
            string keystoneId = $"{prefix}-K{suffix}";
            owned.Add(keystoneId);
            active[branch] = keystoneId;
        }
    }

    /// 옵션에서 고른 개발용 상황을 현재 Play 세션 동안만 보관한다.
    /// 앱 저장과 영구 성장 저장에는 어떤 값도 쓰지 않는다.
    public static class DebugShowcaseScenarioProfile
    {
        static readonly DebugShowcaseScenarioDefinition[] definitions =
        {
            new(
                DebugShowcaseScenarioId.SwarmParade,
                "상황 1 · 먹떼 행진",
                "10마리 · 바람 능선 · 먹떼 성장",
                "열 마리 먹방울과 풍맥이 한꺼번에 깨어납니다",
                260,
                10,
                PermanentGrowthPath.C,
                PermanentGrowthPath.B,
                PermanentGrowthPath.A,
                spawnWindPlatform: true,
                flipWind: true),
            new(
                DebugShowcaseScenarioId.GoldenInkRain,
                "상황 2 · 황금 먹비",
                "3마리 · 먹비 계곡 · 황금 붓 성장",
                "먹비 속에서 황금 붓과 먹물 상승을 이어갑니다",
                510,
                3,
                PermanentGrowthPath.A,
                PermanentGrowthPath.A,
                PermanentGrowthPath.B,
                applyGoldenBrush: true,
                applyInkDrop: true),
            new(
                DebugShowcaseScenarioId.HaetaeDescent,
                "상황 3 · 해태 급습",
                "2마리 · 검은 절벽 · 부활 성장",
                "방어막을 두른 먹떼 앞으로 해태가 내려옵니다",
                760,
                2,
                PermanentGrowthPath.B,
                PermanentGrowthPath.B,
                PermanentGrowthPath.C,
                spawnHaetae: true,
                shieldAllPlayers: true),
            new(
                DebugShowcaseScenarioId.CelestialLeap,
                "상황 4 · 천상 도약",
                "5마리 · 월련 성해 · 2단점프 성장",
                "우주 수묵화에서 상승기류와 2단도약을 펼칩니다",
                1260,
                5,
                PermanentGrowthPath.A,
                PermanentGrowthPath.C,
                PermanentGrowthPath.A,
                triggerUpdraft: true,
                applyInkDrop: true),
            new(
                DebugShowcaseScenarioId.InkRiverRelay,
                "상황 5 · 천하수 먹떼",
                "2→12마리 · 천하수 · 50m 연속 상승",
                "먹물방울과 먹분신이 번갈아 이어지는 천하수 질주입니다",
                1510,
                2,
                PermanentGrowthPath.C,
                PermanentGrowthPath.C,
                PermanentGrowthPath.C,
                flipWind: true,
                runInkSwarmCascade: true),
        };

        static DebugShowcaseScenarioId selectedId;

        public static event System.Action Changed;
        public static IReadOnlyList<DebugShowcaseScenarioDefinition> Definitions =>
            definitions;
        public static DebugShowcaseScenarioId SelectedId => selectedId;
        public static DebugShowcaseScenarioDefinition SelectedDefinition =>
            GetDefinition(selectedId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            selectedId = DebugShowcaseScenarioId.Normal;
            Changed = null;
        }

        public static bool Select(DebugShowcaseScenarioId id)
        {
            if (id != DebugShowcaseScenarioId.Normal && GetDefinition(id) == null)
                return false;
            if (selectedId == id)
                return true;
            selectedId = id;
            Changed?.Invoke();
            return true;
        }

        public static DebugShowcaseScenarioDefinition GetDefinition(
            DebugShowcaseScenarioId id)
        {
            for (int i = 0; i < definitions.Length; i++)
                if (definitions[i].Id == id)
                    return definitions[i];
            return null;
        }
    }

    /// 정상 로비 시작을 그대로 거친 뒤 선택한 상황의 판 한정 스냅샷과 연출을 적용한다.
    [DisallowMultipleComponent]
    public sealed class DebugShowcaseScenarioController : MonoBehaviour
    {
        public const int CascadeInkDropCount = 3;
        public const int CascadeFinalLivingPlayers = 12;

        readonly List<PlayerController> livingPlayers =
            new(GameManager.MaxLivingPlayers);

        GameManager manager;
        DebugShowcaseScenarioDefinition preparedScenario;
        Coroutine effectsRoutine;

        /// SetState(Playing)이 정상 성장 스냅샷을 만든 직후, 플레이어 체력을
        /// 초기화하기 전에 이 판에서만 사용할 시나리오 성장값으로 교체한다.
        public void PrepareSelectedRun(GameManager owner)
        {
            manager = owner;
            preparedScenario = GameManager.DebugToolsAvailable
                ? DebugShowcaseScenarioProfile.SelectedDefinition
                : null;
            if (preparedScenario == null)
                return;

            RunGrowthController growth =
                owner != null ? owner.GetComponent<RunGrowthController>() : null;
            growth ??= RunGrowthController.Instance;
            growth?.DebugApplyPermanentSnapshot(
                preparedScenario.CreateGrowthSnapshot());
        }

        /// 모든 플레이어의 BeginFromLobby와 Score 원점 설정이 끝난 뒤 호출한다.
        public void BeginPreparedRun()
        {
            if (!GameManager.DebugToolsAvailable ||
                manager == null ||
                preparedScenario == null ||
                manager.State != GameState.Playing)
                return;

            ScoreManager.Instance?.InvalidateCurrentRunForRecords();
            manager.DebugTeleportToHeight(preparedScenario.TargetHeight);
            CreateStartingSwarm(preparedScenario.DesiredLivingPlayers);

            if (effectsRoutine != null)
                StopCoroutine(effectsRoutine);
            effectsRoutine = StartCoroutine(ApplyOpeningEffects(preparedScenario));
        }

        void CreateStartingSwarm(int desiredLivingPlayers)
        {
            CreateSwarmTowardCount(desiredLivingPlayers);
        }

        IEnumerator ApplyOpeningEffects(
            DebugShowcaseScenarioDefinition scenario)
        {
            while (manager != null &&
                   manager.State == GameState.Playing &&
                   !manager.IsGameplayTicking)
                yield return null;
            if (manager == null || manager.State != GameState.Playing)
                yield break;

            yield return new WaitForSeconds(0.18f);
            if (!manager.IsGameplayTicking)
                yield break;

            GameFeedbackController.Instance?.ShowZone(
                scenario.Title,
                scenario.BannerSubtitle);

            if (scenario.ShieldAllPlayers)
            {
                manager.GetLivingPlayersNonAlloc(livingPlayers);
                for (int i = 0; i < livingPlayers.Count; i++)
                    livingPlayers[i]?.TryGrantShield();
            }

            if (scenario.FlipWind)
                WindWeatherController.Instance?.DebugFlipDirection();
            if (scenario.SpawnWindPlatform)
                RestPlatformSpawner.Instance?.DebugSpawnWindNearPlayer();
            if (scenario.TriggerUpdraft)
                WindWeatherController.Instance?.DebugTriggerUpdraft();
            if (scenario.SpawnHaetae)
            {
                (ObstacleSpawner.Instance ??
                 FindFirstObjectByType<ObstacleSpawner>())?.DebugSpawnHaetae();
            }

            if (scenario.RunInkSwarmCascade)
            {
                // 순간이동 직후 추락하기 전에 첫 상승을 시작한다. 천하수 배경은
                // 먹떼가 오르는 동안 1초 교차 전환으로 함께 드러난다.
                ProtectLivingPlayers(2f);
                yield return RunInkSwarmCascade();
                effectsRoutine = null;
                yield break;
            }

            PlayerController target = manager.HighestLivingPlayer;
            if (scenario.ApplyGoldenBrush && target != null)
                ItemEffect.Apply(ItemType.GoldenBrush, target);

            if (scenario.ApplyInkDrop && target != null)
            {
                yield return new WaitForSeconds(0.32f);
                if (manager != null && manager.IsGameplayTicking)
                    ItemEffect.Apply(ItemType.InkDrop, target);
            }

            effectsRoutine = null;
        }

        /// 천하수 시나리오는 먹물방울 3회 사이에 먹분신 픽업을 끼워 넣어
        /// 2마리에서 12마리까지 실제 아이템 배관으로 불어난다.
        IEnumerator RunInkSwarmCascade()
        {
            ProtectLivingPlayers(10f);
            int[] clonePickupsAfterDrop = { 2, 2, 1 };
            int[] livingTargets = { 6, 10, CascadeFinalLivingPlayers };

            for (int round = 0; round < CascadeInkDropCount; round++)
            {
                if (manager == null || !manager.IsGameplayTicking)
                    yield break;

                ApplyToHighest(ItemType.InkDrop);
                yield return new WaitForSeconds(0.82f);

                for (int pickup = 0;
                     pickup < clonePickupsAfterDrop[round];
                     pickup++)
                {
                    if (manager == null || !manager.IsGameplayTicking)
                        yield break;
                    ApplyToHighest(ItemType.InkClone);
                    ProtectLivingPlayers(3f);
                    yield return new WaitForSeconds(0.42f);
                }

                // 화면 가장자리 때문에 실제 아이템 생성이 일부 실패하더라도
                // 다른 생존자를 기준으로 빈 슬롯을 찾아 촬영 인원을 맞춘다.
                CreateSwarmTowardCount(livingTargets[round]);
                ProtectLivingPlayers(3f);
                if (round + 1 < CascadeInkDropCount)
                    yield return new WaitForSeconds(0.72f);
            }

            GameFeedbackController.Instance?.ShowZone(
                "먹떼 열두 마리",
                "세 번의 50m 상승 뒤에도 분신과 2단도약이 이어집니다");
        }

        void ApplyToHighest(ItemType type)
        {
            PlayerController target = manager != null
                ? manager.HighestLivingPlayer
                : null;
            if (target != null)
                ItemEffect.Apply(type, target);
        }

        void CreateSwarmTowardCount(int targetCount)
        {
            if (manager == null)
                return;

            int attemptsRemaining = GameManager.MaxLivingPlayers * 2;
            bool spreadAcrossRows = false;
            while (manager.LivingPlayerCount < targetCount &&
                   attemptsRemaining-- > 0)
            {
                manager.GetLivingPlayersNonAlloc(livingPlayers);
                bool created = false;
                for (int i = 0;
                     i < livingPlayers.Count &&
                     manager.LivingPlayerCount < targetCount;
                     i++)
                {
                    PlayerController source = livingPlayers[i];
                    if (source == null || source.IsDead ||
                        !manager.TryCreateInkClone(source))
                        continue;
                    created = true;
                }
                if (!created)
                {
                    // 세로 화면은 같은 높이에 약 7마리까지만 안전하게 들어간다.
                    // 한 번만 두 행으로 펼쳐 10마리 이상의 분신도 화면 안에서 읽히게 한다.
                    if (spreadAcrossRows || !SpreadSwarmAcrossTwoRows())
                        break;
                    spreadAcrossRows = true;
                }
            }
        }

        bool SpreadSwarmAcrossTwoRows()
        {
            if (manager == null)
                return false;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            if (livingPlayers.Count < 2)
                return false;

            PlayerController highest = manager.HighestLivingPlayer;
            if (highest == null)
                return false;
            float topRowY = highest.transform.position.y;
            const float RowSeparation = 1.65f;
            for (int i = 0; i < livingPlayers.Count; i++)
            {
                PlayerController player = livingPlayers[i];
                if (player == null || player.IsDead)
                    continue;
                Rigidbody2D body = player.Body;
                Vector2 velocity = body != null
                    ? body.linearVelocity
                    : Vector2.zero;
                float targetY = topRowY - (i % 2) * RowSeparation;
                player.DebugTeleportBy(
                    Vector2.up * (targetY - player.transform.position.y));
                // DebugTeleportBy가 위치 안정화를 위해 속도를 비우므로, 연쇄 상승의
                // 흐름은 끊지 않도록 시나리오에서만 직전 속도를 되돌린다.
                if (body != null)
                    body.linearVelocity = velocity;
            }
            Physics2D.SyncTransforms();
            return true;
        }

        void ProtectLivingPlayers(float seconds)
        {
            if (manager == null)
                return;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            for (int i = 0; i < livingPlayers.Count; i++)
                livingPlayers[i]?.GrantObstacleProtection(seconds);
        }
    }
}
