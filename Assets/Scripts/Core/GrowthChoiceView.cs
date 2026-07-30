using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 증강 두루마리를 획득했을 때 한 가지 먹결을 고르는 전용 모달.
    /// 씬에 UI 계층을 직렬화하지 않고 최초 사용 시 한 번만 생성해 구형 씬도 지원한다.
    public sealed class GrowthChoiceView : MonoBehaviour
    {
        const int GrowthTypeCount = 8;
        const int MaxVisibleChoices = 3;
        const int CanvasSortingOrder = 3000;
        const float RevealDuration = 0.26f;
        const float CloseDuration = 0.16f;
        const float RollOpenDistance = 690f;
        const float ClosedPaperScale = 0.12f;
        const float CardWidth = 244f;
        const float CardHeight = 760f;
        const float CardVerticalPosition = -70f;
        const float ThreeCardSpacing = 264f;
        const float TwoCardSpacing = 130f;
        const float VisualFootprintWidth = 920f;
        const float VisualFootprintHeight = 1500f;
        const float SafeAreaPadding = 40f;

        static readonly string[] IconResourcePaths =
        {
            "MukJump/UI/Growth/growth_vitality",
            "MukJump/UI/Growth/growth_jump",
            "MukJump/UI/Growth/growth_ink_capacity",
            "MukJump/UI/Growth/growth_ink_regen",
            "MukJump/UI/Growth/growth_platform",
            "MukJump/UI/Growth/growth_platform",
            "MukJump/UI/Growth/growth_guard",
            "MukJump/UI/Growth/growth_fortune",
        };

        [Header("선택 카드 아이콘")]
        [Tooltip("GrowthUpgradeType enum 순서와 같은 8칸. 발판 수명·개수는 같은 아이콘을 공유한다.")]
        [SerializeField] Sprite[] growthIcons = new Sprite[GrowthTypeCount];

        public static GrowthChoiceView Instance { get; private set; }
        public bool IsOpen { get; private set; }

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        RectTransform panel;
        RectTransform scrollBody;
        RectTransform topRoll;
        RectTransform bottomRoll;
        RectTransform contentRect;
        CanvasGroup contentGroup;
        Text hintText;
        readonly ChoiceCard[] choiceCards = new ChoiceCard[MaxVisibleChoices];
        RunGrowthController boundController;
        Coroutine visibilityRoutine;
        bool selectionLocked;
        int lastScreenWidth;
        int lastScreenHeight;
        Rect lastSafeArea;
        float responsiveScale = 1f;
        float revealScale = 1f;

        sealed class ChoiceCard
        {
            public RectTransform Root;
            public Button Button;
            public Image Outline;
            public Image Paper;
            public Image Icon;
            public Text Title;
            public Text Status;
            public Text Effect;
            public CanvasGroup Group;
            public GrowthUpgradeType Type;
            public UnityEngine.Events.UnityAction Pressed;
        }

        void Awake()
        {
            if (Application.isPlaying)
                BuildIfNeeded();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            BuildIfNeeded();
            BindController();
            BindButtons();
            ApplySafeArea();
        }

        void OnDisable()
        {
            bool cancelPendingChoice =
                IsOpen && boundController != null &&
                boundController.HasPendingChoice;
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            UnbindButtons();
            IsOpen = false;
            selectionLocked = false;
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
            if (cancelPendingChoice)
                boundController?.CancelChoice();
            UnbindController();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            BindController();
            // 씬 전환·게임오버가 성장 선택을 취소한 경우 모달이 다음 화면 위에
            // 남아 입력을 막지 않도록 즉시 정리한다.
            if (IsOpen &&
                (boundController == null || !boundController.HasPendingChoice))
            {
                HideImmediately();
                return;
            }
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height ||
                lastSafeArea != Screen.safeArea)
                ApplySafeArea();
        }

        /// 씬 빌더가 임포트한 스프라이트를 enum 순서대로 주입할 때 사용한다.
        /// 발판 공용 아이콘을 한 번만 넘기는 7개 배열과 기존 2개 배열도 함께 지원한다.
        public void SetSprites(params Sprite[] sprites)
        {
            EnsureIconArray();
            if (sprites != null && sprites.Length == GrowthTypeCount - 1)
            {
                SetIcon(GrowthUpgradeType.Vitality, sprites[0]);
                SetIcon(GrowthUpgradeType.JumpPower, sprites[1]);
                SetIcon(GrowthUpgradeType.InkCapacity, sprites[2]);
                SetIcon(GrowthUpgradeType.InkRecovery, sprites[3]);
                SetIcon(GrowthUpgradeType.PlatformLifetime, sprites[4]);
                SetIcon(GrowthUpgradeType.PlatformSlots, sprites[4]);
                SetIcon(GrowthUpgradeType.StrokeGuard, sprites[5]);
                SetIcon(GrowthUpgradeType.ItemFortune, sprites[6]);
            }
            else if (sprites != null)
            {
                int count = Mathf.Min(sprites.Length, growthIcons.Length);
                for (int i = 0; i < count; i++)
                    if (sprites[i] != null)
                        growthIcons[i] = sprites[i];
            }

            ResolveSprites();
            ApplyCardSprites();
        }

        public void Show(RunGrowthController controller)
        {
            if (controller == null) return;

            BuildIfNeeded();
            BindController(controller);
            ApplySafeArea();
            selectionLocked = false;
            IsOpen = true;
            SetSelectedCard(null);
            RefreshCards();

            if (visibilityRoutine != null)
                StopCoroutine(visibilityRoutine);
            visibilityRoutine = StartCoroutine(RevealRoutine());
        }

        /// 테스트나 세션 정리에서 사용할 수 있는 닫기 진입점. 플레이 중에는 선택 후에만 호출한다.
        public void Hide()
        {
            if (!IsOpen) return;
            BeginClose();
        }

        void BindController()
        {
            BindController(RunGrowthController.Instance);
        }

        void BindController(RunGrowthController controller)
        {
            if (controller == boundController) return;
            UnbindController();
            boundController = controller;
            if (boundController == null) return;
            boundController.ChoiceRequested += HandleChoiceRequested;
            boundController.ChoiceCancelled += HandleChoiceCancelled;
            boundController.Changed += HandleGrowthChanged;
            // 런타임 폴백으로 이 컴포넌트가 이벤트 뒤에 추가되어도 정지 화면을 복구한다.
            if (boundController.HasPendingChoice && !IsOpen)
                Show(boundController);
        }

        void UnbindController()
        {
            if (boundController != null)
            {
                boundController.ChoiceRequested -= HandleChoiceRequested;
                boundController.ChoiceCancelled -= HandleChoiceCancelled;
                boundController.Changed -= HandleGrowthChanged;
            }
            boundController = null;
        }

        void BindButtons()
        {
            UnbindButtons();
            for (int i = 0; i < choiceCards.Length; i++)
            {
                ChoiceCard card = choiceCards[i];
                if (card?.Button == null) continue;
                card.Pressed ??= () => HandleCardPressed(card);
                card.Button.onClick.AddListener(card.Pressed);
            }
        }

        void UnbindButtons()
        {
            for (int i = 0; i < choiceCards.Length; i++)
            {
                ChoiceCard card = choiceCards[i];
                if (card?.Button != null && card.Pressed != null)
                    card.Button.onClick.RemoveListener(card.Pressed);
            }
        }

        void HandleChoiceRequested()
        {
            if (boundController != null)
                Show(boundController);
        }

        void HandleChoiceCancelled()
        {
            if (IsOpen)
                HideImmediately();
        }

        void HandleGrowthChanged()
        {
            if (IsOpen)
                RefreshCards();
        }

        void HandleCardPressed(ChoiceCard card)
        {
            if (card != null && card.Root.gameObject.activeSelf)
                Select(card.Type, card);
        }

        void Select(GrowthUpgradeType type, ChoiceCard selectedCard)
        {
            if (!IsOpen || selectionLocked || boundController == null) return;

            selectionLocked = true;
            SetCardsInteractable(false);
            if (!boundController.TrySelectUpgrade(type))
            {
                selectionLocked = false;
                RefreshCards();
                return;
            }

            SetSelectedCard(selectedCard);
            BeginClose();
        }

        void BeginClose()
        {
            if (visibilityRoutine != null)
                StopCoroutine(visibilityRoutine);
            rootGroup.interactable = false;
            visibilityRoutine = StartCoroutine(CloseRoutine());
        }

        void HideImmediately()
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            ApplyRevealPose(0f);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            IsOpen = false;
            selectionLocked = false;
        }

        IEnumerator RevealRoutine()
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = false;
            ApplyRevealPose(0f);

            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyRevealPose(elapsed / RevealDuration);
                yield return null;
            }

            ApplyRevealPose(1f);
            rootGroup.interactable = true;
            RefreshCards();
            visibilityRoutine = null;
        }

        IEnumerator CloseRoutine()
        {
            float elapsed = 0f;
            while (elapsed < CloseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth01(elapsed / CloseDuration);
                ApplyRevealPose(1f - progress);
                yield return null;
            }

            ApplyRevealPose(0f);
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            IsOpen = false;
            selectionLocked = false;
            visibilityRoutine = null;

            // 닫힘 연출이 끝난 뒤에만 시간을 재개해 모달 뒤 게임이 먼저 움직이지 않게 한다.
            if (boundController != null)
            {
                if (boundController.HasSelectedPendingChoice)
                    boundController.FinishChoice();
                else
                    boundController.CancelChoice();
            }
        }

        void RefreshCards()
        {
            if (boundController == null || choiceCards[0] == null)
                return;

            var offers = boundController.CurrentOffers;
            int offerCount = offers == null
                ? 0
                : Mathf.Min(MaxVisibleChoices, offers.Count);
            if (hintText != null)
            {
                hintText.text = offerCount switch
                {
                    1 => "이어갈 먹결을 선택하세요",
                    2 => "둘 중 하나를 선택하세요",
                    _ => "셋 중 하나를 선택하세요",
                };
            }
            bool canInteract = IsOpen && !selectionLocked &&
                               rootGroup != null && rootGroup.interactable;

            for (int i = 0; i < choiceCards.Length; i++)
            {
                ChoiceCard card = choiceCards[i];
                bool visible = i < offerCount;
                card.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                GrowthUpgradeType type = offers[i];
                int level = Mathf.Max(0, boundController.GetLevel(type));
                int maxLevel = Mathf.Max(1, boundController.GetMaxLevel(type));
                bool maxed = level >= maxLevel;

                card.Type = type;
                card.Root.anchoredPosition = new Vector2(
                    ResolveCardX(i, offerCount), CardVerticalPosition);
                card.Title.text = GetTitle(type);
                card.Status.text = GetStatus(level, maxLevel);
                card.Effect.text = GetEffect(type);
                card.Icon.sprite = GetIcon(type);
                SetCardState(card, canInteract && !maxed, maxed);
            }
        }

        static string GetStatus(int level, int maxLevel)
        {
            int nextLevel = Mathf.Min(level + 1, maxLevel);
            return level >= maxLevel
                ? $"Lv.{level} · 완성"
                : $"Lv.{level} → {nextLevel}";
        }

        static string GetTitle(GrowthUpgradeType type)
        {
            return type switch
            {
                GrowthUpgradeType.Vitality => "먹두께",
                GrowthUpgradeType.JumpPower => "도약",
                GrowthUpgradeType.InkCapacity => "큰 벼루",
                GrowthUpgradeType.InkRecovery => "먹샘",
                GrowthUpgradeType.PlatformLifetime => "긴 여운",
                GrowthUpgradeType.PlatformSlots => "겹친 획",
                GrowthUpgradeType.StrokeGuard => "굳은 획",
                GrowthUpgradeType.ItemFortune => "길운",
                _ => "먹결",
            };
        }

        static string GetEffect(GrowthUpgradeType type)
        {
            return type switch
            {
                GrowthUpgradeType.Vitality =>
                    "먹떼 완충 +1",
                GrowthUpgradeType.JumpPower =>
                    "점프력 +4%",
                GrowthUpgradeType.InkCapacity =>
                    "최대 먹 +10%",
                GrowthUpgradeType.InkRecovery =>
                    "먹 회복 +12%",
                GrowthUpgradeType.PlatformLifetime =>
                    "발판 수명 +10%",
                GrowthUpgradeType.PlatformSlots =>
                    "동시 발판 +1",
                GrowthUpgradeType.StrokeGuard =>
                    "낙묵석 방어 +1",
                GrowthUpgradeType.ItemFortune =>
                    "아이템 간격 -7%",
                _ => string.Empty,
            };
        }

        static float ResolveCardX(int index, int count)
        {
            return count switch
            {
                <= 1 => 0f,
                2 => index == 0 ? -TwoCardSpacing : TwoCardSpacing,
                _ => (index - 1) * ThreeCardSpacing,
            };
        }

        static void SetCardState(ChoiceCard card, bool interactable, bool maxed)
        {
            if (card == null) return;
            card.Button.interactable = interactable;
            card.Group.alpha = maxed ? 0.72f : 1f;
            card.Paper.color = maxed
                ? new Color(InkPalette.Paper2.r, InkPalette.Paper2.g, InkPalette.Paper2.b, 0.9f)
                : InkPalette.Paper;
            card.Outline.color = InkPalette.Ink;
        }

        void SetCardsInteractable(bool interactable)
        {
            for (int i = 0; i < choiceCards.Length; i++)
                if (choiceCards[i] != null && choiceCards[i].Root.gameObject.activeSelf)
                    choiceCards[i].Button.interactable = interactable;
        }

        void SetSelectedCard(ChoiceCard selected)
        {
            for (int i = 0; i < choiceCards.Length; i++)
                ApplySelectedState(choiceCards[i], selected == choiceCards[i]);
        }

        static void ApplySelectedState(ChoiceCard card, bool selected)
        {
            if (card == null) return;
            card.Outline.color = selected
                ? InkPalette.Gold
                : InkPalette.Ink;
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            ResolveSprites();
            var existing = transform.Find("GrowthChoiceCanvas");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject(
                "GrowthChoiceCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            var backdrop = CreateStretchImage(
                "InkWash",
                root.transform,
                new Color(0.035f, 0.032f, 0.028f, 0.6f));
            backdrop.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            panel = CreateRect(
                "GrowthScrollPopup",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(900f, 1480f));

            BuildScrollPaper();
            BuildContent();
            ApplyCardSprites();
            ApplyRevealPose(0f);
        }

        void ResolveSprites()
        {
            EnsureIconArray();
            for (int i = 0; i < growthIcons.Length; i++)
                if (growthIcons[i] == null && i < IconResourcePaths.Length)
                    growthIcons[i] = Resources.Load<Sprite>(IconResourcePaths[i]);
        }

        void ApplyCardSprites()
        {
            for (int i = 0; i < choiceCards.Length; i++)
                if (choiceCards[i]?.Icon != null)
                    choiceCards[i].Icon.sprite = GetIcon(choiceCards[i].Type);
        }

        void EnsureIconArray()
        {
            if (growthIcons != null && growthIcons.Length == GrowthTypeCount)
                return;

            var resized = new Sprite[GrowthTypeCount];
            if (growthIcons != null)
                for (int i = 0; i < Mathf.Min(growthIcons.Length, resized.Length); i++)
                    resized[i] = growthIcons[i];
            growthIcons = resized;
        }

        void SetIcon(GrowthUpgradeType type, Sprite icon)
        {
            int index = (int)type;
            if (icon != null && index >= 0 && index < growthIcons.Length)
                growthIcons[index] = icon;
        }

        Sprite GetIcon(GrowthUpgradeType type)
        {
            EnsureIconArray();
            int index = (int)type;
            return index >= 0 && index < growthIcons.Length
                ? growthIcons[index]
                : null;
        }

        void BuildScrollPaper()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            scrollBody = CreateRect(
                "ScrollBody",
                panel,
                Vector2.zero,
                new Vector2(840f, 1390f));

            var shadow = CreateImage(
                "InkBleedShadow",
                scrollBody,
                brush,
                new Vector2(13f, -15f),
                new Vector2(1410f, 848f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollBodyOutline",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(1390f, 834f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "ScrollPaper",
                scrollBody,
                brush,
                Vector2.zero,
                new Vector2(1364f, 808f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            // 붓 마스크 섬유 틈 사이로 뒷배경이 비처럼 비치지 않도록 안쪽을 한지로 채운다.
            CreateImage(
                "PaperCore",
                scrollBody,
                null,
                Vector2.zero,
                new Vector2(760f, 1300f),
                InkPalette.Paper);

            topRoll = CreateScrollRoll(panel, RollOpenDistance, true);
            bottomRoll = CreateScrollRoll(panel, -RollOpenDistance, false);
        }

        void BuildContent()
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            contentRect = CreateRect(
                "GrowthContent",
                panel,
                Vector2.zero,
                new Vector2(780f, 1320f));
            contentGroup = contentRect.gameObject.AddComponent<CanvasGroup>();

            CreateAugmentEmblem(contentRect, blob);

            hintText = CreateText(
                "Hint",
                contentRect,
                "셋 중 하나를 선택하세요",
                34,
                new Vector2(0f, 405f),
                new Vector2(660f, 56f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableTextWeight(hintText, 0.16f);

            for (int i = 0; i < choiceCards.Length; i++)
            {
                choiceCards[i] = CreateChoiceCard(
                    $"GrowthChoice{i + 1}",
                    contentRect,
                    new Vector2(
                        ResolveCardX(i, choiceCards.Length),
                        CardVerticalPosition));
                choiceCards[i].Root.gameObject.SetActive(false);
            }

            var footer = CreateText(
                "FooterHint",
                contentRect,
                "이번 도전에만 유지",
                24,
                new Vector2(0f, -565f),
                new Vector2(710f, 54f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableTextWeight(footer, 0.14f);
        }

        static void CreateAugmentEmblem(
            Transform parent,
            Sprite blob)
        {
            var emblem = CreateRect(
                "AugmentEmblem",
                parent,
                new Vector2(0f, 525f),
                new Vector2(210f, 210f));

            CreateImage(
                "EmblemInkRing",
                emblem,
                blob,
                Vector2.zero,
                new Vector2(188f, 188f),
                InkPalette.Ink);
            CreateImage(
                "EmblemPaper",
                emblem,
                blob,
                Vector2.zero,
                new Vector2(148f, 148f),
                InkPalette.Paper2);

            var title = CreateText(
                "Title",
                emblem,
                "증강",
                48,
                Vector2.zero,
                new Vector2(132f, 72f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableTextWeight(title, 0.26f);
        }

        static ChoiceCard CreateChoiceCard(
            string objectName,
            Transform parent,
            Vector2 position)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                objectName,
                parent,
                position,
                new Vector2(CardWidth, CardHeight));
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var outline = CreateImage(
                "Outline",
                root,
                brush,
                Vector2.zero,
                new Vector2(744f, 230f),
                InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            var paper = CreateImage(
                "Paper",
                root,
                brush,
                Vector2.zero,
                new Vector2(730f, 216f),
                InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            paper.raycastTarget = true;

            // 붓 섬유 틈은 중앙 한지로 채우되 거친 둥근 외곽선은 그대로 남긴다.
            CreateImage(
                "PaperCore",
                root,
                null,
                Vector2.zero,
                new Vector2(188f, 650f),
                InkPalette.Paper);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.95f, 0.93f, 0.88f, 1f),
                pressedColor = new Color(0.86f, 0.84f, 0.79f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.76f, 0.74f, 0.7f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
            root.gameObject.AddComponent<InkUiPressFeedback>();

            // 참고 판화처럼 장식 문양 대신 큰 담청회색 먹달 하나만 남긴다.
            // 성장 아이콘 자체가 카드의 한 장면이 되고 나머지는 여백으로 읽힌다.
            CreateImage(
                "SceneMoon",
                root,
                blob,
                new Vector2(0f, 118f),
                new Vector2(204f, 204f),
                new Color(
                    InkPalette.WindPlatform.r,
                    InkPalette.WindPlatform.g,
                    InkPalette.WindPlatform.b,
                    0.34f));
            var iconImage = CreateImage(
                "Icon",
                root,
                null,
                new Vector2(0f, 118f),
                new Vector2(196f, 196f),
                InkPalette.Ink);
            iconImage.preserveAspect = true;

            var nameText = CreateText(
                "Name",
                root,
                string.Empty,
                40,
                new Vector2(0f, 312f),
                new Vector2(212f, 68f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableTextWeight(nameText, 0.24f);

            var statusText = CreateText(
                "Status",
                root,
                "Lv.0 → 1",
                22,
                new Vector2(0f, -34f),
                new Vector2(214f, 36f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableTextWeight(statusText, 0.13f);

            var effectText = CreateText(
                "Effect",
                root,
                string.Empty,
                28,
                new Vector2(0f, -180f),
                new Vector2(214f, 52f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableTextWeight(effectText, 0.11f);

            return new ChoiceCard
            {
                Root = root,
                Button = button,
                Outline = outline,
                Paper = paper,
                Icon = iconImage,
                Title = nameText,
                Status = statusText,
                Effect = effectText,
                Group = group,
            };
        }

        void ApplyRevealPose(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float appear = EaseOutCubic(Mathf.InverseLerp(0f, 0.22f, t));
            float unroll = EaseOutCubic(Mathf.InverseLerp(0.02f, 0.82f, t));
            float content = EaseOutCubic(Mathf.InverseLerp(0.25f, 0.86f, t));

            rootGroup.alpha = appear;
            revealScale = Mathf.Lerp(0.965f, 1f, unroll);
            ApplyPanelScale();
            scrollBody.localScale = new Vector3(
                1f,
                Mathf.Lerp(ClosedPaperScale, 1f, unroll),
                1f);
            topRoll.anchoredPosition = Vector2.up * (RollOpenDistance * unroll);
            bottomRoll.anchoredPosition = Vector2.down * (RollOpenDistance * unroll);
            contentGroup.alpha = content;
            contentRect.anchoredPosition = Vector2.down * (14f * (1f - content));
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                Mathf.Clamp01(safe.xMin / Screen.width),
                Mathf.Clamp01(safe.yMin / Screen.height));
            safeAreaRoot.anchorMax = new Vector2(
                Mathf.Clamp01(safe.xMax / Screen.width),
                Mathf.Clamp01(safe.yMax / Screen.height));
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;

            float referenceScale = 1920f / Screen.height;
            responsiveScale = CalculateResponsiveScale(
                new Vector2(
                    safe.width * referenceScale,
                    safe.height * referenceScale));
            ApplyPanelScale();
        }

        void ApplyPanelScale()
        {
            if (panel == null) return;
            panel.localScale = Vector3.one * (revealScale * responsiveScale);
        }

        /// CanvasScaler가 높이 기준일 때 계산한 논리 Safe Area에 두루마리 전체를 맞춘다.
        /// 1080×1920에서는 원본 크기를 유지하고, 폭이 좁은 긴 화면에서만 균등 축소한다.
        static float CalculateResponsiveScale(Vector2 logicalSafeSize)
        {
            if (logicalSafeSize.x <= 0f || logicalSafeSize.y <= 0f)
                return 1f;

            float usableWidth = Mathf.Max(1f, logicalSafeSize.x - SafeAreaPadding);
            float usableHeight = Mathf.Max(1f, logicalSafeSize.y - SafeAreaPadding);
            return Mathf.Clamp01(Mathf.Min(
                usableWidth / VisualFootprintWidth,
                usableHeight / VisualFootprintHeight));
        }

        static RectTransform CreateScrollRoll(Transform parent, float y, bool top)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            Sprite blob = InkUiTextureFactory.CreateBlobSprite();
            var root = CreateRect(
                top ? "TopRoll" : "BottomRoll",
                parent,
                new Vector2(0f, y),
                new Vector2(820f, 96f));

            CreateImage(
                "Shadow",
                root,
                brush,
                new Vector2(8f, -7f),
                new Vector2(794f, 78f),
                new Color(0f, 0f, 0f, 0.17f));
            var roll = CreateImage(
                "PaperRoll",
                root,
                brush,
                Vector2.zero,
                new Vector2(804f, 78f),
                InkPalette.Ink);
            CreateImage(
                "Paper",
                roll.transform,
                brush,
                Vector2.zero,
                new Vector2(778f, 58f),
                InkPalette.Paper2);
            CreateImage(
                "FoldShade",
                roll.transform,
                brush,
                new Vector2(0f, top ? -13f : 13f),
                new Vector2(740f, 8f),
                new Color(InkPalette.Ink.r, InkPalette.Ink.g, InkPalette.Ink.b, 0.12f));

            for (int side = -1; side <= 1; side += 2)
            {
                var cap = CreateImage(
                    side < 0 ? "LeftCap" : "RightCap",
                    root,
                    blob,
                    new Vector2(side * 388f, 0f),
                    new Vector2(82f, 82f),
                    InkPalette.Ink);
                CreateImage(
                    "Paper",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(62f, 62f),
                    InkPalette.Paper2);
                CreateImage(
                    "Axis",
                    cap.transform,
                    blob,
                    Vector2.zero,
                    new Vector2(19f, 19f),
                    InkPalette.Ink);
            }
            return root;
        }

        static RectTransform CreateRect(
            string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateStretchImage(string objectName, Transform parent, Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            FontStyle style)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            return text;
        }

        static void AddReadableTextWeight(Text text, float alpha)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<Outline>();
            Color ink = InkPalette.Ink;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, alpha);
            outline.effectDistance = new Vector2(1.35f, -1.35f);
            outline.useGraphicAlpha = true;
        }

        static Color ReadableMutedColor()
        {
            Color color = InkPalette.TextDark;
            color.a = 0.92f;
            return color;
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
