using System;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class DeleteConfirmationPopupTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        LobbyOptionsView view;
        MukJumpAccountRuntime account;
        int deletions;
        string uid;
        Transform Popup => host.transform.Find("DeleteConfirmationCanvas");
        Transform Paper => Popup.Find("SafeAreaRoot/DeleteConfirmationScroll");
        void Call(string method) => typeof(LobbyOptionsView).GetMethod(method, Private).Invoke(view, null);
        void Ready() => typeof(LobbyOptionsView).GetField("deleteConfirmationArmedAt", Private).SetValue(view, Time.unscaledTime - 1);

        [SetUp] public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
            host = new GameObject("DeleteConfirmationTests");
            account = host.AddComponent<MukJumpAccountRuntime>();
            typeof(MukJumpAccountRuntime).GetMethod("OnEnable", Private).Invoke(account, null);
            typeof(MukJumpAccountRuntime).GetProperty("IsOnlineAuthenticated").SetValue(account, true);
            typeof(MukJumpAccountRuntime).GetProperty("AccountKind").SetValue(account, MukJumpAccountKind.BackendGuest);
            typeof(MukJumpAccountRuntime).GetProperty("Phase").SetValue(account, MukJumpAccountPhase.OnlineReady);
            uid = "123456";
            typeof(MukJumpAccountRuntime).GetField("backendUidForTests", Private).SetValue(account, new Func<string>(() => uid));
            typeof(MukJumpAccountRuntime).GetField("currentAccountScopeForTests", Private).SetValue(account, new Func<string>(() => uid));
            view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            deletions = 0;
            typeof(LobbyOptionsView).GetField("deleteConfirmationDispatchForTests", Private).SetValue(view, new Action(() => deletions++));
        }
        [TearDown] public void TearDown()
        {
            typeof(MukJumpAccountRuntime).GetMethod("OnDisable", Private).Invoke(account, null);
            Object.DestroyImmediate(host);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            MukJumpIdentityProfile.UseStoreForTests(null);
            PointerInput.ResetSuppressionForTests();
        }
        [Test] public void DeleteOpensSeparateBlockingPopupWithoutToastOrDeletingOnRepeatedParentTap()
        {
            Call("HandleDeleteAccount");
            Assert.That(Popup.gameObject.activeSelf, Is.True);
            Assert.That(Popup.GetComponent<Canvas>().sortingOrder,
                Is.GreaterThan(host.transform.Find("LobbyOptionsCanvas").GetComponent<Canvas>().sortingOrder));
            Assert.That(Popup.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(Popup.Find("InkDim").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(typeof(LobbyOptionsView).GetField("accountToastSource", Private).GetValue(view), Is.Null);
            Assert.That(Paper.Find("Warning").GetComponent<Text>().text,
                Is.EqualTo(GameLocalization.Translate("계정과 서버 기록이 영구 삭제됩니다")));
            Call("ConfirmAccountDeletion");
            Assert.That(deletions, Is.Zero, "팝업을 연 탭이 확인 버튼에 이어지면 안 됩니다.");
            Ready(); Call("HandleDeleteAccount");
            Assert.That(deletions, Is.Zero, "기존 계정 삭제 버튼은 최종 확인을 대신하지 않습니다.");
            Paper.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();
            Paper.Find("ConfirmButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(deletions, Is.EqualTo(1));
            Assert.That(Popup.gameObject.activeSelf, Is.False);
        }
        [Test] public void CancelNeverDeletesAndReopeningRequiresFreshConfirmation()
        {
            Call("HandleDeleteAccount"); Ready();
            Paper.Find("CancelButton").GetComponent<Button>().onClick.Invoke();
            Call("ConfirmAccountDeletion");
            Assert.That(deletions, Is.Zero);
            Assert.That(Popup.gameObject.activeSelf, Is.False);
            Call("HandleDeleteAccount"); Call("ConfirmAccountDeletion");
            Assert.That(deletions, Is.Zero);
        }
        [Test] public void ChangedAccountCannotUsePreviousPopupConfirmation()
        {
            Call("HandleDeleteAccount"); Ready(); uid = "654321";
            Call("ConfirmAccountDeletion");
            Assert.That(deletions, Is.Zero);
            Assert.That(Popup.gameObject.activeSelf, Is.False);
        }
        [Test] public void MissingAuthenticatedOwnerCannotConfirmDeletion()
        {
            uid = ""; Call("HandleDeleteAccount"); Ready(); Call("ConfirmAccountDeletion");
            Assert.That(deletions, Is.Zero);
        }
        [TestCase(true)] [TestCase(false)]
        public void ModalBlocksUnderlyingNavigationAndCancelRestoresPreviousState(bool interactable)
        {
            var underlying = host.transform.Find("LobbyOptionsCanvas").GetComponent<CanvasGroup>();
            underlying.interactable = interactable;
            Call("HandleDeleteAccount");
            Assert.That(underlying.interactable, Is.False);
            Paper.Find("CancelButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(underlying.interactable, Is.EqualTo(interactable));
            Assert.That(deletions, Is.Zero);
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void WarningAndActionsFitInAllLanguages(GameLanguage language)
        {
            GameLocalization.SetLanguage(language); Call("HandleDeleteAccount");
            Canvas.ForceUpdateCanvases();
            foreach (string path in new[] { "Title", "Warning", "CancelButton/Paper/Label", "ConfirmButton/Paper/Label" })
            {
                var text = Paper.Find(path).GetComponent<Text>();
                Assert.That(text.text, Is.Not.Empty);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), path);
                foreach (char c in text.text) if (!char.IsWhiteSpace(c)) Assert.That(text.font.HasCharacter(c), Is.True, c.ToString());
            }
            foreach (var size in new[] { new Vector2(1179, 2556), new Vector2(750, 1334), new Vector2(2556, 1179) })
            {
                view.SetDisplayMetricsForTests((int)size.x, (int)size.y, new Rect(0, 70, size.x, size.y - 140));
                Call("LayoutDeleteConfirmation");
                Assert.That(((RectTransform)Paper).anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(Paper.localScale.x, Is.GreaterThan(0));
            }
        }
    }
}
