using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Obstacles;
using MukJump.Player;

namespace MukJump.Core
{
    /// 하이어라키에서 직접 편집할 수 있는 플레이 중 고도 HUD.
    [ExecuteAlways]
    public class GameplayHudView : MonoBehaviour
    {
        const float TopHudWidth = 900f;
        const float TopHudHeight = 148f;
        const float TopHudSideMargin = 48f;

        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform topHudRoot;
        [SerializeField] Text heightText;
        [SerializeField] Text heightCaption;
        [SerializeField] Text bestText;
        [SerializeField] Text bestCaption;
        [SerializeField] RectTransform itemTestControls;
        [SerializeField] RectTransform debugPanel;
        [SerializeField] Button debugToggleButton;
        [SerializeField] Button invincibleButton;
        [SerializeField] Text invincibleLabel;
        [SerializeField] Button inkDropButton;
        [SerializeField] Button goldenBrushButton;
        [SerializeField] Button inkShieldButton;
        [SerializeField] Button inkCloneButton;
        [SerializeField] Button mapStartButton;
        [SerializeField] Button mapWindButton;
        [SerializeField] Button mapRainButton;
        [SerializeField] Button mapGorgeButton;
        [FormerlySerializedAs("restPlatformButton")]
        [SerializeField] Button updraftButton;
        [SerializeField] Button windDirectionButton;
        [SerializeField] Button windPlatformButton;
        [SerializeField] Button haetaeButton;
        [SerializeField] Button vfxQualityButton;
        [SerializeField] Text vfxQualityLabel;
        [SerializeField] Text vfxStatsText;
        [SerializeField] WindIndicatorView windIndicator;
        [SerializeField] NewBestIndicatorView newBestIndicator;

        public static GameplayHudView Instance { get; private set; }

        int lastHeight = int.MinValue;
        int lastBest = int.MinValue;
        bool lastNewBest;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;
        float nextVfxStatsRefreshTime;
        bool playerHealthBootstrapComplete;
        readonly List<PlayerController> playerHealthScratch =
            new(GameManager.MaxLivingPlayers);

        void OnEnable()
        {
            Instance = this;
            lastHeight = int.MinValue;
            lastBest = int.MinValue;
            lastNewBest = false;
            playerHealthBootstrapComplete = false;
            if (Application.isPlaying && GameManager.DebugToolsAvailable)
                EnsureVfxDebugControls();
            ApplyCrispTextSettings();
            // 제출 화면에는 개발용 서랍을 노출하지 않는다. 내부 디버그 API는
            // 회귀 테스트에서만 사용하고 HUD 계층은 항상 닫아 둔다.
            SetDebugToolsAvailable(false);
            if (!Application.isPlaying) return;
            if (windIndicator == null)
                windIndicator = GetComponentInChildren<WindIndicatorView>(true);
            if (newBestIndicator == null)
                newBestIndicator = GetComponentInChildren<NewBestIndicatorView>(true);
            EnsureTopHudContainer();
            HideLegacyAggregateHealth();
            playerHealthBootstrapComplete = EnsurePlayerHealthBillboards();
            if (newBestIndicator == null)
                newBestIndicator = NewBestIndicatorView.CreateRuntime(
                    topHudRoot != null ? topHudRoot : transform);
            ApplyPolishedRuntimeLayout();
            if (GameManager.DebugToolsAvailable)
            {
                debugToggleButton?.onClick.AddListener(ToggleDebugPanel);
                invincibleButton?.onClick.AddListener(ToggleInvincible);
                inkDropButton?.onClick.AddListener(UseInkDrop);
                goldenBrushButton?.onClick.AddListener(UseGoldenBrush);
                inkShieldButton?.onClick.AddListener(UseInkShield);
                inkCloneButton?.onClick.AddListener(UseInkClone);
                mapStartButton?.onClick.AddListener(MoveToStartHeight);
                mapWindButton?.onClick.AddListener(MoveToWindHeight);
                mapRainButton?.onClick.AddListener(MoveToRainHeight);
                mapGorgeButton?.onClick.AddListener(MoveToGorgeHeight);
                updraftButton?.onClick.AddListener(TriggerUpdraft);
                windDirectionButton?.onClick.AddListener(FlipWindDirection);
                windPlatformButton?.onClick.AddListener(SpawnWindPlatform);
                haetaeButton?.onClick.AddListener(SpawnHaetae);
                vfxQualityButton?.onClick.AddListener(CycleVfxQuality);
            }
        }

