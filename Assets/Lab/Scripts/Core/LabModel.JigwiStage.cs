using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Jigwi's stage-only A/B policy. Standalone Lab retains the old manual experiment.</summary>
    public sealed partial class LabModel
    {
        private static readonly double[] JigwiStageSlots = { 10d, 30d, 40d, 50d, 60d, 70d, 80d, 90d };
        private static readonly double[] JigwiStageCommonSlots = { 20d, 40d, 60d, 80d };
        private const uint JigwiStageRoleSalt = 0x332A0B01u;
        private const ulong JigwiStageDrawKey = ulong.MaxValue - 8UL;
        private readonly List<LabJigwiStageGroup> jigwiStageGroups = new List<LabJigwiStageGroup>();
        private bool jigwiStageEnabled, jigwiStageCutoffReached;
        private double jigwiStageStartedAt, jigwiStageCutoffAt, jigwiStageAt;
        private int jigwiStageNextSlot, jigwiStageFirstSeat, jigwiStageFirstAffected, jigwiStageFirstHelper;
        private int[] jigwiStageAffectedCounts, jigwiStageHelperCounts;
        private float jigwiStageWarning, jigwiStageActive, jigwiStageRootAngle, jigwiStageFireAngle,
            jigwiStageSpread, jigwiStageFever;
        private int jigwiStagePatches;
        private Func<ulong, Vector3, bool> jigwiStageBlocker;

        public bool JigwiStageEnabled => jigwiABEnabled || jigwiStageEnabled;

        private void ResetJigwiStage(LabConfig config, double now)
        {
            ResetJigwiAB(config, now);
            if (jigwiABEnabled) return;
            jigwiStageBlocker = JigwiStagePaintBlocked;
            jigwiStageEnabled = !teamMode && StageYokai == LabStageYokai.Jigwi && config.JigwiStageTimelineEnabled;
            jigwiStageGroups.Clear();
            jigwiGourdBlastIds.Clear();
            jigwiStageNextSlot = 0;
            jigwiStageCutoffReached = false;
            jigwiStageStartedAt = jigwiStageAt = now;
            jigwiStageCutoffAt = now + 100d;
            jigwiStageWarning = config.JigwiStageWarningSeconds;
            jigwiStageActive = config.JigwiStageActiveSeconds;
            jigwiStageRootAngle = config.JigwiStageRootAngleDegrees;
            jigwiStageFireAngle = config.JigwiStageFireAngleDegrees;
            jigwiStageSpread = config.JigwiStageSpreadSeconds;
            jigwiStagePatches = config.JigwiStageSpreadPatches;
            jigwiStageFever = config.JigwiStageFeverMultiplier;
            jigwiStageAffectedCounts = new int[activeSeatCount];
            jigwiStageHelperCounts = new int[activeSeatCount];
            uint roleSeed = SeedFor(sessionId, 1u);
            int firstSeat = (int)(DrawFor(roleSeed ^ JigwiStageRoleSalt, JigwiStageDrawKey, 0u) % (ulong)activeSeatCount);
            jigwiStageFirstSeat = (firstSeat + (int)((round - 1u) % (uint)activeSeatCount)) % activeSeatCount;
            jigwiStageFirstAffected = jigwiStageFirstSeat;
            jigwiStageFirstHelper = (jigwiStageFirstSeat + 1) % activeSeatCount;
            paintSurface.SetPaintBlocker(null);
            if (!jigwiStageEnabled) return;
            // Reuse the single common dispatcher with these occurrences; never a second repeating timer.
            nextCommonGimmickAt = NextJigwiStageCommonAt(0u);
            nextUniqueGimmickAt = nextComboAt = nextFlashAt = nextStageJigwiAt = double.PositiveInfinity;
        }

        private double NextJigwiStageCommonAt(uint index) => jigwiABEnabled ? NextJigwiABCommonAt(index) : jigwiStageEnabled && !jigwiStageCutoffReached
            && index < JigwiStageCommonSlots.Length ? jigwiStageStartedAt + JigwiStageCommonSlots[index]
            : double.PositiveInfinity;

        private double NextJigwiStageBoundary(double requested)
        {
            if (jigwiABEnabled) return NextJigwiABBoundary(requested);
            if (!jigwiStageEnabled || phase != LabPhase.Playing) return requested;
            double next = requested;
            void Include(double at) { if (at > lastTime && at < next) next = at; }
            if (!jigwiStageCutoffReached)
            {
                Include(jigwiStageCutoffAt);
                if (jigwiStageNextSlot < JigwiStageSlots.Length)
                    Include(jigwiStageStartedAt + JigwiStageSlots[jigwiStageNextSlot]);
                foreach (var group in jigwiStageGroups)
                {
                    if (group.resolution != LabJigwiStageResolution.None) continue;
                    Include(group.activeAt);
                    Include(group.expiresAt);
                    for (int i = 1; i < group.patchDirections.Length; i++)
                        Include(group.activeAt + group.spreadSeconds * i);
                }
            }
            return next;
        }

        private void AdvanceJigwiStage(double now)
        {
            if (jigwiABEnabled) { AdvanceJigwiAB(now); return; }
            if (!jigwiStageEnabled) return;
            jigwiStageAt = now;
            if (phase != LabPhase.Playing)
            {
                CancelJigwiStageGroups(Math.Min(now, jigwiStageCutoffAt));
                paintSurface.SetPaintBlocker(null);
                return;
            }
            if (!jigwiStageCutoffReached && now >= jigwiStageCutoffAt)
                EndJigwiStageInterference();
            if (!jigwiStageCutoffReached)
            {
                while (jigwiStageNextSlot < JigwiStageSlots.Length
                    && jigwiStageStartedAt + JigwiStageSlots[jigwiStageNextSlot] <= now)
                {
                    int slot = jigwiStageNextSlot++;
                    BeginJigwiStageSlot(slot, jigwiStageStartedAt + JigwiStageSlots[slot]);
                }
                foreach (var group in jigwiStageGroups)
                    if (group.resolution == LabJigwiStageResolution.None && now >= group.expiresAt)
                        ResolveJigwiStageGroup(group, LabJigwiStageResolution.Expired, group.expiresAt);
            }
            UpdateJigwiStagePaintBlocker();
        }

        private void UpdateJigwiStagePaintBlocker()
        {
            bool blocking = false;
            if (!jigwiStageCutoffReached && phase == LabPhase.Playing)
                for (int i = 0; i < jigwiStageGroups.Count; i++)
                    if (jigwiStageGroups[i].geometryConfigured && jigwiStageGroups[i].Active(jigwiStageAt))
                    { blocking = true; break; }
            paintSurface.SetPaintBlocker(blocking ? jigwiStageBlocker : null);
        }

        public bool JigwiStagePaintBlocked(ulong player, Vector3 direction)
        {
            if (!jigwiStageEnabled || jigwiStageCutoffReached || phase != LabPhase.Playing) return false;
            // This callback runs per candidate surface cell; do not allocate a captured predicate.
            for (int i = 0; i < jigwiStageGroups.Count; i++)
                if (jigwiStageGroups[i].Blocks(player, direction, jigwiStageAt)) return true;
            return false;
        }

        public float JigwiStageRecoveryMultiplier(double at) => JigwiStageEnabled && phase == LabPhase.Playing
            && at >= jigwiStageCutoffAt ? jigwiStageFever : 1f;

        private LabJigwiStageSnapshot CopyJigwiStageSnapshot(double now)
        {
            if (!jigwiStageEnabled) return new LabJigwiStageSnapshot();
            if (phase != LabPhase.Playing) AdvanceJigwiStage(now);
            return new LabJigwiStageSnapshot
            {
                enabled = true, fever = phase == LabPhase.Playing && now >= jigwiStageCutoffAt,
                startedAt = jigwiStageStartedAt, cutoffAt = jigwiStageCutoffAt,
                groups = jigwiStageGroups.Select(group => group.Copy()).ToArray()
            };
        }

        /// <summary>Called only from the Host's real shell hit path, before normal paint for that projectile.</summary>
        public bool TryExtinguishJigwiStageRoot(ulong attacker, Vector3 localDirection, double now, out string groupId)
        {
            groupId = null;
            if (!jigwiStageEnabled || !LabJigwiStageNetworkValidation.Direction(localDirection, false)
                || !TryAdvance(now, out _) || phase != LabPhase.Playing || jigwiStageCutoffReached) return false;
            LabJigwiStageGroup selected = null;
            float closest = float.NegativeInfinity;
            foreach (var group in jigwiStageGroups)
            {
                if (!group.ContainsRoot(attacker, localDirection, now)) continue;
                float dot = Vector3.Dot(group.rootDirection, localDirection.normalized);
                if (dot > closest) { closest = dot; selected = group; }
            }
            if (selected == null) return false;
            ResolveJigwiStageGroup(selected, LabJigwiStageResolution.Extinguished, now);
            groupId = selected.id;
            UpdateJigwiStagePaintBlocker();
            return true;
        }

        /// <summary>Host-sampled local shell anchors, never client-provided collision claims.</summary>
        public bool SetJigwiStageGeometry(string groupId, Vector3 rootLocalDirection,
            Vector3[] patchLocalDirections, double now)
        {
            if (!jigwiStageEnabled || !LabJigwiStageNetworkValidation.Direction(rootLocalDirection, false)
                || patchLocalDirections == null || patchLocalDirections.Length < 1
                || patchLocalDirections.Length > LabJigwiStageNetworkValidation.MaximumPatches
                || patchLocalDirections.Any(value => !LabJigwiStageNetworkValidation.Direction(value, false))
                || !TryAdvance(now, out _) || phase != LabPhase.Playing || jigwiStageCutoffReached) return false;
            var group = jigwiStageGroups.FirstOrDefault(item => item.id == groupId);
            if (group == null || group.resolution != LabJigwiStageResolution.None || now >= group.expiresAt) return false;
            Vector3 root = rootLocalDirection.normalized;
            Vector3[] patches = patchLocalDirections.Select(direction => direction.normalized).ToArray();
            if (group.geometryConfigured && Vector3.SqrMagnitude(group.rootDirection - root) < .0000001f
                && group.patchDirections.Length == patches.Length
                && group.patchDirections.Zip(patches, (a, b) => (a - b).sqrMagnitude).All(square => square < .0000001f)) return true;
            group.rootDirection = root;
            group.patchDirections = patches;
            group.geometryConfigured = true;
            revision++;
            UpdateJigwiStagePaintBlocker();
            return true;
        }

        private void BeginJigwiStageSlot(int slot, double at)
        {
            bool ignition = slot == 0 || slot == 2 || slot == 4 || slot == 6;
            if (ignition)
            {
                int affected = LeastJigwiAffectedSeat(slot);
                int count = slot == 6 ? 2 : 1;
                for (int i = 0; i < count; i++) CreateJigwiStageGroup(slot, i, affected, affected, at, count);
                return;
            }
            if (activeSeatCount < 2) return; // A solo fixture cannot demonstrate cooperation.
            int victim, helper;
            if (slot == 1)
            {
                victim = jigwiStageFirstAffected;
                helper = jigwiStageFirstHelper;
            }
            else if (slot == 3) { victim = jigwiStageFirstHelper; helper = jigwiStageFirstAffected; }
            else if (slot == 5 && activeSeatCount == 2)
            { victim = jigwiStageFirstAffected; helper = jigwiStageFirstHelper; }
            else SelectJigwiPair(slot, out victim, out helper);
            CreateJigwiStageGroup(slot, 0, victim, helper, at, 1);
            if (slot == 7) CreateJigwiStageGroup(slot, 1, helper, victim, at, 1);
        }

        private int LeastJigwiAffectedSeat(int slot)
        {
            int selected = (jigwiStageFirstSeat + slot / 2) % activeSeatCount;
            for (int n = 0; n < activeSeatCount; n++)
            {
                int candidate = (jigwiStageFirstSeat + slot / 2 + n) % activeSeatCount;
                if (jigwiStageAffectedCounts[candidate] < jigwiStageAffectedCounts[selected]) selected = candidate;
            }
            return selected;
        }

        private void SelectJigwiPair(int slot, out int victim, out int helper)
        {
            victim = (jigwiStageFirstSeat + slot) % activeSeatCount;
            helper = (victim + 1) % activeSeatCount;
            int best = int.MaxValue;
            for (int n = 0; n < activeSeatCount; n++)
            {
                int a = (jigwiStageFirstSeat + slot + n) % activeSeatCount, b = (a + 1) % activeSeatCount;
                int cost = jigwiStageAffectedCounts[a] + jigwiStageHelperCounts[a]
                    + jigwiStageAffectedCounts[b] + jigwiStageHelperCounts[b];
                if (cost < best) { best = cost; victim = a; helper = b; }
            }
        }

        private void CreateJigwiStageGroup(int slot, int groupIndex, int affectedSeat, int helperSeat,
            double at, int siblings)
        {
            bool ignition = affectedSeat == helperSeat;
            float variant = slot == 4 || slot == 5 ? 18f : slot == 2 || slot == 3 ? -14f : 0f;
            float yaw = siblings == 2 ? (groupIndex == 0 ? -32f : 32f) : variant;
            Vector3 root = JigwiStageSeatDirection(helperSeat, yaw, slot == 4 ? 12f : 4f);
            Vector3 patchCenter = ignition ? root : JigwiStageSeatDirection(affectedSeat, variant, 0f);
            var patches = new Vector3[jigwiStagePatches];
            for (int i = 0; i < patches.Length; i++)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, patchCenter).normalized;
                patches[i] = Quaternion.AngleAxis(i * (slot == 4 ? -14f : 14f), tangent) * patchCenter;
            }
            double activeAt = Math.Min(at + jigwiStageWarning, jigwiStageCutoffAt);
            jigwiStageGroups.Add(new LabJigwiStageGroup
            {
                id = "jigwi-stage/" + sessionId + "/" + round + "/" + slot + "/" + groupIndex,
                slot = slot, pattern = ignition ? LabJigwiStagePattern.Ignition : LabJigwiStagePattern.Curtain,
                affectedPlayer = seats[affectedSeat], helperPlayer = seats[helperSeat],
                warningAt = at, activeAt = activeAt, expiresAt = Math.Min(activeAt + jigwiStageActive, jigwiStageCutoffAt),
                rootDirection = root.normalized, patchDirections = patches,
                rootAngleDegrees = jigwiStageRootAngle, fireAngleDegrees = jigwiStageFireAngle,
                spreadSeconds = jigwiStageSpread
            });
            jigwiStageAffectedCounts[affectedSeat]++;
            jigwiStageHelperCounts[helperSeat]++;
            revision++;
        }

        private Vector3 JigwiStageSeatDirection(int seat, float yawOffset, float pitch)
        {
            float yaw = 360f * seat / activeSeatCount + yawOffset;
            float radians = yaw * Mathf.Deg2Rad, elevation = pitch * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                Mathf.Cos(radians) * Mathf.Cos(elevation));
        }

        private void ResolveJigwiStageGroup(LabJigwiStageGroup group, LabJigwiStageResolution result, double at)
        {
            if (group.resolution != LabJigwiStageResolution.None) return;
            group.resolution = result;
            group.resolvedAt = at;
            revision++;
        }

        private void CancelJigwiStageGroups(double at)
        {
            foreach (var group in jigwiStageGroups)
                ResolveJigwiStageGroup(group, LabJigwiStageResolution.Cancelled, Math.Max(at, group.warningAt));
        }

        private void EndJigwiStageInterference()
        {
            jigwiStageCutoffReached = true;
            double at = jigwiStageCutoffAt;
            CancelJigwiStageGroups(at);
            nextCommonGimmickAt = nextUniqueGimmickAt = nextStageJigwiAt = nextComboAt = nextFlashAt = double.PositiveInfinity;
            if (swipeBlockedUntil > at) swipeBlockedUntil = at;
            comboWarned = flashWarned = comboHeld = comboWasHeld = flashHeld = flashWasHeld = false;
            comboQueuedAt = flashQueuedAt = -1d;
            combo.Stop(at);
            jigwiStates.Clear();
            failedJigwiFires.Clear();
            ClearJigwiGourdBlastEffects();
            foreach (var bomb in orbs.Values.Where(orb => orb.kind == LabOrbKind.Bomb
                && orb.origin == LabOrbOrigin.Gourd && !orb.inFlight).ToArray())
            {
                orbs.Remove(bomb.id);
                launchScales.Remove(bomb.id);
                orbExpiries.Add(new LabOrbExpiry(bomb.owner, bomb.id));
            }
            revision++;
        }
    }
}
