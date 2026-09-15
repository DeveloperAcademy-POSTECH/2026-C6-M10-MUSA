using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
    /// Controller regressions in a saved historical scene with explicit continuous opt-in.
    /// Registry/queue fixtures reproduce delayed observation; they are not network or Touch evidence.
    /// </summary>
    public sealed class ContinuousTransferControllerPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private T09BattleController controller;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator OpenSavedSceneWithExplicitContinuousOptIn()
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
            controller = Object.FindAnyObjectByType<T09BattleController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.ContinuousTransfersEnabled, Is.False, "Historical scene is not silently opted in.");
            controller.ConfigureOrbPhysics(true);
            controller.ConfigureTransfers(true);
            controller.Attack.ConfigureTransfers(true, controller.Layout.Config.OrbStorageLimit,
                controller.Layout.Config.OrbRadiusScreenFraction);
            controller.ConfigureContinuousTransfers(true);
        }

        [UnityTearDown]
        public IEnumerator CloseTestConnectionAndScene()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), "Continuous test connection did not stop.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Continuous Controller Cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator TransferFirstObservedWithActualDeadlineResultStaysStoppedWithoutThrowing()
        {
            yield return StartSolo(1d);
            ulong owner = controller.Attack.LocalPlayerId;
            var incoming = controller.Attack.Registry.RegisterDevelopmentOrb(owner + 1, OrbKind.Raw,
                OrbPolarity.Yin, new Vector2(.5f, .5f));
            // Commit before the actual deadline, but do not publish an intermediate Playing
            // attack snapshot. The terminal publication first reveals this ownership epoch.
            TransferFixture(incoming, owner, true, new Vector2(.3f, .02f));
            Assert.That(controller.Views.ContainsKey(incoming.OrbId), Is.False);
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Defeat,
                "The explicit one-second test round did not expire.");
            Assert.That(controller.Attack.Snapshot.orbs.Any(orb => orb.id == incoming.OrbId && orb.transferCount == 1), Is.True);
            Assert.That(controller.Views.ContainsKey(incoming.OrbId), Is.True);
            Assert.That(controller.ReceivedTransfers, Is.EqualTo(1));
            Assert.That(controller.OrbPhysics.Paused, Is.True);
            Assert.That(Velocity(incoming.OrbId), Is.EqualTo(Vector2.zero));
            var body = controller.Views[incoming.OrbId].GetComponent<Rigidbody2D>();
            Vector2 position = body.position;
            controller.Attack.PublishInventoryChange("p4-terminal-repeat-observation-fixture");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(controller.ReceivedTransfers, Is.EqualTo(1), "The terminal epoch must be marked observed once.");
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(Velocity(incoming.OrbId), Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator RepeatedAuthoritativeSnapshotDoesNotReapplyArrivalMotionOrTeleportItsBody()
        {
            yield return StartSolo();
            ulong owner = controller.Attack.LocalPlayerId;
            var incoming = controller.Attack.Registry.RegisterDevelopmentOrb(owner + 1, OrbKind.Raw,
                OrbPolarity.Yin, new Vector2(.5f, .5f));
            TransferFixture(incoming, owner, true, new Vector2(.4f, .02f));
            controller.Attack.PublishInventoryChange("p4-first-approved-arrival-fixture");
            Assert.That(controller.ReceivedTransfers, Is.EqualTo(1));
            var body = controller.Views[incoming.OrbId].GetComponent<Rigidbody2D>();
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0));
            // A later local position and reduced speed must survive a repeated wire epoch.
            body.position += Vector2.right * controller.OrbPhysics.CenterBounds.width * .15f;
            body.linearVelocity *= .6f;
            Vector2 position = body.position, velocity = body.linearVelocity;
            controller.Attack.PublishInventoryChange("p4-unchanged-arrival-epoch-fixture");
            Assert.That(controller.ReceivedTransfers, Is.EqualTo(1));
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(body.linearVelocity, Is.EqualTo(velocity));
            Assert.That(controller.OrbPhysics.TryGetPendingEdge(incoming.OrbId, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ReturnedOwnershipEpochRetiresOldOutgoingPendingAndIgnoresItsDelayedReceipt()
        {
            yield return StartSolo();
            ulong owner = controller.Attack.LocalPlayerId;
            var original = controller.Attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw,
                OrbPolarity.Yin, new Vector2(.5f, .5f));
            controller.Attack.PublishInventoryChange("p4-local-before-unobserved-roundtrip-fixture");
            var sent = TransferFixture(original, owner + 1, true, new Vector2(.4f, 0));
            var originalRequest = LastFixtureRequest;
            var pending = (IDictionary)typeof(T09BattleController).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(controller);
            Type entryType = typeof(T09BattleController).GetNestedType("PendingInput", BindingFlags.NonPublic);
            var pendingEntry = Activator.CreateInstance(entryType, true);
            entryType.GetField("request").SetValue(pendingEntry, originalRequest);
            entryType.GetField("sentAt").SetValue(pendingEntry, Time.unscaledTime);
            pending.Add(originalRequest.RequestId, pendingEntry);
            controller.OrbPhysics.SetLocked(original.OrbId, true);
            TransferFixture(sent, owner, true, new Vector2(.3f, .01f));
            // The intermediate departure snapshot and receipt were deliberately not observed.
            controller.Attack.PublishInventoryChange("p4-return-supersedes-old-outgoing-fixture");
            Assert.That(pending.Count, Is.Zero);
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Views.ContainsKey(original.OrbId), Is.True);
            var body = controller.Views[original.OrbId].GetComponent<Rigidbody2D>();
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0));
            Vector2 position = body.position, velocity = body.linearVelocity;
            var late = new AttackRequestReply
            {
                sessionId = originalRequest.SessionId, roundId = originalRequest.RoundId,
                requestId = originalRequest.RequestId, orbId = original.OrbId,
                known = true, accepted = true, pending = false, reason = "EXPLICIT_DELAYED_RECEIPT_FIXTURE",
                confirmedOrb = OrbWire.FromRecord(sent)
            };
            typeof(T09BattleController).GetMethod("OnResolved", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, new object[] { late });
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(body.linearVelocity, Is.EqualTo(velocity));
            Assert.That(controller.ReceivedTransfers, Is.EqualTo(1));
            Assert.That(controller.Attack.Snapshot.orbs.Single(orb => orb.id == original.OrbId).transferCount, Is.EqualTo(2));
        }

        private OrbActionRequest LastFixtureRequest;
        private OrbRecord TransferFixture(OrbRecord source, ulong destination, bool toRight, Vector2 velocity)
        {
            var registry = controller.Attack.Registry;
            var motion = new OrbTransferMotion(velocity, controller.Attack.MotionServerTime);
            var request = new OrbActionRequest(registry.SessionId, registry.RoundId, Guid.NewGuid().ToString("N"),
                source.OrbId, null, toRight ? OrbActionKind.TransferRight : OrbActionKind.TransferLeft,
                source.SequenceNumber + 1, new Vector2(toRight ? 1 : 0, .5f), transferMotion: motion);
            var reserved = registry.Reserve(source.OwnerPlayerId, request);
            Assert.That(reserved.Accepted, Is.True, reserved.Reason);
            Assert.That(registry.TryCompleteReservedTransfer(reserved.Reservation, destination,
                controller.Layout.Config.OrbStorageLimit, controller.Layout.Config.OrbRadiusScreenFraction,
                out var result, motion), Is.True);
            LastFixtureRequest = request;
            return result;
        }

        private IEnumerator StartSolo(double? duration = null)
        {
            controller.ConfigureDevelopmentSolo(true);
            controller.ConfigureDevelopmentDuration(duration);
            Assert.That(controller.StartDevelopmentHost("25137"), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart && controller.Combination.IsHost && controller.Resource.IsHost,
                "Continuous test Host did not bind its battle services.");
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract, "Continuous test Host did not enter Playing.");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            Assert.That(controller.OrbPhysics.HorizontalPassageEnabled, Is.True);
        }

        private Vector2 Velocity(string id)
        { Assert.That(controller.OrbPhysics.TryGetVelocity(id, out var velocity), Is.True); return velocity; }
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Now + 5;
            while (!condition() && Now < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
