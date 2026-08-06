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
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Transform background =
                view.TreeCanvas.Find("InkTreeBackground");
            Assert.That(background, Is.Not.Null);
            var image = background.GetComponent<Image>();
            var rect = background.GetComponent<RectTransform>();
            Assert.That(image.sprite.name, Does.StartWith(
                "pg_tree_background_v3"));
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(
                image.color.a,
                Is.EqualTo(0.42f).Within(0.001f),
                "큰 나무는 열매와 연결선보다 옅어야 합니다.");
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(2200f, 3060f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(view.TreeCanvas.Find("InkTreeTrunk"), Is.Null);
            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes)
            {
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
        public void View_UsesTopSpaceAndKeepsReadableInkBackingBehindEveryBud()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Assert.That(view.TreeViewport.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(view.TreeViewport.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(view.TreeViewport.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(view.TreeViewport.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(
                view.TreeViewport.anchoredPosition,
                Is.EqualTo(Vector2.zero));
            Assert.That(view.TreeViewport.GetSiblingIndex(), Is.Zero);
            Assert.That(
                view.TreeViewport.GetComponent<RectMask2D>().padding,
                Is.EqualTo(Vector4.zero));
            Assert.That(
                view.TreeCanvas.localScale,
                Is.EqualTo(
                    Vector3.one *
                    PermanentGrowthView.CalculateTreeZoomForTests(
                        Screen.width,
                        Screen.height)));
            Assert.That(
                PermanentGrowthView.CalculateTreeZoomForTests(1080f, 1920f),
                Is.EqualTo(0.60f).Within(0.001f));
            Assert.That(
                PermanentGrowthView.CalculateTreeZoomForTests(1080f, 2400f),
                Is.EqualTo(0.48f).Within(0.001f),
                "20:9 화면은 좌우 계보 뿌리가 잘리지 않도록 나무만 축소해야 합니다.");

            Assert.That(
                view.TreeViewport.parent.name,
                Is.EqualTo("TreeLayerRoot"),
                "나무 지도는 Safe Area 안에서 다시 축소되면 안 됩니다.");
            Transform panel = view.ScreenRoot.Find(
                "SafeAreaRoot/PermanentGrowthScreen");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Find("Title"), Is.Null);
            Assert.That(panel.Find("Subtitle"), Is.Null);
            Assert.That(panel.Find("CurrencyBrush"), Is.Null);
            var headerInfoPanel = panel.Find("HeaderInfoPanel")
                ?.GetComponent<Image>();
            Assert.That(headerInfoPanel, Is.Not.Null);
            Assert.That(
                headerInfoPanel.color.a,
                Is.GreaterThanOrEqualTo(0.9f),
                "재화와 성장 요약은 상세창과 같은 진한 먹 패널 위에 있어야 합니다.");
            Assert.That(headerInfoPanel.raycastTarget, Is.False);
            Assert.That(panel.Find("CurrencyHud/CurrencyDrop"), Is.Not.Null);
            Assert.That(panel.Find("CurrencyHud/Balance"), Is.Not.Null);
            var currencyHud = panel.Find("CurrencyHud") as RectTransform;
            var balance = panel.Find("CurrencyHud/Balance") as RectTransform;
            var balanceLabel = balance?.GetComponent<Text>();
            var journeyText = panel.Find("CurrencyHud/JourneyText")
                ?.GetComponent<Text>();
            var journeyTrack = panel.Find("CurrencyHud/JourneyTrack")
                as RectTransform;
            var journeyFill = panel.Find("CurrencyHud/JourneyTrack/Fill")
                as RectTransform;
            var journeyPercent = panel.Find(
                    "CurrencyHud/JourneyTrack/JourneyPercentText")
                ?.GetComponent<Text>();
            Assert.That(journeyText, Is.Not.Null);
            Assert.That(journeyTrack, Is.Not.Null);
            Assert.That(journeyFill, Is.Not.Null);
            Assert.That(journeyPercent, Is.Not.Null);
            Assert.That(currencyHud, Is.Not.Null);
            Assert.That(currencyHud.anchoredPosition.x, Is.Zero.Within(0.001f),
                "재화 HUD는 성장 화면의 정확한 수평 중앙에 있어야 합니다.");
            Assert.That(balanceLabel.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(balanceLabel.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(balanceLabel.color, Is.EqualTo(InkPalette.TextLight));
            Assert.That(balanceLabel.GetComponent<Outline>().enabled, Is.False);
            Assert.That(journeyText.resizeTextForBestFit, Is.False);
            Assert.That(journeyText.fontSize, Is.GreaterThanOrEqualTo(30));
            Assert.That(journeyText.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(journeyText.color, Is.EqualTo(InkPalette.TextLight));
            Assert.That(journeyTrack.sizeDelta.x,
                Is.EqualTo(520f).Within(0.01f));
            Assert.That(journeyTrack.sizeDelta.y,
                Is.EqualTo(32f).Within(0.01f));
            Assert.That(journeyPercent.text, Does.Match(@"^\d+ / 100%$"));
            Assert.That(journeyPercent.alignment,
                Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(panel.Find("NodeResetButton"), Is.Not.Null);
            Assert.That(journeyFill.anchorMin.x, Is.Zero);
            Assert.That(journeyFill.anchorMax.x, Is.Zero);
            Assert.That(journeyFill.pivot.x, Is.Zero);
            float balanceBottom = balance.anchoredPosition.y -
                                  balance.sizeDelta.y * 0.5f;
            float journeyTop = journeyText.rectTransform.anchoredPosition.y +
                               journeyText.rectTransform.sizeDelta.y * 0.5f;
            Assert.That(balanceBottom, Is.GreaterThanOrEqualTo(journeyTop + 1f));

            foreach (string summaryName in new[]
                     {
                         "SurvivalSummary", "LeapSummary", "InkSummary",
                     })
            {
                Text summary = panel.Find(summaryName)?.GetComponent<Text>();
                Assert.That(summary, Is.Not.Null);
                Assert.That(summary.fontSize, Is.GreaterThanOrEqualTo(34));
                Assert.That(summary.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(summary.color, Is.EqualTo(InkPalette.TextLight));
                Assert.That(summary.GetComponent<Outline>().enabled, Is.False);
                Assert.That(summary.text, Does.Not.Contain("<b>"));
                Assert.That(
                    headerInfoPanel.transform.GetSiblingIndex(),
                    Is.LessThan(summary.transform.GetSiblingIndex()));
            }

            RectTransform backButton =
                view.BackButton.GetComponent<RectTransform>();
            Assert.That(backButton.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(backButton.anchoredPosition.y, Is.LessThan(0f));

            foreach (PermanentGrowthNodeDefinition definition
                     in PermanentGrowthCatalog.Nodes)
            {
                Transform node = view.TreeCanvas.Find(
                    $"GrowthNode_{Sanitize(definition.Id)}");
                Transform contrast = node?.Find("NodeContrast");
                Transform ring = node?.Find("SelectionRing");
                Transform glow = node?.Find("FruitGlow");
                Transform surface = node?.Find("NodeSurface");
                Assert.That(contrast, Is.Not.Null, definition.Id);
                Assert.That(ring, Is.Not.Null, definition.Id);
                Assert.That(glow, Is.Not.Null, definition.Id);
                Assert.That(surface, Is.Not.Null, definition.Id);
                var contrastImage = contrast.GetComponent<Image>();
                var contrastRect = contrast.GetComponent<RectTransform>();
                var surfaceRect = surface.GetComponent<RectTransform>();
                Assert.That(
                    contrastImage.color.a,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(contrastImage.raycastTarget, Is.False);
                Assert.That(
                    contrastRect.sizeDelta.x,
                    Is.EqualTo(surfaceRect.sizeDelta.x + 32f),
                    definition.Id);
                Assert.That(
                    contrast.GetSiblingIndex(),
                    Is.LessThan(ring.GetSiblingIndex()),
                    definition.Id);
                Assert.That(
                    contrast.GetSiblingIndex(),
                    Is.LessThan(glow.GetSiblingIndex()),
                    definition.Id);
                Assert.That(
                    contrast.GetSiblingIndex(),
                    Is.LessThan(surface.GetSiblingIndex()),
                    definition.Id);
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
            Assert.That(branch.color.a, Is.EqualTo(1f).Within(0.001f),
                "노드 사이 실제 나뭇가지는 진행 상태와 무관하게 완전히 보여야 합니다.");
            Assert.That(
                branch.rectTransform.sizeDelta.y,
                Is.GreaterThanOrEqualTo(140f));
            Assert.That(line.rectTransform.sizeDelta.y, Is.EqualTo(7f));

            view.SelectGrowthForTests(0);
            view.PurchaseButton.onClick.Invoke();

            Assert.That(branch.color.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                view.TreeCanvas.Find($"GrowthNode_{child}/Fruit")
                    .GetComponent<Image>().color.a,
                Is.GreaterThan(0.9f));
        }

        [Test]
        public void BranchArtwork_VisibleInkOverlapsBothConnectionEndpoints()
        {
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();

            Image[] images =
                view.TreeCanvas.GetComponentsInChildren<Image>(true);
            int checkedBranches = 0;
            for (int i = 0; i < images.Length; i++)
            {
                Image branch = images[i];
                string branchName = branch.name;
                bool rootBranch = branchName.StartsWith(
                    "TreeRootBranchArt_",
                    System.StringComparison.Ordinal);
                bool nodeBranch = branchName.StartsWith(
                    "TreeBranchArt_",
                    System.StringComparison.Ordinal);
                if (!rootBranch && !nodeBranch)
                    continue;

                string lineName = rootBranch
                    ? branchName.Replace(
                        "TreeRootBranchArt_",
                        "GrowthRootPath_")
                    : branchName.Replace(
                        "TreeBranchArt_",
                        "GrowthPath_");
                RectTransform line = view.TreeCanvas.Find(lineName)
                    ?.GetComponent<RectTransform>();
                Assert.That(line, Is.Not.Null, branchName);

                Vector2 visibleRange = VisibleHorizontalRange(
                    branch.sprite?.name);
                RectTransform branchRect = branch.rectTransform;
                float radians =
                    branchRect.localEulerAngles.z * Mathf.Deg2Rad;
                Vector2 direction =
                    new(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 visibleStart =
                    branchRect.anchoredPosition +
                    direction *
                    ((visibleRange.x - 0.5f) *
                     branchRect.sizeDelta.x);
                Vector2 visibleEnd =
                    branchRect.anchoredPosition +
                    direction *
                    ((visibleRange.y - 0.5f) *
                     branchRect.sizeDelta.x);
                Vector2 logicalStart =
                    line.anchoredPosition -
                    direction * (line.sizeDelta.x * 0.5f);
                Vector2 logicalEnd =
                    line.anchoredPosition +
                    direction * (line.sizeDelta.x * 0.5f);

                Assert.That(
                    Vector2.Dot(logicalStart - visibleStart, direction),
                    Is.GreaterThanOrEqualTo(17.5f),
                    $"{branchName} 시작점의 먹가지가 끊겼습니다.");
                Assert.That(
                    Vector2.Dot(visibleEnd - logicalEnd, direction),
                    Is.GreaterThanOrEqualTo(17.5f),
                    $"{branchName} 끝점의 먹가지가 끊겼습니다.");
                checkedBranches++;
            }

            Assert.That(checkedBranches, Is.GreaterThan(0));
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
