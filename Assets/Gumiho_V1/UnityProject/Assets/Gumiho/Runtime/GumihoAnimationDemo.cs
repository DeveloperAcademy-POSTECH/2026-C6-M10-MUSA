using UnityEngine;

namespace ADA.Gumiho
{
    /// <summary>Simple in-scene preview controls for the supplied animation states.</summary>
    public sealed class GumihoAnimationDemo : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start() => Play("Idle");

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Play("Idle");
            if (Input.GetKeyDown(KeyCode.Alpha2)) Play("Walk_InPlace");
            if (Input.GetKeyDown(KeyCode.Alpha3)) Play("Run_InPlace");
            if (Input.GetKeyDown(KeyCode.Alpha4)) Play("Howl_Threat");
            if (Input.GetKeyDown(KeyCode.Alpha5)) Play("Pounce_Attack");
            if (Input.GetKeyDown(KeyCode.Alpha6)) Play("Hit_Preview");
        }

        private void Play(string state)
        {
            if (animator != null) animator.Play(state, 0, 0f);
        }
    }
}
