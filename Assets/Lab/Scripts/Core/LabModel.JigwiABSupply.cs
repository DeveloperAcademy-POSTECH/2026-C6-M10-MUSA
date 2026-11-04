using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        private float jigwiABSupplyInterval;
        private int jigwiABSupplyInitial, jigwiABSupplyLimit;
        private readonly Dictionary<string, double> jigwiABNextSupplyAt = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, int> jigwiABSupplySequence = new Dictionary<ulong, int>();

        private void ResetJigwiABSupply(LabConfig config)
        {
            jigwiABNextSupplyAt.Clear(); jigwiABSupplySequence.Clear();
            jigwiABSupplyInterval = config.JigwiABSupplyIntervalSeconds;
            jigwiABSupplyLimit = config.JigwiABSupplyLimit;
            jigwiABSupplyInitial = Math.Min(jigwiABSupplyLimit, config.JigwiABSupplyInitialCount);
        }

        private void StartJigwiABSupply(LabJigwiABRound mission, double now)
        {
            jigwiABSupplySequence.Clear();
            jigwiABNextSupplyAt[mission.id] = mission.activeAt + jigwiABSupplyInterval;
            if (!mission.Active(now)) return;
            foreach (ulong helper in JigwiABSupplyRecipients(mission))
                GrantJigwiABSupply(mission, helper, jigwiABSupplyInitial, now);
        }

        /// <summary>
        /// #509: free Water lands on the helpers' boards (the seats B did not pick), so it has to be rolled to a
        /// picked neighbour. With no helper at all (a solo DEV room) the picked seat gets it directly.
        /// </summary>
        private IEnumerable<ulong> JigwiABSupplyRecipients(LabJigwiABRound mission)
        {
            var picked = new HashSet<ulong>(mission.players.Select(player => player.playerId));
            ulong[] helpers = seats.Take(activeSeatCount).Where(owner => !picked.Contains(owner)).ToArray();
            return helpers.Length > 0 ? helpers : picked.ToArray();
        }

        private void AdvanceJigwiABSupply(LabJigwiABRound mission, double now)
        {
            if (!mission.Active(now) || !jigwiABNextSupplyAt.TryGetValue(mission.id, out double due) || now < due) return;
            // A due check grants one current batch and skips missed periodic intervals.
            // Model slices for real mission/relight boundaries still retain their chronology.
            double elapsedIntervals = Math.Floor(Math.Max(0d, now - mission.activeAt) / jigwiABSupplyInterval);
            double next = mission.activeAt + (elapsedIntervals + 1d) * jigwiABSupplyInterval;
            if (!(next > now)) next = now + jigwiABSupplyInterval;
            jigwiABNextSupplyAt[mission.id] = next;
            foreach (ulong helper in JigwiABSupplyRecipients(mission))
                GrantJigwiABSupply(mission, helper, 1, now);
        }

        private void GrantJigwiABSupply(LabJigwiABRound mission, ulong recipient, int requested, double now)
        {
            // Count the original recipient's remaining supplies even if a friend holds one
            // or it is in flight. Passing or launching must not bypass the finite supply cap.
            int outstanding = orbs.Values.Count(orb => orb.jigwiABSupplyEventId == mission.id && orb.originOwner == recipient);
            int count = Math.Min(Math.Max(0, requested), Math.Max(0, jigwiABSupplyLimit - outstanding));
            for (int i = 0; i < count; i++)
            {
                jigwiABSupplySequence.TryGetValue(recipient, out int sequence);
                jigwiABSupplySequence[recipient] = sequence + 1;
                float x = .15f + .7f * ((sequence * .61803398875f + .5f) % 1f);
                var supply = new LabOrbState
                {
                    id = NewOrbId(), owner = recipient, kind = LabOrbKind.Combined, element = LabOrbElement.Water,
                    origin = LabOrbOrigin.Gourd, originOwner = recipient,
                    x = x, y = .22f + .1f * (sequence % 3),
                    createdAt = now, expiresAt = Math.Max(now + rules.OrbLifetimeSeconds, mission.endsAt),
                    jigwiSupply = true, jigwiABSupplyEventId = mission.id, revision = ++revision
                };
                orbs.Add(supply.id, supply);
            }
        }

        private void ClearJigwiABSupplies(LabJigwiABRound mission)
        {
            jigwiABNextSupplyAt.Remove(mission.id);
            foreach (var supply in orbs.Values.Where(orb => orb.jigwiABSupplyEventId == mission.id).ToArray())
            {
                orbs.Remove(supply.id); launchScales.Remove(supply.id);
                orbExpiries.Add(new LabOrbExpiry(supply.owner, supply.id));
                revision++;
            }
        }

        /// <summary>A B-issued orb is extinguishing ammunition, never free boss paint or a hit reward.</summary>
        private bool ConsumeJigwiABSupplyBossHit(LabOrbState orb, double now)
        {
            if (string.IsNullOrEmpty(orb.jigwiABSupplyEventId)) return false;
            var mission = jigwiABRounds.FirstOrDefault(value => value.id == orb.jigwiABSupplyEventId);
            var player = mission?.players.FirstOrDefault(value => value.playerId == orb.owner);
            if (player != null) ConsumeJigwiABWaterImpact(orb, player, now);
            else
            {
                orbs.Remove(orb.id); launchScales.Remove(orb.id); revision++;
            }
            return true;
        }
    }
}
