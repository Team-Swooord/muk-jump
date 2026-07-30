using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public enum LobbyMenuSelection
    {
        Start,
        Growth,
        Codex,
        Options,
    }

    /// 사용자가 직접 맞춘 최고 기록 칸을 기준으로 로비 메뉴의 시각 중심을 통일한다.
    /// 씬 빌더와 구버전 씬 런타임 보정이 이 값 하나만 공유해야 한다.
    public static class LobbyMenuLayout
    {
        // 버튼 PNG 안쪽의 비대칭 보정은 유지하되, 메뉴 글자의 최종 시각 중심은
        // 기기 화면 중앙과 일치시킨다.
        public const float MenuRailX = 0.5f;
        public const float RecordRailX = MenuRailX;
        public const float PrimaryAlpha = 1f;
        public const float SecondaryAlpha = 0.78f;

        public static readonly Vector2 RecordAnchor = new(RecordRailX, 0.94f);
        public static readonly Vector2 RecordPosition = new(89f, -12f);
        // 비대칭 붓 원본은 배경을 오른쪽, 라벨을 왼쪽으로 같은 양만큼 보정한다.
        // 두 값의 합을 거의 0으로 유지해 실제 라벨 중심은 화면 중앙에 둔다.
        public static readonly Vector2 ButtonPosition = new(89f, 0f);
        public static readonly Vector2 BackgroundSize = new(610.273f, 130.157f);
        public static readonly Vector2 LabelPosition = new(-87f, -5f);
        public static readonly Vector2 LabelSize = new(400f, 80f);
        public const int FontSize = 46;

        public static readonly Vector2 StartAnchor = new(MenuRailX, 0.46f);
        public static readonly Vector2 GrowthAnchor = new(MenuRailX, 0.385f);
        public static readonly Vector2 CodexAnchor = new(MenuRailX, 0.31f);
        public static readonly Vector2 OptionsAnchor = new(MenuRailX, 0.235f);

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

            ApplyLabel(label, label.text, Color.white);
        }

        public static void ApplyButton(
            Button button,
            string label,
            Vector2 anchor)
        {
            ApplyButton(button, label, anchor, label == "시작");
        }

        public static void ApplyButton(
            Button button,
            string label,
            Vector2 anchor,
            bool primary)
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
            ApplySelectionEmphasis(button, primary);

            Text text = button.transform.Find("Label")?.GetComponent<Text>();
            if (text == null)
                text = button.GetComponentInChildren<Text>(true);
            ApplyLabel(text, label, InkPalette.TextLight);
        }

        public static void ApplySelectionEmphasis(
            Button button,
            bool selected)
        {
            if (button == null) return;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();
            // CanvasGroup으로 흐리면 흰 글자도 함께 한지색에 섞여 읽기 어려워진다.
            // 글자는 항상 선명하게 두고 붓 배경의 알파만 단계에 따라 낮춘다.
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.ignoreParentGroups = false;

            Graphic background = button.targetGraphic;
            if (background == null)
                background = button.GetComponent<Graphic>();
            if (background == null) return;
            Color color = background.color;
            color.a = selected ? PrimaryAlpha : SecondaryAlpha;
            background.color = color;
        }

        static void ApplyLabel(Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = LabelPosition;
            rect.sizeDelta = LabelSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            InkUiStyle.ApplyButtonLabel(text, FontSize);
            text.color = color;
        }
    }
}