        void OnValidate()
        {
            ApplyCrispTextSettings(false);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (!Application.isPlaying) return;
            debugToggleButton?.onClick.RemoveListener(ToggleDebugPanel);
            invincibleButton?.onClick.RemoveListener(ToggleInvincible);
            inkDropButton?.onClick.RemoveListener(UseInkDrop);
            goldenBrushButton?.onClick.RemoveListener(UseGoldenBrush);
            inkShieldButton?.onClick.RemoveListener(UseInkShield);
            inkCloneButton?.onClick.RemoveListener(UseInkClone);
            mapStartButton?.onClick.RemoveListener(MoveToStartHeight);
            mapWindButton?.onClick.RemoveListener(MoveToWindHeight);
            mapRainButton?.onClick.RemoveListener(MoveToRainHeight);
            mapGorgeButton?.onClick.RemoveListener(MoveToGorgeHeight);
            updraftButton?.onClick.RemoveListener(TriggerUpdraft);
            windDirectionButton?.onClick.RemoveListener(FlipWindDirection);
            windPlatformButton?.onClick.RemoveListener(SpawnWindPlatform);
            haetaeButton?.onClick.RemoveListener(SpawnHaetae);
            vfxQualityButton?.onClick.RemoveListener(CycleVfxQuality);
        }

        public static bool IsPointerOverItemTestControls(Vector2 screenPosition)
        {
            if (Instance == null) return false;
            bool overToggle = Instance.debugToggleButton != null &&
                              Instance.debugToggleButton.gameObject.activeInHierarchy &&
                              RectTransformUtility.RectangleContainsScreenPoint(
                                  Instance.debugToggleButton.transform as RectTransform,
                                  screenPosition, null);
            bool overOpenPanel = Instance.debugPanel != null &&
                                 Instance.debugPanel.gameObject.activeInHierarchy &&
                                 RectTransformUtility.RectangleContainsScreenPoint(
                                     Instance.debugPanel, screenPosition, null);
            return overToggle || overOpenPanel;
        }

        void UseInkDrop() => ApplyDebugItem(ItemType.InkDrop);
        void UseGoldenBrush() => ApplyDebugItem(ItemType.GoldenBrush);
        void UseInkShield() => ApplyDebugItem(ItemType.InkShield);
        void UseInkClone() => ApplyDebugItem(ItemType.InkClone);
        void MoveToHeight(int height) => GameManager.Instance?.DebugTeleportToHeight(height);
        void MoveToStartHeight() => MoveToHeight(0);
        void MoveToWindHeight() => MoveToHeight(250);
        void MoveToRainHeight() => MoveToHeight(500);
        void MoveToGorgeHeight() => MoveToHeight(750);
        void TriggerUpdraft()
        {
            MarkDebugRun();
            WindWeatherController.Instance?.DebugTriggerUpdraft();
        }

        void FlipWindDirection()
        {
            MarkDebugRun();
            WindWeatherController.Instance?.DebugFlipDirection();
        }

        void SpawnWindPlatform()
        {
            MarkDebugRun();
            RestPlatformSpawner.Instance?.DebugSpawnWindNearPlayer();
        }

        void SpawnHaetae()
        {
            MarkDebugRun();
            ObstacleSpawner.Instance?.DebugSpawnHaetae();
        }

        void CycleVfxQuality()
        {
            VfxRuntimeMonitor.Instance?.CycleQualityForDebug();
            RefreshVfxDebugStats(true);
        }

        static void ApplyDebugItem(ItemType type)
        {
            MarkDebugRun();
            ItemEffect.Apply(type);
        }

        static void MarkDebugRun()
        {
            if (GameManager.DebugToolsAvailable)
                ScoreManager.Instance?.InvalidateCurrentRunForRecords();
        }

        void SetDebugToolsAvailable(bool _)
        {
            if (itemTestControls != null)
                itemTestControls.gameObject.SetActive(false);
            if (debugPanel != null)
                debugPanel.gameObject.SetActive(false);
        }

        void ToggleDebugPanel()
        {
            if (debugPanel != null)
            {
                debugPanel.gameObject.SetActive(!debugPanel.gameObject.activeSelf);
                if (debugPanel.gameObject.activeSelf)
                    RefreshVfxDebugStats(true);
            }
        }

