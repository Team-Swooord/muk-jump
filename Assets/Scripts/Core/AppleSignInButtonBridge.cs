using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// iOS에서는 Apple이 제공하는 네이티브 버튼을 Unity 계정 버튼 위에
    /// 정확히 겹쳐 표시한다. 시스템 버튼을 사용해 로고·문구·VoiceOver
    /// 규격을 OS가 직접 보장하고, 다른 플랫폼에는 아무 영향도 주지 않는다.
    public static class AppleSignInButtonBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int MukJumpAppleSignInButtonShow(
            float x,
            float y,
            float width,
            float height,
            int enabled);

        [DllImport("__Internal")]
        static extern void MukJumpAppleSignInButtonHide();

        static readonly Vector3[] Corners = new Vector3[4];
        static Rect lastNormalizedRect;
        static bool lastEnabled;
        static bool shown;
#endif

        public static void Sync(Button button, bool visible)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!visible || button == null ||
                !button.gameObject.activeInHierarchy ||
                Screen.width <= 0 || Screen.height <= 0)
            {
                Hide();
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect == null)
            {
                Hide();
                return;
            }

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null &&
                            canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            rect.GetWorldCorners(Corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
                camera,
                Corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
                camera,
                Corners[2]);
            var normalized = new Rect(
                Mathf.Clamp01(bottomLeft.x / Screen.width),
                Mathf.Clamp01(bottomLeft.y / Screen.height),
                Mathf.Clamp01((topRight.x - bottomLeft.x) / Screen.width),
                Mathf.Clamp01((topRight.y - bottomLeft.y) / Screen.height));
            if (normalized.width <= 0f || normalized.height <= 0f)
            {
                Hide();
                return;
            }

            bool enabled = button.interactable;
            if (shown && enabled == lastEnabled &&
                Approximately(lastNormalizedRect, normalized))
                return;

            int didShow = MukJumpAppleSignInButtonShow(
                normalized.x,
                normalized.y,
                normalized.width,
                normalized.height,
                enabled ? 1 : 0);
            if (didShow == 0)
            {
                shown = false;
                return;
            }
            lastNormalizedRect = normalized;
            lastEnabled = enabled;
            shown = true;
#endif
        }

        public static void Hide()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!shown)
                return;
            MukJumpAppleSignInButtonHide();
            shown = false;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static bool Approximately(Rect left, Rect right)
        {
            const float epsilon = 0.0005f;
            return Mathf.Abs(left.x - right.x) <= epsilon &&
                   Mathf.Abs(left.y - right.y) <= epsilon &&
                   Mathf.Abs(left.width - right.width) <= epsilon &&
                   Mathf.Abs(left.height - right.height) <= epsilon;
        }
#endif
    }
}
