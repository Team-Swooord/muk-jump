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
        const float PressSpeed = 30f;
        const float ReleaseSpeed = 42f;

        RectTransform target;
        Button button;
        Vector3 restingScale = Vector3.one;
        float targetScale = 1f;
        bool isAnimating;
        bool pointerHeld;
        int pressedPointerId;

        void Awake()
        {
            target = transform as RectTransform;
            button = GetComponent<Button>();
            if (target != null)
                restingScale = target.localScale;
        }

        void OnEnable()
        {
            if (target == null) target = transform as RectTransform;
            if (button == null) button = GetComponent<Button>();
            if (target != null)
                restingScale = target.localScale;
            targetScale = 1f;
            isAnimating = false;
            pointerHeld = false;
        }

        void OnDisable() => ResetPress();
        void OnApplicationPause(bool paused) { if (paused) ResetPress(); }
        void OnApplicationFocus(bool focused) { if (!focused) ResetPress(); }

        void ResetPress()
        {
            // 레이아웃이 갱신한 대기 중 크기는 건드리지 않는다.
            if (target != null && (isAnimating || pointerHeld))
                target.localScale = restingScale;
            targetScale = 1f;
            isAnimating = false;
            pointerHeld = false;
        }

        void Update() => AdvancePress(Time.unscaledDeltaTime);

        void AdvancePress(float deltaTime)
        {
            if (target == null || (!isAnimating && !pointerHeld)) return;
            if (!MobileApplicationLifecycle.IsApplicationActive || !CanPress())
            {
                ResetPress();
                return;
            }
            if (!isAnimating) return;
            float current = restingScale.x > 0.0001f
                ? target.localScale.x / restingScale.x
                : 1f;
            float speed = targetScale < current ? PressSpeed : ReleaseSpeed;
            float next = Mathf.Lerp(
                current,
                targetScale,
                1f - Mathf.Exp(-speed * Mathf.Max(0f, deltaTime)));
            target.localScale = restingScale * next;
            if (Mathf.Abs(next - targetScale) > .0004f) return;
            target.localScale = restingScale * targetScale;
            isAnimating = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsPrimaryPointer(eventData) || !CanPress() || pointerHeld) return;
            // 안전 영역/부모 레이아웃이 Awake 이후 정한 실제 크기를 기준으로 누른다.
            if (!isAnimating && target != null) restingScale = target.localScale;
            pressedPointerId = eventData?.pointerId ?? -1;
            pointerHeld = true;
            targetScale = LobbySettingsProfile.ReducedMotionEnabled ? .995f : PressedScale;
            isAnimating = true;
        }

        public void OnPointerUp(PointerEventData eventData) => ReleasePointer(eventData);

        public void OnPointerExit(PointerEventData eventData) => ReleasePointer(eventData);

        public void OnPointerClick(PointerEventData eventData)
        {
            ReleasePointer(eventData);
            // 먹 번짐은 화면 공통 입력에서 누르는 순간 한 번만 재생한다.
            // 버튼은 눌림 크기만 담당해 손을 뗄 때 두 번째 효과가 생기지 않게 한다.
        }

        void ReleasePointer(PointerEventData eventData)
        {
            if (!pointerHeld || !IsPrimaryPointer(eventData) ||
                pressedPointerId != (eventData?.pointerId ?? -1)) return;
            pointerHeld = false;
            targetScale = 1f;
            isAnimating = true;
        }

        bool CanPress() => isActiveAndEnabled &&
            (button == null || (button.IsActive() && button.IsInteractable()));

        static bool IsPrimaryPointer(PointerEventData data) =>
            data == null || data.button == PointerEventData.InputButton.Left;
    }

    public enum ActionButtonRole
    {
        Primary,
        Secondary,
    }

    public enum ActionButtonLayout
    {
        SingleLine,
        TwoLine,
    }


    /// 신규·기존 런타임 UI가 임의의 작은 글씨와 서로 다른 버튼 반응을 만들지 않게 하는 규칙.
    public static class InkUiStyle
    {
        public const string ActionButtonResourcePath =
            "MukJump/UI/Common/action_button_hanji_v1";
        public const int ScreenTitleSize = 64;
        // 붓글씨는 실제 획이 작아 모달의 짧은 제목·안내에는 별도 최소 크기를 둔다.
        public const int PauseTitleSize = 80;
        public const int InstructionTitleSize = 64;
        public const int InstructionBodySize = 52;
        public const int InstructionCaptionSize = 44;
        public const int CardTitleSize = 42;
        public const int BodySize = 34;
        public const int CaptionSize = 32;
        public const int LobbyMenuSize = 46;
        public const int StandardButtonLabelSize = 36;
        public const int ActionButtonLabelSize = 40;
        public const float MinimumTapHeight = 120f;
        public const float PopupDimAlpha = 0.78f;
        public const float ActionButtonBorderWidth = 6f;
        public const float HanjiPixelsPerUnitMultiplier = 1f;
        public const float ActionButtonTwoLineHeight = 152f;
        public const float ActionLabelHorizontalInsetRatio = 0.12f;
        public const float ActionLabelVerticalInsetRatio = 0.15f;

        /// 팝업 뒤의 플레이·로비 UI를 한 단계 뒤로 보내는 공통 먹색 장막.
        /// 화면마다 제각각인 옅은 회색을 쓰지 않아 팝업의 깊이와 입력 경계를 통일한다.
        public static Color PopupDimColor =>
            new Color(0.035f, 0.032f, 0.028f, PopupDimAlpha);

        /// 모달 팝업이 항상 같은 깊이와 입력 경계를 갖도록 배경 장막을 한 번에 설정한다.
        public static void ConfigurePopupDim(Image dim)
        {
            if (dim == null) return;
            dim.color = PopupDimColor;
            dim.raycastTarget = true;
        }

        static Sprite actionButtonSprite;
        public static Color HanjiPaperColor => Color.Lerp(InkPalette.Paper, InkPalette.TextLight, .35f);

        /// 텍스트 행동 버튼이 공통으로 사용하는 한지 카드 스프라이트.
        public static Sprite ActionButtonSprite
        {
            get
            {
                if (actionButtonSprite == null)
                {
                    Sprite paper = InkUiTextureFactory.CreateGrowthPaperRibbonSprite();
                    // 같은 종이 원화의 불규칙한 가장자리를 쓰되 짧은 버튼의 모서리는 늘리지 않는다.
                    actionButtonSprite = Sprite.Create(paper.texture, paper.rect,
                        new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                        new Vector4(40f, 40f, 40f, 40f));
                    actionButtonSprite.name = "MukJump_SharedHanjiCard";
                }
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
            InkLocalizedText.Bind(text);
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

        /// 다음·이전·확인·닫기처럼 독립된 직사각형 텍스트 CTA를
        /// 공통 한지 카드로 통일한다. 선택 카드·토글·원형 아이콘·브랜드 버튼에는
        /// 호출하지 않아 각 컨트롤의 의미와 고유 실루엣을 보존한다. 로비의
        /// 시작·성장·옵션은 사용자가 지정한 전용 검정 먹물 PNG를 쓴다.
        public static void ConfigureActionButton(
            Button button,
            Image background,
            Text label,
            ActionButtonRole role = ActionButtonRole.Primary,
            ActionButtonLayout layout = ActionButtonLayout.SingleLine,
            Graphic obsoleteSurface = null,
            FontStyle labelStyle = FontStyle.Bold)
        {
            if (button == null || background == null) return;
            ConfigureButton(button, background);
            Image actionGraphic = ConfigureActionSurface(
                background,
                label,
                role,
                layout,
                obsoleteSurface,
                labelStyle);
            if (actionGraphic != null)
            {
                button.targetGraphic = actionGraphic;
                button.colors = ActionButtonColors();
                // 한지 원본색은 Image 한 장에서만 보존한다. 첫 상태 전환 전에도
                // 다른 색이 번쩍이지 않도록 렌더 색을 즉시 정상 상태에 맞춘다.
                actionGraphic.canvasRenderer.SetColor(button.colors.normalColor);
            }
        }

        /// 버튼 컴포넌트 없이 전체 화면 터치로 동작하는 CTA도 같은 모양을 쓴다.
        /// 한지 외곽은 한 장만 그리고 중요한 행동은 종이 안쪽의 옅은 번짐으로 구분한다.
        public static Image ConfigureActionSurface(
            Image background,
            Text label,
            ActionButtonRole role = ActionButtonRole.Primary,
            ActionButtonLayout layout = ActionButtonLayout.SingleLine,
            Graphic obsoleteSurface = null,
            FontStyle labelStyle = FontStyle.Bold)
        {
            if (background == null) return null;

            ApplyHanjiLayer(background, true);
            Mask legacyMask = background.GetComponent<Mask>();
            if (legacyMask != null)
                legacyMask.enabled = false;

            Transform generatedSurface = background.transform.Find("Surface");
            if (generatedSurface != null)
                DisableObsoleteSurface(
                    generatedSurface.GetComponent<Graphic>(), background);

            DisableObsoleteSurface(obsoleteSurface, background);

            var visual = background.GetComponent<InkActionButtonVisual>();
            if (visual == null)
                visual = background.gameObject.AddComponent<InkActionButtonVisual>();
            visual.Border = background;
            visual.Surface = null;
            visual.Label = label;
            visual.Role = role;
            visual.Layout = layout;
            visual.LabelStyle = labelStyle;

            SetActionButtonRole(background, role);
            ApplyActionButtonLayout(visual);
            return background;
        }

        static void ApplyHanjiLayer(Image image, bool receivesRaycast)
        {
            if (image == null) return;
            Sprite hanjiSprite = ActionButtonSprite;
            image.sprite = hanjiSprite;
            image.type = hanjiSprite != null
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.preserveAspect = false;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = HanjiPixelsPerUnitMultiplier;
            image.color = HanjiPaperColor;
            image.raycastTarget = receivesRaycast;
            HanjiCardFrame.Ensure(image);
        }

        static void DisableObsoleteSurface(
            Graphic obsoleteSurface,
            Image background)
        {
            if (obsoleteSurface == null || obsoleteSurface == background)
                return;
            obsoleteSurface.raycastTarget = false;
            if (obsoleteSurface is Image image)
            {
                image.sprite = null;
                image.color = Color.clear;
            }
        }

        /// 옵션의 조용한 한지 면을 공통으로 쓴다. 라벨 크기와 터치 영역은 바꾸지 않는다.
        public static void ConfigureHanjiSurface(
            Image background,
            ActionButtonRole role = ActionButtonRole.Secondary)
        {
            if (background == null) return;
            ApplyHanjiLayer(background, background.raycastTarget);
            foreach (Outline outline in background.GetComponents<Outline>())
                outline.enabled = false;
            bool primary = role == ActionButtonRole.Primary;
            background.color = primary ? Color.Lerp(HanjiPaperColor, InkPalette.Paper2, .12f) : HanjiPaperColor;
            ConfigureInteriorWash(background, "RoleWash", primary,
                new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.82f), InkPalette.Red, 0.075f);
            ConfigureInteriorWash(background, "RoleWashSoft", primary,
                new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.75f), InkPalette.Ink, 0.025f);
        }

        static void ConfigureInteriorWash(Image paper, string name, bool visible,
            Vector2 anchorMin, Vector2 anchorMax, Color color, float alpha)
        {
            var wash = paper.transform.Find(name)?.GetComponent<Image>();
            if (wash == null && !visible) return;
            if (wash == null)
            {
                var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                root.transform.SetParent(paper.transform, false);
                wash = root.GetComponent<Image>();
            }
            // 외곽까지 번지지 않는 정적 장식이다. 상태 갱신 시 오브젝트를 재생성하지 않는다.
            wash.transform.SetAsFirstSibling();
            wash.rectTransform.anchorMin = anchorMin;
            wash.rectTransform.anchorMax = anchorMax;
            wash.rectTransform.offsetMin = wash.rectTransform.offsetMax = Vector2.zero;
            wash.sprite = InkUiTextureFactory.CreateBlobSprite();
            color.a = alpha;
            wash.color = color;
            wash.raycastTarget = false;
            wash.enabled = visible;
        }

        public static void SetActionButtonRole(
            Image background,
            ActionButtonRole role)
        {
            if (background == null) return;
            InkActionButtonVisual visual =
                background.GetComponent<InkActionButtonVisual>() ??
                background.GetComponentInParent<InkActionButtonVisual>();
            Image border = visual != null && visual.Border != null
                ? visual.Border
                : background;
            ConfigureHanjiSurface(border, role);
            if (visual != null)
                visual.Role = role;
        }

        public static void SetActionButtonLayout(
            Image background,
            ActionButtonLayout layout)
        {
            if (background == null) return;
            InkActionButtonVisual visual =
                background.GetComponent<InkActionButtonVisual>() ??
                background.GetComponentInParent<InkActionButtonVisual>();
            if (visual == null) return;
            visual.Layout = layout;
            ApplyActionButtonLayout(visual);
        }

        public static void RefreshActionButtonLayout(Image background)
        {
            if (background == null) return;
            InkActionButtonVisual visual =
                background.GetComponent<InkActionButtonVisual>() ??
                background.GetComponentInParent<InkActionButtonVisual>();
            if (visual != null)
                ApplyActionButtonLayout(visual);
        }

        static void ApplyActionButtonLayout(InkActionButtonVisual visual)
        {
            if (visual == null || visual.Border == null) return;
            RectTransform borderRect = visual.Border.rectTransform;
            float minimumHeight = visual.Layout == ActionButtonLayout.TwoLine
                ? ActionButtonTwoLineHeight
                : MinimumTapHeight;
            Vector2 size = borderRect.sizeDelta;
            if (size.y < minimumHeight)
            {
                size.y = minimumHeight;
                borderRect.sizeDelta = size;
            }

            Text label = visual.Label;
            if (label == null) return;
            label.font = InkPalette.UiFont;
            label.fontSize = ActionButtonLabelSize;
            label.fontStyle = visual.LabelStyle;
            label.alignment = TextAnchor.MiddleCenter;
            label.alignByGeometry = true;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = visual.Layout == ActionButtonLayout.TwoLine
                ? 0.92f
                : 1f;
            label.color = InkPalette.TextDark;
            label.raycastTarget = false;
            foreach (Outline outline in label.GetComponents<Outline>())
                outline.enabled = false;

            RectTransform labelRect = label.rectTransform;
            float horizontalInset =
                visual.Layout == ActionButtonLayout.TwoLine ? 28f : 32f;
            float verticalInset = visual.Layout == ActionButtonLayout.TwoLine
                ? 16f
                : 20f;
            labelRect.anchorMin = labelRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(
                Mathf.Max(1f, borderRect.sizeDelta.x - horizontalInset * 2f),
                Mathf.Max(1f, borderRect.sizeDelta.y - verticalInset * 2f));
        }

        /// 붓획 PNG의 투명·거친 가장자리에 글자가 걸리지 않도록 실제 버튼 크기에
        /// 비례한 안전 여백을 둔다. 작은 뒤로 버튼과 넓은 CTA 모두 같은 비율로 읽힌다.
        public static void ApplyActionLabelPadding(
            RectTransform surface,
            RectTransform label)
        {
            if (surface == null || label == null || label.parent != surface)
                return;

            Vector2 surfaceSize = surface.sizeDelta;
            if (surfaceSize.x <= 0f || surfaceSize.y <= 0f)
                return;

            float horizontalInset = Mathf.Clamp(
                surfaceSize.x * ActionLabelHorizontalInsetRatio,
                28f,
                64f);
            float verticalInset = Mathf.Clamp(
                surfaceSize.y * ActionLabelVerticalInsetRatio,
                14f,
                24f);
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.pivot = new Vector2(0.5f, 0.5f);
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(
                Mathf.Max(1f, surfaceSize.x - horizontalInset * 2f),
                Mathf.Max(1f, surfaceSize.y - verticalInset * 2f));
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
            // 성장 화면은 동일한 한지 스프라이트를 Image에 직접 사용한다.
            // 시각 래퍼 유무가 아니라 실제 표시 자산으로 판별한다.
            return image != null && ActionButtonSprite != null &&
                   image.sprite == ActionButtonSprite;
        }

        public static ColorBlock ActionButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = InkPalette.TextLight;
            colors.pressedColor = InkPalette.Paper2;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.Lerp(
                Color.white,
                InkPalette.TextMuted,
                0.24f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        public static ColorBlock ReadableButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.97f, 0.95f, 0.9f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.66f, 1f);
            colors.selectedColor = Color.white;
            // 비활성 버튼도 글자를 읽을 수 있어야 한다. 한지 위에서 표면 알파가
            // 지나치게 낮아지면 흰 라벨과 함께 회색 얼룩처럼 보이므로 형태는
            // 유지하고 명도 차이만 줄여 비활성 상태를 표현한다.
            colors.disabledColor = new Color(0.72f, 0.69f, 0.63f, 0.84f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }
    }
}
