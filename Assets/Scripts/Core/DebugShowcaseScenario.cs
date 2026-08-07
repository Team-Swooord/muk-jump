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
            bool shieldAllPlayers = false)
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
            int remainingAttempts = GameManager.MaxLivingPlayers;
            while (manager.LivingPlayerCount < desiredLivingPlayers &&
                   remainingAttempts-- > 0)
            {
                PlayerController source = manager.HighestLivingPlayer;
                if (source == null || !manager.TryCreateInkClone(source))
                    break;
            }
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
    }
}
