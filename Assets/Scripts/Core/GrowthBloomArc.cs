using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 한 번 그어지며 끝이 가늘어지는 금분 먹고리. 텍스처·전용 머티리얼은 만들지 않는다.
    public sealed class GrowthBloomArc : MaskableGraphic
    {
        float reveal;
        public bool IsWaterRipple { get; set; }
        public float Reveal
        {
            get => reveal;
            set { if (Mathf.Approximately(reveal, value)) return; reveal = value; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 80;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = (IsWaterRipple ? t * 360f : -32f + t * 316f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float contour = radius * (.98f + .015f * Mathf.Sin(angle * 3f) + .009f * Mathf.Sin(angle * 7f));
                float thickness = IsWaterRipple
                    ? radius * .009f * (.7f + .3f * Mathf.Sin(angle * 5f + 1.2f))
                    : radius * .019f * Mathf.Lerp(1.3f, .15f, t);
                Color tint = color;
                tint.a *= Mathf.Clamp01((reveal - t) * segments) * Mathf.Clamp01(t * 24f)
                    * (.70f + .30f * Mathf.Abs(Mathf.Sin(i * 1.71f)));
                if (IsWaterRipple)
                    tint.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                        (Mathf.Sin(angle * 3f + .8f) + .45f * Mathf.Sin(angle * 7f) + .35f) * 1.6f));
                mesh.AddVert(normal * (contour - thickness), tint, Vector2.zero);
                mesh.AddVert(normal * (contour + thickness), tint, Vector2.zero);
                if (i == 0) continue;
                int a = (i - 1) * 2;
                mesh.AddTriangle(a, a + 1, a + 3);
                mesh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }
}
