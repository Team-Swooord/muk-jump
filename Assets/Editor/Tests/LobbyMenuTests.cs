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
        public void LobbySeparatesThreePermanentGrowthBranchesFromRunCodex()
        {
            managerHost = new GameObject("LobbyCollectionTestManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyCollectionTestHost");
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            var codexView = viewHost.AddComponent<LobbyCollectionView>();
            growthView.BuildForTests();
            codexView.BuildForTests();

            growthView.Open();
            Assert.That(growthView.IsOpen, Is.True);
            Assert.That(growthView.CreatedRowCount, Is.EqualTo(3));
            Assert.That(
                growthView.CreatedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.All.Count));
            Assert.That(growthView.BalanceLabel, Is.EqualTo("보유 먹빛 0"));
            Transform growthPanel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(growthPanel, Is.Not.Null);
            var viewport =
                (RectTransform)growthPanel.Find("TreeViewport");
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
            var inkRoot =
                (RectTransform)treeCanvas.Find("InkTreeRoot");
            Assert.That(inkRoot, Is.Not.Null);
            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches)
            {
                Transform header = treeCanvas.Find(
                    $"GrowthBranchHeader_{branch.Branch}");
                Assert.That(
                    header,
                    Is.Not.Null,
                    branch.DisplayName);
                Assert.That(
                    header.Find("Brush/BranchTitle")
                        ?.GetComponent<Text>()?.fontSize,
                    Is.GreaterThanOrEqualTo(36));
            }
            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
            {
                Transform node = treeCanvas.Find(
                    $"GrowthNode_{definition.Type}");
                Assert.That(node, Is.Not.Null, definition.Name);
                var rect = node.GetComponent<RectTransform>();
                Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(100f));
                Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(100f));
                Assert.That(node.GetComponent<Button>(), Is.Not.Null);
                Assert.That(
                    node.Find("NodeName")?.GetComponent<Text>()?.fontSize,
                    Is.GreaterThanOrEqualTo(30));
                RectTransform surface = node.Find("NodeSurface")
                    ?.GetComponent<RectTransform>();
                Assert.That(surface, Is.Not.Null);
                Assert.That(
                    surface.sizeDelta.x,
                    Is.EqualTo(surface.sizeDelta.y).Within(0.01f),
                    definition.Name);
            }
            var detailPanel = (RectTransform)growthPanel.Find(
                "SelectedGrowthDetail");
            Assert.That(
                detailPanel.sizeDelta,
                Is.EqualTo(new Vector2(920f, 330f)));
            foreach (string elementName in new[]
                     {
                         "DetailBranch",
                         "DetailName",
                         "DetailLevel",
                         "DetailDescription",
                         "CurrentEffect",
                         "NextEffect",
                         "Requirement",
                     })
            {
                var detailText = detailPanel.Find(elementName)
                    ?.GetComponent<Text>();
                Assert.That(detailText, Is.Not.Null, elementName);
                Assert.That(
                    detailText.alignment,
                    Is.EqualTo(TextAnchor.MiddleLeft),
                    elementName);
                Assert.That(
                    detailText.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Wrap),
                    elementName);
            }
            Assert.That(
                detailPanel.IsChildOf(treeCanvas),
                Is.False,
                "상세판은 나무를 드래그해도 고정되어야 합니다.");
            Assert.That(
                growthPanel.Find("PermanentHint"),
                Is.Not.Null);

            growthView.Close();
            codexView.OpenCodex();
            Assert.That(codexView.CurrentModeName, Is.EqualTo("Codex"));
            Assert.That(codexView.FilteredCount, Is.EqualTo(100));
            Assert.That(codexView.CreatedRowCount, Is.EqualTo(4),
                "100개 도감을 열 때도 고정된 큰 카드 네 개만 재사용해야 합니다.");
            Assert.That(codexView.IsCardBackVisible(0), Is.False);
            codexView.FlipCardForTests(0);
            Assert.That(codexView.IsCardBackVisible(0), Is.True,
                "도감 카드를 누르면 큰 그림 앞면에서 설명 뒷면으로 전환돼야 합니다.");
            codexView.FlipCardForTests(0);
            Assert.That(codexView.IsCardBackVisible(0), Is.False);

            codexView.Close();
            Assert.That(codexView.IsOpen, Is.False);
        }

        [Test]
        public void PermanentGrowthTreePansWithoutMovingFixedDetails()
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
            RectTransform detail = view.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/" +
                    "SelectedGrowthDetail")
                ?.GetComponent<RectTransform>();
            Assert.That(viewport, Is.Not.Null);
            Assert.That(treeCanvas, Is.Not.Null);
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(detail, Is.Not.Null);

            Vector2 detailPosition = detail.anchoredPosition;
            Vector2 initialTreePosition = treeCanvas.anchoredPosition;
            scrollRect.horizontalNormalizedPosition = 1f;
            scrollRect.verticalNormalizedPosition = 1f;

            Assert.That(
                treeCanvas.anchoredPosition,
                Is.Not.EqualTo(initialTreePosition),
                "큰 먹나무 지도만 양축으로 움직여야 합니다.");
            Assert.That(
                detail.anchoredPosition,
                Is.EqualTo(detailPosition),
                "선택 상세판은 나무 지도와 함께 움직이면 안 됩니다.");

            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
            {
                RectTransform node = treeCanvas.Find(
                        $"GrowthNode_{definition.Type}")
                    ?.GetComponent<RectTransform>();
                Assert.That(node, Is.Not.Null, definition.Name);
                minimumX = Mathf.Min(minimumX, node.anchoredPosition.x);
                maximumX = Mathf.Max(maximumX, node.anchoredPosition.x);
                minimumY = Mathf.Min(minimumY, node.anchoredPosition.y);
                maximumY = Mathf.Max(maximumY, node.anchoredPosition.y);
            }
            Assert.That(minimumX, Is.LessThan(-500f));
            Assert.That(maximumX, Is.GreaterThan(500f));
            Assert.That(maximumY - minimumY, Is.GreaterThan(900f));

            Assert.That(view.PurchaseButton.interactable, Is.True);
            view.PurchaseButton.onClick.Invoke();
            Assert.That(
                view.TreeScrollRect.enabled,
                Is.False,
                "열매 해금 연출 중에는 지도 관성·드래그를 잠가야 합니다.");
        }

        [Test]
        public void OptionsTutorialUsesFourSequentialPagesAndMarksCompletion()
        {
            viewHost = new GameObject("LobbyOptionsTestHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();

            optionsView.OpenTutorialForTests();

            Assert.That(optionsView.IsOpen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(optionsView.TutorialPageCount, Is.EqualTo(4));
            Assert.That(optionsView.CurrentTutorialPage, Is.EqualTo(0));
            Assert.That(optionsView.PlayerUidLabel,
                Does.StartWith("플레이어 UID   MUK-"));

            for (int expectedPage = 1; expectedPage < 4; expectedPage++)
            {
                Invoke(optionsView, "NextTutorialPage");
                Assert.That(optionsView.CurrentTutorialPage,
                    Is.EqualTo(expectedPage));
                Assert.That(optionsView.IsTutorialOpen, Is.True);
            }

            Invoke(optionsView, "NextTutorialPage");

            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.False,
                "네 번째 안내의 완료 버튼은 옵션 본문으로 돌아가야 합니다.");
            Assert.That(optionsView.IsOpen, Is.True);
        }

        [Test]
        public void OptionsUsesTwoColumnSupportLayoutWithTutorialBelowCustomerCenter()
        {
            managerHost = new GameObject("LobbyOptionsLayoutManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyOptionsLayoutHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();
            optionsView.Open();

            Transform page = viewHost.transform.Find(
                "LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage");
            Assert.IsNotNull(page);
            RectTransform uid = RequireRect(page, "UidButton");
            RectTransform bgm = RequireRect(page, "BgmCard");
            RectTransform sfx = RequireRect(page, "SfxCard");
            RectTransform support = RequireRect(page, "CustomerCenterButton");
            RectTransform tutorial = RequireRect(page, "GuideButton");

            Assert.That(uid.anchoredPosition.y, Is.GreaterThan(bgm.anchoredPosition.y));
            Assert.That(bgm.anchoredPosition.y, Is.EqualTo(sfx.anchoredPosition.y));
            Assert.That(bgm.anchoredPosition.x, Is.EqualTo(-sfx.anchoredPosition.x));
            Assert.That(support.anchoredPosition.x,
                Is.EqualTo(tutorial.anchoredPosition.x));
            Assert.That(support.anchoredPosition.y,
                Is.GreaterThan(tutorial.anchoredPosition.y),
                "튜토리얼은 고객센터 바로 아래 같은 열에 배치해야 합니다.");

            Text title = page.Find("Title")?.GetComponent<Text>();
            Text version = page.Find("Version")?.GetComponent<Text>();
            Assert.IsNotNull(title);
            Assert.IsNotNull(version);
            Assert.That(title.alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(version.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(
                title.rectTransform.anchoredPosition.y,
                Is.EqualTo(version.rectTransform.anchoredPosition.y),
                "설정 제목과 버전은 하나의 상단 정보 띠로 읽혀야 합니다.");
            Assert.That(
                title.rectTransform.anchoredPosition.x,
                Is.LessThan(version.rectTransform.anchoredPosition.x));

            AssertMajorOptionButton(page, "LanguageButton");
            AssertMajorOptionButton(page, "CustomerCenterButton");
            AssertMajorOptionButton(page, "AccountConnectButton");
            AssertMajorOptionButton(page, "GuideButton");
            AssertMajorOptionButton(page, "CloseButton");
            AssertMajorOptionButton(page, "UidButton");
            AssertMajorOptionButton(
                page.Find("BgmCard/Paper"),
                "Toggle");
            AssertMajorOptionButton(
                page.Find("SfxCard/Paper"),
                "Toggle");

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
            AssertMajorOptionButton(tutorialPage, "TutorialClose");
            AssertMajorOptionButton(tutorialPage, "PreviousButton");
            AssertMajorOptionButton(tutorialPage, "NextButton");

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
            Button codex = CreateLegacyButton(viewHost.transform, "CodexButton", "도감");
            var view = viewHost.AddComponent<LobbyView>();
            SetField(view, "bestText", recordLabel);
            SetField(view, "startButton", start);
            SetField(view, "growthButton", growth);
            SetField(view, "codexButton", codex);
            SetField(view, "optionsButton", null);

            view.ApplyMenuLayoutForTests();

            AssertMenuLayout(view.StartButton, "시작", LobbyMenuLayout.StartAnchor);
            AssertMenuLayout(view.GrowthButton, "성장", LobbyMenuLayout.GrowthAnchor);
            AssertMenuLayout(view.CodexButton, "도감", LobbyMenuLayout.CodexAnchor);
            AssertMenuLayout(view.OptionsButton, "옵션", LobbyMenuLayout.OptionsAnchor);
            Assert.That(LobbyMenuLayout.StartAnchor.x,
                Is.EqualTo(LobbyMenuLayout.MenuRailX));
            Assert.That(LobbyMenuLayout.GrowthAnchor.x,
                Is.EqualTo(LobbyMenuLayout.MenuRailX));
            Assert.That(LobbyMenuLayout.CodexAnchor.x,
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
                view.CodexButton.GetComponent<CanvasGroup>().alpha,
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
                view.CodexButton.targetGraphic.color.a,
                Is.EqualTo(LobbyMenuLayout.SecondaryAlpha).Within(0.001f));
            Assert.That(
                view.OptionsButton.targetGraphic.color.a,
                Is.EqualTo(LobbyMenuLayout.SecondaryAlpha).Within(0.001f));

            AssertSelectedMenu(view, LobbyMenuSelection.Start);
            view.SetActiveMenu(LobbyMenuSelection.Growth);
            AssertSelectedMenu(view, LobbyMenuSelection.Growth);
            view.SetActiveMenu(LobbyMenuSelection.Codex);
            AssertSelectedMenu(view, LobbyMenuSelection.Codex);
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
            Button codex =
                CreateLegacyButton(viewHost.transform, "CodexButton", "도감");
            Button option =
                CreateLegacyButton(viewHost.transform, "OptionsButton", "옵션");
            var view = viewHost.AddComponent<LobbyView>();
            SetField(view, "startButton", start);
            SetField(view, "growthButton", growth);
            SetField(view, "codexButton", codex);
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
        public void CodexUsesAsymmetricHeaderAboveTheCardGrid()
        {
            viewHost = new GameObject("LobbyCodexHierarchyHost");
            var codexView = viewHost.AddComponent<LobbyCollectionView>();
            codexView.BuildForTests();

            Transform panel = viewHost.transform.Find(
                "LobbyCollectionCanvas/ScreenRoot/SafeAreaRoot/CodexGallery");
            Assert.IsNotNull(panel);
            Text title = panel.Find("Title")?.GetComponent<Text>();
            Text subtitle = panel.Find("Subtitle")?.GetComponent<Text>();
            RectTransform category =
                panel.Find("CategoryButton") as RectTransform;
            RectTransform headerRule =
                panel.Find("HeaderStroke") as RectTransform;
            RectTransform firstCard =
                panel.Find("CodexCard1") as RectTransform;

            Assert.IsNotNull(title);
            Assert.IsNotNull(subtitle);
            Assert.IsNotNull(category);
            Assert.IsNotNull(headerRule);
            Assert.IsNotNull(firstCard);
            Assert.That(title.alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(subtitle.alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(title.rectTransform.anchoredPosition.x,
                Is.LessThan(0f));
            Assert.That(category.anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(
                category.anchoredPosition.y,
                Is.EqualTo(subtitle.rectTransform.anchoredPosition.y),
                "도감 설명과 계보 필터는 같은 상단 정보 띠에 있어야 합니다.");
            Assert.That(headerRule.anchoredPosition.y,
                Is.LessThan(subtitle.rectTransform.anchoredPosition.y));
            Assert.That(headerRule.anchoredPosition.y,
                Is.GreaterThan(
                    firstCard.anchoredPosition.y +
                    firstCard.sizeDelta.y * 0.5f));
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

        static void AssertMajorOptionButton(Transform parent, string objectName)
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
                Is.True,
                $"{objectName}은 공용 붓획 버튼을 사용해야 합니다.");
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
                view.CodexButton,
                expected == LobbyMenuSelection.Codex);
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
