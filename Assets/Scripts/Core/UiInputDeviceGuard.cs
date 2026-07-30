using UnityEngine;

namespace MukJump.Core
{
    /// Device Simulator가 비활성화한 마우스를 로비·모달 uGUI보다 먼저 복구한다.
    /// Player 빌드에서는 PointerInput 내부가 no-op이므로 런타임 비용이 없다.
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class UiInputDeviceGuard : MonoBehaviour
    {
        void Update()
        {
            PointerInput.EnsureUiDevicesUsable();
        }

#if UNITY_EDITOR
        public void TickForTests()
        {
            PointerInput.EnsureUiDevicesUsable();
        }
#endif
    }
}
