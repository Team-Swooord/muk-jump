using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;
using MukJump.Obstacles;

namespace MukJump.EditorTests
{
    public sealed class VfxRoadmapTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        InkDropJumpVfxInstance ascent;
        VfxQualityTier previousTier;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            previousTier = VfxQualityRuntime.Tier;
            Tier(VfxQualityTier.High);
            root = new GameObject("VfxRoadmapTests");
            ascent = root.AddComponent<InkDropJumpVfxInstance>();
            const string art = "Assets/MukJump/VFX/InkDropJump/Textures/";
            Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(art + name + ".png");
            ascent.Initialize(null, new InkDropJumpVfxInstance.AssetSet(
                S("T_VFX_InkDrop_128"), S("T_VFX_InkGroundBlob_512"), S("T_VFX_InkSplash_512"),
                S("T_VFX_InkShockRing_512"), S("T_VFX_InkVerticalBrush_256x1024"),
                S("T_VFX_BrushFibers_256x1024"), S("T_VFX_SoftFlash_256"),
                S("T_VFX_InkStreak_128x512"), Array.Empty<Sprite>()), 24, 18);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            Tier(previousTier);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void Ascent_ArtHasHeavyDropsAndThinFibers_WithoutChangingGameplayRandom()
        {
            var state = UnityEngine.Random.state;
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.state = state;
            Invoke(ascent, "PrepareMotionStates");
            Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            UnityEngine.Random.state = state;
            float heavy = root.transform.Find("InkSpray_00").GetComponent<SpriteRenderer>().bounds.size.x;
            float fine = root.transform.Find("InkSpray_01").GetComponent<SpriteRenderer>().bounds.size.x;
            Assert.That(heavy, Is.GreaterThan(fine * 1.3f));
            Assert.That(heavy, Is.InRange(0.09f, 0.16f));
        }

