using UnityEngine;
using UnityEngine.EventSystems;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        UnityEngine.UI.Button skillInfoButton;
        RectTransform skillPopupRoot, skillPopupSafeRoot, skillPopupPaper;
        UnityEngine.UI.Text skillTitleText, skillDescriptionText;
        UnityEngine.UI.ScrollRect skillDescriptionScroll;
        CanvasGroup wardrobeContentGroup;
        object skillPopupOwner;
        InkSkinId skillPopupSkin;
        uint skillPopupInactiveGeneration;

        public bool IsSkillPopupOpen => skillPopupRoot != null && skillPopupRoot.gameObject.activeSelf;
        public UnityEngine.UI.Button SkillInfoButton => skillInfoButton;
        public UnityEngine.UI.Text SkillDescriptionText => skillDescriptionText;
        public UnityEngine.UI.Text SkillTitleText => skillTitleText;
        public RectTransform SkillPopupPaper => skillPopupPaper;

        void BuildSkillPopup()
        {
            // 같은 캔버스의 마지막 형제에 표시하고, 뒤쪽 화면은 패드/키보드 탐색도 차단한다.
            wardrobeContentGroup = safeRoot.gameObject.AddComponent<CanvasGroup>();
            skillPopupRoot = Stretch("SkillDetailsOverlay", rootGroup.transform);
            var dim = Stretch("Dismiss", skillPopupRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = InkUiStyle.PopupDimColor;
            InkUiStyle.ConfigurePopupDim(dim);
            dim.raycastTarget = true;
            var dismiss = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dismiss.transition = UnityEngine.UI.Selectable.Transition.None;
            dismiss.onClick.AddListener(CloseSkillPopup);
            skillPopupSafeRoot = Stretch("SafeArea", skillPopupRoot);
            skillPopupPaper = Rect("SkillDetailsPaper", skillPopupSafeRoot, Vector2.zero, new Vector2(960, 860));
            var surface = skillPopupPaper.gameObject.AddComponent<UnityEngine.UI.Image>();
            surface.sprite = InkUiStyle.ActionButtonSprite;
            InkUiStyle.ConfigureHanjiSurface(surface);
            surface.preserveAspect = false;
            surface.raycastTarget = true;
            var category = Label("Category", skillPopupPaper, "특수 능력", 34, new Vector2(0, 344), new Vector2(820, 64), true);
            category.color = InkPalette.Red;
            skillTitleText = Label("SkillTitle", skillPopupPaper, "", 64, new Vector2(0, 240), new Vector2(820, 144), true);
            var divider = SpriteImage("Divider", skillPopupPaper, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0, 140), new Vector2(820, 4));
            divider.preserveAspect = false;
            divider.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .18f);
            var scroll = Rect("DescriptionScroll", skillPopupPaper, new Vector2(0, -40), new Vector2(820, 300));
            skillDescriptionScroll = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Stretch("Viewport", scroll);
            var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            skillDescriptionText = Label("SkillDescription", viewport, "", 48, Vector2.zero, new Vector2(820, 300), false);
            skillDescriptionText.alignment = TextAnchor.UpperLeft;
            skillDescriptionText.color = InkPalette.Ink;
            skillDescriptionText.fontStyle = FontStyle.Bold;
            skillDescriptionText.lineSpacing = 1.12f;
            var content = skillDescriptionText.rectTransform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            skillDescriptionScroll.content = content;
            skillDescriptionScroll.viewport = viewport;
            skillDescriptionScroll.horizontal = false;
            skillDescriptionScroll.vertical = true;
            skillDescriptionScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            skillDescriptionScroll.decelerationRate = .055f;
            skillDescriptionScroll.scrollSensitivity = 56;
            var close = Button("Confirm", skillPopupPaper, "닫기", new Vector2(0, -312), new Vector2(360, 112), 44, out var closeLabel);
            close.onClick.AddListener(CloseSkillPopup);
            foreach (var label in new[] { category, skillTitleText, skillDescriptionText, closeLabel })
                if (label.TryGetComponent<UnityEngine.UI.Outline>(out var outline)) outline.enabled = false;
            skillPopupRoot.gameObject.SetActive(false);
        }

        public void OpenSkillPopup()
        {
            if (!CanInteract() || collectionExpanded || SkinDebugSession.IsConcealed(selected) || !InkSkinCatalog.HasAbility(selected)) return;
            skillPopupSkin = selected;
            skillPopupOwner = PermanentGrowthProfile.SkinAccountToken;
            skillPopupInactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            presentation?.Cancel();
            swipe?.Cancel();
            StopCollection();
            wardrobeContentGroup.interactable = wardrobeContentGroup.blocksRaycasts = false;
            EventSystem.current?.SetSelectedGameObject(null);
            skillPopupRoot.gameObject.SetActive(true);
            skillDescriptionScroll.content.anchoredPosition = Vector2.zero;
            RefreshSkillPopup();
            PointerInput.SuppressUntilRelease();
        }

        public void CloseSkillPopup()
        {
            if (!IsSkillPopupOpen) return;
            skillDescriptionScroll.StopMovement();
            skillPopupRoot.gameObject.SetActive(false);
            wardrobeContentGroup.interactable = wardrobeContentGroup.blocksRaycasts = true;
            EventSystem.current?.SetSelectedGameObject(null);
            skillPopupOwner = null;
            InkLocalizedText.SetSource(skillTitleText, "");
            InkLocalizedText.SetSource(skillDescriptionText, "");
            PointerInput.SuppressUntilRelease();
        }

        void ValidateSkillPopup()
        {
            if (!IsSkillPopupOpen) return;
            if (!CanUseWardrobe() || collectionExpanded || selected != skillPopupSkin ||
                !ReferenceEquals(skillPopupOwner, PermanentGrowthProfile.SkinAccountToken) ||
                skillPopupInactiveGeneration != MobileApplicationLifecycle.InactiveGeneration ||
                SkinDebugSession.IsConcealed(selected) || !InkSkinCatalog.HasAbility(selected)) CloseSkillPopup();
        }

        void RefreshSkillPopup()
        {
            ValidateSkillPopup();
            if (!IsSkillPopupOpen) return;
            InkLocalizedText.SetSource(skillTitleText, InkSkinCatalog.Ability(selected));
            InkLocalizedText.SetSource(skillDescriptionText, InkSkinCatalog.Description(selected));
            LayoutSkillPopup();
        }

        void LayoutSkillPopup()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(skillPopupSafeRoot, safe, Screen.width, Screen.height);
            skillPopupPaper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                skillPopupPaper.sizeDelta, safe, Screen.width, Screen.height, Vector2.one * 32);
            // 원격 설명이 길어져도 팝업 안에서만 스크롤한다. 뒤쪽 카드 배치는 절대 바꾸지 않는다.
            var content = skillDescriptionText.rectTransform;
            content.sizeDelta = new Vector2(820, Mathf.Max(300, Mathf.Ceil(skillDescriptionText.preferredHeight) + 4));
        }
    }
}
