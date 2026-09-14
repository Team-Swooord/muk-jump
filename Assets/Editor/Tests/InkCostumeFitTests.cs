using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class InkCostumeFitTests
    {
        [Test] public void NewRewardsKeepOldIdsAndRoundTripHighBitsAndGameCenterIds()
        {
            Assert.That((int)InkSkinId.OrigamiWings,Is.EqualTo(35));
            var state=new SkinAchievementState { gameplayMask=3 };
            for(int i=35;i<40;i++) state.Grant((InkSkinId)i);
            var restored=JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(state));
            Assert.That(restored.IsValid,Is.True);
            for(int i=35;i<40;i++) Assert.That(restored.Has((InkSkinId)i),Is.True);
            var ids=InkAchievementCatalog.SavedProgress(restored,0).Select(x=>x.id).ToArray();
            for(int i=36;i<40;i++) Assert.That(ids,Has.Member($"mukjump.achievement.skin.{i:00}"));
            Assert.That(ids,Has.Member("mukjump.achievement.wall")); Assert.That(ids,Has.Member("mukjump.achievement.flight3"));
            Assert.That(InkSkinCatalog.IsValid((InkSkinId)40),Is.False);
        }
        static readonly InkSkinId[] AllAccessories=Enumerable.Range(1,InkSkinCatalog.Count-1).Select(i=>(InkSkinId)i).ToArray();
        [TestCaseSource(nameof(AllAccessories))]
        public void NewArtHasRealTransparentBackground(InkSkinId id)
        {
            var png=new Texture2D(2,2);
            try
            {
                Assert.That(png.LoadImage(File.ReadAllBytes("Assets/Resources/"+InkSkinCatalog.ResourcePath(id)+".png")),Is.True);
                var pixels=png.GetPixels32();
                Assert.That(pixels.Count(p=>p.a==0),Is.GreaterThan(pixels.Length*.15),id+" baked background");
                Assert.That(pixels[0].a,Is.Zero); Assert.That(pixels[^1].a,Is.Zero);
                if(id==InkSkinId.RedHeadband)
                {
                    int groups=0; bool ink=false;
                    for(int y=0;y<png.height;y++)
                    { bool current=pixels[y*png.width+png.width/2].a>128; if(current&&!ink)groups++; ink=current; }
                    Assert.That(groups,Is.EqualTo(1),"이마 한가운데에는 앞쪽 띠 한 겹만 있어야 한다.");
                }
            }
            finally { Object.DestroyImmediate(png); }
        }
        [TestCase(InkSkinId.RedShoes)] [TestCase(InkSkinId.CloudBoots)]
        public void BothShoesFollowEveryFrameAndFacingWithoutChangingBody(InkSkinId id)
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore()); SkinDebugSession.Reset();
            var host=new GameObject("ShoeFit");
            try
            {
                Assert.That(SkinDebugSession.SetAllUnlocked(true),Is.True); Assert.That(SkinDebugSession.TryEquip(id),Is.True);
                var body=host.AddComponent<SpriteRenderer>(); var skin=host.AddComponent<InkSkinView>();
                typeof(InkSkinView).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(skin,null);
                foreach(string path in new[]{"Assets/Art/Character/Player/muk_spritesheet.png","Assets/Resources/MukJump/Player/muk_spritesheet_hit_02.png"})
                foreach(var frame in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                foreach(bool flip in new[]{false,true})
                {
                    body.sprite=frame; body.flipX=flip;
                    typeof(InkSkinView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(skin,null);
                    Vector2 size=frame.rect.size/frame.pixelsPerUnit;
                    Vector2 center=(frame.rect.size*.5f-frame.pivot)/frame.pixelsPerUnit;
                    foreach(bool right in new[]{false,true})
                    {
                        var shoe=right?host.transform.Find("InkSkinRightShoe").GetComponent<SpriteRenderer>():skin.AccessoryRenderer;
                        var anchor=InkFootwearPose.Anchor(frame,right)*size; if(flip)anchor.x=-anchor.x;
                        Assert.That(Vector2.Distance(shoe.transform.localPosition,center+anchor),Is.LessThan(.0001f));
                        Assert.That(shoe.enabled,Is.True); Assert.That(shoe.flipX,Is.EqualTo(flip));
                        Assert.That(shoe.sortingOrder,Is.GreaterThan(body.sortingOrder));
                    }
                    Assert.That(body.sprite,Is.SameAs(frame)); Assert.That(host.transform.localScale,Is.EqualTo(Vector3.one));
                }
                var idle=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s=>s.name=="idle");
                var apex=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s=>s.name=="apex");
                Assert.That(InkFootwearPose.Anchor(apex,true).x,Is.LessThan(InkFootwearPose.Anchor(idle,true).x*.6f));
                SkinDebugSession.TryEquip(InkSkinId.Gat);
                typeof(InkSkinView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(skin,null);
                Assert.That(host.transform.Find("InkSkinRightShoe").GetComponent<SpriteRenderer>().enabled,Is.False);
            }
            finally { Object.DestroyImmediate(host); SkinDebugSession.Reset(); PermanentGrowthProfile.RestoreDefaultStoreForTests(); }
        }
        [Test] public void DoodleCostumesDoNotCoverOriginalIdleEyes() => VerifyEyes(AllAccessories);

        [Test, Category("SkinReworkAppearance")]
        public void ReworkedFrontLayersKeepOriginalEyesClear() => VerifyEyes(new[] {
            InkSkinId.Gat, InkSkinId.CatEars, InkSkinId.WindFan, InkSkinId.ZodiacOx,
            InkSkinId.ZodiacTiger, InkSkinId.ZodiacHorse, InkSkinId.ZodiacGoat, InkSkinId.HaetaeCrest,
            InkSkinId.ZodiacPig, InkSkinId.ZodiacRat, InkSkinId.ZodiacRabbit, InkSkinId.ZodiacDragon,
            InkSkinId.ZodiacDog, InkSkinId.ZodiacRooster });

        static void VerifyEyes(InkSkinId[] ids)
        {
            var body=new Texture2D(2,2); var eyePoints=new System.Collections.Generic.List<Vector2>();
            try
            {
                body.LoadImage(File.ReadAllBytes("Assets/Art/Character/Player/muk_spritesheet.png"));
                // 첫 프레임의 원래 흰 눈만 추려 장식의 투명 구멍과 대조한다.
                for(int y=400;y<=551;y+=3) for(int x=342;x<=685;x+=3)
                {
                    var color=body.GetPixel(x,body.height-1-y);
                    if(color.a>.78f && Mathf.Min(color.r,Mathf.Min(color.g,color.b))>.67f)
                        eyePoints.Add(new Vector2(x/1024f-.5f,.5f-y/1024f));
                }
                foreach(var id in ids)
                {
                    if(InkSkinCatalog.BehindBody(id) || InkSkinCatalog.IsFootwear(id)) continue;
                    var art=new Texture2D(2,2);
                    try
                    {
                        art.LoadImage(File.ReadAllBytes("Assets/Resources/"+InkSkinCatalog.ResourcePath(id)+".png"));
                        float width=InkSkinCatalog.Width(id), height=width*art.height/art.width;
                        var offset=InkSkinCatalog.Offset(id); int covered=0;
                        foreach(var p in eyePoints)
                        {
                            float u=(p.x-offset.x)/width+.5f,v=(p.y-offset.y)/height+.5f;
                            if(u>=0 && u<=1 && v>=0 && v<=1 && art.GetPixelBilinear(u,v).a>.25f) covered++;
                        }
                        Assert.That((float)covered/eyePoints.Count,Is.LessThan(.005f),id+" covers original eyes");
                    }
                    finally { Object.DestroyImmediate(art); }
                }
            }
            finally { Object.DestroyImmediate(body); }
        }
        [Test] public void RenderEveryCostumeForFitReview()
        {
            // 실제 게임의 원화와 공통 앵커로 만든 검수용 합성. 원본 이미지 파일은 바꾸지 않는다.
            const int w=2000,h=880,tile=400,size=300;
            var body=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s=>s.name=="idle");
            Directory.CreateDirectory("output/qa/ink-showroom");
            for(int page=0;page<4;page++)
            {
                var target=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
                Texture2D png=null;
                try
                {
                    RenderTexture.active=target; GL.Clear(true,true,InkPalette.Paper); GL.PushMatrix();
                    try
                    {
                        GL.LoadPixelMatrix(0,w,h,0);
                        for(int slot=0;slot<10;slot++)
                        {
                            var id=(InkSkinId)(page*10+slot); var source=InkSkinCatalog.Sprite(id);
                            var center=new Vector2(slot%5*tile+tile*.5f,slot/5*440+240);
                            void Costume()
                            {
                                if(source==null)return;
                                if(InkSkinCatalog.IsFootwear(id))
                                { for(int side=0;side<2;side++) { var r=InkFootwearPose.DrawingRect(id,source,side==1); Draw(InkFootwearPose.Part(id,side==1),center+new Vector2(r.center.x,-r.center.y)*size,r.size*size); } }
                                else
                                { float width=size*InkSkinCatalog.Width(id); Draw(source,center+Vector2.Scale(InkSkinCatalog.Offset(id)*size,new Vector2(1,-1)),new Vector2(width,width*source.rect.height/source.rect.width)); }
                            }
                            if(InkSkinCatalog.BehindBody(id))Costume(); Draw(body,center,Vector2.one*size);
                            if(!InkSkinCatalog.BehindBody(id))Costume();
                        }
                    }
                    finally { GL.PopMatrix(); }
                    png=new Texture2D(w,h,TextureFormat.RGB24,false); png.ReadPixels(new Rect(0,0,w,h),0,0); png.Apply();
                    File.WriteAllBytes($"output/qa/ink-showroom/costume-fit-{page}.png",png.EncodeToPNG());
                }
                finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); if(png!=null)Object.DestroyImmediate(png); }
            }
        }
        static void Draw(Sprite sprite,Vector2 center,Vector2 size)
        {
            Rect uv=sprite.textureRect;
            var scale=size/sprite.rect.size; var trim=uv.size*scale; var pad=sprite.textureRectOffset*scale;
            var bounds=new Rect(center-size*.5f+new Vector2(pad.x,size.y-pad.y-trim.y),trim);
            Graphics.DrawTexture(bounds,sprite.texture,new Rect(uv.x/sprite.texture.width,uv.y/sprite.texture.height,uv.width/sprite.texture.width,uv.height/sprite.texture.height),0,0,0,0);
        }
    }
}
