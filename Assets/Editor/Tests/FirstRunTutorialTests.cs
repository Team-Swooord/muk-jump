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
            PointerInput.ResetSuppressionForTests();
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
                Assert.That(
                    page.Description.Split('\n').Length,
                    Is.InRange(3, 4),
                    $"{page.Topic} 설명은 3~4개의 짧은 문장이어야 합니다.");
                Assert.That(page.SpriteResourcePath, Is.Not.Empty);
            }

            foreach (GameplayTutorialTopic topic in
                     Enum.GetValues(typeof(GameplayTutorialTopic)))
                Assert.That(topics, Does.Contain(topic));

            GameplayTutorialPage drawingPage = GameplayTutorialCatalog.Get(0);
            Assert.That(drawingPage.Description, Does.Contain("시간이 지나면"));
            Assert.That(drawingPage.Description, Does.Contain("오래된 것부터"));
            GameplayTutorialPage obstaclePage = GameplayTutorialCatalog.Get(2);
            Assert.That(obstaclePage.Description, Does.Contain("체력 한 칸"));
            Assert.That(obstaclePage.Description, Does.Contain("위에서 내려와요"));
            Assert.That(LobbySettingsProfile.CurrentGameplayTutorialVersion,
                Is.EqualTo(5));
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
        public void PausedTutorialUsesReadableBlockingPopupAndCompletesOnce()
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
            Assert.That(root.GetComponent<Canvas>().sortingOrder,
                Is.GreaterThan(1000));
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);

            Image dim = root.Find("TutorialDim")?.GetComponent<Image>();
            Assert.That(dim, Is.Not.Null);
            Assert.That(dim.raycastTarget, Is.True);
            Assert.That(dim.color.a,
                Is.EqualTo(InkUiStyle.PopupDimAlpha).Within(0.001f));
            Assert.That(dim.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(dim.rectTransform.anchorMax, Is.EqualTo(Vector2.one));

            Transform panel = root.Find("SafeAreaRoot/TutorialPanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Find("Paper")?.GetComponent<Image>()?.raycastTarget,
                Is.True,
                "카드 빈 영역도 아래 HUD로 탭을 통과시키면 안 됩니다.");
            var title = panel.Find("Title")?.GetComponent<Text>();
            var description = panel.Find("Description")?.GetComponent<Text>();
            var skip = panel.Find("SkipButton") as RectTransform;
            var previous = panel.Find("PreviousButton")
                ?.GetComponent<Button>();
            var next = panel.Find("NextButton")?.GetComponent<Button>();
            Assert.That(title, Is.Not.Null);
            Assert.That(description, Is.Not.Null);
            Assert.That(title.resizeTextForBestFit, Is.False);
            Assert.That(description.resizeTextForBestFit, Is.False);
            Assert.That(description.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(description.alignment,
                Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(description.rectTransform.sizeDelta,
                Is.EqualTo(new Vector2(700f, 230f)));
            Canvas.ForceUpdateCanvases();
            Assert.That(description.preferredHeight,
                Is.LessThanOrEqualTo(
                    description.rectTransform.rect.height + 0.01f),
                "설명은 자동 축소 없이 정해진 영역 안에 모두 보여야 합니다.");
            Assert.That(skip, Is.Not.Null);
            Assert.That(previous, Is.Not.Null);
            Assert.That(next, Is.Not.Null);
            Assert.That(previous.interactable, Is.False);
            Assert.That(skip.sizeDelta.y,
                Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
            var panelChildren = new[]
            {
                panel.Find("Progress") as RectTransform,
                panel.Find("TopicIconPaper") as RectTransform,
                panel.Find("TopicIcon") as RectTransform,
                panel.Find("Title") as RectTransform,
                panel.Find("Description") as RectTransform,
                panel.Find("PauseHint") as RectTransform,
                panel.Find("PreviousButton") as RectTransform,
                panel.Find("NextButton") as RectTransform,
                skip,
            };
            for (int i = 0; i < panelChildren.Length; i++)
                Assert.That(
                    IsInsidePanel(panelChildren[i]),
                    Is.True,
                    $"튜토리얼 카드 요소 {i}가 패널 경계를 벗어났습니다.");
            for (int i = 0; i < panelChildren.Length; i++)
                Assert.That(
                    IsInsideVisualPanel(panelChildren[i]),
                    Is.True,
                    $"튜토리얼 카드 요소 {i}가 장식 테두리와 겹쳤습니다.");

            Text progress = panel.Find("Progress")?.GetComponent<Text>();
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(progress.rectTransform.anchoredPosition.x,
                Is.Zero.Within(0.001f));

            skip.GetComponent<Button>().onClick.Invoke();
            Text skipLabel = skip.Find("Label")?.GetComponent<Text>();
            Canvas.ForceUpdateCanvases();
            Assert.That(skipLabel?.text, Is.EqualTo("다시 눌러 확인"));
            Assert.That(skipLabel?.preferredWidth,
                Is.LessThanOrEqualTo(skipLabel.rectTransform.rect.width + 0.01f));

            Assert.That(
                FirstRunTutorialController.IsPointerOverControls(
                    new Vector2(1f, 1f)),
                Is.True,
                "모달 바깥 터치도 월드 먹선으로 통과하면 안 됩니다.");

            for (int i = 1; i < GameplayTutorialCatalog.Count; i++)
                tutorial.AdvanceForTests();
            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.CurrentTopic,
                Is.EqualTo(GameplayTutorialTopic.MapZones));
            Assert.That(next.transform.Find("Label")?.GetComponent<Text>()?.text,
                Is.EqualTo("시작하기"));

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
            float logicalSafeHeight = MobileUiLayout.GetLogicalSafeSize(
                safeArea,
                screenWidth,
                screenHeight).y;

            Assert.That(scale, Is.GreaterThan(0f));
            Assert.That(
                FirstRunTutorialController.PanelDesignWidth * scale,
                Is.LessThanOrEqualTo(
                    logicalSafeWidth -
                    FirstRunTutorialController.PanelEdgePadding * 2f +
                    0.01f));
            Assert.That(
                FirstRunTutorialController.PanelDesignHeight * scale,
                Is.LessThanOrEqualTo(
                    logicalSafeHeight -
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

        static bool IsInsideVisualPanel(RectTransform child)
        {
            if (child == null || child.parent is not RectTransform parent)
                return false;
            Rect inner = parent.rect;
            inner.xMin += 48f;
            inner.xMax -= 48f;
            inner.yMin += 100f;
            inner.yMax -= 110f;
            Vector3[] childCorners = new Vector3[4];
            child.GetWorldCorners(childCorners);
            for (int i = 0; i < childCorners.Length; i++)
            {
                Vector2 local = parent.InverseTransformPoint(childCorners[i]);
                if (!inner.Contains(local))
                    return false;
            }
            return true;
        }
    }
}
