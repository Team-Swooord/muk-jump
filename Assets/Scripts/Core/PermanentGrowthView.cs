using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로비에서 먹빛을 사용해 먹방울이를 영구 강화하는 전용 화면.
    /// 플레이 중 3지선다 두루마리 UI와 데이터 소유권을 공유하지 않는다.
    [DisallowMultipleComponent]
    public sealed class PermanentGrowthView : MonoBehaviour
    {
        const int CanvasSortingOrder = 4050;
        const float ReferenceHeight = 1920f;
        const float PurchaseFlowDuration = 0.55f;
        const string ArtResourceRoot = "MukJump/UI/PermanentGrowth/";
        static readonly Vector2 HiddenScreenPosition =
            new(0f, ReferenceHeight);

        sealed class GrowthRow
        {
            public RectTransform Root;
            public Image Paper;
            public Image Icon;
            public Image Connector;
            public Image ProgressFlow;
            public Image RedFlow;
            public Image BranchTip;
            public Image CostBrush;
            public Image SelectedRing;
            public Button Button;
            public Text Name;
            public Text Description;
            public Text Level;
            public Text Effect;
            public Text Cost;
            public Image[] Pips;
            public PermanentGrowthType Type;
            public float TrunkFillTarget;
            public bool UsesCardArt;
        }

        readonly List<GrowthRow> rows =
            new(PermanentGrowthCatalog.All.Count);
        readonly Dictionary<string, Sprite> spriteCache = new();
        readonly HashSet<string> missingSpritePaths = new();

        CanvasGroup rootGroup;
        Canvas rootCanvas;
        RectTransform safeAreaRoot;
        RectTransform contentPanel;
        Text balanceText;
        Text detailNameText;
        Text detailLevelText;
        Text detailDescriptionText;
        Text detailCurrentText;
        Text detailNextText;
        Text detailCostText;
        Image detailIcon;
        Image trunkRedFlow;
        GameManager manager;
        Rect lastSafeArea;
        int lastScreenWidth;
        int lastScreenHeight;
        float purchaseLockedUntil;
        float purchaseFlowStartedAt;
        GrowthRow purchaseFlowRow;
        int selectedSlot;

        public bool IsOpen =>
            rootGroup != null && rootGroup.blocksRaycasts;
        public Button BackButton { get; private set; }
        public Button PurchaseButton { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public bool IsDedicatedScreen => ScreenRoot != null;
        public int CreatedRowCount => rows.Count;
        public int CreatedNodeCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < rows.Count; i++)
                    total += rows[i].Pips?.Length ?? 0;
                return total;
            }
        }
        public string BalanceLabel => balanceText != null
            ? balanceText.text
            : string.Empty;
        public PermanentGrowthType SelectedGrowthType =>
            rows.Count > 0 && selectedSlot >= 0 && selectedSlot < rows.Count
                ? rows[selectedSlot].Type
                : PermanentGrowthType.InkCapacity;

        void Awake()
        {
            // 영구 성장 아트는 로비 진입 때부터 상주시키지 않는다.
            // 붓 전환이 화면을 덮은 뒤 처음 표시할 때 한 번만 생성한다.
        }

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
            UpdatePurchaseFlow();
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

            var stale = transform.Find("PermanentGrowthCanvas");
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
            var canvas = root.GetComponent<Canvas>();
            rootCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            rootGroup = root.GetComponent<CanvasGroup>();
            ScreenRoot = CreateRect(
                "ScreenRoot",
                root.transform,
                HiddenScreenPosition,
                new Vector2(1080f, ReferenceHeight));

            var background = CreateStretchImage(
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
            var panel = contentPanel;

            var title = CreateText(
                "Title",
                panel,
                "영구 성장",
                64,
                new Vector2(0f, 800f),
                new Vector2(500f, 92f),
                InkPalette.TextDark,
                FontStyle.Bold);
            AddReadableWeight(title, 0.22f);

            var subtitle = CreateText(
                "Subtitle",
                panel,
                "모든 도전에 계속 적용되는 힘",
                31,
                new Vector2(0f, 728f),
                new Vector2(670f, 56f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(subtitle, 0.12f);

            Sprite balanceSprite =
                LoadPermanentGrowthSprite("pg_currency_badge");
            var balanceBrush = CreateImage(
                "CurrencyBrush",
                panel,
                balanceSprite ??
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 646f),
                new Vector2(470f, 76f),
                balanceSprite != null ? Color.white : InkPalette.Ink);
            balanceText = CreateText(
                "Balance",
                balanceBrush.transform,
                "보유 먹빛 0",
                34,
                Vector2.zero,
                new Vector2(410f, 56f),
                InkPalette.Paper,
                FontStyle.Bold);

            BackButton = CreateBrushButton(
                "BackButton",
                panel,
                "로비",
                new Vector2(-389f, 800f),
                new Vector2(190f, 120f),
                34);
            BackButton.onClick.AddListener(HandleBackRequested);

            BuildInkGrowthTree(panel);

            for (int i = 0; i < PermanentGrowthCatalog.All.Count; i++)
            {
                int slot = i;
                PermanentGrowthDefinition definition =
                    PermanentGrowthCatalog.All[i];
                GrowthRow row = CreateGrowthRow(panel, i, definition);
                row.Button.onClick.AddListener(() => SelectGrowth(slot));
                rows.Add(row);
            }

            BuildSelectedDetailPanel(panel);

            var footer = CreateText(
                "PermanentHint",
                panel,
                "먹가지를 선택한 뒤 강화 · 모든 도전에 영구 적용",
                27,
                new Vector2(0f, -835f),
                new Vector2(780f, 54f),
                ReadableMutedColor(),
                FontStyle.Bold);
            AddReadableWeight(footer, 0.12f);

            ApplySafeArea();
            Refresh();
        }

        void BuildFullScreenInkWash(Transform parent)
        {
            Color paleInk = new(
                InkPalette.Ink.r,
                InkPalette.Ink.g,
                InkPalette.Ink.b,
                0.045f);
            Color faintInk = new(
                InkPalette.Ink.r,
                InkPalette.Ink.g,
                InkPalette.Ink.b,
                0.025f);

            var leftWash = CreateImage(
                "LeftInkWash",
                parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(-430f, 210f),
                new Vector2(560f, 1320f),
                paleInk);
            leftWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -8f);

            var rightWash = CreateImage(
                "RightInkWash",
                parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(455f, -390f),
                new Vector2(500f, 1080f),
                faintInk);
            rightWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, 11f);

            var horizon = CreateImage(
                "HanjiHorizon",
                parent,
                InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, 590f),
                new Vector2(1000f, 18f),
                new Color(
                    InkPalette.Ink.r,
                    InkPalette.Ink.g,
                    InkPalette.Ink.b,
                    0.08f));
            horizon.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -1.5f);
        }

        void BuildInkGrowthTree(Transform panel)
        {
            var crownWash = CreateImage(
                "TreeCrownWash",
                panel,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, -28f),
                new Vector2(850f, 1100f),
                new Color(
                    InkPalette.Ink.r,
                    InkPalette.Ink.g,
                    InkPalette.Ink.b,
                    0.035f));
            crownWash.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -7f);

            Sprite trunkSprite =
                LoadPermanentGrowthSprite("pg_tree_trunk");
            Sprite trunkMask =
                LoadPermanentGrowthSprite("pg_tree_trunk_mask");
            var trunk = CreateImage(
                "InkTreeTrunk",
                panel,
                trunkSprite ?? InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(0f, trunkSprite != null ? 60f : -70f),
                trunkSprite != null
                    ? new Vector2(520f, 980f)
                    : new Vector2(1050f, 62f),
                trunkSprite != null
                    ? Color.white
                    : new Color(
                        InkPalette.Ink.r,
                        InkPalette.Ink.g,
                        InkPalette.Ink.b,
                        0.78f));
            trunk.preserveAspect = trunkSprite != null;
            if (trunkSprite == null)
                trunk.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, 90f);

            if (trunkMask != null)
            {
                trunkRedFlow = CreateImage(
                    "InkTreeRedFlow",
                    panel,
                    trunkMask,
                    new Vector2(0f, 60f),
                    new Vector2(520f, 980f),
                    TransparentColor(InkPalette.Red));
                trunkRedFlow.preserveAspect = true;
                trunkRedFlow.type = Image.Type.Filled;
                trunkRedFlow.fillMethod = Image.FillMethod.Vertical;
                trunkRedFlow.fillOrigin = (int)Image.OriginVertical.Bottom;
                trunkRedFlow.fillAmount = 0f;
            }

            Sprite rootSprite =
                LoadPermanentGrowthSprite("pg_root_emblem");
            var root = CreateImage(
                "InkTreeRoot",
                panel,
                rootSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, -430f),
                rootSprite != null
                    ? new Vector2(98f, 98f)
                    : new Vector2(122f, 86f),
                rootSprite != null ? Color.white : InkPalette.Ink);
            root.preserveAspect = rootSprite != null;
            CreateText(
                "InkTreeRootLabel",
                panel,
                "먹빛",
                25,
                new Vector2(0f, -485f),
                new Vector2(110f, 40f),
                InkPalette.TextDark,
                FontStyle.Bold);

            if (trunkSprite == null)
            {
                for (int i = 0; i < 5; i++)
                {
                    CreateImage(
                        $"TrunkKnot{i + 1}",
                        panel,
                        InkUiTextureFactory.CreateBlobSprite(),
                        new Vector2(
                            (i % 2 == 0 ? -1f : 1f) * 8f,
                            463f - i * 220f),
                        new Vector2(
                            32f + i % 2 * 5f,
                            32f + i % 2 * 5f),
                        InkPalette.Ink);
                }
            }
        }

        GrowthRow CreateGrowthRow(
            Transform parent,
            int index,
            PermanentGrowthDefinition definition)
        {
            bool left = index % 2 == 0;
            float y = 445f - index * 230f;
            float x = left ? -250f : 250f;
            float branchAngle = left ? 8f : -8f;

            Sprite branchSprite =
                LoadPermanentGrowthSprite("pg_branch");
            Sprite branchMask =
                LoadPermanentGrowthSprite("pg_branch_mask");
            var connector = CreateImage(
                $"GrowthBranch{index + 1}",
                parent,
                branchSprite ?? InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(left ? -112f : 112f, y - 7f),
                branchSprite != null
                    ? new Vector2(285f, 68f)
                    : new Vector2(250f, 38f),
                branchSprite != null
                    ? new Color(1f, 1f, 1f, 0.16f)
                    : new Color(
                        InkPalette.Ink.r,
                        InkPalette.Ink.g,
                        InkPalette.Ink.b,
                        0.16f));
            connector.preserveAspect = branchSprite != null;
            if (branchSprite != null)
                connector.rectTransform.localScale =
                    new Vector3(left ? -1f : 1f, 1f, 1f);
            else
                connector.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, branchAngle);

            Image progressFlow = null;
            Image redFlow = null;
            if (branchMask != null)
            {
                progressFlow = CreateImage(
                    $"GrowthBranchProgress{index + 1}",
                    parent,
                    branchMask,
                    new Vector2(left ? -112f : 112f, y - 7f),
                    new Vector2(285f, 68f),
                    WithAlpha(InkPalette.Ink, 0.92f));
                progressFlow.preserveAspect = true;
                progressFlow.rectTransform.localScale =
                    new Vector3(left ? -1f : 1f, 1f, 1f);
                progressFlow.type = Image.Type.Filled;
                progressFlow.fillMethod = Image.FillMethod.Horizontal;
                progressFlow.fillOrigin =
                    (int)Image.OriginHorizontal.Left;
                progressFlow.fillAmount = 0f;

                redFlow = CreateImage(
                    $"GrowthBranchRedFlow{index + 1}",
                    parent,
                    branchMask,
                    new Vector2(left ? -112f : 112f, y - 7f),
                    new Vector2(285f, 68f),
                    TransparentColor(InkPalette.Red));
                redFlow.preserveAspect = true;
                redFlow.rectTransform.localScale =
                    new Vector3(left ? -1f : 1f, 1f, 1f);
                redFlow.type = Image.Type.Filled;
                redFlow.fillMethod = Image.FillMethod.Horizontal;
                redFlow.fillOrigin = (int)Image.OriginHorizontal.Left;
                redFlow.fillAmount = 0f;
            }

            Sprite budSprite =
                LoadPermanentGrowthSprite("pg_node_bud");
            var branchTip = CreateImage(
                $"GrowthBranchTip{index + 1}",
                parent,
                budSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(left ? -245f : 245f, y + 25f),
                budSprite != null
                    ? new Vector2(48f, 48f)
                    : new Vector2(58f, 58f),
                new Color(1f, 1f, 1f, 0.28f));
            branchTip.preserveAspect = budSprite != null;

            var root = CreateRect(
                $"PermanentGrowth{index + 1}",
                parent,
                new Vector2(x, y),
                new Vector2(438f, 238f));
            Sprite cardSprite =
                LoadPermanentGrowthSprite("pg_hanji_card");
            var outline = CreateImage(
                "Outline",
                root,
                cardSprite ??
                InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero,
                new Vector2(438f, 238f),
                cardSprite != null ? Color.white : InkPalette.Ink);
            outline.preserveAspect = false;
            var paper = CreateImage(
                "Paper",
                outline.transform,
                cardSprite != null
                    ? null
                    : InkUiTextureFactory.CreateBlobSprite(),
                Vector2.zero,
                new Vector2(420f, 220f),
                cardSprite != null
                    ? new Color(1f, 1f, 1f, 0f)
                    : InkPalette.Paper2);
            var button = outline.gameObject.AddComponent<Button>();
            InkUiStyle.ConfigureButton(button, outline);

            var iconPaper = CreateImage(
                "IconPaper",
                parent,
                InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(left ? -245f : 245f, y + 28f),
                new Vector2(96f, 96f),
                new Color(
                    InkPalette.Paper.r,
                    InkPalette.Paper.g,
                    InkPalette.Paper.b,
                    0.96f));
            var selectedRing = CreateImage(
                "SelectedRing",
                iconPaper.transform,
                LoadPermanentGrowthSprite("pg_selected_ring"),
                Vector2.zero,
                new Vector2(102f, 102f),
                TransparentColor(InkPalette.Red));
            selectedRing.preserveAspect = true;
            var icon = CreateImage(
                "Icon",
                iconPaper.transform,
                null,
                Vector2.zero,
                new Vector2(76f, 76f),
                Color.white);
            icon.preserveAspect = true;
            branchTip.rectTransform.SetParent(iconPaper.transform, false);
            branchTip.rectTransform.anchoredPosition = Vector2.zero;
            branchTip.rectTransform.sizeDelta = new Vector2(92f, 92f);
            branchTip.rectTransform.localScale = Vector3.one;
            branchTip.rectTransform.SetAsFirstSibling();

            float textX = left ? -112f : 112f;
            TextAnchor textAlignment =
                left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            var name = CreateText(
                "Name",
                paper.transform,
                "영구 성장",
                38,
                new Vector2(textX, 82f),
                new Vector2(190f, 46f),
                InkPalette.TextDark,
                FontStyle.Bold,
                textAlignment);
            var level = CreateText(
                "Level",
                paper.transform,
                "Lv. 0 / 6",
                28,
                new Vector2(textX, 49f),
                new Vector2(190f, 32f),
                ReadableMutedColor(),
                FontStyle.Bold,
                textAlignment);
            var description = CreateText(
                "Description",
                paper.transform,
                "효과 설명",
                29,
                new Vector2(textX, 3f),
                new Vector2(190f, 56f),
                InkPalette.TextDark,
                FontStyle.Bold,
                textAlignment);
            var effect = CreateText(
                "Effect",
                paper.transform,
                "현재 +0% → 다음 +0%",
                28,
                new Vector2(textX, -55f),
                new Vector2(190f, 40f),
                InkPalette.TextDark,
                FontStyle.Bold,
                textAlignment);

            int nodeCount = Mathf.Max(1, definition?.MaxLevel ?? 1);
            var pips = new Image[nodeCount];
            for (int pip = 0; pip < pips.Length; pip++)
            {
                float nodeProgress = pips.Length == 1
                    ? 0.5f
                    : pip / (float)(pips.Length - 1);
                float nodeX = Mathf.Lerp(42f, 202f, nodeProgress);
                float nodeY = y +
                              Mathf.Lerp(14f, 30f, nodeProgress) +
                              Mathf.Sin(nodeProgress * Mathf.PI * 3.4f) * 5f;
                pips[pip] = CreateImage(
                    $"GrowthNode{index + 1}_{pip + 1}",
                    parent,
                    budSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                    new Vector2(
                        (left ? -1f : 1f) * nodeX,
                        nodeY),
                    budSprite != null
                        ? new Vector2(36f, 36f)
                        : new Vector2(22f, 22f),
                    new Color(1f, 1f, 1f, 0.24f));
                pips[pip].preserveAspect = budSprite != null;
            }

            Sprite currencyBadge =
                LoadPermanentGrowthSprite("pg_currency_badge");
            var costBrush = CreateImage(
                "CostBrush",
                paper.transform,
                currencyBadge ?? InkUiTextureFactory.CreateBrushSprite(),
                new Vector2(textX, -96f),
                new Vector2(126f, 38f),
                currencyBadge != null ? Color.white : InkPalette.Ink);
            var cost = CreateText(
                "Cost",
                costBrush.transform,
                "먹빛 0",
                26,
                Vector2.zero,
                new Vector2(116f, 34f),
                InkPalette.TextLight,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            connector.rectTransform.SetAsLastSibling();
            progressFlow?.rectTransform.SetAsLastSibling();
            redFlow?.rectTransform.SetAsLastSibling();
            for (int pip = 0; pip < pips.Length; pip++)
                pips[pip].rectTransform.SetAsLastSibling();
            iconPaper.rectTransform.SetAsLastSibling();

            return new GrowthRow
            {
                Root = root,
                Paper = cardSprite != null ? outline : paper,
                Icon = icon,
                Connector = connector,
                ProgressFlow = progressFlow,
                RedFlow = redFlow,
                BranchTip = branchTip,
                CostBrush = costBrush,
                SelectedRing = selectedRing,
                Button = button,
                Name = name,
                Description = description,
                Level = level,
                Effect = effect,
                Cost = cost,
                Pips = pips,
                Type = definition != null
                    ? definition.Type
                    : PermanentGrowthType.InkCapacity,
                TrunkFillTarget = Mathf.Clamp01(0.94f - index * 0.23f),
                UsesCardArt = cardSprite != null,
            };
        }

        void BuildSelectedDetailPanel(Transform parent)
        {
            Sprite cardSprite =
                LoadPermanentGrowthSprite("pg_hanji_card");
            var panel = CreateImage(
                "SelectedGrowthDetail",
                parent,
                cardSprite ?? InkUiTextureFactory.CreateBlobSprite(),
                new Vector2(0f, -650f),
                new Vector2(900f, 280f),
                cardSprite != null ? Color.white : InkPalette.Paper2);
            panel.preserveAspect = false;

            detailIcon = CreateImage(
                "DetailIcon",
                panel.transform,
                null,
                new Vector2(-375f, 28f),
                new Vector2(118f, 118f),
                Color.white);
            detailIcon.preserveAspect = true;
            detailNameText = CreateText(
                "DetailName",
                panel.transform,
                "먹그릇",
                40,
                new Vector2(-236f, 88f),
                new Vector2(250f, 52f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailLevelText = CreateText(
                "DetailLevel",
                panel.transform,
                "Lv. 0 / 6",
                28,
                new Vector2(-236f, 45f),
                new Vector2(250f, 36f),
                ReadableMutedColor(),
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailDescriptionText = CreateText(
                "DetailDescription",
                panel.transform,
                "기본 능력이 자랍니다",
                28,
                new Vector2(100f, 88f),
                new Vector2(430f, 54f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailCurrentText = CreateText(
                "CurrentEffect",
                panel.transform,
                "현재 효과",
                28,
                new Vector2(-112f, 7f),
                new Vector2(500f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            detailNextText = CreateText(
                "NextEffect",
                panel.transform,
                "다음 레벨 효과",
                28,
                new Vector2(-112f, -38f),
                new Vector2(500f, 42f),
                InkPalette.TextDark,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            PurchaseButton = CreateBrushButton(
                "EnhanceButton",
                panel.transform,
                "강화",
                new Vector2(325f, -54f),
                new Vector2(220f, 104f),
                31);
            detailCostText =
                PurchaseButton.GetComponentInChildren<Text>(true);
            PurchaseButton.onClick.AddListener(HandleSelectedPurchase);
        }

        void SelectGrowth(int slot)
        {
            if (slot < 0 || slot >= rows.Count) return;
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
            if (slot < 0 || slot >= rows.Count) return;
            if (Time.unscaledTime < purchaseLockedUntil) return;
            GrowthRow row = rows[slot];
            if (!PermanentGrowthProfile.TryPurchase(row.Type)) return;

            purchaseLockedUntil =
                Time.unscaledTime + PurchaseFlowDuration;
            purchaseFlowStartedAt = Time.unscaledTime;
            purchaseFlowRow = row;
            if (trunkRedFlow != null)
            {
                trunkRedFlow.fillAmount = 0f;
                trunkRedFlow.color = InkPalette.Red;
            }
            if (row.RedFlow != null)
            {
                row.RedFlow.fillAmount = 0f;
                row.RedFlow.color = InkPalette.Red;
            }
            if (row.SelectedRing != null)
                row.SelectedRing.color = InkPalette.Red;
            // 프로필 변경 이벤트의 수신 순서와 무관하게 구매 직후 단계·비용·
            // 가지 진행도를 같은 프레임에 확정한다.
            Refresh();
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                null,
                row.Icon.rectTransform.position);
            InkUiFeedbackController.PlayLevelUp(screenPosition);
        }

        void UpdatePurchaseFlow()
        {
            if (purchaseFlowRow == null) return;

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - purchaseFlowStartedAt) /
                PurchaseFlowDuration);
            float rise = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress / 0.72f));
            float fade = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.72f, 1f, progress));

            if (trunkRedFlow != null)
            {
                trunkRedFlow.fillAmount =
                    purchaseFlowRow.TrunkFillTarget * rise;
                trunkRedFlow.color =
                    WithAlpha(InkPalette.Red, fade);
            }
            if (purchaseFlowRow.RedFlow != null)
            {
                purchaseFlowRow.RedFlow.fillAmount = rise;
                purchaseFlowRow.RedFlow.color =
                    WithAlpha(InkPalette.Red, fade);
            }
            if (purchaseFlowRow.SelectedRing != null)
            {
                purchaseFlowRow.SelectedRing.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(0.82f, 1.08f, rise);
                purchaseFlowRow.SelectedRing.color =
                    WithAlpha(InkPalette.Red, fade);
            }

            if (progress < 1f) return;
            if (trunkRedFlow != null)
            {
                trunkRedFlow.fillAmount = 0f;
                trunkRedFlow.color = TransparentColor(InkPalette.Red);
            }
            if (purchaseFlowRow.RedFlow != null)
            {
                purchaseFlowRow.RedFlow.fillAmount = 0f;
                purchaseFlowRow.RedFlow.color =
                    TransparentColor(InkPalette.Red);
            }
            if (purchaseFlowRow.SelectedRing != null)
            {
                purchaseFlowRow.SelectedRing.rectTransform.localScale =
                    Vector3.one;
                purchaseFlowRow.SelectedRing.color =
                    SelectionRingColor(purchaseFlowRow);
            }
            purchaseFlowRow = null;
        }

        void Refresh()
        {
            if (balanceText == null) return;
            balanceText.text = $"보유 먹빛 {PermanentGrowthProfile.Currency}";

            Sprite branchSprite =
                LoadPermanentGrowthSprite("pg_branch");
            Sprite budSprite =
                LoadPermanentGrowthSprite("pg_node_bud");
            Sprite bloomSprite =
                LoadPermanentGrowthSprite("pg_node_bloom");
            Sprite bloomMask =
                LoadPermanentGrowthSprite("pg_node_bloom_mask");
            Sprite currencyBadge =
                LoadPermanentGrowthSprite("pg_currency_badge");
            for (int i = 0; i < rows.Count; i++)
            {
                GrowthRow row = rows[i];
                PermanentGrowthDefinition definition =
                    PermanentGrowthCatalog.Get(row.Type);
                if (definition == null) continue;

                int level = PermanentGrowthProfile.GetLevel(row.Type);
                bool maxed = level >= definition.MaxLevel;
                int cost = definition.GetCost(level);
                float currentPercent = definition.GetPercentAtLevel(level);
                float nextPercent = definition.GetPercentAtLevel(level + 1);
                string sign = definition.ReducesValue ? "-" : "+";

                row.Name.text = definition.Name;
                row.Description.text = CompactDescription(row.Type);
                row.Level.text = $"Lv. {level} / {definition.MaxLevel}";
                row.Icon.sprite = LoadIcon(row.Type);
                row.Icon.color = row.Icon.sprite != null
                    ? Color.white
                    : InkPalette.Ink;
                row.Effect.text = maxed
                    ? $"{definition.EffectUnit} {sign}{FormatPercent(currentPercent)}% · 완성"
                    : $"현재 {sign}{FormatPercent(currentPercent)}%  →  " +
                      $"{sign}{FormatPercent(nextPercent)}%";
                row.Cost.text = maxed ? "완성" : $"먹빛 {cost}";
                bool canPurchase =
                    !maxed &&
                    PermanentGrowthProfile.Currency >= cost;
                row.Button.interactable = true;
                bool usesCurrencyArt =
                    currencyBadge != null &&
                    row.CostBrush.sprite == currencyBadge;
                row.CostBrush.color = usesCurrencyArt
                    ? new Color(
                        1f,
                        1f,
                        1f,
                        canPurchase ? 1f : 0.56f)
                    : new Color(
                        InkPalette.Ink.r,
                        InkPalette.Ink.g,
                        InkPalette.Ink.b,
                        canPurchase ? 1f : 0.56f);
                if (row.ProgressFlow != null)
                {
                    row.Connector.color =
                        new Color(1f, 1f, 1f, 0.16f);
                    row.ProgressFlow.fillAmount =
                        level / (float)definition.MaxLevel;
                    row.ProgressFlow.color =
                        WithAlpha(InkPalette.Ink, 0.92f);
                }
                else
                {
                    float branchAlpha = level > 0
                        ? Mathf.Lerp(
                            0.52f,
                            0.95f,
                            level / (float)definition.MaxLevel)
                        : 0.16f;
                    row.Connector.color = branchSprite != null
                        ? new Color(1f, 1f, 1f, branchAlpha)
                        : new Color(
                            InkPalette.Ink.r,
                            InkPalette.Ink.g,
                            InkPalette.Ink.b,
                            branchAlpha);
                }
                if (maxed && bloomMask != null)
                {
                    row.BranchTip.sprite = bloomMask;
                    row.BranchTip.color = InkPalette.Gold;
                }
                else if (level > 0 && bloomSprite != null)
                {
                    row.BranchTip.sprite = bloomSprite;
                    row.BranchTip.color =
                        new Color(1f, 1f, 1f, 0.92f);
                }
                else
                {
                    row.BranchTip.sprite =
                        budSprite ?? InkUiTextureFactory.CreateBlobSprite();
                    row.BranchTip.color = budSprite != null
                        ? new Color(1f, 1f, 1f, 0.3f)
                        : new Color(
                            InkPalette.Ink.r,
                            InkPalette.Ink.g,
                            InkPalette.Ink.b,
                            0.2f);
                }
                bool selected = i == selectedSlot;
                row.Paper.color = maxed
                    ? new Color(1f, 0.97f, 0.86f, 1f)
                    : selected
                        ? new Color(1f, 0.96f, 0.92f, 1f)
                        : row.UsesCardArt
                            ? Color.white
                            : InkPalette.Paper2;
                if (!ReferenceEquals(purchaseFlowRow, row))
                {
                    row.SelectedRing.rectTransform.localScale = Vector3.one;
                    row.SelectedRing.color = SelectionRingColor(row);
                }
                for (int pip = 0; pip < row.Pips.Length; pip++)
                {
                    bool purchased = pip < level;
                    bool goldTip =
                        maxed && pip == row.Pips.Length - 1;
                    if (goldTip && bloomMask != null)
                    {
                        row.Pips[pip].sprite = bloomMask;
                        row.Pips[pip].color = InkPalette.Gold;
                    }
                    else if (purchased && bloomSprite != null)
                    {
                        row.Pips[pip].sprite = bloomSprite;
                        row.Pips[pip].color = Color.white;
                    }
                    else
                    {
                        row.Pips[pip].sprite =
                            budSprite ??
                            InkUiTextureFactory.CreateBlobSprite();
                        float alpha =
                            pip == level &&
                            canPurchase
                                ? 0.55f
                                : 0.24f;
                        row.Pips[pip].color = budSprite != null
                            ? new Color(1f, 1f, 1f, alpha)
                            : new Color(
                                InkPalette.Ink.r,
                                InkPalette.Ink.g,
                                InkPalette.Ink.b,
                                alpha);
                    }
                }
            }
            RefreshSelectedDetail();
        }

        void RefreshSelectedDetail()
        {
            if (rows.Count == 0 ||
                selectedSlot < 0 ||
                selectedSlot >= rows.Count ||
                detailNameText == null)
                return;

            GrowthRow row = rows[selectedSlot];
            PermanentGrowthDefinition definition =
                PermanentGrowthCatalog.Get(row.Type);
            if (definition == null) return;

            int level = PermanentGrowthProfile.GetLevel(row.Type);
            bool maxed = level >= definition.MaxLevel;
            int cost = definition.GetCost(level);
            float currentPercent = definition.GetPercentAtLevel(level);
            float nextPercent = definition.GetPercentAtLevel(level + 1);
            string sign = definition.ReducesValue ? "-" : "+";

            detailIcon.sprite = LoadIcon(row.Type);
            detailIcon.color = detailIcon.sprite != null
                ? Color.white
                : InkPalette.Ink;
            detailNameText.text = definition.Name;
            detailLevelText.text = $"Lv. {level} / {definition.MaxLevel}";
            detailDescriptionText.text = definition.Description;
            detailCurrentText.text =
                $"현재 효과  {definition.EffectUnit} " +
                $"{sign}{FormatPercent(currentPercent)}%";
            detailNextText.text = maxed
                ? "다음 레벨 효과  최고 단계 완성"
                : $"다음 레벨 효과  {definition.EffectUnit} " +
                  $"{sign}{FormatPercent(nextPercent)}%";
            detailCostText.text = maxed
                ? "완성"
                : $"강화 · 먹빛 {cost}";
            PurchaseButton.interactable =
                !maxed && PermanentGrowthProfile.Currency >= cost;
        }

        Color SelectionRingColor(GrowthRow row)
        {
            return rows.Count > 0 &&
                   selectedSlot >= 0 &&
                   selectedSlot < rows.Count &&
                   ReferenceEquals(rows[selectedSlot], row)
                ? WithAlpha(InkPalette.Red, 0.82f)
                : TransparentColor(InkPalette.Red);
        }

        Sprite LoadIcon(PermanentGrowthType type)
        {
            string permanentPath = type switch
            {
                PermanentGrowthType.InkCapacity =>
                    "pg_icon_capacity",
                PermanentGrowthType.InkRecovery =>
                    "pg_icon_recovery",
                PermanentGrowthType.PlatformLifetime =>
                    "pg_icon_platform",
                PermanentGrowthType.JumpCharge =>
                    "pg_icon_jump",
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
                PermanentGrowthType.JumpCharge =>
                    "MukJump/UI/Growth/growth_jump",
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
                // 이전 Multiple 임포터 메타로 실행 중인 구버전 씬도 한 번만 복구한다.
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

        static string CompactDescription(PermanentGrowthType type)
        {
            return type switch
            {
                PermanentGrowthType.InkCapacity =>
                    "기본 먹통이 넓어집니다",
                PermanentGrowthType.InkRecovery =>
                    "먹 회복이 빨라집니다",
                PermanentGrowthType.PlatformLifetime =>
                    "발판 여운이 길어집니다",
                PermanentGrowthType.JumpCharge =>
                    "점프 준비가 빨라집니다",
                _ => "기본 능력이 자랍니다",
            };
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
            if (ScreenRoot != null)
            {
                ScreenRoot.anchoredPosition = visible
                    ? Vector2.zero
                    : HiddenScreenPosition;
            }
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
            if (contentPanel != null &&
                safe.width > 0f &&
                safe.height > 0f)
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

        static string FormatPercent(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
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

        Button CreateBrushButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var brush = CreateImage(
                objectName,
                parent,
                null,
                position,
                size,
                InkPalette.Ink);
            brush.raycastTarget = true;
            var button = brush.gameObject.AddComponent<Button>();
            var text = CreateText(
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
            muted.a = 0.9f;
            return muted;
        }

        static void AddReadableWeight(Text text, float alpha)
        {
            if (text == null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            Color ink = InkPalette.Ink;
            shadow.effectColor = new Color(ink.r, ink.g, ink.b, alpha);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }
    }
}
