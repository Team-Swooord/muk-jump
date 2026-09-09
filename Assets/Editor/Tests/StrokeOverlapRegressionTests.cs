using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class StrokeOverlapRegressionTests
    {
        readonly List<GameObject> created = new();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            PointerInput.ResetSuppressionForTests();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        PlayerController PlayerAt(Vector2 position)
        {
            var go = new GameObject("StrokeOverlapProbe");
            created.Add(go);
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            go.AddComponent<CircleCollider2D>().radius = 0.25f;
            var player = go.AddComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Awake", Private).Invoke(player, null);
            return player;
        }

        PlatformCollider LineAt(float y = 10000f)
        {
            var line = PlatformCollider.Spawn(new List<Vector2>
            {
                new(9998f, y), new(10002f, y)
            });
            created.Add(line.gameObject);
            return line;
        }

        static void Refresh(PlatformCollider line) => typeof(PlatformCollider)
            .GetMethod("RefreshDeferredContacts", Private).Invoke(line, null);

        static bool Ignored(PlatformCollider line, PlayerController player) =>
            Physics2D.GetIgnoreCollision(line.GetComponent<EdgeCollider2D>(), player.PrimaryCollider);

        [Test]
        public void CrossingLineSurvivesAndOnlyOverlappingPlayerIsDeferred()
        {
            var overlapping = PlayerAt(new(10000f, 10000f));
            var clear = PlayerAt(new(10000f, 10002f));
            var line = LineAt();
            line.DeferInitialPlayerContacts(new[] { overlapping, clear });
            Assert.That(Ignored(line, overlapping), Is.True);
            Assert.That(Ignored(line, clear), Is.False);
            Assert.That(line.Line.enabled, Is.True);
            Assert.That(line.GetComponent<EdgeCollider2D>().enabled, Is.True);
            Assert.That(line.Length, Is.EqualTo(4f).Within(0.001f));
            Assert.That(line.RetainedInkCost, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void SwarmContactsRearmIndividuallyOnlyAfterSeparation()
        {
            var players = new List<PlayerController>();
            for (int i = 0; i < 24; i++) players.Add(PlayerAt(new(10000f, 10000f)));
            var line = LineAt();
            line.DeferInitialPlayerContacts(players);
            for (int i = 0; i < 24; i++) Assert.That(Ignored(line, players[i]), Is.True);
            for (int i = 0; i < 150; i++) Refresh(line);
            Assert.That(Ignored(line, players[1]), Is.True, "시간 경과만으로 겹친 몸체를 밀면 안 됩니다.");
            players[0].Body.position += Vector2.up;
            Physics2D.SyncTransforms();
            Refresh(line);
            Assert.That(Ignored(line, players[0]), Is.False);
            Assert.That(Ignored(line, players[1]), Is.True);
            Assert.That(line.Length, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void SeparationCanRestoreContactBelowTheLineWithoutDeletingIt()
        {
            var player = PlayerAt(new(10000f, 10000f));
            var line = LineAt();
            line.DeferInitialPlayerContacts(new[] { player });
            player.Body.position -= Vector2.up;
            Physics2D.SyncTransforms();
            Refresh(line);
            Assert.That(Ignored(line, player), Is.False);
            Assert.That(line.IsOneWayPlatform, Is.True);
            Assert.That(line.Line.positionCount, Is.EqualTo(2));
        }

        [Test]
        public void VisualOnlyOverlapDoesNotDisableAUsablePlatform()
        {
            var player = PlayerAt(new(10000f, 10001f));
            // 물리 몸체와 무관한 큰 렌더 영역은 검사하지 않는다.
            var visual = player.gameObject.AddComponent<SpriteRenderer>();
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 1f);
            visual.sprite = sprite;
            try
            {
                var line = LineAt();
                line.DeferInitialPlayerContacts(new[] { player });
                Assert.That(Ignored(line, player), Is.False);
                Assert.That(line.Length, Is.EqualTo(4f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void DisabledComponentReleasesOnlyItsOwnIgnoredPair()
        {
            var player = PlayerAt(new(10000f, 10000f));
            var line = LineAt();
            line.DeferInitialPlayerContacts(new[] { player });
            line.enabled = false;
            typeof(PlatformCollider).GetMethod("OnDisable", Private).Invoke(line, null);
            Assert.That(Ignored(line, player), Is.False);
        }

        [Test]
        public void ExistingExternalIgnoreIsNotOwnedOrRestoredByTheLine()
        {
            var player = PlayerAt(new(10000f, 10000f));
            var line = LineAt();
            var edge = line.GetComponent<EdgeCollider2D>();
            Physics2D.IgnoreCollision(edge, player.PrimaryCollider, true);
            line.DeferInitialPlayerContacts(new[] { player });
            player.Body.position += Vector2.up;
            Physics2D.SyncTransforms();
            Refresh(line);
            line.enabled = false;
            typeof(PlatformCollider).GetMethod("OnDisable", Private).Invoke(line, null);
            Assert.That(Physics2D.GetIgnoreCollision(edge, player.PrimaryCollider), Is.True);
        }

        [Test]
        public void DisablingBodyOrDestroyingPlayerDoesNotLeavePendingPairs()
        {
            var player = PlayerAt(new(10000f, 10000f));
            var line = LineAt();
            line.DeferInitialPlayerContacts(new[] { player });
            player.PrimaryCollider.enabled = false;
            Refresh(line);
            player.PrimaryCollider.enabled = true;
            Assert.That(Ignored(line, player), Is.False);
            line.DeferInitialPlayerContacts(new[] { player });
            Object.DestroyImmediate(player.gameObject);
            Assert.DoesNotThrow(() => Refresh(line));
            var pending = (List<Collider2D>)typeof(PlatformCollider)
                .GetField("deferredContacts", Private).GetValue(line);
            Assert.That(pending, Is.Empty);
        }

        [Test]
        public void FinalizingAcrossRegisteredPlayerPreservesLineAndAutomaticallyDefersContact()
        {
            GameManager previous = GameManager.Instance;
            var managerObject = new GameObject("StrokeManagerProbe");
            created.Add(managerObject);
            var manager = managerObject.AddComponent<GameManager>();
            var instance = typeof(GameManager).GetProperty(nameof(GameManager.Instance));
            instance.SetValue(null, manager);
            try
            {
                var player = PlayerAt(new(10000f, 10000f));
                manager.RegisterPlayer(player);
                var captureObject = new GameObject("CrossingStrokeCapture");
                created.Add(captureObject);
                var capture = captureObject.AddComponent<StrokeCapture>();
                typeof(StrokeCapture).GetMethod("BeginStrokeAtWorld", Private)
                    .Invoke(capture, new object[] { new Vector2(9999.5f, 10000f) });
                created.Add(((LineRenderer)typeof(StrokeCapture).GetField("preview", Private)
                    .GetValue(capture)).gameObject);
                typeof(StrokeCapture).GetMethod("AppendWorldSample", Private)
                    .Invoke(capture, new object[] { new Vector2(10000.5f, 10000f) });
                PlatformCollider made = null;
                capture.ValidStrokeCreated += (platform, _, _) => made = platform;
                typeof(StrokeCapture).GetMethod("EndStroke", Private).Invoke(capture, null);
                if (made != null) created.Add(made.gameObject);
                Assert.That(made, Is.Not.Null, "이전 겹침 필터에서는 짧은 두 조각이 모두 취소되던 획입니다.");
                Assert.That(made.Length, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Ignored(made, player), Is.True);
            }
            finally { instance.SetValue(null, previous); }
        }

        [Test]
        public void FinalShortTailIsRetainedBeforeMinimumLengthValidation()
        {
            var go = new GameObject("ReleaseTailCapture");
            created.Add(go);
            var capture = go.AddComponent<StrokeCapture>();
            typeof(StrokeCapture).GetMethod("BeginStrokeAtWorld", Private)
                .Invoke(capture, new object[] { new Vector2(10000f, 10000f) });
            var preview = (LineRenderer)typeof(StrokeCapture).GetField("preview", Private).GetValue(capture);
            created.Add(preview.gameObject);
            typeof(StrokeCapture).GetMethod("AppendWorldSample", Private)
                .Invoke(capture, new object[] { new Vector2(10000.5f, 10000f) });
            typeof(StrokeCapture).GetMethod("AppendWorldSampleWithTail", Private)
                .Invoke(capture, new object[] { new Vector2(10000.64f, 10000f), true });
            PlatformCollider made = null;
            capture.ValidStrokeCreated += (platform, _, _) => made = platform;
            typeof(StrokeCapture).GetMethod("EndStroke", Private).Invoke(capture, null);
            if (made != null) created.Add(made.gameObject);
            Assert.That(made, Is.Not.Null, "해제 프레임의 0.14m도 유효 길이에 포함해야 합니다.");
            Assert.That(made.Length, Is.GreaterThan(0.63f));
        }

        [TestCase(0.61f)]
        [TestCase(1.21f)]
        public void SmoothingPreservesTheActualReleaseEndpoint(float length)
        {
            var points = BezierSmoother.Smooth(new[] { Vector2.zero, Vector2.right * length });
            Assert.That(points[^1].x, Is.EqualTo(length).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator ReleasedPointerKeepsFinalPositionInThePlayerInputLoop()
        {
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            // 도메인 리로드가 끝난 뒤 콜백 클로저를 생성한다.
            yield return CheckReleasedPointerInPlayerLoop();
            yield return new ExitPlayMode();
            Application.runInBackground = previousBackground;
        }

        static IEnumerator CheckReleasedPointerInPlayerLoop()
        {
            EditorApplication.isPaused = false;
            PointerInput.ResetSuppressionForTests();
            Mouse previous = Mouse.current;
            var mouse = InputSystem.AddDevice<Mouse>();
            bool mouseReleased = false, suppressionWorked = false;
            Vector2 mouseEnd = default;
            try
            {
                _ = mouse.leftButton.wasReleasedThisFrame;
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = new(50, 50) }.WithButton(MouseButton.Left)));
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(mouse,
                    new MouseState { position = new(170, 80) }), () =>
                {
                    mouse.MakeCurrent();
                    mouseReleased = PointerInput.TryGetReleased(out mouseEnd);
                    PointerInput.SuppressUntilRelease();
                    suppressionWorked = !PointerInput.TryGetReleased(out _);
                });
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                previous?.MakeCurrent();
                PointerInput.ResetSuppressionForTests();
            }
            Touchscreen previousTouch = Touchscreen.current;
            var touch = InputSystem.AddDevice<Touchscreen>();
            bool touchReleased = false, cancellationIgnored = false;
            Vector2 touchEnd = default;
            try
            {
                _ = touch.primaryTouch.press.wasReleasedThisFrame;
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = new(50, 50) }));
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new(170, 80) }), () =>
                {
                    touch.MakeCurrent();
                    touchReleased = PointerInput.TryGetReleased(out touchEnd);
                });
                yield return ProcessPlayerInput(() => { });
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = new(20, 20) }));
                yield return ProcessPlayerInput(() => InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2,
                    phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = new(80, 50) }), () =>
                {
                    touch.MakeCurrent();
                    cancellationIgnored = !PointerInput.TryGetReleased(out _);
                });
            }
            finally { InputSystem.RemoveDevice(touch); previousTouch?.MakeCurrent(); }
            Assert.That(mouseReleased, Is.True, "마우스 해제 좌표");
            Assert.That(mouseEnd, Is.EqualTo(new Vector2(170, 80)));
            Assert.That(suppressionWorked, Is.True);
            Assert.That(touchReleased, Is.True, "실행 중 터치 해제 좌표");
            Assert.That(touchEnd, Is.EqualTo(new Vector2(170, 80)));
            Assert.That(cancellationIgnored, Is.True);
        }

        // 실제 PlayerLoop를 쓰는 검사에서 InputSystem.Update를 따로 호출하면
        // 에디터·Device Simulator의 네이티브 이벤트 버퍼까지 중복 처리한다.
        // 엔진이 다음 Dynamic 입력 프레임을 마친 시점에만 결과를 읽는다.
        static IEnumerator ProcessPlayerInput(System.Action queue, System.Action sample = null)
        {
            bool processed = false;
            void AfterUpdate()
            {
                if (processed || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                sample?.Invoke();
                processed = true;
            }
            InputSystem.onAfterUpdate += AfterUpdate;
            try
            {
                queue();
                double deadline = EditorApplication.timeSinceStartup + 5d;
                while (!processed && EditorApplication.timeSinceStartup < deadline)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;
                }
                Assert.That(processed, Is.True, "실제 입력 프레임이 처리되어야 합니다.");
            }
            finally { InputSystem.onAfterUpdate -= AfterUpdate; }
        }

        [UnityTest]
        public IEnumerator OverlappingSpawnDoesNotPushBodyAndCatchesAfterReentry()
        {
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;
            var scene = SceneManager.CreateScene("DeferredStrokePhysics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var previousScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var playerObject = new GameObject("DeferredContactPhysicsProbe");
            playerObject.layer = LayerMask.NameToLayer("Player");
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerObject.AddComponent<CircleCollider2D>().radius = 0.25f;
            var player = playerObject.AddComponent<PlayerController>();
            player.enabled = false;
            var line = PlatformCollider.Spawn(new List<Vector2> { new(-2, 0), new(2, 0) });
            line.DeferInitialPlayerContacts(new[] { player });
            Physics2D.SyncTransforms();
            var physics = scene.GetPhysicsScene2D();
            bool simulationOk = true;
            for (int i = 0; i < 8; i++) simulationOk &= physics.Simulate(0.02f);
            float pushDistance = body.position.magnitude;
            bool ignoredAtSpawn = Ignored(line, player);
            body.linearVelocity = Vector2.up * 3f;
            for (int i = 0; i < 25; i++)
            {
                Refresh(line);
                simulationOk &= physics.Simulate(0.02f);
            }
            bool restored = !Ignored(line, player);
            body.linearVelocity = Vector2.down * 3f;
            bool landed = false;
            for (int i = 0; i < 35; i++)
            {
                Refresh(line);
                simulationOk &= physics.Simulate(0.02f);
                if (body.position.y > 0 && Mathf.Abs(body.linearVelocity.y) < 0.05f) { landed = true; break; }
            }
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
            Application.runInBackground = previousBackground;
            Assert.That(simulationOk, Is.True);
            Assert.That(ignoredAtSpawn, Is.True);
            Assert.That(pushDistance, Is.LessThan(0.001f), "겹친 먹선 생성으로 몸체를 밀어내면 안 됩니다.");
            Assert.That(restored, Is.True);
            Assert.That(landed, Is.True, "빠져나왔다가 내려오면 같은 선을 다시 밟아야 합니다.");
        }
    }
}
