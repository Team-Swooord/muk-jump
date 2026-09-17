using System.Globalization;
using UnityEngine;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        RectTransform skillDetailsRoot, skillCooldownBadge, skillScrollHint, skillScrollThumb;
        UnityEngine.UI.Image skillIcon;
        UnityEngine.UI.Text skillTitleText, skillDescriptionText, skillCooldownText;
        UnityEngine.UI.ScrollRect skillDescriptionScroll;
        string renderedSkillDescription;
        InkSkinId detailSkin;

        public RectTransform SkillDetailsRoot => skillDetailsRoot;
        public UnityEngine.UI.Text SkillDescriptionText => skillDescriptionText;
        public UnityEngine.UI.Text SkillTitleText => skillTitleText;

        void BuildInlineSkill()
        {
            // 별도 팝업 없이 이름·설명과 같은 한지 안에서 능력을 읽는다.
            skillDetailsRoot = Rect("InlineSkillDetails", paper, new Vector2(0, -104), new Vector2(888, 248));
            skillIcon = SpriteImage("CostumeIcon", skillDetailsRoot, null, new Vector2(-382, 74), new Vector2(104, 104));
            var category = Label("Category", skillDetailsRoot, "특수 능력", 30,
                new Vector2(-50, 103), new Vector2(520, 40), true);
            category.alignment = TextAnchor.MiddleLeft;
            category.color = InkPalette.Red;
            skillTitleText = Label("SkillTitle", skillDetailsRoot, "", 50,
                new Vector2(-50, 50), new Vector2(520, 68), true);
            skillTitleText.alignment = TextAnchor.MiddleLeft;
            skillTitleText.color = InkPalette.Ink;
            skillCooldownBadge = Rect("CooldownBadge", skillDetailsRoot, new Vector2(350, 65), new Vector2(160, 72));
            var badge = skillCooldownBadge.gameObject.AddComponent<UnityEngine.UI.Image>();
            badge.sprite = InkUiStyle.ActionButtonSprite;
            InkUiStyle.ConfigureHanjiSurface(badge);
            badge.color = new Color(1, 1, 1, .7f);
            badge.raycastTarget = false;
            skillCooldownText = Label("Cooldown", skillCooldownBadge, "", 38, Vector2.zero, new Vector2(140, 58), true);
            skillCooldownText.color = InkPalette.Ink;
            InkLocalizedText.Exclude(skillCooldownText);

            var scroll = Rect("DescriptionScroll", skillDetailsRoot, new Vector2(0, -56), new Vector2(820, 144));
            skillDescriptionScroll = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Stretch("Viewport", scroll);
            var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            skillDescriptionText = Label("SkillDescription", viewport, "", 38, Vector2.zero, new Vector2(820, 144), true);
            skillDescriptionText.alignment = TextAnchor.UpperLeft;
            skillDescriptionText.color = InkPalette.Ink;
            skillDescriptionText.lineSpacing = 1.04f;
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
            skillScrollHint = Rect("ScrollHint", skillDetailsRoot, new Vector2(436, -56), new Vector2(4, 144));
            var hintTrack = skillScrollHint.gameObject.AddComponent<UnityEngine.UI.Image>();
            hintTrack.color = new Color(.11f, .106f, .102f, .12f);
            hintTrack.raycastTarget = false;
            skillScrollThumb = Rect("Thumb", skillScrollHint, Vector2.zero, new Vector2(4, 144));
            skillScrollThumb.anchorMin = skillScrollThumb.anchorMax = skillScrollThumb.pivot = new Vector2(.5f, 1);
            var thumb = skillScrollThumb.gameObject.AddComponent<UnityEngine.UI.Image>();
            thumb.color = new Color(.11f, .106f, .102f, .46f);
            thumb.raycastTarget = false;
            skillDescriptionScroll.onValueChanged.AddListener(_ => RefreshSkillScrollHint());
            skillScrollHint.gameObject.SetActive(false);
            foreach (var label in new[] { category, skillTitleText, skillDescriptionText, skillCooldownText })
                if (label.TryGetComponent<UnityEngine.UI.Outline>(out var outline)) outline.enabled = false;
            skillDetailsRoot.gameObject.SetActive(false);
        }

        void RefreshInlineSkill(bool hidden, bool hasAbility)
        {
            bool show = !collectionExpanded && !hidden && hasAbility;
            skillDetailsRoot.gameObject.SetActive(show);
            InkLocalizedText.SetSource(skillTitleText, show ? InkSkinCatalog.Ability(selected) : "");
            InkLocalizedText.SetSource(skillDescriptionText, show ? InkSkinCatalog.Description(selected) : "");
            skillIcon.sprite = show ? InkSkinCatalog.Sprite(selected) : null;
            skillIcon.enabled = skillIcon.sprite != null;
            skillIcon.color = SkinDebugSession.CanEquip(selected) ? Color.white : Color.black;
            float cooldown = show ? InkSkinChart.Cooldown(selected) : 0;
            skillCooldownBadge.gameObject.SetActive(cooldown > 0);
            string seconds = cooldown.ToString("0.#", CultureInfo.InvariantCulture);
            skillCooldownText.text = cooldown <= 0 ? "" : seconds + (GameLocalization.Language switch
            { GameLanguage.Korean => "초", GameLanguage.Japanese => "秒", _ => "s" });
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
            float height = Mathf.Max(144, Mathf.Ceil(skillDescriptionText.preferredHeight) + 2);
            skillDescriptionScroll.content.sizeDelta = new Vector2(820, height);
            skillDescriptionScroll.enabled = skillDetailsRoot.gameObject.activeSelf && CanInteract() && height > 144;
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

        void StopSkillDetails() => skillDescriptionScroll?.StopMovement();
    }
}
