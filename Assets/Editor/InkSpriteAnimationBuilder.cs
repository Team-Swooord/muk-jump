using System;
using System.Collections.Generic;
using System.Linq;
using MukJump.Core;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// Main 씬 빌더가 관리하는 원화 슬라이싱. PNG 픽셀은 가공하지 않는다.
    public static class InkSpriteAnimationBuilder
    {
        public const string ArtRoot = "Assets/Art/Character/Animations";
        public const string DataRoot = "Assets/Resources/MukJump/SpriteAnimations";
        public static void Configure()
        {
            EnsureFolder(DataRoot);
            ConfigureSheet("muk_held", 6f);
            ConfigureSheet("mask_strike", 11.111111f);
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                var id = (InkSkinId)i; string key = InkAccessoryMotion.Key(id);
                if (key == null) continue;
                float fps = id == InkSkinId.BlueWisps ? 7f :
                    id is InkSkinId.SpiritHalo or InkSkinId.NineTails or InkSkinId.MoonClouds or InkSkinId.PaperLanterns ? 4f : 6f;
                ConfigureSheet(key, fps);
            }
            AssetDatabase.SaveAssets();
        }
        static void ConfigureSheet(string key, float fps)
        {
            string path = ArtRoot + "/" + key + "_sheet.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("그린 애니메이션 원화 누락: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width < 4 || height < 4) throw new InvalidOperationException("2×2 프레임 격자가 맞지 않음: " + path);
            if (!importer.DoesSourceTextureHaveAlpha()) throw new InvalidOperationException("투명 알파 채널 누락: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = 1024;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Sprite 데이터 제공자 누락: " + path);
            provider.InitSpriteEditorDataProvider();
            var capabilities = provider.GetDataProvider<ISpriteFrameEditCapability>();
            foreach (var capability in new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName,
                EEditCapability.EditSpriteRect, EEditCapability.EditPivot })
                if (capabilities == null || !capabilities.GetEditCapability().HasCapability(capability))
                    throw new InvalidOperationException("Sprite 편집 잠금: " + path + " / " + capability);
            var previous = provider.GetSpriteRects();
            var rects = new SpriteRect[4];
            for (int i = 0; i < 4; i++)
            {
                string name = key + "_" + i;
                var existing = previous.FirstOrDefault(r => r.name == name);
                rects[i] = new SpriteRect { name = name, spriteID = existing?.spriteID ?? GUID.Generate(),
                    rect = SourceRect(key, i, width, height),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) };
            }
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names == null) throw new InvalidOperationException("Sprite 이름 식별자 제공자 누락: " + path);
            names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (frames.Length != 4) throw new InvalidOperationException("프레임 수 오류: " + path);
            string assetPath = DataRoot + "/" + key + ".asset";
            var animation = AssetDatabase.LoadAssetAtPath<InkSpriteAnimation>(assetPath);
            if (animation == null)
            {
                animation = ScriptableObject.CreateInstance<InkSpriteAnimation>();
                AssetDatabase.CreateAsset(animation, assetPath);
            }
            var serialized = new SerializedObject(animation);
            var entries = serialized.FindProperty("frames"); entries.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.FindProperty("framesPerSecond").floatValue = fps;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(animation);
        }
        // 원화의 행/열 간격이 정확히 절반인 것은 아니다. 술·꼬리 끝이 아닌 실제 투명
        // 경계로 자르되 네 프레임의 크기·비율은 동일하게 유지해 재생 중 확대/축소를 막는다.
        public static Rect SourceRect(string key, int frame, int width, int height)
        {
            if (frame < 0 || frame > 3) throw new ArgumentOutOfRangeException(nameof(frame));
            bool right = frame % 2 != 0, top = frame < 2;
            if (key is "paper_lanterns" or "dragon_tail" or "nine_tails")
            {
                if (width != 1254 || height != 1254)
                    throw new InvalidOperationException(key + " 원화 크기가 바뀌어 프레임 경계를 다시 확인해야 한다.");
                if (key == "paper_lanterns") return new Rect(right ? 647 : 20, top ? 600 : 0, 600, 600);
                // 아래로 굽히는 원화의 뿌리를 같은 높이에 둔다. 행 사이의 겹친 구간은 투명하다.
                if (key == "dragon_tail") return new Rect(right ? 610 : 19, top ? 625 : 105, 610, 610);
                return new Rect(right ? 627 : 0, top ? 620 : 70, 627, 600);
            }
            // 홀수 크기 원화는 중앙의 투명한 1px만 제외한다.
            int cellWidth = width / 2, cellHeight = height / 2;
            return new Rect(right ? width - cellWidth : 0, top ? height - cellHeight : 0, cellWidth, cellHeight);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
