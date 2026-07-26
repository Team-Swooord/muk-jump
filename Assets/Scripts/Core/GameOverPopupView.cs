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
        RectTransform scrollBody;
        RectTransform topRoll;
        RectTransform bottomRoll;
        CanvasGroup contentGroup;
        Text heightText;
        Text bestText;
        Text newBestText;
        Image bestGlow;
        Coroutine showRoutine;

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

            var panelImage = CreateImage("ScrollResultPopup", root.transform, null,
                Vector2.zero, new Vector2(900f, 980f), Color.clear);
            panel = panelImage.rectTransform;

            scrollBody = CreateImage("ScrollBody", panel, null, Vector2.zero,
                new Vector2(810f, 880f), Color.clear).rectTransform;
            CreateImage("ScrollShadow", scrollBody, null, new Vector2(18f, -20f),
                new Vector2(798f, 870f), new Color(0f, 0f, 0f, 0.24f));
            CreateImage("ScrollBodyOutline", scrollBody, null, Vector2.zero,
                new Vector2(792f, 860f), InkPalette.Ink);
            CreateImage("ScrollPaper", scrollBody, null, Vector2.zero,
                new Vector2(770f, 840f), InkPalette.Paper);
            CreateImage("LeftPaperShade", scrollBody, null, new Vector2(-374f, 0f),
                new Vector2(18f, 824f), new Color(InkPalette.Paper2.r, InkPalette.Paper2.g,
                    InkPalette.Paper2.b, 0.72f));
            CreateImage("RightPaperShade", scrollBody, null, new Vector2(374f, 0f),
                new Vector2(18f, 824f), new Color(InkPalette.Paper2.r, InkPalette.Paper2.g,
                    InkPalette.Paper2.b, 0.72f));
            topRoll = CreateScrollRoll(panel, 430f, true);
            bottomRoll = CreateScrollRoll(panel, -430f, false);

            var content = CreateImage("ResultContent", panel, null, Vector2.zero,
                new Vector2(800f, 860f), Color.clear);
            contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            CreateText("Title", content.transform, "플레이 결과", 62, new Vector2(0f, 354f),
                new Vector2(620f, 90f), InkPalette.TextDark, FontStyle.Normal);
            CreateImage("TitleDivider", content.transform, null, new Vector2(0f, 294f),
                new Vector2(610f, 3f), new Color(InkPalette.Red.r, InkPalette.Red.g,
                    InkPalette.Red.b, 0.78f));

            heightText = CreateResultBlock("CurrentResult", content.transform, "이번 고도",
                new Vector2(0f, 160f), out var currentHighlight);
            currentHighlight.gameObject.SetActive(false);
            bestText = CreateResultBlock("BestResult", content.transform, "최고 고도",
                new Vector2(0f, -82f), out bestGlow);

            newBestText = CreateText("NewBest", content.transform, "신기록", 40,
                new Vector2(0f, -230f), new Vector2(280f, 58f),
                InkPalette.Red, FontStyle.Normal);
            CreateText("TouchHint", content.transform, "화면을 터치해 다시 도전", 34,
                new Vector2(0f, -356f), new Vector2(700f, 70f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.86f),
                FontStyle.Normal);
        }

        IEnumerator ShowRoutine(bool reachedNewBest)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            panel.localScale = Vector3.one * 0.98f;
            panel.localEulerAngles = Vector3.zero;
            scrollBody.localScale = new Vector3(1f, 0.02f, 1f);
            topRoll.anchoredPosition = Vector2.zero;
            bottomRoll.anchoredPosition = Vector2.zero;
            contentGroup.alpha = 0f;

            float elapsed = 0f;
            const float duration = 0.78f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Smooth01(Mathf.InverseLerp(0f, 0.22f, t));
                float unroll = Smooth01(Mathf.InverseLerp(0.08f, 0.78f, t));
                float content = Smooth01(Mathf.InverseLerp(0.58f, 0.96f, t));
                rootGroup.alpha = appear;
                panel.localScale = Vector3.one * Mathf.Lerp(0.98f, 1f, unroll);
                scrollBody.localScale = new Vector3(1f, Mathf.Max(0.02f, unroll), 1f);
                topRoll.anchoredPosition = Vector2.up * (430f * unroll);
                bottomRoll.anchoredPosition = Vector2.down * (430f * unroll);
                contentGroup.alpha = content;
                yield return null;
            }

            rootGroup.alpha = 1f;
            panel.localScale = Vector3.one;
            panel.localEulerAngles = Vector3.zero;
            scrollBody.localScale = Vector3.one;
            topRoll.anchoredPosition = Vector2.up * 430f;
            bottomRoll.anchoredPosition = Vector2.down * 430f;
            contentGroup.alpha = 1f;

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

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            var root = CreateImage(top ? "TopRoll" : "BottomRoll", parent, null,
                new Vector2(0f, y), new Vector2(900f, 120f), Color.clear).rectTransform;
            CreateImage("Shadow", root, null, new Vector2(12f, -10f), new Vector2(866f, 92f),
                new Color(0f, 0f, 0f, 0.22f));
            var roll = CreateImage("PaperRoll", root, null,
                Vector2.zero, new Vector2(852f, 86f), InkPalette.Ink);
            CreateImage("Paper", roll.transform, null, Vector2.zero,
                new Vector2(828f, 68f), InkPalette.Paper2);
            CreateImage("FoldHighlight", roll.transform, null,
                new Vector2(0f, top ? 15f : -15f), new Vector2(800f, 5f),
                new Color(InkPalette.Paper.r, InkPalette.Paper.g, InkPalette.Paper.b, 0.9f));

            Sprite capSprite = InkUiTextureFactory.CreateBlobSprite();
            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(side < 0 ? "LeftCap" : "RightCap", root, capSprite,
                    new Vector2(side * 426f, 0f), new Vector2(104f, 104f), InkPalette.Ink);
                CreateImage("Paper", cap.transform, capSprite, Vector2.zero,
                    new Vector2(78f, 78f), InkPalette.Paper2);
                CreateImage("Axis", cap.transform, capSprite, Vector2.zero,
                    new Vector2(24f, 24f), InkPalette.Ink);
            }
            return root;
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
            CreateText("Caption", border.transform, caption, 32, new Vector2(0f, 48f),
                new Vector2(560f, 52f), InkPalette.TextMuted, FontStyle.Normal);
            return CreateText("Value", border.transform, "0 m", 78, new Vector2(0f, -32f),
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
