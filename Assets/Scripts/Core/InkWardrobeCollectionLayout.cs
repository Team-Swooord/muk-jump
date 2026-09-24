using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 작은 카드 줄은 기존 Grid를 유지하고 전체 보기에서만 세 범주 사이에 제목과 여백을 둔다.
    public sealed class InkWardrobeCollectionLayout : GridLayoutGroup
    {
        const float HeadingHeight = 96f, SectionGap = 56f;
        readonly int[] counts = new int[3];
        RectTransform[] headings;
        bool expanded;

        public void ConfigureSections(bool show, RectTransform[] labels)
        {
            expanded = show; headings = labels;
            System.Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < InkWardrobeEntry.Count; i++) counts[InkWardrobeEntry.At(i).Group]++;
            for (int i = 0; i < headings.Length; i++) headings[i].gameObject.SetActive(show && counts[i] > 0);
            SetDirty();
        }
        float SectionHeight(int count) => HeadingHeight +
            Mathf.CeilToInt(count / (float)constraintCount) * (cellSize.y + spacing.y) - spacing.y;

        public override void CalculateLayoutInputVertical()
        {
            if (!expanded) { base.CalculateLayoutInputVertical(); return; }
            float height = padding.vertical;
            int sections = 0;
            foreach (int count in counts)
                if (count > 0) { height += SectionHeight(count); sections++; }
            height += Mathf.Max(0, sections - 1) * SectionGap;
            SetLayoutInputForAxis(height, height, -1, 1);
        }
        public override void SetLayoutVertical()
        {
            if (!expanded) { base.SetLayoutVertical(); return; }
            float top = padding.top;
            float startX = GetStartOffset(0, constraintCount * cellSize.x + (constraintCount - 1) * spacing.x);
            int slot = 0;
            for (int group = 0; group < counts.Length; group++)
            {
                int count = counts[group]; if (count == 0) continue;
                SetChildAlongAxis(headings[group], 0, padding.left, rectTransform.rect.width - padding.horizontal);
                SetChildAlongAxis(headings[group], 1, top, 72f);
                for (int i = 0; i < count && slot < rectChildren.Count; i++, slot++)
                {
                    var card = rectChildren[slot];
                    SetChildAlongAxis(card, 0, startX + (i % constraintCount) * (cellSize.x + spacing.x), cellSize.x);
                    SetChildAlongAxis(card, 1, top + HeadingHeight + (i / constraintCount) * (cellSize.y + spacing.y), cellSize.y);
                }
                top += SectionHeight(count) + SectionGap;
            }
        }
        public float RevealTop(int browseIndex)
        {
            float top = padding.top;
            foreach (int count in counts)
            {
                if (count == 0) continue;
                if (browseIndex < count)
                {
                    int row = browseIndex / constraintCount;
                    return row == 0 ? top : top + HeadingHeight + row * (cellSize.y + spacing.y);
                }
                browseIndex -= count;
                top += SectionHeight(count) + SectionGap;
            }
            return top;
        }
    }
}
