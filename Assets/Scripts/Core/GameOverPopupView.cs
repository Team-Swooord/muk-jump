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
                new Vector2(1400f, 2300f), new Color(0.04f, 0.038f, 0.034f, 0.48f));
            backdrop.raycastTarget = false;

            var shadow = CreateImage("InkShadow", root.transform, InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(22f, -30f), new Vector2(920f, 1010f), new Color(0f, 0f, 0f, 0.3f));
            shadow.raycastTarget = false;

            var panelImage = CreateImage("InkResultPopup", root.transform,
                InkUiTextureFactory.CreateBlobSprite(), Vector2.zero, new Vector2(890f, 980f),
                InkPalette.Ink);
            panel = panelImage.rectTransform;

            // 검은 먹 테두리 안에 따뜻한 한지 카드를 한 겹 넣어 결과 정보의 가독성을 높인다.
            CreateImage("PaperCard", panel, InkUiTextureFactory.CreateBlobSprite(), Vector2.zero,
                new Vector2(805f, 875f), InkPalette.Paper);

            var titleBrush = CreateImage("TitleBrush", panel, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 285f), new Vector2(470f, 78f),
                new Color(InkPalette.Red.r, InkPalette.Red.g, InkPalette.Red.b, 0.92f));
            titleBrush.transform.localEulerAngles = new Vector3(0f, 0f, -2f);
            CreateText("Title", panel, "게임 종료", 54, new Vector2(0f, 286f),
                new Vector2(500f, 90f), InkPalette.Paper, FontStyle.Bold);

            CreateText("HeightCaption", panel, "이번 오름", 30, new Vector2(0f, 142f),
                new Vector2(420f, 55f), new Color(InkPalette.Ink.r, InkPalette.Ink.g,
                    InkPalette.Ink.b, 0.66f), FontStyle.Normal);
            heightText = CreateText("Height", panel, "고도 0m", 88, new Vector2(0f, 45f),
                new Vector2(700f, 130f), InkPalette.Ink, FontStyle.Bold);
            heightRect = heightText.rectTransform;

            bestGlow = CreateImage("BestGlow", panel, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, -88f), new Vector2(570f, 92f),
                new Color(0.86f, 0.65f, 0.18f, 0.7f));
            bestText = CreateText("Best", panel, "최고 0m", 42, new Vector2(0f, -87f),
                new Vector2(560f, 75f), InkPalette.Ink, FontStyle.Bold);
            newBestText = CreateText("NewBest", panel, "새로운 최고 고도", 36,
                new Vector2(0f, -188f), new Vector2(650f, 72f),
                InkPalette.Red, FontStyle.Bold);
            CreateText("TouchHint", panel, "화면을 터치해 메인으로", 30,
                new Vector2(0f, -306f), new Vector2(700f, 70f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.62f),
                FontStyle.Normal);

            // 손으로 찍은 낙관과 바깥으로 튄 작은 먹방울을 더해 정형적인 팝업 느낌을 줄인다.
            CreateImage("Seal", panel, InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(278f, -250f), new Vector2(94f, 94f), InkPalette.Red);
            CreateText("SealText", panel, "먹", 36, new Vector2(278f, -250f),
                new Vector2(75f, 75f), InkPalette.Paper, FontStyle.Bold);
            CreateInkDot(root.transform, new Vector2(-390f, 310f), 34f);
            CreateInkDot(root.transform, new Vector2(-425f, 250f), 18f);
            CreateInkDot(root.transform, new Vector2(405f, -285f), 28f);
            CreateInkDot(root.transform, new Vector2(430f, -335f), 14f);
        }

        IEnumerator ShowRoutine(bool reachedNewBest)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            panel.localScale = Vector3.one * 0.76f;
            panel.localEulerAngles = new Vector3(0f, 0f, -4f);
            heightRect.anchoredPosition = new Vector2(0f, 285f);

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
                panel.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-4f, 0f, appear));

                float fall = 1f - Mathf.Pow(1f - Mathf.Clamp01((t - 0.18f) / 0.72f), 3f);
                float bounce = Mathf.Sin(fall * Mathf.PI * 2f) * (1f - fall) * 24f;
                heightRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(285f, 45f, fall) + bounce);
                yield return null;
            }

            rootGroup.alpha = 1f;
            panel.localScale = Vector3.one;
            panel.localEulerAngles = Vector3.zero;
            heightRect.anchoredPosition = new Vector2(0f, 45f);

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

        static void CreateInkDot(Transform parent, Vector2 position, float size)
        {
            var dot = CreateImage("InkDroplet", parent, InkUiTextureFactory.CreateBlobSprite(),
                position, Vector2.one * size, new Color(InkPalette.Ink.r, InkPalette.Ink.g,
                    InkPalette.Ink.b, 0.86f));
            dot.rectTransform.localEulerAngles = new Vector3(0f, 0f, position.x * 0.07f);
        }
    }
}
