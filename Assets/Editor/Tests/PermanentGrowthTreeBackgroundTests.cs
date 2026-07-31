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
            "pg_tree_background_v2.png";

        GameObject managerHost;
        GameObject viewHost;

        [SetUp]
        public void SetUp()
        {
            var store = new MemoryPermanentGrowthStore
            {
                Json =
                    "{\"schemaVersion\":1,\"balanceVersion\":1," +
                    "\"wallet\":1000,\"spent\":0," +
                    "\"tutorialRewardClaimed\":true," +
                    "\"lastSettledRunId\":\"tree-background-test\"," +
                    "\"ranks\":[]}",
            };
            PermanentGrowthProfile.UseStoreForTests(store);
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
        public void View_PlacesEveryFruitAboveOneGiantTreeBackground()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Transform background =
                view.TreeCanvas.Find("InkTreeBackground");
            Assert.That(background, Is.Not.Null);
            var image = background.GetComponent<Image>();
            var rect = background.GetComponent<RectTransform>();
            Assert.That(image.sprite.name, Does.StartWith(
                "pg_tree_background_v2"));
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.preserveAspect, Is.False);
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(3000f, 3060f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, -20f)));

            Rect backgroundBounds = new(
                rect.anchoredPosition - rect.sizeDelta * 0.5f,
                rect.sizeDelta);
            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes)
            {
                Assert.That(
                    backgroundBounds.Contains(
                        new Vector2(node.LayoutX, node.LayoutY)),
                    Is.True,
                    $"{node.Id} 열매가 큰 나무 배경 밖에 있습니다.");
                Transform nodeTransform = view.TreeCanvas.Find(
                    $"GrowthNode_{Sanitize(node.Id)}");
                Assert.That(nodeTransform, Is.Not.Null, node.Id);
                Assert.That(
                    background.GetSiblingIndex(),
                    Is.LessThan(nodeTransform.GetSiblingIndex()),
                    node.Id);
            }
        }

        [Test]
        public void BranchArtwork_RemainsFixedWhileOnlyFruitShowsUnlockState()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            PermanentGrowthNodeDefinition first =
                PermanentGrowthCatalog.GetNode(
                    PermanentGrowthType.InkCapacity,
                    1);
            string child = Sanitize(first.Id);
            Image branch = view.TreeCanvas
                .Find($"TreeRootBranchArt_{child}")
                .GetComponent<Image>();
            Image line = view.TreeCanvas
                .Find($"GrowthRootPath_{child}")
                .GetComponent<Image>();
            Assert.That(branch.color.a, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(
                branch.rectTransform.sizeDelta.y,
                Is.GreaterThanOrEqualTo(140f));
            Assert.That(line.rectTransform.sizeDelta.y, Is.EqualTo(7f));

            view.SelectGrowthForTests(0);
            view.PurchaseButton.onClick.Invoke();

            Assert.That(branch.color.a, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(
                view.TreeCanvas.Find($"GrowthNode_{child}/Fruit")
                    .GetComponent<Image>().color.a,
                Is.GreaterThan(0.9f));
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
