using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 작은 크기에서도 읽히는 굵은 먹획. 끝의 갈필과 짧은 번짐만 남긴 고정 메시다.
    public sealed class InkBrushIcon : MaskableGraphic
    {
        public enum Symbol { Pause, Arrow }
        [SerializeField] Symbol symbol;
        public void Configure(Symbol value)
        {
            symbol = value;
            color = InkPalette.Ink;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (symbol == Symbol.Pause)
            {
                Stroke(mesh, new Vector2(-.17f, .36f), new Vector2(-.20f, -.36f), .15f);
                Stroke(mesh, new Vector2(.22f, .34f), new Vector2(.18f, -.35f), .145f);
            }
            else
            {
                Stroke(mesh, new Vector2(-.43f, -.012f), new Vector2(.35f, .012f), .135f);
                Stroke(mesh, new Vector2(.08f, .29f), new Vector2(.39f, .012f), .13f);
                Stroke(mesh, new Vector2(.39f, .012f), new Vector2(.075f, -.28f), .13f);
            }
        }

        void Stroke(VertexHelper mesh, Vector2 start, Vector2 end, float thickness)
        {
            Rect rect = GetPixelAdjustedRect();
            start = Vector2.Scale(start, rect.size) + rect.center;
            end = Vector2.Scale(end, rect.size) + rect.center;
            float width = Mathf.Min(rect.width, rect.height) * thickness;
            Vector2 normal = new Vector2(-(end - start).y, (end - start).x).normalized;
            const int steps = 18;
            int first = mesh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float taper = Mathf.Lerp(.45f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) * 10f));
                float grain = 1f + Mathf.Sin(t * 39f + start.x) * .12f + Mathf.Sin(t * 79f) * .05f;
                Vector2 point = Vector2.Lerp(start, end, t) + normal * (Mathf.Sin(t * 9f) * width * .06f);
                for (int band = 0; band < 4; band++)
                {
                    float edge = band == 0 ? -.58f : band == 1 ? -.42f : band == 2 ? .42f : .58f;
                    Color tint = color;
                    if (band == 0 || band == 3) tint.a = 0f;
                    mesh.AddVert(point + normal * (edge * width * taper * grain), tint, Vector2.zero);
                }
                if (i == 0) continue;
                for (int band = 0; band < 3; band++)
                {
                    int a = first + (i - 1) * 4 + band;
                    mesh.AddTriangle(a, a + 4, a + 5);
                    mesh.AddTriangle(a, a + 5, a + 1);
                }
            }
        }
    }
}
