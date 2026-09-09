using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Items;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class MobileFeedbackPolishTests
    {
        GameObject root;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            root = new GameObject("MobileFeedbackPolishTests");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void GoldenGauge_EnterExtendExit_EndsAtLiveResourceWithoutJump()
        {
            var hud = root.AddComponent<PrototypeHud>();
            StepGauge(hud, 0.25f, false, 0f);
            StepGauge(hud, 0.25f, true, 0.09f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.InRange(0.6f, 0.65f));
            // 같은 황금 상태 갱신은 보간 시작점을 다시 잡으면 안 된다.
            StepGauge(hud, 0.25f, true, 0.09f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.EqualTo(1f).Within(0.001f));
            StepGauge(hud, 0.25f, true, 1f);
            StepGauge(hud, 0.25f, false, 0f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.EqualTo(1f));
            StepGauge(hud, 0.3f, false, 0.12f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.InRange(0.64f, 0.66f));
            StepGauge(hud, 0.4f, false, 0.12f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(Field<float>(hud, "goldenBlend"), Is.Zero.Within(0.001f));
        }

        [Test]
        public void ReducedGoldenGauge_ChangesWidthImmediately_OnlyCrossfadesColor()
        {
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            var hud = root.AddComponent<PrototypeHud>();
            StepGauge(hud, 0.2f, false, 0f);
            StepGauge(hud, 0.2f, true, 0.07f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.EqualTo(1f));
            Assert.That(Field<float>(hud, "goldenBlend"), Is.InRange(0.49f, 0.51f));
            StepGauge(hud, 0.2f, true, 0.07f);
            StepGauge(hud, 0.35f, false, 0.14f);
            Assert.That(Field<float>(hud, "displayedInkRatio"), Is.EqualTo(0.35f));
            Assert.That(Field<float>(hud, "goldenBlend"), Is.Zero.Within(0.001f));
        }

        [Test]
        public void PickupAbsorption_ReleasesOnce_AndPoolReuseRestoresOriginalArt()
        {
            root.transform.localScale = Vector3.one * 0.8f;
            var pickup = root.AddComponent<ItemPickup>();
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.color = InkPalette.Red;
            pickup.Configure(ItemType.InkShield, 0f);
            int releases = 0;
            pickup.ReleaseRequested += _ => releases++;
            Invoke(pickup, "BeginCollection", root.transform);
            Invoke(pickup, "AdvanceCollection", 0.09f);
            Assert.That(releases, Is.Zero);
            Assert.That(renderer.color.a, Is.InRange(0.49f, 0.51f));
            Assert.That(root.transform.localScale.x, Is.LessThan(0.8f));
            Invoke(pickup, "AdvanceCollection", 0.1f);
            Invoke(pickup, "AdvanceCollection", 0.1f);
            Assert.That(releases, Is.EqualTo(1));
            pickup.OnPoolRelease();
            pickup.OnPoolAcquire();
            Assert.That(renderer.color, Is.EqualTo(InkPalette.Red));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one * 0.8f));
            Assert.That(root.GetComponent<CircleCollider2D>().enabled, Is.True);
            Invoke(pickup, "AdvanceCollection", 1f);
            Assert.That(releases, Is.EqualTo(1), "이전 획득의 반납이 새 대여를 반납하면 안 됩니다.");
        }

        [Test]
        public void SwarmFeedback_24Events_SelectVisibleContactRegardlessOfOrder()
        {
            var feedback = root.AddComponent<GameFeedbackController>();
            var cameraObject = new GameObject("FeedbackTestCamera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            typeof(GameFeedbackController).GetField("feedbackCamera", Private).SetValue(feedback, camera);
            for (int order = 0; order < 2; order++)
            {
                for (int index = 0; index < 24; index++)
                {
                    int i = order == 0 ? index : 23 - index;
                    var position = new Vector3(0f, i == 0 ? -20f : (i - 12) * 0.25f, 0f);
                    feedback.PlayDirectionalJump(position, Vector2.up);
                    feedback.PlayLanding(position, i + 1f);
                }
                Assert.That(Field<Vector3>(feedback, "pendingJumpPosition"), Is.EqualTo(Vector3.zero));
                Assert.That(Field<Vector3>(feedback, "pendingLandingPosition"), Is.EqualTo(Vector3.zero));
                Assert.That(Field<float>(feedback, "pendingLandingStrength"), Is.EqualTo(24f));
                typeof(GameFeedbackController).GetField("jumpPending", Private).SetValue(feedback, false);
                typeof(GameFeedbackController).GetField("landingPending", Private).SetValue(feedback, false);
            }
            feedback.PlayJump(Vector3.zero);
            feedback.enabled = false;
            // 일반 MonoBehaviour는 EditMode에서 enabled 변경만으로 생명주기 메시지를
            // 받지 않는다. 런타임이 호출하는 정리 진입점을 명시해 정리 계약을 검사한다.
            Invoke(feedback, "OnDisable");
            Assert.That(Field<bool>(feedback, "jumpPending"), Is.False);
            Assert.That(feedback.ActiveLineVfxCount, Is.Zero);
        }

        [Test]
        public void ReducedComposite_RemovesMovingDecoration_WithoutRebuildingPool()
        {
            var effect = root.AddComponent<InkDropJumpVfxInstance>();
            effect.Initialize(null, default, 8, 6);
            int childCount = effect.BuiltChildCount;
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Invoke(effect, "PrepareMotionStates");
            Assert.That(ActiveMotionCount(effect, "sprayMotions"), Is.Zero);
            Assert.That(ActiveMotionCount(effect, "residualMotions"), Is.Zero);
            Assert.That(ActiveMotionCount(effect, "afterimageMotions"), Is.Zero);
            effect.OnPoolRelease();
            effect.OnPoolAcquire();
            LobbySettingsProfile.SetReducedMotionEnabled(false);
            Invoke(effect, "PrepareMotionStates");
            Assert.That(ActiveMotionCount(effect, "sprayMotions"), Is.GreaterThan(0));
            Assert.That(ActiveMotionCount(effect, "afterimageMotions"), Is.GreaterThan(0));
            Assert.That(effect.BuiltChildCount, Is.EqualTo(childCount));
        }

        [Test]
        public void SwarmLaunch_FeedbackUsesCameraLeader_NotLowerCollectorOrMedian()
        {
            var manager = root.AddComponent<GameManager>();
            typeof(GameManager).GetField("<State>k__BackingField", Private)
                .SetValue(manager, GameState.Playing);
            PlayerController collector = null;
            PlayerController highest = null;
            for (int i = 0; i < 24; i++)
            {
                var body = new GameObject("LaunchFeedbackProbe");
                body.transform.SetParent(root.transform);
                body.transform.position = new Vector3(0f, i * 2f, 0f);
                body.AddComponent<SpriteRenderer>();
                body.AddComponent<CircleCollider2D>();
                body.AddComponent<Rigidbody2D>();
                var player = body.AddComponent<PlayerController>();
                Invoke(player, "Awake");
                manager.RegisterPlayer(player);
                collector ??= player;
                highest = player;
            }
            Assert.That(manager.LaunchSwarmInkDrop(collector, 50f, out var feedback), Is.True);
            Assert.That(manager.TryGetSwarmCameraFrame(out var cameraLeader, out _, out _), Is.True);
            Assert.That(feedback, Is.SameAs(highest));
            Assert.That(feedback, Is.SameAs(cameraLeader));
            foreach (var player in root.GetComponentsInChildren<PlayerController>())
                Assert.That(player.IsInkDropBoosted, Is.True, "표시 대상을 바꿔도 먹떼 상승은 전원 유지합니다.");
        }

        [Test]
        public void GoldenShader_RecolorsBlackPixels_AndKeepsTransparentPixelsClear()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("금색 실제 픽셀 검증은 GPU가 있는 Unity에서 실행합니다.");
            Shader shader = Resources.Load<Shader>("MukJump/Shaders/InkGaugeTint");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            var material = new Material(shader);
            var source = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            var result = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            var target = RenderTexture.GetTemporary(2, 1, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                source.filterMode = FilterMode.Point;
                source.SetPixels(new[] { Color.black, Color.clear });
                source.Apply();
                material.SetColor("_InkColor", InkPalette.Gold);
                material.SetFloat("_Recolor", 1f);
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(source, target, material);
                result.ReadPixels(new Rect(0, 0, 2, 1), 0, 0);
                Color gold = result.GetPixel(0, 0);
                Assert.That(gold.r, Is.GreaterThan(0.2f), "검정×금색의 곱셈 tint는 검정으로 남습니다.");
                Assert.That(gold.r, Is.GreaterThan(gold.g));
                Assert.That(gold.g, Is.GreaterThan(gold.b));
                Assert.That(result.GetPixel(1, 0).a, Is.LessThan(0.02f));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(result);
            }
        }

        static int ActiveMotionCount(object effect, string name)
        {
            var motions = Field<Array>(effect, name);
            int count = 0;
            foreach (object motion in motions)
                if ((bool)motion.GetType().GetField("Active").GetValue(motion)) count++;
            return count;
        }

        static void StepGauge(PrototypeHud hud, float ratio, bool golden, float delta) =>
            Invoke(hud, "AdvanceGaugePresentation", ratio, golden, delta);
        static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);
        static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}
