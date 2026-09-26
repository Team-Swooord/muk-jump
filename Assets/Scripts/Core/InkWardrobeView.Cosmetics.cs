using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    public sealed partial class InkWardrobeView
    {
        InkCosmeticSlot selectedSlot;
        string selectedEffect;
        RectTransform slotRoot, effectRoot, combinedClone;
        readonly Text[] effectNames = new Text[2], effectStates = new Text[2];
        readonly Image[] effectPapers = new Image[2], effectIcons = new Image[2], leaves = new Image[3];
        Image cloneBody, cloneAccessory, cloneTail, effectCloneBody;
        public InkCosmeticSlot SelectedSlot => selectedSlot;
        public string SelectedEffect => selectedEffect;
        bool IsSelectedSkinEquipped => IsSkinEquipped(selected, selectedGold, selectedNest);
        bool IsSkinEquipped(InkSkinId id, bool gold, bool nest)
        {
            if (SkinDebugSession.Active) return !gold && !nest && InkSkinPreferences.Equipped == id;
            string key = nest ? InkCosmeticCatalog.MagpieNest : gold ? InkCosmeticCatalog.Gold : InkCosmeticCatalog.Legacy(id);
            return InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin) == key;
        }
        public void SelectNest() => SelectCore(InkSkinId.Ink, true, false, true);
        public void SelectSlot(InkCosmeticSlot slot)
        {
            // 붓 갈래는 준비 중으로 보여 주되 아직 없는 선택 화면은 열지 않는다.
            if (!CanInteract() || slot == InkCosmeticSlot.Brush || !System.Enum.IsDefined(typeof(InkCosmeticSlot), slot)) return;
            StopCollection(); presentation.Cancel(); swipe.Cancel();
            categoryHome = false;
            collectionExpanded = false;
            selectedSlot = slot;
            selected = InkSkinPreferences.Equipped;
            selectedGold = InkPremiumAppearance.IsEquipped;
            selectedNest = InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin) == InkCosmeticCatalog.MagpieNest;
            selectedEffect = InkSkinPreferences.EquippedCosmetic(slot);
            hasPreview = hasProgress = false;
            InkLocalizedText.SetSource(statusText, "");
            ApplyCollectionLayout(); RefreshSelection();
            if (slot == InkCosmeticSlot.Skin) RevealSelectedCard(true);
        }
        public void SelectEffect(string id)
        {
            if (!CanInteract() || categoryHome || selectedSlot == InkCosmeticSlot.Skin || !InkCosmeticCatalog.BelongsTo(id, selectedSlot)) return;
            selectedEffect = id; hasProgress = false;
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
        }
        void EquipEffect()
        {
            if (selectedEffect == InkSkinPreferences.EquippedCosmetic(selectedSlot)) return;
            bool saved = InkSkinPreferences.TryEquipCosmetic(selectedSlot, selectedEffect);
            InkLocalizedText.SetSource(statusText, saved ? "" : "저장하지 못했어요. 다시 시도해 주세요");
            RefreshSelection();
            if (saved) presentation.Wear();
        }
        void BuildCosmeticSlots()
        {
            BuildCategoryHome();
            effectRoot = Rect("EffectChoices", paper, CompactCollectionPosition, CompactCollectionSize);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var button = Button("Choice" + i, effectRoot, "", new Vector2(i == 0 ? -220 : 220, 0), new Vector2(360, 300), 30, out _);
                effectPapers[i] = button.transform.Find("Paper").GetComponent<Image>();
                effectIcons[i] = SpriteImage("Icon", button.transform, null, new Vector2(0, 50), new Vector2(180, 140));
                effectNames[i] = Label("Name", button.transform, "", 38, new Vector2(0, -45), new Vector2(330, 70), true);
                effectStates[i] = Label("State", button.transform, "", 30, new Vector2(0, -105), new Vector2(330, 50), false);
                button.onClick.AddListener(() => SelectEffect(EffectId(index)));
                if (i == 1) effectCloneBody = SpriteImage("CloneBody", button.transform, previewBodySprite,
                    new Vector2(-20, 50), Vector2.one * 180);
            }
            var preview = previewBody.rectTransform.parent;
            for (int i = 0; i < leaves.Length; i++)
            {
                leaves[i] = SpriteImage("LandingLeaf" + i, preview, InkCosmeticCatalog.Sprite(InkCosmeticCatalog.BambooLeaf),
                    new Vector2(-130 + i * 110, -115 + (i % 2) * 35), new Vector2(90, 90));
                leaves[i].rectTransform.localRotation = Quaternion.Euler(0, 0, -30 + i * 42);
            }
            combinedClone = Rect("CombinedClonePreview", stage, new Vector2(260, -20), new Vector2(230, 230));
            cloneBody = SpriteImage("OriginalInkBody", combinedClone, previewBodySprite, Vector2.zero, Vector2.one * 230);
            cloneAccessory = SpriteImage("Accessory", combinedClone, null, Vector2.zero, Vector2.one);
            cloneTail = SpriteImage("TadpoleTail", combinedClone, InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole), Vector2.zero, Vector2.one);
            cloneTail.transform.SetAsFirstSibling();
        }
        string EffectId(int index) => index == 0 ? InkCosmeticCatalog.Default(selectedSlot) :
            selectedSlot == InkCosmeticSlot.Landing ? InkCosmeticCatalog.BambooLeaf : InkCosmeticCatalog.Tadpole;
        void RefreshSlotVisibility()
        {
            if (slotRoot == null) return;
            bool skin = selectedSlot == InkCosmeticSlot.Skin;
            slotRoot.gameObject.SetActive(categoryHome);
            paper.Find("Title").gameObject.SetActive(categoryHome);
            categoryTitle.gameObject.SetActive(!categoryHome);
            InkLocalizedText.SetSource(categoryTitle, CategoryName(selectedSlot));
            stage.gameObject.SetActive(!categoryHome && !collectionExpanded);
            effectRoot.gameObject.SetActive(!categoryHome && !skin && !collectionExpanded);
            collectionRoot.gameObject.SetActive(!categoryHome && skin);
            collectionToggleText.transform.parent.gameObject.SetActive(!categoryHome && skin);
            statusText.gameObject.SetActive(!categoryHome && !collectionExpanded);
            collectionScroll.enabled = !categoryHome && skin && CanInteract();
            swipe.enabled = !categoryHome && skin && !collectionExpanded;
            foreach (var button in neighborButtons) button.gameObject.SetActive(!categoryHome && skin);
        }
        void RefreshEffectSelection()
        {
            // 다른 계정으로 바뀌면 갈래 미리보기의 본체도 현재 권한으로 다시 결정한다.
            var currentSkin = InkSkinPreferences.Equipped;
            bool gold = InkPremiumAppearance.IsEquipped;
            bool nest = InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin) == InkCosmeticCatalog.MagpieNest;
            if (currentSkin != selected || gold != selectedGold || nest != selectedNest) hasPreview = false;
            selected = currentSkin; selectedGold = gold; selectedNest = nest;
            bool unlocked = InkSkinPreferences.CanUseCosmetic(selectedEffect);
            bool equipped = selectedEffect == InkSkinPreferences.EquippedCosmetic(selectedSlot);
            int goal = InkCosmeticCatalog.Goal(selectedEffect);
            int value = InkCosmeticCatalog.Progress(selectedEffect, PermanentGrowthProfile.AchievementSnapshot());
            InkLocalizedText.SetSource(nameText, InkCosmeticCatalog.Name(selectedEffect));
            InkLocalizedText.SetSource(loreText, selectedEffect switch
            {
                InkCosmeticCatalog.BambooLeaf => "발끝에서 댓잎이 가볍게 흩어져요\n능력 변화 없음",
                InkCosmeticCatalog.Tadpole => "작은 꼬리를 단 먹 분신\n능력 변화 없음",
                InkCosmeticCatalog.FollowBody => "분신도 본체와 같은 옷을 입어요",
                _ => "착지할 때 익숙한 먹이 번져요"
            });
            InkLocalizedText.SetSource(descriptionText, "");
            InkLocalizedText.SetSource(achievementText, selectedEffect == InkCosmeticCatalog.BambooLeaf
                ? "그린 발판에 누적 200회 착지" : selectedEffect == InkCosmeticCatalog.Tadpole
                    ? "원먹 포함 12마리 동시 생존" : "");
            progressText.text = goal > 0 ? $"{value} / {goal}" : "";
            RefreshDetailLayout(false, false, unlocked); RefreshInlineSkill(false, false);
            achievementText.gameObject.SetActive(!unlocked && goal > 0);
            progressText.gameObject.SetActive(goal > 0);
            achievementText.color = InkPalette.TextLight;
            bool failed = !string.IsNullOrEmpty(statusText.text);
            achievementText.enabled = progressText.enabled = !failed;
            RefreshProgress(goal, value, unlocked, failed);
            previewQuestion.enabled = false;
            if (!hasPreview)
            {
                presentation.PrepareCostumeChange(false);
                SetPreview(previewBody, previewAccessory, selected, HeroSize, false, true, selectedGold, selectedNest);
                presentation.RememberCostumePose(); hasPreview = true;
                renderedSkin = selected; renderedGold = selectedGold; renderedNest = selectedNest;
            }
            InkLocalizedText.SetSource(equipText, equipped ? "착용 중" : "착용하기");
            equipButton.gameObject.SetActive(unlocked);
            equipButton.interactable = unlocked && !equipped;
            for (int i = 0; i < 2; i++)
            {
                string id = EffectId(i);
                bool owns = InkSkinPreferences.CanUseCosmetic(id), chosen = id == selectedEffect;
                effectPapers[i].color = chosen ? InkPalette.Paper : new Color(.86f, .82f, .73f, .62f);
                effectIcons[i].sprite = i == 1 ? InkCosmeticCatalog.Sprite(id) :
                    selectedSlot == InkCosmeticSlot.Clone ? previewBodySprite : InkUiTextureFactory.CreateBrushSprite();
                effectIcons[i].color = i == 0 && selectedSlot == InkCosmeticSlot.Landing ? InkPalette.Ink : owns ? Color.white : Color.black;
                effectIcons[i].rectTransform.anchoredPosition = new Vector2(0, 50);
                effectIcons[i].rectTransform.sizeDelta = new Vector2(180, 140);
                if (i == 1 && selectedSlot == InkCosmeticSlot.Clone)
                {
                    var sprite = effectIcons[i].sprite;
                    float width = 180 * InkCosmeticCatalog.Width(InkCosmeticCatalog.Tadpole);
                    effectIcons[i].rectTransform.sizeDelta = sprite == null ? Vector2.zero : new Vector2(width, width * sprite.rect.height / sprite.rect.width);
                    effectIcons[i].rectTransform.anchoredPosition = new Vector2(-20, 50) + 180 * InkCosmeticCatalog.Offset(InkCosmeticCatalog.Tadpole);
                }
                effectStates[i].color = chosen ? InkPalette.Red : InkPalette.TextMuted;
                InkLocalizedText.SetSource(effectNames[i], InkCosmeticCatalog.Name(id));
                InkLocalizedText.SetSource(effectStates[i], !owns ? "잠김" : id == InkSkinPreferences.EquippedCosmetic(selectedSlot)
                    ? "착용 중" : chosen ? "선택됨" : "해금됨");
            }
            effectCloneBody.gameObject.SetActive(selectedSlot == InkCosmeticSlot.Clone);
            effectCloneBody.color = InkSkinPreferences.CanUseCosmetic(InkCosmeticCatalog.Tadpole) ? Color.white : Color.black;
            RefreshCombinedPreview();
        }
        void RefreshCombinedPreview()
        {
            if (combinedClone == null) return;
            bool hidden = selectedSlot == InkCosmeticSlot.Skin && SkinDebugSession.IsConcealed(selected);
            string landing = selectedSlot == InkCosmeticSlot.Landing ? selectedEffect : InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Landing);
            string clone = selectedSlot == InkCosmeticSlot.Clone ? selectedEffect : InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Clone);
            bool tadpole = clone == InkCosmeticCatalog.Tadpole;
            bool unlockedSkin = selectedNest ? PermanentGrowthProfile.OwnsCosmetic(InkCosmeticCatalog.MagpieNest) : SkinDebugSession.CanEquip(selected);
            foreach (var leaf in leaves)
            {
                leaf.enabled = !hidden && landing == InkCosmeticCatalog.BambooLeaf;
                leaf.color = InkSkinPreferences.CanUseCosmetic(landing) ? Color.white : Color.black;
            }
            combinedClone.gameObject.SetActive(!hidden);
            SetPreview(cloneBody, cloneAccessory, tadpole ? InkSkinId.Ink : selected, 230, hidden,
                tadpole ? InkSkinPreferences.CanUseCosmetic(clone) : unlockedSkin, !tadpole && selectedGold, !tadpole && selectedNest);
            cloneTail.enabled = tadpole && !hidden;
            cloneTail.color = cloneBody.color;
            if (cloneTail.sprite != null)
            {
                float width = 230 * InkCosmeticCatalog.Width(InkCosmeticCatalog.Tadpole);
                cloneTail.rectTransform.sizeDelta = new Vector2(width, width * cloneTail.sprite.rect.height / cloneTail.sprite.rect.width);
                cloneTail.rectTransform.anchoredPosition = 230 * InkCosmeticCatalog.Offset(InkCosmeticCatalog.Tadpole);
                cloneTail.transform.SetAsFirstSibling();
            }
        }
    }
}
