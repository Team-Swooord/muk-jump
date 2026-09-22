using System;
using System.Reflection;
using System.Threading.Tasks;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MukJump.EditorTests
{
    public sealed class AppsInTossExitTests
    {
        GameObject host;
        AppsInTossExitView view;
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("TossExitTest");
            view = host.AddComponent<AppsInTossExitView>();
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            PointerInput.ResetSuppressionForTests();
            Assert.That(AppsInTossExitView.IsOpen, Is.False);
        }
        [Test]
        public void ExitPromptIsSingleCenteredModalAndBackCancelsIt()
        {
            view.RequestExit(); view.RequestExit();
            Assert.That(AppsInTossExitView.IsOpen, Is.True);
            Assert.That(host.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(host.transform.Find("TossExitConfirmation/Dim").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(host.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
            foreach (Text text in host.GetComponentsInChildren<Text>(true))
                Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            view.HandleBack();
            Assert.That(AppsInTossExitView.IsOpen, Is.False);
        }
        [Test]
        public void CloseFailureLeavesRetryAndCancelAvailable()
        {
            SetClose(() => Task.FromException(new Exception("test failure")));
            view.RequestExit(); view.Confirm();
            Assert.That(AppsInTossExitView.IsOpen, Is.True);
            foreach (Button button in host.GetComponentsInChildren<Button>(true))
                Assert.That(button.interactable, Is.True);
            view.Cancel();
            Assert.That(AppsInTossExitView.IsOpen, Is.False);
        }
        [Test]
        public void DuplicateConfirmSendsOnlyOneRequestAndLateCallbackIsIgnored()
        {
            int count = 0;
            var pending = new TaskCompletionSource<bool>();
            SetClose(() => { count++; return pending.Task; });
            view.RequestExit(); view.Confirm(); view.Confirm(); view.Cancel();
            Assert.That(count, Is.EqualTo(1));
            Assert.That(AppsInTossExitView.IsOpen, Is.True);
            view.enabled = false;
            pending.SetResult(true);
            Assert.That(AppsInTossExitView.IsOpen, Is.False);
        }

        [Test]
        public void OutsideTapCancelsWithoutExitingAndCannotCancelPendingExit()
        {
            int calls = 0;
            var pending = new TaskCompletionSource<bool>();
            SetClose(() => { calls++; return pending.Task; });
            view.RequestExit();
            var dim = host.transform.Find("TossExitConfirmation/Dim").GetComponent<EventTrigger>();
            Assert.That(dim, Is.Not.Null);
            var outside = new PointerEventData(null)
            {
                position = new Vector2(-2000, -2000), pressPosition = new Vector2(-2000, -2000),
                button = PointerEventData.InputButton.Left
            };
            dim.OnPointerClick(outside);
            Assert.That(AppsInTossExitView.IsOpen, Is.False);
            Assert.That(calls, Is.Zero, "바깥 탭은 종료가 아니라 취소입니다");
            view.RequestExit(); view.Confirm();
            dim.OnPointerClick(outside);
            Assert.That(AppsInTossExitView.IsOpen, Is.True);
            Assert.That(calls, Is.EqualTo(1));
            pending.SetResult(true);
        }
        void SetClose(Func<Task> action) => typeof(AppsInTossExitView)
            .GetField("closeViewForTests", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(view, action);
    }
}
