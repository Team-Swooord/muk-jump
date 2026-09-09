using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthArtTests
    {
        const string ArtRoot =
            "Assets/Resources/MukJump/UI/PermanentGrowth/";

        static readonly string[] SpriteNames =
        {
            "pg_hanji_background",
            "pg_tree_trunk",
            "pg_tree_background_v3",
            "pg_branch",
            "pg_node_bud",
            "pg_node_bloom_mask",
            "pg_selected_ring",
            "pg_hanji_card",
            "pg_root_emblem",
            "pg_inklight_sumukhwa_v1",
            "pg_icon_capacity",
            "pg_icon_recovery",
            "pg_icon_platform",
            "pg_icon_jump",
        };

        GameObject managerHost;
        GameObject viewHost;

        [SetUp]
        public void SetUp()
        {
            var store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            PermanentGrowthProfile.DebugRefillCurrency();

            managerHost = new GameObject("PermanentGrowthArtManager");
            managerHost.AddComponent<GameManager>();
            viewHost = new GameObject("PermanentGrowthArtView");
        }

        [TearDown]
        public void TearDown()
        {
            if (viewHost != null)
                Object.DestroyImmediate(viewHost);
            if (managerHost != null)
                Object.DestroyImmediate(managerHost);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void SpritePackUsesMobileUiImportSettings()
        {
            for (int i = 0; i < SpriteNames.Length; i++)
            {
                string path = ArtRoot + SpriteNames[i] + ".png";
                var importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(
                    importer.textureType,
                    Is.EqualTo(TextureImporterType.Sprite),
                    path);
                Assert.That(
                    importer.spriteImportMode,
                    Is.EqualTo(SpriteImportMode.Single),
                    path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(
                    settings.spriteMeshType,
                    Is.EqualTo(SpriteMeshType.FullRect),
                    path);
                Assert.That(
                    importer.spritePixelsPerUnit,
                    Is.EqualTo(100f).Within(0.001f),
                    path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(
                    importer.npotScale,
                    Is.EqualTo(TextureImporterNPOTScale.None),
                    path);
                Assert.That(
                    importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp),
                    path);
                Assert.That(
                    importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear),
                    path);
                bool large =
                    SpriteNames[i] == "pg_hanji_background" ||
                    SpriteNames[i] == "pg_tree_trunk" ||
                    SpriteNames[i].StartsWith("pg_tree_background_v");
                bool medium =
                    SpriteNames[i] == "pg_branch" ||
                    SpriteNames[i] == "pg_hanji_card";
                Assert.That(
                    importer.maxTextureSize,
                    Is.EqualTo(large ? 2048 : medium ? 1024 : 512),
                    path);
                Assert.That(
                    importer.alphaIsTransparency,
                    Is.EqualTo(SpriteNames[i] != "pg_hanji_background"),
                    path);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.CompressedHQ),
                    path);
                Assert.That(importer.compressionQuality, Is.EqualTo(100), path);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    Is.Not.Null,
                    path);
            }
        }

        [Test]
        public void MiniBrushIsTransparentAndImportedForBothGrowthIconSizes()
        {
            string path = "Assets/Resources/" + PermanentGrowthView.BrushIconResourcePath + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.maxTextureSize, Is.EqualTo(512));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(Resources.Load<Sprite>(PermanentGrowthView.BrushIconResourcePath), Is.Not.Null);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                Assert.That(source.width, Is.EqualTo(source.height));
                Color32[] pixels = source.GetPixels32();
                int clear = 0, painted = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a < 8) clear++;
                    if (pixels[i].a > 240) painted++;
                }
                Assert.That(clear, Is.GreaterThan(pixels.Length / 3), "체크무늬 배경이 아닌 실제 알파가 필요합니다.");
                Assert.That(painted, Is.GreaterThan(pixels.Length / 8), "붓 실루엣이 너무 작거나 비어 있습니다.");
                Assert.That(source.GetPixel(0, 0).a, Is.Zero);
                Assert.That(source.GetPixel(source.width - 1, source.height - 1).a, Is.Zero);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void InkLightPaintingHasRealTransparentMarginsAndDenseInkBody()
        {
            string path = ArtRoot + "pg_inklight_sumukhwa_v1.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(source.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                Assert.That(source.width, Is.EqualTo(source.height));
                // 체크무늬가 RGB에 구워진 원화를 투명 PNG로 잘못 채택하지 않는다.
                Color32[] pixels = source.GetPixels32();
                int edge = source.width - 1;
                for (int i = 0; i < source.width; i++)
                {
                    Assert.That(pixels[i].a, Is.LessThanOrEqualTo(1));
                    Assert.That(pixels[edge * source.width + i].a, Is.LessThanOrEqualTo(1));
                    Assert.That(pixels[i * source.width].a, Is.LessThanOrEqualTo(1));
                    Assert.That(pixels[i * source.width + edge].a, Is.LessThanOrEqualTo(1));
                }
                Color body = source.GetPixel(source.width / 2, source.height / 3);
                Assert.That(body.a, Is.GreaterThan(0.95f));
                Assert.That(body.grayscale, Is.LessThan(0.35f));
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ViewDefersArtConstructionUntilFirstVisiblePresentation()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();

            Assert.That(view.ScreenRoot, Is.Null,
                "로비 첫 프레임에는 영구 성장 아트와 UI를 만들지 않아야 합니다.");

            view.SetNavigationPresentation(false, false);
            Assert.That(view.ScreenRoot, Is.Null,
                "닫힌 화면 상태를 적용하는 것만으로 리소스를 올리면 안 됩니다.");

            view.SetNavigationPresentation(true, false);
            Assert.That(view.ScreenRoot, Is.Not.Null,
                "붓 전환이 덮인 뒤 처음 표시할 때 전용 화면을 생성해야 합니다.");
        }

        [Test]
        public void ViewBuildsThreeReadableBranchesAndEveryCatalogNode()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform compactPanel = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen");
                Transform grid = compactPanel.Find("ChoiceGrid");
                Assert.That(compactView.CreatedCardCount, Is.EqualTo(4));
                Assert.That(compactView.TreeCanvas, Is.Null);
                Assert.That(compactView.IsNodePopupOpen, Is.False);
                for (int i = 0; i < 4; i++)
                {
                    Transform card = grid.Find($"GrowthCard{i}");
                    Assert.That(card, Is.Not.Null);
                    Image icon = card.Find("Icon")?.GetComponent<Image>();
                    Assert.That(icon?.sprite, Is.Not.Null);
                    Assert.That(icon.preserveAspect, Is.True);
                    Assert.That(card.GetComponent<Button>(), Is.Not.Null);
                }
            }
        }

        [Test]
        public void SelectingBranchDoesNotPurchaseUntilEnhanceButton()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform grid = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ChoiceGrid");
                Button brushCard = grid.Find("GrowthCard2").GetComponent<Button>();
                brushCard.onClick.Invoke();

                Assert.That(compactView.SelectedNodeId, Is.EqualTo("brush"));
                Assert.That(PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkBudgetEfficiency), Is.Zero);
                Assert.That(compactView.IsNodePopupOpen, Is.False);

                compactView.PurchaseButton.onClick.Invoke();

                Assert.That(PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkBudgetEfficiency), Is.EqualTo(1));
                Assert.That(grid.Find("GrowthCard2/SelectionInk")
                    .gameObject.activeSelf, Is.True);
                Assert.That(grid.Find("GrowthCard2/TabLabel")
                    .GetComponent<Text>().color, Is.EqualTo(InkPalette.Red));
            }
        }

        static void AssertSprite(Transform transform, string expectedName)
        {
            Assert.That(transform, Is.Not.Null, expectedName);
            var image = transform.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, expectedName);
            Assert.That(image.sprite, Is.Not.Null, expectedName);
            Assert.That(image.sprite.name, Does.StartWith(expectedName));
        }

        static bool HasIncomingPath(
            Transform treeCanvas,
            PermanentGrowthNodeDefinition definition)
        {
            string child = SanitizeNodeId(definition.Id);
            if (definition.ParentIds.Count == 0)
                return treeCanvas.Find($"GrowthRootPath_{child}") != null;
            for (int i = 0; i < definition.ParentIds.Count; i++)
            {
                string parent = SanitizeNodeId(definition.ParentIds[i]);
                if (treeCanvas.Find(
                        $"GrowthPath_{child}_From_{parent}") != null)
                    return true;
            }
            return false;
        }

        static Transform FindIncomingBranchArt(
            Transform treeCanvas,
            PermanentGrowthNodeDefinition definition)
        {
            string child = SanitizeNodeId(definition.Id);
            if (definition.ParentIds.Count == 0)
                return treeCanvas.Find($"TreeRootBranchArt_{child}");
            string parent = SanitizeNodeId(definition.ParentIds[0]);
            return treeCanvas.Find(
                $"TreeBranchArt_{child}_From_{parent}");
        }

        static string SanitizeNodeId(string id)
        {
            char[] characters = id.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsLetterOrDigit(characters[i]))
                    characters[i] = '_';
            return new string(characters);
        }
    }
}
