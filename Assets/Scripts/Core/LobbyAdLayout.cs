using UnityEngine;

namespace MukJump.Core
{
    /// 네이티브·Apps in Toss·에디터 배너가 실제로 차지하는 상단 높이를
    /// 로비·플레이 HUD에 전달한다. 값은 전체 화면 높이에 대한 0~1 비율이다.
    public static class LobbyAdLayout
    {
        const float MaximumTopInsetFraction = 0.25f;
        public const float ReservedBannerInsetFraction = 0.08f;

        public static float TopInsetFraction { get; private set; }
        public static float MeasuredTopInsetFraction { get; private set; }
        public static float GameplayTopInsetFraction { get; private set; }
        public static bool IsBannerVisible { get; private set; }
        public static bool HasReservedTopInset { get; private set; }

        /// 로비가 처음 배치되기 전에 플랫폼 공통 최대 광고 슬롯을 고정한다.
        public static void ReserveDefaultTopInset()
        {
            if (HasReservedTopInset)
                return;
            TopInsetFraction = ReservedBannerInsetFraction;
            GameplayTopInsetFraction = Mathf.Max(GameplayTopInsetFraction, TopInsetFraction);
            HasReservedTopInset = true;
        }

        public static void SetTopInsetPixels(float heightPixels)
        {
            if (Screen.height <= 0)
            {
                MeasuredTopInsetFraction = 0f;
                return;
            }
            SetTopInsetFraction(heightPixels / Screen.height);
        }

        public static void SetTopInsetFraction(float fraction)
        {
            MeasuredTopInsetFraction = Mathf.Clamp(
                float.IsNaN(fraction) || float.IsInfinity(fraction) ? 0f : fraction,
                0f,
                MaximumTopInsetFraction);
            // 플레이 HUD는 예약 슬롯이 아니라 최신 실제 배너 하단에 붙인다.
            // 작은 배너로 바뀌면 빈 공간을 돌려주되, 숨김/no-fill의 0은 마지막 기준선을 유지한다.
            if (MeasuredTopInsetFraction > 0f)
                GameplayTopInsetFraction = MeasuredTopInsetFraction;
            // 첫 레이아웃 전 예약을 빠뜨린 기존 호출 경로만 측정값으로 초기화한다.
            // 예약 이후의 load/resize/no-fill은 실제 광고만 바꾸고 로비는 움직이지 않는다.
            if (HasReservedTopInset)
                return;
            TopInsetFraction = MeasuredTopInsetFraction;
            HasReservedTopInset = true;
        }

        public static void MarkBannerVisible() => IsBannerVisible = true;

        /// 화면 전환 중에는 네이티브 광고만 숨기고 로비 기준선은 유지한다.
        /// 예약 높이까지 지우면 서로 다른 앵커를 쓰는 로비 요소가 제각각 움직인다.
        public static void MarkBannerHidden() => IsBannerVisible = false;

        /// 광고 런타임이 완전히 종료될 때만 예약 높이를 해제한다.
        public static void ClearTopInset()
        {
            TopInsetFraction = 0f;
            MeasuredTopInsetFraction = 0f;
            GameplayTopInsetFraction = 0f;
            HasReservedTopInset = false;
            IsBannerVisible = false;
        }

        public static float CalculateCanvasInset(
            float referenceHeight,
            float topInsetFraction) =>
            Mathf.Max(1f, referenceHeight) * Mathf.Clamp(
                topInsetFraction,
                0f,
                MaximumTopInsetFraction);

        public static bool IsPointerInBannerSlot(Vector2 position) =>
            IsPointerInBannerSlot(position, MobileUiLayout.CurrentSafeArea, Screen.width, Screen.height);

        public static bool IsPointerInBannerSlot(Vector2 position, Rect safeArea, int width, int height)
        {
            if (GameplayTopInsetFraction <= 0f || width <= 0 || height <= 0) return false;
            Rect safe = MobileUiLayout.SanitizeSafeArea(safeArea, width, height);
            float bottom = safe.yMax - height * GameplayTopInsetFraction;
            return position.x >= 0f && position.x <= width && position.y >= bottom && position.y <= height;
        }
    }
}
