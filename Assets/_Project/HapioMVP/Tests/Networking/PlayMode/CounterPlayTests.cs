using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace C6.Prototype.Networking.Counter.Tests
{
    /// <summary>Saved-scene UI and local Host authority integration; not a device/LAN test.</summary>
    public sealed class CounterPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/CounterSmoke.unity";
        private const string TestPort = "24994";
        private DirectConnectionView connectionView;
        private DirectConnectionSession session;
        private CounterView view;
        private CounterSync sync;

        [UnitySetUp]
        public IEnumerator LoadSavedCounterScene()
        {
            yield return LoadScene();
            yield return null;
            connectionView = Object.FindAnyObjectByType<DirectConnectionView>();
            session = Object.FindAnyObjectByType<DirectConnectionSession>();
            view = Object.FindAnyObjectByType<CounterView>();
            sync = Object.FindAnyObjectByType<CounterSync>();
            Assert.That(connectionView, Is.Not.Null, "The saved T03 scene must retain its connection view.");
            Assert.That(session, Is.Not.Null);
            Assert.That(view, Is.Not.Null, "The saved T03 scene must contain CounterView.");
            Assert.That(sync, Is.Not.Null, "The saved T03 scene must contain CounterSync.");
            Assert.That(view.Sync, Is.SameAs(sync));
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Idle));
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator StopAndUnloadCounterScene()
        {
            foreach (var existing in Object.FindObjectsByType<DirectConnectionSession>())
                existing.Stop();
            yield return WaitFor(
                () => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                5f, "Network shutdown did not finish during T03 cleanup.");

            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.isLoaded)
            {
                var empty = SceneManager.CreateScene("T03 Test Cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "The T03 scene must remove its owned NetworkManager.");
        }

        [UnityTest]
        public IEnumerator SavedSceneReusesOneInputLoopAndDoesNotStartRequests()
        {
            AssertSingleInputLoop();
            Assert.That(connectionView.BuildLabel.text, Does.Contain("C6-T03"));
            Assert.That(connectionView.SafeArea.GetComponent<UISafeArea>(), Is.Not.Null);
            Assert.That(connectionView.Scroll.vertical, Is.True);
            Assert.That(connectionView.Scroll.horizontal, Is.False);
            Assert.That(view.NumberLabel.transform.IsChildOf(connectionView.Scroll.content), Is.True);
            Assert.That(view.RequestButton.transform.IsChildOf(connectionView.Scroll.content), Is.True);
            Assert.That(view.NumberLabel.text, Is.EqualTo("0"));
            Assert.That(view.SessionLabel.text, Does.Contain("waiting"));
            Assert.That(view.RequestButton.interactable || view.BatchButton.interactable ||
                        view.ReplayButton.interactable, Is.False);
            Assert.That(view.IsBatchRunning, Is.False);
            Assert.That(sync.IsReady, Is.False);
            Assert.That(sync.SentCount, Is.Zero);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "Loading T03 must not automatically start Host, Client, or a batch.");

            connectionView.Scroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    view.RequestButton.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(view.RequestButton.gameObject),
                "The shared-counter button must be visible through the existing scroll viewport.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostButtonRequestIsApprovedOnceAndReplayDoesNotIncrementAgain()
        {
            yield return StartHost();
            Assert.That(sync.Snapshot.Value, Is.Zero);
            Assert.That(sync.SentCount, Is.Zero, "Starting Host alone must not send any counter request.");
            Assert.That(view.RequestButton.interactable && view.BatchButton.interactable, Is.True);
            Assert.That(view.ReplayButton.interactable, Is.False);

            Click(view.RequestButton);
            yield return WaitFor(() => sync.AcknowledgedCount == 1 && sync.PendingCount == 0,
                3f, "The Host request did not receive its authority receipt.");
            Assert.That(sync.Snapshot.Value, Is.EqualTo(1L));
            Assert.That(sync.Snapshot.ApprovedCount, Is.EqualTo(1UL));
            Assert.That(sync.Snapshot.RejectedCount, Is.Zero);
            Assert.That(sync.Snapshot.Revision, Is.EqualTo(1UL));
            Assert.That(sync.SentCount, Is.EqualTo(1UL));
            Assert.That(view.NumberLabel.text, Is.EqualTo("1"));
            Assert.That(view.StatsLabel.text, Does.Contain("Host approved: 1"));
            Assert.That(view.StatsLabel.text, Does.Contain("Acknowledged: 1"));
            Assert.That(view.ReplayButton.interactable, Is.True);

            Click(view.ReplayButton);
            yield return WaitFor(() => sync.DuplicateReceiptCount == 1,
                3f, "The explicit replay did not return a duplicate receipt.");
            Assert.That(sync.Snapshot.Value, Is.EqualTo(1L));
            Assert.That(sync.Snapshot.ApprovedCount, Is.EqualTo(1UL));
            Assert.That(sync.Snapshot.Revision, Is.EqualTo(1UL));
            Assert.That(sync.SentCount, Is.EqualTo(1UL));
            Assert.That(sync.AcknowledgedCount, Is.EqualTo(1UL));
            Assert.That(sync.PendingCount, Is.Zero);
            Assert.That(view.NumberLabel.text, Is.EqualTo("1"));
        }

        [UnityTest]
        public IEnumerator ExplicitDevelopmentBatchCompletesExactlyFiftyApprovedRequests()
        {
            yield return StartHost();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(sync.SentCount, Is.Zero, "Host readiness must not trigger a development batch.");

            Click(view.BatchButton);
            Assert.That(view.IsBatchRunning, Is.True);
            Assert.That(view.RequestButton.interactable || view.BatchButton.interactable ||
                        view.ReplayButton.interactable, Is.False);
            // A repeated UI click must not create a second running coroutine.
            Click(view.BatchButton);
            yield return WaitFor(() => !view.IsBatchRunning && sync.PendingCount == 0,
                8f, "The explicit development batch did not finish with all requests acknowledged.");

            Assert.That(sync.SentCount, Is.EqualTo(50UL));
            Assert.That(sync.AcknowledgedCount, Is.EqualTo(50UL));
            Assert.That(sync.PendingCount, Is.Zero);
            Assert.That(sync.Snapshot.Value, Is.EqualTo(50L));
            Assert.That(sync.Snapshot.ApprovedCount, Is.EqualTo(50UL));
            Assert.That(sync.Snapshot.RejectedCount, Is.Zero);
            Assert.That(sync.Snapshot.Revision, Is.EqualTo(50UL));
            Assert.That(sync.DuplicateReceiptCount, Is.Zero);
            Assert.That(view.NumberLabel.text, Is.EqualTo("50"));
            Assert.That(view.StatsLabel.text, Does.Contain("Pending: 0"));
            Assert.That(view.RequestButton.interactable && view.BatchButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingViewStopsBatchAndReenableRetainsOneWorkingInputLoop()
        {
            yield return StartHost();
            Click(view.BatchButton);
            yield return WaitFor(() => sync.SentCount >= 2, 3f,
                "The batch did not begin before the disable check.");
            Assert.That(sync.SentCount, Is.LessThan(50UL));

            view.enabled = false;
            ulong countAtDisable = sync.SentCount;
            Assert.That(view.IsBatchRunning, Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(sync.IsReady, Is.True, "Disabling only the view must not end the network session.");
            Assert.That(sync.SentCount, Is.EqualTo(countAtDisable),
                "The disabled view continued sending automated requests.");
            Assert.That(sync.Snapshot.Value, Is.EqualTo((long)countAtDisable));

            view.enabled = true;
            yield return null;
            AssertSingleInputLoop();
            Assert.That(view.IsBatchRunning, Is.False, "Re-enabling must not automatically resume a stopped batch.");
            Assert.That(view.RequestButton.interactable, Is.True);
            Click(view.RequestButton);
            yield return WaitFor(() => sync.AcknowledgedCount == countAtDisable + 1,
                3f, "The re-enabled view did not send one manual request.");
            Assert.That(sync.SentCount, Is.EqualTo(countAtDisable + 1));
            Assert.That(sync.Snapshot.Value, Is.EqualTo((long)countAtDisable + 1));
            Assert.That(sync.PendingCount, Is.Zero);
            Assert.That(view.NumberLabel.text, Is.EqualTo(((long)countAtDisable + 1).ToString()));
        }

        [UnityTest]
        public IEnumerator EndingHostCancelsBatchAndNextSessionStartsFromZeroWithNewIdentity()
        {
            yield return StartHost();
            string oldSessionId = sync.Snapshot.SessionId;
            Click(view.BatchButton);
            yield return WaitFor(() => sync.SentCount >= 2, 3f,
                "The batch did not begin before the session-end check.");
            Assert.That(view.IsBatchRunning, Is.True);

            Click(connectionView.StopButton);
            yield return WaitFor(() => session.State == DirectConnectionState.Idle && !sync.IsReady,
                5f, "Ending Host did not shut down the counter session.");
            Assert.That(view.IsBatchRunning, Is.False);
            Assert.That(sync.SentCount, Is.Zero);
            Assert.That(sync.AcknowledgedCount, Is.Zero);
            Assert.That(sync.PendingCount, Is.Zero);
            Assert.That(sync.Snapshot.Value, Is.Zero);
            Assert.That(view.NumberLabel.text, Is.EqualTo("0"));
            Assert.That(view.RequestButton.interactable || view.BatchButton.interactable, Is.False);

            yield return StartHost();
            Assert.That(sync.Snapshot.SessionId, Is.Not.EqualTo(oldSessionId));
            Assert.That(sync.Snapshot.SessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(sync.Snapshot.Value, Is.Zero);
            Assert.That(sync.Snapshot.ApprovedCount, Is.Zero);
            Assert.That(sync.Snapshot.RejectedCount, Is.Zero);
            Assert.That(sync.Snapshot.Revision, Is.Zero);
            Assert.That(sync.SentCount, Is.Zero);
            Assert.That(sync.DuplicateReceiptCount, Is.Zero);
            Assert.That(view.IsBatchRunning, Is.False);
            Assert.That(view.ReplayButton.interactable, Is.False);
            AssertSingleInputLoop();

            Click(view.RequestButton);
            yield return WaitFor(() => sync.AcknowledgedCount == 1 && sync.PendingCount == 0,
                3f, "The new session could not approve its first request.");
            Assert.That(sync.Snapshot.Value, Is.EqualTo(1L));
            Assert.That(sync.SentCount, Is.EqualTo(1UL));
        }

        private IEnumerator StartHost()
        {
            connectionView.PortInput.text = TestPort;
            Click(connectionView.HostButton);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected && sync.IsReady,
                5f, "The saved T03 scene did not start a Host with ready counter authority.");
            Assert.That(sync.Snapshot.SessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Canvas.ForceUpdateCanvases();
        }

        private void AssertSingleInputLoop()
        {
            Assert.That(Object.FindObjectsByType<DirectConnectionView>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CounterView>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CounterSync>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<InputSystemUIInputModule>(), Has.Length.EqualTo(1));
            Assert.That(EventSystem.current.currentInputModule, Is.TypeOf<InputSystemUIInputModule>());
        }

        private static void Click(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
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

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string failure)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, failure);
        }
    }
}
