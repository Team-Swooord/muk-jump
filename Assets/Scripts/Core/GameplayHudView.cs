using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using MukJump.Items;
using MukJump.Drawing;

namespace MukJump.Core
{
    /// 하이어라키에서 직접 편집할 수 있는 플레이 중 고도 HUD.
    [ExecuteAlways]
    public class GameplayHudView : MonoBehaviour
    {
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
        [SerializeField] Button inkReserveButton;
        [SerializeField] Button mapStartButton;
        [SerializeField] Button mapWindButton;
        [SerializeField] Button mapRainButton;
        [SerializeField] Button mapGorgeButton;
        [FormerlySerializedAs("restPlatformButton")]
        [SerializeField] Button updraftButton;
        [SerializeField] Button windDirectionButton;
        [SerializeField] Button windPlatformButton;
        [SerializeField] WindIndicatorView windIndicator;
        [SerializeField] NewBestIndicatorView newBestIndicator;

        public static GameplayHudView Instance { get; private set; }

        int lastHeight = int.MinValue;
        int lastBest = int.MinValue;
        bool lastNewBest;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;

        void OnEnable()
        {
            Instance = this;
            lastHeight = int.MinValue;
            lastBest = int.MinValue;
            lastNewBest = false;
            ApplyCrispTextSettings();
            if (!Application.isPlaying) return;
            if (windIndicator == null)
                windIndicator = GetComponentInChildren<WindIndicatorView>(true);
            if (newBestIndicator == null)
                newBestIndicator = GetComponentInChildren<NewBestIndicatorView>(true);
            EnsureTopHudContainer();
            if (newBestIndicator == null)
                newBestIndicator = NewBestIndicatorView.CreateRuntime(
                    topHudRoot != null ? topHudRoot : transform);
            ApplyPolishedRuntimeLayout();
            debugToggleButton?.onClick.AddListener(ToggleDebugPanel);
            invincibleButton?.onClick.AddListener(ToggleInvincible);
            inkDropButton?.onClick.AddListener(UseInkDrop);
            goldenBrushButton?.onClick.AddListener(UseGoldenBrush);
            inkShieldButton?.onClick.AddListener(UseInkShield);
            inkCloneButton?.onClick.AddListener(UseInkClone);
            inkReserveButton?.onClick.AddListener(UseInkReserve);
            mapStartButton?.onClick.AddListener(() => MoveToHeight(0));
            mapWindButton?.onClick.AddListener(() => MoveToHeight(250));
            mapRainButton?.onClick.AddListener(() => MoveToHeight(500));
            mapGorgeButton?.onClick.AddListener(() => MoveToHeight(750));
            updraftButton?.onClick.AddListener(TriggerUpdraft);
            windDirectionButton?.onClick.AddListener(FlipWindDirection);
            windPlatformButton?.onClick.AddListener(SpawnWindPlatform);
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
            inkReserveButton?.onClick.RemoveListener(UseInkReserve);
            mapStartButton?.onClick.RemoveAllListeners();
            mapWindButton?.onClick.RemoveAllListeners();
            mapRainButton?.onClick.RemoveAllListeners();
            mapGorgeButton?.onClick.RemoveAllListeners();
            updraftButton?.onClick.RemoveListener(TriggerUpdraft);
            windDirectionButton?.onClick.RemoveListener(FlipWindDirection);
            windPlatformButton?.onClick.RemoveListener(SpawnWindPlatform);
        }

        public static bool IsPointerOverItemTestControls(Vector2 screenPosition)
        {
            if (Instance == null) return false;
            bool overToggle = Instance.debugToggleButton != null &&
                              RectTransformUtility.RectangleContainsScreenPoint(
                                  Instance.debugToggleButton.transform as RectTransform,
                                  screenPosition, null);
            bool overOpenPanel = Instance.debugPanel != null &&
                                 Instance.debugPanel.gameObject.activeInHierarchy &&
                                 RectTransformUtility.RectangleContainsScreenPoint(
                                     Instance.debugPanel, screenPosition, null);
            return overToggle || overOpenPanel;
        }

        void UseInkDrop() => ItemEffect.Apply(ItemType.InkDrop);
        void UseGoldenBrush() => ItemEffect.Apply(ItemType.GoldenBrush);
        void UseInkShield() => ItemEffect.Apply(ItemType.InkShield);
        void UseInkClone() => ItemEffect.Apply(ItemType.InkClone);
        void UseInkReserve() => ItemEffect.Apply(ItemType.InkReserve);
        void MoveToHeight(int height) => GameManager.Instance?.DebugTeleportToHeight(height);
        void TriggerUpdraft() => WindWeatherController.Instance?.DebugTriggerUpdraft();
        void FlipWindDirection() => WindWeatherController.Instance?.DebugFlipDirection();
        void SpawnWindPlatform() => RestPlatformSpawner.Instance?.DebugSpawnWindNearPlayer();

