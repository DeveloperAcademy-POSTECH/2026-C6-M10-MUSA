using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Resizing the UI-backed board must not restart its local simulation or Host requests.</summary>
    public sealed class LabOrbAreaResizeTests
    {
        private static readonly Rect OriginalBounds = new Rect(-4f, -2f, 8f, 4f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private Camera inputCamera;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Lab board resize test");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Resize input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            inputCamera = cameraObject.AddComponent<Camera>();
            inputCamera.orthographic = true;
            var boardObject = new GameObject("Resize board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, inputCamera, OriginalBounds);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (config != null) UnityEngine.Object.Destroy(config);
            yield return null;
        }

        [Test]
        public void ShiftAndResizeKeepIdentityNormalizedMotionAndColliderSize()
        {
            Vector2 position = new Vector2(.25f, .4f);
            Vector2 velocity = new Vector2(.18f, .05f);
            LabOrbView orb = board.UpsertOrb("rolling", LabOrbKind.Yin, position, velocity);
            Rigidbody body = orb.Body;
            SphereCollider collider = orb.HitSphere;
            Rect resized = new Rect(-6f, 3f, 12f, 6f);

            Assert.That(board.TryResizeBounds(resized), Is.True);
            Assert.That(board.CenterBounds, Is.EqualTo(resized));
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(orb.OrbId, Is.EqualTo("rolling"));
            Assert.That(board.GetComponentInChildren<LabOrbView>(), Is.SameAs(orb));
            Assert.That(orb.Body, Is.SameAs(body));
            Assert.That(orb.HitSphere, Is.SameAs(collider));
            Assert.That(collider.radius, Is.EqualTo(config.OrbRadius).Within(.0001f));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(board.TryGetMotion("rolling", out Vector2 actualPosition, out Vector2 actualVelocity), Is.True);
            Assert.That(Vector2.Distance(actualPosition, position), Is.LessThan(.0001f));
            Assert.That(Vector2.Distance(actualVelocity, velocity), Is.LessThan(.0001f));
            Assert.That(body.position.x, Is.EqualTo(-3f).Within(.0001f));
            Assert.That(body.position.y, Is.EqualTo(5.4f).Within(.0001f));
        }

        [UnityTest]
        public IEnumerator ResizeKeepsPendingEdgeAndExplicitLocksUntilTheirNormalResolution()
        {
            LabOrbView pending = board.UpsertOrb("outgoing", LabOrbKind.Yin,
                new Vector2(.999f, .6f), new Vector2(.5f, .05f));
            LabOrbView locked = board.UpsertOrb("locked", LabOrbKind.Combined, new Vector2(.3f, .3f));
            LabOrbView remote = board.UpsertOrb("remote", LabOrbKind.Yang, new Vector2(.7f, .3f), localOwner: false);
            Assert.That(board.SetOrbLocked("locked", true), Is.True);
            int crossings = 0;
            board.EdgeCrossed += _ => crossings++;
            for (int i = 0; i < 8 && crossings == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.EqualTo(1));
            LabOrbEdgeCrossing savedEdge = EntryField<LabOrbEdgeCrossing>("outgoing", "PendingEdge");

            Assert.That(board.TryResizeBounds(new Rect(2f, 5f, 6f, 3f)), Is.True);
            Assert.That(EntryField<bool>("outgoing", "EdgePending"), Is.True);
            Assert.That(EntryField<LabOrbEdgeCrossing>("outgoing", "PendingEdge"), Is.EqualTo(savedEdge));
            Assert.That(EntryField<bool>("locked", "Locked"), Is.True);
            Assert.That(pending.Body.isKinematic, Is.True);
            Assert.That(locked.Body.isKinematic, Is.True);
            Assert.That(remote.Body.isKinematic, Is.True);
            Assert.That(board.TryBeginDragAtWorld(XY(locked.Body.position), 10d), Is.False);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.EqualTo(1), "A layout change must not emit a second ownership request.");

            Assert.That(board.RejectEdge("outgoing"), Is.True);
            Assert.That(pending.Body.isKinematic, Is.False);
            Assert.That(board.SetOrbLocked("locked", false), Is.True);
            Assert.That(locked.Body.isKinematic, Is.False);
            Assert.That(remote.Body.isKinematic, Is.True);
        }

        [Test]
        public void ResizeKeepsBothMaterialsOfAPendingCombinationLockedTogether()
        {
            LabOrbView yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.25f, .5f));
            LabOrbView yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.6f, .5f));
            Assert.That(board.TryBeginDragAtWorld(XY(yin.Body.position), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(XY(yang.Body.position), 1.2d), Is.True);
            Assert.That(EntryField<string>("yin", "PendingPartnerId"), Is.EqualTo("yang"));

            Assert.That(board.TryResizeBounds(new Rect(-6f, -1f, 12f, 3f)), Is.True);
            Assert.That(EntryField<string>("yin", "PendingPartnerId"), Is.EqualTo("yang"));
            Assert.That(EntryField<string>("yang", "PendingPartnerId"), Is.EqualTo("yin"));
            Assert.That(yin.Body.isKinematic, Is.True);
            Assert.That(yang.Body.isKinematic, Is.True);
            Assert.That(board.RejectEdge("yin"), Is.True);
            Assert.That(yin.Body.isKinematic, Is.False);
            Assert.That(yang.Body.isKinematic, Is.False);
        }

        [Test]
        public void HeldGestureDefersResizeWithoutCancellationAndCanFinishOutsideTheInputRect()
        {
            LabOrbView orb = board.UpsertOrb("held", LabOrbKind.Combined, new Vector2(.5f, .4f));
            board.SetInputScreenRect(new Rect(50f, 50f, 100f, 100f));
            Assert.That(board.TryBeginDragAtWorld(XY(orb.Body.position), 1d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(0f, .1f), 1.04d), Is.True);
            Rect requested = new Rect(-5f, 3f, 10f, 4f);
            Assert.That(board.TryResizeBounds(requested), Is.False);
            Assert.That(board.CenterBounds, Is.EqualTo(OriginalBounds));
            Assert.That(orb.Body.isKinematic, Is.True);
            Assert.That(orb.HitSphere.isTrigger, Is.True, "The held gesture must survive a deferred resize.");
            int throws = 0;
            board.ThrowRequested += _ => throws++;
            // Releasing beyond both the board and the screen-start gate still completes the held flick.
            Assert.That(board.EndDragAtWorld(new Vector2(0f, 3f), 1.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1));
            Assert.That(board.TryResizeBounds(requested), Is.True);
            Assert.That(orb.HitSphere.isTrigger, Is.False);
        }

        [UnityTest]
        public IEnumerator TopAndBottomWallsReflectAtTheNewShiftedBounds()
        {
            board.UpsertOrb("up", LabOrbKind.Yin, new Vector2(.25f, .97f), new Vector2(0f, .9f));
            board.UpsertOrb("down", LabOrbKind.Yin, new Vector2(.75f, .03f), new Vector2(0f, -.9f));
            Rect resized = new Rect(-4f, 4f, 8f, 3f);
            Assert.That(board.TryResizeBounds(resized), Is.True);
            Physics.SyncTransforms();
            int crossings = 0;
            board.EdgeCrossed += _ => crossings++;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("up", out Vector2 upPosition, out Vector2 upVelocity), Is.True);
            Assert.That(board.TryGetMotion("down", out Vector2 downPosition, out Vector2 downVelocity), Is.True);
            Assert.That(upVelocity.y, Is.LessThan(0f));
            Assert.That(downVelocity.y, Is.GreaterThan(0f));
            Assert.That(upPosition.y, Is.InRange(0f, 1f));
            Assert.That(downPosition.y, Is.InRange(0f, 1f));
            Assert.That(crossings, Is.Zero);
            Transform walls = board.transform.Find("Physics walls - top and bottom");
            Assert.That(walls.childCount, Is.EqualTo(2));
            Assert.That(walls.Find("Top").position.y,
                Is.EqualTo(resized.yMax + config.OrbRadius + .25f).Within(.001f));
            Assert.That(walls.Find("Bottom").position.y,
                Is.EqualTo(resized.yMin - config.OrbRadius - .25f).Within(.001f));
        }

        [Test]
        public void LargerDrumRetainsItsRadiusAndFitsTheNewVerticalClearance()
        {
            LabOrbView drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .95f));
            Rect resized = new Rect(-4f, 10f, 8f, 1f);
            Assert.That(board.TryResizeBounds(resized), Is.True);
            Assert.That(drum.HitSphere.radius, Is.EqualTo(config.DrumRadius).Within(.0001f));
            Assert.That(drum.Body.position.y + drum.HitSphere.radius,
                Is.LessThanOrEqualTo(resized.yMax + config.OrbRadius + .0001f));
            Assert.That(drum.Body.position.y - drum.HitSphere.radius,
                Is.GreaterThanOrEqualTo(resized.yMin - config.OrbRadius - .0001f));
            Vector3 before = drum.Body.position;
            Assert.That(board.TryResizeBounds(new Rect(-4f, 10f, 8f, .1f)), Is.False);
            Assert.That(board.CenterBounds, Is.EqualTo(resized));
            Assert.That(drum.Body.position, Is.EqualTo(before));
        }

        [Test]
        public void ScreenStartGateRejectsOutsidePointsButIncludesItsExactEdges()
        {
            MethodInfo gate = typeof(LabOrbBoard).GetMethod("CanStartPointerAtScreen", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gate, Is.Not.Null);
            bool Allowed(Vector2 point) => (bool)gate.Invoke(board, new object[] { point });
            Assert.That(Allowed(new Vector2(-50f, -50f)), Is.True, "An unset gate keeps existing test/tool behavior.");
            board.SetInputScreenRect(new Rect(20f, 30f, 100f, 80f));
            Assert.That(Allowed(new Vector2(20f, 30f)), Is.True);
            Assert.That(Allowed(new Vector2(120f, 110f)), Is.True);
            Assert.That(Allowed(new Vector2(19f, 60f)), Is.False);
            Assert.That(Allowed(new Vector2(80f, 111f)), Is.False);
            Assert.Throws<ArgumentException>(() => board.SetInputScreenRect(new Rect(0f, 0f, 0f, 100f)));
            Assert.That(board.TryResizeBounds(new Rect(0f, 0f, float.NaN, 2f)), Is.False);
            Assert.That(board.CenterBounds, Is.EqualTo(OriginalBounds));
        }

        private T EntryField<T>(string id, string field)
        {
            var entries = (IDictionary)typeof(LabOrbBoard).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
            object entry = entries[id];
            Assert.That(entry, Is.Not.Null);
            return (T)entry.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).GetValue(entry);
        }

        private static Vector2 XY(Vector3 point) => new Vector2(point.x, point.y);
    }
}