        void ToggleInvincible()
        {
            GameManager.Instance?.ToggleDebugInvincible();
            RefreshInvincibleButton();
        }

        void RefreshInvincibleButton()
        {
            bool enabled = GameManager.Instance != null && GameManager.Instance.DebugInvincible;
            if (invincibleLabel != null)
                invincibleLabel.text = enabled ? "무적 ON" : "무적 OFF";
            if (invincibleButton != null && invincibleButton.targetGraphic is Image image)
                image.color = enabled
                    ? new Color(0.95f, 0.72f, 0.2f, 0.96f)
                    : new Color(0.92f, 0.89f, 0.82f, 0.94f);
        }

        void ApplyCrispTextSettings(bool resizeItemIcons = true)
        {
            if (canvas != null) canvas.pixelPerfect = true;
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].font = InkPalette.UiFont;
            ConfigureText(heightText);
            ConfigureText(heightCaption);
            ConfigureText(bestText);
            ConfigureText(bestCaption);
            ConfigureText(invincibleLabel);
            ConfigureDebugButton(debugToggleButton, null, 27);
            ConfigureDebugButton(invincibleButton, null, 27);
            ConfigureDebugButton(mapStartButton, null, 27);
            ConfigureDebugButton(mapWindButton, null, 27);
            ConfigureDebugButton(mapRainButton, null, 27);
            ConfigureDebugButton(mapGorgeButton, null, 27);
            ConfigureDebugButton(updraftButton, "상승기류", 27);
            ConfigureDebugButton(windDirectionButton, "풍향 전환", 27);
            ConfigureDebugButton(windPlatformButton, null, 27);
            ConfigureDebugButton(haetaeButton, "먹해태", 27);
            ConfigureDebugButton(vfxQualityButton, null, 23);
            ConfigureDebugButton(inkDropButton, null, 26);
            ConfigureDebugButton(goldenBrushButton, null, 26);
            ConfigureDebugButton(inkShieldButton, null, 26);
            ConfigureDebugButton(inkCloneButton, null, 26);
            var mapTitle = debugPanel != null
                ? debugPanel.Find("MapDebugTitle")?.GetComponent<Text>()
                : null;
            if (mapTitle != null)
            {
                mapTitle.font = InkPalette.UiFont;
                mapTitle.fontSize = 30;
                mapTitle.fontStyle = FontStyle.Bold;
                mapTitle.color = InkPalette.Paper;
            }
            if (vfxStatsText != null)
            {
                vfxStatsText.font = InkPalette.UiFont;
                vfxStatsText.fontSize = 21;
                vfxStatsText.fontStyle = FontStyle.Bold;
                vfxStatsText.alignment = TextAnchor.MiddleCenter;
                vfxStatsText.color = InkPalette.Paper;
                vfxStatsText.resizeTextForBestFit = true;
                vfxStatsText.resizeTextMinSize = 16;
                vfxStatsText.resizeTextMaxSize = 21;
            }
            if (resizeItemIcons)
            {
                SetItemIconNativeSize(inkDropButton);
                SetItemIconNativeSize(goldenBrushButton);
                SetItemIconNativeSize(inkShieldButton);
                SetItemIconNativeSize(inkCloneButton);
            }
        }

