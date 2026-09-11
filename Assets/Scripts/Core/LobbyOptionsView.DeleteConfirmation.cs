using UnityEngine;
using UnityEngine.EventSystems;

namespace MukJump.Core
{
    public sealed partial class LobbyOptionsView
    {
        CanvasGroup deleteConfirmationRoot;
        RectTransform deleteConfirmationSafeArea, deleteConfirmationPaper;
        UnityEngine.UI.Button deleteConfirmationButton;
        MukJumpAccountRuntime deleteConfirmationAccount;
        MukJumpAccountKind deleteConfirmationKind;
        string deleteConfirmationOwner;
        bool deleteConfirmationUnderlyingInteraction;
        bool IsDeleteConfirmationOpen => deleteConfirmationRoot != null && deleteConfirmationRoot.gameObject.activeSelf;
#if UNITY_EDITOR
        System.Action deleteConfirmationDispatchForTests;
#endif

        void BuildDeleteConfirmation()
        {
            if (deleteConfirmationRoot != null) return;
            var root = new GameObject("DeleteConfirmationCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder + 30;
            MobileUiLayout.ConfigurePortraitScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
            deleteConfirmationRoot = root.GetComponent<CanvasGroup>();
            var dim = CreateStretchImage("InkDim", root.transform, InkUiStyle.PopupDimColor);
            InkUiStyle.ConfigurePopupDim(dim);
            dim.raycastTarget = true;
            var trigger = dim.gameObject.AddComponent<EventTrigger>();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left && !pointer.dragging &&
                    !RectTransformUtility.RectangleContainsScreenPoint(deleteConfirmationPaper, pointer.position, pointer.pressEventCamera))
                    CancelDeleteConfirmation();
            });
            trigger.triggers.Add(click);
            deleteConfirmationSafeArea = CreateStretchRect("SafeAreaRoot", root.transform);
            deleteConfirmationPaper = CreateRect("DeleteConfirmationScroll", deleteConfirmationSafeArea, Vector2.zero, new Vector2(780, 720));
            HanjiScrollFrame.Attach(deleteConfirmationPaper, new Vector2(748, 680));
            CreateReadableText("Title", deleteConfirmationPaper, "계정 삭제", 60,
                new Vector2(0, 220), new Vector2(660, 100), InkPalette.Red, strong: true);
            CreateReadableText("Warning", deleteConfirmationPaper, "계정과 서버 기록이 영구 삭제됩니다", 38,
                new Vector2(0, 55), new Vector2(640, 150), InkPalette.TextDark);
            var cancel = CreatePaperButton("CancelButton", deleteConfirmationPaper, "취소",
                new Vector2(-165, -180), new Vector2(300, 124), 38);
            cancel.onClick.AddListener(CancelDeleteConfirmation);
            deleteConfirmationButton = CreatePaperButton("ConfirmButton", deleteConfirmationPaper, "계정 삭제",
                new Vector2(165, -180), new Vector2(300, 124), 38);
            deleteConfirmationButton.transform.Find("Paper/Label").GetComponent<UnityEngine.UI.Text>().color = InkPalette.Red;
            deleteConfirmationButton.onClick.AddListener(ConfirmAccountDeletion);
            root.SetActive(false);
        }

        void ShowDeleteConfirmation()
        {
            deleteConfirmationAccount = MukJumpAccountRuntime.Instance;
            // 표시용 UID 캐시가 아니라 현재 인증의 UserInDate로 소유자를 고정한다.
            deleteConfirmationOwner = deleteConfirmationAccount?.SupportCode;
            deleteConfirmationKind = deleteConfirmationAccount != null ? deleteConfirmationAccount.AccountKind : default;
            ClearAccountToast();
            // 포인터뿐 아니라 키보드/패드 탐색도 뒤쪽 계정 버튼에 도달하지 않게 한다.
            deleteConfirmationUnderlyingInteraction = rootGroup != null && rootGroup.interactable;
            if (rootGroup != null) rootGroup.interactable = false;
            deleteConfirmationRoot.gameObject.SetActive(true);
            deleteConfirmationRoot.alpha = 1;
            deleteConfirmationRoot.interactable = deleteConfirmationRoot.blocksRaycasts = true;
            deleteConfirmationButton.interactable = false;
            deleteConfirmationPaper.GetComponent<HanjiScrollFrame>().ResetPresentation();
            LayoutDeleteConfirmation();
            AppleSignInButtonBridge.Hide();
            PointerInput.SuppressUntilRelease();
        }

        bool IsDeleteConfirmationAccountCurrent() => deleteConfirmationAccount != null &&
            deleteConfirmationAccount == MukJumpAccountRuntime.Instance && deleteConfirmationAccount.IsOnlineAuthenticated &&
            !string.IsNullOrEmpty(deleteConfirmationOwner) && deleteConfirmationAccount.SupportCode == deleteConfirmationOwner &&
            deleteConfirmationAccount.AccountKind == deleteConfirmationKind &&
            !deleteConfirmationAccount.BlocksGameplayForAccountSync && !deleteConfirmationAccount.IsTemporaryBackendPaused &&
            deleteConfirmationAccount.Phase != MukJumpAccountPhase.Connecting && deleteConfirmationAccount.Phase != MukJumpAccountPhase.Deleting;

        void UpdateDeleteConfirmation()
        {
            if (!IsDeleteConfirmationOpen) return;
            LayoutDeleteConfirmation();
            AppleSignInButtonBridge.Hide();
            if (!deleteConfirmationArmed) return;
            if (!IsDeleteConfirmationAccountCurrent()) { DisarmDeleteConfirmation(); return; }
            deleteConfirmationButton.interactable = IsDeleteConfirmationReady(deleteConfirmationArmedAt, Time.unscaledTime);
        }

        void LayoutDeleteConfirmation()
        {
            if (UiScreenWidth <= 0 || UiScreenHeight <= 0) return;
            MobileUiLayout.ApplySafeArea(deleteConfirmationSafeArea, UiSafeArea, UiScreenWidth, UiScreenHeight);
            deleteConfirmationPaper.localScale = Vector3.one * MobileUiLayout.CalculateFitScale(
                deleteConfirmationPaper.sizeDelta, UiSafeArea, UiScreenWidth, UiScreenHeight, Vector2.one * 24);
        }

        void CancelDeleteConfirmation()
        {
            if (!IsDeleteConfirmationOpen || !deleteConfirmationRoot.interactable) return;
            deleteConfirmationArmed = false;
            deleteConfirmationRoot.interactable = false;
            deleteConfirmationPaper.GetComponent<HanjiScrollFrame>().Close(DisarmDeleteConfirmation, deleteConfirmationRoot);
            PointerInput.SuppressUntilRelease();
        }

        void HideDeleteConfirmation()
        {
            deleteConfirmationAccount = null;
            deleteConfirmationOwner = null;
            if (deleteConfirmationRoot == null) return;
            if (IsDeleteConfirmationOpen && rootGroup != null)
                rootGroup.interactable = deleteConfirmationUnderlyingInteraction;
            deleteConfirmationPaper.GetComponent<HanjiScrollFrame>().CancelClose();
            deleteConfirmationRoot.interactable = deleteConfirmationRoot.blocksRaycasts = false;
            deleteConfirmationRoot.gameObject.SetActive(false);
        }

        void ConfirmAccountDeletion()
        {
            if (!deleteConfirmationArmed || !IsDeleteConfirmationOpen || !deleteConfirmationRoot.interactable ||
                !IsDeleteConfirmationReady(deleteConfirmationArmedAt, Time.unscaledTime)) return;
            if (!IsDeleteConfirmationAccountCurrent()) { DisarmDeleteConfirmation(); return; }
            var account = deleteConfirmationAccount;
            DisarmDeleteConfirmation();
            PointerInput.SuppressUntilRelease();
#if UNITY_EDITOR
            if (deleteConfirmationDispatchForTests != null) { deleteConfirmationDispatchForTests(); return; }
#endif
            MukJumpAnalytics.Account(AnalyticsAccountAction.Delete, AnalyticsOutcome.Requested);
            account.DeleteAccountConfirmed();
        }
    }
}
