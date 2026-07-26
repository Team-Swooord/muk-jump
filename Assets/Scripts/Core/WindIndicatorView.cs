using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 화면 상단에서 현재 풍향과 상승기류 상태를 간결하게 알려 주는 HUD 뷰.
    public sealed class WindIndicatorView : MonoBehaviour
    {
        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform directionArrow;
        [SerializeField] Text stateText;
        [SerializeField] Image strengthFill;
        [FormerlySerializedAs("tintGraphics")]
        [SerializeField] Graphic[] arrowGraphics;
        [SerializeField] Graphic[] strengthBrushes;
        [SerializeField] Image alertSeal;
        [SerializeField] Text sealText;

        Vector3 arrowBaseScale = Vector3.one;
        WindWeatherPhase lastPhase;
        bool hasLastPhase;
        bool isVisible = true;
        int displayedDirection = 1;
        float stateFade = 1f;
        float sealEmphasis;

        void Awake()
        {
            ConfigureStaticVisuals();
            ApplyPolishedLayout();
            CacheArrowBaseScale();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnEnable()
        {
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
                root.anchorMin = root.anchorMax = new Vector2(0.16f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(270f, 84f);
            }

            var oldCard = GetComponent<Graphic>();
            if (oldCard != null) oldCard.enabled = false;

            if (directionArrow != null)
            {
                directionArrow.anchorMin = directionArrow.anchorMax =
                    new Vector2(0.42f, 0.5f);
                directionArrow.pivot = new Vector2(0.5f, 0.5f);
                directionArrow.anchoredPosition = Vector2.zero;
                directionArrow.sizeDelta = new Vector2(76f, 36f);
                ConfigureArrowPart("Shaft", new Vector2(-5f, 0f),
                    new Vector2(44f, 7f), 0f);
                ConfigureArrowPart("UpperHead", new Vector2(15f, 7f),
                    new Vector2(20f, 6f), -40f);
                ConfigureArrowPart("LowerHead", new Vector2(15f, -7f),
                    new Vector2(20f, 6f), 40f);
            }

            if (stateText != null)
            {
                var rect = stateText.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.76f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(112f, 40f);
                stateText.fontSize = 22;
                stateText.fontStyle = FontStyle.Normal;
                stateText.alignment = TextAnchor.MiddleCenter;
            }

            if (strengthFill != null)
                strengthFill.transform.parent.gameObject.SetActive(false);
            if (strengthBrushes != null)
            {
                for (int i = 0; i < strengthBrushes.Length; i++)
                    if (strengthBrushes[i] != null)
                        strengthBrushes[i].gameObject.SetActive(false);
            }

            if (alertSeal != null)
            {
                var rect = alertSeal.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(32f, 32f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -4f);
                alertSeal.enabled = true;
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
                    stateText.text = GetStateLabel(phase);
            }
            stateFade = Mathf.MoveTowards(
                stateFade, 1f, Time.unscaledDeltaTime / 0.12f);

            bool updraft = weather.IsUpdraftActive || phase == WindWeatherPhase.Updraft;
            bool alert = phase == WindWeatherPhase.Warning || updraft;
            float directionBlend = Mathf.Clamp(weather.DirectionBlend, -1f, 1f);
            if (directionBlend >= 0.12f) displayedDirection = 1;
            else if (directionBlend <= -0.12f) displayedDirection = -1;
            sealEmphasis = Mathf.MoveTowards(
                sealEmphasis, alert ? 1f : 0f,
                Time.unscaledDeltaTime / (alert ? 0.14f : 0.1f));

            if (directionArrow != null)
            {
                float targetAngle = updraft
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
                    return "강풍 예고";
                case WindWeatherPhase.Updraft:
                    return "상승기류";
                case WindWeatherPhase.Recovery:
                    return "잔바람";
                default:
                    return "산들";
            }
        }

        void ApplyInkVisuals(float strength, bool alert)
        {
            if (arrowGraphics != null)
            {
                for (int i = 0; i < arrowGraphics.Length; i++)
                {
                    if (arrowGraphics[i] == null) continue;
                    Color ink = InkPalette.Ink;
                    ink.a = Mathf.Lerp(0.48f, 0.84f, Mathf.Clamp01(strength));
                    if (lastPhase == WindWeatherPhase.Updraft) ink.a = 0.92f;
                    arrowGraphics[i].color = ink;
                }
            }

            if (stateText != null)
            {
                Color textColor = alert ? InkPalette.Red : InkPalette.TextDark;
                textColor.a = (alert ? 0.9f : 0.72f) * stateFade;
                stateText.color = textColor;
            }

            if (alertSeal != null)
            {
                Color red = InkPalette.Red;
                red.a = Mathf.Lerp(0.62f, 0.94f, sealEmphasis);
                alertSeal.color = red;
                alertSeal.enabled = true;
                alertSeal.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(0.94f, 1f, sealEmphasis);
            }
            if (sealText != null)
            {
                Color paper = InkPalette.Paper;
                paper.a = Mathf.Lerp(0.78f, 1f, sealEmphasis);
                sealText.color = paper;
            }
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
                stateText.fontStyle = FontStyle.Normal;
                stateText.color = InkPalette.TextDark;
            }
            if (alertSeal != null)
            {
                if (Application.isPlaying && alertSeal.sprite == null)
                    alertSeal.sprite = InkUiTextureFactory.CreateBlobSprite();
                alertSeal.color = InkPalette.Red;
            }
            if (sealText != null)
            {
                sealText.text = "풍";
                sealText.font = InkPalette.UiFont;
                sealText.fontSize = 19;
                sealText.fontStyle = FontStyle.Normal;
                sealText.alignment = TextAnchor.MiddleCenter;
                sealText.color = InkPalette.Paper;
            }
        }

        /// 구형 Main 씬에서도 중복 생성 없이 작은 풍향 낙관만 보강한다.
        void EnsureRuntimeDecorations()
        {
            if (strengthBrushes == null || strengthBrushes.Length == 0)
            {
                var found = new Graphic[3];
                int count = 0;
                for (int i = 0; i < found.Length; i++)
                {
                    found[i] = transform.Find($"WindStrengthStroke{i + 1}")
                        ?.GetComponent<Graphic>();
                    if (found[i] != null) count++;
                }
                if (count > 0) strengthBrushes = found;
            }

            if (alertSeal == null)
            {
                alertSeal = transform.Find("WindAlertSeal")?.GetComponent<Image>();
            }
            if (alertSeal == null)
            {
                alertSeal = CreateRuntimeImage("WindAlertSeal",
                    new Vector2(0.1f, 0.5f), new Vector2(32f, 32f), Vector2.zero);
                alertSeal.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, -4f);
            }
            if (sealText == null)
                sealText = alertSeal.transform.Find("SealText")?.GetComponent<Text>();
            if (sealText == null)
            {
                var go = new GameObject(
                    "SealText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(alertSeal.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(26f, 26f);
                sealText = go.GetComponent<Text>();
            }
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
