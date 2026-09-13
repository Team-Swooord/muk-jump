using System.Collections.Generic;
using System.IO;
using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTests
{
    [Category("NaturalAccessoryArt")]
    public sealed class InkNaturalAccessoryTests
    {
        public static readonly InkSkinId[] Changed = {
            InkSkinId.ZodiacRat, InkSkinId.ZodiacOx, InkSkinId.ZodiacRabbit,
            InkSkinId.ZodiacDragon, InkSkinId.ZodiacSnake, InkSkinId.ZodiacHorse,
            InkSkinId.ZodiacGoat, InkSkinId.ZodiacMonkey, InkSkinId.ZodiacRooster,
            InkSkinId.ZodiacDog, InkSkinId.ZodiacPig, InkSkinId.HaetaeCrest };
        public static bool Paired(InkSkinId id) => id is InkSkinId.ZodiacRat or InkSkinId.ZodiacOx
            or InkSkinId.ZodiacRabbit or InkSkinId.ZodiacDragon or InkSkinId.ZodiacGoat
            or InkSkinId.ZodiacMonkey or InkSkinId.ZodiacDog or InkSkinId.ZodiacPig;

        [TestCaseSource(nameof(Changed))]
        public void NaturalShapesHaveRealTransparencyAndNoConnectingBand(InkSkinId id)
        {
            string path = "Assets/Resources/" + InkSkinCatalog.ResourcePath(id) + ".png";
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True);
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(p => p.a < 8) / (float)pixels.Length, Is.GreaterThan(.45f), "배경은 실제 알파여야 한다");
                Assert.That(pixels.Count(p => p.a > 128 && p.r < 80 && p.g < 80 && p.b < 80),
                    Is.GreaterThan(pixels.Length * .01f), "검은 낙서 윤곽을 유지한다");
                var parts = SignificantParts(pixels, texture.width, texture.height);
                Assert.That(parts.Count, Is.EqualTo(Paired(id) ? 2 : 1), "띠·별도 장식 없이 뿔/귀/갈기만 남긴다");
                if (Paired(id))
                {
                    int centerInk = 0;
                    for (int y = 0; y < texture.height; y++)
                        for (int x = texture.width * 47 / 100; x < texture.width * 53 / 100; x++)
                            if (pixels[y * texture.width + x].a > 128) centerInk++;
                    Assert.That(centerInk, Is.LessThan(20), "중앙의 연결 띠가 다시 생기지 않는다");
                    Assert.That(InkSkinCatalog.BehindBody(id), Is.True, "뿌리는 원래 몸체 뒤에 묻힌다");
                }
                foreach (var part in parts)
                {
                    float width = InkSkinCatalog.Width(id);
                    var bottom = new Vector2(part.xMin / texture.width - .5f, part.yMin / texture.height - .5f) * width;
                    var size = new Vector2(part.width / texture.width, part.height / texture.height) * width;
                    var fitted = new Rect(bottom + InkSkinCatalog.Offset(id), size);
                    Assert.That(fitted.Overlaps(LobbySkinPreview.BodyDrawingBounds), Is.True, "각 장식은 먹과 접점이 있다");
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(importer.maxTextureSize, Is.EqualTo(512));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
                Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null);
                Assert.That(InkSkinCatalog.HasAbility(id), Is.False, "외형 변경은 능력을 추가하지 않는다");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static List<Rect> SignificantParts(Color32[] pixels, int width, int height)
        {
            var visited = new bool[pixels.Length]; var queue = new Queue<int>(); var parts = new List<Rect>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (visited[i] || pixels[i].a <= 128) continue;
                int count = 0, x0 = width, y0 = height, x1 = 0, y1 = 0;
                visited[i] = true; queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue(), x = p % width, y = p / width; count++;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    void Visit(int n) { if (!visited[n] && pixels[n].a > 128) { visited[n] = true; queue.Enqueue(n); } }
                    if (x > 0) Visit(p - 1); if (x < width - 1) Visit(p + 1);
                    if (y > 0) Visit(p - width); if (y < height - 1) Visit(p + width);
                }
                // 가장자리의 작은 목탄 점은 형태 개수에 포함하지 않는다.
                if (count > pixels.Length * .005f) parts.Add(Rect.MinMaxRect(x0, y0, x1 + 1, y1 + 1));
            }
            return parts;
        }
    }
}
