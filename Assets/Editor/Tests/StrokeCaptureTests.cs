using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class StrokeCaptureTests
    {
        GameObject playerObject;
        GameObject strokeObject;
        readonly HashSet<int> existingPlatformIds = new();

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
            existingPlatformIds.Clear();
            PlatformCollider[] platforms = Object.FindObjectsByType<PlatformCollider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
                existingPlatformIds.Add(platforms[i].GetInstanceID());
        }

        [TearDown]
        public void TearDown()
        {
            LineRenderer preview = null;
            if (strokeObject != null)
            {
                var capture = strokeObject.GetComponent<StrokeCapture>();
                preview = GetField<LineRenderer>(capture, "preview");
                Object.DestroyImmediate(strokeObject);
            }
            if (preview != null)
                Object.DestroyImmediate(preview.gameObject);

            PlatformCollider[] platforms = Object.FindObjectsByType<PlatformCollider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
                if (!existingPlatformIds.Contains(platforms[i].GetInstanceID()))
                    Object.DestroyImmediate(platforms[i].gameObject);

            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void SafeSegmentSelectionReturnsLongestReusableSegment()
        {
            playerObject = new GameObject("Player");
            var player = playerObject.AddComponent<PlayerController>();
            playerObject.transform.position = Vector3.zero;

            var points = new List<Vector2>
            {
                new(-2f, 0f),
                new(-1f, 0f),
                Vector2.zero,
                new(1.5f, 0f),
                new(2.5f, 0f),
                new(3.5f, 0f),
            };
            var players = new List<PlayerController> { player };
            var longest = new List<Vector2>();
            var candidate = new List<Vector2>();
            var method = typeof(StrokeCapture).GetMethod(
                "SelectLongestSafeSegment",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var result = (List<Vector2>)method.Invoke(null, new object[]
            {
                points,
                players,
                0.75f,
                longest,
                candidate,
            });

            Assert.AreSame(longest, result,
                "선분 결과 버퍼를 재사용해 매 스트로크 할당을 만들지 않아야 합니다.");
            Assert.That(result, Is.EqualTo(new[]
            {
                new Vector2(1.5f, 0f),
                new Vector2(2.5f, 0f),
                new Vector2(3.5f, 0f),
            }));
        }

        [Test]
        public void EndStrokeFinalizesTheActivePointSetOnlyOnce()
        {
            StrokeCapture capture = CreateActiveStroke(new Vector2(10001f, 10000f));
            MethodInfo endStroke = GetMethod("EndStroke");

            endStroke.Invoke(capture, null);
            endStroke.Invoke(capture, null);

            Assert.That(CountNewPlatforms(), Is.EqualTo(1),
                "같은 활성 획의 종료 신호가 겹쳐도 발판은 한 번만 생성되어야 합니다.");
            Assert.That(GetField<bool>(capture, "drawing"), Is.False);
        }

        [Test]
        public void MaximumLengthSplitFinalizesOnceAndRestartsFromTheSeam()
        {
            Vector2 seam = new(10001.25f, 10000.5f);
            StrokeCapture capture = CreateActiveStroke(seam);
            MethodInfo split = GetMethod("FinalizeStrokeAndRestartAtSeam");

            split.Invoke(capture, null);

            var points = GetField<List<Vector2>>(capture, "points");
            Assert.That(CountNewPlatforms(), Is.EqualTo(1),
                "분할 경계에서는 이전 점열을 정확히 한 번만 확정해야 합니다.");
            Assert.That(GetField<bool>(capture, "drawing"), Is.True,
                "포인터를 놓지 않은 최대 길이 분할은 드로잉을 계속해야 합니다.");
            Assert.That(GetField<float>(capture, "strokeLength"), Is.Zero);
            Assert.That(points, Is.EqualTo(new[] { seam }),
                "다음 점열은 이전 획의 마지막 seam 한 점에서 시작해야 합니다.");
        }

        [Test]
        public void MaximumLengthSplitConsumesObservedTailWithoutGap()
        {
            strokeObject = new GameObject("StrokeCaptureTailTest");
            var capture = strokeObject.AddComponent<StrokeCapture>();
            SetField(capture, "maxContinuousStrokeLength", 1f);
            SetField(capture, "unlimitedInkUntil", Time.time + 30f);
            Vector2 start = new(10000f, 10000f);
            GetMethod("BeginStrokeAtWorld").Invoke(capture, new object[] { start });

            GetMethod("AppendWorldSample").Invoke(
                capture,
                new object[] { start + Vector2.right * 2.5f });

            var points = GetField<List<Vector2>>(capture, "points");
            Assert.That(CountNewPlatforms(), Is.EqualTo(2));
            Assert.That(points.Count, Is.EqualTo(2));
            Assert.That(points[0].x, Is.EqualTo(start.x + 2f).Within(0.001f));
            Assert.That(points[1].x, Is.EqualTo(start.x + 2.5f).Within(0.001f));
            Assert.That(GetField<float>(capture, "strokeLength"),
                Is.EqualTo(0.5f).Within(0.001f));
        }

        [TestCase(10f, 1f, 1f, 10f)]
        [TestCase(10f, 0.97f, 1f, 9.7f)]
        [TestCase(1.5f, 0.97f, 0.94f, 1.3677f)]
        [TestCase(2f, 0.97f, 0.94f, 1.94f)]
        public void PermanentEfficiencyReducesRetainedInkBudgetCost(
            float rawLength,
            float globalMultiplier,
            float shortMultiplier,
            float expected)
        {
            MethodInfo method = typeof(StrokeCapture).GetMethod(
                "StrokeBudgetCost",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float actual = (float)method.Invoke(null, new object[]
            {
                rawLength,
                globalMultiplier,
                shortMultiplier,
            });

            Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
        }

        StrokeCapture CreateActiveStroke(Vector2 end)
        {
            strokeObject = new GameObject("StrokeCaptureSplitTest");
            var capture = strokeObject.AddComponent<StrokeCapture>();
            MethodInfo beginStroke = GetMethod("BeginStrokeAtWorld");
            Vector2 start = end - Vector2.right;
            beginStroke.Invoke(capture, new object[] { start });

            var points = GetField<List<Vector2>>(capture, "points");
            points.Add(end);
            SetField(capture, "strokeLength", Vector2.Distance(start, end));
            return capture;
        }

        int CountNewPlatforms()
        {
            int count = 0;
            PlatformCollider[] platforms = Object.FindObjectsByType<PlatformCollider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < platforms.Length; i++)
                if (!existingPlatformIds.Contains(platforms[i].GetInstanceID()))
                    count++;
            return count;
        }

        static MethodInfo GetMethod(string methodName)
        {
            MethodInfo method = typeof(StrokeCapture).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }

        static T GetField<T>(StrokeCapture capture, string fieldName)
        {
            FieldInfo field = typeof(StrokeCapture).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(capture);
        }

        static void SetField<T>(StrokeCapture capture, string fieldName, T value)
        {
            FieldInfo field = typeof(StrokeCapture).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(capture, value);
        }
    }
}
