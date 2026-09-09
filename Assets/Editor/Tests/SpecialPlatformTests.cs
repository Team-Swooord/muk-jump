using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;

public sealed class SpecialPlatformTests
{
    readonly List<GameObject> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
        GameplayRandom.ResetSession(0x4D554B);
    }

    [Test]
    public void WindPlatformIsOneWayAndUsesWindPlatformColor()
    {
        var platform = Track(PlatformCollider.SpawnWindCurrentPlatform(CreatePoints()));

        Assert.IsTrue(platform.IsWindCurrentPlatform);
        AssertOneWayTintablePlatform(platform, InkPalette.WindPlatform);
    }

    [Test]
    public void MapRestPlatformIsPermanentOneWayInkFreeAndRockProof()
    {
        float inkBefore = PlatformCollider.ActiveInkCost;
        int drawnBefore = PlatformCollider.ActiveDrawnPlatformCount;

        var platform = Track(
            PlatformCollider.SpawnMapRestPlatform(CreatePoints(2.4f)));

        Assert.That(platform, Is.Not.Null);
        Assert.That(platform.IsMapRestPlatform, Is.True);
        Assert.That(platform.IsWindCurrentPlatform, Is.False);
        Assert.That(platform.IsGrowthSafetyPlatform, Is.False);
        Assert.That(platform.IsTemporaryDrawnPlatform, Is.False);
        Assert.That(GetField<float>(platform, "lifetime"), Is.Zero);
        Assert.That(platform.IsOneWayPlatform, Is.True);
        AssertOneWayPhysics(platform);
        Assert.That(PlatformCollider.ActiveInkCost,
            Is.EqualTo(inkBefore).Within(0.0001f));
        Assert.That(PlatformCollider.ActiveDrawnPlatformCount,
            Is.EqualTo(drawnBefore));
        Assert.That(platform.BreakFromHazard(), Is.False);
        Assert.That(platform.GetComponent<EdgeCollider2D>().enabled, Is.True);
        Assert.That(platform.transform.Find("HanjiSupport"), Is.Not.Null);
    }

    [Test]
    public void MapRestLongWidthUsesUnitJumpMultiplier()
    {
        var platform = Track(
            PlatformCollider.SpawnMapRestPlatform(CreatePoints(2.4f)));
        var playerObject = new GameObject("MapRestAutoJumpProbe");
        cleanup.Add(playerObject);
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        var autoJump = playerObject.AddComponent<AutoJump>();
        SetField(autoJump, "player", player);
        SetProperty(player, "CurrentPlatform", platform);

        float multiplier = (float)Invoke(autoJump, "PowerMultiplier");

        Assert.That(multiplier, Is.EqualTo(1f).Within(0.0001f));
    }

    [TestCase(1.2f)]
    [TestCase(2.4f)]
    [TestCase(3.2f)]
    public void MapRestHasSageColorAndEvenEndsWithoutChangingPhysics(float halfWidth)
    {
        var points = CreatePoints(halfWidth);
        var platform = Track(PlatformCollider.SpawnMapRestPlatform(points));
        var line = platform.Line;
        var support = platform.transform.Find("HanjiSupport").GetComponent<LineRenderer>();
        var color = InkPalette.MapRestPlatform;
        color.a = 0.98f;
        AssertColor(color, line.startColor);
        AssertColor(color, line.endColor);
        Assert.That(line.sharedMaterial, Is.SameAs(FallbackInkStyle.SharedRestPlatformMaterial));
        Assert.That(support.sharedMaterial, Is.SameAs(line.sharedMaterial));
        Assert.That(line.sharedMaterial, Is.Not.SameAs(FallbackInkStyle.SharedTintableBrushMaterial));
        Assert.That(InkPalette.MapRestPlatform, Is.Not.EqualTo(InkPalette.Ink));
        Assert.That(InkPalette.MapRestPlatform, Is.Not.EqualTo(InkPalette.WindPlatform));
        foreach (var visual in new[] { line, support })
        {
            Assert.That(visual.numCapVertices, Is.Zero, "충돌 끝점 밖으로 붓꼬리를 늘리지 않는다");
            for (int i = 0; i <= 10; i++)
                Assert.That(visual.widthCurve.Evaluate(i / 10f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(visual.GetPosition(0), Is.EqualTo(line.GetPosition(0)));
            Assert.That(visual.GetPosition(visual.positionCount - 1),
                Is.EqualTo(line.GetPosition(line.positionCount - 1)));
        }
        var collider = platform.GetComponent<EdgeCollider2D>();
        Assert.That(collider.points.Length, Is.EqualTo(points.Count));
        for (int i = 0; i < points.Count; i++)
            Assert.That(Vector2.Distance(collider.transform.TransformPoint(collider.points[i]), points[i]),
                Is.LessThan(0.0001f), "외형 변경으로 발판 착지 위치를 바꾸지 않는다");

        var ordinary = new GameObject("OrdinaryBrushVisual");
        cleanup.Add(ordinary);
        var ordinaryLine = ordinary.AddComponent<LineRenderer>();
        FallbackInkStyle.Apply(ordinaryLine, halfWidth * 2);
        Assert.That(ordinaryLine.sharedMaterial, Is.SameAs(FallbackInkStyle.SharedInkMaterial));
        Assert.That(ordinaryLine.widthCurve.Evaluate(1), Is.LessThan(ordinaryLine.widthCurve.Evaluate(0.5f)),
            "직접 그리는 획의 붓끝은 그대로 유지한다");
    }

    [Test]
    public void MapRestFirstAndRepeatReservationsUseExpectedRanges()
    {
        GameplayRandom.ResetSession(20260831);
        var root = new GameObject("NaturalMapRestSpawnerTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();

        Invoke(spawner, "EnsureSessionSchedule");
        float first = GetField<float>(spawner, "nextMapRestHeight");
        Assert.That(first, Is.InRange(22f, 28f));

        bool spawned = (bool)Invoke(
            spawner,
            "TrySpawnMapRest",
            first,
            0f);
        var active = GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform");
        float activeHeight = GetField<float>(spawner, "activeMapRestHeight");
        float next = GetField<float>(spawner, "nextMapRestHeight");

        Assert.That(spawned, Is.True);
        Assert.That(active, Is.Not.Null);
        cleanup.Add(active.gameObject);
        Assert.That(activeHeight, Is.EqualTo(first).Within(0.0001f));
        Assert.That(next - activeHeight, Is.InRange(28f, 38f));
        Assert.That(active.Length,
            Is.EqualTo(RestPlatformSpawner.DefaultMapRestWidth).Within(0.01f));
        Assert.That(Mathf.Abs(active.transform.position.x),
            Is.LessThanOrEqualTo(0.9f));

        Assert.That((bool)Invoke(
            spawner,
            "TrySpawnMapRest",
            next,
            0f), Is.True,
            "보존 중인 쉼터 하나가 다음 자연 생성을 막으면 안 됩니다.");
        var second = GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform");
        cleanup.Add(second.gameObject);
        float third = GetField<float>(spawner, "nextMapRestHeight");
        Assert.That((bool)Invoke(
            spawner,
            "TrySpawnMapRest",
            third,
            0f), Is.True,
            "보호 슬롯 외의 쉼터는 다음 예약으로 자연스럽게 교체되어야 합니다.");
        var spawnedPlatforms = GetField<List<PlatformCollider>>(spawner, "spawned");
        Assert.That(spawnedPlatforms.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform),
            Has.Count.EqualTo(RestPlatformSpawner.MaxRetainedMapRests));
    }

    [Test]
    public void LaggingSurvivorKeepsOldRestWithoutBlockingNextAndQueueStaysBounded()
    {
        var managerObject = new GameObject("LaggingRestGameManager");
        cleanup.Add(managerObject);
        var manager = managerObject.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        PlayerController leader = CreateLivingPlayer(manager, "RestLeader", 150f);
        PlayerController follower = CreateLivingPlayer(manager, "RestFollower", 20f);
        Assert.That(leader, Is.Not.Null);

        var root = new GameObject("LaggingRestSpawner");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "scheduledSessionVersion", GameplayRandom.SessionVersion);
        SetField(spawner, "restHeightIntervalRange", new Vector2(30f, 30f));
        SetField(spawner, "nextMapRestHeight", 50f);

        Assert.That((bool)Invoke(spawner, "TrySpawnMapRest", 50f, 0f), Is.True);
        var first = GetField<PlatformCollider>(spawner, "activeMapRestPlatform");
        cleanup.Add(first.gameObject);
        float protectedCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            100f);
        Invoke(spawner, "CleanupOldPlatforms", 100f, protectedCutoff);
        Assert.That(first, Is.Not.Null, "뒤처진 생존자가 쓸 50m 쉼터는 보존해야 합니다.");

        Assert.That((bool)Invoke(spawner, "TrySpawnMapRest", 150f, protectedCutoff),
            Is.True, "보존된 50m 쉼터와 별개로 80m 쉼터가 한 번 생성되어야 합니다.");
        var spawned = GetField<List<PlatformCollider>>(spawner, "spawned");
        var rests = spawned.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform);
        Assert.That(rests, Has.Count.EqualTo(2));
        cleanup.Add(rests[1].gameObject);
        Assert.That(RestBaselineWorldY(rests[0]), Is.EqualTo(50f).Within(0.001f));
        Assert.That(RestBaselineWorldY(rests[1]), Is.EqualTo(80f).Within(0.001f));
        PlatformCollider eightyMeterRest = rests.Find(platform =>
            Mathf.Abs(RestBaselineWorldY(platform) - 80f) < 0.001f);
        for (int i = 0; i < 4; i++)
        {
            Assert.That((bool)Invoke(
                spawner,
                "TrySpawnMapRest",
                101f,
                protectedCutoff), Is.False);
            Assert.That(eightyMeterRest, Is.Not.Null.And.SameAs(
                rests.Find(platform =>
                    Mathf.Abs(RestBaselineWorldY(platform) - 80f) < 0.001f)));
            Assert.That(eightyMeterRest.gameObject.activeSelf, Is.True,
                "110m 예약이 범위 밖일 때 80m 쉼터를 미리 지우면 안 됩니다.");
        }
        Assert.That((bool)Invoke(spawner, "TrySpawnMapRest", 150f, protectedCutoff),
            Is.True);
        rests = spawned.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform);
        Assert.That(rests, Has.Count.EqualTo(RestPlatformSpawner.MaxRetainedMapRests));
        Assert.That(rests.Exists(platform =>
            Mathf.Abs(RestBaselineWorldY(platform) - 50f) < 0.001f), Is.True);
        Assert.That(rests.Exists(platform =>
            Mathf.Abs(RestBaselineWorldY(platform) - 110f) < 0.001f), Is.True);
        Assert.That(GetField<float>(spawner, "nextMapRestHeight"),
            Is.EqualTo(140f).Within(0.001f));

        Assert.That((bool)Invoke(spawner, "TrySpawnMapRest", 180f, protectedCutoff),
            Is.True);
        rests = spawned.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform);
        Assert.That(rests, Has.Count.EqualTo(RestPlatformSpawner.MaxRetainedMapRests));
        Assert.That(rests.Exists(platform =>
            Mathf.Abs(RestBaselineWorldY(platform) - 50f) < 0.001f), Is.True);
        Assert.That(rests.Exists(platform =>
            Mathf.Abs(RestBaselineWorldY(platform) - 140f) < 0.001f), Is.True);
        Assert.That(GetField<float>(spawner, "nextMapRestHeight"),
            Is.EqualTo(170f).Within(0.001f));

        follower.transform.position = new Vector3(0f, 130f, 0f);
        float caughtUpCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            100f);
        Invoke(spawner, "CleanupOldPlatforms", 100f, caughtUpCutoff);
        rests = spawned.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform);
        Assert.That(rests, Has.Count.EqualTo(1));
        Assert.That(RestBaselineWorldY(rests[0]),
            Is.EqualTo(140f).Within(0.001f),
            "뒤처진 생존자가 지난 뒤에는 옛 보호 슬롯만 정리해야 합니다.");

        leader.transform.position = new Vector3(0f, 200f, 0f);
        follower.transform.position = new Vector3(0f, 200f, 0f);
        float finalCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            180f);
        Invoke(spawner, "CleanupOldPlatforms", 180f, finalCutoff);
        Assert.That(spawned.FindAll(platform =>
            platform != null && platform.IsMapRestPlatform), Is.Empty,
            "카메라와 다음 선두가 지난 보호 쉼터는 정리되어야 합니다.");
    }

    static float RestBaselineWorldY(PlatformCollider platform)
    {
        // 원점은 살짝 휜 먹선의 점평균이다. 예약 고도는 아치 양끝으로 검증한다.
        var points = platform.GetComponent<EdgeCollider2D>().points;
        float firstY = platform.transform.TransformPoint(points[0]).y;
        float lastY = platform.transform.TransformPoint(points[^1]).y;
        Assert.That(lastY, Is.EqualTo(firstY).Within(0.001f));
        Assert.That(platform.transform.TransformPoint(points[points.Length / 2]).y,
            Is.GreaterThan(firstY));
        return firstY;
    }

    [Test]
    public void MapRestHazardBandCoversActiveAndNextReservations()
    {
        GameplayRandom.ResetSession(3108);
        var root = new GameObject("MapRestHazardBandTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        Invoke(spawner, "EnsureSessionSchedule");
        float first = GetField<float>(spawner, "nextMapRestHeight");
        Invoke(spawner, "TrySpawnMapRest", first, 0f);
        var active = GetField<PlatformCollider>(spawner, "activeMapRestPlatform");
        cleanup.Add(active.gameObject);
        float next = GetField<float>(spawner, "nextMapRestHeight");

        Assert.That(spawner.IsHazardHeightBlocked(first - 8f), Is.True);
        Assert.That(spawner.IsHazardHeightBlocked(first + 8f), Is.True);
        Assert.That(spawner.IsHazardHeightBlocked(next), Is.True);
        Assert.That(spawner.IsHazardBandBlocked(next - 12f, next - 7.9f),
            Is.True);
        Assert.That(spawner.IsHazardHeightBlocked(next + 8.1f), Is.False);
    }

    [Test]
    public void SameSessionKeepsReservationAndSkippedHeightRepairsOnce()
    {
        GameplayRandom.ResetSession(9173);
        var root = new GameObject("MapRestSessionTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        Invoke(spawner, "EnsureSessionSchedule");
        int version = GetField<int>(spawner, "scheduledSessionVersion");
        float reservation = GetField<float>(spawner, "nextMapRestHeight");

        Invoke(spawner, "EnsureSessionSchedule");
        Assert.That(GetField<int>(spawner, "scheduledSessionVersion"),
            Is.EqualTo(version));
        Assert.That(GetField<float>(spawner, "nextMapRestHeight"),
            Is.EqualTo(reservation).Within(0.0001f),
            "광고 부활처럼 난수 세션이 같은 재진입은 예약을 바꾸면 안 됩니다.");

        SetField(spawner, "nextMapRestHeight", 10f);
        bool spawnedPastPlatform = (bool)Invoke(
            spawner,
            "TrySpawnMapRest",
            300f,
            280f);
        float repaired = GetField<float>(spawner, "nextMapRestHeight");
        Assert.That(spawnedPastPlatform, Is.False);
        Assert.That(repaired - 280f, Is.InRange(28f, 38f));
        Assert.That(GetField<PlatformCollider>(spawner, "activeMapRestPlatform"),
            Is.Null,
            "지나간 예약을 만큼 과거 발판을 한 프레임에 쌓으면 안 됩니다.");
    }

    [Test]
    public void MapRestCleanupWaitsForNextHighestSurvivorBeforeLeaderReframe()
    {
        var managerObject = new GameObject("RestCleanupGameManager");
        cleanup.Add(managerObject);
        var manager = managerObject.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        var leaderObject = new GameObject("RestCleanupLeader");
        cleanup.Add(leaderObject);
        leaderObject.AddComponent<Rigidbody2D>();
        leaderObject.AddComponent<CircleCollider2D>();
        var leader = leaderObject.AddComponent<PlayerController>();
        Invoke(leader, "Awake");
        leader.transform.position = new Vector3(0f, 100f, 0f);
        manager.RegisterPlayer(leader);

        var followerObject = new GameObject("RestCleanupNextLeader");
        cleanup.Add(followerObject);
        followerObject.AddComponent<Rigidbody2D>();
        followerObject.AddComponent<CircleCollider2D>();
        var follower = followerObject.AddComponent<PlayerController>();
        Invoke(follower, "Awake");
        follower.transform.position = new Vector3(0f, 40f, 0f);
        manager.RegisterPlayer(follower);

        var root = new GameObject("RestCleanupSpawner");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "nextMapRestHeight", 50f);
        SetField(spawner, "scheduledSessionVersion", GameplayRandom.SessionVersion);
        Invoke(spawner, "TrySpawnMapRest", 50f, 0f);
        var active = GetField<PlatformCollider>(spawner, "activeMapRestPlatform");
        Assert.That(active, Is.Not.Null);
        cleanup.Add(active.gameObject);

        float protectedCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            60f);
        Invoke(spawner, "CleanupOldPlatforms", 60f, protectedCutoff);
        Assert.That(GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform"), Is.SameAs(active),
            "높은 선두를 따라간 카메라가 다음 선두 근처 쉼터를 먼저 지우면 안 됩니다.");

        follower.transform.position = new Vector3(0f, 80f, 0f);
        float caughtUpCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            60f);
        Invoke(spawner, "CleanupOldPlatforms", 60f, caughtUpCutoff);
        Assert.That(GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform"), Is.Null,
            "생존 먹떼와 카메라가 모두 지나간 쉼터는 정상 정리되어야 합니다.");
    }

    [Test]
    public void MapRestCleanupSingleSurvivorProtectsUntilReframeThenCleansAfterPassing()
    {
        var managerObject = new GameObject("SingleSurvivorGameManager");
        cleanup.Add(managerObject);
        var manager = managerObject.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        var playerObject = new GameObject("SingleRestCleanupSurvivor");
        cleanup.Add(playerObject);
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        player.transform.position = new Vector3(0f, 40f, 0f);
        manager.RegisterPlayer(player);

        var root = new GameObject("SingleSurvivorRestCleanupSpawner");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "nextMapRestHeight", 50f);
        SetField(spawner, "scheduledSessionVersion", GameplayRandom.SessionVersion);
        Invoke(spawner, "TrySpawnMapRest", 50f, 0f);
        var active = GetField<PlatformCollider>(spawner, "activeMapRestPlatform");
        Assert.That(active, Is.Not.Null);
        cleanup.Add(active.gameObject);

        float cutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            60f);
        Assert.That(cutoff, Is.EqualTo(32f).Within(0.001f));
        Invoke(spawner, "CleanupOldPlatforms", 60f, cutoff);
        Assert.That(GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform"), Is.SameAs(active),
            "한 명만 남은 사망 프레임에도 카메라 재구도 전 쉼터를 지우면 안 됩니다.");

        player.transform.position = new Vector3(0f, 80f, 0f);
        cutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            60f);
        Assert.That(cutoff, Is.EqualTo(60f).Within(0.001f));
        Invoke(spawner, "CleanupOldPlatforms", 60f, cutoff);
        Assert.That(GetField<PlatformCollider>(
            spawner,
            "activeMapRestPlatform"), Is.Null,
            "한 명뿐이어도 생존자와 카메라가 모두 지난 쉼터는 정상 정리해야 합니다.");
    }

    [Test]
    public void MapRestProtectionDoesNotRetainOldWindPlatforms()
    {
        var managerObject = new GameObject("WindCleanupGameManager");
        cleanup.Add(managerObject);
        var manager = managerObject.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        var playerObject = new GameObject("WindCleanupSurvivor");
        cleanup.Add(playerObject);
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        player.transform.position = new Vector3(0f, 40f, 0f);
        manager.RegisterPlayer(player);

        var root = new GameObject("WindCleanupSpawner");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "nextMapRestHeight", 50f);
        SetField(spawner, "scheduledSessionVersion", GameplayRandom.SessionVersion);
        Invoke(spawner, "TrySpawnMapRest", 50f, 0f);
        var active = GetField<PlatformCollider>(spawner, "activeMapRestPlatform");
        Assert.That(active, Is.Not.Null);
        cleanup.Add(active.gameObject);

        Invoke(
            spawner,
            "SpawnWindPlatform",
            new Vector2(0f, 20f),
            3f,
            "OLD");
        var spawned = GetField<List<PlatformCollider>>(spawner, "spawned");
        Assert.That(spawned.Count, Is.EqualTo(2));

        float restCutoff = (float)Invoke(
            spawner,
            "ResolveCleanupCutoffWorldY",
            60f);
        Invoke(spawner, "CleanupOldPlatforms", 60f, restCutoff);

        Assert.That(spawned.Count, Is.EqualTo(1));
        Assert.That(spawned[0], Is.SameAs(active),
            "생존자 보호선은 쉼터에만 적용하고 과거 풍맥은 카메라 기준으로 정리해야 합니다.");
    }

    [Test]
    public void DrawnPlatformUsesOneWayEffector()
    {
        var platform = Track(PlatformCollider.Spawn(CreatePoints()));

        Assert.IsFalse(platform.IsWindCurrentPlatform);
        Assert.IsTrue(platform.IsOneWayPlatform);
        AssertOneWayPhysics(platform);
    }

    [Test]
    public void DrawnSteepPlatformStillUsesOneWayEffector()
    {
        var platform = Track(PlatformCollider.Spawn(new List<Vector2>
        {
            new(-0.2f, -2f),
            new(0.2f, 2f),
        }));

        Assert.That(platform.Length, Is.GreaterThan(4f));
        AssertOneWayPhysics(platform);
    }

    [Test]
    public void PermanentStarterPlatformRemainsBidirectional()
    {
        var host = new GameObject("PermanentStarterPlatformProbe");
        cleanup.Add(host);
        host.AddComponent<LineRenderer>();
        host.AddComponent<EdgeCollider2D>();
        var platform = host.AddComponent<PlatformCollider>();

        platform.ConfigurePermanentInkLine(new[]
        {
            new Vector2(-2f, 0f),
            new Vector2(2f, 0f),
        });

        Assert.IsFalse(platform.IsOneWayPlatform);
        AssertSolidPhysics(platform);
    }

    [Test]
    public void DrawnPlatformBudgetUsesRetainedLengthInsteadOfFourObjects()
    {
        var platforms = new List<PlatformCollider>();
        for (int i = 0; i < 8; i++)
            platforms.Add(Track(PlatformCollider.Spawn(CreatePoints(), 1f)));

        for (int i = 0; i < platforms.Count; i++)
        {
            var edge = platforms[i].GetComponent<EdgeCollider2D>();
            Assert.IsNotNull(edge);
            Assert.IsTrue(edge.enabled,
                "최대 먹 용량 안에서는 발판 개수와 무관하게 충돌이 유지되어야 합니다.");
        }

        PlatformCollider.ReconcileActiveInkBudget(4f);
        Assert.That(PlatformCollider.ActiveInkCost,
            Is.EqualTo(4f).Within(0.0001f));
        for (int i = 0; i < platforms.Count; i++)
        {
            float target = GetField<float>(platforms[i], "evictionTargetFraction");
            Assert.That(target, Is.EqualTo(i < 4 ? 1f : 0f).Within(0.0001f),
                $"오래된 네 획부터 순서대로 소멸 예약되어야 합니다. index={i}");
        }
    }

    [TestCase(0f, 0f)]
    [TestCase(-20f, -3f)]
    [TestCase(128f, 82f)]
    public void WindScheduleAlwaysAdvancesWithInvalidOrReversedRange(float min, float max)
    {
        var root = new GameObject("RestPlatformSpawnerTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "windHeightIntervalRange", new Vector2(min, max));

        spawner.DebugResetSchedule(40);

        float next = GetField<float>(spawner, "nextWindHeight");
        Assert.That(next, Is.GreaterThan(40f),
            "풍맥 간격이 0 이하이면 Update의 while이 끝나지 않을 수 있습니다.");
    }

    [Test]
    public void WindScheduleRepairsNonFiniteRange()
    {
        var root = new GameObject("RestPlatformSpawnerFiniteTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();
        SetField(spawner, "windHeightIntervalRange",
            new Vector2(float.NaN, float.PositiveInfinity));

        spawner.DebugResetSchedule(75);

        float next = GetField<float>(spawner, "nextWindHeight");
        Assert.That(float.IsNaN(next) || float.IsInfinity(next), Is.False);
        Assert.That(next, Is.GreaterThan(75f));
    }

    [Test]
    public void DebugResetScheduleDestroysPreviouslySpawnedWindPlatforms()
    {
        var root = new GameObject("RestPlatformSpawnerCleanupTests");
        cleanup.Add(root);
        var spawner = root.AddComponent<RestPlatformSpawner>();

        InvokeSpawnWindPlatform(spawner, new Vector2(-1f, 20f), "FIRST");
        InvokeSpawnWindPlatform(spawner, new Vector2(1f, 30f), "SECOND");

        var spawned = GetField<List<PlatformCollider>>(spawner, "spawned");
        Assert.That(spawned, Has.Count.EqualTo(2));
        var first = spawned[0];
        var second = spawned[1];
        cleanup.Add(first.gameObject);
        cleanup.Add(second.gameObject);

        spawner.DebugResetSchedule(250);

        Assert.That(spawned, Is.Empty,
            "디버그 고도 이동 시 이전 풍맥 목록이 남으면 왕복마다 계속 누적됩니다.");
        Assert.IsTrue(first == null,
            "디버그 리셋은 이전에 생성한 풍맥 오브젝트까지 제거해야 합니다.");
        Assert.IsTrue(second == null,
            "고고도 풍맥도 카메라 아래 정리 조건과 무관하게 제거해야 합니다.");
        Assert.That(GetField<float>(spawner, "nextWindHeight"), Is.GreaterThan(250f));
    }

    PlatformCollider Track(PlatformCollider platform)
    {
        cleanup.Add(platform.gameObject);
        return platform;
    }

    PlayerController CreateLivingPlayer(
        GameManager manager,
        string name,
        float worldY)
    {
        var playerObject = new GameObject(name);
        cleanup.Add(playerObject);
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        player.transform.position = new Vector3(0f, worldY, 0f);
        manager.RegisterPlayer(player);
        return player;
    }

    static List<Vector2> CreatePoints(float halfWidth = 2f)
    {
        return new List<Vector2>
        {
            new(-halfWidth, 0f),
            new(0f, 0.12f),
            new(halfWidth, 0f),
        };
    }

    static void AssertOneWayTintablePlatform(PlatformCollider platform, Color expectedColor)
    {
        AssertOneWayPhysics(platform);

        var line = platform.Line;
        Assert.IsNotNull(line);
        Assert.AreSame(FallbackInkStyle.SharedTintableBrushMaterial, line.sharedMaterial,
            "특수 발판 안쪽 선은 효과색을 보존하는 전용 붓 재질을 사용해야 합니다.");
        Assert.AreNotSame(FallbackInkStyle.SharedInkMaterial, line.sharedMaterial,
            "검정 먹선 재질을 사용하면 풍맥 효과색이 검게 곱해집니다.");

        expectedColor.a = 0.96f;
        AssertColor(expectedColor, line.startColor);
        AssertColor(expectedColor, line.endColor);
    }

    static void AssertOneWayPhysics(PlatformCollider platform)
    {
        var effectors = platform.GetComponents<PlatformEffector2D>();
        Assert.That(effectors, Has.Length.EqualTo(1),
            "먹선 발판에는 중복 없이 하나의 PlatformEffector2D만 있어야 합니다.");
        var effector = effectors[0];
        Assert.IsTrue(effector.enabled);
        Assert.IsTrue(effector.useOneWay,
            "먹선 발판은 아래에서 통과하는 단방향 충돌이어야 합니다.");
        Assert.IsFalse(effector.useOneWayGrouping);
        Assert.That(effector.surfaceArc, Is.EqualTo(165f).Within(0.001f));
        Assert.IsFalse(effector.useColliderMask);

        var edge = platform.GetComponent<EdgeCollider2D>();
        Assert.IsNotNull(edge);
        Assert.IsTrue(edge.usedByEffector,
            "먹선의 EdgeCollider2D가 단방향 Effector를 사용해야 합니다.");
    }

    static void AssertSolidPhysics(PlatformCollider platform)
    {
        var edge = platform.GetComponent<EdgeCollider2D>();
        Assert.IsNotNull(edge);
        Assert.IsTrue(edge.enabled);
        Assert.IsFalse(edge.usedByEffector,
            "씬에 영구 배치된 시작 지형은 양방향 충돌이어야 합니다.");

        var effector = platform.GetComponent<PlatformEffector2D>();
        if (effector == null)
            return;
        Assert.IsFalse(effector.enabled,
            "구 씬에 남은 단방향 Effector는 일반 발판에서 비활성화해야 합니다.");
        Assert.IsFalse(effector.useOneWay);
    }

    static void SimulateFor(float seconds)
    {
        const float step = 0.02f;
        int count = Mathf.CeilToInt(seconds / step);
        for (int i = 0; i < count; i++)
            Physics2D.Simulate(step);
    }

    static void AssertColor(Color expected, Color actual)
    {
        const float tolerance = 0.001f;
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static object Invoke(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
    }

    static void InvokeSpawnWindPlatform(
        RestPlatformSpawner spawner,
        Vector2 center,
        string suffix)
    {
        spawner.GetType().GetMethod(
                "SpawnWindPlatform", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(spawner, new object[] { center, 3.4f, suffix });
    }

    static T GetField<T>(object target, string fieldName)
    {
        object value = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(target);
        return value is T typed ? typed : default;
    }
}
