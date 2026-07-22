using UnityEngine;
using UnityEngine.UI;
using MukJump.Items;

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
        [SerializeField] Button inkDropButton;
        [SerializeField] Button goldenBrushButton;
        [SerializeField] Button inkShieldButton;

        public static GameplayHudView Instance { get; private set; }

        void OnEnable()
        {
            Instance = this;
            ApplyCrispTextSettings();
            if (!Application.isPlaying) return;
            inkDropButton?.onClick.AddListener(UseInkDrop);
            goldenBrushButton?.onClick.AddListener(UseGoldenBrush);
            inkShieldButton?.onClick.AddListener(UseInkShield);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (!Application.isPlaying) return;
            inkDropButton?.onClick.RemoveListener(UseInkDrop);
            goldenBrushButton?.onClick.RemoveListener(UseGoldenBrush);
            inkShieldButton?.onClick.RemoveListener(UseInkShield);
        }

        public static bool IsPointerOverItemTestControls(Vector2 screenPosition)
        {
            return Instance != null && Instance.itemTestControls != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       Instance.itemTestControls, screenPosition, null);
        }

        void UseInkDrop() => ItemEffect.Apply(ItemType.InkDrop);
        void UseGoldenBrush() => ItemEffect.Apply(ItemType.GoldenBrush);
        void UseInkShield() => ItemEffect.Apply(ItemType.InkShield);

        void ApplyCrispTextSettings()
        {
            if (canvas != null) canvas.pixelPerfect = true;
            ConfigureText(heightText);
            ConfigureText(bestText);
            SetItemIconNativeSize(inkDropButton);
            SetItemIconNativeSize(goldenBrushButton);
            SetItemIconNativeSize(inkShieldButton);
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
            if (!visible || heightText == null) return;

            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            heightText.text = $"고도 {height}";
            if (bestText != null)
            {
                int best = ScoreManager.Instance != null ? ScoreManager.Instance.Best : 0;
                bestText.text = $"최고 {best}";
            }
        }
    }
}
