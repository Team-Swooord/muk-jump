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
        const string CharPath = "Assets/Art/Character/character_muk_bangul_v3.png";

        // 배경 1080×1920, PPU 100 → 월드 10.8×19.2. 세로(9:16) 화면 가득 채우는 카메라 크기
        const float OrthoSize = 9.6f;

        [MenuItem("MukJump/Build Main Scene")]
        public static void Build()
        {
            EnsureLayer("Platform");
            ConfigureSprite(BgPath, pixelsPerUnit: 100);
            ConfigureSprite(CharPath, pixelsPerUnit: 900);

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
            var go = new GameObject("Player (먹방울이)");
            go.transform.position = new Vector3(0f, -6f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharPath);
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
            return go;
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
        }

        static void BuildSystems()
        {
            var go = new GameObject("Systems");
            go.AddComponent<GameManager>();
            go.AddComponent<ScoreManager>();
            go.AddComponent<SketchToInkService>();
            go.AddComponent<StrokeCapture>();
            go.AddComponent<PrototypeHud>();
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
