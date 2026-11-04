using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 튜토리얼 (#477) 연습 판의 대본 장치.
    /// - 판을 연 직후 북을 플레이어 한 사람의 판 가운데에 모은다. 보통 판은 북 자리를 seed로 뽑지만, 튜토리얼은 언제나
    ///   가운데 앉은 "나"가 북을 들고 시작해야 첫 동작(북 치기)을 바로 할 수 있다.
    /// - 북에서 나올 구슬 종류를 차례로 정해 둔다 (1단계: 음·양·음). 합쳐서 나올 오행도 정해 둔다 (2단계: 화).
    /// - 기운 회복을 멈추거나 정한 만큼까지만 차게 한다 (1단계: 바닥난 막대가 살짝만 차서 기운 부족 빨간 빛이 보이게).
    ///   막힌 동안의 회복은 나중에 한꺼번에 들어오지 않는다.
    /// </summary>
    public sealed partial class LabModel
    {
        private struct ScriptedGeneration
        {
            public LabOrbKind Kind;
            public bool HasLaunch;
            public Vector2 Launch;
        }

        private readonly Queue<ScriptedGeneration> tutorialGenerations = new Queue<ScriptedGeneration>();
        private ulong tutorialGenerationOwner;
        private readonly Queue<LabOrbElement> tutorialCombinations = new Queue<LabOrbElement>();
        private ulong tutorialCombinationOwner;
        private float tutorialStaminaCeiling = float.PositiveInfinity;

        /// <summary>Whether stamina refill is held or capped for the tutorial.</summary>
        public bool TutorialStaminaHeld => !float.IsPositiveInfinity(tutorialStaminaCeiling);
        /// <summary>How far stamina may refill right now; infinite when the tutorial does not limit it.</summary>
        public float TutorialStaminaCeiling => tutorialStaminaCeiling;
        /// <summary>Scripted generations still waiting for their taps.</summary>
        public int TutorialScriptedGenerationsLeft => tutorialGenerations.Count;

        /// <summary>Gives every drum to <paramref name="owner"/> at the center of that board. False before a round or for a stranger.</summary>
        public bool TutorialHandDrumsTo(ulong owner)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(owner)) return false;
            foreach (LabOrbState drum in drums.Values)
            {
                drum.owner = owner;
                drum.x = .5f;
                drum.y = .5f;
                drum.vx = drum.vy = 0f;
                drum.revision = ++revision;
            }
            return true;
        }

        /// <summary>
        /// The next generations of <paramref name="owner"/> come out as <paramref name="kinds"/>, in order, replacing any
        /// earlier script. Stamina, storage and the launch are unchanged; only the kind is decided here.
        /// </summary>
        public bool TutorialScriptGenerations(ulong owner, params LabOrbKind[] kinds) =>
            TutorialScriptGenerations(owner, kinds, null);

        /// <summary>
        /// As above, and each orb leaves the drum with its own <paramref name="launches"/> velocity (board widths per
        /// second, +y up) instead of a drawn one, so it always comes to rest in the same place. Null keeps drawn launches.
        /// </summary>
        public bool TutorialScriptGenerations(ulong owner, IReadOnlyList<LabOrbKind> kinds, IReadOnlyList<Vector2> launches)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(owner) || kinds == null) return false;
            if (launches != null && launches.Count != kinds.Count) return false;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (!Enum.IsDefined(typeof(LabOrbKind), kinds[i]) || kinds[i].IsDrum()) return false;
                if (launches != null && !Finite(launches[i])) return false;
            }
            tutorialGenerations.Clear();
            tutorialGenerationOwner = owner;
            for (int i = 0; i < kinds.Count; i++)
                tutorialGenerations.Enqueue(new ScriptedGeneration
                {
                    Kind = kinds[i], HasLaunch = launches != null, Launch = launches != null ? launches[i] : Vector2.zero
                });
            return true;
        }

        /// <summary>Hold or release every player's stamina refill from <paramref name="now"/> on.</summary>
        public bool TutorialHoldStaminaRecovery(bool held, double now) =>
            TutorialCapStaminaRecovery(held ? 0f : float.PositiveInfinity, now);

        /// <summary>
        /// From <paramref name="now"/> on, stamina refills only up to <paramref name="ceiling"/> (0 holds it, infinity
        /// releases it). Stamina already above the ceiling is kept.
        /// </summary>
        public bool TutorialCapStaminaRecovery(float ceiling, double now)
        {
            if (float.IsNaN(ceiling) || ceiling < 0f) return false;
            // Settle the time before the switch with the rule that applied to it.
            if (phase != LabPhase.Playing || !TryAdvance(now, out _)) return false;
            tutorialStaminaCeiling = ceiling;
            return true;
        }

        /// <summary>The next scripted generation of <paramref name="sender"/>, if any: its kind, and its launch when set.</summary>
        private void TutorialTakeGeneration(ulong sender, ref LabOrbKind kind, out Vector2? launch)
        {
            launch = null;
            if (tutorialGenerations.Count == 0 || sender != tutorialGenerationOwner) return;
            ScriptedGeneration next = tutorialGenerations.Dequeue();
            kind = next.Kind;
            if (next.HasLaunch) launch = Vector2.ClampMagnitude(next.Launch, rules.MaximumMotionSpeed);
        }

        /// <summary>
        /// The next combinations of <paramref name="owner"/> make <paramref name="elements"/>, in order (2단계: 화).
        /// Replaces any earlier script; the combination itself is decided as always.
        /// </summary>
        public bool TutorialScriptCombinations(ulong owner, params LabOrbElement[] elements)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(owner) || elements == null) return false;
            foreach (LabOrbElement element in elements)
                if (element < LabOrbElement.Fire || element > LabOrbElement.Earth) return false;
            tutorialCombinations.Clear();
            tutorialCombinationOwner = owner;
            foreach (LabOrbElement element in elements) tutorialCombinations.Enqueue(element);
            return true;
        }

        /// <summary>
        /// 3단계: a missed throw is given back. A new orb of <paramref name="kind"/> (and <paramref name="element"/> for an
        /// elemental orb) appears on <paramref name="owner"/>'s board at <paramref name="landing"/> (0..1); the board shows
        /// it falling in from above. No stamina is spent.
        /// </summary>
        public bool TutorialDropOrb(ulong owner, LabOrbKind kind, LabOrbElement element, Vector2 landing, double now,
            out LabOrbState orb)
        {
            orb = null;
            if (phase != LabPhase.Playing || !players.ContainsKey(owner) || !Enum.IsDefined(typeof(LabOrbKind), kind)
                || kind.IsDrum() || !Unit(landing.x) || !Unit(landing.y)) return false;
            if (kind == LabOrbKind.Combined && (element < LabOrbElement.Fire || element > LabOrbElement.Earth)) return false;
            if (!TryAdvance(now, out _)) return false;
            var dropped = new LabOrbState
            {
                id = NewOrbId(), owner = owner, kind = kind,
                element = kind == LabOrbKind.Combined ? element : LabOrbElement.None,
                origin = kind == LabOrbKind.Combined ? LabOrbOrigin.Combined : LabOrbOrigin.Gourd, originOwner = owner,
                x = landing.x, y = landing.y, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(dropped.id, dropped);
            orb = dropped.Copy();
            return true;
        }

        /// <summary>
        /// 5단계: the gourd drops a bomb with exactly <paramref name="lockSwipes"/> locks on <paramref name="target"/>'s
        /// board at <paramref name="landing"/> (0..1), the same way as its random bombs (#177): it is reported once
        /// through <see cref="DrainGourdBombs"/>, so the warning, the yokai's throw and the fall from above all play.
        /// </summary>
        public bool TutorialDropGourdBomb(ulong target, Vector2 landing, int lockSwipes, double now, out LabOrbState bomb)
        {
            bomb = null;
            if (phase != LabPhase.Playing || !players.ContainsKey(target) || !Unit(landing.x) || !Unit(landing.y)
                || lockSwipes < 1 || lockSwipes > 20) return false;
            if (!TryAdvance(now, out _)) return false;
            gourdBombCount++;
            var made = new LabOrbState
            {
                id = NewOrbId(), owner = target, kind = LabOrbKind.Bomb, lockSwipes = lockSwipes,
                origin = LabOrbOrigin.Gourd, originOwner = target,
                x = landing.x, y = landing.y, createdAt = now, revision = ++revision, inFlight = false
            };
            orbs.Add(made.id, made);
            gourdBombs.Add(new LabGourdBomb(target, made.id, landing));
            bomb = made.Copy();
            return true;
        }

        /// <summary>How long a tutorial swipe block holds unless lifted: as long as the practice battle itself.</summary>
        public const double TutorialSwipeBlockHoldSeconds = 86400d;

        /// <summary>
        /// 6단계: the gourd shuts one of <paramref name="target"/>'s side edges (+1 right, -1 left) the same way as its
        /// timed swipe block (#215), so the edge glows red and that player's own passes bounce off it, but it holds until
        /// the tutorial lifts it with <paramref name="side"/> 0 instead of running out.
        /// </summary>
        public bool TutorialBlockSwipe(ulong target, int side, double now)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(target)) return false;
            if (!TryAdvance(now, out _)) return false;
            if (side == 0)
            {
                swipeBlockedPlayer = 0;
                swipeBlockedRight = false;
                swipeBlockStartedAt = swipeBlockedUntil = -1d;
                return true;
            }
            swipeBlockCount++;
            swipeBlockedPlayer = target;
            swipeBlockedRight = side > 0;
            swipeBlockStartedAt = now;
            swipeBlockedUntil = now + TutorialSwipeBlockHoldSeconds;
            return true;
        }

        private uint tutorialHandOvers;

        /// <summary>
        /// 4·5·6단계: a story neighbor (a virtual seat) passes <paramref name="orbId"/> on to <paramref name="to"/>, its
        /// next seat either way, as a real swipe over the shared edge would: through the same transfer, so a drum arrives
        /// as drums do and a locked bomb loses a lock. It enters at <paramref name="height"/> (0..1) moving inward at
        /// <paramref name="speed"/> board widths per second.
        /// </summary>
        public bool TutorialHandOver(ulong from, string orbId, ulong to, float height, float speed, double now,
            out string reason)
        {
            reason = "NOT_NEIGHBORS";
            if (phase != LabPhase.Playing || !players.TryGetValue(from, out LabPlayerState giver)
                || !players.TryGetValue(to, out LabPlayerState taker) || from == to) return false;
            bool right;
            if (LabPlayerSlots.NeighborSeat(giver.seat, activeSeatCount, true) == taker.seat) right = true;
            else if (LabPlayerSlots.NeighborSeat(giver.seat, activeSeatCount, false) == taker.seat) right = false;
            else return false;
            speed = Mathf.Clamp(speed, .01f, rules.MaximumMotionSpeed);
            var edge = new Vector2(right ? 1f : 0f, Mathf.Clamp01(height));
            var outward = new Vector2(right ? speed : -speed, 0f);
            return TryTransferAt(from, orbId, right, edge, outward, "tutorial-hand-over-" + ++tutorialHandOvers, now,
                LabEdgeCrossingKind.Swipe, out _, out reason);
        }

        /// <summary>Scripted combinations still waiting.</summary>
        public int TutorialScriptedCombinationsLeft => tutorialCombinations.Count;

        private LabOrbElement TutorialScriptedElement(ulong sender, LabOrbElement drawn) =>
            tutorialCombinations.Count > 0 && sender == tutorialCombinationOwner ? tutorialCombinations.Dequeue() : drawn;
    }
}
