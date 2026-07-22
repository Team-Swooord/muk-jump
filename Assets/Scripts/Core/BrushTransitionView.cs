using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 세 번의 먹붓으로 화면을 덮고 암전 중 상태를 교체한 뒤 다음 화면을 드러낸다.
    public sealed class BrushTransitionView : MonoBehaviour
    {
        const float CoverDuration = 0.8f;
        const float BlackHoldDuration = 0.22f;
        const float RevealDuration = 0.7f;

        CanvasGroup group;
        Image wash;
        RectTransform[] strokes;
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

            var brushSprite = InkUiTextureFactory.CreateBrushSprite();
            strokes = new RectTransform[3];
            strokes[0] = CreateImage("Brush_Curve", root.transform, brushSprite,
                new Vector2(-80f, 260f), new Vector2(1850f, 720f), -34f).rectTransform;
            strokes[1] = CreateImage("Brush_Cross", root.transform, brushSprite,
                new Vector2(100f, -110f), new Vector2(1900f, 760f), 38f).rectTransform;
            strokes[2] = CreateImage("Brush_Finish", root.transform, brushSprite,
                new Vector2(0f, 0f), new Vector2(2100f, 1040f), -8f).rectTransform;
            for (int i = 0; i < strokes.Length; i++)
                strokes[i].localScale = new Vector3(0f, 1f, 1f);
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
                SetStrokeProgress(0, Smooth01(t / 0.46f));
                SetStrokeProgress(1, Smooth01((t - 0.22f) / 0.5f));
                SetStrokeProgress(2, Smooth01((t - 0.53f) / 0.47f));
                wash.canvasRenderer.SetAlpha(Smooth01((t - 0.82f) / 0.18f));
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
            for (int i = 0; i < strokes.Length; i++)
                strokes[i].localScale = new Vector3(0f, 1f, 1f);
            playing = false;
        }

        void SetStrokeProgress(int index, float progress)
        {
            strokes[index].localScale = new Vector3(progress, 1f, 1f);
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
