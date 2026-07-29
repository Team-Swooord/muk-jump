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
        public IEnumerator BasicJumpApexBelowGuideKeepsCameraStill()
        {
            yield return new EnterPlayMode();

            var cameraObject = new GameObject("CameraFollowIntegration");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 9.6f;
            var follow = cameraObject.AddComponent<CameraFollow>();

            var targetObject = new GameObject("CameraFollowTarget");
            targetObject.transform.position = new Vector3(0f, 4.18f, 0f);
            SetField(follow, "target", targetObject.transform);

            yield return null;
            yield return null;
            float cameraY = cameraObject.transform.position.y;

            Object.Destroy(targetObject);
            Object.Destroy(cameraObject);
            yield return null;
            yield return new ExitPlayMode();

            Assert.AreEqual(0f, cameraY, 0.001f,
                "기본 점프 정점이 화면 75% 아래라면 실제 카메라 Transform도 고정돼야 합니다.");
        }

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

        [UnityTest]
        public IEnumerator CloneArrivalShowsBodyThenFullCharacterAndRestoresRenderer()
        {
            yield return new EnterPlayMode();

            Time.timeScale = 1f;
            var cameraObject = new GameObject("CloneArrivalCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();

            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "CloneArrivalTestTexture";
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                8f);

            var playerObject = new GameObject("CloneArrivalPlayer");
            var playerRenderer = playerObject.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = sprite;
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerObject.AddComponent<CircleCollider2D>();
            playerObject.AddComponent<PlayerController>();
            var arrival = playerObject.AddComponent<InkCloneArrivalView>();

            arrival.Play();
            var arrivalRenderer = playerObject.transform
                .Find("InkCloneArrivalVisual")
                ?.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(arrivalRenderer);
            Assert.IsFalse(playerRenderer.enabled);
            Assert.IsTrue(arrivalRenderer.enabled);
            Assert.AreEqual("MukJump_InkBlobMask", arrivalRenderer.sprite.name);

            // 몸통 0.12초와 완성 팝 0.18초의 가운데를 검사해 느린 첫 프레임에도
            // 단계 경계와 겹치지 않게 한다.
            float phaseDeadline = Time.time + 0.22f;
            while (Time.time < phaseDeadline)
                yield return null;
            Assert.AreSame(sprite, arrivalRenderer.sprite,
                "몸통 단계 뒤에는 눈·다리가 포함된 현재 캐릭터 프레임이 뿅 나타나야 합니다.");
            Assert.IsFalse(playerRenderer.enabled);
            Assert.IsTrue(arrivalRenderer.enabled);

            phaseDeadline = Time.time + 0.18f;
            while (Time.time < phaseDeadline)
                yield return null;
            Assert.IsTrue(playerRenderer.enabled);
            Assert.IsFalse(arrivalRenderer.enabled);
            Assert.AreEqual(Vector3.one, arrivalRenderer.transform.localScale);

            Object.Destroy(playerObject);
            Object.Destroy(cameraObject);
            Object.Destroy(sprite);
            Object.Destroy(texture);
            yield return null;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return new ExitPlayMode();
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
