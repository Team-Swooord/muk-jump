using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    // 무음 PCM의 isPlaying만으로 실기기 청취 성공을 판정하지 않는다.
    public sealed class GameplayAudioRegressionTests
    {
        const string SceneKey = "MukJump.AudioTests.SceneSetup";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        GameFeedbackController feedback;
        VfxAudioManager pool;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(EditorSceneManager.GetSceneManagerSetup().All(s =>
                !SceneManager.GetSceneByPath(s.path).isDirty), Is.True);
            SessionState.SetBool(SceneKey + ".RunInBackground", Application.runInBackground);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            // 음향 fixture의 설정 변경을 계정의 저장 대기 표식에 전달하지 않는다.
            if (MukJumpAccountRuntime.Instance != null) MukJumpAccountRuntime.Instance.enabled = false;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            AudioListener.pause = false;
            Application.runInBackground = true;
            host = new GameObject("AudioRegression", typeof(AudioListener));
            var cameraObject = new GameObject("AudioTestCamera", typeof(Camera));
            cameraObject.transform.SetParent(host.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().orthographic = true;
            cameraObject.GetComponent<Camera>().orthographicSize = 8f;
            pool = host.AddComponent<VfxAudioManager>();
            feedback = host.AddComponent<GameFeedbackController>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
            {
                if (host != null) Object.DestroyImmediate(host);
                AudioListener.pause = false;
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                yield return new ExitPlayMode();
            }
            Application.runInBackground = SessionState.GetBool(SceneKey + ".RunInBackground", false);
            SessionState.EraseBool(SceneKey + ".RunInBackground");
        }

        [UnityTest]
        public IEnumerator BrushRestartsStoppedCursorAndRecoversDuringMovement()
        {
            var source = Get<AudioSource>(feedback, "brushSource");
            AssertNonSilent(source.clip);
            // OS/장치 재시작 등으로 재생 세션만 사라지고 위치가 남은 상태를 재현한다.
            source.Stop();
            source.timeSamples = source.clip.samples / 4;
            feedback.StartBrushDrawing();
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(source.isPlaying, Is.True, "정지된 소스는 UnPause만으로 시작할 수 없다.");
            source.Stop();
            feedback.PlayBrushMovement(.25f);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(source.isPlaying, Is.True, "현재 획의 다음 움직임에서 붓 소리가 복구되어야 한다.");
            feedback.StopBrushDrawing();
            feedback.PlayBrushMovement(.25f);
            yield return null;
            Assert.That(source.isPlaying, Is.False, "종료된 획의 늦은 이동 알림은 소리를 켜지 않는다.");
        }

        [UnityTest]
        public IEnumerator PausedRequestsAreDiscardedInsteadOfBurstingAfterResume()
        {
            AudioClip wall = Get<AudioClip>(feedback, "wallHitClip");
            AudioListener.pause = true;
            for (int i = 0; i < 20; i++) pool.PlayOneShot(wall);
            feedback.StartBrushDrawing();
            feedback.StopBrushDrawing();
            Assert.That(Get<int>(pool, "nextSource"), Is.Zero,
                "메뉴/광고/안내 정지 중 요청은 슬롯과 재생 대기열을 소비하지 않는다.");
            AudioListener.pause = false;
            yield return null;
            Assert.That(Get<AudioSource[]>(pool, "sources").Any(s => s.isPlaying), Is.False);
            Assert.That(Get<AudioSource>(feedback, "brushSource").isPlaying, Is.False);
            pool.PlayOneShot(wall);
            yield return null;
            Assert.That(Get<AudioSource[]>(pool, "sources").Any(s => s.isPlaying), Is.True);
            pool.StopAll();
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            pool.PlayOneShot(wall);
            feedback.StartBrushDrawing();
            yield return null;
            Assert.That(Get<int>(pool, "nextSource"), Is.Zero);
            Assert.That(Get<AudioSource>(feedback, "brushSource").isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator RealClipsReuseSixSlotsAndRespectMuteAndStop()
        {
            var clips = new[] { Get<AudioClip>(feedback, "brushLoopClip"),
                Get<AudioClip>(feedback, "wallHitClip"), Get<AudioClip>(feedback, "jumpClip") };
            foreach (AudioClip clip in clips) AssertNonSilent(clip);
            Assert.That(pool.isActiveAndEnabled, Is.True);
            for (int i = 0; i < 6; i++) pool.PlayOneShot(clips[0], .12f);
            var sources = Get<AudioSource[]>(pool, "sources");
            yield return null;
            Assert.That(sources.Count(s => s.isPlaying), Is.EqualTo(6));
            for (int i = 0; i < 18; i++) pool.PlayOneShot(clips[i % 3], .12f);
            yield return null;
            Assert.That(pool.GetComponents<AudioSource>().Length, Is.EqualTo(6));
            Assert.That(sources.Count(s => s.isPlaying), Is.EqualTo(6));
            int next = Get<int>(pool, "nextSource");
            LobbySettingsProfile.SetSfxVolume(0f);
            pool.PlayOneShot(clips[1]);
            Assert.That(Get<int>(pool, "nextSource"), Is.EqualTo(next));
            pool.StopAll();
            Assert.That(sources.All(s => !s.isPlaying), Is.True);
            feedback.StartBrushDrawing();
            yield return null;
            Assert.That(Get<AudioSource>(feedback, "brushSource").volume, Is.Zero);
            feedback.StopBrushDrawing();
            LobbySettingsProfile.SetSfxVolume(.5f);
            for (int i = 0; i < 9; i++)
            {
                pool.PlayOneShot(clips[1], .5f);
                yield return new WaitForSecondsRealtime(.2f);
            }
            yield return new WaitForSecondsRealtime(clips[1].length + .1f);
            Assert.That(sources.All(s => !s.isPlaying), Is.True);
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(.5f));
            pool.enabled = false;
            pool.enabled = true;
            pool.PlayOneShot(clips[2]);
            yield return null;
            Assert.That(sources.Any(s => s.isPlaying), Is.True);
        }

        [UnityTest]
        public IEnumerator MusicKeepsAdAndBackgroundPauseReasonsSeparate()
        {
            var music = new GameObject("MusicRegression").AddComponent<BackgroundMusicController>();
            var source = music.GetComponent<AudioSource>();
            try
            {
                yield return null;
                Assert.That(source.isPlaying, Is.True);
                AudioListener.pause = true;
                yield return null;
                Assert.That(source.isPlaying, Is.True, "메뉴에서는 BGM만 계속된다.");
                Assert.That(Get<AudioSource>(feedback, "brushSource").ignoreListenerPause, Is.False);
                music.SetApplicationActive(false);
                music.SetFullScreenAdActive(true);
                music.SetFullScreenAdActive(false);
                yield return null;
                Assert.That(source.isPlaying, Is.False, "광고 종료가 백그라운드 정지를 해제하지 않는다.");
                music.SetApplicationActive(true);
                yield return null;
                Assert.That(source.isPlaying, Is.True);
                Assert.That(AudioListener.pause, Is.True, "BGM 복귀가 게임의 정지를 임의로 해제하지 않는다.");
                music.SetFullScreenAdActive(true);
                music.SetApplicationActive(false);
                music.SetApplicationActive(true);
                Assert.That(source.isPlaying, Is.False, "앱 복귀가 진행 중인 광고 음소거를 해제하지 않는다.");
                music.SetFullScreenAdActive(false);
                AudioListener.pause = false;
                feedback.StartBrushDrawing();
                yield return null;
                Assert.That(source.isPlaying, Is.True);
                Assert.That(Get<AudioSource>(feedback, "brushSource").isPlaying, Is.True);
            }
            finally { if (music != null) Object.DestroyImmediate(music.gameObject); }
        }

        [UnityTest]
        public IEnumerator DenseWallAndJumpMixHasHeadroomAndDiagnosticExplainsGates()
        {
            AudioPlaybackDiagnostics.Snapshot wallRequest = null, jumpRequest = null, blocked = null;
            void Observe(AudioPlaybackDiagnostics.Snapshot snapshot)
            {
                if (snapshot.reason == "pool-play" && snapshot.clipName == "ObstaclePunchImpact") wallRequest = snapshot;
                if (snapshot.reason == "pool-play" && snapshot.clipName == "JumpBrush") jumpRequest = snapshot;
                if (snapshot.reason == "pool-suspended") blocked = snapshot;
            }
            AudioPlaybackDiagnostics.Captured += Observe;
            var music = new GameObject("MixRegression").AddComponent<BackgroundMusicController>();
            try
            {
                feedback.StartBrushDrawing();
                feedback.PlayBrushMovement(.5f);
                feedback.PlayWallHit(Vector3.zero, 1f);
                feedback.PlayJump(Vector3.zero);
                yield return null;
                Assert.That(wallRequest, Is.Not.Null);
                Assert.That(jumpRequest, Is.Not.Null);
                var brush = Get<AudioSource>(feedback, "brushSource");
                AudioClip wall = Get<AudioClip>(feedback, "wallHitClip");
                AudioClip jump = Get<AudioClip>(feedback, "jumpClip");
                AudioClip bgm = music.GetComponent<AudioSource>().clip;
                float musicGain = Get<float>(music, "playingVolume");
                var brushPcm = Samples(brush.clip);
                var originalBrush = Resources.Load<AudioClip>("MukJump/Audio/SFX/SFX_Brush_Community");
                var originalPcm = Samples(originalBrush);
                Assert.That(brush.clip.samples, Is.EqualTo(originalBrush.samples));
                double originalEnergy = originalPcm.Sum(v => (double)v * v);
                double balancedEnergy = brushPcm.Sum(v => (double)v * v);
                Assert.That(Math.Sqrt(balancedEnergy / originalEnergy), Is.InRange(3.5d, 4.5d));
                var wallPcm = Samples(wall);
                var jumpPcm = Samples(jump);
                var bgmPcm = Samples(bgm);
                float peak = 0f;
                int clipped = 0;
                // 실제 요청의 gain으로 벽 80ms·점프 100ms의 지속 밀집 상황을 합성한다.
                // 모바일 출력/청취 검증은 아니며 대표 음악 구간 양 채널의 수치적 여유 검사다.
                foreach (float musicOffset in new[] { 0f, 13f, 31f })
                    for (int i = 0; i < 44100 * 2; i++)
                    {
                        float t = i / 44100f;
                        float wallSum = 0f, jumpSum = 0f;
                        for (int k = 0; k < 3; k++) wallSum += At(wallPcm, wall, t % .08f + .08f * k, 0);
                        for (int k = 0; k < 2; k++) jumpSum += At(jumpPcm, jump, t % .1f + .1f * k, 0);
                        for (int channel = 0; channel < bgm.channels; channel++)
                        {
                            float sample = At(bgmPcm, bgm, musicOffset + t, channel) * musicGain +
                                At(brushPcm, brush.clip, t % brush.clip.length, 0) * brush.volume +
                                wallSum * wallRequest.requestedGain + jumpSum * jumpRequest.requestedGain;
                            peak = Mathf.Max(peak, Mathf.Abs(sample));
                            if (Mathf.Abs(sample) >= 1f) clipped++;
                        }
                    }
                string evidence = System.IO.Path.Combine("output/qa/toss-recovery-20260925", "mix.json");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(evidence));
                System.IO.File.WriteAllText(evidence, Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    simulated = true, actualDeviceListeningVerified = false, peak, clipped,
                    musicGain, brushGain = brush.volume, wallGain = wallRequest.requestedGain,
                    jumpGain = jumpRequest.requestedGain,
                    brushPeak = brushPcm.Max(v => Mathf.Abs(v)), wallPeak = wallPcm.Max(v => Mathf.Abs(v)),
                    jumpPeak = jumpPcm.Max(v => Mathf.Abs(v)),
                }, Newtonsoft.Json.Formatting.Indented));
                Assert.That(clipped, Is.Zero, "지속적인 벽/점프와 BGM/붓의 대표 혼합에 클리핑이 없어야 한다.");
                Assert.That(peak, Is.LessThan(.98f));
                AudioListener.pause = true;
                pool.PlayOneShot(wall);
                Assert.That(blocked, Is.Not.Null);
                Assert.That(blocked.listenerPaused, Is.True);
                Assert.That(blocked.sfxSetting, Is.EqualTo(1f));
                Assert.That(blocked.clipSamples, Is.GreaterThan(0));
                Assert.That(blocked.loadState, Is.EqualTo("Loaded"));
                Assert.That(AudioListener.pause, Is.True);
            }
            finally
            {
                AudioPlaybackDiagnostics.Captured -= Observe;
                if (music != null) Object.DestroyImmediate(music.gameObject);
            }
        }

        static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(data, 0), Is.True);
            return data;
        }

        [UnityTest]
        public IEnumerator PhysicsLandingWallContactAndPlayerJumpReachAudioPool()
        {
            var played = new System.Collections.Generic.HashSet<string>();
            void Observe(AudioPlaybackDiagnostics.Snapshot s) { if (s.reason == "pool-play") played.Add(s.clipName); }
            AudioPlaybackDiagnostics.Captured += Observe;
            Scene fixture = SceneManager.CreateScene("AudioContactPhysics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var managerObject = new GameObject("AudioContactState");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            // 영속화/로비 생성 없이 플레이 상태만 fixture에 제공한다.
            typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
            typeof(GameManager).GetProperty("State").SetValue(manager, GameState.Playing);
            try
            {
                var ground = new GameObject("ContactGround", typeof(BoxCollider2D));
                SceneManager.MoveGameObjectToScene(ground, fixture);
                ground.GetComponent<BoxCollider2D>().size = new Vector2(6f, .2f);
                var wall = new GameObject("ContactWall", typeof(BoxCollider2D), typeof(MukJump.Player.ScreenSideWall));
                SceneManager.MoveGameObjectToScene(wall, fixture);
                wall.transform.position = new Vector3(2f, 1f);
                wall.GetComponent<BoxCollider2D>().size = new Vector2(.2f, 6f);
                var playerObject = new GameObject("ContactPlayer", typeof(Rigidbody2D), typeof(CircleCollider2D));
                SceneManager.MoveGameObjectToScene(playerObject, fixture);
                playerObject.GetComponent<CircleCollider2D>().radius = .2f;
                var player = playerObject.AddComponent<MukJump.Player.PlayerController>();
                var autoJump = playerObject.AddComponent<MukJump.Player.AutoJump>();
                autoJump.enabled = false;
                var body = player.Body;
                body.gravityScale = 0f;
                body.position = new Vector2(0f, 1f);
                body.linearVelocity = Vector2.down * 3f;
                Physics2D.SyncTransforms();
                for (int i = 0; i < 20; i++) fixture.GetPhysicsScene2D().Simulate(.02f);
                yield return null;
                Assert.That(played, Does.Contain("LandingInk"), "실제 착지 충돌이 재생 요청까지 이어진다.");
                typeof(MukJump.Player.AutoJump).GetMethod("Jump", Private).Invoke(autoJump, null);
                yield return null;
                Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
                Assert.That(played, Does.Contain("JumpBrush"), "AutoJump의 실제 발사 경로가 점프음을 요청한다.");
                body.gravityScale = 0f;
                body.position = new Vector2(1.3f, 1f);
                body.linearVelocity = Vector2.right * 5f;
                Physics2D.SyncTransforms();
                for (int i = 0; i < 12; i++) fixture.GetPhysicsScene2D().Simulate(.02f);
                yield return null;
                Assert.That(played, Does.Contain("ObstaclePunchImpact"), "ScreenSideWall의 실제 접촉이 하단 복귀와 같은 충돌음을 요청한다.");
            }
            finally
            {
                AudioPlaybackDiagnostics.Captured -= Observe;
                foreach (var root in fixture.GetRootGameObjects()) Object.DestroyImmediate(root);
                typeof(GameManager).GetProperty("Instance").SetValue(null, null);
                Object.DestroyImmediate(managerObject);
            }
            yield return SceneManager.UnloadSceneAsync(fixture);
        }

        [UnityTest]
        public IEnumerator FallRecoveryWallContactPlaysOnceWithoutDamageOrRepeatedStaySound()
        {
            int requests = 0;
            void Observe(AudioPlaybackDiagnostics.Snapshot s)
            { if (s.reason == "pool-play" && s.clipName == "ObstaclePunchImpact") requests++; }
            AudioPlaybackDiagnostics.Captured += Observe;
            var playerObject = new GameObject("RecoveryWallPlayer", typeof(Rigidbody2D), typeof(CircleCollider2D));
            var player = playerObject.AddComponent<MukJump.Player.PlayerController>();
            try
            {
                typeof(MukJump.Player.PlayerController).GetField("centeringAfterFall", Private).SetValue(player, true);
                typeof(MukJump.Player.PlayerController).GetField("fallRecoveryCenterX", Private).SetValue(player, 100f);
                player.Body.linearVelocity = Vector2.up * 10f;
                int health = player.CurrentHealth;
                var contact = typeof(MukJump.Player.PlayerController).GetMethod("TryHandleFallRecoveryWallContact", Private);
                for (int i = 0; i < 10; i++) Assert.That(contact.Invoke(player, new object[] { 1f }), Is.True);
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(player.CurrentHealth, Is.EqualTo(health));
                Assert.That(Get<AudioClip>(feedback, "wallHitClip"), Is.SameAs(Get<AudioClip>(feedback, "damageHitClip")));
                yield return null;
            }
            finally
            {
                AudioPlaybackDiagnostics.Captured -= Observe;
                Object.DestroyImmediate(playerObject);
            }
        }

        static float At(float[] data, AudioClip clip, float seconds, int channel)
        {
            int frame = Mathf.FloorToInt(seconds * clip.frequency);
            return frame >= clip.samples ? 0f : data[frame * clip.channels + channel];
        }

        static void AssertNonSilent(AudioClip clip)
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            Assert.That(samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True);
            Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(.01f, 1f));
        }

        static T Get<T>(object instance, string field) =>
            (T)instance.GetType().GetField(field, Private).GetValue(instance);
    }
}
