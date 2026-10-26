using System.Collections;
using System.Linq;
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

        [UnityTest]
        public IEnumerator TheBlockBubbleShowsTheBlockedWayInsteadOfTheExclamationAndStaysLonger()
        {
            root = new GameObject("Gourd block alert test");
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

            alert.ShowBlock(gourd, true);
            Transform blockMark = alert.Bubble.Find("Gourd Alert Block Mark");
            Transform exclamation = alert.Bubble.Find("Gourd Alert Mark");
            Assert.That(alert.ShowingBlock, Is.True, "The gourd's swipe block pops its own bubble (#215).");
            Assert.That(alert.Visible, Is.True);
            Assert.That(alert.BlockRight, Is.True);
            Assert.That(alert.Label, Is.EqualTo("→ 🚫"));
            Assert.That(blockMark.gameObject.activeSelf, Is.True, "A drawn arrow and no-entry sign...");
            Assert.That(exclamation.gameObject.activeSelf, Is.False, "...instead of the bomb's \"!\".");
            var image = blockMark.GetComponent<Image>();
            Assert.That(image.sprite.name, Is.EqualTo("Lab gourd block right"));
            Assert.That(image.raycastTarget, Is.False, "It never blocks a touch.");

            alert.ShowBlock(gourd, false);
            Assert.That(alert.Label, Is.EqualTo("← 🚫"));
            Assert.That(image.sprite.name, Is.EqualTo("Lab gourd block left"));

            float shown = Time.time;
            float until = Time.time + LabGourdAlert.BlockShowSeconds + 1f;
            while (alert.Active && Time.time < until) yield return null;
            Assert.That(alert.Active, Is.False, "It leaves by itself.");
            Assert.That(Time.time - shown, Is.GreaterThanOrEqualTo(LabGourdAlert.BlockShowSeconds - .1f),
                "After about two seconds, longer than the bomb's bubble.");
            Assert.That(LabGourdAlert.BlockShowSeconds, Is.GreaterThan(alert.ShowSeconds));

            alert.Show(gourd);
            Assert.That(alert.ShowingBlock, Is.False);
            Assert.That(alert.Label, Is.EqualTo("!"), "A bomb still shows its \"!\".");
            Assert.That(exclamation.gameObject.activeSelf, Is.True);
            Assert.That(blockMark.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TheYaguangEyesShowPlainThenGlowAndLeaveAtTheFlash()
        {
            root = new GameObject("Gourd eyes alert test");
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

            alert.ShowEyes(gourd, .6f);
            Transform eyes = alert.Bubble.Find("Gourd Alert Eyes");
            Image lit = eyes.Find("Gourd Alert Eyes Lit").GetComponent<Image>();
            Assert.That(alert.ShowingEyes, Is.True, "The yaguang-gwi's eyes in the bubble (#268).");
            Assert.That(alert.Label, Is.EqualTo("👀"));
            Assert.That(eyes.gameObject.activeSelf, Is.True);
            Assert.That(alert.Bubble.Find("Gourd Alert Mark").gameObject.activeSelf, Is.False);
            Assert.That(lit.color.a, Is.Zero, "Plain eyes first.");
            Assert.That(eyes.GetComponentsInChildren<Image>().All(image => !image.raycastTarget), Is.True);

            alert.SetEyeGlow(1f);
            Assert.That(alert.EyeGlow, Is.EqualTo(1f));
            Assert.That(lit.color.a, Is.EqualTo(1f), "Then the eyes light up.");

            float until = Time.time + 1.5f;
            while (alert.Active && Time.time < until) yield return null;
            Assert.That(alert.Active, Is.False, "Gone by the flash.");
            Assert.That(alert.ShowingEyes, Is.False);

            alert.ShowWord(gourd, "셋!", 1f);
            Text word = alert.Bubble.Find("Gourd Alert Mark").GetComponent<Text>();
            Assert.That(alert.ShowingWord, Is.True);
            Assert.That(word.text, Is.EqualTo("셋!"));
            Assert.That(word.GetComponent<Outline>().enabled, Is.False, "No offset outline doubling the ink strokes (#271).");
            alert.ShowWord(gourd, "넷!", 1f);
            Assert.That(word.text, Is.EqualTo("넷!"), "The same bubble just changes its word.");
            Assert.That(word.resizeTextForBestFit, Is.False, "Short words keep their big size.");
            alert.ShowWord(gourd, "안보이지롱~", 1f);
            RectTransform bubbleRect = alert.Bubble;
            Assert.That(word.resizeTextForBestFit, Is.True, "A long word (#301) shrinks to fit...");
            Assert.That(word.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(word.rectTransform.sizeDelta.x, Is.LessThan(bubbleRect.sizeDelta.x), "...inside the bubble.");
            Assert.That(word.rectTransform.sizeDelta.y, Is.LessThan(bubbleRect.sizeDelta.y));
            alert.ShowWord(gourd, "넷!", 1f);
            Assert.That(word.resizeTextForBestFit, Is.False);
            alert.Show(gourd);
            Assert.That(word.GetComponent<Outline>().enabled, Is.True, "The bomb's red \"!\" keeps its outline.");
            alert.ShowEyes(gourd, 2f);
            alert.Show(gourd);
            Assert.That(alert.ShowingEyes, Is.False, "A bomb's \"!\" replaces the eyes.");
            Assert.That(eyes.gameObject.activeSelf, Is.False);
        }
    }
}
