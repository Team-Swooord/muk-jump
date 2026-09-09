using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 실제 월드/UI가 보이는 둥근 구멍. 별도 카메라·텍스처·스텐실 없이 작은 메시만 갱신한다.
    /// 구멍도 Raycast는 받아 안내를 넘기는 탭이 뒤쪽 조작으로 새지 않게 한다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialSpotlightGraphic : MaskableGraphic
    {
        public TutorialSpotlightGraphic() => useLegacyMeshGeneration = false;

        Rect focus;
        float cornerRadius;
        public Rect FocusRect => focus;

        public void SetFocus(Rect value, float radius)
        {
            if (focus == value && Mathf.Approximately(cornerRadius, radius)) return;
            focus = value;
            cornerRadius = radius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect outer = rectTransform.rect;
            if (focus.width <= 0 || focus.height <= 0) { Quad(mesh, outer); return; }
            Rect hole = Rect.MinMaxRect(Mathf.Clamp(focus.xMin, outer.xMin, outer.xMax),
                Mathf.Clamp(focus.yMin, outer.yMin, outer.yMax),
                Mathf.Clamp(focus.xMax, outer.xMin, outer.xMax),
                Mathf.Clamp(focus.yMax, outer.yMin, outer.yMax));
            Quad(mesh, Rect.MinMaxRect(outer.xMin, outer.yMin, outer.xMax, hole.yMin));
            Quad(mesh, Rect.MinMaxRect(outer.xMin, hole.yMax, outer.xMax, outer.yMax));
            Quad(mesh, Rect.MinMaxRect(outer.xMin, hole.yMin, hole.xMin, hole.yMax));
            Quad(mesh, Rect.MinMaxRect(hole.xMax, hole.yMin, outer.xMax, hole.yMax));
            float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(hole.width, hole.height) * .5f);
            Corner(mesh, new Vector2(hole.xMax, hole.yMax), new Vector2(-radius, -radius), radius, 0f);
            Corner(mesh, new Vector2(hole.xMin, hole.yMax), new Vector2(radius, -radius), radius, 90f);
            Corner(mesh, new Vector2(hole.xMin, hole.yMin), new Vector2(radius, radius), radius, 180f);
            Corner(mesh, new Vector2(hole.xMax, hole.yMin), new Vector2(-radius, radius), radius, 270f);
        }

        void Quad(VertexHelper mesh, Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        void Corner(VertexHelper mesh, Vector2 corner, Vector2 offset, float radius, float startAngle)
        {
            if (radius <= 0f) return;
            int start = mesh.currentVertCount;
            mesh.AddVert(corner, color, Vector2.zero);
            Vector2 center = corner + offset;
            const int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + 90f * i / segments) * Mathf.Deg2Rad;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i + 1, start + i);
            }
        }
    }
}
