using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthKeystoneViewTests
    {
        GameObject host;
        PermanentGrowthView view;
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            GameLocalization.SetLanguage(GameLanguage.Korean);
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            PermanentGrowthProfile.DebugRefillCurrency();
            host = new GameObject("PermanentGrowthFourCardViewTests");
            view = host.AddComponent<PermanentGrowthView>();
            typeof(PermanentGrowthView).GetField("purchaseButtonTexture",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/muk_start_button.png"));
            // EditMode에서는 MonoBehaviour의 활성화 콜백이 자동 실행되지 않는다.
            // 실제 화면처럼 프로필 변경 이벤트를 구독한 상태에서 갱신을 검증한다.
            typeof(PermanentGrowthView).GetMethod("OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            view.BuildForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                typeof(PermanentGrowthView).GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                Object.DestroyImmediate(host);
            }
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void ViewHasOneFocusedGrowthAndFourBottomSelectionTabs()
        {
            Vector2[] positions =
            {
                new(-390f, -405f), new(-130f, -405f),
                new(130f, -405f), new(390f, -405f),
            };
            Transform grid = Screen().Find("ChoiceGrid");
            Assert.That(grid, Is.Not.Null);
            Assert.That(Screen().Find("FocusedGrowth/FocusIcon"), Is.Not.Null);
            Assert.That(Screen().Find("FocusedGrowth/FocusTitle"), Is.Not.Null);
            Assert.That(Screen().Find("FocusedGrowth/FocusSummary"), Is.Not.Null);
            for (int i = 0; i < positions.Length; i++)
            {
                RectTransform card = grid.Find($"GrowthCard{i}")
                    as RectTransform;
                Assert.That(card, Is.Not.Null);
                Assert.That(card.anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(240f, 386f)));
                Assert.That(card.Find("Surface"), Is.Null);
                Assert.That(card.Find("TabEffectSummary"), Is.Not.Null);
            }
        }

        [Test]
        public void HeaderIconsSitNearOppositeEdgesWithoutLosingTheirTouchAreas()
        {
            foreach (Button button in new[] { view.BackButton, view.NodeResetButton })
            {
                RectTransform hitArea = button.GetComponent<RectTransform>();
                RectTransform icon = button.transform.Find("Icon") as RectTransform;
                float centerX = hitArea.anchoredPosition.x + icon.anchoredPosition.x;
                Assert.That(centerX, Is.EqualTo(button == view.BackButton ? -465f : 465f));
                Assert.That(540f - Mathf.Abs(centerX) - icon.sizeDelta.x / 2f, Is.EqualTo(33f));
                Assert.That(icon.anchoredPosition.y, Is.Zero);
                Rect iconBounds = LocalBounds(icon);
                Assert.That(iconBounds.xMin - hitArea.rect.xMin, Is.GreaterThanOrEqualTo(20f));
                Assert.That(hitArea.rect.xMax - iconBounds.xMax, Is.GreaterThanOrEqualTo(20f));
                Assert.That(LocalBounds(hitArea).xMin, Is.GreaterThan(-540f));
                Assert.That(LocalBounds(hitArea).xMax, Is.LessThan(540f));
                Assert.That(hitArea.rect.height, Is.GreaterThanOrEqualTo(120f));
            }
        }

        [Test]
        public void HeaderAndBottomActionsUseTheApprovedSimpleGeometry()
        {
            AssertRect("HeaderGroup/BackButton", new(-420f, 860f), new(220f, 120f));
            AssertRect("HeaderGroup/Title", new(0f, 860f), new(520f, 108f));
            Assert.That(Screen().Find("HeaderGroup/Description"), Is.Null);
            AssertRect("HeaderGroup/BalanceHud", new(415f, 720f), new(230f, 120f));
            AssertRect("HeaderGroup/BalanceHud/InkDropIcon", new(-64f, 0f), new(84f, 84f));
            AssertRect("HeaderGroup/BalanceHud/Balance", new(42f, 0f), new(136f, 100f));
            AssertRect("FocusedGrowth/FocusIcon", new(0f, 318f), new(310f, 310f));
            AssertRect("FocusedGrowth/FocusTitle", new(0f, 78f), new(760f, 120f));
            AssertRect("FocusedGrowth/FocusSummary", new(0f, -52f), new(900f, 100f));
            AssertRect("BottomActions/SelectedCost", new(0f, -670f), new(720f, 80f));
            AssertRect("BottomActions/PurchaseButton", new(0f, -840f), new(820f, 200f));
            AssertRect("HeaderGroup/NodeResetButton", new(430f, 860f), new(200f, 120f));
            Assert.That(Screen().Find("BottomActions/NodeResetButton"), Is.Null);
            Image backIcon = view.BackButton.transform.Find("Icon").GetComponent<Image>();
            Assert.That(backIcon.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthBackIconSprite()));
            Assert.That(backIcon.sprite.name, Is.EqualTo("GrowthBackIcon"));
            Assert.That(view.NodeResetButton.transform.Find("Label"), Is.Null);
            Assert.That(view.NodeResetButton.transform.Find("Icon").GetComponent<Image>().sprite,
                Is.SameAs(InkUiTextureFactory.CreateGrowthResetIconSprite()));
            Assert.That(Screen().Find("HeaderGroup/Title")
                .GetComponent<Text>().fontSize, Is.EqualTo(80));
            Assert.That(Screen().Find("HeaderGroup/Title")
                .GetComponent<Text>().text, Is.EqualTo("성장"));
            Assert.That(Screen().Find("HeaderGroup/BalanceHud/BalanceCaption"), Is.Null);
            Assert.That(Screen().Find("HeaderGroup/BalanceHud/Balance")
                .GetComponent<Text>().fontSize, Is.EqualTo(68));
            Text cost = Screen().Find("BottomActions/SelectedCost").GetComponent<Text>();
            Assert.That(cost.fontSize, Is.EqualTo(56));
            Assert.That(cost.raycastTarget, Is.False);
        }

        [Test]
        public void NoTreeScrollFruitOrNodePopupExists()
        {
            Assert.That(view.TreeViewport, Is.Null);
            Assert.That(view.TreeCanvas, Is.Null);
            Assert.That(view.TreeScrollRect, Is.Null);
            Assert.That(view.NodePopupDimmerButton, Is.Null);
            Assert.That(host.GetComponentInChildren<ScrollRect>(true), Is.Null);
            Assert.That(host.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "GrowthNodePopupOverlay" ||
                             item.name == "TreeCanvas" ||
                             item.name.StartsWith("GrowthNode_")),
                Is.False);
        }

        [Test]
        public void FocusTextIsReadableAndTabsStayShort()
        {
            Transform grid = Screen().Find("ChoiceGrid");
            string[] compactEffects =
            {
                "체력\n+1칸", "최대 먹물\n+12.5%", "먹물 소모\n-3%", "점프\n+1.25%",
            };
            for (int i = 0; i < PermanentGrowthCatalog.Choices.Count; i++)
            {
                Text tabLabel = grid.Find($"GrowthCard{i}/TabLabel")
                    .GetComponent<Text>();
                Text tabEffect = grid.Find(
                        $"GrowthCard{i}/TabEffectSummary")
                    .GetComponent<Text>();
                Assert.That(
                    new[] { "먹", "먹물", "붓", "도약" },
                    Does.Contain(tabLabel.text));
                Assert.That(tabLabel.fontSize, Is.EqualTo(56));
                Assert.That(tabLabel.resizeTextForBestFit, Is.False);
                Assert.That(tabEffect.text, Is.EqualTo(compactEffects[i]));
                Assert.That(tabEffect.fontSize, Is.EqualTo(56));
                Assert.That(tabEffect.color, Is.EqualTo(InkPalette.TextDark));
                Assert.That(tabEffect.resizeTextForBestFit, Is.False);
                Assert.That(tabEffect.preferredWidth,
                    Is.LessThanOrEqualTo(
                        tabEffect.rectTransform.rect.width + 0.01f));
                Assert.That(tabEffect.preferredHeight,
                    Is.LessThanOrEqualTo(
                        tabEffect.rectTransform.rect.height + 0.01f));
                view.SelectGrowthForTests(i);
                Text title = Screen().Find("FocusedGrowth/FocusTitle")
                    .GetComponent<Text>();
                Text summary = Screen().Find("FocusedGrowth/FocusSummary")
                    .GetComponent<Text>();
                Assert.That(title.text,
                    Is.EqualTo(PermanentGrowthCatalog.Choices[i].DisplayName));
                Assert.That(summary.text,
                    Is.EqualTo(PermanentGrowthCatalog.Choices[i].Summary));
                Assert.That(title.fontSize, Is.EqualTo(80));
                Assert.That(summary.fontSize, Is.EqualTo(56));
                Assert.That(title.fontSize - summary.fontSize,
                    Is.GreaterThanOrEqualTo(20));
                Assert.That(title.color, Is.EqualTo(InkPalette.TextDark));
                Assert.That(title.resizeTextForBestFit, Is.False);
                Assert.That(summary.resizeTextForBestFit, Is.False);
                Assert.That(summary.text, Does.Not.Match("[0-9%+→]"));
            }
        }

        [Test]
        public void ProgressDropsShareCurrencyBrushArtworkAndShowOwnedDensityForEveryTrack()
        {
            Transform focus = Screen().Find("FocusedGrowth");
            int objectCount = host.GetComponentsInChildren<Transform>(true).Length;
            Rect summary = LocalBounds(focus.Find("FocusSummary") as RectTransform);
            Rect tabs = LocalBounds(Screen().Find("ChoiceGrid/GrowthCard0") as RectTransform);
            for (int choiceIndex = 0; choiceIndex < 4; choiceIndex++)
            {
                PermanentGrowthType type = PermanentGrowthCatalog.Choices[choiceIndex].Type;
                int maximum = PermanentGrowthCatalog.Get(type).MaxLevel;
                view.SelectGrowthForTests(choiceIndex);
                for (int level = 0; level <= maximum; level++)
                {
                    AssertProgressDropState(focus, maximum, level);
                    for (int i = 0; i < maximum; i++)
                    {
                        RectTransform drop = focus.Find($"FocusGrowthMark{i}") as RectTransform;
                        Rect bounds = LocalBounds(drop);
                        Assert.That(bounds.yMax, Is.LessThan(summary.yMin));
                        Assert.That(bounds.yMin, Is.GreaterThan(tabs.yMax));
                    }
                    if (level < maximum)
                        Assert.That(PermanentGrowthProfile.TryPurchase(type), Is.True);
                }
            }

            // 완료 후 환급·다른 선택 왕복에서도 빈 칸이 다시 나타나며 객체를 추가하지 않는다.
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            for (int i = 3; i >= 0; i--)
            {
                view.SelectGrowthForTests(i);
                AssertProgressDropState(focus,
                    PermanentGrowthCatalog.Get(PermanentGrowthCatalog.Choices[i].Type).MaxLevel, 0);
            }
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
        }

        static void AssertProgressDropState(Transform focus, int maximum, int level)
        {
            Image balance = focus.parent.Find("HeaderGroup/BalanceHud/InkDropIcon").GetComponent<Image>();
            Assert.That(balance.sprite, Is.Not.Null);
            Assert.That(balance.sprite.texture.name, Does.StartWith("pg_inklight_sumukhwa_v1"));
            Assert.That(focus.Find("FocusCompleteLabel").gameObject.activeSelf,
                Is.EqualTo(level == maximum));
            for (int i = 0; i < 8; i++)
            {
                Image drop = focus.Find($"FocusGrowthMark{i}").GetComponent<Image>();
                bool visible = level < maximum && i < maximum;
                Assert.That(drop.gameObject.activeSelf, Is.EqualTo(visible));
                Assert.That(drop.rectTransform.sizeDelta, Is.EqualTo(new Vector2(64f, 64f)));
                Assert.That(drop.transform.Find("EmptyInset"), Is.Null);
                Assert.That(drop.sprite, Is.SameAs(balance.sprite));
                Assert.That(drop.enabled, Is.True);
                Assert.That(drop.raycastTarget, Is.False);
                Assert.That(drop.preserveAspect, Is.True);
                Assert.That(drop.GetComponent<Button>(), Is.Null);
                if (!visible) continue;
                Assert.That(drop.rectTransform.anchoredPosition,
                    Is.EqualTo(new Vector2((i - (maximum - 1) * 0.5f) * 80f, -150f)));
                Assert.That(drop.color, Is.EqualTo(new Color(1f, 1f, 1f, i < level ? 1f : 0.32f)));
                if (i > 0)
                {
                    Rect previous = LocalBounds(focus.Find($"FocusGrowthMark{i - 1}") as RectTransform);
                    Assert.That(LocalBounds(drop.rectTransform).xMin - previous.xMax,
                        Is.GreaterThanOrEqualTo(16f));
                }
            }
        }

        [Test]
        public void CardClickOnlySelectsAndBottomActionPerformsPurchase()
        {
            Transform card = Screen().Find("ChoiceGrid/GrowthCard3");
            card.GetComponent<Button>().onClick.Invoke();

            Assert.That(view.SelectedGrowthType,
                Is.EqualTo(PermanentGrowthType.JumpHeight));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);

            view.PurchaseButton.onClick.Invoke();

            Assert.That(PermanentGrowthProfile.GetLevel(
                PermanentGrowthType.JumpHeight), Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactlyOneSelectedTabUsesTheRedMeaningUnderline()
        {
            for (int selected = 0; selected < 4; selected++)
            {
                view.SelectGrowthForTests(selected);
                int red = Screen().Find("ChoiceGrid")
                    .Cast<Transform>()
                    .Count(card => card.Find("SelectionInk")
                        .gameObject.activeSelf);
                Assert.That(red, Is.EqualTo(1));
                Text label = Screen().Find(
                    $"ChoiceGrid/GrowthCard{selected}/TabLabel")
                    .GetComponent<Text>();
                Text effect = Screen().Find(
                    $"ChoiceGrid/GrowthCard{selected}/TabEffectSummary")
                    .GetComponent<Text>();
                Assert.That(Approximately(label.color, InkPalette.Red), Is.True);
                Assert.That(Approximately(effect.color, InkPalette.TextDark), Is.True);
            }
        }

        [Test]
        public void OrnamentsUseSharedArtBehindTextAndNeverBlockTouches()
        {
            Transform ornaments = Screen().Find("GrowthOrnaments");
            Assert.That(ornaments, Is.Not.Null);
            Assert.That(ornaments.GetSiblingIndex(), Is.Zero);
            Assert.That(ornaments.GetComponentsInChildren<Button>(true), Is.Empty);
            Assert.That(ornaments.GetComponentsInChildren<Text>(true), Is.Empty);
            Image[] images = ornaments.GetComponentsInChildren<Image>(true);
            Assert.That(images.Length, Is.EqualTo(2));
            Assert.That(ornaments.Cast<Transform>().Any(item => item.name.StartsWith("ChoiceDivider")), Is.False);
            foreach (Image image in images)
            {
                Assert.That(image.raycastTarget, Is.False, image.name);
                Assert.That(image.sprite, Is.Not.Null, image.name);
                Assert.That(image.color.a, Is.LessThan(0.8f), image.name);
            }

            Image ring = ornaments.Find("FocusBrushRing").GetComponent<Image>();
            Assert.That(ring.sprite, Is.SameAs(Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_selected_ring")));
            Assert.That(ring.preserveAspect, Is.True);
            Assert.That(ring.rectTransform.sizeDelta, Is.EqualTo(new Vector2(350f, 350f)));
            Assert.That(ornaments.Find("ChoicePaperRibbon"), Is.Null);
            Assert.That(ornaments.Find("ChoiceBrushFrame"), Is.Null);
            var card = Screen().Find("ChoiceGrid/GrowthCard0");
            var frame = card.Find("BrushFrame").GetComponent<GrowthRingFrameGraphic>();
            Assert.That(frame.sprite, Is.SameAs(ring.sprite), "원형 테두리를 흉내 내지 않고 같은 원화를 사용합니다.");
            Assert.That(frame.color, Is.EqualTo(ring.color));
            Assert.That(frame.raycastTarget, Is.False);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(new Vector2(204f, 350f)));
            Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(card.Find("Paper").GetSiblingIndex()));
            Assert.That(LocalBounds(ring.rectTransform).yMin,
                Is.GreaterThan(LocalBounds(Screen().Find("FocusedGrowth/FocusTitle") as RectTransform).yMax));

            Image ribbon = card.Find("Paper").GetComponent<Image>();
            Assert.That(ribbon.color.a, Is.EqualTo(.94f).Within(.001f));
            Color paperTint = Color.Lerp(InkPalette.Paper, InkPalette.TextLight, .35f);
            Assert.That(ribbon.color.r, Is.EqualTo(paperTint.r).Within(.001f));
            Assert.That(ribbon.color.g, Is.EqualTo(paperTint.g).Within(.001f));
            Assert.That(ribbon.color.b, Is.EqualTo(paperTint.b).Within(.001f));
            Assert.That(ribbon.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthPaperRibbonSprite()));
            Assert.That(ribbon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(240f, 386f)));
            Assert.That(ribbon.type, Is.EqualTo(Image.Type.Simple), "카드 전체의 불규칙한 붓끝을 9-slice로 펴지 않습니다.");
            Assert.That(ribbon.sprite.border, Is.EqualTo(Vector4.zero));
            Rect ribbonBounds = LocalBounds((RectTransform)card);
            Assert.That(ribbonBounds.yMax,
                Is.LessThan(LocalBounds(Screen().Find("FocusedGrowth/FocusCompleteLabel") as RectTransform).yMin));
            Assert.That(ribbonBounds.yMin,
                Is.GreaterThan(LocalBounds(view.PurchaseButton.transform as RectTransform).yMax));
            Assert.That(ribbonBounds.xMin, Is.GreaterThan(-540f));
            Assert.That(ribbonBounds.xMax, Is.LessThan(540f));
            Assert.That(ornaments.Find("ChoiceTopBrushEdge"), Is.Null);
            Assert.That(ornaments.Find("ChoiceBottomBrushEdge"), Is.Null);
            Assert.That(ribbon.rectTransform.rect.yMax - LocalBounds((RectTransform)card.Find("Icon")).yMax,
                Is.GreaterThanOrEqualTo(24f));
            Assert.That(LocalBounds((RectTransform)card.Find("TabEffectSummary")).yMin - ribbon.rectTransform.rect.yMin,
                Is.GreaterThanOrEqualTo(24f));
            Rect progress = LocalBounds(Screen().Find("FocusedGrowth/FocusGrowthMark0") as RectTransform);
            Assert.That(progress.yMin - ribbonBounds.yMax, Is.GreaterThanOrEqualTo(28f));
            Rect cost = LocalBounds(Screen().Find("BottomActions/SelectedCost") as RectTransform);
            Assert.That(ribbonBounds.yMin - cost.yMax, Is.GreaterThanOrEqualTo(30f));

            Assert.That(view.PurchaseButton.transform.Find("RoleWash"), Is.Null);
            var purchaseBrush = view.PurchaseButton.transform.Find("BrushBackground").GetComponent<RectTransform>();
            var purchaseLabel = view.PurchaseButton.transform.Find("Label").GetComponent<RectTransform>();
            Assert.That(purchaseLabel.anchoredPosition.x, Is.Zero);
            Assert.That(purchaseBrush.anchoredPosition.x,
                Is.EqualTo(-LobbyMenuLayout.BrushArtworkLabelOffsetX * 820f / LobbyMenuLayout.BackgroundSize.x).Within(.01f));
            Assert.That(purchaseBrush.GetSiblingIndex(), Is.LessThan(purchaseLabel.GetSiblingIndex()));
            Assert.That(LocalBounds(purchaseBrush).xMax, Is.LessThan(540f));
        }

        [TestCase(0, GameLanguage.Korean)]
        [TestCase(1, GameLanguage.Korean)]
        [TestCase(2, GameLanguage.Korean)]
        [TestCase(3, GameLanguage.Korean)]
        [TestCase(0, GameLanguage.English)]
        [TestCase(1, GameLanguage.English)]
        [TestCase(2, GameLanguage.English)]
        [TestCase(3, GameLanguage.English)]
        public void RenderFourSelectionPanelsWithButtonCallback(int selected, GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            // 실제 uGUI 선택 영역을 고정 배율로 렌더한다. 실기기 캡처나 서버 데이터가 아니다.
            const int width = 1080, height = 860;
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D capture = null;
            RenderTexture previous = RenderTexture.active;
            int currency = PermanentGrowthProfile.Currency;
            try
            {
                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(1080, 1920);
                canvasRect.position = Vector3.zero;
                canvasRect.localScale = Vector3.one;
                var visibleGroup = canvas.GetComponent<CanvasGroup>();
                visibleGroup.alpha = 1f;
                visibleGroup.interactable = visibleGroup.blocksRaycasts = true;
                view.ScreenRoot.anchoredPosition = Vector2.zero;
                var safe = (RectTransform)view.ScreenRoot.Find("SafeAreaRoot");
                safe.anchorMin = Vector2.zero;
                safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                Screen().localScale = Vector3.one;
                ((RectTransform)Screen()).anchoredPosition = Vector2.zero;
                Screen().Find($"ChoiceGrid/GrowthCard{selected}").GetComponent<Button>().onClick.Invoke();
                // EditMode에서는 ColorTween 코루틴이 진행하지 않으므로 열린 버튼의 정상 색을 즉시 반영한다.
                foreach (Button button in Screen().Find("ChoiceGrid").GetComponentsInChildren<Button>())
                {
                    button.enabled = false;
                    button.enabled = true;
                    Assert.That(button.IsInteractable(), Is.True);
                }
                Assert.That(view.SelectedGrowthType, Is.EqualTo(PermanentGrowthCatalog.Choices[selected].Type));
                Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(currency));
                foreach (Transform item in host.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                cameraHost = new GameObject("GrowthSelectionRenderCamera");
                Camera camera = cameraHost.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = height * .5f;
                camera.transform.position = new Vector3(0f, -520f, -10f);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = InkPalette.Paper;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                canvas.enabled = false;
                canvas.enabled = true;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                const string folder = "output/quality-polish/growth-selection";
                System.IO.Directory.CreateDirectory(folder);
                string suffix = language == GameLanguage.English ? "-en" : "";
                System.IO.File.WriteAllBytes($"{folder}/selection-{selected}{suffix}.png", capture.EncodeToPNG());
                int inkPixels = capture.GetPixels32().Count(pixel => pixel.r < 100 && pixel.g < 100 && pixel.b < 100);
                Assert.That(inkPixels, Is.GreaterThan(500), "빈 렌더는 선택 UI 검증으로 인정하지 않습니다.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (capture != null) Object.DestroyImmediate(capture);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        [Test]
        public void PaperRibbonHasSolidCenterAndIrregularBrushEdges()
        {
            Sprite ribbon = Screen().Find("ChoiceGrid/GrowthCard0/Paper").GetComponent<Image>().sprite;
            Assert.That(ribbon, Is.SameAs(InkUiTextureFactory.CreateGrowthPaperRibbonSprite()));
            Assert.That(ribbon, Is.Not.SameAs(InkUiTextureFactory.CreateBrushSprite()));
            Assert.That(ribbon.texture.isReadable, Is.False);
            Assert.That(ribbon.texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            RenderTexture previous = RenderTexture.active;
            int width = ribbon.texture.width, height = ribbon.texture.height;
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                target.Create();
                Graphics.Blit(ribbon.texture, target);
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                Color32[] pixels = capture.GetPixels32();
                int transparentCore = 0;
                byte darkest = 255, lightest = 0;
                for (int y = 40; y < height - 40; y++)
                for (int x = 40; x < width - 40; x++)
                {
                    Color32 pixel = pixels[y * width + x];
                    if (pixel.a < 254) transparentCore++;
                    if (pixel.r < darkest) darkest = pixel.r;
                    if (pixel.r > lightest) lightest = pixel.r;
                }
                Assert.That(transparentCore, Is.Zero, "아이콘·글씨 뒤에 길쭉한 붓 구멍이 없어야 합니다.");
                Assert.That(lightest - darkest, Is.LessThan(16), "종이 중심은 얼룩 덩어리 없이 고른 농도를 유지합니다.");
                Assert.That(pixels[0].a, Is.Zero);
                Assert.That(pixels[width - 1].a, Is.Zero);
                int lowestTop = height, highestTop = 0;
                for (int x = 30; x < width - 30; x += 4)
                {
                    int top = 0;
                    for (int y = height - 40; y < height; y++)
                        if (pixels[y * width + x].a >= 128) top = y;
                    lowestTop = Mathf.Min(lowestTop, top);
                    highestTop = Mathf.Max(highestTop, top);
                }
                Assert.That(highestTop - lowestTop, Is.InRange(7, 28),
                    "흰 바탕의 붓끝이 눈에 띄게 불규칙해야 합니다.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(capture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        public void FourPanelsShareTheApprovedStyleAndKeepSeparateTouchAreas(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            Image circle = Screen().Find("GrowthOrnaments/FocusBrushRing").GetComponent<Image>();
            Image previousPaper = null;
            float previousRight = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                view.SelectGrowthForTests(i);
                var card = (RectTransform)Screen().Find($"ChoiceGrid/GrowthCard{i}");
                var paper = card.Find("Paper").GetComponent<Image>();
                var frame = card.Find("BrushFrame").GetComponent<GrowthRingFrameGraphic>();
                Assert.That(paper.rectTransform.sizeDelta, Is.EqualTo(card.sizeDelta));
                Assert.That(paper.raycastTarget || frame.raycastTarget, Is.False);
                Assert.That(card.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(frame.sprite, Is.SameAs(circle.sprite));
                Assert.That(frame.color, Is.EqualTo(circle.color));
                Assert.That(frame.transform.GetSiblingIndex(), Is.LessThan(card.Find("Icon").GetSiblingIndex()));
                if (previousPaper != null)
                {
                    Assert.That(paper.sprite, Is.SameAs(previousPaper.sprite));
                    Assert.That(paper.color, Is.EqualTo(previousPaper.color));
                }
                Rect bounds = LocalBounds(card);
                if (i > 0) Assert.That(bounds.xMin - previousRight, Is.EqualTo(20f).Within(.01f));
                Assert.That(bounds.xMin, Is.GreaterThan(-540f));
                Assert.That(bounds.xMax, Is.LessThan(540f));
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "TabLabel", "TabEffectSummary" })
                {
                    var label = card.Find(name).GetComponent<Text>();
                    AssertGeneratedTextFits(label, name == "TabLabel" ? 1 : 2);
                    Assert.That(label.fontSize, Is.EqualTo(56));
                    Assert.That(card.rect.width - label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(48f));
                }
                previousRight = bounds.xMax;
                previousPaper = paper;
            }
        }

        [Test]
        public void BrushRibbonPixelModelKeepsTextClearAndCornersSoft()
        {
            var sample = (System.Func<int, int, int, int, Color32>)typeof(InkUiTextureFactory)
                .GetMethod("SampleGrowthPaperRibbonPixel", BindingFlags.Static | BindingFlags.NonPublic)
                .CreateDelegate(typeof(System.Func<int, int, int, int, Color32>));
            int inkEdgePixels = 0;
            int softEdgePixels = 0;
            const int width = 240, height = 386;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 pixel = sample(x, y, width, height);
                if (pixel.a > 20 && pixel.a < 235) softEdgePixels++;
                if (pixel.a > 128 && pixel.r < 238) inkEdgePixels++;
                if (x >= 40 && x < width - 40 && y >= 40 && y < height - 40)
                {
                    Assert.That(pixel.a, Is.EqualTo(255), "글씨 뒤 한지에 구멍이 없어야 합니다.");
                    Assert.That(pixel.r, Is.GreaterThan(240), "먹선은 글씨 안쪽까지 침범하지 않습니다.");
                }
            }
            Assert.That(inkEdgePixels, Is.Zero, "받침에 별도 합성 테두리를 그리지 않습니다.");
            Assert.That(softEdgePixels, Is.GreaterThan(400));
            Assert.That(sample(2, 2, width, height).a, Is.Zero);
            Assert.That(sample(40, 40, width, height).a, Is.GreaterThan(200), "글씨를 담을 면적은 유지합니다.");
            Assert.That(sample(40, height / 2, width, height).a, Is.GreaterThan(200));
            Assert.That(sample(width / 2, height / 2, width, height), Is.EqualTo(sample(width / 2, height / 2, width, height)),
                "다시 그려도 바탕 노이즈가 움직이지 않아야 합니다.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RectangleFramePreservesTheCircularBrushTextureAndHasNoTextAreaMesh(int index)
        {
            var frame = Screen().Find($"ChoiceGrid/GrowthCard{index}/BrushFrame").GetComponent<GrowthRingFrameGraphic>();
            var circle = Screen().Find("GrowthOrnaments/FocusBrushRing").GetComponent<Image>();
            Assert.That(frame.mainTexture, Is.SameAs(circle.mainTexture));
            using var mesh = new VertexHelper();
            typeof(GrowthRingFrameGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                .Invoke(frame, new object[] { mesh });
            Assert.That(mesh.currentVertCount, Is.EqualTo((GrowthRingFrameGraphic.Segments + 1) * (GrowthRingFrameGraphic.RadialBands + 1)));
            Assert.That(mesh.currentVertCount, Is.LessThan(2500), "카드마다 고정 메시 한 장만 사용합니다.");
            Assert.That(mesh.currentVertCount * 4, Is.LessThan(10000));
            int opaque = 0;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                UIVertex vertex = default;
                mesh.PopulateUIVertex(ref vertex, i);
                Assert.That(Mathf.Abs(vertex.position.x), Is.LessThan(126f));
                Assert.That(Mathf.Abs(vertex.position.y), Is.LessThan(200f));
                Assert.That(Mathf.Abs(vertex.position.x) > 76f || Mathf.Abs(vertex.position.y) > 145f, Is.True,
                    "가운데 글씨 공간에는 테두리 메시가 들어오지 않습니다.");
                Assert.That(vertex.uv0.x, Is.InRange(-.03f, 1.03f));
                Assert.That(vertex.uv0.y, Is.InRange(-.03f, 1.03f));
                if (vertex.color.a == 0) continue;
                opaque++;
                Assert.That(vertex.color, Is.EqualTo((Color32)circle.color), "원형과 먹 농도를 공유합니다.");
            }
            Assert.That(opaque, Is.GreaterThan(1000));
        }

        [Test]
        public void RectangleFrameHasLongStraightSidesSmallCornersAndAClosedSeam()
        {
            var size = new Vector2(1000f, 386f);
            int straight = 0;
            for (int i = 0; i < 320; i++)
            {
                Vector2 point = GrowthRingFrameGraphic.PerimeterPoint(size, i / 320f, out Vector2 normal);
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(.001f));
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(500f));
                Assert.That(Mathf.Abs(point.y), Is.LessThanOrEqualTo(193f));
                if (Mathf.Abs(normal.x) < .001f || Mathf.Abs(normal.y) < .001f) straight++;
            }
            Assert.That(straight, Is.GreaterThan(280), "캡슐 대신 네모 네 변을 충분히 남깁니다.");
            Assert.That(GrowthRingFrameGraphic.PerimeterPoint(size, 0f, out var firstNormal),
                Is.EqualTo(GrowthRingFrameGraphic.PerimeterPoint(size, 1f, out var lastNormal)));
            Assert.That(firstNormal, Is.EqualTo(lastNormal));
        }

        [Test]
        public void SelectionWashMovesWithSelectionWithoutRebuildingOrChangingProgress()
        {
            Transform grid = Screen().Find("ChoiceGrid");
            int count = host.GetComponentsInChildren<Transform>(true).Length;
            int currency = PermanentGrowthProfile.Currency;
            int owned = PermanentGrowthProfile.OwnedNodeCount;
            for (int selected = 0; selected < 12; selected++)
            {
                view.SelectGrowthForTests(selected % 4);
                int visible = 0;
                for (int i = 0; i < 4; i++)
                {
                    Transform card = grid.Find($"GrowthCard{i}");
                    Image wash = card.Find("SelectionWash").GetComponent<Image>();
                    Assert.That(card.Find("SelectionEffectWash"), Is.Null,
                        "선택 번짐은 이름 뒤 한 개만 남깁니다.");
                    Assert.That(wash.raycastTarget, Is.False);
                    Assert.That(wash.color.a, Is.EqualTo(0.20f).Within(0.001f));
                    Assert.That(wash.transform.GetSiblingIndex(), Is.LessThan(card.Find("TabLabel").GetSiblingIndex()));
                    Assert.That(wash.rectTransform.anchoredPosition,
                        Is.EqualTo(((RectTransform)card.Find("TabLabel")).anchoredPosition));
                    Assert.That(wash.gameObject.activeSelf, Is.EqualTo(i == selected % 4));
                    Assert.That(LocalBounds(wash.rectTransform).yMax,
                        Is.LessThan((card as RectTransform).rect.yMax));
                    if (wash.gameObject.activeSelf) visible++;
                    Assert.That(card.Find("SelectionInk").GetComponent<Image>().sprite,
                        Is.SameAs(InkUiTextureFactory.CreateBrushSprite()));
                    Assert.That(card.Find("TabEffectSummary").GetComponent<Text>().fontSize, Is.EqualTo(56));
                    Text effect = card.Find("TabEffectSummary").GetComponent<Text>();
                    Assert.That(effect.fontStyle, Is.EqualTo(i == selected % 4 ? FontStyle.Bold : FontStyle.Normal));
                    AssertGeneratedTextFits(effect, 2);
                    Assert.That(card.Find("Paper").GetComponent<Image>().raycastTarget, Is.False);
                    Assert.That(card.Find("BrushFrame").GetComponent<Image>().raycastTarget, Is.False);
                    Button button = card.GetComponent<Button>();
                    Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.normalColor));
                    Assert.That(button.GetComponent<InkUiPressFeedback>(), Is.Not.Null);
                }
                Assert.That(visible, Is.EqualTo(1));
                Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            view.BuildForTests();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(currency));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(owned));
        }

        [Test]
        public void CostAboveButtonTracksEveryPriceCompletionAndRefund()
        {
            Text cost = Screen().Find("BottomActions/SelectedCost").GetComponent<Text>();
            Text action = view.PurchaseButton.transform.Find("Label").GetComponent<Text>();
            for (int index = 0; index < PermanentGrowthCatalog.Choices.Count; index++)
            {
                view.SelectGrowthForTests(index);
                PermanentGrowthType type = PermanentGrowthCatalog.Choices[index].Type;
                PermanentGrowthDefinition definition = PermanentGrowthCatalog.Get(type);
                for (int level = 0; level < definition.MaxLevel; level++)
                {
                    Assert.That(cost.text, Is.EqualTo($"먹빛 {definition.GetCost(level)}개 필요"));
                    Assert.That(cost.color, Is.EqualTo(InkPalette.TextDark));
                    Assert.That(action.text, Is.EqualTo("성장하기"));
                    Assert.That(view.PurchaseButton.interactable, Is.True);
                    Assert.That(PermanentGrowthProfile.TryPurchase(type), Is.True);
                }
                Assert.That(cost.text, Is.EqualTo("다 자랐어요"));
                Assert.That(view.PurchaseButton.interactable, Is.False);
                Assert.That(action.text, Is.EqualTo("성장하기"));
            }
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            Assert.That(cost.text, Is.EqualTo("먹빛 1개 필요"));
            Assert.That(view.PurchaseButton.interactable, Is.True);
        }

        [Test]
        public void InsufficientCurrencyAndRecoveryNeverOfferFreeGrowth()
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            view.BuildForTests();
            Text cost = Screen().Find("BottomActions/SelectedCost").GetComponent<Text>();
            Assert.That(cost.text, Is.EqualTo("먹빛 1개 필요"));
            Assert.That(cost.color, Is.EqualTo(InkPalette.Red));
            Assert.That(view.PurchaseButton.interactable, Is.False);
            view.PurchaseButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.Currency, Is.Zero);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);

            var broken = new MemoryPermanentGrowthStore { Json = "{broken" };
            PermanentGrowthProfile.UseStoreForTests(broken);
            view.BuildForTests();
            Assert.That(view.IsRecoveryPromptOpen, Is.True);
            Assert.That(cost.text, Is.EqualTo("저장 복구가 필요해요"));
            Assert.That(view.PurchaseButton.interactable, Is.False);
            view.PurchaseButton.onClick.Invoke();
            Assert.That(broken.Json, Is.EqualTo("{broken"));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
        }

        [Test]
        public void PurchaseLockPreventsDoubleSpendAndReturnsToOnePrimaryAction()
        {
            view.PurchaseButton.onClick.Invoke();
            int level = PermanentGrowthProfile.GetLevel(
                PermanentGrowthType.Vitality);
            view.PurchaseButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.GetLevel(
                PermanentGrowthType.Vitality), Is.EqualTo(level));
            Assert.That(view.PurchaseButton.interactable, Is.False);
            Assert.That(view.BackButton.interactable, Is.False);

            typeof(PermanentGrowthView).GetField(
                    "purchaseLockedUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(view, -1f);
            typeof(PermanentGrowthView).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);

            Assert.That(view.PurchaseButton.interactable, Is.True);
            Assert.That(view.BackButton.interactable, Is.True);
            Assert.That(PrimaryPurchaseActionCount(), Is.EqualTo(1));
        }

        [Test]
        public void LargerPrimaryControlsAndCompactResetRemainTouchable()
        {
            foreach (Button button in new[] { view.PurchaseButton })
            {
                Text label = button.GetComponentInChildren<Text>(true);
                Assert.That(label, Is.Not.Null);
                Assert.That(label.fontSize, Is.EqualTo(80));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(button.GetComponent<RectTransform>().sizeDelta.y,
                    Is.GreaterThanOrEqualTo(120f));
            }
            Assert.That(view.NodeResetButton
                .GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(200f, 120f)));
            Assert.That(view.BackButton.GetComponent<InkActionButtonVisual>(),
                Is.Null);
            Assert.That(view.PurchaseButton.GetComponent<InkActionButtonVisual>(),
                Is.Null);
            Assert.That(view.PurchaseButton.GetComponent<Image>(), Is.Null);
            RawImage brush = view.PurchaseButton.transform.Find("BrushBackground").GetComponent<RawImage>();
            Assert.That(brush.texture, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/UI/muk_start_button.png")));
            Assert.That(view.PurchaseButton.targetGraphic, Is.SameAs(brush));
            Assert.That(view.PurchaseButton.colors, Is.EqualTo(InkUiStyle.ReadableButtonColors()));
            Assert.That(view.PurchaseButton.GetComponent<InkUiPressFeedback>(), Is.Not.Null);
            Assert.That(view.PurchaseButton.transform.Find("Label").GetComponent<Text>().color,
                Is.EqualTo(InkPalette.TextLight));
            Assert.That(view.PurchaseButton.transform.Find("RoleWash"), Is.Null);
            Assert.That(view.NodeResetButton.GetComponent<InkActionButtonVisual>(),
                Is.Null);
            foreach (Button button in new[] { view.BackButton, view.NodeResetButton })
            {
                Image hitArea = button.GetComponent<Image>();
                Image icon = button.transform.Find("Icon").GetComponent<Image>();
                Assert.That(hitArea.sprite, Is.Null);
                Assert.That(hitArea.color.a, Is.Zero);
                Assert.That(hitArea.raycastTarget, Is.True);
                Assert.That(button.targetGraphic, Is.SameAs(icon));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(84f, 84f)));
                Assert.That(button.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(120f));
                Assert.That(button.GetComponentsInChildren<Text>(true), Is.Empty);
                Assert.That(button.colors.disabledColor.a, Is.EqualTo(.32f));
                Assert.That(button.GetComponent<InkUiPressFeedback>(), Is.Not.Null);
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            }
            Assert.That(view.BackButton.transform.Find("PrimaryAccent"), Is.Null);
            Assert.That(view.NodeResetButton.transform.Find("PrimaryAccent"), Is.Null);
        }

        [Test]
        public void LargerGrowthActionImmediatelySwitchesLanguagesWithoutClipping()
        {
            GameLanguage original = GameLocalization.Language;
            try
            {
                Text label = view.PurchaseButton.transform.Find("Label").GetComponent<Text>();
                RectTransform button = view.PurchaseButton.GetComponent<RectTransform>();
                RectTransform brush = view.PurchaseButton.transform.Find("BrushBackground") as RectTransform;
                Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(820f, 200f)));
                Assert.That(brush.sizeDelta, Is.EqualTo(button.sizeDelta));
                foreach (GameLanguage language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Korean })
                {
                    GameLocalization.SetLanguage(language);
                    Assert.That(label.text, Is.EqualTo(language == GameLanguage.Korean ? "성장하기" : "Upgrade"));
                    Assert.That(label.fontSize, Is.EqualTo(80));
                    Assert.That(label.resizeTextForBestFit, Is.False);
                    Assert.That(label.rectTransform.anchoredPosition.x, Is.Zero);
                    AssertGeneratedTextFits(label, 1);
                }
                Rect labelBounds = LocalBounds(label.rectTransform);
                Assert.That(labelBounds.yMin, Is.GreaterThan(button.rect.yMin));
                Assert.That(labelBounds.yMax, Is.LessThan(button.rect.yMax));
            }
            finally { GameLocalization.SetLanguage(original); }
        }

        [Test]
        public void GrowthNamesAndMiniBrushSwitchLanguagesWithoutChangingSavedTracks()
        {
            Sprite brush = Resources.Load<Sprite>(PermanentGrowthView.BrushIconResourcePath);
            Assert.That(brush, Is.Not.Null);
            Assert.That(PermanentGrowthCatalog.Choices.Select(choice => choice.Id),
                Is.EqualTo(new[] { "body", "ink", "brush", "jump" }));
            int wallet = PermanentGrowthProfile.Currency;
            foreach (GameLanguage language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Korean })
            {
                GameLocalization.SetLanguage(language);
                bool korean = language == GameLanguage.Korean;
                string[] labels = korean ? new[] { "먹", "먹물", "붓", "도약" } :
                    new[] { "Muk", "Ink", "Brush", "Jump" };
                string[] titles = korean ? new[] { "튼튼한 먹", "넉넉한 먹물", "알뜰한 붓", "높은 도약" } :
                    new[] { "Stronger Muk", "More Ink", "Thrifty Brush", "Higher Jump" };
                for (int i = 0; i < labels.Length; i++)
                {
                    Transform card = Screen().Find($"ChoiceGrid/GrowthCard{i}");
                    Text label = card.Find("TabLabel").GetComponent<Text>();
                    Assert.That(label.text, Is.EqualTo(labels[i]));
                    AssertGeneratedTextFits(label, 1);
                    AssertGeneratedTextFits(card.Find("TabEffectSummary").GetComponent<Text>(), 2);
                    view.SelectGrowthForTests(i);
                    Text title = Screen().Find("FocusedGrowth/FocusTitle").GetComponent<Text>();
                    Assert.That(title.text, Is.EqualTo(titles[i]));
                    AssertGeneratedTextFits(title, 1);
                    AssertGeneratedTextFits(Screen().Find("FocusedGrowth/FocusSummary").GetComponent<Text>(), 1);
                    if (i != 2) continue;
                    Image icon = card.Find("Icon").GetComponent<Image>();
                    Image focus = Screen().Find("FocusedGrowth/FocusIcon").GetComponent<Image>();
                    Assert.That(icon.sprite, Is.SameAs(brush));
                    Assert.That(focus.sprite, Is.SameAs(brush));
                    Assert.That(icon.preserveAspect && focus.preserveAspect, Is.True);
                    Assert.That(icon.raycastTarget || focus.raycastTarget, Is.False);
                }
            }
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(wallet));
        }

        [Test]
        public void GrowthTitleUsesCenteredBrushArtworkAndImmediatelyRestoresEnglishText()
        {
            GameLanguage original = GameLocalization.Language;
            try
            {
                Text title = Screen().Find("HeaderGroup/Title").GetComponent<Text>();
                RawImage artwork = title.transform.Find("TitleArtwork").GetComponent<RawImage>();
                Assert.That(artwork.texture, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Resources/MukJump/UI/PermanentGrowth/pg_title_growth_ko_v1.png")));
                Assert.That(artwork.raycastTarget, Is.False);
                Assert.That(artwork.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(artwork.rectTransform.rect.width, Is.LessThanOrEqualTo(title.rectTransform.rect.width));
                Assert.That(artwork.rectTransform.rect.height, Is.LessThanOrEqualTo(title.rectTransform.rect.height));
                Assert.That(artwork.rectTransform.rect.width / artwork.rectTransform.rect.height,
                    Is.EqualTo((float)artwork.texture.width / artwork.texture.height).Within(.001f));
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(artwork.enabled, Is.True);
                Assert.That(title.enabled, Is.False);
                GameLocalization.SetLanguage(GameLanguage.English);
                Assert.That(artwork.enabled, Is.False);
                Assert.That(title.enabled, Is.True);
                Assert.That(title.text, Is.EqualTo("Growth"));
                GameLocalization.SetLanguage(GameLanguage.Korean);
                Assert.That(artwork.enabled, Is.True);
                Assert.That(title.enabled, Is.False);
            }
            finally { GameLocalization.SetLanguage(original); }
        }

        [Test]
        public void ResetIconOnlyOpensPopupAndExplicitConfirmationRefundsOnce()
        {
            int currency = PermanentGrowthProfile.Currency;
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            Image icon = view.NodeResetButton.transform.Find("Icon").GetComponent<Image>();
            Assert.That(view.NodeResetButton.interactable, Is.True);
            int saves = store.SaveCount;
            long distance = PermanentGrowthProfile.CumulativeDistanceMeters;
            view.NodeResetButton.onClick.Invoke();
            Assert.That(view.IsResetConfirmationOpen, Is.True);
            Assert.That(icon.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthResetIconSprite()));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(saves));
            Assert.That(view.PurchaseButton.interactable, Is.False);
            Assert.That(view.BackButton.interactable, Is.False);
            Assert.That(view.NodeResetButton.interactable, Is.False);

            // 뒤쪽 아이콘/구매를 재호출해도 확인 팝업을 우회하지 못한다.
            view.NodeResetButton.onClick.Invoke();
            view.PurchaseButton.onClick.Invoke();
            PermanentGrowthType selected = view.SelectedGrowthType;
            view.SelectGrowthForTests(3);
            Assert.That(view.SelectedGrowthType, Is.EqualTo(selected));
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(store.SaveCount, Is.EqualTo(saves));

            view.ConfirmResetButton.onClick.Invoke();
            view.ConfirmResetButton.onClick.Invoke();
            Assert.That(view.IsResetConfirmationOpen, Is.False);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(currency));
            Assert.That(PermanentGrowthProfile.CumulativeDistanceMeters, Is.EqualTo(distance));
            Assert.That(store.SaveCount, Is.EqualTo(saves + 1));
            Assert.That(icon.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthResetIconSprite()));
            Assert.That(icon.color, Is.EqualTo(InkPalette.TextDark));
            Assert.That(view.NodeResetButton.interactable, Is.False);
            Assert.That(view.PurchaseButton.interactable, Is.True);
            Assert.That(Screen().Find("BottomActions/SelectedCost").GetComponent<Text>().text,
                Is.EqualTo("먹빛 1개 필요"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelOrOutsideDimClosesResetWithoutChangingGrowth(bool outside)
        {
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            int currency = PermanentGrowthProfile.Currency;
            int saves = store.SaveCount;
            view.NodeResetButton.onClick.Invoke();
            (outside ? view.ResetDimmerButton : view.CancelResetButton).onClick.Invoke();
            view.ConfirmResetButton.onClick.Invoke();
            Assert.That(view.IsResetConfirmationOpen, Is.False);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(currency));
            Assert.That(store.SaveCount, Is.EqualTo(saves));
            Image icon = view.NodeResetButton.transform.Find("Icon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.SameAs(InkUiTextureFactory.CreateGrowthResetIconSprite()));
            Assert.That(icon.color, Is.EqualTo(InkPalette.TextDark));
            Assert.That(view.NodeResetButton.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(view.PurchaseButton.interactable, Is.True);
        }

        [Test]
        public void ClosingGrowthDiscardsAnUnconfirmedReset()
        {
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            view.NodeResetButton.onClick.Invoke();
            view.Close();
            view.ConfirmResetButton.onClick.Invoke();
            Assert.That(view.IsResetConfirmationOpen, Is.False);
            Assert.That(view.ScreenRoot.Find("GrowthResetConfirmation").gameObject.activeSelf, Is.False);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedResetKeepsGrowthAndRequiresRecoveryBeforeRetry()
        {
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            int currency = PermanentGrowthProfile.Currency;
            view.NodeResetButton.onClick.Invoke();
            store.ThrowOnPrimarySave = true;
            try { view.ConfirmResetButton.onClick.Invoke(); }
            finally { store.ThrowOnPrimarySave = false; }
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            Assert.That(PermanentGrowthProfile.Currency, Is.EqualTo(currency));
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            Assert.That(view.IsResetConfirmationOpen, Is.False);
            Assert.That(view.ConfirmResetButton.interactable, Is.False);
            Assert.That(view.IsRecoveryPromptOpen, Is.True);
            Assert.That(view.RestoreBackupButton.gameObject.activeSelf, Is.True);
            view.RestoreBackupButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.EqualTo(1));
            view.NodeResetButton.onClick.Invoke();
            view.ConfirmResetButton.onClick.Invoke();
            Assert.That(PermanentGrowthProfile.OwnedNodeCount, Is.Zero);
            Assert.That(view.IsResetConfirmationOpen, Is.False);
        }

        [Test]
        public void ResetPopupUsesHanjiAndFitsImmediatelyLocalizedCopy()
        {
            Assert.That(PermanentGrowthProfile.TryPurchase(PermanentGrowthType.JumpHeight), Is.True);
            view.NodeResetButton.onClick.Invoke();
            RectTransform panel = view.ScreenRoot.Find(
                "GrowthResetConfirmation/SafeAreaRoot/PopupContent/ResetPanel") as RectTransform;
            Assert.That(panel.GetComponent<HanjiScrollFrame>(), Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(view.ResetDimmerButton.transform.parent, Is.SameAs(panel.parent.parent.parent));
            Assert.That(view.ConfirmResetButton.GetComponent<Image>().sprite, Is.SameAs(InkUiStyle.ActionButtonSprite));
            Rect cancel = LocalBounds(view.CancelResetButton.transform as RectTransform);
            Rect confirm = LocalBounds(view.ConfirmResetButton.transform as RectTransform);
            Assert.That(confirm.xMin - cancel.xMax, Is.GreaterThanOrEqualTo(40f));
            Assert.That(confirm.yMin - (-280f), Is.GreaterThanOrEqualTo(50f));
            GameLanguage original = GameLocalization.Language;
            try
            {
                foreach (GameLanguage language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Korean })
                {
                    GameLocalization.SetLanguage(language);
                    Assert.That(panel.Find("ResetTitle").GetComponent<Text>().text,
                        Is.EqualTo(language == GameLanguage.Korean ? "초기화하시겠습니까?" : "Reset your upgrades?"));
                    Assert.That(view.CancelResetButton.GetComponentInChildren<Text>(true).text,
                        Is.EqualTo(language == GameLanguage.Korean ? "취소" : "Cancel"));
                    Assert.That(view.ConfirmResetButton.GetComponentInChildren<Text>(true).text,
                        Is.EqualTo(language == GameLanguage.Korean ? "초기화" : "Reset"));
                    foreach (Text label in panel.GetComponentsInChildren<Text>(true))
                    {
                        if (language == GameLanguage.English)
                            Assert.That(label.text, Does.Not.Match("[가-힣]"), label.name);
                        AssertGeneratedTextFits(label, 1);
                    }
                }
            }
            finally { GameLocalization.SetLanguage(original); }
        }

        [Test]
        public void GrowthScreenUsesLargeTypeWithoutAutomaticShrinking()
        {
            foreach (Text text in host.GetComponentsInChildren<Text>(true))
            {
                bool isTabEffect = text.name == "TabEffectSummary";
                bool isCompactReset = text == view.NodeResetButton
                    .transform.Find("Label")?.GetComponent<Text>();
                bool isCompactHelp = text.name == "Detail" && text.transform.parent.name == "BalanceHelp";
                int minimum = isCompactHelp ? 36 : isTabEffect
                    ? 56
                    : isCompactReset
                        ? 44
                        : 40;
                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimum),
                    $"{text.name} 글씨가 합의한 최소 크기보다 작습니다.");
                Assert.That(text.resizeTextForBestFit, Is.False,
                    $"{text.name} 자동 축소를 쓰면 안 됩니다.");
            }
        }

        [Test]
        public void LargeEffectLinesAndDynamicActionLabelsNeverClip()
        {
            foreach (Text label in Screen().GetComponentsInChildren<Text>(true))
            {
                if (label.name == "TabEffectSummary")
                    AssertGeneratedTextFits(label, 2);
                else if (label.name == "Title" || label.name == "TabLabel")
                    AssertGeneratedTextFits(label, 1);
            }
            for (int i = 0; i < 4; i++)
            {
                view.SelectGrowthForTests(i);
                AssertGeneratedTextFits(Screen().Find("FocusedGrowth/FocusTitle").GetComponent<Text>(), 1);
                AssertGeneratedTextFits(Screen().Find("FocusedGrowth/FocusSummary").GetComponent<Text>(), 1);
            }
            Text purchase = view.PurchaseButton.transform.Find("Label").GetComponent<Text>();
            AssertGeneratedTextFits(Screen().Find("FocusedGrowth/FocusCompleteLabel").GetComponent<Text>(), 1);
            Assert.That(purchase.text, Is.EqualTo("성장하기"));
            AssertGeneratedTextFits(purchase, 1);
            Assert.That(view.BackButton.transform.Find("Label"), Is.Null);
            Assert.That(view.NodeResetButton.transform.Find("Label"), Is.Null);
            Text cost = Screen().Find("BottomActions/SelectedCost").GetComponent<Text>();
            foreach (string value in new[] { "먹빛 1개 필요", "먹빛 2개 필요", "먹빛 3개 필요", "먹빛 5개 필요", "먹빛 7개 필요", "먹빛 10개 필요", "먹빛 14개 필요", "먹빛 19개 필요", "다 자랐어요", "힘을 선택하세요", "저장 복구가 필요해요" })
            {
                cost.text = value;
                AssertGeneratedTextFits(cost, 1);
            }
        }

        [Test]
        public void TabsAndActionsKeepSeparateUnclippedTouchAreas()
        {
            Transform grid = Screen().Find("ChoiceGrid");
            Rect summary = LocalBounds(Screen().Find("FocusedGrowth/FocusSummary") as RectTransform);
            Rect complete = LocalBounds(Screen().Find("FocusedGrowth/FocusCompleteLabel") as RectTransform);
            Assert.That(complete.yMax, Is.LessThan(summary.yMin));
            Assert.That(complete.yMin, Is.GreaterThan(LocalBounds(grid.Find("GrowthCard0") as RectTransform).yMax));
            for (int i = 0; i < 4; i++)
            {
                RectTransform card = grid.Find($"GrowthCard{i}") as RectTransform;
                Rect previous = default;
                foreach (string name in new[] { "Icon", "TabLabel", "SelectionInk", "TabEffectSummary" })
                {
                    RectTransform item = card.Find(name) as RectTransform;
                    Rect bounds = LocalBounds(item);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(card.rect.xMin));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(card.rect.xMax));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(card.rect.yMin));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(card.rect.yMax));
                    if (name != "Icon") Assert.That(bounds.yMax, Is.LessThan(previous.yMin));
                    previous = bounds;
                }
                Assert.That(LocalBounds(card).yMin,
                    Is.GreaterThan(LocalBounds(view.PurchaseButton.transform as RectTransform).yMax));
            }
            Rect purchase = LocalBounds(view.PurchaseButton.transform as RectTransform);
            Rect reset = LocalBounds(view.NodeResetButton.transform as RectTransform);
            Rect cost = LocalBounds(Screen().Find("BottomActions/SelectedCost") as RectTransform);
            float ribbonBottom = LocalBounds(Screen().Find("ChoiceGrid/GrowthCard0") as RectTransform).yMin;
            Assert.That(cost.yMax, Is.LessThan(ribbonBottom));
            Assert.That(cost.yMin - purchase.yMax, Is.GreaterThanOrEqualTo(30f));
            Assert.That(cost.center.x, Is.Zero);
            Assert.That(cost.xMin, Is.GreaterThan(-540f));
            Assert.That(reset.xMax, Is.LessThan(540f));
            Assert.That(purchase.yMin, Is.GreaterThan(-960f));
            Rect back = LocalBounds(view.BackButton.transform as RectTransform);
            Rect title = LocalBounds(Screen().Find("HeaderGroup/Title") as RectTransform);
            Rect balance = LocalBounds(Screen().Find("HeaderGroup/BalanceHud") as RectTransform);
            Assert.That(back.xMin, Is.GreaterThan(-540f));
            Assert.That(back.xMax, Is.LessThan(title.xMin));
            Assert.That(title.xMax, Is.LessThan(balance.xMin));
            Assert.That(reset.center.y, Is.EqualTo(title.center.y));
            Assert.That(reset.xMin, Is.GreaterThan(title.xMax));
            Assert.That(reset.yMax, Is.LessThan(960f));
            Assert.That(reset.yMin - balance.yMax, Is.GreaterThanOrEqualTo(20f));
            Assert.That(reset.xMax, Is.EqualTo(balance.xMax));
        }

        [TestCase(1080, 1920, 3f, 24, 24)]
        [TestCase(1179, 2556, 3f, 102, 177)]
        [TestCase(1440, 3200, 4f, 96, 120)]
        public void TypographyRemainsLargeAtPhoneSizeAndRendersWithoutClipping(
            int width, int height, float deviceScale, int bottom, int top)
        {
            Rect safe = new Rect(0, bottom, width, height - bottom - top);
            Canvas runtimeCanvas = host.GetComponentInChildren<Canvas>(true);
            CanvasScaler runtimeScaler = runtimeCanvas.GetComponent<CanvasScaler>();
            Assert.That(runtimeCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(runtimeScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(runtimeScaler.referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            Assert.That(runtimeScaler.matchWidthOrHeight, Is.EqualTo(1f));
            if (UnityEngine.Screen.width > 0 && UnityEngine.Screen.height > 0)
            {
                float currentFit = MobileUiLayout.CalculateFitScale(new Vector2(1080, 1920),
                    PermanentGrowthView.CalculateContentSafeArea(MobileUiLayout.CurrentSafeArea,
                        UnityEngine.Screen.width, UnityEngine.Screen.height),
                    UnityEngine.Screen.width, UnityEngine.Screen.height, Vector2.zero);
                Assert.That(Screen().localScale, Is.EqualTo(Vector3.one * currentFit));
            }
            float fit = MobileUiLayout.CalculateFitScale(new Vector2(1080, 1920), safe, width, height, Vector2.zero);
            float canvasScale = height / 1920f;
            Text effect = Screen().Find("ChoiceGrid/GrowthCard0/TabEffectSummary").GetComponent<Text>();
            Text purchase = view.PurchaseButton.transform.Find("Label").GetComponent<Text>();
            Assert.That(effect.fontSize * fit * canvasScale / deviceScale, Is.GreaterThanOrEqualTo(18f));
            Assert.That(purchase.fontSize * fit * canvasScale / deviceScale, Is.GreaterThanOrEqualTo(20f));
            RectTransform progress = Screen().Find("FocusedGrowth/FocusGrowthMark0") as RectTransform;
            Assert.That(progress.sizeDelta.y * fit * canvasScale / deviceScale,
                Is.GreaterThanOrEqualTo(20f), "성장 진행 먹방울이 휴대폰에서 다시 작아지면 안 됩니다.");

            // 저장된 씬·사용자 재화는 건드리지 않고 실제 UI/폰트를 격리된 프리뷰 씬에 렌더한다.
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            view.SelectGrowthForTests(2);
            var scene = MukJumpSceneBuilder.BuildForTests();
            try
            {
                Camera camera = scene.GetRootGameObjects().Select(go => go.GetComponent<Camera>())
                    .First(value => value != null);
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != camera.gameObject) root.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                camera.scene = scene;
                camera.enabled = false;
                camera.aspect = width / (float)height;
                Canvas canvas = host.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.enabled = true;
                RectTransform canvasRect = canvas.transform as RectTransform;
                canvasRect.sizeDelta = new Vector2(width / canvasScale, 1920f);
                canvasRect.localScale = Vector3.one * (camera.orthographicSize * 2f / 1920f);
                canvasRect.position = camera.transform.position + Vector3.forward;
                view.ScreenRoot.anchoredPosition = Vector2.zero;
                canvas.GetComponent<CanvasGroup>().alpha = 1f;
                var safeRoot = view.ScreenRoot.Find("SafeAreaRoot") as RectTransform;
                MobileUiLayout.ApplySafeArea(safeRoot, safe, width, height);
                Screen().localScale = Vector3.one * fit;
                Canvas.ForceUpdateCanvases();
                string directory = System.IO.Path.GetFullPath("output/growth-readability");
                System.IO.Directory.CreateDirectory(directory);
                MukJumpAmbientCloudPreview.Save(camera, width, height, null);
                MukJumpAmbientCloudPreview.Save(camera, width, height,
                    System.IO.Path.Combine(directory, $"growth-{width}x{height}.png"));
            }
            finally { MukJumpSceneBuilder.CloseTestScene(scene); }
        }

        static Rect LocalBounds(RectTransform rect) => new Rect(rect.anchoredPosition - rect.sizeDelta * 0.5f, rect.sizeDelta);

        static void AssertGeneratedTextFits(Text label, int lines)
        {
            var generator = new TextGenerator();
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.updateBounds = true;
            Assert.That(generator.Populate(label.text, settings), Is.True, label.name);
            Assert.That(generator.lineCount, Is.EqualTo(lines), label.name + ": " + label.text);
            Assert.That(generator.characterCountVisible, Is.EqualTo(label.text.Length), label.name);
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 0.01f), label.name);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.01f), label.name);
            Assert.That(label.resizeTextForBestFit, Is.False);
        }

        Transform Screen() => view.transform.Find(
            "PermanentGrowthCanvas/ScreenRoot/SafeAreaRoot/" +
            "PermanentGrowthScreen");

        void AssertRect(string path, Vector2 position, Vector2 size)
        {
            RectTransform rect = Screen().Find(path) as RectTransform;
            Assert.That(rect, Is.Not.Null, path);
            Assert.That(rect.anchoredPosition, Is.EqualTo(position), path);
            Assert.That(rect.sizeDelta, Is.EqualTo(size), path);
        }

        int PrimaryPurchaseActionCount()
        {
            RawImage image = view.PurchaseButton.targetGraphic as RawImage;
            return view.PurchaseButton.interactable &&
                   image != null && image.texture != null &&
                   view.PurchaseButton.transform.Find("RoleWash") == null
                ? 1
                : 0;
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.001f &&
            Mathf.Abs(a.g - b.g) < 0.001f &&
            Mathf.Abs(a.b - b.b) < 0.001f &&
            Mathf.Abs(a.a - b.a) < 0.001f;
    }
}
