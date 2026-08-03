using System;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 영구 성장 화면의 서예 글꼴이 9:16 모바일 화면에서 뭉개지지 않도록
    /// 최소 크기와 얇은 합성 굵기만 보강한다. 위치와 크기는 View가 소유한다.
    public static class PermanentGrowthTypography
    {
        public static int Resolve(string elementName, int requestedSize)
        {
            int minimum = elementName switch
            {
                "Balance" => 44,
                "BranchTitle" => 34,
                "ActionName" => 32,
                "ActionUsage" => 27,
                "ActionStatus" => 28,
                _ => requestedSize,
            };
            return Math.Max(requestedSize, minimum);
        }

        public static void ApplyLayout(Text text, string elementName)
        {
            if (text == null || text.rectTransform == null)
                return;

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            ApplyVisualWeight(text, elementName);
            ApplyEnhanceButtonMinimum(text, elementName);
        }

        static void ApplyVisualWeight(Text text, string elementName)
        {
            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
                outline = text.gameObject.AddComponent<Outline>();

            float distance = elementName switch
            {
                "Balance" => 2f,
                "BranchTitle" => 0.65f,
                "ActionName" => 0.55f,
                "ActionBranch" => 0.35f,
                "ActionDescription" => 0.35f,
                "ActionCurrentEffect" => 0.4f,
                "ActionUsage" => 0.35f,
                "ActionNextEffect" => 0.45f,
                "ActionStatus" => 0.4f,
                _ => 1f,
            };
            float alpha = elementName switch
            {
                "Balance" => 0.8f,
                "BranchTitle" => 0.28f,
                "ActionName" => 0.24f,
                "ActionBranch" => 0.18f,
                "ActionDescription" => 0.2f,
                "ActionCurrentEffect" => 0.2f,
                "ActionUsage" => 0.18f,
                "ActionNextEffect" => 0.22f,
                "ActionStatus" => 0.2f,
                _ => 0.64f,
            };
            Color color = text.color;
            outline.effectColor = new Color(
                color.r,
                color.g,
                color.b,
                alpha);
            outline.effectDistance =
                new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        static void ApplyEnhanceButtonMinimum(
            Text text,
            string elementName)
        {
            if (elementName != "Label" ||
                text.transform.parent is not RectTransform button ||
                button.name != "EnhanceButton")
                return;

            Vector2 size = button.sizeDelta;
            size.x = Mathf.Max(250f, size.x);
            size.y = Mathf.Max(104f, size.y);
            button.sizeDelta = size;
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta =
                new Vector2(size.x - 36f, size.y - 18f);
            text.alignment = TextAnchor.MiddleCenter;
        }
    }
}
