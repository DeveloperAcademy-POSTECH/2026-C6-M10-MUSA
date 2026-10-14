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
        public const int MinimumParticipants = 2;
        public const int MaximumParticipants = 5;
        public const int DefaultParticipants = 3;
        private const int MaximumRequestsPerRound = 4096;
        private const float EdgeTolerance = .06f;
        private const float SecretBoxSpread = .05f;
        private const int RelayMaxStage = 3;
        private const uint LockDrawSalt = 0x10C4ED00u;
        private const uint SpawnAngleSalt = 0x5A4E0A11u;
        private const uint SpawnSpeedSalt = 0x5A4E05EDu;
        private const uint DrumSeatSalt = 0xD2D2D2D2u;
        // 갓 생성된 구슬은 옆 가장자리(이웃에게 넘어가는 선)에서 이만큼 안쪽까지만 미끄러진다.
        private const float SpawnEdgeMargin = .08f;
        // 한 판에 북이 여러 개 놓일 때 가운데부터 좌우로 벌려 놓는다.
        private static readonly float[] DrumStartX = { .5f, .3f, .7f, .15f, .85f };
        private readonly Dictionary<ulong, LabPlayerState> players = new Dictionary<ulong, LabPlayerState>();
        private readonly Dictionary<string, LabOrbState> orbs = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly Dictionary<string, LabOrbState> drums = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly HashSet<string> requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly ulong[] seats = new ulong[MaximumParticipants];
        private int activeSeatCount;
        private readonly Dictionary<ulong, uint> generatedCounts = new Dictionary<ulong, uint>();
        private readonly List<ulong> bombExplosions = new List<ulong>();
        private Rules rules;
        private string sessionId;
        private uint round;
        private uint seed;
        private uint combinedCount;
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

        /// <summary>ids must be the Host-approved player IDs in P1..PN order.</summary>
        public void Begin(string id, ulong[] ids, double now, LabConfig config)
        {
            if (ids == null || ids.Length < MinimumParticipants || ids.Length > MaximumParticipants
                || ids.Distinct().Count() != ids.Length)
                throw new ArgumentException("Two to five distinct player IDs are required.", nameof(ids));
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
            players.Clear(); orbs.Clear(); drums.Clear(); requests.Clear(); generatedCounts.Clear(); bombExplosions.Clear();
            for (int i = 0; i < activeSeatCount; i++)
            {
                players.Add(ids[i], new LabPlayerState
                {
                    id = ids[i], seat = i, stamina = rules.StaminaStart, personalHp = rules.PlayerMaxHp
                });
                generatedCounts.Add(ids[i], 0u);
            }
            seed = SeedFor(id, round);
            combinedCount = 0;
            hp = rules.MonsterMaxHp;
            revision = 0;
            PlaceDrums(now);
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

        /// <summary>Generate from the sender's first drum at its last Host-known position.</summary>
        public bool TryGenerate(ulong sender, string requestId, double now, out LabOrbState orb, out string reason)
            => TryGenerate(sender, null, null, requestId, now, out orb, out reason);

        /// <summary>
        /// A player can generate only from a drum on their own board. The orb starts at the drum's
        /// position, which the owner's screen reports with the tap because only it simulates the drum.
        /// </summary>
        public bool TryGenerate(ulong sender, string drumId, Vector2? drumPosition, string requestId, double now,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            // Without a tapped drum, use the sender's earliest placed one (drums are never removed).
            LabOrbState drum = drumId == null
                ? drums.Values.FirstOrDefault(candidate => candidate.owner == sender)
                : drums.TryGetValue(drumId, out var tapped) && tapped.owner == sender ? tapped : null;
            if (drum == null) return Reject("NO_DRUM", out reason);
            Vector2 position = drumPosition ?? new Vector2(drum.x, drum.y);
            if (!Unit(position.x) || !Unit(position.y)) return Reject("INVALID_MOTION", out reason);
            var player = players[sender];
            if (CountStored(sender) >= rules.OrbStorageLimit) return Reject("STORAGE_FULL", out reason);
            if (player.stamina + .00001f < rules.GenerateCost) return Reject("INSUFFICIENT_STAMINA", out reason);
            uint generated = generatedCounts[sender];
            if (generated == uint.MaxValue) return Reject("GENERATION_LIMIT", out reason);
            player.stamina = Mathf.Max(0f, player.stamina - rules.GenerateCost);
            var kind = GeneratedKindFor(seed, sender, generated, rules.GenerationChances);
            int lockSwipes = kind.IsItem() ? LockSwipesFor(seed, sender, generated) : 0;
            int relayStage = kind == LabOrbKind.Relay && lockSwipes == 0 ? 1 : 0;
            generatedCounts[sender] = generated + 1;
            Vector2 launch = SpawnVelocityFor(seed, sender, generated, position.x);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = kind, lockSwipes = lockSwipes, relayStage = relayStage,
                origin = LabOrbOrigin.Generated, originOwner = sender,
                x = position.x, y = position.y,
                vx = launch.x, vy = launch.y, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(orb.id, orb);
            reason = "GENERATED";
            orb = orb.Copy();
            return true;
        }

        public bool TryCombine(ulong sender, string firstId, string secondId, string requestId,
            double now, out LabOrbState orb, out string reason)
            => TryCombine(sender, firstId, secondId, requestId, now, Vector2.zero, out orb, out reason);

        /// <summary>
        /// 결합 구슬이 결합 직전 두 구슬의 움직임을 이어받는다. 속도는 요청한 기기가 계산해
        /// 보내며(보드 폭/초), Host는 유한한 값인지 확인하고 최대 이동 속도 안으로 제한한다.
        /// </summary>
        public bool TryCombine(ulong sender, string firstId, string secondId, string requestId,
            double now, Vector2 normalizedVelocity, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId) || firstId == secondId ||
                !orbs.TryGetValue(firstId, out var first) || !orbs.TryGetValue(secondId, out var second))
                return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(first, sender) || !UsableBy(second, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (!first.kind.IsRaw() || !second.kind.IsRaw() || first.kind == second.kind)
                return Reject("POLARITY_MISMATCH", out reason);

            orbs.Remove(firstId); orbs.Remove(secondId);
            var element = ElementFor(seed, combinedCount++);
            Vector2 velocity = Finite(normalizedVelocity)
                ? Vector2.ClampMagnitude(normalizedVelocity, rules.MaximumMotionSpeed)
                : Vector2.zero;
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, element = element,
                origin = LabOrbOrigin.Combined, originOwner = sender,
                x = (first.x + second.x) * .5f, y = (first.y + second.y) * .5f,
                vx = velocity.x, vy = velocity.y, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(orb.id, orb);
            reason = "COMBINED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// A right edge enters the next seat from its left; a left edge enters the previous seat
        /// from its right. The same ID, creation time, and velocity survive the handoff.
        /// Each handoff of a locked item removes one lock, so items unlock only by teamwork. A relay
        /// orb becomes x1 when it unlocks, then each further handoff raises it to x2 and MAX. An
        /// unlocked bomb becomes an ordinary attack orb: its fuse is cut and a fresh orb lifetime starts.
        /// </summary>
        public bool TryTransfer(ulong sender, string id, bool right, float height01,
            Vector2 normalizedVelocity, string requestId, double now, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!TryFindMovable(id, out var current)) return Reject("ORB_NOT_FOUND", out reason);
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
            // A drum is not a stored orb, so a full board still receives it.
            if (!current.kind.IsDrum() && CountStored(receiver) >= rules.OrbStorageLimit)
                return Reject("RECEIVER_FULL", out reason);
            current.owner = receiver;
            current.x = right ? 0f : 1f;
            current.y = height01;
            current.vx = normalizedVelocity.x;
            current.vy = normalizedVelocity.y;
            current.unlockedAt = -1d;
            if (current.lockSwipes > 0)
            {
                current.lockSwipes--;
                if (current.lockSwipes == 0) current.unlockedAt = now;
                if (current.lockSwipes == 0 && current.kind == LabOrbKind.Relay) current.relayStage = 1;
                if (current.lockSwipes == 0 && current.kind == LabOrbKind.Bomb) current.createdAt = now;
            }
            else if (current.kind == LabOrbKind.Relay && current.relayStage < RelayMaxStage) current.relayStage++;
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
            if (!current.kind.CanThrow(current.lockSwipes)) return Reject("RAW_CANNOT_ATTACK", out reason);
            current.inFlight = true;
            current.vx = current.vy = 0f;
            current.revision = ++revision;
            reason = "LAUNCHED";
            orb = current.Copy();
            return true;
        }

        /// <summary>Call only after the Host projectile actually collides with the cylinder target.</summary>
        public bool ConfirmHit(string id, double now)
            => ConfirmHit(id, now, out _, out _);

        /// <summary>
        /// Confirm one Host collision and return the consumed orb's element and the HP actually
        /// removed. The latter can be less than configured damage for a finishing hit.
        /// </summary>
        public bool ConfirmHit(string id, double now, out LabOrbElement element, out int actualDamage)
        {
            element = LabOrbElement.None;
            actualDamage = 0;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !orbs.TryGetValue(id ?? string.Empty, out var orb)
                || !orb.inFlight) return false;
            element = orb.element;
            orbs.Remove(id);
            int previousHp = hp;
            hp = Math.Max(0, hp - DamageFor(orb));
            actualDamage = previousHp - hp;
            if (players.TryGetValue(orb.owner, out var attacker))
                attacker.stamina = Mathf.Min(rules.StaminaMax, attacker.stamina + rules.HitRecovery);
            if (hp == 0) phase = LabPhase.Victory;
            return true;
        }

        /// <summary>
        /// Host-only entry point for a future player-damage event. No gameplay action invokes it yet.
        /// One player reaching zero does not stop the round; all players reaching zero ends in Defeat.
        /// </summary>
        public bool TryApplyPlayerDamage(ulong playerId, int amount, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || amount <= 0 ||
                !players.TryGetValue(playerId, out var player) || player.personalHp <= 0) return false;

            player.personalHp = Math.Max(0, player.personalHp - amount);
            if (players.Values.All(current => current.personalHp == 0)) phase = LabPhase.Defeat;
            return true;
        }

        /// <summary>
        /// Double-tap use of the owner's unlocked item. A secret box opens into two throwable element
        /// orbs beside it, which needs one more free storage slot. A potion heals its user's personal
        /// HP up to the maximum; at full HP it is refused and kept so it can still be passed to a
        /// neighbor. A player already at 0 HP cannot be revived by a potion.
        /// </summary>
        public bool TryUseItem(ulong sender, string id, string requestId, double now,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!orbs.TryGetValue(id ?? string.Empty, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (!current.kind.IsDoubleTapUsable()) return Reject("NOT_USABLE", out reason);
            if (current.lockSwipes > 0) return Reject("ITEM_LOCKED", out reason);
            if (current.kind == LabOrbKind.SecretBox)
            {
                if (CountStored(sender) + 1 > rules.OrbStorageLimit) return Reject("STORAGE_FULL", out reason);
                orbs.Remove(current.id);
                foreach (float side in new[] { -1f, 1f })
                {
                    var opened = new LabOrbState
                    {
                        id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, element = ElementFor(seed, combinedCount++),
                        origin = LabOrbOrigin.Combined, originOwner = sender,
                        x = Mathf.Clamp01(current.x + side * SecretBoxSpread), y = current.y,
                        vx = 0f, vy = 0f, createdAt = now, revision = ++revision, inFlight = false
                    };
                    orbs.Add(opened.id, opened);
                }
                reason = "ITEM_USED";
                orb = current.Copy();
                return true;
            }
            var player = players[sender];
            if (player.personalHp <= 0) return Reject("PLAYER_DOWN", out reason);
            if (player.personalHp >= rules.PlayerMaxHp) return Reject("HP_FULL", out reason);
            player.personalHp = Math.Min(rules.PlayerMaxHp, player.personalHp + rules.PotionHeal);
            orbs.Remove(current.id);
            reason = "ITEM_USED";
            orb = current.Copy();
            return true;
        }

        /// <summary>Players whose boards were cleared by an unthrown bomb since the last call.</summary>
        public ulong[] DrainBombExplosions()
        {
            ulong[] owners = bombExplosions.ToArray();
            bombExplosions.Clear();
            return owners;
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
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !TryFindMovable(id, out var orb)
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
                orbs = orbs.Values.OrderBy(orb => orb.id, StringComparer.Ordinal).Select(orb => orb.Copy()).ToArray(),
                drums = drums.Values.OrderBy(drum => drum.id, StringComparer.Ordinal).Select(drum => drum.Copy()).ToArray()
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
                foreach (var expired in orbs.Values.Where(orb => !orb.inFlight && effectiveNow - orb.createdAt >= LifetimeOf(orb))
                    .ToArray())
                {
                    if (!orbs.Remove(expired.id) || expired.kind != LabOrbKind.Bomb || expired.lockSwipes == 0) continue;
                    // A bomb still locked at its fuse takes every orb on its current holder's board.
                    foreach (string id in orbs.Values.Where(orb => orb.owner == expired.owner && !orb.inFlight)
                        .Select(orb => orb.id).ToArray()) orbs.Remove(id);
                    bombExplosions.Add(expired.owner);
                }
                remaining = Math.Max(0d, deadline - now);
                if (now >= deadline) phase = LabPhase.Defeat;
            }
            lastTime = now;
            return true;
        }

        private int DamageFor(LabOrbState orb)
        {
            if (orb.kind == LabOrbKind.Bomb) return Math.Max(1, (int)Math.Round(rules.Damage * rules.BombMultiplier));
            if (orb.kind != LabOrbKind.Relay) return rules.Damage;
            float multiplier = orb.relayStage >= RelayMaxStage ? rules.RelayMaxMultiplier
                : orb.relayStage == 2 ? rules.RelayDoubleMultiplier : 1f;
            return Math.Max(1, (int)Math.Round(rules.Damage * multiplier));
        }

        // An unlocked bomb is an ordinary attack orb and keeps the orb lifetime, not the fuse.
        private float LifetimeOf(LabOrbState orb) =>
            orb.kind.IsItem() && !(orb.kind == LabOrbKind.Bomb && orb.lockSwipes == 0)
                ? rules.ItemLifetimeSeconds : rules.OrbLifetimeSeconds;
        private int CountStored(ulong owner) => orbs.Values.Count(orb => orb.owner == owner && !orb.inFlight);

        /// <summary>Orbs and drums both move between screens; look the ID up in either list.</summary>
        private bool TryFindMovable(string id, out LabOrbState found) =>
            orbs.TryGetValue(id ?? string.Empty, out found) || drums.TryGetValue(id ?? string.Empty, out found);

        /// <summary>
        /// 판마다 북을 받을 사람을 무작위로 정한다. 북이 인원 이하이면 서로 다른 사람이 받고,
        /// 더 많으면 모두 하나씩 받은 뒤 남는 북도 무작위로 나눈다. Host만 뽑으므로 모든 기기가
        /// 스냅샷으로 같은 결과를 받는다. 북은 받은 사람 판의 가운데에서 시작한다.
        /// </summary>
        private void PlaceDrums(double now)
        {
            int[] shuffled = Enumerable.Range(0, activeSeatCount)
                .OrderBy(seat => DrawFor(seed ^ DrumSeatSalt, (ulong)seat, 0u)).ToArray();
            var perBoard = new int[activeSeatCount];
            for (int i = 0; i < rules.DrumCount; i++)
            {
                int seat = i < activeSeatCount ? shuffled[i]
                    : (int)(DrawFor(seed ^ DrumSeatSalt, ulong.MaxValue, (uint)i) % (ulong)activeSeatCount);
                var drum = new LabOrbState
                {
                    id = NewOrbId(), owner = seats[seat], kind = LabOrbKind.Drum,
                    x = DrumStartX[perBoard[seat]++], y = .5f, createdAt = now, revision = ++revision
                };
                drums.Add(drum.id, drum);
            }
        }

        /// <summary>
        /// 북에서 튀어나가는 속도 (보드 너비/초). 방향은 위쪽 기준 좌우 각도 안(180°면 모든 방향),
        /// 속도는 최소~최대 사이에서 Host가 뽑는다. 종류 뽑기와 같은 seed 방식이라
        /// 한 사람의 생성이 다른 사람의 결과를 바꾸지 않고, 모든 기기가 스냅샷으로 같은 값을 받는다.
        /// 북이 옆 가장자리 가까이 있어도 나오자마자 이웃에게 넘어가지 않도록, 바닥 감속으로
        /// 계산한 옆 이동 거리가 그쪽 가장자리까지 남은 거리를 넘으면 그만큼만 속도를 줄인다.
        /// </summary>
        private Vector2 SpawnVelocityFor(uint drawSeed, ulong playerId, uint index, float originX)
        {
            float angle = Mathf.Lerp(-rules.SpawnFanAngle, rules.SpawnFanAngle,
                Unit01(DrawFor(drawSeed ^ SpawnAngleSalt, playerId, index))) * Mathf.Deg2Rad;
            float speed = Mathf.Lerp(rules.SpawnMinSpeed, rules.SpawnMaxSpeed,
                Unit01(DrawFor(drawSeed ^ SpawnSpeedSalt, playerId, index)));
            float sin = Mathf.Sin(angle);
            float side = Mathf.Abs(sin);
            if (rules.FloorDeceleration > 0f && side > .0001f)
            {
                float room = sin > 0f ? 1f - SpawnEdgeMargin - originX : originX - SpawnEdgeMargin;
                // 멈출 때까지 옆으로 가는 거리 = 속도² × sin / (2 × 감속)
                float safeSpeed = Mathf.Sqrt(2f * rules.FloorDeceleration * Mathf.Max(0f, room) / side);
                speed = Mathf.Min(speed, safeSpeed);
            }
            speed = Mathf.Min(speed, rules.MaximumMotionSpeed);
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;
        }

        private static float Unit01(ulong draw) => (float)((draw >> 11) * (1.0 / (1UL << 53)));
        private string NewOrbId()
        {
            string id;
            do id = Guid.NewGuid().ToString("N"); while (orbs.ContainsKey(id) || drums.ContainsKey(id));
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

        /// <summary>
        /// 결합 구슬의 오행 속성을 정한다. 다섯 속성이 같은 확률로 나온다.
        /// Host만 호출하므로 모든 기기가 스냅샷으로 같은 결과를 받는다.
        /// 규칙을 바꿀 때는 이 함수만 고치면 된다.
        /// </summary>
        private static LabOrbElement ElementFor(uint seed, uint combinedIndex)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ 0xE1E3E7A5UL;
                value += 0x9E3779B97F4A7C15UL * (combinedIndex + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (LabOrbElement)(1 + (int)(value % 5UL));
            }
        }

        // Counter-based Host draw: one player's accepted generation cannot change another's
        // sequence. Each draw is independent, so results follow the configured percentages.
        private static LabOrbKind GeneratedKindFor(uint seed, ulong playerId, uint index, int[] chances)
        {
            int total = 0;
            foreach (int chance in chances) total += chance;
            int roll = (int)(DrawFor(seed, playerId, index) % (ulong)total);
            for (int i = 0; i < chances.Length; i++)
            {
                roll -= chances[i];
                if (roll < 0) return LabConfig.GeneratedKinds[i];
            }
            return LabOrbKind.Yin;
        }

        private int LockSwipesFor(uint seed, ulong playerId, uint index)
        {
            int span = rules.ItemUnlockSwipesMax - rules.ItemUnlockSwipesMin + 1;
            return rules.ItemUnlockSwipesMin + (int)(DrawFor(seed ^ LockDrawSalt, playerId, index) % (ulong)span);
        }

        private static ulong DrawFor(uint seed, ulong playerId, uint index)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ playerId;
                value += 0x9E3779B97F4A7C15UL * (index + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return value;
            }
        }

        private sealed class Rules
        {
            public readonly int MonsterMaxHp, PlayerMaxHp, PotionHeal, Damage, OrbStorageLimit;
            public readonly float RelayDoubleMultiplier, RelayMaxMultiplier, BombMultiplier;
            public readonly float BattleDurationSeconds, StaminaMax, StaminaStart, GenerateCost,
                StaminaRecoveryPerSecond, HitRecovery, OrbLifetimeSeconds, MaximumMotionSpeed, ItemLifetimeSeconds;
            public readonly float SpawnFanAngle, SpawnMinSpeed, SpawnMaxSpeed, FloorDeceleration;
            public readonly int[] GenerationChances;
            public readonly int ItemUnlockSwipesMin, ItemUnlockSwipesMax, DrumCount;

            public Rules(LabConfig config)
            {
                GenerationChances = config.GenerationChances();
                ItemLifetimeSeconds = config.ItemLifetimeSeconds;
                ItemUnlockSwipesMin = config.ItemUnlockSwipesMin;
                ItemUnlockSwipesMax = config.ItemUnlockSwipesMax;
                MonsterMaxHp = config.MonsterMaxHp;
                PlayerMaxHp = config.PlayerMaxHp;
                PotionHeal = config.PotionHeal;
                RelayDoubleMultiplier = config.RelayDoubleMultiplier;
                RelayMaxMultiplier = config.RelayMaxMultiplier;
                BombMultiplier = config.BombMultiplier;
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
                SpawnFanAngle = config.SpawnFanAngle;
                SpawnMinSpeed = config.SpawnMinSpeed;
                SpawnMaxSpeed = config.SpawnMaxSpeed;
                FloorDeceleration = config.OrbFloorDeceleration;
                DrumCount = config.DrumCount;
            }
        }
    }
}
