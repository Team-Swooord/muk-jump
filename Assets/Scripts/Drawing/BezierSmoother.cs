using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Drawing
{
    /// 터치 스트로크 원본 점열을 부드러운 곡선 점열로 다듬는다.
    /// Chaikin 코너 컷팅(베지어 근사) 후 일정 간격으로 리샘플링.
    public static class BezierSmoother
    {
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> raw, int iterations = 2, float spacing = 0.12f)
        {
            if (raw == null || raw.Count < 2)
                return raw == null ? new List<Vector2>() : new List<Vector2>(raw);

            var points = new List<Vector2>(raw);
            for (int i = 0; i < iterations; i++)
                points = Chaikin(points);

            return Resample(points, spacing);
        }

        static List<Vector2> Chaikin(List<Vector2> points)
        {
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
            var result = new List<Vector2> { points[0] };
            float carried = 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                float segment = Vector2.Distance(a, b);
                if (segment <= Mathf.Epsilon) continue;

                float d = spacing - carried;
                while (d <= segment)
                {
                    result.Add(Vector2.Lerp(a, b, d / segment));
                    d += spacing;
                }
                carried = segment - (d - spacing);
            }

            if (Vector2.Distance(result[^1], points[^1]) > spacing * 0.25f)
                result.Add(points[^1]);

            return result;
        }

        public static float PolylineLength(IReadOnlyList<Vector2> points)
        {
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
                length += Vector2.Distance(points[i], points[i + 1]);
            return length;
        }
    }
}