        /// 예전 Main 씬도 재생 순간 동일한 한지 상단 HUD 계층으로 이관한다.
        void EnsureTopHudContainer()
        {
            if (topHudRoot == null)
                topHudRoot = transform.Find("TopHudRoot") as RectTransform;
            if (topHudRoot == null)
            {
                var rootObject = new GameObject(
                    "TopHudRoot",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                topHudRoot = rootObject.GetComponent<RectTransform>();
                topHudRoot.SetParent(transform, false);
                var background = rootObject.GetComponent<Image>();
                background.sprite = InkUiTextureFactory.CreateBrushSprite();
                background.color = new Color(
                    InkPalette.Paper.r, InkPalette.Paper.g, InkPalette.Paper.b, 0.78f);
                background.raycastTarget = false;
            }

            // 작은 보조 캡션은 실제 420px 화면에서 10px 미만으로 축소된다.
            // 구형 씬에 남은 참조만 찾아 숨기고, 새 캡션은 만들지 않는다.
            if (heightCaption == null)
                heightCaption = topHudRoot.Find("HeightCaption")?.GetComponent<Text>();
            if (bestCaption == null)
                bestCaption = topHudRoot.Find("BestCaption")?.GetComponent<Text>();
        }

        /// 이전에 생성된 Main 씬의 상단 통합 체력 HUD는 숨기고, 각 캐릭터의
        /// 월드 체력 표시만 사용한다.
        void HideLegacyAggregateHealth()
        {
            Transform legacyHealth = topHudRoot != null
                ? topHudRoot.Find("HealthRoot")
                : null;
            if (legacyHealth != null)
                legacyHealth.gameObject.SetActive(false);
        }

        /// 씬을 다시 생성하지 않은 이전 Main도 첫 플레이 프레임에 새 표시를 얻는다.
        /// 원본에 한 번 붙으면 이후 먹분신은 해당 컴포넌트를 함께 복제한다.
        bool EnsurePlayerHealthBillboards()
        {
            var manager = GameManager.Instance;
            if (manager == null) return false;
            manager.GetLivingPlayersNonAlloc(playerHealthScratch);
            for (int i = 0; i < playerHealthScratch.Count; i++)
            {
                PlayerController player = playerHealthScratch[i];
                if (player == null || player.IsDead ||
                    player.GetComponent<PlayerHealthBillboard>() != null)
                    continue;
                player.gameObject.AddComponent<PlayerHealthBillboard>();
            }
            return playerHealthScratch.Count > 0;
        }

        void ApplyPolishedRuntimeLayout()
        {
            if (topHudRoot == null) return;
            ApplySafeAreaLayout();

            var background = topHudRoot.GetComponent<Graphic>();
            if (background != null)
            {
                Color paper = InkPalette.Paper;
                paper.a = 0.9f;
                background.color = paper;
                background.raycastTarget = false;
            }

            SetCaptionHidden(heightCaption);
            SetCaptionHidden(bestCaption);

            if (heightText != null)
            {
                var display = heightText.transform.parent as RectTransform;
                if (display != null)
                {
                    display.SetParent(topHudRoot, false);
                    display.anchorMin = display.anchorMax = new Vector2(0.5f, 0.5f);
                    display.pivot = new Vector2(0.5f, 0.5f);
                    display.anchoredPosition = Vector2.zero;
                    display.sizeDelta = new Vector2(320f, 118f);

                    var oldBackground = display.GetComponent<Graphic>();
                    if (oldBackground != null) oldBackground.enabled = false;
                }

                ConfigurePrimaryHudText(
                    heightText, new Vector2(0.5f, 0.5f),
                    new Vector2(315f, 84f), 60, 46);
            }

            if (bestText != null)
            {
                var bestRect = bestText.rectTransform;
                bestRect.SetParent(topHudRoot, false);
                ConfigurePrimaryHudText(
                    bestText, new Vector2(0.805f, 0.5f),
                    new Vector2(235f, 76f), 50, 38);
            }

            if (windIndicator != null)
            {
                windIndicator.transform.SetParent(topHudRoot, false);
                windIndicator.ApplyPolishedLayout();
            }

            if (newBestIndicator != null)
            {
                newBestIndicator.transform.SetParent(topHudRoot, false);
                newBestIndicator.ApplyPolishedLayout();
            }

            ApplyDebugReadabilityLayout();
        }

        static void SetCaptionHidden(Text text)
        {
            if (text == null) return;
            text.gameObject.SetActive(false);
        }

        static void ConfigurePrimaryHudText(
            Text text, Vector2 anchor, Vector2 size, int fontSize, int minimumFontSize)
        {
            if (text == null) return;
            text.gameObject.SetActive(true);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = InkPalette.Ink;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumFontSize;
            text.resizeTextMaxSize = fontSize;
            text.alignByGeometry = true;

            var outline = text.GetComponent<Outline>();
            if (outline == null)
                outline = text.gameObject.AddComponent<Outline>();
            Color ink = InkPalette.Ink;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, 0.25f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        void ApplySafeAreaLayout()
        {
            if (topHudRoot == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            Vector2 logicalSafeSize = MobileUiLayout.GetLogicalSafeSize(
                safe,
                Screen.width,
                Screen.height);
            Vector2 safeCenterOffset =
                MobileUiLayout.GetLogicalSafeCenterOffset(
                    safe,
                    Screen.width,
                    Screen.height);
            float topInset = MobileUiLayout.GetLogicalTopInset(
                safe,
                Screen.width,
                Screen.height);
            float hudScale = Mathf.Clamp(
                (logicalSafeSize.x - TopHudSideMargin * 2f) / TopHudWidth,
                0.01f,
                1f);
            topHudRoot.anchorMin = topHudRoot.anchorMax = new Vector2(0.5f, 1f);
            topHudRoot.pivot = new Vector2(0.5f, 1f);
            topHudRoot.anchoredPosition = new Vector2(
                safeCenterOffset.x,
                -(topInset + 52f));
            topHudRoot.sizeDelta = new Vector2(TopHudWidth, TopHudHeight);
            topHudRoot.localScale = Vector3.one * hudScale;

            if (itemTestControls != null)
            {
                float debugScale = Mathf.Clamp(
                    Mathf.Min(
                        (logicalSafeSize.x - 16f) / 410f,
                        (logicalSafeSize.y - 16f) / 1320f),
                    0.01f,
                    1f);
                itemTestControls.anchoredPosition = new Vector2(
                    MobileUiLayout.GetLogicalLeftInset(
                        safe,
                        Screen.width,
                        Screen.height) + 8f,
                    safeCenterOffset.y);
                itemTestControls.localScale = Vector3.one * debugScale;
            }
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        static void ConfigureText(Text text)
        {
            if (text == null) return;
            text.alignByGeometry = true;
        }

        static void SetItemIconNativeSize(Button button)
        {
            if (button == null) return;
            var icon = button.transform.Find("Icon")?.GetComponent<RawImage>();
            if (icon == null || icon.texture == null) return;
            icon.SetNativeSize();
            icon.rectTransform.sizeDelta /= 9f;
        }

        static void ConfigureDebugButton(Button button, string label, int fontSize)
        {
            var text = button != null ? button.transform.Find("Label")?.GetComponent<Text>() : null;
            if (text == null) return;
            if (!string.IsNullOrEmpty(label)) text.text = label;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 22;
            text.resizeTextMaxSize = fontSize;
            text.color = InkPalette.Ink;
        }

        void ApplyDebugReadabilityLayout()
        {
            Button[] itemButtons =
            {
                inkDropButton, goldenBrushButton, inkShieldButton,
                inkCloneButton,
            };
            for (int i = 0; i < itemButtons.Length; i++)
            {
                var buttonRect = itemButtons[i] != null
                    ? itemButtons[i].transform as RectTransform
                    : null;
                if (buttonRect != null)
                    buttonRect.sizeDelta = new Vector2(145f, 136f);
                var label = itemButtons[i] != null
                    ? itemButtons[i].transform.Find("Label") as RectTransform
                    : null;
                if (label != null)
                    label.sizeDelta = new Vector2(132f, 40f);
            }
        }

        /// 씬 빌더를 아직 다시 실행하지 않은 기존 Main 씬에서도 새 VFX 검증 버튼을
        /// 사용할 수 있게 Development Build에서만 같은 계층을 복구한다.
        void EnsureVfxDebugControls()
        {
            if (debugPanel == null) return;
            if (haetaeButton == null)
                haetaeButton = debugPanel.Find("HaetaeButton")?.GetComponent<Button>();
            if (vfxQualityButton == null)
            {
                var existing = debugPanel.Find("VfxQualityButton");
                if (existing != null)
                    vfxQualityButton = existing.GetComponent<Button>();
            }
            if (vfxStatsText == null)
                vfxStatsText = debugPanel.Find("VfxStatsText")?.GetComponent<Text>();

            if (vfxQualityButton == null)
            {
                var buttonObject = new GameObject(
                    "VfxQualityButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.SetParent(debugPanel, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(190f, -400f);
                rect.sizeDelta = new Vector2(175f, 72f);
                var image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.92f, 0.89f, 0.82f, 0.94f);
                vfxQualityButton = buttonObject.GetComponent<Button>();
                vfxQualityButton.targetGraphic = image;

                var labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(163f, 62f);
                vfxQualityLabel = labelObject.GetComponent<Text>();
                vfxQualityLabel.text = "VFX 자동";
                vfxQualityLabel.alignment = TextAnchor.MiddleCenter;
                vfxQualityLabel.raycastTarget = false;
            }
            else if (vfxQualityLabel == null)
            {
                vfxQualityLabel =
                    vfxQualityButton.transform.Find("Label")?.GetComponent<Text>();
            }

            if (haetaeButton == null)
                haetaeButton = CreateRuntimeDebugButton(
                    "HaetaeButton", "먹해태", new Vector2(22f, -438f),
                    new Vector2(145f, 72f));

            if (vfxStatsText != null) return;
            var statsObject = new GameObject(
                "VfxStatsText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            var statsRect = statsObject.GetComponent<RectTransform>();
            statsRect.SetParent(debugPanel, false);
            statsRect.anchorMin = statsRect.anchorMax = new Vector2(0.74f, 0.035f);
            statsRect.sizeDelta = new Vector2(180f, 72f);
            vfxStatsText = statsObject.GetComponent<Text>();
            vfxStatsText.text = "VFX 통계 준비 중";
            vfxStatsText.alignment = TextAnchor.MiddleCenter;
            vfxStatsText.raycastTarget = false;
        }

        Button CreateRuntimeDebugButton(
            string objectName, string label, Vector2 position, Vector2 size)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(debugPanel, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.92f, 0.89f, 0.82f, 0.94f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = size - new Vector2(12f, 10f);
            var labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.raycastTarget = false;
            ConfigureDebugButton(button, label, 27);
            return button;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                // Play 전 Game View도 실제 런타임 로비와 똑같이 보여야 한다.
                // 인게임 HUD·DEBUG는 Playing 상태에서만 표시한다.
                if (canvas != null) canvas.enabled = false;
                return;
            }

            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != Screen.safeArea)
                ApplySafeAreaLayout();

            bool visible = GameManager.Instance != null &&
                           GameManager.Instance.State == GameState.Playing;
            if (canvas != null) canvas.enabled = visible;
            windIndicator?.SetVisible(visible);
            newBestIndicator?.SetVisible(visible);
            if (!visible) return;
            if (!playerHealthBootstrapComplete)
                playerHealthBootstrapComplete = EnsurePlayerHealthBillboards();
            if (heightText == null) return;
            RefreshInvincibleButton();
            RefreshVfxDebugStats(false);

            var score = ScoreManager.Instance;
            int height = score != null ? score.Height : 0;
            if (height != lastHeight)
            {
                lastHeight = height;
                heightText.text = $"고도 {FormatHeight(height)}";
            }

            bool newBest = score != null && score.IsNewBestThisRun;
            int best = score != null
                ? (newBest ? score.DisplayBest : score.Best)
                : 0;
            if (bestText != null &&
                (best != lastBest || newBest != lastNewBest))
            {
                lastBest = best;
                lastNewBest = newBest;
                bestText.text = $"최고 {FormatHeight(best)}";
                bestText.color = newBest ? InkPalette.Red : InkPalette.Ink;
            }
        }

        static string FormatHeight(int meters)
        {
            return meters >= 10000
                ? $"{meters / 1000f:0.#}km"
                : $"{meters}m";
        }

        void RefreshVfxDebugStats(bool force)
        {
            if (!GameManager.DebugToolsAvailable) return;
            if (debugPanel == null || !debugPanel.gameObject.activeInHierarchy) return;
            if (!force && Time.unscaledTime < nextVfxStatsRefreshTime) return;
            nextVfxStatsRefreshTime = Time.unscaledTime + 0.25f;

            var monitor = VfxRuntimeMonitor.Instance;
            string automatic = monitor != null && monitor.AutomaticQualityEnabled
                ? "자동 "
                : string.Empty;
            if (vfxQualityLabel != null)
                vfxQualityLabel.text = $"VFX {automatic}{VfxQualityRuntime.Tier}";

            if (vfxStatsText == null) return;
            if (monitor == null)
            {
                vfxStatsText.text = "VFX 통계 준비 중";
                return;
            }

            int dropped = monitor.DroppedDecorativeVfx +
                          monitor.DroppedNormalVfx +
                          monitor.DroppedImportantVfx +
                          monitor.DroppedCriticalVfx;
            vfxStatsText.text =
                $"{monitor.MeasuredFps:0} FPS · 피드백 " +
                $"{monitor.ActiveLineVfx}/{monitor.ActiveSpriteVfx}\n" +
                $"먹점프 {monitor.ActiveCompositeVfx} · 피크 {monitor.PeakActiveVfx}/생략 {dropped}";
        }
    }
}
