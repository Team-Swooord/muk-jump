using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;
using MukJump.Obstacles;

namespace MukJump.EditorTools
{
    /// 메뉴 "MukJump > Build Main Scene" 한 번으로 Splash와 Main 씬을 구성한다.
    /// (씬 구성을 코드로 남겨 두면 협업 시 씬 머지 충돌을 피하고 재현 가능)
    public static class MukJumpSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        internal const string SceneAttestationPath =
            "ProjectSettings/MukJumpSceneAttestation.json";
        const string BuilderSourcePath =
            "Assets/Editor/MukJumpSceneBuilder.cs";
        const string SplashBuilderSourcePath =
            "Assets/Editor/MukJumpSplashSceneBuilder.cs";
        internal const string SceneSourceStampPrefix =
            "@MukJumpSceneSource_";
        const string BgPath = "Assets/Art/Background/background_ink_landscape.png";
        internal const string AmbientCloudAtlasPath =
            "Assets/Resources/MukJump/Background/ambient_cloud_atlas_v1.png";
        internal static readonly string[] AmbientThemeAtlasPaths =
        {
            AmbientCloudAtlasPath,
            "Assets/Resources/MukJump/Background/ambient_01_wind_ribbons_v1.png",
            "Assets/Resources/MukJump/Background/ambient_02_rain_veil_v1.png",
            "Assets/Resources/MukJump/Background/ambient_03_cliff_haze_v1.png",
            "Assets/Resources/MukJump/Background/ambient_04_gate_stardust_v1.png",
            "Assets/Resources/MukJump/Background/ambient_05_lotus_mist_v1.png",
            "Assets/Resources/MukJump/Background/ambient_06_river_current_v1.png",
        };
        static readonly string[] MapBackgroundPaths =
        {
            "Assets/Art/Background/Maps/map_00_quiet_mountain.png",
            "Assets/Art/Background/Maps/map_01_wind_ridge.png",
            "Assets/Art/Background/Maps/map_02_ink_rain_valley.png",
            "Assets/Art/Background/Maps/map_03_black_cliff.png",
        };
        static readonly string[] EndlessMapBackgroundPaths =
        {
            "Assets/Resources/MukJump/Background/Endless/map_04_ink_galaxy_gate.png",
            "Assets/Resources/MukJump/Background/Endless/map_05_celestial_lotus.png",
            "Assets/Resources/MukJump/Background/Endless/map_06_heavenly_ink_river.png",
        };
        const string CharSheetPath = "Assets/Art/Character/Player/muk_spritesheet.png";
        const string CharHitOneSheetPath =
            "Assets/Resources/MukJump/Player/muk_spritesheet_hit_01.png";
        const string CharHitTwoSheetPath =
            "Assets/Resources/MukJump/Player/muk_spritesheet_hit_02.png";
        const string ObstaclePath = "Assets/Art/Character/Obstacles/anermy_01.png";
        const string DragonObstaclePath =
            "Assets/Resources/MukJump/Obstacles/child_ink_dragon.png";
        const string DragonObstacleSheetPath =
            "Assets/Resources/MukJump/Obstacles/child_ink_dragon_4frame_v3.png";
        const string HaetaeObstacleSheetPath =
            "Assets/Resources/MukJump/Obstacles/child_ink_haetae_4frame_v2.png";
        const string FallingInkRockPath = "Assets/Art/Character/Obstacles/anermy_02.png";
        const string LobbyLogoPath = "Assets/Art/UI/muk_logo.png";
        const string StartButtonPath = "Assets/Art/UI/muk_start_button.png";
        const string GaugeFillPath = "Assets/Art/UI/muk_gauge_fill.png";
        const string GaugeTrackPath = "Assets/Art/UI/muk_gauge_track.png";
        const string GaugeBrushIconPath = "Assets/Art/UI/muk_brush_icon.png";
        const string LineSpritePrefabPath = "Assets/Art/UI/LineSprite.prefab";
        const string InkDropItemPath = "Assets/Art/UI/ink_drop.png";
        const string GoldenBrushItemPath = "Assets/Art/UI/golden_brush.png";
        const string InkShieldItemPath = "Assets/Art/UI/ink_shield.png";
        const string InkCloneItemPath = "Assets/Art/UI/ink_clone.png";
        const string ActionButtonPath =
            "Assets/Resources/MukJump/UI/Common/action_button_hanji_v1.png";
        internal const string HanjiScrollRollPath =
            "Assets/Resources/MukJump/UI/Common/scroll_roll_hanji_v2.png";
        internal static readonly string[] SettingsIconKeys =
        {
            "music", "sound", "haptics", "motion", "language", "support", "tutorial", "nickname", "account", "rank"
        };
        // 뜯긴 한지 가장자리와 모서리는 고정하고 깨끗한 중앙부만 늘린다.
        static readonly Vector4 ActionButtonBorder =
            new Vector4(64f, 48f, 64f, 48f);
        const string PermanentGrowthUiRoot =
            "Assets/Resources/MukJump/UI/PermanentGrowth/";
        static readonly string[] PermanentGrowthUiPaths =
        {
            PermanentGrowthUiRoot + "pg_hanji_background.png",
            PermanentGrowthUiRoot + "pg_tree_trunk.png",
            PermanentGrowthUiRoot + "pg_tree_background_v3.png",
            PermanentGrowthUiRoot + "pg_branch.png",
            PermanentGrowthUiRoot + "pg_branch_piece_01.png",
            PermanentGrowthUiRoot + "pg_branch_piece_02.png",
            PermanentGrowthUiRoot + "pg_branch_piece_03.png",
            PermanentGrowthUiRoot + "pg_branch_piece_04.png",
            PermanentGrowthUiRoot + "pg_branch_piece_05.png",
            PermanentGrowthUiRoot + "pg_branch_piece_06.png",
            PermanentGrowthUiRoot + "pg_node_bud.png",
            PermanentGrowthUiRoot + "pg_node_bloom_mask.png",
            PermanentGrowthUiRoot + "pg_selected_ring.png",
            PermanentGrowthUiRoot + "pg_hanji_card.png",
            PermanentGrowthUiRoot + "pg_root_emblem.png",
            PermanentGrowthUiRoot + "pg_inklight_sumukhwa_v1.png",
            PermanentGrowthUiRoot + "pg_title_growth_ko_v1.png",
            PermanentGrowthUiRoot + "pg_icon_capacity.png",
            PermanentGrowthUiRoot + "pg_icon_recovery.png",
            PermanentGrowthUiRoot + "pg_icon_platform.png",
            PermanentGrowthUiRoot + "pg_icon_jump.png",
            "Assets/Resources/" + PermanentGrowthView.BrushIconResourcePath + ".png",
        };
        const string UiFontPath =
            "Assets/Resources/MukJump/Fonts/NanumBrushScript-Regular.ttf";
        const string DeathSplashPath = "Assets/Art/Character/Death/ink_death_splash.png";
        const string InkDropVfxRoot = "Assets/MukJump/VFX/InkDropJump";
        const string InkDropVfxTextureRoot = InkDropVfxRoot + "/Textures/";
        const string InkDropVfxAudioRoot = InkDropVfxRoot + "/Audio/";
        // 14a6141의 사용자 수동 로비 배치를 빌더의 공식 값으로 고정한다.
        // Main 씬을 다시 생성해도 로고와 최고 기록 칸이 초기 배치로 돌아가면 안 된다.
        static readonly Vector2 LobbyLogoAnchor = new(0.5f, 0.68f);
        static readonly Vector2 LobbyLogoPosition = new(-LobbyMenuLayout.LogoVisibleCenterOffsetX, 79f);
        static readonly Vector2 LobbyLogoSize = new(1281.776f, 854.518f);
        static readonly string[] DeathFramePaths =
        {
            "Assets/Art/Character/Death/mukbangul_death_01_idle.png",
            "Assets/Art/Character/Death/mukbangul_death_02_impact.png",
            "Assets/Art/Character/Death/mukbangul_death_03_x_eyes.png",
            "Assets/Art/Character/Death/mukbangul_death_04_pop_up.png",
            "Assets/Art/Character/Death/mukbangul_death_05_apex.png",
            "Assets/Art/Character/Death/mukbangul_death_06_fall_start.png",
            "Assets/Art/Character/Death/mukbangul_death_07_fast_fall.png",
            "Assets/Art/Character/Death/mukbangul_death_08_final_fall.png",
        };
        const int CharFrameSize = 1024;
        const int CharSheetColumns = 4;
        internal const int CharacterSheetWidth = 4096;
        internal const int CharacterSheetHeight = 2048;
        // 루트 Transform·물리 콜라이더는 그대로 두고 시각 크기만 기존보다 약 15% 키운다.
        const float CharPpu = 780f;
        // 먹방울이는 피해를 입을수록 먹이 불어난 듯 조금씩 커진다. PPU만 낮춰
        // 충돌 크기와 점프 물리는 바꾸지 않는다.
        const float CharHitOnePpu = 735f;
        const float CharHitTwoPpu = 690f;
        // 사망 원본은 같은 1024 캔버스 안에서 캐릭터가 약 80% 크기로 들어가 있어
        // 일반/점프 프레임과 화면상 몸통 크기를 맞추기 위해 1.25배 확대한다.
        const float DeathPpu = CharPpu * 0.8f;
        // 캐릭터 프레임의 월드 폭 — 별도 캔버스의 스프라이트(죽음 포즈 등)도 이 폭에 맞춘다
        const float CharWorldWidth = CharFrameSize / CharPpu;

        // 점프 애니메이션 8프레임: 4×2 그리드, 좌→우/위→아래 순서
        // idle·crouch·launch·rise (윗줄) / apex·fall·dive·land (아랫줄)
        static readonly string[] CharFrameNames =
        {
            "idle", "crouch", "launch", "rise",
            "apex", "fall", "dive", "land",
        };

        // 월드 화면 폭 10.8유닛, 세로(9:16) → 카메라 반높이 9.6유닛
        const float WorldScreenWidth = 10.8f;
        const float OrthoSize = 9.6f;

        [MenuItem("MukJump/Build Main Scene")]
        public static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning(
                    "[MukJump] 컴파일·에셋 갱신이 끝난 뒤 Main 씬을 생성하세요.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    "[MukJump] Play Mode를 종료한 뒤 Main 씬을 생성하세요.");
                return;
            }

            EnsureLayer("Platform");
            EnsureLayer("Obstacle");
            EnsureLayer("Item");
            EnsureLayer("Player");
            ConfigureBackground();
            foreach (string atlasPath in AmbientThemeAtlasPaths) ConfigureAmbientCloudAtlas(atlasPath);
            ConfigureCharacterSheets();
            ConfigureDeathSprites();
            ConfigureObstacleSprite();
            ConfigureDragonObstacleSprites();
            ConfigureHaetaeObstacleSprites();
            ConfigureFallingInkRockSprite();
            ConfigureItemSprites();
            ConfigurePermanentGrowthSprites();
            ConfigureActionButtonSprite();
            ConfigureHanjiScrollRoll();
            ConfigureSettingsIcons();
            ConfigureWindIcon();
            ConfigureLobbySky();
            ConfigureInkDropJumpVfxAssets();
            if (AssetDatabase.LoadAssetAtPath<Font>(UiFontPath) == null)
                Debug.LogWarning($"[MukJump] UI 폰트를 찾을 수 없음: {UiFontPath}");

