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
                "PermanentGrowthCanvas/SafeAreaRoot/PermanentGrowthScroll");
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
                Assert.That(description.fontSize, Is.GreaterThanOrEqualTo(26));
                Assert.That(
                    description.rectTransform.sizeDelta.y,
                    Is.GreaterThanOrEqualTo(52f),
                    "성장 설명은 두 줄을 담을 높이가 필요합니다.");
                Assert.That(effect.fontSize, Is.GreaterThanOrEqualTo(26));
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

            AssertMajorOptionButton(page, "LanguageButton");
            AssertMajorOptionButton(page, "CustomerCenterButton");
            AssertMajorOptionButton(page, "AccountConnectButton");
            AssertMajorOptionButton(page, "GuideButton");
            AssertMajorOptionButton(page, "CloseButton");

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
            Assert.That(recordRoot.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.RecordPosition));
            Assert.That(recordLabel.rectTransform.anchoredPosition,
                Is.EqualTo(LobbyMenuLayout.LabelPosition));
            Assert.That(recordLabel.fontSize, Is.EqualTo(LobbyMenuLayout.FontSize));
            Assert.That(recordLabel.fontStyle, Is.EqualTo(FontStyle.Bold));
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
