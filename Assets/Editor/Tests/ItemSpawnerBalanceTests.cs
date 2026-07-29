using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
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
    public void DrawingBalanceUsesFasterRecoveryAndShorterLifetime()
    {
        var capture = Track(new GameObject("StrokeCapture"))
            .AddComponent<MukJump.Drawing.StrokeCapture>();
        var platform = Track(new GameObject("Platform"))
            .AddComponent<MukJump.Drawing.PlatformCollider>();

        Assert.AreEqual(3f, (float)GetField(capture, "inkRegenPerSecond"));
        Assert.AreEqual(4.5f, (float)GetField(platform, "lifetime"));
        Assert.AreEqual(0.8f, (float)GetField(platform, "fadeDuration"));
    }

    [Test]
    public void SwarmCameraUsesLowerMedianInsteadOfSingleOutlier()
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
    public void TwoPlayerSwarmFollowsLowerPlayerDuringLeaderBoost()
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

    [Test]
    public void ClonePickupAddsExactlyOnePlayer()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>();
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Assert.AreEqual(1, manager.LivingPlayerCount);
        Assert.IsTrue(ItemEffect.Apply(ItemType.InkClone, source));
        Assert.AreEqual(2, manager.LivingPlayerCount,
            "먹분신 아이템 한 번은 정확히 한 마리만 늘려야 합니다.");

        var living = new List<PlayerController>();
        manager.GetLivingPlayersNonAlloc(living);
        for (int i = 0; i < living.Count; i++)
            if (living[i] != source)
                Track(living[i].gameObject);
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
    public void CloneSpawnChoosesOpenScreenCandidateInsteadOfFixedPile()
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
        sourceObject.AddComponent<CircleCollider2D>();
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);
        for (int i = 0; i < 7; i++)
        {
            var occupied = Track(new GameObject($"Occupied_{i}"));
            occupied.transform.position = new Vector3(-1.9f + i % 2 * 3.8f, 0f, 0f);
            occupied.AddComponent<Rigidbody2D>();
            occupied.AddComponent<CircleCollider2D>();
            manager.RegisterPlayer(occupied.AddComponent<PlayerController>());
        }

        Vector3 result = (Vector3)Invoke(manager, "FindCloneSpawnPosition", source, 8);

        Assert.Greater(Mathf.Abs(Mathf.Abs(result.x) - 1.9f), 0.25f,
            "8마리 이후에도 ±1.9 위치에 계속 겹치면 안 됩니다.");
        float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
        Assert.That(result.x, Is.InRange(-halfWidth + 0.65f, halfWidth - 0.65f));
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
        sourceObject.AddComponent<CircleCollider2D>();
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        source.transform.position = new Vector3(1.25f, 7f, 0f);
        Vector3 fromLeft = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Assert.Greater(fromLeft.x, cameraObject.transform.position.x,
            "카메라 왼쪽에서 먹분신을 획득하면 새 분신은 오른쪽 절반에 생겨야 합니다.");

        source.transform.position = new Vector3(5.25f, 7f, 0f);
        Vector3 fromRight = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);
        Assert.Less(fromRight.x, cameraObject.transform.position.x,
            "카메라 오른쪽에서 먹분신을 획득하면 새 분신은 왼쪽 절반에 생겨야 합니다.");
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
        sourceObject.AddComponent<CircleCollider2D>();
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Vector3 odd = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Vector3 even = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);

        Assert.Greater(odd.x, cameraObject.transform.position.x);
        Assert.Less(even.x, cameraObject.transform.position.x);
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

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.NonPublic)?.Invoke(target, arguments);
    }

}