            string[] assetIssues = CollectRequiredSceneAssetIssues();
            if (assetIssues.Length > 0)
                throw new System.InvalidOperationException(
                    "Main 씬 필수 에셋 검증에 실패했습니다:\n" +
                    string.Join("\n", assetIssues));

            MukJumpSplashSceneBuilder.BuildSceneFile();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildSceneContents(configureUiImporters: true);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException(
                    $"Main 씬을 저장하지 못했습니다: {ScenePath}");
            MukJumpSplashSceneBuilder.ConfigureBuildSettings();
            WriteCurrentSceneAttestation();

            Debug.Log(
                "[MukJump] Splash·Main 씬 구성 완료 — " +
                "Game 뷰를 9:16으로 두고 Play 하세요.");
        }

        [MenuItem("MukJump/Configure Dragon Obstacle Sprites")]
        public static void ConfigureDragonObstacleSprites()
        {
            ConfigureDragonObstacleSprite();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MukJump/Configure Haetae Obstacle Sprites")]
        public static void ConfigureHaetaeObstacleSprites()
        {
            ConfigureHaetaeObstacleSheet();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MukJump/Configure Permanent Growth Sprites")]
        public static void ConfigurePermanentGrowthSprites()
        {
            for (int i = 0; i < PermanentGrowthUiPaths.Length; i++)
            {
                string path = PermanentGrowthUiPaths[i];
                var importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning(
                        $"[MukJump] 영구 성장 UI 스프라이트를 찾을 수 없음: {path}");
                    continue;
                }

                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);

                bool large =
                    path.EndsWith("pg_hanji_background.png") ||
                    path.EndsWith("pg_tree_trunk.png") ||
                    path.Contains("pg_tree_background_v");
                bool medium =
                    path.EndsWith("pg_branch.png") ||
                    path.Contains("pg_branch_piece_") ||
                    path.EndsWith("pg_hanji_card.png");
                bool opaque =
                    path.EndsWith("pg_hanji_background.png");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = !opaque;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = large
                    ? 2048
                    : medium
                        ? 1024
                        : 512;
                importer.textureCompression =
                    TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("MukJump/Configure Hanji Scroll Roll")]
        public static void ConfigureHanjiScrollRoll()
        {
            AssetDatabase.ImportAsset(HanjiScrollRollPath);
            var importer = AssetImporter.GetAtPath(HanjiScrollRollPath) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("두루마리 롤 원화 없음");
            // 원본 PNG를 가공하지 않고 투명 여백만 임포터 슬라이스로 제외한다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect bounds;
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(HanjiScrollRollPath)))
                    throw new System.InvalidOperationException("두루마리 롤 PNG 해석 실패");
                var pixels = source.GetPixels32();
                int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a < 8) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                if (maxX < minX) throw new System.InvalidOperationException("두루마리 롤이 비어 있음");
                minX = Mathf.Max(0, minX - 2); minY = Mathf.Max(0, minY - 2);
                maxX = Mathf.Min(source.width - 1, maxX + 2);
                maxY = Mathf.Min(source.height - 1, maxY + 2);
                bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally { Object.DestroyImmediate(source); }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
