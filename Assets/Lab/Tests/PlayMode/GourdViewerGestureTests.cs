using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// Pointer-event regression on the saved UI layout. This does not synthesize a hardware
    /// Touchscreen pinch or establish device gesture support; it checks pointer handoff only.
    /// </summary>
    public sealed class GourdViewerGestureTests
    {
        private GameObject ownedUi, ownedEventSystem;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (ownedUi != null) Object.Destroy(ownedUi);
            if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemainingPointerResumesWithANewBaselineAfterTheFirstPointerLifts()
        {
            GameObject prefab = Resources.Load<GameObject>(LabGourdResultViewer.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            ownedUi = Object.Instantiate(prefab);
            GourdResultViewerUI ui = ownedUi.GetComponent<GourdResultViewerUI>();
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.HasRequiredReferences, Is.True);
            var rotation = new List<Vector2>();
            var zoom = new List<float>();
            ui.Bind(() => { }, () => { }, () => { }, rotation.Add, zoom.Add);
            ui.viewerPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(ui.previewImage.rectTransform);
            Canvas.ForceUpdateCanvases();
            RectTransform rectangle = ui.previewImage.rectTransform;
            Assert.That(rectangle.rect.width, Is.GreaterThan(1f));
            Assert.That(rectangle.rect.height, Is.GreaterThan(1f));
            GourdViewerGestureSurface gestures = ui.previewImage.GetComponent<GourdViewerGestureSurface>();
            Assert.That(gestures, Is.Not.Null);
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                ownedEventSystem = new GameObject("Viewer gesture test EventSystem", typeof(EventSystem));
                events = ownedEventSystem.GetComponent<EventSystem>();
            }

            Vector2 aStart = ScreenPoint(rectangle, .25f, .4f);
            Vector2 bStart = ScreenPoint(rectangle, .7f, .65f);
            var a = new PointerEventData(events) { pointerId = 101, position = aStart };
            var b = new PointerEventData(events) { pointerId = 202, position = bStart };
            gestures.OnPointerDown(a);
            gestures.OnPointerDown(b);
            gestures.OnPointerUp(a);
            // B already moved while A was held. Its first drag must establish a new baseline,
            // rather than jumping from A's last position or being ignored until another down.
            b.position = ScreenPoint(rectangle, .6f, .55f);
            gestures.OnDrag(b);
            Assert.That(rotation, Is.Empty, "A pointer handoff must not rotate by the old pointer's gap.");
            b.position = ScreenPoint(rectangle, .7f, .6f);
            gestures.OnDrag(b);
            Assert.That(rotation.Count, Is.EqualTo(1), "The remaining finger must resume rotation on its next move.");
            Assert.That(float.IsNaN(rotation[0].x) || float.IsInfinity(rotation[0].x)
                || float.IsNaN(rotation[0].y) || float.IsInfinity(rotation[0].y), Is.False);
            Assert.That(rotation[0].x, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(rotation[0].y, Is.EqualTo(.05f).Within(.0001f));

            var wheel = new PointerEventData(events)
            {
                position = ScreenPoint(rectangle, 2f, .5f), scrollDelta = Vector2.up
            };
            gestures.OnScroll(wheel);
            Assert.That(zoom, Is.Empty, "Wheel input outside the preview must not change its zoom.");
            wheel.position = ScreenPoint(rectangle, .5f, .5f);
            gestures.OnScroll(wheel);
            Assert.That(zoom.Count, Is.EqualTo(1));
            Assert.That(zoom[0], Is.GreaterThan(0f));
            Assert.That(rotation.Count, Is.EqualTo(1), "Wheel input must not create a rotation callback.");
            gestures.OnPointerUp(b);
            yield return null;
        }

        private static Vector2 ScreenPoint(RectTransform rectangle, float x, float y)
        {
            Rect local = rectangle.rect;
            Vector3 world = rectangle.TransformPoint(new Vector3(local.xMin + local.width * x,
                local.yMin + local.height * y, 0f));
            // The saved viewer prefab uses an overlay Canvas, so its event camera is null.
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }
    }
}
