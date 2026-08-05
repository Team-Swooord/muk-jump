using System;
using System.Collections.Generic;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class FirstRunTutorialTests
    {
        GameObject host;
        MemoryLobbySettingsStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void CatalogCoversEachRequiredGameplayTopicExactlyOnce()
        {
            var topics = new HashSet<GameplayTutorialTopic>();
            for (int i = 0; i < GameplayTutorialCatalog.Count; i++)
            {
                GameplayTutorialPage page = GameplayTutorialCatalog.Get(i);
                Assert.That(topics.Add(page.Topic), Is.True,
                    $"튜토리얼 주제가 중복되었습니다: {page.Topic}");
                Assert.That(page.Title, Is.Not.Empty);
                Assert.That(page.Description, Is.Not.Empty);
                Assert.That(page.SpriteResourcePath, Is.Not.Empty);
            }

            foreach (GameplayTutorialTopic topic in
                     Enum.GetValues(typeof(GameplayTutorialTopic)))
                Assert.That(topics, Does.Contain(topic));

            GameplayTutorialPage drawingPage = GameplayTutorialCatalog.Get(0);
            Assert.That(drawingPage.Description, Does.Contain("곧 마르고"));
            Assert.That(drawingPage.Description, Does.Contain("오래된 선부터"));
            Assert.That(LobbySettingsProfile.CurrentGameplayTutorialVersion,
                Is.EqualTo(3));
        }

        [Test]
        public void LegacyGuideCompletionDoesNotSkipNewInteractiveVersion()
        {
            LobbySettingsProfile.MarkTutorialSeen();

            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.True);

            Assert.That(
                LobbySettingsProfile.TryMarkGameplayTutorialCompleted(),
                Is.True);
            Assert.That(
                LobbySettingsProfile.GameplayTutorialVersion,
                Is.EqualTo(
                    LobbySettingsProfile.CurrentGameplayTutorialVersion));
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);

            LobbySettingsProfile.UseStoreForTests(store);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False,
                "완료 버전은 다음 실행에도 유지되어야 합니다.");
        }

        [Test]
        public void InteractiveTutorialUsesSafeReadableOverlayAndCompletesOnce()
        {
            host = new GameObject("FirstRunTutorialTestHost");
            var tutorial = host.AddComponent<FirstRunTutorialController>();
            tutorial.BuildForTests();
            tutorial.BeginForTests();

            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.CurrentTopic,
                Is.EqualTo(GameplayTutorialTopic.DrawInk));
            Transform root = host.transform.Find("FirstRunTutorialCanvas");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);

            Transform panel = root.Find("SafeAreaRoot/TutorialPanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.True,
                "카드 빈 영역도 아래 HUD로 탭을 통과시키면 안 됩니다.");
            var title = panel.Find("Title")?.GetComponent<Text>();
            var description = panel.Find("Description")?.GetComponent<Text>();
            var skip = panel.Find("SkipButton") as RectTransform;
            Assert.That(title, Is.Not.Null);
            Assert.That(description, Is.Not.Null);
            Assert.That(title.resizeTextForBestFit, Is.False);
            Assert.That(description.resizeTextForBestFit, Is.False);
            Assert.That(description.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(skip, Is.Not.Null);
            Assert.That(skip.sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            var panelChildren = new[]
            {
                panel.Find("TopicIcon") as RectTransform,
                panel.Find("Title") as RectTransform,
                panel.Find("Description") as RectTransform,
                panel.Find("Progress") as RectTransform,
                skip,
            };
            for (int i = 0; i < panelChildren.Length; i++)
                Assert.That(
                    IsInsidePanel(panelChildren[i]),
                    Is.True,
                    $"튜토리얼 카드 요소 {i}가 패널 경계를 벗어났습니다.");

            for (int i = 1; i < GameplayTutorialCatalog.Count; i++)
                tutorial.AdvanceForTests();
            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.CurrentTopic,
                Is.EqualTo(GameplayTutorialTopic.MapZones));

            tutorial.AdvanceForTests();
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);
        }

        [TestCase(1080, 2400, 0, 96, 1080, 2208)]
        [TestCase(1179, 2556, 0, 102, 1179, 2352)]
        public void TutorialPanelFitsAndroidAndIphoneSafeWidth(
            int screenWidth,
            int screenHeight,
            float safeX,
            float safeY,
            float safeWidth,
            float safeHeight)
        {
            var safeArea = new Rect(
                safeX,
                safeY,
                safeWidth,
                safeHeight);
            float scale =
                FirstRunTutorialController.CalculatePanelScaleForTests(
                    safeArea,
                    screenWidth,
                    screenHeight);
            float logicalSafeWidth = MobileUiLayout.GetLogicalSafeSize(
                safeArea,
                screenWidth,
                screenHeight).x;

            Assert.That(scale, Is.GreaterThan(0f));
            Assert.That(
                FirstRunTutorialController.PanelDesignWidth * scale,
                Is.LessThanOrEqualTo(
                    logicalSafeWidth -
                    FirstRunTutorialController.PanelEdgePadding * 2f +
                    0.01f));
        }

        static bool IsInsidePanel(RectTransform child)
        {
            if (child == null)
                return false;
            RectTransform parent = child.parent as RectTransform;
            if (parent == null)
                return false;
            Vector3[] childCorners = new Vector3[4];
            child.GetWorldCorners(childCorners);
            for (int i = 0; i < childCorners.Length; i++)
            {
                Vector2 local = parent.InverseTransformPoint(childCorners[i]);
                if (!parent.rect.Contains(local))
                    return false;
            }
            return true;
        }
    }
}
