using System;
using System.Collections.Generic;

namespace C6Lab
{
    /// <summary>
    /// 튜토리얼 (#477) 연습 판의 대본 장치.
    /// - 판을 연 직후 북을 플레이어 한 사람의 판 가운데에 모은다. 보통 판은 북 자리를 seed로 뽑지만, 튜토리얼은 언제나
    ///   가운데 앉은 "나"가 북을 들고 시작해야 첫 동작(북 치기)을 바로 할 수 있다.
    /// - 북에서 나올 구슬 종류를 차례로 정해 둔다 (1단계: 음·양·음).
    /// - 기운 회복을 멈추거나 정한 만큼까지만 차게 한다 (1단계: 바닥난 막대가 살짝만 차서 기운 부족 빨간 빛이 보이게).
    ///   막힌 동안의 회복은 나중에 한꺼번에 들어오지 않는다.
    /// </summary>
    public sealed partial class LabModel
    {
        private readonly Queue<LabOrbKind> tutorialGenerations = new Queue<LabOrbKind>();
        private ulong tutorialGenerationOwner;
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
        public bool TutorialScriptGenerations(ulong owner, params LabOrbKind[] kinds)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(owner) || kinds == null) return false;
            foreach (LabOrbKind kind in kinds)
                if (!Enum.IsDefined(typeof(LabOrbKind), kind) || kind.IsDrum()) return false;
            tutorialGenerations.Clear();
            tutorialGenerationOwner = owner;
            foreach (LabOrbKind kind in kinds) tutorialGenerations.Enqueue(kind);
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

        private LabOrbKind TutorialScriptedKind(ulong sender, LabOrbKind drawn) =>
            tutorialGenerations.Count > 0 && sender == tutorialGenerationOwner ? tutorialGenerations.Dequeue() : drawn;
    }
}
