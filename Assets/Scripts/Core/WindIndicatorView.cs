using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 화면 상단에서 현재 풍향과 상승기류 상태를 간결하게 알려 주는 HUD 뷰.
    public sealed class WindIndicatorView : MonoBehaviour
    {
        public const string WindIconResourcePath = "MukJump/UI/Common/wind_icon_round_v1";

        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform directionArrow;
        [SerializeField] Text stateText;
        [FormerlySerializedAs("tintGraphics")]
        [SerializeField] Graphic[] arrowGraphics;
        [SerializeField] Image alertSeal;
        [SerializeField] Text sealText;

        Vector3 arrowBaseScale = Vector3.one;
        WindWeatherPhase lastPhase;
        bool hasLastPhase;
        bool isVisible = true;
        int displayedDirection = 1;
        float stateFade = 1f;
        InkBrushIcon brushArrow;

        void Awake()
        {
            InkLocalizedText.BindTree(transform);
            ConfigureStaticVisuals();
            ApplyPolishedLayout();
            CacheArrowBaseScale();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnEnable()
        {
            InkLocalizedText.BindTree(transform);
            ConfigureStaticVisuals();
            ApplyPolishedLayout();
            CacheArrowBaseScale();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnValidate()
        {
            CacheArrowBaseScale();
            ConfigureStaticVisuals();
            DisableRaycasts();
            ApplyVisibility();
        }

        void Update()
        {
            var weather = WindWeatherController.Instance;
            if (weather == null) return;

            ApplyState(weather);
        }

        /// 외부 HUD가 로비·플레이 상태에 맞춰 풍향 표시를 켜고 끌 때 사용한다.
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            ApplyVisibility();
        }

        public void ApplyPolishedLayout()
        {
            if (transform is RectTransform root)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.17f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(220f, 82f);
            }

            var oldCard = GetComponent<Graphic>();
            if (oldCard != null) oldCard.enabled = false;

            if (directionArrow != null)
            {
                directionArrow.anchorMin = directionArrow.anchorMax =
                    new Vector2(0.5f, 0.5f);
                directionArrow.pivot = new Vector2(0.5f, 0.5f);
                directionArrow.anchoredPosition = new Vector2(-80f, 0f);
                directionArrow.sizeDelta = new Vector2(44f, 44f);
                EnsureBrushArrow();
            }

            if (stateText != null)
            {
                var rect = stateText.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -26f);
                rect.sizeDelta = new Vector2(210f, 30f);
                stateText.fontSize = 28;
                stateText.fontStyle = FontStyle.Bold;
                stateText.alignment = TextAnchor.MiddleCenter;
                stateText.resizeTextForBestFit = false;
                stateText.alignByGeometry = true;
                stateText.verticalOverflow = VerticalWrapMode.Overflow;
                stateText.horizontalOverflow = HorizontalWrapMode.Overflow;
                stateText.enabled = true;
                EnsureTextWeight(stateText);
            }

            if (alertSeal != null)
            {
                var rect = alertSeal.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.24f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(66f, 66f);
                rect.localRotation = Quaternion.identity;
                ConfigureWindIcon();
            }
        }

        void ApplyState(WindWeatherController weather)
        {
            WindWeatherPhase phase = weather.Phase;
            if (!hasLastPhase || lastPhase != phase)
            {
                hasLastPhase = true;
                lastPhase = phase;
                stateFade = 0f;
                if (stateText != null)
                    InkLocalizedText.SetSource(stateText, GetStateLabel(phase));
            }
            stateFade = Mathf.MoveTowards(
                stateFade, 1f, Time.unscaledDeltaTime / 0.12f);

            bool updraft = weather.IsUpdraftActive || phase == WindWeatherPhase.Updraft;
            bool downward = weather.IsDowndraftActive || phase == WindWeatherPhase.DowndraftWarning;
            bool alert = phase == WindWeatherPhase.Warning || updraft || downward ||
                phase == WindWeatherPhase.GaleWarning || phase == WindWeatherPhase.Gale;
            if (stateText != null) stateText.enabled = true;
            // 모든 날씨에 같은 화살표 + 문구를 사용한다. 별도 소용돌이 형태는 표시하지 않는다.
            if (alertSeal != null)
            {
                alertSeal.enabled = false;
                alertSeal.rectTransform.anchoredPosition = Vector2.zero;
                alertSeal.rectTransform.sizeDelta = Vector2.one * 66f;
            }
            if (directionArrow != null)
            {
                directionArrow.anchorMin = directionArrow.anchorMax = new Vector2(.5f, .5f);
                directionArrow.anchoredPosition = new Vector2(-80f, 0f);
                directionArrow.sizeDelta = new Vector2(44f, 44f);
            }
            if (stateText != null)
            {
                stateText.rectTransform.anchoredPosition = new Vector2(26f, 0f);
                stateText.rectTransform.sizeDelta = new Vector2(150f, 60f);
                stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
                stateText.verticalOverflow = VerticalWrapMode.Truncate;
                var outline = stateText.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
            float directionBlend = Mathf.Clamp(weather.DirectionBlend, -1f, 1f);
            if (directionBlend >= 0.12f) displayedDirection = 1;
            else if (directionBlend <= -0.12f) displayedDirection = -1;

            if (directionArrow != null)
            {
                float targetAngle = downward ? -90f : updraft || phase == WindWeatherPhase.Warning
                    ? 90f
                    : (displayedDirection < 0 ? 180f : 0f);

                float smoothing = Application.isPlaying
                    ? 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime)
                    : 1f;
                float angle = Mathf.LerpAngle(
                    directionArrow.localEulerAngles.z,
                    targetAngle,
                    smoothing);
                directionArrow.localRotation = Quaternion.Euler(0f, 0f, angle);

                Vector3 scale = directionArrow.localScale;
                scale.x = Mathf.Lerp(scale.x, arrowBaseScale.x, smoothing);
                scale.y = arrowBaseScale.y;
                scale.z = arrowBaseScale.z;
                directionArrow.localScale = scale;
            }

            ApplyInkVisuals(weather.Strength01, alert);
        }

        static string GetStateLabel(WindWeatherPhase phase)
        {
            switch (phase)
            {
                case WindWeatherPhase.Warning:
                    return "상승 예고";
                case WindWeatherPhase.GaleWarning:
                    return "광풍 예고";
                case WindWeatherPhase.Gale:
                    return "광풍";
                case WindWeatherPhase.Updraft:
                    return "상승기류";
                case WindWeatherPhase.DowndraftWarning:
                    return "하강 예고";
                case WindWeatherPhase.Downdraft:
                    return "하강기류";
                case WindWeatherPhase.Recovery:
                    return "잔바람";
                default:
                    return "산들";
            }
        }

        void ApplyInkVisuals(float strength, bool alert)
        {
            if (brushArrow != null) brushArrow.color = alert ? InkPalette.Red : InkPalette.Ink;
            if (arrowGraphics != null)
            {
                for (int i = 0; i < arrowGraphics.Length; i++)
                {
                    if (arrowGraphics[i] == null) continue;
                    Color ink = InkPalette.Ink;
                    ink.a = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(strength));
                    if (lastPhase == WindWeatherPhase.Updraft) ink.a = 1f;
                    arrowGraphics[i].color = ink;
                }
            }

            if (stateText != null)
            {
                Color textColor = alert ? InkPalette.Red : InkPalette.Ink;
                textColor.a = stateFade;
                stateText.color = textColor;
            }

            // 경고는 상태 글자만 강조한다. 바람 아이콘은 크기·색을 고정한다.
        }

        void EnsureBrushArrow()
        {
            foreach (string name in new[] { "Shaft", "UpperHead", "LowerHead" })
            {
                var old = directionArrow.Find(name)?.GetComponent<Graphic>();
                if (old != null) old.enabled = false;
            }
            brushArrow = directionArrow.Find("BrushArrow")?.GetComponent<InkBrushIcon>();
            if (brushArrow == null)
            {
                var go = new GameObject("BrushArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(InkBrushIcon));
                go.transform.SetParent(directionArrow, false);
                brushArrow = go.GetComponent<InkBrushIcon>();
            }
            brushArrow.rectTransform.anchorMin = Vector2.zero;
            brushArrow.rectTransform.anchorMax = Vector2.one;
            brushArrow.rectTransform.offsetMin = brushArrow.rectTransform.offsetMax = Vector2.zero;
            brushArrow.Configure(InkBrushIcon.Symbol.Arrow);
        }

        void ConfigureStaticVisuals()
        {
            var background = GetComponent<Graphic>();
            if (background != null)
                background.enabled = false;

            if (Application.isPlaying)
                EnsureRuntimeDecorations();

            if (arrowGraphics != null)
            {
                for (int i = 0; i < arrowGraphics.Length; i++)
                {
                    if (arrowGraphics[i] is Image image)
                    {
                        if (Application.isPlaying && image.sprite == null)
                            image.sprite = InkUiTextureFactory.CreateBrushSprite();
                        image.type = Image.Type.Simple;
                    }
                }
            }

            if (stateText != null)
            {
                stateText.font = InkPalette.UiFont;
                stateText.fontSize = 34;
                stateText.fontStyle = FontStyle.Bold;
                stateText.color = InkPalette.Ink;
            }
            ConfigureWindIcon();
        }

        /// 구형 씬의 붉은 낙관과 글자를 숨기고 투명 수묵 아이콘 하나만 표시한다.
        void ConfigureWindIcon()
        {
            if (alertSeal == null)
                alertSeal = transform.Find("WindAlertSeal")?.GetComponent<Image>();
            if (alertSeal == null) return;

            alertSeal.sprite = Resources.Load<Sprite>(WindIconResourcePath);
            alertSeal.type = Image.Type.Simple;
            alertSeal.preserveAspect = true;
            alertSeal.color = Color.white;
            alertSeal.raycastTarget = false;
            alertSeal.enabled = false;
            alertSeal.rectTransform.localScale = Vector3.one;

            var oldSurface = alertSeal.transform.Find("HudInkSurface")?.GetComponent<Graphic>();
            if (oldSurface != null)
            {
                oldSurface.enabled = false;
                oldSurface.raycastTarget = false;
            }
            if (sealText == null)
                sealText = alertSeal.transform.Find("SealText")?.GetComponent<Text>();
            if (sealText != null)
            {
                sealText.enabled = false;
                sealText.raycastTarget = false;
            }
        }

        /// 구형 Main 씬에도 아이콘만 보강하며 장식 배경·글자는 만들지 않는다.
        void EnsureRuntimeDecorations()
        {
            if (alertSeal == null)
            {
                alertSeal = transform.Find("WindAlertSeal")?.GetComponent<Image>();
            }
            if (alertSeal == null)
            {
                alertSeal = CreateRuntimeImage("WindAlertSeal",
                    new Vector2(0.09f, 0.5f), new Vector2(44f, 44f), Vector2.zero);
            }
        }

        static void EnsureTextWeight(Text text)
        {
            if (text == null) return;
            var outline = text.GetComponent<Outline>();
            if (outline == null && Application.isPlaying)
                outline = text.gameObject.AddComponent<Outline>();
            if (outline == null) return;
            Color ink = InkPalette.Ink;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, 0.22f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        void ConfigureArrowPart(string name, Vector2 position, Vector2 size, float angle)
        {
            if (directionArrow == null) return;
            var rect = directionArrow.Find(name) as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        Image CreateRuntimeImage(
            string name, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var go = new GameObject(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void CacheArrowBaseScale()
        {
            if (directionArrow == null) return;

            Vector3 scale = directionArrow.localScale;
            arrowBaseScale = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                scale.y,
                scale.z);
        }

        void DisableRaycasts()
        {
            if (rootGroup != null)
            {
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }

            var graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        void ApplyVisibility()
        {
            if (rootGroup == null) return;
            rootGroup.alpha = isVisible ? 1f : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }
    }
}
