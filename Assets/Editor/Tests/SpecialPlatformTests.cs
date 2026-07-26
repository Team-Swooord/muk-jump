using System.Collections.Generic;
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
}
