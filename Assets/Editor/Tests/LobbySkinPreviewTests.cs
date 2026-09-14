using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class LobbySkinPreviewTests
    {
        [TestCase(30), TestCase(60), TestCase(120), Category("LobbyDrift")]
        public void AutomaticFloatKeepsSpeedAndVisitsBothDirectionsWithoutGravity(int fps)
        {
            var random = Random.state;
            Vector2 position = Vector2.zero, speed = new Vector2(3, 4).normalized * LobbySkinPreview.DriftSpeed;
            Vector2 limits = new(90, 170);
            bool up = false, down = false, left = false, right = false;
            for (int i = 0; i < fps * 60; i++)
            {
                LobbySkinPreview.AdvanceFloat(ref position, ref speed, limits, 1f / fps);
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x));
                Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limits.y));
                Assert.That(speed.magnitude, Is.EqualTo(LobbySkinPreview.DriftSpeed).Within(.001f));
                up |= speed.y > 0; down |= speed.y < 0; left |= speed.x < 0; right |= speed.x > 0;
            }
            Assert.That(up && down && left && right, Is.True);
            Assert.That(Random.state, Is.EqualTo(random), "로비 부유가 게임 난수를 소비하지 않는다.");
        }

        [TestCase(30), TestCase(60), TestCase(120), Category("LobbyDrift")]
        public void AutomaticFloatHasNoDownwardAccelerationAndIsFrameRateIndependent(int fps)
        {
            Vector2 position = Vector2.zero, speed = new Vector2(3, 4).normalized * LobbySkinPreview.DriftSpeed;
            Vector2 expected = speed;
            for (int i = 0; i < fps; i++)
                LobbySkinPreview.AdvanceFloat(ref position, ref speed, Vector2.one * 100, 1f / fps);
            Assert.That(Vector2.Distance(position, expected), Is.LessThan(.02f));
            Assert.That(Vector2.Distance(speed, expected), Is.LessThan(.001f));
        }

        [Test, Category("LobbyDrift")]
        public void AutomaticFloatLeavesFloorAndReflectsLongFramesWithoutLeavingCostumeBounds()
        {
            Vector2 limit = LobbySkinPreview.RotationTravel(new Vector2(250, 500), new Vector2(160, 240), 65f);
            Vector2 position = new(0, -limit.y), speed = Vector2.zero;
            LobbySkinPreview.AdvanceFloat(ref position, ref speed, limit, .1f);
            Assert.That(position.y, Is.GreaterThan(-limit.y));
            Assert.That(speed.y, Is.GreaterThan(0));
            LobbySkinPreview.AdvanceFloat(ref position, ref speed, limit, 1000f);
            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limit.x));
            Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limit.y));
            LobbySkinPreview.AdvanceFloat(ref position, ref speed, Vector2.zero, 1f);
            Assert.That(position, Is.EqualTo(Vector2.zero));
            Assert.That(speed, Is.EqualTo(Vector2.zero));
        }

        [UnityTest, Category("LobbyDrift"), Timeout(120000)]
        public IEnumerator UntouchedLobbyFloatsAndSettledReleaseResumesWithoutGrantingCushionReward()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                Assert.That(preview.IsVisible, Is.True);
                Vector2 speed = preview.Velocity, before = preview.Drift.anchoredPosition;
                yield return Wait(1.5f);
                Assert.That(Vector2.Distance(preview.Velocity, speed), Is.LessThan(.01f));
                Assert.That(preview.Velocity.magnitude, Is.EqualTo(LobbySkinPreview.DriftSpeed).Within(.01f));
                Assert.That(Vector2.Distance(before, preview.Drift.anchoredPosition), Is.GreaterThan(10f));

                // 바닥에 잡아 놓은 투척이 끝나도 그 자리에 굳지 않고 다시 부유한다.
                var e = new PointerEventData(EventSystem.current) { pointerId = 42,
                    position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                preview.OnPointerDown(e);
                Assert.That(preview.IsDragging, Is.True);
                e.position = ScreenPoint(preview, new Vector2(0, -preview.Travel.y));
                preview.OnDrag(e);
                yield return Wait(.15f);
                preview.OnPointerUp(e);
                yield return Wait(.8f);
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(preview.Velocity.y, Is.GreaterThan(0f));
                Assert.That(preview.Drift.anchoredPosition.y, Is.GreaterThan(-preview.Travel.y + 5f));
                Assert.That(preview.Velocity.magnitude, Is.EqualTo(LobbySkinPreview.DriftSpeed).Within(.01f));
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.False);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                CheckBounds(preview);
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }

        [TestCase(-1), TestCase(1), Category("LobbyTumble")]
        public void SlantedWallImpactSpinsInContactDirectionAndKeepsGravity(int side)
        {
            Vector2 arena = new(300, 600), size = new(120, 160);
            Vector2 position = new(side * 240, 50), speed = new(side * 700, -360);
            float angle = 0f, spin = 0f;
            int contacts = LobbySkinPreview.AdvanceTumble(ref position, ref speed, ref angle, ref spin, arena, size, .2f);
            Assert.That(contacts, Is.EqualTo(1));
            Assert.That(speed.x * side, Is.LessThan(0));
            Assert.That(speed.y, Is.LessThan(-360));
            Assert.That(spin * side, Is.GreaterThan(40));
            Assert.That(angle * side, Is.GreaterThan(5));
            var limits = LobbySkinPreview.RotationTravel(arena, size, angle);
            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x));
        }

        [Test, Category("LobbyTumble")]
        public void ThrowSpinUsesGripOffsetAndDoesNotInventSpinForCenteredGrip()
        {
            var size = new Vector2(120, 160);
            Assert.That(LobbySkinPreview.ReleaseSpin(Vector2.zero, new Vector2(700, 300), size), Is.Zero);
            float above = LobbySkinPreview.ReleaseSpin(new Vector2(0, 50), new Vector2(600, 0), size);
            float below = LobbySkinPreview.ReleaseSpin(new Vector2(0, -50), new Vector2(600, 0), size);
            Assert.That(above, Is.LessThan(0));
            Assert.That(below, Is.EqualTo(-above));
            Assert.That(LobbySkinPreview.ReleaseSpin(new Vector2(100, 100), Vector2.up * 10000, size), Is.EqualTo(LobbySkinPreview.MaxSpin));
        }

        [TestCase(-1), TestCase(1), Category("LobbyTumble")]
        public void GroundRollFollowsTravelWithoutExceedingSpinCap(int direction)
        {
            Vector2 arena = new(10000, 500), size = new(40, 40);
            Vector2 position = new(0, -480), speed = new(direction * LobbySkinPreview.MaxThrowSpeed, 0);
            float angle = 0, spin = 0;
            for (int i = 0; i < 60; i++)
            {
                LobbySkinPreview.AdvanceTumble(ref position, ref speed, ref angle, ref spin, arena, size, 1f / 120f);
                Assert.That(spin * direction, Is.LessThan(0));
                Assert.That(Mathf.Abs(spin), Is.LessThanOrEqualTo(LobbySkinPreview.MaxSpin));
            }
        }

        [TestCase(30), TestCase(60), TestCase(120), Category("LobbyTumble")]
        public void FreeRotationHasNoUprightClampAndIsFrameRateIndependent(int fps)
        {
            Vector2 position = Vector2.zero, speed = new(200, 500), arena = Vector2.one * 10000, size = new(120, 160);
            float angle = 70, spin = 420;
            for (int i = 0; i < fps; i++)
                LobbySkinPreview.AdvanceTumble(ref position, ref speed, ref angle, ref spin, arena, size, 1f / fps);
            float rotation = 70f + 420f * (1f - Mathf.Exp(-.65f)) / .65f;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(angle, rotation)), Is.LessThan(.02f));
            Assert.That(spin, Is.EqualTo(420f * Mathf.Exp(-.65f)).Within(.02f));
        }

        [Test, Category("LobbyTumble")]
        public void RotatingCostumeStaysInsideWallsAndSettlesWithoutRegainingEnergy()
        {
            Vector2 position = Vector2.zero, speed = new(1000, -400), arena = new(250, 500), size = new(160, 240);
            float angle = 35, spin = 360;
            var random = Random.state;
            for (int i = 0; i < 2400; i++)
            {
                LobbySkinPreview.AdvanceTumble(ref position, ref speed, ref angle, ref spin, arena, size, 1f / 120f);
                Vector2 limits = LobbySkinPreview.RotationTravel(arena, size, angle);
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x + .001f));
                Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limits.y + .001f));
                Assert.That(Mathf.Abs(spin), Is.LessThanOrEqualTo(LobbySkinPreview.MaxSpin));
            }
            Assert.That(speed, Is.EqualTo(Vector2.zero));
            Assert.That(spin, Is.Zero);
            float settledAngle = angle;
            Assert.That(LobbySkinPreview.AdvanceTumble(ref position, ref speed, ref angle, ref spin, arena, size, 3f), Is.Zero);
            Assert.That(angle, Is.EqualTo(settledAngle));
            Assert.That(Random.state, Is.EqualTo(random));
        }

        [Test, Category("LobbyDrift"), Category("LobbyThrow")]
        public void GentleGravitySettlesWithoutTouchingGameRandomOrChatteringOnFloor()
        {
            var state = Random.state;
            Vector2 position = Vector2.zero, velocity = new Vector2(3, 4).normalized * LobbySkinPreview.DriftSpeed;
            Vector2 limits = new(100, 200);
            int contacts = 0;
            for (int i = 0; i < 1200; i++)
            {
                contacts += LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1f / 60f);
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x));
                Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limits.y));
            }
            Assert.That(position.y, Is.EqualTo(-limits.y));
            Assert.That(velocity, Is.EqualTo(Vector2.zero));
            Assert.That(contacts, Is.LessThan(3), "단순 낙하의 작은 반동으로 쓰리쿠션이 달성되면 안 된다.");
            var resting = position;
            Assert.That(LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 20f), Is.Zero);
            Assert.That(position, Is.EqualTo(resting));
            Assert.That(Random.state, Is.EqualTo(state));
        }

        [Test, Category("LobbyDrift"), Category("LobbyThrow")]
        public void WallCornerAndLongFrameReflectWithoutEscaping()
        {
            Vector2 position = new(9, 19), velocity = new(10, 10), limits = new(10, 20);
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, .2f);
            Assert.That(position.x, Is.LessThan(10));
            Assert.That(velocity.x, Is.InRange(-6f, 0f));
            Assert.That(velocity.y, Is.LessThan(0f));
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 100f);
            Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(10));
            Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(20));
            limits = Vector2.zero;
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1);
            Assert.That(position, Is.EqualTo(Vector2.zero));
            limits = Vector2.one * 10; position = new Vector2(-10, 10); velocity = new(-5, 5);
            LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, .01f);
            Assert.That(velocity.x, Is.GreaterThan(0));
            Assert.That(velocity.y, Is.LessThan(0));
        }

        [TestCase(30), TestCase(60), TestCase(120), Category("LobbyThrow")]
        public void ThrowKeepsHandDirectionAndSpeedAcrossFrameRates(int fps)
        {
            foreach (float multiplier in new[] { .25f, 1f, 2f })
            {
                var tracker = new LobbySkinPreview.ThrowTracker();
                Vector2 handSpeed = new Vector2(360, -240) * multiplier;
                tracker.Reset(Vector2.zero, 0);
                for (int frame = 1; frame <= fps; frame++)
                {
                    double time = frame / (double)fps;
                    tracker.Record(handSpeed * (float)time, time);
                }
                Assert.That(Vector2.Distance(tracker.ReleaseVelocity, handSpeed), Is.LessThan(.002f));
            }
        }

        [Test, Category("LobbyThrow")]
        public void ThrowUsesLastDirectionNotTheWholeDragAndStopsAfterHoldingStill()
        {
            var tracker = new LobbySkinPreview.ThrowTracker();
            tracker.Reset(Vector2.zero, 0);
            tracker.Record(new Vector2(100, 0), .1);
            tracker.Record(new Vector2(200, 0), .2);
            tracker.Record(new Vector2(150, 30), .25);
            tracker.Record(new Vector2(100, 60), .3);
            Assert.That(Vector2.Distance(tracker.ReleaseVelocity, new Vector2(-1000, 600)), Is.LessThan(.002f));
            tracker.Record(new Vector2(100, 60), .35);
            tracker.Record(new Vector2(100, 60), .4);
            Assert.That(tracker.ReleaseVelocity, Is.EqualTo(Vector2.zero));
            tracker.Record(new Vector2(100, 60), 10);
            tracker.Record(new Vector2(150, 60), 10.05);
            Assert.That(tracker.ReleaseVelocity.x, Is.GreaterThan(600), "잡아 둔 전체 시간이 새 던지기를 느리게 만들지 않는다.");
        }

        [Test, Category("LobbyThrow")]
        public void ThrowDeduplicatesReleaseFrameCapsSpikesAndClearsPreviousGrab()
        {
            var tracker = new LobbySkinPreview.ThrowTracker();
            tracker.Reset(Vector2.zero, 10);
            tracker.Record(Vector2.one, 10);
            Assert.That(tracker.ReleaseVelocity, Is.EqualTo(Vector2.zero), "시간이 없는 탭은 던지기가 아니다.");
            tracker.Record(new Vector2(31, 41), 10.05);
            var speed = tracker.ReleaseVelocity;
            tracker.Record(new Vector2(31, 41), 10.05);
            Assert.That(tracker.ReleaseVelocity, Is.EqualTo(speed), "같은 프레임의 정지 표본으로 던지기를 지우지 않는다.");
            tracker.Record(new Vector2(30000, 40000), 10.06);
            Assert.That(tracker.ReleaseVelocity.magnitude, Is.EqualTo(LobbySkinPreview.MaxThrowSpeed).Within(.001f));
            tracker.Reset(Vector2.zero, 20);
            tracker.Record(Vector2.zero, 20.2);
            Assert.That(tracker.ReleaseVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test, Category("LobbyThrow")]
        public void FastThrowLosesEnergyAtWallsAndEventuallyComesToRest()
        {
            Vector2 initial = new Vector2(3, -4).normalized * LobbySkinPreview.MaxThrowSpeed;
            Vector2 position = Vector2.zero, velocity = initial, limits = new(90, 170);
            float firstSideSpeed = 0f;
            for (int i = 0; i < 1800; i++)
            {
                float previousX = velocity.x;
                LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1f / 120f);
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(limits.x));
                Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(limits.y));
                Assert.That(Mathf.Abs(velocity.x), Is.LessThanOrEqualTo(Mathf.Abs(previousX) + .001f));
                if (firstSideSpeed == 0f && velocity.x < 0f) firstSideSpeed = Mathf.Abs(velocity.x);
            }
            Assert.That(firstSideSpeed, Is.InRange(1f, Mathf.Abs(initial.x) * LobbySkinPreview.Restitution));
            Assert.That(velocity, Is.EqualTo(Vector2.zero));
            Assert.That(position.y, Is.EqualTo(-limits.y));
        }

        [TestCase(30), TestCase(60), TestCase(120), Category("LobbyThrow")]
        public void GravityAndDragAreConsistentAcrossFrameRates(int fps)
        {
            Vector2 start = new(400, 250), limits = Vector2.one * 10000;
            Vector2 position = Vector2.zero, velocity = start;
            for (int i = 0; i < fps; i++) LobbySkinPreview.AdvanceBounce(ref position, ref velocity, limits, 1f / fps);
            float decay = Mathf.Exp(-LobbySkinPreview.AirDrag);
            float travel = (1f - decay) / LobbySkinPreview.AirDrag;
            Vector2 expected = new(start.x * travel,
                (start.y + LobbySkinPreview.Gravity / LobbySkinPreview.AirDrag) * travel - LobbySkinPreview.Gravity / LobbySkinPreview.AirDrag);
            Assert.That(Vector2.Distance(position, expected), Is.LessThan(.1f));
            Assert.That(velocity.x, Is.EqualTo(start.x * decay).Within(.05f));
            Assert.That(velocity.y, Is.EqualTo(start.y * decay - LobbySkinPreview.Gravity * travel).Within(.05f));
        }

        [UnityTest, Category("LobbyThrow"), Timeout(120000)]
        public IEnumerator TouchReleaseFlingsThenRegrabStopsWithoutStartingGame()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Touchscreen touch = null, previousTouch = Touchscreen.current;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var body = preview.transform.Find("Drift/Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                touch = InputSystem.AddDevice<Touchscreen>();
                Vector2 origin = preview.Drift.anchoredPosition;
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Began, ScreenPoint(preview, origin));
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(preview.Velocity, Is.EqualTo(Vector2.zero));
                for (int i = 1; i <= 3; i++)
                    yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved,
                        ScreenPoint(preview, origin + new Vector2(30, 12) * i));
                // 마지막 위치가 Ended에만 실려도 놓은 방향과 속도가 반영되어야 한다.
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Ended,
                    ScreenPoint(preview, origin + new Vector2(120, 48)));
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(preview.Velocity.x, Is.GreaterThan(0));
                Assert.That(preview.Velocity.y, Is.GreaterThan(0));
                Assert.That(preview.Velocity.magnitude, Is.GreaterThan(LobbySkinPreview.DriftSpeed * 2));
                Assert.That(preview.Velocity.magnitude, Is.LessThanOrEqualTo(LobbySkinPreview.MaxThrowSpeed + .01f));
                Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                Vector2 before = preview.Drift.anchoredPosition, released = preview.Velocity;
                // EditMode 테스트의 yield 4회가 Player 프레임 4회를 보장하지 않는다.
                int targetFrame = Time.frameCount + 4;
                float movementDeadline = Time.realtimeSinceStartup + 3f;
                while (Time.frameCount < targetFrame && Time.realtimeSinceStartup < movementDeadline) yield return null;
                Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(targetFrame));
                Vector2 movement = preview.Drift.anchoredPosition - before;
                Assert.That(Vector2.Dot(movement.normalized, released.normalized), Is.GreaterThan(.99f));
                Assert.That(preview.Velocity.x, Is.LessThan(released.x));
                Assert.That(preview.Velocity.y, Is.LessThan(released.y));

                // 재포획과 정지 후 놓기: 실제 UI 이벤트 경로에서도 예전 던지기가 되살아나지 않는다.
                var e = new PointerEventData(EventSystem.current) { pointerId = 41, position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                preview.OnPointerDown(e);
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(preview.Velocity, Is.EqualTo(Vector2.zero));
                before = preview.Drift.anchoredPosition;
                yield return Wait(.15f);
                preview.OnPointerUp(e); preview.OnEndDrag(e);
                yield return Wait(.15f);
                Assert.That(preview.Velocity.x, Is.Zero);
                Assert.That(preview.Velocity.y, Is.LessThan(0));
                Assert.That(preview.Drift.anchoredPosition.x, Is.EqualTo(before.x));
                Assert.That(preview.Drift.anchoredPosition.y, Is.LessThan(before.y), "정지해서 놓아도 이전 투척 없이 중력만 작용한다.");

                // 시작 버튼 위에 놓아도 게임을 시작하지 않고, 다음 일반 탭은 여전히 버튼이 받는다.
                // 던진 뒤 메뉴/로고 밑으로 들어갈 수 있으므로 다시 빈 로비 영역에서 잡는다. 메뉴 입력 우선은 유지한다.
                preview.Drift.anchoredPosition = origin;
                Canvas.ForceUpdateCanvases();
                yield return CheckRaycast(ScreenPoint(preview, origin), hit => Assert.That(hit, Is.EqualTo(preview.Drift.gameObject)));
                yield return Touch(touch, 2, UnityEngine.InputSystem.TouchPhase.Began, ScreenPoint(preview, origin));
                Assert.That(preview.IsDragging, Is.True);
                Vector2 startPoint = RectTransformUtility.WorldToScreenPoint(null, lobby.StartButton.transform.position);
                yield return Touch(touch, 2, UnityEngine.InputSystem.TouchPhase.Ended, startPoint);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(preview.IsDragging, Is.False);
                CheckTouchTargets(preview); CheckBounds(preview);

                // 백그라운드/모션 줄이기는 던지기를 취소한다.
                e.position = ScreenPoint(preview, preview.Drift.anchoredPosition); preview.OnPointerDown(e);
                yield return null;
                e.position += Vector2.one * 30; preview.OnDrag(e);
                MobileApplicationLifecycle.SetPlatformVisibility(false); MobileApplicationLifecycle.SetPlatformVisibility(true);
                preview.OnPointerUp(e);
                Assert.That(preview.Velocity, Is.EqualTo(Vector2.zero));
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                e.position = ScreenPoint(preview, preview.Drift.anchoredPosition); preview.OnPointerDown(e);
                yield return null;
                e.position += Vector2.one * 30; preview.OnDrag(e); preview.OnPointerUp(e);
                before = preview.Drift.anchoredPosition;
                yield return Wait(.15f);
                Assert.That(preview.Velocity, Is.EqualTo(Vector2.zero));
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(before));
                LobbySettingsProfile.SetReducedMotionEnabled(false); yield return Wait(.15f);
                Assert.That(preview.Velocity.x, Is.Zero);
                Assert.That(preview.Velocity.y, Is.LessThan(0));
            }
            finally
            {
                if (touch != null) InputSystem.RemoveDevice(touch);
                previousTouch?.MakeCurrent();
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Category("LobbyTumble"), Timeout(120000)]
        public IEnumerator LobbyDriftTouchesBounceAndPauseWithoutStartingGame()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Touchscreen touch = null, previousTouch = Touchscreen.current;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var body = preview.transform.Find("Drift/Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                Assert.That(body.GetComponents<UnityEngine.UI.BaseMeshEffect>(), Is.Empty, "본체 메시 변형을 사용하지 않는다.");
                Vector2 clearOrigin = preview.Drift.anchoredPosition;
                Assert.That(preview.Area.parent.name, Is.EqualTo("SafeAreaRoot"));
                var safe = (RectTransform)preview.Area.parent;
                Assert.That(preview.Area.rect.width, Is.EqualTo(safe.rect.width).Within(.01));
                Assert.That(preview.Area.rect.height, Is.GreaterThan(safe.rect.height * .7f));
                Assert.That(preview.Area.anchoredPosition.y + preview.Area.rect.yMax,
                    Is.LessThanOrEqualTo(safe.rect.yMax - MobileUiLayout.ReferenceHeight * LobbyAdLayout.TopInsetFraction + .1f));
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                foreach (var id in new[] { InkSkinId.Ink, InkSkinId.RedHeadband, InkSkinId.Astronaut, InkSkinId.RedShoes, InkSkinId.CraneWings, InkSkinId.DragonTail })
                {
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    Assert.That(body.rectTransform.rect.width, Is.LessThanOrEqualTo(360f * .85f));
                    Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                    Assert.That(body.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(4));
                    Assert.That(preview.Drift.localRotation.x, Is.Zero);
                    Assert.That(preview.Drift.localRotation.y, Is.Zero);
                    Assert.That(preview.Drift.localScale, Is.EqualTo(Vector3.one));
                }
                InkSkinPreferences.TryEquip(InkSkinId.RedShoes); yield return Wait(.1f);
                CheckTouchTargets(preview);
                Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                var before = preview.Drift.anchoredPosition;
                yield return Wait(.4f);
                float distance = Vector2.Distance(before, preview.Drift.anchoredPosition);
                Assert.That(distance, Is.InRange(1f, 24f), "자동 부유는 중력 없이 일정한 저속을 유지한다.");
                yield return Capture("slow-free-drift");

                // 실제 Input System → UI 모듈 → 레이캐스트 → 드래그 경로. 시작 버튼 위에서 손을 떼도 시작하지 않는다.
                touch = InputSystem.AddDevice<Touchscreen>();
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                // 자연 낙하로 버튼 뒤에 들어간 먹 대신, 처음의 빈 로비 영역에서 터치 검사를 시작한다.
                // 메뉴와 겹쳤을 때 버튼 입력이 우선하는지는 아래에서 별도로 확인한다.
                preview.Drift.anchoredPosition = clearOrigin;
                Canvas.ForceUpdateCanvases();
                Vector2 origin = ScreenPoint(preview, preview.Drift.anchoredPosition);
                yield return CheckRaycast(origin, hit => Assert.That(hit, Is.EqualTo(preview.Drift.gameObject)));
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Began, origin);
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(body.overrideSprite, Is.SameAs(InkAccessoryMotion.Held.Frame(0)));
                Vector2 target = new(-preview.Travel.x * .7f, preview.Travel.y * .5f);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved, ScreenPoint(preview, target));
                Assert.That(Vector2.Distance(preview.Drift.anchoredPosition, target), Is.LessThan(1));
                foreach (var corner in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                {
                    yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved,
                        ScreenPoint(preview, Vector2.Scale(corner, preview.Area.sizeDelta)));
                    Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                    Assert.That(Vector2.Distance(preview.Drift.anchoredPosition, Vector2.Scale(corner, preview.Travel)), Is.LessThan(1));
                }
                yield return Capture("drag-bottom-left");
                Vector2 startPoint = RectTransformUtility.WorldToScreenPoint(null, lobby.StartButton.transform.position);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Moved, startPoint);
                yield return Touch(touch, 1, UnityEngine.InputSystem.TouchPhase.Ended, startPoint);
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                Assert.That(body.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                yield return CheckRaycast(startPoint, hit => Assert.That(hit.GetComponentInParent<UnityEngine.UI.Button>(), Is.EqualTo(lobby.StartButton),
                    "먹이 겹쳐도 시작 버튼의 탭이 우선이다."));

                // 두 번째 손가락은 먼저 잡은 손가락을 가로채지 못한다.
                var e = new PointerEventData(EventSystem.current) { pointerId = 41, position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                preview.OnPointerDown(e); var held = preview.Drift.anchoredPosition;
                var other = new PointerEventData(EventSystem.current) { pointerId = 42, position = Vector2.zero };
                preview.OnPointerDown(other); preview.OnDrag(other); preview.OnPointerUp(other);
                Assert.That(preview.IsDragging, Is.True);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                e.position += Vector2.one * 100; preview.OnDrag(e);
                Assert.That(preview.IsDragging, Is.False);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                // 모션 줄이기 상태에서도 수동 이동은 가능하고 손을 떼면 그 자리에 남는다.
                yield return Wait(.2f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(held));
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                preview.OnPointerDown(e);
                var caughtFrame = body.overrideSprite;
                float heldDeadline = Time.realtimeSinceStartup + 1;
                while (body.overrideSprite == caughtFrame && Time.realtimeSinceStartup < heldDeadline) yield return null;
                Assert.That(body.overrideSprite, Is.Not.SameAs(caughtFrame), "잡고 있으면 실제 원화 프레임이 바뀐다.");
                yield return Capture("held-red-shoes");
                preview.OnPointerUp(e);
                Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                var edge = new PointerEventData(EventSystem.current) { pointerId = 43, position = ScreenPoint(preview, held) };
                preview.OnPointerDown(edge);
                yield return null;
                edge.position = ScreenPoint(preview, preview.Area.sizeDelta); preview.OnDrag(edge); preview.OnPointerUp(edge);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(preview.Travel));
                yield return Wait(.2f);
                Assert.That(preview.Velocity.x, Is.LessThan(0)); Assert.That(preview.Velocity.y, Is.LessThan(0));
                Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                yield return Capture("top-right-bounce");

                // 정면 고정 회귀: 실제 LateUpdate에서 비스듬한 벽 충돌을 시켜 본체·의상을 함께 회전한다.
                preview.Drift.anchoredPosition = new Vector2(preview.Travel.x, preview.Travel.y * .25f);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(LobbySkinPreview).GetField("velocity", flags).SetValue(preview, new Vector2(800, -400));
                typeof(LobbySkinPreview).GetField("angularVelocity", flags).SetValue(preview, 0f);
                float beforeAngle = preview.Drift.eulerAngles.z;
                yield return Wait(.35f);
                Assert.That(preview.Velocity.x, Is.LessThan(0));
                Assert.That(preview.AngularVelocity, Is.GreaterThan(10));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(beforeAngle, preview.Drift.eulerAngles.z)), Is.GreaterThan(5));
                Canvas.ForceUpdateCanvases(); CheckBounds(preview);
                var costume = preview.transform.Find("Drift/Artwork/Accessory");
                Assert.That(Quaternion.Angle(body.transform.rotation, costume.rotation), Is.LessThan(.01f));
                yield return Capture("oblique-wall-tumble");

                var regrab = new PointerEventData(EventSystem.current) { pointerId = 44,
                    position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                preview.OnPointerDown(regrab);
                var heldRotation = preview.Drift.localRotation;
                yield return Wait(.15f);
                Assert.That(preview.AngularVelocity, Is.Zero);
                Assert.That(preview.Drift.localRotation, Is.EqualTo(heldRotation), "잡힌 각도에서 정지하고 정면으로 튀지 않는다.");
                preview.OnPointerUp(regrab);
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                yield return Wait(.15f);
                Assert.That(preview.Drift.localRotation, Is.EqualTo(heldRotation));
                LobbySettingsProfile.SetReducedMotionEnabled(false);

                lobby.OptionsButton.onClick.Invoke(); yield return Wait(.5f);
                before = preview.Drift.anchoredPosition;
                var pausedRotation = preview.Drift.localRotation; yield return Wait(.3f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(before));
                Assert.That(preview.Drift.localRotation, Is.EqualTo(pausedRotation));
                Assert.That(preview.Drift.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
                Object.FindAnyObjectByType<LobbyOptionsView>().Close(); yield return Wait(.5f);
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                before = preview.Drift.anchoredPosition; yield return Wait(.2f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(before));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                lobby.SetNavigationPresentation(false, false); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.False);
            }
            finally
            {
                if (touch != null) InputSystem.RemoveDevice(touch);
                previousTouch?.MakeCurrent();
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static Vector2 ScreenPoint(LobbySkinPreview preview, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, preview.Area.TransformPoint(local));

        [UnityTest, Timeout(90000)]
        public IEnumerator HeldArtRegistrationKeepsAccessoriesOnEveryDrawnPose()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                SkinDebugSession.SetAllUnlocked(true);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var artwork = (RectTransform)preview.Drift.Find("Artwork");
                var body = artwork.Find("OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                var art = artwork.Find("Accessory").GetComponent<InkAccessoryImage>();
                var clock = typeof(LobbySkinPreview).GetField("heldTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var present = typeof(LobbySkinPreview).GetMethod("PresentHeldPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                // 검수 화면만 확대한다. 씬/저장값/실제 로비 배율에는 반영하지 않는다.
                preview.transform.SetAsLastSibling();
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                lobby.LogoRect.gameObject.SetActive(false);
                foreach (var button in lobby.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    foreach (var graphic in button.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.enabled = false;
                foreach (var id in new[] { InkSkinId.Ink, InkSkinId.RedHeadband, InkSkinId.Astronaut, InkSkinId.SpiderMask, InkSkinId.RedShoes, InkSkinId.CloudBoots })
                {
                    preview.enabled = true; artwork.localScale = Vector3.one;
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    preview.enabled = false;
                    preview.Drift.anchoredPosition = new Vector2(0, 100);
                    artwork.localScale = Vector3.one * 4;
                    var e = new PointerEventData(EventSystem.current) { pointerId = 87, position = ScreenPoint(preview, preview.Drift.anchoredPosition) };
                    preview.OnPointerDown(e); Assert.That(preview.IsDragging, Is.True);
                    for (int frame = 0; frame < 4; frame++)
                    {
                        clock.SetValue(preview, (frame + .1f) / InkAccessoryMotion.Held.FramesPerSecond);
                        present.Invoke(preview, null); Canvas.ForceUpdateCanvases();
                        Assert.That(body.overrideSprite, Is.SameAs(InkAccessoryMotion.Held.Frame(frame)));
                        Assert.That(art.sprite == InkSkinCatalog.Sprite(id), Is.True);
                        yield return Capture("held-fit-" + id + "-" + frame);
                    }
                    preview.OnPointerUp(e);
                    Assert.That(body.overrideSprite, Is.SameAs(body.sprite));
                    Assert.That(body.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                }
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckRaycast(Vector2 point, System.Action<GameObject> assertion)
        {
            // EditMode 테스트의 바깥 콜백은 Screen 크기가 Test Runner 창 기준이다.
            // 실제 Game View의 Player 입력 프레임에서만 UI 레이캐스트를 수행한다.
            bool sampled = false; GameObject target = null; System.Exception failure = null;
            System.Action after = () =>
            {
                if (sampled || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                sampled = true;
                try { target = TopTarget(point); } catch (System.Exception e) { failure = e; }
            };
            InputSystem.onAfterUpdate += after;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (!sampled && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(sampled, Is.True);
                Assert.That(failure, Is.Null, failure?.ToString());
                assertion(target);
            }
            finally { InputSystem.onAfterUpdate -= after; }
        }
        static GameObject TopTarget(Vector2 point)
        {
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty, $"point={point}, screen={Screen.width}x{Screen.height}, raycasters=" +
                string.Join(";", Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None)
                    .Select(r => $"{r.name}:{r.isActiveAndEnabled}:{r.GetComponent<Canvas>().renderMode}")) + "; graphics=" +
                string.Join(";", Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
                    .Where(g => g.raycastTarget && g.GetComponentInParent<LobbyView>() != null)
                    .Select(g => $"{g.name}:depth={g.depth},cull={g.canvasRenderer.cull},rect={RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform,point)},filter={g.Raycast(point,null)},pos={g.transform.position}")));
            return hits[0].gameObject;
        }
        static IEnumerator Touch(Touchscreen touch, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 point)
        {
            bool queued = false;
            int queuedFrame = -1;
            System.Action before = () =>
            {
                if (queued || UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                queued = true;
                queuedFrame = Time.frameCount;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = phase, position = point });
            };
            InputSystem.onBeforeUpdate += before;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (!queued && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(queued, Is.True);
                // EditMode의 yield 횟수와 실제 Player 프레임은 다르다. UI 모듈이 이벤트를 처리할 때까지 기다린다.
                while (Time.frameCount <= queuedFrame + 1 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(queuedFrame + 1));
            }
            finally { InputSystem.onBeforeUpdate -= before; }
        }
        static void CheckTouchTargets(LobbySkinPreview preview)
        {
            var targets = preview.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.raycastTarget).ToArray();
            Assert.That(targets.Length, Is.EqualTo(1), "먹 외곽만 터치할 수 있고 빈 화면은 막지 않는다.");
            Assert.That(targets[0].transform, Is.EqualTo(preview.Drift));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EquippedSkinFloatsOnlyInLobbyAndNeverStartsGame()
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var lobby = Object.FindAnyObjectByType<LobbyView>();
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.IsVisible, Is.True);
                CheckTouchTargets(preview);
                Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(preview.GetComponentsInChildren<PlayerController>(true), Is.Empty);
                int players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;
                var initial = preview.Drift.anchoredPosition;
                yield return Wait(1);
                Assert.That(Vector2.Distance(initial, preview.Drift.anchoredPosition), Is.GreaterThan(1));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length, Is.EqualTo(players));

                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                for (int i = 0; i < InkSkinCatalog.Count; i++)
                {
                    var id = (InkSkinId)i;
                    Assert.That(InkSkinPreferences.TryEquip(id), Is.True); yield return Wait(.1f);
                    Assert.That(preview.DisplayedSkin, Is.EqualTo(id));
                    var art = preview.transform.Find("Drift/Artwork/Accessory").GetComponent<InkAccessoryImage>();
                    // Unity의 빈 오브젝트 참조도 기본 먹의 null 스프라이트와 동일하게 비교한다.
                    Assert.That(art.sprite == InkSkinCatalog.Sprite(id), Is.True, id + " accessory");
                    CheckBounds(preview);
                    Assert.That(art.transform.GetSiblingIndex(), Is.EqualTo(InkSkinCatalog.BehindBody(id) ? 0 : 1));
                }
                InkSkinPreferences.TryEquip(InkSkinId.RedHeadband); yield return Wait(.1f);
                yield return Capture("lobby-floating-headband");
                yield return Wait(2); yield return Capture("lobby-floating-drift");
                InkSkinPreferences.TryEquip(InkSkinId.CraneWings); yield return Wait(.1f);
                yield return Capture("lobby-floating-wings");
                InkSkinPreferences.TryEquip(InkSkinId.RedShoes); yield return Wait(.1f);
                yield return Capture("lobby-floating-boots");
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(),Is.True);
                yield return WaitNavigation();
                var wardrobe=Object.FindAnyObjectByType<InkWardrobeView>();
                Assert.That(wardrobe.IsOpen,Is.True);
                foreach(var language in new[]{GameLanguage.Korean,GameLanguage.English,GameLanguage.Japanese})
                {
                    GameLocalization.SetLanguage(language);
                    wardrobe.Select(language==GameLanguage.Korean ? InkSkinId.RedShoes : InkSkinId.SpiritHalo);
                    Assert.That(wardrobe.Selected,Is.EqualTo(language==GameLanguage.Korean ? InkSkinId.RedShoes : InkSkinId.SpiritHalo));
                    yield return Wait(.5f); yield return Capture("wardrobe-polish-"+language);
                }
                wardrobe.ToggleCollection(); Assert.That(wardrobe.CollectionExpanded,Is.True);
                yield return Wait(.2f); yield return Capture("wardrobe-polish-groups");
                wardrobe.ToggleCollection();
                GameLocalization.SetLanguage(GameLanguage.Korean);
                wardrobe.BackButton.onClick.Invoke(); yield return WaitNavigation();
                Assert.That(lobby.IsInteractive,Is.True);

                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                var paused = preview.Drift.anchoredPosition;
                var pausedRotation = preview.Drift.localRotation; yield return Wait(.15f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.SetReducedMotionEnabled(true); yield return Wait(.1f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                Assert.That(preview.Drift.localRotation, Is.EqualTo(pausedRotation));
                LobbySettingsProfile.SetReducedMotionEnabled(false);
                lobby.SetActiveMenu(LobbyMenuSelection.Options);
                // 실제 옵션 창을 열어 메뉴 상태가 다음 프레임에도 유지되게 한다.
                lobby.OptionsButton.onClick.Invoke(); yield return Wait(.5f);
                paused = preview.Drift.anchoredPosition; yield return Wait(.15f);
                Assert.That(preview.Drift.anchoredPosition, Is.EqualTo(paused));
                Object.FindAnyObjectByType<LobbyOptionsView>().Close(); yield return Wait(.5f);
                lobby.SetNavigationPresentation(false, false); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.False);
                lobby.SetNavigationPresentation(true, true); yield return Wait(.1f);
                Assert.That(preview.IsVisible, Is.True);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(LobbyScreenNavigator.Instance.CanStartGame,Is.True);
                lobby.StartButton.onClick.Invoke();
                float playDeadline=Time.realtimeSinceStartup+20;
                while(GameManager.Instance.State==GameState.Lobby && Time.realtimeSinceStartup<playDeadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                Assert.That(preview.IsVisible, Is.False);
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                Application.runInBackground = previousBackground;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static void CheckBounds(LobbySkinPreview preview)
        {
            var corners = new Vector3[4];
            foreach (var image in preview.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if (!image.enabled) continue;
                image.rectTransform.GetWorldCorners(corners);
                if (image.name == "OriginalInkBody")
                {
                    Rect r = preview.IsDragging ? LobbySkinPreview.HeldDrawingBounds : LobbySkinPreview.BodyDrawingBounds;
                    if (preview.IsDragging) r.position -= LobbySkinPreview.HeldArtOffset;
                    float size = image.rectTransform.rect.width;
                    corners[0] = image.transform.TransformPoint(new Vector3(r.xMin, r.yMin) * size);
                    corners[1] = image.transform.TransformPoint(new Vector3(r.xMin, r.yMax) * size);
                    corners[2] = image.transform.TransformPoint(new Vector3(r.xMax, r.yMax) * size);
                    corners[3] = image.transform.TransformPoint(new Vector3(r.xMax, r.yMin) * size);
                }
                // 신발은 빈 이미지 사각형 대신 실제 좌우 신발 메시를 검사한다.
                if (image is InkAccessoryImage && InkSkinCatalog.IsFootwear(preview.DisplayedSkin))
                {
                    var mesh = image.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.EqualTo(8));
                    foreach (var vertex in mesh.vertices)
                        Assert.That(preview.Area.rect.Contains(preview.Area.InverseTransformPoint(image.transform.TransformPoint(vertex))),
                            Is.True, preview.DisplayedSkin + " footwear overflow");
                    continue;
                }
                foreach (var c in corners)
                {
                    var p = preview.Area.InverseTransformPoint(c);
                    Assert.That(preview.Area.rect.Contains(p), Is.True, preview.DisplayedSkin + " overflow");
                }
            }
        }
        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
        static IEnumerator WaitNavigation()
        {
            float until=Time.realtimeSinceStartup+20;
            while(LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(LobbyScreenNavigator.Instance.IsTransitioning,Is.False,"붓 전환 완료 후 촬영한다.");
            yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/sprite-animation-2026-09-13");
            File.Copy(path, "output/qa/sprite-animation-2026-09-13/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
