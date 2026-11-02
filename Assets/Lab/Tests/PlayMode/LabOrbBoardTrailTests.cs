using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
                for (int i = 0; i < 3; i++) Assert.That(trail.Profile.StainTexture(i), Is.Not.Null, "Authored main stains must be connected.");
                for (int i = 0; i < 2; i++) Assert.That(trail.Profile.SpatterTexture(i), Is.Not.Null, "Authored small spatters must be connected.");
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

            float slow = trail.Profile.FadeInSpeed * .5f;
            float middleSpeed = Mathf.Lerp(trail.Profile.FadeInSpeed, trail.Profile.FullSpeed, .5f);
            orb.transform.position += Vector3.right * BoardRadius * slow * .02f;
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Below the movement threshold: no emission.");
            orb.transform.position += Vector3.right * BoardRadius * middleSpeed * .02f;
            trail.Step(.02f);
            float medium = trail.Intensity;
            Assert.That(medium, Is.GreaterThan(0f).And.LessThan(trail.Strength));
            orb.transform.position += Vector3.right * BoardRadius * (trail.Profile.FullSpeed + 1f) * .02f;
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.EqualTo(trail.Strength).Within(1e-5f));
            Assert.That(trail.Strength, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(trail.IntensityFor(middleSpeed), Is.EqualTo(medium).Within(1e-4f));
            Assert.That(trail.IntensityFor(slow), Is.LessThan(trail.IntensityFor(middleSpeed)));
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

        [Test]
        public void RemovingAnArtworkReferenceFromALiveProfileClearsSafelyWithoutEditingTheSourceAsset()
        {
            LabOrbDripProfile original = trail.Profile;
            LabOrbDripProfile local = Object.Instantiate(original);
            FieldInfo profileField = typeof(LabOrbBoardTrail).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo texturesField = typeof(LabOrbDripProfile).GetField("stainTextures", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(profileField, Is.Not.Null);
            Assert.That(texturesField, Is.Not.Null);
            Texture2D originalTexture = original.StainTexture(0);
            try
            {
                profileField.SetValue(trail, local);
                EmitOne();
                Assert.That(trail.On, Is.True);
                Assert.That(trail.DropCount, Is.GreaterThan(0));
                Assert.That(trail.Mesh.vertexCount, Is.GreaterThan(0));
                // Clone the array explicitly: never mutate a reference array from the source asset.
                Texture2D[] localTextures = (Texture2D[])((Texture2D[])texturesField.GetValue(local)).Clone();
                localTextures[0] = null;
                texturesField.SetValue(local, localTextures);
                Assert.That(local.HasArtwork, Is.False);
                Assert.DoesNotThrow(() => trail.Step(.02f));
                Assert.That(trail.On, Is.False);
                Assert.That(trail.DropCount, Is.Zero);
                Assert.That(trail.Mesh.vertexCount, Is.Zero);
                Assert.That(original.StainTexture(0), Is.SameAs(originalTexture));
                Assert.That(original.HasArtwork, Is.True);
            }
            finally
            {
                trail.Clear();
                profileField.SetValue(trail, original);
                Object.DestroyImmediate(local);
            }
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
        public void ExistingDropsFallThenLandSpreadAndFadeWithoutFollowingTheSourceOrAddingPhysics()
        {
            EmitOne();
            Assert.That(trail.DropCount, Is.EqualTo(1));
            Assert.That(trail.GetDropLanded(0), Is.False);
            Vector3 first = trail.GetDropPosition(0);
            float firstOpacity = trail.GetDropOpacity(0), diameter = trail.GetDropDiameter(0);
            Assert.That(first.x, Is.InRange(0f, orb.transform.position.x), "Emission starts on an actual visited segment.");
            Assert.That(diameter, Is.InRange(trail.DropDiameter * .85f, trail.DropDiameter * 1.15f));
            AssertVisualClockHeld();
            trail.Step(trail.Profile.FallDuration);
            Assert.That(trail.GetDropLanded(0), Is.True);
            Vector3 landed = trail.GetDropPosition(0);
            Assert.That(landed.x, Is.EqualTo(first.x));
            Assert.That(landed.y, Is.LessThan(first.y), "Only the brief falling phase moves the decorative drop downwards.");
            Assert.That(trail.GetDropReveal(0), Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            float reveal = trail.GetDropReveal(0);
            AssertVisualClockHeld();
            trail.Step(trail.Profile.SpreadDuration * .35f);
            Assert.That(trail.GetDropReveal(0), Is.GreaterThan(reveal));
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(landed), "A landed stain stays on the paper while it reveals.");
            Assert.That(trail.GetDropDiameter(0), Is.EqualTo(diameter), "Reveal must not inflate the entire stain quad.");
            trail.Step(trail.Profile.SpreadDuration);
            Assert.That(trail.GetDropReveal(0), Is.EqualTo(1f));
            AssertVisualClockHeld();
            float fadeStart = trail.Profile.FallDuration + trail.Profile.SpreadDuration + trail.Profile.HoldDuration;
            trail.Step(Mathf.Max(0f, fadeStart - trail.GetDropAge(0)) + trail.Profile.FadeDuration * .5f);
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(landed));
            Assert.That(trail.GetDropOpacity(0), Is.GreaterThan(0f).And.LessThan(firstOpacity));
            AssertVisualClockHeld();
            trail.Step(trail.Profile.Lifetime + .01f);
            Assert.That(trail.DropCount, Is.Zero);
            Assert.That(orb.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
            Assert.That(orb.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(orb.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        }

        [Test]
        public void BirthArtworkIsStableAndReplayingItsLocalSeedNeverConsumesGameplayRandomness()
        {
            Random.State randomBefore = Random.state;
            var variants = new List<int>();
            var diameters = new List<float>();
            var positions = new List<Vector3>();
            try
            {
                for (int run = 0; run < 2; run++)
                {
                    trail.Clear();
                    orb.transform.position = Vector3.zero;
                    trail.Sync(view, true, BoardRadius);
                    trail.Step(.001f);
                    for (int emission = 0; emission < 12; emission++)
                    {
                        orb.transform.position += Vector3.right * BoardRadius * trail.Profile.SpacingRadii;
                        // Extra travel avoids a float rounding boundary losing the next sample.
                        orb.transform.position += Vector3.right * BoardRadius * .01f;
                        trail.Step(.001f);
                        int index = trail.DropCount - 1;
                        Assert.That(index, Is.GreaterThanOrEqualTo(0));
                        int variant = trail.GetDropVariant(index);
                        Assert.That(variant, Is.InRange(0, 2));
                        Assert.That(Random.state, Is.EqualTo(randomBefore), "Presentation must not consume UnityEngine.Random.");
                        if (run == 0)
                        {
                            variants.Add(variant); diameters.Add(trail.GetDropDiameter(index));
                            positions.Add(trail.GetDropPosition(index));
                        }
                        else
                        {
                            Assert.That(variant, Is.EqualTo(variants[emission]));
                            Assert.That(trail.GetDropDiameter(index), Is.EqualTo(diameters[emission]));
                            Assert.That(trail.GetDropPosition(index), Is.EqualTo(positions[emission]));
                        }
                        int capturedVariant = trail.GetDropVariant(index);
                        trail.Step(.001f);
                        Assert.That(trail.GetDropVariant(index), Is.EqualTo(capturedVariant), "A visible stain never changes artwork between frames.");
                        Assert.That(trail.Mesh.vertexCount, Is.LessThanOrEqualTo(trail.DropCount * 8), "At most a main stain and small spatter quad per live drop.");
                    }
                }
            }
            finally { Random.state = randomBefore; }
        }

        [UnityTest]
        public IEnumerator LandedArtworkFitsTheActualCurvedPaperEvenAtItsOpenSideEdges()
        {
            LabOrbBoard board = CreateBoard();
            Assert.That(LabOrbAreaShape.TryCreate(new[] {
                new Vector2(-4f, -1.2f), new Vector2(0f, -2f), new Vector2(4f, -1.2f)
            }, new[] {
                new Vector2(-4f, 1.2f), new Vector2(0f, 2f), new Vector2(4f, 1.2f)
            }, boardConfig.OrbRadius, out LabOrbAreaShape paper, out string error), Is.True, error);
            board.ConfigureArea(boardConfig, boardRoot.GetComponentInChildren<Camera>(), paper);
            view = board.UpsertOrb("edge-stains", LabOrbKind.Combined, Vector2.one * .5f, element: LabOrbElement.Water);
            orb = view.gameObject;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null);
            trail.enabled = false;
            foreach (Vector2 side in new[] { new Vector2(0f, .01f), new Vector2(1f, .01f),
                new Vector2(0f, .99f), new Vector2(1f, .99f) })
            {
                trail.Clear();
                trail.Sync(view, true, view.HitSphere.radius * view.ComboScaleTarget);
                Vector2 start = paper.ToWorld(side, view.HitSphere.radius);
                Vector2 end = paper.ToWorld(new Vector2(side.x == 0f ? .2f : .8f, side.y), view.HitSphere.radius);
                orb.transform.position = new Vector3(start.x, start.y, orb.transform.position.z);
                trail.Step(0f);
                orb.transform.position = new Vector3(end.x, end.y, orb.transform.position.z);
                trail.Step(.001f);
                Assert.That(trail.DropCount, Is.GreaterThan(0));
                trail.Step(trail.Profile.FallDuration + trail.Profile.SpreadDuration + .001f);
                MeshFilter filter = null;
                foreach (MeshFilter candidate in orb.GetComponentsInChildren<MeshFilter>(true))
                    if (candidate.sharedMesh == trail.Mesh) { filter = candidate; break; }
                Assert.That(filter, Is.Not.Null, "Inspect the effect mesh rather than the source orb's fallback sphere.");
                foreach (Vector3 local in trail.Mesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(local);
                    Assert.That(paper.Contains(new Vector2(world.x, world.y)), Is.True,
                        "Authored artwork, including transparent padding, must fit the visible paper without changing open-side orb physics.");
                }
            }
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
                view.transform.position += Vector3.right * view.HitSphere.radius * trail.Profile.SpacingRadii * 1.1f * (i % 2 == 0 ? 1f : -1f);
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

        private void EmitOne()
        {
            trail.Sync(view, true, BoardRadius);
            trail.Step(.001f);
            orb.transform.position += Vector3.right * BoardRadius * trail.Profile.SpacingRadii * 1.1f;
            trail.Step(trail.Profile.FallDuration * .2f);
        }

        private void AssertVisualClockHeld()
        {
            Vector3 position = trail.GetDropPosition(0);
            float age = trail.GetDropAge(0), reveal = trail.GetDropReveal(0), opacity = trail.GetDropOpacity(0);
            trail.Sync(view, true, BoardRadius, true);
            orb.transform.position += Vector3.right * 10f;
            trail.Step(.2f);
            Assert.That(trail.GetDropAge(0), Is.EqualTo(age));
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(position));
            Assert.That(trail.GetDropReveal(0), Is.EqualTo(reveal));
            Assert.That(trail.GetDropOpacity(0), Is.EqualTo(opacity));
            Assert.That(trail.LastSourcePosition, Is.EqualTo(orb.transform.position), "Held time rebases the source instead of recording a teleport segment.");
            trail.Sync(view, true, BoardRadius);
            trail.Step(0f);
            Assert.That(trail.GetDropAge(0), Is.EqualTo(age));
            Assert.That(trail.GetDropPosition(0), Is.EqualTo(position));
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
