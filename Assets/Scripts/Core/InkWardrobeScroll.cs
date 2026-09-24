using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 손가락을 그대로 따라가는 카드 줄. 놓은 뒤에만 관성과 중앙 안착을 적용한다.
    public sealed class InkWardrobeScroll : ScrollRect, IPointerDownHandler, IPointerUpHandler
    {
        public InkWardrobeView Owner { get; set; }
        public float CardStride { get; set; } = 264f;
        public bool IsSettling => snapping || pendingSelection;
        public bool IsDragging => dragEvent != null;
        PointerEventData dragEvent;
        int pointerId;
        int tapPointerId = int.MinValue;
        uint inactiveGeneration;
        bool snapping, pendingSelection;
        int targetIndex;
        float snapVelocity;

        bool CanMove => isActiveAndEnabled && Owner != null && Owner.CanBrowse;
        bool Reduced => LobbySettingsProfile.ReducedMotionEnabled;

        public override void OnInitializePotentialDrag(PointerEventData e)
        {
            DropStaleDrag();
            if (!CanMove || dragEvent != null || e.button != PointerEventData.InputButton.Left) return;
            CancelMotion();
            tapPointerId = e.pointerId;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            base.OnInitializePotentialDrag(e);
            UpdatePrevData();
        }
        public void OnPointerDown(PointerEventData e) => OnInitializePotentialDrag(e);
        public void OnPointerUp(PointerEventData e)
        {
            DropStaleDrag();
            if (!CanMove || !horizontal || dragEvent != null || tapPointerId != e.pointerId) return;
            // 카드 사이 빈 곳을 가볍게 잡았다 놓아도 중간 위치에 멈춰 남지 않는다.
            pendingSelection = true;
            if (Reduced) SnapNearest(true);
        }
        public override void OnBeginDrag(PointerEventData e)
        {
            DropStaleDrag();
            if (!CanMove || dragEvent != null || e.button != PointerEventData.InputButton.Left) return;
            CancelMotion();
            dragEvent = e;
            pointerId = e.pointerId;
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            inertia = !Reduced;
            base.OnBeginDrag(e);
        }
        public override void OnDrag(PointerEventData e)
        {
            DropStaleDrag();
            if (!CanMove || dragEvent == null || pointerId != e.pointerId) return;
            base.OnDrag(e);
            Owner.RefreshCardDepth();
        }
        public override void OnEndDrag(PointerEventData e)
        {
            DropStaleDrag();
            if (dragEvent == null || pointerId != e.pointerId) return;
            base.OnEndDrag(e);
            dragEvent = null;
            pendingSelection = CanMove && horizontal;
            if (pendingSelection && Reduced) SnapNearest(true);
        }
        public override void OnScroll(PointerEventData e)
        {
            DropStaleDrag();
            if (!CanMove || dragEvent != null) return;
            CancelMotion();
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            base.OnScroll(e);
            pendingSelection = horizontal;
            Owner.RefreshCardDepth();
        }
        public void CenterOn(int index, bool immediate)
        {
            CancelMotion();
            targetIndex = Mathf.Clamp(index, 0, InkWardrobeEntry.Count - 1);
            inactiveGeneration = MobileApplicationLifecycle.InactiveGeneration;
            if (immediate || Reduced || !Application.isPlaying)
            {
                SetContentAnchoredPosition(new Vector2(-targetIndex * CardStride, 0));
                Owner?.RefreshCardDepth();
            }
            else snapping = true;
        }
        public void CancelMotion()
        {
            if (dragEvent != null) base.OnEndDrag(dragEvent);
            dragEvent = null;
            tapPointerId = int.MinValue;
            snapping = pendingSelection = false;
            snapVelocity = 0;
            StopMovement();
        }
        void DropStaleDrag()
        {
            if (inactiveGeneration != MobileApplicationLifecycle.InactiveGeneration) CancelMotion();
        }
        void SnapNearest(bool immediate)
        {
            targetIndex = Mathf.Clamp(Mathf.RoundToInt(-content.anchoredPosition.x / CardStride), 0, InkWardrobeEntry.Count - 1);
            StopMovement();
            snapVelocity = 0;
            snapping = true;
            if (immediate) FinishSnap();
        }
        void FinishSnap()
        {
            SetContentAnchoredPosition(new Vector2(-targetIndex * CardStride, 0));
            bool select = pendingSelection;
            snapping = pendingSelection = false;
            snapVelocity = 0;
            StopMovement();
            if (select) Owner.SelectFromCarousel(targetIndex);
            Owner?.RefreshCardDepth();
        }
        protected override void LateUpdate()
        {
            if (!CanMove) { CancelMotion(); return; }
            DropStaleDrag();
            inertia = !Reduced;
            base.LateUpdate();
            if (horizontal && dragEvent == null)
            {
                // 빠른 플릭은 여러 장을 지나가고, 충분히 감속한 뒤 가장 가까운 카드에 안착한다.
                if (pendingSelection && !snapping && (Reduced || Mathf.Abs(velocity.x) < 150f)) SnapNearest(Reduced);
                if (snapping)
                {
                    if (Reduced) FinishSnap();
                    else
                    {
                        float target = -targetIndex * CardStride;
                        float x = Mathf.SmoothDamp(content.anchoredPosition.x, target, ref snapVelocity,
                            .14f, Mathf.Infinity, Mathf.Min(Time.unscaledDeltaTime, .05f));
                        SetContentAnchoredPosition(new Vector2(x, 0));
                        if (Mathf.Abs(x - target) < .35f && Mathf.Abs(snapVelocity) < 6f) FinishSnap();
                    }
                }
            }
            Owner.RefreshCardDepth();
        }
        protected override void OnDisable() { CancelMotion(); base.OnDisable(); }
    }
}
