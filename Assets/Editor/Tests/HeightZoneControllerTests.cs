using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MukJump.Core;

public class HeightZoneControllerTests
{
    static readonly string[] EndlessMapPaths =
    {
        "Assets/Resources/MukJump/Background/Endless/map_04_ink_galaxy_gate.png",
        "Assets/Resources/MukJump/Background/Endless/map_05_celestial_lotus.png",
        "Assets/Resources/MukJump/Background/Endless/map_06_heavenly_ink_river.png",
    };

    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 4)]
    [TestCase(5, 5)]
    [TestCase(6, 6)]
    [TestCase(7, 4)]
    [TestCase(8, 5)]
    [TestCase(9, 6)]
    public void EndlessMapsLoopAfterBaseStages(int band, int expectedStage)
    {
        Assert.AreEqual(expectedStage,
            HeightZoneController.ResolveMapStage(band, 4, 3));
    }

    [TestCase(4, false)]
    [TestCase(6, false)]
    [TestCase(7, true)]
    [TestCase(9, true)]
    [TestCase(10, false)]
    public void EverySecondEndlessCycleIsMirrored(int band, bool expected)
    {
        Assert.AreEqual(expected,
            HeightZoneController.ResolveMapMirror(band, 4, 3));
    }

    [Test]
    public void MissingEndlessMapsFallsBackToLastBaseStage()
    {
        Assert.AreEqual(3, HeightZoneController.ResolveMapStage(4, 4, 0));
        Assert.AreEqual(3, HeightZoneController.ResolveMapStage(99, 4, 0));
        Assert.IsFalse(HeightZoneController.ResolveMapMirror(99, 4, 0));
    }

    [Test]
    public void EndlessMapAssetsUsePortraitBackgroundImportSettings()
    {
        for (int i = 0; i < EndlessMapPaths.Length; i++)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(EndlessMapPaths[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EndlessMapPaths[i]);
            var importer = AssetImporter.GetAtPath(EndlessMapPaths[i]) as TextureImporter;

            Assert.IsNotNull(texture, EndlessMapPaths[i]);
            Assert.IsNotNull(sprite, EndlessMapPaths[i]);
            Assert.IsNotNull(importer, EndlessMapPaths[i]);
            Assert.AreEqual(1080, texture.width);
            Assert.AreEqual(1920, texture.height);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f).Within(0.01f));
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
        }
    }
}
