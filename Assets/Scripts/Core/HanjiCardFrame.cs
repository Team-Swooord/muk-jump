using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 성장 카드의 갈필 원화를 모든 작은 한지 패널에 공유한다. 장식은 입력을 받지 않는다.
    [DisallowMultipleComponent]
    public sealed class HanjiCardFrame : MonoBehaviour
    {
        GrowthRingFrameGraphic frame;

        public static void Ensure(Image paper)
        {
            var owner = paper.GetComponent<HanjiCardFrame>();
            if (owner == null) owner = paper.gameObject.AddComponent<HanjiCardFrame>();
            owner.Refresh();
        }

        void OnEnable() => Refresh();
        void OnRectTransformDimensionsChange() => UpdateSize();

        void Refresh()
        {
            if (frame == null)
                frame = transform.Find("BrushFrame")?.GetComponent<GrowthRingFrameGraphic>();
            if (frame == null)
            {
                var go = new GameObject("BrushFrame", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(GrowthRingFrameGraphic));
                go.transform.SetParent(transform, false);
                frame = go.GetComponent<GrowthRingFrameGraphic>();
            }
            frame.sprite = Resources.Load<Sprite>("MukJump/UI/PermanentGrowth/pg_selected_ring");
            frame.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .26f);
            frame.raycastTarget = false;
            frame.enabled = frame.sprite != null;
            frame.transform.SetAsFirstSibling();
            UpdateSize();
        }

        void UpdateSize()
        {
            if (frame == null || !(transform is RectTransform parent)) return;
            float shortest = Mathf.Max(1f, Mathf.Min(parent.rect.width, parent.rect.height));
            float scale = Mathf.Clamp(shortest / 240f, .25f, 1f);
            // 9-slice 종이의 투명 여백 안으로 테두리를 넣어 종이 밖에 따로 뜨지 않게 한다.
            float inset = Mathf.Clamp(shortest * .17f, 16f, 24f);
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
            frame.BrushScale = scale;
        }
    }
}
