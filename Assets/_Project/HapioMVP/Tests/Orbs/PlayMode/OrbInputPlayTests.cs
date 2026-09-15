using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace C6.Prototype.Orbs.Tests
{
    /// <summary>Saved-scene and queued Input System events. These are not physical-device touch tests.</summary>
    public sealed class OrbInputPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/OrbInputSmoke.unity";
        private const string TestPort = "25005";
        private readonly List<InputDevice> ownedDevices = new List<InputDevice>();
        private T05OrbController controller;
        private OrbPointerInput input;
        private Mouse previousMouse;
        private Touchscreen previousTouchscreen;
        private InputSettings previousInputSettings;
        private InputSettings runtimeInputSettings;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedScene()
        {
            previousMouse = Mouse.current;
            previousTouchscreen = Touchscreen.current;
            previousInputSettings = InputSystem.settings;
            previousRunInBackground = Application.runInBackground;
            // Batch PlayMode has no focused GameView. Input System normally routes Pointer
            // events to its Editor state buffer there, invisible to MonoBehaviour.Update.
            // Use an unsaved, test-owned settings copy so queued events reach the player loop.
            runtimeInputSettings = Object.Instantiate(previousInputSettings);
            runtimeInputSettings.name = "T05 test input routing (not a project asset)";
            runtimeInputSettings.hideFlags = HideFlags.DontSave;
            runtimeInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            runtimeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            runtimeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            InputSystem.settings = runtimeInputSettings;
            yield return LoadScene();
            yield return null;
            FindComponents();
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.IsHostReady, Is.False, "Opening a scene cannot silently start a fixture host.");
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
        }

        [UnityTearDown]
        public IEnumerator ReleaseOnlyTestDevicesAndOwnedScene()
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
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), 5f,
                    "The owned test host did not finish shutdown.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var empty = SceneManager.CreateScene("T05 Test Cleanup");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<T05OrbController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<EventSystem>(), Is.Empty);
            }
            finally
            {
                try
                {
                    if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
                }
                finally
                {
                    Application.runInBackground = previousRunInBackground;
                    if (runtimeInputSettings != null) Object.Destroy(runtimeInputSettings);
                    runtimeInputSettings = null;
                    previousInputSettings = null;
                }
            }
        }

        [UnityTest]
        public IEnumerator ExplicitDevelopmentHostSuppliesThreeConfirmedIdsAndSelectionViews()
        {
            AssertOnePresentationLoop();
            Assert.That(controller.Hud.HostButton.interactable, Is.True);
            Assert.That(controller.Hud.HpLabel.text, Is.EqualTo("-- / --"));
            Assert.That(controller.Hud.TimerLabel.text, Is.EqualTo("--:--"));
            yield return StartHost();
            var manager = controller.Session.OwnedManager;
            Assert.That(manager.IsHost && manager.IsListening, Is.True);
            Assert.That(controller.Registry.DevelopmentTestMode, Is.True);
            Assert.That(controller.Registry.RoundId, Is.EqualTo(1));
            Assert.That(controller.Registry.Snapshot().Count, Is.EqualTo(3));
            Assert.That(controller.Views.Keys, Is.EquivalentTo(controller.Registry.Snapshot().Select(orb => orb.OrbId)));
            Assert.That(controller.Registry.Snapshot().All(orb => orb.OwnerPlayerId == manager.LocalClientId), Is.True);
            Assert.That(controller.Registry.Snapshot().Count(orb => orb.Kind == OrbKind.Raw && orb.Polarity == OrbPolarity.Yin), Is.EqualTo(1));
            Assert.That(controller.Registry.Snapshot().Count(orb => orb.Kind == OrbKind.Raw && orb.Polarity == OrbPolarity.Yang), Is.EqualTo(1));
            Assert.That(controller.Registry.Snapshot().Count(orb => orb.Kind == OrbKind.Combined && orb.Polarity == OrbPolarity.None), Is.EqualTo(1));
            foreach (var pair in controller.Views)
            {
                Assert.That(pair.Value.OrbId, Is.EqualTo(pair.Key));
                Assert.That(pair.Value.Collider, Is.Not.Null);
                Assert.That(pair.Value.Collider.isTrigger, Is.True);
                Assert.That(pair.Value.LocalState, Is.EqualTo(LocalOrbState.Idle));
                Assert.That(pair.Value.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("C6Orbs")));
            }
            Assert.That(controller.RequestCount, Is.Zero);
            Assert.That(controller.AcceptedReservations, Is.Zero);
            Assert.That(Object.FindObjectsByType<Rigidbody>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Rigidbody2D>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator CornerZoneRequestReservesOnceLocksConfirmedOrbAndLeavesOtherOrbsUsable()
        {
            yield return StartHost();
            var combined = Find(OrbKind.Combined, OrbPolarity.None);
            var start = controller.GetViewScreenPosition(combined.OrbId);
            Rect lower = controller.Layout.BottomPixelRect;
            var corner = new Vector2(lower.xMin + 1f, lower.yMax - 1f);
            Assert.That(controller.BeginPointer(101, start, false), Is.True);
            controller.MovePointer(101, corner);
            Assert.That(controller.AcceptedReservations, Is.EqualTo(1));
            Assert.That(controller.LatestDecision.HasValue, Is.True);
            Assert.That(controller.LatestDecision.Value.Kind, Is.EqualTo(OrbActionKind.Launch),
                "A sample meeting both zone and horizontal conditions must reserve launch first.");
            Assert.That(controller.Registry.IsPending(combined.OrbId), Is.True);
            Assert.That(controller.Views[combined.OrbId].LocalState, Is.EqualTo(LocalOrbState.Pending));
            controller.MovePointer(101, new Vector2(lower.xMax + Screen.width, lower.center.y));
            controller.EndPointer(101, corner);
            Assert.That(controller.RequestCount, Is.EqualTo(1));
            Assert.That(controller.BeginPointer(102, controller.GetViewScreenPosition(combined.OrbId), false), Is.False);
            controller.CancelPointer(102);
            Assert.That(controller.Registry.TryGet(combined.OrbId, out var confirmed), Is.True);
            Assert.That(confirmed, Is.SameAs(combined));
            Assert.That(controller.Registry.Snapshot().Count, Is.EqualTo(3));

            var raw = Find(OrbKind.Raw, OrbPolarity.Yin);
            Assert.That(controller.BeginPointer(103, controller.GetViewScreenPosition(raw.OrbId), false), Is.True);
            controller.MovePointer(103, new Vector2(controller.GetViewScreenPosition(raw.OrbId).x, lower.yMax - 1f));
            Assert.That(controller.RequestCount, Is.EqualTo(1), "A Raw entering the zone cannot launch or disappear.");
            controller.CancelPointer(103);
            Assert.That(controller.Registry.IsPending(raw.OrbId), Is.False);
            Assert.That(controller.Views[raw.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            Assert.That(controller.Hud.HpLabel.text, Is.EqualTo("-- / --"));
            Assert.That(controller.Hud.TimerLabel.text, Is.EqualTo("--:--"));
            Assert.That(Object.FindObjectsByType<Rigidbody>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<Rigidbody2D>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator QueuedMouseEventsSelectMoveAndReleaseThroughTheRuntimeAdapter()
        {
            yield return StartHost();
            var mouse = AddDevice<Mouse>();
            var orb = Find(OrbKind.Raw, OrbPolarity.Yin);
            Vector2 start = controller.GetViewScreenPosition(orb.OrbId);
            int before = input.MouseSampleCount;
            yield return MouseSample(mouse, start, true);
            Assert.That(input.MouseSampleCount, Is.GreaterThan(before), "The runtime Mouse adapter must consume the queued press.");
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Dragging));
            Vector2 moved = start + Vector2.left * Screen.width * .04f;
            yield return MouseSample(mouse, moved, true);
            Assert.That(Vector2.Distance(start, controller.GetViewScreenPosition(orb.OrbId)), Is.GreaterThan(1f));
            yield return MouseSample(mouse, moved, false);
            Assert.That(input.MouseSampleCount, Is.GreaterThan(before));
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            Assert.That(controller.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator QueuedTouchIdEventsSelectMoveAndCancelThroughEnhancedTouch()
        {
            yield return StartHost();
            Assert.That(EnhancedTouchSupport.enabled, Is.True);
            var touchscreen = AddDevice<Touchscreen>();
            var orb = Find(OrbKind.Raw, OrbPolarity.Yang);
            Vector2 start = controller.GetViewScreenPosition(orb.OrbId);
            int samplesBefore = input.TouchSampleCount;
            int cancelsBefore = input.TouchCancelCount;
            const int touchId = 505;
            yield return TouchSample(touchscreen, touchId, start, TouchPhase.Began);
            Assert.That(input.LastTouchId, Is.EqualTo(touchId));
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Dragging));
            Vector2 moved = start + Vector2.right * Screen.width * .04f;
            yield return TouchSample(touchscreen, touchId, moved, TouchPhase.Moved);
            Assert.That(Vector2.Distance(start, controller.GetViewScreenPosition(orb.OrbId)), Is.GreaterThan(1f));
            yield return TouchSample(touchscreen, touchId, moved, TouchPhase.Canceled);
            Assert.That(input.TouchSampleCount, Is.GreaterThanOrEqualTo(samplesBefore + 3));
            Assert.That(input.TouchCancelCount, Is.EqualTo(cancelsBefore + 1));
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            Assert.That(controller.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MouseStartingOverARealUiButtonCannotBecomeAnOrbGestureWhileHeld()
        {
            yield return StartHost();
            var mouse = AddDevice<Mouse>();
            var orb = Find(OrbKind.Raw, OrbPolarity.Yin);
            Vector2 orbPosition = controller.GetViewScreenPosition(orb.OrbId);
            Canvas.ForceUpdateCanvases();
            var uiPosition = RectTransformUtility.WorldToScreenPoint(null, controller.Hud.EndButton.transform.position);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = uiPosition }, hits);
            Assert.That(hits.Any(hit => hit.gameObject == controller.Hud.EndButton.gameObject
                || hit.gameObject.transform.IsChildOf(controller.Hud.EndButton.transform)), Is.True);
            yield return MouseSample(mouse, uiPosition, true);
            yield return MouseSample(mouse, orbPosition, true);
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            yield return MouseSample(mouse, orbPosition, false);
            Assert.That(controller.IsHostReady, Is.True, "Dragging away from End cannot activate that button.");
            Assert.That(controller.RequestCount, Is.Zero);
            yield return MouseSample(mouse, orbPosition, true);
            Assert.That(controller.Views[orb.OrbId].LocalState, Is.EqualTo(LocalOrbState.Dragging));
            yield return MouseSample(mouse, orbPosition, false);
        }

        [UnityTest]
        public IEnumerator ExtraPointerCannotStealAndInputDisableCancelsWithoutResumingHeldGesture()
        {
            yield return StartHost();
            var yin = Find(OrbKind.Raw, OrbPolarity.Yin);
            var yang = Find(OrbKind.Raw, OrbPolarity.Yang);
            Vector2 first = controller.GetViewScreenPosition(yin.OrbId);
            Vector2 second = controller.GetViewScreenPosition(yang.OrbId);
            Assert.That(controller.BeginPointer(201, first, false), Is.True);
            Assert.That(controller.BeginPointer(202, first, false), Is.False);
            Assert.That(controller.BeginPointer(203, second, false), Is.False);
            controller.MovePointer(202, new Vector2(Screen.width + 10f, first.y));
            Assert.That(controller.RequestCount, Is.Zero);
            Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(201));
            controller.SetInteractionEnabled(false);
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[yin.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            controller.SetInteractionEnabled(true);
            controller.MovePointer(201, new Vector2(-10f, first.y));
            Assert.That(controller.RequestCount, Is.Zero);
            Assert.That(controller.BeginPointer(201, first, false), Is.False,
                "The same finger remains excluded until Up/Cancel after a disable.");
            controller.CancelPointer(201);
            controller.CancelPointer(202);
            controller.CancelPointer(203);
            Assert.That(controller.BeginPointer(204, second, false), Is.True);
            controller.CancelInteractions("TEST_FOCUS_LOSS");
            Assert.That(controller.Gestures.HasActivePointer, Is.False);
            Assert.That(controller.Views[yang.OrbId].LocalState, Is.EqualTo(LocalOrbState.Idle));
            Assert.That(controller.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ContactOnlyWaitsForDropAndAnEarlierHorizontalRequestWinsOverLaterDrop()
        {
            yield return StartHost();
            var yin = Find(OrbKind.Raw, OrbPolarity.Yin);
            var yang = Find(OrbKind.Raw, OrbPolarity.Yang);
            Vector2 source = controller.GetViewScreenPosition(yin.OrbId);
            Vector2 target = controller.GetViewScreenPosition(yang.OrbId);
            Assert.That(Mathf.Abs(target.x - source.x), Is.LessThan(Screen.width * .18f),
                "The development pair must permit a drop without crossing the default swipe threshold.");
            Assert.That(controller.BeginPointer(301, source, false), Is.True);
            controller.MovePointer(301, target);
            Assert.That(controller.RequestCount, Is.Zero, "Mere contact cannot combine materials.");
            controller.EndPointer(301, target);
            Assert.That(controller.LatestDecision.Value.Kind, Is.EqualTo(OrbActionKind.Combine));
            Assert.That(controller.AcceptedReservations, Is.EqualTo(1));
            Assert.That(controller.Registry.IsPending(yin.OrbId) && controller.Registry.IsPending(yang.OrbId), Is.True);
            Assert.That(controller.Registry.Snapshot().Count, Is.EqualTo(3));

            controller.ResetDevelopmentFixture();
            yield return null;
            yin = Find(OrbKind.Raw, OrbPolarity.Yin);
            yang = Find(OrbKind.Raw, OrbPolarity.Yang);
            source = controller.GetViewScreenPosition(yin.OrbId);
            target = controller.GetViewScreenPosition(yang.OrbId);
            Assert.That(controller.BeginPointer(302, source, false), Is.True);
            controller.MovePointer(302, source + Vector2.right * Screen.width * .22f);
            var requestsAfterSwipe = controller.RequestCount;
            controller.EndPointer(302, target);
            Assert.That(controller.RequestCount, Is.EqualTo(requestsAfterSwipe));
            Assert.That(controller.LatestDecision.Value.Kind, Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(controller.Registry.IsPending(yin.OrbId), Is.True);
            Assert.That(controller.Registry.IsPending(yang.OrbId), Is.False);
            Assert.That(controller.Registry.TryGet(yin.OrbId, out var confirmed), Is.True);
            Assert.That(confirmed.OwnerPlayerId, Is.EqualTo(controller.Session.OwnedManager.LocalClientId));
        }

        [UnityTest]
        public IEnumerator ExplicitResetAndEndThenSceneReloadClearOwnedStateWithoutDuplicateLoops()
        {
            yield return StartHost();
            var oldIds = controller.Views.Keys.ToArray();
            var oldViews = controller.Views.Values.ToArray();
            uint oldRound = controller.Registry.RoundId;
            var combined = Find(OrbKind.Combined, OrbPolarity.None);
            Vector2 start = controller.GetViewScreenPosition(combined.OrbId);
            Assert.That(controller.BeginPointer(401, start, false), Is.True);
            controller.MovePointer(401, new Vector2(start.x, controller.Layout.BottomPixelRect.yMax - 1f));
            Assert.That(controller.Registry.IsPending(combined.OrbId), Is.True);
            controller.Hud.ResetButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(controller.Registry.RoundId, Is.GreaterThan(oldRound));
            Assert.That(controller.Views.Count, Is.EqualTo(3));
            Assert.That(controller.Views.Keys.Intersect(oldIds), Is.Empty);
            Assert.That(oldViews.All(view => view == null), Is.True);
            Assert.That(controller.Registry.Snapshot().All(orb => !controller.Registry.IsPending(orb.OrbId)), Is.True);
            Assert.That(controller.Gestures.HasActivePointer || controller.Gestures.HasPending, Is.False);

            var oldController = controller;
            var oldCanvas = controller.Hud.Canvas;
            var oldEventSystem = EventSystem.current;
            var oldManager = controller.Session.OwnedManager;
            controller.Hud.EndButton.onClick.Invoke();
            yield return WaitFor(() => !controller.IsHostReady && (oldManager == null
                || (!oldManager.IsListening && !oldManager.ShutdownInProgress)), 5f, "End did not stop the real host.");
            Assert.That(controller.Registry == null || !controller.Registry.HasSession, Is.True);
            Assert.That(controller.Views, Is.Empty);
            yield return LoadScene();
            yield return null;
            yield return null;
            FindComponents();
            Assert.That(oldController == null && oldCanvas == null && oldEventSystem == null && oldManager == null, Is.True);
            AssertOnePresentationLoop();
            Assert.That(controller.IsHostReady, Is.False);
            Assert.That(controller.Views, Is.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
        }

        private IEnumerator StartHost()
        {
            controller.StartDevelopmentHost(TestPort);
            yield return WaitFor(() => controller.IsHostReady && controller.Views.Count == 3, 5f,
                "The explicit development host did not register its three fixture views.");
            yield return null;
            Physics2D.SyncTransforms();
        }

        private OrbRecord Find(OrbKind kind, OrbPolarity polarity)
        {
            return controller.Registry.Snapshot().Single(orb => orb.Kind == kind && orb.Polarity == polarity);
        }

        private T AddDevice<T>() where T : InputDevice, new()
        {
            var device = InputSystem.AddDevice<T>();
            ownedDevices.Add(device);
            return device;
        }

        private IEnumerator MouseSample(Mouse mouse, Vector2 position, bool down)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, down));
            // Let the normal Input System update run before the adapter's MonoBehaviour.Update.
            yield return null;
            yield return null;
            Debug.Log($"C6_T05_QUEUED_MOUSE device={mouse.deviceId} current={Mouse.current?.deviceId} " +
                $"requestedDown={down} observedDown={mouse.leftButton.isPressed} requestedPosition={position} " +
                $"observedPosition={mouse.position.ReadValue()} adapterSamples={input.MouseSampleCount}");
            Assert.That(Mouse.current, Is.SameAs(mouse), "Another Mouse became current during the queued sample.");
            Assert.That(mouse.leftButton.isPressed, Is.EqualTo(down),
                "The queued state did not reach the player buffer; check test focus routing.");
            Assert.That(Vector2.Distance(mouse.position.ReadValue(), position), Is.LessThan(.5f),
                "The queued Mouse position must reach the runtime before checking gesture behavior.");
        }

        private static IEnumerator TouchSample(Touchscreen touchscreen, int id, Vector2 position, TouchPhase phase)
        {
            touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = position, phase = phase });
            yield return null;
            yield return null;
        }

        private void FindComponents()
        {
            controller = Object.FindAnyObjectByType<T05OrbController>();
            input = Object.FindAnyObjectByType<OrbPointerInput>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(controller.Layout, Is.Not.Null);
            Assert.That(controller.Session, Is.Not.Null);
            Assert.That(controller.Hud, Is.Not.Null);
            Assert.That(controller.Hud.Layout, Is.SameAs(controller.Layout));
        }

        private static void AssertOnePresentationLoop()
        {
            Assert.That(Object.FindObjectsByType<T05OrbController>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<OrbPointerInput>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(), Has.Length.EqualTo(2));
            Assert.That(Object.FindObjectsByType<AudioListener>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(), Has.Length.EqualTo(1));
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
