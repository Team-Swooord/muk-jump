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
        const float PanelWidth = 900f;
        const float PanelHeight = 1510f;
        const float SafeAreaPadding = 40f;

        CanvasGroup rootGroup;
        CanvasGroup optionsGroup;
        CanvasGroup tutorialGroup;
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
        Image tutorialImage;
        Text tutorialTitle;
        Text tutorialDescription;
        Text tutorialPage;
        Text tutorialNextLabel;
        Button tutorialPreviousButton;
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
        }

        void OnDisable()
        {
            LobbySettingsProfile.Changed -= RefreshSettings;
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
                new Color(0.025f, 0.023f, 0.02f, 0.66f));
            dim.raycastTarget = true;
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

            ApplySafeArea();
            RefreshSettings();
        }

        void BuildOptionsPage(Transform panel)
        {
            CreateReadableText(
                "Title", panel, "설정", InkUiStyle.ScreenTitleSize,
                new Vector2(-240f, 620f), new Vector2(360f, 86f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft);
            CreateReadableText(
                "Version", panel, $"v{Application.version}",
                InkUiStyle.CaptionSize,
                new Vector2(280f, 620f), new Vector2(260f, 48f),
                InkPalette.TextMuted,
                TextAnchor.MiddleRight);

            var uidButton = CreatePaperButton(
                "UidButton", panel, string.Empty,
                new Vector2(0f, 500f),
                new Vector2(740f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            uidText = uidButton.transform
                .Find("Paper/Label")?.GetComponent<Text>();
            uidButton.onClick.AddListener(CopyUid);

            CreateReadableText(
                "AudioCaption", panel, "소리",
                InkUiStyle.CaptionSize,
                new Vector2(-305f, 405f), new Vector2(130f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft);

            CreateAudioCard(
                panel,
                "BgmCard",
                "배경음",
                new Vector2(-190f, 215f),
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
                new Vector2(190f, 215f),
                out sfxSlider,
                out sfxValue,
                out Button sfxToggle,
                out sfxToggleLabel);
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            sfxToggle.onClick.AddListener(ToggleSfx);

            CreateReadableText(
                "HelpCaption", panel, "도움과 정보",
                InkUiStyle.CaptionSize,
                new Vector2(-275f, 30f), new Vector2(200f, 48f),
                InkPalette.TextDark, TextAnchor.MiddleLeft);

            var language = CreateUtilityButton(
                "LanguageButton", panel, "한", "언어\n한국어 · 고정",
                new Vector2(-190f, -95f));
            language.onClick.AddListener(ShowLanguageGuide);
            var support = CreateUtilityButton(
                "CustomerCenterButton", panel, "문", "고객센터\n준비 중",
                new Vector2(190f, -95f));
            support.onClick.AddListener(ShowCustomerCenterGuide);
            var account = CreateUtilityButton(
                "AccountConnectButton", panel, "계",
                "계정 연동\nGoogle · Apple",
                new Vector2(-190f, -235f));
            account.onClick.AddListener(ShowConnectionGuide);
            var guide = CreateUtilityButton(
                "GuideButton", panel, "책", "튜토리얼\n5장 다시 보기",
                new Vector2(190f, -235f));
            guide.onClick.AddListener(() => ShowTutorialPage(0));

            connectionStatus = CreateReadableText(
                "ConnectionStatus", panel,
                "설정은 이 기기에 안전하게 저장됩니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -345f), new Vector2(720f, 60f),
                InkPalette.TextMuted);

            var close = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, -490f), new Vector2(420f, 120f),
                InkUiStyle.CardTitleSize);
            close.onClick.AddListener(Close);

            CreateReadableText(
                "PrivacyCaption", panel,
                "로컬 저장 · 개인정보 수집 없음",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -625f), new Vector2(720f, 48f),
                InkPalette.TextMuted);
        }

        void BuildTutorialPage(Transform panel)
        {
            var close = CreatePaperButton(
                "TutorialClose", panel, "옵션으로",
                new Vector2(-265f, 625f),
                new Vector2(210f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.CaptionSize);
            close.onClick.AddListener(ShowOptionsPage);

            tutorialPage = CreateReadableText(
                "Page", panel, "1 / 5", InkUiStyle.BodySize,
                new Vector2(265f, 625f), new Vector2(210f, 70f),
                InkPalette.TextMuted);

            var iconPaper = CreateImage(
                "TutorialIconPaper", panel,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 250f), new Vector2(560f, 560f),
                new Color(
                    InkPalette.Paper2.r,
                    InkPalette.Paper2.g,
                    InkPalette.Paper2.b,
                    0.94f));
            tutorialImage = CreateImage(
                "TutorialIcon", iconPaper.transform, null,
                Vector2.zero, new Vector2(410f, 410f), Color.white);
            tutorialImage.preserveAspect = true;

            tutorialTitle = CreateReadableText(
                "TutorialTitle", panel, string.Empty,
                InkUiStyle.ScreenTitleSize,
                new Vector2(-40f, -100f), new Vector2(680f, 150f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft);
            tutorialDescription = CreateReadableText(
                "TutorialDescription", panel, string.Empty,
                38,
                new Vector2(-40f, -285f), new Vector2(680f, 190f),
                InkPalette.TextDark,
                TextAnchor.MiddleLeft);
            tutorialDescription.lineSpacing = 1.15f;

            tutorialPreviousButton = CreatePaperButton(
                "PreviousButton", panel, "이전",
                new Vector2(-225f, -540f),
                new Vector2(250f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            tutorialPreviousButton.onClick.AddListener(PreviousTutorialPage);
            var next = CreateBrushButton(
                "NextButton", panel, "다음",
                new Vector2(225f, -540f),
                new Vector2(350f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
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
                new Vector2(350f, 280f));
            CreateImage(
                "Outline", root, null, Vector2.zero,
                new Vector2(350f, 280f), InkPalette.Ink);
            var paper = CreateImage(
                "Paper", root, null, Vector2.zero,
                new Vector2(340f, 270f), InkPalette.Paper2);
            CreateReadableText(
                "Label", paper.transform, label, InkUiStyle.CardTitleSize,
                new Vector2(-62f, 78f), new Vector2(185f, 58f),
                InkPalette.TextDark, TextAnchor.MiddleLeft);
            valueText = CreateReadableText(
                "Value", paper.transform, "100%", InkUiStyle.CaptionSize,
                new Vector2(104f, 78f), new Vector2(100f, 50f),
                InkPalette.TextDark, TextAnchor.MiddleRight);

            slider = CreateInkSlider(
                "Slider",
                paper.transform,
                new Vector2(0f, 15f),
                new Vector2(280f, 54f));
            toggle = CreatePaperButton(
                "Toggle",
                paper.transform,
                "켜짐",
                new Vector2(0f, -70f),
                new Vector2(280f, InkUiStyle.MinimumTapHeight),
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
            uidText.text =
                $"플레이어 UID   {LobbySettingsProfile.PlayerUid}   복사";
            RefreshAudioLabels();
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
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var shadow = CreateImage(
                "InkShadow", panel, brush, new Vector2(12f, -15f),
                new Vector2(1510f, 890f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            var outline = CreateImage(
                "ScrollOutline", panel, brush, Vector2.zero,
                new Vector2(1490f, 870f), InkPalette.Ink);
            outline.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            var paper = CreateImage(
                "HanjiPaper", panel, brush, Vector2.zero,
                new Vector2(1462f, 842f), InkPalette.Paper);
            paper.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            CreateImage(
                "PaperCore", panel, null, Vector2.zero,
                new Vector2(790f, 1390f), InkPalette.Paper);
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
            string glyph,
            string label,
            Vector2 position)
        {
            var button = CreatePaperButton(
                objectName,
                parent,
                label,
                position,
                new Vector2(350f, InkUiStyle.MinimumTapHeight),
                InkUiStyle.BodySize);
            Transform paper = button.transform.Find("Paper");
            var labelText = paper?.Find("Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.rectTransform.anchoredPosition =
                    new Vector2(45f, 0f);
                labelText.rectTransform.sizeDelta =
                    new Vector2(230f, 102f);
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.lineSpacing = 0.9f;
            }

            if (paper == null) return button;
            var icon = CreateImage(
                "IconInk",
                paper,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-125f, 0f),
                new Vector2(70f, 70f),
                InkPalette.Paper2);
            CreateReadableText(
                "Glyph",
                icon.transform,
                glyph,
                InkUiStyle.CaptionSize,
                Vector2.zero,
                new Vector2(58f, 58f),
                InkPalette.TextDark);
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
                size - new Vector2(10f, 10f), InkPalette.Paper2);
            var button = outline.gameObject.AddComponent<Button>();
            var labelText = CreateReadableText(
                "Label", paper.transform, label, fontSize,
                Vector2.zero, size - new Vector2(28f, 16f),
                InkPalette.TextLight);
            InkUiStyle.ConfigureActionButton(
                button,
                outline,
                labelText,
                paper);
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
                InkPalette.TextLight);
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
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = color;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                alignment,
                strong: true);
            return text;
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
