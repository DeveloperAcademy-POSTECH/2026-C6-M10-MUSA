using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabImpactFeedbackTests
    {
        [UnityTest]
        public IEnumerator ApprovedHitShowsElementDamageAtMovingHitboxAndCleansUp()
        {
            var canvasObject = new GameObject("Impact Test Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cameraObject = new GameObject("Impact Test Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
            var feedback = canvasObject.AddComponent<LabImpactFeedback>();
            typeof(LabImpactFeedback).GetField("battleCamera",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(feedback, camera);

            feedback.PlayHit(target, Vector3.zero, LabOrbElement.Fire, 0);
            Assert.IsNull(canvasObject.transform.Find("Impact Feedback"),
                "An unapproved zero-damage event must not create a hit marker.");

            feedback.PlayHit(target, Vector3.zero, LabOrbElement.Water, 17);
            Transform layer = canvasObject.transform.Find("Impact Feedback");
            Assert.IsNotNull(layer);
            RectTransform marker = layer.Find("Hit Feedback") as RectTransform;
            Assert.IsNotNull(marker);
            var number = marker.GetComponentInChildren<UnityEngine.UI.Text>();
            Assert.AreEqual("-17", number.text);
            Assert.That(number.color.b - number.color.r, Is.GreaterThan(.5f),
                "Water damage should use the blue element palette.");
            Transform ring = marker.Find("Expanding Hit Ring");
            Assert.AreEqual(20, ring.childCount);
            Assert.AreEqual(0, layer.GetComponentsInChildren<Collider>().Length,
                "Visual feedback must not add a second hitbox.");

            float before = marker.anchoredPosition.x;
            float initialScale = ring.localScale.x;
            target.Hitbox.transform.position += Vector3.right * .5f;
            yield return null;
            Assert.That(marker.anchoredPosition.x - before, Is.GreaterThan(1f),
                "The marker should track the moving MeshCollider, not the static target root.");
            Assert.That(ring.localScale.x, Is.GreaterThan(initialScale),
                "The hit ring should expand after impact.");

            yield return new WaitForSecondsRealtime(.75f);
            yield return null;
            Assert.IsTrue(marker == null, "A short-lived marker should remove itself.");

            feedback.PlayHit(target, Vector3.zero, LabOrbElement.Fire, 5);
            Transform newLayer = canvasObject.transform.Find("Impact Feedback");
            Assert.IsNotNull(newLayer);
            feedback.Clear();
            Assert.IsFalse(newLayer.gameObject.activeSelf, "Clear must hide the effect immediately.");
            Object.Destroy(canvasObject);
            Object.Destroy(cameraObject);
            Object.Destroy(target.gameObject);
        }
    }
}
