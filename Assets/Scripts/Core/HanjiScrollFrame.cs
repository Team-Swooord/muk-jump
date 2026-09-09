using System;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 팝업 공통 두루마리. 열고 닫을 때만 말림을 재생하며 열린 종이는 움직이지 않는다.
    [DisallowMultipleComponent]
    public sealed class HanjiScrollFrame : MonoBehaviour
    {
        const float Duration = 0.28f;
        public const float CloseDuration = 0.20f;
        RectTransform host;
        RectTransform topRoll;
        RectTransform bottomRoll;
        HanjiScrollPaperGraphic paper;
        HanjiScrollPaperGraphic shadow;
        CanvasGroup presentation;
        CanvasGroup decoration;
        CanvasGroup[] ancestors;
        Vector2 paperSize;
        float elapsed;
        bool wasVisible;
        bool closing;
        bool closed;
        float opening = 1f;
        float closeElapsed;
        float closeOpening;
        float closeContent;
        Action afterClose;
        CanvasGroup closeFadeRoot;
        float closeRootAlpha;
        public bool IsClosing => closing;
        public bool IsReady => presentation != null && presentation.interactable;

        /// 소유자가 즉시 숨기지 않고 말림 완료 뒤 화면/게임 상태를 바꾸도록 한다.
        public void Close(Action completed, CanvasGroup fadeRoot = null)
        {
            if (closing) return;
            if (!Application.isPlaying || !isActiveAndEnabled ||
                LobbySettingsProfile.ReducedMotionEnabled || AncestorAlpha() <= 0.001f)
            {
                ResetPresentation();
                completed?.Invoke();
                return;
            }
            BeginClose(completed, fadeRoot);
        }

        void BeginClose(Action completed, CanvasGroup fadeRoot)
        {
            closing = true;
            closed = false;
            closeElapsed = 0f;
            closeOpening = opening;
            closeContent = presentation.alpha;
            afterClose = completed;
            closeFadeRoot = fadeRoot;
            closeRootAlpha = fadeRoot != null ? fadeRoot.alpha : 1f;
            presentation.interactable = false;
            // 투명해지는 버튼/하위 모달도 뒤쪽 화면으로 터치를 통과시키지 않는다.
            presentation.blocksRaycasts = true;
        }

        public void CancelClose()
        {
            if (closing && closeFadeRoot != null) closeFadeRoot.alpha = closeRootAlpha;
            afterClose = null;
            closeFadeRoot = null;
            closing = closed = false;
            // 닫던 현재 길이에서 다시 펼친다. 완전히 열린 자세로 튀지 않는다.
            elapsed = Duration * (1f - Mathf.Pow(1f - opening, 1f / 3f));
            if (decoration != null) decoration.alpha = AncestorAlpha();
        }

        public void ResetPresentation()
        {
            afterClose = null;
            closeFadeRoot = null;
            closing = closed = wasVisible = false;
            elapsed = 0f;
            if (presentation == null) return;
            presentation.alpha = 0f;
            presentation.interactable = presentation.blocksRaycasts = false;
            if (Application.isPlaying && paper != null) SetPose(0f, 0f, false);
        }

        /// 계정 선택/저장 복구처럼 GameObject 활성 상태로 관리되는 하위 팝업용.
        public static void SetActiveAnimated(GameObject root, bool visible)
        {
            if (root == null) return;
            var frame = root.GetComponentInChildren<HanjiScrollFrame>(true);
            if (visible)
            {
                if (frame != null && frame.IsClosing) frame.CancelClose();
                root.SetActive(true);
            }
            else if (root.activeSelf)
            {
                if (frame != null) frame.Close(() => root.SetActive(false));
                else root.SetActive(false);
            }
        }

        public static HanjiScrollFrame Attach(RectTransform panel, Vector2 size)
        {
            var frame = panel.GetComponent<HanjiScrollFrame>();
            if (frame != null) return frame;
            frame = panel.gameObject.AddComponent<HanjiScrollFrame>();
            frame.Initialize(panel, size);
            return frame;
        }

        void Initialize(RectTransform panel, Vector2 size)
        {
            host = panel;
            paperSize = size;
            // 이 그룹은 연출 전용이다. 상위 페이지/계정 잠금 상태는 변경하지 않는다.
            presentation = panel.gameObject.AddComponent<CanvasGroup>();
            ancestors = panel.parent.GetComponentsInParent<CanvasGroup>(true);
            var art = Rect("HanjiScrollArt", panel, Vector2.zero, size);
            art.SetAsFirstSibling();
            decoration = art.gameObject.AddComponent<CanvasGroup>();
            decoration.ignoreParentGroups = true;
            decoration.interactable = decoration.blocksRaycasts = false;
            Texture texture = Resources.Load<Texture2D>("MukJump/UI/PermanentGrowth/pg_hanji_background");
            shadow = Rect("Shadow", art, new Vector2(4, -6), size)
                .gameObject.AddComponent<HanjiScrollPaperGraphic>();
            shadow.Configure(texture, new Color(0.12f, 0.1f, 0.08f, 0.12f));
            paper = Rect("Paper", art, Vector2.zero, size)
                .gameObject.AddComponent<HanjiScrollPaperGraphic>();
            paper.Configure(texture, texture != null ? Color.white : InkPalette.Paper);
            var sprite = Resources.Load<Sprite>("MukJump/UI/Common/scroll_roll_hanji_v2");
            topRoll = Roll("TopRoll", art, sprite);
            bottomRoll = Roll("BottomRoll", art, sprite);
            SetPose(1f, 0f, false);
            decoration.alpha = AncestorAlpha();
            if (Application.isPlaying) ResetPresentation();
        }

        RectTransform Roll(string name, Transform parent, Sprite sprite)
        {
            var rect = Rect(name, parent, Vector2.zero, new Vector2(paperSize.x + 42f, 62f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : InkPalette.Paper2;
            image.raycastTarget = false;
            return rect;
        }

        float AncestorAlpha()
        {
            float alpha = 1f;
            foreach (var group in ancestors)
            {
                if (group == null || !group.enabled) continue;
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            return alpha;
        }

        void LateUpdate()
        {
            if (host == null || !Application.isPlaying) return;
            float alpha = AncestorAlpha();
            bool visible = alpha > 0.001f;
            decoration.alpha = alpha;
            // 마지막 dim 페이드가 거의 투명해져도 완료 콜백까지 반드시 진행한다.
            if (closing)
            {
                AdvanceClose(Time.unscaledDeltaTime);
                return;
            }
            if (!visible)
            {
                wasVisible = false;
                presentation.alpha = 0f;
                presentation.interactable = presentation.blocksRaycasts = false;
                return;
            }
            if (closed) return;
            bool justOpened = !wasVisible;
            if (justOpened) { elapsed = 0f; wasVisible = true; }
            if (!MobileApplicationLifecycle.IsApplicationActive) return;
            // 열린 뒤에는 시간 누적·메시 갱신·롤의 반복 흔들림을 하지 않는다.
            if (elapsed >= Duration && presentation.interactable) return;
            bool reduced = LobbySettingsProfile.ReducedMotionEnabled;
            // UI 생성에 걸린 이전 프레임 시간으로 첫 펼침을 건너뛰지 않는다.
            elapsed = reduced ? Duration : Mathf.Min(Duration, elapsed + (justOpened ? 0f : Time.unscaledDeltaTime));
            float t = elapsed / Duration;
            SetPose(1f - Mathf.Pow(1f - t, 3f), 0f, false);
            float content = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t));
            presentation.alpha = content;
            presentation.interactable = presentation.blocksRaycasts = content >= 0.99f;
        }

        void AdvanceClose(float deltaTime)
        {
            if (!closing || !MobileApplicationLifecycle.IsApplicationActive) return;
            closeElapsed = LobbySettingsProfile.ReducedMotionEnabled
                ? CloseDuration : Mathf.Min(CloseDuration, closeElapsed + deltaTime);
            float t = closeElapsed / CloseDuration;
            SetPose(Mathf.Lerp(closeOpening, 0f, Mathf.SmoothStep(0f, 1f, t)), 0f, false);
            presentation.alpha = closeContent *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f)));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, t));
            if (closeFadeRoot != null) closeFadeRoot.alpha = closeRootAlpha * fade;
            decoration.alpha = AncestorAlpha() * (closeFadeRoot != null ? 1f : fade);
            if (t < 1f) return;
            Action completed = afterClose;
            afterClose = null;
            closeFadeRoot = null;
            closing = false;
            closed = true;
            presentation.interactable = presentation.blocksRaycasts = false;
            completed?.Invoke();
        }

        void OnDisable() { ResetPresentation(); }

        public void SetPaperSize(Vector2 size)
        {
            if (paperSize == size) return;
            paperSize = size;
            paper.rectTransform.sizeDelta = shadow.rectTransform.sizeDelta = size;
            topRoll.sizeDelta = bottomRoll.sizeDelta = new Vector2(size.x + 42f, 62f);
            SetPose(opening, 0f, false);
        }

        public void SetPose(float opening, float seconds, bool flutter)
        {
            this.opening = Mathf.Clamp01(opening);
            // 이전 호출자가 flutter를 전달해도 반복 장식을 다시 켜지 않는다.
            paper.SetPose(opening, 0f, 0f);
            shadow.SetPose(opening, 0f, 0f);
            float half = paperSize.y * 0.5f;
            float fraction = Mathf.Lerp(HanjiScrollPaperGraphic.ClosedFraction, 1f, opening);
            topRoll.anchoredPosition = new Vector2(0, half);
            bottomRoll.anchoredPosition = new Vector2(0, half - paperSize.y * fraction);
            bottomRoll.localScale = new Vector3(1, Mathf.Lerp(1.34f, 1f, opening), 1);
            bottomRoll.localEulerAngles = Vector3.zero;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
