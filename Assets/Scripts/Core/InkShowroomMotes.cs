using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 한 번 만든 메시로 한지 조각과 먹방울을 흩날린다. 게임 난수·물리·머티리얼에 관여하지 않는다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkShowroomMotes : MaskableGraphic
    {
        public const int Capacity = 28;
        float time;
        int count;
        public int VisibleCount => count;
        public void Present(float elapsed, int budget)
        {
            time = elapsed;
            count = Mathf.Clamp(budget, 0, Capacity);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Clamp01((time - .06f - i % 4 * .017f) / (.62f + i % 5 * .065f));
                if (t <= 0f || t >= 1f) continue;
                float angle = (i * 137.508f + 17f) * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                float travel = 1f - Mathf.Pow(1f - t, 2.7f);
                // 눈과 얼굴이 있는 중앙을 비워 두고 바깥으로 휘어나가는 흐름만 만든다.
                Vector2 p = radial * (122f + travel * (78f + i % 5 * 13f));
                p += tangent * Mathf.Sin(t * Mathf.PI) * (15f + i % 4 * 9f);
                p.y -= t * t * (16f + i % 3 * 8f);
                float roll = angle + t * (i % 2 == 0 ? 2.4f : -1.8f);
                Vector2 x = new Vector2(Mathf.Cos(roll), Mathf.Sin(roll));
                Vector2 y = new Vector2(-x.y, x.x);
                bool paper = i % 3 != 0;
                float size = (paper ? 12f : 6f) * (1f - t * .55f);
                x *= size * (paper ? .5f + .5f * Mathf.Abs(Mathf.Cos(t * 5f + i)) : .7f);
                y *= size * (paper ? 1.25f : .85f);
                Color tint = paper ? InkPalette.Paper2 : InkPalette.Ink;
                if (i % 9 == 0) tint = InkPalette.Red;
                tint.a = Mathf.Clamp01(t * 14f) * Mathf.Pow(1f - t, 1.1f) * (paper ? .9f : .7f);
                int n = mesh.currentVertCount;
                // 똑같은 직선 광선 대신 끝이 잘린 비대칭 다섯 변의 작은 종이 조각.
                mesh.AddVert(p - x - y * .65f, tint, Vector2.zero);
                mesh.AddVert(p + x * .65f - y, tint, Vector2.zero);
                mesh.AddVert(p + x + y * .35f, tint, Vector2.zero);
                mesh.AddVert(p + x * .15f + y, tint, Vector2.zero);
                mesh.AddVert(p - x * .85f + y * .55f, tint, Vector2.zero);
                mesh.AddTriangle(n, n + 1, n + 2);
                mesh.AddTriangle(n, n + 2, n + 3);
                mesh.AddTriangle(n, n + 3, n + 4);
            }
        }
    }
}
