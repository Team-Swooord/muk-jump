using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 게임 종료 기록을 세로로 펼쳐지는 한지 두루마리로 보여준다.
    /// MonoBehaviour 파일명과 클래스명을 일치시켜 씬 직렬화 시 Missing Script를 방지한다.
    public sealed class GameOverPopupView : MonoBehaviour
    {
        const int CanvasSortingOrder = 5000;
        const float RevealDuration = 0.5f;
        const float RollOpenDistance = 600f;
        const float ClosedPaperScale = 0.12f;

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        RectTransform scrollBody;
        RectTransform topRoll;
        RectTransform bottomRoll;
        RectTransform contentRect;
        RectTransform newBestSeal;
        CanvasGroup contentGroup;
        CanvasGroup newBestGroup;
        Text heightText;
        Text bestText;
        Image bestGlow;
        Coroutine showRoutine;

        public void Show(int height, int best, bool reachedNewBest)
        {
            BuildIfNeeded();
            ApplySafeArea();
            BindResults(height, best, reachedNewBest);
            if (showRoutine != null)
                StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowRoutine(reachedNewBest));
        }

        void OnDisable()
        {
            if (showRoutine == null) return;
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var root = new GameObject(
                "GameOverPopupCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkWash",
                root.transform,
                new Color(0.035f, 0.032f, 0.028f, 0.6f));
            backdrop.raycastTarget = false;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            panel = CreateRect(
                "ScrollResultPopup",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(860f, 1390f));

            BuildScrollPaper();
            BuildContent();
            ApplySafeArea();
            BindResults(0, 0, false);
            ApplyRevealPose(0f, false);
        }

        void BuildScrollPaper()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            scrollBody = CreateRect(
                "ScrollBody",
                panel,
                Vector2.zero,
                new Vector2(760f, 1194f));

            var shadow = CreateImage(
                "InkBleedShadow",
                scrollBody,
                brush,
                new Vector2(14f, -18f),
                new Vector2(1208f, 786f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollBodyOutline",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(1198f, 778f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "ScrollPaper",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(1170f, 748f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            CreateDeckledEdge(scrollBody, "LeftDeckle", -362f, brush);
            CreateDeckledEdge(scrollBody, "RightDeckle", 362f, brush);
            CreatePaperFibers(scrollBody, brush);

            topRoll = CreateScrollRoll(panel, RollOpenDistance, true);
            bottomRoll = CreateScrollRoll(panel, -RollOpenDistance, false);
        }

        void BuildContent()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();

            contentRect = CreateRect(
                "ResultContent",
                panel,
                Vector2.zero,
                new Vector2(740f, 1170f));
            contentGroup = contentRect.gameObject.AddComponent<CanvasGroup>();

            var headerSeal = CreateImage(
                "ResultSeal",
                contentRect,
                blob,
                new Vector2(-288f, 466f),
                new Vector2(72f, 72f),
                InkPalette.Red);
            CreateText(
                "SealText",
                headerSeal.transform,
                "결",
                28,
                Vector2.zero,
                new Vector2(54f, 52f),
                InkPalette.Paper,
                FontStyle.Normal);

            var title = CreateText(
                "Title",
                contentRect,
                "도전 기록",
                66,
                new Vector2(0f, 466f),
                new Vector2(550f, 92f),
                InkPalette.TextDark,
                FontStyle.Normal);
            AddSoftWeight(title, InkPalette.Ink, 0.2f);

            CreateText(
                "Subtitle",
                contentRect,
                "먹길에 남긴 오늘의 높이",
                30,
                new Vector2(0f, 388f),
                new Vector2(620f, 58f),
                ReadableMutedColor(),
                FontStyle.Normal);

            CreateImage(
                "TitleDivider",
                contentRect,
                brush,
                new Vector2(0f, 334f),
                new Vector2(520f, 16f),
                new Color(InkPalette.Red.r, InkPalette.Red.g, InkPalette.Red.b, 0.68f));

            heightText = CreateResultBlock(
                "CurrentResult",
                contentRect,
                "이번 고도",
                new Vector2(0f, 165f),
                116,
                false,
                out _);

            CreateImage(
                "RecordDivider",
                contentRect,
                brush,
                new Vector2(0f, 34f),
                new Vector2(500f, 10f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.2f));

            bestText = CreateResultBlock(
                "BestResult",
                contentRect,
                "최고 고도",
                new Vector2(0f, -112f),
                70,
                true,
                out bestGlow);

            BuildNewBestSeal(contentRect, blob);

            var retryBrush = CreateImage(
                "RetryBrush",
                contentRect,
                brush,
                new Vector2(0f, -430f),
                new Vector2(640f, 106f),
                InkPalette.Ink);
            var touchHint = CreateText(
                "TouchHint",
                retryBrush.transform,
                "화면을 터치해 다시 도전",
                36,
                Vector2.zero,
                new Vector2(550f, 76f),
                InkPalette.Paper,
                FontStyle.Normal);
            AddSoftWeight(touchHint, Color.black, 0.2f);

            CreateText(
                "Footer",
                contentRect,
                "먹길은 다시 이어집니다",
                27,
                new Vector2(0f, -508f),
                new Vector2(580f, 48f),
                ReadableMutedColor(),
                FontStyle.Normal);
        }

        void BuildNewBestSeal(Transform parent, Sprite blob)
        {
            newBestSeal = CreateRect(
                "NewBestSeal",
                parent,
                new Vector2(246f, -136f),
                new Vector2(104f, 104f));
            newBestSeal.localEulerAngles = new Vector3(0f, 0f, -7f);
            newBestGroup = newBestSeal.gameObject.AddComponent<CanvasGroup>();

            CreateImage(
                "Shadow",
                newBestSeal,
                blob,
                new Vector2(5f, -6f),
                new Vector2(96f, 96f),
                new Color(0f, 0f, 0f, 0.18f));
            CreateImage(
                "Seal",
                newBestSeal,
                blob,
                Vector2.zero,
                new Vector2(92f, 92f),
                InkPalette.Red);
            CreateText(
                "NewBest",
                newBestSeal,
                "신",
                34,
                new Vector2(0f, 10f),
                new Vector2(62f, 46f),
                InkPalette.Paper,
                FontStyle.Normal);
            CreateText(
                "Label",
                newBestSeal,
                "기록",
                18,
                new Vector2(0f, -23f),
                new Vector2(64f, 28f),
                InkPalette.Paper,
                FontStyle.Normal);
        }

        void BindResults(int height, int best, bool reachedNewBest)
        {
            heightText.text = FormatHeight(height);
            bestText.text = FormatHeight(best);
            newBestSeal.gameObject.SetActive(reachedNewBest);
            bestGlow.gameObject.SetActive(reachedNewBest);
        }

        IEnumerator ShowRoutine(bool reachedNewBest)
        {
            rootGroup.blocksRaycasts = true;
            ApplyRevealPose(0f, reachedNewBest);

            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyRevealPose(elapsed / RevealDuration, reachedNewBest);
                yield return null;
            }

            ApplyRevealPose(1f, reachedNewBest);
            showRoutine = null;
        }

        /// 시간 대기 없이 펼침 자세를 검증할 수 있도록 정규화된 진행률만 적용한다.
        void ApplyRevealPose(float progress, bool reachedNewBest)
        {
            float t = Mathf.Clamp01(progress);
            float appear = EaseOutCubic(Mathf.InverseLerp(0f, 0.24f, t));
            float unroll = EaseOutCubic(Mathf.InverseLerp(0.03f, 0.82f, t));
            float content = EaseOutCubic(Mathf.InverseLerp(0.34f, 0.9f, t));

            rootGroup.alpha = appear;
            panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, unroll);
            panel.localEulerAngles = Vector3.zero;
            scrollBody.localScale = new Vector3(
                1f,
                Mathf.Lerp(ClosedPaperScale, 1f, unroll),
                1f);
            topRoll.anchoredPosition = Vector2.up * (RollOpenDistance * unroll);
            bottomRoll.anchoredPosition = Vector2.down * (RollOpenDistance * unroll);
            contentGroup.alpha = content;
            contentRect.anchoredPosition = Vector2.down * (18f * (1f - content));

            if (!reachedNewBest)
            {
                newBestGroup.alpha = 0f;
                newBestSeal.localScale = Vector3.one * 0.9f;
                newBestSeal.localEulerAngles = new Vector3(0f, 0f, -13f);
                return;
            }

            float stamp = Mathf.Clamp01(Mathf.InverseLerp(0.68f, 1f, t));
            float stampScale;
            if (stamp < 0.58f)
            {
                float press = EaseOutCubic(stamp / 0.58f);
                stampScale = Mathf.Lerp(0.86f, 1.04f, press);
            }
            else
            {
                float settle = Smooth01(Mathf.InverseLerp(0.58f, 1f, stamp));
                stampScale = Mathf.Lerp(1.04f, 1f, settle);
            }

            newBestGroup.alpha = EaseOutCubic(stamp);
            newBestSeal.localScale = Vector3.one * stampScale;
            newBestSeal.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Lerp(-13f, -7f, EaseOutCubic(stamp)));
            bestGlow.color = new Color(
                InkPalette.Gold.r,
                InkPalette.Gold.g,
                InkPalette.Gold.b,
                0.14f * newBestGroup.alpha);
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                Mathf.Clamp01(safe.xMin / Screen.width),
                Mathf.Clamp01(safe.yMin / Screen.height));
            safeAreaRoot.anchorMax = new Vector2(
                Mathf.Clamp01(safe.xMax / Screen.width),
                Mathf.Clamp01(safe.yMax / Screen.height));
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
        }

        static void CreateDeckledEdge(
            Transform parent, string objectName, float x, Sprite brush)
        {
            var edge = CreateImage(
                objectName,
                parent,
                brush,
                new Vector2(x, 0f),
                new Vector2(1100f, 22f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.22f));
            edge.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        static void CreatePaperFibers(Transform parent, Sprite brush)
        {
            float[] yPositions = { 392f, 116f, -184f, -398f };
            float[] widths = { 530f, 610f, 470f, 575f };
            float[] rotations = { -1.2f, 0.7f, -0.5f, 1.1f };
            for (int i = 0; i < yPositions.Length; i++)
            {
                var fiber = CreateImage(
                    $"PaperFiber{i + 1}",
                    parent,
                    brush,
                    new Vector2((i % 2 == 0 ? -1f : 1f) * 18f, yPositions[i]),
                    new Vector2(widths[i], 13f),
                    new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.035f));
                fiber.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, rotations[i]);
            }
        }

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(850f, 122f));

            CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(10f, -9f),
                new Vector2(824f, 96f),
                new Color(0f, 0f, 0f, 0.18f));
            var roll = CreateImage(
                "PaperRoll",
                root,
                brush,
                Vector2.zero,
                new Vector2(834f, 94f),
                InkPalette.Ink);
            CreateImage(
                "Paper",
                roll.transform,
                brush,
                Vector2.zero,
                new Vector2(804f, 70f),
                InkPalette.Paper2);
            CreateImage(
                "FoldShade",
                roll.transform,
                brush,
                new Vector2(0f, top ? -17f : 17f),
                new Vector2(750f, 10f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.14f));
            CreateImage(
                "FoldHighlight",
                roll.transform,
                brush,
                new Vector2(0f, top ? 14f : -14f),
                new Vector2(760f, 8f),
                new Color(InkPalette.Paper.r, InkPalette.Paper.g, InkPalette.Paper.b, 0.88f));

            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(
                    side < 0 ? "LeftCap" : "RightCap",
                    root,
                    blob,
                    new Vector2(side * 402f, 0f),
                    new Vector2(98f, 98f),
                    InkPalette.Ink);
                CreateImage(
                    "Paper",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(74f, 74f),
                    InkPalette.Paper2);
                CreateImage(
                    "Axis",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(22f, 22f),
                    InkPalette.Ink);
            }
            return root;
        }

        static Text CreateResultBlock(
            string objectName,
            Transform parent,
            string caption,
            Vector2 position,
            int valueFontSize,
            bool createHighlight,
            out Image highlight)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var root = CreateRect(
                objectName,
                parent,
                position,
                new Vector2(700f, 220f));
            highlight = createHighlight
                ? CreateImage(
                    "Highlight",
                    root,
                    brush,
                    new Vector2(0f, -20f),
                    new Vector2(510f, 104f),
                    new Color(InkPalette.Gold.r, InkPalette.Gold.g, InkPalette.Gold.b, 0.14f))
                : null;

            CreateText(
                "Caption",
                root,
                caption,
                34,
                new Vector2(0f, 65f),
                new Vector2(560f, 56f),
                ReadableMutedColor(),
                FontStyle.Normal);
            var value = CreateText(
                "Value",
                root,
                "0 m",
                valueFontSize,
                new Vector2(0f, -28f),
                new Vector2(650f, 142f),
                InkPalette.TextDark,
                FontStyle.Normal);
            AddSoftWeight(value, InkPalette.Ink, createHighlight ? 0.18f : 0.22f);
            return value;
        }

        static string FormatHeight(int meters)
        {
            int nonNegative = Mathf.Max(0, meters);
            return nonNegative >= 10000
                ? $"{nonNegative / 1000f:0.#} km"
                : $"{nonNegative} m";
        }

        static RectTransform CreateRect(
            string objectName, Transform parent, Vector2 position, Vector2 size)
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

        static RectTransform CreateStretchRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(string objectName, Transform parent, Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            FontStyle style)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            return text;
        }

        static void AddSoftWeight(Text text, Color color, float alpha)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(color.r, color.g, color.b, alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        static Color ReadableMutedColor()
        {
            Color color = InkPalette.TextDark;
            color.a = 0.84f;
            return color;
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
