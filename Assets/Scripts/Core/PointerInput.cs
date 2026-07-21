using UnityEngine;
using UnityEngine.InputSystem;

namespace MukJump.Core
{
    /// 터치·마우스·펜을 모두 지원하는 포인터 입력 헬퍼.
    /// Pointer.current는 에디터에서 Device Simulator의 가상 터치스크린이 차지해
    /// 마우스 입력이 무시될 수 있으므로, 장치별로 직접 확인한다.
    public static class PointerInput
    {
#if UNITY_EDITOR
        /// Device Simulator의 터치 시뮬레이션은 활성화되는 순간 Mouse 장치를 비활성화한다.
        /// 그러면 Simulator 탭이 열려 있는 동안 일반 Game 뷰의 마우스 입력이 죽으므로,
        /// 에디터에서는 마우스가 꺼져 있으면 다시 켜서 두 뷰가 모두 동작하게 한다.
        static void EnsureMouseUsable()
        {
            var mouse = Mouse.current;
            if (mouse != null && !mouse.enabled)
                InputSystem.EnableDevice(mouse);
        }
#endif

        /// 지금 눌려 있는 포인터가 있으면 스크린 좌표를 반환
        public static bool TryGetPressed(out Vector2 screenPos)
        {
#if UNITY_EDITOR
            EnsureMouseUsable();
#endif
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }

            var pen = Pen.current;
            if (pen != null && pen.tip.isPressed)
            {
                screenPos = pen.position.ReadValue();
                return true;
            }

            screenPos = default;
            return false;
        }

        /// 이번 프레임에 새로 눌린 포인터가 있는가 (탭 판정용)
        public static bool WasPressedThisFrame()
        {
#if UNITY_EDITOR
            EnsureMouseUsable();
#endif
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var pen = Pen.current;
            if (pen != null && pen.tip.wasPressedThisFrame) return true;

            return false;
        }
    }
}
