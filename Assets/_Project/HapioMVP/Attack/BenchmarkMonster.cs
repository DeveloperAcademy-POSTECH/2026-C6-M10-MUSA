using System;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>
    /// Config-driven training target geometry. No HP, damage, AI, or victory state lives here:
    /// the existing Host attack/battle authorities remain the only gameplay source of truth.
    /// </summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(MonsterHitTarget))]
    public sealed class BenchmarkMonster : MonoBehaviour
    {
        [SerializeField] private ScreenLayoutConfig config;
        [SerializeField] private Collider hitbox;
        [SerializeField] private Transform visual;
        [SerializeField] private SplitScreenLayout runtimeLayout;
        private string appliedTargetId;
        private Vector3 appliedPosition, appliedCenter, appliedSize;
        private bool hasApplied;

        public ScreenLayoutConfig Config => runtimeLayout != null ? runtimeLayout.Config : config;
        public ScreenLayoutConfig SavedConfig => config;
        public Collider Hitbox => hitbox;
        public Transform Visual => visual;
        public MonsterHitTarget Target => GetComponent<MonsterHitTarget>();

        public void Configure(ScreenLayoutConfig sharedConfig, Collider collider, Transform visualRoot)
        {
            if (sharedConfig == null || collider == null || visualRoot == null)
                throw new ArgumentNullException(nameof(sharedConfig), "The shared Config, hitbox, and visual are required.");
            if (collider.transform.parent != transform || visualRoot.parent != transform || collider.transform == visualRoot)
                throw new ArgumentException("Hitbox and Visual must be separate direct children of the target root.");
            config = sharedConfig; hitbox = collider; visual = visualRoot;
            ApplyConfiguration();
        }

        public void ConfigureRuntimeLayout(SplitScreenLayout layout)
        {
            runtimeLayout = layout;
            if (config != null && hitbox != null && visual != null) ApplyConfiguration();
        }

        private void Awake() => ApplyConfiguration();
        private void LateUpdate()
        {
            var value = Config;
            if (value != null && (!hasApplied || appliedTargetId != value.MonsterTargetId
                || appliedPosition != value.MonsterPosition || appliedCenter != value.MonsterHitboxCenter
                || appliedSize != value.MonsterHitboxSize)) ApplyConfiguration();
        }

        public void ApplyConfiguration()
        {
            var value = Config;
            if (value == null || hitbox == null || visual == null)
                throw new InvalidOperationException("The benchmark monster is missing its Config or child references.");
            if (hitbox.transform.parent != transform || visual.parent != transform)
                throw new InvalidOperationException("Benchmark monster child references must remain under the target root.");
            Target.Configure(value.MonsterTargetId);
            transform.position = value.MonsterPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            hitbox.transform.localPosition = Vector3.zero;
            hitbox.transform.localRotation = Quaternion.identity;
            hitbox.transform.localScale = Vector3.one;

            switch (hitbox)
            {
                // Vertical capsule: every seat around the monster sees the same hit width.
                // MonsterHitboxSize.x is the diameter (z is ignored), y is the full height.
                case CapsuleCollider capsule:
                    capsule.direction = 1;
                    capsule.center = value.MonsterHitboxCenter;
                    capsule.radius = value.MonsterHitboxSize.x * .5f;
                    capsule.height = Mathf.Max(value.MonsterHitboxSize.y, capsule.radius * 2f);
                    break;
                case BoxCollider box:
                    box.center = value.MonsterHitboxCenter;
                    box.size = value.MonsterHitboxSize;
                    break;
                default:
                    throw new InvalidOperationException("The monster hitbox must be a BoxCollider or CapsuleCollider.");
            }

            hitbox.isTrigger = false;
            hitbox.enabled = true;
            appliedTargetId = value.MonsterTargetId;
            appliedPosition = value.MonsterPosition;
            appliedCenter = value.MonsterHitboxCenter;
            appliedSize = value.MonsterHitboxSize;
            hasApplied = true;
        }

        private void OnDrawGizmos()
        {
            if (hitbox == null) return;
            Gizmos.color = new Color(1f, .3f, .2f, .9f);
            Gizmos.matrix = hitbox.transform.localToWorldMatrix;
            switch (hitbox)
            {
                case CapsuleCollider capsule:
                    float half = Mathf.Max(0f, capsule.height * .5f - capsule.radius);
                    Vector3 top = capsule.center + Vector3.up * half, bottom = capsule.center - Vector3.up * half;
                    Gizmos.DrawWireSphere(top, capsule.radius);
                    Gizmos.DrawWireSphere(bottom, capsule.radius);
                    foreach (var side in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                        Gizmos.DrawLine(top + side * capsule.radius, bottom + side * capsule.radius);
                    break;
                case BoxCollider box:
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;
            }
        }
    }
}
