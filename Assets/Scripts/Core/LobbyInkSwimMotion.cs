using UnityEngine;

namespace MukJump.Core
{
    /// 로비 idle 원화만 움직인다. 얼굴·모자 접점은 고정하고 양쪽 팔다리 끝을 번갈아 젓는다.
    [DisallowMultipleComponent]
    public sealed class LobbyInkSwimMotion : UnityEngine.UI.BaseMeshEffect
    {
        public const int Segments = 16;
        public const float CycleSeconds = 1.4f;
        public static readonly Vector2 Padding = new(.04f, .065f);
        float motionTime, motionStrength;
        public float MotionTime => motionTime;
        public void Present(float time, float strength)
        {
            time = InkAccessoryMotion.SampleTime(time);
            strength = Mathf.Clamp01(strength);
            if (motionTime == time && motionStrength == strength) return;
            bool redraw = strength > 0f || motionStrength > 0f;
            motionTime = time; motionStrength = strength;
            if (redraw && graphic != null) graphic.SetVerticesDirty();
        }
        // 몸체 프레임 중심 기준 좌표. 신발도 같은 발끝 변위를 사용해 발에서 떨어지지 않는다.
        public static Vector2 DeformPoint(Vector2 point, float time, float strength)
        {
            float outer = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.23f, .35f, Mathf.Abs(point.x)));
            float lower = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.12f, .12f, point.y));
            float side = point.x >= 0 ? 1f : -1f;
            float phase = time * (Mathf.PI * 2f / CycleSeconds) + (side > 0 ? Mathf.PI : 0f);
            Vector2 stroke = new(side * Padding.x * Mathf.Sin(phase), Padding.y * Mathf.Cos(phase));
            return point + stroke * (outer * lower * Mathf.Clamp01(strength));
        }
        public override void ModifyMesh(UnityEngine.UI.VertexHelper mesh)
        {
            if (!IsActive() || motionStrength <= 0f || mesh.currentVertCount != 4) return;
            var lo = new UIVertex(); var hi = new UIVertex();
            mesh.PopulateUIVertex(ref lo, 0); mesh.PopulateUIVertex(ref hi, 2);
            mesh.Clear();
            for (int y = 0; y <= Segments; y++)
            for (int x = 0; x <= Segments; x++)
            {
                Vector2 uv = new((float)x / Segments, (float)y / Segments);
                Vector2 p = DeformPoint(uv - Vector2.one * .5f, motionTime, motionStrength) + Vector2.one * .5f;
                var vertex = lo;
                vertex.position = new Vector3(Mathf.LerpUnclamped(lo.position.x, hi.position.x, p.x),
                    Mathf.LerpUnclamped(lo.position.y, hi.position.y, p.y), lo.position.z);
                vertex.uv0 = new Vector4(Mathf.Lerp(lo.uv0.x, hi.uv0.x, uv.x), Mathf.Lerp(lo.uv0.y, hi.uv0.y, uv.y), lo.uv0.z, lo.uv0.w);
                mesh.AddVert(vertex);
            }
            for (int y = 0; y < Segments; y++)
            for (int x = 0; x < Segments; x++)
            {
                int i = y * (Segments + 1) + x;
                mesh.AddTriangle(i, i + Segments + 1, i + 1);
                mesh.AddTriangle(i + 1, i + Segments + 1, i + Segments + 2);
            }
        }
    }
}
