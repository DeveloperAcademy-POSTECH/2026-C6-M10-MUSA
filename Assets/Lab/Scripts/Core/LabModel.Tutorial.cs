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

        /// <summary>Scripted combinations still waiting.</summary>
        public int TutorialScriptedCombinationsLeft => tutorialCombinations.Count;

        private LabOrbElement TutorialScriptedElement(ulong sender, LabOrbElement drawn) =>
            tutorialCombinations.Count > 0 && sender == tutorialCombinationOwner ? tutorialCombinations.Dequeue() : drawn;
    }
}
