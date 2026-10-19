using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// Exercises the saved lobby Canvas and the authoritative selection commit gates.
    /// Synthetic mappings below test local gate boundaries, not a real Client connection.
    /// </summary>
    public sealed class LabLobbyPositionSelectionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabNetwork network;
        private LabHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            hud = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
            network = hud.network;
            network.port = 18204;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (network != null)
            {
                network.enabled = true;
                network.Stop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartRequiresEveryChosenPositionAndSettingsAcknowledgementForTwoThroughFivePlayers()
        {
            for (int capacity = 2; capacity <= 5; capacity++)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!network.CanChooseConnection && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(network.CanChooseConnection, Is.True);
                Assert.That(network.SetTargetParticipantCount(capacity), Is.True);
                network.port = (ushort)(18204 + capacity);
                network.Host();
                Assert.That(network.IsHost, Is.True, network.Notice);
                yield return null;
                Assert.That(network.LocalSlotNumber, Is.Zero,
                    "A fresh normal room never reserves P1 for its Host.");
                Assert.That(network.RequestPlayerSlot(capacity), Is.True);
                Assert.That(network.IsHost, Is.True);
                Assert.That(network.LocalSlotNumber, Is.EqualTo(capacity));

                // The start predicate consumes this authoritative lobby state. No network
                // sends or fabricated transport connection callbacks are used here.
                var order = Field<List<ulong>>("order");
                var slots = Field<LabPlayerSlots>("playerSlots");
                var acknowledgements = Field<HashSet<ulong>>("configAcknowledgements");
                ulong host = network.LocalPlayerId;
                ulong[] peers = Enumerable.Range(1, capacity - 1).Select(n => (ulong)(100 + n)).ToArray();
                order.AddRange(peers);
                foreach (ulong peer in peers) acknowledgements.Add(peer);
                Assert.That(network.CanStartBattle, Is.False,
                    "Connected participants and matching settings do not replace position choices.");
                for (int i = 0; i < peers.Length; i++)
                    Assert.That(slots.TrySelect(peers[i], i + 1, out _), Is.True);
                Assert.That(network.CanStartBattle, Is.True);
                Assert.That(slots.PlayersInSlotOrder().Last(), Is.EqualTo(host));

                acknowledgements.Remove(peers[0]);
                Assert.That(network.CanStartBattle, Is.False,
                    "Every position being selected does not bypass settings confirmation.");
                acknowledgements.Add(peers[0]);
                Assert.That(slots.Release(peers[0]), Is.True);
                Assert.That(network.CanStartBattle, Is.False);
                Assert.That(slots.GetSlot(host), Is.EqualTo(capacity),
                    "Freeing a participant's position must not move the Host.");
                Assert.That(slots.TrySelect(peers[0], 1, out _), Is.True);
                Assert.That(network.CanStartBattle, Is.True);

                // Restore the actual single-Host transport state before shutdown.
                foreach (ulong peer in peers) { order.Remove(peer); slots.Release(peer); acknowledgements.Remove(peer); }
                network.Stop();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AcceptedResultWaitsForItsAuthoritativeMappingAndKeepsTheSavedChooserLocked()
        {
            HostWithoutNetworkUpdate();
            BeginPending(41, 2);
            yield return null;
            Assert.That(hud.playerSlotPanel.activeSelf, Is.True);
            Assert.That(hud.playerSlotStatusText.text, Does.Contain("WAITING FOR HOST APPROVAL"));
            foreach (Button button in hud.playerSlotButtons.Where(button => button.gameObject.activeSelf))
                Assert.That(button.interactable, Is.False);
            Assert.That(network.RequestPlayerSlot(3), Is.False,
                "Repeated requests must be blocked while approval and state are incomplete.");

            ReceiveResult(41, 2, true, 7);
            Assert.That(network.SlotSelectionPending, Is.True);
            Assert.That(network.LocalSlotNumber, Is.Zero,
                "An accepted result must not optimistically claim a P number.");
            ApplyMapping(6, 2);
            Assert.That(network.SlotSelectionPending, Is.True,
                "An older lobby revision must not complete this request even when the P number matches.");
            ApplyMapping(7, 2);
            Assert.That(network.SlotSelectionPending, Is.False);
            yield return null;
            Assert.That(network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(hud.playerSlotButtons[1].interactable, Is.True);
            Assert.That(hud.playerSlotButtons[1].GetComponentInChildren<Text>().text, Does.Contain("YOU/CLEAR"));
        }

        [UnityTest]
        public IEnumerator MappingBeforeResultStillWaitsForTheMatchingApprovalAndRejectedChangesKeepTheOldPosition()
        {
            HostWithoutNetworkUpdate();
            BeginPending(51, 2);
            ApplyMapping(9, 2);
            Assert.That(network.SlotSelectionPending, Is.True,
                "Receiving a lobby update alone must not acknowledge an unrelated pending request.");
            ReceiveResult(50, 2, true, 9);
            ReceiveResult(51, 1, true, 9);
            Assert.That(network.SlotSelectionPending, Is.True,
                "Results for another request or another position must not unlock the chooser.");
            ReceiveResult(51, 2, true, 9);
            Assert.That(network.SlotSelectionPending, Is.False);

            BeginPending(52, 1);
            ReceiveResult(52, 1, false, 9, "SLOT_TAKEN");
            Assert.That(network.SlotSelectionPending, Is.False);
            Assert.That(network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(network.Notice, Does.Contain("already selected"));
            yield return null;
            Assert.That(hud.playerSlotPanel.activeSelf, Is.True);
            Assert.That(hud.playerSlotButtons[1].interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator DeveloperRestartKeepsPositionsButLocksChoicesUntilTheRestartGateClears()
        {
            HostWithoutNetworkUpdate();
            Assert.That(network.RequestPlayerSlot(3), Is.True);
            Set("pendingDeveloperRestart", true);
            yield return null;
            Assert.That(network.IsDeveloperRestartPending, Is.True);
            Assert.That(network.CanSelectPlayerSlot, Is.False);
            Assert.That(network.RequestPlayerSlot(1), Is.False);
            Assert.That(network.LocalSlotNumber, Is.EqualTo(3));
            Assert.That(hud.playerSlotPanel.activeSelf, Is.True);
            Assert.That(hud.playerSlotStatusText.text, Does.Contain("RESTARTING"));
            foreach (Button button in hud.playerSlotButtons.Where(button => button.gameObject.activeSelf))
                Assert.That(button.interactable, Is.False);

            Set("pendingDeveloperRestart", false);
            yield return null;
            Assert.That(network.CanSelectPlayerSlot, Is.True);
            Assert.That(network.LocalSlotNumber, Is.EqualTo(3));
            Assert.That(hud.playerSlotButtons[2].interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator SelectionTimeoutReopensTheChooserWithoutInventingAnApprovedChoice()
        {
            HostWithoutNetworkUpdate();
            Assert.That(network.RequestPlayerSlot(2), Is.True);
            BeginPending(61, 1);
            Set("slotRequestSentAt", Time.realtimeSinceStartupAsDouble - 6d);
            network.enabled = true;
            yield return null;
            yield return null;
            Assert.That(network.SlotSelectionPending, Is.False);
            Assert.That(network.LocalSlotNumber, Is.EqualTo(2));
            Assert.That(network.GetSlotOwner(1), Is.EqualTo(ulong.MaxValue));
            Assert.That(network.Notice, Does.Contain("timed out"));
            Assert.That(hud.playerSlotPanel.activeSelf, Is.True);
            Assert.That(hud.playerSlotButtons[0].interactable, Is.True);
        }

        private void HostWithoutNetworkUpdate()
        {
            network.Host();
            Assert.That(network.IsHost, Is.True, network.Notice);
            Assert.That(network.LocalSlotNumber, Is.Zero);
            network.enabled = false;
        }

        private void BeginPending(long id, int slot)
        {
            Set("slotSelectionPending", true);
            Set("pendingSlotRequestId", id);
            Set("pendingSlotNumber", slot);
            Set("slotRequestSentAt", Time.realtimeSinceStartupAsDouble);
            Set("pendingSlotApproved", false);
            Set("pendingSlotLobbyRevision", 0L);
        }

        private void ApplyMapping(long revision, int slot)
        {
            Assert.That(Field<LabPlayerSlots>("playerSlots").TrySelect(network.LocalPlayerId, slot, out _), Is.True);
            Set("receivedLobbyRevision", revision);
            Invoke("TryCompleteSlotSelection");
        }

        private void ReceiveResult(long id, int slot, bool accepted, long revision, string reason = "SLOT_SELECTED")
        {
            var packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            string json = "{\"type\":\"slot_result\",\"requestId\":" + id
                + ",\"slotNumber\":" + slot + ",\"accepted\":" + (accepted ? "true" : "false")
                + ",\"lobbyRevision\":" + revision + ",\"reason\":\"" + reason + "\"}";
            Invoke("ReceiveSlotResult", JsonUtility.FromJson(json, packetType));
        }

        private T Field<T>(string name) => (T)typeof(LabNetwork).GetField(name, Private).GetValue(network);
        private void Set(string name, object value) => typeof(LabNetwork).GetField(name, Private).SetValue(network, value);
        private void Invoke(string method, params object[] arguments) => typeof(LabNetwork)
            .GetMethod(method, Private).Invoke(network, arguments);
    }
}
