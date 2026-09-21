using UnityEngine;

namespace C6.Prototype.Battle
{

    /// Presentation-only monster animation driver. The generated Jangsanbeom controller has no transitions,
    /// so one-shot clips are cross-faded in code and return to Idle when they finish.
    /// It never affects hits, damage, ownership, or the snapshot.
   
    [DisallowMultipleComponent]
    public sealed class MonsterMotion : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Claw_Attack");
        /// <summary>Claw_Attack is 136 frames at 30 fps (asset_manifest.json); Slash_Impact is its frame 74.</summary>
        public const float ClawAttackSeconds = 135f / 30f;
        public const float ClawImpactSeconds = 73f / 30f;
        private const float BlendSeconds = .08f;        // DEMO_TUNING_VALUE: fade into a one-shot
        private const float ReturnBlendSeconds = .2f;   // DEMO_TUNING_VALUE: fade back to Idle
        private Animator animator;
        private int oneShotState;
        private float attackUntil;

        /// <summary>#28: the attack owns the body until it ends; a hit reaction must not cut the claw short.</summary>
        public bool Attacking => oneShotState == AttackState && Time.unscaledTime < attackUntil;

        /// <summary>Finds the model Animator under the monster root and attaches this driver once.</summary>
        public static MonsterMotion For(Component monster)
        {
            var found = monster != null ? monster.GetComponentInChildren<Animator>(true) : null;
            if (found == null) return null;
            var motion = found.GetComponent<MonsterMotion>();
            if (motion == null) motion = found.gameObject.AddComponent<MonsterMotion>();
            motion.animator = found;
            return motion;
        }

        public void PlayHit()
        {
            if (!Attacking) PlayOneShot(HitState, 0f);
        }

        /// <summary>#28: starts Claw_Attack, skipping what a late observer already missed so every screen stays in step.</summary>
        public void PlayClawAttack(float elapsedSeconds)
        {
            float offset = Mathf.Clamp(elapsedSeconds, 0f, ClawAttackSeconds);
            if (!PlayOneShot(AttackState, offset)) return;
            attackUntil = Time.unscaledTime + ClawAttackSeconds - offset + .5f; // releases hits even if the clip never reports its end
        }

        private bool PlayOneShot(int state, float offsetSeconds)
        {
            if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, state)) return false;
            animator.CrossFadeInFixedTime(state, BlendSeconds, 0, offsetSeconds);
            oneShotState = state;
            return true;
        }

        private void Update()
        {
            if (oneShotState == 0 || animator == null || animator.IsInTransition(0)) return;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.fullPathHash != oneShotState || info.normalizedTime < 1f) return;
            animator.CrossFadeInFixedTime(IdleState, ReturnBlendSeconds, 0);
            oneShotState = 0;
        }
    }
}