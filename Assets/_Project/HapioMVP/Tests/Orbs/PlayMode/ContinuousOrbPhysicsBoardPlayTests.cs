using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>Isolated real Physics2D fixtures, not networking, device input, or latency evidence.</summary>
    public sealed class ContinuousOrbPhysicsBoardPlayTests
    {
        private Scene scene;
        private PhysicsScene2D physics;
        private GameObject root;
        private readonly List<LocalOrbPhysicsBoard> boards = new List<LocalOrbPhysicsBoard>();
        private LocalOrbPhysicsBoard board;
        private const float Radius = .2f;
        private const float Width = 5f;
        private static readonly Rect Bounds = new Rect(-2f, -2f, 4f, 4f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("C6 Continuous Physics " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            Assert.That(physics.IsValid(), Is.True);
            root = new GameObject("Continuous edge test fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            board = Board(Bounds, Width);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            boards.Clear();
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) while (!unload.isDone) yield return null;
            }
        }

        private LocalOrbPhysicsBoard Board(Rect bounds, float width, float normalizedDrag = .4f, bool passage = true)
        {
            var game = new GameObject("Board " + boards.Count);
            SceneManager.MoveGameObjectToScene(game, scene);
            game.transform.SetParent(root.transform, false);
            var result = game.AddComponent<LocalOrbPhysicsBoard>();
            result.Configure(bounds, Radius, new OrbPhysicsTuning(.8f, normalizedDrag * width, .01f * width,
                5f * width, .15f, 0f));
            result.ConfigureHorizontalPassage(passage, width);
            boards.Add(result);
            return result;
        }

        private OrbView View(LocalOrbPhysicsBoard owner, string id, Vector2 position, OrbPolarity polarity = OrbPolarity.Yin)
        {
            var game = new GameObject(id);
            SceneManager.MoveGameObjectToScene(game, scene);
            game.transform.SetParent(owner.transform, false);
            game.transform.position = position;
            var view = game.AddComponent<OrbView>();
            view.Configure(id, OrbKind.Raw, polarity, 0, Radius);
            owner.Register(view);
            return view;
        }

        private void Simulate(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                foreach (var item in boards) item.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                Assert.That(physics.Simulate(Time.fixedDeltaTime), Is.True);
            }
        }

        private static Vector2 Velocity(LocalOrbPhysicsBoard owner, string id)
        { Assert.That(owner.TryGetVelocity(id, out var value), Is.True); return value; }

        [TestCase(-1)]
        [TestCase(1)]
        public void OutwardEdgeCapturesRemainingSpeedOnceAndFreezesWithoutBounce(int direction)
        {
            var view = View(board, "cross", Vector2.zero);
            var body = view.GetComponent<Rigidbody2D>();
            body.position = new Vector2(direction > 0 ? Bounds.xMax : Bounds.xMin, .5f);
            body.linearVelocity = new Vector2(direction * 4f, .25f);
            int observations = 0;
            OrbEdgeCrossing crossing = default;
            board.EdgeCrossed += item => { observations++; crossing = item; };
            Simulate(1);
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(crossing.OrbId, Is.EqualTo("cross"));
            Assert.That(crossing.ToRight, Is.EqualTo(direction > 0));
            Assert.That(crossing.Height01, Is.EqualTo(.625f).Within(.00001f));
            Assert.That(crossing.VelocityBoardWidthsPerSecond, Is.EqualTo(new Vector2(direction * .8f, .05f)));
            Assert.That(board.TryGetPendingEdge("cross", out var saved), Is.True);
            Assert.That(saved.VelocityBoardWidthsPerSecond, Is.EqualTo(crossing.VelocityBoardWidthsPerSecond));
            Assert.That(Velocity(board, "cross"), Is.EqualTo(Vector2.zero));
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(body.position.x, Is.EqualTo(direction > 0 ? Bounds.xMax : Bounds.xMin));
            Assert.That(view.transform.position.x, Is.EqualTo(body.position.x));
            Assert.That(board.Grab("cross", 10), Is.False);
            board.SetLocked("cross", true);
            board.SetLocked("cross", false);
            Simulate(25);
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(board.TryGetPendingEdge("cross", out saved), Is.True);
            Assert.That(saved.VelocityBoardWidthsPerSecond.x, Is.EqualTo(direction * .8f));
        }

        [Test]
        public void NormalHeldMotionKeepsTheOrbUntilReleaseThenProducesOneAutomaticCrossing()
        {
            var view = View(board, "held", Vector2.zero);
            int observations = 0;
            board.EdgeCrossed += _ => observations++;
            Assert.That(board.Grab(view.OrbId, 10), Is.True);
            board.SetPosition(view.OrbId, new Vector2(1f, 0), true, 10.04);
            board.SetPosition(view.OrbId, new Vector2(3f, 0), true, 10.08);
            Simulate(5);
            Assert.That(observations, Is.Zero);
            Assert.That(view.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(Bounds.xMax));
            Assert.That(board.Release(view.OrbId, 10.08, true), Is.True);
            Simulate(1);
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.True);
        }

        [Test]
        public void StationaryReleaseAtAnEdgeDoesNotManufactureVelocityOrATransfer()
        {
            var view = View(board, "rest", new Vector2(Bounds.xMax, 0));
            int observations = 0;
            board.EdgeCrossed += _ => observations++;
            Assert.That(board.Grab(view.OrbId, 10), Is.True);
            Assert.That(board.Release(view.OrbId, 11, true), Is.True);
            Simulate(25);
            Assert.That(observations, Is.Zero);
            Assert.That(Velocity(board, view.OrbId), Is.EqualTo(Vector2.zero));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReceiverPreservesHeightAndWidthNormalizedVelocityAcrossDifferentBoardSizes(bool fromLeft)
        {
            var large = Board(new Rect(8f, -3f, 8f, 6f), 10f);
            var view = View(large, "received", large.CenterBounds.center);
            var speed = new Vector2(fromLeft ? .7f : -.7f, .11f);
            Assert.That(large.ResumeTransferred(view.OrbId, fromLeft, .75f, speed), Is.True);
            var body = view.GetComponent<Rigidbody2D>();
            Assert.That(body.position.x, Is.EqualTo(fromLeft ? 8f : 16f));
            Assert.That(body.position.y, Is.EqualTo(1.5f));
            Assert.That(Velocity(large, view.OrbId).x / 10f, Is.EqualTo(speed.x).Within(.00001f));
            Assert.That(Velocity(large, view.OrbId).y / 10f, Is.EqualTo(speed.y).Within(.00001f));
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(body.gravityScale, Is.Zero);
            Simulate(1);
            Assert.That(Velocity(large, view.OrbId).magnitude, Is.LessThan(speed.magnitude * 10f));
            Assert.That(Mathf.Sign(Velocity(large, view.OrbId).x), Is.EqualTo(Mathf.Sign(speed.x)));
            Assert.That(large.TryGetPendingEdge(view.OrbId, out _), Is.False);
        }

        [Test]
        public void RejectedPassageStopsAtSourceEdgeWithoutReflectionOrRepeatedRequests()
        {
            var view = View(board, "full", Vector2.zero);
            var body = view.GetComponent<Rigidbody2D>();
            body.position = new Vector2(Bounds.xMin, .4f);
            body.linearVelocity = Vector2.left * 3f;
            int observations = 0;
            board.EdgeCrossed += _ => observations++;
            Simulate(1);
            Assert.That(body.position.x, Is.EqualTo(Bounds.xMin), "Pending must keep the captured edge pose.");
            Assert.That(board.ResolveRejectedEdge(view.OrbId), Is.True);
            Assert.That(body.position.x, Is.EqualTo(Bounds.xMin), "Restoring a dynamic body must not rewind its pose.");
            Assert.That(board.ResolveRejectedEdge(view.OrbId), Is.False);
            Simulate(30);
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(body.position.x, Is.EqualTo(Bounds.xMin));
            Assert.That(Velocity(board, view.OrbId), Is.EqualTo(Vector2.zero));
            Assert.That(board.Grab(view.OrbId, 12), Is.True, "A new deliberate interaction may try again.");
        }

        [Test]
        public void OptInRemovesSideCollidersButKeepsRealTopAndBottomBounce()
        {
            var view = View(board, "vertical", Vector2.zero);
            var contacts = view.gameObject.AddComponent<LocalBoardContactCounter>();
            int observations = 0;
            board.EdgeCrossed += _ => observations++;
            var activeWalls = board.GetComponentsInChildren<BoxCollider2D>();
            Assert.That(activeWalls.Select(wall => wall.name), Is.EquivalentTo(new[] { "Top", "Bottom" }));
            var body = view.GetComponent<Rigidbody2D>();
            body.position = new Vector2(0, 1.3f); body.linearVelocity = Vector2.up * 5f;
            bool reversed = false;
            for (int i = 0; i < 30; i++) { Simulate(1); if (Velocity(board, view.OrbId).y < -.1f) reversed = true; }
            Assert.That(reversed, Is.True);
            Assert.That(contacts.Contacts, Is.GreaterThan(0));
            Assert.That(observations, Is.Zero);
        }

        [Test]
        public void LegacyOptOutKeepsFourWallsAndDoesNotPublishCrossings()
        {
            board.ConfigureHorizontalPassage(false, Width);
            var view = View(board, "legacy", Vector2.zero);
            int observations = 0;
            board.EdgeCrossed += _ => observations++;
            Assert.That(board.GetComponentsInChildren<BoxCollider2D>().Select(wall => wall.name),
                Is.EquivalentTo(new[] { "Left", "Right", "Top", "Bottom" }));
            var body = view.GetComponent<Rigidbody2D>();
            body.position = new Vector2(1.3f, 0); body.linearVelocity = Vector2.right * 5f;
            bool reversed = false;
            for (int i = 0; i < 25; i++) { Simulate(1); if (Velocity(board, view.OrbId).x < -.1f) reversed = true; }
            Assert.That(reversed, Is.True);
            Assert.That(observations, Is.Zero);
            Assert.That(board.ResumeTransferred(view.OrbId, true, .5f, Vector2.right), Is.False);
        }

        [Test]
        public void PendingCrossingCallbackCanSynchronouslyRemoveAndReconfigureTheBoard()
        {
            var first = View(board, "a", new Vector2(Bounds.xMax, -.5f));
            var second = View(board, "b", Vector2.zero);
            first.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 2f;
            board.EdgeCrossed += edge =>
            {
                board.Remove(edge.OrbId);
                board.Configure(new Rect(-3f, -3f, 6f, 6f), Radius,
                    new OrbPhysicsTuning(.8f, 2f, .05f, 25f, .15f, 0f));
            };
            Assert.DoesNotThrow(() => Simulate(1));
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(board.TryGetVelocity(first.OrbId, out _), Is.False);
            Assert.That(board.TryGetVelocity(second.OrbId, out _), Is.True);
        }

        [Test]
        public void InvalidArrivalOrPausedLockedBoardDoesNotChangeExistingMotion()
        {
            var view = View(board, "invalid", Vector2.zero);
            Assert.That(board.ResumeTransferred(view.OrbId, true, float.NaN, Vector2.right), Is.False);
            Assert.That(board.ResumeTransferred(view.OrbId, true, 1.01f, Vector2.right), Is.False);
            Assert.That(board.ResumeTransferred(view.OrbId, true, .5f, new Vector2(float.PositiveInfinity, 0)), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => board.ConfigureHorizontalPassage(true, 0));
            board.SetLocked(view.OrbId, true);
            Assert.That(board.ResumeTransferred(view.OrbId, true, .5f, Vector2.right), Is.False);
            board.SetLocked(view.OrbId, false); board.SetPaused(true);
            Assert.That(board.ResumeTransferred(view.OrbId, true, .5f, Vector2.right), Is.False);
            Assert.That(Velocity(board, view.OrbId), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PauseResizeDisableAndRemovalClearPendingPhysicalState()
        {
            var view = View(board, "clear", Vector2.zero);
            Action cross = () =>
            {
                var body = view.GetComponent<Rigidbody2D>();
                body.position = new Vector2(Bounds.xMax, 0); body.linearVelocity = Vector2.right;
                Simulate(1);
                Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.True);
            };
            cross(); board.SetPaused(true);
            Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.False);
            board.SetPaused(false); cross();
            board.Configure(new Rect(-2f, -2f, 4f, 4.1f), Radius, new OrbPhysicsTuning(.8f, 2f, .05f, 25f, .15f, 0f));
            Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.False);
            cross(); board.enabled = false;
            Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.False);
            board.enabled = true; cross(); board.Remove(view.OrbId);
            Assert.That(board.TryGetPendingEdge(view.OrbId, out _), Is.False);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void ExplicitThreeBoardFixtureMakesRepeatedHopsWithSameIdThenFrictionStopsIt(int direction)
        {
            // This is an explicit deterministic local fixture with an approved-handoff adapter,
            // not a claim about ordinary release strength or any actual network latency.
            Board(new Rect(8f, -3f, 8f, 6f), 10f);
            Board(new Rect(25f, -1f, 3.2f, 2f), 4f);
            float[] widths = { Width, 10f, 4f };
            int active = 0, hops = 0;
            float previousSpeed = 3f;
            var visited = new HashSet<int> { 0 };
            OrbView moving = View(board, "same-raw-id", Vector2.zero);
            Assert.That(board.ResumeTransferred(moving.OrbId, direction > 0, .5f, Vector2.right * (direction * 3f)), Is.True);
            for (int index = 0; index < boards.Count; index++)
            {
                int sourceIndex = index;
                boards[index].EdgeCrossed += edge =>
                {
                    Assert.That(sourceIndex, Is.EqualTo(active));
                    Assert.That(edge.OrbId, Is.EqualTo("same-raw-id"));
                    Assert.That(edge.ToRight, Is.EqualTo(direction > 0));
                    float speed = edge.VelocityBoardWidthsPerSecond.magnitude;
                    Assert.That(speed, Is.LessThanOrEqualTo(previousSpeed + .0001f));
                    previousSpeed = speed; hops++;
                    boards[active].Remove(edge.OrbId); moving.gameObject.SetActive(false);
                    active = (active + direction + boards.Count) % boards.Count; visited.Add(active);
                    moving = View(boards[active], edge.OrbId, boards[active].CenterBounds.center);
                    Assert.That(boards[active].ResumeTransferred(edge.OrbId, edge.ToRight, edge.Height01,
                        edge.VelocityBoardWidthsPerSecond), Is.True);
                    Assert.That(boards.Sum(item => item.Count), Is.EqualTo(1));
                };
            }
            Simulate(500);
            Assert.That(hops, Is.GreaterThan(3));
            Assert.That(visited.Count, Is.EqualTo(3));
            Assert.That(boards.Sum(item => item.Count), Is.EqualTo(1));
            Assert.That(Velocity(boards[active], moving.OrbId) / widths[active], Is.EqualTo(Vector2.zero));
            int stoppedHops = hops;
            Vector2 stoppedPosition = moving.GetComponent<Rigidbody2D>().position;
            Simulate(30);
            Assert.That(hops, Is.EqualTo(stoppedHops));
            Assert.That(moving.GetComponent<Rigidbody2D>().position, Is.EqualTo(stoppedPosition));
            Assert.That(moving.GetComponentInChildren<TextMesh>().text, Is.EqualTo("YIN"));
            Assert.That(boards[active].TryGetPendingEdge(moving.OrbId, out _), Is.False);
        }

        [Test]
        public void PassiveOppositePolarityContactStillCannotCombine()
        {
            var yin = View(board, "yin", new Vector2(-.8f, 0));
            var yang = View(board, "yang", new Vector2(.2f, 0), OrbPolarity.Yang);
            var contacts = yin.gameObject.AddComponent<LocalBoardContactCounter>();
            yin.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 2f;
            Simulate(20);
            Assert.That(contacts.Contacts, Is.GreaterThan(0));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(yin.OrbId, Is.EqualTo("yin"));
            Assert.That(yang.OrbId, Is.EqualTo("yang"));
            Assert.That(yin.GetComponentInChildren<TextMesh>().text, Is.EqualTo("YIN"));
            Assert.That(yang.GetComponentInChildren<TextMesh>().text, Is.EqualTo("YANG"));
        }
    }
}
