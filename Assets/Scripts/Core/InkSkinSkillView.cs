using UnityEngine;

namespace MukJump.Core
{
    /// 바람부채 전용 버튼. 기존 먹 게이지 위 안전영역에 고정하며 버튼만 입력을 소비한다.
    public sealed class InkSkinSkillView : MonoBehaviour
    {
        public static InkSkinSkillView Instance { get; private set; }
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform safe, control;
        [SerializeField] UnityEngine.UI.Button button;
        [SerializeField] UnityEngine.UI.Image fill;
        [SerializeField] UnityEngine.UI.Text remaining;
        public bool Visible => canvas != null && canvas.enabled;
        public UnityEngine.UI.Button SkillButton => button;
        int lastSeconds = -1;

        void OnEnable()
        {
            Instance = this; lastSeconds = -1;
            if (button != null) { button.onClick.RemoveListener(Activate); button.onClick.AddListener(Activate); }
        }
        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) canvas.enabled = false;
            if (button != null) button.onClick.RemoveListener(Activate);
        }

        void LateUpdate()
        {
            var growth = RunGrowthController.Instance;
            bool visible = GameManager.Instance != null && GameManager.Instance.IsGameplayTicking &&
                MobileApplicationLifecycle.IsApplicationActive && growth != null && growth.HasActiveSkinSkill &&
                MukJumpAccountRuntime.Instance?.BlocksGameplayForAccountSync != true;
            if (visible && canvas == null) Build();
            if (canvas == null) return;
            canvas.enabled = visible;
            if (!visible) return;
            MobileUiLayout.ApplySafeArea(safe, MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);
            control.localScale = Vector3.one * Mathf.Min(1, safe.rect.width / 820f);
            button.interactable = growth.CanUseActiveSkinSkill;
            int seconds = Mathf.CeilToInt(growth.SkinSkillCooldownRemaining);
            if (seconds == 0 && !button.interactable) seconds = -2;
            if (seconds != lastSeconds)
            {
                lastSeconds = seconds;
                InkLocalizedText.SetSource(remaining, seconds > 0 ? seconds.ToString() : seconds == -2 ? "대기" : "준비");
            }
            fill.fillAmount = 1 - Mathf.Clamp01(growth.SkinSkillCooldownRemaining / RunGrowthController.SkinSkillCooldownSeconds);
        }

        void Build()
        {
            var root = new GameObject("SkinSkillCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            control = Rect("WindLeap", safe, new Vector2(-28, 215), new Vector2(200, 216));
            control.anchorMin = control.anchorMax = control.pivot = new Vector2(1, 0);
            var paper = control.gameObject.AddComponent<UnityEngine.UI.Image>();
            InkUiStyle.ConfigureHanjiSurface(paper); paper.raycastTarget = true;
            button = control.gameObject.AddComponent<InkSkinSkillButton>();
            button.targetGraphic = paper; button.colors = InkUiStyle.ActionButtonColors();
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(Activate);
            var icon = Rect("Fan", control, new Vector2(0, 39), new Vector2(122, 104)).gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sprite = InkSkinCatalog.Sprite(InkSkinId.WindFan); icon.preserveAspect = true; icon.raycastTarget = false;
            Label("SkillName", control, "도약", 44, new Vector2(0, -38), new Vector2(178, 58));
            var track = Rect("CooldownTrack", control, new Vector2(0, -82), new Vector2(154, 9)).gameObject.AddComponent<UnityEngine.UI.Image>();
            track.sprite = InkUiTextureFactory.CreateBrushSprite(); track.color = new Color(.11f,.10f,.09f,.15f); track.raycastTarget = false;
            fill = Rect("CooldownInk", track.transform, Vector2.zero, new Vector2(154, 9)).gameObject.AddComponent<UnityEngine.UI.Image>();
            fill.sprite = track.sprite; fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            fill.color = InkPalette.Ink; fill.raycastTarget = false;
            remaining = Label("CooldownSeconds", control, "준비", 29, new Vector2(0, -132), new Vector2(200, 42));
            lastSeconds = -1;
        }

        void Activate()
        {
            PointerInput.SuppressUntilRelease();
            RunGrowthController.Instance?.TryUseActiveSkinSkill();
        }

        public static bool IsPointerOverControls(Vector2 point) => Instance != null && Instance.Visible &&
            RectTransformUtility.RectangleContainsScreenPoint(Instance.control, point, null);

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static UnityEngine.UI.Text Label(string name, Transform parent, string source, int size, Vector2 pos, Vector2 bounds)
        {
            var text = Rect(name, parent, pos, bounds).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkLocalizedText.SetSource(text, source); InkUiStyle.ApplyReadableText(text, size, TextAnchor.MiddleCenter, true, true);
            text.color = InkPalette.Ink; text.raycastTarget = false; return text;
        }
    }
}
