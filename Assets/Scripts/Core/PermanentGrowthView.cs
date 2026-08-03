using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비의 영구 성장 전용 화면.
    /// 생존·도약·먹 운용 세 계보를 하나의 드래그형 먹나무로 표시한다.
    /// 노드를 누르면 화면 중앙 상세 팝업에서 핵심 수치·설명·비용을 확인하고 강화한다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        const string ArtResourceRoot = "MukJump/UI/PermanentGrowth/";
        const float TreeCanvasZoom = 0.66f;
        const float TreeBackgroundOpacity = 0.42f;
        const float TreeBranchOpacity = 1f;
        const float BranchVisibleEndpointOverlap = 18f;
        const float LeapBranchHorizontalOffset = 300f;
        const float LeapLeftKeystoneExtraOffset = 140f;
        static readonly Vector2 TreeCanvasSize = new(3600f, 3200f);
        static readonly Vector2 TreeBackgroundSize = new(2200f, 3060f);
        static readonly Vector2 TreeBackgroundPosition = Vector2.zero;
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
        // 각 PNG에서 alpha 16 이상인 실제 먹선의 가로 범위다. 투명 여백이
        // 조각마다 달라 같은 RectTransform 폭을 쓰면 03·06이 중간에서 끊긴다.
        static readonly Vector2[] BranchPieceVisibleHorizontalRanges =
        {
            new(0.064f, 0.966f),
            new(0.059f, 0.961f),
            new(0.135f, 0.865f),
            new(0.063f, 0.928f),
            new(0.067f, 0.944f),
            new(0.134f, 0.874f),
        };
        static readonly Vector2 HiddenScreenPosition =
            new(0f, ReferenceHeight);

        sealed class GrowthNodeView
        {
            public PermanentGrowthNodeDefinition NodeDefinition;
            public RectTransform Root;
            public List<Image> IncomingLines = new();
            public List<Image> BranchArts = new();
            public Image Contrast;
            public Image Surface;
            public Image FruitGlow;
            public Image Fruit;
            public Image Ring;
            public Image EquippedRing;
            public Image Icon;
            public Image CompletionMark;
            public Button Button;
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
        Image selectedActionDimmer;
        Text balanceText;
        Text selectedActionBranchText;
        Text selectedActionNameText;
        Text selectedActionDescriptionText;
        Text selectedActionEffectSummaryText;
        Image selectedActionIcon;
        Image selectedActionCostPlate;
        Image selectedActionCostIcon;
        Text selectedActionCostText;
        Text purchaseButtonText;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        float purchaseLockedUntil;
        bool purchaseInProgress;
        bool purchaseUiLocked;
        bool nodePopupOpen;
        string pendingKeystoneId = string.Empty;
        int selectedSlot;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RectTransform debugMenuPanel;
#endif

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public Button BackButton { get; private set; }
        public Button PurchaseButton { get; private set; }
        public Button NodePopupCloseButton { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public RectTransform TreeViewport { get; private set; }
        public RectTransform TreeCanvas { get; private set; }
        public ScrollRect TreeScrollRect => treeScrollRect;
        public bool IsDedicatedScreen => ScreenRoot != null;
        public bool IsNodePopupOpen =>
            nodePopupOpen && selectedActionRoot != null &&
            selectedActionRoot.gameObject.activeSelf;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Button DebugMenuButton { get; private set; }
        public Button DebugResetButton { get; private set; }
        public Button DebugCurrencyButton { get; private set; }
#endif

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
            pendingKeystoneId = string.Empty;
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
            CloseNodePopup();
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
            pendingKeystoneId = string.Empty;
            Refresh();
        }

        void HandleBackRequested()
        {
            if (Time.unscaledTime < purchaseLockedUntil)
                return;

            if (IsNodePopupOpen)
            {
                CloseNodePopup();
                return;
            }

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
            // 높이를 기준으로 스케일하는 세로 UI에서도 19.5:9·20:9의 실제 논리 폭을
            // 그대로 사용한다. 고정 1080 폭이면 좌우 HUD와 계보 뿌리가 화면 밖으로 잘린다.
            ScreenRoot = CreateStretchRect("ScreenRoot", root.transform);
            ScreenRoot.anchoredPosition = HiddenScreenPosition;

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

            // 지도는 노치용 Safe Area에 넣지 않는다. 배경과 함께 실제 화면의
            // 네 변까지 사용하고, 고정 HUD만 아래 SafeAreaRoot에서 보호한다.
            RectTransform treeLayerRoot =
                CreateStretchRect("TreeLayerRoot", ScreenRoot);
            safeAreaRoot = CreateStretchRect("SafeAreaRoot", ScreenRoot);
            contentPanel = CreateRect(
                "PermanentGrowthScreen",
                safeAreaRoot,
                Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));

            BuildTreeViewport(treeLayerRoot);
            BuildHeader(contentPanel);
            BuildSelectedNodePopup(contentPanel);

            ApplySafeArea();
            SelectInitialNode();
            Refresh();
        }

        void BuildTreeViewport(Transform panel)
        {
            TreeViewport = CreateStretchRect("TreeViewport", panel);
            // 성장 지도는 헤더 뒤까지 화면을 사용하되 제목·재화·로비 버튼은
            // 항상 앞에 남긴다. 위쪽 여백도 탐색 영역으로 활용할 수 있다.
            TreeViewport.SetAsFirstSibling();
            Image dragSurface = TreeViewport.gameObject.AddComponent<Image>();
            dragSurface.color = new Color(1f, 1f, 1f, 0.001f);
            dragSurface.raycastTarget = true;
            RectMask2D mask = TreeViewport.gameObject.AddComponent<RectMask2D>();
            // 한지 화면 가장자리에 별도의 프레임 여백을 만들지 않는다.
            // 지도는 Safe Area 밖까지 그리고, 고정 HUD만 별도로 보호한다.
            mask.padding = Vector4.zero;

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
            // 전체 나무를 한 번에 더 많이 볼 수 있게 살짝 줌아웃한다.
            // 열매 터치 영역은 축소 후에도 모바일 최소 크기보다 충분히 크다.
            TreeCanvas.localScale = Vector3.one * CalculateTreeZoom(
                Screen.width,
                Screen.height);
            treeScrollRect.content = TreeCanvas;
            BuildThreeBranchTree(TreeCanvas);

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
            RectTransform balanceHud = CreateRect(
                "CurrencyHud",
                panel,
                new Vector2(370f, 800f),
                new Vector2(190f, 84f));
            Image balanceDrop = CreateImage(
                "CurrencyDrop",
                balanceHud,
                LoadPermanentGrowthSprite("pg_ink_drop") ??
                LoadIcon(PermanentGrowthType.InkCapacity),
                new Vector2(-50f, 0f),
                new Vector2(54f, 54f),
                Color.white);
            balanceDrop.preserveAspect = true;
            balanceText = CreateText(
                "Balance",
                balanceHud,
                "0",
                44,
                new Vector2(34f, 0f),
                new Vector2(112f, 66f),
                InkPalette.TextDark,
                FontStyle.Bold);
            balanceText.alignment = TextAnchor.MiddleLeft;

            BackButton = CreateBrushButton(
                "BackButton",
                panel,
                "로비",
                new Vector2(-405f, 800f),
                new Vector2(150f, 120f),
                32);
            BackButton.onClick.AddListener(HandleBackRequested);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDebugMenu(panel);
#endif
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
            // 열매 노드는 연결선 위가 아니라 한 그루의 큰 먹나무 위에 맺힌다.
            // 가장자리까지 투명 여백을 둔 v3를 우선 사용해 흰 사각형이나 잘린
            // 가지 끝이 보이지 않게 한다. 이전 에셋은 호환 폴백으로만 남긴다.
            Sprite treeBackgroundSprite =
                LoadPermanentGrowthSprite("pg_tree_background_v3") ??
                LoadPermanentGrowthSprite("pg_tree_background_v2");
            if (treeBackgroundSprite != null)
            {
                Image treeBackground = CreateImage(
                    "InkTreeBackground",
                    panel,
                    treeBackgroundSprite,
                    TreeBackgroundPosition,
                    TreeBackgroundSize,
                    new Color(1f, 1f, 1f, TreeBackgroundOpacity));
                treeBackground.preserveAspect = true;
                treeBackground.raycastTarget = false;
            }

            // 전체 나무가 없을 때만 기존 조각으로 형태를 복원한다. 완성 배경 위에
            // 줄기와 장식 가지를 겹치면 서로 다른 알파 경계가 사각형처럼 보인다.
            if (treeBackgroundSprite == null)
            {
                Sprite trunkSprite =
                    LoadPermanentGrowthSprite("pg_tree_trunk");
                Image treeTrunk = CreateImage(
                    "InkTreeTrunk",
                    panel,
                    trunkSprite ?? InkUiTextureFactory.CreateBrushSprite(),
                    new Vector2(0f, -70f),
                    new Vector2(1450f, 2730f),
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
            }

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

            // 계보 표식은 가지와 열매가 모두 배치된 뒤 올려, 중앙 줄기가
            // 글자 위를 덮지 않게 한다.
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
                new Vector2(190f, 72f));
            Image brush = CreateImage(
                "Brush",
                root,
                InkUiTextureFactory.CreateBrushSprite(),
                Vector2.zero,
                new Vector2(180f, 64f),
                InkPalette.Ink);
            CreateText(
                "BranchTitle",
                brush.transform,
                CompactBranchTitle(branch.Branch),
                36,
                Vector2.zero,
                new Vector2(164f, 56f),
                InkPalette.Paper,
                FontStyle.Normal);
            return root;
        }

        GrowthNodeView CreateGrowthNode(
            Transform parent,
            PermanentGrowthNodeDefinition definition,
            List<Image> incomingLines,
            List<Image> branchArts)
        {
            bool capstone = definition.IsKeystone;
            bool rootNode = definition.NodeKind == PermanentGrowthNodeKind.Root;
            Vector2 position = NodePosition(definition);
            Vector2 touchSize = capstone
                ? new Vector2(252f, 276f)
                : rootNode
                    ? new Vector2(216f, 240f)
                    : new Vector2(196f, 224f);
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
            float surfaceSize = capstone
                ? 164f
                : rootNode
                    ? 144f
                    : 124f;
            Image nodeContrast = CreateImage(
                "NodeContrast",
                root,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, nodeCenterY),
                new Vector2(surfaceSize + 32f, surfaceSize + 32f),
                InkPalette.Ink);
            nodeContrast.preserveAspect = true;
            nodeContrast.raycastTarget = false;

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

            Image equippedRing = CreateImage(
                "EquippedRing",
                root,
                LoadPermanentGrowthSprite("pg_selected_ring"),
                new Vector2(0f, nodeCenterY),
                new Vector2(
                    capstone ? 232f : 184f,
                    capstone ? 232f : 184f),
                TransparentColor(InkPalette.Gold));
            equippedRing.preserveAspect = true;

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
                LoadIcon(definition),
                new Vector2(0f, nodeCenterY),
                new Vector2(
                    capstone ? 94f : 72f,
                    capstone ? 94f : 72f),
                Color.white);
            icon.preserveAspect = true;
            ApplyIconVariation(icon, definition);

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

            return new GrowthNodeView
            {
                NodeDefinition = definition,
                Root = root,
                IncomingLines = incomingLines,
                BranchArts = branchArts,
                Contrast = nodeContrast,
                Surface = surface,
                FruitGlow = fruitGlow,
                Fruit = fruit,
                Ring = ring,
                EquippedRing = equippedRing,
                Icon = icon,
                CompletionMark = completion,
                Button = button,
            };
        }

        void BuildSelectedNodePopup(Transform parent)
        {
            selectedActionDimmer = CreateStretchImage(
                "GrowthNodePopupDimmer",
                parent,
                WithAlpha(InkPalette.Ink, 0.48f));
            selectedActionDimmer.raycastTarget = true;
            Button dimmerButton =
                selectedActionDimmer.gameObject.AddComponent<Button>();
            dimmerButton.transition = Selectable.Transition.None;
            dimmerButton.onClick.AddListener(CloseNodePopup);

            selectedActionRoot = CreateRect(
                "SelectedGrowthAction",
                parent,
                new Vector2(0f, -8f),
                new Vector2(820f, 820f));
            Image popupPaper =
                selectedActionRoot.gameObject.AddComponent<Image>();
            popupPaper.sprite =
                LoadPermanentGrowthSprite("pg_hanji_card") ??
                InkUiTextureFactory.CreateBlobSprite();
            popupPaper.color = popupPaper.sprite != null
                ? Color.white
                : InkPalette.Paper2;
            popupPaper.raycastTarget = true;
            if (popupPaper.sprite != null &&
                popupPaper.sprite.border != Vector4.zero)
                popupPaper.type = Image.Type.Sliced;

            Image infoPanel = CreateImage(
                "ActionInfoPanel",
                selectedActionRoot,
                LoadPermanentGrowthSprite("pg_hanji_card") ??
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, 8f),
                new Vector2(700f, 520f),
                WithAlpha(InkPalette.Ink, 0.94f));
            infoPanel.raycastTarget = false;
            if (infoPanel.sprite != null &&
                infoPanel.sprite.border != Vector4.zero)
                infoPanel.type = Image.Type.Sliced;

            Image branchBrush = CreateImage(
                "ActionBranchBrush",
                selectedActionRoot,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(72f, 145f),
                new Vector2(476f, 50f),
                WithAlpha(InkPalette.Gold, 0.32f));
            selectedActionBranchText = CreateText(
                "ActionBranch",
                branchBrush.transform,
                string.Empty,
                26,
                Vector2.zero,
                new Vector2(432f, 44f),
                InkPalette.Paper,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);

            Image iconPlate = CreateImage(
                "ActionIconPlate",
                selectedActionRoot,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-260f, 180f),
                new Vector2(96f, 96f),
                WithAlpha(InkPalette.Paper2, 0.96f));
            iconPlate.preserveAspect = true;

            selectedActionIcon = CreateImage(
                "ActionIcon",
                selectedActionRoot,
                null,
                new Vector2(-260f, 180f),
                new Vector2(68f, 68f),
                Color.white);
            selectedActionIcon.preserveAspect = true;

            selectedActionNameText = CreateText(
                "ActionName",
                selectedActionRoot,
                string.Empty,
                44,
                new Vector2(72f, 196f),
                new Vector2(476f, 60f),
                InkPalette.Paper,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            CreateImage(
                "ActionDivider",
                selectedActionRoot,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 90f),
                new Vector2(604f, 10f),
                WithAlpha(InkPalette.Paper, 0.2f));

            selectedActionEffectSummaryText = CreateText(
                "ActionEffectSummary",
                selectedActionRoot,
                string.Empty,
                34,
                new Vector2(0f, 30f),
                new Vector2(604f, 58f),
                InkPalette.Paper,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            EnableBestFit(selectedActionEffectSummaryText, 30);

            selectedActionDescriptionText = CreateText(
                "ActionDescription",
                selectedActionRoot,
                string.Empty,
                30,
                new Vector2(0f, -78f),
                new Vector2(604f, 132f),
                WithAlpha(InkPalette.Paper, 0.92f),
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            selectedActionDescriptionText.lineSpacing = 1.08f;
            selectedActionDescriptionText.verticalOverflow =
                VerticalWrapMode.Truncate;
            EnableBestFit(selectedActionDescriptionText, 28);

            selectedActionCostPlate = CreateImage(
                "ActionCostPlate",
                selectedActionRoot,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-40f, -184f),
                new Vector2(54f, 54f),
                WithAlpha(InkPalette.Paper2, 0.92f));
            selectedActionCostPlate.preserveAspect = true;

            selectedActionCostIcon = CreateImage(
                "ActionCostIcon",
                selectedActionRoot,
                LoadPermanentGrowthSprite("pg_ink_drop") ??
                LoadIcon(PermanentGrowthType.InkCapacity),
                new Vector2(-40f, -184f),
                new Vector2(34f, 34f),
                Color.white);
            selectedActionCostIcon.preserveAspect = true;
            selectedActionCostText = CreateText(
                "ActionCost",
                selectedActionRoot,
                "0",
                34,
                new Vector2(22f, -184f),
                new Vector2(88f, 54f),
                InkPalette.Paper,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            PurchaseButton = CreateBrushButton(
                "EnhanceButton",
                selectedActionRoot,
                "강화하기",
                new Vector2(0f, -304f),
                new Vector2(344f, 104f),
                36);
            purchaseButtonText =
                PurchaseButton.GetComponentInChildren<Text>(true);
            PurchaseButton.onClick.AddListener(HandleSelectedPurchase);

            NodePopupCloseButton = CreateBrushButton(
                "CloseButton",
                selectedActionRoot,
                "닫기",
                new Vector2(280f, 294f),
                new Vector2(116f, 72f),
                26);
            NodePopupCloseButton.onClick.AddListener(CloseNodePopup);

            selectedActionDimmer.gameObject.SetActive(false);
            selectedActionRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void BuildDebugMenu(Transform parent)
        {
            DebugMenuButton = CreateBrushButton(
                "GrowthDebugMenuButton",
                parent,
                "DEBUG",
                new Vector2(-405f, 672f),
                new Vector2(150f, 82f),
                25);

            Image panelImage = CreateImage(
                "GrowthDebugMenu",
                parent,
                LoadPermanentGrowthSprite("pg_hanji_card") ??
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-245f, 525f),
                new Vector2(470f, 238f),
                Color.white);
            panelImage.raycastTarget = true;
            if (panelImage.sprite != null &&
                panelImage.sprite.border != Vector4.zero)
                panelImage.type = Image.Type.Sliced;
            debugMenuPanel = panelImage.rectTransform;

            CreateText(
                "DebugTitle",
                debugMenuPanel,
                "성장 DEBUG",
                28,
                new Vector2(0f, 66f),
                new Vector2(360f, 48f),
                InkPalette.TextDark,
                FontStyle.Bold);
            DebugResetButton = CreateBrushButton(
                "DebugResetButton",
                debugMenuPanel,
                "노드 초기화",
                new Vector2(-112f, -30f),
                new Vector2(202f, 92f),
                25);
            DebugCurrencyButton = CreateBrushButton(
                "DebugCurrencyButton",
                debugMenuPanel,
                "먹빛 999",
                new Vector2(112f, -30f),
                new Vector2(202f, 92f),
                27);

            DebugMenuButton.onClick.AddListener(ToggleDebugMenu);
            DebugResetButton.onClick.AddListener(HandleDebugReset);
            DebugCurrencyButton.onClick.AddListener(HandleDebugRefill);
            debugMenuPanel.gameObject.SetActive(false);
        }
#endif

        void SelectInitialNode()
        {
            int firstAvailable = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].NodeDefinition.ParentIds.Count == 0)
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
            if (selectedSlot != slot)
                pendingKeystoneId = string.Empty;
            selectedSlot = slot;
            SetNodePopupVisible(true);
            Refresh();
        }

        void HandleSelectedPurchase()
        {
            if (selectedSlot < 0 || selectedSlot >= nodes.Count)
                return;

            PermanentGrowthNodeDefinition definition =
                nodes[selectedSlot].NodeDefinition;
            if (definition.IsKeystone &&
                PermanentGrowthProfile.IsNodeUnlocked(definition))
            {
                if (PermanentGrowthProfile.IsKeystoneActive(definition.Id))
                {
                    pendingKeystoneId = string.Empty;
                    PermanentGrowthProfile.ClearActiveKeystone(definition.Branch);
                }
                else
                {
                    string activeId = PermanentGrowthProfile.GetActiveKeystoneId(
                        definition.Branch);
                    if (!string.IsNullOrEmpty(activeId) &&
                        !string.Equals(
                            pendingKeystoneId,
                            definition.Id,
                            StringComparison.Ordinal))
                    {
                        pendingKeystoneId = definition.Id;
                        Refresh();
                        return;
                    }

                    pendingKeystoneId = string.Empty;
                    PermanentGrowthProfile.TryEquipKeystone(definition.Id);
                }
                Refresh();
                return;
            }

            HandlePurchase(selectedSlot);
        }

