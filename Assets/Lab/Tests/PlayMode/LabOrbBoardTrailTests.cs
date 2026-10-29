using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Board-only paint drops: actual movement, pigment, captured size, visual time and owner-board recovery.</summary>
    public sealed class LabOrbBoardTrailTests
    {
        private const float BoardRadius = .32f;
        private GameObject orb;
        private LabOrbView view;
        private LabOrbBoardTrail trail;
        private LabOrbAppearance look;
        private GameObject boardRoot;
        private LabConfig boardConfig;

        [SetUp]
        public void MakeOrb()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config, Is.Not.Null);
            look = config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            orb = new GameObject("Board drip orb");
            view = orb.AddComponent<LabOrbView>();
            view.Configure("drip-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Fire);
            view.ConfigureAppearance(look);
            trail = orb.AddComponent<LabOrbBoardTrail>();
            trail.enabled = false; // Only the test advances this emitter's visual clock.
        }

        [TearDown]
        public void DestroyOrb()
        {
            if (orb != null) Object.DestroyImmediate(orb);
            if (boardRoot != null) Object.DestroyImmediate(boardRoot);
            if (boardConfig != null) Object.DestroyImmediate(boardConfig);
        }

        [Test]
        public void ACombinedOrbUsesItsPaintPigmentAndBoardSizeWithoutEditingAnyFlightProfile()
        {
            var flightBefore = new Dictionary<LabOrbElement, string>();
            foreach (LabOrbElement element in new[] { LabOrbElement.Fire, LabOrbElement.Water,
                LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth })
            {
                LabElementTrailProfile flight = look.ElementTrailFor(element);
                Assert.That(flight, Is.Not.Null);
                flightBefore.Add(element, JsonUtility.ToJson(flight));
            }
            string appearanceBefore = JsonUtility.ToJson(look);
            foreach (LabOrbElement element in flightBefore.Keys)
            {
                view.Configure("drip-orb", LabOrbKind.Combined, BoardRadius, element);
                trail.Sync(view, true, BoardRadius);
                Assert.That(trail.On, Is.True);
                Assert.That(trail.Element, Is.EqualTo(element));
                Assert.That(trail.Pigment, Is.EqualTo(LabPaintPalette.ColorFor(element)), "Use the actual paint palette, not the old flight ink tint.");
                Assert.That(trail.Profile, Is.Not.Null);
                Assert.That(trail.Profile, Is.SameAs(Resources.Load<LabOrbDripProfile>("OrbBoard/BoardPaintDrips")));
                Assert.That(trail.DropDiameter, Is.EqualTo(2f * BoardRadius * trail.Profile.DiameterScale).Within(1e-5f));
                Assert.That(trail.DropCount, Is.Zero);
            }
            foreach (var item in flightBefore)
                Assert.That(JsonUtility.ToJson(look.ElementTrailFor(item.Key)), Is.EqualTo(item.Value), "Board art must not edit a shared flight profile.");
            Assert.That(JsonUtility.ToJson(look), Is.EqualTo(appearanceBefore));
            Assert.That(orb.GetComponentsInChildren<LabElementFlightTrail>(true), Is.Empty, "The new board effect does not attach the old flying-orb ribbons.");
        }

        [Test]
        public void ItShowsOnlyWhileMovingAndDeepensWithSpeed()
        {
            trail.Sync(view, true, BoardRadius);
            trail.Step(.02f); // The first frame establishes a source anchor.
            Assert.That(trail.Intensity, Is.Zero);
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Still: no new drops.");
            Assert.That(trail.DropCount, Is.Zero);

            orb.transform.position += Vector3.right * BoardRadius * .02f; // One radius per second.
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Below the movement threshold: no emission.");
            orb.transform.position += Vector3.right * BoardRadius * 5f * .02f;
            trail.Step(.02f);
            float medium = trail.Intensity;
            Assert.That(medium, Is.GreaterThan(0f).And.LessThan(trail.Strength));
            orb.transform.position += Vector3.right * BoardRadius * 20f * .02f;
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.EqualTo(trail.Strength).Within(1e-5f));
            Assert.That(trail.Strength, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(trail.IntensityFor(5f), Is.EqualTo(medium).Within(1e-4f));
            Assert.That(trail.IntensityFor(3f), Is.LessThan(trail.IntensityFor(6f)));
        }

        [Test]
        public void RawOrbsDevOffAndElementChangesClearPreviouslyEmittedDrops()
        {
            trail.Sync(view, false, BoardRadius);
            Assert.That(trail.On, Is.False);
            trail.Sync(view, true, BoardRadius);
            trail.Step(.02f);
            orb.transform.position += Vector3.right * BoardRadius * 2f;
            trail.Step(.1f);
            Assert.That(trail.DropCount, Is.GreaterThan(0));
            foreach (LabOrbKind kind in new[] { LabOrbKind.Yin, LabOrbKind.Yang })
            {
                view.Configure("drip-orb", kind, BoardRadius);
                trail.Sync(view, true, BoardRadius);
                Assert.That(trail.On, Is.False, "Raw Yin/Yang orbs have no paint-drop trail.");
                Assert.That(trail.DropCount, Is.Zero);
                Assert.That(trail.Mesh == null || trail.Mesh.vertexCount == 0, Is.True);
            }
            view.Configure("drip-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Water);
            trail.Sync(view, true, BoardRadius);
            Assert.That(trail.Element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(trail.Pigment, Is.EqualTo(LabPaintPalette.ColorFor(LabOrbElement.Water)));
            Assert.That(trail.DropCount, Is.Zero);
            trail.Step(.02f);
            orb.transform.position += Vector3.right * BoardRadius * 2f;
            trail.Step(.1f);
            Assert.That(trail.DropCount, Is.GreaterThan(0));
            view.Configure("drip-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Metal);
            trail.Sync(view, true, BoardRadius);
            Assert.That(trail.Element, Is.EqualTo(LabOrbElement.Metal));
            Assert.That(trail.DropCount, Is.Zero, "Old Water drops cannot be recolored into a new element.");
            trail.Sync(view, false, BoardRadius);
            Assert.That(trail.On, Is.False);
            Assert.That(trail.DropCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PendingLaunchStaysClearUntilRestoredAtItsApprovedBoardPosition()
        {
            LabOrbBoard board = CreateBoard();
            view = board.UpsertOrb("pending-drips", LabOrbKind.Combined, new Vector2(.3f, .5f), element: LabOrbElement.Fire);
            orb = view.gameObject;
            Rigidbody originalBody = view.Body;
            Vector3 approvedPosition = view.Body.position;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null, "The real board attaches the effect in LateUpdate.");
            Assert.That(trail.On, Is.True);

            Assert.That(board.BeginPendingLaunch(view.OrbId), Is.True);
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            view.Body.position = approvedPosition + Vector3.right;
            yield return null;
            yield return null;
            Assert.That(trail.On, Is.False, "A pending hidden body cannot restart its emitter.");
            Assert.That(trail.DropCount, Is.Zero);
            Assert.That(board.RejectPendingLaunch(view.OrbId), Is.True);
            yield return null;
            Assert.That(view.Body, Is.SameAs(originalBody));
            Assert.That(view.OrbId, Is.EqualTo("pending-drips"));
            Assert.That(view.Body.position, Is.EqualTo(approvedPosition));
            Assert.That(trail.On, Is.True);
            Assert.That(trail.DropCount, Is.Zero, "Restore starts empty rather than connecting to the hidden body's location.");
            Assert.That(trail.LastSourcePosition, Is.EqualTo(approvedPosition));
            Assert.That(trail.Intensity, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ABlastShrunkCombinedOrbChangesFutureDiameterButKeepsCapturedDropsAndFlightProfiles()
        {
            LabOrbBoard board = CreateBoard();
            LabElementTrailProfile authored = look.ElementTrailFor(LabOrbElement.Fire);
            Assert.That(authored, Is.Not.Null);
            string flightBefore = JsonUtility.ToJson(authored);
            view = board.UpsertOrb("shrinking-drips", LabOrbKind.Combined, Vector2.one * .5f, element: LabOrbElement.Fire);
            orb = view.gameObject;
            Rigidbody originalBody = view.Body;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null);
            trail.enabled = false;
            trail.Sync(view, true, view.HitSphere.radius * view.ComboScaleTarget);
            trail.Step(.02f);
            view.transform.position += Vector3.right * boardConfig.OrbRadius * 2f;
            trail.Step(.1f);
            Assert.That(trail.DropCount, Is.GreaterThan(0));
            int count = trail.DropCount;
            float capturedDiameter = trail.GetDropDiameter(0), originalDiameter = trail.DropDiameter;

            board.UpsertOrb(view.OrbId, LabOrbKind.Combined, Vector2.one * .5f, element: LabOrbElement.Fire, small: true);
            yield return null;
            Assert.That(view.HitSphere.radius, Is.LessThan(boardConfig.OrbRadius));
            float artRadius = view.HitSphere.radius * view.ComboScaleTarget;
            Assert.That(trail.DropDiameter, Is.EqualTo(2f * artRadius * trail.Profile.DiameterScale).Within(1e-5f));
            Assert.That(trail.DropDiameter, Is.LessThan(originalDiameter));
            Assert.That(trail.DropCount, Is.EqualTo(count));
            Assert.That(trail.GetDropDiameter(0), Is.EqualTo(capturedDiameter), "Existing drops retain the size captured at emission.");
            Assert.That(view.Body, Is.SameAs(originalBody));
            Assert.That(JsonUtility.ToJson(authored), Is.EqualTo(flightBefore));
            board.UpsertOrb(view.OrbId, LabOrbKind.Combined, Vector2.one * .5f, element: LabOrbElement.Fire, small: false);
            yield return null;
            Assert.That(trail.DropDiameter, Is.EqualTo(originalDiameter).Within(1e-5f));
            Assert.That(trail.GetDropDiameter(0), Is.EqualTo(capturedDiameter));
        }

        [Test]
        public void ExistingDropsFallInWorldSpaceThenFadeWithoutFollowingTheSourceOrAddingPhysics()
        {
            trail.Sync(view, true, BoardRadius);
            trail.Step(.02f);
            orb.transform.position = Vector3.right * BoardRadius * 2f;
            trail.Step(.1f);
            Assert.That(trail.DropCount, Is.GreaterThan(0));
            int count = trail.DropCount;
            Vector3 first = trail.GetDropPosition(0);
            float firstOpacity = trail.GetDropOpacity(0), diameter = trail.GetDropDiameter(0);
            Assert.That(first.x, Is.InRange(0f, orb.transform.position.x), "Emission starts on an actual visited segment.");
            Assert.That(diameter, Is.InRange(trail.DropDiameter * .85f, trail.DropDiameter * 1.15f));
            trail.Sync(view, true, BoardRadius, true);
            orb.transform.position += Vector3.right * 10f;
            trail.Step(.2f);
            Assert.That(trail.DropCount, Is.EqualTo(count));
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(first));
            Assert.That(trail.GetDropOpacity(0), Is.EqualTo(firstOpacity));
            Assert.That(trail.LastSourcePosition, Is.EqualTo(orb.transform.position), "A held clock rebases the source instead of collecting a teleport segment.");
            trail.Sync(view, true, BoardRadius);
            trail.Step(0f);
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(first));
            Assert.That(trail.GetDropOpacity(0), Is.EqualTo(firstOpacity));
            trail.Step(.15f);
            Assert.That(trail.GetDropPosition(0).x, Is.EqualTo(first.x));
            Assert.That(trail.GetDropPosition(0).y, Is.LessThan(first.y), "Gravity-like visual fall points down the board's world -Y axis.");
            Assert.That(trail.GetDropDiameter(0), Is.EqualTo(diameter));
            Assert.That(trail.GetDropOpacity(0), Is.LessThanOrEqualTo(firstOpacity));
            trail.Step(trail.Profile.Lifetime * .6f);
            Assert.That(trail.DropCount, Is.GreaterThan(0));
            Assert.That(trail.GetDropOpacity(0), Is.LessThan(firstOpacity));
            trail.Step(trail.Profile.Lifetime + .01f);
            Assert.That(trail.DropCount, Is.Zero);
            Assert.That(orb.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
            Assert.That(orb.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(orb.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheRealBoardHoldFreezesExistingDropsAndTheEmitterIsBoundedAndClearsWhenStopped()
        {
            LabOrbBoard board = CreateBoard();
            view = board.UpsertOrb("bounded-drips", LabOrbKind.Combined, Vector2.one * .5f, element: LabOrbElement.Fire);
            orb = view.gameObject;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null);
            trail.enabled = false;
            trail.Sync(view, true, view.HitSphere.radius * view.ComboScaleTarget);
            trail.Step(.02f);
            for (int i = 0; i < trail.Profile.MaxDrops * 3; i++)
            {
                view.transform.position += Vector3.right * view.HitSphere.radius * .9f * (i % 2 == 0 ? 1f : -1f);
                trail.Step(.001f);
                Assert.That(trail.DropCount, Is.LessThanOrEqualTo(trail.Profile.MaxDrops));
            }
            Assert.That(trail.DropCount, Is.EqualTo(trail.Profile.MaxDrops));
            Vector3 first = trail.GetDropPosition(0);
            float opacity = trail.GetDropOpacity(0);
            board.SetSimulationHeld(true);
            yield return null; // The production Board Sync forwards SimulationHeld; global Time.timeScale stays unchanged.
            trail.Step(.2f);
            Assert.That(trail.DropCount, Is.EqualTo(trail.Profile.MaxDrops));
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(first));
            Assert.That(trail.GetDropOpacity(0), Is.EqualTo(opacity));
            board.SetSimulationHeld(false);
            yield return null;
            trail.Step(trail.Profile.Lifetime + .01f);
            Assert.That(trail.DropCount, Is.Zero, "Stationary sources stop emitting and old drops expire independently.");
            Assert.That(trail.Intensity, Is.Zero);
            trail.Clear();
            Assert.That(trail.On, Is.False);
            Assert.That(trail.Mesh == null || trail.Mesh.vertexCount == 0, Is.True);
        }

        private LabOrbBoard CreateBoard()
        {
            Object.DestroyImmediate(orb);
            orb = null;
            boardConfig = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            boardRoot = new GameObject("Board drip integration");
            var cameraObject = new GameObject("Board drip camera");
            cameraObject.transform.SetParent(boardRoot.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.enabled = false;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(boardRoot.transform, false);
            var board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(boardConfig, camera, new Rect(-4f, -2f, 8f, 4f));
            board.SetInteractionEnabled(false);
            return board;
        }
    }
}
