using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MukJump.EditorTools
{
    /// 공개 저장소와 스토어에 함께 배포할 수 없는 초기 에셋을 재현 가능한 안전 자산으로 교체한다.
    public static class MukJumpRestrictedAssetMigration
    {
        const string GoogleFontsCommit =
            "6a003b5eb672dc8bf5bff5937cf5863f8b175445";
        const string FontUrl =
            "https://raw.githubusercontent.com/google/fonts/" +
            GoogleFontsCommit +
            "/ofl/nanumbrushscript/NanumBrushScript-Regular.ttf";
        const string LicenseUrl =
            "https://raw.githubusercontent.com/google/fonts/" +
            GoogleFontsCommit +
            "/ofl/nanumbrushscript/OFL.txt";
        const string FontSha256 =
            "27ceaf578c96f594cdf07fe0181b251790acbb746a164e45c1f6473f89911a31";
        const string LicenseSha256 =
            "eeacf16032901d0ed0456876ec77b8f0fda6b3fecec7d972f8543eb602e6c30f";
        const string OldFontPath =
            "Assets/Resources/MukJump/Fonts/HealthsetJoritdaeStd.otf";
        const string NewFontPath =
            "Assets/Resources/MukJump/Fonts/NanumBrushScript-Regular.ttf";
        const string LicensePath =
            "Assets/ThirdParty/NanumBrushScript/OFL.txt";
        const string ActionButtonPath =
            "Assets/Resources/MukJump/UI/Common/action_button_brush.png";
        const string QuarantineRoot = "Temp/LicenseQuarantine";

        static readonly string[] RestrictedAudioPaths =
        {
            "Assets/Resources/MukJump/Audio/SFX/" +
            "SFX_Character_Death_Slime.mp3",
            "Assets/Resources/MukJump/Audio/SFX/" +
            "SFX_Game_Over_Ink_Spill.mp3",
        };

        [MenuItem("MukJump/Release/Migrate Restricted Assets")]
        public static void Migrate()
        {
            byte[] fontBytes = DownloadVerified(FontUrl, FontSha256);
            byte[] licenseBytes = DownloadVerified(
                LicenseUrl,
                LicenseSha256);

            Directory.CreateDirectory(Path.GetDirectoryName(NewFontPath));
            if (File.Exists(OldFontPath) && !File.Exists(NewFontPath))
            {
                string error = AssetDatabase.MoveAsset(
                    OldFontPath,
                    NewFontPath);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(
                        "UI 폰트 자산 이동 실패: " + error);
            }
            File.WriteAllBytes(NewFontPath, fontBytes);

            Directory.CreateDirectory(Path.GetDirectoryName(LicensePath));
            File.WriteAllBytes(LicensePath, licenseBytes);
            File.WriteAllBytes(ActionButtonPath, CreateOwnedBrushPng());

            foreach (string path in RestrictedAudioPaths)
                Quarantine(path);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureOwnedBrushImporter();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[MukJump] 제한 자산 교체 완료: Nanum Brush Script(OFL), " +
                "자체 먹붓 마스크, 자체 WAV 효과음");
        }

        static byte[] DownloadVerified(string url, string expectedSha256)
        {
            using var client = new WebClient();
            byte[] bytes = client.DownloadData(url);
            using SHA256 sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            if (!string.Equals(
                    actual,
                    expectedSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"외부 자산 해시가 다릅니다: {url}");
            return bytes;
        }

        static byte[] CreateOwnedBrushPng()
        {
            const int width = 700;
            const int height = 350;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width * 2f - 1f;
                    float ny = (y + 0.5f) / height * 2f - 1f;
                    float taper = Mathf.Pow(
                        Mathf.Clamp01(1f - Mathf.Abs(nx)),
                        0.2f);
                    float fibers =
                        Mathf.Sin(x * 0.083f) * 0.018f +
                        Mathf.Sin(x * 0.217f + 1.9f) * 0.012f +
                        Hash01(x, y / 7) * 0.025f;
                    float halfHeight = 0.54f * taper + fibers;
                    float edge = halfHeight - Mathf.Abs(ny);
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(edge * 42f) * 255f);

                    if (alpha > 0 &&
                        Hash01(x / 3, y / 3) > 0.93f &&
                        Mathf.Abs(ny) > halfHeight * 0.62f)
                        alpha = (byte)(alpha * 0.35f);

                    pixels[x + y * width] =
                        new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        static float Hash01(int x, int y)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value & 0xffffu) / 65535f;
            }
        }

        static void Quarantine(string assetPath)
        {
            if (!File.Exists(assetPath))
                return;
            Directory.CreateDirectory(QuarantineRoot);
            string fileName = Path.GetFileName(assetPath);
            string destination = Path.Combine(QuarantineRoot, fileName);
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(assetPath, destination);

            string metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
                return;
            string metaDestination = destination + ".meta";
            if (File.Exists(metaDestination))
                File.Delete(metaDestination);
            File.Move(metaPath, metaDestination);
        }

        static void ConfigureOwnedBrushImporter()
        {
            if (AssetImporter.GetAtPath(ActionButtonPath) is not
                TextureImporter importer)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
