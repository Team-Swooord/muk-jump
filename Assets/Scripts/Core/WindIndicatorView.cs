using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 화면 상단에서 현재 풍향과 상승기류 상태를 간결하게 알려 주는 HUD 뷰.
    [ExecuteAlways]
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

        Vector3 arrowBaseScale = Vector3.one;
        WindWeatherPhase lastPhase;
        bool hasLastPhase;
        bool isVisible = true;

        void Awake()
        {
            CacheArrowBaseScale();
            ConfigureStaticVisuals();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnEnable()
        {
            CacheArrowBaseScale();
            ConfigureStaticVisuals();
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

        void ApplyState(WindWeatherController weather)
        {
            WindWeatherPhase phase = weather.Phase;
            if (!hasLastPhase || lastPhase != phase)
            {
                hasLastPhase = true;
                lastPhase = phase;
                if (stateText != null)
                    stateText.text = GetStateLabel(phase);
            }

            if (strengthFill != null)
            {
                strengthFill.fillAmount = Mathf.Clamp01(weather.Strength01);
                Color fillColor = InkPalette.Ink;
                fillColor.a = 0.7f;
                strengthFill.color = fillColor;
            }

            bool updraft = weather.IsUpdraftActive || phase == WindWeatherPhase.Updraft;
            bool alert = phase == WindWeatherPhase.Warning || updraft;
            float directionBlend = Mathf.Clamp(weather.DirectionBlend, -1f, 1f);
            float blendMagnitude = Mathf.Abs(directionBlend);
            int fallbackSign = weather.DirectionSign < 0 ? -1 : 1;
            float horizontalSign = blendMagnitude > 0.02f
                ? Mathf.Sign(directionBlend)
                : fallbackSign;

            if (directionArrow != null)
            {
                float targetAngle = updraft ? 90f : 0f;
                float targetScaleX = arrowBaseScale.x;
                if (!updraft)
                {
                    float directionReadability = Mathf.Lerp(
                        0.08f, 1f, Mathf.SmoothStep(0f, 1f, blendMagnitude));
                    targetScaleX *= horizontalSign * directionReadability;
                }

                float smoothing = Application.isPlaying
                    ? 1f - Mathf.Exp(-7.5f * Time.unscaledDeltaTime)
                    : 1f;
                float angle = Mathf.LerpAngle(
                    directionArrow.localEulerAngles.z,
                    targetAngle,
                    smoothing);
                directionArrow.localRotation = Quaternion.Euler(0f, 0f, angle);

                Vector3 scale = directionArrow.localScale;
                scale.x = Mathf.Lerp(scale.x, targetScaleX, smoothing);
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
                    return "상승기류 예고";
                case WindWeatherPhase.Updraft:
                    return "상승기류";
                case WindWeatherPhase.Recovery:
                    return "바람 잦음";
                default:
                    return "산들바람";
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
                    ink.a = 0.92f;
                    arrowGraphics[i].color = ink;
                }
            }

            if (strengthBrushes != null)
            {
                int count = Mathf.Max(1, strengthBrushes.Length);
                for (int i = 0; i < strengthBrushes.Length; i++)
                {
                    if (strengthBrushes[i] == null) continue;
                    float threshold = (i + 1f) / count;
                    float active = Mathf.InverseLerp(
                        threshold - 0.34f, threshold, Mathf.Clamp01(strength));
                    Color ink = InkPalette.Ink;
                    ink.a = Mathf.Lerp(0.12f, 0.78f, active);
                    strengthBrushes[i].color = ink;
                }
            }

            if (stateText != null)
                stateText.color = alert ? InkPalette.Red : InkPalette.TextDark;

            if (alertSeal != null)
            {
                alertSeal.enabled = alert;
                alertSeal.color = InkPalette.Red;
            }
        }

        void ConfigureStaticVisuals()
        {
            var background = GetComponent<Graphic>();
            Sprite brushSprite = InkUiTextureFactory.CreateBrushSprite();
            if (Application.isPlaying && background is RawImage rawBackground)
                rawBackground.texture = brushSprite.texture;
            if (background != null)
            {
                Color paper = InkPalette.Paper;
                paper.a = 0.9f;
                background.color = paper;
            }

            if (Application.isPlaying)
                EnsureRuntimeDecorations(brushSprite);

            if (arrowGraphics != null)
            {
                for (int i = 0; i < arrowGraphics.Length; i++)
                {
                    if (arrowGraphics[i] is Image image)
                    {
                        image.sprite = brushSprite;
                        image.type = Image.Type.Simple;
                    }
                }
            }

            if (stateText != null)
            {
                stateText.font = InkPalette.UiFont;
                stateText.color = InkPalette.TextDark;
            }
            if (strengthFill != null)
            {
                strengthFill.sprite = brushSprite;
                var track = strengthFill.transform.parent?.GetComponent<Image>();
                if (track != null) track.color = Color.clear;
            }
            if (alertSeal != null)
            {
                alertSeal.sprite = InkUiTextureFactory.CreateBlobSprite();
                alertSeal.color = InkPalette.Red;
                if (!Application.isPlaying) alertSeal.enabled = true;
            }
            ApplyInkVisuals(0.42f, !Application.isPlaying);
        }

        /// 구형 Main 씬의 직선 게이지를 재생성 없이 세 개의 짧은 먹 붓결로 교체한다.
        void EnsureRuntimeDecorations(Sprite brushSprite)
        {
            if (strengthBrushes == null || strengthBrushes.Length == 0)
            {
                strengthBrushes = new Graphic[3];
                float[] widths = { 28f, 38f, 48f };
                float[] positions = { -45f, 0f, 49f };
                for (int i = 0; i < strengthBrushes.Length; i++)
                {
                    var image = CreateRuntimeImage(
                        $"WindStrengthStroke{i + 1}",
                        new Vector2(0.67f, 0.25f),
                        new Vector2(widths[i], 7f),
                        new Vector2(positions[i], 0f));
                    image.sprite = brushSprite;
                    image.rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, -2f + i * 2f);
                    strengthBrushes[i] = image;
                }

                if (strengthFill != null)
                    strengthFill.transform.parent.gameObject.SetActive(false);
            }

            if (alertSeal == null)
            {
                alertSeal = CreateRuntimeImage(
                    "WindAlertSeal",
                    new Vector2(0.94f, 0.78f),
                    new Vector2(22f, 22f),
                    Vector2.zero);
                alertSeal.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, 8f);
            }
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
