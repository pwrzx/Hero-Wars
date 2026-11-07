using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerTrials
{
    // The EventSystem uses the project's existing Input System UI actions for touch and mouse.
    public sealed class SwipeAttackInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public TowerGameController controller;
        private Vector2 start;
        private int? pointerId;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId.HasValue || eventData.button != PointerEventData.InputButton.Left) return;
            pointerId = eventData.pointerId;
            start = eventData.position;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) controller.PreviewSwipe(start, eventData.position);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            pointerId = null;
            controller.SubmitSwipe(start, eventData.position);
        }
        private void OnDisable() => pointerId = null;
        private void OnApplicationFocus(bool focused) { if (!focused) pointerId = null; }
    }
}
