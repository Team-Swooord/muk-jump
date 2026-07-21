using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MukJump.AI;
using MukJump.Core;
using MukJump.Drawing;
using MukJump.Items;
using MukJump.Player;
using MukJump.Obstacles;

namespace MukJump.EditorTools
{
    /// 메뉴 "MukJump > Build Main Scene" 한 번으로 플레이 가능한 Main 씬을 구성한다.
    /// (씬 구성을 코드로 남겨 두면 협업 시 씬 머지 충돌을 피하고 재현 가능)
    public static class MukJumpSceneBuilder
    {
        struct UiLayout
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
        }

        struct UiTextStyle
        {
            public int FontSize;
            public FontStyle FontStyle;
            public TextAnchor Alignment;
            public Color Color;
            public bool ResizeForBestFit;
            public int ResizeMin;
            public int ResizeMax;
        }

        static readonly Dictionary<string, UiLayout> preservedUiLayouts = new();
        static readonly Dictionary<string, UiTextStyle> preservedTextStyles = new();
        static readonly Dictionary<string, Color> preservedImageColors = new();

        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BgPath = "Assets/Art/Background/background_ink_landscape.png";
        const string CharSheetPath = "Assets/Art/Character/Player/muk_spritesheet.png";
        const string ObstaclePath = "Assets/Art/Character/Obstacles/anermy_01.png";
        const string FallingInkRockPath = "Assets/Art/Character/Obstacles/anermy_02.png";
        const string LobbyLogoPath = "Assets/Art/UI/muk_logo.png";
        const string StartButtonPath = "Assets/Art/UI/muk_start_button.png";
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
        const float CharPpu = 900f;
        // 사망 원본은 같은 1024 캔버스 안에서 캐릭터가 약 80% 크기로 들어가 있어
        // 일반/점프 프레임과 화면상 몸통 크기를 맞추기 위해 1.25배 확대한다.
        const float DeathPpu = 720f;
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
            CaptureUiLayouts();
            EnsureLayer("Platform");
            EnsureLayer("Obstacle");
            EnsureLayer("Item");
            ConfigureBackground();
            ConfigureCharacterSheet();
            ConfigureDeathSprites();
            ConfigureObstacleSprite();
            ConfigureFallingInkRockSprite();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            BuildBackground(camera.transform);
            var player = BuildPlayer();
            BuildSystems(camera, player);
            BuildLobbyUi();
            BuildGameplayUi();

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
            go.AddComponent<ItemEffectView>();
            go.AddComponent<AutoJump>();

            var animator = go.AddComponent<CharacterAnimator>();
            var so = new SerializedObject(animator);
            foreach (var name in CharFrameNames)
                so.FindProperty(name).objectReferenceValue = frames[name];
            var deadProp = so.FindProperty("deadFrames");
            deadProp.arraySize = DeathFramePaths.Length;
            for (int i = 0; i < DeathFramePaths.Length; i++)
                deadProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(DeathFramePaths[i]);
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

