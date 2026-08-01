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
        public void LobbyHidesStarterWorldUntilGameplayBegins()
        {
            playerObject = new GameObject("LegacyLobbyPlayer");
            var playerRenderer = playerObject.AddComponent<SpriteRenderer>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
            playerObject.AddComponent<PlayerController>();
            var legacyWander = playerObject.AddComponent<LobbyCharacterWander>();
            playerObject.transform.position = new Vector3(3.4f, 2f, 0f);
            Assert.IsTrue(legacyWander.enabled);

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

            Assert.IsFalse(legacyWander.enabled,
                "구버전 씬의 로비 왕복 이동도 다시 활성화되면 안 됩니다.");
            Assert.IsFalse(playerRenderer.enabled,
                "로비 하단 먹방울은 시작 전에는 보이지 않아야 합니다.");
            Assert.IsFalse(line.enabled,
                "로비 하단 시작 먹선은 시작 전에는 보이지 않아야 합니다.");
            Assert.That(
                playerObject.transform.position.x,
                Is.EqualTo(platformObject.transform.position.x).Within(0.001f),
                "구버전 왕복 이동 중 남은 위치는 시작 먹선 중앙으로 복구해야 합니다.");
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

            setup.ApplyPresentationForTests(GameState.Playing);

            Assert.IsTrue(playerRenderer.enabled,
                "시작 버튼 뒤에는 실제 플레이어가 보여야 합니다.");
            Assert.IsTrue(line.enabled,
                "시작 버튼 뒤에는 첫 점프용 먹선이 보여야 합니다.");
        }

        [Test]
        public void LobbyPresentationAlsoRunsBeforeEnteringPlayMode()
        {
            Assert.IsTrue(
                System.Attribute.IsDefined(
                    typeof(LobbyWorldSetup),
                    typeof(ExecuteAlways)),
                "Play 전 Main Game View도 런타임 로비 표시 규칙을 사용해야 합니다.");
        }
    }
}
