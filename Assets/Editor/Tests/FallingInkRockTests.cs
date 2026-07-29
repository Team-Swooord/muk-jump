using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    Scene builderTestScene;

    [TearDown]
    public void TearDown()
    {
        if (builderTestScene.IsValid() && builderTestScene.isLoaded)
            MukJumpSceneBuilder.CloseTestScene(builderTestScene);
        builderTestScene = default;

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
    public void MovingObstacleUsesKinematicBodyFixedStepAndVisualUpdate()
    {
        var go = Track(new GameObject("TestMovingObstacle"));
        var obstacle = go.AddComponent<Obstacle>();
        Invoke(obstacle, "Awake");

        var body = go.GetComponent<Rigidbody2D>();
        Assert.IsNotNull(body);
        Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType);
        Assert.AreEqual(0f, body.gravityScale);
        Assert.That(body.constraints & RigidbodyConstraints2D.FreezeRotation,
            Is.Not.EqualTo(0));
        Assert.IsNotNull(typeof(Obstacle).GetMethod(
            "FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(typeof(Obstacle).GetMethod(
            "Update", BindingFlags.Instance | BindingFlags.NonPublic));
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
    public void SpawnerRebindsToLivingCloneAfterOriginalPlayerIsGone()
    {
        var managerObject = Track(new GameObject("TestGameManager"));
        var manager = managerObject.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");

        var playerObject = Track(new GameObject("LivingClone"));
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<CircleCollider2D>();
        var livingClone = playerObject.AddComponent<PlayerController>();
        Invoke(livingClone, "Awake");
        manager.RegisterPlayer(livingClone);

        var cameraObject = Track(new GameObject("TestCamera"));
        var camera = cameraObject.AddComponent<Camera>();
        var spawnerObject = Track(new GameObject("TestSpawner"));
        var spawner = spawnerObject.AddComponent<FallingInkRockSpawner>();
        SetField(spawner, "fallingInkRockSprite", CreateSprite());
        SetField(spawner, "worldCamera", camera);
        SetField(spawner, "player", null);

        Assert.IsTrue((bool)Invoke(spawner, "ValidateReferences"));
        Assert.AreSame(livingClone, GetField(spawner, "player"));
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

        view.DisableLegacyDecorations();
        Assert.IsFalse(dangerRing.enabled);
    }

    [Test]
    public void VisibilitySharedMaterialIsReleasedAtSubsystemReset()
    {
        var reset = typeof(ObstacleVisibilityView).GetMethod(
            "ReleaseRuntimeAssets", BindingFlags.Static | BindingFlags.NonPublic);
        var materialField = typeof(ObstacleVisibilityView).GetField(
            "sharedPaperRedMaterial", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(reset);
        Assert.IsNotNull(materialField);
        reset.Invoke(null, null);

        var root = Track(new GameObject("VisibilityResourceOwner"));
        var body = root.AddComponent<SpriteRenderer>();
        var view = root.AddComponent<ObstacleVisibilityView>();
        view.Configure();
        var first = body.sharedMaterial;

        Assert.IsNotNull(first);
        reset.Invoke(null, null);

        Assert.IsNull(materialField.GetValue(null));
        Assert.IsTrue(first == null,
            "공유 런타임 Material은 SubsystemRegistration에서 해제되어야 합니다.");
        view.Configure();
        Assert.IsNotNull(body.sharedMaterial);
        Assert.AreNotSame(first, body.sharedMaterial);
        reset.Invoke(null, null);
    }

    [Test]
    public void SceneBuilderCreatesSingleConfiguredSpawner()
    {
        const string mainScenePath = "Assets/Scenes/Main.unity";
        Scene activeSceneBefore = SceneManager.GetActiveScene();
        Hash128 mainSceneHashBefore = AssetDatabase.GetAssetDependencyHash(mainScenePath);
        UIOrientation orientationBefore = PlayerSettings.defaultInterfaceOrientation;
        EditorBuildSettingsScene[] buildScenesBefore = EditorBuildSettings.scenes;
        string[] hudTexturePaths =
        {
            "Assets/Art/UI/muk_gauge_fill.png",
            "Assets/Art/UI/muk_gauge_track.png",
            "Assets/Art/UI/muk_brush_icon.png",
        };
        var hudTextureHashesBefore = new Hash128[hudTexturePaths.Length];
        for (int i = 0; i < hudTexturePaths.Length; i++)
            hudTextureHashesBefore[i] =
                AssetDatabase.GetAssetDependencyHash(hudTexturePaths[i]);

        builderTestScene = MukJumpSceneBuilder.BuildForTests();

        Assert.AreEqual(string.Empty, builderTestScene.path);
        Assert.AreEqual(activeSceneBefore, SceneManager.GetActiveScene());
        Assert.AreEqual(mainSceneHashBefore, AssetDatabase.GetAssetDependencyHash(mainScenePath));
        Assert.AreEqual(orientationBefore, PlayerSettings.defaultInterfaceOrientation);
        AssertBuildSettingsUnchanged(buildScenesBefore, EditorBuildSettings.scenes);
        for (int i = 0; i < hudTexturePaths.Length; i++)
        {
            Assert.AreEqual(hudTextureHashesBefore[i],
                AssetDatabase.GetAssetDependencyHash(hudTexturePaths[i]),
                $"테스트 빌더가 importer를 변경했습니다: {hudTexturePaths[i]}");
        }

        var spawners = FindAllInScene<FallingInkRockSpawner>(builderTestScene);
        Assert.AreEqual(1, spawners.Length);
        Assert.AreEqual("Obstacles", spawners[0].gameObject.name);

        var serialized = new SerializedObject(spawners[0]);
        Assert.IsNotNull(serialized.FindProperty("fallingInkRockSprite").objectReferenceValue);
        Assert.IsNotNull(serialized.FindProperty("worldCamera").objectReferenceValue);
        Assert.IsNotNull(serialized.FindProperty("player").objectReferenceValue);
        Assert.AreNotEqual(0, serialized.FindProperty("collisionMask").intValue);
        Assert.AreEqual(30f, serialized.FindProperty("startHeight").floatValue);

        var movingSpawner = FindFirstInScene<ObstacleSpawner>(builderTestScene);
        Assert.IsNotNull(movingSpawner);
        var movingSerialized = new SerializedObject(movingSpawner);
        Assert.IsNotNull(movingSerialized.FindProperty("obstacleSprite").objectReferenceValue);
        Assert.IsNotNull(movingSerialized.FindProperty("dragonSprite").objectReferenceValue);
        var dragonFrames = movingSerialized.FindProperty("dragonFrames");
        Assert.AreEqual(4, dragonFrames.arraySize);
        for (int i = 0; i < dragonFrames.arraySize; i++)
        {
            var frame = dragonFrames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
            Assert.IsNotNull(frame);
            Assert.AreEqual($"child_ink_dragon_frame_{i:00}", frame.name);
        }
        Assert.AreSame(dragonFrames.GetArrayElementAtIndex(0).objectReferenceValue,
            movingSerialized.FindProperty("dragonSprite").objectReferenceValue);
        Assert.AreEqual(30f, movingSerialized.FindProperty("firstSpawnHeight").floatValue);

        var itemSpawner = FindFirstInScene<ItemSpawner>(builderTestScene);
        Assert.IsNotNull(itemSpawner);
        var itemSerialized = new SerializedObject(itemSpawner);
        Assert.IsNotNull(itemSerialized.FindProperty("inkDropSprite").objectReferenceValue);
        Assert.IsNotNull(itemSerialized.FindProperty("goldenBrushSprite").objectReferenceValue);
        Assert.IsNotNull(itemSerialized.FindProperty("inkShieldSprite").objectReferenceValue);
        Assert.IsNotNull(itemSerialized.FindProperty("inkCloneSprite").objectReferenceValue);
        Assert.AreEqual(new Vector2(10f, 16f),
            itemSerialized.FindProperty("verticalSpacing").vector2Value);
        Assert.AreEqual(12f, itemSerialized.FindProperty("firstSpawnHeight").floatValue);
        Assert.AreEqual(0.35f, itemSerialized.FindProperty("cloneChanceAt30m").floatValue);
        Assert.AreEqual(0.5f, itemSerialized.FindProperty("cloneChanceAt250m").floatValue);

        var growthController = FindFirstInScene<RunGrowthController>(builderTestScene);
        var growthChoice = FindFirstInScene<GrowthChoiceView>(builderTestScene);
        var growthSpawner = FindFirstInScene<GrowthScrollSpawner>(builderTestScene);
        Assert.IsNotNull(growthController);
        Assert.IsNotNull(growthChoice);
        Assert.IsNotNull(growthSpawner);
        var growthChoiceSerialized = new SerializedObject(growthChoice);
        var growthIcons = growthChoiceSerialized.FindProperty("growthIcons");
        Assert.IsNotNull(growthIcons);
        Assert.AreEqual(8, growthIcons.arraySize);
        for (int i = 0; i < growthIcons.arraySize; i++)
        {
            Assert.IsNotNull(
                growthIcons.GetArrayElementAtIndex(i).objectReferenceValue,
                $"성장 선택 아이콘 {i}번 슬롯이 비어 있습니다.");
        }
        Assert.AreSame(
            growthIcons.GetArrayElementAtIndex(4).objectReferenceValue,
            growthIcons.GetArrayElementAtIndex(5).objectReferenceValue,
            "긴 여운과 겹친 획은 범용 발판 아이콘을 공유해야 합니다.");
        var growthSerialized = new SerializedObject(growthSpawner);
        Assert.IsNotNull(
            growthSerialized.FindProperty("growthScrollSprite").objectReferenceValue);
        Assert.AreEqual(
            GrowthScrollSpawner.DefaultFirstHeight,
            growthSerialized.FindProperty("firstHeight").floatValue);
        Assert.AreEqual(
            GrowthScrollSpawner.DefaultInterval,
            growthSerialized.FindProperty("interval").floatValue);

        var capture = FindFirstInScene<StrokeCapture>(builderTestScene);
        Assert.IsNotNull(capture);
        var captureSerialized = new SerializedObject(capture);
        Assert.AreEqual(3f,
            captureSerialized.FindProperty("inkRegenPerSecond").floatValue);

        var player = FindFirstInScene<PlayerController>(builderTestScene);
        Assert.IsNotNull(player);
        var playerSerialized = new SerializedObject(player);
        Assert.AreEqual(1f,
            playerSerialized.FindProperty("cloneSpawnGraceDuration").floatValue);
        Assert.IsNotNull(player.GetComponent<InkCloneArrivalView>(),
            "씬 빌더가 먹분신 몸통→완성 팝 연출을 플레이어에 구성해야 합니다.");
        PlatformCollider starterPlatform = null;
        var builtPlatforms = FindAllInScene<PlatformCollider>(builderTestScene);
        for (int i = 0; i < builtPlatforms.Length; i++)
            if (builtPlatforms[i].name == "StarterInkPlatform")
                starterPlatform = builtPlatforms[i];
        Assert.IsNotNull(starterPlatform,
            "명시적 시작 버튼은 캐릭터가 즉사하지 않을 영구 시작 발판과 함께 생성돼야 합니다.");
        Assert.AreEqual(2, starterPlatform.GetComponent<EdgeCollider2D>().pointCount);
        Assert.That(starterPlatform.transform.position.y,
            Is.EqualTo(player.transform.position.y - 0.42f).Within(0.001f));

        var inkDropVfx = FindFirstInScene<InkDropJumpVfx>(builderTestScene);
        Assert.IsNotNull(inkDropVfx);
        var vfxSerialized = new SerializedObject(inkDropVfx);
        Assert.IsNotNull(vfxSerialized.FindProperty("groundBlob").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("inkSplash").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("shockRing").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("verticalBrush").objectReferenceValue);
        Assert.IsNotNull(vfxSerialized.FindProperty("immediateClip").objectReferenceValue);
        Assert.IsNotNull(FindFirstInScene<VfxAudioManager>(builderTestScene));
        Assert.IsNotNull(FindFirstInScene<VfxRuntimeMonitor>(builderTestScene));
        var feedbackAudio = FindFirstInScene<VfxAudioManager>(builderTestScene);
        Assert.AreEqual(6, feedbackAudio.GetComponents<AudioSource>().Length);
        Assert.IsNotNull(feedbackAudio.transform.Find("BrushDrawingAudio")
            ?.GetComponent<AudioSource>());
        Assert.IsNotNull(feedbackAudio.transform.Find("PriorityAccentAudio")
            ?.GetComponent<AudioSource>());

        var builtCamera = FindFirstInScene<Camera>(builderTestScene);
        Assert.IsNotNull(builtCamera);
        Assert.IsFalse(builtCamera.allowHDR);
        Assert.IsFalse(builtCamera.allowMSAA);
        var cameraFollow = builtCamera.GetComponent<CameraFollow>();
        Assert.IsNotNull(cameraFollow);
        var cameraFollowSerialized = new SerializedObject(cameraFollow);
        Assert.AreEqual(0.75f,
            cameraFollowSerialized.FindProperty("upperFollowViewportY").floatValue,
            0.001f);
        Assert.AreEqual(0.9f,
            cameraFollowSerialized.FindProperty("hardCeilingViewportY").floatValue,
            0.001f);
        Assert.AreSame(player.transform,
            cameraFollowSerialized.FindProperty("target").objectReferenceValue);

        var lobby = FindFirstInScene<LobbyView>(builderTestScene);
        Assert.IsNotNull(lobby);
        var lobbySerialized = new SerializedObject(lobby);
        var lobbyBest = lobbySerialized.FindProperty("bestText").objectReferenceValue as Text;
        Assert.IsNotNull(lobbyBest);
        Assert.AreEqual(InkPalette.UiFont, lobbyBest.font);
        Assert.AreEqual(37, lobbyBest.fontSize);
        Assert.AreEqual(FontStyle.Normal, lobbyBest.fontStyle);
        Assert.AreEqual(TextAnchor.MiddleCenter, lobbyBest.alignment);
        Assert.AreEqual(Color.white, lobbyBest.color);
        Assert.IsFalse(lobbyBest.resizeTextForBestFit);
        Assert.IsTrue(lobbyBest.alignByGeometry);
        Assert.That(lobbyBest.rectTransform.anchoredPosition.x, Is.EqualTo(-87f).Within(0.01f));
        Assert.That(lobbyBest.rectTransform.anchoredPosition.y, Is.EqualTo(-5f).Within(0.01f));
        Assert.That(lobbyBest.rectTransform.sizeDelta.x, Is.EqualTo(400f).Within(0.01f));
        Assert.That(lobbyBest.rectTransform.sizeDelta.y, Is.EqualTo(80f).Within(0.01f));

        var lobbyLogo = lobby.transform.Find("Logo") as RectTransform;
        Assert.IsNotNull(lobbyLogo?.GetComponent<RawImage>());
        Assert.That(lobbyLogo.anchoredPosition.x, Is.EqualTo(12f).Within(0.01f));
        Assert.That(lobbyLogo.anchoredPosition.y, Is.EqualTo(79f).Within(0.01f));
        Assert.That(lobbyLogo.sizeDelta.x, Is.EqualTo(1281.776f).Within(0.01f));
        Assert.That(lobbyLogo.sizeDelta.y, Is.EqualTo(854.518f).Within(0.01f));
        Assert.IsNull(lobby.transform.Find("BrushGuide"),
            "버튼 시작 로비에는 더 이상 획 시작 안내가 남으면 안 됩니다.");
        var startButton =
            lobbySerialized.FindProperty("startButton").objectReferenceValue as Button;
        var growthButton =
            lobbySerialized.FindProperty("growthButton").objectReferenceValue as Button;
        var codexButton =
            lobbySerialized.FindProperty("codexButton").objectReferenceValue as Button;
        Assert.IsNotNull(startButton);
        Assert.IsNotNull(growthButton);
        Assert.IsNotNull(codexButton);
        Assert.AreEqual("시작",
            startButton.transform.Find("Label")?.GetComponent<Text>()?.text);
        Assert.AreEqual("성장",
            growthButton.transform.Find("Label")?.GetComponent<Text>()?.text);
        Assert.AreEqual("도감",
            codexButton.transform.Find("Label")?.GetComponent<Text>()?.text);
        Assert.IsNotNull(FindFirstInScene<LobbyCollectionView>(builderTestScene));
        Assert.IsNotNull(FindFirstInScene<PermanentGrowthView>(builderTestScene));
        var bestDisplay = lobby.transform.Find("BestDisplay") as RectTransform;
        Assert.IsNotNull(bestDisplay?.GetComponent<RawImage>());
        Assert.That(bestDisplay.anchoredPosition.x, Is.EqualTo(89f).Within(0.01f));
        Assert.That(bestDisplay.anchoredPosition.y, Is.EqualTo(-12f).Within(0.01f));
        Assert.That(bestDisplay.sizeDelta.x, Is.EqualTo(610.273f).Within(0.01f));
        Assert.That(bestDisplay.sizeDelta.y, Is.EqualTo(130.157f).Within(0.01f));

        var gameplayHud = FindFirstInScene<GameplayHudView>(builderTestScene);
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
        Assert.IsNotNull(
            hudSerialized.FindProperty("vfxQualityButton").objectReferenceValue as Button);
        Assert.IsNotNull(
            hudSerialized.FindProperty("vfxStatsText").objectReferenceValue as Text);
        Assert.IsNotNull(
            hudSerialized.FindProperty("growthChoiceButton").objectReferenceValue as Button);
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
        Assert.IsNotNull(FindFirstInScene<PauseMenuView>(builderTestScene));

        var importer = (TextureImporter)AssetImporter.GetAtPath(
            "Assets/Art/Character/Obstacles/anermy_02.png");
        Assert.IsNotNull(importer);
        Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
        Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
        Assert.AreEqual(700f, importer.spritePixelsPerUnit);
        Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);

        var dragonImporter = (TextureImporter)AssetImporter.GetAtPath(
            "Assets/Resources/MukJump/Obstacles/child_ink_dragon.png");
        Assert.IsNotNull(dragonImporter);
        Assert.AreEqual(TextureImporterType.Sprite, dragonImporter.textureType);
        Assert.AreEqual(SpriteImportMode.Single, dragonImporter.spriteImportMode);
        Assert.AreEqual(700f, dragonImporter.spritePixelsPerUnit);
        Assert.AreEqual(TextureWrapMode.Clamp, dragonImporter.wrapMode);

        const string dragonSheetPath =
            "Assets/Resources/MukJump/Obstacles/child_ink_dragon_4frame_v3.png";
        var dragonSheetImporter =
            (TextureImporter)AssetImporter.GetAtPath(dragonSheetPath);
        Assert.IsNotNull(dragonSheetImporter);
        Assert.AreEqual(TextureImporterType.Sprite, dragonSheetImporter.textureType);
        Assert.AreEqual(SpriteImportMode.Multiple, dragonSheetImporter.spriteImportMode);
        Assert.AreEqual(700f, dragonSheetImporter.spritePixelsPerUnit);
        Assert.AreEqual(TextureWrapMode.Clamp, dragonSheetImporter.wrapMode);

        var dragonSheetAssets = AssetDatabase.LoadAllAssetsAtPath(dragonSheetPath);
        var importedFrames = new List<Sprite>(4);
        for (int i = 0; i < dragonSheetAssets.Length; i++)
            if (dragonSheetAssets[i] is Sprite frame)
                importedFrames.Add(frame);
        importedFrames.Sort((left, right) =>
            string.CompareOrdinal(left.name, right.name));
        Assert.AreEqual(4, importedFrames.Count);
        for (int i = 0; i < importedFrames.Count; i++)
        {
            Assert.AreEqual($"child_ink_dragon_frame_{i:00}", importedFrames[i].name);
            Assert.That(importedFrames[i].rect.width, Is.EqualTo(768f).Within(0.01f));
            Assert.That(importedFrames[i].rect.height, Is.EqualTo(512f).Within(0.01f));
            Assert.That(importedFrames[i].rect.x,
                Is.EqualTo((i % 2) * 768f).Within(0.01f));
            Assert.That(importedFrames[i].rect.y,
                Is.EqualTo((1 - i / 2) * 512f).Within(0.01f));
        }
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

    static T FindFirstInScene<T>(Scene scene) where T : Component
    {
        var matches = FindAllInScene<T>(scene);
        return matches.Length > 0 ? matches[0] : null;
    }

    static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        var matches = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return matches.ToArray();

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            matches.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return matches.ToArray();
    }

    static void AssertBuildSettingsUnchanged(
        EditorBuildSettingsScene[] before, EditorBuildSettingsScene[] after)
    {
        Assert.AreEqual(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++)
        {
            Assert.AreEqual(before[i].path, after[i].path);
            Assert.AreEqual(before[i].enabled, after[i].enabled);
        }
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
