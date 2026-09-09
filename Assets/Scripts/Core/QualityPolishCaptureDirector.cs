#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.EditorTools
{
    /// 실제 Game View 프레임을 저장하는 재현 촬영. 배포 빌드에 포함되지 않는다.
    /// 효과를 합성하거나 물리 속도를 바꾸지 않고, 촬영용 아이템/획만 배치한다.
    public sealed class QualityPolishCaptureDirector : MonoBehaviour
    {
        const string ArmedKey = "MukJump.QualityCapture.Armed";
        const string ContactKey = "MukJump.QualityCapture.Contacts";
        const string ResultKey = "MukJump.QualityCapture.Results";
        const string StageKey = "MukJump.QualityCapture.Stage";
        const int Fps = 20;
        static bool storesInstalled;
        static bool captureIdentityInstalled;
        sealed class CaptureIdentityStore : MukJumpIdentityProfile.IStore
        {
            readonly Dictionary<string, string> values = new();
            public string Read(string key) => values.TryGetValue(key, out string value) ? value : string.Empty;
            public void Write(string key, string value) => values[key] = value;
            public void Save() { }
        }
        readonly WaitForEndOfFrame frameEnd = new();
        readonly List<string> evidence = new();
        GameManager manager;
        PlayerController player;
        StrokeCapture stroke;
        string directory;
        int previousCaptureRate;
        bool contactsOnly;
        bool resultsOnly;
        int roadmapStage;
        VfxQualityTier previousTier;
        Action gaugePreview;

        // HUD의 Update 보간 뒤에 표시값을 고정해야 금색이 다음 프레임에 덮이지 않는다.
        void LateUpdate() => gaugePreview?.Invoke();

        [MenuItem("MukJump/검증/수정 효과 GIF 프레임 촬영")]
        static void Arm() => ArmCapture(false);

        [MenuItem("MukJump/검증/점프 착지 파티클 등급별 촬영")]
        static void ArmContacts() => ArmCapture(true);

        [MenuItem("MukJump/검증/결과창 가독성 GIF 프레임 촬영")]
        static void ArmResults() => ArmCapture(false, true);

        [MenuItem("MukJump/검증/50m 상승 등급별 촬영")]
        static void ArmAscent() => ArmCapture(false, false, 2);

        [MenuItem("MukJump/검증/방어막 아이템 등급별 촬영")]
        static void ArmShield() => ArmCapture(false, false, 3);

        [MenuItem("MukJump/검증/먹선 분신 등급별 촬영")]
        static void ArmCreation() => ArmCapture(false, false, 4);

        [MenuItem("MukJump/검증/피격 사망 위험 중첩 촬영")]
        static void ArmSafety() => ArmCapture(false, false, 5);

        [MenuItem("MukJump/검증/일시정지 안내 가독성 촬영")]
        static void ArmReadability() => ArmCapture(false, false, 6);

        [MenuItem("MukJump/검증/먹 게이지 붓 접점 촬영")]
        static void ArmGaugeContact() => ArmCapture(false, false, 7);

        [MenuItem("MukJump/검증/두루마리 펼침 펄럭임 촬영")]
        static void ArmScroll() => ArmCapture(false, true, 8);

        [MenuItem("MukJump/검증/설정 공통 두루마리 촬영")]
        static void ArmSettings() => ArmCapture(false, false, 9);

        [MenuItem("MukJump/검증/해태 양쪽 벽 실제 촬영")]
        static void ArmHaetae() => ArmCapture(false, false, 10);

        [MenuItem("MukJump/검증/첫 실행 스포트라이트 실제 촬영")]
        static void ArmFirstRun() => ArmCapture(false, false, 11);

        [MenuItem("MukJump/검증/로비 순위 아이콘과 닉네임 배치 촬영")]
        static void ArmLobbyRanking() => ArmCapture(false, false, 12);

        [MenuItem("MukJump/검증/로고 탭 낮과 밤 실제 촬영")]
        static void ArmLogoNight() => ArmCapture(false, false, 13);

        [MenuItem("MukJump/검증/결과 먹빛 누적 애니메이션 촬영")]
        static void ArmResultGrowth() => ArmCapture(false, false, 14);

        [MenuItem("MukJump/검증/화면 전환 완전 암전 촬영")]
        static void ArmBlackout() => ArmCapture(false, false, 15);

        static void ArmCapture(bool contacts, bool results = false, int stage = 0)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[QualityCapture] Play 종료 후 촬영을 시작하세요.");
                return;
            }
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetBool(ContactKey, contacts);
            SessionState.SetBool(ResultKey, results);
            SessionState.SetInt(StageKey, stage);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallStores()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            var store = new MemoryLobbySettingsStore();
            store.SetInt("MukJump.Settings.TutorialSeen", 1);
            if (SessionState.GetInt(StageKey, 0) != 11)
                store.SetInt("MukJump.Settings.GameplayTutorialVersion",
                    LobbySettingsProfile.CurrentGameplayTutorialVersion);
            if (SessionState.GetInt(StageKey, 0) is 11 or 12 or 13 or 14 or 15)
            {
                MukJumpIdentityProfile.UseStoreForTests(new CaptureIdentityStore());
                captureIdentityInstalled = true;
            }
            LobbySettingsProfile.UseStoreForTests(store);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            storesInstalled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            // 촬영 고도가 실제 계정으로 동기화되지 않도록 인증/동기화 컴포넌트를 끈다.
            if (MukJumpAccountRuntime.Instance != null)
                MukJumpAccountRuntime.Instance.gameObject.SetActive(false);
            var host = new GameObject("QualityCapture_EDITOR_ONLY");
            DontDestroyOnLoad(host);
            var director = host.AddComponent<QualityPolishCaptureDirector>();
            director.contactsOnly = SessionState.GetBool(ContactKey, false);
            director.resultsOnly = SessionState.GetBool(ResultKey, false);
            director.roadmapStage = SessionState.GetInt(StageKey, 0);
        }

        IEnumerator Start()
        {
            directory = Path.GetFullPath("output/quality-polish/captures/" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(directory);
            previousCaptureRate = Time.captureFramerate;
            previousTier = VfxQualityRuntime.Tier;
            Time.captureFramerate = Fps;
            if (roadmapStage != 9 && roadmapStage != 11 && roadmapStage != 12 && roadmapStage != 13 && roadmapStage != 15)
                yield return PrepareRun(false);
            if (roadmapStage == 15)
            {
                Time.captureFramerate = 0;
                float deadline = Time.realtimeSinceStartup + 30f;
                while (GameManager.Instance == null || StartupBrandSplash.IsBlockingInput)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("암전 촬영 준비 시간 초과");
                    yield return null;
                }
                manager = GameManager.Instance;
                for (int i = 0; i < 20; i++) yield return null;
                var navigator = LobbyScreenNavigator.Instance;
                if (!navigator.OpenGrowth()) throw new InvalidOperationException("성장 진입 거절");
                yield return CaptureResultFrames("01-blackout-growth", 5.4f, realtime: true);
                if (navigator.IsTransitioning || navigator.CurrentSection != LobbyScreenNavigator.LobbySection.PermanentGrowth)
                    throw new InvalidOperationException("성장 드러남 미완료");
                if (!navigator.ReturnToLobby()) throw new InvalidOperationException("성장 복귀 거절");
                yield return CaptureResultFrames("02-blackout-lobby", 5.4f, realtime: true);
                if (navigator.IsTransitioning || !navigator.CanStartGame) throw new InvalidOperationException("로비 드러남 미완료");
                manager.StartGameFromMenu();
                yield return CaptureResultFrames("03-blackout-play", 5.4f, realtime: true);
                if (!manager.IsGameplayTicking) throw new InvalidOperationException("게임 시작 드러남 미완료");
                if (!manager.PauseGame() || !manager.ReturnToLobby()) throw new InvalidOperationException("일시정지 로비 복귀 거절");
                yield return CaptureResultFrames("04-blackout-reload", 5.4f, realtime: true);
                manager = GameManager.Instance;
                if (manager == null || manager.State != GameState.Lobby || manager.IsTransitioning)
                    throw new InvalidOperationException("씬 재로드 드러남 미완료");
                evidence.Add("PASS production growth/lobby/start/scene-reload transitions; real GameView pixels fully black during hold; memory-only stores, no account or ad writes.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 14)
            {
                Time.captureFramerate = 0;
                typeof(GameManager).GetMethod("SetState", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(manager, new object[] { GameState.GameOver });
                manager.enabled = false;
                FindAnyObjectByType<LobbyView>()?.SetNavigationPresentation(false, false);
                var popup = manager.GetComponent<GameOverPopupView>();
                popup.ConfigureActions(null, null);
                // 결과 UI만 실제 PlayerLoop로 촬영하며 계정·광고·성장 저장은 호출하지 않는다.
                var earned = new GameOverResult(245, 620, false, 1, 8, true,
                    cumulativeGrowthDistanceMeters: 625, previousGrowthRewardDistanceMeters: 500,
                    nextGrowthRewardDistanceMeters: 1000, growthDistanceBeforeMeters: 380);
                popup.Show(earned);
                yield return CaptureResultFrames("01-result-inklight-earned", 4.8f, realtime: true);
                GameLocalization.SetLanguage(GameLanguage.English);
                var preview = new GameOverResult(245, 620, false, 0, 7, true,
                    cumulativeGrowthDistanceMeters: 625, previousGrowthRewardDistanceMeters: 500,
                    nextGrowthRewardDistanceMeters: 1000, growthDistanceBeforeMeters: 380,
                    isGrowthPreview: true, previewGrowthCurrency: 1);
                popup.Show(preview, true, true);
                yield return CaptureResultFrames("02-result-inklight-preview", 4.8f, realtime: true);
                evidence.Add("Production GameOverPopupView: 380m + 245m, full 500m threshold, +1 Inklight, 125/500m carry; Korean committed and English revive preview. Read-only result fixtures, no account writes.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 13)
            {
                // 천체는 unscaledDeltaTime을 사용한다. 고정 게임 시계와 실제 촬영 시계를 섞지 않는다.
                Time.captureFramerate = 0;
                float deadline = Time.realtimeSinceStartup + 20f;
                while (GameManager.Instance == null || StartupBrandSplash.IsBlockingInput)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("로고 촬영 진입 시간 초과");
                    yield return null;
                }
                manager = GameManager.Instance;
                var lobby = FindAnyObjectByType<LobbyView>();
                var sky = FindAnyObjectByType<LobbyNightSkyView>();
                for (int i = 0; i < 20; i++) yield return null;
                if (!lobby.IsVisible || !lobby.IsInteractive || lobby.LogoRect == null)
                    throw new InvalidOperationException("실제 로고 입력 영역을 찾을 수 없습니다.");
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    string label = cycle == 0 ? "01-logo-sunset" : "02-logo-sunrise";
                    yield return CaptureResultFrames(label, 12f, frame =>
                    {
                        if (frame < 8 || frame > 80 || (frame - 8) % 8 != 0) return;
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, lobby.LogoRect.position);
                        bool blocked = sky.IsOverUiForTests(point);
                        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                        { position = point, pointerId = 3, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                        UnityEngine.EventSystems.ExecuteEvents.Execute(lobby.LogoRect.gameObject, pointer,
                            UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                        UnityEngine.EventSystems.ExecuteEvents.Execute(lobby.LogoRect.gameObject, pointer,
                            UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                        UnityEngine.EventSystems.ExecuteEvents.Execute(lobby.LogoRect.gameObject, pointer,
                            UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                        int tap = (frame - 8) / 8 + 1;
                        if (blocked || (tap < 10 ? sky.TapCount != tap : !sky.IsTransitioning))
                            throw new InvalidOperationException($"로고 탭 실패: {label} tap={tap} blocked={blocked}");
                    }, realtime: true);
                    if (sky.IsTransitioning || LobbyNightState.TargetNight != (cycle == 0))
                        throw new InvalidOperationException("로고 낮/밤 전환이 끝나지 않았습니다.");
                }
                evidence.Add("Actual lobby logo UI event handlers and raycast; 10 completed taps each direction; outgoing fully hidden before arrival; native player-loop sun/moon motion; no account writes. Event replay, not a physical device touch test.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 12)
            {
                float deadline = Time.realtimeSinceStartup + 20f;
                while (GameManager.Instance == null || StartupBrandSplash.IsBlockingInput)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("로비 촬영 진입 시간 초과");
                    yield return null;
                }
                manager = GameManager.Instance;
                var options = FindAnyObjectByType<LobbyOptionsView>();
                var lobby = FindAnyObjectByType<LobbyView>();
                for (int i = 0; i < 20; i++) yield return null;
                yield return CaptureResultFrames("01-lobby-ranking-icon", 1f);
                options.Open();
                yield return CaptureResultFrames("02-settings-nickname-icon", 2f);
                options.Close();
                for (int i = 0; i < 20; i++) yield return null;
                lobby.LeaderboardButton.onClick.Invoke();
                yield return CaptureResultFrames("03-ranking-from-lobby", 2f);
                if (!options.IsOpen) throw new InvalidOperationException("메인 순위 아이콘 진입 실패");
                evidence.Add("Production lobby icon, settings nickname slot, direct ranking opening; memory-only stores and no live account writes.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 11)
            {
                float deadline = Time.realtimeSinceStartup + 20f;
                while (FirstRunTutorialController.Instance == null || !FirstRunTutorialController.Instance.IsActive)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("첫 실행 자동 안내 시간 초과");
                    yield return null;
                }
                manager = GameManager.Instance;
                player = manager.HighestLivingPlayer;
                var tutorial = FirstRunTutorialController.Instance;
                Vector3 firstPosition = player.transform.position;
                for (int step = 0; step < FirstRunTutorialController.StepCount; step++)
                {
                    yield return CaptureResultFrames($"onboarding-{step + 1}", 1.8f);
                    if (manager.IsGameplayTicking || tutorial.CurrentStep != step ||
                        Vector3.Distance(player.transform.position, firstPosition) > .02f)
                        throw new InvalidOperationException("안내 중 월드 진행 또는 단계 순서 오류");
                    evidence.Add($"step {step + 1}: world frozen, focus={tutorial.FocusScreenRect}, callout={tutorial.CalloutScreenRect}");
                    var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                    UnityEngine.EventSystems.ExecuteEvents.Execute(
                        tutorial.transform.Find("FirstRunTutorialCanvas/TutorialDim").gameObject,
                        pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                }
                yield return CaptureResultFrames("onboarding-5-nickname", 1.6f);
                if (!tutorial.IsAwaitingNickname || manager.IsGameplayTicking || !LobbySettingsProfile.NeedsGameplayTutorial)
                    throw new InvalidOperationException("닉네임 저장 전 첫 판이 시작됨");
                var paper = manager.transform.Find("NicknameCanvas/SafeAreaRoot/NicknameScroll");
                paper.Find("NicknameInput").GetComponent<UnityEngine.UI.InputField>().text = "먹새싹";
                yield return CaptureResultFrames("onboarding-6-name-entered", .6f);
                paper.Find("SaveButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return CaptureResultFrames("onboarding-7-first-game", 2f);
                if (tutorial.IsActive || !manager.IsGameplayTicking || LobbySettingsProfile.NeedsGameplayTutorial)
                    throw new InvalidOperationException("닉네임 저장 뒤 첫 판 재개 실패");
                evidence.Add("PASS auto-start -> four real tap steps -> memory-only nickname -> same run resumes; no real account/store changes.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 10)
            {
                yield return CaptureHaetaeWall(true);
                yield return PrepareRun(true);
                yield return CaptureHaetaeWall(false);
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 9)
            {
                // 설정은 새 로비에서 촬영한다. 플레이→로비 전환은 씬을 재로드한다.
                for (int i = 0; i < 20; i++) yield return null;
                manager = GameManager.Instance;
                var options = FindAnyObjectByType<LobbyOptionsView>();
                if (options == null) throw new InvalidOperationException("설정 화면 없음");
                options.Open();
                yield return CaptureResultFrames("01-settings-scroll", 5f);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(LobbyOptionsView).GetMethod("ToggleBgm", flags).Invoke(options, null);
                yield return CaptureResultFrames("02-settings-muted", 1f);
                typeof(LobbyOptionsView).GetMethod("ShowPlaySettingsPage", flags).Invoke(options, null);
                yield return CaptureResultFrames("03-audio-detail", 1.5f);
                options.OpenTutorialForTests();
                for (int page = 0; page < GameplayTutorialCatalog.Count; page++)
                {
                    yield return CaptureResultFrames($"04-tutorial-{page + 1}", 1f);
                    if (page + 1 < GameplayTutorialCatalog.Count)
                        typeof(LobbyOptionsView).GetMethod("NextTutorialPage", flags).Invoke(options, null);
                }
                typeof(LobbyOptionsView).GetMethod("ShowAccountPage", flags).Invoke(options, null);
                yield return CaptureResultFrames("05-account", 1f);
                options.Close();
                yield return PrepareRun(true);
                if (!manager.PauseGame()) throw new InvalidOperationException("일시정지 촬영 실패");
                yield return CaptureResultFrames("06-pause-scroll", 2f);
                manager.ResumeGame();
                FirstRunTutorialController.Instance.BeginForTests();
                yield return CaptureResultFrames("07-first-tutorial", 2f);
                evidence.Add("Production settings, mute, detail sliders, all replay pages, account, pause, first tutorial. Memory-only stores; no sign-in/ad/legal navigation.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 7)
            {
                var hud = FindAnyObjectByType<PrototypeHud>();
                if (hud == null) throw new InvalidOperationException("먹 게이지 촬영 대상 없음");
                const System.Reflection.BindingFlags fields =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var ratioField = typeof(PrototypeHud).GetField("displayedInkRatio", fields);
                var initializedField = typeof(PrototypeHud).GetField("displayedInkRatioInitialized", fields);
                var goldField = typeof(PrototypeHud).GetField("goldenBlend", fields);
                float previousTimeScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    float[] ratios = { 1f, 0.5f, 0.1f, 0f, 1f };
                    for (int i = 0; i < ratios.Length; i++)
                    {
                        float ratio = ratios[i];
                        float gold = i == ratios.Length - 1 ? 1f : 0f;
                        gaugePreview = () =>
                        {
                            initializedField.SetValue(hud, true);
                            ratioField.SetValue(hud, ratio);
                            goldField.SetValue(hud, gold);
                        };
                        yield return CaptureResultFrames($"{i + 1:00}-gauge-{ratio:0.0}-gold-{gold:0}", 0.5f);
                        evidence.Add($"gauge display: ratio={ratioField.GetValue(hud)}, gold={goldField.GetValue(hud)}");
                    }
                }
                finally
                {
                    gaugePreview = null;
                    Time.timeScale = previousTimeScale;
                }
                evidence.Add("Production OnGUI gauge; controlled display ratios 1/0.5/0.1/0 + gold; actual ink and account saves unchanged.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 6)
            {
                if (!manager.PauseGame())
                    throw new InvalidOperationException("일시정지 가독성 촬영 진입 실패");
                yield return CaptureResultFrames("01-pause", 1.5f);
                manager.ResumeGame();
                for (int i = 0; i < 12; i++) yield return null;
                var tutorial = FirstRunTutorialController.Instance;
                if (tutorial == null) throw new InvalidOperationException("튜토리얼 촬영 대상 없음");
                tutorial.BeginForTests();
                for (int page = 0; page < GameplayTutorialCatalog.Count; page++)
                {
                    if (!tutorial.IsActive) throw new InvalidOperationException("튜토리얼 촬영 진입 실패");
                    yield return CaptureResultFrames($"{page + 2:00}-tutorial-{page + 1}", 0.4f);
                    if (page + 1 < GameplayTutorialCatalog.Count) tutorial.AdvanceForTests();
                }
                evidence.Add("Production pause + five tutorial pages; memory-only saves; no font shrinking.");
                CompleteCapture();
                yield break;
            }
            if (roadmapStage == 5)
            {
                for (int variant = 0; variant < 3; variant++)
                {
                    if (variant > 0) yield return PrepareRun(true);
                    LobbySettingsProfile.SetReducedMotionEnabled(variant == 2);
                    VfxQualityRuntime.SetTier(variant == 1 ? VfxQualityTier.Low : VfxQualityTier.High,
                        VfxQualityChangeReason.DebugOverride);
                    string label = variant == 2 ? "reduced" : variant == 1 ? "low" : "high";
                    yield return CaptureResultFrames($"{variant + 1:00}-safety-{label}", 5f, frame =>
                    {
                        if (frame == 8) SpawnWarningRock();
                        if (frame == 24)
                        {
                            int health = player.CurrentHealth;
                            player.TakeHit();
                            if (player.CurrentHealth != health - 1) throw new InvalidOperationException("피격 촬영 체력 감소 실패");
                            evidence.Add("damage: production TakeHit, exactly one health consumed");
                        }
                        if (frame == 45)
                        {
                            player.Kill();
                            if (!player.IsDead) throw new InvalidOperationException("사망 촬영 Kill 실패");
                            evidence.Add("death: production Kill + character death sequence; memory-only settlement");
                        }
                    });
                }
                CompleteCapture();
                yield break;
            }
            if (roadmapStage > 0 && !resultsOnly)
            {
                for (int variant = 0; variant < 4; variant++)
                {
                    if (variant > 0) yield return PrepareRun(true);
                    LobbySettingsProfile.SetReducedMotionEnabled(variant == 3);
                    VfxQualityRuntime.SetTier(variant == 3 ? VfxQualityTier.High :
                        (VfxQualityTier)(2 - variant), VfxQualityChangeReason.DebugOverride);
                    string label = variant == 3 ? "reduced" : VfxQualityRuntime.Tier.ToString().ToLowerInvariant();
                    yield return Capture($"{variant + 1:00}-stage{roadmapStage}-{label}", 4f, frame =>
                    {
                        if (frame == 10)
                        {
                            if (roadmapStage == 2) SpawnPickup(ItemType.InkDrop);
                            else if (roadmapStage == 3) SpawnPickup(ItemType.InkShield);
                            else DrawStroke();
                        }
                        if (roadmapStage == 3 && frame == 35)
                        {
                            int health = player.CurrentHealth;
                            bool shield = player.HasShield;
                            player.TakeHit();
                            if (!shield || player.HasShield || player.CurrentHealth != health)
                                throw new InvalidOperationException("방어막 파열 상태 검증 실패");
                            evidence.Add("shield break PASS: charge consumed once, health unchanged");
                        }
                        if (roadmapStage == 4 && frame == 35)
                        {
                            if (!manager.TryCreateInkClone(player))
                                throw new InvalidOperationException("분신 촬영 생성 실패");
                            evidence.Add("clone: production spawn/arrival, unchanged physics");
                        }
                        if (roadmapStage == 2 && frame == 16)
                        {
                            if (!player.IsInkDropBoosted) throw new InvalidOperationException("상승 촬영 아이템 흡수 실패");
                            evidence.Add("boost active: production pickup; no physics changes");
                        }
                    });
                }
                CompleteCapture();
                yield break;
            }
            if (resultsOnly)
            {
                yield return CaptureResults();
                CompleteCapture();
                yield break;
            }
            if (contactsOnly)
            {
                yield return VerifyParticleClock();
                VfxQualityRuntime.SetTier(VfxQualityTier.High, VfxQualityChangeReason.DebugOverride);
                yield return Capture("01-contact-high", 7f, frame => { if (frame == 20) DrawStroke(); });
                yield return PrepareRun(true);
                VfxQualityRuntime.SetTier(VfxQualityTier.Low, VfxQualityChangeReason.DebugOverride);
                yield return Capture("02-contact-low", 7f, frame => { if (frame == 20) DrawStroke(); });
                yield return PrepareRun(true);
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                yield return Capture("03-contact-reduced", 7f, frame => { if (frame == 20) DrawStroke(); });
                CompleteCapture();
                yield break;
            }
            yield return Capture("01-jump-landing", 7f, frame =>
            {
                if (frame == 20) DrawStroke();
            });

            yield return PrepareRun(true);
            yield return Capture("02-shield-pickup", 5f, frame =>
            {
                if (frame == 15) SpawnPickup(ItemType.InkShield);
                if (frame == 50)
                {
                    bool before = player.HasShield;
                    player.TakeHit();
                    evidence.Add($"shield break: before={before}, after={player.HasShield}, health={player.CurrentHealth}");
                }
            });

            yield return PrepareRun(true);
            yield return Capture("03-boost-50m", 5f, frame =>
            {
                if (frame == 15) SpawnPickup(ItemType.InkDrop);
            });

            yield return PrepareRun(true);
            yield return Capture("04-golden-gauge", 11f, frame =>
            {
                if (frame == 5) DrawStroke();
                if (frame == 20) SpawnPickup(ItemType.GoldenBrush);
                if (frame == 158) DrawStroke();
                if (frame == 25 || frame == 174 || frame == 190 || frame == 210)
                    evidence.Add($"gold frame={frame}, unlimited={stroke.HasUnlimitedInk}, ink={stroke.InkRemaining01:F3}");
            });

            CompleteCapture();
        }

        IEnumerator CaptureResults()
        {
            // 실제 결과 UI를 읽기 전용 결과 데이터로 촬영한다. 광고·정산은 호출하지 않는다.
            typeof(GameManager).GetMethod("SetState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(manager, new object[] { GameState.GameOver });
            manager.enabled = false;
            // 촬영용 매니저 정지는 Instance도 비운다. 실제 GameOver처럼 로비는 숨겨 둔다.
            FindAnyObjectByType<LobbyView>()?.SetNavigationPresentation(false, false);
            var popup = manager.GetComponent<GameOverPopupView>();
            int reviveClicks = 0, lobbyClicks = 0;
            popup.ConfigureActions(() => reviveClicks++, () => lobbyClicks++);
            var result = new GameOverResult(132, 132, true, 2, 8, true,
                cumulativeGrowthDistanceMeters: 132, previousGrowthRewardDistanceMeters: 120,
                nextGrowthRewardDistanceMeters: 160);
            popup.Hide();
            yield return frameEnd;
            popup.Show(result, true, true);
            yield return CaptureResultFrames("01-result-revive", roadmapStage == 8 ? 8f : 2f);
            popup.SetReviveRequestInFlight(true);
            yield return CaptureResultFrames("02-result-ad-opening", 1f);
            popup.Hide();
            popup.Show(result);
            yield return CaptureResultFrames("03-result-settled", 2f);
            popup.RefreshResult(new GameOverResult(132, 132, false, 0, 0, true,
                growthRewardSaved: false));
            yield return CaptureResultFrames("04-result-recovery", 1f);
            popup.ShowPendingAbandonConfirmation();
            yield return CaptureResultFrames("05-result-confirmation", 1f);
            var content = manager.transform.Find(
                "GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup/ResultContent");
            content.Find("RetryBrush").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            if (lobbyClicks != 1 || reviveClicks != 0)
                throw new InvalidOperationException("결과창 부활 이후 메인 버튼 복구 검사 실패");
            evidence.Add("Result UI fixture: production popup + memory stores; no actual ad or settlement. Lobby callback after in-flight/reshow = 1.");
            if (roadmapStage == 8)
            {
                popup.Hide();
                popup.Show(result, true, false);
                yield return new WaitForSecondsRealtime(0.7f);
                var contentGroup = content.GetComponent<CanvasGroup>();
                contentGroup.alpha = 0f;
                yield return CaptureResultFrames("06-paper-flutter", 6f);
                contentGroup.alpha = 1f;
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                popup.Hide();
                popup.Show(result, true, false);
                yield return CaptureResultFrames("07-reduced-motion", 1f);
                evidence.Add("Scroll: 8s real entry + idle; 6s paper-only inspection; Reduced Motion uses static open paper.");
            }
        }

        IEnumerator CaptureHaetaeWall(bool fromLeft)
        {
            manager.DebugTeleportToHeight(350);
            DrawStroke();
            var spawner = FindAnyObjectByType<ObstacleSpawner>();
            if (spawner == null) throw new InvalidOperationException("해태 스포너 없음");
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ObstacleSpawner).GetMethod("LoadHaetaeVisuals", flags).Invoke(spawner, null);
            var spawn = typeof(ObstacleSpawner).GetMethod("SpawnHaetae", flags);
            string side = fromLeft ? "left" : "right";
            HaetaeObstacle haetae = null;
            bool warned = false;
            bool descended = false;
            string previousState = null;
            yield return Capture("haetae-" + side, 4.2f, frame =>
            {
                if (frame == 10)
                {
                    // 출현 방향만 재현한다. 이동·예고·피격·풀 반납은 실제 FixedUpdate가 처리한다.
                    int seed = 0;
                    for (; seed < 1000; seed++)
                    {
                        GameplayRandom.ResetSession(seed);
                        if ((GameplayRandom.Value(GameplayRandomStream.Obstacles) < 0.5f) == fromLeft)
                            break;
                    }
                    GameplayRandom.ResetSession(seed);
                    if (!(bool)spawn.Invoke(spawner, new object[] { 350f, false, true }))
                        throw new InvalidOperationException("해태 실제 스폰 실패: " + side);
                    haetae = FindAnyObjectByType<HaetaeObstacle>();
                    if (haetae == null || (haetae.LockedStart.x < Camera.main.transform.position.x) != fromLeft)
                        throw new InvalidOperationException("해태 벽 방향 불일치: " + side);
                    evidence.Add($"{side}: production SpawnHaetae, seed={seed}, path={haetae.LockedStart}->{haetae.LockedTarget}");
                }
                if (haetae == null) return;
                warned |= haetae.IsSideWarningVisible && haetae.IsExclamationVisible;
                descended |= haetae.State == HaetaeObstacleState.Pounce && haetae.IsHitboxEnabled;
                string state = haetae.gameObject.activeSelf ? haetae.State.ToString() : "Pooled";
                if (state == previousState) return;
                previousState = state;
                evidence.Add($"{side}: t={frame / (float)Fps:F2}s {state}, hp={player.CurrentHealth}");
            });
            if (!warned || !descended || spawner.HasActiveHaetae)
                throw new InvalidOperationException($"해태 촬영 확인 실패 {side}: warning={warned}, descent={descended}, active={spawner.HasActiveHaetae}");
            evidence.Add($"{side}: PASS warning + exclamation, live descent hitbox, automatic pool release; no manual movement or damage calls.");
        }

        IEnumerator CaptureResultFrames(string name, float seconds, Action<int> events = null, bool realtime = false)
        {
            string frames = Path.Combine(directory, name);
            Directory.CreateDirectory(frames);
            int count = Mathf.RoundToInt(seconds * Fps);
            float nextFrameTime = Time.unscaledTime;
            int blackFrames = 0;
            for (int i = 0; i < count; i++)
            {
                if (realtime)
                    while (Time.unscaledTime < nextFrameTime) yield return null;
                events?.Invoke(i);
                yield return frameEnd;
                Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    if (roadmapStage == 15 && IsBlackoutFullyCovered())
                    {
                        if (blackFrames == 0)
                        {
                            foreach (Color32 pixel in image.GetPixels32())
                                if (pixel.r > 1 || pixel.g > 1 || pixel.b > 1)
                                    throw new InvalidOperationException($"{name} 암전 프레임에 배경/UI 픽셀이 남음: {pixel}");
                        }
                        blackFrames++;
                    }
                    File.WriteAllBytes(Path.Combine(frames, $"frame-{i:0000}.png"), image.EncodeToPNG());
                }
                finally { Destroy(image); }
                nextFrameTime += 1f / Fps;
            }
            evidence.Add($"{name}: {count} real Game View frames, {Fps} fps");
            if (roadmapStage == 15)
            {
                if (blackFrames < 38) throw new InvalidOperationException($"{name} 암전 유지 부족: {blackFrames} frames");
                evidence.Add($"{name}: solid black {blackFrames} frames / {Fps} fps, first opaque frame checked across every pixel.");
            }
        }

        static bool IsBlackoutFullyCovered()
        {
            var view = GameManager.Instance?.GetComponent<BrushTransitionView>();
            var canvas = view != null ? view.transform.Find("BrushTransitionCanvas") : null;
            if (canvas == null) return false;
            var group = canvas.GetComponent<CanvasGroup>();
            var wash = canvas.Find("InkWash").GetComponent<UnityEngine.UI.Image>();
            return group.alpha >= .99999f && wash.canvasRenderer.GetAlpha() >= .99999f;
        }

        void CompleteCapture()
        {
            File.WriteAllLines(Path.Combine(directory, "evidence.txt"), evidence);
            File.WriteAllText(Path.Combine(directory, "complete.txt"),
                $"Unity {Application.unityVersion}; GameView {Screen.width}x{Screen.height}; {Fps} fps; " +
                "real ScreenCapture frames; controlled item/line fixtures; memory-only stores.");
            Debug.Log("[QualityCapture] COMPLETE " + directory);
            EditorApplication.isPlaying = false;
        }

        IEnumerator VerifyParticleClock()
        {
            const string art = "Assets/MukJump/VFX/InkDropJump/Textures/";
            using var probe = new InkContactParticles(transform,
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkDropletAtlas_512.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkSplash_512.png"),
                Resources.Load<Shader>("MukJump/Shaders/InkContactParticle"));
            // 실제 PlayerLoop를 한 프레임 거쳐도 수동 Advance 없이 입자가 움직이면 안 된다.
            probe.EmitJump(new Vector3(1000f, 1000f), Vector2.up, false);
            var system = transform.Find("InkContactParticles/BallisticInk").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[48];
            if (system.GetParticles(particles) == 0) throw new InvalidOperationException("파티클 시계 검증: 방출 실패");
            float before = particles[0].remainingLifetime;
            Vector3 position = particles[0].position;
            yield return null;
            yield return frameEnd;
            system.GetParticles(particles);
            if (!Mathf.Approximately(before, particles[0].remainingLifetime) || position != particles[0].position)
                throw new InvalidOperationException("파티클 시계 검증: PlayerLoop가 자동으로 진행함");
            probe.Advance(0.02f, true);
            system.GetParticles(particles);
            if (Mathf.Abs((before - particles[0].remainingLifetime) - 0.02f) > 0.001f)
                throw new InvalidOperationException("파티클 시계 검증: 수동 시간 중복/누락");
            evidence.Add("PlayMode clock PASS: one PlayerLoop frame unchanged; manual Advance = 0.020s exactly");
        }

        IEnumerator PrepareRun(bool restart)
        {
            if (restart)
            {
                GameManager old = manager;
                manager.Restart();
                float deadline = Time.realtimeSinceStartup + 30f;
                while (GameManager.Instance == null || GameManager.Instance == old)
                {
                    if (Time.realtimeSinceStartup > deadline)
                        throw new InvalidOperationException("촬영 씬 재시작 시간 초과");
                    yield return null;
                }
            }
            for (int i = 0; i < 12; i++) yield return null;
            manager = GameManager.Instance;
            if (manager == null) throw new InvalidOperationException("Main 씬의 GameManager가 없습니다.");
            if (MukJumpAccountRuntime.Instance != null)
                MukJumpAccountRuntime.Instance.gameObject.SetActive(false);
            // 임의 장애물이 촬영을 가리지 않도록 스포너만 비활성화한다. 점프/중력은 그대로.
            foreach (var value in FindObjectsByType<ObstacleSpawner>()) value.enabled = false;
            foreach (var value in FindObjectsByType<FallingInkRockSpawner>()) value.enabled = false;
            foreach (var value in FindObjectsByType<ItemSpawner>()) value.enabled = false;
            foreach (var value in FindObjectsByType<RestPlatformSpawner>()) value.enabled = false;
            // 씬 재시작의 붓 전환이 끝나기 전에 시작 요청을 보내면 정상 로비가 거절한다.
            float startDeadline = Time.realtimeSinceStartup + 30f;
            while (!manager.IsGameplayTicking)
            {
                if (!manager.IsTransitioning) manager.StartGameFromMenu();
                if (Time.realtimeSinceStartup > startDeadline)
                    throw new InvalidOperationException($"촬영 시작 시간 초과: {manager.State}, transition={manager.IsTransitioning}");
                yield return null;
            }
            ScoreManager.Instance?.InvalidateCurrentRunForRecords();
            player = manager.HighestLivingPlayer;
            stroke = FindAnyObjectByType<StrokeCapture>();
            evidence.Add($"run ready: player={player != null}, ink={stroke.InkRemaining01:F3}");
            for (int i = 0; i < 6; i++) yield return null;
        }

        IEnumerator Capture(string name, float seconds, Action<int> events)
        {
            string frames = Path.Combine(directory, name);
            Directory.CreateDirectory(frames);
            int count = Mathf.RoundToInt(seconds * Fps);
            int particlePeak = 0;
            for (int i = 0; i < count; i++)
            {
                if (manager.State != GameState.Playing || player == null || player.IsDead)
                    throw new InvalidOperationException($"{name} frame {i}: 촬영 중 게임 종료");
                events(i);
                yield return frameEnd;
                particlePeak = Mathf.Max(particlePeak, GameFeedbackController.Instance?.ActiveContactParticleCount ?? 0);
                Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    File.WriteAllBytes(Path.Combine(frames, $"frame-{i:0000}.png"), image.EncodeToPNG());
                }
                finally { Destroy(image); }
            }
            evidence.Add($"{name}: {count} frames, {Fps} fps");
            evidence.Add($"contact particle peak={particlePeak}, tier={VfxQualityRuntime.Tier}, reduced={LobbySettingsProfile.ReducedMotionEnabled}");
            File.WriteAllLines(Path.Combine(directory, "evidence.txt"), evidence);
            Debug.Log($"[QualityCapture] {name}: {count} frames");
        }

        void DrawStroke()
        {
            Camera camera = Camera.main;
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector2 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.28f, depth));
            Vector2 from = center + new Vector2(-1.3f, -0.14f);
            Vector2 to = center + new Vector2(1.3f, 0.14f);
            if (!stroke.BeginRecordingStroke(from)) return;
            for (int i = 1; i <= 30; i++) stroke.AppendRecordingStroke(Vector2.Lerp(from, to, i / 30f));
            stroke.EndRecordingStroke();
            evidence.Add($"draw: ink={stroke.InkRemaining01:F3}");
        }

        void SpawnPickup(ItemType type)
        {
            string path = type switch
            {
                ItemType.InkShield => "Assets/Art/UI/ink_shield.png",
                ItemType.GoldenBrush => "Assets/Art/UI/golden_brush.png",
                _ => "Assets/Art/UI/ink_drop.png",
            };
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("촬영 아이템 원화 누락: " + path);
            var item = new GameObject("CapturePickup_" + type) { layer = LayerMask.NameToLayer("Item") };
            item.transform.position = player.transform.position + Vector3.up * 0.1f;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            item.transform.localScale = Vector3.one * (0.9f / sprite.bounds.size.x);
            var trigger = item.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Min(sprite.bounds.extents.x, sprite.bounds.extents.y) * 0.72f;
            var pickup = item.AddComponent<ItemPickup>();
            pickup.Configure(type, 0f);
            pickup.ReleaseRequested += value =>
            {
                evidence.Add($"pickup absorbed: {type}, shield={player.HasShield}, boost={player.IsInkDropBoosted}, golden={stroke.HasUnlimitedInk}");
                Destroy(value.gameObject);
            };
        }

        void SpawnWarningRock()
        {
            // 기존 낙묵석의 실제 예고/낙하. 촬영에서만 충돌을 제외해 피격 시점을 고정한다.
            var rock = new GameObject("CaptureWarningRock");
            Camera camera = Camera.main;
            rock.transform.position = camera.ViewportToWorldPoint(
                new Vector3(0.76f, 0.7f, Mathf.Abs(camera.transform.position.z)));
            var renderer = rock.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Character/Obstacles/anermy_02.png");
            renderer.color = InkPalette.ObstaclePaperRed;
            renderer.sortingOrder = 4;
            rock.transform.localScale = Vector3.one * (0.95f / renderer.sprite.bounds.size.x);
            var effect = rock.AddComponent<FallingInkRock>();
            effect.Initialize(null, camera, 0, 0.9f, 4f, 9f, 8f, 4f);
            evidence.Add("hazard: production FallingInkRock 0.9s telegraph; fixture collision disabled");
        }

        void OnDestroy()
        {
            Time.captureFramerate = previousCaptureRate;
            VfxQualityRuntime.SetTier(previousTier, VfxQualityChangeReason.DebugOverride);
            if (!storesInstalled) return;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests();
            storesInstalled = false;
            if (captureIdentityInstalled)
            {
                MukJumpIdentityProfile.UseStoreForTests(null);
                captureIdentityInstalled = false;
            }
        }
    }
}
#endif
