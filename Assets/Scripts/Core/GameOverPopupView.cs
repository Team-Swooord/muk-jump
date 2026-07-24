using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 게임 종료 결과를 현재 고도와 최고 고도로 나눠 간결하게 표시한다.
    /// MonoBehaviour 파일명과 클래스명을 일치시켜 씬 직렬화 시 Missing Script를 방지한다.
    public sealed class GameOverPopupView : MonoBehaviour
    {
        CanvasGroup rootGroup;
        RectTransform panel;
        Text heightText;
        Text bestText;
        Text newBestText;
        Image bestGlow;
        Coroutine showRoutine;

        void Awake() => BuildIfNeeded();

        public void Show(int height, int best, bool reachedNewBest)
        {
            BuildIfNeeded();
            heightText.text = $"{height} m";
            bestText.text = $"{best} m";
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
                new Vector2(1400f, 2300f), new Color(0.04f, 0.038f, 0.034f, 0.48f));
            backdrop.raycastTarget = false;

            var shadow = CreateImage("ResultShadow", root.transform, null,
                new Vector2(18f, -22f), new Vector2(866f, 926f), new Color(0f, 0f, 0f, 0.24f));
            shadow.raycastTarget = false;

            var panelImage = CreateImage("ResultOutline", root.transform, null,
                Vector2.zero, new Vector2(850f, 910f), InkPalette.Ink);
            panel = panelImage.rectTransform;

            CreateImage("PaperCard", panel, null, Vector2.zero,
                new Vector2(824f, 884f), InkPalette.Paper);
            CreateText("Title", panel, "플레이 결과", 56, new Vector2(0f, 354f),
                new Vector2(620f, 90f), InkPalette.TextDark, FontStyle.Normal);
            CreateImage("TitleDivider", panel, null, new Vector2(0f, 294f),
                new Vector2(610f, 3f), new Color(InkPalette.Red.r, InkPalette.Red.g,
                    InkPalette.Red.b, 0.78f));

            heightText = CreateResultBlock("CurrentResult", panel, "이번 고도",
                new Vector2(0f, 160f), out var currentHighlight);
            currentHighlight.gameObject.SetActive(false);
            bestText = CreateResultBlock("BestResult", panel, "최고 고도",
                new Vector2(0f, -82f), out bestGlow);

            newBestText = CreateText("NewBest", panel, "신기록", 34,
                new Vector2(0f, -230f), new Vector2(280f, 58f),
                InkPalette.Red, FontStyle.Normal);
            CreateText("TouchHint", panel, "화면을 터치해 다시 도전", 30,
                new Vector2(0f, -356f), new Vector2(700f, 70f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.62f),
                FontStyle.Normal);
        }

        IEnumerator ShowRoutine(bool reachedNewBest)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            panel.localScale = Vector3.one * 0.94f;
            panel.localEulerAngles = Vector3.zero;

            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Smooth01(t);
                rootGroup.alpha = appear;
                panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, appear);
                yield return null;
            }

            rootGroup.alpha = 1f;
            panel.localScale = Vector3.one;
            panel.localEulerAngles = Vector3.zero;

            while (reachedNewBest)
            {
                float pulse = 0.84f + 0.16f * Mathf.Sin(Time.unscaledTime * 7f);
                newBestText.color = new Color(InkPalette.Red.r, InkPalette.Red.g,
                    InkPalette.Red.b, pulse);
                bestGlow.color = new Color(InkPalette.Gold.r, InkPalette.Gold.g,
                    InkPalette.Gold.b, 0.14f + pulse * 0.1f);
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

        static Text CreateResultBlock(string objectName, Transform parent, string caption,
            Vector2 position, out Image highlight)
        {
            var border = CreateImage(objectName, parent, null, position,
                new Vector2(700f, 204f), InkPalette.Ink);
            CreateImage("Paper", border.transform, null, Vector2.zero,
                new Vector2(688f, 192f), InkPalette.Paper2);
            highlight = CreateImage("Highlight", border.transform, null, Vector2.zero,
                new Vector2(688f, 192f), new Color(InkPalette.Gold.r, InkPalette.Gold.g,
                    InkPalette.Gold.b, 0.2f));
            CreateText("Caption", border.transform, caption, 28, new Vector2(0f, 48f),
                new Vector2(560f, 48f), InkPalette.TextMuted, FontStyle.Normal);
            return CreateText("Value", border.transform, "0 m", 72, new Vector2(0f, -32f),
                new Vector2(620f, 105f), InkPalette.TextDark, FontStyle.Normal);
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
            text.font = InkPalette.UiFont;
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
