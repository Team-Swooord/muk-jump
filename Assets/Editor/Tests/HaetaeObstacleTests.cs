using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Drawing;
using MukJump.EditorTools;
using MukJump.Obstacles;
using MukJump.Player;

public sealed class HaetaeObstacleTests
{
    const string HaetaeSheetPath =
        "Assets/Resources/MukJump/Obstacles/child_ink_haetae_4frame_v2.png";

    readonly List<UnityEngine.Object> cleanup = new();
    Scene builderTestScene;

    [TearDown]
    public void TearDown()
    {
        if (builderTestScene.IsValid() && builderTestScene.isLoaded)
            MukJumpSceneBuilder.CloseTestScene(builderTestScene);
        builderTestScene = default;

        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
        InvokeStatic(typeof(HazardConcurrencyGate), "ResetStatics");
    }

    [Test]
    public void FirstHaetaeIsGuaranteedAt320mButNeverBeforeUnlock()
    {
        var spawner = Track(new GameObject("HaetaeSpawner"))
            .AddComponent<ObstacleSpawner>();
        var frames = CreateHaetaeFrames();
        SetField(spawner, "haetaeSprite", frames[0]);
        SetField(spawner, "haetaeFrames", frames);
        SetField(spawner, "haetaeUnlockHeight", 320f);
        SetField(spawner, "haetaeChance", 0f);
        SetField(spawner, "firstHaetaePending", true);

        Assert.AreEqual(
            "Spike",
            Invoke(spawner, "ChooseVariant", 319.99f).ToString());
        Assert.IsTrue((bool)GetField(spawner, "firstHaetaePending"),
            "해금 전 슬롯은 첫 해태 보장을 소비하면 안 됩니다.");
        Assert.AreEqual(
            "Haetae",
            Invoke(spawner, "ChooseVariant", 320f).ToString());
        Assert.IsFalse((bool)GetField(spawner, "firstHaetaePending"));
        SetField(spawner, "firstDragonPending", false);
        Assert.AreEqual(
            "Spike",
            Invoke(spawner, "ChooseVariant", 340f).ToString(),
            "첫 보장 이후 확률이 0이면 다음 슬롯은 해태가 아니어야 합니다.");
    }