        static void BuildSystems(Camera camera, GameObject player)
        {
            var go = new GameObject("Systems");
            go.AddComponent<GameManager>();
            go.AddComponent<ScoreManager>();
            go.AddComponent<SketchToInkService>();
            go.AddComponent<StrokeCapture>();

            var obstaclesRoot = new GameObject("Obstacles");
            obstaclesRoot.transform.SetParent(go.transform);

            var obstacleSpawner = obstaclesRoot.AddComponent<ObstacleSpawner>();
            var obstacleSo = new SerializedObject(obstacleSpawner);
            obstacleSo.FindProperty("obstacleSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ObstaclePath);
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
                LayerMask.GetMask("Default", "Platform");
            fallingSo.ApplyModifiedPropertiesWithoutUndo();

            var itemSpawner = go.AddComponent<ItemSpawner>();
            var itemSo = new SerializedObject(itemSpawner);
            itemSo.FindProperty("placeholderSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(ObstaclePath);
            itemSo.ApplyModifiedPropertiesWithoutUndo();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(go.transform);

            var hud = go.AddComponent<PrototypeHud>();
            var so = new SerializedObject(hud);
            AssignHudTexture(so, "inkGaugeFill", "Assets/Art/UI/muk_gauge_fill.png");
            AssignHudTexture(so, "inkGaugeTrack", "Assets/Art/UI/muk_gauge_track.png");
            AssignHudTexture(so, "inkBrushIcon", "Assets/Art/UI/muk_brush_icon.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildLobbyUi()
        {
            var root = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(LobbyView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            Texture2D logoTexture = null;
            if (AssetDatabase.GetMainAssetTypeAtPath(LobbyLogoPath) != null)
            {
                ConfigureUiTexture(LobbyLogoPath);
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LobbyLogoPath);
            }

            if (logoTexture != null)
            {
                var logo = CreateUiObject("Logo", root.transform, new Vector2(0.5f, 0.68f),
                    new Vector2(900f, 600f));
                var image = logo.gameObject.AddComponent<RawImage>();
                image.texture = logoTexture;
                image.raycastTarget = false;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                RestoreUiLayout(logo);
            }
            else
            {
                CreateText("Logo", root.transform, "먹점프", 112, FontStyle.Bold,
                    new Vector2(0.5f, 0.68f), new Vector2(720f, 220f), InkPalette.Ink);
            }

            var brush = CreateUiObject("BrushGuide", root.transform, new Vector2(0.5f, 0.5f),
                new Vector2(105f, 105f));
            brush.anchoredPosition = new Vector2(0f, -620f);
            RestoreUiLayout(brush);
            var brushImage = brush.gameObject.AddComponent<RawImage>();
            brushImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/muk_brush_icon.png");
            brushImage.raycastTarget = false;
            var brushGroup = brush.gameObject.AddComponent<CanvasGroup>();
            brushGroup.alpha = 0.5f;
            brushGroup.interactable = false;
            brushGroup.blocksRaycasts = false;

            var view = root.GetComponent<LobbyView>();
            var so = new SerializedObject(view);
            so.FindProperty("brushGuide").objectReferenceValue = brush;
            so.FindProperty("brushCanvasGroup").objectReferenceValue = brushGroup;
            so.FindProperty("canvasRect").objectReferenceValue = root.GetComponent<RectTransform>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildGameplayUi()
        {
            var root = new GameObject("GameplayCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameplayHudView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvas.enabled = false; // 편집/로비에서는 숨기고 GameplayHudView가 플레이 시작 시 켠다

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            ConfigureUiTexture(StartButtonPath);
            var display = CreateUiObject("HeightDisplay", root.transform, new Vector2(0.5f, 0.94f),
                new Vector2(500f, 110f));
            var background = display.gameObject.AddComponent<RawImage>();
            background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StartButtonPath);
            background.raycastTarget = false;
            RestoreUiLayout(display);

            var label = CreateText("HeightText", display, "고도 0", 46, FontStyle.Bold,
                new Vector2(0.5f, 0.5f), new Vector2(400f, 80f), Color.white);
            RestoreUiLayout(label.rectTransform);
            var bestLabel = CreateText("BestText", root.transform, "최고 0", 30, FontStyle.Normal,
                new Vector2(0.5f, 0.89f), new Vector2(360f, 60f), InkPalette.TextMuted);
            RestoreUiLayout(bestLabel.rectTransform);

            var testControls = CreateUiObject("ItemTestControls", root.transform,
                new Vector2(0f, 0.5f), new Vector2(170f, 500f));
            testControls.pivot = new Vector2(0f, 0.5f);
            testControls.anchoredPosition = new Vector2(25f, 0f);
            RestoreUiLayout(testControls);

            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ObstaclePath);
            var inkDropButton = CreateItemTestButton("InkDropButton", testControls, iconTexture,
                new Vector2(0f, 150f), new Color(0.42f, 0.62f, 0.72f), "50m");
            var goldenBrushButton = CreateItemTestButton("GoldenBrushButton", testControls, iconTexture,
                Vector2.zero, new Color(0.95f, 0.72f, 0.2f), "무한");
            var inkShieldButton = CreateItemTestButton("InkShieldButton", testControls, iconTexture,
                new Vector2(0f, -150f), new Color(0.72f, 0.18f, 0.28f), "방어");

            var view = root.GetComponent<GameplayHudView>();
            var so = new SerializedObject(view);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("heightText").objectReferenceValue = label;
            so.FindProperty("bestText").objectReferenceValue = bestLabel;
            so.FindProperty("itemTestControls").objectReferenceValue = testControls;
            so.FindProperty("inkDropButton").objectReferenceValue = inkDropButton;
            so.FindProperty("goldenBrushButton").objectReferenceValue = goldenBrushButton;
            so.FindProperty("inkShieldButton").objectReferenceValue = inkShieldButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button CreateItemTestButton(string name, Transform parent, Texture2D iconTexture,
            Vector2 position, Color iconColor, string labelText)
        {
            var rect = CreateUiObject(name, parent, new Vector2(0f, 0.5f), new Vector2(130f, 130f));
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;

            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.92f, 0.89f, 0.82f, 0.9f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var icon = CreateUiObject("Icon", rect, new Vector2(0.5f, 0.58f), new Vector2(88f, 88f));
            var iconImage = icon.gameObject.AddComponent<RawImage>();
            iconImage.texture = iconTexture;
            iconImage.color = iconColor;
            iconImage.raycastTarget = false;

            var label = CreateText("Label", rect, labelText, 24, FontStyle.Bold,
                new Vector2(0.5f, 0.12f), new Vector2(110f, 34f), InkPalette.Ink);
            label.raycastTarget = false;
            RestoreUiLayout(rect);
            RestoreUiLayout(icon);
            RestoreUiLayout(label.rectTransform);
            return button;
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

        /// 씬 빌더를 다시 실행해도 사용자가 Inspector에서 조정한 UI 배치를 보존한다.
        static void CaptureUiLayouts()
        {
            preservedUiLayouts.Clear();
            preservedTextStyles.Clear();
            preservedImageColors.Clear();
            if (EditorSceneManager.GetActiveScene().path != ScenePath) return;

            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                string path = HierarchyPath(rect);
                if (!path.StartsWith("LobbyCanvas/") && !path.StartsWith("GameplayCanvas/")) continue;
                preservedUiLayouts[path] = new UiLayout
                {
                    AnchorMin = rect.anchorMin,
                    AnchorMax = rect.anchorMax,
                    Pivot = rect.pivot,
                    AnchoredPosition = rect.anchoredPosition,
                    SizeDelta = rect.sizeDelta,
                };

                var text = rect.GetComponent<Text>();
                if (text != null)
                {
                    preservedTextStyles[path] = new UiTextStyle
                    {
                        FontSize = text.fontSize,
                        FontStyle = text.fontStyle,
                        Alignment = text.alignment,
                        Color = text.color,
                        ResizeForBestFit = text.resizeTextForBestFit,
                        ResizeMin = text.resizeTextMinSize,
                        ResizeMax = text.resizeTextMaxSize,
                    };
                }

                var image = rect.GetComponent<RawImage>();
                if (image != null)
                    preservedImageColors[path] = image.color;
            }
        }

        static void RestoreUiLayout(RectTransform rect)
        {
            if (!preservedUiLayouts.TryGetValue(HierarchyPath(rect), out var layout)) return;
            rect.anchorMin = layout.AnchorMin;
            rect.anchorMax = layout.AnchorMax;
            rect.pivot = layout.Pivot;
            rect.anchoredPosition = layout.AnchoredPosition;
            rect.sizeDelta = layout.SizeDelta;

            string path = HierarchyPath(rect);
            var text = rect.GetComponent<Text>();
            if (text != null && preservedTextStyles.TryGetValue(path, out var textStyle))
            {
                text.fontSize = textStyle.FontSize;
                text.fontStyle = textStyle.FontStyle;
                text.alignment = textStyle.Alignment;
                text.color = textStyle.Color;
                text.resizeTextForBestFit = textStyle.ResizeForBestFit;
                text.resizeTextMinSize = textStyle.ResizeMin;
                text.resizeTextMaxSize = textStyle.ResizeMax;
            }

            var image = rect.GetComponent<RawImage>();
            if (image != null && preservedImageColors.TryGetValue(path, out var imageColor))
                image.color = imageColor;
        }

        static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        static Text CreateText(string name, Transform parent, string value, int fontSize,
            FontStyle fontStyle, Vector2 anchor, Vector2 size, Color color)
        {
            var rect = CreateUiObject(name, parent, anchor, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = fontSize;
            return text;
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

        /// 장애물은 캐릭터와 비슷한 월드 폭으로 임포트하고 런타임에서 최종 크기를 조절한다.
        static void ConfigureObstacleSprite()
        {
            ConfigureSprite(ObstaclePath, pixelsPerUnit: 700f);
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

        /// 개별 1024×1024 프레임을 모두 같은 PPU와 중앙 피벗으로 임포트한다.
        static void ConfigureDeathSprites()
        {
            foreach (var path in DeathFramePaths)
                ConfigureSprite(path, DeathPpu);
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
