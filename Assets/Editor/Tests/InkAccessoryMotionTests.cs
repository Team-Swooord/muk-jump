using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class InkAccessoryMotionTests
    {
        static readonly InkSkinId[] animated = { InkSkinId.SpiritHalo, InkSkinId.BlueWisps, InkSkinId.PaperLanterns,
            InkSkinId.MoonClouds, InkSkinId.CraneWings, InkSkinId.ButterflyWings, InkSkinId.OrigamiWings, InkSkinId.StormScarf };
        readonly List<Object> owned = new();
        [TearDown] public void Cleanup() { for (int i=owned.Count-1; i>=0; i--) if(owned[i]!=null) Object.DestroyImmediate(owned[i]); owned.Clear(); }
        GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); owned.Add(go); return go;
        }
        [Test] public void NewHalosAppendIdsAndHaveLocalizedGoalsWithoutChangingAbilities()
        {
            Assert.That((int)InkSkinId.CloudBoots, Is.EqualTo(39)); Assert.That(InkSkinCatalog.Count, Is.GreaterThanOrEqualTo(43));
            foreach(var id in new[]{InkSkinId.BlueWisps, InkSkinId.PaperLanterns, InkSkinId.MoonClouds})
            {
                Assert.That(InkSkinCatalog.BehindBody(id), Is.True); Assert.That(InkSkinCatalog.HasAbility(id), Is.False);
                Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null);
                var lore = InkSkinLore.Get(id);
                foreach(string source in new[]{InkSkinCatalog.Name(id), lore.ko, InkSkinAchievements.Requirement(id)})
                    foreach(bool japanese in new[]{false,true})
                    { Assert.That(InkSkinTranslations.TryTranslate(source,japanese,out var result),Is.True); Assert.That(result,Is.Not.Empty); }
                var state=new SkinAchievementState(); state.Grant(id);
                Assert.That(JsonUtility.FromJson<SkinAchievementState>(JsonUtility.ToJson(state)).Has(id),Is.True);
                Assert.That(state.IsValid,Is.True);
            }
            var progress=new SkinAchievementState { inkDrops=19, items=19, runs=6, nightBest=199 };
            Assert.That(progress.Progress(InkSkinId.BlueWisps,0,0),Is.EqualTo(19));
            progress.Settle(200,10,new SkinRunStats { version=1,inkDrops=1,items=1,maxLivingPlayers=1,startedAtNight=true },200,0);
            foreach(var id in new[]{InkSkinId.BlueWisps, InkSkinId.PaperLanterns, InkSkinId.MoonClouds}) Assert.That(progress.Has(id),Is.True);
        }
        [Test] public void MotionIsBoundedContinuousAndDoesNotUseGameRandom()
        {
            var random=Random.state;
            foreach(var id in animated)
            {
                float travel=0;
                for(int t=0;t<80;t++) for(int x=0;x<=4;x++) for(int y=0;y<=4;y++)
                {
                    var uv=new Vector2(x/4f,y/4f); var p=InkAccessoryMotion.Deform(id,uv,t*.1f,1);
                    var next=InkAccessoryMotion.Deform(id,uv,t*.1f+1f/60,1);
                    Assert.That(Mathf.Abs(p.x-uv.x),Is.LessThanOrEqualTo(InkAccessoryMotion.Padding+.00001f));
                    Assert.That(Mathf.Abs(p.y-uv.y),Is.LessThanOrEqualTo(InkAccessoryMotion.Padding+.00001f));
                    Assert.That(Vector2.Distance(p,next),Is.LessThan(.006f)); travel+=Vector2.Distance(p,next);
                    Assert.That(InkAccessoryMotion.Deform(id,uv,t,0),Is.EqualTo(uv));
                }
                Assert.That(travel,Is.GreaterThan(.01f),id.ToString());
            }
            Assert.That(Random.state,Is.EqualTo(random));
            Assert.That(InkAccessoryMotion.Supports(InkSkinId.Gat),Is.False);
            Assert.That(InkAccessoryMotion.Supports(InkSkinId.RedShoes),Is.False);
        }
        [Test] public void WingRootsAndScarfForeheadStayAttached()
        {
            for(int t=0;t<100;t++)
            {
                foreach(var id in new[]{InkSkinId.CraneWings,InkSkinId.ButterflyWings,InkSkinId.OrigamiWings})
                    Assert.That(InkAccessoryMotion.Deform(id,new Vector2(.5f,.5f),t*.1f,1),Is.EqualTo(new Vector2(.5f,.5f)));
                var forehead=new Vector2(.3f,.6f);
                Assert.That(InkAccessoryMotion.Deform(InkSkinId.StormScarf,forehead,t*.1f,1),Is.EqualTo(forehead));
            }
        }
        [Test] public void UiAndWorldUseSameWarpAndReuseTheirMeshes()
        {
            var go=Make("Source"); var source=go.AddComponent<SpriteRenderer>();
            var draw=Make("Motion",go.transform).AddComponent<InkAccessoryMesh>();
            var ui=Make("UI").AddComponent<InkAccessoryImage>(); ui.rectTransform.sizeDelta=Vector2.one*400; ui.raycastTarget=false;
            foreach(var id in animated)
            {
                source.sprite=ui.sprite=InkSkinCatalog.Sprite(id);
                ui.rectTransform.sizeDelta=new Vector2(400,400*source.sprite.rect.height/source.sprite.rect.width);
                Assert.That(ui.HasMotion,Is.False);
                ui.PresentMotion(id,.7f,1); Assert.That(ui.HasMotion,Is.True);
                Assert.That(draw.Present(source,id,.7f,1),Is.True);
                var mesh=draw.Mesh; var material=draw.Renderer.sharedMaterial;
                using(var vh=new VertexHelper())
                {
                    typeof(InkAccessoryImage).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(ui,new object[]{vh});
                    Assert.That(vh.currentVertCount,Is.EqualTo(mesh.vertexCount));
                    var points=mesh.vertices; var v=new UIVertex();
                    for(int i=0;i<points.Length;i++)
                    {
                        vh.PopulateUIVertex(ref v,i);
                        Vector2 normalizedWorld=Vector2.Scale((Vector2)points[i]*source.sprite.pixelsPerUnit+source.sprite.pivot,
                            new Vector2(1/source.sprite.rect.width,1/source.sprite.rect.height));
                        var r=ui.rectTransform.rect;
                        Vector2 normalizedUi=new((v.position.x-r.xMin)/r.width,(v.position.y-r.yMin)/r.height);
                        Assert.That(Vector2.Distance(normalizedWorld,normalizedUi),Is.LessThan(.0001f));
                    }
                }
                source.flipX=true; source.flipY=true; source.color=Color.red; source.sortingOrder=17;
                draw.Present(source,id,1.3f,.55f);
                Assert.That(draw.Mesh,Is.SameAs(mesh)); Assert.That(draw.Renderer.sharedMaterial,Is.SameAs(material));
                Assert.That(draw.transform.localScale,Is.EqualTo(new Vector3(-1,-1,1))); Assert.That(draw.Renderer.sortingOrder,Is.EqualTo(17));
                ui.PresentMotion(id,1,0); Assert.That(ui.HasMotion,Is.False);
            }
            draw.Hide(); Assert.That(draw.Renderer.enabled,Is.False);
        }
        [Test] public void ClonedRenderersNeverShareMutableMeshOrMaterial()
        {
            var source=Make("Source").AddComponent<SpriteRenderer>(); source.sprite=InkSkinCatalog.Sprite(InkSkinId.BlueWisps);
            var original=Make("Motion",source.transform).AddComponent<InkAccessoryMesh>(); original.Present(source,InkSkinId.BlueWisps,0,1);
            var clone=Object.Instantiate(original.gameObject); owned.Add(clone);
            var other=clone.GetComponent<InkAccessoryMesh>(); other.Present(source,InkSkinId.BlueWisps,1,1);
            Assert.That(other.Mesh,Is.Not.SameAs(original.Mesh)); Assert.That(other.Renderer.sharedMaterial,Is.Not.SameAs(original.Renderer.sharedMaterial));
        }
    }
}
