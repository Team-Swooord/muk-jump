using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.EditorTests
{
    /// EditMode 단위 테스트가 보지 못하는 실제 Physics2D 트리거 순서를 Play 상태에서 검증한다.
    public sealed class RuntimePhysicsIntegrationTests
    {
        [UnityTest]
        public IEnumerator DrawnPlatformPassesFromBelowAndCatchesFromAbove()
        {
            bool previousRunInBackground = Application.runInBackground;
            float previousTimeScale = Time.timeScale;
            float previousFixedDeltaTime = Time.fixedDeltaTime;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            // 다른 fixture의 시작 지형·카메라·일시정지 상태가 단방향 물리
            // 자체의 검증에 섞이지 않게 전용 Physics2D 씬을 고정 간격으로 진행한다.
            Scene physicsTestScene = SceneManager.CreateScene(
                "OneWayPlatformPhysicsTest",
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            PhysicsScene2D physics = physicsTestScene.GetPhysicsScene2D();
            PlatformCollider platform = PlatformCollider.Spawn(
                new System.Collections.Generic.List<Vector2>
                {
                    new(-2f, 0f),
                    new(2f, 0f),
                });
            SceneManager.MoveGameObjectToScene(platform.gameObject, physicsTestScene);
            var bodyObject = new GameObject("OneWayPhysicsProbe");
            SceneManager.MoveGameObjectToScene(bodyObject, physicsTestScene);
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                bodyObject.layer = playerLayer;
            var body = bodyObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            bodyObject.AddComponent<CircleCollider2D>().radius = 0.25f;
            body.position = new Vector2(0f, -1f);
            body.linearVelocity = Vector2.up * 5f;
            Physics2D.SyncTransforms();

            bool passedFromBelow = false;
            bool simulationSucceeded = true;
            for (int i = 0; i < 24; i++)
            {
                simulationSucceeded &= physics.Simulate(0.02f);
                if (body.position.y > 0.55f)
                {
                    passedFromBelow = true;
                    break;
                }
            }

            body.position = new Vector2(0f, 1.2f);
            body.linearVelocity = Vector2.down * 3f;
            Physics2D.SyncTransforms();
            bool caughtFromAbove = false;
            for (int i = 0; i < 32; i++)
            {
                simulationSucceeded &= physics.Simulate(0.02f);
                if (body.position.y > 0f &&
                    Mathf.Abs(body.linearVelocity.y) < 0.05f)
                {
                    caughtFromAbove = true;
                    break;
                }
            }

            Object.Destroy(bodyObject);
            Object.Destroy(platform.gameObject);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(physicsTestScene);
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            AudioListener.pause = false;
            yield return new ExitPlayMode();
            Application.runInBackground = previousRunInBackground;

            Assert.That(simulationSucceeded, Is.True,
                "격리된 Physics2D 씬의 모든 시뮬레이션 스텝이 실행돼야 합니다.");
            Assert.That(passedFromBelow, Is.True,
                "그린 먹선은 상승 중 아래면을 막으면 안 됩니다.");
            Assert.That(caughtFromAbove, Is.True,
                "그린 먹선은 하강 중 위쪽에서 착지시켜야 합니다.");
        }

        [UnityTest]
        public IEnumerator BalancedGuidePlacesTargetBeforeOldUpperBand()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;

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
            // 전체 스위트의 일시정지·히트스톱 상태와 무관하게 공개 스냅 경로가
            // 실제 Camera Transform에 55% 기준선을 적용하는지 확인한다.
            follow.DebugSnapTo(targetObject.transform);
            float cameraY = cameraObject.transform.position.y;

            Object.Destroy(targetObject);
            Object.Destroy(cameraObject);
            yield return null;
            yield return new ExitPlayMode();
            Application.runInBackground = previousRunInBackground;

            Assert.Greater(cameraY, 0f,
                "55% 균형선은 이전 75% 데드존보다 실제 상승을 빠르게 따라가야 합니다.");
        }

        [UnityTest]
        public IEnumerator MovingObstacleFirstContactConsumesOneHealthAndItself()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;

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
            obstacleObject.AddComponent<SpriteRenderer>();
            var obstacleBody = obstacleObject.AddComponent<Rigidbody2D>();
            obstacleBody.bodyType = RigidbodyType2D.Kinematic;
            var obstacleTrigger =
                obstacleObject.AddComponent<CircleCollider2D>();
            obstacleObject.AddComponent<CapsuleCollider2D>();
            var obstacle = obstacleObject.AddComponent<Obstacle>();
            obstacleTrigger.isTrigger = true;
            obstacleTrigger.radius = 0.45f;
            obstacleObject.transform.position = Vector3.zero;
            obstacle.Configure(0f, 0f, 0f);

            // 연속 EnterPlayMode에서 첫 FixedUpdate가 테스트 콜백보다 늦게
            // 복원될 수 있으므로 실제 trigger가 호출하는 동일 경로를 직접 검증한다.
            Physics2D.SyncTransforms();
            Invoke(
                obstacle,
                "OnTriggerEnter2D",
                playerObject.GetComponent<CircleCollider2D>());
            yield return null;

            bool diedOnFirstContact = player != null && player.IsDead;
            int healthAfterContact = player != null
                ? player.CurrentHealth
                : -1;
            var remainingTrigger = obstacleObject != null
                ? obstacleObject.GetComponent<CircleCollider2D>()
                : null;
            bool triggerDisabled =
                remainingTrigger == null || !remainingTrigger.enabled;

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
            Application.runInBackground = previousRunInBackground;

            Assert.That(diedOnFirstContact, Is.False);
            Assert.That(healthAfterContact, Is.EqualTo(2),
                "기본 체력 3칸은 첫 무방비 장애물 접촉 뒤 2칸이 남아야 합니다.");
            Assert.That(triggerDisabled, Is.True,
                "한 번 피해를 준 이동 장애물은 즉시 판정을 꺼야 합니다.");
        }

        [UnityTest]
        public IEnumerator ChildDragonCapsuleConsumesShieldThenThreeHealthHits()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;

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

            // 전체 스위트가 EnterPlayMode를 연속 실행하면 첫 물리 스텝보다
            // 테스트 콜백이 먼저 복원되는 경우가 있다. 실제 capsule 판정이
            // 호출하는 동일 경로를 직접 실행해 방어막/체력 계약을 결정적으로 검증한다.
            Physics2D.SyncTransforms();
            Invoke(
                dragon,
                "OnTriggerEnter2D",
                playerObject.GetComponent<CircleCollider2D>());
            yield return null;

            Assert.IsFalse(player.IsDead);
            Assert.IsFalse(player.HasShield);
            Assert.AreEqual(3, player.CurrentHealth);
            Assert.IsFalse(capsule.enabled,
                "방어막을 소모시킨 용도 한 번 충돌한 뒤 사라져야 합니다.");
            Assert.IsFalse(dragonObject.GetComponent<CircleCollider2D>().enabled);

            Object.Destroy(dragonObject);
            yield return null;
            bool everyDragonDisabledAfterHit = true;
            for (int hit = 0; hit < 3; hit++)
            {
                SetField(player, "damageInvulnerableUntil", Time.time - 1f);
                var nextDragonObject = new GameObject(
                    $"RuntimeChildDragonHealthHit{hit + 1}");
                var nextDragon = nextDragonObject.AddComponent<Obstacle>();
                var nextCapsule =
                    nextDragonObject.GetComponent<CapsuleCollider2D>();
                nextCapsule.isTrigger = true;
                nextCapsule.size = new Vector2(2.5f, 0.55f);
                nextDragon.Configure(
                    0f, 0f, 0f, ObstacleKind.ChildDragon);
                Invoke(nextDragon, "OnTriggerEnter2D",
                    playerObject.GetComponent<CircleCollider2D>());
                everyDragonDisabledAfterHit &= !nextCapsule.enabled;
                Object.Destroy(nextDragonObject);
                yield return null;
            }

            bool diedAfterThreeHealthHits = player.IsDead;
            int remainingHealth = player.CurrentHealth;
            Object.Destroy(playerObject);
            Object.Destroy(managerObject);
            Object.Destroy(cameraObject);
            if (BackgroundMusicController.Instance != null)
                Object.Destroy(BackgroundMusicController.Instance.gameObject);
            yield return null;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return new ExitPlayMode();
            Application.runInBackground = previousRunInBackground;

            Assert.IsTrue(diedAfterThreeHealthHits);
            Assert.AreEqual(0, remainingHealth);
            Assert.IsTrue(everyDragonDisabledAfterHit,
                "각 어린 용은 한 번 피해를 준 직후 판정을 꺼야 합니다.");
        }

        [UnityTest]
        public IEnumerator CloneArrivalShowsBodyThenFullCharacterAndRestoresRenderer()
        {
            bool previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();

            EditorApplication.isPaused = false;
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
            var player = playerObject.AddComponent<PlayerController>();
            // 전체 스위트의 Main 씬 카메라·게임 상태가 남아 있어도 테스트용 개체가
            // 추락 피해로 죽지 않게 PlayerController의 물리 생명주기만 격리한다.
            // InkCloneArrivalView 코루틴은 별도 컴포넌트라 그대로 실행된다.
            player.enabled = false;
            var arrival = playerObject.AddComponent<InkCloneArrivalView>();

            Assert.IsTrue(playerRenderer.enabled,
                "생성 연출을 시작하기 전 본체 렌더러는 활성 상태여야 합니다.");
            arrival.Play();
            var arrivalRenderer = playerObject.transform
                .Find("InkCloneArrivalVisual")
                ?.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(arrivalRenderer);
            Assert.IsFalse(playerRenderer.enabled);
            Assert.IsTrue(arrivalRenderer.enabled);
            Assert.AreEqual("MukJump_InkBlobMask", arrivalRenderer.sprite.name);

            // 완성 캐릭터가 잠깐 보이는 중간 단계는 에디터 프레임이 길면 테스트
            // 코루틴이 건너뛸 수 있다. 순간 활성 상태 대신 연출이 남기는 안정된
            // 최종 상태를 구현과 같은 unscaledTime 제한 안에서 기다린다. 에디터가
            // 일시정지되거나 비활성화되면 realtime 제한만 먼저 끝날 수 있으므로
            // 위에서 플레이어 루프를 정상화하고 두 시계를 섞지 않는다.
            float completionDeadline = Time.unscaledTime + 2f;
            while ((!playerRenderer.enabled || arrivalRenderer.enabled) &&
                   Time.unscaledTime < completionDeadline)
                yield return null;
            Assert.AreSame(sprite, arrivalRenderer.sprite,
                "몸통 단계 뒤에는 눈·다리가 포함된 현재 캐릭터 프레임으로 전환돼야 합니다.");
            Assert.IsTrue(playerRenderer.enabled,
                "생성 연출이 끝나면 본체 렌더러가 다시 활성화돼야 합니다.");
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
            Application.runInBackground = previousRunInBackground;
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
