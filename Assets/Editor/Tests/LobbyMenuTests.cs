using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
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
            ScoreManager.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void LobbyBuildsThreePermanentGrowthBranches()
        {
            {
                managerHost = new GameObject("PermanentGrowthV8Manager");
                var compactManager = managerHost.AddComponent<GameManager>();
                Invoke(compactManager, "OnEnable");
                viewHost = new GameObject("PermanentGrowthV8Host");
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                compactView.Open();

                Assert.That(compactView.IsOpen, Is.True);
                Assert.That(compactView.CreatedRowCount, Is.EqualTo(1));
                Assert.That(compactView.CreatedCardCount, Is.EqualTo(4));
                Assert.That(compactView.BalanceLabel, Is.EqualTo("0"));
                Assert.That(compactView.TreeViewport, Is.Null);
                Assert.That(compactView.TreeCanvas, Is.Null);
                Assert.That(compactView.TreeScrollRect, Is.Null);
                Assert.That(compactView.IsNodePopupOpen, Is.False);
                Transform panel = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen");
                Assert.That(panel.Find("HeaderGroup"), Is.Not.Null);
                Assert.That(panel.Find("FocusedGrowth"), Is.Not.Null);
                Assert.That(panel.Find("ChoiceGrid"), Is.Not.Null);
                Assert.That(panel.Find("BottomActions"), Is.Not.Null);
            }
        }

        [Test]
        public void PermanentGrowthTreePansUntilFixedNodePopupOpens()
        {
            {
                managerHost = new GameObject("GrowthCardClickManager");
                var compactManager = managerHost.AddComponent<GameManager>();
                Invoke(compactManager, "OnEnable");
                PermanentGrowthProfile.DebugRefillCurrency();
                viewHost = new GameObject("GrowthCardClickHost");
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform screen = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen");
                Transform grid = screen.Find("ChoiceGrid");

                grid.Find("GrowthCard3").GetComponent<Button>().onClick.Invoke();
                Assert.That(compactView.SelectedNodeId, Is.EqualTo("jump"));
                Assert.That(PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.JumpHeight), Is.Zero);
                compactView.PurchaseButton.onClick.Invoke();
                Assert.That(PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.JumpHeight), Is.EqualTo(1));
            }
        }

        [Test]
        public void EveryGrowthNodeOpensReadableFixedDetailPopup()
        {
            {
                managerHost = new GameObject("GrowthCardCopyManager");
                var compactManager = managerHost.AddComponent<GameManager>();
                Invoke(compactManager, "OnEnable");
                viewHost = new GameObject("GrowthCardCopyHost");
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform screen = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen");
                Transform grid = screen.Find("ChoiceGrid");
                string[] names =
                {
                    "튼튼한 먹", "넉넉한 먹물", "알뜰한 붓", "높은 도약",
                };
                string[] summaries =
                {
                    "부딪혀도 더 오래 버텨요.",
                    "먹물을 넉넉히 담아 둘 수 있어요.",
                    "그릴 때 먹물이 천천히 줄어요.",
                    "더 높은 곳까지 뛰어요.",
                };
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(grid.Find($"GrowthCard{i}")
                        .GetComponent<Button>(), Is.Not.Null);
                    compactView.SelectGrowthForTests(i);
                    Text title = screen.Find("FocusedGrowth/FocusTitle")
                        .GetComponent<Text>();
                    Text summary = screen.Find("FocusedGrowth/FocusSummary")
                        .GetComponent<Text>();
                    Assert.That(title.text, Is.EqualTo(names[i]));
                    Assert.That(summary.text, Is.EqualTo(summaries[i]));
                    Assert.That(summary.text, Does.Not.Match("[0-9%+→]"));
                }
                Assert.That(compactView.IsNodePopupOpen, Is.False);
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
            // Canvas 스케일의 부동소수 반올림으로 0.02 논리 픽셀 정도 흔들릴 수 있다.
            const float Tolerance = 1.1f;
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
        public void OptionsTutorialUsesThreeSequentialPagesWithoutCompletingFirstRun()
        {
            viewHost = new GameObject("LobbyOptionsTestHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();

            bool seen = LobbySettingsProfile.TutorialSeen;
            optionsView.OpenTutorialForTests();

            Assert.That(optionsView.IsOpen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(
                optionsView.TutorialPageCount,
                Is.EqualTo(GameplayTutorialCatalog.Count));
            Assert.That(optionsView.CurrentTutorialPage, Is.EqualTo(0));
            Assert.That(optionsView.TutorialPageCount, Is.EqualTo(3));
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

            Assert.That(LobbySettingsProfile.TutorialSeen, Is.EqualTo(seen));
            Assert.That(optionsView.CurrentTutorialPage, Is.EqualTo(2));
            Assert.That(optionsView.IsTutorialOpen, Is.True,
                "마지막 >는 비활성화하며 바깥 dim으로만 옵션에 돌아갑니다.");
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
            var bgm = RequireRect(page, "BgmToggle");
            var sfx = RequireRect(page, "SfxToggle");
            var haptics = RequireRect(page, "HapticsToggle");
            Assert.That(page.Find("MotionToggle"), Is.Null);
            Assert.That(bgm.anchoredPosition.y, Is.EqualTo(sfx.anchoredPosition.y));
            Assert.That(haptics.anchoredPosition.y, Is.EqualTo(sfx.anchoredPosition.y));
            Assert.That(bgm.anchoredPosition.x, Is.LessThan(sfx.anchoredPosition.x));
            Assert.That(sfx.anchoredPosition.x, Is.LessThan(haptics.anchoredPosition.x));
            var support = RequireRect(page, "CustomerCenterButton");
            var tutorial = RequireRect(page, "GuideButton");
            Assert.That(support.anchoredPosition.y, Is.GreaterThan(tutorial.anchoredPosition.y));
            Assert.That(tutorial.anchoredPosition.x, Is.LessThan(support.anchoredPosition.x));
            Assert.That(page.Find("UidButton"), Is.Null);
            Assert.That(page.Find("UuidButton/Label"), Is.Not.Null);
            Assert.That(page.Find("LanguageButton"), Is.Not.Null);
            Assert.That(page.Find("AudioSettingsButton"), Is.Null);
            Assert.That(page.Find("AdPrivacyButton"), Is.Null);
            Assert.That(page.Find("DebugScenarioButton"), Is.Null);
            Assert.That(page.parent.GetComponent<HanjiScrollFrame>(), Is.Not.Null);
            Assert.That(page.Find("TermsButton"), Is.Not.Null);
            Assert.That(page.Find("PrivacyButton"), Is.Not.Null);

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

            AssertOptionButton(page, "CustomerCenterButton", usesActionBrush: false);
            AssertOptionButton(page, "GuideButton", usesActionBrush: false);
            AssertOptionButton(page, "AccountButton", usesActionBrush: false);
            AssertOptionButton(page, "LanguageButton", usesActionBrush: false);
            AssertOptionButton(page, "CloseButton", usesActionBrush: true);
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Bold));
            AssertQuietOptionText(page.Find("Version")?.GetComponent<Text>());
            AssertQuietOptionText(page.Find("ConnectionStatus")?.GetComponent<Text>());

            Transform accountPage = page.parent.Find("AccountPage");
            Transform leaderboardPage = page.parent.Find("LeaderboardPage");
            Transform playSettingsPage = page.parent.Find("PlaySettingsPage");
            Transform debugScenarioPage = page.parent.Find("DebugScenarioPage");
            Assert.That(accountPage, Is.Not.Null);
            Assert.That(leaderboardPage, Is.Not.Null);
            Assert.That(playSettingsPage, Is.Not.Null);
            Assert.That(debugScenarioPage, Is.Null);
            foreach (string conflictName in new[]
                     {
                         "AccountConflict",
                         "SyncConflict",
                         "AccountSyncPending",
                     })
            {
                RectTransform conflict = accountPage.Find(conflictName)
                    ?.GetComponent<RectTransform>();
                Image blocker = conflict?.Find("ModalBlocker")
                    ?.GetComponent<Image>();
                Assert.That(conflict, Is.Not.Null, conflictName);
                Assert.That(conflict.sizeDelta.x, Is.GreaterThanOrEqualTo(760f));
                float paperHeight = (float)typeof(LobbyOptionsView).GetField(
                    "accountPaperHeight", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(optionsView);
                Assert.That(conflict.sizeDelta.y, Is.EqualTo(paperHeight + 60f),
                    "계정 선택 두루마리도 현재 내용 높이에 맞아야 합니다.");
                Assert.That(blocker, Is.Not.Null, conflictName);
                Assert.That(blocker.raycastTarget, Is.True,
                    "충돌 선택 전에는 뒤쪽 계정 메뉴를 누를 수 없어야 합니다.");
                Assert.That(conflict.GetComponent<HanjiScrollFrame>(), Is.Not.Null);
                Assert.That(conflict.Find("HanjiScrollArt/Paper").GetComponent<HanjiScrollPaperGraphic>().color.a, Is.EqualTo(1));
            }
            AssertOptionButton(
                playSettingsPage,
                "HapticsButton",
                usesActionBrush: false);
            Assert.That(playSettingsPage.Find("ReducedMotionButton"), Is.Null);
            Assert.That(playSettingsPage.Find("AdConsentButton"), Is.Null);
            AssertOptionButton(
                accountPage,
                "CopySupportCode",
                usesActionBrush: false);
            AssertOptionButton(
                accountPage.Find("AccountConflict"),
                "UseExistingAccount",
                usesActionBrush: true);
            AssertOptionButton(
                accountPage.Find("AccountConflict"),
                "KeepGuestAccount",
                usesActionBrush: true);
            AssertOptionButton(
                accountPage.Find("SyncConflict"),
                "UseServerRecord",
                usesActionBrush: true);
            AssertOptionButton(
                accountPage.Find("SyncConflict"),
                "UseDeviceRecord",
                usesActionBrush: true);
            AssertOptionButton(
                accountPage.Find("AccountSyncPending"),
                "RetryAccountSync",
                usesActionBrush: true);
            AssertOptionButton(
                accountPage.Find("AccountSyncPending"),
                "ReturnToLocalGuest",
                usesActionBrush: true);
            Assert.That(leaderboardPage.Find("LeaderboardRefresh"), Is.Null,
                "실제 순위 창에 별도 새로고침 버튼을 노출하지 않습니다.");
            Assert.That(
                leaderboardPage.Find("LeaderboardRow1")?.GetComponent<Text>(),
                Is.Not.Null);
            Assert.That(
                leaderboardPage.Find("LeaderboardRow10")?.GetComponent<Text>(),
                Is.Not.Null);

            RectTransform bgmSlider = RequireRect(playSettingsPage.Find("BgmCard/Paper"), "Slider");
            RectTransform bgmToggle = RequireRect(playSettingsPage.Find("BgmCard/Paper"), "Toggle");
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
            Assert.That(status.text, Does.Contain("cysbandcs@gmail.com"));

            page.Find("GuideButton")?.GetComponent<Button>()?.onClick.Invoke();
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(
                page.parent.Find("TutorialPage")
                    ?.GetComponent<CanvasGroup>()
                    ?.blocksRaycasts,
                Is.True);
            Transform tutorialPage = page.parent.Find("TutorialPage");
            Assert.That(tutorialPage.Find("TutorialClose"), Is.Null);
            foreach (string name in new[] { "PreviousButton", "NextButton" })
            {
                var arrow = tutorialPage.Find(name);
                Assert.That(arrow.GetComponent<Button>(), Is.Not.Null);
                Assert.That(arrow.GetComponent<Image>().color.a, Is.Zero);
                Assert.That(arrow.Find("Icon").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(arrow.Find("Paper"), Is.Null);
                Assert.That(arrow.GetComponentsInChildren<Text>(), Is.Empty);
            }
            AssertQuietOptionText(
                tutorialPage.Find("TutorialDescription")?.GetComponent<Text>());
            Assert.That(
                tutorialPage.Find("TutorialDescription")?.GetComponent<Text>()?.alignment,
                Is.EqualTo(TextAnchor.MiddleCenter));
            RectTransform tutorialDescription = tutorialPage
                .Find("TutorialDescription") as RectTransform;
            Assert.That(tutorialDescription, Is.Not.Null);
            Assert.That(tutorialDescription.sizeDelta,
                Is.EqualTo(new Vector2(700f, 320f)));

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
            LobbySettingsProfile.SetHapticsEnabled(false);
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            LobbySettingsProfile.MarkTutorialSeen();
            string firstUid = LobbySettingsProfile.PlayerUid;
            LobbySettingsProfile.Flush();

            Assert.That(firstUid, Does.Match("^MUK-[0-9A-F]{8}$"));
            Assert.That(lobbySettingsStore.SaveCount, Is.GreaterThanOrEqualTo(2));

            LobbySettingsProfile.UseStoreForTests(lobbySettingsStore);

            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(LobbySettingsProfile.HapticsEnabled, Is.False);
            Assert.That(LobbySettingsProfile.ReducedMotionEnabled, Is.True);
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
        public void LobbySettingsSaveFailureDoesNotTrapOptionsOverlay()
        {
            managerHost = new GameObject("LobbySaveFailureManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbySaveFailureOptions");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();
            optionsView.Open();
            Assert.That(optionsView.IsOpen, Is.True);

            lobbySettingsStore.ThrowOnSave = true;
            LogAssert.Expect(
                LogType.Warning,
                "[MukJump] 로비 설정을 저장하지 못해 다음 기회에 " +
                "다시 시도합니다: Injected lobby settings save failure");

            Assert.DoesNotThrow(optionsView.Close);
            Assert.That(optionsView.IsOpen, Is.False,
                "저장 실패 뒤에도 옵션 오버레이가 입력을 놓아야 합니다.");

            lobbySettingsStore.ThrowOnSave = false;
            Assert.That(LobbySettingsProfile.TryFlush(), Is.True,
                "다음 저장 기회에는 보류된 메모리 값을 다시 내구 저장해야 합니다.");
        }

        [Test]
        public void PlaySettingsPageTogglesHapticsWithoutExposingRemovedMotionOption()
        {
            managerHost = new GameObject("PlaySettingsManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("PlaySettingsHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();
            optionsView.Open();

            Transform pages = viewHost.transform.Find(
                "LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
            Transform optionsPage = pages?.Find("OptionsPage");
            Transform playPage = pages?.Find("PlaySettingsPage");
            Assert.That(optionsPage, Is.Not.Null);
            Assert.That(playPage, Is.Not.Null);

            Invoke(optionsView, "ShowPlaySettingsPage");
            Assert.That(
                playPage.GetComponent<CanvasGroup>().blocksRaycasts,
                Is.True);
            Assert.That(
                optionsPage.GetComponent<CanvasGroup>().blocksRaycasts,
                Is.False);

            playPage.Find("HapticsButton")
                ?.GetComponent<Button>()?.onClick.Invoke();
            Assert.That(playPage.Find("ReducedMotionButton"), Is.Null);

            Assert.That(LobbySettingsProfile.HapticsEnabled, Is.False);
            Assert.That(
                playPage.Find("HapticsButton/Paper/Status")
                    ?.GetComponent<Text>()?.text,
                Is.EqualTo("꺼짐"));
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

            Assert.That(recordRoot.GetComponent<Button>(), Is.SameAs(view.LeaderboardButton));
            Assert.That(recordRoot.GetComponent<RawImage>().raycastTarget, Is.True);
            Assert.That(view.LeaderboardButton, Is.Not.Null);
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
                "비대칭 먹물 버튼 라벨의 실제 중심은 화면 중앙이어야 합니다.");
            float recordContentLeft = LobbyMenuLayout.LeaderboardPosition.x -
                                      LobbyMenuLayout.LeaderboardIconSize.x * .5f;
            float recordContentRight = LobbyMenuLayout.RecordLabelPosition.x +
                                       LobbyMenuLayout.RecordLabelSize.x * .5f;
            float recordContentCenterAt1080 =
                LobbyMenuLayout.RecordRailX * 1080f +
                LobbyMenuLayout.RecordPosition.x +
                (recordContentLeft + recordContentRight) * .5f;
            Assert.That(
                recordContentCenterAt1080,
                Is.EqualTo(551f).Within(4f),
                "텍스트를 왼쪽, 아이콘을 안쪽으로 당긴 최종 배치를 유지한다.");
            Assert.That(
                LobbyMenuLayout.FontSize,
                Is.EqualTo(46));
            Assert.That(
                view.StartButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.GrowthButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.OptionsButton.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(1f).Within(0.001f));
            AssertMenuAlpha(view.StartButton, true);
            AssertMenuAlpha(view.GrowthButton, false);
            AssertMenuAlpha(view.OptionsButton, false);

            AssertSelectedMenu(view, LobbyMenuSelection.Start);
            view.SetActiveMenu(LobbyMenuSelection.Growth);
            AssertSelectedMenu(view, LobbyMenuSelection.Growth);
            view.SetActiveMenu(LobbyMenuSelection.Options);
            AssertSelectedMenu(view, LobbyMenuSelection.Options);

            Assert.That(recordRoot.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.RecordPosition));
            Assert.That(recordLabel.rectTransform.anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.RecordLabelPosition));
            Assert.That(recordLabel.rectTransform.sizeDelta,
                Is.EqualTo(LobbyMenuLayout.RecordLabelSize));
            Assert.That(recordLabel.fontSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(recordLabel.fontStyle, Is.EqualTo(FontStyle.Bold));
        }

        [Test]
        public void CommittedBestRefreshesLobbyAndSurvivesLobbyRecreation()
        {
            var scoreStore = new MemoryScoreStore { Best = 25 };
            ScoreManager.UseStoreForTests(scoreStore);
            managerHost = new GameObject("LobbyBestScoreManager");
            var score = managerHost.AddComponent<ScoreManager>();
            Invoke(score, "OnEnable");
            Invoke(score, "Awake");
            Text firstBest = BuildBestOnlyLobby("FirstLobbyBest");

            Assert.That(firstBest.text, Is.EqualTo("최고 25m"));
            Assert.That(score.TryCommitBestCandidate(74), Is.True);
            Assert.That(firstBest.text, Is.EqualTo("최고 74m"),
                "결과 저장 완료 이벤트가 현재 로비 기록 문구를 즉시 갱신해야 합니다.");

            Object.DestroyImmediate(viewHost);
            viewHost = null;
            Object.DestroyImmediate(managerHost);
            managerHost = new GameObject("ReloadedLobbyBestScoreManager");
            var reloadedScore = managerHost.AddComponent<ScoreManager>();
            Invoke(reloadedScore, "OnEnable");
            Invoke(reloadedScore, "Awake");
            Text reloadedBest = BuildBestOnlyLobby("ReloadedLobbyBest");

            Assert.That(scoreStore.Best, Is.EqualTo(74));
            Assert.That(reloadedScore.Best, Is.EqualTo(74));
            Assert.That(reloadedBest.text, Is.EqualTo("최고 74m"),
                "씬이 다시 만들어져도 내구 저장된 전판 최고 기록을 표시해야 합니다.");
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

        [Test]
        public void MainLeaderboardIconOpensScrollAndBackdropReturnsToMain()
        {
            managerHost = new GameObject("LeaderboardManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LeaderboardLobby", typeof(RectTransform), typeof(CanvasGroup));
            var record = new GameObject("BestDisplay", typeof(RectTransform), typeof(RawImage));
            record.transform.SetParent(viewHost.transform, false);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(record.transform, false);
            var options = viewHost.AddComponent<LobbyOptionsView>();
            var lobby = viewHost.AddComponent<LobbyView>();
            SetField(lobby, "bestText", labelObject.GetComponent<Text>());
            SetField(lobby, "optionsView", options);
            Invoke(lobby, "Start");
            var shortcut = lobby.LeaderboardButton;
            Assert.That(shortcut, Is.Not.Null);
            Assert.That(record.GetComponent<Button>(), Is.SameAs(shortcut));
            Assert.That(record.GetComponent<RawImage>().raycastTarget, Is.True);
            Assert.That(shortcut.targetGraphic, Is.SameAs(record.GetComponent<RawImage>()));
            Assert.That(shortcut.GetComponent<InkUiPressFeedback>(), Is.Not.Null,
                "월계관과 기록을 포함한 붓패널 전체가 함께 눌려야 한다");
            Assert.That(shortcut.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(1),
                "기존 최고 기록만 남기고 순위 글자는 추가하지 않는다");
            var icon = shortcut.transform.Find("LeaderboardIcon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.EqualTo(Resources.Load<Sprite>(LobbyMenuLayout.LeaderboardIconResourcePath)));
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.preserveAspect, Is.True);
            var hit = (RectTransform)shortcut.transform;
            Assert.That(hit.sizeDelta, Is.EqualTo(LobbyMenuLayout.BackgroundSize));
            Assert.That(icon.rectTransform.anchoredPosition, Is.EqualTo(LobbyMenuLayout.LeaderboardPosition),
                "최고 기록 패널 안쪽으로 옮긴 월계관 위치를 유지한다");
            Assert.That(icon.rectTransform.anchoredPosition.x + icon.rectTransform.rect.xMin,
                Is.GreaterThan(hit.rect.xMin));
            Assert.That(icon.rectTransform.anchoredPosition.y, Is.EqualTo(LobbyMenuLayout.RecordLabelPosition.y));
            Assert.That(labelObject.GetComponent<Text>().raycastTarget, Is.False,
                "기록 글자도 부모 붓버튼의 입력을 가로채지 않는다");
            Assert.That(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<
                UnityEngine.EventSystems.IPointerClickHandler>(labelObject), Is.SameAs(record));
            Assert.That(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<
                UnityEngine.EventSystems.IPointerClickHandler>(icon.gameObject), Is.SameAs(record));
            lobby.ApplyMenuLayoutForTests();
            Assert.That(record.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(1), "재배치로 아이콘을 중복 생성하지 않는다");
            shortcut.onClick.Invoke();
            Transform page = viewHost.transform.Find(
                "LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            Assert.That(options.IsOpen, Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(page.Find("LeaderboardDone"), Is.Null);
            Canvas.ForceUpdateCanvases();
            var dim = viewHost.transform.Find("LobbyOptionsCanvas/InkDim")
                .GetComponent<UnityEngine.EventSystems.EventTrigger>();
            dim.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null)
            {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                position = new Vector2(-100, -100),
            });
            Assert.That(options.IsOpen, Is.False);
            PointerInput.ResetSuppressionForTests();
        }

        [Test]
        public void MainLeaderboardRecordReplacesDetachedLegacyShortcutWithoutDuplicateInput()
        {
            viewHost = new GameObject("LegacyBestDisplay", typeof(RectTransform), typeof(RawImage), typeof(Button));
            var oldRecordButton = viewHost.GetComponent<Button>();
            oldRecordButton.enabled = false;
            oldRecordButton.interactable = false;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(viewHost.transform, false);
            var detached = new GameObject("LeaderboardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            detached.transform.SetParent(viewHost.transform, false);

            Button first = LobbyMenuLayout.EnsureLeaderboardShortcut(labelObject.GetComponent<Text>());
            Button second = LobbyMenuLayout.EnsureLeaderboardShortcut(labelObject.GetComponent<Text>());

            Assert.That(first, Is.SameAs(oldRecordButton));
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.enabled && first.interactable, Is.True);
            Assert.That(detached.activeSelf, Is.False);
            Assert.That(viewHost.GetComponentsInChildren<Button>(), Has.Length.EqualTo(1));
            Assert.That(viewHost.GetComponentsInChildren<Image>(), Has.Length.EqualTo(1),
                "월계관은 하나만 생성하고 구 분리 버튼은 표시하지 않는다");
        }

        [TestCase(GameLanguage.Korean, "최고 —")]
        [TestCase(GameLanguage.English, "최고 —")]
        [TestCase(GameLanguage.Korean, "최고 999999m")]
        [TestCase(GameLanguage.English, "최고 999999m")]
        public void MainLeaderboardRecordFitsLocalizedScoreOnOneLine(GameLanguage language, string source)
        {
            GameLocalization.SetLanguage(language);
            viewHost = new GameObject("BestDisplay", typeof(RectTransform), typeof(RawImage));
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(viewHost.transform, false);
            var label = labelObject.GetComponent<Text>();
            InkLocalizedText.SetSource(label, source);
            LobbyMenuLayout.EnsureLeaderboardShortcut(label);

            Assert.That(label.resizeTextForBestFit, Is.True);
            Assert.That(label.resizeTextMinSize, Is.EqualTo(32));
            Assert.That(label.resizeTextMaxSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(source)));
            var generator = label.cachedTextGenerator;
            generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
            Assert.That(generator.lineCount, Is.EqualTo(1), "긴 기록도 아이콘 옆 한 줄 안에 들어가야 한다");
        }

        Text BuildBestOnlyLobby(string objectName)
        {
            viewHost = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasGroup));
            var labelObject = new GameObject(
                "BestText",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(viewHost.transform, false);
            Text label = labelObject.GetComponent<Text>();
            var view = viewHost.AddComponent<LobbyView>();
            SetField(view, "bestText", label);
            Invoke(view, "Start");
            return label;
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
            Assert.That(button.targetGraphic.raycastTarget ||
                button.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(button.targetGraphic, Is.TypeOf<Image>());
            Assert.That(
                InkUiStyle.UsesActionButtonSprite(
                    button.targetGraphic as Image),
                Is.True,
                $"{objectName} 행동·설정 행 모두 공통 한지 재질을 사용해야 합니다.");
            if (!usesActionBrush)
            {
                Assert.That(button.targetGraphic.name, Is.EqualTo("Paper"),
                    $"{objectName} 비활성 상태는 한지 면 전체에서 보여야 합니다.");
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero,
                    $"{objectName} 한지 뒤에 기본 사각 배경이 남으면 안 됩니다.");
                Assert.That(button.targetGraphic.GetComponent<Outline>(), Is.Null);
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
            Assert.That(label.color, Is.EqualTo(InkPalette.TextLight));
            RawImage legacyBackground = button.GetComponent<RawImage>();
            Assert.That(legacyBackground, Is.Not.Null);
            Assert.That(legacyBackground.enabled, Is.True);
            Assert.That(legacyBackground.raycastTarget, Is.True);
            Assert.That(button.targetGraphic, Is.SameAs(legacyBackground));
            Assert.That(
                button.GetComponentsInChildren<InkActionButtonVisual>(false),
                Is.Empty);
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
            Assert.That(button.targetGraphic, Is.TypeOf<RawImage>());
            Assert.That(
                button.targetGraphic.color.a,
                Is.EqualTo(selected
                    ? LobbyMenuLayout.PrimaryAlpha
                    : LobbyMenuLayout.SecondaryAlpha).Within(0.001f));
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
