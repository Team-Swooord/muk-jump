using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

public sealed class ItemEffectCloneTests
{
    GameObject player;
    GameObject clone;
    GameObject detachedVisual;

    [TearDown]
    public void TearDown()
    {
        if (clone != null) Object.DestroyImmediate(clone);
        if (detachedVisual != null) Object.DestroyImmediate(detachedVisual);
        if (player != null) Object.DestroyImmediate(player);
    }

    [Test]
    public void RuntimeEffectChildrenAreExcludedFromPlayerClone()
    {
        player = new GameObject("PlayerWithCachedEffects");
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<PlayerController>();
        var view = player.AddComponent<ItemEffectView>();

        detachedVisual = new GameObject("ShieldMote01");
        detachedVisual.transform.SetParent(player.transform, false);
        var gameplayChild = new GameObject("Eyes");
        gameplayChild.transform.SetParent(player.transform, false);
        var lifecycle = (IRuntimeCloneLifecycle)view;

        lifecycle.PrepareForRuntimeClone();
        Assert.IsNull(detachedVisual.transform.parent);
        Assert.AreSame(player.transform, gameplayChild.transform.parent);

        clone = Object.Instantiate(player);

        Assert.IsNull(clone.transform.Find("ShieldMote01"),
            "과거에 생성된 비활성 아이템 효과가 먹분신마다 복제되면 안 됩니다.");
        Assert.IsNotNull(clone.transform.Find("Eyes"));

        lifecycle.RestoreAfterRuntimeClone();
        Assert.AreSame(player.transform, detachedVisual.transform.parent);
    }

    [Test]
    public void RuntimeCloneUsesReducedShieldParticleBudget()
    {
        player = new GameObject("ShieldedRuntimeClone");
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 2.2f;
        var controller = player.AddComponent<PlayerController>();
        Invoke(controller, "Awake");
        controller.ConfigureAsClone(2.2f);
        var view = player.AddComponent<ItemEffectView>();
        Invoke(view, "Awake");

        Invoke(view, "EnsureShieldVisuals");

        var motes = (SpriteRenderer[])GetField(view, "shieldMotes");
        var shards = (SpriteRenderer[])GetField(view, "shieldShards");
        Assert.AreEqual(4, motes.Length);
        Assert.AreEqual(6, shards.Length);
        Assert.IsTrue(controller.IsRuntimeClone);
    }

    [Test]
    public void CloneArrivalVisualIsSingleReusableChildAndCloneStartsClean()
    {
        player = new GameObject("PlayerWithCloneArrival");
        var playerRenderer = player.AddComponent<SpriteRenderer>();
        playerRenderer.enabled = false;
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CircleCollider2D>();
        player.AddComponent<PlayerController>();
        var view = player.AddComponent<InkCloneArrivalView>();
        Invoke(view, "Awake");

        var visual = player.transform.Find("InkCloneArrivalVisual");
        Assert.IsNotNull(visual);
        var arrivalRenderer = visual.GetComponent<SpriteRenderer>();
        arrivalRenderer.enabled = true;

        var lifecycle = (IRuntimeCloneLifecycle)view;
        lifecycle.PrepareForRuntimeClone();
        Assert.IsTrue(playerRenderer.enabled);
        Assert.IsFalse(arrivalRenderer.enabled);

        clone = Object.Instantiate(player);
        var cloneVisual = clone.transform.Find("InkCloneArrivalVisual");
        Assert.IsNotNull(cloneVisual);
        Assert.AreEqual(1, CountNamedChildren(
            clone.transform, "InkCloneArrivalVisual"));
        Assert.IsTrue(clone.GetComponent<SpriteRenderer>().enabled);
        Assert.IsFalse(cloneVisual.GetComponent<SpriteRenderer>().enabled);

        lifecycle.RestoreAfterRuntimeClone();
        Assert.IsFalse(playerRenderer.enabled);
        Assert.IsTrue(arrivalRenderer.enabled);

        Invoke(view, "EnsureVisuals");
        Assert.AreEqual(1, CountNamedChildren(
            player.transform, "InkCloneArrivalVisual"),
            "반복 획득에도 캐릭터마다 보조 렌더러는 하나만 유지해야 합니다.");
    }

    static int CountNamedChildren(Transform root, string childName)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
            if (root.GetChild(i).name == childName)
                count++;
        return count;
    }

    static void Invoke(object target, string methodName, params object[] arguments)
    {
        target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);
    }

    static object GetField(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }
}
