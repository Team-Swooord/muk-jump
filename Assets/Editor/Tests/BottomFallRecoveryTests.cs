using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class BottomFallRecoveryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HiddenPlatformCannotBecomeAnInfiniteFreeBounceFloor()
        {
            yield return new EnterPlayMode();
            var errors = new List<string>();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            var previousManager = GameManager.Instance;
            Scene scene = SceneManager.CreateScene("BottomFallPhysics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            PhysicsScene2D physics = scene.GetPhysicsScene2D();
            // 게임 상태만 공급한다. 실제 계정·점수·씬 생성과 무관한 물리 재현이다.
            var managerObject = new GameObject("BottomFallState");
            managerObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            var manager = managerObject.AddComponent<GameManager>();
            Set(manager, "<State>k__BackingField", GameState.Playing);
            typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
            try
            {
                foreach (float halfHeight in new[] { 5f, 7f })
                foreach (bool shield in new[] { false, true })
                foreach (bool visible in new[] { false, true })
                {
                    var cameraObject = new GameObject("BottomFallCamera");
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    var camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = halfHeight;
                    camera.transform.position = new Vector3(0, 20, -10);
                    float bottom = 20 - halfHeight;
                    float supportY = bottom + (visible ? .3f : -.15f);
                    var platform = PlatformCollider.SpawnMapRestPlatform(new List<Vector2>
                        { new(-3, supportY), new(3, supportY) });
                    SceneManager.MoveGameObjectToScene(platform.gameObject, scene);
                    var playerObject = new GameObject("BottomFallPlayer");
                    SceneManager.MoveGameObjectToScene(playerObject, scene);
                    playerObject.layer = LayerMask.NameToLayer("Player");
                    playerObject.AddComponent<SpriteRenderer>();
                    var body = playerObject.AddComponent<Rigidbody2D>();
                    body.gravityScale = 2.2f;
                    body.constraints = RigidbodyConstraints2D.FreezeRotation;
                    var circle = playerObject.AddComponent<CircleCollider2D>();
                    circle.radius = .4f;
                    circle.offset = new Vector2(0, .1f);
                    var player = playerObject.AddComponent<PlayerController>();
                    var jump = playerObject.AddComponent<AutoJump>();
                    Set(player, "cam", camera);
                    // 시작 때 캐시한 값과 실제 카메라 크기가 달라져도 실제 하단을 따른다.
                    Set(player, "camHalfHeight", 5f);
                    Set(player, "damageInvulnerableUntil", Time.time - 1);
                    if (shield) player.GrantShield();
                    body.position = new Vector2(0, supportY + .8f);
                    body.linearVelocity = new Vector2(0, -3);
                    Physics2D.SyncTransforms();
                    bool recovered = false;
                    bool regularJump = false;
                    for (int step = 0; step < 180; step++)
                    {
                        Call(player, "FixedUpdate");
                        physics.Simulate(.02f);
                        // 수동 물리 루프에서는 Unity의 프레임 시간이 흐르지 않는다.
                        // 접지 충전 시간도 같은 시뮬레이션 간격으로 진행시킨다.
                        if (player.IsGrounded)
                            Set(jump, "chargeTimer", (float)typeof(AutoJump).GetField("chargeTimer", Private).GetValue(jump) + .02f);
                        Call(jump, "Update");
                        if (body.linearVelocity.y > 30)
                        {
                            recovered = body.position.y >= bottom + .75f && !player.IsGrounded && player.CurrentPlatform == null;
                            break;
                        }
                        regularJump |= body.linearVelocity.y > 5;
                    }
                    string label = $"half={halfHeight}, shield={shield}, visible={visible}";
                    int expectedHealth = visible || shield ? 3 : 2;
                    bool expectedShield = visible && shield;
                    if (player.CurrentHealth != expectedHealth || player.HasShield != expectedShield ||
                        recovered != !visible || (visible && !regularJump))
                        errors.Add($"{label}: HP={player.CurrentHealth}, shield={player.HasShield}, recovery={recovered}, normalJump={regularJump}");
                    Object.DestroyImmediate(playerObject);
                    Object.DestroyImmediate(platform.gameObject);
                    Object.DestroyImmediate(cameraObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                typeof(GameManager).GetProperty("Instance").SetValue(null, previousManager);
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                Application.runInBackground = oldBackground;
            }
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
