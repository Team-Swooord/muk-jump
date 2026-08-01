using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class BrushTransitionViewTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void CoveredCallbackFailureReleasesRaycastBlocker()
        {
            root = new GameObject("BrushTransitionViewTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");
            SetField(view, "playing", true);
            var group = (CanvasGroup)GetField(view, "group");
            group.alpha = 1f;
            group.blocksRaycasts = true;
            bool recoveryCalled = false;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: transition-test"));

            bool succeeded = (bool)Invoke(
                view,
                "TryInvokeCovered",
                (Action)(() => throw new InvalidOperationException("transition-test")),
                (Action)(() => recoveryCalled = true));

            Assert.That(succeeded, Is.False);
            Assert.That(recoveryCalled, Is.True);
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
        }

        [Test]
        public void DisablingActiveTransitionInvokesRecovery()
        {
            root = new GameObject("BrushTransitionDisableTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");
            bool recoveryCalled = false;
            SetField(view, "playing", true);
            SetField(view, "coveredCallbackStarted", false);
            SetField(view, "activeFailureCallback", (Action)(() => recoveryCalled = true));

            Invoke(view, "OnDisable");

            Assert.That(recoveryCalled, Is.True);
            Assert.That(view.IsPlaying, Is.False);
            var group = (CanvasGroup)GetField(view, "group");
            Assert.That(group.blocksRaycasts, Is.False);
        }

        [Test]
        public void TransitionBuildsFullScreenInputBlockerAndRejectsOverlap()
        {
            root = new GameObject("BrushTransitionBlockerTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");

            var blocker = root.transform.Find(
                "BrushTransitionCanvas/InputBlocker")?.GetComponent<Image>();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(blocker.color.a, Is.Zero);

            SetField(view, "playing", true);
            Assert.That(view.TryPlay(null), Is.False);
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, arguments);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
