using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class ReleaseGameplayAuditTests
    {
        // 실행 도중 자정을 넘어도 생성한 증거 폴더와 캡처 경로가 달라지지 않는다.
        static readonly string Evidence = Path.Combine("output/release-qa",
            "compatibility-" + DateTime.Now.ToString("yyyy-MM-dd"));
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> problems = new();

        [Test]
        public void EveryRuntimeBehaviourHasAResolvableMonoScript()
        {
            var registered = new HashSet<Type>(MonoImporter.GetAllRuntimeMonoScripts()
                .Select(script => script.GetClass()).Where(type => type != null));
            var missing = typeof(GameManager).Assembly.GetTypes().Where(type => !type.IsAbstract &&
                typeof(MonoBehaviour).IsAssignableFrom(type) && !registered.Contains(type)).Select(type => type.FullName).ToArray();
            Assert.That(missing, Is.Empty, "재컴파일/씬 저장에서 Missing Script가 되는 컴포넌트입니다.");
        }

        [Test]
        public void GrowthBloomRepairsDestroyedComponentsWithoutDuplicatingItsPool()
        {
            var host = new GameObject("BloomLifecycleAudit", typeof(RectTransform));
            try
            {
                var view = host.AddComponent<GrowthBloomPresentation>();
                view.Initialize((RectTransform)host.transform);
                var pool = host.transform.Find("GrowthBloom");
                int count = pool.GetComponentsInChildren<Transform>(true).Length;
                Object.DestroyImmediate(pool.GetComponent<CanvasGroup>());
                view.Initialize((RectTransform)host.transform);
                Assert.That(pool.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(pool.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator GrowthTweenCompletesAndCancelsInRealPlayAtZeroTimeScale()
        {
            yield return new EnterPlayMode();
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            float oldScale = Time.timeScale;
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 0f;
            var host = new GameObject("GrowthTweenRuntimeAudit", typeof(RectTransform), typeof(Canvas));
            try
            {
                var icon = new GameObject("Focus", typeof(RectTransform)).GetComponent<RectTransform>();
                icon.SetParent(host.transform, false);
                var marks = new RectTransform[3];
                for (int i = 0; i < marks.Length; i++)
                {
                    marks[i] = new GameObject("Mark" + i, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    marks[i].SetParent(host.transform, false);
                    marks[i].anchoredPosition = new Vector2(i * 50f, -100f);
                }
                var bloom = host.AddComponent<GrowthBloomPresentation>();
                bloom.Initialize((RectTransform)host.transform);
                int poolSize = host.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < marks.Length; i++)
                {
                    bloom.Play(icon, marks[i], marks);
                    Assert.That(Get(bloom, "progressSequence"), Is.Not.Null, "실제 DOTween 모듈을 초기화한다.");
                    yield return WaitReal(GrowthBloomPresentation.Duration + .2f);
                    Assert.That(bloom.IsPlaying, Is.False);
                    Assert.That(Get(bloom, "progressSequence"), Is.Null);
                    Assert.That(icon.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(Vector2.Distance(marks[i].anchoredPosition, new Vector2(i * 50f, -100f)), Is.LessThan(.001f));
                }
                bloom.PlayReset(icon, marks.Select(mark => mark.position).ToArray());
                yield return WaitReal(GrowthBloomPresentation.Duration + .2f);
                Assert.That(bloom.IsPlaying, Is.False);
                bloom.Play(icon, marks[1], marks);
                yield return WaitReal(.1f);
                host.SetActive(false);
                Assert.That(bloom.IsPlaying, Is.False);
                Assert.That(Get(bloom, "progressSequence"), Is.Null);
                host.SetActive(true);
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                bloom.Play(icon, marks[2], marks);
                Assert.That(Get(bloom, "progressSequence"), Is.Null);
                yield return WaitReal(GrowthBloomPresentation.Duration + .2f);
                Assert.That(bloom.IsPlaying, Is.False);
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(poolSize));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Time.timeScale = oldScale;
                Application.runInBackground = oldBackground;
                LobbySettingsProfile.RestoreDefaultStoreForTests();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator MainSceneAllMapsDay()
        {
            yield return new EnterPlayMode();
            yield return RunMatrix(false);
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AudioPoolStopsAndRecoversInRealMainScene()
        {
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            AudioClip clip = null;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                yield return WaitReal(.8f);
                var pool = VfxAudioManager.Instance;
                Assert.That(pool, Is.Not.Null);
                clip = AudioClip.Create("SilentPoolRegression", 22050 * 2, 1, 22050, false);
                var sources = (AudioSource[])Get(pool, "sources");
                pool.StopAll();
                for (int i = 0; i < sources.Length; i++) pool.PlayOneShot(clip);
                yield return null;
                Assert.That(sources.All(source => source.isPlaying), Is.True, "실제 오디오 엔진의 모든 재생 슬롯을 채운다.");
                int next = (int)Get(pool, "nextSource");
                pool.PlayOneShot(clip, 0f);
                Assert.That((int)Get(pool, "nextSource"), Is.EqualTo(next));
                pool.enabled = false;
                yield return null;
                Assert.That(sources.All(source => !source.isPlaying), Is.True);
                Assert.That(VfxAudioManager.Instance, Is.Null);
                Object.Destroy(sources[0]);
                yield return null;
                pool.enabled = true;
                Assert.That(VfxAudioManager.Instance, Is.SameAs(pool));
                var repaired = (AudioSource[])Get(pool, "sources");
                Assert.That(repaired.Length, Is.EqualTo(sources.Length));
                Assert.That(repaired.All(source => source != null), Is.True);
                Assert.That(pool.GetComponents<AudioSource>().Length, Is.EqualTo(sources.Length));
                pool.PlayOneShot(clip);
                yield return null;
                Assert.That(repaired.Any(source => source.isPlaying), Is.True);
                pool.StopAll();
                Assert.That(repaired.All(source => !source.isPlaying), Is.True);
            }
            finally
            {
                if (VfxAudioManager.Instance != null) VfxAudioManager.Instance.StopAll();
                if (clip != null) Object.DestroyImmediate(clip);
                if (Application.isPlaying)
                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                Application.runInBackground = oldBackground;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AccountDeletionReturnsThroughSplashBeforePausedTutorial()
        {
            yield return new EnterPlayMode();
            yield return RunDeletionSplashFlow();
            yield return new ExitPlayMode();
        }

        static IEnumerator RunDeletionSplashFlow()
        {
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Time.timeScale = 1;
            AudioListener.pause = false;
            var loaded = new List<string>();
            void OnLoaded(Scene scene, LoadSceneMode mode) => loaded.Add(scene.name);
            SceneManager.sceneLoaded += OnLoaded;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                yield return WaitReal(.2f);
                var oldManager = GameManager.Instance;
                Assert.That(oldManager.State, Is.EqualTo(GameState.Lobby));
                loaded.Clear();
                // 서버를 삭제하지 않고, 검증된 삭제의 로컬 완료 경계부터 실제 씬 이동을 검사한다.
                Assert.That(LobbySettingsProfile.TryResetForAccountDeletion(), Is.True);
                var restart = typeof(StartupBrandSplash).GetMethod("TryRestartAfterAccountDeletion",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(restart.Invoke(null, null), Is.True);
                Assert.That(StartupBrandSplash.IsBlockingInput, Is.True);
                Assert.That(oldManager.State, Is.EqualTo(GameState.Lobby));
                int fadeInFrames = 0;
                int fadeOutFrames = 0;
                bool sawFullLogo = false;
                string brandEvidence = Path.Combine(Evidence, "brand-motion-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(brandEvidence);
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (Time.realtimeSinceStartupAsDouble < deadline &&
                       (loaded.Count < 2 || StartupBrandSplash.IsBlockingInput ||
                        FirstRunTutorialController.Instance == null ||
                        !FirstRunTutorialController.Instance.IsActive))
                {
                    var brand = Object.FindAnyObjectByType<StartupBrandSplash>();
                    var logo = brand == null ? null : brand.transform.Find("StartupBrandCanvas/Logo")?.GetComponent<Image>();
                    if (logo != null)
                    {
                        float alpha = logo.color.a;
                        if (alpha > .05f && alpha < .95f)
                        {
                            if (sawFullLogo) fadeOutFrames++; else fadeInFrames++;
                            if (alpha > .4f && alpha < .6f)
                            {
                                string filename = sawFullLogo ? "brand-fade-out.png" : "brand-fade-in.png";
                                if (!File.Exists(Path.Combine(brandEvidence, filename)))
                                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(brandEvidence, filename)));
                            }
                        }
                        if (alpha >= .999f && !sawFullLogo)
                        {
                            sawFullLogo = true;
                            // Inspector의 알파 값만 바뀌고 화면은 계속 투명한 회귀도 잡는다.
                            Canvas.ForceUpdateCanvases();
                            var renderedLogo = logo.canvasRenderer.GetMesh();
                            Assert.That(renderedLogo, Is.Not.Null);
                            Assert.That(renderedLogo.vertexCount, Is.GreaterThan(0));
                            Assert.That(renderedLogo.colors32.Any(color => color.a >= 250), Is.True,
                                "완전히 등장한 로고의 실제 Canvas 정점이 불투명해야 한다.");
                            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(brandEvidence, "brand-hold.png")));
                            var corners = new Vector3[4];
                            logo.rectTransform.GetWorldCorners(corners);
                            // CanvasScaler는 renderingDisplaySize를 사용한다. Device Simulator가
                            // 보고하는 Screen.width와 혼용하지 않고 같은 Canvas 좌표로 비교한다.
                            var canvasCorners = new Vector3[4];
                            ((RectTransform)logo.canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
                            float canvasWidth = canvasCorners[3].x - canvasCorners[0].x;
                            Assert.That(canvasWidth, Is.GreaterThan(0f));
                            Assert.That(corners[3].x - corners[0].x, Is.LessThan(canvasWidth * .8f),
                                "투명 여백을 포함한 전체 원본 로고가 화면 폭을 넘지 않아야 합니다.");
                        }
                    }
                    yield return null;
                }
                Assert.That(sawFullLogo, Is.True, "로딩이 빨라도 로고를 한 번 온전히 표시한다.");
                Assert.That(fadeInFrames, Is.GreaterThan(4), "실제 프레임에 중간 알파가 있어야 한다.");
                Assert.That(fadeOutFrames, Is.GreaterThan(4), "씬 활성화가 로고 퇴장을 잘라서는 안 된다.");
                Assert.That(loaded, Is.EqualTo(new[] { "Splash", "Main" }));
                Assert.That(GameManager.Instance, Is.Not.SameAs(oldManager));
                Assert.That(FirstRunTutorialController.Instance.IsActive, Is.True);
                Assert.That(FirstRunTutorialController.Instance.CurrentStep, Is.Zero);
                Assert.That(GameManager.Instance.PauseReason, Is.EqualTo(GameplayPauseReason.FirstRunTutorial));
                yield return WaitReal(.5f);
                Assert.That(GameManager.Instance.IsGameplayTicking, Is.False);
                Assert.That(Time.timeScale, Is.Zero);

                // 안내 완료 뒤 앱을 다시 켠 상황은 Splash를 지나도 자동으로 새 판을 시작하지 않는다.
                Assert.That(LobbySettingsProfile.TryMarkGameplayTutorialCompleted(), Is.True);
                LobbySettingsProfile.UseStoreForTests(settings);
                loaded.Clear();
                yield return SceneManager.LoadSceneAsync(StartupBrandSplash.SceneName);
                deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (Time.realtimeSinceStartupAsDouble < deadline &&
                       (loaded.Count < 2 || StartupBrandSplash.IsBlockingInput))
                    yield return WaitReal(.1f);
                Assert.That(loaded, Is.EqualTo(new[] { "Splash", "Main" }));
                yield return WaitReal(1f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(FirstRunTutorialController.Instance.IsActive, Is.False);
            }
            finally
            {
                SceneManager.sceneLoaded -= OnLoaded;
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PointerInput.ResetSuppressionForTests();
                Time.timeScale = 1;
                AudioListener.pause = false;
                Application.runInBackground = oldBackground;
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator IncompleteTutorialNeverAutoRestartsAfterResultOrPauseExit()
        {
            yield return new EnterPlayMode();
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Time.timeScale = 1;
            AudioListener.pause = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main");
                for (int run = 0; run < 3; run++)
                {
                    // 최초 설치만 자동 진입, 이후 두 판은 명시적인 시작 버튼으로 진입한다.
                    if (run > 0) Object.FindAnyObjectByType<LobbyView>().StartButton.onClick.Invoke();
                    double deadline = Time.realtimeSinceStartupAsDouble + 12;
                    while (Time.realtimeSinceStartupAsDouble < deadline &&
                           (GameManager.Instance.State != GameState.Playing || GameManager.Instance.IsTransitioning))
                        yield return WaitReal(.1f);
                    var manager = GameManager.Instance;
                    Assert.That(manager.State, Is.EqualTo(GameState.Playing));
                    Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.False);
                    // 완료 저장이 없는 경우에도 로비를 강제로 다시 시작하면 안 된다.
                    var tutorial = Object.FindAnyObjectByType<FirstRunTutorialController>();
                    Assert.That(tutorial.IsActive, Is.True,
                        "로고 없이 진입해 붓 전환을 거쳐도 첫 안내를 생략하면 안 된다.");
                    Assert.That(manager.PauseReason, Is.EqualTo(GameplayPauseReason.FirstRunTutorial));
                    typeof(FirstRunTutorialController).GetMethod("EndWithoutCompletion", Private).Invoke(tutorial, null);
                    Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.True);
                    yield return WaitForGameplay();
                    if (run == 1)
                    {
                        Assert.That(manager.PauseGame(), Is.True);
                        Assert.That(manager.ReturnToLobby(), Is.True);
                    }
                    else
                    {
                        var players = new List<PlayerController>();
                        manager.GetLivingPlayersNonAlloc(players);
                        foreach (var player in players) player.Kill();
                        yield return WaitReal(2f);
                        Assert.That(manager.State, Is.EqualTo(GameState.GameOver));
                        Click(Object.FindAnyObjectByType<GameOverPopupView>(), "lobbyButton");
                    }
                    yield return WaitForLobby();
                    // 도착 순간만 검사하지 않고 자동 시작 Update와 전환 시간보다 오래 기다린다.
                    double idleUntil = Time.realtimeSinceStartupAsDouble + 4;
                    while (Time.realtimeSinceStartupAsDouble < idleUntil)
                    {
                        Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                        Assert.That(GameManager.Instance.IsTransitioning, Is.False);
                        yield return WaitReal(.1f);
                    }
                }
            }
            finally
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PointerInput.ResetSuppressionForTests();
                Time.timeScale = 1;
                AudioListener.pause = false;
                Application.runInBackground = oldBackground;
            }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(600000)]
        public IEnumerator MainSceneAllMapsNight()
        {
            yield return new EnterPlayMode();
            yield return RunMatrix(true);
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MainSceneNightFinalBand()
        {
            yield return new EnterPlayMode();
            yield return RunMatrix(true, 9);
            yield return new ExitPlayMode();
        }

        static IEnumerator RunMatrix(bool night, int firstBand = 0)
        {
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            Assert.That(Application.isPlaying, Is.True, "실제 Play 상태에서만 씬을 검사한다.");
            EditorApplication.isPaused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            problems.Clear();
            Application.logMessageReceived += CaptureProblem;
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Directory.CreateDirectory(Evidence);
            string report = Path.Combine(Evidence, firstBand == 0
                ? (night ? "maps-night.txt" : "maps-day.txt") : $"maps-night-band-{firstBand}.txt");
            File.WriteAllText(report, "Main scene real Play loop; isolated in-memory saves.\n");
            try
            {
                // 기본 7종과 무한 구간 3종의 반전 순환까지 검사한다.
                for (int band = firstBand; band < 10; band++)
                {
                    typeof(LobbyNightState).GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { night ? 1f : 0f, night, 0f, 0f, 0f });
                    yield return SceneManager.LoadSceneAsync("Main");
                    yield return WaitReal(.8f);
                    CheckSceneScripts();
                    var manager = GameManager.Instance;
                    Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
                    Assert.That(LobbyNightState.Blend, Is.EqualTo(night ? 1f : 0f).Within(.001f));
                    manager.StartGameFromMenu();
                    yield return WaitForGameplay();
                    // 경계 아래까지만 이동하고 실제 물리 상승으로 구간 전환한다.
                    if (band > 0)
                    {
                        manager.DebugTeleportToHeight(band * 250 - 2);
                        Assert.That(ScoreManager.Instance.HeightAt(manager.HighestLivingPlayer.Body.position.y),
                            Is.EqualTo(band * 250 - 2).Within(.01f), "보간된 Transform과 새 물리 고도의 원점이 일치해야 합니다.");
                    }
                    var player = manager.HighestLivingPlayer;
                    var debugControls = Object.FindAnyObjectByType<GameplayHudView>()?.transform.Find("ItemTestControls");
                    if (debugControls != null) debugControls.gameObject.SetActive(false);
                    Assert.That(player, Is.Not.Null);
                    player.LaunchToHeight(8f);
                    yield return WaitReal(1.8f);
                    var map = Object.FindAnyObjectByType<MapBackgroundView>();
                    int expected = HeightZoneController.ResolveMapStage(band, 4, 3);
                    Assert.That((int)Get(map, "currentStage"), Is.EqualTo(expected), "경계 통과 후 배경");
                    var renderer = (SpriteRenderer)Get(map, "currentRenderer");
                    Assert.That(renderer.sprite, Is.Not.Null);
                    Assert.That(renderer.flipX, Is.EqualTo(HeightZoneController.ResolveMapMirror(band, 4, 3)));
                    yield return DrawRealStroke(player);
                    Assert.That(ItemEffect.Apply(ItemType.GoldenBrush, player), Is.True);
                    // 실제 무작위 아이템을 먼저 먹은 경우에는 보호막 중복 획득이 거부되는 것이 정상이다.
                    bool alreadyShielded = player.HasShield;
                    Assert.That(ItemEffect.Apply(ItemType.InkShield, player), Is.EqualTo(!alreadyShielded));
                    Assert.That(player.HasShield, Is.True);
                    Assert.That(manager.TryCreateInkClone(player), Is.True);
                    WindWeatherController.Instance.DebugFlipDirection();
                    var weather = WindWeatherController.Instance;
                    WindWeatherPhase expectedWind;
                    if (band % 3 == 0) { weather.DebugTriggerUpdraft(); expectedWind = WindWeatherPhase.Updraft; }
                    else if (band % 3 == 1) { weather.DebugTriggerDowndraft(); expectedWind = WindWeatherPhase.Downdraft; }
                    else { weather.DebugTriggerGale(); expectedWind = WindWeatherPhase.Gale; }
                    yield return WaitReal(2.1f);
                    Assert.That(weather.Phase, Is.EqualTo(expectedWind), "예고 이후 실제 바람 활성 단계");
                    Assert.That(manager.PauseGame(), Is.True);
                    yield return WaitReal(.7f);
                    float pausedY = player.Body.position.y;
                    var pause = Object.FindAnyObjectByType<PauseMenuView>();
                    Click(pause, "lobbyButton");
                    yield return WaitReal(.7f);
                    Assert.That(((RectTransform)Get(pause, "exitPromptRoot")).gameObject.activeSelf, Is.True);
                    Click(pause, "cancelExitButton");
                    yield return WaitReal(.4f);
                    Assert.That(manager.IsPaused, Is.True);
                    Assert.That(player.Body.position.y, Is.EqualTo(pausedY).Within(.01f));
                    Click(pause, "resumeButton");
                    yield return WaitReal(.5f);
                    Assert.That(manager.IsGameplayTicking, Is.True);
                    ScreenCapture.CaptureScreenshot(Path.Combine(Evidence, $"{(night ? "night" : "day")}-band-{band:00}.png"));
                    yield return WaitReal(.2f);
                    var living = new List<PlayerController>();
                    manager.GetLivingPlayersNonAlloc(living);
                    Assert.That(living.Count, Is.GreaterThan(0));
                    foreach (var alive in living) alive.Kill();
                    yield return WaitReal(2f);
                    Assert.That(manager.State, Is.EqualTo(GameState.GameOver));
                    Assert.That(manager.LivingPlayerCount, Is.Zero);
                    Click(Object.FindAnyObjectByType<GameOverPopupView>(), "lobbyButton");
                    yield return WaitForLobby();
                    Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby), "결과창→로비 씬 재시작");
                    Assert.That(GameManager.Instance.IsTransitioning, Is.False);
                    Assert.That(LobbyNightState.Blend, Is.EqualTo(night ? 1f : 0f).Within(.001f));
                    CheckSceneScripts();
                    Assert.That(problems, Is.Empty, string.Join("\n", problems));
                    File.AppendAllText(report, $"PASS band={band} map={expected} boundary/drawing/items/wind/pause/cancel/resume/death/result/reload\n");
                }
                GameManager.Instance.StartGameFromMenu();
                // 붓 덮기·완전 암전·드러남이 모두 끝나야 일시정지 입력을 검사한다.
                // 예전 고정 1.2초 대기는 현재 전환 도중 PauseGame을 호출했다.
                yield return WaitForGameplay();
                Assert.That(GameManager.Instance.PauseGame(), Is.True);
                yield return WaitReal(.7f);
                var exit = Object.FindAnyObjectByType<PauseMenuView>();
                Click(exit, "lobbyButton");
                yield return WaitReal(.7f);
                Click(exit, "confirmExitButton");
                yield return WaitForLobby();
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                Assert.That(problems, Is.Empty, string.Join("\n", problems));
                File.AppendAllText(report, "PASS confirmed exit/reload; no runtime errors or missing scripts\n");
            }
            finally
            {
                Application.logMessageReceived -= CaptureProblem;
                if (Application.isPlaying)
                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
                PermanentGrowthProfile.RestoreDefaultStoreForTests();
                ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests();
                PointerInput.ResetSuppressionForTests();
                Time.timeScale = 1;
                AudioListener.pause = false;
                Application.runInBackground = oldBackground;
            }
        }

        static IEnumerator DrawRealStroke(PlayerController player)
        {
            var previous = Mouse.current;
            var mouse = InputSystem.AddDevice<Mouse>("ReleaseAuditMouse");
            var capture = Object.FindAnyObjectByType<StrokeCapture>();
            Assert.That(capture, Is.Not.Null);
            int created = 0;
            void OnCreated(PlatformCollider platform, float length, float cost)
            {
                Assert.That(platform, Is.Not.Null);
                Assert.That(length, Is.GreaterThan(0));
                created++;
            }
            capture.ValidStrokeCreated += OnCreated;
            try
            {
                PointerInput.ResetSuppressionForTests();
                Vector3 origin = player.transform.position + Vector3.down * 1.1f;
                origin.x = 0;
                for (int point = 0; point <= 8; point++)
                {
                    Vector2 position = Camera.main.WorldToScreenPoint(origin + Vector3.right * (-1.4f + point * .35f));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
                    yield return WaitReal(.025f);
                }
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Camera.main.WorldToScreenPoint(origin + Vector3.right * 1.4f) });
                yield return WaitReal(.08f);
                Assert.That(created, Is.EqualTo(1), "실제 포인터 드로잉→먹선 생성 (기존 발판 소멸 개수와 독립)");
            }
            finally
            {
                if (capture != null) capture.ValidStrokeCreated -= OnCreated;
                InputSystem.RemoveDevice(mouse);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }

        static IEnumerator WaitForGameplay()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            do { yield return WaitReal(.1f); }
            while (Time.realtimeSinceStartupAsDouble < deadline && (GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking));
            Assert.That(GameManager.Instance != null && GameManager.Instance.IsGameplayTicking,
                Is.True, "붓 덮기→암전 유지→드러남 이후 실제 물리 플레이가 시작돼야 한다");
        }

        static IEnumerator WaitForLobby()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            do { yield return WaitReal(.1f); }
            while (Time.realtimeSinceStartupAsDouble < deadline && (GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Lobby || GameManager.Instance.IsTransitioning));
            Assert.That(GameManager.Instance != null && GameManager.Instance.State == GameState.Lobby &&
                !GameManager.Instance.IsTransitioning, Is.True, "두루마리 닫힘→붓 덮기→실제 씬 로드→드러남 완료");
        }

        static IEnumerator WaitReal(float seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            int before = Time.frameCount;
            while (Time.realtimeSinceStartupAsDouble < deadline || Time.frameCount <= before)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
        }
        static object Get(object owner, string field) => owner.GetType().GetField(field, Private).GetValue(owner);
        static void Click(object owner, string field) => ((Button)Get(owner, field)).onClick.Invoke();
        static void CheckSceneScripts()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, "Missing Script: " + transform.name);
        }
        static void CaptureProblem(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception || message.Contains("referenced script"))
                problems.Add(message + "\n" + stack);
        }
    }
}
