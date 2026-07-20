using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Drawing
{
    /// <summary>
    /// 손끝으로 대충 찍은 원시 터치 포인트를 부드러운 곡선(Catmull-Rom 기반)으로 변환한다.
    /// 결과는 물리 콜라이더 생성(PlatformCollider)과 AI 변환용 스트로크 텍스처 렌더링에 재사용된다.
    /// </summary>
    public static class BezierSmoother
    {
        /// <param name="rawPoints">원시 터치 포인트 (월드 좌표)</param>
        /// <param name="segmentsPerSpan">두 점 사이를 몇 개의 세그먼트로 보간할지</param>
        /// <param name="minPointDistance">이보다 가까운 연속 포인트는 노이즈로 간주해 제거</param>
        public static Vector2[] Smooth(IReadOnlyList<Vector2> rawPoints, int segmentsPerSpan = 8, float minPointDistance = 0.02f)
        {
            List<Vector2> cleaned = RemoveNoise(rawPoints, minPointDistance);

            if (cleaned.Count < 2)
                return cleaned.ToArray();

            if (cleaned.Count == 2)
                return cleaned.ToArray();

            var result = new List<Vector2>();
            int n = cleaned.Count;

            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = cleaned[Mathf.Max(i - 1, 0)];
                Vector2 p1 = cleaned[i];
                Vector2 p2 = cleaned[Mathf.Min(i + 1, n - 1)];
                Vector2 p3 = cleaned[Mathf.Min(i + 2, n - 1)];

                int segments = i == n - 2 ? segmentsPerSpan + 1 : segmentsPerSpan;
                for (int s = 0; s < segments; s++)
                {
                    float t = s / (float)segmentsPerSpan;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result.ToArray();
        }

        private static List<Vector2> RemoveNoise(IReadOnlyList<Vector2> raw, float minDist)
        {
            var cleaned = new List<Vector2>();
            foreach (var p in raw)
            {
                if (cleaned.Count == 0 || Vector2.Distance(cleaned[^1], p) >= minDist)
                {
                    cleaned.Add(p);
                }
            }
            return cleaned;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        /// <summary>발판의 대략적인 표면 각도(도 단위, 0=수평)를 시작-끝 벡터로 근사한다.</summary>
        public static float EstimateAngleDeg(Vector2[] smoothed)
        {
            if (smoothed.Length < 2) return 0f;
            Vector2 dir = smoothed[^1] - smoothed[0];
            return Vector2.SignedAngle(Vector2.right, dir);
        }
    }
}
