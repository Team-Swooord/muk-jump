using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 메인 로비의 성장 수련과 100종 먹결 도감을 한 개의 페이지형 모달로 제공한다.
    /// 100개 행을 한꺼번에 만들지 않고 고정된 6개 행만 재사용해 모바일 UI 비용을 제한한다.
    [DisallowMultipleComponent]
    public sealed class LobbyCollectionView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4000;
        const int PageSize = 6;

        enum DisplayMode
        {
            Closed,
            Growth,
            Codex,
        }

        sealed class EntryRow
        {
            public RectTransform Root;
            public Image Paper;
            public Button Button;
            public Text Index;
            public Text Name;
            public Text Detail;
            public Text State;
            public int FilteredIndex;
        }

        readonly List<RoguelikeGrowthDefinition> filtered = new(100);
        readonly List<EntryRow> rows = new(PageSize);

        CanvasGroup rootGroup;
        RectTransform safeAreaRoot;
        Text titleText;
        Text subtitleText;
        Text categoryText;
        Text pageText;
        Button categoryButton;
        Button previousButton;
        Button nextButton;
        Button closeButton;
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
        public int CreatedRowCount => rows.Count;
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
            GrowthFocusProfile.Changed += HandleFocusChanged;
        }

        void OnDisable()
        {
            GrowthFocusProfile.Changed -= HandleFocusChanged;
            UnbindManager();
            CloseImmediate();
        }

        void Update()
        {
            if (manager == null)
                BindManager();
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();

            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                Screen.safeArea != lastSafeArea)
                ApplySafeArea();
        }

        public void OpenGrowth()
        {
            BuildIfNeeded();
            mode = DisplayMode.Growth;
            categoryFilter = null;
            currentPage = 0;
            SetVisible(true);
            RebuildFilter();
        }

        public void OpenCodex()
        {
            BuildIfNeeded();
            mode = DisplayMode.Codex;
            categoryFilter = null;
            currentPage = 0;
            SetVisible(true);
            RebuildFilter();
        }

        public void Close()
        {
            mode = DisplayMode.Closed;
            SetVisible(false);
        }

        /// EditMode에서 실제 런타임과 같은 고정 행 풀·페이지 구조를 검증한다.
        public void BuildForTests()
        {
            BuildIfNeeded();
            CloseImmediate();
        }

        void BindManager()
        {
            var next = GameManager.Instance;
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

        void HandleFocusChanged()
        {
            if (mode == DisplayMode.Growth)
                RefreshPage();
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
                new Color(0.025f, 0.023f, 0.02f, 0.62f));
            dim.raycastTarget = true;

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", root.transform);
            var panel = CreateRect(
                "GrowthCodexScroll",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(900f, 1510f));
            BuildScrollFrame(panel);

            titleText = CreateText(
                "Title", panel, "먹결 도감", 58,
                new Vector2(0f, 620f), new Vector2(700f, 78f),
                InkPalette.TextDark, FontStyle.Bold);
            subtitleText = CreateText(
                "Subtitle", panel, string.Empty, 29,
                new Vector2(0f, 540f), new Vector2(750f, 80f),
                ReadableMutedColor(), FontStyle.Normal);

            categoryButton = CreatePaperButton(
                "CategoryButton", panel, "전체 계보",
                new Vector2(0f, 455f), new Vector2(420f, 70f), 29);
            categoryText = categoryButton.transform
                .Find("Paper/Label")?.GetComponent<Text>();
            categoryButton.onClick.AddListener(HandleCategoryPressed);

            for (int i = 0; i < PageSize; i++)
            {
                int slot = i;
                var row = CreateEntryRow(panel, i);
                row.Button.onClick.AddListener(() => HandleRowPressed(slot));
                rows.Add(row);
            }

            previousButton = CreatePaperButton(
                "PreviousButton", panel, "이전",
                new Vector2(-250f, -565f), new Vector2(190f, 76f), 31);
            nextButton = CreatePaperButton(
                "NextButton", panel, "다음",
                new Vector2(250f, -565f), new Vector2(190f, 76f), 31);
            pageText = CreateText(
                "Page", panel, "1 / 1", 30,
                new Vector2(0f, -565f), new Vector2(220f, 70f),
                InkPalette.TextDark, FontStyle.Bold);
            closeButton = CreateBrushButton(
                "CloseButton", panel, "닫기",
                new Vector2(0f, -675f), new Vector2(420f, 88f), 34);

            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            closeButton.onClick.AddListener(Close);

            ApplySafeArea();
        }

        void BuildScrollFrame(Transform panel)
        {
            Sprite brush = InkUiTextureFactory.CreateBrushSprite();
            var shadow = CreateImage(
                "InkShadow", panel, brush, new Vector2(12f, -15f),
                new Vector2(1510f, 890f),
                new Color(0f, 0f, 0f, 0.16f));
            shadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var outline = CreateImage(
                "ScrollOutline", panel, brush, Vector2.zero,
                new Vector2(1490f, 870f), InkPalette.Ink);
            outline.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var paper = CreateImage(
                "HanjiPaper", panel, brush, Vector2.zero,
                new Vector2(1462f, 842f), InkPalette.Paper);
            paper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            CreateImage(
                "PaperCore", panel, null, Vector2.zero,
                new Vector2(790f, 1390f), InkPalette.Paper);
        }

        EntryRow CreateEntryRow(Transform parent, int index)
        {
            float y = 335f - index * 146f;
            var root = CreateRect(
                $"Entry{index + 1}",
                parent,
                new Vector2(0f, y),
                new Vector2(790f, 128f));
            var outline = CreateImage(
                "Outline", root, null, Vector2.zero,
                new Vector2(790f, 128f), InkPalette.Ink);
            var paper = CreateImage(
                "Paper", outline.transform, null, Vector2.zero,
                new Vector2(778f, 116f), InkPalette.Paper2);
            paper.raycastTarget = true;
            var button = outline.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = ReadableButtonColors();

            var number = CreateText(
                "Index", paper.transform, "001", 25,
                new Vector2(-340f, 24f), new Vector2(78f, 42f),
                ReadableMutedColor(), FontStyle.Bold);
            var name = CreateText(
                "Name", paper.transform, "먹결 이름", 35,
                new Vector2(-178f, 25f), new Vector2(290f, 50f),
                InkPalette.TextDark, FontStyle.Bold, TextAnchor.MiddleLeft);
            var state = CreateText(
                "State", paper.transform, "기획", 24,
                new Vector2(298f, 27f), new Vector2(150f, 40f),
                ReadableMutedColor(), FontStyle.Bold, TextAnchor.MiddleRight);
            var detail = CreateText(
                "Detail", paper.transform, "효과 설명", 26,
                new Vector2(18f, -31f), new Vector2(680f, 54f),
                InkPalette.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);

            return new EntryRow
            {
                Root = root,
                Paper = paper,
                Button = button,
                Index = number,
                Name = name,
                Detail = detail,
                State = state,
            };
        }

        void RebuildFilter()
        {
            filtered.Clear();
            var all = RoguelikeGrowthCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var definition = all[i];
                if (mode == DisplayMode.Growth &&
                    (definition.Status != ImplementationStatus.RuntimeReady ||
                     !definition.RuntimeType.HasValue))
                    continue;
                if (categoryFilter.HasValue &&
                    definition.Category != categoryFilter.Value)
                    continue;
                filtered.Add(definition);
            }

            int pageCount = GetPageCount();
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
            RefreshPage();
        }

        void RefreshPage()
        {
            if (mode == DisplayMode.Closed) return;

            bool growth = mode == DisplayMode.Growth;
            titleText.text = growth ? "수련 방향" : "먹결 도감";
            subtitleText.text = growth
                ? "고른 먹결은 첫 성장 두루마리에 반드시 나타납니다"
                : $"25계보 · 100먹결 · 실전 {RoguelikeGrowthCatalog.RuntimeReady.Count}종";
            categoryText.text = growth
                ? (GrowthFocusProfile.HasSelection ? "균형으로 되돌리기" : "현재 균형 수련")
                : categoryFilter.HasValue
                    ? GetCategoryName(categoryFilter.Value)
                    : "전체 계보";

            int start = currentPage * PageSize;
            string selected = GrowthFocusProfile.SelectedDefinitionId;
            for (int i = 0; i < rows.Count; i++)
            {
                int filteredIndex = start + i;
                var row = rows[i];
                if (filteredIndex >= filtered.Count)
                {
                    row.Root.gameObject.SetActive(false);
                    row.FilteredIndex = -1;
                    continue;
                }

                var definition = filtered[filteredIndex];
                row.Root.gameObject.SetActive(true);
                row.FilteredIndex = filteredIndex;
                row.Index.text = (RoguelikeGrowthCatalog.IndexOf(definition.Id) + 1)
                    .ToString("000");
                row.Name.text = definition.Name;
                row.Detail.text = definition.Description;
                bool isSelected = growth &&
                                  string.Equals(selected, definition.Id,
                                      StringComparison.Ordinal);
                row.State.text = isSelected
                    ? "집중 중"
                    : definition.Status == ImplementationStatus.RuntimeReady
                        ? GetTierName(definition.Tier)
                        : "기획";
                row.State.color = isSelected
                    ? InkPalette.Ink
                    : ReadableMutedColor();
                row.Paper.color = isSelected
                    ? InkPalette.Paper
                    : InkPalette.Paper2;
                row.Button.interactable = true;
            }

            int pageCount = GetPageCount();
            pageText.text = $"{currentPage + 1} / {pageCount}";
            previousButton.interactable = currentPage > 0;
            nextButton.interactable = currentPage + 1 < pageCount;
        }

        void HandleRowPressed(int slot)
        {
            if (mode != DisplayMode.Growth || slot < 0 || slot >= rows.Count)
                return;
            int index = rows[slot].FilteredIndex;
            if (index < 0 || index >= filtered.Count)
                return;
            GrowthFocusProfile.TrySelect(filtered[index].Id);
        }

        void HandleCategoryPressed()
        {
            if (mode == DisplayMode.Growth)
            {
                GrowthFocusProfile.Clear();
                RefreshPage();
                return;
            }
            if (mode != DisplayMode.Codex) return;

            var values = (GrowthCatalogCategory[])Enum.GetValues(
                typeof(GrowthCatalogCategory));
            int next = -1;
            if (categoryFilter.HasValue)
                next = Array.IndexOf(values, categoryFilter.Value);

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
            var all = RoguelikeGrowthCatalog.All;
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
            int pageCount = GetPageCount();
            if (currentPage + 1 >= pageCount) return;
            currentPage++;
            RefreshPage();
        }

        int GetPageCount() => Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)PageSize));

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
            string objectName, Transform parent, Sprite sprite,
            Vector2 position, Vector2 size, Color color)
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
            string objectName, Transform parent, string value, int fontSize,
            Vector2 position, Vector2 size, Color color, FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(objectName, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(20, fontSize - 7);
            text.resizeTextMaxSize = fontSize;
            text.alignByGeometry = true;
            return text;
        }

        static Button CreatePaperButton(
            string objectName, Transform parent, string label,
            Vector2 position, Vector2 size, int fontSize)
        {
            var outline = CreateImage(
                objectName, parent, null, position, size, InkPalette.Ink);
            var paper = CreateImage(
                "Paper", outline.transform, null, Vector2.zero,
                size - new Vector2(10f, 10f), InkPalette.Paper2);
            paper.raycastTarget = true;
            var button = outline.gameObject.AddComponent<Button>();
            button.targetGraphic = paper;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = ReadableButtonColors();
            CreateText(
                "Label", paper.transform, label, fontSize, Vector2.zero,
                size - new Vector2(28f, 16f),
                InkPalette.TextDark, FontStyle.Bold);
            return button;
        }

        static Button CreateBrushButton(
            string objectName, Transform parent, string label,
            Vector2 position, Vector2 size, int fontSize)
        {
            var brush = CreateImage(
                objectName, parent, InkUiTextureFactory.CreateBrushSprite(),
                position, size, InkPalette.Ink);
            brush.raycastTarget = true;
            var button = brush.gameObject.AddComponent<Button>();
            button.targetGraphic = brush;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = ReadableButtonColors();
            CreateText(
                "Label", brush.transform, label, fontSize, Vector2.zero,
                size - new Vector2(36f, 14f),
                InkPalette.TextLight, FontStyle.Bold);
            return button;
        }

        static ColorBlock ReadableButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            colors.pressedColor = new Color(0.76f, 0.72f, 0.64f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.52f, 0.5f, 0.46f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        static Color ReadableMutedColor()
        {
            Color muted = InkPalette.TextMuted;
            muted.a = 0.88f;
            return muted;
        }
    }
}
