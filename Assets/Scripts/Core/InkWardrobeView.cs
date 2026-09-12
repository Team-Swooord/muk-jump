using UnityEngine;

namespace MukJump.Core
{
    /// 로비 전용 꾸미기. 선택 미리보기와 저장을 분리하고, 판중 장착 변경을 받지 않는다.
    [DisallowMultipleComponent]
    public sealed class InkWardrobeView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        CanvasGroup rootGroup;
        RectTransform safeRoot, paper;
        UnityEngine.UI.Image previewBody, previewAccessory;
        UnityEngine.UI.Text nameText, abilityText, descriptionText, equipText, statusText;
        UnityEngine.UI.Text achievementText, progressText;
        RectTransform progressFill;
        int lastGatProgress = -1, lastSpiderProgress = -1;
        UnityEngine.UI.Button equipButton, backButton;
        readonly UnityEngine.UI.Image[] cardPapers = new UnityEngine.UI.Image[InkSkinCatalog.Count];
        readonly UnityEngine.UI.Text[] cardStates = new UnityEngine.UI.Text[InkSkinCatalog.Count];
        InkSkinId selected;
        bool visible;
        bool interactive;
        public bool IsOpen => visible && interactive;
        public InkSkinId Selected => selected;
        public RectTransform Paper => paper;
        public UnityEngine.UI.Button EquipButton => equipButton;
        public UnityEngine.UI.Button BackButton => backButton;

        public void SetNavigationPresentation(bool show, bool allowInput)
        {
            if (show) EnsureBuilt();
            if (rootGroup == null) return;
            if (show && !visible)
            {
                selected = InkSkinPreferences.Equipped;
                InkLocalizedText.SetSource(statusText, string.Empty);
                RefreshSelection();
                paper.GetComponent<HanjiScrollFrame>().ResetPresentation();
            }
            visible = show;
            interactive = show && allowInput;
            rootGroup.alpha = show ? 1 : 0;
            rootGroup.interactable = interactive;
            rootGroup.blocksRaycasts = show;
            ApplyLayout();
        }

        void OnDisable()
        {
            visible = interactive = false;
            if (rootGroup != null)
            {
                rootGroup.alpha = 0;
                rootGroup.interactable = rootGroup.blocksRaycasts = false;
            }
        }

