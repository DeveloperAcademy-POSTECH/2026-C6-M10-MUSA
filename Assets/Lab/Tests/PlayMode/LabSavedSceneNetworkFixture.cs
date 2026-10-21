using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Owns the saved Scene's network even after NGO moves it into DontDestroyOnLoad.
    /// Test ports are chosen from the OS ephemeral range instead of shared fixed ports.
    /// </summary>
    internal sealed class LabSavedSceneNetworkFixture
    {
        private LabNetwork ownedNetwork;
        private Scene ownedScene;

        public IEnumerator Load()
        {
            Assert.That(ownedNetwork, Is.Null, "Dispose the previous fixture before loading another Scene.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ownedScene = SceneManager.GetActiveScene();
            // Scene-root lookup avoids stale NetworkManagers retained by other fixtures.
            LabDeveloperMode developer = ownedScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            developer.allowOfflineEditorTuning = true; // Explicit offline physics fixture, not the product lobby.
            ownedNetwork = developer.network;
            Assert.That(ownedNetwork, Is.Not.Null, "The saved DEV UI must own its network reference.");
        }

        public ushort UseAvailablePort(LabNetwork network)
        {
            Assert.That(network, Is.SameAs(ownedNetwork), "Assign ports only to this fixture's network.");
            network.port = FindAvailableDualFamilyPort();
            return network.port;
        }

        private static ushort FindAvailableDualFamilyPort()
        {
            // Both listeners must be available because the real Host binds the same port in
            // both address families. Never enable port reuse or retry a failed real Host.
            for (int attempt = 0; attempt < 16; attempt++)
            {
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        // Production rejects port 0. Release the probe sockets just before
                        // startup; an unrelated process could still claim the port afterwards.
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        // IPv4 and IPv6 ephemeral allocations can differ. Probe another pair
                        // before starting UnityTransport; its real bind errors remain visible.
                    }
                }
            }
            throw new InvalidOperationException("Could not reserve a free IPv4/IPv6 UDP port for the fixture.");
        }

        public IEnumerator Cleanup()
        {
            if (ownedNetwork != null)
            {
                // Stop alone schedules NGO shutdown; loading another Scene does not dispose
                // this GameObject once NGO has moved it into DontDestroyOnLoad.
                ownedNetwork.Stop();
                Object.Destroy(ownedNetwork.gameObject);
                yield return null;
                Assert.That(ownedNetwork == null, Is.True, "The fixture must release its owned NetworkManager.");
                ownedNetwork = null;
            }
            if (!ownedScene.IsValid() || !ownedScene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Saved network fixture cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(ownedScene);
            ownedScene = default;
        }
    }
}
