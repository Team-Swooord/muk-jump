using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Drawing
{
    /// 터치 스트로크 원본 점열을 부드러운 곡선 점열로 다듬는다.
    /// Chaikin 코너 컷팅(베지어 근사) 후 일정 간격으로 리샘플링.
    public static class BezierSmoother
    {
        const int MaxSmoothingIterations = 8;
        const int MaxGeneratedPoints = 32768;

        public static List<Vector2> Smooth(IReadOnlyList<Vector2> raw, int iterations = 2, float spacing = 0.12f)
        {
            ValidateParameters(iterations, spacing);
            if (raw == null) return new List<Vector2>();
            ValidatePoints(raw, nameof(raw));
            if (raw.Count < 2) return new List<Vector2>(raw);

            var points = new List<Vector2>(raw);
            for (int i = 0; i < iterations; i++)
            {
                points = Chaikin(points);
                ValidatePoints(points, nameof(raw));
            }

            return Resample(points, spacing);
        }

        static void ValidateParameters(int iterations, float spacing)
        {
            if (iterations < 0 || iterations > MaxSmoothingIterations)
                throw new ArgumentOutOfRangeException(nameof(iterations), iterations,
                    $"스무딩 반복 횟수는 0~{MaxSmoothingIterations} 범위여야 합니다.");
            if (!IsFinite(spacing) || spacing <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing,
                    "리샘플링 간격은 0보다 큰 유한값이어야 합니다.");
        }

        static void ValidatePoints(IReadOnlyList<Vector2> points, string parameterName)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (IsFinite(points[i].x) && IsFinite(points[i].y)) continue;
                throw new ArgumentException(
                    $"점 {i}에 NaN 또는 Infinity가 포함되어 있습니다.", parameterName);
            }
        }

        static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        static List<Vector2> Chaikin(List<Vector2> points)
        {
            if (points.Count > MaxGeneratedPoints / 2)
                throw new ArgumentException(
                    $"스무딩 결과는 {MaxGeneratedPoints:N0}개 정점을 넘을 수 없습니다.",
                    nameof(points));

            var result = new List<Vector2>(points.Count * 2) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                result.Add(Vector2.Lerp(a, b, 0.25f));
                result.Add(Vector2.Lerp(a, b, 0.75f));
            }
            result.Add(points[^1]);
            return result;
        }

        /// 폴리라인을 따라 일정 간격으로 점을 다시 찍는다 (콜라이더/라인 정점 수 안정화)
        static List<Vector2> Resample(List<Vector2> points, float spacing)
        {
            float totalLength = PolylineLength(points);
            if (totalLength > 0f &&
                spacing < totalLength / (MaxGeneratedPoints - 1))
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing,
                    $"리샘플링 결과는 {MaxGeneratedPoints:N0}개 정점을 넘을 수 없습니다.");
            }

            var result = new List<Vector2> { points[0] };
            float carried = 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                float segment = Vector2.Distance(a, b);
                if (!IsFinite(segment))
                    throw new ArgumentException("점 사이 거리가 유한 범위를 벗어났습니다.",
                        nameof(points));
                if (segment <= Mathf.Epsilon) continue;

                float d = spacing - carried;
                while (d <= segment)
                {
                    if (result.Count >= MaxGeneratedPoints)
                        throw new ArgumentOutOfRangeException(nameof(spacing), spacing,
                            $"리샘플링 결과는 {MaxGeneratedPoints:N0}개 정점을 넘을 수 없습니다.");
                    result.Add(Vector2.Lerp(a, b, d / segment));
                    d += spacing;
                }
                carried = segment - (d - spacing);
            }

            if (Vector2.Distance(result[^1], points[^1]) > spacing * 0.25f)
            {
                if (result.Count >= MaxGeneratedPoints)
                    throw new ArgumentOutOfRangeException(nameof(spacing), spacing,
                        $"리샘플링 결과는 {MaxGeneratedPoints:N0}개 정점을 넘을 수 없습니다.");
                result.Add(points[^1]);
            }

            return result;
        }

        public static float PolylineLength(IReadOnlyList<Vector2> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            ValidatePoints(points, nameof(points));
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                length += Vector2.Distance(points[i], points[i + 1]);
                if (!IsFinite(length))
                    throw new ArgumentException("폴리라인 길이가 유한 범위를 벗어났습니다.",
                        nameof(points));
            }
            return length;
        }
    }
}
