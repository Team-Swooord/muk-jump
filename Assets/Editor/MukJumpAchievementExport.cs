using System.IO;
using System.Linq;
using MukJump.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
namespace MukJump.EditorTools
{
    /// 실제 게임의 원먹·장식으로 Apple 업적 등록 자료를 만든다. 콘솔 등록/심사 제출은 하지 않는다.
    public static class MukJumpAchievementExport
    {
        public const string Output = "output/store/game-center-achievements";
        [MenuItem("MukJump/Export Game Center Achievement Catalog")]
        public static void Export()
        {
            JObject catalog = BuildCatalog();
            Directory.CreateDirectory(Output);
            for (int i = 1; i <= InkAchievementCatalog.GameCenterCount; i++)
                ExportIcon(i, Path.Combine(Output, InkAchievementCatalog.Id(i) + ".png"));
            File.WriteAllText(Path.Combine(Output,"catalog.json"), catalog.ToString()+"\n");
            Debug.Log($"[Achievements] {InkAchievementCatalog.GameCenterCount}개 의상 해금 / 한·영·일 / RGB 1024 아이콘 내보내기 완료. Apple 등록은 별도입니다.");
        }
        public static JObject BuildCatalog()
        {
            var all = new JArray();
            for (int i=1;i<=InkAchievementCatalog.GameCenterCount;i++)
            {
                string name=InkAchievementCatalog.Name(i), requirement=InkAchievementCatalog.Requirement(i);
                var locales=new JArray();
                foreach(string locale in new[]{"ko","en-US","ja"})
                {
                    string title=name, description=requirement;
                    if(locale!="ko")
                    {
                        if(!InkSkinTranslations.TryTranslate(name,locale=="ja",out title) ||
                           !InkSkinTranslations.TryTranslate(requirement,locale=="ja",out description))
                            throw new System.InvalidOperationException("업적 번역 누락: "+name);
                    }
                    locales.Add(new JObject { ["locale"]=locale,["name"]=title,
                        ["beforeEarnedDescription"]=description,["afterEarnedDescription"]=description });
                }
                string icon=InkAchievementCatalog.Id(i)+".png";
                all.Add(new JObject { ["id"]=InkAchievementCatalog.Id(i),["referenceName"]=name,
                    ["points"]=Points(i),["hidden"]=InkAchievementCatalog.Hidden(i),["repeatable"]=false,
                    ["icon"]=icon,["localizations"]=locales });
            }
            return new JObject { ["appId"]="6804614765",["registrationStatus"]="NOT_REGISTERED",
                ["totalPoints"]=Enumerable.Range(1, InkAchievementCatalog.GameCenterCount).Sum(Points),["achievements"]=all };
        }
        // 기존 45개 배점은 유지한다. 추가 히든까지 Apple의 총 1,000점 한도 안에 둔다.
        public static int Points(int index) => index < InkMythicSkins.First ? 20 : 5;
        static void ExportIcon(int index,string path)
        {
            var id=(InkSkinId)index;
            var body=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s=>s.name=="idle");
            var accessory=InkSkinCatalog.Sprite(id);
            var target=RenderTexture.GetTemporary(1024,1024,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Texture2D png=null;
            try
            {
                RenderTexture.active=target;
                GL.Clear(true,true,InkPalette.Paper);
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0,1024,1024,0);
                    Rect bodyBounds = IconBodyRect(id, accessory);
                    float size = bodyBounds.width;
                    Vector2 center = bodyBounds.center;
                    void Costume()
                    {
                        if(accessory==null) return;
                        if(InkSkinCatalog.IsFootwear(id))
                        {
                            for(int side=0;side<2;side++)
                            {
                                Rect r=InkFootwearPose.DrawingRect(id,accessory,side==1);
                                Vector2 p=center+new Vector2(r.center.x,-r.center.y)*size;
                                Draw(InkFootwearPose.Part(id,side==1),new Rect(p-r.size*size*.5f,r.size*size));
                            }
                            return;
                        }
                        float width=size*InkSkinCatalog.Width(id);
                        var position=center+Vector2.Scale(InkSkinCatalog.Offset(id)*size,new Vector2(1,-1));
                        var bounds=new Vector2(width,width*accessory.rect.height/accessory.rect.width);
                        Draw(accessory,new Rect(position-bounds*.5f,bounds));
                    }
                    if(InkSkinCatalog.BehindBody(id)) Costume();
                    Draw(body,bodyBounds);
                    if(!InkSkinCatalog.BehindBody(id)) Costume();
                }
                finally { GL.PopMatrix(); }
                png=new Texture2D(1024,1024,TextureFormat.RGB24,false);
                png.ReadPixels(new Rect(0,0,1024,1024),0,0); png.Apply();
                File.WriteAllBytes(path,png.EncodeToPNG());
            }
            finally
            { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); if(png!=null) Object.DestroyImmediate(png); }
        }
        public static Rect IconBodyRect(InkSkinId id, Sprite accessory)
        {
            // 긴 꼬리·날개도 둥근 Game Center 배지 안에 들어오도록 실제 의상 외곽을 중앙에 맞춘다.
            Rect bounds = LobbySkinPreview.CostumeBounds(id, accessory);
            float size = 700f / Mathf.Max(bounds.width, bounds.height);
            Vector2 center = Vector2.one * 512f - Vector2.Scale(bounds.center, new Vector2(1, -1)) * size;
            return new Rect(center - Vector2.one * size * .5f, Vector2.one * size);
        }
        static void Draw(Sprite sprite,Rect bounds)
        {
            Rect r=sprite.textureRect; var texture=sprite.texture;
            var scale=bounds.size/sprite.rect.size; var trim=r.size*scale; var pad=sprite.textureRectOffset*scale;
            bounds=new Rect(bounds.position+new Vector2(pad.x,bounds.height-pad.y-trim.y),trim);
            Graphics.DrawTexture(bounds,texture,new Rect(r.x/texture.width,r.y/texture.height,r.width/texture.width,r.height/texture.height),0,0,0,0);
        }
    }
}
