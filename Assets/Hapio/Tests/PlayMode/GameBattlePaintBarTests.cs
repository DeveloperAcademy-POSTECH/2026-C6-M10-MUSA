using System;
using System.Collections;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>The top paint bar is drawn across its whole track, so 100% fills it.</summary>
    [Category("Slow")]
    public sealed class GameBattlePaintBarTests
    {
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home");
            session = GameSession.Instance; network = session.Network;
            yield return null;
            network.port = AvailableDualFamilyPort();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (session != null) { network.Stop(); Object.Destroy(session.gameObject); yield return null; }
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Paint bar cleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheTopPaintBarIsDrawnAcrossTheWholeTrackAtFullPaint()
        {
            session.StartSolo();
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && network.HasBattleReferences, "Playing");
            yield return null;
            LabHud hud = Object.FindAnyObjectByType<LabHud>();
            UnityEngine.UI.Image fill = hud.yokaiHpFill;
            var track = (RectTransform)fill.transform.parent;
            Assert.That(track.name, Is.EqualTo("Fill Mask"));
            // The real drawn area: Image draws within these bounds, narrowed when it keeps the sprite's aspect.
            MethodInfo dims = typeof(UnityEngine.UI.Image).GetMethod("GetDrawingDimensions", BindingFlags.Instance | BindingFlags.NonPublic);
            var drawn = (Vector4)dims.Invoke(fill, new object[] { fill.preserveAspect });
            Assert.That(drawn.z - drawn.x, Is.EqualTo(track.rect.width).Within(1f), "Full paint must reach the end of the track.");
            Assert.That(drawn.w - drawn.y, Is.EqualTo(track.rect.height).Within(1f));
        }

        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
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
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }

        private static IEnumerator Until(Func<bool> condition, string reason, double seconds = 60d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(reason);
                yield return null;
            }
        }
    }
}
