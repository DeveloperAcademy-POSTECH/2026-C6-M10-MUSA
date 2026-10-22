using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        private const ulong StageJigwiDrawKey = ulong.MaxValue - 6UL;
        private const uint StageJigwiDelaySalt = 0x719D01A1u, StageJigwiKindSalt = 0x719D01A2u,
            StageJigwiTargetSalt = 0x719D01A3u;
        private double nextStageJigwiAt;
        private uint stageJigwiCount;
        private ulong? lastStageJigwiTarget;
        private float stageJigwiMin, stageJigwiMax;
        private int stageJigwiFireWeight, stageJigwiBurnWeight;

        private void ResetStageYokai(LabConfig config, double now)
        {
            stageJigwiMin = config.JigwiAutoIntervalMin;
            stageJigwiMax = config.JigwiAutoIntervalMax;
            stageJigwiFireWeight = config.JigwiAutoFireWeight;
            stageJigwiBurnWeight = config.JigwiAutoGenerationBurnWeight;
            stageJigwiCount = 0;
            lastStageJigwiTarget = null;
            nextStageJigwiAt = StageYokai == LabStageYokai.Jigwi
                ? now + StageJigwiDelay(0u) : double.PositiveInfinity;
        }

        private double StageJigwiDelay(uint index) => Mathf.Lerp(stageJigwiMin, stageJigwiMax,
            Unit01(DrawFor(seed ^ StageJigwiDelaySalt, StageJigwiDrawKey, index)));

        private List<ulong> StageJigwiTargets(bool fire, double at)
        {
            var eligible = new List<ulong>();
            for (int seat = 0; seat < activeSeatCount; seat++)
            {
                ulong player = seats[seat];
                if (activeSeatCount > 1 && lastStageJigwiTarget == player) continue;
                if (!CanStartJigwiAt(player, at, out _)) continue;
                if (fire && orbs.Values.Count(orb => orb.owner == player && orb.jigwiSupply)
                    + jigwiFireTargets + 2 > MaximumJigwiSuppliesPerPlayer) continue;
                eligible.Add(player);
            }
            return eligible;
        }

        private void AdvanceStageJigwi(double until)
        {
            if (StageYokai != LabStageYokai.Jigwi) return;
            while (nextStageJigwiAt <= until)
            {
                double at = nextStageJigwiAt;
                uint index = stageJigwiCount++;
                nextStageJigwiAt = at + StageJigwiDelay(stageJigwiCount);
                var fireTargets = stageJigwiFireWeight > 0 ? StageJigwiTargets(true, at) : null;
                var burnTargets = stageJigwiBurnWeight > 0 ? StageJigwiTargets(false, at) : null;
                int fireWeight = fireTargets != null && fireTargets.Count > 0 ? stageJigwiFireWeight : 0;
                int burnWeight = burnTargets != null && burnTargets.Count > 0 ? stageJigwiBurnWeight : 0;
                if (fireWeight + burnWeight == 0) continue;
                bool fire = DrawFor(seed ^ StageJigwiKindSalt, StageJigwiDrawKey, index)
                    % (ulong)(fireWeight + burnWeight) < (ulong)fireWeight;
                var targets = fire ? fireTargets : burnTargets;
                ulong player = targets[(int)(DrawFor(seed ^ StageJigwiTargetSalt, StageJigwiDrawKey, index)
                    % (ulong)targets.Count)];
                bool started = fire ? StartJigwiFireCore(player, at, out _)
                    : StartJigwiGenerationBurnCore(player, at, out _);
                if (started) lastStageJigwiTarget = player;
            }
        }

        /// <summary>
        /// Staged rounds process the next real gameplay boundary first, so a late frame never
        /// creates future common bombs before deciding an earlier unique pattern. None never uses this path.
        /// </summary>
        private double NextStageBoundary(double requested)
        {
            double next = Math.Min(requested, deadline);
            void Include(double at) { if (Finite(at) && at > lastTime && at < next) next = at; }
            Include(nextGourdBombAt);
            Include(nextSwipeBlockAt);
            Include(nextSabotageAt);
            Include(nextStageJigwiAt);
            Include(swipeBlockedUntil);
            Include(charmUntil);
            if (rules.ComboEnabled)
            {
                if (combo.Active) Include(combo.EndsAt);
                else { Include(nextComboAt - rules.ComboWarningSeconds); Include(nextComboAt); }
            }
            if (rules.FlashEnabled)
            {
                Include(nextFlashAt - rules.FlashWarningSeconds);
                Include(nextFlashAt);
                Include(lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds);
            }
            foreach (var player in players.Values)
            {
                Include(player.comboPenaltyUntil);
                Include(player.comboRewardUntil);
            }
            foreach (var state in jigwiStates.Values)
            {
                Include(state.fireEndsAt);
                Include(state.overheatUntil);
                Include(state.generationBurnUntil);
            }
            foreach (var orb in orbs.Values) if (!orb.inFlight) Include(ExpiresAt(orb));
            foreach (var busy in screenBusyAt.Values)
                foreach (double closedAt in busy) Include(closedAt + EventGapSeconds);
            return next;
        }

        private void ExpireOrbs(double at)
        {
            foreach (var expired in orbs.Values.Where(orb => !orb.inFlight && at >= ExpiresAt(orb)).ToArray())
            {
                if (!orbs.Remove(expired.id)) continue;
                if (expired.kind != LabOrbKind.Bomb || expired.lockSwipes == 0)
                {
                    orbExpiries.Add(new LabOrbExpiry(expired.owner, expired.id));
                    continue;
                }
                bombExplosions.Add(new LabBombBlast(expired.owner, expired.id, new Vector2(expired.x, expired.y)));
                unreportedBlasts[expired.id] = new KeyValuePair<ulong, double>(expired.owner, at);
            }
        }
    }
}
