using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Items;

public sealed class InkDropJumpVfxPoolTests
{
    GameObject owner;
    GameObject secondOwner;
    GameObject sharedPoolRoot;
    GameObject inactivePoolRoot;

    [TearDown]
    public void TearDown()
    {
        if (sharedPoolRoot != null)
            Object.DestroyImmediate(sharedPoolRoot);
        if (inactivePoolRoot != null)
            Object.DestroyImmediate(inactivePoolRoot);
        if (secondOwner != null)
            Object.DestroyImmediate(secondOwner);
        if (owner != null)
            Object.DestroyImmediate(owner);
        VfxQualityRuntime.SetTier(
            VfxQualityTier.Medium,
            VfxQualityChangeReason.DebugOverride);
    }

    [Test]
    public void AcquireReleaseReacquireKeepsCompositeChildCount()
    {
        const int sprayCount = 4;
        const int residualDropCount = 3;
        const int expectedChildCount = 7 + sprayCount + residualDropCount + 3;

        owner = new GameObject("InkDropJumpVfxPoolTestOwner");
        var pool = new ComponentPool<InkDropJumpVfxInstance>(() =>
        {
            var go = new GameObject("PooledInkDropJumpVfxTest");
            go.SetActive(false);
            go.transform.SetParent(owner.transform, false);
            var instance = go.AddComponent<InkDropJumpVfxInstance>();
            instance.Initialize(owner.transform, default, sprayCount, residualDropCount);
            return instance;
        }, 1);

        var first = pool.Acquire();
        Assert.AreEqual(expectedChildCount, first.BuiltChildCount);
        Assert.AreEqual(expectedChildCount,
            first.GetComponentsInChildren<SpriteRenderer>(true).Length);

        Assert.IsTrue(pool.Release(first));
        var reacquired = pool.Acquire();

        Assert.AreSame(first, reacquired);
        Assert.AreEqual(expectedChildCount, reacquired.BuiltChildCount);
        Assert.AreEqual(expectedChildCount,
            reacquired.GetComponentsInChildren<SpriteRenderer>(true).Length);
        Assert.IsTrue(pool.Release(reacquired));
    }

    [Test]
    public void SharedPoolIsSingleAndStaysOutsidePlayerHierarchies()
    {
        owner = new GameObject("InkDropJumpVfxPlayerA");
        secondOwner = new GameObject("InkDropJumpVfxPlayerB");

        var first = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = first.gameObject;
        var second = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);

        Assert.AreSame(first, second,
            "먹분신마다 별도 합성 풀을 만들면 전체 오브젝트 상한이 분신 수만큼 늘어납니다.");
        Assert.IsFalse(first.transform.IsChildOf(owner.transform));
        Assert.IsFalse(first.transform.IsChildOf(secondOwner.transform));
    }

    [Test]
    public void HotReloadRebuildsLostManagedPool()
    {
        var service = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = service.gameObject;
        var poolField = typeof(InkDropJumpVfxPool).GetField("pool",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(poolField);
        poolField.SetValue(service, null);

        var reconfigured = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);

        Assert.AreSame(service, reconfigured);
        Assert.IsNotNull(poolField.GetValue(service),
            "Play 중 스크립트 리로드로 managed 풀이 사라지면 즉시 다시 구성해야 합니다.");
    }

    [Test]
    public void InactiveServiceIsNotReusedForPlayback()
    {
        inactivePoolRoot = new GameObject("InactiveInkDropJumpVfxPool");
        var inactiveService = inactivePoolRoot.AddComponent<InkDropJumpVfxPool>();
        inactivePoolRoot.SetActive(false);

        var service = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = service.gameObject;

        Assert.AreNotSame(inactiveService, service);
        Assert.That(service.isActiveAndEnabled, Is.True);
        Assert.That(service.gameObject.activeInHierarchy, Is.True);
    }

    [Test]
    public void DisabledServiceOnActiveObjectIsNotReusedForPlayback()
    {
        inactivePoolRoot = new GameObject("DisabledInkDropJumpVfxPool");
        var disabledService = inactivePoolRoot.AddComponent<InkDropJumpVfxPool>();
        disabledService.enabled = false;

        var service = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = service.gameObject;

        Assert.AreNotSame(disabledService, service);
        Assert.That(service.isActiveAndEnabled, Is.True);
    }

    [Test]
    public void QualityDowngradeImmediatelyReclaimsOldestCompositeUntilNewLimit()
    {
        VfxQualityRuntime.SetTier(
            VfxQualityTier.High,
            VfxQualityChangeReason.DebugOverride);
        owner = new GameObject("InkDropJumpVfxQualityOwner");
        var ownerVfx = owner.AddComponent<InkDropJumpVfx>();
        var renderer = owner.GetComponent<SpriteRenderer>();
        var service = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = service.gameObject;

        for (int i = 0; i < 3; i++)
            service.Play(
                ownerVfx,
                owner.transform,
                renderer,
                Vector3.zero,
                1f,
                2f);

        Assert.That(service.ActiveCount, Is.EqualTo(3));

        VfxQualityRuntime.SetTier(
            VfxQualityTier.Low,
            VfxQualityChangeReason.DebugOverride);

        Assert.That(service.ActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void HighQualityPrewarmBuildsAllThreeCompositesOnce()
    {
        VfxQualityRuntime.SetTier(
            VfxQualityTier.High,
            VfxQualityChangeReason.DebugOverride);
        var service = InkDropJumpVfxPool.GetOrCreate(default, 4, 3);
        sharedPoolRoot = service.gameObject;

        service.PrewarmForCurrentTier();
        int firstCount = service.GetComponentsInChildren<InkDropJumpVfxInstance>(true).Length;
        int firstRendererCount =
            service.GetComponentsInChildren<SpriteRenderer>(true).Length;
        service.PrewarmForCurrentTier();

        Assert.That(firstCount, Is.EqualTo(3));
        Assert.That(
            service.GetComponentsInChildren<InkDropJumpVfxInstance>(true).Length,
            Is.EqualTo(firstCount));
        Assert.That(
            service.GetComponentsInChildren<SpriteRenderer>(true).Length,
            Is.EqualTo(firstRendererCount));
    }
}