#if UNITY_EDITOR
        public void SelectGrowthForTests(int slot)
        {
            SelectGrowth(slot);
        }

        public void SelectGrowthForTests(string nodeId)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!string.Equals(
                        nodes[i].NodeDefinition.Id,
                        nodeId,
                        StringComparison.Ordinal))
                    continue;
                SelectGrowth(i);
                return;
            }
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
            SetNodePopupVisible(false);
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
            balanceText.text = PermanentGrowthProfile.Currency.ToString();

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
                bool activeKeystone = definition.IsKeystone &&
                    PermanentGrowthProfile.IsKeystoneActive(definition.Id);
                Color pathColor = ResolveGrowthPathColor(definition);

                node.Icon.sprite = LoadIcon(definition);
                ApplyIconVariation(node.Icon, definition);
                node.Icon.color = node.Icon.sprite != null
                    ? new Color(1f, 1f, 1f, requirementsMet ? 1f : 0.6f)
                    : WithAlpha(InkPalette.Ink, requirementsMet ? 1f : 0.58f);
                if (node.Contrast != null)
                {
                    Color contrastColor = definition.Branch ==
                                          PermanentGrowthBranch.Leap &&
                                          definition.Id != "J00"
                        ? Color.Lerp(InkPalette.Ink, pathColor, 0.48f)
                        : InkPalette.Ink;
                    node.Contrast.color = WithAlpha(contrastColor, 0.98f);
                }
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
                node.Ring.color = selected && IsNodePopupOpen
                    ? WithAlpha(InkPalette.Ink, 0.76f)
                    : TransparentColor(InkPalette.Ink);
                node.EquippedRing.color = activeKeystone
                    ? WithAlpha(InkPalette.Gold, 0.98f)
                    : TransparentColor(InkPalette.Gold);
                node.CompletionMark.color = activeKeystone
                    ? InkPalette.Gold
                    : TransparentColor(InkPalette.Gold);
                bool isColoredLeapPath = definition.Branch ==
                                         PermanentGrowthBranch.Leap &&
                                         definition.Id != "J00";
                Color lineBase = isColoredLeapPath
                    ? pathColor
                    : unlocked ? InkPalette.Gold : InkPalette.Ink;
                Color lineColor = WithAlpha(
                    lineBase,
                    unlocked
                        ? isColoredLeapPath ? 0.72f : 0.38f
                        : requirementsMet
                            ? isColoredLeapPath ? 0.34f : 0.11f
                            : isColoredLeapPath ? 0.13f : 0.045f);
                for (int lineIndex = 0;
                     lineIndex < node.IncomingLines.Count;
                     lineIndex++)
                    if (node.IncomingLines[lineIndex] != null)
                        node.IncomingLines[lineIndex].color = lineColor;

                // 가지는 진행 상태가 아니라 나무 자체다. 해금 상태 표현은
                // 열매와 얇은 상태선에만 맡겨 한 그루의 실루엣을 유지한다.
                Color branchColor =
                    new(1f, 1f, 1f, TreeBranchOpacity);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugMenuButton != null)
                DebugMenuButton.interactable = !purchaseUiLocked;
            if (DebugResetButton != null)
                DebugResetButton.interactable = !purchaseUiLocked;
            if (DebugCurrencyButton != null)
                DebugCurrencyButton.interactable = !purchaseUiLocked;
