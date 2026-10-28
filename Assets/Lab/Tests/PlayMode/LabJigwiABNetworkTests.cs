using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Real owner-filtered projectile queries and replication fixtures, not remote sockets or device playability.</summary>
    public sealed class LabJigwiABNetworkTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        // Keep isolation from the saved scene without losing camera round-trip precision at kilometre coordinates.
        private static readonly Vector3 Arena = new Vector3(0f, 61.01f, 0f);
        private GameObject root;
        private LabNetwork network;
        private LabTarget target;
        private LabConfig config;
        private LabModel model;
        private int serial;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi AB network fixture");
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Assert.That(source, Is.Not.Null);
            config = Object.Instantiate(source); // Keep the actual saved game throw profile (g30 / speed12.5), not code defaults.
            float[] values = config.CaptureDeveloperValues();
            values[3] = 1000f; values[4] = 100f; values[5] = 1f; values[6] = 0f; values[7] = 1f;
            values[9] = 100f; values[10] = 120f; values[155] = values[184] = values[166] = values[207] = values[208] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            network = root.AddComponent<LabNetwork>();
            network.enabled = false; // No discovery, socket Host or real-time model ticks in this fixture.
            network.config = config;
            target = LabTarget.CreateCylinder(root.transform, Arena, .8f, 2f);
            network.target = target;
            model = Room(LabStageYokai.Jigwi);
            Field("model").SetValue(network, model);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllThreeIgnitionsAreReachableByRealBoardGesturesWithTheSavedArcAndStraightProfiles()
        {
            var cameraObject = new GameObject("Legal flick board camera"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.transform.position = Vector3.back * 10f;
            var boardObject = new GameObject("Real input measurement board"); boardObject.transform.SetParent(root.transform, false);
            var board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            LabConfig saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config.ThrowGravity, Is.EqualTo(saved.ThrowGravity));
            Assert.That(config.ThrowMaxWorldSpeed, Is.EqualTo(saved.ThrowMaxWorldSpeed));
            Assert.That(config.ThrowForwardGain, Is.EqualTo(saved.ThrowForwardGain));
            Assert.That(config.ThrowLateralGain, Is.EqualTo(saved.ThrowLateralGain));
            Assert.That(config.ThrowPowerExponent, Is.EqualTo(saved.ThrowPowerExponent));
            yield return null; // Retire the target's temporary primitive collider before any flight.
            int completed = 0;
            foreach (LabThrowForm form in new[] { LabThrowForm.Arc, LabThrowForm.Straight })
            {
                float[] values = config.CaptureDeveloperValues();
                values[111] = (float)form;
                values[244] = 3f;
                values[248] = .1f; // Maximum existing motion setting: no enlarged target-radius workaround.
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                foreach (int count in new[] { 3, 5 })
                {
                    ulong[] owners = Enumerable.Range(0, count).Select(index => (ulong)(index * 10)).ToArray();
                    model = new LabModel();
                    model.Begin("legal-saved-profile-" + form + "-" + count, owners, 0d, config, LabStageYokai.Jigwi);
                    LabSnapshot normal = model.Snapshot(31.2d), reduced = model.Snapshot(43.2d);
                    foreach (LabPlayerState player in normal.players)
                        for (int pointIndex = 0; pointIndex < 3; pointIndex++)
                        {
                            bool small = (player.seat + pointIndex) % 2 == 0;
                            LabSnapshot sample = LabBoardReplica.CopySnapshot(small ? reduced : normal);
                            sample.serverTime += player.seat % 2 == 0 ? 0d : .75d;
                            sample.paused = true; // Hold the target-motion sample; this fixture never changes global Time.timeScale.
                            Field("latest").SetValue(network, sample); Sync();
                            LabJigwiABRound active = sample.jigwiAB.Active(sample.serverTime);
                            LabJigwiABFireTarget expected = active.players.Single(owner => owner.playerId == player.id).targets[pointIndex];
                            Assert.That(active.players.All(owner => owner.targets.All(fire => fire.lit)), Is.True,
                                "Every target stays lit: a neighbour cannot be disabled to make the desired ID pass.");
                            float releaseX = Mathf.Lerp(.15f, .85f, pointIndex / 2f);
                            LabOrbThrowGesture gesture = CaptureRealBoardGesture(board, releaseX, 2.8f, 100d + completed);
                            Assert.That(LabThrowMath.IsThrowGesture(gesture.Velocity, gesture.DistanceInBoardWidths, config), Is.True);
                            Assert.That(gesture.DistanceInBoardWidths, Is.GreaterThanOrEqualTo(config.ThrowSwipeMinDistance));
                            Vector3 origin = LabThrowMath.LaunchPosition(player.seat, count, Arena, gesture.ReleaseX01, config);
                            Vector3 anchor = LabThrowMath.SeatPosition(player.seat, count, Arena,
                                config.ThrowOriginRadius, config.ThrowOriginHeightOffset, true);
                            Vector3 velocity = LabThrowMath.LaunchVelocity(gesture.Velocity, anchor, Arena, config);
                            Assert.That(network.TryGetJigwiABFireWorldPosition(active, expected, player.id, pointIndex,
                                sample.serverTime, out Vector3 aim), Is.True);
                            Assert.That(aim.y - Arena.y, Is.LessThan(.8f), "The old camera-elevated row was above the saved profile's reachable band.");
                            var shot = LabProjectile.Create(origin, config, root.transform, LabOrbElement.Water);
                            if (small) shot.SetJigwiABRadiusScale(active.shrinkScale);
                            shot.SetFireSurfaces(Surfaces().Values.Where(surface => surface.OwnerPlayerId == player.id));
                            bool received = false; LabProjectileResult result = default;
                            string label = form + " / players=" + count + " / seat=" + player.seat + " / point=" + pointIndex + " / small=" + small;
                            Assert.That(shot.Launch("legal-" + completed, player.id, velocity, config, target,
                                contact => { received = true; result = contact; }), Is.True);
                            for (int step = 0; step < 60 && !received; step++) yield return new WaitForFixedUpdate();
                            Assert.That(received, Is.True, label);
                            Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Fire), label);
                            Assert.That(result.FireId, Is.EqualTo(active.id), label);
                            Assert.That(result.FireTargetId, Is.EqualTo(expected.id),
                                label + " must hit the exact point, not the wall or an overlapping neighbour.");
                            completed++;
                            if (shot != null) { shot.gameObject.SetActive(false); Object.Destroy(shot.gameObject); }
                        }
                }
            }
            Assert.That(completed, Is.EqualTo(48));
            TestContext.WriteLine("JIGWI_LEGAL_REACH cases=" + completed
                + " board=TryBeginDrag/DragTo/EndDrag sourceGravity=" + saved.ThrowGravity
                + " maxWorld=" + saved.ThrowMaxWorldSpeed + " maxMotion=.1 targetRadius=" + config.JigwiABFireTargetRadius);
        }

        [UnityTest]
        public IEnumerator AnActualAuthoritativeProjectileReachesTheOwnersScreenFireAndConsumesWithoutBossRewards()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            LabJigwiABFireTarget fire = active.players.Single(player => player.playerId == 0).targets[1];
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, 0, 1, snapshot.serverTime, out Vector3 point), Is.True);
            LabOrbState orb = Add(model, 0, snapshot.serverTime, LabOrbElement.Water);
            Assert.That(model.TryLaunch(0, orb.id, "actual-ab-launch", snapshot.serverTime, out _, out string error), Is.True, error);
            int beforeHits = snapshot.players.Single(player => player.id == 0).hitTotal;
            float beforeStamina = snapshot.players.Single(player => player.id == 0).stamina;
            int beforePaint = model.PaintSurface.CoveredCount;
            yield return null;

            bool received = false, accepted = false, extinguished = false;
            LabProjectileResult result = default;
            LabProjectile shot = Shot();
            shot.SetFireSurfaces(Surfaces().Values.Where(surface => surface.OwnerPlayerId == 0));
            shot.Launch(orb.id, 0, VelocityTo(point, .06f), config, target, contact =>
            {
                received = true; result = contact;
                if (contact.Kind == LabProjectileHitKind.Fire)
                    accepted = model.ConfirmJigwiABFireHit(contact.ProjectileId, contact.FireId,
                        contact.FireTargetId, snapshot.serverTime, out extinguished);
            });
            for (int i = 0; i < 60 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received, Is.True);
            Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Fire));
            Assert.That(result.Hit, Is.False);
            Assert.That(result.FireId, Is.EqualTo(active.id));
            Assert.That(accepted && extinguished, Is.True);
            LabSnapshot after = model.Snapshot(snapshot.serverTime);
            Assert.That(after.orbs.Any(item => item.id == orb.id), Is.False);
            Assert.That(after.jigwiAB.Active(snapshot.serverTime).players.Single(player => player.playerId == 0)
                .targets.Single(item => item.id == result.FireTargetId).lit, Is.False);
            Assert.That(after.players.Single(player => player.id == 0).hitTotal, Is.EqualTo(beforeHits));
            Assert.That(after.players.Single(player => player.id == 0).stamina, Is.EqualTo(beforeStamina));
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(beforePaint));
        }

        [UnityTest]
        public IEnumerator AnotherPlayersRealFireCollidersCannotCaptureTheSameFlight()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            LabJigwiABFireTarget fire = active.players.Single(player => player.playerId == 0).targets[1];
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, 0, 1, snapshot.serverTime, out Vector3 point), Is.True);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = Arena + Vector3.back * 2f;
            wall.transform.localScale = new Vector3(30f, 30f, .1f);
            yield return null;
            bool received = false; LabProjectileResult result = default;
            LabProjectile shot = Shot();
            shot.SetMissSurface(wall.GetComponent<Collider>());
            // Deliberately register foreign colliders in the shot's path: ownership must still
            // be checked by the production projectile, not merely by a correct caller list.
            shot.SetFireSurfaces(Surfaces().Values.Where(surface => surface.OwnerPlayerId == 0));
            shot.Launch("foreign-fire-flight", 20, VelocityTo(point, .06f), config, target,
                contact => { received = true; result = contact; });
            for (int i = 0; i < 60 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received, Is.True);
            Assert.That(result.Kind, Is.Not.EqualTo(LabProjectileHitKind.Fire));
            Assert.That(result.FireId, Is.Null);
            Assert.That(active.players.Single(player => player.playerId == 0).targets.All(item => item.lit), Is.True);
        }

        [UnityTest]
        public IEnumerator WaterHittingTheEmptyCurtainRectangleOnlySplashesAndReplicatesItsTimestamp()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            Assert.That(network.TryGetJigwiABCurtainFrame(active, 0, snapshot.serverTime,
                out Vector3 center, out Quaternion rotation, out Vector2 half), Is.True);
            string curtainId = LabJigwiABFireMotion.CurtainId(active.id, 0);
            LabJigwiFireSurface curtain = Surfaces()[curtainId];
            Assert.That(curtain.IsCurtain, Is.True);
            Assert.That(curtain.Hitbox, Is.TypeOf<BoxCollider>());
            Vector3 aim = center + rotation * Vector3.right * (half.x * .7f)
                - rotation * Vector3.up * (half.y * .6f);
            LabOrbState orb = Add(model, 0, snapshot.serverTime, LabOrbElement.Water);
            Assert.That(model.TryLaunch(0, orb.id, "actual-curtain-water", snapshot.serverTime, out _, out string error), Is.True, error);
            int beforeHits = snapshot.players.Single(player => player.id == 0).hitTotal;
            float beforeStamina = snapshot.players.Single(player => player.id == 0).stamina;
            yield return null;
            bool received = false, accepted = false, extinguished = true;
            LabProjectileResult result = default;
            LabProjectile shot = Shot();
            shot.SetFireSurfaces(Surfaces().Values.Where(surface => surface.OwnerPlayerId == 0));
            shot.Launch(orb.id, 0, VelocityTo(aim, .06f), config, target, contact =>
            {
                received = true; result = contact;
                if (contact.Kind == LabProjectileHitKind.Fire)
                    accepted = model.ConfirmJigwiABFireHit(contact.ProjectileId, contact.FireId,
                        contact.FireTargetId, snapshot.serverTime, out extinguished);
            });
            for (int i = 0; i < 60 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received && accepted, Is.True);
            Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Fire));
            Assert.That(result.FireTargetId, Is.EqualTo(curtainId), "An empty part of the wall cannot count as an ignition point.");
            Assert.That(extinguished, Is.False);
            LabSnapshot after = model.Snapshot(snapshot.serverTime);
            LabJigwiABPlayerFire owner = after.jigwiAB.Active(snapshot.serverTime).players.Single(player => player.playerId == 0);
            Assert.That(owner.targets.All(fire => fire.lit), Is.True);
            Assert.That(owner.lastWaterSplashAt, Is.EqualTo(snapshot.serverTime));
            Assert.That(after.orbs.Any(value => value.id == orb.id), Is.False);
            Assert.That(after.players.Single(player => player.id == 0).hitTotal, Is.EqualTo(beforeHits));
            Assert.That(after.players.Single(player => player.id == 0).stamina, Is.EqualTo(beforeStamina));
            Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            var publisher = new LabBoardPublisher();
            LabBoardFrame frame = publisher.Build(after, 0, 1, 1);
            var replica = new LabBoardReplica(); replica.SetActive(after.sessionId, after.round, 0);
            Assert.That(replica.TryApply(frame, out error), Is.True, error);
            var oldPaint = new LabSnapshot { sessionId = after.sessionId, round = after.round,
                phase = LabPhase.Playing, serverTime = 30d };
            Assert.That(replica.Overlay(oldPaint).jigwiAB.Active(snapshot.serverTime).players
                .Single(player => player.playerId == 0).lastWaterSplashAt, Is.EqualTo(snapshot.serverTime));
        }

        [UnityTest]
        public IEnumerator TheWholeCurtainStaysClosedUntilTheLastLocalPointThenLetsARealBossShotThrough()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            string curtainId = LabJigwiABFireMotion.CurtainId(active.id, 0);
            LabJigwiFireSurface curtain = Surfaces()[curtainId];
            Collider originalCollider = curtain.Hitbox;
            Vector3 fullSize = ((BoxCollider)curtain.Hitbox).size;
            string[] points = active.players.Single(player => player.playerId == 0).targets.Select(fire => fire.id).ToArray();
            for (int i = 0; i < points.Length; i++)
            {
                double at = 31.2d + i * .1d;
                ExtinguishPoint(0, active.id, points[i], at);
                snapshot = Install(at);
                Assert.That(Surfaces()[curtainId], Is.SameAs(curtain));
                Assert.That(curtain.Hitbox, Is.SameAs(originalCollider));
                Assert.That(((BoxCollider)curtain.Hitbox).size, Is.EqualTo(fullSize));
                bool closed = i < points.Length - 1;
                Assert.That(curtain.Active, Is.EqualTo(closed));
                Assert.That(model.JigwiABBlocksBoss(0, at), Is.EqualTo(closed));
            }
            Assert.That(model.JigwiABBlocksBoss(20, snapshot.serverTime), Is.True,
                "The other player's still-lit curtain is independent of this local opening.");
            LabOrbState orb = Add(model, 0, snapshot.serverTime, LabOrbElement.Water);
            Assert.That(model.TryLaunch(0, orb.id, "open-curtain-boss-flight", snapshot.serverTime, out _, out string error), Is.True, error);
            yield return null;
            bool received = false, approved = false; LabProjectileResult result = default;
            LabProjectile shot = Shot();
            shot.SetFireSurfaces(Surfaces().Values.Where(surface => surface.OwnerPlayerId == 0));
            Vector2 flick = new Vector2(0f, 2.8f);
            Assert.That(LabThrowMath.IsThrowGesture(flick, Mathf.Max(.24f, config.ThrowSwipeMinDistance), config), Is.True);
            Vector3 anchor = LabThrowMath.SeatPosition(0, 2, Arena, config.ThrowOriginRadius, config.ThrowOriginHeightOffset, true);
            Vector3 launchVelocity = LabThrowMath.LaunchVelocity(flick, anchor, Arena, config);
            shot.Launch(orb.id, 0, launchVelocity, config, target, contact =>
            {
                received = true; result = contact;
                if (contact.Kind == LabProjectileHitKind.Monster)
                    approved = model.ConfirmHit(orb.id, snapshot.serverTime,
                        target.Hitbox.transform.InverseTransformPoint(contact.HitPoint).normalized, out _, out _, out _);
            });
            for (int i = 0; i < 60 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received, Is.True, "The saved-profile legal post-curtain shot must resolve; form=" + config.ThrowForm
                + " origin=" + shot.LaunchPosition + " velocity=" + launchVelocity + " gravity=" + config.EffectiveThrowGravity);
            Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Monster), "After every local point is out, no curtain may intercept this legal boss-bound flight.");
            Assert.That(approved, Is.True, "The actual Monster contact must pass the Host's final curtain gate after local extinction.");
            Assert.That(model.PaintSurface.CoveredCount, Is.GreaterThan(0));
            Install(43.5d);
            Assert.That(Surfaces()[curtainId], Is.SameAs(curtain));
            Assert.That(curtain.Hitbox, Is.SameAs(originalCollider));
            Assert.That(curtain.Active, Is.True, "A relit point restores the entire wall on the retained collider.");
            Assert.That(model.JigwiABBlocksBoss(0, 43.5d), Is.True);
        }

        [Test]
        public void CurtainCornersCoverTheRotatedBossAtEverySeatAndBothPhoneAndTabletAspects()
        {
            model = new LabModel();
            model.Begin("curtain-all-seats", new[] { 0ul, 20ul, 30ul, 40ul, 50ul }, 0d, config, LabStageYokai.Jigwi);
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            var cameraObject = new GameObject("Curtain envelope camera"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            target.Hitbox.transform.localRotation = Quaternion.Euler(32f, 143f, 17f);
            Vector3[] meshVertices = target.Hitbox.sharedMesh.vertices;
            foreach (LabPlayerState player in snapshot.players)
            {
                Assert.That(network.TryGetJigwiABCurtainFrame(active, player.id, snapshot.serverTime,
                    out Vector3 center, out Quaternion rotation, out Vector2 half), Is.True);
                camera.transform.position = LabThrowMath.SeatPosition(player.seat, snapshot.players.Length, Arena,
                    config.CameraRadius, config.CameraHeightOffset, true);
                camera.transform.LookAt(Arena + Vector3.up * config.CameraLookHeightOffset);
                foreach (float aspect in new[] { 390f / 844f, 3f / 4f })
                {
                    camera.aspect = aspect;
                    Vector3 lower = camera.WorldToViewportPoint(center - rotation * Vector3.right * half.x - rotation * Vector3.up * half.y);
                    Vector3 upper = camera.WorldToViewportPoint(center + rotation * Vector3.right * half.x + rotation * Vector3.up * half.y);
                    foreach (Vector3 vertex in meshVertices)
                    {
                        Vector3 projected = camera.WorldToViewportPoint(target.Hitbox.transform.TransformPoint(vertex));
                        Assert.That(projected.x, Is.InRange(lower.x - .001f, upper.x + .001f));
                        Assert.That(projected.y, Is.InRange(lower.y - .001f, upper.y + .001f));
                    }
                    Assert.That(network.TryGetJigwiABCurtainFrame(active, player.id, snapshot.serverTime,
                        out Vector3 again, out Quaternion rotationAgain, out Vector2 halfAgain), Is.True);
                    Assert.That(again, Is.EqualTo(center)); Assert.That(halfAgain, Is.EqualTo(half));
                    Assert.That(Quaternion.Angle(rotationAgain, rotation), Is.LessThan(.001f));
                }
                LabJigwiABPlayerFire owner = active.players.Single(value => value.playerId == player.id);
                Assert.That(network.TryGetJigwiABFireWorldPosition(active, owner.targets[0], player.id, 0,
                    snapshot.serverTime, out Vector3 ignition), Is.True);
                Assert.That(Vector3.Dot(center - ignition, rotation * Vector3.forward), Is.GreaterThan(0f),
                    "Ignition points must be nearer than their wall so a direct point hit is not swallowed by the curtain.");
            }
        }

        [Test]
        public void ExtremeRopeEnvelopeNeverSilentlyRemovesTheCurtainOrItsIgnitionPoints()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            var hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSwing(12f, 75f, 10f, 1.4f);
            hanging.ConfigureRopeMotion(1f, .5f, 2f, 100f, 20f, 3f);
            Assert.That(network.TryGetJigwiABCurtainFrame(active, 0, snapshot.serverTime,
                out Vector3 before, out Quaternion orientation, out Vector2 half), Is.True);
            Assert.That(float.IsNaN(half.x) || float.IsInfinity(half.x) || float.IsNaN(half.y) || float.IsInfinity(half.y), Is.False);
            Assert.That(half.x, Is.GreaterThan(0f)); Assert.That(half.y, Is.GreaterThan(0f));
            var owner = active.players.Single(player => player.playerId == 0);
            var pointPositions = new Vector3[owner.targets.Length];
            for (int i = 0; i < owner.targets.Length; i++)
                Assert.That(network.TryGetJigwiABFireWorldPosition(active, owner.targets[i], 0, i,
                    snapshot.serverTime, out pointPositions[i]), Is.True);
            hanging.ReconcileRope(.125f, 3f, 0f, true);
            Assert.That(network.TryGetJigwiABCurtainFrame(active, 0, snapshot.serverTime,
                out Vector3 after, out Quaternion afterOrientation, out Vector2 afterHalf), Is.True);
            Assert.That(Quaternion.Angle(afterOrientation, orientation), Is.LessThan(.001f));
            for (int i = 0; i < owner.targets.Length; i++)
            {
                Assert.That(network.TryGetJigwiABFireWorldPosition(active, owner.targets[i], 0, i,
                    snapshot.serverTime, out Vector3 actual), Is.True);
                Assert.That(Vector3.Distance(actual, pointPositions[i]), Is.LessThan(.001f),
                    "Ignition points stay fixed in front of the screen while the curtain resizes to cover the moving shell.");
            }
            AssertCurrentShellInsideCurtain(after, afterOrientation, afterHalf, 0, snapshot.players.Length);
        }

        [Test]
        public void UnlitTargetsKeepTheirExactColliderForRelightWithoutAnyWorldFlameVisual()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            LabJigwiABFireTarget fire = active.players[0].targets[0];
            LabJigwiFireSurface original = Surfaces()[fire.id];
            Collider collider = original.Hitbox;
            Assert.That(original.Active, Is.True);
            Assert.That(original.GetComponentsInChildren<MeshRenderer>(true)
                .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.False);
            fire.lit = false; fire.extinguishedAt = 31.1d; fire.relightAt = 43.1d;
            Sync();
            Assert.That(Surfaces()[fire.id], Is.SameAs(original));
            Assert.That(original.Hitbox, Is.SameAs(collider));
            Assert.That(original.Active, Is.False);
            fire.lit = true; snapshot.serverTime = 43.2d;
            Sync();
            Assert.That(Surfaces()[fire.id], Is.SameAs(original));
            Assert.That(original.Hitbox, Is.SameAs(collider));
            Assert.That(original.Active, Is.True);
            snapshot.jigwiAB.fever = true;
            active.resolution = LabJigwiABResolution.Cancelled; active.resolvedAt = 100d;
            snapshot.serverTime = 100d;
            Sync();
            Assert.That(Surfaces(), Is.Empty);
            Assert.That(original.Active, Is.False, "A queued Destroy cannot leave its old collider active after cutoff.");
        }

        [Test]
        public void PhoneAndTabletProjectionUseOneRotationIndependentWorldTargetAndRadius()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            LabJigwiABFireTarget fire = active.players[0].targets[1];
            ulong owner = active.players[0].playerId;
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 1, 31.1d, out Vector3 expected), Is.True);
            var cameraObject = new GameObject("AB phone and tablet projection"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            LabPlayerState player = snapshot.players.Single(item => item.id == owner);
            camera.transform.position = LabThrowMath.SeatPosition(player.seat, snapshot.players.Length, Arena,
                config.CameraRadius, config.CameraHeightOffset, true);
            camera.transform.LookAt(Arena + Vector3.up * config.CameraLookHeightOffset);
            Vector3 launch = LabThrowMath.LaunchPosition(player.seat, snapshot.players.Length, Arena, .5f, config);
            Assert.That(Vector3.Dot(expected - launch, camera.transform.forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(Arena - expected, camera.transform.forward), Is.GreaterThan(0f));
            foreach (float aspect in new[] { 390f / 844f, 3f / 4f })
            {
                camera.aspect = aspect;
                Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 1, 31.1d, out Vector3 actual), Is.True);
                Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.00001f));
                Vector3 projected = camera.WorldToViewportPoint(actual);
                Assert.That(projected.z, Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(camera.ViewportToWorldPoint(projected), actual), Is.LessThan(.001f));
                Vector3 edge = camera.WorldToViewportPoint(actual + camera.transform.right * active.targetRadius);
                Assert.That(edge.x - projected.x, Is.GreaterThan(0f), "The Canvas can size the flame from the very same world radius as its collider.");
            }
            target.Hitbox.transform.localRotation = Quaternion.Euler(35f, 127f, 0f);
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 1, 31.1d, out Vector3 rotated), Is.True);
            Assert.That(Vector3.Distance(rotated, expected), Is.LessThan(.0001f));
        }

        [Test]
        public void BoardAndLaunchUseTheSmallerIndependentRadiusWhileKeepingGrabReachAndPaintBudget()
        {
            var cameraRoot = new GameObject("Board camera"); cameraRoot.transform.SetParent(root.transform, false);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.transform.position = Vector3.back * 10f;
            var boardRoot = new GameObject("B size board"); boardRoot.transform.SetParent(root.transform, false);
            var board = boardRoot.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            LabOrbView view = board.UpsertOrb("B-raw", LabOrbKind.Yin, Vector2.one * .5f, jigwiABScale: .65f);
            Assert.That(view.HitSphere.radius, Is.EqualTo(config.OrbRadius * .65f).Within(.00001f));
            Assert.That(board.TryBeginDragAtWorld(new Vector2(view.Body.position.x + config.OrbRadius * 1.05f,
                view.Body.position.y), 1d), Is.True, "B shrink must preserve the pre-shrink touch reach.");
            board.UpsertOrb("B-raw", LabOrbKind.Yin, Vector2.one * .5f, small: true, jigwiABScale: .65f);
            Assert.That(view.HitSphere.radius, Is.EqualTo(config.OrbRadius * Mathf.Min(config.BlastShrinkScale, .65f)).Within(.00001f));
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            Assert.That(view.HitSphere.radius, Is.EqualTo(config.OrbRadius * Mathf.Min(config.BlastShrinkScale, .65f)).Within(.00001f));

            foreach (float combo in new[] { .3f, 1.5f })
            {
                LabProjectile shot = LabProjectile.Create(Arena + Vector3.forward * 4f, config, root.transform,
                    LabOrbElement.Water, config.BlastShrinkScale);
                shot.SetRadiusScale(combo); shot.SetJigwiABRadiusScale(.65f);
                Assert.That(shot.Launch("B-sized-" + combo, 0, Vector3.zero, config, target, _ => { }), Is.True);
                float radius = config.ThrowRadius * Mathf.Min(config.BlastShrinkScale * combo, .65f);
                Assert.That(shot.GetComponent<SphereCollider>().radius, Is.EqualTo(radius).Within(.00001f));
                Assert.That(shot.RadiusScale, Is.EqualTo(combo), "B may not overwrite the combo/paint scale.");
                Assert.That(shot.JigwiABRadiusScale, Is.EqualTo(.65f));
            }
            LabModel control = Room(LabStageYokai.None);
            model.Tick(43d);
            // One player's extinguished curtain allows boss attacks while the other player's
            // live points keep B and its already-applied radius reduction in force.
            ExtinguishOwnPoints(0, 43d);
            Assert.That(model.JigwiABBlocksBoss(0, 43d), Is.False);
            Assert.That(model.JigwiABBlocksBoss(20, 43d), Is.True);
            LabOrbState reduced = Add(model, 0, 43d, LabOrbElement.Water);
            LabOrbState normal = Add(control, 0, 43d, LabOrbElement.Water);
            foreach (var pair in new[] { (model, reduced), (control, normal) })
            {
                Assert.That(pair.Item1.TryLaunch(0, pair.Item2.id, "same-paint-launch", 43d, out _, out string error), Is.True, error);
                Assert.That(pair.Item1.LaunchScaleOf(pair.Item2.id), Is.EqualTo(1f));
                Assert.That(pair.Item1.ConfirmHit(pair.Item2.id, 43d, Vector3.forward, out _, out _, out _), Is.True);
            }
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(control.PaintSurface.CoveredCount),
                "One B-shrunk hit and one normal hit must fund the same newly painted area.");
        }

        [Test]
        public void BurnDeadlinesAndABHeadersDeepCopyValidateAndClearThroughOwnerDeltas()
        {
            float[] values = config.CaptureDeveloperValues(); values[243] = 30f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            model = Room(LabStageYokai.Jigwi);
            ulong owner = model.Snapshot(10d).jigwiAB.Active(10d).targetPlayer;
            LabOrbState orb = Add(model, owner, 10d, LabOrbElement.Fire);
            LabSnapshot snapshot = model.Snapshot(11d);
            var publisher = new LabBoardPublisher();
            var replica = new LabBoardReplica(); replica.SetActive(snapshot.sessionId, snapshot.round, owner);
            LabBoardFrame frame = publisher.Build(snapshot, owner, 1, 1);
            Assert.That(frame.states.Single(item => item.id == orb.id).jigwiABBurnAt, Is.EqualTo(41d));
            snapshot.jigwiAB.rounds[0].hitCount = 2;
            snapshot.orbs.Single(item => item.id == orb.id).jigwiABBurnAt = 99d;
            Assert.That(frame.jigwiAB.rounds[0].hitCount, Is.Zero);
            Assert.That(replica.TryApply(frame, out error), Is.True, error);
            frame.jigwiAB.rounds[0].targetPlayer = 999;
            frame.states.Single(item => item.id == orb.id).jigwiABBurnAt = 999;
            Assert.That(replica.TryGetState(orb.id, out LabOrbState copied), Is.True);
            Assert.That(copied.jigwiABBurnAt, Is.EqualTo(41d));
            LabSnapshot oldPaint = new LabSnapshot { sessionId = snapshot.sessionId, round = snapshot.round,
                phase = LabPhase.Playing, serverTime = 0d };
            Assert.That(replica.Overlay(oldPaint).jigwiAB.rounds[0].targetPlayer, Is.EqualTo(owner));

            LabBoardFrame malformed = publisher.Build(model.Snapshot(11.1d), owner, 2, 2, true);
            malformed.states.Single(item => item.id == orb.id).jigwiABBurnAt = double.NaN;
            Assert.That(replica.TryApply(malformed, out error), Is.False);
            Assert.That(error, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            malformed = publisher.Build(model.Snapshot(11.1d), owner, 2, 2, true);
            malformed.jigwiAB.rounds[0].targetPlayer = 999;
            Assert.That(replica.TryApply(malformed, out error), Is.False);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));

            LabBoardFrame cleared = publisher.Build(model.Snapshot(21d), owner, 2, 3);
            Assert.That(cleared.states.Single(item => item.id == orb.id).jigwiABBurnAt, Is.EqualTo(-1d));
            Assert.That(replica.TryApply(cleared, out error), Is.True, error);
            Assert.That(replica.TryGetState(orb.id, out copied), Is.True);
            Assert.That(copied.jigwiABBurnEventId, Is.Null.Or.Empty);
            Assert.That(copied.jigwiABBurnAt, Is.EqualTo(-1d));
        }

        [Test]
        public void AnApprovedBShrinkReconcilesOnlyTheCosmeticColliderWithoutRewindingEitherFlight()
        {
            Vector3 origin = Arena + Vector3.forward * 4f;
            Vector3 velocity = new Vector3(2f, 3f, -4f);
            LabProjectile visual = LabProjectile.Create(origin, config, root.transform, LabOrbElement.Water);
            LabProjectile host = LabProjectile.Create(origin + Vector3.right * 3f, config, root.transform, LabOrbElement.Water);
            Assert.That(visual.LaunchVisual(velocity, config, target), Is.True);
            Assert.That(host.Launch("frozen-host-radius", 0, velocity, config, target, _ => { }), Is.True);
            Rigidbody visualBody = visual.GetComponent<Rigidbody>(), hostBody = host.GetComponent<Rigidbody>();
            Vector3 visualPosition = visualBody.position, visualVelocity = visualBody.linearVelocity;
            Vector3 hostPosition = hostBody.position, hostVelocity = hostBody.linearVelocity;
            float hostRadius = host.GetComponent<SphereCollider>().radius;
            Assert.That(visual.GetComponent<SphereCollider>().radius, Is.EqualTo(config.ThrowRadius).Within(.00001f));
            visual.ReconcileVisual(LabOrbElement.Water, 1f, 1f, .65f);
            Assert.That(visual.GetComponent<SphereCollider>().radius, Is.EqualTo(config.ThrowRadius * .65f).Within(.00001f));
            Assert.That(visual.GetComponent<SphereCollider>().isTrigger, Is.True);
            Assert.That(visualBody.position, Is.EqualTo(visualPosition));
            Assert.That(visualBody.linearVelocity, Is.EqualTo(visualVelocity));
            Assert.That(visual.InitialVelocity, Is.EqualTo(visualVelocity));
            Assert.That(visual.LaunchPosition, Is.EqualTo(visualPosition));

            host.ReconcileVisual(LabOrbElement.Fire, 1f, 1f, .2f);
            Assert.That(host.GetComponent<SphereCollider>().radius, Is.EqualTo(hostRadius));
            Assert.That(host.GetComponent<SphereCollider>().isTrigger, Is.False);
            Assert.That(host.JigwiABRadiusScale, Is.EqualTo(1f));
            Assert.That(hostBody.position, Is.EqualTo(hostPosition));
            Assert.That(hostBody.linearVelocity, Is.EqualTo(hostVelocity));
        }

        [Test]
        public void ADelayedOlderBoardPauseCannotOverrideNewerBulkPauseOrFreezeItsInput()
        {
            var cameraRoot = new GameObject("Pause consistency camera"); cameraRoot.transform.SetParent(root.transform, false);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.transform.position = Vector3.back * 10f;
            var boardRoot = new GameObject("Pause consistency board"); boardRoot.transform.SetParent(root.transform, false);
            var board = boardRoot.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            network.board = board;
            var publisher = new LabBoardPublisher();
            MethodInfo receive = typeof(LabNetwork).GetMethod("ReceiveBoardFrame", Private);
            MethodInfo pause = typeof(LabNetwork).GetMethod("ApplyLocalPause", Private);
            var replica = (LabBoardReplica)Field("boardReplica").GetValue(network);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    bool newerPaused = i == 1;
                    double oldTime = 31.1d + i, newTime = 31.2d + i;
                    LabSnapshot oldHeader = model.Snapshot(oldTime);
                    oldHeader.paused = !newerPaused;
                    LabSnapshot newer = model.Snapshot(newTime);
                    newer.paused = newerPaused;
                    Field("latest").SetValue(network, newer);
                    pause.Invoke(network, new object[] { newerPaused });
                    LabBoardFrame frame = publisher.Build(oldHeader, network.LocalPlayerId, i + 1, (ulong)i + 1, true);
                    receive.Invoke(network, new object[] { frame });
                    Assert.That(network.ReceivedBoardFrames, Is.EqualTo(i + 1), "The older header is valid; it must be applied to the owner ledger rather than dropped wholesale.");
                    Assert.That(network.Snapshot.serverTime, Is.EqualTo(newTime));
                    Assert.That(network.Snapshot.paused, Is.EqualTo(newerPaused));
                    Assert.That(Time.timeScale, Is.EqualTo(newerPaused ? 0f : 1f));
                    Assert.That(board.InteractionEnabled, Is.EqualTo(!newerPaused));
                    Assert.That(board.SimulationHeld, Is.EqualTo(network.GameStartHeld),
                        "The initial-loading physics hold stays separate from normal global pause.");
                    Assert.That(replica.NeedsCheckpoint, Is.False);
                    Assert.That(replica.InteractionAllowedFor(network.Snapshot), Is.EqualTo(!newerPaused));
                }
                LabSnapshot visible = network.Snapshot;
                visible.paused = false;
                LabBoardFrame gap = publisher.Build(model.Snapshot(33.1d), network.LocalPlayerId, 4, 4);
                Assert.That(replica.TryApply(gap, out string error), Is.False);
                Assert.That(error, Is.EqualTo("BOARD_CHECKPOINT_REQUIRED"));
                Assert.That(replica.InteractionAllowedFor(visible), Is.False,
                    "A newer bulk clock must never bypass missing owner-board transactions.");
            }
            finally
            {
                pause.Invoke(network, new object[] { false });
                Time.timeScale = 1f;
            }
        }

        private LabOrbThrowGesture CaptureRealBoardGesture(LabOrbBoard board, float releaseX, float speed, double startAt)
        {
            board.Clear(); board.SetInteractionEnabled(true);
            string id = "legal-gesture-" + ++serial;
            LabOrbView orb = board.UpsertOrb(id, LabOrbKind.Combined, new Vector2(releaseX, .2f), element: LabOrbElement.Water);
            Vector2 start = new Vector2(orb.Body.position.x, orb.Body.position.y);
            float travelPerSecond = speed * board.CenterBounds.width;
            bool observed = false; LabOrbThrowGesture gesture = default;
            Action<LabOrbThrowGesture> collect = value => { observed = true; gesture = value; };
            board.ThrowRequested += collect;
            try
            {
                Assert.That(board.TryBeginDragAtWorld(start, startAt), Is.True);
                Assert.That(board.DragToWorld(start + Vector2.up * (travelPerSecond * .04f), startAt + .04d), Is.True);
                Assert.That(board.DragToWorld(start + Vector2.up * (travelPerSecond * .08f), startAt + .08d), Is.True);
                Assert.That(board.EndDragAtWorld(start + Vector2.up * (travelPerSecond * .12f), startAt + .12d), Is.True);
                Assert.That(observed, Is.True, "An actual Board gesture must emit ThrowRequested; no injected distance or velocity.");
                Assert.That(gesture.ReleaseX01, Is.EqualTo(releaseX).Within(.001f));
                Assert.That(gesture.Velocity.y, Is.EqualTo(speed).Within(.01f));
                return gesture;
            }
            finally { board.ThrowRequested -= collect; }
        }

        private void AssertCurrentShellInsideCurtain(Vector3 center, Quaternion rotation, Vector2 half,
            int seat, int count)
        {
            Vector3 eye = LabThrowMath.SeatPosition(seat, count, Arena, config.CameraRadius, config.CameraHeightOffset, true);
            Vector3 forward = rotation * Vector3.forward;
            float depth = Vector3.Dot(center - eye, forward);
            Bounds bounds = target.Hitbox.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 relative = target.Hitbox.transform.TransformPoint(local) - eye;
                Vector3 projected = eye + relative * (depth / Mathf.Max(.1f, Vector3.Dot(relative, forward)));
                Vector3 onFrame = Quaternion.Inverse(rotation) * (projected - center);
                Assert.That(onFrame.x, Is.InRange(-half.x - .001f, half.x + .001f));
                Assert.That(onFrame.y, Is.InRange(-half.y - .001f, half.y + .001f));
            }
        }

        private void ExtinguishOwnPoints(ulong owner, double at)
        {
            LabJigwiABRound round = model.Snapshot(at).jigwiAB.Active(at);
            foreach (LabJigwiABFireTarget fire in round.players.Single(player => player.playerId == owner).targets)
                if (fire.lit) ExtinguishPoint(owner, round.id, fire.id, at);
        }
        private void ExtinguishPoint(ulong owner, string roundId, string targetId, double at)
        {
            LabOrbState water = Add(model, owner, at, LabOrbElement.Water);
            Assert.That(model.TryLaunch(owner, water.id, "clear-point-" + water.id, at, out _, out string error), Is.True, error);
            Assert.That(model.ConfirmJigwiABFireHit(water.id, roundId, targetId, at, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
        }

        private LabModel Room(LabStageYokai stage)
        {
            var value = new LabModel(); value.Begin("jigwi-ab-network", new[] { 0ul, 20ul }, 0d, config, stage);
            return value;
        }
        private LabSnapshot Install(double at)
        {
            LabSnapshot value = model.Snapshot(at);
            Field("latest").SetValue(network, value);
            Sync(); return value;
        }
        private void Sync() => typeof(LabNetwork).GetMethod("SyncJigwiFireSurfaces", Private).Invoke(network, null);
        private Dictionary<string, LabJigwiFireSurface> Surfaces() =>
            (Dictionary<string, LabJigwiFireSurface>)Field("jigwiFireSurfaces").GetValue(network);
        private LabOrbState Add(LabModel value, ulong owner, double at, LabOrbElement element)
        {
            var orb = new LabOrbState { id = "net-fixture-" + ++serial, owner = owner, kind = LabOrbKind.Combined,
                element = element, x = .5f, y = .5f, createdAt = at };
            ((Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(value)).Add(orb.id, orb);
            return orb;
        }
        private LabProjectile Shot()
        {
            Vector3 origin = LabThrowMath.LaunchPosition(0, 2, Arena, .5f, config);
            return LabProjectile.Create(origin, config, root.transform, LabOrbElement.Water);
        }
        private Vector3 VelocityTo(Vector3 point, float seconds)
        {
            Vector3 origin = LabThrowMath.LaunchPosition(0, 2, Arena, .5f, config);
            return (point - origin) / seconds + Vector3.up * (.5f * config.EffectiveThrowGravity * seconds);
        }
        private static FieldInfo Field(string name) => typeof(LabNetwork).GetField(name, Private);
    }
}
