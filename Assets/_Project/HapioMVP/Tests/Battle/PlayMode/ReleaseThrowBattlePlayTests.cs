using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Saved legacy scene with explicit P2 opt-in and real local Host. Pointer/Combined are fixtures, not Touch evidence.</summary>
    public sealed class ReleaseThrowBattlePlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private T09BattleController controller;
        private bool previousRunInBackground;
        private int pointer;
        private readonly List<AttackRequestReply> replies = new List<AttackRequestReply>();

        [UnitySetUp]
        public IEnumerator OpenLegacySceneAndOptIntoReleaseThrows()
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
            controller = Object.FindAnyObjectByType<T09BattleController>(); pointer = 21500;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.ReleaseThrowsEnabled, Is.False, "The historical scene remains opt-out.");
            controller.ConfigureOrbPhysics(true);
            controller.ConfigureReleaseThrows(true);
            replies.Clear(); controller.Attack.RequestResolved += replies.Add;
            controller.ConfigureDevelopmentSolo(true);
            Assert.That(controller.StartDevelopmentHost("25132"), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart && controller.Combination.IsHost && controller.Resource.IsHost,
                "The P2 fixture Host did not bind its battle services.");
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract, "The P2 fixture did not enter Playing.");
        }

        [UnityTearDown]
        public IEnumerator CloseOnlyTheFixtureHostAndScene()
        {
            try
            {
                if (controller != null)
                {
                    controller.Attack.RequestResolved -= replies.Add;
                    controller.EndDevelopmentTest();
                }
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), "The P2 fixture Host did not stop.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("P2 release throw cleanup"));
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

        private OrbRecord Supply(OrbKind kind = OrbKind.Combined) => controller.Attack.Registry.RegisterDevelopmentOrb(
            controller.Attack.LocalPlayerId, kind, kind == OrbKind.Raw ? OrbPolarity.Yin : OrbPolarity.None, new Vector2(.5f, .6f));
        private IEnumerator Publish()
        {
            controller.Attack.PublishInventoryChange("p2-explicit-release-gesture-fixture");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
        }
        private int Begin(string id)
        {
            int finger = ++pointer;
            Assert.That(controller.BeginPointer(finger, controller.GetViewScreenPosition(id), false), Is.True);
            return finger;
        }
        private Vector2 Upper(float extraWidth = .04f) => new Vector2(controller.Layout.BottomPixelRect.center.x,
            controller.Layout.BottomPixelRect.yMax + Screen.width * extraWidth);
        private void AssertKept(OrbRecord orb)
        {
            Assert.That(controller.Views.ContainsKey(orb.OrbId), Is.True);
            Assert.That(controller.Attack.Registry.TryGet(orb.OrbId, out var kept), Is.True);
            Assert.That(kept.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(replies, Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
        }
        private IEnumerator MovingUpperRelease(int finger)
        {
            Vector2 start = Upper(-.025f);
            controller.MovePointer(finger, start);
            yield return new WaitForSecondsRealtime(.16f);
            double began = Time.unscaledTimeAsDouble;
            Vector2 end = start;
            do
            {
                yield return new WaitForSecondsRealtime(.02f);
                // Real elapsed sample time, not the requested wait, keeps swipe speed stable across render rates.
                end = start + Vector2.up * (float)((Time.unscaledTimeAsDouble - began) * Screen.width * 1.25);
                Assert.That(end.y, Is.LessThanOrEqualTo(Screen.height), "The fixture must release inside the actual display.");
                controller.MovePointer(finger, end);
            } while (Time.unscaledTimeAsDouble - began < .1);
            Assert.That(controller.ThrowArmed, Is.True);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty, "All Move samples remain reversible.");
            Assert.That(replies, Is.Empty);
            controller.EndPointer(finger, end);
        }

        [UnityTest]
        public IEnumerator StartingHostModeChangeRejectsBeforeEitherReleaseFlagChanges()
        {
            controller.EndDevelopmentTest();
            yield return WaitFor(() => controller.Attack.Connection.CanStart && !controller.Attack.Connected,
                "The previous fixture Host did not finish its normal shutdown.");
            bool observedStarting = false, wasConnected = true, rejected = false;
            bool controllerModeAfterAttempt = false, sessionModeAfterAttempt = false;
            void TryChangeDuringSynchronousStart()
            {
                if (controller.Attack.Connection.State != DirectConnectionState.StartingHost) return;
                observedStarting = true;
                wasConnected = controller.Attack.Connected;
                try { controller.ConfigureReleaseThrows(false); }
                catch (InvalidOperationException) { rejected = true; }
                controllerModeAfterAttempt = controller.ReleaseThrowsEnabled;
                sessionModeAfterAttempt = controller.Attack.ReleaseThrowsEnabled;
            }
            controller.Attack.Connection.Changed += TryChangeDuringSynchronousStart;
            try
            {
                // This event runs inside Host start, before an NGO connection event can
                // make the controller's Connected guard mask the session's earlier guard.
                Assert.That(controller.StartDevelopmentHost("25132"), Is.True);
                Assert.That(observedStarting, Is.True);
                Assert.That(wasConnected, Is.False, "The regression requires the in-progress, not already-connected branch.");
                Assert.That(rejected, Is.True);
                Assert.That(controllerModeAfterAttempt, Is.True, "A rejected session change cannot first mutate controller mode.");
                Assert.That(sessionModeAfterAttempt, Is.True);
                Assert.That(controller.ReleaseThrowsEnabled && controller.Attack.ReleaseThrowsEnabled, Is.True);
            }
            finally { controller.Attack.Connection.Changed -= TryChangeDuringSynchronousStart; }
        }

        [UnityTest]
        public IEnumerator HoldingInBattleDoesNotReserveConsumeOrSpawnAndStationaryReleaseKeepsOrb()
        {
            var orb = Supply(); yield return Publish();
            int finger = Begin(orb.OrbId);
            controller.MovePointer(finger, Upper());
            Assert.That(controller.ThrowArmed, Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            AssertKept(orb);
            Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(finger));
            controller.EndPointer(finger, Upper());
            yield return new WaitForSecondsRealtime(.1f);
            AssertKept(orb);
            Assert.That(controller.ThrowArmed || controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].HeldFeedbackActive, Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var velocity), Is.True);
            Assert.That(velocity.sqrMagnitude, Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator MovingReleaseLaunchesExactlyOnceAndActualHostPhysicsHits()
        {
            var orb = Supply(); yield return Publish();
            int finger = Begin(orb.OrbId);
            yield return MovingUpperRelease(finger);
            Assert.That(controller.Views.ContainsKey(orb.OrbId), Is.False);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out _), Is.False);
            var projectile = Object.FindObjectsByType<HostProjectile3D>().Single(item => item.OrbId == orb.OrbId);
            Assert.That(projectile.BallisticActive, Is.True);
            Assert.That(projectile.InitialVelocity.y, Is.GreaterThan(0));
            Assert.That(projectile.InitialVelocity.z, Is.GreaterThan(0));
            Assert.That(controller.ThrowArmed || controller.Gestures.HasActivePointer, Is.False);
            controller.EndPointer(finger, Upper());
            controller.MovePointer(finger, Upper(.08f));
            controller.CancelPointer(finger);
            Assert.That(controller.Hud.ActionLabel.text, Is.Not.EqualTo("THROW READY"));
            Assert.That(replies.Count(reply => reply.accepted && reply.orbId == orb.OrbId), Is.EqualTo(1));
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, "The release-created Rigidbody did not actually hit the target.");
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.TryGet(orb.OrbId, out var consumed), Is.True);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
        }

        [UnityTest]
        public IEnumerator ReturnToLowerDisarmsThenAnOrdinaryReleaseCoastsWithoutAttack()
        {
            var orb = Supply(); yield return Publish();
            int finger = Begin(orb.OrbId);
            Vector2 lower = controller.GetViewScreenPosition(orb.OrbId);
            controller.MovePointer(finger, Upper());
            Assert.That(controller.ThrowArmed, Is.True);
            controller.MovePointer(finger, lower);
            Assert.That(controller.ThrowArmed, Is.False);
            yield return new WaitForSecondsRealtime(.18f);
            Vector2 end = lower;
            for (int i = 1; i <= 4; i++)
            {
                yield return new WaitForSecondsRealtime(.02f);
                end = lower + Vector2.right * (controller.OrbGridScreenRect.width * .08f * i / 4f);
                controller.MovePointer(finger, end);
            }
            controller.EndPointer(finger, end);
            AssertKept(orb);
            Assert.That(controller.OrbPhysics.TryGetVelocity(orb.OrbId, out var velocity), Is.True);
            Assert.That(velocity.x, Is.GreaterThan(.01f));
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
        }

        [UnityTest]
        public IEnumerator CancellationAndRawUpperReleasePreserveTheirOrbs()
        {
            var combined = Supply(); yield return Publish();
            int finger = Begin(combined.OrbId);
            controller.MovePointer(finger, Upper());
            controller.CancelPointer(finger);
            Assert.That(controller.Hud.ActionLabel.text, Is.Not.EqualTo("THROW READY"));
            controller.EndPointer(finger, Upper(.06f));
            AssertKept(combined);
            Assert.That(controller.ThrowArmed, Is.False);
            // Add a second explicit fixture through the same development registry path.
            var raw = controller.Attack.Registry.RegisterDevelopmentOrb(controller.Attack.LocalPlayerId,
                OrbKind.Raw, OrbPolarity.Yin, new Vector2(.25f, .6f));
            yield return Publish();
            finger = Begin(raw.OrbId);
            controller.MovePointer(finger, Upper());
            Assert.That(controller.ThrowArmed, Is.False);
            yield return new WaitForSecondsRealtime(.04f);
            controller.EndPointer(finger, Upper(.05f));
            AssertKept(raw);
            Assert.That(controller.Views.Count, Is.EqualTo(2));
            Assert.That(controller.OrbPhysics.TryGetVelocity(raw.OrbId, out var velocity), Is.True);
            Assert.That(velocity.sqrMagnitude, Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator ExtraPointerCannotStealClearOrReleaseTheOwnersThrow()
        {
            var orb = Supply(); yield return Publish();
            int owner = Begin(orb.OrbId);
            Vector2 original = controller.GetViewScreenPosition(orb.OrbId);
            int other = ++pointer;
            Assert.That(controller.BeginPointer(other, original, false), Is.False);
            controller.MovePointer(other, Upper());
            controller.EndPointer(other, Upper());
            controller.CancelPointer(other);
            Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(owner));
            AssertKept(orb);
            yield return MovingUpperRelease(owner);
            Assert.That(replies.Count(reply => reply.accepted && reply.orbId == orb.OrbId), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>().Count(item => item.OrbId == orb.OrbId), Is.EqualTo(1));
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
