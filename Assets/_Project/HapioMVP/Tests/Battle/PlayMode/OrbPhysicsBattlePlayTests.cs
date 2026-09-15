using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>
    /// Saved battle wiring with explicit P1 opt-in, real NGO and local 2D simulation.
    /// Geometry/release samples are fixtures; these tests do not claim physical Touch or P2 throwing.
    /// </summary>
    public sealed class OrbPhysicsBattlePlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private T09BattleController controller;
        private bool previousRunInBackground;
        private int pointer;

        [UnitySetUp]
        public IEnumerator OpenSavedSceneAndOptIntoLocalPhysics()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null; yield return null;
            controller = Object.FindAnyObjectByType<T09BattleController>(); pointer = 19500;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.OrbPhysicsEnabled, Is.False, "The historical scene must retain its existing input model.");
            controller.ConfigureOrbPhysics(true);
            Assert.That(controller.OrbPhysicsEnabled, Is.True);
            Assert.That(controller.OrbPhysics, Is.Not.Null);
            Assert.That(controller.Views, Is.Empty);
        }

        [UnityTearDown]
        public IEnumerator CloseOnlyTheTestConnectionAndScene()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                    "The P1 test connection did not stop.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("P1 Orb Physics Cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<T09BattleController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator ReleasedCollisionAndWallContactCannotCreateGameplayRequests()
        {
            controller.ConfigureTransfers(true);
            yield return StartSolo();
            var source = Supply(OrbKind.Raw, OrbPolarity.Yin, new Vector2(.3f, .55f));
            var target = Supply(OrbKind.Raw, OrbPolarity.Yang, new Vector2(.55f, .55f));
            var combined = Supply(OrbKind.Combined, OrbPolarity.None, new Vector2(.75f, .7f));
            yield return PublishFixture();
            var replies = new List<AttackRequestReply>();
            controller.Attack.RequestResolved += replies.Add;
            try
            {
                float radius = WorldRadius(source.OrbId);
                Vector2 targetBefore = controller.Views[target.OrbId].transform.position;
                // Position the Raw just outside contact, then release a sampled motion into its opposite polarity.
                controller.OrbPhysics.SetPosition(source.OrbId, targetBefore - Vector2.right * radius * 3.2f, false, Now);
                ReleaseSample(source.OrbId, Vector2.right * radius * .8f);
                // A freely moving Combined reaches both physical bounds without pointer input.
                controller.OrbPhysics.SetPosition(combined.OrbId, controller.OrbPhysics.CenterBounds.max - Vector2.one * radius * 2f, false, Now);
                ReleaseSample(combined.OrbId, Vector2.one * radius * .8f);
                var combinedBody = controller.Views[combined.OrbId].GetComponent<Rigidbody2D>();
                bool upperBounce = false, sideBounce = false;
                double collisionDeadline = Now + 1d;
                while (Now < collisionDeadline && !(upperBounce && sideBounce))
                {
                    upperBounce |= combinedBody.linearVelocity.y < -.001f;
                    sideBounce |= combinedBody.linearVelocity.x < -.001f;
                    yield return null;
                }
                Assert.That(upperBounce && sideBounce, Is.True, "The passive Combined fixture must actually reach and rebound from both boundaries.");
                yield return WaitFor(() => Vector2.Distance(targetBefore, controller.Views[target.OrbId].transform.position) > radius * .05f,
                    "The released Raw did not actually collide with and move the other Raw.");
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(controller.Views.Count, Is.EqualTo(3));
                Assert.That(controller.Combination.LastResult, Is.Null, "Passive Yin/Yang contact must not combine.");
                Assert.That(controller.Attack.Registry.Snapshot().All(o => o.AuthorityState == OrbAuthorityState.Idle), Is.True);
                Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
                Assert.That(controller.Attack.Snapshot.totalHits, Is.Zero);
                Assert.That(controller.SentTransfers, Is.Zero);
                Assert.That(replies, Is.Empty, "Physics movement must not submit launch or transfer requests.");
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
                Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
            }
            finally { controller.Attack.RequestResolved -= replies.Add; }
        }

        [UnityTest]
        public IEnumerator HeldOverlapWaitsForNormalReleaseThenConsumesOnlyTheTwoMaterials()
        {
            yield return StartSolo();
            var pair = SupplyPair(false);
            yield return PublishFixture();
            Vector2 targetBefore = controller.Views[pair[1].OrbId].transform.position;
            int id = BeginDrop(pair[0].OrbId, pair[1].OrbId, out var end);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(controller.Combination.LastResult, Is.Null, "Holding an overlap cannot invoke combination.");
            Assert.That(controller.Views.Count, Is.EqualTo(2));
            Assert.That(Vector2.Distance(targetBefore, controller.Views[pair[1].OrbId].transform.position), Is.LessThan(.01f),
                "A held orb must not push its intended drop target out from under the finger.");
            Assert.That(controller.Views[pair[0].OrbId].HeldFeedbackActive, Is.True);
            controller.EndPointer(id, end);
            yield return WaitFor(() => controller.Combination.LastResult != null && !controller.HasCombinationPending,
                "The Host did not resolve the normal release.");
            var result = controller.Combination.LastResult;
            Assert.That(result.known && result.accepted, Is.True, result.reason);
            Assert.That(result.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(result.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(result.originalCombined.id, Is.Not.EqualTo(pair[0].OrbId).And.Not.EqualTo(pair[1].OrbId));
            Assert.That(controller.Views.Keys.Single(), Is.EqualTo(result.originalCombined.id));
            Assert.That(controller.OrbPhysics.TryGetVelocity(pair[0].OrbId, out _), Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(pair[1].OrbId, out _), Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(result.originalCombined.id, out var velocity), Is.True);
            Assert.That(velocity.sqrMagnitude, Is.LessThan(.0001f), "The new Combined cannot inherit a consumed material's fling.");
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            // P1 preserves the existing upper-boundary Move launch. Release-based 3D throwing belongs to P2.
            Physics2D.SyncTransforms();
            Vector2 start = controller.GetViewScreenPosition(result.originalCombined.id);
            Vector2 upper = new Vector2(start.x, controller.Layout.BottomPixelRect.yMax + 2f);
            id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            controller.MovePointer(id, upper);
            Assert.That(controller.Views.ContainsKey(result.originalCombined.id), Is.False,
                "The opt-in 2D board must preserve P1's existing upper-boundary launch timing.");
            Assert.That(controller.OrbPhysics.TryGetVelocity(result.originalCombined.id, out _), Is.False);
            var projectile = Object.FindObjectsByType<HostProjectile3D>().Single(p => p.OrbId == result.originalCombined.id);
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.linearVelocity.magnitude, Is.GreaterThan(0));
            controller.EndPointer(id, upper);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, "The preserved Host 3D projectile did not actually hit.");
        }

        [UnityTest]
        public IEnumerator SamePolarityReleaseIsRejectedAndLeavesTwoSeparatedDynamicBodies()
        {
            yield return StartSolo();
            var pair = SupplyPair(true);
            yield return PublishFixture();
            int id = BeginDrop(pair[0].OrbId, pair[1].OrbId, out var end);
            controller.EndPointer(id, end);
            yield return WaitFor(() => controller.Combination.LastResult != null && !controller.HasCombinationPending,
                "The Host did not resolve the same-polarity drop.");
            Assert.That(controller.Combination.LastResult.accepted, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.Views.Count, Is.EqualTo(2));
            foreach (var orb in pair)
            {
                var body = controller.Views[orb.OrbId].GetComponent<Rigidbody2D>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(body.simulated, Is.True);
                Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
                Assert.That(controller.Attack.Registry.TryGet(orb.OrbId, out var kept), Is.True);
                Assert.That(kept.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            }
            float separation = Vector2.Distance(controller.Views[pair[0].OrbId].transform.position,
                controller.Views[pair[1].OrbId].transform.position);
            Assert.That(separation, Is.GreaterThanOrEqualTo(WorldRadius(pair[0].OrbId) * 1.9f),
                "Rejected overlap must not leave two dynamic bodies permanently interpenetrating.");
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator NormalControllerReleaseCarriesRecentDragMotionAndASecondGrabCancelsIt()
        {
            yield return StartSolo();
            var orb = Supply(OrbKind.Raw, OrbPolarity.Yin, new Vector2(.35f, .55f));
            yield return PublishFixture();
            Vector2 start = controller.GetViewScreenPosition(orb.OrbId);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            float travel = Mathf.Min(controller.OrbGridScreenRect.width * .12f, 60f);
            for (int step = 1; step <= 4; step++)
            {
                yield return new WaitForSecondsRealtime(.02f);
                controller.MovePointer(id, start + Vector2.right * (travel * step / 4f));
                Assert.That(controller.Views[orb.OrbId].HeldFeedbackActive, Is.True);
                Assert.That(controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
            }
            Vector2 end = start + Vector2.right * travel;
            controller.EndPointer(id, end);
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].HeldFeedbackActive, Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var released), Is.True);
            Assert.That(released.x, Is.GreaterThan(.01f), "The controller's ordinary Up must hand recent movement to the local body.");
            Assert.That(Mathf.Abs(released.y), Is.LessThan(.01f));
            Vector2 releasedAt = controller.Views[orb.OrbId].transform.position;
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(controller.Views[orb.OrbId].transform.position.x, Is.GreaterThan(releasedAt.x + .001f));
            Physics2D.SyncTransforms();
            id = ++pointer;
            Vector2 regrab = controller.GetViewScreenPosition(orb.OrbId);
            Assert.That(controller.BeginPointer(id, regrab, false), Is.True);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var grabbed), Is.True);
            Assert.That(grabbed.sqrMagnitude, Is.LessThan(.0001f));
            controller.CancelPointer(id);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var cancelled), Is.True);
            Assert.That(cancelled.sqrMagnitude, Is.LessThan(.0001f));
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator OrdinaryInventorySnapshotsPreserveCoastingAndPointerCancelStopsIt()
        {
            yield return StartSolo();
            var orb = Supply(OrbKind.Raw, OrbPolarity.Yin, new Vector2(.35f, .55f));
            yield return PublishFixture();
            ReleaseSample(orb.OrbId, Vector2.right * WorldRadius(orb.OrbId) * .8f);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var before), Is.True);
            Assert.That(before.magnitude, Is.GreaterThan(.01f));
            Vector2 positionBefore = controller.Views[orb.OrbId].transform.position;
            controller.Attack.PublishInventoryChange("p1-unchanged-authoritative-inventory-fixture");
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var after), Is.True);
            Assert.That(Vector2.Distance(after, before), Is.LessThan(.001f), "An ordinary snapshot must not reset local inertia.");
            Assert.That(Vector2.Distance(controller.Views[orb.OrbId].transform.position, positionBefore), Is.LessThan(.001f),
                "An ordinary snapshot must not teleport a coasting view to its spawn position.");
            yield return WaitFor(() => Vector2.Distance(controller.Views[orb.OrbId].transform.position, positionBefore) > .001f,
                "The interpolated coasting view did not move after the unchanged snapshot.");
            Physics2D.SyncTransforms();
            Vector2 start = controller.GetViewScreenPosition(orb.OrbId);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            controller.MovePointer(id, start + Vector2.up * 12f);
            yield return null;
            Assert.That(controller.Views[orb.OrbId].HeldFeedbackActive, Is.True);
            controller.CancelPointer(id);
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].HeldFeedbackActive, Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var cancelled), Is.True);
            Assert.That(cancelled.sqrMagnitude, Is.LessThan(.0001f));
            Vector2 cancelledAt = controller.Views[orb.OrbId].transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Vector2.Distance(controller.Views[orb.OrbId].transform.position, cancelledAt), Is.LessThan(.01f));
            Assert.That(controller.Combination.LastResult, Is.Null);
        }

        [UnityTest]
        public IEnumerator UnconfirmedMaterialReservationFreezesBothBodiesUntilSessionCleanup()
        {
            yield return StartSolo();
            var pair = SupplyPair(false);
            yield return PublishFixture();
            var held = controller.Attack.Registry.Reserve(controller.Attack.LocalPlayerId, new OrbActionRequest(
                controller.Attack.Snapshot.sessionId, controller.Attack.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                pair[1].OrbId, null, OrbActionKind.TransferLeft, 1, pair[1].NormalizedPosition));
            Assert.That(held.Accepted, Is.True, "The fixture reserves a material without implementing or completing a transfer.");
            int id = BeginDrop(pair[0].OrbId, pair[1].OrbId, out var end);
            controller.EndPointer(id, end);
            Assert.That(controller.Combination.LastResult.reason, Is.EqualTo("OTHER_ORB_PENDING"));
            Assert.That(controller.HasCombinationPending && controller.Combination.HasPending, Is.True);
            var positions = pair.ToDictionary(o => o.OrbId, o => (Vector2)controller.Views[o.OrbId].transform.position);
            yield return new WaitForSecondsRealtime(.2f);
            foreach (var orb in pair)
            {
                Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Pending));
                Assert.That(controller.Views[orb.OrbId].GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
                Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var velocity), Is.True);
                Assert.That(velocity.sqrMagnitude, Is.LessThan(.0001f));
                Assert.That(Vector2.Distance(positions[orb.OrbId], controller.Views[orb.OrbId].transform.position), Is.LessThan(.001f));
                Assert.That(controller.BeginPointer(++pointer, controller.GetViewScreenPosition(orb.OrbId), false), Is.False);
            }
            controller.EndDevelopmentTest();
            yield return WaitFor(() => !controller.Attack.Connected, "The explicit End did not close the reserved session.");
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.OrbPhysics.Count, Is.Zero);
            foreach (var orb in pair) Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out _), Is.False);
            Assert.That(controller.HasCombinationPending || controller.Gestures.HasPending, Is.False);
        }

        [UnityTest]
        public IEnumerator ActualShortRoundExpiryStopsMotionAndRetryRemovesOldBodies()
        {
            yield return StartSolo(2);
            var orb = Supply(OrbKind.Raw, OrbPolarity.Yin, new Vector2(.35f, .55f));
            yield return PublishFixture();
            ReleaseSample(orb.OrbId, Vector2.right * WorldRadius(orb.OrbId) * .8f);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat, "The explicit two-second fixture did not expire.");
            Assert.That(controller.Battle.Snapshot.shortDuration, Is.True);
            Assert.That(controller.Battle.Snapshot.remaining, Is.Zero);
            if (controller.Views.TryGetValue(orb.OrbId, out var stopped))
            {
                Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var velocity), Is.True);
                Assert.That(velocity.sqrMagnitude, Is.LessThan(.0001f));
                Vector2 position = stopped.transform.position;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Vector2.Distance(stopped.transform.position, position), Is.LessThan(.001f));
            }
            Assert.That(controller.RequestGenerate(), Is.False);
            uint oldRound = controller.Battle.Snapshot.roundId;
            Assert.That(controller.Battle.RetryHost(), Is.True);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.roundId > oldRound,
                "Retry did not enter a new empty Ready round.");
            Assert.That(controller.Views, Is.Empty);
            Assert.That(controller.OrbPhysics.Count, Is.Zero);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out _), Is.False);
            Assert.That(controller.Resource.LocalPlayer.stamina, Is.EqualTo(100));
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
        }

        private IEnumerator StartSolo(double? duration = null)
        {
            controller.ConfigureDevelopmentSolo(true);
            controller.ConfigureDevelopmentDuration(duration);
            Assert.That(controller.StartDevelopmentHost("25131"), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart && controller.Combination.IsHost && controller.Resource.IsHost,
                "The P1 test Host did not bind its existing battle services.");
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract, "The P1 test Host did not start Playing.");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
        }

        private OrbRecord Supply(OrbKind kind, OrbPolarity polarity, Vector2 position) =>
            controller.Attack.Registry.RegisterDevelopmentOrb(controller.Attack.LocalPlayerId, kind, polarity, position);

        private OrbRecord[] SupplyPair(bool samePolarity) => new[]
        {
            Supply(OrbKind.Raw, OrbPolarity.Yin, new Vector2(.3f, .55f)),
            Supply(OrbKind.Raw, samePolarity ? OrbPolarity.Yin : OrbPolarity.Yang, new Vector2(.6f, .55f))
        };

        private IEnumerator PublishFixture()
        {
            controller.Attack.PublishInventoryChange("p1-explicit-local-physics-geometry-fixture");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            foreach (var view in controller.Views.Values)
                Assert.That(view.GetComponent<Rigidbody2D>(), Is.Not.Null, "Every opt-in local view needs a physical body.");
        }

        private int BeginDrop(string source, string target, out Vector2 end)
        {
            Physics2D.SyncTransforms();
            Vector2 start = controller.GetViewScreenPosition(source);
            end = controller.GetViewScreenPosition(target);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            controller.MovePointer(id, end);
            return id;
        }

        private void ReleaseSample(string id, Vector2 displacement)
        {
            Vector2 start = controller.Views[id].transform.position;
            double time = Now - .08d;
            controller.OrbPhysics.Grab(id, time);
            controller.OrbPhysics.SetPosition(id, start + displacement, true, time + .06d);
            controller.OrbPhysics.Release(id, time + .06d, true);
            Assert.That(controller.OrbPhysics.TryGetVelocity(id, out var velocity), Is.True);
            Assert.That(velocity.magnitude, Is.GreaterThan(.01f), "The explicit release fixture must really coast.");
        }

        private float WorldRadius(string id)
        {
            var view = controller.Views[id];
            return view.Collider.radius * Mathf.Abs(view.transform.lossyScale.x);
        }
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Now + 5;
            while (!condition() && Now < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
