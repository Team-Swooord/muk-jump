using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Obstacles;
using MukJump.Player;

public sealed class MovingObstacleTests
{
    readonly List<Object> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void DragonAndSpikeUseMutuallyExclusiveCollidersAcrossReuse()
    {
        var go = Track(new GameObject("ObstacleVariant"));
        var obstacle = go.AddComponent<Obstacle>();
        Invoke(obstacle, "Awake");

        obstacle.OnPoolAcquire();
        obstacle.Configure(1f, 0.6f, 0f, ObstacleKind.ChildDragon);

        var circle = go.GetComponent<CircleCollider2D>();
        var capsule = go.GetComponent<CapsuleCollider2D>();
        Assert.IsFalse(circle.enabled);
        Assert.IsTrue(capsule.enabled);
        Assert.AreEqual(CapsuleDirection2D.Horizontal, capsule.direction);
        Assert.AreEqual(ObstacleKind.ChildDragon, obstacle.Kind);

        obstacle.OnPoolRelease();
        Assert.IsFalse(circle.enabled);
        Assert.IsFalse(capsule.enabled);
        Assert.IsFalse(go.GetComponent<SpriteRenderer>().flipX);

        obstacle.OnPoolAcquire();
        Assert.IsFalse(circle.enabled);
        Assert.IsFalse(capsule.enabled);
        obstacle.Configure(1f, 0.6f, 0f, ObstacleKind.Spike);
        Assert.IsTrue(circle.enabled);
        Assert.IsFalse(capsule.enabled);
        Assert.AreEqual(ObstacleKind.Spike, obstacle.Kind);
    }