    [Test]
    public void DragonAndHaetaeShareThirtyPercentLargeAnimalBudget()
    {
        var spawner = Track(new GameObject("LargeAnimalWeights"))
            .AddComponent<ObstacleSpawner>();

        float earlyDragonChance =
            (float)GetField(spawner, "dragonChanceBeforeHaetae");
        float dragonChance = (float)GetField(spawner, "dragonChance");
        float haetaeChance = (float)GetField(spawner, "haetaeChance");

        Assert.That(earlyDragonChance, Is.EqualTo(0.28f).Within(0.0001f),
            "해태가 없는 초·중반의 기존 어린 용 빈도는 유지해야 합니다.");
        Assert.That(dragonChance, Is.EqualTo(0.18f).Within(0.0001f));
        Assert.That(haetaeChance, Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(dragonChance + haetaeChance,
            Is.EqualTo(0.30f).Within(0.0001f),
            "해태를 추가하면서 기존 슬롯의 대형 장애물 총량을 늘리면 안 됩니다.");

        SetField(spawner, "dragonSprite", CreateSprite(300, 100));
        SetField(spawner, "firstDragonPending", false);
        SetField(spawner, "dragonChanceBeforeHaetae", 1f);
        SetField(spawner, "dragonChance", 0f);
        Assert.IsTrue((bool)Invoke(spawner, "ShouldSpawnDragon", 319f));
        Assert.IsFalse((bool)Invoke(spawner, "ShouldSpawnDragon", 320f),
            "320m 이후에만 용 18%·해태 12%의 합산 예산으로 전환해야 합니다.");
    }

    [Test]
    public void DragonAndHaetaeAreMutuallyExclusiveLargeAnimals()
    {
        var spawner = Track(new GameObject("LargeAnimalExclusion"))
            .AddComponent<ObstacleSpawner>();
        SetField(spawner, "dragonSprite", CreateSprite(300, 100));
        var frames = CreateHaetaeFrames();
        SetField(spawner, "haetaeSprite", frames[0]);
        SetField(spawner, "haetaeFrames", frames);
        SetField(spawner, "firstDragonPending", true);
        SetField(spawner, "firstHaetaePending", true);

        var dragonObject = Track(new GameObject("ActiveDragon"));
        var dragon = dragonObject.AddComponent<Obstacle>();
        dragon.OnPoolAcquire();
        dragon.Configure(0f, 0f, 0f, ObstacleKind.ChildDragon);
        var activeMoving = (IList)GetField(spawner, "active");
        activeMoving.Add(dragon);

        Assert.IsTrue((bool)Invoke(spawner, "HasActiveLargeAnimal"));
        Assert.AreEqual(
            "Spike",
            Invoke(spawner, "ChooseVariant", 320f).ToString(),
            "활성 어린 용이 있는 동안 해태를 같은 화면에 추가하면 안 됩니다.");
        Assert.IsTrue((bool)GetField(spawner, "firstHaetaePending"),
            "대형 장애물 충돌로 미룬 슬롯은 첫 보장을 소비하면 안 됩니다.");

        activeMoving.Clear();
        var haetae = CreateConfiguredHaetae("ActiveHaetae");
        RegisterActiveHaetae(spawner, haetae);

        Assert.IsTrue(ReadBoolMember(spawner, "HasActiveHaetae"));
        Assert.IsTrue((bool)Invoke(spawner, "HasActiveLargeAnimal"));
        Assert.IsFalse((bool)Invoke(spawner, "ShouldSpawnDragon", 60f),
            "활성 해태가 있는 동안 어린 용을 같은 화면에 추가하면 안 됩니다.");
        Assert.IsTrue((bool)GetField(spawner, "firstDragonPending"),
            "해태 때문에 미룬 슬롯은 첫 용 보장을 소비하면 안 됩니다.");
    }

    [Test]
    public void RockOrStrongWindDefersHaetaeWithoutConsumingGuarantee()
    {
        var spawner = Track(new GameObject("HaetaeThreatGates"))
            .AddComponent<ObstacleSpawner>();
        var frames = CreateHaetaeFrames();
        SetField(spawner, "haetaeSprite", frames[0]);
        SetField(spawner, "haetaeFrames", frames);
        SetField(spawner, "haetaeUnlockHeight", 320f);
        SetField(spawner, "firstHaetaePending", true);

        var rockSpawner = Track(new GameObject("RockSpawner"))
            .AddComponent<FallingInkRockSpawner>();
        var rock = Track(new GameObject("ActiveRock")).AddComponent<FallingInkRock>();
        ((IList)GetField(rockSpawner, "active")).Add(rock);
        SetField(spawner, "fallingInkRockSpawner", rockSpawner);

        Assert.AreEqual(
            "Spike",
            Invoke(spawner, "ChooseVariant", 320f).ToString());
        Assert.IsTrue((bool)GetField(spawner, "firstHaetaePending"));

        ((IList)GetField(rockSpawner, "active")).Clear();
        var weather = Track(new GameObject("WindWeather"))
            .AddComponent<WindWeatherController>();
        SetProperty(weather, "Phase", WindWeatherPhase.Warning);
        SetField(spawner, "windWeatherController", weather);

        Assert.AreEqual(
            "Spike",
            Invoke(spawner, "ChooseVariant", 320f).ToString());
        Assert.IsTrue((bool)GetField(spawner, "firstHaetaePending"));

        SetProperty(weather, "Phase", WindWeatherPhase.Breeze);
        Assert.AreEqual(
            "Haetae",
            Invoke(spawner, "ChooseVariant", 320f).ToString(),
            "다른 큰 위험이 끝나면 미뤄 둔 첫 해태가 다음 적격 슬롯에 나와야 합니다.");
    }

    [Test]
    public void TelegraphLocksTargetAndKeepsHitboxDisabledUntilPounce()
    {
        var cameraObject = Track(new GameObject("HaetaeTestCamera"));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.transform.position = new Vector3(0f, 2f, -10f);

        var target = CreateShieldedPlayer("LockedTarget");
        target.transform.position = new Vector3(1.1f, 2.4f, 0f);
        var haetae = CreateConfiguredHaetae("LockingHaetae", camera);
        haetae.Activate(target, 2.4f, true);

        Assert.IsTrue(haetae.TryBeginTelegraphNow());
        Vector2 lockedStart = haetae.LockedStart;
        Vector2 lockedTarget = haetae.LockedTarget;
        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State);
        Assert.IsTrue(haetae.HasLockedPath);
        Assert.IsFalse(haetae.IsHitboxEnabled);
        float cameraDistance = Mathf.Abs(
            camera.transform.position.z - haetae.transform.position.z);
        float viewportLeft = camera.ViewportToWorldPoint(
            new Vector3(0f, 0.5f, cameraDistance)).x;
        float viewportRight = camera.ViewportToWorldPoint(
            new Vector3(1f, 0.5f, cameraDistance)).x;
        Assert.That(
            lockedStart.y,
            Is.EqualTo(lockedTarget.y).Within(0.001f),
            "해태는 선택된 화면 벽에서 한 높이의 차선을 가로질러야 합니다.");
        Assert.That(
            lockedStart.x,
            Is.InRange(viewportLeft, viewportRight),
            "낙관과 해태가 화면 밖에서 갑자기 튀어나오지 않도록 시작점은 뷰포트 안쪽이어야 합니다.");
        Assert.That(lockedStart.x, Is.LessThan(lockedTarget.x));
        Assert.IsTrue(haetae.GetComponent<SpriteRenderer>().flipX,
            "왼쪽에서 오른쪽으로 돌진할 때는 왼쪽 얼굴 원본을 뒤집어야 합니다.");
        Assert.AreEqual(4,
            haetae.GetComponentsInChildren<LineRenderer>(true).Length,
            "경로선·위험 띠·느낌표 두 획만 미리 만들어 재사용해야 합니다.");
        Assert.IsTrue(haetae.IsSideWarningVisible);
        Assert.IsTrue(haetae.IsExclamationVisible,
            "색만으로 경고하지 않고 진입 측면에 느낌표 형태가 함께 보여야 합니다.");
        Assert.That(haetae.WarningBandAlpha, Is.InRange(0.08f, 0.22f));
        Assert.IsFalse(haetae.IsMaterializeSealVisible,
            "첫 경고 구간에는 느낌표와 붉은 먹빛이 먼저 보이고 해태는 아직 나타나면 안 됩니다.");
        Assert.That(haetae.BodyAlpha, Is.EqualTo(0f).Within(0.001f));

        target.transform.position = new Vector3(-3.8f, 8.5f, 0f);
        Invoke(haetae, "AdvanceState", 0.6f);

        Assert.That(haetae.LockedStart, Is.EqualTo(lockedStart));
        Assert.That(haetae.LockedTarget, Is.EqualTo(lockedTarget),
            "예고를 본 뒤 플레이어가 움직여도 해태가 유도탄처럼 경로를 바꾸면 안 됩니다.");
        Assert.IsFalse(haetae.IsHitboxEnabled);

        Invoke(haetae, "AdvanceState", 0.61f);
        Assert.AreEqual(HaetaeObstacleState.Pounce, haetae.State);
        Assert.AreEqual(2, haetae.CurrentFrameIndex);
        Assert.IsTrue(haetae.IsHitboxEnabled);
    }

