using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>Owner-local physics in an isolated scene. These are not physical-device gesture results.</summary>
    public sealed class LocalOrbPhysicsBoardPlayTests
    {
        private Scene scene;
        private PhysicsScene2D physics;
        private GameObject root;
        private LocalOrbPhysicsBoard board;
        private const float Radius = .2f;
        private static readonly Rect Bounds = new Rect(-2f, -2f, 4f, 4f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("C6 Local Physics " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            Assert.That(physics.IsValid(), Is.True);
            root = new GameObject("Owned local physics test");
            SceneManager.MoveGameObjectToScene(root, scene);
            board = root.AddComponent<LocalOrbPhysicsBoard>();
            board.Configure(Bounds, Radius, Tuning());
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) while (!unload.isDone) yield return null;
            }
        }
        private static OrbPhysicsTuning Tuning(float drag = .4f, float stop = .02f, float bounce = .8f, float max = 5f)
            => new OrbPhysicsTuning(bounce, drag, stop, max, .15f, 0f);
        private OrbView View(string id, Vector2 position, OrbPolarity polarity = OrbPolarity.Yin, float scale = 1f)
        {
            var game = new GameObject(id);
            SceneManager.MoveGameObjectToScene(game, scene);
            game.transform.SetParent(root.transform, false);
            game.transform.position = position;
            game.transform.localScale = new Vector3(scale, scale, 1f);
            var view = game.AddComponent<OrbView>();
            view.Configure(id, OrbKind.Raw, polarity, 0, Radius);
            board.Register(view);
            return view;
        }
        private void Simulate(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                board.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                Assert.That(physics.Simulate(Time.fixedDeltaTime), Is.True);
            }
        }
        private void Flick(OrbView view, Vector2 start, Vector2 end, double time = 10)
        {
            board.SetPosition(view.OrbId, start, false, time);
            Assert.That(board.Grab(view.OrbId, time), Is.True);
            board.SetPosition(view.OrbId, Vector2.Lerp(start, end, .5f), true, time + .04);
            board.SetPosition(view.OrbId, end, true, time + .08);
            Assert.That(board.Release(view.OrbId, time + .08, true), Is.True);
        }
        private Vector2 Velocity(OrbView view)
        { Assert.That(board.TryGetVelocity(view.OrbId, out var velocity), Is.True); return velocity; }

        [Test]
        public void RegisterCreatesAWorldSizedDynamicCircleWithNoGravityOrRotation()
        {
            var view = View("scaled", Vector2.zero, OrbPolarity.Yin, .4f);
            var body = view.GetComponent<Rigidbody2D>();
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.gravityScale, Is.Zero);
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(view.Collider.isTrigger, Is.False);
            Assert.That(view.Collider.radius * .4f, Is.EqualTo(Radius).Within(.00001f));
            Assert.That(view.Collider.sharedMaterial.bounciness, Is.EqualTo(.8f));
            Simulate(20);
            Assert.That(body.position, Is.EqualTo(Vector2.zero));
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DirectReleaseProducesClampedMotionAndResistanceStopsItExactly()
        {
            board.Configure(new Rect(-20, -20, 40, 40), Radius, Tuning(4f, .02f, .8f, 4f));
            var view = View("flick", Vector2.zero);
            Flick(view, Vector2.zero, Vector2.right);
            Assert.That(Velocity(view).x, Is.EqualTo(4f).Within(.0001f));
            Simulate(8);
            Assert.That(view.GetComponent<Rigidbody2D>().position.x, Is.GreaterThan(1f));
            Assert.That(Velocity(view).x, Is.InRange(.01f, 3.99f));
            Simulate(100);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
            Vector2 stopped = view.GetComponent<Rigidbody2D>().position;
            Simulate(15);
            Assert.That(view.GetComponent<Rigidbody2D>().position, Is.EqualTo(stopped));
        }

        [Test]
        public void PhysicalWallCollisionReversesTheVelocityWithoutARequestOrOrbRemoval()
        {
            board.Configure(Bounds, Radius, Tuning(0f, 0f, .8f));
            var view = View("wall", Vector2.zero);
            var contacts = view.gameObject.AddComponent<LocalBoardContactCounter>();
            Flick(view, new Vector2(.6f, 0), new Vector2(1.2f, 0));
            bool reversed = false;
            for (int i = 0; i < 20; i++) { Simulate(1); if (Velocity(view).x < -.1f) reversed = true; }
            Assert.That(contacts.Contacts, Is.GreaterThan(0), "Actual Collider2D contact is required, not only a bounds clamp.");
            Assert.That(reversed, Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(view.OrbId, Is.EqualTo("wall"));
            Assert.That(view.GetComponent<Rigidbody2D>().position.x, Is.InRange(Bounds.xMin - .005f, Bounds.xMax + .005f));
        }

        [Test]
        public void PassiveOppositePolarityContactMovesBothBodiesWithoutCombining()
        {
            board.Configure(Bounds, Radius, Tuning(0f, 0f, .8f));
            var yin = View("yin", new Vector2(-1.2f, 0));
            var yang = View("yang", new Vector2(.5f, 0), OrbPolarity.Yang);
            var contacts = yin.gameObject.AddComponent<LocalBoardContactCounter>();
            Flick(yin, new Vector2(-1.2f, 0), new Vector2(-.6f, 0));
            Simulate(15);
            Assert.That(contacts.Contacts, Is.GreaterThan(0));
            Assert.That(yang.GetComponent<Rigidbody2D>().position.x, Is.GreaterThan(.6f));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(root.GetComponentsInChildren<OrbView>(), Has.Length.EqualTo(2));
            Assert.That(yin.OrbId, Is.EqualTo("yin"));
            Assert.That(yang.OrbId, Is.EqualTo("yang"));
            Assert.That(yin.GetComponentInChildren<TextMesh>().text, Is.EqualTo("YIN"));
            Assert.That(yang.GetComponentInChildren<TextMesh>().text, Is.EqualTo("YANG"));
        }

        [Test]
        public void HeldViewCanOverlapWithoutPushingAndUnlockedRejectionSeparatesIt()
        {
            var moving = View("a", new Vector2(-1, 0));
            var other = View("b", Vector2.zero, OrbPolarity.Yang);
            Assert.That(board.Grab(moving.OrbId, 10), Is.True);
            board.SetPosition(moving.OrbId, Vector2.zero, true, 10.1);
            Assert.That(moving.Collider.isTrigger, Is.True);
            Assert.That(moving.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Simulate(10);
            Assert.That(other.GetComponent<Rigidbody2D>().position, Is.EqualTo(Vector2.zero));
            board.SetLocked(moving.OrbId, true);
            board.SetLocked(other.OrbId, true);
            Assert.That(board.Grab(moving.OrbId, 11), Is.False);
            board.SetLocked(other.OrbId, false);
            board.SetLocked(moving.OrbId, false);
            Assert.That(Vector2.Distance(moving.GetComponent<Rigidbody2D>().position, other.GetComponent<Rigidbody2D>().position), Is.GreaterThanOrEqualTo(Radius * 2));
            Assert.That(moving.Collider.isTrigger, Is.False);
            Assert.That(other.Collider.isTrigger, Is.False);
            Assert.That(Velocity(moving), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void StationaryHoldDoesNotReuseAnOldFastMovement()
        {
            var view = View("wait", Vector2.zero);
            Assert.That(board.Grab(view.OrbId, 10), Is.True);
            board.SetPosition(view.OrbId, Vector2.right, true, 10.05);
            Assert.That(board.Release(view.OrbId, 11, true), Is.True);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CancelDuplicateReleaseAndInvalidTimesCannotCreateMomentum()
        {
            var view = View("cancel", Vector2.zero);
            Assert.That(board.Grab(view.OrbId, double.NaN), Is.False);
            Assert.That(board.Grab(view.OrbId, 10), Is.True);
            board.SetPosition(view.OrbId, Vector2.right, true, 10.05);
            board.SetPosition(view.OrbId, new Vector2(2, 0), true, 9);
            Assert.That(view.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(1f));
            Assert.That(board.Release(view.OrbId, 10.05, false), Is.True);
            Assert.That(board.Release(view.OrbId, 10.06, true), Is.False);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
            Assert.That(board.Grab(view.OrbId, 12), Is.True);
            board.SetPosition(view.OrbId, new Vector2(float.NaN, 0), true, 12.1);
            Assert.That(board.Release(view.OrbId, double.PositiveInfinity, true), Is.True);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PauseClearsHoldAndMotionAndResumeRestoresCollisionState()
        {
            var view = View("pause", Vector2.zero);
            Flick(view, Vector2.zero, Vector2.right);
            board.SetPaused(true);
            Assert.That(board.Paused, Is.True);
            Assert.That(view.Collider.isTrigger, Is.True);
            Assert.That(board.Grab(view.OrbId, 11), Is.False);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
            board.SetPaused(false);
            Assert.That(view.Collider.isTrigger, Is.False);
            Assert.That(view.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(board.Release(view.OrbId, 11.1, true), Is.False);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void GeometryChangeClampsAndClearsWhileIdenticalConfigurePreservesMotion()
        {
            var view = View("resize", Vector2.zero);
            Flick(view, Vector2.zero, Vector2.right);
            Vector2 velocity = Velocity(view);
            board.Configure(Bounds, Radius, Tuning());
            Assert.That(Velocity(view), Is.EqualTo(velocity));
            board.Configure(new Rect(-.5f, -.5f, 1f, 1f), Radius, Tuning());
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
            Assert.That(view.GetComponent<Rigidbody2D>().position.x, Is.InRange(-.5f, .5f));
            Assert.That(view.Collider.isTrigger, Is.False);
        }

        [Test]
        public void OwnershipRemovalAndClearStopBodiesAndPreserveViewsForTheirController()
        {
            var view = View("remove", Vector2.zero);
            Flick(view, Vector2.zero, Vector2.right);
            board.Remove(view.OrbId);
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.TryGetVelocity(view.OrbId, out _), Is.False);
            Assert.That(view.GetComponent<Rigidbody2D>().simulated, Is.False);
            Assert.That(view != null, Is.True);
            board.Register(view);
            Assert.That(view.GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(view.Collider.isTrigger, Is.False);
            Assert.That(Velocity(view), Is.EqualTo(Vector2.zero));
            board.Clear();
            Assert.That(board.Count, Is.Zero);
        }

        [Test]
        public void InvalidGeometryAndDefaultTuningAreRejectedBeforeChangingTheBoard()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Configure(new Rect(float.NaN, 0, 1, 1), Radius, Tuning()));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Configure(Bounds, Radius, default));
            Assert.That(board.CenterBounds, Is.EqualTo(Bounds));
        }
    }

    public sealed class LocalBoardContactCounter : MonoBehaviour
    {
        public int Contacts { get; private set; }
        private void OnCollisionEnter2D(Collision2D collision) { Contacts++; }
    }
}
