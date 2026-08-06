using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class LobbyMenuTests
    {
        GameObject viewHost;
        GameObject managerHost;
        GameObject playerHost;
        MemoryLobbySettingsStore lobbySettingsStore;

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
            lobbySettingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(lobbySettingsStore);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerHost != null)
                Object.DestroyImmediate(playerHost);
            if (managerHost != null)
                Object.DestroyImmediate(managerHost);
            if (viewHost != null)
                Object.DestroyImmediate(viewHost);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void LobbyBuildsThreePermanentGrowthBranches()
        {
            managerHost = new GameObject("PermanentGrowthTestManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("PermanentGrowthTestHost");
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            growthView.Open();
            Assert.That(growthView.IsOpen, Is.True);
            Assert.That(growthView.CreatedRowCount, Is.EqualTo(3));
            Assert.That(
                growthView.CreatedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.Nodes.Count));
            Assert.That(growthView.BalanceLabel, Is.EqualTo("0"));
            Assert.That(
                growthView.DistanceProgressLabel,
                Is.EqualTo("누적 0 / 20 m"));
            Transform growthPanel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(growthPanel, Is.Not.Null);
            var viewport = (RectTransform)viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/TreeLayerRoot/" +
                "TreeViewport");
            Assert.That(viewport, Is.Not.Null);
            Assert.That(
                viewport.parent.name,
                Is.EqualTo("TreeLayerRoot"),
                "지도는 Safe Area와 무관하게 실제 화면 네 변까지 사용해야 합니다.");
            var treeCanvas =
                (RectTransform)viewport.Find("TreeCanvas");
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(scrollRect.content, Is.SameAs(treeCanvas));
            Assert.That(scrollRect.horizontal, Is.True);
            Assert.That(scrollRect.vertical, Is.True);
            Assert.That(
                scrollRect.movementType,
                Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(treeCanvas.sizeDelta.x, Is.GreaterThan(viewport.sizeDelta.x));
            Assert.That(treeCanvas.sizeDelta.y, Is.GreaterThan(viewport.sizeDelta.y));
            Assert.That(viewport.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(viewport.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(viewport.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(viewport.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(viewport.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(
                viewport.GetComponent<RectMask2D>().padding,
                Is.EqualTo(Vector4.zero));
            Assert.That(
                treeCanvas.localScale.x,
                Is.EqualTo(
                    PermanentGrowthView.CalculateTreeZoomForTests(
                        Screen.width,
                        Screen.height))
                    .Within(0.001f));
            Assert.That(
                viewport.GetSiblingIndex(),
                Is.Zero,
                "성장 지도는 고정 헤더 뒤에서 화면 위쪽까지 사용해야 합니다.");
            var inkRoot =
                (RectTransform)treeCanvas.Find("InkTreeRoot");
            Assert.That(inkRoot, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            AssertContainedInViewport(inkRoot, viewport, "먹나무 뿌리");
            AssertContainedInViewport(
                FindGrowthNode(treeCanvas, "S00"),
                viewport,
                "생존 첫 열매");
            AssertContainedInViewport(
                FindGrowthNode(treeCanvas, "I00"),
                viewport,
                "먹 운용 첫 열매");
            AssertContainedInViewport(
                FindGrowthNode(treeCanvas, "J00"),
                viewport,
                "도약 첫 열매");
            Assert.That(
                treeCanvas.Find("InkTreeTrunk"),
                Is.Null,
                "완성 나무 위에 별도 줄기를 겹치면 알파 경계가 드러납니다.");
            Assert.That(
                treeCanvas.Find("InkTreeRootLabel"),
                Is.Null,
                "지도 안에는 뿌리 설명 글자를 반복하지 않습니다.");
            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches)
            {
                RectTransform header = treeCanvas.Find(
                        $"GrowthBranchHeader_{branch.Branch}")
                    ?.GetComponent<RectTransform>();
                Assert.That(
                    header,
                    Is.Not.Null,
                    branch.DisplayName);
                AssertContainedInViewport(
                    header,
                    viewport,
                    $"{branch.DisplayName} 대분류");
                Image branchBrush = header.Find("Brush")
                    ?.GetComponent<Image>();
                Text branchTitle = header.Find("Brush/BranchTitle")
                    ?.GetComponent<Text>();
                Assert.That(branchBrush, Is.Not.Null);
                Assert.That(
                    branchBrush.color.a,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(branchTitle?.fontSize, Is.GreaterThanOrEqualTo(36));
                Assert.That(branchTitle?.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(header.Find("BranchSummary"), Is.Null);
            }
            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                Transform node = treeCanvas.Find(
                    $"GrowthNode_{SanitizeNodeId(definition.Id)}");
                Assert.That(node, Is.Not.Null, definition.Name);
                var rect = node.GetComponent<RectTransform>();
                Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(100f));
                Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(100f));
                Assert.That(node.GetComponent<Button>(), Is.Not.Null);
                Assert.That(node.Find("NodeName"), Is.Null);
                Assert.That(node.Find("NodeLevel"), Is.Null);
                RectTransform surface = node.Find("NodeSurface")
                    ?.GetComponent<RectTransform>();
                Assert.That(surface, Is.Not.Null);
                Assert.That(
                    surface.sizeDelta.x,
                    Is.EqualTo(surface.sizeDelta.y).Within(0.01f),
                    definition.Name);
            }
            Assert.That(
                growthPanel.Find("SelectedGrowthDetail"),
                Is.Null);
            Assert.That(
                growthPanel.Find("PermanentHint"),
                Is.Null);
            var selectedAction = growthView.ScreenRoot.Find(
                    "GrowthNodePopupOverlay/SafeAreaRoot/PopupContent/" +
                    "SelectedGrowthAction")
                ?.GetComponent<RectTransform>();
            Assert.That(selectedAction, Is.Not.Null);
            Assert.That(selectedAction.IsChildOf(treeCanvas), Is.False,
                "상세 팝업은 드래그되는 나무가 아니라 고정 화면에 있어야 합니다.");
            Assert.That(
                selectedAction.GetComponent<Image>(),
                Is.Not.Null,
                "상세 정보는 읽을 수 있는 한지 카드 위에 표시해야 합니다.");
            Assert.That(
                selectedAction.sizeDelta.x,
                Is.GreaterThanOrEqualTo(800f));
            Assert.That(
                selectedAction.sizeDelta.y,
                Is.InRange(1000f, 1040f),
                "하단 행동을 내린 뒤에도 한지 카드 안에서 잘리지 않아야 합니다.");
            Assert.That(selectedAction.gameObject.activeSelf, Is.False);

            growthView.SelectGrowthForTests(0);

            Assert.That(growthView.IsNodePopupOpen, Is.True);
            Assert.That(selectedAction.gameObject.activeSelf, Is.True);
            Assert.That(
                growthView.ScreenRoot.Find(
                    "GrowthNodePopupOverlay/GrowthNodePopupDimmer")
                    ?.gameObject.activeSelf,
                Is.True);
            RectTransform growthOverlay = growthView.ScreenRoot
                .Find("GrowthNodePopupOverlay")
                ?.GetComponent<RectTransform>();
            Image growthDim = growthView.ScreenRoot
                .Find("GrowthNodePopupOverlay/GrowthNodePopupDimmer")
                ?.GetComponent<Image>();
            Assert.That(growthOverlay, Is.Not.Null);
            Assert.That(growthDim, Is.Not.Null);
            Assert.That(growthDim.raycastTarget, Is.True);
            Assert.That(growthDim.color.a,
                Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));
            Assert.That(growthDim.color.a,
                Is.GreaterThanOrEqualTo(0.78f),
                "팝업 뒤 화면은 정보 계층이 분명하도록 충분히 어두워야 합니다.");
            Assert.That(growthDim.rectTransform.anchorMin,
                Is.EqualTo(Vector2.zero));
            Assert.That(growthDim.rectTransform.anchorMax,
                Is.EqualTo(Vector2.one));
            Assert.That(growthOverlay.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(growthOverlay.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(growthOverlay.parent, Is.SameAs(growthView.ScreenRoot));
            Assert.That(
                growthDim.transform.parent,
                Is.SameAs(growthOverlay),
                "성장 팝업 dim은 축소되는 1080×1920 카드가 아니라 실제 화면을 덮어야 합니다.");
            Text actionName = selectedAction.Find("ActionName")
                ?.GetComponent<Text>();
            Assert.That(actionName, Is.Not.Null);
            Assert.That(actionName.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(selectedAction.Find("ActionDescription"), Is.Not.Null);
            Assert.That(selectedAction.Find("ActionEffectSummary"), Is.Not.Null);
            Assert.That(selectedAction.Find("ActionCurrentEffect"), Is.Null);
            Assert.That(selectedAction.Find("ActionUsage"), Is.Null);
            Assert.That(selectedAction.Find("ActionNextEffect"), Is.Null);
            Assert.That(selectedAction.Find("ActionStatus"), Is.Null);
            Assert.That(
                selectedAction.Find("ActionCostIcon")?.GetComponent<Image>(),
                Is.Not.Null);
            Assert.That(
                selectedAction.Find("EnhanceButton")?.GetComponent<Button>(),
                Is.Not.Null);
            Assert.That(
                selectedAction.Find("CloseButton"),
                Is.Null,
                "상세창은 별도 닫기 버튼 대신 전체 화면 DIM으로 닫습니다.");
            Assert.That(
                growthPanel.Find("GrowthDebugMenuButton"),
                Is.Null);
            Assert.That(
                growthPanel.Find("GrowthDebugMenu"),
                Is.Null,
                "성장 화면에는 개발용 재화·초기화 UI를 노출하지 않습니다.");

            growthView.NodePopupDimmerButton.onClick.Invoke();
            Assert.That(growthView.IsNodePopupOpen, Is.False);

            growthView.Close();
            Assert.That(growthView.IsOpen, Is.False);
        }

        [Test]
        public void PermanentGrowthTreePansUntilFixedNodePopupOpens()
        {
            managerHost = new GameObject("GrowthPanTestManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            PermanentGrowthProfile.SettleRun(
                "growth-pan-lock",
                100,
                0,
                true);
            viewHost = new GameObject("GrowthPanTestHost");
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            RectTransform viewport = view.TreeViewport;
            RectTransform treeCanvas = view.TreeCanvas;
            ScrollRect scrollRect = view.TreeScrollRect;
            RectTransform selectedAction = view.ScreenRoot.Find(
                    "GrowthNodePopupOverlay/SafeAreaRoot/PopupContent/" +
                    "SelectedGrowthAction")
                ?.GetComponent<RectTransform>();
            RectTransform selectedNode = treeCanvas.Find(
                    "GrowthNode_I00")
                ?.GetComponent<RectTransform>();
            Assert.That(viewport, Is.Not.Null);
            Assert.That(treeCanvas, Is.Not.Null);
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(selectedAction, Is.Not.Null);
            Assert.That(selectedNode, Is.Not.Null);
            Assert.That(selectedAction.IsChildOf(treeCanvas), Is.False);
            Assert.That(selectedAction.gameObject.activeSelf, Is.False);

            Vector2 actionPosition = selectedAction.anchoredPosition;
            Vector3 actionWorldPosition = selectedAction.position;
            Vector2 initialTreePosition = treeCanvas.anchoredPosition;
            scrollRect.horizontalNormalizedPosition = 1f;
            scrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();

            Assert.That(
                treeCanvas.anchoredPosition,
                Is.Not.EqualTo(initialTreePosition),
                "큰 먹나무 지도만 양축으로 움직여야 합니다.");
            Assert.That(
                selectedAction.anchoredPosition,
                Is.EqualTo(actionPosition),
                "고정 팝업 위치는 지도 팬과 무관해야 합니다.");
            Assert.That(
                selectedAction.position,
                Is.EqualTo(actionWorldPosition),
                "상세 팝업은 먹나무와 함께 이동하면 안 됩니다.");

            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                RectTransform node = treeCanvas.Find(
                        $"GrowthNode_{SanitizeNodeId(definition.Id)}")
                    ?.GetComponent<RectTransform>();
                Assert.That(node, Is.Not.Null, definition.Name);
                minimumX = Mathf.Min(minimumX, node.anchoredPosition.x);
                maximumX = Mathf.Max(maximumX, node.anchoredPosition.x);
                minimumY = Mathf.Min(minimumY, node.anchoredPosition.y);
                maximumY = Mathf.Max(maximumY, node.anchoredPosition.y);
            }
            Assert.That(minimumX, Is.LessThan(-300f));
            Assert.That(maximumX, Is.GreaterThan(300f));
            Assert.That(maximumY - minimumY, Is.GreaterThan(800f));

            view.SelectGrowthForTests(0);
            Assert.That(view.IsNodePopupOpen, Is.True);
            Assert.That(view.TreeScrollRect.enabled, Is.False,
                "상세 팝업을 읽는 동안 나무가 뒤에서 움직이면 안 됩니다.");
            Assert.That(view.PurchaseButton.interactable, Is.True);
            view.PurchaseButton.onClick.Invoke();
            Assert.That(view.IsNodePopupOpen, Is.False,
                "강화 성공 뒤에는 팝업을 닫고 해금 연출에 집중해야 합니다.");
            Assert.That(
                view.TreeScrollRect.enabled,
                Is.False,
                "열매 해금 연출 중에는 지도 관성·드래그를 잠가야 합니다.");
        }

        [Test]
        public void EveryGrowthNodeOpensReadableFixedDetailPopup()
        {
            managerHost = new GameObject("GrowthActionPlacementManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("GrowthActionPlacementHost");
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            RectTransform treeCanvas = view.TreeCanvas;
            RectTransform action = view.ScreenRoot.Find(
                    "GrowthNodePopupOverlay/SafeAreaRoot/PopupContent/" +
                    "SelectedGrowthAction")
                ?.GetComponent<RectTransform>();
            Assert.That(action, Is.Not.Null);
            Text actionName = action.Find("ActionName")?.GetComponent<Text>();
            Text actionDescription = action.Find("ActionDescription")
                ?.GetComponent<Text>();
            Text effectSummary = action.Find("ActionEffectSummary")
                ?.GetComponent<Text>();
            Assert.That(actionName, Is.Not.Null);
            Assert.That(actionDescription, Is.Not.Null);
            Assert.That(effectSummary, Is.Not.Null);

            for (int slot = 0;
                 slot < PermanentGrowthCatalog.Nodes.Count;
                 slot++)
            {
                PermanentGrowthNodeDefinition definition =
                    PermanentGrowthCatalog.Nodes[slot];
                view.SelectGrowthForTests(slot);
                Canvas.ForceUpdateCanvases();

                Assert.That(view.IsNodePopupOpen, Is.True, definition.Id);
                Assert.That(actionName.text, Is.EqualTo(definition.Name));
                Assert.That(
                    actionDescription.text,
                    Is.EqualTo(definition.Description));
                Assert.That(
                    effectSummary.text,
                    Is.EqualTo(definition.EffectSummary));
                Assert.That(view.TreeScrollRect.enabled, Is.False);
                view.NodePopupDimmerButton.onClick.Invoke();
                Assert.That(view.IsNodePopupOpen, Is.False);
                Assert.That(view.TreeScrollRect.enabled, Is.True);
            }
        }

        [Test]
        public void GrowthScreenOmitsDevelopmentDebugControls()
        {
            managerHost = new GameObject("GrowthNoDebugManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("GrowthNoDebugHost");
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(
                view.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/" +
                    "GrowthDebugMenuButton"),
                Is.Null);
            Assert.That(
                view.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/" +
                    "GrowthDebugMenu"),
                Is.Null);
        }

        static RectTransform FindGrowthNode(
            RectTransform treeCanvas,
            PermanentGrowthType type,
            int rank)
        {
            PermanentGrowthNodeDefinition definition =
                PermanentGrowthCatalog.GetNode(type, rank);
            return treeCanvas.Find(
                    $"GrowthNode_{SanitizeNodeId(definition.Id)}")
                ?.GetComponent<RectTransform>();
        }

        static RectTransform FindGrowthNode(
            RectTransform treeCanvas,
            string nodeId)
        {
            return treeCanvas.Find(
                    $"GrowthNode_{SanitizeNodeId(nodeId)}")
                ?.GetComponent<RectTransform>();
        }

        static void AssertContainedInViewport(
            RectTransform element,
            RectTransform viewport,
            string label)
        {
            Assert.That(element, Is.Not.Null, label);
            Rect elementRect = WorldRect(element);
            Rect viewportRect = WorldRect(viewport);
            const float Tolerance = 1f;
            Assert.That(
                elementRect.xMin,
                Is.GreaterThanOrEqualTo(viewportRect.xMin - Tolerance),
                $"{label} 왼쪽");
            Assert.That(
                elementRect.xMax,
                Is.LessThanOrEqualTo(viewportRect.xMax + Tolerance),
                $"{label} 오른쪽");
            Assert.That(
                elementRect.yMin,
                Is.GreaterThanOrEqualTo(viewportRect.yMin - Tolerance),
                $"{label} 아래");
            Assert.That(
                elementRect.yMax,
                Is.LessThanOrEqualTo(viewportRect.yMax + Tolerance),
                $"{label} 위");
        }

        static Rect WorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners[0].x,
                corners[0].y,
                corners[2].x,
                corners[2].y);
        }

        static string SanitizeNodeId(string id)
        {
            char[] characters = id.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsLetterOrDigit(characters[i]))
                    characters[i] = '_';
            return new string(characters);
        }

        [Test]
        public void OptionsTutorialUsesFiveSequentialPagesAndMarksCompletion()
        {
            viewHost = new GameObject("LobbyOptionsTestHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();

            optionsView.OpenTutorialForTests();

            Assert.That(optionsView.IsOpen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(
                optionsView.TutorialPageCount,
                Is.EqualTo(GameplayTutorialCatalog.Count));
            Assert.That(optionsView.CurrentTutorialPage, Is.EqualTo(0));
            Assert.That(optionsView.PlayerUidLabel,
                Does.Match("^MUK-[0-9A-F]{8}$"));

            for (int expectedPage = 1;
                 expectedPage < GameplayTutorialCatalog.Count;
                 expectedPage++)
            {
                Invoke(optionsView, "NextTutorialPage");
                Assert.That(optionsView.CurrentTutorialPage,
                    Is.EqualTo(expectedPage));
                Assert.That(optionsView.IsTutorialOpen, Is.True);
            }

            Invoke(optionsView, "NextTutorialPage");

            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.False,
                "마지막 안내의 완료 버튼은 옵션 본문으로 돌아가야 합니다.");
            Assert.That(optionsView.IsOpen, Is.True);
        }

        [Test]
        public void OptionsUsesCalmHanjiHierarchyWithOnlyPrimaryActionInInk()
        {
            managerHost = new GameObject("LobbyOptionsLayoutManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyOptionsLayoutHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();
            optionsView.Open();

            Image optionsDim = viewHost.transform
                .Find("LobbyOptionsCanvas/InkDim")
                ?.GetComponent<Image>();
            Assert.That(optionsDim, Is.Not.Null);
            Assert.That(optionsDim.raycastTarget, Is.True);
            Assert.That(optionsDim.color.a,
                Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));

            Transform page = viewHost.transform.Find(
                "LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
            Assert.IsNotNull(page);
            RectTransform uid = RequireRect(page, "UidButton");
            RectTransform bgm = RequireRect(page, "BgmCard");
            RectTransform sfx = RequireRect(page, "SfxCard");
            RectTransform support = RequireRect(page, "CustomerCenterButton");
            RectTransform tutorial = RequireRect(page, "GuideButton");

            Assert.That(uid.anchoredPosition.y, Is.GreaterThan(bgm.anchoredPosition.y));
            Assert.That(bgm.anchoredPosition.x, Is.EqualTo(0f));
            Assert.That(sfx.anchoredPosition.x, Is.EqualTo(0f));
            Assert.That(bgm.anchoredPosition.y, Is.GreaterThan(sfx.anchoredPosition.y),
                "두 음량 조절은 좁은 화면에서도 읽히는 한 줄 행으로 쌓아야 합니다.");
            Assert.That(support.anchoredPosition.x,
                Is.EqualTo(tutorial.anchoredPosition.x));
            Assert.That(support.anchoredPosition.y,
                Is.GreaterThan(tutorial.anchoredPosition.y),
                "튜토리얼은 고객센터 바로 아래 같은 열에 배치해야 합니다.");

            Text title = page.Find("Title")?.GetComponent<Text>();
            Text version = page.Find("Version")?.GetComponent<Text>();
            Assert.IsNotNull(title);
            Assert.IsNotNull(version);
            Assert.That(title.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(version.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(
                title.rectTransform.anchoredPosition.y,
                Is.GreaterThan(version.rectTransform.anchoredPosition.y),
                "버전은 제목 아래의 조용한 보조 정보여야 합니다.");
            Assert.That(
                title.rectTransform.anchoredPosition.x,
                Is.EqualTo(version.rectTransform.anchoredPosition.x));

            AssertOptionButton(page, "LanguageButton", usesActionBrush: false);
            AssertOptionButton(page, "CustomerCenterButton", usesActionBrush: false);
            AssertOptionButton(page, "AccountConnectButton", usesActionBrush: false);
            AssertOptionButton(page, "GuideButton", usesActionBrush: false);
            AssertOptionButton(page, "CloseButton", usesActionBrush: true);
            AssertOptionButton(page, "UidButton", usesActionBrush: false);
            AssertOptionButton(
                page.Find("BgmCard/Paper"),
                "Toggle",
                usesActionBrush: false);
            AssertOptionButton(
                page.Find("SfxCard/Paper"),
                "Toggle",
                usesActionBrush: false);

            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Bold));
            AssertQuietOptionText(page.Find("Version")?.GetComponent<Text>());
            AssertQuietOptionText(page.Find("ConnectionStatus")?.GetComponent<Text>());
            AssertQuietOptionText(page.Find("PrivacyCaption")?.GetComponent<Text>());
            AssertQuietOptionText(
                page.Find("CustomerCenterButton/Paper/Status")?.GetComponent<Text>());

            RectTransform bgmSlider = RequireRect(page.Find("BgmCard/Paper"), "Slider");
            RectTransform bgmToggle = RequireRect(page.Find("BgmCard/Paper"), "Toggle");
            Assert.That(bgmSlider.sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            Assert.That(bgmSlider.anchoredPosition.x + bgmSlider.sizeDelta.x * 0.5f,
                Is.LessThanOrEqualTo(
                    bgmToggle.anchoredPosition.x - bgmToggle.sizeDelta.x * 0.5f),
                "슬라이더와 음소거 버튼의 터치 영역이 겹치면 안 됩니다.");

            page.Find("CustomerCenterButton")
                ?.GetComponent<Button>()
                ?.onClick.Invoke();
            Text status = page.Find("ConnectionStatus")?.GetComponent<Text>();
            Assert.IsNotNull(status);
            Assert.That(status.text, Does.Contain("고객센터"));
            Assert.That(status.text, Does.Contain("준비 중"));

            page.Find("GuideButton")?.GetComponent<Button>()?.onClick.Invoke();
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(
                page.parent.Find("TutorialPage")
                    ?.GetComponent<CanvasGroup>()
                    ?.blocksRaycasts,
                Is.True);
            Transform tutorialPage = page.parent.Find("TutorialPage");
            AssertOptionButton(tutorialPage, "TutorialClose", usesActionBrush: false);
            AssertOptionButton(tutorialPage, "PreviousButton", usesActionBrush: false);
            AssertOptionButton(tutorialPage, "NextButton", usesActionBrush: true);
            AssertQuietOptionText(
                tutorialPage.Find("TutorialDescription")?.GetComponent<Text>());
            RectTransform tutorialDescription = tutorialPage
                .Find("TutorialDescription") as RectTransform;
            Assert.That(tutorialDescription, Is.Not.Null);
            Assert.That(tutorialDescription.sizeDelta,
                Is.EqualTo(new Vector2(680f, 300f)));

            optionsView.Close();
            CanvasGroup root = viewHost.transform
                .Find("LobbyOptionsCanvas")
                ?.GetComponent<CanvasGroup>();
            Assert.IsNotNull(root);
            Assert.That(root.blocksRaycasts, Is.False);
        }

        [Test]
        public void LobbySettingsMemoryStorePersistsAudioTutorialAndUid()
        {
            LobbySettingsProfile.SetBgmVolume(0.35f);
            LobbySettingsProfile.SetSfxVolume(0.6f);
            LobbySettingsProfile.SetBgmVolume(0f);
            LobbySettingsProfile.SetSfxVolume(0f);
            LobbySettingsProfile.MarkTutorialSeen();
            string firstUid = LobbySettingsProfile.PlayerUid;
            LobbySettingsProfile.Flush();

            Assert.That(firstUid, Does.Match("^MUK-[0-9A-F]{8}$"));
            Assert.That(lobbySettingsStore.SaveCount, Is.GreaterThanOrEqualTo(2));

            LobbySettingsProfile.UseStoreForTests(lobbySettingsStore);

            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.BgmResumeVolume,
                Is.EqualTo(0.35f).Within(0.001f),
                "음소거를 껐다 켜면 사용자가 마지막으로 고른 배경음 크기로 돌아가야 합니다.");
            Assert.That(
                LobbySettingsProfile.SfxResumeVolume,
                Is.EqualTo(0.6f).Within(0.001f),
                "음소거를 껐다 켜면 사용자가 마지막으로 고른 효과음 크기로 돌아가야 합니다.");
            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(
                LobbySettingsProfile.NeedsGameplayTutorial,
                Is.True,
                "과거 정적 가이드 완료 여부가 새 인터랙티브 안내 버전을 대신하면 안 됩니다.");
            Assert.That(LobbySettingsProfile.PlayerUid, Is.EqualTo(firstUid),
                "로컬 UID는 옵션 화면을 다시 열어도 바뀌면 안 됩니다.");
        }

        [Test]
        public void LegacyLobbyBackupReceivesTheSameRecordBasedMenuLayout()
        {
            viewHost = new GameObject(
                "LegacyLobbyCanvas",
                typeof(RectTransform),
                typeof(CanvasGroup));
            var recordRoot = new GameObject(
                "BestDisplay",
                typeof(RectTransform),
                typeof(RawImage));
            recordRoot.transform.SetParent(viewHost.transform, false);
            var recordLabelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            recordLabelObject.transform.SetParent(recordRoot.transform, false);
            var recordLabel = recordLabelObject.GetComponent<Text>();
            recordLabel.text = "최고 102";

            Button start = CreateLegacyButton(viewHost.transform, "StartButton", "시작");
            Button growth = CreateLegacyButton(viewHost.transform, "GrowthButton", "성장");
            var view = viewHost.AddComponent<LobbyView>();
            SetField(view, "bestText", recordLabel);
            SetField(view, "startButton", start);
            SetField(view, "growthButton", growth);
            SetField(view, "optionsButton", null);

            view.ApplyMenuLayoutForTests();

            AssertMenuLayout(view.StartButton, "시작", LobbyMenuLayout.StartAnchor);
            AssertMenuLayout(view.GrowthButton, "성장", LobbyMenuLayout.GrowthAnchor);
            AssertMenuLayout(view.OptionsButton, "옵션", LobbyMenuLayout.OptionsAnchor);
            Assert.That(LobbyMenuLayout.StartAnchor.x,
                Is.EqualTo(LobbyMenuLayout.MenuRailX));
            Assert.That(LobbyMenuLayout.GrowthAnchor.x,
                Is.EqualTo(LobbyMenuLayout.MenuRailX));
            Assert.That(LobbyMenuLayout.OptionsAnchor.x,
                Is.EqualTo(LobbyMenuLayout.MenuRailX));
            Assert.That(LobbyMenuLayout.RecordAnchor.x,
                Is.EqualTo(LobbyMenuLayout.RecordRailX));
            Assert.That(
                LobbyMenuLayout.RecordRailX,
                Is.EqualTo(LobbyMenuLayout.MenuRailX).Within(0.001f),
                "최고 기록 칸과 로비 메뉴는 같은 화면 중앙 레일을 사용해야 합니다.");
            Assert.That(
                LobbyMenuLayout.MenuRailX,
                Is.EqualTo(0.5f).Within(0.001f),
                "로비 메뉴 레일은 화면 중앙을 기준으로 해야 합니다.");
            float labelCenterAt1080 =
                LobbyMenuLayout.MenuRailX * 1080f +
                LobbyMenuLayout.ButtonPosition.x +
                LobbyMenuLayout.LabelPosition.x;
            Assert.That(
                labelCenterAt1080,
                Is.EqualTo(540f).Within(4f),
                "비대칭 붓 보정 뒤 라벨의 실제 중심은 화면 중앙이어야 합니다.");
            float recordLabelCenterAt1080 =
                LobbyMenuLayout.RecordRailX * 1080f +
                LobbyMenuLayout.RecordPosition.x +
                LobbyMenuLayout.LabelPosition.x;
            Assert.That(
                recordLabelCenterAt1080,
                Is.EqualTo(540f).Within(4f),
                "최고 기록 붓획의 라벨도 화면 중앙에 보여야 합니다.");
            Assert.That(
                LobbyMenuLayout.FontSize,
                Is.GreaterThanOrEqualTo(44));
            Assert.That(
                view.StartButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.GrowthButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.OptionsButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.StartButton.targetGraphic.color.a,
                Is.EqualTo(LobbyMenuLayout.PrimaryAlpha).Within(0.001f));
            Assert.That(
                view.GrowthButton.targetGraphic.color.a,
                Is.EqualTo(LobbyMenuLayout.SecondaryAlpha).Within(0.001f));
            Assert.That(
                view.OptionsButton.targetGraphic.color.a,
                Is.EqualTo(LobbyMenuLayout.SecondaryAlpha).Within(0.001f));

            AssertSelectedMenu(view, LobbyMenuSelection.Start);
            view.SetActiveMenu(LobbyMenuSelection.Growth);
            AssertSelectedMenu(view, LobbyMenuSelection.Growth);
            view.SetActiveMenu(LobbyMenuSelection.Options);
            AssertSelectedMenu(view, LobbyMenuSelection.Options);

            Assert.That(recordRoot.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.RecordPosition));
            Assert.That(recordLabel.rectTransform.anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.LabelPosition));
            Assert.That(recordLabel.fontSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(recordLabel.fontStyle, Is.EqualTo(FontStyle.Bold));
        }

        [Test]
        public void ClosingOptionsRestoresStartButtonEmphasis()
        {
            managerHost = new GameObject("LobbyOptionsSelectionManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            var options = managerHost.AddComponent<LobbyOptionsView>();
            options.BuildForTests();

            viewHost = new GameObject(
                "LobbyOptionsSelectionCanvas",
                typeof(RectTransform),
                typeof(CanvasGroup));
            Button start =
                CreateLegacyButton(viewHost.transform, "StartButton", "시작");
            Button growth =
                CreateLegacyButton(viewHost.transform, "GrowthButton", "성장");
            Button option =
                CreateLegacyButton(viewHost.transform, "OptionsButton", "옵션");
            var view = viewHost.AddComponent<LobbyView>();
            SetField(view, "startButton", start);
            SetField(view, "growthButton", growth);
            SetField(view, "optionsButton", option);
            view.ApplyMenuLayoutForTests();

            options.Open();
            Invoke(view, "RefreshMenuSelection");
            AssertSelectedMenu(view, LobbyMenuSelection.Options);

            options.Close();
            Invoke(view, "RefreshMenuSelection");
            AssertSelectedMenu(view, LobbyMenuSelection.Start);
        }

        [Test]
        public void ExplicitMenuStartReleasesLobbyPlayerExactlyOnce()
        {
            managerHost = new GameObject("LobbyStartManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            playerHost = new GameObject("LobbyStartPlayer");
            playerHost.AddComponent<SpriteRenderer>();
            var body = playerHost.AddComponent<Rigidbody2D>();
            playerHost.AddComponent<CircleCollider2D>();
            var player = playerHost.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            body.bodyType = RigidbodyType2D.Kinematic;
            manager.RegisterPlayer(player);

            manager.StartGameFromMenu();

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(manager.LivingPlayerCount, Is.EqualTo(1));

            manager.StartGameFromMenu();
            Assert.That(manager.State, Is.EqualTo(GameState.Playing),
                "시작 버튼 중복 탭은 새 세션 전환을 다시 실행하면 안 됩니다.");
            Assert.That(manager.LivingPlayerCount, Is.EqualTo(1));
        }

        [Test]
        public void PermanentGrowthCannotOpenAfterGameplayStarts()
        {
            managerHost = new GameObject("LobbyGrowthBoundaryManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyGrowthBoundaryView");
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(growthView.IsOpen, Is.False,
                "영구 성장 UI는 게임 시작 전 로비에서만 열려야 합니다.");
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static Button CreateLegacyButton(
            Transform parent,
            string objectName,
            string value)
        {
            var root = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(RawImage),
                typeof(Button));
            root.transform.SetParent(parent, false);
            var button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<RawImage>();
            var label = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            label.transform.SetParent(root.transform, false);
            label.GetComponent<Text>().text = value;
            return button;
        }

        static RectTransform RequireRect(Transform parent, string objectName)
        {
            var rect = parent.Find(objectName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(rect, $"{objectName} RectTransform이 필요합니다.");
            return rect;
        }

        static void AssertOptionButton(
            Transform parent,
            string objectName,
            bool usesActionBrush)
        {
            Transform target = parent.Find(objectName);
            Assert.IsNotNull(target, $"{objectName} 버튼이 필요합니다.");
            var rect = target.GetComponent<RectTransform>();
            var button = target.GetComponent<Button>();
            Assert.IsNotNull(rect);
            Assert.IsNotNull(button);
            Assert.That(rect.sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            Assert.IsNotNull(button.targetGraphic);
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            Assert.That(button.targetGraphic, Is.TypeOf<Image>());
            Assert.That(
                InkUiStyle.UsesActionButtonSprite(
                    button.targetGraphic as Image),
                Is.EqualTo(usesActionBrush),
                usesActionBrush
                    ? $"{objectName} 주 행동은 공용 붓획을 사용해야 합니다."
                    : $"{objectName} 카드·토글에는 공용 붓획을 사용하면 안 됩니다.");
            if (!usesActionBrush)
            {
                Assert.That(button.targetGraphic.name, Is.EqualTo("Paper"),
                    $"{objectName} 비활성 상태는 한지 면 전체에서 보여야 합니다.");
            }
        }

        static void AssertQuietOptionText(Text text)
        {
            Assert.IsNotNull(text);
            Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(text.resizeTextForBestFit, Is.False);
            var outline = text.GetComponent<Outline>();
            Assert.That(outline == null || !outline.enabled, Is.True,
                $"{text.name} 보조 문구에는 외곽선을 사용하면 안 됩니다.");
        }

        static void AssertMenuLayout(
            Button button,
            string expectedText,
            Vector2 expectedAnchor)
        {
            Assert.IsNotNull(button);
            var rect = button.GetComponent<RectTransform>();
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            Assert.That(rect.anchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(rect.anchorMax, Is.EqualTo(expectedAnchor));
            Assert.That(rect.anchoredPosition, Is.EqualTo(LobbyMenuLayout.ButtonPosition));
            Assert.That(rect.sizeDelta, Is.EqualTo(LobbyMenuLayout.BackgroundSize));
            Assert.IsNotNull(label);
            Assert.That(label.text, Is.EqualTo(expectedText));
            Assert.That(label.rectTransform.anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.LabelPosition));
            Assert.That(label.rectTransform.sizeDelta,
                Is.EqualTo(LobbyMenuLayout.LabelSize));
            Assert.That(label.fontSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Bold));
        }

        static void AssertSelectedMenu(
            LobbyView view,
            LobbyMenuSelection expected)
        {
            Assert.That(view.ActiveMenu, Is.EqualTo(expected));
            AssertMenuAlpha(
                view.StartButton,
                expected == LobbyMenuSelection.Start);
            AssertMenuAlpha(
                view.GrowthButton,
                expected == LobbyMenuSelection.Growth);
            AssertMenuAlpha(
                view.OptionsButton,
                expected == LobbyMenuSelection.Options);
        }

        static void AssertMenuAlpha(Button button, bool selected)
        {
            Assert.That(
                button.targetGraphic.color.a,
                Is.EqualTo(
                    selected
                        ? LobbyMenuLayout.PrimaryAlpha
                        : LobbyMenuLayout.SecondaryAlpha)
                    .Within(0.001f));
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
