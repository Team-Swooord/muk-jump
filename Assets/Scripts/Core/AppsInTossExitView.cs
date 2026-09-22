using System;
using System.Threading.Tasks;
using AppsInToss;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 토스 시스템 뒤로가기를 게임의 확인·저장·종료 흐름에 연결한다.
    [DisallowMultipleComponent]
    public sealed class AppsInTossExitView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static AppsInTossExitView owner;
        Action unsubscribeBack;
        Action unsubscribeHome;
        Canvas canvas;
        RectTransform safeRoot;
        RectTransform panel;
        Text message;
        Button confirm;
        Button cancel;
        GameManager pausedManager;
        bool closing;
        int generation;
#if UNITY_EDITOR
        Func<Task> closeViewForTests;
#endif

        void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            unsubscribeBack = AIT.GraniteEventSubscribeBackEvent(HandleBack,
                _ => Debug.LogWarning("먹점프 시스템 뒤로가기 연결을 확인해 주세요"));
            unsubscribeHome = AIT.GraniteEventSubscribeHomeEvent(RequestExit,
                _ => Debug.LogWarning("먹점프 홈 이동 연결을 확인해 주세요"));
#endif
        }

        void OnDisable()
        {
            generation++;
            unsubscribeBack?.Invoke();
            unsubscribeHome?.Invoke();
            unsubscribeBack = unsubscribeHome = null;
            Dismiss();
        }

        public void HandleBack()
        {
            if (closing || MonetizationAds.HasPendingPresentation) return;
            if (owner == this && IsOpen) { Cancel(); return; }
            var options = FindAnyObjectByType<LobbyOptionsView>();
            if (options != null && options.IsOpen &&
                (MukJumpAccountRuntime.Instance == null ||
                 !MukJumpAccountRuntime.Instance.BlocksGameplayForAccountSync))
            { options.Close(); return; }
            var navigator = LobbyScreenNavigator.Instance;
            if (navigator != null && !navigator.IsTransitioning &&
                navigator.CurrentSection != LobbyScreenNavigator.LobbySection.Lobby)
            { navigator.ReturnToLobby(); return; }
            RequestExit();
        }

        public void RequestExit()
        {
            if (IsOpen || closing || MonetizationAds.HasPendingPresentation) return;
            Build();
            owner = this;
            IsOpen = true;
            GameManager manager = GameManager.Instance;
            // 원래 열려 있던 일시정지/튜토리얼은 취소 시 임의로 풀지 않는다.
            pausedManager = manager != null && !manager.IsPaused && manager.PauseGame()
                ? manager : null;
            message.text = "저장된 기록은 유지돼요\n진행 중인 판은 종료돼요";
            canvas.gameObject.SetActive(true);
            ApplyLayout();
        }

        public void Cancel()
        {
            if (closing || owner != this) return;
            Dismiss();
        }

        public async void Confirm()
        {
            if (owner != this || !IsOpen || closing) return;
            closing = true;
            confirm.interactable = cancel.interactable = false;
            int request = ++generation;
            try
            {
                LobbySettingsProfile.TryFlush();
                PlayerPrefs.Save();
                await ClosePlatformView();
                if (request == generation && this != null) Dismiss();
            }
            catch (Exception)
            {
                if (request != generation || this == null) return;
                closing = false;
                message.text = "종료하지 못했어요\n다시 시도하거나 취소해 주세요";
                confirm.interactable = cancel.interactable = true;
            }
        }

        async Task ClosePlatformView()
        {
#if UNITY_EDITOR
            if (closeViewForTests != null) { await closeViewForTests(); return; }
#endif
            await AIT.CloseView(timeoutMs: 10000);
        }

        void Dismiss()
        {
            if (owner != this) return;
            owner = null;
            IsOpen = false;
            closing = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (pausedManager != null && GameManager.Instance == pausedManager)
                pausedManager.ResumeGame();
            pausedManager = null;
            if (confirm != null) confirm.interactable = cancel.interactable = true;
        }

        void Update()
        {
            if (owner == this && IsOpen) ApplyLayout();
        }

        void ApplyLayout()
        {
            Rect safe = MobileUiLayout.CurrentSafeArea;
            MobileUiLayout.ApplySafeArea(safeRoot, safe, Screen.width, Screen.height);
            panel.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                new Vector2(760, 630), safe, Screen.width, Screen.height, new Vector2(32f, 32f));
        }

        void Build()
        {
            if (canvas != null) return;
            var root = new GameObject("TossExitConfirmation", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            RectTransform dim = Rect("Dim", root.transform, Vector2.zero, Vector2.zero);
            Stretch(dim);
            InkUiStyle.ConfigurePopupDim(dim.gameObject.AddComponent<Image>());
            safeRoot = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero);
            Stretch(safeRoot);
            panel = Rect("ExitScroll", safeRoot, Vector2.zero, new Vector2(700, 570));
            HanjiScrollFrame.Attach(panel, new Vector2(700, 570));
            Label("Title", "먹점프를 종료할까요?", panel, new Vector2(0, 165), new Vector2(620, 100), 58);
            message = Label("Message", "", panel, new Vector2(0, 15), new Vector2(610, 150), 42);
            cancel = MakeButton("Cancel", "취소", -150, ActionButtonRole.Secondary, Cancel);
            confirm = MakeButton("Confirm", "종료", 150, ActionButtonRole.Primary, Confirm);
            root.SetActive(false);
        }

        Button MakeButton(string name, string text, float x, ActionButtonRole role, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, panel, new Vector2(x, -165), new Vector2(270, 120));
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            Text label = Label("Label", text, rect, Vector2.zero, new Vector2(240, 95), 50);
            InkUiStyle.ConfigureActionButton(button, image, label, role);
            button.onClick.AddListener(action);
            return button;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Text Label(string name, string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.text = value;
            label.font = InkPalette.UiFont;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = InkPalette.Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }
    }
}
