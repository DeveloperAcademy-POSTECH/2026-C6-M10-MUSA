using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabStaminaBarHudTests
    {
        [UnityTest]
        public IEnumerator SavedBarFollowsLocalStaminaThroughSpendingRecoveryAndLobbyReturn()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var hud = Object.FindAnyObjectByType<LabHud>();
            Assert.That(hud.battleUi.transform.Find("FigmaPlayerHpBar").gameObject.activeSelf, Is.False);
            Assert.That(hud.battleUi.transform.Find("BottomHudBackground").gameObject.activeSelf, Is.False);
            var network = hud.network;
            network.port = 18271;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                network.enabled = hud.enabled = false;
                foreach (float fraction in new[] { 1f, .8f, .35f, 0f, .16f, 1f })
                {
                    // Synthetic snapshots verify HUD binding; they do not claim remote transport coverage.
                    var snapshot = new LabSnapshot { phase = LabPhase.Playing, players = new[] {
                        new LabPlayerState { id = network.LocalPlayerId, seat = 0,
                            stamina = network.config.StaminaMax * fraction },
                        new LabPlayerState { id = 902, seat = 1, stamina = 0f }
                    }};
                    typeof(LabNetwork).GetField("latest", flags).SetValue(network, snapshot);
                    typeof(LabHud).GetMethod("Update", flags).Invoke(hud, null);
                    Assert.That(hud.staminaFill.fillAmount, Is.EqualTo(fraction).Within(.0001f));
                    Assert.That(hud.staminaFill.gameObject.activeInHierarchy, Is.True);
                    Assert.That(hud.staminaFill.transform.parent.Find("StaminaFrame").gameObject.activeInHierarchy, Is.True);
                }
                typeof(LabNetwork).GetField("latest", flags).SetValue(network, null);
                typeof(LabHud).GetMethod("Update", flags).Invoke(hud, null);
                Assert.That(hud.staminaFill.fillAmount, Is.Zero);
            }
            finally { network.enabled = true; hud.enabled = true; network.Stop(); }
            yield return null;
        }
    }
}
