using System.IO;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthTreeBackgroundTests
    {
        const string BackgroundPath =
            "Assets/Resources/MukJump/UI/PermanentGrowth/" +
            "pg_tree_background_v3.png";

        GameObject managerHost;
        GameObject viewHost;

        [SetUp]
        public void SetUp()
        {
            var store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            PermanentGrowthProfile.DebugRefillCurrency();
            managerHost = new GameObject("GrowthTreeBackgroundManager");
            managerHost.AddComponent<GameManager>();
            viewHost = new GameObject("GrowthTreeBackgroundView");
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
        public void BackgroundSprite_IsTransparentLargeMobileSprite()
        {
            var importer =
                AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;

            Assert.That(importer, Is.Not.Null, BackgroundPath);
            Assert.That(
                importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(
                importer.spriteImportMode,
                Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath),
                Is.Not.Null);
        }

        [Test]
        public void BackgroundSprite_HasTransparentPaddingOnEveryEdge()
        {
            byte[] bytes = File.ReadAllBytes(BackgroundPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(bytes), Is.True);
                Color32[] pixels = texture.GetPixels32();
                int minimumX = texture.width;
                int minimumY = texture.height;
                int maximumX = -1;
                int maximumY = -1;
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        byte alpha = pixels[y * texture.width + x].a;
                        if (x == 0 || y == 0 ||
                            x == texture.width - 1 ||
                            y == texture.height - 1)
                            Assert.That(alpha, Is.Zero, $"edge ({x}, {y})");
                        if (alpha == 0) continue;
                        minimumX = Mathf.Min(minimumX, x);
                        minimumY = Mathf.Min(minimumY, y);
                        maximumX = Mathf.Max(maximumX, x);
                        maximumY = Mathf.Max(maximumY, y);
                    }
                }

                Assert.That(minimumX, Is.GreaterThanOrEqualTo(40));
                Assert.That(minimumY, Is.GreaterThanOrEqualTo(40));
                Assert.That(
                    maximumX,
                    Is.LessThanOrEqualTo(texture.width - 40));
                Assert.That(
                    maximumY,
                    Is.LessThanOrEqualTo(texture.height - 40));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void View_PlacesEveryFruitAboveOneGiantTreeBackground()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Assert.That(compactView.CreatedCardCount, Is.EqualTo(4));
                Assert.That(compactView.TreeCanvas, Is.Null);
                Transform grid = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ChoiceGrid");
                Assert.That(grid, Is.Not.Null);
                for (int i = 0; i < 4; i++)
                    Assert.That(grid.Find($"GrowthCard{i}"), Is.Not.Null);
            }
        }

        [Test]
        public void View_UsesTopSpaceAndKeepsReadableInkBackingBehindEveryBud()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform compactPanel = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen");
                Assert.That(compactPanel.Find("HeaderGroup"), Is.Not.Null);
                Assert.That(compactPanel.Find("ChoiceGrid"), Is.Not.Null);
                Assert.That(compactPanel.Find("BottomActions"), Is.Not.Null);
                Assert.That(compactView.TreeViewport, Is.Null);
                Assert.That(compactView.TreeScrollRect, Is.Null);
            }
        }

        [Test]
        public void RootBranchArtwork_RemainsFixedWhileFruitShowsUnlockState()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform card = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ChoiceGrid/GrowthCard0");
                Assert.That(card.GetComponent<Button>(), Is.Not.Null);
                card.GetComponent<Button>().onClick.Invoke();
                compactView.PurchaseButton.onClick.Invoke();
                Assert.That(PermanentGrowthProfile.IsNodeUnlocked("body.1"), Is.True);
            }
        }

        [Test]
        public void SelectedPathLeavesOtherTwoWrappedInNonBlockingThorns()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform grid = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ChoiceGrid");
                grid.Find("GrowthCard2").GetComponent<Button>().onClick.Invoke();
                int selectedUnderlines = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (grid.Find($"GrowthCard{i}/SelectionInk")
                        .gameObject.activeSelf)
                        selectedUnderlines++;
                }
                Assert.That(selectedUnderlines, Is.EqualTo(1));
                Assert.That(compactView.SelectedNodeId, Is.EqualTo("brush"));
            }
        }

        [Test]
        public void BranchArtwork_VisibleInkOverlapsBothConnectionEndpoints()
        {
            {
                var compactView = viewHost.AddComponent<PermanentGrowthView>();
                compactView.BuildForTests();
                Transform grid = compactView.ScreenRoot.Find(
                    "SafeAreaRoot/PermanentGrowthScreen/ChoiceGrid");
                Assert.That(grid.Find("InkTreeBackground"), Is.Null);
                Assert.That(grid.GetComponentsInChildren<Button>(true).Length,
                    Is.EqualTo(4));
            }
        }

        static Vector2 VisibleHorizontalRange(string spriteName)
        {
            return spriteName switch
            {
                "pg_branch_piece_01" => new Vector2(0.064f, 0.966f),
                "pg_branch_piece_02" => new Vector2(0.059f, 0.961f),
                "pg_branch_piece_03" => new Vector2(0.135f, 0.865f),
                "pg_branch_piece_04" => new Vector2(0.063f, 0.928f),
                "pg_branch_piece_05" => new Vector2(0.067f, 0.944f),
                "pg_branch_piece_06" => new Vector2(0.134f, 0.874f),
                _ => new Vector2(0f, 1f),
            };
        }

        static string Sanitize(string id)
        {
            char[] characters = id.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsLetterOrDigit(characters[i]))
                    characters[i] = '_';
            return new string(characters);
        }
    }
}
