using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 신발 쌍만 두 사각형으로 그린다. UI도 게임과 같은 발 앵커·크기를 사용한다.
    public sealed class InkAccessoryImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if(!InkFootwearPose.Identify(sprite,out var id)) { base.OnPopulateMesh(mesh); return; }
            mesh.Clear(); float bodySize=rectTransform.rect.width/InkSkinCatalog.Width(id);
            Rect uv=sprite.textureRect;
            uv=new Rect(uv.x/sprite.texture.width,uv.y/sprite.texture.height,uv.width/sprite.texture.width,uv.height/sprite.texture.height);
            for(int side=0;side<2;side++)
            {
                Rect r=InkFootwearPose.DrawingRect(id,sprite,side==1);
                r.position=(r.position-InkSkinCatalog.Offset(id))*bodySize; r.size*=bodySize;
                float left=uv.x+side*uv.width*.5f,right=left+uv.width*.5f;
                int start=mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin,r.yMin),color,new Vector2(left,uv.yMin));
                mesh.AddVert(new Vector3(r.xMin,r.yMax),color,new Vector2(left,uv.yMax));
                mesh.AddVert(new Vector3(r.xMax,r.yMax),color,new Vector2(right,uv.yMax));
                mesh.AddVert(new Vector3(r.xMax,r.yMin),color,new Vector2(right,uv.yMin));
                mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start+2,start+3,start);
            }
        }
    }
}
