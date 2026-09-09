using System.Collections;
using System.Collections.Generic;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;
using UnityEngine;

namespace MukJump.Core
{
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
                 FindAnyObjectByType<ObstacleSpawner>())?.DebugSpawnHaetae();
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