    [Test]
    public void ValidPlayerContactConsumesObstacleAndReturnsItToSpawnerPool()
    {
        var managerObject = Track(new GameObject("ObstacleHitManager"));
        var manager = managerObject.AddComponent<GameManager>();
        SetAutoProperty(manager, "State", GameState.Playing);

        var spawnerObject = Track(new GameObject("ObstacleHitSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        SetField(spawner, "obstacleSprite", CreateSprite(100, 100));
        Invoke(spawner, "Spawn", 30f);
        var active = (IList)GetField(spawner, "active");
        Assert.That(active.Count, Is.EqualTo(1));
        var obstacle = (Obstacle)active[0];

        var playerObject = Track(new GameObject("ObstacleHitPlayer"));
        playerObject.AddComponent<SpriteRenderer>();
        playerObject.AddComponent<Rigidbody2D>();
        var playerCollider = playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        SetField(player, "damageInvulnerableUntil", Time.time - 1f);

        Invoke(obstacle, "OnTriggerEnter2D", playerCollider);

        Assert.That(player.CurrentHealth, Is.Zero);
        Assert.That(player.IsDead, Is.True,
            "기본 체력 1칸은 첫 무방비 장애물 피격에 소진되어야 합니다.");
        Assert.That(active.Count, Is.Zero,
            "유효 피격을 준 먹가시는 활성 목록에서 즉시 빠져야 합니다.");
        Assert.That(obstacle.gameObject.activeSelf, Is.False,
            "사라진 장애물은 파괴 대신 풀로 반환되어야 합니다.");
    }

    [Test]
    public void IgnoredBoostContactKeepsObstacleTriggerActive()
    {
        var managerObject = Track(new GameObject("IgnoredHitManager"));
        var manager = managerObject.AddComponent<GameManager>();
        SetAutoProperty(manager, "State", GameState.Playing);

        var obstacleObject = Track(new GameObject("IgnoredHitObstacle"));
        var obstacle = obstacleObject.AddComponent<Obstacle>();
        obstacle.Configure(0f, 0f, 0f, ObstacleKind.Spike);

        var playerObject = Track(new GameObject("BoostedPlayer"));
        playerObject.AddComponent<SpriteRenderer>();
        playerObject.AddComponent<Rigidbody2D>();
        var playerCollider = playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        Invoke(player, "Awake");
        player.LaunchInkDrop(1f, false);
        bool releaseRequested = false;
        obstacle.ReleaseRequested += _ => releaseRequested = true;

        Invoke(obstacle, "OnTriggerEnter2D", playerCollider);

        Assert.That(releaseRequested, Is.False);
        Assert.That(player.CurrentHealth, Is.EqualTo(1));
        Assert.That(obstacleObject.GetComponent<CircleCollider2D>().enabled, Is.True);
    }

    [Test]
    public void DragonVisibilityPreservesDefaultMaterialAcrossSpikePoolReuse()
    {
        var go = Track(new GameObject("ObstacleVisibility"));
        var renderer = go.AddComponent<SpriteRenderer>();
        var defaultMaterial = renderer.sharedMaterial;
        var visibility = go.AddComponent<ObstacleVisibilityView>();

        visibility.Configure();
        Assert.AreEqual("MukJump/ObstaclePaperRed",
            renderer.sharedMaterial.shader.name);

        visibility.Configure(preserveInkOutlines: true);

        Assert.AreSame(defaultMaterial, renderer.sharedMaterial);
        Assert.That(renderer.color.r,
            Is.EqualTo(InkPalette.ObstaclePaperRed.r).Within(0.001f));
        Assert.That(renderer.color.g,
            Is.EqualTo(InkPalette.ObstaclePaperRed.g).Within(0.001f));
        Assert.That(renderer.color.b,
            Is.EqualTo(InkPalette.ObstaclePaperRed.b).Within(0.001f));

        visibility.Configure();
        Assert.AreEqual("MukJump/ObstaclePaperRed",
            renderer.sharedMaterial.shader.name);
        visibility.Configure(preserveInkOutlines: true);
        Assert.AreSame(defaultMaterial, renderer.sharedMaterial);
    }

    [Test]
    public void FirstDragonIsGuaranteedAt60mButNeverBeforeUnlock()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        SetField(spawner, "dragonSprite", CreateSprite(300, 100));
        SetField(spawner, "dragonUnlockHeight", 60f);
        SetField(spawner, "dragonChance", 0f);
        SetField(spawner, "firstDragonPending", true);

        Assert.IsFalse((bool)Invoke(spawner, "ShouldSpawnDragon", 59.99f));
        Assert.IsTrue((bool)Invoke(spawner, "ShouldSpawnDragon", 60f));
        Assert.IsFalse((bool)Invoke(spawner, "ShouldSpawnDragon", 80f),
            "첫 보장 이후 확률이 0이면 다음 슬롯은 일반 장애물이어야 합니다.");
    }

    [Test]
    public void RuntimeFallbackLoadsFourSortedDragonFrames()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        SetField(spawner, "dragonSprite", null);
        SetField(spawner, "dragonFrames", null);

        Invoke(spawner, "LoadDragonVisuals");

        var frames = (Sprite[])GetField(spawner, "dragonFrames");
        Assert.IsNotNull(frames);
        Assert.AreEqual(4, frames.Length);
        for (int i = 0; i < frames.Length; i++)
            Assert.AreEqual($"child_ink_dragon_frame_{i:00}", frames[i].name);
        Assert.AreSame(frames[0], GetField(spawner, "dragonSprite"));
    }

    [Test]
    public void RuntimeFallbackRepairsPartiallyMissingSerializedFrames()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        var resourceFrames = Resources.LoadAll<Sprite>(
            "MukJump/Obstacles/child_ink_dragon_4frame_v3");
        Assert.AreEqual(4, resourceFrames.Length);
        System.Array.Sort(resourceFrames,
            (left, right) => string.CompareOrdinal(left.name, right.name));
        SetField(spawner, "dragonSprite", resourceFrames[0]);
        SetField(spawner, "dragonFrames", new[]
        {
            resourceFrames[0],
            null,
            resourceFrames[2],
            resourceFrames[3],
        });

        Invoke(spawner, "LoadDragonVisuals");

