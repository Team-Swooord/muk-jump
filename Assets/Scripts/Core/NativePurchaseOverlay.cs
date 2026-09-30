using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 결제 오버레이의 음향·입력 잠금. 광고/백그라운드/사용자 설정과 별도로 소유한다.
    public sealed class NativePurchaseOverlay : IDisposable
    {
        static int activeCount;
        bool ended;
        public static bool IsActive => activeCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => activeCount = 0;

        public NativePurchaseOverlay()
        {
            activeCount++;
            BackgroundMusicController.Instance?.SetPurchaseActive(true);
            AudioListener.pause = true;
            PointerInput.SuppressUntilRelease();
        }

        public void Dispose()
        {
            if (ended) return;
            ended = true;
            activeCount = Math.Max(0, activeCount - 1);
            BackgroundMusicController.Instance?.SetPurchaseActive(IsActive);
            AudioListener.pause = IsActive || !MobileApplicationLifecycle.IsApplicationActive ||
                BackgroundMusicController.Instance != null && BackgroundMusicController.Instance.IsFullScreenAdActive ||
                GameManager.Instance != null && GameManager.Instance.IsPaused;
            PointerInput.SuppressUntilRelease();
        }
    }
}
