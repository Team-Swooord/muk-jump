using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class StrokeCaptureTests
    {
        GameObject playerObject;

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
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
    }
}
