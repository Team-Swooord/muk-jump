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
        [SerializeField] Text heightText;
        [SerializeField] Text bestText;
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

        void OnEnable()
        {
            Instance = this;
            ApplyCrispTextSettings();
            if (!Application.isPlaying) return;
            if (newBestIndicator == null)
                newBestIndicator = NewBestIndicatorView.CreateRuntime(transform);
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

        void ApplyCrispTextSettings()
        {
            if (canvas != null) canvas.pixelPerfect = true;
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].font = InkPalette.UiFont;
            ConfigureText(heightText);
            ConfigureText(bestText);
            ConfigureText(invincibleLabel);
            SetButtonLabel(updraftButton, "상승기류");
            SetButtonLabel(windDirectionButton, "풍향 전환");
            SetItemIconNativeSize(inkDropButton);
            SetItemIconNativeSize(goldenBrushButton);
            SetItemIconNativeSize(inkShieldButton);
            SetItemIconNativeSize(inkCloneButton);
            SetItemIconNativeSize(inkReserveButton);
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

        static void SetButtonLabel(Button button, string label)
        {
            var text = button != null ? button.transform.Find("Label")?.GetComponent<Text>() : null;
            if (text != null) text.text = label;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyCrispTextSettings();
                // 편집 모드에서는 로비 UI와 함께 보이게 하여 하이어라키에서 직접 배치할 수 있게 한다.
                if (canvas != null) canvas.enabled = true;
                return;
            }

            bool visible = GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby;
            if (canvas != null) canvas.enabled = visible;
            bool playing = GameManager.Instance != null &&
                           GameManager.Instance.State == GameState.Playing;
            windIndicator?.SetVisible(playing);
            newBestIndicator?.SetVisible(playing);
            if (!visible || heightText == null) return;
            RefreshInvincibleButton();

            var score = ScoreManager.Instance;
            int height = score != null ? score.Height : 0;
            heightText.text = $"고도 {height}";
            if (bestText != null)
            {
                bool newBest = score != null && score.IsNewBestThisRun;
                int best = score != null
                    ? (newBest ? score.DisplayBest : score.Best)
                    : 0;
                bestText.text = newBest ? $"신기록 {best}" : $"최고 {best}";
                bestText.color = newBest ? InkPalette.Red : InkPalette.TextMuted;
            }
        }
    }
}
