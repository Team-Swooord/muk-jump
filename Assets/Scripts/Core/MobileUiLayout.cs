using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// iOS 노치·다이내믹 아일랜드와 Android 시스템 바를 같은 규칙으로 처리한다.
    /// 배경은 전체 화면을 쓰되, 글자와 조작 UI만 Safe Area 안에서 배치한다.
    public static class MobileUiLayout
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        public static void ConfigurePortraitScaler(CanvasScaler scaler)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;
        }

        public static Rect SanitizeSafeArea(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return Rect.zero;

            Rect full = new Rect(0f, 0f, screenWidth, screenHeight);
            if (safeArea.width <= 0f || safeArea.height <= 0f)
                return full;

            float xMin = Mathf.Clamp(safeArea.xMin, 0f, screenWidth);
            float xMax = Mathf.Clamp(safeArea.xMax, 0f, screenWidth);
            float yMin = Mathf.Clamp(safeArea.yMin, 0f, screenHeight);
            float yMax = Mathf.Clamp(safeArea.yMax, 0f, screenHeight);
            if (xMax <= xMin || yMax <= yMin)
                return full;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Rect CurrentSafeArea =>
            SanitizeSafeArea(Screen.safeArea, Screen.width, Screen.height);

        public static void ApplySafeArea(
            RectTransform target,
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (target == null || screenWidth <= 0 || screenHeight <= 0)
                return;

            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            target.anchorMin = new Vector2(
                safe.xMin / screenWidth,
                safe.yMin / screenHeight);
            target.anchorMax = new Vector2(
                safe.xMax / screenWidth,
                safe.yMax / screenHeight);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        public static void ApplyCurrentSafeArea(RectTransform target)
        {
            ApplySafeArea(target, Screen.safeArea, Screen.width, Screen.height);
        }

        public static Vector2 GetLogicalSafeSize(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return Vector2.zero;
            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            float scale = ReferenceHeight / screenHeight;
            return new Vector2(safe.width * scale, safe.height * scale);
        }

        public static Vector2 GetLogicalSafeCenterOffset(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return Vector2.zero;
            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            float scale = ReferenceHeight / screenHeight;
            return new Vector2(
                (safe.center.x - screenWidth * 0.5f) * scale,
                (safe.center.y - screenHeight * 0.5f) * scale);
        }

        public static float GetLogicalTopInset(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (screenHeight <= 0) return 0f;
            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            return Mathf.Max(0f, screenHeight - safe.yMax) *
                   ReferenceHeight / screenHeight;
        }

        public static float GetLogicalLeftInset(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            if (screenHeight <= 0) return 0f;
            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            return safe.xMin * ReferenceHeight / screenHeight;
        }

        public static float CalculateFitScale(
            Vector2 designSize,
            Rect safeArea,
            int screenWidth,
            int screenHeight,
            Vector2 edgePadding)
        {
            if (designSize.x <= 0f || designSize.y <= 0f)
                return 1f;
            Vector2 safeSize = GetLogicalSafeSize(
                safeArea,
                screenWidth,
                screenHeight);
            if (safeSize.x <= 0f || safeSize.y <= 0f)
                return 1f;
            float usableHeight = Mathf.Max(
                1f,
                safeSize.y - Mathf.Max(0f, edgePadding.y) * 2f);
            return Mathf.Clamp(
                Mathf.Min(
                    CalculateWidthFitScale(
                        designSize.x,
                        safeArea,
                        screenWidth,
                        screenHeight,
                        edgePadding.x),
                    usableHeight / designSize.y),
                0.01f,
                1f);
        }

        public static float CalculateWidthFitScale(
            float designWidth,
            Rect safeArea,
            int screenWidth,
            int screenHeight,
            float edgePadding)
        {
            if (designWidth <= 0f) return 1f;
            float safeWidth = GetLogicalSafeSize(
                safeArea,
                screenWidth,
                screenHeight).x;
            if (safeWidth <= 0f) return 1f;
            float usableWidth = Mathf.Max(
                1f,
                safeWidth - Mathf.Max(0f, edgePadding) * 2f);
            return Mathf.Clamp(usableWidth / designWidth, 0.01f, 1f);
        }

        /// 부모가 이미 축소된 상태에서, 비대칭 이미지의 실제 보이는 부분만 Safe Area에 맞춘다.
        /// anchoredPosition은 자기 localScale의 영향을 받지 않으므로 별도 항으로 계산한다.
        public static float CalculateVisibleContentFitScale(
            float visibleWidth,
            float visibleCenterOffset,
            float anchoredPositionX,
            float parentScale,
            Rect safeArea,
            int screenWidth,
            int screenHeight,
            float edgePadding)
        {
            if (visibleWidth <= 0f) return 1f;
            float safeWidth = GetLogicalSafeSize(
                safeArea,
                screenWidth,
                screenHeight).x;
            if (safeWidth <= 0f) return 1f;

            float usableHalfWidth = Mathf.Max(
                0.5f,
                (safeWidth - Mathf.Max(0f, edgePadding) * 2f) * 0.5f);
            float localHalfWidth = usableHalfWidth /
                                   Mathf.Max(0.01f, parentScale);
            float rightExtent = Mathf.Max(
                0.01f,
                visibleWidth * 0.5f + visibleCenterOffset);
            float leftExtent = Mathf.Max(
                0.01f,
                visibleWidth * 0.5f - visibleCenterOffset);
            float rightScale =
                (localHalfWidth - anchoredPositionX) / rightExtent;
            float leftScale =
                (localHalfWidth + anchoredPositionX) / leftExtent;
            return Mathf.Clamp(Mathf.Min(rightScale, leftScale), 0.01f, 1f);
        }

        /// Unity GUI는 좌상단 원점이므로 Screen.safeArea의 Y축을 뒤집어 반환한다.
        public static Rect ToGuiSafeArea(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            Rect safe = SanitizeSafeArea(
                safeArea,
                screenWidth,
                screenHeight);
            return new Rect(
                safe.xMin,
                screenHeight - safe.yMax,
                safe.width,
                safe.height);
        }
    }
}
