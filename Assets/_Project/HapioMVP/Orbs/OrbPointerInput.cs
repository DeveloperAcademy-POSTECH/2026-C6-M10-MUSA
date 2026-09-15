using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace C6.Prototype.Orbs
{
    [DisallowMultipleComponent]
    public sealed class OrbPointerInput : MonoBehaviour
    {
        public const int MousePointerId = int.MinValue;
        [SerializeField] private MonoBehaviour controller;
        private IOrbPointerSink Sink => controller as IOrbPointerSink;
        private readonly HashSet<int> activeTouchIds = new HashSet<int>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private bool hooked;
        private int latestTouchFrame = -1;
        public int TouchSampleCount { get; private set; }
        public int MouseSampleCount { get; private set; }
        public int TouchCancelCount { get; private set; }
        public int LastTouchId { get; private set; }
        public int LastTouchDeviceId { get; private set; }

        public void Configure(IOrbPointerSink owner) => controller = owner as MonoBehaviour;
        private void Awake()
        {
            if (Sink != null) return;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IOrbPointerSink) { controller = component; break; }
        }
        public bool IsActiveTouchPointer(int pointerId) => activeTouchIds.Contains(pointerId);
        private void OnEnable()
        {
            if (hooked) return;
            // Input System 1.20 uses a reference count; our Disable balances only our Enable.
            EnhancedTouchSupport.Enable();
            EnhancedTouch.onFingerDown += FingerDown;
            EnhancedTouch.onFingerMove += FingerMove;
            EnhancedTouch.onFingerUp += FingerUp;
            InputSystem.onDeviceChange += DeviceChanged;
            hooked = true;
        }

        private void FingerDown(Finger finger)
        {
            var touch = finger.currentTouch;
            if (!touch.valid || Sink == null) return;
            Track(finger, touch.touchId);
            activeTouchIds.Add(touch.touchId);
            Sink.CancelPointer(MousePointerId);
            Sink.BeginPointer(touch.touchId, touch.screenPosition, StartedOverUi(touch.touchId, touch.screenPosition));
        }
        private void FingerMove(Finger finger)
        {
            var touch = finger.currentTouch;
            if (!touch.valid || Sink == null) return;
            Track(finger, touch.touchId);
            Sink.MovePointer(touch.touchId, touch.screenPosition);
        }
        private void FingerUp(Finger finger)
        {
            var touch = finger.currentTouch;
            if (!touch.valid || Sink == null) return;
            Track(finger, touch.touchId);
            activeTouchIds.Remove(touch.touchId);
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                TouchCancelCount++;
                Sink.CancelPointer(touch.touchId);
            }
            else Sink.EndPointer(touch.touchId, touch.screenPosition);
        }
        private void Track(Finger finger, int touchId)
        {
            TouchSampleCount++;
            LastTouchId = touchId;
            LastTouchDeviceId = finger.screen.deviceId;
            latestTouchFrame = Time.frameCount;
        }

        private void Update()
        {
            if (Sink == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            // A touch and any mouse-like event synthesized for it cannot control two paths.
            if (activeTouchIds.Count > 0 || latestTouchFrame == Time.frameCount)
            {
                Sink.CancelPointer(MousePointerId);
                return;
            }
            var point = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                MouseSampleCount++;
                Sink.BeginPointer(MousePointerId, point, StartedOverUi(MousePointerId, point));
            }
            if (mouse.leftButton.isPressed)
            {
                MouseSampleCount++;
                Sink.MovePointer(MousePointerId, point);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                MouseSampleCount++;
                Sink.EndPointer(MousePointerId, point);
            }
        }

        private bool StartedOverUi(int pointerId, Vector2 point)
        {
            if (EventSystem.current == null) return false;
            // Query the current position directly; IsPointerOverGameObject can reflect the prior UI update.
            var data = new PointerEventData(EventSystem.current) { pointerId = pointerId, position = point };
            uiHits.Clear();
            EventSystem.current.RaycastAll(data, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Mouse) && !(device is Touchscreen)) return;
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Disabled || change == InputDeviceChange.SoftReset || change == InputDeviceChange.HardReset)
            {
                activeTouchIds.Clear();
                if (Sink != null) Sink.CancelInteractions("Input device reset or removed");
            }
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { activeTouchIds.Clear(); if (Sink != null) Sink.CancelInteractions("Pointer focus lost"); }
        }
        private void OnDisable()
        {
            if (!hooked) return;
            EnhancedTouch.onFingerDown -= FingerDown;
            EnhancedTouch.onFingerMove -= FingerMove;
            EnhancedTouch.onFingerUp -= FingerUp;
            InputSystem.onDeviceChange -= DeviceChanged;
            EnhancedTouchSupport.Disable();
            hooked = false;
            activeTouchIds.Clear();
            if (Sink != null) Sink.CancelInteractions("Pointer adapter disabled");
        }
    }
}
