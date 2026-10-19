using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// A locked bomb that reached its fuse: the board it blasts and where, in 0..1 board units,
    /// its holder last reported it.
    /// </summary>
    public readonly struct LabBombBlast
    {
        public readonly ulong Owner;
        public readonly string BombId;
        public readonly Vector2 Position;

        public LabBombBlast(ulong owner, string bombId, Vector2 position)
        {
            Owner = owner;
            BombId = bombId;
            Position = position;
        }
    }

    /// <summary>
    /// A locked bomb the gourd dropped onto one player's board as sabotage (#177): who receives it
    /// and where, in 0..1 board units, it lands.
    /// </summary>
    public readonly struct LabGourdBomb
    {
        public readonly ulong Target;
        public readonly string BombId;
        public readonly Vector2 Position;

        public LabGourdBomb(ulong target, string bombId, Vector2 position)
        {
            Target = target;
            BombId = bombId;
            Position = position;
        }
    }

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
        private const uint GourdDelaySalt = 0x60BDDE1Au;
        private const uint GourdTargetSalt = 0x60BD7A26u;
        private const uint GourdLandingSalt = 0x60BD1A4Du;
        private const uint SwipeBlockTargetSalt = 0x5B10C7A6u;
        private const uint SwipeBlockSideSalt = 0x5B105D1Eu;
        // 박의 넘기기 막기 뽑기도 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (#215).
        private const ulong SwipeBlockDrawKey = ulong.MaxValue - 3UL;
        private const uint DrumZoneSalt = 0xD2017E5Au;
        private const uint DrumSpotSalt = 0xD2051075u;
        // 북 전달 뽑기는 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다.
        private const ulong DrumHandoffKey = ulong.MaxValue - 2UL;
        // 박 폭탄 뽑기는 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (북 자리 뽑기는 ulong.MaxValue).
        private const ulong GourdDrawKey = ulong.MaxValue - 1UL;
        // 박 폭탄이 떨어지는 곳: 판 너비의 20~80% 사이, 판 높이의 이 지점 (보드가 영역 안으로 맞춘다).
        private const float GourdLandingMinX = .2f, GourdLandingMaxX = .8f, GourdLandingY = .6f;
        // DEV에서 간격을 아주 작게 넣어도 한 프레임에 폭탄이 쏟아지지 않도록 하는 최소 간격.
        private const double GourdMinimumDelaySeconds = 1d;
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
        private readonly List<LabBombBlast> bombExplosions = new List<LabBombBlast>();
        /// <summary>When the gourd last dropped bombs, so a swipe block keeps clear of them (#215).</summary>
        private readonly List<double> recentGourdBombTimes = new List<double>();
        private double nextSwipeBlockAt;
        private uint swipeBlockCount;
        private ulong swipeBlockedPlayer;
        private bool swipeBlockedRight;
        private double swipeBlockStartedAt = -1d, swipeBlockedUntil = -1d;
        /// <summary>Blasts sent to their holders and not yet reported back, by bomb ID, with their Host time (#213).</summary>
        private readonly Dictionary<string, KeyValuePair<ulong, double>> unreportedBlasts =
            new Dictionary<string, KeyValuePair<ulong, double>>();
        /// <summary>How long after a blast the Host still takes its holder's report of the orbs it flung.</summary>
        public const double BlastReportWindowSeconds = 3d;
        /// <summary>More orb IDs than any board holds; a longer report is dropped.</summary>
        public const int MaximumBlastReportOrbs = 64;
        private readonly List<LabGourdBomb> gourdBombs = new List<LabGourdBomb>();
        private double nextGourdBombAt;
        private uint gourdBombCount;
        private uint drumHandoffCount;
        private Rules rules;
        private string sessionId;
        private uint round;
        private uint seed;
        private uint combinedCount;
        private ulong revision;
        private int hp;
        private readonly LabPaintSurface paintSurface = new LabPaintSurface();
        private double lastTime;
        private double deadline;
        private double remaining;
        private double played;
        private LabPhase phase = LabPhase.Idle;

        public string SessionId => sessionId;
        public uint Round => round;
        public LabPhase Phase => phase;
        public int Hp => hp;
        /// <summary>Round-owned CPU paint state; rendering reads this state and never decides victory.</summary>
        public LabPaintSurface PaintSurface => paintSurface;

        /// <summary>The element palette captured when this round began, independent of later material edits.</summary>
        public Color PaintColorFor(LabOrbElement element) => rules != null
            && element >= LabOrbElement.Fire && element <= LabOrbElement.Earth
            ? rules.PaintColors[(int)element] : Color.clear;

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
            unreportedBlasts.Clear();
            gourdBombs.Clear();
            recentGourdBombTimes.Clear();
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
            paintSurface.Reset(rules.PaintGrowthSettings, seed);
            revision = 0;
            PlaceDrums(now);
            gourdBombCount = 0;
            drumHandoffCount = 0;
            nextGourdBombAt = now + GourdBombDelay(0u);
            swipeBlockCount = 0;
            swipeBlockedPlayer = 0;
            swipeBlockedRight = false;
            swipeBlockStartedAt = swipeBlockedUntil = -1d;
            nextSwipeBlockAt = rules.SwipeBlockInterval > 0f ? now + rules.SwipeBlockInterval : double.PositiveInfinity;
            lastTime = now;
            deadline = nextDeadline;
            remaining = rules.BattleDurationSeconds;
            played = 0d;
            phase = LabPhase.Playing;
        }

        /// <summary>Advance the absolute Host clock; only Idle orbs expire.</summary>
        public void Tick(double now)
        {
            if (!TryAdvance(now, out string reason)) throw new ArgumentOutOfRangeException(nameof(now), reason);
        }

        /// <summary>
        /// Install the Host's next local gravity sample after consuming the previous interval.
        /// Requests and snapshots share this state; they do not resample target pose themselves.
        /// </summary>
        public bool SetPaintFlowDirection(Vector3 localDown, double now)
        {
            float magnitude = localDown.sqrMagnitude;
            if (!Finite(localDown.x) || !Finite(localDown.y) || !Finite(localDown.z)
                || !Finite(magnitude) || magnitude < .00001f) return false;
            if (!TryAdvance(now, out _)) return false;
            return paintSurface.SetFlowDirection(localDown);
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
            player.generatedTotal++;
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
                vx = velocity.x, vy = velocity.y, createdAt = now, revision = ++revision, inFlight = false,
                // One blast-shrunk half makes the whole orb small, and its fire burns on (#213).
                small = first.small || second.small, burnedAt = Math.Max(first.burnedAt, second.burnedAt)
            };
            orbs.Add(orb.id, orb);
            players[sender].combinedTotal++;
            reason = "COMBINED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// A right edge enters the next seat from its left; a left edge enters the previous seat
        /// from its right. The same ID, creation time, and velocity survive the handoff.
        /// Each handoff of a locked item removes one lock, so items unlock only by teamwork. A relay
        /// orb becomes x1 when it unlocks, then each further handoff raises it to x2 and MAX. A bomb
        /// that unlocks is defused into an attack orb: its fuse is cut and a fresh orb lifetime starts.
        /// </summary>
        public bool TryTransfer(ulong sender, string id, bool right, float height01,
            Vector2 normalizedVelocity, string requestId, double now, out LabOrbState orb, out string reason)
            => TryTransfer(sender, id, right, height01, normalizedVelocity, requestId, now,
                LabEdgeCrossingKind.Swipe, out orb, out reason);

        /// <summary>
        /// The same handoff, told why the body crossed. A drum also gets how it arrives on the next
        /// board (#178); see <see cref="ArriveDrum"/>.
        /// </summary>
        public bool TryTransfer(ulong sender, string id, bool right, float height01, Vector2 normalizedVelocity,
            string requestId, double now, LabEdgeCrossingKind crossing, out LabOrbState orb, out string reason)
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
            // The gourd's swipe block (#215) stops only this player's own passes that way; a blast's push
            // and a drum running on through still go, and nothing coming in is stopped.
            if (crossing == LabEdgeCrossingKind.Swipe && SwipeBlocked(sender, right, now))
                return Reject("SWIPE_BLOCKED", out reason);
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
            if (current.kind.IsDrum()) ArriveDrum(current, crossing, now);
            current.revision = ++revision;
            if (current.kind.IsDrum()) players[sender].drumPassedTotal++;
            else players[sender].passedTotal++;
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
            // 릴레이·폭탄은 던지는 것이 곧 아이템 사용이다.
            if (current.kind == LabOrbKind.Relay) players[sender].relayUsed++;
            else if (current.kind == LabOrbKind.Bomb) players[sender].bombUsed++;
            reason = "LAUNCHED";
            orb = current.Copy();
            return true;
        }

        /// <summary>Call only after the Host projectile actually collides with the cylinder target.</summary>
        public bool ConfirmHit(string id, double now)
            => ConfirmHit(id, now, out _, out _);

        /// <summary>
        /// Compatibility path for callers without a surface direction. Confirms damage and hit
        /// rewards, but cannot add paint or decide a paint victory from an external coverage value.
        /// </summary>
        public bool ConfirmHit(string id, double now, out LabOrbElement element, out int actualDamage)
            => ConfirmHit(id, now, Vector3.zero, out element, out actualDamage, out _);

        /// <summary>
        /// Atomically confirm one Host-owned projectile collision, rewards, CPU paint and victory.
        /// actualDamage is the HP really removed. impactValue is the positive presentation/attack
        /// value in Paint (even after its diagnostic HP reaches zero), and actualDamage in Crack.
        /// </summary>
        public bool ConfirmHit(string id, double now, Vector3 paintDirection,
            out LabOrbElement element, out int actualDamage, out int impactValue)
        {
            element = LabOrbElement.None;
            actualDamage = 0;
            impactValue = 0;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing
                || !orbs.TryGetValue(id ?? string.Empty, out var orb) || !orb.inFlight
                || !players.TryGetValue(orb.owner, out var attacker)) return false;
            element = orb.element;
            orbs.Remove(id);
            int attackValue = DamageFor(orb);
            int previousHp = hp;
            hp = Math.Max(0, hp - attackValue);
            actualDamage = previousHp - hp;
            bool paintRound = rules.GourdMode == LabGourdMode.Paint;
            impactValue = paintRound ? attackValue : actualDamage;
            attacker.stamina = Mathf.Min(rules.StaminaMax, attacker.stamina + rules.HitRecovery);
            attacker.hitTotal++;
            // Paint keeps the existing attack-score statistic; Crack records actual HP removed.
            // Long paint rounds can outlive diagnostic HP, so prevent the integer score wrapping.
            attacker.damageTotal = (int)Math.Min(int.MaxValue,
                (long)attacker.damageTotal + (paintRound ? attackValue : actualDamage));

            if (paintRound)
            {
                if (element >= LabOrbElement.Fire && element <= LabOrbElement.Earth
                    && paintSurface.RegisterHit(paintDirection, PaintColorFor(element), attacker.id, attacker.seat + 1,
                        orb.small ? rules.SmallOrbPaintShare : 1f)
                    && paintSurface.IsComplete)
                {
                    phase = LabPhase.Victory;
                    if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                }
            }
            else if (hp == 0)
            {
                phase = LabPhase.Victory;
                attacker.lastHit = true;
            }
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
                players[sender].secretBoxUsed++;
                reason = "ITEM_USED";
                orb = current.Copy();
                return true;
            }
            var player = players[sender];
            if (player.personalHp <= 0) return Reject("PLAYER_DOWN", out reason);
            if (player.personalHp >= rules.PlayerMaxHp) return Reject("HP_FULL", out reason);
            player.personalHp = Math.Min(rules.PlayerMaxHp, player.personalHp + rules.PotionHeal);
            player.potionUsed++;
            orbs.Remove(current.id);
            reason = "ITEM_USED";
            orb = current.Copy();
            return true;
        }

        /// <summary>
        /// The holder's board reports which orbs a blast flung (#213). Only that board knows where its
        /// orbs were, so it names them; the Host checks that it sent this blast to this player a moment
        /// ago and that each orb is still theirs on the board, then sets it alight and shrinks it for the
        /// rest of its life. Drums and bombs never burn. Each blast is taken once. Returns how many
        /// orbs caught fire.
        /// </summary>
        public int MarkBlasted(ulong sender, string bombId, IReadOnlyList<string> orbIds, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing) return 0;
            foreach (string stale in unreportedBlasts.Where(blast => now - blast.Value.Value > BlastReportWindowSeconds)
                .Select(blast => blast.Key).ToArray())
                unreportedBlasts.Remove(stale);
            if (bombId == null || orbIds == null || orbIds.Count > MaximumBlastReportOrbs
                || !unreportedBlasts.TryGetValue(bombId, out var blast) || blast.Key != sender)
                return 0;
            unreportedBlasts.Remove(bombId);
            int burned = 0;
            foreach (string id in orbIds.Distinct())
            {
                if (id == null || !orbs.TryGetValue(id, out var orb) || orb.owner != sender || orb.inFlight
                    || orb.kind == LabOrbKind.Bomb || orb.kind.IsDrum())
                    continue;
                orb.burnedAt = now;
                orb.small = true;
                orb.revision = ++revision;
                burned++;
            }
            return burned;
        }

        /// <summary>Locked bombs that blasted at their fuse since the last call, each reported once.</summary>
        public LabBombBlast[] DrainBombExplosions()
        {
            LabBombBlast[] blasts = bombExplosions.ToArray();
            bombExplosions.Clear();
            return blasts;
        }

        /// <summary>Bombs the gourd dropped since the last call, each reported once.</summary>
        public LabGourdBomb[] DrainGourdBombs()
        {
            LabGourdBomb[] dropped = gourdBombs.ToArray();
            gourdBombs.Clear();
            return dropped;
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

        private bool BlockOn(double now) => phase == LabPhase.Playing && swipeBlockedUntil > now && now >= swipeBlockStartedAt;

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
                playedSeconds = played,
                hp = hp,
                paintCoverage = paintSurface.Coverage,
                paintGoalPercent = rules.PaintVictoryPercent,
                paintColumns = paintSurface.Columns,
                paintRows = paintSurface.Rows,
                paintRevision = paintSurface.Revision,
                paintCoveredCount = paintSurface.CoveredCount,
                paintCellCount = paintSurface.CellCount,
                paintCount = paintSurface.PaintCount,
                players = players.Values.OrderBy(player => player.seat).Select(player => player.Copy()).ToArray(),
                orbs = orbs.Values.OrderBy(orb => orb.id, StringComparer.Ordinal).Select(orb => orb.Copy()).ToArray(),
                drums = drums.Values.OrderBy(drum => drum.id, StringComparer.Ordinal).Select(drum => drum.Copy()).ToArray(),
                swipeBlockedPlayer = BlockOn(now) ? swipeBlockedPlayer : 0,
                swipeBlockedRight = BlockOn(now) && swipeBlockedRight,
                swipeBlockStartedAt = BlockOn(now) ? swipeBlockStartedAt : -1d,
                swipeBlockedUntil = BlockOn(now) ? swipeBlockedUntil : -1d
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
                bool paintCompleted = false;
                if (rules.GourdMode == LabGourdMode.Paint && elapsed > 0d)
                {
                    // A late frame still resolves growth that was due before the deadline.
                    // Growth due exactly at the deadline cannot outrun the existing timeout rule.
                    double completionOffset = paintSurface.Advance(elapsed, now < deadline);
                    if (completionOffset >= 0d)
                    {
                        effectiveNow = lastTime + completionOffset;
                        elapsed = completionOffset;
                        paintCompleted = true;
                    }
                }
                if (elapsed > 0d)
                {
                    played += elapsed;
                    foreach (var player in players.Values)
                        player.stamina = Mathf.Min(rules.StaminaMax,
                            (float)(player.stamina + elapsed * rules.StaminaRecoveryPerSecond));
                    // 결과 대시보드 기록: 이 사이에 북을 점유한 사람의 점유 시간 (북 점유율 계산용)
                    // (북은 넘길 때도 먼저 시간을 진행시키므로 넘기기 전 주인에게 쌓인다)
                    foreach (var holder in drums.Values.Select(drum => drum.owner).Distinct())
                        if (players.TryGetValue(holder, out var drummer)) drummer.drumSeconds += elapsed;
                }
                DropGourdBombs(effectiveNow);
                StartSwipeBlocks(effectiveNow);
                foreach (var expired in orbs.Values.Where(orb => !orb.inFlight && effectiveNow - orb.createdAt >= LifetimeOf(orb))
                    .ToArray())
                {
                    if (!orbs.Remove(expired.id) || expired.kind != LabOrbKind.Bomb || expired.lockSwipes == 0) continue;
                    // A bomb still locked at the end of its fuse blasts on its current holder's board. No
                    // orb is lost: the holder's screen pushes the ones around it away from where it last was.
                    bombExplosions.Add(new LabBombBlast(expired.owner, expired.id, new Vector2(expired.x, expired.y)));
                    unreportedBlasts[expired.id] = new KeyValuePair<ulong, double>(expired.owner, effectiveNow);
                }
                remaining = Math.Max(0d, deadline - effectiveNow);
                if (paintCompleted)
                {
                    phase = LabPhase.Victory;
                    if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                }
                else if (now >= deadline) phase = LabPhase.Defeat;
            }
            lastTime = now;
            return true;
        }

        /// <summary>
        /// 박의 방해공작 (#177): 정해 둔 시각마다 무작위 한 명의 판에 잠긴 폭탄을 떨어뜨린다.
        /// 구슬이 가득 찬 판에도 떨어지고, 같은 사람이 연달아 받을 수도 있으며, 스태미나는 쓰지 않는다.
        /// 잠금 횟수·도화선은 북에서 나온 폭탄과 같다: 잠긴 채 도화선(아이템 수명)이 끝나면 그때 들고 있는 사람 판에서 터진다. 시간을 한 번에 많이 건너뛰어도 그 사이
        /// 떨어졌어야 할 폭탄을 정해 둔 시각 그대로 차례대로 만든다.
        /// </summary>
        private void DropGourdBombs(double until)
        {
            while (nextGourdBombAt <= until)
            {
                double at = nextGourdBombAt;
                uint index = gourdBombCount++;
                ulong target = seats[(int)(DrawFor(seed ^ GourdTargetSalt, GourdDrawKey, index) % (ulong)activeSeatCount)];
                var landing = new Vector2(
                    Mathf.Lerp(GourdLandingMinX, GourdLandingMaxX, Unit01(DrawFor(seed ^ GourdLandingSalt, GourdDrawKey, index))),
                    GourdLandingY);
                var bomb = new LabOrbState
                {
                    id = NewOrbId(), owner = target, kind = LabOrbKind.Bomb,
                    lockSwipes = LockSwipesFor(seed, GourdDrawKey, index),
                    origin = LabOrbOrigin.Gourd, originOwner = target,
                    x = landing.x, y = landing.y, createdAt = at, revision = ++revision, inFlight = false
                };
                orbs.Add(bomb.id, bomb);
                gourdBombs.Add(new LabGourdBomb(target, bomb.id, landing));
                recentGourdBombTimes.Add(at);
                if (recentGourdBombTimes.Count > 8) recentGourdBombTimes.RemoveAt(0);
                nextGourdBombAt = at + GourdBombDelay(gourdBombCount);
            }
        }

        /// <summary>
        /// 옆 사람에게 넘어가는 북이 받는 판에서 어떻게 들어올지 Host가 정한다 (#178).
        /// 스와이프로 넘기면 가까운 쪽·가운데·먼 쪽 구간 또는 통과를 설정 확률로 뽑고, 구간 안의 멈출
        /// 자리(들어온 가장자리에서 판 폭 비율)도 뽑는다. 앞 판을 통과해 온 북은 이번 판의 가까운 쪽에서
        /// 반드시 멈추고, 폭발에 밀려 넘어온 북은 제 힘대로 미끄러지되 먼 쪽 끝 안에서 멈춘다.
        /// 뽑기는 seed와 전달 순번으로 정해져 모든 기기가 스냅샷으로 같은 결과를 받는다.
        /// </summary>
        private void ArriveDrum(LabOrbState drum, LabEdgeCrossingKind crossing, double now)
        {
            uint index = drumHandoffCount++;
            float[] bounds = rules.DrumStopBounds;
            int zone;
            if (crossing == LabEdgeCrossingKind.Pushed) zone = -1;
            // Only a drum that really arrived as a pass may claim to be passing on.
            else if (crossing == LabEdgeCrossingKind.Passing && drum.arrival == LabDrumArrival.Pass) zone = 0;
            else zone = DrumZoneFor(DrawFor(seed ^ DrumZoneSalt, DrumHandoffKey, index), rules.DrumStopChances);

            drum.arrivedAt = now;
            if (zone < 0)
            {
                drum.arrival = LabDrumArrival.Coast;
                drum.stopAt = bounds[3];
            }
            else if (zone >= 3)
            {
                drum.arrival = LabDrumArrival.Pass;
                drum.stopAt = -1f;
            }
            else
            {
                drum.arrival = LabDrumArrival.Stop;
                drum.stopAt = Mathf.Lerp(bounds[zone], bounds[zone + 1],
                    Unit01(DrawFor(seed ^ DrumSpotSalt, DrumHandoffKey, index)));
            }
        }

        /// <summary>0 near, 1 middle, 2 far, 3 pass, by the configured percentages.</summary>
        private static int DrumZoneFor(ulong draw, int[] chances)
        {
            int roll = (int)(draw % 100UL);
            for (int i = 0; i < chances.Length; i++)
            {
                roll -= chances[i];
                if (roll < 0) return i;
            }
            return 0;
        }

        /// <summary>다음 박 폭탄까지의 시간. 최대 간격이 0이면 떨어뜨리지 않는다.</summary>
        /// <summary>
        /// 박의 방해공작 (#215): 정해 둔 간격마다 무작위 한 명과 무작위 방향(왼쪽·오른쪽)을 골라, 그 사람이
        /// 그쪽으로 스스로 넘기는 것을 정해 둔 시간 동안 막는다. 박 폭탄과 연출이 겹치지 않도록 폭탄이
        /// 떨어졌거나 떨어질 시각과 최소 간격 안이면 그 폭탄 뒤로 미룬다. 한 번에 하나만 걸리고, 다음 막기는
        /// 지금 것이 끝난 뒤에 온다. 뽑기는 seed와 순번으로 정해 모든 기기가 같은 결과를 받는다.
        /// </summary>
        private void StartSwipeBlocks(double until)
        {
            while (nextSwipeBlockAt <= until)
            {
                double at = nextSwipeBlockAt;
                double clear = ClearOfGourdBombs(at);
                if (clear > at)
                {
                    nextSwipeBlockAt = clear;
                    continue;
                }
                uint index = swipeBlockCount++;
                swipeBlockedPlayer = seats[(int)(DrawFor(seed ^ SwipeBlockTargetSalt, SwipeBlockDrawKey, index)
                    % (ulong)activeSeatCount)];
                swipeBlockedRight = (DrawFor(seed ^ SwipeBlockSideSalt, SwipeBlockDrawKey, index) & 1UL) == 1UL;
                swipeBlockStartedAt = at;
                swipeBlockedUntil = at + rules.SwipeBlockSeconds;
                nextSwipeBlockAt = Math.Max(at + rules.SwipeBlockInterval, swipeBlockedUntil);
            }
        }

        /// <summary>The earliest time from at that is at least the bomb gap from the gourd's bombs near it.</summary>
        private double ClearOfGourdBombs(double at)
        {
            double gap = rules.SwipeBlockBombGap;
            if (gap <= 0d) return at;
            double clear = at;
            foreach (double bomb in recentGourdBombTimes)
                if (Math.Abs(clear - bomb) < gap) clear = Math.Max(clear, bomb + gap);
            if (Math.Abs(clear - nextGourdBombAt) < gap) clear = Math.Max(clear, nextGourdBombAt + gap);
            return clear;
        }

        /// <summary>Whether the gourd's swipe block (#215) stops this player's own passes that way at this Host time.</summary>
        public bool SwipeBlocked(ulong player, bool right, double now) =>
            phase == LabPhase.Playing && swipeBlockedUntil > now && now >= swipeBlockStartedAt
            && player == swipeBlockedPlayer && right == swipeBlockedRight;

        private double GourdBombDelay(uint index)
        {
            if (rules.GourdBombIntervalMax <= 0f) return double.PositiveInfinity;
            float delay = Mathf.Lerp(rules.GourdBombIntervalMin, rules.GourdBombIntervalMax,
                Unit01(DrawFor(seed ^ GourdDelaySalt, GourdDrawKey, index)));
            return Math.Max(GourdMinimumDelaySeconds, delay);
        }

        private int DamageFor(LabOrbState orb)
        {
            if (orb.kind == LabOrbKind.Bomb) return Math.Max(1, (int)Math.Round(rules.Damage * rules.BombMultiplier));
            if (orb.kind != LabOrbKind.Relay) return rules.Damage;
            float multiplier = orb.relayStage >= RelayMaxStage ? rules.RelayMaxMultiplier
                : orb.relayStage == 2 ? rules.RelayDoubleMultiplier : 1f;
            return Math.Max(1, (int)Math.Round(rules.Damage * multiplier));
        }

        // Items last the item lifetime; for a locked bomb that is its fuse. An unlocked bomb is an
        // ordinary attack orb and keeps the orb lifetime.
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
            /// <summary>How much a blast-shrunk orb paints, against a whole one (#213).</summary>
            public readonly float SmallOrbPaintShare;
            public readonly float BattleDurationSeconds, StaminaMax, StaminaStart, GenerateCost,
                StaminaRecoveryPerSecond, HitRecovery, OrbLifetimeSeconds, MaximumMotionSpeed, ItemLifetimeSeconds;
            public readonly float SpawnFanAngle, SpawnMinSpeed, SpawnMaxSpeed, FloorDeceleration;
            public readonly int[] GenerationChances;
            public readonly int ItemUnlockSwipesMin, ItemUnlockSwipesMax, DrumCount;
            public readonly LabGourdMode GourdMode;
            public readonly int PaintVictoryPercent;
            public readonly LabPaintGrowthSettings PaintGrowthSettings;
            public readonly float GourdBombIntervalMin, GourdBombIntervalMax;
            /// <summary>The gourd's swipe block (#215): every how long, for how long, and how far from its bombs.</summary>
            public readonly float SwipeBlockInterval, SwipeBlockSeconds, SwipeBlockBombGap;
            public readonly float[] DrumStopBounds;
            public readonly int[] DrumStopChances;
            public readonly Color[] PaintColors;

            public Rules(LabConfig config)
            {
                GenerationChances = config.GenerationChances();
                ItemLifetimeSeconds = config.ItemLifetimeSeconds;
                ItemUnlockSwipesMin = config.ItemUnlockSwipesMin;
                ItemUnlockSwipesMax = config.ItemUnlockSwipesMax;
                MonsterMaxHp = config.MonsterMaxHp;
                GourdMode = config.GourdMode;
                PaintVictoryPercent = config.PaintVictoryPercent;
                PaintGrowthSettings = config.PaintGrowthSettings;
                GourdBombIntervalMin = config.GourdBombIntervalMin;
                GourdBombIntervalMax = config.GourdBombIntervalMax;
                SwipeBlockInterval = config.SwipeBlockInterval;
                SwipeBlockSeconds = config.SwipeBlockSeconds;
                SwipeBlockBombGap = config.SwipeBlockBombGap;
                DrumStopBounds = config.DrumStopBounds();
                DrumStopChances = config.DrumStopChances();
                PaintColors = new Color[(int)LabOrbElement.Earth + 1];
                for (int i = (int)LabOrbElement.Fire; i <= (int)LabOrbElement.Earth; i++)
                    PaintColors[i] = LabPaintPalette.ColorFor((LabOrbElement)i);
                PlayerMaxHp = config.PlayerMaxHp;
                PotionHeal = config.PotionHeal;
                RelayDoubleMultiplier = config.RelayDoubleMultiplier;
                RelayMaxMultiplier = config.RelayMaxMultiplier;
                BombMultiplier = config.BombMultiplier;
                SmallOrbPaintShare = config.SmallOrbPaintShare;
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