#pragma warning disable CS0618
            importer.spritesheet = new[] { new SpriteMetaData
            {
                name = "scroll_roll_hanji_v2", rect = bounds,
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f)
            } };
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        [MenuItem("MukJump/Configure Settings Icons")]
        public static void ConfigureSettingsIcons()
        {
            foreach (string key in SettingsIconKeys)
            {
                string path = $"Assets/Resources/MukJump/UI/Common/settings_icon_{key}_v1.png";
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.maxTextureSize == 256 && importer.alphaIsTransparency &&
                    !importer.mipmapEnabled && !importer.isReadable &&
                    importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear &&
                    importer.textureCompression == TextureImporterCompression.CompressedHQ)
                    continue;
                // 런타임 최대 148px. 원본 알파·붓결은 보존하고 모바일에는 256px만 올린다.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.maxTextureSize = 256;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
                importer.spriteBorder = Vector4.zero;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        [MenuItem("MukJump/Configure Wind Icon")]
        public static void ConfigureWindIcon()
        {
            string path = "Assets/Resources/" + WindIndicatorView.WindIconResourcePath + ".png";
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.maxTextureSize == 256 && importer.alphaIsTransparency &&
                !importer.mipmapEnabled && !importer.isReadable &&
                importer.wrapMode == TextureWrapMode.Clamp &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.textureCompression == TextureImporterCompression.Uncompressed)
                return;

            // 44×44 HUD용 작은 투명 아이콘. 압축 블록 없이 여백·붓결을 보존한다.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 256;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        [MenuItem("MukJump/Configure Shared Action Button Sprite")]
        public static void ConfigureActionButtonSprite()
        {
            var importer =
                AssetImporter.GetAtPath(ActionButtonPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning(
                    $"[MukJump] 공용 행동 버튼 스프라이트를 찾을 수 없음: " +
                    ActionButtonPath);
                return;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteBorder = ActionButtonBorder;
            importer.SetTextureSettings(textureSettings);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        [InitializeOnLoadMethod]
        static void SchedulePermanentGrowthSpriteConfiguration()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying ||
                    EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                if (NeedsPermanentGrowthSpriteConfiguration())
                    ConfigurePermanentGrowthSprites();
                if (NeedsActionButtonSpriteConfiguration())
                    ConfigureActionButtonSprite();
                ConfigureSettingsIcons();
                ConfigureWindIcon();
                ConfigureLobbySky();
                if (NeedsCharacterVisualConfiguration())
                {
                    ConfigureCharacterSheets();
                    ConfigureDeathSprites();
                }
            };
        }

        static bool NeedsCharacterVisualConfiguration()
        {
            (string path, float ppu)[] liveSheets =
            {
                (CharSheetPath, CharPpu),
                (CharHitOneSheetPath, CharHitOnePpu),
                (CharHitTwoSheetPath, CharHitTwoPpu),
            };
            for (int i = 0; i < liveSheets.Length; i++)
            {
                var importer =
                    AssetImporter.GetAtPath(liveSheets[i].path) as TextureImporter;
                if (importer == null ||
                    !IsCharacterSheetConfigured(
                        liveSheets[i].path,
                        liveSheets[i].ppu,
                        importer) ||
                    !Mathf.Approximately(
                        importer.spritePixelsPerUnit,
                        liveSheets[i].ppu))
                    return true;
            }
            for (int i = 0; i < DeathFramePaths.Length; i++)
            {
                var importer =
                    AssetImporter.GetAtPath(DeathFramePaths[i]) as TextureImporter;
                if (importer == null ||
                    !Mathf.Approximately(importer.spritePixelsPerUnit, DeathPpu))
                    return true;
            }
            return false;
        }

        static bool IsCharacterSheetConfigured(
            string path,
            float expectedPpu,
            TextureImporter importer)
        {
            if (importer == null)
                return false;
            importer.GetSourceTextureWidthAndHeight(
                out int sourceWidth,
                out int sourceHeight);
            if (!IsExpectedCharacterSheetDimensions(
                    sourceWidth,
                    sourceHeight) ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                !Mathf.Approximately(
                    importer.spritePixelsPerUnit,
                    expectedPpu) ||
                importer.mipmapEnabled ||
                importer.maxTextureSize < CharacterSheetWidth ||
                !HasCharacterSheetPlatformSettings(
                    importer,
                    "iPhone",
                    TextureImporterFormat.ASTC_4x4) ||
                !HasCharacterSheetPlatformSettings(
                    importer,
                    "WebGL",
                    TextureImporterFormat.ASTC_4x4) ||
                !HasCharacterSheetPlatformSettings(
                    importer,
                    "Android",
                    TextureImporterFormat.Automatic))
                return false;

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int frameIndex = 0;
                 frameIndex < CharFrameNames.Length;
                 frameIndex++)
            {
                Sprite match = assets.OfType<Sprite>().FirstOrDefault(sprite =>
                    string.Equals(
                        sprite.name,
                        CharFrameNames[frameIndex],
                        System.StringComparison.Ordinal));
                if (match == null ||
                    !Mathf.Approximately(match.rect.width, CharFrameSize) ||
                    !Mathf.Approximately(match.rect.height, CharFrameSize))
                    return false;
            }
            return assets.OfType<Sprite>().Count(sprite =>
                CharFrameNames.Contains(sprite.name)) == CharFrameNames.Length;
        }

        static bool HasCharacterSheetPlatformSettings(
            TextureImporter importer,
            string platform,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            return settings.overridden &&
                   settings.maxTextureSize >= CharacterSheetWidth &&
                   settings.format == format;
        }

        internal static bool IsExpectedCharacterSheetDimensions(
            int width,
            int height) =>
            width == CharacterSheetWidth && height == CharacterSheetHeight;

        internal static string[] CollectRequiredSceneAssetIssues()
        {
            var issues = new List<string>();
            foreach (string path in MapBackgroundPaths.Concat(
                         EndlessMapBackgroundPaths).Append(BgPath))
                RequireAsset<Sprite>(issues, path, "배경 스프라이트");
            foreach (string atlasPath in AmbientThemeAtlasPaths)
                if (AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().Count() != 3)
                    issues.Add("맵별 배경 효과 atlas가 3개로 슬라이스되지 않았습니다: " + atlasPath);

            (string path, float ppu)[] sheets =
            {
                (CharSheetPath, CharPpu),
                (CharHitOneSheetPath, CharHitOnePpu),
                (CharHitTwoSheetPath, CharHitTwoPpu),
            };
            for (int i = 0; i < sheets.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(sheets[i].path) as
                    TextureImporter;
                if (!IsCharacterSheetConfigured(
                        sheets[i].path,
                        sheets[i].ppu,
                        importer))
                    issues.Add(
                        "캐릭터 시트가 4096×2048·8프레임·모바일 4096 " +
                        $"설정을 충족하지 않습니다: {sheets[i].path}");
            }

            foreach (string path in DeathFramePaths.Append(DeathSplashPath))
                RequireAsset<Sprite>(issues, path, "사망 스프라이트");
            foreach (string path in new[]
                     {
                         ObstaclePath,
                         DragonObstaclePath,
                         DragonObstacleSheetPath,
                         HaetaeObstacleSheetPath,
                         FallingInkRockPath,
                         InkDropItemPath,
                         GoldenBrushItemPath,
                         InkShieldItemPath,
                         InkCloneItemPath,
                         ActionButtonPath,
                     })
                RequireAsset<Sprite>(issues, path, "게임 스프라이트");
            foreach (string path in PermanentGrowthUiPaths)
                RequireAsset<Sprite>(issues, path, "성장 UI 스프라이트");
            foreach (string path in RequiredHudTexturePaths())
                RequireAsset<Texture2D>(issues, path, "HUD 텍스처");
            RequireAsset<Sprite>(issues, HanjiScrollRollPath, "두루마리 롤");
            RequireAsset<Texture2D>(issues,
                "Assets/Resources/" + LobbyNightSkyView.CleanSkyResourcePath + ".png", "로비 해 분리용 하늘");
            RequireAsset<Shader>(issues,
                "Assets/Resources/MukJump/Shaders/BackgroundNight.shader", "밤 배경 셰이더");
            RequireAsset<Sprite>(issues,
                "Assets/Resources/" + WindIndicatorView.WindIconResourcePath + ".png", "풍향 수묵 아이콘");
            foreach (string key in SettingsIconKeys)
                RequireAsset<Sprite>(issues,
                    $"Assets/Resources/MukJump/UI/Common/settings_icon_{key}_v1.png", "설정 수묵 아이콘");

            RequireAsset<GameObject>(
                issues,
                LineSpritePrefabPath,
                "먹선 프리팹");
            RequireAsset<Font>(issues, UiFontPath, "UI 폰트");
            RequireAsset<Sprite>(
                issues,
                MukJumpSplashSceneBuilder.LogoPath,
                "CYSBand 스플래시 로고");
            return issues.ToArray();
        }

        internal static string[] RequiredHudTexturePaths() =>
            new[]
            {
                LobbyLogoPath,
                InkLocalizedGameLogo.EnglishAssetPath,
                StartButtonPath,
                GaugeFillPath,
                GaugeTrackPath,
                GaugeBrushIconPath,
            };

        static void RequireAsset<T>(
            ICollection<string> issues,
            string path,
            string label)
            where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                issues.Add($"{label}을 불러올 수 없습니다: {path}");
        }

        static bool NeedsPermanentGrowthSpriteConfiguration()
        {
            for (int i = 0; i < PermanentGrowthUiPaths.Length; i++)
            {
                string path = PermanentGrowthUiPaths[i];
                var importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) return true;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                bool large =
                    path.EndsWith("pg_hanji_background.png") ||
                    path.EndsWith("pg_tree_trunk.png") ||
                    path.Contains("pg_tree_background_v");
                bool medium =
                    path.EndsWith("pg_branch.png") ||
                    path.Contains("pg_branch_piece_") ||
                    path.EndsWith("pg_hanji_card.png");
                bool opaque =
                    path.EndsWith("pg_hanji_background.png");
                int expectedMax = large ? 2048 : medium ? 1024 : 512;
                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                    !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                    importer.alphaIsTransparency == opaque ||
                    importer.mipmapEnabled ||
                    importer.npotScale != TextureImporterNPOTScale.None ||
                    importer.wrapMode != TextureWrapMode.Clamp ||
                    importer.filterMode != FilterMode.Bilinear ||
                    importer.maxTextureSize != expectedMax ||
                    importer.textureCompression !=
                        TextureImporterCompression.CompressedHQ ||
                    importer.compressionQuality != 100)
                    return true;
            }
            return false;
        }

        static bool NeedsActionButtonSpriteConfiguration()
        {
            var importer =
                AssetImporter.GetAtPath(ActionButtonPath) as TextureImporter;
            if (importer == null) return true;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            return importer.textureType != TextureImporterType.Sprite ||
                   importer.spriteImportMode != SpriteImportMode.Single ||
                   textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                   textureSettings.spriteBorder != ActionButtonBorder ||
                   !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                   !importer.alphaIsTransparency ||
                   importer.mipmapEnabled ||
                   importer.npotScale != TextureImporterNPOTScale.None ||
                   importer.wrapMode != TextureWrapMode.Clamp ||
                   importer.filterMode != FilterMode.Bilinear ||
                   importer.maxTextureSize != 2048 ||
                   importer.textureCompression !=
                       TextureImporterCompression.CompressedHQ ||
                   importer.compressionQuality != 100;
        }

        /// 테스트가 실제 Main 씬, Build Settings, Player Settings, import 설정을 변경하지 않고
        /// 빌더 결과를 검증할 수 있도록 저장되지 않는 additive 씬에 같은 오브젝트 그래프를 만든다.
        public static Scene BuildForTests()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene testScene = default;
            var existingRootIds = new HashSet<EntityId>();
            if (!previousScene.IsValid() || !previousScene.isLoaded)
                throw new System.InvalidOperationException(
                    "빌더 테스트를 실행할 활성 씬이 없습니다.");
            foreach (var root in previousScene.GetRootGameObjects())
                existingRootIds.Add(root.GetEntityId());

            try
            {
                // EditorTestRunner는 저장되지 않은 기본 씬을 활성화한 채 시작할 수 있다.
                // 일반 additive 씬은 그 상태에서 생성할 수 없으므로 저장 대상이 될 수 없는
                // preview 씬을 만들고, 새로 만든 루트만 그 씬으로 옮겨 완전히 격리한다.
                testScene = EditorSceneManager.NewPreviewScene();
                BuildSceneContents(configureUiImporters: false);

                var rootsAfterBuild = previousScene.GetRootGameObjects();
                for (int i = 0; i < rootsAfterBuild.Length; i++)
                {
                    var root = rootsAfterBuild[i];
                    if (!existingRootIds.Contains(root.GetEntityId()))
                        SceneManager.MoveGameObjectToScene(root, testScene);
                }
                return testScene;
            }
            catch
            {
                if (testScene.IsValid() && testScene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(testScene);
                foreach (var root in previousScene.GetRootGameObjects())
                {
                    if (!existingRootIds.Contains(root.GetEntityId()))
                        Object.DestroyImmediate(root);
                }
                throw;
            }
        }

        public static void CloseTestScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            EditorSceneManager.ClosePreviewScene(scene);
        }

        static void BuildSceneContents(bool configureUiImporters)
        {
            var camera = BuildCamera();
            BuildBackground(camera.transform);
            var player = BuildPlayer();
            BuildStarterPlatform(player);
            BuildSystems(camera, player, configureUiImporters);
            BuildLobbyUi(configureUiImporters);
            BuildGameplayUi(configureUiImporters);

            var follow = camera.GetComponent<CameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.FindProperty("upperFollowViewportY").floatValue =
                CameraFollow.BalancedFollowViewportY;
            so.FindProperty("followTuningVersion").intValue =
                CameraFollow.CurrentFollowTuningVersion;
            so.FindProperty("hardCeilingViewportY").floatValue = 0.9f;
            so.FindProperty("survivorReframeViewportY").floatValue =
                CameraFollow.SurvivorReframeViewportY;
            so.FindProperty("survivorReframeDuration").floatValue =
                CameraFollow.SurvivorReframeSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();

            // UI 생성 중 importer가 바뀔 수 있으므로 모든 변형이 끝난 다음
            // 최종 meta까지 해시해 저장 직후에도 같은 세대로 판정되게 한다.
            new GameObject(CurrentSceneSourceStampName());
        }

        internal static string CurrentSceneSourceStampName()
        {
            string[] sourceInputs = CurrentSceneSourceInputPaths();
            var sourceManifest = new StringBuilder();
            for (int i = 0; i < sourceInputs.Length; i++)
            {
                string path = sourceInputs[i];
                string source = File.ReadAllText(path)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');
                AppendManifestField(sourceManifest, path);
                AppendManifestField(sourceManifest, source);
            }
            return SceneSourceStampPrefix +
                   Hash128.Compute(sourceManifest.ToString()).ToString();
        }

        internal static string[] CurrentSceneSourceInputPaths()
        {
            var inputs = new HashSet<string>(System.StringComparer.Ordinal)
            {
                NormalizeProjectPath(BuilderSourcePath),
                NormalizeProjectPath(SplashBuilderSourcePath),
                "ProjectSettings/TagManager.asset",
                "ProjectSettings/ProjectVersion.txt",
                "Packages/manifest.json",
                "Packages/packages-lock.json",
            };

            AddExistingFiles(inputs, "Assets/Scripts", "*.cs");
            // 씬이 참조하는 에셋 GUID와 임포트 설정도 생성 코드의 일부다.
            // meta가 바뀌었는데 과거 씬이 통과하면 잘못된 참조가 출시될 수 있다.
            AddExistingFiles(inputs, "Assets", "*.meta");

            string[] result = inputs
                .Where(File.Exists)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            if (!result.Contains(
                    NormalizeProjectPath(BuilderSourcePath),
                    System.StringComparer.Ordinal))
                throw new FileNotFoundException(
                    "Main 씬 빌더 원본을 찾을 수 없습니다.",
                    BuilderSourcePath);
            return result;
        }

        static void AddExistingFiles(
            ISet<string> destination,
            string root,
            string pattern)
        {
            if (!Directory.Exists(root))
                return;
            string[] paths = Directory.GetFiles(
                root,
                pattern,
                SearchOption.AllDirectories);
            for (int i = 0; i < paths.Length; i++)
                destination.Add(NormalizeProjectPath(paths[i]));
        }

        static string NormalizeProjectPath(string path) =>
            path?.Replace('\\', '/');

        static void AppendManifestField(StringBuilder manifest, string value)
        {
            string field = value ?? string.Empty;
            manifest.Append(field.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(field)
                .Append('\n');
        }

        internal static bool SavedMainSceneMatchesCurrentSource()
        {
            if (!File.Exists(ScenePath))
                return false;
            string mainSceneText = File.ReadAllText(ScenePath);
            if (!SceneTextContainsCurrentSourceStamp(mainSceneText) ||
                !File.Exists(MukJumpSplashSceneBuilder.ScenePath) ||
                !File.Exists(SceneAttestationPath))
                return false;
            try
            {
                SceneAttestation attestation = JsonUtility.FromJson<
                    SceneAttestation>(File.ReadAllText(SceneAttestationPath));
                return SceneTextsMatchAttestation(
                    mainSceneText,
                    File.ReadAllText(MukJumpSplashSceneBuilder.ScenePath),
                    attestation);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        internal static void WriteCurrentSceneAttestation()
        {
            if (!File.Exists(ScenePath) ||
                !File.Exists(MukJumpSplashSceneBuilder.ScenePath))
                throw new FileNotFoundException(
                    "Main·Splash 씬을 모두 저장한 뒤 증명 파일을 만들 수 있습니다.");
            string mainSceneText = File.ReadAllText(ScenePath);
            if (!SceneTextContainsCurrentSourceStamp(mainSceneText))
                throw new System.InvalidOperationException(
                    "현재 생성 코드 표식이 없는 Main 씬은 증명할 수 없습니다.");
            SceneAttestation attestation = CreateSceneAttestation(
                mainSceneText,
                File.ReadAllText(MukJumpSplashSceneBuilder.ScenePath));
            File.WriteAllText(
                SceneAttestationPath,
                JsonUtility.ToJson(attestation, true) + "\n");
        }

        internal static SceneAttestation CreateSceneAttestation(
            string mainSceneText,
            string splashSceneText) =>
            new()
            {
                schemaVersion = 1,
                sourceStamp = CurrentSceneSourceStampName(),
                mainSceneHash = HashSceneText(mainSceneText),
                splashSceneHash = HashSceneText(splashSceneText),
            };

        internal static bool SceneTextsMatchAttestation(
            string mainSceneText,
            string splashSceneText,
            SceneAttestation attestation)
        {
            if (attestation == null ||
                attestation.schemaVersion != 1 ||
                !string.Equals(
                    attestation.sourceStamp,
                    CurrentSceneSourceStampName(),
                    System.StringComparison.Ordinal))
                return false;
            return string.Equals(
                       attestation.mainSceneHash,
                       HashSceneText(mainSceneText),
                       System.StringComparison.Ordinal) &&
                   string.Equals(
                       attestation.splashSceneHash,
                       HashSceneText(splashSceneText),
                       System.StringComparison.Ordinal);
        }

        static string HashSceneText(string sceneText)
        {
            string normalized = (sceneText ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            return Hash128.Compute(normalized).ToString();
        }

        [System.Serializable]
        internal sealed class SceneAttestation
        {
            public int schemaVersion;
            public string sourceStamp;
            public string mainSceneHash;
            public string splashSceneHash;
        }

        internal static bool SceneTextContainsCurrentSourceStamp(
            string sceneText)
        {
            if (string.IsNullOrEmpty(sceneText))
                return false;
            string stamp = CurrentSceneSourceStampName();
            int markerCount = 0;
            bool containsCurrent = false;
            using var reader = new StringReader(sceneText);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                const string namePrefix = "m_Name: ";
                if (!trimmed.StartsWith(
                        namePrefix,
                        System.StringComparison.Ordinal))
                    continue;
                string objectName = trimmed.Substring(namePrefix.Length);
                if (objectName.Length >= 2 &&
                    (objectName[0] == '\'' && objectName[^1] == '\'' ||
                     objectName[0] == '\"' && objectName[^1] == '\"'))
                    objectName = objectName.Substring(1, objectName.Length - 2);
                if (!objectName.StartsWith(
                        SceneSourceStampPrefix,
                        System.StringComparison.Ordinal))
                    continue;
                markerCount++;
                containsCurrent |= string.Equals(
                    objectName,
                    stamp,
                    System.StringComparison.Ordinal);
            }
            return markerCount == 1 && containsCurrent;
        }

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = InkPalette.Paper;
            // 현재 프로젝트는 후처리·HDR 색을 쓰지 않는 2D 수묵 렌더링이다.
            // 모바일에서 불필요한 HDR/MSAA 렌더 타깃을 만들지 않게 명시한다.
            cam.allowHDR = false;
            cam.allowMSAA = false;

            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraFollow>();
            go.AddComponent<ScreenSideWalls>();
            return cam;
        }

        static void BuildBackground(Transform cameraTransform)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(cameraTransform);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);

            var currentObject = new GameObject("BackgroundCurrent");
            currentObject.transform.SetParent(go.transform, false);
            var current = currentObject.AddComponent<SpriteRenderer>();
            current.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapBackgroundPaths[0]) ??
                             AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
            current.sortingOrder = -10;

            var nextObject = new GameObject("BackgroundNext");
            nextObject.transform.SetParent(go.transform, false);
            var next = nextObject.AddComponent<SpriteRenderer>();
            next.sortingOrder = -9;
            next.color = Color.clear;

            var view = go.AddComponent<MapBackgroundView>();
            var so = new SerializedObject(view);
            so.FindProperty("worldCamera").objectReferenceValue =
                cameraTransform.GetComponent<Camera>();
            so.FindProperty("currentRenderer").objectReferenceValue = current;
            so.FindProperty("nextRenderer").objectReferenceValue = next;
            var stages = so.FindProperty("stageSprites");
            stages.arraySize = MapBackgroundPaths.Length;
            for (int i = 0; i < MapBackgroundPaths.Length; i++)
                stages.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(MapBackgroundPaths[i]);
            var endlessStages = so.FindProperty("endlessStageSprites");
            endlessStages.arraySize = EndlessMapBackgroundPaths.Length;
            for (int i = 0; i < EndlessMapBackgroundPaths.Length; i++)
                endlessStages.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(EndlessMapBackgroundPaths[i]);
            so.ApplyModifiedPropertiesWithoutUndo();

            var clouds = new GameObject("AmbientClouds");
            clouds.transform.SetParent(go.transform, false);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AmbientCloudAtlasPath)
                .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            var renderers = new SpriteRenderer[AmbientCloudView.MaximumLayers];
            for (int i = 0; i < renderers.Length; i++)
            {
                var layer = new GameObject($"CloudLayer_{i:00}");
                layer.transform.SetParent(clouds.transform, false);
                var renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites.Length == 0 ? null : sprites[i % sprites.Length];
                renderer.flipX = i % 2 != 0;
                renderer.sortingOrder = AmbientCloudView.SortingOrder;
                renderers[i] = renderer;
            }
            var cloudView = clouds.AddComponent<AmbientCloudView>();
            cloudView.Configure(cameraTransform.GetComponent<Camera>(), renderers, BuildAmbientThemes());
            so.Update();
            so.FindProperty("ambientClouds").objectReferenceValue = cloudView;
            so.ApplyModifiedPropertiesWithoutUndo();
            var night = go.AddComponent<LobbyNightSkyView>();
            night.Configure(cameraTransform.GetComponent<Camera>(), new[] { current, next }, current.sprite,
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Resources/" + LobbyNightSkyView.CleanSkyResourcePath + ".png"));
        }

        internal static AmbientCloudTheme[] BuildAmbientThemes()
        {
            var paths = MapBackgroundPaths.Concat(EndlessMapBackgroundPaths).ToArray();
            var result = new AmbientCloudTheme[AmbientThemeAtlasPaths.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = new AmbientCloudTheme
                {
                    background = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]),
                    sprites = AssetDatabase.LoadAllAssetsAtPath(AmbientThemeAtlasPaths[i])
                        .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray(),
                    motion = (AmbientCloudMotion)i,
                };
            return result;
        }

        static GameObject BuildPlayer()
        {
            var frames = LoadCharacterFrames();

            var go = new GameObject("Player (먹방울이)")
            {
                layer = LayerMask.NameToLayer("Player"),
            };
            go.transform.position = new Vector3(0f, -6.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames["idle"];
            sr.sortingOrder = 5;
            sr.enabled = false;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.2f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.4f;
            circle.offset = new Vector2(0f, 0.1f);

            var playerController = go.AddComponent<PlayerController>();
            var playerSo = new SerializedObject(playerController);
            playerSo.FindProperty("deathSplashSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(DeathSplashPath);
            playerSo.FindProperty("cloneSpawnGraceDuration").floatValue = 1f;
            playerSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<PlayerHealthBillboard>();
            var itemEffectView = go.AddComponent<ItemEffectView>();
            var itemEffectSo = new SerializedObject(itemEffectView);
            itemEffectSo.FindProperty("effectDroplet").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(InkDropVfxTextureRoot + "T_VFX_InkDrop_128.png");
            itemEffectSo.FindProperty("shieldAnticipationClip").objectReferenceValue =
                LoadVfxAudio("SFX_InkDropJump_Anticipation_Stem.wav");
            itemEffectSo.FindProperty("shieldImpactClip").objectReferenceValue =
                LoadVfxAudio("SFX_InkDropJump_Impact_Stem.wav");
            itemEffectSo.FindProperty("shieldTailClip").objectReferenceValue =
                LoadVfxAudio("SFX_InkDropJump_Tail_Stem.wav");
            itemEffectSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<InkCloneArrivalView>();
            var inkDropVfx = go.AddComponent<InkDropJumpVfx>();
            var vfxSo = new SerializedObject(inkDropVfx);
            AssignVfxSprite(vfxSo, "inkDrop", "T_VFX_InkDrop_128.png");
            AssignVfxSprite(vfxSo, "groundBlob", "T_VFX_InkGroundBlob_512.png");
            AssignVfxSprite(vfxSo, "inkSplash", "T_VFX_InkSplash_512.png");
            AssignVfxSprite(vfxSo, "shockRing", "T_VFX_InkShockRing_512.png");
            AssignVfxSprite(vfxSo, "verticalBrush", "T_VFX_InkVerticalBrush_256x1024.png");
            AssignVfxSprite(vfxSo, "brushFibers", "T_VFX_BrushFibers_256x1024.png");
            AssignVfxSprite(vfxSo, "softFlash", "T_VFX_SoftFlash_256.png");
            AssignVfxSprite(vfxSo, "inkStreak", "T_VFX_InkStreak_128x512.png");
            var dropletFrames = AssetDatabase.LoadAllAssetRepresentationsAtPath(
                InkDropVfxTextureRoot + "T_VFX_InkDropletAtlas_512.png");
            var dropletProperty = vfxSo.FindProperty("dropletFrames");
            dropletProperty.arraySize = dropletFrames.Length;
            for (int i = 0; i < dropletFrames.Length; i++)
                dropletProperty.GetArrayElementAtIndex(i).objectReferenceValue = dropletFrames[i];
            vfxSo.FindProperty("immediateClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                InkDropVfxAudioRoot + "SFX_InkDropJump_Immediate.wav");
            vfxSo.FindProperty("whooshClip").objectReferenceValue = LoadVfxAudio(
                "SFX_InkDropJump_Whoosh_Stem.wav");
            vfxSo.ApplyModifiedPropertiesWithoutUndo();
            var autoJump = go.AddComponent<AutoJump>();
            var autoJumpSo = new SerializedObject(autoJump);
            autoJumpSo.FindProperty("jumpIntervalSeconds").floatValue = 1f;
            autoJumpSo.ApplyModifiedPropertiesWithoutUndo();
            var animator = go.AddComponent<CharacterAnimator>();
            var so = new SerializedObject(animator);
            foreach (var name in CharFrameNames)
                so.FindProperty(name).objectReferenceValue = frames[name];
            AssignCharacterFrameArray(
                so, "damageStageOneFrames", CharHitOneSheetPath);
            AssignCharacterFrameArray(
                so, "damageStageTwoFrames", CharHitTwoSheetPath);
            var deadProp = so.FindProperty("deadFrames");
            deadProp.arraySize = DeathFramePaths.Length;
            for (int i = 0; i < DeathFramePaths.Length; i++)
                deadProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(DeathFramePaths[i]);
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        /// 시작 버튼 방식에서도 첫 프레임에 추락하지 않도록 씬에 영구 먹 발판을 둔다.
        /// 런타임 Spawn 발판과 달리 수명·동시 획 예산을 소모하지 않는 재현 가능한 시작 지형이다.
        static void BuildStarterPlatform(GameObject player)
        {
            var go = new GameObject("StarterInkPlatform")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            go.transform.position = new Vector3(
                player.transform.position.x,
                player.transform.position.y - 0.42f,
                0f);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sortingOrder = 2;
            line.enabled = false;
            var edge = go.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-LobbyWorldSetup.StarterPlatformHalfWidth, 0f),
                new Vector2(LobbyWorldSetup.StarterPlatformHalfWidth, 0f),
            };
            edge.edgeRadius = 0.06f;
            go.AddComponent<PlatformCollider>();
        }

        static Dictionary<string, Sprite> LoadCharacterFrames()
        {
            var sheetSprites = AssetDatabase.LoadAllAssetsAtPath(CharSheetPath);
            var frames = new Dictionary<string, Sprite>();
            foreach (var obj in sheetSprites)
            {
                if (obj is Sprite sprite && System.Array.IndexOf(CharFrameNames, sprite.name) >= 0)
                    frames[sprite.name] = sprite;
            }

            foreach (var name in CharFrameNames)
            {
                if (!frames.ContainsKey(name))
                    Debug.LogWarning($"[MukJump] 캐릭터 프레임을 찾을 수 없음: {name} ({CharSheetPath})");
            }
            return frames;
        }

        static void AssignCharacterFrameArray(
            SerializedObject animator,
            string propertyName,
            string sheetPath)
        {
            var property = animator.FindProperty(propertyName);
            if (property == null) return;

            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            property.arraySize = CharFrameNames.Length;
            for (int frameIndex = 0;
                 frameIndex < CharFrameNames.Length;
                 frameIndex++)
            {
                Sprite frame = null;
                for (int assetIndex = 0; assetIndex < sprites.Length; assetIndex++)
                {
                    if (sprites[assetIndex] is Sprite candidate &&
                        candidate.name == CharFrameNames[frameIndex])
                    {
                        frame = candidate;
                        break;
                    }
                }
                property.GetArrayElementAtIndex(frameIndex).objectReferenceValue =
                    frame;
            }
        }

        static void BuildSystems(
            Camera camera,
            GameObject player,
            bool configureUiImporters)
        {
            var music = new GameObject("BackgroundMusic");
            music.AddComponent<BackgroundMusicController>();

            var go = new GameObject("Systems");
            go.AddComponent<GameManager>();
            go.AddComponent<LobbyWorldSetup>();
            go.AddComponent<BrushTransitionView>();
            go.AddComponent<GameOverPopupView>();
            go.AddComponent<PauseMenuView>();
            go.AddComponent<RunGrowthController>();
            var growthView = go.AddComponent<PermanentGrowthView>();
            var growthSettings = new SerializedObject(growthView);
            growthSettings.FindProperty("purchaseButtonTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(StartButtonPath);
            growthSettings.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<LobbyOptionsView>();
            go.AddComponent<FirstRunTutorialController>();
            go.AddComponent<LobbyScreenNavigator>();
            go.AddComponent<InkUiFeedbackController>();
            go.AddComponent<ScoreManager>();
            for (int i = 0; i < 6; i++)
                ConfigureAudioSource(go.AddComponent<AudioSource>(), loop: false, priority: 128);
            CreateFeedbackAudioChild(go.transform, "BrushDrawingAudio", loop: true, priority: 128);
            CreateFeedbackAudioChild(go.transform, "PriorityAccentAudio", loop: false, priority: 32);
            go.AddComponent<VfxAudioManager>();
            go.AddComponent<VfxRuntimeMonitor>();
            var feedback = go.AddComponent<GameFeedbackController>();
            var feedbackSo = new SerializedObject(feedback);
            feedbackSo.FindProperty("contactDropletAtlas").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(InkDropVfxTextureRoot + "T_VFX_InkDropletAtlas_512.png");
            feedbackSo.FindProperty("contactSplash").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(InkDropVfxTextureRoot + "T_VFX_InkSplash_512.png");
            feedbackSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<HeightZoneController>();
            var windWeatherController = go.AddComponent<WindWeatherController>();
            go.AddComponent<WindWeatherView>();
            var restPlatformSpawner = go.AddComponent<RestPlatformSpawner>();
            var restPlatformSo = new SerializedObject(restPlatformSpawner);
            restPlatformSo.FindProperty("firstRestHeightRange").vector2Value =
                new Vector2(22f, 28f);
            restPlatformSo.FindProperty("restHeightIntervalRange").vector2Value =
                new Vector2(28f, 38f);
            restPlatformSo.FindProperty("restPlatformWidth").floatValue =
                RestPlatformSpawner.DefaultMapRestWidth;
            restPlatformSo.FindProperty("restHorizontalOffsetRange").vector2Value =
                new Vector2(-0.9f, 0.9f);
            restPlatformSo.FindProperty("restHazardClearance").floatValue =
                RestPlatformSpawner.DefaultMapRestHazardClearance;
            restPlatformSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<SketchToInkService>();
            var strokeCapture = go.AddComponent<StrokeCapture>();
            var strokeSo = new SerializedObject(strokeCapture);
            var linePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LineSpritePrefabPath);
            var lineTexture = linePrefab != null
                ? linePrefab.GetComponent<RawImage>()?.texture as Texture2D
                : null;
            strokeSo.FindProperty("lineSpriteTexture").objectReferenceValue = lineTexture;
            strokeSo.FindProperty("inkCapacity").floatValue =
                StrokeCapture.DefaultInkCapacity;
            strokeSo.FindProperty("inkCapacityTuningVersion").intValue =
                StrokeCapture.CurrentInkCapacityTuningVersion;
            strokeSo.FindProperty("evictionFadeDuration").floatValue = 1.1f;
            strokeSo.FindProperty("naturalHoldDuration").floatValue =
                PlatformCollider.DefaultNaturalHoldDuration;
            strokeSo.ApplyModifiedPropertiesWithoutUndo();

            var obstaclesRoot = new GameObject("Obstacles");
            obstaclesRoot.transform.SetParent(go.transform);

            var obstacleSpawner = obstaclesRoot.AddComponent<ObstacleSpawner>();
            var obstacleSo = new SerializedObject(obstacleSpawner);
            obstacleSo.FindProperty("obstacleSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ObstaclePath);
            var dragonFrames = LoadDragonObstacleFrames();
            obstacleSo.FindProperty("dragonSprite").objectReferenceValue =
                dragonFrames.Length > 0
                    ? dragonFrames[0]
                    : AssetDatabase.LoadAssetAtPath<Sprite>(DragonObstaclePath);
            var dragonFrameProperty = obstacleSo.FindProperty("dragonFrames");
            dragonFrameProperty.arraySize = dragonFrames.Length;
            for (int i = 0; i < dragonFrames.Length; i++)
                dragonFrameProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                    dragonFrames[i];
            var haetaeFrames = LoadHaetaeObstacleFrames();
            obstacleSo.FindProperty("haetaeSprite").objectReferenceValue =
                haetaeFrames.Length > 0 ? haetaeFrames[0] : null;
            var haetaeFrameProperty = obstacleSo.FindProperty("haetaeFrames");
            haetaeFrameProperty.arraySize = haetaeFrames.Length;
            for (int i = 0; i < haetaeFrames.Length; i++)
                haetaeFrameProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                    haetaeFrames[i];
            obstacleSo.FindProperty("haetaeUnlockHeight").floatValue = 320f;
            obstacleSo.FindProperty("haetaeChance").floatValue = 0.08f;
            obstacleSo.FindProperty("dragonChanceBeforeHaetae").floatValue = 0.2f;
            obstacleSo.FindProperty("dragonChance").floatValue = 0.12f;
            obstacleSo.FindProperty("verticalSpacing").vector2Value =
                new Vector2(12f, 16f);
            obstacleSo.FindProperty("moveAmplitudeRange").vector2Value =
                new Vector2(1f, 2f);
            obstacleSo.FindProperty("dragonMoveAmplitudeRange").vector2Value =
                new Vector2(0.8f, 1.35f);
            obstacleSo.FindProperty("densityTuningVersion").intValue =
                ObstacleSpawner.CurrentDensityTuningVersion;
            obstacleSo.FindProperty("windWeatherController").objectReferenceValue =
                windWeatherController;
            obstacleSo.FindProperty("firstSpawnHeight").floatValue = 30f;
            obstacleSo.ApplyModifiedPropertiesWithoutUndo();

            var fallingRockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FallingInkRockPath);
            if (fallingRockSprite == null)
                Debug.LogWarning($"[MukJump] 낙묵석 스프라이트를 찾을 수 없음: {FallingInkRockPath}");
            var fallingSpawner = obstaclesRoot.AddComponent<FallingInkRockSpawner>();
            var fallingSo = new SerializedObject(fallingSpawner);
            fallingSo.FindProperty("fallingInkRockSprite").objectReferenceValue = fallingRockSprite;
            fallingSo.FindProperty("worldCamera").objectReferenceValue = camera;
            fallingSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            fallingSo.FindProperty("collisionMask").intValue =
                LayerMask.GetMask("Default", "Platform", "Player");
            fallingSo.FindProperty("startHeight").floatValue = 30f;
            fallingSo.FindProperty("lowHeightInterval").vector2Value =
                new Vector2(7f, 10f);
            fallingSo.FindProperty("highHeightInterval").vector2Value =
                new Vector2(5.5f, 7.5f);
            fallingSo.FindProperty("viewportSideMargin").floatValue = 0.08f;
            fallingSo.FindProperty("densityTuningVersion").intValue =
                FallingInkRockSpawner.CurrentDensityTuningVersion;
            fallingSo.ApplyModifiedPropertiesWithoutUndo();

            obstacleSo.Update();
            obstacleSo.FindProperty("fallingInkRockSpawner").objectReferenceValue =
                fallingSpawner;
            obstacleSo.ApplyModifiedPropertiesWithoutUndo();

            var itemSpawner = go.AddComponent<ItemSpawner>();
            var itemSo = new SerializedObject(itemSpawner);
            itemSo.FindProperty("placeholderSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ObstaclePath);
            itemSo.FindProperty("inkDropSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(InkDropItemPath);
            itemSo.FindProperty("goldenBrushSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(GoldenBrushItemPath);
            itemSo.FindProperty("inkShieldSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(InkShieldItemPath);
            itemSo.FindProperty("inkCloneSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(InkCloneItemPath);
            itemSo.FindProperty("verticalSpacing").vector2Value = new Vector2(10f, 16f);
            itemSo.FindProperty("firstSpawnHeight").floatValue = 12f;
            itemSo.FindProperty("cloneChanceAt30m").floatValue = 0.35f;
            itemSo.FindProperty("cloneChanceAt250m").floatValue = 0.5f;
            itemSo.ApplyModifiedPropertiesWithoutUndo();

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule),
                typeof(UiInputDeviceGuard));
            eventSystem.transform.SetParent(go.transform);

            var hud = go.AddComponent<PrototypeHud>();
            var so = new SerializedObject(hud);
            AssignHudTexture(so, "inkGaugeFill", GaugeFillPath,
                configureUiImporters);
            AssignHudTexture(so, "inkGaugeTrack", GaugeTrackPath,
                configureUiImporters);
            AssignHudTexture(so, "inkBrushIcon", GaugeBrushIconPath,
                configureUiImporters);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateFeedbackAudioChild(
            Transform parent,
            string objectName,
            bool loop,
            int priority)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            ConfigureAudioSource(child.AddComponent<AudioSource>(), loop, priority);
        }

        static void ConfigureAudioSource(AudioSource source, bool loop, int priority)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.priority = priority;
        }

        static void BuildLobbyUi(bool configureUiImporters)
        {
            var root = new GameObject(
                "LobbyCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(LobbyView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;

            var scaler = root.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            var safeAreaObject = new GameObject(
                "SafeAreaRoot",
                typeof(RectTransform));
            var safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRoot.SetParent(root.transform, false);
            safeAreaRoot.anchorMin = Vector2.zero;
            safeAreaRoot.anchorMax = Vector2.one;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            var contentObject = new GameObject(
                "LobbyContentRoot",
                typeof(RectTransform));
            var contentRoot = contentObject.GetComponent<RectTransform>();
            contentRoot.SetParent(safeAreaRoot, false);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;

            Texture2D logoTexture = null;
            if (configureUiImporters)
                MukJumpSplashSceneBuilder.ConfigureLocalizedLogoArtwork();
            if (AssetDatabase.GetMainAssetTypeAtPath(LobbyLogoPath) != null)
            {
                if (configureUiImporters)
                    ConfigureUiTexture(LobbyLogoPath);
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LobbyLogoPath);
            }

            if (logoTexture != null)
            {
                var logo = CreateUiObject(
                    "Logo",
                    contentRoot,
                    LobbyLogoAnchor,
                    LobbyLogoSize);
                logo.anchoredPosition = LobbyLogoPosition;
                var image = logo.gameObject.AddComponent<RawImage>();
                image.texture = logoTexture;
                image.raycastTarget = true;
                logo.gameObject.AddComponent<LobbyLogoTapTarget>();
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                InkLocalizedGameLogo.Bind(image, logoTexture);
            }
            else
            {
                var logo = CreateText("Logo", contentRoot, "먹점프", 112, FontStyle.Bold,
                    LobbyLogoAnchor, LobbyLogoSize, InkPalette.Ink);
                logo.rectTransform.anchoredPosition = LobbyLogoPosition;
                logo.gameObject.AddComponent<LobbyLogoTapTarget>();
            }

            if (configureUiImporters)
                ConfigureUiTexture(StartButtonPath);
            var lobbyBest = CreateLobbyRecordDisplay("BestDisplay", contentRoot, "최고 0",
                LobbyMenuLayout.RecordAnchor);
            var buttonTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(StartButtonPath);
            var startButton = CreateLobbyMenuButton(
                "StartButton",
                contentRoot,
                buttonTexture,
                "시작",
                LobbyMenuLayout.StartAnchor);
            var growthButton = CreateLobbyMenuButton(
                "GrowthButton",
                contentRoot,
                buttonTexture,
                "성장",
                LobbyMenuLayout.GrowthAnchor);
            var optionsButton = CreateLobbyMenuButton(
                "OptionsButton",
                contentRoot,
                buttonTexture,
                "옵션",
                LobbyMenuLayout.OptionsAnchor);

            var view = root.GetComponent<LobbyView>();
            var so = new SerializedObject(view);
            so.FindProperty("bestText").objectReferenceValue = lobbyBest;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("growthButton").objectReferenceValue = growthButton;
            so.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.RefreshResponsiveLayout();
        }

        static Button CreateLobbyMenuButton(
            string name,
            Transform parent,
            Texture2D texture,
            string label,
            Vector2 anchor)
        {
            // 시작·성장·옵션은 사용자가 지정한 예전 검정 먹물 붓 PNG를 유지한다.
            // 비대칭 원본은 배경과 글자를 반대로 보정해 화면 중앙에 맞춘다.
            var rect = CreateUiObject(
                name,
                parent,
                anchor,
                LobbyMenuLayout.BackgroundSize);
            rect.anchoredPosition = LobbyMenuLayout.ButtonPosition;
            var background = rect.gameObject.AddComponent<RawImage>();
            background.texture = texture;
            background.color = Color.white;

            var button = rect.gameObject.AddComponent<Button>();

            CreateText(
                "Label",
                rect,
                label,
                LobbyMenuLayout.FontSize,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                LobbyMenuLayout.LabelSize,
                InkPalette.TextLight);
            LobbyMenuLayout.ApplyButton(button, label, anchor);
            return button;
        }

        static Text CreateLobbyRecordDisplay(
            string name, Transform parent, string value, Vector2 anchor)
        {
            var display = CreateUiObject(
                name,
                parent,
                anchor,
                LobbyMenuLayout.BackgroundSize);
            display.anchoredPosition = LobbyMenuLayout.RecordPosition;
            var background = display.gameObject.AddComponent<RawImage>();
            background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StartButtonPath);
            background.raycastTarget = false;

            var label = CreateText(
                "Label",
                display,
                value,
                LobbyMenuLayout.FontSize,
                FontStyle.Bold,
                new Vector2(0.5f, 0.5f),
                LobbyMenuLayout.RecordLabelSize,
                Color.white);
            label.rectTransform.anchoredPosition =
                LobbyMenuLayout.RecordLabelPosition;
            label.fontSize = LobbyMenuLayout.FontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.resizeTextForBestFit = false;
            label.alignByGeometry = true;
            LobbyMenuLayout.ApplyRecord(label);
            LobbyMenuLayout.EnsureLeaderboardShortcut(label);
            return label;
        }

        static void BuildGameplayUi(bool configureUiImporters)
        {
            var root = new GameObject("GameplayCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameplayHudView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            // 저장된 Main 씬의 Game View도 런타임 로비와 같은 초기 화면을 보여 준다.
            // GameplayHudView가 Playing 진입 뒤에만 켠다.
            canvas.enabled = false;
            canvas.pixelPerfect = true;

            var scaler = root.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            var topHudRoot = CreateUiObject("TopHudRoot", root.transform, new Vector2(0.5f, 1f),
                new Vector2(900f, 148f));
            topHudRoot.pivot = new Vector2(0.5f, 1f);
            Rect initialHud = GameplayHudView.CalculateTopHudRect(
                new Rect(0f, 0f, 1080f, 1920f), 1080, 1920);
            topHudRoot.anchoredPosition = new Vector2(initialHud.center.x, initialHud.yMax);
            InkHudSurface.Ensure(topHudRoot, false);

            var display = CreateUiObject("HeightDisplay", topHudRoot, new Vector2(0.5f, 0.5f),
                new Vector2(320f, 118f));
            var label = CreateText("HeightText", display, "고도 0m", 60, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(290f, 84f), InkPalette.Ink);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 46;
            label.resizeTextMaxSize = 60;
            label.alignByGeometry = true;
            AddReadableTextWeight(label, 0.25f);

            var bestLabel = CreateText("BestText", topHudRoot, "0m", 42, FontStyle.Bold,
                new Vector2(0.785f, 0.5f), new Vector2(208f, 76f), InkPalette.Ink);
            bestLabel.resizeTextForBestFit = true;
            bestLabel.resizeTextMinSize = 38;
            bestLabel.resizeTextMaxSize = 50;
            bestLabel.alignByGeometry = true;
            AddReadableTextWeight(bestLabel, 0.25f);
            var newBestIndicator = CreateNewBestIndicator(topHudRoot);
            var windIndicator = CreateWindIndicator(topHudRoot, configureUiImporters);

            var testControls = CreateUiObject("ItemTestControls", root.transform,
                new Vector2(0f, 0.5f), new Vector2(410f, 1320f));
            testControls.pivot = new Vector2(0f, 0.5f);
            testControls.anchoredPosition = Vector2.zero;
            // 개발 기능은 코드 회귀 검증용으로만 남기고 제출 UI에는 노출하지 않는다.
            testControls.gameObject.SetActive(false);

            var debugToggleButton = CreateDebugTextButton("DebugToggleButton", testControls,
                new Vector2(8f, 580f), new Vector2(194f, 64f), "DEBUG");
            var debugPanel = CreateUiObject("DebugPanel", testControls,
                new Vector2(0f, 0.5f), new Vector2(390f, 1160f));
            debugPanel.pivot = new Vector2(0f, 0.5f);
            debugPanel.anchoredPosition = new Vector2(8f, -10f);
            var panelBackground = debugPanel.gameObject.AddComponent<Image>();
            panelBackground.color = new Color(0.11f, 0.105f, 0.1f, 0.82f);

            var placeholderTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ObstaclePath);
            var inkDropTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(InkDropItemPath);
            var goldenBrushTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GoldenBrushItemPath);
            var inkShieldTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(InkShieldItemPath);
            var inkCloneTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(InkCloneItemPath);
            var inkDropButton = CreateItemTestButton("InkDropButton", debugPanel, inkDropTexture,
                new Vector2(22f, 300f), Color.white, "50m");
            var goldenBrushButton = CreateItemTestButton("GoldenBrushButton", debugPanel,
                goldenBrushTexture != null ? goldenBrushTexture : placeholderTexture,
                new Vector2(22f, 145f), goldenBrushTexture != null ? Color.white : new Color(0.95f, 0.72f, 0.2f), "무한");
            var inkShieldButton = CreateItemTestButton("InkShieldButton", debugPanel,
                inkShieldTexture != null ? inkShieldTexture : placeholderTexture,
                new Vector2(22f, -10f), inkShieldTexture != null ? Color.white : new Color(0.72f, 0.18f, 0.28f), "방어");
            var inkCloneButton = CreateItemTestButton("InkCloneButton", debugPanel,
                inkCloneTexture != null ? inkCloneTexture : placeholderTexture,
                new Vector2(22f, -165f), inkCloneTexture != null ? Color.white : InkPalette.Ink, "분신");
            var haetaeButton = CreateDebugTextButton("HaetaeButton", debugPanel,
                new Vector2(22f, -320f), new Vector2(145f, 72f), "먹해태");
            CreateText("MapDebugTitle", debugPanel, "맵 이동", 30, FontStyle.Bold,
                new Vector2(0.76f, 0.9f), new Vector2(175f, 55f), InkPalette.Paper);
            var mapStartButton = CreateDebugTextButton("MapStartButton", debugPanel,
                new Vector2(190f, 300f), new Vector2(175f, 62f), "산길 0m");
            var mapWindButton = CreateDebugTextButton("MapWindButton", debugPanel,
                new Vector2(190f, 220f), new Vector2(175f, 62f), "바람 250m");
            var mapRainButton = CreateDebugTextButton("MapRainButton", debugPanel,
                new Vector2(190f, 140f), new Vector2(175f, 62f), "먹비 500m");
            var mapGorgeButton = CreateDebugTextButton("MapGorgeButton", debugPanel,
                new Vector2(190f, 60f), new Vector2(175f, 62f), "협곡 750m");
            var updraftButton = CreateDebugTextButton("UpdraftButton", debugPanel,
                new Vector2(190f, -40f), new Vector2(175f, 78f), "상승기류");
            var windDirectionButton = CreateDebugTextButton("WindDirectionButton", debugPanel,
                new Vector2(190f, -130f), new Vector2(175f, 72f), "풍향 전환");
            var windPlatformButton = CreateDebugTextButton("WindPlatformButton", debugPanel,
                new Vector2(190f, -220f), new Vector2(175f, 72f), "풍맥 발판");
            var invincibleButton = CreateDebugTextButton("InvincibleButton", debugPanel,
                new Vector2(190f, -310f), new Vector2(175f, 72f), "무적 OFF");
            var invincibleLabel = invincibleButton.transform.Find("Label")?.GetComponent<Text>();
            var vfxQualityButton = CreateDebugTextButton("VfxQualityButton", debugPanel,
                new Vector2(190f, -400f), new Vector2(175f, 72f), "VFX 자동");
            var vfxQualityLabel =
                vfxQualityButton.transform.Find("Label")?.GetComponent<Text>();
            var vfxStatsText = CreateText("VfxStatsText", debugPanel,
                "60 FPS · L0 S0 C0\n피크 0 · 생략 0", 21, FontStyle.Bold,
                new Vector2(0.74f, 0.035f), new Vector2(180f, 72f), InkPalette.Paper);
            vfxStatsText.resizeTextMinSize = 16;
            vfxStatsText.resizeTextMaxSize = 21;
            debugPanel.gameObject.SetActive(false);

            var view = root.GetComponent<GameplayHudView>();
            var so = new SerializedObject(view);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("topHudRoot").objectReferenceValue = topHudRoot;
            so.FindProperty("heightText").objectReferenceValue = label;
            so.FindProperty("bestText").objectReferenceValue = bestLabel;
            so.FindProperty("itemTestControls").objectReferenceValue = testControls;
            so.FindProperty("debugPanel").objectReferenceValue = debugPanel;
            so.FindProperty("debugToggleButton").objectReferenceValue = debugToggleButton;
            so.FindProperty("invincibleButton").objectReferenceValue = invincibleButton;
            so.FindProperty("invincibleLabel").objectReferenceValue = invincibleLabel;
            so.FindProperty("inkDropButton").objectReferenceValue = inkDropButton;
            so.FindProperty("goldenBrushButton").objectReferenceValue = goldenBrushButton;
            so.FindProperty("inkShieldButton").objectReferenceValue = inkShieldButton;
            so.FindProperty("inkCloneButton").objectReferenceValue = inkCloneButton;
            so.FindProperty("haetaeButton").objectReferenceValue = haetaeButton;
            so.FindProperty("mapStartButton").objectReferenceValue = mapStartButton;
            so.FindProperty("mapWindButton").objectReferenceValue = mapWindButton;
            so.FindProperty("mapRainButton").objectReferenceValue = mapRainButton;
            so.FindProperty("mapGorgeButton").objectReferenceValue = mapGorgeButton;
            so.FindProperty("updraftButton").objectReferenceValue = updraftButton;
            so.FindProperty("windDirectionButton").objectReferenceValue = windDirectionButton;
            so.FindProperty("windPlatformButton").objectReferenceValue = windPlatformButton;
            so.FindProperty("vfxQualityButton").objectReferenceValue = vfxQualityButton;
            so.FindProperty("vfxQualityLabel").objectReferenceValue = vfxQualityLabel;
            so.FindProperty("vfxStatsText").objectReferenceValue = vfxStatsText;
            so.FindProperty("windIndicator").objectReferenceValue = windIndicator;
            so.FindProperty("newBestIndicator").objectReferenceValue = newBestIndicator;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplySharedPaperLayout();

            // LineSprite 프리팹은 StrokeCapture의 붓결 텍스처 원본으로만 사용한다.
            // GameplayCanvas에 표시 인스턴스를 만들면 화면 중앙에 불필요한 획이 남는다.
        }

        static NewBestIndicatorView CreateNewBestIndicator(Transform parent)
        {
            var root = CreateUiObject("NewBestInkSeal", parent, new Vector2(0.5f, 0.5f),
                new Vector2(NewBestIndicatorView.BadgeSize, NewBestIndicatorView.BadgeHeight));
            root.anchoredPosition = new Vector2(0, NewBestIndicatorView.BadgeCenterOffsetY);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var sealRoot = CreateUiObject("RecordSeal", root, new Vector2(0.5f, 0.5f),
                new Vector2(NewBestIndicatorView.BadgeSize, NewBestIndicatorView.BadgeHeight));
            var seal = sealRoot.gameObject.AddComponent<Image>();
            seal.color = InkPalette.Red;
            seal.raycastTarget = false;
            var sealText = CreateText("SealText", sealRoot, "NEW!", NewBestIndicatorView.BadgeFontSize, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(78f, 32f), InkPalette.Red);
            sealText.resizeTextForBestFit = false;

            var view = root.gameObject.AddComponent<NewBestIndicatorView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("rootGroup").objectReferenceValue = group;
            viewSo.FindProperty("stampRoot").objectReferenceValue = sealRoot;
            viewSo.FindProperty("sealImage").objectReferenceValue = seal;
            viewSo.FindProperty("sealText").objectReferenceValue = sealText;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyPolishedLayout();
            return view;
        }

        static WindIndicatorView CreateWindIndicator(Transform parent, bool configureUiImporters)
        {
            if (configureUiImporters)
            {
                ConfigureUiTexture(GaugeFillPath);
                ConfigureWindIcon();
            }
            var brushTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GaugeFillPath);

            var root = CreateUiObject("WindInkIndicator", parent, new Vector2(0.165f, 0.5f),
                new Vector2(280f, 104f));

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var sealRect = CreateUiObject("WindAlertSeal", root, new Vector2(0.09f, 0.5f),
                new Vector2(44f, 44f));
            sealRect.localRotation = Quaternion.identity;
            var alertSeal = sealRect.gameObject.AddComponent<Image>();
            alertSeal.sprite = Resources.Load<Sprite>(WindIndicatorView.WindIconResourcePath);
            alertSeal.type = Image.Type.Simple;
            alertSeal.preserveAspect = true;
            alertSeal.color = Color.white;
            alertSeal.raycastTarget = false;
            alertSeal.enabled = alertSeal.sprite != null;

            var arrow = CreateUiObject("DirectionArrow", root, new Vector2(0.4f, 0.5f),
                new Vector2(84f, 42f));
            var shaft = CreateWindArrowPart("Shaft", arrow, new Vector2(-5f, 0f),
                new Vector2(50f, 8f), 0f, brushTexture);
            var upper = CreateWindArrowPart("UpperHead", arrow, new Vector2(18f, 8f),
                new Vector2(22f, 7f), -40f, brushTexture);
            var lower = CreateWindArrowPart("LowerHead", arrow, new Vector2(18f, -8f),
                new Vector2(22f, 7f), 40f, brushTexture);

            var state = CreateText("WindStateText", root, "산들", 34, FontStyle.Bold,
                new Vector2(0.8f, 0.5f), new Vector2(132f, 58f),
                InkPalette.Ink);
            state.resizeTextForBestFit = true;
            state.resizeTextMinSize = 26;
            state.resizeTextMaxSize = 34;
            state.alignByGeometry = true;
            AddReadableTextWeight(state, 0.22f);

            var view = root.gameObject.AddComponent<WindIndicatorView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("rootGroup").objectReferenceValue = group;
            viewSo.FindProperty("directionArrow").objectReferenceValue = arrow;
            viewSo.FindProperty("stateText").objectReferenceValue = state;
            var arrowGraphics = viewSo.FindProperty("arrowGraphics");
            arrowGraphics.arraySize = 3;
            arrowGraphics.GetArrayElementAtIndex(0).objectReferenceValue = shaft;
            arrowGraphics.GetArrayElementAtIndex(1).objectReferenceValue = upper;
            arrowGraphics.GetArrayElementAtIndex(2).objectReferenceValue = lower;
            viewSo.FindProperty("alertSeal").objectReferenceValue = alertSeal;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyPolishedLayout();
            return view;
        }

        static RawImage CreateWindArrowPart(string name, Transform parent, Vector2 position,
            Vector2 size, float angle, Texture2D brushTexture)
        {
            var rect = CreateUiObject(name, parent, new Vector2(0.5f, 0.5f), size);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = brushTexture;
            image.color = InkPalette.Ink;
            image.raycastTarget = false;
            return image;
        }

        static Button CreateDebugTextButton(string name, Transform parent, Vector2 position,
            Vector2 size, string labelText)
        {
            var rect = CreateUiObject(name, parent, new Vector2(0f, 0.5f), size);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.92f, 0.89f, 0.82f, 0.94f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var label = CreateText("Label", rect, labelText, 27, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), size - new Vector2(12f, 10f), InkPalette.Ink);
            label.raycastTarget = false;
            return button;
        }

        static Button CreateItemTestButton(string name, Transform parent, Texture2D iconTexture,
            Vector2 position, Color iconColor, string labelText)
        {
            var rect = CreateUiObject(name, parent, new Vector2(0f, 0.5f), new Vector2(145f, 136f));
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;

            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.92f, 0.89f, 0.82f, 0.9f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var icon = CreateUiObject("Icon", rect, new Vector2(0.5f, 0.58f), Vector2.zero);
            var iconImage = icon.gameObject.AddComponent<RawImage>();
            iconImage.texture = iconTexture;
            iconImage.color = iconColor;
            iconImage.raycastTarget = false;

            var label = CreateText("Label", rect, labelText, 26, FontStyle.Bold,
                new Vector2(0.5f, 0.12f), new Vector2(132f, 40f), InkPalette.Ink);
            label.raycastTarget = false;
            rect.sizeDelta = new Vector2(145f, 136f);
            label.rectTransform.sizeDelta = new Vector2(132f, 40f);
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 22;
            label.resizeTextMaxSize = 26;
            label.color = InkPalette.Ink;
            SetNativeSizeDivided(iconImage, 9f);
            return button;
        }

        static void SetNativeSizeDivided(RawImage image, float divisor)
        {
            if (image == null || image.texture == null || divisor <= 0f) return;
            image.SetNativeSize();
            image.rectTransform.sizeDelta /= divisor;
        }

        static RectTransform CreateUiObject(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        static Text CreateText(string name, Transform parent, string value, int fontSize,
            FontStyle fontStyle, Vector2 anchor, Vector2 size, Color color)
        {
            var rect = CreateUiObject(name, parent, anchor, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath) ?? InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = fontSize;
            InkLocalizedText.Bind(text);
            return text;
        }

        static void AddReadableTextWeight(Text text, float alpha)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<Outline>();
            Color ink = InkPalette.Ink;
            outline.effectColor = new Color(ink.r, ink.g, ink.b, Mathf.Clamp01(alpha));
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        static void AssignHudTexture(
            SerializedObject so,
            string field,
            string path,
            bool configureImporter)
        {
            if (configureImporter)
                ConfigureUiTexture(path);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                Debug.LogWarning($"[MukJump] HUD 텍스처를 찾을 수 없음: {path}");
            so.FindProperty(field).objectReferenceValue = tex;
        }

        static void ConfigureUiTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.GUI;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        /// 장애물은 캐릭터와 비슷한 월드 폭으로 임포트하고 런타임에서 최종 크기를 조절한다.
        static void ConfigureObstacleSprite()
        {
            ConfigureSprite(ObstaclePath, pixelsPerUnit: 700f);
        }

        static void ConfigureDragonObstacleSprite()
        {
            ConfigureSprite(DragonObstaclePath, pixelsPerUnit: 700f);
            var importer = (TextureImporter)AssetImporter.GetAtPath(DragonObstaclePath);
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            ConfigureDragonObstacleSheet();
        }

        static void ConfigureDragonObstacleSheet()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                DragonObstacleSheetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(
                DragonObstacleSheetPath);
            if (texture == null || importer == null)
            {
                Debug.LogWarning(
                    $"[MukJump] 어린 용 애니메이션 시트를 찾을 수 없음: {DragonObstacleSheetPath}");
                return;
            }

            const int columns = 2;
            const int rows = 2;
            importer.GetSourceTextureWidthAndHeight(
                out int sourceWidth, out int sourceHeight);
            if (sourceWidth % columns != 0 || sourceHeight % rows != 0)
            {
                Debug.LogError(
                    $"[MukJump] 어린 용 시트 크기는 2×2로 나누어져야 함: " +
                    $"{sourceWidth}×{sourceHeight}");
                return;
            }
            int frameWidth = sourceWidth / columns;
            int frameHeight = sourceHeight / rows;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 700f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            var importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            importerSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importerSettings);

            var metas = new SpriteMetaData[columns * rows];
            Vector2[] visualPivots = CalculateOpaqueCentroidPivots(
                DragonObstacleSheetPath,
                columns,
                rows,
                frameWidth,
                frameHeight);
            for (int i = 0; i < metas.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                metas[i] = new SpriteMetaData
                {
                    name = $"child_ink_dragon_frame_{i:00}",
                    rect = new Rect(
                        column * frameWidth,
                        (rows - 1 - row) * frameHeight,
                        frameWidth,
                        frameHeight),
                    // 프레임마다 몸을 굽히면서 원화 중심이 움직여도 실제 먹 실루엣의
                    // 무게중심은 같은 Transform에 고정해 애니메이션 떨림을 막는다.
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = visualPivots != null &&
                            i < visualPivots.Length
                        ? visualPivots[i]
                        : new Vector2(0.5f, 0.5f),
                };
            }
