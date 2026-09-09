using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
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
        public void BlackWashAndInputBlockerCoverFullCanvasAboveBrushTextures()
        {
            root = new GameObject("BlackoutBoundsTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");
            Transform canvas = root.transform.Find("BrushTransitionCanvas");
            var wash = (Image)GetField(view, "wash");
            Assert.That(wash.color, Is.EqualTo(InkPalette.Ink));
            Assert.That(wash.raycastTarget, Is.False);
            Assert.That(wash.transform.GetSiblingIndex(), Is.EqualTo(canvas.childCount - 1));
            foreach (string name in new[] { "InkWash", "InputBlocker" })
            {
                var rect = canvas.Find(name) as RectTransform;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            }
            Assert.That(canvas.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(9000));
            Assert.That(canvas.Find("AccumulatedBrushInk"), Is.Null);
            Assert.That(canvas.GetComponentsInChildren<RawImage>(true).Length, Is.EqualTo(14));
            foreach (var stroke in canvas.GetComponentsInChildren<RawImage>(true))
                Assert.That(stroke.color, Is.EqualTo(InkPalette.Ink));
        }

        [Test]
        public void OriginalBrushesKeepTimingAndExtraOriginalArtPassesFollow()
        {
            var view = BuildTimedView();
            var wash = (Image)GetField(view, "wash");
            var masks = (RectTransform[])GetField(view, "strokeMasks");
            IEnumerator routine = null;
            SetField(view, "startCoroutineForTests", (Action<IEnumerator>)(value => routine = value));
            view.TryPlay(null);
            bool sawExtraStroke = false, sawGentleWash = false;
            int frame = 0;
            while (routine.MoveNext())
            {
                Assert.That(++frame, Is.LessThan(110));
                if (masks[8].sizeDelta.y > 0f)
                {
                    sawExtraStroke = true;
                    Assert.That(BrushTransitionView.StrokeProgressAtSeconds(0, frame * .05f), Is.EqualTo(1f));
                }
                float alpha = wash.canvasRenderer.GetAlpha();
                sawGentleWash |= alpha > 0f && alpha < 1f;
            }
            Assert.That(sawExtraStroke && sawGentleWash, Is.True);
            Assert.That(BrushTransitionView.StrokeProgressAtSeconds(8, 1f), Is.Zero);
            Assert.That(BrushTransitionView.StrokeProgressAtSeconds(13, 1.85f), Is.EqualTo(1f));
        }

        [TestCase(1080, 1920)]
        [TestCase(1179, 2556)]
        [TestCase(1536, 2048)]
        [TestCase(1920, 1080)]
        public void AccumulatedBrushMeshFillsEverySampleIncludingCornersWithoutWash(int width, int height)
        {
            root = new GameObject("BrushCoverageMesh", typeof(RectTransform), typeof(BrushCoverGraphic));
            var cover = root.GetComponent<BrushCoverGraphic>();
            cover.rectTransform.sizeDelta = new Vector2(width, height);
            Rect rect = cover.rectTransform.rect;
            var triangles = new List<UIVertex>();
            using var mesh = new VertexHelper();
            foreach (float progress in new[] { 0f, .25f, .55f, 1f })
            {
                cover.SetProgress(progress);
                Invoke(cover, "OnPopulateMesh", mesh);
                triangles.Clear();
                mesh.GetUIVertexStream(triangles);
                int painted = 0, total = 0;
                for (int y = 0; y <= 20; y++)
                for (int x = 0; x <= 12; x++)
                {
                    // 실제 화면 경계의 안쪽 반 픽셀까지 포함한다.
                    Vector2 point = new(Mathf.Lerp(rect.xMin + .5f, rect.xMax - .5f, x / 12f),
                        Mathf.Lerp(rect.yMin + .5f, rect.yMax - .5f, y / 20f));
                    if (IsOpaqueAt(point, triangles)) painted++;
                    total++;
                }
                if (progress == 0f) Assert.That(painted, Is.Zero);
                else if (progress < 1f) Assert.That(painted, Is.InRange(1, total - 1));
                else Assert.That(painted, Is.EqualTo(total), "페이드 덮개 없이 붓 메쉬 자체가 끝까지 덮어야 합니다.");
                Assert.That(mesh.currentVertCount, Is.LessThanOrEqualTo(4608), "모바일에서도 고정된 작은 메쉬만 재사용합니다.");
            }
        }

        static bool IsOpaqueAt(Vector2 point, List<UIVertex> triangles)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                UIVertex a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (a.color.a != 255 || b.color.a != 255 || c.color.a != 255) continue;
                float area = Side(a.position, b.position, c.position);
                if (Mathf.Abs(area) < .0001f) continue;
                float ab = Side(a.position, b.position, point);
                float bc = Side(b.position, c.position, point);
                float ca = Side(c.position, a.position, point);
                if ((ab >= -.0001f && bc >= -.0001f && ca >= -.0001f) ||
                    (ab <= .0001f && bc <= .0001f && ca <= .0001f)) return true;
            }
            return false;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) =>
            (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        [TestCase(false)]
        [TestCase(true)]
        public void ShortInkHoldBeforeSwapInBothMotionModes(bool reduced)
        {
            var view = BuildTimedView();
            SetField(view, "reducedMotionForTests", (Func<bool>)(() => reduced));
            var group = (CanvasGroup)GetField(view, "group");
            var wash = (Image)GetField(view, "wash");
            int callbacks = 0;
            int frame = 0, firstBlackFrame = -1, callbackFrame = -1;
            IEnumerator routine = null;
            SetField(view, "startCoroutineForTests", (Action<IEnumerator>)(value => routine = value));
            Assert.That(view.TryPlay(() =>
            {
                callbacks++;
                callbackFrame = frame;
                Assert.That(group.alpha * wash.canvasRenderer.GetAlpha(), Is.EqualTo(1f));
                Assert.That(wash.color, Is.EqualTo(InkPalette.Ink));
                Assert.That(group.blocksRaycasts, Is.True);
                foreach (var mask in (RectTransform[])GetField(view, "strokeMasks"))
                    Assert.That(mask.gameObject.activeSelf, Is.False);
            }), Is.True);
            Assert.That(view.TryPlay(null), Is.False);
            while (routine.MoveNext())
            {
                if (callbacks == 0 && group.alpha * wash.canvasRenderer.GetAlpha() >= .9999f && firstBlackFrame < 0)
                    firstBlackFrame = frame;
                Assert.That(group.blocksRaycasts, Is.True);
                Assert.That(++frame, Is.LessThan(110), "암전과 드러남이 정해진 시간 안에 끝나야 합니다.");
            }
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(firstBlackFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That((callbackFrame - firstBlackFrame) * .05f, Is.InRange(.20f, .40f));
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
        }

        [Test]
        public void BlackoutTimerStopsInBackgroundAndResumesWithoutSkipping()
        {
            var view = BuildTimedView();
            bool active = true;
            SetField(view, "applicationActiveForTests", (Func<bool>)(() => active));
            var group = (CanvasGroup)GetField(view, "group");
            var wash = (Image)GetField(view, "wash");
            int callbacks = 0;
            IEnumerator routine = null;
            SetField(view, "startCoroutineForTests", (Action<IEnumerator>)(value => routine = value));
            view.TryPlay(() => callbacks++);
            for (int i = 0; i < 45 && wash.canvasRenderer.GetAlpha() < .9999f; i++)
                Assert.That(routine.MoveNext(), Is.True);
            Assert.That(wash.canvasRenderer.GetAlpha(), Is.EqualTo(1f));
            active = false;
            for (int i = 0; i < 100; i++)
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(group.alpha, Is.EqualTo(1f));
                Assert.That(callbacks, Is.Zero);
            }
            active = true;
            int resumedFrames = 0;
            while (routine.MoveNext())
                Assert.That(++resumedFrames, Is.LessThan(90));
            Assert.That(resumedFrames, Is.GreaterThanOrEqualTo(18));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(group.blocksRaycasts, Is.False);
        }

        [Test]
        public void ReloadStartsFullyBlackAndDoesNotRepeatTheTwoSecondHold()
        {
            var view = BuildTimedView();
            var group = (CanvasGroup)GetField(view, "group");
            var wash = (Image)GetField(view, "wash");
            IEnumerator routine = (IEnumerator)Invoke(view, "RevealLoadedSceneRoutine");
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(wash.canvasRenderer.GetAlpha(), Is.EqualTo(1f));
            Assert.That(wash.color, Is.EqualTo(InkPalette.Ink));
            Assert.That(group.blocksRaycasts, Is.True);
            int frame = 0;
            while (routine.MoveNext()) Assert.That(++frame, Is.LessThan(22));
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(group.blocksRaycasts, Is.False);
        }

        BrushTransitionView BuildTimedView()
        {
            root = new GameObject("TimedBlackoutTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");
            SetField(view, "frameDeltaForTests", (Func<float>)(() => .05f));
            SetField(view, "applicationActiveForTests", (Func<bool>)(() => true));
            SetField(view, "reducedMotionForTests", (Func<bool>)(() => false));
            SetField(view, "feedbackForTests", (Action)(() => { }));
            return view;
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

        [TestCase("buildForTests", "build-failed")]
        [TestCase("reducedMotionForTests", "settings-failed")]
        [TestCase("feedbackForTests", "feedback-failed")]
        [TestCase("startCoroutineForTests", "start-failed")]
        public void SetupFailureReleasesTransitionAndInvokesRecoveryOnce(
            string injectionField,
            string failureMessage)
        {
            root = new GameObject("BrushTransitionSetupFailureTests");
            var view = root.AddComponent<BrushTransitionView>();
            if (injectionField == "buildForTests")
            {
                SetField(view, injectionField,
                    (Action)(() => throw new InvalidOperationException(
                        failureMessage)));
            }
            else if (injectionField == "reducedMotionForTests")
            {
                SetField(view, injectionField,
                    (Func<bool>)(() => throw new InvalidOperationException(
                        failureMessage)));
            }
            else if (injectionField == "feedbackForTests")
            {
                SetField(view, "reducedMotionForTests", (Func<bool>)(() => false));
                SetField(view, injectionField,
                    (Action)(() => throw new InvalidOperationException(
                        failureMessage)));
            }
            else
            {
                SetField(view, "reducedMotionForTests", (Func<bool>)(() => true));
                SetField(view, injectionField,
                    (Action<IEnumerator>)(_ =>
                        throw new InvalidOperationException(failureMessage)));
            }

            int recoveryCount = 0;
            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: " + failureMessage));

            bool started = view.TryPlay(
                null,
                () => recoveryCount++);

            Assert.That(started, Is.False);
            Assert.That(recoveryCount, Is.EqualTo(1));
            Assert.That(view.IsPlaying, Is.False);
            var group = (CanvasGroup)GetField(view, "group");
            if (group != null)
                Assert.That(group.blocksRaycasts, Is.False);
        }

        [Test]
        public void CoroutineFailureBeforeCoverReleasesRaycastsAndRecoversOnce()
        {
            root = new GameObject("BrushTransitionCoroutineFailureTests");
            var view = root.AddComponent<BrushTransitionView>();
            Invoke(view, "BuildIfNeeded");
            SetField(view, "playing", true);
            SetField(view, "coveredCallbackStarted", false);
            var group = (CanvasGroup)GetField(view, "group");
            group.alpha = 1f;
            group.blocksRaycasts = true;
            int recoveryCount = 0;
            IEnumerator guarded = (IEnumerator)Invoke(
                view,
                "RunGuarded",
                ThrowBeforeYield(),
                (Action)(() => recoveryCount++));
            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: routine-failed"));

            Assert.That(guarded.MoveNext(), Is.False);

            Assert.That(recoveryCount, Is.EqualTo(1));
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
        }

        static IEnumerator ThrowBeforeYield()
        {
            throw new InvalidOperationException("routine-failed");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        static object Invoke(object target, string methodName, params object[] arguments)
        {
            foreach (var method in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                if (method.Name == methodName && method.GetParameters().Length == arguments.Length)
                {
                    bool matches = true;
                    var parameters = method.GetParameters();
                    for (int i = 0; i < arguments.Length; i++)
                        matches &= arguments[i] == null || parameters[i].ParameterType.IsInstanceOfType(arguments[i]);
                    if (matches) return method.Invoke(target, arguments);
                }
            throw new MissingMethodException(target.GetType().Name, methodName);
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
