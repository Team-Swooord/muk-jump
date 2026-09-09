using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;

public sealed class ItemSpawnerBalanceTests
{
    readonly List<Object> cleanup = new();
    readonly List<Camera> retaggedMainCameras = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < retaggedMainCameras.Count; i++)
            if (retaggedMainCameras[i] != null)
                retaggedMainCameras[i].gameObject.tag = "MainCamera";
        retaggedMainCameras.Clear();

        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void IntroSlotIsAlwaysCloneForEverySeed()
    {
        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();
        for (int seed = 0; seed < 128; seed++)
        {
            GameplayRandom.ResetSession(seed);
            var type = (ItemType)Invoke(spawner, "ChooseItemType", 12f, true);
            Assert.AreEqual(ItemType.InkClone, type, $"seed {seed}");
        }
    }

    [Test]
    public void RetiredInkReserveNeverAppearsInRandomItemPool()
    {
        var spawner = Track(new GameObject("ItemSpawner"))
            .AddComponent<ItemSpawner>();
        for (int seed = 0; seed < 256; seed++)
        {
            GameplayRandom.ResetSession(seed);
            var type = (ItemType)Invoke(spawner, "ChooseItemType", 250f, false);
            Assert.AreNotEqual(ItemType.InkReserve, type, $"seed {seed}");
        }
    }

    [Test]
    public void FirstItemWorldPositionUsesScoreOrigin()
    {
        var score = Track(new GameObject("ScoreManager")).AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        score.ResetOrigin(-6f);
        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();

        float worldY = (float)Invoke(spawner, "WorldYAtGameHeight", 12f);

        Assert.AreEqual(6f, worldY, 0.001f);
        Assert.AreEqual(12f, score.HeightAt(worldY), 0.001f);
    }

    [Test]
    public void CloneCapIs24AndSpawnerExcludesDeadPickupAtCap()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        for (int i = 0; i < GameManager.MaxLivingPlayers; i++)
        {
            var playerObject = Track(new GameObject($"Player_{i:00}"));
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            manager.RegisterPlayer(playerObject.AddComponent<PlayerController>());
        }

        Assert.AreEqual(24, manager.LivingPlayerCount);
        Assert.IsFalse(manager.CanCreateInkClone);

        var spawner = Track(new GameObject("ItemSpawner")).AddComponent<ItemSpawner>();
        for (int seed = 0; seed < 64; seed++)
        {
            GameplayRandom.ResetSession(seed);
            var type = (ItemType)Invoke(spawner, "ChooseItemType", 250f, true);
            Assert.AreNotEqual(ItemType.InkClone, type,
                "상한에서는 먹어도 무효인 분신 픽업을 생성하면 안 됩니다.");
        }
    }

    [Test]
    public void DrawingBalanceUsesRetainedInkCapacityAndGradualEviction()
    {
        var capture = Track(new GameObject("StrokeCapture"))
            .AddComponent<MukJump.Drawing.StrokeCapture>();

        Assert.AreEqual(
            StrokeCapture.DefaultInkCapacity,
            (float)GetField(capture, "inkCapacity"));
        Assert.AreEqual(1.1f, (float)GetField(capture, "evictionFadeDuration"));
        Assert.AreEqual(
            PlatformCollider.DefaultNaturalHoldDuration,
            (float)GetField(capture, "naturalHoldDuration"));
        Assert.AreEqual(4.5f, capture.EffectiveNaturalInkLifetime, 0.0001f);
    }

    [TestCase(12f, 0)]
    [TestCase(18f, 0)]
    [TestCase(24f, 1)]
    [TestCase(3.2f, 2)]
    [TestCase(4.8f, 3)]
    public void LegacySceneInkCapacityUpgradesToCurrentBalance(
        float legacyCapacity,
        int legacyTuningVersion)
    {
        var capture = Track(new GameObject("LegacyStrokeCapture"))
            .AddComponent<StrokeCapture>();
        SetField(capture, "inkCapacity", legacyCapacity);
        SetField(capture, "inkCapacityTuningVersion", legacyTuningVersion);

        Invoke(capture, "UpgradeInkCapacityTuning");

        Assert.AreEqual(
            StrokeCapture.DefaultInkCapacity,
            (float)GetField(capture, "inkCapacity"));
        Assert.AreEqual(
            StrokeCapture.CurrentInkCapacityTuningVersion,
            (int)GetField(capture, "inkCapacityTuningVersion"));
    }

    [TestCase(6f, 3, 6f)]
    [TestCase(4.8f, 0, 4.8f)]
    [TestCase(4.8f, 1, 4.8f)]
    [TestCase(4.8f, 2, 4.8f)]
    [TestCase(4.8f, 4, 4.8f)]
    [TestCase(6f, 4, 6f)]
    public void InkCapacityMigrationPreservesCustomAndAlreadyCurrentValues(
        float savedCapacity,
        int savedTuningVersion,
        float expectedCapacity)
    {
        var capture = Track(new GameObject("CustomStrokeCapture"))
            .AddComponent<StrokeCapture>();
        SetField(capture, "inkCapacity", savedCapacity);
        SetField(capture, "inkCapacityTuningVersion", savedTuningVersion);

        Invoke(capture, "UpgradeInkCapacityTuning");

        Assert.That(
            (float)GetField(capture, "inkCapacity"),
            Is.EqualTo(expectedCapacity).Within(0.0001f));
        Assert.AreEqual(
            StrokeCapture.CurrentInkCapacityTuningVersion,
            (int)GetField(capture, "inkCapacityTuningVersion"));
    }

    [TestCase(0f, 1000f)]
    [TestCase(20f, 1080f)]
    [TestCase(57f, 1179f)]
    public void PortraitInkGaugeKeepsEightPercentMarginOnBothSides(
        float safeXMin,
        float safeWidth)
    {
        MethodInfo boundsMethod = typeof(PrototypeHud).GetMethod(
            "CalculatePortraitHorizontalBounds",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(boundsMethod, Is.Not.Null);
        Rect bounds = (Rect)boundsMethod.Invoke(
            null,
            new object[] { safeXMin, safeWidth });

        Assert.That(bounds.xMin,
            Is.EqualTo(safeXMin + safeWidth * .08f).Within(0.001f));
        Assert.That(bounds.xMax,
            Is.EqualTo(safeXMin + safeWidth * .92f).Within(0.001f));

        // 트랙과 붓 아이콘이 겹친 후의 전체 폭이 같은 경계에
        // 정확히 맞아야 실기기에서 붓만 밖으로 빠지지 않는다.
        MethodInfo heightMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeVisualHeight",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo iconMethod = typeof(PrototypeHud).GetMethod(
            "CalculateBrushIconSize",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(heightMethod, Is.Not.Null);
        Assert.That(iconMethod, Is.Not.Null);
        float iconSize = (float)iconMethod.Invoke(
            null, new object[] { safeWidth });
        float overlap = iconSize * 0.62f;
        float trackWidth = bounds.width - iconSize + overlap;
        Assert.That(trackWidth + iconSize - overlap,
            Is.EqualTo(bounds.width).Within(0.001f));
    }

    [TestCase(540f)]
    [TestCase(1000f)]
    [TestCase(1080f)]
    [TestCase(1179f)]
    [TestCase(1440f)]
    [TestCase(876.75f)]
    public void InkGaugeEntersBrushCenterAtEveryRemainingAmount(float layoutWidth)
    {
        float iconSize = layoutWidth * 0.14f;
        float height = layoutWidth * 0.12f;
        float overlapRatio = (float)typeof(PrototypeHud).GetField("BrushOverlapRatio",
            BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        Assert.That(overlapRatio, Is.EqualTo(0.62f));
        float width = layoutWidth - 60f - iconSize + iconSize * overlapRatio;
        var method = typeof(PrototypeHud).GetMethod("CalculateBrushConnectedTrackRect",
            BindingFlags.Static | BindingFlags.NonPublic);
        var fillMethod = typeof(PrototypeHud).GetMethod("CalculateGaugeFillRect",
            BindingFlags.Static | BindingFlags.NonPublic);
        Rect track = (Rect)method.Invoke(null, new object[] { 30f, width, height, 500f, iconSize });
        var brush = new Rect(track.xMax - iconSize * overlapRatio,
            500f - iconSize * 0.5f, iconSize, iconSize);
        Assert.That(track.center.y, Is.EqualTo(brush.center.y).Within(0.001f));
        Assert.That(track.xMax, Is.GreaterThan(brush.center.x), "붓의 중앙을 살짝 지나도록 끝을 겹친다");
        Assert.That(brush.xMax, Is.EqualTo(layoutWidth - 30f).Within(0.001f));
        foreach (float ratio in new[] { 0f, 0.1f, 0.5f, 1f })
        foreach (bool golden in new[] { false, true })
        {
            Rect fill = (Rect)fillMethod.Invoke(null, new object[] { track, ratio, golden });
            Assert.That(fill.xMax, Is.EqualTo(track.xMax).Within(0.001f));
            Assert.That(fill.center.y, Is.EqualTo(track.center.y).Within(0.001f));
            Assert.That(fill.height, Is.EqualTo(height).Within(0.001f));
            Assert.That(fill.width, Is.EqualTo(track.width * (golden ? 1f : ratio)).Within(0.001f));
        }
    }

    [TestCase("muk_gauge_fill.png")]
    [TestCase("muk_gauge_track.png")]
    public void GaugeAndBrushArtworkActuallyOverlapBeyondTransparentImageBounds(string gaugeFile)
    {
        Texture2D gauge = Track(new Texture2D(2, 2));
        Texture2D brush = Track(new Texture2D(2, 2));
        Assert.That(gauge.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/UI/" + gaugeFile)), Is.True);
        Assert.That(brush.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/UI/muk_brush_icon.png")), Is.True);
        const float iconSize = 140f;
        var method = typeof(PrototypeHud).GetMethod("CalculateBrushConnectedTrackRect",
            BindingFlags.Static | BindingFlags.NonPublic);
        Rect track = (Rect)method.Invoke(null, new object[] { 30f, 844.8f, 120f, 500f, iconSize });
        var icon = new Rect(track.xMax - iconSize * 0.62f, 430f, iconSize, iconSize);
        float cap = Mathf.Clamp(gauge.height / (float)gauge.width, 0.08f, 0.25f);
        int opaqueOverlap = 0;
        // 3-slice의 오른쪽 캡과 실제 붓털 알파를 같은 GUI 위치에서 비교한다.
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float px = Mathf.Lerp(icon.x, track.xMax, (x + 0.5f) / 32f);
            float py = Mathf.Lerp(track.y, track.yMax, (y + 0.5f) / 32f);
            if (!icon.Contains(new Vector2(px, py))) continue;
            float u = 1f - (track.xMax - px) / track.height * cap;
            float v = 1f - (py - track.y) / track.height;
            float brushU = (px - icon.x) / icon.width;
            float brushV = 1f - (py - icon.y) / icon.height;
            if (gauge.GetPixelBilinear(u, v).a > 0.5f &&
                brush.GetPixelBilinear(brushU, brushV).a > 0.5f) opaqueOverlap++;
        }
        Assert.That(opaqueOverlap, Is.GreaterThan(20),
            "Rect만 겹치고 투명 여백 때문에 실제 게이지와 붓이 떨어져 있으면 안 됩니다.");
    }

    [Test]
    public void FreshRunSnapsFullInkButRewardedRevivePreservesDisplay()
    {
        var capture = Track(new GameObject("HudStrokeCapture"))
            .AddComponent<StrokeCapture>();
        var hud = Track(new GameObject("PrototypeHud"))
            .AddComponent<PrototypeHud>();
        SetField(hud, "strokeCapture", capture);
        SetField(hud, "displayedInkRatio", 0.25f);
        SetField(hud, "displayedInkRatioInitialized", true);

        Invoke(
            hud,
            "HandleGameStateChanged",
            GameState.Lobby,
            GameState.Playing);

        Assert.That(capture.InkRemaining01, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(
            (float)GetField(hud, "displayedInkRatio"),
            Is.EqualTo(1f).Within(0.0001f));

        SetField(hud, "displayedInkRatio", 0.25f);
        Invoke(
            hud,
            "HandleGameStateChanged",
            GameState.GameOver,
            GameState.Playing);

        Assert.That(
            (float)GetField(hud, "displayedInkRatio"),
            Is.EqualTo(0.25f).Within(0.0001f),
            "광고 부활은 같은 판이므로 기존 먹 표시를 보존해야 합니다.");
    }

    [Test]
    public void GoldenBrushRingTracksRealTimeRefreshAndReset()
    {
        var capture = Track(new GameObject("GoldenTimerClock")).AddComponent<StrokeCapture>();
        Assert.That(capture.UnlimitedInkRemaining01, Is.Zero);
        capture.ActivateUnlimitedInk(8);
        Assert.That(capture.UnlimitedInkRemaining01, Is.EqualTo(1));
        Invoke(capture, "AdvanceUnlimitedInk", 2f);
        Assert.That(capture.UnlimitedInkRemainingSeconds, Is.EqualTo(6));
        Assert.That(capture.UnlimitedInkRemaining01, Is.EqualTo(0.75f));
        capture.ActivateUnlimitedInk(4);
        Assert.That(capture.UnlimitedInkRemaining01, Is.EqualTo(0.75f), "짧은 재획득은 남은 시간을 줄이거나 링을 재설정하지 않는다");
        capture.ActivateUnlimitedInk(8);
        Assert.That(capture.UnlimitedInkRemaining01, Is.EqualTo(1));
        Invoke(capture, "AdvanceUnlimitedInk", 8f);
        Assert.That(capture.UnlimitedInkRemaining01, Is.Zero);
        Assert.That(capture.HasUnlimitedInk, Is.False);
        capture.ActivateUnlimitedInk(8);
        Invoke(capture, "HandleGrowthRunReset");
        Assert.That(capture.UnlimitedInkRemainingSeconds, Is.Zero);
        Assert.That(capture.UnlimitedInkRemaining01, Is.Zero);
    }

    [Test]
    public void GoldenBrushRingFreezesWithPausedGameplay()
    {
        var manager = Track(new GameObject("GoldenTimerPause")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        var capture = Track(new GameObject("GoldenTimerCapture")).AddComponent<StrokeCapture>();
        capture.ActivateUnlimitedInk(8);
        Invoke(capture, "AdvanceUnlimitedInk", 2f);
        Assert.That(manager.PauseGame(), Is.True);
        Invoke(capture, "Update");
        Assert.That(capture.UnlimitedInkRemainingSeconds, Is.EqualTo(6));
        Assert.That(capture.UnlimitedInkRemaining01, Is.EqualTo(0.75f));
    }

    [TestCase(390f, 844f)]
    [TestCase(1179f, 2556f)]
    [TestCase(1518f, 835f)]
    public void GoldenBrushRingIsSmallRoundAndInsideSafeArea(float width, float height)
    {
        var safe = new Rect(0, 48, width, height - 96);
        var body = new Rect(width * .45f, height * .45f, width * .07f, width * .07f);
        var method = typeof(PrototypeHud).GetMethod("TryCalculateGoldenTimerRect", BindingFlags.Static | BindingFlags.NonPublic);
        object[] arguments = { body, default(Rect), safe, default(Rect) };
        Assert.That((bool)method.Invoke(null, arguments), Is.True);
        var rect = (Rect)arguments[3];
        Assert.That(rect.width, Is.EqualTo(rect.height));
        Assert.That(rect.width, Is.InRange(20f, 60f));
        Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(safe.xMin + 8));
        Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(safe.yMin + 8));
        Assert.That(rect.xMax, Is.LessThanOrEqualTo(safe.xMax - 8));
        Assert.That(rect.yMax, Is.LessThanOrEqualTo(safe.yMax - 8));
        Assert.That(rect.xMin, Is.GreaterThan(body.xMax), "붓이 아닌 캐릭터 오른쪽에 붙입니다.");
        Assert.That(rect.yMin, Is.LessThan(body.center.y), "머리 옆 위쪽에 가깝게 붙입니다.");
        Assert.That(rect.Overlaps(body), Is.False);
    }

    [Test]
    public void GoldenTimerShaderRendersHollowClockwiseDrainAndReleasesMaterial()
    {
        var shader = Resources.Load<Shader>("MukJump/Shaders/GoldenBrushTimer");
        Assert.That(shader, Is.Not.Null);
        Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False);
        Assert.That(shader.isSupported, Is.True);
        var hud = Track(new GameObject("GoldenTimerMaterial")).AddComponent<PrototypeHud>();
        Invoke(hud, "OnEnable");
        var material = (Material)GetField(hud, "goldenTimerMaterial");
        Assert.That(material, Is.Not.Null);
        var texture = Track(new Texture2D(128, 128, TextureFormat.RGBA32, false, true));
        var target = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            material.SetFloat("_Remaining", 0.5f);
            Graphics.Blit(Texture2D.whiteTexture, target, material);
            texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            texture.Apply();
            Assert.That(texture.GetPixel(64, 64).a, Is.LessThan(0.01f), "링 가운데는 투명하게 비운다");
            Assert.That(texture.GetPixel(14, 64).r, Is.GreaterThan(texture.GetPixel(114, 64).r + 0.2f),
                "남은 시간이 절반이면 오른쪽이 비고 왼쪽은 노란색으로 남는다");
            GL.Clear(true, true, Color.clear);
            material.SetFloat("_Remaining", 0);
            Graphics.Blit(Texture2D.whiteTexture, target, material);
            texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            texture.Apply();
            Assert.That(texture.GetPixel(14, 64).a, Is.LessThan(0.01f));
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        Invoke(hud, "OnDisable");
        Assert.That(material == null, Is.True, "HUD 종료 때 재사용 머티리얼을 해제한다");
    }

    [Test]
    public void RewardedRevivePreservesSameRunGrowthInkLedgerAndUnlimitedInk()
    {
        var root = Track(new GameObject("RewardedReviveRunRoot"));
        var manager = root.AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        var growth = root.AddComponent<RunGrowthController>();
        Invoke(growth, "OnEnable");

        var capture = Track(new GameObject("RewardedReviveStrokeCapture"))
            .AddComponent<StrokeCapture>();
        Invoke(capture, "OnEnable");

        int resetCount = 0;
        growth.RunReset += () => resetCount++;
        Invoke(manager, "SetState", GameState.Playing);
        Assert.That(resetCount, Is.EqualTo(1),
            "로비에서 시작한 새 판은 성장 상태를 한 번 초기화해야 합니다.");

        var platform = PlatformCollider.Spawn(new List<Vector2>
        {
            new(100f, 100f),
            new(101f, 100f),
        }, 1f);
        Track(platform.gameObject);
        capture.ActivateUnlimitedInk(30f);
        float retainedInk = PlatformCollider.ActiveInkCost;

        Invoke(manager, "SetState", GameState.GameOver);
        Invoke(manager, "SetState", GameState.Playing);

        Assert.That(resetCount, Is.EqualTo(1),
            "광고 부활은 같은 판이므로 RunReset을 다시 보내면 안 됩니다.");
        Assert.That(capture.HasUnlimitedInk, Is.True,
            "광고 부활이 같은 판의 무한 먹 상태를 지우면 안 됩니다.");
        Assert.That(PlatformCollider.ActiveInkCost,
            Is.EqualTo(retainedInk).Within(0.0001f),
            "광고 부활이 남아 있던 먹선 장부를 재정산하면 안 됩니다.");
    }

    [Test]
    public void RewardedRevivePreservesMonotonicSwarmProgressForTheSameRun()
    {
        var manager = Track(new GameObject("RewardedReviveProgressManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        SetField(manager, "maxSwarmProgressHeight", 74f);

        Invoke(manager, "SetState", GameState.GameOver);
        Invoke(manager, "SetState", GameState.Playing);

        Assert.That(manager.SwarmProgressHeight,
            Is.EqualTo(74f).Within(0.0001f),
            "광고 부활은 같은 판이므로 성장 정산용 진행 높이를 초기화하면 안 됩니다.");
    }

    [Test]
    public void InkGaugeBrushKeepsReadableRealDeviceSize()
    {
        MethodInfo heightMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeVisualHeight",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo iconMethod = typeof(PrototypeHud).GetMethod(
            "CalculateBrushIconSize",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo bottomMarginMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeBottomMargin",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo bottomMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeBottom",
            BindingFlags.Static | BindingFlags.NonPublic);

        const float iphoneWidth = 1179f;
        float gaugeHeight = (float)heightMethod.Invoke(
            null, new object[] { iphoneWidth });
        float iconSize = (float)iconMethod.Invoke(
            null, new object[] { iphoneWidth });
        float bottomMargin = (float)bottomMarginMethod.Invoke(
            null, new object[] { iphoneWidth });
        float gaugeBottom = (float)bottomMethod.Invoke(
            null, new object[] { 2350f, 2556f });

        Assert.That(gaugeHeight, Is.EqualTo(141.48f).Within(0.01f));
        Assert.That(iconSize, Is.EqualTo(165.06f).Within(0.01f));
        Assert.That(iconSize, Is.GreaterThan(gaugeHeight));
        Assert.That(bottomMargin, Is.EqualTo(9.432f).Within(0.01f),
            "세로 화면 높이가 아니라 폭을 기준으로 하단 여백을 잡아 붓을 아래에 둡니다.");
        Assert.That(gaugeBottom, Is.EqualTo(2453f).Within(0.01f),
            "비터치 HUD는 하단 안전영역의 절반까지 내려 첫 캐릭터와 겹치지 않게 합니다.");
    }

    [Test]
    public void WideWebInkGaugeUsesCompactHeightBasedLayoutOnlyOnWebGl()
    {
        MethodInfo compactMethod = typeof(PrototypeHud).GetMethod(
            "ShouldUseCompactWideWebLayout",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo layoutWidthMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeLayoutWidth",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo heightMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeVisualHeight",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo iconMethod = typeof(PrototypeHud).GetMethod(
            "CalculateBrushIconSize",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(compactMethod, Is.Not.Null);
        Assert.That(layoutWidthMethod, Is.Not.Null);

        bool wideWeb = (bool)compactMethod.Invoke(
            null,
            new object[] { RuntimePlatform.WebGLPlayer, 1518, 835 });
        bool portraitWeb = (bool)compactMethod.Invoke(
            null,
            new object[] { RuntimePlatform.WebGLPlayer, 1080, 1920 });
        bool landscapeIos = (bool)compactMethod.Invoke(
            null,
            new object[] { RuntimePlatform.IPhonePlayer, 1518, 835 });
        float compactWidth = (float)layoutWidthMethod.Invoke(
            null,
            new object[] { 1518f, 835f, wideWeb });
        float mobileWidth = (float)layoutWidthMethod.Invoke(
            null,
            new object[] { 1179f, 2361f, portraitWeb });
        float compactGaugeHeight = (float)heightMethod.Invoke(
            null,
            new object[] { compactWidth });
        float compactBrushSize = (float)iconMethod.Invoke(
            null,
            new object[] { compactWidth });

        Assert.That(wideWeb, Is.True);
        Assert.That(portraitWeb, Is.False,
            "앱인토스의 세로 WebGL은 모바일 레이아웃을 유지해야 합니다.");
        Assert.That(landscapeIos, Is.False,
            "iOS 네이티브 레이아웃은 WebGL 보정의 영향을 받으면 안 됩니다.");
        Assert.That(compactWidth, Is.EqualTo(876.75f).Within(0.01f));
        Assert.That(mobileWidth, Is.EqualTo(1179f).Within(0.01f));
        Assert.That(compactGaugeHeight, Is.EqualTo(105.21f).Within(0.01f));
        Assert.That(compactBrushSize, Is.EqualTo(122.745f).Within(0.01f));
    }

    [Test]
    public void InkGaugeRemainingFillChangesInsideFixedTrack()
    {
        MethodInfo rectMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeFillRect",
            BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo uvMethod = typeof(PrototypeHud).GetMethod(
            "CalculateGaugeFillUv",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(rectMethod, Is.Not.Null);
        Assert.That(uvMethod, Is.Not.Null);

        var area = new Rect(100f, 50f, 400f, 80f);
        Rect empty = (Rect)rectMethod.Invoke(
            null, new object[] { area, 0f, false });
        Rect quarter = (Rect)rectMethod.Invoke(
            null, new object[] { area, 0.25f, false });
        Rect half = (Rect)rectMethod.Invoke(
            null, new object[] { area, 0.5f, false });
        Rect full = (Rect)rectMethod.Invoke(
            null, new object[] { area, 1f, false });
        Rect golden = (Rect)rectMethod.Invoke(
            null, new object[] { area, 0f, true });
        Rect quarterUv = (Rect)uvMethod.Invoke(
            null, new object[] { 0.25f, false });

        Assert.That(empty.x, Is.EqualTo(500f).Within(0.001f));
        Assert.That(empty.width, Is.Zero.Within(0.001f));
        Assert.That(quarter.x, Is.EqualTo(400f).Within(0.001f));
        Assert.That(quarter.width, Is.EqualTo(100f).Within(0.001f));
        Assert.That(half.x, Is.EqualTo(300f).Within(0.001f));
        Assert.That(half.width, Is.EqualTo(200f).Within(0.001f));
        Assert.That(full, Is.EqualTo(area));
        Assert.That(golden, Is.EqualTo(area));
        Assert.That(quarter.xMax, Is.EqualTo(area.xMax).Within(0.001f));
        Assert.That(half.xMax, Is.EqualTo(area.xMax).Within(0.001f));
        Assert.That(quarterUv.x, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(quarterUv.width, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void SwarmProgressUsesLowerMedianInsteadOfSingleOutlier()
    {
        var players = new List<PlayerController>();
        float[] heights = { 100f, 12f, 11f, 10f, 9f };
        for (int i = 0; i < heights.Length; i++)
        {
            var playerObject = Track(new GameObject($"CameraPlayer_{i}"));
            playerObject.transform.position = Vector3.up * heights[i];
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        float followY = GameManager.ResolveSwarmAnchorY(
            players, out var representative);

        Assert.AreEqual(11f, followY, 0.001f);
        Assert.AreEqual(11f, representative.transform.position.y, 0.001f);
    }

    [Test]
    public void TwoPlayerProgressUsesLowerPlayerDuringLeaderBoost()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 50f, 10f })
        {
            var playerObject = Track(new GameObject($"TwoPlayer_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        float followY = GameManager.ResolveSwarmAnchorY(
            players, out var representative);

        Assert.AreEqual(10f, followY, 0.001f);
        Assert.AreEqual(10f, representative.transform.position.y, 0.001f);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(24)]
    public void SwarmCameraFrameUsesHighestLivingPlayer(int playerCount)
    {
        var players = new List<PlayerController>(playerCount);
        for (int i = playerCount - 1; i >= 0; i--)
        {
            var playerObject = Track(new GameObject($"FramePlayer_{i}"));
            playerObject.transform.position = Vector3.up * i;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        bool resolved = GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY);

        Assert.That(resolved, Is.True);
        Assert.That(representative, Is.Not.Null);
        float expectedHighest = playerCount - 1;
        Assert.That(clusterY, Is.EqualTo(expectedHighest).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(expectedHighest).Within(0.001f));
        Assert.That(representative.transform.position.y,
            Is.EqualTo(expectedHighest).Within(0.001f));
    }

    [Test]
    public void SwarmCameraTracksHighestLivingOutlier()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 100f, 12f, 11f, 10f, 9f })
        {
            var playerObject = Track(new GameObject($"GuardPlayer_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }

        Assert.That(GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY), Is.True);
        Assert.That(representative.transform.position.y,
            Is.EqualTo(100f).Within(0.001f));
        Assert.That(clusterY, Is.EqualTo(100f).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(100f).Within(0.001f),
            "본체 여부와 무관하게 가장 높은 생존 먹방울을 놓치면 안 됩니다.");
    }

    [Test]
    public void SwarmCameraFrameDropsDeadOriginalAndUsesLivingClones()
    {
        var players = new List<PlayerController>();
        foreach (float height in new[] { 4f, 8f, 12f })
        {
            var playerObject = Track(new GameObject($"LivingClone_{height}"));
            playerObject.transform.position = Vector3.up * height;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            players.Add(playerObject.AddComponent<PlayerController>());
        }
        typeof(PlayerController).GetProperty(
                nameof(PlayerController.IsDead),
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)
            ?.SetValue(players[0], true);
        PlayerController expectedRepresentative = players[2];

        Assert.That(GameManager.ResolveSwarmCameraFrame(
            players,
            out var representative,
            out float clusterY,
            out float upperGuardY), Is.True);
        Assert.That(representative, Is.SameAs(expectedRepresentative),
            "사망 원본을 제거한 뒤 가장 높은 생존 분신이 카메라 대표여야 합니다.");
        Assert.That(clusterY, Is.EqualTo(12f).Within(0.001f));
        Assert.That(upperGuardY, Is.EqualTo(12f).Within(0.001f));
    }

    [Test]
    public void ClonePickupWithoutGrowthAddsExactlyOnePlayer()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneSource"));
        var sourceBody = sourceObject.AddComponent<Rigidbody2D>();
        sourceBody.linearVelocity = new Vector2(1.25f, 2.5f);
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Assert.AreEqual(1, manager.LivingPlayerCount);
        Assert.IsTrue(ItemEffect.Apply(ItemType.InkClone, source));
        Assert.AreEqual(2, manager.LivingPlayerCount,
            "성장이 없으면 먹분신 아이템 한 번은 기본 한 마리만 늘려야 합니다.");

        var living = new List<PlayerController>();
        manager.GetLivingPlayersNonAlloc(living);
        for (int i = 0; i < living.Count; i++)
            if (living[i] != source)
            {
                Vector2 cloneVelocity = living[i].Body.linearVelocity;
                Assert.That(cloneVelocity.x,
                    Is.GreaterThan(sourceBody.linearVelocity.x),
                    "오른쪽에 생긴 분신은 원본과 같은 줄에 멈추지 않고 바깥으로 퍼져야 합니다.");
                Assert.That(cloneVelocity.y,
                    Is.GreaterThan(sourceBody.linearVelocity.y + 1f),
                    "새 분신은 팝콘처럼 눈에 보이는 상향 속도로 튀어야 합니다.");
                Assert.That(living[i].MaxHealth,
                    Is.EqualTo(PlayerController.RuntimeCloneMaxHealth));
                Assert.That(living[i].CurrentHealth,
                    Is.EqualTo(PlayerController.RuntimeCloneMaxHealth),
                    "성장을 찍지 않은 새 먹분신은 기본 1/1로 시작해야 합니다.");
                Track(living[i].gameObject);
            }
    }

    [Test]
    public void ClonePreparationFailureRestoresEveryAttemptedHookAndAddsNoClone()
    {
        var manager = Track(new GameObject("CloneFailureManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneFailureSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        var first = sourceObject.AddComponent<CloneLifecycleProbe>();
        var failing = sourceObject.AddComponent<CloneLifecycleProbe>();
        failing.ThrowOnPrepare = true;
        manager.RegisterPlayer(source);

        LogAssert.Expect(LogType.Exception, "InvalidOperationException: clone prepare failed");
        Assert.IsFalse(manager.TryCreateInkClone(source));
        Assert.AreEqual(1, first.PrepareCount);
        Assert.AreEqual(1, first.RestoreCount,
            "앞 훅은 뒤 훅의 준비 실패와 무관하게 반드시 복구되어야 합니다.");
        Assert.AreEqual(1, failing.RestoreCount,
            "예외를 낸 훅도 부분 변경 가능성이 있어 복구를 시도해야 합니다.");
        Assert.AreEqual(1, manager.LivingPlayerCount,
            "실패한 복제는 먹분신 수나 아이템 결과를 바꾸면 안 됩니다.");
    }

    [Test]
    public void CloneRestoreFailureDoesNotSkipEarlierHookOrRegisterClone()
    {
        var manager = Track(new GameObject("CloneRestoreFailureManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneRestoreFailureSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        var first = sourceObject.AddComponent<CloneLifecycleProbe>();
        var failing = sourceObject.AddComponent<CloneLifecycleProbe>();
        failing.ThrowOnRestore = true;
        manager.RegisterPlayer(source);

        LogAssert.Expect(LogType.Exception, "InvalidOperationException: clone restore failed");
        Assert.IsFalse(manager.TryCreateInkClone(source));
        Assert.AreEqual(1, first.RestoreCount,
            "뒤 훅의 복구 실패 뒤에도 앞 훅 복구를 계속해야 합니다.");
        Assert.AreEqual(1, manager.LivingPlayerCount,
            "복구가 완전하지 않은 복제는 등록하면 안 됩니다.");
        Assert.That(System.Array.Exists(
            Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include),
            player => player.name == "CloneRestoreFailureSource(Clone)"), Is.False,
            "실패한 복제가 남아 다음 로비·물리 검증을 오염시키면 안 됩니다.");
    }

    [Test]
    public void ClonePopVelocityAlternatesAndKeepsEverySpawnDistinct()
    {
        Vector2 sourceVelocity = new(8f, -4f);
        Vector2 right = GameManager.ResolveClonePopVelocity(
            sourceVelocity, 1f, 1);
        Vector2 left = GameManager.ResolveClonePopVelocity(
            sourceVelocity, -1f, 2);

        Assert.That(right.x, Is.GreaterThan(0f));
        Assert.That(left.x, Is.LessThan(0f));
        Assert.That(right.y, Is.GreaterThan(GameManager.ClonePopVerticalSpeed));
        Assert.That(left.y, Is.EqualTo(GameManager.ClonePopVerticalSpeed)
            .Within(0.001f));
        Assert.That(right, Is.Not.EqualTo(left));

        Vector2 specialRise = GameManager.ResolveClonePopVelocity(
            new Vector2(0f, 42f), 1f, 3, preserveSpecialRise: true);
        Assert.That(specialRise.y, Is.EqualTo(42f).Within(0.001f),
            "먹물방울·부활 상승은 속도를 더하지 않아 분신이 카메라를 끌고 가면 안 됩니다.");

        Vector2 repeatedNormalRise = GameManager.ResolveClonePopVelocity(
            new Vector2(0f, 17.5f), -1f, 4);
        Assert.That(repeatedNormalRise.y,
            Is.EqualTo(GameManager.ClonePopMaximumVerticalSpeed).Within(0.001f),
            "연쇄 복제에서도 일반 상승 속도는 상한을 넘어 누적되면 안 됩니다.");
    }

    [TestCase(0, 24, 1)]
    [TestCase(1, 24, 2)]
    [TestCase(4, 24, 2)]
    [TestCase(4, 1, 1)]
    [TestCase(4, 0, 0)]
    [TestCase(99, 24, 2)]
    public void ClonePickupGrowthAddsAtMostOneBonusWithinLivingCap(
        int growthExtraCount,
        int availableSlots,
        int expectedCount)
    {
        Assert.AreEqual(
            expectedCount,
            GameManager.ResolveInkCloneItemSpawnCount(
                growthExtraCount,
                availableSlots));
    }

    [Test]
    public void InkDropLaunchesEveryLivingCloneTogether()
    {
        var manager = Track(new GameObject("InkDropSwarmManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var firstObject = Track(new GameObject("InkDropPlayerA"));
        var firstBody = firstObject.AddComponent<Rigidbody2D>();
        firstBody.gravityScale = 1f;
        firstObject.AddComponent<CircleCollider2D>();
        var first = firstObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(first);

        var secondObject = Track(new GameObject("InkDropPlayerB"));
        secondObject.transform.position = new Vector3(0f, 8f, 0f);
        var secondBody = secondObject.AddComponent<Rigidbody2D>();
        secondBody.gravityScale = 1f;
        secondObject.AddComponent<CircleCollider2D>();
        var second = secondObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(second);

        Assert.IsTrue(ItemEffect.Apply(ItemType.InkDrop, second));

        Assert.IsTrue(first.IsInkDropBoosted);
        Assert.IsTrue(second.IsInkDropBoosted);
        Assert.That(firstBody.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(secondBody.linearVelocity.y,
            Is.EqualTo(firstBody.linearVelocity.y).Within(0.001f),
            "먹물방울을 먹은 분신만 카메라 위로 이탈하면 안 됩니다.");
    }

    [Test]
    public void SwarmProgressHeightUsesGroupAnchorInsteadOfScoreLeader()
    {
        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);
        var score = Track(new GameObject("ScoreManager")).AddComponent<ScoreManager>();
        Invoke(score, "OnEnable");
        score.ResetOrigin(-6f);

        var progressPlayers = new List<PlayerController>();
        foreach (float worldY in new[] { 44f, 4f })
        {
            var playerObject = Track(new GameObject($"ProgressPlayer_{worldY}"));
            playerObject.transform.position = Vector3.up * worldY;
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            var progressPlayer = playerObject.AddComponent<PlayerController>();
            manager.RegisterPlayer(progressPlayer);
            progressPlayers.Add(progressPlayer);
        }

        Assert.AreEqual(10f, manager.SwarmProgressHeight, 0.001f,
            "50m로 튄 선두가 10m의 먹떼보다 먼저 위험물을 열면 안 됩니다.");

        foreach (var player in progressPlayers)
            player.transform.position -= Vector3.up * 8f;
        Assert.AreEqual(10f, manager.SwarmProgressHeight, 0.001f,
            "일반 플레이 구간 진행은 점프 하강 때문에 뒤로 돌아가면 안 됩니다.");
    }

    [Test]
    public void DrawingOverlapDefersContactInsteadOfDeletingTheLine()
    {
        var playerObject = Track(new GameObject("ClearancePlayer"));
        var playerCollider = playerObject.AddComponent<CircleCollider2D>();
        playerCollider.radius = 0.4f;
        playerCollider.offset = new Vector2(0f, 0.1f);
        var player = playerObject.AddComponent<PlayerController>();
        float platformY = playerCollider.offset.y;
        var platform = Track(MukJump.Drawing.PlatformCollider.Spawn(
            new List<Vector2>
            {
                new(-1f, platformY),
                new(1f, platformY),
            }));
        Physics2D.SyncTransforms();
        platform.DeferInitialPlayerContacts(new[] { player });
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider,
            platform.GetComponent<EdgeCollider2D>()), Is.True);
        Assert.That(platform.Length, Is.EqualTo(2f).Within(0.001f),
            "겹친 캐릭터와 충돌만 유예하고 선 전체를 보존해야 합니다.");
    }

    [Test]
    public void CloneSpawnStaysImmediatelyBesideCollector()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("CloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("GameManager")).AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CloneSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Vector3 result = (Vector3)Invoke(manager, "FindCloneSpawnPosition", source, 1);

        Assert.That(Mathf.Abs(result.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f),
            "반지름 0.4 캐릭터는 0.1 간격을 두고 바로 옆에 생겨야 합니다.");
        Assert.That(result.y, Is.EqualTo(source.transform.position.y).Within(0.001f));
    }

    [Test]
    public void CloneSpawnsOnOppositeSideOfCollectorWithOffsetCamera()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("OffsetCloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(3.25f, 14f, -10f);
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("OppositeCloneManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("OppositeCloneSource"));
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        source.transform.position = new Vector3(1.25f, 7f, 0f);
        Vector3 fromLeft = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Assert.Greater(fromLeft.x, source.transform.position.x,
            "카메라 왼쪽의 획득자에게는 화면 안쪽인 오른편 바로 옆에 생겨야 합니다.");
        Assert.That(fromLeft.x - source.transform.position.x,
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.Less(fromLeft.x, cameraObject.transform.position.x,
            "인접 생성 때문에 화면 중앙을 넘어 멀리 떨어지면 안 됩니다.");

        source.transform.position = new Vector3(5.25f, 7f, 0f);
        Vector3 fromRight = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);
        Assert.Less(fromRight.x, source.transform.position.x,
            "카메라 오른쪽의 획득자에게는 화면 안쪽인 왼편 바로 옆에 생겨야 합니다.");
        Assert.That(source.transform.position.x - fromRight.x,
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.Greater(fromRight.x, cameraObject.transform.position.x,
            "인접 생성 때문에 화면 중앙을 넘어 멀리 떨어지면 안 됩니다.");
    }

    [Test]
    public void CloneSpawnAtCameraCenterAlternatesSides()
    {
        RetagExistingMainCameras();
        var cameraObject = Track(new GameObject("CenteredCloneSpawnCamera"));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(-2f, 6f, -10f);
        var worldCamera = cameraObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = 9.6f;

        var manager = Track(new GameObject("CenteredCloneManager"))
            .AddComponent<GameManager>();
        Invoke(manager, "OnEnable");
        Invoke(manager, "SetState", GameState.Playing);

        var sourceObject = Track(new GameObject("CenteredCloneSource"));
        sourceObject.transform.position = new Vector3(-2f, 2f, 0f);
        sourceObject.AddComponent<Rigidbody2D>();
        sourceObject.AddComponent<CircleCollider2D>().radius = 0.4f;
        var source = sourceObject.AddComponent<PlayerController>();
        manager.RegisterPlayer(source);

        Vector3 odd = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 1);
        Vector3 even = (Vector3)Invoke(
            manager, "FindCloneSpawnPosition", source, 2);

        Assert.Greater(odd.x, cameraObject.transform.position.x);
        Assert.Less(even.x, cameraObject.transform.position.x);
        Assert.That(Mathf.Abs(odd.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(Mathf.Abs(even.x - source.transform.position.x),
            Is.EqualTo(0.9f).Within(0.001f));
    }

    void RetagExistingMainCameras()
    {
        var cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (!candidate.CompareTag("MainCamera")) continue;
            candidate.gameObject.tag = "Untagged";
            retaggedMainCameras.Add(candidate);
        }
    }

    T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    static object GetField(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    static object Invoke(object target, string methodName, params object[] arguments)
    {
        return target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.NonPublic)?.Invoke(target, arguments);
    }

    public sealed class CloneLifecycleProbe : MonoBehaviour,
        IRuntimeCloneLifecycle
    {
        public int PrepareCount { get; private set; }
        public int RestoreCount { get; private set; }
        public bool ThrowOnPrepare { get; set; }
        public bool ThrowOnRestore { get; set; }

        public void PrepareForRuntimeClone()
        {
            PrepareCount++;
            if (ThrowOnPrepare)
                throw new System.InvalidOperationException(
                    "clone prepare failed");
        }

        public void RestoreAfterRuntimeClone()
        {
            RestoreCount++;
            if (ThrowOnRestore)
                throw new System.InvalidOperationException(
                    "clone restore failed");
        }
    }

}
