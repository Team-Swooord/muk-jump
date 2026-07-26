using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 이전 최고 고도를 넘긴 순간 낙관처럼 찍히고, 이번 판이 끝날 때까지 유지되는 HUD.
    public sealed class NewBestIndicatorView : MonoBehaviour
    {
        const float StampDuration = 0.24f;
        const float FullEmphasisHold = 0.7f;
        const float RestingAlpha = 0.78f;

        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform stampRoot;
        [SerializeField] Image sealImage;
        [SerializeField] Text sealText;
        [SerializeField] Text valueText;

        ScoreManager boundScore;
        bool gameplayVisible = true;
        bool recordVisible;
        bool stampAnimating;
        float stampElapsed;
        float visibleElapsed;
        float visualAlpha = RestingAlpha;

        void Awake()
        {
            ConfigureVisuals();
            BindScoreManager();
            ApplyVisibility();
        }

        void OnEnable()
        {
            ConfigureVisuals();
            BindScoreManager();
            ApplyVisibility();
        }

        void OnDisable()
        {
            UnbindScoreManager();
        }

        void OnValidate()
        {
            ConfigureVisuals();
            ApplyVisibility();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                recordVisible = true;
                UpdateValueText();
                ApplyVisibility();
                return;
            }

            BindScoreManager();
            bool shouldShow = boundScore != null && boundScore.IsNewBestThisRun;
            if (shouldShow && !recordVisible)
                ShowRecord(false);
            else if (!shouldShow && recordVisible)
                HideRecord();

            UpdateValueText();
            UpdateStampAnimation();
            UpdateRestingEmphasis();
            ApplyVisibility();
        }

        public void SetVisible(bool visible)
        {
            gameplayVisible = visible;
            ApplyVisibility();
        }

        /// 씬 빌더를 아직 다시 실행하지 않은 기존 Main 씬에서도 신기록 HUD가 즉시 동작한다.
        public static NewBestIndicatorView CreateRuntime(Transform parent)
        {
            var existing = parent.GetComponentInChildren<NewBestIndicatorView>(true);
            if (existing != null)
            {
                existing.ApplyPolishedLayout();
                return existing;
            }

            var rootObject = new GameObject(
                "NewBestInkSeal",
                typeof(RectTransform),
                typeof(CanvasGroup));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.955f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(50f, 50f);
            root.anchoredPosition = Vector2.zero;

            var sealRoot = CreateRect(
                "RecordSeal", root, new Vector2(0.5f, 0.5f), new Vector2(50f, 50f));
            sealRoot.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var seal = sealRoot.gameObject.AddComponent<Image>();
            seal.sprite = InkUiTextureFactory.CreateBlobSprite();
            seal.color = InkPalette.Red;
            seal.raycastTarget = false;
            var recordText = CreateText("SealText", sealRoot, "신", 22, InkPalette.Paper,
                new Vector2(0.5f, 0.5f), new Vector2(38f, 36f));

            var view = rootObject.AddComponent<NewBestIndicatorView>();
            view.rootGroup = rootObject.GetComponent<CanvasGroup>();
            view.stampRoot = sealRoot;
            view.sealImage = seal;
            view.sealText = recordText;
            view.ConfigureVisuals();
            view.BindScoreManager();
            view.ApplyVisibility();
            return view;
        }

        public void ApplyPolishedLayout()
        {
            ApplyCompactRuntimeLayout();
        }

        void BindScoreManager()
        {
            var score = ScoreManager.Instance;
            if (score == boundScore) return;

            UnbindScoreManager();
            boundScore = score;
            if (boundScore == null) return;

            boundScore.NewBestReached += HandleNewBestReached;
            if (boundScore.IsNewBestThisRun)
                ShowRecord(false);
        }

        void UnbindScoreManager()
        {
            if (boundScore != null)
                boundScore.NewBestReached -= HandleNewBestReached;
            boundScore = null;
        }

        void HandleNewBestReached(int height, int previousBest)
        {
            ShowRecord(true);
        }

        void ShowRecord(bool animate)
        {
            recordVisible = true;
            stampAnimating = animate;
            stampElapsed = 0f;
            visibleElapsed = 0f;
            visualAlpha = animate ? 0f : RestingAlpha;
            if (stampRoot != null)
            {
                stampRoot.localScale = animate ? Vector3.one * 1.18f : Vector3.one;
                stampRoot.localRotation = animate
                    ? Quaternion.Euler(0f, 0f, -8f)
                    : Quaternion.Euler(0f, 0f, -4f);
            }
            UpdateValueText();
            ApplyVisibility();
        }

        void HideRecord()
        {
            recordVisible = false;
            stampAnimating = false;
            stampElapsed = 0f;
            visibleElapsed = 0f;
            visualAlpha = RestingAlpha;
            ApplyVisibility();
        }

        void UpdateStampAnimation()
        {
            if (!stampAnimating || stampRoot == null) return;

            stampElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(stampElapsed / StampDuration);
            float scale;
            if (progress < 0.58f)
            {
                float strike = EaseOutCubic(progress / 0.58f);
                scale = Mathf.Lerp(1.18f, 0.94f, strike);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, (progress - 0.58f) / 0.42f);
                scale = Mathf.Lerp(0.94f, 1f, settle);
            }

            visualAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.58f));
            stampRoot.localScale = Vector3.one * scale;
            stampRoot.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Lerp(-8f, -4f, EaseOutCubic(progress)));
            if (progress >= 1f)
                stampAnimating = false;
        }

        void UpdateRestingEmphasis()
        {
            if (!recordVisible || stampAnimating || !Application.isPlaying) return;
            visibleElapsed += Time.unscaledDeltaTime;
            if (visibleElapsed <= FullEmphasisHold) return;
            visualAlpha = Mathf.MoveTowards(
                visualAlpha, RestingAlpha,
                (1f - RestingAlpha) / 0.18f * Time.unscaledDeltaTime);
        }

        void UpdateValueText()
        {
            if (valueText == null || !valueText.gameObject.activeSelf) return;
            int best = boundScore != null ? boundScore.DisplayBest : 0;
            valueText.text = $"신기록 · {best}m";
        }

        void ConfigureVisuals()
        {
            if (stampRoot != null && sealText == null)
                sealText = stampRoot.Find("SealText")?.GetComponent<Text>();
            if (sealImage != null)
            {
                if (Application.isPlaying && sealImage.sprite == null)
                    sealImage.sprite = InkUiTextureFactory.CreateBlobSprite();
                sealImage.color = InkPalette.Red;
                sealImage.raycastTarget = false;
            }
            if (valueText != null)
            {
                valueText.font = InkPalette.UiFont;
                valueText.color = InkPalette.Red;
                valueText.raycastTarget = false;
            }
            if (sealText != null)
            {
                sealText.text = "신";
                sealText.font = InkPalette.UiFont;
                sealText.fontSize = 22;
                sealText.fontStyle = FontStyle.Normal;
                sealText.alignment = TextAnchor.MiddleCenter;
                sealText.color = InkPalette.Paper;
                sealText.raycastTarget = false;
            }
            if (rootGroup != null)
            {
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
            if (Application.isPlaying)
                ApplyCompactRuntimeLayout();
        }

        void ApplyVisibility()
        {
            if (rootGroup == null) return;
            bool visible = !Application.isPlaying || (gameplayVisible && recordVisible);
            rootGroup.alpha = visible
                ? (Application.isPlaying ? visualAlpha : 1f)
                : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        void ApplyCompactRuntimeLayout()
        {
            if (transform is RectTransform root)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.955f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(50f, 50f);
            }

            var legacyBackground = GetComponent<Graphic>();
            if (legacyBackground != null) legacyBackground.enabled = false;
            if (valueText != null) valueText.gameObject.SetActive(false);

            if (stampRoot == null) return;
            stampRoot.anchorMin = stampRoot.anchorMax = new Vector2(0.5f, 0.5f);
            stampRoot.pivot = new Vector2(0.5f, 0.5f);
            stampRoot.anchoredPosition = Vector2.zero;
            stampRoot.sizeDelta = new Vector2(50f, 50f);
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static RectTransform CreateRect(
            string name, Transform parent, Vector2 anchor, Vector2 size)
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

        static Text CreateText(
            string name, Transform parent, string value, int fontSize, Color color,
            Vector2 anchor, Vector2 size)
        {
            var rect = CreateRect(name, parent, anchor, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
