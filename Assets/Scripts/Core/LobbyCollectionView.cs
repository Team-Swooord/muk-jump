using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 한 판 성장 100종을 큰 그림 카드로 탐색하고 탭해 설명을 뒤집어 보는 로비 도감.
    /// 화면에는 네 카드만 재사용해 100개 항목의 모바일 오브젝트 비용을 고정한다.
    [DisallowMultipleComponent]
    public sealed class LobbyCollectionView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4000;
        const int PageSize = 4;
        const float FlipDuration = 0.22f;

        enum DisplayMode
        {
            Closed,
            Codex,
        }

        sealed class CardSlot
        {
            public RectTransform Root;
            public CanvasGroup Front;
            public CanvasGroup Back;
            public Button Button;
            public Image Icon;
            public Text Index;
            public Text Name;
            public Text State;
            public Text BackName;
            public Text BackDescription;
            public Text BackMeta;
            public int FilteredIndex;
            public bool ShowingBack;
            public Coroutine FlipRoutine;
        }

        readonly List<RoguelikeGrowthDefinition> filtered = new(100);
        readonly List<CardSlot> cards = new(PageSize);

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        Text subtitleText;
        Text categoryText;
        Text pageText;
        Button previousButton;
        Button nextButton;
        DisplayMode mode;
        GrowthCatalogCategory? categoryFilter;
        int currentPage;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        GameManager manager;

        public bool IsOpen => mode != DisplayMode.Closed &&
                              rootGroup != null &&
                              rootGroup.blocksRaycasts;
        public int FilteredCount => filtered.Count;
        public int CurrentPage => currentPage;
        public int CreatedRowCount => cards.Count;
        public string CurrentModeName => mode.ToString();

        void Awake()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        void OnEnable()
        {
            BuildIfNeeded();
            BindManager();
        }

        void OnDisable()
        {
            UnbindManager();
            ResetCardTransforms();
            CloseImmediate();
        }

        void Update()
        {
            if (manager == null)
                BindManager();
            if (manager != null &&
                manager.State != GameState.Lobby &&
                IsOpen)
                Close();
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                Screen.safeArea != lastSafeArea)
                ApplySafeArea();
        }

        public void OpenCodex()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }
            mode = DisplayMode.Codex;
            categoryFilter = null;
            currentPage = 0;
            SetVisible(true);
            RebuildFilter();
        }

        public void Close()
        {
            mode = DisplayMode.Closed;
            ResetCardTransforms();
            SetVisible(false);
        }

        public void BuildForTests()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        public bool IsCardBackVisible(int slot)
        {
            return slot >= 0 &&
                   slot < cards.Count &&
                   cards[slot].ShowingBack;
        }

        public void FlipCardForTests(int slot)
        {
            if (slot < 0 || slot >= cards.Count) return;
            SetCardFace(cards[slot], !cards[slot].ShowingBack);
        }

        void BindManager()
        {
            GameManager next = GameManager.Instance;
            if (ReferenceEquals(manager, next)) return;
            UnbindManager();
            manager = next;
            if (manager != null)
                manager.StateChanged += HandleStateChanged;
        }

        void UnbindManager()
        {
            if (manager != null)
                manager.StateChanged -= HandleStateChanged;
            manager = null;
        }

        void HandleStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Lobby)
                Close();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            var stale = transform.Find("LobbyCollectionCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "LobbyCollectionCanvas",
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
            var dim = CreateStretchImage(
                "InkDim",
                root.transform,
                new Color(0.025f, 0.023f, 0.02f, 0.64f));
            dim.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            var panel = CreateRect(
                "CodexScroll",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(900f, 1510f));
            BuildScrollFrame(panel);

            CreateReadableText(
                "Title", panel, "두루마리 도감",
                InkUiStyle.ScreenTitleSize,
                new Vector2(0f, 625f), new Vector2(730f, 84f),
                InkPalette.TextDark);
            subtitleText = CreateReadableText(
                "Subtitle", panel,
                "큰 그림을 눌러 먹결의 설명을 확인하세요",
                InkUiStyle.BodySize,
                new Vector2(0f, 548f), new Vector2(760f, 62f),
                InkPalette.TextMuted);

            var categoryButton = CreatePaperButton(
                "CategoryButton", panel, "전체 계보",
                new Vector2(0f, 470f), new Vector2(430f, 78f),
                InkUiStyle.BodySize);
            categoryText = categoryButton.transform
                .Find("Paper/Label")?.GetComponent<Text>();
            categoryButton.onClick.AddListener(HandleCategoryPressed);

            for (int i = 0; i < PageSize; i++)
            {
                int slot = i;
                CardSlot card = CreateCard(panel, i);
                card.Button.onClick.AddListener(() => HandleCardPressed(slot));
                cards.Add(card);
            }

            previousButton = CreatePaperButton(
                "PreviousButton", panel, "이전",
                new Vector2(-245f, -590f), new Vector2(210f, 88f),
                InkUiStyle.BodySize);
            nextButton = CreatePaperButton(
                "NextButton", panel, "다음",
                new Vector2(245f, -590f), new Vector2(210f, 88f),
                InkUiStyle.BodySize);
            pageText = CreateReadableText(
                "Page", panel, "1 / 1", InkUiStyle.BodySize,
                new Vector2(0f, -590f), new Vector2(230f, 76f),
                InkPalette.TextDark);
            var closeButton = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, -690f), new Vector2(420f, 92f),
                InkUiStyle.CardTitleSize);
            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            closeButton.onClick.AddListener(Close);

            ApplySafeArea();
        }

        CardSlot CreateCard(Transform parent, int index)
        {
            float x = index % 2 == 0 ? -202f : 202f;
            float y = index < 2 ? 205f : -300f;
            var root = CreateRect(
                $"CodexCard{index + 1}",
                parent,
                new Vector2(x, y),
                new Vector2(370f, 470f));
            var hit = CreateImage(
                "HitSurface", root, null, Vector2.zero,
                new Vector2(370f, 470f), InkPalette.Ink);
            var button = hit.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, hit, addInkFeedback: false);

            CanvasGroup front = CreateCardFace("Front", hit.transform);
            var frontPaper = CreateImage(
                "Paper", front.transform, null, Vector2.zero,
                new Vector2(356f, 456f), InkPalette.Paper2);
            var iconPaper = CreateImage(
                "IconPaper", frontPaper.transform,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 58f), new Vector2(292f, 292f),
                new Color(
                    InkPalette.Paper.r,
                    InkPalette.Paper.g,
                    InkPalette.Paper.b,
                    0.98f));
            var icon = CreateImage(
                "Icon", iconPaper.transform, null,
                Vector2.zero, new Vector2(252f, 252f), Color.white);
            icon.preserveAspect = true;
            Text number = CreateReadableText(
                "Index", frontPaper.transform, "001",
                InkUiStyle.CaptionSize,
                new Vector2(-128f, 196f), new Vector2(82f, 44f),
                InkPalette.TextMuted);
            Text state = CreateReadableText(
                "State", frontPaper.transform, "사용 가능",
                InkUiStyle.CaptionSize,
                new Vector2(98f, 196f), new Vector2(160f, 44f),
                InkPalette.TextMuted, TextAnchor.MiddleRight);
            Text name = CreateReadableText(
                "Name", frontPaper.transform, "먹결 이름",
                InkUiStyle.CardTitleSize,
                new Vector2(0f, -178f), new Vector2(320f, 72f),
                InkPalette.TextDark);

            CanvasGroup back = CreateCardFace("Back", hit.transform);
            var backPaper = CreateImage(
                "Paper", back.transform, null, Vector2.zero,
                new Vector2(356f, 456f), InkPalette.Paper);
            Text backName = CreateReadableText(
                "Name", backPaper.transform, "먹결 이름",
                InkUiStyle.CardTitleSize,
                new Vector2(0f, 164f), new Vector2(316f, 86f),
                InkPalette.TextDark);
            Text description = CreateReadableText(
                "Description", backPaper.transform, "설명",
                32,
                new Vector2(0f, 12f), new Vector2(312f, 220f),
                InkPalette.TextDark);
            description.lineSpacing = 1.12f;
            Text meta = CreateReadableText(
                "Meta", backPaper.transform, "분류 · 단계",
                InkUiStyle.CaptionSize,
                new Vector2(0f, -151f), new Vector2(312f, 90f),
                InkPalette.TextMuted);
            CreateReadableText(
                "FlipHint", backPaper.transform, "다시 눌러 그림 보기",
                26,
                new Vector2(0f, -207f), new Vector2(300f, 38f),
                InkPalette.TextMuted);

            var slot = new CardSlot
            {
                Root = root,
                Front = front,
                Back = back,
                Button = button,
                Icon = icon,
                Index = number,
                Name = name,
                State = state,
                BackName = backName,
                BackDescription = description,
                BackMeta = meta,
                FilteredIndex = -1,
            };
            SetCardFace(slot, false);
            return slot;
        }

        void RebuildFilter()
        {
            filtered.Clear();
            IReadOnlyList<RoguelikeGrowthDefinition> all =
                RoguelikeGrowthCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                RoguelikeGrowthDefinition definition = all[i];
                if (categoryFilter.HasValue &&
                    definition.Category != categoryFilter.Value)
                    continue;
                filtered.Add(definition);
            }

            currentPage = Mathf.Clamp(
                currentPage,
                0,
                GetPageCount() - 1);
            RefreshPage();
        }

        void RefreshPage()
        {
            if (mode == DisplayMode.Closed) return;
            ResetCardTransforms();
            subtitleText.text =
                $"실전 {RoguelikeGrowthCatalog.RuntimeReady.Count} · " +
                $"전체 {RoguelikeGrowthCatalog.All.Count} · 그림을 눌러 뒤집기";
            categoryText.text = categoryFilter.HasValue
                ? GetCategoryName(categoryFilter.Value)
                : "전체 계보";

            int start = currentPage * PageSize;
            for (int i = 0; i < cards.Count; i++)
            {
                CardSlot card = cards[i];
                int filteredIndex = start + i;
                if (filteredIndex >= filtered.Count)
                {
                    card.Root.gameObject.SetActive(false);
                    card.FilteredIndex = -1;
                    continue;
                }

                RoguelikeGrowthDefinition definition =
                    filtered[filteredIndex];
                card.Root.gameObject.SetActive(true);
                card.Root.localScale = Vector3.one;
                card.FilteredIndex = filteredIndex;
                card.Index.text =
                    (RoguelikeGrowthCatalog.IndexOf(definition.Id) + 1)
                    .ToString("000");
                card.Name.text = definition.Name;
                card.State.text =
                    definition.Status == ImplementationStatus.RuntimeReady
                        ? "사용 가능"
                        : "기획";
                card.Icon.sprite = LoadIcon(definition);
                card.Icon.color = card.Icon.sprite != null
                    ? Color.white
                    : InkPalette.Ink;
                card.BackName.text = definition.Name;
                card.BackDescription.text = definition.Description;
                card.BackMeta.text =
                    $"{GetCategoryName(definition.Category)} · " +
                    $"{GetTierName(definition.Tier)} · 최대 Lv.{definition.MaxLevel}";
                SetCardFace(card, false);
            }

            int pageCount = GetPageCount();
            pageText.text = $"{currentPage + 1} / {pageCount}";
            previousButton.interactable = currentPage > 0;
            nextButton.interactable = currentPage + 1 < pageCount;
        }

        void HandleCardPressed(int slot)
        {
            if (slot < 0 || slot >= cards.Count) return;
            CardSlot card = cards[slot];
            if (card.FilteredIndex < 0 || card.FlipRoutine != null) return;

            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                card.Root.position);
            InkUiFeedbackController.PlayTap(screenPosition);
            card.FlipRoutine = StartCoroutine(
                FlipCard(card, !card.ShowingBack));
        }

        IEnumerator FlipCard(CardSlot card, bool showBack)
        {
            float half = FlipDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                float x = Mathf.Lerp(1f, 0.06f, 1f - Mathf.Pow(1f - t, 3f));
                card.Root.localScale = new Vector3(x, 1f, 1f);
                yield return null;
            }

            SetCardFace(card, showBack);
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                float x = Mathf.Lerp(0.06f, 1f, 1f - Mathf.Pow(1f - t, 3f));
                card.Root.localScale = new Vector3(x, 1f, 1f);
                yield return null;
            }
            card.Root.localScale = Vector3.one;
            card.FlipRoutine = null;
        }

        static void SetCardFace(CardSlot card, bool showBack)
        {
            card.ShowingBack = showBack;
            SetFaceVisible(card.Front, !showBack);
            SetFaceVisible(card.Back, showBack);
        }

        static void SetFaceVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        void HandleCategoryPressed()
        {
            if (mode != DisplayMode.Codex) return;
            var values = (GrowthCatalogCategory[])Enum.GetValues(
                typeof(GrowthCatalogCategory));
            int next = categoryFilter.HasValue
                ? Array.IndexOf(values, categoryFilter.Value)
                : -1;
            for (int attempt = 0; attempt <= values.Length; attempt++)
            {
                next++;
                if (next >= values.Length)
                {
                    categoryFilter = null;
                    break;
                }
                if (!CatalogContains(values[next])) continue;
                categoryFilter = values[next];
                break;
            }
            currentPage = 0;
            RebuildFilter();
        }

        bool CatalogContains(GrowthCatalogCategory category)
        {
            IReadOnlyList<RoguelikeGrowthDefinition> all =
                RoguelikeGrowthCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Category == category)
                    return true;
            return false;
        }

        void PreviousPage()
        {
            if (currentPage <= 0) return;
            currentPage--;
            RefreshPage();
        }

        void NextPage()
        {
            if (currentPage + 1 >= GetPageCount()) return;
            currentPage++;
            RefreshPage();
        }

        int GetPageCount() =>
            Mathf.Max(
                1,
                Mathf.CeilToInt(filtered.Count / (float)PageSize));

        void SetVisible(bool visible)
        {
            if (rootGroup == null) return;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
            ApplySafeArea();
        }

        void CloseImmediate()
        {
            mode = DisplayMode.Closed;
            SetVisible(false);
        }

        void ResetCardTransforms()
        {
            StopAllCoroutines();
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].FlipRoutine = null;
                cards[i].Root.localScale = Vector3.one;
                SetCardFace(cards[i], false);
            }
        }

        void ApplySafeArea()
        {
            if (safeAreaRoot == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
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
        }

        static Sprite LoadIcon(RoguelikeGrowthDefinition definition)
        {
            string path = definition.RuntimeType switch
            {
                GrowthUpgradeType.Vitality =>
                    "MukJump/UI/Growth/growth_vitality",
                GrowthUpgradeType.JumpPower =>
                    "MukJump/UI/Growth/growth_jump",
                GrowthUpgradeType.InkCapacity =>
                    "MukJump/UI/Growth/growth_ink_capacity",
                GrowthUpgradeType.InkRecovery =>
                    "MukJump/UI/Growth/growth_ink_regen",
                GrowthUpgradeType.PlatformLifetime =>
                    "MukJump/UI/Growth/growth_platform",
                GrowthUpgradeType.PlatformSlots =>
                    "MukJump/UI/Growth/growth_platform",
                GrowthUpgradeType.StrokeGuard =>
                    "MukJump/UI/Growth/growth_guard",
                GrowthUpgradeType.ItemFortune =>
                    "MukJump/UI/Growth/growth_fortune",
                _ => CategoryIconPath(definition.Category),
            };
            return Resources.Load<Sprite>(path);
        }

        static string CategoryIconPath(GrowthCatalogCategory category)
        {
            return category switch
            {
                GrowthCatalogCategory.Survival =>
                    "MukJump/UI/Growth/growth_vitality",
                GrowthCatalogCategory.Jump =>
                    "MukJump/UI/Growth/growth_jump",
                GrowthCatalogCategory.InkResource =>
                    "MukJump/UI/Growth/growth_ink_capacity",
                GrowthCatalogCategory.Platform =>
                    "MukJump/UI/Growth/growth_platform",
                GrowthCatalogCategory.PlatformDefense =>
                    "MukJump/UI/Growth/growth_guard",
                GrowthCatalogCategory.Item =>
                    "MukJump/UI/Growth/growth_fortune",
                GrowthCatalogCategory.Weather =>
                    "MukJump/UI/Growth/growth_platform",
                GrowthCatalogCategory.Swarm =>
                    "MukJump/UI/Growth/growth_vitality",
                GrowthCatalogCategory.Shield =>
                    "MukJump/UI/Growth/growth_guard",
                GrowthCatalogCategory.Hazard =>
                    "MukJump/UI/Growth/growth_guard",
                GrowthCatalogCategory.Drawing =>
                    "MukJump/UI/Growth/growth_platform",
                GrowthCatalogCategory.Mastery =>
                    "MukJump/UI/Growth/growth_scroll",
                GrowthCatalogCategory.AirControl =>
                    "MukJump/UI/Growth/growth_jump",
                GrowthCatalogCategory.Pact =>
                    "MukJump/UI/Growth/growth_fortune",
                _ => "MukJump/UI/Growth/growth_scroll",
            };
        }

        static string GetCategoryName(GrowthCatalogCategory category)
        {
            return category switch
            {
                GrowthCatalogCategory.Survival => "생존",
                GrowthCatalogCategory.Jump => "도약",
                GrowthCatalogCategory.InkResource => "먹 자원",
                GrowthCatalogCategory.Platform => "발판",
                GrowthCatalogCategory.PlatformDefense => "발판 방어",
                GrowthCatalogCategory.Item => "아이템",
                GrowthCatalogCategory.Weather => "기후·풍맥",
                GrowthCatalogCategory.Swarm => "먹떼",
                GrowthCatalogCategory.Shield => "방패",
                GrowthCatalogCategory.Hazard => "장애물",
                GrowthCatalogCategory.Drawing => "드로잉",
                GrowthCatalogCategory.Mastery => "숙련",
                GrowthCatalogCategory.AirControl => "공중 제어",
                GrowthCatalogCategory.Pact => "서약",
                _ => category.ToString(),
            };
        }

        static string GetTierName(NodeTier tier)
        {
            return tier switch
            {
                NodeTier.Root => "뿌리",
                NodeTier.Branch => "가지",
                NodeTier.Completion => "완성",
                _ => tier.ToString(),
            };
        }

        static CanvasGroup CreateCardFace(string name, Transform parent)
        {
            var rect = CreateStretchRect(name, parent);
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        static void BuildScrollFrame(Transform panel)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var shadow = CreateImage(
                "InkShadow", panel, brush, new Vector2(12f, -15f),
                new Vector2(1510f, 890f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            var outline = CreateImage(
                "ScrollOutline", panel, brush, Vector2.zero,
                new Vector2(1490f, 870f), InkPalette.Ink);
            outline.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            var paper = CreateImage(
                "HanjiPaper", panel, brush, Vector2.zero,
                new Vector2(1462f, 842f), InkPalette.Paper);
            paper.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 90f);
            CreateImage(
                "PaperCore", panel, null, Vector2.zero,
                new Vector2(790f, 1390f), InkPalette.Paper);
        }

        static Button CreatePaperButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var outline = CreateImage(
                objectName, parent, null, position, size, InkPalette.Ink);
            var paper = CreateImage(
                "Paper", outline.transform, null, Vector2.zero,
                size - new Vector2(10f, 10f), InkPalette.Paper2);
            var button = outline.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, paper);
            CreateReadableText(
                "Label", paper.transform, label, fontSize,
                Vector2.zero, size - new Vector2(28f, 16f),
                InkPalette.TextDark);
            return button;
        }

        static Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var brush = CreateImage(
                objectName, parent,
                InkUiTextureFactory.CreateBrushSprite(),
                position, size, InkPalette.Ink);
            var button = brush.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, brush);
            CreateReadableText(
                "Label", brush.transform, label, fontSize,
                Vector2.zero, size - new Vector2(36f, 14f),
                InkPalette.TextLight);
            return button;
        }

        static Text CreateReadableText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = color;
            InkUiStyle.ApplyReadableText(
                text,
                fontSize,
                alignment,
                strong: true);
            return text;
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchRect(
            string objectName,
            Transform parent)
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

        static Image CreateStretchImage(
            string objectName,
            Transform parent,
            Color color)
        {
            var rect = CreateStretchRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
