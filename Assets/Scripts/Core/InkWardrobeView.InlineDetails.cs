using System.Globalization;
using UnityEngine;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        RectTransform skillDetailsRoot, skillCooldownBadge, skillScrollHint, skillScrollThumb;
        RectTransform skillOverlay, skillSafeArea;
        CanvasGroup wardrobeInteraction;
        UnityEngine.UI.Button skillButton;
        UnityEngine.UI.Image skillIcon, skillButtonIcon;
        UnityEngine.UI.Text skillTitleText, skillDescriptionText, skillCooldownText, skillSummaryText, skillModeText;
        UnityEngine.UI.ScrollRect skillDescriptionScroll;
        string renderedSkillDescription;
        InkSkinId detailSkin;

        public RectTransform SkillDetailsRoot => skillDetailsRoot;
        public UnityEngine.UI.Text SkillDescriptionText => skillDescriptionText;
        public UnityEngine.UI.Text SkillTitleText => skillTitleText;
        public UnityEngine.UI.Button SkillButton => skillButton;
        public UnityEngine.UI.Text SkillSummaryText => skillSummaryText;
        public bool IsSkillDetailsOpen => skillOverlay != null && skillOverlay.gameObject.activeSelf;

        void BuildInlineSkill()
        {
            // 능력 요약 → 중앙 이름·이야기 → 상세 버튼. 팝업과 아래 수집 목록은 유지한다.
            skillButton = Button("SpecialSkillButton", paper, "특수 능력", new Vector2(0, -34),
                new Vector2(280, 84), 34, out var buttonLabel);
            buttonLabel.color = InkPalette.Red;
            buttonLabel.rectTransform.anchoredPosition = new Vector2(30, 0);
            buttonLabel.rectTransform.sizeDelta = new Vector2(196, 68);
            skillButtonIcon = SpriteImage("CostumeIcon", skillButton.transform, null, new Vector2(-88, 0), new Vector2(64, 64));
            skillButton.onClick.AddListener(() => OpenSkillDetails());
            skillSummaryText = Label("SkillSummary", paper, "", 38, new Vector2(0, 245), new Vector2(900, 50), true);
            skillSummaryText.color = InkPalette.Red;
            InkLocalizedText.Exclude(skillSummaryText);

            wardrobeInteraction = paper.gameObject.AddComponent<CanvasGroup>();
            skillOverlay = Stretch("SkillDetailsOverlay", rootGroup.transform);
            var dim = Stretch("InkDim", skillOverlay).gameObject.AddComponent<UnityEngine.UI.Image>();
            InkUiStyle.ConfigurePopupDim(dim);
            skillSafeArea = Stretch("SafeAreaRoot", skillOverlay);
            skillDetailsRoot = Rect("SkillDetailsPaper", skillSafeArea, Vector2.zero, new Vector2(940, 940));
            var surface = skillDetailsRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            surface.sprite = InkUiStyle.ActionButtonSprite;
            InkUiStyle.ConfigureHanjiSurface(surface);
            surface.color = new Color(1, 1, 1, .97f);
            surface.raycastTarget = true;
            var category = Label("Category", skillDetailsRoot, "특수 능력", 46,
                new Vector2(0, 368), new Vector2(620, 70), true);
            category.color = InkPalette.Red;
            skillIcon = SpriteImage("CostumeIcon", skillDetailsRoot, null, new Vector2(-324, 234), new Vector2(116, 116));
            skillTitleText = Label("SkillTitle", skillDetailsRoot, "", 70,
                new Vector2(68, 234), new Vector2(630, 138), true);
            skillTitleText.alignment = TextAnchor.MiddleLeft;
            skillTitleText.color = InkPalette.Ink;
            var mode = SpriteImage("ModeBadge", skillDetailsRoot, InkUiStyle.ActionButtonSprite,
                new Vector2(-162, 112), new Vector2(260, 74));
            InkUiStyle.ConfigureHanjiSurface(mode);
            skillModeText = Label("Mode", mode.transform, "", 34, Vector2.zero, new Vector2(236, 60), true);
            skillModeText.color = InkPalette.Ink;
            skillCooldownBadge = Rect("CooldownBadge", skillDetailsRoot, new Vector2(150, 112), new Vector2(300, 74));
            var badge = skillCooldownBadge.gameObject.AddComponent<UnityEngine.UI.Image>();
            badge.sprite = InkUiStyle.ActionButtonSprite;
            InkUiStyle.ConfigureHanjiSurface(badge);
            badge.color = new Color(1, 1, 1, .7f);
            badge.raycastTarget = false;
            skillCooldownText = Label("Cooldown", skillCooldownBadge, "", 34, Vector2.zero, new Vector2(274, 60), true);
            skillCooldownText.color = InkPalette.Red;
            InkLocalizedText.Exclude(skillCooldownText);
            var divider = SpriteImage("Divider", skillDetailsRoot, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0, 38), new Vector2(780, 3));
            divider.preserveAspect = false; divider.color = new Color(.11f, .106f, .102f, .24f);

            var scroll = Rect("DescriptionScroll", skillDetailsRoot, new Vector2(0, -114), new Vector2(780, 236));
            skillDescriptionScroll = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Stretch("Viewport", scroll);
            var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            skillDescriptionText = Label("SkillDescription", viewport, "", 46, Vector2.zero, new Vector2(780, 236), true);
            skillDescriptionText.alignment = TextAnchor.UpperLeft;
            skillDescriptionText.color = InkPalette.Ink;
            skillDescriptionText.lineSpacing = 1.16f;
            var content = skillDescriptionText.rectTransform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            skillDescriptionScroll.content = content;
            skillDescriptionScroll.viewport = viewport;
            skillDescriptionScroll.horizontal = false;
            skillDescriptionScroll.vertical = true;
            skillDescriptionScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            skillDescriptionScroll.decelerationRate = .055f;
            skillDescriptionScroll.scrollSensitivity = 56;
            // 내용이 길 때만 가느다란 스크롤 표시를 보여 끝까지 읽을 수 있음을 알린다.
            skillScrollHint = Rect("ScrollHint", skillDetailsRoot, new Vector2(408, -114), new Vector2(4, 236));
            var hintTrack = skillScrollHint.gameObject.AddComponent<UnityEngine.UI.Image>();
            hintTrack.color = new Color(.11f, .106f, .102f, .12f);
            hintTrack.raycastTarget = false;
            skillScrollThumb = Rect("Thumb", skillScrollHint, Vector2.zero, new Vector2(4, 236));
            skillScrollThumb.anchorMin = skillScrollThumb.anchorMax = skillScrollThumb.pivot = new Vector2(.5f, 1);
            var thumb = skillScrollThumb.gameObject.AddComponent<UnityEngine.UI.Image>();
            thumb.color = new Color(.11f, .106f, .102f, .46f);
            thumb.raycastTarget = false;
            skillDescriptionScroll.onValueChanged.AddListener(_ => RefreshSkillScrollHint());
            skillScrollHint.gameObject.SetActive(false);
            var confirm = Button("ConfirmButton", skillDetailsRoot, "알겠습니다", new Vector2(0, -350),
                new Vector2(720, 116), 46, out var confirmLabel);
            confirm.onClick.AddListener(CloseSkillDetails);
            var close = Button("CloseButton", skillDetailsRoot, "×", new Vector2(382, 370),
                new Vector2(108, 108), 68, out var closeLabel);
            close.transform.Find("Paper").GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            close.onClick.AddListener(CloseSkillDetails);
            foreach (var label in new[] { buttonLabel, skillSummaryText, category, skillTitleText, skillDescriptionText,
                skillCooldownText, skillModeText, confirmLabel, closeLabel })
                if (label.TryGetComponent<UnityEngine.UI.Outline>(out var outline)) outline.enabled = false;
            skillOverlay.gameObject.SetActive(false);
            skillDetailsRoot.gameObject.SetActive(false);
        }

        void RefreshInlineSkill(bool hidden, bool hasAbility)
        {
            bool show = !collectionExpanded && !hidden && hasAbility;
            skillButton.gameObject.SetActive(show);
            skillSummaryText.gameObject.SetActive(show);
            if (!show) CloseSkillDetails();
            InkLocalizedText.SetSource(skillTitleText, show ? InkSkinCatalog.Ability(selected) : "");
            InkLocalizedText.SetSource(skillDescriptionText, show ? InkSkinCatalog.Description(selected) : "");
            skillIcon.sprite = show ? InkSkinCatalog.Sprite(selected) : null;
            skillIcon.enabled = skillIcon.sprite != null;
            skillIcon.color = SkinDebugSession.CanEquip(selected) ? Color.white : Color.black;
            skillButtonIcon.sprite = skillIcon.sprite;
            skillButtonIcon.enabled = skillIcon.enabled;
            skillButtonIcon.color = skillIcon.color;
            InkLocalizedText.SetSource(skillModeText, selected == InkSkinId.WindFan ? "직접 사용" : "자동 발동");
            float cooldown = show ? InkSkinChart.Cooldown(selected) : 0;
            skillCooldownBadge.gameObject.SetActive(cooldown > 0);
            skillModeText.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(cooldown > 0 ? -162 : 0, 112);
            string seconds = cooldown.ToString("0.#", CultureInfo.InvariantCulture);
            string duration = seconds + (GameLocalization.Language switch
            { GameLanguage.Korean => "초", GameLanguage.Japanese => "秒", _ => "s" });
            skillCooldownText.text = cooldown <= 0 ? "" : GameLocalization.Translate("재사용") + " " + duration;
            skillSummaryText.text = !show ? "" : GameLocalization.Translate(InkSkinCatalog.Ability(selected)) +
                (cooldown > 0 ? " · " + duration : "");
            if (detailSkin != selected || renderedSkillDescription != skillDescriptionText.text)
            {
                skillDescriptionScroll.StopMovement();
                skillDescriptionScroll.content.anchoredPosition = Vector2.zero;
                detailSkin = selected;
                renderedSkillDescription = skillDescriptionText.text;
            }
            LayoutInlineSkill();
        }

        void LayoutInlineSkill()
        {
            // 긴 원격 문구만 한지 안에서 스크롤한다. 아래 수집 목록의 위치/크기는 고정한다.
            float visibleHeight = skillDescriptionScroll.viewport.rect.height;
            float height = Mathf.Max(visibleHeight, Mathf.Ceil(skillDescriptionText.preferredHeight) + 2);
            skillDescriptionScroll.content.sizeDelta = new Vector2(skillDescriptionScroll.viewport.rect.width, height);
            skillDescriptionScroll.enabled = IsSkillDetailsOpen && CanUseWardrobe() && height > visibleHeight;
            skillDescriptionScroll.viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget = skillDescriptionScroll.enabled;
            RefreshSkillScrollHint();
        }

        void RefreshSkillScrollHint()
        {
            float visible = skillDescriptionScroll.viewport.rect.height;
            float content = skillDescriptionScroll.content.rect.height;
            bool overflow = content > visible + 1;
            skillScrollHint.gameObject.SetActive(overflow);
            if (!overflow) return;
            float height = Mathf.Clamp(visible * visible / content, 16, visible);
            skillScrollThumb.sizeDelta = new Vector2(4, height);
            skillScrollThumb.anchoredPosition = new Vector2(0,
                -(visible - height) * (1 - Mathf.Clamp01(skillDescriptionScroll.verticalNormalizedPosition)));
        }

        public bool OpenSkillDetails()
        {
            if (!CanInteract() || collectionExpanded || SkinDebugSession.IsConcealed(selected) || !InkSkinCatalog.HasAbility(selected)) return false;
            StopCollection(); swipe?.Cancel(); presentation?.Cancel();
            skillOverlay.SetAsLastSibling();
            skillOverlay.gameObject.SetActive(true); skillDetailsRoot.gameObject.SetActive(true);
            wardrobeInteraction.interactable = wardrobeInteraction.blocksRaycasts = false;
            skillDescriptionScroll.StopMovement();
            skillDescriptionScroll.content.anchoredPosition = Vector2.zero;
            RefreshInlineSkill(false, true); LayoutSkillPopup();
            PointerInput.SuppressUntilRelease();
            return true;
        }

        public void CloseSkillDetails()
        {
            skillDescriptionScroll?.StopMovement();
            if (!IsSkillDetailsOpen) return;
            skillDetailsRoot.gameObject.SetActive(false); skillOverlay.gameObject.SetActive(false);
            wardrobeInteraction.interactable = wardrobeInteraction.blocksRaycasts = true;
            if (collectionScroll != null) collectionScroll.enabled = CanInteract();
            PointerInput.SuppressUntilRelease();
        }

        void LayoutSkillPopup()
        {
            if (skillSafeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(skillSafeArea, safe, Screen.width, Screen.height);
            skillDetailsRoot.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                skillDetailsRoot.sizeDelta, safe, Screen.width, Screen.height, Vector2.one * 32);
        }

        void StopSkillDetails() => CloseSkillDetails();
    }
}
