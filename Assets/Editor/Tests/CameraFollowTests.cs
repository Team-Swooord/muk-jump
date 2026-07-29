using MukJump.Core;
using NUnit.Framework;

public sealed class CameraFollowTests
{
    [Test]
    public void StartingJumpInsideDeadZoneKeepsCameraStill()
    {
        float targetY = CameraFollow.ResolveHighestFollowTargetY(
            highestTargetY: 0f,
            trackedY: 4.18f,
            baseHalfHeight: 9.6f,
            followViewportY: 0.75f);

        Assert.AreEqual(0f, targetY, 0.001f,
            "같은 발판의 기본 점프 정점은 카메라를 올리면 안 됩니다.");
    }

    [Test]
    public void CameraMovesOnlyByAmountPastUpperDeadZone()
    {
        float targetY = CameraFollow.ResolveHighestFollowTargetY(
            highestTargetY: 0f,
            trackedY: 5.8f,
            baseHalfHeight: 9.6f,
            followViewportY: 0.75f);

        Assert.AreEqual(1f, targetY, 0.001f);
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
                followViewportY: 0.75f);
        }

        Assert.AreEqual(1f, targetY, 0.001f,
            "같은 낮은 점프를 반복해도 카메라 위치가 누적되면 안 됩니다.");
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
}
