using System;
using System.Collections.Generic;
using C6.Prototype.Attack;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// #28 shows the Host monster attack on every screen: the monster turns to face the target's seat, Claw_Attack
    /// lands its Slash_Impact when the warning ends, then it returns to Idle and faces forward. Only the target sees
    /// the edge warning (light green once it holds the defense stance). Display only: target, timing, and result come
    /// from the Host battle snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterAttackPresenter : MonoBehaviour
    {
        private const float TurnDegreesPerSecond = 300f; // DEMO_TUNING_VALUE
        private BattleSession battle;
        private AttackSession attack;
        private MonsterMotion motion;
        private Transform visual;
        private ThrowBattleFraming framing;
        private MonsterAttackWarning warning;
        private MonsterDefenseInput defense;
        private Func<double?> hostClock;
        private Quaternion restRotation = Quaternion.identity;
        private int clawSequence;
        private uint clawRound;

        public void Configure(BattleSession battleSession, AttackSession attackSession, MonsterMotion monsterMotion,
            BenchmarkMonster monster, ThrowBattleFraming battleFraming, MonsterAttackWarning attackWarning, MonsterDefenseInput defenseInput = null)
        {
            battle = battleSession; attack = attackSession; motion = monsterMotion; framing = battleFraming; warning = attackWarning; defense = defenseInput;
            visual = monster != null ? monster.Visual : null;
            if (visual != null) restRotation = visual.localRotation;
        }

        /// <summary>Host time estimate for this screen. Without one only the Host, whose clock the snapshot uses, can present.</summary>
        public void ConfigureHostClock(Func<double?> clock) => hostClock = clock;

        /// <summary>Claw_Attack starts early enough for Slash_Impact to land when the warning ends (or at once for a short warning).</summary>
        public static double ClawStartsAt(double warningStartsAt, double warningEndsAt, double impactSeconds) =>
            Math.Max(warningStartsAt, warningEndsAt - impactSeconds);

        /// <summary>The seat yaw of the target in the frozen roster, the same angle its camera uses; null if unknown.</summary>
        public static float? TargetYaw(IReadOnlyList<ulong> roster, ulong target)
        {
            if (roster == null || roster.Count < 2 || roster.Count > 5) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] == target) return ParticipantViewAngle.CalculateYaw(i + 1, roster.Count);
            return null;
        }

        /// <summary>An attack is shown until its claw recovers, unless the round ended before it resolved.</summary>
        public static bool Presents(BattleSnapshot state, double hostNow) => state != null && state.attackSequence > 0
            && (state.attackActive || state.attackResolvedSequence == state.attackSequence)
            && hostNow < state.attackWarningEndsAt + MonsterMotion.ClawAttackSeconds - MonsterMotion.ClawImpactSeconds;

        /// <summary>
        /// A running claw stops when its attack no longer exists: the session ended, a new round began, or the round
        /// ended before the attack resolved. A resolved attack (Hit or Defended) always finishes its claw.
        /// </summary>
        public static bool CancelsClaw(BattleSnapshot state) => state == null || state.attackSequence == 0
            || !state.attackActive && state.attackResolvedSequence != state.attackSequence;

        /// <summary>Only the attacked player's screen warns, and only during the Host warning window.</summary>
        public static bool Warns(BattleSnapshot state, ulong localPlayer, double hostNow) => state != null
            && state.phase == BattlePhase.Playing.ToString() && state.attackActive && state.attackTarget == localPlayer
            && hostNow < state.attackWarningEndsAt;

        private double? HostNow()
        {
            if (hostClock != null) return hostClock();
            return battle != null && battle.IsHost ? Time.realtimeSinceStartupAsDouble : (double?)null;
        }

        private void Update()
        {
            var state = battle != null ? battle.Snapshot : null;
            double? now = HostNow();
            bool presents = now.HasValue && attack != null && Presents(state, now.Value);
            float? yaw = presents ? TargetYaw(attack.OrderedParticipantIds, state.attackTarget) : null;

            if (presents && (state.attackSequence != clawSequence || state.roundId != clawRound) && motion != null)
            {
                double clawStartsAt = ClawStartsAt(state.attackWarningStartsAt, state.attackWarningEndsAt, MonsterMotion.ClawImpactSeconds);
                if (now.Value >= clawStartsAt)
                {
                    motion.PlayClawAttack((float)(now.Value - clawStartsAt));
                    clawSequence = state.attackSequence; clawRound = state.roundId;
                }
            }
            if (motion != null && motion.Attacking && CancelsClaw(state)) motion.StopAttack();

            if (visual != null)
            {
                var goal = yaw.HasValue ? Quaternion.AngleAxis(yaw.Value, Vector3.up) * restRotation : restRotation;
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation, goal, TurnDegreesPerSecond * Time.unscaledDeltaTime);
            }
            // The turn and claw change the visual bounds; keep the solved camera instead of zooming with the pose.
            if (framing != null)
                framing.HoldFraming = presents || visual != null && Quaternion.Angle(visual.localRotation, restRotation) > .01f;

            if (warning != null)
            {
                if (now.HasValue && attack != null && Warns(state, attack.LocalPlayerId, now.Value))
                    warning.Show(now.Value - state.attackWarningStartsAt, defense == null ? WarningLook.Pulse
                        : defense.InStance ? WarningLook.Stance : defense.Holding ? WarningLook.Holding : WarningLook.Pulse);
                else if (warning.Visible) warning.Hide();
            }
        }

        private void OnDisable()
        {
            if (warning != null) warning.Hide();
            if (motion != null) motion.StopAttack();
            if (visual != null) visual.localRotation = restRotation;
            if (framing != null) framing.HoldFraming = false;
            clawSequence = 0; clawRound = 0;
        }
    }
}
