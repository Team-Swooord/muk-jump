using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class GameplayLoopAuditTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator DestroyedSupportingStrokeCannotLeavePlayerFloating()
        {
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return new EnterPlayMode();
            EditorApplication.isPaused = false;
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("DestroyedStrokeAudit",
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("AuditCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>().orthographic = true;
            var playerObject = new GameObject("AuditPlayer");
            playerObject.layer = LayerMask.NameToLayer("Player");
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.position = new Vector2(0f, 1f);
            playerObject.AddComponent<CircleCollider2D>().radius = 0.25f;
            var player = playerObject.AddComponent<PlayerController>();
            var platform = PlatformCollider.Spawn(new List<Vector2> { new(-2f, 0f), new(2f, 0f) });
            var physics = scene.GetPhysicsScene2D();
            Physics2D.SyncTransforms();
            for (int i = 0; i < 40; i++)
            {
                typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(player, null);
                physics.Simulate(0.02f);
            }
            bool landed = player.CurrentPlatform == platform && player.IsGrounded;
            float supportedGravity = body.gravityScale;
            // 수명/장애물/씬 정리에서 실제 오브젝트가 사라지는 경로를 검증한다.
            Object.Destroy(platform.gameObject);
            yield return null;
            float startY = body.position.y;
            for (int i = 0; i < 12; i++)
            {
                typeof(PlayerController).GetMethod("FixedUpdate", Private).Invoke(player, null);
                physics.Simulate(0.02f);
            }
            float detachedGravity = body.gravityScale;
            bool falling = body.position.y < startY - 0.1f;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
            Application.runInBackground = previousBackground;
            Assert.That(landed, Is.True, "검사 전 실제 먹선에 착지해야 합니다.");
            Assert.That(supportedGravity, Is.Zero);
            Assert.That(detachedGravity, Is.EqualTo(3f), "삭제된 발판의 접착 중력을 해제해야 합니다.");
            Assert.That(falling, Is.True, "먹선이 사라진 뒤 공중에 붙어 있으면 안 됩니다.");
        }
    }
}
