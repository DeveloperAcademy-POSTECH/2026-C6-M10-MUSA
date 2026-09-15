using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>Local held artwork regression checks; these are not physical-device touch evidence.</summary>
    public sealed class OrbHeldFeedbackPlayTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator RemoveOwnedViews()
        {
            foreach (var value in owned)
                if (value != null) Object.Destroy(value);
            owned.Clear();
            yield return null;
            yield return null;
        }

        [Test]
        public void EarlierScenesKeepTheirOriginalArtworkUntilTheyOptIn()
        {
            var view = CreateView("legacy");
            var ring = view.GetComponent<SpriteRenderer>();
            Vector3 rootScale = view.transform.localScale;
            Vector3 coreScale = view.transform.Find("Core").localScale;
            view.SetLocalState(LocalOrbState.Dragging);
            Assert.That(view.HeldFeedbackActive, Is.False);
            Assert.That(view.HeldScale, Is.EqualTo(1f));
            Assert.That(view.RingRenderer, Is.SameAs(ring));
            Assert.That(ring.enabled, Is.True);
            Assert.That(ring.color, Is.EqualTo(Color.white));
            Assert.That(view.transform.Find("HeldArtwork"), Is.Null);
            Assert.That(view.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(view.transform.Find("Core").localScale, Is.EqualTo(coreScale));
        }

        [UnityTest]
        public IEnumerator HeldArtworkPulsesWithoutMovingTheColliderOrCanonicalDropCenter()
        {
            var view = CreateView("held-center");
            view.transform.position = new Vector3(4f, -2f, 0f);
            view.transform.localScale = new Vector3(0.37f, 0.37f, 1f);
            view.SetHeldFeedbackEnabled(true);
            Physics2D.SyncTransforms();
            Vector3 position = view.transform.position;
            Vector3 scale = view.transform.localScale;
            Bounds colliderBounds = view.Collider.bounds;
            float radius = view.Collider.radius;
            float idleArtworkWidth = view.RingRenderer.bounds.size.x;
            var label = view.GetComponentInChildren<TextMesh>();
            float labelSize = label.characterSize;
            Vector3 labelScale = label.transform.localScale;
            view.SetLocalState(LocalOrbState.Dragging);
            Assert.That(view.HeldFeedbackActive, Is.True);
            Assert.That(view.HeldScale, Is.InRange(1.214f, 1.266f));
            Assert.That(view.RingRenderer.bounds.size.x, Is.GreaterThan(idleArtworkWidth * 1.20f));
            Assert.That(view.transform.Find("HeldHaloOuter").GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(view.transform.Find("HeldHaloInner").GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(view.transform.Find("HeldShadow").GetComponent<SpriteRenderer>().enabled, Is.True);
            float minimum = view.HeldScale;
            float maximum = view.HeldScale;
            float until = Time.realtimeSinceStartup + 0.30f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                minimum = Mathf.Min(minimum, view.HeldScale);
                maximum = Mathf.Max(maximum, view.HeldScale);
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.001f), "Held feedback must animate while the pointer is still.");
            Physics2D.SyncTransforms();
            Assert.That(view.transform.position, Is.EqualTo(position));
            Assert.That(view.transform.localScale, Is.EqualTo(scale));
            Assert.That(view.Collider.radius, Is.EqualTo(radius));
            Assert.That(view.Collider.offset, Is.EqualTo(Vector2.zero));
            Assert.That(view.Collider.bounds.center, Is.EqualTo(colliderBounds.center));
            Assert.That(view.Collider.bounds.size, Is.EqualTo(colliderBounds.size));
            Assert.That(view.GetComponentsInChildren<Collider2D>(), Has.Length.EqualTo(1));
            Assert.That(label.characterSize, Is.EqualTo(labelSize));
            Assert.That(label.transform.localScale, Is.EqualTo(labelScale));
            Assert.That(label.transform.localPosition.y, Is.LessThan(-radius * 1.5f));
        }

        [TestCase(LocalOrbState.Idle)]
        [TestCase(LocalOrbState.Pending)]
        public void ReleasingOrReservingTheOrbImmediatelyRestoresItsSize(LocalOrbState endState)
        {
            var view = CreateView("restore-" + endState);
            view.SetHeldFeedbackEnabled(true);
            var label = view.GetComponentInChildren<TextMesh>();
            Vector3 initialLabelPosition = label.transform.localPosition;
            view.SetLocalState(LocalOrbState.Dragging);
            Assert.That(view.HeldScale, Is.GreaterThan(1.2f));
            view.SetLocalState(endState);
            AssertRestored(view);
            Assert.That(label.transform.localPosition, Is.EqualTo(initialLabelPosition));
            Assert.That(label.text, Is.EqualTo(endState == LocalOrbState.Pending ? "LOCKED" : "YIN"));
            Assert.That(view.RingRenderer.sortingOrder, Is.EqualTo(40));
        }

        [Test]
        public void OptingOutAndRepeatedlyGrabbingReuseTheSameSharedArtwork()
        {
            var view = CreateView("reuse");
            view.SetHeldFeedbackEnabled(true);
            int children = view.GetComponentsInChildren<Transform>(true).Length;
            Sprite circle = view.RingRenderer.sprite;
            Material material = view.RingRenderer.sharedMaterial;
            for (int i = 0; i < 12; ++i)
            {
                view.SetHeldFeedbackEnabled(true);
                view.SetLocalState(LocalOrbState.Dragging);
                Assert.That(view.HeldFeedbackActive, Is.True);
                view.SetHeldFeedbackEnabled(false);
                AssertRestored(view);
                view.SetLocalState(LocalOrbState.Idle);
            }
            Assert.That(view.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(children));
            foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Assert.That(renderer.sprite, Is.SameAs(circle));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            }
        }

        [Test]
        public void DisablingASelectedViewClearsFeedbackAndDoesNotRestoreAStaleHold()
        {
            var view = CreateView("disable");
            view.SetHeldFeedbackEnabled(true);
            view.SetLocalState(LocalOrbState.Dragging);
            view.enabled = false;
            AssertRestored(view);
            Assert.That(view.LocalState, Is.EqualTo(LocalOrbState.Idle));
            view.enabled = true;
            AssertRestored(view);
            view.SetLocalState(LocalOrbState.Dragging);
            Assert.That(view.HeldFeedbackActive, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingAHeldViewPreservesItsSiblingThenReleasesTheirSharedCircle()
        {
            // A dedicated radius keeps this asset isolated from any scene fixture views.
            var first = CreateView("lifetime-first", 0.287531f);
            var second = CreateView("lifetime-second", 0.287531f);
            first.SetHeldFeedbackEnabled(true);
            first.SetLocalState(LocalOrbState.Dragging);
            second.SetHeldFeedbackEnabled(true);
            Sprite sprite = first.RingRenderer.sprite;
            Assert.That(second.RingRenderer.sprite, Is.SameAs(sprite));
            Object.Destroy(first.gameObject);
            yield return null;
            yield return null;
            Assert.That(sprite != null, Is.True, "Destroying one view cannot destroy a sibling's shared circle.");
            Assert.That(sprite.texture != null, Is.True);
            second.SetLocalState(LocalOrbState.Dragging);
            Assert.That(second.HeldFeedbackActive, Is.True);
            Object.Destroy(second.gameObject);
            yield return null;
            yield return null;
            Assert.That(sprite == null, Is.True, "The last view must release the shared circle, including held artwork.");
        }

        private OrbView CreateView(string id, float radius = 0.3f)
        {
            var value = new GameObject("Held feedback test " + id);
            owned.Add(value);
            var view = value.AddComponent<OrbView>();
            view.Configure(id, OrbKind.Raw, OrbPolarity.Yin, 0, radius);
            return view;
        }

        private static void AssertRestored(OrbView view)
        {
            Assert.That(view.HeldFeedbackActive, Is.False);
            Assert.That(view.HeldScale, Is.EqualTo(1f));
            foreach (string name in new[] { "HeldHaloOuter", "HeldHaloInner", "HeldShadow" })
                Assert.That(view.transform.Find(name).GetComponent<SpriteRenderer>().enabled, Is.False, name);
        }
    }
}
