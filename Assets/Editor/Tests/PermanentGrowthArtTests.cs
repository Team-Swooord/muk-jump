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
        public void ViewBuildsThreeReadableBranchesAndEveryCatalogNode()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(view.CreatedRowCount, Is.EqualTo(3));
            Assert.That(
                view.CreatedNodeCount,
                Is.EqualTo(PermanentGrowthCatalog.All.Count));
            Assert.That(view.PurchaseButton, Is.Not.Null);
            Assert.That(
                view.PurchaseButton.GetComponent<RectTransform>().sizeDelta.y,
                Is.GreaterThanOrEqualTo(100f));
            Assert.That(
                view.PurchaseButton.targetGraphic,
                Is.TypeOf<Image>());
            Assert.That(
                InkUiStyle.UsesActionButtonSprite(
                    view.PurchaseButton.targetGraphic as Image),
                Is.True);

            Transform panel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Assert.That(panel, Is.Not.Null);
            AssertSprite(panel.Find("InkTreeRoot"), "pg_root_emblem");
            Assert.That(
                panel.Find("InkTreeRedFlow"),
                Is.Null,
                "강화 성공은 화면 먹획 연출로 표시하며 나무를 붉게 덮지 않습니다.");

            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches)
            {
                Transform header = panel.Find(
                    $"GrowthBranchHeader_{branch.Branch}");
                Assert.That(
                    header,
                    Is.Not.Null,
                    branch.DisplayName);
                Text title = header.Find("Brush/BranchTitle")
                    ?.GetComponent<Text>();
                Assert.That(
                    title?.fontSize,
                    Is.GreaterThanOrEqualTo(36));
            }

            foreach (PermanentGrowthDefinition definition
                     in PermanentGrowthCatalog.All)
            {
                Transform node = panel.Find(
                    $"GrowthNode_{definition.Type}");
                Assert.That(
                    node,
                    Is.Not.Null,
                    definition.Name);
                RectTransform touch = node.GetComponent<RectTransform>();
                Assert.That(touch.sizeDelta.x, Is.GreaterThanOrEqualTo(100f));
                Assert.That(touch.sizeDelta.y, Is.GreaterThanOrEqualTo(100f));
                Assert.That(node.GetComponent<Button>(), Is.Not.Null);
                Assert.That(node.Find("NodeName")?.GetComponent<Text>()
                        ?.fontSize,
                    Is.GreaterThanOrEqualTo(30));
                Assert.That(
                    panel.Find($"GrowthPath_{definition.Type}"),
                    Is.Not.Null);
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

            view.SelectGrowthForTests(0);

            Assert.That(
                view.SelectedGrowthType,
                Is.EqualTo(PermanentGrowthType.InkCapacity));
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.Zero);
            Assert.That(view.PurchaseButton.interactable, Is.True);

            view.PurchaseButton.onClick.Invoke();

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(1));
            Transform panel = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
                "PermanentGrowthScreen");
            Transform selectedNode = panel.Find(
                "GrowthNode_InkCapacity");
            Assert.That(
                selectedNode.Find("NodeLevel").GetComponent<Text>().text,
                Is.EqualTo("Lv. 1 / 6"));
            Assert.That(
                selectedNode.Find("NodeSurface").GetComponent<Image>().color,
                Is.Not.EqualTo(InkPalette.Red),
                "구매한 노드도 붉은 칠 대신 먹색/기존 꽃색을 사용해야 합니다.");
            Assert.That(
                panel.Find("GrowthBranchRedFlow2"),
                Is.Null);
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
