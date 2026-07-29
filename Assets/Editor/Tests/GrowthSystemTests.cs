using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    /// 성장 두루마리의 규칙, 모달 소유권, 풀링, 피해 우선순위와
    /// 고정 비용 VFX 계약을 함께 지키는 회귀 테스트.
    public sealed class GrowthSystemTests
    {
        const string ScrollAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_scroll.png";
        const string VitalityAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_vitality.png";
        const string JumpAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_jump.png";
        const string InkCapacityAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_ink_capacity.png";
        const string InkRecoveryAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_ink_regen.png";
        const string PlatformAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_platform.png";
        const string GuardAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_guard.png";
        const string FortuneAssetPath =
            "Assets/Resources/MukJump/UI/Growth/growth_fortune.png";

        readonly List<UnityEngine.Object> cleanup = new();
        readonly List<Camera> retaggedMainCameras = new();
        float originalTimeScale;
        float originalFixedDeltaTime;
        bool originalAudioPause;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            originalAudioPause = AudioListener.pause;
            PlatformCollider.RuntimeLifetimeMultiplier = 1f;
            ClearActivePlatforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();

            for (int i = 0; i < retaggedMainCameras.Count; i++)
                if (retaggedMainCameras[i] != null)
                    retaggedMainCameras[i].gameObject.tag = "MainCamera";
            retaggedMainCameras.Clear();

            // GameManager.OnDisable이 일시정지 상태를 복구한 뒤 원래 테스트 환경으로 되돌린다.
            AudioListener.pause = originalAudioPause;
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            PlatformCollider.RuntimeLifetimeMultiplier = 1f;
            ClearActivePlatforms();
        }

        [Test]
        public void GrowthPauseHasIndependentOwnershipFromUserPause()
        {
            var manager = CreatePlayingManager(out _);

            Assert.That(manager.BeginGrowthChoicePause(), Is.True);
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.GrowthChoice));
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(manager.ResumeGame(), Is.False,
                "일시정지판의 재개 입력이 성장 선택 모달을 닫으면 안 됩니다.");
            Assert.That(manager.PauseGame(), Is.False,
                "성장 선택 중 사용자 일시정지판이 겹쳐 열리면 안 됩니다.");
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.EndGrowthChoicePause(), Is.True);
            Assert.That(manager.IsPaused, Is.False);

            Assert.That(manager.PauseGame(), Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.UserMenu));
            Assert.That(manager.EndGrowthChoicePause(), Is.False,
                "성장 모달의 닫기 경로가 사용자 일시정지를 풀면 안 됩니다.");
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(manager.ResumeGame(), Is.True);
            Assert.That(manager.PauseReason, Is.EqualTo(GameplayPauseReason.None));
        }

        [Test]
        public void GrowthRequestPausesPlayingUntilAppliedChoiceFinishes()
        {
            var manager = CreatePlayingManager(out var growth);
            int requestCount = 0;
            GrowthUpgradeType? selected = null;
            growth.ChoiceRequested += () => requestCount++;
            growth.UpgradeSelected += type => selected = type;

            Assert.That(growth.RequestChoice(), Is.True);
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(growth.HasPendingChoice, Is.True);
            Assert.That(manager.PauseReason,
                Is.EqualTo(GameplayPauseReason.GrowthChoice));
            Assert.That(Time.timeScale, Is.Zero);

            ForceCurrentOffers(growth, GrowthUpgradeType.Vitality);
            Assert.That(growth.TrySelectUpgrade(GrowthUpgradeType.Vitality), Is.True);
            Assert.That(selected, Is.EqualTo(GrowthUpgradeType.Vitality));
            Assert.That(growth.VitalityLevel, Is.EqualTo(1));
            Assert.That(growth.VitalityCharges, Is.EqualTo(1));
            Assert.That(growth.HasSelectedPendingChoice, Is.True);
            Assert.That(manager.IsPaused, Is.True,
                "선택 직후가 아니라 두루마리 닫힘 연출 뒤에 시간이 재개되어야 합니다.");

            Assert.That(growth.FinishChoice(), Is.True);
            Assert.That(growth.HasPendingChoice, Is.False);
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(manager.IsGameplayTicking, Is.True);
        }

        [Test]
        public void GrowthOffersAreThreeUniqueAndGuaranteeBodyAndDrawing()
        {
            GameplayRandom.ResetSession(20260729);
            CreatePlayingManager(out var growth);

            Assert.That(growth.RequestChoice(), Is.True);
            var offers = growth.CurrentOffers.ToArray();

            Assert.That(offers, Has.Length.EqualTo(3));
            Assert.That(offers.Distinct().Count(), Is.EqualTo(3),
                "한 선택판에 같은 성장이 중복되면 안 됩니다.");
            Assert.That(offers.Any(IsBodyUpgrade), Is.True,
                "세 선택지 중 몸 성장이 하나는 보장되어야 합니다.");
            Assert.That(offers.Any(IsDrawingUpgrade), Is.True,
                "세 선택지 중 드로잉 성장이 하나는 보장되어야 합니다.");
            Assert.That(growth.CancelChoice(), Is.True);
        }

        [Test]
        public void LobbyTrainingFocusIsGuaranteedInFirstGrowthChoice()
        {
            Assert.That(
                RoguelikeGrowthCatalog.TryGetDefinition(
                    GrowthUpgradeType.ItemFortune,
                    out var focusedDefinition),
                Is.True);
            Assert.That(
                GrowthFocusProfile.SetForTests(focusedDefinition.Id),
                Is.True);

            try
            {
                GameplayRandom.ResetSession(20260731);
                CreatePlayingManager(out var growth);

                Assert.That(growth.RequestChoice(), Is.True);
                var offers = growth.CurrentOffers.ToArray();
                CollectionAssert.Contains(offers, GrowthUpgradeType.ItemFortune,
                    "로비에서 고른 수련 방향은 첫 두루마리 한 칸을 보장해야 합니다.");
                Assert.That(offers, Has.Length.EqualTo(3));
                Assert.That(offers.Any(IsBodyUpgrade), Is.True);
                Assert.That(offers.Any(IsDrawingUpgrade), Is.True);
                Assert.That(growth.CancelChoice(), Is.True);
            }
            finally
            {
                GrowthFocusProfile.ResetForTests();
            }
        }

        [Test]
        public void GrowthRejectsNonOfferAndExcludesMaxedUpgrades()
        {
            GameplayRandom.ResetSession(20260730);
            CreatePlayingManager(out var growth);
            SetProperty(growth, "VitalityLevel",
                RunGrowthController.MaxVitalityLevel);
            SetProperty(growth, "InkCapacityLevel",
                RunGrowthController.MaxInkCapacityLevel);
            SetProperty(growth, "PlatformSlotsLevel",
                RunGrowthController.MaxPlatformSlotsLevel);

            Assert.That(growth.RequestChoice(), Is.True);
            var offers = growth.CurrentOffers.ToArray();
            CollectionAssert.DoesNotContain(offers, GrowthUpgradeType.Vitality);
            CollectionAssert.DoesNotContain(offers, GrowthUpgradeType.InkCapacity);
            CollectionAssert.DoesNotContain(offers, GrowthUpgradeType.PlatformSlots);

            GrowthUpgradeType nonOffer = Enum
                .GetValues(typeof(GrowthUpgradeType))
                .Cast<GrowthUpgradeType>()
                .First(type =>
                    growth.CanSelectUpgrade(type) &&
                    !offers.Contains(type));
            int levelBefore = growth.GetLevel(nonOffer);
            Assert.That(growth.TrySelectUpgrade(nonOffer), Is.False,
                "현재 두루마리에 표시되지 않은 성장을 강제로 적용하면 안 됩니다.");
            Assert.That(growth.GetLevel(nonOffer), Is.EqualTo(levelBefore));
            Assert.That(growth.HasSelectedPendingChoice, Is.False);
            Assert.That(growth.CancelChoice(), Is.True);
        }

        [Test]
        public void NewGrowthMultipliersReachTheirDocumentedCaps()
        {
            CreatePlayingManager(out var growth);

            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.InkCapacity,
                RunGrowthController.MaxInkCapacityLevel);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.InkRecovery,
                RunGrowthController.MaxInkRecoveryLevel);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.PlatformLifetime,
                RunGrowthController.MaxPlatformLifetimeLevel);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.PlatformSlots,
                RunGrowthController.MaxPlatformSlotsLevel);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.StrokeGuard,
                RunGrowthController.MaxStrokeGuardLevel);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.ItemFortune,
                RunGrowthController.MaxItemFortuneLevel);

            Assert.That(growth.InkCapacityMultiplier,
                Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(growth.InkRecoveryMultiplier,
                Is.EqualTo(1.48f).Within(0.0001f));
            Assert.That(growth.PlatformLifetimeMultiplier,
                Is.EqualTo(1.3f).Within(0.0001f));
            Assert.That(growth.AdditionalPlatformSlots, Is.EqualTo(1));
            Assert.That(growth.NewPlatformsHaveStrokeGuard, Is.True);
            Assert.That(growth.ItemSpacingMultiplier,
                Is.EqualTo(0.79f).Within(0.0001f));

            foreach (GrowthUpgradeType type in new[]
                     {
                         GrowthUpgradeType.InkCapacity,
                         GrowthUpgradeType.InkRecovery,
                         GrowthUpgradeType.PlatformLifetime,
                         GrowthUpgradeType.PlatformSlots,
                         GrowthUpgradeType.StrokeGuard,
                         GrowthUpgradeType.ItemFortune,
                     })
            {
                Assert.That(growth.CanSelectUpgrade(type), Is.False,
                    $"{type} 최대 단계가 선택 후보에 다시 들어가면 안 됩니다.");
            }
        }

        [Test]
        public void StrokeCaptureAppliesCapacityDeltaRecoveryAndRunReset()
        {
            CreatePlayingManager(out var growth);
            var host = Track(new GameObject("GrowthStrokeCapture"));
            var stroke = host.AddComponent<StrokeCapture>();
            SetField(stroke, "inkCapacity", 12f);
            SetField(stroke, "inkRegenPerSecond", 3f);
            SetField(stroke, "appliedInkCapacity", 12f);
            SetField(stroke, "ink", 5f);
            Invoke(stroke, "TryBindGrowthController");

            ApplyChoice(growth, GrowthUpgradeType.InkCapacity);

            Assert.That(stroke.EffectiveInkCapacity,
                Is.EqualTo(13.2f).Within(0.0001f));
            Assert.That(GetField<float>(stroke, "ink"),
                Is.EqualTo(6.2f).Within(0.0001f),
                "용량 성장 시 늘어난 1.2만큼 현재 먹도 즉시 충전되어야 합니다.");

            ApplyChoice(growth, GrowthUpgradeType.InkRecovery);
            Assert.That(stroke.EffectiveInkRegenPerSecond,
                Is.EqualTo(3.36f).Within(0.0001f));

            SetField(stroke, "ink", 1f);
            SetField(stroke, "inkReserve", 4f);
            SetField(stroke, "unlimitedInkUntil", Time.time + 10f);
            Invoke(growth, "ResetRun");

            Assert.That(stroke.EffectiveInkCapacity,
                Is.EqualTo(12f).Within(0.0001f));
            Assert.That(stroke.EffectiveInkRegenPerSecond,
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(GetField<float>(stroke, "ink"),
                Is.EqualTo(12f).Within(0.0001f));
            Assert.That(GetField<float>(stroke, "inkReserve"), Is.Zero);
            Assert.That(GetField<float>(stroke, "unlimitedInkUntil"), Is.Zero);
        }

        [Test]
        public void PlatformGrowthCombinesZoneLifetimeBudgetAndOneHitGuard()
        {
            CreatePlayingManager(out var growth);
            ApplyRepeatedChoice(
                growth,
                GrowthUpgradeType.PlatformLifetime,
                RunGrowthController.MaxPlatformLifetimeLevel);
            ApplyChoice(growth, GrowthUpgradeType.PlatformSlots);
            ApplyChoice(growth, GrowthUpgradeType.StrokeGuard);
            PlatformCollider.RuntimeLifetimeMultiplier = 0.72f;

            var platforms = new List<PlatformCollider>();
            for (int i = 0; i < 6; i++)
            {
                var points = new List<Vector2>
                {
                    new(i, 0f),
                    new(i + 1f, 0f),
                };
                var platform = PlatformCollider.Spawn(points);
                Track(platform.gameObject);
                platforms.Add(platform);
            }

            float effectiveLifetime = GetProperty<float>(
                platforms[^1], "EffectiveLifetime");
            Assert.That(effectiveLifetime,
                Is.EqualTo(4.5f * 0.72f * 1.3f).Within(0.0001f),
                "맵 구간 배율과 성장 배율은 서로 덮지 말고 곱해져야 합니다.");

            Assert.That(platforms[0].GetComponent<EdgeCollider2D>().enabled, Is.False,
                "기본 4칸+성장 1칸을 넘긴 가장 오래된 발판은 물리 예산에서 빠져야 합니다.");
            for (int i = 1; i < platforms.Count; i++)
                Assert.That(platforms[i].GetComponent<EdgeCollider2D>().enabled, Is.True);

            var guarded = platforms[^1];
            var guardedEdge = guarded.GetComponent<EdgeCollider2D>();
            Assert.That(guarded.HasStrokeGuard, Is.True);
            Assert.That(guarded.BreakFromHazard(), Is.True);
            Assert.That(guarded.HasStrokeGuard, Is.False);
            Assert.That(guarded.IsTemporaryDrawnPlatform, Is.True);
            Assert.That(guardedEdge.enabled, Is.True,
                "첫 낙묵석은 수호만 소모하고 발판을 남겨야 합니다.");

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "Destroy may not be called from edit mode"));
            Assert.That(guarded.BreakFromHazard(), Is.True);
            Assert.That(guarded.IsTemporaryDrawnPlatform, Is.False);
            Assert.That(guardedEdge.enabled, Is.False,
                "두 번째 낙묵석은 수호가 사라진 발판을 제거해야 합니다.");
        }

        [Test]
        public void PlatformLifetimeGrowthPreservesExistingPlatformProgress()
        {
            CreatePlayingManager(out var growth);
            var platform = PlatformCollider.Spawn(new List<Vector2>
            {
                Vector2.zero,
                Vector2.right,
            });
            Track(platform.gameObject);
            SetField(platform, "age", 3.6f);
            SetField(platform, "lastEffectiveLifetime", 4.5f);

            ApplyChoice(growth, GrowthUpgradeType.PlatformLifetime);
            float effectiveLifetime = GetProperty<float>(
                platform, "EffectiveLifetime");
            Invoke(platform, "SynchronizeLifetimeProgress", effectiveLifetime);
            float migratedAge = GetField<float>(platform, "age");
            Assert.That(effectiveLifetime, Is.EqualTo(4.95f).Within(0.0001f));
            Assert.That(
                migratedAge / effectiveLifetime,
                Is.EqualTo(0.8f).Within(0.001f),
                "이미 존재하는 발판은 수명 성장 뒤에도 마르는 진행률을 유지해야 합니다.");
        }

        [Test]
        public void ItemFortuneChangesFixedTenMeterSpacingToNinePointThree()
        {
            CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.ItemFortune);
            var host = Track(new GameObject("FortuneItemSpawner"));
            var spawner = host.AddComponent<ItemSpawner>();
            SetField(spawner, "verticalSpacing", new Vector2(10f, 10f));

            float spacing = (float)Invoke(spawner, "NextSpacing");

            Assert.That(spacing, Is.EqualTo(9.3f).Within(0.0001f));
        }

        [Test]
        public void PendingGrowthChoiceBlocksQueuedObstacleHit()
        {
            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var player = CreatePlayer(
                "PausedGrowthCollisionTarget",
                withEffectView: true);
            manager.RegisterPlayer(player);
            player.GrantShield();
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);

            Assert.That(growth.RequestChoice(), Is.True);
            Assert.That(manager.IsGameplayTicking, Is.False);

            // 두루마리 OnTrigger가 시간을 멈춘 같은 물리 스텝의 잔여 충돌을 흉내 낸다.
            player.TakeHit();

            Assert.That(player.IsDead, Is.False);
            Assert.That(player.HasShield, Is.True,
                "선택판 뒤에서 방어막이 소모되면 안 됩니다.");
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "선택판 뒤에서 공유 먹두께가 소모되면 안 됩니다.");
            Assert.That(growth.CancelChoice(), Is.True);
        }

        [Test]
        public void CancelledGrowthChoiceImmediatelyReleasesModalRaycasts()
        {
            CreatePlayingManager(out var growth);
            var host = Track(new GameObject("CancelledGrowthChoiceView"));
            var view = host.AddComponent<GrowthChoiceView>();
            Invoke(view, "BuildIfNeeded");
            Invoke(view, "BindController", growth);

            Assert.That(growth.RequestChoice(), Is.True);
            SetProperty(view, "IsOpen", true);
            Invoke(view, "ApplyRevealPose", 1f);
            var canvasGroup = host.transform
                .Find("GrowthChoiceCanvas")
                .GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;

            Assert.That(growth.CancelChoice(), Is.True);

            Assert.That(view.IsOpen, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False,
                "게임 상태가 선택을 취소하면 두루마리가 다음 화면 입력을 막으면 안 됩니다.");
            Assert.That(canvasGroup.interactable, Is.False);
        }

        [Test]
        public void VitalityIsSharedCappedAndNeverBecomesNegative()
        {
            CreatePlayingManager(out var growth);
            var player = CreatePlayer("VitalityTarget");

            for (int i = 0; i < RunGrowthController.MaxVitalityLevel; i++)
                ApplyChoice(growth, GrowthUpgradeType.Vitality);

            Assert.That(growth.VitalityLevel,
                Is.EqualTo(RunGrowthController.MaxVitalityLevel));
            Assert.That(growth.VitalityCharges,
                Is.EqualTo(RunGrowthController.MaxVitalityLevel));
            Assert.That(growth.CanSelectUpgrade(GrowthUpgradeType.Vitality),
                Is.False);

            for (int expected = RunGrowthController.MaxVitalityLevel - 1;
                 expected >= 0;
                 expected--)
            {
                Assert.That(growth.TryAbsorbObstacleHit(player), Is.True);
                Assert.That(growth.VitalityCharges, Is.EqualTo(expected));
            }

            Assert.That(growth.TryAbsorbObstacleHit(player), Is.False);
            Assert.That(growth.VitalityCharges, Is.Zero);
        }

        [Test]
        public void ShieldIsConsumedBeforeSharedVitalityInTakeHit()
        {
            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var player = CreatePlayer("ShieldPriorityTarget", withEffectView: true);
            manager.RegisterPlayer(player);
            Vector3 rootScale = player.transform.localScale;
            var collider = player.GetComponent<CircleCollider2D>();
            float colliderRadius = collider.radius;
            Vector2 colliderOffset = collider.offset;

            player.GrantShield();
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
            player.TakeHit();

            Assert.That(player.HasShield, Is.False);
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "방어막이 남아 있을 때 공유 먹두께를 먼저 쓰면 안 됩니다.");
            Assert.That(player.IsDead, Is.False);

            SetField(player, "damageInvulnerableUntil", Time.time - 1f);
            player.TakeHit();

            Assert.That(growth.VitalityCharges, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(colliderRadius));
            Assert.That(collider.offset, Is.EqualTo(colliderOffset));
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void FallDeathDoesNotConsumeVitalityCharge()
        {
            RetagExistingMainCameras();
            var cameraObject = Track(new GameObject("GrowthFallCamera"));
            cameraObject.tag = "MainCamera";
            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 5f;

            var manager = CreatePlayingManager(out var growth);
            ApplyChoice(growth, GrowthUpgradeType.Vitality);
            var fallingPlayer = CreatePlayer("FallingGrowthPlayer");
            var survivingPlayer = CreatePlayer("SurvivingGrowthPlayer");
            manager.RegisterPlayer(fallingPlayer);
            manager.RegisterPlayer(survivingPlayer);
            fallingPlayer.transform.position = Vector3.down * 20f;
            SetField(fallingPlayer, "cam", worldCamera);
            SetField(fallingPlayer, "camHalfHeight", worldCamera.orthographicSize);

            Invoke(fallingPlayer, "FixedUpdate");

            Assert.That(fallingPlayer.IsDead, Is.True);
            Assert.That(growth.VitalityCharges, Is.EqualTo(1),
                "먹두께는 장애물 완충이며 화면 아래 추락 목숨까지 막으면 안 됩니다.");
            Assert.That(manager.State, Is.EqualTo(GameState.Playing),
                "다른 먹분신이 살아 있으면 추락 테스트도 게임을 계속해야 합니다.");
        }

        [Test]
        public void JumpGrowthAddsFourPercentPerLevelAndCapsAtTwentyPercent()
        {
            CreatePlayingManager(out var growth);

            ApplyChoice(growth, GrowthUpgradeType.JumpPower);
            Assert.That(growth.JumpLevel, Is.EqualTo(1));
            Assert.That(growth.JumpPowerMultiplier, Is.EqualTo(1.04f).Within(0.0001f));

            for (int i = 1; i < RunGrowthController.MaxJumpLevel; i++)
                ApplyChoice(growth, GrowthUpgradeType.JumpPower);

            Assert.That(growth.JumpLevel,
                Is.EqualTo(RunGrowthController.MaxJumpLevel));
            Assert.That(growth.JumpPowerMultiplier,
                Is.EqualTo(1.20f).Within(0.0001f));
            Assert.That(growth.CanSelectUpgrade(GrowthUpgradeType.JumpPower),
                Is.False);
        }

        [Test]
        public void ChoiceViewBuildsBlockingScrollWithOneToThreeDynamicCards()
        {
            var vitalitySprite = LoadGrowthSprite(
                VitalityAssetPath, "MukJump/UI/Growth/growth_vitality");
            var jumpSprite = LoadGrowthSprite(
                JumpAssetPath, "MukJump/UI/Growth/growth_jump");
            var capacitySprite = LoadGrowthSprite(
                InkCapacityAssetPath, "MukJump/UI/Growth/growth_ink_capacity");
            var recoverySprite = LoadGrowthSprite(
                InkRecoveryAssetPath, "MukJump/UI/Growth/growth_ink_regen");
            var platformSprite = LoadGrowthSprite(
                PlatformAssetPath, "MukJump/UI/Growth/growth_platform");
            var guardSprite = LoadGrowthSprite(
                GuardAssetPath, "MukJump/UI/Growth/growth_guard");
            var fortuneSprite = LoadGrowthSprite(
                FortuneAssetPath, "MukJump/UI/Growth/growth_fortune");
            var scrollSprite = LoadGrowthSprite(
                ScrollAssetPath, "MukJump/UI/Growth/growth_scroll");
            Assert.That(scrollSprite, Is.Not.Null);

            var host = Track(new GameObject("GrowthChoiceViewHost"));
            var view = host.AddComponent<GrowthChoiceView>();
            view.SetSprites(
                vitalitySprite,
                jumpSprite,
                capacitySprite,
                recoverySprite,
                platformSprite,
                platformSprite,
                guardSprite,
                fortuneSprite);
            Invoke(view, "BuildIfNeeded");
            Invoke(view, "BuildIfNeeded");
            Invoke(view, "BindButtons");

            Assert.That(CountDirectChildren(
                host.transform, "GrowthChoiceCanvas"), Is.EqualTo(1));
            var canvasRoot = host.transform.Find("GrowthChoiceCanvas");
            Assert.That(canvasRoot, Is.Not.Null);
            Assert.That(canvasRoot.GetComponent<Canvas>().sortingOrder, Is.EqualTo(3000));
            Assert.That(canvasRoot.GetComponent<Canvas>().pixelPerfect, Is.True);

            var backdrop = canvasRoot.Find("InkWash") as RectTransform;
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(backdrop.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backdrop.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(backdrop.GetComponent<Image>().raycastTarget, Is.True);

            var panel = canvasRoot.Find(
                "SafeAreaRoot/GrowthScrollPopup") as RectTransform;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Find("ScrollBody/ScrollPaper"), Is.Not.Null);
            Assert.That(panel.Find("ScrollBody/PaperCore"), Is.Not.Null);
            Assert.That(panel.Find("TopRoll"), Is.Not.Null);
            Assert.That(panel.Find("BottomRoll"), Is.Not.Null);
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(900f, 1480f)));

            var content = panel.Find("GrowthContent");
            Assert.That(content, Is.Not.Null);
            AssertReadableGrowthText(
                content.Find("Title")?.GetComponent<Text>(), 64);
            AssertReadableGrowthText(
                content.Find("Hint")?.GetComponent<Text>(), 34);
            AssertReadableGrowthText(
                content.Find("FooterHint")?.GetComponent<Text>(), 28);

            var reveal = (IEnumerator)Invoke(view, "RevealRoutine");
            Assert.That(reveal.MoveNext(), Is.True);
            var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
            Assert.That(rootGroup.blocksRaycasts, Is.True,
                "모달이 열리는 첫 프레임부터 뒤 게임 입력을 막아야 합니다.");

            CreatePlayingManager(out var growth);
            Assert.That(growth.RequestChoice(), Is.True);
            SetField(view, "boundController", growth);
            SetProperty(view, "IsOpen", true);
            rootGroup.interactable = true;

            var firstCard = content.Find("GrowthChoice1");
            var secondCard = content.Find("GrowthChoice2");
            var thirdCard = content.Find("GrowthChoice3");
            var firstButton = firstCard.GetComponent<Button>();
            var secondButton = secondCard.GetComponent<Button>();
            var thirdButton = thirdCard.GetComponent<Button>();
            Assert.That(firstButton, Is.Not.Null);
            Assert.That(secondButton, Is.Not.Null);
            Assert.That(thirdButton, Is.Not.Null);
            Assert.That(firstButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(secondButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(thirdButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(((RectTransform)firstCard).sizeDelta,
                Is.EqualTo(new Vector2(740f, 250f)));

            AssertReadableGrowthText(
                firstCard.Find("Name")?.GetComponent<Text>(), 44);
            AssertReadableGrowthText(
                firstCard.Find("Status")?.GetComponent<Text>(), 29);
            AssertReadableGrowthText(
                firstCard.Find("Effect")?.GetComponent<Text>(), 31);
            AssertVerticalGapAtLeast(
                firstCard.Find("Name") as RectTransform,
                firstCard.Find("Status") as RectTransform,
                8f);
            AssertVerticalGapAtLeast(
                firstCard.Find("Status") as RectTransform,
                firstCard.Find("Effect") as RectTransform,
                8f);

            ForceCurrentOffers(
                growth,
                GrowthUpgradeType.Vitality,
                GrowthUpgradeType.InkCapacity,
                GrowthUpgradeType.ItemFortune);
            Invoke(view, "RefreshCards");

            Assert.That(firstCard.gameObject.activeSelf, Is.True);
            Assert.That(secondCard.gameObject.activeSelf, Is.True);
            Assert.That(thirdCard.gameObject.activeSelf, Is.True);
            AssertVerticalGapAtLeast(
                firstCard as RectTransform,
                secondCard as RectTransform,
                45f);
            AssertVerticalGapAtLeast(
                secondCard as RectTransform,
                thirdCard as RectTransform,
                45f);
            Assert.That(firstCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(vitalitySprite));
            Assert.That(secondCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(capacitySprite));
            Assert.That(thirdCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(fortuneSprite));
            Assert.That(firstCard.Find("Name").GetComponent<Text>().text,
                Is.EqualTo("먹두께"));
            Assert.That(secondCard.Find("Name").GetComponent<Text>().text,
                Is.EqualTo("큰 벼루"));
            Assert.That(thirdCard.Find("Name").GetComponent<Text>().text,
                Is.EqualTo("길운"));

            foreach (GrowthUpgradeType type in Enum.GetValues(
                         typeof(GrowthUpgradeType)))
            {
                ForceCurrentOffers(growth, type);
                Invoke(view, "RefreshCards");
                Canvas.ForceUpdateCanvases();
                AssertTextFitsRect(firstCard.Find("Name")?.GetComponent<Text>());
                AssertTextFitsRect(firstCard.Find("Status")?.GetComponent<Text>());
                AssertTextFitsRect(firstCard.Find("Effect")?.GetComponent<Text>());
            }

            ForceCurrentOffers(
                growth,
                GrowthUpgradeType.JumpPower,
                GrowthUpgradeType.PlatformLifetime);
            Invoke(view, "RefreshCards");
            Assert.That(firstCard.gameObject.activeSelf, Is.True);
            Assert.That(secondCard.gameObject.activeSelf, Is.True);
            Assert.That(thirdCard.gameObject.activeSelf, Is.False);
            Assert.That(firstCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(jumpSprite));
            Assert.That(secondCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(platformSprite));
            Assert.That(((RectTransform)firstCard).anchoredPosition.y,
                Is.EqualTo(147.5f).Within(0.0001f));
            Assert.That(((RectTransform)secondCard).anchoredPosition.y,
                Is.EqualTo(-147.5f).Within(0.0001f));

            ForceCurrentOffers(growth, GrowthUpgradeType.StrokeGuard);
            Invoke(view, "RefreshCards");
            Assert.That(firstCard.gameObject.activeSelf, Is.True);
            Assert.That(secondCard.gameObject.activeSelf, Is.False);
            Assert.That(thirdCard.gameObject.activeSelf, Is.False);
            Assert.That(firstCard.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(guardSprite));
            Assert.That(((RectTransform)firstCard).anchoredPosition.y,
                Is.Zero.Within(0.0001f));

            rootGroup.interactable = true;
            Invoke(view, "RefreshCards");
            firstCard.GetComponent<Button>().onClick.Invoke();
            Assert.That(growth.StrokeGuardLevel, Is.EqualTo(1),
                "첫 카드 버튼은 자기 카드에 표시된 성장 종류를 선택해야 합니다.");
            Assert.That(growth.HasSelectedPendingChoice, Is.True);
            Assert.That(growth.CancelChoice(), Is.True);
        }

        [Test]
        public void GrowthScrollResponsiveScaleFitsNarrowSafeAreas()
        {
            float standard = InvokeResponsiveScale(new Vector2(1080f, 1920f));
            Assert.That(standard, Is.EqualTo(1f).Within(0.0001f));

            var narrowSafeArea = new Vector2(823f, 1800f);
            float narrow = InvokeResponsiveScale(narrowSafeArea);
            Assert.That(narrow, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(920f * narrow,
                Is.LessThanOrEqualTo(narrowSafeArea.x - 40f + 0.001f));
            Assert.That(1500f * narrow,
                Is.LessThanOrEqualTo(narrowSafeArea.y - 40f + 0.001f));
        }

        [Test]
        public void GrowthSpawnerUsesGuaranteedScheduleAndOneReusablePickup()
        {
            Assert.That(GrowthScrollSpawner.DefaultFirstHeight, Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.DefaultInterval, Is.EqualTo(120f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(44f),
                Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(45f),
                Is.EqualTo(45f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(46f),
                Is.EqualTo(165f));
            Assert.That(GrowthScrollSpawner.NextScheduleAtOrAbove(1000f),
                Is.EqualTo(1005f));

            RetagExistingMainCameras();
            var cameraObject = Track(new GameObject("GrowthSpawnerCamera"));
            cameraObject.tag = "MainCamera";
            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 9.6f;

            var host = Track(new GameObject("GrowthScrollSpawnerHost"));
            host.SetActive(false);
            var spawner = host.AddComponent<GrowthScrollSpawner>();
            var scrollSprite = LoadGrowthSprite(
                ScrollAssetPath, "MukJump/UI/Growth/growth_scroll");
            spawner.Configure(scrollSprite, 45f, 120f);
            SetField(spawner, "worldCamera", worldCamera);
            Invoke(spawner, "EnsurePool");

            Assert.That(spawner.FirstHeight, Is.EqualTo(45f));
            Assert.That(spawner.Interval, Is.EqualTo(120f));
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1));
            Assert.That((bool)Invoke(spawner, "TrySpawn", 45f), Is.True);
            var first = spawner.ActivePickup;
            Assert.That(first, Is.Not.Null);
            Assert.That(spawner.PoolLeasedCount, Is.EqualTo(1));
            Assert.That((bool)Invoke(spawner, "TrySpawn", 165f), Is.False,
                "한 화면에 성장 두루마리를 두 개 이상 대여하면 안 됩니다.");

            Invoke(spawner, "ReleaseActive");
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1));
            Assert.That(spawner.PoolLeasedCount, Is.Zero);
            Assert.That((bool)Invoke(spawner, "TrySpawn", 165f), Is.True);
            Assert.That(spawner.ActivePickup, Is.SameAs(first),
                "두 번째 일정은 Instantiate 대신 같은 한 슬롯 풀을 재사용해야 합니다.");

            Invoke(spawner, "HandleWorldHeightTeleported", 1000);
            Assert.That(spawner.HasActivePickup, Is.False);
            Assert.That(spawner.NextScheduledHeight, Is.EqualTo(1005f));
            Assert.That(spawner.PoolAvailableCount, Is.EqualTo(1),
                "순간이동으로 건너뛴 과거 슬롯을 한꺼번에 생성하면 안 됩니다.");
        }

        [Test]
        public void VitalityPuffIsSingleReusableCloneExcludedVisual()
        {
            var player = CreatePlayer("VitalityPuffPlayer", withEffectView: true);
            var view = player.GetComponent<ItemEffectView>();
            var renderer = player.GetComponent<SpriteRenderer>();
            renderer.sprite = CreateTestSprite();
            Vector3 rootScale = player.transform.localScale;
            var collider = player.GetComponent<CircleCollider2D>();
            float radius = collider.radius;
            Vector2 offset = collider.offset;

            view.PlayVitalityHit();
            view.PlayVitalityHit();

            Assert.That(CountDirectChildren(
                player.transform, "GrowthVitalityPuff"), Is.EqualTo(1));
            var puff = player.transform.Find("GrowthVitalityPuff");
            Assert.That(puff, Is.Not.Null);
            Assert.That(puff.GetComponent<SpriteRenderer>(), Is.Not.Null);

            var lifecycle = (IRuntimeCloneLifecycle)view;
            lifecycle.PrepareForRuntimeClone();
            Assert.That(puff.parent, Is.Null);
            var clone = Track(UnityEngine.Object.Instantiate(player.gameObject));
            Assert.That(clone.transform.Find("GrowthVitalityPuff"), Is.Null,
                "고정 캐시 VFX가 먹분신 수만큼 복제되면 안 됩니다.");
            lifecycle.RestoreAfterRuntimeClone();

            Assert.That(puff.parent, Is.SameAs(player.transform));
            Assert.That(player.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(collider.radius, Is.EqualTo(radius));
            Assert.That(collider.offset, Is.EqualTo(offset));
        }

        GameManager CreatePlayingManager(out RunGrowthController growth)
        {
            var host = Track(new GameObject("GrowthGameManager"));
            var manager = host.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            SetProperty(manager, "State", GameState.Playing);

            growth = host.GetComponent<RunGrowthController>();
            if (growth == null)
                growth = host.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            return manager;
        }

        PlayerController CreatePlayer(
            string objectName,
            bool withEffectView = false)
        {
            var host = Track(new GameObject(objectName));
            host.AddComponent<SpriteRenderer>();
            var body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.2f;
            host.AddComponent<CircleCollider2D>();
            var player = host.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            if (withEffectView)
            {
                var view = host.AddComponent<ItemEffectView>();
                Invoke(view, "Awake");
            }
            return player;
        }

        void ApplyChoice(
            RunGrowthController growth,
            GrowthUpgradeType upgrade)
        {
            Assert.That(growth.RequestChoice(), Is.True);
            ForceCurrentOffers(growth, upgrade);
            Assert.That(growth.TrySelectUpgrade(upgrade), Is.True);
            Assert.That(growth.FinishChoice(), Is.True);
        }

        void ApplyRepeatedChoice(
            RunGrowthController growth,
            GrowthUpgradeType upgrade,
            int count)
        {
            for (int i = 0; i < count; i++)
                ApplyChoice(growth, upgrade);
        }

        static void ForceCurrentOffers(
            RunGrowthController growth,
            params GrowthUpgradeType[] upgrades)
        {
            var field = typeof(RunGrowthController).GetField(
                "currentOffers",
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "RunGrowthController.currentOffers 필드를 찾을 수 없습니다.");
            var offers = field.GetValue(growth) as List<GrowthUpgradeType>;
            Assert.That(offers, Is.Not.Null);
            offers.Clear();
            if (upgrades != null)
                offers.AddRange(upgrades);
        }

        static bool IsBodyUpgrade(GrowthUpgradeType type)
        {
            return type == GrowthUpgradeType.Vitality ||
                   type == GrowthUpgradeType.JumpPower;
        }

        static bool IsDrawingUpgrade(GrowthUpgradeType type)
        {
            return type == GrowthUpgradeType.InkCapacity ||
                   type == GrowthUpgradeType.InkRecovery ||
                   type == GrowthUpgradeType.PlatformLifetime ||
                   type == GrowthUpgradeType.PlatformSlots ||
                   type == GrowthUpgradeType.StrokeGuard;
        }

        Sprite CreateTestSprite()
        {
            var texture = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            texture.SetPixels(Enumerable.Repeat(Color.black, 16).ToArray());
            texture.Apply();
            return Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f));
        }

        static Sprite LoadGrowthSprite(
            string assetPath,
            string resourcePath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null,
                $"성장 아이콘 파일이 없습니다: {assetPath}");
            Assert.That(importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite),
                $"Resources.Load<Sprite>를 위해 Sprite 임포트가 필요합니다: {assetPath}");
            Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(512),
                $"모바일 선택 아이콘은 512px GPU 예산을 넘기면 안 됩니다: {assetPath}");
            var sprite = Resources.Load<Sprite>(resourcePath);
            Assert.That(sprite, Is.Not.Null,
                $"성장 아이콘 Resources 경로가 올바르지 않습니다: {resourcePath}");
            return sprite;
        }

        static void AssertReadableGrowthText(Text text, int minimumFontSize)
        {
            Assert.That(text, Is.Not.Null);
            Assert.That(text.font, Is.SameAs(InkPalette.UiFont));
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimumFontSize));
            Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(text.resizeTextForBestFit, Is.False,
                "성장 선택 핵심 문구는 작은 화면에서 임의로 축소되면 안 됩니다.");
            Assert.That(text.GetComponent<Outline>(), Is.Not.Null,
                "인게임 HUD와 같은 얇은 먹 외곽선이 필요합니다.");
        }

        static void AssertVerticalGapAtLeast(
            RectTransform upper,
            RectTransform lower,
            float expectedGap)
        {
            Assert.That(upper, Is.Not.Null);
            Assert.That(lower, Is.Not.Null);
            float upperBottom = upper.anchoredPosition.y - upper.sizeDelta.y * 0.5f;
            float lowerTop = lower.anchoredPosition.y + lower.sizeDelta.y * 0.5f;
            Assert.That(
                upperBottom - lowerTop,
                Is.GreaterThanOrEqualTo(expectedGap - 0.001f));
        }

        static void AssertTextFitsRect(Text text)
        {
            Assert.That(text, Is.Not.Null);
            RectTransform rect = text.rectTransform;
            Assert.That(text.preferredWidth,
                Is.LessThanOrEqualTo(rect.sizeDelta.x + 0.001f),
                $"{text.name} 문구가 카드 가로 영역을 넘습니다: {text.text}");
            Assert.That(text.preferredHeight,
                Is.LessThanOrEqualTo(rect.sizeDelta.y + 0.001f),
                $"{text.name} 문구가 카드 세로 영역을 넘습니다: {text.text}");
        }

        static float InvokeResponsiveScale(Vector2 logicalSafeSize)
        {
            var method = typeof(GrowthChoiceView).GetMethod(
                "CalculateResponsiveScale",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { logicalSafeSize });
        }

        void RetagExistingMainCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (!cameras[i].CompareTag("MainCamera")) continue;
                cameras[i].gameObject.tag = "Untagged";
                retaggedMainCameras.Add(cameras[i]);
            }
        }

        T Track<T>(T target) where T : UnityEngine.Object
        {
            cleanup.Add(target);
            return target;
        }

        static int CountDirectChildren(Transform root, string objectName)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name == objectName)
                    count++;
            return count;
        }

        static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType()
                .GetMethods(BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != methodName) return false;
                    var parameters = candidate.GetParameters();
                    if (parameters.Length != arguments.Length) return false;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null) continue;
                        if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i]) &&
                            !(parameters[i].ParameterType.IsValueType &&
                              parameters[i].ParameterType == arguments[i].GetType()))
                            return false;
                    }
                    return true;
                });
            Assert.That(method, Is.Not.Null,
                $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
            return method.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
            return (T)field.GetValue(target);
        }

        static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"{target.GetType().Name}.{propertyName} 속성을 찾을 수 없습니다.");
            return (T)property.GetValue(target);
        }

        static void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"{target.GetType().Name}.{propertyName} 속성을 찾을 수 없습니다.");
            property.SetValue(target, value);
        }

        static void ClearActivePlatforms()
        {
            var field = typeof(PlatformCollider).GetField(
                "active",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "PlatformCollider.active 필드를 찾을 수 없습니다.");
            var active = field.GetValue(null) as IList;
            Assert.That(active, Is.Not.Null);
            active.Clear();
        }
    }
}
