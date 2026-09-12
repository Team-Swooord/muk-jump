using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    /// 원화 프레임의 발바닥 접점을 사용한다. 물리 몸체와 점프에는 영향을 주지 않는다.
    public static class InkFootwearPose
    {
        public const float ArtScale = .64f;
        static readonly Vector2[] normal = {
            new(.326f,-.340f),new(.300f,-.233f),new(.295f,-.370f),new(.244f,-.356f),
            new(.175f,-.334f),new(.305f,-.352f),new(.302f,-.293f),new(.326f,-.340f) };
        static readonly Vector2[] damagedLeft = {
            new(-.343f,-.344f),new(-.323f,-.243f),new(-.349f,-.373f),new(-.277f,-.361f),
            new(-.202f,-.344f),new(-.329f,-.356f),new(-.345f,-.298f),new(-.372f,-.345f) };
        static readonly Vector2[] damagedRight = {
            new(.345f,-.344f),new(.277f,-.247f),new(.324f,-.373f),new(.245f,-.361f),
            new(.198f,-.345f),new(.304f,-.356f),new(.299f,-.298f),new(.345f,-.345f) };
        static readonly Dictionary<int,Sprite> parts = new();
        static readonly Dictionary<Sprite,(Vector2 left,Vector2 right)> frameAnchors = new();
        public static int FrameIndex(Sprite frame)
        {
            string n=frame!=null ? frame.name : "idle";
            // 피격 확대용 복제 이름에도 남아 있는 동작 토큰을 기준으로 한다.
            if(n.Contains("crouch")) return 1;
            if(n.Contains("launch")) return 2;
            if(n.Contains("rise")) return 3;
            if(n.Contains("apex")) return 4;
            if(n.Contains("fall")) return 5;
            if(n.Contains("dive")) return 6;
            if(n.Contains("land")) return 7;
            return 0;
        }
        public static Vector2 Anchor(Sprite frame,bool right)
        {
            if(frame==null) return right ? normal[0] : new Vector2(-normal[0].x,normal[0].y);
            // 분신 양발마다 네이티브 이름 문자열을 읽지 않고 프레임당 한 번만 판별한다.
            if(!frameAnchors.TryGetValue(frame,out var pose))
            {
                int index=FrameIndex(frame);
                bool damage=frame.name.Contains("hit_02") || frame.texture.name.Contains("hit_02");
                pose=damage ? (damagedLeft[index],damagedRight[index])
                    : (new Vector2(-normal[index].x,normal[index].y),normal[index]);
                frameAnchors[frame]=pose;
            }
            return right ? pose.right : pose.left;
        }
        // 붉은 버선 원화는 발끝이 안쪽을 향한다. 좌우 조각과 발목 피벗을 함께 바꾼다.
        public static bool SourceRight(InkSkinId id, bool right) => id == InkSkinId.RedShoes ? !right : right;
        public static Vector2 Pivot(InkSkinId id,bool right) => id==InkSkinId.RedShoes
            ? new Vector2(SourceRight(id,right) ? .65f : .35f,.19f) : new Vector2(right ? .42f : .58f,.20f);
        public static bool Identify(Sprite sprite,out InkSkinId id)
        {
            id=sprite!=null && sprite.name=="skin_red_shoes_v1" ? InkSkinId.RedShoes :
                sprite!=null && sprite.name=="skin_cloud_boots_v1" ? InkSkinId.CloudBoots : InkSkinId.Ink;
            return InkSkinCatalog.IsFootwear(id);
        }
        public static Rect DrawingRect(InkSkinId id,Sprite source,bool right,Sprite frame=null)
        {
            var size=new Vector2(ArtScale*.5f,ArtScale*source.rect.height/source.rect.width);
            return new Rect(Anchor(frame,right)-Vector2.Scale(Pivot(id,right),size),size);
        }
        public static Sprite Part(InkSkinId id,bool right)
        {
            int key=(int)id*2+(right?1:0);
            if(parts.TryGetValue(key,out var cached) && cached!=null) return cached;
            var source=InkSkinCatalog.Sprite(id);
            if(source==null || !InkSkinCatalog.IsFootwear(id)) return null;
            Rect r=source.rect; r.width*=.5f; if(SourceRight(id,right)) r.x+=r.width;
            cached=UnityEngine.Sprite.Create(source.texture,r,Pivot(id,right),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            cached.name=source.name+(right?"_right":"_left"); cached.hideFlags=HideFlags.HideAndDontSave;
            parts[key]=cached; return cached;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearParts()
        {
            foreach(var sprite in parts.Values) if(sprite!=null) Object.Destroy(sprite);
            parts.Clear();
            frameAnchors.Clear();
        }
    }
}
