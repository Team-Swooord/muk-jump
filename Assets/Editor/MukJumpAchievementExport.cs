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
            Directory.CreateDirectory(Output);
            var all = new JArray();
            for (int i=1;i<=InkAchievementCatalog.Count;i++)
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
                ExportIcon(i,Path.Combine(Output,icon));
                all.Add(new JObject { ["id"]=InkAchievementCatalog.Id(i),["referenceName"]=name,
                    ["points"]=20,["hidden"]=InkAchievementCatalog.Hidden(i),["repeatable"]=false,
                    ["icon"]=icon,["localizations"]=locales });
            }
            File.WriteAllText(Path.Combine(Output,"catalog.json"),new JObject {
                ["appId"]="6804614765",["registrationStatus"]="NOT_REGISTERED",["totalPoints"]=740,["achievements"]=all }.ToString()+"\n");
            Debug.Log("[Achievements] 37개 업적 / 한·영·일 / RGB 1024 아이콘 내보내기 완료. Apple 등록은 별도입니다.");
        }
        static void ExportIcon(int index,string path)
        {
            var id=index<InkSkinCatalog.Count?(InkSkinId)index:index==36?InkSkinId.SpiderMask:InkSkinId.CatEars;
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
                    const float size=740;
                    var center=new Vector2(512,585);
                    void Costume()
                    {
                        if(accessory==null) return;
                        float width=size*InkSkinCatalog.Width(id);
                        var position=center+Vector2.Scale(InkSkinCatalog.Offset(id)*size,new Vector2(1,-1));
                        var bounds=new Vector2(width,width*accessory.rect.height/accessory.rect.width);
                        Draw(accessory,new Rect(position-bounds*.5f,bounds));
                    }
                    if(InkSkinCatalog.BehindBody(id)) Costume();
                    Draw(body,new Rect(center-Vector2.one*size*.5f,Vector2.one*size));
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
        static void Draw(Sprite sprite,Rect bounds)
        {
            Rect r=sprite.textureRect; var texture=sprite.texture;
            Graphics.DrawTexture(bounds,texture,new Rect(r.x/texture.width,r.y/texture.height,r.width/texture.width,r.height/texture.height),0,0,0,0);
        }
    }
}
