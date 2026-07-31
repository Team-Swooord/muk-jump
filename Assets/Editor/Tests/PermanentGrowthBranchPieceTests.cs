using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTools.Tests
{
    public sealed class PermanentGrowthBranchPieceTests
    {
        const string Root =
            "Assets/Resources/MukJump/UI/PermanentGrowth/";

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void ModularBranchPiece_IsTransparentMobileSprite(int index)
        {
            string fileName = $"pg_branch_piece_{index:00}.png";
            string path = Root + fileName;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Sprite>(path),
                Is.Not.Null,
                $"{fileName}은 Resources.Load 가능한 단일 Sprite여야 합니다.");
        }
    }
}
