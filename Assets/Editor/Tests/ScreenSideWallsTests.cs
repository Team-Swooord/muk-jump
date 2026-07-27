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
            cameraObject.AddComponent<Camera>();
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
        }
    }
}
