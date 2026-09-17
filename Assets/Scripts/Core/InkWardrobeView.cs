using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 넓은 수집 목록을 스크롤하거나 전체 펼쳐 본다. 비밀 보상은 발견 전까지 감춘다.
    [DisallowMultipleComponent]
    public sealed partial class InkWardrobeView : MonoBehaviour
    {
        [SerializeField] Sprite previewBodySprite;
        CanvasGroup rootGroup;
        RectTransform safeRoot, paper;
        Image previewBody, previewAccessory, detailPaper, detailDivider;
        float accessoryMotionTime;
        UnityEngine.UI.Text previewQuestion;
        readonly Button[] neighborButtons = new Button[2];
        InkShowroomPresentation presentation;
        InkShowroomSwipe swipe;
        Text nameText, loreText, descriptionText, equipText, statusText, achievementText, progressText, collectionToggleText;
        InkWardrobeScroll collectionScroll;
        UnityEngine.UI.GridLayoutGroup collectionGrid;
        RectTransform collectionContent, collectionRoot, stage;
        bool collectionExpanded;
        UnityEngine.UI.Image progressFill;
        RectTransform progressTrack, completionSeal;
        InkSkinId progressSkin;
        float progressTarget, progressStart, progressStartedAt;
        uint progressInactiveGeneration;
        bool hasProgress, progressAnimating, progressComplete;
        const float ProgressDuration = .36f;
        Button equipButton, backButton;
        readonly GameObject[] cards = new GameObject[InkSkinCatalog.Count];
        readonly Image[] cardPapers = new Image[InkSkinCatalog.Count], cardBodies = new Image[InkSkinCatalog.Count], cardAccessories = new Image[InkSkinCatalog.Count];
        readonly Text[] cardStates = new Text[InkSkinCatalog.Count], cardNames = new Text[InkSkinCatalog.Count];
        readonly Text[] cardBestHeights = new Text[InkSkinCatalog.Count];
        readonly RectTransform[] cardSkillMarks = new RectTransform[InkSkinCatalog.Count];
        readonly UnityEngine.UI.Text[] cardQuestions = new UnityEngine.UI.Text[InkSkinCatalog.Count];
        readonly RectTransform[] cardPreviews = new RectTransform[InkSkinCatalog.Count];
        readonly RectTransform[] cardViewports = new RectTransform[InkSkinCatalog.Count];
        InkSkinId selected;
        InkSkinId renderedSkin;
        int page;
        float refreshAt;
        bool visible, interactive;
        bool hasPreview, renderedHidden, renderedUnlocked;
        const int PageSize = 6;
        const float HeroSize = 500;
        static readonly Vector2 CompactCollectionPosition = new(0, -587f);
        static readonly Vector2 CompactCollectionSize = new(1000f, 340f);
        public bool IsOpen => visible && interactive;
        public InkSkinId Selected => selected;
        public RectTransform Paper => paper;
        public Button EquipButton => equipButton;
        public Button BackButton => backButton;
        public int Page => page;
        public RectTransform CollectionContent => collectionContent;
        public UnityEngine.UI.ScrollRect CollectionScroll => collectionScroll;
        public bool CollectionExpanded => collectionExpanded;
        public InkShowroomPresentation Presentation => presentation;
        public bool CanBrowse => CanInteract();

        public void SetNavigationPresentation(bool show, bool allowInput)
        {
            if (show) EnsureBuilt();
            if (rootGroup == null) return;
            if (!show || !allowInput) { StopSkillDetails(); presentation?.Cancel(); swipe?.Cancel(); SnapProgress(); StopCollection(); }
            if (show && !visible)
            {
                selected = InkSkinPreferences.Equipped;
                page = InkSkinCatalog.BrowseIndex(selected) / PageSize;
                collectionExpanded = false;
                ApplyCollectionLayout();
                InkLocalizedText.SetSource(statusText, "");
                RefreshSelection();
            }
            visible = show; interactive = show && allowInput;
            rootGroup.alpha = show ? 1 : 0;
            rootGroup.interactable = interactive; rootGroup.blocksRaycasts = show;
            collectionScroll.enabled = interactive && MobileApplicationLifecycle.IsApplicationActive;
            ApplyLayout();
            if (show) RevealSelectedCard(true);
        }
        void OnDisable()
        {
            visible = interactive = false;
            StopSkillDetails();
            presentation?.Cancel(); swipe?.Cancel();
            StopCollection();
            SnapProgress();
            if (rootGroup == null) return;
            rootGroup.alpha = 0; rootGroup.interactable = rootGroup.blocksRaycasts = false;
        }
        void Update()
        {
            if (!visible) return;
            ApplyLayout();
            bool canScroll = CanInteract();
            if (!canScroll) StopCollection();
            else if (!collectionScroll.enabled) collectionScroll.enabled = true;
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .25f; RefreshSelection(); }
            UpdateProgress();
            if (canScroll && (GameManager.Instance == null || !GameManager.Instance.IsPaused))
                accessoryMotionTime += Mathf.Min(.05f, Time.unscaledDeltaTime);
            (previewAccessory as InkAccessoryImage)?.PresentMotion(selected, accessoryMotionTime,
                SkinDebugSession.CanEquip(selected) ? InkAccessoryMotion.Strength : 0f);
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Lobby) SetNavigationPresentation(false,false);
        }
        public void Select(InkSkinId id)
            => SelectCore(id, true);
        internal void SelectFromCarousel(int index)
        {
            var id = InkSkinCatalog.BrowseAt(index);
            if (id != selected) SelectCore(id, false);
        }
        void SelectCore(InkSkinId id, bool reveal)
        {
            if (!CanInteract() || collectionScroll.IsDragging || !InkSkinCatalog.IsAvailable(id)) return;
            int step = InkSkinCatalog.BrowseIndex(id) - InkSkinCatalog.BrowseIndex(selected);
            selected = id; page = InkSkinCatalog.BrowseIndex(id) / PageSize;
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
            if (reveal) RevealSelectedCard();
            if (step != 0) presentation.Browse(step);
        }
        public void StepSelection(int direction)
        {
            if (!CanInteract() || direction == 0) return;
            int next = Mathf.Clamp(InkSkinCatalog.BrowseIndex(selected) + (direction > 0 ? 1 : -1), 0, InkSkinCatalog.BrowseCount - 1);
            if (InkSkinCatalog.BrowseAt(next) != selected) Select(InkSkinCatalog.BrowseAt(next));
        }
        public void ChangePage(int direction)
        {
            if (!CanInteract()) return;
            int previous = InkSkinCatalog.BrowseIndex(selected);
            page = Mathf.Clamp(page + direction, 0, (InkSkinCatalog.BrowseCount-1)/PageSize);
            selected = InkSkinCatalog.BrowseAt(page * PageSize);
            InkLocalizedText.SetSource(statusText, "");
            RefreshSelection();
            RevealSelectedCard();
            if (InkSkinCatalog.BrowseIndex(selected) != previous) presentation.Browse(InkSkinCatalog.BrowseIndex(selected) - previous);
        }
        bool CanInteract() => CanUseWardrobe();
        bool CanUseWardrobe() => IsOpen && MobileApplicationLifecycle.IsApplicationActive &&
            (GameManager.Instance == null || GameManager.Instance.State == GameState.Lobby) &&
            (LobbyScreenNavigator.Instance == null || !LobbyScreenNavigator.Instance.IsTransitioning) &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync);
        void Equip()
        {
            if (!CanInteract() || !SkinDebugSession.CanEquip(selected) || InkSkinPreferences.Equipped == selected) return;
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
            bool hidden = SkinDebugSession.IsConcealed(selected);
            bool hasAbility = InkSkinCatalog.HasAbility(selected);
            bool unlocked = SkinDebugSession.CanEquip(selected);
            bool equipped = InkSkinPreferences.Equipped == selected;
            InkLocalizedText.SetSource(nameText, hidden ? "" : InkSkinCatalog.Name(selected));
            InkLocalizedText.SetSource(loreText, hidden ? "" : InkSkinLore.Get(selected).ko);
            InkLocalizedText.SetSource(descriptionText, hidden ? "숨겨진 행동으로 발견해 보세요." : "");
            RefreshDetailLayout(hidden, hasAbility);
            RefreshInlineSkill(hidden, hasAbility);
            previewQuestion.enabled = hidden;
            // 주기적인 상태 갱신이 의상 교체 애니메이션의 위치를 덮어쓰지 않는다.
            if (!hasPreview || renderedSkin != selected || renderedHidden != hidden || renderedUnlocked != unlocked)
            {
                // 히든 화면과 계정 교체 직후에는 이전 계정의 원화·색을 잔상으로도 노출하지 않는다.
                presentation.PrepareCostumeChange(hasPreview && !hidden && !SkinDebugSession.IsConcealed(renderedSkin) &&
                    renderedUnlocked == SkinDebugSession.CanEquip(renderedSkin));
                SetPreview(previewBody, previewAccessory, selected, HeroSize, hidden, unlocked);
                presentation.RememberCostumePose();
                renderedSkin = selected; renderedHidden = hidden; renderedUnlocked = unlocked; hasPreview = true;
            }
            for (int side = 0; side < 2; side++)
            {
                int index = InkSkinCatalog.BrowseIndex(selected) + (side == 0 ? -1 : 1);
                bool exists = index >= 0 && index < InkSkinCatalog.BrowseCount;
                neighborButtons[side].interactable = exists;
            }
            InkLocalizedText.SetSource(equipText, !unlocked ? "업적 달성 필요" : equipped ? "착용 중" : "착용하기");
            equipButton.gameObject.SetActive(!hidden);
            equipButton.interactable = unlocked && !equipped;
            InkLocalizedText.SetSource(achievementText, hidden || selected == InkSkinId.Ink ? "" : InkSkinAchievements.Requirement(selected));
            achievementText.color = unlocked ? InkPalette.TextMuted : InkPalette.Red;
            int goal = hidden ? 0 : InkSkinAchievements.Goal(selected), progress = InkSkinAchievements.Progress(selected);
            if (SkinDebugSession.Active) progress = goal;
            progressText.text = goal == 0 ? "" : $"{progress} / {goal}";
            // 저장 오류는 진행도 자리에 표시해 하단 착용 버튼과 겹치지 않는다.
            bool saveFailed = !string.IsNullOrEmpty(statusText.text);
            achievementText.enabled = progressText.enabled = !saveFailed;
            RefreshProgress(goal, progress, unlocked, saveFailed);
            equipButton.gameObject.SetActive(!hidden && !collectionExpanded);
            for (int i=0; i<cards.Length; i++)
            {
                if (cards[i] == null) continue;
                var id = (InkSkinId)i;
                bool concealed = SkinDebugSession.IsConcealed(id);
                bool cardUnlocked = SkinDebugSession.CanEquip(id);
                cardPapers[i].color = id == selected ? InkPalette.Paper : new Color(.86f, .82f, .73f, .62f);
                cardStates[i].color = id == selected ? InkPalette.Red : InkPalette.TextMuted;
                SetPreview(cardBodies[i], cardAccessories[i], id, 200, concealed, cardUnlocked);
                cardQuestions[i].enabled = concealed;
                InkLocalizedText.SetSource(cardNames[i], concealed ? "?" : InkSkinCatalog.Name(id));
                int bestHeight = PermanentGrowthProfile.SkinBestHeight(id);
                InkLocalizedText.SetSource(cardBestHeights[i], bestHeight < 0 ? "기록 없음" : $"최고 {bestHeight}m");
                cardSkillMarks[i].gameObject.SetActive(!concealed && InkSkinCatalog.HasAbility(id));
                InkLocalizedText.SetSource(cardStates[i], concealed ? "" : !cardUnlocked ? "잠김" :
                    id == InkSkinPreferences.Equipped ? "착용 중" : id == selected ? "선택됨" : "해금됨");
            }
            FitCardPreviews();
        }
        void RefreshDetailLayout(bool hidden, bool hasAbility)
        {
            // 모든 의상에 같은 슬롯을 쓴다. 능력·숨김·해금 여부가 카드 크기와 스크롤 위치를 바꾸지 않는다.
            descriptionText.gameObject.SetActive(!collectionExpanded && hidden);
            descriptionText.alignment = TextAnchor.MiddleCenter;
            PlaceDetailRow(descriptionText.rectTransform, 198f, 144f);
            PlaceDetailRow(loreText.rectTransform, 138f, 108f);
            detailDivider.rectTransform.anchoredPosition = new Vector2(0, 28f);
            detailPaper.gameObject.SetActive(!collectionExpanded && !hidden);
            detailDivider.gameObject.SetActive(!collectionExpanded && !hidden && hasAbility);
            PlaceDetailRow(detailPaper.rectTransform, 280f, 520f);
            bool showRequirement = !hidden && selected != InkSkinId.Ink;
            bool showProgress = !hidden && InkSkinAchievements.Goal(selected) > 0;
            achievementText.gameObject.SetActive(!collectionExpanded && showRequirement);
            progressText.gameObject.SetActive(!collectionExpanded && showProgress);
            achievementText.alignment = progressText.alignment = TextAnchor.MiddleCenter;
            PlaceDetailRow(achievementText.rectTransform, -252f, 76f);
            PlaceDetailRow(progressText.rectTransform, -332f, 42f);
            PlaceDetailRow(progressTrack, -378f, 30f);
            PlaceDetailRow(statusText.rectTransform, -252f, 120f);
        }
        static float PlaceDetailRow(RectTransform row, float top, float height)
        {
            row.anchoredPosition = new Vector2(0, top - height * .5f);
            row.sizeDelta = new Vector2(row.sizeDelta.x, height);
            return top - height;
        }
        void RefreshProgress(int goal, int value, bool unlocked, bool saveFailed)
        {
            bool show = goal > 0 && !saveFailed && !collectionExpanded;
            progressTrack.gameObject.SetActive(show);
            if (!show)
            {
                // 비밀·기본 먹·오류 화면으로 넘어갈 때 이전 의상의 완료 표시도 즉시 비운다.
                hasProgress = progressAnimating = progressComplete = false;
                progressTarget = progressFill.fillAmount = 0f;
                completionSeal.gameObject.SetActive(false);
                return;
            }
            float target = Mathf.Clamp01((float)value / goal);
            bool changedSkin = !hasProgress || progressSkin != selected;
            bool changedValue = !Mathf.Approximately(progressTarget, target);
            progressComplete = unlocked && target >= 1f;
            progressText.color = progressComplete ? InkPalette.Red : InkPalette.TextMuted;
            if (changedSkin || changedValue)
            {
                // 주기적인 갱신은 번짐을 재시작하지 않는다. 계정 초기화로 줄어든 값은 즉시 반영한다.
                bool decreased = !changedSkin && target < progressTarget;
                progressStart = changedSkin ? 0f : progressFill.fillAmount;
                progressTarget = target; progressSkin = selected; hasProgress = true;
                progressStartedAt = Time.unscaledTime;
                progressInactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
                progressAnimating = CanAnimateProgress() && !decreased && target > progressStart;
                progressFill.fillAmount = progressAnimating ? progressStart : target;
            }
            completionSeal.gameObject.SetActive(progressComplete && !progressAnimating);
        }
        bool CanAnimateProgress() => Application.isPlaying && visible && interactive &&
            MobileApplicationLifecycle.IsApplicationActive && !LobbySettingsProfile.ReducedMotionEnabled;
        void UpdateProgress()
        {
            if (!progressAnimating) return;
            if (!CanAnimateProgress() || progressInactiveGeneration != MobileApplicationLifecycle.InactiveGeneration)
            { SnapProgress(); return; }
            float t = Mathf.Clamp01((Time.unscaledTime - progressStartedAt) / ProgressDuration);
            // 끝으로 갈수록 천천히 스며드는 한 번의 붓질. 재질·오브젝트는 재사용한다.
            progressFill.fillAmount = Mathf.Lerp(progressStart, progressTarget, 1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f) SnapProgress();
        }
        void SnapProgress()
        {
            if (progressFill == null) return;
            progressAnimating = false;
            progressFill.fillAmount = progressTarget;
            completionSeal.gameObject.SetActive(hasProgress && progressComplete);
        }
        void FitCardPreviews()
        {
            // 큰 날개가 다른 의상의 먹까지 줄이지 않도록 각 칸 안에서만 맞춘다.
            for(int i=0;i<cards.Length;i++)
            {
                if (cards[i] == null) continue;
                Vector2 min=cardBodies[i].rectTransform.sizeDelta*-.5f, max=-min;
                if(cardAccessories[i].enabled)
                {
                    var rect=cardAccessories[i].rectTransform;
                    min=Vector2.Min(min,rect.anchoredPosition-rect.sizeDelta*.5f);
                    max=Vector2.Max(max,rect.anchoredPosition+rect.sizeDelta*.5f);
                }
                Vector2 size=max-min, available=cardViewports[i].sizeDelta-Vector2.one*8f;
                float scale=Mathf.Min(1f,Mathf.Min(available.x/size.x,available.y/size.y));
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
            // 성장과 같은 전용 화면: 산수화를 밝게 유지하고 뒤쪽 로비 입력만 막는다.
            blocker.color=Color.clear; blocker.raycastTarget=true;
            safeRoot=Stretch("SafeAreaRoot",root.transform);
            paper=Rect("WardrobeScreen",safeRoot,Vector2.zero,new Vector2(1080,1920));
            Label("Title",paper,"꾸미기",84,new Vector2(0,860),new Vector2(520,108),true);
            stage=Rect("ShowroomStage",paper,new Vector2(0,515),new Vector2(1000,560));
            // 큰 장식·파동은 제목과 하단 의상 목록을 침범하지 않는다.
            stage.gameObject.AddComponent<RectMask2D>().softness=new Vector2Int(10,16);
            var stageHit=stage.gameObject.AddComponent<Image>(); stageHit.color=Color.clear; stageHit.raycastTarget=true;
            swipe=stage.gameObject.AddComponent<InkShowroomSwipe>(); swipe.Owner=this;
            for (int side=0;side<2;side++) BuildNeighbor(stage,side);
            var preview=Rect("Preview",stage,Vector2.zero,Vector2.one*HeroSize);
            previewBody=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,Vector2.one*HeroSize);
            previewAccessory=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            previewQuestion=Label("QuestionMark",preview,"?",260,Vector2.zero,new Vector2(360,420),true);
            // 설명 영역을 넓혀도 높은 관·헬멧이 잘리지 않게 전체 의상 공통 틀에 맞춘다.
            // 한 번 계산한 틀을 공유하므로 넘길 때마다 원먹의 크기·중심이 흔들리지 않는다.
            Rect heroBounds=LobbySkinPreview.BodyDrawingBounds;
            for(int i=1;i<InkSkinCatalog.Count;i++)
            {
                var id=(InkSkinId)i;
                if (!InkSkinCatalog.IsAvailable(id)) continue;
                Rect bounds=LobbySkinPreview.CostumeBounds(id,InkSkinCatalog.Sprite(id));
                heroBounds=UnityEngine.Rect.MinMaxRect(Mathf.Min(heroBounds.xMin,bounds.xMin),Mathf.Min(heroBounds.yMin,bounds.yMin),
                    Mathf.Max(heroBounds.xMax,bounds.xMax),Mathf.Max(heroBounds.yMax,bounds.yMax));
            }
            float heroScale=Mathf.Min(1f,(stage.rect.height-40f)/(heroBounds.height*HeroSize));
            preview.localScale=Vector3.one*heroScale;
            preview.anchoredPosition=-heroBounds.center*HeroSize*heroScale;
            presentation=stage.gameObject.AddComponent<InkShowroomPresentation>(); presentation.Initialize(stage,preview);
            BuildPreviewBackdrop(preview);
            foreach (var arrow in neighborButtons) arrow.transform.SetAsLastSibling();
            detailPaper=SpriteImage("ItemDetailsPaper",paper,InkUiStyle.ActionButtonSprite,
                new Vector2(0,150),new Vector2(1000,220));
            InkUiStyle.ConfigureHanjiSurface(detailPaper);
            detailPaper.preserveAspect=false; detailPaper.color=new Color(1,1,1,.84f);
            detailDivider=SpriteImage("ItemNameBrushLine",paper,InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0,28),new Vector2(872,3));
            detailDivider.preserveAspect=false;
            var dividerTint=InkPalette.Ink; dividerTint.a=.20f; detailDivider.color=dividerTint;
            nameText=Label("SkinName",paper,"",76,new Vector2(0,188),new Vector2(900,92),true);
            loreText=Label("ItemLore",paper,"",44,new Vector2(0,84),new Vector2(900,108),false);
            loreText.color=InkPalette.Ink; loreText.fontStyle=FontStyle.Bold; loreText.lineSpacing=1.12f;
            BuildInlineSkill();
            BuildCollection();
            for(int i=0;i<InkSkinCatalog.BrowseCount;i++) BuildCard(InkSkinCatalog.BrowseAt(i));
            var toggle=Button("CollectionToggle",paper,"전체 보기",new Vector2(410,860),new Vector2(230,94),30,out collectionToggleText);
            toggle.onClick.AddListener(ToggleCollection);
            descriptionText=Label("Description",paper,"",50,new Vector2(0,-96),new Vector2(920,144),false);
            descriptionText.color=InkPalette.Ink; descriptionText.fontStyle=FontStyle.Bold;
            descriptionText.lineSpacing=1.12f;
            foreach(var text in new[]{nameText,loreText,descriptionText})
                if(text.TryGetComponent<Outline>(out var outline)) outline.enabled=false;
            achievementText=Label("Achievement",paper,"",38,new Vector2(0,-152),new Vector2(960,64),true);
            progressText=Label("AchievementProgress",paper,"",40,new Vector2(0,-212),new Vector2(800,52),true); InkLocalizedText.Exclude(progressText);
            progressTrack=Rect("AchievementTrack",paper,new Vector2(0,-253),new Vector2(860,30));
            var brush=InkUiTextureFactory.CreateBrushSprite();
            var trackImage=progressTrack.gameObject.AddComponent<UnityEngine.UI.Image>();
            trackImage.sprite=brush; trackImage.color=new Color(.11f,.106f,.102f,.16f); trackImage.raycastTarget=false;
            progressFill=Stretch("Progress",progressTrack).gameObject.AddComponent<UnityEngine.UI.Image>();
            progressFill.sprite=brush; progressFill.color=InkPalette.Ink; progressFill.raycastTarget=false;
            progressFill.type=UnityEngine.UI.Image.Type.Filled;
            progressFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; progressFill.fillOrigin=0;
            progressFill.fillAmount=0f;
            completionSeal=Rect("CompletionSeal",progressTrack,new Vector2(462,0),Vector2.one*44f);
            completionSeal.localRotation=Quaternion.Euler(0,0,-6);
            InkHudSurface.Ensure(completionSeal,true);
            var sealMark=SpriteImage("Mark",completionSeal,InkUiTextureFactory.CreateGrowthConfirmIconSprite(),Vector2.zero,Vector2.one*34f);
            sealMark.color=InkPalette.Paper; completionSeal.gameObject.SetActive(false);
            BuildEquipButton(); equipButton.onClick.AddListener(Equip);
            statusText=Label("SaveStatus",paper,"",36,new Vector2(0,-195),new Vector2(940,120),false); statusText.color=InkPalette.Red;
            var backHit=Rect("BackButton",paper,new Vector2(-420,860),new Vector2(220,120)).gameObject.AddComponent<UnityEngine.UI.Image>();
            backHit.color=Color.clear; backHit.raycastTarget=true;
            var backIcon=SpriteImage("Icon",backHit.transform,InkUiTextureFactory.CreateGrowthBackIconSprite(),new Vector2(-45,0),new Vector2(84,84));
            backIcon.color=InkPalette.TextDark;
            backButton=backHit.gameObject.AddComponent<UnityEngine.UI.Button>();
            InkUiStyle.ConfigureButton(backButton,backIcon); backButton.onClick.AddListener(Back);
            ApplyCollectionLayout();
            rootGroup.alpha=0; rootGroup.interactable=rootGroup.blocksRaycasts=false;
        }
        void BuildCollection()
        {
            collectionRoot=Rect("CollectionScroll",paper,new Vector2(0,-548),new Vector2(1000,424));
            collectionScroll=collectionRoot.gameObject.AddComponent<InkWardrobeScroll>();
            collectionScroll.Owner=this;
            var viewport=Stretch("Viewport",collectionRoot);
            var hit=viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color=Color.clear; hit.raycastTarget=true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            collectionContent=Rect("Content",viewport,Vector2.zero,new Vector2(980,0));
            collectionContent.anchorMin=collectionContent.anchorMax=new Vector2(.5f,1);
            collectionContent.pivot=new Vector2(.5f,1);
            collectionGrid=collectionContent.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            collectionGrid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            collectionGrid.childAlignment=TextAnchor.UpperCenter;
            collectionGrid.spacing=new Vector2(16,16);
            var fitter=collectionContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            collectionScroll.content=collectionContent; collectionScroll.viewport=viewport;
            collectionScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Elastic;
            collectionScroll.elasticity=.10f;
            collectionScroll.decelerationRate=.055f; collectionScroll.scrollSensitivity=56;
        }
        public void ToggleCollection()
        {
            if (!CanInteract()) return;
            collectionExpanded=!collectionExpanded;
            presentation.Cancel(); swipe.Cancel();
            ApplyCollectionLayout(); RefreshSelection(); RevealSelectedCard(true);
        }
        void ApplyCollectionLayout()
        {
            if (collectionGrid==null) return;
            StopCollection();
            collectionScroll.horizontal=!collectionExpanded; collectionScroll.vertical=collectionExpanded;
            collectionGrid.constraint=collectionExpanded ? GridLayoutGroup.Constraint.FixedColumnCount : GridLayoutGroup.Constraint.FixedRowCount;
            collectionGrid.constraintCount=collectionExpanded ? 3 : 1;
            collectionGrid.childAlignment=collectionExpanded ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft;
            collectionGrid.spacing=collectionExpanded ? new Vector2(20,24) : new Vector2(24,0);
            collectionGrid.padding=collectionExpanded ? new RectOffset(5,5,8,8) : new RectOffset(380,380,0,0);
            var fitter=collectionContent.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit=collectionExpanded ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit=collectionExpanded ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            collectionContent.anchorMin=collectionContent.anchorMax=collectionExpanded ? new Vector2(.5f,1) : new Vector2(0,.5f);
            collectionContent.pivot=collectionContent.anchorMin;
            collectionContent.sizeDelta=collectionExpanded ? new Vector2(980,0) : new Vector2(0,320);
            collectionContent.anchoredPosition=Vector2.zero;
            collectionRoot.anchoredPosition=collectionExpanded ? Vector2.zero : CompactCollectionPosition;
            collectionRoot.sizeDelta=collectionExpanded ? new Vector2(1000,1560) : CompactCollectionSize;
            stage.gameObject.SetActive(!collectionExpanded);
            foreach(var label in new[]{nameText,loreText,descriptionText,achievementText,progressText,statusText})
                if(label!=null) label.gameObject.SetActive(!collectionExpanded);
            if(progressTrack!=null) progressTrack.gameObject.SetActive(!collectionExpanded && hasProgress);
            InkLocalizedText.SetSource(collectionToggleText,collectionExpanded ? "크게 보기" : "전체 보기");
            RefreshCardLayout(true);
            collectionScroll.enabled=CanInteract();
        }
        void RefreshCardLayout(bool force=false)
        {
            if(collectionGrid==null) return;
            var cell=collectionExpanded ? new Vector2(310,420) : new Vector2(240,300);
            if(!force && collectionGrid.cellSize==cell) return;
            // 카드 높이는 모드별로 고정한다. 의상 설명 길이와 무관하게 같은 크기를 유지한다.
            collectionGrid.cellSize=cell;
            if(!collectionExpanded) collectionContent.sizeDelta=new Vector2(collectionContent.sizeDelta.x,cell.y);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(collectionContent);
            // 카드 자체는 Grid가, 내부 종이와 미리보기만 이 화면이 배치한다.
            for(int i=0;i<cards.Length;i++)
            {
                if(cards[i]==null) continue;
                cardPapers[i].rectTransform.sizeDelta=cell;
                cardViewports[i].anchoredPosition=collectionExpanded ? new Vector2(0,44) : Vector2.zero;
                cardViewports[i].sizeDelta=new Vector2(cell.x-28,collectionExpanded ? 200 : cell.y-144);
                cardBodies[i].rectTransform.sizeDelta=Vector2.one*200;
                cardPreviews[i].sizeDelta=Vector2.one*200;
                cardQuestions[i].rectTransform.sizeDelta=cardViewports[i].sizeDelta;
                cardQuestions[i].fontSize=collectionExpanded ? 96 : 60;
                var bestHeight=cardBestHeights[i];
                bestHeight.rectTransform.sizeDelta=new Vector2(cell.x-24,68);
                bestHeight.rectTransform.anchoredPosition=new Vector2(0,-36);
                bestHeight.fontSize=32;
                InkLocalizedFont.Bind(bestHeight);
                cardNames[i].gameObject.SetActive(collectionExpanded);
                cardNames[i].rectTransform.anchoredPosition=new Vector2(0,-102);
                cardNames[i].rectTransform.sizeDelta=new Vector2(cell.x-28,80);
                cardStates[i].fontSize=30;
                cardStates[i].rectTransform.anchoredPosition=new Vector2(0,-cell.y*.5f+34);
                cardStates[i].rectTransform.sizeDelta=new Vector2(cell.x-24,52);
            }
            FitCardPreviews(); RefreshCardDepth();
        }
        internal void RefreshCardDepth()
        {
            if(collectionScroll==null) return;
            float center=-collectionContent.anchoredPosition.x/collectionScroll.CardStride;
            bool motion=!collectionExpanded && !LobbySettingsProfile.ReducedMotionEnabled;
            for(int slot=0;slot<InkSkinCatalog.BrowseCount;slot++)
            {
                var card=cards[(int)InkSkinCatalog.BrowseAt(slot)];
                if(card==null) continue;
                float distance=motion ? Mathf.Clamp(slot-center,-2,2) : 0;
                var scale=Vector3.one*(1f-Mathf.Abs(distance)*.035f);
                var rotation=Quaternion.Euler(0,0,-distance*1.5f);
                if(card.transform.localScale!=scale) card.transform.localScale=scale;
                if(!card.transform.localRotation.Equals(rotation)) card.transform.localRotation=rotation;
            }
        }
        void RevealSelectedCard(bool immediate=false)
        {
            if(collectionScroll==null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(collectionContent);
            if(!collectionExpanded)
            {
                collectionScroll.CenterOn(InkSkinCatalog.BrowseIndex(selected),immediate);
                return;
            }
            var card=(RectTransform)cards[(int)selected].transform;
            float top=-card.anchoredPosition.y-card.rect.height*.5f;
            float height=collectionScroll.viewport.rect.height;
            collectionScroll.StopMovement();
            // 전체보기 진입 시 선택된 줄의 위를 맞춰 반쯤 잘린 첫 줄로 시작하지 않는다.
            collectionContent.anchoredPosition=new Vector2(0,Mathf.Clamp(top-8f,0,Mathf.Max(0,collectionContent.rect.height-height)));
        }
        void StopCollection()
        {
            if(collectionScroll==null) return;
            collectionScroll.CancelMotion(); collectionScroll.enabled=false;
        }
        void OnApplicationPause(bool paused) { if(paused) { StopSkillDetails(); StopCollection(); } }
        void OnApplicationFocus(bool focused) { if(!focused) { StopSkillDetails(); StopCollection(); } }
        void BuildEquipButton()
        {
            // 성장하기와 같은 검정 먹 붓·밝은 글씨. 팝업용 종이 받침은 사용하지 않는다.
            Vector2 size=new Vector2(820,160);
            var root=Rect("EquipButton",paper,new Vector2(0,-850),size);
            var hit=root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color=Color.clear; hit.raycastTarget=true;
            float scaleX=size.x/LobbyMenuLayout.BackgroundSize.x;
            var brush=Rect("BrushBackground",root,new Vector2(-LobbyMenuLayout.BrushArtworkLabelOffsetX*scaleX,0),size)
                .gameObject.AddComponent<UnityEngine.UI.RawImage>();
            var lobby=FindAnyObjectByType<LobbyView>(FindObjectsInactive.Include);
            brush.texture=lobby?.StartButton?.GetComponent<UnityEngine.UI.RawImage>()?.texture ?? InkUiTextureFactory.CreateBrushSprite().texture;
            brush.color=Color.white; brush.raycastTarget=false;
            equipButton=root.gameObject.AddComponent<UnityEngine.UI.Button>();
            InkUiStyle.ConfigureButton(equipButton,brush);
            equipText=Label("Label",root,"착용하기",64,Vector2.zero,new Vector2(700,110),true);
            equipText.color=InkPalette.TextLight;
        }
        void BuildCard(InkSkinId id)
        {
            int i=(int)id;
            var button=Button("SkinCard"+i,collectionContent,"",Vector2.zero,new Vector2(180,190),24,out _);
            cards[i]=button.gameObject; cardPapers[i]=button.transform.Find("Paper").GetComponent<Image>();
            var viewport=Rect("PreviewViewport",button.transform,new Vector2(0,16),new Vector2(104,90));
            viewport.gameObject.AddComponent<RectMask2D>();
            var preview=Rect("Preview",viewport,Vector2.zero,new Vector2(100,100));
            cardViewports[i]=viewport; cardPreviews[i]=preview;
            cardBodies[i]=SpriteImage("OriginalInkBody",preview,previewBodySprite,Vector2.zero,new Vector2(100,100));
            cardAccessories[i]=SpriteImage("Accessory",preview,null,Vector2.zero,Vector2.one);
            // 물음표는 장식 크기에 맞추는 보정과 분리해 모든 히든 칸의 중앙에 고정한다.
            cardQuestions[i]=Label("QuestionMark",viewport,"?",48,Vector2.zero,new Vector2(104,90),true);
            cardStates[i]=Label("State",button.transform,"",24,new Vector2(0,-48),new Vector2(104,28),false);
            cardNames[i]=Label("CardName",button.transform,"",34,Vector2.zero,new Vector2(282,80),true);
            var bestHeight=Label("BestHeight",button.transform,"기록 없음",32,new Vector2(0,-28),new Vector2(166,50),true);
            cardBestHeights[i]=bestHeight;
            bestHeight.rectTransform.anchorMin=bestHeight.rectTransform.anchorMax=new Vector2(.5f,1);
            // 해금 조건 분류 대신 이 의상을 착용하고 정상 정산한 개인 최고 고도를 표시한다.
            bestHeight.rectTransform.anchoredPosition=new Vector2(0,-28);
            bestHeight.color=InkPalette.Ink;
            bestHeight.fontStyle=FontStyle.Bold;
            // 해금 여부를 뜻하는 체크와 구분되는 작은 능력 낙관. 숨겨진 의상에서는 표시하지 않는다.
            var skillMark=Rect("SpecialSkillMark",button.transform,Vector2.zero,new Vector2(28,34));
            skillMark.anchorMin=skillMark.anchorMax=new Vector2(1,1);
            skillMark.anchoredPosition=new Vector2(-22,-74);
            InkHudSurface.Ensure(skillMark,true);
            var spark=SpriteImage("Spark",skillMark,null,Vector2.zero,new Vector2(10,10));
            spark.color=InkPalette.TextLight; spark.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            cardSkillMarks[i]=skillMark;
            button.onClick.AddListener(()=>
            {
                if(!CanInteract() || collectionScroll.IsDragging) return;
                if(collectionExpanded) ToggleCollection();
                Select(id);
            });
        }
        void BuildNeighbor(RectTransform stage, int side)
        {
            int step=side==0 ? -1 : 1;
            var button=Button(side==0 ? "PreviousCostume" : "NextCostume",stage,side==0 ? "‹" : "›",
                new Vector2(step*430,20),new Vector2(116,148),90,out _);
            button.onClick.AddListener(()=>StepSelection(step)); neighborButtons[side]=button;
        }
        void BuildPreviewBackdrop(RectTransform preview)
        {
            // 의상 전체가 아니라 실제 먹 몸체를 기준으로 원형 배경과 발밑 그림자를 맞춘다.
            Rect drawing = LobbySkinPreview.BodyDrawingBounds;
            float bodyScale = HeroSize * preview.localScale.x;
            Vector2 center = preview.anchoredPosition + drawing.center * bodyScale;
            float diameter = Mathf.Max(drawing.width, drawing.height) * bodyScale + 96f;
            var backdrop = Rect("PreviewBackdrop", stage, center, Vector2.one * diameter);
            backdrop.SetAsFirstSibling();
            var paperWash = SpriteImage("FocusPaperWash", backdrop, InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero, Vector2.one * diameter);
            var paperTint = InkPalette.TextLight; paperTint.a = .84f; paperWash.color = paperTint;
            var ring = SpriteImage("FocusBrushRing", backdrop,
                Resources.Load<Sprite>("MukJump/UI/PermanentGrowth/pg_selected_ring"),
                Vector2.zero, Vector2.one * diameter);
            var ringTint = InkPalette.Ink; ringTint.a = .26f; ring.color = ringTint;
            ring.enabled = ring.sprite != null;
            var floor = (RectTransform)stage.Find("InkStageFloor");
            floor.anchoredPosition = new Vector2(center.x,
                preview.anchoredPosition.y + drawing.yMin * bodyScale - 12f);
            floor.sizeDelta = new Vector2(drawing.width * bodyScale * 1.06f, 32f);
        }
        void SetPreview(Image body, Image accessory, InkSkinId id, float size, bool hidden, bool unlocked)
        {
            // 미해금은 알파 윤곽만 남기고 색·눈·무늬를 숨긴다. 히든은 원먹도 표시하지 않는다.
            body.sprite=hidden ? null : previewBodySprite; body.enabled=!hidden;
            body.color=unlocked ? Color.white : Color.black;
            accessory.sprite=hidden ? null : InkSkinCatalog.Sprite(id);
            accessory.enabled=accessory.sprite!=null;
            accessory.rectTransform.localScale=Vector3.one;
            accessory.rectTransform.localRotation=Quaternion.identity;
            accessory.color=unlocked ? Color.white : Color.black;
            if (InkSkinCatalog.BehindBody(id)) accessory.transform.SetAsFirstSibling();
            else accessory.transform.SetAsLastSibling();
            if (!accessory.enabled) return;
            float width=size*InkSkinCatalog.Width(id);
            accessory.rectTransform.sizeDelta=new Vector2(width,width*accessory.sprite.rect.height/accessory.sprite.rect.width);
            accessory.rectTransform.anchoredPosition=InkSkinCatalog.Offset(id)*size;
        }
        void ApplyLayout()
        {
            if (safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(safeRoot, safe, Screen.width, Screen.height);
            float scale = MobileUiLayout.CalculateFitScale(new Vector2(1080, 1920), safe, Screen.width, Screen.height, Vector2.zero);
            paper.localScale = Vector3.one * scale;
            // 성장 화면과 같은 상단 기준. 꾸미기는 배너가 없으므로 노치만 제외한다.
            float topOffset=(safe.height*1920f/Screen.height-1920f*scale)*.5f;
            paper.anchoredPosition=new Vector2(0,topOffset);
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
            var rect = Rect(name, parent, position, size);
            UnityEngine.UI.Image image = name=="Accessory" ? rect.gameObject.AddComponent<InkAccessoryImage>() : rect.gameObject.AddComponent<UnityEngine.UI.Image>();
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
