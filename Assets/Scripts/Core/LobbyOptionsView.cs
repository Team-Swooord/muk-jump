using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비 옵션, 로컬 소리 설정, 플랫폼 연동 안내와 4장 튜토리얼을 제공한다.
    /// 실제 Google/Apple 인증은 하지 않으며 버튼의 정보 구조만 제출 버전에 포함한다.
    [DisallowMultipleComponent]
    public sealed class LobbyOptionsView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4150;
        const int TutorialPageCountValue = 4;

        static readonly string[] TutorialTitles =
        {
            "먹방울은 스스로 뛰어요",
            "착지할 곳에 한 획을 그려요",
            "선의 기울기가 방향을 정해요",
            "먹을 아끼며 더 높이 올라요",
        };

        static readonly string[] TutorialDescriptions =
        {
            "캐릭터는 1초마다 자동으로 점프해요.\n점프 버튼을 찾지 않아도 됩니다.",
            "손가락으로 선을 그으면 바로 발판이 돼요.\n떨어질 자리를 먼저 보고 그려주세요.",
            "오른쪽으로 기울이면 오른쪽으로,\n왼쪽으로 기울이면 왼쪽으로 날아가요.",
            "붓을 쉬면 먹 게이지가 다시 차요.\n아이템과 분신을 모아 최고 높이에 도전하세요.",
        };

        static readonly string[] TutorialSpritePaths =
        {
            "MukJump/UI/Growth/growth_jump",
            "MukJump/UI/Growth/growth_platform",
            "MukJump/UI/Growth/growth_guard",
            "MukJump/UI/Growth/growth_ink_regen",
        };

        CanvasGroup rootGroup;
        CanvasGroup optionsGroup;
        CanvasGroup tutorialGroup;
        RectTransform safeAreaRoot;
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
        public int TutorialPageCount => TutorialPageCountValue;
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            rootGroup = root.GetComponent<CanvasGroup>();

            var dim = CreateStretchImage(
                "InkDim",
                root.transform,
                new Color(0.025f, 0.023f, 0.02f, 0.66f));
            dim.raycastTarget = true;
            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            var panel = CreateRect(
                "OptionsScroll",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(900f, 1510f));
            BuildScrollFrame(panel);

            optionsGroup = CreatePageGroup("OptionsPage", panel);
            BuildOptionsPage(optionsGroup.transform);
            tutorialGroup = CreatePageGroup("TutorialPage", panel);
            BuildTutorialPage(tutorialGroup.transform);

            ApplySafeArea();
            RefreshSettings();
        }

        void BuildOptionsPage(Transform panel)
        {
            CreateReadableText(
                "Title", panel, "옵션", InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 620f), new Vector2(700f, 86f),
                InkPalette.TextDark);
            CreateReadableText(
                "Subtitle", panel, "게임 설정과 쉬운 4장 가이드",
                InkUiStyle.BodySize,
                new Vector2(0f, 545f), new Vector2(730f, 62f),
                InkPalette.TextMuted);

            var guide = CreatePaperButton(
                "GuideButton", panel, "게임 방법   ·   4장",
                new Vector2(0f, 430f), new Vector2(740f, 126f),
                InkUiStyle.CardTitleSize);
            guide.onClick.AddListener(() => ShowTutorialPage(0));

            CreateAudioRow(
                panel,
                "BgmRow",
                "배경음",
                new Vector2(0f, 260f),
                out bgmSlider,
                out bgmValue,
                out Button bgmToggle,
                out bgmToggleLabel);
            bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
            bgmToggle.onClick.AddListener(ToggleBgm);

            CreateAudioRow(
                panel,
                "SfxRow",
                "효과음",
                new Vector2(0f, 90f),
                out sfxSlider,
                out sfxValue,
                out Button sfxToggle,
                out sfxToggleLabel);
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            sfxToggle.onClick.AddListener(ToggleSfx);

            CreateReadableText(
                "AccountCaption", panel, "계정 연동",
                InkUiStyle.BodySize,
                new Vector2(-255f, -42f), new Vector2(250f, 56f),
                InkPalette.TextDark, TextAnchor.MiddleLeft);
            var google = CreatePaperButton(
                "GoogleConnectButton", panel, "Google Play   준비 중",
                new Vector2(0f, -130f), new Vector2(740f, 100f),
                InkUiStyle.BodySize);
            var apple = CreatePaperButton(
                "AppleConnectButton", panel, "Apple   준비 중",
                new Vector2(0f, -245f), new Vector2(740f, 100f),
                InkUiStyle.BodySize);
            google.onClick.AddListener(ShowConnectionGuide);
            apple.onClick.AddListener(ShowConnectionGuide);

            connectionStatus = CreateReadableText(
                "ConnectionStatus", panel,
                "제출 버전은 로컬 저장으로 플레이합니다",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -330f), new Vector2(720f, 52f),
                InkPalette.TextMuted);

            var uidButton = CreatePaperButton(
                "UidButton", panel, string.Empty,
                new Vector2(0f, -425f), new Vector2(740f, 104f),
                InkUiStyle.BodySize);
            uidText = uidButton.transform
                .Find("Paper/Label")?.GetComponent<Text>();
            uidButton.onClick.AddListener(CopyUid);

            var close = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, -610f), new Vector2(420f, 96f),
                InkUiStyle.CardTitleSize);
            close.onClick.AddListener(Close);
        }

        void BuildTutorialPage(Transform panel)
        {
            var close = CreatePaperButton(
                "TutorialClose", panel, "옵션으로",
                new Vector2(-265f, 625f), new Vector2(210f, 82f),
                InkUiStyle.CaptionSize);
            close.onClick.AddListener(ShowOptionsPage);

            tutorialPage = CreateReadableText(
                "Page", panel, "1 / 4", InkUiStyle.BodySize,
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
                new Vector2(0f, -100f), new Vector2(760f, 150f),
                InkPalette.TextDark);
            tutorialDescription = CreateReadableText(
                "TutorialDescription", panel, string.Empty,
                38,
                new Vector2(0f, -285f), new Vector2(750f, 190f),
                InkPalette.TextDark);
            tutorialDescription.lineSpacing = 1.15f;

            tutorialPreviousButton = CreatePaperButton(
                "PreviousButton", panel, "이전",
                new Vector2(-225f, -540f), new Vector2(250f, 100f),
                InkUiStyle.BodySize);
            tutorialPreviousButton.onClick.AddListener(PreviousTutorialPage);
            var next = CreateBrushButton(
                "NextButton", panel, "다음",
                new Vector2(225f, -540f), new Vector2(350f, 100f),
                InkUiStyle.BodySize);
            tutorialNextLabel = next.transform
                .Find("Label")?.GetComponent<Text>();
            next.onClick.AddListener(NextTutorialPage);
        }

        void CreateAudioRow(
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
                new Vector2(740f, 140f));
            CreateReadableText(
                "Label", root, label, InkUiStyle.BodySize,
                new Vector2(-278f, 22f), new Vector2(170f, 54f),
                InkPalette.TextDark, TextAnchor.MiddleLeft);
            valueText = CreateReadableText(
                "Value", root, "100", InkUiStyle.CaptionSize,
                new Vector2(272f, 22f), new Vector2(120f, 50f),
                InkPalette.TextDark, TextAnchor.MiddleRight);

            slider = CreateInkSlider(
                "Slider",
                root,
                new Vector2(-35f, -35f),
                new Vector2(480f, 54f));
            toggle = CreatePaperButton(
                "Toggle",
                root,
                "켜짐",
                new Vector2(300f, -35f),
                new Vector2(130f, 70f),
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
            uidText.text = $"플레이어 UID   {LobbySettingsProfile.PlayerUid}";
            RefreshAudioLabels();
        }

        void RefreshAudioLabels()
        {
            if (bgmValue == null || sfxValue == null) return;
            bgmValue.text =
                Mathf.RoundToInt(LobbySettingsProfile.BgmVolume * 100f)
                    .ToString();
            sfxValue.text =
                Mathf.RoundToInt(LobbySettingsProfile.SfxVolume * 100f)
                    .ToString();
            bgmToggleLabel.text =
                LobbySettingsProfile.BgmVolume > 0.01f ? "켜짐" : "꺼짐";
            sfxToggleLabel.text =
                LobbySettingsProfile.SfxVolume > 0.01f ? "켜짐" : "꺼짐";
        }

        void ShowConnectionGuide()
        {
            connectionStatus.text =
                "연동 버튼 가이드만 제공하며 실제 로그인은 하지 않습니다";
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
                TutorialPageCountValue - 1);
            SetPageVisible(optionsGroup, false);
            SetPageVisible(tutorialGroup, true);
            tutorialTitle.text = TutorialTitles[currentTutorialPage];
            tutorialDescription.text =
                TutorialDescriptions[currentTutorialPage];
            tutorialImage.sprite = Resources.Load<Sprite>(
                TutorialSpritePaths[currentTutorialPage]);
            tutorialImage.color = tutorialImage.sprite != null
                ? Color.white
                : InkPalette.Ink;
            tutorialPage.text =
                $"{currentTutorialPage + 1} / {TutorialPageCountValue}";
            tutorialPreviousButton.interactable =
                currentTutorialPage > 0;
            tutorialNextLabel.text =
                currentTutorialPage == TutorialPageCountValue - 1
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
            if (currentTutorialPage < TutorialPageCountValue - 1)
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
            Rect safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                Mathf.Clamp01(safe.xMin / Screen.width),
                Mathf.Clamp01(safe.yMin / Screen.height));
            safeAreaRoot.anchorMax = new Vector2(
                Mathf.Clamp01(safe.xMax / Screen.width),
                Mathf.Clamp01(safe.yMax / Screen.height));
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
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
            var paper = CreateImage(
                "Paper", outline.transform, null, Vector2.zero,
                size - new Vector2(10f, 10f), InkPalette.Paper2);
            var button = outline.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, paper);
            CreateReadableText(
                "Label", paper.transform, label, fontSize,
                Vector2.zero, size - new Vector2(28f, 16f),
                InkPalette.TextDark);
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
                objectName, parent,
                InkUiTextureFactory.CreateBrushSprite(),
                position, size, InkPalette.Ink);
            var button = brush.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, brush);
            CreateReadableText(
                "Label", brush.transform, label, fontSize,
                Vector2.zero, size - new Vector2(36f, 14f),
                InkPalette.TextLight);
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
