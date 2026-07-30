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
        public void LobbySeparatesFourPermanentGrowthsFromHundredRunCodexEntries()
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
            Assert.That(growthView.CreatedRowCount, Is.EqualTo(4));
            Assert.That(growthView.BalanceLabel, Is.EqualTo("보유 먹빛 0"));
            Transform growthPanel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(growthPanel, Is.Not.Null);
            Assert.That(growthPanel.Find("InkTreeTrunk"), Is.Not.Null,
                "영구 성장 화면은 중앙 먹나무 기둥을 가져야 합니다.");
            var inkRoot =
                (RectTransform)growthPanel.Find("InkTreeRoot");
            Assert.That(inkRoot, Is.Not.Null);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(
                    growthPanel.Find($"GrowthBranch{i + 1}"),
                    Is.Not.Null,
                    $"영구 성장 {i + 1}의 먹가지 연결선이 필요합니다.");
                var card = (RectTransform)growthPanel.Find(
                    $"PermanentGrowth{i + 1}");
                Assert.That(card.sizeDelta.x, Is.GreaterThanOrEqualTo(384f));
                Assert.That(card.sizeDelta.y, Is.GreaterThanOrEqualTo(196f),
                    "성장 잎은 모바일에서 충분한 터치 높이를 유지해야 합니다.");
                var description = card.Find("Outline/Paper/Description")
                    .GetComponent<Text>();
                var effect = card.Find("Outline/Paper/Effect")
                    .GetComponent<Text>();
                var name = card.Find("Outline/Paper/Name")
                    .GetComponent<Text>();
                var level = card.Find("Outline/Paper/Level")
                    .GetComponent<Text>();
                Assert.That(
                    name.fontSize,
                    Is.GreaterThanOrEqualTo(44));
                Assert.That(
                    name.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(190f, 54f)));
                Assert.That(
                    level.fontSize,
                    Is.GreaterThanOrEqualTo(32));
                Assert.That(
                    level.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(190f, 40f)));
                Assert.That(
                    description.fontSize,
                    Is.GreaterThanOrEqualTo(34));
                Assert.That(
                    description.rectTransform.sizeDelta.y,
                    Is.GreaterThanOrEqualTo(44f));
                Assert.That(
                    description.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Overflow),
                    "카드 설명은 짧은 한 줄 문구로 유지해야 합니다.");
                Assert.That(effect.fontSize, Is.GreaterThanOrEqualTo(32));
                Assert.That(
                    effect.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(190f, 42f)));
            }
            var lowestCard = (RectTransform)growthPanel.Find(
                "PermanentGrowth4");
            float lowestCardBottom = lowestCard.anchoredPosition.y -
                                     lowestCard.sizeDelta.y * 0.5f;
            float rootTop = inkRoot.anchoredPosition.y +
                            inkRoot.sizeDelta.y * 0.5f;
            Assert.That(
                lowestCardBottom,
                Is.GreaterThan(rootTop + 4f),
                "마지막 성장 잎과 먹뿌리가 겹치면 안 됩니다.");
            var footer = (RectTransform)growthPanel.Find("PermanentHint");
            float rootBottom = inkRoot.anchoredPosition.y -
                               inkRoot.sizeDelta.y * 0.5f;
            float footerTop = footer.anchoredPosition.y +
                              footer.sizeDelta.y * 0.5f;
            Assert.That(
                rootBottom,
                Is.GreaterThan(footerTop + 4f),
                "먹뿌리와 하단 안내 문구가 겹치면 안 됩니다.");

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
            Assert.That(recordRoot.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.RecordPosition));
            Assert.That(recordLabel.rectTransform.anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.LabelPosition));
            Assert.That(recordLabel.fontSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(recordLabel.fontStyle, Is.EqualTo(FontStyle.Bold));
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