        [TestCase(VfxQualityTier.Low, 1)]
        [TestCase(VfxQualityTier.Medium, 2)]
        [TestCase(VfxQualityTier.High, 3)]
        public void Ascent_QualityKeepsFixedPoolAndAfterimageLimit(VfxQualityTier tier, int count)
        {
            Tier(tier);
            Invoke(ascent, "PrepareMotionStates");
            Assert.That(Active("afterimageMotions"), Is.EqualTo(count));
            Assert.That(ascent.BuiltChildCount, Is.EqualTo(52));
            for (int i = 0; i < 100; i++)
            {
                ascent.OnPoolRelease();
                ascent.OnPoolAcquire();
                Invoke(ascent, "PrepareMotionStates");
            }
            Assert.That(ascent.BuiltChildCount, Is.EqualTo(52));
            ascent.OnPoolRelease();
            Assert.That(Active("sprayMotions") + Active("residualMotions") + Active("afterimageMotions"), Is.Zero);
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.enabled, Is.False);
        }

        [Test]
        public void Ascent_ReducedMotionDuringPlaybackCancelsAllMovingDecoration()
        {
            Invoke(ascent, "PrepareMotionStates");
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Invoke(ascent, "UpdateSprays", 0.02f);
            Invoke(ascent, "UpdateResidualDrops", 0.1f, 0.02f);
            Invoke(ascent, "UpdateAfterimages", 0.1f, 0.02f);
            Assert.That(Active("sprayMotions") + Active("residualMotions") + Active("afterimageMotions"), Is.Zero);
        }

        [Test]
        public void Ascent_HighToLowRemovesExcessWithoutRebuilding()
        {
            Invoke(ascent, "PrepareMotionStates");
            Tier(VfxQualityTier.Low);
            Invoke(ascent, "UpdateSprays", 0.02f);
            Invoke(ascent, "UpdateResidualDrops", 0.01f, 0.02f);
            Invoke(ascent, "UpdateAfterimages", 0.01f, 0.02f);
            Assert.That(Active("sprayMotions"), Is.EqualTo(VfxQualityRuntime.Profile.ScaleDecorativeCount(24, 8)));
            Assert.That(Active("residualMotions"), Is.EqualTo(VfxQualityRuntime.Profile.ScaleDecorativeCount(18, 6)));
            Assert.That(Active("afterimageMotions"), Is.EqualTo(1));
            Assert.That(ascent.BuiltChildCount, Is.EqualTo(52));
        }

        [Test]
        public void Shield_ShardsStartOnBoundary_StayInWorldWhenPlayerMoves()
        {
            var shield = Shield();
            Invoke(shield, "OnShieldConsumed");
            var shards = Field<SpriteRenderer[]>(shield, "shieldShards");
            Vector3 position = shards[0].transform.position;
            Assert.That(position.magnitude, Is.EqualTo(0.78f).Within(0.001f));
            shield.transform.position = new Vector3(0f, 50f);
            Invoke(shield, "AdvanceShieldShards", 0f);
            Assert.That(Vector3.Distance(shards[0].transform.position, position), Is.LessThan(0.001f));
            Invoke(shield, "AdvanceShieldShards", 0.05f);
            Assert.That(Vector3.Distance(shards[0].transform.position, position), Is.LessThan(0.3f));
        }

        [Test]
        public void Shield_ReducedAndDisableImmediatelyClearTransientState()
        {
            var shield = Shield();
            Invoke(shield, "OnShieldConsumed");
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Invoke(shield, "AdvanceShieldShards", 0f);
            foreach (var shard in Field<SpriteRenderer[]>(shield, "shieldShards")) Assert.That(shard.enabled, Is.False);
            LobbySettingsProfile.SetReducedMotionEnabled(false);
            Invoke(shield, "OnShieldConsumed");
            Set(shield, "shieldWasVisible", true);
            Invoke(shield, "OnDisable");
            Assert.That(Field<bool>(shield, "shieldWasVisible"), Is.False);
            Assert.That(Field<float>(shield, "shieldPulseTime") + Field<float>(shield, "shieldShardTime"), Is.Zero);
            foreach (var renderer in shield.GetComponentsInChildren<Renderer>()) Assert.That(renderer.enabled, Is.False);
        }

        [Test]
        public void Shield_ReusesBoundedRenderersAndKeepsGameplayRandomUntouched()
        {
            var shield = Shield();
            var state = UnityEngine.Random.state;
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.state = state;
            for (int i = 0; i < 50; i++) Invoke(shield, "OnShieldConsumed");
            Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            UnityEngine.Random.state = state;
            Assert.That(Field<SpriteRenderer[]>(shield, "shieldMotes").Length, Is.EqualTo(11));
            Assert.That(Field<SpriteRenderer[]>(shield, "shieldShards").Length, Is.EqualTo(18));
            Assert.That(shield.transform.childCount, Is.EqualTo(32));
        }

        [Test]
        public void Shield_HighToLowDropsExcessShards()
        {
            var shield = Shield();
            Invoke(shield, "OnShieldConsumed");
            Tier(VfxQualityTier.Low);
            Invoke(shield, "AdvanceShieldShards", 0f);
            int visible = 0;
            foreach (var shard in Field<SpriteRenderer[]>(shield, "shieldShards")) if (shard.enabled) visible++;
            Assert.That(visible, Is.EqualTo(VfxQualityRuntime.Profile.ScaleDecorativeCount(18, 4)));
            Assert.That(shield.transform.childCount, Is.EqualTo(32));
        }

        [Test]
        public void Clone_ReducedDuringArrivalRestoresBodyWithoutFinishingPop()
        {
            var body = new GameObject("ArrivalProbe");
            body.transform.SetParent(root.transform);
            var renderer = body.AddComponent<SpriteRenderer>();
            body.AddComponent<Rigidbody2D>();
            body.AddComponent<CircleCollider2D>();
            var player = body.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            var arrival = body.AddComponent<InkCloneArrivalView>();
            Invoke(arrival, "Awake");
            var routine = (System.Collections.IEnumerator)typeof(InkCloneArrivalView)
                .GetMethod("AnimateArrival", Private).Invoke(arrival, null);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(renderer.enabled, Is.False);
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(body.transform.Find("InkCloneArrivalVisual").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(body.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [TestCase(540, 960, 100)]
        [TestCase(1080, 1920, 150)]
        [TestCase(1179, 2556, 102)]
        public void HazardMarker_ClearsHudAndKeepsFallingX(int width, int height, int bottomInset)
        {
            float hudTop = PrototypeHud.CalculateGaugeTopScreenY(
                new Rect(0f, bottomInset, width, height - bottomInset), width, height, RuntimePlatform.IPhonePlayer);
            Assert.That(hudTop, Is.GreaterThan(width * 0.14f));
            Assert.That(hudTop, Is.LessThan(height * 0.25f));
            float markerY = FallingInkRock.CalculateWarningMarkerScreenY(
                new Rect(0f, bottomInset, width, height - bottomInset), width, height,
                RuntimePlatform.IPhonePlayer, 20f);
            Assert.That(markerY - 20f - hudTop, Is.GreaterThanOrEqualTo(Mathf.Max(16f, width * 0.025f) - 0.001f));
            // 위 계산은 입력된 3개 Safe Area, 아래 렌더 경로는 현재 Editor 화면에서 검사한다.
            var cameraObject = new GameObject("WarningCamera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.6f;
            var rockObject = new GameObject("WarningProbe");
            rockObject.transform.SetParent(root.transform);
            rockObject.transform.position = new Vector3(2f, 6f);
            var rock = rockObject.AddComponent<FallingInkRock>();
            Invoke(rock, "Awake");
            rock.Initialize(null, camera, 0, 0.9f, 4f, 9f, 8f, 4f);
            Invoke(rock, "UpdateWarningVisuals", 1f);
            var marker = Field<LineRenderer>(rock, "warningMarker");
            float minY = float.MaxValue, meanX = 0f;
            for (int i = 0; i < marker.positionCount; i++)
            {
                var point = marker.GetPosition(i);
                minY = Mathf.Min(minY, camera.WorldToScreenPoint(point - Vector3.up * 0.0325f).y);
                meanX += point.x;
            }
            Assert.That(minY, Is.GreaterThan(PrototypeHud.CalculateGaugeTopScreenY(
                MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height, Application.platform) + 15.9f));
            Assert.That(meanX / marker.positionCount, Is.EqualTo(2f).Within(0.01f));
            Assert.That(rock.State, Is.EqualTo(FallingInkRockState.Warning));
            Assert.That(rock.GetComponent<CircleCollider2D>().enabled, Is.False);
        }

        ItemEffectView Shield()
        {
            var body = new GameObject("ShieldProbe");
            body.transform.SetParent(root.transform);
            body.AddComponent<Rigidbody2D>();
            body.AddComponent<CircleCollider2D>();
            var player = body.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            var shield = body.AddComponent<ItemEffectView>();
            Invoke(shield, "Awake");
            Invoke(shield, "EnsureShieldVisuals");
            return shield;
        }

        [Test]
        public void Ascent_BrushTailFollowsHighPlayer_AndDoesNotStretchBackToGround()
        {
            var player = new GameObject("AscentAnchor");
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(1f, 50f);
            Set(ascent, "trackedPlayer", player.transform);
            Set(ascent, "maximumStrokeLength", 15f);
            var brush = Field<SpriteRenderer>(ascent, "brush");
            brush.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/MukJump/VFX/InkDropJump/Textures/T_VFX_InkVerticalBrush_256x1024.png");
            Assert.That(brush.sprite, Is.Not.Null);
            Invoke(ascent, "UpdateVerticalStroke", 0.5f);
            Assert.That(brush.bounds.size.y, Is.LessThanOrEqualTo(3.801f));
            Assert.That(brush.bounds.max.y, Is.EqualTo(49.45f).Within(0.01f));
            Assert.That(brush.transform.position.x, Is.EqualTo(1f));
            player.transform.position = new Vector3(1f, 44f);
            Invoke(ascent, "UpdateVerticalStroke", 0.8f);
            Assert.That(brush.bounds.max.y, Is.EqualTo(49.45f).Within(0.01f), "착지 방향으로 꼬리가 역주행하면 안 됩니다.");
        }

        int Active(string name)
        {
            int result = 0;
            foreach (var value in Field<Array>(ascent, name))
                if ((bool)value.GetType().GetField("Active").GetValue(value)) result++;
            return result;
        }

        static void Tier(VfxQualityTier value) => VfxQualityRuntime.SetTier(value, VfxQualityChangeReason.DebugOverride);
        static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
        static void Set(object value, string name, object data) => value.GetType().GetField(name, Private).SetValue(value, data);
        static void Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
    }
}
