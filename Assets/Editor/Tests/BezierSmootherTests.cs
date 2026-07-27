using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MukJump.Drawing;

namespace MukJump.EditorTests
{
    public sealed class BezierSmootherTests
    {
        static readonly List<Vector2> ValidPoints = new()
        {
            Vector2.zero,
            Vector2.one,
        };

        [TestCase(-1)]
        [TestCase(9)]
        public void SmoothRejectsUnsupportedIterationCount(int iterations)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BezierSmoother.Smooth(ValidPoints, iterations));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.Epsilon)]
        public void SmoothRejectsUnsafeSpacing(float spacing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BezierSmoother.Smooth(ValidPoints, 0, spacing));
        }

        [Test]
        public void SmoothRejectsNonFinitePointEvenForSinglePoint()
        {
            var points = new List<Vector2>
            {
                new(float.NaN, 0f),
            };

            Assert.Throws<ArgumentException>(() => BezierSmoother.Smooth(points));
        }

        [Test]
        public void SmoothHandlesDuplicatePointsWithoutNonFiniteOutput()
        {
            var points = new List<Vector2>
            {
                Vector2.zero,
                Vector2.zero,
                Vector2.right,
            };

            var result = BezierSmoother.Smooth(points, 0, 0.2f);

            Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(result[0], Is.EqualTo(Vector2.zero));
            Assert.That(result[^1], Is.EqualTo(Vector2.right));
            Assert.That(result, Has.All.Matches<Vector2>(
                point => !float.IsNaN(point.x) &&
                    !float.IsNaN(point.y) &&
                    !float.IsInfinity(point.x) &&
                    !float.IsInfinity(point.y)));
        }
    }
}
