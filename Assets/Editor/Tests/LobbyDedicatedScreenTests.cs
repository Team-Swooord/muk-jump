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
        LobbyCollectionView codex;
        LobbyScreenNavigator navigator;

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());

            systemsHost = new GameObject("DedicatedScreenSystems");
            manager = systemsHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            systemsHost.AddComponent<BrushTransitionView>();
            growth = systemsHost.AddComponent<PermanentGrowthView>();
            codex = systemsHost.AddComponent<LobbyCollectionView>();

            lobbyHost = new GameObject(
                "DedicatedScreenLobby",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(LobbyView));
            lobby = lobbyHost.GetComponent<LobbyView>();

            navigator = systemsHost.AddComponent<LobbyScreenNavigator>();
            growth.BuildForTests();
            codex.BuildForTests();
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
        public void GrowthAndCodexBuildAsDedicatedFullScreens()
        {
            Assert.That(growth.IsDedicatedScreen, Is.True);
            Assert.That(codex.IsDedicatedScreen, Is.True);
            AssertDedicatedScreen(
                growth.ScreenRoot,
                growth.BackButton,
                "PermanentGrowthScreen");
            AssertDedicatedScreen(
                codex.ScreenRoot,
                codex.BackButton,
                "CodexGallery");
            Transform codexGallery = codex.ScreenRoot.Find(
                "SafeAreaRoot/CodexGallery");
            AssertSharedActionButton(
                codexGallery?.Find("CategoryButton")?.GetComponent<Button>());
            AssertSharedActionButton(
                codexGallery?.Find("PreviousButton")?.GetComponent<Button>());
            AssertSharedActionButton(
                codexGallery?.Find("NextButton")?.GetComponent<Button>());
            AssertSemanticSelectionSurface(
                growth.ScreenRoot.Find(
                        "SafeAreaRoot/PermanentGrowthScreen/" +
                        "TreeViewport/TreeCanvas/" +
                        "GrowthNode_permanent_ink_capacity_rank_1")
                    ?.GetComponent<Button>());
            AssertSemanticSelectionSurface(
                codexGallery?.Find("CodexCard1/HitSurface")
                    ?.GetComponent<Button>());

            Assert.That(
                systemsHost.transform.Find(
                    "PermanentGrowthCanvas/InkDim"),
                Is.Null);
            Assert.That(
                systemsHost.transform.Find(
                    "LobbyCollectionCanvas/InkDim"),
                Is.Null);
            Assert.That(
                growth.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ScrollOutline"),
                Is.Null);
            Assert.That(
                codex.ScreenRoot.Find(
                    "SafeAreaRoot/CodexGallery/ScrollOutline"),
                Is.Null);
        }

        [TestCase(LobbyScreenNavigator.LobbySection.PermanentGrowth)]
        [TestCase(LobbyScreenNavigator.LobbySection.Codex)]
        public void ScreenEntrySwapsOnlyAtBrushCover(
            LobbyScreenNavigator.LobbySection destination)
        {
            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(lobby.IsInteractive, Is.True);

            bool requested = destination ==
                             LobbyScreenNavigator.LobbySection.PermanentGrowth
                ? navigator.OpenGrowth()
                : navigator.OpenCodex();

            Assert.That(requested, Is.True);
            Assert.That(navigator.IsTransitioning, Is.True);
            Assert.That(
                lobby.ActiveMenu,
                Is.EqualTo(
                    destination ==
                    LobbyScreenNavigator.LobbySection.PermanentGrowth
                        ? LobbyMenuSelection.Growth
                        : LobbyMenuSelection.Codex));
            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(lobby.IsVisible, Is.True);
            Assert.That(lobby.IsInteractive, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(codex.IsOpen, Is.False);

            navigator.CompleteCoverForTests();

            Assert.That(navigator.CurrentSection, Is.EqualTo(destination));
            Assert.That(lobby.IsVisible, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(codex.IsOpen, Is.False);
            Assert.That(
                destination ==
                LobbyScreenNavigator.LobbySection.PermanentGrowth
                    ? growth.ScreenRoot.anchoredPosition.y
                    : codex.ScreenRoot.anchoredPosition.y,
                Is.Zero.Within(0.001f));

            navigator.CompleteRevealForTests();

            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(growth.IsOpen,
                Is.EqualTo(
                    destination ==
                    LobbyScreenNavigator.LobbySection.PermanentGrowth));
            Assert.That(codex.IsOpen,
                Is.EqualTo(
                    destination ==
                    LobbyScreenNavigator.LobbySection.Codex));
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
            Assert.That(navigator.OpenCodex(), Is.False);
            Assert.That(navigator.ReturnToLobby(), Is.False);
            Assert.That(navigator.PendingSection,
                Is.EqualTo(
                    LobbyScreenNavigator.LobbySection.PermanentGrowth));

            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(navigator.OpenCodex(), Is.True);
            Assert.That(navigator.OpenCodex(), Is.False);
        }

        [Test]
        public void RequestIsRejectedWhenSharedBrushTransitionIsBusy()
        {
            var transition = systemsHost.GetComponent<BrushTransitionView>();
            SetField(transition, "playing", true);

            Assert.That(navigator.OpenGrowth(), Is.False);
            Assert.That(navigator.OpenCodex(), Is.False);
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
            Assert.That(navigator.OpenCodex(), Is.False);
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(codex.IsOpen, Is.False);
            Assert.That(lobby.IsInteractive, Is.False);
        }

        [Test]
        public void LeavingLobbyImmediatelyClosesDedicatedScreen()
        {
            Assert.That(navigator.OpenCodex(), Is.True);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            Assert.That(codex.IsOpen, Is.True);

            Invoke(manager, "SetState", GameState.Playing);

            Assert.That(navigator.CurrentSection,
                Is.EqualTo(LobbyScreenNavigator.LobbySection.Lobby));
            Assert.That(navigator.IsTransitioning, Is.False);
            Assert.That(codex.IsOpen, Is.False);
            Assert.That(growth.IsOpen, Is.False);
            Assert.That(lobby.IsInteractive, Is.False);
        }

        static void AssertDedicatedScreen(
            RectTransform screenRoot,
            Button backButton,
            string contentName)
        {
            Assert.That(screenRoot, Is.Not.Null);
            Assert.That(
                screenRoot.Find("OpaqueHanjiBackground"),
                Is.Not.Null);
            Assert.That(
                screenRoot.Find($"SafeAreaRoot/{contentName}"),
                Is.Not.Null);
            Assert.That(backButton, Is.Not.Null);
            Assert.That(
                backButton.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            Assert.That(backButton.targetGraphic, Is.Not.Null);
            Assert.That(backButton.targetGraphic.raycastTarget, Is.True);
            AssertSharedActionButton(backButton);
            Assert.That(backButton.navigation.mode,
                Is.EqualTo(Navigation.Mode.None));
            Text label = backButton.GetComponentInChildren<Text>(true);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo("로비"));
            Assert.That(label.fontSize,
                Is.GreaterThanOrEqualTo(InkUiStyle.BodySize));
        }

        static void AssertSharedActionButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.TypeOf<Image>());
            Assert.That(
                InkUiStyle.UsesActionButtonSprite(
                    button.targetGraphic as Image),
                Is.True);
        }

        static void AssertSemanticSelectionSurface(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(
                InkUiStyle.UsesActionButtonSprite(
                    button.targetGraphic as Image),
                Is.False,
                "카드·성장 가지 선택 영역은 텍스트 행동 버튼 스킨 대상이 아닙니다.");
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
