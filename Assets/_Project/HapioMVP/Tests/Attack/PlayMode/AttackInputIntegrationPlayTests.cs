using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace C6.Prototype.Attack.Tests
{
    /// <summary>Saved scene + actual NGO host + PhysX. Queued touch is not physical iPhone evidence.</summary>
    public sealed class AttackInputIntegrationPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/AttackSmoke.unity";
        private const string TestPort = "25067";
        private readonly List<InputDevice> ownedDevices = new List<InputDevice>();
        private T06AttackController controller;
        private OrbPointerInput input;
        private Mouse previousMouse;
        private Touchscreen previousTouchscreen;
        private InputSettings previousInputSettings;
        private InputSettings runtimeInputSettings;
        private HideFlags previousInputSettingsHideFlags;
        private bool capturedInputSettings;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedSceneWithTestOwnedInputRouting()
        {
            previousMouse = Mouse.current;
            previousTouchscreen = Touchscreen.current;
            previousInputSettings = InputSystem.settings;
            previousRunInBackground = Application.runInBackground;
            Assert.That(previousInputSettings != null, Is.True, "Input settings must be live before this fixture takes ownership.");
            previousInputSettingsHideFlags = previousInputSettings.hideFlags;
            capturedInputSettings = true;
            // Input System 1.20 InputManager.settings destroys the previous object when its flags
            // are exactly HideAndDontSave. Retain that object while temporarily swapping settings;
            // DontSave also keeps it alive across the saved-scene load. Restore its exact flags below.
            previousInputSettings.hideFlags = previousInputSettingsHideFlags == HideFlags.HideAndDontSave
                ? HideFlags.DontSave : previousInputSettingsHideFlags | HideFlags.DontUnloadUnusedAsset;
            runtimeInputSettings = Object.Instantiate(previousInputSettings);
            runtimeInputSettings.name = "T06 queued-input routing (not a project asset)";
            runtimeInputSettings.hideFlags = HideFlags.DontSave;
            runtimeInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            runtimeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            runtimeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            InputSystem.settings = runtimeInputSettings;
            yield return LoadScene();
            yield return null;
            yield return null;
            controller = Object.FindAnyObjectByType<T06AttackController>();
            input = Object.FindAnyObjectByType<OrbPointerInput>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.CanInteract, Is.False);
            Assert.That(controller.Attack.Connected, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "Opening the saved scene must not silently create a development host.");
        }

        [UnityTearDown]
        public IEnumerator RestoreInputAndReleaseOwnedSceneAndHost()
        {
            try
            {
                foreach (var device in ownedDevices)
                    if (device.added) InputSystem.RemoveDevice(device);
                ownedDevices.Clear();
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                if (previousTouchscreen != null && previousTouchscreen.added) previousTouchscreen.MakeCurrent();
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), 5f, "The test host did not finish shutdown.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var cleanup = SceneManager.CreateScene("T06 Integration Cleanup");
                    SceneManager.SetActiveScene(cleanup);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<T06AttackController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally
            {
                try
                {
                    if (capturedInputSettings)
                    {
                        Assert.That(previousInputSettings != null, Is.True,
                            "The T06 fixture must preserve the input settings object it temporarily replaces.");
                        InputSystem.settings = previousInputSettings;
                        Assert.That(ReferenceEquals(InputSystem.settings, previousInputSettings), Is.True);
                    }
                }
                finally
                {
                    Application.runInBackground = previousRunInBackground;
                    if (capturedInputSettings && previousInputSettings != null)
                        previousInputSettings.hideFlags = previousInputSettingsHideFlags;
                    // Destroy only our detached clone. Synchronous destruction prevents a deferred
                    // destroy from crossing into the next fixture's setup or input update.
                    if (runtimeInputSettings != null && !ReferenceEquals(InputSystem.settings, runtimeInputSettings))
                        Object.DestroyImmediate(runtimeInputSettings);
                    runtimeInputSettings = null;
                }
            }
            // Advance the player loop and verify cleanup cannot leave a dangling global settings reference.
            yield return null;
            if (capturedInputSettings)
            {
                Assert.That(previousInputSettings != null, Is.True);
                Assert.That(ReferenceEquals(InputSystem.settings, previousInputSettings), Is.True);
                Assert.That(previousInputSettings.hideFlags, Is.EqualTo(previousInputSettingsHideFlags));
            }
            previousInputSettings = null;
            capturedInputSettings = false;
        }

        [UnityTest]
        public IEnumerator SavedSceneRequiresExplicitHostAndRegistersFiveCombinedPlusOneRaw()
        {
            Assert.That(Object.FindObjectsByType<T06AttackController>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<T05OrbController>(), Is.Empty,
                "T06 must not also run the preserved T05 scene's reservation controller.");
            Assert.That(Object.FindObjectsByType<OrbPointerInput>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsByType<AudioListener>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(), Has.Length.EqualTo(1));
            var marker = Object.FindAnyObjectByType<MonsterHitTarget>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<BoxCollider>(), Has.Length.EqualTo(1));
            Assert.That(marker.GetComponent<Rigidbody>(), Is.Null);
            yield return StartHost();
            var records = controller.Attack.Registry.Snapshot();
            var manager = controller.GetComponent<DirectConnectionSession>().OwnedManager;
            Assert.That(manager.IsHost && manager.IsListening, Is.True);
            Assert.That(records.Count, Is.EqualTo(6));
            Assert.That(records.Count(orb => orb.Kind == OrbKind.Combined), Is.EqualTo(5));
            Assert.That(records.Count(orb => orb.Kind == OrbKind.Raw), Is.EqualTo(1));
            Assert.That(records.Select(orb => orb.OrbId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(records.All(orb => orb.OwnerPlayerId == manager.LocalClientId && orb.AuthorityState == OrbAuthorityState.Idle), Is.True);
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Attack.Snapshot.state, Is.EqualTo("Playing"));
            Assert.That(controller.TouchBegins + controller.TouchLaunches, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ZoneEntryRemovesTwoDimensionalViewAndHitsWithTheSameLogicalIdOnce()
        {
            yield return StartHost();
            var orb = Combined().First();
            var originalView = controller.Views[orb.OrbId];
            Vector2 start = controller.GetViewScreenPosition(orb.OrbId);
            Vector2 zone = ZonePoint(start.x);
            Assert.That(controller.BeginPointer(6061, start, false), Is.True);
            controller.MovePointer(6061, zone);
            Assert.That(controller.Views.ContainsKey(orb.OrbId), Is.False, "Only host-confirmed launch removes the 2D view.");
            Assert.That(controller.Attack.Registry.TryGet(orb.OrbId, out var flying), Is.True);
            Assert.That(flying.AuthorityState, Is.EqualTo(OrbAuthorityState.Projectile));
            Assert.That(flying.OwnerPlayerId, Is.EqualTo(orb.OwnerPlayerId));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100), "Approval cannot replace real flight with immediate damage.");
            var body = Object.FindObjectsByType<HostProjectile3D>().Single();
            Assert.That(body.OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(body.AttackerPlayerId, Is.EqualTo(orb.OwnerPlayerId));
            Assert.That(body.Body.isKinematic, Is.False);
            controller.MovePointer(6061, zone + Vector2.up * 20f);
            controller.EndPointer(6061, zone);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Has.Length.EqualTo(1));
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 4f, "The actual launched Rigidbody did not hit the fixed target.");
            Assert.That(controller.Attack.Registry.TryGet(orb.OrbId, out var consumed), Is.True);
            Assert.That(consumed.OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Attack.Registry.Snapshot().Count, Is.EqualTo(6), "Launch keeps the record ID through final consumption.");
            Assert.That(controller.TouchLaunches, Is.Zero, "Controller debug pointer calls are not real or queued touch evidence.");
            yield return null;
            Assert.That(originalView == null, Is.True);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T06_INTEGRATION sameId=true removed2D=true realHit=1 hp=80 source=DEBUG_POINTER");
        }

        [UnityTest]
        public IEnumerator FiveRealHitsClearTargetAndExplicitResetRestoresFreshRoundWhileKeepingSessionTotals()
        {
            yield return StartHost();
            string[] oldIds = controller.Attack.Registry.Snapshot().Select(orb => orb.OrbId).ToArray();
            uint oldRound = controller.Attack.Snapshot.roundId;
            string[] combinedIds = Combined().Select(orb => orb.OrbId).ToArray();
            for (int index = 0; index < combinedIds.Length; index++)
            {
                string id = combinedIds[index];
                Vector2 start = controller.GetViewScreenPosition(id);
                int pointer = 6100 + index;
                Assert.That(controller.BeginPointer(pointer, start, false), Is.True);
                controller.MovePointer(pointer, ZonePoint(start.x));
                controller.EndPointer(pointer, ZonePoint(start.x));
                int expectedHp = 100 - ((index + 1) * 20);
                yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp, 4f,
                    "Real hit " + (index + 1) + " did not apply its single configured 20 damage.");
            }
            Assert.That(controller.Attack.Snapshot.state, Is.EqualTo("TargetCleared"));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(5));
            Assert.That(controller.CanInteract, Is.False);
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);
            string rawId = controller.Views.Keys.Single();
            Assert.That(controller.BeginPointer(6199, controller.GetViewScreenPosition(rawId), false), Is.False);
            controller.ResetDevelopmentRound();
            yield return WaitFor(() => controller.Attack.Snapshot.roundId > oldRound && controller.Views.Count == 6,
                3f, "Explicit DEV reset did not create the fresh confirmed round.");
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Attack.Snapshot.state, Is.EqualTo("Playing"));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(5));
            Assert.That(controller.Attack.Snapshot.resets, Is.EqualTo(1));
            Assert.That(controller.Views.Keys.Intersect(oldIds), Is.Empty);
            Assert.That(controller.CanInteract, Is.True);
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);
            yield return null;
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            controller.EndDevelopmentTest();
            yield return WaitFor(() => controller.Attack.Connection.CanStart, 5f, "Manual End did not finish stopping.");
            Assert.That(controller.Hud.HostButton.interactable, Is.True, "Host must re-enable after asynchronous Stop without an unrelated input.");
            Assert.That(controller.Hud.JoinButton.interactable, Is.True);
            Debug.Log("C6_T06_INTEGRATION realHits=5 targetHp=0 explicitReset=1 freshHp=100 totalHits=5 source=DEBUG_POINTER");
        }

        [UnityTest]
        public IEnumerator RawVerticalDragAndCancelledCombinedCannotLaunchOrLoseTheirViews()
        {
            yield return StartHost();
            var raw = controller.Attack.Registry.Snapshot().Single(orb => orb.Kind == OrbKind.Raw);
            Vector2 start = controller.GetViewScreenPosition(raw.OrbId);
            Assert.That(controller.BeginPointer(6201, start, false), Is.True);
            controller.MovePointer(6201, ZonePoint(start.x));
            controller.EndPointer(6201, ZonePoint(start.x));
            Assert.That(controller.Views.ContainsKey(raw.OrbId), Is.True);
            Assert.That(controller.Attack.Registry.TryGet(raw.OrbId, out var preserved), Is.True);
            Assert.That(preserved.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            var combined = Combined().First();
            Vector2 combinedStart = controller.GetViewScreenPosition(combined.OrbId);
            Assert.That(controller.BeginPointer(6202, combinedStart, false), Is.True);
            controller.MovePointer(6202, combinedStart + Vector2.up * 2f);
            controller.CancelPointer(6202);
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);
            Assert.That(Vector2.Distance(controller.GetViewScreenPosition(combined.OrbId), combinedStart), Is.LessThan(1f));
            yield return new WaitForFixedUpdate();
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.Zero);
            Assert.That(controller.Views.Count, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator QueuedEnhancedTouchRecordsItsSourceAndCancellationCannotCreateAnExtraHit()
        {
            yield return StartHost();
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            ownedDevices.Add(touchscreen);
            string id = Combined().First().OrbId;
            Vector2 start = controller.GetViewScreenPosition(id);
            yield return TouchSample(touchscreen, 6301, start, TouchPhase.Began);
            Assert.That(controller.TouchBegins, Is.EqualTo(1));
            Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(6301));
            yield return TouchSample(touchscreen, 6301, ZonePoint(start.x), TouchPhase.Moved);
            Assert.That(controller.TouchLaunches, Is.EqualTo(1));
            Assert.That(controller.Views.ContainsKey(id), Is.False);
            yield return TouchSample(touchscreen, 6301, ZonePoint(start.x), TouchPhase.Ended);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 4f, "Queued Touch did not reach actual launch/physics.");
            string cancelId = Combined().First().OrbId;
            Vector2 cancelStart = controller.GetViewScreenPosition(cancelId);
            yield return TouchSample(touchscreen, 6302, cancelStart, TouchPhase.Began);
            yield return TouchSample(touchscreen, 6302, cancelStart + Vector2.up * 2f, TouchPhase.Moved);
            yield return TouchSample(touchscreen, 6302, cancelStart + Vector2.up * 2f, TouchPhase.Canceled);
            Assert.That(input.LastTouchId, Is.EqualTo(6302));
            Assert.That(input.LastTouchDeviceId, Is.EqualTo(touchscreen.deviceId));
            Assert.That(input.TouchCancelCount, Is.EqualTo(1));
            Assert.That(controller.TouchBegins, Is.EqualTo(2));
            Assert.That(controller.TouchLaunches, Is.EqualTo(1));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(80));
            Assert.That(controller.Attack.Snapshot.totalHits, Is.EqualTo(1));
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);
            Assert.That(controller.Views.ContainsKey(cancelId), Is.True);
            Assert.That(Object.FindObjectsByType<HostProjectile3D>(), Is.Empty);
            Debug.Log("C6_T06_INTEGRATION source=QUEUED_ENHANCED_TOUCH touchBegins=2 touchLaunches=1 cancels=1 realHits=1 physicalDevice=false");
        }

        private IEnumerable<OrbRecord> Combined() => controller.Attack.Registry.Snapshot()
            .Where(orb => orb.Kind == OrbKind.Combined && orb.AuthorityState == OrbAuthorityState.Idle)
            .OrderBy(orb => orb.NormalizedPosition.x);

        private Vector2 ZonePoint(float x) => new Vector2(x, controller.Layout.BottomPixelRect.yMax - 1f);

        private IEnumerator StartHost()
        {
            Assert.That(controller.StartDevelopmentHost(TestPort), Is.True);
            yield return WaitFor(() => controller.Attack.IsHost && controller.CanInteract && controller.Views.Count == 6,
                5f, "The actual NGO host did not register its six explicit development fixture views.");
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
        }

        private static IEnumerator TouchSample(Touchscreen touchscreen, int id, Vector2 position, TouchPhase phase)
        {
            touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = position, phase = phase });
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static AsyncOperation LoadScene()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
        }
    }
}
