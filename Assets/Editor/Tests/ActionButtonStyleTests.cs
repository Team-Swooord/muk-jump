using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class ActionButtonStyleTests
    {
        const string AssetPath =
            "Assets/Resources/MukJump/UI/Common/action_button_brush.png";

        [Test]
        public void SharedBrushUsesMobileUiSpriteSettings()
        {
            var importer =
                AssetImporter.GetAtPath(AssetPath) as TextureImporter;
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
                settings.spriteBorder.x,
                Is.GreaterThan(0f));
            Assert.That(
                settings.spriteBorder.z,
                Is.GreaterThan(0f));
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
                AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
            Sprite resourceSprite =
                Resources.Load<Sprite>(InkUiStyle.ActionButtonResourcePath);
            Assert.That(assetSprite, Is.Not.Null);
            Assert.That(resourceSprite, Is.SameAs(assetSprite));
        }

        [Test]
        public void ActionButtonUsesSharedSlicedSpriteAndReadableLabel()
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

                InkUiStyle.ConfigureActionButton(button, image, label);

                Assert.That(
                    image.sprite,
                    Is.SameAs(InkUiStyle.ActionButtonSprite));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(button.targetGraphic, Is.SameAs(image));
                Assert.That(
                    label.color,
                    Is.EqualTo(InkPalette.TextLight));
                Assert.That(
                    root.GetComponent<InkUiPressFeedback>(),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
