using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 게임 종료 결과를 먹 웅덩이 팝업으로 표시한다.
    /// MonoBehaviour 파일명과 클래스명을 일치시켜 씬 직렬화 시 Missing Script를 방지한다.
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
}
