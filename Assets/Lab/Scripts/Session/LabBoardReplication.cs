using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>Small board/control update. Paint, target HP and paint victory never travel here.</summary>
    [Serializable]
    public sealed class LabBoardFrame
    {
        public string sessionId;
        public uint round;
        public long sequence;
        public ulong localOwner;
        public bool checkpoint;
        public double serverTime;
        public ulong modelRevision;
        public LabOrbState[] states = Array.Empty<LabOrbState>();
        public string[] removedIds = Array.Empty<string>();
        public LabPlayerState[] players = Array.Empty<LabPlayerState>();
        public LabPhase phase;
        public bool paused;
        public double remaining, playedSeconds;
        public ulong swipeBlockedPlayer;
        public bool swipeBlockedRight;
        public double swipeBlockStartedAt = -1d, swipeBlockedUntil = -1d;
        /// <summary>The charm (#251) travels with the board header so the charmed player and those beside see it at once.</summary>
        public ulong charmedPlayer;
        public double charmStartedAt = -1d, charmUntil = -1d;
        public int charmTaps;
        public float targetYawDegrees, targetYawSpeedDegreesPerSecond;
        public float targetRopeCycleElapsed, targetRopeHitOffset, targetRopeHitVelocity;

        internal LabBoardFrame CopyHeader()
        {
            var copy = (LabBoardFrame)MemberwiseClone();
            copy.states = Array.Empty<LabOrbState>();
            copy.removedIds = Array.Empty<string>();
            copy.players = Array.Empty<LabPlayerState>();
            return copy;
        }
    }

    /// <summary>
    /// Host per-recipient metadata delta cache. Local positions, velocities and pose revisions do
    /// not generate network updates; newly created/received entities carry their initial pose.
    /// </summary>
    public sealed class LabBoardPublisher
    {
        public const double CheckpointIntervalSeconds = 5d;
        private sealed class SentBoard
        {
            public string session;
            public uint round;
            public double checkpointAt;
            public Dictionary<string, LabOrbState> states;
        }

        private readonly Dictionary<ulong, SentBoard> sent = new Dictionary<ulong, SentBoard>();

        public void Reset() => sent.Clear();

        public LabBoardFrame Build(LabSnapshot snapshot, ulong recipient, long sequence,
            ulong modelRevision, bool force = false)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.sessionId) || snapshot.round == 0
                || sequence <= 0) throw new ArgumentException("An active snapshot and positive board sequence are required.");
            LabOrbState[] current = (snapshot.orbs ?? Array.Empty<LabOrbState>())
                .Where(orb => orb != null && orb.owner == recipient && !orb.inFlight)
                .Concat((snapshot.drums ?? Array.Empty<LabOrbState>()).Where(drum => drum != null))
                .OrderBy(orb => orb.id, StringComparer.Ordinal).ToArray();
            if (current.Length > LabBoardReplica.MaximumStates
                || current.Count(orb => orb.kind.IsDrum()) > LabBoardReplica.MaximumDrums)
                throw new ArgumentException("A board exceeds the configured orb/drum limit.");
            bool newRound = !sent.TryGetValue(recipient, out var previous)
                || previous.session != snapshot.sessionId || previous.round != snapshot.round;
            bool checkpoint = force || newRound
                || snapshot.serverTime - previous.checkpointAt >= CheckpointIntervalSeconds;
            var states = new List<LabOrbState>();
            var currentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LabOrbState orb in current)
            {
                currentIds.Add(orb.id);
                if (checkpoint || !previous.states.TryGetValue(orb.id, out var old) || !SameMetadata(old, orb))
                    states.Add(orb.Copy());
            }
            string[] removed = checkpoint ? Array.Empty<string>() : previous.states.Keys
                .Where(id => !currentIds.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var frame = new LabBoardFrame
            {
                sessionId = snapshot.sessionId, round = snapshot.round, sequence = sequence,
                localOwner = recipient, checkpoint = checkpoint, serverTime = snapshot.serverTime,
                modelRevision = modelRevision, states = states.ToArray(), removedIds = removed,
                players = CopyPlayers(snapshot.players), phase = snapshot.phase, paused = snapshot.paused,
                remaining = snapshot.remaining, playedSeconds = snapshot.playedSeconds,
                swipeBlockedPlayer = snapshot.swipeBlockedPlayer, swipeBlockedRight = snapshot.swipeBlockedRight,
                swipeBlockStartedAt = snapshot.swipeBlockStartedAt, swipeBlockedUntil = snapshot.swipeBlockedUntil,
                charmedPlayer = snapshot.charmedPlayer, charmStartedAt = snapshot.charmStartedAt,
                charmUntil = snapshot.charmUntil, charmTaps = snapshot.charmTaps,
                targetYawDegrees = snapshot.targetYawDegrees,
                targetYawSpeedDegreesPerSecond = snapshot.targetYawSpeedDegreesPerSecond,
                targetRopeCycleElapsed = snapshot.targetRopeCycleElapsed,
                targetRopeHitOffset = snapshot.targetRopeHitOffset, targetRopeHitVelocity = snapshot.targetRopeHitVelocity
            };
            sent[recipient] = new SentBoard
            {
                session = snapshot.sessionId, round = snapshot.round,
                checkpointAt = checkpoint ? snapshot.serverTime : previous.checkpointAt,
                states = current.ToDictionary(orb => orb.id, orb => orb.Copy(), StringComparer.Ordinal)
            };
            return frame;
        }

        private static bool SameMetadata(LabOrbState a, LabOrbState b) => a.id == b.id && a.owner == b.owner
            && a.ownershipVersion == b.ownershipVersion && a.kind == b.kind && a.element == b.element
            && a.lockSwipes == b.lockSwipes && a.relayStage == b.relayStage && a.origin == b.origin
            && a.originOwner == b.originOwner && a.createdAt == b.createdAt && a.unlockedAt == b.unlockedAt
            && a.inFlight == b.inFlight && a.arrival == b.arrival && a.stopAt == b.stopAt
            && a.arrivedAt == b.arrivedAt && a.burnedAt == b.burnedAt && a.small == b.small;

        internal static LabPlayerState[] CopyPlayers(LabPlayerState[] players) =>
            (players ?? Array.Empty<LabPlayerState>()).Select(player => player?.Copy()).ToArray();
    }

    /// <summary>
    /// Owner board ledger independent of large paint checkpoints. A receipt and a board delta use
    /// one ordered recipient stream. Gaps freeze new interaction until an explicit checkpoint.
    /// </summary>
    public sealed class LabBoardReplica
    {
        // This is a packet/ledger safety bound, not the configured storage rule: gourd bombs can
        // legally arrive on a full board and therefore must not be capped at storage + drums.
        public const int MaximumStates = 4096;
        public const int MaximumPlayers = 5;
        public const int MaximumDrums = 5;
        private readonly Dictionary<string, LabOrbState> states = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly HashSet<string> tombstones = new HashSet<string>(StringComparer.Ordinal);
        private string session;
        private uint round;
        private ulong owner;
        private bool baseline;
        private LabBoardFrame header;
        private LabPlayerState[] players = Array.Empty<LabPlayerState>();
        private double playersAt = -1d;
        public long LatestSequence { get; private set; }
        public bool NeedsCheckpoint { get; private set; }
        public bool IsInitialized => baseline || LatestSequence > 0;
        public bool InteractionAllowed => !NeedsCheckpoint && header != null
            && header.phase == LabPhase.Playing && !header.paused;

        public void SetActive(string activeSession, uint activeRound, ulong localOwner)
        {
            if (session == activeSession && round == activeRound && owner == localOwner) return;
            session = activeSession;
            round = activeRound;
            owner = localOwner;
            states.Clear(); tombstones.Clear();
            players = Array.Empty<LabPlayerState>(); playersAt = -1d;
            header = null; baseline = false; LatestSequence = 0; NeedsCheckpoint = false;
        }

        public bool TryGetState(string id, out LabOrbState state)
        {
            state = null;
            if (id == null || !states.TryGetValue(id, out var current)) return false;
            state = current.Copy();
            return true;
        }

        public bool TryApply(LabBoardFrame frame, out string reason)
        {
            reason = null;
            if (frame == null || !Matches(frame.sessionId, frame.round) || frame.localOwner != owner)
                return Reject("STALE_BOARD_ROUND", out reason);
            if (!ValidFrame(frame) || !CanApplyStates(frame.states, frame.removedIds, frame.checkpoint))
                return Reject("INVALID_BOARD_FRAME", out reason);
            if (frame.sequence <= LatestSequence) return Reject("STALE_BOARD_SEQUENCE", out reason);
            if (!frame.checkpoint && (NeedsCheckpoint || !baseline || frame.sequence != LatestSequence + 1))
            {
                NeedsCheckpoint = true;
                return Reject("BOARD_CHECKPOINT_REQUIRED", out reason);
            }
            if (frame.checkpoint)
            {
                states.Clear(); tombstones.Clear(); baseline = true; NeedsCheckpoint = false;
            }
            ApplyStates(frame.states, frame.removedIds);
            header = frame.CopyHeader();
            players = LabBoardPublisher.CopyPlayers(frame.players);
            playersAt = frame.serverTime;
            LatestSequence = frame.sequence;
            return true;
        }

        public bool ApplyReceipt(LabActionReceipt receipt)
        {
            if (receipt == null || !Matches(receipt.sessionId, receipt.round) || receipt.sequence <= LatestSequence
                || receipt.sequence <= 0 || !Finite(receipt.serverTime) || receipt.serverTime < 0d
                || !ValidStates(receipt.states, false) || !ValidRemoved(receipt.removedIds)
                || !ValidPlayers(receipt.players) || !CanApplyStates(receipt.states, receipt.removedIds, false)) return false;
            if (NeedsCheckpoint || receipt.sequence != LatestSequence + 1)
            {
                NeedsCheckpoint = true;
                return false;
            }
            // Rejected operations also occupy a stream sequence and can carry restoring ledger state.
            ApplyStates(receipt.states, receipt.removedIds);
            if (receipt.serverTime >= playersAt && (receipt.players?.Length ?? 0) > 0)
            {
                players = LabBoardPublisher.CopyPlayers(receipt.players);
                playersAt = receipt.serverTime;
            }
            LatestSequence = receipt.sequence;
            return true;
        }

        /// <summary>
        /// Copy the shared snapshot and replace only local-board/control data. In particular this
        /// never manufactures paint progress, HP, combo outcomes or a Victory/Defeat transition.
        /// </summary>
        public LabSnapshot Overlay(LabSnapshot snapshot)
        {
            if (snapshot == null || !Matches(snapshot.sessionId, snapshot.round)) return snapshot;
            if (!baseline)
            {
                foreach (LabOrbState state in (snapshot.orbs ?? Array.Empty<LabOrbState>())
                    .Where(orb => orb != null && orb.owner == owner && !orb.inFlight)
                    .Concat((snapshot.drums ?? Array.Empty<LabOrbState>()).Where(drum => drum != null)))
                {
                    if (!tombstones.Contains(state.id) && !states.ContainsKey(state.id)) states.Add(state.id, state.Copy());
                }
                baseline = true;
            }
            LabSnapshot copy = CopySnapshot(snapshot);
            copy.orbs = (snapshot.orbs ?? Array.Empty<LabOrbState>())
                .Where(orb => orb != null && orb.owner != owner && !states.ContainsKey(orb.id) && !tombstones.Contains(orb.id))
                .Select(orb => orb.Copy()).Concat(states.Values.Where(orb => !orb.kind.IsDrum()).Select(orb => orb.Copy()))
                .OrderBy(orb => orb.id, StringComparer.Ordinal).ToArray();
            copy.drums = states.Values.Where(orb => orb.kind.IsDrum()).OrderBy(orb => orb.id, StringComparer.Ordinal)
                .Select(orb => orb.Copy()).ToArray();
            if (playersAt >= snapshot.serverTime && players.Length > 0)
                copy.players = LabBoardPublisher.CopyPlayers(players);
            if (header != null && header.serverTime >= snapshot.serverTime && snapshot.phase == LabPhase.Playing)
            {
                copy.serverTime = header.serverTime;
                copy.paused = header.paused;
                copy.remaining = header.remaining; copy.playedSeconds = header.playedSeconds;
                copy.swipeBlockedPlayer = header.swipeBlockedPlayer; copy.swipeBlockedRight = header.swipeBlockedRight;
                copy.swipeBlockStartedAt = header.swipeBlockStartedAt; copy.swipeBlockedUntil = header.swipeBlockedUntil;
                copy.charmedPlayer = header.charmedPlayer; copy.charmStartedAt = header.charmStartedAt;
                copy.charmUntil = header.charmUntil; copy.charmTaps = header.charmTaps;
                copy.targetYawDegrees = header.targetYawDegrees;
                copy.targetYawSpeedDegreesPerSecond = header.targetYawSpeedDegreesPerSecond;
                copy.targetRopeCycleElapsed = header.targetRopeCycleElapsed;
                copy.targetRopeHitOffset = header.targetRopeHitOffset; copy.targetRopeHitVelocity = header.targetRopeHitVelocity;
            }
            return copy;
        }

        private void ApplyStates(LabOrbState[] added, string[] removed)
        {
            foreach (string id in removed ?? Array.Empty<string>()) { states.Remove(id); tombstones.Add(id); }
            foreach (LabOrbState state in added ?? Array.Empty<LabOrbState>())
            {
                if (!state.kind.IsDrum() && (state.owner != owner || state.inFlight))
                {
                    states.Remove(state.id); tombstones.Add(state.id);
                }
                else
                {
                    states[state.id] = state.Copy(); tombstones.Remove(state.id);
                }
            }
        }

        private bool CanApplyStates(LabOrbState[] added, string[] removed, bool checkpoint)
        {
            var ids = checkpoint ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(states.Keys, StringComparer.Ordinal);
            var drums = checkpoint ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(states.Values.Where(state => state.kind.IsDrum()).Select(state => state.id), StringComparer.Ordinal);
            foreach (string id in removed ?? Array.Empty<string>()) { ids.Remove(id); drums.Remove(id); }
            foreach (LabOrbState state in added ?? Array.Empty<LabOrbState>())
            {
                if (!state.kind.IsDrum() && (state.owner != owner || state.inFlight))
                {
                    ids.Remove(state.id); drums.Remove(state.id);
                }
                else
                {
                    ids.Add(state.id);
                    if (state.kind.IsDrum()) drums.Add(state.id);
                    else drums.Remove(state.id);
                }
            }
            return ids.Count <= MaximumStates && drums.Count <= MaximumDrums;
        }

        private bool Matches(string value, uint valueRound) => !string.IsNullOrEmpty(session) && round > 0
            && session == value && round == valueRound;

        private bool ValidFrame(LabBoardFrame frame) => frame.sequence > 0
            && Finite(frame.serverTime) && frame.serverTime >= 0d
            && Finite(frame.remaining) && frame.remaining >= 0d && Finite(frame.playedSeconds) && frame.playedSeconds >= 0d
            && frame.phase >= LabPhase.Idle && frame.phase <= LabPhase.Defeat
            && Finite(frame.swipeBlockStartedAt) && Finite(frame.swipeBlockedUntil)
            && Finite(frame.charmStartedAt) && Finite(frame.charmUntil) && frame.charmTaps >= 0
            && Finite(frame.targetYawDegrees) && Finite(frame.targetYawSpeedDegreesPerSecond)
            && Finite(frame.targetRopeCycleElapsed) && Finite(frame.targetRopeHitOffset) && Finite(frame.targetRopeHitVelocity)
            && ValidStates(frame.states, true) && ValidRemoved(frame.removedIds) && ValidPlayers(frame.players);

        private bool ValidStates(LabOrbState[] values, bool forThisOwner)
        {
            if (values == null || values.Length > MaximumStates) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int drums = 0;
            foreach (LabOrbState state in values)
            {
                if (state == null || !ValidId(state.id) || !ids.Add(state.id) || state.ownershipVersion == 0
                    || state.kind < LabOrbKind.Yin || state.kind > LabOrbKind.FoxFire
                    || state.element < LabOrbElement.None || state.element > LabOrbElement.Earth
                    || state.lockSwipes < 0 || state.relayStage < 0 || state.relayStage > 3
                    || !Unit(state.x) || !Unit(state.y) || !Finite(state.vx) || !Finite(state.vy)
                    || !Finite(state.createdAt) || state.createdAt < 0d || !Finite(state.unlockedAt)
                    || !Finite(state.arrivedAt) || !Finite(state.burnedAt) || !Finite(state.stopAt)
                    || state.arrival < LabDrumArrival.None || state.arrival > LabDrumArrival.Coast
                    || (forThisOwner && !state.kind.IsDrum() && state.owner != owner)) return false;
                if (state.kind.IsDrum() && ++drums > MaximumDrums) return false;
            }
            return true;
        }

        private static bool ValidRemoved(string[] ids)
        {
            if (ids == null || ids.Length > MaximumStates) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            return ids.All(id => ValidId(id) && unique.Add(id));
        }

        private static bool ValidPlayers(LabPlayerState[] values)
        {
            if (values == null || values.Length > MaximumPlayers) return false;
            var ids = new HashSet<ulong>();
            var seats = new HashSet<int>();
            return values.All(player => player != null && ids.Add(player.id) && player.seat >= 0
                && player.seat < MaximumPlayers && seats.Add(player.seat) && player.personalHp >= 0
                && Finite(player.stamina) && player.stamina >= 0f && Finite(player.drumSeconds)
                && Finite(player.comboPenaltyUntil) && Finite(player.comboRewardUntil));
        }

        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;
        private static bool Unit(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Reject(string value, out string reason) { reason = value; return false; }

        public static LabSnapshot CopySnapshot(LabSnapshot source) => new LabSnapshot
        {
            sessionId = source.sessionId, round = source.round, phase = source.phase, paused = source.paused,
            serverTime = source.serverTime, remaining = source.remaining, playedSeconds = source.playedSeconds,
            hp = source.hp, paintCoverage = source.paintCoverage, paintGoalPercent = source.paintGoalPercent,
            paintColumns = source.paintColumns, paintRows = source.paintRows, paintRevision = source.paintRevision,
            paintCoveredCount = source.paintCoveredCount, paintCellCount = source.paintCellCount, paintCount = source.paintCount,
            targetYawDegrees = source.targetYawDegrees, targetYawSpeedDegreesPerSecond = source.targetYawSpeedDegreesPerSecond,
            targetRopeCycleElapsed = source.targetRopeCycleElapsed, targetRopeHitOffset = source.targetRopeHitOffset,
            targetRopeHitVelocity = source.targetRopeHitVelocity, players = LabBoardPublisher.CopyPlayers(source.players),
            orbs = source.orbs, drums = source.drums, swipeBlockedPlayer = source.swipeBlockedPlayer,
            swipeBlockedRight = source.swipeBlockedRight, swipeBlockStartedAt = source.swipeBlockStartedAt,
            swipeBlockedUntil = source.swipeBlockedUntil, charmedPlayer = source.charmedPlayer,
            charmStartedAt = source.charmStartedAt, charmUntil = source.charmUntil, charmTaps = source.charmTaps,
            comboActive = source.comboActive,
            comboColors = source.comboColors == null ? Array.Empty<LabOrbElement>() : (LabOrbElement[])source.comboColors.Clone(),
            comboProgress = source.comboProgress, comboStartedAt = source.comboStartedAt, comboEndsAt = source.comboEndsAt,
            comboCount = source.comboCount, comboResult = source.comboResult, comboResultCount = source.comboResultCount,
            comboResultAt = source.comboResultAt, comboFailedBy = source.comboFailedBy,
            comboNextAt = source.comboNextAt, comboWarningAt = source.comboWarningAt,
            flashWarningAt = source.flashWarningAt, flashAt = source.flashAt, flashTarget = source.flashTarget,
            flashCount = source.flashCount, flashLastAt = source.flashLastAt, flashLastTarget = source.flashLastTarget
        };
    }
}
