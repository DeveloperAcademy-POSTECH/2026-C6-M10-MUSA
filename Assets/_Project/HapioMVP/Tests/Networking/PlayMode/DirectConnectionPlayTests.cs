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

namespace C6.Prototype.Networking.Tests
{
    public sealed class DirectConnectionPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/DirectConnectionSmoke.unity";
        private const string TestPort = "24992";
        private const string UnusedPeerPort = "24993";
        private DirectConnectionView view;
        private DirectConnectionSession session;

        [UnitySetUp]
        public IEnumerator LoadSavedConnectionScene()
        {
            yield return LoadScene();
            yield return null;
            FindView();
            Canvas.ForceUpdateCanvases();
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Idle));
            Assert.That(session.CanStart, Is.True);
        }

        [UnityTearDown]
        public IEnumerator StopAndUnloadConnectionScene()
        {
            foreach (var existing in Object.FindObjectsByType<DirectConnectionSession>())
                existing.Stop();
            yield return WaitFor(
                () => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                5f, "Network shutdown did not finish during cleanup.");

            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.isLoaded)
            {
                var empty = SceneManager.CreateScene("T02 Test Cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(loaded);
            }
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "The T02 scene must remove its owned NetworkManager.");
        }

        [UnityTest]
        public IEnumerator SavedSceneHasOneInputLoopAndReachableConnectionControls()
        {
            AssertSingleView();
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "Opening the screen must not start a network session.");
            Assert.That(view.BuildLabel.text, Does.Contain("C6-T02"));
            Assert.That(view.SafeArea.GetComponent<UISafeArea>(), Is.Not.Null);
            Assert.That(view.Scroll.vertical, Is.True);
            Assert.That(view.Scroll.horizontal, Is.False);
            Assert.That(view.PortInput.text, Is.EqualTo(DirectConnectionSession.DefaultPort.ToString()));
            Assert.That(view.AddressInput.keyboardType,
                Is.EqualTo(TouchScreenKeyboardType.NumbersAndPunctuation));
            Assert.That(view.HostButton.interactable && view.JoinButton.interactable, Is.True);
            Assert.That(view.StopButton.interactable, Is.False);
            Assert.That(view.ParticipantsLabel.text, Does.Contain("0 / 2"));

            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    view.JoinButton.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.SameAs(view.JoinButton.gameObject),
                "The scroll viewport and decorative text must not intercept the Join button.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidAddressShowsFailureAndAllowsManualHostRetry()
        {
            view.AddressInput.text = "not-an-address";
            view.PortInput.text = TestPort;
            Click(view.JoinButton);
            yield return null;

            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Failed));
            Assert.That(view.StateLabel.text, Does.Contain("Failed"));
            Assert.That(view.MessageLabel.text, Does.Contain("IPv4"));
            Assert.That(view.GuidanceLabel.text, Does.Contain("Wi-Fi"));
            Assert.That(view.GuidanceLabel.text, Does.Contain("router"));
            Assert.That(view.GuidanceLabel.text, Does.Contain("permission"));
            Assert.That(session.CanStart && view.HostButton.interactable && view.JoinButton.interactable,
                Is.True);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty,
                "Invalid input must be rejected before opening a transport.");

            Click(view.HostButton);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "The valid manual retry did not start a host.");
            Assert.That(view.StateLabel.text, Does.Contain("Connected"));
            Assert.That(view.RoleLabel.text, Does.Contain("Host"));
            Assert.That(session.ParticipantIds, Is.EquivalentTo(new[] { NetworkManager.ServerClientId }));
        }

        [UnityTest]
        public IEnumerator ConnectingBlocksDuplicateInputAndCancelReturnsToIdle()
        {
            int connectingNotifications = 0;
            Action changed = () =>
            {
                if (session.State == DirectConnectionState.Connecting)
                    connectingNotifications++;
            };
            session.Changed += changed;
            try
            {
                view.AddressInput.text = "127.0.0.1";
                view.PortInput.text = UnusedPeerPort;
                Click(view.JoinButton);
                Assert.That(session.State, Is.EqualTo(DirectConnectionState.Connecting));
                Assert.That(view.HostButton.interactable || view.JoinButton.interactable, Is.False);
                Assert.That(view.AddressInput.interactable || view.PortInput.interactable, Is.False);
                Assert.That(view.StopButton.interactable, Is.True);
                Assert.That(view.StopButtonLabel.text, Is.EqualTo("Cancel"));

                Click(view.JoinButton);
                Click(view.HostButton);
                Assert.That(session.Join("127.0.0.1", UnusedPeerPort), Is.False,
                    "The session must also reject a repeated start independently of the UI.");
                yield return null;
                Assert.That(connectingNotifications, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));

                Click(view.StopButton);
                yield return WaitFor(() => session.State == DirectConnectionState.Idle,
                    5f, "Cancel did not finish shutting down the pending connection.");
                Assert.That(session.CanStart && view.JoinButton.interactable, Is.True);
                Assert.That(session.LocalClientId, Is.Null);
                Assert.That(session.ParticipantIds, Is.Empty);
                Assert.That(view.RoleLabel.text, Does.Contain("None"));
            }
            finally
            {
                session.Changed -= changed;
            }
        }

        [UnityTest]
        public IEnumerator TwoHostCyclesReuseOneManagerAndProduceOneConnectedNotificationEach()
        {
            int connectedNotifications = 0;
            Action changed = () =>
            {
                if (session.State == DirectConnectionState.Connected)
                    connectedNotifications++;
            };
            session.Changed += changed;
            NetworkManager firstManager = null;
            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    // Re-enabling the view must restore its UI subscription cleanly.
                    view.enabled = false;
                    view.enabled = true;
                    view.PortInput.text = TestPort;
                    Click(view.HostButton);
                    yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                        5f, $"Host cycle {cycle + 1} failed to connect.");

                    var managers = Object.FindObjectsByType<NetworkManager>();
                    Assert.That(managers, Has.Length.EqualTo(1));
                    if (firstManager == null) firstManager = managers[0];
                    else Assert.That(managers[0], Is.SameAs(firstManager));
                    Assert.That(connectedNotifications, Is.EqualTo(cycle + 1),
                        "Repeated subscriptions or callbacks must not duplicate a host connection notification.");
                    Assert.That(session.LocalClientId, Is.EqualTo(NetworkManager.ServerClientId));
                    Assert.That(view.ParticipantsLabel.text, Does.Contain("1 / 2"));
                    Assert.That(view.RoleLabel.text, Does.Contain("Host"));
                    Assert.That(view.HostButton.interactable || view.JoinButton.interactable, Is.False);

                    Click(view.StopButton);
                    yield return WaitFor(() => session.State == DirectConnectionState.Idle,
                        5f, $"Host cycle {cycle + 1} did not stop.");
                    Assert.That(firstManager.IsListening || firstManager.ShutdownInProgress, Is.False);
                    Assert.That(session.CanStart, Is.True);
                    Assert.That(view.ParticipantsLabel.text, Does.Contain("0 / 2"));
                    Assert.That(view.RoleLabel.text, Does.Contain("None"));
                    AssertSingleView();
                }
            }
            finally
            {
                session.Changed -= changed;
            }
        }

        [UnityTest]
        public IEnumerator ReloadRemovesOwnedManagerAndAllowsAFreshSession()
        {
            view.PortInput.text = TestPort;
            Click(view.HostButton);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "The host did not start before the reload check.");
            var oldManager = Object.FindAnyObjectByType<NetworkManager>();
            var oldSession = session;

            yield return LoadScene();
            yield return WaitFor(() => oldManager == null && oldSession == null,
                5f, "Reload retained the previous session or its owned NetworkManager.");
            yield return null;
            FindView();
            AssertSingleView();
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Idle));
            Assert.That(session.CanStart, Is.True);
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);

            view.PortInput.text = TestPort;
            Click(view.HostButton);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "The reloaded screen could not start a fresh host.");
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Assert.That(view.ParticipantsLabel.text, Does.Contain("1 / 2"));
        }

        private void FindView()
        {
            view = Object.FindAnyObjectByType<DirectConnectionView>();
            session = Object.FindAnyObjectByType<DirectConnectionSession>();
            Assert.That(view, Is.Not.Null, "The saved scene must contain the T02 view.");
            Assert.That(session, Is.Not.Null, "The saved scene must contain the T02 session.");
            Assert.That(view.Session, Is.SameAs(session));
        }

        private void AssertSingleView()
        {
            Assert.That(Object.FindObjectsByType<DirectConnectionView>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>(), Has.Length.EqualTo(1));
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
