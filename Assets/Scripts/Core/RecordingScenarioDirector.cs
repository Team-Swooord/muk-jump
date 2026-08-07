#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.EditorTools
{
    /// <summary>
    /// Unity Recorder 제출 영상 전용 50초 연출 감독. 다음 Play 한 번에만 생성되며
    /// 실제 저장 대신 메모리 저장소를 사용해 사용자 기록·성장 데이터는 건드리지 않는다.
    /// 플레이어 빌드에는 타입 자체가 포함되지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class RecordingScenarioDirector : MonoBehaviour
    {
        public enum ScenarioStage
        {
            Intro,
            PermanentGrowth,
            GameplayStart,
            Drawing,
            InkSwarm,
            Items,
            Hazards,
            GameOver,
            TeamCard,
        }

        public const string ArmSessionKey = "MukJump.RecordingScenario.Armed";
        public const float TargetDurationSeconds = 50f;

        static readonly string[] StageLabels =
        {
            "로비 타이틀",
            "영구 성장",
            "도전 시작",
            "먹선과 게이지",
            "먹분신과 카메라",
            "아이템",
            "해태와 풍맥",
            "사망과 결과",
            "팀 엔드카드",
        };

        // 전환 대기 시간을 포함해 약 48초가 되도록 구성한다.
        static readonly float[] StageDurations =
        {
            2.5f,
            5f,
            3f,
            7f,
            4f,
            6f,
            6f,
            7f,
            4f,
        };

        static bool isolatedStoresInstalled;

        readonly List<PlayerController> livingPlayers = new();

        Canvas overlayCanvas;
        CanvasGroup subtitleGroup;
        Text subtitleTitle;
        Text subtitleDetail;
        CanvasGroup teamCardGroup;
        StrokeCapture strokeCapture;
        GameManager manager;
        LobbyScreenNavigator navigator;
        Camera worldCamera;

        bool running;
        bool stageEntered;
        bool secondStrokeStarted;
        bool secondCloneApplied;
        bool inkDropApplied;
        bool windCueApplied;
        bool strokeMotionActive;
        int stageIndex;
        float scenarioStartedAt;
        float stageStartedAt;
        float strokeStartedAt;
        float strokeDuration;
        float strokeArc;
        Vector2 strokeStart;
        Vector2 strokeEnd;

        public static RecordingScenarioDirector Instance { get; private set; }
        public static int StageCount => StageLabels.Length;
        public static float ScheduledDuration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < StageDurations.Length; i++)
                    total += StageDurations[i];
                return total;
            }
        }

        public static string GetStageLabel(int index) =>
            index >= 0 && index < StageLabels.Length
                ? StageLabels[index]
                : string.Empty;

        public static float GetStageDuration(int index) =>
            index >= 0 && index < StageDurations.Length
                ? StageDurations[index]
                : 0f;

        public int CurrentStageIndex => Mathf.Clamp(stageIndex, 0, StageCount - 1);
        public string CurrentStageLabel => GetStageLabel(CurrentStageIndex);
        public bool IsRunning => running;
        public float ElapsedSeconds => running
            ? Mathf.Max(0f, Time.unscaledTime - scenarioStartedAt)
            : 0f;
        public float Progress01 => Mathf.Clamp01(
            ElapsedSeconds / Mathf.Max(0.01f, TargetDurationSeconds));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallIsolatedRecordingStores()
        {
            if (!SessionState.GetBool(ArmSessionKey, false))
                return;

            var lobbyStore = new MemoryLobbySettingsStore();
            lobbyStore.SetFloat("MukJump.Settings.BgmVolume", 1f);
            lobbyStore.SetFloat("MukJump.Settings.SfxVolume", 1f);
            lobbyStore.SetInt("MukJump.Settings.TutorialSeen", 1);
            lobbyStore.SetInt(
                "MukJump.Settings.GameplayTutorialVersion",
                LobbySettingsProfile.CurrentGameplayTutorialVersion);
            lobbyStore.SetString("MukJump.Settings.PlayerUid", "MUK-RECORD");

            LobbySettingsProfile.UseStoreForTests(lobbyStore);
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore { Best = 12 });
            isolatedStoresInstalled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LaunchArmedScenario()
        {
            if (!SessionState.GetBool(ArmSessionKey, false))
                return;

            SessionState.SetBool(ArmSessionKey, false);
            var host = new GameObject("RecordingScenarioDirector_EDITOR_ONLY");
            DontDestroyOnLoad(host);
            host.AddComponent<RecordingScenarioDirector>().Begin();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            FinishActiveStroke();
            if (Instance == this)
                Instance = null;
            if (!isolatedStoresInstalled)
                return;

            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            isolatedStoresInstalled = false;
        }

        public void Begin()
        {
            ResolveRuntimeReferences();
            BuildOverlay();
            running = true;
            stageEntered = false;
            stageIndex = 0;
            scenarioStartedAt = Time.unscaledTime;
            SetTeamCardVisible(false, true);
        }

        public void AdvanceToNextStage()
        {
            if (!running) return;
            FinishActiveStroke();
            stageIndex = Mathf.Min(stageIndex + 1, StageCount - 1);
            stageEntered = false;
        }

        public void StopScenario()
        {
            FinishActiveStroke();
            running = false;
            SetSubtitleVisible(false);
        }

        void Update()
        {
            if (!running) return;
            ResolveRuntimeReferences();
            UpdateStrokeMotion();

            if (!stageEntered)
            {
                if (!TryEnterStage((ScenarioStage)stageIndex))
                    return;
                stageEntered = true;
                stageStartedAt = Time.unscaledTime;
            }

            float stageTime = Mathf.Max(0f, Time.unscaledTime - stageStartedAt);
            UpdateStage((ScenarioStage)stageIndex, stageTime);
            MaintainRecordingSafety((ScenarioStage)stageIndex);

            if (stageTime < StageDurations[stageIndex])
                return;

            FinishActiveStroke();
            if (stageIndex >= StageCount - 1)
            {
                running = false;
                return;
            }
            stageIndex++;
            stageEntered = false;
        }

        bool TryEnterStage(ScenarioStage stage)
        {
            switch (stage)
            {
                case ScenarioStage.Intro:
                    SetSubtitle(
                        "그려서 오르는 수묵 액션",
                        "먹점프 · 최연소밴드");
                    return true;

                case ScenarioStage.PermanentGrowth:
                    if (manager == null || manager.State != GameState.Lobby)
                        return false;
                    navigator ??= LobbyScreenNavigator.Instance;
                    if (navigator == null)
                        return false;
                    if (navigator.CurrentSection !=
                        LobbyScreenNavigator.LobbySection.PermanentGrowth)
                    {
                        if (navigator.IsTransitioning)
                            return false;
                        if (!navigator.OpenGrowth())
                            return false;
                    }
                    SetSubtitle(
                        "한 줄기를 고르는 영구 성장",
                        "생존 · 도약 · 먹 운용, 선택이 다음 도전을 바꿉니다");
                    return true;

                case ScenarioStage.GameplayStart:
                    return TryEnterGameplay();

                case ScenarioStage.Drawing:
                    if (!IsGameplayReady()) return false;
                    SetSubtitle(
                        "먹선이 곧 발판",
                        "새 획을 그리면 가장 오래된 먹부터 천천히 마릅니다");
                    secondStrokeStarted = false;
                    BeginStrokeMotion(false);
                    return true;

                case ScenarioStage.InkSwarm:
                    if (!IsGameplayReady()) return false;
                    SetSubtitle(
                        "함께 오르는 먹떼",
                        "먹분신이 늘어나도 카메라는 가장 높은 생존자를 놓치지 않습니다");
                    secondCloneApplied = false;
                    ApplyItem(ItemType.InkClone);
                    return true;

                case ScenarioStage.Items:
                    if (!IsGameplayReady()) return false;
                    SetSubtitle(
                        "한 번의 아이템이 흐름을 바꾼다",
                        "황금붓과 먹물방울로 먹떼 전체가 다시 솟아오릅니다");
                    inkDropApplied = false;
                    ApplyItem(ItemType.GoldenBrush);
                    return true;

                case ScenarioStage.Hazards:
                    if (!IsGameplayReady()) return false;
                    SetSubtitle(
                        "예고하고 덮치는 수묵 장애물",
                        "먹해태와 풍향이 매 판 다른 움직임을 만듭니다");
                    windCueApplied = false;
                    GrantSwarmShields();
                    (ObstacleSpawner.Instance ??
                     FindFirstObjectByType<ObstacleSpawner>())?
                        .DebugSpawnHaetae();
                    return true;

                case ScenarioStage.GameOver:
                    // 위험 장면 중 먹떼가 예상보다 먼저 전멸해도 해태 자막이
                    // 실제 결과 두루마리 위에 남지 않게 먼저 내린다.
                    SetSubtitleVisible(false);
                    if (manager == null || manager.State != GameState.Playing)
                        return manager != null && manager.State == GameState.GameOver;
                    manager.GetLivingPlayersNonAlloc(livingPlayers);
                    for (int i = livingPlayers.Count - 1; i >= 0; i--)
                        livingPlayers[i]?.Kill();
                    return true;

                case ScenarioStage.TeamCard:
                    SetSubtitleVisible(false);
                    SetTeamCardVisible(true, false);
                    return true;

                default:
                    return false;
            }
        }

        bool TryEnterGameplay()
        {
            if (manager == null)
                return false;
            navigator ??= LobbyScreenNavigator.Instance;
            if (navigator != null &&
                navigator.CurrentSection != LobbyScreenNavigator.LobbySection.Lobby)
            {
                if (!navigator.IsTransitioning)
                    navigator.ReturnToLobby();
                return false;
            }
            if (navigator != null && navigator.IsTransitioning)
                return false;

            if (manager.State == GameState.Lobby)
            {
                manager.StartGameFromMenu();
                return false;
            }
            if (!IsGameplayReady())
                return false;

            SetSubtitle(
                "붓끝 하나로 도전 시작",
                "캐릭터는 자동으로 뛰고, 플레이어는 길만 그립니다");
            ProtectLivingPlayers(8f);
            return true;
        }

        void UpdateStage(ScenarioStage stage, float stageTime)
        {
            switch (stage)
            {
                case ScenarioStage.Drawing:
                    if (!secondStrokeStarted && stageTime >= 3.25f)
                    {
                        secondStrokeStarted = true;
                        BeginStrokeMotion(true);
                    }
                    break;

                case ScenarioStage.InkSwarm:
                    if (!secondCloneApplied && stageTime >= 1.8f)
                    {
                        secondCloneApplied = true;
                        ApplyItem(ItemType.InkClone);
                    }
                    break;

                case ScenarioStage.Items:
                    if (!inkDropApplied && stageTime >= 2.1f)
                    {
                        inkDropApplied = true;
                        ApplyItem(ItemType.InkDrop);
                    }
                    break;

                case ScenarioStage.Hazards:
                    if (!windCueApplied && stageTime >= 2.8f)
                    {
                        windCueApplied = true;
                        RestPlatformSpawner.Instance?.DebugSpawnWindNearPlayer();
                        WindWeatherController.Instance?.DebugFlipDirection();
                    }
                    break;

                case ScenarioStage.TeamCard:
                    if (teamCardGroup != null)
                        teamCardGroup.alpha = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.Clamp01(stageTime / 0.7f));
                    break;
            }
        }

        bool IsGameplayReady() =>
            manager != null && manager.IsGameplayTicking;

        void ApplyItem(ItemType type)
        {
            PlayerController target = manager != null
                ? manager.HighestLivingPlayer
                : null;
            if (target != null)
                ItemEffect.Apply(type, target);
        }

        void GrantSwarmShields()
        {
            if (manager == null) return;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            for (int i = 0; i < livingPlayers.Count; i++)
                livingPlayers[i]?.TryGrantShield();
        }

        void ProtectLivingPlayers(float seconds)
        {
            if (manager == null) return;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            for (int i = 0; i < livingPlayers.Count; i++)
                livingPlayers[i]?.GrantObstacleProtection(seconds);
        }

        void MaintainRecordingSafety(ScenarioStage stage)
        {
            if (manager == null || manager.State != GameState.Playing ||
                stage >= ScenarioStage.Hazards)
                return;

            ProtectLivingPlayers(1.2f);
            worldCamera ??= Camera.main;
            if (worldCamera == null) return;
            float depth = Mathf.Abs(worldCamera.transform.position.z);
            float safeY = worldCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.18f, depth)).y;
            manager.GetLivingPlayersNonAlloc(livingPlayers);
            for (int i = 0; i < livingPlayers.Count; i++)
            {
                PlayerController player = livingPlayers[i];
                if (player == null || player.transform.position.y >= safeY)
                    continue;
                player.DebugTeleportBy(Vector2.up *
                    (safeY - player.transform.position.y + 0.35f));
                player.LaunchToHeight(3.5f);
            }
        }

        void BeginStrokeMotion(bool reverseSlope)
        {
            FinishActiveStroke();
            strokeCapture ??= FindFirstObjectByType<StrokeCapture>();
            worldCamera ??= Camera.main;
            if (strokeCapture == null || worldCamera == null)
                return;

            float depth = Mathf.Abs(worldCamera.transform.position.z);
            // 기본 4.8m 먹 용량으로 화면 폭 약 0.42개가 한 획에 가깝게 보인다.
            // 첫 획 자체가 즉시 전부 밀려나지 않게 실제 밸런스와 같은 길이를 쓴다.
            float startX = reverseSlope ? 0.73f : 0.28f;
            float endX = reverseSlope ? 0.31f : 0.70f;
            float startY = reverseSlope ? 0.39f : 0.32f;
            float endY = reverseSlope ? 0.33f : 0.40f;
            strokeStart = worldCamera.ViewportToWorldPoint(
                new Vector3(startX, startY, depth));
            strokeEnd = worldCamera.ViewportToWorldPoint(
                new Vector3(endX, endY, depth));
            strokeArc = reverseSlope ? -0.22f : 0.28f;
            strokeDuration = 2.25f;
            strokeStartedAt = Time.unscaledTime;
            strokeMotionActive = strokeCapture.BeginRecordingStroke(strokeStart);
        }

        void UpdateStrokeMotion()
        {
            if (!strokeMotionActive || strokeCapture == null)
                return;
            float t = Mathf.Clamp01(
                (Time.unscaledTime - strokeStartedAt) /
                Mathf.Max(0.01f, strokeDuration));
            Vector2 point = Vector2.Lerp(strokeStart, strokeEnd, t);
            point.y += Mathf.Sin(t * Mathf.PI) * strokeArc;
            strokeCapture.AppendRecordingStroke(point);
            if (t >= 1f)
                FinishActiveStroke();
        }

        void FinishActiveStroke()
        {
            if (!strokeMotionActive) return;
            strokeCapture?.EndRecordingStroke();
            strokeMotionActive = false;
        }

        void ResolveRuntimeReferences()
        {
            manager ??= GameManager.Instance ?? FindFirstObjectByType<GameManager>();
            navigator ??= LobbyScreenNavigator.Instance ??
                          FindFirstObjectByType<LobbyScreenNavigator>();
            strokeCapture ??= FindFirstObjectByType<StrokeCapture>();
            worldCamera ??= Camera.main;
        }

        void BuildOverlay()
        {
            if (overlayCanvas != null) return;

            var root = new GameObject("RecordingScenarioOverlay");
            root.transform.SetParent(transform, false);
            overlayCanvas = root.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 9000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>().enabled = false;

            RectTransform subtitle = CreateRect(
                "Subtitle",
                root.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 230f),
                new Vector2(860f, 138f));
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            Image subtitleBrush = subtitle.gameObject.AddComponent<Image>();
            subtitleBrush.sprite = InkUiTextureFactory.CreateBrushSprite();
            subtitleBrush.type = Image.Type.Sliced;
            subtitleBrush.color = new Color(
                InkPalette.Ink.r,
                InkPalette.Ink.g,
                InkPalette.Ink.b,
                0.88f);
            subtitleBrush.raycastTarget = false;

            subtitleTitle = CreateText(
                "Title",
                subtitle,
                39,
                InkPalette.Paper,
                new Vector2(24f, 59f),
                new Vector2(-24f, -10f));
            subtitleDetail = CreateText(
                "Detail",
                subtitle,
                25,
                new Color(
                    InkPalette.Paper.r,
                    InkPalette.Paper.g,
                    InkPalette.Paper.b,
                    0.88f),
                new Vector2(30f, 12f),
                new Vector2(-30f, -58f));

            RectTransform card = CreateRect(
                "TeamCard",
                root.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            teamCardGroup = card.gameObject.AddComponent<CanvasGroup>();
            Image dim = card.gameObject.AddComponent<Image>();
            dim.color = new Color(
                InkPalette.Ink.r,
                InkPalette.Ink.g,
                InkPalette.Ink.b,
                0.94f);
            dim.raycastTarget = false;

            Text title = CreateCenteredText(
                "GameTitle", card, "먹점프", 124, InkPalette.Paper,
                new Vector2(0f, 190f), new Vector2(920f, 170f));
            title.fontStyle = FontStyle.Normal;
            CreateCenteredText(
                "Tagline", card, "그려서 오르는 수묵 액션", 44,
                InkPalette.Paper2,
                new Vector2(0f, 62f), new Vector2(920f, 88f));
            Image divider = CreateRect(
                    "Divider",
                    card,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -22f),
                    new Vector2(520f, 8f))
                .gameObject.AddComponent<Image>();
            divider.color = InkPalette.Red;
            divider.raycastTarget = false;
            CreateCenteredText(
                "Team", card, "최연소밴드", 42, InkPalette.Paper,
                new Vector2(0f, -108f), new Vector2(920f, 74f));
            CreateCenteredText(
                "Members", card, "김승연 · 최성빈", 31,
                new Color(
                    InkPalette.Paper.r,
                    InkPalette.Paper.g,
                    InkPalette.Paper.b,
                    0.82f),
                new Vector2(0f, -180f), new Vector2(920f, 64f));
            CreateCenteredText(
                "Footer", card, "AI와 함께 완성한 2인 팀 프로젝트", 27,
                InkPalette.Gold,
                new Vector2(0f, -310f), new Vector2(920f, 60f));
        }

        void SetSubtitle(string title, string detail)
        {
            BuildOverlay();
            subtitleTitle.text = title;
            subtitleDetail.text = detail;
            SetSubtitleVisible(true);
        }

        void SetSubtitleVisible(bool visible)
        {
            if (subtitleGroup == null) return;
            subtitleGroup.alpha = visible ? 1f : 0f;
            subtitleGroup.interactable = false;
            subtitleGroup.blocksRaycasts = false;
        }

        void SetTeamCardVisible(bool visible, bool immediate)
        {
            if (teamCardGroup == null) return;
            teamCardGroup.alpha = visible && immediate ? 1f : 0f;
            teamCardGroup.interactable = false;
            teamCardGroup.blocksRaycasts = false;
        }

        static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        static Text CreateText(
            string name,
            RectTransform parent,
            int fontSize,
            Color color,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static Text CreateCenteredText(
            string name,
            RectTransform parent,
            string value,
            int fontSize,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }
    }
}
#endif
