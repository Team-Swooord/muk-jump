using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 세 번의 먹붓으로 화면을 덮고 암전 중 상태를 교체한 뒤 다음 화면을 드러낸다.
    public sealed class BrushTransitionView : MonoBehaviour
    {
        const float CoverDuration = 1.15f;
        const float BlackHoldDuration = 0.22f;
        const float RevealDuration = 0.7f;

        CanvasGroup group;
        Image wash;
        RectTransform[] strokeMasks;
        float[] strokeHeights;
        bool playing;

        public bool IsPlaying => playing;

        void Awake() => BuildIfNeeded();

        public void Play(Action onCovered)
        {
            if (playing) return;
            BuildIfNeeded();
            StartCoroutine(PlayRoutine(onCovered));
        }

        void BuildIfNeeded()
        {
            if (group != null) return;

            var root = new GameObject("BrushTransitionCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            wash = CreateImage("InkWash", root.transform, null, Vector2.zero,
                new Vector2(1500f, 2400f), 0f);
            wash.color = InkPalette.Ink;
            wash.canvasRenderer.SetAlpha(0f);

            var textures = LoadBrushTextures();
            var positions = new[]
            {
                new Vector2(-275f, 760f), new Vector2(250f, 650f),
                new Vector2(-355f, 210f), new Vector2(-120f, 120f),
                new Vector2(120f, 40f), new Vector2(355f, -70f),
                new Vector2(-245f, -720f), new Vector2(270f, -790f),
            };
            var sizes = new[]
            {
                new Vector2(710f, 232f), new Vector2(760f, 214f),
                new Vector2(455f, 810f), new Vector2(450f, 812f),
                new Vector2(330f, 998f), new Vector2(365f, 1103f),
                new Vector2(680f, 363f), new Vector2(720f, 284f),
            };
            strokeMasks = new RectTransform[textures.Length];
            strokeHeights = new float[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                strokeMasks[i] = CreateMaskedStroke($"Brush_{i + 1:00}", root.transform,
                    textures[i], positions[i], sizes[i]);
                strokeHeights[i] = sizes[i].y;
            }
        }

        IEnumerator PlayRoutine(Action onCovered)
        {
            playing = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            wash.canvasRenderer.SetAlpha(0f);

            float elapsed = 0f;
            while (elapsed < CoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CoverDuration);
                for (int i = 0; i < strokeMasks.Length; i++)
                {
                    float start = i switch
                    {
                        0 => 0f, 1 => 0.07f, 2 => 0.18f, 3 => 0.27f,
                        4 => 0.37f, 5 => 0.47f, 6 => 0.66f, _ => 0.73f,
                    };
                    SetStrokeProgress(i, Smooth01((t - start) / 0.3f));
                }
                wash.canvasRenderer.SetAlpha(Smooth01((t - 0.9f) / 0.1f));
                yield return null;
            }

            wash.canvasRenderer.SetAlpha(1f);
            onCovered?.Invoke();
            yield return new WaitForSecondsRealtime(BlackHoldDuration);

            elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / RevealDuration);
                group.alpha = 1f - Smooth01(t);
                yield return null;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            for (int i = 0; i < strokeMasks.Length; i++) SetStrokeProgress(i, 0f);
            playing = false;
        }

        void SetStrokeProgress(int index, float progress)
        {
            Vector2 size = strokeMasks[index].sizeDelta;
            size.y = strokeHeights[index] * progress;
            strokeMasks[index].sizeDelta = size;
        }

        static Texture2D[] LoadBrushTextures()
        {
            var textures = new Texture2D[8];
            for (int i = 0; i < textures.Length; i++)
            {
                string number = (i + 1).ToString("00");
                string suffix = i switch
                {
                    0 => "top_left", 1 => "top_right", 2 => "left_vertical",
                    3 => "center_left_vertical", 4 => "center_right_vertical",
                    5 => "right_vertical", 6 => "bottom_left", _ => "bottom_right",
                };
                textures[i] = Resources.Load<Texture2D>($"MukJump/BrushTransitions/brush_stroke_{number}_{suffix}");
                if (textures[i] == null)
                    textures[i] = InkUiTextureFactory.CreateBrushSprite().texture;
            }
            return textures;
        }

        static RectTransform CreateMaskedStroke(string objectName, Transform parent, Texture texture,
            Vector2 centerPosition, Vector2 fullSize)
        {
            var maskObject = new GameObject(objectName, typeof(RectTransform), typeof(RectMask2D));
            var mask = maskObject.GetComponent<RectTransform>();
            mask.SetParent(parent, false);
            mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f);
            mask.pivot = new Vector2(0.5f, 1f);
            mask.anchoredPosition = centerPosition + Vector2.up * (fullSize.y * 0.5f);
            mask.sizeDelta = new Vector2(fullSize.x, 0f);

            var imageObject = new GameObject("InkStroke", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(mask, false);
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 1f);
            imageRect.pivot = new Vector2(0.5f, 1f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = fullSize;
            var rawImage = imageObject.GetComponent<RawImage>();
            rawImage.texture = texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
            return mask;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position,
            Vector2 size, float rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = InkPalette.Ink;
            image.raycastTarget = false;
            return image;
        }
    }

    /// 게임 종료 결과를 먹 웅덩이 팝업으로 표시한다.
    public sealed class GameOverPopupView : MonoBehaviour
    {
        CanvasGroup rootGroup;
        RectTransform panel;
        RectTransform heightRect;
        Text heightText;
        Text bestText;
        Text newBestText;
        Image bestGlow;
        Coroutine showRoutine;

        void Awake() => BuildIfNeeded();

        public void Show(int height, int best, bool reachedNewBest)
        {
            BuildIfNeeded();
            heightText.text = $"고도 {height}m";
            bestText.text = $"최고 {best}m";
            newBestText.gameObject.SetActive(reachedNewBest);
            bestGlow.gameObject.SetActive(reachedNewBest);
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowRoutine(reachedNewBest));
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var root = new GameObject("GameOverPopupCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;

            var backdrop = CreateImage("InkWash", root.transform, null, Vector2.zero,
                new Vector2(1400f, 2300f), new Color(0.04f, 0.038f, 0.034f, 0.6f));
            backdrop.raycastTarget = false;

            var shadow = CreateImage("InkShadow", root.transform, InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(22f, -28f), new Vector2(900f, 980f), new Color(0f, 0f, 0f, 0.28f));
            shadow.raycastTarget = false;

            var panelImage = CreateImage("InkResultPopup", root.transform,
                InkUiTextureFactory.CreateBlobSprite(), Vector2.zero, new Vector2(880f, 960f),
                new Color(0.045f, 0.043f, 0.038f, 0.99f));
            panel = panelImage.rectTransform;

            CreateText("Title", panel, "게임 종료", 62, new Vector2(0f, 250f),
                new Vector2(620f, 100f), InkPalette.Paper, FontStyle.Bold);
            heightText = CreateText("Height", panel, "고도 0m", 88, Vector2.zero,
                new Vector2(700f, 130f), InkPalette.Paper, FontStyle.Bold);
            heightRect = heightText.rectTransform;

            bestGlow = CreateImage("BestGlow", panel, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, -110f), new Vector2(570f, 105f), new Color(0.93f, 0.72f, 0.23f, 0.34f));
            bestGlow.transform.SetAsFirstSibling();
            bestText = CreateText("Best", panel, "최고 0m", 44, new Vector2(0f, -110f),
                new Vector2(560f, 80f), InkPalette.Paper, FontStyle.Bold);
            newBestText = CreateText("NewBest", panel, "최고 점수 달성!", 42,
                new Vector2(0f, -215f), new Vector2(650f, 80f),
                new Color(1f, 0.82f, 0.35f, 1f), FontStyle.Bold);
            CreateText("TouchHint", panel, "화면을 터치해 메인으로", 30,
                new Vector2(0f, -325f), new Vector2(700f, 70f),
                new Color(InkPalette.Paper.r, InkPalette.Paper.g, InkPalette.Paper.b, 0.72f),
                FontStyle.Normal);
        }

        IEnumerator ShowRoutine(bool reachedNewBest)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            panel.localScale = Vector3.one * 0.72f;
            heightRect.anchoredPosition = new Vector2(0f, 330f);

            float elapsed = 0f;
            const float duration = 0.82f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Smooth01(t / 0.55f);
                rootGroup.alpha = appear;
                panel.localScale = Vector3.one * (Mathf.Lerp(0.72f, 1f, appear) +
                    Mathf.Sin(appear * Mathf.PI) * 0.055f);

                float fall = 1f - Mathf.Pow(1f - Mathf.Clamp01((t - 0.18f) / 0.72f), 3f);
                float bounce = Mathf.Sin(fall * Mathf.PI * 2f) * (1f - fall) * 24f;
                heightRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(330f, 15f, fall) + bounce);
                yield return null;
            }

            rootGroup.alpha = 1f;
            panel.localScale = Vector3.one;
            heightRect.anchoredPosition = new Vector2(0f, 15f);

            while (reachedNewBest)
            {
                float pulse = 0.84f + 0.16f * Mathf.Sin(Time.unscaledTime * 7f);
                newBestText.color = new Color(1f, 0.82f, 0.35f, pulse);
                bestGlow.color = new Color(0.93f, 0.72f, 0.23f, 0.22f + pulse * 0.22f);
                bestGlow.rectTransform.localScale = Vector3.one * (0.96f + pulse * 0.08f);
                yield return null;
            }
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static Image CreateImage(string objectName, Transform parent, Sprite sprite,
            Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(string objectName, Transform parent, string value, int fontSize,
            Vector2 position, Vector2 size, Color color, FontStyle style)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.alignByGeometry = true;
            return text;
        }
    }

    /// UI 전환과 팝업이 공유하는 저비용 절차적 먹 마스크. 최초 한 번만 생성한다.
    static class InkUiTextureFactory
    {
        static Sprite brushSprite;
        static Sprite blobSprite;

        public static Sprite CreateBrushSprite()
        {
            if (brushSprite != null) return brushSprite;
            const int width = 512;
            const int height = 160;
            var texture = NewTexture(width, height, "MukJump_BrushMask");
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = Mathf.Abs(y / (float)(height - 1) - 0.5f) * 2f;
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Min(u / 0.08f, (1f - u) / 0.11f));
                float edge = 0.88f - v + (Mathf.PerlinNoise(u * 18f, y * 0.075f) - 0.5f) * 0.24f;
                float fibers = Mathf.PerlinNoise(u * 7f, y * 0.31f);
                float alpha = edge > 0f ? taper * Mathf.Clamp01(edge * 5f) : 0f;
                if (fibers < 0.13f && v > 0.35f) alpha *= 0.2f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(false, true);
            brushSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            brushSprite.name = "MukJump_BrushMask";
            return brushSprite;
        }

        public static Sprite CreateBlobSprite()
        {
            if (blobSprite != null) return blobSprite;
            const int size = 512;
            var texture = NewTexture(size, size, "MukJump_InkBlobMask");
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1) - 0.5f) * 2f;
                float ny = (y / (float)(size - 1) - 0.5f) * 2f;
                float angle = Mathf.Atan2(ny, nx);
                float noise = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.8f + 2.3f,
                    Mathf.Sin(angle) * 1.8f + 3.7f);
                float radius = 0.83f + (noise - 0.5f) * 0.22f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01((radius - distance) * 35f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(false, true);
            blobSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            blobSprite.name = "MukJump_InkBlobMask";
            return blobSprite;
        }

        static Texture2D NewTexture(int width, int height, string textureName)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
    }
}
