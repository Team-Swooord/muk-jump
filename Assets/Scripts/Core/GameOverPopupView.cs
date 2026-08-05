using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 짧은 반복 플레이 흐름을 방해하지 않는 간결한 게임 종료 두루마리.
    /// MonoBehaviour 파일명과 클래스명을 일치시켜 씬 직렬화 시 Missing Script를 방지한다.
    public sealed class GameOverPopupView : MonoBehaviour
    {
        const int CanvasSortingOrder = 5000;
        const float RevealDuration = 0.3f;
        const float RollOpenDistance = 360f;
        const float ClosedPaperScale = 0.18f;

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
        Text growthRewardText;
        Text growthJourneyText;
        Image growthJourneyFill;
        Text touchHint;
        Coroutine showRoutine;
        GameOverResult boundResult;

        public void Show(int height, int best, bool reachedNewBest)
        {
            Show(new GameOverResult(
                height,
                best,
                reachedNewBest,
                0,
                0,
                true));
        }

        public void Show(GameOverResult result)
        {
            BuildIfNeeded();
            ApplySafeArea();
            boundResult = result;
            BindResult(result);
            if (showRoutine != null)
                StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowRoutine());
        }

        public void RefreshResult(GameOverResult result)
        {
            BuildIfNeeded();
            boundResult = result;
            BindResult(result);
            if (showRoutine == null && rootGroup.blocksRaycasts)
            {
                ApplyRevealPose(1f, result.ReachedNewBest);
            }
        }

        public string GrowthRewardLabel =>
            growthRewardText != null ? growthRewardText.text : string.Empty;
        public string TouchHintLabel =>
            touchHint != null ? touchHint.text : string.Empty;
        public string GrowthJourneyLabel =>
            growthJourneyText != null ? growthJourneyText.text : string.Empty;

        public void ShowPendingAbandonConfirmation()
        {
            BuildIfNeeded();
            touchHint.text = "한 번 더 터치해 기록·먹빛 포기";
        }

        void OnDisable()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var existing = transform.Find("GameOverPopupCanvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

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
                new Color(0.035f, 0.032f, 0.028f, 0.56f));
            backdrop.raycastTarget = false;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            panel = CreateRect(
                "ScrollResultPopup",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(800f, 900f));

            BuildScrollPaper();
            BuildContent();
            ApplySafeArea();
            BindResult(new GameOverResult(0, 0, false, 0, 0, true));
            ApplyRevealPose(0f, false);
        }

        void BuildScrollPaper()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            scrollBody = CreateRect(
                "ScrollBody",
                panel,
                Vector2.zero,
                new Vector2(700f, 720f));

            var shadow = CreateImage(
                "InkBleedShadow",
                scrollBody,
                brush,
                new Vector2(12f, -14f),
                new Vector2(742f, 712f),
                new Color(0f, 0f, 0f, 0.15f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollBodyOutline",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(728f, 704f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "ScrollPaper",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(708f, 680f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            // 회전해 늘린 붓 마스크의 섬유 틈이 어두운 배경을 비처럼 비추지 않도록
            // 본문 안쪽만 불투명 한지로 채운다. 가장자리의 불규칙한 붓결은 그대로 남긴다.
            CreateImage(
                "PaperCore",
                scrollBody,
                null,
                Vector2.zero,
                new Vector2(620f, 680f),
                InkPalette.Paper);

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
                new Vector2(650f, 650f));
            contentGroup = contentRect.gameObject.AddComponent<CanvasGroup>();

            var title = CreateText(
                "Title",
                contentRect,
                "도전 끝",
                58,
                new Vector2(-30f, 270f),
                new Vector2(520f, 78f),
                InkPalette.TextDark,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            AddSoftWeight(title, InkPalette.Ink, 0.2f);

            var currentResult = CreateRect(
                "CurrentResult",
                contentRect,
                new Vector2(0f, 100f),
                new Vector2(650f, 230f));
            CreateText(
                "Caption",
                currentResult,
                "이번 고도",
                32,
                new Vector2(-95f, 80f),
                new Vector2(420f, 52f),
                ReadableMutedColor(),
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            heightText = CreateText(
                "Value",
                currentResult,
                "0 m",
                112,
                new Vector2(20f, -15f),
                new Vector2(600f, 150f),
                InkPalette.TextDark,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            AddSoftWeight(heightText, InkPalette.Ink, 0.22f);

            CreateImage(
                "RecordDivider",
                contentRect,
                brush,
                new Vector2(0f, -25f),
                new Vector2(360f, 8f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.16f));

            var bestResult = CreateRect(
                "BestResult",
                contentRect,
                new Vector2(0f, -90f),
                new Vector2(610f, 82f));
            CreateText(
                "Caption",
                bestResult,
                "최고 고도",
                30,
                new Vector2(-150f, 0f),
                new Vector2(250f, 58f),
                ReadableMutedColor(),
                FontStyle.Normal);
            bestText = CreateText(
                "Value",
                bestResult,
                "0 m",
                48,
                new Vector2(125f, 0f),
                new Vector2(330f, 72f),
                InkPalette.TextDark,
                FontStyle.Normal);
            AddSoftWeight(bestText, InkPalette.Ink, 0.16f);

            BuildNewBestSeal(contentRect, blob);

            var growthResult = CreateRect(
                "PermanentGrowthReward",
                contentRect,
                new Vector2(0f, -174f),
                new Vector2(610f, 92f));
            CreateText(
                "Caption",
                growthResult,
                "영구 성장 · 먹빛",
                27,
                new Vector2(-182f, 22f),
                new Vector2(230f, 42f),
                ReadableMutedColor(),
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            growthRewardText = CreateText(
                "Value",
                growthResult,
                "+0 · 보유 0",
                26,
                new Vector2(112f, 22f),
                new Vector2(370f, 44f),
                InkPalette.TextDark,
                FontStyle.Normal,
                TextAnchor.MiddleRight);
            AddSoftWeight(growthRewardText, InkPalette.Ink, 0.14f);

            growthJourneyText = CreateText(
                "JourneyProgress",
                growthResult,
                "누적 0 / 20 m",
                26,
                new Vector2(0f, -16f),
                new Vector2(540f, 30f),
                ReadableMutedColor(),
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            Image journeyTrack = CreateImage(
                "JourneyTrack",
                growthResult,
                null,
                new Vector2(0f, -39f),
                new Vector2(520f, 9f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.13f));
            growthJourneyFill = CreateImage(
                "Fill",
                journeyTrack.transform,
                null,
                Vector2.zero,
                new Vector2(0f, 9f),
                InkPalette.Red);
            RectTransform journeyFillRect = growthJourneyFill.rectTransform;
            journeyFillRect.anchorMin = journeyFillRect.anchorMax =
                new Vector2(0f, 0.5f);
            journeyFillRect.pivot = new Vector2(0f, 0.5f);
            journeyFillRect.anchoredPosition = Vector2.zero;

            var retryBrush = CreateImage(
                "RetryBrush",
                contentRect,
                null,
                new Vector2(0f, -280f),
                new Vector2(580f, 104f),
                InkPalette.Ink);
            touchHint = CreateText(
                "TouchHint",
                retryBrush.transform,
                "터치하여 로비로",
                36,
                Vector2.zero,
                new Vector2(500f, 74f),
                InkPalette.Paper,
                FontStyle.Normal);
            AddSoftWeight(touchHint, Color.black, 0.2f);
            InkUiStyle.ConfigureActionSurface(retryBrush, touchHint);
        }

        void BuildNewBestSeal(Transform parent, Sprite blob)
        {
            newBestSeal = CreateRect(
                "NewBestSeal",
                parent,
                new Vector2(236f, 75f),
                new Vector2(100f, 100f));
            newBestSeal.localEulerAngles = new Vector3(0f, 0f, -7f);
            newBestGroup = newBestSeal.gameObject.AddComponent<CanvasGroup>();

            CreateImage(
                "Shadow",
                newBestSeal,
                blob,
                new Vector2(4f, -5f),
                new Vector2(92f, 92f),
                new Color(0f, 0f, 0f, 0.17f));
            CreateImage(
                "Seal",
                newBestSeal,
                blob,
                Vector2.zero,
                new Vector2(88f, 88f),
                InkPalette.Red);
            CreateText(
                "NewBest",
                newBestSeal,
                "신",
                32,
                new Vector2(0f, 9f),
                new Vector2(58f, 44f),
                InkPalette.Paper,
                FontStyle.Normal);
            CreateText(
                "Label",
                newBestSeal,
                "기록",
                17,
                new Vector2(0f, -22f),
                new Vector2(60f, 26f),
                InkPalette.Paper,
                FontStyle.Normal);
        }

        void BindResult(GameOverResult result)
        {
            heightText.text = FormatHeight(result.Height);
            bestText.text = FormatHeight(result.Best);
            SetGrowthJourneyVisible(true);
            switch (result.PersistenceState)
            {
                case GameOverPersistenceState.ScoreBaselinePending:
                    growthRewardText.text = "기록 기준 확인 중 · 자동 재시도";
                    growthJourneyText.text = "거리 정산 대기";
                    SetGrowthJourneyProgress(0f);
                    touchHint.text = "이번 판 기록·먹빛 포기";
                    break;
                case GameOverPersistenceState.GrowthRecoveryRequired:
                    growthRewardText.text = "저장 실패 · 성장 복구 필요";
                    growthJourneyText.text = "이번 판 누적 거리 미반영";
                    SetGrowthJourneyProgress(0f);
                    touchHint.text = "로비에서 성장 복구";
                    break;
                case GameOverPersistenceState.RecordWritePending:
                    growthRewardText.text = "기록 저장 중 · 자동 재시도";
                    BindGrowthJourney(result);
                    touchHint.text = "재시도 중단하고 로비로";
                    break;
                default:
                    growthRewardText.text = result.RewardsAllowed
                        ? $"+{Mathf.Max(0, result.EarnedGrowthCurrency)} · " +
                          $"보유 {Mathf.Max(0, result.GrowthCurrencyBalance)}"
                        : "디버그 판 · 보상 없음";
                    if (result.RewardsAllowed)
                        BindGrowthJourney(result);
                    else
                    {
                        growthJourneyText.text = "누적 거리 미반영";
                        SetGrowthJourneyProgress(0f);
                    }
                    touchHint.text = "터치하여 로비로";
                    break;
            }
            newBestSeal.gameObject.SetActive(result.ReachedNewBest);
        }

        void BindGrowthJourney(GameOverResult result)
        {
            long cumulative = System.Math.Max(
                0L,
                result.CumulativeGrowthDistanceMeters);
            if (result.GrowthDistanceJourneyComplete)
            {
                growthJourneyText.text = $"완성 · {FormatDistance(cumulative)}";
                SetGrowthJourneyProgress(1f);
                return;
            }

            long previous = System.Math.Max(
                0L,
                result.PreviousGrowthRewardDistanceMeters);
            long configuredNext = result.NextGrowthRewardDistanceMeters;
            long next = configuredNext > previous
                ? configuredNext
                : RunRewardCalculator.GetNextRewardDistance(0);
            growthJourneyText.text =
                $"누적 {cumulative:N0} / {next:N0} m";
            float progress = (float)System.Math.Clamp(
                (cumulative - previous) / (double)(next - previous),
                0d,
                1d);
            SetGrowthJourneyProgress(progress);
        }

        void SetGrowthJourneyVisible(bool visible)
        {
            if (growthJourneyText != null)
                growthJourneyText.gameObject.SetActive(visible);
            if (growthJourneyFill != null && growthJourneyFill.transform.parent != null)
                growthJourneyFill.transform.parent.gameObject.SetActive(visible);
        }

        void SetGrowthJourneyProgress(float progress)
        {
            if (growthJourneyFill == null) return;
            growthJourneyFill.rectTransform.sizeDelta = new Vector2(
                520f * Mathf.Clamp01(progress),
                9f);
        }

        static string FormatDistance(long meters)
        {
            long safeMeters = System.Math.Max(0L, meters);
            return safeMeters >= 10000L
                ? $"{safeMeters / 1000d:0.#} km"
                : $"{safeMeters:N0} m";
        }

        // 기존 EditMode 레이아웃 테스트와 구형 호출 경로를 위한 단순 결과 바인딩.
        void BindResults(int height, int best, bool reachedNewBest)
        {
            BindResult(new GameOverResult(
                height,
                best,
                reachedNewBest,
                0,
                0,
                true));
        }

        IEnumerator ShowRoutine()
        {
            rootGroup.blocksRaycasts = true;
            ApplyRevealPose(0f, boundResult.ReachedNewBest);

            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyRevealPose(
                    elapsed / RevealDuration,
                    boundResult.ReachedNewBest);
                yield return null;
            }

            ApplyRevealPose(1f, boundResult.ReachedNewBest);
            showRoutine = null;
        }

        /// 시간 대기 없이 팝업 진입 자세를 검증할 수 있도록 정규화된 진행률만 적용한다.
        void ApplyRevealPose(float progress, bool reachedNewBest)
        {
            float t = Mathf.Clamp01(progress);
            float appear = EaseOutCubic(Mathf.InverseLerp(0f, 0.2f, t));
            float unroll = EaseOutCubic(Mathf.InverseLerp(0.02f, 0.82f, t));
            float content = EaseOutCubic(Mathf.InverseLerp(0.22f, 0.78f, t));

            rootGroup.alpha = appear;
            panel.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, unroll);
            panel.localEulerAngles = Vector3.zero;
            scrollBody.localScale = new Vector3(
                1f,
                Mathf.Lerp(ClosedPaperScale, 1f, unroll),
                1f);
            topRoll.anchoredPosition = Vector2.up * (RollOpenDistance * unroll);
            bottomRoll.anchoredPosition = Vector2.down * (RollOpenDistance * unroll);
            contentGroup.alpha = content;
            contentRect.anchoredPosition = Vector2.down * (12f * (1f - content));

            if (!reachedNewBest)
            {
                newBestGroup.alpha = 0f;
                newBestSeal.localScale = Vector3.one * 0.9f;
                newBestSeal.localEulerAngles = new Vector3(0f, 0f, -12f);
                return;
            }

            float stamp = Mathf.Clamp01(Mathf.InverseLerp(0.62f, 1f, t));
            float stampScale;
            if (stamp < 0.58f)
            {
                float press = EaseOutCubic(stamp / 0.58f);
                stampScale = Mathf.Lerp(0.88f, 1.035f, press);
            }
            else
            {
                float settle = Smooth01(Mathf.InverseLerp(0.58f, 1f, stamp));
                stampScale = Mathf.Lerp(1.035f, 1f, settle);
            }

            newBestGroup.alpha = EaseOutCubic(stamp);
            newBestSeal.localScale = Vector3.one * stampScale;
            newBestSeal.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Lerp(-12f, -7f, EaseOutCubic(stamp)));
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

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(760f, 96f));

            CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(8f, -7f),
                new Vector2(734f, 78f),
                new Color(0f, 0f, 0f, 0.17f));
            var roll = CreateImage(
                "PaperRoll",
                root,
                brush,
                Vector2.zero,
                new Vector2(744f, 78f),
                InkPalette.Ink);
            CreateImage(
                "Paper",
                roll.transform,
                brush,
                Vector2.zero,
                new Vector2(718f, 58f),
                InkPalette.Paper2);
            CreateImage(
                "FoldShade",
                roll.transform,
                brush,
                new Vector2(0f, top ? -13f : 13f),
                new Vector2(680f, 8f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(
                    side < 0 ? "LeftCap" : "RightCap",
                    root,
                    blob,
                    new Vector2(side * 358f, 0f),
                    new Vector2(82f, 82f),
                    InkPalette.Ink);
                CreateImage(
                    "Paper",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(62f, 62f),
                    InkPalette.Paper2);
                CreateImage(
                    "Axis",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(19f, 19f),
                    InkPalette.Ink);
            }
            return root;
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
            FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
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
