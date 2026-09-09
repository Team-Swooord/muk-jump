using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 기존 붓 원화 연출 뒤에 같은 붓질을 이어 빈 곳을 채우고 다음 화면을 드러낸다.
    public sealed class BrushTransitionView : MonoBehaviour
    {
        const float OriginalCoverDuration = 1.15f;
        const float CoverDuration = 1.85f;
        const float BlackHoldDuration = .22f;
        const float RevealDuration = 0.7f;
        const float ReducedFadeDuration = 0.12f;

        CanvasGroup group;
        Image wash;
        RectTransform[] strokeMasks;
        RectTransform[] strokeImages;
        float[] strokeHeights;
        readonly System.Random strokeRandom = new System.Random();
        bool playing;
        bool coveredCallbackStarted;
        Action activeFailureCallback;
        static bool revealAfterSceneLoad;

#if UNITY_EDITOR
        Action buildForTests;
        Func<bool> reducedMotionForTests;
        Action feedbackForTests;
        Action<IEnumerator> startCoroutineForTests;
        Func<float> frameDeltaForTests;
        Func<bool> applicationActiveForTests;
#endif

        public bool IsPlaying => playing;

        void Awake()
        {
            if (!revealAfterSceneLoad) return;

            revealAfterSceneLoad = false;
            playing = true;
            try
            {
                BuildForTransition();
                StartTransitionCoroutine(
                    RunGuarded(RevealLoadedSceneRoutine(), null));
            }
            catch (Exception exception)
            {
                FailBeforeCovered(null, exception);
            }
        }

        public static void RequestRevealAfterSceneLoad() => revealAfterSceneLoad = true;

        public void Play(Action onCovered, Action onFailed = null)
        {
            TryPlay(onCovered, onFailed);
        }

        /// 이미 다른 전환이 화면을 소유 중이면 새 코루틴을 시작하지 않는다.
        /// 호출자는 반환값으로 입력 잠금이나 목적 화면 예약을 되돌릴 수 있다.
        public bool TryPlay(Action onCovered, Action onFailed = null)
        {
            if (playing) return false;
            playing = true;
            coveredCallbackStarted = false;
            activeFailureCallback = onFailed;
            try
            {
                BuildForTransition();
                bool reducedMotion = ResolveReducedMotion();
                if (!reducedMotion)
                    PlayTransitionFeedback();
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = false;
                wash.canvasRenderer.SetAlpha(0f);
                SetStrokesVisible(!reducedMotion);
                StartTransitionCoroutine(
                    RunGuarded(
                        PlayRoutine(reducedMotion, onCovered, onFailed),
                        onFailed));
                return playing;
            }
            catch (Exception exception)
            {
                FailBeforeCovered(onFailed, exception);
                return false;
            }
        }

        void OnDisable()
        {
            bool notifyCancellation = playing && !coveredCallbackStarted;
            Action cancellationCallback = activeFailureCallback;
            StopAllCoroutines();
            ResetOverlayState();
            if (notifyCancellation)
                TryInvokeFailure(cancellationCallback);
        }

        void BuildIfNeeded()
        {
            if (group != null) return;

            var root = new GameObject("BrushTransitionCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var scaler = root.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var inputBlocker = CreateImage(
                "InputBlocker",
                root.transform,
                null,
                Vector2.zero,
                new Vector2(2000f, 3000f),
                0f);
            inputBlocker.color = Color.clear;
            inputBlocker.raycastTarget = true;
            StretchToScreen(inputBlocker.rectTransform);

            wash = CreateImage("InkWash", root.transform, null, Vector2.zero,
                new Vector2(1500f, 2400f), 0f);
            wash.color = InkPalette.Ink;
            StretchToScreen(wash.rectTransform);
            wash.canvasRenderer.SetAlpha(0f);

            var textures = LoadBrushTextures();
            var positions = new[]
            {
                new Vector2(-250f, 720f), new Vector2(250f, 650f),
                new Vector2(-400f, 0f), new Vector2(-140f, -50f),
                new Vector2(130f, -120f), new Vector2(400f, -200f),
                new Vector2(-245f, -720f), new Vector2(270f, -790f),
            };
            var sizes = new[]
            {
                new Vector2(1650f, 538f), new Vector2(1750f, 492f),
                new Vector2(1100f, 1960f), new Vector2(1100f, 1980f),
                new Vector2(850f, 2300f), new Vector2(900f, 2700f),
                new Vector2(1500f, 800f), new Vector2(1600f, 630f),
            };
            const int extraPasses = 6;
            strokeMasks = new RectTransform[textures.Length + extraPasses];
            strokeImages = new RectTransform[strokeMasks.Length];
            strokeHeights = new float[strokeMasks.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                strokeMasks[i] = CreateMaskedStroke($"Brush_{i + 1:00}", root.transform,
                    textures[i], positions[i], sizes[i], out strokeImages[i]);
                strokeHeights[i] = sizes[i].y;
            }
            // 기존 8획은 유지하고 추가 획만 여러 방향으로 긋는다.
            // 기준 화면을 넘어서는 여백까지 겹쳐 칠하되 별도 기하학 먹 메쉬를 합성하지 않는다.
            float width = Mathf.Max(1080f, Screen.width * 1920f / Mathf.Max(1f, Screen.height));
            for (int pass = 0; pass < extraPasses; pass++)
            {
                int i = textures.Length + pass;
                float x = Mathf.Lerp(-width * .5f, width * .5f, pass / (float)(extraPasses - 1));
                Vector2 size = new(width * .90f, 3200f);
                strokeMasks[i] = CreateMaskedStroke($"Brush_Extra_{pass + 1:00}", root.transform,
                    textures[2 + pass % 4], new Vector2(x, pass % 2 == 0 ? 80f : -80f), size, out strokeImages[i], true);
                strokeHeights[i] = size.y;
            }
            // 마지막 원화 붓질과 함께 기존의 짧은 먹색 워시를 이어 빈 픽셀만 마무리한다.
            wash.transform.SetAsLastSibling();
        }

        IEnumerator PlayRoutine(
            bool reducedMotion,
            Action onCovered,
            Action onFailed)
        {
            if (reducedMotion)
            {
                for (int i = 0; i < strokeMasks.Length; i++)
                    SetStrokeProgress(i, 0f);
                wash.canvasRenderer.SetAlpha(1f);
                IEnumerator fadeIn = FadeGroupAlpha(
                    0f,
                    1f,
                    ReducedFadeDuration);
                while (fadeIn.MoveNext())
                    yield return fadeIn.Current;
                IEnumerator hold = HoldBlackout(BlackHoldDuration);
                while (hold.MoveNext())
                    yield return hold.Current;
                if (!TryInvokeCovered(onCovered, onFailed))
                    yield break;
                yield return null;
                IEnumerator fadeOut = FadeGroupAlpha(
                    1f,
                    0f,
                    ReducedFadeDuration);
                while (fadeOut.MoveNext())
                    yield return fadeOut.Current;
                ResetOverlayState();
                yield break;
            }

            RandomizeFinishingStrokes();
            float elapsed = 0f;
            while (elapsed < CoverDuration)
            {
                elapsed += ResolveFrameDelta();
                float t = Mathf.Clamp01(elapsed / CoverDuration);
                for (int i = 0; i < strokeMasks.Length; i++)
                {
                    SetStrokeProgress(i, StrokeProgressAtSeconds(i, elapsed));
                }
                float washProgress = Smooth01((t - .86f) / .14f);
                wash.canvasRenderer.SetAlpha(washProgress * washProgress);
                yield return null;
            }

            IEnumerator blackHold = HoldBlackout(BlackHoldDuration);
            while (blackHold.MoveNext())
                yield return blackHold.Current;
            if (!TryInvokeCovered(onCovered, onFailed))
                yield break;
            // 목적 화면의 첫 Canvas 배치도 검정 아래에서 끝낸다.
            // 씬 재로드라면 새 씬의 RevealLoadedSceneRoutine이 여기서 이어받는다.
            yield return null;

            elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += ResolveFrameDelta();
                float t = Mathf.Clamp01(elapsed / RevealDuration);
                group.alpha = 1f - Smooth01(t);
                yield return null;
            }

            ResetOverlayState();
        }

        IEnumerator RevealLoadedSceneRoutine()
        {
            playing = true;
            group.blocksRaycasts = true;
            // 짧은 대기는 이전 씬에서 끝냈다. 재로드 뒤에는 첫 렌더만 먹색으로
            // 보호하고 드러내어 중복 대기나 한 프레임 배경 노출을 막는다.
            IEnumerator ready = HoldBlackout(.08f);
            while (ready.MoveNext())
                yield return ready.Current;

            float elapsed = 0f;
            float duration = ResolveReducedMotion() ? ReducedFadeDuration : RevealDuration;
            while (elapsed < duration)
            {
                elapsed += ResolveFrameDelta();
                float t = Smooth01(elapsed / duration);
                group.alpha = 1f - t;
                yield return null;
            }

            ResetOverlayState();
        }

        IEnumerator HoldBlackout(float duration)
        {
            group.alpha = 1f;
            wash.canvasRenderer.SetAlpha(1f);
            // 가려진 붓 원화는 유지 시간·드러남 동안 렌더하지 않고 다음 전환에 재사용한다.
            SetStrokesVisible(false);
            yield return null;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += ResolveFrameDelta();
                yield return null;
            }
            while (!IsApplicationActive()) yield return null;
        }

        bool IsApplicationActive()
        {
#if UNITY_EDITOR
            if (applicationActiveForTests != null) return applicationActiveForTests();
#endif
            return MobileApplicationLifecycle.IsApplicationActive;
        }

        float ResolveFrameDelta()
        {
            if (!IsApplicationActive()) return 0f;
            float delta = Time.unscaledDeltaTime;
#if UNITY_EDITOR
            if (frameDeltaForTests != null) delta = frameDeltaForTests();
#endif
            // 일시정지 중에도 진행하지만 앱을 떠난 시간·복귀 프레임 지연은 건너뛰지 않는다.
            return float.IsFinite(delta) ? Mathf.Clamp(delta, 0f, .05f) : 0f;
        }

        void SetStrokesVisible(bool visible)
        {
            if (strokeMasks == null) return;
            for (int i = 0; i < strokeMasks.Length; i++)
                if (strokeMasks[i] != null) strokeMasks[i].gameObject.SetActive(visible);
        }

        static void StretchToScreen(RectTransform rect)
        {
            // Safe Area가 아닌 실제 Canvas 전체. 노치·가로 WebGL 가장자리도 포함한다.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        IEnumerator RunGuarded(IEnumerator routine, Action onFailed)
        {
            while (true)
            {
                bool hasNext;
                object current;
                try
                {
                    hasNext = routine.MoveNext();
                    current = hasNext ? routine.Current : null;
                }
                catch (Exception exception)
                {
                    if (playing && !coveredCallbackStarted)
                        FailBeforeCovered(onFailed, exception);
                    else
                    {
                        ResetOverlayState();
                        Debug.LogException(exception, this);
                    }
                    yield break;
                }

                if (!hasNext)
                    yield break;
                yield return current;
            }
        }

        void BuildForTransition()
        {
#if UNITY_EDITOR
            buildForTests?.Invoke();
#endif
            BuildIfNeeded();
        }

        bool ResolveReducedMotion()
        {
#if UNITY_EDITOR
            if (reducedMotionForTests != null)
                return reducedMotionForTests.Invoke();
#endif
            return LobbySettingsProfile.ReducedMotionEnabled;
        }

        void PlayTransitionFeedback()
        {
#if UNITY_EDITOR
            if (feedbackForTests != null)
            {
                feedbackForTests.Invoke();
                return;
            }
#endif
            GameFeedbackController.Instance?.PlayBrushTransition();
        }

        void StartTransitionCoroutine(IEnumerator routine)
        {
#if UNITY_EDITOR
            if (startCoroutineForTests != null)
            {
                startCoroutineForTests.Invoke(routine);
                return;
            }
#endif
            StartCoroutine(routine);
        }

        void FailBeforeCovered(Action onFailed, Exception exception)
        {
            bool shouldNotify = playing && !coveredCallbackStarted;
            ResetOverlayState();
            if (shouldNotify)
                TryInvokeFailure(onFailed);
            Debug.LogException(exception, this);
        }

        IEnumerator FadeGroupAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += ResolveFrameDelta();
                group.alpha = Mathf.Lerp(
                    from,
                    to,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        bool TryInvokeCovered(Action onCovered, Action onFailed)
        {
            coveredCallbackStarted = true;
            try
            {
                onCovered?.Invoke();
                activeFailureCallback = null;
                return true;
            }
            catch (Exception exception)
            {
                ResetOverlayState();
                TryInvokeFailure(onFailed);
                Debug.LogException(exception, this);
                return false;
            }
        }

        void TryInvokeFailure(Action onFailed)
        {
            try
            {
                onFailed?.Invoke();
            }
            catch (Exception recoveryException)
            {
                Debug.LogException(recoveryException, this);
            }
        }

        void ResetOverlayState()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (wash != null) wash.canvasRenderer.SetAlpha(0f);
            if (strokeMasks != null)
            {
                for (int i = 0; i < strokeMasks.Length; i++)
                    if (strokeMasks[i] != null)
                        SetStrokeProgress(i, 0f);
            }
            playing = false;
            coveredCallbackStarted = false;
            activeFailureCallback = null;
        }

        void SetStrokeProgress(int index, float progress)
        {
            Vector2 size = strokeMasks[index].sizeDelta;
            size.y = strokeHeights[index] * progress;
            strokeMasks[index].sizeDelta = size;
            // 실제 붓털이 마스크 선두를 조금 뒤따라오는 듯한 짧은 끌림.
            strokeImages[index].anchoredPosition = new Vector2(0f, Mathf.Lerp(46f, 0f, progress));
        }

        void RandomizeFinishingStrokes()
        {
            // 게임 난수와 분리한다. 방향군을 섞어 같은 세로 획만 연속되지 않게 한다.
            float[] angles = { -65f, 65f, 90f, -90f, 155f, -25f };
            for (int i = angles.Length - 1; i > 0; i--)
            {
                int j = strokeRandom.Next(i + 1);
                (angles[i], angles[j]) = (angles[j], angles[i]);
            }
            float width = Mathf.Max(1080f, Screen.width * 1920f / Mathf.Max(1f, Screen.height));
            float diagonal = Mathf.Sqrt(width * width + 1920f * 1920f);
            for (int pass = 0; pass < angles.Length && pass + 8 < strokeMasks.Length; pass++)
            {
                int i = pass + 8;
                float angle = angles[pass] + (float)(strokeRandom.NextDouble() * 16 - 8);
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                Vector2 center = new Vector2((float)(strokeRandom.NextDouble() - .5) * width * .3f,
                    (float)(strokeRandom.NextDouble() - .5) * 400f);
                float length = diagonal * 1.5f;
                strokeMasks[i].localRotation = rotation;
                strokeMasks[i].anchoredPosition = center + (Vector2)(rotation * (Vector3.up * length * .5f));
                strokeMasks[i].sizeDelta = new Vector2(diagonal * .85f, 0f);
                strokeImages[i].sizeDelta = new Vector2(diagonal * .85f, length);
                strokeHeights[i] = length;
            }
        }

        static Texture2D[] LoadBrushTextures()
        {
            var textures = new Texture2D[8];
            for (int i = 0; i < textures.Length; i++)
            {
                string number = (i + 1).ToString("00");
                string suffix = i switch
                {
                    0 => "top_left", 1 => "top_right", 2 => "left_vertical",
                    3 => "center_left_vertical", 4 => "center_right_vertical",
                    5 => "right_vertical", 6 => "bottom_left", _ => "bottom_right",
                };
                textures[i] = Resources.Load<Texture2D>($"MukJump/BrushTransitions/brush_stroke_{number}_{suffix}");
                if (textures[i] == null)
                    textures[i] = InkUiTextureFactory.CreateBrushSprite().texture;
            }
            return textures;
        }

        static RectTransform CreateMaskedStroke(string objectName, Transform parent, Texture texture,
            Vector2 centerPosition, Vector2 fullSize, out RectTransform imageRect, bool rotatedMask = false)
        {
            var maskObject = new GameObject(objectName, typeof(RectTransform));
            if (rotatedMask)
            {
                // RectMask2D는 축 정렬 클립이므로 대각선 붓질은 스텐실 마스크를 쓴다.
                maskObject.AddComponent<Image>().raycastTarget = false;
                maskObject.AddComponent<Mask>().showMaskGraphic = false;
            }
            else maskObject.AddComponent<RectMask2D>();
            var mask = maskObject.GetComponent<RectTransform>();
            mask.SetParent(parent, false);
            mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f);
            mask.pivot = new Vector2(0.5f, 1f);
            mask.anchoredPosition = centerPosition + Vector2.up * (fullSize.y * 0.5f);
            mask.sizeDelta = new Vector2(fullSize.x, 0f);

            var imageObject = new GameObject("InkStroke", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(mask, false);
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 1f);
            imageRect.pivot = new Vector2(0.5f, 1f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = fullSize;
            var rawImage = imageObject.GetComponent<RawImage>();
            rawImage.texture = texture;
            // 원본 RGB에 예기치 않은 색이 포함돼도 전환은 항상 먹색으로만 보인다.
            rawImage.color = InkPalette.Ink;
            rawImage.raycastTarget = false;
            return mask;
        }

        public static float StrokeProgressAtSeconds(int index, float seconds)
        {
            float amount;
            if (index < 8)
            {
                float start = index switch
                {
                    0 => 0f, 1 => .10f, 2 => .26f, 3 => .31f,
                    4 => .38f, 5 => .45f, 6 => .64f, _ => .72f,
                };
                float duration = index < 2 ? .23f : index < 6 ? .26f : .22f;
                amount = (seconds / OriginalCoverDuration - start) / duration;
            }
            else amount = (seconds - (1.02f + (index - 8) * .10f)) / .33f;
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(amount), 3f);
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position,
            Vector2 size, float rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = InkPalette.Ink;
            image.raycastTarget = false;
            return image;
        }
    }

    /// UI 전환과 팝업이 공유하는 저비용 절차적 먹 마스크. 최초 한 번만 생성한다.
    public static class InkUiTextureFactory
    {
        static Sprite brushSprite;
        static Sprite blobSprite;
        static Sprite inkDropSprite;
        static Sprite celestialDiscSprite;
        static Sprite hanjiMoonSprite;
        static Sprite growthPaperRibbonSprite;
        static Texture2D brushTexture;
        static Texture2D blobTexture;
        static Texture2D inkDropTexture;
        static Texture2D celestialDiscTexture;
        static Texture2D hanjiMoonTexture;
        static Texture2D growthPaperRibbonTexture;
        static readonly Sprite[] growthNavigationSprites = new Sprite[3];
        static readonly Texture2D[] growthNavigationTextures = new Texture2D[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReleaseRuntimeAssets()
        {
            for (int i = 0; i < growthNavigationSprites.Length; i++)
            {
                DestroyRuntimeObject(growthNavigationSprites[i]);
                DestroyRuntimeObject(growthNavigationTextures[i]);
                growthNavigationSprites[i] = null;
                growthNavigationTextures[i] = null;
            }
            DestroyRuntimeObject(brushSprite);
            DestroyRuntimeObject(blobSprite);
            DestroyRuntimeObject(inkDropSprite);
            DestroyRuntimeObject(celestialDiscSprite);
            DestroyRuntimeObject(hanjiMoonSprite);
            DestroyRuntimeObject(growthPaperRibbonSprite);
            DestroyRuntimeObject(brushTexture);
            DestroyRuntimeObject(blobTexture);
            DestroyRuntimeObject(inkDropTexture);
            DestroyRuntimeObject(celestialDiscTexture);
            DestroyRuntimeObject(hanjiMoonTexture);
            DestroyRuntimeObject(growthPaperRibbonTexture);
            brushSprite = null;
            blobSprite = null;
            inkDropSprite = null;
            celestialDiscSprite = null;
            hanjiMoonSprite = null;
            growthPaperRibbonSprite = null;
            brushTexture = null;
            blobTexture = null;
            inkDropTexture = null;
            celestialDiscTexture = null;
            hanjiMoonTexture = null;
            growthPaperRibbonTexture = null;
        }

        public static Sprite CreateBrushSprite()
        {
            if (brushSprite != null) return brushSprite;
            const int width = 512;
            const int height = 160;
            var texture = NewTexture(width, height, "MukJump_BrushMask");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = Mathf.Abs(y / (float)(height - 1) - 0.5f) * 2f;
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Min(u / 0.08f, (1f - u) / 0.11f));
                float edge = 0.88f - v + (Mathf.PerlinNoise(u * 18f, y * 0.075f) - 0.5f) * 0.24f;
                float fibers = Mathf.PerlinNoise(u * 7f, y * 0.31f);
                float alpha = edge > 0f ? taper * Mathf.Clamp01(edge * 5f) : 0f;
                if (fibers < 0.13f && v > 0.35f) alpha *= 0.2f;
                pixels[y * width + x] =
                    new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            brushTexture = texture;
            brushSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            brushSprite.name = "MukJump_BrushMask";
            return brushSprite;
        }

        public static Sprite CreateGrowthBackIconSprite() => CreateGrowthNavigationIcon(0);
        public static Sprite CreateGrowthResetIconSprite() => CreateGrowthNavigationIcon(1);
        public static Sprite CreateGrowthConfirmIconSprite() => CreateGrowthNavigationIcon(2);

        // 배경판 없이 같은 먹선 굵기로 그린 뒤로·되돌림·확인 아이콘. 생성은 종류별 한 번뿐이다.
        static Sprite CreateGrowthNavigationIcon(int kind)
        {
            if (growthNavigationSprites[kind] != null) return growthNavigationSprites[kind];
            Vector2[][] paths;
            if (kind == 0)
            {
                paths = new[] {
                    new[] { new Vector2(.68f, .82f), new Vector2(.31f, .50f), new Vector2(.68f, .18f) }
                };
            }
            else if (kind == 1)
            {
                var arc = new Vector2[33];
                for (int i = 0; i < arc.Length; i++)
                {
                    float angle = Mathf.Lerp(-140f, 150f, i / 32f) * Mathf.Deg2Rad;
                    arc[i] = new Vector2(.5f, .5f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .31f;
                }
                Vector2 tip = arc[32];
                paths = new[] { arc, new[] { tip + new Vector2(-.02f, .18f), tip, tip + new Vector2(.18f, -.025f) } };
            }
            else
                paths = new[] { new[] { new Vector2(.20f, .49f), new Vector2(.42f, .28f), new Vector2(.80f, .75f) } };

            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new((x + .5f) / size, (y + .5f) / size);
                float distance = 1f;
                foreach (Vector2[] path in paths)
                for (int i = 1; i < path.Length; i++)
                {
                    Vector2 ab = path[i] - path[i - 1];
                    float t = Mathf.Clamp01(Vector2.Dot(p - path[i - 1], ab) / ab.sqrMagnitude);
                    distance = Mathf.Min(distance, Vector2.Distance(p, path[i - 1] + ab * t));
                }
                float grain = Mathf.PerlinNoise(x * .36f + 4f, y * .36f + 7f);
                float radius = .032f + (grain - .5f) * .009f;
                float alpha = Mathf.Clamp01((radius - distance) * size + .5f) * Mathf.Lerp(.88f, 1f, grain);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            Texture2D texture = NewTexture(size, size, "MukJump_GrowthNavigation_" + kind);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            sprite.name = kind == 0 ? "GrowthBackIcon" : kind == 1 ? "GrowthResetIcon" : "GrowthConfirmIcon";
            growthNavigationTextures[kind] = texture;
            growthNavigationSprites[kind] = sprite;
            return sprite;
        }

        public static Sprite CreateBlobSprite()
        {
            if (blobSprite != null) return blobSprite;
            const int size = 128;
            var texture = NewTexture(size, size, "MukJump_InkBlobMask");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1) - 0.5f) * 2f;
                float ny = (y / (float)(size - 1) - 0.5f) * 2f;
                float angle = Mathf.Atan2(ny, nx);
                float noise = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.8f + 2.3f,
                    Mathf.Sin(angle) * 1.8f + 3.7f);
                float radius = 0.83f + (noise - 0.5f) * 0.22f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01((radius - distance) * 35f);
                pixels[y * size + x] =
                    new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            blobTexture = texture;
            blobSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            blobSprite.name = "MukJump_InkBlobMask";
            return blobSprite;
        }

        /// 기존 로비 해의 단순한 원을 유지한다. 달의 한지 질감은 별도로 만든다.
        public static Sprite CreateCelestialDiscSprite()
        {
            if (celestialDiscSprite != null) return celestialDiscSprite;
            const int size = 128;
            var texture = NewTexture(size, size, "MukJump_CelestialDisc");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float fiber = Mathf.PerlinNoise(x * 0.13f, y * 0.13f);
                float edge = 0.965f + (fiber - 0.5f) * 0.009f;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01((edge - radius) * 80f) * 255f);
                byte pigment = (byte)Mathf.RoundToInt(Mathf.Lerp(0.96f, 1f, fiber) * 255f);
                pixels[y * size + x] = new Color32(pigment, pigment, pigment, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            celestialDiscTexture = texture;
            celestialDiscSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            celestialDiscSprite.name = "MukJump_CelestialDisc";
            return celestialDiscSprite;
        }

        /// 원의 크기는 해와 같고, 넓고 옅은 한지 농담과 완만한 가장자리만 남긴다.
        /// 한 번 만든 128px 원화를 공유하며 프레임마다 노이즈를 갱신하지 않는다.
        public static Sprite CreateHanjiMoonSprite()
        {
            if (hanjiMoonSprite != null) return hanjiMoonSprite;
            const int size = 128;
            var texture = NewTexture(size, size, "MukJump_HanjiMoon");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = SampleHanjiMoonPixel(x, y, size);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            hanjiMoonTexture = texture;
            hanjiMoonSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f);
            hanjiMoonSprite.name = "MukJump_HanjiMoon";
            return hanjiMoonSprite;
        }

        // 실제 표시 크기에서 모래처럼 보이는 픽셀 단위 노이즈는 쓰지 않는다.
        // 명암은 넓은 얼룩 위주로 7% 이내만 달라지며, 테두리도 작은 굴곡만 갖는다.
        public static Color32 SampleHanjiMoonPixel(int x, int y, int size = 128)
        {
            float nx = (x + .5f) / size * 2f - 1f;
            float ny = (y + .5f) / size * 2f - 1f;
            float distance = Mathf.Sqrt(nx * nx + ny * ny);
            float angle = Mathf.Atan2(ny, nx);
            float edge = .962f + Mathf.Sin(angle * 3f + .4f) * .010f
                + Mathf.Sin(angle * 7f - 1.3f) * .006f
                + Mathf.Sin(angle * 11f + .2f) * .0025f;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - distance) / .024f));
            float wash = .5f + .5f * Mathf.Sin(nx * 4.2f + Mathf.Sin(ny * 3.1f + .8f) * .7f + .4f)
                * Mathf.Sin(ny * 4.7f + nx * 1.6f - .5f);
            float softFiber = .5f + .5f * Mathf.Sin(nx * 15.2f + ny * 5.1f + Mathf.Sin(ny * 6.4f))
                * Mathf.Sin(ny * 11.7f - nx * 2.6f);
            byte pigment = (byte)Mathf.RoundToInt((.925f + wash * .058f + softFiber * .009f) * 255f);
            return new Color32(pigment, pigment, pigment, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        /// 원형 아이콘 받침처럼 흰 바탕의 가장자리를 불규칙한 붓끝으로 풀어 준다.
        /// 테두리는 실제 원형 갈필 원화를 따로 펼쳐 사용한다.
        public static Sprite CreateGrowthPaperRibbonSprite()
        {
            if (growthPaperRibbonSprite != null) return growthPaperRibbonSprite;
            const int width = 240, height = 386;
            var texture = NewTexture(width, height, "MukJump_GrowthPaperRibbon");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = SampleGrowthPaperRibbonPixel(x, y, width, height);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            growthPaperRibbonTexture = texture;
            growthPaperRibbonSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            growthPaperRibbonSprite.name = "MukJump_GrowthPaperRibbon";
            return growthPaperRibbonSprite;
        }

        // 순수 픽셀 계산을 분리해 에디터를 조작하지 않고도 같은 원화를 검증할 수 있게 한다.
        internal static Color32 SampleGrowthPaperRibbonPixel(int x, int y, int width, int height)
        {
            float nx = x / (float)(width - 1) * 2f - 1f;
            float ny = y / (float)(height - 1) * 2f - 1f;
            float angle = System.MathF.Atan2(ny, nx);
            const float corner = 24f;
            const float margin = 15f;
            float qx = System.MathF.Abs(nx * width * .5f) - (width * .5f - margin - corner);
            float qy = System.MathF.Abs(ny * height * .5f) - (height * .5f - margin - corner);
            float dx = System.MathF.Max(qx, 0f), dy = System.MathF.Max(qy, 0f);
            float distance = System.MathF.Sqrt(dx * dx + dy * dy) +
                System.MathF.Min(System.MathF.Max(qx, qy), 0f) - corner;
            // 자잘한 톱니 대신 길이·높이가 다른 넓은 붓끝을 잇는다.
            // 9-slice로 늘리지 않아 긴 변에도 뾰족한 돌출과 파임이 그대로 남는다.
            const int tips = 23;
            float phase = (angle + System.MathF.PI) / (System.MathF.PI * 2f) * tips;
            int tip = (int)System.MathF.Floor(phase);
            float first = GrowthPaperTipHeight(tip % tips);
            float next = GrowthPaperTipHeight((tip + 1) % tips);
            float brushTip = first + (next - first) * (phase - tip);
            float contour = System.MathF.Sin(angle * 3f + .8f) * 3f +
                System.MathF.Sin(angle * 7f - .6f) * 2f + brushTip * 7f;
            float grainSeed = System.MathF.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            float fiber = grainSeed - System.MathF.Floor(grainSeed);
            float alpha = GrowthRibbonSmooth((.8f - distance + contour + (fiber - .5f) * .45f) / 1.6f);
            float paper = .979f + fiber * .011f +
                System.MathF.Sin(x * .031f + System.MathF.Sin(y * .047f)) * .006f;
            byte pigment = (byte)System.MathF.Round(paper * 255f);
            return new Color32(pigment, pigment, pigment, (byte)System.MathF.Round(alpha * 255f));
        }

        static float GrowthPaperTipHeight(int tip)
        {
            float seed = System.MathF.Sin(tip * 12.9898f + 4.37f) * 43758.5453f;
            return (seed - System.MathF.Floor(seed)) * 2f - 1f;
        }

        static float GrowthRibbonSmooth(float value)
        {
            float t = System.MathF.Max(0f, System.MathF.Min(1f, value));
            return t * t * (3f - 2f * t);
        }

        public static Sprite CreateInkDropSprite()
        {
            if (inkDropSprite != null) return inkDropSprite;
            const int size = 128;
            var texture = NewTexture(size, size, "MukJump_InkDropMask");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1) - 0.5f) * 2f;
                float ny = (y / (float)(size - 1) - 0.5f) * 2f;
                float circleDistance =
                    new Vector2(nx, ny + 0.28f).magnitude - 0.62f;
                float upperProgress = Mathf.InverseLerp(-0.18f, 0.94f, ny);
                float upperHalfWidth = Mathf.Lerp(0.58f, 0f, upperProgress);
                float triangleDistance = Mathf.Max(
                    Mathf.Abs(nx) - upperHalfWidth,
                    Mathf.Max(-ny - 0.18f, ny - 0.94f));
                float signedDistance = Mathf.Min(circleDistance, triangleDistance);
                float alpha = Mathf.Clamp01(-signedDistance * 28f + 0.5f);
                pixels[y * size + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            inkDropTexture = texture;
            inkDropSprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            inkDropSprite.name = "MukJump_InkDropMask";
            return inkDropSprite;
        }

        static Texture2D NewTexture(int width, int height, string textureName)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
