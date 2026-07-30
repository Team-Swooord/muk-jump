using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 모든 수묵 UI 버튼이 같은 눌림 감각과 먹물 피드백을 사용하게 하는 공통 컴포넌트.
    [DisallowMultipleComponent]
    public sealed class InkUiPressFeedback : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        const float PressedScale = 0.97f;
        const float PressSpeed = 26f;
        const float ReleaseSpeed = 18f;

        RectTransform target;
        Button button;
        Vector3 restingScale = Vector3.one;
        float targetScale = 1f;
        bool isAnimating;

        void Awake()
        {
            target = transform as RectTransform;
            button = GetComponent<Button>();
            if (target != null)
                restingScale = target.localScale;
        }

        void OnEnable()
        {
            target ??= transform as RectTransform;
            if (target != null)
                restingScale = target.localScale;
            targetScale = 1f;
            isAnimating = false;
        }

        void OnDisable()
        {
            targetScale = 1f;
            isAnimating = false;
            if (target != null)
                target.localScale = restingScale;
        }

        void Update()
        {
            if (target == null || !isAnimating) return;
            float current = restingScale.x > 0.0001f
                ? target.localScale.x / restingScale.x
                : 1f;
            float speed = targetScale < current ? PressSpeed : ReleaseSpeed;
            float next = Mathf.MoveTowards(
                current,
                targetScale,
                speed * Time.unscaledDeltaTime);
            target.localScale = restingScale * next;
            if (!Mathf.Approximately(next, targetScale)) return;
            target.localScale = restingScale * targetScale;
            isAnimating = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = PressedScale;
            isAnimating = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = 1f;
            isAnimating = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = 1f;
            isAnimating = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = 1f;
            isAnimating = true;
            InkUiFeedbackController.PlayTap(eventData.position);
        }
    }

    /// 신규·기존 런타임 UI가 임의의 작은 글씨와 서로 다른 버튼 반응을 만들지 않게 하는 규칙.
    public static class InkUiStyle
    {
        public const int ScreenTitleSize = 64;
        public const int CardTitleSize = 42;
        public const int BodySize = 34;
        public const int CaptionSize = 30;
        public const int LobbyMenuSize = 37;
        public const float MinimumTapHeight = 120f;

        public static void ApplyReadableText(
            Text text,
            int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            bool strong = true,
            bool wrap = true)
        {
            if (text == null) return;
            text.font = InkPalette.UiFont;
            text.fontSize = Mathf.Max(24, fontSize);
            text.fontStyle = strong ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment;
            text.alignByGeometry = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = wrap
                ? HorizontalWrapMode.Wrap
                : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var outline = text.GetComponent<Outline>();
            if (outline == null)
                outline = text.gameObject.AddComponent<Outline>();
            Color ink = InkPalette.Ink;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, 0.2f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        public static void ConfigureButton(
            Button button,
            Graphic targetGraphic,
            bool addInkFeedback = true)
        {
            if (button == null) return;
            if (targetGraphic != null)
            {
                targetGraphic.raycastTarget = true;
                button.targetGraphic = targetGraphic;
            }
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ReadableButtonColors();

            if (addInkFeedback &&
                button.GetComponent<InkUiPressFeedback>() == null)
            {
                button.gameObject.AddComponent<InkUiPressFeedback>();
            }
        }

        public static ColorBlock ReadableButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.97f, 0.95f, 0.9f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.66f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.48f, 0.44f, 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }
    }
}
