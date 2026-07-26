using UnityEngine;
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
        [SerializeField] Graphic[] tintGraphics;

        Vector3 arrowBaseScale = Vector3.one;
        WindWeatherPhase lastPhase;
        bool hasLastPhase;
        bool isVisible = true;

        void Awake()
        {
            CacheArrowBaseScale();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnEnable()
        {
            CacheArrowBaseScale();
            DisableRaycasts();
            ApplyVisibility();
        }

        void OnValidate()
        {
            CacheArrowBaseScale();
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
                strengthFill.fillAmount = Mathf.Clamp01(weather.Strength01);

            bool updraft = weather.IsUpdraftActive || phase == WindWeatherPhase.Updraft;
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
                    float directionReadability = Mathf.Lerp(0.72f, 1f, blendMagnitude);
                    targetScaleX *= horizontalSign * directionReadability;
                }

                float smoothing = Application.isPlaying
                    ? 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)
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

            Color tint = phase == WindWeatherPhase.Warning || updraft
                ? InkPalette.Gold
                : InkPalette.WindAccent;
            ApplyTint(tint);
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

        void ApplyTint(Color tint)
        {
            if (tintGraphics != null)
            {
                for (int i = 0; i < tintGraphics.Length; i++)
                {
                    if (tintGraphics[i] != null)
                        tintGraphics[i].color = tint;
                }
            }

            if (strengthFill != null)
                strengthFill.color = tint;
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
