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
        public const string ActionButtonResourcePath =
            "MukJump/UI/Common/action_button_brush";
        public const int ScreenTitleSize = 64;
        public const int CardTitleSize = 42;
        public const int BodySize = 34;
        public const int CaptionSize = 30;
        public const int LobbyMenuSize = 46;
        public const int StandardButtonLabelSize = 36;
        public const int ActionButtonLabelSize = 40;
        public const float MinimumTapHeight = 120f;

        static Sprite actionButtonSprite;

        /// 로비의 네 메뉴 버튼을 제외한 명시적 행동 버튼이 공유하는 붓획 마스크.
        public static Sprite ActionButtonSprite
        {
            get
            {
                if (actionButtonSprite == null)
                    actionButtonSprite =
                        Resources.Load<Sprite>(ActionButtonResourcePath);
                return actionButtonSprite;
            }
        }

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

            Text directLabel =
                button.transform.Find("Label")?.GetComponent<Text>();
            if (directLabel != null)
                ApplyButtonLabel(directLabel, StandardButtonLabelSize);
        }

        /// 다음·이전·확인·닫기처럼 화면의 흐름을 바꾸는 버튼만 공통 붓획으로 통일한다.
        /// 카드 선택 영역·토글·로비 메뉴에는 호출하지 않는다.
        public static void ConfigureActionButton(
            Button button,
            Image background,
            Text label,
            Graphic obsoleteSurface = null)
        {
            if (button == null || background == null) return;
            ConfigureActionSurface(background, label, obsoleteSurface);
            ConfigureButton(button, background);
        }

        /// 버튼 컴포넌트 없이 전체 화면 터치로 동작하는 게임오버 CTA도 같은 모양을 쓴다.
        public static void ConfigureActionSurface(
            Image background,
            Text label,
            Graphic obsoleteSurface = null)
        {
            if (background == null) return;

            Sprite sprite = ActionButtonSprite;
            bool usesImportedSprite = sprite != null;
            background.sprite = usesImportedSprite
                ? sprite
                : InkUiTextureFactory.CreateBrushSprite();
            // 공용 붓획은 작은 뒤로/닫기 버튼에서도 전체 모양이 보여야 한다.
            // 9-slice는 큰 테두리가 서로 맞닿으며 모서리 먹점만 남기므로 사용하지 않는다.
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            background.fillCenter = true;
            background.color = InkPalette.Ink;
            background.raycastTarget = true;

            if (obsoleteSurface != null && obsoleteSurface != background)
            {
                obsoleteSurface.raycastTarget = false;
                if (obsoleteSurface is Image image)
                {
                    image.sprite = null;
                    image.color = Color.clear;
                }
            }

            if (label == null) return;
            label.color = InkPalette.TextLight;
            label.raycastTarget = false;
            ApplyButtonLabel(label, ActionButtonLabelSize);
        }

        public static void ApplyButtonLabel(Text label, int minimumSize)
        {
            if (label == null) return;
            ApplyReadableText(
                label,
                Mathf.Max(label.fontSize, minimumSize),
                label.alignment,
                strong: true,
                wrap: false);
            label.color = InkPalette.TextLight;

            // 반투명 붓 가장자리 위에서도 흰 획의 외곽이 무너지지 않게 한다.
            var outline = label.GetComponent<Outline>();
            if (outline == null)
                outline = label.gameObject.AddComponent<Outline>();
            Color ink = InkPalette.Ink;
            outline.effectColor =
                new Color(ink.r, ink.g, ink.b, 0.68f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        public static bool UsesActionButtonSprite(Image image)
        {
            return image != null &&
                   ActionButtonSprite != null &&
                   image.sprite == ActionButtonSprite;
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
