using UnityEngine;

namespace MukJump.Core
{
    /// 로비 여백의 iOS 구매 입구. 권한·가격·결제 처리는 기존 구매창에 맡긴다.
    public sealed class InkPremiumLobbyEntry : MonoBehaviour
    {
        [SerializeField] LobbyView lobby;
        [SerializeField] UnityEngine.UI.Image ink;
        [SerializeField] UnityEngine.UI.Text label;
        CanvasGroup group;
        bool? displayedOwned;
        public UnityEngine.UI.Button Button => GetComponent<UnityEngine.UI.Button>();
        void OnEnable() { Button.onClick.AddListener(Open); Refresh(); }
        void OnDisable() { Button.onClick.RemoveListener(Open); }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null || label == null) return;
            bool supported = InkPremiumAppearance.IsSupported;
            group.alpha = supported ? 1 : 0;
            group.interactable = group.blocksRaycasts = supported;
            if (!supported) return;
            if (ink.sprite == null) ink.sprite = InkUiTextureFactory.CreateBlobSprite();
            bool owned = InkPremiumAppearance.IsOwned;
            if (displayedOwned != owned)
            {
                displayedOwned = owned;
                InkLocalizedText.SetSource(label, owned ? "광고\n없음" : "광고\n제거");
            }
        }
        void Open()
        {
            if (!InkPremiumAppearance.IsSupported || lobby == null || !lobby.IsVisible ||
                !lobby.IsInteractive || InkPremiumShopView.IsOpen ||
                LobbyScreenNavigator.Instance != null && !LobbyScreenNavigator.Instance.CanStartGame) return;
            InkPremiumAppearance.OpenShop();
        }
        public static void Build(RectTransform safeRoot, LobbyView lobby, Sprite splash)
        {
            var root = new GameObject("PremiumLobbyEntry", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(safeRoot, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-28, 32);
            rect.sizeDelta = new Vector2(228, 196);
            var hit = root.GetComponent<UnityEngine.UI.Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var wash = Image("InkBleed", rect, new Vector2(218, 184));
            wash.color = InkPalette.Ink;
            var flecks = Image("InkSplash", rect, new Vector2(228, 196));
            flecks.sprite = splash; flecks.color = InkPalette.Ink;
            var text = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            text.rectTransform.SetParent(rect, false);
            text.rectTransform.sizeDelta = new Vector2(154, 112);
            InkUiStyle.ApplyReadableText(text, 44, TextAnchor.MiddleCenter, true, true);
            text.color = InkPalette.Paper; text.raycastTarget = false;
            var entry = root.AddComponent<InkPremiumLobbyEntry>();
            entry.lobby = lobby; entry.ink = wash; entry.label = text;
            InkUiStyle.ConfigureButton(entry.Button, wash);
            entry.Refresh();
        }
        static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.rectTransform.SetParent(parent, false); image.rectTransform.sizeDelta = size;
            image.raycastTarget = false; return image;
        }
    }
}
