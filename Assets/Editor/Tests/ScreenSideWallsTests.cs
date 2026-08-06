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
            Assert.That(visual.enabled, Is.False,
                "무한 상승 차단용 물리 벽은 유지하되 상시 붉은 띠는 해태 경고와 혼동되지 않게 숨겨야 합니다.");
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
        public void SideWallBounceIsBoundedMirroredTrampoline()
        {
            MethodInfo method = typeof(PlayerController).GetMethod(
                "ResolveSideWallBounceVelocity",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var left = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(-7f, -5f),
                1f,
                7.5f,
                9f,
                new Vector2(4.8f, 6.2f),
                0.5f,
            });
            Assert.That(left.x, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(left.y, Is.EqualTo(6.1f).Within(0.001f));

            var right = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(1f, -20f),
                -1f,
                7.5f,
                9f,
                new Vector2(4.8f, 6.2f),
                1f,
            });
            Assert.That(right.x, Is.EqualTo(-7.5f).Within(0.001f));
            Assert.That(right.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void SideWallStayPreservesGraceThenCancelsRepeatedRise()
        {
            MethodInfo method = typeof(PlayerController).GetMethod(
                "ResolveSideWallEscapeVelocity",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var grace = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(-1f, 5.6f), 1f, 7.5f, true,
            });
            var expired = (Vector2)method.Invoke(null, new object[]
            {
                new Vector2(-1f, 5.6f), 1f, 7.5f, false,
            });

            Assert.That(grace, Is.EqualTo(new Vector2(7.5f, 5.6f)));
            Assert.That(expired, Is.EqualTo(new Vector2(7.5f, 0f)));
        }
    }
}
