using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MukJump.Core
{
    /// 성장 성공: 선택한 원화 둘레의 먹고리 → 금박 → 새로 채운 먹방울.
    /// 고정 UI 풀과 전용 음원 하나를 재사용하며 저장·입력 판정에는 관여하지 않는다.
    [DisallowMultipleComponent]
    public sealed class GrowthBloomPresentation : MonoBehaviour
    {
        public const float Duration = 1.4f;
        public const float ImpactTime = .32f;
        const int MoteCapacity = 12;
        readonly Image[] motes = new Image[MoteCapacity];
        RectTransform host;
        RectTransform root;
        RectTransform focus;
        RectTransform progressMark;
        CanvasGroup group;
        Image wash;
        Image inkRing;
        Image fallingDrop;
        readonly GrowthBloomArc[] ripples = new GrowthBloomArc[3];
        Sequence progressSequence;
        RectTransform[] animatedMarks;
        Vector2[] markPositions;
        Vector3[] markScales;
        bool impactPlayed;
        Image fillingImage;
        Color fillingColor;
        Image progressTrail;
        Image progressSpark;
        Vector3 focusScale;
        int filledIndex;
        bool dispersing;
        Vector2[] dispersalOrigins;
        GrowthBloomArc goldRing;
        GrowthBloomArc markRing;
        AudioSource sound;
        AudioClip clip;
        float elapsed;
        int moteCount;
        bool playing;
        bool reduced;

        public bool IsPlaying => playing;

        bool HasValidReferences()
        {
            if (root == null || group == null || wash == null || inkRing == null ||
                goldRing == null || markRing == null || fallingDrop == null || progressTrail == null ||
                progressSpark == null || sound == null || clip == null) return false;
            for (int i = 0; i < ripples.Length; i++) if (ripples[i] == null) return false;
            for (int i = 0; i < motes.Length; i++) if (motes[i] == null) return false;
            return true;
        }

        void OnEnable()
        {
            if (host != null) Initialize(host);
            Cancel();
        }

        public void Initialize(RectTransform parent)
        {
            if (parent == null) return;
            host = parent;
            if (HasValidReferences()) return;
            Cancel();
            // 리로드 뒤 남은 동일 계층을 다시 연결한다.
            root = Rect("GrowthBloom", parent, new Vector2(1080f, 1920f));
            // Unity의 소실된 컴포넌트는 C# null과 다르므로 ??로 복구하지 않는다.
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            wash = Picture("PaperBloom", InkUiTextureFactory.CreateBlobSprite(), new Vector2(380f, 340f));
            inkRing = Picture("InkCircle", Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_selected_ring"), Vector2.one * 398f);
            goldRing = Arc("GoldCircle", 380f);
            markRing = Arc("NewGrowthMark", 80f);
            markRing.gameObject.SetActive(false);
            fallingDrop = Picture("FallingInkDrop", InkUiTextureFactory.CreateBlobSprite(), new Vector2(32f, 48f));
            progressTrail = Picture("ProgressInkTrail", InkUiTextureFactory.CreateBrushSprite(), new Vector2(1f, 7f));
            progressSpark = Picture("ProgressInkSpark", InkUiTextureFactory.CreateBlobSprite(), new Vector2(18f, 18f));
            for (int i = 0; i < ripples.Length; i++)
            {
                ripples[i] = Arc($"WaterRipple{i}", 360f);
                ripples[i].IsWaterRipple = true;
            }
            Sprite brush = InkUiTextureFactory.CreateBlobSprite();
            for (int i = 0; i < motes.Length; i++)
                motes[i] = Picture($"GoldLeaf{i}", brush,
                    new Vector2(5f + i % 4 * 2f, 7f + i % 3 * 3f));
            RectTransform audioRoot = Rect("GrowthBloomAudio", root, Vector2.zero);
            sound = audioRoot.GetComponent<AudioSource>();
            if (sound == null) sound = audioRoot.gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = sound.loop = false;
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = false;
            if (clip == null && sound.clip != null && sound.clip.name == "Growth_Ink_WaterDrop")
                clip = sound.clip;
            if (clip == null)
            {
                float[] samples = GrowthBloomSound.CreateSamples();
                clip = AudioClip.Create("Growth_Ink_WaterDrop", samples.Length, 1,
                    GrowthBloomSound.SampleRate, false);
                clip.SetData(samples, 0);
            }
            sound.clip = clip;
            Cancel();
        }

        public void Play(RectTransform icon, RectTransform filledMark, RectTransform[] marks = null)
        {
            Cancel();
            if (!HasValidReferences() && host != null) Initialize(host);
            if (!HasValidReferences() || icon == null) return;
            focus = icon;
            focusScale = focus.localScale;
            progressMark = filledMark;
            reduced = LobbySettingsProfile.ReducedMotionEnabled;
            moteCount = reduced ? 0 : VfxQualityRuntime.Profile.ScaleDecorativeCount(MoteCapacity, minimum: 4);
            elapsed = 0f;
            impactPlayed = false;
            playing = true;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            fallingDrop.gameObject.SetActive(true);
            if (reduced && sound != null && Application.isPlaying)
            {
                sound.volume = LobbySettingsProfile.SfxVolume * .72f;
                sound.Play();
                impactPlayed = true;
            }
            if (!reduced && Application.isPlaying) AnimateProgress(marks);
            ApplyFrame(0f);
        }

        public void Cancel()
        {
            dispersing = false;
            dispersalOrigins = null;
            progressSequence?.Kill();
            progressSequence = null;
            if (animatedMarks != null)
                for (int i = 0; i < animatedMarks.Length; i++)
                    if (animatedMarks[i] != null)
                    {
                        animatedMarks[i].anchoredPosition = markPositions[i];
                        animatedMarks[i].localScale = markScales[i];
                    }
            animatedMarks = null;
            if (fillingImage != null) fillingImage.color = fillingColor;
            fillingImage = null;
            if (focus != null) focus.localScale = focusScale;
            playing = false;
            elapsed = 0f;
            focus = progressMark = null;
            if (sound != null) sound.Stop();
            if (group != null) group.alpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
        }

        void OnDisable() => Cancel();
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void OnDestroy()
        {
            if (sound != null) sound.Stop();
            if (clip == null) return;
            if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip);
        }

        void Update()
        {
            if (!playing) return;
            if (host == null || focus == null || !HasValidReferences())
            { Cancel(); return; }
            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            if (!impactPlayed && elapsed >= ImpactTime && sound != null)
            {
                impactPlayed = true;
                sound.volume = LobbySettingsProfile.SfxVolume * .72f;
                sound.Play();
            }
            if (sound != null) sound.volume = LobbySettingsProfile.SfxVolume * .72f;
            ApplyFrame(elapsed);
            if (elapsed >= Duration) Cancel();
        }

        void ApplyFrame(float time)
        {
            if (dispersing) { ApplyDispersal(time); return; }
            // 같은 콘텐츠 좌표계를 써 노치·배너·해상도가 달라도 실제 아이콘을 따라간다.
            Vector2 center = host.InverseTransformPoint(focus.position);
            float impact = Mathf.Max(0f, time - ImpactTime);
            float spring = time < ImpactTime || reduced ? 0f : Mathf.Sin(impact * 22f) * Mathf.Exp(-impact * 9f);
            focus.localScale = Vector3.Scale(focusScale, new Vector3(1f + spring * .14f, 1f - spring * .12f, 1f));
            ApplyProgressFlow(time);
            float fall = Mathf.Clamp01(time / ImpactTime);
            Set(fallingDrop, center + Vector2.up * Mathf.Lerp(340f, 0f, fall * fall),
                InkPalette.Ink, !reduced && time < ImpactTime ? Smooth(0f, .06f, time) : 0f, 1f);
            fallingDrop.rectTransform.localScale = new Vector3(1f - fall * .2f, 1f + fall * .45f, 1f);
            for (int i = 0; i < ripples.Length; i++)
            {
                float r = Mathf.Clamp01((time - ImpactTime - i * .085f) / .86f);
                float expand = 1f - Mathf.Pow(1f - r, 3f);
                Set(ripples[i], center, InkPalette.Ink,
                    reduced ? 0f : Smooth(0f, .12f, r) * Mathf.Pow(1f - r, 1.6f) * (.32f - i * .055f), 1f);
                // 화면 XY 평면의 동심 파동. Y를 압축하면 물 표면을 비스듬히 보는 원근감이 생긴다.
                float radiusScale = Mathf.Lerp(.72f, 2.1f - i * .16f, expand);
                ripples[i].rectTransform.localScale = new Vector3(radiusScale, radiusScale, 1f);
                ripples[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 47f);
                ripples[i].Reveal = 1f;
            }
            time = reduced ? time : Mathf.Max(0f, time - ImpactTime);
            float enter = Smooth(0f, .12f, time);
            float fade = 1f - Smooth(.58f, Duration - ImpactTime, time);
            float spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(time / .62f), 3f);
            group.alpha = 1f;
            Set(wash, center, InkPalette.TextLight, .20f * enter * fade,
                reduced ? 1f : Mathf.Lerp(.91f, 1.08f, spread));
            Set(inkRing, center, Color.white, .64f * enter * fade,
                reduced ? 1f : Mathf.Lerp(.89f, 1.06f, spread));
            Set(goldRing, center, InkPalette.Gold, 0f,
                reduced ? 1f : Mathf.Lerp(.95f, 1.10f, spread));
            goldRing.Reveal = reduced ? 1f : Smooth(.07f, .43f, time);

            for (int i = 0; i < motes.Length; i++)
            {
                Image mote = motes[i];
                if (mote == null) { Cancel(); return; }
                bool active = i < moteCount;
                mote.gameObject.SetActive(active);
                if (!active) continue;
                float delay = (i % 4) * .018f;
                float t = Mathf.Clamp01((time - delay) / (.48f + i % 3 * .07f));
                float side = i % 2 == 0 ? -1f : 1f;
                // 중심에서 서로 다른 포물선으로 튀는 작은 먹방울. 방사형 직선/금박 조각은 쓰지 않는다.
                Vector2 point = center + new Vector2(side * (15f + (95f + i % 4 * 23f) * t),
                    (125f + i % 3 * 32f) * t - (160f + i % 3 * 22f) * t * t);
                float alpha = Smooth(delay, delay + .025f, time) * (1f - Smooth(.45f, 1f, t));
                Set(mote, point, InkPalette.Ink, alpha * .48f, Mathf.Lerp(1f, .3f, t));
                mote.rectTransform.localEulerAngles = new Vector3(0f, 0f, side * (18f + t * 38f));
            }

            // 진행 먹방울에는 금색 원을 덧씌우지 않고 탄성 움직임만 유지한다.
            markRing.gameObject.SetActive(false);
        }

        static void Set(Graphic graphic, Vector2 position, Color tint, float alpha, float scale)
        {
            tint.a = alpha;
            graphic.color = tint;
            graphic.rectTransform.anchoredPosition = position;
            graphic.rectTransform.localScale = Vector3.one * scale;
        }

        void AnimateProgress(RectTransform[] marks)
        {
            if (marks == null || marks.Length == 0) return;
            animatedMarks = marks;
            markPositions = new Vector2[marks.Length];
            markScales = new Vector3[marks.Length];
            progressSequence = DOTween.Sequence().SetUpdate(true);
            filledIndex = System.Array.IndexOf(marks, progressMark);
            if (progressMark != null)
            {
                fillingImage = progressMark.GetComponent<Image>();
                if (fillingImage != null)
                {
                    fillingColor = fillingImage.color;
                    Color pending = fillingColor; pending.a = .32f;
                    fillingImage.color = pending;
                    progressSequence.Insert(ImpactTime + .06f + Mathf.Max(0, filledIndex) * .045f, DOTween.To(() => .32f, a =>
                    {
                        if (fillingImage == null) return;
                        Color tint = fillingColor; tint.a = a; fillingImage.color = tint;
                    }, fillingColor.a, .22f).SetEase(Ease.OutCubic));
                }
            }
            for (int i = 0; i < marks.Length; i++)
            {
                RectTransform mark = marks[i];
                if (mark == null) continue;
                Vector2 origin = markPositions[i] = mark.anchoredPosition;
                Vector3 scale = markScales[i] = mark.localScale;
                if (i > filledIndex || !mark.gameObject.activeInHierarchy) continue;
                bool newlyFilled = mark == progressMark;
                float delay = ImpactTime + .06f + i * .045f;
                // UGUI의 실제 먹방울이 차례로 튀었다 눌리고 복원되는 감쇠 탄성파.
                progressSequence.Insert(delay, DOTween.To(() => 0f, t =>
                {
                    if (mark == null) return;
                    float wave = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Pow(1f - t, 2f);
                    mark.anchoredPosition = origin + Vector2.up * wave * (newlyFilled ? 75f : 28f);
                    mark.localScale = Vector3.Scale(scale, new Vector3(1f - wave * .18f,
                        1f + wave * (newlyFilled ? .65f : .3f), 1f));
                }, 1f, .6f).SetEase(Ease.Linear));
            }
        }

        void ApplyProgressFlow(float time)
        {
            bool visible = !reduced && animatedMarks != null && filledIndex >= 0 &&
                animatedMarks[0] != null && animatedMarks[filledIndex] != null;
            progressTrail.gameObject.SetActive(false);
            progressSpark.gameObject.SetActive(visible);
            if (!visible) return;
            Vector2 start = host.InverseTransformPoint(animatedMarks[0].parent.TransformPoint(markPositions[0]));
            Vector2 end = host.InverseTransformPoint(animatedMarks[filledIndex].parent.TransformPoint(markPositions[filledIndex]));
            float t = Mathf.Clamp01((time - ImpactTime - .06f) / Mathf.Max(.045f, filledIndex * .045f));
            Vector2 head = Vector2.Lerp(start, end, t);
            float alpha = Smooth(ImpactTime, ImpactTime + .06f, time) * (1f - Smooth(.95f, Duration, time));
            Set(progressTrail, (start + head) * .5f, InkPalette.Gold, alpha * .48f, 1f);
            progressTrail.rectTransform.sizeDelta = new Vector2(Vector2.Distance(start, head), 6f);
            progressTrail.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
            Set(progressSpark, head, InkPalette.Gold, alpha * (1f - Smooth(.8f, 1.1f, time)), 1f);
        }

        static float Smooth(float from, float to, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        public void PlayReset(RectTransform icon, Vector3[] previousMarkPositions)
        {
            Cancel();
            if (!HasValidReferences() && host != null) Initialize(host);
            if (!HasValidReferences() || icon == null) return;
            focus = icon;
            focusScale = icon.localScale;
            reduced = LobbySettingsProfile.ReducedMotionEnabled;
            dispersalOrigins = new Vector2[Mathf.Max(1, previousMarkPositions?.Length ?? 0)];
            for (int i = 0; i < dispersalOrigins.Length; i++)
                dispersalOrigins[i] = host.InverseTransformPoint(previousMarkPositions != null &&
                    i < previousMarkPositions.Length ? previousMarkPositions[i] : icon.position);
            moteCount = reduced ? 4 : VfxQualityRuntime.Profile.ScaleDecorativeCount(MoteCapacity, minimum: 4);
            dispersing = playing = true;
            impactPlayed = true;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            ApplyFrame(0f);
        }

        void ApplyDispersal(float time)
        {
            group.alpha = 1f;
            Set(wash, Vector2.zero, InkPalette.TextLight, 0f, 1f);
            Set(inkRing, Vector2.zero, Color.white, 0f, 1f);
            Set(goldRing, Vector2.zero, InkPalette.Gold, 0f, 1f);
            markRing.gameObject.SetActive(false);
            fallingDrop.gameObject.SetActive(false);
            progressTrail.gameObject.SetActive(false);
            progressSpark.gameObject.SetActive(false);
            foreach (var ripple in ripples) Set(ripple, Vector2.zero, InkPalette.Ink, 0f, 1f);
            for (int i = 0; i < motes.Length; i++)
            {
                bool active = i < moteCount;
                motes[i].gameObject.SetActive(active);
                if (!active) continue;
                float t = Mathf.Clamp01((time - (i % 4) * .04f) / .85f);
                Vector2 origin = dispersalOrigins[i % dispersalOrigins.Length];
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 drift = reduced ? Vector2.zero : new Vector2(side * (45f + i % 3 * 24f) * t,
                    90f * t - 45f * t * t + Mathf.Sin(t * Mathf.PI) * (i % 3) * 10f);
                Set(motes[i], origin + drift, InkPalette.Ink, .75f * (1f - t) * (1f - t),
                    Mathf.Lerp(1.6f, .25f, t));
                motes[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 37f + side * t * 70f);
            }
        }

        Image Picture(string name, Sprite sprite, Vector2 size)
        {
            RectTransform rect = Rect(name, root, size);
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        GrowthBloomArc Arc(string name, float size)
        {
            RectTransform rect = Rect(name, root, Vector2.one * size);
            var arc = rect.GetComponent<GrowthBloomArc>();
            if (arc == null) arc = rect.gameObject.AddComponent<GrowthBloomArc>();
            arc.raycastTarget = false;
            return arc;
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }
    }


    /// 먹방울이 물에 닿는 둥근 착수음과 짧은 물결 마찰을 합성한다.
    public static class GrowthBloomSound
    {
        public const int SampleRate = 22050;
        public static float[] CreateSamples()
        {
            var samples = new float[Mathf.RoundToInt(SampleRate * GrowthBloomPresentation.Duration)];
            uint noise = 0x72ac3u;
            float filtered = 0f;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate;
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                filtered += (((noise & 65535u) / 32767.5f - 1f) - filtered) * .22f;
                float phase = 2f * Mathf.PI * (330f * t + 1500f * t * t);
                float value = Mathf.Sin(phase) * .40f * Mathf.Exp(-24f * t);
                value += Mathf.Sin(2f * Mathf.PI * 145f * t) * .24f * Mathf.Exp(-19f * t);
                value += filtered * .20f * Mathf.Exp(-11f * t);
                // 작은 반사음 한 번으로 수면의 공간감만 남기고 종 멜로디는 넣지 않는다.
                float echo = Mathf.Max(0f, t - .085f);
                value += Mathf.Sin(2f * Mathf.PI * (460f * echo + 900f * echo * echo)) *
                    .10f * Mathf.Exp(-23f * echo) * Mathf.Clamp01(echo / .006f);
                value *= Mathf.Clamp01(t / .003f) * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.72f, GrowthBloomPresentation.Duration, t)));
                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float gain = .62f / Mathf.Max(.62f, peak);
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }

    }
}
