using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    [Category("SkinAnimationRework")]
    public sealed class InkSkinAnimationReworkTests
    {
        [TestCase("spirit_halo"), TestCase("dragon_tail"), TestCase("nine_tails")]
        public void ReworkedSheetsAreActuallyTransparentAndSliceWithoutMissingPixels(string key)
        {
            Read(key, (texture, pixels) => Assert.That(pixels.Count(c => c.a < 8),
                Is.GreaterThan(pixels.Length * .50f), "가짜 투명 무늬 금지"));
            new InkAccessoryFitRegressionTests().AuthoredFramesKeepEveryPixelOnceAndNeverCutVisibleEdges(key);
        }
        [Test] public void GoldenHaloIsOneRoundRingWithoutFlamesAndWispsAreUnchanged()
        {
            Assert.That(InkSkinCatalog.Name(InkSkinId.SpiritHalo), Is.EqualTo("윤회의 고리"));
            Assert.That(InkSkinCatalog.Name(InkSkinId.BlueWisps), Is.EqualTo("도깨비불 나들이"));
            Assert.That(InkSkinCatalog.Get(InkSkinId.SpiritHalo).Goal, Is.EqualTo(30));
            Assert.That(InkSkinCatalog.Ability(InkSkinId.SpiritHalo), Is.EqualTo("윤회"));
            foreach (bool japanese in new[] { false, true })
                Assert.That(InkSkinTranslations.TryTranslate("윤회의 고리", japanese, out _), Is.True);
            Read("spirit_halo", (texture, pixels) => {
                for (int i = 0; i < 4; i++)
                {
                    Rect r = InkSpriteAnimationBuilder.SourceRect("spirit_halo", i, texture.width, texture.height);
                    var bounds = VisibleBounds(texture.width, pixels, r);
                    Assert.That(Mathf.Abs(bounds.width - bounds.height), Is.LessThan(25), "불꽃이 튀어나오지 않는 원형");
                    var center = bounds.center;
                    int inner = 0;
                    for (int y = (int)r.yMin; y < r.yMax; y++) for (int x = (int)r.xMin; x < r.xMax; x++)
                        if (pixels[y * texture.width + x].a > 32 && Vector2.Distance(new Vector2(x, y), center) < bounds.width * .35f) inner++;
                    Assert.That(inner, Is.Zero, "후광 중앙은 몸을 가리지 않는 투명 영역");
                }
            });
        }
        [Test] public void DragonTipBendsBothAboveAndBelowTheSameRoot()
        {
            Read("dragon_tail", (texture, pixels) => {
                var differences = new List<float>();
                for (int frame = 0; frame < 4; frame++)
                {
                    Rect r = InkSpriteAnimationBuilder.SourceRect("dragon_tail", frame, texture.width, texture.height);
                    var root = VisibleBounds(texture.width, pixels, new Rect(r.xMin + 55, r.yMin, 20, r.height));
                    var tip = VisibleBounds(texture.width, pixels, new Rect(r.xMin + 470, r.yMin, 115, r.height));
                    differences.Add(tip.center.y - root.center.y);
                }
                Assert.That(differences.Max(), Is.GreaterThan(80), "위로 휘는 프레임");
                Assert.That(differences.Min(), Is.LessThan(-80), "아래로 휘는 프레임");
            });
        }
        [Test] public void EachFoxFrameHasNineSeparatePinkTips()
        {
            Read("nine_tails", (texture, pixels) => {
                for (int frame = 0; frame < 4; frame++)
                {
                    Rect r = InkSpriteAnimationBuilder.SourceRect("nine_tails", frame, texture.width, texture.height);
                    int w = (int)r.width, h = (int)r.height;
                    var pink = new bool[w * h]; var queue = new Queue<int>();
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        var c = pixels[((int)r.yMin + y) * texture.width + (int)r.xMin + x];
                        pink[y * w + x] = c.a > 128 && c.r > 170 && c.g > 60 && c.g < c.r * .86f && c.b > 60 && c.b < c.r * .90f;
                    }
                    int tips = 0;
                    for (int index = 0; index < pink.Length; index++)
                    {
                        if (!pink[index]) continue;
                        pink[index] = false; queue.Enqueue(index); int area = 0;
                        while (queue.Count > 0)
                        {
                            int p = queue.Dequeue(); area++;
                            int x = p % w, y = p / w;
                            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
                        }
                        if (area > 150) tips++;
                    }
                    Assert.That(tips, Is.EqualTo(9), "구미호 프레임 " + frame);
                    void Visit(int x, int y)
                    {
                        if (x < 0 || y < 0 || x >= w || y >= h || !pink[y * w + x]) return;
                        pink[y * w + x] = false; queue.Enqueue(y * w + x);
                    }
                }
            });
        }
        [TestCase(InkSkinId.SpiritHalo), TestCase(InkSkinId.DragonTail), TestCase(InkSkinId.NineTails)]
        public void AnimationUsesFourDistinctAuthoredFramesAndTheSameThumbnail(InkSkinId id)
        {
            var clip = InkAccessoryMotion.Animation(id);
            Assert.That(clip, Is.Not.Null);
            Assert.That(InkSkinCatalog.BehindBody(id), Is.True);
            Assert.That(InkSkinCatalog.Sprite(id), Is.SameAs(clip.Frame(0)));
            Assert.That(Enumerable.Range(0, 4).Select(clip.Frame).Distinct().Count(), Is.EqualTo(4));
            for (int i = 0; i < 4; i++) Assert.That(clip.Frame(i).rect.size, Is.EqualTo(clip.Frame(0).rect.size));
        }
        static Rect VisibleBounds(int width, Color32[] pixels, Rect region)
        {
            int left = int.MaxValue, bottom = int.MaxValue, right = -1, top = -1;
            for (int y = (int)region.yMin; y < region.yMax; y++) for (int x = (int)region.xMin; x < region.xMax; x++)
                if (pixels[y * width + x].a > 128)
                { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
            Assert.That(right, Is.GreaterThanOrEqualTo(left));
            return Rect.MinMaxRect(left, bottom, right + 1, top + 1);
        }
        static void Read(string key, Action<Texture2D, Color32[]> inspect)
        {
            var texture = new Texture2D(2, 2);
            try { texture.LoadImage(File.ReadAllBytes(InkSpriteAnimationBuilder.ArtRoot + "/" + key + "_sheet.png")); inspect(texture, texture.GetPixels32()); }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
