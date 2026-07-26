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
    public void RestPlatformIsSolidAndUsesSafePlatformColor()
    {
        var platform = Track(PlatformCollider.SpawnRestPlatform(CreatePoints()));

        Assert.IsTrue(platform.IsRestPlatform);
        Assert.IsFalse(platform.IsWindCurrentPlatform);
        AssertSolidTintablePlatform(platform, InkPalette.SafePlatform);
    }

    [Test]
    public void WindPlatformIsSolidAndUsesWindPlatformColor()
    {
        var platform = Track(PlatformCollider.SpawnWindCurrentPlatform(CreatePoints()));

        Assert.IsFalse(platform.IsRestPlatform);
        Assert.IsTrue(platform.IsWindCurrentPlatform);
        AssertSolidTintablePlatform(platform, InkPalette.WindPlatform);
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

    static void AssertSolidTintablePlatform(PlatformCollider platform, Color expectedColor)
    {
        Assert.IsNull(platform.GetComponentInChildren<PlatformEffector2D>(true),
            "양방향 특수 발판에는 PlatformEffector2D가 없어야 합니다.");

        var edge = platform.GetComponent<EdgeCollider2D>();
        Assert.IsNotNull(edge);
        Assert.IsFalse(edge.usedByEffector,
            "양방향 특수 발판의 EdgeCollider2D는 Effector를 사용하면 안 됩니다.");

        var line = platform.Line;
        Assert.IsNotNull(line);
        Assert.AreSame(FallbackInkStyle.SharedTintableBrushMaterial, line.sharedMaterial,
            "특수 발판 안쪽 선은 효과색을 보존하는 전용 붓 재질을 사용해야 합니다.");
        Assert.AreNotSame(FallbackInkStyle.SharedInkMaterial, line.sharedMaterial,
            "검정 먹선 재질을 사용하면 안전·풍맥 효과색이 검게 곱해집니다.");

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
