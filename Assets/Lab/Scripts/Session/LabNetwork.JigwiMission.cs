using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>A bounded, round-scoped cancellation fence; no growing list of consumed orb IDs travels in headers.</summary>
    public sealed class LabJigwiMissionPurgeGate
    {
        private string session;
        private uint round;
        public ulong Revision { get; private set; }

        public void SetActive(string nextSession, uint nextRound)
        {
            if (session == nextSession && round == nextRound) return;
            session = nextSession; round = nextRound; Revision = 0;
        }

        public bool TryAccept(string sourceSession, uint sourceRound, string eventId, ulong revision)
        {
            if (string.IsNullOrEmpty(session) || round == 0 || sourceSession != session || sourceRound != round
                || revision <= Revision || string.IsNullOrEmpty(eventId)) return false;
            string prefix = "jigwi-ab/" + session + "/" + round + "/";
            if (!eventId.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(eventId.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int slot)
                || slot < 0 || slot >= 6 || slot % 2 != 1) return false;
            Revision = revision;
            return true;
        }

        public bool Cancels(ulong revision) => Revision > 0 && revision <= Revision;
    }

    public sealed partial class LabNetwork
    {
        private readonly LabJigwiMissionPurgeGate jigwiMissionPurge = new LabJigwiMissionPurgeGate();

        private void AnnounceJigwiMissionFailures()
        {
            if (!IsHost || model == null || !model.JigwiABEnabled
                || model.JigwiABFailureRevision <= jigwiMissionPurge.Revision) return;
            foreach (var failed in model.JigwiABState.rounds.Where(value => value.kind == LabJigwiABKind.GroupFire
                && value.resolution == LabJigwiABResolution.Expired).OrderBy(value => value.clearedThroughRevision))
            {
                if (!ApplyJigwiMissionFailure(actionSession, actionRound, failed.id,
                    failed.clearedThroughRevision, failed.resolvedAt)) continue;
                var notice = new LabNetworkPacket { type = "jigwiFailure", sessionId = actionSession, round = actionRound,
                    firstId = failed.id, modelRevision = failed.clearedThroughRevision, eventAt = failed.resolvedAt };
                foreach (ulong peer in order) if (peer != LocalPlayerId) Send(peer, notice);
            }
        }

        private void ObserveJigwiMissionFailures()
        {
            if (latest?.jigwiAB?.enabled != true) return;
            foreach (var failed in latest.jigwiAB.rounds.Where(value => value != null
                && value.kind == LabJigwiABKind.GroupFire && value.resolution == LabJigwiABResolution.Expired)
                .OrderBy(value => value.clearedThroughRevision))
                ApplyJigwiMissionFailure(latest.sessionId, latest.round, failed.id,
                    failed.clearedThroughRevision, failed.resolvedAt);
        }

        private bool ApplyJigwiMissionFailure(string sourceSession, uint sourceRound, string eventId,
            ulong revision, double eventAt)
        {
            if (double.IsNaN(eventAt) || double.IsInfinity(eventAt) || eventAt < 0d) return false;
            jigwiMissionPurge.SetActive(actionSession, actionRound);
            if (!jigwiMissionPurge.TryAccept(sourceSession, sourceRound, eventId, revision)) return false;
            boardReplica.PurgeThrough(revision, eventAt);

            // Keep the action ledger alive. An authoritative receipt must still settle dependencies,
            // but a pending preview or an old launch must not survive the failed mission.
            foreach (var entry in actionRequests.ToArray())
                if (localActions.TryGetPending(entry.Key, out var pending)
                    && pending.state != LabPendingActionState.Accepted && pending.state != LabPendingActionState.Rejected)
                    RemovePurgedActionViews(entry.Value, null);

            foreach (var entry in predictedShots.ToArray())
                if (entry.Value == null || jigwiMissionPurge.Cancels(entry.Value.AuthorityRevision))
                {
                    if (entry.Value != null) { entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject); }
                    predictedShots.Remove(entry.Key);
                    presentedShots.Remove(entry.Key); // A genuinely post-failure approval may create a new visual.
                }
            if (projectileRoot != null)
                for (int index = projectileRoot.childCount - 1; index >= 0; index--)
                {
                    Transform child = projectileRoot.GetChild(index);
                    LabProjectile shot = child.GetComponent<LabProjectile>();
                    LabProjectileRevisionStamp tail = child.GetComponent<LabProjectileRevisionStamp>();
                    ulong bornAt = shot != null ? shot.AuthorityRevision : tail != null ? tail.Revision : 0;
                    if (!jigwiMissionPurge.Cancels(bornAt)) continue;
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            if (!IsHost && latest != null) latest = boardReplica.Overlay(latest);
            return true;
        }

        private void RemovePurgedActionViews(LabNetworkPacket request, LabActionReceipt receipt)
        {
            if (request == null || board == null) return;
            // A newer Host approval may already have reconciled this predicted flight while its receipt is delayed.
            if (predictedShots.TryGetValue(request.requestId, out var approved) && approved != null
                && !jigwiMissionPurge.Cancels(approved.AuthorityRevision)) return;
            RollBackAction(request.requestId);
            presentedShots.Remove(request.requestId);
            string first = ResolveLocalId(request.firstId), second = ResolveLocalId(request.secondId);
            bool Drum(string id) => !string.IsNullOrEmpty(id) &&
                ((latest?.drums ?? Array.Empty<LabOrbState>()).Any(value => value.id == id)
                || (receipt?.states ?? Array.Empty<LabOrbState>()).Any(value => value.id == id && value.kind.IsDrum()));
            if (!string.IsNullOrEmpty(first) && !Drum(first)) board.Vanish(first);
            if (!string.IsNullOrEmpty(second) && !Drum(second)) board.Vanish(second);
            foreach (var state in receipt?.states ?? Array.Empty<LabOrbState>())
                if (!state.kind.IsDrum() && jigwiMissionPurge.Cancels(state.revision)) board.Vanish(state.id);
        }

        private bool JigwiRewardAt(double now) => latest?.jigwiAB?.Reward(now) != null;
    }
}
