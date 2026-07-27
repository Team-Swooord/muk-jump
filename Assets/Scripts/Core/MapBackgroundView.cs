using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] Sprite[] endlessStageSprites;
        [SerializeField, Min(0.2f)] float transitionDuration = 1f;

        const string EndlessResourcePath = "MukJump/Background/Endless";

        Sprite[] resolvedStageSprites;
        int resolvedBaseStageCount;
        int resolvedEndlessStageCount;
        int currentStage = -1;
        bool currentMirrored;
        Coroutine transitionRoutine;
        int transitionTargetStage = -1;
        bool transitionTargetMirrored;

        public int BaseStageCount
        {
            get
            {
                ResolveStageSprites();
                return resolvedBaseStageCount;
            }
        }

        public int EndlessStageCount
        {
            get
            {
                ResolveStageSprites();
                return resolvedEndlessStageCount;
            }
        }

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            ResolveStageSprites();
            SetStage(0, true);
        }

        void OnEnable()
        {
            if (currentStage < 0) SetStage(0, true);
        }

        public void SetStage(int stage, bool immediate = false, bool mirrorX = false)
        {
            ResolveStageSprites();
            if (resolvedStageSprites.Length == 0 ||
                currentRenderer == null || nextRenderer == null)
                return;

            int clamped = Mathf.Clamp(stage, 0, resolvedStageSprites.Length - 1);
            bool resolvedMirror = clamped >= resolvedBaseStageCount && mirrorX;
            if (transitionRoutine == null &&
                clamped == currentStage &&
                resolvedMirror == currentMirrored &&
                !immediate)
                return;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (immediate || currentStage < 0)
            {
                currentStage = clamped;
                currentMirrored = resolvedMirror;
                currentRenderer.sprite = resolvedStageSprites[clamped];
                currentRenderer.flipX = resolvedMirror;
                currentRenderer.color = Color.white;
                nextRenderer.color = Color.clear;
                nextRenderer.flipX = false;
                FitToCamera(currentRenderer);
                return;
            }

            transitionTargetStage = clamped;
            transitionTargetMirrored = resolvedMirror;
            transitionRoutine = StartCoroutine(TransitionTo(clamped, resolvedMirror));
        }

        IEnumerator TransitionTo(int stage, bool mirrorX)
        {
            nextRenderer.sprite = resolvedStageSprites[stage];
            nextRenderer.flipX = mirrorX;
            nextRenderer.sortingOrder = -9;
            currentRenderer.sortingOrder = -10;
            currentRenderer.color = Color.white;
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
            currentMirrored = mirrorX;
            transitionRoutine = null;
            transitionTargetStage = -1;
        }

        void OnDisable()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (transitionTargetStage >= 0 && resolvedStageSprites != null &&
                transitionTargetStage < resolvedStageSprites.Length &&
                currentRenderer != null)
            {
                currentStage = transitionTargetStage;
                currentMirrored = transitionTargetMirrored;
                currentRenderer.sprite = resolvedStageSprites[currentStage];
                currentRenderer.flipX = currentMirrored;
                FitToCamera(currentRenderer);
            }

            if (currentRenderer != null)
            {
                currentRenderer.color = Color.white;
                currentRenderer.sortingOrder = -10;
            }
            if (nextRenderer != null)
            {
                nextRenderer.color = Color.clear;
                nextRenderer.flipX = false;
                nextRenderer.sortingOrder = -9;
            }
            transitionTargetStage = -1;
        }

        void ResolveStageSprites()
        {
            if (resolvedStageSprites != null) return;

            var baseSprites = new List<Sprite>();
            AddValidSprites(baseSprites, stageSprites);

            var endlessSprites = new List<Sprite>();
            AddValidSprites(endlessSprites, endlessStageSprites);
            var loaded = Resources.LoadAll<Sprite>(EndlessResourcePath);
            for (int i = 0; i < loaded.Length; i++)
            {
                Sprite sprite = loaded[i];
                if (sprite != null && !endlessSprites.Contains(sprite))
                    endlessSprites.Add(sprite);
            }
            endlessSprites.Sort((left, right) =>
                string.CompareOrdinal(left.name, right.name));

            resolvedBaseStageCount = baseSprites.Count;
            resolvedEndlessStageCount = endlessSprites.Count;
            baseSprites.AddRange(endlessSprites);
            resolvedStageSprites = baseSprites.ToArray();
        }

        static void AddValidSprites(List<Sprite> target, Sprite[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null)
                    target.Add(source[i]);
        }

        void OnValidate()
        {
            resolvedStageSprites = null;
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
