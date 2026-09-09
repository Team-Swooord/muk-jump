using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class ActionButtonStyleTests
    {
        const string LegacyAssetPath =
            "Assets/Resources/MukJump/UI/Common/action_button_brush.png";
        const string HanjiAssetPath =
            "Assets/Resources/MukJump/UI/Common/action_button_hanji_v1.png";

        [Test]
        public void LegacyBrushAssetKeepsStableImportAndLicenseSurface()
        {
            var importer =
                AssetImporter.GetAtPath(LegacyAssetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(
                importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(
                importer.spriteImportMode,
                Is.EqualTo(SpriteImportMode.Single));

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(
                settings.spriteMeshType,
                Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(
                settings.spriteBorder,
                Is.EqualTo(Vector4.zero));
            Assert.That(
                importer.spritePixelsPerUnit,
                Is.EqualTo(100f).Within(0.001f));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(
                importer.npotScale,
                Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(
                importer.wrapMode,
                Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(
                importer.filterMode,
                Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(importer.compressionQuality, Is.EqualTo(100));

            Sprite assetSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(LegacyAssetPath);
            Assert.That(assetSprite, Is.Not.Null);
        }

        [Test]
        public void HanjiActionAssetUsesSlicedTransparentUiImport()
        {
            var importer =
                AssetImporter.GetAtPath(HanjiAssetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode,
                Is.EqualTo(SpriteImportMode.Single));

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType,
                Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(settings.spriteBorder,
                Is.EqualTo(new Vector4(64f, 48f, 64f, 48f)));
            Assert.That(importer.spritePixelsPerUnit,
                Is.EqualTo(100f).Within(0.001f));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale,
                Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(importer.compressionQuality, Is.EqualTo(100));

            Sprite assetSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(HanjiAssetPath);
            Sprite resourceSprite =
                Resources.Load<Sprite>(InkUiStyle.ActionButtonResourcePath);
            Assert.That(assetSprite, Is.Not.Null);
            Assert.That(resourceSprite, Is.SameAs(assetSprite));
            Assert.That(assetSprite.border,
                Is.EqualTo(new Vector4(64f, 48f, 64f, 48f)));
            Assert.That(assetSprite.rect.width, Is.EqualTo(1559f));
            Assert.That(assetSprite.rect.height, Is.EqualTo(457f));
            Assert.That(assetSprite.pivot,
                Is.EqualTo(new Vector2(779.5f, 228.5f)));

            Vector4 border = assetSprite.border;
            Assert.That(border.x + border.z,
                Is.LessThan(156f),
                "가장 작은 공통 버튼 외곽에도 가로 중앙 한지결이 남아야 합니다.");
            Assert.That(border.y + border.w,
                Is.LessThan(120f),
                "가장 작은 공통 버튼 외곽에도 세로 중앙 한지결이 남아야 합니다.");
        }

        [Test]
        public void ActionButtonUsesOneQuietHanjiSheetInteriorWashAndFixedLabel()
        {
            var root = new GameObject(
                "ActionButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(root.transform, false);

            try
            {
                var image = root.GetComponent<Image>();
                var button = root.GetComponent<Button>();
                var label = labelObject.GetComponent<Text>();
                var buttonRect = root.GetComponent<RectTransform>();
                var labelRect = labelObject.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(344f, 120f);

                InkUiStyle.ConfigureActionButton(
                    button,
                    image,
                    label,
                    ActionButtonRole.Primary);

                Assert.That(image.sprite, Is.SameAs(InkUiStyle.ActionButtonSprite));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(image.color, Is.EqualTo(Color.Lerp(InkUiStyle.HanjiPaperColor, InkPalette.Paper2, .12f)));
                Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(InkUiStyle.HanjiPixelsPerUnitMultiplier));
                Assert.That(button.targetGraphic, Is.SameAs(image));
                Assert.That(root.transform.Find("Surface"), Is.Null);
                Assert.That(button.colors.normalColor, Is.EqualTo(Color.white));
                Assert.That(image.canvasRenderer.GetColor(),
                    Is.EqualTo(Color.white),
                    "첫 렌더 프레임부터 원본 한지색을 보존해야 합니다.");
                Outline roleOutline = root.GetComponent<Outline>();
                Assert.That(roleOutline, Is.Null);
                var wash = root.transform.Find("RoleWash").GetComponent<Image>();
                Assert.That(wash.color.a, Is.EqualTo(0.075f));
                Assert.That(wash.raycastTarget, Is.False);
                Assert.That(wash.enabled, Is.True);
                Assert.That(wash.rectTransform.anchorMin.x, Is.GreaterThan(0));
                Assert.That(wash.rectTransform.anchorMax.x, Is.LessThan(1));
                Assert.That((wash.rectTransform.anchorMin + wash.rectTransform.anchorMax) * 0.5f,
                    Is.EqualTo(new Vector2(0.5f, 0.5f)), "번짐은 글자 뒤 중앙에 놓습니다.");
                Assert.That(wash.transform.GetSiblingIndex(), Is.LessThan(label.transform.GetSiblingIndex()));
                Assert.That(root.GetComponent<Mask>(), Is.Null,
                    "알파 마스크는 한지 섬유를 딱딱한 밝은 테두리로 만들 수 있습니다.");
                Assert.That(
                    root.GetComponentsInChildren<Image>(true).Count(
                        item => item.sprite == InkUiStyle.ActionButtonSprite),
                    Is.EqualTo(1),
                    "뜯긴 한지 외곽을 두 겹 그리면 좌우 중앙이 갈라져 보입니다.");
                Assert.That(
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                        image.gameObject),
                    Is.SameAs(root),
                    "한지 전 영역에서 부모 Button의 클릭 경로를 찾아야 합니다.");
                Assert.That(
                    label.color,
                    Is.EqualTo(InkPalette.TextDark));
                Assert.That(
                    label.fontSize,
                    Is.EqualTo(InkUiStyle.ActionButtonLabelSize));
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Bold));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(label.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(
                    root.GetComponent<InkUiPressFeedback>(),
                    Is.Not.Null);
                Assert.That(
                    button.colors.disabledColor.a,
                    Is.GreaterThanOrEqualTo(0.8f),
                    "비활성 버튼도 한지 위에서 라벨을 읽을 수 있어야 합니다.");
                Assert.That(labelRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(
                    (buttonRect.sizeDelta.x - labelRect.sizeDelta.x) * 0.5f,
                    Is.EqualTo(32f).Within(0.001f));
                Assert.That(
                    (buttonRect.sizeDelta.y - labelRect.sizeDelta.y) * 0.5f,
                    Is.EqualTo(20f).Within(0.001f));
                Assert.That(root.GetComponents<InkActionButtonVisual>().Length,
                    Is.EqualTo(1));

                InkUiStyle.ConfigureActionButton(
                    button,
                    image,
                    label,
                    ActionButtonRole.Secondary);
                Assert.That(root.transform.Find("Surface"), Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(4),
                    "번짐 두 장·라벨·갈필 프레임 한 장을 재사용한다.");
                Assert.That(root.GetComponents<Outline>(), Is.Empty);
                Assert.That(image.color, Is.EqualTo(InkUiStyle.HanjiPaperColor));
                Assert.That(wash.enabled, Is.False);
                InkUiStyle.SetActionButtonRole(image, ActionButtonRole.Primary);
                Assert.That(root.transform.Find("RoleWash").GetComponent<Image>(), Is.SameAs(wash));
                Assert.That(wash.enabled, Is.True);
                InkUiStyle.SetActionButtonRole(image, ActionButtonRole.Secondary);
                Assert.That(
                    root.GetComponent<InkActionButtonVisual>().Role,
                    Is.EqualTo(ActionButtonRole.Secondary));
                Assert.That(InkUiStyle.UsesActionButtonSprite(image), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LegacyPaperActionFitsActualCopyAndWrapsLongerCopyWithoutClipping()
        {
            var root = new GameObject(
                "LegacyPaperAction",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            var paperObject = new GameObject(
                "Paper",
                typeof(RectTransform),
                typeof(Image));
            paperObject.transform.SetParent(root.transform, false);
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(paperObject.transform, false);

            try
            {
                root.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(340f, 120f);
                var paper = paperObject.GetComponent<Image>();
                var label = labelObject.GetComponent<Text>();
                label.text = "로컬 게스트로 돌아가기";

                InkUiStyle.ConfigureActionButton(
                    root.GetComponent<Button>(),
                    root.GetComponent<Image>(),
                    label,
                    ActionButtonRole.Secondary,
                    ActionButtonLayout.TwoLine,
                    obsoleteSurface: paper);

                var visual = root.GetComponent<InkActionButtonVisual>();
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.Surface, Is.Null);
                Assert.That(root.GetComponent<Button>().targetGraphic,
                    Is.SameAs(root.GetComponent<Image>()));
                Assert.That(root.GetComponent<RectTransform>().sizeDelta.y,
                    Is.EqualTo(InkUiStyle.ActionButtonTwoLineHeight));
                Assert.That(label.fontSize,
                    Is.EqualTo(InkUiStyle.ActionButtonLabelSize));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(label.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(label.preferredWidth,
                    Is.LessThanOrEqualTo(label.rectTransform.sizeDelta.x),
                    "실제 계정 복귀 문구는 340px 버튼에서 한 줄로 보여야 합니다.");
                label.text = "서버 기록을 포기하고 로컬 게스트로 돌아가기";
                Assert.That(label.preferredWidth,
                    Is.GreaterThan(label.rectTransform.sizeDelta.x),
                    "더 긴 상태 문구는 글자를 줄이지 않고 자동 줄바꿈해야 합니다.");
                Assert.That(label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.sizeDelta.y));
                Assert.That(paper.color, Is.EqualTo(Color.clear));
                Assert.That(paper.raycastTarget, Is.False);
                Assert.That(root.transform.Find("Surface"), Is.Null,
                    "스타일 갱신이 별도 한지 면을 생성하면 안 됩니다.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TwoLineActionButtonUsesFixedFortyPixelTextAndExpandedHeight()
        {
            var root = new GameObject(
                "TwoLineActionButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(root.transform, false);

            try
            {
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(320f, 104f);
                var label = labelObject.GetComponent<Text>();
                label.text = "광고 보고 부활\n50m 점프 · 방어막";

                InkUiStyle.ConfigureActionButton(
                    root.GetComponent<Button>(),
                    root.GetComponent<Image>(),
                    label,
                    ActionButtonRole.Primary,
                    ActionButtonLayout.TwoLine);

                Assert.That(rect.sizeDelta.y,
                    Is.EqualTo(InkUiStyle.ActionButtonTwoLineHeight));
                Assert.That(label.fontSize,
                    Is.EqualTo(InkUiStyle.ActionButtonLabelSize));
                Assert.That(label.lineSpacing, Is.EqualTo(0.92f).Within(0.001f));
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(label.horizontalOverflow,
                    Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(label.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(264f, 120f)));
                string[] lines = label.text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var generator = new TextGenerator();
                    float preferred = generator.GetPreferredWidth(
                        lines[i],
                        label.GetGenerationSettings(Vector2.zero));
                    Assert.That(preferred,
                        Is.LessThanOrEqualTo(label.rectTransform.sizeDelta.x),
                        $"{lines[i]} 문구가 40px에서 버튼 밖으로 나가면 안 됩니다.");
                }
                Assert.That(
                    label.preferredHeight,
                    Is.LessThanOrEqualTo(label.rectTransform.sizeDelta.y));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExplicitNormalLabelStyleSurvivesRuntimeLayoutRefresh()
        {
            var root = new GameObject(
                "NormalHanjiAction",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(root.transform, false);

            try
            {
                root.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(600f, 152f);
                var image = root.GetComponent<Image>();
                var label = labelObject.GetComponent<Text>();
                InkUiStyle.ConfigureActionButton(
                    root.GetComponent<Button>(),
                    image,
                    label,
                    ActionButtonRole.Primary,
                    ActionButtonLayout.TwoLine,
                    labelStyle: FontStyle.Normal);

                InkUiStyle.SetActionButtonLayout(
                    image,
                    ActionButtonLayout.SingleLine);
                InkUiStyle.RefreshActionButtonLayout(image);

                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(root.GetComponent<InkActionButtonVisual>().LabelStyle,
                    Is.EqualTo(FontStyle.Normal));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