        var repairedFrames = (Sprite[])GetField(spawner, "dragonFrames");
        Assert.AreEqual(4, repairedFrames.Length);
        for (int i = 0; i < repairedFrames.Length; i++)
        {
            Assert.IsNotNull(repairedFrames[i]);
            Assert.AreEqual($"child_ink_dragon_frame_{i:00}",
                repairedFrames[i].name);
        }
        Assert.AreSame(repairedFrames[0], GetField(spawner, "dragonSprite"));
    }

    [Test]
    public void RuntimeFallbackReplacesSerializedFramesFromWrongTexture()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        var legacyFrames = CreateDragonFrames(300, 100);
        for (int i = 0; i < legacyFrames.Length; i++)
            legacyFrames[i].texture.name = "legacy_dragon_sheet";
        SetField(spawner, "dragonSprite", legacyFrames[0]);
        SetField(spawner, "dragonFrames", legacyFrames);

        Invoke(spawner, "LoadDragonVisuals");

        var repairedFrames = (Sprite[])GetField(spawner, "dragonFrames");
        Assert.AreEqual(4, repairedFrames.Length);
        for (int i = 0; i < repairedFrames.Length; i++)
            Assert.AreEqual("child_ink_dragon_4frame_v3",
                repairedFrames[i].texture.name);
        Assert.AreSame(repairedFrames[0], GetField(spawner, "dragonSprite"));
    }

    [Test]
    public void DragonSheetUsesDistinctStableSilhouettesWithoutCellEdgeBleed()
    {
        const string assetPath =
            "Assets/Resources/MukJump/Obstacles/child_ink_dragon_4frame_v3.png";
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        Assert.IsNotNull(projectRoot);
        string fullPath = Path.Combine(projectRoot, assetPath);
        Assert.IsTrue(File.Exists(fullPath));

        var texture = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        Assert.IsTrue(ImageConversion.LoadImage(
            texture, File.ReadAllBytes(fullPath), false));
        Assert.AreEqual(1536, texture.width);
        Assert.AreEqual(1024, texture.height);

        const int columns = 2;
        const int rows = 2;
        const int frameWidth = 768;
        const int frameHeight = 512;
        const byte visibleAlpha = 128;
        var pixels = texture.GetPixels32();
        var masks = new bool[columns * rows][];
        var centroidX = new float[masks.Length];
        var centroidY = new float[masks.Length];

        for (int frameIndex = 0; frameIndex < masks.Length; frameIndex++)
        {
            int column = frameIndex % columns;
            int row = frameIndex / columns;
            int originX = column * frameWidth;
            int originY = (rows - 1 - row) * frameHeight;
            int minX = frameWidth;
            int minY = frameHeight;
            int maxX = -1;
            int maxY = -1;
            int visibleCount = 0;
            long sumX = 0;
            long sumY = 0;
            var mask = new bool[frameWidth * frameHeight];
            masks[frameIndex] = mask;

            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int sourceIndex = originX + x +
                                      (originY + y) * texture.width;
                    bool visible = pixels[sourceIndex].a >= visibleAlpha;
                    mask[x + y * frameWidth] = visible;
                    if (!visible) continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                    visibleCount++;
                    sumX += x;
                    sumY += y;
                }
            }

            Assert.Greater(visibleCount, 45000,
                $"용 프레임 {frameIndex}의 보이는 실루엣이 비정상적으로 작습니다.");
            // 굵어진 붓꼬리의 반투명 번짐은 셀 가장자리 6px 전까지만 허용한다.
            // 실제 경계에는 닿지 않아 이웃 프레임으로 번지지 않는다.
            const int safeCellMargin = 6;
            Assert.That(minX, Is.GreaterThanOrEqualTo(safeCellMargin));
            Assert.That(minY, Is.GreaterThanOrEqualTo(safeCellMargin));
            Assert.That(maxX, Is.LessThan(frameWidth - safeCellMargin));
            Assert.That(maxY, Is.LessThan(frameHeight - safeCellMargin));
            centroidX[frameIndex] = sumX / (float)visibleCount;
            centroidY[frameIndex] = sumY / (float)visibleCount;
        }

        var importedAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int frameIndex = 0; frameIndex < masks.Length; frameIndex++)
        {
            Sprite importedFrame = null;
            string expectedName =
                $"child_ink_dragon_frame_{frameIndex:00}";
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (importedAssets[i] is not Sprite sprite ||
                    sprite.name != expectedName)
                    continue;
                importedFrame = sprite;
                break;
            }

            Assert.IsNotNull(importedFrame, expectedName);
            Assert.That(importedFrame.pivot.x,
                Is.EqualTo(centroidX[frameIndex] + 0.5f).Within(1f),
                "몸 굽힘으로 달라진 가로 무게중심은 커스텀 피벗이 보정해야 합니다.");
            Assert.That(importedFrame.pivot.y,
                Is.EqualTo(centroidY[frameIndex] + 0.5f).Within(1f),
                "몸 굽힘으로 달라진 세로 무게중심은 커스텀 피벗이 보정해야 합니다.");
        }

        for (int frameIndex = 0; frameIndex < masks.Length; frameIndex++)
        {
            var current = masks[frameIndex];
            int nextFrameIndex = (frameIndex + 1) % masks.Length;
            var next = masks[nextFrameIndex];
            int sampleOffsetX = Mathf.RoundToInt(
                centroidX[nextFrameIndex] - centroidX[frameIndex]);
            int sampleOffsetY = Mathf.RoundToInt(
                centroidY[nextFrameIndex] - centroidY[frameIndex]);
            int union = 0;
            int changed = 0;
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    bool currentVisible = current[x + y * frameWidth];
                    int sampleX = x + sampleOffsetX;
                    int sampleY = y + sampleOffsetY;
                    bool nextVisible =
                        sampleX >= 0 && sampleX < frameWidth &&
                        sampleY >= 0 && sampleY < frameHeight &&
                        next[sampleX + sampleY * frameWidth];
                    if (currentVisible || nextVisible) union++;
                    if (currentVisible != nextVisible) changed++;
                }
            }

            float changeRatio = changed / (float)union;
            Assert.Greater(changeRatio, 0.05f,
                $"용 프레임 {frameIndex}은 다음 프레임과 관절 움직임이 부족합니다.");
            Assert.Less(changeRatio, 0.2f,
                $"용 프레임 {frameIndex}은 몸 전체가 바뀌는 것처럼 과도하게 변형됩니다.");
        }
    }

    [Test]
    public void DragonSpawnStaysInsideHorizontalBoundsAndUsesFairCapsule()
    {
        GameplayRandom.ResetSession(20260727);
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        var spike = CreateSprite(100, 100);
        var dragonFrames = CreateDragonFrames(300, 100);
        SetField(spawner, "obstacleSprite", spike);
        SetField(spawner, "dragonSprite", dragonFrames[0]);
        SetField(spawner, "dragonFrames", dragonFrames);
        SetField(spawner, "dragonUnlockHeight", 60f);
        SetField(spawner, "firstDragonPending", true);

        Invoke(spawner, "Spawn", 60f);

        var active = (IList)GetField(spawner, "active");
        Assert.AreEqual(1, active.Count, "한 고도 슬롯에는 장애물 하나만 생성되어야 합니다.");
        var obstacle = (Obstacle)active[0];
        cleanup.Add(obstacle.gameObject);
        Assert.AreEqual(ObstacleKind.ChildDragon, obstacle.Kind);
        Assert.AreEqual(4, obstacle.AnimationFrameCount);

        float halfWorldWidth = 1.6f;
        float amplitude = (float)GetField(obstacle, "amplitude");
        float originX = obstacle.transform.position.x;
        Assert.GreaterOrEqual(originX - amplitude - halfWorldWidth, -4.1001f);
        Assert.LessOrEqual(originX + amplitude + halfWorldWidth, 4.1001f);

        var capsule = obstacle.GetComponent<CapsuleCollider2D>();
        Vector3 scale = obstacle.transform.lossyScale;
        Vector2 worldSize = new(
            capsule.size.x * scale.x,
            capsule.size.y * scale.y);
        Assert.That(worldSize.x, Is.EqualTo(2.56f).Within(0.02f));
        Assert.That(worldSize.y, Is.EqualTo(0.523f).Within(0.02f));
        Assert.IsFalse(obstacle.GetComponent<CircleCollider2D>().enabled);
        Assert.IsTrue(capsule.enabled);
    }

    [Test]
    public void DragonAnimationLoopsKeepsFacingAndClearsAcrossPoolReuse()
    {
        var go = Track(new GameObject("AnimatedDragon"));
        var obstacle = go.AddComponent<Obstacle>();
        Invoke(obstacle, "Awake");
        var renderer = go.GetComponent<SpriteRenderer>();
        var frames = CreateDragonFrames(300, 100);

        obstacle.OnPoolAcquire();
        obstacle.ConfigureSpriteAnimation(frames, 0.2f);
        obstacle.Configure(1f, 0.6f, 0f, ObstacleKind.ChildDragon);
        Assert.AreSame(frames[0], renderer.sprite);
        Assert.AreEqual(0, obstacle.CurrentAnimationFrameIndex);
        Assert.IsTrue(renderer.flipX, "오른쪽 이동 중에는 왼쪽 얼굴 원본을 뒤집어야 합니다.");

        Invoke(obstacle, "AdvanceSpriteAnimation", 0.21f);
        Assert.AreSame(frames[1], renderer.sprite);
        Assert.AreEqual(1, obstacle.CurrentAnimationFrameIndex);
        Assert.IsTrue(renderer.flipX, "프레임 교체가 이동 방향 반전을 지우면 안 됩니다.");

        Invoke(obstacle, "AdvanceSpriteAnimation", 0.6f);
        Assert.AreSame(frames[0], renderer.sprite);
        Assert.AreEqual(0, obstacle.CurrentAnimationFrameIndex);

        obstacle.OnPoolRelease();
        Assert.AreEqual(0, obstacle.AnimationFrameCount);
        Assert.AreEqual(0, obstacle.CurrentAnimationFrameIndex);

        obstacle.OnPoolAcquire();
        renderer.sprite = CreateSprite(100, 100);
        obstacle.ConfigureSpriteAnimation(null, 0.2f);
        obstacle.Configure(1f, 0.6f, Mathf.PI, ObstacleKind.Spike);
        Invoke(obstacle, "AdvanceSpriteAnimation", 1f);
        Assert.AreEqual(0, obstacle.AnimationFrameCount);
        Assert.AreEqual(ObstacleKind.Spike, obstacle.Kind);
        Assert.IsTrue(go.GetComponent<CircleCollider2D>().enabled);
        Assert.IsFalse(go.GetComponent<CapsuleCollider2D>().enabled);
    }

    [Test]
    public void GameOverFreezesVisibleSpikeUntilReturningToLobby()
    {
        GameplayRandom.ResetSession(20260727);
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        SetField(spawner, "obstacleSprite", CreateSprite(100, 100));

        Invoke(spawner, "Spawn", 30f);

        var active = (IList)GetField(spawner, "active");
        Assert.AreEqual(1, active.Count);
        var obstacle = (Obstacle)active[0];
        var renderer = obstacle.GetComponent<SpriteRenderer>();

        Invoke(spawner, "OnStateChanged", GameState.Playing, GameState.GameOver);

        Assert.AreEqual(1, active.Count,
            "사망 순간의 먹가시는 게임오버 장면에 남아 충돌 위치를 보여야 합니다.");
        Assert.IsTrue(obstacle.gameObject.activeSelf);
        Assert.IsTrue(renderer.enabled);

        Invoke(spawner, "OnStateChanged", GameState.GameOver, GameState.Lobby);

        Assert.AreEqual(0, active.Count);
        Assert.IsFalse(obstacle.gameObject.activeSelf);
        Assert.IsFalse(renderer.enabled);
    }

    [Test]
    public void MovingObstacleScheduleStartsAtCourseHeight30()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        Invoke(spawner, "EnsureSessionSchedule");

        Assert.AreEqual(30f, (float)GetField(spawner, "nextSpawnHeight"));
    }

    [Test]
    public void DebugTeleportKeepsFirstDragonGuarantee()
    {
        var spawnerObject = Track(new GameObject("ObstacleSpawner"));
        var spawner = spawnerObject.AddComponent<ObstacleSpawner>();
        SetField(spawner, "firstDragonPending", false);

        Invoke(spawner, "OnWorldHeightTeleported", 120);

        Assert.IsTrue((bool)GetField(spawner, "firstDragonPending"));
    }

    Sprite CreateSprite(int width, int height)
    {
        var texture = Track(new Texture2D(width, height));
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f);
        cleanup.Add(sprite);
        return sprite;
    }

    Sprite[] CreateDragonFrames(int width, int height)
    {
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = CreateSprite(width, height);
            frames[i].name = $"child_ink_dragon_frame_{i:00}";
        }
        return frames;
    }

    static float Min(float[] values)
    {
        float result = float.PositiveInfinity;
        for (int i = 0; i < values.Length; i++)
            result = Mathf.Min(result, values[i]);
        return result;
    }

    static float Max(float[] values)
    {
        float result = float.NegativeInfinity;
        for (int i = 0; i < values.Length; i++)
            result = Mathf.Max(result, values[i]);
        return result;
    }

    T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    static void SetAutoProperty(object target, string propertyName, object value)
    {
        target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static object GetField(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);
    }
}
