using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public enum LabJigwiStagePattern { Ignition, Curtain }
    public enum LabJigwiStageResolution { None, Extinguished, Expired, Cancelled }

    /// <summary>One independent root and the affected player's finite surface patches.</summary>
    [Serializable]
    public sealed class LabJigwiStageGroup
    {
        public string id;
        public int slot;
        public LabJigwiStagePattern pattern;
        public ulong affectedPlayer, helperPlayer;
        public double warningAt, activeAt, expiresAt, resolvedAt = -1d;
        public LabJigwiStageResolution resolution;
        public Vector3 rootDirection;
        public Vector3[] patchDirections = Array.Empty<Vector3>();
        public float rootAngleDegrees, fireAngleDegrees, spreadSeconds;
        public bool geometryConfigured;

        public bool Warning(double now) => resolution == LabJigwiStageResolution.None
            && now >= warningAt && now < activeAt && now < expiresAt;
        public bool Active(double now) => resolution == LabJigwiStageResolution.None
            && now >= activeAt && now < expiresAt;
        public int VisiblePatchCount(double now)
        {
            if (!geometryConfigured || !Active(now) || patchDirections == null) return 0;
            return Math.Min(patchDirections.Length, 1 + (int)Math.Floor((now - activeAt) / spreadSeconds));
        }
        public bool ContainsRoot(ulong player, Vector3 direction, double now) => player == helperPlayer
            && geometryConfigured && Active(now) && Contains(rootDirection, direction, rootAngleDegrees);
        public bool Blocks(ulong player, Vector3 direction, double now)
        {
            if (!geometryConfigured || player != affectedPlayer) return false;
            int count = VisiblePatchCount(now);
            for (int i = 0; i < count; i++)
                if (Contains(patchDirections[i], direction, fireAngleDegrees)) return true;
            return false;
        }
        private static bool Contains(Vector3 center, Vector3 direction, float angle)
        {
            if (direction.sqrMagnitude < .000001f || !LabJigwiStageNetworkValidation.Direction(direction, false)) return false;
            return Vector3.Dot(center, direction.normalized) >= Mathf.Cos(angle * Mathf.Deg2Rad);
        }
        public LabJigwiStageGroup Copy()
        {
            var copy = (LabJigwiStageGroup)MemberwiseClone();
            copy.patchDirections = patchDirections == null ? null : (Vector3[])patchDirections.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class LabJigwiStageSnapshot
    {
        public bool enabled, fever;
        public double startedAt = -1d, cutoffAt = -1d;
        public LabJigwiStageGroup[] groups = Array.Empty<LabJigwiStageGroup>();
        public LabJigwiStageSnapshot Copy() => new LabJigwiStageSnapshot
        {
            enabled = enabled, fever = fever, startedAt = startedAt, cutoffAt = cutoffAt,
            groups = groups == null ? null : groups.Select(group => group?.Copy()).ToArray()
        };
    }

    /// <summary>Bounded, finite metadata. Null denotes the feature absent in legacy/non-Jigwi fixtures.</summary>
    public static class LabJigwiStageNetworkValidation
    {
        public const int MaximumGroups = 10;
        public const int MaximumPatches = 3;
        public static LabJigwiStageSnapshot Copy(LabJigwiStageSnapshot state) =>
            state?.Copy() ?? new LabJigwiStageSnapshot();
        public static bool Valid(LabJigwiStageSnapshot state, LabPlayerState[] players = null)
        {
            if (state == null) return true;
            if (state.groups == null || state.groups.Length > MaximumGroups) return false;
            if (!state.enabled) return !state.fever && state.groups.Length == 0
                && state.startedAt == -1d && state.cutoffAt == -1d;
            if (!Finite(state.startedAt) || state.startedAt < 0d || !Finite(state.cutoffAt)
                || Math.Abs(state.cutoffAt - state.startedAt - 100d) > .001d) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<ulong> members = players == null ? null : new HashSet<ulong>(players.Where(p => p != null).Select(p => p.id));
            foreach (var group in state.groups)
            {
                if (group == null || string.IsNullOrEmpty(group.id) || group.id.Length > 160 || !ids.Add(group.id)
                    || group.slot < 0 || group.slot > 7 || group.pattern < LabJigwiStagePattern.Ignition
                    || group.pattern > LabJigwiStagePattern.Curtain
                    || group.resolution < LabJigwiStageResolution.None || group.resolution > LabJigwiStageResolution.Cancelled
                    || !Finite(group.warningAt) || !Finite(group.activeAt) || !Finite(group.expiresAt)
                    || group.warningAt < state.startedAt || group.warningAt >= state.cutoffAt
                    || group.activeAt < group.warningAt || group.activeAt > state.cutoffAt
                    || group.expiresAt < group.activeAt || group.expiresAt > state.cutoffAt
                    || !Range(group.rootAngleDegrees, .1f, 80f) || !Range(group.fireAngleDegrees, .1f, 89f)
                    || !Range(group.spreadSeconds, .01f, 120f) || !Direction(group.rootDirection, true)
                    || group.patchDirections == null || group.patchDirections.Length < 1
                    || group.patchDirections.Length > MaximumPatches
                    || group.patchDirections.Any(p => !Direction(p, true))) return false;
                if (group.pattern == LabJigwiStagePattern.Ignition && group.affectedPlayer != group.helperPlayer
                    || group.pattern == LabJigwiStagePattern.Curtain && group.affectedPlayer == group.helperPlayer) return false;
                if (members != null && (!members.Contains(group.affectedPlayer) || !members.Contains(group.helperPlayer))) return false;
                if (group.resolution == LabJigwiStageResolution.None)
                {
                    if (group.resolvedAt != -1d) return false;
                }
                else if (!Finite(group.resolvedAt) || group.resolvedAt < group.warningAt
                    || group.resolvedAt > state.cutoffAt) return false;
            }
            return true;
        }
        internal static bool Direction(Vector3 value, bool unit)
        {
            float square = value.sqrMagnitude;
            return Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(square)
                && square > .000001f && (!unit || Math.Abs(square - 1f) < .002f);
        }
        private static bool Range(float value, float low, float high) => Finite(value) && value >= low && value <= high;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
