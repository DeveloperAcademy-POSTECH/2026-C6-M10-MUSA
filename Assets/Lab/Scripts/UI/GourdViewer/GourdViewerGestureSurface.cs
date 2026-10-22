using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace C6Lab
{
    /// <summary>Owns pointers that start in the preview: one rotates, exactly two touches pinch.</summary>
    [DisallowMultipleComponent]
    public sealed class GourdViewerGestureSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IScrollHandler, IEndDragHandler
    {
        private sealed class PointerState
        {
            public Touchscreen screen;
            public int touchId;
            public Camera camera;
            public Vector2 previousLocal;
            public bool hasLocal;
        }

        private Action<Vector2> rotate;
        private Action<float> zoom;
        private RectTransform rectangle;
        private readonly Dictionary<int, PointerState> pointers = new Dictionary<int, PointerState>();
        private readonly List<int> releasedPointers = new List<int>();
        private int pressedTouchCount;
        private float pinchDistance;

        public void Bind(Action<Vector2> onRotate, Action<float> onZoom)
        {
            rotate = onRotate; zoom = onZoom; rectangle = transform as RectTransform;
            ResetGesture();
        }

        public void ResetGesture()
        {
            pointers.Clear(); releasedPointers.Clear(); pressedTouchCount = 0; pinchDistance = 0f;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!isActiveAndEnabled || rectangle == null || data.button != PointerEventData.InputButton.Left
                || pointers.ContainsKey(data.pointerId)
                || !RectTransformUtility.RectangleContainsScreenPoint(rectangle, data.position, data.pressEventCamera)) return;
            var extended = data as ExtendedPointerEventData;
            var state = new PointerState
            {
                screen = extended != null ? extended.device as Touchscreen : null,
                touchId = extended != null ? extended.touchId : 0,
                camera = data.pressEventCamera
            };
            // A simulated mouse must not become a second owner of a physical touch gesture.
            if (state.screen == null && CountPressedTouches(Touchscreen.current) > 0) return;
            if (pointers.Count > 0) InvalidateBaselines();
            state.hasLocal = RectTransformUtility.ScreenPointToLocalPointInRectangle(rectangle,
                data.position, state.camera, out state.previousLocal);
            pointers.Add(data.pointerId, state);
            pressedTouchCount = CountPressedTouches(state.screen);
            if (pointers.Count != 1 || pressedTouchCount > 1) InvalidateBaselines();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointers.Remove(data.pointerId)) InvalidateBaselines();
        }

        public void OnDrag(PointerEventData data)
        {
            if (!isActiveAndEnabled || rectangle == null) return;
            RefreshTouches();
            // A drag without a down on this surface (including a held touch after reset/reopen)
            // never acquires ownership. A remaining owned finger receives a fresh baseline.
            if (!pointers.TryGetValue(data.pointerId, out PointerState state)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectangle,
                    data.position, state.camera, out Vector2 current)) return;
            if (pointers.Count == 1 && pressedTouchCount <= 1 && state.hasLocal)
            {
                Vector2 delta = current - state.previousLocal;
                rotate?.Invoke(new Vector2(delta.x / Mathf.Max(1f, rectangle.rect.width),
                    delta.y / Mathf.Max(1f, rectangle.rect.height)));
            }
            state.previousLocal = current;
            state.hasLocal = pointers.Count == 1 && pressedTouchCount <= 1;
        }

        public void OnScroll(PointerEventData data)
        {
            if (isActiveAndEnabled && rectangle != null && pointers.Count == 0
                && CountPressedTouches(Touchscreen.current) == 0
                && RectTransformUtility.RectangleContainsScreenPoint(rectangle,
                    data.position, data.enterEventCamera)) zoom?.Invoke(data.scrollDelta.y * .08f);
        }

        private static int CountPressedTouches(Touchscreen screen)
        {
            if (screen == null || !screen.added) return 0;
            int count = 0;
            foreach (TouchControl touch in screen.touches) if (touch.press.isPressed) count++;
            return count;
        }

        private static TouchControl FindPressedTouch(PointerState pointer)
        {
            if (pointer.screen == null || !pointer.screen.added || pointer.touchId == 0) return null;
            foreach (TouchControl touch in pointer.screen.touches)
                if (touch.press.isPressed && touch.touchId.ReadValue() == pointer.touchId) return touch;
            return null;
        }

        private void InvalidateBaselines()
        {
            pinchDistance = 0f;
            foreach (PointerState state in pointers.Values) state.hasLocal = false;
        }

        private void RefreshTouches()
        {
            Touchscreen screen = null;
            releasedPointers.Clear();
            foreach (var pair in pointers)
            {
                PointerState state = pair.Value;
                if (state.screen == null) continue;
                screen = state.screen;
                // Canceled touches and recycled slots may disappear before a UI up event arrives.
                if (FindPressedTouch(state) == null) releasedPointers.Add(pair.Key);
            }
            foreach (int id in releasedPointers) pointers.Remove(id);
            int count = CountPressedTouches(screen ?? Touchscreen.current);
            if (releasedPointers.Count > 0 || count != pressedTouchCount) InvalidateBaselines();
            pressedTouchCount = count;
        }

        private void Update()
        {
            if (rectangle == null) return;
            RefreshTouches();
            if (pointers.Count != 2 || pressedTouchCount != 2) { pinchDistance = 0f; return; }
            TouchControl first = null, second = null;
            Touchscreen screen = null;
            foreach (PointerState state in pointers.Values)
            {
                TouchControl touch = FindPressedTouch(state);
                if (touch == null || (screen != null && state.screen != screen)) { pinchDistance = 0f; return; }
                screen = state.screen;
                if (first == null) first = touch; else second = touch;
            }
            float distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
            if (pinchDistance > 1f && distance > 1f && distance != pinchDistance)
                zoom?.Invoke(Mathf.Log(distance / pinchDistance) * 1.2f);
            pinchDistance = distance;
        }

        public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
        private void OnApplicationFocus(bool focused) { if (!focused) ResetGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetGesture(); }
        private void OnDisable() => ResetGesture();
    }
}
