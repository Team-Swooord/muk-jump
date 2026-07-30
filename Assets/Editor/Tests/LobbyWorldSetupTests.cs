using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class LobbyWorldSetupTests
    {
        GameObject playerObject;
        GameObject platformObject;
        GameObject setupObject;

        [TearDown]
        public void TearDown()
        {
            if (setupObject != null)
                Object.DestroyImmediate(setupObject);
            if (platformObject != null)
                Object.DestroyImmediate(platformObject);
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void LegacyLobbyReceivesFullWidthPlatformAndWanderingPlayer()
        {
            playerObject = new GameObject("LegacyLobbyPlayer");
            playerObject.AddComponent<SpriteRenderer>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            playerObject.AddComponent<PlayerController>();
            Assert.IsNull(playerObject.GetComponent<LobbyCharacterWander>());

            platformObject = new GameObject(
                LobbyWorldSetup.StarterPlatformObjectName);
            var line = platformObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            var edge = platformObject.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-1.65f, 0f),
                new Vector2(1.65f, 0f),
            };
            var platform = platformObject.AddComponent<PlatformCollider>();

            setupObject = new GameObject("LegacyLobbyWorldSetup");
            var setup = setupObject.AddComponent<LobbyWorldSetup>();
            setup.ApplyForTests();
            setup.ApplyForTests();

            Assert.That(
                playerObject.GetComponents<LobbyCharacterWander>().Length,
                Is.EqualTo(1),
                "구버전 씬을 여러 번 복구해도 로비 이동 컴포넌트는 중복되면 안 됩니다.");
            Assert.That(edge.pointCount, Is.EqualTo(2));
            Assert.That(
                edge.points[0].x,
                Is.EqualTo(-LobbyWorldSetup.StarterPlatformHalfWidth)
                    .Within(0.001f));
            Assert.That(
                edge.points[1].x,
                Is.EqualTo(LobbyWorldSetup.StarterPlatformHalfWidth)
                    .Within(0.001f));
            Assert.That(line.positionCount, Is.EqualTo(2));
            Assert.That(
                line.GetPosition(0).x,
                Is.EqualTo(-LobbyWorldSetup.StarterPlatformHalfWidth)
                    .Within(0.001f));
            Assert.That(
                line.GetPosition(1).x,
                Is.EqualTo(LobbyWorldSetup.StarterPlatformHalfWidth)
                    .Within(0.001f));
            Assert.That(line.GetPosition(0).y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(line.GetPosition(1).y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(platform.Length,
                Is.EqualTo(LobbyWorldSetup.StarterPlatformHalfWidth * 2f)
                    .Within(0.001f));
        }
    }
}
