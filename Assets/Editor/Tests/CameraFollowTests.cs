using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraFollowTests
{
    [Test]
    public void BalancedGuideFollowsBeforeTheOldSeventyFivePercentBand()
    {
        float targetY = CameraFollow.ResolveHighestFollowTargetY(
            highestTargetY: 0f,
            trackedY: 4.18f,
            baseHalfHeight: 9.6f,
            followViewportY: CameraFollow.BalancedFollowViewportY);

        Assert.AreEqual(3.22f, targetY, 0.001f,
            "55% 균형선은 기존 75% 데드존보다 실제 상승을 빠르게 따라가야 합니다.");
    }

    [Test]
    public void BalancedGuideKeepsTrackedPlayerAtFiftyFivePercent()
    {
        const float trackedY = 10f;
        const float halfHeight = 9.6f;
        float targetY = CameraFollow.ResolveHighestFollowTargetY(
            highestTargetY: 0f,
            trackedY: trackedY,
            baseHalfHeight: halfHeight,
            followViewportY: CameraFollow.BalancedFollowViewportY);
        float resolvedViewportY =
            ((trackedY - targetY) / halfHeight + 1f) * 0.5f;

        Assert.AreEqual(
            CameraFollow.BalancedFollowViewportY,
            resolvedViewportY,
            0.001f);
    }

    [Test]
    public void FallingAndRepeatedLowJumpsNeverCreepCameraUpward()
    {
        float targetY = 0f;
        for (int i = 0; i < 20; i++)
        {
            targetY = CameraFollow.ResolveHighestFollowTargetY(
                highestTargetY: targetY,
                trackedY: i % 2 == 0 ? 5.8f : -1f,
                baseHalfHeight: 9.6f,
                followViewportY: CameraFollow.BalancedFollowViewportY);
        }

        Assert.AreEqual(4.84f, targetY, 0.001f,
            "같은 낮은 점프를 반복해도 카메라 위치가 누적되면 안 됩니다.");
    }

    [Test]
    public void LegacySeventyFivePercentSceneMigratesToBalancedGuide()
    {
        var cameraObject = new GameObject("LegacyCamera");
        try
        {
            cameraObject.AddComponent<Camera>();
            var follow = cameraObject.AddComponent<CameraFollow>();
            SetField(follow, "upperFollowViewportY", 0.75f);
            SetField(follow, "followTuningVersion", 0);

            Invoke(follow, "OnEnable");

            Assert.AreEqual(
                CameraFollow.BalancedFollowViewportY,
                GetField<float>(follow, "upperFollowViewportY"),
                0.001f);
            Assert.AreEqual(
                CameraFollow.CurrentFollowTuningVersion,
                GetField<int>(follow, "followTuningVersion"));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void HardCeilingKeepsFastBoostInsideViewport()
    {
        const float halfHeight = 9.6f;
        const float ceilingViewportY = 0.9f;
        const float trackedY = 50f;

        float cameraY = CameraFollow.ResolveHardCeilingCameraY(
            currentCameraY: 0f,
            trackedY: trackedY,
            baseHalfHeight: halfHeight,
            ceilingViewportY: ceilingViewportY);
        float resolvedViewportY =
            ((trackedY - cameraY) / halfHeight + 1f) * 0.5f;

        Assert.AreEqual(ceilingViewportY, resolvedViewportY, 0.001f);
    }

    [Test]
    public void DeathReframePlacesLowerSurvivorsBackInsideTheViewOnce()
    {
        const float currentCameraY = 50f;
        const float survivorClusterY = 12f;
        const float halfHeight = 9.6f;
        const float viewportY = 0.46f;

        float cameraY = CameraFollow.ResolveSurvivorReframeCameraY(
            currentCameraY,
            survivorClusterY,
            halfHeight,
            viewportY);
        float resolvedViewportY =
            ((survivorClusterY - cameraY) / halfHeight + 1f) * 0.5f;

        Assert.AreEqual(viewportY, resolvedViewportY, 0.001f);
        Assert.Less(cameraY, currentCameraY);
    }

    [Test]
    public void OnlyADeadUpperLeaderRequestsSurvivorReframe()
    {
        Assert.That(
            CameraFollow.ShouldReframeAfterDeath(
                dyingPlayerY: 18f,
                survivingUpperGuardY: 12f),
            Is.True);
        Assert.That(
            CameraFollow.ShouldReframeAfterDeath(
                dyingPlayerY: 8f,
                survivingUpperGuardY: 12f),
            Is.False,
            "낮은 분신 사망이 카메라와 추락 사망선을 반복해서 내리면 안 됩니다.");
    }

    static void Invoke(object target, string methodName)
    {
        target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }

    static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static T GetField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(target);
    }
}
