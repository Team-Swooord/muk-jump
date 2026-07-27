using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using MukJump.Core;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.EditorTests
{
    /// EditMode 단위 테스트가 보지 못하는 실제 Physics2D 트리거 순서를 Play 상태에서 검증한다.
    public sealed class RuntimePhysicsIntegrationTests
    {
        [UnityTest]
        public IEnumerator MovingObstacleFirstContactKillsPlayer()
        {
            yield return new EnterPlayMode();

            var cameraObject = new GameObject("RuntimePhysicsCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();

            var managerObject = new GameObject("RuntimePhysicsManager");
            var manager = managerObject.AddComponent<GameManager>();
            SetAutoProperty(manager, "State", GameState.Playing);

            var playerObject = new GameObject("RuntimePhysicsPlayer");
            playerObject.AddComponent<SpriteRenderer>();
            var playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerObject.AddComponent<CircleCollider2D>().radius = 0.4f;
            var player = playerObject.AddComponent<PlayerController>();
            playerObject.transform.position = Vector3.zero;

            var obstacleObject = new GameObject("RuntimePhysicsObstacle");
            var obstacle = obstacleObject.AddComponent<Obstacle>();
            var obstacleTrigger = obstacleObject.GetComponent<CircleCollider2D>();
            obstacleTrigger.isTrigger = true;
            obstacleTrigger.radius = 0.45f;
            obstacleObject.transform.position = Vector3.zero;
            obstacle.Configure(0f, 0f, 0f);

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            bool diedOnFirstContact = player.IsDead;

            Object.Destroy(obstacleObject);
            Object.Destroy(playerObject);
            Object.Destroy(managerObject);
            Object.Destroy(cameraObject);
            if (BackgroundMusicController.Instance != null)
                Object.Destroy(BackgroundMusicController.Instance.gameObject);
            yield return null;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return new ExitPlayMode();

            Assert.That(diedOnFirstContact, Is.True,
                "이동 장애물의 첫 트리거 접촉은 점프 반동이 아니라 즉시 사망이어야 합니다.");
        }

        [UnityTest]
        public IEnumerator ChildDragonCapsuleConsumesOneShieldThenKills()
        {
            yield return new EnterPlayMode();

            var cameraObject = new GameObject("DragonPhysicsCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();

            var managerObject = new GameObject("DragonPhysicsManager");
            var manager = managerObject.AddComponent<GameManager>();
            SetAutoProperty(manager, "State", GameState.Playing);

            var playerObject = new GameObject("ShieldedDragonTarget");
            playerObject.AddComponent<SpriteRenderer>();
            var playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerObject.AddComponent<CircleCollider2D>().radius = 0.4f;
            var player = playerObject.AddComponent<PlayerController>();
            player.GrantShield();

            var dragonObject = new GameObject("RuntimeChildDragon");
            var dragon = dragonObject.AddComponent<Obstacle>();
            var capsule = dragonObject.GetComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.size = new Vector2(2.5f, 0.55f);
            dragon.Configure(0f, 0f, 0f, ObstacleKind.ChildDragon);

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsFalse(player.IsDead);
            Assert.IsFalse(player.HasShield);
            Assert.IsTrue(capsule.enabled);
            Assert.IsFalse(dragonObject.GetComponent<CircleCollider2D>().enabled);

            Object.Destroy(dragonObject);
            yield return null;
            SetField(player, "damageInvulnerableUntil", Time.time - 1f);

            var secondDragonObject = new GameObject("RuntimeChildDragonSecondHit");
            var secondDragon = secondDragonObject.AddComponent<Obstacle>();
            var secondCapsule = secondDragonObject.GetComponent<CapsuleCollider2D>();
            secondCapsule.isTrigger = true;
            secondCapsule.size = new Vector2(2.5f, 0.55f);
            secondDragon.Configure(0f, 0f, 0f, ObstacleKind.ChildDragon);
            Invoke(secondDragon, "OnTriggerEnter2D",
                playerObject.GetComponent<CircleCollider2D>());
            yield return null;

            bool diedAfterShield = player.IsDead;
            Object.Destroy(secondDragonObject);
            Object.Destroy(playerObject);
            Object.Destroy(managerObject);
            Object.Destroy(cameraObject);
            if (BackgroundMusicController.Instance != null)
                Object.Destroy(BackgroundMusicController.Instance.gameObject);
            yield return null;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return new ExitPlayMode();

            Assert.IsTrue(diedAfterShield,
                "어린 용의 캡슐 판정은 방어막 한 번 뒤 다음 접촉에서 사망시켜야 합니다.");
        }

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        static void Invoke(object target, string methodName, params object[] arguments)
        {
            target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }
    }
}
