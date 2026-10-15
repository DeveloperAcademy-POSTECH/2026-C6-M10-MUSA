using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabCurvedOrbAreaPhysicsTests
    {
        private GameObject root;
        private LabConfig config;
        private Camera camera;
        private LabOrbBoard board;
        private LabOrbAreaShape shape;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Curved board physics fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            SetConfig("orbFloorDeceleration", 0f);
            SetConfig("orbStopSpeed", 0f);
            SetConfig("contactCombineEnabled", false);
            var cameraObject = new GameObject("Curved input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            var boardObject = new GameObject("Curved board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            shape = Shape(2.5f);
            board.ConfigureArea(config, camera, shape);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlopingTopAndBottomRimsReflectActualSpheresInTheirNormalDirection()
        {
            LabOrbView up = board.UpsertOrb("curve-up", LabOrbKind.Yin, new Vector2(.15f, .97f));
            LabOrbView down = board.UpsertOrb("curve-down", LabOrbKind.Yin, new Vector2(.85f, .03f));
            up.Body.linearVelocity = new Vector3(0f, 4f, 0f);
            down.Body.linearVelocity = new Vector3(0f, -4f, 0f);
            bool topReflected = false, bottomReflected = false;
            for (int i = 0; i < 15; i++)
            {
                yield return new WaitForFixedUpdate();
                topReflected |= up.Body.linearVelocity.y < -.1f && up.Body.linearVelocity.x > .1f;
                bottomReflected |= down.Body.linearVelocity.y > .1f && down.Body.linearVelocity.x < -.1f;
            }
            Assert.That(topReflected, Is.True, "A slope must change horizontal velocity too, unlike the old flat wall.");
            Assert.That(bottomReflected, Is.True);
            Assert.That(up.Body.position.z, Is.Zero.Within(.001f));
            Assert.That(down.Body.position.z, Is.Zero.Within(.001f));
            Transform walls = board.transform.Find("Physics walls - top and bottom");
            Assert.That(walls.GetComponentsInChildren<MeshCollider>().Length, Is.EqualTo(2));
            Assert.That(walls.GetComponentsInChildren<BoxCollider>().Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BothPortalsSendOnceAndReceiverKeepsTheCurvedPassageHeightAndSpeed()
        {
            var sent = new Dictionary<string, LabOrbEdgeCrossing>();
            board.EdgeCrossed += edge => sent.Add(edge.OrbId, edge);
            board.UpsertOrb("left", LabOrbKind.Yin, new Vector2(.001f, .25f), new Vector2(-.7f, 0f));
            board.UpsertOrb("right", LabOrbKind.Yin, new Vector2(.999f, .75f), new Vector2(.7f, 0f));
            for (int i = 0; i < 10 && sent.Count < 2; i++) yield return new WaitForFixedUpdate();
            Assert.That(sent.Count, Is.EqualTo(2));
            Assert.That(sent["left"].ToRight, Is.False);
            Assert.That(sent["right"].ToRight, Is.True);
            Assert.That(sent["left"].Height01, Is.EqualTo(.25f).Within(.025f));
            Assert.That(sent["right"].Height01, Is.EqualTo(.75f).Within(.025f));
            board.Clear();
            foreach (var edge in sent.Values)
            {
                LabOrbView received = board.UpsertOrb(edge.OrbId, LabOrbKind.Yin,
                    new Vector2(edge.ToRight ? 0f : 1f, edge.Height01), edge.VelocityInBoardWidthsPerSecond);
                Assert.That(shape.Contains(XY(received.Body.position), config.OrbRadius), Is.True);
                Assert.That(board.TryGetMotion(edge.OrbId, out Vector2 position, out Vector2 velocity), Is.True);
                Assert.That(position.y, Is.EqualTo(edge.Height01).Within(.0001f));
                Assert.That(velocity, Is.EqualTo(edge.VelocityInBoardWidthsPerSecond));
            }
        }

        [Test]
        public void CurveBackgroundRejectsNewTouchesWhileAHeldFlickStillFinishesOutside()
        {
            var orb = board.UpsertOrb("throw", LabOrbKind.Combined, new Vector2(.5f, .35f));
            Vector2 background = new Vector2(shape.CenterBounds.xMin + .01f, shape.CenterBounds.yMax);
            Assert.That(shape.Contains(background, 0f), Is.False);
            MethodInfo gate = typeof(LabOrbBoard).GetMethod("CanStartPointerAtScreen", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)gate.Invoke(board, new object[] { (Vector2)camera.WorldToScreenPoint(background) }), Is.False);
            Assert.That(board.TryBeginDragAtWorld(XY(orb.Body.position), 1d), Is.True);
            Assert.That(board.TryResizeArea(Shape(2.1f)), Is.False);
            int throws = 0;
            board.ThrowRequested += _ => throws++;
            board.DragToWorld(new Vector2(0f, .1f), 1.04d);
            board.EndDragAtWorld(new Vector2(0f, 4f), 1.1d);
            Assert.That(throws, Is.EqualTo(1));
            Assert.That(board.TryResizeArea(Shape(2.1f)), Is.True);
        }

        [Test]
        public void CurveResizePreservesIdsNormalizedMotionAndPendingOwnership()
        {
            Vector2 normalized = new Vector2(.25f, .8f), velocity = new Vector2(.1f, .05f);
            LabOrbView moving = board.UpsertOrb("moving", LabOrbKind.Yin, normalized, velocity);
            LabOrbView pending = board.UpsertOrb("locked", LabOrbKind.Combined, new Vector2(.7f, .3f));
            board.SetOrbLocked("locked", true);
            LabOrbAreaShape next = Shape(2.1f);
            Assert.That(board.TryResizeArea(next), Is.True);
            Assert.That(board.TryGetMotion("moving", out Vector2 actualPosition, out Vector2 actualVelocity), Is.True);
            Assert.That(Vector2.Distance(normalized, actualPosition), Is.LessThan(.0001f));
            Assert.That(Vector2.Distance(velocity, actualVelocity), Is.LessThan(.0001f));
            Assert.That(moving.OrbId, Is.EqualTo("moving"));
            Assert.That(pending.Body.isKinematic, Is.True);
            Assert.That(next.Contains(XY(moving.Body.position), moving.HitSphere.radius), Is.True);
            Assert.That(next.Contains(XY(pending.Body.position), pending.HitSphere.radius), Is.True);
            board.RejectEdge("locked");
            Assert.That(pending.Body.isKinematic, Is.False);
        }

        [UnityTest]
        public IEnumerator LargeDrumCanGenerateAndBeGrabbedAtEitherCurvedEnd()
        {
            foreach (float x in new[] { 0f, 1f })
            {
                board.Clear();
                LabOrbView drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(x, .95f));
                Assert.That(shape.Contains(XY(drum.Body.position), drum.HitSphere.radius), Is.True);
                Assert.That(board.TryBeginDragAtWorld(XY(drum.Body.position), 1d), Is.True);
                board.CancelDrag();
                var orb = board.UpsertOrb("new", LabOrbKind.Yin, new Vector2(x, .95f),
                    new Vector2(x == 0f ? .2f : -.2f, -.1f), playSpawn: true);
                Assert.That(shape.Contains(XY(orb.Body.position), orb.HitSphere.radius), Is.True);
                for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
                Assert.That(shape.Contains(XY(drum.Body.position), drum.HitSphere.radius - .04f), Is.True);
            }
        }

        private LabOrbAreaShape Shape(float peak)
        {
            Vector2[] lower = { new Vector2(-4f, -1f), new Vector2(-2f, -2.2f),
                new Vector2(0f, -2.5f), new Vector2(2f, -2.2f), new Vector2(4f, -1f) };
            Vector2[] upper = { new Vector2(-4f, 1f), new Vector2(-2f, 2f),
                new Vector2(0f, peak), new Vector2(2f, 2f), new Vector2(4f, 1f) };
            Assert.That(LabOrbAreaShape.TryCreate(lower, upper, config.OrbRadius, out var result, out string error), Is.True, error);
            return result;
        }

        private void SetConfig(string field, object value) => typeof(LabConfig)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        private static Vector2 XY(Vector3 point) => new Vector2(point.x, point.y);
    }
}
