using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hapio
{
    /// <summary>Input is local to the result preview and never changes the completed shared gourd.</summary>
    public sealed class GamePreviewInput : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private float previousPinch;
        public void OnDrag(PointerEventData eventData)
        {
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 1
                && Touchscreen.current.touches[0].press.isPressed && Touchscreen.current.touches[1].press.isPressed) return;
            GameSession.Instance?.RotatePreview(eventData.delta);
        }
        public void OnScroll(PointerEventData eventData) => GameSession.Instance?.ZoomPreview(eventData.scrollDelta.y);
        private void Update()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null || touch.touches.Count < 2 || !touch.touches[0].press.isPressed || !touch.touches[1].press.isPressed)
            { previousPinch = 0f; return; }
            float distance = Vector2.Distance(touch.touches[0].position.ReadValue(), touch.touches[1].position.ReadValue());
            if (previousPinch > 0f) GameSession.Instance?.ZoomPreview((distance - previousPinch) * .02f);
            previousPinch = distance;
        }
    }
}
