using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        private readonly Dictionary<ulong, int> jigwiABSupplySequence = new Dictionary<ulong, int>();

        private void ResetJigwiABSupply(LabConfig config)
        {
            // #509: DEV253~255 (interval / initial count / outstanding cap) are retired. B hands each picked seat
            // exactly one special Water orb when it starts; there is no cadence and no helper supply.
            jigwiABSupplySequence.Clear();
        }

        /// <summary>
        /// #509: when B starts, every picked seat gets one special Water orb (darker, shimmering on the board).
        /// Only this orb counts toward the simultaneous hit, and there is just one chance per B.
        /// </summary>
        private void StartJigwiABSupply(LabJigwiABRound mission, double now)
        {
            jigwiABSupplySequence.Clear();
            if (!mission.Active(now)) return;
            foreach (var picked in mission.players)
                GrantJigwiABSupply(mission, picked.playerId, now);
        }

        private void GrantJigwiABSupply(LabJigwiABRound mission, ulong recipient, double now)
        {
            // One per picked seat, even if it was passed to a friend or is in flight.
            if (orbs.Values.Any(orb => orb.jigwiABSupplyEventId == mission.id && orb.originOwner == recipient)) return;
            jigwiABSupplySequence.TryGetValue(recipient, out int sequence);
            jigwiABSupplySequence[recipient] = sequence + 1;
            var supply = new LabOrbState
            {
                id = NewOrbId(), owner = recipient, kind = LabOrbKind.Combined, element = LabOrbElement.Water,
                origin = LabOrbOrigin.Gourd, originOwner = recipient,
                x = .5f, y = .28f,
                createdAt = now, expiresAt = Math.Max(now + rules.OrbLifetimeSeconds, mission.endsAt),
                jigwiSupply = true, jigwiABSupplyEventId = mission.id, revision = ++revision
            };
            orbs.Add(supply.id, supply);
        }

        private void ClearJigwiABSupplies(LabJigwiABRound mission)
        {
            foreach (var supply in orbs.Values.Where(orb => orb.jigwiABSupplyEventId == mission.id).ToArray())
            {
                orbs.Remove(supply.id); launchScales.Remove(supply.id);
                orbExpiries.Add(new LabOrbExpiry(supply.owner, supply.id));
                revision++;
            }
        }

        /// <summary>A B-issued orb is the simultaneous-hit token, never free boss paint or a hit reward.</summary>
        private bool ConsumeJigwiABSupplyBossHit(LabOrbState orb, double now)
        {
            if (string.IsNullOrEmpty(orb.jigwiABSupplyEventId)) return false;
            // #509: it may already have counted as a sync hit; it is consumed without paint.
            orbs.Remove(orb.id); launchScales.Remove(orb.id); revision++;
            return true;
        }

        /// <summary>
        /// #509: one chance per B. A special orb that misses the boss fails this B at once (board fire), whatever the
        /// other picked seats were doing.
        /// </summary>
        private void FailJigwiSyncOnMiss(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || orb == null || string.IsNullOrEmpty(orb.jigwiABSupplyEventId) || phase != LabPhase.Playing) return;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || active.id != orb.jigwiABSupplyEventId) return;
            FailJigwiSync(active, now);
        }

        /// <summary>#509: the single chance is gone (a miss or a window that closed short) — B fails right away.</summary>
        private void FailJigwiSync(LabJigwiABRound active, double at)
        {
            if (active.resolution != LabJigwiABResolution.None) return;
            if (active.syncWindowAt >= 0d) active.syncResetAt = Math.Min(at, active.endsAt);
            active.syncWindowAt = -1d;
            foreach (var target in active.players.SelectMany(p => p.targets)) target.extinguishedAt = -1d;
            ResolveJigwiAB(active, LabJigwiABResolution.Expired, Math.Min(at, active.endsAt));
        }
    }
}
