using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 제작사 워드마크에서 별도 로딩 화면 없이 메인 로비로 연결한다.
    /// 씬 로딩만 기다리며 광고·원격 로그인 성공을 로딩 완료 조건으로 삼지 않는다.
    public sealed class StartupBrandSplash : MonoBehaviour
    {
        public const string SceneName = "Splash";
        public const string NextSceneName = "Main";
        public const float InitialDelay = 0.1f;
        public const float FadeInDuration = 0.35f;
        public const float HoldDuration = 0.8f;
        public const float FadeOutDuration = 0.3f;

        CanvasGroup rootGroup;
        CanvasGroup logoGroup;
        Image logoImage;
        Coroutine playback;
        Text loadFailureText;
        Text retryText;
        AsyncOperation mainLoad;
        bool waitingForRetry;

        public static bool IsBlockingInput { get; private set; }

#if UNITY_EDITOR
        static System.Action<string> restartSceneForTests;
#endif

        /// 서버·기기 삭제 성공 뒤에만 호출한다. 기존 Main의 자동 진입을 닫고
        /// 제작사 씬을 다시 거쳐 새 Main의 첫 안내를 준비한다.
        internal static bool TryRestartAfterAccountDeletion()
        {
            // 시작 화면 아래에서 중단된 탈퇴를 복구했다면 현재 Splash가 이어 맡는다.
            if (IsBlockingInput) return true;
#if UNITY_EDITOR
            if (!Application.isPlaying && restartSceneForTests == null) return true;
#endif
            FirstRunTutorialController.Instance?.PrepareForStartupReturn();
            PointerInput.SuppressUntilRelease();
            IsBlockingInput = true;
            try
            {
#if UNITY_EDITOR
                if (restartSceneForTests != null)
                {
                    restartSceneForTests(SceneName);
                    return true;
                }
#endif
                if (SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single) == null)
                    throw new System.InvalidOperationException("Splash 씬 로드를 시작하지 못했습니다.");
                return true;
            }
            catch (System.Exception exception)
            {
                IsBlockingInput = false;
                Debug.LogWarning("[MukJump] 계정 삭제 후 시작 화면 복귀 실패: " + exception.Message);
                return false;
            }
        }

        void OnEnable()
        {
            BuildIfNeeded();
            if (!Application.isPlaying)
                return;
            DontDestroyOnLoad(gameObject);
            IsBlockingInput = true;
            if (playback != null)
                StopCoroutine(playback);
            playback = StartCoroutine(PlaySequence());
        }

        void OnDisable()
        {
            IsBlockingInput = false;
            // Unity는 activation=false 작업 뒤의 로딩 큐를 멈춘다. 중단 때 반드시 해제한다.
            if (mainLoad != null && !mainLoad.isDone)
                mainLoad.allowSceneActivation = true;
            if (playback == null)
                return;
            StopCoroutine(playback);
            playback = null;
        }

        void Update()
        {
            if (!Application.isPlaying || !IsBlockingInput)
                return;
            if (waitingForRetry)
            {
                if (PointerInput.TryGetPressed(out _))
                {
                    playback = StartCoroutine(LoadLobby());
                }
                return;
            }
            PointerInput.SuppressUntilRelease();
        }

        void BuildIfNeeded()
        {
            if (rootGroup != null && logoGroup != null && logoImage != null)
            {
                ConfigureFadeGroups();
                return;
            }

            Transform existingCanvas = transform.Find("StartupBrandCanvas");
            if (existingCanvas != null)
            {
                rootGroup = existingCanvas.GetComponent<CanvasGroup>();
                logoImage = existingCanvas.Find("Logo")?.GetComponent<Image>();
                if (rootGroup != null && logoImage != null)
                {
                    ConfigureFadeGroups();
                    return;
                }
                if (Application.isPlaying)
                {
                    Debug.LogError(
                        "[MukJump] Splash 씬의 브랜드 UI 구성이 손상됐습니다.");
                    return;
                }
                DestroyImmediate(existingCanvas.gameObject);
            }

            var canvasObject = new GameObject(
                "StartupBrandCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            MobileUiLayout.ConfigurePortraitScaler(scaler);

            rootGroup = canvasObject.GetComponent<CanvasGroup>();

            RectTransform backdrop = CreateStretchRect(
                "BlackBackdrop",
                canvasObject.transform);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = Color.black;
            backdropImage.raycastTarget = true;

            RectTransform logoRect = CreateRect(
                "Logo",
                canvasObject.transform,
                Vector2.zero,
                new Vector2(1200f, 1200f));
            logoImage = logoRect.gameObject.AddComponent<Image>();
            logoImage.color = Color.white;
            logoImage.preserveAspect = false;
            logoImage.raycastTarget = false;
            ConfigureFadeGroups();
        }

        void ConfigureFadeGroups()
        {
            // 배경은 처음부터 검정으로 고정하고 브랜드만 나타났다 사라지게 한다.
            // Main이 활성화된 첫 프레임도 브랜드 뒤에서 배치한 뒤 바로 드러낸다.
            rootGroup.GetComponent<Canvas>().sortingOrder = 30000;
            rootGroup.alpha = 1f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            logoGroup = logoImage.GetComponent<CanvasGroup>();
            if (logoGroup == null)
                logoGroup = logoImage.gameObject.AddComponent<CanvasGroup>();
            logoGroup.alpha = 0f;
            logoGroup.interactable = false;
            logoGroup.blocksRaycasts = false;
            logoImage.raycastTarget = false;
        }

        public void SetLogo(Sprite sprite)
        {
            BuildIfNeeded();
            if (logoImage == null)
                return;
            logoImage.sprite = sprite;
            logoImage.enabled = sprite != null;
        }

        IEnumerator PlaySequence()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(InitialDelay);
            yield return Fade(logoGroup, 0f, 1f, FadeInDuration);
            // 로고를 보여 주는 시간에 메인을 준비한다. 로딩 전용 화면·최소 대기는 없다.
            BeginMainLoad();
            yield return new WaitForSecondsRealtime(HoldDuration);
            yield return LoadLobby();
        }

        void BeginMainLoad()
        {
            try
            {
                mainLoad = SceneManager.LoadSceneAsync(NextSceneName, LoadSceneMode.Single);
                if (mainLoad != null)
                    mainLoad.allowSceneActivation = false;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[MukJump] Main 씬을 불러오지 못했습니다: " + exception.Message);
                mainLoad = null;
            }
        }

        IEnumerator LoadLobby()
        {
            // 실패 후 탭 재시도에서도 로딩 UI를 생성하지 않는다.
            if (waitingForRetry)
            {
                waitingForRetry = false;
                if (loadFailureText != null) loadFailureText.gameObject.SetActive(false);
                if (retryText != null) retryText.gameObject.SetActive(false);
                BeginMainLoad();
            }
            if (mainLoad == null)
            {
                ShowLoadFailure();
                PointerInput.SuppressUntilRelease();
                waitingForRetry = true;
                playback = null;
                yield break;
            }
            while (mainLoad.progress < 0.9f)
                yield return null;
            mainLoad.allowSceneActivation = true;
            while (!mainLoad.isDone)
                yield return null;

            // 기존 브랜드만 걷어 메인을 바로 표시한다.
            yield return null;
            FirstRunTutorialController.Instance?.PrepareBeforeStartupReveal();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Fade(rootGroup, 1f, 0f, FadeOutDuration);
            PointerInput.SuppressUntilRelease();
            IsBlockingInput = false;
            playback = null;
            Destroy(gameObject);
        }

        void ShowLoadFailure()
        {
            // 정상 실행에는 문구를 만들지 않는다. 실제 실패 때만 재시도 길을 남긴다.
            logoGroup.alpha = 1f;
            loadFailureText = ConfigureFailureLabel(loadFailureText, "LoadFailure",
                "게임을 불러오지 못했어요", -340f, 46);
            retryText = ConfigureFailureLabel(retryText, "RetryHint",
                "화면을 눌러 다시 시도", -420f, 38);
        }

        Text ConfigureFailureLabel(Text label, string name, string message, float y, int fontSize)
        {
            if (label == null)
                label = rootGroup.transform.Find(name)?.GetComponent<Text>();
            if (label == null)
                label = CreateRect(name, rootGroup.transform, new Vector2(0f, y),
                    new Vector2(840f, 80f)).gameObject.AddComponent<Text>();
            label.font = InkPalette.UiFont;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = InkPalette.Paper;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            InkLocalizedText.SetSource(label, message);
            label.gameObject.SetActive(true);
            return label;
        }

        static IEnumerator Fade(
            CanvasGroup target,
            float from,
            float to,
            float duration)
        {
            if (target == null)
                yield break;
            float elapsed = 0f;
            target.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EvaluateFadeProgress(elapsed / duration);
                target.alpha = Mathf.LerpUnclamped(from, to, t);
                yield return null;
            }
            target.alpha = to;
        }

        static float EvaluateFadeProgress(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float remaining = 1f - t;
            return 1f - remaining * remaining * remaining;
        }

        static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
