using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    public sealed partial class LobbyOptionsView
    {
        Text settingsNicknameText;
        Button settingsNicknameButton;
        bool HasLinkedNicknameAccount => !UsesTossSettings &&
            MukJumpAccountRuntime.Instance != null &&
            MukJumpAccountRuntime.Instance.AccountKind == MukJumpAccountKind.Apple &&
            MukJumpAccountRuntime.Instance.IsOnlineAuthenticated;
        CanvasGroup nicknameRoot;
        RectTransform nicknameSafeArea;
        RectTransform nicknamePaper;
        InputField nicknameInput;
        Text nicknameError;
        Text nicknameTitle;
        Text nicknameSaveLabel;
        Button nicknameSaveButton;
        Button nicknameCancelButton;
        bool nicknameSaving;
        bool firstRunNickname;
        bool nicknameClosing;
        long nicknamePopupGeneration;
        System.Action firstRunNicknameCompleted;
        string nicknameOwner;
        string promptedNicknameOwner;
        bool IsNicknameOpen => nicknameRoot != null && nicknameRoot.blocksRaycasts;

        /// 첫 안내의 정지 소유권은 튜토리얼이 유지하고, 저장 성공·닫힘 뒤에만 넘겨준다.
        public void OpenFirstRunNickname(System.Action completed)
        {
            if (!HasLinkedNicknameAccount) { completed?.Invoke(); return; }
            BuildNicknamePopup();
            if (IsNicknameOpen) CloseNicknameImmediate();
            firstRunNickname = true;
            firstRunNicknameCompleted = completed;
            OpenNicknamePopup(true);
        }

        public void CancelFirstRunNickname()
        {
            if (firstRunNickname) CloseNicknameImmediate();
        }

        void BuildNicknamePopup()
        {
            if (nicknameRoot != null) return;
            var root = new GameObject("NicknameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = CanvasSortingOrder + 20;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<CanvasScaler>());
            nicknameRoot = root.GetComponent<CanvasGroup>();
            var dim = CreateStretchImage("InkDim", root.transform, InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);
            var trigger = dim.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                if (data is not PointerEventData pointer || pointer.button != PointerEventData.InputButton.Left || pointer.dragging) return;
                if (!RectTransformUtility.RectangleContainsScreenPoint(nicknamePaper, pointer.position, pointer.pressEventCamera))
                    CloseNicknamePopup();
            });
            trigger.triggers.Add(click);
            nicknameSafeArea = CreateStretchRect("SafeAreaRoot", root.transform);
            nicknamePaper = CreateRect("NicknameScroll", nicknameSafeArea, Vector2.zero, new Vector2(780, 750));
            HanjiScrollFrame.Attach(nicknamePaper, new Vector2(748, 710));
            nicknameTitle = CreateReadableText("Title", nicknamePaper, "닉네임 변경", 64,
                new Vector2(0, 255), new Vector2(670, 86), InkPalette.TextDark, strong: true);
            CreateReadableText("Hint", nicknamePaper, "2~10자 · 한글, 영문, 숫자, _, -", 30,
                new Vector2(0, 166), new Vector2(670, 56), InkPalette.TextMuted);
            var fieldImage = CreateImage("NicknameInput", nicknamePaper, null, new Vector2(0, 68), new Vector2(636, 124), Color.white);
            InkUiStyle.ConfigureHanjiSurface(fieldImage);
            fieldImage.raycastTarget = true;
            var inputText = CreateReadableText("Text", fieldImage.transform, string.Empty, 44,
                Vector2.zero, new Vector2(556, 74), InkPalette.TextDark, TextAnchor.MiddleLeft, wrap: false);
            inputText.supportRichText = false;
            InkLocalizedText.Exclude(inputText);
            var placeholder = CreateReadableText("Placeholder", fieldImage.transform, "닉네임을 입력해 주세요", 38,
                Vector2.zero, new Vector2(556, 74), InkPalette.TextMuted, TextAnchor.MiddleLeft, wrap: false);
            nicknameInput = fieldImage.gameObject.AddComponent<InputField>();
            nicknameInput.textComponent = inputText;
            nicknameInput.placeholder = placeholder;
            nicknameInput.targetGraphic = fieldImage;
            nicknameInput.lineType = InputField.LineType.SingleLine;
            nicknameInput.contentType = InputField.ContentType.Standard;
            nicknameInput.characterLimit = MukJumpIdentityProfile.MaxNicknameLength;
            nicknameInput.shouldHideMobileInput = true;
            nicknameInput.selectionColor = WithAlpha(InkPalette.Red, .2f);
            nicknameInput.onValueChanged.AddListener(_ => InkLocalizedText.SetSource(nicknameError, string.Empty));
            nicknameError = CreateReadableText("Error", nicknamePaper, string.Empty, 30,
                new Vector2(0, -45), new Vector2(660, 74), InkPalette.Red);
            CreateReadableText("ChangeIntervalHint", nicknamePaper, "닉네임은 2주에 한 번 변경할 수 있어요", 27,
                new Vector2(0, -112), new Vector2(670, 42), InkPalette.TextMuted);
            nicknameCancelButton = CreatePaperButton("CancelButton", nicknamePaper, "취소",
                new Vector2(-165, -217), new Vector2(300, 124), 44);
            nicknameCancelButton.onClick.AddListener(CloseNicknamePopup);
            nicknameSaveButton = CreatePaperButton("SaveButton", nicknamePaper, "저장",
                new Vector2(165, -217), new Vector2(300, 124), 44);
            nicknameSaveLabel = nicknameSaveButton.transform.Find("Paper/Label").GetComponent<Text>();
            nicknameSaveButton.onClick.AddListener(SubmitNickname);
            CloseNicknameImmediate();
        }

        void OpenNicknamePopup(bool firstAppleLogin)
        {
            if (!HasLinkedNicknameAccount || IsNicknameOpen) return;
            BuildNicknamePopup();
            var account = MukJumpAccountRuntime.Instance;
            nicknameOwner = account?.PlayerId ?? MukJumpIdentityProfile.LocalUid;
            nicknameSaving = false;
            nicknameClosing = false;
            nicknamePopupGeneration++;
            InkLocalizedText.SetSource(nicknameTitle, firstAppleLogin ? "닉네임 설정" : "닉네임 변경");
            string current = account?.Nickname ?? MukJumpIdentityProfile.GuestNickname;
            nicknameInput.SetTextWithoutNotify(MukJumpIdentityProfile.IsGeneratedNickname(current) ? string.Empty : current);
            InkLocalizedText.SetSource(nicknameError, string.Empty);
            nicknameRoot.gameObject.SetActive(true);
            nicknameRoot.alpha = 1;
            nicknameRoot.interactable = nicknameRoot.blocksRaycasts = true;
            nicknameCancelButton.gameObject.SetActive(!firstRunNickname);
            ((RectTransform)nicknameSaveButton.transform).anchoredPosition =
                new Vector2(firstRunNickname ? 0 : 165, -217);
            InkLocalizedText.SetSource(nicknameSaveLabel, firstRunNickname ? "시작하기" : "저장");
            nicknamePaper.GetComponent<HanjiScrollFrame>().ResetPresentation();
            AppleSignInButtonBridge.Hide();
            PointerInput.SuppressUntilRelease();
            LayoutNicknamePopup();
        }

        void UpdateNicknameUi()
        {
            if (UsesTossSettings) return;
            if (settingsNicknameButton != null)
                settingsNicknameButton.gameObject.SetActive(HasLinkedNicknameAccount);
            if (!HasLinkedNicknameAccount && IsNicknameOpen)
            {
                // 로그아웃·게스트 전환 시 키보드와 지연 저장 콜백도 함께 정리한다.
                var completed = firstRunNicknameCompleted;
                CloseNicknameImmediate();
                completed?.Invoke();
                return;
            }
            var runtime = MukJumpAccountRuntime.Instance;
            if (IsNicknameOpen)
            {
                bool tutorialPause = firstRunNickname && manager != null &&
                    manager.State == GameState.Playing && manager.PauseReason == GameplayPauseReason.FirstRunTutorial;
                if (manager != null && manager.State != GameState.Lobby && !tutorialPause)
                { CloseNicknameImmediate(); return; }
                string currentOwner = runtime?.PlayerId ?? MukJumpIdentityProfile.LocalUid;
                if (currentOwner != nicknameOwner)
                {
                    if (!firstRunNickname) { CloseNicknameImmediate(); return; }
                    // 첫 게스트 인증이 뒤늦게 끝나도 입력 중인 이름은 유지한다.
                    nicknameOwner = currentOwner;
                    nicknameSaving = nicknameClosing = false;
                    nicknamePopupGeneration++;
                    nicknameRoot.interactable = true;
                    nicknamePaper.GetComponent<HanjiScrollFrame>().CancelClose();
                }
                if (nicknameClosing) { LayoutNicknamePopup(); return; }
                bool busy = nicknameSaving || (runtime != null && runtime.IsNicknameBusy);
                nicknameInput.interactable = !nicknameSaving;
                nicknameSaveButton.interactable = !busy && (runtime == null || runtime.CanChangeNickname);
                nicknameCancelButton.interactable = !nicknameSaving;
                InkLocalizedText.SetSource(nicknameSaveLabel, nicknameSaving ? "저장 중" : firstRunNickname ? "시작하기" : "저장");
                LayoutNicknamePopup();
                AppleSignInButtonBridge.Hide();
                return;
            }
            if (runtime == null || !runtime.IsOnlineAuthenticated)
                promptedNicknameOwner = null;
            if (runtime != null && runtime.NeedsNicknameSetup && !runtime.IsNicknameBusy &&
                runtime.PlayerId != promptedNicknameOwner && manager != null && manager.State == GameState.Lobby &&
                !manager.IsTransitioning && !StartupBrandSplash.IsBlockingInput &&
                !LobbySettingsProfile.NeedsGameplayTutorial &&
                (FirstRunTutorialController.Instance == null || !FirstRunTutorialController.Instance.IsActive))
            {
                promptedNicknameOwner = runtime.PlayerId;
                OpenNicknamePopup(true);
            }
        }

        void SubmitNickname()
        {
            if (!HasLinkedNicknameAccount || nicknameSaving || nicknameClosing || !IsNicknameOpen) return;
            if (!MukJumpIdentityProfile.TryNormalizeNickname(nicknameInput.text, out string value, out string error))
            { InkLocalizedText.SetSource(nicknameError, error); return; }
            nicknameSaving = true;
            nicknameInput.DeactivateInputField();
            var runtime = MukJumpAccountRuntime.Instance;
            string owner = nicknameOwner;
            long generation = nicknamePopupGeneration;
            void Complete(bool success, string message)
            {
                if (this == null || !IsNicknameOpen || nicknameOwner != owner || generation != nicknamePopupGeneration) return;
                nicknameSaving = false;
                if (!success) { InkLocalizedText.SetSource(nicknameError, message); return; }
                RefreshSettingsUuid();
                CloseNicknameAfterSave();
            }
            if (runtime != null) runtime.ChangeNickname(value, Complete);
            else
            {
                try { MukJumpIdentityProfile.SaveNickname(MukJumpIdentityProfile.LocalScope, value); Complete(true, string.Empty); }
                catch (System.Exception) { Complete(false, "닉네임을 저장하지 못했어요. 다시 시도해 주세요"); }
            }
        }

        void CloseNicknamePopup()
        {
            if (!IsNicknameOpen || nicknameSaving || nicknameClosing || firstRunNickname) return;
            BeginNicknameClose(false);
        }

        void CloseNicknameAfterSave() => BeginNicknameClose(true);

        void BeginNicknameClose(bool saved)
        {
            if (nicknameClosing) return;
            nicknameClosing = true;
            long generation = nicknamePopupGeneration;
            var completed = saved && firstRunNickname ? firstRunNicknameCompleted : null;
            nicknameInput.DeactivateInputField();
            nicknameRoot.interactable = false;
            nicknamePaper.GetComponent<HanjiScrollFrame>().Close(() =>
            {
                if (this == null || generation != nicknamePopupGeneration) return;
                CloseNicknameImmediate();
                completed?.Invoke();
            }, nicknameRoot);
            PointerInput.SuppressUntilRelease();
        }

        void CloseNicknameImmediate()
        {
            nicknameSaving = false;
            nicknameClosing = firstRunNickname = false;
            firstRunNicknameCompleted = null;
            nicknamePopupGeneration++;
            if (nicknameRoot == null) return;
            nicknameInput?.DeactivateInputField();
            nicknameRoot.alpha = 0;
            nicknameRoot.interactable = nicknameRoot.blocksRaycasts = false;
            nicknameRoot.gameObject.SetActive(false);
        }

        void LayoutNicknamePopup()
        {
            ApplyNicknameKeyboardLayout(TouchScreenKeyboard.area, TouchScreenKeyboard.visible);
        }

        void ApplyNicknameKeyboardLayout(Rect keyboardArea, bool keyboardVisible)
        {
            if (nicknameSafeArea == null) return;
            Rect safe = UiSafeArea;
            // Unity 6의 iOS/Android 키보드는 좌상단, Safe Area는 좌하단 기준이다.
            // yMax를 그대로 쓰면 하단 키보드의 끝(화면 높이)까지 영역을 잘라 팝업이 점으로 줄어든다.
            if (keyboardVisible && keyboardArea.width > 0 && keyboardArea.height > 0)
            {
                float keyboardTop = UiScreenHeight - keyboardArea.yMin;
                // 열림·닫힘 중 0/화면 밖 좌표는 무시해 정상 팝업 크기와 입력을 유지한다.
                if (keyboardTop > safe.yMin && keyboardTop < safe.yMax)
                    safe.yMin = keyboardTop;
            }
            MobileUiLayout.ApplySafeArea(nicknameSafeArea, safe, UiScreenWidth, UiScreenHeight);
            nicknamePaper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                nicknamePaper.sizeDelta, safe, UiScreenWidth, UiScreenHeight, Vector2.one * 24);
        }
    }
}
