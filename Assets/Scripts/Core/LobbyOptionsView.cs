using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비에서 꼭 필요한 소리 설정, 고객센터와 튜토리얼만 제공한다.
    [DisallowMultipleComponent]
    public sealed partial class LobbyOptionsView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4150;
        const float PanelWidth = 820f;
        const float PanelHeight = 1510f;
        const float SettingsDesignHeight = 1450f;
        const float SettingsPanelHeight = 1220f;
        const float SettingsStatusHeight = 110f;
        const float CloseFooterHeight = 190f;
        const float SafeAreaPadding = 24f;
        const float DeleteConfirmationMinimumDelay = 0.45f;
        const string CustomerSupportEmail = "cysbandcs@gmail.com";

        CanvasGroup rootGroup;
        CanvasGroup optionsGroup;
        CanvasGroup tutorialGroup;
        CanvasGroup playSettingsGroup;
        CanvasGroup debugScenarioGroup;
        CanvasGroup accountGroup;
        CanvasGroup leaderboardGroup;
        CanvasGroup languageGroup;
        CanvasGroup analyticsPrivacyGroup;
        Text analyticsPrivacyStatus;
        RectTransform safeAreaRoot;
        RectTransform optionsPanel;
        Slider bgmSlider;
        Slider sfxSlider;
        Button tossIdentityRetry;
        Text settingsAccountLabel;
        Button settingsUuidButton;
        Text settingsUuidText;
        string settingsUuid = string.Empty;
        string settingsUuidCaption;
        string uuidCopyStatus = string.Empty;
        float uuidCopyStatusUntil;
        bool uuidCopyPending = false;
#if UNITY_EDITOR
        RuntimePlatform? platformOverrideForTests;
#endif
        Button quickBgm, quickSfx, quickHaptics;
        Text quickBgmStatus, quickSfxStatus, quickHapticsStatus;
        Text bgmValue;
        Text sfxValue;
        Text bgmToggleLabel;
        Text sfxToggleLabel;
        Text connectionStatus;
        float settingsLayoutHeight = SettingsDesignHeight;
        Text hapticsStatus;
        Text debugScenarioStatus;
        Text debugScenarioSummary;
        Text accountKindText;
        Text accountStatusText;
        CanvasGroup accountToast;
        Text accountToastText;
        string accountToastSource;
        string lastAccountToastStatus;
        float accountToastElapsed;
        float accountToastFadeFrom;
        const float AccountToastFadeIn = .16f;
        const float AccountToastHold = 4f;
        const float AccountToastFadeOut = .24f;
        Text accountSyncPendingTitleText;
        Text accountSyncPendingStatusText;
        Text accountSyncPendingCaptionText;
        Text accountDeleteLabel;
        Text accountPlayerIdText;
        Button accountAppleButton;
        Button accountLogoutButton;
        Button accountDeleteButton;
        Button accountSyncRetryButton;
        Button accountSyncReturnButton;
        RectTransform accountConflictRoot;
        RectTransform syncConflictRoot;
        RectTransform accountSyncPendingRoot;
        readonly Text[] leaderboardRows = new Text[10];
        readonly Text[] leaderboardRanks = new Text[10];
        readonly Text[] leaderboardNames = new Text[10];
        readonly Image[] leaderboardFlags = new Image[10];
        readonly Text[] leaderboardSources = new Text[10];
        readonly Image[] leaderboardSeals = new Image[10];
        Text globalLeaderboardLabel, appleLeaderboardLabel;
        bool leaderboardFromLobby;
        bool showingAppleLeaderboard;
        Button leaderboardRefresh;
        Image tutorialImage;
        Text tutorialTitle;
        Text tutorialDescription;
        Text tutorialPage;
        Button tutorialPreviousButton;
        Button tutorialNextButton;
        readonly Button[] debugScenarioButtons = new Button[5];
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        int currentTutorialPage;
        bool suppressSliderCallbacks;
        bool showingSettingsPage;
        bool showingTutorialPage;
        bool scrollPageTransition;
        bool showingAccountPage;
        float accountPaperHeight = 930f;
        Button accountCloseButton;
        bool deleteConfirmationArmed;
        float deleteConfirmationArmedAt = float.NegativeInfinity;
        bool applicationFocused = true;
        bool applicationPaused;
#if UNITY_WEBGL && !UNITY_EDITOR
        bool supportMailOpening;
#endif
        MukJumpAccountRuntime boundAccountRuntime;

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public bool IsTutorialOpen =>
            IsOpen && tutorialGroup != null && tutorialGroup.blocksRaycasts;
        public bool IsDebugScenarioOpen =>
            IsOpen && debugScenarioGroup != null &&
            debugScenarioGroup.blocksRaycasts;
        public bool IsAccountOpen =>
            IsOpen && accountGroup != null && accountGroup.blocksRaycasts;
        public int TutorialPageCount => GameplayTutorialCatalog.Count;
        public int CurrentTutorialPage => currentTutorialPage;

        // 이 프로젝트의 WebGL 배포는 앱인토스 전용이다. OS 로그인은 네이티브에만 둔다.
        public static bool UsesTossSettingsFor(RuntimePlatform platform) =>
            platform == RuntimePlatform.WebGLPlayer;

        RuntimePlatform UiPlatform =>
#if UNITY_EDITOR
            platformOverrideForTests ??
#endif
            Application.platform;
        bool UsesTossSettings => UsesTossSettingsFor(UiPlatform);
        bool ShowingLanguagePage => languageGroup != null && languageGroup.blocksRaycasts;
        bool ShowingAnalyticsPrivacy => analyticsPrivacyGroup != null && analyticsPrivacyGroup.blocksRaycasts;
        bool ShowingLeaderboardPage => leaderboardGroup != null && leaderboardGroup.blocksRaycasts;

#if UNITY_EDITOR
        Vector2Int? displaySizeForTests;
        Rect displaySafeAreaForTests;
#endif
        int UiScreenWidth =>
#if UNITY_EDITOR
            displaySizeForTests?.x ??
#endif
            Screen.width;
        int UiScreenHeight =>
#if UNITY_EDITOR
            displaySizeForTests?.y ??
#endif
            Screen.height;
        Rect UiSafeArea =>
#if UNITY_EDITOR
            displaySizeForTests.HasValue ? displaySafeAreaForTests :
#endif
            MobileUiLayout.CurrentSafeArea;

        void Awake()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        void OnEnable()
        {
            BuildIfNeeded();
            CancelScrollPageTransition();
            BindManager();
            LobbySettingsProfile.Changed += RefreshSettings;
            GameLocalization.Changed += RefreshLanguage;
            AppleGameCenterRuntime.Changed += RefreshLeaderboardPage;
            DebugShowcaseScenarioProfile.Changed += RefreshDebugScenario;
            BindAccountRuntime();
        }

        void OnDisable()
        {
            CloseNicknameImmediate();
            LobbySettingsProfile.Changed -= RefreshSettings;
            GameLocalization.Changed -= RefreshLanguage;
            AppleGameCenterRuntime.Changed -= RefreshLeaderboardPage;
            DebugShowcaseScenarioProfile.Changed -= RefreshDebugScenario;
            UnbindAccountRuntime();
            AppleSignInButtonBridge.Hide();
            UnbindManager();
            CloseImmediate();
            LobbySettingsProfile.TryFlush();
        }

        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
            {
                DisarmDeleteConfirmation();
                AppleSignInButtonBridge.Hide();
                LobbySettingsProfile.Flush();
            }
        }

        void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
            {
                DisarmDeleteConfirmation();
                AppleSignInButtonBridge.Hide();
                LobbySettingsProfile.Flush();
            }
        }

        void OnApplicationQuit()
        {
            LobbySettingsProfile.Flush();
        }

        void Update()
        {
            if (manager == null)
                BindManager();
            if (boundAccountRuntime == null)
                BindAccountRuntime();
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();
            if (UiScreenWidth != lastScreenWidth ||
                UiScreenHeight != lastScreenHeight ||
                UiSafeArea != lastSafeArea)
                ApplySafeArea();
            SyncNativeAppleSignInButton();
            AdvanceAccountToast(Time.unscaledDeltaTime,
                IsAccountOpen && rootGroup.interactable && !scrollPageTransition &&
                applicationFocused && !applicationPaused &&
                (!Application.isPlaying || optionsPanel.GetComponent<HanjiScrollFrame>().IsReady));
            if (IsOpen && optionsGroup != null && optionsGroup.blocksRaycasts)
                RefreshSettingsUuid();
            UpdateNicknameUi();
            UpdateDeleteConfirmation();
        }

        public void Open() => OpenLobbyPage(false);

        void OpenLobbyPage(bool openLeaderboard)
        {
            CancelScrollPageTransition();
            leaderboardFromLobby = false;
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }
            RefreshSettings();
            bool requiredSync = MukJumpAccountRuntime.Instance != null &&
                                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync;
            if (openLeaderboard && UsesTossSettings && !requiredSync)
            {
                // 토스도 설정을 뒤에 남기지 않고 메인에서 공식 순위로 바로 이동한다.
                AppsInTossGameCenterRuntime.OpenLeaderboard();
                return;
            }
            if (requiredSync)
                ShowAccountPageImmediate();
            else if (openLeaderboard && !UsesTossSettings)
            {
                // 메인에서 바로 여는 순위는 설정을 먼저 펼치지 않고 목적 내용으로 한 번만 연다.
                leaderboardFromLobby = true;
                ShowLeaderboardPageImmediate();
            }
            else
                ShowOptionsPageImmediate();
            SetVisible(true);
        }

        public void OpenLeaderboard()
        {
            if (IsOpen) return;
            OpenLobbyPage(true);
        }

        void LeaveLeaderboard()
        {
            if (leaderboardFromLobby) Close();
            else ShowOptionsPage();
        }

        public void Close()
        {
            if (IsDeleteConfirmationOpen) { CancelDeleteConfirmation(); return; }
            if (MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                return;
            if (!IsOpen) return;
            CancelScrollPageTransition();
            DisarmDeleteConfirmation();
            ClearAccountToast();
            AppleSignInButtonBridge.Hide();
            rootGroup.interactable = false;
            optionsPanel.GetComponent<HanjiScrollFrame>().Close(() =>
            {
                SetVisible(false);
                LobbySettingsProfile.TryFlush();
            }, rootGroup);
        }

        void HandleBackdropClick(BaseEventData data)
        {
            if (!IsOpen || !rootGroup.interactable || optionsPanel == null ||
                data is not PointerEventData pointer ||
                pointer.button != PointerEventData.InputButton.Left || pointer.dragging)
                return;
            if (Application.isPlaying && !optionsPanel.GetComponent<HanjiScrollFrame>().IsReady)
                return;
            // 종이 장식은 raycast를 받지 않는다. 안쪽 빈 곳/펼침 도중의 터치도
            // dim까지 도달할 수 있으므로 실제 패널 영역은 여기서 제외한다.
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    optionsPanel, pointer.position, pointer.pressEventCamera))
                return;
            PointerInput.SuppressUntilRelease();
            if (ShowingLeaderboardPage) LeaveLeaderboard();
            else if (showingTutorialPage || showingAccountPage || ShowingLanguagePage || ShowingAnalyticsPrivacy) ShowOptionsPage();
            else Close();
        }

        public void OpenAccountForRequiredSync()
        {
            CancelScrollPageTransition();
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
                return;
            ShowAccountPageImmediate();
            SetVisible(true);
            RefreshAccountState();
        }

        public void BuildForTests()
        {
            BuildIfNeeded();
            RefreshSettings();
            CloseImmediate();
        }

        public void OpenTutorialForTests()
        {
            BuildIfNeeded();
            CancelScrollPageTransition();
            ShowTutorialPageImmediate(0);
            SetVisible(true);
        }

#if UNITY_EDITOR
        // 렌더 크기와 배치 기준을 함께 주입한다. 실제 Screen/전역 safe area는 변경하지 않는다.
        public void SetDisplayMetricsForTests(int width, int height, Rect safeArea)
        {
            if (width <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(height));
            displaySizeForTests = new Vector2Int(width, height);
            displaySafeAreaForTests = MobileUiLayout.SanitizeSafeArea(safeArea, width, height);
            ApplySafeArea();
        }

        public void BuildForPlatformForTests(RuntimePlatform platform)
        {
            if (rootGroup != null)
                throw new System.InvalidOperationException("UI 생성 전에 테스트 플랫폼을 지정해야 합니다.");
            platformOverrideForTests = platform;
            BuildForTests();
        }
