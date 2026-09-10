using System;
using System.Collections.Generic;
using System.Reflection;
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
        sealed class IdentityStore : MukJumpIdentityProfile.IStore
        {
            readonly Dictionary<string, string> values = new();
            public string Read(string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
            public void Write(string key, string value) => values[key] = value;
            public void Save() { }
        }

        [SetUp]
        public void SetUp()
        {
            store = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(store);
            MukJumpIdentityProfile.UseStoreForTests(new IdentityStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                UnityEngine.Object.DestroyImmediate(host);
            PointerInput.ResetSuppressionForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            MukJumpIdentityProfile.UseStoreForTests(null);
        }

        [Test]
        public void CatalogOnlyKeepsThreeShortEssentialPages()
        {
            Assert.That(GameplayTutorialCatalog.Count, Is.EqualTo(3));
            var topics = new HashSet<GameplayTutorialTopic>();
            int totalCharacters = 0;
            for (int i = 0; i < GameplayTutorialCatalog.Count; i++)
            {
                GameplayTutorialPage page = GameplayTutorialCatalog.Get(i);
                Assert.That(topics.Add(page.Topic), Is.True,
                    $"튜토리얼 주제가 중복되었습니다: {page.Topic}");
                Assert.That(page.Title, Is.Not.Empty);
                Assert.That(page.Description, Is.Not.Empty);
                Assert.That(
                    page.Description.Split('\n').Length,
                    Is.InRange(2, 3),
                    $"{page.Topic} 설명은 2~3문장만 남겨야 합니다.");
                Assert.That(page.Description.Length, Is.LessThanOrEqualTo(80));
                totalCharacters += page.Description.Length;
                Assert.That(page.SpriteResourcePath, Is.Not.Empty);
            }

            Assert.That(totalCharacters, Is.LessThanOrEqualTo(200));
            Assert.That(topics, Is.EquivalentTo(new[] {
                GameplayTutorialTopic.DrawInk, GameplayTutorialTopic.InkBudget, GameplayTutorialTopic.Obstacles }));

            GameplayTutorialPage drawingPage = GameplayTutorialCatalog.Get(0);
            Assert.That(drawingPage.Description, Does.Contain("자동으로"));
            Assert.That(drawingPage.Description, Does.Contain("기울기"));
            Assert.That(drawingPage.Description, Does.Contain("길이"));
            GameplayTutorialPage inkPage = GameplayTutorialCatalog.Get(1);
            Assert.That(inkPage.Description, Does.Contain("시간이 지나면"));
            Assert.That(inkPage.Description, Does.Contain("오래된 선부터"));
            GameplayTutorialPage obstaclePage = GameplayTutorialCatalog.Get(2);
            Assert.That(obstaclePage.Description, Does.Contain("체력이 1칸"));
            Assert.That(obstaclePage.Description, Does.Contain("분신까지 모두"));
            Assert.That(LobbySettingsProfile.CurrentGameplayTutorialVersion,
                Is.EqualTo(5));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryEssentialPageHasConciseEnglishCopy(int index)
        {
            var translate = typeof(GameLocalization).GetMethod("English", BindingFlags.NonPublic | BindingFlags.Static);
            var page = GameplayTutorialCatalog.Get(index);
            foreach (string source in new[] { page.Title, page.Description })
            {
                var english = (string)translate.Invoke(null, new object[] { source });
                Assert.That(english, Is.Not.EqualTo(source));
                Assert.That(english, Does.Not.Match("[가-힣]"));
                Assert.That(english.Length, Is.LessThanOrEqualTo(145));
                Assert.That(english.Split('\n').Length, Is.LessThanOrEqualTo(3));
            }
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
        public void FreshInstallAutoStartsButCompletedInstallDoesNot()
        {
            Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.True);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.False);
            LobbySettingsProfile.UseStoreForTests(store);
            Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.False);
        }

        [Test]
        public void CloudAccountChangesCannotSkipOrRepeatInstallationTutorial()
        {
            LobbySettingsProfile.ApplyCloudSettings(1, 1, 5);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.True);
            var beforeCompletion = LobbySettingsProfile.CaptureCloudSettings();
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            LobbySettingsProfile.ApplyCloudSettings(1, 1, 0);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);
            LobbySettingsProfile.TryRestoreCloudSettings(beforeCompletion);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);
            LobbySettingsProfile.TryResetForAccountDeletion();
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.True);
        }

        [Test]
        public void AccountDeletionStopsOldControllerAndLeavesTutorialForNewScene()
        {
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            host = new GameObject("DeletedAccountTutorial");
            var tutorial = host.AddComponent<FirstRunTutorialController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LobbySettingsProfile).GetMethod("MarkGameplayStartedThisSession",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            typeof(FirstRunTutorialController).GetField("autoStartAttempted", flags)
                .SetValue(tutorial, true);
            Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.False);

            Assert.That(LobbySettingsProfile.TryResetForAccountDeletion(), Is.True);
            typeof(FirstRunTutorialController).GetMethod("PrepareForStartupReturn", flags)
                .Invoke(tutorial, null);
            Assert.That(LobbySettingsProfile.ShouldAutoStartGameplayTutorial, Is.True);
            Assert.That(typeof(FirstRunTutorialController).GetField("autoStartAttempted", flags)
                .GetValue(tutorial), Is.True);
            Assert.That(typeof(FirstRunTutorialController).GetField("autoStartFirstVisit", flags)
                .GetValue(tutorial), Is.False,
                "삭제 전 Main의 Update는 첫 판을 시작하지 않는다.");
            Assert.That(LobbySettingsProfile.TutorialSeen, Is.False);
            LobbySettingsProfile.UseStoreForTests(store);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.True,
                "삭제 직후 앱을 종료해도 새 게스트의 첫 안내가 남아야 합니다.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstTutorialPausesBeforeStartupOrBrushCoverReveals(bool transitionStillPlaying)
        {
            float oldTimeScale = Time.timeScale;
            bool oldAudioPause = AudioListener.pause;
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            host = new GameObject("CoveredTutorialEntry");
            try
            {
                var game = host.AddComponent<GameManager>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(GameManager).GetMethod("OnEnable", flags).Invoke(game, null);
                var tutorial = host.GetComponent<FirstRunTutorialController>() ??
                    host.AddComponent<FirstRunTutorialController>();
                typeof(FirstRunTutorialController).GetMethod("BindRuntimeSignals", flags)
                    .Invoke(tutorial, null);
                Assert.That(tutorial.PrepareForGameStart(), Is.True);
                typeof(GameManager).GetField("transitionInProgress", flags)
                    .SetValue(game, transitionStillPlaying);
                typeof(GameManager).GetMethod("SetState", flags)
                    .Invoke(game, new object[] { GameState.Playing });
                typeof(GameManager).GetField("transitionInProgress", flags).SetValue(game, false);
                Assert.That(tutorial.IsActive, Is.True);
                Assert.That(game.PauseReason, Is.EqualTo(GameplayPauseReason.FirstRunTutorial));
                Assert.That(game.IsGameplayTicking, Is.False);
                Assert.That(Time.timeScale, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                Time.timeScale = oldTimeScale;
                AudioListener.pause = oldAudioPause;
            }
        }

        [Test]
        public void CompletedProfileInvalidatesAlreadyCachedAutoStart()
        {
            host = new GameObject("CachedTutorialAutoStart");
            var tutorial = host.AddComponent<FirstRunTutorialController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FirstRunTutorialController).GetField("autoStartFirstVisit", flags).SetValue(tutorial, true);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            typeof(FirstRunTutorialController).GetMethod("TryAutoStartFirstVisit", flags)
                .Invoke(tutorial, new object[] { false });
            Assert.That(typeof(FirstRunTutorialController).GetField("autoStartFirstVisit", flags)
                .GetValue(tutorial), Is.False);
        }

        [Test]
        public void SpotlightBlocksTheWholeScreenAndCompletesWithoutNicknameForGuest()
        {
            host = new GameObject("FirstRunTutorialTestHost");
            var tutorial = host.AddComponent<FirstRunTutorialController>();
            tutorial.BuildForTests();
            tutorial.BeginForTests();
            var root = host.transform.Find("FirstRunTutorialCanvas");
            var dim = root.Find("TutorialDim").GetComponent<TutorialSpotlightGraphic>();
            var panel = root.Find("SafeAreaRoot/TutorialPanel");
            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(tutorial.CurrentTopic, Is.EqualTo(GameplayTutorialTopic.AutoJump));
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(dim.raycastTarget, Is.True);
            Assert.That(dim.color, Is.EqualTo(new Color(0, 0, 0, .78f)));
            Assert.That(dim.FocusRect.width, Is.GreaterThan(0));
            Assert.That(dim.FocusRect.height, Is.GreaterThan(0));
            Assert.That(panel.GetComponent<HanjiScrollFrame>(), Is.Null,
                "첫 안내는 설명할 게임 화면을 가리는 두루마리를 만들지 않는다.");
            Assert.That(panel.GetComponentsInChildren<Button>(), Is.Empty);
            Assert.That(panel.Find("Title").GetComponent<Text>().text, Is.EqualTo("자동 점프"));
            Assert.That(FirstRunTutorialController.IsPointerOverControls(Vector2.zero), Is.True);
            var topics = new HashSet<GameplayTutorialTopic>();
            for (int i = 0; i < FirstRunTutorialController.StepCount; i++)
            {
                topics.Add(tutorial.CurrentTopic);
                Assert.That(panel.Find("Progress").GetComponent<Text>().text, Is.EqualTo($"{i + 1} / 4"));
                tutorial.AdvanceForTests();
            }
            Assert.That(topics.Count, Is.EqualTo(4));
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(tutorial.IsAwaitingNickname, Is.False);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(host.transform.Find("NicknameCanvas"), Is.Null);
            Assert.That(MukJumpIdentityProfile.IsGeneratedNickname(MukJumpIdentityProfile.GuestNickname), Is.True);
            Assert.That(tutorial.PrepareForGameStart(), Is.False);
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        public void LastStepHasNoTapHintAndGuestSkipsNicknameSetup(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("TutorialFinalHintTestHost");
            var tutorial = host.AddComponent<FirstRunTutorialController>();
            tutorial.BuildForTests();
            tutorial.BeginForTests();
            var hint = host.transform.Find(
                "FirstRunTutorialCanvas/SafeAreaRoot/TutorialPanel/TapHint").GetComponent<Text>();
            for (int i = 0; i < FirstRunTutorialController.StepCount - 1; i++)
            {
                Assert.That(hint.text, Is.EqualTo(language == GameLanguage.English
                    ? "Tap to continue" : "탭하여 다음"));
                tutorial.AdvanceForTests();
            }
            Assert.That(hint.text, Is.Empty);
            GameLocalization.SetLanguage(language == GameLanguage.English
                ? GameLanguage.Korean : GameLanguage.English);
            Assert.That(hint.text, Is.Empty, "언어를 바꿔도 마지막 안내 문구가 되살아나면 안 됩니다.");
            tutorial.AdvanceForTests();
            Assert.That(tutorial.IsAwaitingNickname, Is.False);
            Assert.That(LobbySettingsProfile.NeedsGameplayTutorial, Is.False);
            GameLocalization.SetLanguage(GameLanguage.Korean);
        }

        [Test]
        public void SpotlightMeshLeavesItsCenterActuallyTransparent()
        {
            host = new GameObject("Spotlight", typeof(RectTransform));
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1080, 1920);
            var graphic = host.AddComponent<TutorialSpotlightGraphic>();
            Assert.That(typeof(Graphic).GetProperty("useLegacyMeshGeneration", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(graphic), Is.False, "VertexHelper 메시가 실제 렌더 경로에서도 사용되어야 한다.");
            graphic.SetFocus(new Rect(-120, -80, 240, 160), 24);
            using var vertices = new VertexHelper();
            typeof(TutorialSpotlightGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(graphic, new object[] { vertices });
            var mesh = new Mesh();
            try
            {
                vertices.FillMesh(mesh);
                var points = mesh.vertices;
                var triangles = mesh.triangles;
                Assert.That(triangles.Length, Is.GreaterThan(24));
                for (int i = 0; i < triangles.Length; i += 3)
                    Assert.That(ContainsPoint(Vector2.zero, points[triangles[i]], points[triangles[i + 1]], points[triangles[i + 2]]),
                        Is.False, "구멍 중앙에는 어두운 삼각형이 없어야 한다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        static bool ContainsPoint(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            float x = Cross(b - a, p - a), y = Cross(c - b, p - b), z = Cross(a - c, p - c);
            return (x > .001f && y > .001f && z > .001f) || (x < -.001f && y < -.001f && z < -.001f);
        }

        [TestCase(1080, 1920, 0, 0)]
        [TestCase(1179, 2556, 177, 102)]
        [TestCase(1080, 2400, 100, 90)]
        public void CalloutStaysInsideSafeAreaAndOutsideTheHole(int width, int height, int top, int bottom)
        {
            var safe = new Rect(0, bottom, width, height - top - bottom);
            foreach (float fraction in new[] { .08f, .25f, .6f, .88f })
            {
                var focus = new Rect(safe.center.x - width * .1f,
                    safe.yMin + safe.height * fraction, width * .2f, width * .08f);
                var callout = FirstRunTutorialController.CalculateCalloutRect(focus, safe, height);
                Assert.That(callout.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                Assert.That(callout.xMax, Is.LessThanOrEqualTo(safe.xMax));
                Assert.That(callout.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
                Assert.That(callout.yMax, Is.LessThanOrEqualTo(safe.yMax));
                Assert.That(callout.Overlaps(focus), Is.False);
            }
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
