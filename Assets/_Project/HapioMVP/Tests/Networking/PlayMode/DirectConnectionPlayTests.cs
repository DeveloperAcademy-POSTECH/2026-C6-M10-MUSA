using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Networking.Tests
{
    /// <summary>
    /// Transport lifecycle checks in the current saved game scene. The room UI and its
    /// approval/initial-state path are covered by SavedRoomLobbyPlayTests instead.
    /// One Editor process owns one DirectConnectionSession, so an unanswered loopback
    /// join is not evidence of a second connected device.
    /// </summary>
    public sealed class DirectConnectionPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string HostPort = "24992";
        private const string UnusedLoopbackPort = "24993";
        private DirectConnectionSession session;
        private bool previousRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadCurrentGameSceneWithTransportIsolated()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return LoadScene();
            yield return null;
            yield return null;
            IsolateTransport();
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Idle));
            Assert.That(session.CanStart, Is.True);
            Assert.That(Managers(), Is.Empty, "Opening the current scene must not start NGO implicitly.");
        }

        [UnityTearDown]
        public IEnumerator StopAndUnloadCurrentGameScene()
        {
            try
            {
                if (session != null) session.Stop();
                yield return WaitFor(() => Managers().All(manager => !manager.IsListening && !manager.ShutdownInProgress),
                    5f, "Network shutdown did not finish during cleanup.");

                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    var empty = SceneManager.CreateScene("Current Connection Test Cleanup");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null;
                yield return null;
                Assert.That(Object.FindObjectsByType<DirectConnectionSession>(), Is.Empty);
                Assert.That(Managers(), Is.Empty, "The saved scene must remove its owned NetworkManager.");
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator InvalidDirectAddressDoesNotAllocateTransportAndHostCanStartAfterward()
        {
            Assert.That(session.Join("not-an-address", HostPort), Is.False);
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Failed));
            Assert.That(session.FailureStage, Is.EqualTo("INPUT"));
            Assert.That(session.FailureCode, Is.EqualTo("INVALID_ADDRESS"));
            Assert.That(Managers(), Is.Empty, "Invalid input must fail before creating NGO transport.");
            Assert.That(session.CanStart, Is.True);

            Assert.That(session.StartHost(HostPort), Is.True);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "A valid host retry did not connect.");
            Assert.That(session.Role, Is.EqualTo("Host"));
            Assert.That(session.LocalClientId, Is.EqualTo(NetworkManager.ServerClientId));
            Assert.That(session.ParticipantIds, Is.EquivalentTo(new[] { NetworkManager.ServerClientId }));
            Assert.That(Managers(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnansweredLoopbackJoinRejectsDuplicateStartAndCanBeCancelled()
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
                Assert.That(session.Join("127.0.0.1", UnusedLoopbackPort), Is.True);
                Assert.That(session.State, Is.EqualTo(DirectConnectionState.Connecting));
                Assert.That(session.JoinInProgress, Is.True);
                Assert.That(session.CanStart, Is.False);
                Assert.That(session.Join("127.0.0.1", UnusedLoopbackPort), Is.False);
                Assert.That(session.StartHost(HostPort), Is.False);
                Assert.That(connectingNotifications, Is.EqualTo(1));
                Assert.That(Managers(), Has.Length.EqualTo(1));

                session.Stop();
                yield return WaitFor(() => session.State == DirectConnectionState.Idle,
                    5f, "Cancelling an unanswered loopback join did not return to Idle.");
                Assert.That(session.CanStart, Is.True);
                Assert.That(session.JoinInProgress, Is.False);
                Assert.That(session.LocalClientId, Is.Null);
                Assert.That(session.ParticipantIds, Is.Empty);
                Assert.That(Managers().All(manager => !manager.IsListening && !manager.ShutdownInProgress), Is.True);
            }
            finally { session.Changed -= changed; }
        }

        [UnityTest]
        public IEnumerator TwoHostCyclesReuseOneManagerAndPublishOneConnectionEach()
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
                    Assert.That(session.StartHost(HostPort), Is.True);
                    yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                        5f, $"Host cycle {cycle + 1} failed to connect.");
                    var managers = Managers();
                    Assert.That(managers, Has.Length.EqualTo(1));
                    if (firstManager == null) firstManager = managers[0];
                    else Assert.That(managers[0], Is.SameAs(firstManager));
                    Assert.That(connectedNotifications, Is.EqualTo(cycle + 1));
                    Assert.That(session.LocalClientId, Is.EqualTo(NetworkManager.ServerClientId));
                    Assert.That(session.ParticipantIds, Has.Count.EqualTo(1));

                    session.Stop();
                    yield return WaitFor(() => session.State == DirectConnectionState.Idle,
                        5f, $"Host cycle {cycle + 1} did not stop.");
                    Assert.That(firstManager.IsListening || firstManager.ShutdownInProgress, Is.False);
                    Assert.That(session.CanStart, Is.True);
                    Assert.That(session.ParticipantIds, Is.Empty);
                }
            }
            finally { session.Changed -= changed; }
        }

        [UnityTest]
        public IEnumerator ReloadDestroysOwnedManagerAndAllowsAFreshHost()
        {
            Assert.That(session.StartHost(HostPort), Is.True);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "The first host did not connect before reload.");
            var oldManager = session.OwnedManager;
            var oldSession = session;

            yield return LoadScene();
            yield return WaitFor(() => oldManager == null && oldSession == null,
                5f, "Reload retained the previous session or its owned NetworkManager.");
            yield return null;
            IsolateTransport();
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Idle));
            Assert.That(session.CanStart, Is.True);
            Assert.That(Managers(), Is.Empty);

            Assert.That(session.StartHost(HostPort), Is.True);
            yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                5f, "The reloaded current scene could not start a fresh host.");
            Assert.That(Managers(), Has.Length.EqualTo(1));
            Assert.That(session.ParticipantIds, Is.EquivalentTo(new[] { NetworkManager.ServerClientId }));
        }

        [UnityTest]
        public IEnumerator CandidateRetryKeepsPayloadAndIgnoresRetiredManagerCallbacks()
        {
            // The deliberately unused first endpoint emits this stock UTP diagnostic.
            LogAssert.Expect(LogType.Error, "Failed to connect to server.");
            var payload = new byte[] { 7, 12, 25, 41 };
            Assert.That(session.ConfigureConnection(DirectConnectionSession.ProtocolVersion, payload), Is.True);
            Assert.That(session.JoinCandidates(new[] { "::1", "127.0.0.1" }, UnusedLoopbackPort), Is.True);
            var retired = session.OwnedManager;
            var firstAttempt = session.AttemptId;
            payload[0] = 99;
            Assert.That(session.ConfigureConnection(DirectConnectionSession.ProtocolVersion, payload), Is.False);
            yield return WaitFor(() => session.CandidateAttempt == 2 && session.State == DirectConnectionState.Connecting,
                22f, "First address did not finish shutdown and start its next candidate.");
            Assert.That(session.AttemptId, Is.EqualTo(firstAttempt + 1));
            Assert.That(session.OwnedManager, Is.Not.SameAs(retired));
            Assert.That(session.OwnedManager.NetworkConfig.ConnectionData, Is.EqualTo(new byte[] { 7, 12, 25, 41 }));
            var method = typeof(DirectConnectionSession).GetMethod("OnConnectionEvent",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(session, new object[]
            {
                retired,
                new ConnectionEventData { EventType = ConnectionEvent.ClientConnected, ClientId = 999 }
            });
            Assert.That(session.State, Is.EqualTo(DirectConnectionState.Connecting));
            Assert.That(session.LocalClientId, Is.Null);
            Assert.That(session.ParticipantIds, Is.Empty);
            session.Stop();
            yield return WaitFor(() => session.State == DirectConnectionState.Idle,
                5f, "Retry cancellation did not finish.");
            Assert.That(session.OwnedManager, Is.Null);
        }

        [UnityTest]
        public IEnumerator CancelDuringCandidateShutdownPreventsNextAttemptAndAllowsHostRestart()
        {
            LogAssert.Expect(LogType.Error, "Failed to connect to server.");
            bool cancelled = false;
            Action onChanged = () =>
            {
                if (!cancelled && session.State == DirectConnectionState.Stopping && session.JoinInProgress)
                {
                    cancelled = true;
                    session.Stop();
                }
            };
            session.Changed += onChanged;
            try
            {
                Assert.That(session.JoinCandidates(new[] { "::1", "127.0.0.1" }, UnusedLoopbackPort), Is.True);
                var firstAttempt = session.AttemptId;
                yield return WaitFor(() => cancelled && session.State == DirectConnectionState.Idle,
                    24f, "Cancellation at the shutdown barrier did not return to Idle.");
                yield return null;
                Assert.That(session.AttemptId, Is.EqualTo(firstAttempt));
                Assert.That(session.JoinInProgress, Is.False);
                Assert.That(session.CanStart, Is.True);
                Assert.That(session.OwnedManager, Is.Null);
                Assert.That(session.StartHost(HostPort), Is.True);
                yield return WaitFor(() => session.State == DirectConnectionState.Connected,
                    5f, "Host restart after cancellation failed.");
                Assert.That(session.ParticipantIds, Has.Count.EqualTo(1));
            }
            finally { session.Changed -= onChanged; }
        }

        private void IsolateTransport()
        {
            session = Object.FindAnyObjectByType<DirectConnectionSession>();
            Assert.That(session, Is.Not.Null, "The current saved game scene needs one DirectConnectionSession.");
            Assert.That(Object.FindObjectsByType<DirectConnectionSession>(), Has.Length.EqualTo(1));
            // The same root also owns the live lobby. Stop only its Update loop while
            // exercising the transport directly; its full UI path has separate tests.
            var lobby = session.GetComponents<MonoBehaviour>()
                .SingleOrDefault(component => component != null &&
                    component.GetType().FullName == "C6.Prototype.Lobby.T10LobbySession");
            Assert.That(lobby, Is.Not.Null, "The tested transport must belong to the current lobby scene.");
            lobby.enabled = false;
        }

        private static NetworkManager[] Managers() => Object.FindObjectsByType<NetworkManager>();

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
