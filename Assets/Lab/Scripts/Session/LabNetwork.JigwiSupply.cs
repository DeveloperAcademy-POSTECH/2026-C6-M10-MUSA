using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Scoped visual identity for an actual supplied-water flight, not a new projectile or collider.</summary>
    public sealed class LabJigwiSupplyProjectileStamp : MonoBehaviour
    {
        public string EventId { get; private set; }
        public void Bind(string eventId) => EventId = eventId;
    }

    public sealed partial class LabNetwork
    {
        private readonly LabJigwiSupplyRetirementGate jigwiSupplyRetirement = new LabJigwiSupplyRetirementGate();
        private readonly Dictionary<string, string> jigwiSupplyEvents = new Dictionary<string, string>(StringComparer.Ordinal);
        private string jigwiSupplySession;
        private uint jigwiSupplyRound;

        private void ResetJigwiSupplyTransport()
        {
            jigwiSupplySession = null; jigwiSupplyRound = 0;
            jigwiSupplyRetirement.SetActive(null, 0); jigwiSupplyEvents.Clear();
        }
        private void EnsureJigwiSupplyRound()
        {
            string session = latest?.sessionId; uint round = latest?.round ?? 0;
            if (session == jigwiSupplySession && round == jigwiSupplyRound) return;
            jigwiSupplySession = session; jigwiSupplyRound = round;
            jigwiSupplyEvents.Clear(); jigwiSupplyRetirement.SetActive(session, round);
        }
        private bool RememberJigwiSupply(string id, string eventId)
        {
            if (string.IsNullOrEmpty(id) || !LabJigwiSupplyIdentity.ValidEvent(eventId, jigwiSupplySession, jigwiSupplyRound)
                || jigwiSupplyEvents.ContainsKey(id) || jigwiSupplyEvents.Count >= LabBoardReplica.MaximumStates) return false;
            jigwiSupplyEvents.Add(id, eventId);
            return jigwiSupplyRetirement.Cancels(eventId);
        }
        private bool RememberJigwiSupplies(IEnumerable<LabOrbState> states)
        {
            bool lateRetired = false;
            foreach (LabOrbState orb in states ?? Array.Empty<LabOrbState>())
                if (orb != null) lateRetired |= RememberJigwiSupply(orb.id, orb.jigwiABSupplyEventId);
            return lateRetired;
        }
        private bool RetiredJigwiSupplyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string resolved = ResolveLocalId(id);
            return jigwiSupplyEvents.TryGetValue(resolved, out string eventId) && jigwiSupplyRetirement.Cancels(eventId);
        }

        private void ObserveJigwiSupplyRetirement()
        {
            if (latest == null) return;
            EnsureJigwiSupplyRound();
            bool cleanup = RememberJigwiSupplies(latest.orbs);
            cleanup |= jigwiSupplyRetirement.Observe(latest.sessionId, latest.round, latest.jigwiAB, latest.phase);
            boardReplica.ObserveJigwiSupplySnapshot(latest);
            if (!cleanup) return;
            if (!IsHost) latest = boardReplica.Overlay(latest);
            // Keep the action ledger alive: eventual receipts must still settle dependent requests.
            foreach (var pair in jigwiSupplyEvents)
                if (jigwiSupplyRetirement.Cancels(pair.Value))
                { board?.Vanish(pair.Key); visibleOrbIds.Remove(pair.Key); }
            foreach (var request in actionRequests.Values)
                if (RetiredJigwiSupplyId(request.firstId) || RetiredJigwiSupplyId(request.secondId))
                    RemoveRetiredJigwiSupplyActionViews(request);
            foreach (var pair in predictedShots.ToArray())
                if (RetiredJigwiSupplyProjectile(pair.Value))
                {
                    pair.Value.gameObject.SetActive(false); Destroy(pair.Value.gameObject);
                    predictedShots.Remove(pair.Key);
                }
            if (projectileRoot != null)
                for (int i = projectileRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = projectileRoot.GetChild(i);
                    var stamp = child.GetComponent<LabJigwiSupplyProjectileStamp>();
                    if (stamp == null || !jigwiSupplyRetirement.Cancels(stamp.EventId)) continue;
                    child.gameObject.SetActive(false); Destroy(child.gameObject);
                }
        }
        private bool RetiredJigwiSupplyProjectile(LabProjectile projectile) => projectile != null
            && projectile.TryGetComponent(out LabJigwiSupplyProjectileStamp stamp) && jigwiSupplyRetirement.Cancels(stamp.EventId);
        private void TagJigwiSupplyProjectile(LabProjectile projectile, string id, string eventId)
        {
            if (projectile == null || string.IsNullOrEmpty(eventId)) return;
            EnsureJigwiSupplyRound(); RememberJigwiSupply(id, eventId);
            if (!LabJigwiSupplyIdentity.ValidEvent(eventId, jigwiSupplySession, jigwiSupplyRound)) return;
            var stamp = projectile.GetComponent<LabJigwiSupplyProjectileStamp>();
            if (stamp == null) stamp = projectile.gameObject.AddComponent<LabJigwiSupplyProjectileStamp>();
            stamp.Bind(eventId);
        }
        private bool RetiredJigwiSupplyAction(LabNetworkPacket request, LabActionReceipt receipt)
        {
            EnsureJigwiSupplyRound(); RememberJigwiSupplies(receipt?.states);
            return request != null && (RetiredJigwiSupplyId(request.firstId) || RetiredJigwiSupplyId(request.secondId)
                || RetiredJigwiSupplyId(receipt?.outputId));
        }
        private void RemoveRetiredJigwiSupplyActionViews(LabNetworkPacket request)
        {
            if (request == null) return;
            foreach (string id in new[] { request.firstId, request.secondId })
                if (RetiredJigwiSupplyId(id)) board?.Vanish(ResolveLocalId(id));
            // Vanish precedes rollback so a delayed rejection cannot restore the hidden supplied body.
            RollBackAction(request.requestId);
        }
        private bool ValidJigwiSupplyReceipt(LabActionReceipt receipt) => receipt?.states == null
            || receipt.states.All(state => state == null || LabJigwiSupplyIdentity.Valid(state, receipt.sessionId, receipt.round, receipt.players));
        private bool RejectJigwiSupplyProjectile(LabNetworkPacket packet)
        {
            if (string.IsNullOrEmpty(packet.jigwiABSupplyEventId)) return false;
            EnsureJigwiSupplyRound();
            return packet.element != (int)LabOrbElement.Water || packet.fiveColor
                || !LabJigwiSupplyIdentity.ValidEvent(packet.jigwiABSupplyEventId, jigwiSupplySession, jigwiSupplyRound)
                || jigwiSupplyRetirement.Cancels(packet.jigwiABSupplyEventId);
        }
    }
}
