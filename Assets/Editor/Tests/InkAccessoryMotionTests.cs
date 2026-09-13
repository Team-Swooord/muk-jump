using System.Collections.Generic;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class InkAccessoryMotionTests
    {
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
        [Test] public void EveryMovingAccessoryUsesFourDifferentAuthoredFrames()
        {
            var random = Random.state;
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i;
                if (!InkAccessoryMotion.Supports(id)) continue;
                var clip = InkAccessoryMotion.Animation(id);
                Assert.That(clip, Is.Not.Null, id.ToString());
                Assert.That(clip.FrameCount, Is.EqualTo(4));
                for (int frame = 0; frame < 4; frame++)
                {
                    var sprite = clip.Frame(frame);
                    Assert.That(sprite, Is.Not.Null);
                    Assert.That(sprite, Is.SameAs(clip.Sample((frame + .1f) / clip.FramesPerSecond)));
                    Assert.That(sprite, Is.Not.SameAs(clip.Frame((frame + 1) % 4)));
                    Assert.That(sprite.texture, Is.SameAs(clip.Frame(0).texture));
                    Assert.That(sprite.rect.size, Is.EqualTo(clip.Frame(0).rect.size));
                    Assert.That(sprite.vertices.Length, Is.EqualTo(4));
                }
                Assert.That(clip.Sample(clip.Duration), Is.SameAs(clip.Frame(0)));
                Assert.That(clip.Sample(float.NaN), Is.SameAs(clip.Frame(0)));
                Assert.That(clip.Sample(1.3f, false), Is.SameAs(clip.Frame(0)));
                Assert.That(InkAccessoryMotion.Animation(id), Is.SameAs(clip));
            }
            Assert.That(Random.state, Is.EqualTo(random));
            Assert.That(InkAccessoryMotion.Supports(InkSkinId.Gat), Is.False);
            Assert.That(InkAccessoryMotion.Supports(InkSkinId.RedShoes), Is.False);
        }
        [Test] public void UiChangesSpriteReferencesNotMeshShapeAndRestoresStaticArt()
        {
            var ui = Make("UI").AddComponent<InkAccessoryImage>();
            foreach (var id in new[] { InkSkinId.BlueWisps, InkSkinId.SpiritHalo, InkSkinId.DragonTail, InkSkinId.CraneWings })
            {
                ui.sprite = InkSkinCatalog.Sprite(id);
                ui.rectTransform.sizeDelta = new Vector2(400, 400 * ui.sprite.rect.height / ui.sprite.rect.width);
                var clip = InkAccessoryMotion.Animation(id);
                Vector3[] first = null;
                for (int f = 0; f < 4; f++)
                {
                    ui.PresentMotion(id, (f + .1f) / clip.FramesPerSecond, 1);
                    Assert.That(ui.HasMotion, Is.True);
                    Assert.That(ui.overrideSprite, Is.SameAs(clip.Frame(f)));
                    using var vh = new VertexHelper();
                    typeof(InkAccessoryImage).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(ui, new object[] { vh });
                    Assert.That(vh.currentVertCount, Is.EqualTo(4));
                    var points = new Vector3[4]; var vertex = new UIVertex();
                    for (int v = 0; v < 4; v++) { vh.PopulateUIVertex(ref vertex, v); points[v] = vertex.position; }
                    if (first == null) first = points; else Assert.That(points, Is.EqualTo(first), "그림만 교체하고 꼭짓점은 움직이지 않는다.");
                }
                ui.PresentMotion(id, 1, 0); Assert.That(ui.HasMotion, Is.False);
                Assert.That(ui.overrideSprite, Is.SameAs(ui.sprite));
            }
        }
        [Test] public void HeldFramesHaveDistinctFeetAndKeepAllFootwearInsideDragBounds()
        {
            var clip = InkAccessoryMotion.Held;
            Assert.That(clip, Is.Not.Null); Assert.That(clip.FrameCount, Is.EqualTo(4));
            foreach (var id in new[] { InkSkinId.RedShoes, InkSkinId.CloudBoots })
            {
                var source = InkSkinCatalog.Sprite(id);
                var bounds = LobbySkinPreview.CostumeBounds(id, source);
                for (int f = 0; f < 4; f++)
                {
                    var frame = clip.Frame(f);
                    Assert.That(InkFootwearPose.Anchor(frame, false), Is.Not.EqualTo(InkFootwearPose.Anchor(null, false)));
                    foreach (bool right in new[] { false, true })
                    {
                        var rect = InkFootwearPose.DrawingRect(id, source, right, frame);
                        Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(bounds.xMin));
                        Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(bounds.yMin));
                        Assert.That(rect.xMax, Is.LessThanOrEqualTo(bounds.xMax));
                        Assert.That(rect.yMax, Is.LessThanOrEqualTo(bounds.yMax));
                    }
                }
            }
        }
    }
}
