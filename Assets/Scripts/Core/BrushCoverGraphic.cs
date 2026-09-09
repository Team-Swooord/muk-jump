using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 붓이 지나간 자리만 채우는 먹 메쉬. 화면 전체 알파 페이드 없이 가장자리까지 칠한다.
    /// 기존 붓 원화 아래에서 같은 순서로 누적되며, 하나의 재사용 Graphic만 사용한다.
    [DisallowMultipleComponent]
    public sealed class BrushCoverGraphic : MaskableGraphic
    {
        const int PassCount = 8;
        const int BristleBands = 16;
        const int LengthSegments = 8;
        const float TipFeather = .014f;
        static readonly Color32 Ink = new(0, 0, 0, 255);
        static readonly Color32 ClearInk = new(0, 0, 0, 0);
        float progress;

        public float Progress => progress;

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value)) return;
            progress = value;
            SetVerticesDirty();
        }

        public static float StrokeProgress(int index, float value)
        {
            float start = index switch
            {
                0 => 0f, 1 => .10f, 2 => .26f, 3 => .40f,
                4 => .56f, 5 => .72f, 6 => .64f, _ => .76f,
            };
            float duration = index < 2 ? .23f : index < 6 ? .28f : .22f;
            float t = Mathf.Clamp01((value - start) / duration);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (progress <= 0f || rect.width <= 0f || rect.height <= 0f) return;
            for (int pass = 0; pass < PassCount; pass++)
                AddStroke(mesh, rect, pass, StrokeProgress(pass, progress));
        }

        static void AddStroke(VertexHelper mesh, Rect rect, int pass, float amount)
        {
            if (amount <= 0f) return;
            for (int band = 0; band < BristleBands; band++)
            {
                float q0 = band * 2f / BristleBands - 1f;
                float q1 = (band + 1) * 2f / BristleBands - 1f;
                float tip0 = Mathf.Clamp01(amount + BristleTip(q0, pass));
                float tip1 = Mathf.Clamp01(amount + BristleTip(q1, pass));
                float core0 = Mathf.Max(0f, tip0 - TipFeather);
                float core1 = Mathf.Max(0f, tip1 - TipFeather);
                for (int segment = 0; segment < LengthSegments; segment++)
                {
                    float from = (float)segment / LengthSegments;
                    if (from >= core0 && from >= core1) break;
                    float to = (float)(segment + 1) / LengthSegments;
                    AddQuad(mesh,
                        Point(rect, pass, Mathf.Min(from, core0), q0),
                        Point(rect, pass, Mathf.Min(from, core1), q1),
                        Point(rect, pass, Mathf.Min(to, core1), q1),
                        Point(rect, pass, Mathf.Min(to, core0), q0), Ink, Ink);
                }
                // 반듯한 네모 끝 대신 길이가 다른 붓털과 아주 짧은 번짐을 남긴다.
                AddQuad(mesh, Point(rect, pass, core0, q0), Point(rect, pass, core1, q1),
                    Point(rect, pass, tip1, q1), Point(rect, pass, tip0, q0), Ink, ClearInk);
            }
        }

        static float BristleTip(float cross, int pass) =>
            .018f * Mathf.Sin(cross * 17f + pass * 2.3f) +
            .011f * Mathf.Sin(cross * 39f - pass * 1.7f);

        static Vector2 Point(Rect rect, int pass, float distance, float cross)
        {
            bool vertical = pass >= 2 && pass <= 5;
            Vector2 start, end;
            switch (pass)
            {
                case 0: start = new(-.15f, .99f); end = new(.80f, .90f); break;
                case 1: start = new(1.15f, .92f); end = new(.22f, .83f); break;
                case 2: start = new(.08f, 1.16f); end = new(.06f, -.16f); break;
                case 3: start = new(.35f, 1.16f); end = new(.38f, -.16f); break;
                case 4: start = new(.65f, 1.16f); end = new(.62f, -.16f); break;
                case 5: start = new(.92f, 1.16f); end = new(.94f, -.16f); break;
                case 6: start = new(-.15f, .12f); end = new(1.15f, .04f); break;
                default: start = new(1.15f, .03f); end = new(-.15f, .12f); break;
            }
            Vector2 center = rect.min + Vector2.Scale(rect.size, Vector2.Lerp(start, end, distance));
            float halfWidth = vertical ? rect.width * .24f : rect.height * .14f;
            float edge = .965f + .018f * Mathf.Sin(distance * 25f + pass) +
                .012f * Mathf.Sin(distance * 51f + pass * 1.8f);
            float bend = .025f * Mathf.Sin(distance * 14f + pass * 2f);
            return center + (vertical ? Vector2.right : Vector2.up) *
                (halfWidth * (cross * edge + bend));
        }

        static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color32 back, Color32 front)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, back, Vector2.zero);
            mesh.AddVert(b, back, Vector2.zero);
            mesh.AddVert(c, front, Vector2.zero);
            mesh.AddVert(d, front, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
