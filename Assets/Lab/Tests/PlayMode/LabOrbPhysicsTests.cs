using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Real 3D Rigidbody/SphereCollider integration, not a mocked movement calculator.</summary>
    public sealed class LabOrbPhysicsTests
    {
        private const float Width = 8f;
        private static readonly Rect Bounds = new Rect(-4f, -2f, Width, 4f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private Camera inputCamera;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Lab physics test root");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Board camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            inputCamera = cameraObject.AddComponent<Camera>();
            inputCamera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, inputCamera, Bounds);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [Test]
        public void BoardOrbUsesA3DSphereAndAnIndependentReplaceableMeshVisual()
        {
            var orb = board.UpsertOrb("sphere", LabOrbKind.Yin, new Vector2(.5f, .5f));

            Assert.That(orb.Body, Is.SameAs(orb.GetComponent<Rigidbody>()));
            Assert.That(orb.HitSphere, Is.SameAs(orb.GetComponent<SphereCollider>()));
            Assert.That(orb.GetComponent<Rigidbody2D>(), Is.Null);
            Assert.That(orb.GetComponent<CircleCollider2D>(), Is.Null);
            Assert.That(orb.HitSphere.radius, Is.EqualTo(config.OrbRadius).Within(.001f));
            Assert.That(orb.HitSphere.isTrigger, Is.False);
            Assert.That(orb.Body.useGravity, Is.False);
            Assert.That(orb.Body.constraints & RigidbodyConstraints.FreezePositionZ,
                Is.EqualTo(RigidbodyConstraints.FreezePositionZ),
                "3D physics must stay on the existing XY interaction plane.");
            Assert.That(orb.Visual, Is.Not.Null);
            Assert.That(orb.Visual, Is.Not.SameAs(orb.transform),
                "The visual must be replaceable without changing the physics root.");
            var mesh = orb.Visual.GetComponentInChildren<MeshFilter>(true);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.sharedMesh, Is.Not.Null);
            Assert.That(orb.Visual.GetComponentInChildren<MeshRenderer>(true), Is.Not.Null);
            Assert.That(orb.Visual.GetComponentInChildren<SpriteRenderer>(true), Is.Null);
            Assert.That(orb.Visual.GetComponentInChildren<Collider>(true), Is.Null,
                "A replacement visual must not add a second gameplay hitbox.");
        }

        [UnityTest]
        public IEnumerator ReplacingOrbArtCannotIntroduce2DPhysics()
        {
            var orb = board.UpsertOrb("replaceable", LabOrbKind.Yang, new Vector2(.5f, .5f));
            var artPrefab = new GameObject("Art with legacy 2D physics");
            var artChild = new GameObject("Physics child");
            artChild.transform.SetParent(artPrefab.transform, false);
            artChild.AddComponent<CircleCollider2D>();
            artChild.AddComponent<Rigidbody2D>();

            orb.SetVisualPrefab(artPrefab);
            Object.Destroy(artPrefab);
            Collider2D artCollider = orb.Visual.GetComponentInChildren<Collider2D>(true);
            Rigidbody2D artBody = orb.Visual.GetComponentInChildren<Rigidbody2D>(true);
            if (artCollider != null) Assert.That(artCollider.enabled, Is.False);
            if (artBody != null) Assert.That(artBody.simulated, Is.False);

            yield return null; // Destroy(Component) finishes at the end of the frame.
            Assert.That(orb.Visual.GetComponentInChildren<Collider2D>(true), Is.Null);
            Assert.That(orb.Visual.GetComponentInChildren<Rigidbody2D>(true), Is.Null);
            Assert.That(orb.HitSphere.enabled, Is.True, "Replacing art must leave gameplay collision intact.");
            orb.SetVisualPrefab(null);
            Assert.That(orb.Visual.Find("Default Sphere").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator FreeRollingDeceleratesToAStopWithoutInput()
        {
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.5f, .5f), new Vector2(.125f, 0f));
            for (int i = 0; i < 55; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("yin", out _, out var velocity), Is.True);
            Assert.That(velocity.magnitude, Is.LessThanOrEqualTo(config.OrbStopSpeed + .001f));
        }

        [UnityTest]
        public IEnumerator RepeatedSnapshotsDoNotResetTheOwnersVelocity()
        {
            var orb = board.UpsertOrb("steady", LabOrbKind.Yin, new Vector2(.2f, .5f), new Vector2(1f, 0f));
            float before = orb.Body.linearVelocity.x;
            for (int i = 0; i < 8; i++)
            {
                board.UpsertOrb("steady", LabOrbKind.Yin, new Vector2(.2f, .5f), Vector2.zero);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(board.TryGetMotion("steady", out var position, out var velocity), Is.True);
            Assert.That(position.x, Is.GreaterThan(.2f));
            Assert.That(velocity.x, Is.GreaterThan(0f));
            Assert.That(orb.Body.linearVelocity.x, Is.LessThan(before),
                "Friction should reduce momentum, while repeated snapshots must not reset it.");
        }

        [UnityTest]
        public IEnumerator TopBoundaryReflectsInsteadOfTransferring()
        {
            board.UpsertOrb("up", LabOrbKind.Yin, new Vector2(.5f, .97f),
                new Vector2(0f, .9f));
            int crossings = 0;
            board.EdgeCrossed += _ => crossings++;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("up", out _, out var velocity), Is.True);
            Assert.That(velocity.y, Is.LessThan(0f));
            Assert.That(crossings, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OutwardEdgeEmitsExactlyOneTransferWithMotionAndCanRecoverAfterRejection()
        {
            var orb = board.UpsertOrb("out", LabOrbKind.Yang, new Vector2(.995f, .65f),
                new Vector2(.35f, .025f));
            int crossings = 0;
            LabOrbEdgeCrossing saved = default;
            board.EdgeCrossed += crossing => { crossings++; saved = crossing; };
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Assert.That(crossings, Is.EqualTo(1));
            Assert.That(saved.OrbId, Is.EqualTo("out"));
            Assert.That(saved.ToRight, Is.True);
            Assert.That(saved.Height01, Is.GreaterThan(.5f));
            Assert.That(saved.VelocityInBoardWidthsPerSecond.x, Is.GreaterThan(0f));
            Assert.That(orb.Body.isKinematic, Is.True);
            Assert.That(board.RejectEdge("out"), Is.True);
            Assert.That(orb.Body.isKinematic, Is.False);
            Assert.That(board.TryGetMotion("out", out _, out var speed), Is.True);
            Assert.That(speed, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator RepeatedScreenCrossingsPreserveSpeedThenFrictionStopsTheOrb()
        {
            var secondObject = new GameObject("Second board");
            secondObject.transform.SetParent(root.transform, false);
            var second = secondObject.AddComponent<LabOrbBoard>();
            second.Configure(config, inputCamera, new Rect(12f, -2f, Width, 4f));
            LabOrbBoard active = board;
            int crossings = 0;
            bool removedEveryTime = true;
            float velocityDifference = 0f;

            void Relay(LabOrbBoard source, LabOrbBoard receiver, LabOrbEdgeCrossing crossing)
            {
                crossings++;
                removedEveryTime &= source.RemoveOrb(crossing.OrbId);
                receiver.UpsertOrb(crossing.OrbId, LabOrbKind.Yang,
                    new Vector2(crossing.ToRight ? 0f : 1f, crossing.Height01),
                    crossing.VelocityInBoardWidthsPerSecond);
                receiver.TryGetMotion(crossing.OrbId, out _, out var receivedVelocity);
                velocityDifference = Mathf.Max(velocityDifference,
                    (receivedVelocity - crossing.VelocityInBoardWidthsPerSecond).magnitude);
                active = receiver;
            }

            board.EdgeCrossed += crossing => Relay(board, second, crossing);
            second.EdgeCrossed += crossing => Relay(second, board, crossing);
            board.UpsertOrb("rolling", LabOrbKind.Yang, new Vector2(.95f, .5f), new Vector2(2.8f, 0f));
            Assert.That(board.TryGetMotion("rolling", out _, out var initial), Is.True);
            Assert.That(initial.x, Is.EqualTo(2.8f).Within(.001f),
                "OrbMaxReleaseSpeed is in board widths/second, not world units/second.");

            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.GreaterThanOrEqualTo(2),
                "A strong roll should cross more than one screen before friction stops it.");
            Assert.That(removedEveryTime, Is.True);
            Assert.That(velocityDifference, Is.LessThan(.001f));
            Assert.That(active.TryGetMotion("rolling", out _, out var resting), Is.True);
            Assert.That(resting.magnitude, Is.LessThanOrEqualTo(config.OrbStopSpeed + .001f));
        }

        [UnityTest]
        public IEnumerator PhysicalContactAloneNeverCombinesButDirectYinYangDropDoes()
        {
            var yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f),
                new Vector2(.9f, 0f));
            var yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f),
                new Vector2(-.9f, 0f));
            int combines = 0;
            board.CombineRequested += (first, second, center) => combines++;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.Zero, "A 3D sphere collision must not combine materials.");

            Vector2 from = BoardPoint(yin);
            Vector2 to = BoardPoint(yang);
            Assert.That(board.TryBeginDragAtWorld(from, 1d), Is.True);
            Assert.That(board.DragToWorld(to, 1.1d), Is.True);
            Assert.That(board.EndDragAtWorld(to, 1.12d), Is.True);
            Assert.That(combines, Is.EqualTo(1));
            Assert.That(yin.Body.isKinematic, Is.True);
            Assert.That(yang.Body.isKinematic, Is.True);
            Assert.That(board.RejectEdge("yin"), Is.True);
            Assert.That(yin.Body.isKinematic, Is.False);
            Assert.That(yang.Body.isKinematic, Is.False);
        }

        [Test]
        public void OnlyCombinedFlickReleasedInsideTheBoardRequestsThrow()
        {
            board.UpsertOrb("raw", LabOrbKind.Yin, new Vector2(.4f, .3f));
            board.UpsertOrb("combined", LabOrbKind.Combined, new Vector2(.6f, .3f));
            int throws = 0;
            LabOrbThrowGesture gesture = default;
            board.ThrowRequested += value => { throws++; gesture = value; };

            Assert.That(board.TryBeginDragAtWorld(new Vector2(-.8f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(-.8f, .9f), 1.1d), Is.True);
            Assert.That(throws, Is.Zero, "A Raw orb must remain non-attackable.");

            Assert.That(board.TryBeginDragAtWorld(new Vector2(.8f, -.8f), 2d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(.8f, .9f), 2.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1));
            Assert.That(gesture.OrbId, Is.EqualTo("combined"));
            Assert.That(gesture.Velocity.y, Is.GreaterThan(config.ThrowSwipeMinSpeed));
            Assert.That(gesture.ReleaseX01, Is.EqualTo(.6f).Within(.001f));
            Assert.That(gesture.DistanceInBoardWidths, Is.GreaterThan(config.ThrowSwipeMinDistance));
        }

        [Test]
        public void SlowOrSidewaysCombinedMovementDoesNotThrow()
        {
            board.UpsertOrb("slow", LabOrbKind.Combined, new Vector2(.5f, .35f));
            board.UpsertOrb("side", LabOrbKind.Combined, new Vector2(.5f, .65f));
            int throws = 0;
            board.ThrowRequested += _ => throws++;

            Assert.That(board.TryBeginDragAtWorld(new Vector2(0f, -.6f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(0f, .2f), 1.5d), Is.True);
            Assert.That(board.TryBeginDragAtWorld(new Vector2(0f, .6f), 2d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(1.5f, .7f), 2.1d), Is.True);
            Assert.That(throws, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OutwardCombinedFlickTransfersBeforeThrowing()
        {
            board.UpsertOrb("edge-combined", LabOrbKind.Combined, new Vector2(.9f, .3f));
            int throws = 0;
            int crossings = 0;
            board.ThrowRequested += _ => throws++;
            board.EdgeCrossed += crossing =>
            {
                Assert.That(crossing.OrbId, Is.EqualTo("edge-combined"));
                Assert.That(crossing.ToRight, Is.True);
                crossings++;
            };

            Assert.That(board.TryBeginDragAtWorld(new Vector2(3.2f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(4.5f, 1.1f), 1.04d), Is.True);
            for (int i = 0; i < 8 && crossings == 0; i++) yield return new WaitForFixedUpdate();

            Assert.That(throws, Is.Zero, "An outward side exit takes precedence over a Combined throw.");
            Assert.That(crossings, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewGeneratedRawRisesOnceBeforeBecomingMovable()
        {
            LabOrbView orb = board.UpsertOrb("generated", LabOrbKind.Yin,
                new Vector2(.5f, .5f), playSpawnRise: true);
            Assert.That(orb.IsSpawning, Is.True);
            Assert.That(orb.Body.isKinematic, Is.True);
            Assert.That(orb.HitSphere.enabled, Is.False);
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(orb), 1d), Is.False);
            Assert.That(orb.Visual.localPosition.y, Is.LessThan(0f));

            yield return new WaitForSeconds(config.SpawnRiseDuration + .08f);
            Assert.That(orb.IsSpawning, Is.False);
            Assert.That(orb.Visual.localPosition.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(orb.HitSphere.enabled, Is.True);
            Assert.That(orb.Body.isKinematic, Is.False);
            board.UpsertOrb("generated", LabOrbKind.Yin, new Vector2(.5f, .5f), playSpawnRise: true);
            Assert.That(orb.IsSpawning, Is.False, "Repeated snapshots must not replay the rise.");
            Assert.That(board.RemoveOrb("generated"), Is.True);
            LabOrbView returned = board.UpsertOrb("generated", LabOrbKind.Yin,
                new Vector2(.5f, .5f), playSpawnRise: true);
            Assert.That(returned.IsSpawning, Is.False, "Returning with the same ID must not replay the rise.");
        }

        private static Vector2 BoardPoint(LabOrbView orb)
        {
            Vector3 position = orb.Body.position;
            return new Vector2(position.x, position.y);
        }
    }
}
