using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Screen-only A/B policy, separate from the retained surface and standalone Lab experiments.</summary>
    public sealed partial class LabModel
    {
        private readonly List<LabJigwiABRound> jigwiABRounds = new List<LabJigwiABRound>();
        private readonly HashSet<string> jigwiABActivated = new HashSet<string>(StringComparer.Ordinal);
        private bool jigwiABEnabled;
        private int jigwiABFirstSeat, jigwiABRequiredHits, jigwiABFireCount;
        private int[] jigwiABAffectedCounts;
        private float jigwiABDuration, jigwiABRecovery, jigwiABBurnSeconds, jigwiABRelight,
            jigwiABShrinkDelay, jigwiABShrinkScale, jigwiABMoveAmount, jigwiABMovePeriod, jigwiABTargetRadius;
        public bool JigwiABEnabled => jigwiABEnabled;
        private LabJigwiABRound CurrentJigwiABRound => jigwiABRounds.FirstOrDefault(value => value.resolution == LabJigwiABResolution.None);

        private void ResetJigwiAB(LabConfig config, double now)
        {
            jigwiABEnabled = !teamMode && StageYokai == LabStageYokai.Jigwi && config.JigwiStageTimelineEnabled;
            jigwiABRounds.Clear(); jigwiABActivated.Clear();
            if (!jigwiABEnabled) return;
            // Preserve the old code as an inactive migration path, never run two policies together.
            jigwiStageEnabled = false; jigwiStageGroups.Clear(); jigwiGourdBlastIds.Clear();
            jigwiStageNextSlot = 0; jigwiStageCutoffReached = false;
            jigwiStageStartedAt = now; jigwiStageCutoffAt = now + 100d;
            jigwiStageWarning = config.JigwiStageWarningSeconds; jigwiStageFever = config.JigwiStageFeverMultiplier;
            jigwiABDuration = config.JigwiABPersonalDurationSeconds; jigwiABRecovery = config.JigwiABPersonalRecoveryMultiplier;
            jigwiABRequiredHits = config.JigwiABPersonalRequiredHits; jigwiABBurnSeconds = config.JigwiABPersonalBurnSeconds;
            jigwiABFireCount = config.JigwiABGroupFireCount; jigwiABRelight = config.JigwiABRelightSeconds;
            jigwiABShrinkDelay = config.JigwiABShrinkDelaySeconds; jigwiABShrinkScale = config.JigwiABShrinkScale;
            jigwiABMoveAmount = config.JigwiABFireMoveAmount; jigwiABMovePeriod = config.JigwiABFireMovePeriodSeconds;
            jigwiABTargetRadius = config.JigwiABFireTargetRadius; jigwiABAffectedCounts = new int[activeSeatCount];
            jigwiABFirstSeat = ((int)(DrawFor(SeedFor(sessionId, 1u) ^ 0x332A0B01u, ulong.MaxValue - 8UL, 0u)
                % (ulong)activeSeatCount) + (int)((round - 1u) % (uint)activeSeatCount)) % activeSeatCount;
            paintSurface.SetPaintBlocker(null);
            nextCommonGimmickAt = NextJigwiABCommonAt(0u);
            nextUniqueGimmickAt = nextComboAt = nextFlashAt = nextStageJigwiAt = double.PositiveInfinity;
        }
        private double NextJigwiABCommonAt(uint index) => !jigwiStageCutoffReached && index < JigwiStageCommonSlots.Length
            ? jigwiStageStartedAt + JigwiStageCommonSlots[index] : double.PositiveInfinity;
        private double NextJigwiABBoundary(double requested)
        {
            if (phase != LabPhase.Playing || jigwiStageCutoffReached) return requested;
            double next = requested;
            void Include(double at) { if (at > lastTime && at < next) next = at; }
            Include(jigwiStageCutoffAt);
            if (jigwiStageNextSlot < JigwiStageSlots.Length) Include(jigwiStageStartedAt + JigwiStageSlots[jigwiStageNextSlot]);
            var active = CurrentJigwiABRound;
            if (active != null)
            {
                Include(active.activeAt); Include(active.endsAt);
                if (active.kind == LabJigwiABKind.GroupFire)
                {
                    if (!active.shrunk) Include(active.shrinkAt);
                    foreach (var player in active.players)
                        foreach (var fire in player.targets) if (!fire.lit) Include(fire.relightAt);
                }
            }
            return next;
        }
        private void AdvanceJigwiAB(double now)
        {
            if (phase != LabPhase.Playing)
            {
                foreach (var item in jigwiABRounds.Where(value => value.resolution == LabJigwiABResolution.None).ToArray())
                    ResolveJigwiAB(item, LabJigwiABResolution.Cancelled, Math.Min(now, jigwiStageCutoffAt));
                return;
            }
            if (!jigwiStageCutoffReached && now >= jigwiStageCutoffAt)
            {
                foreach (var item in jigwiABRounds.Where(value => value.resolution == LabJigwiABResolution.None).ToArray())
                    ResolveJigwiAB(item, LabJigwiABResolution.Cancelled, jigwiStageCutoffAt);
                EndJigwiStageInterference(); // Reuse source-specific common-bomb cleanup, without a new engine.
            }
            if (jigwiStageCutoffReached) return;
            var active = CurrentJigwiABRound;
            if (active != null)
            {
                if (now >= active.endsAt) ResolveJigwiAB(active, LabJigwiABResolution.Expired, active.endsAt);
                else if (now >= active.activeAt) AdvanceJigwiABActive(active, now);
            }
            while (jigwiStageNextSlot < JigwiStageSlots.Length
                && jigwiStageStartedAt + JigwiStageSlots[jigwiStageNextSlot] <= now)
            {
                int slot = jigwiStageNextSlot++;
                BeginJigwiABSlot(slot, jigwiStageStartedAt + JigwiStageSlots[slot]);
            }
            active = CurrentJigwiABRound;
            if (active != null && active.Active(now)) AdvanceJigwiABActive(active, now);
        }
        private void AdvanceJigwiABActive(LabJigwiABRound active, double now)
        {
            if (jigwiABActivated.Add(active.id))
            {
                foreach (var orb in orbs.Values) ApplyJigwiABOrb(orb, active.activeAt);
                revision++;
            }
            if (active.kind != LabJigwiABKind.GroupFire) return;
            foreach (var player in active.players)
                foreach (var fire in player.targets)
                    if (!fire.lit && now >= fire.relightAt) { fire.lit = true; revision++; }
            if (!active.shrunk && now >= active.shrinkAt)
            {
                active.shrunk = true;
                foreach (var orb in orbs.Values) ApplyJigwiABOrb(orb, active.shrinkAt);
                revision++;
            }
        }
        private void BeginJigwiABSlot(int slot, double at)
        {
            bool personal = slot == 0 || slot == 2 || slot == 4 || slot == 6;
            var item = new LabJigwiABRound
            {
                id = "jigwi-ab/" + sessionId + "/" + round + "/" + slot, slot = slot,
                kind = personal ? LabJigwiABKind.PersonalBurn : LabJigwiABKind.GroupFire,
                warningAt = at, activeAt = Math.Min(jigwiStageCutoffAt, at + jigwiStageWarning),
                endsAt = personal ? Math.Min(jigwiStageCutoffAt, at + jigwiStageWarning + jigwiABDuration) : jigwiStageCutoffAt,
                requiredHits = personal ? jigwiABRequiredHits : 0, shrinkAt = at + jigwiStageWarning + jigwiABShrinkDelay,
                shrinkScale = jigwiABShrinkScale, moveAmount = jigwiABMoveAmount,
                movePeriodSeconds = jigwiABMovePeriod, targetRadius = jigwiABTargetRadius
            };
            if (CurrentJigwiABRound != null)
            {
                item.resolution = LabJigwiABResolution.Skipped; item.resolvedAt = at;
                if (personal) item.targetPlayer = seats[jigwiABFirstSeat];
            }
            else if (personal)
            {
                int minimum = jigwiABAffectedCounts.Min();
                int chosen = Enumerable.Range(0, activeSeatCount).Select(i => (jigwiABFirstSeat + i) % activeSeatCount)
                    .First(i => jigwiABAffectedCounts[i] == minimum);
                item.targetPlayer = seats[chosen]; jigwiABAffectedCounts[chosen]++;
            }
            else item.players = seats.Take(activeSeatCount).Select(owner => new LabJigwiABPlayerFire
            {
                playerId = owner, targets = Enumerable.Range(0, jigwiABFireCount).Select(i => new LabJigwiABFireTarget
                {
                    id = item.id + "/" + owner + "/" + i,
                    x = .2f + .6f * (i + 1f) / (jigwiABFireCount + 1f), y = .62f + .04f * (i % 2)
                }).ToArray()
            }).ToArray();
            jigwiABRounds.Add(item); revision++;
        }
        private void ResolveJigwiAB(LabJigwiABRound item, LabJigwiABResolution result, double at)
        {
            if (item == null || item.resolution != LabJigwiABResolution.None) return;
            item.resolution = result; item.resolvedAt = Math.Max(item.warningAt, at);
            foreach (var orb in orbs.Values)
            {
                if (item.kind == LabJigwiABKind.PersonalBurn && orb.jigwiABBurnEventId == item.id)
                { orb.jigwiABBurnEventId = null; orb.jigwiABBurnAt = -1d; orb.revision = ++revision; }
                if (item.kind == LabJigwiABKind.GroupFire && !orb.inFlight && orb.jigwiABRadiusScale != 1f)
                { orb.jigwiABRadiusScale = 1f; orb.revision = ++revision; }
            }
            revision++;
        }
        public float JigwiABRecoveryMultiplier(ulong player, double at)
        {
            var active = CurrentJigwiABRound;
            if (jigwiABEnabled && active != null && active.kind == LabJigwiABKind.PersonalBurn
                && active.targetPlayer == player && active.Active(at)) return jigwiABRecovery;
            return JigwiStageRecoveryMultiplier(at);
        }
        private LabOrbElement JigwiABCombinedElement(LabOrbElement normal, double at)
        {
            var active = CurrentJigwiABRound;
            return jigwiABEnabled && active != null && active.kind == LabJigwiABKind.GroupFire && active.Active(at)
                ? LabOrbElement.Water : normal;
        }
        private void ApplyJigwiABOrb(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || orb.inFlight || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) return;
            var active = CurrentJigwiABRound;
            if (active == null || !active.Active(now)) return;
            if (active.kind == LabJigwiABKind.PersonalBurn && orb.owner == active.targetPlayer)
            {
                double deadline = now + jigwiABBurnSeconds;
                if (orb.jigwiABBurnAt < 0d || deadline < orb.jigwiABBurnAt)
                { orb.jigwiABBurnAt = deadline; orb.jigwiABBurnEventId = active.id; orb.revision = ++revision; }
            }
            if (active.kind == LabJigwiABKind.GroupFire && active.shrunk && orb.jigwiABRadiusScale != active.shrinkScale)
            { orb.jigwiABRadiusScale = active.shrinkScale; orb.revision = ++revision; }
        }
        private void CarryJigwiABBurn(LabOrbState first, LabOrbState second, LabOrbState combined, double now)
        {
            var earliest = first.jigwiABBurnAt >= 0d && (second.jigwiABBurnAt < 0d || first.jigwiABBurnAt <= second.jigwiABBurnAt)
                ? first : second;
            combined.jigwiABBurnAt = earliest.jigwiABBurnAt; combined.jigwiABBurnEventId = earliest.jigwiABBurnEventId;
            ApplyJigwiABOrb(combined, now);
        }
        private void RecordJigwiABBossHit(LabOrbState orb, Vector3 direction, double now)
        {
            var active = CurrentJigwiABRound;
            if (!jigwiABEnabled || active == null || !active.Active(now) || active.kind != LabJigwiABKind.PersonalBurn
                || active.targetPlayer != orb.owner || orb.kind != LabOrbKind.Combined
                || !LabJigwiStageNetworkValidation.Direction(direction, false)) return;
            active.hitCount++; revision++;
            if (active.hitCount >= active.requiredHits) ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
        }
        /// <summary>A cleared personal curtain opens that player's boss even while teammates are still extinguishing theirs.</summary>
        public bool JigwiABBlocksBoss(ulong owner, double now)
        {
            var active = CurrentJigwiABRound;
            return jigwiABEnabled && phase == LabPhase.Playing && active != null
                && active.kind == LabJigwiABKind.GroupFire && active.Active(now)
                && active.players.Any(player => player.playerId == owner && player.targets.Any(target => target.lit));
        }
        private void ConsumeJigwiABWaterImpact(LabOrbState orb, LabJigwiABPlayerFire owner, double now)
        {
            orbs.Remove(orb.id); launchScales.Remove(orb.id);
            if (orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water)
                owner.lastWaterSplashAt = now;
            revision++;
        }
        /// <summary>Final authority backstop if a real projectile curves around the visible curtain or crosses a collision boundary.</summary>
        private bool TryBlockJigwiABBossHit(LabOrbState orb, double now)
        {
            if (!JigwiABBlocksBoss(orb.owner, now)) return false;
            var owner = CurrentJigwiABRound.players.First(player => player.playerId == orb.owner);
            ConsumeJigwiABWaterImpact(orb, owner, now);
            return true;
        }
        /// <summary>Called only for a real Host fire collider collision, without normal paint/hit rewards or statistics.</summary>
        public bool ConfirmJigwiABFireHit(string projectileOrbId, string eventId, string targetId, double now, out bool extinguished)
        {
            extinguished = false;
            if (!jigwiABEnabled || !TryAdvance(now, out _) || phase != LabPhase.Playing
                || !orbs.TryGetValue(projectileOrbId ?? string.Empty, out var orb) || !orb.inFlight) return false;
            var active = CurrentJigwiABRound;
            if (active == null || active.kind != LabJigwiABKind.GroupFire || !active.Active(now) || active.id != eventId) return false;
            var owner = active.players.FirstOrDefault(p => p.playerId == orb.owner);
            if (owner == null) return false;
            bool curtain = targetId == LabJigwiABFireMotion.CurtainId(active.id, orb.owner)
                && owner.targets.Any(value => value.lit);
            var fire = owner.targets.FirstOrDefault(value => value.id == targetId && value.lit);
            if (!curtain && fire == null) return false;
            ConsumeJigwiABWaterImpact(orb, owner, now);
            if (!curtain && orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water)
            {
                fire.lit = false; fire.extinguishedAt = now; fire.relightAt = now + jigwiABRelight;
                extinguished = true; revision++;
                if (active.players.All(p => p.targets.All(value => !value.lit)))
                    ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
            }
            return true;
        }
        private LabJigwiABSnapshot CopyJigwiABSnapshot(double now) => !jigwiABEnabled ? new LabJigwiABSnapshot()
            : new LabJigwiABSnapshot { enabled = true, fever = phase == LabPhase.Playing && now >= jigwiStageCutoffAt,
                startedAt = jigwiStageStartedAt, cutoffAt = jigwiStageCutoffAt,
                rounds = jigwiABRounds.Select(item => item.Copy()).ToArray() };
    }
}
