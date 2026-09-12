#if UNITY_EDITOR || (UNITY_IOS && MUKJUMP_SKIN_DEBUG)
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MukJump.Core
{
    /// 심사 제출 불가인 내부 TestFlight에만 생성되는 로비 시험 도구.
    public sealed class InkSkinDebugView : MonoBehaviour
    {
        CanvasGroup group;
        RectTransform safeRoot, panel;
        GameObject toggle, blocker;
        UnityEngine.UI.Text unlockLabel, note;
        GameManager manager;
        LobbyOptionsView options;
        BrushTransitionView transition;
        bool open;
        public bool IsOpen => open;
        public bool CanOpen => Ready();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager != null && manager.GetComponent<InkSkinDebugView>() == null)
                manager.gameObject.AddComponent<InkSkinDebugView>();
        }
        void Start()
        {
            manager = GetComponent<GameManager>(); options = GetComponent<LobbyOptionsView>();
            transition = GetComponent<BrushTransitionView>(); Build();
        }
        bool Ready() => SkinDebugSession.Available && manager != null && manager.State == GameState.Lobby &&
            !manager.IsTransitioning && !manager.IsPaused && !StartupBrandSplash.IsBlockingInput &&
            MobileApplicationLifecycle.IsApplicationActive && !PermanentGrowthProfile.RequiresRecovery &&
            (options == null || !options.IsOpen) &&
            (transition == null || !transition.IsPlaying) &&
            LobbyScreenNavigator.Instance != null && !LobbyScreenNavigator.Instance.IsTransitioning &&
            LobbyScreenNavigator.Instance.CurrentSection == LobbyScreenNavigator.LobbySection.Lobby &&
            (MukJumpAccountRuntime.Instance == null || !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync);
        void Update()
        {
            if (group == null) return;
            bool ready = Ready();
            if (open && (!ready || !SkinDebugSession.MenuOpen)) Close();
            group.alpha = ready ? 1 : 0; group.interactable = group.blocksRaycasts = ready;
            var safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(safeRoot, safe, Screen.width, Screen.height);
            panel.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(new Vector2(820, 900), safe,
                Screen.width, Screen.height, Vector2.one * 28);
            InkLocalizedText.SetSource(unlockLabel, SkinDebugSession.Active ? "전체 의상 해금 ON" : "전체 의상 해금 OFF");
            InkLocalizedText.SetSource(note, SkinDebugSession.Active ? "시험 착용 중 · 플레이 기록 저장 안 됨" : "실제 기록은 바뀌지 않아요");
        }
        public void Toggle()
        {
            if (!Ready()) return;
            open = !open; SkinDebugSession.SetMenuOpen(open);
            blocker.SetActive(open); panel.gameObject.SetActive(open); toggle.SetActive(!open);
            PointerInput.SuppressUntilRelease();
        }
        public void Close()
        {
            open = false; SkinDebugSession.SetMenuOpen(false);
            if (panel != null) panel.gameObject.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
            if (toggle != null) toggle.SetActive(true);
            PointerInput.SuppressUntilRelease();
        }
        void OnDisable()
        {
            Close();
            if (group != null) { group.alpha = 0; group.interactable = group.blocksRaycasts = false; }
        }
        public void ToggleAll()
        {
            if (Ready() && open) SkinDebugSession.SetAllUnlocked(!SkinDebugSession.Active);
        }
        public void Preview(bool hidden)
        {
            if (!Ready() || !open) return;
            ulong mask = hidden ? (1UL << (int)InkSkinId.NightBat) | (1UL << (int)InkSkinId.OrigamiWings)
                : (1UL << (int)InkSkinId.Gat) | (1UL << (int)InkSkinId.RedHeadband);
            var owner = PermanentGrowthProfile.SkinAccountToken;
            Close();
            transition.TryPlay(() =>
            {
                SkinUnlockSceneController.PreparePreview(mask, owner);
                BrushTransitionView.RequestRevealAfterSceneLoad();
                SceneManager.LoadScene(SkinUnlockSceneController.SceneName);
            });
        }
        void Build()
        {
            var canvasObject = new GameObject("SkinDebugCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4500;
            MobileUiLayout.ConfigurePortraitScaler(canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>());
            group = canvasObject.GetComponent<CanvasGroup>();
            var dim = Stretch("InputBlocker", canvasObject.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = InkUiStyle.PopupDimColor; blocker = dim.gameObject;
            safeRoot = Stretch("SafeAreaRoot", canvasObject.transform);
            toggle = Button("DebugToggle", safeRoot, "DEBUG", new Vector2(110, 72), new Vector2(184, 112), Toggle).gameObject;
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = Vector2.zero;
            toggleRect.anchoredPosition = new Vector2(24, 28);
            panel = Rect("TestPanel", safeRoot, Vector2.zero, new Vector2(760, 820));
            var paper = panel.gameObject.AddComponent<UnityEngine.UI.Image>(); paper.sprite = InkUiStyle.ActionButtonSprite;
            InkUiStyle.ConfigureHanjiSurface(paper); paper.raycastTarget = true;
            Label("Title", panel, "의상 시험실", 58, new Vector2(0, 318), new Vector2(690, 100));
            Label("Build", panel, "TESTFLIGHT · DEBUG", 28, new Vector2(0, 247), new Vector2(680, 46));
            Button("UnlockEffect", panel, "해금 연출 보기", new Vector2(0, 135), new Vector2(640, 112), () => Preview(false));
            Button("HiddenEffect", panel, "히든 해금 연출", new Vector2(0, 3), new Vector2(640, 112), () => Preview(true));
            var all = Button("UnlockAll", panel, "", new Vector2(0, -129), new Vector2(640, 112), ToggleAll);
            unlockLabel = all.GetComponentInChildren<UnityEngine.UI.Text>();
            note = Label("Notice", panel, "", 30, new Vector2(0, -232), new Vector2(690, 68)); note.color = InkPalette.Red;
            Button("Close", panel, "닫기", new Vector2(0, -325), new Vector2(360, 104), Close);
            Close(); group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        static RectTransform Stretch(string name, Transform parent)
        { var r = Rect(name, parent, Vector2.zero, Vector2.zero); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r; }
        static UnityEngine.UI.Text Label(string name, Transform parent, string value, int fontSize, Vector2 position, Vector2 size)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Text>();
            InkUiStyle.ApplyReadableText(text, fontSize, TextAnchor.MiddleCenter, true, true);
            InkLocalizedText.SetSource(text, value); text.color = InkPalette.TextDark; text.raycastTarget = false; return text;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, string value, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = InkUiStyle.ActionButtonSprite; InkUiStyle.ConfigureHanjiSurface(image);
            var b = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); InkUiStyle.ConfigureButton(b, image);
            b.colors = InkUiStyle.ActionButtonColors(); Label("Label", rect, value, 42, Vector2.zero, size - new Vector2(24, 12));
            b.onClick.AddListener(action); return b;
        }
    }
}
#endif
