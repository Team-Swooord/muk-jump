using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class LobbyDedicatedScreenTests
    {
        GameObject systemsHost;
        GameObject lobbyHost;
        GameManager manager;
        LobbyView lobby;
        PermanentGrowthView growth;
        LobbyScreenNavigator navigator;
        MemoryPermanentGrowthStore growthStore;

        [SetUp]
        public void SetUp()
        {
            growthStore = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(growthStore);

            systemsHost = new GameObject("DedicatedScreenSystems");
            manager = systemsHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            systemsHost.AddComponent<BrushTransitionView>();
            growth = systemsHost.AddComponent<PermanentGrowthView>();

            lobbyHost = new GameObject(
                "DedicatedScreenLobby",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(LobbyView));
            lobby = lobbyHost.GetComponent<LobbyView>();

            navigator = systemsHost.AddComponent<LobbyScreenNavigator>();
            growth.BuildForTests();
            navigator.BuildForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (lobbyHost != null)
                Object.DestroyImmediate(lobbyHost);
            if (systemsHost != null)
                Object.DestroyImmediate(systemsHost);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void GrowthBuildsAsDedicatedFullScreen()
        {
            Assert.That(growth.IsDedicatedScreen, Is.True);
            AssertDedicatedScreen(
                growth.ScreenRoot,
                growth.BackButton,
                "PermanentGrowthScreen");
            AssertOpenSelectionSurface(
                growth.ScreenRoot.Find(
                        "SafeAreaRoot/PermanentGrowthScreen/" +
                        "ChoiceGrid/GrowthCard1")
                    ?.GetComponent<Button>());

            Assert.That(
                systemsHost.transform.Find(
                    "PermanentGrowthCanvas/InkDim"),
                Is.Null);
            Assert.That(
                growth.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ScrollOutline"),
                Is.Null);
        }

        [Test]
        public void ScreenEntrySwapsOnlyAtBrushCover()
        {
            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(lobby.IsInteractive, Is.True);

            bool requested = navigator.OpenGrowth();

            Assert.That(requested, Is.True);
            Assert.That(navigator.IsTransitioning, Is.True);
            Assert.That(
                lobby.ActiveMenu,
                Is.EqualTo(LobbyMenuSelection.Growth));
            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.IsInteractive, Is.False);
            Assert.That(growth.IsOpen, Is.False);

            navigator.CompleteCoverForTests();

            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.PermanentGrowth));
            Assert.That(lobby.IsVisible, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(growth.ScreenRoot.anchoredPosition.y,
                Is.Zero.Within(0.001f));

            navigator.CompleteRevealForTests();

            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(growth.IsOpen, Is.True);
        }

        [Test]
        public void BackUsesSameTransitionAndRestoresLobbyInput()
        {
            Assert.That(navigator.OpenGrowth(), Is.True);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(growth.IsOpen, Is.True);

            growth.BackButton.onClick.Invoke();

            Assert.That(navigator.IsTransitioning, Is.True);
            Assert.That(lobby.ActiveMenu,
                Is.EqualTo(LobbyMenuSelection.Start));
            Assert.That(navigator.PendingSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(growth.ScreenRoot.anchoredPosition.y,
                Is.Zero.Within(0.001f));
            Assert.That(growth.IsOpen, Is.False);

            navigator.CompleteCoverForTests();
            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(lobby.ActiveMenu,
                Is.EqualTo(LobbyMenuSelection.Start));
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.IsInteractive, Is.False);
            Assert.That(growth.ScreenRoot.anchoredPosition.y,
                Is.EqualTo(1920f).Within(0.001f));

            navigator.CompleteRevealForTests();
            Assert.That(lobby.IsInteractive, Is.True);
            Assert.That(navigator.CanStartGame, Is.True);
        }

        [Test]
        public void RepeatedRequestsAreIgnoredDuringTransition()
        {
            Assert.That(navigator.OpenGrowth(), Is.True);
            Assert.That(navigator.OpenGrowth(), Is.False);
            Assert.That(navigator.ReturnToLobby(), Is.False);
            Assert.That(navigator.PendingSection,
                Is.EqualTo(
                    LobbyScreenNavigator.LobbySection.PermanentGrowth));

            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(navigator.OpenGrowth(), Is.False);
        }

        [Test]
        public void RecoveryStateBlocksStartAndRoutesToGrowthRecoveryPrompt()
        {
            growthStore.Json = "{broken json";
            growthStore.BackupJson =
                "{\"schemaVersion\":1,\"balanceVersion\":4," +
                "\"wallet\":2,\"spent\":1," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"settledRunIds\":[]," +
                "\"ranks\":[],\"ownedNodeIds\":[\"I00\"]," +
                "\"survivalKeystoneId\":\"\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();

            manager.StartGameFromMenu();
            Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
            Assert.That(navigator.CanStartGame, Is.False);

            Invoke(lobby, "HandleStartPressed");
            Assert.That(navigator.IsTransitioning, Is.True,
                "초기 로비에서 시작을 눌러도 복구 화면으로 보내야 합니다.");
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();

            Assert.That(growth.IsRecoveryPromptOpen, Is.True);
            Assert.That(growth.RestoreBackupButton.gameObject.activeSelf, Is.True);
            RectTransform recoveryOverlay = growth.ScreenRoot.Find(
                    "GrowthRecoveryPrompt")
                ?.GetComponent<RectTransform>();
            Image recoveryDim = recoveryOverlay?.GetComponent<Image>();
            Assert.That(recoveryOverlay, Is.Not.Null);
            Assert.That(recoveryDim, Is.Not.Null);
            Assert.That(recoveryOverlay.parent, Is.SameAs(growth.ScreenRoot));
            Assert.That(recoveryOverlay.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(recoveryOverlay.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(recoveryDim.raycastTarget, Is.True);
            Assert.That(recoveryDim.color.a,
                Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));
            growth.RestoreBackupButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(growth.IsRecoveryPromptOpen, Is.False);

            Assert.That(navigator.ReturnToLobby(), Is.True);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            manager.StartGameFromMenu();
            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void RecoveryResetRequiresTwoClicksThenAllowsSettlement()
        {
            growthStore.Json = "{broken json";
            growthStore.BackupJson = string.Empty;
            PermanentGrowthProfile.ResetCacheForTests();

            Assert.That(navigator.OpenGrowth(), Is.True);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(growth.IsRecoveryPromptOpen, Is.True);
            Assert.That(growth.RestoreBackupButton.gameObject.activeSelf, Is.False);

            growth.ResetGrowthSaveButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(growth.IsRecoveryPromptOpen, Is.True);
            Assert.That(
                growth.ResetGrowthSaveButton
                    .GetComponentInChildren<Text>(true).text,
                Is.EqualTo(GameLocalization.Translate("초기화 확인")));

            SetField(growth, "recoveryResetArmedAt", -1f);
            growth.ResetGrowthSaveButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(growth.IsRecoveryPromptOpen, Is.False);
            PermanentGrowthSettlement settlement =
                PermanentGrowthProfile.SettleRun(
                    "ui-reset-settlement", 0, (int)RunRewardCalculator.GetNextRewardDistance(0), 0, 0f, true);
            Assert.That(settlement.Accepted, Is.True);
            Assert.That(settlement.Earned, Is.EqualTo(1));
            PermanentGrowthProfile.ResetCacheForTests();
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(1));
        }

        [Test]
        public void RequestIsRejectedWhenSharedBrushTransitionIsBusy()
        {
            var transition = systemsHost.GetComponent<BrushTransitionView>();
            SetField(transition, "playing", true);

            Assert.That(navigator.OpenGrowth(), Is.False);
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.IsInteractive, Is.True);
        }

        [Test]
        public void FailedTransitionRestoresPreviousScreenAndInput()
        {
            Assert.That(navigator.OpenGrowth(), Is.True);
            navigator.FailTransitionForTests();

            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.IsInteractive, Is.True);
            Assert.That(growth.IsOpen, Is.False);
        }

        [Test]
        public void RequestsAreRejectedOutsideLobby()
        {
            Invoke(manager, "SetState", GameState.Playing);

            Assert.That(navigator.OpenGrowth(), Is.False);
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(lobby.IsInteractive, Is.False);
        }

        [Test]
        public void LeavingLobbyImmediatelyClosesDedicatedScreen()
        {
            Assert.That(navigator.OpenGrowth(), Is.True);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(growth.IsOpen, Is.True);

            Invoke(manager, "SetState", GameState.Playing);

            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(lobby.IsInteractive, Is.False);
        }

        static void AssertDedicatedScreen(
            RectTransform screenRoot,
            Button backButton,
            string contentName)
        {
            Assert.That(screenRoot, Is.Not.Null);
            Image backgroundBlocker = screenRoot
                .Find("LobbyBackgroundInputBlocker")
                ?.GetComponent<Image>();
            Assert.That(backgroundBlocker, Is.Not.Null);
            Assert.That(backgroundBlocker.color.a, Is.Zero.Within(0.001f),
                "성장 화면은 로비 월드 배경을 가리면 안 됩니다.");
            Assert.That(backgroundBlocker.raycastTarget, Is.True,
                "투명 배경은 숨은 로비 입력을 차단해야 합니다.");
            Assert.That(screenRoot.Find("OpaqueHanjiBackground"), Is.Null);
            Assert.That(screenRoot.Find("LeftInkWash"), Is.Null);
            Assert.That(screenRoot.Find("RightInkWash"), Is.Null);
            Assert.That(
                screenRoot.Find($"SafeAreaRoot/{contentName}"),
                Is.Not.Null);
            Assert.That(backButton, Is.Not.Null);
            Assert.That(
                backButton.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            Assert.That(backButton.targetGraphic, Is.Not.Null);
            Assert.That(backButton.targetGraphic.raycastTarget, Is.False);
            Image hit = backButton.GetComponent<Image>();
            Image icon = backButton.transform.Find("Icon").GetComponent<Image>();
            Assert.That(hit.sprite, Is.Null);
            Assert.That(hit.color.a, Is.Zero);
            Assert.That(hit.raycastTarget, Is.True);
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(84, 84)));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(backButton.targetGraphic, Is.SameAs(icon));
            Assert.That(backButton.navigation.mode,
                Is.EqualTo(Navigation.Mode.None));
            Assert.That(backButton.GetComponentsInChildren<Text>(true), Is.Empty);
        }

        static void AssertHanjiNavigationButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.TypeOf<Image>());
            Image hitArea = button.targetGraphic as Image;
            Assert.That(InkUiStyle.UsesActionButtonSprite(hitArea), Is.True);
            Assert.That(hitArea.sprite, Is.SameAs(InkUiStyle.ActionButtonSprite));
            Assert.That(hitArea.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(hitArea.color.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(button.transform.Find("PrimaryAccent"), Is.Null);
        }

        static void AssertOpenSelectionSurface(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var paper = button.transform.Find("Paper")?.GetComponent<Image>();
            Assert.That(paper, Is.Not.Null, "성장 선택지는 각각 독립된 한지 카드입니다.");
            Assert.That(paper.raycastTarget, Is.False);
            Assert.That(paper.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthPaperRibbonSprite()));
            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            Assert.That(icon, Is.SameAs(button.targetGraphic));
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(button.transform.Find("SelectionWash"), Is.Not.Null);
            Image hitArea = button.GetComponent<Image>();
            Assert.That(hitArea.raycastTarget, Is.True);
            Assert.That(hitArea.color.a, Is.Zero, "입력 영역은 투명하게 유지한다.");
            Assert.That(button.transform.Find("Icon"), Is.Not.Null);
            Assert.That(button.transform.Find("TabEffectSummary"), Is.Not.Null);
            Assert.That(button.transform.Find("SelectionInk"), Is.Not.Null);
        }

        static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            field.SetValue(target, value);
        }
    }
}
