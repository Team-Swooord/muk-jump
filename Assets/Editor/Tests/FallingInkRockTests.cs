using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Drawing;
using MukJump.EditorTools;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;
using UnityEngine.UI;

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
        Assert.AreEqual(InkPalette.ObstaclePaperRed,
            rock.GetComponent<SpriteRenderer>().color);
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
    public void PoolReuseResetsWarningPhysicsAndTimers()
    {
        var pool = new ComponentPool<FallingInkRock>(() => CreateRock(0.8f), 1);
        var first = pool.Acquire();
        SetField(first, "warningElapsed", 0.6f);
        SetField(first, "lifetimeElapsed", 3f);
        first.GetComponent<CircleCollider2D>().enabled = true;
        first.GetComponent<Rigidbody2D>().simulated = true;
        first.GetComponent<SpriteRenderer>().enabled = false;

        Assert.IsTrue(pool.Release(first));
        var reused = pool.Acquire();

        Assert.AreSame(first, reused);
        Assert.AreEqual(FallingInkRockState.Warning, reused.State);
        Assert.IsFalse(reused.GetComponent<CircleCollider2D>().enabled);
        Assert.IsFalse(reused.GetComponent<Rigidbody2D>().simulated);
        Assert.IsTrue(reused.GetComponent<SpriteRenderer>().enabled);
        Assert.AreEqual(InkPalette.ObstaclePaperRed,
            reused.GetComponent<SpriteRenderer>().color);
        Assert.AreEqual(0f, (float)GetField(reused, "warningElapsed"));
        Assert.AreEqual(0f, (float)GetField(reused, "lifetimeElapsed"));
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
    public void VisibilityTintsBodyAndDisablesLegacyDecorations()
    {
        var root = Track(new GameObject("VisibleObstacle"));
        var body = root.AddComponent<SpriteRenderer>();
        body.sortingOrder = 6;
        body.color = new Color(1f, 1f, 1f, 0.47f);

        var haloObject = Track(new GameObject("PaperHalo"));
        haloObject.transform.SetParent(root.transform, false);
        var paperHalo = haloObject.AddComponent<SpriteRenderer>();

        var ringObject = Track(new GameObject("DangerRing"));
        ringObject.transform.SetParent(root.transform, false);
        var dangerRing = ringObject.AddComponent<LineRenderer>();

        var view = root.AddComponent<ObstacleVisibilityView>();

        view.Configure();

        Assert.That(body.color.r, Is.EqualTo(InkPalette.ObstaclePaperRed.r).Within(0.001f));
        Assert.That(body.color.g, Is.EqualTo(InkPalette.ObstaclePaperRed.g).Within(0.001f));
        Assert.That(body.color.b, Is.EqualTo(InkPalette.ObstaclePaperRed.b).Within(0.001f));
        Assert.That(body.color.a, Is.EqualTo(0.47f).Within(0.001f));
        Assert.IsNotNull(body.sharedMaterial);
        Assert.AreEqual("MukJump/ObstaclePaperRed", body.sharedMaterial.shader.name);
        Assert.IsFalse(paperHalo.enabled);
        Assert.IsFalse(dangerRing.enabled);

        view.SetVisible(false);
        Assert.IsFalse(dangerRing.enabled);
        view.SetVisible(true);
        Assert.IsFalse(dangerRing.enabled);
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
        Assert.IsNotNull(itemSerialized.FindProperty("goldenBrushSprite").objectReferenceValue);
        Assert.IsNotNull(itemSerialized.FindProperty("inkShieldSprite").objectReferenceValue);

        var inkDropVfx = Object.FindFirstObjectByType<InkDropJumpVfx>();
        Assert.IsNotNull(inkDropVfx);
        var vfxSerialized = new SerializedObject(inkDropVfx);
        Assert.IsNotNull(vfxSerialized.FindProperty("groundBlob").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("inkSplash").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("shockRing").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("verticalBrush").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("immediateClip").objectReferenceValue);
        Assert.IsNotNull(Object.FindFirstObjectByType<VfxAudioManager>());

        var lobby = Object.FindFirstObjectByType<LobbyView>();
        Assert.IsNotNull(lobby);
        var lobbySerialized = new SerializedObject(lobby);
        var lobbyBest = lobbySerialized.FindProperty("bestText").objectReferenceValue as Text;
        Assert.IsNotNull(lobbyBest);
        Assert.AreEqual(50, lobbyBest.fontSize);
        Assert.AreEqual(InkPalette.Paper, lobbyBest.color);
        Assert.AreEqual(2, lobby.GetComponentsInChildren<RawImage>(true).Length - 2);

        var gameplayHud = Object.FindFirstObjectByType<GameplayHudView>();
        Assert.IsNotNull(gameplayHud);
        var hudSerialized = new SerializedObject(gameplayHud);
        var topHud = hudSerialized.FindProperty("topHudRoot").objectReferenceValue
            as RectTransform;
        Assert.IsNotNull(topHud);
        Assert.AreEqual("TopHudRoot", topHud.name);
        Assert.That(topHud.sizeDelta.x, Is.EqualTo(900f).Within(0.01f));
        Assert.That(topHud.sizeDelta.y, Is.EqualTo(148f).Within(0.01f));
        Assert.IsNull(hudSerialized.FindProperty("heightCaption").objectReferenceValue);
        Assert.IsNull(hudSerialized.FindProperty("bestCaption").objectReferenceValue);
        Assert.IsNull(topHud.Find("HeightCaption"));
        Assert.IsNull(topHud.Find("BestCaption"));
        var heightText = hudSerialized.FindProperty("heightText").objectReferenceValue as Text;
        var bestText = hudSerialized.FindProperty("bestText").objectReferenceValue as Text;
        Assert.IsNotNull(heightText);
        Assert.IsNotNull(bestText);
        Assert.AreEqual("고도 0m", heightText.text);
        Assert.AreEqual("최고 0m", bestText.text);
        Assert.AreEqual(60, heightText.fontSize);
        Assert.AreEqual(50, bestText.fontSize);
        Assert.AreEqual(FontStyle.Bold, heightText.fontStyle);
        Assert.AreEqual(FontStyle.Bold, bestText.fontStyle);
        Assert.GreaterOrEqual(heightText.rectTransform.sizeDelta.x, 315f);
        Assert.GreaterOrEqual(bestText.rectTransform.sizeDelta.x, 235f);
        Assert.That(heightText.rectTransform.anchorMin.y, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(bestText.rectTransform.anchorMin.y, Is.EqualTo(0.5f).Within(0.001f));
        Assert.IsTrue(heightText.resizeTextForBestFit);
        Assert.IsTrue(bestText.resizeTextForBestFit);
        Assert.IsNotNull(heightText.GetComponent<Outline>());
        Assert.IsNotNull(bestText.GetComponent<Outline>());

        var windIndicator = hudSerialized.FindProperty("windIndicator").objectReferenceValue
            as WindIndicatorView;
        var newBestIndicator = hudSerialized.FindProperty("newBestIndicator").objectReferenceValue
            as NewBestIndicatorView;
        Assert.IsNotNull(windIndicator);
        Assert.IsNotNull(newBestIndicator);
        var windSerialized = new SerializedObject(windIndicator);
        var windState = windSerialized.FindProperty("stateText").objectReferenceValue as Text;
        Assert.IsNotNull(windState);
        Assert.AreEqual(34, windState.fontSize);
        Assert.AreEqual(FontStyle.Bold, windState.fontStyle);
        Assert.IsNotNull(windState.GetComponent<Outline>());
        Assert.AreSame(topHud, windIndicator.transform.parent);
        Assert.AreSame(topHud, newBestIndicator.transform.parent);
        Assert.IsNull(windIndicator.transform.Find("WindStrengthStroke1"));
        Assert.That(((RectTransform)newBestIndicator.transform).sizeDelta.x,
            Is.LessThanOrEqualTo(50f));
        Assert.IsNotNull(Object.FindFirstObjectByType<PauseMenuView>());

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
        var spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateSprite();
        go.AddComponent<ObstacleVisibilityView>().Configure();
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