#pragma warning disable CS0618
            importer.spritesheet = metas;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static Vector2[] CalculateOpaqueCentroidPivots(
            string assetPath,
            int columns,
            int rows,
            int frameWidth,
            int frameHeight)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return null;
            string fullPath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(fullPath))
                return null;

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(
                        source, File.ReadAllBytes(fullPath), false))
                    return null;

                Color32[] pixels = source.GetPixels32();
                var pivots = new Vector2[columns * rows];
                for (int frame = 0; frame < pivots.Length; frame++)
                {
                    int column = frame % columns;
                    int row = frame / columns;
                    int originX = column * frameWidth;
                    int originY = (rows - 1 - row) * frameHeight;
                    long sumX = 0;
                    long sumY = 0;
                    int visibleCount = 0;
                    for (int y = 0; y < frameHeight; y++)
                    {
                        for (int x = 0; x < frameWidth; x++)
                        {
                            int sourceIndex =
                                originX + x + (originY + y) * source.width;
                            if (pixels[sourceIndex].a < 128) continue;
                            sumX += x;
                            sumY += y;
                            visibleCount++;
                        }
                    }

                    pivots[frame] = visibleCount > 0
                        ? new Vector2(
                            (sumX / (float)visibleCount + 0.5f) / frameWidth,
                            (sumY / (float)visibleCount + 0.5f) / frameHeight)
                        : new Vector2(0.5f, 0.5f);
                }
                return pivots;
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        static Sprite[] LoadDragonObstacleFrames()
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(
                DragonObstacleSheetPath);
            var frames = new List<Sprite>(4);
            for (int i = 0; i < allAssets.Length; i++)
                if (allAssets[i] is Sprite sprite)
                    frames.Add(sprite);
            frames.Sort((left, right) =>
                string.CompareOrdinal(left.name, right.name));
            return frames.ToArray();
        }

        static void ConfigureHaetaeObstacleSheet()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                HaetaeObstacleSheetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(
                HaetaeObstacleSheetPath);
            if (texture == null || importer == null)
            {
                Debug.LogWarning(
                    $"[MukJump] 어린 해태 애니메이션 시트를 찾을 수 없음: {HaetaeObstacleSheetPath}");
                return;
            }

            const int columns = 2;
            const int rows = 2;
            importer.GetSourceTextureWidthAndHeight(
                out int sourceWidth, out int sourceHeight);
            if (sourceWidth != 1254 || sourceHeight != 1254 ||
                sourceWidth % columns != 0 || sourceHeight % rows != 0)
            {
                Debug.LogError(
                    $"[MukJump] 어린 해태 시트는 1254×1254의 2×2 그리드여야 함: " +
                    $"{sourceWidth}×{sourceHeight}");
                return;
            }
            int frameWidth = sourceWidth / columns;
            int frameHeight = sourceHeight / rows;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 700f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            var importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            importerSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importerSettings);

            // 좌상단부터 서기 → 웅크리기 → 돌진 → 착지 순서로 고정한다.
            // 프레임마다 투명 여백이 달라 중앙 피벗을 쓰면 몸이 최대 약 0.5월드 단위
            // 튄다. 각 셀의 불투명 몸체 중심을 같은 원점에 맞춘 피벗을 사용한다.
            var bodyPivots = new[]
            {
                new Vector2(350f / 627f, 287.5f / 627f),
                new Vector2(293f / 627f, 256.5f / 627f),
                new Vector2(344.5f / 627f, 399f / 627f),
                new Vector2(286.5f / 627f, 359.5f / 627f),
            };
            var metas = new SpriteMetaData[columns * rows];
            for (int i = 0; i < metas.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                metas[i] = new SpriteMetaData
                {
                    name = $"child_ink_haetae_frame_{i:00}",
                    rect = new Rect(
                        column * frameWidth,
                        (rows - 1 - row) * frameHeight,
                        frameWidth,
                        frameHeight),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = bodyPivots[i],
                };
            }
