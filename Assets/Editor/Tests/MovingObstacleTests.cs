using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Obstacles;

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
            "MukJump/Obstacles/child_ink_dragon_4frame");
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
