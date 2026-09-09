using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public enum LobbyMenuSelection
    {
        Start,
        Growth,
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
        public const float SecondaryAlpha = 0.9f;
        public const float LogoVisibleCenterOffsetX = 30.88f;
        public const float BrushArtworkLabelOffsetX = -87f;

        // 광고가 없을 때의 기본 위치다. 실제 배너 높이는 LobbyAdLayout이
        // 로비 콘텐츠 루트에 동적으로 반영한다.
        public static readonly Vector2 RecordAnchor = new(RecordRailX, 0.865f);
        public static readonly Vector2 RecordPosition = new(77f, -12f);
        // 긴 오른쪽 붓꼬리의 무게를 줄이도록 배경만 12만큼 왼쪽으로 보정한다.
        // 라벨은 반대로 이동해 실제 글씨 중심은 기존 화면 중앙에 유지한다.
        public static readonly Vector2 ButtonPosition = new(77f, 0f);
        public static readonly Vector2 BackgroundSize = new(610.273f, 130.157f);
        public static readonly Vector2 LabelPosition = new(BrushArtworkLabelOffsetX + 12f, -5f);
        public static readonly Vector2 LabelSize = new(400f, 80f);
        // 아이콘과 기록을 한 묶음으로 중앙 정렬하고 오른쪽 붓꼬리는 여백으로 남긴다.
        public static readonly Vector2 RecordLabelPosition = new(-48f, -5f);
        public static readonly Vector2 RecordLabelSize = new(268f, 64f);
        public const int FontSize = 46;
        public const string LeaderboardIconResourcePath = "MukJump/UI/Common/settings_icon_rank_v1";
        public static readonly Vector2 LeaderboardIconSize = new(72f, 72f);
        public static readonly Vector2 LeaderboardPosition = new(-182f, -5f);

        public static readonly Vector2 StartAnchor = new(MenuRailX, 0.46f);
        public static readonly Vector2 GrowthAnchor = new(MenuRailX, 0.385f);
        public static readonly Vector2 OptionsAnchor = new(MenuRailX, 0.31f);

        public static void ApplyRecord(Text label)
        {
            if (label == null) return;
            InkLocalizedText.Bind(label);
            if (label.transform.parent is RectTransform background)
            {
                background.anchorMin = background.anchorMax = RecordAnchor;
                background.pivot = new Vector2(0.5f, 0.5f);
                background.anchoredPosition = RecordPosition;
                background.sizeDelta = BackgroundSize;
            }

            ApplyLabel(
                label,
                label.GetComponent<InkLocalizedText>().SourceText,
                Color.white,
                RecordLabelPosition,
                RecordLabelSize);
            label.fontSize = FontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 32;
            label.resizeTextMaxSize = FontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public static Button EnsureLeaderboardShortcut(Text recordLabel)
        {
            if (recordLabel == null || recordLabel.transform.parent is not RectTransform record)
                return null;
            // 저장된 구 씬의 분리형 아이콘은 숨기고 기록 붓패널 하나만 누르게 한다.
            Transform legacyShortcut = record.Find("LeaderboardButton");
            if (legacyShortcut != null)
                legacyShortcut.gameObject.SetActive(false);

            var button = record.GetComponent<Button>() ?? record.gameObject.AddComponent<Button>();
            button.enabled = true;
            button.interactable = true;
            Graphic background = ResolveLegacyInkBackground(button);
            if (background == null)
            {
                var hitArea = record.gameObject.AddComponent<Image>();
                hitArea.color = Color.clear;
                background = hitArea;
            }
            InkUiStyle.ConfigureButton(button, background);
            ApplyRecord(recordLabel);

            var icon = record.Find("LeaderboardIcon")?.GetComponent<Image>();
            if (icon == null)
            {
                icon = new GameObject("LeaderboardIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(record, false);
            }
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = LeaderboardPosition;
            icon.rectTransform.sizeDelta = LeaderboardIconSize;
            icon.sprite = Resources.Load<Sprite>(LeaderboardIconResourcePath);
            icon.enabled = icon.sprite != null;
            // 원화의 밝은 한지 잎이 먹바탕 위에 남아 별도 배지 배경 없이 읽힌다.
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
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

            Graphic background = ResolveLegacyInkBackground(button);
            InkUiStyle.ConfigureButton(button, background);
            ApplySelectionEmphasis(button, primary);

            Text text = button.transform.Find("Label")?.GetComponent<Text>();
            if (text == null)
                text = button.GetComponentInChildren<Text>(true);
            ApplyLabel(
                text,
                label,
                InkPalette.TextLight,
                LabelPosition,
                LabelSize);
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
            // 글자는 항상 선명하게 두고 먹물 배경의 알파만 단계에 따라 낮춘다.
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.ignoreParentGroups = false;

            Graphic background = button.targetGraphic ??
                                 button.GetComponent<Graphic>();
            if (background == null) return;
            Color color = background.color;
            color.a = selected ? PrimaryAlpha : SecondaryAlpha;
            background.color = color;
        }

        static Graphic ResolveLegacyInkBackground(Button button)
        {
            RawImage legacyBackground = button.GetComponent<RawImage>();
            if (legacyBackground != null)
            {
                legacyBackground.enabled = true;
                legacyBackground.raycastTarget = true;
                button.targetGraphic = legacyBackground;

                // 같은 플레이 세션에서 한지 스타일이 이미 생성됐어도 즉시 숨겨
                // 시작·성장·옵션 세 버튼만 예전 먹물 원본으로 되돌린다.
                Transform hanjiBorder = button.transform.Find("HanjiBorder");
                if (hanjiBorder != null)
                    hanjiBorder.gameObject.SetActive(false);
                return legacyBackground;
            }

            return button.targetGraphic ?? button.GetComponent<Graphic>();
        }

        static void ApplyLabel(
            Text text,
            string value,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            if (text == null) return;
            InkLocalizedText.SetSource(text, value);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            InkUiStyle.ApplyButtonLabel(text, FontSize);
            text.color = color;
        }
    }
}
