using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비 옵션, 로컬 소리 설정, 지원 안내와 5장 튜토리얼을 제공한다.
    /// 실제 고객센터·Google/Apple 연결은 하지 않으며 준비 중 정보만 제공한다.
    [DisallowMultipleComponent]
    public sealed class LobbyOptionsView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4150;
        const float PanelWidth = 820f;
        const float PanelHeight = 1510f;
        const float SafeAreaPadding = 24f;

        CanvasGroup rootGroup;
        CanvasGroup optionsGroup;
        CanvasGroup tutorialGroup;
        CanvasGroup debugScenarioGroup;
        RectTransform safeAreaRoot;
        RectTransform optionsPanel;
        Slider bgmSlider;
        Slider sfxSlider;
        Text bgmValue;
        Text sfxValue;
        Text bgmToggleLabel;
        Text sfxToggleLabel;
        Text uidText;
        Text connectionStatus;
        Text debugScenarioStatus;
        Text debugScenarioSummary;
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

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public bool IsTutorialOpen =>
            IsOpen && tutorialGroup != null && tutorialGroup.blocksRaycasts;
        public bool IsDebugScenarioOpen =>
            IsOpen && debugScenarioGroup != null &&
            debugScenarioGroup.blocksRaycasts;
        public int TutorialPageCount => GameplayTutorialCatalog.Count;
        public int CurrentTutorialPage => currentTutorialPage;
        public string PlayerUidLabel => uidText != null ? uidText.text : string.Empty;

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
        }

        void OnDisable()
        {
            LobbySettingsProfile.Changed -= RefreshSettings;
            DebugShowcaseScenarioProfile.Changed -= RefreshDebugScenario;
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
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                Screen.safeArea != lastSafeArea)
                ApplySafeArea();
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
            ShowOptionsPage();
            SetVisible(true);
        }

        public void Close()
        {
            LobbySettingsProfile.Flush();
            SetVisible(false);
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

            var uidButton = CreatePaperButton(
                "UidButton", panel, string.Empty,
                new Vector2(0f, 475f),
                new Vector2(700f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            uidText = uidButton.transform
                .Find("Paper/Label")?.GetComponent<Text>();
            Transform uidPaper = uidButton.transform.Find("Paper");
            if (uidText != null)
            {
                uidText.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
                uidText.rectTransform.sizeDelta = new Vector2(470f, 88f);
                uidText.alignment = TextAnchor.MiddleCenter;
                uidText.fontSize = InkUiStyle.BodySize;
                uidText.fontStyle = FontStyle.Normal;
            }
            CreateReadableText(
                "UidCaption", uidPaper, "UID", InkUiStyle.CaptionSize,
                new Vector2(-290f, 0f), new Vector2(84f, 72f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft,
                strong: true);
            CreateReadableText(
                "CopyHint", uidPaper, "복사", InkUiStyle.CaptionSize,
                new Vector2(292f, 0f), new Vector2(88f, 72f),
                InkPalette.TextMuted,
                TextAnchor.MiddleRight);
            uidButton.onClick.AddListener(CopyUid);

            CreateReadableText(
                "AudioCaption", panel, "소리",
                InkUiStyle.BodySize,
                new Vector2(-290f, 380f), new Vector2(120f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);

            CreateAudioCard(
                panel,
                "BgmCard",
                "배경음",
                new Vector2(0f, 285f),
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
                new Vector2(0f, 135f),
                out sfxSlider,
                out sfxValue,
                out Button sfxToggle,
                out sfxToggleLabel);
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            sfxToggle.onClick.AddListener(ToggleSfx);

            CreateReadableText(
                "HelpCaption", panel, "도움과 정보",
                InkUiStyle.BodySize,
                new Vector2(-250f, 35f), new Vector2(200f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft,
                strong: true);

            var language = CreateUtilityButton(
                "LanguageButton", panel, "언어", "한국어",
                new Vector2(-180f, -65f));
            language.onClick.AddListener(ShowLanguageGuide);
            var support = CreateUtilityButton(
                "CustomerCenterButton", panel, "고객센터", "준비 중",
                new Vector2(180f, -65f));
            support.onClick.AddListener(ShowCustomerCenterGuide);
            var account = CreateUtilityButton(
                "AccountConnectButton", panel, "계정 연동", "준비 중",
                new Vector2(-180f, -205f));
            account.onClick.AddListener(ShowConnectionGuide);
            var guide = CreateUtilityButton(
                "GuideButton", panel, "튜토리얼", "다시 보기",
                new Vector2(180f, -205f));
            guide.onClick.AddListener(() => ShowTutorialPage(0));

            bool showDebugScenario = GameManager.DebugToolsAvailable;
            if (showDebugScenario)
            {
                var debugScenario = CreateWideUtilityButton(
                    "DebugScenarioButton",
                    panel,
                    "DEBUG · 연출 시나리오",
                    "일반 플레이",
                    new Vector2(0f, -335f),
                    out debugScenarioStatus);
                debugScenario.onClick.AddListener(ShowDebugScenarioPage);
            }

            connectionStatus = CreateReadableText(
                "ConnectionStatus", panel,
                "설정은 이 기기에 저장됩니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, showDebugScenario ? -438f : -325f),
                new Vector2(700f, 56f),
                InkPalette.TextMuted);

            var close = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, showDebugScenario ? -550f : -450f),
                new Vector2(390f, 120f),
                InkUiStyle.CardTitleSize);
            close.onClick.AddListener(Close);

            CreateReadableText(
                "PrivacyCaption", panel,
                "로컬 저장  ·  개인정보 수집 없음",
                InkUiStyle.CaptionSize,
                new Vector2(0f, showDebugScenario ? -655f : -555f),
                new Vector2(700f, 44f),
                InkPalette.TextMuted);
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
                InkPalette.TextMuted);

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
                InkUiStyle.CardTitleSize,
                new Vector2(0f, 30f), new Vector2(680f, 90f),
                InkPalette.TextDark,
                TextAnchor.MiddleCenter,
                strong: true);
            tutorialDescription = CreateReadableText(
                "TutorialDescription", panel, string.Empty,
                InkUiStyle.BodySize,
                new Vector2(0f, -200f), new Vector2(680f, 300f),
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
            uidText.text = LobbySettingsProfile.PlayerUid;
            RefreshAudioLabels();
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

        void ShowConnectionGuide()
        {
            connectionStatus.text =
                "Google Play · Apple 계정 연동은 준비 중입니다";
        }

        void ShowLanguageGuide()
        {
            connectionStatus.text = "현재 한국어를 지원합니다";
        }

        void ShowCustomerCenterGuide()
        {
            connectionStatus.text =
                "고객센터는 제출 버전에서 준비 중입니다";
        }

        void CopyUid()
        {
            GUIUtility.systemCopyBuffer = LobbySettingsProfile.PlayerUid;
            connectionStatus.text = "플레이어 UID를 복사했습니다";
        }

        void ShowOptionsPage()
        {
            SetPageVisible(optionsGroup, true);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(debugScenarioGroup, false);
            RefreshDebugScenario();
        }

        void ShowDebugScenarioPage()
        {
            if (!GameManager.DebugToolsAvailable || debugScenarioGroup == null)
                return;
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, false);
            SetPageVisible(debugScenarioGroup, true);
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
            SetPageVisible(debugScenarioGroup, false);
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
            lastSafeArea = Screen.safeArea;

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
                InkPalette.TextMuted, TextAnchor.MiddleRight);
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
                InkPalette.TextMuted,
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
