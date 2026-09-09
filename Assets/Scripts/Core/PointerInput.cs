using UnityEngine;
using UnityEngine.InputSystem;

namespace MukJump.Core
{
    /// 터치·마우스·펜을 모두 지원하는 포인터 입력 헬퍼.
    /// Pointer.current는 에디터에서 Device Simulator의 가상 터치스크린이 차지해
    /// 마우스 입력이 무시될 수 있으므로, 장치별로 직접 확인한다.
    public static class PointerInput
    {
        static bool suppressedUntilRelease;

        /// Device Simulator의 터치 시뮬레이션은 활성화되는 순간 Mouse 장치를 비활성화한다.
        /// 그러면 Simulator 탭이 열려 있는 동안 일반 Game 뷰의 마우스 입력이 죽으므로,
        /// 에디터에서는 마우스가 꺼져 있으면 다시 켜서 두 뷰가 모두 동작하게 한다.
        public static void EnsureUiDevicesUsable()
        {
#if UNITY_EDITOR
            var mouse = Mouse.current;
            if (mouse != null && !mouse.enabled)
                InputSystem.EnableDevice(mouse);
#endif
        }

        /// 지금 눌려 있는 포인터가 있으면 스크린 좌표를 반환
        public static bool TryGetPressed(out Vector2 screenPos)
        {
            EnsureUiDevicesUsable();
            if (suppressedUntilRelease)
            {
                if (!IsAnyPressed()) suppressedUntilRelease = false;
                screenPos = default;
                return false;
            }

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

        /// 해제 프레임의 최종 좌표도 획에 포함한다. 취소·입력 억제는 새 끝점을 만들지 않는다.
        public static bool TryGetReleased(out Vector2 screenPos)
        {
            screenPos = default;
            if (suppressedUntilRelease) return false;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame &&
                touch.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
            var pen = Pen.current;
            if (pen != null && pen.tip.wasReleasedThisFrame)
            {
                screenPos = pen.position.ReadValue();
                return true;
            }
            return false;
        }

        /// 이번 프레임에 새로 눌린 포인터가 있는가 (탭 판정용)
        public static bool WasPressedThisFrame()
        {
            EnsureUiDevicesUsable();
            if (suppressedUntilRelease)
            {
                if (!IsAnyPressed()) suppressedUntilRelease = false;
                return false;
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var pen = Pen.current;
            if (pen != null && pen.tip.wasPressedThisFrame) return true;

            return false;
        }

        /// 화면 공통 터치 연출용. 게임 입력 억제 여부와 무관하게 새로 누른
        /// 위치만 읽으며, 드래그/길게 누르기는 반복하지 않는다. 입력은 소비하지 않는다.
        public static int CollectVisualPressStarts(Vector2[] positions)
        {
            if (positions == null || positions.Length == 0) return 0;
            EnsureUiDevicesUsable();
            int count = 0;
            bool touchInUse = false;
            var screen = Touchscreen.current;
            if (screen != null)
            {
                for (int i = 0; i < screen.touches.Count; i++)
                {
                    var touch = screen.touches[i];
                    bool began = touch.press.wasPressedThisFrame;
                    touchInUse |= began || touch.press.isPressed || touch.press.wasReleasedThisFrame;
                    if (began && count < positions.Length)
                        positions[count++] = touch.position.ReadValue();
                }
            }
            // 모바일/Device Simulator가 같은 터치를 마우스로도 전달하는 경우 중복 방지.
            if (touchInUse) return count;

            var pen = Pen.current;
            if (pen != null && (pen.tip.isPressed || pen.tip.wasPressedThisFrame || pen.tip.wasReleasedThisFrame))
            {
                if (pen.tip.wasPressedThisFrame) positions[count++] = pen.position.ReadValue();
                return count;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                positions[count++] = mouse.position.ReadValue();
            return count;
        }

        /// 로비 시작/재시작에 사용한 터치가 다음 상태의 드로잉 입력으로 이어지지 않게 한다.
        /// 호출 시점부터 모든 포인터가 한 번 놓일 때까지 입력을 소비한다.
        public static void SuppressUntilRelease()
        {
            suppressedUntilRelease = true;
        }

#if UNITY_EDITOR
        public static void ResetSuppressionForTests()
        {
            suppressedUntilRelease = false;
        }
#endif

        static bool IsAnyPressed()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;

            var pen = Pen.current;
            return pen != null && pen.tip.isPressed;
        }
    }
}
