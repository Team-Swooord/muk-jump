using System;
using System.Collections;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;

public class HanjiScrollCloseTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    RectTransform panel;
    HanjiScrollFrame frame;
    CanvasGroup backdrop;
    CanvasGroup content;

    [SetUp]
    public void SetUp()
    {
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        root = new GameObject("ScrollCloseTests", typeof(RectTransform), typeof(CanvasGroup));
        backdrop = root.GetComponent<CanvasGroup>();
        panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(root.transform, false);
        frame = HanjiScrollFrame.Attach(panel, new Vector2(764, 1450));
        content = panel.GetComponent<CanvasGroup>();
        content.alpha = 1;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        LobbySettingsProfile.RestoreDefaultStoreForTests();
    }

    static void Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);

    [Test]
    public void OpenScrollIsTimeInvariantAndDoesNotRestartLegacyFlutter()
    {
        frame.SetPose(1, 0, false);
        Vector2 bottom = Roll("BottomRoll").anchoredPosition;
        Vector2 top = Roll("TopRoll").anchoredPosition;
        var paper = panel.Find("HanjiScrollArt/Paper").GetComponent<HanjiScrollPaperGraphic>();
        for (int i = 0; i < 120; i++)
        {
            frame.SetPose(1, i * 30f, true);
            Assert.That(Roll("BottomRoll").anchoredPosition, Is.EqualTo(bottom));
            Assert.That(Roll("TopRoll").anchoredPosition, Is.EqualTo(top));
            Assert.That(Roll("BottomRoll").localEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(paper.PhaseSeconds, Is.Zero);
        }
    }
    void Begin(Action action = null) => Invoke(frame, "BeginClose", action, backdrop);
    void Tick(float seconds) => Invoke(frame, "AdvanceClose", seconds);
    RectTransform Roll(string name) => (RectTransform)panel.Find("HanjiScrollArt/" + name);

    [TestCase(0.25f)]
    [TestCase(0.6f)]
    [TestCase(1f)]
    public void CloseFromCurrentLengthRaisesOnlyBottomRollAndCompletesOnce(float start)
    {
        frame.SetPose(start, 0, false);
        float top = Roll("TopRoll").anchoredPosition.y;
        float bottom = Roll("BottomRoll").anchoredPosition.y;
        int calls = 0;
        Begin(() => calls++);
        Assert.That(Roll("BottomRoll").anchoredPosition.y, Is.EqualTo(bottom));
        Assert.That(content.interactable, Is.False);
        Assert.That(content.blocksRaycasts, Is.True);
        for (int i = 0; i < 6; i++)
        {
            Tick(HanjiScrollFrame.CloseDuration / 6f);
            Assert.That(Roll("TopRoll").anchoredPosition.y, Is.EqualTo(top));
            Assert.That(Roll("BottomRoll").anchoredPosition.y, Is.GreaterThanOrEqualTo(bottom));
            bottom = Roll("BottomRoll").anchoredPosition.y;
            if (i < 5) Assert.That(calls, Is.Zero);
        }
        Tick(0.01f);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(bottom, Is.GreaterThan(680));
        Assert.That(backdrop.alpha, Is.Zero);
        Tick(1f);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void ContentFadesBeforePaperCanRollAcrossItsText()
    {
        Begin();
        Tick(HanjiScrollFrame.CloseDuration * 0.25f);
        Assert.That(content.alpha, Is.Zero.Within(0.001));
        Assert.That(backdrop.alpha, Is.EqualTo(1));
        Assert.That(content.blocksRaycasts, Is.True);
    }

    [Test]
    public void ReopenCancelsStaleCompletionAndKeepsCurrentRollPosition()
    {
        int calls = 0;
        Begin(() => calls++);
        Tick(HanjiScrollFrame.CloseDuration * .75f);
        float bottom = Roll("BottomRoll").anchoredPosition.y;
        frame.CancelClose();
        Tick(1f);
        Assert.That(calls, Is.Zero);
        Assert.That(frame.IsClosing, Is.False);
        Assert.That(backdrop.alpha, Is.EqualTo(1));
        Assert.That(Roll("BottomRoll").anchoredPosition.y, Is.EqualTo(bottom));
    }

    [Test]
    public void DisableCancelsDeferredAction()
    {
        int calls = 0;
        Begin(() => calls++);
        Invoke(frame, "OnDisable");
        Tick(1f);
        Assert.That(calls, Is.Zero);
        Assert.That(frame.IsClosing, Is.False);
    }

    [Test]
    public void BackgroundFreezesCloseAndReducedMotionFinishesOnForeground()
    {
        int calls = 0;
        Begin(() => calls++);
        float bottom = Roll("BottomRoll").anchoredPosition.y;
        MobileApplicationLifecycle.SetPlatformVisibility(false);
        Tick(1f);
        Assert.That(calls, Is.Zero);
        Assert.That(Roll("BottomRoll").anchoredPosition.y, Is.EqualTo(bottom));
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        LobbySettingsProfile.SetReducedMotionEnabled(true);
        Tick(0.001f);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void ResultCloseUsesSameTopFixedReverseGeometryAndNeverEnablesActions()
    {
        var view = root.AddComponent<GameOverPopupView>();
        Invoke(view, "BuildIfNeeded");
        Invoke(view, "ApplyRevealPose", 1f, true);
        Transform result = root.transform.Find("GameOverPopupCanvas/SafeAreaRoot/ScrollResultPopup");
        var top = (RectTransform)result.Find("TopRoll");
        var bottom = (RectTransform)result.Find("BottomRoll");
        var texts = result.Find("ResultContent").GetComponent<CanvasGroup>();
        float oldY = bottom.anchoredPosition.y;
        for (int i = 0; i <= 10; i++)
        {
            Invoke(view, "ApplyClosePose", i / 10f, 1f, 1f, 1f);
            Assert.That(top.anchoredPosition.y, Is.EqualTo(550));
            Assert.That(bottom.anchoredPosition.y, Is.GreaterThanOrEqualTo(oldY));
            Assert.That(texts.interactable, Is.False);
            oldY = bottom.anchoredPosition.y;
        }
        Assert.That(oldY, Is.GreaterThan(510));
        Assert.That(texts.alpha, Is.Zero);
    }
}

public class HanjiScrollCloseRuntimeTests
{
    static int callbackCount;
    static void OnClosed() => callbackCount++;
    static void OnDuplicateClosed() => callbackCount += 100;

    [UnityTest]
    public IEnumerator NicknameCooldownOverlayKeepsLocalizedHintAndActionsVisible() => CaptureAccountOverlay(false);

    [UnityTest]
    public IEnumerator DeleteConfirmationOverlayShowsWarningAndCancelInThreeLanguages() => CaptureAccountOverlay(true);

    [UnityTest]
    public IEnumerator GuestLinkHintAccountPageShowsRedNoticeBelowDelete() => CaptureAccountOverlay(false, true);

    IEnumerator CaptureAccountOverlay(bool deletion, bool guestHint = false)
    {
        string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        if (!Application.isBatchMode)
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MukJumpIdentityProfile.UseStoreForTests(new MukJump.EditorTests.MemoryIdentityStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        var host = new GameObject("NicknameCooldownOverlayProbe");
        var camera = new GameObject("NicknameBackground", typeof(Camera));
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = InkPalette.Paper;
        // 비활성 대역으로 UI만 촬영한다. 실제 인증·프로필 초기화는 호출하지 않는다.
        var accountHost = new GameObject("NicknameAccountProbe");
        accountHost.SetActive(false);
        var account = accountHost.AddComponent<MukJumpAccountRuntime>();
        var previousAccount = MukJumpAccountRuntime.Instance;
        typeof(MukJumpAccountRuntime).GetProperty("Instance").SetValue(null, account);
        typeof(MukJumpAccountRuntime).GetProperty("AccountKind").SetValue(account, guestHint ? MukJumpAccountKind.BackendGuest : MukJumpAccountKind.Apple);
        typeof(MukJumpAccountRuntime).GetProperty("IsOnlineAuthenticated").SetValue(account, true);
        typeof(MukJumpAccountRuntime).GetField("currentAccountScopeForTests", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(account, new Func<string>(() => "nickname-ui-owner"));
        typeof(MukJumpAccountRuntime).GetField("backendUidForTests", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(account, new Func<string>(() => "123456"));
        try
        {
            foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
            {
                GameLocalization.SetLanguage(language);
                var view = host.AddComponent<LobbyOptionsView>();
                view.BuildForTests();
                if (deletion || guestHint)
                {
                    view.OpenTutorialForTests();
                    typeof(LobbyOptionsView).GetMethod("ShowAccountPageImmediate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                }
                if (!guestHint) typeof(LobbyOptionsView).GetMethod(deletion ? "HandleDeleteAccount" : "OpenNicknamePopup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, deletion ? null : new object[] { false });
                var canvas = host.transform.Find(guestHint ? "LobbyOptionsCanvas" : deletion ? "DeleteConfirmationCanvas" : "NicknameCanvas").GetComponent<Canvas>();
                Vector2 display = canvas.renderingDisplaySize;
                view.SetDisplayMetricsForTests(Mathf.RoundToInt(display.x), Mathf.RoundToInt(display.y), new Rect(0, 0, display.x, display.y));
                var panel = canvas.transform.Find(guestHint ? "SafeAreaRoot/OptionsScroll" : deletion ? "SafeAreaRoot/DeleteConfirmationScroll" : "SafeAreaRoot/NicknameScroll");
                if (!deletion && !guestHint) InkLocalizedText.SetSource(panel.Find("Error").GetComponent<UnityEngine.UI.Text>(), MukJumpNicknameChange.WaitMessage);
                panel.GetComponent<HanjiScrollFrame>().ResetPresentation();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(panel.Find(guestHint ? "AccountPage/GuestLinkHint" : deletion ? "Warning" : "ChangeIntervalHint").GetComponent<UnityEngine.UI.Text>().text,
                    Is.EqualTo(GameLocalization.Translate(guestHint ? "기기 변경 전에 계정을 연동해 주세요" : deletion ? "계정과 서버 기록이 영구 삭제됩니다" : MukJumpNicknameChange.Hint)));
                if (!Application.isBatchMode)
                {
                    object capture = MukJump.EditorTools.MukJumpAgentAudit.CaptureUi();
                    string path = (string)capture.GetType().GetProperty("path").GetValue(capture);
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!System.IO.File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(System.IO.File.Exists(path), Is.True, path);
                    Debug.Log($"[AccountOverlay] {(guestHint ? "GuestHint" : deletion ? "Delete" : "Nickname")} {language}: {path}");
                }
                UnityEngine.Object.DestroyImmediate(view);
                for (int i = host.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(camera);
            UnityEngine.Object.DestroyImmediate(accountHost);
            typeof(MukJumpAccountRuntime).GetProperty("Instance").SetValue(null, previousAccount);
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            MukJumpIdentityProfile.UseStoreForTests(null);
        }
        yield return new ExitPlayMode();
        if (!string.IsNullOrEmpty(originalScene)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScene);
    }

    [UnityTest]
    public IEnumerator CenteredOptionsPagesKeepOverlayPaperAtTheSafeAreaCenter()
    {
        string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        if (!Application.isBatchMode)
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MukJumpIdentityProfile.UseStoreForTests(new MukJump.EditorTests.MemoryIdentityStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        var host = new GameObject("CenteredOptionsOverlayProbe");
        var camera = new GameObject("CenteredOptionsBackground", typeof(Camera));
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = InkPalette.Paper;
        try
        {
            var view = host.AddComponent<LobbyOptionsView>();
            view.OpenTutorialForTests();
            // Device Simulator의 Screen 값과 실제 Game 뷰 렌더 크기를 혼용하지 않는다.
            var canvas = host.GetComponentInChildren<Canvas>();
            Vector2 display = canvas.renderingDisplaySize;
            view.SetDisplayMetricsForTests(Mathf.RoundToInt(display.x), Mathf.RoundToInt(display.y),
                new Rect(0, 0, display.x, display.y));
            var panel = (RectTransform)host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
            foreach (string page in new[] { "ShowOptionsPageImmediate", "ShowTutorialPageImmediate", "ShowLanguagePageImmediate" })
            {
                var method = typeof(LobbyOptionsView).GetMethod(page, BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(view, page == "ShowTutorialPageImmediate" ? new object[] { 0 } : null);
                panel.GetComponent<HanjiScrollFrame>().ResetPresentation();
                yield return new WaitForSecondsRealtime(.45f);
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.anchoredPosition, Is.EqualTo(Vector2.zero), page);
                Assert.That(Vector3.Distance(panel.position, panel.parent.position), Is.LessThan(.01f), page);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                // 배치 모드에는 합성 Game 뷰가 없을 수 있으므로 실제 창에서만 촬영한다.
                if (!Application.isBatchMode)
                {
                    object capture = MukJump.EditorTools.MukJumpAgentAudit.CaptureUi();
                    string path = (string)capture.GetType().GetProperty("path").GetValue(capture);
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!System.IO.File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(System.IO.File.Exists(path), Is.True, path);
                    Debug.Log($"[CenteredOptions] {page}: {path} ({Screen.width}x{Screen.height})");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(camera);
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            MukJumpIdentityProfile.UseStoreForTests(null);
        }
        yield return new ExitPlayMode();
        if (!string.IsNullOrEmpty(originalScene))
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScene);
    }

    [UnityTest]
    public IEnumerator AccountTransitionCentersPopupAndRestoresSettingsWithoutStaleNavigation()
    {
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        float oldScale = Time.timeScale;
        Application.runInBackground = true;
        Time.timeScale = 0;
        var host = new GameObject("AccountTransitionProbe");
        bool entryGated = false, openingGated = false, entered = false;
        bool exitGated = false, returned = false, reopened = false, syncBlocked = false, deferredSyncBlocked = false;
        bool toastVisible = false, toastExpired = false, toastCleared = false;
        try
        {
            var view = host.AddComponent<LobbyOptionsView>();
            view.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(.7f);
            Transform canvas = host.transform.Find("LobbyOptionsCanvas");
            var root = canvas.GetComponent<CanvasGroup>();
            Transform panel = canvas.Find("SafeAreaRoot/OptionsScroll");
            var frame = panel.GetComponent<HanjiScrollFrame>();
            var top = panel.Find("HanjiScrollArt/TopRoll");
            var dim = canvas.Find("InkDim").GetComponent<UnityEngine.EventSystems.EventTrigger>();
            var account = panel.Find("OptionsPage/AccountButton").GetComponent<UnityEngine.UI.Button>();
            var close = panel.Find("AccountPage/AccountClose").GetComponent<UnityEngine.UI.Button>();
            var outside = new UnityEngine.EventSystems.PointerEventData(null)
            {
                position = new Vector2(-100, -100),
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            dim.OnPointerClick(outside);
            yield return new WaitForSecondsRealtime(1f);
            Canvas.ForceUpdateCanvases();
            float topY = top.position.y;

            account.onClick.Invoke();
            account.onClick.Invoke();
            close.onClick.Invoke();
            dim.OnPointerClick(outside);
            entryGated = frame.IsClosing && !view.IsAccountOpen && root.alpha == 1
                && root.blocksRaycasts && !root.interactable;
            yield return new WaitForSecondsRealtime(.36f);
            close.onClick.Invoke();
            openingGated = view.IsAccountOpen && !frame.IsClosing && !frame.IsReady
                && !close.IsInteractable();
            yield return new WaitForSecondsRealtime(.7f);
            entered = view.IsAccountOpen && frame.IsReady && close.IsInteractable()
                && ((RectTransform)panel).anchoredPosition == Vector2.zero;

            var showNotice = typeof(LobbyOptionsView).GetMethod("ShowAccountNotice",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var toast = canvas.Find("SafeAreaRoot/AccountToast").GetComponent<CanvasGroup>();
            // TestRunner/Game 뷰의 에디터 포커스와 무관하게 전경 복귀 상태를 명시한다.
            typeof(LobbyOptionsView).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { true });
            showNotice.Invoke(view, new object[] { "문의 코드를 복사했어요" });
            yield return new WaitForSecondsRealtime(.3f);
            toastVisible = toast.alpha > .99f && Time.timeScale == 0;
            yield return new WaitForSecondsRealtime(4.3f);
            toastExpired = toast.alpha == 0;

            // 실제 인증 요청 없이 동기화 잠금만 주입해 복귀 보호를 확인한다.
            var runtime = MukJumpAccountRuntime.Instance;
            var phase = typeof(MukJumpAccountRuntime).GetProperty("Phase");
            object previousPhase = phase.GetValue(runtime);
            try
            {
                phase.SetValue(runtime, MukJumpAccountPhase.Connecting);
                close.onClick.Invoke();
                dim.OnPointerClick(outside);
                syncBlocked = runtime.BlocksGameplayForAccountSync && view.IsAccountOpen && !frame.IsClosing;
            }
            finally { phase.SetValue(runtime, previousPhase); }

            close.onClick.Invoke();
            try
            {
                phase.SetValue(runtime, MukJumpAccountPhase.Connecting);
                yield return new WaitForSecondsRealtime(1f);
                deferredSyncBlocked = view.IsAccountOpen && frame.IsReady;
            }
            finally { phase.SetValue(runtime, previousPhase); }

            showNotice.Invoke(view, new object[] { "문의 코드를 복사했어요" });
            yield return new WaitForSecondsRealtime(.2f);
            close.onClick.Invoke();
            toastCleared = toast.alpha == 0;
            close.onClick.Invoke();
            dim.OnPointerClick(outside);
            exitGated = frame.IsClosing && view.IsAccountOpen && root.alpha == 1
                && root.blocksRaycasts && !root.interactable;
            yield return new WaitForSecondsRealtime(1f);
            returned = view.IsOpen && !view.IsAccountOpen && frame.IsReady
                && Mathf.Abs(top.position.y - topY) < .01f;

            account.onClick.Invoke();
            host.SetActive(false);
            host.SetActive(true);
            view.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(1f);
            reopened = view.IsTutorialOpen && !view.IsAccountOpen && frame.IsReady && root.interactable;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Time.timeScale = oldScale;
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        yield return new ExitPlayMode();
        Assert.That(entryGated, Is.True, "진입 중 중복 터치가 다른 전환을 시작하면 안 된다");
        Assert.That(openingGated && entered, Is.True, "펼침 완료 뒤에만 계정 버튼을 활성화한다");
        Assert.That(syncBlocked, Is.True, "계정 기록 확인 중에는 설정으로 빠져나가지 않는다");
        Assert.That(deferredSyncBlocked, Is.True, "닫히는 도중 시작된 기록 확인도 보호한다");
        Assert.That(exitGated && returned, Is.True, "접기/펼치기를 유지하며 설정의 원래 상단 위치로 돌아온다");
        Assert.That(reopened, Is.True, "취소된 계정 전환이 다시 나타나면 안 된다");
        Assert.That(toastVisible, Is.True, "전경이며 게임 시간이 멈춰 있어도 토스트는 표시된다");
        Assert.That(toastExpired, Is.True, "전경의 토스트는 실제 시간으로 만료된다");
        Assert.That(toastCleared, Is.True, "계정 종이가 접히는 즉시 토스트도 지운다");
    }

    [UnityTest]
    public IEnumerator TutorialTransitionKeepsTopRollAndDimAndCancelsStaleNavigation()
    {
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        float oldScale = Time.timeScale;
        Application.runInBackground = true;
        Time.timeScale = 0;
        var host = new GameObject("TutorialTransitionProbe");
        bool exitGated = false, returned = false, entryGated = false, entered = false;
        bool pagesStayOpen = false, reopened = false;
        try
        {
            var view = host.AddComponent<LobbyOptionsView>();
            view.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(.7f);
            Transform canvas = host.transform.Find("LobbyOptionsCanvas");
            var root = canvas.GetComponent<CanvasGroup>();
            Transform panel = canvas.Find("SafeAreaRoot/OptionsScroll");
            var frame = panel.GetComponent<HanjiScrollFrame>();
            var top = (RectTransform)panel.Find("HanjiScrollArt/TopRoll");
            var dim = canvas.Find("InkDim").GetComponent<UnityEngine.EventSystems.EventTrigger>();
            var next = panel.Find("TutorialPage/NextButton").GetComponent<UnityEngine.UI.Button>();
            var guide = panel.Find("OptionsPage/GuideButton").GetComponent<UnityEngine.UI.Button>();
            Canvas.ForceUpdateCanvases();
            float topY = top.position.y;
            var outside = new UnityEngine.EventSystems.PointerEventData(null)
            {
                position = new Vector2(-100, -100),
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };

            dim.OnPointerClick(outside);
            dim.OnPointerClick(outside);
            next.onClick.Invoke();
            exitGated = frame.IsClosing && view.IsTutorialOpen && root.blocksRaycasts
                && !root.interactable && root.alpha == 1 && view.CurrentTutorialPage == 0;
            yield return new WaitForSecondsRealtime(1f);
            returned = view.IsOpen && !view.IsTutorialOpen && frame.IsReady
                && Mathf.Abs(top.position.y - topY) < .01f;

            guide.onClick.Invoke();
            guide.onClick.Invoke();
            next.onClick.Invoke();
            entryGated = frame.IsClosing && !view.IsTutorialOpen && root.alpha == 1
                && root.blocksRaycasts && !root.interactable;
            yield return new WaitForSecondsRealtime(1f);
            entered = view.IsTutorialOpen && frame.IsReady && view.CurrentTutorialPage == 0
                && Mathf.Abs(top.position.y - topY) < .01f;
            next.onClick.Invoke();
            pagesStayOpen = frame.IsReady && !frame.IsClosing && view.CurrentTutorialPage == 1;

            // 접힘 도중 비활성화/재개해도 이전 복귀 콜백이 새 페이지를 덮지 않는다.
            dim.OnPointerClick(outside);
            host.SetActive(false);
            host.SetActive(true);
            view.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(1f);
            reopened = view.IsOpen && view.IsTutorialOpen && frame.IsReady && root.interactable;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Time.timeScale = oldScale;
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        yield return new ExitPlayMode();
        Assert.That(exitGated && returned, Is.True, "dim은 튜토리얼만 접어 설정으로 돌아가야 한다");
        Assert.That(entryGated && entered, Is.True, "상단 축을 유지하며 한 번만 전환해야 한다");
        Assert.That(pagesStayOpen, Is.True, "페이지 이동마다 두루마리를 다시 펼치면 안 된다");
        Assert.That(reopened, Is.True, "취소한 전환이 다시 나타나면 안 된다");
    }

    [UnityTest]
    public IEnumerator LanguageDimReturnsOneLevelAndGuardsRepeatedInputDuringTransition()
    {
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        float oldScale = Time.timeScale;
        Application.runInBackground = true;
        Time.timeScale = 0;
        var host = new GameObject("LanguageDimProbe");
        bool entryGated = false, entered = false, exitGated = false, returned = false, selected = false;
        try
        {
            var view = host.AddComponent<LobbyOptionsView>();
            view.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(.7f);
            Transform canvas = host.transform.Find("LobbyOptionsCanvas");
            var root = canvas.GetComponent<CanvasGroup>();
            Transform panel = canvas.Find("SafeAreaRoot/OptionsScroll");
            var frame = panel.GetComponent<HanjiScrollFrame>();
            var dim = canvas.Find("InkDim").GetComponent<UnityEngine.EventSystems.EventTrigger>();
            var options = panel.Find("OptionsPage").GetComponent<CanvasGroup>();
            var chooser = panel.Find("LanguagePage").GetComponent<CanvasGroup>();
            var open = options.transform.Find("LanguageButton").GetComponent<UnityEngine.UI.Button>();
            var english = chooser.transform.Find("EnglishButton").GetComponent<UnityEngine.UI.Button>();
            var korean = chooser.transform.Find("KoreanButton").GetComponent<UnityEngine.UI.Button>();
            var outside = new UnityEngine.EventSystems.PointerEventData(null)
            {
                position = new Vector2(-100, -100),
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            dim.OnPointerClick(outside);
            yield return new WaitForSecondsRealtime(1f);
            float settingsTop = panel.Find("HanjiScrollArt/TopRoll").position.y;
            open.onClick.Invoke();
            open.onClick.Invoke();
            english.onClick.Invoke();
            dim.OnPointerClick(outside);
            entryGated = frame.IsClosing && !root.interactable && root.alpha == 1 && !GameLocalization.IsEnglish;
            yield return new WaitForSecondsRealtime(1f);
            entered = view.IsOpen && chooser.blocksRaycasts && frame.IsReady;
            dim.OnPointerClick(outside);
            dim.OnPointerClick(outside);
            english.onClick.Invoke();
            exitGated = frame.IsClosing && !root.interactable && root.alpha == 1 && !GameLocalization.IsEnglish;
            yield return new WaitForSecondsRealtime(1f);
            returned = view.IsOpen && options.blocksRaycasts && !chooser.blocksRaycasts && frame.IsReady
                && Mathf.Abs(panel.Find("HanjiScrollArt/TopRoll").position.y - settingsTop) < .01f;
            open.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1f);
            english.onClick.Invoke();
            korean.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1f);
            selected = GameLocalization.IsEnglish && view.IsOpen && options.blocksRaycasts && !chooser.blocksRaycasts;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Time.timeScale = oldScale;
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        yield return new ExitPlayMode();
        Assert.That(entryGated && entered, Is.True, "언어창 진입 중 중복 입력을 막습니다.");
        Assert.That(exitGated && returned, Is.True, "dim 연속 터치도 설정으로 한 단계만 돌아갑니다.");
        Assert.That(selected, Is.True, "선택한 언어는 즉시 적용하고 전환 중 두 번째 선택은 무시합니다.");
    }

    [UnityTest]
    public IEnumerator RankingAnimatesFromLobbyWithoutReopeningOnRefresh()
    {
        yield return new EnterPlayMode();
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        bool oldBackground = Application.runInBackground;
        float oldScale = Time.timeScale;
        Application.runInBackground = true;
        Time.timeScale = 0;
        var managerHost = new GameObject("InactiveRankingManager");
        managerHost.SetActive(false);
        var instance = typeof(GameManager).GetProperty("Instance");
        var previous = GameManager.Instance;
        instance.SetValue(null, managerHost.AddComponent<GameManager>());
        var host = new GameObject("RankingMotionProbe");
        bool lobbyEntry = false, lobbyReturn = false;
        bool refreshedInPlace = false, canceled = false;
        try
        {
            var view = host.AddComponent<LobbyOptionsView>();
            view.OpenLeaderboard();
            view.OpenLeaderboard();
            Transform canvas = host.transform.Find("LobbyOptionsCanvas");
            var root = canvas.GetComponent<CanvasGroup>();
            Transform panel = canvas.Find("SafeAreaRoot/OptionsScroll");
            var frame = panel.GetComponent<HanjiScrollFrame>();
            var bottom = (RectTransform)panel.Find("HanjiScrollArt/BottomRoll");
            var options = panel.Find("OptionsPage").GetComponent<CanvasGroup>();
            var ranking = panel.Find("LeaderboardPage").GetComponent<CanvasGroup>();
            var dim = canvas.Find("InkDim").GetComponent<UnityEngine.EventSystems.EventTrigger>();
            var outside = new UnityEngine.EventSystems.PointerEventData(null)
            {
                position = new Vector2(-100, -100),
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            bool directRanking = ranking.blocksRaycasts && !options.blocksRaycasts && !frame.IsClosing;
            float earlyBottom = bottom.anchoredPosition.y;
            bool singleOpening = !frame.IsReady && !frame.IsClosing;
            yield return new WaitForSecondsRealtime(.65f);
            lobbyEntry = directRanking && singleOpening && frame.IsReady && bottom.anchoredPosition.y < earlyBottom;
            float openBottom = bottom.anchoredPosition.y;
            typeof(LobbyOptionsView).GetMethod("RefreshLeaderboardPage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            yield return null;
            refreshedInPlace = frame.IsReady && !frame.IsClosing && Mathf.Abs(bottom.anchoredPosition.y - openBottom) < .01f;
            dim.OnPointerClick(outside);
            dim.OnPointerClick(outside);
            bool closingLobbyRanking = frame.IsClosing && root.blocksRaycasts;
            yield return new WaitForSecondsRealtime(.4f);
            lobbyReturn = closingLobbyRanking && !view.IsOpen;

            view.OpenLeaderboard();
            yield return new WaitForSecondsRealtime(.1f);
            host.SetActive(false);
            host.SetActive(true);
            view.Open();
            yield return new WaitForSecondsRealtime(1f);
            canceled = view.IsOpen && options.blocksRaycasts && !ranking.blocksRaycasts && frame.IsReady;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            instance.SetValue(null, previous);
            UnityEngine.Object.DestroyImmediate(managerHost);
            Time.timeScale = oldScale;
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }
        yield return new ExitPlayMode();
        Assert.That(lobbyEntry && lobbyReturn, Is.True, "메인 순위는 설정 없이 한 번만 펼치고 접어서 닫습니다.");
        Assert.That(refreshedInPlace, Is.True, "조회 결과 갱신은 열린 두루마리를 재시작하지 않습니다.");
        Assert.That(canceled, Is.True, "비활성화한 뒤 이전 순위 진입 콜백을 되살리지 않습니다.");
    }

    [UnityTest]
    public IEnumerator SettingsAndResultStayBlockingUntilRealUnscaledCloseCompletes()
    {
        yield return new EnterPlayMode();
        var store = new MemoryLobbySettingsStore();
        LobbySettingsProfile.UseStoreForTests(store);
        MobileApplicationLifecycle.SetPlatformVisibility(true);
        float oldScale = Time.timeScale;
        bool oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = 0;
        var host = new GameObject("LiveScrollCloseProbe");
        bool settingsBlocked, settingsDone, resultBlocked, resultDone;
        // EnterPlayMode 도메인 리로드는 컴파일러의 캡처 closure를 복구하지 않는다.
        // 콜백은 캡처 없는 메서드를 쓰고 ExitPlayMode 전 결과만 값으로 보존한다.
        callbackCount = 0;
        int completions = 0;
        try
        {
            var options = host.AddComponent<LobbyOptionsView>();
            // 테스트 전용 다시보기는 실제 설정의 공통 패널과 닫기 경로를 사용한다.
            options.OpenTutorialForTests();
            yield return new WaitForSecondsRealtime(0.7f);
            options.Close();
            settingsBlocked = options.IsOpen;
            options.Close();
            yield return new WaitForSecondsRealtime(0.4f);
            settingsDone = !options.IsOpen;
            var result = host.AddComponent<GameOverPopupView>();
            result.Show(132, 132, true);
            yield return new WaitForSecondsRealtime(0.7f);
            result.Close(OnClosed);
            result.Close(OnDuplicateClosed);
            resultBlocked = result.IsClosing && host.transform.Find("GameOverPopupCanvas")
                .GetComponent<CanvasGroup>().blocksRaycasts;
            yield return new WaitForSecondsRealtime(0.4f);
            resultDone = !result.IsClosing && host.transform.Find("GameOverPopupCanvas")
                .GetComponent<CanvasGroup>().alpha == 0;
            completions = callbackCount;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Time.timeScale = oldScale;
            Application.runInBackground = oldBackground;
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }
        yield return new ExitPlayMode();
        Assert.That(settingsBlocked && settingsDone, Is.True);
        Assert.That(resultBlocked && resultDone, Is.True);
        Assert.That(completions, Is.EqualTo(1));
    }
}