        void ToggleDebugPanel()
        {
            if (debugPanel != null)
                debugPanel.gameObject.SetActive(!debugPanel.gameObject.activeSelf);
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
            ConfigureDebugButton(inkDropButton, null, 26);
            ConfigureDebugButton(goldenBrushButton, null, 26);
            ConfigureDebugButton(inkShieldButton, null, 26);
            ConfigureDebugButton(inkCloneButton, null, 26);
            ConfigureDebugButton(inkReserveButton, null, 26);
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
            if (resizeItemIcons)
            {
                SetItemIconNativeSize(inkDropButton);
                SetItemIconNativeSize(goldenBrushButton);
                SetItemIconNativeSize(inkShieldButton);
                SetItemIconNativeSize(inkCloneButton);
                SetItemIconNativeSize(inkReserveButton);
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

            if (heightCaption == null)
                heightCaption = FindOrCreateHudText("HeightCaption", "고도");
            if (bestCaption == null)
                bestCaption = FindOrCreateHudText("BestCaption", "최고");
        }

        void ApplyPolishedRuntimeLayout()
        {
            if (topHudRoot == null) return;
            ApplySafeAreaLayout();

            var background = topHudRoot.GetComponent<Graphic>();
            if (background != null)
            {
                Color paper = InkPalette.Paper;
                paper.a = 0.78f;
                background.color = paper;
                background.raycastTarget = false;
            }

            if (heightText != null)
            {
                var display = heightText.transform.parent as RectTransform;
                if (display != null)
                {
                    display.SetParent(topHudRoot, false);
                    display.anchorMin = display.anchorMax = new Vector2(0.5f, 0.5f);
                    display.pivot = new Vector2(0.5f, 0.5f);
                    display.anchoredPosition = Vector2.zero;
                    display.sizeDelta = new Vector2(360f, 108f);

                    var oldBackground = display.GetComponent<Graphic>();
                    if (oldBackground != null) oldBackground.enabled = false;
                }

                var heightRect = heightText.rectTransform;
                heightRect.anchorMin = heightRect.anchorMax = new Vector2(0.5f, 0.33f);
                heightRect.anchoredPosition = new Vector2(0f, -3f);
                heightRect.sizeDelta = new Vector2(340f, 66f);
                heightText.fontSize = 56;
                heightText.fontStyle = FontStyle.Normal;
                heightText.alignment = TextAnchor.MiddleCenter;
                heightText.color = InkPalette.TextDark;
            }

            ConfigureHudCaption(
                heightCaption, new Vector2(0.5f, 0.78f), new Vector2(110f, 32f), "고도");

            if (bestText != null)
            {
                var bestRect = bestText.rectTransform;
                bestRect.SetParent(topHudRoot, false);
                bestRect.anchorMin = bestRect.anchorMax = new Vector2(0.82f, 0.35f);
                bestRect.pivot = new Vector2(0.5f, 0.5f);
                bestRect.anchoredPosition = Vector2.zero;
                bestRect.sizeDelta = new Vector2(220f, 50f);
                bestText.fontSize = 36;
                bestText.fontStyle = FontStyle.Normal;
                bestText.alignment = TextAnchor.MiddleCenter;
                bestText.color = InkPalette.TextDark;
            }

            ConfigureHudCaption(
                bestCaption, new Vector2(0.82f, 0.75f), new Vector2(150f, 32f), "최고");

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

        Text FindOrCreateHudText(string name, string value)
        {
            var existing = topHudRoot.Find(name)?.GetComponent<Text>();
            if (existing != null) return existing;

            var go = new GameObject(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(topHudRoot, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.raycastTarget = false;
            return text;
        }

        static void ConfigureHudCaption(
            Text text, Vector2 anchor, Vector2 size, string value)
        {
            if (text == null) return;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = 24;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = InkPalette.TextDark;
            text.raycastTarget = false;
        }

        void ApplySafeAreaLayout()
        {
            if (topHudRoot == null) return;
            float scale = Screen.height > 0 ? 1920f / Screen.height : 1f;
            float topInset = Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) * scale;
            float logicalWidth = Mathf.Max(720f, Screen.width * scale);
            float hudScale = Mathf.Min(1f, (logicalWidth - 64f) / 1016f);
            topHudRoot.anchorMin = topHudRoot.anchorMax = new Vector2(0.5f, 1f);
            topHudRoot.pivot = new Vector2(0.5f, 1f);
            topHudRoot.anchoredPosition = new Vector2(0f, -(topInset + 52f));
            topHudRoot.sizeDelta = new Vector2(1016f, 124f);
            topHudRoot.localScale = Vector3.one * hudScale;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        static void ConfigureText(Text text)
        {
            if (text == null) return;
            text.resizeTextForBestFit = false;
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
                inkCloneButton, inkReserveButton,
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

        void Update()
        {
            if (!Application.isPlaying)
            {
                // 편집 모드에서는 로비 UI와 함께 보이게 하여 하이어라키에서 직접 배치할 수 있게 한다.
                if (canvas != null) canvas.enabled = true;
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
            if (!visible || heightText == null) return;
            RefreshInvincibleButton();

            var score = ScoreManager.Instance;
            int height = score != null ? score.Height : 0;
            if (height != lastHeight)
            {
                lastHeight = height;
                heightText.text = $"{height}m";
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
                bestText.text = $"{best}m";
                bestText.color = newBest ? InkPalette.Red : InkPalette.TextDark;
                if (bestCaption != null)
                    bestCaption.text = newBest ? "현재 최고" : "최고";
            }
        }
    }
}
