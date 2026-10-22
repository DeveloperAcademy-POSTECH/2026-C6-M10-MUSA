using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace C6Lab.Tests
{
    /// <summary>Saved-layout pointer and synthetic Touchscreen regressions; these are not physical-device gesture tests.</summary>
    public sealed class GourdViewerGestureTests
    {
        private GameObject ownedUi, ownedEventSystem;
        private Touchscreen touchscreen;
        private EventSystem events;
        private InputSettings originalInputSettings, fixtureInputSettings;
        private HideFlags originalInputFlags;
        private readonly List<BaseInputModule> disabledModules = new List<BaseInputModule>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (ownedUi != null) Object.Destroy(ownedUi);
            if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            touchscreen = null;
            if (fixtureInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = originalInputFlags;
                Object.Destroy(fixtureInputSettings);
                fixtureInputSettings = originalInputSettings = null;
            }
            yield return null;
            foreach (BaseInputModule module in disabledModules) if (module != null) module.enabled = true;
            disabledModules.Clear();
        }

        [UnityTest]
        public IEnumerator RemainingPointerResumesWithANewBaselineAfterTheFirstPointerLifts()
        {
            GourdViewerGestureSurface gestures = CreateSurface(out RectTransform rectangle,
                out List<Vector2> rotation, out List<float> zoom);
            var a = Pointer(101, ScreenPoint(rectangle, .25f, .4f));
            var b = Pointer(202, ScreenPoint(rectangle, .7f, .65f));
            gestures.OnPointerDown(a);
            gestures.OnPointerDown(b);
            gestures.OnPointerUp(a);
            // B already moved while A was held. Handoff establishes a new baseline.
            b.position = ScreenPoint(rectangle, .6f, .55f);
            gestures.OnDrag(b);
            Assert.That(rotation, Is.Empty);
            b.position = ScreenPoint(rectangle, .7f, .6f);
            gestures.OnDrag(b);
            Assert.That(rotation.Count, Is.EqualTo(1));
            Assert.That(rotation[0].x, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(rotation[0].y, Is.EqualTo(.05f).Within(.0001f));

            var wheel = Pointer(303, ScreenPoint(rectangle, .5f, .5f));
            wheel.scrollDelta = Vector2.up;
            gestures.OnScroll(wheel);
            Assert.That(zoom, Is.Empty, "Wheel zoom must not overlap an owned drag.");
            gestures.OnPointerUp(b);
            wheel.position = ScreenPoint(rectangle, 2f, .5f);
            gestures.OnScroll(wheel);
            Assert.That(zoom, Is.Empty, "Wheel input outside the preview must not zoom.");
            wheel.position = ScreenPoint(rectangle, .5f, .5f);
            gestures.OnScroll(wheel);
            Assert.That(zoom.Count, Is.EqualTo(1));
            Assert.That(zoom[0], Is.GreaterThan(0f));
            Assert.That(rotation.Count, Is.EqualTo(1), "Wheel input must not rotate.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PinchUsesOwnedTouchIdsAcrossSlotsAndCancellationHandsOffWithoutRotationJump()
        {
            GourdViewerGestureSurface gestures = CreateSurface(out RectTransform rectangle,
                out List<Vector2> rotation, out List<float> zoom);
            CreateTouchscreen();
            Vector2 outside = ScreenPoint(rectangle, 2f, .5f);
            Vector2 a = ScreenPoint(rectangle, .3f, .5f), b = ScreenPoint(rectangle, .7f, .5f);
            // Reserve slot 0 first, so the eventual pinch uses slots 1 and 2.
            yield return ApplyTouches(Touch(10, InputTouchPhase.Began, outside),
                Touch(11, InputTouchPhase.Began, a), Touch(12, InputTouchPhase.Began, b));
            var first = TouchPointer(11, a);
            var second = TouchPointer(12, b);
            gestures.OnPointerDown(first);
            gestures.OnPointerDown(second);
            yield return ApplyTouches(Touch(10, InputTouchPhase.Ended, outside));
            Assert.That(touchscreen.touches[0].press.isPressed, Is.False);
            Assert.That(touchscreen.touches[1].press.isPressed && touchscreen.touches[2].press.isPressed, Is.True);
            Assert.That(zoom, Is.Empty, "The change to two fingers establishes a pinch baseline.");

            Vector2 nextA = ScreenPoint(rectangle, .2f, .5f), nextB = ScreenPoint(rectangle, .8f, .5f);
            yield return ApplyTouches(Touch(11, InputTouchPhase.Moved, nextA), Touch(12, InputTouchPhase.Moved, nextB));
            first.position = nextA; second.position = nextB;
            gestures.OnDrag(first); gestures.OnDrag(second);
            Assert.That(rotation, Is.Empty, "A pinch must never produce rotation callbacks.");
            Assert.That(zoom.Sum(), Is.EqualTo(Mathf.Log(1.5f) * 1.2f).Within(.001f));

            float beforeThird = zoom.Sum();
            yield return ApplyTouches(Touch(13, InputTouchPhase.Began, outside));
            yield return ApplyTouches(Touch(11, InputTouchPhase.Moved, a), Touch(12, InputTouchPhase.Moved, b));
            Assert.That(zoom.Sum(), Is.EqualTo(beforeThird), "Three fingers cannot keep zooming an old pair.");
            yield return ApplyTouches(Touch(13, InputTouchPhase.Ended, outside));
            Assert.That(zoom.Sum(), Is.EqualTo(beforeThird), "Returning to two fingers needs a new baseline.");

            // No UI up is sent: polling must also release a canceled native touch.
            yield return ApplyTouches(Touch(11, InputTouchPhase.Canceled, a));
            second.position = b;
            gestures.OnDrag(second);
            Assert.That(rotation, Is.Empty, "The remaining finger first establishes its own baseline.");
            second.position = ScreenPoint(rectangle, .75f, .55f);
            yield return ApplyTouches(Touch(12, InputTouchPhase.Moved, second.position));
            gestures.OnDrag(second);
            Assert.That(rotation.Count, Is.EqualTo(1));
            Assert.That(rotation[0].x, Is.EqualTo(.05f).Within(.0001f));
            Assert.That(rotation[0].y, Is.EqualTo(.05f).Within(.0001f));
        }

        [UnityTest]
        public IEnumerator OutsideTouchesAndHeldPointersCannotAcquireOwnershipAfterResetDisableOrFocusLoss()
        {
            GourdViewerGestureSurface gestures = CreateSurface(out RectTransform rectangle,
                out List<Vector2> rotation, out List<float> zoom);
            CreateTouchscreen();
            Vector2 inside = ScreenPoint(rectangle, .4f, .4f), outside = ScreenPoint(rectangle, 2f, .5f);
            yield return ApplyTouches(Touch(21, InputTouchPhase.Began, inside), Touch(22, InputTouchPhase.Began, outside));
            var first = TouchPointer(21, inside);
            var other = TouchPointer(22, outside);
            gestures.OnPointerDown(first); gestures.OnPointerDown(other);
            first.position = ScreenPoint(rectangle, .5f, .5f);
            other.position = ScreenPoint(rectangle, .8f, .5f);
            yield return ApplyTouches(Touch(21, InputTouchPhase.Moved, first.position), Touch(22, InputTouchPhase.Moved, other.position));
            gestures.OnDrag(first); gestures.OnDrag(other);
            Assert.That(rotation, Is.Empty);
            Assert.That(zoom, Is.Empty, "A touch starting outside cannot join a pinch by sliding into the image.");

            gestures.ResetGesture();
            yield return ApplyTouches(Touch(21, InputTouchPhase.Moved, inside), Touch(22, InputTouchPhase.Moved, outside));
            gestures.OnDrag(first); gestures.OnDrag(other);
            Assert.That(rotation, Is.Empty);
            Assert.That(zoom, Is.Empty, "A held pinch cannot reacquire ownership after reset.");
            yield return ApplyTouches(Touch(21, InputTouchPhase.Ended, inside), Touch(22, InputTouchPhase.Ended, outside));

            System.Action[] resets =
            {
                gestures.ResetGesture,
                () => gestures.enabled = false,
                () => gestures.SendMessage("OnApplicationFocus", false),
                () => gestures.SendMessage("OnApplicationPause", true)
            };
            for (int i = 0; i < resets.Length; i++)
            {
                int id = 30 + i;
                yield return ApplyTouches(Touch(id, InputTouchPhase.Began, inside));
                var fresh = TouchPointer(id, inside);
                gestures.OnPointerDown(fresh);
                fresh.position = ScreenPoint(rectangle, .5f, .45f);
                yield return ApplyTouches(Touch(id, InputTouchPhase.Moved, fresh.position));
                gestures.OnDrag(fresh);
                Assert.That(rotation.Count, Is.EqualTo(i + 1), "Each fresh gesture must work before its own reset path.");
                Assert.That(rotation[i].x, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(rotation[i].y, Is.EqualTo(.05f).Within(.0001f));
                resets[i]();
                if (!gestures.enabled)
                {
                    gestures.OnPointerDown(fresh); // Disabled handlers cannot capture even a fresh-looking down.
                    gestures.enabled = true;
                }
                fresh.position = ScreenPoint(rectangle, .6f, .55f);
                yield return ApplyTouches(Touch(id, InputTouchPhase.Moved, fresh.position));
                gestures.OnDrag(fresh);
                Assert.That(rotation.Count, Is.EqualTo(i + 1), "Reset/disable/focus/pause must each discard an active baseline.");
                yield return ApplyTouches(Touch(id, InputTouchPhase.Ended, fresh.position));
            }
        }

        private GourdViewerGestureSurface CreateSurface(out RectTransform rectangle,
            out List<Vector2> rotation, out List<float> zoom)
        {
            // Drive the handlers ourselves; prevent a Scene's module from dispatching the same synthetic touches too.
            foreach (BaseInputModule module in Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None))
                if (module.enabled) { disabledModules.Add(module); module.enabled = false; }
            GameObject prefab = Resources.Load<GameObject>(LabGourdResultViewer.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            ownedUi = Object.Instantiate(prefab);
            GourdResultViewerUI ui = ownedUi.GetComponent<GourdResultViewerUI>();
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.HasRequiredReferences, Is.True);
            rotation = new List<Vector2>(); zoom = new List<float>();
            ui.Bind(() => { }, () => { }, () => { }, rotation.Add, zoom.Add);
            ui.viewerPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(ui.previewImage.rectTransform);
            Canvas.ForceUpdateCanvases();
            rectangle = ui.previewImage.rectTransform;
            Assert.That(rectangle.rect.width, Is.GreaterThan(1f));
            Assert.That(rectangle.rect.height, Is.GreaterThan(1f));
            events = EventSystem.current;
            if (events == null)
            {
                ownedEventSystem = new GameObject("Viewer gesture test EventSystem", typeof(EventSystem));
                events = ownedEventSystem.GetComponent<EventSystem>();
            }
            return ui.previewImage.GetComponent<GourdViewerGestureSurface>();
        }

        private void CreateTouchscreen()
        {
            // Match the existing synthetic-pointer fixture: a batch Editor has no focused GameView,
            // so its default policy can consume Pointer events before they reach the test device.
            originalInputSettings = InputSystem.settings;
            originalInputFlags = originalInputSettings.hideFlags;
            originalInputSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            fixtureInputSettings = Object.Instantiate(originalInputSettings);
            fixtureInputSettings.hideFlags = HideFlags.HideAndDontSave;
            fixtureInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            fixtureInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = fixtureInputSettings;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
        }

        private PointerEventData Pointer(int id, Vector2 position)
            => new PointerEventData(events) { pointerId = id, position = position };
        private ExtendedPointerEventData TouchPointer(int id, Vector2 position)
            => new ExtendedPointerEventData(events)
            {
                device = touchscreen, touchId = id, pointerType = UIPointerType.Touch,
                pointerId = 100000 + id, position = position
            };
        private static TouchState Touch(int id, InputTouchPhase phase, Vector2 position)
            => new TouchState { touchId = id, phase = phase, position = position };
        private IEnumerator ApplyTouches(params TouchState[] touches)
        {
            foreach (TouchState touch in touches) InputSystem.QueueStateEvent(touchscreen, touch);
            yield return null;
            yield return null;
            foreach (TouchState expected in touches)
            {
                var actual = touchscreen.touches.FirstOrDefault(touch => touch.touchId.ReadValue() == expected.touchId);
                bool pressed = expected.phase == InputTouchPhase.Began || expected.phase == InputTouchPhase.Moved
                    || expected.phase == InputTouchPhase.Stationary;
                Assert.That(actual != null && actual.press.isPressed, Is.EqualTo(pressed),
                    "The synthetic device must receive touch " + expected.touchId + " / " + expected.phase + " before checking gestures.");
            }
        }
        private static Vector2 ScreenPoint(RectTransform rectangle, float x, float y)
        {
            Rect local = rectangle.rect;
            Vector3 world = rectangle.TransformPoint(new Vector3(local.xMin + local.width * x,
                local.yMin + local.height * y, 0f));
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }
    }
}
