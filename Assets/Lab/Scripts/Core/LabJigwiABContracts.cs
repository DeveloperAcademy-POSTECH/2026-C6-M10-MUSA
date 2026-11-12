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
        /// <summary>#540: which fixed timetable the Host runs (LabJigwiABTimeline.LegacySchedule / FlowSchedule).</summary>
        public int schedule;
        public LabJigwiABRound[] rounds = Array.Empty<LabJigwiABRound>();
        /// <summary>The running mission takes priority over a following mission's overlapping warning.</summary>
        public LabJigwiABRound Active(double now) => (rounds ?? Array.Empty<LabJigwiABRound>())
            .FirstOrDefault(value => value != null && value.Active(now)) ?? Warning(now);
        public LabJigwiABRound Warning(double now) => (rounds ?? Array.Empty<LabJigwiABRound>())
            .FirstOrDefault(value => value != null && value.Warning(now));
        /// <summary>The completed B mission whose half-open launch-reward window contains this battle time.</summary>
        public LabJigwiABRound Reward(double now) => (rounds ?? Array.Empty<LabJigwiABRound>())
            .LastOrDefault(value => value != null && value.RewardActive(now));
        public LabJigwiABSnapshot Copy() => new LabJigwiABSnapshot
        {
            enabled = enabled, fever = fever, startedAt = startedAt, cutoffAt = cutoffAt, schedule = schedule,
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
        public double warningAt, activeAt, endsAt, resolvedAt = -1d;
        public double rewardUntil = -1d;
        public float rewardSizeScale = 1f, rewardPaintScale = 1f;
        /// <summary>Expired B retired all approvals through this Host revision; no orb-ID history is transmitted.</summary>
        public ulong clearedThroughRevision;
        public double shrinkAt;
        public bool shrunk;
        public float shrinkScale, moveAmount, movePeriodSeconds, targetRadius;
        public LabJigwiABPlayerFire[] players = Array.Empty<LabJigwiABPlayerFire>();
        /// <summary>#509 B: synchronized clears so far (0..3), the open one-second window (-1 none) and the last reset.</summary>
        public int syncCount;
        public double syncWindowAt = -1d, syncResetAt = -1d;
        /// <summary>#509 B failure: nobody can press a drum until this battle time (-1 = no board fire).</summary>
        public double boardFireUntil = -1d;
        /// <summary>#551 B countdown: when every picked seat was holding its special Water (-1 = not all ready).</summary>
        public double countdownAt = -1d;
        /// <summary>#551: this B's sync window in seconds (0 = the #509 one second).</summary>
        public float syncWindowSeconds;
        /// <summary>
        /// #551 나무 물통 (개별, 여우구슬처럼 따로따로): every seat gets its own bucket at its own time (seat.readyAt = arrival,
        /// staggered in a random order) and has BucketSeatSeconds to pour it by rocking the phone (launchedAt = when it poured).
        /// No special Water, no 3-2-1. Everyone poured = completed; a seat that runs out gets its board steamed over (client).
        /// </summary>
        public bool bucket;
        /// <summary>
        /// #595 연기: 양동이 없이 모든 자리의 화면이 같은 때(seat.readyAt = activeAt + SmokeLeadSeconds)에 지귀 연기로 덮인다.
        /// bucket과 함께 켜진다(개별 B의 화면 길을 그대로 쓴다). 쏟기는 없고 시간이 다 되면 그냥 끝난다(벌 없음).
        /// </summary>
        public bool smoke;
        /// <summary>#595 공용 부적(A): 모두가 써야 모두 보상, 한 명이라도 못 쓰면 모두의 판이 탄다.</summary>
        public bool common;
        /// <summary>#595: 이 A가 끝났을 때 이 자리 판이 타는지. 공용 부적이면 한 명이라도 못 썼을 때 모두.</summary>
        public bool TalismanFailed(ulong player)
        {
            if (kind != LabJigwiABKind.PersonalBurn) return false;
            bool member = false, anyOpen = false, mineOpen = false;
            foreach (var seat in players ?? Array.Empty<LabJigwiABPlayerFire>())
            {
                if (seat == null) continue;
                if (seat.talismanAt < 0d) anyOpen = true;
                if (seat.playerId == player) { member = true; mineOpen = seat.talismanAt < 0d; }
            }
            return member && (common ? anyOpen : mineOpen);
        }
        /// <summary>#551: 3-2-1 length.</summary>
        public double CountdownLength => LabJigwiABTimeline.CountdownSeconds;
        /// <summary>#551: how long after "던져!" the throw may come.</summary>
        public double PullSeconds => LabJigwiABTimeline.ReleaseSeconds;
        /// <summary>#551: how long after the first picked hit the others may land.</summary>
        public double SyncWindow => syncWindowSeconds > 0f ? syncWindowSeconds : LabJigwiABTimeline.GroupSyncWindowSeconds;
        /// <summary>#551: the "지금!" moment (-1 while the 3-2-1 has not started).</summary>
        public double GoAt => countdownAt < 0d ? -1d : countdownAt + CountdownLength;
        public bool Warning(double now) => resolution == LabJigwiABResolution.None && now >= warningAt && now < activeAt;
        public bool Active(double now) => resolution == LabJigwiABResolution.None && now >= activeAt && now < endsAt;
        public bool RewardActive(double now) => kind == LabJigwiABKind.GroupFire
            && resolution == LabJigwiABResolution.Completed && now >= resolvedAt && now < rewardUntil;
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
        /// <summary>#479 A only: when this player finished writing the talisman; -1 while still burning.</summary>
        public double talismanAt = -1d;
        /// <summary>#551 B only: since when this picked seat holds its special Water (-1 = not holding); when it threw it.</summary>
        public double readyAt = -1d, launchedAt = -1d;
        public LabJigwiABFireTarget[] targets = Array.Empty<LabJigwiABFireTarget>();
        public LabJigwiABPlayerFire Copy() => new LabJigwiABPlayerFire
        {
            playerId = playerId, lastWaterSplashAt = lastWaterSplashAt, talismanAt = talismanAt, readyAt = readyAt, launchedAt = launchedAt,
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
        public const int MaximumRounds = LabJigwiABTimeline.SlotCount, MaximumPlayers = 5, MaximumFires = 6;
        public static LabJigwiABSnapshot Copy(LabJigwiABSnapshot state) => state?.Copy() ?? new LabJigwiABSnapshot();
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        /// <summary>#663: when a mission's time is really over — its end, or earlier if it was resolved before that.</summary>
        private static double MissionUntil(LabJigwiABRound round) => round.resolution != LabJigwiABResolution.None
            && round.resolvedAt >= 0d ? Math.Min(round.endsAt, round.resolvedAt) : round.endsAt;
        private static bool Range(double value, double min, double max) => Finite(value) && value >= min && value <= max;
        public static bool Valid(LabJigwiABSnapshot state, LabPlayerState[] members = null, ulong? modelRevision = null)
        {
            if (state == null) return true; // Legacy/standalone snapshots have no AB payload.
            if (state.rounds == null || state.rounds.Length > MaximumRounds || !LabJigwiABTimeline.ValidSchedule(state.schedule)) return false;
            if (!state.enabled) return !state.fever && state.rounds.Length == 0 && state.startedAt == -1d && state.cutoffAt == -1d;
            if (state.rounds.Length > LabJigwiABTimeline.SlotCountFor(state.schedule)) return false;
            if (!Range(state.startedAt, 0d, double.MaxValue) || !Finite(state.cutoffAt)
                || Math.Abs(state.cutoffAt - state.startedAt - 100d) > .00001d) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<int>();
            var memberIds = members == null ? null : new HashSet<ulong>(members.Where(p => p != null).Select(p => p.id));
            int unresolved = 0;
            var intervals = new List<LabJigwiABRound>();
            foreach (var round in state.rounds)
            {
                if (round == null || string.IsNullOrEmpty(round.id) || round.id.Length > 256 || !ids.Add(round.id)
                    || round.slot < 0 || round.slot >= LabJigwiABTimeline.SlotCountFor(state.schedule) || !slots.Add(round.slot)
                    || round.kind < LabJigwiABKind.PersonalBurn || round.kind > LabJigwiABKind.GroupFire
                    || round.resolution < LabJigwiABResolution.None || round.resolution > LabJigwiABResolution.Skipped
                    || !Range(round.warningAt, state.startedAt, state.cutoffAt)
                    || !Range(round.activeAt, round.warningAt, state.cutoffAt)
                    || !Range(round.endsAt, round.activeAt, state.cutoffAt)
                    || !Range(round.resolvedAt, -1d, state.cutoffAt)
                    || !Range(round.shrinkScale, .2d, 1d) || !Range(round.moveAmount, 0d, .100001d)
                    || !Range(round.movePeriodSeconds, 1d, 20d) || !Range(round.targetRadius, .099999d, 1d)
                    || !Finite(round.shrinkAt) || round.shrunk || round.shrinkScale != 1f
                    || !Range(round.rewardSizeScale, 1d, 3d) || !Range(round.rewardPaintScale, 1d, 3d)
                    || !Range(round.rewardUntil, -1d, state.cutoffAt)
                    || round.players == null || round.players.Length > MaximumPlayers) return false;
                double scheduled = state.startedAt + LabJigwiABTimeline.ActiveOffset(round.slot, state.schedule);
                // #663: in the smoke flow a talisman may start earlier than its slot (right after the smoke before it).
                bool pulled = LabJigwiABTimeline.IsSmokeFlow(state.schedule) && round.kind == LabJigwiABKind.PersonalBurn
                    && round.activeAt < scheduled - .00001d;
                if (!pulled && Math.Abs(round.activeAt - scheduled) > .00001d
                    || Math.Abs(round.warningAt - (round.activeAt - LabJigwiABTimeline.WarningSeconds)) > .00001d
                    || round.kind != LabJigwiABTimeline.KindForSlot(round.slot, state.schedule)) return false;
                if (round.resolution != LabJigwiABResolution.Skipped)
                {
                    // #663: a mission that ended early (smoke wiped away) frees its time from that moment, so the pulled talisman fits.
                    if (intervals.Any(other => round.activeAt < MissionUntil(other) && other.activeAt < MissionUntil(round))) return false;
                    intervals.Add(round);
                }
                bool failed = round.kind == LabJigwiABKind.GroupFire && round.resolution == LabJigwiABResolution.Expired;
                // #509: a failed B no longer wipes boards (no purge fence) and success has no timed launch reward.
                if (round.clearedThroughRevision != 0
                    || modelRevision.HasValue && round.clearedThroughRevision > modelRevision.Value) return false;
                if (round.rewardUntil != -1d) return false;
                // #551: a failed B may have no board fire at all (the punishment is off in the asset).
                if (failed ? round.boardFireUntil != -1d && !Range(round.boardFireUntil, round.resolvedAt, round.resolvedAt + 10d)
                    : round.boardFireUntil != -1d) return false;
                if (round.resolution == LabJigwiABResolution.None)
                {
                    // A may still be running during the next B's one-second warning.
                    if (round.resolvedAt != -1d || ++unresolved > 2) return false;
                }
                else if (round.resolvedAt < round.warningAt) return false;
                if (round.kind == LabJigwiABKind.PersonalBurn && (round.bucket || round.smoke)) return false;
                // #595: smoke rides on the bucket B; only an A can be a common talisman.
                if (round.smoke && !round.bucket || round.kind == LabJigwiABKind.GroupFire && round.common) return false;
                if (round.kind == LabJigwiABKind.PersonalBurn
                    && (round.syncCount != 0 || round.syncWindowAt != -1d || round.syncResetAt != -1d)) return false;
                if (round.kind == LabJigwiABKind.PersonalBurn)
                {
                    // #479: A ignites every seat; each seat only records when it finished its talisman.
                    if (round.rewardSizeScale != 1f || round.rewardPaintScale != 1f
                        || round.endsAt - round.activeAt < .99999d || round.endsAt - round.activeAt > 15.00001d) return false;
                    var seats = new HashSet<ulong>();
                    foreach (var player in round.players)
                        if (player == null || !seats.Add(player.playerId) || memberIds != null && !memberIds.Contains(player.playerId)
                            || player.targets == null || player.targets.Length != 0 || player.lastWaterSplashAt != -1d
                            || player.talismanAt != -1d && !Range(player.talismanAt, round.activeAt, round.endsAt)) return false;
                    if (round.resolution != LabJigwiABResolution.Skipped && round.players.Length == 0
                        || memberIds != null && round.resolution != LabJigwiABResolution.Skipped && !seats.SetEquals(memberIds)) return false;
                    bool everyoneWrote = round.players.Length > 0 && round.players.All(player => player.talismanAt >= 0d);
                    if (round.resolution == LabJigwiABResolution.Completed && !everyoneWrote
                        || round.resolution == LabJigwiABResolution.None && everyoneWrote) return false;
                    continue;
                }
                if (round.shrinkAt < round.activeAt || round.players.Any(player => player != null && player.talismanAt != -1d)
                    || Math.Abs(round.endsAt - Math.Min(state.cutoffAt, round.activeAt + LabJigwiABTimeline.GroupMissionSeconds)) > .00001d
                    || round.resolution != LabJigwiABResolution.Skipped && round.players.Length == 0) return false;
                // #509 sync progress: 0..2 while running/failed, exactly 3 once completed; a window only while running.
                bool completed = round.resolution == LabJigwiABResolution.Completed;
                if (round.syncCount < 0 || round.syncCount > LabJigwiABTimeline.GroupSyncRequired
                    || completed != (round.syncCount == LabJigwiABTimeline.GroupSyncRequired)
                    || round.syncWindowAt != -1d && (round.resolution != LabJigwiABResolution.None
                        || !Range(round.syncWindowAt, round.activeAt, round.endsAt))
                    || round.syncResetAt != -1d && !Range(round.syncResetAt, round.activeAt,
                        round.endsAt + round.SyncWindow)) return false;
                // #551 countdown: a start time only inside the B, a known window length.
                if (round.countdownAt != -1d && !Range(round.countdownAt, round.activeAt, round.endsAt)
                    || round.syncWindowSeconds != 0f && !Range(round.syncWindowSeconds, .5d, 3d)) return false;
                var owners = new HashSet<ulong>();
                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var player in round.players)
                {
                    if (player == null || !owners.Add(player.playerId) || memberIds != null && !memberIds.Contains(player.playerId)
                        || player.lastWaterSplashAt != -1d && !Range(player.lastWaterSplashAt, round.activeAt, round.endsAt)
                        || player.readyAt != -1d && !Range(player.readyAt, round.activeAt, round.endsAt)
                        || player.launchedAt != -1d && !Range(player.launchedAt, round.activeAt, round.endsAt)
                        || player.targets == null || player.targets.Length < 1 || player.targets.Length > MaximumFires) return false;
                    foreach (var target in player.targets)
                    {
                        if (target == null || string.IsNullOrEmpty(target.id) || target.id.Length > 256 || !targets.Add(target.id)
                            || !Range(target.x, .1d, .9d) || !Range(target.y, .48d, .86d)
                            || target.relightAt != -1d || target.lit == completed) return false;
                        // A hit time exists only inside the open window.
                        if (target.extinguishedAt != -1d && (round.syncWindowAt < 0d
                            || target.extinguishedAt < round.syncWindowAt
                            || target.extinguishedAt >= round.syncWindowAt + round.SyncWindow)) return false;
                    }
                }
                // #509: only the picked seats are listed (helpers are everyone else).
                if (memberIds != null && round.resolution != LabJigwiABResolution.Skipped
                    && owners.Count != (round.bucket ? memberIds.Count : LabJigwiABTimeline.GroupPickCount(memberIds.Count))) return false;
                // #551 나무 물통: no 3-2-1; each bucket arrives inside the stagger and is poured inside its own time.
                // #595 연기: everyone's screen smokes at the same moment and nobody pours.
                double lead = round.smoke ? LabJigwiABTimeline.SmokeLeadSeconds : LabJigwiABTimeline.BucketLeadSeconds;
                double spread = round.smoke ? 0d : LabJigwiABTimeline.BucketStaggerSeconds * (MaximumPlayers - 1);
                if (round.smoke && (round.resolution == LabJigwiABResolution.Completed || round.players.Any(player => player.launchedAt != -1d)))
                    return false;
                if (round.bucket && (round.countdownAt != -1d
                    || round.resolution == LabJigwiABResolution.Completed && round.players.Any(player => player.launchedAt < 0d)
                    || round.players.Any(player => !Range(player.readyAt, round.activeAt + lead - .00001d,
                        round.activeAt + lead + spread + .00001d)
                        || player.launchedAt != -1d && !Range(player.launchedAt, player.readyAt - .00001d,
                            player.readyAt + LabJigwiABTimeline.BucketSeatSeconds + .00001d)))) return false;
                bool allOff = round.players.Length > 0 && round.players.All(player => player.targets.All(target => !target.lit));
                if (round.resolution == LabJigwiABResolution.Completed && !allOff
                    || (round.resolution == LabJigwiABResolution.None || failed) && allOff) return false;
            }
            var pending = state.rounds.Where(value => value.resolution == LabJigwiABResolution.None)
                .OrderBy(value => value.activeAt).ToArray();
            // Either kind may still run during the next round's one-second warning (#509: B2 runs into A3's).
            if (pending.Length == 2 && (pending[0].kind == pending[1].kind || pending[1].slot != pending[0].slot + 1
                || pending[0].endsAt <= pending[1].warningAt || pending[0].endsAt > pending[1].activeAt)) return false;
            return true;
        }
    }
}
