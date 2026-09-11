using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsScrollTests
{
    GameObject host;
    LobbyOptionsView view;
    Transform panel;
    Transform page;

    [SetUp]
    public void SetUp()
    {
        MukJumpIdentityProfile.UseStoreForTests(new MukJump.EditorTests.MemoryIdentityStore());
        LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
        host = new GameObject("SettingsScrollTests");
        view = host.AddComponent<LobbyOptionsView>();
        view.BuildForTests();
        panel = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll");
        page = panel.Find("OptionsPage");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        MukJumpIdentityProfile.UseStoreForTests(null);
        LobbySettingsProfile.RestoreDefaultStoreForTests();
        PointerInput.ResetSuppressionForTests();
    }

    [Test]
    public void MainSettingsHasThreeCompactTogglesNoMotionOptionAndReadableLabels()
    {
        Assert.That(panel.Find("PaperCore"), Is.Null);
        Assert.That(panel.Find("HanjiScrollArt/Paper"), Is.Not.Null);
        Assert.That(panel.Find("DebugScenarioPage"), Is.Null);
        Assert.That(page.GetComponentsInChildren<Slider>(true), Is.Empty);
        Assert.That(page.Find("MotionToggle"), Is.Null);
        Assert.That(panel.Find("PlaySettingsPage/ReducedMotionButton"), Is.Null);
        foreach (string name in new[] { "BgmToggle", "SfxToggle", "HapticsToggle" })
        {
            var rect = (RectTransform)page.Find(name);
            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(120));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(183));
        }
        foreach (Text text in page.GetComponentsInChildren<Text>(true))
        {
            Assert.That(text.text, Does.Not.Contain("DEBUG"));
            Assert.That(text.resizeTextForBestFit, Is.EqualTo(text.name == "Nickname"));
            Canvas.ForceUpdateCanvases();
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), $"{text.name}: {text.text}, {text.font?.name}, {text.fontSize}");
        }
    }

    [TestCase("BgmToggle")]
    [TestCase("SfxToggle")]
    [TestCase("HapticsToggle")]
    public void QuickToggleContentsKeepTopAndBottomPaperInsets(string name)
    {
        Canvas.ForceUpdateCanvases();
        var paper = (RectTransform)page.Find(name + "/Paper");
        var icon = (RectTransform)paper.Find("Icon");
        var title = paper.Find("Name").GetComponent<Text>();
        var state = paper.Find("State").GetComponent<Text>();
        float Top(RectTransform r) => r.anchoredPosition.y + r.rect.yMax;
        float Bottom(RectTransform r) => r.anchoredPosition.y + r.rect.yMin;
        Assert.That(paper.rect.yMax - Top(icon), Is.GreaterThanOrEqualTo(24));
        Assert.That(Bottom(state.rectTransform) - paper.rect.yMin, Is.GreaterThanOrEqualTo(24));
        Assert.That(icon.rect.size, Is.EqualTo(new Vector2(80, 80)));
        Assert.That(Bottom(icon) - Top(title.rectTransform), Is.GreaterThanOrEqualTo(16));
        Assert.That(Bottom(title.rectTransform) - Top(state.rectTransform), Is.GreaterThanOrEqualTo(10));
        foreach (var text in new[] { title, state })
        {
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
        }
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void SettingsHeaderAndCaptionsFitBothLanguagesWithoutTouchingTheRoll(GameLanguage language)
    {
        GameLocalization.SetLanguage(language);
        Canvas.ForceUpdateCanvases();
        var paper = (RectTransform)panel.Find("HanjiScrollArt/Paper");
        var title = page.Find("Title").GetComponent<Text>();
        Assert.That(title.text, Is.EqualTo(language == GameLanguage.English ? "Settings" : "설정"));
        Assert.That(paper.rect.yMax - title.rectTransform.anchoredPosition.y - title.rectTransform.rect.yMax,
            Is.GreaterThanOrEqualTo(30), "제목이 위 롤에 붙지 않아야 합니다.");
        foreach (var text in page.GetComponentsInChildren<Text>(true))
        {
            if (string.IsNullOrEmpty(text.text)) continue;
            Assert.That(text.resizeTextForBestFit, Is.EqualTo(text.name == "Nickname"), text.name);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
            if (text.name != "Nickname")
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.name);
        }
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void SettingsFooterKeepsSaveWarningsReadableAboveLegalText(GameLanguage language)
    {
        GameLocalization.SetLanguage(language);
        var status = page.Find("ConnectionStatus").GetComponent<Text>();
        var account = (RectTransform)page.Find("AccountButton");
        var legal = (RectTransform)page.Find("TermsButton");
        var legalText = (RectTransform)legal.Find("Label");
        foreach (string source in new[] { "언어를 저장하지 못했어요. 다시 시도해 주세요", "cysbandcs@gmail.com" })
        {
            typeof(LobbyOptionsView).GetMethod("SetSettingsStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { source });
            Canvas.ForceUpdateCanvases();
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1));
            Assert.That(account.anchoredPosition.y + account.rect.yMin -
                status.rectTransform.anchoredPosition.y - status.rectTransform.rect.yMax, Is.GreaterThanOrEqualTo(12));
            Assert.That(status.rectTransform.anchoredPosition.y + status.rectTransform.rect.yMin -
                legal.anchoredPosition.y - legalText.rect.yMax, Is.GreaterThanOrEqualTo(10));
        }
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    [TestCase(GameLanguage.Japanese)]
    public void EmptySettingsFooterCollapsesWithoutMovingUpperControls(GameLanguage language)
    {
        GameLocalization.SetLanguage(language);
        var rect = (RectTransform)panel;
        var title = (RectTransform)page.Find("Title");
        var account = (RectTransform)page.Find("AccountButton");
        var status = page.Find("ConnectionStatus").GetComponent<Text>();
        var setStatus = typeof(LobbyOptionsView).GetMethod("SetSettingsStatus",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Canvas.ForceUpdateCanvases();
        Vector3 titlePosition = title.position;
        Vector3 accountPosition = account.position;
        for (int i = 0; i < 3; i++)
        {
            Assert.That(rect.rect.height, Is.EqualTo(1220));
            Assert.That(status.gameObject.activeSelf, Is.False);
            setStatus.Invoke(view, new object[] { "언어를 저장하지 못했어요. 다시 시도해 주세요" });
            Canvas.ForceUpdateCanvases();
            Assert.That(rect.rect.height, Is.EqualTo(1330));
            Assert.That(status.gameObject.activeSelf, Is.True);
            Assert.That(title.position.y, Is.EqualTo(titlePosition.y).Within(.01f));
            Assert.That(account.position.y, Is.EqualTo(accountPosition.y).Within(.01f));
            setStatus.Invoke(view, new object[] { string.Empty });
            view.OpenTutorialForTests();
            typeof(LobbyOptionsView).GetMethod("ShowOptionsPageImmediate",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Canvas.ForceUpdateCanvases();
            Assert.That(rect.rect.height, Is.EqualTo(1220));
            Assert.That(title.position.y, Is.EqualTo(titlePosition.y).Within(.01f));
            Assert.That(account.position.y, Is.EqualTo(accountPosition.y).Within(.01f));
        }
    }

    [TestCase(GameLanguage.Korean, false)]
    [TestCase(GameLanguage.English, false)]
    [TestCase(GameLanguage.Korean, true)]
    [TestCase(GameLanguage.English, true)]
    public void RenderSettingsSpacingFromActualUi(GameLanguage language, bool account)
    {
        // Play/서버/사용자 씬 저장 없이 실제 uGUI만 격리 카메라로 확인한다.
        GameLocalization.SetLanguage(language);
        LobbySettingsProfile.SetBgmVolume(0);
        LobbySettingsProfile.SetSfxVolume(0);
        LobbySettingsProfile.SetHapticsEnabled(false);
        view.BuildForTests();
        if (account)
            typeof(LobbyOptionsView).GetMethod("ShowAccountPageImmediate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
        view.SetDisplayMetricsForTests(1080, 1920, new Rect(0, 0, 1080, 1920));
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        var cameraHost = new GameObject("SettingsSpacingCamera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraHost, scene);
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D capture = null;
        try
        {
            var canvas = host.GetComponentInChildren<Canvas>(true);
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(1080, 1920);
            rect.position = Vector3.zero;
            rect.localScale = Vector3.one;
            canvas.GetComponent<CanvasGroup>().alpha = 1;
            canvas.GetComponent<CanvasGroup>().interactable = true;
            panel.GetComponent<CanvasGroup>().alpha = 1;
            panel.GetComponent<CanvasGroup>().interactable = true;
            panel.GetComponent<HanjiScrollFrame>().SetPose(1, 0, false);
            panel.Find("HanjiScrollArt").GetComponent<CanvasGroup>().alpha = 1;
            foreach (Transform child in host.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var camera = cameraHost.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = InkPalette.Paper;
            const int width = 540, height = 960;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.worldCamera = camera;
            canvas.enabled = false;
            canvas.enabled = true;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                button.enabled = false;
                button.enabled = true;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            capture.Apply();
            string folder = account ? "output/quality-polish/account-alignment" : "output/quality-polish/settings-spacing";
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes($"{folder}/{language.ToString().ToLowerInvariant()}.png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cameraHost.GetComponent<Camera>().targetTexture = null;
            if (capture != null) Object.DestroyImmediate(capture);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            Object.DestroyImmediate(cameraHost);
            Object.DestroyImmediate(host);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void SettingsUuidIsAQuietCopyableLineBetweenVersionAndDivider()
    {
        var title = page.Find("Title").GetComponent<Text>();
        var version = page.Find("Version").GetComponent<Text>();
        var button = page.Find("UuidButton").GetComponent<Button>();
        var label = button.transform.Find("Label").GetComponent<Text>();
        var hit = (RectTransform)button.transform;
        var divider = (RectTransform)page.Find("HeaderDivider");
        var toggle = (RectTransform)page.Find("BgmToggle");
        Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
        Assert.That(button.transform.Find("Paper"), Is.Null);
        Assert.That(hit.rect.height, Is.GreaterThanOrEqualTo(InkUiStyle.MinimumTapHeight));
        Assert.That(hit.anchoredPosition.y + hit.rect.yMin,
            Is.GreaterThanOrEqualTo(toggle.anchoredPosition.y + toggle.rect.yMax));
        float uuidTop = hit.anchoredPosition.y + label.rectTransform.rect.yMax;
        float uuidBottom = hit.anchoredPosition.y + label.rectTransform.rect.yMin;
        Assert.That(version.rectTransform.anchoredPosition.y + version.rectTransform.rect.yMin - uuidTop,
            Is.GreaterThanOrEqualTo(8));
        Assert.That(uuidBottom - (divider.anchoredPosition.y + divider.rect.yMax), Is.GreaterThanOrEqualTo(12));
        Assert.That(title.rectTransform.anchoredPosition.y + title.rectTransform.rect.yMin,
            Is.GreaterThan(version.rectTransform.anchoredPosition.y + version.rectTransform.rect.yMax));
        Assert.That(label.supportRichText, Is.False);
        Assert.That(label.resizeTextForBestFit, Is.False);
        Assert.That(label.raycastTarget, Is.False);
        Assert.That(label.fontSize, Is.EqualTo(30));
        Assert.That(label.color, Is.EqualTo(InkPalette.TextMuted));

        var apply = typeof(LobbyOptionsView).GetMethod("ApplySettingsUuid", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (string id in new[] { "", "01234567-89ab-cdef-0123-456789abcdef", new string('a', 64), "" })
        {
            apply.Invoke(view, new object[] { id });
            Canvas.ForceUpdateCanvases();
            Assert.That(label.text, Is.EqualTo(LobbyOptionsView.FormatSettingsUuid(id)));
            Assert.That(button.interactable, Is.EqualTo(id.Length > 0));
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width));
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height));
            Assert.That(typeof(LobbyOptionsView).GetField("settingsUuid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view),
                Is.EqualTo(id), "복사용 값은 긴 ID도 끝까지 보존해야 합니다.");
        }
    }

    [TestCase(null, "UID —")]
    [TestCase("   ", "UID —")]
    [TestCase("abc-123", "UID abc-123")]
    [TestCase("  abc-123  ", "UID abc-123")]
    [TestCase("1234567890123", "UID 1234567890123")]
    [TestCase("01234567-89ab-cdef-0123-456789abcdef", "UID 01234567…abcdef")]
    [TestCase("0123456789012345678901234567890123456789012345678901234567890123", "UID 01234567…890123")]
    public void SettingsUuidStaysOnOneLineWithoutInventingAnId(string source, string expected) =>
        Assert.That(LobbyOptionsView.FormatSettingsUuid(source), Is.EqualTo(expected));

    [TestCase(false, "native-id", "toss-hash", "native-id")]
    [TestCase(true, "native-id", "toss-hash", "toss-hash")]
    [TestCase(true, "native-id", "", "")]
    [TestCase(false, "", "toss-hash", "")]
    [TestCase(false, null, null, "")]
    [TestCase(true, "native-id", " toss-hash ", "toss-hash")]
    public void SettingsUuidNeverMixesPlatformAccounts(bool toss, string nativeId, string tossId, string expected) =>
        Assert.That(LobbyOptionsView.ResolveSettingsUuid(toss, nativeId, tossId), Is.EqualTo(expected));

    [TestCase(1080, 1920, 0, 0)]
    [TestCase(1170, 2532, 102, 141)]
    [TestCase(1080, 2400, 72, 90)]
    public void DisplayFixtureUsesInjectedSizeAndSafeAreaTogether(int width, int height, int bottom, int top)
    {
        Rect original = MobileUiLayout.CurrentSafeArea;
        var safe = new Rect(0, bottom, width, height - bottom - top);
        view.SetDisplayMetricsForTests(width, height, safe);
        var root = (RectTransform)panel.parent;
        Assert.That(root.anchorMin.y, Is.EqualTo((float)bottom / height).Within(0.00001f));
        Assert.That(root.anchorMax.y, Is.EqualTo(1f - (float)top / height).Within(0.00001f));
        Assert.That(root.anchorMin.x, Is.Zero);
        Assert.That(root.anchorMax.x, Is.EqualTo(1));
        var scroll = (RectTransform)panel;
        Vector2 logicalSafe = MobileUiLayout.GetLogicalSafeSize(safe, width, height);
        Assert.That(scroll.rect.width * scroll.localScale.x + 24f, Is.LessThanOrEqualTo(logicalSafe.x + 0.01f));
        Assert.That((scroll.rect.height + 190f) * scroll.localScale.y + 24f,
            Is.LessThanOrEqualTo(logicalSafe.y + 0.01f));
        Assert.That(scroll.anchoredPosition.y, Is.EqualTo(95f * scroll.localScale.y).Within(0.01f));
        Assert.That(MobileUiLayout.CurrentSafeArea, Is.EqualTo(original), "전역 화면 설정을 바꾸면 안 됩니다.");
    }

    [Test]
    public void BackdropDismissesSettingsButNotPaperInteriorOrDrag()
    {
        view.OpenTutorialForTests();
        Canvas.ForceUpdateCanvases();
        var dim = host.transform.Find("LobbyOptionsCanvas/InkDim").GetComponent<EventTrigger>();
        Assert.That(dim, Is.Not.Null);
        var pointer = new PointerEventData(null)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, panel.position)
        };
        dim.OnPointerClick(pointer);
        Assert.That(view.IsOpen, Is.True, "종이 안 빈 공간은 닫힘이 아니다");
        pointer.position = new Vector2(-100, -100);
        pointer.dragging = true;
        dim.OnPointerClick(pointer);
        Assert.That(view.IsOpen, Is.True, "드래그를 마치는 동작은 닫힘이 아니다");
        pointer.dragging = false;
        pointer.button = PointerEventData.InputButton.Right;
        dim.OnPointerClick(pointer);
        Assert.That(view.IsOpen, Is.True);
        pointer.button = PointerEventData.InputButton.Left;
        dim.OnPointerClick(pointer);
        Assert.That(view.IsOpen, Is.True, "튜토리얼 바깥 클릭은 옵션으로만 돌아간다");
        Assert.That(view.IsTutorialOpen, Is.False);
        Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        dim.OnPointerClick(pointer);
        Assert.That(view.IsOpen, Is.False, "옵션에서 다시 바깥을 누르면 닫힌다");
    }

    [Test]
    public void RoutineSettingsDoNotRepeatInstructionCaptions()
    {
        Assert.That(page.Find("AccountSummary"), Is.Null);
        Transform details = panel.Find("PlaySettingsPage");
        foreach (string name in new[] { "PlaySettingsCaption", "ReducedMotionCaption", "AdConsentCaption" })
            Assert.That(details.Find(name), Is.Null, name);
        Assert.That(panel.Find("AccountPage/AccountConflict/ConflictFootnote"), Is.Null);
        Assert.That(panel.Find("AccountPage/SyncConflict/SyncConflictFootnote"), Is.Null);
        Assert.That(panel.Find("AccountPage/AccountSyncPending/SyncPendingFootnote"), Is.Null);
        // 삭제·기록 선택 경고는 불필요한 설명이 아니다.
        Assert.That(panel.Find("AccountPage/SyncConflict/SyncConflictCaption").GetComponent<Text>().text,
            Does.Contain("합쳐지지"));
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    [TestCase(GameLanguage.Japanese)]
    public void LanguagePageReturnsThroughDimWithoutClosingSettingsOrChangingLanguage(GameLanguage language)
    {
        GameLanguage original = GameLocalization.Language;
        try
        {
            GameLocalization.SetLanguage(language);
            view.OpenTutorialForTests();
            InvokeAccount("ShowOptionsPageImmediate");
            Canvas.ForceUpdateCanvases();
            Vector3 settingsTop = panel.Find("HanjiScrollArt/TopRoll").position;
            page.Find("LanguageButton").GetComponent<Button>().onClick.Invoke();
            var chooser = panel.Find("LanguagePage").GetComponent<CanvasGroup>();
            Assert.That(chooser.transform.Find("LanguageBack"), Is.Null);
            Assert.That(chooser.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
            var dim = host.transform.Find("LobbyOptionsCanvas/InkDim").GetComponent<EventTrigger>();
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, panel.position)
            };
            dim.OnPointerClick(pointer);
            Assert.That(chooser.blocksRaycasts, Is.True, "종이 내부는 뒤로가기가 아닙니다.");
            pointer.position = new Vector2(-100, -100);
            pointer.dragging = true;
            dim.OnPointerClick(pointer);
            Assert.That(chooser.blocksRaycasts, Is.True);
            pointer.dragging = false;
            pointer.button = PointerEventData.InputButton.Right;
            dim.OnPointerClick(pointer);
            Assert.That(chooser.blocksRaycasts, Is.True);
            pointer.button = PointerEventData.InputButton.Left;
            dim.OnPointerClick(pointer);
            Canvas.ForceUpdateCanvases();
            Assert.That(view.IsOpen, Is.True, "전체 설정을 닫지 않고 한 단계만 돌아갑니다.");
            Assert.That(chooser.blocksRaycasts, Is.False);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(GameLocalization.Language, Is.EqualTo(language));
            Assert.That(panel.Find("HanjiScrollArt/TopRoll").position, Is.EqualTo(settingsTop));
        }
        finally { GameLocalization.SetLanguage(original); }
    }

    [TestCase(RuntimePlatform.WebGLPlayer, true)]
    [TestCase(RuntimePlatform.IPhonePlayer, false)]
    [TestCase(RuntimePlatform.Android, false)]
    public void TossSettingsPolicyDoesNotRemoveNativeSignIn(RuntimePlatform platform, bool expected)
    {
        Assert.That(LobbyOptionsView.UsesTossSettingsFor(platform), Is.EqualTo(expected));
    }

    [TestCase("게스트로 바로 플레이할 수 있습니다", "")]
    [TestCase("동기화 완료", "")]
    [TestCase("서버 설정 전에도 게스트로 모든 콘텐츠를 플레이할 수 있습니다", "")]
    [TestCase("서버 저장을 다시 시도합니다", "서버 저장을 다시 시도합니다")]
    [TestCase("서버 연결을 다시 확인하고 있습니다", "서버 연결을 다시 확인하고 있습니다")]
    public void AccountCopyOnlyHidesKnownRoutineMessagesNotSaveOrReconnectWarnings(string source, string expected)
    {
        Assert.That(LobbyOptionsView.EssentialAccountStatus(source), Is.EqualTo(expected));
    }

    [Test]
    public void TossOptionsOmitLeaderboardAndNativeNicknameOrLoginMenus()
    {
        WithTossOptions((tossView, tossPanel) =>
        {
            foreach (string name in new[] { "GoogleLoginButton", "AppleLoginButton", "AccountLogout",
                         "AccountDelete", "AccountConflict", "SyncConflict", "AccountSyncPending" })
                Assert.That(tossPanel.Find("AccountPage/" + name), Is.Null, name);
            Assert.That(tossPanel.Find("LeaderboardPage"), Is.Not.Null);
            Assert.That(tossPanel.Find("LeaderboardPage/AppleLeaderboard"), Is.Null);
            Assert.That(tossPanel.Find("LeaderboardPage/LeaderboardRefresh"), Is.Null);
            Assert.That(tossPanel.Find("OptionsPage/AdPrivacyButton"), Is.Null);
            Assert.That(tossPanel.Find("PlaySettingsPage/AdConsentButton"), Is.Null);
            Assert.That(tossPanel.Find("OptionsPage/AccountButton"), Is.Null);
            Assert.That(tossPanel.Find("OptionsPage/LeaderboardMenuButton"), Is.Null);
            Assert.That(tossPanel.Find("OptionsPage/NicknameButton"), Is.Null, "토스 이름은 플랫폼 정책을 유지한다");
            Assert.That(tossPanel.Find("AccountPage/RetryTossIdentity").GetComponent<Button>(), Is.Not.Null);
        });
    }

    [Test]
    public void LeaderboardColumnsKeepNamesScoresAndSourceSeparate()
    {
        Transform ranking = panel.Find("LeaderboardPage");
        Assert.That(ranking, Is.Not.Null);
        for (int i = 1; i <= 10; i++)
        {
            var nameCell = ranking.Find("NameCell" + i).GetComponent<RectTransform>();
            var name = nameCell.Find("Name" + i).GetComponent<Text>();
            var score = ranking.Find("LeaderboardRow" + i).GetComponent<Text>();
            Assert.That(ranking.Find("Source" + i), Is.Null);
            Assert.That(name.supportRichText, Is.False);
            Assert.That(name.resizeTextForBestFit, Is.False);
            Assert.That(name.fontSize, Is.GreaterThanOrEqualTo(36));
            Assert.That(nameCell.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(nameCell.anchoredPosition.x + nameCell.rect.width / 2,
                Is.LessThan(score.rectTransform.anchoredPosition.x - score.rectTransform.rect.width / 2));
            Assert.That(name.text, Is.Empty, "서버 응답 전에는 가짜 이름을 표시하지 않습니다.");
            Assert.That(score.text, Is.Empty);
            Assert.That(score.alignment, Is.EqualTo(TextAnchor.MiddleRight));
        }
    }

    [TestCase("WWWWWWWWWWWWWWWWWW")]
    [TestCase("가나다라마바사아자차카타파하")]
    [TestCase("👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦가나다라마")]
    [TestCase("e\u0301e\u0301e\u0301가나다라마바사")]
    [TestCase("<size=200>먹방울</size>")]
    [TestCase("이름 없는 먹방울")]
    public void WorstCaseNamesFitTheActualFontWidthWithoutShrinking(string value)
    {
        var text = panel.Find("LeaderboardPage/NameCell1/Name1").GetComponent<Text>();
        foreach (float width in new[] { 90f, 340f })
        {
            text.rectTransform.sizeDelta = new Vector2(width, 56f);
            text.supportRichText = false;
            text.text = value;
            bool overflow = text.preferredWidth > width - 4f;
            LobbyOptionsView.FitLeaderboardName(text, value);
            Canvas.ForceUpdateCanvases();
            Assert.That(text.text, Is.Not.Empty);
            if (overflow) Assert.That(text.text, Does.EndWith("…"));
            else Assert.That(text.text, Is.EqualTo(value));
            Assert.That(text.supportRichText, Is.False);
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.fontSize, Is.EqualTo(40));
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(width - 4f));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(56f));
        }
    }

    [TestCase(RuntimePlatform.IPhonePlayer, true)]
    [TestCase(RuntimePlatform.Android, false)]
    [TestCase(RuntimePlatform.WebGLPlayer, false)]
    public void PlatformLeaderboardTabsDoNotClaimAUnifiedRanking(RuntimePlatform platform, bool apple)
    {
        var platformHost = new GameObject("PlatformLeaderboardTest");
        platformHost.SetActive(false);
        try
        {
            var platformView = platformHost.AddComponent<LobbyOptionsView>();
            platformView.BuildForPlatformForTests(platform);
            var ranking = platformHost.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            foreach (string removed in new[] { "AppleLeaderboard", "GlobalLeaderboard", "LeaderboardBack",
                "LeaderboardDone", "LeaderboardRefresh", "SourceHeading" })
                Assert.That(ranking.Find(removed), Is.Null, removed);
            Assert.That(ranking.Find("LeaderboardStatus"), Is.Null);
            var account = ranking.parent.Find("AccountPage");
            var appleLogin = account.Find("AppleLoginButton");
            if (platform == RuntimePlatform.WebGLPlayer)
            {
                Assert.That(appleLogin, Is.Null);
                Assert.That(account.Find("RetryTossIdentity"), Is.Not.Null);
            }
            else
            {
                Assert.That(appleLogin.gameObject.activeSelf, Is.EqualTo(apple));
            }
        }
        finally { Object.DestroyImmediate(platformHost); }
    }

    [TestCase(null, "이름 없는 먹방울")]
    [TestCase("   ", "이름 없는 먹방울")]
    [TestCase("먹방울\n\t", "먹방울")]
    [TestCase("가나다라마바사아", "가나다라마바사아")]
    [TestCase("guest(1234567890)", "guest(1234567890)")]
    [TestCase("123456789012345678901234", "12345678901234567890…")]
    public void LeaderboardNamesAreBoundedAndDoNotExposeAccountIds(string raw, string expected)
    {
        var entry = new MukJumpLeaderboardEntry(1, 132, raw);
        Assert.That(entry.DisplayName, Is.EqualTo(expected));
        Assert.That(entry.Source, Is.EqualTo("BACKND"));
    }

    [Test]
    public void TossIdentityRecoveryPreservesErrorsAndReturnsToOptionsOnlyWhenReady()
    {
        WithTossOptions((tossView, tossPanel) =>
        {
            tossView.OpenTutorialForTests();
            typeof(LobbyOptionsView).GetMethod("ShowAccountPage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(tossView, null);
            var apply = typeof(LobbyOptionsView).GetMethod("ApplyTossIdentityState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var retry = tossPanel.Find("AccountPage/RetryTossIdentity").GetComponent<Button>();
            var status = tossPanel.Find("AccountPage/AccountStatus").GetComponent<Text>();
            apply.Invoke(tossView, new object[] { false, true, "확인 중" });
            Assert.That(retry.interactable, Is.False);
            Assert.That(tossView.IsAccountOpen, Is.True);
            const string error = "다른 사용자의 기록입니다. 토스 계정을 확인해 주세요";
            apply.Invoke(tossView, new object[] { false, false, error });
            Assert.That(retry.interactable, Is.True);
            Assert.That(status.text, Is.EqualTo(error));
            Assert.That(tossView.IsAccountOpen, Is.True);
            apply.Invoke(tossView, new object[] { true, false, "확인 완료" });
            Assert.That(tossView.IsAccountOpen, Is.False);
            Assert.That(tossPanel.Find("OptionsPage").GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(status.text, Is.Empty, "사용자 식별 완료를 클라우드 동기화 완료로 표시하면 안 된다");
        });
    }

    static void WithTossOptions(System.Action<LobbyOptionsView, Transform> inspect)
    {
        var tossHost = new GameObject("TossSettingsTests");
        tossHost.SetActive(false);
        try
        {
            var tossView = tossHost.AddComponent<LobbyOptionsView>();
            tossView.BuildForPlatformForTests(RuntimePlatform.WebGLPlayer);
            inspect(tossView, tossHost.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll"));
        }
        finally { Object.DestroyImmediate(tossHost); }
    }

    [Test]
    public void IconMenuUsesTwoAlignedColumnsWithoutSoundOrAdMenu()
    {
        string[] names = { "LanguageButton", "CustomerCenterButton", "GuideButton", "NicknameButton" };
        for (int i = 0; i < names.Length; i++)
        {
            var rect = (RectTransform)page.Find(names[i]);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(i % 2 == 0 ? -178 : 178, i < 2 ? 56 : -88)));
            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(120));
            var icon = rect.Find("Paper/Icon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.Not.Null, names[i]);
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.color, Is.EqualTo(Color.white), "원화 한지색을 검정 tint로 뭉개지 않는다");
        }
        Assert.That(page.Find("AudioSettingsButton"), Is.Null);
        Assert.That(page.Find("LeaderboardMenuButton"), Is.Null);
        Assert.That(page.Find("AdPrivacyButton"), Is.Null);
        Assert.That(page.Find("LanguageButton/Paper/Label").GetComponent<Text>().text, Is.EqualTo("한국어"));
        Assert.That(panel.Find("AccountPage/AccountSupportCode"), Is.Null);
        Assert.That(panel.Find("AccountPage/CopySupportCode"), Is.Null);
        Assert.That(panel.Find("AccountPage/AccountLegal"), Is.Null);
        Assert.That(panel.Find("AccountPage/LeaderboardButton"), Is.Null);
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void NicknameMenuIconMatchesOtherTwoColumnButtonsWithoutCrowdingTheCaption(GameLanguage language)
    {
        GameLocalization.SetLanguage(language);
        Canvas.ForceUpdateCanvases();
        var icon = page.Find("NicknameButton/Paper/Icon").GetComponent<Image>();
        var accountIcon = page.Find("CustomerCenterButton/Paper/Icon").GetComponent<Image>();
        var label = page.Find("NicknameButton/Paper/Label").GetComponent<Text>();
        var accountLabel = page.Find("CustomerCenterButton/Paper/Label").GetComponent<Text>();
        Assert.That(label.text, Is.EqualTo(language == GameLanguage.English ? "Nickname" : "닉네임"));
        Assert.That(icon.enabled, Is.True);
        Assert.That(icon.sprite, Is.EqualTo(Resources.Load<Sprite>("MukJump/UI/Common/settings_icon_nickname_v1")));
        Assert.That(icon.sprite, Is.Not.Null);
        Assert.That(icon.sprite, Is.Not.EqualTo(accountIcon.sprite));
        Assert.That(icon.preserveAspect, Is.True);
        Assert.That(icon.raycastTarget, Is.False, "아이콘 위를 눌러도 닉네임 버튼에 입력을 전달한다");
        Assert.That(icon.color, Is.EqualTo(Color.white));
        Assert.That(icon.rectTransform.anchoredPosition, Is.EqualTo(accountIcon.rectTransform.anchoredPosition));
        Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(accountIcon.rectTransform.sizeDelta));
        Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(accountLabel.rectTransform.anchoredPosition));
        Assert.That(label.rectTransform.anchoredPosition.x + label.rectTransform.rect.xMin,
            Is.GreaterThan(icon.rectTransform.anchoredPosition.x + icon.rectTransform.rect.xMax));
        Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1));
        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
    }

    [Test]
    public void SettingsGroupsKeepBreathingRoomWithoutOverlappingLowerContent()
    {
        var sound = (RectTransform)page.Find("BgmToggle");
        var language = (RectTransform)page.Find("LanguageButton");
        var guide = (RectTransform)page.Find("GuideButton");
        var account = (RectTransform)page.Find("AccountButton");
        var status = (RectTransform)page.Find("ConnectionStatus");
        var legal = (RectTransform)page.Find("TermsButton");
        float Gap(RectTransform upper, RectTransform lower) =>
            upper.anchoredPosition.y - upper.rect.height / 2 -
            (lower.anchoredPosition.y + lower.rect.height / 2);
        Assert.That(Gap(sound, language), Is.InRange(40f, 60f));
        Assert.That(Gap(language, guide), Is.GreaterThanOrEqualTo(16f));
        Assert.That(Gap(guide, account), Is.InRange(32f, 40f), "이전 닉네임 행의 빈자리는 남기지 않는다");
        Assert.That(status.gameObject.activeSelf, Is.False, "빈 안내 영역을 예약하지 않는다");
        Assert.That(Gap(account, legal), Is.InRange(24f, 36f));
        Assert.That(account.anchoredPosition, Is.EqualTo(new Vector2(0, -367)));
        Assert.That(legal.anchoredPosition.y - legal.rect.height / 2, Is.GreaterThan(-580f));
    }

    [TestCase("music")]
    [TestCase("sound")]
    [TestCase("haptics")]
    [TestCase("motion")]
    [TestCase("language")]
    [TestCase("support")]
    [TestCase("tutorial")]
    [TestCase("nickname")]
    [TestCase("account")]
    [TestCase("rank")]
    public void PaintedSettingsIconsAreImportedAsSmallTransparentSprites(string key)
    {
        string path = $"Assets/Resources/MukJump/UI/Common/settings_icon_{key}_v1.png";
        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        Assert.That(sprite, Is.Not.Null, path);
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.isReadable, Is.False);
        Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        Assert.That(importer.spriteImportMode, Is.EqualTo(UnityEditor.SpriteImportMode.Single));
        Assert.That(sprite.texture.width, Is.LessThanOrEqualTo(256));
    }

    [Test]
    public void RankingUsesSeparateLeftRankAndRightScoreWithoutExtraButtons()
    {
        for (int i = 1; i <= 10; i++)
        {
            var score = panel.Find($"LeaderboardPage/LeaderboardRow{i}").GetComponent<Text>();
            var rank = panel.Find($"LeaderboardPage/Rank{i}").GetComponent<Text>();
            Assert.That(score.alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(rank.alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(score.fontSize, Is.GreaterThanOrEqualTo(42));
            Assert.That(rank.fontStyle, Is.EqualTo(FontStyle.Bold));
        }
        view.OpenTutorialForTests();
        typeof(LobbyOptionsView).GetMethod("ShowLeaderboardPage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, null);
        Assert.That(panel.Find("LeaderboardPage").GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        Assert.That(panel.Find("LeaderboardPage/LeaderboardBack"), Is.Null);
        Assert.That(panel.Find("LeaderboardPage").GetComponentsInChildren<Button>(true), Is.Empty);
        Assert.That(view.IsAccountOpen, Is.False);
    }

    [Test]
    public void CustomerSupportOpensAMailDraftWithoutSendingOrAddingAccountData()
    {
        var uri = new System.Uri(LobbyOptionsView.CustomerSupportMailUri);
        Assert.That(uri.Scheme, Is.EqualTo("mailto"));
        Assert.That(LobbyOptionsView.CustomerSupportMailUri, Does.StartWith("mailto:cysbandcs@gmail.com?subject="));
        Assert.That(System.Uri.UnescapeDataString(uri.Query), Does.Contain("먹점프 고객 문의"));
        Assert.That(uri.Query, Does.Not.Contain("body="));
        Assert.That(uri.Query, Does.Not.Contain("token"));
    }

    [Test]
    public void RankingDimReturnsToMainWithoutOpeningSettings()
    {
        // 게임 월드를 생성하지 않는 비활성 로비 상태로 공개 진입 API를 검증한다.
        var managerHost = new GameObject("InactiveRankingManager");
        managerHost.SetActive(false);
        var instance = typeof(GameManager).GetProperty("Instance");
        var previous = GameManager.Instance;
        try
        {
            instance.SetValue(null, managerHost.AddComponent<GameManager>());
            view.OpenLeaderboard();
            Assert.That(page.Find("LeaderboardMenuButton"), Is.Null);
            var ranking = panel.Find("LeaderboardPage").GetComponent<CanvasGroup>();
            Assert.That(ranking.blocksRaycasts, Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Canvas.ForceUpdateCanvases();
            var dim = host.transform.Find("LobbyOptionsCanvas/InkDim").GetComponent<EventTrigger>();
            var pointer = new PointerEventData(null)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, panel.position),
                button = PointerEventData.InputButton.Left
            };
            dim.OnPointerClick(pointer);
            Assert.That(ranking.blocksRaycasts, Is.True, "랭킹 안쪽 여백은 뒤로가기가 아닙니다.");
            pointer.position = new Vector2(-100, -100);
            dim.OnPointerClick(pointer);
            Assert.That(view.IsOpen, Is.False);
        }
        finally
        {
            instance.SetValue(null, previous);
            Object.DestroyImmediate(managerHost);
        }
    }

    [TestCase(false, MukJumpAccountKind.LocalGuest, false, true, true)]
    [TestCase(false, MukJumpAccountKind.LocalGuest, false, false, false)]
    [TestCase(true, MukJumpAccountKind.BackendGuest, false, true, true)]
    [TestCase(true, MukJumpAccountKind.BackendGuest, true, true, true)]
    [TestCase(false, MukJumpAccountKind.BackendGuest, false, true, true)]
    [TestCase(true, MukJumpAccountKind.Google, false, true, false)]
    [TestCase(true, MukJumpAccountKind.Apple, false, true, false)]
    [TestCase(false, MukJumpAccountKind.Google, false, true, false)]
    [TestCase(false, MukJumpAccountKind.Apple, false, true, true)]
    [TestCase(false, MukJumpAccountKind.Apple, true, true, true)]
    public void AccountActionsOnlyOfferAppleAndPreserveAppleReauthentication(
        bool online, MukJumpAccountKind kind, bool busy, bool appleAvailable, bool apple)
    {
        typeof(LobbyOptionsView).GetMethod("ApplyAccountActions", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { online, kind, busy, appleAvailable });
        Transform account = panel.Find("AccountPage");
        Assert.That(account.Find("GoogleLoginButton"), Is.Null);
        var appleButton = account.Find("AppleLoginButton").GetComponent<Button>();
        Assert.That(appleButton.gameObject.activeSelf, Is.EqualTo(apple));
        if (busy)
        {
            Assert.That(appleButton.interactable, Is.False);
        }
        Assert.That(account.Find("AccountDelete").gameObject.activeSelf, Is.EqualTo(online));
        bool linked = kind == MukJumpAccountKind.Apple || kind == MukJumpAccountKind.Google;
        var logout = account.Find("AccountLogout").GetComponent<Button>();
        Assert.That(logout.gameObject.activeSelf, Is.EqualTo(online && linked));
        Assert.That(logout.interactable, Is.EqualTo(online && linked && !busy));
        Assert.That(account.Find("AccountDelete").GetComponent<Button>().interactable,
            Is.EqualTo(online && !busy), "기존 계정 삭제의 인증·진행 중 보호 조건을 유지합니다.");
    }

    [Test]
    public void GuestDeleteStaysCenteredAndLinkedLogoutReturnsAfterAccountChanges()
    {
        Transform account = panel.Find("AccountPage");
        var logout = account.Find("AccountLogout").GetComponent<Button>();
        var delete = account.Find("AccountDelete").GetComponent<Button>();
        foreach (var kind in new[] { MukJumpAccountKind.BackendGuest,
                     MukJumpAccountKind.Apple, MukJumpAccountKind.BackendGuest })
        {
            InvokeAccount("ApplyAccountActions", true, kind, false, true);
            Canvas.ForceUpdateCanvases();
            bool linked = kind == MukJumpAccountKind.Apple;
            Assert.That(logout.gameObject.activeSelf, Is.EqualTo(linked));
            Assert.That(delete.gameObject.activeSelf, Is.True);
            Assert.That(delete.interactable, Is.True);
            var deleteRect = (RectTransform)delete.transform;
            Assert.That(deleteRect.anchoredPosition.x, Is.EqualTo(linked ? 180f : 0f));
            if (linked)
            {
                var logoutRect = (RectTransform)logout.transform;
                Assert.That(logoutRect.anchoredPosition.x, Is.EqualTo(-180f));
                Assert.That(logoutRect.anchoredPosition.y, Is.EqualTo(deleteRect.anchoredPosition.y));
            }
            Assert.That(delete.GetComponentInChildren<Text>().text, Is.EqualTo("계정 삭제"));
        }
    }

    [TestCase(false, MukJumpAccountKind.LocalGuest, true)]
    [TestCase(false, MukJumpAccountKind.LocalGuest, false)]
    [TestCase(true, MukJumpAccountKind.BackendGuest, true)]
    [TestCase(true, MukJumpAccountKind.Google, true)]
    [TestCase(false, MukJumpAccountKind.Apple, true)]
    public void AccountSheetFitsVisibleActionsWithAnOutsideClose(
        bool online, MukJumpAccountKind kind, bool appleAvailable)
    {
        typeof(LobbyOptionsView).GetMethod("ShowAccountPage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, null);
        Transform account = panel.Find("AccountPage");
        Assert.That(account.Find("CopySupportCode"), Is.Null, "문의 코드 복사 버튼과 빈 행을 생성하지 않습니다.");
        typeof(LobbyOptionsView).GetMethod("ApplyAccountActions", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { online, kind, false, appleAvailable });
        Canvas.ForceUpdateCanvases();
        Assert.That(account.Find("AccountBack"), Is.Null);
        Assert.That(account.Find("AccountDone"), Is.Null);
        var paper = (RectTransform)panel.Find("HanjiScrollArt/Paper");
        Assert.That(paper.rect.height, Is.LessThan(1450));
        var title = (RectTransform)account.Find("AccountTitle");
        Assert.That(title.anchoredPosition.x, Is.Zero);
        Assert.That(paper.rect.yMax - title.anchoredPosition.y - title.rect.yMax, Is.GreaterThanOrEqualTo(50));
        float lastBottom = float.PositiveInfinity;
        string accountAction = account.Find("AccountLogout").gameObject.activeSelf
            ? "AccountLogout" : "AccountDelete";
        foreach (string name in new[] { "AppleLoginButton", accountAction })
        {
            var rect = (RectTransform)account.Find(name);
            if (!rect.gameObject.activeSelf) continue;
            float top = rect.anchoredPosition.y + rect.rect.yMax;
            float bottom = rect.anchoredPosition.y + rect.rect.yMin;
            if (!float.IsPositiveInfinity(lastBottom))
                Assert.That(lastBottom - top, Is.GreaterThanOrEqualTo(25), name);
            Assert.That(bottom - paper.rect.yMin, Is.GreaterThanOrEqualTo(60), name);
            lastBottom = bottom;
        }
        if (!float.IsPositiveInfinity(lastBottom))
            Assert.That(lastBottom - paper.rect.yMin, Is.LessThanOrEqualTo(130), "마지막 버튼 아래 큰 빈 공간을 남기지 않는다");
        var close = (RectTransform)account.Find("AccountClose");
        Assert.That(paper.rect.yMin - close.anchoredPosition.y - close.rect.yMax, Is.GreaterThanOrEqualTo(60));
        Assert.That(account.Find("AccountStatus"), Is.Null, "일반 안내 때문에 버튼 사이에 빈 행을 남기지 않습니다.");
        foreach (string name in new[] { "AccountTitle", "AccountKind", "AccountPlayerId" })
        {
            var label = account.Find(name).GetComponent<Text>();
            Assert.That(label.resizeTextForBestFit, Is.False);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
        }
        var kindRect = (RectTransform)account.Find("AccountKind");
        var idRect = (RectTransform)account.Find("AccountPlayerId");
        Assert.That(kindRect.anchoredPosition.y + kindRect.rect.yMin -
                    idRect.anchoredPosition.y - idRect.rect.yMax, Is.GreaterThanOrEqualTo(8));
        Assert.That(idRect.GetComponent<Text>().supportRichText, Is.False);
    }

    void InvokeAccount(string method, params object[] args) =>
        typeof(LobbyOptionsView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, args);

    const string ServerFailure = "서버 연결을 확인할 수 없습니다. 네트워크를 확인한 뒤 다시 시도해 주세요";

    [TestCase(false)]
    [TestCase(true)]
    public void AccountActionsSitDirectlyBelowIdentityWithoutReservedStatusSpace(bool hasId)
    {
        InvokeAccount("ShowAccountPageImmediate");
        var account = panel.Find("AccountPage");
        var id = account.Find("AccountPlayerId").GetComponent<Text>();
        id.text = hasId ? "UID test-user-id" : "";
        InvokeAccount("ApplyAccountActions", false, MukJumpAccountKind.LocalGuest, false, true);
        Canvas.ForceUpdateCanvases();
        Assert.That(id.gameObject.activeSelf, Is.EqualTo(hasId));
        var identity = hasId ? id.rectTransform : (RectTransform)account.Find("AccountKind");
        var apple = (RectTransform)account.Find("AppleLoginButton");
        float gap = identity.anchoredPosition.y + identity.rect.yMin - apple.anchoredPosition.y - apple.rect.yMax;
        Assert.That(gap, Is.InRange(24, 44));
        Vector2 size = ((RectTransform)panel).sizeDelta;
        Vector3 scale = panel.localScale;
        InvokeAccount("ShowAccountNotice", ServerFailure);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(((RectTransform)panel).sizeDelta, Is.EqualTo(size));
        Assert.That(panel.localScale, Is.EqualTo(scale), "안내가 표시되어도 팝업 크기는 변하지 않습니다.");
    }

    [Test]
    public void AccountToastFadesOnceAndPausesItsReadingTimeUntilVisible()
    {
        InvokeAccount("ShowAccountPageImmediate");
        var toast = panel.parent.Find("AccountToast").GetComponent<CanvasGroup>();
        InvokeAccount("RefreshAccountNotice", ServerFailure, false);
        InvokeAccount("AdvanceAccountToast", 10f, false);
        Assert.That(toast.alpha, Is.Zero);
        InvokeAccount("AdvanceAccountToast", .08f, true);
        Assert.That(toast.alpha, Is.EqualTo(.875f).Within(.01), "OutCubic 진입의 절반 시점");
        InvokeAccount("AdvanceAccountToast", .12f, true);
        Assert.That(toast.alpha, Is.EqualTo(1));
        InvokeAccount("RefreshAccountNotice", ServerFailure, false);
        InvokeAccount("AdvanceAccountToast", 4.08f, true);
        Assert.That(toast.alpha, Is.InRange(.45f, .55f), "반복 갱신은 표시 시간을 다시 시작하지 않습니다.");
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.Zero);
        InvokeAccount("RefreshAccountNotice", ServerFailure, false);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.Zero, "사라진 같은 오류가 상태 갱신마다 반복되지 않습니다.");
        InvokeAccount("RefreshAccountNotice", "문의 코드를 복사했어요", false);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.EqualTo(1));
    }

    [TestCase("ShowOptionsPageImmediate")]
    [TestCase("CloseImmediate")]
    [TestCase("OnDisable")]
    public void AccountToastClearsWhenLeavingThePopup(string method)
    {
        InvokeAccount("ShowAccountPageImmediate");
        InvokeAccount("ShowAccountNotice", ServerFailure);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        var toast = panel.parent.Find("AccountToast").GetComponent<CanvasGroup>();
        Assert.That(toast.alpha, Is.EqualTo(1));
        InvokeAccount(method);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.Zero);
    }

    [Test]
    public void AccountToastDoesNotReplaceRequiredSyncOrInterceptInput()
    {
        InvokeAccount("ShowAccountPageImmediate");
        var toast = panel.parent.Find("AccountToast").GetComponent<CanvasGroup>();
        Assert.That(toast.blocksRaycasts, Is.False);
        Assert.That(toast.interactable, Is.False);
        foreach (var graphic in toast.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
        InvokeAccount("ShowAccountNotice", ServerFailure);
        InvokeAccount("RefreshAccountNotice", ServerFailure, true);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.Zero);
        Assert.That(panel.Find("AccountPage/AccountSyncPending"), Is.Not.Null);
        InvokeAccount("RefreshAccountNotice", ServerFailure, false);
        InvokeAccount("AdvanceAccountToast", .2f, true);
        Assert.That(toast.alpha, Is.EqualTo(1));
    }

    [Test]
    public void CompactAccountStillReservesEnoughPaperForRequiredRecoveryDialogs()
    {
        InvokeAccount("ShowAccountPageImmediate");
        InvokeAccount("LayoutAccountContents", true);
        Canvas.ForceUpdateCanvases();
        foreach (string name in new[] { "AccountConflict", "SyncConflict", "AccountSyncPending" })
        {
            var dialog = panel.Find("AccountPage/" + name);
            var paper = (RectTransform)dialog.Find("HanjiScrollArt/Paper");
            foreach (var label in dialog.GetComponentsInChildren<Text>(true))
            {
                var rect = label.rectTransform;
                if (rect.parent != dialog) continue;
                Assert.That(rect.anchoredPosition.y + rect.rect.yMax, Is.LessThanOrEqualTo(paper.rect.yMax - 40), name);
                Assert.That(rect.anchoredPosition.y + rect.rect.yMin, Is.GreaterThanOrEqualTo(paper.rect.yMin + 40), name);
            }
        }
    }

    [TestCase(GameLanguage.Korean)]
    [TestCase(GameLanguage.English)]
    public void AccountToastTranslationsFitAndRefreshWithoutRestarting(GameLanguage language)
    {
        GameLanguage original = GameLocalization.Language;
        try
        {
            GameLocalization.SetLanguage(language);
            InvokeAccount("ShowAccountPageImmediate");
            foreach (string message in new[] { ServerFailure, "문의 코드를 복사했어요", "계정과 서버 기록이 영구 삭제됩니다",
                "확인 시간이 지나 다시 눌렀습니다. 잠시 후 한 번 더 눌러 주세요" })
            {
                InvokeAccount("ShowAccountNotice", message);
                var label = panel.parent.Find("AccountToast/Message").GetComponent<Text>();
                Canvas.ForceUpdateCanvases();
                Assert.That(label.text, Is.EqualTo(GameLocalization.Translate(message)));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
            }
            InvokeAccount("ShowAccountNotice", ServerFailure);
            InvokeAccount("AdvanceAccountToast", .2f, true);
            GameLocalization.SetLanguage(language == GameLanguage.Korean ? GameLanguage.English : GameLanguage.Korean);
            Assert.That(panel.parent.Find("AccountToast/Message").GetComponent<Text>().text,
                Is.EqualTo(GameLocalization.Translate(ServerFailure)));
            InvokeAccount("AdvanceAccountToast", 4.3f, true);
            Assert.That(panel.parent.Find("AccountToast").GetComponent<CanvasGroup>().alpha, Is.Zero);
        }
        finally { GameLocalization.SetLanguage(original); }
    }

    [TestCase(1080, 1920, 0, 0, 0, 0)]
    [TestCase(1170, 2532, 102, 141, 0, 0)]
    [TestCase(1080, 2400, 72, 90, 0, 0)]
    [TestCase(750, 1334, 0, 40, 0, 0)]
    [TestCase(1536, 2048, 40, 48, 0, 0)]
    [TestCase(1080, 1600, 40, 64, 0, 0)]
    [TestCase(1170, 2532, 102, 141, 30, 70)]
    public void AccountRowsStayCenteredAndKeepOutsideCloseInsideSafeArea(int width, int height, int bottom, int top, int left, int right)
    {
        var safe = new Rect(left, bottom, width - left - right, height - bottom - top);
        view.SetDisplayMetricsForTests(width, height, safe);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)panel;
        var roll = (RectTransform)panel.Find("HanjiScrollArt/TopRoll");
        Vector3 settingsScale = rect.localScale;
        var available = MobileUiLayout.GetLogicalSafeSize(safe, width, height);
        page.Find("AccountButton").GetComponent<Button>().onClick.Invoke();
        var account = panel.Find("AccountPage");
        Assert.That(account.Find("CopySupportCode"), Is.Null);
        foreach (bool online in new[] { false, true })
        foreach (bool apple in new[] { false, true })
        {
            typeof(LobbyOptionsView).GetMethod("ApplyAccountActions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, new object[] { online, MukJumpAccountKind.BackendGuest, false, apple });
            Canvas.ForceUpdateCanvases();
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero), "행 수가 변해도 계정 팝업 중심은 유지합니다.");
            Assert.That(rect.localScale.x, Is.LessThanOrEqualTo(settingsScale.x + .001f));
            Assert.That(rect.localScale.y, Is.EqualTo(rect.localScale.x));
            var paper = (RectTransform)panel.Find("HanjiScrollArt/Paper");
            Assert.That(paper.position, Is.EqualTo(rect.position), "종이 본체가 안전영역 가운데에 놓입니다.");
            float scale = rect.localScale.y;
            InvokeAccount("ShowAccountNotice", ServerFailure);
            var toast = (RectTransform)panel.parent.Find("AccountToast");
            float toastBottom = toast.anchoredPosition.y + toast.rect.yMin * toast.localScale.y;
            float toastTop = toast.anchoredPosition.y + toast.rect.yMax * toast.localScale.y;
            Assert.That(toastBottom, Is.GreaterThan(rect.sizeDelta.y * scale * .5f));
            Assert.That(toastTop, Is.LessThanOrEqualTo(available.y * .5f));
            float rollTop = rect.anchoredPosition.y + (roll.anchoredPosition.y + 31) * scale;
            var close = (RectTransform)account.Find("AccountClose");
            float closeBottom = rect.anchoredPosition.y + (close.anchoredPosition.y + close.rect.yMin) * scale;
            Assert.That(rollTop, Is.LessThan(available.y * .5f));
            Assert.That(closeBottom, Is.GreaterThan(-available.y * .5f));
        }
        // 자식 계정 선택/복구 두루마리도 가운데 계정 창과 정렬한다.
        foreach (string name in new[] { "AccountConflict", "SyncConflict", "AccountSyncPending" })
        {
            var overlayTop = account.Find(name + "/HanjiScrollArt/TopRoll");
            Assert.That(overlayTop.position.y, Is.EqualTo(roll.position.y).Within(.001));
            Assert.That(account.Find(name).position, Is.EqualTo(rect.position));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AccountDismissReturnsToSettingsWithoutClosingTheWholePopup(bool useCloseButton)
    {
        view.OpenTutorialForTests();
        typeof(LobbyOptionsView).GetMethod("ShowOptionsPage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, null);
        Canvas.ForceUpdateCanvases();
        var top = panel.Find("HanjiScrollArt/TopRoll");
        Vector3 settingsTop = top.position;
        page.Find("AccountButton").GetComponent<Button>().onClick.Invoke();
        Assert.That(view.IsAccountOpen, Is.True);
        Canvas.ForceUpdateCanvases();
        if (useCloseButton)
            panel.Find("AccountPage/AccountClose").GetComponent<Button>().onClick.Invoke();
        else
            host.transform.Find("LobbyOptionsCanvas/InkDim").GetComponent<EventTrigger>().OnPointerClick(
                new PointerEventData(null) { position = new Vector2(-100, -100), button = PointerEventData.InputButton.Left });
        Assert.That(view.IsOpen, Is.True);
        Assert.That(view.IsAccountOpen, Is.False);
        Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        Assert.That(top.position.y, Is.EqualTo(settingsTop.y).Within(.001));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdvertisingPrivacyButtonsAreNotCreatedEvenWhenSdkRequiresAnEntry(bool required)
    {
        var register = typeof(GoogleMobileAdsPrivacy).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic);
        var reset = typeof(GoogleMobileAdsPrivacy).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            register.Invoke(null, new object[] { (System.Action<System.Action<string>>)(_ => { }), required });
            Assert.That(page.Find("PrivacyOptionsLink"), Is.Null);
            Assert.That(panel.Find("PlaySettingsPage/AdConsentButton"), Is.Null);
            Assert.That(GoogleMobileAdsPrivacy.IsRequired, Is.EqualTo(required),
                "UI 제거가 광고 SDK의 동의 상태를 덮어쓰면 안 된다");
        }
        finally { reset.Invoke(null, null); }
    }

    [Test]
    public void QuickTogglesUseOneHanjiSheetWithoutGeometricBorder()
    {
        Assert.That(InkUiStyle.ActionButtonSprite, Is.Not.Null);
        string[] names = { "BgmToggle", "SfxToggle", "HapticsToggle" };
        for (int i = 0; i < names.Length; i++)
        {
            var root = page.Find(names[i]).GetComponent<Image>();
            var paper = root.transform.Find("Paper").GetComponent<Image>();
            var button = root.GetComponent<Button>();
            Assert.That(root.color.a, Is.Zero, names[i] + " must not draw a square");
            Assert.That(root.GetComponent<Outline>(), Is.Null);
            Assert.That(root.raycastTarget, Is.True);
            Assert.That(root.rectTransform.sizeDelta, Is.EqualTo(new Vector2(220, 260)));
            Assert.That(root.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(-240 + 240 * i, 298)));
            Assert.That(paper.sprite, Is.EqualTo(InkUiStyle.ActionButtonSprite));
            Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(paper.pixelsPerUnitMultiplier, Is.EqualTo(InkUiStyle.HanjiPixelsPerUnitMultiplier));
            Assert.That(paper.GetComponent<Outline>(), Is.Null);
            Assert.That(button.targetGraphic, Is.SameAs(paper));
            Assert.That(root.GetComponent<InkUiPressFeedback>(), Is.Not.Null);
            foreach (Graphic decoration in root.GetComponentsInChildren<Graphic>(true))
                if (decoration != root) Assert.That(decoration.raycastTarget, Is.False, decoration.name);
        }
    }

    [TestCase("BgmToggle")]
    [TestCase("SfxToggle")]
    [TestCase("HapticsToggle")]
    public void HanjiToggleStatesKeepLabelsReadableAndTheHitAreaInvisible(string name)
    {
        var button = page.Find(name).GetComponent<Button>();
        var state = button.transform.Find("Paper/State").GetComponent<Text>();
        var paper = button.transform.Find("Paper").GetComponent<Image>();
        for (int i = 0; i < 2; i++)
        {
            string expectedState = state.text == "켜짐" ? "꺼짐" : "켜짐";
            button.onClick.Invoke();
            bool enabled = state.text == "켜짐";
            Assert.That(state.text, Is.EqualTo(expectedState));
            Assert.That(state.color, Is.EqualTo(enabled ? InkPalette.TextDark : InkPalette.TextMuted));
            Assert.That(state.fontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(state.fontSize, Is.EqualTo(40));
            Assert.That(paper.color, Is.EqualTo(enabled ? InkUiStyle.HanjiPaperColor : Color.Lerp(InkUiStyle.HanjiPaperColor, InkPalette.Paper2, 0.5f)));
            Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(button.interactable, Is.True);
        }
    }

    [Test]
    public void SettingsButtonsUseHanjiExceptOfficialAppleAndQuietLegalLinks()
    {
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "TermsButton" || button.name == "PrivacyButton")
            {
                Assert.That(button.targetGraphic, Is.InstanceOf<Text>());
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
                Assert.That(button.transform.Find("Paper"), Is.Null);
                continue;
            }
            if (button.name == "UuidButton" || button.name == "PreviousButton" || button.name == "NextButton")
            {
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
                Assert.That(button.transform.Find("Paper"), Is.Null);
                continue;
            }
            var face = button.targetGraphic as Image;
            Assert.That(face, Is.Not.Null, button.name);
            if (button.name == "AppleLoginButton")
            {
                Assert.That(face.sprite, Is.Null, "공식 버튼은 한지로 바꾸지 않는다");
                continue;
            }
            Assert.That(face.sprite, Is.EqualTo(InkUiStyle.ActionButtonSprite), button.name);
            Assert.That(face.type, Is.EqualTo(Image.Type.Sliced), button.name);
            if (face.transform != button.transform)
            {
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero, button.name);
                Assert.That(face.GetComponent<Outline>(), Is.Null, button.name);
                Assert.That(face.raycastTarget, Is.False, button.name);
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.True, button.name);
            }
        }
    }

    [TestCase("TermsButton", -180, TextAnchor.MiddleLeft)]
    [TestCase("PrivacyButton", 180, TextAnchor.MiddleRight)]
    public void LegalFooterUsesSmallUnderlinedTextWithFullTouchArea(string name, float x, TextAnchor alignment)
    {
        var button = page.Find(name).GetComponent<Button>();
        var hit = button.GetComponent<Image>();
        var text = button.transform.Find("Label").GetComponent<Text>();
        var underline = button.transform.Find("Underline").GetComponent<Image>();
        Assert.That(hit.raycastTarget, Is.True);
        Assert.That(hit.color.a, Is.Zero);
        Assert.That(hit.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(x, -519)));
        Assert.That(hit.rectTransform.sizeDelta, Is.EqualTo(new Vector2(340, 120)));
        Assert.That(text.fontSize, Is.EqualTo(32));
        Assert.That(text.resizeTextForBestFit, Is.False);
        Assert.That(text.alignment, Is.EqualTo(alignment));
        Assert.That(text.raycastTarget, Is.False);
        Assert.That(underline.raycastTarget, Is.False);
        Assert.That(underline.rectTransform.rect.height, Is.EqualTo(2));
        var close = (RectTransform)page.Find("CloseButton");
        Assert.That(close.anchoredPosition.y + close.rect.height * 0.5f,
            Is.LessThan(hit.rectTransform.anchoredPosition.y - hit.rectTransform.rect.height * 0.5f));
    }

    [Test]
    public void ExternalCloseUsesSmallHanjiCrossWithFullHitAreaBelowTheRoll()
    {
        var button = page.Find("CloseButton").GetComponent<Button>();
        var hit = button.GetComponent<Image>();
        var paper = button.transform.Find("Paper").GetComponent<Image>();
        Assert.That(hit.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, -705)));
        Assert.That(hit.rectTransform.sizeDelta, Is.EqualTo(new Vector2(120, 120)));
        Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(new Vector2(96, 96)));
        Assert.That(hit.raycastTarget, Is.True);
        Assert.That(hit.color.a, Is.Zero);
        Assert.That(paper.raycastTarget, Is.False);
        Assert.That(button.targetGraphic, Is.SameAs(paper));
        Assert.That(paper.transform.Find("Label").GetComponent<Text>().text, Is.Empty);
        Assert.That(button.GetComponentInParent<CanvasGroup>(), Is.SameAs(page.GetComponent<CanvasGroup>()));
        var frame = panel.GetComponent<HanjiScrollFrame>();
        frame.SetPose(1, 0, false);
        var roll = (RectTransform)panel.Find("HanjiScrollArt/BottomRoll");
        Assert.That(hit.rectTransform.anchoredPosition.y + 60,
            Is.LessThan(roll.anchoredPosition.y - roll.rect.height * 0.5f - 24));
        for (int i = 0; i < 2; i++)
        {
            var stroke = paper.transform.Find("CrossStroke" + i).GetComponent<Image>();
            Assert.That(stroke.sprite, Is.SameAs(InkUiTextureFactory.CreateBrushSprite()));
            Assert.That(stroke.raycastTarget, Is.False);
            Assert.That(Mathf.DeltaAngle(0, stroke.rectTransform.localEulerAngles.z),
                Is.EqualTo(i == 0 ? 45 : -45).Within(0.01));
        }
    }

    [TestCase(1080, 1920, 0, 0, 0, 0)]
    [TestCase(1170, 2532, 0, 0, 102, 141)]
    [TestCase(1080, 2400, 0, 0, 72, 90)]
    [TestCase(750, 1334, 0, 0, 0, 40)]
    [TestCase(1536, 2048, 0, 0, 40, 48)]
    [TestCase(1080, 1600, 32, 16, 40, 64)]
    public void TutorialSharedGeometryKeepsRollAndFooterInsideSafeArea(
        int width, int height, int left, int right, int bottom, int top)
    {
        var safe = new Rect(left, bottom, width - left - right, height - bottom - top);
        float scale = LobbyOptionsView.CalculateSettingsTutorialScale(safe, width, height);
        Vector2 settings = LobbyOptionsView.CalculateSettingsTutorialPosition(false, scale);
        Vector2 tutorial = LobbyOptionsView.CalculateSettingsTutorialPosition(true, scale);
        Vector2 available = MobileUiLayout.GetLogicalSafeSize(safe, width, height);
        float settingsRoll = settings.y + 1160f * .5f * scale;
        float tutorialRoll = tutorial.y + 1300f * .5f * scale;
        Assert.That(tutorialRoll, Is.EqualTo(settingsRoll).Within(.001f));
        Assert.That(tutorial.x, Is.EqualTo(settings.x));
        Assert.That(scale, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
        Assert.That(820f * scale + 24f, Is.LessThanOrEqualTo(available.x + .001f));
        Assert.That(1640f * scale + 24f, Is.LessThanOrEqualTo(available.y + .001f));
        Assert.That(tutorialRoll + 31f * scale, Is.LessThan(available.y * .5f));
        Assert.That(tutorial.y - 681f * scale, Is.GreaterThan(-available.y * .5f));
    }

    [Test]
    public void TutorialArrowsStayInPlaceAndNeverFinishTheFirstRunTutorial()
    {
        view.OpenTutorialForTests();
        Transform tutorial = panel.Find("TutorialPage");
        var previous = tutorial.Find("PreviousButton").GetComponent<Button>();
        var next = tutorial.Find("NextButton").GetComponent<Button>();
        var top = (RectTransform)panel.Find("HanjiScrollArt/TopRoll");
        Vector3 topBefore = top.position;
        Assert.That(previous.transform.Find("Icon").localScale.x, Is.EqualTo(1));
        Assert.That(next.transform.Find("Icon").localScale.x, Is.EqualTo(-1));
        Assert.That(previous.interactable, Is.False);
        Assert.That(next.interactable, Is.True);
        previous.onClick.Invoke();
        Assert.That(view.CurrentTutorialPage, Is.Zero);
        for (int i = 1; i < GameplayTutorialCatalog.Count; i++) next.onClick.Invoke();
        Assert.That(next.interactable, Is.False);
        Assert.That(previous.interactable, Is.True);
        next.onClick.Invoke();
        Assert.That(view.CurrentTutorialPage, Is.EqualTo(GameplayTutorialCatalog.Count - 1));
        Assert.That(view.IsTutorialOpen, Is.True);
        Assert.That(LobbySettingsProfile.TutorialSeen, Is.False);
        previous.onClick.Invoke();
        Assert.That(view.CurrentTutorialPage, Is.EqualTo(GameplayTutorialCatalog.Count - 2));
        Assert.That(top.position, Is.EqualTo(topBefore));
        Assert.That(tutorial.Find("TutorialClose"), Is.Null);
    }

    [Test]
    public void SettingsAndTutorialShareScaleAndTopRollPosition()
    {
        var rect = (RectTransform)panel;
        float expectedScale = MobileUiLayout.CalculateFitScale(new Vector2(820, 1640),
            MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height, Vector2.one * 12);
        Assert.That(rect.localScale.x, Is.EqualTo(expectedScale).Within(0.001));
        Assert.That(rect.anchoredPosition.y, Is.EqualTo(210 * expectedScale).Within(0.001));
        var top = (RectTransform)panel.Find("HanjiScrollArt/TopRoll");
        Vector3 topBefore = top.position;
        view.OpenTutorialForTests();
        Assert.That(rect.anchoredPosition.y, Is.EqualTo(140 * expectedScale).Within(.001));
        Assert.That(rect.localScale.x, Is.EqualTo(expectedScale).Within(.001));
        Assert.That(top.position.y, Is.EqualTo(topBefore.y).Within(.001));
        Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        Assert.That(page.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(rect.sizeDelta.y, Is.EqualTo(FirstRunTutorialController.PanelDesignHeight));
        Assert.That(panel.Find("TutorialPage/TutorialClose"), Is.Null);
        var dim = host.transform.Find("LobbyOptionsCanvas/InkDim").GetComponent<EventTrigger>();
        Canvas.ForceUpdateCanvases();
        dim.OnPointerClick(new PointerEventData(null) { position = new Vector2(-100, -100),
            button = PointerEventData.InputButton.Left });
        Assert.That(rect.localScale.x, Is.EqualTo(expectedScale).Within(0.001));
        Assert.That(rect.anchoredPosition.y, Is.EqualTo(210 * expectedScale).Within(0.001));
        page.Find("CloseButton").GetComponent<Button>().onClick.Invoke();
        Assert.That(view.IsOpen, Is.False, "EditMode의 닫힘 완료도 기존 Close 경로를 따른다");
    }

    [TestCase("BgmCard")]
    [TestCase("SfxCard")]
    public void AudioRowsUseHanjiAndBrushFillWithoutChangingVolumeOrHitGeometry(string cardName)
    {
        Transform card = panel.Find("PlaySettingsPage/" + cardName);
        Assert.That(card.Find("Outline"), Is.Null);
        var paper = card.Find("Paper").GetComponent<Image>();
        Assert.That(paper.sprite, Is.EqualTo(InkUiStyle.ActionButtonSprite));
        Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(paper.raycastTarget, Is.False);
        var slider = paper.transform.Find("Slider").GetComponent<Slider>();
        var fill = slider.fillRect.GetComponent<Image>();
        var track = slider.transform.Find("Track").GetComponent<Image>();
        var hit = slider.transform.Find("HitArea").GetComponent<Image>();
        Assert.That(hit.color.a, Is.Zero);
        Assert.That(hit.raycastTarget, Is.True);
        Assert.That(hit.rectTransform.rect.height, Is.GreaterThanOrEqualTo(120));
        Assert.That(track.sprite, Is.EqualTo(InkUiTextureFactory.CreateBrushSprite()));
        Assert.That(fill.sprite, Is.SameAs(track.sprite));
        Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
        Assert.That(track.raycastTarget, Is.False);
        Assert.That(fill.raycastTarget, Is.False);
        foreach (float value in new[] { 0f, 0.35f, 1f })
        {
            slider.value = value;
            Canvas.ForceUpdateCanvases();
            Assert.That(fill.fillAmount, Is.EqualTo(value).Within(0.001));
            Assert.That(slider.fillRect.rect.width, Is.EqualTo(track.rectTransform.rect.width).Within(0.01),
                "먹 붓결은 폭을 줄여 찌그러뜨리지 않고 UV로 자른다");
            Assert.That(slider.handleRect.anchorMin.x, Is.EqualTo(value).Within(0.001));
            Assert.That(slider.handleRect.rect.size, Is.EqualTo(new Vector2(48, 48)));
            Assert.That(cardName == "BgmCard" ? LobbySettingsProfile.BgmVolume : LobbySettingsProfile.SfxVolume,
                Is.EqualTo(value).Within(0.001));
            Assert.That(paper.transform.Find("Value").GetComponent<Text>().text,
                Is.EqualTo($"{Mathf.RoundToInt(value * 100)}%"));
        }
    }

    [Test]
    public void QuickMuteRestoresLastVolumeAndDetailHandlesAreNotStretched()
    {
        LobbySettingsProfile.SetBgmVolume(0.35f);
        LobbySettingsProfile.SetSfxVolume(0.6f);
        foreach (string name in new[] { "BgmToggle", "SfxToggle" })
        {
            var button = page.Find(name).GetComponent<Button>();
            button.onClick.Invoke();
            Assert.That(button.transform.Find("Paper/State").GetComponent<Text>().text, Is.EqualTo("꺼짐"));
            button.onClick.Invoke();
        }
        Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(0.35f).Within(0.001));
        Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(0.6f).Within(0.001));
        Canvas.ForceUpdateCanvases();
        foreach (Slider slider in panel.GetComponentsInChildren<Slider>(true))
        {
            Assert.That(slider.handleRect.rect.height, Is.EqualTo(48f).Within(0.01));
            Assert.That(slider.handleRect.rect.width, Is.EqualTo(48f).Within(0.01));
            Assert.That(slider.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(120));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ReviewRetainsShortScrollPagesSeparateFromFirstRunSpotlight(int index)
    {
        typeof(LobbyOptionsView).GetMethod("ShowTutorialPage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, new object[] { index });
        Transform replay = panel.Find("TutorialPage");
        var page = GameplayTutorialCatalog.Get(index);
        Assert.That(replay.Find("TutorialTitle").GetComponent<Text>().text, Is.EqualTo(page.Title));
        Assert.That(replay.Find("TutorialDescription").GetComponent<Text>().text, Is.EqualTo(page.Description));
        Assert.That(replay.Find("Page").GetComponent<Text>().text, Is.EqualTo($"{index + 1} / 3"));
        Assert.That(replay.Find("TutorialIconPaper/TutorialIcon").GetComponent<Image>().sprite,
            Is.EqualTo(Resources.Load<Sprite>(page.SpriteResourcePath)));
        Assert.That(panel.GetComponent<HanjiScrollFrame>(), Is.Not.Null);
        Assert.That(((RectTransform)panel).sizeDelta.y, Is.EqualTo(FirstRunTutorialController.PanelDesignHeight));
        Assert.That(host.GetComponent<FirstRunTutorialController>(), Is.Null);
    }

    [Test]
    public void ScrollDecorationDoesNotInterceptButtonsAndTopRollStaysFixed()
    {
        var frame = panel.GetComponent<HanjiScrollFrame>();
        var top = (RectTransform)panel.Find("HanjiScrollArt/TopRoll");
        var bottom = (RectTransform)panel.Find("HanjiScrollArt/BottomRoll");
        frame.SetPose(0, 0, false);
        float topY = top.anchoredPosition.y;
        Assert.That(topY - bottom.anchoredPosition.y, Is.InRange(0f, 80f));
        frame.SetPose(1, 8, true);
        Assert.That(top.anchoredPosition.y, Is.EqualTo(topY));
        Assert.That(bottom.anchoredPosition.y, Is.EqualTo(-topY).Within(0.01f));
        foreach (Graphic graphic in panel.Find("HanjiScrollArt").GetComponentsInChildren<Graphic>())
            Assert.That(graphic.raycastTarget, Is.False);
    }

    [TestCase("PlaySettingsPage")]
    [TestCase("AccountPage")]
    public void DetailTextFitsItsAllocatedArea(string pageName)
    {
        Canvas.ForceUpdateCanvases();
        foreach (Text text in panel.Find(pageName).GetComponentsInChildren<Text>())
        {
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1),
                pageName + "/" + text.name + ": " + text.text);
        }
    }
}
