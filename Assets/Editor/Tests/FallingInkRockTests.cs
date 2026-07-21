using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Drawing;
using MukJump.EditorTools;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;

public class FallingInkRockTests
{
    readonly List<Object> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        }
        cleanup.Clear();
    }

    [Test]
    public void WarningDisablesCollisionThenEnablesFalling()
    {
        var rock = CreateRock(0.8f);
        var collider = rock.GetComponent<CircleCollider2D>();
        var body = rock.GetComponent<Rigidbody2D>();

        Assert.AreEqual(FallingInkRockState.Warning, rock.State);
        Assert.IsFalse(collider.enabled);
        Assert.IsFalse(body.simulated);

        SetField(rock, "warningElapsed", 0.8f);
        Invoke(rock, "UpdateWarning");

        Assert.AreEqual(FallingInkRockState.Falling, rock.State);
        Assert.IsTrue(collider.enabled);
        Assert.IsTrue(body.simulated);
    }

    [Test]
    public void ResolveStateCanOnlyBeEnteredOnce()
    {
        var rock = CreateRock(0.8f);

        Assert.IsTrue((bool)Invoke(rock, "TryEnterResolvedState"));
        Assert.IsFalse((bool)Invoke(rock, "TryEnterResolvedState"));
        Assert.AreEqual(FallingInkRockState.Resolved, rock.State);
        Assert.IsFalse(rock.GetComponent<CircleCollider2D>().enabled);
        Assert.IsFalse(rock.GetComponent<Rigidbody2D>().simulated);
    }

    [Test]
    public void ThinPlatformIsCastableAndRemovalIsIdempotent()
    {
        var platform = PlatformCollider.Spawn(new List<Vector2>
        {
            new(-2f, 0f),
            new(2f, 0f),
        });
        cleanup.Add(platform.gameObject);
        Physics2D.SyncTransforms();

        RaycastHit2D hit = Physics2D.CircleCast(new Vector2(0f, 1f), 0.4f,
            Vector2.down, 2f, LayerMask.GetMask("Platform"));
        Assert.AreSame(platform.GetComponent<EdgeCollider2D>(), hit.collider);

        Assert.IsTrue((bool)Invoke(platform, "TryBeginHazardRemoval"));
        Assert.IsFalse((bool)Invoke(platform, "TryBeginHazardRemoval"));
        Assert.IsFalse(platform.GetComponent<EdgeCollider2D>().enabled);
    }

    [Test]
    public void SpawnerChoosesInsideViewportAwayFromPlayer()
    {
        var playerObject = Track(new GameObject("TestPlayer"));
        playerObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        playerObject.AddComponent<CircleCollider2D>();
        var player = playerObject.AddComponent<PlayerController>();
        playerObject.transform.position = Vector3.zero;

        var spawnerObject = Track(new GameObject("TestSpawner"));
        var spawner = spawnerObject.AddComponent<FallingInkRockSpawner>();
        SetField(spawner, "player", player);
        SetField(spawner, "playerHorizontalClearance", 0.7f);
        SetField(spawner, "xSelectionAttempts", 5);

        for (int i = 0; i < 20; i++)
        {
            float x = (float)Invoke(spawner, "ChooseSafestX", -4f, 4f);
            Assert.That(x, Is.InRange(-4f, 4f));
            Assert.GreaterOrEqual(Mathf.Abs(x), 0.7f);
        }
    }

    [Test]
    public void SceneBuilderCreatesSingleConfiguredSpawner()
    {
        MukJumpSceneBuilder.Build();

        var spawners = Object.FindObjectsByType<FallingInkRockSpawner>(FindObjectsSortMode.None);
        Assert.AreEqual(1, spawners.Length);
        Assert.AreEqual("Obstacles", spawners[0].gameObject.name);

        var serialized = new SerializedObject(spawners[0]);
        Assert.IsNotNull(serialized.FindProperty("fallingInkRockSprite").objectReferenceValue);
        Assert.IsNotNull(serialized.FindProperty("worldCamera").objectReferenceValue);
        Assert.IsNotNull(serialized.FindProperty("player").objectReferenceValue);
        Assert.AreNotEqual(0, serialized.FindProperty("collisionMask").intValue);

        var itemSpawner = Object.FindFirstObjectByType<ItemSpawner>();
        Assert.IsNotNull(itemSpawner);
        var itemSerialized = new SerializedObject(itemSpawner);
        Assert.IsNotNull(itemSerialized.FindProperty("inkDropSprite").objectReferenceValue);

        var importer = (TextureImporter)AssetImporter.GetAtPath(
            "Assets/Art/Character/Obstacles/anermy_02.png");
        Assert.IsNotNull(importer);
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(700f, importer.spritePixelsPerUnit);
        Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
    }

    FallingInkRock CreateRock(float warningDuration)
    {
        var go = Track(new GameObject("TestFallingInkRock"));
        go.AddComponent<SpriteRenderer>().sprite = CreateSprite();
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.4f;
        var rock = go.AddComponent<FallingInkRock>();
        // EditMode에서는 Awake가 자동 호출되지 않으므로 런타임 초기화 순서를 재현한다.
        Invoke(rock, "Awake");
        rock.Initialize(null, null, LayerMask.GetMask("Default", "Platform"),
            warningDuration, 4f, 9f, 8f, 8f);
        return rock;
    }

    Sprite CreateSprite()
    {
        var texture = Track(new Texture2D(16, 16));
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f), 16f);
        cleanup.Add(sprite);
        return sprite;
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

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);
    }
}
