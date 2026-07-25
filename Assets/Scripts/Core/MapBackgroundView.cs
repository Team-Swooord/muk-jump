using System.Collections;
using UnityEngine;

namespace MukJump.Core
{
    /// 고도별 수채화 맵 배경을 두 장의 렌더러로 재사용해 부드럽게 교차 전환한다.
    public sealed class MapBackgroundView : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] SpriteRenderer currentRenderer;
        [SerializeField] SpriteRenderer nextRenderer;
        [SerializeField] Sprite[] stageSprites;
        [SerializeField, Min(0.2f)] float transitionDuration = 1f;

        int currentStage = -1;
        Coroutine transitionRoutine;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            SetStage(0, true);
        }

        void OnEnable()
        {
            if (currentStage < 0) SetStage(0, true);
        }

        public void SetStage(int stage, bool immediate = false)
        {
            if (stageSprites == null || stageSprites.Length == 0 ||
                currentRenderer == null || nextRenderer == null)
                return;

            int clamped = Mathf.Clamp(stage, 0, stageSprites.Length - 1);
            if (clamped == currentStage && !immediate) return;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (immediate || currentStage < 0)
            {
                currentStage = clamped;
                currentRenderer.sprite = stageSprites[clamped];
                currentRenderer.color = Color.white;
                nextRenderer.color = Color.clear;
                FitToCamera(currentRenderer);
                return;
            }

            transitionRoutine = StartCoroutine(TransitionTo(clamped));
        }

        IEnumerator TransitionTo(int stage)
        {
            nextRenderer.sprite = stageSprites[stage];
            nextRenderer.sortingOrder = -9;
            currentRenderer.sortingOrder = -10;
            nextRenderer.color = new Color(1f, 1f, 1f, 0f);
            FitToCamera(currentRenderer);
            FitToCamera(nextRenderer);

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                t = t * t * (3f - 2f * t);
                currentRenderer.color = new Color(1f, 1f, 1f, 1f - t);
                nextRenderer.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }

            currentRenderer.color = Color.clear;
            nextRenderer.color = Color.white;
            (currentRenderer, nextRenderer) = (nextRenderer, currentRenderer);
            currentRenderer.sortingOrder = -10;
            nextRenderer.sortingOrder = -9;
            currentStage = stage;
            transitionRoutine = null;
        }

        void FitToCamera(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null || worldCamera == null) return;
            float cameraHeight = worldCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * worldCamera.aspect;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scale = Mathf.Max(cameraWidth / Mathf.Max(0.01f, spriteSize.x),
                cameraHeight / Mathf.Max(0.01f, spriteSize.y));
            renderer.transform.localScale = Vector3.one * scale;
        }
    }
}
