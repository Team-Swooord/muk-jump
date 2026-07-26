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

        static void SetAutoProperty(object target, string propertyName, object value)
        {
            target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
