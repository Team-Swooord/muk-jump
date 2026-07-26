using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core.Pooling;
using MukJump.Items;

public sealed class InkDropJumpVfxPoolTests
{
    GameObject owner;
    GameObject secondOwner;
    GameObject sharedPoolRoot;

    [TearDown]
    public void TearDown()
    {
        if (sharedPoolRoot != null)
            Object.DestroyImmediate(sharedPoolRoot);
        if (secondOwner != null)
            Object.DestroyImmediate(secondOwner);
        if (owner != null)
            Object.DestroyImmediate(owner);
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
}
