using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class ScreenSideWallsTests
    {
        GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);
            var orphanedWalls = Object.FindObjectsByType<ScreenSideWall>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < orphanedWalls.Length; i++)
                if (orphanedWalls[i] != null)
                    Object.DestroyImmediate(orphanedWalls[i].gameObject);
        }

        [Test]
        public void SideWallsUseKinematicBodiesAndAreOwnedByComponent()
        {
            cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var sideWalls = cameraObject.AddComponent<ScreenSideWalls>();
            var ensureWalls = typeof(ScreenSideWalls).GetMethod(
                "EnsureWalls", BindingFlags.Instance | BindingFlags.NonPublic);
            var cleanup = typeof(ScreenSideWalls).GetMethod(
                "CleanupWalls", BindingFlags.Instance | BindingFlags.NonPublic);
            var leftField = typeof(ScreenSideWalls).GetField(
                "leftWall", BindingFlags.Instance | BindingFlags.NonPublic);
            var rightField = typeof(ScreenSideWalls).GetField(
                "rightWall", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(ensureWalls, Is.Not.Null);
            Assert.That(cleanup, Is.Not.Null);
            Assert.That(leftField, Is.Not.Null);
            Assert.That(rightField, Is.Not.Null);

            ensureWalls.Invoke(sideWalls, null);
            var left = (Rigidbody2D)leftField.GetValue(sideWalls);
            var right = (Rigidbody2D)rightField.GetValue(sideWalls);

            AssertKinematicWall(left);
            AssertKinematicWall(right);

            leftField.SetValue(sideWalls, null);
            rightField.SetValue(sideWalls, null);
            ensureWalls.Invoke(sideWalls, null);

            Assert.AreSame(left, leftField.GetValue(sideWalls),
                "domain reload 뒤 기존 왼쪽 벽을 다시 찾아야 합니다.");
            Assert.AreSame(right, rightField.GetValue(sideWalls),
                "domain reload 뒤 기존 오른쪽 벽을 다시 찾아야 합니다.");
            Assert.That(Object.FindObjectsByType<ScreenSideWall>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(2));

            cleanup.Invoke(sideWalls, null);

            Assert.That(left == null, Is.True);
            Assert.That(right == null, Is.True);
        }

        static void AssertKinematicWall(Rigidbody2D wall)
        {
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(wall.gravityScale, Is.Zero);
            Assert.That(
                (wall.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                Is.True);
            Assert.That(wall.transform.parent, Is.Null,
                "카메라 Transform으로 정적 콜라이더를 순간이동시키면 안 됩니다.");
            var collider = wall.GetComponent<BoxCollider2D>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.sharedMaterial, Is.Not.Null);
            Assert.That(collider.sharedMaterial.friction, Is.Zero);
            Assert.That(collider.sharedMaterial.bounciness, Is.Zero);
            Assert.That(wall.GetComponent<ScreenSideWall>(), Is.Not.Null);
            var visual = wall.GetComponent<LineRenderer>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.useWorldSpace, Is.False);
            Assert.That(visual.startWidth, Is.EqualTo(1.9f).Within(0.001f));
            Assert.That(visual.startColor.a, Is.EqualTo(0.16f).Within(0.001f));
        }

        [TestCase(9.6f, 0.5625f)]
        [TestCase(9.6f, 0.46153846f)]
        public void DrawableRangeTracksPortraitViewportAndCameraMotion(
            float orthographicSize,
            float aspect)
        {
            cameraObject = new GameObject("CameraRange");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.aspect = aspect;
            camera.transform.position = new Vector3(1.7f, 24f, -10f);
            var sideWalls = cameraObject.AddComponent<ScreenSideWalls>();

            Assert.That(sideWalls.TryGetDrawableWorldXRange(
                out float minimumX,
                out float maximumX), Is.True);

            float halfWidth = orthographicSize * aspect;
            Assert.That(minimumX,
                Is.EqualTo(1.7f - halfWidth + 0.95f).Within(0.001f));
            Assert.That(maximumX,
                Is.EqualTo(1.7f + halfWidth - 0.95f).Within(0.001f));
            Assert.That((minimumX + maximumX) * 0.5f,
                Is.EqualTo(camera.transform.position.x).Within(0.001f));

            camera.transform.position += Vector3.right * 2f;
            camera.orthographicSize = orthographicSize * 0.92f;
            Assert.That(sideWalls.TryGetDrawableWorldXRange(
                out float movedMinimum,
                out float movedMaximum), Is.True);
            Assert.That((movedMinimum + movedMaximum) * 0.5f,
                Is.EqualTo(camera.transform.position.x).Within(0.001f));
            Assert.That(movedMaximum - movedMinimum,
                Is.LessThan(maximumX - minimumX));
        }

        [Test]
        public void OrdinarySideWallBounceCancelsOnlyUpwardRatchetVelocity()
        {
            MethodInfo method = typeof(PlayerController).GetMethod(
                "ResolveSideWallBounceVelocity",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var rising = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(-7f, 5.2f),
                1f,
                2.4f,
            });
            Assert.That(rising.x, Is.EqualTo(3.85f).Within(0.001f));
            Assert.That(rising.y, Is.Zero,
                "최초 충돌과 접촉 유지 모두 급경사 먹선에서 얻은 상승 속도를 보존하면 안 됩니다.");

            var falling = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(1f, -3.1f),
                -1f,
                2.4f,
            });
            Assert.That(falling.x, Is.EqualTo(-2.4f).Within(0.001f));
            Assert.That(falling.y, Is.EqualTo(-3.1f).Within(0.001f),
                "일반 하강 감각과 벽 비기 진입 조건은 유지해야 합니다.");
        }
    }
}
