using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 중앙 무대의 교체/착용 연출. 저장과 분리하며 UI와 이펙트는 최초 한 번만 만든다.
    [DisallowMultipleComponent]
    public sealed class InkShowroomPresentation : MonoBehaviour
    {
        public const float EquipDuration = 1.05f;
        public const float BrowseDuration = .42f;
        [SerializeField] RectTransform focus, effectRoot;
        [SerializeField] CanvasGroup focusAlpha;
        [SerializeField] Image wash;
        [SerializeField] GrowthBloomArc[] rings;
        [SerializeField] InkShowroomMotes motes;
        [SerializeField] Image costume, outgoingCostume;
        [SerializeField] Vector2 costumePosition, outgoingPosition;
        [SerializeField] Quaternion restRotation = Quaternion.identity;
        [SerializeField] bool costumePoseKnown;
        bool playing, equipping, prepared;
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
            restRotation = target.localRotation;
            costume = target.Find("Accessory").GetComponent<Image>();
            outgoingCostume = Picture("OutgoingCostume", target, null, Vector2.one);
            outgoingCostume.preserveAspect = true;
            RememberCostumePose();
            focusAlpha = target.GetComponent<CanvasGroup>();
            if (focusAlpha == null) focusAlpha = target.gameObject.AddComponent<CanvasGroup>();
            effectRoot = Rect("ShowroomInkEffect", stage, new Vector2(570, 570));
            effectRoot.anchoredPosition = new Vector2(0,30);
            effectRoot.SetAsFirstSibling();
            var clip = effectRoot.GetComponent<RectMask2D>();
            if (clip == null) clip = effectRoot.gameObject.AddComponent<RectMask2D>();
            clip.softness = new Vector2Int(12,24);
            var group = effectRoot.GetComponent<CanvasGroup>();
            if (group == null) group = effectRoot.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            wash = Picture("InkWash", effectRoot, InkUiTextureFactory.CreateBlobSprite(), Vector2.one * 460f);
            wash.rectTransform.anchoredPosition = new Vector2(0,-30);
            rings = new GrowthBloomArc[2];
            for (int i = 0; i < rings.Length; i++)
            {
                var rect = Rect("Ripple" + i, effectRoot, Vector2.one * 500f);
                rect.anchoredPosition = new Vector2(0,-30);
                // 사용자 정의 Graphic은 Image와 달리 CanvasRenderer를 자동으로 요구하지 않는다.
                if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
                rings[i] = rect.GetComponent<GrowthBloomArc>();
                if (rings[i] == null) rings[i] = rect.gameObject.AddComponent<GrowthBloomArc>();
                rings[i].raycastTarget = false;
                rings[i].IsWaterRipple = true;
            }
            var moteRect = Rect("HanjiMotes", effectRoot, Vector2.one * 520f);
            moteRect.anchoredPosition = new Vector2(0,-30);
            motes = moteRect.GetComponent<InkShowroomMotes>();
            if (motes == null) motes = moteRect.gameObject.AddComponent<InkShowroomMotes>();
            motes.raycastTarget = false;
            moteRect.localScale = new Vector3(1.45f, 1.25f, 1);
            // 바닥의 엷은 먹 타원은 무대만 기울이며 캐릭터를 원근 왜곡하지 않는다.
            var floor = Picture("InkStageFloor", stage, InkUiTextureFactory.CreateBlobSprite(), new Vector2(500, 52));
            floor.rectTransform.anchoredPosition = new Vector2(0, -228);
            floor.color = new Color(.11f, .106f, .102f, .075f);
            floor.transform.SetAsFirstSibling();
            Cancel();
        }
        // 몸통 복제 없이 장식 한 장만 재사용한다. 연타 시 이전 연출을 끝내고 현재 옷을 출발점으로 삼는다.
        public void PrepareCostumeChange(bool allowOutgoing)
        {
            Cancel();
            if (costume == null || outgoingCostume == null) return;
            outgoingCostume.sprite = allowOutgoing && costume.enabled ? costume.sprite : null;
            outgoingCostume.rectTransform.sizeDelta = costume.rectTransform.sizeDelta;
            outgoingPosition = costume.rectTransform.anchoredPosition;
            outgoingCostume.rectTransform.anchoredPosition = outgoingPosition;
            outgoingCostume.rectTransform.localScale = Vector3.one;
            outgoingCostume.rectTransform.localRotation = Quaternion.identity;
            outgoingCostume.color = Color.white;
            if (costume.transform.GetSiblingIndex() < focus.Find("OriginalInkBody").GetSiblingIndex())
                outgoingCostume.transform.SetAsFirstSibling();
            else outgoingCostume.transform.SetAsLastSibling();
            prepared = true;
        }
        public void RememberCostumePose()
        {
            if (costume == null) return;
            costumePosition = costume.rectTransform.anchoredPosition;
            costumePoseKnown = true;
        }
        public void Browse(int step)
        {
            if (!prepared) Cancel();
            prepared = false;
            if (focus == null || step == 0 || LobbySettingsProfile.ReducedMotionEnabled) { Cancel(); return; }
            direction = Mathf.Sign(step);
            elapsed = 0f; playing = true; equipping = false;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            outgoingCostume.enabled = outgoingCostume.sprite != null;
            effectRoot.gameObject.SetActive(true);
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
                float hop = reduced ? 0f : Mathf.Sin(t * Mathf.PI);
                focus.anchoredPosition = restPosition + new Vector2(0, hop * 24f);
                focus.localScale = Vector3.Scale(restScale, new Vector3(1f - hop * .045f, 1f + hop * .045f, 1));
                focus.localRotation = restRotation * Quaternion.Euler(0, 0, -direction * hop * 3f);
                float arrive = Mathf.Clamp01((t - .08f) / .92f);
                // 약한 오버슈트로 새 장식이 제자리에 걸리는 느낌을 준다.
                float u = arrive - 1f;
                float settle = 1f + 2.2f * u * u * u + 1.2f * u * u;
                costume.rectTransform.anchoredPosition = costumePosition + (reduced ? Vector2.zero :
                    new Vector2(direction * 170f * (1f - settle), Mathf.Sin(arrive * Mathf.PI) * 40f));
                costume.rectTransform.localScale = Vector3.one * (reduced ? 1f : Mathf.LerpUnclamped(.82f, 1f, settle));
                costume.rectTransform.localRotation = Quaternion.Euler(0, 0, reduced ? 0f : direction * -22f * (1f - settle));
                costume.color = new Color(1, 1, 1, reduced ? 1f : Mathf.Clamp01(arrive * 6f));
                float leave = Mathf.Clamp01(t / .45f);
                outgoingCostume.enabled = !reduced && leave < 1f && outgoingCostume.sprite != null;
                outgoingCostume.rectTransform.anchoredPosition = outgoingPosition +
                    new Vector2(-direction * leave * 160f, -Mathf.Sin(leave * Mathf.PI * .75f) * 45f);
                outgoingCostume.rectTransform.localScale = Vector3.one * (1f - leave * .2f);
                outgoingCostume.rectTransform.localRotation = Quaternion.Euler(0, 0, direction * leave * 28f);
                outgoingCostume.color = new Color(1, 1, 1, 1f - leave);
                // 탐색은 짧은 먹 번짐만 사용한다. 저장 성공용 파동과 구분한다.
                wash.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, reduced ? 0f : hop * .045f);
                wash.rectTransform.localScale = Vector3.one * (.9f + t * .15f);
                foreach (var ring in rings) ring.color = Color.clear;
                motes.Present(t * EquipDuration, reduced ? 0 : VfxQualityRuntime.Profile.ScaleDecorativeCount(10, 3));
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
            playing = equipping = prepared = false;
            if (focus != null) { focus.anchoredPosition = restPosition; focus.localScale = restScale; focus.localRotation = restRotation; }
            if (focusAlpha != null) focusAlpha.alpha = 1;
            if (costume != null && costumePoseKnown)
            {
                costume.rectTransform.anchoredPosition = costumePosition;
                costume.rectTransform.localScale = Vector3.one;
                costume.rectTransform.localRotation = Quaternion.identity;
                costume.color = Color.white;
            }
            if (outgoingCostume != null) { outgoingCostume.enabled = false; outgoingCostume.sprite = null; }
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
