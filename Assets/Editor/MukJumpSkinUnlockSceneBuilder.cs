using System.Linq;
using MukJump.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MukJump.EditorTools
{
    public static class MukJumpSkinUnlockSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/SkinUnlock.unity";
        public static void BuildSceneFile()
        {
            var previous = SceneManager.GetActiveScene();
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new System.InvalidOperationException("열려 있는 해금 씬은 자동 교체하지 않습니다.");
            // 저장되지 않은 기본 씬이 열려 있어도 기존 씬 복사본은 안전하게 additive로 열 수 있다.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null &&
                !AssetDatabase.CopyAsset(MukJumpSplashSceneBuilder.ScenePath, ScenePath))
                throw new System.IO.IOException("해금 씬 생성 실패");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            var camera = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = InkPalette.Paper;
            camera.orthographic = true; camera.orthographicSize = 10f; camera.transform.position = new Vector3(0, 0, -10);
            var canvas = new GameObject("Landscape", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = -1;
            var landscape = new GameObject("Mountains", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.AspectRatioFitter));
            landscape.transform.SetParent(canvas.transform, false);
            var image = landscape.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/Maps/map_00_quiet_mountain.png");
            image.raycastTarget = false;
            var fitter = landscape.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            var systems = new GameObject("UnlockScene", typeof(BrushTransitionView), typeof(VfxAudioManager),
                typeof(InkSkinUnlockView), typeof(SkinUnlockSceneController));
            var settings = new SerializedObject(systems.GetComponent<InkSkinUnlockView>());
            // 원먹 idle 스프라이트는 Main 쇼룸과 같은 빌더가 넘긴다.
            settings.FindProperty("previewBodySprite").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s => s.name == "idle");
            settings.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(UiInputDeviceGuard));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.IO.IOException("해금 씬 저장 실패");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
