using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 신발 쌍만 두 사각형으로 그린다. UI도 게임과 같은 발 앵커·크기를 사용한다.
    public sealed class InkAccessoryImage : Image
    {
        InkSkinId motionId;
        float motionTime, motionStrength;
        float swimTime, swimStrength;
        public float MotionTime => motionTime;
        public bool HasMotion => motionStrength > 0f && InkAccessoryMotion.Supports(motionId);
        // 로비 미리보기에서만 사용한다. 나머지 UI의 신발 앵커와 렌더 비용은 유지한다.
        public void PresentSwim(float time, float strength)
        {
            time = InkAccessoryMotion.SampleTime(time);
            strength = InkFootwearPose.Identify(sprite, out _) ? Mathf.Clamp01(strength) : 0f;
            if (swimTime == time && swimStrength == strength) return;
            bool redraw = strength > 0f || swimStrength > 0f;
            swimTime = time; swimStrength = strength;
            if (redraw) SetVerticesDirty();
        }
        // 큰 미리보기 소유자가 명시적으로 호출한다. 목록·리더보드에는 Update 자체가 없다.
        public void PresentMotion(InkSkinId id, float time, float strength)
        {
            time = InkAccessoryMotion.SampleTime(time);
            strength = InkAccessoryMotion.Supports(id) ? Mathf.Clamp01(strength) : 0f;
            if (motionId == id && motionTime == time && motionStrength == strength) return;
            bool redraw = strength > 0f || motionStrength > 0f;
            motionId = id; motionTime = time; motionStrength = strength;
            if (redraw) SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (HasMotion && sprite != null) { PopulateMotion(mesh); return; }
            if(!InkFootwearPose.Identify(sprite,out var id)) { base.OnPopulateMesh(mesh); return; }
            mesh.Clear(); float bodySize=rectTransform.rect.width/InkSkinCatalog.Width(id);
            Rect uv=sprite.textureRect;
            uv=new Rect(uv.x/sprite.texture.width,uv.y/sprite.texture.height,uv.width/sprite.texture.width,uv.height/sprite.texture.height);
            for(int side=0;side<2;side++)
            {
                Rect r=InkFootwearPose.DrawingRect(id,sprite,side==1);
                Vector2 foot = InkFootwearPose.Anchor(null, side == 1);
                r.position += LobbyInkSwimMotion.DeformPoint(foot, swimTime, swimStrength) - foot;
                r.position=(r.position-InkSkinCatalog.Offset(id))*bodySize; r.size*=bodySize;
                int sourceSide=InkFootwearPose.SourceRight(id,side==1) ? 1 : 0;
                float left=uv.x+sourceSide*uv.width*.5f,right=left+uv.width*.5f;
                int start=mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin,r.yMin),color,new Vector2(left,uv.yMin));
                mesh.AddVert(new Vector3(r.xMin,r.yMax),color,new Vector2(left,uv.yMax));
                mesh.AddVert(new Vector3(r.xMax,r.yMax),color,new Vector2(right,uv.yMax));
                mesh.AddVert(new Vector3(r.xMax,r.yMin),color,new Vector2(right,uv.yMin));
                mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start+2,start+3,start);
            }
        }
        void PopulateMotion(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = GetPixelAdjustedRect(), texture = sprite.textureRect;
            const int n = InkAccessoryMotion.Segments;
            for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++)
            {
                Vector2 uv = new((float)x/n, (float)y/n);
                Vector2 p = InkAccessoryMotion.Deform(motionId, uv, motionTime, motionStrength);
                mesh.AddVert(new Vector3(r.xMin + p.x*r.width, r.yMin + p.y*r.height), color,
                    new Vector2((texture.x+uv.x*texture.width)/sprite.texture.width, (texture.y+uv.y*texture.height)/sprite.texture.height));
            }
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = y*(n+1)+x;
                mesh.AddTriangle(i, i+n+1, i+1); mesh.AddTriangle(i+1, i+n+1, i+n+2);
            }
        }
    }
}
