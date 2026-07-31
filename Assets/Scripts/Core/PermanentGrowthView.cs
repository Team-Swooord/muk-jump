using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비의 영구 성장 전용 화면.
    /// 생존·도약·먹 운용 세 계보를 하나의 드래그형 먹나무로 표시한다.
    /// 선택한 노드 곁에는 비용·잠금 상태와 강화 동작만 간결하게 보여준다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;
        const float ReferenceHeight = 1920f;
        const string ArtResourceRoot = "MukJump/UI/PermanentGrowth/";
        static readonly Vector2 TreeViewportPosition = new(0f, -120f);
        static readonly Vector2 TreeViewportSize = new(980f, 1440f);
        static readonly Vector2 TreeCanvasSize = new(3400f, 3200f);
        static readonly Vector2 TreeRootPosition = new(0f, -1420f);
        static readonly string[] BranchPieceNames =
        {
            "pg_branch_piece_01",
            "pg_branch_piece_02",
            "pg_branch_piece_03",
            "pg_branch_piece_04",
            "pg_branch_piece_05",
            "pg_branch_piece_06",
        };
        static readonly Vector2 HiddenScreenPosition =
            new(0f, ReferenceHeight);

        sealed class GrowthNodeView
        {
            public PermanentGrowthNodeDefinition NodeDefinition;
            public RectTransform Root;
            public List<Image> IncomingLines = new();
            public List<Image> BranchArts = new();
            public Image Surface;
            public Image FruitGlow;
            public Image Fruit;
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
        ScrollRect treeScrollRect;
        RectTransform selectedActionRoot;
        Text balanceText;
        Text selectedActionStatusText;
        Text purchaseButtonText;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        float purchaseLockedUntil;
        bool purchaseInProgress;
        bool purchaseUiLocked;
        int selectedSlot;

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public Button BackButton { get; private set; }
        public Button PurchaseButton { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public RectTransform TreeViewport { get; private set; }
        public RectTransform TreeCanvas { get; private set; }
        public ScrollRect TreeScrollRect => treeScrollRect;
        public bool IsDedicatedScreen => ScreenRoot != null;
        public int CreatedRowCount => branchHeaders.Count;
        public int CreatedNodeCount => nodes.Count;
        public string BalanceLabel => balanceText != null
            ? balanceText.text
            : string.Empty;
        public PermanentGrowthType SelectedGrowthType =>
            nodes.Count > 0 && selectedSlot >= 0 && selectedSlot < nodes.Count
                ? nodes[selectedSlot].NodeDefinition.Type
                : PermanentGrowthType.InkCapacity;
        public string SelectedNodeId =>
            nodes.Count > 0 && selectedSlot >= 0 && selectedSlot < nodes.Count
                ? nodes[selectedSlot].NodeDefinition.Id
                : string.Empty;

        void OnEnable()
        {
            BindManager();
            PermanentGrowthProfile.Changed += HandleProfileChanged;
        }

        void OnDisable()
        {
            PermanentGrowthProfile.Changed -= HandleProfileChanged;
            UnbindManager();
            InkUiFeedbackController.CancelGrowthPresentation();
            CloseImmediate();
        }

        void Update()
        {
            if (manager == null)
                BindManager();
            if (manager != null && manager.State != GameState.Lobby && IsOpen)
                Close();
            if (purchaseUiLocked &&
                Time.unscaledTime >= purchaseLockedUntil)
            {
                purchaseUiLocked = false;
                Refresh();
            }
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
            BuildTreeViewport(contentPanel);

            ApplySafeArea();
            SelectInitialNode();
            Refresh();
        }

        void BuildTreeViewport(Transform panel)
        {
            TreeViewport = CreateRect(
                "TreeViewport",
                panel,
                TreeViewportPosition,
                TreeViewportSize);
            Image dragSurface = TreeViewport.gameObject.AddComponent<Image>();
            dragSurface.color = new Color(1f, 1f, 1f, 0.001f);
            dragSurface.raycastTarget = true;
            RectMask2D mask = TreeViewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(8f, 8f, 8f, 8f);

            treeScrollRect = TreeViewport.gameObject.AddComponent<ScrollRect>();
            treeScrollRect.viewport = TreeViewport;
            treeScrollRect.horizontal = true;
            treeScrollRect.vertical = true;
            treeScrollRect.movementType = ScrollRect.MovementType.Clamped;
            treeScrollRect.inertia = true;
            treeScrollRect.decelerationRate = 0.14f;
            treeScrollRect.scrollSensitivity = 70f;

            TreeCanvas = CreateRect(
                "TreeCanvas",
                TreeViewport,
                Vector2.zero,
                TreeCanvasSize);
            treeScrollRect.content = TreeCanvas;
            BuildThreeBranchTree(TreeCanvas);
            BuildSelectedNodeAction(TreeCanvas);

            ResetTreeViewportToRoot();
        }

        void ResetTreeViewportToRoot()
        {
            if (TreeCanvas == null) return;
            TreeCanvas.anchoredPosition = Vector2.zero;
            if (treeScrollRect == null) return;
            treeScrollRect.StopMovement();
            treeScrollRect.horizontalNormalizedPosition = 0.5f;
            treeScrollRect.verticalNormalizedPosition = 0f;
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
                new Vector2(0f, -40f),
                new Vector2(3000f, 2800f),
                WithAlpha(InkPalette.Ink, 0.022f));
            crownWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -5f);

            Sprite trunkSprite =
                LoadPermanentGrowthSprite("pg_tree_trunk");
            Image treeTrunk = CreateImage(
                "InkTreeTrunk",
                panel,
                trunkSprite ?? InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, -120f),
                new Vector2(900f, 2600f),
                trunkSprite != null
                    ? new Color(1f, 1f, 1f, 0.84f)
                    : WithAlpha(InkPalette.Ink, 0.22f));
            treeTrunk.preserveAspect = trunkSprite != null;

            CreateDecorativeTreeBranch(
                panel,
                "TreeSprigLowerLeft",
                new Vector2(-40f, -1160f),
                new Vector2(-720f, -1260f),
                0.2f,
                0);
            CreateDecorativeTreeBranch(
                panel,
                "TreeSprigLowerRight",
                new Vector2(40f, -1160f),
                new Vector2(720f, -1260f),
                0.18f,
                1);
            CreateDecorativeTreeBranch(
                panel,
                "TreeSprigUpperLeft",
                new Vector2(-40f, 280f),
                new Vector2(-760f, 880f),
                0.16f,
                2);
            CreateDecorativeTreeBranch(
                panel,
                "TreeSprigUpperRight",
                new Vector2(40f, 320f),
                new Vector2(760f, 920f),
                0.14f,
                3);

            Sprite rootSprite =
                LoadPermanentGrowthSprite("pg_root_emblem");
            Image treeRoot = CreateImage(
                "InkTreeRoot",
                panel,
                rootSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                TreeRootPosition,
                new Vector2(132f, 132f),
                rootSprite != null ? Color.white : InkPalette.Ink);
            treeRoot.preserveAspect = rootSprite != null;
            CreateText(
                "InkTreeRootLabel",
                panel,
                "먹빛의 뿌리",
                32,
                TreeRootPosition + new Vector2(0f, -88f),
                new Vector2(260f, 46f),
                InkPalette.TextDark,
                FontStyle.Bold);

            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches
                         .OrderBy(item => item.DisplayOrder))
            {
                branchHeaders.Add(
                    CreateBranchHeader(
                        panel,
                        branch,
                        BranchHeaderPosition(branch.Branch)));
            }

            var incomingLinesById =
                new Dictionary<string, List<Image>>(StringComparer.Ordinal);
            var branchArtsById =
                new Dictionary<string, List<Image>>(StringComparer.Ordinal);
            for (int i = 0; i < PermanentGrowthCatalog.Nodes.Count; i++)
            {
                PermanentGrowthNodeDefinition definition =
                    PermanentGrowthCatalog.Nodes[i];
                var incomingLines = new List<Image>();
                var branchArts = new List<Image>();
                incomingLinesById[definition.Id] = incomingLines;
                branchArtsById[definition.Id] = branchArts;

                Vector2 end = NodePosition(definition);
                string childName = SanitizeNodeId(definition.Id);
                if (definition.ParentIds.Count == 0)
                {
                    string rootEdgeId = $"root->{definition.Id}";
                    branchArts.Add(CreateTreeBranchArt(
                        panel,
                        $"TreeRootBranchArt_{childName}",
                        definition,
                        TreeRootPosition,
                        end,
                        rootEdgeId));
                    incomingLines.Add(CreateInkLine(
                        $"GrowthRootPath_{childName}",
                        panel,
                        TreeRootPosition,
                        end));
                    continue;
                }

                for (int parentIndex = 0;
                     parentIndex < definition.ParentIds.Count;
                     parentIndex++)
                {
                    string parentId = definition.ParentIds[parentIndex];
                    PermanentGrowthNodeDefinition parentDefinition =
                        PermanentGrowthCatalog.GetNode(parentId);
                    if (parentDefinition == null)
                        continue;

                    string parentName = SanitizeNodeId(parentId);
                    string edgeId = $"{parentId}->{definition.Id}";
                    Vector2 start = NodePosition(parentDefinition);
                    Image branchArt = CreateTreeBranchArt(
                        panel,
                        $"TreeBranchArt_{childName}_From_{parentName}",
                        definition,
                        start,
                        end,
                        edgeId);
                    branchArts.Add(branchArt);
                    incomingLines.Add(CreateInkLine(
                        $"GrowthPath_{childName}_From_{parentName}",
                        panel,
                        start,
                        end));
                }
            }

            for (int i = 0; i < PermanentGrowthCatalog.Nodes.Count; i++)
            {
                int slot = i;
                PermanentGrowthNodeDefinition definition =
                    PermanentGrowthCatalog.Nodes[i];
                GrowthNodeView node = CreateGrowthNode(
                    panel,
                    definition,
                    incomingLinesById[definition.Id],
                    branchArtsById[definition.Id]);
                node.Button.onClick.AddListener(() => SelectGrowth(slot));
                nodes.Add(node);
            }
        }

        RectTransform CreateBranchHeader(
            Transform parent,
            PermanentGrowthBranchMetadata branch,
            Vector2 position)
        {
            RectTransform root = CreateRect(
                $"GrowthBranchHeader_{branch.Branch}",
                parent,
                position,
                new Vector2(330f, 118f));
            Image brush = CreateImage(
                "Brush",
                root,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 16f),
                new Vector2(310f, 74f),
                WithAlpha(InkPalette.Ink, 0.9f));
            CreateText(
                "BranchTitle",
                brush.transform,
                branch.DisplayName,
                40,
                Vector2.zero,
                new Vector2(280f, 58f),
                InkPalette.Paper,
                FontStyle.Bold);
            CreateText(
                "BranchSummary",
                root,
                CompactBranchSummary(branch.Branch),
                30,
                new Vector2(0f, -44f),
                new Vector2(326f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold);
            return root;
        }

        GrowthNodeView CreateGrowthNode(
            Transform parent,
            PermanentGrowthNodeDefinition definition,
            List<Image> incomingLines,
            List<Image> branchArts)
        {
            bool capstone = definition.IsCapstone;
            Vector2 position = NodePosition(definition);
            Vector2 touchSize = capstone
                ? new Vector2(240f, 260f)
                : new Vector2(188f, 218f);
            RectTransform root = CreateRect(
                $"GrowthNode_{SanitizeNodeId(definition.Id)}",
                parent,
                position,
                touchSize);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            hit.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, hit, addInkFeedback: false);

            float nodeCenterY = 30f;
            float surfaceSize = capstone ? 164f : 124f;
            Image ring = CreateImage(
                "SelectionRing",
                root,
                LoadPermanentGrowthSprite("pg_selected_ring"),
                new Vector2(0f, nodeCenterY),
                new Vector2(
                    capstone ? 210f : 166f,
                    capstone ? 210f : 166f),
                TransparentColor(InkPalette.Gold));
            ring.preserveAspect = true;

            Sprite fruitSprite =
                LoadPermanentGrowthSprite("pg_node_bloom_mask") ??
                InkUiTextureFactory.CreateBlobSprite();
            Image fruitGlow = CreateImage(
                "FruitGlow",
                root,
                fruitSprite,
                new Vector2(0f, nodeCenterY),
                new Vector2(
                    capstone ? 232f : 188f,
                    capstone ? 232f : 188f),
                TransparentColor(InkPalette.Red));
            fruitGlow.preserveAspect = true;

            Image surface = CreateImage(
                "NodeSurface",
                root,
                LoadPermanentGrowthSprite("pg_node_bud") ??
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, nodeCenterY),
                new Vector2(surfaceSize, surfaceSize),
                InkPalette.Paper2);
            surface.preserveAspect = true;

            Image fruit = CreateImage(
                "Fruit",
                root,
                fruitSprite,
                new Vector2(0f, nodeCenterY),
                new Vector2(surfaceSize, surfaceSize),
                TransparentColor(InkPalette.Red));
            fruit.preserveAspect = true;

            Image icon = CreateImage(
                "Icon",
                root,
                LoadIcon(definition.Type),
                new Vector2(0f, nodeCenterY),
                new Vector2(
                    capstone ? 94f : 72f,
                    capstone ? 94f : 72f),
                Color.white);
            icon.preserveAspect = true;

            Image completion = CreateImage(
                "CompletionMark",
                root,
                LoadPermanentGrowthSprite("pg_node_bloom_mask"),
                new Vector2(
                    capstone ? 72f : 56f,
                    capstone ? 92f : 78f),
                new Vector2(
                    capstone ? 42f : 36f,
                    capstone ? 42f : 36f),
                TransparentColor(InkPalette.Gold));
            completion.preserveAspect = true;

            Text name = CreateText(
                "NodeName",
                root,
                definition.Name,
                capstone ? 36 : 34,
                new Vector2(0f, capstone ? -88f : -62f),
                new Vector2(290f, 46f),
                InkPalette.TextDark,
                FontStyle.Bold);
            Text level = CreateText(
                "NodeLevel",
                root,
                definition.IsCapstone
                    ? "최종 열매"
                    : $"{definition.Rank}단계",
                capstone ? 29 : 28,
                new Vector2(0f, capstone ? -128f : -101f),
                new Vector2(286f, 38f),
                ReadableMutedColor(),
                FontStyle.Bold);

            return new GrowthNodeView
            {
                NodeDefinition = definition,
                Root = root,
                IncomingLines = incomingLines,
                BranchArts = branchArts,
                Surface = surface,
                FruitGlow = fruitGlow,
                Fruit = fruit,
                Ring = ring,
                Icon = icon,
                CompletionMark = completion,
                Button = button,
                Name = name,
                Level = level,
            };
        }

        void BuildSelectedNodeAction(Transform parent)
        {
            selectedActionRoot = CreateRect(
                "SelectedGrowthAction",
                parent,
                Vector2.zero,
                new Vector2(310f, 176f));

            PurchaseButton = CreateBrushButton(
                "EnhanceButton",
                selectedActionRoot,
                "강화하기",
                new Vector2(0f, 30f),
                new Vector2(230f, 104f),
                32);
            purchaseButtonText =
                PurchaseButton.GetComponentInChildren<Text>(true);
            PurchaseButton.onClick.AddListener(HandleSelectedPurchase);

            selectedActionStatusText = CreateText(
                "ActionStatus",
                selectedActionRoot,
                "먹빛 0",
                27,
                new Vector2(0f, -54f),
                new Vector2(306f, 58f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(selectedActionStatusText, 0.08f);
            selectedActionRoot.SetAsLastSibling();
        }

        void SelectInitialNode()
        {
            int firstAvailable = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].NodeDefinition.Type ==
                        PermanentGrowthType.InkCapacity &&
                    nodes[i].NodeDefinition.Rank == 1)
                {
                    selectedSlot = i;
                    return;
                }
                if (firstAvailable < 0 &&
                    PermanentGrowthProfile.MeetsNodeRequirements(
                        nodes[i].NodeDefinition))
                    firstAvailable = i;
            }
            selectedSlot = Mathf.Max(0, firstAvailable);
        }

        void SelectGrowth(int slot)
        {
            if (slot < 0 || slot >= nodes.Count ||
                purchaseInProgress ||
                purchaseUiLocked ||
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
                purchaseUiLocked ||
                Time.unscaledTime < purchaseLockedUntil)
                return;

            GrowthNodeView node = nodes[slot];
            if (!TryPurchaseWithoutReentry(node.NodeDefinition))
            {
                Refresh();
                return;
            }

            Vector2 fruitScreenPosition =
                RectTransformUtility.WorldToScreenPoint(
                    null,
                    node.Fruit.rectTransform.position);
            purchaseLockedUntil =
                Time.unscaledTime +
                GrowthUnlockPresentation.SequenceDuration;
            purchaseUiLocked = true;
            Refresh();
            InkUiFeedbackController.PlayGrowthUnlock(
                node.NodeDefinition.Name,
                node.Icon.sprite,
                fruitScreenPosition,
                node.Fruit.sprite);
        }

        bool TryPurchaseWithoutReentry(
            PermanentGrowthNodeDefinition definition)
        {
            purchaseInProgress = true;
            try
            {
                return PermanentGrowthProfile.TryPurchaseNode(definition);
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
                PermanentGrowthNodeDefinition definition =
                    node.NodeDefinition;
                bool unlocked =
                    PermanentGrowthProfile.IsNodeUnlocked(definition);
                bool requirementsMet =
                    PermanentGrowthProfile.MeetsNodeRequirements(definition);
                bool selected = i == selectedSlot;

                node.Name.text = definition.Name;
                node.Level.text = unlocked
                    ? "열매 개화"
                    : definition.IsCapstone
                        ? "최종 열매"
                        : $"{definition.Rank}단계";
                node.Icon.sprite = LoadIcon(definition.Type);
                node.Icon.color = node.Icon.sprite != null
                    ? new Color(1f, 1f, 1f, requirementsMet ? 1f : 0.6f)
                    : WithAlpha(InkPalette.Ink, requirementsMet ? 1f : 0.58f);
                node.Name.color = requirementsMet || unlocked
                    ? InkPalette.TextDark
                    : WithAlpha(InkPalette.TextMuted, 0.84f);
                node.Level.color = unlocked
                    ? InkPalette.Gold
                    : ReadableMutedColor();
                node.Surface.color = unlocked
                    ? TransparentColor(InkPalette.Paper2)
                    : requirementsMet
                        ? WithAlpha(InkPalette.Paper2, 1f)
                        : WithAlpha(InkPalette.Paper2, 0.62f);
                node.Fruit.color = unlocked
                    ? WithAlpha(InkPalette.Red, 1f)
                    : TransparentColor(InkPalette.Red);
                node.FruitGlow.color = unlocked
                    ? WithAlpha(
                        InkPalette.Red,
                        selected ? 0.28f : 0.18f)
                    : TransparentColor(InkPalette.Red);
                node.Ring.color = selected
                    ? WithAlpha(InkPalette.Gold, 0.95f)
                    : unlocked
                        ? WithAlpha(InkPalette.Gold, 0.62f)
                        : TransparentColor(InkPalette.Gold);
                node.CompletionMark.color = unlocked
                    ? InkPalette.Gold
                    : TransparentColor(InkPalette.Gold);
                Color lineColor = unlocked
                    ? WithAlpha(InkPalette.Gold, 0.82f)
                    : requirementsMet
                        ? WithAlpha(InkPalette.Ink, 0.35f)
                        : WithAlpha(InkPalette.Ink, 0.2f);
                for (int lineIndex = 0;
                     lineIndex < node.IncomingLines.Count;
                     lineIndex++)
                    if (node.IncomingLines[lineIndex] != null)
                        node.IncomingLines[lineIndex].color = lineColor;

                Color branchColor = unlocked
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : requirementsMet
                        ? new Color(1f, 1f, 1f, 0.68f)
                        : new Color(1f, 1f, 1f, 0.48f);
                for (int artIndex = 0;
                     artIndex < node.BranchArts.Count;
                     artIndex++)
                {
                    if (node.BranchArts[artIndex] != null)
                        node.BranchArts[artIndex].color = branchColor;
                }
                node.Button.interactable = !purchaseUiLocked;
            }

            if (BackButton != null)
                BackButton.interactable = !purchaseUiLocked;
            UpdateTreeInteraction();
            RefreshSelectedAction();
        }

        void UpdateTreeInteraction()
        {
            if (treeScrollRect == null) return;
            treeScrollRect.enabled = !purchaseUiLocked;
            if (purchaseUiLocked)
                treeScrollRect.StopMovement();
        }

        void RefreshSelectedAction()
        {
            if (nodes.Count == 0 ||
                selectedSlot < 0 ||
                selectedSlot >= nodes.Count ||
                selectedActionRoot == null ||
                selectedActionStatusText == null)
                return;

            GrowthNodeView node = nodes[selectedSlot];
            PermanentGrowthNodeDefinition definition =
                node.NodeDefinition;
            bool unlocked =
                PermanentGrowthProfile.IsNodeUnlocked(definition);
            bool requirementsMet =
                PermanentGrowthProfile.MeetsNodeRequirements(definition);
            int cost = definition.Cost;
            bool hasEnoughCurrency =
                PermanentGrowthProfile.Currency >= cost;

            selectedActionStatusText.text = unlocked
                ? "열매 개화 완료"
                : requirementsMet
                    ? $"먹빛 {cost}" +
                      (hasEnoughCurrency ? string.Empty : " · 부족")
                    : "선행 열매 필요";
            selectedActionStatusText.color =
                !unlocked && requirementsMet && !hasEnoughCurrency
                    ? InkPalette.Red
                    : requirementsMet || unlocked
                        ? ReadableMutedColor()
                        : InkPalette.Red;

            if (purchaseButtonText != null)
            {
                purchaseButtonText.text = unlocked
                    ? "개화 완료"
                    : requirementsMet
                        ? "강화하기"
                        : "잠김";
            }
            PurchaseButton.interactable =
                !purchaseUiLocked &&
                !unlocked &&
                requirementsMet &&
                hasEnoughCurrency;
            selectedActionRoot.anchoredPosition =
                FindSelectedActionPosition(node);
            selectedActionRoot.SetAsLastSibling();
        }

        Vector2 FindSelectedActionPosition(GrowthNodeView selectedNode)
        {
            Vector2 origin = selectedNode.Root.anchoredPosition;
            float preferredDirection = origin.x < 0f ? 1f : -1f;
            Vector2[] offsets =
            {
                new(preferredDirection * 310f, 0f),
                new(-preferredDirection * 310f, 0f),
                new(0f, 235f),
                new(0f, -235f),
                new(preferredDirection * 285f, 185f),
                new(-preferredDirection * 285f, 185f),
                new(preferredDirection * 285f, -185f),
                new(-preferredDirection * 285f, -185f),
                new(preferredDirection * 410f, 120f),
                new(-preferredDirection * 410f, 120f),
                new(preferredDirection * 410f, -120f),
                new(-preferredDirection * 410f, -120f),
            };

            Vector2 actionSize = selectedActionRoot.sizeDelta;
            Rect visibleBounds = VisibleTreeCanvasBounds();
            var candidates = new List<Vector2>(192);
            for (int i = 0; i < offsets.Length; i++)
                candidates.Add(
                    ClampActionCenter(
                        origin + offsets[i],
                        actionSize,
                        visibleBounds));

            Vector2 halfAction = actionSize * 0.5f;
            float minimumX = visibleBounds.xMin + halfAction.x;
            float maximumX = visibleBounds.xMax - halfAction.x;
            float minimumY = visibleBounds.yMin + halfAction.y;
            float maximumY = visibleBounds.yMax - halfAction.y;
            const float SearchStep = 88f;
            for (float y = minimumY; y <= maximumY; y += SearchStep)
                for (float x = minimumX; x <= maximumX; x += SearchStep)
                    candidates.Add(new Vector2(x, y));
            candidates.Add(new Vector2(maximumX, maximumY));
            candidates.Add(new Vector2(minimumX, maximumY));
            candidates.Add(new Vector2(maximumX, minimumY));
            candidates.Add(new Vector2(minimumX, minimumY));

            Vector2 best = candidates[0];
            float bestScore = float.PositiveInfinity;
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                Vector2 candidate = candidates[candidateIndex];
                Rect actionRect = CenteredRect(candidate, actionSize);
                float score =
                    (candidate - origin).sqrMagnitude * 0.002f;
                score += RectOverflow(actionRect, visibleBounds) * 1000000f;

                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                {
                    GrowthNodeView other = nodes[nodeIndex];
                    Rect occupied = CenteredRect(
                        other.Root.anchoredPosition,
                        other.Root.sizeDelta + new Vector2(36f, 36f));
                    score += RectOverlapArea(actionRect, occupied) * 1000f;
                }

                for (int headerIndex = 0;
                     headerIndex < branchHeaders.Count;
                     headerIndex++)
                {
                    RectTransform header = branchHeaders[headerIndex];
                    Rect occupied = CenteredRect(
                        header.anchoredPosition,
                        header.sizeDelta + new Vector2(24f, 24f));
                    score += RectOverlapArea(actionRect, occupied) * 500f;
                }

                if (score >= bestScore)
                    continue;
                bestScore = score;
                best = candidate;
            }

            return best;
        }

        Rect VisibleTreeCanvasBounds()
        {
            if (TreeViewport == null || TreeCanvas == null)
                return new Rect(
                    -TreeCanvasSize.x * 0.5f,
                    -TreeCanvasSize.y * 0.5f,
                    TreeCanvasSize.x,
                    TreeCanvasSize.y);

            var corners = new Vector3[4];
            TreeViewport.GetWorldCorners(corners);
            Vector3 bottomLeft = TreeCanvas.InverseTransformPoint(corners[0]);
            Vector3 topRight = TreeCanvas.InverseTransformPoint(corners[2]);
            const float SafeInset = 22f;
            return Rect.MinMaxRect(
                bottomLeft.x + SafeInset,
                bottomLeft.y + SafeInset,
                topRight.x - SafeInset,
                topRight.y - SafeInset);
        }

        static Vector2 ClampActionCenter(
            Vector2 center,
            Vector2 size,
            Rect bounds)
        {
            Vector2 half = size * 0.5f;
            float minimumX = bounds.xMin + half.x;
            float maximumX = bounds.xMax - half.x;
            float minimumY = bounds.yMin + half.y;
            float maximumY = bounds.yMax - half.y;
            return new Vector2(
                minimumX <= maximumX
                    ? Mathf.Clamp(center.x, minimumX, maximumX)
                    : bounds.center.x,
                minimumY <= maximumY
                    ? Mathf.Clamp(center.y, minimumY, maximumY)
                    : bounds.center.y);
        }

        static Rect CenteredRect(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        static float RectOverflow(Rect inner, Rect outer)
        {
            return Mathf.Max(0f, outer.xMin - inner.xMin) +
                   Mathf.Max(0f, inner.xMax - outer.xMax) +
                   Mathf.Max(0f, outer.yMin - inner.yMin) +
                   Mathf.Max(0f, inner.yMax - outer.yMax);
        }

        static float RectOverlapArea(Rect a, Rect b)
        {
            float width =
                Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) -
                               Mathf.Max(a.xMin, b.xMin));
            float height =
                Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) -
                               Mathf.Max(a.yMin, b.yMin));
            return width * height;
        }

        static Vector2 NodePosition(
            PermanentGrowthNodeDefinition definition)
        {
            return definition != null
                ? new Vector2(definition.LayoutX, definition.LayoutY)
                : Vector2.zero;
        }

        static Vector2 BranchHeaderPosition(PermanentGrowthBranch branch)
        {
            PermanentGrowthNodeDefinition last =
                PermanentGrowthCatalog.Nodes
                    .Where(item => item.Branch == branch)
                    .OrderBy(item => item.LayoutY)
                    .LastOrDefault();
            if (last == null)
                return Vector2.zero;
            Vector2 lastPosition = NodePosition(last);
            return new Vector2(
                lastPosition.x,
                lastPosition.y + 210f);
        }

        static string CompactBranchSummary(PermanentGrowthBranch branch)
        {
            return branch switch
            {
                PermanentGrowthBranch.Survival => "체력 · 피격 · 분신",
                PermanentGrowthBranch.Leap => "준비 · 점프 · 도약",
                PermanentGrowthBranch.InkHandling => "먹량 · 회복 · 발판",
                _ => "영구 성장",
            };
        }

        Image CreateTreeBranchArt(
            Transform parent,
            string objectName,
            PermanentGrowthNodeDefinition definition,
            Vector2 start,
            Vector2 end,
            string stableEdgeId)
        {
            Vector2 delta = end - start;
            Sprite branchSprite = LoadBranchPiece(
                BranchPieceVariant(delta, stableEdgeId));
            if (branchSprite == null)
                return null;

            float thickness = definition.IsCapstone ? 124f : 104f;
            Image branch = CreateImage(
                objectName,
                parent,
                branchSprite,
                (start + end) * 0.5f,
                new Vector2(delta.magnitude * 1.12f, thickness),
                new Color(1f, 1f, 1f, 0.55f));
            branch.preserveAspect = false;
            branch.rectTransform.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return branch;
        }

        static int BranchPieceVariant(Vector2 delta, string stableEdgeId)
        {
            float horizontal = Mathf.Abs(delta.x);
            float vertical = Mathf.Abs(delta.y);
            int shapeOffset;
            if (horizontal <= vertical * 0.34f)
                shapeOffset = 0;
            else
                shapeOffset = delta.x < 0f ? 2 : 4;
            return shapeOffset + (StableHash(stableEdgeId) & 1);
        }

        static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (value != null)
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                return (int)(hash & 0x7fffffffu);
            }
        }

        static string SanitizeNodeId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "unknown";
            char[] characters = id.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsLetterOrDigit(characters[i]))
                    characters[i] = '_';
            return new string(characters);
        }

        void CreateDecorativeTreeBranch(
            Transform parent,
            string objectName,
            Vector2 start,
            Vector2 end,
            float alpha,
            int variantIndex)
        {
            Sprite branchSprite = LoadBranchPiece(variantIndex);
            if (branchSprite == null)
                return;

            Vector2 delta = end - start;
            Image branch = CreateImage(
                objectName,
                parent,
                branchSprite,
                (start + end) * 0.5f,
                new Vector2(delta.magnitude * 1.08f, 92f),
                new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            branch.preserveAspect = false;
            branch.rectTransform.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        Sprite LoadBranchPiece(int variantIndex)
        {
            // 새 조각을 같은 이름으로 Resources에 추가하면 별도 코드 수정 없이
            // 계보 순서에 맞춰 교대 사용하고, 미제작 상태에서는 기존 가지로 폴백한다.
            int safeIndex =
                Mathf.Abs(variantIndex) % BranchPieceNames.Length;
            return LoadPermanentGrowthSprite(BranchPieceNames[safeIndex]) ??
                   LoadPermanentGrowthSprite("pg_branch");
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
                new Vector2(delta.magnitude, 16f),
                WithAlpha(InkPalette.Ink, 0.16f));
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
                PermanentGrowthType.CloneSpawnGrace => string.Empty,
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
                PermanentGrowthType.CloneSpawnGrace or
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
            if (!visible)
            {
                purchaseUiLocked = false;
                purchaseLockedUntil = 0f;
                InkUiFeedbackController.CancelGrowthPresentation();
            }
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
            if (!visible || !interactive)
                treeScrollRect?.StopMovement();
            UpdateTreeInteraction();
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
