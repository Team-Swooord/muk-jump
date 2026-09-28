using UnityEngine;

namespace MukJump.Core
{
    /// 다음 출시는 구매 화면·팝업·양쪽 광고만 포함한다. 보류한 구현과 저장은 삭제하지 않는다.
    public static class NextUpdateScope
    {
        public static bool DeferredWardrobeEnabled
        {
            get
            {
#if UNITY_EDITOR
                return deferredPreviewForTests;
#else
                return false;
#endif
            }
        }

        public static bool GrowthLengthComparisonEnabled => DeferredWardrobeEnabled;

#if UNITY_EDITOR
        // 보류 기능의 기존 회귀 검사에서만 명시적으로 사용한다. Player에는 포함하지 않는다.
        static bool deferredPreviewForTests;
        public static void UseDeferredPreviewForTests(bool enabled) => deferredPreviewForTests = enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => deferredPreviewForTests = false;
#endif
    }
}
