using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 기존 팝업 내용을 유지하면서 먹 웅덩이 형태와 순차 등장 모션을 적용한다.
    public sealed class InkPopupAnimator : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup contentGroup;
        Image backdrop;
        bool initialized;

        public void Show()
        {
            gameObject.SetActive(true);
            Initialize();
            StopAllCoroutines();
            StartCoroutine(ShowRoutine());
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            backdrop = GetComponent<Image>();
            panel = transform.Find("PaperPanel") as RectTransform;
            if (panel == null) return;

            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = InkUiTextureFactory.CreateBlobSprite();
                panelImage.type = Image.Type.Simple;
                panelImage.color = new Color(0.045f, 0.043f, 0.038f, 0.98f);
            }
            var outline = panel.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;

            contentGroup = panel.GetComponent<CanvasGroup>();
            if (contentGroup == null) contentGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                texts[i].color = texts[i].name == "Label" ? InkPalette.Ink : InkPalette.Paper;

            var close = panel.Find("CloseButton")?.GetComponent<Image>();
            if (close != null)
            {
                close.color = InkPalette.Paper;
                close.sprite = InkUiTextureFactory.CreateBrushSprite();
                close.type = Image.Type.Simple;
            }
        }

        IEnumerator ShowRoutine()
        {
            if (panel == null) yield break;
            if (backdrop != null) backdrop.color = new Color(0.04f, 0.038f, 0.034f, 0f);
            contentGroup.alpha = 0f;
            panel.localScale = Vector3.one * 0.62f;

            float elapsed = 0f;
            const float duration = 0.72f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.075f;
                panel.localScale = Vector3.one * (Mathf.Lerp(0.62f, 1f, eased) + overshoot);
                contentGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.9f, t));
                if (backdrop != null)
                    backdrop.color = new Color(0.04f, 0.038f, 0.034f, 0.62f * eased);
                yield return null;
            }
            panel.localScale = Vector3.one;
            contentGroup.alpha = 1f;
        }
    }
}
