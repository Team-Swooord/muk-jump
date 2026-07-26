using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 이전 최고 고도를 넘긴 순간 낙관처럼 찍히고, 이번 판이 끝날 때까지 유지되는 HUD.
    [ExecuteAlways]
    public sealed class NewBestIndicatorView : MonoBehaviour
    {
        const float StampDuration = 0.42f;

        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform stampRoot;
        [SerializeField] Image sealImage;
        [SerializeField] Text valueText;

        ScoreManager boundScore;
        bool gameplayVisible = true;
        bool recordVisible;
        bool stampAnimating;
        float stampElapsed;

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
            var rootObject = new GameObject(
                "NewBestInkSeal",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.835f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(460f, 108f);
            root.anchoredPosition = Vector2.zero;

            var paper = rootObject.GetComponent<Image>();
            paper.sprite = InkUiTextureFactory.CreateBrushSprite();
            paper.color = InkPalette.Paper;
            paper.raycastTarget = false;

            var sealRoot = CreateRect(
                "RecordSeal", root, new Vector2(0.13f, 0.5f), new Vector2(74f, 74f));
            sealRoot.localRotation = Quaternion.Euler(0f, 0f, -5f);
            var seal = sealRoot.gameObject.AddComponent<Image>();
            seal.sprite = InkUiTextureFactory.CreateBlobSprite();
            seal.color = InkPalette.Red;
            seal.raycastTarget = false;
            CreateText("SealText", sealRoot, "최고", 23, InkPalette.Paper,
                new Vector2(0.5f, 0.5f), new Vector2(62f, 58f));

            var value = CreateText(
                "RecordText", root, "지금 기록이 최고 · 0m", 28, InkPalette.Red,
                new Vector2(0.61f, 0.51f), new Vector2(350f, 64f));

            var view = rootObject.AddComponent<NewBestIndicatorView>();
            view.rootGroup = rootObject.GetComponent<CanvasGroup>();
            view.stampRoot = sealRoot;
            view.sealImage = seal;
            view.valueText = value;
            view.ConfigureVisuals();
            view.BindScoreManager();
            view.ApplyVisibility();
            return view;
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
                ShowRecord(true);
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
            if (stampRoot != null)
            {
                stampRoot.localScale = animate ? Vector3.one * 1.55f : Vector3.one;
                stampRoot.localRotation = animate
                    ? Quaternion.Euler(0f, 0f, -12f)
                    : Quaternion.Euler(0f, 0f, -5f);
            }
            UpdateValueText();
            ApplyVisibility();
        }

        void HideRecord()
        {
            recordVisible = false;
            stampAnimating = false;
            stampElapsed = 0f;
            ApplyVisibility();
        }

        void UpdateStampAnimation()
        {
            if (!stampAnimating || stampRoot == null) return;

            stampElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(stampElapsed / StampDuration);
            float scale;
            if (progress < 0.72f)
            {
                float strike = Mathf.SmoothStep(0f, 1f, progress / 0.72f);
                scale = Mathf.Lerp(1.55f, 0.92f, strike);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, (progress - 0.72f) / 0.28f);
                scale = Mathf.Lerp(0.92f, 1f, settle);
            }

            stampRoot.localScale = Vector3.one * scale;
            stampRoot.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Lerp(-12f, -5f, Mathf.SmoothStep(0f, 1f, progress)));
            if (progress >= 1f)
                stampAnimating = false;
        }

        void UpdateValueText()
        {
            if (valueText == null) return;
            int best = boundScore != null ? boundScore.DisplayBest : 0;
            valueText.text = Application.isPlaying
                ? $"지금 기록이 최고 · {best}m"
                : "지금 기록이 최고 · 123m";
        }

        void ConfigureVisuals()
        {
            if (sealImage != null)
            {
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
            if (rootGroup != null)
            {
                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;
            }
        }

        void ApplyVisibility()
        {
            if (rootGroup == null) return;
            bool visible = !Application.isPlaying || (gameplayVisible && recordVisible);
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
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
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
