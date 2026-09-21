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
        private const float BlendSeconds = .08f;        // DEMO_TUNING_VALUE: fade into a one-shot
        private const float ReturnBlendSeconds = .2f;   // DEMO_TUNING_VALUE: fade back to Idle
        private Animator animator;
        private int oneShotState;

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

        public void PlayHit() => PlayOneShot(HitState);

        private void PlayOneShot(int state)
        {
            if (animator == null || !animator.isActiveAndEnabled || !animator.HasState(0, state)) return;
            animator.CrossFadeInFixedTime(state, BlendSeconds, 0, 0f);
            oneShotState = state;
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