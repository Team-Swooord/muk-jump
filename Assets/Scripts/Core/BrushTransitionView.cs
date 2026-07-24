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
        RectTransform[] strokeImages;
        float[] strokeHeights;
        bool playing;
        static bool revealAfterSceneLoad;

        public bool IsPlaying => playing;

        void Awake()
        {
            BuildIfNeeded();
            if (revealAfterSceneLoad)
            {
                revealAfterSceneLoad = false;
                StartCoroutine(RevealLoadedSceneRoutine());
            }
        }

        public static void RequestRevealAfterSceneLoad() => revealAfterSceneLoad = true;

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
                new Vector2(-250f, 720f), new Vector2(250f, 650f),
                new Vector2(-400f, 0f), new Vector2(-140f, -50f),
                new Vector2(130f, -120f), new Vector2(400f, -200f),
                new Vector2(-245f, -720f), new Vector2(270f, -790f),
            };
            var sizes = new[]
            {
                new Vector2(1650f, 538f), new Vector2(1750f, 492f),
                new Vector2(1100f, 1960f), new Vector2(1100f, 1980f),
                new Vector2(850f, 2300f), new Vector2(900f, 2700f),
                new Vector2(1500f, 800f), new Vector2(1600f, 630f),
            };
            strokeMasks = new RectTransform[textures.Length];
            strokeImages = new RectTransform[textures.Length];
            strokeHeights = new float[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                strokeMasks[i] = CreateMaskedStroke($"Brush_{i + 1:00}", root.transform,
                    textures[i], positions[i], sizes[i], out strokeImages[i]);
                strokeHeights[i] = sizes[i].y;
            }
        }

        IEnumerator PlayRoutine(Action onCovered)
        {
            playing = true;
            GameFeedbackController.Instance?.PlayBrushTransition();
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
                        0 => 0f, 1 => 0.1f, 2 => 0.26f, 3 => 0.31f,
                        4 => 0.38f, 5 => 0.45f, 6 => 0.64f, _ => 0.72f,
                    };
                    float duration = i < 2 ? 0.23f : i < 6 ? 0.26f : 0.22f;
                    SetStrokeProgress(i, BrushEase((t - start) / duration));
                }
                float washProgress = Smooth01((t - 0.76f) / 0.24f);
                wash.canvasRenderer.SetAlpha(washProgress * washProgress);
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

        IEnumerator RevealLoadedSceneRoutine()
        {
            playing = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            for (int i = 0; i < strokeMasks.Length; i++) SetStrokeProgress(i, 1f);
            wash.canvasRenderer.SetAlpha(1f);
            yield return new WaitForSecondsRealtime(0.08f);

            float elapsed = 0f;
            const float duration = 0.68f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth01(elapsed / duration);
                group.alpha = 1f - t;
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
            // 실제 붓털이 마스크 선두를 조금 뒤따라오는 듯한 짧은 끌림.
            strokeImages[index].anchoredPosition = new Vector2(0f, Mathf.Lerp(46f, 0f, progress));
        }

        static float BrushEase(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
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
            Vector2 centerPosition, Vector2 fullSize, out RectTransform imageRect)
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
            imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(mask, false);
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 1f);
            imageRect.pivot = new Vector2(0.5f, 1f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = fullSize;
            var rawImage = imageObject.GetComponent<RawImage>();
            rawImage.texture = texture;
            // 원본 RGB에 예기치 않은 색이 포함돼도 전환은 항상 먹색으로만 보인다.
            rawImage.color = InkPalette.Ink;
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