        void Update()
        {
            if (!visible) return;
            ApplyLayout();
            if (lastGatProgress != InkSkinAchievements.Progress(InkSkinId.Gat) ||
                lastSpiderProgress != InkSkinAchievements.Progress(InkSkinId.SpiderMask))
                RefreshSelection();
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby)
                SetNavigationPresentation(false, false);
        }

        public void Select(InkSkinId id)
        {
            if (!CanInteract() || !InkSkinCatalog.IsValid(id)) return;
            selected = id;
            InkLocalizedText.SetSource(statusText, string.Empty);
            RefreshSelection();
        }

        bool CanInteract() => IsOpen &&
            (GameManager.Instance == null || GameManager.Instance.State == GameState.Lobby) &&
            (LobbyScreenNavigator.Instance == null || !LobbyScreenNavigator.Instance.IsTransitioning) &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) &&
            (!Application.isPlaying || paper.GetComponent<HanjiScrollFrame>().IsReady);

        void Equip()
        {
            if (!CanInteract()) return;
            if (!InkSkinPreferences.TryEquip(selected))
                InkLocalizedText.SetSource(statusText, "저장하지 못했어요. 다시 시도해 주세요");
            else
                InkLocalizedText.SetSource(statusText, string.Empty);
            RefreshSelection();
        }

        void Back()
        {
            if (!CanInteract()) return;
            PointerInput.SuppressUntilRelease();
            var navigator = LobbyScreenNavigator.Instance != null
                ? LobbyScreenNavigator.Instance : GetComponent<LobbyScreenNavigator>();
            navigator?.ReturnToLobby();
        }

        void RefreshSelection()
        {
            InkLocalizedText.SetSource(nameText, InkSkinCatalog.Name(selected));
            InkLocalizedText.SetSource(abilityText, InkSkinCatalog.Ability(selected));
            InkLocalizedText.SetSource(descriptionText, InkSkinCatalog.Description(selected));
            SetAccessory(previewAccessory, selected, 350);
            bool unlocked = InkSkinAchievements.IsUnlocked(selected);
            bool equipped = InkSkinPreferences.Equipped == selected;
            InkLocalizedText.SetSource(equipText, !unlocked ? "업적 달성 필요" : equipped ? "착용 중" : "착용하기");
            equipButton.interactable = unlocked && !equipped;
            InkLocalizedText.SetSource(achievementText, InkSkinAchievements.Requirement(selected));
            achievementText.color = unlocked ? InkPalette.TextMuted : InkPalette.Red;
            int goal = InkSkinAchievements.Goal(selected);
            int progress = InkSkinAchievements.Progress(selected);
            progressText.text = goal == 0 ? string.Empty : $"{progress} / {goal}";
            progressFill.parent.gameObject.SetActive(goal > 0);
            progressFill.anchorMax = new Vector2(goal == 0 ? 1 : (float)progress / goal, 1);
            lastGatProgress = InkSkinAchievements.Progress(InkSkinId.Gat);
            lastSpiderProgress = InkSkinAchievements.Progress(InkSkinId.SpiderMask);
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                cardPapers[i].color = i == (int)selected ? InkPalette.Paper : Color.white;
                cardStates[i].color = i == (int)selected ? InkPalette.Red : InkPalette.TextMuted;
                InkLocalizedText.SetSource(cardStates[i], !InkSkinAchievements.IsUnlocked((InkSkinId)i) ? "잠김" :
                    i == (int)InkSkinPreferences.Equipped ? "착용 중" : i == (int)selected ? "선택됨" : "해금됨");
            }
        }

        void EnsureBuilt()
        {
            if (rootGroup != null) return;
            var root = new GameObject("InkWardrobeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4050;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            rootGroup = root.GetComponent<CanvasGroup>();
            var blocker = Stretch("LobbyBackgroundInputBlocker", root.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            blocker.color = InkUiStyle.PopupDimColor;
            blocker.raycastTarget = true;
            safeRoot = Stretch("SafeAreaRoot", root.transform);
            paper = Rect("WardrobeScroll", safeRoot, Vector2.zero, new Vector2(920, 1440));
            HanjiScrollFrame.Attach(paper, new Vector2(880, 1400));
            Label("Title", paper, "꾸미기", 72, new Vector2(0, 595), new Vector2(750, 105), true);
            var preview = Rect("Preview", paper, new Vector2(0, 280), new Vector2(350, 350));
            previewBody = SpriteImage("OriginalInkBody", preview, previewBodySprite, Vector2.zero, new Vector2(350, 350));
            previewAccessory = SpriteImage("Accessory", preview, null, Vector2.zero, Vector2.one);
            nameText = Label("SkinName", paper, string.Empty, 54, new Vector2(0, 45), new Vector2(780, 80), true);
            for (int i = 0; i < InkSkinCatalog.Count; i++) BuildCard((InkSkinId)i);
            abilityText = Label("Ability", paper, string.Empty, 48, new Vector2(0, -305), new Vector2(780, 60), true);
            descriptionText = Label("Description", paper, string.Empty, 36, new Vector2(0, -380), new Vector2(780, 90), false);
            achievementText = Label("Achievement", paper, string.Empty, 34, new Vector2(0, -454), new Vector2(780, 52), true);
            progressText = Label("AchievementProgress", paper, string.Empty, 28, new Vector2(0, -491), new Vector2(780, 36), false);
            InkLocalizedText.Exclude(progressText);
            var track = Rect("AchievementTrack", paper, new Vector2(0, -518), new Vector2(620, 6));
            var trackImage = track.gameObject.AddComponent<UnityEngine.UI.Image>();
            trackImage.color = new Color(0.11f, 0.106f, 0.102f, .15f); trackImage.raycastTarget = false;
            progressFill = Stretch("Progress", track);
            var fillImage = progressFill.gameObject.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = InkPalette.Ink; fillImage.raycastTarget = false;
            equipButton = Button("EquipButton", paper, "착용하기", new Vector2(0, -588), new Vector2(620, 110), 48, out equipText);
            equipButton.onClick.AddListener(Equip);
            statusText = Label("SaveStatus", paper, string.Empty, 30, new Vector2(0, -675), new Vector2(780, 56), false);
            statusText.color = InkPalette.Red;
            backButton = Button("BackButton", safeRoot, "로비로", new Vector2(0, -810), new Vector2(360, 110), 42, out _);
            backButton.onClick.AddListener(Back);
            rootGroup.alpha = 0;
            rootGroup.interactable = rootGroup.blocksRaycasts = false;
        }

        void BuildCard(InkSkinId id)
        {
            var button = Button("SkinCard" + (int)id, paper, string.Empty,
                new Vector2(((int)id - 1) * 270, -140), new Vector2(248, 250), 36, out _);
            cardPapers[(int)id] = button.transform.Find("Paper").GetComponent<UnityEngine.UI.Image>();
            var preview = Rect("Preview", button.transform, new Vector2(0, 37), new Vector2(128, 128));
            SpriteImage("OriginalInkBody", preview, previewBodySprite, Vector2.zero, new Vector2(128, 128));
            var accessory = SpriteImage("Accessory", preview, null, Vector2.zero, Vector2.one);
            SetAccessory(accessory, id, 128);
            Label("Name", button.transform, InkSkinCatalog.Name(id), 38, new Vector2(0, -53), new Vector2(234, 55), true);
            cardStates[(int)id] = Label("State", button.transform, string.Empty, 30, new Vector2(0, -98), new Vector2(230, 44), false);
            button.onClick.AddListener(() => Select(id));
        }

        static void SetAccessory(UnityEngine.UI.Image image, InkSkinId id, float bodySize)
        {
            string path = InkSkinCatalog.ResourcePath(id);
            image.sprite = path.Length == 0 ? null : Resources.Load<Sprite>(path);
            image.enabled = image.sprite != null;
            if (!image.enabled) return;
            float width = bodySize * InkSkinCatalog.Width(id);
            image.rectTransform.sizeDelta = new Vector2(width, width * image.sprite.rect.height / image.sprite.rect.width);
            image.rectTransform.anchoredPosition = InkSkinCatalog.Offset(id) * bodySize;
        }

        void ApplyLayout()
        {
            if (safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(safeRoot, safe, Screen.width, Screen.height);
            float scale = MobileUiLayout.CalculateFitScale(new Vector2(960, 1760), safe, Screen.width, Screen.height, Vector2.one * 24);
            paper.localScale = Vector3.one * scale;
            backButton.transform.localScale = Vector3.one * scale;
            backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -810 * scale);
        }

        static RectTransform Stretch(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        static UnityEngine.UI.Image SpriteImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
        static UnityEngine.UI.Text Label(string name, Transform parent, string source, int size, Vector2 position, Vector2 bounds, bool strong)
        {
            var text = Rect(name, parent, position, bounds).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkLocalizedText.SetSource(text, source);
            InkUiStyle.ApplyReadableText(text, size, TextAnchor.MiddleCenter, strong, true);
            text.color = InkPalette.TextDark; text.raycastTarget = false;
            return text;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, string source, Vector2 position, Vector2 size, int fontSize, out UnityEngine.UI.Text label)
        {
            var rect = Rect(name, parent, position, size);
            var hit = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            var paper = SpriteImage("Paper", rect, InkUiStyle.ActionButtonSprite, Vector2.zero, size);
            InkUiStyle.ConfigureHanjiSurface(paper);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            InkUiStyle.ConfigureButton(button, paper);
            button.colors = InkUiStyle.ActionButtonColors();
            label = Label("Label", rect, source, fontSize, Vector2.zero, size - new Vector2(32, 12), true);
            return button;
        }

#if UNITY_EDITOR
        public void BuildForTests(Sprite sprite = null) { previewBodySprite = sprite; EnsureBuilt(); }
#endif
    }
}
