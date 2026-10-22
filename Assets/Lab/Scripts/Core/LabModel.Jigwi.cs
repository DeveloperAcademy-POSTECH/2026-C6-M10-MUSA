using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        public const int MaximumJigwiSuppliesPerPlayer = 64;
        private readonly Dictionary<ulong, LabJigwiState> jigwiStates = new Dictionary<ulong, LabJigwiState>();
        private readonly HashSet<string> failedJigwiFires = new HashSet<string>(StringComparer.Ordinal);
        private uint jigwiFireCount;
        private int jigwiFireTargets;
        private float jigwiFireSeconds, jigwiOverheatSeconds, jigwiGenerationBurnSeconds,
            jigwiRawRadiusScale, jigwiRawLifeScale;

        private void ResetJigwi(LabConfig config)
        {
            jigwiStates.Clear();
            failedJigwiFires.Clear();
            jigwiFireCount = 0;
            jigwiFireTargets = config.JigwiFireTargetCount;
            jigwiFireSeconds = config.JigwiFireSeconds;
            jigwiOverheatSeconds = config.JigwiOverheatSeconds;
            jigwiGenerationBurnSeconds = config.JigwiGenerationBurnSeconds;
            jigwiRawRadiusScale = config.JigwiRawRadiusScale;
            jigwiRawLifeScale = config.JigwiRawLifeScale;
        }

        private LabJigwiState JigwiStateFor(ulong player)
        {
            if (!jigwiStates.TryGetValue(player, out var state))
            {
                state = new LabJigwiState { playerId = player };
                jigwiStates.Add(player, state);
            }
            return state;
        }

        private LabJigwiState[] CopyJigwiStates() => jigwiStates.Values
            .OrderBy(state => players[state.playerId].seat).Select(state => state.Copy()).ToArray();

        public bool JigwiOverheated(ulong player, double now) => phase == LabPhase.Playing
            && jigwiStates.TryGetValue(player, out var state) && state.Overheated(now);

        private bool JigwiBlocksSabotage(ulong player, double at)
        {
            if (!jigwiStates.TryGetValue(player, out var state)) return false;
            if (state.FireActive(at) || state.Overheated(at)) return true;
            // A late Tick draws historical sabotages before AdvanceJigwi latches the failed fire.
            // Check the same deadline here so that frame size cannot allow a charm during overheat.
            return state.fireStartedAt >= 0d && at >= state.fireEndsAt
                && at < state.fireEndsAt + jigwiOverheatSeconds
                && state.targets.Any(target => !target.extinguished);
        }

        private bool CanStartJigwi(ulong player, double now, out string reason)
        {
            if (!TryAdvance(now, out reason)) return false;
            return CanStartJigwiAt(player, now, out reason);
        }

        private bool CanStartJigwiAt(ulong player, double now, out string reason)
        {
            if (phase != LabPhase.Playing) return Reject("BATTLE_NOT_PLAYING", out reason);
            if (StageYokai != LabStageYokai.None && StageYokai != LabStageYokai.Jigwi)
                return Reject("STAGE_PATTERN_NOT_ALLOWED", out reason);
            if (!players.ContainsKey(player)) return Reject("UNKNOWN_PLAYER", out reason);
            if (Charmed(player, now)) return Reject("CHARMED", out reason);
            if (jigwiStates.TryGetValue(player, out var state)
                && (state.FireActive(now) || state.Overheated(now) || state.GenerationBurnActive(now)))
                return Reject("JIGWI_ALREADY_ACTIVE", out reason);
            reason = null;
            return true;
        }

        /// <summary>Manual DEV trigger for standalone Lab or Jigwi stages, separate from the automatic timer.</summary>
        public bool TryStartJigwiFire(ulong player, double now, out string reason)
        {
            if (!CanStartJigwi(player, now, out reason)) return false;
            return StartJigwiFireCore(player, now, out reason);
        }

        private bool StartJigwiFireCore(ulong player, double now, out string reason)
        {
            int supplyCount = orbs.Values.Count(orb => orb.owner == player && orb.jigwiSupply);
            if (supplyCount + jigwiFireTargets + 2 > MaximumJigwiSuppliesPerPlayer)
                return Reject("JIGWI_SUPPLY_LIMIT", out reason);
            var state = JigwiStateFor(player);
            state.fireId = "jigwi/" + round + "/" + (++jigwiFireCount);
            state.fireStartedAt = now;
            state.fireEndsAt = now + jigwiFireSeconds;
            state.overheatUntil = -1d;
            state.targets = new LabJigwiFireTarget[jigwiFireTargets];
            for (int i = 0; i < state.targets.Length; i++)
            {
                // The client maps this normalized grid onto the visible battle plane.
                int count = state.targets.Length;
                float x = count == 1 ? .5f : count <= 3 ? (i + 1f) / (count + 1f)
                    : .25f + .25f * (i % 3);
                float y = count <= 3 ? .5f : i < 3 ? .65f : .35f;
                state.targets[i] = new LabJigwiFireTarget
                {
                    id = state.fireId + "/target/" + i, x = x, y = y
                };
            }
            for (int i = 0; i < jigwiFireTargets + 2; i++)
            {
                var supply = new LabOrbState
                {
                    id = NewOrbId(), owner = player, kind = LabOrbKind.Combined, element = LabOrbElement.Water,
                    origin = LabOrbOrigin.Gourd, originOwner = player,
                    x = (i + 1f) / (jigwiFireTargets + 3f), y = .25f + .08f * (i % 2),
                    createdAt = now, expiresAt = Math.Max(now + rules.OrbLifetimeSeconds, state.fireEndsAt),
                    jigwiSupply = true, revision = ++revision
                };
                orbs.Add(supply.id, supply);
            }
            revision++;
            reason = "JIGWI_FIRE_STARTED";
            return true;
        }

        public bool TryStartJigwiGenerationBurn(ulong player, double now, out string reason)
        {
            if (!CanStartJigwi(player, now, out reason)) return false;
            return StartJigwiGenerationBurnCore(player, now, out reason);
        }

        private bool StartJigwiGenerationBurnCore(ulong player, double now, out string reason)
        {
            JigwiStateFor(player).generationBurnUntil = now + jigwiGenerationBurnSeconds;
            revision++;
            reason = "JIGWI_GENERATION_BURN_STARTED";
            return true;
        }

        private void ApplyJigwiGenerationBurn(LabOrbState orb, double now)
        {
            if (!orb.kind.IsRaw() || !jigwiStates.TryGetValue(orb.owner, out var state)
                || !state.GenerationBurnActive(now)) return;
            orb.jigwiGenerationBurn = true;
            orb.jigwiRadiusScale = jigwiRawRadiusScale;
            orb.expiresAt = now + rules.OrbLifetimeSeconds * jigwiRawLifeScale;
        }

        private void AdvanceJigwi(double now)
        {
            foreach (var state in jigwiStates.Values)
            {
                if (state.fireStartedAt < 0d || now < state.fireEndsAt
                    || failedJigwiFires.Contains(state.fireId)
                    || state.targets.All(target => target.extinguished)) continue;
                failedJigwiFires.Add(state.fireId);
                state.overheatUntil = state.fireEndsAt + jigwiOverheatSeconds;
                revision++;
            }
        }

        private void RemoveJigwiBurned(string id)
        {
            if (!orbs.Remove(id)) return;
            launchScales.Remove(id);
            revision++;
        }

        /// <summary>Consume the owned Idle orbs explicitly released while overheated. Drums never burn.</summary>
        public bool TryJigwiBurn(ulong sender, string first, string secondOrNull, string requestId,
            double now, out string reason)
        {
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!JigwiOverheated(sender, now)) return Reject("JIGWI_NOT_OVERHEATED", out reason);
            if (string.IsNullOrEmpty(first) || !orbs.TryGetValue(first, out var one)
                || !UsableBy(one, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            bool hasSecond = !string.IsNullOrEmpty(secondOrNull);
            if (hasSecond && (first == secondOrNull
                || !orbs.TryGetValue(secondOrNull, out var two) || !UsableBy(two, sender)))
                return Reject("NOT_OWNED_IDLE", out reason);
            RemoveJigwiBurned(first);
            if (hasSecond) RemoveJigwiBurned(secondOrNull);
            reason = "JIGWI_BURNED";
            return true;
        }

        /// <summary>
        /// One Host projectile hit a fire collider. Every valid shot is consumed here; only a Water
        /// Combined extinguishes it. A handled fire hit never applies gourd paint, HP or hit rewards.
        /// </summary>
        public bool ConfirmJigwiFireHit(string projectileOrbId, string fireId, string fireTargetId,
            double now, out bool extinguished)
        {
            extinguished = false;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing
                || !orbs.TryGetValue(projectileOrbId ?? string.Empty, out var orb) || !orb.inFlight
                || !jigwiStates.TryGetValue(orb.owner, out var state)
                || !state.FireActive(now) || state.fireId != fireId) return false;
            var target = state.targets.FirstOrDefault(candidate => candidate.id == fireTargetId && !candidate.extinguished);
            if (target == null) return false;
            RemoveJigwiBurned(projectileOrbId);
            if (orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water)
            {
                target.extinguished = extinguished = true;
                revision++;
            }
            return true;
        }
    }
}
