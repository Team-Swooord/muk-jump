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
                Is.EqualTo(PermanentGrowthCatalog.Nodes.Count));
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
            Transform treeCanvas = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/TreeLayerRoot/" +
                "TreeViewport/TreeCanvas");
            Assert.That(treeCanvas, Is.Not.Null);
            AssertSprite(treeCanvas.Find("InkTreeRoot"), "pg_root_emblem");
            AssertSprite(
                treeCanvas.Find("InkTreeBackground"),
                "pg_tree_background_v3");
            Assert.That(treeCanvas.Find("InkTreeTrunk"), Is.Null);
            Assert.That(
                treeCanvas.Find("InkTreeRedFlow"),
                Is.Null,
                "강화 성공은 화면 먹획 연출로 표시하며 나무를 붉게 덮지 않습니다.");

            foreach (PermanentGrowthBranchMetadata branch
                     in PermanentGrowthCatalog.Branches)
            {
                Transform header = treeCanvas.Find(
                    $"GrowthBranchHeader_{branch.Branch}");
                Assert.That(
                    header,
                    Is.Not.Null,
                    branch.DisplayName);
                Text title = header.Find("Brush/BranchTitle")
                    ?.GetComponent<Text>();
                Assert.That(
                    title?.fontSize,
                    Is.GreaterThanOrEqualTo(34));
                Assert.That(header.Find("BranchSummary"), Is.Null);
            }

            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                Transform node = treeCanvas.Find(
                    $"GrowthNode_{SanitizeNodeId(definition.Id)}");
                Assert.That(
                    node,
                    Is.Not.Null,
                    definition.Name);
                RectTransform touch = node.GetComponent<RectTransform>();
                Assert.That(touch.sizeDelta.x, Is.GreaterThanOrEqualTo(100f));
                Assert.That(touch.sizeDelta.y, Is.GreaterThanOrEqualTo(100f));
                Assert.That(node.GetComponent<Button>(), Is.Not.Null);
                Assert.That(node.Find("NodeName"), Is.Null);
                Assert.That(node.Find("NodeLevel"), Is.Null);
                Assert.That(
                    HasIncomingPath(treeCanvas, definition),
                    Is.True,
                    definition.Id);
                Assert.That(node.Find("Fruit"), Is.Not.Null);
                Assert.That(node.Find("FruitGlow"), Is.Not.Null);
                Transform branchArt =
                    FindIncomingBranchArt(treeCanvas, definition);
                Assert.That(branchArt, Is.Not.Null, definition.Id);
                string spriteName =
                    branchArt.GetComponent<Image>()?.sprite?.name;
                Assert.That(
                    spriteName,
                    Does.StartWith("pg_branch"),
                    definition.Id);
            }

            Transform popup = panel.Find("SelectedGrowthAction");
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.Find("ActionName"), Is.Not.Null);
            Assert.That(popup.Find("ActionDescription"), Is.Not.Null);
            Assert.That(popup.Find("ActionEffectSummary"), Is.Not.Null);
            Assert.That(popup.Find("ActionCurrentEffect"), Is.Null);
            Assert.That(popup.Find("ActionUsage"), Is.Null);
            Assert.That(popup.Find("ActionNextEffect"), Is.Null);
            Assert.That(popup.Find("ActionStatus"), Is.Null);
            Assert.That(popup.Find("ActionCostIcon"), Is.Not.Null);
            Assert.That(popup.Find("EnhanceButton"), Is.Not.Null);
            Assert.That(popup.Find("CloseButton"), Is.Not.Null);
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
            Assert.That(view.IsNodePopupOpen, Is.True);
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
            Transform selectedNode = viewHost.transform.Find(
                "PermanentGrowthCanvas/ScreenRoot/TreeLayerRoot/" +
                "TreeViewport/TreeCanvas/" +
                "GrowthNode_I00");
            Assert.That(selectedNode.Find("NodeLevel"), Is.Null);
            Assert.That(
                selectedNode.Find("NodeSurface").GetComponent<Image>().color,
                Is.Not.EqualTo(InkPalette.Red),
                "나무 전체가 아니라 별도의 해금 열매만 붉어야 합니다.");
            Color fruitColor =
                selectedNode.Find("Fruit").GetComponent<Image>().color;
            Assert.That(
                fruitColor.r,
                Is.EqualTo(InkPalette.Red.r).Within(0.001f));
            Assert.That(
                fruitColor.g,
                Is.EqualTo(InkPalette.Red.g).Within(0.001f));
            Assert.That(
                fruitColor.b,
                Is.EqualTo(InkPalette.Red.b).Within(0.001f));
            Assert.That(fruitColor.a, Is.GreaterThan(0.9f));
            Assert.That(
                selectedNode.Find("FruitGlow")
                    .GetComponent<Image>().color.a,
                Is.GreaterThan(0.1f));
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
