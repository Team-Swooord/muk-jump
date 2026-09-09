using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 중앙 아이콘의 원형 갈필 원화를 둘레 길이에 맞춰 네모로 펼친다.
    /// 선을 새로 그리거나 가로로 늘리지 않아 원화의 두 겹 붓결·끊김을 그대로 쓴다.
    [DisallowMultipleComponent]
    public sealed class GrowthRingFrameGraphic : Image
    {
        public const int Segments = 320;
        public const int RadialBands = 6;
        public const float CornerRadius = 36f;
        [SerializeField] float brushScale = 1f;
        public float BrushScale
        {
            get => brushScale;
            set
            {
                if (Mathf.Approximately(brushScale, value)) return;
                brushScale = value;
                SetVerticesDirty();
            }
        }

        public GrowthRingFrameGraphic() => useLegacyMeshGeneration = false;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (sprite == null) return;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            float strokeScale = Mathf.Min(150f, Mathf.Min(rect.width, rect.height) * .4f);
            for (int row = 0; row <= RadialBands; row++)
            {
                float radius = Mathf.Lerp(.62f, 1.04f, row / (float)RadialBands);
                float inset = (1f - radius) * strokeScale - 18f * brushScale;
                Color tint = color;
                // 원화 바깥 투명 여백에서만 끝내고 UV 경계의 반복 샘플을 숨긴다.
                if (row == 0 || row == RadialBands) tint.a = 0f;
                for (int column = 0; column <= Segments; column++)
                {
                    float progress = column / (float)Segments;
                    Vector2 point = PerimeterPoint(rect.size, progress, out Vector2 normal);
                    float angle = (.375f - progress) * Mathf.PI * 2f;
                    Vector2 sample = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * .5f) + Vector2.one * .5f;
                    mesh.AddVert(point - normal * inset + rect.center, tint,
                        new Vector2(Mathf.LerpUnclamped(uv.x, uv.z, sample.x), Mathf.LerpUnclamped(uv.y, uv.w, sample.y)));
                }
            }
            for (int row = 0; row < RadialBands; row++)
            for (int column = 0; column < Segments; column++)
            {
                int a = row * (Segments + 1) + column;
                int b = a + Segments + 1;
                mesh.AddTriangle(a, b, b + 1);
                mesh.AddTriangle(a, b + 1, a + 1);
            }
        }

        public static Vector2 PerimeterPoint(Vector2 size, float progress, out Vector2 normal)
        {
            Vector2 half = size * .5f;
            float radius = Mathf.Min(CornerRadius, Mathf.Min(half.x, half.y) * .4f);
            float horizontal = size.x - radius * 2f;
            float vertical = size.y - radius * 2f;
            float arc = Mathf.PI * radius * .5f;
            float distance = Mathf.Repeat(progress, 1f) * (horizontal * 2f + vertical * 2f + arc * 4f);
            for (int side = 0; side < 4; side++)
            {
                float length = side % 2 == 0 ? horizontal : vertical;
                if (distance <= length)
                {
                    normal = side switch { 0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, _ => Vector2.left };
                    return side switch
                    {
                        0 => new Vector2(-half.x + radius + distance, half.y),
                        1 => new Vector2(half.x, half.y - radius - distance),
                        2 => new Vector2(half.x - radius - distance, -half.y),
                        _ => new Vector2(-half.x, -half.y + radius + distance),
                    };
                }
                distance -= length;
                if (distance <= arc)
                {
                    float angle = (90f - side * 90f - distance / arc * 90f) * Mathf.Deg2Rad;
                    normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 center = side switch
                    {
                        0 => new Vector2(half.x - radius, half.y - radius),
                        1 => new Vector2(half.x - radius, -half.y + radius),
                        2 => new Vector2(-half.x + radius, -half.y + radius),
                        _ => new Vector2(-half.x + radius, half.y - radius),
                    };
                    return center + normal * radius;
                }
                distance -= arc;
            }
            normal = Vector2.up;
            return new Vector2(-half.x + radius, half.y);
        }
    }
}
