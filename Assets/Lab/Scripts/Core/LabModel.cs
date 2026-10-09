using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Plain Host-only battle state. Transport authenticates sender IDs before calling this class;
    /// clients render copies from Snapshot and never decide ownership, damage, or expiry.
    /// </summary>
    public sealed class LabModel
    {
        public const int ParticipantCount = 3;
        private const int MaximumRequestsPerRound = 4096;
        private const float EdgeTolerance = .06f;
        private readonly Dictionary<ulong, LabPlayerState> players = new Dictionary<ulong, LabPlayerState>();
        private readonly Dictionary<string, LabOrbState> orbs = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly HashSet<string> requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly ulong[] seats = new ulong[ParticipantCount];
        private int activeSeatCount;
        private readonly Dictionary<ulong, uint> generatedCounts = new Dictionary<ulong, uint>();
        private Rules rules;
        private string sessionId;
        private uint round;
        private uint seed;
        private ulong revision;
        private int hp;
        private double lastTime;
        private double deadline;
        private double remaining;
        private LabPhase phase = LabPhase.Idle;

        public string SessionId => sessionId;
        public uint Round => round;
        public LabPhase Phase => phase;
        public int Hp => hp;

        public void Begin(string id, ulong[] ids, double now, LabConfig config)
        {
            if (ids == null || ids.Length != ParticipantCount || ids.Distinct().Count() != ParticipantCount)
                throw new ArgumentException("Exactly three distinct player IDs are required.", nameof(ids));
            BeginWithSeats(id, ids, now, config);
        }

        /// <summary>Development-only battle model with one real player and no transferable neighbor.</summary>
        public void BeginSolo(string id, ulong playerId, double now, LabConfig config)
        {
            BeginWithSeats(id, new[] { playerId }, now, config);
        }

        private void BeginWithSeats(string id, ulong[] ids, double now, LabConfig config)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("A bounded session ID is required.", nameof(id));
            if (!Finite(now) || now < 0d || config == null) throw new ArgumentException("Valid Host time and a Config are required.");
            if (phase == LabPhase.Playing) throw new InvalidOperationException("End the current round before beginning another.");
            if (sessionId == id && round == uint.MaxValue) throw new InvalidOperationException("The round number is exhausted.");
            var nextRules = new Rules(config);
            double nextDeadline = now + nextRules.BattleDurationSeconds;
            if (!Finite(nextDeadline) || nextDeadline <= now)
                throw new ArgumentOutOfRangeException(nameof(now), "The battle deadline is not representable.");

            round = sessionId == id ? round + 1u : 1u;
            sessionId = id;
            rules = nextRules;
            activeSeatCount = ids.Length;
            Array.Clear(seats, 0, seats.Length);
            for (int i = 0; i < activeSeatCount; i++) seats[i] = ids[i];
            players.Clear(); orbs.Clear(); requests.Clear(); generatedCounts.Clear();
            for (int i = 0; i < activeSeatCount; i++)
            {
                players.Add(ids[i], new LabPlayerState { id = ids[i], seat = i, stamina = rules.StaminaStart });
                generatedCounts.Add(ids[i], 0u);
            }
            seed = SeedFor(id, round);
            hp = rules.MonsterMaxHp;
            revision = 0;
            lastTime = now;
            deadline = nextDeadline;
            remaining = rules.BattleDurationSeconds;
            phase = LabPhase.Playing;
        }

        /// <summary>Advance the absolute Host clock; only Idle orbs expire.</summary>
        public void Tick(double now)
        {
            if (!TryAdvance(now, out string reason)) throw new ArgumentOutOfRangeException(nameof(now), reason);
        }

        public bool TryGenerate(ulong sender, string requestId, double now, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            var player = players[sender];
            if (CountStored(sender) >= rules.OrbStorageLimit) return Reject("STORAGE_FULL", out reason);
            if (player.stamina + .00001f < rules.GenerateCost) return Reject("INSUFFICIENT_STAMINA", out reason);
            uint generated = generatedCounts[sender];
            if (generated == uint.MaxValue) return Reject("GENERATION_LIMIT", out reason);
            player.stamina = Mathf.Max(0f, player.stamina - rules.GenerateCost);
            var kind = PolarityFor(seed, sender, generated);
            generatedCounts[sender] = generated + 1;
            Vector2 position = SpawnPosition(sender);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = kind, origin = LabOrbOrigin.Generated, originOwner = sender,
                x = position.x, y = position.y,
                vx = 0f, vy = 0f, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(orb.id, orb);
            reason = "GENERATED";
            orb = orb.Copy();
            return true;
        }

        public bool TryCombine(ulong sender, string firstId, string secondId, string requestId,
            double now, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId) || firstId == secondId ||
                !orbs.TryGetValue(firstId, out var first) || !orbs.TryGetValue(secondId, out var second))
                return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(first, sender) || !UsableBy(second, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (first.kind == LabOrbKind.Combined || second.kind == LabOrbKind.Combined || first.kind == second.kind)
                return Reject("POLARITY_MISMATCH", out reason);

            orbs.Remove(firstId); orbs.Remove(secondId);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, origin = LabOrbOrigin.Combined, originOwner = sender,
                x = (first.x + second.x) * .5f, y = (first.y + second.y) * .5f,
                vx = 0f, vy = 0f, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(orb.id, orb);
            reason = "COMBINED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// A right edge enters the next seat from its left; a left edge enters the previous seat
        /// from its right. The same ID, creation time, and velocity survive the handoff.
        /// </summary>
        public bool TryTransfer(ulong sender, string id, bool right, float height01,
            Vector2 normalizedVelocity, string requestId, double now, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!orbs.TryGetValue(id ?? string.Empty, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (activeSeatCount == 1) return Reject("NO_NEIGHBOR", out reason);
            if (!Unit(height01) || !Finite(normalizedVelocity) ||
                normalizedVelocity.sqrMagnitude > rules.MaximumMotionSpeed * rules.MaximumMotionSpeed)
                return Reject("INVALID_MOTION", out reason);
            if (right ? current.x < 1f - EdgeTolerance || normalizedVelocity.x <= 0f
                : current.x > EdgeTolerance || normalizedVelocity.x >= 0f)
                return Reject("NOT_OUTWARD_EDGE", out reason);
            int receiverSeat = (players[sender].seat + (right ? 1 : activeSeatCount - 1)) % activeSeatCount;
            ulong receiver = seats[receiverSeat];
            if (CountStored(receiver) >= rules.OrbStorageLimit) return Reject("RECEIVER_FULL", out reason);
            current.owner = receiver;
            current.x = right ? 0f : 1f;
            current.y = height01;
            current.vx = normalizedVelocity.x;
            current.vy = normalizedVelocity.y;
            current.revision = ++revision;
            reason = "TRANSFERRED";
            orb = current.Copy();
            return true;
        }

        public bool TryLaunch(ulong sender, string id, string requestId, double now,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!orbs.TryGetValue(id ?? string.Empty, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (current.kind != LabOrbKind.Combined) return Reject("RAW_CANNOT_ATTACK", out reason);
            current.inFlight = true;
            current.vx = current.vy = 0f;
            current.revision = ++revision;
            reason = "LAUNCHED";
            orb = current.Copy();
            return true;
        }

        /// <summary>Call only after the Host projectile actually collides with the cylinder target.</summary>
        public bool ConfirmHit(string id, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !orbs.TryGetValue(id ?? string.Empty, out var orb)
                || !orb.inFlight) return false;
            orbs.Remove(id);
            hp = Math.Max(0, hp - rules.Damage);
            if (players.TryGetValue(orb.owner, out var attacker))
                attacker.stamina = Mathf.Min(rules.StaminaMax, attacker.stamina + rules.HitRecovery);
            if (hp == 0) phase = LabPhase.Victory;
            return true;
        }

        /// <summary>Remove an expired or missed projectile without damage or a hit reward.</summary>
        public bool ConfirmMiss(string id, double now)
        {
            if (!TryAdvance(now, out _) || !orbs.TryGetValue(id ?? string.Empty, out var orb) || !orb.inFlight) return false;
            return orbs.Remove(id);
        }

        /// <summary>Only the current owner may submit a finite local physics sample.</summary>
        public bool TryUpdateMotion(ulong sender, string id, Vector2 normalizedPosition,
            Vector2 normalizedVelocity, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !orbs.TryGetValue(id ?? string.Empty, out var orb)
                || !UsableBy(orb, sender) || !Unit(normalizedPosition.x) || !Unit(normalizedPosition.y)
                || !Finite(normalizedVelocity) ||
                normalizedVelocity.sqrMagnitude > rules.MaximumMotionSpeed * rules.MaximumMotionSpeed) return false;
            orb.x = normalizedPosition.x; orb.y = normalizedPosition.y;
            orb.vx = normalizedVelocity.x; orb.vy = normalizedVelocity.y;
            orb.revision = ++revision;
            return true;
        }

        public LabSnapshot Snapshot(double now)
        {
            if (phase == LabPhase.Idle)
            {
                if (!Finite(now) || now < 0d) throw new ArgumentOutOfRangeException(nameof(now));
                return new LabSnapshot { sessionId = string.Empty, serverTime = now, phase = LabPhase.Idle };
            }
            Tick(now);
            return new LabSnapshot
            {
                sessionId = sessionId ?? string.Empty,
                round = round,
                phase = phase,
                serverTime = now,
                remaining = remaining,
                hp = hp,
                players = players.Values.OrderBy(player => player.seat).Select(player => player.Copy()).ToArray(),
                orbs = orbs.Values.OrderBy(orb => orb.id, StringComparer.Ordinal).Select(orb => orb.Copy()).ToArray()
            };
        }

        private bool BeginRequest(ulong sender, string requestId, double now, out string reason)
        {
            if (!TryAdvance(now, out reason)) return false;
            if (phase != LabPhase.Playing) return Reject("BATTLE_NOT_PLAYING", out reason);
            if (!players.ContainsKey(sender)) return Reject("UNKNOWN_PLAYER", out reason);
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) return Reject("INVALID_REQUEST_ID", out reason);
            if (requests.Contains(requestId)) return Reject("DUPLICATE_REQUEST", out reason);
            if (requests.Count >= MaximumRequestsPerRound) return Reject("REQUEST_LIMIT", out reason);
            requests.Add(requestId);
            reason = null;
            return true;
        }

        private bool TryAdvance(double now, out string reason)
        {
            reason = null;
            if (phase == LabPhase.Idle) return Reject("NO_SESSION", out reason);
            if (!Finite(now) || now < lastTime) return Reject("INVALID_TIME", out reason);
            if (phase == LabPhase.Playing)
            {
                double effectiveNow = Math.Min(now, deadline);
                double elapsed = effectiveNow - lastTime;
                if (elapsed > 0d)
                    foreach (var player in players.Values)
                        player.stamina = Mathf.Min(rules.StaminaMax,
                            (float)(player.stamina + elapsed * rules.StaminaRecoveryPerSecond));
                foreach (string id in orbs.Values.Where(orb => !orb.inFlight && effectiveNow - orb.createdAt >= rules.OrbLifetimeSeconds)
                    .Select(orb => orb.id).ToArray()) orbs.Remove(id);
                remaining = Math.Max(0d, deadline - now);
                if (now >= deadline) phase = LabPhase.Defeat;
            }
            lastTime = now;
            return true;
        }

        private int CountStored(ulong owner) => orbs.Values.Count(orb => orb.owner == owner && !orb.inFlight);
        private Vector2 SpawnPosition(ulong owner)
        {
            // Keep the lowest row above the bottom HUD so generated orbs can be grabbed.
            // The rise animation still begins below the row, as if the orb emerges from underneath.
            for (int index = 0; index < 20; index++)
            {
                var point = new Vector2(.16f + (index % 5) * .17f, .43f + (index / 5) * .17f);
                bool clear = !orbs.Values.Any(orb => orb.owner == owner && !orb.inFlight &&
                    Mathf.Abs(orb.x - point.x) < .14f && Mathf.Abs(orb.y - point.y) < .16f);
                if (clear) return point;
            }
            return new Vector2(.5f, .5f);
        }
        private string NewOrbId()
        {
            string id;
            do id = Guid.NewGuid().ToString("N"); while (orbs.ContainsKey(id));
            return id;
        }
        private static bool UsableBy(LabOrbState orb, ulong sender) => orb.owner == sender && !orb.inFlight;
        private static bool Reject(string value, out string reason) { reason = value; return false; }
        private static bool Unit(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static uint SeedFor(string id, uint round)
        {
            unchecked
            {
                uint hash = 2166136261u ^ round;
                foreach (char character in id) { hash ^= character; hash *= 16777619u; }
                return hash;
            }
        }

        // Counter-based Host draw: one player's accepted generation cannot change another's
        // polarity sequence. After three equal results the next Raw flips polarity.
        private static LabOrbKind PolarityFor(uint seed, ulong playerId, uint successfulIndex)
        {
            var previous = LabOrbKind.Combined;
            var current = LabOrbKind.Combined;
            uint run = 0;
            for (uint index = 0; ; index++)
            {
                current = RawPolarityFor(seed, playerId, index);
                if (run >= 3 && current == previous)
                    current = current == LabOrbKind.Yin ? LabOrbKind.Yang : LabOrbKind.Yin;
                if (current == previous) run++;
                else { previous = current; run = 1; }
                if (index == successfulIndex) return current;
            }
        }

        private static LabOrbKind RawPolarityFor(uint seed, ulong playerId, uint index)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ playerId;
                value += 0x9E3779B97F4A7C15UL * (index + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (value & 1UL) == 0 ? LabOrbKind.Yin : LabOrbKind.Yang;
            }
        }

        private sealed class Rules
        {
            public readonly int MonsterMaxHp, Damage, OrbStorageLimit;
            public readonly float BattleDurationSeconds, StaminaMax, StaminaStart, GenerateCost,
                StaminaRecoveryPerSecond, HitRecovery, OrbLifetimeSeconds, MaximumMotionSpeed;

            public Rules(LabConfig config)
            {
                MonsterMaxHp = config.MonsterMaxHp;
                Damage = config.Damage;
                OrbStorageLimit = config.OrbStorageLimit;
                BattleDurationSeconds = config.BattleDurationSeconds;
                StaminaMax = config.StaminaMax;
                StaminaStart = config.StaminaStart;
                GenerateCost = config.GenerateCost;
                StaminaRecoveryPerSecond = config.StaminaRecoveryPerSecond;
                HitRecovery = config.HitRecovery;
                OrbLifetimeSeconds = config.OrbLifetimeSeconds;
                MaximumMotionSpeed = config.OrbMaxReleaseSpeed * 16f;
            }
        }
    }
}