#pragma warning disable CS0618
            importer.spritesheet = metas;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static Sprite[] LoadHaetaeObstacleFrames()
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(
                HaetaeObstacleSheetPath);
            var frames = new List<Sprite>(4);
            for (int i = 0; i < allAssets.Length; i++)
                if (allAssets[i] is Sprite sprite)
                    frames.Add(sprite);
            frames.Sort((left, right) =>
                string.CompareOrdinal(left.name, right.name));
            return frames.ToArray();
        }

        static void ConfigureFallingInkRockSprite()
        {
            ConfigureSprite(FallingInkRockPath, pixelsPerUnit: 700f);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FallingInkRockPath);
            if (importer == null) return;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void ConfigureItemSprites()
        {
            ConfigureItemSprite(InkDropItemPath, "먹물방울");
            ConfigureItemSprite(GoldenBrushItemPath, "황금 붓");
            ConfigureItemSprite(InkShieldItemPath, "먹 방어막");
            ConfigureItemSprite(InkCloneItemPath, "먹분신");
        }

        static void ConfigureInkDropJumpVfxAssets()
        {
            string[] textures =
            {
                "T_VFX_InkDrop_128.png", "T_VFX_InkGroundBlob_512.png",
                "T_VFX_InkSplash_512.png", "T_VFX_InkShockRing_512.png",
                "T_VFX_InkVerticalBrush_256x1024.png", "T_VFX_BrushFibers_256x1024.png",
                "T_VFX_SoftFlash_256.png", "T_VFX_InkStreak_128x512.png",
                "T_VFX_InkDropletAtlas_512.png",
            };
            for (int i = 0; i < textures.Length; i++)
            {
                string path = InkDropVfxTextureRoot + textures[i];
                ConfigureSprite(path, 256f);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            ConfigureDropletAtlas();
        }

        static void ConfigureDropletAtlas()
        {
            string path = InkDropVfxTextureRoot + "T_VFX_InkDropletAtlas_512.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 256f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var metas = new SpriteMetaData[16];
            for (int i = 0; i < metas.Length; i++)
            {
                int column = i % 4;
                int row = i / 4;
                metas[i] = new SpriteMetaData
                {
                    name = $"ink_droplet_{i:00}",
                    rect = new Rect(column * 128, (3 - row) * 128, 128, 128),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
            }
#pragma warning disable CS0618
            importer.spritesheet = metas;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static AudioClip LoadVfxAudio(string fileName)
        {
            string path = InkDropVfxAudioRoot + fileName;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[MukJump] VFX 효과음을 찾을 수 없음: {path}");
            return clip;
        }

        static void AssignVfxSprite(SerializedObject target, string propertyName, string fileName)
        {
            string path = InkDropVfxTextureRoot + fileName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[MukJump] 먹물방울 VFX 스프라이트를 찾을 수 없음: {path}");
            target.FindProperty(propertyName).objectReferenceValue = sprite;
        }

        static void ConfigureItemSprite(string path, string displayName)
        {
            ConfigureSprite(path, pixelsPerUnit: 700f);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[MukJump] {displayName} 아이템 스프라이트를 찾을 수 없음: {path}");
                return;
            }
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        /// 배경 이미지의 픽셀 폭이 얼마든 월드 폭 10.8유닛(화면 가득)이 되도록 PPU를 계산한다
        static void ConfigureBackground()
        {
            ConfigureBackgroundSet(MapBackgroundPaths);
            ConfigureBackgroundSet(EndlessMapBackgroundPaths);
        }

        static void ConfigureAmbientCloudAtlas(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // 원화의 실제 투명 여백에서 나눠 옅은 꼬리와 별가루가 잘리지 않게 합니다.
            int[] boundaries = path == AmbientThemeAtlasPaths[3] ? new[] { 0, 360, 710, height } :
                path == AmbientThemeAtlasPaths[4] ? new[] { 0, 350, 665, height } :
                new[] { 0, height / 3, height * 2 / 3, height };
            var rows = new SpriteMetaData[3];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = new SpriteMetaData
                {
                    name = $"ambient_cloud_{i:00}",
                    rect = new Rect(0, height - boundaries[i + 1], width, boundaries[i + 1] - boundaries[i]),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
#pragma warning disable CS0618
            importer.spritesheet = rows;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        [MenuItem("MukJump/Configure Lobby Sky")]
        public static void ConfigureLobbySky()
        {
            string path = "Assets/Resources/" + LobbyNightSkyView.CleanSkyResourcePath + ".png";
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Default && importer.maxTextureSize == 2048 &&
                !importer.isReadable && !importer.mipmapEnabled && !importer.alphaIsTransparency &&
                importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ)
                return;
            // 원래 산수화는 그대로 두고 해 주변의 작은 영역만 이 텍스처에서 읽는다.
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void ConfigureBackgroundSet(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                ConfigureSprite(path, pixelsPerUnit: 100f);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                ConfigureSprite(path, pixelsPerUnit: tex.width / WorldScreenWidth);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[MukJump] 텍스처를 찾을 수 없음: {path}");
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        /// 4×2 스프라이트시트를 8개의 서브스프라이트로 슬라이스하고 CharFrameNames 순서대로 이름을 붙인다
        [MenuItem("MukJump/Configure Character Sheets")]
        public static void ConfigureCharacterSheets()
        {
            ConfigureCharacterSheet(CharSheetPath, CharPpu);
            ConfigureCharacterSheet(CharHitOneSheetPath, CharHitOnePpu);
            ConfigureCharacterSheet(CharHitTwoSheetPath, CharHitTwoPpu);
        }

        static void ConfigureCharacterSheet(string sheetPath, float pixelsPerUnit)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            if (importer == null)
            {
                Debug.LogWarning($"[MukJump] 텍스처를 찾을 수 없음: {sheetPath}");
                return;
            }

            importer.GetSourceTextureWidthAndHeight(
                out int sourceWidth,
                out int sourceHeight);
            if (!IsExpectedCharacterSheetDimensions(
                    sourceWidth,
                    sourceHeight))
                throw new System.InvalidOperationException(
                    "캐릭터 시트는 정확히 4096×2048이어야 합니다: " +
                    $"{sheetPath} ({sourceWidth}×{sourceHeight})");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            // 텍스처 좌표는 좌하단이 원점이므로, 이미지 위쪽 줄일수록 y가 커진다
            int rows = Mathf.CeilToInt(
                CharFrameNames.Length / (float)CharSheetColumns);

            // 기본 Max Size(2048)보다 시트가 크면(4096폭) 임포트 시 축소되어 픽셀 슬라이스
            // 좌표가 틀어진다 — 시트 실제 크기 이상으로 명시
            importer.maxTextureSize = CharacterSheetWidth;
            ConfigureCharacterSheetPlatform(
                importer,
                "iPhone",
                TextureImporterFormat.ASTC_4x4);
            ConfigureCharacterSheetPlatform(
                importer,
                "WebGL",
                TextureImporterFormat.ASTC_4x4);
            ConfigureCharacterSheetPlatform(
                importer,
                "Android",
                TextureImporterFormat.Automatic);
            var metas = new SpriteMetaData[CharFrameNames.Length];
            for (int i = 0; i < CharFrameNames.Length; i++)
            {
                int col = i % CharSheetColumns;
                int row = i / CharSheetColumns; // 0 = 이미지 맨 윗줄
                metas[i] = new SpriteMetaData
                {
                    name = CharFrameNames[i],
                    rect = new Rect(col * CharFrameSize, (rows - 1 - row) * CharFrameSize, CharFrameSize, CharFrameSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
            }

#pragma warning disable CS0618 // SpriteMetaData/spritesheet: 슬라이스 정보를 직접 지정하기 위해 구 API 사용
            importer.spritesheet = metas;
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        static void ConfigureCharacterSheetPlatform(
            TextureImporter importer,
            string platform,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = CharacterSheetWidth;
            settings.format = format;
            settings.textureCompression =
                TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        /// 개별 1024×1024 프레임을 모두 같은 PPU와 중앙 피벗으로 임포트한다.
        static void ConfigureDeathSprites()
        {
            foreach (var path in DeathFramePaths)
                ConfigureSprite(path, DeathPpu);
            ConfigureSprite(DeathSplashPath, 300f);
        }

        static void EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (element.stringValue == layerName) return;
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            Debug.LogError($"[MukJump] 빈 레이어 슬롯이 없어 '{layerName}' 레이어를 추가하지 못함");
        }
    }
}
