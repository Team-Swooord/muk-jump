using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비에서 꼭 필요한 소리 설정, 고객센터와 튜토리얼만 제공한다.
    [DisallowMultipleComponent]
    public sealed class LobbyOptionsView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4150;
        const float PanelWidth = 820f;
        const float PanelHeight = 1510f;
        const float SafeAreaPadding = 24f;
        const string CustomerSupportEmail = "cysbandcs@gmail.com";
        const string PrivacyPolicyUrl =
            "https://github.com/Team-Swooord/muk-jump/blob/main/" +
            "docs/legal/privacy-policy.md";

        CanvasGroup rootGroup;
        CanvasGroup optionsGroup;
        CanvasGroup tutorialGroup;
        CanvasGroup playSettingsGroup;
        CanvasGroup debugScenarioGroup;
        CanvasGroup accountGroup;
        CanvasGroup leaderboardGroup;
        RectTransform safeAreaRoot;
        RectTransform optionsPanel;
        Slider bgmSlider;
        Slider sfxSlider;
        Text bgmValue;
        Text sfxValue;
        Text bgmToggleLabel;
        Text sfxToggleLabel;
        Text connectionStatus;
        Text adPrivacyStatus;
        Text adConsentStatus;
        Text hapticsStatus;
        Text reducedMotionStatus;
        Text debugScenarioStatus;
        Text debugScenarioSummary;
        Text accountKindText;
        Text accountStatusText;
        Text accountSyncPendingTitleText;
        Text accountSyncPendingStatusText;
        Text accountSyncPendingCaptionText;
        Text accountSyncPendingFootnoteText;
        Text accountDeleteLabel;
        Text leaderboardStatusText;
        Button accountGoogleButton;
        Button accountAppleButton;
        Button accountLogoutButton;
        Button accountDeleteButton;
        Button accountSyncRetryButton;
        Button accountSyncReturnButton;
        RectTransform accountConflictRoot;
        RectTransform syncConflictRoot;
        RectTransform accountSyncPendingRoot;
        readonly Text[] leaderboardRows = new Text[10];
        Image tutorialImage;
        Text tutorialTitle;
        Text tutorialDescription;
        Text tutorialPage;
        Text tutorialNextLabel;
        Button tutorialPreviousButton;
        readonly Button[] debugScenarioButtons = new Button[5];
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        int currentTutorialPage;
        bool suppressSliderCallbacks;
        bool deleteConfirmationArmed;
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

        void Awake()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        void OnEnable()
        {
            BuildIfNeeded();
            BindManager();
            LobbySettingsProfile.Changed += RefreshSettings;
            DebugShowcaseScenarioProfile.Changed += RefreshDebugScenario;
            BindAccountRuntime();
        }

        void OnDisable()
        {
            LobbySettingsProfile.Changed -= RefreshSettings;
            DebugShowcaseScenarioProfile.Changed -= RefreshDebugScenario;
            UnbindAccountRuntime();
            LobbySettingsProfile.Flush();
            UnbindManager();
            CloseImmediate();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                LobbySettingsProfile.Flush();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                LobbySettingsProfile.Flush();
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
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                MobileUiLayout.CurrentSafeArea != lastSafeArea)
                ApplySafeArea();
            RefreshAdPrivacyStatus();
        }

        public void Open()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }
            RefreshSettings();
            if (MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                ShowAccountPage();
            else
                ShowOptionsPage();
            SetVisible(true);
        }

        public void Close()
        {
            if (MukJumpAccountRuntime.Instance != null &&
                MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync)
                return;
            LobbySettingsProfile.Flush();
            SetVisible(false);
        }

        public void OpenAccountForRequiredSync()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
                return;
            ShowAccountPage();
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
            ShowTutorialPage(0);
            SetVisible(true);
        }

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
            BuildAccountPage(accountGroup.transform);
            leaderboardGroup = CreatePageGroup(
                "LeaderboardPage",
                optionsPanel);
            BuildLeaderboardPage(leaderboardGroup.transform);
            if (GameManager.DebugToolsAvailable)
            {
                debugScenarioGroup = CreatePageGroup(
                    "DebugScenarioPage",
                    optionsPanel);
                BuildDebugScenarioPage(debugScenarioGroup.transform);
            }

            ApplySafeArea();
            RefreshSettings();
        }

        void BuildOptionsPage(Transform panel)
        {
            CreateReadableText(
                "Title", panel, "설정", InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 660f), new Vector2(520f, 80f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "Version", panel, $"v{Application.version}",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 590f), new Vector2(300f, 46f),
                InkPalette.TextMuted,
                TextAnchor.MiddleCenter);
            CreateDivider(panel, "HeaderDivider", 548f, 700f);

            CreateReadableText(
                "AudioCaption", panel, "소리",
                InkUiStyle.BodySize,
                new Vector2(-290f, 475f), new Vector2(120f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);

            CreateAudioCard(
                panel,
                "BgmCard",
                "배경음",
                new Vector2(0f, 375f),
                out bgmSlider,
                out bgmValue,
                out Button bgmToggle,
                out bgmToggleLabel);
            bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
            bgmToggle.onClick.AddListener(ToggleBgm);

            CreateAudioCard(
                panel,
                "SfxCard",
                "효과음",
                new Vector2(0f, 225f),
                out sfxSlider,
                out sfxValue,
                out Button sfxToggle,
                out sfxToggleLabel);
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            sfxToggle.onClick.AddListener(ToggleSfx);

            CreateReadableText(
                "HelpCaption", panel, "도움과 정보",
                InkUiStyle.BodySize,
                new Vector2(-250f, 115f), new Vector2(200f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);

            var support = CreateWideUtilityButton(
                "CustomerCenterButton", panel, "고객센터", "이메일 문의",
                new Vector2(0f, 15f), out _);
            support.onClick.AddListener(ShowCustomerCenterGuide);
            var guide = CreateWideUtilityButton(
                "GuideButton", panel, "튜토리얼", "다시 보기",
                new Vector2(0f, -115f), out _);
            guide.onClick.AddListener(() => ShowTutorialPage(0));

            bool showDebugScenario = GameManager.DebugToolsAvailable;
            if (showDebugScenario)
            {
                var debugScenario = CreateWideUtilityButton(
                    "DebugScenarioButton",
                    panel,
                    "DEBUG · 연출 시나리오",
                    "일반 플레이",
                    new Vector2(0f, -245f),
                    out debugScenarioStatus);
                debugScenario.onClick.AddListener(ShowDebugScenarioPage);
            }

            float accountY = showDebugScenario ? -375f : -245f;
            float adPrivacyY = showDebugScenario ? -505f : -375f;
            var account = CreateWideUtilityButton(
                "AccountButton",
                panel,
#if UNITY_WEBGL && !UNITY_EDITOR
                "최고 고도",
                "토스 게임센터",
#else
                "계정",
                "연결·동기화",
#endif
                new Vector2(0f, accountY),
                out _);
#if UNITY_WEBGL && !UNITY_EDITOR
            account.onClick.AddListener(
                AppsInTossGameCenterRuntime.OpenLeaderboard);
#else
            account.onClick.AddListener(ShowAccountPage);
#endif

            var adPrivacy = CreateWideUtilityButton(
                "AdPrivacyButton",
                panel,
                "플레이·광고",
                "햅틱·움직임·동의",
                new Vector2(0f, adPrivacyY),
                out adPrivacyStatus);
            adPrivacy.onClick.AddListener(ShowPlaySettingsPage);

            connectionStatus = CreateReadableText(
                "ConnectionStatus", panel,
                "설정은 이 기기에 저장됩니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, showDebugScenario ? -585f : -455f),
                new Vector2(700f, 56f),
                InkPalette.TextMuted);

            var close = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, showDebugScenario ? -665f : -535f),
                new Vector2(390f, 120f),
                InkUiStyle.CardTitleSize);
            close.onClick.AddListener(Close);

            CreateReadableText(
                "PrivacyCaption", panel,
                "게임 기록·설정은 기기에 저장되며 광고 선택은 언제든 바꿀 수 있습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, showDebugScenario ? -735f : -635f),
                new Vector2(700f, 44f),
                InkPalette.TextMuted);
            RefreshAdPrivacyStatus();
        }

        void BuildPlaySettingsPage(Transform panel)
        {
            var back = CreateBrushButton(
                "PlaySettingsBack", panel, "옵션으로",
                new Vector2(-250f, 640f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            back.onClick.AddListener(ShowOptionsPage);

            CreateReadableText(
                "PlaySettingsTitle", panel, "플레이·광고",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 535f), new Vector2(560f, 82f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "PlaySettingsCaption", panel,
                "기기별 플레이 감각과 광고 개인정보 선택을 관리합니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 455f), new Vector2(680f, 64f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            CreateDivider(panel, "PlaySettingsDivider", 405f, 680f);

            var haptics = CreateWideUtilityButton(
                "HapticsButton", panel,
                "햅틱", "켜짐",
                new Vector2(0f, 300f), out hapticsStatus);
            haptics.onClick.AddListener(() =>
            {
                LobbySettingsProfile.SetHapticsEnabled(
                    !LobbySettingsProfile.HapticsEnabled);
                RefreshPlaySettings();
            });

            var reducedMotion = CreateWideUtilityButton(
                "ReducedMotionButton", panel,
                "움직임 줄이기", "꺼짐",
                new Vector2(0f, 155f), out reducedMotionStatus);
            reducedMotion.onClick.AddListener(() =>
            {
                LobbySettingsProfile.SetReducedMotionEnabled(
                    !LobbySettingsProfile.ReducedMotionEnabled);
                RefreshPlaySettings();
            });

            CreateReadableText(
                "ReducedMotionCaption", panel,
                "켜면 강한 점프의 화면 흔들림과 확대 연출을 제거합니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 65f), new Vector2(680f, 58f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

            var privacy = CreateWideUtilityButton(
                "AdConsentButton", panel,
                "광고 개인정보", "선택 관리",
                new Vector2(0f, -60f), out adConsentStatus);
            privacy.onClick.AddListener(ShowAdPrivacyOptions);
            CreateReadableText(
                "AdConsentCaption", panel,
                "동의 여부와 관계없이 게임은 플레이할 수 있습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -150f), new Vector2(680f, 58f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

            var done = CreateBrushButton(
                "PlaySettingsDone", panel, "완료",
                new Vector2(0f, -520f),
                new Vector2(390f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CardTitleSize);
            done.onClick.AddListener(ShowOptionsPage);
            RefreshPlaySettings();
        }

        void BuildAccountPage(Transform panel)
        {
            var back = CreateBrushButton(
                "AccountBack", panel, "옵션으로",
                new Vector2(-250f, 640f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            back.onClick.AddListener(ShowOptionsPage);

            CreateReadableText(
                "AccountTitle", panel, "계정과 기록",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 535f), new Vector2(560f, 82f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            accountKindText = CreateReadableText(
                "AccountKind", panel, "게스트",
                InkUiStyle.CardTitleSize,
                new Vector2(0f, 445f), new Vector2(650f, 70f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            accountStatusText = CreateReadableText(
                "AccountStatus", panel,
                "로그인하지 않아도 바로 플레이할 수 있습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 375f), new Vector2(680f, 90f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

            accountGoogleButton = CreateWideUtilityButton(
                "GoogleLoginButton", panel,
                "Google", "로그인·연결",
                new Vector2(0f, 245f), out _);
            accountGoogleButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.SignInWithGoogle());
            accountAppleButton = CreateWideUtilityButton(
                "AppleLoginButton", panel,
                "Apple", "로그인·연결",
                new Vector2(0f, 105f), out _);
            accountAppleButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.SignInWithApple());
            // Android 1.0과 WebGL은 fresh authorization code를 안전하게 얻는
            // 탈퇴 철회 흐름이 없어 Apple 계정 생성을 제공하지 않는다.
            accountAppleButton.gameObject.SetActive(
                ShouldOfferAppleSignIn(Application.platform));

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
            CreateReadableText(
                "ConflictTitle", accountConflictRoot,
                "계정 선택이 필요합니다",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 300f), new Vector2(680f, 90f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "ConflictCaption", accountConflictRoot,
                "이미 사용 중인 계정입니다\n현재 게스트 기록은 기기에 보관하고 합치지 않습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 115f), new Vector2(680f, 130f),
                InkPalette.Red, TextAnchor.MiddleCenter,
                strong: true);
            var useExisting = CreatePaperButton(
                "UseExistingAccount", accountConflictRoot,
                "기존 계정으로 전환",
                new Vector2(-180f, -75f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            useExisting.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .UseExistingAccountAfterConflict());
            var keepGuest = CreatePaperButton(
                "KeepGuestAccount", accountConflictRoot,
                "현재 게스트 유지",
                new Vector2(180f, -75f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            keepGuest.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .KeepCurrentGuestAfterConflict());
            CreateReadableText(
                "ConflictFootnote", accountConflictRoot,
                "선택하기 전에는 다른 계정 메뉴를 조작할 수 없습니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -235f), new Vector2(680f, 70f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

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
            CreateReadableText(
                "SyncConflictTitle", syncConflictRoot,
                "기록 선택이 필요합니다",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 300f), new Vector2(680f, 90f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            CreateReadableText(
                "SyncConflictCaption", syncConflictRoot,
                "다른 기기의 성장 기록이 변경되었습니다\n자동으로 합치면 먹빛이나 성장이 사라질 수 있어 직접 선택해야 합니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 115f), new Vector2(690f, 150f),
                InkPalette.Red, TextAnchor.MiddleCenter,
                strong: true);
            var useServer = CreatePaperButton(
                "UseServerRecord", syncConflictRoot,
                "서버 기록 사용",
                new Vector2(-180f, -85f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            useServer.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .UseServerAfterSyncConflict());
            var useDevice = CreatePaperButton(
                "UseDeviceRecord", syncConflictRoot,
                "이 기기 기록 사용",
                new Vector2(180f, -85f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            useDevice.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .KeepThisDeviceAfterSyncConflict());
            CreateReadableText(
                "SyncConflictFootnote", syncConflictRoot,
                "최고 고도는 어느 쪽을 골라도 더 높은 기록을 보존합니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -245f), new Vector2(680f, 70f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

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
                "확인이 끝나기 전에 플레이하면 다른 계정의 기록이 섞일 수 있어 잠시 시작을 멈춥니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 30f), new Vector2(690f, 100f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            accountSyncRetryButton = CreatePaperButton(
                "RetryAccountSync", accountSyncPendingRoot,
                "다시 확인",
                new Vector2(-180f, -130f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            accountSyncRetryButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .RetryPendingProfileResolution());
            accountSyncReturnButton = CreatePaperButton(
                "ReturnToLocalGuest", accountSyncPendingRoot,
                "로컬 게스트로 돌아가기",
                new Vector2(180f, -130f),
                new Vector2(340f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            accountSyncReturnButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?
                    .ReturnToLocalGuestDuringAccountSync());
            accountSyncPendingFootnoteText = CreateReadableText(
                "SyncPendingFootnote", accountSyncPendingRoot,
                "로컬 게스트로 돌아가면 전환 전 기기 기록을 다시 불러옵니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -280f), new Vector2(680f, 70f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);

            accountLogoutButton = CreateUtilityButton(
                "AccountLogout", panel,
                "로그아웃", "로컬 게스트로 전환",
                new Vector2(-180f, -250f));
            accountLogoutButton.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.Logout());
            var leaderboard = CreateUtilityButton(
                "LeaderboardButton", panel,
                "최고 고도", "TOP 10",
                new Vector2(180f, -250f));
            leaderboard.onClick.AddListener(ShowLeaderboardPage);

            accountDeleteButton = CreateBrushButton(
                "AccountDelete", panel, "계정 삭제",
                new Vector2(0f, -410f),
                new Vector2(520f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            accountDeleteLabel = accountDeleteButton.transform
                .Find("Label")?.GetComponent<Text>();
            accountDeleteButton.onClick.AddListener(HandleDeleteAccount);

            var legal = CreatePaperButton(
                "AccountLegal", panel,
                "개인정보·계정 삭제 안내",
                new Vector2(0f, -535f),
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            legal.onClick.AddListener(OpenPrivacyPolicy);

            var done = CreateBrushButton(
                "AccountDone", panel, "완료",
                new Vector2(0f, -650f),
                new Vector2(390f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CardTitleSize);
            done.onClick.AddListener(ShowOptionsPage);
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
            var back = CreateBrushButton(
                "LeaderboardBack", panel, "계정으로",
                new Vector2(-250f, 640f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            back.onClick.AddListener(ShowAccountPage);

            CreateReadableText(
                "LeaderboardTitle", panel, "최고 고도 순위",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 535f), new Vector2(600f, 82f),
                InkPalette.TextDark, TextAnchor.MiddleCenter,
                strong: true);
            leaderboardStatusText = CreateReadableText(
                "LeaderboardStatus", panel,
                "순위를 불러오려면 새로고침을 눌러 주세요",
                InkUiStyle.CaptionSize,
                new Vector2(0f, 455f), new Vector2(680f, 60f),
                InkPalette.TextMuted, TextAnchor.MiddleCenter);
            CreateDivider(panel, "LeaderboardDivider", 410f, 680f);

            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                leaderboardRows[i] = CreateReadableText(
                    $"LeaderboardRow{i + 1}", panel,
                    $"{i + 1}위     —",
                    InkUiStyle.BodySize,
                    new Vector2(0f, 350f - i * 68f),
                    new Vector2(620f, 56f),
                    i < 3 ? InkPalette.Red : InkPalette.TextDark,
                    TextAnchor.MiddleCenter,
                    strong: i < 3);
            }

            var refresh = CreatePaperButton(
                "LeaderboardRefresh", panel, "순위 새로고침",
                new Vector2(0f, -430f),
                new Vector2(620f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            refresh.onClick.AddListener(
                () => MukJumpAccountRuntime.Instance?.RefreshLeaderboard());
            var done = CreateBrushButton(
                "LeaderboardDone", panel, "완료",
                new Vector2(0f, -575f),
                new Vector2(390f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CardTitleSize);
            done.onClick.AddListener(ShowAccountPage);
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
                boundAccountRuntime.StateChanged += RefreshAccountState;
            RefreshAccountState();
        }

        void UnbindAccountRuntime()
        {
            if (boundAccountRuntime != null)
                boundAccountRuntime.StateChanged -= RefreshAccountState;
            boundAccountRuntime = null;
        }

        void ShowAccountPage()
        {
            deleteConfirmationArmed = false;
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
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, true);
            BindAccountRuntime();
            RefreshLeaderboardPage();
            MukJumpAccountRuntime.Instance?.RefreshLeaderboard();
        }

        void RefreshLeaderboardPage()
        {
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            if (leaderboardStatusText != null)
                leaderboardStatusText.text = runtime != null
                    ? runtime.LeaderboardStatus
                    : "서버 연결 전에도 게임은 계속 플레이할 수 있습니다";

            IReadOnlyList<MukJumpLeaderboardEntry> entries =
                runtime?.LeaderboardEntries;
            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                if (leaderboardRows[i] == null)
                    continue;
                leaderboardRows[i].text = entries != null && i < entries.Count
                    ? $"{entries[i].Rank}위     {entries[i].Height:N0}m"
                    : $"{i + 1}위     —";
            }
        }

        void HandleDeleteAccount()
        {
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            if (runtime == null || !runtime.IsOnlineAuthenticated)
                return;
            if (!deleteConfirmationArmed)
            {
                deleteConfirmationArmed = true;
                if (accountDeleteLabel != null)
                    accountDeleteLabel.text = "정말 삭제할까요? 한 번 더 누르기";
                if (accountStatusText != null)
                    accountStatusText.text =
                        "계정과 서버 기록이 영구 삭제됩니다";
                return;
            }
            deleteConfirmationArmed = false;
            runtime.DeleteAccountConfirmed();
        }

        void RefreshAccountState()
        {
            MukJumpAccountRuntime runtime = MukJumpAccountRuntime.Instance;
            if (accountKindText == null || accountStatusText == null)
                return;

            bool online = runtime != null && runtime.IsOnlineAuthenticated;
            MukJumpAccountKind kind = runtime != null
                ? runtime.AccountKind
                : MukJumpAccountKind.LocalGuest;
            accountKindText.text = kind switch
            {
                MukJumpAccountKind.BackendGuest => "연결된 게스트 계정",
                MukJumpAccountKind.Google => "Google 계정",
                MukJumpAccountKind.Apple => "Apple 계정",
                _ => "로컬 게스트",
            };
            accountStatusText.text = runtime != null
                ? runtime.StatusMessage
                : "로그인하지 않아도 바로 플레이할 수 있습니다";

            bool busy = runtime != null &&
                (runtime.Phase == MukJumpAccountPhase.Connecting ||
                 runtime.Phase == MukJumpAccountPhase.NeedsAccountChoice ||
                 runtime.Phase == MukJumpAccountPhase.NeedsSyncChoice ||
                 runtime.Phase == MukJumpAccountPhase.Deleting ||
                 runtime.BlocksGameplayForAccountSync);
            bool canStartSocialLogin = CanStartSocialLogin(
                online,
                kind,
                busy);
            if (accountGoogleButton != null)
                accountGoogleButton.interactable = canStartSocialLogin;
            if (accountAppleButton != null)
                accountAppleButton.interactable = canStartSocialLogin;
            if (accountLogoutButton != null)
                accountLogoutButton.interactable = online && !busy;
            if (accountDeleteButton != null)
                accountDeleteButton.interactable = online && !busy;
            if (accountConflictRoot != null)
            {
                bool active = runtime != null &&
                              runtime.HasPendingAccountConflict;
                accountConflictRoot.gameObject.SetActive(active);
                if (active)
                    accountConflictRoot.SetAsLastSibling();
            }
            if (syncConflictRoot != null)
            {
                bool active = runtime != null &&
                              runtime.HasPendingSyncConflict;
                syncConflictRoot.gameObject.SetActive(active);
                if (active)
                    syncConflictRoot.SetAsLastSibling();
            }
            if (accountSyncPendingRoot != null)
            {
                bool active = runtime != null &&
                              runtime.BlocksGameplayForAccountSync;
                bool deleting = runtime != null &&
                                runtime.HasPendingAccountDeletionCleanup;
                accountSyncPendingRoot.gameObject.SetActive(active);
                if (accountSyncPendingTitleText != null)
                    accountSyncPendingTitleText.text = deleting
                        ? "계정 삭제 마무리 중"
                        : "계정 기록 확인 중";
                if (accountSyncPendingStatusText != null)
                    accountSyncPendingStatusText.text = runtime != null
                        ? runtime.StatusMessage
                        : "계정 기록을 확인하고 있습니다";
                if (accountSyncPendingCaptionText != null)
                    accountSyncPendingCaptionText.text = deleting
                        ? "서버 계정 삭제 요청 뒤 기기에 남은 기록까지 안전하게 지우고 있습니다"
                        : "확인이 끝나기 전에 플레이하면 다른 계정의 기록이 섞일 수 있어 잠시 시작을 멈춥니다";
                if (accountSyncReturnButton != null)
                    accountSyncReturnButton.gameObject.SetActive(!deleting);
                if (accountSyncRetryButton != null)
                {
                    RectTransform retryRect = accountSyncRetryButton
                        .GetComponent<RectTransform>();
                    if (retryRect != null)
                        retryRect.anchoredPosition = new Vector2(
                            deleting ? 0f : -180f,
                            -130f);
                }
                if (accountSyncPendingFootnoteText != null)
                    accountSyncPendingFootnoteText.text = deleting
                        ? "삭제가 끝날 때까지 다시 확인을 눌러 기기 데이터 정리를 완료해 주세요"
                        : "로컬 게스트로 돌아가면 전환 전 기기 기록을 다시 불러옵니다";
                if (active)
                    accountSyncPendingRoot.SetAsLastSibling();
            }
            if (!deleteConfirmationArmed && accountDeleteLabel != null)
                accountDeleteLabel.text = "계정 삭제";
            RefreshLeaderboardPage();
        }

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
                InkUiStyle.ActionButtonLabelSize);
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
                InkUiStyle.BodySize);
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
            var close = CreateBrushButton(
                "TutorialClose", panel, "옵션으로",
                new Vector2(-250f, 640f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            close.onClick.AddListener(ShowOptionsPage);

            tutorialPage = CreateReadableText(
                "Page", panel, "1 / 5", InkUiStyle.BodySize,
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
                46,
                new Vector2(0f, 30f), new Vector2(680f, 90f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            tutorialDescription = CreateReadableText(
                "TutorialDescription", panel, string.Empty,
                40,
                new Vector2(0f, -200f), new Vector2(680f, 320f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter);
            tutorialDescription.lineSpacing = 1.2f;

            tutorialPreviousButton = CreateBrushButton(
                "PreviousButton", panel, "이전",
                new Vector2(-190f, -520f),
                new Vector2(300f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            tutorialPreviousButton.onClick.AddListener(PreviousTutorialPage);
            var next = CreateBrushButton(
                "NextButton", panel, "다음",
                new Vector2(190f, -520f),
                new Vector2(300f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.ActionButtonLabelSize);
            tutorialNextLabel = next.transform
                .Find("Label")?.GetComponent<Text>();
            next.onClick.AddListener(NextTutorialPage);
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
            CreateImage(
                "Outline", root, null, Vector2.zero,
                new Vector2(700f, 132f), WithAlpha(InkPalette.Ink, 0.34f));
            var paper = CreateImage(
                "Paper", root, null, Vector2.zero,
                new Vector2(696f, 128f), InkPalette.Paper2);
            CreateReadableText(
                "Label", paper.transform, label, InkUiStyle.CardTitleSize,
                new Vector2(-275f, 0f), new Vector2(120f, 72f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);
            valueText = CreateReadableText(
                "Value", paper.transform, "100%", InkUiStyle.CaptionSize,
                new Vector2(180f, 0f), new Vector2(90f, 58f),
                InkPalette.TextDark, TextAnchor.MiddleRight);

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
                InkUiStyle.CaptionSize);
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
            RefreshSettings();
        }

        void ToggleSfx()
        {
            float next = LobbySettingsProfile.SfxVolume > 0.01f
                ? 0f
                : LobbySettingsProfile.SfxResumeVolume;
            LobbySettingsProfile.SetSfxVolume(next);
            RefreshSettings();
        }

        void RefreshSettings()
        {
            if (bgmSlider == null || sfxSlider == null) return;
            suppressSliderCallbacks = true;
            bgmSlider.value = LobbySettingsProfile.BgmVolume;
            sfxSlider.value = LobbySettingsProfile.SfxVolume;
            suppressSliderCallbacks = false;
            RefreshAudioLabels();
            RefreshPlaySettings();
            RefreshDebugScenario();
            RefreshAdPrivacyStatus();
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
                debugScenarioStatus.text = status;
            if (debugScenarioSummary != null)
            {
                debugScenarioSummary.text = selected != null
                    ? $"선택됨 · {selected.Summary}"
                    : "선택됨 · 저장된 성장으로 0m부터 시작";
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
                    outline.color = isSelected ? InkPalette.Red : InkPalette.Ink;
                if (paper != null)
                    paper.color = isSelected
                        ? Color.Lerp(InkPalette.Paper2, InkPalette.Red, 0.10f)
                        : InkPalette.Paper2;
            }
        }

        void RefreshAudioLabels()
        {
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
                hapticsStatus.text = LobbySettingsProfile.HapticsEnabled
                    ? "켜짐"
                    : "꺼짐";
            if (reducedMotionStatus != null)
                reducedMotionStatus.text =
                    LobbySettingsProfile.ReducedMotionEnabled
                        ? "켜짐"
                        : "꺼짐";
        }

        void ShowCustomerCenterGuide()
        {
            connectionStatus.text =
                $"고객센터 문의 · {CustomerSupportEmail}";
#if !UNITY_EDITOR
            Application.OpenURL(
                $"mailto:{CustomerSupportEmail}?subject=" +
                System.Uri.EscapeDataString("먹점프 고객 문의"));
#endif
        }

        void OpenPrivacyPolicy()
        {
            if (accountStatusText != null)
                accountStatusText.text =
                    "개인정보처리방침과 계정 삭제 안내를 엽니다";
#if !UNITY_EDITOR
            Application.OpenURL(PrivacyPolicyUrl);
#endif
        }

        void ShowAdPrivacyOptions()
        {
            if (connectionStatus != null)
                connectionStatus.text = "광고 개인정보 선택 화면을 여는 중입니다";
            GoogleMobileAdsPrivacy.ShowOptions(message =>
            {
                if (connectionStatus != null)
                    connectionStatus.text = message;
                RefreshAdPrivacyStatus();
            });
        }

        void RefreshAdPrivacyStatus()
        {
            string status = GoogleMobileAdsPrivacy.IsRequired
                ? "선택 필요"
                : GoogleMobileAdsPrivacy.IsAvailable
                    ? "선택 관리"
                    : "해당 없음";
            if (adPrivacyStatus != null)
                adPrivacyStatus.text = status;
            if (adConsentStatus != null)
                adConsentStatus.text = status;
        }

        void ShowOptionsPage()
        {
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
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(playSettingsGroup, true);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            RefreshPlaySettings();
            RefreshAdPrivacyStatus();
        }

        void ShowDebugScenarioPage()
        {
            if (!GameManager.DebugToolsAvailable || debugScenarioGroup == null)
                return;
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
            currentTutorialPage = Mathf.Clamp(
                page,
                0,
                GameplayTutorialCatalog.Count - 1);

            GameplayTutorialPage pageData =
                GameplayTutorialCatalog.Get(currentTutorialPage);
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, true);
            SetPageVisible(playSettingsGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            SetPageVisible(accountGroup, false);
            SetPageVisible(leaderboardGroup, false);
            tutorialTitle.text = pageData.Title;
            tutorialDescription.text = pageData.Description;
            tutorialImage.sprite = Resources.Load<Sprite>(
                pageData.SpriteResourcePath);
            tutorialImage.color = tutorialImage.sprite != null
                ? Color.white
                : InkPalette.Ink;
            tutorialPage.text =
                $"{currentTutorialPage + 1} / {GameplayTutorialCatalog.Count}";
            tutorialPreviousButton.interactable =
                currentTutorialPage > 0;
            tutorialNextLabel.text =
                currentTutorialPage == GameplayTutorialCatalog.Count - 1
                    ? "완료"
                    : "다음";
        }

        void PreviousTutorialPage()
        {
            if (currentTutorialPage <= 0) return;
            ShowTutorialPage(currentTutorialPage - 1);
        }

        void NextTutorialPage()
        {
            if (currentTutorialPage < GameplayTutorialCatalog.Count - 1)
            {
                ShowTutorialPage(currentTutorialPage + 1);
                return;
            }
            LobbySettingsProfile.MarkTutorialSeen();
            ShowOptionsPage();
        }

        void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
            ApplySafeArea();
        }

        void CloseImmediate()
        {
            SetVisible(false);
            ShowOptionsPage();
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
                return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(
                safeAreaRoot,
                safe,
                Screen.width,
                Screen.height);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;

            float panelScale = MobileUiLayout.CalculateFitScale(
                new Vector2(PanelWidth, PanelHeight),
                safe,
                Screen.width,
                Screen.height,
                Vector2.one * (SafeAreaPadding * 0.5f));
            if (optionsPanel != null)
            {
                optionsPanel.anchoredPosition = Vector2.zero;
                optionsPanel.localScale = Vector3.one * panelScale;
            }
        }

        static CanvasGroup CreatePageGroup(string name, Transform parent)
        {
            var root = CreateStretchRect(name, parent);
            return root.gameObject.AddComponent<CanvasGroup>();
        }

        static void SetPageVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        static void BuildScrollFrame(Transform panel)
        {
            CreateImage(
                "InkShadow", panel, null, new Vector2(8f, -10f),
                new Vector2(800f, 1450f),
                new Color(0f, 0f, 0f, 0.16f));
            CreateImage(
                "ScrollOutline", panel, null, Vector2.zero,
                new Vector2(800f, 1450f), WithAlpha(InkPalette.Ink, 0.72f));
            CreateImage(
                "HanjiPaper", panel, null, Vector2.zero,
                new Vector2(792f, 1442f), InkPalette.Paper);
            CreateImage(
                "PaperCore", panel, null, Vector2.zero,
                new Vector2(764f, 1414f), InkPalette.Paper2);
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
                new Color(0f, 0f, 0f, 0.001f));
            hitArea.raycastTarget = true;
            var track = CreateImage(
                "Track", root, null, Vector2.zero,
                new Vector2(size.x, 16f),
                new Color(
                    InkPalette.Ink.r,
                    InkPalette.Ink.g,
                    InkPalette.Ink.b,
                    0.22f));
            var fillArea = CreateRect(
                "FillArea", root,
                new Vector2(-12f, 0f),
                new Vector2(size.x - 30f, 18f));
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(12f, -9f);
            fillArea.offsetMax = new Vector2(-12f, 9f);
            var fill = CreateImage(
                "Fill", fillArea, null, Vector2.zero,
                fillArea.sizeDelta, InkPalette.Ink);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = CreateStretchRect("HandleArea", root);
            handleArea.offsetMin = new Vector2(18f, 0f);
            handleArea.offsetMax = new Vector2(-18f, 0f);
            var handle = CreateImage(
                "Handle", handleArea,
                InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, new Vector2(48f, 48f), InkPalette.Ink);
            handle.raycastTarget = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            track.raycastTarget = true;
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
                new Vector2(98f, 0f), new Vector2(130f, 72f),
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
                InkUiStyle.BodySize);
            Transform paper = button.transform.Find("Paper");
            CreateReadableText(
                "Title",
                paper,
                title,
                InkUiStyle.BodySize,
                new Vector2(-155f, 0f),
                new Vector2(350f, 74f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft,
                strong: true);
            statusText = CreateReadableText(
                "Status",
                paper,
                status,
                InkUiStyle.CaptionSize,
                new Vector2(210f, 0f),
                new Vector2(240f, 74f),
                InkPalette.TextDark,
                TextAnchor.MiddleRight);
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
            int fontSize)
        {
            var outline = CreateImage(
                objectName, parent, null, position, size, InkPalette.Ink);
            outline.raycastTarget = true;
            var paper = CreateImage(
                "Paper", outline.transform, null, Vector2.zero,
                size - new Vector2(4f, 4f), InkPalette.Paper2);
            var button = outline.gameObject.AddComponent<Button>();
            CreateReadableText(
                "Label", paper.transform, label,
                Mathf.Max(fontSize, InkUiStyle.StandardButtonLabelSize),
                Vector2.zero, size - new Vector2(28f, 16f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            // 비활성 상태는 얇은 테두리만이 아니라 한지 면 전체가 흐려져야
            // 첫 튜토리얼의 사용할 수 없는 `이전` 버튼도 즉시 구분된다.
            InkUiStyle.ConfigureButton(button, paper);
            return button;
        }

        static Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
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
                labelText);
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
            text.text = value;
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
                null,
                new Vector2(0f, y),
                new Vector2(width, 2f),
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
