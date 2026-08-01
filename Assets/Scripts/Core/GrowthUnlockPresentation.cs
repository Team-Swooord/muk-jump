using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 영구 성장 성공을 대각선 먹획과 중앙 먹번짐으로 보여 주는 고정 UI 연출.
    /// 런타임 중 오브젝트를 다시 만들지 않고 같은 계층과 절차적 마스크를 재사용한다.
    [DisallowMultipleComponent]
    public sealed class GrowthUnlockPresentation : MonoBehaviour
    {
        public const float SequenceDuration = 1.26f;
        public const float UpgradeSequenceDuration = 0.86f;

        const int MaxDecorativeDrops = 8;
        const int MaxNodeDrops = 4;

        static Sprite ringSprite;
        static Texture2D ringTexture;

        readonly Image[] drops = new Image[MaxDecorativeDrops];
        readonly Vector2[] dropDirections = new Vector2[MaxDecorativeDrops];
        readonly float[] dropDistances = new float[MaxDecorativeDrops];
        readonly float[] dropDelays = new float[MaxDecorativeDrops];
        readonly float[] dropRotations = new float[MaxDecorativeDrops];
        readonly Image[] nodeDrops = new Image[MaxNodeDrops];
        readonly Vector2[] nodeDropDirections =
            new Vector2[MaxNodeDrops];
        readonly float[] nodeDropRotations =
            new float[MaxNodeDrops];

        RectTransform presentationRoot;
        CanvasGroup presentationGroup;
        Image wash;
        Image upperBrush;
        Image lowerBrush;
        Image splash;
        Image outerRing;
        Image innerRing;
        Image slashPrimary;
        Image slashSecondary;
        Image lockPlate;
        Image unlockedIconPlate;
        Image unlockedIcon;
        RectTransform nodeFeedbackRoot;
        Image nodeFruitGlow;
        Image nodeFruit;
        Text lockText;
        Text titleText;
        Text subtitleText;
        int activeDecorativeDrops;
        int activeNodeDrops;
        int playSerial;
        float elapsed;
        float activeDuration = SequenceDuration;
        bool upgradeMode;
        bool hasNodeFeedback;
        bool playing;

        public bool IsPlaying => playing;
        public int ActiveDecorativeDropCount => activeDecorativeDrops;
        public RectTransform PresentationRoot => presentationRoot;
        public CanvasGroup PresentationGroup => presentationGroup;
        public string Title => titleText != null ? titleText.text : string.Empty;
        public string Subtitle => subtitleText != null ? subtitleText.text : string.Empty;
        public Sprite UnlockedIcon =>
            unlockedIcon != null ? unlockedIcon.sprite : null;
        public float ActiveSequenceDuration => activeDuration;
        public bool HasNodeFeedback => hasNodeFeedback;
        public int ActiveNodeDropCount => activeNodeDrops;
        public Vector2 NodeFeedbackPosition =>
            nodeFeedbackRoot != null
                ? nodeFeedbackRoot.anchoredPosition
                : Vector2.zero;
        public Sprite NodeFruitSprite =>
            nodeFruit != null ? nodeFruit.sprite : null;
        public Color NodeFruitColor =>
            nodeFruit != null ? nodeFruit.color : Color.clear;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReleaseRuntimeAssets()
        {
            DestroyRuntimeObject(ringSprite);
            DestroyRuntimeObject(ringTexture);
            ringSprite = null;
            ringTexture = null;
        }

        public void Initialize(RectTransform canvasRoot)
        {
            if (canvasRoot == null)
                return;

            if (presentationRoot != null)
            {
                if (presentationRoot.parent != canvasRoot)
                    presentationRoot.SetParent(canvasRoot, false);
                presentationRoot.SetAsFirstSibling();
                return;
            }

            var root = new GameObject(
                "GrowthUnlockPresentation",
                typeof(RectTransform),
                typeof(CanvasGroup));
            presentationRoot = root.GetComponent<RectTransform>();
            presentationRoot.SetParent(canvasRoot, false);
            presentationRoot.anchorMin = Vector2.zero;
            presentationRoot.anchorMax = Vector2.one;
            presentationRoot.offsetMin = Vector2.zero;
            presentationRoot.offsetMax = Vector2.zero;
            presentationRoot.SetAsFirstSibling();

            presentationGroup = root.GetComponent<CanvasGroup>();
            presentationGroup.alpha = 0f;
            presentationGroup.interactable = false;
            presentationGroup.blocksRaycasts = false;

            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            Sprite ring = CreateRingSprite();

            wash = CreateImage(
                "InkVeil",
                presentationRoot,
                null,
                Vector2.zero,
                new Vector2(1400f, 2300f),
                Vector3.zero);
            wash.rectTransform.anchorMin = Vector2.zero;
            wash.rectTransform.anchorMax = Vector2.one;
            wash.rectTransform.offsetMin = Vector2.zero;
            wash.rectTransform.offsetMax = Vector2.zero;

            upperBrush = CreateImage(
                "UpperDiagonalBrush",
                presentationRoot,
                brush,
                new Vector2(-1320f, 590f),
                new Vector2(1680f, 420f),
                new Vector3(0f, 0f, -9f));
            lowerBrush = CreateImage(
                "LowerDiagonalBrush",
                presentationRoot,
                brush,
                new Vector2(1320f, -590f),
                new Vector2(1680f, 420f),
                new Vector3(0f, 0f, -9f));

            outerRing = CreateImage(
                "OuterInkRing",
                presentationRoot,
                ring,
                Vector2.zero,
                new Vector2(610f, 610f),
                Vector3.zero);
            innerRing = CreateImage(
                "InnerInkRing",
                presentationRoot,
                ring,
                Vector2.zero,
                new Vector2(480f, 480f),
                new Vector3(0f, 0f, 17f));

            slashPrimary = CreateImage(
                "PrimaryRevealSlash",
                presentationRoot,
                brush,
                Vector2.zero,
                new Vector2(920f, 25f),
                new Vector3(0f, 0f, -11f));
            slashSecondary = CreateImage(
                "SecondaryRevealSlash",
                presentationRoot,
                brush,
                new Vector2(0f, -8f),
                new Vector2(760f, 13f),
                new Vector3(0f, 0f, 9f));

            splash = CreateImage(
                "UnlockInkSplash",
                presentationRoot,
                blob,
                Vector2.zero,
                new Vector2(520f, 430f),
                new Vector3(0f, 0f, -4f));

            lockPlate = CreateImage(
                "LockedInkPlate",
                presentationRoot,
                blob,
                Vector2.zero,
                new Vector2(190f, 150f),
                new Vector3(0f, 0f, 7f));
            lockText = CreateText(
                "LockedLabel",
                lockPlate.transform,
                "잠금",
                34,
                Vector2.zero,
                new Vector2(150f, 76f),
                FontStyle.Bold);

            unlockedIconPlate = CreateImage(
                "UnlockedIconPlate",
                presentationRoot,
                blob,
                new Vector2(0f, 62f),
                new Vector2(182f, 158f),
                new Vector3(0f, 0f, -3f));
            unlockedIcon = CreateImage(
                "UnlockedGrowthIcon",
                presentationRoot,
                null,
                new Vector2(0f, 62f),
                new Vector2(124f, 124f),
                Vector3.zero);

            titleText = CreateText(
                "UnlockTitle",
                presentationRoot,
                "성장 해금",
                58,
                new Vector2(0f, -70f),
                new Vector2(700f, 90f),
                FontStyle.Bold);
            subtitleText = CreateText(
                "UnlockSubtitle",
                presentationRoot,
                "새 먹결이 열렸습니다",
                30,
                new Vector2(0f, -146f),
                new Vector2(760f, 64f),
                FontStyle.Bold);

            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = CreateImage(
                    $"UnlockDrop{i + 1:00}",
                    presentationRoot,
                    blob,
                    Vector2.zero,
                    new Vector2(30f, 24f),
                    Vector3.zero);
                drops[i].gameObject.SetActive(false);
            }

            nodeFeedbackRoot = CreateRect(
                "NodeFruitFeedback",
                presentationRoot,
                Vector2.zero,
                new Vector2(220f, 220f));
            nodeFruitGlow = CreateImage(
                "FruitGlow",
                nodeFeedbackRoot,
                ring,
                Vector2.zero,
                new Vector2(190f, 190f),
                Vector3.zero);
            nodeFruit = CreateImage(
                "FruitBloom",
                nodeFeedbackRoot,
                blob,
                Vector2.zero,
                new Vector2(112f, 112f),
                Vector3.zero);
            for (int i = 0; i < nodeDrops.Length; i++)
            {
                nodeDrops[i] = CreateImage(
                    $"FruitDrop{i + 1:00}",
                    nodeFeedbackRoot,
                    blob,
                    Vector2.zero,
                    new Vector2(22f, 18f),
                    Vector3.zero);
                nodeDrops[i].gameObject.SetActive(false);
            }

            ResetPresentation();
        }

        public void Play(string growthName, Sprite growthIcon = null)
        {
            PlayInternal(
                growthName,
                growthIcon,
                level: 1,
                isUpgrade: false,
                showNodeFeedback: false,
                nodePosition: Vector2.zero,
                fruitSprite: null);
        }

        public void PlayAtNode(
            string growthName,
            Sprite growthIcon,
            Vector2 nodePosition,
            Sprite fruitSprite)
        {
            PlayInternal(
                growthName,
                growthIcon,
                level: 1,
                isUpgrade: false,
                showNodeFeedback: true,
                nodePosition: nodePosition,
                fruitSprite: fruitSprite);
        }

        public void PlayUpgrade(
            string growthName,
            Sprite growthIcon,
            int level)
        {
            PlayInternal(
                growthName,
                growthIcon,
                Mathf.Max(1, level),
                isUpgrade: true,
                showNodeFeedback: false,
                nodePosition: Vector2.zero,
                fruitSprite: null);
        }

        void PlayInternal(
            string growthName,
            Sprite growthIcon,
            int level,
            bool isUpgrade,
            bool showNodeFeedback,
            Vector2 nodePosition,
            Sprite fruitSprite)
        {
            if (presentationRoot == null)
                return;

            playSerial++;
            elapsed = 0f;
            upgradeMode = isUpgrade;
            activeDuration = isUpgrade
                ? UpgradeSequenceDuration
                : SequenceDuration;
            playing = true;
            activeDecorativeDrops =
                VfxQualityRuntime.Profile.ScaleDecorativeCount(
                    MaxDecorativeDrops,
                    minimum: 3);
            hasNodeFeedback = showNodeFeedback;
            activeNodeDrops = showNodeFeedback
                ? VfxQualityRuntime.Profile.ScaleDecorativeCount(
                    MaxNodeDrops,
                    minimum: 0)
                : 0;
            if (nodeFeedbackRoot != null)
            {
                nodeFeedbackRoot.anchoredPosition = nodePosition;
                nodeFeedbackRoot.gameObject.SetActive(showNodeFeedback);
            }
            if (nodeFruit != null && fruitSprite != null)
                nodeFruit.sprite = fruitSprite;
            unlockedIcon.sprite = growthIcon;
            unlockedIconPlate.gameObject.SetActive(growthIcon != null);
            unlockedIcon.gameObject.SetActive(growthIcon != null);
            lockText.text = isUpgrade
                ? $"Lv. {level}"
                : "잠금";
            titleText.text = isUpgrade
                ? "성장 강화"
                : "성장 해금";
            subtitleText.text = BuildSubtitle(
                growthName,
                level,
                isUpgrade);
            PrepareDrops();
            PrepareNodeDrops();
            presentationGroup.alpha = 1f;
            ApplyFrame(0f);
        }

        public void ResetPresentation()
        {
            playing = false;
            elapsed = 0f;
            activeDecorativeDrops = 0;
            activeNodeDrops = 0;
            upgradeMode = false;
            hasNodeFeedback = false;
            activeDuration = SequenceDuration;
            if (presentationGroup != null)
            {
                presentationGroup.alpha = 0f;
                presentationGroup.interactable = false;
                presentationGroup.blocksRaycasts = false;
            }

            for (int i = 0; i < drops.Length; i++)
                if (drops[i] != null)
                    drops[i].gameObject.SetActive(false);
            for (int i = 0; i < nodeDrops.Length; i++)
                if (nodeDrops[i] != null)
                    nodeDrops[i].gameObject.SetActive(false);
            if (nodeFeedbackRoot != null)
                nodeFeedbackRoot.gameObject.SetActive(false);
            if (unlockedIcon != null)
                unlockedIcon.gameObject.SetActive(false);
            if (unlockedIconPlate != null)
                unlockedIconPlate.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            ResetPresentation();
        }

        void Update()
        {
            if (!playing)
                return;

            // 프레임 수가 아니라 실제 비정지 시간을 따라가야 저사양 기기에서
            // 암막 연출이 두 배 이상 길어지지 않는다.
            elapsed = Mathf.Min(
                activeDuration,
                elapsed + Mathf.Max(0f, Time.unscaledDeltaTime));
            ApplyFrame(ToAuthoredTimeline(elapsed));
        }

        void PrepareDrops()
        {
            for (int i = 0; i < drops.Length; i++)
            {
                bool active = i < activeDecorativeDrops;
                drops[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                float angleDegrees =
                    i * 137.5f + playSerial * 19f;
                float angle = angleDegrees * Mathf.Deg2Rad;
                dropDirections[i] =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                dropDistances[i] = 150f + (i % 4) * 44f;
                dropDelays[i] = (i % 3) * 0.025f;
                dropRotations[i] = angleDegrees;
            }
        }

        void PrepareNodeDrops()
        {
            for (int i = 0; i < nodeDrops.Length; i++)
            {
                bool active = hasNodeFeedback && i < activeNodeDrops;
                nodeDrops[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                float angleDegrees =
                    22f + i * 91f + playSerial * 17f;
                float angle = angleDegrees * Mathf.Deg2Rad;
                nodeDropDirections[i] =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                nodeDropRotations[i] = angleDegrees;
            }
        }

        void ApplyFrame(float seconds)
        {
            if (presentationGroup == null)
                return;

            float enter = SmoothRange(0f, 0.18f, seconds);
            float exit = SmoothRange(0.96f, SequenceDuration, seconds);
            float frameVisibility = enter * (1f - exit);

            presentationGroup.alpha = 1f;
            presentationGroup.interactable = false;
            presentationGroup.blocksRaycasts = false;
            float washAlpha = upgradeMode ? 0.44f : 0.54f;
            SetImageColor(
                wash,
                InkPalette.Ink,
                washAlpha * frameVisibility);

            upperBrush.rectTransform.anchoredPosition =
                Vector2.Lerp(
                    new Vector2(-1320f, 590f),
                    new Vector2(-40f, 590f),
                    EaseOutCubic(enter)) +
                Vector2.right * (1340f * EaseInCubic(exit));
            lowerBrush.rectTransform.anchoredPosition =
                Vector2.Lerp(
                    new Vector2(1320f, -590f),
                    new Vector2(40f, -590f),
                    EaseOutCubic(enter)) +
                Vector2.left * (1340f * EaseInCubic(exit));
            SetImageColor(
                upperBrush,
                InkPalette.Ink,
                0.98f * frameVisibility);
            SetImageColor(
                lowerBrush,
                InkPalette.Ink,
                0.98f * frameVisibility);

            float lockVisibility =
                SmoothRange(0.05f, 0.13f, seconds) *
                (1f - SmoothRange(0.24f, 0.34f, seconds)) *
                (1f - exit);
            float lockScale =
                Mathf.Lerp(0.72f, 1f, EaseOutCubic(enter)) *
                Mathf.Lerp(1f, 0.7f, SmoothRange(0.24f, 0.34f, seconds));
            lockPlate.rectTransform.localScale = Vector3.one * lockScale;
            SetImageColor(lockPlate, InkPalette.Ink, lockVisibility);
            SetTextColor(lockText, InkPalette.Gold, lockVisibility);

            float impact = SmoothRange(0.27f, 0.47f, seconds);
            float contentFade =
                1f - SmoothRange(0.98f, 1.14f, seconds);
            float impactVisibility =
                SmoothRange(0.27f, 0.31f, seconds) * contentFade;
            float splashScale =
                Mathf.LerpUnclamped(0.18f, 1f, EaseOutBack(impact));
            splash.rectTransform.localScale = Vector3.one * splashScale;
            SetImageColor(
                splash,
                InkPalette.Ink,
                0.94f * impactVisibility);

            ApplyRing(
                outerRing,
                seconds,
                0.30f,
                0.60f,
                0.52f,
                1.34f,
                InkPalette.Gold,
                0.9f);
            ApplyRing(
                innerRing,
                seconds,
                0.37f,
                0.52f,
                0.62f,
                1.24f,
                InkPalette.Paper,
                0.62f);

            float slashProgress =
                EaseOutCubic(SmoothRange(0.29f, 0.42f, seconds));
            float slashVisibility =
                SmoothRange(0.29f, 0.36f, seconds) *
                (1f - SmoothRange(0.76f, 1.02f, seconds));
            slashPrimary.rectTransform.localScale =
                new Vector3(Mathf.Lerp(0.04f, 1f, slashProgress), 1f, 1f);
            slashSecondary.rectTransform.localScale =
                new Vector3(Mathf.Lerp(0.04f, 1f, slashProgress), 1f, 1f);
            SetImageColor(
                slashPrimary,
                InkPalette.Paper,
                0.88f * slashVisibility);
            SetImageColor(
                slashSecondary,
                InkPalette.Gold,
                0.92f * slashVisibility);

            float iconVisibility =
                SmoothRange(0.32f, 0.43f, seconds) * contentFade;
            float iconScale =
                Mathf.Lerp(
                    0.58f,
                    1f,
                    EaseOutBack(iconVisibility));
            unlockedIconPlate.rectTransform.localScale =
                Vector3.one * iconScale;
            unlockedIcon.rectTransform.localScale =
                Vector3.one *
                Mathf.Lerp(0.7f, 1f, EaseOutBack(iconVisibility));
            SetImageColor(
                unlockedIconPlate,
                InkPalette.Paper,
                0.98f * iconVisibility);
            SetImageColor(
                unlockedIcon,
                Color.white,
                iconVisibility);

            float titleVisibility =
                SmoothRange(0.34f, 0.45f, seconds) * contentFade;
            titleText.rectTransform.anchoredPosition =
                Vector2.Lerp(
                    new Vector2(0f, -90f),
                    new Vector2(0f, -70f),
                    EaseOutCubic(titleVisibility));
            SetTextColor(titleText, InkPalette.TextLight, titleVisibility);

            float subtitleVisibility =
                SmoothRange(0.43f, 0.56f, seconds) * contentFade;
            SetTextColor(
                subtitleText,
                InkPalette.Paper2,
                0.96f * subtitleVisibility);

            ApplyDrops(seconds, contentFade);
            ApplyNodeFruitFeedback(seconds, contentFade);

            if (seconds < SequenceDuration)
                return;

            ResetPresentation();
        }

        void ApplyNodeFruitFeedback(float seconds, float contentFade)
        {
            if (!hasNodeFeedback || nodeFeedbackRoot == null)
                return;

            float appear = SmoothRange(0.28f, 0.46f, seconds);
            float settle = SmoothRange(0.48f, 0.78f, seconds);
            float disappear =
                SmoothRange(0.98f, SequenceDuration, seconds);
            float visibility =
                appear * (1f - disappear) * contentFade;

            float fruitScale = Mathf.Lerp(
                0.42f,
                1.16f,
                EaseOutBack(appear));
            fruitScale = Mathf.Lerp(
                fruitScale,
                1f,
                EaseOutCubic(settle));
            nodeFruit.rectTransform.localScale =
                Vector3.one * fruitScale;
            SetImageColor(
                nodeFruit,
                InkPalette.Red,
                0.98f * visibility);

            float glowProgress =
                Mathf.Clamp01((seconds - 0.30f) / 0.64f);
            nodeFruitGlow.rectTransform.localScale =
                Vector3.one *
                Mathf.Lerp(
                    0.48f,
                    1.58f,
                    EaseOutCubic(glowProgress));
            SetImageColor(
                nodeFruitGlow,
                InkPalette.Red,
                0.66f *
                SmoothRange(0.30f, 0.40f, seconds) *
                (1f - EaseInCubic(glowProgress)) *
                contentFade);

            for (int i = 0; i < nodeDrops.Length; i++)
            {
                Image drop = nodeDrops[i];
                if (i >= activeNodeDrops)
                {
                    drop.gameObject.SetActive(false);
                    continue;
                }

                float start = 0.34f + i * 0.018f;
                float progress =
                    Mathf.Clamp01((seconds - start) / 0.48f);
                drop.rectTransform.anchoredPosition =
                    nodeDropDirections[i] *
                    Mathf.Lerp(18f, 92f + i * 9f, EaseOutCubic(progress));
                drop.rectTransform.localScale =
                    Vector3.one *
                    Mathf.Lerp(1f, 0.42f, EaseInCubic(progress));
                drop.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, nodeDropRotations[i]);
                SetImageColor(
                    drop,
                    InkPalette.Ink,
                    SmoothRange(start, start + 0.05f, seconds) *
                    (1f - SmoothRange(
                        start + 0.24f,
                        start + 0.48f,
                        seconds)) *
                    contentFade);
            }
        }

        void ApplyRing(
            Image ring,
            float seconds,
            float start,
            float duration,
            float startScale,
            float endScale,
            Color color,
            float maxAlpha)
        {
            float progress = Mathf.Clamp01((seconds - start) / duration);
            float visibility =
                SmoothRange(start, start + 0.08f, seconds) *
                (1f - EaseInCubic(progress));
            ring.rectTransform.localScale =
                Vector3.one *
                Mathf.Lerp(startScale, endScale, EaseOutCubic(progress));
            SetImageColor(ring, color, maxAlpha * visibility);
        }

        void ApplyDrops(float seconds, float contentFade)
        {
            for (int i = 0; i < drops.Length; i++)
            {
                Image drop = drops[i];
                if (i >= activeDecorativeDrops)
                {
                    drop.gameObject.SetActive(false);
                    continue;
                }

                float start = 0.31f + dropDelays[i];
                float progress = Mathf.Clamp01((seconds - start) / 0.58f);
                float eased = EaseOutCubic(progress);
                drop.rectTransform.anchoredPosition =
                    dropDirections[i] *
                    Mathf.Lerp(34f, dropDistances[i], eased);
                float scale =
                    Mathf.Lerp(1.12f, 0.48f, EaseInCubic(progress));
                drop.rectTransform.localScale = Vector3.one * scale;
                drop.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, dropRotations[i]);
                Color color = i % 5 == 0
                    ? InkPalette.Gold
                    : i % 7 == 0
                        ? InkPalette.Paper
                        : InkPalette.Ink;
                float visibility =
                    SmoothRange(start, start + 0.06f, seconds) *
                    (1f - SmoothRange(start + 0.30f, start + 0.58f, seconds)) *
                    contentFade;
                SetImageColor(drop, color, visibility);
            }
        }

