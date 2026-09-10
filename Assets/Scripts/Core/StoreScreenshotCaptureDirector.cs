#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.EditorTools
{
    /// 승인된 스토어 장면을 실제 PlayerLoop로 촬영한다. 배포 코드에는 포함되지 않는다.
    [DefaultExecutionOrder(-32000)]
    public sealed class StoreScreenshotCaptureDirector : MonoBehaviour
    {
        const string Key = "MukJump.StoreCapture.";
        const int Width = 1320, Height = 2868, Fps = 30;
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        static bool installed;
        static double nextRequestPoll;
        readonly WaitForEndOfFrame frameEnd = new();
        readonly List<PlayerController> players = new();
        readonly List<string> evidence = new();
        GameManager manager;
        StrokeCapture stroke;
        string directory;
        int previousRate;
        bool previousBackground;
        double deadline;
        readonly List<(Vector2 center, float time)> drawnStrokes = new();
        Vector2 drawingFrom, drawingTo;
        int drawingStep;
        bool drawing;

        sealed class IdentityStore : MukJumpIdentityProfile.IStore
        {
            readonly Dictionary<string, string> data = new();
            public string Read(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;
            public void Write(string key, string value) => data[key] = value;
            public void Save() { }
        }

        [MenuItem("MukJump/스토어 촬영/한글 6장 원본과 영상 프레임")]
        static void ArmKorean() => Arm(false);
        [MenuItem("MukJump/스토어 촬영/영어 6장 원본과 영상 프레임")]
        static void ArmEnglish() => Arm(true);

        static void Arm(bool english, bool remainingOnly = false, bool proofOnly = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Play·컴파일·빌드 종료 후 시작하세요.");
            if (SessionState.GetBool("MukJump.QualityCapture.Armed", false) ||
                SessionState.GetBool(RecordingScenarioDirector.ArmSessionKey, false))
                throw new InvalidOperationException("다른 촬영 예약을 먼저 해제하세요.");
            ConfigureGameView();
            SessionState.SetBool(Key + "Active", true);
            SessionState.SetBool(Key + "Armed", true);
            SessionState.SetBool(Key + "English", english);
            SessionState.SetBool(Key + "RemainingOnly", remainingOnly);
            SessionState.SetBool(Key + "ProofOnly", proofOnly);
            EditorApplication.isPlaying = true;
        }

        // 계정 BeforeSceneLoad/Awake보다 먼저 메모리 저장을 설치한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void InstallStores()
        {
            if (!SessionState.GetBool(Key + "Armed", false)) return;
            MukJumpIdentityProfile.UseStoreForTests(new IdentityStore());
            var settings = new MemoryLobbySettingsStore();
            settings.SetInt("MukJump.Settings.TutorialSeen", 1);
            settings.SetInt("MukJump.Settings.GameplayTutorialVersion", LobbySettingsProfile.CurrentGameplayTutorialVersion);
            LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MukJumpAnalytics.SetCollectionEnabled(false);
            installed = true;
            SuspendDeviceSimulator();
            GameLocalization.SetLanguage(SessionState.GetBool(Key + "English", false)
                ? GameLanguage.English : GameLanguage.Korean);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!SessionState.GetBool(Key + "Armed", false)) return;
            SessionState.SetBool(Key + "Armed", false);
            if (!installed) throw new InvalidOperationException("촬영 메모리 저장소 미설치");
            var host = new GameObject("StoreScreenshotCapture_EDITOR_ONLY");
            DontDestroyOnLoad(host);
            host.AddComponent<StoreScreenshotCaptureDirector>();
        }

        [InitializeOnLoadMethod]
        static void RegisterCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update -= PollOneShotRequest;
            EditorApplication.update += PollOneShotRequest;
        }

        static void PollOneShotRequest()
        {
            if (EditorApplication.timeSinceStartup < nextRequestPoll) return;
            nextRequestPoll = EditorApplication.timeSinceStartup + 1;
            const string request = "Temp/MukJumpStoreCapture.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            if (File.Exists("Temp/MukJumpRunAllTests.request") || File.Exists("Temp/MukJumpRunAllTests.active")) return;
            string locale = File.ReadAllText(request).Trim();
            if (locale != "ko" && locale != "en" && locale != "ko-remaining" && locale != "en-remaining" &&
                locale != "ko-proof" && locale != "en-proof") return;
            // 명시적인 촬영 요청 한 건만 소비한다. 반복 예약은 만들지 않는다.
            File.Delete(request);
            try { Arm(locale.StartsWith("en", StringComparison.Ordinal),
                locale.EndsWith("-remaining", StringComparison.Ordinal), locale.EndsWith("-proof", StringComparison.Ordinal)); }
            catch (Exception ex)
            {
                File.WriteAllText("Temp/MukJumpStoreCapture.failed", ex.ToString());
                SessionState.SetBool(Key + "Armed", false);
                SessionState.SetBool(Key + "Active", false);
                RestoreGameView();
                Debug.LogError("[StoreCapture] START FAILED " + ex.Message);
            }
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + "Active", false)) return;
            SessionState.SetBool(Key + "Armed", false);
            SessionState.SetBool(Key + "Active", false);
            // 모든 런타임 OnDisable/OnDestroy가 끝날 때까지 메모리 저장을 유지한다.
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests();
            MukJumpIdentityProfile.UseStoreForTests(null);
            MobileUiLayout.ClearPlatformSafeAreaOverride();
            installed = false;
            RestoreGameView();
            RestoreDeviceSimulator();
        }

        IEnumerator Start()
        {
            directory = Path.GetFullPath("output/store-capture/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") +
                (SessionState.GetBool(Key + "English", false) ? "-en" : "-ko"));
            Directory.CreateDirectory(directory);
            previousRate = Time.captureFramerate;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 900;
            Debug.Log("[StoreCapture] START " + directory);
            // 중첩 코루틴의 예외도 잡아 Play와 촬영 상태를 정리한다.
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0)
            {
                object current = null;
                Exception failure = null;
                bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null)
                {
                    File.WriteAllText(Path.Combine(directory, "failed.txt"), failure.ToString());
                    Debug.LogError("[StoreCapture] FAILED " + failure.Message);
                    EditorApplication.isPlaying = false;
                    yield break;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator child) stack.Push(child);
                else yield return current;
            }
            File.WriteAllLines(Path.Combine(directory, "evidence.txt"), evidence);
            File.WriteAllText(Path.Combine(directory, "complete.txt"),
                $"Unity {Application.unityVersion}; actual GameView {Width}x{Height}; {Fps}fps; " +
                $"scene count={evidence.Count}; real gameplay effects; memory-only fixtures; no ad/debug UI; no image synthesis.");
            Debug.Log("[StoreCapture] COMPLETE " + directory);
            EditorApplication.isPlaying = false;
        }

        // 각 UI의 Update가 안전 영역을 계산하기 전에 광고 예약 여백을 없앤다.
        // 촬영 프레임만 교정하며 본체의 HUD/성장 배치 수치는 변경하지 않는다.
        void Update() => HideCaptureOnlyUi();

        void LateUpdate()
        {
            HideCaptureOnlyUi();
            if (EditorApplication.timeSinceStartup > deadline && deadline > 0)
            {
                if (directory != null) File.WriteAllText(Path.Combine(directory, "failed.txt"), "촬영 전체 제한시간 초과");
                EditorApplication.isPlaying = false;
            }
        }

        void HideCaptureOnlyUi()
        {
            SuspendDeviceSimulator();
            if (MukJumpAccountRuntime.Instance != null) MukJumpAccountRuntime.Instance.gameObject.SetActive(false);
            MukJumpAnalytics.SetCollectionEnabled(false);
            foreach (var ads in FindObjectsByType<EditorTestAdsRuntime>())
            {
                // 루트를 살려 두어 EnsureExists가 중복 생성하지 못하게 한다.
                ads.enabled = false;
                foreach (var canvas in ads.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            }
            LobbyAdLayout.ClearTopInset();
            foreach (var hud in FindObjectsByType<GameplayHudView>())
            {
                var controls = hud.transform.Find("ItemTestControls");
                if (controls != null) controls.gameObject.SetActive(false);
            }
            // iPhone 세로 안전영역만 흉내 낸다. 원본의 게임 UI를 잘라 내거나 그리지 않는다.
            if (Screen.width == Width && Screen.height == Height)
                MobileUiLayout.SetPlatformSafeAreaOverride(new Rect(0, 102, Width, Height - 282));
        }

        static Type SimulatorWindowType() => Type.GetType(
            "UnityEditor.DeviceSimulation.SimulatorWindow, UnityEditor.DeviceSimulatorModule", false);

        static void SuspendDeviceSimulator()
        {
            var type = SimulatorWindowType();
            if (type == null) return;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                object main = type.GetProperty("main", PublicInstance)?.GetValue(window);
                if (main == null) continue;
                // 다른 크기의 Simulator가 남긴 Screen.safeArea shim만 촬영 중 해제한다.
                // 창·기기 선택·ProjectSettings를 삭제하거나 변경하지 않는다.
                main.GetType().GetMethod("Disable", PublicInstance).Invoke(main, null);
                SessionState.SetBool(Key + "SimulatorSuspended", true);
            }
        }

        static void RestoreDeviceSimulator()
        {
            if (!SessionState.GetBool(Key + "SimulatorSuspended", false)) return;
            SessionState.SetBool(Key + "SimulatorSuspended", false);
            var type = SimulatorWindowType();
            if (type == null) return;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                if (type.GetField("m_State", PrivateInstance)?.GetValue(window)?.ToString() != "Enabled") continue;
                object main = type.GetProperty("main", PublicInstance)?.GetValue(window);
                main?.GetType().GetMethod("Enable", PublicInstance)?.Invoke(main, null);
            }
        }

        IEnumerator Ready(Func<bool> condition, string label)
        {
            double until = EditorApplication.timeSinceStartup + 45;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > until) throw new InvalidOperationException(label + " 시간 초과");
                yield return null;
            }
        }

        IEnumerator Run()
        {
            yield return Ready(() => GameManager.Instance != null && !StartupBrandSplash.IsBlockingInput &&
                Screen.width == Width && Screen.height == Height, "Main/고해상도 준비");
            manager = GameManager.Instance;
            yield return Ready(() => LobbyScreenNavigator.Instance != null &&
                LobbyScreenNavigator.Instance.CanStartGame, "로비 준비");
            // 정상 경제 API를 쓰되 메모리 지갑에만 적용하는 예시 진행도다.
            PermanentGrowthProfile.SettleRun("store-capture-fixture", 15000, 15000, 0, 300f, true);
            var types = new[] { PermanentGrowthType.Vitality, PermanentGrowthType.InkCapacity,
                PermanentGrowthType.InkBudgetEfficiency, PermanentGrowthType.JumpHeight };
            var ranks = new[] { 3, 4, 2, 3 };
            for (int type = 0; type < types.Length; type++)
                for (int rank = 0; rank < ranks[type]; rank++)
                    if (!PermanentGrowthProfile.TryPurchase(types[type])) throw new InvalidOperationException("성장 예시 준비 실패");
            bool remainingOnly = SessionState.GetBool(Key + "RemainingOnly", false);
            bool proofOnly = SessionState.GetBool(Key + "ProofOnly", false);
            if (!remainingOnly)
            {
                if (!LobbyScreenNavigator.Instance.OpenGrowth()) throw new InvalidOperationException("성장 열기 실패");
                yield return Ready(() => !LobbyScreenNavigator.Instance.IsTransitioning, "성장 전환");
                yield return new WaitForSecondsRealtime(.5f);
                yield return Capture("06-growth", proofOnly ? 3 : 120, null, false);
                if (!LobbyScreenNavigator.Instance.ReturnToLobby()) throw new InvalidOperationException("로비 복귀 실패");
                yield return Ready(() => LobbyScreenNavigator.Instance.CanStartGame, "로비 복귀");
            }

            int[] heights = { 0, 760, 1010, 1260, 1510 };
            string[] names = { "01-mountain-jump", "02-cliff-shield", "03-gate-clones", "04-lotus-golden", "05-river-boost" };
            int firstScene = remainingOnly || proofOnly ? 2 : 0;
            int lastScene = proofOnly ? 3 : names.Length;
            for (int scene = firstScene; scene < lastScene; scene++)
            {
                yield return PrepareRun(scene > firstScene);
                manager.DebugTeleportToHeight(heights[scene]);
                manager.HighestLivingPlayer.LaunchToHeight(8f);
                Time.captureFramerate = Fps;
                int targetCount = scene == 2 ? 10 : scene == 4 ? 5 : 1;
                // 분신을 격자로 순간이동하거나 동시에 발사하지 않는다.
                // 실제 아이템 분열의 시간차·팝 속도·물리·애니메이션을 그대로 사용한다.
                for (int frame = 0; frame < 36; frame++)
                {
                    if (frame == 4) DrawUnderLeader(.22f);
                    if (frame == 12 || frame == 25) TryAddNaturalClone(targetCount, frame);
                    StepStroke();
                    yield return frameEnd;
                }
                if (scene == 1) ItemEffect.Apply(ItemType.InkShield, manager.HighestLivingPlayer);
                if (scene == 3) ItemEffect.Apply(ItemType.GoldenBrush, manager.HighestLivingPlayer);
                int selectedScene = scene;
                yield return Capture(names[scene], 120, frame =>
                {
                    if (selectedScene == 1 && frame == 0) SpawnHaetae(heights[selectedScene]);
                    if (selectedScene == 4 && frame == 8) ItemEffect.Apply(ItemType.InkDrop, manager.HighestLivingPlayer);
                    if (frame % 13 == 5) TryAddNaturalClone(targetCount, frame);
                    if (selectedScene == 3 && (frame == 20 || frame == 70))
                        DrawAtViewport(frame == 20 ? .30f : .70f, frame == 20 ? .32f : .51f, 3.1f,
                            frame == 20 ? .30f : -.25f);
                    else if (selectedScene != 4 && (frame == 28 || frame == 88))
                        DrawUnderFallingPlayer(selectedScene == 0 ? .25f : -.22f);
                }, true);
            }
        }

        IEnumerator PrepareRun(bool restart)
        {
            Time.captureFramerate = 0;
            if (restart)
            {
                var old = manager;
                manager.Restart();
                yield return Ready(() => GameManager.Instance != null && GameManager.Instance != old, "씬 재시작");
            }
            manager = GameManager.Instance;
            drawing = false;
            drawnStrokes.Clear();
            foreach (var item in FindObjectsByType<ObstacleSpawner>()) item.enabled = false;
            foreach (var item in FindObjectsByType<FallingInkRockSpawner>()) item.enabled = false;
            foreach (var item in FindObjectsByType<ItemSpawner>()) item.enabled = false;
            foreach (var item in FindObjectsByType<RestPlatformSpawner>()) item.enabled = false;
            yield return Ready(() =>
            {
                if (!manager.IsTransitioning) manager.StartGameFromMenu();
                return manager.IsGameplayTicking;
            }, "게임 시작");
            ScoreManager.Instance.InvalidateCurrentRunForRecords();
            stroke = FindAnyObjectByType<StrokeCapture>();
            if (stroke == null || manager.HighestLivingPlayer == null) throw new InvalidOperationException("플레이 구성 누락");
        }

        IEnumerator Capture(string name, int count, Action<int> events, bool gameplay)
        {
            Time.captureFramerate = Fps;
            string frames = Path.Combine(directory, name);
            Directory.CreateDirectory(frames);
            for (int i = 0; i < count; i++)
            {
                if (gameplay && !manager.IsGameplayTicking) throw new InvalidOperationException(name + " 플레이 중단 " + i);
                events?.Invoke(i);
                if (gameplay) StepStroke();
                HideCaptureOnlyUi();
                yield return frameEnd;
                if (i == 0 || i == count / 2) RecordLayout(name, i);
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    if (texture.width != Width || texture.height != Height) throw new InvalidOperationException("실제 캡처 해상도 불일치");
                    File.WriteAllBytes(Path.Combine(frames, $"frame-{i:0000}.png"), texture.EncodeToPNG());
                }
                finally { Destroy(texture); }
            }
            manager.GetLivingPlayersNonAlloc(players);
            evidence.Add($"{name}: {count} frames / {Fps}fps; living={players.Count}; " +
                $"height={ScoreManager.Instance.Height}; language={GameLocalization.Language}; real PlayerLoop.");
            File.WriteAllLines(Path.Combine(directory, "evidence.txt"), evidence);
            Debug.Log("[StoreCapture] CAPTURED " + name);
        }

        void TryAddNaturalClone(int target, int sequence)
        {
            if (manager.LivingPlayerCount >= target) return;
            manager.GetLivingPlayersNonAlloc(players);
            for (int i = 0; i < players.Count; i++)
                if (manager.TryCreateInkClonesFromItem(players[(i + sequence) % players.Count])) return;
        }

        static Vector2 Viewport(float x, float y) => Camera.main.ViewportToWorldPoint(
            new Vector3(x, y, Mathf.Abs(Camera.main.transform.position.z)));

        void DrawUnderLeader(float slope)
        {
            var leader = manager.HighestLivingPlayer;
            if (leader == null) return;
            Vector2 center = (Vector2)leader.transform.position + Vector2.down * 1.25f;
            Draw(center, 2.6f, slope);
        }

        void DrawAtViewport(float x, float y, float length, float slope) => Draw(Viewport(x, y), length, slope);

        void DrawUnderFallingPlayer(float slope)
        {
            manager.GetLivingPlayersNonAlloc(players);
            foreach (var player in players)
            {
                var body = player.GetComponent<Rigidbody2D>();
                Vector3 view = Camera.main.WorldToViewportPoint(player.transform.position);
                if (body.linearVelocity.y >= -1f || view.y < .18f || view.y > .65f) continue;
                Vector2 center = (Vector2)player.transform.position +
                    new Vector2(Mathf.Clamp(body.linearVelocity.x * .15f, -.45f, .45f), -1.25f);
                Draw(center, 3.1f, slope);
                if (drawing) return;
            }
        }

        void Draw(Vector2 center, float length, float slope)
        {
            if (drawing) return;
            foreach (var previous in drawnStrokes)
                if (Time.time - previous.time < 5f &&
                    Mathf.Abs(center.y - previous.center.y) < 2.1f &&
                    Mathf.Abs(center.x - previous.center.x) < 3.4f) return;
            drawingFrom = center + new Vector2(-length * .5f, -slope);
            drawingTo = center + new Vector2(length * .5f, slope);
            if (!stroke.BeginRecordingStroke(drawingFrom)) return;
            drawnStrokes.Add((center, Time.time));
            drawingStep = 0;
            drawing = true;
        }

        void StepStroke()
        {
            if (!drawing) return;
            // 한 번의 연속적인 손짓처럼 0.27초 동안 실제 입력 경로로 그린다.
            float t = ++drawingStep / 8f;
            stroke.AppendRecordingStroke(Vector2.Lerp(drawingFrom, drawingTo, t) +
                Vector2.up * (Mathf.Sin(t * Mathf.PI) * .035f));
            if (drawingStep < 8) return;
            stroke.EndRecordingStroke();
            drawing = false;
        }

        void RecordLayout(string name, int frame)
        {
            Rect safe = MobileUiLayout.CurrentSafeArea;
            if (Mathf.Abs(safe.width - Width) > 1f || Mathf.Abs(safe.yMax - (Height - 180)) > 1f)
                throw new InvalidOperationException("다른 기기의 안전 영역이 촬영에 남아 있음: " + safe);
            var lines = new List<string>
            {
                $"{name}/{frame}: nativeSafe={Screen.safeArea}; currentSafe={MobileUiLayout.CurrentSafeArea}; " +
                $"banner={LobbyAdLayout.GameplayTopInsetFraction}; hud={GameplayHudView.CalculateVisibleHudRect(MobileUiLayout.CurrentSafeArea, Width, Height)}"
            };
            foreach (var rect in FindObjectsByType<RectTransform>())
            {
                if (rect.name != "TopHudRoot" && rect.name != "Title" && rect.name != "BackButton" &&
                    rect.name != "NodeResetButton" && rect.name != "BalanceHud" && rect.name != "GrowthTabs") continue;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                lines.Add($"{rect.name}: bottomLeft={corners[0]}; topRight={corners[2]}");
            }
            manager.GetLivingPlayersNonAlloc(players);
            foreach (var player in players)
                lines.Add($"player: position={player.transform.position}; velocity={player.GetComponent<Rigidbody2D>().linearVelocity}; " +
                    $"rotation={player.transform.eulerAngles.z}; grounded={player.IsGrounded}");
            File.AppendAllLines(Path.Combine(directory, "layout-evidence.txt"), lines);
        }

        static void SpawnHaetae(int height)
        {
            var spawner = FindAnyObjectByType<ObstacleSpawner>();
            if (spawner == null) throw new InvalidOperationException("해태 스포너 누락");
            typeof(ObstacleSpawner).GetMethod("LoadHaetaeVisuals", PrivateInstance).Invoke(spawner, null);
            if (!(bool)typeof(ObstacleSpawner).GetMethod("SpawnHaetae", PrivateInstance)
                .Invoke(spawner, new object[] { (float)height, false, true })) throw new InvalidOperationException("해태 스폰 실패");
        }

        static object SizeGroup()
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(type);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            return type.GetProperty("currentGroup", PublicInstance).GetValue(sizes);
        }

        static EditorWindow GameView() => EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView", true));

        static void SelectSize(EditorWindow window, int index) => window.GetType()
            .GetMethod("SizeSelectionCallback", PublicInstance).Invoke(window, new object[] { index, null });

        static void ConfigureGameView()
        {
            var window = GameView();
            SessionState.SetInt(Key + "OldSize", (int)window.GetType().GetProperty("selectedSizeIndex", PublicInstance).GetValue(window));
            var group = SizeGroup();
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var assembly = typeof(Editor).Assembly;
            var sizeType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize", true),
                Enum.Parse(sizeType, "FixedResolution"), Width, Height, "MukJump Store Capture TEMP");
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            SessionState.SetInt(Key + "TempSize", count);
            SelectSize(window, count);
            window.Focus();
            window.Repaint();
        }

        static void RestoreGameView()
        {
            try
            {
                SelectSize(GameView(), SessionState.GetInt(Key + "OldSize", 0));
                var group = SizeGroup();
                int index = SessionState.GetInt(Key + "TempSize", -1);
                if (index >= 0)
                {
                    var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                    if ((string)size.GetType().GetProperty("baseText").GetValue(size) == "MukJump Store Capture TEMP")
                        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                }
            }
            catch (Exception ex) { Debug.LogWarning("[StoreCapture] GameView 복구 확인 필요: " + ex.Message); }
        }

        void OnDestroy()
        {
            Time.captureFramerate = previousRate;
            Application.runInBackground = previousBackground;
        }
    }
}
#endif
