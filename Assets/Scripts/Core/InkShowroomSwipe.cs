using UnityEngine;
using UnityEngine.EventSystems;

namespace MukJump.Core
{
    /// 중앙 무대의 가로 드래그만 넘기기로 해석한다. 버튼의 일반 탭은 그대로 유지한다.
    public sealed class InkShowroomSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public InkWardrobeView Owner { get; set; }
        int pointerId;
        Vector2 origin;
        bool dragging;
        uint inactiveGeneration;
        public void OnBeginDrag(PointerEventData e)
        {
            if (dragging && inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) Cancel();
            if (e.button != PointerEventData.InputButton.Left || dragging || Owner == null || !Owner.CanBrowse) return;
            pointerId = e.pointerId; origin = e.pressPosition; dragging = true;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
        }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging || pointerId != e.pointerId) return;
            dragging = false;
            if (inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration || Owner == null || !Owner.CanBrowse) return;
            Vector2 delta = e.position - origin;
            if (Mathf.Abs(delta.x) < Mathf.Max(36, Screen.width * .07f) || Mathf.Abs(delta.x) < Mathf.Abs(delta.y) * 1.3f) return;
            Owner?.StepSelection(delta.x < 0 ? 1 : -1);
        }
        public void Cancel() => dragging = false;
        void OnDisable() => Cancel();
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
