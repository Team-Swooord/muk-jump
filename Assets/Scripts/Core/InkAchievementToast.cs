using System.Collections.Generic;
using UnityEngine;
namespace MukJump.Core
{
    /// 조작을 가로채지 않는 수묵 업적 알림. 고정 UI 하나를 큐로 재사용한다.
    public sealed class InkAchievementToast : MonoBehaviour
    {
        readonly Queue<int> queue = new();
        CanvasGroup group;
        RectTransform safe, panel;
        UnityEngine.UI.Text label;
        object owner;
        float time;
        bool showing;
        public void Enqueue(int id)
        {
            if (id <= 0 || id > InkSkinCatalog.Count + 1) return;
            if (!ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken)) Clear();
            owner = PermanentGrowthProfile.SkinAccountToken;
            if (queue.Count < InkSkinCatalog.Count + 1) queue.Enqueue(id);
        }
        void Build()
        {
            if (group != null) return;
            var root = new GameObject("AchievementToastCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 4000;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            panel = Rect("InkBanner", safe, new Vector2(-24, -210), new Vector2(630, 136));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            var brush = Rect("Brush", panel, Vector2.zero, new Vector2(660, 146)).gameObject.AddComponent<UnityEngine.UI.Image>();
            brush.sprite = InkUiTextureFactory.CreateBrushSprite(); brush.color = InkPalette.Ink;
            var stamp = Rect("Seal", panel, new Vector2(-248, 0), new Vector2(74, 74));
            InkHudSurface.Ensure(stamp, true);
            Label(stamp, "해금", 30, Vector2.zero, new Vector2(72, 68));
            Label(panel, "업적 달성", 31, new Vector2(42, 32), new Vector2(480, 40));
            label = Label(panel, "", 40, new Vector2(42, -20), new Vector2(480, 62));
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            group.alpha = 0;
        }
        void Update()
        {
            var game = GameManager.Instance;
            if (!ReferenceEquals(owner, PermanentGrowthProfile.SkinAccountToken) || game == null || game.State != GameState.Playing)
            { Clear(); return; }
            if (!game.IsGameplayTicking) { if (group != null) group.alpha = 0; return; }
            if (!showing && queue.Count == 0) return;
            Build();
            MobileUiLayout.ApplySafeArea(safe, MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);
            panel.localScale = Vector3.one * Mathf.Min(1, safe.rect.width / 700f);
            if (!showing)
            {
                int id = queue.Dequeue(); showing = true; time = 0;
                InkLocalizedText.SetSource(label, InkAchievementCatalog.Name(id));
            }
            time += Mathf.Min(Time.unscaledDeltaTime, .05f);
            float fade = Mathf.Min(Mathf.Clamp01(time / .2f), Mathf.Clamp01((3f - time) / .25f));
            group.alpha = fade;
            panel.anchoredPosition = new Vector2(-24 + (LobbySettingsProfile.ReducedMotionEnabled ? 0 : 45 * (1 - fade)),
                CalculateTop(MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height));
            if (time >= 3) { showing = false; group.alpha = 0; }
        }
        void Clear() { queue.Clear(); showing = false; if (group != null) group.alpha = 0; }
        void OnDisable() => Clear();
        public static float CalculateTop(Rect area, int width, int height)
        {
            float hudBottom = Mathf.Min(GameplayHudView.CalculateVisibleHudRect(area, width, height).yMin,
                GameplayHudView.CalculatePauseTouchRect(area, width, height).yMin);
            return hudBottom + MobileUiLayout.GetLogicalTopInset(area, width, height) - 20f;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
            r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }
        static UnityEngine.UI.Text Label(Transform parent, string source, int size, Vector2 pos, Vector2 bounds)
        {
            var t = Rect("Label", parent, pos, bounds).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkLocalizedText.SetSource(t, source); InkUiStyle.ApplyReadableText(t, size, TextAnchor.MiddleCenter, true, true);
            t.color = InkPalette.Paper; t.raycastTarget = false; return t;
        }
    }
}
