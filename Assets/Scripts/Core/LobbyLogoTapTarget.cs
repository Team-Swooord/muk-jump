using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 로고도 메뉴 버튼과 같은 UI 터치 경로를 쓴다. 투명 여백과 드래그는 제외한다.
    [DisallowMultipleComponent]
    public sealed class LobbyLogoTapTarget : MonoBehaviour, ICanvasRaycastFilter,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IDragHandler, IInitializePotentialDragHandler
    {
        Action tapped;
        int? pointerId;
        int? completedPointerId;
        Vector2 pressPosition;
        float pressedAt;
        float travel;

        public void Bind(Action onTapped)
        {
            tapped = onTapped;
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }

        public bool IsRaycastLocationValid(Vector2 position, Camera eventCamera) =>
            LobbyNightSkyView.IsInsideLogoBand(transform as RectTransform, position, eventCamera);

        public void OnPointerDown(PointerEventData data)
        {
            if (pointerId.HasValue || data.button != PointerEventData.InputButton.Left) return;
            pointerId = data.pointerId;
            completedPointerId = null;
            pressPosition = data.position;
            pressedAt = Time.unscaledTime;
            travel = 0f;
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

        public void OnDrag(PointerEventData data)
        {
            if (pointerId == data.pointerId)
                travel = Mathf.Max(travel, Vector2.Distance(pressPosition, data.position));
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointerId != data.pointerId) return;
            pointerId = null;
            travel = Mathf.Max(travel, Vector2.Distance(pressPosition, data.position));
            if (Time.unscaledTime - pressedAt <= .65f &&
                travel <= Mathf.Max(10f, Screen.width * .022f) &&
                IsRaycastLocationValid(data.position, data.pressEventCamera))
                completedPointerId = data.pointerId;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (completedPointerId != data.pointerId) return;
            completedPointerId = null;
            tapped?.Invoke();
        }

        public void CancelPress() => pointerId = completedPointerId = null;
        void OnDisable() => CancelPress();
    }
}