#endif
            UpdateTreeInteraction();
            RefreshSelectedNodePopup();
        }

        static Color ResolveGrowthPathColor(
            PermanentGrowthNodeDefinition definition)
        {
            if (definition == null ||
                definition.Branch != PermanentGrowthBranch.Leap)
                return InkPalette.Ink;

            string id = definition.Id ?? string.Empty;
            if (id.StartsWith("J-A", StringComparison.Ordinal) || id == "J-KA")
                return new Color(0.34f, 0.46f, 0.36f, 1f); // 먹청록: 준비시간
            if (id.StartsWith("J-B", StringComparison.Ordinal) || id == "J-KB")
                return new Color(0.30f, 0.43f, 0.60f, 1f); // 쪽빛: 점프력 주줄기
            if (id.StartsWith("J-C", StringComparison.Ordinal) || id == "J-KC")
                return new Color(0.48f, 0.36f, 0.52f, 1f); // 옅은 자주먹: 높이
            return InkPalette.Ink;
        }

        void UpdateTreeInteraction()
        {
            if (treeScrollRect == null) return;
            bool blocked = purchaseUiLocked || IsNodePopupOpen;
            treeScrollRect.enabled = !blocked;
            if (blocked)
                treeScrollRect.StopMovement();
        }

        void RefreshSelectedNodePopup()
        {
            if (nodes.Count == 0 ||
                selectedSlot < 0 ||
                selectedSlot >= nodes.Count ||
                selectedActionRoot == null ||
                selectedActionBranchText == null ||
                selectedActionNameText == null ||
                selectedActionDescriptionText == null ||
                selectedActionEffectSummaryText == null ||
                selectedActionIcon == null ||
                selectedActionCostPlate == null ||
                selectedActionCostIcon == null ||
                selectedActionCostText == null ||
                PurchaseButton == null)
                return;

            GrowthNodeView node = nodes[selectedSlot];
            PermanentGrowthNodeDefinition definition =
                node.NodeDefinition;
            bool unlocked =
                PermanentGrowthProfile.IsNodeUnlocked(definition);
            bool activeKeystone = definition.IsKeystone &&
                PermanentGrowthProfile.IsKeystoneActive(definition.Id);
            string activeKeystoneId = definition.IsKeystone
                ? PermanentGrowthProfile.GetActiveKeystoneId(definition.Branch)
                : string.Empty;
            bool replacingKeystone = definition.IsKeystone &&
                unlocked &&
                !activeKeystone &&
                !string.IsNullOrEmpty(activeKeystoneId);
            bool replacementPending = replacingKeystone &&
                string.Equals(
                    pendingKeystoneId,
                    definition.Id,
                    StringComparison.Ordinal);
            bool requirementsMet =
                PermanentGrowthProfile.MeetsNodeRequirements(definition);
            int cost = definition.Cost;
            bool hasEnoughCurrency =
                PermanentGrowthProfile.Currency >= cost;
            PermanentGrowthBranchMetadata branch =
                PermanentGrowthCatalog.GetBranch(definition.Branch);

            selectedActionBranchText.text =
                $"{branch.DisplayName} · {NodeKindLabel(definition.NodeKind)}";
            selectedActionNameText.text = definition.DisplayName;
            selectedActionDescriptionText.text = definition.Description;
            selectedActionIcon.sprite = LoadIcon(definition);
            ApplyIconVariation(selectedActionIcon, definition);
            selectedActionIcon.color = selectedActionIcon.sprite != null
                ? Color.white
                : InkPalette.Ink;
            selectedActionEffectSummaryText.text = definition.EffectSummary;
            bool showCost = !unlocked;
            selectedActionCostPlate.gameObject.SetActive(showCost);
            selectedActionCostIcon.gameObject.SetActive(showCost);
            selectedActionCostText.gameObject.SetActive(showCost);
            selectedActionCostText.text = cost.ToString();
            PurchaseButton.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, -304f);

            if (purchaseButtonText != null)
            {
                purchaseButtonText.text = unlocked
                    ? definition.IsKeystone
                        ? activeKeystone
                            ? "장착 해제"
                            : replacementPending
                                ? "교체 확인"
                                : replacingKeystone
                                    ? "교체하기"
                                    : "장착하기"
                        : "적용 중"
                    : requirementsMet
                        ? hasEnoughCurrency
                            ? "강화하기"
                            : "먹빛 부족"
                        : "선행 필요";
            }
            PurchaseButton.interactable =
                !purchaseUiLocked &&
                (unlocked
                    ? definition.IsKeystone
                    : requirementsMet && hasEnoughCurrency);
            NodePopupCloseButton.interactable = !purchaseUiLocked;
        }

        void SetNodePopupVisible(bool visible)
        {
            nodePopupOpen = visible && selectedActionRoot != null;
            if (!nodePopupOpen)
                pendingKeystoneId = string.Empty;
            if (selectedActionDimmer != null)
            {
                selectedActionDimmer.gameObject.SetActive(nodePopupOpen);
                if (nodePopupOpen)
                    selectedActionDimmer.transform.SetAsLastSibling();
            }
            if (selectedActionRoot != null)
            {
                selectedActionRoot.gameObject.SetActive(nodePopupOpen);
                if (nodePopupOpen)
                    selectedActionRoot.SetAsLastSibling();
            }
            UpdateTreeInteraction();
        }

        void CloseNodePopup()
        {
            SetNodePopupVisible(false);
            Refresh();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void ToggleDebugMenu()
        {
            if (debugMenuPanel == null || purchaseUiLocked)
                return;
            bool visible = !debugMenuPanel.gameObject.activeSelf;
            debugMenuPanel.gameObject.SetActive(visible);
            if (visible)
                debugMenuPanel.SetAsLastSibling();
        }

        void HandleDebugReset()
        {
            if (purchaseUiLocked)
                return;
            CloseNodePopup();
            PermanentGrowthProfile.DebugResetProgress();
            SelectInitialNode();
            Refresh();
        }

        void HandleDebugRefill()
        {
            if (purchaseUiLocked)
                return;
            PermanentGrowthProfile.DebugRefillCurrency();
            Refresh();
        }
#endif

        static Vector2 NodePosition(
            PermanentGrowthNodeDefinition definition)
        {
            if (definition == null)
                return Vector2.zero;

            Vector2 fallback =
                new(definition.LayoutX, definition.LayoutY);
            if (definition.Branch != PermanentGrowthBranch.Leap)
                return fallback;

            // 도약 계보는 세로 세 줄처럼 보이지 않도록 뿌리에서 서서히
            // 벌어졌다 다시 모이는 부채꼴로 배치한다. 좌우 폭과 높이에 작은
            // 차이를 둬 나뭇가지답게 보이되, 터치 영역은 겹치지 않는다.
            bool usesFiveStepPaths =
                PermanentGrowthCatalog.GetNode("J-A4") != null;
            Vector2 resolved;
            if (!usesFiveStepPaths)
            {
                resolved = definition.Id switch
                {
                    "J00" => new Vector2(700f, -1080f),
                    "J-A1" => new Vector2(520f, -750f),
                    "J-A2" => new Vector2(400f, -360f),
                    "J-A3" => new Vector2(480f, 30f),
                    "J-KA" => new Vector2(330f, 450f),
                    "J-B1" => new Vector2(780f, -730f),
                    "J-B2" => new Vector2(830f, -340f),
                    "J-B3" => new Vector2(750f, 50f),
                    "J-KB" => new Vector2(830f, 500f),
                    "J-C1" => new Vector2(1060f, -760f),
                    "J-C2" => new Vector2(1190f, -380f),
                    "J-C3" => new Vector2(1110f, 10f),
                    "J-KC" => new Vector2(1280f, 460f),
                    _ => fallback,
                };
            }
            else
            {
                resolved = definition.Id switch
                {
                    "J00" => new Vector2(700f, -1080f),
                    "J-A1" => new Vector2(520f, -810f),
                    "J-A2" => new Vector2(400f, -500f),
                    "J-A3" => new Vector2(470f, -180f),
                    "J-A4" => new Vector2(350f, 120f),
                    "J-A5" => new Vector2(460f, 430f),
                    "J-KA" => new Vector2(330f, 780f),
                    "J-B1" => new Vector2(780f, -790f),
                    "J-B2" => new Vector2(830f, -470f),
                    "J-B3" => new Vector2(750f, -150f),
                    "J-B4" => new Vector2(850f, 160f),
                    "J-B5" => new Vector2(780f, 460f),
                    "J-KB" => new Vector2(830f, 830f),
                    "J-C1" => new Vector2(1060f, -820f),
                    "J-C2" => new Vector2(1190f, -520f),
                    "J-C3" => new Vector2(1110f, -210f),
                    "J-C4" => new Vector2(1240f, 90f),
                    "J-C5" => new Vector2(1150f, 410f),
                    "J-KC" => new Vector2(1280f, 770f),
                    _ => fallback,
                };
            }

            float extraOffset = definition.Id == "J-KA"
                ? LeapLeftKeystoneExtraOffset
                : 0f;
            return resolved +
                new Vector2(LeapBranchHorizontalOffset + extraOffset, 0f);
        }

        static float CalculateTreeZoom(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
                return TreeCanvasZoom;
            float logicalWidth = screenWidth * ReferenceHeight / screenHeight;
            float narrowScreenFit = Mathf.Clamp01(logicalWidth / ReferenceWidth);
            return TreeCanvasZoom * narrowScreenFit;
        }

#if UNITY_EDITOR
        public static float CalculateTreeZoomForTests(
            float screenWidth,
            float screenHeight) =>
            CalculateTreeZoom(screenWidth, screenHeight);
#endif

        static Vector2 BranchHeaderPosition(PermanentGrowthBranch branch)
        {
            // 세 계보는 각각 독립된 뿌리에서 시작한다. 표찰을 실제 입구 가까이에
            // 두어 드래그 중에도 현재 보고 있는 계보를 바로 알 수 있게 한다.
            return branch switch
            {
                PermanentGrowthBranch.Survival =>
                    new Vector2(-700f, -1210f),
                PermanentGrowthBranch.InkHandling =>
                    new Vector2(0f, -1270f),
                PermanentGrowthBranch.Leap =>
                    new Vector2(
                        700f + LeapBranchHorizontalOffset,
                        -1210f),
                _ => Vector2.zero,
            };
        }

        static string NodeKindLabel(PermanentGrowthNodeKind kind)
        {
            return kind switch
            {
                PermanentGrowthNodeKind.Root => "뿌리",
                PermanentGrowthNodeKind.Stat => "성장",
                PermanentGrowthNodeKind.Mechanic => "특성",
                PermanentGrowthNodeKind.Keystone => "비기",
                _ => "열매",
            };
        }

        static string CompactBranchTitle(PermanentGrowthBranch branch)
        {
            return branch switch
            {
                PermanentGrowthBranch.Survival => "생존",
                PermanentGrowthBranch.Leap => "도약",
                PermanentGrowthBranch.InkHandling => "먹 운용",
                _ => string.Empty,
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
            int variantIndex = BranchPieceVariant(delta, stableEdgeId);
            Sprite branchSprite = LoadBranchPiece(variantIndex);
            if (branchSprite == null)
                return null;

            Vector2 visibleRange = BranchPieceVisibleHorizontalRange(
                variantIndex,
                branchSprite);
            float visibleSpan = Mathf.Max(
                0.1f,
                visibleRange.y - visibleRange.x);
            float width =
                (delta.magnitude + BranchVisibleEndpointOverlap * 2f) /
                visibleSpan;
            float spriteAspect = branchSprite.rect.height > 0f
                ? branchSprite.rect.width / branchSprite.rect.height
                : 2f;
            float naturalHeight = width / Mathf.Max(1f, spriteAspect);
            float thickness = Mathf.Clamp(
                naturalHeight,
                definition.IsKeystone ? 176f : 140f,
                definition.IsKeystone ? 220f : 190f);
            Vector2 direction = delta.sqrMagnitude > 0.001f
                ? delta.normalized
                : Vector2.right;
            float centerFromStart =
                width * (0.5f - visibleRange.x) -
                BranchVisibleEndpointOverlap;
            Image branch = CreateImage(
                objectName,
                parent,
                branchSprite,
                start + direction * centerFromStart,
                new Vector2(width, thickness),
                new Color(1f, 1f, 1f, TreeBranchOpacity));
            branch.preserveAspect = false;
            branch.rectTransform.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return branch;
        }

        static Vector2 BranchPieceVisibleHorizontalRange(
            int variantIndex,
            Sprite sprite)
        {
            int safeIndex =
                Mathf.Abs(variantIndex) % BranchPieceNames.Length;
            return sprite != null &&
                   sprite.name == BranchPieceNames[safeIndex]
                ? BranchPieceVisibleHorizontalRanges[safeIndex]
                : new Vector2(0f, 1f);
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
            float width = delta.magnitude * 1.08f;
            float spriteAspect = branchSprite.rect.height > 0f
                ? branchSprite.rect.width / branchSprite.rect.height
                : 2f;
            float thickness = Mathf.Clamp(
                width / Mathf.Max(1f, spriteAspect),
                150f,
                250f);
            Image branch = CreateImage(
                objectName,
                parent,
                branchSprite,
                (start + end) * 0.5f,
                new Vector2(width, thickness),
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
                new Vector2(delta.magnitude, 7f),
                WithAlpha(InkPalette.Ink, 0.05f));
            line.rectTransform.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return line;
        }

        Sprite LoadIcon(PermanentGrowthNodeDefinition definition)
        {
            if (definition == null)
                return null;

            // 추후 같은 IconKey 이름의 전용 그림을 Resources에 넣으면 코드 변경 없이
            // 즉시 사용한다. 아직 전용 그림이 없는 노드는 계보별 수묵 아이콘으로 폴백한다.
            string iconKey = definition.IconKey ?? string.Empty;
            string keyedPath = string.IsNullOrEmpty(iconKey)
                ? string.Empty
                : "pg_icon_" + iconKey.Replace('.', '_');
            return LoadPermanentGrowthSprite(keyedPath) ??
                   LoadIcon(definition.EffectId);
        }

        void ApplyIconVariation(
            Image icon,
            PermanentGrowthNodeDefinition definition)
        {
            if (icon == null || definition == null)
                return;

            int hash = StableHash(definition.IconKey);
            float rotation = ((hash % 5) - 2) * 3.5f;
            float scale = 0.92f + ((hash / 5) % 4) * 0.025f;
            icon.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, rotation);
            icon.rectTransform.localScale =
                new Vector3(scale, scale, 1f);
        }

        Sprite LoadIcon(PermanentGrowthType type)
        {
            string permanentPath = type switch
            {
                PermanentGrowthType.InkCapacity => "pg_icon_capacity",
                PermanentGrowthType.InkRecovery => "pg_icon_recovery",
                PermanentGrowthType.PlatformLifetime or
                PermanentGrowthType.FirstLandingPause or
                PermanentGrowthType.NaturalExpiryRefund or
                PermanentGrowthType.StrokeGuard => "pg_icon_platform",
                PermanentGrowthType.JumpCharge or
                PermanentGrowthType.JumpPower or
                PermanentGrowthType.JumpHeight or
                PermanentGrowthType.SafetyPlatform or
                PermanentGrowthType.DoubleJump or
                PermanentGrowthType.WallCling or
                PermanentGrowthType.DrawnChargeRhythm or
                PermanentGrowthType.ConsecutiveLandingRhythm or
                PermanentGrowthType.ShortPlatformControl or
                PermanentGrowthType.ApexHang or
                PermanentGrowthType.FallControl or
                PermanentGrowthType.LastFallBrake => "pg_icon_jump",
                PermanentGrowthType.Vitality => string.Empty,
                PermanentGrowthType.DamageGrace or
                PermanentGrowthType.HitHorizontalStability or
                PermanentGrowthType.HitReboundControl or
                PermanentGrowthType.StableHit or
                PermanentGrowthType.CloneSpawnGrace or
                PermanentGrowthType.CloneSourceGrace or
                PermanentGrowthType.CloneDeathHeal or
                PermanentGrowthType.CloneBond => string.Empty,
                PermanentGrowthType.LastBreath => "pg_root_emblem",
                PermanentGrowthType.DrawnPlatformLeap => "pg_icon_platform",
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
                PermanentGrowthType.JumpPower or
                PermanentGrowthType.JumpHeight or
                PermanentGrowthType.SafetyPlatform or
                PermanentGrowthType.DoubleJump or
                PermanentGrowthType.WallCling or
                PermanentGrowthType.DrawnChargeRhythm or
                PermanentGrowthType.ConsecutiveLandingRhythm or
                PermanentGrowthType.ShortPlatformControl or
                PermanentGrowthType.ApexHang or
                PermanentGrowthType.FallControl or
                PermanentGrowthType.LastFallBrake =>
                    "MukJump/UI/Growth/growth_jump",
                PermanentGrowthType.Vitality or
                PermanentGrowthType.LastBreath =>
                    "MukJump/UI/Growth/growth_vitality",
                PermanentGrowthType.DamageGrace or
                PermanentGrowthType.HitHorizontalStability or
                PermanentGrowthType.HitReboundControl or
                PermanentGrowthType.StableHit or
                PermanentGrowthType.CloneSpawnGrace or
                PermanentGrowthType.CloneSourceGrace or
                PermanentGrowthType.CloneDeathHeal or
                PermanentGrowthType.CloneBond or
                PermanentGrowthType.StrokeGuard =>
                    "MukJump/UI/Growth/growth_guard",
                PermanentGrowthType.DrawnPlatformLeap or
                PermanentGrowthType.FirstLandingPause or
                PermanentGrowthType.NaturalExpiryRefund =>
                    "MukJump/UI/Growth/growth_platform",
                PermanentGrowthType.HitInkRecovery or
                PermanentGrowthType.DrawnLandingInk or
                PermanentGrowthType.LowInkRecovery =>
                    "MukJump/UI/Growth/growth_ink_regen",
                PermanentGrowthType.ShortStrokeEfficiency or
                PermanentGrowthType.IdleStrokeEfficiency =>
                    "MukJump/UI/Growth/growth_scroll",
                PermanentGrowthType.WindControl =>
                    "MukJump/UI/Growth/growth_fortune",
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
                CloseNodePopup();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugMenuPanel != null)
                    debugMenuPanel.gameObject.SetActive(false);
#endif
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
                    logicalSafeWidth / ReferenceWidth,
                    logicalSafeHeight / ReferenceHeight);
                contentPanel.localScale =
                    Vector3.one * Mathf.Max(0.01f, contentScale);
            }
            if (TreeCanvas != null)
            {
                TreeCanvas.localScale = Vector3.one * CalculateTreeZoom(
                    Screen.width,
                    Screen.height);
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
                strong: style is FontStyle.Bold or FontStyle.BoldAndItalic);
            PermanentGrowthTypography.ApplyLayout(text, objectName);
            return text;
        }

        static void EnableBestFit(Text text, int minimumSize)
        {
            if (text == null)
                return;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Clamp(minimumSize, 10, text.fontSize);
            text.resizeTextMaxSize = text.fontSize;
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

    }
}
