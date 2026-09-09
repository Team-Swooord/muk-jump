using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 이전 최고 고도를 넘긴 순간 낙관처럼 찍히고, 이번 판이 끝날 때까지 유지되는 HUD.
    public sealed class NewBestIndicatorView : MonoBehaviour
    {
        const float StampDuration = 0.24f;
        const float FullEmphasisHold = 0.7f;
        const float RestingAlpha = 0.94f;
        public const float BadgeSize = 96f;
        public const float BadgeHeight = 28f;
        public const float BadgeCenterOffsetY = 21f;
        public const int BadgeFontSize = 28;

        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform stampRoot;
        [SerializeField] Image sealImage;
        [SerializeField] Text sealText;
        Image impactWash;

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
            InkLocalizedText.BindTree(transform);
            BindScoreManager();
            ApplyVisibility();
        }

        void OnDisable()
        {
            UnbindScoreManager();
        }

        void OnValidate()
        {
            // OnValidate 중 CanvasGroup 값을 바꾸면 자식 Graphic으로
            // OnCanvasGroupChanged SendMessage가 발생해 에디터 Console을 오염시킨다.
            // 여기서는 직렬화 참조만 복구하고 실제 시각 적용은 Awake/OnEnable에 맡긴다.
            if (rootGroup == null)
                rootGroup = GetComponent<CanvasGroup>();
            if (stampRoot != null && sealText == null)
                sealText = stampRoot.Find("SealText")?.GetComponent<Text>();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                recordVisible = true;
                ApplyVisibility();
                return;
            }

            BindScoreManager();
            bool shouldShow = boundScore != null && boundScore.IsNewBestThisRun;
            if (shouldShow && !recordVisible)
                ShowRecord(false);
            else if (!shouldShow && recordVisible)
                HideRecord();

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
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(BadgeSize, BadgeHeight);
            root.anchoredPosition = new Vector2(0f, BadgeCenterOffsetY);

            var sealRoot = CreateRect(
                "RecordSeal", root, new Vector2(0.5f, 0.5f), new Vector2(BadgeSize, BadgeHeight));
            var seal = sealRoot.gameObject.AddComponent<Image>();
            seal.sprite = InkUiTextureFactory.CreateBlobSprite();
            seal.color = InkPalette.Red;
            seal.raycastTarget = false;
            var recordText = CreateText("SealText", sealRoot, "NEW!", BadgeFontSize, InkPalette.Red,
                new Vector2(0.5f, 0.5f), new Vector2(78f, 32f));

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
            ConfigureVisuals();
        }

        void BindScoreManager()
        {
            var score = ScoreManager.Instance;
            if (score == boundScore) return;

            UnbindScoreManager();
            boundScore = score;
            if (boundScore == null) { HideRecord(); return; }

            boundScore.NewBestReached += HandleNewBestReached;
            if (boundScore.IsNewBestThisRun)
                ShowRecord(false);
            else
                HideRecord();
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
            GameFeedbackController.Instance?.PlayRecordStamp();
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
                stampRoot.localScale = animate ? Vector3.one * 1.12f : Vector3.one;
                stampRoot.localRotation = animate
                    ? Quaternion.Euler(0f, 0f, -8f)
                    : Quaternion.identity;
            }
            ResetImpactWash();
            ApplyVisibility();
        }

        void HideRecord()
        {
            recordVisible = false;
            stampAnimating = false;
            stampElapsed = 0f;
            visibleElapsed = 0f;
            visualAlpha = RestingAlpha;
            ResetImpactWash();
            ApplyVisibility();
        }

        void UpdateStampAnimation()
        {
            if (!stampAnimating || stampRoot == null) return;

            stampElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(stampElapsed / StampDuration);
            ApplyStampProgress(progress);
            if (progress >= 1f)
                stampAnimating = false;
        }

        void ApplyStampProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            float scale;
            if (progress < 0.58f)
            {
                float strike = EaseOutCubic(progress / 0.58f);
                scale = Mathf.Lerp(1.12f, 0.96f, strike);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, (progress - 0.58f) / 0.42f);
                scale = Mathf.Lerp(0.96f, 1f, settle);
            }

            visualAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.58f));
            stampRoot.localScale = Vector3.one * scale;
            stampRoot.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Lerp(-8f, 0f, EaseOutCubic(progress)));
            if (impactWash != null)
            {
                impactWash.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(0.9f, 1.46f, EaseOutCubic(progress));
                Color color = InkPalette.Red;
                color.a = 0.26f * (1f - progress) * (1f - progress);
                impactWash.color = color;
            }
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

        void ConfigureVisuals()
        {
            if (stampRoot != null && sealText == null)
                sealText = stampRoot.Find("SealText")?.GetComponent<Text>();
            if (sealImage != null)
            {
                if (Application.isPlaying && sealImage.sprite == null)
                    sealImage.sprite = InkUiTextureFactory.CreateBlobSprite();
                sealImage.color = InkPalette.Red;
                // 광고 아래 점수칸 안에서 짧은 붉은 글자만 찍는다. 큰 원형 받침은 공간을 차지하지 않는다.
                sealImage.enabled = false;
                sealImage.raycastTarget = false;
            }
            if (sealText != null)
            {
                // 한국어/영어 모두 짧은 NEW! 표식을 쓴다. 1글자용 '신' 칸을 번역하지 않는다.
                InkLocalizedText.SetSource(sealText, "NEW!");
                sealText.font = InkPalette.UiFont;
                sealText.fontSize = BadgeFontSize;
                sealText.fontStyle = FontStyle.Bold;
                sealText.alignment = TextAnchor.MiddleCenter;
                sealText.color = InkPalette.Red;
                sealText.raycastTarget = false;
                sealText.resizeTextForBestFit = false;
                sealText.horizontalOverflow = HorizontalWrapMode.Overflow;
                sealText.verticalOverflow = VerticalWrapMode.Truncate;
                sealText.alignByGeometry = true;
            }
            if (rootGroup != null)
            {
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
            EnsureImpactWash();
            ApplyRecordLayout();
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

        void ApplyRecordLayout()
        {
            if (transform is RectTransform root)
            {
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = new Vector2(0f, BadgeCenterOffsetY);
                root.sizeDelta = new Vector2(BadgeSize, BadgeHeight);
            }

            var legacyBackground = GetComponent<Graphic>();
            if (legacyBackground != null) legacyBackground.enabled = false;

            if (stampRoot == null) return;
            stampRoot.anchorMin = stampRoot.anchorMax = new Vector2(0.5f, 0.5f);
            stampRoot.pivot = new Vector2(0.5f, 0.5f);
            stampRoot.anchoredPosition = Vector2.zero;
            stampRoot.sizeDelta = new Vector2(BadgeSize, BadgeHeight);
            if (!stampAnimating) stampRoot.localRotation = Quaternion.identity;
            if (sealText != null)
            {
                sealText.rectTransform.anchorMin = sealText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                sealText.rectTransform.anchoredPosition = Vector2.zero;
                sealText.rectTransform.sizeDelta = new Vector2(78f, 32f);
            }
        }

        void EnsureImpactWash()
        {
            if (stampRoot == null) return;
            if (impactWash == null)
            {
                var existing = transform.Find("ImpactWash");
                impactWash = existing != null ? existing.GetComponent<Image>() : null;
                if (impactWash == null)
                {
                    var rect = CreateRect("ImpactWash", transform, new Vector2(0.5f, 0.5f),
                        new Vector2(BadgeSize, BadgeHeight));
                    rect.SetAsFirstSibling();
                    impactWash = rect.gameObject.AddComponent<Image>();
                }
                ResetImpactWash();
            }
            // 최초 생성 때 준비한 한 장을 재사용한다. 신기록 이벤트에서 생성/파괴하지 않는다.
            impactWash.sprite = sealImage != null ? sealImage.sprite : null;
            impactWash.rectTransform.sizeDelta = new Vector2(BadgeSize, BadgeHeight);
            impactWash.raycastTarget = false;
        }

        void ResetImpactWash()
        {
            if (impactWash == null) return;
            impactWash.color = Color.clear;
            impactWash.rectTransform.localScale = Vector3.one;
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
            InkLocalizedText.SetSource(text, value);
            text.font = InkPalette.UiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            text.raycastTarget = false;
            InkLocalizedText.Bind(text);
            return text;
        }
    }
}
