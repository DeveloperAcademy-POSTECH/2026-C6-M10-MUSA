using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace C6Lab.Experiments
{
    /// <summary>Pointer drag/wheel and two-finger pinch restricted to the preview rectangle.</summary>
    public sealed class GourdViewerGestureSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IScrollHandler, IEndDragHandler
    {
        private Action<Vector2> rotate;
        private Action<float> zoom;
        private RectTransform rectangle;
        private int pointer = int.MinValue;
        private Vector2 previousLocal;
        private bool hasLocal, pinching, suppressNextDrag;
        private int pinchFirst, pinchSecond;
        private float pinchDistance;
        private Camera eventCamera;
        public void Bind(Action<Vector2> onRotate, Action<float> onZoom)
        {
            rotate = onRotate; zoom = onZoom; rectangle = transform as RectTransform;
        }
        public void ResetGesture()
        {
            pointer = int.MinValue; hasLocal = pinching = suppressNextDrag = false; pinchDistance = 0f;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (pointer != int.MinValue) return;
            pointer = data.pointerId; eventCamera = data.pressEventCamera;
            hasLocal = RectTransformUtility.ScreenPointToLocalPointInRectangle(rectangle, data.position,
                eventCamera, out previousLocal);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == pointer) { pointer = int.MinValue; hasLocal = false; }
        }
        public void OnDrag(PointerEventData data)
        {
            if (rectangle == null) return;
            // The first finger may lift while a second one already owns a drag event.
            // Adopt that remaining pointer without applying its accumulated pinch motion.
            if (pointer == int.MinValue) { pointer = data.pointerId; hasLocal = false; }
            if (data.pointerId != pointer) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectangle, data.position,
                data.pressEventCamera, out Vector2 current)) return;
            bool multiTouch = PressedTouchCount() > 1;
            if (!pinching && !multiTouch && !suppressNextDrag && hasLocal)
            {
                Vector2 delta = current - previousLocal;
                rotate?.Invoke(new Vector2(delta.x / Mathf.Max(1f, rectangle.rect.width),
                    delta.y / Mathf.Max(1f, rectangle.rect.height)));
            }
            previousLocal = current; hasLocal = true; suppressNextDrag = multiTouch;
        }
        public void OnScroll(PointerEventData data)
        {
            if (rectangle != null && RectTransformUtility.RectangleContainsScreenPoint(rectangle,
                data.position, data.enterEventCamera)) zoom?.Invoke(data.scrollDelta.y * .08f);
        }
        private static int PressedTouchCount()
        {
            if (Touchscreen.current == null) return 0;
            int count = 0;
            foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) count++;
            return count;
        }
        private void Update()
        {
            if (rectangle == null || Touchscreen.current == null) return;
            int count = 0, first = 0, second = 0;
            Vector2 a = default, b = default;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rectangle,
                    touch.startPosition.ReadValue(), eventCamera)) { count = 3; break; }
                count++;
                if (count == 1) { first = touch.touchId.ReadValue(); a = touch.position.ReadValue(); }
                if (count == 2) { second = touch.touchId.ReadValue(); b = touch.position.ReadValue(); }
            }
            if (count != 2)
            {
                if (pinching) suppressNextDrag = true;
                pinching = false; pinchDistance = 0f;
                return;
            }
            float distance = Vector2.Distance(a, b);
            if (pinching && first == pinchFirst && second == pinchSecond && pinchDistance > 1f)
                zoom?.Invoke(Mathf.Log(Mathf.Max(1f, distance) / pinchDistance) * 1.2f);
            pinching = true; suppressNextDrag = true;
            pinchFirst = first; pinchSecond = second; pinchDistance = distance;
        }
        public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
        private void OnApplicationFocus(bool focused) { if (!focused) ResetGesture(); }
        private void OnDisable() => ResetGesture();
    }
}
