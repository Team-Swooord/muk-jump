using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        bool categoryHome = true;
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
            slotRoot = Rect("CosmeticCategories", paper, new Vector2(0, 60), new Vector2(1000, 1100));
            var slots = new[] { InkCosmeticSlot.Skin, InkCosmeticSlot.Landing, InkCosmeticSlot.Brush, InkCosmeticSlot.Clone };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                float side = i % 2 == 0 ? -1 : 1, row = i < 2 ? 1 : -1;
                bool available = slot != InkCosmeticSlot.Brush;
                var tint = InkPalette.Ink; tint.a = available ? .8f : .3f;
                // 먹에서 네 모서리로 뻗는 단순한 꺾인 획. 장식은 터치를 가로채지 않는다.
                BranchLine("Branch" + slot + "Stem", new Vector2(side * 165, row * 120), new Vector2(side * 240, row * 290), tint);
                BranchLine("Branch" + slot + "End", new Vector2(side * 240, row * 290), new Vector2(side * 475, row * 290), tint);
                var button = Button("Category" + slot, slotRoot, CategoryName(slot), new Vector2(side * 330, row * 370),
                    new Vector2(300, 156), 58, out var label);
                button.transform.Find("Paper").gameObject.SetActive(false);
                InkUiStyle.ConfigureButton(button, label);
                InkUiStyle.ApplyReadableText(label, 58, TextAnchor.MiddleCenter, true, true);
                label.color = available ? InkPalette.Ink : InkPalette.TextMuted;
                if (label.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
                button.interactable = available;
                button.onClick.AddListener(() => SelectSlot(slot));
                if (!available)
                {
                    var note = Label("ComingSoon", button.transform, "준비 중", 32, new Vector2(0, -72), new Vector2(280, 60), false);
                    note.color = InkPalette.TextMuted;
                }
            }
            homePreview = Rect("EquippedInk", slotRoot, Vector2.zero, Vector2.one * 500);
            homeBody = SpriteImage("OriginalInkBody", homePreview, previewBodySprite, Vector2.zero, Vector2.one * 500);
            homeAccessory = SpriteImage("Accessory", homePreview, null, Vector2.zero, Vector2.one);
            var hint = Label("CategoryHint", slotRoot, "꾸밀 곳을 골라 주세요", 42, new Vector2(0, -660), new Vector2(900, 100), false);
            hint.color = InkPalette.Ink;
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
