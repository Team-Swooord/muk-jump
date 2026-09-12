using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 중앙 무대의 교체/착용 연출. 저장과 분리하며 UI와 이펙트는 최초 한 번만 만든다.
    [DisallowMultipleComponent]
    public sealed class InkShowroomPresentation : MonoBehaviour
    {
        public const float EquipDuration = 1.05f;
        public const float BrowseDuration = .28f;
        [SerializeField] RectTransform focus, effectRoot;
        [SerializeField] CanvasGroup focusAlpha;
        [SerializeField] Image wash;
        [SerializeField] GrowthBloomArc[] rings;
        [SerializeField] InkShowroomMotes motes;
        bool playing, equipping;
        float elapsed, direction;
        uint inactiveGeneration;
        [SerializeField] Vector2 restPosition;
        [SerializeField] Vector3 restScale = Vector3.one;
        public bool IsPlaying => playing;
        public bool IsEquipping => playing && equipping;
        public int ParticleCount => motes != null ? motes.VisibleCount : 0;
        public int EquipPlayCount { get; private set; }

        public void Initialize(RectTransform stage, RectTransform target)
        {
            Cancel();
            if (stage == null || target == null) return;
            focus = target;
            restPosition = target.anchoredPosition;
            restScale = target.localScale;
            focusAlpha = target.GetComponent<CanvasGroup>();
            if (focusAlpha == null) focusAlpha = target.gameObject.AddComponent<CanvasGroup>();
            effectRoot = Rect("ShowroomInkEffect", stage, new Vector2(500, 372));
            effectRoot.anchoredPosition = new Vector2(0,45);
            effectRoot.SetAsFirstSibling();
            var clip = effectRoot.GetComponent<RectMask2D>();
            if (clip == null) clip = effectRoot.gameObject.AddComponent<RectMask2D>();
            clip.softness = new Vector2Int(12,24);
            var group = effectRoot.GetComponent<CanvasGroup>();
            if (group == null) group = effectRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            wash = Picture("InkWash", effectRoot, InkUiTextureFactory.CreateBlobSprite(), Vector2.one * 320f);
            wash.rectTransform.anchoredPosition = new Vector2(0,-45);
            rings = new GrowthBloomArc[2];
            for (int i = 0; i < rings.Length; i++)
            {
                var rect = Rect("Ripple" + i, effectRoot, Vector2.one * 390f);
                rect.anchoredPosition = new Vector2(0,-45);
                // 사용자 정의 Graphic은 Image와 달리 CanvasRenderer를 자동으로 요구하지 않는다.
                if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
                rings[i] = rect.GetComponent<GrowthBloomArc>();
                if (rings[i] == null) rings[i] = rect.gameObject.AddComponent<GrowthBloomArc>();
                rings[i].raycastTarget = false;
                rings[i].IsWaterRipple = true;
            }
            var moteRect = Rect("HanjiMotes", effectRoot, Vector2.one * 520f);
            moteRect.anchoredPosition = new Vector2(0,-45);
            motes = moteRect.GetComponent<InkShowroomMotes>();
            if (motes == null) motes = moteRect.gameObject.AddComponent<InkShowroomMotes>();
            motes.raycastTarget = false;
            moteRect.localScale = new Vector3(1, .8f, 1);
            // 바닥의 엷은 먹 타원은 무대만 기울이며 캐릭터를 원근 왜곡하지 않는다.
            var floor = Picture("InkStageFloor", stage, InkUiTextureFactory.CreateBlobSprite(), new Vector2(370, 44));
            floor.rectTransform.anchoredPosition = new Vector2(0, -130);
            floor.color = new Color(.11f, .106f, .102f, .075f);
            floor.transform.SetAsFirstSibling();
            Cancel();
        }
        public void Browse(int step)
        {
            Cancel();
            if (focus == null || LobbySettingsProfile.ReducedMotionEnabled) return;
            direction = Mathf.Sign(step);
            elapsed = 0f; playing = true; equipping = false;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            ApplyFrame(0f);
        }
        public void Wear()
        {
            Cancel();
            if (focus == null || effectRoot == null) return;
            EquipPlayCount++;
            elapsed = 0f; playing = equipping = true;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            effectRoot.gameObject.SetActive(true);
            ApplyFrame(0f);
        }
        void OnEnable() => Cancel();
        void OnDisable() => Cancel();
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void Update()
        {
            if (!playing) return;
            if (!MobileApplicationLifecycle.IsApplicationActive || inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration ||
                focus == null || !focus.gameObject.activeInHierarchy)
            { Cancel(); return; }
            elapsed += Mathf.Min(.05f, Mathf.Max(0f, Time.unscaledDeltaTime));
            ApplyFrame(elapsed);
            if (elapsed >= (equipping ? EquipDuration : BrowseDuration)) Cancel();
        }
        void ApplyFrame(float time)
        {
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            if (!equipping)
            {
                float t = Mathf.Clamp01(time / BrowseDuration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                focus.anchoredPosition = restPosition + new Vector2(reduced ? 0f : direction * (1f - ease) * 52f, 0f);
                focus.localScale = restScale * (reduced ? 1f : Mathf.Lerp(.91f, 1f, ease));
                focusAlpha.alpha = Mathf.Lerp(.55f, 1f, ease);
                return;
            }
            float t1 = Mathf.Clamp01(time / EquipDuration);
            float pulse = reduced ? 0f : Mathf.Sin(t1 * Mathf.PI * 2f) * Mathf.Pow(1f - t1, 3f);
            focus.localScale = restScale * (1f + pulse * .075f);
            Color ink = InkPalette.Ink;
            ink.a = .12f * Mathf.Sin(t1 * Mathf.PI) * (1f - t1);
            wash.color = ink;
            wash.rectTransform.localScale = Vector3.one * (reduced ? 1f : .78f + t1 * .45f);
            for (int i = 0; i < rings.Length; i++)
            {
                float t = Mathf.Clamp01((time - .06f - i * .10f) / .7f);
                Color tint = InkPalette.Ink;
                tint.a = reduced ? 0f : Mathf.Clamp01(t * 12f) * Mathf.Pow(1f - t, 1.35f) * (.6f - i * .14f);
                rings[i].color = tint;
                rings[i].Reveal = 1f;
                rings[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(.62f, 1.2f, 1f - Mathf.Pow(1f - t, 3f));
                rings[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * 77);
            }
            motes.Present(time, reduced ? 0 : VfxQualityRuntime.Profile.ScaleDecorativeCount(InkShowroomMotes.Capacity, 8));
        }
        public void Cancel()
        {
            playing = equipping = false;
            if (focus != null) { focus.anchoredPosition = restPosition; focus.localScale = restScale; }
            if (focusAlpha != null) focusAlpha.alpha = 1;
            if (motes != null) motes.Present(0, 0);
            if (effectRoot != null) effectRoot.gameObject.SetActive(false);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
            return rect;
        }
        static Image Picture(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var rect = Rect(name, parent, size);
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }
#if UNITY_EDITOR
        public void SampleForTests(float time) { if (playing) ApplyFrame(time); }
#endif
    }
}
