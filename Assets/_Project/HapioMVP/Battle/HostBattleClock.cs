using System;
using C6.Prototype.Networking;

namespace C6.Prototype.Battle
{
    public enum BattlePhase { Boot, Lobby, Ready, Playing, Victory, Defeat, NetworkError }

    /// <summary>
    /// Host-only absolute monotonic clock and once-only result. It never applies monster damage;
    /// ObservedMonsterHp mirrors an already accepted result from the existing AttackAuthority.
    /// </summary>
    public sealed class HostBattleClock
    {
        private bool hasObservedTime;
        private double lastObservedTime;

        public double DurationSeconds { get; }
        public double TeamHpDecayPerSecond { get; }
        public BattlePhase Phase { get; private set; } = BattlePhase.Boot;
        public string SessionId { get; private set; } = string.Empty;
        public uint RoundId { get; private set; }
        public int Participants { get; private set; }
        public bool DevelopmentSolo { get; private set; }
        public int MaximumParticipants { get; }
        public int RequiredParticipants => DevelopmentSolo ? 1 : 2;
        public int MonsterMaxHp { get; private set; }
        public int ObservedMonsterHp { get; private set; }
        public double StartedAt { get; private set; }
        public double Deadline { get; private set; }
        public double Remaining { get; private set; }
        public double TeamHp => Remaining * TeamHpDecayPerSecond;
        public bool IsTerminal => Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat || Phase == BattlePhase.NetworkError;
        public bool CanStart => Phase == BattlePhase.Ready && Participants >= RequiredParticipants && Participants <= MaximumParticipants;

        public HostBattleClock(double durationSeconds, double teamHpDecayPerSecond, int maximumParticipants = 2)
        {
            if (maximumParticipants < 2 || maximumParticipants > ParticipantRing.MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(maximumParticipants));
            MaximumParticipants = maximumParticipants;
            double fullTeamHp = durationSeconds * teamHpDecayPerSecond;
            if (!Finite(durationSeconds) || durationSeconds <= 0 || !Finite(teamHpDecayPerSecond)
                || teamHpDecayPerSecond <= 0 || !Finite(fullTeamHp) || fullTeamHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration, decay and full Team HP must be finite and positive.");
            DurationSeconds = durationSeconds;
            TeamHpDecayPerSecond = teamHpDecayPerSecond;
        }

        /// <summary>Host Reset/Retry must use a fresh session or a strictly increasing round.</summary>
        public void BeginLobby(string sessionId, uint roundId, int participants, bool devSolo, int monsterMaxHp)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 128)
                throw new ArgumentException("A bounded Host session ID is required.", nameof(sessionId));
            if (roundId == 0 || participants < 0 || participants > MaximumParticipants || monsterMaxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(roundId), "A positive round/monster HP and participants within the configured capacity are required.");
            if (string.Equals(SessionId, sessionId, StringComparison.Ordinal) && roundId <= RoundId)
                throw new InvalidOperationException("A bound round cannot restart or move backwards.");

            SessionId = sessionId; RoundId = roundId; Participants = participants; DevelopmentSolo = devSolo;
            MonsterMaxHp = monsterMaxHp; ObservedMonsterHp = monsterMaxHp;
            StartedAt = Deadline = 0;
            Remaining = DurationSeconds;
            hasObservedTime = false; lastObservedTime = 0;
            Phase = participants >= RequiredParticipants ? BattlePhase.Ready : BattlePhase.Lobby;
        }

        /// <summary>Lobby readiness only. An in-battle disconnect uses NetworkError with its Host time.</summary>
        public bool SetParticipants(int participants)
        {
            if ((Phase != BattlePhase.Lobby && Phase != BattlePhase.Ready) || participants < 0 || participants > MaximumParticipants) return false;
            Participants = participants;
            Phase = participants >= RequiredParticipants ? BattlePhase.Ready : BattlePhase.Lobby;
            return true;
        }

        public bool Start(double now)
        {
            if (!CanStart || !ValidTime(now)) return false;
            double deadline = now + DurationSeconds;
            if (!Finite(deadline) || deadline <= now) return false;
            AcceptTime(now);
            StartedAt = now; Deadline = deadline; Remaining = DurationSeconds;
            Phase = BattlePhase.Playing;
            return true;
        }

        /// <summary>
        /// Returns whether the supplied Host time was valid. Lobby/Ready/Result never count down.
        /// Playing has no pause or local delta accumulation: time spent suspended still elapses.
        /// </summary>
        public bool Advance(double now)
        {
            if (Phase == BattlePhase.Boot || !ValidTime(now)) return false;
            AcceptTime(now);
            if (Phase != BattlePhase.Playing) return true;
            SetRemainingAt(now);
            if (now >= Deadline || TeamHp <= 0)
            { Remaining = 0; Phase = BattlePhase.Defeat; }
            return true;
        }

        /// <summary>Call immediately before the existing Host collision authority with one captured processing time.</summary>
        public bool CanApplyHit(double now) => Advance(now) && Phase == BattlePhase.Playing
            && now < Deadline && TeamHp > 0 && ObservedMonsterHp > 0;

        /// <summary>
        /// Call only after AttackAuthority accepted actual damage, with the exact same captured processing time.
        /// During this pair the integration layer must not interleave a newer clock update or new round.
        /// </summary>
        public bool ObserveAppliedHit(double now, int hpAfter)
        {
            if (hpAfter < 0 || hpAfter >= ObservedMonsterHp || !CanApplyHit(now)) return false;
            ObservedMonsterHp = hpAfter;
            if (hpAfter == 0) Phase = BattlePhase.Victory;
            return true;
        }

        /// <summary>
        /// A connection failure is separate from combat defeat. It freezes the current clock without
        /// manufacturing a new Defeat while handling disconnection; previously resolved results remain immutable.
        /// </summary>
        public bool NetworkError(double now)
        {
            if (Phase == BattlePhase.Boot || IsTerminal || !ValidTime(now)) return false;
            AcceptTime(now);
            if (Phase == BattlePhase.Playing) SetRemainingAt(now);
            Phase = BattlePhase.NetworkError;
            return true;
        }

        public bool Matches(string sessionId, uint roundId) => Phase != BattlePhase.Boot
            && string.Equals(SessionId, sessionId, StringComparison.Ordinal) && RoundId == roundId;
        public bool Advance(string sessionId, uint roundId, double now) => Matches(sessionId, roundId) && Advance(now);
        public bool CanApplyHit(string sessionId, uint roundId, double now) => Matches(sessionId, roundId) && CanApplyHit(now);
        public bool ObserveAppliedHit(string sessionId, uint roundId, double now, int hpAfter)
            => Matches(sessionId, roundId) && ObserveAppliedHit(now, hpAfter);
        public bool NetworkError(string sessionId, uint roundId, double now) => Matches(sessionId, roundId) && NetworkError(now);

        private void SetRemainingAt(double now) => Remaining = Math.Min(DurationSeconds, Math.Max(0, Deadline - now));
        private void AcceptTime(double now) { hasObservedTime = true; lastObservedTime = now; }
        private bool ValidTime(double now) => Finite(now) && now >= 0 && (!hasObservedTime || now >= lastObservedTime);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
