using System;
using System.Reflection;
using System.Threading.Tasks;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        void SetClose(Func<Task> action) => typeof(AppsInTossExitView)
            .GetField("closeViewForTests", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(view, action);
    }
}
