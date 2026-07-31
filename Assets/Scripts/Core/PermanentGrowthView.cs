using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비의 영구 성장 전용 화면.
    /// 생존·도약·먹 운용 세 계보와 하나의 공용 상세판만 표시해 작은 화면에서도
    /// 능력의 선행 관계와 현재 선택을 한눈에 읽을 수 있게 한다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;
        const float ReferenceHeight = 1920f;
        const string ArtResourceRoot = "MukJump/UI/PermanentGrowth/";
        static readonly Vector2 HiddenScreenPosition =
            new(0f, ReferenceHeight);

        sealed class GrowthNodeView
        {
            public PermanentGrowthDefinition Definition;
            public RectTransform Root;
            public Image IncomingLine;
            public Image Surface;
            public Image Ring;
            public Image Icon;
            public Image CompletionMark;
            public Button Button;
            public Text Name;
            public Text Level;
        }

        readonly List<GrowthNodeView> nodes = new();
        readonly List<RectTransform> branchHeaders = new();
        readonly Dictionary<string, Sprite> spriteCache = new();
        readonly HashSet<string> missingSpritePaths = new();

        CanvasGroup rootGroup;
        Canvas rootCanvas;
        RectTransform safeAreaRoot;
        RectTransform contentPanel;
        Text balanceText;
        Text detailBranchText;
        Text detailNameText;
        Text detailLevelText;
        Text detailDescriptionText;
        Text detailCurrentText;
        Text detailNextText;
        Text detailLockText;
        Text detailCostText;
        Text purchaseButtonText;
        Image detailIcon;
        Image detailCostIcon;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        float purchaseLockedUntil;
        bool purchaseInProgress;
        int selectedSlot;

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public Button BackButton { get; private set; }
        public Button PurchaseButton { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public bool IsDedicatedScreen => ScreenRoot != null;
        public int CreatedRowCount => branchHeaders.Count;
        public int CreatedNodeCount => nodes.Count;
        public string BalanceLabel => balanceText != null
            ? balanceText.text
            : string.Empty;
        public PermanentGrowthType SelectedGrowthType =>
            nodes.Count > 0 && selectedSlot >= 0 && selectedSlot < nodes.Count
                ? nodes[selectedSlot].Definition.Type
                : PermanentGrowthType.InkCapacity;

        void OnEnable()
        {
            BindManager();
            PermanentGrowthProfile.Changed += HandleProfileChanged;
        }

        void OnDisable()
        {
            PermanentGrowthProfile.Changed -= HandleProfileChanged;
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

        public void Open()
        {
            BuildIfNeeded();
            BindManager();
            if (manager == null || manager.State != GameState.Lobby)
            {
                CloseImmediate();
                return;
            }

            Refresh();
            SetVisible(true);
        }

        public void Close()
        {
            SetVisible(false);
        }

        public void BuildForTests()
        {
            BuildIfNeeded();
            Refresh();
            CloseImmediate();
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

        void HandleProfileChanged()
        {
            Refresh();
        }

        void HandleBackRequested()
        {
            if (Time.unscaledTime < purchaseLockedUntil)
                return;

            LobbyScreenNavigator navigator =
                LobbyScreenNavigator.Instance != null
                    ? LobbyScreenNavigator.Instance
                    : FindFirstObjectByType<LobbyScreenNavigator>();
            if (navigator != null && navigator.ReturnToLobby())
                return;
            Close();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null) return;

            Transform stale = transform.Find("PermanentGrowthCanvas");
            if (stale != null)
            {
                if (Application.isPlaying)
                    Destroy(stale.gameObject);
                else
                    DestroyImmediate(stale.gameObject);
            }

            var root = new GameObject(
                "PermanentGrowthCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            rootCanvas = root.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = CanvasSortingOrder;
            rootCanvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;

            rootGroup = root.GetComponent<CanvasGroup>();
            ScreenRoot = CreateRect(
                "ScreenRoot",
                root.transform,
                HiddenScreenPosition,
                new Vector2(1080f, ReferenceHeight));

            Image background = CreateStretchImage(
                "OpaqueHanjiBackground",
                ScreenRoot,
                InkPalette.Paper);
            background.sprite =
                LoadPermanentGrowthSprite("pg_hanji_background");
            background.color = background.sprite != null
                ? Color.white
                : InkPalette.Paper;
            background.raycastTarget = true;
            BuildFullScreenInkWash(ScreenRoot);

            safeAreaRoot = CreateStretchRect("SafeAreaRoot", ScreenRoot);
            contentPanel = CreateRect(
                "PermanentGrowthScreen",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(980f, 1760f));

            BuildHeader(contentPanel);
            BuildThreeBranchTree(contentPanel);
            BuildSelectedDetailPanel(contentPanel);

            Text footer = CreateText(
                "PermanentHint",
                contentPanel,
                "노드를 눌러 효과를 확인하고 · 아래에서 강화",
                30,
                new Vector2(0f, -842f),
                new Vector2(820f, 54f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(footer, 0.12f);

            ApplySafeArea();
            SelectInitialNode();
            Refresh();
        }

        void BuildHeader(Transform panel)
        {
            Text title = CreateText(
                "Title",
                panel,
                "영구 성장",
                72,
                new Vector2(0f, 798f),
                new Vector2(520f, 92f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableWeight(title, 0.22f);

            Text subtitle = CreateText(
                "Subtitle",
                panel,
                "세 갈래 먹가지를 길러 모든 도전에 힘을 남깁니다",
                36,
                new Vector2(0f, 726f),
                new Vector2(780f, 56f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(subtitle, 0.12f);

            Sprite balanceSprite =
                LoadPermanentGrowthSprite("pg_currency_badge");
            Image balanceBrush = CreateImage(
                "CurrencyBrush",
                panel,
                balanceSprite ?? InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 650f),
                new Vector2(470f, 82f),
                balanceSprite != null ? Color.white : InkPalette.Ink);
            if (balanceSprite != null)
                balanceBrush.type = Image.Type.Sliced;
            Image balanceDrop = CreateImage(
                "CurrencyDrop",
                balanceBrush.transform,
                LoadPermanentGrowthSprite("pg_ink_drop") ??
                LoadIcon(PermanentGrowthType.InkCapacity),
                new Vector2(-172f, 0f),
                new Vector2(44f, 44f),
                InkPalette.Paper);
            balanceDrop.preserveAspect = true;
            balanceText = CreateText(
                "Balance",
                balanceBrush.transform,
                "보유 먹빛 0",
                40,
                new Vector2(22f, 0f),
                new Vector2(360f, 62f),
                InkPalette.Paper,
                FontStyle.Bold);

            BackButton = CreateBrushButton(
                "BackButton",
                panel,
                "로비",
                new Vector2(-389f, 798f),
                new Vector2(190f, 120f),
                34);
            BackButton.onClick.AddListener(HandleBackRequested);
        }

        void BuildFullScreenInkWash(Transform parent)
        {
            Color ink = InkPalette.Ink;
            Image leftWash = CreateImage(
                "LeftInkWash",
                parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-430f, 210f),
                new Vector2(560f, 1320f),
                new Color(ink.r, ink.g, ink.b, 0.035f));
            leftWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -8f);
            Image rightWash = CreateImage(
                "RightInkWash",
                parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(455f, -390f),
                new Vector2(500f, 1080f),
                new Color(ink.r, ink.g, ink.b, 0.022f));
            rightWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 11f);
        }

        void BuildThreeBranchTree(Transform panel)
        {
            Image crownWash = CreateImage(
                "TreeCrownWash",
                panel,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 65f),
                new Vector2(920f, 980f),
                WithAlpha(InkPalette.Ink, 0.025f));
            crownWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -5f);

            Sprite rootSprite =
                LoadPermanentGrowthSprite("pg_root_emblem");
            Image treeRoot = CreateImage(
                "InkTreeRoot",
                panel,
                rootSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, -350f),
                new Vector2(104f, 104f),
                rootSprite != null ? Color.white : InkPalette.Ink);
            treeRoot.preserveAspect = rootSprite != null;
            CreateText(
                "InkTreeRootLabel",
                panel,
                "먹빛의 뿌리",
                28,
                new Vector2(0f, -418f),
                new Vector2(220f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold);

            var connectorByType =
                new Dictionary<PermanentGrowthType, Image>();
            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches
                         .OrderBy(item => item.DisplayOrder))
            {
                float x = BranchX(branch.Branch);
                branchHeaders.Add(CreateBranchHeader(panel, branch, x));
                List<PermanentGrowthDefinition> definitions =
                    PermanentGrowthCatalog.All
                        .Where(item => item.Branch == branch.Branch)
                        .OrderBy(item => item.BranchOrder)
                        .ToList();

                Vector2 previous = new(0f, -350f);
                for (int i = 0; i < definitions.Count; i++)
                {
                    PermanentGrowthDefinition definition = definitions[i];
                    Vector2 position = NodePosition(definition);
                    Image line = CreateInkLine(
                        $"GrowthPath_{definition.Type}",
                        panel,
                        previous,
                        position);
                    connectorByType[definition.Type] = line;
                    previous = position;
                }
            }

            for (int i = 0; i < PermanentGrowthCatalog.All.Count; i++)
            {
                int slot = i;
                PermanentGrowthDefinition definition =
                    PermanentGrowthCatalog.All[i];
                GrowthNodeView node = CreateGrowthNode(
                    panel,
                    definition,
                    connectorByType[definition.Type]);
                node.Button.onClick.AddListener(() => SelectGrowth(slot));
                nodes.Add(node);
            }
        }

        RectTransform CreateBranchHeader(
            Transform parent,
            PermanentGrowthBranchMetadata branch,
            float x)
        {
            RectTransform root = CreateRect(
                $"GrowthBranchHeader_{branch.Branch}",
                parent,
                new Vector2(x, 525f),
                new Vector2(270f, 108f));
            Image brush = CreateImage(
                "Brush",
                root,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 16f),
                new Vector2(252f, 68f),
                WithAlpha(InkPalette.Ink, 0.9f));
            CreateText(
                "BranchTitle",
                brush.transform,
                branch.DisplayName,
                36,
                Vector2.zero,
                new Vector2(222f, 54f),
                InkPalette.Paper,
                FontStyle.Bold);
            CreateText(
                "BranchSummary",
                root,
                CompactBranchSummary(branch.Branch),
                30,
                new Vector2(0f, -40f),
                new Vector2(268f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold);
            return root;
        }

        GrowthNodeView CreateGrowthNode(
            Transform parent,
            PermanentGrowthDefinition definition,
            Image incomingLine)
        {
            bool capstone = definition.IsCapstone;
            Vector2 position = NodePosition(definition);
            Vector2 touchSize = capstone
                ? new Vector2(260f, 158f)
                : new Vector2(250f, 140f);
            RectTransform root = CreateRect(
                $"GrowthNode_{definition.Type}",
                parent,
                position,
                touchSize);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            hit.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, hit, addInkFeedback: false);

            float surfaceSize = capstone ? 116f : 88f;
            Image ring = CreateImage(
                "SelectionRing",
                root,
                LoadPermanentGrowthSprite("pg_selected_ring"),
                new Vector2(0f, 16f),
                new Vector2(
                    capstone ? 142f : 108f,
                    capstone ? 142f : 108f),
                TransparentColor(InkPalette.Gold));
            ring.preserveAspect = true;

            Image surface = CreateImage(
                "NodeSurface",
                root,
                LoadPermanentGrowthSprite(
                    capstone ? "pg_node_bloom_mask" : "pg_node_bud") ??
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 16f),
                new Vector2(surfaceSize, surfaceSize),
                InkPalette.Paper2);
            surface.preserveAspect = true;

            Image icon = CreateImage(
                "Icon",
                root,
                LoadIcon(definition.Type),
                new Vector2(0f, 16f),
                new Vector2(
                    capstone ? 72f : 54f,
                    capstone ? 72f : 54f),
                Color.white);
            icon.preserveAspect = true;

            Image completion = CreateImage(
                "CompletionMark",
                root,
                LoadPermanentGrowthSprite("pg_node_bloom_mask"),
                new Vector2(38f, 48f),
                new Vector2(34f, 34f),
                TransparentColor(InkPalette.Gold));
            completion.preserveAspect = true;

            Text name = CreateText(
                "NodeName",
                root,
                definition.Name,
                capstone ? 36 : 34,
                new Vector2(0f, capstone ? -64f : -52f),
                new Vector2(258f, 44f),
                InkPalette.TextDark,
                FontStyle.Bold);
            Text level = CreateText(
                "NodeLevel",
                root,
                capstone ? "최종 패시브" : "Lv. 0",
                capstone ? 29 : 28,
                new Vector2(0f, capstone ? -96f : -83f),
                new Vector2(250f, 38f),
                ReadableMutedColor(),
                FontStyle.Bold);

            return new GrowthNodeView
            {
                Definition = definition,
                Root = root,
                IncomingLine = incomingLine,
                Surface = surface,
                Ring = ring,
                Icon = icon,
                CompletionMark = completion,
                Button = button,
                Name = name,
                Level = level,
            };
        }

        void BuildSelectedDetailPanel(Transform parent)
        {
            Sprite cardSprite =
                LoadPermanentGrowthSprite("pg_hanji_card");
            Image panel = CreateImage(
                "SelectedGrowthDetail",
                parent,
                cardSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, -650f),
                new Vector2(920f, 330f),
                cardSprite != null ? Color.white : InkPalette.Paper2);
            panel.preserveAspect = false;
            if (cardSprite != null)
                panel.type = Image.Type.Sliced;

            detailIcon = CreateImage(
                "DetailIcon",
                panel.transform,
                null,
                new Vector2(-395f, 62f),
                new Vector2(96f, 96f),
                Color.white);
            detailIcon.preserveAspect = true;
            detailBranchText = CreateText(
                "DetailBranch",
                panel.transform,
                "먹 운용 계보",
                28,
                new Vector2(-180f, 116f),
                new Vector2(300f, 36f),
                ReadableMutedColor(),
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailNameText = CreateText(
                "DetailName",
                panel.transform,
                "먹그릇",
                46,
                new Vector2(-180f, 72f),
                new Vector2(300f, 56f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailLevelText = CreateText(
                "DetailLevel",
                panel.transform,
                "Lv. 0 / 6",
                31,
                new Vector2(-180f, 24f),
                new Vector2(300f, 40f),
                ReadableMutedColor(),
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailDescriptionText = CreateText(
                "DetailDescription",
                panel.transform,
                "기본 능력이 자랍니다",
                34,
                new Vector2(170f, 82f),
                new Vector2(420f, 86f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            CreateImage(
                "DetailDivider",
                panel.transform,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 4f),
                new Vector2(820f, 4f),
                WithAlpha(InkPalette.Ink, 0.16f));

            detailCurrentText = CreateText(
                "CurrentEffect",
                panel.transform,
                "현재 효과",
                32,
                new Vector2(-142f, -28f),
                new Vector2(560f, 40f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailNextText = CreateText(
                "NextEffect",
                panel.transform,
                "다음 단계",
                32,
                new Vector2(-142f, -68f),
                new Vector2(560f, 40f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailLockText = CreateText(
                "Requirement",
                panel.transform,
                string.Empty,
                30,
                new Vector2(-142f, -108f),
                new Vector2(560f, 38f),
                ReadableMutedColor(),
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            detailCostIcon = CreateImage(
                "DetailCostIcon",
                panel.transform,
                LoadPermanentGrowthSprite("pg_ink_drop") ??
                LoadIcon(PermanentGrowthType.InkCapacity),
                new Vector2(-383f, -140f),
                new Vector2(30f, 30f),
                InkPalette.Ink);
            detailCostIcon.preserveAspect = true;
            detailCostText = CreateText(
                "DetailCost",
                panel.transform,
                "먹빛 0",
                32,
                new Vector2(-257f, -140f),
                new Vector2(210f, 38f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            PurchaseButton = CreateBrushButton(
                "EnhanceButton",
                panel.transform,
                "강화하기",
                new Vector2(322f, -104f),
                new Vector2(250f, 112f),
                34);
            purchaseButtonText =
                PurchaseButton.GetComponentInChildren<Text>(true);
            PurchaseButton.onClick.AddListener(HandleSelectedPurchase);
        }

        void SelectInitialNode()
        {
            int firstAvailable = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Definition.Type ==
                    PermanentGrowthType.InkCapacity)
                {
                    selectedSlot = i;
                    return;
                }
                if (firstAvailable < 0 &&
                    PermanentGrowthProfile.MeetsRequirements(
                        nodes[i].Definition.Type))
                    firstAvailable = i;
            }
            selectedSlot = Mathf.Max(0, firstAvailable);
        }

        void SelectGrowth(int slot)
        {
            if (slot < 0 || slot >= nodes.Count ||
                purchaseInProgress ||
                Time.unscaledTime < purchaseLockedUntil)
                return;
            selectedSlot = slot;
            Refresh();
        }

        void HandleSelectedPurchase()
        {
            HandlePurchase(selectedSlot);
        }

#if UNITY_EDITOR
        public void SelectGrowthForTests(int slot)
        {
            SelectGrowth(slot);
        }
#endif

        void HandlePurchase(int slot)
        {
            if (slot < 0 || slot >= nodes.Count ||
                purchaseInProgress ||
                Time.unscaledTime < purchaseLockedUntil)
                return;

            GrowthNodeView node = nodes[slot];
            int previousLevel =
                PermanentGrowthProfile.GetLevel(node.Definition.Type);
            if (!TryPurchaseWithoutReentry(node.Definition.Type))
            {
                Refresh();
                return;
            }

            int purchasedLevel =
                PermanentGrowthProfile.GetLevel(node.Definition.Type);
            bool firstUnlock =
                previousLevel == 0 && purchasedLevel == 1;
            purchaseLockedUntil =
                Time.unscaledTime +
                (firstUnlock
                    ? GrowthUnlockPresentation.SequenceDuration
                    : GrowthUnlockPresentation.UpgradeSequenceDuration);
            Refresh();
            if (firstUnlock)
            {
                InkUiFeedbackController.PlayGrowthUnlock(
                    node.Definition.Name,
                    node.Icon.sprite);
            }
            else
            {
                InkUiFeedbackController.PlayGrowthUpgrade(
                    node.Definition.Name,
                    node.Icon.sprite,
                    purchasedLevel);
            }
        }

        bool TryPurchaseWithoutReentry(PermanentGrowthType type)
        {
            purchaseInProgress = true;
            try
            {
                return PermanentGrowthProfile.TryPurchase(type);
            }
            finally
            {
                purchaseInProgress = false;
            }
        }

        void Refresh()
        {
            if (balanceText == null) return;
            balanceText.text = $"보유 먹빛 {PermanentGrowthProfile.Currency}";

            for (int i = 0; i < nodes.Count; i++)
            {
                GrowthNodeView node = nodes[i];
                PermanentGrowthDefinition definition = node.Definition;
                int level =
                    PermanentGrowthProfile.GetLevel(definition.Type);
                bool maxed = level >= definition.MaxLevel;
                bool requirementsMet =
                    PermanentGrowthProfile.MeetsRequirements(definition.Type);
                bool selected = i == selectedSlot;

                node.Name.text = definition.Name;
                node.Level.text = definition.IsCapstone
                    ? maxed ? "패시브 장착" : "최종 패시브"
                    : $"Lv. {level} / {definition.MaxLevel}";
                node.Icon.sprite = LoadIcon(definition.Type);
                node.Icon.color = node.Icon.sprite != null
                    ? new Color(1f, 1f, 1f, requirementsMet ? 1f : 0.6f)
                    : WithAlpha(InkPalette.Ink, requirementsMet ? 1f : 0.58f);
                node.Name.color = requirementsMet || level > 0
                    ? InkPalette.TextDark
                    : WithAlpha(InkPalette.TextMuted, 0.84f);
                node.Level.color = maxed
                    ? InkPalette.Gold
                    : ReadableMutedColor();
                node.Surface.color = maxed
                    ? WithAlpha(InkPalette.Gold, 0.92f)
                    : level > 0
                        ? WithAlpha(InkPalette.Paper, 1f)
                        : requirementsMet
                            ? WithAlpha(InkPalette.Paper2, 1f)
                            : WithAlpha(InkPalette.Paper2, 0.62f);
                node.Ring.color = selected
                    ? WithAlpha(InkPalette.Gold, 0.95f)
                    : maxed
                        ? WithAlpha(InkPalette.Gold, 0.62f)
                        : level > 0
                            ? WithAlpha(InkPalette.Ink, 0.72f)
                            : TransparentColor(InkPalette.Gold);
                node.CompletionMark.color = maxed
                    ? InkPalette.Gold
                    : TransparentColor(InkPalette.Gold);
                node.IncomingLine.color = maxed
                    ? WithAlpha(InkPalette.Gold, 0.82f)
                    : level > 0
                        ? WithAlpha(InkPalette.Ink, 0.78f)
                        : requirementsMet
                            ? WithAlpha(InkPalette.Ink, 0.35f)
                            : WithAlpha(InkPalette.Ink, 0.2f);
                node.Button.interactable = true;
            }

            RefreshSelectedDetail();
        }

        void RefreshSelectedDetail()
        {
            if (nodes.Count == 0 ||
                selectedSlot < 0 ||
                selectedSlot >= nodes.Count ||
                detailNameText == null)
                return;

            GrowthNodeView node = nodes[selectedSlot];
            PermanentGrowthDefinition definition = node.Definition;
            PermanentGrowthBranchMetadata branch =
                PermanentGrowthCatalog.GetBranch(definition.Branch);
            int level = PermanentGrowthProfile.GetLevel(definition.Type);
            bool maxed = level >= definition.MaxLevel;
            bool requirementsMet =
                PermanentGrowthProfile.MeetsRequirements(definition.Type);
            int cost = definition.GetCost(level);
            bool hasEnoughCurrency =
                PermanentGrowthProfile.Currency >= cost;

            detailIcon.sprite = LoadIcon(definition.Type);
            detailIcon.color = detailIcon.sprite != null
                ? Color.white
                : InkPalette.Ink;
            detailBranchText.text =
                $"{branch.DisplayName ?? "영구 성장"} 계보" +
                (definition.IsCapstone ? " · 완성 특성" : string.Empty);
            detailNameText.text = definition.Name;
            detailLevelText.text = definition.IsCapstone
                ? maxed ? "장착 완료" : "최종 패시브 · 1회 해금"
                : $"Lv. {level} / {definition.MaxLevel}";
            detailDescriptionText.text = definition.Description;
            detailCurrentText.text = definition.IsCapstone
                ? maxed ? "현재  패시브가 모든 도전에 적용됩니다"
                        : "현재  아직 장착되지 않았습니다"
                : $"현재  {FormatEffect(definition, level)}";
            detailNextText.text = maxed
                ? "다음  최고 단계 완성"
                : definition.IsCapstone
                    ? $"해금  {definition.EffectUnit}"
                    : $"다음  {FormatEffect(definition, level + 1)}";

            string lockReason = maxed
                ? string.Empty
                : PermanentGrowthProfile.GetLockReason(definition.Type);
            detailLockText.text = maxed
                ? "이 계보의 힘이 완성되었습니다"
                : requirementsMet
                    ? "선행 조건 완료"
                    : lockReason;
            detailLockText.color = requirementsMet || maxed
                ? ReadableMutedColor()
                : InkPalette.Red;

            detailCostText.text = maxed
                ? "완성"
                : $"먹빛 {cost}" +
                  (hasEnoughCurrency ? string.Empty : " · 부족");
            detailCostText.color =
                !maxed && requirementsMet && !hasEnoughCurrency
                    ? InkPalette.Red
                    : InkPalette.TextDark;
            detailCostIcon.color =
                !maxed && requirementsMet && !hasEnoughCurrency
                    ? InkPalette.Red
                    : maxed
                        ? InkPalette.Gold
                        : InkPalette.Ink;

            if (purchaseButtonText != null)
            {
                purchaseButtonText.text = maxed
                    ? "완성"
                    : requirementsMet
                        ? "강화하기"
                        : "선행 노드 필요";
            }
            PurchaseButton.interactable =
                !maxed && requirementsMet && hasEnoughCurrency;
        }

        static string FormatEffect(
            PermanentGrowthDefinition definition,
            int level)
        {
            float value = definition.GetDisplayValueAtLevel(level);
            string sign = definition.ReducesValue ? "-" : "+";
            string suffix = definition.ValueKind switch
            {
                PermanentGrowthValueKind.Percent => "%",
                PermanentGrowthValueKind.Seconds => "초",
                _ => string.Empty,
            };
            return $"{definition.EffectUnit} {sign}{FormatNumber(value)}{suffix}";
        }

        static string FormatNumber(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        static Vector2 NodePosition(PermanentGrowthDefinition definition)
        {
            float x = BranchX(definition.Branch);
            if (definition.IsCapstone)
                return new Vector2(x, 328f);

            float y = definition.Branch == PermanentGrowthBranch.InkHandling
                ? -190f + definition.BranchOrder * 145f
                : -170f + definition.BranchOrder * 205f;
            return new Vector2(x, y);
        }

        static float BranchX(PermanentGrowthBranch branch)
        {
            return branch switch
            {
                PermanentGrowthBranch.Survival => -305f,
                PermanentGrowthBranch.Leap => 0f,
                PermanentGrowthBranch.InkHandling => 305f,
                _ => 0f,
            };
        }

        static string CompactBranchSummary(PermanentGrowthBranch branch)
        {
            return branch switch
            {
                PermanentGrowthBranch.Survival => "체력 · 피격 · 부활",
                PermanentGrowthBranch.Leap => "준비 · 점프 · 도약",
                PermanentGrowthBranch.InkHandling => "먹량 · 회복 · 발판",
                _ => "영구 성장",
            };
        }

        Image CreateInkLine(
            string objectName,
            Transform parent,
            Vector2 start,
            Vector2 end)
        {
            Vector2 delta = end - start;
            Image line = CreateImage(
                objectName,
                parent,
                InkUiTextureFactory.CreateBrushSprite(),
                (start + end) * 0.5f,
                new Vector2(delta.magnitude, 12f),
                WithAlpha(InkPalette.Ink, 0.12f));
            line.rectTransform.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return line;
        }

        Sprite LoadIcon(PermanentGrowthType type)
        {
            string permanentPath = type switch
            {
                PermanentGrowthType.InkCapacity => "pg_icon_capacity",
                PermanentGrowthType.InkRecovery => "pg_icon_recovery",
                PermanentGrowthType.PlatformLifetime => "pg_icon_platform",
                PermanentGrowthType.JumpCharge => "pg_icon_jump",
                PermanentGrowthType.Vitality => string.Empty,
                PermanentGrowthType.DamageGrace => string.Empty,
                PermanentGrowthType.LastBreath => "pg_root_emblem",
                PermanentGrowthType.JumpPower => "pg_icon_jump",
                PermanentGrowthType.DrawnPlatformLeap => "pg_icon_platform",
                PermanentGrowthType.StrokeGuard => "pg_icon_platform",
                _ => string.Empty,
            };
            Sprite permanentSprite =
                LoadPermanentGrowthSprite(permanentPath);
            if (permanentSprite != null)
                return permanentSprite;

            string fallbackPath = type switch
            {
                PermanentGrowthType.InkCapacity =>
                    "MukJump/UI/Growth/growth_ink_capacity",
                PermanentGrowthType.InkRecovery =>
                    "MukJump/UI/Growth/growth_ink_regen",
                PermanentGrowthType.PlatformLifetime =>
                    "MukJump/UI/Growth/growth_platform",
                PermanentGrowthType.JumpCharge or
                PermanentGrowthType.JumpPower =>
                    "MukJump/UI/Growth/growth_jump",
                PermanentGrowthType.Vitality or
                PermanentGrowthType.LastBreath =>
                    "MukJump/UI/Growth/growth_vitality",
                PermanentGrowthType.DamageGrace or
                PermanentGrowthType.StrokeGuard =>
                    "MukJump/UI/Growth/growth_guard",
                PermanentGrowthType.DrawnPlatformLeap =>
                    "MukJump/UI/Growth/growth_platform",
                _ => string.Empty,
            };
            return string.IsNullOrEmpty(fallbackPath)
                ? null
                : LoadSpriteResource(fallbackPath);
        }

        Sprite LoadPermanentGrowthSprite(string fileName)
        {
            return string.IsNullOrEmpty(fileName)
                ? null
                : LoadSpriteResource(ArtResourceRoot + fileName);
        }

        Sprite LoadSpriteResource(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (spriteCache.TryGetValue(path, out Sprite cached))
                return cached;
            if (missingSpritePaths.Contains(path))
                return null;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                sprite = sprites != null && sprites.Length > 0
                    ? sprites[0]
                    : null;
            }

            if (sprite != null)
                spriteCache[path] = sprite;
            else
                missingSpritePaths.Add(path);
            return sprite;
        }

        public void SetNavigationPresentation(bool visible, bool interactive)
        {
            if (!visible && rootGroup == null)
                return;
            if (visible)
                BuildIfNeeded();
            if (rootGroup == null || ScreenRoot == null) return;
            if (visible)
            {
                BindManager();
                if (manager == null || manager.State != GameState.Lobby)
                {
                    visible = false;
                    interactive = false;
                }
                else
                {
                    Refresh();
                }
            }

            ScreenRoot.anchoredPosition = visible
                ? Vector2.zero
                : HiddenScreenPosition;
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible && interactive;
            rootGroup.blocksRaycasts = visible && interactive;
            if (rootCanvas != null)
                rootCanvas.enabled = visible;
            ApplySafeArea();
        }

        void SetVisible(bool visible)
        {
            SetNavigationPresentation(visible, visible);
        }

        void CloseImmediate()
        {
            SetVisible(false);
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
            if (contentPanel != null && safe.width > 0f && safe.height > 0f)
            {
                float logicalSafeWidth =
                    safe.width * ReferenceHeight / Screen.height;
                float logicalSafeHeight =
                    safe.height * ReferenceHeight / Screen.height;
                float contentScale = Mathf.Min(
                    1f,
                    logicalSafeWidth / 980f,
                    logicalSafeHeight / 1760f);
                contentPanel.localScale =
                    Vector3.one * Mathf.Max(0.01f, contentScale);
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastSafeArea = safe;
        }

        static RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
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
            RectTransform rect = go.GetComponent<RectTransform>();
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
            RectTransform rect =
                CreateRect(objectName, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
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
            RectTransform rect = CreateStretchRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
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
            FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect =
                CreateRect(objectName, parent, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = color;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            InkUiStyle.ApplyReadableText(
                text,
                PermanentGrowthTypography.Resolve(
                    objectName,
                    fontSize),
                alignment,
                strong: true);
            PermanentGrowthTypography.ApplyLayout(text, objectName);
            return text;
        }

        Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            Image brush = CreateImage(
                objectName,
                parent,
                null,
                position,
                size,
                InkPalette.Ink);
            brush.raycastTarget = true;
            Button button = brush.gameObject.AddComponent<Button>();
            Text text = CreateText(
                "Label",
                brush.transform,
                label,
                fontSize,
                Vector2.zero,
                size - new Vector2(36f, 14f),
                InkPalette.TextLight,
                FontStyle.Bold);
            InkUiStyle.ConfigureActionButton(button, brush, text);
            return button;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        static Color TransparentColor(Color color)
        {
            return WithAlpha(color, 0f);
        }

        static Color ReadableMutedColor()
        {
            Color muted = InkPalette.TextMuted;
            muted.a = 0.92f;
            return muted;
        }

        static void AddReadableWeight(Text text, float alpha)
        {
            if (text == null) return;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            Color ink = InkPalette.Ink;
            shadow.effectColor = new Color(
                ink.r,
                ink.g,
                ink.b,
                alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }
    }
}
