using System;
using System.Collections;
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
    /// <summary>Every story stage draws its gourd, and the hit shell that follows it, at the approved larger size.</summary>
    [Category("Slow")]
    public sealed class GameStageGourdSizeTests
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("Stage gourd size cleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryStoryStageUsesTheLargerGourdAndHitShell()
        {
            session.StartSolo();
            for (int stage = 1; stage <= 3; stage++)
            {
                int current = stage;
                yield return Until(() => session.State.phase == GameFlowPhase.Playing && session.State.stageIndex == current
                    && !session.IsLoadingScene && network.HasBattleReferences, "Stage " + current);
                yield return null;
                var paint = network.target.GetComponent<LabGourdPaint>();
                Assert.That(paint.Rig, Is.Not.Null);
                float expected = network.config.GourdVisualSize / paint.Rig.referenceVisualSize * 1.4f;
                Assert.That(paint.VisualInstance.transform.localScale.x, Is.EqualTo(expected).Within(1e-4f));
                Assert.That(network.target.Hitbox.transform.lossyScale.x,
                    Is.EqualTo(paint.Rig.hitShell.transform.lossyScale.x).Within(1e-4f), "The hit shell follows the drawn gourd.");
                TestContext.WriteLine("stage " + current + " hitbox y " + network.target.Hitbox.bounds.min.y + ".." + network.target.Hitbox.bounds.max.y
                    + " width " + network.target.Hitbox.bounds.size.x + " floor " + (network.battleFloor != null ? network.battleFloor.bounds.max.y.ToString() : "none"));
                if (current < 3)
                {
                    Assert.That(network.PauseBattle(), Is.True);
                    Assert.That(session.DeveloperNextStage(), Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator TappingTheDrumWithoutStaminaLeavesNoCodeOnTheBattleScreen()
        {
            session.StartSolo();
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && network.HasBattleReferences && !network.GameStartHeld, "Playing");
            int shortTaps = 0;
            Action onShort = () => shortTaps++;
            network.LocalStaminaShort += onShort;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 30d;
                while (shortTaps == 0)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "The gauge ran out.");
                    network.Generate();
                    yield return new WaitForSecondsRealtime(.05f);
                }
                yield return null;
            }
            finally { network.LocalStaminaShort -= onShort; }
            Assert.That(network.Notice, Does.Not.Contain("INSUFFICIENT_STAMINA"));
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
