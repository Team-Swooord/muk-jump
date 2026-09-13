using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 장식은 원화 프레임 네 꼭짓점, 신발만 두 발의 원화 앵커에 각각 그린다.
    public sealed class InkAccessoryImage : Image
    {
        float motionTime;
        bool hasMotion;
        Sprite bodyFrame;
        public float MotionTime => motionTime;
        public bool HasMotion => hasMotion;
        public void PresentBodyFrame(Sprite frame)
        {
            if (bodyFrame == frame) return;
            bodyFrame = frame;
            if (InkFootwearPose.Identify(sprite, out _)) SetVerticesDirty();
        }
        // 소유 화면만 시계를 진행한다. 목록·리더보드의 작은 썸네일은 정지 상태다.
        public void PresentMotion(InkSkinId id, float time, float strength)
        {
            motionTime = time;
            hasMotion = strength > 0f && InkAccessoryMotion.Supports(id);
            overrideSprite = hasMotion ? InkAccessoryMotion.Sample(id, time) : null;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (!InkFootwearPose.Identify(sprite, out var id)) { base.OnPopulateMesh(mesh); return; }
            mesh.Clear(); float bodySize = rectTransform.rect.width / InkSkinCatalog.Width(id);
            Rect uv = sprite.textureRect;
            uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height,
                uv.width / sprite.texture.width, uv.height / sprite.texture.height);
            for (int side = 0; side < 2; side++)
            {
                Rect r = InkFootwearPose.DrawingRect(id, sprite, side == 1, bodyFrame);
                r.position = (r.position - InkSkinCatalog.Offset(id)) * bodySize; r.size *= bodySize;
                int sourceSide = InkFootwearPose.SourceRight(id, side == 1) ? 1 : 0;
                float left = uv.x + sourceSide * uv.width * .5f, right = left + uv.width * .5f;
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin, r.yMin), color, new Vector2(left, uv.yMin));
                mesh.AddVert(new Vector3(r.xMin, r.yMax), color, new Vector2(left, uv.yMax));
                mesh.AddVert(new Vector3(r.xMax, r.yMax), color, new Vector2(right, uv.yMax));
                mesh.AddVert(new Vector3(r.xMax, r.yMin), color, new Vector2(right, uv.yMin));
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start + 2, start + 3, start);
            }
        }
        protected override void OnDisable() { overrideSprite = null; hasMotion = false; base.OnDisable(); }
    }
}
