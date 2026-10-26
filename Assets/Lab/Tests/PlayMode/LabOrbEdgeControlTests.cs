using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Actual board input and Rigidbody regressions for #310. Explicit screen geometry keeps
    /// the tests independent of the Editor's current Game view and physical iOS safe insets.
    /// These synthetic gestures are not evidence of iPhone system-gesture behavior.
    /// </summary>
    public sealed class LabOrbEdgeControlTests
    {
        private static readonly Rect Bounds = new Rect(-4f, -2f, 8f, 4f);
        private static readonly Rect Viewport = new Rect(0f, 0f, 390f, 195f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private Camera camera;
        private readonly List<LabOrbEdgeCrossing> crossings = new List<LabOrbEdgeCrossing>();
        private int throwCount;
        private int combineCount;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            crossings.Clear();
            throwCount = 0;
            combineCount = 0;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            root = new GameObject("Edge control regression root");
            config = ScriptableObject.CreateInstance<LabConfig>();
            SetCombining(false);
            var cameraObject = new GameObject("Deterministic edge input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.pixelRect = Viewport;
            camera.aspect = 2f;
            var boardObject = new GameObject("Edge control board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, Bounds);
            board.SetInputScreenRect(Viewport);
            board.SetEdgeControlScreenGeometry(Viewport, Viewport);
            board.EdgeCrossed += crossings.Add;
            board.ThrowRequested += _ => throwCount++;
            board.CombineRequested += (_, __, ___, ____) => combineCount++;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            crossings.Clear();
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void ReleasingInsideTheVisibleSideBandUsesTheExistingTransferEvent(int side)
        {
            var orb = Spawn("assisted", new Vector2(side > 0 ? .8f : .2f, .5f));
            Vector2 from = XY(orb.Body.position);
            Vector2 release = new Vector2(side * 3.6f, from.y);
            Assert.That(Mathf.Abs(release.x), Is.LessThan(Bounds.xMax), "The finger stays inside the display.");

            Begin(from, 1d);
            Assert.That(board.EndDragAtWorld(release, 1.1d), Is.True);

            Assert.That(crossings.Count, Is.EqualTo(1));
            Assert.That(crossings[0].OrbId, Is.EqualTo("assisted"));
            Assert.That(crossings[0].ToRight, Is.EqualTo(side > 0));
            Assert.That(crossings[0].Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe));
            Assert.That(crossings[0].VelocityInBoardWidthsPerSecond.x * side, Is.GreaterThan(0f),
                "The same outward-motion contract must reach Host validation.");
            Assert.That(crossings[0].Height01, Is.EqualTo(.5f).Within(.001f));
            Assert.That(orb.Body.isKinematic, Is.True, "Ownership approval is still pending.");
            Assert.That(board.Count, Is.EqualTo(1), "An assisted gesture cannot create another orb.");
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void HoldingStillInTheBandKeepsIntentButDoesNotTransferBeforeRelease(int side)
        {
            var orb = Spawn("hold", new Vector2(side > 0 ? .8f : .2f, .5f));
            Begin(XY(orb.Body.position), 1d);
            Vector2 end = new Vector2(side * 3.6f, 0f);
            Assert.That(board.DragToWorld(end, 1.1d), Is.True);
            Assert.That(board.PreparedTransferSide, Is.EqualTo(side));
            Assert.That(board.TryGetTransferGuide(out Vector2 guide, out bool right), Is.True);
            Assert.That(right, Is.EqualTo(side > 0));
            Assert.That(Mathf.Abs(guide.x), Is.LessThan(Bounds.xMax));
            Assert.That(board.DragToWorld(end, 2.1d), Is.True);
            Assert.That(crossings, Is.Empty, "Keeping a finger down must never hand off ownership.");

            Assert.That(board.EndDragAtWorld(end, 2.2d), Is.True);
            Assert.That(crossings.Count, Is.EqualTo(1), "A deliberate release works after the velocity samples settled.");
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void TurningBackInwardCancelsPreparedTransferEvenWhileStillInsideTheBand(int side)
        {
            var orb = Spawn("turn-back", new Vector2(side > 0 ? .8f : .2f, .5f));
            Begin(XY(orb.Body.position), 1d);
            Vector2 outer = new Vector2(side * 3.7f, 0f);
            Vector2 inner = new Vector2(side * 3.4f, 0f);
            Assert.That(board.DragToWorld(outer, 1.1d), Is.True);
            Assert.That(board.PreparedTransferSide, Is.EqualTo(side));
            Assert.That(board.DragToWorld(inner, 1.2d), Is.True);
            Assert.That(board.PreparedTransferSide, Is.Zero);
            Assert.That(board.TryGetTransferGuide(out _, out _), Is.False);
            Assert.That(board.EndDragAtWorld(inner, 1.25d), Is.True);
            Assert.That(crossings, Is.Empty);
            Assert.That(orb.Body.isKinematic, Is.False);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void AStationaryTapOrLongPressInTheBandDoesNotPass(int side)
        {
            var orb = Spawn("stationary", new Vector2(side > 0 ? .95f : .05f, .5f));
            Vector2 point = XY(orb.Body.position);
            Begin(point, 1d);
            Assert.That(board.EndDragAtWorld(point, 1.05d), Is.True);
            Begin(point, 2d);
            Assert.That(board.DragToWorld(point, 3d), Is.True);
            Assert.That(board.EndDragAtWorld(point, 3.1d), Is.True);
            Assert.That(crossings, Is.Empty);
            Assert.That(board.PreparedTransferSide, Is.Zero);
        }

        [Test]
        public void AHeavyBombCannotSkipItsDragLagWhenOnlyTheFingerReachedTheSideBand()
        {
            float[] values = config.CaptureDeveloperValues();
            values[141] = 1f; // maximum supported drag lag, in seconds
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.BombDragLag, Is.EqualTo(1f));
            var bomb = board.UpsertOrb("heavy-lag", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2);
            Begin(XY(bomb.Body.position), 1d);
            Vector2 finger = new Vector2(3.6f, 0f);
            Assert.That(board.DragToWorld(finger, 1.05d), Is.True);
            Assert.That(bomb.Body.position.x, Is.LessThan(1f), "The heavy body is still near the center while the finger reached the gate.");
            Assert.That(board.PreparedTransferSide, Is.Zero);
            Assert.That(board.TryGetTransferGuide(out _, out _), Is.False);
            Assert.That(board.EndDragAtWorld(finger, 1.05d), Is.True);

            Assert.That(crossings, Is.Empty, "No assisted handoff may teleport a lagging bomb to the edge.");
            Assert.That(bomb.Body.isKinematic, Is.False);
            Assert.That(bomb.Body.linearVelocity.x, Is.GreaterThan(0f), "The ordinary weighted flick remains available to travel physically.");
            Assert.That(bomb.OrbId, Is.EqualTo("heavy-lag"));
        }

        [Test]
        public void DroppingOnTheOppositeRawInTheSideBandKeepsManualCombinePriority()
        {
            var yin = Spawn("drop-yin", new Vector2(.8f, .5f));
            var yang = Spawn("drop-yang", new Vector2(.95f, .5f), LabOrbKind.Yang);
            bool contact = true;
            board.CombineRequested += (_, __, ___, ____) => contact = board.IsContactCombineRequest;
            Assert.That(config.DropCombineEnabled, Is.True);
            Begin(XY(yin.Body.position), 1d);
            Assert.That(board.EndDragAtWorld(XY(yang.Body.position), 1.1d), Is.True);

            Assert.That(combineCount, Is.EqualTo(1), "An intentional Yin/Yang drop still combines at the side of the board.");
            Assert.That(contact, Is.False, "This is the existing manual drop path, not a recovery-generated collision.");
            Assert.That(crossings, Is.Empty, "The new inside-screen assistance must not consume the Raw as a pass first.");
            Assert.That(board.Count, Is.EqualTo(2), "Both original IDs wait for the usual Host combine approval.");
            Assert.That(board.OrbIds, Is.EquivalentTo(new[] { "drop-yin", "drop-yang" }));
            Assert.That(yin.Body.isKinematic && yang.Body.isKinematic, Is.True);
        }

        [Test]
        public void APreparedDiagonalCombinedPassWinsOverAnUpwardThrow()
        {
            var orb = Spawn("combined-pass", new Vector2(.77f, .3f), LabOrbKind.Combined);
            Begin(XY(orb.Body.position), 1d);
            Assert.That(board.EndDragAtWorld(new Vector2(3.6f, 1f), 1.1d), Is.True);
            Assert.That(crossings.Count, Is.EqualTo(1));
            Assert.That(throwCount, Is.Zero);
        }

        [Test]
        public void AVerticalCombinedFlickInTheBandStillThrowsWhenNoSideIntentExists()
        {
            var orb = Spawn("vertical-throw", new Vector2(.95f, .25f), LabOrbKind.Combined);
            Vector2 from = XY(orb.Body.position);
            Begin(from, 1d);
            Assert.That(board.EndDragAtWorld(from + Vector2.up * 1.7f, 1.1d), Is.True);
            Assert.That(throwCount, Is.EqualTo(1));
            Assert.That(crossings, Is.Empty);
        }

        [Test]
        public void TurningUpwardAfterPreparingASidePassReleasesTheSideIntentAndThrows()
        {
            var orb = Spawn("turn-up", new Vector2(.8f, .25f), LabOrbKind.Combined);
            Begin(XY(orb.Body.position), 1d);
            Vector2 side = new Vector2(3.6f, -1f);
            Assert.That(board.DragToWorld(side, 1.1d), Is.True);
            Assert.That(board.PreparedTransferSide, Is.EqualTo(1));
            Assert.That(board.EndDragAtWorld(side + Vector2.up * 1.8f, 1.2d), Is.True);
            Assert.That(throwCount, Is.EqualTo(1), "A clear change to an upward attack must not be stolen by remembered side intent.");
            Assert.That(crossings, Is.Empty);
        }

        [TestCase(-1, false)]
        [TestCase(1, false)]
        [TestCase(-1, true)]
        [TestCase(1, true)]
        public void AssistedReleaseCannotBypassSwipeBlocksOrTeamOuterWalls(int side, bool teamWall)
        {
            var orb = Spawn("blocked", new Vector2(side > 0 ? .8f : .2f, .5f));
            if (teamWall) board.SetTeamWall(side);
            else board.SetSwipeBlock(side, 10f);
            Begin(XY(orb.Body.position), 1d);
            Assert.That(board.EndDragAtWorld(new Vector2(side * 3.6f, 0f), 1.1d), Is.True);

            Assert.That(crossings, Is.Empty);
            Assert.That(teamWall ? board.TeamWallBounces : board.BlockBounces, Is.EqualTo(1));
            Assert.That(orb.Body.isKinematic, Is.False);
            Assert.That(orb.Body.linearVelocity.x * side, Is.LessThanOrEqualTo(0f));
        }

        [UnityTest]
        public IEnumerator PendingAssistedTransferEmitsOnceAndRejectionReturnsTheSameGrabbableOrb()
        {
            var orb = Spawn("pending", new Vector2(.8f, .5f));
            Begin(XY(orb.Body.position), 1d);
            Assert.That(board.EndDragAtWorld(new Vector2(3.6f, 0f), 1.1d), Is.True);
            Vector3 waitingAt = orb.Body.position;
            yield return FixedSeconds(.45f);
            Assert.That(crossings.Count, Is.EqualTo(1), "Neither real edge detection nor recovery may repeat the request.");
            Assert.That(orb.Body.position, Is.EqualTo(waitingAt));
            Assert.That(board.TryBeginDragAtWorld(XY(waitingAt), 3d), Is.False, "The existing pending lock is honored.");

            Assert.That(board.RejectEdge("pending"), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(orb.OrbId, Is.EqualTo("pending"));
            Assert.That(orb.Body.isKinematic, Is.False);
            yield return FixedSeconds(.8f);
            Assert.That(orb.Body.position.x, Is.LessThan(waitingAt.x), "Once rejected, a stranded edge orb becomes easier to grab.");
            Begin(XY(orb.Body.position), 4d);
            board.CancelDrag();
            Assert.That(crossings.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AStationarySideOrbWaitsThenMovesInwardWithoutTeleporting()
        {
            var orb = Spawn("resting", new Vector2(.98f, .5f));
            Vector3 before = orb.Body.position;
            yield return FixedSeconds(.12f);
            Assert.That(Vector3.Distance(orb.Body.position, before), Is.LessThan(.001f), "The rest delay must elapse first.");
            yield return FixedSeconds(.35f);
            Assert.That(orb.Body.position.x, Is.LessThan(before.x - .01f));
            Assert.That(orb.Body.position.x, Is.GreaterThan(before.x - 1f), "Correction is gradual, not a direct position reset.");
            Assert.That(crossings, Is.Empty);
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ALowerEdgeOrbMovesUpIntoReachWithoutCreatingAThrow()
        {
            var orb = Spawn("lower", new Vector2(.5f, 0f));
            float before = orb.Body.position.y;
            yield return FixedSeconds(.8f);
            Assert.That(orb.Body.position.y, Is.GreaterThan(before + .01f));
            Assert.That(board.AreaShape.Contains(XY(orb.Body.position), orb.HitSphere.radius), Is.True);
            Assert.That(crossings, Is.Empty);
            Assert.That(throwCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RecoveryLeavesRemoteLockedAndHiddenPendingBodiesAlone()
        {
            var remote = board.UpsertOrb("remote", LabOrbKind.Yin, new Vector2(.02f, .25f), localOwner: false);
            var locked = Spawn("locked", new Vector2(.02f, .5f));
            var hidden = Spawn("hidden", new Vector2(.02f, .75f));
            Assert.That(board.SetOrbLocked("locked", true), Is.True);
            Assert.That(board.BeginPendingLaunch("hidden"), Is.True);
            Vector3 remoteAt = remote.Body.position, lockedAt = locked.Body.position, hiddenAt = hidden.Body.position;
            yield return FixedSeconds(.8f);
            Assert.That(remote.Body.position, Is.EqualTo(remoteAt));
            Assert.That(locked.Body.position, Is.EqualTo(lockedAt));
            Assert.That(hidden.Body.position, Is.EqualTo(hiddenAt));
            Assert.That(crossings, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SimulationHoldAndNonPlayingInteractionPreventRestCorrection()
        {
            var orb = Spawn("held-round", new Vector2(.98f, .5f));
            Vector3 before = orb.Body.position;
            board.SetSimulationHeld(true);
            yield return FixedSeconds(.5f);
            Assert.That(orb.Body.position, Is.EqualTo(before), "The countdown barrier holds correction as well as physics.");
            board.SetSimulationHeld(false);
            board.SetInteractionEnabled(false);
            yield return FixedSeconds(.5f);
            Assert.That(orb.Body.position, Is.EqualTo(before), "Lobby/results must not run a new correction.");
            board.SetInteractionEnabled(true);
            yield return FixedSeconds(.8f);
            Assert.That(orb.Body.position.x, Is.LessThan(before.x));
        }

        [UnityTest]
        public IEnumerator OutwardRollingAndIncomingMovementRemainPhysicalAndDoNotGetPulledBack()
        {
            var incoming = board.UpsertOrb("incoming", LabOrbKind.Yin, new Vector2(.01f, .7f), new Vector2(1.5f, 0f));
            board.UpsertOrb("outward", LabOrbKind.Yang, new Vector2(.98f, .3f), new Vector2(1.5f, 0f));
            float incomingStart = incoming.Body.position.x;
            yield return FixedSeconds(.12f);
            Assert.That(incoming.Body.position.x, Is.GreaterThan(incomingStart + .5f));
            Assert.That(incoming.Body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(crossings.Count, Is.EqualTo(1));
            Assert.That(crossings[0].OrbId, Is.EqualTo("outward"));
            Assert.That(crossings[0].VelocityInBoardWidthsPerSecond.x, Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator InwardCorrectionDoesNotStartContactCombiningEvenWhenTheExistingRuleIsEnabled()
        {
            SetCombining(true, 0f);
            var pushed = Spawn("recover-yin", new Vector2(.01f, .5f));
            Spawn("waiting-yang", new Vector2(.20f, .5f), LabOrbKind.Yang);
            int contacts = 0;
            pushed.HitOrb += (other, speed) => { if (other.OrbId == "waiting-yang") contacts++; };
            float before = pushed.Body.position.x;
            yield return FixedSeconds(1.5f);
            Assert.That(pushed.Body.position.x, Is.GreaterThan(before + .25f), "The recovery actually moved a body toward its opposite.");
            Assert.That(contacts, Is.GreaterThan(0), "The real SphereColliders must actually contact, so a zero combine count is meaningful.");
            Assert.That(combineCount, Is.Zero, "Recovery alone must not turn the configuration's contact rule into an unwanted combine.");
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(config.ContactCombineEnabled, Is.True, "The setting itself remains unchanged.");
        }

        [Test]
        public void TheMinimumGripMakesAShrunkEdgeOrbReachableWithoutChangingItsCollider()
        {
            var tiny = board.UpsertOrb("tiny", LabOrbKind.Yin, new Vector2(.99f, .5f), small: true);
            float radius = tiny.HitSphere.radius;
            Vector2 at = XY(tiny.Body.position);
            Vector2 touch = at + Vector2.left * .30f;
            Assert.That((touch - at).magnitude, Is.GreaterThan(radius * 1.25f));
            Begin(touch, 1d);
            Assert.That(tiny.HitSphere.radius, Is.EqualTo(radius));
            Assert.That(tiny.HitSphere.isTrigger, Is.True);
            board.CancelDrag();
        }

        [Test]
        public void AnExpandedTinyGripDoesNotStealATouchOnAnotherOrbsActualBody()
        {
            var tiny = board.UpsertOrb("tiny-neighbor", LabOrbKind.Yin, new Vector2(.5f, .5f), small: true);
            var whole = Spawn("whole-neighbor", new Vector2(.5625f, .5f));
            Vector2 touch = XY(tiny.Body.position) + Vector2.right * .20f;
            Assert.That(Vector2.Distance(touch, XY(tiny.Body.position)), Is.GreaterThan(tiny.HitSphere.radius));
            Assert.That(Vector2.Distance(touch, XY(whole.Body.position)), Is.LessThan(whole.HitSphere.radius),
                "The touch is physically on the whole orb, although it is closer to the tiny orb's expanded grip.");
            Begin(touch, 1d);
            Assert.That(whole.HitSphere.isTrigger, Is.True);
            Assert.That(tiny.HitSphere.isTrigger, Is.False);
            board.CancelDrag();
        }

        [UnityTest]
        public IEnumerator RecoveryFollowsTheSavedCurvedAreaInsteadOfFlatteningItsBoundary()
        {
            Assert.That(LabOrbAreaShape.TryCreate(new[] {
                new Vector2(-4f, -1f), new Vector2(0f, -2.4f), new Vector2(4f, -1f)
            }, new[] {
                new Vector2(-4f, 1.8f), new Vector2(0f, 2.4f), new Vector2(4f, 1.8f)
            }, config.OrbRadius, out LabOrbAreaShape curved, out string error), Is.True, error);
            board.ConfigureArea(config, camera, curved);
            var orb = Spawn("curve", new Vector2(.99f, .05f));
            Vector3 before = orb.Body.position;
            yield return FixedSeconds(.8f);
            Assert.That(orb.Body.position.x, Is.LessThan(before.x));
            Assert.That(board.AreaShape, Is.SameAs(curved), "The existing drawn curve is not rebuilt or inset.");
            Assert.That(curved.Contains(XY(orb.Body.position), orb.HitSphere.radius), Is.True);
        }

        private LabOrbView Spawn(string id, Vector2 position, LabOrbKind kind = LabOrbKind.Yin) =>
            board.UpsertOrb(id, kind, position, element: LabOrbElement.Fire);

        private void Begin(Vector2 point, double at) =>
            Assert.That(board.TryBeginDragAtWorld(point, at), Is.True, "The intended orb must be reachable.");

        private void SetCombining(bool enabled, float minimumSpeed = .15f)
        {
            float[] values = config.CaptureDeveloperValues();
            values[49] = minimumSpeed;
            values[51] = enabled ? 1f : 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private static IEnumerator FixedSeconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return new WaitForFixedUpdate();
        }

        private static Vector2 XY(Vector3 value) => new Vector2(value.x, value.y);
    }
}
