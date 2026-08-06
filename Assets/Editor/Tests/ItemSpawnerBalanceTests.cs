using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;

public sealed class ItemSpawnerBalanceTests
{
    readonly List<Object> cleanup = new();
    readonly List<Camera> retaggedMainCameras = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < retaggedMainCameras.Count; i++)
            if (retaggedMainCameras[i] != null)
                retaggedMainCameras[i].gameObject.tag = "MainCamera";
        retaggedMainCameras.Clear();

        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void IntroSlotIsAlwaysCloneForEverySeed()
    {
        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();
        for (int seed = 0; seed < 128; seed++)
        {
            GameplayRandom.ResetSession(seed);
            var type = (ItemType)Invoke(spawner, "ChooseItemType", 12f, true);
            Assert.AreEqual(ItemType.InkClone, type, $"seed {seed}");
        }
    }

    [Test]
    public void FirstItemWorldPositionUsesScoreOrigin()
    {
        var score = Track(new GameObject("ScoreManager")).AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        score.ResetOrigin(-6f);
        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();

        float worldY = (float)Invoke(spawner, "WorldYAtGameHeight", 12f);

        Assert.AreEqual(6f, worldY, 0.001f);
        Assert.AreEqual(12f, score.HeightAt(worldY), 0.001f);
    }

    [Test]
    public void CloneCapIs24AndSpawnerExcludesDeadPickupAtCap()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        for (int i = 0; i < GameManager.MaxLivingPlayers; i++)
        {
            var playerObject = Track(new GameObject($"Player_{i:00}"));
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            manager.RegisterPlayer(playerObject.AddComponent<PlayerController>());
        }

        Assert.AreEqual(24, manager.LivingPlayerCount);
        Assert.IsFalse(manager.CanCreateInkClone);

        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();
        for (int seed = 0; seed < 64; seed++)
        {
            GameplayRandom.ResetSession(seed);
            var type = (ItemType)Invoke(spawner, "ChooseItemType", 250f, true);
            Assert.AreNotEqual(ItemType.InkClone, type,
                "상한에서는 먹어도 무효인 분신 픽업을 생성하면 안 됩니다.");
        }
    }

    [Test]
    public void DrawingBalanceUsesRetainedInkCapacityAndGradualEviction()
    {
        var capture = Track(new GameObject("StrokeCapture"))
            .AddComponent<MukJump.Drawing.StrokeCapture>();

        Assert.AreEqual(
            StrokeCapture.DefaultInkCapacity,
            (float)GetField(capture, "inkCapacity"));
        Assert.AreEqual(1.1f, (float)GetField(capture, "evictionFadeDuration"));
        Assert.AreEqual(
            PlatformCollider.DefaultNaturalHoldDuration,
            (float)GetField(capture, "naturalHoldDuration"));
        Assert.AreEqual(4.5f, capture.EffectiveNaturalInkLifetime, 0.0001f);
    }

    [TestCase(12f, 0)]
    [TestCase(18f, 0)]
    [TestCase(24f, 1)]
    public void LegacySceneInkCapacityUpgradesToCurrentBalance(
        float legacyCapacity,
        int legacyTuningVersion)
    {
        var capture = Track(new GameObject("LegacyStrokeCapture"))
            .AddComponent<StrokeCapture>();
        SetField(capture, "inkCapacity", legacyCapacity);
        SetField(capture, "inkCapacityTuningVersion", legacyTuningVersion);

        Invoke(capture, "UpgradeInkCapacityTuning");

        Assert.AreEqual(
            StrokeCapture.DefaultInkCapacity,
            (float)GetField(capture, "inkCapacity"));
        Assert.AreEqual(
            StrokeCapture.CurrentInkCapacityTuningVersion,
            (int)GetField(capture, "inkCapacityTuningVersion"));
    }

    [Test]
    public void InkGaugeTrackRepresentsFullTwoPointFiveCapacityGrowth()
    {
        MethodInfo method = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeTrackWidth",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        const float safeWidth = 1000f;
        const float horizontalMargin = 40f;
        float baseWidth = (float)method.Invoke(
            null,
            new object[] { safeWidth, horizontalMargin, 1f });
        float fullGrowthWidth = (float)method.Invoke(
            null,
            new object[] { safeWidth, horizontalMargin, 2.5f });

        Assert.That(baseWidth, Is.EqualTo(330f).Within(0.001f));
        Assert.That(fullGrowthWidth, Is.EqualTo(825f).Within(0.001f));
        Assert.That(fullGrowthWidth / baseWidth,
            Is.EqualTo(2.5f).Within(0.001f));

        // 실제 1024×256 게이지와 붓 아이콘의 겹침까지 포함해도 4% 여백 안에 들어가
        // DrawInkGauge의 후속 fit 단계가 2.5배 폭을 다시 줄이지 않아야 합니다.
        float gaugeHeight = fullGrowthWidth * 0.25f;
        float iconSize = gaugeHeight;
        float clusterWidth = fullGrowthWidth + iconSize - iconSize * 0.65f;
        Assert.That(clusterWidth,
            Is.LessThanOrEqualTo(safeWidth - horizontalMargin * 2f));
    }

    [Test]
    public void SwarmProgressUsesLowerMedianInsteadOfSingleOutlier()
    {
        var players = new List<PlayerController>();
        float[] heights = { 100f, 12f, 11f, 10f, 9f };
        for (int i = 0; i < heights.Length; i++)
        {
            var playerObject = Track(new GameObject($"CameraPlayer_{i}"));
            playerObject.transform.position = Vector3.up * heights[i];
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        float followY = GameManager.ResolveSwarmAnchorY(
            players, out var representative);

        Assert.AreEqual(11f, followY, 0.001f);
        Assert.AreEqual(11f, representative.transform.position.y, 0.001f);
    }

    [Test]
    public void TwoPlayerProgressUsesLowerPlayerDuringLeaderBoost()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 50f, 10f })
        {
            var playerObject = Track(new GameObject($"TwoPlayer_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        float followY = GameManager.ResolveSwarmAnchorY(
            players, out var representative);

        Assert.AreEqual(10f, followY, 0.001f);
        Assert.AreEqual(10f, representative.transform.position.y, 0.001f);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(24)]
    public void SwarmCameraFrameUsesHighestLivingPlayer(int playerCount)
    {
        var players = new List<PlayerController>(playerCount);
        for (int i = playerCount - 1; i >= 0; i--)
        {
            var playerObject = Track(new GameObject($"FramePlayer_{i}"));
            playerObject.transform.position = Vector3.up * i;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        bool resolved = GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY);

        Assert.That(resolved, Is.True);
        Assert.That(representative, Is.Not.Null);
        float expectedHighest = playerCount - 1;
        Assert.That(clusterY, Is.EqualTo(expectedHighest).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(expectedHighest).Within(0.001f));
        Assert.That(representative.transform.position.y,
            Is.EqualTo(expectedHighest).Within(0.001f));
    }

    [Test]
    public void SwarmCameraTracksHighestLivingOutlier()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 100f, 12f, 11f, 10f, 9f })
        {
            var playerObject = Track(new GameObject($"GuardPlayer_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        Assert.That(GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY), Is.True);
        Assert.That(representative.transform.position.y,
            Is.EqualTo(100f).Within(0.001f));
        Assert.That(clusterY, Is.EqualTo(100f).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(100f).Within(0.001f),
            "본체 여부와 무관하게 가장 높은 생존 먹방울을 놓치면 안 됩니다.");
    }

    [Test]
    public void SwarmCameraFrameDropsDeadOriginalAndUsesLivingClones()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 4f, 8f, 12f })
        {
            var playerObject = Track(new GameObject($"LivingClone_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }
        typeof(PlayerController).GetProperty(
                nameof(PlayerController.IsDead),
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)
            ?.SetValue(players[0], true);
        PlayerController expectedRepresentative = players[2];

        Assert.That(GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY), Is.True);
        Assert.That(representative, Is.SameAs(expectedRepresentative),
            "사망 원본을 제거한 뒤 가장 높은 생존 분신이 카메라 대표여야 합니다.");
        Assert.That(clusterY, Is.EqualTo(12f).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(12f).Within(0.001f));
    }

    [Test]
    public void ClonePickupWithoutGrowthAddsExactlyOnePlayer()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneSource"));
        var sourceBody = sourceObject.AddComponent<Rigidbody2D>();
        sourceBody.linearVelocity = new Vector2(1.25f, 2.5f);
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Assert.AreEqual(1, manager.LivingPlayerCount);
        Assert.IsTrue(ItemEffect.Apply(ItemType.InkClone, source));
        Assert.AreEqual(2, manager.LivingPlayerCount,
            "성장이 없으면 먹분신 아이템 한 번은 기본 한 마리만 늘려야 합니다.");

        var living = new List<PlayerController>();
        manager.GetLivingPlayersNonAlloc(living);
        for (int i = 0; i < living.Count; i++)
            if (living[i] != source)
            {
                Assert.That(living[i].Body.linearVelocity,
                    Is.EqualTo(sourceBody.linearVelocity),
                    "생성 직후 분신을 옆으로 밀어 원본과 다시 벌리면 안 됩니다.");
                Track(living[i].gameObject);
            }
    }

    [TestCase(0, 24, 1)]
    [TestCase(4, 24, 5)]
    [TestCase(4, 1, 1)]
    [TestCase(4, 0, 0)]
    [TestCase(99, 24, 5)]
    public void ClonePickupGrowthAddsUpToFiveWithinLivingCap(
        int growthExtraCount,
        int availableSlots,
        int expectedCount)
    {
        Assert.AreEqual(
            expectedCount,
            GameManager.ResolveInkCloneItemSpawnCount(
                growthExtraCount,
                availableSlots));
    }

    [Test]
    public void InkDropLaunchesEveryLivingCloneTogether()
    {
        var manager = Track(new GameObject("InkDropSwarmManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var firstObject = Track(new GameObject("InkDropPlayerA"));
        var firstBody = firstObject.AddComponent<Rigidbody2D>();
        firstBody.gravityScale = 1f;
        firstObject.AddComponent<CircleCollider2D>();
        var first = firstObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(first);

        var secondObject = Track(new GameObject("InkDropPlayerB"));
        secondObject.transform.position = new Vector3(0f, 8f, 0f);
        var secondBody = secondObject.AddComponent<Rigidbody2D>();
        secondBody.gravityScale = 1f;
        secondObject.AddComponent<CircleCollider2D>();
        var second = secondObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(second);

        Assert.IsTrue(ItemEffect.Apply(ItemType.InkDrop, second));

        Assert.IsTrue(first.IsInkDropBoosted);
        Assert.IsTrue(second.IsInkDropBoosted);
        Assert.That(firstBody.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(secondBody.linearVelocity.y,
            Is.EqualTo(firstBody.linearVelocity.y).Within(0.001f),
            "먹물방울을 먹은 분신만 카메라 위로 이탈하면 안 됩니다.");
    }

    [Test]
    public void SwarmProgressHeightUsesGroupAnchorInsteadOfScoreLeader()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        var score = Track(new GameObject("ScoreManager")).AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        score.ResetOrigin(-6f);

        var progressPlayers = new List<PlayerController>();
        foreach (float worldY in new[] { 44f, 4f })
        {
            var playerObject = Track(new GameObject($"ProgressPlayer_{worldY}"));
            playerObject.transform.position = Vector3.up * worldY;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            var progressPlayer = playerObject.AddComponent<PlayerController>();
            manager.RegisterPlayer(progressPlayer);
            progressPlayers.Add(progressPlayer);
        }

        Assert.AreEqual(10f, manager.SwarmProgressHeight, 0.001f,
            "50m로 튄 선두가 10m의 먹떼보다 먼저 위험물을 열면 안 됩니다.");

        foreach (var player in progressPlayers)
            player.transform.position -= Vector3.up * 8f;
        Assert.AreEqual(10f, manager.SwarmProgressHeight, 0.001f,
            "일반 플레이 구간 진행은 점프 하강 때문에 뒤로 돌아가면 안 됩니다.");
    }

    [Test]
    public void DrawingSurfacePaddingShrinksWithoutOverlappingPlayerPhysics()
    {
        var capture = Track(new GameObject("StrokeCapture"))
            .AddComponent<MukJump.Drawing.StrokeCapture>();

        float single = (float)Invoke(capture, "ResolvePlayerSurfacePadding", 1);
        float full = (float)Invoke(capture, "ResolvePlayerSurfacePadding", 24);

        Assert.AreEqual(0.15f, single, 0.001f);
        Assert.AreEqual(0.08f, full, 0.001f);

        var playerObject = Track(new GameObject("ClearancePlayer"));
        var playerCollider = playerObject.AddComponent<CircleCollider2D>();
        playerCollider.radius = 0.4f;
        playerCollider.offset = new Vector2(0f, 0.1f);
        playerObject.AddComponent<PlayerController>();
        float platformY = playerCollider.offset.y + playerCollider.radius + full;
        var platform = Track(MukJump.Drawing.PlatformCollider.Spawn(
            new List<Vector2>
            {
                new(-1f, platformY),
                new(1f, platformY),
            }));
        Physics2D.SyncTransforms();

        Assert.IsFalse(playerCollider.Distance(
            platform.GetComponent<EdgeCollider2D>()).isOverlapped,
            "먹떼용 최소 안전거리에서도 발판 물리가 캐릭터 안쪽에 생기면 안 됩니다.");
    }

    [Test]
    public void CloneSpawnStaysImmediatelyBesideCollector()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("CloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Vector3 result = (Vector3)Invoke(manager, "FindCloneSpawnPosition", source, 1);

        Assert.That(Mathf.Abs(result.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f),
            "반지름 0.4 캐릭터는 0.1 간격을 두고 바로 옆에 생겨야 합니다.");
        Assert.That(result.y, Is.EqualTo(source.transform.position.y).Within(0.001f));
    }

    [Test]
    public void CloneSpawnsOnOppositeSideOfCollectorWithOffsetCamera()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("OffsetCloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(3.25f, 14f, -10f);
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("OppositeCloneManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("OppositeCloneSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        source.transform.position = new Vector3(1.25f, 7f, 0f);
        Vector3 fromLeft = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Assert.Greater(fromLeft.x, source.transform.position.x,
            "카메라 왼쪽의 획득자에게는 화면 안쪽인 오른편 바로 옆에 생겨야 합니다.");
        Assert.That(fromLeft.x - source.transform.position.x,
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.Less(fromLeft.x, cameraObject.transform.position.x,
            "인접 생성 때문에 화면 중앙을 넘어 멀리 떨어지면 안 됩니다.");

        source.transform.position = new Vector3(5.25f, 7f, 0f);
        Vector3 fromRight = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);
        Assert.Less(fromRight.x, source.transform.position.x,
            "카메라 오른쪽의 획득자에게는 화면 안쪽인 왼편 바로 옆에 생겨야 합니다.");
        Assert.That(source.transform.position.x - fromRight.x,
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.Greater(fromRight.x, cameraObject.transform.position.x,
            "인접 생성 때문에 화면 중앙을 넘어 멀리 떨어지면 안 됩니다.");
    }

    [Test]
    public void CloneSpawnAtCameraCenterAlternatesSides()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("CenteredCloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(-2f, 6f, -10f);
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("CenteredCloneManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CenteredCloneSource"));
        sourceObject.transform.position = new Vector3(-2f, 2f, 0f);
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Vector3 odd = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Vector3 even = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);

        Assert.Greater(odd.x, cameraObject.transform.position.x);
        Assert.Less(even.x, cameraObject.transform.position.x);
        Assert.That(Mathf.Abs(odd.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(Mathf.Abs(even.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f));
    }

    void RetagExistingMainCameras()
    {
        var cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (!candidate.CompareTag("MainCamera")) continue;
            candidate.gameObject.tag = "Untagged";
            retaggedMainCameras.Add(candidate);
        }
    }

    T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    static object GetField(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.NonPublic)?.Invoke(target, arguments);
    }

}
