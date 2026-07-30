using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 사용자가 직접 맞춘 최고 기록 칸을 기준으로 로비 메뉴의 시각 중심을 통일한다.
    /// 씬 빌더와 구버전 씬 런타임 보정이 이 값 하나만 공유해야 한다.
    public static class LobbyMenuLayout
    {
        public static readonly Vector2 RecordAnchor = new(0.5f, 0.94f);
        public static readonly Vector2 RecordPosition = new(89f, -12f);
        public static readonly Vector2 ButtonPosition = new(89f, 0f);
        public static readonly Vector2 BackgroundSize = new(610.273f, 130.157f);
        public static readonly Vector2 LabelPosition = new(-87f, -5f);
        public static readonly Vector2 LabelSize = new(400f, 80f);
        public const int FontSize = 37;

        public static readonly Vector2 StartAnchor = new(0.5f, 0.46f);
        public static readonly Vector2 GrowthAnchor = new(0.5f, 0.385f);
        public static readonly Vector2 CodexAnchor = new(0.5f, 0.31f);
        public static readonly Vector2 OptionsAnchor = new(0.5f, 0.235f);

        public static void ApplyRecord(Text label)
        {
            if (label == null) return;
            if (label.transform.parent is RectTransform background)
            {
                background.anchorMin = background.anchorMax = RecordAnchor;
                background.pivot = new Vector2(0.5f, 0.5f);
                background.anchoredPosition = RecordPosition;
                background.sizeDelta = BackgroundSize;
            }

            ApplyLabel(label, label.text);
        }

        public static void ApplyButton(
            Button button,
            string label,
            Vector2 anchor)
        {
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = ButtonPosition;
                rect.sizeDelta = BackgroundSize;
            }

            Graphic background = button.targetGraphic;
            if (background == null)
                background = button.GetComponent<Graphic>();
            InkUiStyle.ConfigureButton(button, background);

            Text text = button.transform.Find("Label")?.GetComponent<Text>();
            if (text == null)
                text = button.GetComponentInChildren<Text>(true);
            ApplyLabel(text, label);
        }

        static void ApplyLabel(Text text, string value)
        {
            if (text == null) return;
            text.text = value;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = LabelPosition;
            rect.sizeDelta = LabelSize;
            InkUiStyle.ApplyReadableText(
                text,
                FontSize,
                TextAnchor.MiddleCenter,
                strong: true);
        }
    }
}
