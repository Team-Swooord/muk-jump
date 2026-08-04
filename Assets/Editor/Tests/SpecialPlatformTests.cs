using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;

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
    }

    [Test]
    public void WindPlatformIsOneWayAndUsesWindPlatformColor()
    {
        var platform = Track(PlatformCollider.SpawnWindCurrentPlatform(CreatePoints()));

        Assert.IsTrue(platform.IsWindCurrentPlatform);
        AssertOneWayTintablePlatform(platform, InkPalette.WindPlatform);
    }

    [Test]
    public void DrawnPlatformRemainsBidirectionalWithoutEffector()
    {
        var platform = Track(PlatformCollider.Spawn(CreatePoints()));

        Assert.IsFalse(platform.IsWindCurrentPlatform);
        Assert.IsNull(platform.GetComponent<PlatformEffector2D>(),
            "일반 드로잉 발판에는 단방향 Effector를 적용하면 안 됩니다.");

        var edge = platform.GetComponent<EdgeCollider2D>();
        Assert.IsNotNull(edge);
        Assert.IsFalse(edge.usedByEffector,
            "일반 드로잉 발판은 양방향 충돌과 대각선 매달리기를 유지해야 합니다.");
    }

    [Test]
    public void DrawnPlatformCollisionBudgetNeverExceedsFour()
    {
        var platforms = new List<PlatformCollider>();
        for (int i = 0; i < 8; i++)
            platforms.Add(Track(PlatformCollider.Spawn(CreatePoints())));

        int enabledColliderCount = 0;
        for (int i = 0; i < platforms.Count; i++)
        {
            var edge = platforms[i].GetComponent<EdgeCollider2D>();
            Assert.IsNotNull(edge);
            if (edge.enabled) enabledColliderCount++;

            Assert.That(edge.enabled, Is.EqualTo(i >= 4),
                $"최근 네 발판만 충돌 가능해야 합니다. index={i}");
        }

        Assert.That(enabledColliderCount, Is.EqualTo(4));
        Assert.IsTrue(platforms[0].Line.enabled,
            "예산에서 밀린 발판은 먹이 마르는 비주얼을 위해 즉시 삭제하지 않습니다.");
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

        float next = (float)GetField(spawner, "nextWindHeight");
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

        float next = (float)GetField(spawner, "nextWindHeight");
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

        var spawned = (List<PlatformCollider>)GetField(spawner, "spawned");
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
        Assert.That((float)GetField(spawner, "nextWindHeight"), Is.GreaterThan(250f));
    }

    PlatformCollider Track(PlatformCollider platform)
    {
        cleanup.Add(platform.gameObject);
        return platform;
    }

    static List<Vector2> CreatePoints()
    {
        return new List<Vector2>
        {
            new(-2f, 0f),
            new(0f, 0.12f),
            new(2f, 0f),
        };
    }

    static void AssertOneWayTintablePlatform(PlatformCollider platform, Color expectedColor)
    {
        var effectors = platform.GetComponents<PlatformEffector2D>();
        Assert.That(effectors, Has.Length.EqualTo(1),
            "특수 발판에는 중복 없이 하나의 PlatformEffector2D만 있어야 합니다.");
        var effector = effectors[0];
        Assert.IsTrue(effector.enabled);
        Assert.IsTrue(effector.useOneWay,
            "특수 발판은 아래에서 통과하는 단방향 충돌이어야 합니다.");
        Assert.That(effector.surfaceArc, Is.EqualTo(165f).Within(0.001f));
        Assert.IsFalse(effector.useColliderMask);

        var edge = platform.GetComponent<EdgeCollider2D>();
        Assert.IsNotNull(edge);
        Assert.IsTrue(edge.usedByEffector,
            "특수 발판의 EdgeCollider2D가 단방향 Effector를 사용해야 합니다.");

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

    static void InvokeSpawnWindPlatform(
        RestPlatformSpawner spawner,
        Vector2 center,
        string suffix)
    {
        spawner.GetType().GetMethod(
                "SpawnWindPlatform", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(spawner, new object[] { center, 3.4f, suffix });
    }

    static object GetField(object target, string fieldName)
    {
        return target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(target);
    }
}
