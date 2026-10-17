using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>The gourd's "!" bubble on the battle view when it drops a bomb (#177).</summary>
    public sealed class LabGourdAlertTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheAlertPopsAnExclamationBubbleBesideTheGourdFollowsItAndLeavesByItself()
        {
            root = new GameObject("Gourd alert test");
            var cameraObject = new GameObject("Battle camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 1f, -8f);
            var battleCamera = cameraObject.AddComponent<Camera>();
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            LabTarget gourd = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 1f, 0f), .8f, 2f);
            var alert = root.AddComponent<LabGourdAlert>();
            alert.Bind(null, canvas, battleCamera);
            yield return null;
            Assert.That(alert.Active, Is.False);

            alert.Show(gourd);
            Assert.That(alert.Active, Is.True);
            Assert.That(alert.Visible, Is.True, "The gourd is on screen, so the bubble shows.");
            Assert.That(alert.Label, Is.EqualTo("!"));
            Assert.That(alert.Bubble.GetComponent<Image>().raycastTarget, Is.False, "It never blocks a touch.");
            Assert.That(alert.Bubble.GetComponentInChildren<Text>().raycastTarget, Is.False);
            // Its tail sits on the gourd's upper right as seen from this camera, so the bubble opens beside it.
            Vector3 gourdOnScreen = battleCamera.WorldToScreenPoint(gourd.Hitbox.bounds.center);
            Vector2 tail = RectTransformUtility.WorldToScreenPoint(null, alert.Bubble.position);
            Assert.That(tail.y, Is.GreaterThan(gourdOnScreen.y));
            Assert.That(tail.x, Is.GreaterThan(gourdOnScreen.x));

            Assert.That(alert.Bubble.localScale.x, Is.LessThan(.5f), "It pops in from small.");
            // Sample until both are seen rather than for a fixed short time, so neither a slow nor a
            // fast frame rate can make the test miss them.
            float biggest = 0f;
            bool wiggled = false;
            float until = Time.time + alert.ShowSeconds * .7f;
            while (Time.time < until && (!wiggled || biggest <= .9f))
            {
                yield return null;
                biggest = Mathf.Max(biggest, alert.Bubble.localScale.x);
                if (Mathf.Abs(Mathf.DeltaAngle(0f, alert.Bubble.localEulerAngles.z)) > 1f) wiggled = true;
            }
            Assert.That(biggest, Is.GreaterThan(.9f), "It grows to full size.");
            Assert.That(wiggled, Is.True, "It wiggles like a hand-drawn bubble.");

            gourd.transform.position += Vector3.right;
            Physics.SyncTransforms();
            yield return null;
            Vector2 moved = RectTransformUtility.WorldToScreenPoint(null, alert.Bubble.position);
            Assert.That(moved.x, Is.GreaterThan(tail.x + 1f), "It follows the swinging gourd.");

            until = Time.time + alert.ShowSeconds + .3f;
            while (alert.Active && Time.time < until) yield return null;
            Assert.That(alert.Active, Is.False, "It leaves by itself.");
            Assert.That(alert.Visible, Is.False);

            // Seen from behind the camera's view, nothing is drawn, but the alert still runs its course.
            alert.Show(gourd);
            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            Assert.That(alert.Active, Is.True);
            Assert.That(alert.Visible, Is.False, "Off screen, the bubble is hidden.");
        }
    }
}