#endif

        void BindManager()
        {
            GameManager next = GameManager.Instance;
            if (ReferenceEquals(manager, next)) return;
            UnbindManager();
            manager = next;
            if (manager != null)
                manager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Lobby)
                Close();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var stale = transform.Find("LobbyOptionsCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "LobbyOptionsCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);
            rootGroup = root.GetComponent<CanvasGroup>();

            var dim = CreateStretchImage(
                "InkDim",
                root.transform,
                InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);
            var backdropEvents = dim.gameObject.AddComponent<EventTrigger>();
            var dismiss = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            dismiss.callback.AddListener(HandleBackdropClick);
            backdropEvents.triggers.Add(dismiss);
            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            optionsPanel = CreateRect(
                "OptionsScroll",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            BuildScrollFrame(optionsPanel);

            optionsGroup = CreatePageGroup("OptionsPage", optionsPanel);
            BuildOptionsPage(optionsGroup.transform);
            tutorialGroup = CreatePageGroup("TutorialPage", optionsPanel);
            BuildTutorialPage(tutorialGroup.transform);
            playSettingsGroup = CreatePageGroup(
                "PlaySettingsPage",
                optionsPanel);
            BuildPlaySettingsPage(playSettingsGroup.transform);
            accountGroup = CreatePageGroup("AccountPage", optionsPanel);
            if (UsesTossSettings)
                BuildTossIdentityPage(accountGroup.transform);
            else
            {
                BuildAccountPage(accountGroup.transform);
            }
            leaderboardGroup = CreatePageGroup("LeaderboardPage", optionsPanel);
            BuildLeaderboardPage(leaderboardGroup.transform);
            languageGroup = CreatePageGroup("LanguagePage", optionsPanel);
            BuildLanguagePage(languageGroup.transform);
            SetPageVisible(languageGroup, false);
            if (!UsesTossSettings)
            {
                analyticsPrivacyGroup = CreatePageGroup("AnalyticsPrivacyPage", optionsPanel);
                BuildAnalyticsPrivacyPage(analyticsPrivacyGroup.transform);
                SetPageVisible(analyticsPrivacyGroup, false);
            }
            if (!UsesTossSettings) BuildAccountToast();
            // 디버그 시나리오는 내부 촬영 API에만 남기고 설정에는 생성하지 않는다.

            ApplySafeArea();
            RefreshSettings();
        }

        void BuildOptionsPage(Transform panel)
        {
            settingsLayoutHeight = SettingsDesignHeight;
            // 실제 글자 획 기준으로 위 롤과 아래 버전 사이의 여백을 맞춘다.
            var title = CreateReadableText("Title", panel, "설정", 72,
                new Vector2(0, 608), new Vector2(520, 88),
                InkPalette.Ink, TextAnchor.MiddleCenter, strong: true, outline: true);
            var titleOutline = title.GetComponent<Outline>();
            titleOutline.effectColor = WithAlpha(InkPalette.Ink, 0.85f);
            titleOutline.effectDistance = new Vector2(1.25f, -1.25f);
            CreateReadableText("Version", panel, $"v{Application.version}", 32,
                new Vector2(0, 540), new Vector2(300, 42),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            var uuidHit = CreateImage("UuidButton", panel, null,
                new Vector2(UsesTossSettings ? 0 : -175, 490), new Vector2(UsesTossSettings ? 700 : 350, InkUiStyle.MinimumTapHeight), Color.clear);
            uuidHit.raycastTarget = true;
            settingsUuidButton = uuidHit.gameObject.AddComponent<Button>();
            // 보조 정보는 일반 CTA의 큰 흰색 라벨 스타일을 적용하지 않는다.
            InkUiStyle.ConfigureButton(settingsUuidButton, uuidHit);
            settingsUuidText = CreateReadableText("Label", uuidHit.transform, "UUID —", 30,
                Vector2.zero, new Vector2(UsesTossSettings ? 700 : 340, 36), InkPalette.TextMuted, wrap: false);
            settingsUuidText.supportRichText = false;
            settingsUuidButton.transition = Selectable.Transition.None;
            settingsUuidButton.onClick.AddListener(CopySettingsUuid);
            if (!UsesTossSettings)
            {
                settingsNicknameText = CreateReadableText("Nickname", panel, string.Empty, 34,
                    new Vector2(180, 490), new Vector2(340, 46), InkPalette.TextDark, strong: true, wrap: false);
                settingsNicknameText.supportRichText = false;
                settingsNicknameText.resizeTextForBestFit = true;
                settingsNicknameText.resizeTextMinSize = 24;
                settingsNicknameText.resizeTextMaxSize = 34;
                InkLocalizedText.Exclude(settingsNicknameText);
            }
            CreateDivider(panel, "HeaderDivider", 458, 700);

            quickBgm = CreateQuickToggle("BgmToggle", panel, "배경음", -240, out quickBgmStatus, 0);
            quickBgm.onClick.AddListener(ToggleBgm);
            quickSfx = CreateQuickToggle("SfxToggle", panel, "효과음", 0, out quickSfxStatus, 1);
            quickSfx.onClick.AddListener(ToggleSfx);
            quickHaptics = CreateQuickToggle("HapticsToggle", panel, "진동", 240, out quickHapticsStatus, 2);
            quickHaptics.onClick.AddListener(() =>
            {
                LobbySettingsProfile.SetHapticsEnabled(!LobbySettingsProfile.HapticsEnabled);
                MukJumpAnalytics.Setting(AnalyticsSetting.Vibration, LobbySettingsProfile.HapticsEnabled ? 1 : 0);
                RefreshSettings();
            });

            CreateIconMenu("LanguageButton", panel, "한국어", "language", new Vector2(-178, 56))
                .onClick.AddListener(ShowLanguagePage);
            CreateIconMenu("CustomerCenterButton", panel, "고객센터", "support", new Vector2(178, 56))
                .onClick.AddListener(ShowCustomerCenterGuide);
            CreateIconMenu("GuideButton", panel, "튜토리얼", "tutorial", new Vector2(-178, -88))
                .onClick.AddListener(() => ShowTutorialPage(0));
            if (!UsesTossSettings)
            {
                var nickname = CreateIconMenu("NicknameButton", panel, "닉네임", "nickname",
                    new Vector2(178, -88));
                nickname.onClick.AddListener(() => OpenNicknamePopup(false));
                settingsNicknameButton = nickname;
                nickname.gameObject.SetActive(HasLinkedNicknameAccount);
                var account = CreateIconMenu("AccountButton", panel, "계정 연동", "account",
                    new Vector2(0, -252), new Vector2(540, 128));
                account.onClick.AddListener(ShowAccountPage);
                settingsAccountLabel = account.transform.Find("Paper/Label").GetComponent<Text>();
            }
            connectionStatus = CreateReadableText("ConnectionStatus", panel, string.Empty, 40,
                new Vector2(0, -527), new Vector2(700, 70),
                InkPalette.TextDark, TextAnchor.MiddleCenter);
            CreateLegalFooterLink("TermsButton", panel, "이용약관", false)
                .onClick.AddListener(OpenTermsOfService);
            CreateLegalFooterLink("PrivacyButton", panel, "개인정보처리방침", true)
                .onClick.AddListener(() => { if (UsesTossSettings) OpenPrivacyPolicy(); else ShowAnalyticsPrivacyPage(); });
            // 종이 밖에 놓되 OptionsPage의 표시·입력·역순 닫힘을 그대로 상속한다.
            var close = CreatePaperButton("CloseButton", panel, string.Empty,
                new Vector2(0, -820), new Vector2(120, 120), 36);
            var closePaper = close.transform.Find("Paper").GetComponent<Image>();
            closePaper.rectTransform.sizeDelta = new Vector2(96, 96);
            for (int i = 0; i < 2; i++)
            {
                var stroke = CreateImage("CrossStroke" + i, closePaper.transform,
                    InkUiTextureFactory.CreateBrushSprite(), Vector2.zero,
                    new Vector2(48, 7), InkPalette.TextDark);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
            }
            close.onClick.AddListener(Close);
        }

        // 토스의 검증된 게임 ID와 네이티브 뒤끝 UID를 섞거나 임의 로컬 ID로 대체하지 않는다.
        public static string ResolveSettingsUuid(bool toss, string nativeId, string verifiedTossId) =>
            (toss ? verifiedTossId : nativeId)?.Trim() ?? string.Empty;

        public static string FormatSettingsUuid(string id)
        {
            string value = id?.Trim() ?? string.Empty;
            if (value.Length == 0) return "UID —";
            // 화면에서는 한 줄만 사용하며 복사에는 잘리지 않은 원문을 전달한다.
            return "UID " + (value.Length <= 18 ? value : value.Substring(0, 8) + "…" + value.Substring(value.Length - 6));
        }

        string ReadSettingsUuid() => UsesTossSettings
            ? ResolveSettingsUuid(true, null, AppsInTossIdentityPolicy.VerifiedUserHash)
            : MukJumpAccountRuntime.Instance?.BackendUid ?? string.Empty;

        void RefreshSettingsUuid()
        {
            if (!UsesTossSettings) MukJumpAccountRuntime.Instance?.RefreshDisplayIdentity();
            ApplySettingsUuid(ReadSettingsUuid());
            if (settingsNicknameText != null)
            {
                string nickname = MukJumpAccountRuntime.Instance?.Nickname ?? MukJumpIdentityProfile.GuestNickname;
                // 서버 이름 조회 전·미등록 상태에도 설치에 고정된 게스트 이름을 표시한다.
                // Apple 첫 이름 설정 여부는 서버 identity 상태로 별도 판단한다.
                settingsNicknameText.text = string.IsNullOrWhiteSpace(nickname)
                    ? MukJumpIdentityProfile.GuestNickname : nickname;
            }
        }

        void ApplySettingsUuid(string id)
        {
            if (settingsUuidText == null || settingsUuidButton == null) return;
            id = id?.Trim() ?? string.Empty;
            if (settingsUuid != id)
            {
                settingsUuid = id;
                uuidCopyStatus = string.Empty;
                settingsUuidCaption = null;
            }
            // 설정이 열린 매 프레임 같은 UID 문자열을 다시 할당하지 않는다.
            // 소유자가 바뀌거나 UID가 해제되면 같은 프레임에 다시 만든다.
            if (settingsUuidCaption == null) settingsUuidCaption = FormatSettingsUuid(settingsUuid);
            if (Time.unscaledTime >= uuidCopyStatusUntil)
                uuidCopyStatus = string.Empty;
            string source = string.IsNullOrEmpty(uuidCopyStatus) ? settingsUuidCaption : uuidCopyStatus;
            var binding = settingsUuidText.GetComponent<InkLocalizedText>();
            if (binding == null || binding.SourceText != source)
                InkLocalizedText.SetSource(settingsUuidText, source);
            settingsUuidButton.interactable = !string.IsNullOrEmpty(settingsUuid) && !uuidCopyPending;
        }

        void CopySettingsUuid()
        {
            RefreshSettingsUuid();
            if (string.IsNullOrEmpty(settingsUuid) || uuidCopyPending) return;
            string id = settingsUuid;
#if UNITY_WEBGL && !UNITY_EDITOR
            CopyTossSettingsUuid(id);
#else
            try
            {
                GUIUtility.systemCopyBuffer = id;
                ShowSettingsUuidCopyResult(id, true);
            }
            catch (System.Exception) { ShowSettingsUuidCopyResult(id, false); }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async void CopyTossSettingsUuid(string id)
        {
            uuidCopyPending = true;
            RefreshSettingsUuid();
            try
            {
                await AppsInToss.AIT.SetClipboardText(id, 5000);
                ShowSettingsUuidCopyResult(id, true);
            }
            catch (System.Exception) { ShowSettingsUuidCopyResult(id, false); }
            finally
            {
                if (this != null) { uuidCopyPending = false; RefreshSettingsUuid(); }
            }
        }
#endif

        void ShowSettingsUuidCopyResult(string id, bool success)
        {
            if (this == null || !isActiveAndEnabled || ReadSettingsUuid() != id) return;
            uuidCopyStatus = success ? "UUID 복사됨" : "복사하지 못했어요";
            uuidCopyStatusUntil = Time.unscaledTime + 1.4f;
            RefreshSettingsUuid();
        }

        static Button CreateLegalFooterLink(string name, Transform parent, string label, bool right)
        {
            // 보이는 것은 작은 글자와 밑줄뿐이며 실제 입력 영역은 340×120을 유지한다.
            var hit = CreateImage(name, parent, null,
                new Vector2(right ? 180 : -180, -634), new Vector2(340, 120), Color.clear);
            var button = hit.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, hit);
            var text = CreateReadableText("Label", hit.transform, label, 32,
                Vector2.zero, new Vector2(340, 52), InkPalette.TextMuted,
                right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, strong: true, wrap: false);
            button.targetGraphic = text;
            float width = Mathf.Clamp(text.preferredWidth, 1, 340);
            CreateImage("Underline", hit.transform, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2((right ? 1 : -1) * (340 - width) * 0.5f, -19),
                new Vector2(width, 2), WithAlpha(InkPalette.TextMuted, 0.65f));
            return button;
        }

        static readonly Dictionary<string, Sprite> settingsIcons = new Dictionary<string, Sprite>();

        static Image CreateIcon(string name, Transform parent, string key, Vector2 position, float size)
        {
            if (!settingsIcons.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>($"MukJump/UI/Common/settings_icon_{key}_v1");
                settingsIcons[key] = sprite;
            }
            var icon = CreateImage(name, parent, sprite, position, new Vector2(size, size), Color.white);
            icon.preserveAspect = true;
            // 임포트 전에는 단색 사각형을 폴백으로 노출하지 않는다.
            icon.enabled = sprite != null;
            icon.raycastTarget = false;
            return icon;
        }

        static Button CreateIconMenu(string name, Transform parent, string label, string icon,
            Vector2 position, Vector2? size = null)
        {
            Vector2 dimensions = size ?? new Vector2(348, 128);
            var button = CreatePaperButton(name, parent, label, position, dimensions, 48);
            Transform face = button.transform.Find("Paper");
            var caption = face.Find("Label").GetComponent<Text>();
            caption.rectTransform.anchoredPosition = new Vector2(42, 0);
            caption.rectTransform.sizeDelta = new Vector2(dimensions.x - 150, 82);
            CreateIcon("Icon", face, icon, new Vector2(-dimensions.x * 0.5f + 65, 0), 86);
            return button;
        }

        static Button CreateQuickToggle(string name, Transform parent, string label, float x,
            out Text status, int glyph)
        {
            // 사각 hit rect는 보이지 않게 유지하고 한지 한 장만 그린다.
            // 작은 타일에 CTA용 굵은 외곽선을 더하지 않아 종이 결이 먼저 보이게 한다.
            var hit = CreateImage(name, parent, null,
                new Vector2(x, 298), new Vector2(220, 260), Color.clear);
            hit.raycastTarget = true;
            var paper = CreateHanjiPaper("Paper", hit.transform, new Vector2(220, 260));
            var button = hit.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, paper);
            button.colors = InkUiStyle.ActionButtonColors();
            paper.raycastTarget = false;
            Transform face = paper.transform;
            // 종이 위아래에 24px 이상 여백을 남기고 세 내용 영역을 분리한다.
            CreateReadableText("Name", face, label, 40, new Vector2(0, -20),
                new Vector2(176, 56), InkPalette.TextDark, TextAnchor.MiddleCenter, strong: true);
            status = CreateReadableText("State", face, "켜짐", 40, new Vector2(0, -82),
                new Vector2(164, 48), InkPalette.TextDark, TextAnchor.MiddleCenter, strong: true);
            string key = glyph == 0 ? "music" : glyph == 1 ? "sound" : glyph == 2 ? "haptics" : "motion";
            CreateIcon("Icon", face, key, new Vector2(0, 64), 80);
            return button;
        }

        void BuildPlaySettingsPage(Transform panel)
        {
            var back = CreateBrushButton(
                "PlaySettingsBack", panel, "설정",
                new Vector2(-250f, 620f),
                new Vector2(200f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize,
                ActionButtonRole.Secondary);
            back.onClick.AddListener(ShowOptionsPage);

            CreateReadableText(
                "PlaySettingsTitle", panel, "소리·화면",
                72,
                new Vector2(60f, 620f), new Vector2(480f, 96f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateDivider(panel, "PlaySettingsDivider", 450f, 680f);

            CreateAudioCard(panel, "BgmCard", "배경음", new Vector2(0, 330),
                out bgmSlider, out bgmValue, out Button bgmToggle, out bgmToggleLabel);
            bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
            bgmToggle.onClick.AddListener(ToggleBgm);
            CreateAudioCard(panel, "SfxCard", "효과음", new Vector2(0, 175),
                out sfxSlider, out sfxValue, out Button sfxToggle, out sfxToggleLabel);
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            sfxToggle.onClick.AddListener(ToggleSfx);
            var haptics = CreateWideUtilityButton(
                "HapticsButton", panel,
                "햅틱", "켜짐",
                new Vector2(0f, 15f), out hapticsStatus);
            haptics.onClick.AddListener(() =>
            {
                LobbySettingsProfile.SetHapticsEnabled(
                    !LobbySettingsProfile.HapticsEnabled);
                RefreshPlaySettings();
            });


            var done = CreateBrushButton(
                "PlaySettingsDone", panel, "완료",
                new Vector2(0f, -620f),
                new Vector2(390f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CardTitleSize);
            done.onClick.AddListener(ShowOptionsPage);
            RefreshPlaySettings();
        }

        void BuildTossIdentityPage(Transform panel)
        {
            // 일반 옵션에는 로그인 메뉴가 없다. 식별 실패 때 필요한 복구 동선만 남긴다.
            CreateReadableText("AccountTitle", panel, "기록 확인", 72,
                new Vector2(0, 300), new Vector2(680, 96),
                InkPalette.TextDark, TextAnchor.MiddleCenter, strong: true);
            accountStatusText = CreateReadableText("AccountStatus", panel,
                "토스 사용자 확인 중", 44, new Vector2(0, 70), new Vector2(680, 230),
                InkPalette.TextDark, TextAnchor.MiddleCenter, strong: true);
            tossIdentityRetry = CreateBrushButton("RetryTossIdentity", panel, "다시 확인",
                new Vector2(0, -170), new Vector2(390, 120), 52);
            tossIdentityRetry.onClick.AddListener(() =>
                MukJumpAccountRuntime.Instance?.RetryPendingProfileResolution());
            RefreshTossIdentityState();
        }

        void BuildAccountPage(Transform panel)
        {
            CreateReadableText(
                "AccountTitle", panel, "계정 연동",
                72,
                new Vector2(0f, 365f), new Vector2(640f, 96f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateIcon("AccountArt", panel, "account", new Vector2(0, 245), 96);
            accountKindText = CreateReadableText(
                "AccountKind", panel, "연동 안 됨",
                52,
                new Vector2(0f, 320f), new Vector2(650f, 76f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            accountPlayerIdText = CreateReadableText(
                "AccountPlayerId", panel, string.Empty, 32,
                Vector2.zero, new Vector2(680f, 44f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            accountPlayerIdText.supportRichText = false;
            InkLocalizedText.Exclude(accountPlayerIdText);

            // 이번 배포는 Apple만 노출한다. Google 버튼이나 빈 자리도 만들지 않는다.
            accountAppleButton = CreateAppleSignInPlaceholder(
                panel,
                new Vector2(0f, -125f));
            accountAppleButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.SignInWithApple());
            // Android 1.0과 WebGL은 fresh authorization code를 안전하게 얻는
            // 탈퇴 철회 흐름이 없어 Apple 계정 생성을 제공하지 않는다.
            accountAppleButton.gameObject.SetActive(
                ShouldOfferAppleSignIn(UiPlatform));

            accountConflictRoot = CreateRect(
                "AccountConflict",
                panel,
                Vector2.zero,
                new Vector2(780f, 1490f));
            Image accountConflictBlocker = CreateStretchImage(
                "ModalBlocker",
                accountConflictRoot,
                InkPalette.Paper);
            accountConflictBlocker.raycastTarget = true;
            accountConflictBlocker.color = Color.clear;
            HanjiScrollFrame.Attach(accountConflictRoot, new Vector2(764, 1450));
            CreateReadableText(
                "ConflictTitle", accountConflictRoot,
                "이미 사용 중인 계정",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 250f), new Vector2(680f, 130f),
                InkPalette.Red, TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "ConflictCaption", accountConflictRoot,
                "기존 Apple 계정으로 전환합니다.\n현재 게스트 계정과 기록은 삭제되며 복구할 수 없습니다.",
                InkUiStyle.BodySize,
                new Vector2(0f, 65f), new Vector2(660f, 140f),
                InkPalette.TextDark, TextAnchor.MiddleCenter);
            var useExisting = CreatePaperButton(
                "UseExistingAccount", accountConflictRoot,
                "알겠습니다",
                new Vector2(0f, -115f),
                new Vector2(600f, 156f),
                InkUiStyle.ActionButtonLabelSize,
                useHanji: true);
            useExisting.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .UseExistingAccountAfterConflict());
            InkUiStyle.SetActionButtonRole(
                useExisting.GetComponent<Image>(), ActionButtonRole.Primary);
            var keepGuest = CreatePaperButton(
                "KeepGuestAccount", accountConflictRoot,
                "게스트로 계속하기",
                new Vector2(0f, -305f),
                new Vector2(600f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize,
                useHanji: true);
            keepGuest.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .KeepCurrentGuestAfterConflict());

            syncConflictRoot = CreateRect(
                "SyncConflict",
                panel,
                Vector2.zero,
                new Vector2(780f, 1490f));
            Image syncConflictBlocker = CreateStretchImage(
                "ModalBlocker",
                syncConflictRoot,
                InkPalette.Paper);
            syncConflictBlocker.raycastTarget = true;
            syncConflictBlocker.color = Color.clear;
            HanjiScrollFrame.Attach(syncConflictRoot, new Vector2(764, 1450));
            CreateReadableText(
                "SyncConflictTitle", syncConflictRoot,
                "기록 선택이 필요합니다",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 300f), new Vector2(680f, 90f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "SyncConflictCaption", syncConflictRoot,
                "사용할 성장 기록을 선택하세요\n서버와 기기 기록은 합쳐지지 않아요",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 115f), new Vector2(690f, 150f),
                InkPalette.Red, TextAnchor.MiddleCenter,
                strong: true);
            var useServer = CreatePaperButton(
                "UseServerRecord", syncConflictRoot,
                "서버 기록 사용",
                new Vector2(-180f, -85f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize,
                useHanji: true);
            useServer.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .UseServerAfterSyncConflict());
            var useDevice = CreatePaperButton(
                "UseDeviceRecord", syncConflictRoot,
                "이 기기 기록 사용",
                new Vector2(180f, -85f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize,
                useHanji: true);
            useDevice.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .KeepThisDeviceAfterSyncConflict());

            accountSyncPendingRoot = CreateRect(
                "AccountSyncPending",
                panel,
                Vector2.zero,
                new Vector2(780f, 1490f));
            Image accountSyncBlocker = CreateStretchImage(
                "ModalBlocker",
                accountSyncPendingRoot,
                InkPalette.Paper);
            accountSyncBlocker.raycastTarget = true;
            accountSyncBlocker.color = Color.clear;
            HanjiScrollFrame.Attach(accountSyncPendingRoot, new Vector2(764, 1450));
            accountSyncPendingTitleText = CreateReadableText(
                "SyncPendingTitle", accountSyncPendingRoot,
                "계정 기록 확인 중",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 315f), new Vector2(680f, 90f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            accountSyncPendingStatusText = CreateReadableText(
                "SyncPendingStatus", accountSyncPendingRoot,
                "서버 기록을 안전하게 확인하고 있습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 145f), new Vector2(680f, 130f),
                InkPalette.Red, TextAnchor.MiddleCenter,
                strong: true);
            accountSyncPendingCaptionText = CreateReadableText(
                "SyncPendingCaption", accountSyncPendingRoot,
                "확인 후 게임을 시작할 수 있어요",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 30f), new Vector2(690f, 100f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            accountSyncRetryButton = CreatePaperButton(
                "RetryAccountSync", accountSyncPendingRoot,
                "다시 확인",
                new Vector2(-180f, -130f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize,
                useHanji: true);
            accountSyncRetryButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .RetryPendingProfileResolution());
            InkUiStyle.SetActionButtonRole(
                accountSyncRetryButton.GetComponent<Image>(),
                ActionButtonRole.Primary);
            accountSyncReturnButton = CreatePaperButton(
                "ReturnToLocalGuest", accountSyncPendingRoot,
                "로컬 게스트로 돌아가기",
                new Vector2(180f, -130f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize,
                useHanji: true);
            accountSyncReturnButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .ReturnToLocalGuestDuringAccountSync());

            accountLogoutButton = CreatePaperButton(
                "AccountLogout", panel, "로그아웃",
                new Vector2(-180f, -315f), new Vector2(340, 120), 44);
            accountLogoutButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.Logout());
            accountDeleteButton = CreateBrushButton(
                "AccountDelete", panel, "계정 삭제",
                new Vector2(180f, -315f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize,
                ActionButtonRole.Secondary);
            accountDeleteLabel = accountDeleteButton.transform
                .Find("Label")?.GetComponent<Text>();
            accountDeleteButton.onClick.AddListener(HandleDeleteAccount);

            accountCloseButton = CreatePaperButton("AccountClose", panel, string.Empty,
                new Vector2(0, -590), new Vector2(120, 120), 36);
            var closePaper = accountCloseButton.transform.Find("Paper").GetComponent<Image>();
            closePaper.rectTransform.sizeDelta = new Vector2(96, 96);
            for (int i = 0; i < 2; i++)
            {
                var stroke = CreateImage("CrossStroke" + i, closePaper.transform,
                    InkUiTextureFactory.CreateBrushSprite(), Vector2.zero,
                    new Vector2(48, 7), InkPalette.TextDark);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
            }
            accountCloseButton.onClick.AddListener(ShowOptionsPage);
            RefreshAccountState();
        }

        public static bool ShouldOfferAppleSignIn(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.IPhonePlayer ||
                   platform == RuntimePlatform.OSXEditor ||
                   platform == RuntimePlatform.WindowsEditor ||
                   platform == RuntimePlatform.LinuxEditor;
        }

        void BuildLeaderboardPage(Transform panel)
        {
            CreateReadableText("LeaderboardTitle", panel, "세계 최고의 먹", 64,
                new Vector2(0f, 555f), new Vector2(600f, 82f),
                InkPalette.TextDark, TextAnchor.MiddleCenter, strong: true);
            Text rankHeading = CreateReadableText("RankHeading", panel, "순위", 36, new Vector2(-292f, 430f),
                new Vector2(96f, 50f), InkPalette.TextDark, TextAnchor.MiddleLeft, strong: true);
            InkLocalizedText.OverrideEnglish(rankHeading, "Rank");
            CreateReadableText("NameHeading", panel, "이름", 36, new Vector2(-65f, 430f),
                new Vector2(340f, 50f), InkPalette.TextDark, TextAnchor.MiddleLeft, strong: true);
            CreateReadableText("HeightHeading", panel, "고도", 36, new Vector2(245f, 430f),
                new Vector2(190f, 50f), InkPalette.TextDark, TextAnchor.MiddleRight, strong: true);
            CreateDivider(panel, "LeaderboardDivider", 390f, 680f);
            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                float y = 325f - i * 94f;
                leaderboardRanks[i] = CreateReadableText($"Rank{i + 1}", panel, "", 42,
                    new Vector2(-292f, y), new Vector2(96, 62),
                    i < 3 ? InkPalette.Red : InkPalette.TextDark, TextAnchor.MiddleLeft, strong: true);
                var cell = CreateRect($"NameCell{i + 1}", panel,
                    new Vector2(-36f, y), new Vector2(282f, 62f));
                leaderboardFlags[i] = CreateImage($"RegionFlag{i + 1}", panel,
                    RegionFlagImages.Get(null), new Vector2(-213f, y),
                    new Vector2(44f, 44f), Color.white);
                leaderboardFlags[i].preserveAspect = true;
                leaderboardFlags[i].raycastTarget = false;
                leaderboardFlags[i].enabled = false;
                cell.gameObject.AddComponent<RectMask2D>();
                leaderboardNames[i] = CreateReadableText($"Name{i + 1}", cell, "", 40,
                    Vector2.zero, new Vector2(282f, 62f), InkPalette.TextDark,
                    TextAnchor.MiddleLeft, strong: true);
                leaderboardNames[i].supportRichText = false;
                leaderboardNames[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                leaderboardNames[i].resizeTextForBestFit = false;
                leaderboardRows[i] = CreateReadableText($"LeaderboardRow{i + 1}", panel, "", 42,
                    new Vector2(245f, y), new Vector2(190f, 62f),
                    InkPalette.TextDark, TextAnchor.MiddleRight, strong: true);
            }
            RefreshLeaderboardPage();
        }

        void BindAccountRuntime()
        {
            MukJumpAccountRuntime next = MukJumpAccountRuntime.Instance;
            if (ReferenceEquals(boundAccountRuntime, next))
                return;
            UnbindAccountRuntime();
            boundAccountRuntime = next;
            if (boundAccountRuntime != null)
                boundAccountRuntime.StateChanged += HandleAccountStateChanged;
            RefreshAccountState();
        }

        void UnbindAccountRuntime()
        {
            if (boundAccountRuntime != null)
                boundAccountRuntime.StateChanged -= HandleAccountStateChanged;
            boundAccountRuntime = null;
        }

        void HandleAccountStateChanged()
        {
            // 저장 완료·네트워크 콜백처럼 인증 상태를 유지하는 이벤트도
            // 첫 삭제 탭과 무관한 비동기 경계이므로 확인 상태를 취소한다.
            DisarmDeleteConfirmation();
            RefreshAccountState();
        }

        void ShowAccountPage()
        {
            if (scrollPageTransition) return;
            if (!UsesTossSettings && !showingAccountPage && IsOpen)
            {
                TransitionScrollPage(ShowAccountPageImmediate);
                return;
            }
            ShowAccountPageImmediate();
        }

        void ShowAccountPageImmediate()
        {
            DisarmDeleteConfirmation();
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, true);
            SetPageVisible(leaderboardGroup, false);
            BindAccountRuntime();
            RefreshAccountState();
        }

        void ShowLeaderboardPage()
        {
            if (scrollPageTransition || ShowingLeaderboardPage) return;
            // 토스는 사용자별 행 조회 API가 없으므로 빈 자체 표를 열지 않는다.
            // 로비 순위는 공식 게임센터 진입점을 사용한다.
            if (UsesTossSettings)
            {
                if (MukJumpAccountRuntime.Instance != null &&
                    MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                    ShowAccountPage();
                else
                    AppsInTossGameCenterRuntime.OpenLeaderboard();
                return;
            }
            if (IsOpen)
            {
                TransitionScrollPage(ShowLeaderboardPageImmediate);
                return;
            }
            ShowLeaderboardPageImmediate();
        }

        void ShowLeaderboardPageImmediate()
        {
            showingAppleLeaderboard = false;
            DisarmDeleteConfirmation();
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, true);
            BindAccountRuntime();
            RefreshLeaderboardPage();
#if !UNITY_EDITOR
            if (!UsesTossSettings) MukJumpAccountRuntime.Instance?.RefreshLeaderboard();
#endif
        }

        void RefreshSelectedLeaderboard()
        {
            if (UsesTossSettings) AppsInTossGameCenterRuntime.OpenLeaderboard();
            else if (showingAppleLeaderboard) AppleGameCenterRuntime.LoadLeaderboard();
            else MukJumpAccountRuntime.Instance?.RefreshLeaderboard();
            RefreshLeaderboardPage();
        }

        void RefreshLeaderboardPage()
        {
            if (globalLeaderboardLabel != null) globalLeaderboardLabel.color =
                showingAppleLeaderboard ? InkPalette.TextDark : InkPalette.Red;
            if (appleLeaderboardLabel != null) appleLeaderboardLabel.color =
                showingAppleLeaderboard ? InkPalette.Red : InkPalette.TextDark;
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;

            IReadOnlyList<MukJumpLeaderboardEntry> entries =
                runtime?.LeaderboardEntries;
            if (showingAppleLeaderboard)
            {
                entries = AppleGameCenterRuntime.Entries;
            }
            if (UsesTossSettings)
            {
                entries = null;
            }
            if (leaderboardRefresh != null)
                leaderboardRefresh.interactable = showingAppleLeaderboard
                    ? !AppleGameCenterRuntime.Loading : UsesTossSettings || runtime == null || !runtime.LeaderboardLoading;
            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                if (leaderboardRows[i] == null)
                    continue;
                leaderboardRows[i].text = entries != null && i < entries.Count
                    ? $"{entries[i].Height:N0} m"
                    : string.Empty;
                if (leaderboardRanks[i] != null)
                    leaderboardRanks[i].text = entries != null && i < entries.Count ? $"{entries[i].Rank}" : string.Empty;
                bool hasEntry = entries != null && i < entries.Count;
                if (leaderboardFlags[i] != null)
                {
                    leaderboardFlags[i].enabled = hasEntry;
                    if (hasEntry) leaderboardFlags[i].sprite = RegionFlagImages.Get(entries[i].RegionCode);
                }
                if (leaderboardSeals[i] != null) leaderboardSeals[i].enabled = hasEntry;
                if (leaderboardNames[i] != null)
                    FitLeaderboardName(leaderboardNames[i], hasEntry
                        ? (entries[i].DisplayName == "이름 없는 먹방울"
                            ? GameLocalization.Translate("이름 없는 먹방울") : entries[i].DisplayName)
                        : string.Empty);
                if (leaderboardSources[i] != null) leaderboardSources[i].text = !hasEntry ? string.Empty :
                    entries[i].Source == "APPLE" ? "GC" : entries[i].Source == "TOSS" ? "토스" : "뒤끝";
            }
        }

        public static void FitLeaderboardName(Text label, string value)
        {
            if (label == null) return;
            InkLocalizedText.Exclude(label);
            label.supportRichText = false;
            label.text = value ?? string.Empty;
            float width = Mathf.Max(0f, label.rectTransform.rect.width - 4f);
            if (label.preferredWidth <= width) return;
            // 문자 수뿐 아니라 실제 폰트 폭으로 줄인다. 크기는 줄이지 않고
            // grapheme 단위로 생략해 이모지/결합 문자의 중간을 자르지 않는다.
            var elements = new List<string>();
            var iterator = System.Globalization.StringInfo.GetTextElementEnumerator(label.text);
            while (iterator.MoveNext()) elements.Add(iterator.GetTextElement());
            if (elements.Count > 0 && elements[elements.Count - 1] == "…")
                elements.RemoveAt(elements.Count - 1);
            while (elements.Count > 0)
            {
                elements.RemoveAt(elements.Count - 1);
                label.text = string.Concat(elements) + "…";
                if (label.preferredWidth <= width) return;
            }
            label.text = string.Empty;
        }

        void HandleDeleteAccount()
        {
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            if (runtime == null || !runtime.IsOnlineAuthenticated || IsDeleteConfirmationOpen ||
                runtime.BlocksGameplayForAccountSync || runtime.Phase == MukJumpAccountPhase.Deleting)
                return;
            ArmDeleteConfirmation(Time.unscaledTime);
        }

        void ArmDeleteConfirmation(float now)
        {
            BuildDeleteConfirmation();
            deleteConfirmationArmed = true;
            deleteConfirmationArmedAt = now;
            ShowDeleteConfirmation();
        }

        void DisarmDeleteConfirmation()
        {
            deleteConfirmationArmed = false;
            deleteConfirmationArmedAt = float.NegativeInfinity;
            HideDeleteConfirmation();
            if (accountDeleteLabel != null)
                InkLocalizedText.SetSource(accountDeleteLabel, "계정 삭제");
            InkUiStyle.SetActionButtonRole(
                accountDeleteButton != null
                    ? accountDeleteButton.GetComponent<Image>()
                    : null,
                ActionButtonRole.Secondary);
        }

        public static bool IsDeleteConfirmationReady(float armedAt, float now)
        {
            float elapsed = now - armedAt;
            return elapsed >= DeleteConfirmationMinimumDelay && !float.IsInfinity(elapsed);
        }

        void RefreshAccountState()
        {
            RefreshSettingsUuid();
            if (UsesTossSettings)
            {
                RefreshTossIdentityState();
                return;
            }
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            if (accountKindText == null)
                return;

            bool online = runtime != null && runtime.IsOnlineAuthenticated;
            MukJumpAccountKind kind = runtime != null
                ? runtime.AccountKind
                : MukJumpAccountKind.LocalGuest;
            InkLocalizedText.SetSource(accountKindText, kind switch
            {
                MukJumpAccountKind.Google => online ? "Google 연동됨" : "Google 연결 확인",
                MukJumpAccountKind.Apple => online ? "Apple 연동됨" : "Apple 연결 확인",
                MukJumpAccountKind.BackendGuest => online ? "게스트 계정" : "게스트 · 오프라인",
                _ => "연동 안 됨",
            });
            string accountStatus = EssentialAccountStatus(runtime?.StatusMessage);
            if (online && kind == MukJumpAccountKind.BackendGuest &&
                (string.IsNullOrEmpty(accountStatus) || accountStatus == "계정 연결 완료" ||
                 accountStatus == "게스트 계정 연결 완료"))
                accountStatus = "기기 변경 전에 계정을 연동해 주세요";
            if (accountPlayerIdText != null)
            {
                string playerId = ReadSettingsUuid();
                accountPlayerIdText.text = string.IsNullOrWhiteSpace(playerId) ? string.Empty : "UID " + playerId;
            }
            if (settingsAccountLabel != null)
                InkLocalizedText.SetSource(settingsAccountLabel, online && HasLinkedProvider(kind) ? "연동 계정" : "계정 연동");
            bool busy = runtime != null &&
                (runtime.Phase == MukJumpAccountPhase.Connecting ||
                 runtime.Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                 runtime.Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                 runtime.Phase == MukJumpAccountPhase.Deleting ||
                 runtime.IsTemporaryBackendPaused ||
                 runtime.BlocksGameplayForAccountSync);
            if (deleteConfirmationArmed && (!online || busy))
                DisarmDeleteConfirmation();
            ApplyAccountActions(online, kind, busy, ShouldOfferAppleSignIn(UiPlatform));
            if (accountConflictRoot != null)
            {
                bool active = runtime != null &&
                              runtime.HasPendingAccountConflict;
                HanjiScrollFrame.SetActiveAnimated(accountConflictRoot.gameObject, active);
                if (active)
                    accountConflictRoot.SetAsLastSibling();
            }
            if (syncConflictRoot != null)
            {
                bool active = runtime != null &&
                              runtime.HasPendingSyncConflict;
                HanjiScrollFrame.SetActiveAnimated(syncConflictRoot.gameObject, active);
                if (active)
                    syncConflictRoot.SetAsLastSibling();
            }
            if (accountSyncPendingRoot != null)
            {
                bool active = runtime != null &&
                              runtime.BlocksGameplayForAccountSync &&
                              !runtime.HasPendingAccountConflict &&
                              !runtime.HasPendingSyncConflict;
                // 계정/기록 선택은 전용 버튼으로만 진행한다. 일반 재시도 창이
                // 위를 덮으면 아직 전환하지 않은 게스트를 잘못된 소유자로 판정한다.
                bool deleting = runtime != null &&
                                runtime.HasPendingAccountDeletionCleanup;
                bool canReturnToLocal = !deleting && runtime != null &&
                                        runtime
                                            .CanReturnToLocalGuestDuringAccountSync;
                HanjiScrollFrame.SetActiveAnimated(accountSyncPendingRoot.gameObject, active);
                if (accountSyncPendingTitleText != null)
                    InkLocalizedText.SetSource(accountSyncPendingTitleText, deleting
                        ? "계정 삭제 마무리 중"
                        : "계정 기록 확인 중");
                if (accountSyncPendingStatusText != null)
                    InkLocalizedText.SetSource(accountSyncPendingStatusText, runtime != null
                        ? runtime.StatusMessage
                        : "계정 기록을 확인하고 있습니다");
                if (accountSyncPendingCaptionText != null)
                    InkLocalizedText.SetSource(accountSyncPendingCaptionText, deleting
                        ? "기기에 남은 기록을 정리하고 있어요"
                        : "확인 후 게임을 시작할 수 있어요");
                if (accountSyncReturnButton != null)
                    accountSyncReturnButton.gameObject.SetActive(
                        canReturnToLocal);
                if (accountSyncRetryButton != null)
                {
                    RectTransform retryRect = accountSyncRetryButton
                        .GetComponent<RectTransform>();
                    if (retryRect != null)
                        retryRect.anchoredPosition = new Vector2(
                            canReturnToLocal ? -180f : 0f,
                            -130f);
                }
                if (active)
                    accountSyncPendingRoot.SetAsLastSibling();
            }
            if (!deleteConfirmationArmed && accountDeleteLabel != null)
            {
                InkLocalizedText.SetSource(accountDeleteLabel, "계정 삭제");
                InkUiStyle.SetActionButtonRole(
                    accountDeleteButton.GetComponent<Image>(),
                    ActionButtonRole.Secondary);
            }
            RefreshAccountNotice(accountStatus, runtime != null &&
                (runtime.HasPendingAccountConflict || runtime.HasPendingSyncConflict || runtime.BlocksGameplayForAccountSync));
            SyncNativeAppleSignInButton();
            RefreshLeaderboardPage();
        }

        public static bool HasLinkedProvider(MukJumpAccountKind kind) =>
            kind == MukJumpAccountKind.Google || kind == MukJumpAccountKind.Apple;

        void ApplyAccountActions(bool online, MukJumpAccountKind kind, bool busy, bool appleAvailable)
        {
            // 이미 연동한 계정에는 다른 로그인 수단을 나열하지 않는다.
            // 이번 배포에서 제공하는 Apple의 재연결 경로는 인증 만료 뒤에도 유지한다.
            bool guest = !HasLinkedProvider(kind);
            bool apple = appleAvailable && (guest || (!online && kind == MukJumpAccountKind.Apple));
            bool canLogin = CanStartSocialLogin(online, kind, busy);
            if (accountAppleButton != null)
            {
                accountAppleButton.gameObject.SetActive(apple);
                accountAppleButton.interactable = canLogin;
                accountAppleButton.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(0, -50);
            }
            float actionY = apple ? -315 : -80;
            if (accountLogoutButton != null)
            {
                // 게스트는 로그아웃 대신 기존 계정 삭제 경로만 제공한다.
                accountLogoutButton.gameObject.SetActive(online && !guest);
                accountLogoutButton.interactable = online && !guest && !busy;
                accountLogoutButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, actionY);
            }
            if (accountDeleteButton != null)
            {
                accountDeleteButton.gameObject.SetActive(online);
                accountDeleteButton.interactable = online && !busy;
                accountDeleteButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(180, actionY);
            }
            var runtime = MukJumpAccountRuntime.Instance;
            LayoutAccountContents(runtime != null && (runtime.HasPendingAccountConflict ||
                runtime.HasPendingSyncConflict || runtime.BlocksGameplayForAccountSync));
        }

        void LayoutAccountContents(bool requiredChoice)
        {
            if (accountKindText == null) return;
            Transform page = accountKindText.transform.parent;
            bool Visible(Button button) => button != null && button.gameObject.activeSelf;
            int rows = (Visible(accountAppleButton) ? 1 : 0) +
                       (Visible(accountLogoutButton) || Visible(accountDeleteButton) ? 1 : 0);
            // 안내는 종이 밖 토스트에 표시한다. UID가 없으면 그 자리도 예약하지 않는다.
            bool hasId = accountPlayerIdText != null && !string.IsNullOrWhiteSpace(accountPlayerIdText.text);
            if (accountPlayerIdText != null) accountPlayerIdText.gameObject.SetActive(hasId);
            float firstActionOffset = hasId ? 515f : 465f;
            accountPaperHeight = firstActionOffset + Mathf.Max(1, rows) * 145f + 40f;
            // 선택/복구 팝업의 고정 설명과 버튼은 축소된 일반 계정 종이에 자르지 않는다.
            if (requiredChoice) accountPaperHeight = Mathf.Max(accountPaperHeight, 845f);
            float top = accountPaperHeight * 0.5f;
            void Place(Transform child, float y, float x = 0) =>
                ((RectTransform)child).anchoredPosition = new Vector2(x, y);
            Place(page.Find("AccountTitle"), top - 100);
            Place(page.Find("AccountArt"), top - 220);
            Place(accountKindText.transform, top - 325);
            if (accountPlayerIdText != null) Place(accountPlayerIdText.transform, top - 393);
            float nextY = top - firstActionOffset;
            foreach (var login in new[] { accountAppleButton })
            {
                if (!Visible(login)) continue;
                Place(login.transform, nextY);
                nextY -= 145;
            }
            if (Visible(accountLogoutButton) || Visible(accountDeleteButton))
            {
                bool paired = Visible(accountLogoutButton) && Visible(accountDeleteButton);
                if (Visible(accountLogoutButton))
                    Place(accountLogoutButton.transform, nextY, paired ? -180 : 0);
                if (Visible(accountDeleteButton))
                    Place(accountDeleteButton.transform, nextY, paired ? 180 : 0);
            }
            if (accountCloseButton != null) Place(accountCloseButton.transform, -top - 125);
            foreach (var overlay in new[] { accountConflictRoot, syncConflictRoot, accountSyncPendingRoot })
            {
                if (overlay == null) continue;
                overlay.sizeDelta = new Vector2(780, accountPaperHeight + 60);
                overlay.GetComponent<HanjiScrollFrame>().SetPaperSize(new Vector2(764, accountPaperHeight));
            }
            if (showingAccountPage) ApplyAccountPaperSize();
        }

        void BuildAccountToast()
        {
            // 두루마리 밖 별도 형제에 두어 접기 마스크와 버튼 입력에 영향을 주지 않는다.
            var rect = CreateRect("AccountToast", safeAreaRoot, Vector2.zero, new Vector2(764, 120));
            accountToast = rect.gameObject.AddComponent<CanvasGroup>();
            accountToast.interactable = accountToast.blocksRaycasts = false;
            var paper = CreateHanjiPaper("Paper", rect, rect.sizeDelta);
            paper.color = InkPalette.Ink;
            paper.raycastTarget = false;
            accountToastText = CreateReadableText("Message", rect, string.Empty, 36,
                Vector2.zero, new Vector2(684, 180), InkPalette.TextLight, TextAnchor.MiddleCenter);
            accountToastText.supportRichText = false;
            accountToastText.raycastTarget = false;
            ClearAccountToast();
        }

        void RefreshAccountNotice(string status, bool requiredChoice)
        {
            if (!showingAccountPage) return;
            // 반드시 선택해야 하는 복구/동기화 안내는 기존 차단 팝업에 그대로 남긴다.
            if (requiredChoice)
            {
                ClearAccountToast();
                lastAccountToastStatus = null;
                return;
            }
            if (status == lastAccountToastStatus || deleteConfirmationArmed) return;
            lastAccountToastStatus = status;
            if (string.IsNullOrEmpty(status)) ClearAccountToast();
            else ShowAccountNotice(status);
        }

        void ShowAccountNotice(string source)
        {
            if (!showingAccountPage || accountToast == null || string.IsNullOrWhiteSpace(source)) return;
            if (source == accountToastSource) return;
            accountToastSource = source;
            accountToastElapsed = 0;
            // 연속 상태 변경도 이미 보이는 토스트를 껐다 켜지 않는다.
            accountToastFadeFrom = accountToast.alpha;
            InkLocalizedText.SetSource(accountToastText, source);
            ApplySafeArea();
        }

        float LayoutAccountToastContents()
        {
            if (accountToast == null) return 120;
            // Canvas 배율/언어별 글꼴 갱신 후의 줄 높이 차이까지 여유를 둔다.
            float height = Mathf.Clamp(accountToastText.preferredHeight + 52, 96, 224);
            var rect = (RectTransform)accountToast.transform;
            rect.sizeDelta = new Vector2(764, height);
            ((RectTransform)rect.Find("Paper")).sizeDelta = rect.sizeDelta;
            accountToastText.rectTransform.sizeDelta = new Vector2(684, height - 40);
            return height;
        }

        void AdvanceAccountToast(float unscaledDelta, bool canPresent)
        {
            if (accountToast == null || string.IsNullOrEmpty(accountToastSource)) return;
            if (!canPresent)
            {
                accountToast.alpha = 0;
                return; // 두루마리 펼침이나 Apple 인증 복귀 전에 읽기 시간이 소진되지 않는다.
            }
            accountToastElapsed += Mathf.Max(0, unscaledDelta);
            float fadeOutStart = AccountToastFadeIn + AccountToastHold;
            accountToast.alpha = accountToastElapsed < AccountToastFadeIn
                ? Mathf.Lerp(accountToastFadeFrom, 1f,
                    1f - Mathf.Pow(1f - Mathf.Clamp01(accountToastElapsed / AccountToastFadeIn), 3f))
                : 1f - Mathf.Clamp01((accountToastElapsed - fadeOutStart) / AccountToastFadeOut);
            accountToast.transform.localScale = Vector3.one * (LobbySettingsProfile.ReducedMotionEnabled
                ? 1f : Mathf.Lerp(.985f, 1f, accountToast.alpha));
            if (accountToastElapsed >= fadeOutStart + AccountToastFadeOut) ClearAccountToast();
        }

        void ClearAccountToast()
        {
            accountToastSource = null;
            accountToastElapsed = 0;
            accountToastFadeFrom = 0f;
            if (accountToast != null)
            {
                accountToast.alpha = 0;
                accountToast.transform.localScale = Vector3.one;
            }
        }

        void ApplyAccountPaperSize()
        {
            optionsPanel.sizeDelta = new Vector2(PanelWidth, accountPaperHeight + 60);
            optionsPanel.GetComponent<HanjiScrollFrame>().SetPaperSize(new Vector2(764, accountPaperHeight));
            ApplySafeArea();
        }

        public static string EssentialAccountStatus(string message) => message switch
        {
            null => string.Empty,
            "게스트로 바로 플레이할 수 있습니다" => string.Empty,
            "로그인하지 않아도 바로 플레이할 수 있습니다" => string.Empty,
            "서버 설정 전에도 게스트로 모든 콘텐츠를 플레이할 수 있습니다" => string.Empty,
            "동기화 완료" => string.Empty,
            // OnlineReady/LocalReady에서도 저장 재시도·연결 실패가 올 수 있다.
            // 알려진 정상 안내만 생략하고 그 밖의 상태를 임의로 숨기지 않는다.
            _ => message,
        };

        void RefreshTossIdentityState()
        {
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            bool ready = runtime != null && runtime.HasVerifiedAppsInTossIdentity &&
                         !runtime.BlocksGameplayForAccountSync;
            bool busy = runtime == null || runtime.Phase == MukJumpAccountPhase.Connecting;
            ApplyTossIdentityState(ready, busy, runtime?.StatusMessage);
        }

        void ApplyTossIdentityState(bool ready, bool busy, string message)
        {
            if (accountStatusText != null)
                InkLocalizedText.SetSource(accountStatusText, ready ? string.Empty : busy ? "토스 사용자 확인 중" :
                    string.IsNullOrWhiteSpace(message) ? "사용자를 확인하지 못했어요. 다시 확인해 주세요" : message);
            if (tossIdentityRetry != null)
                tossIdentityRetry.interactable = !busy && !ready;
            // 식별 완료는 클라우드 저장 완료가 아니다. 로그인/동기화 완료 문구를 표시하지 않는다.
            if (ready && IsAccountOpen)
                ShowOptionsPage();
        }

        void SyncNativeAppleSignInButton()
        {
            bool visible = !IsNicknameOpen && IsAccountOpen && rootGroup.interactable && !scrollPageTransition &&
                           optionsPanel.GetComponent<HanjiScrollFrame>().IsReady && accountAppleButton != null &&
                           accountAppleButton.gameObject.activeInHierarchy &&
                           applicationFocused && !applicationPaused &&
                           !IsAccountOverlayOpen();
            AppleSignInButtonBridge.Sync(accountAppleButton, visible);
        }

        bool IsAccountOverlayOpen() =>
            IsDeleteConfirmationOpen ||
            accountConflictRoot != null &&
            accountConflictRoot.gameObject.activeInHierarchy ||
            syncConflictRoot != null &&
            syncConflictRoot.gameObject.activeInHierarchy ||
            accountSyncPendingRoot != null &&
            accountSyncPendingRoot.gameObject.activeInHierarchy;

        public static bool CanStartSocialLogin(
            bool online,
            MukJumpAccountKind kind,
            bool busy)
        {
            if (busy)
                return false;
            return !online || kind == MukJumpAccountKind.BackendGuest;
        }

        void BuildDebugScenarioPage(Transform panel)
        {
            var back = CreateBrushButton(
                "DebugScenarioBack",
                panel,
                "옵션으로",
                new Vector2(-250f, 640f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize,
                ActionButtonRole.Secondary);
            back.onClick.AddListener(ShowOptionsPage);

            CreateReadableText(
                "DebugScenarioTitle",
                panel,
                "연출 시나리오",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 535f),
                new Vector2(520f, 82f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "DebugScenarioCaption",
                panel,
                "선택한 상황은 시작 버튼을 누른 다음 판부터 적용됩니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 460f),
                new Vector2(680f, 52f),
                InkPalette.TextMuted,
                TextAnchor.MiddleCenter);

            debugScenarioSummary = CreateReadableText(
                "DebugScenarioSummary",
                panel,
                string.Empty,
                InkUiStyle.BodySize,
                new Vector2(0f, 392f),
                new Vector2(680f, 62f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter);

            IReadOnlyList<DebugShowcaseScenarioDefinition> definitions =
                DebugShowcaseScenarioProfile.Definitions;
            float[] yPositions = { 285f, 153f, 21f, -111f, -243f };
            for (int i = 0;
                 i < definitions.Count && i < debugScenarioButtons.Length;
                 i++)
            {
                DebugShowcaseScenarioDefinition definition = definitions[i];
                Button button = CreateDebugScenarioCard(
                    panel,
                    definition,
                    new Vector2(0f, yPositions[i]));
                DebugShowcaseScenarioId selectedId = definition.Id;
                button.onClick.AddListener(
                    () => DebugShowcaseScenarioProfile.Select(selectedId));
                debugScenarioButtons[i] = button;
            }

            var normal = CreatePaperButton(
                "DebugScenarioNormal",
                panel,
                "일반 플레이로 초기화",
                new Vector2(0f, -405f),
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize,
                useHanji: true);
            normal.onClick.AddListener(
                () => DebugShowcaseScenarioProfile.Select(
                    DebugShowcaseScenarioId.Normal));

            var done = CreateBrushButton(
                "DebugScenarioDone",
                panel,
                "선택 완료",
                new Vector2(0f, -565f),
                new Vector2(390f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CardTitleSize);
            done.onClick.AddListener(ShowOptionsPage);
            RefreshDebugScenario();
        }

        void BuildTutorialPage(Transform panel)
        {
            tutorialPage = CreateReadableText(
                "Page", panel, $"1 / {GameplayTutorialCatalog.Count}", InkUiStyle.BodySize,
                new Vector2(250f, 640f), new Vector2(200f, 70f),
                InkPalette.TextDark,
                strong: true);

            var iconPaper = CreateImage(
                "TutorialIconPaper", panel,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 315f), new Vector2(430f, 430f),
                new Color(
                    InkPalette.Paper2.r,
                    InkPalette.Paper2.g,
                    InkPalette.Paper2.b,
                    0.94f));
            tutorialImage = CreateImage(
                "TutorialIcon", iconPaper.transform, null,
                Vector2.zero, new Vector2(300f, 300f), Color.white);
            tutorialImage.preserveAspect = true;

            tutorialTitle = CreateReadableText(
                "TutorialTitle", panel, string.Empty,
                InkUiStyle.InstructionTitleSize,
                new Vector2(0f, 70f), new Vector2(680f, 96f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            tutorialDescription = CreateReadableText(
                "TutorialDescription", panel, string.Empty,
                InkUiStyle.InstructionBodySize,
                new Vector2(0f, -200f), new Vector2(680f, 400f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter);
            tutorialDescription.lineSpacing = 1.12f;

            tutorialPreviousButton = CreateTutorialArrow("PreviousButton", panel, false);
            tutorialPreviousButton.onClick.AddListener(PreviousTutorialPage);
            tutorialNextButton = CreateTutorialArrow("NextButton", panel, true);
            tutorialNextButton.onClick.AddListener(NextTutorialPage);
            GameplayTutorialLayout.Apply(tutorialPage, tutorialImage, iconPaper.rectTransform,
                tutorialTitle, tutorialDescription, (RectTransform)tutorialPreviousButton.transform,
                (RectTransform)tutorialNextButton.transform, null);
        }

        static Button CreateTutorialArrow(string name, Transform parent, bool right)
        {
            var hit = CreateImage(name, parent, null, Vector2.zero,
                new Vector2(300, InkUiStyle.MinimumTapHeight), Color.clear);
            hit.raycastTarget = true;
            var icon = CreateImage("Icon", hit.transform, InkUiTextureFactory.CreateGrowthBackIconSprite(),
                Vector2.zero, new Vector2(76, 76), InkPalette.TextDark);
            icon.preserveAspect = true;
            icon.rectTransform.localScale = new Vector3(right ? -1 : 1, 1, 1);
            var button = hit.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, icon);
            icon.raycastTarget = false;
            var colors = button.colors;
            colors.disabledColor = new Color(1, 1, 1, .25f);
            button.colors = colors;
            return button;
        }

        void CreateAudioCard(
            Transform parent,
            string objectName,
            string label,
            Vector2 position,
            out Slider slider,
            out Text valueText,
            out Button toggle,
            out Text toggleLabel)
        {
            var root = CreateRect(
                objectName,
                parent,
                position,
                new Vector2(700f, 132f));
            var paper = CreateHanjiPaper("Paper", root, new Vector2(700f, 132f));
            CreateReadableText(
                "Label", paper.transform, label, 44,
                new Vector2(-275f, 0f), new Vector2(120f, 72f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);
            valueText = CreateReadableText(
                "Value", paper.transform, "100%", 40,
                new Vector2(180f, 0f), new Vector2(90f, 58f),
                InkPalette.TextDark, TextAnchor.MiddleRight, strong: true);

            slider = CreateInkSlider(
                "Slider",
                paper.transform,
                new Vector2(-45f, 0f),
                new Vector2(300f, InkUiStyle.MinimumTapHeight));
            toggle = CreatePaperButton(
                "Toggle",
                paper.transform,
                "켜짐",
                new Vector2(285f, 0f),
                new Vector2(120f, InkUiStyle.MinimumTapHeight),
                40);
            toggleLabel = toggle.transform
                .Find("Paper/Label")?.GetComponent<Text>();
        }

        void HandleBgmChanged(float value)
        {
            if (suppressSliderCallbacks) return;
            LobbySettingsProfile.SetBgmVolume(value);
            RefreshAudioLabels();
        }

        void HandleSfxChanged(float value)
        {
            if (suppressSliderCallbacks) return;
            LobbySettingsProfile.SetSfxVolume(value);
            RefreshAudioLabels();
        }

        void ToggleBgm()
        {
            float next = LobbySettingsProfile.BgmVolume > 0.01f
                ? 0f
                : LobbySettingsProfile.BgmResumeVolume;
            LobbySettingsProfile.SetBgmVolume(next);
            MukJumpAnalytics.Setting(AnalyticsSetting.Music, next > 0.01f ? 1 : 0);
            RefreshSettings();
        }

        void ToggleSfx()
        {
            float next = LobbySettingsProfile.SfxVolume > 0.01f
                ? 0f
                : LobbySettingsProfile.SfxResumeVolume;
            LobbySettingsProfile.SetSfxVolume(next);
            MukJumpAnalytics.Setting(AnalyticsSetting.Sound, next > 0.01f ? 1 : 0);
            RefreshSettings();
        }

        void RefreshLanguage()
        {
            RefreshSettings();
            RefreshLeaderboardPage();
            if (!string.IsNullOrEmpty(accountToastSource))
            {
                InkLocalizedText.SetSource(accountToastText, accountToastSource);
                ApplySafeArea();
            }
        }

        void BuildLanguagePage(Transform panel)
        {
            CreateReadableText("LanguageTitle", panel, "언어", 72,
                new Vector2(0, 250), new Vector2(680, 100), InkPalette.TextDark, strong: true);
            Button korean = CreatePaperButton("KoreanButton", panel, "한국어",
                new Vector2(0, 80), new Vector2(560, 140), 56);
            foreach (Text label in korean.GetComponentsInChildren<Text>(true))
            {
                InkLocalizedText.Exclude(label);
                label.text = "한국어";
            }
            korean.onClick.AddListener(() => SelectLanguage(GameLanguage.Korean));
            Button english = CreatePaperButton("EnglishButton", panel, "English",
                new Vector2(0, -100), new Vector2(560, 140), 56);
            foreach (Text label in english.GetComponentsInChildren<Text>(true))
            {
                InkLocalizedText.Exclude(label);
                label.text = "English";
            }
            english.onClick.AddListener(() => SelectLanguage(GameLanguage.English));
            Button japanese = CreatePaperButton("JapaneseButton", panel, "日本語",
                new Vector2(0, -280), new Vector2(560, 140), 56);
            japanese.onClick.AddListener(() => SelectLanguage(GameLanguage.Japanese));
        }

        void SelectLanguage(GameLanguage language)
        {
            if (scrollPageTransition || (Application.isPlaying &&
                (!rootGroup.interactable || !optionsPanel.GetComponent<HanjiScrollFrame>().IsReady))) return;
            GameLanguage previous = GameLocalization.Language;
            bool saved = GameLocalization.SetLanguage(language);
            if (saved && previous != language) MukJumpAnalytics.Setting(AnalyticsSetting.Language, (int)language);
            ShowOptionsPage();
            SetSettingsStatus(saved ? string.Empty : "언어를 저장하지 못했어요. 다시 시도해 주세요");
        }

        void ShowLanguagePage()
        {
            if (scrollPageTransition) return;
            if (!ShowingLanguagePage && IsOpen)
            {
                TransitionScrollPage(ShowLanguagePageImmediate);
                return;
            }
            ShowLanguagePageImmediate();
        }

        void ShowLanguagePageImmediate()
        {
            DisarmDeleteConfirmation();
            foreach (CanvasGroup page in new[] { optionsGroup, tutorialGroup, playSettingsGroup,
                         debugScenarioGroup, accountGroup, leaderboardGroup })
                SetPageVisible(page, false);
            SetPageVisible(languageGroup, true);
        }

        void RefreshSettings()
        {
            RefreshSettingsUuid();
            if (bgmSlider == null || sfxSlider == null) return;
            suppressSliderCallbacks = true;
            bgmSlider.value = LobbySettingsProfile.BgmVolume;
            sfxSlider.value = LobbySettingsProfile.SfxVolume;
            suppressSliderCallbacks = false;
            RefreshAudioLabels();
            RefreshQuickToggles();
            RefreshPlaySettings();
            RefreshDebugScenario();
        }

        void RefreshDebugScenario()
        {
            if (!GameManager.DebugToolsAvailable)
                return;

            DebugShowcaseScenarioDefinition selected =
                DebugShowcaseScenarioProfile.SelectedDefinition;
            string status = selected != null
                ? selected.Title.Replace(" · ", " ")
                : "일반 플레이";
            if (debugScenarioStatus != null)
                InkLocalizedText.SetSource(debugScenarioStatus, status);
            if (debugScenarioSummary != null)
            {
                InkLocalizedText.SetSource(debugScenarioSummary, selected != null
                    ? $"선택됨 · {selected.Summary}"
                    : "선택됨 · 저장된 성장으로 0m부터 시작");
            }

            IReadOnlyList<DebugShowcaseScenarioDefinition> definitions =
                DebugShowcaseScenarioProfile.Definitions;
            for (int i = 0; i < debugScenarioButtons.Length; i++)
            {
                Button button = debugScenarioButtons[i];
                if (button == null || i >= definitions.Count)
                    continue;
                bool isSelected = definitions[i].Id ==
                                  DebugShowcaseScenarioProfile.SelectedId;
                Image outline = button.GetComponent<Image>();
                Image paper = button.transform.Find("Paper")?.GetComponent<Image>();
                if (outline != null)
                    outline.color = Color.clear;
                if (paper != null)
                    paper.color = isSelected
                        ? Color.Lerp(Color.white, InkPalette.Red, 0.10f)
                        : Color.white;
            }
        }

        void RefreshQuickToggles()
        {
            SetQuickState(quickBgm, quickBgmStatus, LobbySettingsProfile.BgmVolume > 0.01f);
            SetQuickState(quickSfx, quickSfxStatus, LobbySettingsProfile.SfxVolume > 0.01f);
            SetQuickState(quickHaptics, quickHapticsStatus, LobbySettingsProfile.HapticsEnabled);
        }

        static void SetQuickState(Button button, Text label, bool enabled)
        {
            if (button == null || label == null) return;
            InkLocalizedText.SetSource(label, enabled ? "켜짐" : "꺼짐");
            label.color = enabled ? InkPalette.TextDark : InkPalette.TextMuted;
            var face = button.transform.Find("Paper").GetComponent<Image>();
            // 원화의 밝은 한지색을 보존하고, 꺼짐도 글씨를 읽고 다시 누를 수 있게 한다.
            face.color = enabled ? InkUiStyle.HanjiPaperColor : Color.Lerp(InkUiStyle.HanjiPaperColor, InkPalette.Paper2, 0.5f);
            var icon = button.transform.Find("Paper/Icon");
            foreach (var graphic in icon.GetComponentsInChildren<Image>())
                graphic.color = enabled ? Color.white : WithAlpha(Color.white, 0.65f);
        }

        void OpenTermsOfService()
        {
#if !UNITY_EDITOR
            Application.OpenURL(MukJumpLegalUrls.TermsOfService);
#endif
        }

        void RefreshAudioLabels()
        {
            RefreshQuickToggles();
            if (bgmValue == null || sfxValue == null) return;
            bgmValue.text =
                $"{Mathf.RoundToInt(LobbySettingsProfile.BgmVolume * 100f)}%";
            sfxValue.text =
                $"{Mathf.RoundToInt(LobbySettingsProfile.SfxVolume * 100f)}%";
            bgmToggleLabel.text =
                LobbySettingsProfile.BgmVolume > 0.01f ? "켜짐" : "꺼짐";
            sfxToggleLabel.text =
                LobbySettingsProfile.SfxVolume > 0.01f ? "켜짐" : "꺼짐";
        }

        void RefreshPlaySettings()
        {
            if (hapticsStatus != null)
                InkLocalizedText.SetSource(hapticsStatus, LobbySettingsProfile.HapticsEnabled
                    ? "켜짐"
                    : "꺼짐");
        }

        public static string CustomerSupportMailUri =>
            $"mailto:{CustomerSupportEmail}?subject=" + System.Uri.EscapeDataString(GameLocalization.Translate("먹점프 고객 문의"));

        void ShowCustomerCenterGuide()
        {
            // 메일 작성 화면만 연다. 전송·계정 정보 첨부는 사용자가 직접 결정한다.
            SetSettingsStatus(CustomerSupportEmail);
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenTossCustomerSupportMail();
#elif !UNITY_EDITOR
            Application.OpenURL(CustomerSupportMailUri);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async void OpenTossCustomerSupportMail()
        {
            if (supportMailOpening) return;
            supportMailOpening = true;
            try
            {
                // Unity의 새 브라우저 창 대신 토스 네이티브 URL 브리지를 쓴다.
                await AppsInToss.AIT.OpenURL(CustomerSupportMailUri, 10000);
            }
            catch (System.Exception)
            {
                if (this != null && connectionStatus != null)
                    SetSettingsStatus($"메일 연결 실패 · {CustomerSupportEmail}");
            }
            finally { supportMailOpening = false; }
        }
#endif

        void OpenPrivacyPolicy()
        {
            ShowAccountNotice("개인정보처리방침과 계정 삭제 안내를 엽니다");
#if !UNITY_EDITOR
            Application.OpenURL(MukJumpLegalUrls.PrivacyPolicy);
#endif
        }

        void ShowOptionsPage()
        {
            if (scrollPageTransition) return;
            if (showingAccountPage && MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) return;
            if ((showingTutorialPage || showingAccountPage || ShowingLanguagePage || ShowingLeaderboardPage || ShowingAnalyticsPrivacy) && IsOpen)
            {
                TransitionScrollPage(ShowOptionsPageImmediate);
                return;
            }
            ShowOptionsPageImmediate();
        }

        void ShowOptionsPageImmediate()
        {
            DisarmDeleteConfirmation();
            SetPageVisible(optionsGroup, true);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            RefreshDebugScenario();
        }

        void ShowPlaySettingsPage()
        {
            DisarmDeleteConfirmation();
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, true);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            RefreshPlaySettings();
        }

        void ShowDebugScenarioPage()
        {
            if (!GameManager.DebugToolsAvailable || debugScenarioGroup == null)
                return;
            DisarmDeleteConfirmation();
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, true);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            RefreshDebugScenario();
        }

        void ShowTutorialPage(int page)
        {
            if (scrollPageTransition) return;
            if (!showingTutorialPage && IsOpen)
            {
                TransitionScrollPage(() => ShowTutorialPageImmediate(0));
                return;
            }
            ShowTutorialPageImmediate(page);
        }

        void ShowTutorialPageImmediate(int page)
        {
            DisarmDeleteConfirmation();
            currentTutorialPage = Mathf.Clamp(
                page,
                0,
                GameplayTutorialCatalog.Count - 1);
            if (IsOpen) MukJumpAnalytics.TutorialStep(currentTutorialPage, review: true);

            GameplayTutorialPage pageData =
                GameplayTutorialCatalog.Get(currentTutorialPage);
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, true);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            InkLocalizedText.SetSource(tutorialTitle, pageData.Title);
            InkLocalizedText.SetSource(tutorialDescription, pageData.Description);
            tutorialImage.sprite = Resources.Load<Sprite>(
                pageData.SpriteResourcePath);
            tutorialImage.color = tutorialImage.sprite != null
                ? Color.white
                : InkPalette.Ink;
            tutorialPage.text =
                $"{currentTutorialPage + 1} / {GameplayTutorialCatalog.Count}";
            tutorialPreviousButton.interactable =
                currentTutorialPage > 0;
            tutorialNextButton.interactable = currentTutorialPage < GameplayTutorialCatalog.Count - 1;
        }

        bool CanNavigateTutorial => IsTutorialOpen && rootGroup.interactable && !scrollPageTransition
            && (!Application.isPlaying || optionsPanel.GetComponent<HanjiScrollFrame>().IsReady);

        void PreviousTutorialPage()
        {
            if (!CanNavigateTutorial || currentTutorialPage <= 0) return;
            ShowTutorialPage(currentTutorialPage - 1);
        }

        void NextTutorialPage()
        {
            if (!CanNavigateTutorial) return;
            if (currentTutorialPage < GameplayTutorialCatalog.Count - 1)
            {
                ShowTutorialPage(currentTutorialPage + 1);
                return;
            }
        }

        void TransitionScrollPage(System.Action showPage)
        {
            var frame = optionsPanel.GetComponent<HanjiScrollFrame>();
            if (scrollPageTransition || frame.IsClosing) return;
            if (Application.isPlaying && (!rootGroup.interactable || !frame.IsReady)) return;
            scrollPageTransition = true;
            rootGroup.interactable = false;
            ClearAccountToast();
            AppleSignInButtonBridge.Hide();
            // 상단 축과 dim은 유지하고, 닫힌 순간에만 내용을 교체한 뒤 다시 펼친다.
            frame.Close(() =>
            {
                if (this == null || !isActiveAndEnabled || !IsOpen) return;
                scrollPageTransition = false;
                // 접히는 동안 기록 선택/복구가 시작되면 계정 화면을 유지한다.
                if (!showingAccountPage || MukJumpAccountRuntime.Instance == null ||
                    !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                    showPage();
                rootGroup.interactable = true;
                frame.CancelClose();
            });
        }

        void CancelScrollPageTransition()
        {
            if (!scrollPageTransition) return;
            scrollPageTransition = false;
            if (optionsPanel != null) optionsPanel.GetComponent<HanjiScrollFrame>().CancelClose();
            if (rootGroup != null) rootGroup.interactable = IsOpen;
        }

        void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            bool wasVisible = IsOpen;
            var frame = optionsPanel.GetComponent<HanjiScrollFrame>();
            if (visible) frame.CancelClose();
            else frame.ResetPresentation();
            if (!visible)
            {
                scrollPageTransition = false;
                ClearAccountToast();
                lastAccountToastStatus = null;
                DisarmDeleteConfirmation();
                AppleSignInButtonBridge.Hide();
            }
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
            if (visible) TrackAnalyticsScreen();
            else if (wasVisible) MukJumpAnalytics.Screen(AnalyticsScreen.Lobby);
            ApplySafeArea();
        }

        void CloseImmediate()
        {
            SetVisible(false);
            ShowOptionsPageImmediate();
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || optionsPanel == null ||
                UiScreenWidth <= 0 ||
                UiScreenHeight <= 0)
                return;
            Rect safe = UiSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                UiScreenWidth,
                UiScreenHeight);
            lastScreenWidth = UiScreenWidth;
            lastScreenHeight = UiScreenHeight;
            lastSafeArea = safe;

            // 계정 종이는 안전영역 정중앙에 둔다. 외부 닫기 버튼까지 들어갈 때만
            // 기존 배율을 유지하고, 낮은 화면에서는 중심을 옮기지 않고 축소한다.
            if (showingAccountPage)
            {
                float halfHeight = optionsPanel.sizeDelta.y * .5f;
                // 토스트 유무에 따라 팝업이 흔들리지 않도록 위쪽 공간은 항상 확보한다.
                float toastHeight = LayoutAccountToastContents();
                halfHeight += 24f + 224f;
                if (accountCloseButton != null)
                {
                    var close = (RectTransform)accountCloseButton.transform;
                    halfHeight = Mathf.Max(halfHeight, -close.anchoredPosition.y - close.rect.yMin);
                }
                float centeredScale = MobileUiLayout.CalculateFitScale(
                    new Vector2(PanelWidth, halfHeight * 2f), safe, UiScreenWidth, UiScreenHeight,
                    Vector2.one * (SafeAreaPadding * .5f));
                float scale = Mathf.Min(CalculateSettingsTutorialScale(safe, UiScreenWidth, UiScreenHeight), centeredScale);
                optionsPanel.anchoredPosition = Vector2.zero;
                optionsPanel.localScale = Vector3.one * scale;
                if (accountToast != null)
                {
                    var toastRect = (RectTransform)accountToast.transform;
                    toastRect.localScale = Vector3.one * scale;
                    toastRect.anchoredPosition = new Vector2(0,
                        (optionsPanel.sizeDelta.y * .5f + 24f + toastHeight * .5f) * scale);
                }
                return;
            }
            // 짧아진 종이에 예전 상단 축을 적용하면 설정 전체가 위로 치우친다.
            // 본체는 다른 팝업과 같은 중앙에 두고 외부 닫기 영역까지 대칭으로 확보한다.
            if (showingSettingsPage || showingTutorialPage)
            {
                float scale = CalculateSettingsTutorialScale(safe, UiScreenWidth, UiScreenHeight);
                float halfHeight = optionsPanel.sizeDelta.y * .5f;
                if (showingSettingsPage && optionsGroup.transform.Find("CloseButton") is RectTransform close)
                    halfHeight = Mathf.Max(halfHeight, -close.anchoredPosition.y - close.rect.yMin);
                scale = Mathf.Min(scale, MobileUiLayout.CalculateFitScale(
                    new Vector2(PanelWidth, halfHeight * 2f), safe, UiScreenWidth, UiScreenHeight,
                    Vector2.one * (SafeAreaPadding * .5f)));
                optionsPanel.anchoredPosition = CalculateSettingsTutorialPosition(showingTutorialPage, scale);
                optionsPanel.localScale = Vector3.one * scale;
                return;
            }
            float panelScale = MobileUiLayout.CalculateFitScale(
                optionsPanel.sizeDelta,
                safe,
                UiScreenWidth,
                UiScreenHeight,
                Vector2.one * (SafeAreaPadding * 0.5f));
            if (optionsPanel != null)
            {
                optionsPanel.anchoredPosition = Vector2.zero;
                optionsPanel.localScale = Vector3.one * panelScale;
            }
        }

        public static float CalculateSettingsTutorialScale(Rect safeArea, int width, int height) =>
            MobileUiLayout.CalculateFitScale(new Vector2(PanelWidth, SettingsDesignHeight + CloseFooterHeight),
                safeArea, width, height, Vector2.one * (SafeAreaPadding * .5f));

        public static Vector2 CalculateSettingsTutorialPosition(bool tutorial, float scale) => Vector2.zero;

        void SetSettingsStatus(string source)
        {
            InkLocalizedText.SetSource(connectionStatus, source);
            LayoutSettingsContents();
            if (showingSettingsPage) ApplySafeArea();
        }

        void LayoutSettingsContents()
        {
            if (optionsGroup == null) return;
            bool hasStatus = connectionStatus != null && !string.IsNullOrWhiteSpace(connectionStatus.text);
            float height = UsesTossSettings ? SettingsDesignHeight :
                SettingsPanelHeight + (hasStatus ? SettingsStatusHeight : 0f);
            // 내용의 종이 상단 여백·크기는 유지한다. 종이 본체의 화면 중심은 별도로 고정한다.
            float offset = (height - settingsLayoutHeight) * .5f;
            foreach (RectTransform child in optionsGroup.transform)
                child.anchoredPosition += Vector2.up * offset;
            settingsLayoutHeight = height;
            float footerLift = (SettingsDesignHeight - height) * .5f;
            foreach (string name in new[] { "TermsButton", "PrivacyButton", "CloseButton" })
            {
                var rect = optionsGroup.transform.Find(name) as RectTransform;
                if (rect != null)
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                        (name == "CloseButton" ? -820f : -634f) + footerLift);
            }
            if (connectionStatus != null)
            {
                connectionStatus.gameObject.SetActive(hasStatus);
                connectionStatus.rectTransform.anchoredPosition = new Vector2(0,
                    UsesTossSettings ? -527f : -381f - footerLift);
            }
            if (!showingSettingsPage) return;
            optionsPanel.sizeDelta = new Vector2(PanelWidth, height);
            optionsPanel.GetComponent<HanjiScrollFrame>().SetPaperSize(new Vector2(764, height - 60f));
        }

        static CanvasGroup CreatePageGroup(string name, Transform parent)
        {
            var root = CreateStretchRect(name, parent);
            return root.gameObject.AddComponent<CanvasGroup>();
        }

        void SetPageVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            if (visible)
            {
                // 강제 페이지 변경은 대기 중인 이전 복귀 동작을 취소한다.
                if (scrollPageTransition) CancelScrollPageTransition();
                bool choosingLanguage = ReferenceEquals(group, languageGroup);
                bool privacy = ReferenceEquals(group, analyticsPrivacyGroup);
                if (!privacy) SetPageVisible(analyticsPrivacyGroup, false);
                if (!choosingLanguage) SetPageVisible(languageGroup, false);
                showingSettingsPage = ReferenceEquals(group, optionsGroup);
                showingTutorialPage = ReferenceEquals(group, tutorialGroup);
                showingAccountPage = ReferenceEquals(group, accountGroup) && !UsesTossSettings;
                bool tutorial = ReferenceEquals(group, tutorialGroup);
                optionsPanel.sizeDelta = new Vector2(PanelWidth,
                    choosingLanguage || privacy ? 900 : tutorial ? FirstRunTutorialController.PanelDesignHeight : showingSettingsPage ? SettingsPanelHeight : PanelHeight);
                optionsPanel.GetComponent<HanjiScrollFrame>().SetPaperSize(
                    new Vector2(764, choosingLanguage || privacy ? 840 : tutorial ? 1300 : showingSettingsPage ? SettingsPanelHeight - 60f : 1450));
                if (showingSettingsPage) LayoutSettingsContents();
                if (showingAccountPage) ApplyAccountPaperSize();
                ApplySafeArea();
            }
            if (!visible && ReferenceEquals(group, accountGroup))
            {
                ClearAccountToast();
                lastAccountToastStatus = null;
                AppleSignInButtonBridge.Hide();
            }
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible && IsOpen) TrackAnalyticsScreen();
        }

        void TrackAnalyticsScreen()
        {
            MukJumpAnalytics.Screen(ShowingAnalyticsPrivacy ? AnalyticsScreen.Privacy :
                ShowingLanguagePage ? AnalyticsScreen.Language : showingTutorialPage ? AnalyticsScreen.Tutorial :
                showingAccountPage ? AnalyticsScreen.Account : ShowingLeaderboardPage ? AnalyticsScreen.Leaderboard : AnalyticsScreen.Settings);
        }

        void BuildAnalyticsPrivacyPage(Transform parent)
        {
            CreateReadableText("Title", parent, "플레이 분석", 64, new Vector2(0, 315),
                new Vector2(680, 96), InkPalette.TextDark, strong: true);
            CreateReadableText("Description", parent,
                "설치 식별자·기기 정보와 플레이 이벤트를 Google Analytics로 전송해 게임을 개선합니다.\n선택 사항이며 언제든 끌 수 있어요. 계정 UUID·닉네임은 보내지 않습니다.",
                40, new Vector2(0, 110), new Vector2(660, 260), InkPalette.TextDark);
            analyticsPrivacyStatus = CreateReadableText("Status", parent, string.Empty, 40,
                new Vector2(0, -68), new Vector2(680, 60), InkPalette.TextMuted);
            CreatePaperButton("EnableAnalytics", parent, "동의하고 켜기", new Vector2(-166, -170), new Vector2(306, 120), 44)
                .onClick.AddListener(() => SetAnalyticsConsent(true));
            CreatePaperButton("DisableAnalytics", parent, "분석 끄기", new Vector2(166, -170), new Vector2(306, 120), 44)
                .onClick.AddListener(() => SetAnalyticsConsent(false));
            CreatePaperButton("ReadPrivacyPolicy", parent, "개인정보처리방침", new Vector2(0, -310), new Vector2(640, 100), 40)
                .onClick.AddListener(OpenPrivacyPolicy);
        }

        void ShowAnalyticsPrivacyPage()
        {
            if (UsesTossSettings || scrollPageTransition || ShowingAnalyticsPrivacy) return;
            TransitionScrollPage(() =>
            {
                foreach (var group in new[] { optionsGroup, tutorialGroup, accountGroup, playSettingsGroup, leaderboardGroup, languageGroup })
                    SetPageVisible(group, false);
                SetPageVisible(analyticsPrivacyGroup, true);
                RefreshAnalyticsPrivacy();
            });
        }

        void SetAnalyticsConsent(bool enabled)
        {
            if (!ShowingAnalyticsPrivacy || scrollPageTransition || !rootGroup.interactable ||
                Application.isPlaying && !optionsPanel.GetComponent<HanjiScrollFrame>().IsReady) return;
            bool saved = MukJumpAnalyticsPrivacy.TrySetConsent(enabled);
            RefreshAnalyticsPrivacy();
            if (!saved) InkLocalizedText.SetSource(analyticsPrivacyStatus, "저장하지 못했어요. 다시 시도해 주세요");
        }

        void RefreshAnalyticsPrivacy()
        {
            if (analyticsPrivacyStatus != null)
                InkLocalizedText.SetSource(analyticsPrivacyStatus, MukJumpAnalyticsPrivacy.HasConsent ? "분석 켜짐" : "분석 꺼짐");
        }

        static void BuildScrollFrame(Transform panel)
        {
            HanjiScrollFrame.Attach((RectTransform)panel, new Vector2(764, 1450));
        }

        static Slider CreateInkSlider(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var root = CreateRect(objectName, parent, position, size);
            var hitArea = CreateImage(
                "HitArea", root, null, Vector2.zero, size,
                Color.clear);
            hitArea.raycastTarget = true;
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var track = CreateImage(
                "Track", root, brush, Vector2.zero,
                new Vector2(size.x - 36f, 18f),
                new Color(
                    InkPalette.Ink.r,
                    InkPalette.Ink.g,
                    InkPalette.Ink.b,
                    0.22f));
            var fillArea = CreateRect(
                "FillArea", root, Vector2.zero,
                new Vector2(size.x - 36f, 18f));
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(18f, -9f);
            fillArea.offsetMax = new Vector2(-18f, 9f);
            var fill = CreateImage(
                "Fill", fillArea, brush, Vector2.zero,
                fillArea.sizeDelta, InkPalette.Ink);
            // 값에 맞춰 붓결을 자른다. 채움 폭으로 원화를 압축하지 않는다.
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = CreateStretchRect("HandleArea", root);
            handleArea.offsetMin = new Vector2(18f, (size.y - 48f) * 0.5f);
            handleArea.offsetMax = new Vector2(-18f, -(size.y - 48f) * 0.5f);
            var handle = CreateImage(
                "Handle", handleArea,
                InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, new Vector2(48f, 48f), InkPalette.Ink);
            handle.raycastTarget = true;
            CreateImage("PaperCenter", handle.transform,
                InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, new Vector2(24f, 24f), InkPalette.TextLight);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            // Slider가 세로 anchor를 0..1로 바꾸므로 높이 오프셋을 더하지 않는다.
            handle.rectTransform.sizeDelta = new Vector2(48f, 0f);
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            root.gameObject.AddComponent<InkUiPressFeedback>();
            return slider;
        }

        static Button CreateUtilityButton(
            string objectName,
            Transform parent,
            string title,
            string status,
            Vector2 position)
        {
            var button = CreatePaperButton(
                objectName,
                parent,
                string.Empty,
                position,
                new Vector2(350f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            Transform paper = button.transform.Find("Paper");
            if (paper == null) return button;
            CreateReadableText(
                "Title", paper, title, InkUiStyle.BodySize,
                new Vector2(-74f, 0f), new Vector2(180f, 72f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);
            CreateReadableText(
                "Status", paper, status, InkUiStyle.CaptionSize,
                new Vector2(98f, 0f), new Vector2(130f, 96f),
                InkPalette.TextDark, TextAnchor.MiddleRight);
            return button;
        }

        static Button CreateWideUtilityButton(
            string objectName,
            Transform parent,
            string title,
            string status,
            Vector2 position,
            out Text statusText)
        {
            var button = CreatePaperButton(
                objectName,
                parent,
                string.Empty,
                position,
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                44);
            Transform paper = button.transform.Find("Paper");
            CreateReadableText(
                "Title",
                paper,
                title,
                44,
                new Vector2(-155f, 0f),
                new Vector2(350f, 74f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft,
                strong: true);
            statusText = CreateReadableText(
                "Status",
                paper,
                status,
                40,
                new Vector2(210f, 0f),
                new Vector2(240f, 74f),
                InkPalette.TextDark,
                TextAnchor.MiddleRight,
                strong: true);
            return button;
        }

        static Button CreateAppleSignInPlaceholder(
            Transform parent,
            Vector2 position)
        {
            var button = CreatePaperButton(
                "AppleLoginButton",
                parent,
                "Apple로 계속하기",
                position,
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize,
                useHanji: false);
            Image outline = button.GetComponent<Image>();
            Image paper = button.transform.Find("Paper")?
                .GetComponent<Image>();
            Text label = button.transform.Find("Paper/Label")?
                .GetComponent<Text>();
            // 공식 Apple 버튼 자리표시자에는 게임 한지 스킨을 적용하지 않는다.
            if (paper != null)
            {
                paper.sprite = null;
                paper.type = Image.Type.Simple;
            }
#if UNITY_IOS && !UNITY_EDITOR
            // 실제 iOS에서는 시스템 ASAuthorizationAppleIDButton이 이
            // Rect를 덮는다. 자리표시자의 사각 배경이 공식 둥근 모서리
            // 밖으로 비치지 않도록 입력 영역만 투명하게 유지한다.
            if (outline != null)
                outline.color = Color.clear;
            if (paper != null)
                paper.color = Color.clear;
            if (label != null)
                label.color = Color.clear;
            button.transition = Selectable.Transition.None;
#else
            if (outline != null)
                outline.color = Color.black;
            if (paper != null)
                paper.color = Color.black;
            if (label != null)
            {
                label.color = Color.white;
                label.fontSize = Mathf.RoundToInt(
                    InkUiStyle.MinimumTapHeight * 0.43f);
            }
#endif
            return button;
        }

        static Button CreateDebugScenarioCard(
            Transform parent,
            DebugShowcaseScenarioDefinition definition,
            Vector2 position)
        {
            var button = CreatePaperButton(
                $"DebugScenario{(int)definition.Id}",
                parent,
                string.Empty,
                position,
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            Transform paper = button.transform.Find("Paper");
            CreateReadableText(
                "Title",
                paper,
                definition.Title,
                InkUiStyle.BodySize,
                new Vector2(0f, 23f),
                new Vector2(650f, 46f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true,
                wrap: false);
            CreateReadableText(
                "Summary",
                paper,
                definition.Summary,
                InkUiStyle.CaptionSize,
                new Vector2(0f, -25f),
                new Vector2(650f, 38f),
                InkPalette.TextMuted,
                TextAnchor.MiddleCenter,
                wrap: false);
            return button;
        }

        static Button CreatePaperButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize,
            bool useHanji = false)
        {
            var outline = CreateImage(
                objectName, parent, null, position, size, Color.clear);
            outline.raycastTarget = true;
            var paper = CreateHanjiPaper("Paper", outline.transform, size);
            var button = outline.gameObject.AddComponent<Button>();
            Text labelText = CreateReadableText(
                "Label", paper.transform, label,
                Mathf.Max(
                    fontSize,
                    useHanji
                        ? InkUiStyle.ActionButtonLabelSize
                        : InkUiStyle.StandardButtonLabelSize),
                Vector2.zero,
                size - (useHanji
                    ? new Vector2(64f, 32f)
                    : new Vector2(28f, 16f)),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            // 비활성 상태는 얇은 테두리만이 아니라 한지 면 전체가 흐려져야
            // 첫 튜토리얼의 사용할 수 없는 `이전` 버튼도 즉시 구분된다.
            if (useHanji)
            {
                bool needsTwoLines = size.x <= 360f && label.Length > 7;
                InkUiStyle.ConfigureActionButton(
                    button,
                    outline,
                    labelText,
                    ActionButtonRole.Secondary,
                    needsTwoLines
                        ? ActionButtonLayout.TwoLine
                        : ActionButtonLayout.SingleLine,
                    obsoleteSurface: paper);
            }
            else
            {
                InkUiStyle.ConfigureButton(button, paper);
                button.colors = InkUiStyle.ActionButtonColors();
                paper.raycastTarget = false;
            }
            return button;
        }

        static Image CreateHanjiPaper(string objectName, Transform parent, Vector2 size)
        {
            var paper = CreateImage(objectName, parent, InkUiStyle.ActionButtonSprite,
                Vector2.zero, size, Color.white);
            InkUiStyle.ConfigureHanjiSurface(paper);
            return paper;
        }

        static Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize,
            ActionButtonRole role = ActionButtonRole.Primary)
        {
            var brush = CreateImage(
                objectName, parent, null,
                position, size, InkPalette.Ink);
            var button = brush.gameObject.AddComponent<Button>();
            var labelText = CreateReadableText(
                "Label", brush.transform, label, fontSize,
                Vector2.zero, size - new Vector2(36f, 14f),
                InkPalette.TextLight,
                TextAnchor.MiddleCenter,
                strong: true,
                outline: true,
                wrap: false);
            InkUiStyle.ConfigureActionButton(
                button,
                brush,
                labelText,
                role);
            labelText.fontSize = Mathf.Max(40, fontSize);
            return button;
        }

        static Text CreateReadableText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            bool strong = false,
            bool outline = false,
            bool wrap = true)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            InkLocalizedText.SetSource(text, value);
            text.color = color;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                alignment,
                strong,
                wrap);
            var textOutline = text.GetComponent<Outline>();
            if (textOutline != null)
                textOutline.enabled = outline;
            return text;
        }

        static void CreateDivider(
            Transform parent,
            string objectName,
            float y,
            float width)
        {
            CreateImage(
                objectName,
                parent,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, y),
                new Vector2(width, 4f),
                WithAlpha(InkPalette.Ink, 0.18f));
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(
            string objectName,
            Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(
            string objectName,
            Transform parent,
            Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
