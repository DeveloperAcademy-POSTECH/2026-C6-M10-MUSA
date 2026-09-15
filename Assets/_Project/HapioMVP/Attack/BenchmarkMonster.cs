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
        [SerializeField] private BoxCollider hitbox;
        [SerializeField] private Transform visual;
        [SerializeField] private SplitScreenLayout runtimeLayout;
        private string appliedTargetId;
        private Vector3 appliedPosition, appliedCenter, appliedSize;
        private bool hasApplied;

        public ScreenLayoutConfig Config => runtimeLayout != null ? runtimeLayout.Config : config;
        public ScreenLayoutConfig SavedConfig => config;
        public BoxCollider Hitbox => hitbox;
        public Transform Visual => visual;
        public MonsterHitTarget Target => GetComponent<MonsterHitTarget>();

        public void Configure(ScreenLayoutConfig sharedConfig, BoxCollider collider, Transform visualRoot)
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
            hitbox.center = value.MonsterHitboxCenter;
            hitbox.size = value.MonsterHitboxSize;
            hitbox.isTrigger = false;
            hitbox.enabled = true;
            appliedTargetId = value.MonsterTargetId;
            appliedPosition = value.MonsterPosition;
            appliedCenter = value.MonsterHitboxCenter;
            appliedSize = value.MonsterHitboxSize;
            hasApplied = true;
        }
    }
}
