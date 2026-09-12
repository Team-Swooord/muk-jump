using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 6칸 페이지로 의상을 탐색한다. 비밀 보상은 이름·원화·조건·진행도 모두 감춘다.
    [DisallowMultipleComponent]
    public sealed class InkWardrobeView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        CanvasGroup rootGroup;
        RectTransform safeRoot, paper;
        Image previewBody, previewAccessory;
        Text nameText, abilityText, descriptionText, equipText, statusText, achievementText, progressText, pageText;
        RectTransform progressFill;
        Button equipButton, backButton, previousButton, nextButton;
        readonly GameObject[] cards = new GameObject[InkSkinCatalog.Count];
        readonly Image[] cardPapers = new Image[InkSkinCatalog.Count], cardBodies = new Image[InkSkinCatalog.Count], cardAccessories = new Image[InkSkinCatalog.Count];
        readonly Text[] cardStates = new Text[InkSkinCatalog.Count], cardNames = new Text[InkSkinCatalog.Count];
        InkSkinId selected;
        int page;
        float refreshAt;
        bool visible, interactive;
        const int PageSize = 6;
        public bool IsOpen => visible && interactive;
        public InkSkinId Selected => selected;
        public RectTransform Paper => paper;
        public Button EquipButton => equipButton;
        public Button BackButton => backButton;
        public int Page => page;

        public void SetNavigationPresentation(bool show, bool allowInput)
        {
            if (show) EnsureBuilt();
            if (rootGroup == null) return;
            if (show && !visible)
            {
                selected = InkSkinPreferences.Equipped;
                page = (int)selected / PageSize;
                InkLocalizedText.SetSource(statusText, "");
                RefreshSelection();
                paper.GetComponent<HanjiScrollFrame>().ResetPresentation();
            }
            visible = show; interactive = show && allowInput;
            rootGroup.alpha = show ? 1 : 0;
            rootGroup.interactable = interactive; rootGroup.blocksRaycasts = show;
            ApplyLayout();
        }
        void OnDisable()
        {
            visible = interactive = false;
            if (rootGroup == null) return;
            rootGroup.alpha = 0; rootGroup.interactable = rootGroup.blocksRaycasts = false;
        }
        void Update()
        {
            if (!visible) return;
            ApplyLayout();
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .25f; RefreshSelection(); }
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby) SetNavigationPresentation(false,false);
        }
        public void Select(InkSkinId id)
        {
            if (!CanInteract() || !InkSkinCatalog.IsValid(id)) return;
            selected = id; page = (int)id / PageSize;
            SkinSecrets.SelectedByUser(id);
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
        }
        public void ChangePage(int direction)
        {
            if (!CanInteract()) return;
            page = Mathf.Clamp(page + direction, 0, (InkSkinCatalog.Count-1)/PageSize);
            selected = (InkSkinId)(page * PageSize);
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
            InkLocalizedText.SetSource(statusText, InkSkinPreferences.TryEquip(selected) ? "" : "저장하지 못했어요. 다시 시도해 주세요");
            RefreshSelection();
        }
        void Back()
        {
            if (!CanInteract()) return;
            PointerInput.SuppressUntilRelease();
            (LobbyScreenNavigator.Instance != null ? LobbyScreenNavigator.Instance : GetComponent<LobbyScreenNavigator>())?.ReturnToLobby();
        }
        void RefreshSelection()
        {
            bool hidden = InkSkinCatalog.IsConcealed(selected);
            bool unlocked = InkSkinAchievements.IsUnlocked(selected);
            bool equipped = InkSkinPreferences.Equipped == selected;
            InkLocalizedText.SetSource(nameText, hidden ? "???" : InkSkinCatalog.Name(selected));
            InkLocalizedText.SetSource(abilityText, hidden ? "비밀 의상" : InkSkinCatalog.Ability(selected));
            InkLocalizedText.SetSource(descriptionText, hidden ? "숨겨진 행동으로 발견해 보세요." : InkSkinCatalog.Description(selected));
            SetPreview(previewBody, previewAccessory, selected, 220, hidden);
            InkLocalizedText.SetSource(equipText, !unlocked ? "업적 달성 필요" : equipped ? "착용 중" : "착용하기");
            equipButton.interactable = unlocked && !equipped;
            InkLocalizedText.SetSource(achievementText, hidden ? "" : InkSkinAchievements.Requirement(selected));
            achievementText.color = unlocked ? InkPalette.TextMuted : InkPalette.Red;
            int goal = hidden ? 0 : InkSkinAchievements.Goal(selected), progress = InkSkinAchievements.Progress(selected);
            progressText.text = goal == 0 ? "" : $"{progress} / {goal}";
            progressFill.parent.gameObject.SetActive(goal > 0);
            progressFill.anchorMax = new Vector2(goal == 0 ? 1 : (float)progress / goal, 1);
            pageText.text = $"{page+1} / {(InkSkinCatalog.Count+PageSize-1)/PageSize}";
            previousButton.interactable = page > 0;
            nextButton.interactable = (page+1)*PageSize < InkSkinCatalog.Count;
            for (int i=0; i<cards.Length; i++)
            {
                bool active = i / PageSize == page;
                cards[i].SetActive(active);
                if (!active) continue;
                var id = (InkSkinId)i;
                bool concealed = InkSkinCatalog.IsConcealed(id);
                cardPapers[i].color = id == selected ? InkPalette.Paper : Color.white;
                cardStates[i].color = id == selected ? InkPalette.Red : InkPalette.TextMuted;
                SetPreview(cardBodies[i], cardAccessories[i], id, 90, concealed);
                InkLocalizedText.SetSource(cardNames[i], concealed ? "???" : InkSkinCatalog.Name(id));
                InkLocalizedText.SetSource(cardStates[i], !InkSkinAchievements.IsUnlocked(id) ? "잠김" :
                    id == InkSkinPreferences.Equipped ? "착용 중" : id == selected ? "선택됨" : "해금됨");
            }
        }
        void EnsureBuilt()
        {
            if (rootGroup != null) return;
            var root = new GameObject("InkWardrobeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform,false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=4050;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            rootGroup = root.GetComponent<CanvasGroup>();
            var blocker = Stretch("LobbyBackgroundInputBlocker",root.transform).gameObject.AddComponent<Image>();
            blocker.color=InkUiStyle.PopupDimColor; blocker.raycastTarget=true;
            safeRoot=Stretch("SafeAreaRoot",root.transform);
            paper=Rect("WardrobeScroll",safeRoot,Vector2.zero,new Vector2(920,1440));
            HanjiScrollFrame.Attach(paper,new Vector2(880,1400));
            Label("Title",paper,"꾸미기",72,new Vector2(0,595),new Vector2(750,105),true);
            var preview=Rect("Preview",paper,new Vector2(0,410),new Vector2(220,220));
            previewBody=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,new Vector2(220,220));
            previewAccessory=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            nameText=Label("SkinName",paper,"",48,new Vector2(0,257),new Vector2(780,75),true);
            for(int i=0;i<cards.Length;i++) BuildCard((InkSkinId)i);
            previousButton=Button("PreviousPage",paper,"‹",new Vector2(-220,-269),new Vector2(160,80),50,out _);
            nextButton=Button("NextPage",paper,"›",new Vector2(220,-269),new Vector2(160,80),50,out _);
            previousButton.onClick.AddListener(()=>ChangePage(-1)); nextButton.onClick.AddListener(()=>ChangePage(1));
            pageText=Label("Page",paper,"",30,new Vector2(0,-269),new Vector2(250,50),false); InkLocalizedText.Exclude(pageText);
            abilityText=Label("Ability",paper,"",44,new Vector2(0,-340),new Vector2(780,60),true);
            descriptionText=Label("Description",paper,"",32,new Vector2(0,-400),new Vector2(780,90),false);
            achievementText=Label("Achievement",paper,"",30,new Vector2(0,-467),new Vector2(790,52),true);
            progressText=Label("AchievementProgress",paper,"",27,new Vector2(0,-506),new Vector2(780,34),false); InkLocalizedText.Exclude(progressText);
            var track=Rect("AchievementTrack",paper,new Vector2(0,-532),new Vector2(620,6));
            var ti=track.gameObject.AddComponent<Image>(); ti.color=new Color(.11f,.106f,.102f,.15f); ti.raycastTarget=false;
            progressFill=Stretch("Progress",track); var fi=progressFill.gameObject.AddComponent<Image>(); fi.color=InkPalette.Ink; fi.raycastTarget=false;
            equipButton=Button("EquipButton",paper,"착용하기",new Vector2(0,-599),new Vector2(620,110),48,out equipText); equipButton.onClick.AddListener(Equip);
            statusText=Label("SaveStatus",paper,"",30,new Vector2(0,-680),new Vector2(780,50),false); statusText.color=InkPalette.Red;
            backButton=Button("BackButton",safeRoot,"로비로",new Vector2(0,-810),new Vector2(360,110),42,out _); backButton.onClick.AddListener(Back);
            rootGroup.alpha=0; rootGroup.interactable=rootGroup.blocksRaycasts=false;
        }
        void BuildCard(InkSkinId id)
        {
            int i=(int)id, slot=i%PageSize;
            var button=Button("SkinCard"+i,paper,"",new Vector2((slot%3-1)*270,100-(slot/3)*213),new Vector2(248,198),36,out _);
            cards[i]=button.gameObject; cardPapers[i]=button.transform.Find("Paper").GetComponent<Image>();
            var preview=Rect("Preview",button.transform,new Vector2(0,38),new Vector2(90,90));
            cardBodies[i]=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,new Vector2(90,90));
            cardAccessories[i]=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            cardNames[i]=Label("Name",button.transform,"",30,new Vector2(0,-28),new Vector2(234,52),true);
            cardStates[i]=Label("State",button.transform,"",27,new Vector2(0,-72),new Vector2(230,40),false);
            button.onClick.AddListener(()=>Select(id));
        }
        void SetPreview(Image body, Image accessory, InkSkinId id, float size, bool hidden)
        {
            // 특수 의상도 원래 먹과 눈을 절대 대체하지 않는다.
            body.sprite=previewBodySprite; body.enabled=true;
            body.color=hidden ? new Color(.11f,.106f,.102f,.16f) : Color.white;
            accessory.sprite=hidden ? null : InkSkinCatalog.Sprite(id);
            accessory.enabled=accessory.sprite!=null;
            if (InkSkinCatalog.BehindBody(id)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            if (!accessory.enabled) { body.enabled=true; return; }
            float width=size*InkSkinCatalog.Width(id);
            accessory.rectTransform.sizeDelta=new Vector2(width,width*accessory.sprite.rect.height/accessory.sprite.rect.width);
            accessory.rectTransform.anchoredPosition=InkSkinCatalog.Offset(id)*size;
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
