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
        readonly Button[] neighborButtons = new Button[2];
        InkShowroomPresentation presentation;
        InkShowroomSwipe swipe;
        Text nameText, abilityText, descriptionText, equipText, statusText, achievementText, progressText, pageText;
        RectTransform progressFill;
        Button equipButton, backButton, previousButton, nextButton;
        readonly GameObject[] cards = new GameObject[InkSkinCatalog.Count];
        readonly Image[] cardPapers = new Image[InkSkinCatalog.Count], cardBodies = new Image[InkSkinCatalog.Count], cardAccessories = new Image[InkSkinCatalog.Count];
        readonly Text[] cardStates = new Text[InkSkinCatalog.Count];
        readonly RectTransform[] cardPreviews = new RectTransform[InkSkinCatalog.Count];
        readonly RectTransform[] cardViewports = new RectTransform[InkSkinCatalog.Count];
        InkSkinId selected;
        InkSkinId renderedSkin;
        int page;
        float refreshAt;
        bool visible, interactive;
        bool hasPreview, renderedHidden;
        const int PageSize = 6;
        const float HeroSize = 500;
        public bool IsOpen => visible && interactive;
        public InkSkinId Selected => selected;
        public RectTransform Paper => paper;
        public Button EquipButton => equipButton;
        public Button BackButton => backButton;
        public int Page => page;
        public InkShowroomPresentation Presentation => presentation;
        public bool CanBrowse => CanInteract();

        public void SetNavigationPresentation(bool show, bool allowInput)
        {
            if (show) EnsureBuilt();
            if (rootGroup == null) return;
            if (!show || !allowInput) { presentation?.Cancel(); swipe?.Cancel(); }
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
            presentation?.Cancel(); swipe?.Cancel();
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
            int step = (int)id - (int)selected;
            selected = id; page = (int)id / PageSize;
            SkinSecrets.SelectedByUser(id);
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
            if (step != 0) presentation.Browse(step);
        }
        public void StepSelection(int direction)
        {
            if (!CanInteract() || direction == 0) return;
            int next = Mathf.Clamp((int)selected + (direction > 0 ? 1 : -1), 0, InkSkinCatalog.Count - 1);
            if (next != (int)selected) Select((InkSkinId)next);
        }
        public void ChangePage(int direction)
        {
            if (!CanInteract()) return;
            int previous = (int)selected;
            page = Mathf.Clamp(page + direction, 0, (InkSkinCatalog.Count-1)/PageSize);
            selected = (InkSkinId)(page * PageSize);
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
            if ((int)selected != previous) presentation.Browse((int)selected - previous);
        }
        bool CanInteract() => IsOpen && MobileApplicationLifecycle.IsApplicationActive &&
            (GameManager.Instance == null || GameManager.Instance.State == GameState.Lobby) &&
            (LobbyScreenNavigator.Instance == null || !LobbyScreenNavigator.Instance.IsTransitioning) &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync) &&
            (!Application.isPlaying || paper.GetComponent<HanjiScrollFrame>().IsReady);
        void Equip()
        {
            if (!CanInteract() || !InkSkinAchievements.IsUnlocked(selected) || InkSkinPreferences.Equipped == selected) return;
            bool saved = InkSkinPreferences.TryEquip(selected);
            InkLocalizedText.SetSource(statusText, saved ? "" : "저장하지 못했어요. 다시 시도해 주세요");
            RefreshSelection();
            if (saved) presentation.Wear();
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
            // 주기적인 상태 갱신이 의상 교체 애니메이션의 위치를 덮어쓰지 않는다.
            if (!hasPreview || renderedSkin != selected || renderedHidden != hidden)
            {
                // 계정 교체 직후에는 이전 계정에서만 발견한 장식을 잔상으로도 노출하지 않는다.
                presentation.PrepareCostumeChange(hasPreview && !InkSkinCatalog.IsConcealed(renderedSkin));
                SetPreview(previewBody, previewAccessory, selected, HeroSize, hidden);
                presentation.RememberCostumePose();
                renderedSkin = selected; renderedHidden = hidden; hasPreview = true;
            }
            for (int side = 0; side < 2; side++)
            {
                int index = (int)selected + (side == 0 ? -1 : 1);
                bool exists = index >= 0 && index < InkSkinCatalog.Count;
                neighborButtons[side].interactable = exists;
            }
            InkLocalizedText.SetSource(equipText, !unlocked ? "업적 달성 필요" : equipped ? "착용 중" : "착용하기");
            equipButton.interactable = unlocked && !equipped;
            InkLocalizedText.SetSource(achievementText, hidden ? "" : InkSkinAchievements.Requirement(selected));
            achievementText.color = unlocked ? InkPalette.TextMuted : InkPalette.Red;
            int goal = hidden ? 0 : InkSkinAchievements.Goal(selected), progress = InkSkinAchievements.Progress(selected);
            progressText.text = goal == 0 ? "" : $"{progress} / {goal}";
            // 저장 오류는 진행도 자리에 표시해 버튼·두루마리 테두리와 겹치지 않는다.
            bool saveFailed = !string.IsNullOrEmpty(statusText.text);
            achievementText.enabled = progressText.enabled = !saveFailed;
            progressFill.parent.gameObject.SetActive(goal > 0 && !saveFailed);
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
                cardPapers[i].color = id == selected ? InkPalette.Paper : new Color(.86f, .82f, .73f, .62f);
                cardStates[i].color = id == selected ? InkPalette.Red : InkPalette.TextMuted;
                SetPreview(cardBodies[i], cardAccessories[i], id, 80, concealed);
                InkLocalizedText.SetSource(cardStates[i], !InkSkinAchievements.IsUnlocked(id) ? "잠김" :
                    id == InkSkinPreferences.Equipped ? "착용 중" : id == selected ? "선택됨" : "해금됨");
            }
            FitCardPreviews();
        }
        void FitCardPreviews()
        {
            // 같은 페이지는 먹 크기와 발끝 기준선을 공유한다. 가장 큰 장식까지 포함해 칸 안으로 맞춘다.
            Vector2 min=Vector2.one*-40f, max=-min;
            int first=page*PageSize, end=Mathf.Min(first+PageSize,cards.Length);
            for(int i=first;i<end;i++)
            {
                if(!cardAccessories[i].enabled) continue;
                var rect=cardAccessories[i].rectTransform;
                min=Vector2.Min(min,rect.anchoredPosition-rect.sizeDelta*.5f);
                max=Vector2.Max(max,rect.anchoredPosition+rect.sizeDelta*.5f);
            }
            Vector2 size=max-min, available=cardViewports[first].sizeDelta-Vector2.one*8f;
            float scale=Mathf.Min(1f,Mathf.Min(available.x/size.x,available.y/size.y));
            for(int i=first;i<end;i++)
            {
                cardPreviews[i].localScale=Vector3.one*scale;
                cardPreviews[i].anchoredPosition=-(min+max)*(.5f*scale);
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
            paper=Rect("WardrobeScroll",safeRoot,Vector2.zero,new Vector2(920,1580));
            HanjiScrollFrame.Attach(paper,new Vector2(880,1540));
            Label("Title",paper,"쇼룸",72,new Vector2(0,650),new Vector2(750,105),true);
            var stage=Rect("ShowroomStage",paper,new Vector2(0,180),new Vector2(880,810));
            // 큰 장식의 교체 궤적도 제목과 두루마리 밖으로 나가지 않게 고정 무대에서 자른다.
            stage.gameObject.AddComponent<RectMask2D>().softness=new Vector2Int(10,16);
            var stageHit=stage.gameObject.AddComponent<Image>(); stageHit.color=Color.clear; stageHit.raycastTarget=true;
            swipe=stage.gameObject.AddComponent<InkShowroomSwipe>(); swipe.Owner=this;
            for (int side=0;side<2;side++) BuildNeighbor(stage,side);
            var preview=Rect("Preview",stage,Vector2.zero,Vector2.one*HeroSize);
            previewBody=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,Vector2.one*HeroSize);
            previewAccessory=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            presentation=stage.gameObject.AddComponent<InkShowroomPresentation>(); presentation.Initialize(stage,preview);
            foreach (var arrow in neighborButtons) arrow.transform.SetAsLastSibling();
            nameText=Label("SkinName",paper,"",54,new Vector2(0,-123),new Vector2(780,70),true);
            for(int i=0;i<cards.Length;i++) BuildCard((InkSkinId)i);
            previousButton=Button("PreviousPage",paper,"‹",new Vector2(-220,-358),new Vector2(160,64),36,out _);
            nextButton=Button("NextPage",paper,"›",new Vector2(220,-358),new Vector2(160,64),36,out _);
            previousButton.onClick.AddListener(()=>ChangePage(-1)); nextButton.onClick.AddListener(()=>ChangePage(1));
            pageText=Label("Page",paper,"",30,new Vector2(0,-358),new Vector2(250,50),false); InkLocalizedText.Exclude(pageText);
            abilityText=Label("Ability",paper,"",40,new Vector2(0,-423),new Vector2(780,50),true);
            descriptionText=Label("Description",paper,"",28,new Vector2(0,-490),new Vector2(780,72),false);
            achievementText=Label("Achievement",paper,"",28,new Vector2(0,-553),new Vector2(790,42),true);
            progressText=Label("AchievementProgress",paper,"",26,new Vector2(0,-594),new Vector2(780,30),false); InkLocalizedText.Exclude(progressText);
            var track=Rect("AchievementTrack",paper,new Vector2(0,-619),new Vector2(620,6));
            var ti=track.gameObject.AddComponent<Image>(); ti.color=new Color(.11f,.106f,.102f,.15f); ti.raycastTarget=false;
            progressFill=Stretch("Progress",track); var fi=progressFill.gameObject.AddComponent<Image>(); fi.color=InkPalette.Ink; fi.raycastTarget=false;
            equipButton=Button("EquipButton",paper,"착용하기",new Vector2(0,-680),new Vector2(620,110),48,out equipText); equipButton.onClick.AddListener(Equip);
            statusText=Label("SaveStatus",paper,"",28,new Vector2(0,-576),new Vector2(780,70),false); statusText.color=InkPalette.Red;
            backButton=Button("BackButton",safeRoot,"로비로",new Vector2(0,-872),new Vector2(360,110),42,out _); backButton.onClick.AddListener(Back);
            rootGroup.alpha=0; rootGroup.interactable=rootGroup.blocksRaycasts=false;
        }
        void BuildCard(InkSkinId id)
        {
            int i=(int)id, slot=i%PageSize;
            var button=Button("SkinCard"+i,paper,"",new Vector2((slot-2.5f)*136,-245),new Vector2(128,152),24,out _);
            cards[i]=button.gameObject; cardPapers[i]=button.transform.Find("Paper").GetComponent<Image>();
            var viewport=Rect("PreviewViewport",button.transform,new Vector2(0,16),new Vector2(104,90));
            viewport.gameObject.AddComponent<RectMask2D>();
            var preview=Rect("Preview",viewport,Vector2.zero,new Vector2(80,80));
            cardViewports[i]=viewport; cardPreviews[i]=preview;
            cardBodies[i]=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,new Vector2(80,80));
            cardAccessories[i]=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            cardStates[i]=Label("State",button.transform,"",24,new Vector2(0,-48),new Vector2(104,28),false);
            button.onClick.AddListener(()=>Select(id));
        }
        void BuildNeighbor(RectTransform stage, int side)
        {
            int step=side==0 ? -1 : 1;
            var button=Button(side==0 ? "PreviousCostume" : "NextCostume",stage,side==0 ? "‹" : "›",
                new Vector2(step*350,20),new Vector2(116,148),90,out _);
            button.onClick.AddListener(()=>StepSelection(step)); neighborButtons[side]=button;
        }
        void SetPreview(Image body, Image accessory, InkSkinId id, float size, bool hidden)
        {
            // 특수 의상도 원래 먹과 눈을 절대 대체하지 않는다.
            body.sprite=previewBodySprite; body.enabled=true;
            body.color=hidden ? new Color(.11f,.106f,.102f,.16f) : Color.white;
            accessory.sprite=hidden ? null : InkSkinCatalog.Sprite(id);
            accessory.enabled=accessory.sprite!=null;
            accessory.rectTransform.localScale=Vector3.one;
            accessory.rectTransform.localRotation=Quaternion.identity;
            accessory.color=Color.white;
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
            float scale = MobileUiLayout.CalculateFitScale(new Vector2(960, 1880), safe, Screen.width, Screen.height, Vector2.one * 24);
            paper.localScale = Vector3.one * scale;
            backButton.transform.localScale = Vector3.one * scale;
            backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -872 * scale);
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
