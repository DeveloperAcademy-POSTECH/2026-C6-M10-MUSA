using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public enum LabJigwiABKind { PersonalBurn, GroupFire }
    public enum LabJigwiABResolution { None, Completed, Expired, Cancelled, Skipped }

    [Serializable]
    public sealed class LabJigwiABSnapshot
    {
        public bool enabled, fever;
        public double startedAt = -1d, cutoffAt = -1d;
        public LabJigwiABRound[] rounds = Array.Empty<LabJigwiABRound>();
        public LabJigwiABRound Active(double now) => (rounds ?? Array.Empty<LabJigwiABRound>())
            .FirstOrDefault(value => value != null && value.resolution == LabJigwiABResolution.None
                && now >= value.warningAt && now < value.endsAt);
        public LabJigwiABSnapshot Copy() => new LabJigwiABSnapshot
        {
            enabled = enabled, fever = fever, startedAt = startedAt, cutoffAt = cutoffAt,
            rounds = (rounds ?? Array.Empty<LabJigwiABRound>()).Select(value => value?.Copy()).ToArray()
        };
    }

    [Serializable]
    public sealed class LabJigwiABRound
    {
        public string id;
        public int slot;
        public LabJigwiABKind kind;
        public LabJigwiABResolution resolution;
        public ulong targetPlayer;
        public double warningAt, activeAt, endsAt, resolvedAt = -1d;
        public int hitCount, requiredHits;
        public double shrinkAt;
        public bool shrunk;
        public float shrinkScale, moveAmount, movePeriodSeconds, targetRadius;
        public LabJigwiABPlayerFire[] players = Array.Empty<LabJigwiABPlayerFire>();
        public bool Warning(double now) => resolution == LabJigwiABResolution.None && now >= warningAt && now < activeAt;
        public bool Active(double now) => resolution == LabJigwiABResolution.None && now >= activeAt && now < endsAt;
        public LabJigwiABRound Copy()
        {
            var copy = (LabJigwiABRound)MemberwiseClone();
            copy.players = (players ?? Array.Empty<LabJigwiABPlayerFire>()).Select(value => value?.Copy()).ToArray();
            return copy;
        }
    }

    [Serializable]
    public sealed class LabJigwiABPlayerFire
    {
        public ulong playerId;
        // A real Water impact can splash against the curtain without extinguishing an ignition.
        public double lastWaterSplashAt = -1d;
        public LabJigwiABFireTarget[] targets = Array.Empty<LabJigwiABFireTarget>();
        public LabJigwiABPlayerFire Copy() => new LabJigwiABPlayerFire
        {
            playerId = playerId, lastWaterSplashAt = lastWaterSplashAt,
            targets = (targets ?? Array.Empty<LabJigwiABFireTarget>()).Select(value => value?.Copy()).ToArray()
        };
    }

    [Serializable]
    public sealed class LabJigwiABFireTarget
    {
        public string id;
        public float x, y;
        public double extinguishedAt = -1d, relightAt = -1d;
        public bool lit = true;
        public LabJigwiABFireTarget Copy() => (LabJigwiABFireTarget)MemberwiseClone();
    }

    /// <summary>UI and Host collision anchors sample the same battle-clock motion.</summary>
    public static class LabJigwiABFireMotion
    {
        /// <summary>Derived owner/round identity for the persistent curtain; never an extinguishable ignition ID.</summary>
        public static string CurtainId(string roundId, ulong owner) => roundId + "/" + owner + "/curtain";

        public static Vector2 Position(LabJigwiABRound round, LabJigwiABFireTarget target, int index, double now)
        {
            if (round == null || target == null) return Vector2.one * .5f;
            double elapsed = Math.Max(0d, now - round.activeAt);
            float phase = (float)(elapsed / Math.Max(1f, round.movePeriodSeconds) * Math.PI * 2d) + index * 2.3999632f;
            return new Vector2(Mathf.Clamp(target.x + Mathf.Sin(phase) * round.moveAmount, .1f, .9f),
                Mathf.Clamp(target.y + Mathf.Cos(phase * .83f + index) * round.moveAmount * .55f, .48f, .86f));
        }
    }

    public static class LabJigwiABNetworkValidation
    {
        public const int MaximumRounds = 8, MaximumPlayers = 5, MaximumFires = 6;
        public static LabJigwiABSnapshot Copy(LabJigwiABSnapshot state) => state?.Copy() ?? new LabJigwiABSnapshot();
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Range(double value, double min, double max) => Finite(value) && value >= min && value <= max;
        public static bool Valid(LabJigwiABSnapshot state, LabPlayerState[] members = null)
        {
            if (state == null) return true; // Legacy/standalone snapshots have no AB payload.
            if (state.rounds == null || state.rounds.Length > MaximumRounds) return false;
            if (!state.enabled) return !state.fever && state.rounds.Length == 0 && state.startedAt == -1d && state.cutoffAt == -1d;
            if (!Range(state.startedAt, 0d, double.MaxValue) || !Finite(state.cutoffAt)
                || Math.Abs(state.cutoffAt - state.startedAt - 100d) > .00001d) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<int>();
            var memberIds = members == null ? null : new HashSet<ulong>(members.Where(p => p != null).Select(p => p.id));
            int unresolved = 0;
            foreach (var round in state.rounds)
            {
                if (round == null || string.IsNullOrEmpty(round.id) || round.id.Length > 256 || !ids.Add(round.id)
                    || round.slot < 0 || round.slot >= MaximumRounds || !slots.Add(round.slot)
                    || round.kind < LabJigwiABKind.PersonalBurn || round.kind > LabJigwiABKind.GroupFire
                    || round.resolution < LabJigwiABResolution.None || round.resolution > LabJigwiABResolution.Skipped
                    || !Range(round.warningAt, state.startedAt, state.cutoffAt)
                    || !Range(round.activeAt, round.warningAt, state.cutoffAt)
                    || !Range(round.endsAt, round.activeAt, state.cutoffAt)
                    || !Range(round.resolvedAt, -1d, state.cutoffAt)
                    || !Range(round.shrinkScale, .2d, 1d) || !Range(round.moveAmount, 0d, .100001d)
                    || !Range(round.movePeriodSeconds, 1d, 20d) || !Range(round.targetRadius, .099999d, 1d)
                    || !Finite(round.shrinkAt) || round.players == null || round.players.Length > MaximumPlayers) return false;
                if (round.resolution == LabJigwiABResolution.None)
                {
                    if (round.resolvedAt != -1d || ++unresolved > 1) return false;
                }
                else if (round.resolvedAt < round.warningAt) return false;
                if (round.kind == LabJigwiABKind.PersonalBurn)
                {
                    if (round.requiredHits < 1 || round.requiredHits > 10 || round.hitCount < 0 || round.hitCount > round.requiredHits
                        || round.players.Length != 0 || round.shrunk || memberIds != null && !memberIds.Contains(round.targetPlayer)) return false;
                    if (round.resolution == LabJigwiABResolution.Completed && round.hitCount != round.requiredHits) return false;
                    continue;
                }
                if (round.requiredHits != 0 || round.hitCount != 0 || round.shrinkAt < round.activeAt
                    || round.resolution != LabJigwiABResolution.Skipped && round.players.Length == 0) return false;
                var owners = new HashSet<ulong>();
                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var player in round.players)
                {
                    if (player == null || !owners.Add(player.playerId) || memberIds != null && !memberIds.Contains(player.playerId)
                        || player.lastWaterSplashAt != -1d && !Range(player.lastWaterSplashAt, round.activeAt, state.cutoffAt)
                        || player.targets == null || player.targets.Length < 1 || player.targets.Length > MaximumFires) return false;
                    foreach (var target in player.targets)
                    {
                        if (target == null || string.IsNullOrEmpty(target.id) || target.id.Length > 256 || !targets.Add(target.id)
                            || !Range(target.x, .1d, .9d) || !Range(target.y, .48d, .86d)
                            || !Range(target.extinguishedAt, -1d, state.cutoffAt) || !Finite(target.relightAt)
                            || target.relightAt < -1d) return false;
                        if (target.extinguishedAt < 0d)
                        {
                            if (target.extinguishedAt != -1d || target.relightAt != -1d || !target.lit) return false;
                        }
                        else if (target.extinguishedAt < round.activeAt || target.relightAt <= target.extinguishedAt
                            || target.relightAt - target.extinguishedAt > 60.00001d) return false;
                    }
                }
                if (memberIds != null && round.resolution != LabJigwiABResolution.Skipped && !owners.SetEquals(memberIds)) return false;
                bool allOff = round.players.Length > 0 && round.players.All(player => player.targets.All(target => !target.lit));
                if (round.resolution == LabJigwiABResolution.Completed && !allOff
                    || round.resolution == LabJigwiABResolution.None && allOff) return false;
            }
            return true;
        }
    }
}