#if UNITY_EDITOR
        public void EvaluateForTests(float seconds)
        {
            if (presentationRoot == null)
                return;
            playing = true;
            elapsed = Mathf.Clamp(
                seconds,
                0f,
                activeDuration);
            ApplyFrame(ToAuthoredTimeline(elapsed));
        }
#endif

        float ToAuthoredTimeline(float actualSeconds)
        {
            return Mathf.Clamp01(
                       actualSeconds /
                       Mathf.Max(0.01f, activeDuration)) *
                   SequenceDuration;
        }

        static string BuildSubtitle(
            string growthName,
            int level,
            bool isUpgrade)
        {
            if (isUpgrade)
            {
                return string.IsNullOrWhiteSpace(growthName)
                    ? $"먹결 · Lv. {level} 완성"
                    : $"{growthName} · Lv. {level} 완성";
            }

            return string.IsNullOrWhiteSpace(growthName)
                ? "새 먹결이 열렸습니다"
                : $"{growthName} · 새 먹결이 열렸습니다";
        }

        static Sprite CreateRingSprite()
        {
            if (ringSprite != null)
                return ringSprite;

            const int size = 128;
            ringTexture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "MukJump_GrowthUnlockRing",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1) - 0.5f) * 2f;
                float ny = (y / (float)(size - 1) - 0.5f) * 2f;
                float angle = Mathf.Atan2(ny, nx);
                float noise = Mathf.PerlinNoise(
                    Mathf.Cos(angle) * 1.7f + 2.1f,
                    Mathf.Sin(angle) * 1.7f + 3.4f);
                float radius = 0.76f + (noise - 0.5f) * 0.07f;
                float thickness = 0.045f + noise * 0.025f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float band = Mathf.Abs(distance - radius);
                float alpha = Mathf.Clamp01(
                    (thickness - band) * 45f);
                pixels[y * size + x] =
                    new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
            }
            ringTexture.SetPixels32(pixels);
            ringTexture.Apply(false, true);
            ringSprite = Sprite.Create(
                ringTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            ringSprite.name = "MukJump_GrowthUnlockRing";
            return ringSprite;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Vector3 rotation)
        {
            var go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localEulerAngles = rotation;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            FontStyle style)
        {
            var go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.color = Color.clear;
            return text;
        }

        static void SetImageColor(Image image, Color baseColor, float alpha)
        {
            if (image == null)
                return;
            baseColor.a = Mathf.Clamp01(alpha);
            image.color = baseColor;
        }

        static void SetTextColor(Text text, Color baseColor, float alpha)
        {
            if (text == null)
                return;
            baseColor.a = Mathf.Clamp01(alpha);
            text.color = baseColor;
        }

        static float SmoothRange(float start, float end, float value)
        {
            if (end <= start)
                return value >= end ? 1f : 0f;
            float t = Mathf.Clamp01((value - start) / (end - start));
            return t * t * (3f - 2f * t);
        }

        static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        static float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }

        static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f +
                   (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        static void DestroyRuntimeObject(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
    }
}