    [Test]
    public void TelegraphShowsSideWarningBeforeMaterializingWithoutExtendingWarning()
    {
        var haetae = CreateConfiguredHaetae("MaterializingHaetae");
        haetae.Activate(
            new Vector2(-4.2f, 0.5f),
            new Vector2(1.1f, 1.8f),
            true);

        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State);
        Assert.IsTrue(haetae.IsSideWarningVisible);
        Assert.IsTrue(haetae.IsExclamationVisible);
        Assert.IsFalse(haetae.IsMaterializeSealVisible);
        Assert.That(haetae.BodyAlpha, Is.EqualTo(0f).Within(0.001f));
        Assert.AreEqual(
            1,
            CountNamedSpriteRenderers(haetae, "HaetaeMaterializeSeal"),
            "낙관 SpriteRenderer는 활성화마다 생성하지 말고 고정 자식 하나만 재사용해야 합니다.");

        Invoke(haetae, "AdvanceState", 0.5f);

        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State);
        Assert.IsFalse(haetae.IsMaterializeSealVisible);
        Assert.That(haetae.BodyAlpha, Is.EqualTo(0f).Within(0.001f),
            "전체 예고의 앞부분은 경고 전용 구간이어야 합니다.");

        Invoke(haetae, "AdvanceState", 0.26f);

        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State);
        Assert.IsTrue(haetae.IsMaterializeSealVisible);
        Assert.That(haetae.MaterializeSealAlpha, Is.InRange(0.01f, 0.99f));
        Assert.That(haetae.BodyAlpha, Is.InRange(0.01f, 0.99f),
            "붉은 측면 경고 뒤에 해태 본체가 교차 페이드되어야 합니다.");

        Invoke(haetae, "AdvanceState", 0.2f);
        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State);
        Assert.IsFalse(haetae.IsMaterializeSealVisible);
        Assert.That(haetae.BodyAlpha, Is.EqualTo(1f).Within(0.001f));

        Invoke(haetae, "AdvanceState", 0.22f);
        Assert.AreEqual(HaetaeObstacleState.Telegraph, haetae.State,
            "경고 전용 구간과 실체화는 기존 1.2초 예고 안에 포함되어야 합니다.");

        Invoke(haetae, "AdvanceState", 0.03f);
        Assert.AreEqual(HaetaeObstacleState.Pounce, haetae.State);
        Assert.IsTrue(haetae.IsHitboxEnabled);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SideWarningMatchesSelectedWallAcrossPortraitViewport(bool fromLeft)
    {
        var cameraObject = Track(new GameObject("HaetaePortraitCamera"));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.aspect = 0.5625f;
        cameraObject.transform.position = new Vector3(0f, 4f, -10f);

        var target = CreateShieldedPlayer("PortraitTarget");
        target.transform.position = new Vector3(0.7f, 4.4f, 0f);
        var haetae = CreateConfiguredHaetae("PortraitHaetae", camera);
        haetae.Activate(target, 4.4f, fromLeft);

        Assert.IsTrue(haetae.TryBeginTelegraphNow());
        Assert.That(haetae.LockedStart.y,
            Is.EqualTo(haetae.LockedTarget.y).Within(0.001f));
        Assert.That(fromLeft
                ? haetae.LockedStart.x < haetae.LockedTarget.x
                : haetae.LockedStart.x > haetae.LockedTarget.x,
            Is.True);
        var band = haetae.transform.Find("HaetaeSideDangerBand")
            ?.GetComponent<LineRenderer>();
        var stem = haetae.transform.Find("HaetaeExclamationStem")
            ?.GetComponent<LineRenderer>();
        Assert.IsNotNull(band);
        Assert.IsNotNull(stem);
        Assert.That(band.GetPosition(0).x,
            Is.EqualTo(haetae.LockedStart.x).Within(0.001f));
        Assert.That(stem.GetPosition(0).x,
            fromLeft
                ? Is.GreaterThan(haetae.LockedStart.x)
                : Is.LessThan(haetae.LockedStart.x),
            "느낌표는 선택된 벽 안쪽에 배치되어야 합니다.");
        Assert.IsTrue(haetae.IsSideWarningVisible);
        Assert.IsTrue(haetae.IsExclamationVisible);
        Assert.IsFalse(haetae.IsHitboxEnabled);

        Invoke(haetae, "AdvanceState", 1.21f);
        Assert.IsFalse(haetae.IsSideWarningVisible);
        Assert.IsFalse(haetae.IsExclamationVisible);
        Assert.IsTrue(haetae.IsHitboxEnabled,
            "경고를 모두 끈 뒤 같은 물리 단계에서만 해태 판정을 켜야 합니다.");
    }

    [Test]
    public void SealAwayKeepsLandingPoseAndReusesSealBeforeRelease()
    {
        var haetae = CreatePouncingHaetae("SealAwayHaetae");
        Invoke(haetae, "BeginLand", false);
        Invoke(haetae, "AdvanceState", 0.15f);

        Assert.AreEqual(HaetaeObstacleState.SealAway, haetae.State);
        Vector3 landingPosition = haetae.transform.position;
        Quaternion landingRotation = haetae.transform.localRotation;
        Vector3 landingScale = haetae.transform.localScale;
        float initialBodyAlpha = haetae.BodyAlpha;
        Assert.IsTrue(haetae.IsMaterializeSealVisible,
            "퇴장은 착지 지점에 남는 붉은 낙관으로 즉시 이어져야 합니다.");
        Assert.That(haetae.MaterializeSealAlpha, Is.GreaterThan(0f));

        Invoke(haetae, "AdvanceState", 0.16f);

        Assert.AreEqual(HaetaeObstacleState.SealAway, haetae.State);
        Assert.That(haetae.transform.position, Is.EqualTo(landingPosition),
            "퇴장 중 뒤로 날아가면 안 됩니다.");
        Assert.That(haetae.transform.localRotation, Is.EqualTo(landingRotation),
            "퇴장 중 회전하며 사라지면 안 됩니다.");
        Assert.That(haetae.transform.localScale, Is.EqualTo(landingScale),
            "퇴장 중 비균일 축소나 뒤집힘이 생기면 안 됩니다.");
        Assert.That(haetae.BodyAlpha, Is.LessThan(initialBodyAlpha));
        Assert.IsTrue(haetae.IsMaterializeSealVisible);
        Assert.That(haetae.MaterializeSealAlpha, Is.GreaterThan(0f));

        Invoke(haetae, "AdvanceState", 0.20f);

        Assert.AreEqual(HaetaeObstacleState.Hidden, haetae.State);
        Assert.IsTrue(haetae.IsReleaseRequested);
        Assert.IsFalse(haetae.IsMaterializeSealVisible);
        Assert.IsFalse(haetae.IsHitboxEnabled);
    }

    [Test]
    public void TelegraphWaitsForHazardGateAndResolvesCurrentTargetAtStart()
    {
        var reservedTarget = CreateShieldedPlayer("ReservedTarget");
        reservedTarget.transform.position = new Vector3(0f, 8f, 0f);
        var currentTarget = CreateShieldedPlayer("CurrentSwarmTarget");
        currentTarget.transform.position = new Vector3(1.2f, 2.1f, 0f);
        bool canBegin = false;

        var go = Track(new GameObject("DeferredHaetae"));
        var haetae = go.AddComponent<HaetaeObstacle>();
        haetae.Configure(
            CreateHaetaeFrames(),
            null,
            Physics2D.DefaultRaycastLayers,
            null,
            currentTargetResolver: () => currentTarget,
            canBeginTelegraph: () => canBegin);
        haetae.OnPoolAcquire();
        haetae.Activate(reservedTarget, 2f, true);

        Assert.IsTrue(HazardConcurrencyGate.HasHaetaeReservation);
        Assert.IsFalse(haetae.TryBeginTelegraphNow());
        Assert.AreEqual(HaetaeObstacleState.Hidden, haetae.State);
        Assert.IsFalse(haetae.HasLockedPath);

        canBegin = true;
        Assert.IsTrue(haetae.TryBeginTelegraphNow());
        Assert.That(haetae.LockedTarget,
            Is.EqualTo((Vector2)currentTarget.transform.position),
            "예약 당시 선두가 아니라 예고 순간의 현재 먹떼 대표를 겨냥해야 합니다.");

        haetae.ForceRelease();
        Assert.IsFalse(HazardConcurrencyGate.HasHaetaeReservation);
    }

    [Test]
    public void HaetaeReservationDefersWindWarningUntilAttackEnds()
    {
        var weather = Track(new GameObject("DeferredWind"))
            .AddComponent<WindWeatherController>();
        SetProperty(weather, "Phase", WindWeatherPhase.Breeze);
        SetProperty(weather, "NextUpdraftHeight", 0);

        var haetae = CreateConfiguredHaetae("WindGateHaetae");
        var rockSpawner = Track(new GameObject("DeferredRock"))
            .AddComponent<FallingInkRockSpawner>();
        haetae.Activate(
            new Vector2(-4f, 1f),
            new Vector2(1f, 1f),
            true);
        Assert.IsTrue((bool)Invoke(
            rockSpawner, "IsSpawnBlockedByConcurrentHazard"));
        Invoke(weather, "UpdateWeatherPhase", 0.1f);
        Assert.AreEqual(WindWeatherPhase.Breeze, weather.Phase);

        haetae.ForceRelease();
        Assert.IsFalse((bool)Invoke(
            rockSpawner, "IsSpawnBlockedByConcurrentHazard"));
        Invoke(weather, "UpdateWeatherPhase", 0.1f);
        Assert.AreEqual(WindWeatherPhase.Warning, weather.Phase);
    }

    [Test]
    public void OnePounceCanAffectAtMostOneLivingPlayer()
    {
        var haetae = CreatePouncingHaetae("SingleHitHaetae");
        var first = CreateShieldedPlayer("FirstPlayer");
        var second = CreateShieldedPlayer("SecondPlayer");

        Assert.IsTrue((bool)Invoke(
            haetae, "ResolveContact", first.GetComponent<Collider2D>()));
        Assert.IsTrue(haetae.AttackConsumed);
        Assert.IsFalse(haetae.IsHitboxEnabled);
        Assert.IsFalse(first.HasShield,
            "첫 번째 생존자 한 명에게는 공격 결과가 적용되어야 합니다.");
        Assert.AreEqual(HaetaeObstacleState.Land, haetae.State);

        Assert.IsFalse((bool)Invoke(
            haetae, "ResolveContact", second.GetComponent<Collider2D>()));
        Assert.IsTrue(second.HasShield,
            "같은 돌진이 뒤의 분신까지 연속으로 소모하면 안 됩니다.");
    }

    [Test]
    public void TemporaryDrawnLineBlocksButWindCurrentDoesNot()
    {
        var temporary = CreatePlatform(
            "TemporaryDrawnLine", lifetime: 4.5f, windCurrent: false);
        Assert.IsTrue(temporary.IsTemporaryDrawnPlatform);

        var blockedHaetae = CreatePouncingHaetae("BlockedHaetae");
        Assert.IsTrue((bool)Invoke(
            blockedHaetae,
            "ResolveContact",
            temporary.GetComponent<EdgeCollider2D>()));
        Assert.IsTrue(blockedHaetae.WasBlockedByPlatform);
        Assert.IsTrue(blockedHaetae.AttackConsumed);
        Assert.IsTrue(temporary.gameObject.activeSelf,
            "해태를 막은 선은 낙묵석처럼 파괴하지 않고 플레이어의 길로 남겨야 합니다.");

        var windCurrent = CreatePlatform(
            "WindCurrent", lifetime: 0f, windCurrent: true);
        Assert.IsFalse(windCurrent.IsTemporaryDrawnPlatform);

        var passingHaetae = CreatePouncingHaetae("PassingHaetae");
        Assert.IsFalse((bool)Invoke(
            passingHaetae,
            "ResolveContact",
            windCurrent.GetComponent<EdgeCollider2D>()));
        Assert.IsFalse(passingHaetae.WasBlockedByPlatform);
        Assert.IsFalse(passingHaetae.AttackConsumed);
        Assert.IsTrue(passingHaetae.IsHitboxEnabled);
        Assert.AreEqual(HaetaeObstacleState.Pounce, passingHaetae.State);
    }

    [Test]
    public void PermanentPlatformCannotHidePlayerBehindItFromPounceCast()
    {
        var haetae = CreatePouncingHaetae("NonAllocCastHaetae");
        var permanent = CreatePlatform(
            "PermanentPlatform", lifetime: 0f, windCurrent: false);
        permanent.transform.position = new Vector3(-1.5f, 1.58f, 0f);
        var player = CreateShieldedPlayer("PlayerBehindPermanentPlatform");
        player.transform.position = new Vector3(0.8f, 1.35f, 0f);
        Physics2D.SyncTransforms();

        Vector2 origin = haetae.GetComponent<Rigidbody2D>().position;
        bool resolved = (bool)Invoke(
            haetae,
            "TryResolveCastContact",
            origin,
            new Vector2(5.2f, -0.45f));

        var castHits = (RaycastHit2D[])GetField(haetae, "castHits");
        var hitNames = new List<string>();
        for (int i = 0; i < castHits.Length; i++)
            if (castHits[i].collider != null)
                hitNames.Add(castHits[i].collider.name);
        Assert.IsTrue(
            resolved,
            $"origin={origin}, hits=[{string.Join(", ", hitNames)}]");
        Assert.IsTrue(haetae.AttackConsumed);
        Assert.IsFalse(player.HasShield,
            "무시해야 하는 영구 발판 뒤의 유효 플레이어까지 캐스트 결과를 순회해야 합니다.");
    }

    [Test]
    public void PoolReuseResetsPathFrameColliderFacingAndWarningObjects()
    {
        var root = Track(new GameObject("HaetaePoolRoot"));
        var frames = CreateHaetaeFrames();
        int createdCount = 0;
        var pool = new ComponentPool<HaetaeObstacle>(() =>
        {
            createdCount++;
            var go = new GameObject($"PooledHaetae_{createdCount}");
            go.transform.SetParent(root.transform, false);
            go.SetActive(false);
            var obstacle = go.AddComponent<HaetaeObstacle>();
            obstacle.Configure(
                frames, null, Physics2D.DefaultRaycastLayers, null);
            return obstacle;
        }, 2);

        var first = pool.Acquire();
        first.Activate(new Vector2(4f, 2f), new Vector2(-1f, 1.4f), false);
        Invoke(first, "AdvanceState", 1.21f);
        Assert.AreEqual(HaetaeObstacleState.Pounce, first.State);
        Assert.AreEqual(2, first.CurrentFrameIndex);
        Assert.IsFalse(first.GetComponent<SpriteRenderer>().flipX,
            "오른쪽에서 왼쪽으로 돌진할 때는 왼쪽을 보는 원본 방향을 유지해야 합니다.");
        int warningObjectCount =
            first.GetComponentsInChildren<LineRenderer>(true).Length;
        Assert.AreEqual(
            1,
            CountNamedSpriteRenderers(first, "HaetaeMaterializeSeal"));

        Assert.IsTrue(pool.Release(first));
        var second = pool.Acquire();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, createdCount);
        Assert.AreEqual(HaetaeObstacleState.Hidden, second.State);
        Assert.IsFalse(second.HasLockedPath);
        Assert.IsFalse(second.AttackConsumed);
        Assert.IsFalse(second.WasBlockedByPlatform);
        Assert.IsFalse(second.IsHitboxEnabled);
        Assert.IsFalse(second.IsReleaseRequested);
        Assert.AreEqual(0, second.CurrentFrameIndex);
        Assert.IsFalse(second.GetComponent<SpriteRenderer>().flipX);
        Assert.IsFalse(second.IsMaterializeSealVisible);
        Assert.IsFalse(second.IsSideWarningVisible);
        Assert.IsFalse(second.IsExclamationVisible);
        Assert.That(second.MaterializeSealAlpha, Is.EqualTo(0f).Within(0.001f));
        Assert.That(second.transform.localRotation, Is.EqualTo(Quaternion.identity));
        var warningObjects =
            second.GetComponentsInChildren<LineRenderer>(true);
        Assert.AreEqual(warningObjectCount, warningObjects.Length,
            "풀 재사용 때 경고선·띠·느낌표를 중복 생성하면 안 됩니다.");
        for (int i = 0; i < warningObjects.Length; i++)
            Assert.IsFalse(warningObjects[i].enabled);
        Assert.AreEqual(
            1,
            CountNamedSpriteRenderers(second, "HaetaeMaterializeSeal"),
            "풀 재사용 뒤에도 낙관 자식은 정확히 하나여야 합니다.");

        pool.Release(second);
    }

    [Test]
    public void DirectConfigureUsesPaperRedRemapAndCreatesOneReusableSeal()
    {
        var go = Track(new GameObject("DirectConfiguredHaetae"));
        var haetae = go.AddComponent<HaetaeObstacle>();
        var visibility = go.AddComponent<ObstacleVisibilityView>();
        var body = go.GetComponent<SpriteRenderer>();
        body.sprite = CreateSprite(220, 160);
        body.color = Color.white;
        var frames = CreateHaetaeFrames();

        visibility.Configure(preserveInkOutlines: false);
        haetae.Configure(
            frames,
            null,
            Physics2D.DefaultRaycastLayers,
            null);
        haetae.Configure(
            frames,
            null,
            Physics2D.DefaultRaycastLayers,
            null);

        Assert.That(body.color.r,
            Is.EqualTo(InkPalette.ObstaclePaperRed.r).Within(0.001f));
        Assert.That(body.color.g,
            Is.EqualTo(InkPalette.ObstaclePaperRed.g).Within(0.001f));
        Assert.That(body.color.b,
            Is.EqualTo(InkPalette.ObstaclePaperRed.b).Within(0.001f));
        Assert.IsNotNull(body.sharedMaterial);
        Assert.IsNotNull(body.sharedMaterial.shader);
        Assert.AreEqual(
            "MukJump/ObstaclePaperRed",
            body.sharedMaterial.shader.name);
        Assert.AreEqual(
            1,
            CountNamedSpriteRenderers(haetae, "HaetaeMaterializeSeal"),
            "Configure를 반복해도 낙관 렌더러를 중복 생성하면 안 됩니다.");
        Assert.AreEqual(
            4,
            haetae.GetComponentsInChildren<LineRenderer>(true).Length,
            "낙관을 추가해도 기존 경로선·발자국 LineRenderer 예산은 늘리지 않습니다.");
    }

    [Test]
    public void SpawnerPoolFactoryIncludesPaperRedVisibilityView()
    {
        var spawner = Track(new GameObject("HaetaePoolFactory"))
            .AddComponent<ObstacleSpawner>();

        var haetae = (HaetaeObstacle)Invoke(spawner, "CreatePooledHaetae");
        var visibility = haetae.GetComponent<ObstacleVisibilityView>();
        var body = haetae.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(visibility,
            "런타임 풀 팩토리에서 해태 루트에 붉은 한지 가시성 뷰를 반드시 붙여야 합니다.");

        body.sprite = CreateSprite(220, 160);
        body.color = Color.white;
        visibility.Configure(preserveInkOutlines: false);
        haetae.Configure(
            CreateHaetaeFrames(),
            null,
            Physics2D.DefaultRaycastLayers,
            null);

        Assert.That(body.color.r,
            Is.EqualTo(InkPalette.ObstaclePaperRed.r).Within(0.001f));
        Assert.That(body.color.g,
            Is.EqualTo(InkPalette.ObstaclePaperRed.g).Within(0.001f));
        Assert.That(body.color.b,
            Is.EqualTo(InkPalette.ObstaclePaperRed.b).Within(0.001f));
        Assert.IsNotNull(body.sharedMaterial);
        Assert.IsNotNull(body.sharedMaterial.shader);
        Assert.AreEqual(
            "MukJump/ObstaclePaperRed",
            body.sharedMaterial.shader.name);
        Assert.AreEqual(
            1,
            CountNamedSpriteRenderers(haetae, "HaetaeMaterializeSeal"));
    }

    [Test]
    public void HaetaeSheetImportsAsFourOrderedFullGridFrames()
    {
        var importer = AssetImporter.GetAtPath(HaetaeSheetPath) as TextureImporter;
        Assert.IsNotNull(importer);
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
        Assert.AreEqual(700f, importer.spritePixelsPerUnit);
        Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
        Assert.IsTrue(importer.alphaIsTransparency);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.AreEqual(2048, importer.maxTextureSize);

        var importedAssets = AssetDatabase.LoadAllAssetsAtPath(HaetaeSheetPath);
        var frames = new List<Sprite>(4);
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] is Sprite frame)
                frames.Add(frame);
        }
        frames.Sort((left, right) =>
            string.CompareOrdinal(left.name, right.name));

        Assert.AreEqual(4, frames.Count);
        Vector2[] expectedPivots =
        {
            new(350f, 287.5f),
            new(293f, 256.5f),
            new(344.5f, 399f),
            new(286.5f, 359.5f),
        };
        for (int i = 0; i < frames.Count; i++)
        {
            Assert.AreEqual($"child_ink_haetae_frame_{i:00}", frames[i].name);
            Assert.That(frames[i].rect.width, Is.EqualTo(627f).Within(0.01f));
            Assert.That(frames[i].rect.height, Is.EqualTo(627f).Within(0.01f));
            Assert.That(frames[i].rect.x,
                Is.EqualTo((i % 2) * 627f).Within(0.01f));
            Assert.That(frames[i].rect.y,
                Is.EqualTo((1 - i / 2) * 627f).Within(0.01f));
            Assert.That(frames[i].pivot.x,
                Is.EqualTo(expectedPivots[i].x).Within(0.01f));
            Assert.That(frames[i].pivot.y,
                Is.EqualTo(expectedPivots[i].y).Within(0.01f));
        }
    }

    [Test]
    public void SceneBuilderWiresHaetaeFramesThreatGatesAndDebugButton()
    {
        builderTestScene = MukJumpSceneBuilder.BuildForTests();

        var spawner = FindFirstInScene<ObstacleSpawner>(builderTestScene);
        Assert.IsNotNull(spawner);
        var serialized = new SerializedObject(spawner);
        Assert.IsNotNull(
            serialized.FindProperty("haetaeSprite")?.objectReferenceValue);
        Assert.AreEqual(
            320f,
            serialized.FindProperty("haetaeUnlockHeight")?.floatValue);
        Assert.AreEqual(
            0.12f,
            serialized.FindProperty("haetaeChance")?.floatValue);
        Assert.AreEqual(
            0.28f,
            serialized.FindProperty("dragonChanceBeforeHaetae")?.floatValue);
        Assert.IsNotNull(
            serialized.FindProperty("windWeatherController")?.objectReferenceValue);
        Assert.IsNotNull(
            serialized.FindProperty("fallingInkRockSpawner")?.objectReferenceValue);

        var frames = serialized.FindProperty("haetaeFrames");
        Assert.IsNotNull(frames);
        Assert.AreEqual(4, frames.arraySize);
        for (int i = 0; i < frames.arraySize; i++)
        {
            var frame =
                frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
            Assert.IsNotNull(frame);
            Assert.AreEqual($"child_ink_haetae_frame_{i:00}", frame.name);
        }
        Assert.AreSame(
            frames.GetArrayElementAtIndex(0).objectReferenceValue,
            serialized.FindProperty("haetaeSprite")?.objectReferenceValue);

        var hud = FindFirstInScene<GameplayHudView>(builderTestScene);
        Assert.IsNotNull(hud);
        var hudSerialized = new SerializedObject(hud);
        var button =
            hudSerialized.FindProperty("haetaeButton")?.objectReferenceValue as Button;
        Assert.IsNotNull(button);
        Assert.AreEqual("HaetaeButton", button.name);
        Assert.AreEqual(
            "먹해태",
            button.transform.Find("Label")?.GetComponent<Text>()?.text);
    }

    [Test]
    public void CheckedInMainSceneContainsHaetaeWiringAndDebugButton()
    {
        string source = File.ReadAllText("Assets/Scenes/Main.unity");

        Assert.That(source, Does.Contain("haetaeFrames:"));
        Assert.That(source, Does.Contain("haetaeUnlockHeight: 320"));
        Assert.That(source, Does.Contain("haetaeChance: 0.12"));
        Assert.That(source, Does.Contain("dragonChanceBeforeHaetae: 0.28"));
        Assert.That(source, Does.Contain("fallingInkRockSpawner:"));
        Assert.That(source, Does.Contain("windWeatherController:"));
        Assert.That(source, Does.Contain("m_Name: HaetaeButton"));
        Assert.That(source, Does.Contain("haetaeButton:"));
    }

    [Test]
    public void DebugTeleportRestoresFirstHaetaeGuarantee()
    {
        var spawner = Track(new GameObject("TeleportSpawner"))
            .AddComponent<ObstacleSpawner>();
        SetField(spawner, "firstHaetaePending", false);

        Invoke(spawner, "OnWorldHeightTeleported", 500);

        Assert.IsTrue((bool)GetField(spawner, "firstHaetaePending"));
    }

    HaetaeObstacle CreateConfiguredHaetae(
        string objectName,
        Camera camera = null)
    {
        var go = Track(new GameObject(objectName));
        var haetae = go.AddComponent<HaetaeObstacle>();
        haetae.Configure(
            CreateHaetaeFrames(),
            camera,
            Physics2D.DefaultRaycastLayers,
            null);
        haetae.OnPoolAcquire();
        return haetae;
    }

    HaetaeObstacle CreatePouncingHaetae(string objectName)
    {
        var haetae = CreateConfiguredHaetae(objectName);
        haetae.Activate(
            new Vector2(-4f, 1.8f),
            new Vector2(1.2f, 1.35f),
            true);
        Invoke(haetae, "AdvanceState", 1.21f);
        Assert.AreEqual(HaetaeObstacleState.Pounce, haetae.State);
        return haetae;
    }

    PlayerController CreateShieldedPlayer(string objectName)
    {
        var go = Track(new GameObject(objectName));
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CircleCollider2D>();
        var player = go.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        player.GrantShield();
        return player;
    }

    PlatformCollider CreatePlatform(
        string objectName,
        float lifetime,
        bool windCurrent)
    {
        var go = Track(new GameObject(objectName));
        go.layer = LayerMask.NameToLayer("Platform");
        go.AddComponent<LineRenderer>();
        var edge = go.AddComponent<EdgeCollider2D>();
        edge.points = new[]
        {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
        };
        var platform = go.AddComponent<PlatformCollider>();
        SetField(platform, "lifetime", lifetime);
        SetField(platform, "windCurrentPlatform", windCurrent);
        return platform;
    }

    Sprite[] CreateHaetaeFrames()
    {
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = CreateSprite(220, 160);
            frames[i].name = $"child_ink_haetae_frame_{i:00}";
            frames[i].texture.name = "child_ink_haetae_4frame_v2";
        }
        return frames;
    }

    Sprite CreateSprite(int width, int height)
    {
        var texture = Track(new Texture2D(width, height));
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f);
        cleanup.Add(sprite);
        return sprite;
    }

    void RegisterActiveHaetae(ObstacleSpawner spawner, HaetaeObstacle haetae)
    {
        FieldInfo[] fields = spawner.GetType().GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.Name.IndexOf("haetae", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (field.FieldType == typeof(HaetaeObstacle))
            {
                field.SetValue(spawner, haetae);
                return;
            }

            object value = field.GetValue(spawner);
            if (value is IList list)
            {
                Type elementType = field.FieldType.IsGenericType
                    ? field.FieldType.GetGenericArguments()[0]
                    : null;
                if (elementType == typeof(HaetaeObstacle))
                {
                    list.Add(haetae);
                    return;
                }
            }
        }

        Assert.Fail(
            "ObstacleSpawner에 활성 HaetaeObstacle을 추적하는 필드가 필요합니다.");
    }

    static int CountNamedSpriteRenderers(
        HaetaeObstacle haetae,
        string objectName)
    {
        int count = 0;
        var renderers = haetae.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == objectName)
                count++;
        }
        return count;
    }

    bool ReadBoolMember(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (property != null)
            return (bool)property.GetValue(target);

        MethodInfo method = target.GetType().GetMethod(
            memberName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{memberName} 계약이 필요합니다.");
        return (bool)method.Invoke(target, null);
    }

    T Track<T>(T value) where T : UnityEngine.Object
    {
        cleanup.Add(value);
        return value;
    }

    static T FindFirstInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T match = roots[i].GetComponentInChildren<T>(true);
            if (match != null) return match;
        }
        return null;
    }

    static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(
            property,
            $"{target.GetType().Name}.{propertyName} property가 필요합니다.");
        MethodInfo setter = property.GetSetMethod(true);
        Assert.IsNotNull(
            setter,
            $"{target.GetType().Name}.{propertyName} setter가 필요합니다.");
        setter.Invoke(target, new[] { value });
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(
            field,
            $"{target.GetType().Name}.{fieldName} field가 필요합니다.");
        field.SetValue(target, value);
    }

    static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(
            field,
            $"{target.GetType().Name}.{fieldName} field가 필요합니다.");
        return field.GetValue(target);
    }

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(
            method,
            $"{target.GetType().Name}.{methodName} method가 필요합니다.");
        return method.Invoke(target, arguments);
    }

    static object InvokeStatic(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{type.Name}.{methodName} method가 필요합니다.");
        return method.Invoke(null, arguments);
    }
}
