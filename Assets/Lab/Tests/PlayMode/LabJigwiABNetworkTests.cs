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

        [Test]
        public void BCreatesNoFireOrCurtainCollidersInFrontOfTheBoss()
        {
            // #509: B is judged on the boss itself.
            LabSnapshot snapshot = Install(31.1d);
            Assert.That(snapshot.jigwiAB.Active(snapshot.serverTime).kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(Surfaces(), Is.Empty);
            Assert.That(network.GetComponentsInChildren<LabJigwiFireSurface>(true), Is.Empty);
            Assert.That(model.JigwiABBlocksBoss(0, 31.1d), Is.False);
        }

        [UnityTest]
        public IEnumerator APickedSeatsRealWaterShotOnTheBossCountsAsItsSyncHitAndPaints()
        {
            LabSnapshot snapshot = Install(31.1d);
            Assert.That(snapshot.jigwiAB.Active(snapshot.serverTime).syncCount, Is.Zero);
            LabOrbState orb = Add(model, 0, snapshot.serverTime, LabOrbElement.Water);
            Assert.That(model.TryLaunch(0, orb.id, "sync-boss-flight", snapshot.serverTime, out _, out string error), Is.True, error);
            yield return null;
            bool received = false, approved = false; LabProjectileResult result = default;
            LabProjectile shot = Shot();
            Vector2 flick = new Vector2(0f, 2.8f);
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
            Assert.That(received, Is.True, "The saved-profile legal shot must resolve; form=" + config.ThrowForm
                + " origin=" + shot.LaunchPosition + " velocity=" + launchVelocity + " gravity=" + config.EffectiveThrowGravity);
            Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Monster), "Nothing stands in front of the boss during B.");
            Assert.That(approved, Is.True);
            Assert.That(model.PaintSurface.CoveredCount, Is.GreaterThan(0));
            Assert.That(model.Snapshot(snapshot.serverTime).jigwiAB.Active(snapshot.serverTime).syncCount, Is.EqualTo(1),
                "Seat 0 is the only picked seat, so its Water on the boss is one synchronized clear.");
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
            // #509: only the picked seats (3 of 5) have a curtain.
            Assert.That(active.players, Has.Length.EqualTo(3));
            foreach (LabPlayerState player in snapshot.players.Where(p => active.players.Any(owner => owner.playerId == p.id)))
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
        public void PhoneAndTabletProjectionUseOneRotationIndependentWorldTargetAndRadius()
        {
            LabSnapshot snapshot = Install(31.1d);
            LabJigwiABRound active = snapshot.jigwiAB.Active(snapshot.serverTime);
            LabJigwiABFireTarget fire = active.players[0].targets[0];
            ulong owner = active.players[0].playerId;
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 0, 31.1d, out Vector3 expected), Is.True);
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
                Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 0, 31.1d, out Vector3 actual), Is.True);
                Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.00001f));
                Vector3 projected = camera.WorldToViewportPoint(actual);
                Assert.That(projected.z, Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(camera.ViewportToWorldPoint(projected), actual), Is.LessThan(.001f));
                Vector3 edge = camera.WorldToViewportPoint(actual + camera.transform.right * active.targetRadius);
                Assert.That(edge.x - projected.x, Is.GreaterThan(0f), "The Canvas can size the flame from the very same world radius as its collider.");
            }
            target.Hitbox.transform.localRotation = Quaternion.Euler(35f, 127f, 0f);
            Assert.That(network.TryGetJigwiABFireWorldPosition(active, fire, owner, 0, 31.1d, out Vector3 rotated), Is.True);
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
                view.Body.position.y), 1d), Is.True, "The legacy independent shrink API must preserve the pre-shrink touch reach.");
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
            model.Tick(31d);
            // Retain the generic independent-radius compatibility check with an explicit fixture,
            // while the actual timed B curtain stays active only for the other player.
            ExtinguishOwnPoints(0, 31d);
            Assert.That(model.JigwiABBlocksBoss(0, 31d), Is.False);
            Assert.That(model.JigwiABBlocksBoss(20, 31d), Is.False, "#509: the helper is never blocked.");
            int modelBefore = model.PaintSurface.CoveredCount; // the three sync hits already painted the back
            // Hit after B's 5-second success reward [31, 36) so both rooms use the ordinary paint budget.
            LabOrbState reduced = Add(model, 0, 37d, LabOrbElement.Water);
            reduced.jigwiABRadiusScale = .65f; // Explicit old-field fixture, not a timed-mission penalty.
            LabOrbState normal = Add(control, 0, 37d, LabOrbElement.Water);
            foreach (var pair in new[] { (model, reduced), (control, normal) })
            {
                Assert.That(pair.Item1.TryLaunch(0, pair.Item2.id, "same-paint-launch", 37d, out _, out string error), Is.True, error);
                Assert.That(pair.Item1.LaunchScaleOf(pair.Item2.id), Is.EqualTo(1f));
                Assert.That(pair.Item1.ConfirmHit(pair.Item2.id, 37d, Vector3.forward, out _, out _, out _), Is.True);
            }
            Assert.That(model.PaintSurface.CoveredCount - modelBefore, Is.EqualTo(control.PaintSurface.CoveredCount),
                "An explicitly radius-reduced compatibility hit and one normal hit must fund the same newly painted area.");
        }

        [Test]
        public void BurnDeadlinesAndABHeadersDeepCopyValidateAndClearThroughOwnerDeltas()
        {
            float[] values = config.CaptureDeveloperValues(); values[243] = 30f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            model = Room(LabStageYokai.Jigwi);
            ulong owner = 20ul; // #479: A ignites every seat, so any player owns burning orbs.
            LabOrbState orb = Add(model, owner, 4.2d, LabOrbElement.Fire);
            double expectedBurnAt = 5d + config.JigwiABPersonalBurnSeconds; // Activation applies fire to this pre-existing fixture orb.
            LabSnapshot snapshot = model.Snapshot(5.3d);
            double aEndsAt = snapshot.jigwiAB.rounds[0].endsAt;
            var publisher = new LabBoardPublisher();
            var replica = new LabBoardReplica(); replica.SetActive(snapshot.sessionId, snapshot.round, owner);
            LabBoardFrame frame = publisher.Build(snapshot, owner, 1, 1);
            Assert.That(frame.states.Single(item => item.id == orb.id).jigwiABBurnAt, Is.EqualTo(expectedBurnAt));
            snapshot.jigwiAB.rounds[0].endsAt = 99d;
            snapshot.orbs.Single(item => item.id == orb.id).jigwiABBurnAt = 99d;
            Assert.That(frame.jigwiAB.rounds[0].endsAt, Is.EqualTo(aEndsAt));
            Assert.That(replica.TryApply(frame, out error), Is.True, error);
            frame.jigwiAB.rounds[0].endsAt = 999d;
            frame.states.Single(item => item.id == orb.id).jigwiABBurnAt = 999;
            Assert.That(replica.TryGetState(orb.id, out LabOrbState copied), Is.True);
            Assert.That(copied.jigwiABBurnAt, Is.EqualTo(expectedBurnAt));
            LabSnapshot oldPaint = new LabSnapshot { sessionId = snapshot.sessionId, round = snapshot.round,
                phase = LabPhase.Playing, serverTime = 0d };
            Assert.That(replica.Overlay(oldPaint).jigwiAB.rounds[0].endsAt, Is.EqualTo(aEndsAt));

            LabBoardFrame malformed = publisher.Build(model.Snapshot(5.4d), owner, 2, 2, true);
            malformed.states.Single(item => item.id == orb.id).jigwiABBurnAt = double.NaN;
            Assert.That(replica.TryApply(malformed, out error), Is.False);
            Assert.That(error, Is.EqualTo("INVALID_BOARD_FRAME"));
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            malformed = publisher.Build(model.Snapshot(5.4d), owner, 2, 2, true);
            malformed.jigwiAB.rounds[0].rewardSizeScale = 2f; // A never carries a reward scale.
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

        /// <summary>#509: the single picked seat's three hits are three synchronized clears (B succeeds).</summary>
        private void ExtinguishOwnPoints(ulong owner, double at)
        {
            LabJigwiABRound round = model.Snapshot(at).jigwiAB.Active(at);
            string fire = round.players.Single(player => player.playerId == owner).targets[0].id;
            for (int i = 0; i < LabJigwiABTimeline.GroupSyncRequired; i++) ExtinguishPoint(owner, round.id, fire, at);
        }
        private void ExtinguishPoint(ulong owner, string roundId, string targetId, double at)
        {
            // #509: the picked seat's Water on the boss (aimed at the back, away from the paint checks).
            LabOrbState water = Add(model, owner, at, LabOrbElement.Water);
            Assert.That(model.TryLaunch(owner, water.id, "clear-point-" + water.id, at, out _, out string error), Is.True, error);
            Assert.That(model.ConfirmHit(water.id, at, Vector3.back, out _, out _, out _), Is.True);
        }

        private LabModel Room(LabStageYokai stage)
        {
            // #509: B picks one of two seats from the session seed; this id picks seat 0 for B1 and B2
            // (seat 20 is the helper), so the fixtures below can keep aiming from seat 0.
            // (Checked inside the tests that read B; asking here would advance the model clock.)
            var value = new LabModel(); value.Begin("jigwi-ab-network2", new[] { 0ul, 20ul }, 0d, config, stage);
            return value;
        }
        private LabSnapshot Install(double at)
        {
            LabSnapshot value = model.Snapshot(at);
            LabJigwiABRound b = value.jigwiAB?.Active(at);
            if (b != null && b.kind == LabJigwiABKind.GroupFire && b.slot <= 3 && value.players.Length == 2)
                Assert.That(b.players.Single().playerId, Is.EqualTo(0ul), "Fixture assumption: seat 0 is the picked seat.");
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
