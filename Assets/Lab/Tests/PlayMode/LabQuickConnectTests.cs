using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Uses the saved Canvas and a local UDP sink rather than depending on whether another
    /// room happens to be advertised on the test machine's current network.
    /// </summary>
    public sealed class LabQuickConnectTests
    {
        [UnityTest]
        public IEnumerator SavedBrowserOpensAutomaticallyAndKeepsHostSizeDirectAndDeveloperControlsUsable()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            LabHud hud = SavedHud();
            LabNetwork network = hud.network;
            LabDeveloperMode developer = hud.GetComponent<LabDeveloperMode>();
            try
            {
                Assert.That(network, Is.Not.Null);
                Assert.That(network.CanChooseConnection, Is.True,
                    "Browsing must not lock room creation or developer tuning.");
                Assert.That(network.CanJoinDiscoveredRoom("not-an-advertised-room"), Is.False);
                Assert.That(hud.roomListPanel.activeSelf, Is.True);
                Assert.That(hud.roomSearchStatusText.text, Is.Not.Empty,
                    "Both active browsing and a native discovery error need a visible message.");
                Assert.That(hud.hostButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(hud.hostButton.interactable, Is.True);
                Assert.That(hud.decreasePlayersButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(hud.increasePlayersButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(hud.quickPlayerCountText, Is.Not.Null);
                Assert.That(hud.quickPlayerCountText.text, Does.Contain("3"));
                Assert.That(hud.directConnectionPanel, Is.Not.Null);
                Assert.That(hud.directConnectionButton, Is.Not.Null);
                Assert.That(hud.directConnectionPanel.activeSelf, Is.False,
                    "Direct IP is available without occupying the initial browser.");

                hud.directConnectionButton.onClick.Invoke();
                yield return null;
                Assert.That(hud.directConnectionPanel.activeSelf, Is.True);
                Assert.That(hud.hostAddress.gameObject.activeInHierarchy, Is.True);
                Assert.That(hud.port.gameObject.activeInHierarchy, Is.True);
                Assert.That(hud.joinButton.gameObject.activeInHierarchy, Is.True);
                hud.directConnectionButton.onClick.Invoke();
                yield return null;
                Assert.That(hud.directConnectionPanel.activeSelf, Is.False);

                hud.decreasePlayersButton.onClick.Invoke();
                yield return null;
                Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
                Assert.That(hud.quickPlayerCountText.text, Does.Contain("2"));
                Assert.That(developer.toggleButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(developer.toggleButton.interactable, Is.True);
                developer.toggleButton.onClick.Invoke();
                yield return null;
                Assert.That(developer.panel.activeSelf, Is.True);
                Assert.That(developer.targetParticipantsSlider.interactable, Is.True);
                developer.targetParticipantsSlider.value = 5f;
                Assert.That(network.TargetParticipantCount, Is.EqualTo(5));
                developer.Close();
                yield return null;
                Assert.That(hud.roomListPanel.activeInHierarchy, Is.True);
                Assert.That(hud.quickPlayerCountText.text, Does.Contain("5"));
            }
            finally
            {
                developer?.Close();
                network?.Stop();
            }
        }

        [UnityTest]
        public IEnumerator CancelingALocalPendingJoinBlocksOtherConnectionsUntilShutdownThenReturnsToBrowser()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LabHud hud = SavedHud();
            LabNetwork network = hud.network;
            // Hold this port so no unrelated Host can answer the test's connection request.
            using (var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                try
                {
                    network.port = (ushort)((IPEndPoint)sink.Client.LocalEndPoint).Port;
                    network.Join("127.0.0.1");
                    Assert.That(network.IsJoiningRoom, Is.True, network.Status);
                    Assert.That(network.IsConnected, Is.False);
                    Assert.That(network.CanChooseConnection, Is.False);
                    string firstAddress = network.hostAddress;
                    ushort firstPort = network.port;

                    network.Join("127.0.0.2");
                    network.Host();
                    Assert.That(network.StartRoomSearch(), Is.False,
                        "A second connection or browse must not replace an active transport attempt.");
                    Assert.That(network.hostAddress, Is.EqualTo(firstAddress));
                    Assert.That(network.port, Is.EqualTo(firstPort));
                    Assert.That(network.IsHost, Is.False);
                    Assert.That(network.IsJoiningRoom, Is.True);

                    Assert.That(network.CancelJoin(), Is.True);
                    Assert.That(network.IsCancelingJoin, Is.True);
                    Assert.That(network.CanChooseConnection, Is.False,
                        "Cancellation must wait for NGO transport shutdown before reusing it.");
                    network.Join("127.0.0.2");
                    network.Host();
                    Assert.That(network.hostAddress, Is.EqualTo(firstAddress));
                    Assert.That(network.IsHost, Is.False);

                    double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                    while ((network.IsJoiningRoom || network.IsCancelingJoin)
                           && Time.realtimeSinceStartupAsDouble < deadline)
                        yield return null;
                    Assert.That(network.IsJoiningRoom, Is.False, network.Status);
                    Assert.That(network.IsCancelingJoin, Is.False, network.Status);
                    // CanChooseConnection includes the complete NGO shutdown gate.
                    Assert.That(network.CanChooseConnection, Is.True);
                    yield return null;
                    Assert.That(hud.roomListPanel.activeInHierarchy, Is.True);
                    Assert.That(hud.hostButton.interactable, Is.True);
                    Assert.That(network.RoomSearchMessage, Is.Not.Empty);
                }
                finally { network.Stop(); }
            }
        }

        [UnityTest]
        public IEnumerator EmptyDirectAddressKeepsTheRoomBrowserAndItsFailureReasonVisible()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LabHud hud = SavedHud();
            LabNetwork network = hud.network;
            try
            {
                network.Join(string.Empty);
                string failure = network.RoomSearchMessage;
                Assert.That(failure, Is.EqualTo("Enter the Host IP address."));
                Assert.That(network.RoomSearchFailed, Is.True);
                Assert.That(network.IsJoiningRoom, Is.False);
                Assert.That(network.IsCancelingJoin, Is.False);
                Assert.That(network.IsConnected, Is.False);
                yield return null;
                yield return null;
                Assert.That(network.RoomSearchMessage, Is.EqualTo(failure),
                    "Ordinary discovery refreshes must not immediately replace the connection error.");
                Assert.That(hud.roomListPanel.activeInHierarchy, Is.True);
                Assert.That(hud.roomSearchStatusText.text, Does.Contain(failure));
                Assert.That(network.CanChooseConnection, Is.True);
            }
            finally { network.Stop(); }
        }

        [UnityTest]
        public IEnumerator HostEndpointFailureIsShownInTheBrowserInsteadOfOnlyTheHiddenLegacyStatus()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LabHud hud = SavedHud();
            LabNetwork network = hud.network;
            try
            {
                // The public Host API must reject this before binding a socket. This avoids
                // guessing whether a port is occupied on the test machine's real network.
                network.port = 0;
                network.Host();
                Assert.That(network.IsHost, Is.False);
                Assert.That(network.IsConnected, Is.False);
                Assert.That(network.RoomSearchFailed, Is.True);
                Assert.That(network.RoomSearchMessage, Does.StartWith("Host endpoint failed:"));
                yield return null;
                Assert.That(hud.roomListPanel.activeInHierarchy, Is.True);
                Assert.That(hud.roomSearchStatusText.text, Does.Contain(network.RoomSearchMessage));
                Assert.That(network.CanChooseConnection, Is.True);
            }
            finally { network.Stop(); }
        }

        private static LabHud SavedHud() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
    }
}
