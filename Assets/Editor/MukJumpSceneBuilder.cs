using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.EditorTools
{
    /// 메뉴 "MukJump > Build Main Scene" 한 번으로 플레이 가능한 Main 씬을 구성한다.
    /// (씬 구성을 코드로 남겨 두면 협업 시 씬 머지 충돌을 피하고 재현 가능)
    public static class MukJumpSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BgPath = "Assets/Art/Background/background_ink_landscape.png";
        const string CharSheetPath = "Assets/Art/Character/muk_spritesheet.png";
        const int CharFrameSize = 1024;
        const int CharSheetColumns = 4;

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
            EnsureLayer("Platform");
            ConfigureBackground();
            ConfigureCharacterSheet();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            BuildBackground(camera.transform);
            var player = BuildPlayer();
            BuildStartGround();
            BuildSystems();

            var follow = camera.GetComponent<CameraFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("[MukJump] Main 씬 구성 완료 — Game 뷰를 9:16으로 두고 Play 하세요.");
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

            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraFollow>();
            return cam;
        }

        static void BuildBackground(Transform cameraTransform)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(cameraTransform);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
            sr.sortingOrder = -10;
        }

        static GameObject BuildPlayer()
        {
            var frames = LoadCharacterFrames();

            var go = new GameObject("Player (먹방울이)");
            go.transform.position = new Vector3(0f, -6f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames["idle"];
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.2f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.4f;
            circle.offset = new Vector2(0f, 0.1f);

            go.AddComponent<PlayerController>();
            go.AddComponent<AutoJump>();

            var animator = go.AddComponent<CharacterAnimator>();
            var so = new SerializedObject(animator);
            foreach (var name in CharFrameNames)
                so.FindProperty(name).objectReferenceValue = frames[name];
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
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

        static void BuildStartGround()
        {
            var go = new GameObject("StartGround")
            {
                layer = LayerMask.NameToLayer("Platform"),
            };
            go.transform.position = new Vector3(0f, -6.8f, 0f);

            // PlatformCollider가 Start()에서 콜라이더 점을 읽어 영구 발판으로 비주얼을 구성한다
            var platform = go.AddComponent<PlatformCollider>();
            var edge = go.GetComponent<EdgeCollider2D>();
            edge.points = new[] { new Vector2(-1.6f, 0f), new Vector2(1.6f, 0f) };
            edge.edgeRadius = 0.06f;

            var platformSo = new SerializedObject(platform);
            platformSo.FindProperty("isStartPlatform").boolValue = true;
            platformSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildSystems()
        {
            var go = new GameObject("Systems");
            go.AddComponent<GameManager>();
            go.AddComponent<ScoreManager>();
            go.AddComponent<SketchToInkService>();
            go.AddComponent<StrokeCapture>();

            var hud = go.AddComponent<PrototypeHud>();
            var so = new SerializedObject(hud);
            AssignHudTexture(so, "inkGaugeFill", "Assets/Art/UI/muk_gauge_fill.png");
            AssignHudTexture(so, "inkGaugeTrack", "Assets/Art/UI/muk_gauge_track.png");
            AssignHudTexture(so, "inkBrushIcon", "Assets/Art/UI/muk_brush_icon.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignHudTexture(SerializedObject so, string field, string path)
        {
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

        /// 배경 이미지의 픽셀 폭이 얼마든 월드 폭 10.8유닛(화면 가득)이 되도록 PPU를 계산한다
        static void ConfigureBackground()
        {
            ConfigureSprite(BgPath, pixelsPerUnit: 100); // 우선 임포트 확정
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BgPath);
            if (tex == null) return;
            ConfigureSprite(BgPath, pixelsPerUnit: tex.width / WorldScreenWidth);
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
        static void ConfigureCharacterSheet()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(CharSheetPath);
            if (importer == null)
            {
                Debug.LogWarning($"[MukJump] 텍스처를 찾을 수 없음: {CharSheetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 900;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            // 텍스처 좌표는 좌하단이 원점이므로, 이미지 위쪽 줄일수록 y가 커진다
            int rows = Mathf.CeilToInt(CharFrameNames.Length / (float)CharSheetColumns);

            // 기본 Max Size(2048)보다 시트가 크면(4096폭) 임포트 시 축소되어 픽셀 슬라이스
            // 좌표가 틀어진다 — 시트 실제 크기 이상으로 명시
            importer.maxTextureSize = Mathf.Max(CharSheetColumns * CharFrameSize, rows * CharFrameSize);
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
