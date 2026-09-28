using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        bool categoryHome = NextUpdateScope.DeferredWardrobeEnabled;
        RectTransform homePreview;
        Image homeBody, homeAccessory;
        Text categoryTitle;
        public bool IsCategoryHome => categoryHome;

        static string CategoryName(InkCosmeticSlot slot) => slot switch
        {
            InkCosmeticSlot.Skin => "스킨",
            InkCosmeticSlot.Landing => "발판 효과",
            InkCosmeticSlot.Brush => "붓 효과",
            _ => "분신 외형"
        };

        void BuildCategoryHome()
        {
            categoryTitle = Label("CategoryTitle", paper, "", 78, new Vector2(0, 884), new Vector2(480, 136), true);
            BuildWardrobeInkAccents();
            slotRoot = Rect("CosmeticCategories", paper, new Vector2(0, 60), new Vector2(1000, 1100));
            var wash = SpriteImage("CenterHanjiWash", slotRoot, InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, Vector2.one * 580);
            wash.color = new Color(InkPalette.TextLight.r, InkPalette.TextLight.g, InkPalette.TextLight.b, .78f);
            var ring = SpriteImage("CenterBrushCircle", slotRoot, Resources.Load<Sprite>("MukJump/UI/PermanentGrowth/pg_selected_ring"),
                Vector2.zero, Vector2.one * 570);
            ring.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .32f);
            var slots = new[] { InkCosmeticSlot.Skin, InkCosmeticSlot.Landing, InkCosmeticSlot.Brush, InkCosmeticSlot.Clone };
            var captions = new[] { "옷과 장식", "착지하는 순간", "준비 중", "분신의 모습" };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                float side = i % 2 == 0 ? -1 : 1, row = i < 2 ? 1 : -1;
                bool available = slot != InkCosmeticSlot.Brush;
                var tint = InkPalette.Ink; tint.a = available ? .62f : .22f;
                // 먹에서 네 모서리로 뻗는 단순한 꺾인 획. 장식은 터치를 가로채지 않는다.
                BranchLine("Branch" + slot + "Stem", new Vector2(side * 190, row * 165), new Vector2(side * 245, row * 298), tint);
                BranchLine("Branch" + slot + "End", new Vector2(side * 245, row * 298), new Vector2(side * 475, row * 298), tint);
                var button = Button("Category" + slot, slotRoot, CategoryName(slot), new Vector2(side * 320, row * 410),
                    new Vector2(380, 220), 64, out var label);
                var tagPaper = button.transform.Find("Paper").GetComponent<Image>();
                InkUiStyle.ConfigureHanjiSurface(tagPaper, available ? ActionButtonRole.Primary : ActionButtonRole.Secondary);
                label.rectTransform.anchoredPosition = new Vector2(-14, 26);
                label.rectTransform.sizeDelta = new Vector2(300, 90);
                label.color = available ? InkPalette.Ink : InkPalette.TextMuted;
                if (label.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
                button.interactable = available;
                button.onClick.AddListener(() => SelectSlot(slot));
                var note = Label("CategoryCaption", button.transform, captions[i], 36, new Vector2(-14, -48), new Vector2(300, 62), false);
                note.color = InkPalette.TextMuted;
                if (available)
                {
                    // 작은 붉은 획과 방향표로 이름표 전체를 누를 수 있음을 보여 준다.
                    var mark = SpriteImage("RedBrushMark", button.transform, InkUiTextureFactory.CreateBrushSprite(),
                        new Vector2(-154, 0), new Vector2(7, 86));
                    mark.color = InkPalette.Red; mark.preserveAspect = false;
                    for (int half = -1; half <= 1; half += 2)
                    {
                        var arrow = SpriteImage("OpenArrow" + half, button.transform, null,
                            new Vector2(148, half * 9), new Vector2(5, 27));
                        arrow.color = InkPalette.Ink;
                        arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, half * 45);
                    }
                }
            }
            homePreview = Rect("EquippedInk", slotRoot, Vector2.zero, Vector2.one * 500);
            homeBody = SpriteImage("OriginalInkBody", homePreview, previewBodySprite, Vector2.zero, Vector2.one * 500);
            homeAccessory = SpriteImage("Accessory", homePreview, null, Vector2.zero, Vector2.one);
            var captionWash = SpriteImage("CaptionPaper", slotRoot, InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0, -270), new Vector2(350, 82));
            captionWash.preserveAspect = false; captionWash.color = InkPalette.Paper;
            var current = Label("EquippedCaption", slotRoot, "현재 모습", 36, new Vector2(0, -270), new Vector2(330, 64), false);
            current.color = InkPalette.TextMuted;
            var hint = Label("CategoryHint", slotRoot, "꾸밀 곳을 골라 주세요", 46, new Vector2(0, -655), new Vector2(900, 100), false);
            hint.color = InkPalette.Ink;
            var footLine = SpriteImage("FooterBrush", slotRoot, InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0, -730), new Vector2(210, 5));
            footLine.preserveAspect = false; footLine.color = new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, .22f);
        }

        void BuildWardrobeInkAccents()
        {
            foreach (var heading in new[] { paper.Find("Title"), categoryTitle.transform })
            {
                var underline = SpriteImage("TitleBrush", heading, InkUiTextureFactory.CreateBrushSprite(),
                    new Vector2(0, -68), new Vector2(220, 6));
                underline.preserveAspect = false; underline.color = InkPalette.Ink;
            }
            var titleSeal = Rect("InkSignature", paper.Find("Title"), new Vector2(300, 0), new Vector2(68, 78));
            titleSeal.localRotation = Quaternion.Euler(0, 0, -5);
            InkHudSurface.Ensure(titleSeal, true);
            var signature = Label("Mark", titleSeal, "墨", 52, Vector2.zero, new Vector2(62, 72), true);
            InkLocalizedText.Exclude(signature); signature.color = InkPalette.Paper;
            if (signature.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
            backButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 168);
            backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-420, 872);
            // 해금 조건 위의 큰 자물쇠를 작은 낙관으로 옮겨 조건 문장을 읽기 쉽게 한다.
            var lockMark = detailLockOverlay.transform.Find("LockWatermark").GetComponent<Image>();
            lockMark.rectTransform.anchoredPosition = new Vector2(400, 190);
            lockMark.rectTransform.sizeDelta = Vector2.one * 58;
            lockMark.color = InkPalette.Paper;
            var seal = Rect("LockedSeal", detailLockOverlay.transform, new Vector2(400, 190), new Vector2(78, 78));
            seal.localRotation = Quaternion.Euler(0, 0, -5); InkHudSurface.Ensure(seal, true);
            lockMark.transform.SetAsLastSibling();
            var lockCaption = Label("RequirementCaption", detailLockOverlay.transform, "해금 조건", 34,
                new Vector2(-260, 192), new Vector2(330, 62), false);
            lockCaption.color = InkPalette.Paper;
        }

        void BranchLine(string name, Vector2 from, Vector2 to, Color tint)
        {
            var line = SpriteImage(name, slotRoot, InkUiTextureFactory.CreateBrushSprite(), (from + to) * .5f,
                new Vector2(Vector2.Distance(from, to) + 3, 9));
            line.preserveAspect = false;
            line.color = tint;
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }

        void ShowCategoryHome()
        {
            StopSkillDetails(); StopCollection(); presentation.Cancel(); swipe.Cancel();
            categoryHome = true; collectionExpanded = false;
            hasPreview = hasProgress = progressAnimating = false;
            InkLocalizedText.SetSource(statusText, "");
            ApplyCollectionLayout(); RefreshSelection();
        }

        void RefreshCategoryHome()
        {
            // 메뉴 복귀는 미리보기를 버릴 뿐 저장하지 않는다. 항상 실제 장착한 먹을 보여 준다.
            selectedSlot = InkCosmeticSlot.Skin;
            selected = InkSkinPreferences.Equipped;
            selectedGold = InkPremiumAppearance.IsEquipped;
            selectedNest = InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin) == InkCosmeticCatalog.MagpieNest;
            foreach (var graphic in new Graphic[] { nameText, loreText, descriptionText, achievementText, progressText,
                detailPaper, detailDivider, detailLockOverlay, skillSummaryText }) graphic.gameObject.SetActive(false);
            skillButton.gameObject.SetActive(false);
            progressTrack.gameObject.SetActive(false); completionSeal.gameObject.SetActive(false);
            equipButton.gameObject.SetActive(false);
            SetPreview(homeBody, homeAccessory, selected, 500, false, true, selectedGold, selectedNest);
            Rect bounds = selectedNest ? new Rect(-.46f, -.45f, .92f, 1.24f) :
                LobbySkinPreview.CostumeBounds(selected, InkSkinCatalog.Sprite(selected));
            float scale = Mathf.Min(1f, Mathf.Min(410 / (bounds.width * 500), 450 / (bounds.height * 500)));
            homePreview.localScale = Vector3.one * scale;
            homePreview.anchoredPosition = -bounds.center * (500 * scale);
        }
    }
}
