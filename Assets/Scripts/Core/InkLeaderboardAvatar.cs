using UnityEngine;

namespace MukJump.Core
{
    /// 리더보드 행 전용 아이콘. 순위 데이터의 의상만 그리며 해금/장착 상태는 변경하지 않는다.
    public sealed class InkLeaderboardAvatar
    {
        public RectTransform Root { get; }
        readonly RectTransform artwork;
        readonly UnityEngine.UI.Image body;
        readonly InkAccessoryImage accessory;
        InkSkinId shown = (InkSkinId)(-1);

        public InkLeaderboardAvatar(Transform parent, string name, Vector2 position, float iconSize, Sprite idle)
        {
            Root = Rect(name, parent, Vector2.one * iconSize);
            Root.anchoredPosition = position;
            artwork = Rect("Artwork", Root, Vector2.zero);
            body = Rect("OriginalInkBody", artwork, Vector2.one).gameObject.AddComponent<UnityEngine.UI.Image>();
            body.sprite = idle; body.preserveAspect = true; body.raycastTarget = false;
            accessory = Rect("Accessory", artwork, Vector2.one).gameObject.AddComponent<InkAccessoryImage>();
            accessory.preserveAspect = true; accessory.raycastTarget = false;
            Root.gameObject.SetActive(false);
        }

        public void Set(MukJumpLeaderboardEntry entry)
        {
            Root.gameObject.SetActive(entry != null);
            if (entry == null || entry.EquippedSkin == shown) return;
            shown = entry.EquippedSkin;
            accessory.sprite = InkSkinCatalog.Sprite(shown);
            accessory.enabled = accessory.sprite != null;
            body.enabled = body.sprite != null;
            if (InkSkinCatalog.BehindBody(shown)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            var bounds = LobbySkinPreview.CostumeBounds(shown, accessory.sprite);
            float size = Mathf.Min(Root.rect.width / bounds.width, Root.rect.height / bounds.height) * .94f;
            body.rectTransform.sizeDelta = Vector2.one * size;
            artwork.anchoredPosition = -bounds.center * size;
            if (!accessory.enabled) return;
            float width = InkSkinCatalog.Width(shown) * size;
            accessory.rectTransform.sizeDelta = new Vector2(width, width * accessory.sprite.rect.height / accessory.sprite.rect.width);
            accessory.rectTransform.anchoredPosition = InkSkinCatalog.Offset(shown) * size;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
