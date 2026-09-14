using System.Globalization;
using UnityEngine;

namespace MukJump.Core
{
    /// 시간 아래의 읽기 전용 능력 슬롯. 작은 장식 그림과 실제 재사용 대기시간만 표시한다.
    [DisallowMultipleComponent]
    public sealed class InkSkinCooldownHud : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] UnityEngine.UI.Image icon, cooldown;
        [SerializeField] UnityEngine.UI.Text seconds;
        InkSkinId displayedSkin = (InkSkinId)(-1);
        int displayedSeconds = -1;

        public bool Visible => group != null && group.alpha > 0 && isActiveAndEnabled;
        public UnityEngine.UI.Image CostumeIcon => icon;
        public UnityEngine.UI.Image CooldownOverlay => cooldown;
        public UnityEngine.UI.Text SecondsLabel => seconds;

        public static void Ensure(RectTransform topHud)
        {
            if (topHud.Find("SkinAbilityCooldown") != null) return;
            var root = Rect("SkinAbilityCooldown", topHud, new Vector2(0, -135), new Vector2(84, 84));
            root.anchorMin = root.anchorMax = new Vector2(.5f, 0);
            var view = root.gameObject.AddComponent<InkSkinCooldownHud>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.alpha = 0;
            view.group.interactable = view.group.blocksRaycasts = false;
            var paper = Rect("Paper", root, Vector2.zero, root.sizeDelta).gameObject.AddComponent<UnityEngine.UI.Image>();
            InkUiStyle.ConfigureHanjiSurface(paper);
            paper.raycastTarget = false;
            view.icon = Rect("Costume", root, Vector2.zero, new Vector2(64, 64)).gameObject.AddComponent<UnityEngine.UI.Image>();
            view.icon.preserveAspect = true;
            view.icon.raycastTarget = false;
            view.cooldown = Rect("CooldownSweep", root, Vector2.zero, new Vector2(76, 76)).gameObject.AddComponent<UnityEngine.UI.Image>();
            view.cooldown.sprite = paper.sprite;
            view.cooldown.type = UnityEngine.UI.Image.Type.Filled;
            view.cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            view.cooldown.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            view.cooldown.fillClockwise = true;
            view.cooldown.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .58f);
            view.cooldown.fillAmount = 0;
            view.cooldown.raycastTarget = false;
            view.seconds = Rect("Seconds", root, new Vector2(0, 65), new Vector2(100, 36)).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkUiStyle.ApplyReadableText(view.seconds, 32, TextAnchor.MiddleCenter, true, true);
            view.seconds.color = InkPalette.Ink;
            view.seconds.raycastTarget = false;
            view.seconds.text = "";
        }

        void OnEnable() { displayedSkin = (InkSkinId)(-1); displayedSeconds = -1; }
        void OnDisable() { if (group != null) group.alpha = 0; }

        void LateUpdate()
        {
            if (group == null) return;
            var growth = RunGrowthController.Instance;
            bool visible = GameManager.Instance != null && GameManager.Instance.IsGameplayTicking &&
                MobileApplicationLifecycle.IsApplicationActive && growth != null &&
                InkSkinCatalog.HasAbility(growth.SkinSnapshot) &&
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true;
            group.alpha = visible ? 1 : 0;
            if (!visible) return;
            if (displayedSkin != growth.SkinSnapshot)
            {
                displayedSkin = growth.SkinSnapshot;
                icon.sprite = InkSkinCatalog.Sprite(displayedSkin);
            }
            float duration = growth.EquippedAbilityCooldownDuration;
            float remaining = Mathf.Max(0, growth.EquippedAbilityCooldownRemaining);
            cooldown.fillAmount = duration > 0 ? Mathf.Clamp01(remaining / duration) : 0;
            int value = Mathf.CeilToInt(remaining);
            if (displayedSeconds == value) return;
            displayedSeconds = value;
            // 상시·일회성 능력에는 존재하지 않는 쿨타임을 만들지 않는다. 준비되면 숫자가 사라진다.
            seconds.text = value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "";
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
