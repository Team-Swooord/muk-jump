using UnityEngine;

namespace MukJump.Core
{
    /// 네이티브·Apps in Toss·에디터 배너가 실제로 차지하는 상단 높이를
    /// 로비 레이아웃에 전달한다. 값은 전체 화면 높이에 대한 0~1 비율이다.
    public static class LobbyAdLayout
    {
        const float MaximumTopInsetFraction = 0.25f;

        public static float TopInsetFraction { get; private set; }

        public static void SetTopInsetPixels(float heightPixels)
        {
            if (Screen.height <= 0)
            {
                TopInsetFraction = 0f;
                return;
            }
            SetTopInsetFraction(heightPixels / Screen.height);
        }

        public static void SetTopInsetFraction(float fraction)
        {
            TopInsetFraction = Mathf.Clamp(
                fraction,
                0f,
                MaximumTopInsetFraction);
        }

        public static void ClearTopInset() => TopInsetFraction = 0f;

        public static float CalculateCanvasInset(
            float referenceHeight,
            float topInsetFraction) =>
            Mathf.Max(1f, referenceHeight) * Mathf.Clamp(
                topInsetFraction,
                0f,
                MaximumTopInsetFraction);
    }
}
