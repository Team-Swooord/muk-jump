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
            "pg_tree_trunk_mask",
            "pg_branch",
            "pg_branch_mask",
            "pg_node_bud",
            "pg_node_bloom",
            "pg_node_bloom_mask",
            "pg_selected_ring",
            "pg_hanji_card",
            "pg_primary_button",
            "pg_currency_badge",
            "pg_root_emblem",
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
            var store = new MemoryPermanentGrowthStore
            {
                Json =
                    "{\"schemaVersion\":1,\"balanceVersion\":1," +
                    "\"wallet\":100,\"spent\":0," +
                    "\"tutorialRewardClaimed\":false," +
                    "\"lastSettledRunId\":\"\",\"ranks\":[]}",
            };
            PermanentGrowthProfile.UseStoreForTests(store);

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
                    SpriteNames[i] == "pg_tree_trunk_mask";
                bool medium =
                    SpriteNames[i] == "pg_branch" ||
                    SpriteNames[i] == "pg_branch_mask" ||
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
        public void ViewBuildsFourBranchesAndAllCatalogNodesFromSprites()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(view.CreatedRowCount, Is.EqualTo(4));
            Assert.That(view.CreatedNodeCount, Is.EqualTo(24));
            Assert.That(view.PurchaseButton, Is.Not.Null);
            Assert.That(
                view.PurchaseButton.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(100f));

            Transform panel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(panel, Is.Not.Null);
            AssertSprite(panel.Find("InkTreeTrunk"), "pg_tree_trunk");
            AssertSprite(panel.Find("InkTreeRedFlow"), "pg_tree_trunk_mask");

            for (int branch = 0; branch < 4; branch++)
            {
                AssertSprite(
                    panel.Find($"GrowthBranch{branch + 1}"),
                    "pg_branch");
                AssertSprite(
                    panel.Find($"GrowthBranchProgress{branch + 1}"),
                    "pg_branch_mask");
                AssertSprite(
                    panel.Find($"GrowthBranchRedFlow{branch + 1}"),
                    "pg_branch_mask");
                for (int node = 0; node < 6; node++)
                {
                    AssertSprite(
                        panel.Find(
                            $"GrowthNode{branch + 1}_{node + 1}"),
                        "pg_node_bud");
                }
            }

            Assert.That(
                panel.Find("SelectedGrowthDetail/EnhanceButton"),
                Is.Not.Null);
        }

        [Test]
        public void SelectingBranchDoesNotPurchaseUntilEnhanceButton()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            view.SelectGrowthForTests(1);

            Assert.That(
                view.SelectedGrowthType,
                Is.EqualTo(PermanentGrowthType.InkRecovery));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.Zero);
            Assert.That(view.PurchaseButton.interactable, Is.True);

            view.PurchaseButton.onClick.Invoke();

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.EqualTo(1));
            Transform panel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            var redFlow = panel.Find("GrowthBranchRedFlow2")
                .GetComponent<Image>();
            var progressFlow = panel.Find("GrowthBranchProgress2")
                .GetComponent<Image>();
            Assert.That(progressFlow.fillAmount, Is.EqualTo(1f / 6f)
                .Within(0.001f));
            Assert.That(redFlow.color.r, Is.EqualTo(InkPalette.Red.r)
                .Within(0.001f));
            Assert.That(redFlow.color.a, Is.GreaterThan(0.9f));
        }

        static void AssertSprite(Transform transform, string expectedName)
        {
            Assert.That(transform, Is.Not.Null, expectedName);
            var image = transform.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, expectedName);
            Assert.That(image.sprite, Is.Not.Null, expectedName);
            Assert.That(image.sprite.name, Does.StartWith(expectedName));
        }
    }
}
